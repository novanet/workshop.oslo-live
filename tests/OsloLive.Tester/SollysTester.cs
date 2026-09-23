using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at det statiske sollys-skriptet (#132) serveres som JavaScript.</summary>
public class SollysTester(VertUtenBakgrunnssjekk vert) : IClassFixture<VertUtenBakgrunnssjekk>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Sollys_skriptet_svarer_200_med_javascript_innholdstype()
    {
        var svar = await Klient.GetAsync("/effekter/sollys.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType);
    }
}
