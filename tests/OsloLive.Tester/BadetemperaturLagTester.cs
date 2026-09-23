using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class BadetemperaturLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Badeplass_blir_punkt_med_navn_kilde_temperatur_og_maalt()
    {
        var punkt = BadetemperaturLag.TilPunkt(Rad("""
            { "location_id": "10-1088746", "name": "Sørenga sjøbad", "lat": 59.9036, "lon": 10.7515, "temperature_c": 18.4, "time": "2026-07-01T10:00:00+02:00" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Sørenga sjøbad", punkt!.Properties["navn"]);
        Assert.Equal("Badetemperaturer fra Yr", punkt.Properties["kilde"]);
        Assert.Equal(18.4, punkt.Properties["temperatur"]);
        Assert.Equal("2026-07-01T10:00:00+02:00", punkt.Properties["målt"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = BadetemperaturLag.TilPunkt(Rad("""
            { "location_id": "10-1088746", "name": "Sørenga sjøbad", "lat": 59.9036, "lon": 10.7515, "temperature_c": 18.4, "time": "2026-07-01T10:00:00+02:00" }
            """));

        Assert.Equal([10.7515, 59.9036], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Badeplass_utenfor_utsnittet_blir_forkastet()
    {
        var punkt = BadetemperaturLag.TilPunkt(Rad("""
            { "location_id": "0-99999", "name": "Mjøsa", "lat": 61.115, "lon": 10.466, "temperature_c": 16, "time": "2026-07-01T10:00:00+02:00" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Badeplass_uten_temperatur_faar_null_temperatur()
    {
        var punkt = BadetemperaturLag.TilPunkt(Rad("""
            { "location_id": "10-1088746", "name": "Sørenga sjøbad", "lat": 59.9036, "lon": 10.7515, "time": "2026-07-01T10:00:00+02:00" }
            """));

        Assert.NotNull(punkt);
        Assert.Null(punkt!.Properties["temperatur"]);
    }

    [Fact]
    public void Samme_badeplass_to_ganger_gir_ett_punkt()
    {
        var rad = Rad("""
            { "location_id": "10-1088746", "name": "Sørenga sjøbad", "lat": 59.9036, "lon": 10.7515, "temperature_c": 18.4, "time": "2026-07-01T10:00:00+02:00" }
            """);

        var lag = Geo.Samle([BadetemperaturLag.TilPunkt(rad), BadetemperaturLag.TilPunkt(rad)]);

        Assert.Single(lag.Features);
    }
}
