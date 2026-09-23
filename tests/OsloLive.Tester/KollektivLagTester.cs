using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class KollektivLagTester
{
    private static readonly DateTimeOffset Nå = DateTimeOffset.Parse("2026-09-23T09:23:00Z");

    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Buss_blir_punkt_med_linje_type_destinasjon_og_navn()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "3620803606", "mode": "BUS", "line": "200", "line_name": "Hønefoss-Oslo",
              "destination": "Sollihøgda-Oslo", "lat": 59.9078292679042, "lon": 10.7547549996525,
              "codespace": "BRA", "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);

        Assert.NotNull(punkt);
        Assert.Equal("200 Sollihøgda-Oslo", punkt!.Properties["navn"]);
        Assert.Equal("200", punkt.Properties["linje"]);
        Assert.Equal("buss", punkt.Properties["type"]);
        Assert.Equal("Sollihøgda-Oslo", punkt.Properties["destinasjon"]);
        Assert.Equal("Entur sanntidsposisjoner", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "3620803606", "mode": "BUS", "line": "200",
              "lat": 59.9078292679042, "lon": 10.7547549996525, "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);

        Assert.Equal([10.7547549996525, 59.9078292679042], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Id_er_codespace_og_vehicle_id()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "3620803606", "mode": "BUS", "line": "200",
              "lat": 59.9078292679042, "lon": 10.7547549996525, "codespace": "BRA",
              "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);

        Assert.Equal("BRA:3620803606", punkt!.Properties["id"]);
    }

    [Fact]
    public void Tog_uten_destinasjon_bruker_linjenavn()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1624-2026-09-23", "mode": "RAIL", "line": "R13", "line_name": "Drammen-Oslo S-Dal",
              "destination": null, "lat": 59.911425, "lon": 10.752718, "codespace": "VYG",
              "last_updated": "2026-09-23T09:21:00.613Z" }
            """), Nå);

        Assert.NotNull(punkt);
        Assert.Equal("tog", punkt!.Properties["type"]);
        Assert.Equal("Drammen-Oslo S-Dal", punkt.Properties["destinasjon"]);
        Assert.Equal("R13 Drammen-Oslo S-Dal", punkt.Properties["navn"]);
    }

    [Theory]
    [InlineData("BUS", "buss")]
    [InlineData("COACH", "buss")]
    [InlineData("TRAM", "trikk")]
    [InlineData("METRO", "T-bane")]
    [InlineData("RAIL", "tog")]
    public void Mode_blir_norsk_type(string mode, string forventetType)
    {
        Assert.Equal(forventetType, KollektivLag.Type(mode));
    }

    [Fact]
    public void Buss_og_trikk_faar_ulikt_ikon()
    {
        var buss = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": 59.91, "lon": 10.75,
              "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);
        var trikk = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "2", "mode": "TRAM", "line": "17", "lat": 59.91, "lon": 10.75,
              "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);

        Assert.Equal("🚌", buss!.Properties["ikon"]);
        Assert.Equal("🚊", trikk!.Properties["ikon"]);
        Assert.NotEqual(buss.Properties["ikon"], trikk.Properties["ikon"]);
    }

    [Fact]
    public void Kjoeretoey_uten_vehicle_id_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "mode": "BUS", "line": "31", "lat": 59.91, "lon": 10.75 }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Kjoeretoey_uten_lat_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lon": 10.75 }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Kjoeretoey_uten_lon_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": 59.91 }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Kjoeretoey_med_null_posisjon_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": null, "lon": null }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Kjoeretoey_uten_mode_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "line": "31", "lat": 59.91, "lon": 10.75 }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Ferje_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "FERRY", "line": "F1", "lat": 59.91, "lon": 10.75 }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Gammel_posisjon_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "3620803610", "mode": "BUS", "line": "200", "lat": 59.91, "lon": 10.75,
              "last_updated": "2026-09-23T08:21:11Z" }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Kjoeretoey_uten_tidspunkt_beholdes()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": 59.91, "lon": 10.75 }
            """), Nå);

        Assert.NotNull(punkt);
    }

    [Fact]
    public void Kjoeretoey_utenfor_utsnittet_blir_forkastet()
    {
        var punkt = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": 60.1976, "lon": 11.1004,
              "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);

        Assert.Null(punkt);
    }

    [Fact]
    public void Nye_posisjoner_gir_nye_koordinater()
    {
        var første = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": 59.90, "lon": 10.70,
              "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);
        var andre = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "lat": 59.95, "lon": 10.80,
              "last_updated": "2026-09-23T09:22:45Z" }
            """), Nå);

        Assert.Equal(første!.Properties["id"], andre!.Properties["id"]);
        Assert.NotEqual(første.Geometry.Coordinates, andre.Geometry.Coordinates);
    }

    [Fact]
    public void Samle_gir_ett_punkt_per_kjoeretoey()
    {
        var a = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "codespace": "RUT", "lat": 59.90, "lon": 10.70,
              "last_updated": "2026-09-23T09:22:30Z" }
            """), Nå);
        var b = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "1", "mode": "BUS", "line": "31", "codespace": "RUT", "lat": 59.91, "lon": 10.71,
              "last_updated": "2026-09-23T09:22:45Z" }
            """), Nå);
        var c = KollektivLag.TilPunkt(Rad("""
            { "vehicle_id": "2", "mode": "TRAM", "line": "17", "codespace": "RUT", "lat": 59.91, "lon": 10.75,
              "last_updated": "2026-09-23T09:22:45Z" }
            """), Nå);

        var lag = OsloLive.Kart.Geo.Samle([a, b, c]);

        Assert.Equal(2, lag.Features.Count);
    }
}
