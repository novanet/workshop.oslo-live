using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class SykkelparkeringLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Fact]
    public void Punkt_har_navn_og_kilde()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 5888766585, "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking", "name": "Sykkelhotell Oslo S", "distance_km": 0.063 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("5888766585", punkt!.Properties["id"]);
        Assert.Equal("Sykkelhotell Oslo S", punkt.Properties["navn"]);
        Assert.Equal("OpenStreetMap", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Punkt_uten_navn_faar_standardnavn()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 5888766585, "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking", "distance_km": 0.063 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Sykkelparkering", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Punkt_med_null_navn_faar_standardnavn()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking", "name": null }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Sykkelparkering", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 5888766585, "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking", "distance_km": 0.063 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal([10.750642, 59.9104713], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Punkt_uten_plasser_og_tak_utelater_feltene()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 5888766585, "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking", "distance_km": 0.063 }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("plasser"));
        Assert.False(punkt.Properties.ContainsKey("under tak"));
    }

    [Fact]
    public void Parkering_utenfor_oslo_blir_forkastet()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 1, "lat": 63.43, "lon": 10.39, "type": "amenity", "category": "bicycle_parking" }
            """));

        Assert.Null(punkt);
    }

    [Theory]
    [InlineData("bench")]
    [InlineData("parking")]
    [InlineData("bicycle_rental")]
    public void Rad_med_annen_kategori_gir_ikke_punkt(string kategori)
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad($$"""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "{{kategori}}" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Samme_parkering_to_ganger_gir_ett_punkt()
    {
        var rader = Liste("""
            [
                { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking" },
                { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking" }
            ]
            """);

        var lag = SykkelparkeringLag.Samle(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Single(lag.Features);
    }

    [Fact]
    public void Hver_parkering_blir_ett_punkt()
    {
        var rader = Liste("""
            [
                { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking" },
                { "id": 2, "lat": 59.912, "lon": 10.752, "type": "amenity", "category": "bicycle_parking" },
                { "id": 3, "lat": 59.914, "lon": 10.754, "type": "amenity", "category": "bicycle_parking" }
            ]
            """);

        var lag = SykkelparkeringLag.Samle(rader);

        Assert.Equal(3, lag.Features.Count);
    }
}
