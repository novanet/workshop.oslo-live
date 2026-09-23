using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using OsloLive.Kart;

namespace OsloLive.Helse;

/// <summary>Innstillingene for bakgrunnssjekken, lest fra appsettings.json-seksjonen «Helse».</summary>
public sealed class HelseValg
{
    public int IntervallSekunder { get; set; } = 60;
}

/// <summary>Helsetilstanden til én kilde et lag bruker.</summary>
public sealed record Kildehelse(string Kilde, string Status, DateTimeOffset? SistSjekket, long? VarighetMs);

/// <summary>
/// Sjekker kilden til hvert lag i bakgrunnen med et fast intervall, og holder
/// siste kjente resultat i minnet. «/api/helse/kilder» leser bare dette
/// mellomlageret og venter aldri på kildene selv - det er poenget med å
/// skille «lever prosessen» (/api/helse) fra «virker tjenesten».
/// </summary>
public sealed class HelseSjekker(IEnumerable<ILag> lag, IOptions<HelseValg> valg, ILogger<HelseSjekker> logg) : BackgroundService
{
    private static readonly TimeSpan KildeTidsavbrudd = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, Kildehelse> tilstand =
        new(lag.ToDictionary(l => l.Id, l => new Kildehelse(l.Id, "ukjent", null, null)));

    /// <summary>Siste kjente tilstand for hver kilde, i rekkefølgen lagene er registrert.</summary>
    public IReadOnlyList<Kildehelse> Snapshot() => lag.Select(l => tilstand[l.Id]).ToList();

    /// <summary>«degradert» når minst én kilde er «feil», ellers «ok».</summary>
    public static string SamletStatus(IEnumerable<Kildehelse> kilder) =>
        kilder.Any(k => k.Status == "feil") ? "degradert" : "ok";

    protected override async Task ExecuteAsync(CancellationToken stopp)
    {
        while (!stopp.IsCancellationRequested)
        {
            try
            {
                await SjekkAlle(stopp);
                await Task.Delay(TimeSpan.FromSeconds(valg.Value.IntervallSekunder), stopp);
            }
            catch (OperationCanceledException)
            {
                // Appen stopper. Ingen flere sjekker.
            }
        }
    }

    private async Task SjekkAlle(CancellationToken stopp)
    {
        foreach (var l in lag)
        {
            tilstand[l.Id] = await Sjekk(l, stopp);
        }
    }

    private async Task<Kildehelse> Sjekk(ILag l, CancellationToken appStopp)
    {
        using var tidsavbrudd = CancellationTokenSource.CreateLinkedTokenSource(appStopp);
        tidsavbrudd.CancelAfter(KildeTidsavbrudd);

        var klokke = Stopwatch.StartNew();
        try
        {
            await l.Hent(tidsavbrudd.Token);
            return new Kildehelse(l.Id, "ok", DateTimeOffset.UtcNow, klokke.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            appStopp.ThrowIfCancellationRequested();
            logg.LogWarning(ex, "Helsesjekk for kilden {Kilde} feilet", l.Id);
            return new Kildehelse(l.Id, "feil", DateTimeOffset.UtcNow, klokke.ElapsedMilliseconds);
        }
    }
}
