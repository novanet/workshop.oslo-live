using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Sykkelparkeringer i Oslo sentrum, fra OpenStreetMap. Kilde: poi_norge,
/// operasjon: search_poi. Kategorien «bicycle_parking» kommer fra
/// list_categories(type: "amenity"), som listet den med 5403 punkter i hele
/// snapshotet.
/// </summary>
public sealed class SykkelparkeringLag(Allemannsdata data) : ILag
{
    public string Id => "sykkelparkering";
    public string Navn => "Sykkelparkering";
    public string Beskrivelse => "Sykkelparkeringer i Oslo sentrum fra OpenStreetMap.";
    public string Ikon => "🚲";

    /// <summary>Radius rundt Oslo sentrum vi henter sykkelparkeringer innenfor.</summary>
    public const double RadiusKm = 2;

    /// <summary>Kildens største side; større verdier kuttes til 200 av kilden.</summary>
    public const int Maks = 200;

    /// <summary>Kategorien fra poi_norge list_categories(type: "amenity").</summary>
    public const string Kategori = "bicycle_parking";

    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
        ["radius_km"] = RadiusKm,
        ["category"] = Kategori,
        ["limit"] = Maks,
    };

    /// <summary>
    /// Én rad blir ett punkt. Mangler posisjon eller id, blir det ikke noe punkt.
    /// «capacity» og «covered» finnes ikke i search_poi sitt svar i dag (bekreftet mot
    /// kilden: items har bare id, name, type, category, lat, lon, distance_km), men leses
    /// her i tilfelle kilden begynner å levere dem, slik akseptansekriteriene krever.
    /// </summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("id", out var idEl) || idEl.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var id = idEl.ValueKind == JsonValueKind.Number
            ? idEl.GetInt64().ToString(CultureInfo.InvariantCulture)
            : idEl.GetString();
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var navn = rad.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()
            : null;
        if (string.IsNullOrEmpty(navn))
        {
            navn = "Sykkelparkering";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("capacity", out var kapasitet))
        {
            if (kapasitet.ValueKind == JsonValueKind.Number && kapasitet.TryGetInt32(out var antall))
            {
                detaljer["plasser"] = antall;
            }
            else if (kapasitet.ValueKind == JsonValueKind.String
                && int.TryParse(kapasitet.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tallFraTekst))
            {
                detaljer["plasser"] = tallFraTekst;
            }
        }

        if (rad.TryGetProperty("covered", out var tak) && tak.ValueKind == JsonValueKind.String)
        {
            var underTak = tak.GetString() switch
            {
                "yes" => "ja",
                "no" => "nei",
                _ => null,
            };
            if (underTak is not null)
            {
                detaljer["under tak"] = underTak;
            }
        }

        return Geo.Lag(
            id: id,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "OpenStreetMap",
            detaljer: detaljer);
    }

    public static Kartlag Samle(IEnumerable<JsonElement> rader) =>
        Geo.Samle(rader.Select(TilPunkt));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe("poi_norge", "search_poi", Parametre, liste: "items", stopp);
        return Samle(rader);
    }
}
