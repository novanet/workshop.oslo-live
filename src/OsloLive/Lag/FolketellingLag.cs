using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Eiendommer fra folketellingen 1910, fra Arkivverkets Digitalarkivet
/// (kilde «arkivverket», operasjon «search_census_properties»). Et søk med
/// koordinat gir i dag ingen treff i sentrum, så vi søker på stedsnavnet
/// «Kristiania» i stedet, som gir gårder og gateadresser i det gamle Kristiania.
/// </summary>
public sealed class FolketellingLag(Allemannsdata data) : ILag
{
    public string Id => "folketelling";
    public string Navn => "Kristiania 1910";
    public string Beskrivelse => "Eiendommer fra folketellingen 1910, med gårdsnavn eller gateadresse der de lå.";
    public string Ikon => "🏚️";

    /// <summary>Søkeordet som gir treff i det gamle Kristiania, se klassekommentaren.</summary>
    public const string Søkeord = "Kristiania";

    /// <summary>Antall eiendommer vi ber om per oppdateringer.</summary>
    public const int AntallEiendommer = 50;

    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["query"] = Søkeord,
        ["limit"] = AntallEiendommer,
    };

    /// <summary>Én rad blir ett punkt. Mangler raden id eller koordinater, blir det ikke noe punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("property_id", out var idEl) || idEl.ValueKind != JsonValueKind.String
            || idEl.GetString() is not { Length: > 0 } id)
        {
            return null;
        }

        if (!rad.TryGetProperty("coordinates", out var koordinater) || koordinater.ValueKind != JsonValueKind.Object
            || !koordinater.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !koordinater.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var navn = rad.TryGetProperty("gaardsnavn_gateadr", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!.Trim()
            : "";
        if (navn.Length == 0)
        {
            navn = "Eiendom";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("gaardsnr", out var gnr) && gnr.ValueKind == JsonValueKind.String
            && gnr.GetString() is { Length: > 0 } gnrVerdi)
        {
            detaljer["gårdsnr"] = gnrVerdi;
        }

        if (rad.TryGetProperty("bruksnr", out var bnr) && bnr.ValueKind == JsonValueKind.String
            && bnr.GetString() is { Length: > 0 } bnrVerdi)
        {
            detaljer["bruksnr"] = bnrVerdi;
        }

        if (rad.TryGetProperty("source", out var kildetekst) && kildetekst.ValueKind == JsonValueKind.String
            && kildetekst.GetString() is { Length: > 0 } tellingVerdi)
        {
            detaljer["telling"] = tellingVerdi;
        }

        return Geo.Lag(
            id: id,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "Digitalarkivet, Arkivverket",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: arkivverket, operasjon: search_census_properties. «data» er et objekt med listen i «properties».
        var rader = await data.HentListe("arkivverket", "search_census_properties", Parametre, liste: "properties", stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
