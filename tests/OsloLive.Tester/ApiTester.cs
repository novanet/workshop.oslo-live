using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
    public async Task Ukjent_lag_gir_404_ogsaa_for_bydeler()
    {
        var svar = await Klient.GetAsync("/api/lag/finnes-ikke/bydeler");

        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Svikt_i_kilden_gir_502_for_bydeler()
    {
        Allemannsdata.TømMellomlager();

        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var bydeler = await klient.GetAsync("/api/lag/luftkvalitet/bydeler");
        var helse = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.BadGateway, bydeler.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helse.StatusCode);
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
    public async Task Lagoversikten_har_spisesteder()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var spisesteder = lag!.Single(l => l.Id == "spisesteder");
        Assert.Equal("Spisesteder", spisesteder.Navn);
        Assert.False(string.IsNullOrWhiteSpace(spisesteder.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(spisesteder.Ikon));
    }

    [Fact]
    public async Task Lagoversikten_har_arbeidsplasser()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var arbeidsplasser = lag!.Single(l => l.Id == "arbeidsplasser");
        Assert.Equal("Store arbeidsplasser", arbeidsplasser.Navn);
        Assert.False(string.IsNullOrWhiteSpace(arbeidsplasser.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(arbeidsplasser.Ikon));
    }

    [Fact]
    public async Task Arbeidsplasserlaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "firmafakta",
                "operation": "finn_selskaper_i_omrade",
                "parameters": {},
                "data": {
                    "summary": {},
                    "pagination": {},
                    "companies": [
                        {
                            "organization_id": "944384448",
                            "navn": "STIFTELSEN KIRKENS BYMISJON",
                            "antallAnsatte": 2567,
                            "naeringskode1": { "kode": "94.910", "beskrivelse": "Aktiviteter i religiøse organisasjoner" },
                            "distance_km": 0.2,
                            "coordinates": { "latitude": 59.9099, "longitude": 10.7464 }
                        }
                    ]
                }
            }
            """;

        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new FastSvarHandler(svar))));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/arbeidsplasser");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.7464, 59.9099], lag.Features[0].Geometry.Coordinates);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    [Fact]
    public async Task Spisestederlaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "poi_norge",
                "operation": "search_poi",
                "parameters": {},
                "data": {
                    "matched": 1,
                    "offset": 0,
                    "count": 1,
                    "items": [
                        {
                            "id": 5315431323,
                            "lat": 59.911,
                            "lon": 10.745,
                            "type": "amenity",
                            "category": "restaurant",
                            "name": "Olivia",
                            "distance_km": 0.1
                        }
                    ],
                    "limit": 100,
                    "returned": 1,
                    "has_more_results": false,
                    "truncated": false
                }
            }
            """;

        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new FastSvarHandler(svar))));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/spisesteder");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.745, 59.911], lag.Features[0].Geometry.Coordinates);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
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

    [Fact]
    public async Task Statistikken_svarer_200()
    {
        var svar = await Klient.GetAsync("/api/statistikk");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
    }

    [Fact]
    public async Task Statistikken_har_ett_element_per_lag()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");
        var statistikk = await Klient.GetFromJsonAsync<List<Statistikkoppforing>>("/api/statistikk");

        Assert.Equal(
            lag!.Select(l => l.Id).OrderBy(id => id),
            statistikk!.Select(s => s.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task Lag_som_feiler_gir_feiler_true_og_antall_null_i_statistikken()
    {
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient("fly").ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        await klient.GetAsync("/api/lag/fly");
        var statistikk = await klient.GetFromJsonAsync<List<Statistikkoppforing>>("/api/statistikk");

        var fly = statistikk!.Single(s => s.Id == "fly");
        Assert.True(fly.Feiler);
        Assert.Null(fly.Antall);
    }

    [Fact]
    public async Task Statistikken_henter_ikke_lagene()
    {
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient("fly").ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var statistikk = await klient.GetFromJsonAsync<List<Statistikkoppforing>>("/api/statistikk");

        var fly = statistikk!.Single(s => s.Id == "fly");
        Assert.False(fly.Feiler);
        Assert.Null(fly.Hentet);
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

    [Fact]
    public async Task Ugyldig_tid_gir_400_ogsaa_for_bydeler()
    {
        var svar = await Klient.GetAsync("/api/lag/luftkvalitet/bydeler?tid=ikke-en-tid");

        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Bydeler_med_tid_teller_bildet_som_er_lagret_naermest()
    {
        Allemannsdata.TømMellomlager();
        var tid = DateTimeOffset.Parse("2026-09-18T08:00:00Z");
        var punkt = Geo.Lag("strand-a", 59.9139, 10.7522, "Strand A", "Test");
        var bilder = vert.Services.GetRequiredService<Bildelager>();
        await bilder.Lagre("badetemperatur", Geo.Samle([punkt]), tid.AddMinutes(-30));

        // Bydelssentrene kommer fra Allemannsdata; her ett senter nær punktet, uten nettverk.
        var sentre = JsonSerializer.Serialize(new
        {
            source = "geonorge",
            operation = "search_place_name",
            parameters = new { },
            data = new
            {
                places = new[]
                {
                    new { place_id = 1L, status = "aktiv", lat = 59.91725, lon = 10.70, names = new[] { new { name = "Frogner", status = "hovednavn" } } },
                },
            },
        });
        using var vertMedStub = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(sentre))));
        var klient = vertMedStub.CreateClient();

        var svar = await klient.GetAsync("/api/lag/badetemperatur/bydeler?tid=" + Uri.EscapeDataString(tid.ToString("O")));
        var tellinger = await svar.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var eneste = Assert.Single(tellinger.EnumerateArray());
        Assert.Equal("Frogner", eneste.GetProperty("bydel").GetString());
        Assert.Equal(1, eneste.GetProperty("antall").GetInt32());
        Allemannsdata.TømMellomlager();
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

    private sealed record Statistikkoppforing(string Id, string Navn, int? Antall, DateTimeOffset? Eldste, DateTimeOffset? Nyeste, DateTimeOffset? Hentet, bool Feiler);
}
