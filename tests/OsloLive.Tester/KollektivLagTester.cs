using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class KollektivLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Theory]
    [InlineData("BUS", "buss")]
    [InlineData("COACH", "buss")]
    [InlineData("TRAM", "trikk")]
    [InlineData("METRO", "T-bane")]
    [InlineData("RAIL", "tog")]
    [InlineData(null, "annet")]
    [InlineData("FERRY", "annet")]
    public void Type_oversettes_fra_modus(string? modus, string forventetType)
    {
        Assert.Equal(forventetType, KollektivLag.Type(modus));
    }

    [Theory]
    [InlineData("buss", "🚌")]
    [InlineData("trikk", "🚊")]
    [InlineData("T-bane", "🚇")]
    [InlineData("tog", "🚆")]
    public void Buss_og_trikk_har_ulikt_ikon(string type, string forventetIkon)
    {
        Assert.Equal(forventetIkon, KollektivLag.IkonFor(type));

        if (type == "buss")
        {
            Assert.NotEqual(KollektivLag.IkonFor("trikk"), KollektivLag.IkonFor(type));
        }
    }

    [Fact]
    public void Kjøretøy_gir_punkt_med_lon_lat_og_popupfelt()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            {
                "vehicle_id": "3620803610",
                "mode": "BUS",
                "line": "200",
                "line_name": "Hønefoss-Oslo",
                "origin": "Hønefoss sentrum",
                "destination": "Oslo",
                "lat": 59.9120711814612,
                "lon": 10.7575406413525,
                "last_updated": "2026-09-23T08:21:11Z"
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal([10.7575406413525, 59.9120711814612], punkt!.Geometry.Coordinates);
        Assert.Equal("Entur sanntidsposisjoner", punkt.Properties["kilde"]);
        Assert.Equal("200 til Oslo", punkt.Properties["navn"]);
        Assert.Equal("200", punkt.Properties["linje"]);
        Assert.Equal("buss", punkt.Properties["type"]);
        Assert.Equal("Oslo", punkt.Properties["destinasjon"]);
    }

    [Theory]
    [InlineData("""{ "mode": "BUS", "lat": 59.91, "lon": 10.75 }""")]
    [InlineData("""{ "vehicle_id": "1", "lon": 10.75 }""")]
    [InlineData("""{ "vehicle_id": "1", "lat": 59.91 }""")]
    public void Rad_uten_påkrevd_felt_gir_null(string json)
    {
        Assert.Null(KollektivLag.TilPunkt(Rad(json)));
    }

    [Fact]
    public void Manglende_linje_og_destinasjon_faller_tilbake_til_ukjent()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1522-2026-09-23", "mode": "RAIL", "lat": 59.9097, "lon": 10.7548 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("ukjent", punkt!.Properties["linje"]);
        Assert.Equal("ukjent", punkt.Properties["destinasjon"]);
        Assert.Equal("ukjent til ukjent", punkt.Properties["navn"]);
    }

    [Fact]
    public void Manglende_destinasjon_faller_tilbake_til_rutenavn()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1522-2026-09-23", "mode": "RAIL", "line": "R23", "line_name": "Stabekk-Oslo S-Moss", "lat": 59.9097, "lon": 10.7548 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("R23", punkt!.Properties["linje"]);
        Assert.Equal("Stabekk-Oslo S-Moss", punkt.Properties["destinasjon"]);
    }

    [Fact]
    public void Kjøretøy_utenfor_Oslo_utelates()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "lat": 63.43, "lon": 10.39 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Ett_punkt_per_kjøretøy()
    {
        var rader = Liste("""
            [
                { "vehicle_id": "A", "mode": "BUS", "lat": 59.91, "lon": 10.75, "last_updated": "2026-09-23T09:00:00Z" },
                { "vehicle_id": "B", "mode": "TRAM", "lat": 59.92, "lon": 10.74, "last_updated": "2026-09-23T09:00:00Z" },
                { "vehicle_id": "C", "mode": "RAIL", "lat": 59.93, "lon": 10.73, "last_updated": "2026-09-23T09:00:00Z" }
            ]
            """);

        var lag = KollektivLag.Samle(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(3, lag.Features.Count);
    }

    [Fact]
    public void Samme_kjøretøy_to_ganger_gir_ett_punkt_med_nyeste_posisjon()
    {
        var rader = Liste("""
            [
                { "vehicle_id": "A", "mode": "BUS", "lat": 59.90, "lon": 10.70, "last_updated": "2026-09-23T09:00:00Z" },
                { "vehicle_id": "A", "mode": "BUS", "lat": 59.91, "lon": 10.75, "last_updated": "2026-09-23T09:01:00Z" }
            ]
            """);

        var lag = KollektivLag.Samle(rader);

        Assert.Single(lag.Features);
        Assert.Equal([10.75, 59.91], lag.Features[0].Geometry.Coordinates);
    }

    [Fact]
    public void Nye_posisjoner_gir_nye_koordinater()
    {
        var førsteKall = KollektivLag.Samle(Liste("""
            [ { "vehicle_id": "A", "mode": "BUS", "lat": 59.90, "lon": 10.70, "last_updated": "2026-09-23T09:00:00Z" } ]
            """));
        var andreKall = KollektivLag.Samle(Liste("""
            [ { "vehicle_id": "A", "mode": "BUS", "lat": 59.92, "lon": 10.77, "last_updated": "2026-09-23T09:00:35Z" } ]
            """));

        Assert.NotEqual(førsteKall.Features[0].Geometry.Coordinates, andreKall.Features[0].Geometry.Coordinates);
    }
}
