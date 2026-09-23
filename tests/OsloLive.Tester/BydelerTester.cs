using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task Hent_gjoer_ni_kall_ett_per_prefiks_aldri_ett_per_punkt()
    {
        // Samme bydel i alle svar, så vi også ser at treff fra flere prefikser slås sammen.
        const string svar = """
            {
                "source": "geonorge",
                "operation": "search_place_name",
                "parameters": {},
                "data": {
                    "places": [
                        {
                            "place_id": 145670,
                            "status": "aktiv",
                            "lat": 59.91725,
                            "lon": 10.70886,
                            "names": [{ "name": "Frogner", "status": "hovednavn" }]
                        }
                    ]
                }
            }
            """;

        var håndterer = new TellendeHandler(svar);
        var data = new Allemannsdata(new HttpClient(håndterer), NullLogger<Allemannsdata>.Instance, new Metrikker());

        Allemannsdata.TømMellomlager();
        try
        {
            var sentre = await Bydeler.Hent(data);

            Assert.Equal(9, Bydeler.Prefikser.Length);
            Assert.Equal(Bydeler.Prefikser.Length, håndterer.Antall);
            Assert.Equal(Bydeler.Prefikser.Length, håndterer.Adresser.Distinct().Count());
            Assert.All(håndterer.Adresser, adresse => Assert.Contains("kommunenummer=0301", adresse));
            Assert.Equal("Frogner", Assert.Single(sentre).Navn);
        }
        finally
        {
            Allemannsdata.TømMellomlager();
        }
    }

    /// <summary>Falsk <see cref="HttpMessageHandler"/> som teller kallene og gir samme svar hver gang, uten nettverk.</summary>
    private sealed class TellendeHandler(string svar) : HttpMessageHandler
    {
        private int antall;

        public int Antall => antall;

        public ConcurrentBag<string> Adresser { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp)
        {
            Interlocked.Increment(ref antall);
            Adresser.Add(forespørsel.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(svar, Encoding.UTF8, "application/json"),
            });
        }
    }
}
