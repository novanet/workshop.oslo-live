using System.Net;
using System.Net.Http.Json;
using OsloLive.Helse;

namespace OsloLive.Tester;

/// <summary>Tester «/api/helse/kilder» og den rene logikken bak samlet status.</summary>
public class HelseTester(VertUtenBakgrunnssjekk vert) : IClassFixture<VertUtenBakgrunnssjekk>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Kildehelse_svarer_200_med_riktig_form()
    {
        var svar = await Klient.GetAsync("/api/helse/kilder");
        var innhold = await svar.Content.ReadFromJsonAsync<KildehelseSvar>();

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.NotNull(innhold);
        Assert.NotEmpty(innhold!.Kilder);
    }

    [Fact]
    public async Task Alle_kilder_er_ukjent_for_forste_bakgrunnssjekk_er_ferdig()
    {
        var innhold = await Klient.GetFromJsonAsync<KildehelseSvar>("/api/helse/kilder");

        Assert.Equal("ok", innhold!.Status);
        Assert.All(innhold.Kilder, kilde =>
        {
            Assert.Equal("ukjent", kilde.Status);
            Assert.Null(kilde.SistSjekket);
            Assert.Null(kilde.VarighetMs);
        });
    }

    [Fact]
    public void Standardintervallet_for_bakgrunnssjekken_er_60_sekunder()
    {
        var valg = new HelseValg();

        Assert.Equal(60, valg.IntervallSekunder);
    }

    [Theory]
    [InlineData(new[] { "ok", "ok" }, "ok")]
    [InlineData(new[] { "ok", "feil" }, "degradert")]
    [InlineData(new[] { "ukjent", "ukjent" }, "ok")]
    [InlineData(new[] { "feil" }, "degradert")]
    public void Samlet_status_er_degradert_naar_minst_en_kilde_feiler(string[] statuser, string forventet)
    {
        var kilder = statuser.Select(status => new Kildehelse("test", status, null, null));

        Assert.Equal(forventet, HelseSjekker.SamletStatus(kilder));
    }

    private sealed record KildehelseSvar(string Status, List<Kildeoppforing> Kilder);

    private sealed record Kildeoppforing(string Kilde, string Status, DateTimeOffset? SistSjekket, long? VarighetMs);
}
