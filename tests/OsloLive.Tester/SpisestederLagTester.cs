using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class SpisestederLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Fact]
    public void Punkt_har_navn_kilde_og_kategori()
    {
        var punkt = SpisestederLag.TilPunkt(Rad("""
            { "id": 5315431323, "lat": 59.9104989, "lon": 10.7512005, "type": "amenity", "category": "restaurant", "name": "Olivia" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Olivia", punkt!.Properties["navn"]);
        Assert.Equal("OpenStreetMap", punkt.Properties["kilde"]);
        Assert.Equal("restaurant", punkt.Properties["kategori"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var punkt = SpisestederLag.TilPunkt(Rad("""
            { "id": 1, "lat": 59.9104989, "lon": 10.7512005, "type": "amenity", "category": "cafe", "name": "Kaffebar" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal([10.7512005, 59.9104989], punkt!.Geometry.Coordinates);
    }

    [Theory]
    [InlineData("restaurant", "restaurant")]
    [InlineData("cafe", "kafé")]
    [InlineData("fast_food", "gatekjøkken")]
    [InlineData("bar", "bar")]
    [InlineData("pub", "pub")]
    public void Kategori_oversettes(string kildekategori, string forventet)
    {
        var punkt = SpisestederLag.TilPunkt(Rad($$"""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "{{kildekategori}}", "name": "Sted" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(forventet, punkt!.Properties["kategori"]);
    }

    [Theory]
    [InlineData("bench")]
    [InlineData("waste_basket")]
    [InlineData("parking")]
    public void Rad_som_ikke_er_spisested_gir_ikke_punkt(string kategori)
    {
        var punkt = SpisestederLag.TilPunkt(Rad($$"""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "{{kategori}}", "name": "Ikke et spisested" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_uten_kategori_gir_ikke_punkt()
    {
        var punkt = SpisestederLag.TilPunkt(Rad("""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "name": "Uten kategori" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Sted_utenfor_oslo_blir_forkastet()
    {
        var punkt = SpisestederLag.TilPunkt(Rad("""
            { "id": 1, "lat": 63.43, "lon": 10.39, "type": "amenity", "category": "restaurant", "name": "Trondheim-restaurant" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Sted_uten_navn_faar_standardnavn()
    {
        var punkt = SpisestederLag.TilPunkt(Rad("""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bar" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent spisested", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Samme_sted_to_ganger_gir_ett_punkt()
    {
        var rader = Liste("""
            [
                { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "restaurant", "name": "Olivia" },
                { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "restaurant", "name": "Olivia" }
            ]
            """);

        var lag = SpisestederLag.Samle(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Single(lag.Features);
    }

    [Fact]
    public void Kun_spisesteder_blir_med_i_laget()
    {
        var rader = Liste("""
            [
                { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "restaurant", "name": "Olivia" },
                { "id": 2, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bench", "name": "En benk" },
                { "id": 3, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "waste_basket", "name": "En søppelkasse" }
            ]
            """);

        var lag = SpisestederLag.Samle(rader);

        Assert.Single(lag.Features);
        Assert.Equal("Olivia", lag.Features[0].Properties["navn"]);
    }
}
