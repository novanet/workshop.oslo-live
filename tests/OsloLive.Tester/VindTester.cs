using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Tester;

public class VindTester
{
    [Fact]
    public void Vind_fra_nord_gir_negativ_v()
    {
        var (u, v) = Vind.TilUv(10, 0);

        Assert.True(Math.Abs(u - 0) < 0.01);
        Assert.True(Math.Abs(v - (-10)) < 0.01);
    }

    [Fact]
    public void Vind_fra_vest_gir_positiv_u()
    {
        var (u, v) = Vind.TilUv(10, 270);

        Assert.True(Math.Abs(u - 10) < 0.01);
        Assert.True(Math.Abs(v - 0) < 0.01);
    }

    [Theory]
    [InlineData(10, 90, -10, 0)]
    [InlineData(10, 180, 0, 10)]
    [InlineData(0, 123, 0, 0)]
    public void Vind_fra_retning_gir_u_og_v(double fart, double fraRetning, double forventetU, double forventetV)
    {
        var (u, v) = Vind.TilUv(fart, fraRetning);

        Assert.True(Math.Abs(u - forventetU) < 0.01);
        Assert.True(Math.Abs(v - forventetV) < 0.01);
    }

    [Fact]
    public void Rutenettet_har_16_punkter_innenfor_utsnittet()
    {
        var punkter = Vind.Rutenett();

        Assert.Equal(16, punkter.Count);
        Assert.All(punkter, p => Assert.True(Geo.IOslo(p.Lat, p.Lon)));

        var lats = punkter.Select(p => p.Lat).Distinct().ToList();
        var lons = punkter.Select(p => p.Lon).Distinct().ToList();
        Assert.Equal(4, lats.Count);
        Assert.Equal(4, lons.Count);
        Assert.Equal(Geo.MinLat, lats.Min());
        Assert.Equal(Geo.MaksLat, lats.Max());
        Assert.Equal(Geo.MinLon, lons.Min());
        Assert.Equal(Geo.MaksLon, lons.Max());
    }

    [Fact]
    public void Adressen_bruker_punktum_og_compact()
    {
        var gjeldende = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nb-NO");

            var url = Vind.ByggUrl(59.9139, 10.7522);

            Assert.Equal("https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=59.9139&lon=10.7522", url);
        }
        finally
        {
            CultureInfo.CurrentCulture = gjeldende;
        }
    }

    [Fact]
    public void Tolk_leser_vindfart_og_retning()
    {
        const string json = """
            {
                "properties": {
                    "timeseries": [
                        {
                            "time": "2026-09-23T12:00:00Z",
                            "data": { "instant": { "details": { "wind_speed": 10.0, "wind_from_direction": 270.0 } } }
                        }
                    ]
                }
            }
            """;
        var rot = JsonSerializer.Deserialize<JsonElement>(json);

        var (u, v) = Vind.Tolk(rot);

        Assert.True(Math.Abs(u - 10) < 0.01);
        Assert.True(Math.Abs(v - 0) < 0.01);
    }

    [Fact]
    public void Tolk_uten_tidsserie_kaster()
    {
        const string json = """{ "properties": { "timeseries": [] } }""";
        var rot = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Throws<InvalidOperationException>(() => Vind.Tolk(rot));
    }
}
