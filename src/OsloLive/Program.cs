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

// ---------------------------------------------------------------------------
// Lagene på kartet. Nytt lag? Legg til én linje her.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ILag, LuftkvalitetLag>();

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

// Avgangstavle for Gardermoen. Ikke et kartlag (ingen koordinater), derfor
// eget endepunkt uten tilstand, som adressesøket.
app.MapGet("/api/avganger", async (Allemannsdata data, CancellationToken stopp) =>
{
    try
    {
        var felles = new Dictionary<string, object>
        {
            ["airport_id"] = "OSL",
            ["hours_back"] = 0,
            ["hours_ahead"] = 1,
            ["limit"] = 100,
        };

        var avgangRader = await data.HentListe("avinor", "get_flights",
            new Dictionary<string, object>(felles) { ["direction"] = "departures" }, liste: "flights", stopp);
        var ankomstRader = await data.HentListe("avinor", "get_flights",
            new Dictionary<string, object>(felles) { ["direction"] = "arrivals" }, liste: "flights", stopp);

        var alleRader = avgangRader.Concat(ankomstRader).ToList();

        var flyselskaper = await Avgangstavle.SlåOppNavn(data, "lookup_airline",
            alleRader.Select(r => r.TryGetProperty("airline", out var a) ? a.GetString() : null), stopp);
        var flyplasser = await Avgangstavle.SlåOppNavn(data, "lookup_airport",
            alleRader.Select(r => r.TryGetProperty("airport", out var a) ? a.GetString() : null), stopp);

        var nå = DateTimeOffset.UtcNow;
        return Results.Ok(new
        {
            avganger = Avgangstavle.Neste(avgangRader.Select(r => Avgangstavle.Tolk(r, flyselskaper, flyplasser)), nå),
            ankomster = Avgangstavle.Neste(ankomstRader.Select(r => Avgangstavle.Tolk(r, flyselskaper, flyplasser)), nå),
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Avgangstavla feilet");
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
    }
});

app.Run();

/// <summary>Gjør Program synlig for testprosjektet.</summary>
public partial class Program;

/// <summary>Én rad på avgangstavla for Gardermoen.</summary>
public sealed record Flyvning(string Flynummer, string Flyselskap, string Sted, DateTimeOffset Planlagt, string Status, bool Forsinket);

/// <summary>
/// Oversetter rader fra Avinor (via Allemannsdata) til avgangstavla.
/// Ren logikk i <see cref="Tolk"/> og <see cref="Neste"/>, ingen nettverk.
/// <see cref="SlåOppNavn"/> slår opp fulle navn for koder mot Avinor-kilden.
/// </summary>
public static class Avgangstavle
{
    public const int MaksAntall = 30;
    public static readonly TimeSpan Vindu = TimeSpan.FromMinutes(60);

    /// <summary>Slår opp fulle navn for en liste med koder. Ukjente koder faller tilbake til koden selv.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> SlåOppNavn(
        Allemannsdata data, string operasjon, IEnumerable<string?> koder, CancellationToken stopp)
    {
        var resultat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kode in koder.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var treff = await data.HentListe(
                "avinor", operasjon, new Dictionary<string, object> { ["query"] = kode! }, stopp: stopp);

            var eksakt = treff.FirstOrDefault(t =>
                t.TryGetProperty("code", out var c) && string.Equals(c.GetString(), kode, StringComparison.OrdinalIgnoreCase));

            resultat[kode!] = eksakt.ValueKind == JsonValueKind.Object
                && eksakt.TryGetProperty("name", out var navn)
                && navn.GetString() is string n
                && !string.IsNullOrWhiteSpace(n)
                    ? n
                    : kode!;
        }

        return resultat;
    }

    /// <summary>Oversetter én Avinor-rad til en <see cref="Flyvning"/>. Kaster hvis flynummer eller planlagt tid mangler.</summary>
    public static Flyvning Tolk(JsonElement rad, IReadOnlyDictionary<string, string> flyselskaper, IReadOnlyDictionary<string, string> flyplasser)
    {
        var flynummer = rad.GetProperty("flight_id").GetString()
            ?? throw new InvalidOperationException("Fly uten flynummer.");

        var planlagt = DateTimeOffset.Parse(
            rad.GetProperty("schedule_time").GetString() ?? throw new InvalidOperationException("Fly uten planlagt tid."),
            CultureInfo.InvariantCulture);

        var flyselskapKode = rad.TryGetProperty("airline", out var flyselskapFelt) ? flyselskapFelt.GetString() : null;
        var flyselskap = SlåOpp(flyselskapKode, flyselskaper);

        var stedKode = rad.TryGetProperty("airport", out var stedFelt) ? stedFelt.GetString() : null;
        var sted = SlåOpp(stedKode, flyplasser);

        var forsinket = rad.TryGetProperty("delayed", out var delayFelt) && delayFelt.ValueKind == JsonValueKind.True;

        string? statusKode = null;
        DateTimeOffset? nyTid = null;
        if (rad.TryGetProperty("status", out var statusFelt) && statusFelt.ValueKind == JsonValueKind.Object)
        {
            statusKode = statusFelt.TryGetProperty("code", out var kodeFelt) ? kodeFelt.GetString() : null;
            if (statusFelt.TryGetProperty("time", out var tidFelt) && tidFelt.ValueKind == JsonValueKind.String)
            {
                nyTid = DateTimeOffset.Parse(tidFelt.GetString()!, CultureInfo.InvariantCulture);
            }
        }

        return new(flynummer, flyselskap, sted, planlagt, Status(statusKode, forsinket, nyTid), forsinket);
    }

    private static string SlåOpp(string? kode, IReadOnlyDictionary<string, string> navn) =>
        !string.IsNullOrWhiteSpace(kode) && navn.TryGetValue(kode, out var funnet) ? funnet : kode ?? "";

    /// <summary>Statustekst på norsk. Forsinkelse (Avinors «delayed») går foran statuskoden.</summary>
    public static string Status(string? kode, bool forsinket, DateTimeOffset? nyTid)
    {
        if (forsinket)
        {
            return nyTid.HasValue
                ? $"Forsinket, ny tid {nyTid.Value.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                : "Forsinket";
        }

        return kode switch
        {
            "C" => "Innstilt",
            "D" => "Avgått",
            "A" => "Landet",
            "E" => "Ny tid",
            "N" => "Ny info",
            _ => "I rute",
        };
    }

    /// <summary>Fly de neste <see cref="Vindu"/> fra <paramref name="nå"/>, sortert på planlagt tid, maks <see cref="MaksAntall"/>.</summary>
    public static IReadOnlyList<Flyvning> Neste(IEnumerable<Flyvning> fly, DateTimeOffset nå) =>
        [.. fly.Where(f => f.Planlagt >= nå && f.Planlagt <= nå + Vindu).OrderBy(f => f.Planlagt).Take(MaksAntall)];
}
