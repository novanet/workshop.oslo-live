using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Trafikkregistreringspunktene til Statens vegvesen i Oslo
/// (kilde «vegvesen», operasjon «find_traffic_points»).
/// </summary>
public sealed class TrafikkLag(Allemannsdata data) : ILag
{
    public string Id => "trafikk";
    public string Navn => "Trafikk";
    public string Beskrivelse => "Punktene der Statens vegvesen teller trafikk i Oslo.";
    public string Ikon => "🚗";

    /// <summary>Kildens tak per kall.</summary>
    public const int MaksPunkter = 100;

    /// <summary>Fylkesnummer for Oslo. Brukes i stedet for «query», som kan gi et innpakket svar.</summary>
    public const int OsloFylke = 3;

    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["county_number"] = OsloFylke,
        ["limit"] = MaksPunkter,
    };

    /// <summary>Én rad blir ett punkt. Mangler posisjon eller id, blir det ikke noe punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (!rad.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var navn = rad.TryGetProperty("name", out var navnEl)
                && navnEl.ValueKind == JsonValueKind.String
                && navnEl.GetString()?.Trim() is { Length: > 0 } trimmet
            ? trimmet
            : "Ukjent registreringspunkt";

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("road_reference", out var vei)
            && vei.ValueKind == JsonValueKind.String
            && vei.GetString() is { Length: > 0 } veiStreng)
        {
            detaljer["vei"] = veiStreng;
        }

        return Geo.Lag(
            id: idEl.GetString()!,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "Statens vegvesen",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: vegvesen, operasjon: find_traffic_points. «data» er en liste rett ut.
        var rader = await data.HentListe("vegvesen", "find_traffic_points", Parametre, liste: null, stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
