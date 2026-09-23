using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Målestasjonene til Meteorologisk institutt i Oslo, fra Frost
/// (kilde «frost», operasjon «find_stations»).
/// </summary>
public sealed class VaerstasjonerLag(Allemannsdata data) : ILag
{
    public string Id => "vaerstasjoner";
    public string Navn => "Værstasjoner";
    public string Beskrivelse => "Målestasjonene til Meteorologisk institutt i Oslo, med høyde over havet og hvor lenge de har målt.";
    public string Ikon => "🌡️";

    /// <summary>Kildens tak per kall; det holder rikelig for stasjonene i Oslo kommune.</summary>
    public const int MaksStasjoner = 100;

    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["municipality"] = "Oslo",
        ["limit"] = MaksStasjoner,
    };

    /// <summary>Én stasjonsrad blir ett punkt. Mangler posisjon eller id, blir det ikke noe punkt.</summary>
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

        var navn = rad.TryGetProperty("short_name", out var kortNavn)
                && kortNavn.ValueKind == JsonValueKind.String
                && kortNavn.GetString()?.Trim() is { Length: > 0 } kort
            ? kort
            : rad.TryGetProperty("name", out var langtNavn) && langtNavn.ValueKind == JsonValueKind.String
                ? langtNavn.GetString()!.Trim()
                : "Ukjent stasjon";

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("masl", out var m) && m.ValueKind == JsonValueKind.Number)
        {
            detaljer["moh"] = (int)Math.Round(m.GetDouble());
        }

        if (rad.TryGetProperty("valid_from", out var validFra) && validFra.ValueKind == JsonValueKind.String)
        {
            detaljer["i drift siden"] = validFra.GetString();
        }

        return Geo.Lag(
            id: idEl.GetString()!,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "Meteorologisk institutt (Frost)",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: frost, operasjon: find_stations. «data» er en liste rett ut.
        // Avgrenses til Oslo kommune med parameteren «municipality».
        var rader = await data.HentListe("frost", "find_stations", Parametre, liste: null, stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
