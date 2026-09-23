using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at innbrenningsskriptet serveres som statisk fil.</summary>
public class InnbrenningTester(VertUtenBakgrunnssjekk vert) : IClassFixture<VertUtenBakgrunnssjekk>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Innbrenningsskriptet_svarer_200_med_javascript_innholdstype()
    {
        var svar = await Klient.GetAsync("/effekter/innbrenning.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
    }
}
