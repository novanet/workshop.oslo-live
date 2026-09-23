using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// NVEs målestasjoner i elver og bekker i og rundt Oslo, hentet fra NVE HydAPI
/// (kilde «nve», operasjon «find_hydro_stations»). Vi bruker parameteren
/// «has_recent_data» for å luke ut stasjoner uten fersk data, ikke «active»:
/// kildens egen beskrivelse sier at de fleste «active»-stasjoner bare er
/// arkivstasjoner uten observasjoner.
/// </summary>
public sealed class VannmaalereLag(Allemannsdata data) : ILag
{
    public string Id => "vannmaalere";
    public string Navn => "Vannmålere";
    public string Beskrivelse => "NVEs målestasjoner i elver og bekker i Oslo, med hva de måler og når de sist sendte data.";
    public string Ikon => "💧";

    /// <summary>Radius rundt Oslo sentrum. Dekker Lysakerelva i vest og Alna i øst.</summary>
    public const double RadiusKm = 20;

    /// <summary>Kildens største side. Rundt Oslo gir kilden rundt 40 stasjoner, altså én side.</summary>
    public const int MaksStasjoner = 100;

    /// <summary>
    /// En serie regnes som levende hvis siste verdi er under så lenge før
    /// stasjonens siste måling. Samme grense som kildens «has_recent_data».
    /// </summary>
    public static readonly TimeSpan FerskGrense = TimeSpan.FromDays(7);

    /// <summary>
    /// «has_recent_data=true»: bare stasjoner der nyeste data er under 7 dager
    /// gammel. «active» sier ikke noe om ferskhet, og utelates derfor.
    /// </summary>
    public static IReadOnlyDictionary<string, object> Parametre { get; } = new Dictionary<string, object>
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
        ["radius_km"] = RadiusKm,
        ["has_recent_data"] = true,
        ["limit"] = MaksStasjoner,
    };

    /// <summary>
    /// Slår sammen navnene på seriene som fortsatt leverer data, i den
    /// rekkefølgen kilden lister dem, uten gjentakelser. En serie som sluttet
    /// å levere for lenge siden (sammenlignet med stasjonens egen
    /// «latest_observation_at») telles ikke med.
    /// </summary>
    public static string? Måler(JsonElement rad)
    {
        if (!rad.TryGetProperty("series", out var serier) || serier.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        DateTimeOffset? stasjonstid = rad.TryGetProperty("latest_observation_at", out var s) && s.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(s.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var st)
            ? st
            : null;

        var navn = new List<string>();

        foreach (var serie in serier.EnumerateArray())
        {
            if (!serie.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var serienavn = n.GetString()!.Trim();
            if (serienavn.Length == 0)
            {
                continue;
            }

            DateTimeOffset? serietid = serie.TryGetProperty("latest_data_at", out var l) && l.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(l.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var lt)
                ? lt
                : null;

            if (stasjonstid is not null && serietid is not null && stasjonstid.Value - serietid.Value > FerskGrense)
            {
                continue;
            }

            navn.Add(serienavn);
        }

        var distinkte = navn.Distinct().ToList();
        return distinkte.Count == 0 ? null : string.Join(", ", distinkte);
    }

    /// <summary>Én rad med en stasjon blir ett punkt. Mangler posisjon eller id, blir det ikke noe punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("latitude", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("longitude", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (!rad.TryGetProperty("station_id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var id = idEl.GetString()!;
        if (id.Length == 0)
        {
            return null;
        }

        var navn = rad.TryGetProperty("station_name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!.Trim()
            : "";
        if (navn.Length == 0)
        {
            navn = "Ukjent stasjon";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("river", out var e) && e.ValueKind == JsonValueKind.String)
        {
            var elv = e.GetString()!.Trim();
            if (elv.Length > 0)
            {
                detaljer["elv"] = elv;
            }
        }

        var måler = Måler(rad);
        if (måler is not null)
        {
            detaljer["måler"] = måler;
        }

        if (rad.TryGetProperty("latest_observation_at", out var sistMålt) && sistMålt.ValueKind == JsonValueKind.String)
        {
            var verdi = sistMålt.GetString()!;
            if (verdi.Length > 0)
            {
                detaljer["sist målt"] = verdi;
            }
        }

        return Geo.Lag(
            id: id,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "NVE HydAPI",
            detaljer: detaljer);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: nve, operasjon: find_hydro_stations. «data» er et objekt med listen i «stations».
        var rader = await data.HentListe("nve", "find_hydro_stations", Parametre, liste: "stations", stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
