using OsloLive.Kart;

namespace OsloLive.Historikk;

/// <summary>
/// Tar et øyeblikksbilde av hvert registrerte lag én gang i timen, slik at
/// tidslinjen på kartet har noe å vise tilbake i tid. Se <see cref="Bildelager"/>.
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ett lag som svikter skal ikke stoppe bilder av de andre.
                logg.LogWarning(ex, "Klarte ikke å ta bilde av {Id}", l.Id);
            }
        }

        bilder.SlettEldreEnn(nå - Bildelager.Oppbevaring);
    }
}
