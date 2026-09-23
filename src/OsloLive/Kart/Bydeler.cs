using System.Text.Json;

namespace OsloLive.Kart;

/// <summary>Senteret Kartverket har registrert for en bydel.</summary>
public sealed record Bydelssenter(long Id, string Navn, double Lat, double Lon);

/// <summary>Antall punkter i én bydel. JSON: <c>{ bydel, antall }</c>.</summary>
public sealed record Bydelstelling(string Bydel, int Antall);

/// <summary>
/// Teller punktene i et lag per bydel. Det finnes ingen bydelsgrenser i
/// Allemannsdata, så vi bruker nærmeste bydelssenter (fra Kartverkets
/// stedsnavn) som en tilnærming til bydelsgrensene. Én oppdatering gjør
/// 9 kall (ett per prefiks i <see cref="Prefikser"/>), uavhengig av antall
/// punkter; svarene mellomlagres 30 s i <see cref="Allemannsdata"/>.
/// </summary>
public static class Bydeler
{
    /// <summary>
    /// Forbokstavene til Oslos 15 bydeler. Kilden avviser ledende jokertegn
    /// («*» eller «*e*»), så vi må søke på prefiks i stedet for ett kall for alle.
    /// </summary>
    public static readonly string[] Prefikser = ["A", "B", "F", "G", "N", "S", "U", "V", "Ø"];

    /// <summary>
    /// Et punkt lenger unna enn dette fra alle bydelssentre regnes som utenfor
    /// bydelene, og telles ikke med.
    /// </summary>
    public const double MaksAvstandMeter = 5000;

    /// <summary>
    /// Henter bydelssentrene for Oslo kommune: nøyaktig ett kall per prefiks,
    /// altså 9 kall per oppdatering, aldri ett kall per punkt.
    /// </summary>
    public static async Task<IReadOnlyList<Bydelssenter>> Hent(Allemannsdata data, CancellationToken stopp = default)
    {
        var svar = await Task.WhenAll(Prefikser.Select(prefiks => data.HentListe(
            "geonorge",
            "search_place_name",
            new Dictionary<string, object>
            {
                ["text"] = $"{prefiks}*",
                ["kommunenummer"] = "0301",
                ["navneobjekttype"] = "administrativBydel",
                ["limit"] = 100,
            },
            liste: "places",
            stopp)));

        return Tolk(svar.SelectMany(steder => steder));
    }

    /// <summary>
    /// Plukker ut de aktive bydelene med hovednavn fra søketreffene, og
    /// fjerner duplikater som følge av at flere prefikser treffer samme bydel.
    /// </summary>
    public static IReadOnlyList<Bydelssenter> Tolk(IEnumerable<JsonElement> steder)
    {
        var sentre = new List<Bydelssenter>();

        foreach (var sted in steder)
        {
            if (!sted.TryGetProperty("status", out var status) || status.GetString() != "aktiv")
            {
                continue;
            }

            if (!sted.TryGetProperty("names", out var names))
            {
                continue;
            }

            var hovednavn = names.EnumerateArray()
                .FirstOrDefault(n => n.TryGetProperty("status", out var s) && s.GetString() == "hovednavn");

            if (hovednavn.ValueKind != JsonValueKind.Object || !hovednavn.TryGetProperty("name", out var navn))
            {
                continue;
            }

            sentre.Add(new Bydelssenter(
                Id: sted.GetProperty("place_id").GetInt64(),
                Navn: navn.GetString() ?? "Ukjent",
                Lat: sted.GetProperty("lat").GetDouble(),
                Lon: sted.GetProperty("lon").GetDouble()));
        }

        return sentre.DistinctBy(s => s.Id).ToList();
    }

    /// <summary>
    /// Finner navnet på bydelen nærmest punktet, eller null hvis punktet
    /// ligger for langt fra alle sentre, eller det ikke finnes noen.
    /// </summary>
    public static string? Finn(double lat, double lon, IReadOnlyList<Bydelssenter> sentre)
    {
        if (sentre.Count == 0)
        {
            return null;
        }

        var nærmest = sentre.MinBy(s => Geo.Avstand(lat, lon, s.Lat, s.Lon))!;
        var avstand = Geo.Avstand(lat, lon, nærmest.Lat, nærmest.Lon);

        return avstand <= MaksAvstandMeter ? nærmest.Navn : null;
    }

    /// <summary>
    /// Teller punktene i laget per bydel. Punkter som ikke treffer noen bydel
    /// utelates, slik at summen av <see cref="Bydelstelling.Antall"/> er lik
    /// antall punkter som fikk en bydel.
    /// </summary>
    public static IReadOnlyList<Bydelstelling> Tell(Kartlag lag, IReadOnlyList<Bydelssenter> sentre) =>
        lag.Features
            .Select(f => Finn(lat: f.Geometry.Coordinates[1], lon: f.Geometry.Coordinates[0], sentre))
            .Where(bydel => bydel is not null)
            .GroupBy(bydel => bydel!)
            .Select(g => new Bydelstelling(g.Key, g.Count()))
            .OrderByDescending(t => t.Antall)
            .ThenBy(t => t.Bydel, StringComparer.Ordinal)
            .ToList();
}
