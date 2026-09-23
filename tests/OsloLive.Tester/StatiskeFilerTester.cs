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
    public async Task Lagraden_har_grafen_mellom_navn_og_antall()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/");

        var navn = innhold.IndexOf("<span class=\"navn\">${l.navn}</span>", StringComparison.Ordinal);
        var graf = innhold.IndexOf("class=\"graf\" id=\"graf-${l.id}\"", StringComparison.Ordinal);
        var antall = innhold.IndexOf("class=\"antall\" id=\"antall-${l.id}\"", StringComparison.Ordinal);

        Assert.True(navn >= 0 && graf > navn && antall > graf);
    }

    [Fact]
    public async Task Lagraden_har_ingen_egen_grafrad()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/");

        Assert.DoesNotContain("liste.appendChild(graf)", innhold);
    }

    [Fact]
    public async Task Indeksen_viser_ikke_historikktekst_i_raden()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/");

        Assert.DoesNotContain("graf-tom", innhold);
    }

    [Fact]
    public async Task Indeksen_setter_verktoytips_uten_historikk()
    {
        var innhold = await vert.CreateClient().GetStringAsync("/");

        Assert.Contains("Ingen historikk ennå", innhold);
        Assert.Contains("bilder.length < 2", innhold);
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
}
