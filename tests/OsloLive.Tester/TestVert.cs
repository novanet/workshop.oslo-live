using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace OsloLive.Tester;

/// <summary>
/// Testverten for API-testene. Fjerner bakgrunnstjenestene (øyeblikksjobben og
/// kildehelsesjekken) og peker <c>Historikk:Mappe</c> på en egen midlertidig mappe,
/// slik at testene verken gjør nettverkskall i bakgrunnen eller skriver i den
/// ekte historikkmappa. <see cref="Bildelager"/> beholdes, så testene kan lagre bilder selv.
/// </summary>
public sealed class TestVert : WebApplicationFactory<Program>
{
    public string Mappe { get; } = Path.Combine(Path.GetTempPath(), "oslolive-test-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Historikk:Jobb", "false");
        builder.UseSetting("Historikk:Mappe", Mappe);
        builder.ConfigureServices(tjenester => tjenester.RemoveAll<IHostedService>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (Directory.Exists(Mappe))
        {
            Directory.Delete(Mappe, recursive: true);
        }
    }
}
