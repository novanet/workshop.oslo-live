using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class SkolerLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string GrunnskoleRad = """
        {
            "organization_id": "974590328",
            "Navn": "Uranienborg skole",
            "Kommunenr": "0301",
            "ErAktiv": true,
            "ErSkole": true,
            "ErGrunnskole": true,
            "ErVideregaaendeSkole": false,
            "ErPrivatskole": false,
            "ErOffentligSkole": true
        }
        """;

    private const string BarnehageRad = """
        {
            "organization_id": "994174525",
            "Navn": "Testbarnehagen",
            "ErAktiv": true,
            "ErBarnehage": true,
            "ErOffentligBarnehage": false,
            "ErPrivatBarnehage": true
        }
        """;

    private static readonly SkolerLag.Enhet Sentrum = new(59.91101, 10.78188, "Ullensakergata 13, 0655 OSLO");

    [Theory]
    [InlineData("""{"ErGrunnskole": true, "ErVideregaaendeSkole": false}""", false, "grunnskole")]
    [InlineData("""{"ErGrunnskole": false, "ErVideregaaendeSkole": true}""", false, "videregående")]
    [InlineData("""{"ErGrunnskole": true, "ErVideregaaendeSkole": true}""", false, "grunnskole")]
    [InlineData("""{}""", true, "barnehage")]
    public void Type_avledes_fra_flaggene(string json, bool erBarnehage, string forventet)
    {
        Assert.Equal(forventet, SkolerLag.Type(Rad(json), erBarnehage));
    }

    [Fact]
    public void Skole_uten_grunnskole_eller_vgs_flagg_gir_ingen_type()
    {
        var rad = Rad("""{"organization_id": "1", "Navn": "1080 Map AS", "ErGrunnskole": false, "ErVideregaaendeSkole": false}""");

        Assert.Null(SkolerLag.Type(rad, erBarnehage: false));
        var lag = SkolerLag.Bygg([(rad, false)], _ => Sentrum);
        Assert.Empty(lag.Features);
    }

    [Theory]
    [InlineData("""{"ErPrivatskole": true}""", "grunnskole", "privat")]
    [InlineData("""{"ErPrivatskole": false, "ErOffentligSkole": true}""", "grunnskole", "kommunal")]
    [InlineData("""{"ErPrivatskole": false, "ErOffentligSkole": true}""", "videregående", "fylkeskommunal")]
    [InlineData("""{"ErPrivatBarnehage": true}""", "barnehage", "privat")]
    [InlineData("""{"ErOffentligBarnehage": true}""", "barnehage", "kommunal")]
    public void Eierform_avledes_fra_flagg_og_type(string json, string type, string forventet)
    {
        Assert.Equal(forventet, SkolerLag.Eierform(Rad(json), type));
    }

    [Fact]
    public void Punkt_har_navn_og_kilde()
    {
        var punkt = SkolerLag.TilPunkt(Rad(GrunnskoleRad), erBarnehage: false, Sentrum);

        Assert.NotNull(punkt);
        Assert.Equal("Uranienborg skole", punkt!.Properties["navn"]);
        Assert.Equal("Nasjonalt skoleregister / barnehageregister (Udir)", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Enhet_uten_navn_faar_standardnavn()
    {
        var skole = SkolerLag.TilPunkt(Rad("""{"organization_id": "1", "ErGrunnskole": true}"""), erBarnehage: false, Sentrum);
        var barnehage = SkolerLag.TilPunkt(Rad("""{"organization_id": "2"}"""), erBarnehage: true, Sentrum);

        Assert.Equal("Ukjent skole", skole!.Properties["navn"]);
        Assert.Equal("Ukjent barnehage", barnehage!.Properties["navn"]);
    }

    [Fact]
    public void Punkt_har_type_og_eierform()
    {
        var skole = SkolerLag.TilPunkt(Rad(GrunnskoleRad), erBarnehage: false, Sentrum);
        var barnehage = SkolerLag.TilPunkt(Rad(BarnehageRad), erBarnehage: true, Sentrum);

        Assert.Equal("grunnskole", skole!.Properties["type"]);
        Assert.Equal("kommunal", skole.Properties["eierform"]);
        Assert.Equal("barnehage", barnehage!.Properties["type"]);
        Assert.Equal("privat", barnehage.Properties["eierform"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var punkt = SkolerLag.TilPunkt(Rad(GrunnskoleRad), erBarnehage: false, Sentrum);

        Assert.Equal([10.78188, 59.91101], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Samme_enhet_to_ganger_gir_ett_punkt()
    {
        var rad = Rad(GrunnskoleRad);

        var lag = SkolerLag.Bygg([(rad, false), (rad, false)], _ => Sentrum);

        Assert.Single(lag.Features);
    }

    [Fact]
    public void Skole_og_barnehage_med_samme_orgnr_gir_to_punkter()
    {
        var skole = Rad("""{"organization_id": "123", "ErGrunnskole": true}""");
        var barnehage = Rad("""{"organization_id": "123"}""");

        var lag = SkolerLag.Bygg([(skole, false), (barnehage, true)], _ => Sentrum);

        Assert.Equal(2, lag.Features.Count);
        Assert.Contains(lag.Features, f => (string)f.Properties["id"]! == "skole:123");
        Assert.Contains(lag.Features, f => (string)f.Properties["id"]! == "barnehage:123");
    }

    [Fact]
    public void Enhet_utenfor_oslo_forkastes()
    {
        var rad = Rad(GrunnskoleRad);
        var lillehammer = new SkolerLag.Enhet(61.115, 10.466, null);

        var lag = SkolerLag.Bygg([(rad, false)], _ => lillehammer);

        Assert.Empty(lag.Features);
    }

    [Fact]
    public void Enhet_uten_oppslag_gir_ikke_punkt()
    {
        var rad = Rad(GrunnskoleRad);

        var lag = SkolerLag.Bygg([(rad, false)], _ => null);

        Assert.Empty(lag.Features);
    }

    [Fact]
    public void Rad_med_orgnr_som_tall_hoppes_over()
    {
        var rad = Rad("""{"organization_id": 123, "ErGrunnskole": true}""");

        var punkt = SkolerLag.TilPunkt(rad, erBarnehage: false, Sentrum);

        Assert.Null(punkt);
    }

    [Fact]
    public void TilEnhet_leser_koordinat_og_adresse()
    {
        var enhet = SkolerLag.TilEnhet(Rad("""
            {
                "Navn": "Uranienborg skole",
                "ErAktiv": true,
                "Koordinat": {"Lengdegrad": 10.78188, "Breddegrad": 59.91101, "Zoom": 15, "GeoKilde": "GeoNorge"},
                "Beliggenhetsadresse": {"Adresse": "Ullensakergata 13", "Postnr": "0655", "Poststed": "OSLO"}
            }
            """));

        Assert.NotNull(enhet);
        Assert.Equal(59.91101, enhet!.Lat);
        Assert.Equal(10.78188, enhet.Lon);
        Assert.Equal("Ullensakergata 13, 0655 OSLO", enhet.Adresse);
    }

    [Fact]
    public void TilEnhet_nedlagt_gir_null()
    {
        var enhet = SkolerLag.TilEnhet(Rad("""
            {"ErAktiv": false, "Koordinat": {"Lengdegrad": 10.78188, "Breddegrad": 59.91101}}
            """));

        Assert.Null(enhet);
    }

    [Fact]
    public void TilEnhet_uten_koordinat_gir_null()
    {
        Assert.Null(SkolerLag.TilEnhet(Rad("""{"ErAktiv": true}""")));
    }

    [Fact]
    public void TilEnhet_med_koordinat_som_tekst_gir_null()
    {
        var enhet = SkolerLag.TilEnhet(Rad("""
            {"ErAktiv": true, "Koordinat": {"Lengdegrad": "10.78188", "Breddegrad": "59.91101"}}
            """));

        Assert.Null(enhet);
    }

    [Fact]
    public void Full_side_uten_total_er_ikke_siste_side()
    {
        Assert.False(SkolerLag.ErSisteSide(antallRader: 100, nesteOffset: 100, total: null));
    }

    [Fact]
    public void Ufull_side_uten_total_er_siste_side()
    {
        Assert.True(SkolerLag.ErSisteSide(antallRader: 42, nesteOffset: 142, total: null));
    }

    [Fact]
    public void Tom_side_er_siste_side()
    {
        Assert.True(SkolerLag.ErSisteSide(antallRader: 0, nesteOffset: 200, total: null));
    }

    [Fact]
    public void Full_side_som_naar_oppgitt_total_er_siste_side()
    {
        Assert.True(SkolerLag.ErSisteSide(antallRader: 100, nesteOffset: 200, total: 200));
    }

    [Fact]
    public void Full_side_under_oppgitt_total_er_ikke_siste_side()
    {
        Assert.False(SkolerLag.ErSisteSide(antallRader: 100, nesteOffset: 100, total: 578));
    }
}
