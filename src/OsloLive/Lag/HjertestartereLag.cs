using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Hjertestartere i Oslo, registrert i OpenStreetMap og hentet fra Overpass API.
/// Allemannsdata har ingen kilde for hjertestartere, så laget bruker, som
/// <see cref="FlyLag"/>, sin egen navngitte <see cref="HttpClient"/> fra
/// <see cref="IHttpClientFactory"/> og eget mellomlager i delt <see cref="IMemoryCache"/>.
/// Overpass er en delt, gratis tjeneste som svarer 429 ved for mange kall, så selve
/// hentingen (også en som feiler) ligger i mellomlageret i én time: appen gjør maks
/// ett kall i timen. Hentingen er ikke bundet til den som spør, slik at helsesjekkens
/// tidsavbrudd ikke avbryter kallet og utløser et nytt. Svikter kilden, kaster
/// <see cref="Hent"/>, og bare dette laget blir rødt.
/// </summary>
public sealed class HjertestartereLag(IHttpClientFactory httpFactory, IMemoryCache mellomlager) : ILag
{
    public const string KlientNavn = "hjertestartere";
    public const string Url = "https://overpass-api.de/api/interpreter";
    private const string MellomlagerNøkkel = "hjertestartere-mellomlager";
    private const string Kilde = "OpenStreetMap";
    private static readonly TimeSpan Levetid = TimeSpan.FromHours(1);

    public string Id => "hjertestartere";
    public string Navn => "Hjertestartere";
    public string Beskrivelse => "Hjertestartere i Oslo fra OpenStreetMap, med åpningstid og plassering der det er kjent.";
    public string Ikon => "❤️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var noder = await HentNoder().WaitAsync(stopp);
        return Geo.Samle(noder.Select(TilPunkt));
    }

    private Task<IReadOnlyList<JsonElement>> HentNoder() =>
        mellomlager.GetOrCreate(MellomlagerNøkkel, oppføring =>
        {
            oppføring.AbsoluteExpirationRelativeToNow = Levetid;
            return HentFraOverpass();
        })!;

    private async Task<IReadOnlyList<JsonElement>> HentFraOverpass()
    {
        using var http = httpFactory.CreateClient(KlientNavn);
        using var skjema = new FormUrlEncodedContent([new("data", Spørring())]);
        using var svar = await http.PostAsync(Url, skjema);
        svar.EnsureSuccessStatusCode();

        var tekst = await svar.Content.ReadAsStringAsync();
        var rot = JsonSerializer.Deserialize<JsonElement>(tekst);

        return rot.ValueKind == JsonValueKind.Object
            && rot.TryGetProperty("elements", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(n => n.Clone()).ToList()
            : [];
    }

    /// <summary>Overpass-spørringen etter hjertestartere i kartutsnittet, med tall på engelsk format.</summary>
    public static string Spørring() => string.Format(
        CultureInfo.InvariantCulture,
        "[out:json][timeout:25];node[\"emergency\"=\"defibrillator\"]({0},{1},{2},{3});out;",
        Geo.MinLat,
        Geo.MinLon,
        Geo.MaksLat,
        Geo.MaksLon);

    /// <summary>
    /// Oversetter én node fra «elements» til et kartpunkt. Noder uten tallfestet id eller
    /// posisjon gir null, slik at én dårlig node aldri feller laget. Mangler åpningstid
    /// eller plassering, utelates feltet.
    /// </summary>
    public static Kartpunkt? TilPunkt(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("id", out var idFelt) || idFelt.ValueKind != JsonValueKind.Number || !idFelt.TryGetInt64(out var id)
            || !node.TryGetProperty("lat", out var latFelt) || latFelt.ValueKind != JsonValueKind.Number
            || !node.TryGetProperty("lon", out var lonFelt) || lonFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var tagger = node.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Object ? t : default;
        var detaljer = new Dictionary<string, object?>();
        if (Tagg(tagger, "opening_hours") is { } åpent)
        {
            detaljer["åpent"] = åpent;
        }

        if (Tagg(tagger, "defibrillator:location") is { } plassering)
        {
            detaljer["plassering"] = plassering;
        }

        return Geo.Lag(
            id: id.ToString(CultureInfo.InvariantCulture),
            lat: latFelt.GetDouble(),
            lon: lonFelt.GetDouble(),
            navn: Tagg(tagger, "name") ?? "Hjertestarter",
            kilde: Kilde,
            detaljer: detaljer);
    }

    private static string? Tagg(JsonElement tagger, string navn) =>
        tagger.ValueKind == JsonValueKind.Object
        && tagger.TryGetProperty(navn, out var verdi)
        && verdi.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(verdi.GetString())
            ? verdi.GetString()
            : null;
}
