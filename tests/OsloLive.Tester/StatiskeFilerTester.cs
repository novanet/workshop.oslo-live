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
    public async Task Lagnavnet_kuttes_med_ellipse_pa_en_linje()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        var regel = CssRegel(innhold, ".lag .navn");

        Assert.Contains("min-width: 0", regel);
        Assert.Contains("white-space: nowrap", regel);
        Assert.Contains("overflow: hidden", regel);
        Assert.Contains("text-overflow: ellipsis", regel);
    }

    [Fact]
    public async Task Lagradens_hoyrekolonner_har_fast_bredde()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        foreach (var selektor in new[] { ".lag .antall", ".lag .alder", ".lag .visning" })
        {
            var regel = CssRegel(innhold, selektor);

            Assert.Contains("flex: none", regel);
            Assert.Contains("width:", regel);
            Assert.Contains("white-space: nowrap", regel);
        }
    }

    [Fact]
    public async Task Lagradens_tittel_starter_med_navnet()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/index.html");

        var start = innhold.IndexOf("async function start()", StringComparison.Ordinal);
        Assert.True(start >= 0, "Fant ikke start() i index.html");
        var slutt = innhold.IndexOf("\n}", start, StringComparison.Ordinal);
        var funksjon = innhold[start..slutt];

        Assert.Contains("rad.title = l.beskrivelse ? l.navn", funksjon);
    }

    private static string CssRegel(string innhold, string selektor)
    {
        var start = innhold.IndexOf(selektor + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Fant ikke CSS-regelen {selektor} i index.html");
        var slutt = innhold.IndexOf('}', start);
        return innhold[start..slutt];
    }
}
