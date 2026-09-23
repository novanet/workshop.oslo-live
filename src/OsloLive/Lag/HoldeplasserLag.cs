using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Holdeplasser og stasjoner for kollektivtrafikk i Oslo, fra Enturs nasjonale
/// stoppestedsregister. Kilden har ikke noe geografisk søk (verken punkt+radius
/// eller kartutsnitt), bare fritekstsøk på navn, så vi søker etter kjente
/// knutepunkt spredt over hele Oslo og lar <see cref="Geo.Lag"/> luke bort alt
/// utenfor kartutsnittet, akkurat som treff fra andre kommuner med samme navn.
/// </summary>
public sealed class HoldeplasserLag(Allemannsdata data) : ILag
{
    public string Id => "holdeplasser";
    public string Navn => "Holdeplasser";
    public string Beskrivelse => "Holdeplasser og stasjoner for buss, trikk, T-bane, tog og båt i Oslo.";
    public string Ikon => "🚏";

    /// <summary>Tak per søk, slik at ett søkeord aldri gir en uforholdsmessig stor treffliste.</summary>
    public const int MaksPerSøk = 10;

    /// <summary>
    /// Kjente knutepunkt spredt over Oslo (sentrum, vest, nord, øst, sør og havna),
    /// valgt for å dekke buss, trikk, T-bane, tog og båt. Ikke en fullstendig liste
    /// over alle holdeplasser i byen, bare et representativt utvalg av kilden vi har tilgang til.
    /// </summary>
    private static readonly string[] Søkeord =
    [
        "Oslo S",
        "Nationaltheatret",
        "Jernbanetorget",
        "Majorstuen",
        "Grønland",
        "Storo",
        "Aker brygge",
        "Grorud",
        "Mortensrud",
        "Skøyen",
        "Frognerseteren",
        "Vippetangen",
    ];

    /// <summary>
    /// Oversetter Enturs stoppestedskategorier (NeTEx) til norske transportnavn.
    /// Rekkefølgen er også visningsrekkefølgen når en holdeplass har flere.
    /// </summary>
    private static readonly (string Kategori, string Navn)[] Transportkategorier =
    [
        ("onstreetBus", "buss"),
        ("busStation", "buss"),
        ("coachStation", "buss"),
        ("onstreetTram", "trikk"),
        ("tramStation", "trikk"),
        ("metroStation", "T-bane"),
        ("railStation", "tog"),
        ("harbourPort", "båt"),
        ("ferryStop", "båt"),
    ];

    /// <summary>Slår sammen kategoriene til en tekst som «buss, trikk», eller «ukjent» uten treff.</summary>
    public static string Transport(IEnumerable<string> kategorier)
    {
        var sett = kategorier.ToHashSet();
        var navn = Transportkategorier
            .Where(t => sett.Contains(t.Kategori))
            .Select(t => t.Navn)
            .Distinct()
            .ToList();

        return navn.Count > 0 ? string.Join(", ", navn) : "ukjent";
    }

    /// <summary>Én rad fra <c>search_stops</c> blir ett punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        var id = rad.GetProperty("id").GetString();
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var navn = rad.TryGetProperty("name", out var n) ? n.GetString() : null;
        var kategorier = rad.TryGetProperty("category", out var k) && k.ValueKind == JsonValueKind.Array
            ? k.EnumerateArray().Select(c => c.GetString()).OfType<string>()
            : [];

        return Geo.Lag(
            id: id,
            lat: rad.GetProperty("lat").GetDouble(),
            lon: rad.GetProperty("lon").GetDouble(),
            navn: navn ?? "Ukjent holdeplass",
            kilde: "Entur",
            detaljer: new Dictionary<string, object?> { ["transport"] = Transport(kategorier) });
    }

    public static Kartlag Samle(IEnumerable<JsonElement> rader) =>
        Geo.Samle(rader.Select(TilPunkt));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var svar = await Task.WhenAll(Søkeord.Select(søk => data.HentListe(
            "entur",
            "search_stops",
            new Dictionary<string, object> { ["query"] = søk, ["limit"] = MaksPerSøk },
            liste: "stops",
            stopp)));

        return Samle(svar.SelectMany(r => r));
    }
}
