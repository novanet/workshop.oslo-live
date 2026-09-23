using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class HjertestartereLagTester(TestVert vert) : IClassFixture<TestVert>
{
    private const string OverpassSvar = """
        {"elements":[
            {"type":"node","id":123456789,"lat":59.911,"lon":10.753,"tags":{"emergency":"defibrillator","name":"Oslo S","opening_hours":"24/7"}},
            {"type":"node","id":987654321,"lat":59.93,"lon":10.72,"tags":{"emergency":"defibrillator"}}
        ]}
        """;

    private static JsonElement Node(string json) => JsonDocument.Parse(json).RootElement;

    private static HjertestartereLag NyttLag(HttpMessageHandler handler) =>
        new(new FastKlientFabrikk(handler), new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Node_med_navn_blir_punkt_med_navn_og_detaljer()
    {
        var punkt = HjertestartereLag.TilPunkt(Node("""
            {"type":"node","id":123456789,"lat":59.911,"lon":10.753,"tags":{"name":"Oslo S","opening_hours":"24/7","defibrillator:location":"Ved inngangen"}}
            """));

        Assert.NotNull(punkt);
        Assert.Equal("123456789", punkt!.Properties["id"]);
        Assert.Equal("Oslo S", punkt.Properties["navn"]);
        Assert.Equal("OpenStreetMap", punkt.Properties["kilde"]);
        Assert.Equal("24/7", punkt.Properties["åpent"]);
        Assert.Equal("Ved inngangen", punkt.Properties["plassering"]);
        Assert.Equal("Point", punkt.Geometry.Type);
        Assert.Equal([10.753, 59.911], punkt.Geometry.Coordinates);
    }

    [Fact]
    public void Node_uten_navn_heter_hjertestarter()
    {
        var punkt = HjertestartereLag.TilPunkt(Node("""
            {"type":"node","id":42,"lat":59.93,"lon":10.72,"tags":{"emergency":"defibrillator"}}
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Hjertestarter", punkt!.Properties["navn"]);
        Assert.False(punkt.Properties.ContainsKey("åpent"));
        Assert.False(punkt.Properties.ContainsKey("plassering"));
    }

    [Fact]
    public void Node_uten_tagger_heter_hjertestarter()
    {
        var punkt = HjertestartereLag.TilPunkt(Node("""
            {"type":"node","id":42,"lat":59.93,"lon":10.72}
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Hjertestarter", punkt!.Properties["navn"]);
        Assert.False(punkt.Properties.ContainsKey("åpent"));
        Assert.False(punkt.Properties.ContainsKey("plassering"));
    }

    [Fact]
    public void Node_med_tagger_av_feil_type_utelater_feltene()
    {
        var punkt = HjertestartereLag.TilPunkt(Node("""
            {"type":"node","id":42,"lat":59.93,"lon":10.72,"tags":{"name":7,"opening_hours":24,"defibrillator:location":""}}
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Hjertestarter", punkt!.Properties["navn"]);
        Assert.False(punkt.Properties.ContainsKey("åpent"));
        Assert.False(punkt.Properties.ContainsKey("plassering"));
    }

    [Theory]
    [InlineData("""{"type":"node","lat":59.91,"lon":10.75}""")]
    [InlineData("""{"type":"node","id":1,"lon":10.75}""")]
    [InlineData("""{"type":"node","id":1,"lat":null,"lon":10.75}""")]
    [InlineData("""{"type":"node","id":"x","lat":59.91,"lon":10.75}""")]
    public void Node_uten_gyldig_posisjon_eller_id_blir_forkastet(string json)
    {
        Assert.Null(HjertestartereLag.TilPunkt(Node(json)));
    }

    [Fact]
    public void Node_utenfor_utsnittet_blir_forkastet()
    {
        var punkt = HjertestartereLag.TilPunkt(Node("""
            {"type":"node","id":1,"lat":60.5,"lon":10.75}
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Sporringen_bruker_utsnittet_med_punktum_som_desimaltegn()
    {
        var opprinnelig = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nb-NO");

            var spørring = HjertestartereLag.Spørring();

            Assert.Equal("[out:json][timeout:25];node[\"emergency\"=\"defibrillator\"](59.8,10.45,60.14,10.98);out;", spørring);
        }
        finally
        {
            CultureInfo.CurrentCulture = opprinnelig;
        }
    }

    [Fact]
    public async Task To_hentinger_gir_ett_kall_mot_overpass()
    {
        var handler = new OverpassHandler(HttpStatusCode.OK, OverpassSvar);
        var lag = NyttLag(handler);

        var første = await lag.Hent();
        var andre = await lag.Hent();

        Assert.Equal(2, første.Features.Count);
        Assert.Equal(2, andre.Features.Count);
        Assert.Equal(1, handler.Kall);
    }

    [Fact]
    public async Task Feil_fra_overpass_gir_ikke_nytt_kall_innen_en_time()
    {
        var handler = new OverpassHandler(HttpStatusCode.TooManyRequests, "");
        var lag = NyttLag(handler);

        var første = await Assert.ThrowsAsync<HttpRequestException>(() => lag.Hent());
        var andre = await Assert.ThrowsAsync<HttpRequestException>(() => lag.Hent());

        Assert.Equal(HttpStatusCode.TooManyRequests, første.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, andre.StatusCode);
        Assert.Equal(1, handler.Kall);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"elements":null}""")]
    [InlineData("[]")]
    public async Task Svar_uten_elements_gir_tomt_lag(string kropp)
    {
        var handler = new OverpassHandler(HttpStatusCode.OK, kropp);
        var lag = NyttLag(handler);

        var kartlag = await lag.Hent();

        Assert.Empty(kartlag.Features);
    }

    [Fact]
    public async Task Lagoversikten_har_hjertestartere()
    {
        var klient = vert.CreateClient();

        var svar = await klient.GetAsync("/api/lag");
        var lag = await svar.Content.ReadFromJsonAsync<List<JsonElement>>();

        var hjertestartere = Assert.Single(lag!, l => l.GetProperty("id").GetString() == "hjertestartere");
        Assert.Equal("Hjertestartere", hjertestartere.GetProperty("navn").GetString());
        var beskrivelse = hjertestartere.GetProperty("beskrivelse").GetString();
        Assert.False(string.IsNullOrWhiteSpace(beskrivelse));
        Assert.EndsWith(".", beskrivelse);
        Assert.False(string.IsNullOrWhiteSpace(hjertestartere.GetProperty("ikon").GetString()));
    }

    [Fact]
    public async Task Hjertestartere_svarer_200_med_punkter_naar_kilden_svarer()
    {
        var handler = new OverpassHandler(HttpStatusCode.OK, OverpassSvar);
        using var vertMedData = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient(HjertestartereLag.KlientNavn).ConfigurePrimaryHttpMessageHandler(() => handler)));
        var klient = vertMedData.CreateClient();

        var svar = await klient.GetAsync("/api/lag/hjertestartere");
        var kropp = await svar.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Equal("FeatureCollection", kropp.GetProperty("type").GetString());
        Assert.Equal(2, kropp.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task Hjertestartere_poster_sporringen_i_skjemafeltet_data()
    {
        var handler = new OverpassHandler(HttpStatusCode.OK, OverpassSvar);
        using var vertMedData = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient(HjertestartereLag.KlientNavn).ConfigurePrimaryHttpMessageHandler(() => handler)));
        var klient = vertMedData.CreateClient();

        await klient.GetAsync("/api/lag/hjertestartere");

        Assert.Equal(HttpMethod.Post, handler.Metode);
        Assert.Equal(new Uri("https://overpass-api.de/api/interpreter"), handler.Adresse);
        Assert.Equal("data=" + HjertestartereLag.Spørring(), WebUtility.UrlDecode(handler.Kropp));
        Assert.Contains("OsloLive", handler.UserAgent);
        Assert.Contains("hjertestartere", handler.UserAgent);
    }

    [Fact]
    public async Task Overpass_som_svarer_429_gir_502_bare_for_hjertestartere()
    {
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient(HjertestartereLag.KlientNavn).ConfigurePrimaryHttpMessageHandler(() => new OverpassHandler(HttpStatusCode.TooManyRequests, ""))));
        var klient = vertMedSvikt.CreateClient();

        var hjertestartere = await klient.GetAsync("/api/lag/hjertestartere");
        var kropp = await hjertestartere.Content.ReadFromJsonAsync<JsonElement>();
        var lag = await klient.GetAsync("/api/lag");
        var helse = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.BadGateway, hjertestartere.StatusCode);
        Assert.Equal("Kilden svarte 429.", kropp.GetProperty("feil").GetString());
        Assert.Equal(HttpStatusCode.OK, lag.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helse.StatusCode);
    }

    [Fact]
    public async Task Svikt_i_overpass_gir_502()
    {
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
            tjenester.AddHttpClient(HjertestartereLag.KlientNavn).ConfigurePrimaryHttpMessageHandler(() => new SviktHandler())));
        var klient = vertMedSvikt.CreateClient();

        var svar = await klient.GetAsync("/api/lag/hjertestartere");

        Assert.Equal(HttpStatusCode.BadGateway, svar.StatusCode);
    }

    private sealed class OverpassHandler(HttpStatusCode kode, string kropp) : HttpMessageHandler
    {
        private int kall;

        public int Kall => kall;

        public HttpMethod? Metode { get; private set; }

        public Uri? Adresse { get; private set; }

        public string? UserAgent { get; private set; }

        public string? Kropp { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
        {
            Interlocked.Increment(ref kall);
            Metode = forespørsel.Method;
            Adresse = forespørsel.RequestUri;
            UserAgent = forespørsel.Headers.UserAgent.ToString();
            Kropp = forespørsel.Content is null ? null : await forespørsel.Content.ReadAsStringAsync(stopp);

            return new HttpResponseMessage(kode)
            {
                Content = new StringContent(kropp, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SviktHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            throw new HttpRequestException("Kilden er nede.");
    }

    private sealed class FastKlientFabrikk(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
