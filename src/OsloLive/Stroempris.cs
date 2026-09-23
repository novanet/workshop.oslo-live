using System.Text.Json;

namespace OsloLive;

/// <summary>
/// Strømprisen i Oslo (prisområde NO1), fra kilden «strompris» i Allemannsdata
/// (hvakosterstrommen.no). Ikke et kartlag: eget endepunkt i <c>Program.cs</c>.
/// </summary>
public static class Stroempris
{
    public const string Kilde = "strompris";
    public const string Operasjon = "get_prices";
    public const string Område = "NO1";

    /// <summary>NO1 har 25 % mva på strøm. Kilden gir prisen uten mva.</summary>
    private const double MvaFaktor = 1.25;

    public static TimeZoneInfo Oslo => TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    public sealed record Timepris(int Time, double Pris);

    public sealed record Svar(double Naa, Timepris Billigst, Timepris Dyrest, IReadOnlyList<Timepris> Timer);

    /// <summary>Regner om fra NOK/kWh uten mva til øre/kWh med mva.</summary>
    public static double TilOreMedMva(double nokUtenMva) => Math.Round(nokUtenMva * 100 * MvaFaktor, 2);

    /// <summary>
    /// Tolker radene fra <c>strompris/get_prices</c> til et svar for «nå».
    /// Gir <c>null</c> hvis ingen rad dekker <paramref name="nå"/>, slik at
    /// endepunktet kan svare 502.
    /// </summary>
    public static Svar? Tolk(IReadOnlyList<JsonElement> rader, DateTimeOffset nå)
    {
        var timer = new List<(int Time, double Pris, DateTimeOffset Start, DateTimeOffset Slutt)>();

        foreach (var rad in rader)
        {
            if (!rad.TryGetProperty("time_start", out var startFelt) ||
                !rad.TryGetProperty("time_end", out var sluttFelt) ||
                !rad.TryGetProperty("NOK_per_kWh", out var prisFelt) ||
                prisFelt.ValueKind != JsonValueKind.Number ||
                !DateTimeOffset.TryParse(startFelt.GetString(), out var start) ||
                !DateTimeOffset.TryParse(sluttFelt.GetString(), out var slutt))
            {
                continue;
            }

            timer.Add((start.Hour, TilOreMedMva(prisFelt.GetDouble()), start, slutt));
        }

        if (timer.Count == 0)
        {
            return null;
        }

        var gjeldende = timer.FindIndex(t => nå >= t.Start && nå < t.Slutt);
        if (gjeldende < 0)
        {
            return null;
        }

        var sortert = timer.OrderBy(t => t.Time).Select(t => new Timepris(t.Time, t.Pris)).ToList();
        var billigst = sortert.MinBy(t => t.Pris)!;
        var dyrest = sortert.MaxBy(t => t.Pris)!;

        return new Svar(timer[gjeldende].Pris, billigst, dyrest, sortert);
    }
}
