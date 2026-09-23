using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class ArbeidsplasserLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Virksomhet_blir_punkt_med_navn_og_kilde()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Oslo Universitetssykehus HF",
              "employees": 25000,
              "industry_description": "Spesialisthelsetjenester",
              "location": { "lat": 59.9300, "lon": 10.7350 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Oslo Universitetssykehus HF", punkt!.Properties["navn"]);
        Assert.Equal("Firmafakta", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Oslo Universitetssykehus HF",
              "location": { "lat": 59.9300, "lon": 10.7350 }
            }
            """));

        Assert.Equal([10.7350, 59.9300], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Orgnummer_brukes_som_id()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Testfirma AS",
              "location": { "lat": 59.9139, "lon": 10.7522 }
            }
            """));

        Assert.Equal("123456789", punkt!.Properties["id"]);
    }

    [Fact]
    public void Ansatte_og_bransje_med_naar_kilden_gir_dem()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Testfirma AS",
              "employees": 1200,
              "industry_description": "Programvareutvikling",
              "location": { "lat": 59.9139, "lon": 10.7522 }
            }
            """));

        Assert.Equal(1200, punkt!.Properties["ansatte"]);
        Assert.Equal("Programvareutvikling", punkt.Properties["bransje"]);
    }

    [Fact]
    public void Manglende_ansatte_og_bransje_utelates()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Testfirma AS",
              "location": { "lat": 59.9139, "lon": 10.7522 }
            }
            """));

        Assert.False(punkt!.Properties.ContainsKey("ansatte"));
        Assert.False(punkt.Properties.ContainsKey("bransje"));
    }

    [Fact]
    public void Rad_uten_koordinater_hoppes_over()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Testfirma AS",
              "employees": 600
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_med_null_koordinater_hoppes_over()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "123456789",
              "name": "Testfirma AS",
              "location": null
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Virksomhet_utenfor_utsnittet_forkastes()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
              "organization_number": "987654321",
              "name": "Utenfor AS",
              "employees": 1000,
              "location": { "lat": 63.4, "lon": 10.4 }
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Samme_virksomhet_to_ganger_gir_ett_punkt()
    {
        var rad = Rad("""
            {
              "organization_number": "123456789",
              "name": "Testfirma AS",
              "location": { "lat": 59.9139, "lon": 10.7522 }
            }
            """);

        var lag = Geo.Samle([ArbeidsplasserLag.TilPunkt(rad), ArbeidsplasserLag.TilPunkt(rad)]);

        Assert.Single(lag.Features);
    }
}
