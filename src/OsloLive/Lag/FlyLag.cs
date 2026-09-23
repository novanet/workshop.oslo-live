using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Fly i lufta over Oslo, og fly på bakken der utsnittet dekker en flyplass.
///
/// Allemannsdata har ingen kilde med flyposisjoner: «avinor» gir bare
/// rutetider og status (ingen lat/lon). Dette laget bruker derfor
/// airplanes.live, et gratis, nøkkelfritt ADS-B-nettverk drevet av
/// entusiaster, i stedet for Allemannsdata-klienten. Bruddet med
/// ARKITEKTUR.md er isolert her: eget navngitt <see cref="HttpClient"/> fra
/// <see cref="IHttpClientFactory"/>, mellomlager i delt <see cref="IMemoryCache"/>
/// i stedet for et felt på klassen. De andre lagene er upåvirket. Svikter
/// kilden (feil, tidsavbrudd, avvisning), kaster <see cref="Hent"/>, og
/// laget blir rødt i lagvelgeren; det tar ikke ned resten av kartet.
/// </summary>
public sealed class FlyLag(IHttpClientFactory httpFactory, IMemoryCache mellomlager) : ILag
{
    private const string KlientNavn = "fly";
    private const string MellomlagerNøkkel = "fly-mellomlager";
    private const string Kilde = "airplanes.live (ADS-B)";
    private const double RadiusNautiskeMil = 30;
    private static readonly TimeSpan Levetid = TimeSpan.FromSeconds(30);

    public string Id => "fly";
    public string Navn => "Flytrafikk";
    public string Beskrivelse => "Fly i lufta over Oslo, med kallesignal, høyde og fart.";
    public string Ikon => "✈️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await HentFly(stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }

    private async Task<IReadOnlyList<JsonElement>> HentFly(CancellationToken stopp)
    {
        if (mellomlager.TryGetValue(MellomlagerNøkkel, out IReadOnlyList<JsonElement>? lagret) && lagret is not null)
        {
            return lagret;
        }

        using var http = httpFactory.CreateClient(KlientNavn);
        using var svar = await http.GetAsync(ByggUrl(), stopp);
        svar.EnsureSuccessStatusCode();

        var tekst = await svar.Content.ReadAsStringAsync(stopp);
        var rot = JsonSerializer.Deserialize<JsonElement>(tekst);

        var rader = rot.TryGetProperty("ac", out var fly) && fly.ValueKind == JsonValueKind.Array
            ? fly.EnumerateArray().Select(f => f.Clone()).ToList()
            : [];

        mellomlager.Set(MellomlagerNøkkel, (IReadOnlyList<JsonElement>)rader, Levetid);
        return rader;
    }

    /// <summary>Adressen til airplanes.live sitt punkt-og-radius-endepunkt, med tall på engelsk format.</summary>
    public static string ByggUrl() => string.Format(
        CultureInfo.InvariantCulture,
        "https://api.airplanes.live/v2/point/{0}/{1}/{2}",
        Geo.OsloLat,
        Geo.OsloLon,
        RadiusNautiskeMil);

    /// <summary>Oversetter én rad fra «ac»-lista til et kartpunkt, eller null uten kjent posisjon.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var latFelt) || !rad.TryGetProperty("lon", out var lonFelt))
        {
            return null;
        }

        var hex = rad.GetProperty("hex").GetString() ?? "ukjent";
        var kallesignal = rad.TryGetProperty("flight", out var f) ? f.GetString()?.Trim() : null;
        var navn = string.IsNullOrEmpty(kallesignal) ? hex.ToUpperInvariant() : kallesignal;

        var (høyde, påBakken) = TolkHøyde(rad);
        var fart = rad.TryGetProperty("gs", out var g) && g.ValueKind == JsonValueKind.Number
            ? $"{g.GetDouble().ToString("0", CultureInfo.InvariantCulture)} knop"
            : null;

        return Geo.Lag(
            id: hex,
            lat: latFelt.GetDouble(),
            lon: lonFelt.GetDouble(),
            navn: navn,
            kilde: Kilde,
            detaljer: new Dictionary<string, object?>
            {
                ["kallesignal"] = navn,
                ["høyde"] = høyde,
                ["fart"] = fart,
                ["status"] = påBakken ? "på bakken" : "i lufta",
            });
    }

    private static (string? Høyde, bool PåBakken) TolkHøyde(JsonElement rad)
    {
        if (!rad.TryGetProperty("alt_baro", out var alt))
        {
            return (null, false);
        }

        if (alt.ValueKind == JsonValueKind.String && alt.GetString() == "ground")
        {
            return (null, true);
        }

        return alt.ValueKind == JsonValueKind.Number
            ? ($"{alt.GetDouble().ToString("N0", CultureInfo.InvariantCulture)} fot", false)
            : (null, false);
    }
}
