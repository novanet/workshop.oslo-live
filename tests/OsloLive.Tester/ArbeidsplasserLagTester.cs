using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class ArbeidsplasserLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Fact]
    public void Punkt_har_orgnr_som_id_navn_og_kilde()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
                "organization_id": "944384448",
                "navn": "STIFTELSEN KIRKENS BYMISJON",
                "antallAnsatte": 2567,
                "naeringskode1": { "kode": "94.910", "beskrivelse": "Aktiviteter i religiøse organisasjoner" },
                "coordinates": { "latitude": 59.9099, "longitude": 10.7464 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("944384448", punkt!.Properties["id"]);
        Assert.Equal("STIFTELSEN KIRKENS BYMISJON", punkt.Properties["navn"]);
        Assert.Equal("Firmafakta", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
                "organization_id": "1",
                "navn": "Selskap",
                "coordinates": { "latitude": 59.9099, "longitude": 10.7464 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal([10.7464, 59.9099], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Punkt_har_ansatte_som_heltall_og_bransje()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
                "organization_id": "1",
                "navn": "Selskap",
                "antallAnsatte": 750,
                "naeringskode1": { "kode": "62.200", "beskrivelse": "Konsulentvirksomhet" },
                "coordinates": { "latitude": 59.91, "longitude": 10.75 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(750, punkt!.Properties["ansatte"]);
        Assert.Equal("Konsulentvirksomhet", punkt.Properties["bransje"]);
    }

    [Fact]
    public void Mangler_ansatte_utelates_feltet()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
                "organization_id": "1",
                "navn": "Selskap",
                "antallAnsatte": null,
                "naeringskode1": { "kode": "62.200", "beskrivelse": "Konsulentvirksomhet" },
                "coordinates": { "latitude": 59.91, "longitude": 10.75 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("ansatte"));
    }

    [Fact]
    public void Mangler_bransje_utelates_feltet()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            {
                "organization_id": "1",
                "navn": "Selskap",
                "antallAnsatte": 600,
                "naeringskode1": null,
                "coordinates": { "latitude": 59.91, "longitude": 10.75 }
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("bransje"));
    }

    [Fact]
    public void Rad_uten_koordinatobjekt_gir_ikke_punkt()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            { "organization_id": "1", "navn": "Selskap" }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_med_null_koordinater_gir_ikke_punkt()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            { "organization_id": "1", "navn": "Selskap", "coordinates": null }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_uten_tall_i_koordinater_gir_ikke_punkt()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            { "organization_id": "1", "navn": "Selskap", "coordinates": { "latitude": null, "longitude": 10.75 } }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_uten_orgnr_gir_ikke_punkt()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            { "navn": "Selskap", "coordinates": { "latitude": 59.91, "longitude": 10.75 } }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Virksomhet_utenfor_oslo_blir_forkastet()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            { "organization_id": "1", "navn": "Trondheim-selskap", "coordinates": { "latitude": 63.43, "longitude": 10.39 } }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Selskap_uten_navn_faar_standardnavn()
    {
        var punkt = ArbeidsplasserLag.TilPunkt(Rad("""
            { "organization_id": "1", "coordinates": { "latitude": 59.91, "longitude": 10.75 } }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent virksomhet", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Samle_hopper_over_rader_uten_koordinater()
    {
        var rader = Liste("""
            [
                { "organization_id": "1", "navn": "A", "coordinates": { "latitude": 59.91, "longitude": 10.75 } },
                { "organization_id": "2", "navn": "B" },
                { "organization_id": "3", "navn": "C", "coordinates": { "latitude": 59.92, "longitude": 10.76 } }
            ]
            """);

        var lag = ArbeidsplasserLag.Samle(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(2, lag.Features.Count);
    }
}
