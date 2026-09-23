using System.Collections.Concurrent;
using System.Globalization;
using OsloLive.Kart;

namespace OsloLive;

/// <summary>Siste kjente tilstand for ett lag.</summary>
public sealed record Lagstatus(int? Antall, DateTimeOffset? Eldste, DateTimeOffset? Nyeste, DateTimeOffset? Hentet, bool Feiler);

/// <summary>
/// Siste kjente tilstand per lag, fylt av /api/lag/{id}. /api/statistikk leser
/// herfra og henter aldri lagene selv.
/// </summary>
public sealed class Lagstatistikk
{
    /// <summary>Egenskaper i et kartpunkt som regnes som tidsstempel.</summary>
    public static readonly string[] Tidsnøkler = ["målt"];

    private readonly ConcurrentDictionary<string, Lagstatus> status = new(StringComparer.OrdinalIgnoreCase);

    public void Vellykket(string id, Kartlag lag, DateTimeOffset nå)
    {
        var (eldste, nyeste) = Tidsrom(lag);
        status[id] = new Lagstatus(lag.Features.Count, eldste, nyeste, nå, false);
    }

    public void Feilet(string id) =>
        status.AddOrUpdate(
            id,
            _ => new Lagstatus(null, null, null, null, true),
            (_, forrige) => forrige with { Antall = null, Eldste = null, Nyeste = null, Feiler = true });

    public Lagstatus Hent(string id) =>
        status.TryGetValue(id, out var s) ? s : new Lagstatus(null, null, null, null, false);

    /// <summary>Finner eldste og nyeste tidsstempel blant punktene i laget, eller (null, null) hvis ingen har noe.</summary>
    public static (DateTimeOffset? Eldste, DateTimeOffset? Nyeste) Tidsrom(Kartlag lag)
    {
        var tidspunkter = new List<DateTimeOffset>();

        foreach (var punkt in lag.Features)
        {
            foreach (var nøkkel in Tidsnøkler)
            {
                if (!punkt.Properties.TryGetValue(nøkkel, out var verdi) || verdi is null)
                {
                    continue;
                }

                if (verdi is DateTimeOffset tidspunkt)
                {
                    tidspunkter.Add(tidspunkt);
                }
                else if (verdi is string tekst
                    && DateTimeOffset.TryParse(tekst, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var tolket))
                {
                    tidspunkter.Add(tolket);
                }
            }
        }

        return tidspunkter.Count > 0
            ? (tidspunkter.Min(), tidspunkter.Max())
            : (null, null);
    }
}
