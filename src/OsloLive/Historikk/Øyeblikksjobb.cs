using OsloLive.Kart;

namespace OsloLive.Historikk;

/// <summary>
/// Tar et øyeblikksbilde av hvert registrerte lag én gang i timen, slik at
/// tidslinjen på kartet har noe å vise tilbake i tid. Se <see cref="Bildelager"/>.
///
/// Dette er den samme timesjobben som historikk-issuen (#25) beskriver: ett
/// JSON-bilde per lag per time på disk, valgt etter nærmeste tidspunkt. Jobben
/// er med her for at tidslinjen (#34) skal kunne merges uten å vente på #25;
/// når #25 sin jobb (<c>Bildejobb</c>) er inne, beholdes én av dem, og
/// <see cref="Bildelager"/> og <c>?tid=</c> er felles for begge.
/// </summary>
public sealed class Øyeblikksjobb(IEnumerable<ILag> lag, Bildelager bilder, IConfiguration konfig, ILogger<Øyeblikksjobb> logg) : BackgroundService
{
    public static readonly TimeSpan Intervall = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stopp)
    {
        // Lest her, ikke i Program.cs før Build(), slik at testvertens
        // UseSetting garantert slår gjennom.
        if (!konfig.GetValue("Historikk:Jobb", true))
        {
            return;
        }

        using var takt = new PeriodicTimer(Intervall);
        do
        {
            await TaBilder(stopp);
        }
        while (await takt.WaitForNextTickAsync(stopp));
    }

    private async Task TaBilder(CancellationToken stopp)
    {
        var nå = DateTimeOffset.UtcNow;

        foreach (var l in lag)
        {
            try
            {
                await bilder.Lagre(l.Id, await l.Hent(stopp), nå, stopp);
            }
            catch (Exception ex) when (!stopp.IsCancellationRequested)
            {
                // Ett lag som svikter skal ikke stoppe bilder av de andre. Det gjelder også
                // tidsavbrudd mot kilden (TaskCanceledException); bare når verten selv
                // stopper skal avbruddet slippe gjennom.
                logg.LogWarning(ex, "Klarte ikke å ta bilde av {Id}", l.Id);
            }
        }

        try
        {
            bilder.SlettEldreEnn(nå - Bildelager.Oppbevaring);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ryddingen skal ikke stoppe jobben; de gamle bildene forsøkes slettet igjen neste time.
            logg.LogWarning(ex, "Klarte ikke å slette gamle bilder");
        }
    }
}
