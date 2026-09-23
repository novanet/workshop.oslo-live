namespace OsloLive.Kart;

/// <summary>Ett punkt på kartet, slik GeoJSON vil ha det.</summary>
public sealed record Geometri(string Type, double[] Coordinates);

/// <summary>En ting som tegnes på kartet: et punkt pluss det vi vet om det.</summary>
public sealed record Kartpunkt(string Type, Geometri Geometry, Dictionary<string, object?> Properties);

/// <summary>Svaret et lag gir tilbake. Dette er formatet Leaflet leser.</summary>
public sealed record Kartlag(string Type, IReadOnlyList<Kartpunkt> Features);

/// <summary>Kartutsnittet: boksen kartet dekker, og sentrum lag spør etter data «i nærheten» av.</summary>
public sealed record Kartutsnitt(double MinLat, double MaksLat, double MinLon, double MaksLon, double SentrumLat, double SentrumLon)
{
    /// <summary>Oslo og indre Oslofjord. Standardverdien når «Kart»-seksjonen mangler i oppsettet.</summary>
    public static readonly Kartutsnitt Oslo = new(59.80, 60.14, 10.45, 10.98, 59.9139, 10.7522);
}

/// <summary>
/// Kartutsnittet er statisk fordi alle lag og <see cref="Geo"/> selv bruker det gjennom
/// hele appens levetid, og fordi det ikke er noe å instansiere flere av. <see cref="Bruk"/>
/// setter utsnittet én gang ved oppstart, fra «Kart»-seksjonen i oppsettet eller Oslo som
/// standard. Tester som vil prøve et annet utsnitt bruker <see cref="MedUtsnitt"/>, som legger
/// utsnittet i en <see cref="AsyncLocal{T}"/>: verdien følger bare den logiske kallkjeden til
/// testen selv, så andre tester som kjører samtidig på andre tråder eller async-flyter ser
/// aldri overstyringen, og den nulles ut igjen når testen er ferdig.
/// </summary>
public static class Geo
{
    private static Kartutsnitt _standard = Kartutsnitt.Oslo;
    private static readonly AsyncLocal<Kartutsnitt?> _overstyring = new();

    private static Kartutsnitt Aktivt => _overstyring.Value ?? _standard;

    /// <summary>Setter kartutsnittet appen bruker. Kalles én gang ved oppstart.</summary>
    public static void Bruk(Kartutsnitt utsnitt) => _standard = utsnitt;

    /// <summary>
    /// Kjører <paramref name="handling"/> med et annet kartutsnitt, og setter det gjeldende
    /// utsnittet tilbake når handlingen er ferdig. Til bruk i tester; påvirker ikke andre
    /// tester som kjører samtidig.
    /// </summary>
    public static T MedUtsnitt<T>(Kartutsnitt utsnitt, Func<T> handling)
    {
        var forrige = _overstyring.Value;
        _overstyring.Value = utsnitt;
        try
        {
            return handling();
        }
        finally
        {
            _overstyring.Value = forrige;
        }
    }

    /// <summary>Grensene til kartutsnittet. Alt utenfor denne boksen hører ikke hjemme på kartet.</summary>
    public static double MinLat => Aktivt.MinLat;
    public static double MaksLat => Aktivt.MaksLat;
    public static double MinLon => Aktivt.MinLon;
    public static double MaksLon => Aktivt.MaksLon;

    /// <summary>Sentrum i kartutsnittet. Brukes som utgangspunkt når et lag spør etter data «i nærheten».</summary>
    public static double OsloLat => Aktivt.SentrumLat;
    public static double OsloLon => Aktivt.SentrumLon;

    /// <summary>Kartutsnittet slik det er nå. Brukt av <c>/api/kart</c>.</summary>
    public static Kartutsnitt Utsnitt => Aktivt;

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
