using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OsloLive.Tester;

/// <summary>
/// Testverten for API-testene. Skrur av øyeblikksjobben og peker
/// <c>Historikk:Mappe</c> på en egen midlertidig mappe, slik at testene
/// verken gjør nettverkskall i bakgrunnen eller skriver i den ekte historikkmappa.
/// </summary>
public sealed class TestVert : WebApplicationFactory<Program>
{
    public string Mappe { get; } = Path.Combine(Path.GetTempPath(), "oslolive-test-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Historikk:Jobb", "false");
        builder.UseSetting("Historikk:Mappe", Mappe);
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
