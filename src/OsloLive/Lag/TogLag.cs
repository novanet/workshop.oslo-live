using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Persontog og godstog gjennom Oslo. GPS-posisjon for persontog kommer fra
/// Entur, mens Bane NOR gir stasjonstavla med tognummer, opprinnelse,
/// destinasjon og godstog. Et tog uten kjent GPS-posisjon vises på sist
/// kjente stasjon.
///
/// Kollektivlaget kan også vise persontog med GPS fra Entur, med ikonet 🚆.
/// Dette laget bruker ikonet 🚂 for å skille seg fra det, og setter ikke noe
/// eget ikon per punkt, så et tog vises aldri med samme ikon to ganger selv om
/// begge lag er slått på samtidig. Til forskjell fra kollektivlaget viser
/// dette laget også godstog og tog som står på stasjon, med feltene «fra»,
/// «til» og (for stasjonsposisjoner) «stasjon».
/// </summary>
public sealed class TogLag(Allemannsdata data) : ILag
{
    public string Id => "tog";
    public string Navn => "Tog";
    public string Beskrivelse => "Persontog og godstog i Oslo med tognummer, avgangssted og endestasjon.";
    public string Ikon => "🚂";

    /// <summary>Radius rundt Oslo sentrum for GPS-posisjoner fra Entur.</summary>
    public const double RadiusKm = 20;

    /// <summary>Tak på antall tog med GPS, romslig nok til å dekke alle togene i og rundt Oslo.</summary>
    public const int MaksTog = 50;

    /// <summary>Stasjoner vi henter stasjonstavla for. Koordinatene er faste, siden kilden ikke gir dem.</summary>
    private static readonly (string Kode, string Navn, double Lat, double Lon)[] Stasjoner =
    [
        ("OSL", "Oslo S", 59.9110, 10.7528),
        ("NTH", "Nationaltheatret", 59.9145, 10.7317),
        ("SKØ", "Skøyen", 59.9227, 10.6790),
        ("BR", "Bryn", 59.9092, 10.8163),
        ("ALB", "Alnabru", 59.9320, 10.8440),
    ];

    /// <summary>Tognummeret er delen av <c>vehicle_id</c> foran den første bindestreken.</summary>
    public static string? Tognummer(string? kjøretøyId)
    {
        if (string.IsNullOrEmpty(kjøretøyId))
        {
            return null;
        }

        var nummer = kjøretøyId.Split('-', 2)[0];
        return nummer.Length > 0 ? nummer : null;
    }

    /// <summary>Ett GPS-punkt fra Entur, eller null hvis raden ikke er et tog med kjent posisjon.</summary>
    public static Kartpunkt? FraGps(JsonElement rad, IReadOnlyDictionary<string, JsonElement> tavle)
    {
        if (rad.TryGetProperty("mode", out var modus) && modus.ValueKind == JsonValueKind.String
            && modus.GetString() != "RAIL")
        {
            return null;
        }

        var nummer = rad.TryGetProperty("vehicle_id", out var v) && v.ValueKind == JsonValueKind.String
            ? Tognummer(v.GetString())
            : null;

        if (nummer is null
            || !rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var (fra, til) = FraOgTil(rad, nummer, tavle);

        var detaljer = new Dictionary<string, object?>
        {
            ["tognummer"] = nummer,
            ["fra"] = fra,
            ["til"] = til,
        };

        if (rad.TryGetProperty("line", out var linje) && linje.ValueKind == JsonValueKind.String)
        {
            detaljer["linje"] = linje.GetString();
        }

        return Geo.Lag(
            id: $"tog:{nummer}",
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: nummer,
            kilde: "Entur",
            detaljer: detaljer);
    }

    /// <summary>Finner fra/til fra stasjonstavla, eller fra linjenavnet («Stabekk-Oslo S-Moss») hvis toget ikke er på tavla.</summary>
    private static (string Fra, string Til) FraOgTil(JsonElement gpsRad, string nummer, IReadOnlyDictionary<string, JsonElement> tavle)
    {
        if (tavle.TryGetValue(nummer, out var tavleRad))
        {
            var fraTavle = tavleRad.TryGetProperty("origin", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : null;
            var tilTavle = tavleRad.TryGetProperty("destination", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            if (fraTavle is not null && tilTavle is not null)
            {
                return (fraTavle, tilTavle);
            }
        }

        if (gpsRad.TryGetProperty("line_name", out var ln) && ln.ValueKind == JsonValueKind.String)
        {
            var deler = ln.GetString()!.Split('-');
            if (deler.Length >= 2)
            {
                return (deler[0].Trim(), deler[^1].Trim());
            }
        }

        return ("ukjent", "ukjent");
    }

    /// <summary>Ett punkt for et tog som står på stasjonen, eller null hvis toget ikke er der eller er innstilt.</summary>
    public static Kartpunkt? FraStasjon(JsonElement tog, (string Kode, string Navn, double Lat, double Lon) stasjon)
    {
        if (!tog.TryGetProperty("at_stop", out var påStasjon) || påStasjon.ValueKind != JsonValueKind.True)
        {
            return null;
        }

        if (tog.TryGetProperty("departure_status", out var status) && status.ValueKind == JsonValueKind.String
            && status.GetString() == "cancelled")
        {
            return null;
        }

        var nummer = tog.TryGetProperty("train", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        if (string.IsNullOrEmpty(nummer))
        {
            return null;
        }

        var tjeneste = tog.TryGetProperty("service", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;

        var detaljer = new Dictionary<string, object?>
        {
            ["tognummer"] = nummer,
            ["fra"] = tog.TryGetProperty("origin", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : "ukjent",
            ["til"] = tog.TryGetProperty("destination", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : "ukjent",
            ["stasjon"] = stasjon.Navn,
            ["type"] = tjeneste == "freight" ? "godstog" : "persontog",
        };

        if (tog.TryGetProperty("operator", out var op) && op.ValueKind == JsonValueKind.String)
        {
            detaljer["operatør"] = op.GetString();
        }

        if (tog.TryGetProperty("line", out var linje) && linje.ValueKind == JsonValueKind.String)
        {
            detaljer["linje"] = linje.GetString();
        }

        return Geo.Lag(
            id: $"tog:{nummer}",
            lat: stasjon.Lat,
            lon: stasjon.Lon,
            navn: nummer,
            kilde: "Bane NOR",
            detaljer: detaljer);
    }

    /// <summary>
    /// Samler GPS-posisjonene og stasjonstavlene til ett lag. GPS-punktene
    /// kommer først, slik at <see cref="Geo.Samle"/> beholder GPS-posisjonen
    /// og forkaster stasjonspunktet når samme tog finnes begge steder.
    /// </summary>
    public static Kartlag Samle(IEnumerable<JsonElement> gps, IEnumerable<(JsonElement Tog, (string Kode, string Navn, double Lat, double Lon) Stasjon)> påStasjon)
    {
        var toglisteMedTavle = påStasjon.ToList();

        var tavle = new Dictionary<string, JsonElement>();
        foreach (var (tog, _) in toglisteMedTavle)
        {
            if (tog.TryGetProperty("train", out var t) && t.ValueKind == JsonValueKind.String)
            {
                var nummer = t.GetString()!;
                tavle.TryAdd(nummer, tog);
            }
        }

        var gpsPunkter = gps.Select(rad => FraGps(rad, tavle));
        var stasjonPunkter = toglisteMedTavle.Select(p => FraStasjon(p.Tog, p.Stasjon));

        return Geo.Samle(gpsPunkter.Concat(stasjonPunkter));
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var gpsOppgave = HentGpsTrygt(stopp);
        var stasjonsOppgaver = Stasjoner.Select(s => HentStasjonTrygt(s, stopp)).ToList();

        var gps = await gpsOppgave;
        var stasjonssvar = await Task.WhenAll(stasjonsOppgaver);

        var påStasjon = stasjonssvar
            .SelectMany((rader, i) => rader.Select(rad => (Tog: rad, Stasjon: Stasjoner[i])));

        return Samle(gps, påStasjon);
    }

    /// <summary>Svikter GPS-kallet, viser vi bare togene fra stasjonstavlene i stedet for å felle hele laget.</summary>
    private async Task<IReadOnlyList<JsonElement>> HentGpsTrygt(CancellationToken stopp)
    {
        try
        {
            return await data.HentListe(
                "entur",
                "find_live_vehicles_nearby",
                new Dictionary<string, object>
                {
                    ["lat"] = Geo.OsloLat,
                    ["lon"] = Geo.OsloLon,
                    ["radius_km"] = RadiusKm,
                    ["mode"] = "RAIL",
                    ["limit"] = MaksTog,
                },
                liste: "vehicles",
                stopp);
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Svikter tavla for én stasjon, tar det ikke ned resten av laget.</summary>
    private async Task<IReadOnlyList<JsonElement>> HentStasjonTrygt((string Kode, string Navn, double Lat, double Lon) stasjon, CancellationToken stopp)
    {
        try
        {
            return await data.HentListe(
                "banenor",
                "get_station_board",
                new Dictionary<string, object>
                {
                    ["station_id"] = stasjon.Kode,
                    ["minutes_ahead"] = 60,
                    ["max_trains"] = 20,
                    ["limit"] = 20,
                },
                liste: "trains",
                stopp);
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }
}
