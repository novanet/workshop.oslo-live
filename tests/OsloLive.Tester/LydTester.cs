using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at lydeffekten serveres som statisk JavaScript.</summary>
public class LydTester(TestVert vert) : IClassFixture<TestVert>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Lydskriptet_svarer_200_med_javascript_innholdstype()
    {
        var svar = await Klient.GetAsync("/effekter/lyd.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType ?? "");
    }
}
