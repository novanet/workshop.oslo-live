using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at popup-synlighet-skriptet (#203) serveres og er koblet til.</summary>
public class PopupSynligTester(VertUtenBakgrunnssjekk vert) : IClassFixture<VertUtenBakgrunnssjekk>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Popup_skriptet_svarer_200_med_javascript_innholdstype()
    {
        var svar = await Klient.GetAsync("/effekter/popup-synlig.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Popup_skriptet_definerer_sikreSynligPopup()
    {
        var svar = await Klient.GetAsync("/effekter/popup-synlig.js");
        var innhold = await svar.Content.ReadAsStringAsync();

        Assert.Contains("window.sikreSynligPopup", innhold);
        Assert.Contains("hovedpanel", innhold);
        Assert.Contains("naermest", innhold);
        Assert.Contains("omrade-panel", innhold);
    }

    [Fact]
    public async Task Forsiden_laster_popup_skriptet_og_har_ikke_lenger_lokal_definisjon()
    {
        var svar = await Klient.GetAsync("/index.html");
        var innhold = await svar.Content.ReadAsStringAsync();

        Assert.Contains("effekter/popup-synlig.js", innhold);
        Assert.DoesNotContain("function sikreSynligPopup", innhold);
    }
}
