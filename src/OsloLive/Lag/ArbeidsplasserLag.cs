using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Virksomheter med mange ansatte i og rundt Oslo sentrum, fra Firmafakta.
/// Kilden filtrerer på minste antall ansatte og radius i selve kallet, så vi
/// henter aldri flere selskaper enn dem som faktisk er store arbeidsplasser.
/// </summary>
public sealed class ArbeidsplasserLag(Allemannsdata data) : ILag
{
    public string Id => "arbeidsplasser";
    public string Navn => "Store arbeidsplasser";
    public string Beskrivelse => "Virksomheter med minst 500 ansatte innenfor 5 km fra Oslo sentrum.";
    public string Ikon => "🏢";

    /// <summary>Radius rundt Oslo sentrum vi henter virksomheter innenfor.</summary>
    public const double RadiusKm = 5;

    /// <summary>Bare virksomheter med minst så mange ansatte regnes som en stor arbeidsplass.</summary>
    public const int MinsteAntallAnsatte = 500;

    /// <summary>Tak på antall treff, slik at kilden ikke kutter svaret over 64 KB.</summary>
    public const int Maks = 100;

    /// <summary>Ett selskap blir ett punkt. Selskaper uten koordinater gir ikke punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        var orgnr = rad.TryGetProperty("organization_id", out var o) ? o.GetString() : null;
        if (string.IsNullOrWhiteSpace(orgnr))
        {
            return null;
        }

        if (!rad.TryGetProperty("coordinates", out var koordinater) || koordinater.ValueKind != JsonValueKind.Object
            || !koordinater.TryGetProperty("latitude", out var lat) || lat.ValueKind != JsonValueKind.Number
            || !koordinater.TryGetProperty("longitude", out var lon) || lon.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var navn = rad.TryGetProperty("navn", out var n) ? n.GetString() : null;

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("antallAnsatte", out var ansatte) && ansatte.ValueKind == JsonValueKind.Number)
        {
            detaljer["ansatte"] = ansatte.GetInt32();
        }

        if (rad.TryGetProperty("naeringskode1", out var næring) && næring.ValueKind == JsonValueKind.Object
            && næring.TryGetProperty("beskrivelse", out var bransje) && bransje.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(bransje.GetString()))
        {
            detaljer["bransje"] = bransje.GetString();
        }

        return Geo.Lag(
            id: orgnr,
            lat: lat.GetDouble(),
            lon: lon.GetDouble(),
            navn: navn ?? "Ukjent virksomhet",
            kilde: "Firmafakta",
            detaljer: detaljer);
    }

    public static Kartlag Samle(IEnumerable<JsonElement> rader) =>
        Geo.Samle(rader.Select(TilPunkt));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: firmafakta, operasjon: finn_selskaper_i_omrade.
        var rader = await data.HentListe(
            "firmafakta",
            "finn_selskaper_i_omrade",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["min_employees"] = MinsteAntallAnsatte,
                ["limit"] = Maks,
            },
            liste: "companies",
            stopp);

        return Samle(rader);
    }
}
