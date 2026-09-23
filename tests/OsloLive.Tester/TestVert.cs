using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OsloLive.Tester;

/// <summary>
/// Testverten for hele appen. Skrur av historikkjobben og peker
/// historikklageret til en midlertidig mappe, slik at testene aldri gjør
/// nettverkskall i bakgrunnen eller skriver filer utenfor sin egen mappe.
/// </summary>
public sealed class TestVert : WebApplicationFactory<Program>
{
    public string Mappe { get; } = Path.Combine(Path.GetTempPath(), "oslolive-test-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Historikk:Aktiv", "false");
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
