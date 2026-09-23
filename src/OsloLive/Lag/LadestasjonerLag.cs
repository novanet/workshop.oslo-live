using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Offentlige ladestasjoner for elbil i Oslo, fra Enovas NOBIL (kilde «nobil»,
/// operasjon «find_charging_stations»). «data» er en liste rett ut.
/// Kilden svarer av og til 502. Da kaster Hent, og /api/lag/{id} gir 502 med
/// { feil } for dette laget alene; laget blir rødt, resten av kartet virker.
/// </summary>
public sealed class LadestasjonerLag(Allemannsdata data) : ILag
{
    public string Id => "ladestasjoner";
    public string Navn => "Ladestasjoner";
    public string Beskrivelse => "Offentlige ladestasjoner for elbil i Oslo, med antall ladepunkter.";
    public string Ikon => "🔌";

    /// <summary>Radius rundt Oslo sentrum.</summary>
    public const double RadiusKm = 15;

    /// <summary>Kildens største side.</summary>
    public const int MaksStasjoner = 100;

    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
        ["radius_km"] = RadiusKm,
        ["limit"] = MaksStasjoner,
    };

    /// <summary>Én ladestasjon blir ett punkt. Mangler posisjon eller id, blir det ikke noe punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (!rad.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String
            || idEl.GetString() is not { Length: > 0 } id)
        {
            return null;
        }

        var navn = rad.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!.Trim()
            : "";
        if (navn.Length == 0)
        {
            navn = "Ukjent ladestasjon";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("charging_points", out var p) && p.ValueKind == JsonValueKind.Number)
        {
            detaljer["ladepunkter"] = p.GetInt32();
        }

        return Geo.Lag(
            id: id,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "NOBIL, Enova",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: nobil, operasjon: find_charging_stations. «data» er en liste rett ut.
        var rader = await data.HentListe("nobil", "find_charging_stations", Parametre, liste: null, stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
