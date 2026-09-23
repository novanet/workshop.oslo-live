using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

/// <summary>Tester funksjonene som tolker varselet, ikke selve hentingen.</summary>
public class LuftkvalitetTester
{
    private static List<JsonElement> Timer(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

    [Theory]
    [InlineData(1.2, "Lite")]
    [InlineData(1.99, "Lite")]
    [InlineData(2.0, "Moderat")]
    [InlineData(2.9, "Moderat")]
    [InlineData(3.1, "Mye")]
    [InlineData(4.5, "Svært mye")]
    [InlineData(9.0, "Svært mye")]
    public void Nivå_regnes_ut_fra_aqi_verdien(double aqi, string forventetNivå)
    {
        Assert.Equal(forventetNivå, LuftkvalitetLag.NivåFraAqi(aqi));
    }

    [Theory]
    [InlineData("Lite", false)]
    [InlineData("Moderat", false)]
    [InlineData("Mye", true)]
    [InlineData("Svært mye", true)]
    [InlineData(null, false)]
    public void Advarsel_gjelder_mye_og_verre(string? nivå, bool erAdvarsel)
    {
        Assert.Equal(erAdvarsel, LuftkvalitetLag.ErAdvarsel(nivå));
    }

    [Fact]
    public void Verste_nivaa_er_det_hoeyeste_i_neste_doegn()
    {
        var nå = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var timer = Timer("""
            [
                {"time":"2026-09-23T13:00:00+00:00","value":1.2},
                {"time":"2026-09-23T17:00:00+00:00","value":3.4},
                {"time":"2026-09-23T22:00:00+00:00","value":2.1}
            ]
            """);

        var varsel = LuftkvalitetLag.TolkVarsel(timer, nå);

        Assert.NotNull(varsel);
        Assert.Equal("Mye", varsel.VersteNivå);
    }

    [Fact]
    public void Timer_etter_24_timer_teller_ikke()
    {
        var nå = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var timer = Timer("""
            [
                {"time":"2026-09-23T15:00:00+00:00","value":2.2},
                {"time":"2026-09-24T14:00:00+00:00","value":4.8}
            ]
            """);

        var varsel = LuftkvalitetLag.TolkVarsel(timer, nå);

        Assert.NotNull(varsel);
        Assert.Equal("Moderat", varsel.VersteNivå);
    }

    [Fact]
    public void Timer_foer_naa_teller_ikke()
    {
        var nå = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var timer = Timer("""
            [
                {"time":"2026-09-23T10:00:00+00:00","value":3.5},
                {"time":"2026-09-23T14:00:00+00:00","value":1.1}
            ]
            """);

        var varsel = LuftkvalitetLag.TolkVarsel(timer, nå);

        Assert.NotNull(varsel);
        Assert.Equal("Lite", varsel.VersteNivå);
    }

    [Fact]
    public void Tom_timeserie_gir_ingen_varsel()
    {
        var varsel = LuftkvalitetLag.TolkVarsel([], DateTimeOffset.UtcNow);

        Assert.Null(varsel);
    }

    [Fact]
    public void Varselteksten_er_kort_og_ikke_tom()
    {
        var nå = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var timer = Timer("""
            [
                {"time":"2026-09-23T13:00:00+00:00","value":2.1},
                {"time":"2026-09-23T18:00:00+00:00","value":2.3}
            ]
            """);

        var varsel = LuftkvalitetLag.TolkVarsel(timer, nå);

        Assert.NotNull(varsel);
        Assert.NotEmpty(varsel.Tekst);
        Assert.True(varsel.Tekst.Length <= 80, $"Teksten er for lang: {varsel.Tekst}");
    }

    [Fact]
    public void Samme_nivaa_hele_perioden_gir_samlet_tekst()
    {
        var nå = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var timer = Timer("""
            [
                {"time":"2026-09-23T13:00:00+00:00","value":1.1},
                {"time":"2026-09-23T18:00:00+00:00","value":1.3}
            ]
            """);

        var varsel = LuftkvalitetLag.TolkVarsel(timer, nå);

        Assert.NotNull(varsel);
        Assert.Equal("Lite hele neste døgn", varsel.Tekst);
    }

    [Fact]
    public void Varselkall_er_begrenset()
    {
        Assert.True(LuftkvalitetLag.MaksVarselkall > 0);
        Assert.True(LuftkvalitetLag.MaksVarselkall <= 10);
    }
}
