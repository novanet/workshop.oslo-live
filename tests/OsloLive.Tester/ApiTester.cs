using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OsloLive.Historikk;
using OsloLive.Kart;

namespace OsloLive.Tester;

/// <summary>Tester at kart-API-et svarer slik frontenden forventer.</summary>
public class ApiTester(TestVert vert) : IClassFixture<TestVert>
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
    public async Task Lagoversikten_har_flylaget()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var fly = lag!.Single(l => l.Id == "fly");
        Assert.Equal("Flytrafikk", fly.Navn);
        Assert.False(string.IsNullOrWhiteSpace(fly.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(fly.Ikon));
    }

    [Fact]
    public async Task Svikt_i_flykilden_gir_502_bare_for_flylaget()
    {
        // Bytt ut flylagets HttpClient med en som alltid feiler, slik kilden gjør når den er nede.
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient("fly").ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var fly = await klient.GetAsync("/api/lag/fly");
        var lag = await klient.GetAsync("/api/lag");
        var helse = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.BadGateway, fly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lag.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helse.StatusCode);
    }

    [Fact]
    public async Task Ugyldig_tid_gir_400()
    {
        var svar = await Klient.GetAsync("/api/lag/luftkvalitet?tid=ikke-en-tid");

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Tid_uten_lagrede_bilder_gir_tom_featurecollection()
    {
        var svar = await Klient.GetAsync("/api/lag/fly?tid=2026-09-18T08:00:00Z");
        var lag = JsonDocument.Parse(await svar.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Equal("FeatureCollection", lag.GetProperty("type").GetString());
        Assert.Empty(lag.GetProperty("features").EnumerateArray());
    }

    [Fact]
    public async Task Tid_gir_bildet_som_er_lagret_naermest()
    {
        var tid = DateTimeOffset.Parse("2026-09-18T08:00:00Z");
        var punkt = Geo.Lag("stasjon-a", 59.9139, 10.7522, "Stasjon A", "Test");
        var bilder = vert.Services.GetRequiredService<Bildelager>();
        await bilder.Lagre("luftkvalitet", Geo.Samle([punkt]), tid.AddMinutes(-30));

        var svar = await Klient.GetAsync("/api/lag/luftkvalitet?tid=" + Uri.EscapeDataString(tid.ToString("O")));
        var lag = JsonDocument.Parse(await svar.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Single(lag.GetProperty("features").EnumerateArray());
    }

    [Fact]
    public async Task Ukjent_lag_med_tid_gir_404()
    {
        var svar = await Klient.GetAsync("/api/lag/finnes-ikke?tid=2026-09-18T08:00:00Z");

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    private sealed class SviktHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            throw new HttpRequestException("Kilden er nede.");
    }

    private sealed record Lagoppforing(string Id, string Navn, string Beskrivelse, string Ikon);
}
