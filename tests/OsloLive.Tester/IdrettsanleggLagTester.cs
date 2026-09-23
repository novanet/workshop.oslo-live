using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class IdrettsanleggLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Punkt_har_navn_kilde_type_og_status()
    {
        var rad = Rad("""
            {
                "id": 1,
                "name": "Testbanen",
                "facility_type": "Fotballbane gress",
                "status": "EXISTING",
                "lat": 59.9139,
                "lon": 10.7522
            }
            """);

        var punkt = IdrettsanleggLag.TilPunkt(rad);

        Assert.NotNull(punkt);
        Assert.Equal("Testbanen", punkt!.Properties["navn"]);
        Assert.Equal("Anleggsregisteret", punkt.Properties["kilde"]);
        Assert.Equal("Fotballbane gress", punkt.Properties["type"]);
        Assert.Equal("Eksisterende", punkt.Properties["status"]);
    }

    [Fact]
    public void Punkt_har_koordinater_som_lon_lat()
    {
        var rad = Rad("""{"id": 1, "name": "Testbanen", "lat": 59.9139, "lon": 10.7522}""");

        var punkt = IdrettsanleggLag.TilPunkt(rad);

        Assert.Equal([10.7522, 59.9139], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Anlegg_utenfor_oslo_blir_forkastet()
    {
        var rad = Rad("""{"id": 1, "name": "Lillehammer stadion", "lat": 61.115, "lon": 10.466}""");

        var lag = IdrettsanleggLag.Bygg([rad]);

        Assert.Empty(lag.Features);
    }

    [Fact]
    public void Anlegg_uten_navn_faar_standardnavn()
    {
        var rad = Rad("""{"id": 1, "lat": 59.9139, "lon": 10.7522}""");

        var punkt = IdrettsanleggLag.TilPunkt(rad);

        Assert.Equal("Ukjent anlegg", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Hvert_anlegg_gir_ett_punkt()
    {
        var rader = new[]
        {
            Rad("""{"id": 1, "name": "Anlegg 1", "lat": 59.9139, "lon": 10.7522}"""),
            Rad("""{"id": 2, "name": "Anlegg 2", "lat": 59.92, "lon": 10.75}"""),
        };

        var lag = IdrettsanleggLag.Bygg(rader);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(2, lag.Features.Count);
    }

    [Fact]
    public void Samme_anlegg_to_ganger_gir_ett_punkt()
    {
        var rader = new[]
        {
            Rad("""{"id": 1, "name": "Anlegg 1", "lat": 59.9139, "lon": 10.7522}"""),
            Rad("""{"id": 1, "name": "Anlegg 1", "lat": 59.9139, "lon": 10.7522}"""),
        };

        var lag = IdrettsanleggLag.Bygg(rader);

        Assert.Single(lag.Features);
    }

    [Fact]
    public void Numerisk_id_blir_tekst()
    {
        var rad = Rad("""{"id": 42, "name": "Testbanen", "lat": 59.9139, "lon": 10.7522}""");

        var punkt = IdrettsanleggLag.TilPunkt(rad);

        Assert.Equal("42", punkt!.Properties["id"]);
        Assert.IsType<string>(punkt.Properties["id"]);
    }

    [Theory]
    [InlineData("PLANNED", "Planlagt")]
    [InlineData("CLOSED_DOWN", "Nedlagt")]
    [InlineData("UNREALIZED", "Ble ikke realisert")]
    [InlineData("NOE_UKJENT", "NOE_UKJENT")]
    public void Statuskode_blir_norsk_tekst(string kode, string forventetTekst)
    {
        var rad = Rad($$"""{"id": 1, "name": "Anlegg", "status": "{{kode}}", "lat": 59.9139, "lon": 10.7522}""");

        var punkt = IdrettsanleggLag.TilPunkt(rad);

        Assert.Equal(forventetTekst, punkt!.Properties["status"]);
    }
}
