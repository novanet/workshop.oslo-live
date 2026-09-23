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

    /// <summary>Under denne farten regnes fartøyet som stilleliggende, og kurs over grunn er støy.</summary>
    public const double FartIBevegelseKnop = 0.5;

    /// <summary>
    /// Retningen fartøyet skal vises med, i hele grader 0-359 der 0 er nord, eller null.
    ///
    /// Feltnavn fra kilden (describe_operation og svaret fra ais/find_vessels_nearby):
    /// «kurs» er kurs over grunn (COG) i grader, «fart_knop» er farten i knop, og
    /// find_vessels_nearby oppgir ingen heading; «heading» finnes i kilden bare i
    /// operasjoner per fartøy, som ville gitt ett kall per punkt, så den leses her
    /// bare hvis raden har feltet. Regel: er fartøyet i fart (fart ukjent eller
    /// minst <see cref="FartIBevegelseKnop"/> knop) og kurs over grunn er gyldig, brukes
    /// den; ellers brukes heading når den er gyldig, og mangler heading brukes kurs
    /// over grunn likevel, så feltet finnes så lenge kilden oppgir kurs eller heading.
    /// AIS bruker 360 som «ukjent kurs» og 511 som «ukjent heading»; de og alle
    /// verdier utenfor [0, 360) forkastes.
    /// </summary>
    public static int? UtledKurs(JsonElement rad)
    {
        var kurs = Grader(rad, "kurs");
        var heading = Grader(rad, "heading");
        var fart = rad.TryGetProperty("fart_knop", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetDouble() : (double?)null;
        var iFart = fart is null || fart >= FartIBevegelseKnop;

        if (iFart && kurs is not null)
        {
            return kurs;
        }

        // Stilleliggende: heading er riktig retning; mangler den, er kurs over grunn det kilden har.
        return heading ?? kurs;
    }

    /// <summary>Et gradfelt som heltall 0-359, eller null når feltet mangler, ikke er et tall eller er utenfor [0, 360).</summary>
    private static int? Grader(JsonElement rad, string felt)
    {
        if (!rad.TryGetProperty(felt, out var el) || el.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var verdi = el.GetDouble();
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
