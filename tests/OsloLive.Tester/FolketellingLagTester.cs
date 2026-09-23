using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class FolketellingLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string KarlstadgateRad = """
        {
            "property_id": "gf01036392173180",
            "gaardsnavn_gateadr": "Karlstadgate 11",
            "fylke": "Oslo",
            "kommune_sokn": "Kristiania",
            "coordinates": { "lat": 59.924763789872, "lon": 10.764700216189 },
            "source": "Folketelling 1910 for 0301 Kristiania kjøpstad"
        }
        """;

    [Fact]
    public void Parametrene_ber_om_soekeordet_og_minst_50_eiendommer()
    {
        var url = Allemannsdata.ByggUrl("arkivverket", "search_census_properties", FolketellingLag.Parametre);

        Assert.Contains("query=Kristiania", url);
        Assert.Contains("limit=50", url);
    }

    [Fact]
    public void Eiendom_blir_punkt_med_id_navn_og_kilde()
    {
        var punkt = FolketellingLag.TilPunkt(Rad(KarlstadgateRad));

        Assert.NotNull(punkt);
        Assert.Equal("gf01036392173180", punkt!.Properties["id"]);
        Assert.Equal("Karlstadgate 11", punkt.Properties["navn"]);
        Assert.Equal("Digitalarkivet, Arkivverket", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = FolketellingLag.TilPunkt(Rad(KarlstadgateRad));

        Assert.Equal([10.764700216189, 59.924763789872], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Gaardsnr_bruksnr_og_telling_vises_naar_kilden_oppgir_dem()
    {
        var punkt = FolketellingLag.TilPunkt(Rad("""
            {
                "property_id": "bf01036372028453",
                "gaardsnavn_gateadr": "Kristiania metalvæveri og staalnetfabrik",
                "gaardsnr": "132",
                "bruksnr": "16",
                "coordinates": { "lat": 59.916162038291, "lon": 10.808059023187 },
                "source": "Folketelling 1910 for 0218 Aker herred"
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("132", punkt!.Properties["gårdsnr"]);
        Assert.Equal("16", punkt.Properties["bruksnr"]);
        Assert.Equal("Folketelling 1910 for 0218 Aker herred", punkt.Properties["telling"]);
    }

    [Fact]
    public void Eiendom_uten_gaardsnr_bruksnr_eller_telling_mangler_feltene()
    {
        var punkt = FolketellingLag.TilPunkt(Rad("""
            {
                "property_id": "gf01036392173180",
                "gaardsnavn_gateadr": "Karlstadgate 11",
                "coordinates": { "lat": 59.924763789872, "lon": 10.764700216189 },
                "source": null
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("gårdsnr"));
        Assert.False(punkt.Properties.ContainsKey("bruksnr"));
        Assert.False(punkt.Properties.ContainsKey("telling"));
    }

    [Fact]
    public void Eiendom_uten_navn_heter_eiendom()
    {
        var punkt = FolketellingLag.TilPunkt(Rad("""
            {
                "property_id": "gf01036392175632",
                "gaardsnavn_gateadr": null,
                "coordinates": { "lat": 59.9139, "lon": 10.7522 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Eiendom", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Eiendom_uten_koordinater_blir_forkastet()
    {
        // Kjøbenhavnsgate 1 mangler «coordinates» helt i et ekte svar.
        var punkt = FolketellingLag.TilPunkt(Rad("""
            {
                "property_id": "gf01036392175632",
                "gaardsnavn_gateadr": "Kjøbenhavnsgate 1",
                "source": "Folketelling 1910 for 0301 Kristiania kjøpstad"
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Eiendom_uten_property_id_blir_forkastet()
    {
        var punkt = FolketellingLag.TilPunkt(Rad("""
            {
                "gaardsnavn_gateadr": "Karlstadgate 11",
                "coordinates": { "lat": 59.924763789872, "lon": 10.764700216189 }
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Eiendom_utenfor_utsnittet_blir_forkastet()
    {
        // Langt sør for Oslo-boksen (MinLat 59.80).
        var punkt = FolketellingLag.TilPunkt(Rad("""
            {
                "property_id": "gf01036392177436",
                "gaardsnavn_gateadr": "Langgate 2",
                "coordinates": { "lat": 59.70, "lon": 10.83 }
            }
            """));

        Assert.Null(punkt);
    }
}
