using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class VeiarbeidLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string E18Bekkelaget = """
        {"situation_id":"NPRA_HBT_04-09-2026.131981","type":"MaintenanceWorks","sub_types":["roadworks"],
         "description":"Vegarbeid, gyldig mellom 07:00 og 19:00 ... fra 07.09.2026 til 10.10.2026.",
         "location":"E18 Nedre Bekkelaget skole i Oslo - E18 Nedre Bekkelaget skole i Oslo, i retning mot Askim",
         "road":"E18","severity":"low","lat":59.880383,"lon":10.773848,
         "start_time":"2026-09-07T07:00:00+02:00","end_time":"2026-10-10T19:00:00+02:00",
         "last_update":"2026-09-04T14:43:01.735+02:00","active":true}
        """;

    [Fact]
    public void Melding_i_oslo_blir_et_punkt_med_navn_og_kilde()
    {
        var punkt = VeiarbeidLag.TilPunkt(Rad(E18Bekkelaget));

        Assert.NotNull(punkt);
        Assert.Equal("E18 Nedre Bekkelaget skole i Oslo", punkt!.Properties["navn"]);
        Assert.Equal("Statens vegvesen", punkt.Properties["kilde"]);
        Assert.Equal("NPRA_HBT_04-09-2026.131981", punkt.Properties["id"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = VeiarbeidLag.TilPunkt(Rad(E18Bekkelaget));

        Assert.Equal([10.773848, 59.880383], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Punktet_har_type_beskrivelse_alvorlighet_og_varer_til()
    {
        var punkt = VeiarbeidLag.TilPunkt(Rad(E18Bekkelaget));

        Assert.Equal("Vedlikehold", punkt!.Properties["type"]);
        Assert.Equal("Vegarbeid, gyldig mellom 07:00 og 19:00 ... fra 07.09.2026 til 10.10.2026.", punkt.Properties["beskrivelse"]);
        Assert.Equal("Lav", punkt.Properties["alvorlighet"]);
        Assert.Equal("10.10.2026 19:00", punkt.Properties["varer til"]);
    }

    [Fact]
    public void Melding_uten_koordinat_blir_hoppet_over()
    {
        var rad = Rad("""{"situation_id": "x", "active": true}""");

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Melding_med_null_koordinat_blir_hoppet_over()
    {
        var rad = Rad("""{"situation_id": "x", "active": true, "lat": null, "lon": null}""");

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Melding_utenfor_oslo_blir_forkastet()
    {
        var rad = Rad("""
            {"situation_id":"NPRA_utenfor","type":"MaintenanceWorks","sub_types":[],
             "location":"Narvestad i Rælingen","road":"Fv120","severity":"low",
             "lat":59.84999,"lon":11.111215,"active":true}
            """);

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Meldinger_uten_koordinat_velter_ikke_laget()
    {
        var rader = new[]
        {
            Rad(E18Bekkelaget),
            Rad("""{"situation_id":"b","type":"Accident","sub_types":[],"lat":59.92,"lon":10.76,"active":true}"""),
            Rad("""{"situation_id":"c","active":true}"""),
        };

        var lag = Geo.Samle(rader.Select(VeiarbeidLag.TilPunkt));

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(2, lag.Features.Count);
    }

    [Theory]
    [InlineData("highest", "Svært høy")]
    [InlineData("high", "Høy")]
    [InlineData("medium", "Middels")]
    [InlineData("low", "Lav")]
    [InlineData("none", "Ingen")]
    [InlineData(null, "Ukjent")]
    [InlineData("rart", "Ukjent")]
    public void Alvorlighet_oversettes(string? severity, string forventet)
    {
        Assert.Equal(forventet, VeiarbeidLag.Alvorlighet(severity));
    }

    [Fact]
    public void Hoy_alvorlighet_gir_eget_ikon()
    {
        var rad = Rad("""
            {"situation_id":"NPRA_HBT_04-09-2026.131981","type":"MaintenanceWorks","sub_types":["roadworks"],
             "location":"E18 Nedre Bekkelaget skole i Oslo","road":"E18","severity":"high",
             "lat":59.880383,"lon":10.773848,"active":true}
            """);

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Equal("⛔", punkt!.Properties["ikon"]);
    }

    [Fact]
    public void Lav_alvorlighet_bruker_lagets_ikon()
    {
        var punkt = VeiarbeidLag.TilPunkt(Rad(E18Bekkelaget));

        Assert.Null(punkt!.Properties["ikon"]);
    }

    [Fact]
    public void Stengt_vei_gir_type_med_stengt()
    {
        var rad = Rad("""
            {"situation_id":"s","type":"RoadOrCarriagewayOrLaneManagement","sub_types":["roadClosed"],
             "lat":59.9139,"lon":10.7522,"active":true}
            """);

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Equal("Stengt vei", punkt!.Properties["type"]);
    }

    [Fact]
    public void Melding_uten_sted_bruker_veinummer_som_navn()
    {
        var rad = Rad("""
            {"situation_id":"s","type":"MaintenanceWorks","sub_types":[],"location":null,"road":"E18",
             "lat":59.9139,"lon":10.7522,"active":true}
            """);

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Equal("E18", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Melding_uten_sluttid_varer_til_ukjent()
    {
        var rad = Rad("""
            {"situation_id":"s","type":"MaintenanceWorks","sub_types":[],"end_time":null,
             "lat":59.9139,"lon":10.7522,"active":true}
            """);

        var punkt = VeiarbeidLag.TilPunkt(rad);

        Assert.Equal("ukjent", punkt!.Properties["varer til"]);
    }

    [Fact]
    public void Parametrene_ber_om_meldinger_rundt_oslo()
    {
        var url = Allemannsdata.ByggUrl("vegvesen", "get_traffic_messages", VeiarbeidLag.Parametre());

        Assert.Contains("lat=59.9139", url);
        Assert.Contains("lon=10.7522", url);
        Assert.Contains("radius_km=20", url);
    }
}
