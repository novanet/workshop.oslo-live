using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OsloLive.Tester;

/// <summary>Tester at havet-stiger-effekten serveres av appen.</summary>
public class HavetStigerTester(TestVert vert) : IClassFixture<TestVert>
{
    private HttpClient Klient => vert.CreateClient();

    [Fact]
    public async Task Havet_stiger_skriptet_svarer_med_javascript()
    {
        var svar = await Klient.GetAsync("/effekter/havet-stiger.js");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Contains("javascript", svar.Content.Headers.ContentType?.MediaType);
    }
}
