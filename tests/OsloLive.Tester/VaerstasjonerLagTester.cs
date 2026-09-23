using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class VaerstasjonerLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string BlindernRad = """
        {
            "id": "SN18700",
            "name": "OSLO - BLINDERN",
            "short_name": "Oslo (Blindern)",
            "county": "OSLO",
            "municipality": "OSLO",
            "lon": 10.72,
            "lat": 59.9423,
            "masl": 94,
            "wmo_id": 1492,
            "valid_from": "1931-01-01"
        }
        """;

    [Fact]
    public void Parametrene_ber_om_stasjoner_i_Oslo_kommune()
    {
        var url = Allemannsdata.ByggUrl("frost", "find_stations", VaerstasjonerLag.Parametre);

        Assert.Contains("municipality=Oslo", url);
    }

    [Fact]
    public void Stasjon_blir_punkt_med_id_kort_navn_og_kilde()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad(BlindernRad));

        Assert.NotNull(punkt);
        Assert.Equal("SN18700", punkt!.Properties["id"]);
        Assert.Equal("Oslo (Blindern)", punkt.Properties["navn"]);
        Assert.Equal("Meteorologisk institutt (Frost)", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad(BlindernRad));

        Assert.Equal([10.72, 59.9423], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Moh_og_i_drift_siden_vises_naar_kilden_oppgir_dem()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad(BlindernRad));

        Assert.NotNull(punkt);
        Assert.Equal(94, punkt!.Properties["moh"]);
        Assert.Equal("1931-01-01", punkt.Properties["i drift siden"]);
    }

    [Fact]
    public void Stasjon_uten_kort_navn_bruker_det_lange_navnet()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad("""
            {
                "id": "SN18269", "name": "OSLO - HAUGENSTUA", "short_name": null,
                "lon": 10.9035, "lat": 59.9535, "masl": 123, "valid_from": "2000-01-01"
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("OSLO - HAUGENSTUA", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Stasjon_uten_moh_mangler_mohfeltet()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad("""
            {
                "id": "SN18269", "name": "OSLO - HAUGENSTUA", "short_name": "Haugenstua",
                "lon": 10.9035, "lat": 59.9535, "valid_from": "2000-01-01"
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("moh"));
    }

    [Fact]
    public void Stasjon_uten_valid_from_mangler_i_drift_siden()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad("""
            {
                "id": "SN18269", "name": "OSLO - HAUGENSTUA", "short_name": "Haugenstua",
                "lon": 10.9035, "lat": 59.9535, "masl": 123
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("i drift siden"));
    }

    [Fact]
    public void Stasjon_uten_posisjon_blir_forkastet()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad("""
            {
                "id": "SN18269", "name": "OSLO - HAUGENSTUA", "short_name": "Haugenstua",
                "lon": null, "lat": null
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Stasjon_uten_id_blir_forkastet()
    {
        var punkt = VaerstasjonerLag.TilPunkt(Rad("""
            {
                "name": "OSLO - HAUGENSTUA", "short_name": "Haugenstua",
                "lon": 10.9035, "lat": 59.9535
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Stasjon_utenfor_utsnittet_blir_forkastet()
    {
        // Langt sør for Oslo-boksen (MinLat 59.80).
        var punkt = VaerstasjonerLag.TilPunkt(Rad("""
            {
                "id": "SN99999", "name": "TESTSTASJON", "short_name": "Test",
                "lon": 10.75, "lat": 59.50
            }
            """));

        Assert.Null(punkt);
    }
}
