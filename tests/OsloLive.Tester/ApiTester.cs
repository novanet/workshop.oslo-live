using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OsloLive.Kart;

namespace OsloLive.Tester;

/// <summary>Tester at kart-API-et svarer slik frontenden forventer.</summary>
public class ApiTester(VertUtenBakgrunnssjekk vert) : IClassFixture<VertUtenBakgrunnssjekk>
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
    public async Task Lagoversikten_har_smilefjes()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var smilefjes = lag!.Single(l => l.Id == "smilefjes");
        Assert.Equal("Smilefjes", smilefjes.Navn);
        Assert.False(string.IsNullOrWhiteSpace(smilefjes.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(smilefjes.Ikon));
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
    public async Task Lagoversikten_har_badetemperaturlaget()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var badetemperatur = lag!.Single(l => l.Id == "badetemperatur");
        Assert.Equal("Badetemperatur", badetemperatur.Navn);
        Assert.False(string.IsNullOrWhiteSpace(badetemperatur.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(badetemperatur.Ikon));
    }

    [Fact]
    public async Task Lagoversikten_har_mobilitetslaget()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var mobilitet = lag!.Single(l => l.Id == "mobilitet");
        Assert.Equal("Delt mobilitet", mobilitet.Navn);
        Assert.False(string.IsNullOrWhiteSpace(mobilitet.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(mobilitet.Ikon));
    }

    [Fact]
    public async Task Mobilitetslaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "entur",
                "operation": "find_shared_mobility_nearby",
                "parameters": {},
                "data": {
                    "vehicles": [
                        {
                            "id": "YRY:Vehicle:ea325240",
                            "form_factor": "SCOOTER_STANDING",
                            "lat": 59.909607,
                            "lon": 10.749284,
                            "reserved": false,
                            "disabled": false,
                            "operator": "Ryde",
                            "system_id": "rydeoslo"
                        }
                    ],
                    "stations": []
                }
            }
            """;

        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new FastSvarHandler(svar))));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/mobilitet");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.749284, 59.909607], lag.Features[0].Geometry.Coordinates);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    private sealed class FastSvarHandler(string svar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(svar, Encoding.UTF8, "application/json"),
            });
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

    private sealed class SviktHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            throw new HttpRequestException("Kilden er nede.");
    }

    private sealed record Lagoppforing(string Id, string Navn, string Beskrivelse, string Ikon);
}
