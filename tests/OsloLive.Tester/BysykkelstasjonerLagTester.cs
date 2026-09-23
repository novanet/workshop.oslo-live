using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class BysykkelstasjonerLagTester
{
    private static JsonElement Rot(string json) => JsonDocument.Parse(json).RootElement;

    // station_information.json og station_status.json har samme ramme rundt stasjonene.
    private static JsonElement Fil(params string[] stasjoner) =>
        Rot($$$"""{"last_updated":1758632400,"ttl":10,"data":{"stations":[{{{string.Join(",", stasjoner)}}}]}}""");

    private static JsonElement Informasjon(params string[] stasjoner) => Fil(stasjoner);

    private static JsonElement Status(params string[] stasjoner) => Fil(stasjoner);

    private const string SkøyenInfo = """{"station_id":"627","name":"Skøyen Stasjon","address":"Skøyen","lat":59.9226729,"lon":10.6788129,"capacity":20}""";
    private const string SkøyenStatus = """{"station_id":"627","is_installed":true,"is_renting":true,"is_returning":true,"num_bikes_available":6,"num_docks_available":2,"last_reported":1758632390}""";
    private const string AkerBryggeInfo = """{"station_id":"381","name":"Aker Brygge","address":"Aker Brygge","lat":59.9109,"lon":10.7298,"capacity":12}""";
    private const string AkerBryggeStatus = """{"station_id":"381","is_installed":true,"is_renting":true,"is_returning":true,"num_bikes_available":0,"num_docks_available":12,"last_reported":1758632390}""";

    [Fact]
    public void Seks_sykler_og_to_laaser_gir_fylling_75()
    {
        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo), Status(SkøyenStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal(75, punkt.Properties["fylling"]);
    }

    [Fact]
    public void Stasjon_faar_navn_kilde_og_ledige_sykler_og_laaser()
    {
        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo), Status(SkøyenStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal("627", punkt.Properties["id"]);
        Assert.Equal("Skøyen Stasjon", punkt.Properties["navn"]);
        Assert.Equal("Oslo Bysykkel (GBFS)", punkt.Properties["kilde"]);
        Assert.Equal(6, punkt.Properties["ledige sykler"]);
        Assert.Equal(2, punkt.Properties["ledige låser"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo), Status(SkøyenStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal(new[] { 10.6788129, 59.9226729 }, punkt.Geometry.Coordinates);
    }

    [Fact]
    public void Stasjon_som_bare_finnes_i_informasjonsfilen_er_ikke_med()
    {
        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo, AkerBryggeInfo), Status(SkøyenStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal("627", punkt.Properties["id"]);
    }

    [Fact]
    public void Stasjon_som_bare_finnes_i_statusfilen_er_ikke_med()
    {
        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo), Status(SkøyenStatus, AkerBryggeStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal("627", punkt.Properties["id"]);
    }

    [Fact]
    public void Stasjon_som_ikke_er_installert_er_ikke_med()
    {
        var ikkeInstallert = SkøyenStatus.Replace("\"is_installed\":true", "\"is_installed\":false");

        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo), Status(ikkeInstallert));

        Assert.Empty(lag.Features);
    }

    [Fact]
    public void Stasjon_med_is_installed_0_er_ikke_med()
    {
        var ikkeInstallert = SkøyenStatus.Replace("\"is_installed\":true", "\"is_installed\":0");

        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo), Status(ikkeInstallert));

        Assert.Empty(lag.Features);
    }

    [Theory]
    [InlineData("\"num_bikes_available\":6", "\"num_bikes_available\":\"6\"")]
    [InlineData("\"num_bikes_available\":6", "\"num_bikes_available\":6.5")]
    [InlineData("\"num_docks_available\":2", "\"num_docks_available\":-1")]
    [InlineData("\"num_docks_available\":2,", "")]
    public void Stasjon_med_ugyldig_antall_hoppes_over_uten_aa_felle_laget(string gammel, string ny)
    {
        var ugyldig = SkøyenStatus.Replace(gammel, ny);

        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(SkøyenInfo, AkerBryggeInfo), Status(ugyldig, AkerBryggeStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal("381", punkt.Properties["id"]);
    }

    [Fact]
    public void Tom_stasjon_gir_fylling_0()
    {
        var lag = BysykkelstasjonerLag.SlåSammen(Informasjon(AkerBryggeInfo), Status(AkerBryggeStatus));

        var punkt = Assert.Single(lag.Features);
        Assert.Equal(0, punkt.Properties["fylling"]);
    }

    [Theory]
    [InlineData(6, 2, 75)]
    [InlineData(10, 0, 100)]
    [InlineData(1, 2, 33)]
    [InlineData(2, 1, 67)]
    [InlineData(0, 0, 0)]
    public void Fylling_er_sykler_delt_paa_sykler_pluss_laaser_i_hele_prosent(int sykler, int låser, int forventet)
    {
        Assert.Equal(forventet, BysykkelstasjonerLag.Fylling(sykler, låser));
    }

    [Fact]
    public void Fil_uten_data_stations_gir_tydelig_feil()
    {
        var feil = Assert.Throws<InvalidOperationException>(() =>
            BysykkelstasjonerLag.SlåSammen(Rot("""{"data":{}}"""), Status(SkøyenStatus)));

        Assert.Contains("station_information.json", feil.Message);
    }
}
