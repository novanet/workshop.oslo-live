using System.Text.Json;

namespace OsloLive.Tester;

public class StroemprisTester
{
    private static IReadOnlyList<JsonElement> Rader(object data) =>
        JsonSerializer.Deserialize<IReadOnlyList<JsonElement>>(JsonSerializer.Serialize(data))!;

    private static object Rad(string startLokal, double nokPerKwh) =>
        new
        {
            time_start = startLokal,
            time_end = DateTimeOffset.Parse(startLokal).AddHours(1).ToString("O"),
            NOK_per_kWh = nokPerKwh,
        };

    private static IEnumerable<object> EtDoegn(string dato, Func<int, double> pris) =>
        Enumerable.Range(0, 24).Select(t => Rad($"{dato}T{t:D2}:00:00+02:00", pris(t)));

    [Theory]
    [InlineData(1.0, 125.0)]
    [InlineData(0.8, 100.0)]
    [InlineData(0.0, 0.0)]
    public void Pris_regnes_om_til_oere_med_mva(double nokUtenMva, double forventet) =>
        Assert.Equal(forventet, Stroempris.TilOreMedMva(nokUtenMva));

    [Fact]
    public void Hele_doegnet_gir_24_timer_sortert()
    {
        var rader = Rader(EtDoegn("2026-09-23", _ => 1.0));
        var nå = DateTimeOffset.Parse("2026-09-23T00:30:00+02:00");

        var svar = Stroempris.Tolk(rader, nå);

        Assert.NotNull(svar);
        Assert.Equal(24, svar.Timer.Count);
        Assert.Equal(0, svar.Timer[0].Time);
        Assert.Equal(23, svar.Timer[23].Time);
    }

    [Fact]
    public void Billigste_og_dyreste_time_finnes()
    {
        var rader = Rader(EtDoegn("2026-09-23", t => t switch { 3 => 0.5, 18 => 2.0, _ => 1.0 }));
        var nå = DateTimeOffset.Parse("2026-09-23T03:30:00+02:00");

        var svar = Stroempris.Tolk(rader, nå);

        Assert.NotNull(svar);
        Assert.Equal(3, svar.Billigst.Time);
        Assert.Equal(18, svar.Dyrest.Time);
    }

    [Fact]
    public void Naa_er_prisen_for_timen_vi_er_i()
    {
        var rader = Rader(EtDoegn("2026-09-23", t => t));
        var nå = DateTimeOffset.Parse("2026-09-23T12:45:00+02:00");

        var svar = Stroempris.Tolk(rader, nå);

        Assert.NotNull(svar);
        Assert.Equal(1500.0, svar.Naa); // time 12: 12,0 NOK/kWh * 100 * 1,25 mva
    }

    [Fact]
    public void Tom_liste_gir_ingen_svar()
    {
        var svar = Stroempris.Tolk(Rader(Array.Empty<object>()), DateTimeOffset.UtcNow);

        Assert.Null(svar);
    }

    [Fact]
    public void Mangler_gjeldende_time_gir_ingen_svar()
    {
        var rader = Rader(EtDoegn("2026-09-23", _ => 1.0));
        var iMorgen = DateTimeOffset.Parse("2026-09-24T12:00:00+02:00");

        var svar = Stroempris.Tolk(rader, iMorgen);

        Assert.Null(svar);
    }

    [Fact]
    public void Rad_uten_pris_hoppes_over_men_feller_ikke_resten()
    {
        var rader = Rader(EtDoegn("2026-09-23", _ => 1.0)
            .Cast<object>()
            .Append(new { time_start = "2026-09-23T24:00:00+02:00" })); // ingen NOK_per_kWh
        var nå = DateTimeOffset.Parse("2026-09-23T05:30:00+02:00");

        var svar = Stroempris.Tolk(rader, nå);

        Assert.NotNull(svar);
        Assert.Equal(24, svar.Timer.Count);
    }
}
