using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class SmilefjesLagTester
{
    [Theory]
    [InlineData(0, "blid", "😊")]
    [InlineData(1, "blid", "😊")]
    [InlineData(2, "streng", "😐")]
    [InlineData(3, "sur", "😠")]
    public void Karakter_gir_blid_streng_og_sur(int totalKarakter, string forventetKarakter, string forventetIkon)
    {
        var karakter = SmilefjesLag.Karakter(totalKarakter);

        Assert.Equal((forventetKarakter, forventetIkon), karakter);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    public void Ukjent_karakter_gir_ingen_karakter(int totalKarakter)
    {
        Assert.Null(SmilefjesLag.Karakter(totalKarakter));
    }

    [Fact]
    public void Ulike_karakterer_gir_ulike_ikoner()
    {
        var ikoner = new[] { SmilefjesLag.Karakter(0)!.Value.Ikon, SmilefjesLag.Karakter(2)!.Value.Ikon, SmilefjesLag.Karakter(3)!.Value.Ikon };

        Assert.Equal(3, ikoner.Distinct().Count());
    }

    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Punkt_har_navn_kilde_karakter_tilsyn_og_adresse()
    {
        var rad = Rad("""
            {
                "establishment_id": "sted-1",
                "navn": "Testkafé",
                "adresse": "Karl Johans gate 1",
                "postnr": "0154",
                "poststed": "Oslo",
                "dato": "2026-03-05",
                "karakter": 2
            }
            """);

        var punkt = SmilefjesLag.TilPunkt(rad, 59.9139, 10.7522);

        Assert.NotNull(punkt);
        Assert.Equal("Testkafé", punkt!.Properties["navn"]);
        Assert.Equal("Smilefjes fra Mattilsynet", punkt.Properties["kilde"]);
        Assert.Equal("streng", punkt.Properties["karakter"]);
        Assert.Equal("2026-03-05", punkt.Properties["tilsyn"]);
        Assert.Equal("Karl Johans gate 1, 0154 Oslo", punkt.Properties["adresse"]);
        Assert.Equal("😐", punkt.Properties["ikon"]);
    }

    [Fact]
    public void Punkt_bruker_samme_koordinatrekkefoelge_som_Geo()
    {
        var rad = Rad("""{"establishment_id": "sted-1", "navn": "Testkafé", "karakter": 0}""");

        var punkt = SmilefjesLag.TilPunkt(rad, 59.9139, 10.7522);
        var forventet = Geo.Lag("sted-1", 59.9139, 10.7522, "Testkafé", "kilde");

        Assert.Equal(forventet!.Geometry.Coordinates, punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Sted_uten_registrert_karakter_gir_ikke_punkt()
    {
        var rad = Rad("""{"establishment_id": "sted-1", "navn": "Testkafé", "karakter": null}""");

        Assert.Null(SmilefjesLag.TilPunkt(rad, 59.9139, 10.7522));
    }

    [Fact]
    public void Sted_uten_navn_faar_standardnavn()
    {
        var rad = Rad("""{"establishment_id": "sted-1", "karakter": 0}""");

        var punkt = SmilefjesLag.TilPunkt(rad, 59.9139, 10.7522);

        Assert.Equal("Ukjent spisested", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Adresse_uten_postnummer_blir_likevel_lesbar()
    {
        var rad = Rad("""{"adresse": "Storgata 1", "poststed": "Oslo"}""");

        Assert.Equal("Storgata 1, Oslo", SmilefjesLag.Adresse(rad));
    }

    [Fact]
    public void Adresse_uten_noen_felt_blir_tom_streng()
    {
        var rad = Rad("{}");

        Assert.Equal("", SmilefjesLag.Adresse(rad));
    }

    [Fact]
    public void Sted_uten_koordinat_blir_hoppet_over()
    {
        var rad = Rad("""{"establishment_id": "sted-1", "navn": "Testkafé", "karakter": 0}""");

        var lag = SmilefjesLag.Bygg([rad], _ => null);

        Assert.Empty(lag.Features);
    }

    [Fact]
    public void Sted_utenfor_oslo_blir_forkastet()
    {
        var rad = Rad("""{"establishment_id": "sted-1", "navn": "Testkafé", "karakter": 0}""");

        var lag = SmilefjesLag.Bygg([rad], _ => (61.115, 10.466)); // Lillehammer

        Assert.Empty(lag.Features);
    }

    [Fact]
    public void Sted_med_koordinat_og_karakter_gir_punkt()
    {
        var rad = Rad("""{"establishment_id": "sted-1", "navn": "Testkafé", "karakter": 0}""");

        var lag = SmilefjesLag.Bygg([rad], _ => (59.9139, 10.7522));

        Assert.Single(lag.Features);
    }

    [Fact]
    public void Full_side_uten_total_er_ikke_siste_side()
    {
        Assert.False(SmilefjesLag.ErSisteSide(antallRader: 100, nesteOffset: 100, total: null));
    }

    [Fact]
    public void Ufull_side_uten_total_er_siste_side()
    {
        Assert.True(SmilefjesLag.ErSisteSide(antallRader: 42, nesteOffset: 142, total: null));
    }

    [Fact]
    public void Tom_side_er_siste_side()
    {
        Assert.True(SmilefjesLag.ErSisteSide(antallRader: 0, nesteOffset: 200, total: null));
    }

    [Fact]
    public void Full_side_som_naar_oppgitt_total_er_siste_side()
    {
        Assert.True(SmilefjesLag.ErSisteSide(antallRader: 100, nesteOffset: 200, total: 200));
    }

    [Fact]
    public void Full_side_under_oppgitt_total_er_ikke_siste_side()
    {
        Assert.False(SmilefjesLag.ErSisteSide(antallRader: 100, nesteOffset: 100, total: 1364));
    }
}
