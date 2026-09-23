using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at fyrverkeriskriptet serveres som en statisk JavaScript-fil.</summary>
public class FyrverkeriTester(TestVert vert) : IClassFixture<TestVert>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Fyrverkeriskriptet_svarer_200_med_javascript_som_innholdstype()
    {
        var svar = await Klient.GetAsync("/effekter/fyrverkeri.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType ?? "");
    }
}
