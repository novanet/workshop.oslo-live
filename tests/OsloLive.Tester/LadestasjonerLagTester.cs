using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class LadestasjonerLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string GallerietRad = """
        {
            "id": "NOR_04945",
            "name": "Galleriet Vest/Schweigaardsgate 4",
            "street": "Sonja Henies plass 1",
            "city": "OSLO",
            "municipality": "OSLO",
            "county": "Oslo",
            "lat": 59.91209,
            "lon": 10.75484,
            "operator": "Europark/Galleriet Parkering AS",
            "charging_points": 5,
            "available_points": null,
            "real_time_status": false,
            "open_24h": true,
            "location_description": "Parkeringshus",
            "distance_km": 0.354
        }
        """;

    [Fact]
    public void Ladestasjon_blir_punkt_med_id_navn_og_kilde()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad(GallerietRad));

        Assert.NotNull(punkt);
        Assert.Equal("NOR_04945", punkt!.Properties["id"]);
        Assert.Equal("Galleriet Vest/Schweigaardsgate 4", punkt.Properties["navn"]);
        Assert.Equal("NOBIL, Enova", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad(GallerietRad));

        Assert.NotNull(punkt);
        Assert.Equal(10.75484, punkt!.Geometry.Coordinates[0], 5);
        Assert.Equal(59.91209, punkt.Geometry.Coordinates[1], 5);
    }

    [Fact]
    public void Antall_ladepunkter_vises_naar_kilden_oppgir_det()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad(GallerietRad));

        Assert.NotNull(punkt);
        Assert.Equal(5, punkt!.Properties["ladepunkter"]);
    }

    [Fact]
    public void Ladepunkter_utelates_naar_tallet_mangler()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad("""
            {"id": "NOR_1", "name": "Test", "lat": 59.91, "lon": 10.75, "charging_points": null}
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("ladepunkter"));
    }

    [Fact]
    public void Ladepunkter_utelates_naar_feltet_ikke_finnes()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad("""
            {"id": "NOR_1", "name": "Test", "lat": 59.91, "lon": 10.75}
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("ladepunkter"));
    }

    [Fact]
    public void Ladestasjon_uten_navn_heter_ukjent_ladestasjon()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad("""
            {"id": "NOR_1", "name": null, "lat": 59.91, "lon": 10.75}
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent ladestasjon", punkt!.Properties["navn"]);
    }

    [Theory]
    [InlineData("""{"id": "NOR_1", "name": "Test", "lat": null, "lon": 10.75}""")]
    [InlineData("""{"id": "NOR_1", "name": "Test", "lat": 59.91}""")]
    public void Ladestasjon_uten_posisjon_blir_forkastet(string json)
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad(json));

        Assert.Null(punkt);
    }

    [Fact]
    public void Ladestasjon_uten_id_blir_forkastet()
    {
        var punkt = LadestasjonerLag.TilPunkt(Rad("""
            {"name": "Test", "lat": 59.91, "lon": 10.75}
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Ladestasjon_utenfor_utsnittet_blir_forkastet()
    {
        // Langt sør for Oslo-boksen (MinLat 59.80).
        var punkt = LadestasjonerLag.TilPunkt(Rad("""
            {"id": "NOR_1", "name": "Test", "lat": 59.10, "lon": 10.75}
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Parametrene_ber_om_liste_rundt_oslo_sentrum()
    {
        Assert.Equal(100, LadestasjonerLag.Parametre["limit"]);
        Assert.Equal(Geo.OsloLat, LadestasjonerLag.Parametre["lat"]);
        Assert.Equal(Geo.OsloLon, LadestasjonerLag.Parametre["lon"]);
    }
}
