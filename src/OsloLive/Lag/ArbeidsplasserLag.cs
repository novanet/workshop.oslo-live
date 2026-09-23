using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Store virksomheter i sentrum med mange ansatte, fra Firmafakta.
///
/// Kilde: firmafakta, operasjon: search_companies_nearby.
/// Parametere: lat, lon, radius_km (5), min_employees (500).
/// Koordinatene ligger i et «location»-objekt inne i hver rad.
/// </summary>
public sealed class ArbeidsplasserLag(Allemannsdata data) : ILag
{
    public string Id => "arbeidsplasser";
    public string Navn => "Store arbeidsplasser";
    public string Beskrivelse => "Virksomheter med minst 500 ansatte i og rundt Oslo sentrum.";
    public string Ikon => "🏢";

    private const string Kilde = "firmafakta";
    private const string Operasjon = "search_companies_nearby";
    private const string KildeNavn = "Firmafakta";

    private const int MinAnsatte = 500;
    private const int RadiusKm = 5;

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe(
            Kilde,
            Operasjon,
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["min_employees"] = MinAnsatte,
            },
            liste: null,
            stopp);

        return Geo.Samle(rader.Select(TilPunkt));
    }

    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        // Koordinatene ligger i et «location»-objekt inne i raden.
        if (!rad.TryGetProperty("location", out var location)
            || location.ValueKind != JsonValueKind.Object
            || !location.TryGetProperty("lat", out var latProp)
            || !location.TryGetProperty("lon", out var lonProp)
            || latProp.ValueKind != JsonValueKind.Number
            || lonProp.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var lat = latProp.GetDouble();
        var lon = lonProp.GetDouble();

        var navn = rad.TryGetProperty("name", out var n) ? n.GetString() ?? "Ukjent virksomhet" : "Ukjent virksomhet";
        var orgnr = rad.TryGetProperty("organization_number", out var o) && o.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(o.GetString())
            ? o.GetString()!
            : $"{lat:F6},{lon:F6}";

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("employees", out var ansatte) && ansatte.ValueKind == JsonValueKind.Number)
        {
            detaljer["ansatte"] = ansatte.GetInt32();
        }

        if (rad.TryGetProperty("industry_description", out var bransje) && bransje.ValueKind == JsonValueKind.String)
        {
            var bransjeVerdi = bransje.GetString();
            if (!string.IsNullOrWhiteSpace(bransjeVerdi))
            {
                detaljer["bransje"] = bransjeVerdi;
            }
        }

        return Geo.Lag(
            id: orgnr,
            lat: lat,
            lon: lon,
            navn: navn,
            kilde: KildeNavn,
            detaljer: detaljer);
    }
}
