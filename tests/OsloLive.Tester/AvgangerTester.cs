using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OsloLive.Kart;

namespace OsloLive.Tester;

/// <summary>Ren logikk i Avgangstavle, ingen nettverk.</summary>
public class AvgangstavleTester
{
    private static DateTimeOffset Nå => new(2026, 09, 23, 12, 0, 0, TimeSpan.FromHours(2));

    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly IReadOnlyDictionary<string, string> Selskaper =
        new Dictionary<string, string> { ["DY"] = "Norwegian" };

    private static readonly IReadOnlyDictionary<string, string> Flyplasser =
        new Dictionary<string, string> { ["PRG"] = "Praha" };

    [Fact]
    public void Flyselskapskode_blir_fullt_navn()
    {
        var rad = Rad("""{"flight_id":"DY1502","airline":"DY","airport":"PRG","schedule_time":"2026-09-23T12:10:00+02:00"}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Equal("Norwegian", fly.Flyselskap);
    }

    [Fact]
    public void Flyplasskode_blir_fullt_navn()
    {
        var rad = Rad("""{"flight_id":"DY1502","airline":"DY","airport":"PRG","schedule_time":"2026-09-23T12:10:00+02:00"}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Equal("Praha", fly.Sted);
    }

    [Fact]
    public void Ukjent_kode_vises_som_koden()
    {
        var rad = Rad("""{"flight_id":"XX001","airline":"XX","airport":"ZZZ","schedule_time":"2026-09-23T12:10:00+02:00"}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Equal("XX", fly.Flyselskap);
        Assert.Equal("ZZZ", fly.Sted);
    }

    [Fact]
    public void Mangler_flynummer_kaster()
    {
        var rad = Rad("""{"airline":"DY","airport":"PRG","schedule_time":"2026-09-23T12:10:00+02:00"}""");

        Assert.ThrowsAny<Exception>(() => Avgangstavle.Tolk(rad, Selskaper, Flyplasser));
    }

    [Fact]
    public void Mangler_planlagt_tid_kaster()
    {
        var rad = Rad("""{"flight_id":"DY1502","airline":"DY","airport":"PRG"}""");

        Assert.ThrowsAny<Exception>(() => Avgangstavle.Tolk(rad, Selskaper, Flyplasser));
    }

    [Fact]
    public void Forsinket_fly_er_merket_forsinket()
    {
        var rad = Rad("""{"flight_id":"DY752","airline":"DY","airport":"TRD","schedule_time":"2026-09-23T10:40:00+02:00","delayed":true,"status":{"code":"E","text":"New time","time":"2026-09-23T11:15:00+02:00"}}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.True(fly.Forsinket);
    }

    [Fact]
    public void Statusteksten_sier_forsinket_for_forsinket_fly()
    {
        var rad = Rad("""{"flight_id":"DY752","airline":"DY","airport":"TRD","schedule_time":"2026-09-23T10:40:00+02:00","delayed":true,"status":{"code":"E","text":"New time","time":"2026-09-23T11:15:00+02:00"}}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Contains("forsinket", fly.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("11:15", fly.Status);
    }

    [Fact]
    public void Fly_uten_delayed_flagg_er_ikke_forsinket()
    {
        var rad = Rad("""{"flight_id":"DY372","airline":"DY","airport":"TOS","schedule_time":"2026-09-23T10:30:00+02:00","status":{"code":"E","text":"New time","time":"2026-09-23T10:30:00+02:00"}}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.False(fly.Forsinket);
        Assert.Equal("Ny tid", fly.Status);
    }

    [Fact]
    public void Landet_fly_har_riktig_status()
    {
        var rad = Rad("""{"flight_id":"DY343","airline":"DY","airport":"BOO","schedule_time":"2026-09-23T10:00:00+02:00","status":{"code":"A","text":"Arrived","time":"2026-09-23T09:58:00+02:00"}}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Equal("Landet", fly.Status);
        Assert.False(fly.Forsinket);
    }

    [Fact]
    public void Innstilt_fly_har_riktig_status()
    {
        var rad = Rad("""{"flight_id":"SK308","airline":"SK","airport":"HAU","schedule_time":"2026-09-23T08:40:00+02:00","status":{"code":"C","text":"Cancelled"}}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Equal("Innstilt", fly.Status);
        Assert.False(fly.Forsinket);
    }

    [Fact]
    public void Fly_uten_status_er_i_rute()
    {
        var rad = Rad("""{"flight_id":"DY1052","airline":"DY","airport":"GDN","schedule_time":"2026-09-23T10:45:00+02:00"}""");

        var fly = Avgangstavle.Tolk(rad, Selskaper, Flyplasser);

        Assert.Equal("I rute", fly.Status);
        Assert.False(fly.Forsinket);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(61)]
    [InlineData(120)]
    public void Fly_utenfor_neste_time_blir_ikke_med(int minutter)
    {
        var fly = new[] { new Flyvning("SK001", "SAS", "ARN", Nå.AddMinutes(minutter), "I rute", false) };

        var resultat = Avgangstavle.Neste(fly, Nå);

        Assert.Empty(resultat);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(60)]
    public void Fly_innenfor_neste_time_blir_med(int minutter)
    {
        var fly = new[] { new Flyvning("SK001", "SAS", "ARN", Nå.AddMinutes(minutter), "I rute", false) };

        var resultat = Avgangstavle.Neste(fly, Nå);

        Assert.Single(resultat);
    }

    [Fact]
    public void Listen_er_sortert_paa_planlagt()
    {
        var fly = new[]
        {
            new Flyvning("SK1", "SAS", "ARN", Nå.AddMinutes(40), "I rute", false),
            new Flyvning("SK2", "SAS", "ARN", Nå.AddMinutes(10), "I rute", false),
            new Flyvning("SK3", "SAS", "ARN", Nå.AddMinutes(25), "I rute", false),
        };

        var resultat = Avgangstavle.Neste(fly, Nå);

        Assert.Equal(["SK2", "SK3", "SK1"], resultat.Select(f => f.Flynummer));
    }

    [Fact]
    public void Listen_har_maks_tretti_elementer()
    {
        var fly = Enumerable.Range(0, 40)
            .Select(i => new Flyvning($"SK{i}", "SAS", "ARN", Nå.AddMinutes(i), "I rute", false))
            .ToList();

        var resultat = Avgangstavle.Neste(fly, Nå);

        Assert.Equal(30, resultat.Count);
        Assert.Equal("SK0", resultat.First().Flynummer);
        Assert.Equal("SK29", resultat.Last().Flynummer);
    }
}

/// <summary>HTTP-api mot avgangstavla, med Avinor-kilden stubbet.</summary>
public class AvgangerApiTester
{
    private sealed class FalskAvinor : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
        {
            var vei = forespørsel.RequestUri!.AbsolutePath;
            var spørring = QueryHelpers.ParseQuery(forespørsel.RequestUri!.Query);

            var avgang = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O", CultureInfo.InvariantCulture);
            var avgangStatusTid = DateTimeOffset.UtcNow.AddMinutes(17).ToString("O", CultureInfo.InvariantCulture);
            var ankomst = DateTimeOffset.UtcNow.AddMinutes(20).ToString("O", CultureInfo.InvariantCulture);
            var ankomstStatusTid = DateTimeOffset.UtcNow.AddMinutes(18).ToString("O", CultureInfo.InvariantCulture);

            string svar;
            if (vei.EndsWith("/get_flights"))
            {
                var retning = spørring["direction"];
                svar = retning == "departures"
                    ? "{\"data\":{\"flights\":[{\"flight_id\":\"DY1502\",\"airline\":\"DY\",\"airport\":\"PRG\",\"schedule_time\":\"" + avgang + "\",\"status\":{\"code\":\"D\",\"text\":\"Departed\",\"time\":\"" + avgangStatusTid + "\"}}]}}"
                    : "{\"data\":{\"flights\":[{\"flight_id\":\"DY343\",\"airline\":\"DY\",\"airport\":\"BOO\",\"schedule_time\":\"" + ankomst + "\",\"status\":{\"code\":\"A\",\"text\":\"Arrived\",\"time\":\"" + ankomstStatusTid + "\"}}]}}";
            }
            else if (vei.EndsWith("/lookup_airline"))
            {
                svar = """{"data":[{"code":"DY","name":"Norwegian"}]}""";
            }
            else if (vei.EndsWith("/lookup_airport"))
            {
                svar = spørring["query"] == "PRG"
                    ? """{"data":[{"code":"PRG","name":"Praha"}]}"""
                    : """{"data":[{"code":"BOO","name":"Bodø"}]}""";
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(svar),
            });
        }
    }

    private sealed class FeilendeAvinor : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private static WebApplicationFactory<Program> Vert(HttpMessageHandler falskHandler)
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddHttpClient<Allemannsdata>().ConfigurePrimaryHttpMessageHandler(() => falskHandler)));
    }

    [Fact]
    public async Task Avganger_svarer_200_med_avganger_og_ankomster_med_fulle_navn()
    {
        Allemannsdata.TømMellomlager();
        using var vert = Vert(new FalskAvinor());
        using var klient = vert.CreateClient();

        var svar = await klient.GetAsync("/api/avganger");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var json = await svar.Content.ReadFromJsonAsync<JsonElement>();

        var avganger = json.GetProperty("avganger");
        var ankomster = json.GetProperty("ankomster");
        Assert.Equal(JsonValueKind.Array, avganger.ValueKind);
        Assert.Equal(JsonValueKind.Array, ankomster.ValueKind);

        Assert.Single(avganger.EnumerateArray());
        var avgang = avganger[0];
        Assert.Equal("DY1502", avgang.GetProperty("flynummer").GetString());
        Assert.Equal("Norwegian", avgang.GetProperty("flyselskap").GetString());
        Assert.Equal("Praha", avgang.GetProperty("sted").GetString());
        Assert.False(avgang.GetProperty("forsinket").GetBoolean());

        Assert.Single(ankomster.EnumerateArray());
        Assert.Equal("Bodø", ankomster[0].GetProperty("sted").GetString());
    }

    [Fact]
    public async Task Planlagt_er_iso_8601()
    {
        Allemannsdata.TømMellomlager();
        using var vert = Vert(new FalskAvinor());
        using var klient = vert.CreateClient();

        var json = await klient.GetFromJsonAsync<JsonElement>("/api/avganger");
        var planlagt = json.GetProperty("avganger")[0].GetProperty("planlagt").GetString();

        Assert.Contains("T", planlagt);
        DateTimeOffset.Parse(planlagt!, CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task Avganger_gir_502_med_feil_naar_kilden_feiler()
    {
        Allemannsdata.TømMellomlager();
        using var vert = Vert(new FeilendeAvinor());
        using var klient = vert.CreateClient();

        var svar = await klient.GetAsync("/api/avganger");

        Assert.Equal(HttpStatusCode.BadGateway, svar.StatusCode);
        var json = await svar.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("feil", out var feil) && !string.IsNullOrWhiteSpace(feil.GetString()));
    }

    [Fact]
    public async Task Helsesjekken_svarer_200_selv_om_kilden_feiler()
    {
        Allemannsdata.TømMellomlager();
        using var vert = Vert(new FeilendeAvinor());
        using var klient = vert.CreateClient();

        var svar = await klient.GetAsync("/api/helse");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
    }
}
