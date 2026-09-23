using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class FlyLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Fly_i_lufta_blir_punkt_med_kallesignal_hoyde_fart_og_status()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": "SAS117  ", "lat": 59.95, "lon": 10.75, "alt_baro": 3400, "gs": 210.4 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("SAS117", punkt!.Properties["navn"]);
        Assert.Equal("SAS117", punkt.Properties["kallesignal"]);
        Assert.Equal("airplanes.live (ADS-B)", punkt.Properties["kilde"]);
        Assert.Equal("3,400 fot", punkt.Properties["høyde"]);
        Assert.Equal("210 knop", punkt.Properties["fart"]);
        Assert.Equal("i lufta", punkt.Properties["status"]);
    }

    [Fact]
    public void Fly_paa_bakken_faar_status_paa_bakken()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": "SAS117", "lat": 59.95, "lon": 10.75, "alt_baro": "ground", "gs": 0 }
            """));

        Assert.Equal("på bakken", punkt!.Properties["status"]);
        Assert.Null(punkt.Properties["høyde"]);
    }

    [Fact]
    public void Fly_uten_posisjon_blir_forkastet()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": "SAS117" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Fly_med_null_posisjon_blir_forkastet()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": "SAS117", "lat": null, "lon": null }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Fly_uten_hex_blir_forkastet()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "flight": "SAS117", "lat": 59.95, "lon": 10.75 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Fly_med_tekst_som_kallesignal_av_feil_type_bruker_hex()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": 12345, "lat": 59.95, "lon": 10.75 }
            """));

        Assert.Equal("4601B1", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Fly_uten_kallesignal_bruker_hex()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "lat": 59.95, "lon": 10.75 }
            """));

        Assert.Equal("4601B1", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Fly_uten_fart_utelater_fartfeltet()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "lat": 59.95, "lon": 10.75 }
            """));

        Assert.Null(punkt!.Properties["fart"]);
    }

    [Fact]
    public void Fly_paa_gardermoen_er_utenfor_dagens_utsnitt()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": "SAS117", "lat": 60.1976, "lon": 11.1004 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Koordinatene_kommer_fra_Geo_Punkt()
    {
        var punkt = FlyLag.TilPunkt(Rad("""
            { "hex": "4601b1", "flight": "SAS117", "lat": 59.95, "lon": 10.75 }
            """));

        Assert.Equal("Point", punkt!.Geometry.Type);
    }

    [Fact]
    public void Url_bruker_punktum_som_desimaltegn()
    {
        var url = FlyLag.ByggUrl();

        Assert.StartsWith("https://api.airplanes.live/v2/point/59.9139/10.7522/30", url);
    }
}
