using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    public async Task Vannstand_svarer_200_med_naa_neste_og_maalt()
    {
        // Mellomlageret er statisk og delt mellom tester, så det må tømmes før hvert kall.
        Allemannsdata.TømMellomlager();
        var nå = DateTimeOffset.UtcNow;
        var naaJson = $$"""[{"time":"{{nå.AddMinutes(-20).ToString("o", CultureInfo.InvariantCulture)}}","tide_cm":10.0,"sea_level_cm":12.5,"surge_cm":2.5}]""";
        var tabellJson = $$"""[{"time":"{{nå.AddHours(3).ToString("o", CultureInfo.InvariantCulture)}}","height_cm":24.0,"kind":"high"},{"time":"{{nå.AddHours(9).ToString("o", CultureInfo.InvariantCulture)}}","height_cm":-12.0,"kind":"low"}]""";

        using var vertMedData = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new VannstandHandler(naaJson, tabellJson))));
        var klient = vertMedData.CreateClient();

        var svar = await klient.GetAsync("/api/vannstand");
        var kropp = await svar.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Equal(12.5, kropp.GetProperty("naa").GetDouble());
        Assert.True(DateTimeOffset.TryParse(kropp.GetProperty("maalt").GetString(), out _));
        Assert.Equal("høyvann", kropp.GetProperty("neste").GetProperty("type").GetString());
        Assert.True(DateTimeOffset.TryParse(kropp.GetProperty("neste").GetProperty("tidspunkt").GetString(), out _));
        Assert.Equal(24.0, kropp.GetProperty("neste").GetProperty("verdi").GetDouble());
    }

    [Fact]
    public async Task Svikt_i_vannstandskilden_gir_502_med_feil()
    {
        Allemannsdata.TømMellomlager();
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var svar = await klient.GetAsync("/api/vannstand");
        var kropp = await svar.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadGateway, svar.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(kropp.GetProperty("feil").GetString()));
    }

    [Fact]
    public async Task Svikt_i_vannstandskilden_rammer_ikke_lagoversikten()
    {
        Allemannsdata.TømMellomlager();
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var lag = await klient.GetAsync("/api/lag");
        var helse = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.OK, lag.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helse.StatusCode);
    }

    private sealed class SviktHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            throw new HttpRequestException("Kilden er nede.");
    }

    /// <summary>Svarer med tidsserien til «naa» eller tabellen til «neste», avhengig av hvilken operasjon adressen ber om.</summary>
    private sealed class VannstandHandler(string naaJson, string tabellJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
        {
            var erTabell = forespørsel.RequestUri!.ToString().Contains("get_tide_table");
            var kropp = Innpakket(erTabell ? "get_tide_table" : "get_tide_forecast", erTabell ? tabellJson : naaJson);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(kropp, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string Innpakket(string operasjon, string data) =>
        $$"""{"source":"weather","operation":"{{operasjon}}","parameters":{},"data":{{data}}}""";

    private sealed record Lagoppforing(string Id, string Navn, string Beskrivelse, string Ikon);
}
