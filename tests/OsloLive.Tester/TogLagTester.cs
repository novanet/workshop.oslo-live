using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class TogLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly (string Kode, string Navn, double Lat, double Lon) Osl = ("OSL", "Oslo S", 59.9110, 10.7528);
    private static readonly (string Kode, string Navn, double Lat, double Lon) Alb = ("ALB", "Alnabru", 59.9320, 10.8440);

    [Theory]
    [InlineData("1137-2026-09-23", "1137")]
    [InlineData("2740-2026-09-23", "2740")]
    public void Tognummer_er_delen_foran_foerste_bindestrek(string kjøretøyId, string forventet)
    {
        Assert.Equal(forventet, TogLag.Tognummer(kjøretøyId));
    }

    [Fact]
    public void Gpstog_blir_punkt_med_navn_kilde_og_tognummer()
    {
        var rad = Rad("""
            { "vehicle_id": "1137-2026-09-23", "mode": "RAIL", "line": "R21", "line_name": "Stabekk-Oslo S-Moss", "origin": null, "destination": null, "lat": 59.91032, "lon": 10.755376 }
            """);

        var punkt = TogLag.FraGps(rad, new Dictionary<string, JsonElement>());

        Assert.NotNull(punkt);
        Assert.Equal("1137", punkt!.Properties["navn"]);
        Assert.Equal("Entur", punkt.Properties["kilde"]);
        Assert.Equal("1137", punkt.Properties["tognummer"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var rad = Rad("""
            { "vehicle_id": "1137-2026-09-23", "mode": "RAIL", "line_name": "Stabekk-Oslo S-Moss", "lat": 59.91032, "lon": 10.755376 }
            """);

        var punkt = TogLag.FraGps(rad, new Dictionary<string, JsonElement>());

        Assert.Equal([10.755376, 59.91032], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Gpstog_henter_fra_og_til_fra_stasjonstavla()
    {
        var gps = Rad("""
            { "vehicle_id": "1137-2026-09-23", "mode": "RAIL", "line_name": "Stabekk-Oslo S-Moss", "lat": 59.91032, "lon": 10.755376 }
            """);
        var tavleRad = Rad("""
            { "train": "1137", "origin": "Stabekk", "destination": "Moss", "at_stop": true }
            """);

        var punkt = TogLag.FraGps(gps, new Dictionary<string, JsonElement> { ["1137"] = tavleRad });

        Assert.Equal("Stabekk", punkt!.Properties["fra"]);
        Assert.Equal("Moss", punkt.Properties["til"]);
    }

    [Fact]
    public void Gpstog_uten_tavlerad_faar_fra_og_til_fra_linjenavnet()
    {
        var gps = Rad("""
            { "vehicle_id": "1137-2026-09-23", "mode": "RAIL", "line_name": "Stabekk-Oslo S-Moss", "lat": 59.91032, "lon": 10.755376 }
            """);

        var punkt = TogLag.FraGps(gps, new Dictionary<string, JsonElement>());

        Assert.Equal("Stabekk", punkt!.Properties["fra"]);
        Assert.Equal("Moss", punkt.Properties["til"]);
    }

    [Fact]
    public void Gpstog_har_ikke_stasjonsfelt()
    {
        var gps = Rad("""
            { "vehicle_id": "1137-2026-09-23", "mode": "RAIL", "line_name": "Stabekk-Oslo S-Moss", "lat": 59.91032, "lon": 10.755376 }
            """);

        var punkt = TogLag.FraGps(gps, new Dictionary<string, JsonElement>());

        Assert.False(punkt!.Properties.ContainsKey("stasjon"));
    }

    [Fact]
    public void Godstog_paa_stasjon_faar_stasjonens_posisjon_og_stasjonsfelt()
    {
        var tog = Rad("""
            { "train": "5532", "operator": "CN", "service": "freight", "origin": "Hønefoss", "destination": "Alnabru", "at_stop": true }
            """);

        var punkt = TogLag.FraStasjon(tog, Alb);

        Assert.NotNull(punkt);
        Assert.Equal("Alnabru", punkt!.Properties["stasjon"]);
        Assert.Equal("godstog", punkt.Properties["type"]);
        Assert.Equal([Alb.Lon, Alb.Lat], punkt.Geometry.Coordinates);
    }

    [Fact]
    public void Tog_som_ikke_staar_paa_stasjonen_blir_forkastet()
    {
        var tog = Rad("""
            { "train": "2138", "operator": "VY", "service": "passenger", "origin": "Lillehammer", "destination": "Oslo S" }
            """);

        Assert.Null(TogLag.FraStasjon(tog, Osl));
    }

    [Fact]
    public void Innstilt_tog_blir_forkastet()
    {
        var tog = Rad("""
            { "train": "65", "operator": "VY", "service": "passenger", "origin": "Skien", "destination": "Oslo S", "departure_status": "cancelled", "at_stop": true }
            """);

        Assert.Null(TogLag.FraStasjon(tog, Osl));
    }

    [Fact]
    public void Samme_tog_med_gps_og_stasjon_vises_en_gang_med_gpsposisjonen()
    {
        var gps = Rad("""
            { "vehicle_id": "1137-2026-09-23", "mode": "RAIL", "line_name": "Stabekk-Oslo S-Moss", "lat": 59.91032, "lon": 10.755376 }
            """);
        var påStasjon = Rad("""
            { "train": "1137", "operator": "VY", "service": "passenger", "origin": "Stabekk", "destination": "Moss", "at_stop": true }
            """);

        var lag = TogLag.Samle([gps], [(påStasjon, Osl)]);

        Assert.Single(lag.Features);
        var punkt = lag.Features[0];
        Assert.False(punkt.Properties.ContainsKey("stasjon"));
        Assert.Equal([10.755376, 59.91032], punkt.Geometry.Coordinates);
    }

    [Fact]
    public void Gpspunkt_utenfor_utsnittet_blir_forkastet()
    {
        // Hamar, langt nord for kartutsnittet.
        var gps = Rad("""
            { "vehicle_id": "700-2026-09-23", "mode": "RAIL", "line_name": "Oslo S-Hamar", "lat": 60.79, "lon": 11.06 }
            """);

        var lag = TogLag.Samle([gps], []);

        Assert.Empty(lag.Features);
    }
}
