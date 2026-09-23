using OsloLive.Historikk;
using OsloLive.Kart;

namespace OsloLive.Tester;

public class BildelagerVelgNaermesteTester
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-09-18T08:00:00Z");

    [Fact]
    public void VelgNaermeste_velger_bildet_naermest_tidspunktet()
    {
        var tidspunkter = new[] { T.AddHours(-2), T.AddHours(-1), T.AddHours(1) };

        var valgt = Bildelager.VelgNærmeste(tidspunkter, T.AddMinutes(10), Bildelager.MaksAvstand);

        Assert.Equal(T.AddHours(1), valgt);
    }

    [Fact]
    public void VelgNaermeste_med_lik_avstand_velger_det_eldste()
    {
        var tidspunkter = new[] { T.AddHours(-1), T.AddHours(1) };

        var valgt = Bildelager.VelgNærmeste(tidspunkter, T, Bildelager.MaksAvstand);

        Assert.Equal(T.AddHours(-1), valgt);
    }

    [Fact]
    public void VelgNaermeste_uten_bilde_innenfor_to_timer_gir_null()
    {
        var tidspunkter = new[] { T.AddHours(-3), T.AddHours(2).AddMinutes(30) };

        var valgt = Bildelager.VelgNærmeste(tidspunkter, T, Bildelager.MaksAvstand);

        Assert.Null(valgt);
    }

    [Fact]
    public void VelgNaermeste_akkurat_to_timer_unna_er_med()
    {
        var tidspunkter = new[] { T.AddHours(2) };

        var valgt = Bildelager.VelgNærmeste(tidspunkter, T, Bildelager.MaksAvstand);

        Assert.Equal(T.AddHours(2), valgt);
    }

    [Fact]
    public void VelgNaermeste_uten_bilder_gir_null()
    {
        var valgt = Bildelager.VelgNærmeste([], T, Bildelager.MaksAvstand);

        Assert.Null(valgt);
    }
}

public class BildelagerDiskTester : IDisposable
{
    private readonly string mappe = Path.Combine(Path.GetTempPath(), "oslolive-test-" + Guid.NewGuid().ToString("N"));
    private readonly Bildelager bilder;
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-09-18T08:00:00Z");

    public BildelagerDiskTester()
    {
        bilder = new Bildelager(mappe);
    }

    public void Dispose()
    {
        if (Directory.Exists(mappe))
        {
            Directory.Delete(mappe, recursive: true);
        }
    }

    [Fact]
    public async Task Lagret_bilde_hentes_igjen_med_punktene()
    {
        await bilder.Lagre("luftkvalitet", Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test")]), T);

        var lag = await bilder.HentNærmest("luftkvalitet", T.AddMinutes(40));

        Assert.NotNull(lag);
        Assert.Equal("FeatureCollection", lag!.Type);
        Assert.Single(lag.Features);
        Assert.Equal("a", lag.Features[0].Properties["id"]!.ToString());
    }

    [Fact]
    public async Task Tidspunkter_er_sortert_eldste_foerst()
    {
        var lag = Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test")]);
        await bilder.Lagre("luftkvalitet", lag, T.AddHours(1));
        await bilder.Lagre("luftkvalitet", lag, T.AddHours(-1));

        var tidspunkter = bilder.Tidspunkter("luftkvalitet");

        Assert.Equal([T.AddHours(-1), T.AddHours(1)], tidspunkter);
    }

    [Fact]
    public async Task Bilder_eldre_enn_grensen_slettes()
    {
        var lag = Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test")]);
        await bilder.Lagre("luftkvalitet", lag, T);
        await bilder.Lagre("luftkvalitet", lag, T.AddDays(-8));

        var antall = bilder.SlettEldreEnn(T.AddDays(-7));

        Assert.Equal(1, antall);
        Assert.Equal([T], bilder.Tidspunkter("luftkvalitet"));
    }

    [Fact]
    public async Task Lag_uten_bilder_gir_null()
    {
        var lag = await bilder.HentNærmest("fly", T);

        Assert.Null(lag);
    }
}
