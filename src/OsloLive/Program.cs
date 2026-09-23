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

// ---------------------------------------------------------------------------
// Lagene på kartet. Nytt lag? Legg til én linje her.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ILag, LuftkvalitetLag>();
builder.Services.AddSingleton<ILag, SmilefjesLag>();
builder.Services.AddSingleton<ILag, FlyLag>();
builder.Services.AddSingleton<ILag, BadetemperaturLag>();
builder.Services.AddSingleton<ILag, MobilitetLag>();

// Bakgrunnssjekk av kildehelse, se Helse/HelseSjekker.cs.
builder.Services.Configure<HelseValg>(builder.Configuration.GetSection("Helse"));
builder.Services.AddSingleton<HelseSjekker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HelseSjekker>());

// Historikk: ett bilde av hvert lag i timen, lagret som filer. Se Historikk/Bildejobb.cs.
builder.Services.AddSingleton(tjenester =>
    new Bildelager(Bildelager.FinnMappe(tjenester.GetRequiredService<IConfiguration>()["Historikk:Mappe"])));
builder.Services.AddHostedService<Bildejobb>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Hvilke lag finnes?
app.MapGet("/api/lag", (IEnumerable<ILag> lag) =>
    lag.Select(l => new { id = l.Id, navn = l.Navn, beskrivelse = l.Beskrivelse, ikon = l.Ikon }));

// Punktene i ett lag, som GeoJSON.
app.MapGet("/api/lag/{id}", async (string id, IEnumerable<ILag> lag, CancellationToken stopp) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
    }

    try
    {
        return Results.Ok(await valgt.Hent(stopp));
    }
    catch (Exception ex)
    {
        // Et lag som feiler skal ikke ta ned kartet.
        app.Logger.LogError(ex, "Laget {Id} feilet", id);
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
    }
});

// Antall punkter per time i ett lag, siste døgn, eldste først.
app.MapGet("/api/lag/{id}/historikk", (string id, IEnumerable<ILag> lag, Bildelager lager) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
    }

    return Results.Ok(lager.Les(valgt.Id, DateTimeOffset.UtcNow));
});

// Antall punkter per bydel for ett lag. Tilstandsløs; bydelssentrene hentes
// via Allemannsdata (mellomlagret 30 s der, som resten av kallene).
app.MapGet("/api/lag/{id}/bydeler", async (string id, IEnumerable<ILag> lag, Allemannsdata data, CancellationToken stopp) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
    }

    try
    {
        var punkter = await valgt.Hent(stopp);
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

// Virker tjenesten? Leser siste kjente resultat fra bakgrunnssjekken.
app.MapGet("/api/helse/kilder", (HelseSjekker sjekker) =>
{
    var kilder = sjekker.Snapshot();
    return Results.Ok(new { status = HelseSjekker.SamletStatus(kilder), kilder });
});

app.Run();

/// <summary>Gjør Program synlig for testprosjektet.</summary>
public partial class Program;
