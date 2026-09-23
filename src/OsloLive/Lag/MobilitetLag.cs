using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Delt mobilitet i Oslo: ledige elsparkesykler, bysykler og delebiler akkurat nå.
/// Kilde: Entur, som samler alle operatørene (Ryde, Voi, Bolt, Hyre, Oslo Bysykkel m.fl.).
/// Popupen i frontenden viser alle egenskapene på et punkt (unntatt id, navn og kilde),
/// så «operatør» og «type» dukker opp der automatisk.
/// </summary>
public sealed class MobilitetLag(Allemannsdata data) : ILag
{
    public string Id => "mobilitet";
    public string Navn => "Delt mobilitet";
    public string Beskrivelse => "Ledige elsparkesykler, bysykler og delebiler i Oslo akkurat nå.";
    public string Ikon => "🛴";

    /// <summary>Radius rundt Oslo sentrum vi henter kjøretøy og stasjoner innenfor.</summary>
    public const int RadiusMeter = 5000;

    /// <summary>
    /// Tak per formfaktor. Kilden kutter svar over 64 KB, og stasjoner tar mest
    /// plass (~600 byte per stykk); 60 per formfaktor holder oss godt under taket.
    /// </summary>
    public const int MaksPerType = 60;

    private static readonly string[] Formfaktorer =
        ["SCOOTER_STANDING", "SCOOTER_SEATED", "BICYCLE", "CARGO_BICYCLE", "CAR"];

    /// <summary>Oversetter kildens formfaktor til teksten popupen skal vise.</summary>
    public static string Type(string? formFactor) => formFactor switch
    {
        "SCOOTER_STANDING" or "SCOOTER_SEATED" or "SCOOTER" => "elsparkesykkel",
        "BICYCLE" or "CARGO_BICYCLE" => "sykkel",
        "CAR" => "bil",
        "MOPED" => "moped",
        _ => "annet",
    };

    /// <summary>
    /// Oversetter operatørens system-id til navnet akseptansekriteriene ber om
    /// («Ryde», «Voi», «Oslo Bysykkel»), siden kilden ellers gir selskapsnavn
    /// («VOI Technology Norway AS», «UIP Bauer Media Outdoor Norge AS»).
    /// </summary>
    public static string Operatør(string? systemId, string? operatør)
    {
        if (systemId is not null)
        {
            if (systemId.StartsWith("ryde", StringComparison.OrdinalIgnoreCase)) return "Ryde";
            if (systemId.StartsWith("voi", StringComparison.OrdinalIgnoreCase)) return "Voi";
            if (systemId.StartsWith("bolt", StringComparison.OrdinalIgnoreCase)) return "Bolt";
            if (systemId.Equals("oslobysykkel", StringComparison.OrdinalIgnoreCase)) return "Oslo Bysykkel";
            if (systemId.StartsWith("hyre", StringComparison.OrdinalIgnoreCase)) return "Hyre";
            if (systemId.StartsWith("getaround", StringComparison.OrdinalIgnoreCase)) return "Getaround";
            if (systemId.StartsWith("hertz", StringComparison.OrdinalIgnoreCase)) return "Hertz Bildeling";
            if (systemId.Equals("otto", StringComparison.OrdinalIgnoreCase)) return "Otto";
            if (systemId.StartsWith("dott", StringComparison.OrdinalIgnoreCase)) return "Dott";
        }

        return operatør ?? "Ukjent operatør";
    }

    /// <summary>Ett ledig kjøretøy blir ett punkt. Reserverte eller avskrudde kjøretøy vises ikke.</summary>
    public static Kartpunkt? FraKjøretøy(JsonElement rad)
    {
        if (rad.TryGetProperty("reserved", out var reservert) && reservert.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        if (rad.TryGetProperty("disabled", out var deaktivert) && deaktivert.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        var systemId = rad.TryGetProperty("system_id", out var s) ? s.GetString() : null;
        var operatørNavn = rad.TryGetProperty("operator", out var o) ? o.GetString() : null;
        var operatør = Operatør(systemId, operatørNavn);

        var formFactor = rad.TryGetProperty("form_factor", out var ff) ? ff.GetString() : null;
        var type = Type(formFactor);

        var detaljer = new Dictionary<string, object?>
        {
            ["operatør"] = operatør,
            ["type"] = type,
        };

        if (rad.TryGetProperty("propulsion", out var drivstoff) && drivstoff.ValueKind == JsonValueKind.String)
        {
            detaljer["drivstoff"] = drivstoff.GetString();
        }

        if (rad.TryGetProperty("range_m", out var rekkevidde) && rekkevidde.ValueKind == JsonValueKind.Number)
        {
            detaljer["rekkevidde km"] = Math.Round(rekkevidde.GetDouble() / 1000, 1);
        }

        return Geo.Lag(
            id: rad.GetProperty("id").GetString() ?? "",
            lat: rad.GetProperty("lat").GetDouble(),
            lon: rad.GetProperty("lon").GetDouble(),
            navn: $"{operatør} {type}",
            kilde: "Entur delt mobilitet",
            detaljer: detaljer);
    }

    /// <summary>
    /// Oslo Bysykkel listes ikke som enkeltkjøretøy hos kilden, bare som
    /// stasjoner med antall ledige sykler. Én stasjon blir ett punkt med
    /// stasjonens id, og antallet vises som «ledige sykler» i popupen.
    /// En stasjon uten ledige sykler gir ingen punkter.
    /// </summary>
    public static IEnumerable<Kartpunkt?> FraBysykkelstasjon(JsonElement stasjon)
    {
        var systemId = stasjon.TryGetProperty("system_id", out var s) ? s.GetString() : null;
        if (!string.Equals(systemId, "oslobysykkel", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var ledige = stasjon.TryGetProperty("vehicles_available", out var v) ? v.GetInt32() : 0;
        if (ledige <= 0)
        {
            return [];
        }

        var stasjonId = stasjon.GetProperty("id").GetString() ?? "";
        var lat = stasjon.GetProperty("lat").GetDouble();
        var lon = stasjon.GetProperty("lon").GetDouble();
        var stasjonNavn = stasjon.GetProperty("name").GetString() ?? "Ukjent stasjon";

        return [Geo.Lag(
            id: stasjonId,
            lat: lat,
            lon: lon,
            navn: stasjonNavn,
            kilde: "Entur delt mobilitet",
            detaljer: new Dictionary<string, object?>
            {
                ["operatør"] = "Oslo Bysykkel",
                ["type"] = "sykkel",
                ["stasjon"] = stasjonNavn,
                ["ledige sykler"] = ledige,
            })];
    }

    public static Kartlag Samle(IEnumerable<JsonElement> kjøretøy, IEnumerable<JsonElement> stasjoner) =>
        Geo.Samle(kjøretøy.Select(FraKjøretøy).Concat(stasjoner.SelectMany(FraBysykkelstasjon)));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var alleKjøretøy = new List<JsonElement>();
        var alleStasjoner = new List<JsonElement>();

        await Task.WhenAll(Formfaktorer.Select(async formFactor =>
        {
            // Samme parametre til begge kallene, slik at Allemannsdata sitt
            // mellomlager gjenkjenner adressen og bare gjør ett faktisk kall.
            var parametre = new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_m"] = RadiusMeter,
                ["form_factor"] = formFactor,
                ["limit"] = MaksPerType,
            };

            var kjøretøy = await data.HentListe("entur", "find_shared_mobility_nearby", parametre, liste: "vehicles", stopp);
            var stasjoner = await data.HentListe("entur", "find_shared_mobility_nearby", parametre, liste: "stations", stopp);

            lock (alleKjøretøy)
            {
                alleKjøretøy.AddRange(kjøretøy);
                alleStasjoner.AddRange(stasjoner);
            }
        }));

        return Samle(alleKjøretøy, alleStasjoner);
    }
}
