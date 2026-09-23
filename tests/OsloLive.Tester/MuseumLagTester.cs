using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class MuseumLagTester
{
    private static List<JsonElement> Rader(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Museum_blir_punkt_med_navn_og_kilde()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 135892, "navn": "Kunstindustrimuseet", "lon_lat": [10.743, 59.918] }
            """), eksempel: null);

        Assert.NotNull(punkt);
        Assert.Equal("Kunstindustrimuseet", punkt!.Properties["navn"]);
        Assert.Equal("Askeladden og DigitaltMuseum (Riksantikvaren)", punkt.Properties["kilde"]);
        Assert.Equal([10.743, 59.918], punkt.Geometry.Coordinates);
    }

    [Fact]
    public void Smakebiten_kommer_med_i_detaljene()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 135892, "navn": "Kunstindustrimuseet", "lon_lat": [10.743, 59.918] }
            """), eksempel: "En gammel vase");

        Assert.Equal("En gammel vase", punkt!.Properties["eksempel"]);
    }

    [Fact]
    public void Museum_uten_smakebit_vises_likevel_uten_eksempelfeltet()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 135892, "navn": "Kunstindustrimuseet", "lon_lat": [10.743, 59.918] }
            """), eksempel: null);

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("eksempel"));
    }

    [Fact]
    public void Museum_uten_id_blir_forkastet()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "navn": "Kunstindustrimuseet", "lon_lat": [10.743, 59.918] }
            """), eksempel: null);

        Assert.Null(punkt);
    }

    [Fact]
    public void Museum_uten_koordinater_blir_forkastet()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 135892, "navn": "Kunstindustrimuseet" }
            """), eksempel: null);

        Assert.Null(punkt);
    }

    [Fact]
    public void Kjent_museum_uten_navn_i_askeladden_faar_navnet_fra_tabellen()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 135892, "lon_lat": [10.743, 59.918] }
            """), eksempel: null);

        Assert.Equal("Kunstindustrimuseet", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Museum_utenfor_oslo_blir_forkastet()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 135892, "navn": "Kunstindustrimuseet", "lon_lat": [10.39, 63.43] }
            """), eksempel: null);

        Assert.Null(punkt);
    }

    [Fact]
    public void Flere_bygninger_i_samme_museum_blir_ett_punkt()
    {
        var rader = Rader("""
            [
                { "heritage_site_id": 137517, "navn": "Tilbygg. Nf 325", "lon_lat": [10.6864, 59.9070] },
                { "heritage_site_id": 137517, "navn": "Hovedbygg. Nf 316", "lon_lat": [10.6862, 59.9068] },
                { "heritage_site_id": 135892, "navn": "Kunstindustrimuseet", "lon_lat": [10.743, 59.918] }
            ]
            """);

        var museer = MuseumLag.ÉnPerMuseum(rader);

        Assert.Equal(2, museer.Count);
        Assert.Equal("Tilbygg. Nf 325", museer.Single(m => m.GetProperty("heritage_site_id").GetInt64() == 137517).GetProperty("navn").GetString());
    }

    [Fact]
    public void Rader_uten_id_blir_forkastet_ved_sammenslaaing()
    {
        var rader = Rader("""
            [
                { "navn": "Uten id", "lon_lat": [10.7, 59.9] },
                { "heritage_site_id": 1, "navn": "Med id", "lon_lat": [10.7, 59.9] }
            ]
            """);

        var museer = MuseumLag.ÉnPerMuseum(rader);

        Assert.Single(museer);
        Assert.Equal("Med id", museer[0].GetProperty("navn").GetString());
    }

    [Fact]
    public void Kjent_museum_faar_museets_navn_og_ikke_bygningsnavnet()
    {
        var museer = MuseumLag.ÉnPerMuseum(Rader("""
            [
                { "heritage_site_id": 137517, "navn": "Tilbygg. Nf 325", "lon_lat": [10.6864, 59.9070] },
                { "heritage_site_id": 137517, "navn": "Hovedbygg. Nf 316", "lon_lat": [10.6862, 59.9068] }
            ]
            """));

        var punkt = MuseumLag.TilPunkt(museer.Single(), eksempel: null);

        Assert.Equal("Norsk Folkemuseum", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Kjent_museum_har_samlingskoden_i_digitaltmuseum()
    {
        var museum = MuseumLag.Oppslag(Rad("""
            { "heritage_site_id": 137517, "navn": "Tilbygg. Nf 325" }
            """));

        Assert.Equal(new MuseumLag.Museum("Norsk Folkemuseum", "NF"), museum);
    }

    [Fact]
    public void Ukjent_bygning_blir_ikke_punkt()
    {
        var rad = Rad("""
            { "heritage_site_id": 98246, "navn": "Bakgårdsbygning", "lon_lat": [10.7587, 59.9278] }
            """);

        Assert.Null(MuseumLag.Oppslag(rad));
        Assert.Null(MuseumLag.TilPunkt(rad, eksempel: null));
    }

    [Fact]
    public void Kjent_museum_uten_samling_vises_uten_eksempel()
    {
        var punkt = MuseumLag.TilPunkt(Rad("""
            { "heritage_site_id": 168599, "navn": "Kulturinstitusjoner - Frammuseet - Bygdøynesveien 0", "lon_lat": [10.6995, 59.9034] }
            """), eksempel: null);

        Assert.Equal("Frammuseet", punkt!.Properties["navn"]);
        Assert.False(punkt.Properties.ContainsKey("eksempel"));
    }

    [Fact]
    public void Kall_per_oppdatering_er_begrenset()
    {
        Assert.Equal(1 + MuseumLag.KjenteMuseer.Values.Count(m => m.Samling is not null), MuseumLag.MaksKallPerOppdatering);
        Assert.True(MuseumLag.MaksKallPerOppdatering <= 20);
    }
}
