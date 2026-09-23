using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Målestasjonene for luftkvalitet i Oslo, med siste målte nivå og et
/// modellert varsel for det neste døgnet.
///
/// Dette er eksempellaget. Bruk det som mal når du legger til nye lag:
///
///   1. Finn kilden og operasjonen med Allemannsdata-MCP-serveren.
///   2. Kall den med <see cref="Allemannsdata.HentListe"/>.
///   3. Lag ett <see cref="Geo.Lag"/>-punkt per rad.
///   4. Returner <see cref="Geo.Samle"/>.
/// </summary>
public sealed class LuftkvalitetLag(Allemannsdata data) : ILag
{
    public string Id => "luftkvalitet";
    public string Navn => "Luftkvalitet";
    public string Beskrivelse => "Målestasjoner med siste målte luftkvalitet og varsel for neste døgn.";
    public string Ikon => "🌬️";

    /// <summary>
    /// Antall stasjoner vi henter varsel for per oppdatering. Varselet tar en
    /// koordinat, ikke en stasjonsliste, så ett kall per stasjon er nødvendig;
    /// vi begrenser oss til de nærmeste for å ikke belaste kilden for mye.
    /// </summary>
    public const int MaksVarselkall = 8;

    /// <summary>Tak per varselkall, slik at én treg stasjon ikke forsinker resten av laget.</summary>
    private static readonly TimeSpan VarselFrist = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Nivåene til Allemannsdata sin luftkvalitetsindeks: et helt tall fra 1
    /// til 4, avrundet ned fra den kontinuerlige AQI-verdien varselet gir per
    /// time. Kilden gir bare nivånavnet for nå-verdien («now_level»), ikke
    /// per time i varselet, så vi regner navnet ut selv fra samme skala.
    /// </summary>
    private static readonly string[] Nivåer = ["Lite", "Moderat", "Mye", "Svært mye"];

    public static string NivåFraAqi(double aqi) =>
        Nivåer[Math.Clamp((int)Math.Floor(aqi) - 1, 0, Nivåer.Length - 1)];

    public static bool ErAdvarsel(string? nivå)
    {
        var rang = Array.IndexOf(Nivåer, nivå);
        return rang >= Array.IndexOf(Nivåer, "Mye");
    }

    public sealed record Varsel(string Tekst, string VersteNivå);

    /// <summary>
    /// Finner det verste nivået og lager en kort tekst av AQI-timeserien for
    /// de neste 24 timene regnet fra <paramref name="nå"/>.
    /// </summary>
    public static Varsel? TolkVarsel(IReadOnlyList<JsonElement> timer, DateTimeOffset nå)
    {
        var punkter = new List<(DateTimeOffset Tid, double Aqi)>();
        foreach (var time in timer)
        {
            if (!time.TryGetProperty("time", out var tField) || !time.TryGetProperty("value", out var vField))
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(tField.GetString(), out var tid))
            {
                continue;
            }

            if (tid < nå || tid >= nå.AddHours(24))
            {
                continue;
            }

            punkter.Add((tid, vField.GetDouble()));
        }

        if (punkter.Count == 0)
        {
            return null;
        }

        var verst = punkter.MaxBy(p => p.Aqi);
        var versteNivå = NivåFraAqi(verst.Aqi);

        var tekst = punkter.All(p => NivåFraAqi(p.Aqi) == versteNivå)
            ? $"{versteNivå} hele neste døgn"
            : $"Verst {versteNivå} om ca. {(int)Math.Round((verst.Tid - nå).TotalHours)} timer";

        return new Varsel(tekst, versteNivå);
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: luftkvalitet, operasjon: get_air_quality_nearby.
        // «data» er her en liste rett ut, så vi trenger ikke oppgi listenavn.
        var rader = await data.HentListe(
            "luftkvalitet",
            "get_air_quality_nearby",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["limit"] = 50,
            },
            liste: null,
            stopp);

        // Sortert etter avstand av kilden, så de nærmeste stasjonene får varsel.
        var antallMedVarsel = Math.Min(rader.Count, MaksVarselkall);
        var varsler = await Task.WhenAll(rader.Take(antallMedVarsel).Select(rad => HentVarsel(rad, stopp)));

        var punkter = rader.Select((rad, i) =>
        {
            var navn = rad.GetProperty("station").GetString() ?? "Ukjent stasjon";
            var nivå = rad.TryGetProperty("aqi_level", out var n) ? n.GetString() : null;
            var varsel = i < varsler.Length ? varsler[i] : null;

            var detaljer = new Dictionary<string, object?>
            {
                ["nivå"] = nivå ?? "ukjent",
                ["målt"] = rad.TryGetProperty("aqi_time", out var t) ? t.GetString() : null,
                ["varsel"] = varsel?.Tekst,
                ["verste nivå neste døgn"] = varsel?.VersteNivå,
            };

            if (varsel is not null && ErAdvarsel(varsel.VersteNivå))
            {
                detaljer["advarsel"] = $"{varsel.VersteNivå} ventet neste døgn";
            }

            return Geo.Lag(
                id: rad.TryGetProperty("eoi", out var eoi) ? eoi.GetString() ?? navn : navn,
                lat: rad.GetProperty("latitude").GetDouble(),
                lon: rad.GetProperty("longitude").GetDouble(),
                navn: navn,
                kilde: "Luftkvalitet i Norge",
                detaljer: detaljer);
        });

        return Geo.Samle(punkter);
    }

    /// <summary>
    /// Henter varselet for én stasjon. Svikter kallet eller tar det for lang
    /// tid, gir vi opp varselet for denne stasjonen uten å felle hele laget.
    /// </summary>
    private async Task<Varsel?> HentVarsel(JsonElement rad, CancellationToken stopp)
    {
        using var frist = CancellationTokenSource.CreateLinkedTokenSource(stopp);
        frist.CancelAfter(VarselFrist);

        try
        {
            var lat = rad.GetProperty("latitude").GetDouble();
            var lon = rad.GetProperty("longitude").GetDouble();

            var komponenter = await data.HentListe(
                "luftkvalitet",
                "get_air_quality_forecast",
                new Dictionary<string, object> { ["lat"] = lat, ["lon"] = lon },
                liste: "components",
                frist.Token);

            var aqiKomponent = komponenter.FirstOrDefault(k =>
                k.TryGetProperty("component", out var c) && c.GetString() == "AQI");

            if (aqiKomponent.ValueKind != JsonValueKind.Object || !aqiKomponent.TryGetProperty("values", out var verdier))
            {
                return null;
            }

            return TolkVarsel(verdier.EnumerateArray().ToList(), DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
