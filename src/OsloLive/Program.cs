using System.Globalization;
using System.Text.Json;
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
builder.Services.AddSingleton<ILag, FlyLag>();

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

app.MapGet("/api/helse", () => new { status = "ok", tid = DateTimeOffset.Now });

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
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
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
