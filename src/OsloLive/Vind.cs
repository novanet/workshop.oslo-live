using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive;

/// <summary>
/// Vind over Oslo fra MET locationforecast (compact), som et rutenett på 4 x 4 punkter over
/// kartutsnittet i <see cref="Geo"/>. Ikke et kartlag: eget endepunkt /api/vind i Program.cs.
/// Egen navngitt HttpClient («met»), mellomlager i delt IMemoryCache i 30 minutter.
/// </summary>
public static class Vind
{
    public const string KlientNavn = "met";
    public const int Side = 4; // 4 x 4 punkter
    public const int MaksSamtidige = 4;
    private const string MellomlagerNøkkel = "vind-rutenett";
    public static readonly TimeSpan Levetid = TimeSpan.FromMinutes(30);

    public sealed record Vindpunkt(double Lat, double Lon, double U, double V);

    /// <summary>
    /// Regner om fra vindfart og retningen vinden kommer fra til øst- og nordkomponent (m/s).
    /// «wind_from_direction» er retningen vinden kommer fra, så vektoren peker motsatt vei.
    /// </summary>
    public static (double U, double V) TilUv(double fart, double fraRetningGrader)
    {
        var rad = fraRetningGrader * Math.PI / 180;
        var u = -fart * Math.Sin(rad);
        var v = -fart * Math.Cos(rad);
        return (u, v);
    }

    /// <summary>
    /// De 16 punktene i rutenettet, jevnt fordelt over kartutsnittet inkludert kantene,
    /// rad for rad fra sør til nord og fra vest til øst innenfor hver rad.
    /// Avrundes til 4 desimaler: MET avviser flere, og svaret skal gi tilbake de samme
    /// koordinatene som ble spurt om.
    /// </summary>
    public static IReadOnlyList<(double Lat, double Lon)> Rutenett()
    {
        var punkter = new List<(double Lat, double Lon)>();

        for (var i = 0; i < Side; i++)
        {
            var lat = Math.Round(Geo.MinLat + (i * (Geo.MaksLat - Geo.MinLat) / (Side - 1)), 4);
            for (var j = 0; j < Side; j++)
            {
                var lon = Math.Round(Geo.MinLon + (j * (Geo.MaksLon - Geo.MinLon) / (Side - 1)), 4);
                punkter.Add((lat, lon));
            }
        }

        return punkter;
    }

    /// <summary>Adressen til MET locationforecast (compact) for ett punkt, med punktum og fire desimaler.</summary>
    public static string ByggUrl(double lat, double lon) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={0}&lon={1}",
            lat,
            lon);

    /// <summary>Leser vindfart og -retning fra det første tidspunktet i svaret, og regner om til u/v.</summary>
    public static (double U, double V) Tolk(JsonElement rot)
    {
        var tidsserie = rot.GetProperty("properties").GetProperty("timeseries");
        if (tidsserie.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("MET ga ingen tidsserie.");
        }

        var detaljer = tidsserie[0].GetProperty("data").GetProperty("instant").GetProperty("details");
        var fart = detaljer.GetProperty("wind_speed").GetDouble();
        var fraRetning = detaljer.GetProperty("wind_from_direction").GetDouble();

        return TilUv(fart, fraRetning);
    }

    /// <summary>
    /// Henter rutenettet, fra mellomlageret hvis det finnes der ennå. Ellers gjøres de 16 kallene
    /// til MET med høyst <see cref="MaksSamtidige"/> samtidig. Feiler ett punkt, feiler hele kallet
    /// og ingenting mellomlagres, slik at endepunktet kan svare 502 og prøve på nytt neste gang.
    /// </summary>
    public static async Task<IReadOnlyList<Vindpunkt>> Hent(IHttpClientFactory http, IMemoryCache mellomlager, CancellationToken stopp)
    {
        if (mellomlager.TryGetValue(MellomlagerNøkkel, out IReadOnlyList<Vindpunkt>? lagret) && lagret is not null)
        {
            return lagret;
        }

        var punkter = Rutenett();
        var svar = new Vindpunkt[punkter.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, punkter.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaksSamtidige, CancellationToken = stopp },
            async (i, t) =>
            {
                var (lat, lon) = punkter[i];
                using var klient = http.CreateClient(KlientNavn);
                using var respons = await klient.GetAsync(ByggUrl(lat, lon), t);
                respons.EnsureSuccessStatusCode();

                var kropp = await respons.Content.ReadAsStringAsync(t);
                var rot = JsonSerializer.Deserialize<JsonElement>(kropp);
                var (u, v) = Tolk(rot);

                svar[i] = new Vindpunkt(lat, lon, Math.Round(u, 2), Math.Round(v, 2));
            });

        mellomlager.Set(MellomlagerNøkkel, (IReadOnlyList<Vindpunkt>)svar, Levetid);
        return svar;
    }
}
