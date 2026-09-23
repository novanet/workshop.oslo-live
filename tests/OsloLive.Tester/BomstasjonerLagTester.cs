using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class BomstasjonerLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string SmestadRad = """
        {
            "id": 1000123,
            "egenskaper": [
                {"id":1, "navn":"Navn bomstasjon", "verdi":"Smestad"},
                {"id":2, "navn":"Takst liten bil", "verdi":38.0},
                {"id":3, "navn":"Rushtidstakst liten bil", "verdi":46.0}
            ],
            "geometri": {"wkt":"POINT Z (59.93145778 10.70870196 49.62650543)", "srid":4326}
        }
        """;

    [Fact]
    public void Wkt_med_hoyde_gir_breddegrad_forst()
    {
        var resultat = BomstasjonerLag.TolkWkt("POINT Z (59.93145778 10.70870196 49.62650543)");

        Assert.NotNull(resultat);
        Assert.Equal(59.93145778, resultat!.Value.Lat, 8);
        Assert.Equal(10.70870196, resultat.Value.Lon, 8);
    }

    [Theory]
    [InlineData("tull")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("POINT Z (abc def)")]
    [InlineData("POINT Z (59.9")]
    [InlineData("LINESTRING (59.9 10.7, 59.8 10.6)")]
    public void Ugyldig_wkt_gir_null(string? input)
    {
        Assert.Null(BomstasjonerLag.TolkWkt(input));
    }

    [Fact]
    public void Bomstasjon_blir_punkt_med_id_navn_og_kilde()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad(SmestadRad));

        Assert.NotNull(punkt);
        Assert.Equal("1000123", punkt!.Properties["id"]);
        Assert.Equal("Smestad", punkt.Properties["navn"]);
        Assert.Equal("Statens vegvesen, NVDB", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad(SmestadRad));

        Assert.NotNull(punkt);
        Assert.Equal(10.70870196, punkt!.Geometry.Coordinates[0], 8);
        Assert.Equal(59.93145778, punkt.Geometry.Coordinates[1], 8);
    }

    [Fact]
    public void Takstene_vises_i_kroner()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad(SmestadRad));

        Assert.NotNull(punkt);
        Assert.Equal("38 kr", punkt!.Properties["takst"]);
        Assert.Equal("46 kr", punkt.Properties["rushtid"]);
    }

    [Fact]
    public void Bomstasjon_uten_takster_faar_null_i_takstfeltene()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad("""
            {
                "id": 999,
                "egenskaper": [{"id":1, "navn":"Navn bomstasjon", "verdi":"Test"}],
                "geometri": {"wkt":"POINT Z (59.93 10.71 50)"}
            }
            """));

        Assert.NotNull(punkt);
        Assert.Null(punkt!.Properties["takst"]);
        Assert.Null(punkt.Properties["rushtid"]);
    }

    [Fact]
    public void Bomstasjon_uten_navn_heter_ukjent_bomstasjon()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad("""
            {
                "id": 999,
                "egenskaper": [],
                "geometri": {"wkt":"POINT Z (59.93 10.71 50)"}
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent bomstasjon", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Bomstasjon_med_ugyldig_geometri_blir_forkastet()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad("""
            {
                "id": 999,
                "egenskaper": [{"id":1, "navn":"Navn bomstasjon", "verdi":"Test"}],
                "geometri": {"wkt":"tull"}
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Bomstasjon_uten_id_blir_forkastet()
    {
        var punkt = BomstasjonerLag.TilPunkt(Rad("""
            {
                "egenskaper": [{"id":1, "navn":"Navn bomstasjon", "verdi":"Test"}],
                "geometri": {"wkt":"POINT Z (59.93 10.71 50)"}
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Bomstasjon_utenfor_utsnittet_blir_forkastet()
    {
        // Langt sør for Oslo-boksen (MinLat 59.80).
        var punkt = BomstasjonerLag.TilPunkt(Rad("""
            {
                "id": 999,
                "egenskaper": [{"id":1, "navn":"Navn bomstasjon", "verdi":"Test"}],
                "geometri": {"wkt":"POINT Z (59.10 10.71 50)"}
            }
            """));

        Assert.Null(punkt);
    }
}
