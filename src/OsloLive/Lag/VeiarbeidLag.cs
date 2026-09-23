using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>Veiarbeid, stengte veier og andre trafikkmeldinger fra Statens vegvesen (DATEX II) som gjelder nå.</summary>
public sealed class VeiarbeidLag(Allemannsdata data) : ILag
{
    public string Id => "veiarbeid";
    public string Navn => "Veiarbeid";
    public string Beskrivelse => "Veiarbeid, stengte veier og trafikkhendelser fra Statens vegvesen som gjelder nå.";
    public string Ikon => "🚧";

    /// <summary>
    /// Kilde: vegvesen, operasjon: get_traffic_messages. «include_planned» holdes på
    /// standardverdien false, slik at vi bare får meldinger som gjelder akkurat nå.
    /// </summary>
    public static Dictionary<string, object> Parametre() => new()
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
        ["radius_km"] = 20,
        ["limit"] = 100,
    };

    /// <summary>Kildens «severity» oversatt til norsk. Ukjent verdi gir «Ukjent».</summary>
    public static string Alvorlighet(string? severity) => severity switch
    {
        "highest" => "Svært høy",
        "high" => "Høy",
        "medium" => "Middels",
        "low" => "Lav",
        "lowest" => "Svært lav",
        "none" => "Ingen",
        _ => "Ukjent",
    };

    /// <summary>Skiller høy alvorlighet ut, slik at frontenden kan gi den et eget ikon.</summary>
    public static bool ErHøy(string? severity) => severity is "high" or "highest";

    /// <summary>
    /// Norsk tekst for hva slags melding det er. Stengt vei eller kjørefelt går foran
    /// kildens «type», slik at frontendens varselregel for stengte veier fanger dem opp.
    /// </summary>
    public static string TypeTekst(string? type, IEnumerable<string> underTyper)
    {
        if (underTyper.Contains("roadClosed"))
        {
            return "Stengt vei";
        }

        if (underTyper.Contains("laneClosures"))
        {
            return "Stengt kjørefelt";
        }

        return type switch
        {
            "MaintenanceWorks" => "Vedlikehold",
            "ConstructionWorks" => "Veiarbeid",
            "RoadOrCarriagewayOrLaneManagement" => "Trafikkregulering",
            "SpeedManagement" => "Nedsatt fartsgrense",
            "ReroutingManagement" => "Omkjøring",
            "Accident" => "Ulykke",
            "VehicleObstruction" => "Kjøretøy i veien",
            "EnvironmentalObstruction" => "Vær og føre",
            "AnimalPresenceObstruction" => "Dyr i veien",
            "InfrastructureDamageObstruction" => "Skade på vei",
            "PublicEvent" => "Arrangement",
            "TransitInformation" => "Ferje",
            null => "Trafikkmelding",
            _ => type,
        };
    }

    /// <summary>
    /// Kort navn på stedet: delen av «location» før første « - », eller «road»,
    /// eller «Veiarbeid» hvis begge mangler.
    /// </summary>
    public static string KortNavn(JsonElement rad)
    {
        var sted = rad.TryGetProperty("location", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
        if (!string.IsNullOrWhiteSpace(sted))
        {
            var del = sted.Split(" - ", 2)[0].Trim();
            if (del.Length > 0)
            {
                return del;
            }
        }

        var vei = rad.TryGetProperty("road", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return string.IsNullOrWhiteSpace(vei) ? "Veiarbeid" : vei;
    }

    /// <summary>Sluttidspunktet formatert i Oslo-tid, eller «ukjent» når det mangler eller ikke lar seg tolke.</summary>
    public static string VarerTil(string? endTime)
    {
        if (endTime is null || !DateTimeOffset.TryParse(endTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tid))
        {
            return "ukjent";
        }

        var osloTid = TimeZoneInfo.ConvertTime(tid, Stroempris.Oslo);
        return osloTid.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Oversetter én trafikkmelding fra kilden til et kartpunkt. Null når meldingen mangler koordinat.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var lat) || lat.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lon) || lon.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var type = rad.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var beskrivelse = rad.TryGetProperty("description", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;
        var severity = rad.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        var endTime = rad.TryGetProperty("end_time", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        var vei = rad.TryGetProperty("road", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        var underTyper = rad.TryGetProperty("sub_types", out var u) && u.ValueKind == JsonValueKind.Array
            ? u.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)
            : [];

        return Geo.Lag(
            id: rad.GetProperty("situation_id").GetString()!,
            lat: lat.GetDouble(),
            lon: lon.GetDouble(),
            navn: KortNavn(rad),
            kilde: "Statens vegvesen",
            detaljer: new Dictionary<string, object?>
            {
                ["type"] = TypeTekst(type, underTyper),
                ["beskrivelse"] = beskrivelse,
                ["alvorlighet"] = Alvorlighet(severity),
                ["varer til"] = VarerTil(endTime),
                ["vei"] = vei,
                ["ikon"] = ErHøy(severity) ? "⛔" : null,
            });
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe("vegvesen", "get_traffic_messages", Parametre(), liste: "messages", stopp);
        return Geo.Samle(rader.Select(TilPunkt));
    }
}
