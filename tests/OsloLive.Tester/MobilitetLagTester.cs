using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class MobilitetLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private static List<JsonElement> Liste(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Theory]
    [InlineData("SCOOTER_STANDING", "elsparkesykkel")]
    [InlineData("SCOOTER_SEATED", "elsparkesykkel")]
    [InlineData("BICYCLE", "sykkel")]
    [InlineData("CARGO_BICYCLE", "sykkel")]
    [InlineData("CAR", "bil")]
    [InlineData(null, "annet")]
    [InlineData("OTHER", "annet")]
    public void Type_oversettes_fra_form_factor(string? formFactor, string forventetType)
    {
        Assert.Equal(forventetType, MobilitetLag.Type(formFactor));
    }

    [Theory]
    [InlineData("rydeoslo", "Ryde", "Ryde")]
    [InlineData("voioslo", "VOI Technology Norway AS", "Voi")]
    [InlineData("boltoslo", "Bolt", "Bolt")]
    [InlineData("oslobysykkel", "UIP Bauer Media Outdoor Norge AS", "Oslo Bysykkel")]
    [InlineData("hyrenorge", "Hyre", "Hyre")]
    [InlineData("ukjent", "Noe AS", "Noe AS")]
    [InlineData(null, null, "Ukjent operatør")]
    public void Operatør_får_kjent_navn(string? systemId, string? operatør, string forventet)
    {
        Assert.Equal(forventet, MobilitetLag.Operatør(systemId, operatør));
    }

    [Fact]
    public void Ledig_kjøretøy_gir_punkt_med_lon_lat()
    {
        var punkt = MobilitetLag.FraKjøretøy(Rad("""
            {
                "id": "YRY:Vehicle:ea325240",
                "form_factor": "SCOOTER_STANDING",
                "lat": 59.909607,
                "lon": 10.749284,
                "reserved": false,
                "disabled": false,
                "operator": "Ryde",
                "system_id": "rydeoslo"
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal([10.749284, 59.909607], punkt!.Geometry.Coordinates);
        Assert.False(string.IsNullOrWhiteSpace((string?)punkt.Properties["navn"]));
        Assert.Equal("Entur delt mobilitet", punkt.Properties["kilde"]);
        Assert.Equal("Ryde", punkt.Properties["operatør"]);
        Assert.Equal("elsparkesykkel", punkt.Properties["type"]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Reservert_eller_deaktivert_kjøretøy_utelates(bool reservert, bool deaktivert)
    {
        var punkt = MobilitetLag.FraKjøretøy(Rad($$"""
            {
                "id": "YRY:Vehicle:ea325240",
                "form_factor": "SCOOTER_STANDING",
                "lat": 59.909607,
                "lon": 10.749284,
                "reserved": {{(reservert ? "true" : "false")}},
                "disabled": {{(deaktivert ? "true" : "false")}},
                "operator": "Ryde",
                "system_id": "rydeoslo"
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Ett_punkt_per_kjøretøy()
    {
        var kjøretøy = Liste("""
            [
                { "id": "BOLT:1", "form_factor": "SCOOTER_STANDING", "lat": 59.91, "lon": 10.75, "reserved": false, "disabled": false, "operator": "Bolt", "system_id": "boltoslo" },
                { "id": "RYDE:1", "form_factor": "SCOOTER_STANDING", "lat": 59.92, "lon": 10.74, "reserved": false, "disabled": false, "operator": "Ryde", "system_id": "rydeoslo" },
                { "id": "HYRE:1", "form_factor": "CAR", "lat": 59.93, "lon": 10.73, "reserved": false, "disabled": false, "operator": "Hyre", "system_id": "hyrenorge" },
                { "id": "RYDE:1", "form_factor": "SCOOTER_STANDING", "lat": 59.92, "lon": 10.74, "reserved": false, "disabled": false, "operator": "Ryde", "system_id": "rydeoslo" }
            ]
            """);

        var lag = MobilitetLag.Samle(kjøretøy, []);

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(3, lag.Features.Count);
    }

    [Fact]
    public void Kjøretøy_utenfor_Oslo_utelates()
    {
        var punkt = MobilitetLag.FraKjøretøy(Rad("""
            {
                "id": "TRD:1",
                "form_factor": "SCOOTER_STANDING",
                "lat": 63.43,
                "lon": 10.39,
                "reserved": false,
                "disabled": false,
                "operator": "Voi",
                "system_id": "voitrondheim"
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Bysykkelstasjon_gir_ett_punkt_med_antall_ledige_sykler()
    {
        // Tidligere ble det ett punkt per ledig sykkel, alle på samme koordinat,
        // og de kunne aldri skilles på kartet (#209). Nå er én stasjon ett punkt.
        var punkter = MobilitetLag.FraBysykkelstasjon(Rad("""
            {
                "id": "YOS:Station:599",
                "name": "Paléhaven",
                "lat": 59.9103199,
                "lon": 10.7499929,
                "vehicles_available": 3,
                "operator": "UIP Bauer Media Outdoor Norge AS",
                "system_id": "oslobysykkel"
            }
            """)).ToList();

        var p = Assert.Single(punkter);
        Assert.NotNull(p);
        Assert.Equal("YOS:Station:599", p!.Properties["id"]);
        Assert.Equal([10.7499929, 59.9103199], p.Geometry.Coordinates);
        Assert.Equal("Paléhaven", p.Properties["navn"]);
        Assert.Equal("Entur delt mobilitet", p.Properties["kilde"]);
        Assert.Equal("Oslo Bysykkel", p.Properties["operatør"]);
        Assert.Equal("sykkel", p.Properties["type"]);
        Assert.Equal("Paléhaven", p.Properties["stasjon"]);
        Assert.IsType<int>(p.Properties["ledige sykler"]);
        Assert.Equal(3, p.Properties["ledige sykler"]);
    }

    [Fact]
    public void Samme_bysykkelstasjon_fra_flere_formfaktorer_gir_ett_punkt()
    {
        // Laget spør både BICYCLE og CARGO_BICYCLE, så samme stasjon kan komme to ganger.
        var stasjoner = Liste("""
            [
                { "id": "YOS:Station:599", "name": "Paléhaven", "lat": 59.91, "lon": 10.75, "vehicles_available": 33, "operator": "UIP Bauer Media Outdoor Norge AS", "system_id": "oslobysykkel" },
                { "id": "YOS:Station:599", "name": "Paléhaven", "lat": 59.91, "lon": 10.75, "vehicles_available": 33, "operator": "UIP Bauer Media Outdoor Norge AS", "system_id": "oslobysykkel" }
            ]
            """);

        var lag = MobilitetLag.Samle([], stasjoner);

        var p = Assert.Single(lag.Features);
        Assert.Equal(33, p.Properties["ledige sykler"]);
    }

    [Fact]
    public void Samle_gir_ett_punkt_per_kjøretøy_og_per_bysykkelstasjon()
    {
        var kjøretøy = Liste("""
            [
                { "id": "BOLT:1", "form_factor": "SCOOTER_STANDING", "lat": 59.91, "lon": 10.75, "reserved": false, "disabled": false, "operator": "Bolt", "system_id": "boltoslo" }
            ]
            """);
        var stasjoner = Liste("""
            [
                { "id": "YOS:Station:599", "name": "Paléhaven", "lat": 59.91, "lon": 10.75, "vehicles_available": 2, "operator": "UIP Bauer Media Outdoor Norge AS", "system_id": "oslobysykkel" },
                { "id": "YOS:Station:600", "name": "Tom stasjon", "lat": 59.92, "lon": 10.74, "vehicles_available": 0, "operator": "UIP Bauer Media Outdoor Norge AS", "system_id": "oslobysykkel" }
            ]
            """);

        var lag = MobilitetLag.Samle(kjøretøy, stasjoner);

        // Én sparkesykkel og én stasjon med ledige sykler; den tomme stasjonen gir ingenting.
        Assert.Equal(2, lag.Features.Count);
    }

    [Fact]
    public void Punktene_har_feltene_operatør_og_type_til_popupen()
    {
        var sparkesykkel = MobilitetLag.FraKjøretøy(Rad("""
            { "id": "VOI:1", "form_factor": "SCOOTER_STANDING", "lat": 59.91, "lon": 10.75, "reserved": false, "disabled": false, "operator": "VOI Technology Norway AS", "system_id": "voioslo" }
            """));
        var bysykkel = MobilitetLag.FraBysykkelstasjon(Rad("""
            { "id": "YOS:Station:599", "name": "Paléhaven", "lat": 59.91, "lon": 10.75, "vehicles_available": 1, "operator": "UIP Bauer Media Outdoor Norge AS", "system_id": "oslobysykkel" }
            """)).Single();

        Assert.NotNull(sparkesykkel);
        Assert.True(sparkesykkel!.Properties.ContainsKey("operatør"));
        Assert.True(sparkesykkel.Properties.ContainsKey("type"));
        Assert.Equal("Voi", sparkesykkel.Properties["operatør"]);
        Assert.Equal("elsparkesykkel", sparkesykkel.Properties["type"]);

        Assert.NotNull(bysykkel);
        Assert.True(bysykkel!.Properties.ContainsKey("operatør"));
        Assert.True(bysykkel.Properties.ContainsKey("type"));
        Assert.Equal("Oslo Bysykkel", bysykkel.Properties["operatør"]);
        Assert.Equal("sykkel", bysykkel.Properties["type"]);
    }

    [Fact]
    public void Tom_bysykkelstasjon_og_andre_stasjoner_utelates()
    {
        var paléhaven = MobilitetLag.FraBysykkelstasjon(Rad("""
            { "id": "YOS:Station:599", "name": "Paléhaven", "lat": 59.91, "lon": 10.75, "vehicles_available": 0, "operator": "UIP Bauer Media Outdoor Norge AS", "system_id": "oslobysykkel" }
            """));
        var voi = MobilitetLag.FraBysykkelstasjon(Rad("""
            { "id": "VOI:Station:1", "name": "Voi-stasjon", "lat": 59.91, "lon": 10.75, "vehicles_available": 0, "operator": "VOI Technology Norway AS", "system_id": "voioslo" }
            """));
        var hyre = MobilitetLag.FraBysykkelstasjon(Rad("""
            { "id": "HYRE:Station:1", "name": "Hyre-stasjon", "lat": 59.91, "lon": 10.75, "vehicles_available": 4, "operator": "Hyre", "system_id": "hyrenorge" }
            """));

        Assert.Empty(paléhaven);
        Assert.Empty(voi);
        Assert.Empty(hyre);
    }
}
