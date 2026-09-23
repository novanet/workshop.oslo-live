using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsloLive.Bysykkel;

/// <summary>Én rad fra Oslo Bysykkels åpne månedsfil. Alle tider er UTC.</summary>
public sealed record Kildetur(
    [property: JsonPropertyName("started_at")] string? StartetVed,
    [property: JsonPropertyName("ended_at")] string? SluttetVed,
    [property: JsonPropertyName("start_station_latitude")] double? FraLat,
    [property: JsonPropertyName("start_station_longitude")] double? FraLon,
    [property: JsonPropertyName("end_station_latitude")] double? TilLat,
    [property: JsonPropertyName("end_station_longitude")] double? TilLon);

/// <summary>Én kompakt sykkeltur, klar for avspilling. <see cref="Fra"/> og <see cref="Til"/> er <c>[lon, lat]</c>.</summary>
public sealed record Bysykkeltur(double[] Fra, double[] Til, int Start, int Slutt);

/// <summary>Den travleste hverdagen i siste hele måned, med alle turene den dagen.</summary>
public sealed record Bysykkeldøgn(string Dato, IReadOnlyList<Bysykkeltur> Turer);

/// <summary>
/// Ren logikk for å velge ut og filtrere ett døgn med bysykkelturer fra Oslo Bysykkels
/// åpne data, se <see cref="Bysykkeltjeneste"/> for nedlasting og mellomlagring.
/// </summary>
public static class Bysykkeldogn
{
    public static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    public static readonly TimeSpan MaksVarighet = TimeSpan.FromHours(3);

    /// <summary>Siste hele måned, regnet i Oslo-tid. Januar gir desember året før.</summary>
    public static (int År, int Måned) SisteHeleMåned(DateTimeOffset nå)
    {
        var lokal = TimeZoneInfo.ConvertTime(nå, Oslo);
        var forrige = new DateOnly(lokal.Year, lokal.Month, 1).AddDays(-1);
        return (forrige.Year, forrige.Month);
    }

    /// <summary>Adressen til månedsfila hos Oslo Bysykkel/urbansharing.</summary>
    public static string MånedsUrl(int år, int måned) =>
        string.Create(CultureInfo.InvariantCulture, $"https://data.urbansharing.com/oslobysykkel.no/trips/v1/{år:0000}/{måned:00}.json");

    /// <summary>
    /// Gjør om én kilderad til en kompakt tur i Oslo-tid, eller <c>null</c> hvis raden
    /// mangler felt, har ugyldig tid, samme start- og sluttstasjon, eller varer for lenge.
    /// </summary>
    public static (DateOnly Dato, Bysykkeltur Tur)? TilTur(Kildetur rad)
    {
        if (rad.StartetVed is null || rad.SluttetVed is null ||
            rad.FraLat is null || rad.FraLon is null || rad.TilLat is null || rad.TilLon is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(rad.StartetVed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start) ||
            !DateTimeOffset.TryParse(rad.SluttetVed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var slutt))
        {
            return null;
        }

        if (rad.FraLat == rad.TilLat && rad.FraLon == rad.TilLon)
        {
            return null;
        }

        if (slutt <= start || slutt - start > MaksVarighet)
        {
            return null;
        }

        var lokal = TimeZoneInfo.ConvertTime(start, Oslo);
        var dato = DateOnly.FromDateTime(lokal.DateTime);
        var startSekunder = (int)lokal.TimeOfDay.TotalSeconds;
        var sluttSekunder = startSekunder + (int)(slutt - start).TotalSeconds;

        var tur = new Bysykkeltur(
            [Math.Round(rad.FraLon.Value, 5), Math.Round(rad.FraLat.Value, 5)],
            [Math.Round(rad.TilLon.Value, 5), Math.Round(rad.TilLat.Value, 5)],
            startSekunder,
            sluttSekunder);

        return (dato, tur);
    }

    /// <summary>
    /// Velger den travleste hverdagen (mandag–fredag) blant turene, og sorterer
    /// dens turer etter starttidspunkt. Ved likt antall vinner den tidligste datoen.
    /// Returnerer <c>null</c> hvis ingen hverdag finnes.
    /// </summary>
    public static Bysykkeldøgn? VelgDag(IEnumerable<(DateOnly Dato, Bysykkeltur Tur)> turer)
    {
        var travlest = turer
            .Where(t => t.Dato.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .GroupBy(t => t.Dato)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .FirstOrDefault();

        if (travlest is null)
        {
            return null;
        }

        var sortert = travlest.Select(t => t.Tur).OrderBy(t => t.Start).ToList();
        return new Bysykkeldøgn(travlest.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), sortert);
    }

    /// <summary>Leser turer fra en strøm (månedsfila) uten å holde hele innholdet i minnet som tekst.</summary>
    public static async Task<Bysykkeldøgn?> Velg(Stream strøm, CancellationToken stopp)
    {
        var turer = new List<(DateOnly Dato, Bysykkeltur Tur)>();

        await foreach (var rad in JsonSerializer.DeserializeAsyncEnumerable<Kildetur>(strøm, cancellationToken: stopp))
        {
            if (rad is not null && TilTur(rad) is { } tur)
            {
                turer.Add(tur);
            }
        }

        return VelgDag(turer);
    }
}
