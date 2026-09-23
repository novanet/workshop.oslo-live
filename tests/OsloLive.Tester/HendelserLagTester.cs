using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class HendelserLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Hendelse_i_oslo_blir_et_punkt_med_navn_og_kilde()
    {
        var rad = Rad("""
            {"id": "politilogg-1", "name": "Brann i Storgata", "lat": 59.9139, "lon": 10.7522, "source": "Politiloggen", "summary": "Røykutvikling.", "pub_date": "2026-09-23T05:59:16.7492253+00:00"}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.NotNull(punkt);
        Assert.Equal("Brann i Storgata", punkt!.Properties["navn"]);
        Assert.Equal("Politiloggen", punkt.Properties["kilde"]);
        Assert.Equal("politilogg-1", punkt.Properties["id"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var rad = Rad("""
            {"id": "politilogg-1", "name": "Brann i Storgata", "lat": 59.9139, "lon": 10.7522, "source": "Politiloggen"}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.Equal([10.7522, 59.9139], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Hendelse_har_sammendrag_og_meldt()
    {
        var rad = Rad("""
            {"id": "politilogg-1", "name": "Brann i Storgata", "lat": 59.9139, "lon": 10.7522, "source": "Politiloggen", "summary": "Røykutvikling.", "pub_date": "2026-09-23T05:59:16.7492253+00:00"}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.Equal("Røykutvikling.", punkt!.Properties["sammendrag"]);
        Assert.Equal("2026-09-23T05:59:16.7492253+00:00", punkt.Properties["meldt"]);
    }

    [Fact]
    public void Hendelse_uten_sammendrag_gir_likevel_punkt()
    {
        var rad = Rad("""
            {"id": "n", "name": "En nyhet", "lat": 59.9139, "lon": 10.7522}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.NotNull(punkt);
        Assert.Null(punkt!.Properties["sammendrag"]);
        Assert.Null(punkt.Properties["meldt"]);
    }

    [Fact]
    public void Hendelse_uten_koordinat_blir_hoppet_over()
    {
        var rad = Rad("""
            {"id": "n", "name": "Uten sted"}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Hendelse_med_null_koordinat_blir_hoppet_over()
    {
        var rad = Rad("""
            {"id": "n", "name": "Uten sted", "lat": null, "lon": null}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Hendelse_utenfor_oslo_blir_forkastet()
    {
        var rad = Rad("""
            {"id": "n", "name": "Lillehammer-nytt", "lat": 61.115, "lon": 10.466, "source": "NRK"}
            """);

        var punkt = HendelserLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Hendelser_uten_koordinat_velter_ikke_laget()
    {
        var rader = new[]
        {
            Rad("""{"id": "a", "name": "A", "lat": 59.9139, "lon": 10.7522}"""),
            Rad("""{"id": "b", "name": "B", "lat": 59.92, "lon": 10.76}"""),
            Rad("""{"id": "c", "name": "C uten koordinat"}"""),
        };

        var lag = Geo.Samle(rader.Select(HendelserLag.TilPunkt));

        Assert.Equal("FeatureCollection", lag.Type);
        Assert.Equal(2, lag.Features.Count);
    }

    [Fact]
    public void Parametrene_ber_om_hendelser_fra_siste_dogn()
    {
        var nå = new DateTimeOffset(2026, 9, 23, 12, 10, 54, TimeSpan.Zero);

        var url = Allemannsdata.ByggUrl("nyheter", "search_news", HendelserLag.Parametre(nå));

        Assert.Contains("since=2026-09-22T12%3A10%3A00Z", url);
    }

    [Fact]
    public void Nyeste_hendelse_kommer_forst()
    {
        var rader = new[]
        {
            Rad("""{"id": "kl-10", "name": "A", "pub_date": "2026-09-23T10:00:00Z"}"""),
            Rad("""{"id": "kl-12", "name": "B", "pub_date": "2026-09-23T12:00:00Z"}"""),
            Rad("""{"id": "kl-11", "name": "C", "pub_date": "2026-09-23T11:00:00Z"}"""),
        };
        var fra = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

        var rekkefølge = HendelserLag.Ferskeste(rader, fra).Select(r => r.GetProperty("id").GetString());

        Assert.Equal(["kl-12", "kl-11", "kl-10"], rekkefølge);
    }

    [Fact]
    public void Hendelse_eldre_enn_ett_dogn_blir_utelatt()
    {
        var rader = new[]
        {
            Rad("""{"id": "gammel", "name": "Gammel nyhet", "pub_date": "2026-09-13T10:25:49+00:00"}"""),
            Rad("""{"id": "fersk", "name": "Fersk hendelse", "pub_date": "2026-09-23T11:00:00Z"}"""),
        };
        var fra = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

        var igjen = HendelserLag.Ferskeste(rader, fra).Select(r => r.GetProperty("id").GetString());

        Assert.Equal(["fersk"], igjen);
    }

    [Fact]
    public void Hendelse_uten_meldt_tidspunkt_beholdes()
    {
        var rader = new[]
        {
            Rad("""{"id": "uten-tid", "name": "Uten tidspunkt"}"""),
            Rad("""{"id": "fersk", "name": "Fersk hendelse", "pub_date": "2026-09-23T11:00:00Z"}"""),
        };
        var fra = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

        var rekkefølge = HendelserLag.Ferskeste(rader, fra).Select(r => r.GetProperty("id").GetString());

        Assert.Equal(["fersk", "uten-tid"], rekkefølge);
    }
}
