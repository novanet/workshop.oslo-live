using System.Collections.Concurrent;

namespace OsloLive.Kart;

/// <summary>
/// Teller oppslagene i <see cref="Allemannsdata.Hent"/> per kilde: treff i
/// mellomlageret, bom som gikk til kilden, tiden kildene brukte og feil.
/// Trådsikker. Tallene nullstilles aldri mens prosessen lever.
/// </summary>
public sealed class Metrikker
{
    private readonly ConcurrentDictionary<string, Teller> tellere = new();

    /// <summary>Når tellingen startet.</summary>
    public DateTimeOffset Siden { get; } = DateTimeOffset.UtcNow;

    public sealed record KildeMetrikk(string Kilde, long Kall, long Treff, long Bom, double SnittMs, long Feil);

    public sealed record Svar(DateTimeOffset Siden, IReadOnlyList<KildeMetrikk> Kilder);

    /// <summary>Et oppslag som ble besvart fra mellomlageret.</summary>
    public void RegistrerTreff(string kilde) => Interlocked.Increment(ref Hent(kilde).Treff);

    /// <summary>Et oppslag som gikk til kilden, med tiden det tok og om det feilet.</summary>
    public void RegistrerBom(string kilde, TimeSpan tid, bool feilet)
    {
        var teller = Hent(kilde);
        Interlocked.Add(ref teller.TidTicks, tid.Ticks);
        if (feilet)
        {
            Interlocked.Increment(ref teller.Feil);
        }

        Interlocked.Increment(ref teller.Bom);
    }

    /// <summary>Et øyeblikksbilde av tallene, sortert på kildenavn.</summary>
    public Svar Les() => new(Siden, tellere
        .OrderBy(t => t.Key, StringComparer.Ordinal)
        .Select(t =>
        {
            var bom = Interlocked.Read(ref t.Value.Bom);
            var treff = Interlocked.Read(ref t.Value.Treff);
            var tid = TimeSpan.FromTicks(Interlocked.Read(ref t.Value.TidTicks));
            return new KildeMetrikk(
                t.Key,
                treff + bom,
                treff,
                bom,
                bom == 0 ? 0 : tid.TotalMilliseconds / bom,
                Interlocked.Read(ref t.Value.Feil));
        })
        .ToList());

    private Teller Hent(string kilde) => tellere.GetOrAdd(kilde, _ => new Teller());

    private sealed class Teller
    {
        public long Treff;
        public long Bom;
        public long TidTicks;
        public long Feil;
    }
}
