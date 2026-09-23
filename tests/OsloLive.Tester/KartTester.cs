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
}
