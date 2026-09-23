using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace OsloLive.Tester;

/// <summary>
/// Test-vert uten den kjørende bakgrunnssjekken av kildehelse, slik at
/// testene ikke gjør nettverkskall mot de virkelige kildene.
/// </summary>
public sealed class VertUtenBakgrunnssjekk : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(tjenester => tjenester.RemoveAll<IHostedService>());
}
