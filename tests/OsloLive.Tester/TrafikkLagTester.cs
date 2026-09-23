using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class TrafikkLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string EufemiasRad = """
        {
            "id": "17684V2460285",
            "name": "Dr. Eufemias Gt. Vestgående Datter",
            "road_reference": "EV18 S55D10 m1625",
            "municipality": "Oslo",
            "county": "Oslo",
            "lat": 59.908734,
            "lon": 10.754618,
            "type": "VEHICLE",
            "operational": true,
            "latest_hourly_data": "2026-09-23T11:00:00+02:00"
        }
        """;

    [Fact]
    public void Parametrene_ber_om_punkter_i_Oslo_fylke()
    {
        var url = Allemannsdata.ByggUrl("vegvesen", "find_traffic_points", TrafikkLag.Parametre);

        Assert.Contains("county_number=3", url);
    }

    [Fact]
    public void Registreringspunkt_blir_punkt_med_id_navn_og_kilde()
    {
        var punkt = TrafikkLag.TilPunkt(Rad(EufemiasRad));

        Assert.NotNull(punkt);
        Assert.Equal("17684V2460285", punkt!.Properties["id"]);
        Assert.Equal("Dr. Eufemias Gt. Vestgående Datter", punkt.Properties["navn"]);
        Assert.Equal("Statens vegvesen", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = TrafikkLag.TilPunkt(Rad(EufemiasRad));

        Assert.Equal([10.754618, 59.908734], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Vei_vises_fra_road_reference()
    {
        var punkt = TrafikkLag.TilPunkt(Rad(EufemiasRad));

        Assert.NotNull(punkt);
        Assert.Equal("EV18 S55D10 m1625", punkt!.Properties["vei"]);
    }

    [Fact]
    public void Punkt_uten_road_reference_mangler_vei()
    {
        var punkt = TrafikkLag.TilPunkt(Rad("""
            {
                "id": "03375V625405", "name": "Rv. 150 Ulvensplitten",
                "lat": 59.922586, "lon": 10.807645
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("vei"));
    }

    [Fact]
    public void Punkt_uten_navn_faar_reservenavn()
    {
        var punkt = TrafikkLag.TilPunkt(Rad("""
            {
                "id": "03375V625405", "name": null, "road_reference": "RV150 S1D1 m326",
                "lat": 59.922586, "lon": 10.807645
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent registreringspunkt", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Punkt_uten_posisjon_blir_forkastet()
    {
        var punkt = TrafikkLag.TilPunkt(Rad("""
            {
                "id": "03375V625405", "name": "Rv. 150 Ulvensplitten",
                "lat": null, "lon": null
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Punkt_uten_id_blir_forkastet()
    {
        var punkt = TrafikkLag.TilPunkt(Rad("""
            {
                "name": "Rv. 150 Ulvensplitten",
                "lat": 59.922586, "lon": 10.807645
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Punkt_utenfor_utsnittet_blir_forkastet()
    {
        // Langt sør for Oslo-boksen (MinLat 59.80).
        var punkt = TrafikkLag.TilPunkt(Rad("""
            {
                "id": "99999V999999", "name": "Test",
                "lat": 59.50, "lon": 10.75
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Samme_id_to_ganger_blir_ett_punkt()
    {
        var lag = Geo.Samle([TrafikkLag.TilPunkt(Rad(EufemiasRad)), TrafikkLag.TilPunkt(Rad(EufemiasRad))]);

        Assert.Single(lag.Features);
    }
}
