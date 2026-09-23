namespace OsloLive.Kart;

/// <summary>Ett punkt på kartet, slik GeoJSON vil ha det.</summary>
public sealed record Geometri(string Type, double[] Coordinates);

/// <summary>En ting som tegnes på kartet: et punkt pluss det vi vet om det.</summary>
public sealed record Kartpunkt(string Type, Geometri Geometry, Dictionary<string, object?> Properties);

/// <summary>Svaret et lag gir tilbake. Dette er formatet Leaflet leser.</summary>
public sealed record Kartlag(string Type, IReadOnlyList<Kartpunkt> Features);

public static class Geo
{
    /// <summary>Oslo og indre Oslofjord. Alt utenfor denne boksen hører ikke hjemme på kartet.</summary>
    public const double MinLat = 59.80;
    public const double MaksLat = 60.14;
    public const double MinLon = 10.45;
    public const double MaksLon = 10.98;

    /// <summary>Midt i Oslo. Brukes som utgangspunkt når et lag spør etter data «i nærheten».</summary>
    public const double OsloLat = 59.9139;
    public const double OsloLon = 10.7522;

    /// <summary>Punktet slik GeoJSON skal ha det.</summary>
    public static Geometri Punkt(double lat, double lon) =>
        new("Point", [lat, lon]);

    /// <summary>Ligger punktet innenfor kartutsnittet vårt?</summary>
    public static bool IOslo(double lat, double lon) =>
        (lat >= MinLat && lat <= MaksLat) && (lon >= MinLon && lon <= MaksLon);

    /// <summary>
    /// Lager ett kartpunkt, eller null hvis punktet ligger utenfor Oslo.
    /// Alle lag skal gå via denne, slik at alle punkter får de samme
    /// egenskapene: id, navn og kilde er alltid med.
    /// </summary>
    public static Kartpunkt? Lag(
        string id,
        double lat,
        double lon,
        string navn,
        string kilde,
        Dictionary<string, object?>? detaljer = null)
    {
        if (!IOslo(lat, lon))
        {
            return null;
        }

        var egenskaper = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["navn"] = navn,
            ["kilde"] = kilde,
        };

        if (detaljer is not null)
        {
            foreach (var (nøkkel, verdi) in detaljer)
            {
                egenskaper[nøkkel] = verdi;
            }
        }

        return new Kartpunkt("Feature", Punkt(lat, lon), egenskaper);
    }

    /// <summary>
    /// Samler punktene til et ferdig lag. Punkter utenfor Oslo er allerede
    /// silt bort av <see cref="Lag"/>; her fjerner vi duplikater, altså
    /// punkter med samme id.
    /// </summary>
    public static Kartlag Samle(IEnumerable<Kartpunkt?> punkter)
    {
        var rene = punkter
            .Where(p => p is not null)
            .Select(p => p!)
            .DistinctBy(p => p.Properties["id"])
            .ToList();

        return new Kartlag("FeatureCollection", rene);
    }
}
