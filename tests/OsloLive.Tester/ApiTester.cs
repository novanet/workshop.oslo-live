using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OsloLive.Tester;

/// <summary>Tester at kart-API-et svarer slik frontenden forventer.</summary>
public class ApiTester(WebApplicationFactory<Program> vert) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Helsesjekken_svarer()
    {
        var svar = await Klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
    }

    [Fact]
    public async Task Lagoversikten_har_minst_ett_lag()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        Assert.NotNull(lag);
        Assert.NotEmpty(lag);
    }

    [Fact]
    public async Task Alle_lag_har_id_og_navn()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        Assert.All(lag!, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l.Id));
            Assert.False(string.IsNullOrWhiteSpace(l.Navn));
        });
    }

    [Fact]
    public async Task Lagoversikten_har_arter()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var arter = lag!.Single(l => l.Id == "arter");
        Assert.Equal("Artsobservasjoner", arter.Navn);
        Assert.False(string.IsNullOrWhiteSpace(arter.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(arter.Ikon));
    }

    [Fact]
    public async Task Ukjent_lag_gir_404()
    {
        var svar = await Klient.GetAsync("/api/lag/finnes-ikke");

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    private sealed record Lagoppforing(string Id, string Navn, string Beskrivelse, string Ikon);
}
