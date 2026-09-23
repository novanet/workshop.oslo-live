using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Steder i Oslo med en artikkel på norsk Wikipedia.
///
/// Wikipedia er ikke en Allemannsdata-kilde, så laget følger mønsteret fra
/// <see cref="FlyLag"/>: eget navngitt <see cref="HttpClient"/> fra
/// <see cref="IHttpClientFactory"/> (med beskrivende User-Agent, som Wikimedia
/// krever) og mellomlager i delt <see cref="IMemoryCache"/>. Artiklene endrer
/// seg sjelden, og Wikimedia ber om at man ikke spør oftere enn nødvendig,
/// så svaret lagres i en time.
/// </summary>
public sealed class WikipediaLag(IHttpClientFactory httpFactory, IMemoryCache mellomlager) : ILag
{
    private const string KlientNavn = "wikipedia";
    private const string MellomlagerNøkkel = "wikipedia-mellomlager";
    private const string Kilde = "Wikipedia";
    private const int RadiusMeter = 10_000;
    private const int MaksTreff = 100;
    private static readonly TimeSpan Levetid = TimeSpan.FromHours(1);

    public string Id => "wikipedia";
    public string Navn => "Wikipedia";
    public string Beskrivelse => "Steder i Oslo som har en artikkel på norsk Wikipedia.";
    public string Ikon => "📖";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await HentArtikler(stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }

    private async Task<IReadOnlyList<JsonElement>> HentArtikler(CancellationToken stopp)
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

        var rader = rot.TryGetProperty("query", out var query)
            && query.ValueKind == JsonValueKind.Object
            && query.TryGetProperty("geosearch", out var treff)
            && treff.ValueKind == JsonValueKind.Array
                ? treff.EnumerateArray().Select(t => t.Clone()).ToList()
                : throw new InvalidOperationException("Svaret fra Wikipedia mangler «query.geosearch».");

        mellomlager.Set(MellomlagerNøkkel, (IReadOnlyList<JsonElement>)rader, Levetid);
        return rader;
    }

    /// <summary>Adressen til geosøket i MediaWiki-API-et til norsk Wikipedia, med tall på engelsk format.</summary>
    public static string ByggUrl() => string.Format(
        CultureInfo.InvariantCulture,
        "https://no.wikipedia.org/w/api.php?action=query&list=geosearch&gscoord={0}%7C{1}&gsradius={2}&gslimit={3}&format=json",
        Geo.OsloLat,
        Geo.OsloLon,
        RadiusMeter,
        MaksTreff);

    /// <summary>
    /// Oversetter én rad fra «query.geosearch» til et kartpunkt. Rader uten
    /// «pageid», tittel eller posisjon gir null, slik at én dårlig rad fra
    /// kilden aldri feller hele laget.
    /// </summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (rad.ValueKind != JsonValueKind.Object
            || !rad.TryGetProperty("pageid", out var pageidFelt) || pageidFelt.ValueKind != JsonValueKind.Number || !pageidFelt.TryGetInt64(out var pageid)
            || !rad.TryGetProperty("title", out var tittelFelt) || tittelFelt.ValueKind != JsonValueKind.String
            || !rad.TryGetProperty("lat", out var latFelt) || latFelt.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonFelt) || lonFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var tittel = tittelFelt.GetString();
        if (string.IsNullOrWhiteSpace(tittel))
        {
            return null;
        }

        var id = pageid.ToString(CultureInfo.InvariantCulture);

        return Geo.Lag(
            id: id,
            lat: latFelt.GetDouble(),
            lon: lonFelt.GetDouble(),
            navn: tittel,
            kilde: Kilde,
            detaljer: new Dictionary<string, object?>
            {
                ["artikkel"] = $"https://no.wikipedia.org/?curid={id}",
            });
    }
}
