using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Busser, trikker, T-bane og tog i Oslo som rapporterer posisjon akkurat nå.
/// Kilde: Entur, som samler sanntidsposisjoner (SIRI-VM) for hele landet.
/// </summary>
public sealed class KollektivLag(Allemannsdata data) : ILag
{
    public string Id => "kollektiv";
    public string Navn => "Kollektiv";
    public string Beskrivelse => "Busser, trikker, T-bane og tog i Oslo som rapporterer posisjon akkurat nå.";
    public string Ikon => "🚌";

    /// <summary>Radius rundt Oslo sentrum vi henter kjøretøy innenfor.</summary>
    public const double RadiusKm = 20;

    /// <summary>Tak per side. Kilden kutter svar over 64 KB.</summary>
    public const int MaksPerSide = 50;

    /// <summary>Maks antall sider per modus, som en sikkerhet mot uendelig paginering.</summary>
    public const int MaksSider = 10;

    private static readonly string[] Moduser = ["BUS", "TRAM", "METRO", "RAIL"];

    /// <summary>Oversetter kildens modus til teksten popupen skal vise.</summary>
    public static string Type(string? modus) => modus switch
    {
        "BUS" or "COACH" => "buss",
        "TRAM" => "trikk",
        "METRO" => "T-bane",
        "RAIL" => "tog",
        _ => "annet",
    };

    /// <summary>Ikon per type, slik at buss og trikk skiller seg fra hverandre i popupen.</summary>
    public static string IkonFor(string type) => type switch
    {
        "buss" => "🚌",
        "trikk" => "🚊",
        "T-bane" => "🚇",
        "tog" => "🚆",
        _ => "🚏",
    };

    /// <summary>Tidspunktet kjøretøyet siste gang rapporterte posisjon, eller det eldst mulige hvis feltet mangler.</summary>
    private static DateTimeOffset Tidspunkt(JsonElement rad) =>
        rad.TryGetProperty("last_updated", out var felt)
        && felt.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(felt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var tid)
            ? tid
            : DateTimeOffset.MinValue;

    /// <summary>
    /// Oversetter én rad til et kartpunkt. Rader uten kjent id eller posisjon
    /// gir null, slik at én dårlig rad fra kilden aldri feller hele laget.
    /// </summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (rad.ValueKind != JsonValueKind.Object
            || !rad.TryGetProperty("vehicle_id", out var idFelt) || idFelt.ValueKind != JsonValueKind.String
            || !rad.TryGetProperty("lat", out var latFelt) || latFelt.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonFelt) || lonFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var modus = rad.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        var type = Type(modus);

        var linjenummer = rad.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
        var rutenavn = rad.TryGetProperty("line_name", out var ln) && ln.ValueKind == JsonValueKind.String ? ln.GetString() : null;
        var linje = linjenummer ?? rutenavn ?? "ukjent";

        var destinasjon = rad.TryGetProperty("destination", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;
        destinasjon ??= rutenavn ?? "ukjent";

        return Geo.Lag(
            id: idFelt.GetString()!,
            lat: latFelt.GetDouble(),
            lon: lonFelt.GetDouble(),
            navn: $"{linje} til {destinasjon}",
            kilde: "Entur sanntidsposisjoner",
            detaljer: new Dictionary<string, object?>
            {
                ["linje"] = linje,
                ["type"] = type,
                ["destinasjon"] = destinasjon,
                ["ikon"] = IkonFor(type),
            });
    }

    /// <summary>
    /// Samler radene til et ferdig lag. Rekkefølgen er nyeste rapportering
    /// først, slik at <see cref="Geo.Samle"/> sin fjerning av duplikater på id
    /// beholder den siste kjente posisjonen når samme kjøretøy dukker opp flere ganger.
    /// </summary>
    public static Kartlag Samle(IEnumerable<JsonElement> rader) =>
        Geo.Samle(rader.OrderByDescending(Tidspunkt).Select(TilPunkt));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var alleRader = new List<JsonElement>();

        await Task.WhenAll(Moduser.Select(async modus =>
        {
            var rader = await HentModus(modus, stopp);
            lock (alleRader)
            {
                alleRader.AddRange(rader);
            }
        }));

        return Samle(alleRader);
    }

    /// <summary>Henter alle sidene for én modus (buss, trikk, T-bane eller tog).</summary>
    private async Task<List<JsonElement>> HentModus(string modus, CancellationToken stopp)
    {
        var rader = new List<JsonElement>();

        for (var side = 0; side < MaksSider; side++)
        {
            var parametre = new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["mode"] = modus,
                ["limit"] = MaksPerSide,
                ["offset"] = side * MaksPerSide,
            };

            var svar = await data.Hent("entur", "find_live_vehicles_nearby", parametre, stopp);
            var kjøretøy = svar.TryGetProperty("vehicles", out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().ToList()
                : [];

            rader.AddRange(kjøretøy);

            var flereSider = svar.TryGetProperty("has_more_results", out var flere) && flere.ValueKind == JsonValueKind.True;
            if (!flereSider || kjøretøy.Count == 0)
            {
                break;
            }
        }

        return rader;
    }
}
