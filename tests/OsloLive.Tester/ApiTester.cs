using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public async Task Lagoversikten_har_arter()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var arter = lag!.Single(l => l.Id == "arter");
        Assert.Equal("Artsobservasjoner", arter.Navn);
        Assert.False(string.IsNullOrWhiteSpace(arter.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(arter.Ikon));
    }

    [Fact]
    public async Task Lagoversikten_har_hendelser_med_navn_beskrivelse_og_ikon()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var hendelser = lag!.Single(l => l.Id == "hendelser");
        Assert.Equal("Hendelser", hendelser.Navn);
        Assert.False(string.IsNullOrWhiteSpace(hendelser.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(hendelser.Ikon));
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
    public async Task Lagoversikten_har_idrettsanlegg()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var anlegg = lag!.Single(l => l.Id == "idrettsanlegg");
        Assert.Equal("Idrettsanlegg", anlegg.Navn);
        Assert.False(string.IsNullOrWhiteSpace(anlegg.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(anlegg.Ikon));
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
    public async Task Lagoversikten_har_holdeplasser()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var holdeplasser = lag!.Single(l => l.Id == "holdeplasser");
        Assert.Equal("Holdeplasser", holdeplasser.Navn);
        Assert.False(string.IsNullOrWhiteSpace(holdeplasser.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(holdeplasser.Ikon));
    }
    
    [Fact]
    public async Task Lagoversikten_har_skip()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var skip = lag!.Single(l => l.Id == "skip");
        Assert.Equal("Skipstrafikk", skip.Navn);
        Assert.False(string.IsNullOrWhiteSpace(skip.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(skip.Ikon));
    }

    [Fact]
    public async Task Lagoversikten_har_vannmaalere()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var vannmaalere = lag!.Single(l => l.Id == "vannmaalere");
        Assert.Equal("Vannmålere", vannmaalere.Navn);
        Assert.False(string.IsNullOrWhiteSpace(vannmaalere.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(vannmaalere.Ikon));
    }

    [Fact]
    public async Task Lagoversikten_har_skolelaget()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var skoler = lag!.Single(l => l.Id == "skoler");
        Assert.Equal("Skoler", skoler.Navn);
        Assert.False(string.IsNullOrWhiteSpace(skoler.Beskrivelse));
        Assert.EndsWith(".", skoler.Beskrivelse);
        Assert.Equal(1, skoler.Beskrivelse!.Count(t => t == '.'));
        Assert.False(string.IsNullOrWhiteSpace(skoler.Ikon));
    }
    
    public async Task Lagoversikten_har_kaier()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var kaier = lag!.Single(l => l.Id == "kaier");
        Assert.Equal("Kaier", kaier.Navn);
        Assert.False(string.IsNullOrWhiteSpace(kaier.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(kaier.Ikon));
    }

    [Fact]
    public async Task Lagoversikten_har_vaerstasjoner()
    {
        var lag = await Klient.GetFromJsonAsync<List<Lagoppforing>>("/api/lag");

        var vaerstasjoner = lag!.Single(l => l.Id == "vaerstasjoner");
        Assert.Equal("Værstasjoner", vaerstasjoner.Navn);
        Assert.False(string.IsNullOrWhiteSpace(vaerstasjoner.Beskrivelse));
        Assert.False(string.IsNullOrWhiteSpace(vaerstasjoner.Ikon));
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

    [Fact]
    public async Task Skiplaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "ais",
                "operation": "find_vessels_nearby",
                "parameters": {},
                "data": {
                    "fartoy": [
                        {
                            "vessel_id": 258219000,
                            "navn": "Tåkeheimen",
                            "kallesignal": "LCDK",
                            "imo": 9481207,
                            "skipstype": 60,
                            "lat": 59.905,
                            "lon": 10.72,
                            "fart_knop": 12.3,
                            "kurs": 112.3,
                            "destinasjon": "NESODDTANGEN",
                            "sist_oppdatert": "2026-09-23T09:38:37+00:00",
                            "avstand_km": 1.01
                        }
                    ],
                    "radius_km": 20
                }
            }
            """;

        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => new FastSvarHandler(svar))));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/skip");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.72, 59.905], lag.Features[0].Geometry.Coordinates);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }
    
    [Fact]
    public async Task Holdeplasslaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "entur",
                "operation": "search_stops",
                "parameters": {},
                "data": {
                    "returned": 1,
                    "stops": [
                        {
                            "id": "NSR:StopPlace:59872",
                            "name": "Oslo S",
                            "label": "Oslo S, Oslo",
                            "category": ["onstreetBus", "railStation"],
                            "municipality": "Oslo",
                            "county": "Oslo",
                            "lat": 59.910357,
                            "lon": 10.753051
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
            var respons = await klient.GetAsync("/api/lag/holdeplasser");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.753051, 59.910357], lag.Features[0].Geometry.Coordinates);
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
    public async Task Vannmaalerlaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "nve",
                "operation": "find_hydro_stations",
                "parameters": {},
                "data": {
                    "count": 1,
                    "returned": 1,
                    "stations": [
                        {
                            "station_id": "6.38.0",
                            "station_name": "Akerselva v/Elvebakken",
                            "river": "Nordmarkvassdraget",
                            "municipality": "Oslo",
                            "county": "Oslo",
                            "latitude": 59.91945,
                            "longitude": 10.75348,
                            "masl": 4,
                            "latest_observation_at": "2026-09-23T11:00:00Z",
                            "series": [
                                {"parameter":1000,"name":"Vannstand","unit":"m","from":null,"to":null,"latest_data_at":"2026-09-23T11:00:00Z","resolutions":[0,60,1440]}
                            ],
                            "distance_km": 1.1
                        }
                    ],
                    "limit": 100,
                    "offset": 0,
                    "has_more_results": false,
                    "truncated": false
                }
            }
            """;

        var handler = new OpptakendeSvarHandler(svar);
        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => handler)));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/vannmaalere");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.75348, 59.91945], lag.Features[0].Geometry.Coordinates);
            Assert.Contains("nve/find_hydro_stations", handler.SisteAdresse!.ToString());
            Assert.Contains("has_recent_data=true", handler.SisteAdresse!.ToString());
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    [Fact]
    public async Task Kaierlaget_gir_featurecollection_uten_nett()
    {
        const string svar = """
            {
                "source": "kystdatahuset",
                "operation": "find_ports_nearby",
                "parameters": {},
                "data": {
                    "havner": [
                        {
                            "port_id": 2467069,
                            "navn": "Salt brygge FK",
                            "kode": "NOOSL",
                            "type": "Ferjekai",
                            "kommune": "Oslo",
                            "fylke": "Oslo",
                            "lat": 59.906647,
                            "lon": 10.747188,
                            "avstand_km": 0.4
                        }
                    ],
                    "radius_km": 20
                }
            }
            """;

        var handler = new OpptakendeSvarHandler(svar);
        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => handler)));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/kaier");
            var lag = await respons.Content.ReadFromJsonAsync<Kartlag>();

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag!.Type);
            Assert.NotEmpty(lag.Features);
            Assert.Equal([10.747188, 59.906647], lag.Features[0].Geometry.Coordinates);
            Assert.Contains("kystdatahuset/find_ports_nearby", handler.SisteAdresse!.ToString());
            Assert.Contains("radius_km=", handler.SisteAdresse!.ToString());
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    private sealed class OpptakendeSvarHandler(string svar) : HttpMessageHandler
    {
        public Uri? SisteAdresse { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
        {
            SisteAdresse = forespørsel.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(svar, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Skolelaget_gir_featurecollection_uten_nett()
    {
        var handler = new SkoleHandler();
        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => handler)));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/skoler");
            var lag = JsonDocument.Parse(await respons.Content.ReadAsStringAsync()).RootElement;

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            Assert.Equal("FeatureCollection", lag.GetProperty("type").GetString());

            var features = lag.GetProperty("features").EnumerateArray();
            var skole = features.Single(f => f.GetProperty("properties").GetProperty("id").GetString() == "skole:999000001");
            var barnehage = features.Single(f => f.GetProperty("properties").GetProperty("id").GetString() == "barnehage:999000002");
            var skoleKoordinater = skole.GetProperty("geometry").GetProperty("coordinates").EnumerateArray().Select(c => c.GetDouble()).ToArray();
            var barnehageKoordinater = barnehage.GetProperty("geometry").GetProperty("coordinates").EnumerateArray().Select(c => c.GetDouble()).ToArray();
            Assert.Equal([10.78188, 59.91101], skoleKoordinater);
            Assert.Equal([10.78188, 59.91101], barnehageKoordinater);

            Assert.DoesNotContain(handler.Adresser, url => url.Contains("geonorge"));
            Assert.True(handler.Adresser.Count(url => url.Contains("search_schools") || url.Contains("search_kindergartens")) <= 2);
            Assert.True(handler.Adresser.Count(url => url.Contains("get_unit")) <= 60);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    [Fact]
    public async Task Popupen_viser_type_og_eierform_for_skolelaget()
    {
        var handler = new SkoleHandler();
        using var vertUtenNett = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => handler)));
        var klient = vertUtenNett.CreateClient();

        Allemannsdata.TømMellomlager();
        try
        {
            var respons = await klient.GetAsync("/api/lag/skoler");
            var lag = JsonDocument.Parse(await respons.Content.ReadAsStringAsync()).RootElement;

            var skole = lag.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("properties").GetProperty("id").GetString() == "skole:999000001")
                .GetProperty("properties");
            var barnehage = lag.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("properties").GetProperty("id").GetString() == "barnehage:999000002")
                .GetProperty("properties");

            Assert.Equal("grunnskole", skole.GetProperty("type").GetString());
            Assert.Equal("kommunal", skole.GetProperty("eierform").GetString());
            Assert.Equal("barnehage", barnehage.GetProperty("type").GetString());
            Assert.Equal("privat", barnehage.GetProperty("eierform").GetString());

            var html = await Klient.GetStringAsync("/index.html");
            var match = System.Text.RegularExpressions.Regex.Match(
                html, @"function popup\(e\)\s*\{\s*const skjul = new Set\(\[([^\]]*)\]\)");
            Assert.True(match.Success, "Fant ikke skjul-settet i popup() i index.html.");

            var skjulteFelt = match.Groups[1].Value
                .Split(',')
                .Select(s => s.Trim().Trim('\'', '"'))
                .Where(s => s.Length > 0)
                .ToHashSet();

            Assert.Equal(new HashSet<string> { "id", "navn", "kilde" }, skjulteFelt);
            Assert.DoesNotContain("type", skjulteFelt);
            Assert.DoesNotContain("eierform", skjulteFelt);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    /// <summary>
    /// Ruter på hvilken operasjon adressen ber om: skolesøk, barnehagesøk eller enhetsoppslag.
    /// Registrerer hver kalte adresse, slik at testen kan telle kall og sjekke at ingen går til Kartverket.
    /// </summary>
    private sealed class SkoleHandler : HttpMessageHandler
    {
        public ConcurrentBag<string> Adresser { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
        {
            var url = forespørsel.RequestUri!.ToString();
            Adresser.Add(url);

            var kropp = url switch
            {
                var u when u.Contains("search_schools") => """
                    {
                        "source": "utdanning",
                        "operation": "search_schools",
                        "parameters": {},
                        "data": {
                            "total": 1,
                            "returned": 1,
                            "units": [
                                {
                                    "organization_id": "999000001",
                                    "Navn": "Testskolen",
                                    "ErAktiv": true,
                                    "ErGrunnskole": true,
                                    "ErVideregaaendeSkole": false,
                                    "ErPrivatskole": false,
                                    "ErOffentligSkole": true
                                }
                            ]
                        }
                    }
                    """,
                var u when u.Contains("search_kindergartens") => """
                    {
                        "source": "utdanning",
                        "operation": "search_kindergartens",
                        "parameters": {},
                        "data": {
                            "total": 1,
                            "returned": 1,
                            "units": [
                                {
                                    "organization_id": "999000002",
                                    "Navn": "Testbarnehagen",
                                    "ErAktiv": true,
                                    "ErBarnehage": true,
                                    "ErOffentligBarnehage": false,
                                    "ErPrivatBarnehage": true
                                }
                            ]
                        }
                    }
                    """,
                _ => """
                    {
                        "source": "utdanning",
                        "operation": "get_unit",
                        "parameters": {},
                        "data": {
                            "ErAktiv": true,
                            "Koordinat": {"Lengdegrad": 10.78188, "Breddegrad": 59.91101, "Zoom": 15, "GeoKilde": "GeoNorge"},
                            "Beliggenhetsadresse": {"Adresse": "Testveien 1", "Postnr": "0655", "Poststed": "OSLO"}
                        }
                    }
                    """,
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(kropp, Encoding.UTF8, "application/json"),
            });
        }
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

        var vannstand = await klient.GetAsync("/api/vannstand");
        var lag = await klient.GetAsync("/api/lag");
        var helse = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.BadGateway, vannstand.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lag.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helse.StatusCode);
    }

    [Fact]
    public async Task Testverten_kjoerer_ingen_bakgrunnsjobb_og_lagrer_bare_i_sin_egen_mappe()
    {
        // Øyeblikksjobb og helsesjekken er fjernet fra testverten, og Historikk:Mappe peker på
        // en midlertidig mappe som slettes etter testene. Ingen filer havner i App_Data/historikk.
        // Testserveren har sin egen vertstjeneste; ingen av appens bakgrunnsjobber skal være der.
        Assert.DoesNotContain(vert.Services.GetServices<IHostedService>(), t => t.GetType().Namespace?.StartsWith("OsloLive") == true);

        var lager = vert.Services.GetRequiredService<Bildelager>();
        await lager.Lagre("testlag", Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test")]), DateTimeOffset.UtcNow);

        Assert.NotEmpty(Directory.GetFiles(Path.Combine(vert.Mappe, "testlag"), "*.json"));
    }

    [Fact]
    public async Task Historikk_for_ukjent_lag_gir_404()
    {
        var svar = await Klient.GetAsync("/api/lag/finnes-ikke/historikk");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
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
    public async Task Historikk_for_lag_uten_bilder_gir_tom_liste()
    {
        var svar = await Klient.GetAsync("/api/lag/luftkvalitet/historikk");
        var bilder = await svar.Content.ReadFromJsonAsync<List<Bilde>>();

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.NotNull(bilder);
        Assert.Empty(bilder);
    }

    [Fact]
    public async Task Historikk_viser_lagrede_bilder_med_tidspunkt_og_antall()
    {
        var lager = vert.Services.GetRequiredService<Bildelager>();
        var punkt = Geo.Lag("a", 59.91, 10.75, "A", "Test");
        await lager.Lagre("fly", Geo.Samle([punkt]), DateTimeOffset.UtcNow.AddHours(-1));

        var svar = await Klient.GetAsync("/api/lag/fly/historikk");
        var bilder = await svar.Content.ReadFromJsonAsync<List<Bilde>>();

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.NotNull(bilder);
        var bilde = Assert.Single(bilder);
        Assert.Equal(1, bilde.Antall);
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
        Assert.True(innhold.TryGetProperty("naa", out var naa));
        Assert.Equal(JsonValueKind.Number, naa.ValueKind);
        Assert.True(innhold.TryGetProperty("billigst", out var billigst));
        Assert.Equal(JsonValueKind.Number, billigst.GetProperty("pris").ValueKind);
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

    [Fact]
    public async Task Flykilden_som_svarer_403_gir_feil_kilden_svarte_403()
    {
        using var vertMedForbud = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient("fly").ConfigurePrimaryHttpMessageHandler(() => new StatuskodeHandler(HttpStatusCode.Forbidden))));
        var klient = vertMedForbud.CreateClient();

        var svar = await klient.GetAsync("/api/lag/fly");
        var kropp = await svar.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadGateway, svar.StatusCode);
        Assert.Equal("Kilden svarte 403.", kropp.GetProperty("feil").GetString());
    }

    private sealed class StatuskodeHandler(HttpStatusCode kode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            Task.FromResult(new HttpResponseMessage(kode));
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
