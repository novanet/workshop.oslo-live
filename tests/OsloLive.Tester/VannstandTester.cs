using System.Text.Json;

namespace OsloLive.Tester;

/// <summary>Tester funksjonen som tolker tidevannssvaret, ikke selve hentingen.</summary>
public class VannstandTester
{
    private static readonly DateTimeOffset Nå = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static List<JsonElement> Rader(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Fact]
    public void Naa_er_maalingen_naermest_tidspunktet()
    {
        var naaRader = Rader("""
            [
                {"time":"2026-09-23T10:00:00+00:00","tide_cm":8.0,"sea_level_cm":40.0,"surge_cm":32.0},
                {"time":"2026-09-23T11:50:00+00:00","tide_cm":9.0,"sea_level_cm":42.0,"surge_cm":33.0},
                {"time":"2026-09-23T12:30:00+00:00","tide_cm":10.0,"sea_level_cm":45.0,"surge_cm":35.0}
            ]
            """);
        var tabellRader = Rader("""
            [
                {"time":"2026-09-23T15:10:00+00:00","height_cm":30.0,"kind":"high"}
            ]
            """);

        var svar = Vannstand.Tolk(naaRader, tabellRader, Nå);

        Assert.Equal(42.0, svar.Naa);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 11, 50, 0, TimeSpan.Zero), svar.Maalt);
    }

    [Fact]
    public void Neste_er_foerste_hoeyvann_etter_naa()
    {
        var naaRader = Rader("""[{"time":"2026-09-23T11:50:00+00:00","tide_cm":9.0,"sea_level_cm":42.0,"surge_cm":33.0}]""");
        var tabellRader = Rader("""
            [
                {"time":"2026-09-23T09:00:00+00:00","height_cm":28.0,"kind":"high"},
                {"time":"2026-09-23T15:10:00+00:00","height_cm":30.0,"kind":"high"},
                {"time":"2026-09-23T21:20:00+00:00","height_cm":-12.0,"kind":"low"}
            ]
            """);

        var svar = Vannstand.Tolk(naaRader, tabellRader, Nå);

        Assert.Equal("høyvann", svar.Neste.Type);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 15, 10, 0, TimeSpan.Zero), svar.Neste.Tidspunkt);
        Assert.Equal(30.0, svar.Neste.Verdi);
    }

    [Fact]
    public void Neste_er_lavvann_naar_det_kommer_foerst()
    {
        var naaRader = Rader("""[{"time":"2026-09-23T11:50:00+00:00","tide_cm":9.0,"sea_level_cm":42.0,"surge_cm":33.0}]""");
        var tabellRader = Rader("""
            [
                {"time":"2026-09-23T13:00:00+00:00","height_cm":-12.0,"kind":"low"},
                {"time":"2026-09-23T19:00:00+00:00","height_cm":30.0,"kind":"high"}
            ]
            """);

        var svar = Vannstand.Tolk(naaRader, tabellRader, Nå);

        Assert.Equal("lavvann", svar.Neste.Type);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 13, 0, 0, TimeSpan.Zero), svar.Neste.Tidspunkt);
    }

    [Fact]
    public void Ingen_fremtidig_hoey_eller_lavvann_kaster()
    {
        var naaRader = Rader("""[{"time":"2026-09-23T11:50:00+00:00","tide_cm":9.0,"sea_level_cm":42.0,"surge_cm":33.0}]""");
        var tabellRader = Rader("""[{"time":"2026-09-23T09:00:00+00:00","height_cm":28.0,"kind":"high"}]""");

        Assert.Throws<InvalidOperationException>(() => Vannstand.Tolk(naaRader, tabellRader, Nå));
    }

    [Fact]
    public void Ingen_vannstandsdata_kaster()
    {
        var tabellRader = Rader("""[{"time":"2026-09-23T15:10:00+00:00","height_cm":30.0,"kind":"high"}]""");

        Assert.Throws<InvalidOperationException>(() => Vannstand.Tolk([], tabellRader, Nå));
    }
}
