using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Kaier, ferjekaier og havneanlegg i og rundt Oslo havn, fra Kystverkets
/// Kystdatahuset (kilde «kystdatahuset», operasjon «find_ports_nearby»).
/// Id er «port_id», ikke «kode»: «kode» er havnens LOCODE (for eksempel
/// NOOSL) og er lik for nesten alle kaiene, så Geo.Samle ville slått dem
/// sammen til noen få punkter.
/// </summary>
public sealed class KaierLag(Allemannsdata data) : ILag
{
    public string Id => "kaier";
    public string Navn => "Kaier";
    public string Beskrivelse => "Kaier og ferjekaier i Oslo havn, med hva slags anlegg hver av dem er.";
    public string Ikon => "⚓";

    /// <summary>Radius rundt Oslo sentrum. Gir under 90 rader, altså én side.</summary>
    public const double RadiusKm = 20;

    /// <summary>Kildens største side; større verdier kuttes til 100 av kilden.</summary>
    public const int MaksKaier = 100;

    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
        ["radius_km"] = RadiusKm,
        ["limit"] = MaksKaier,
    };

    /// <summary>Én rad med en kai blir ett punkt. Mangler posisjon eller port_id, blir det ikke noe punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        string? id = null;
        if (rad.TryGetProperty("port_id", out var idEl))
        {
            if (idEl.ValueKind == JsonValueKind.Number)
            {
                id = idEl.GetInt64().ToString(CultureInfo.InvariantCulture);
            }
            else if (idEl.ValueKind == JsonValueKind.String && idEl.GetString() is { Length: > 0 } s)
            {
                id = s;
            }
        }

        if (id is null)
        {
            return null;
        }

        var navn = rad.TryGetProperty("navn", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!.Trim()
            : "";
        if (navn.Length == 0)
        {
            navn = "Kai";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
        {
            var type = t.GetString()!.Trim();
            if (type.Length > 0)
            {
                detaljer["type"] = type;
            }
        }

        return Geo.Lag(
            id: id,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "Kystverket Kystdatahuset",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: kystdatahuset, operasjon: find_ports_nearby. «data» er et objekt med listen i «havner».
        var rader = await data.HentListe("kystdatahuset", "find_ports_nearby", Parametre, liste: "havner", stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
