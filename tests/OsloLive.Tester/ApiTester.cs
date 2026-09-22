using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
    public async Task Ukjent_lag_gir_404()
    {
        var svar = await Klient.GetAsync("/api/lag/finnes-ikke");

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Kameraknappene_ligger_i_kartets_kontrollstabel()
    {
        var side = await Klient.GetStringAsync("/");

        Assert.Contains("class=\"kamera maplibregl-ctrl\"", side);
        Assert.Matches(@"addControl\([^;]*kameraboks[^;]*'bottom-right'\)", side);
    }

    [Fact]
    public async Task Kameraknappene_er_ikke_fritt_plassert_over_kartet()
    {
        var side = await Klient.GetStringAsync("/");

        var regel = Regex.Match(side, @"\.kamera\s*\{[^}]*\}");

        Assert.True(regel.Success, "Fant ingen CSS-regel for .kamera i index.html");
        Assert.DoesNotMatch(@"position\s*:\s*(absolute|fixed)", regel.Value);
    }

    private sealed record Lagoppforing(string Id, string Navn, string Beskrivelse, string Ikon);
}
