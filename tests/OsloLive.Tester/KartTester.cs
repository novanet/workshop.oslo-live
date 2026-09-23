using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OsloLive.Kart;

namespace OsloLive.Tester;

public class GeoTester
{
    [Fact]
    public void Punkt_i_oslo_blir_laget()
    {
        var punkt = Geo.Lag("id-1", 59.9139, 10.7522, "Rådhuset", "Test");

        Assert.NotNull(punkt);
        Assert.Equal("Feature", punkt.Type);
        Assert.Equal("Point", punkt.Geometry.Type);
    }

    [Fact]
    public void Punkt_har_alltid_navn_og_kilde()
    {
        var punkt = Geo.Lag("id-1", 59.9139, 10.7522, "Rådhuset", "Test");

        Assert.Equal("Rådhuset", punkt!.Properties["navn"]);
        Assert.Equal("Test", punkt.Properties["kilde"]);
        Assert.Equal("id-1", punkt.Properties["id"]);
    }

    [Fact]
    public void Detaljer_blir_med_videre()
    {
        var punkt = Geo.Lag("id-1", 59.9139, 10.7522, "Rådhuset", "Test",
            new Dictionary<string, object?> { ["temperatur"] = 15.7 });

        Assert.Equal(15.7, punkt!.Properties["temperatur"]);
    }

    [Fact]
    public void Samle_gir_en_featurecollection()
    {
        var lag = Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Kilde A")]);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Single(lag.Features);
    }

    [Fact]
    public void Samle_hopper_over_punkter_som_er_null()
    {
        var lag = Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Kilde A"), null]);

        Assert.Single(lag.Features);
    }

    [Fact]
    public void Rådhuset_ligger_i_oslo()
    {
        Assert.True(Geo.IOslo(59.9139, 10.7522));
    }

    [Theory]
    [InlineData(59.9139, 10.7522)] // Rådhuset
    [InlineData(59.8960, 10.6270)] // Nesoddtangen
    public void Punkt_innenfor_utsnittet_ligger_i_oslo(double lat, double lon)
    {
        Assert.True(Geo.IOslo(lat, lon));
    }

    [Theory]
    [InlineData(61.1150, 10.4660)] // Lillehammer: lengdegrad innenfor, breddegrad utenfor
    [InlineData(63.4305, 10.3951)] // Trondheim
    public void Punkt_langt_nord_ligger_ikke_i_oslo(double lat, double lon)
    {
        Assert.False(Geo.IOslo(lat, lon));
    }

    [Theory]
    [InlineData(60.3913, 5.3221)] // Bergen
    [InlineData(59.9139, 4.0000)] // Samme breddegrad som Oslo, i Nordsjøen
    public void Punkt_langt_vest_ligger_ikke_i_oslo(double lat, double lon)
    {
        Assert.False(Geo.IOslo(lat, lon));
    }

    [Fact]
    public void Punkt_som_bare_feiler_paa_breddegrad_ligger_ikke_i_oslo()
    {
        Assert.False(Geo.IOslo(61.0, 10.75)); // lon innenfor, lat utenfor
    }

    [Fact]
    public void Punkt_som_bare_feiler_paa_lengdegrad_ligger_ikke_i_oslo()
    {
        Assert.False(Geo.IOslo(59.9139, 4.0)); // lat innenfor, lon utenfor
    }

    [Fact]
    public void Punkt_utenfor_utsnittet_blir_forkastet()
    {
        var punkt = Geo.Lag("id", 61.115, 10.466, "Lillehammer", "Test");

        Assert.Null(punkt);
    }

    [Fact]
    public void Samle_beholder_ulike_punkter_fra_samme_kilde()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.91, 10.75, "A", "Kilde"),
            Geo.Lag("b", 59.92, 10.76, "B", "Kilde"),
            Geo.Lag("c", 59.93, 10.77, "C", "Kilde"),
        ]);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(3, lag.Features.Count);
        Assert.Equal(["a", "b", "c"], lag.Features.Select(f => f.Properties["id"]));

        foreach (var punkt in lag.Features)
        {
            Assert.Contains("id", punkt.Properties.Keys);
            Assert.Contains("navn", punkt.Properties.Keys);
            Assert.Contains("kilde", punkt.Properties.Keys);
        }
    }

    [Fact]
    public void Samle_fjerner_punkter_med_samme_id()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.91, 10.75, "A", "Kilde"),
            Geo.Lag("a", 59.91, 10.75, "A", "Kilde"),
        ]);

        Assert.Single(lag.Features);
        Assert.Equal("a", lag.Features[0].Properties["id"]);
    }

    [Fact]
    public void Samle_tar_ikke_med_punkter_utenfor_utsnittet()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.91, 10.75, "Rådhuset", "Kilde A"),
            Geo.Lag("b", 61.115, 10.466, "Lillehammer", "Kilde B"),
        ]);

        Assert.Single(lag.Features);
        Assert.Equal("a", lag.Features[0].Properties["id"]);
    }
}

public class AllemannsdataTester
{
    [Fact]
    public void Url_peker_paa_riktig_kilde_og_operasjon()
    {
        var url = Allemannsdata.ByggUrl("luftkvalitet", "get_air_quality_nearby",
            new Dictionary<string, object> { ["limit"] = 50 });

        Assert.StartsWith("https://allemannsdata.com/wiki/api/v1/kilder/luftkvalitet/get_air_quality_nearby?", url);
        Assert.Contains("limit=50", url);
    }

    [Fact]
    public void Url_tar_med_alle_parametre()
    {
        var url = Allemannsdata.ByggUrl("ais", "find_vessels_nearby",
            new Dictionary<string, object> { ["limit"] = 10, ["offset"] = 0 });

        Assert.Contains("limit=10", url);
        Assert.Contains("offset=0", url);
    }

    [Fact]
    public void Levetid_er_tretti_sekunder()
    {
        Assert.Equal(30, Allemannsdata.Levetid.TotalSeconds);
    }

    [Fact]
    public void Levetid_bruker_konstanten_i_sekunder()
    {
        Assert.Equal(TimeSpan.FromSeconds(Allemannsdata.LevetidSekunder), Allemannsdata.Levetid);
    }

    private static void MedNorskKultur(Action handling)
    {
        var forrigeKultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nb-NO");
        try
        {
            handling();
        }
        finally
        {
            CultureInfo.CurrentCulture = forrigeKultur;
        }
    }

    [Fact]
    public void Url_bruker_punktum_for_double_med_norsk_kultur()
    {
        MedNorskKultur(() =>
        {
            Assert.Equal(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            var url = Allemannsdata.ByggUrl("luftkvalitet", "get_air_quality_nearby",
                new Dictionary<string, object> { ["lat"] = 59.9139, ["lon"] = 10.7522 });

            Assert.Contains("lat=59.9139", url);
            Assert.Contains("lon=10.7522", url);
            Assert.DoesNotContain("59,9139", url);
            Assert.DoesNotContain("59%2C9139", url);
        });
    }

    [Fact]
    public void Url_bruker_punktum_for_float_med_norsk_kultur()
    {
        MedNorskKultur(() =>
        {
            var url = Allemannsdata.ByggUrl("luftkvalitet", "get_air_quality_nearby",
                new Dictionary<string, object> { ["lat"] = 59.5f });

            Assert.Contains("lat=59.5", url);
            Assert.DoesNotContain("%2C", url);
        });
    }

    [Fact]
    public void Url_bruker_punktum_for_decimal_med_norsk_kultur()
    {
        MedNorskKultur(() =>
        {
            var url = Allemannsdata.ByggUrl("luftkvalitet", "get_air_quality_nearby",
                new Dictionary<string, object> { ["lat"] = 59.9139m });

            Assert.Contains("lat=59.9139", url);
            Assert.DoesNotContain("%2C", url);
        });
    }

    [Fact]
    public void Heltall_formateres_som_foer_med_norsk_kultur()
    {
        MedNorskKultur(() =>
        {
            var url = Allemannsdata.ByggUrl("luftkvalitet", "get_air_quality_nearby",
                new Dictionary<string, object> { ["limit"] = 50, ["navn"] = "Oslo" });

            Assert.Contains("limit=50", url);
            Assert.Contains("navn=Oslo", url);
        });
    }
}

/// <summary>Tester nye forsøk i <see cref="Allemannsdata.Hent"/> mot en falsk <see cref="HttpMessageHandler"/>.</summary>
public class AllemannsdataForsøkTester
{
    private static readonly IReadOnlyDictionary<string, object> Parametre = new Dictionary<string, object> { ["limit"] = 1 };

    [Fact]
    public async Task Lykkes_paa_andre_forsoek_etter_femhundretre_svar()
    {
        var håndterer = new FalskHandler(forsøk => forsøk == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : LagJsonSvar(1));
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        var svar = await data.Hent("test-retry-1", "operasjon", Parametre);

        Assert.Equal(1, svar.GetProperty("verdi").GetInt32());
        Assert.Equal(2, håndterer.Forsøk);
    }

    [Fact]
    public async Task Nettverksfeil_proeves_paa_nytt()
    {
        var håndterer = new FalskHandler(forsøk => forsøk == 1
            ? throw new HttpRequestException("Nettverksfeil")
            : LagJsonSvar(2));
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        var svar = await data.Hent("test-retry-2", "operasjon", Parametre);

        Assert.Equal(2, svar.GetProperty("verdi").GetInt32());
        Assert.Equal(2, håndterer.Forsøk);
    }

    [Fact]
    public async Task Tidsavbrudd_proeves_paa_nytt()
    {
        var håndterer = new FalskHandler(forsøk => forsøk == 1
            ? throw new TaskCanceledException("Tidsavbrudd", new TimeoutException())
            : LagJsonSvar(3));
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        var svar = await data.Hent("test-retry-3", "operasjon", Parametre);

        Assert.Equal(3, svar.GetProperty("verdi").GetInt32());
        Assert.Equal(2, håndterer.Forsøk);
    }

    [Fact]
    public async Task Kall_med_404_proeves_ikke_paa_nytt()
    {
        var håndterer = new FalskHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => data.Hent("test-retry-4", "operasjon", Parametre));

        Assert.Equal(1, håndterer.Forsøk);
    }

    [Fact]
    public async Task Gir_opp_etter_tredje_forsoek()
    {
        var håndterer = new FalskHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => data.Hent("test-retry-5", "operasjon", Parametre));

        Assert.Equal(3, håndterer.Forsøk);
    }

    [Fact]
    public async Task Svar_fra_nytt_forsoek_blir_mellomlagret()
    {
        var håndterer = new FalskHandler(forsøk => forsøk == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : LagJsonSvar(4));
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        await data.Hent("test-retry-6", "operasjon", Parametre);
        await data.Hent("test-retry-6", "operasjon", Parametre);

        Assert.Equal(2, håndterer.Forsøk);
    }

    [Fact]
    public async Task Nye_forsoek_logges_som_advarsel_med_kilde_operasjon_og_forsoeksnummer()
    {
        var håndterer = new FalskHandler(forsøk => forsøk switch
        {
            1 or 2 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => LagJsonSvar(5),
        });
        var logg = new OpptakLogg();
        var data = new Allemannsdata(new HttpClient(håndterer), logg, new StraksTid());

        await data.Hent("min-kilde", "min-operasjon", Parametre);

        var advarsler = logg.Oppføringer.Where(o => o.Nivå == LogLevel.Warning).ToList();
        Assert.Equal(2, advarsler.Count);
        Assert.Contains("min-kilde", advarsler[0].Melding);
        Assert.Contains("min-operasjon", advarsler[0].Melding);
        Assert.Contains("2", advarsler[0].Melding);
        Assert.Contains("3", advarsler[1].Melding);
    }

    [Fact]
    public async Task Foerste_forsoek_som_lykkes_logger_ingen_advarsel()
    {
        var håndterer = new FalskHandler(_ => LagJsonSvar(6));
        var logg = new OpptakLogg();
        var data = new Allemannsdata(new HttpClient(håndterer), logg, new StraksTid());

        await data.Hent("test-retry-7", "operasjon", Parametre);

        Assert.DoesNotContain(logg.Oppføringer, o => o.Nivå == LogLevel.Warning);
    }

    [Fact]
    public async Task Kansellert_token_stopper_uten_flere_forsoek()
    {
        using var kilde = new CancellationTokenSource();
        var håndterer = new FalskHandler(forsøk =>
        {
            if (forsøk == 1)
            {
                kilde.Cancel();
                throw new TaskCanceledException("Avbrutt", null, kilde.Token);
            }

            return LagJsonSvar(7);
        });
        var data = new Allemannsdata(new HttpClient(håndterer), new OpptakLogg(), new StraksTid());

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => data.Hent("test-retry-8", "operasjon", Parametre, kilde.Token));

        Assert.Equal(1, håndterer.Forsøk);
    }

    private static HttpResponseMessage LagJsonSvar(int verdi)
    {
        var json = JsonSerializer.Serialize(new { data = new { verdi } });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>Falsk <see cref="HttpMessageHandler"/> som svarer ut fra forsøksnummeret, uten nettverk.</summary>
internal sealed class FalskHandler(Func<int, HttpResponseMessage> svar) : HttpMessageHandler
{
    public int Forsøk { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Forsøk++;
        return Task.FromResult(svar(Forsøk));
    }
}

/// <summary>Fanger opp loggoppføringer slik at tester kan se etter varsler om nye forsøk.</summary>
internal sealed class OpptakLogg : ILogger<Allemannsdata>
{
    public List<(LogLevel Nivå, string Melding)> Oppføringer { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instans;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Oppføringer.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instans = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>TimeProvider som utløser tidsurer nesten øyeblikkelig, slik at tester ikke venter i sanntid.</summary>
internal sealed class StraksTid : TimeProvider
{
    public override long GetTimestamp() => 0;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var tidsur = new FalskTimer();
        _ = Task.Run(() => callback(state));
        return tidsur;
    }

    private sealed class FalskTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
