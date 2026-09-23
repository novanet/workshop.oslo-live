using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at meteorsimuleringen serveres som statisk skript og lastes av forsiden.</summary>
public class MeteorTester(TestVert vert) : IClassFixture<TestVert>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Meteorskriptet_svarer_200_med_javascript()
    {
        var svar = await Klient.GetAsync("/effekter/meteor.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Forsiden_laster_meteorskriptet_med_defer()
    {
        var html = await Klient.GetStringAsync("/");

        const string tagg = "<script src=\"effekter/meteor.js\" defer></script>";
        Assert.Equal(1, html.Split(tagg).Length - 1);
        Assert.DoesNotContain("id=\"meteor\"", html);
    }

    [Fact]
    public async Task Meteorskriptet_er_pakket_i_en_iife()
    {
        var skript = (await Klient.GetStringAsync("/effekter/meteor.js")).Trim();

        Assert.StartsWith("(() => {", skript);
        Assert.EndsWith("})();", skript);
    }

    [Theory]
    [InlineData("SIMULERING")]
    [InlineData("role")]
    [InlineData("prefers-reduced-motion: reduce")]
    [InlineData("building-3d")]
    [InlineData("getPaintProperty")]
    public async Task Meteorskriptet_inneholder(string tekst)
    {
        var skript = await Klient.GetStringAsync("/effekter/meteor.js");

        Assert.Contains(tekst, skript);
    }

    [Theory]
    [InlineData("https://")]
    [InlineData("http://")]
    [InlineData("new maplibregl.Map")]
    public async Task Meteorskriptet_inneholder_ikke(string tekst)
    {
        var skript = await Klient.GetStringAsync("/effekter/meteor.js");

        Assert.DoesNotContain(tekst, skript);
    }
}
