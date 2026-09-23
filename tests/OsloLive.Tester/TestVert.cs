using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace OsloLive.Tester;

/// <summary>
/// Testverten for hele appen. Fjerner bakgrunnstjenestene (helsesjekken og
/// historikkjobben) og peker historikklageret til en midlertidig mappe, slik
/// at testene aldri gjør nettverkskall i bakgrunnen eller skriver filer
/// utenfor sin egen mappe.
/// </summary>
public sealed class TestVert : WebApplicationFactory<Program>
{
    public string Mappe { get; } = Path.Combine(Path.GetTempPath(), "oslolive-test-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Historikk:Aktiv", "false");
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
