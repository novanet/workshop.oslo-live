using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using OsloLive;
using OsloLive.Bysykkel;
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

// Bysykkeldøgnet (#191) laster ned en hel månedsfil (rundt 90 MB) fra Oslo Bysykkels
// åpne data, ikke Allemannsdata. Derfor egen navngitt HttpClient med lang tidsgrense.
builder.Services.AddHttpClient("bysykkel", klient =>
{
    klient.Timeout = TimeSpan.FromMinutes(10);
    klient.DefaultRequestHeaders.UserAgent.ParseAdd("OsloLive/1.0 (kurs)");
});

// Bomstasjonene hentes fra NVDB (Statens vegvesen), som heller ikke er en Allemannsdata-kilde,
// og krever headeren X-Client. Se BomstasjonerLag.cs.
builder.Services.AddHttpClient("bomstasjoner", klient =>
{
    klient.Timeout = TimeSpan.FromSeconds(15);
    klient.DefaultRequestHeaders.UserAgent.ParseAdd("OsloLive/1.0 (kurs)");
    klient.DefaultRequestHeaders.Add("X-Client", "OsloLive");
    klient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
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

// Bysykkeldøgnet (#191): jobben starter først når noen spør, se Bysykkel/Bysykkeltjeneste.cs.
// Standardmappa ligger under appens rotmappe (App_Data/bysykkel, ikke i git). Overstyres med Bysykkel:Mappe.
builder.Services.AddSingleton(tjenester =>
{
    var mappe = tjenester.GetRequiredService<IConfiguration>()["Bysykkel:Mappe"];
    return new Bysykkeltjeneste(
        tjenester.GetRequiredService<IHttpClientFactory>(),
        tjenester.GetRequiredService<IMemoryCache>(),
        string.IsNullOrWhiteSpace(mappe)
            ? Path.Combine(tjenester.GetRequiredService<IHostEnvironment>().ContentRootPath, "App_Data", "bysykkel")
            : mappe,
        tjenester.GetRequiredService<ILogger<Bysykkeltjeneste>>());
});

// ---------------------------------------------------------------------------
// Lagene på kartet. Nytt lag? Legg til én linje her.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ILag, LuftkvalitetLag>();
builder.Services.AddSingleton<ILag, ArterLag>();
builder.Services.AddSingleton<ILag, HendelserLag>();
builder.Services.AddSingleton<ILag, SmilefjesLag>();
builder.Services.AddSingleton<ILag, FlyLag>();
builder.Services.AddSingleton<ILag, BadetemperaturLag>();
builder.Services.AddSingleton<ILag, MobilitetLag>();
builder.Services.AddSingleton<ILag, SpisestederLag>();
builder.Services.AddSingleton<ILag, HoldeplasserLag>();
builder.Services.AddSingleton<ILag, SkipLag>();
builder.Services.AddSingleton<ILag, VannmaalereLag>();
builder.Services.AddSingleton<ILag, KaierLag>();
builder.Services.AddSingleton<ILag, BomstasjonerLag>();
builder.Services.AddSingleton<ILag, VaerstasjonerLag>();
builder.Services.AddSingleton<ILag, IdrettsanleggLag>();

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
        return Results.Json(new { feil = Feiltekst.Fra(ex) }, statusCode: 502);
    }
});

// Antall punkter per time i ett lag, siste døgn, eldste først.
// Antall punkter per lagret bilde siste 24 timer (#25). Bildene ligger i App_Data/historikk i
// containerens filsystem: de overlever omstart av prosessen, men en ny revisjon uten volum
// starter med tom historikk som fylles igjen time for time. Se ARKITEKTUR.md.
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
        return Results.Json(new { feil = Feiltekst.Fra(ex) }, statusCode: 502);
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
        return Results.Json(new { feil = Feiltekst.Fra(ex) }, statusCode: 502);
    }
});

// Et helt døgn med bysykkelturer, den travleste hverdagen i siste hele måned (#191).
// Kilden er Oslo Bysykkels egne åpne data, ikke Allemannsdata; se Bysykkel/.
// Første kall etter at måneden skifter kan ta tid, siden månedsfila (rundt 90 MB)
// må lastes ned og strømmes gjennom; da svarer endepunktet 202 mens jobben går i bakgrunnen.
app.MapGet("/api/bysykkeldogn", (Bysykkeltjeneste tjeneste) => tjeneste.Hent(DateTimeOffset.UtcNow) switch
{
    Tilstand.Klar k => Results.Ok(k.Døgn),
    Tilstand.Feilet f => LoggFeilOgSvar(f.Feil),
    _ => Results.Json(new { status = "forbereder" }, statusCode: 202),
});

IResult LoggFeilOgSvar(string feil)
{
    app.Logger.LogError("Bysykkeldøgnet feilet: {Feil}", feil);
    return Results.Json(new { feil }, statusCode: 502);
}

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

// Vannstand og neste høy-/lavvann i Oslo havn. Ikke et kartlag med mange punkter, så eget endepunkt.
app.MapGet("/api/vannstand", async (Allemannsdata data, CancellationToken stopp) =>
{
    try
    {
        var naaRader = await data.HentListe(Vannstand.Kilde, Vannstand.OperasjonNaa, Vannstand.Parametre(), liste: null, stopp);
        var tabellRader = await data.HentListe(Vannstand.Kilde, Vannstand.OperasjonNeste, Vannstand.Parametre(), liste: null, stopp);
        return Results.Ok(Vannstand.Tolk(naaRader, tabellRader, DateTimeOffset.UtcNow));
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Vannstand feilet");
        return Results.Json(new { feil = Feiltekst.Fra(ex) }, statusCode: 502);
    }
});

app.Run();

/// <summary>Gjør Program synlig for testprosjektet.</summary>
public partial class Program;

/// <summary>
/// Tolker tidevannssvaret fra Kartverket (via Allemannsdata, kilde «weather»).
/// Ren funksjon, ingen tilstand.
/// </summary>
public static class Vannstand
{
    public const string Kilde = "weather";

    /// <summary>Tidsserie med aktuell/nær sanntids sjøstand, brukt til «naa».</summary>
    public const string OperasjonNaa = "get_tide_forecast";

    /// <summary>Tabell over kommende høy- og lavvann, brukt til «neste».</summary>
    public const string OperasjonNeste = "get_tide_table";

    // Samme parametre til begge operasjonene, og ingen tidsstempel i dem,
    // så adressen er lik fra kall til kall og mellomlageret i Allemannsdata treffer.
    public static IReadOnlyDictionary<string, object> Parametre() => new Dictionary<string, object>
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
    };

    public sealed record Neste(string Type, DateTimeOffset Tidspunkt, double Verdi);

    public sealed record Svar(double Naa, DateTimeOffset Maalt, Neste Neste);

    /// <summary>
    /// «naa»/«maalt»: raden i tidsserien med tidspunkt nærmest <paramref name="nå"/>.
    /// «neste»: første rad i tidevannstabellen med tidspunkt etter <paramref name="nå"/>.
    /// </summary>
    public static Svar Tolk(IReadOnlyList<JsonElement> naaRader, IReadOnlyList<JsonElement> tabellRader, DateTimeOffset nå)
    {
        var naaKandidater = naaRader
            .Select(rad => (
                Tid: DateTimeOffset.Parse(rad.GetProperty("time").GetString()!, CultureInfo.InvariantCulture),
                Verdi: rad.GetProperty("sea_level_cm").GetDouble()))
            .OrderBy(p => Math.Abs((p.Tid - nå).Ticks))
            .ToList();

        if (naaKandidater.Count == 0)
        {
            throw new InvalidOperationException("Fant ingen vannstandsdata for Oslo havn.");
        }

        var naa = naaKandidater[0];

        var nesteKandidater = tabellRader
            .Select(rad => (
                Tid: DateTimeOffset.Parse(rad.GetProperty("time").GetString()!, CultureInfo.InvariantCulture),
                Verdi: rad.GetProperty("height_cm").GetDouble(),
                Kind: rad.GetProperty("kind").GetString()))
            .Where(p => p.Tid > nå)
            .OrderBy(p => p.Tid)
            .ToList();

        if (nesteKandidater.Count == 0)
        {
            throw new InvalidOperationException("Fant ingen fremtidig høyvann eller lavvann for Oslo havn.");
        }

        var neste = nesteKandidater[0];
        var type = neste.Kind switch
        {
            "high" => "høyvann",
            "low" => "lavvann",
            var ukjent => throw new InvalidOperationException($"Ukjent tidevannstype «{ukjent}»."),
        };

        return new Svar(naa.Verdi, naa.Tid, new Neste(type, neste.Tid, neste.Verdi));
    }
}
