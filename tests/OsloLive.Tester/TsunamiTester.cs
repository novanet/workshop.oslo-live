using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at tsunamisimuleringen serveres som en statisk JavaScript-fil.</summary>
public class TsunamiTester(TestVert vert) : IClassFixture<TestVert>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Tsunamiskriptet_svarer_200_med_javascript_som_innholdstype()
    {
        var svar = await Klient.GetAsync("/effekter/tsunami.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType ?? "");
    }
}
