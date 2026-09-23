using System.Globalization;
using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class WikipediaLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Artikkel_blir_punkt_med_pageid_som_id_og_tittel_som_navn()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 1470, "ns": 0, "title": "Stortingsbygningen", "lat": 59.9127, "lon": 10.7403, "dist": 650.2, "primary": "" }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("1470", punkt!.Properties["id"]);
        Assert.Equal("Stortingsbygningen", punkt.Properties["navn"]);
        Assert.Equal("Wikipedia", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Artikkel_faar_lenke_med_curid()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 1470, "title": "Stortingsbygningen", "lat": 59.9127, "lon": 10.7403 }
            """));

        Assert.Equal("https://no.wikipedia.org/?curid=1470", punkt!.Properties["artikkel"]);
    }

    [Fact]
    public void Punktet_ligger_der_artikkelen_er_merket_med_lon_foran_lat()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 1470, "title": "Stortingsbygningen", "lat": 59.9127, "lon": 10.7403 }
            """));

        Assert.Equal("Point", punkt!.Geometry.Type);
        Assert.Equal(new[] { 10.7403, 59.9127 }, punkt.Geometry.Coordinates);
    }

    [Fact]
    public void Artikkel_med_tom_tittel_blir_forkastet()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 1470, "title": "  ", "lat": 59.9127, "lon": 10.7403 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Rad_som_ikke_er_et_objekt_blir_forkastet()
    {
        Assert.Null(WikipediaLag.TilPunkt(Rad("null")));
        Assert.Null(WikipediaLag.TilPunkt(Rad("[]")));
    }

    [Fact]
    public void Artikkel_uten_pageid_blir_forkastet()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "title": "Stortingsbygningen", "lat": 59.9127, "lon": 10.7403 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Artikkel_med_pageid_som_tekst_blir_forkastet()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": "1470", "title": "Stortingsbygningen", "lat": 59.9127, "lon": 10.7403 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Artikkel_uten_tittel_blir_forkastet()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 1470, "lat": 59.9127, "lon": 10.7403 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Artikkel_med_null_posisjon_blir_forkastet()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 1470, "title": "Stortingsbygningen", "lat": null, "lon": null }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Artikkel_utenfor_utsnittet_blir_forkastet()
    {
        var punkt = WikipediaLag.TilPunkt(Rad("""
            { "pageid": 99, "title": "Gardermoen", "lat": 60.1976, "lon": 11.1004 }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Url_spør_geosearch_rundt_sentrum_med_radius_og_antall()
    {
        var url = WikipediaLag.ByggUrl();

        Assert.Equal(
            "https://no.wikipedia.org/w/api.php?action=query&list=geosearch&gscoord=59.9139%7C10.7522&gsradius=10000&gslimit=100&format=json",
            url);
    }

    [Fact]
    public void Url_bruker_punktum_som_desimaltegn_med_norsk_kultur()
    {
        var forrigeKultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nb-NO");
        string url;
        try
        {
            url = WikipediaLag.ByggUrl();
        }
        finally
        {
            CultureInfo.CurrentCulture = forrigeKultur;
        }

        Assert.Contains("gscoord=59.9139%7C10.7522", url);
    }
}
