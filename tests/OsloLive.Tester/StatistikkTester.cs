using System.Globalization;
using OsloLive.Kart;

namespace OsloLive.Tester;

public class StatistikkTester
{
    [Fact]
    public void Vellykket_henting_gir_antall_og_tidsrom()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.91, 10.75, "A", "Test", new() { ["målt"] = "2026-09-23T08:00:00Z" }),
            Geo.Lag("b", 59.92, 10.76, "B", "Test", new() { ["målt"] = "2026-09-23T10:00:00+00:00" }),
        ]);
        var statistikk = new Lagstatistikk();
        var nå = DateTimeOffset.UtcNow;

        statistikk.Vellykket("a", lag, nå);
        var status = statistikk.Hent("a");

        Assert.Equal(2, status.Antall);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T08:00:00Z", CultureInfo.InvariantCulture), status.Eldste);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T10:00:00Z", CultureInfo.InvariantCulture), status.Nyeste);
        Assert.Equal(nå, status.Hentet);
        Assert.False(status.Feiler);
    }

    [Fact]
    public void Lag_uten_tidsstempler_gir_null_i_tidsrom()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.91, 10.75, "A", "Test"),
            Geo.Lag("b", 59.92, 10.76, "B", "Test"),
        ]);
        var statistikk = new Lagstatistikk();

        statistikk.Vellykket("a", lag, DateTimeOffset.UtcNow);
        var status = statistikk.Hent("a");

        Assert.Equal(2, status.Antall);
        Assert.Null(status.Eldste);
        Assert.Null(status.Nyeste);
    }

    [Fact]
    public void Feil_etter_vellykket_henting_beholder_hentet_og_nuller_antall()
    {
        var lag = Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test")]);
        var statistikk = new Lagstatistikk();
        var t1 = DateTimeOffset.UtcNow;

        statistikk.Vellykket("a", lag, t1);
        statistikk.Feilet("a");
        var status = statistikk.Hent("a");

        Assert.True(status.Feiler);
        Assert.Null(status.Antall);
        Assert.Equal(t1, status.Hentet);
    }

    [Fact]
    public void Ukjent_lag_gir_tom_status()
    {
        var statistikk = new Lagstatistikk();

        var status = statistikk.Hent("x");

        Assert.Null(status.Antall);
        Assert.Null(status.Eldste);
        Assert.Null(status.Nyeste);
        Assert.Null(status.Hentet);
        Assert.False(status.Feiler);
    }

    [Fact]
    public void Tidsstempel_tolkes_uavhengig_av_norsk_kultur()
    {
        var forrigeKultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nb-NO");
        try
        {
            var lag = Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test", new() { ["målt"] = "2026-09-23T08:00:00Z" })]);

            var (eldste, nyeste) = Lagstatistikk.Tidsrom(lag);

            Assert.Equal(DateTimeOffset.Parse("2026-09-23T08:00:00Z", CultureInfo.InvariantCulture), eldste);
            Assert.Equal(eldste, nyeste);
        }
        finally
        {
            CultureInfo.CurrentCulture = forrigeKultur;
        }
    }

    [Fact]
    public void Vannmaalere_og_smilefjes_gir_riktig_tidsrom()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.91, 10.75, "A", "Test", new() { ["sist målt"] = "2026-09-23T10:00:00Z" }),
            Geo.Lag("b", 59.92, 10.76, "B", "Test", new() { ["tilsyn"] = "2026-08-12" }),
        ]);

        var (eldste, nyeste) = Lagstatistikk.Tidsrom(lag);

        Assert.Equal(DateTimeOffset.Parse("2026-08-11T22:00:00Z", CultureInfo.InvariantCulture), eldste);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T10:00:00Z", CultureInfo.InvariantCulture), nyeste);
    }
}
