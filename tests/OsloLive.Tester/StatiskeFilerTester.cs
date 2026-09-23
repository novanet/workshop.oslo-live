using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at statiske filer frontend trenger, blir servert.</summary>
public class StatiskeFilerTester : IClassFixture<VertUtenBakgrunnssjekk>
{
    private readonly VertUtenBakgrunnssjekk vert;

    public StatiskeFilerTester(VertUtenBakgrunnssjekk vert)
    {
        this.vert = vert;
    }

    [Fact]
    public async Task Symbolskriptet_svarer_200()
    {
        var svar = await vert.CreateClient().GetAsync("/effekter/symboler.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
    }

    [Fact]
    public async Task Symbolskriptet_definerer_lagSymbol()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/effekter/symboler.js");

        Assert.Contains("window.lagSymbol", innhold);
    }

    [Fact]
    public async Task Forsiden_formaterer_stroempris_med_norsk_tallformat()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        Assert.Contains("Intl.NumberFormat('nb-NO'", innhold);
    }

    [Fact]
    public async Task Stroempanelet_bruker_ikke_toFixed_eller_punktumerstatning()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        var start = innhold.IndexOf("async function hentStroem()", StringComparison.Ordinal);
        Assert.True(start >= 0, "Fant ikke hentStroem() i index.html");
        var slutt = innhold.IndexOf("\n}", start, StringComparison.Ordinal);
        var funksjon = innhold[start..slutt];

        Assert.DoesNotContain("toFixed(", funksjon);
        Assert.DoesNotContain("replace('", funksjon);
    }

    [Fact]
    public async Task Popupformatskriptet_svarer_200()
    {
        var svar = await vert.CreateClient().GetAsync("/effekter/popupformat.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task Popupformatskriptet_definerer_formaterEgenskap()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/effekter/popupformat.js");

        Assert.Contains("window.formaterEgenskap", innhold);
    }

    [Fact]
    public async Task Popupformatskriptet_bruker_oslotid()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/effekter/popupformat.js");

        Assert.Contains("Europe/Oslo", innhold);
        Assert.Contains("i dag", innhold);
    }

    [Fact]
    public async Task Popupformatskriptet_har_enhet_for_temperatur()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/effekter/popupformat.js");

        Assert.Contains("temperatur: ' °C'", innhold);
        Assert.Contains("kurs: '°'", innhold);
    }

    [Fact]
    public async Task Popupformatskriptet_skjuler_ikon_og_advarsel()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/effekter/popupformat.js");

        Assert.Contains("'ikon'", innhold);
        Assert.Contains("'advarsel'", innhold);
    }

    [Fact]
    public async Task Forsiden_laster_popupformatskriptet()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        Assert.Contains("effekter/popupformat.js", innhold);
    }

    [Fact]
    public async Task Popup_kaller_formaterEgenskap_og_esc()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        var start = innhold.IndexOf("function popup(e)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Fant ikke popup(e) i index.html");
        var slutt = innhold.IndexOf("\n}", start, StringComparison.Ordinal);
        var funksjon = innhold[start..slutt];

        Assert.Contains("window.formaterEgenskap(", funksjon);
        Assert.Contains("typeof window.formaterEgenskap", funksjon);
        Assert.Contains("filter(Boolean)", funksjon);
        Assert.Contains("esc(f.nøkkel)", funksjon);
        Assert.Contains("esc(f.verdi)", funksjon);
    }

    [Fact]
    public async Task Punkter_uten_id_faar_syntetisk_id_av_navn_og_koordinater()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        var funksjon = Funksjon(innhold, "function punktId(egenskaper, lon, lat)");

        Assert.Contains("egenskaper.id != null", funksjon);
        Assert.Contains("String(egenskaper.id)", funksjon);
        Assert.Contains("'|' + lon + '|' + lat", funksjon);
        Assert.Contains("punktId(p, c[0], c[1])", Funksjon(innhold, "async function hent(id)"));
    }

    [Fact]
    public async Task Glidning_varer_2_sekunder_og_hopper_over_5_km()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        Assert.Contains("const GLIDNING_VARIGHET = 2000;", innhold);
        Assert.Contains("const GLIDNING_MAKS_AVSTAND = 5000;", innhold);
        Assert.Contains("requestAnimationFrame(glidningSteg)", innhold);
        Assert.Contains("> GLIDNING_MAKS_AVSTAND", Funksjon(innhold, "function forberedGlidning("));
        Assert.Contains("6371000", Funksjon(innhold, "function avstand("));
    }

    [Fact]
    public async Task Glidning_hopper_ved_redusert_bevegelse_og_avbrytes_naar_laget_hentes_paa_nytt()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        Assert.Contains("(prefers-reduced-motion: reduce)", innhold);

        var forbered = Funksjon(innhold, "function forberedGlidning(");
        Assert.Contains("avbrytGlidning(lagId);", forbered);
        Assert.Contains("redusertBevegelse", forbered);
        Assert.Contains("p.egenskaper.id != null ? forrige.get(p.id)", forbered);

        var steg = Funksjon(innhold, "function glidningSteg()");
        Assert.Contains("redusertBevegelseOnsket()", steg);
        Assert.Contains("aapenPopup.boble.setLngLat([p.visLon, p.visLat])", steg);
    }

    /// <summary>Kroppen til funksjonen som begynner med <paramref name="start"/>, fram til første linje som bare er «}».</summary>
    private static string Funksjon(string innhold, string start)
    {
        var fra = innhold.IndexOf(start, StringComparison.Ordinal);
        Assert.True(fra >= 0, $"Fant ikke {start} i index.html");
        var til = innhold.IndexOf("\n}", fra, StringComparison.Ordinal);
        return innhold[fra..til];
    }
}
