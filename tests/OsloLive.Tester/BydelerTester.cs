using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Tester;

public class BydelerTester
{
    // Verifiserte verdier fra Allemannsdata (geonorge/search_place_name, kommunenummer 0301).
    private static readonly Bydelssenter Frogner = new(145670, "Frogner", 59.91725, 10.70886);
    private static readonly Bydelssenter Stovner = new(266710, "Stovner", 59.96166, 10.92835);
    private static readonly Bydelssenter SøndreNordstrand = new(327640, "Søndre Nordstrand", 59.84002, 10.81625);

    private static readonly IReadOnlyList<Bydelssenter> Sentre = [Frogner, Stovner, SøndreNordstrand];

    private static readonly string[] AlleBydelsnavn =
    [
        "Alna", "Bjerke", "Frogner", "Gamle Oslo", "Grorud", "Grünerløkka",
        "Nordre Aker", "Nordstrand", "Sagene", "St. Hanshaugen", "Stovner",
        "Søndre Nordstrand", "Ullern", "Vestre Aker", "Østensjø",
    ];

    private static JsonElement Sted(string status, double lat, double lon, long placeId, params (string navn, string status)[] navn)
    {
        var json = JsonSerializer.Serialize(new
        {
            status,
            lat,
            lon,
            place_id = placeId,
            names = navn.Select(n => new { name = n.navn, status = n.status }),
        });

        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void Tolk_tar_bare_aktive_bydeler_med_hovednavn()
    {
        var frogner = Sted("aktiv", 59.91725, 10.70886, 145670,
            ("Bygdøy Frogner", "historisk"), ("Frogner", "hovednavn"));
        var grefsenKjelsås = Sted("relikt", 59.95, 10.78, 999,
            ("Grefsen Kjelsås", "historisk"));

        var sentre = Bydeler.Tolk([frogner, grefsenKjelsås]);

        var senter = Assert.Single(sentre);
        Assert.Equal("Frogner", senter.Navn);
        Assert.Equal(59.91725, senter.Lat);
    }

    [Fact]
    public void Samme_bydel_fra_flere_soek_telles_en_gang()
    {
        var frogner1 = Sted("aktiv", 59.91725, 10.70886, 145670, ("Frogner", "hovednavn"));
        var frogner2 = Sted("aktiv", 59.91725, 10.70886, 145670, ("Frogner", "hovednavn"));

        var sentre = Bydeler.Tolk([frogner1, frogner2]);

        Assert.Single(sentre);
    }

    [Theory]
    [InlineData(59.918, 10.709, "Frogner")]
    [InlineData(59.962, 10.928, "Stovner")]
    [InlineData(59.841, 10.816, "Søndre Nordstrand")]
    public void Punkt_faar_naermeste_bydel(double lat, double lon, string forventet)
    {
        var bydel = Bydeler.Finn(lat, lon, Sentre);

        Assert.Equal(forventet, bydel);
    }

    [Fact]
    public void Punkt_langt_fra_alle_bydeler_faar_ingen()
    {
        var bydel = Bydeler.Finn(60.13, 10.46, Sentre);

        Assert.Null(bydel);
    }

    [Fact]
    public void Telling_er_sortert_synkende_og_summen_stemmer()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.917, 10.709, "A", "Test"),
            Geo.Lag("b", 59.918, 10.708, "B", "Test"),
            Geo.Lag("c", 59.916, 10.710, "C", "Test"),
            Geo.Lag("d", 59.962, 10.928, "D", "Test"),
            Geo.Lag("e", 60.13, 10.46, "E", "Test"),
        ]);

        var tellinger = Bydeler.Tell(lag, Sentre);

        Assert.Equal([new Bydelstelling("Frogner", 3), new Bydelstelling("Stovner", 1)], tellinger);
        Assert.Equal(4, tellinger.Sum(t => t.Antall));
    }

    [Fact]
    public void Lik_telling_sorteres_paa_navn()
    {
        var lag = Geo.Samle([
            Geo.Lag("a", 59.962, 10.928, "A", "Test"),
            Geo.Lag("b", 59.918, 10.709, "B", "Test"),
        ]);

        var tellinger = Bydeler.Tell(lag, Sentre);

        Assert.Equal(["Frogner", "Stovner"], tellinger.Select(t => t.Bydel));
    }

    [Fact]
    public void Prefiksene_dekker_alle_femten_bydelene()
    {
        Assert.All(AlleBydelsnavn, navn =>
            Assert.Contains(Bydeler.Prefikser, p => navn.StartsWith(p, StringComparison.Ordinal)));
    }
}
