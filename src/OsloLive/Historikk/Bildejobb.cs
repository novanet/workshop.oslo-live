using OsloLive.Kart;

namespace OsloLive.Historikk;

/// <summary>
/// Tar ett øyeblikksbilde av hvert registrerte lag i timen, slik at panelet
/// kan vise hvordan antall punkter har utviklet seg siste døgn. Slått av som
/// standard: bare "Historikk:Aktiv" i oppsettet skrur den på, slik at den
/// aldri kjører ved et uhell i testene.
/// </summary>
public sealed class Bildejobb(IEnumerable<ILag> lagene, Bildelager lager, IConfiguration oppsett, ILogger<Bildejobb> logg) : BackgroundService
{
    public static readonly TimeSpan Intervall = TimeSpan.FromHours(1);

    /// <summary>
    /// Hvor nylig et bilde må være for at vi skal droppe å ta et nytt. Litt
    /// under en time, slik at en omstart rett etter forrige bilde ikke lager
    /// et unødvendig duplikat når mappa ligger på en varig disk.
    /// </summary>
    private static readonly TimeSpan MinsteMellomrom = TimeSpan.FromMinutes(55);

    protected override async Task ExecuteAsync(CancellationToken stopp)
    {
        if (!oppsett.GetValue("Historikk:Aktiv", false))
        {
            return;
        }

        try
        {
            await TaBilder(stopp);

            using var klokke = new PeriodicTimer(Intervall);
            while (await klokke.WaitForNextTickAsync(stopp))
            {
                await TaBilder(stopp);
            }
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested)
        {
        }
    }

    private async Task TaBilder(CancellationToken stopp)
    {
        var nå = DateTimeOffset.UtcNow;

        foreach (var lag in lagene)
        {
            var siste = lager.Siste(lag.Id);
            if (siste is not null && nå - siste < MinsteMellomrom)
            {
                continue;
            }

            try
            {
                lager.Lagre(lag.Id, await lag.Hent(stopp), nå);
            }
            catch (OperationCanceledException) when (stopp.IsCancellationRequested)
            {
                // Tjenesten stoppes: la det gå videre til ExecuteAsync.
                throw;
            }
            catch (Exception ex)
            {
                // Også tidsavbrudd fra en kilde (TaskCanceledException fra HttpClient
                // sin egen timeout) havner her, slik at ett tregt lag ikke stopper jobben.
                logg.LogWarning(ex, "Klarte ikke å ta bilde av laget {Id}", lag.Id);
            }
        }

        try
        {
            lager.Rydd(nå);
        }
        catch (Exception ex)
        {
            logg.LogWarning(ex, "Klarte ikke å rydde bort gamle historikkbilder");
        }
    }
}
