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

    /// <summary>Jordens middelradius i meter. Brukes av <see cref="Avstand"/>.</summary>
    public const double JordradiusMeter = 6_371_000;

    /// <summary>Punktet slik GeoJSON skal ha det: lengdegrad først, [lon, lat].</summary>
    public static Geometri Punkt(double lat, double lon) =>
        new("Point", [lon, lat]);

    /// <summary>
    /// Avstanden i meter mellom to punkter, langs jordoverflaten (haversine).
    /// Ikke Pythagoras: en grad lengdegrad er bare halvparten så lang som en grad breddegrad i Oslo.
    /// </summary>
    public static double Avstand(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radianer(double grader) => grader * Math.PI / 180;

        var dLat = Radianer(lat2 - lat1);
        var dLon = Radianer(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Radianer(lat1)) * Math.Cos(Radianer(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * JordradiusMeter * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Ligger punktet innenfor kartutsnittet vårt?</summary>
    public static bool IOslo(double lat, double lon) =>
        (lat >= MinLat && lat <= MaksLat) && (lon >= MinLon && lon <= MaksLon);

    /// <summary>
    /// Ligger punktet innenfor polygonet? Hjørnene er en liste av (lat, lon)
    /// i den rekkefølgen de ble tegnet; polygonet trenger ikke gjentakelse av
    /// første hjørne til slutt. Et punkt nøyaktig på en kant regnes som innenfor.
    /// Ray casting etter Jordan-kurve-teoremet.
    /// </summary>
    public static bool IPolygon(double lat, double lon, IReadOnlyList<(double Lat, double Lon)> hjørner)
    {
        if (hjørner.Count < 3)
        {
            return false;
        }

        for (var i = 0; i < hjørner.Count; i++)
        {
            if (PåKant(lat, lon, hjørner[i], hjørner[(i + 1) % hjørner.Count]))
            {
                return true;
            }
        }

        var innenfor = false;
        for (int i = 0, j = hjørner.Count - 1; i < hjørner.Count; j = i++)
        {
            var (latI, lonI) = hjørner[i];
            var (latJ, lonJ) = hjørner[j];

            if ((lonI > lon) != (lonJ > lon)
                && lat < ((latJ - latI) * (lon - lonI) / (lonJ - lonI)) + latI)
            {
                innenfor = !innenfor;
            }
        }

        return innenfor;
    }

    /// <summary>Ligger punktet nøyaktig på linjestykket fra <paramref name="a"/> til <paramref name="b"/>?</summary>
    private static bool PåKant(double lat, double lon, (double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        const double Epsilon = 1e-9;

        var kryssprodukt = ((b.Lon - a.Lon) * (lat - a.Lat)) - ((b.Lat - a.Lat) * (lon - a.Lon));
        if (Math.Abs(kryssprodukt) > Epsilon)
        {
            return false;
        }

        return lon >= Math.Min(a.Lon, b.Lon) - Epsilon && lon <= Math.Max(a.Lon, b.Lon) + Epsilon
            && lat >= Math.Min(a.Lat, b.Lat) - Epsilon && lat <= Math.Max(a.Lat, b.Lat) + Epsilon;
    }

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
