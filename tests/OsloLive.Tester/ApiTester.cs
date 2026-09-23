using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OsloLive.Kart;

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
    public async Task Stroemprisen_har_naa_billigst_dyrest_og_timer()
    {
        Allemannsdata.TømMellomlager();
        var iDag = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Stroempris.Oslo).ToString("yyyy-MM-dd");
        var prisrader = Enumerable.Range(0, 24)
            .Select(t => new { time_start = $"{iDag}T{t:D2}:00:00+02:00", time_end = $"{iDag}T{(t + 1) % 24:D2}:00:00+02:00", NOK_per_kWh = 1.0 });
        var svarFraKilden = JsonSerializer.Serialize(new { source = "strompris", operation = "get_prices", parameters = new { }, data = new { date = iDag, area = "NO1", prices = prisrader } });

        using var vertMedStub = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(svarFraKilden))));
        var klient = vertMedStub.CreateClient();

        var svar = await klient.GetAsync("/api/stroempris");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var innhold = await svar.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(innhold.TryGetProperty("naa", out _));
        Assert.True(innhold.TryGetProperty("billigst", out _));
        Assert.True(innhold.TryGetProperty("dyrest", out _));
        Assert.Equal(24, innhold.GetProperty("timer").GetArrayLength());
        Allemannsdata.TømMellomlager();
    }

    [Fact]
    public async Task Svikt_i_stroemkilden_gir_502_resten_av_api_et_svarer_200()
    {
        Allemannsdata.TømMellomlager();
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var stroem = await klient.GetAsync("/api/stroempris");
        var lag = await klient.GetAsync("/api/lag");
        var helse = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.BadGateway, stroem.StatusCode);
        var feilSvar = await stroem.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(feilSvar.GetProperty("feil").GetString()));
        Assert.Equal(HttpStatusCode.OK, lag.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helse.StatusCode);
        Allemannsdata.TømMellomlager();
    }

    private sealed class SviktHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            throw new HttpRequestException("Kilden er nede.");
    }

    private sealed class StubHandler(string svar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(svar, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private sealed record Lagoppforing(string Id, string Navn, string Beskrivelse, string Ikon);
}
