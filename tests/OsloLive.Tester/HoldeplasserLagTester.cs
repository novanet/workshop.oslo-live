using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class HoldeplasserLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Fact]
    public void Punkt_har_navn_kilde_og_transport()
    {
        var punkt = HoldeplasserLag.TilPunkt(Rad("""
            { "id": "NSR:StopPlace:59872", "name": "Oslo S", "lat": 59.910357, "lon": 10.753051, "category": ["onstreetBus", "railStation"] }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Oslo S", punkt!.Properties["navn"]);
        Assert.Equal("Entur", punkt.Properties["kilde"]);
        Assert.Equal("buss, tog", punkt.Properties["transport"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var punkt = HoldeplasserLag.TilPunkt(Rad("""
            { "id": "NSR:StopPlace:59872", "name": "Oslo S", "lat": 59.910357, "lon": 10.753051, "category": ["onstreetBus"] }
            """));

        Assert.NotNull(punkt);
        Assert.Equal([10.753051, 59.910357], punkt!.Geometry.Coordinates);
    }

    [Theory]
    [InlineData(new[] { "onstreetBus" }, "buss")]
    [InlineData(new[] { "busStation" }, "buss")]
    [InlineData(new[] { "onstreetTram" }, "trikk")]
    [InlineData(new[] { "metroStation" }, "T-bane")]
    [InlineData(new[] { "railStation" }, "tog")]
    [InlineData(new[] { "harbourPort" }, "båt")]
    [InlineData(new[] { "ferryStop" }, "båt")]
    [InlineData(new[] { "metroStation", "onstreetTram", "onstreetBus" }, "buss, trikk, T-bane")]
    [InlineData(new[] { "onstreetBus", "onstreetBus" }, "buss")]
    public void Transport_oversettes(string[] kategorier, string forventet)
    {
        Assert.Equal(forventet, HoldeplasserLag.Transport(kategorier));
    }

    [Fact]
    public void Rad_uten_kjent_kategori_gir_ukjent_transport()
    {
        var punkt = HoldeplasserLag.TilPunkt(Rad("""
            { "id": "NSR:StopPlace:1", "name": "Ukategorisert", "lat": 59.91, "lon": 10.75, "category": ["groupOfStopPlaces"] }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("ukjent", punkt!.Properties["transport"]);
    }

    [Fact]
    public void Holdeplass_uten_navn_faar_standardnavn()
    {
        var punkt = HoldeplasserLag.TilPunkt(Rad("""
            { "id": "NSR:StopPlace:1", "lat": 59.91, "lon": 10.75, "category": ["onstreetBus"] }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent holdeplass", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Holdeplass_utenfor_oslo_blir_forkastet()
    {
        var punkt = HoldeplasserLag.TilPunkt(Rad("""
            { "id": "NSR:StopPlace:2", "name": "Bergen busstasjon", "lat": 60.389, "lon": 5.33, "category": ["busStation"] }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Samme_holdeplass_to_ganger_gir_ett_punkt()
    {
        var rader = Liste("""
            [
                { "id": "NSR:StopPlace:59872", "name": "Oslo S", "lat": 59.910357, "lon": 10.753051, "category": ["onstreetBus"] },
                { "id": "NSR:StopPlace:59872", "name": "Oslo S", "lat": 59.910357, "lon": 10.753051, "category": ["onstreetBus"] }
            ]
            """);

        var lag = HoldeplasserLag.Samle(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Single(lag.Features);
    }

    [Fact]
    public void Samling_forkaster_treff_utenfor_oslo_men_beholder_de_innenfor()
    {
        var rader = Liste("""
            [
                { "id": "NSR:StopPlace:59872", "name": "Oslo S", "lat": 59.910357, "lon": 10.753051, "category": ["onstreetBus"] },
                { "id": "NSR:StopPlace:99999", "name": "Frogner, Gran", "lat": 60.416424, "lon": 10.507133, "category": ["onstreetBus"] }
            ]
            """);

        var lag = HoldeplasserLag.Samle(rader);

        Assert.Single(lag.Features);
        Assert.Equal("Oslo S", lag.Features[0].Properties["navn"]);
    }
}
