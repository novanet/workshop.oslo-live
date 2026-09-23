using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class SkipLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Fartoey_blir_punkt_med_navn_kilde_fart_og_destinasjon()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 258219000, "navn": "Tåkeheimen", "lat": 59.905, "lon": 10.72, "fart_knop": 12.3, "destinasjon": "NESODDTANGEN" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Tåkeheimen", punkt!.Properties["navn"]);
        Assert.Equal("BarentsWatch AIS", punkt.Properties["kilde"]);
        Assert.Equal("12,3 knop", punkt.Properties["fart"]);
        Assert.Equal("NESODDTANGEN", punkt.Properties["destinasjon"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 258219000, "navn": "Tåkeheimen", "lat": 59.905, "lon": 10.72, "fart_knop": 12.3, "destinasjon": "NESODDTANGEN" }
            """));

        Assert.Equal([10.72, 59.905], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Fartoey_uten_destinasjon_mangler_destinasjonsfeltet()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257395400, "navn": "Fjord Harmony", "lat": 59.9058, "lon": 10.7532, "fart_knop": 0, "destinasjon": null }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("destinasjon"));
    }

    [Fact]
    public void Fartoey_uten_fart_mangler_fartfeltet()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257338400, "navn": "Bjorvika", "lat": 59.9058, "lon": 10.7528, "fart_knop": null, "destinasjon": "OSLO" }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("fart"));
    }

    [Fact]
    public void Fartoey_med_fart_null_ligger_stille()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Vision of the Fjords", "lat": 59.9073, "lon": 10.7481, "fart_knop": 0, "destinasjon": "N/A" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("ligger stille", punkt!.Properties["fart"]);
    }

    [Fact]
    public void Fart_vises_med_en_desimal_norsk_komma_og_knop()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 258219000, "navn": "Tåkeheimen", "lat": 59.905, "lon": 10.72, "fart_knop": 12.34, "destinasjon": "NESODDTANGEN" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("12,3 knop", punkt!.Properties["fart"]);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData(" n/a ")]
    [InlineData("NA")]
    [InlineData("none")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("Unknown")]
    public void Destinasjon_som_betyr_mangler_utelates(string destinasjon)
    {
        var punkt = SkipLag.TilPunkt(Rad($$"""
            { "vessel_id": 257852500, "navn": "Vision of the Fjords", "lat": 59.9073, "lon": 10.7481, "fart_knop": 0, "destinasjon": "{{destinasjon}}" }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("destinasjon"));
    }

    [Fact]
    public void Navn_i_versaler_faar_stor_forbokstav_per_ord()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "VISION OF THE FJORDS", "lat": 59.9073, "lon": 10.7481, "fart_knop": 0, "destinasjon": "N/A" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Vision Of The Fjords", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Navn_med_smaa_bokstaver_roeres_ikke()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 258219000, "navn": "MS Tåkeheimen", "lat": 59.905, "lon": 10.72, "fart_knop": 12.3, "destinasjon": "NESODDTANGEN" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("MS Tåkeheimen", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Fartoey_uten_posisjon_blir_forkastet()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Ukjent posisjon", "lat": null, "lon": null, "fart_knop": 0, "destinasjon": null }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Fartoey_utenfor_utsnittet_blir_forkastet()
    {
        // Horten, langt sør for Oslofjord-boksen.
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 123456789, "navn": "Ute av kartet", "lat": 59.4166, "lon": 10.4833, "fart_knop": 5, "destinasjon": null }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Fartoey_uten_navn_heter_ukjent_fartoey()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": null, "lat": 59.905, "lon": 10.72, "fart_knop": 0, "destinasjon": null }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent fartøy", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Samme_fartoey_to_ganger_gir_ett_punkt()
    {
        var rad = Rad("""
            { "vessel_id": 258219000, "navn": "Tåkeheimen", "lat": 59.905, "lon": 10.72, "fart_knop": 12.3, "destinasjon": "NESODDTANGEN" }
            """);

        var lag = Geo.Samle([SkipLag.TilPunkt(rad), SkipLag.TilPunkt(rad)]);

        Assert.Single(lag.Features);
    }

    [Fact]
    public void Radiusen_dekker_Nesodden()
    {
        // Nesoddtangen ferjekai.
        const double NesoddenLat = 59.848;
        const double NesoddenLon = 10.654;

        var avstandKm = Geo.Avstand(Geo.OsloLat, Geo.OsloLon, NesoddenLat, NesoddenLon) / 1000;

        Assert.True(avstandKm <= SkipLag.RadiusKm);
    }
}
