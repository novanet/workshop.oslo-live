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
        Assert.Equal(12.3, punkt.Properties["fart"]);
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
    public void Fartoey_som_ligger_stille_har_fart_null_knop()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Vision of the Fjords", "lat": 59.9073, "lon": 10.7481, "fart_knop": 0, "destinasjon": "N/A" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(0.0, punkt!.Properties["fart"]);
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
    public void Fartoey_i_fart_faar_kurs_over_grunn_som_heltall()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Vision of the Fjords", "lat": 59.9073, "lon": 10.7481, "fart_knop": 8.2, "kurs": 236.7 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(237, punkt!.Properties["kurs"]);
    }

    [Fact]
    public void Fartoey_uten_kjent_fart_bruker_kurs_over_grunn()
    {
        var rad = Rad("""{ "fart_knop": null, "kurs": 12 }""");

        Assert.Equal(12, SkipLag.UtledKurs(rad));
    }

    [Fact]
    public void Stilleliggende_fartoey_bruker_heading_selv_om_kurs_finnes()
    {
        var rad = Rad("""{ "fart_knop": 0, "kurs": 236.7, "heading": 90 }""");

        Assert.Equal(90, SkipLag.UtledKurs(rad));
    }

    [Fact]
    public void Stilleliggende_fartoey_uten_heading_bruker_kurs_over_grunn()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Vision of the Fjords", "lat": 59.9073, "lon": 10.7481, "fart_knop": 0, "kurs": 236.7 }
            """));

        Assert.NotNull(punkt);
        Assert.Equal(237, punkt!.Properties["kurs"]);
    }

    [Fact]
    public void Ukjent_kurs_under_fart_faller_tilbake_til_heading()
    {
        var rad = Rad("""{ "fart_knop": 5, "kurs": 360, "heading": 45 }""");

        Assert.Equal(45, SkipLag.UtledKurs(rad));
    }

    [Fact]
    public void Ukjent_heading_511_faller_tilbake_til_kurs_over_grunn()
    {
        var rad = Rad("""{ "fart_knop": 0, "kurs": 100, "heading": 511 }""");

        Assert.Equal(100, SkipLag.UtledKurs(rad));
    }

    [Fact]
    public void Stilleliggende_uten_kurs_og_heading_gir_ikke_kurs()
    {
        var rad = Rad("""{ "fart_knop": 0, "kurs": 360, "heading": 511 }""");

        Assert.Null(SkipLag.UtledKurs(rad));
    }

    [Fact]
    public void Fartoey_med_ukjent_kurs_mangler_kursfeltet()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Ukjent kurs", "lat": 59.9073, "lon": 10.7481, "fart_knop": 5, "kurs": 360 }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("kurs"));
    }

    [Fact]
    public void Fartoey_uten_kurs_mangler_kursfeltet()
    {
        var punkt = SkipLag.TilPunkt(Rad("""
            { "vessel_id": 257852500, "navn": "Bjorvika", "lat": 59.9073, "lon": 10.7481, "fart_knop": 0, "kurs": null }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("kurs"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(360)]
    [InlineData(511)]
    public void UtledKurs_forkaster_verdier_utenfor_gyldig_omraade(double kurs)
    {
        var rad = Rad($$"""{ "kurs": {{kurs.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }""");

        Assert.Null(SkipLag.UtledKurs(rad));
    }

    [Fact]
    public void UtledKurs_rund_av_til_naermeste_heltall_og_haandterer_wrap_til_null()
    {
        var rad = Rad("""{ "kurs": 359.6 }""");

        Assert.Equal(0, SkipLag.UtledKurs(rad));
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
