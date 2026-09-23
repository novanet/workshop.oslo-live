using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class KaierLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string SaltBryggeRad = """
        {
            "port_id": 2467069,
            "navn": "Salt brygge FK",
            "kode": "NOOSL",
            "type": "Ferjekai",
            "kommune": "Oslo",
            "fylke": "Oslo",
            "lat": 59.906647,
            "lon": 10.747188,
            "avstand_km": 0.4
        }
        """;

    [Fact]
    public void Parametrene_ber_om_radius_og_hele_siden()
    {
        var url = Allemannsdata.ByggUrl("kystdatahuset", "find_ports_nearby", KaierLag.Parametre);

        Assert.Contains("radius_km=20", url);
        Assert.Contains("limit=100", url);
    }

    [Fact]
    public void Kai_blir_punkt_med_port_id_navn_og_kilde()
    {
        var punkt = KaierLag.TilPunkt(Rad(SaltBryggeRad));

        Assert.NotNull(punkt);
        Assert.Equal("2467069", punkt!.Properties["id"]);
        Assert.Equal("Salt brygge FK", punkt.Properties["navn"]);
        Assert.Equal("Kystverket Kystdatahuset", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = KaierLag.TilPunkt(Rad(SaltBryggeRad));

        Assert.Equal([10.747188, 59.906647], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Type_vises_naar_kilden_oppgir_den()
    {
        var punkt = KaierLag.TilPunkt(Rad(SaltBryggeRad));

        Assert.NotNull(punkt);
        Assert.Equal("Ferjekai", punkt!.Properties["type"]);
    }

    [Fact]
    public void Kai_uten_type_mangler_typefeltet()
    {
        var punkt = KaierLag.TilPunkt(Rad("""
            {
                "port_id": 1, "navn": "Test", "kode": "NOOSL", "type": null,
                "lat": 59.91, "lon": 10.75
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("type"));
    }

    [Fact]
    public void Kai_med_tom_type_mangler_typefeltet()
    {
        var punkt = KaierLag.TilPunkt(Rad("""
            {
                "port_id": 1, "navn": "Test", "kode": "NOOSL", "type": "",
                "lat": 59.91, "lon": 10.75
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("type"));
    }

    [Fact]
    public void Kai_uten_navn_heter_kai()
    {
        var punkt = KaierLag.TilPunkt(Rad("""
            {
                "port_id": 1, "navn": null, "kode": "NOOSL", "type": "Kai",
                "lat": 59.91, "lon": 10.75
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Kai", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Kai_uten_posisjon_blir_forkastet()
    {
        var punkt = KaierLag.TilPunkt(Rad("""
            {
                "port_id": 1, "navn": "Test", "kode": "NOOSL", "type": "Kai",
                "lat": null, "lon": null
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Kai_uten_port_id_blir_forkastet()
    {
        var punkt = KaierLag.TilPunkt(Rad("""
            {
                "navn": "Test", "kode": "NOOSL", "type": "Kai",
                "lat": 59.91, "lon": 10.75
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Kai_utenfor_utsnittet_blir_forkastet()
    {
        // Ildjernet FK, langt sør for Oslo-boksen (MinLat 59.80).
        var punkt = KaierLag.TilPunkt(Rad("""
            {
                "port_id": 2467019, "navn": "Ildjernet FK", "kode": "NONEO", "type": "Ferjekai",
                "lat": 59.70, "lon": 10.60
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Kaier_med_samme_kode_blir_ikke_slaatt_sammen()
    {
        var revierkaiaKai = Rad("""
            {
                "port_id": 2460477, "navn": "Revierkaia", "kode": "NOOSL", "type": "Kai",
                "lat": 59.905834, "lon": 10.746333
            }
            """);
        var revierkaiaHavneanlegg = Rad("""
            {
                "port_id": 2456236, "navn": "Revierkaia", "kode": "NOOSL", "type": "Havneanlegg",
                "lat": 59.905834, "lon": 10.746333
            }
            """);

        var lag = Geo.Samle([KaierLag.TilPunkt(revierkaiaKai), KaierLag.TilPunkt(revierkaiaHavneanlegg)]);

        Assert.Equal(2, lag.Features.Count);
    }
}
