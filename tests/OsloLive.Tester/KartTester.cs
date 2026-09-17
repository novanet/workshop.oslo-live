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
}
