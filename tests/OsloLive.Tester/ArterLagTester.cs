using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class ArterLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Observasjon_i_oslo_blir_punkt()
    {
        var rad = Rad("""
            {
                "obs_url": "https://artskart.artsdatabanken.no/obs1",
                "latitude": 59.91,
                "longitude": 10.75,
                "name": "Kjøttmeis",
                "scientific_name": "Parus major",
                "collected_date": "2024-01-15",
                "collector": "Per"
            }
            """);

        var punkt = ArterLag.TilPunkt(rad);

        Assert.NotNull(punkt);
        Assert.Equal("Kjøttmeis", punkt!.Properties["navn"]);
        Assert.Equal("Kjøttmeis", punkt.Properties["art"]);
        Assert.Equal("2024-01-15", punkt.Properties["dato"]);
        Assert.Equal("Per", punkt.Properties["observatør"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)punkt.Properties["kilde"]));
    }

    [Fact]
    public void Art_faller_tilbake_til_vitenskapelig_navn_uten_norsk_navn()
    {
        var rad = Rad("""
            {
                "obs_url": "https://artskart.artsdatabanken.no/obs2",
                "latitude": 59.91,
                "longitude": 10.75,
                "name": null,
                "scientific_name": "Parus major",
                "collected_date": "2024-01-15",
                "collector": "Per"
            }
            """);

        var punkt = ArterLag.TilPunkt(rad);

        Assert.Equal("Parus major", punkt!.Properties["navn"]);
        Assert.Equal("Parus major", punkt.Properties["art"]);
    }

    [Fact]
    public void To_observasjoner_av_samme_art_gir_to_punkter()
    {
        var a = ArterLag.TilPunkt(Rad("""
            {"obs_url": "obs-a", "latitude": 59.91, "longitude": 10.75, "name": "Kjøttmeis", "scientific_name": "Parus major"}
            """));
        var b = ArterLag.TilPunkt(Rad("""
            {"obs_url": "obs-b", "latitude": 59.92, "longitude": 10.76, "name": "Kjøttmeis", "scientific_name": "Parus major"}
            """));

        var lag = Geo.Samle([a, b]);

        Assert.Equal(2, lag.Features.Count);
    }

    [Fact]
    public void Observasjon_utenfor_oslo_forkastes()
    {
        var rad = Rad("""
            {"obs_url": "obs3", "latitude": 63.43, "longitude": 10.39, "name": "Kjøttmeis", "scientific_name": "Parus major"}
            """);

        var punkt = ArterLag.TilPunkt(rad);

        Assert.Null(punkt);
    }

    [Fact]
    public void Observasjon_uten_koordinater_forkastes()
    {
        var rad = Rad("""
            {"obs_url": "obs4", "name": "Kjøttmeis", "scientific_name": "Parus major"}
            """);

        var punkt = ArterLag.TilPunkt(rad);

        Assert.Null(punkt);
    }
}
