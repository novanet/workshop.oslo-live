using System.Globalization;
using OsloLive;
using OsloLive.Helse;
using OsloLive.Historikk;
using OsloLive.Kart;
using OsloLive.Lag;

// Oslo Live: et kart over Oslo som henter levende data fra Allemannsdata.
// Hvert lag på kartet er en klasse som implementerer ILag.

// Appen er norsk, så den kjører med norsk kultur.
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("nb-NO");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("nb-NO");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<Allemannsdata>(klient =>
{
    klient.Timeout = TimeSpan.FromSeconds(30);
    klient.DefaultRequestHeaders.UserAgent.ParseAdd("OsloLive/1.0 (kurs)");
});

// Flylaget bruker ikke Allemannsdata (ingen kilde der har flyposisjoner),
// og trenger derfor sin egen navngitte HttpClient og eget mellomlager. Se FlyLag.cs.
builder.Services.AddHttpClient("fly", klient =>
{
    klient.Timeout = TimeSpan.FromSeconds(10);
    klient.DefaultRequestHeaders.UserAgent.ParseAdd("OsloLive/1.0 (kurs)");
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Lagstatistikk>();

// Øyeblikksbilder av hvert lag hver time, slik at tidslinjen kan vise hvordan kartet så ut. Se Historikk/.
// Standardmappa ligger under appens rotmappe (App_Data/historikk, ikke i git), ikke i temp, slik at
// den er forutsigbar og kan monteres som volum i containeren. Overstyres med Historikk:Mappe.
builder.Services.AddSingleton(tjenester =>
{
    var mappe = tjenester.GetRequiredService<IConfiguration>()["Historikk:Mappe"];
    return new Bildelager(string.IsNullOrWhiteSpace(mappe)
        ? Path.Combine(tjenester.GetRequiredService<IHostEnvironment>().ContentRootPath, "App_Data", "historikk")
        : mappe);
});
builder.Services.AddHostedService<Øyeblikksjobb>();

// ---------------------------------------------------------------------------
// Lagene på kartet. Nytt lag? Legg til én linje her.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ILag, LuftkvalitetLag>();
builder.Services.AddSingleton<ILag, SmilefjesLag>();
builder.Services.AddSingleton<ILag, FlyLag>();
builder.Services.AddSingleton<ILag, BadetemperaturLag>();
builder.Services.AddSingleton<ILag, MobilitetLag>();
builder.Services.AddSingleton<ILag, SpisestederLag>();
builder.Services.AddSingleton<ILag, SkipLag>();
builder.Services.AddSingleton<ILag, VannmaalereLag>();

// Bakgrunnssjekk av kildehelse, se Helse/HelseSjekker.cs.
builder.Services.Configure<HelseValg>(builder.Configuration.GetSection("Helse"));
builder.Services.AddSingleton<HelseSjekker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HelseSjekker>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Hvilke lag finnes?
app.MapGet("/api/lag", (IEnumerable<ILag> lag) =>
    lag.Select(l => new { id = l.Id, navn = l.Navn, beskrivelse = l.Beskrivelse, ikon = l.Ikon }));

// Punktene i ett lag, som GeoJSON. Med ?tid= hentes bildet lagret nærmest det tidspunktet, se Historikk/.
// Bare levende henting oppdaterer statistikken; historiske bilder gjør det ikke.
app.MapGet("/api/lag/{id}", async (string id, string? tid, IEnumerable<ILag> lag, Bildelager bilder, Lagstatistikk statistikk, CancellationToken stopp) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
    }

    if (tid is not null)
    {
        if (!Bildelager.TolkTid(tid, out var tidspunkt))
        {
            return Results.BadRequest(new { feil = $"«{tid}» er ikke et gyldig tidspunkt. Bruk ISO 8601, for eksempel 2026-09-18T08:00:00Z." });
        }

        return Results.Ok(await bilder.HentNærmest(valgt.Id, tidspunkt, stopp) ?? new Kartlag("FeatureCollection", []));
    }

    try
    {
        var kartlag = await valgt.Hent(stopp);
        statistikk.Vellykket(valgt.Id, kartlag, DateTimeOffset.UtcNow);
        return Results.Ok(kartlag);
    }
    catch (Exception ex)
    {
        // Et lag som feiler skal ikke ta ned kartet.
        if (!stopp.IsCancellationRequested)
        {
            statistikk.Feilet(valgt.Id);
        }

        app.Logger.LogError(ex, "Laget {Id} feilet", id);
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
    }
});

// Antall punkter per bydel for ett lag. Tilstandsløs; bydelssentrene hentes
// via Allemannsdata (mellomlagret 30 s der, som resten av kallene).
// Med ?tid= telles bildet lagret nærmest det tidspunktet, som for /api/lag/{id}, slik at tellingen følger tidslinjen.
app.MapGet("/api/lag/{id}/bydeler", async (string id, string? tid, IEnumerable<ILag> lag, Allemannsdata data, Bildelager bilder, CancellationToken stopp) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
    }

    DateTimeOffset? tidspunkt = null;
    if (tid is not null)
    {
        if (!Bildelager.TolkTid(tid, out var t))
        {
            return Results.BadRequest(new { feil = $"«{tid}» er ikke et gyldig tidspunkt. Bruk ISO 8601, for eksempel 2026-09-18T08:00:00Z." });
        }

        tidspunkt = t;
    }

    try
    {
        var punkter = tidspunkt is null
            ? await valgt.Hent(stopp)
            : await bilder.HentNærmest(valgt.Id, tidspunkt.Value, stopp) ?? new Kartlag("FeatureCollection", []);
        var sentre = await Bydeler.Hent(data, stopp);
        return Results.Ok(Bydeler.Tell(punkter, sentre));
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Bydelstelling for laget {Id} feilet", id);
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
    }
});

// Strømprisen i Oslo (prisområde NO1) i dag. Ikke et kartlag, ingen tilstand.
app.MapGet("/api/stroempris", async (Allemannsdata data, CancellationToken stopp) =>
{
    try
    {
        var nå = DateTimeOffset.UtcNow;
        var iDag = TimeZoneInfo.ConvertTime(nå, Stroempris.Oslo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var rader = await data.HentListe(
            Stroempris.Kilde,
            Stroempris.Operasjon,
            new Dictionary<string, object> { ["date"] = iDag, ["area"] = Stroempris.Område },
            liste: "prices",
            stopp);

        var svar = Stroempris.Tolk(rader, nå)
            ?? throw new InvalidOperationException("Fant ingen strømpris for i dag i prisområde NO1.");

        return Results.Ok(svar);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Strømprisen feilet");
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
    }
});

// Lever prosessen? Svarer alltid umiddelbart, uten å spørre kildene.
app.MapGet("/api/helse", () => new { status = "ok", tid = DateTimeOffset.Now });

// Tall om lagene, fra det /api/lag/{id} sist hentet. Henter ingenting selv.
app.MapGet("/api/statistikk", (IEnumerable<ILag> lag, Lagstatistikk statistikk) =>
    lag.Select(l =>
    {
        var s = statistikk.Hent(l.Id);
        return new { id = l.Id, navn = l.Navn, antall = s.Antall, eldste = s.Eldste, nyeste = s.Nyeste, hentet = s.Hentet, feiler = s.Feiler };
    }));

// Virker tjenesten? Leser siste kjente resultat fra bakgrunnssjekken.
app.MapGet("/api/helse/kilder", (HelseSjekker sjekker) =>
{
    var kilder = sjekker.Snapshot();
    return Results.Ok(new { status = HelseSjekker.SamletStatus(kilder), kilder });
});

app.Run();

/// <summary>Gjør Program synlig for testprosjektet.</summary>
public partial class Program;
