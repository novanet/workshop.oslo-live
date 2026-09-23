using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Skipstrafikk i indre Oslofjord: ferger, lasteskip og fritidsbåter, med
/// posisjon fra AIS via BarentsWatch. Kilden gir fart allerede i knop.
/// </summary>
public sealed class SkipLag(Allemannsdata data) : ILag
{
    public string Id => "skip";
    public string Navn => "Skipstrafikk";
    public string Beskrivelse => "Ferger, lasteskip og fritidsbåter i indre Oslofjord, med posisjon fra AIS.";
    public string Ikon => "🚢";

    /// <summary>Radius rundt Oslo sentrum som dekker fjorden ned til Nesodden.</summary>
    public const double RadiusKm = 20;

    /// <summary>Tak på antall fartøy, romslig nok til å ikke kutte Nesoddbåtene.</summary>
    public const int MaksFartøy = 200;

    /// <summary>
    /// Kurs over grunn i grader, 0-359, der 0 er nord. Feltet «kurs» fra
    /// AIS-kilden (describe_operation for ais/find_vessels_nearby) er det
    /// eneste retningsfeltet operasjonen oppgir; den har ingen egen
    /// heading-verdi å falle tilbake på. AIS bruker 360 som «ukjent kurs»,
    /// så verdier utenfor [0, 360) forkastes, i tillegg til manglende felt.
    /// </summary>
    public static int? UtledKurs(JsonElement rad)
    {
        if (!rad.TryGetProperty("kurs", out var k) || k.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var verdi = k.GetDouble();
        if (verdi < 0 || verdi >= 360)
        {
            return null;
        }

        return (int)Math.Round(verdi) % 360;
    }

    /// <summary>Ett fartøy med kjent posisjon blir ett punkt. Ukjent posisjon gir ikke punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var id = rad.TryGetProperty("vessel_id", out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64().ToString()
            : null;

        var navn = rad.TryGetProperty("navn", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!.Trim()
            : "";
        if (navn.Length == 0)
        {
            navn = "Ukjent fartøy";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("fart_knop", out var fart) && fart.ValueKind == JsonValueKind.Number)
        {
            detaljer["fart"] = Math.Round(fart.GetDouble(), 1);
        }

        if (rad.TryGetProperty("destinasjon", out var d) && d.ValueKind == JsonValueKind.String)
        {
            var destinasjon = d.GetString()!.Trim();
            if (destinasjon.Length > 0)
            {
                detaljer["destinasjon"] = destinasjon;
            }
        }

        var kurs = UtledKurs(rad);
        if (kurs is not null)
        {
            detaljer["kurs"] = kurs;
        }

        return Geo.Lag(
            id: id ?? navn,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "BarentsWatch AIS",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe(
            "ais",
            "find_vessels_nearby",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["limit"] = MaksFartøy,
            },
            liste: "fartoy",
            stopp);

        return Geo.Samle(rader.Select(TilPunkt));
    }
}
