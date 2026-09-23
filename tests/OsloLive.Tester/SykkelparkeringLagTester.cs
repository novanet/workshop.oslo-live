using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class SykkelparkeringLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    private const string JernbanetorgetRad = """
        { "id": 5888766585, "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking", "name": "Jernbanetorget", "distance_km": 0.063 }
        """;

    [Fact]
    public void Punkt_har_navn_og_kilde()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad(JernbanetorgetRad));

        Assert.NotNull(punkt);
        Assert.Equal("Jernbanetorget", punkt!.Properties["navn"]);
        Assert.Equal("OpenStreetMap", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad(JernbanetorgetRad));

        Assert.NotNull(punkt);
        Assert.Equal([10.750642, 59.9104713], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Parkering_uten_navn_faar_standardnavn()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 5888766585, "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking", "distance_km": 0.063 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Sykkelparkering", punkt!.Properties["navn"]);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void Parkering_med_tomt_navn_faar_standardnavn(string navnJson)
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad($$"""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking", "name": {{navnJson}} }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Sykkelparkering", punkt!.Properties["navn"]);
    }

    [Theory]
    [InlineData("20")]
    [InlineData("\"20\"")]
    public void Plasser_vises_naar_kilden_oppgir_dem(string kapasitetJson)
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad($$"""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking", "capacity": {{kapasitetJson}} }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(20, punkt!.Properties["plasser"]);
    }

    [Theory]
    [InlineData("yes", "ja")]
    [InlineData("no", "nei")]
    public void Under_tak_vises_naar_kilden_oppgir_det(string kildeverdi, string forventet)
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad($$"""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking", "covered": "{{kildeverdi}}" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(forventet, punkt!.Properties["under tak"]);
    }

    [Fact]
    public void Plasser_og_tak_utelates_naar_kilden_mangler_dem()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad(JernbanetorgetRad));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("plasser"));
        Assert.False(punkt.Properties.ContainsKey("under tak"));
    }

    [Fact]
    public void Ugyldig_plasser_og_tak_utelates()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 1, "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking", "capacity": "mange", "covered": "kanskje" }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("plasser"));
        Assert.False(punkt.Properties.ContainsKey("under tak"));
    }

    [Fact]
    public void Id_som_tekst_godtas()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": "5888766585", "lat": 59.9104713, "lon": 10.750642, "type": "amenity", "category": "bicycle_parking" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("5888766585", punkt!.Properties["id"]);
    }

    [Fact]
    public void Rad_uten_id_blir_forkastet()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "lat": 59.91, "lon": 10.75, "type": "amenity", "category": "bicycle_parking" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_uten_posisjon_blir_forkastet()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 1, "lat": null, "lon": null, "type": "amenity", "category": "bicycle_parking" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Parkering_utenfor_oslo_blir_forkastet()
    {
        var punkt = SykkelparkeringLag.TilPunkt(Rad("""
            { "id": 1, "lat": 63.43, "lon": 10.39, "type": "amenity", "category": "bicycle_parking", "name": "Trondheim-parkering" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Samme_parkering_to_ganger_gir_ett_punkt()
    {
        var rader = Liste($$"""
            [ {{JernbanetorgetRad}}, {{JernbanetorgetRad}} ]
            """);

        var lag = SykkelparkeringLag.Samle(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Single(lag.Features);
    }
}
