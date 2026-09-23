using OsloLive.Bysykkel;

namespace OsloLive.Tester;

/// <summary>Tester den rene logikken for utvalg av bysykkeldøgnet (#191), uten nettverk.</summary>
public class BysykkeldognTester
{
    private static Kildetur Rad(string? startetVed, string? sluttetVed, double? fraLat = 59.9139, double? fraLon = 10.7522, double? tilLat = 59.9270, double? tilLon = 10.7170) =>
        new(startetVed, sluttetVed, fraLat, fraLon, tilLat, tilLon);

    [Fact]
    public void Tur_som_starter_23_50_UTC_om_sommeren_hoerer_til_neste_dag_i_Oslo()
    {
        var rad = Rad("2026-08-11 23:50:00.000000+00:00", "2026-08-12 00:10:00.000000+00:00");

        var resultat = Bysykkeldogn.TilTur(rad);

        Assert.NotNull(resultat);
        Assert.Equal(new DateOnly(2026, 8, 12), resultat!.Value.Dato);
        Assert.Equal(6600, resultat.Value.Tur.Start);
        Assert.Equal(7800, resultat.Value.Tur.Slutt);
    }

    [Fact]
    public void Tur_om_vinteren_bruker_UTC_pluss_en()
    {
        var rad = Rad("2026-01-13 07:00:00+00:00", "2026-01-13 07:10:00+00:00");

        var resultat = Bysykkeldogn.TilTur(rad);

        Assert.NotNull(resultat);
        Assert.Equal(28800, resultat!.Value.Tur.Start);
    }

    [Fact]
    public void Tur_med_samme_start_og_sluttstasjon_er_ikke_med()
    {
        var rad = Rad("2026-08-12 09:00:00.000000+00:00", "2026-08-12 09:10:00.000000+00:00", tilLat: 59.9139, tilLon: 10.7522);

        Assert.Null(Bysykkeldogn.TilTur(rad));
    }

    [Fact]
    public void Tur_over_tre_timer_er_ikke_med()
    {
        var forLang = Rad("2026-08-12 12:00:00.000000+00:00", "2026-08-12 15:01:00.000000+00:00");
        var akkuratNok = Rad("2026-08-12 12:00:00.000000+00:00", "2026-08-12 15:00:00.000000+00:00");

        Assert.Null(Bysykkeldogn.TilTur(forLang));
        Assert.NotNull(Bysykkeldogn.TilTur(akkuratNok));
    }

    [Fact]
    public void Tur_med_manglende_felt_eller_ugyldig_tid_er_ikke_med()
    {
        var manglerStart = Rad(null, "2026-08-12 09:10:00.000000+00:00");
        var ugyldigTid = Rad("tull", "2026-08-12 09:10:00.000000+00:00");

        Assert.Null(Bysykkeldogn.TilTur(manglerStart));
        Assert.Null(Bysykkeldogn.TilTur(ugyldigTid));
    }

    [Fact]
    public void Fra_og_til_er_lon_lat()
    {
        var rad = Rad("2026-08-12 10:00:00.000000+00:00", "2026-08-12 10:20:00.000000+00:00");

        var resultat = Bysykkeldogn.TilTur(rad);

        Assert.NotNull(resultat);
        Assert.Equal([10.7522, 59.9139], resultat!.Value.Tur.Fra);
        Assert.Equal([10.717, 59.927], resultat.Value.Tur.Til);
    }

    [Fact]
    public async Task Travleste_hverdag_velges_og_helg_ignoreres()
    {
        await using var strøm = File.OpenRead("Data/bysykkelturer.json");

        var døgn = await Bysykkeldogn.Velg(strøm, default);

        Assert.NotNull(døgn);
        Assert.Equal("2026-08-11", døgn!.Dato);
        Assert.Equal(3, døgn.Turer.Count);
        Assert.Equal(døgn.Turer.OrderBy(t => t.Start).Select(t => t.Start), døgn.Turer.Select(t => t.Start));
    }

    [Fact]
    public void Uten_hverdager_gir_null()
    {
        var lørdag = new[]
        {
            (new DateOnly(2026, 8, 15), new Bysykkeltur([10.7522, 59.9139], [10.717, 59.927], 0, 600)),
        };

        Assert.Null(Bysykkeldogn.VelgDag(lørdag));
    }

    [Fact]
    public void Siste_hele_maaned()
    {
        Assert.Equal((2026, 8), Bysykkeldogn.SisteHeleMåned(DateTimeOffset.Parse("2026-09-23T12:00:00Z")));
        Assert.Equal((2025, 12), Bysykkeldogn.SisteHeleMåned(DateTimeOffset.Parse("2026-01-05T00:00:00Z")));
        Assert.Equal((2026, 8), Bysykkeldogn.SisteHeleMåned(DateTimeOffset.Parse("2026-08-31T22:30:00Z")));
    }

    [Fact]
    public void Maanedsurl_formateres_med_nuller()
    {
        Assert.Equal("https://data.urbansharing.com/oslobysykkel.no/trips/v1/2026/08.json", Bysykkeldogn.MånedsUrl(2026, 8));
    }
}
