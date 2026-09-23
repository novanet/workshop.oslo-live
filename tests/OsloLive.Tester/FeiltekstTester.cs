using System.Net;

namespace OsloLive.Tester;

/// <summary>Tester at <see cref="Feiltekst.Fra"/> gir korte, norske feiltekster uten interne detaljer.</summary>
public class FeiltekstTester
{
    [Fact]
    public void HttpRequestException_med_statuskode_gir_kilden_svarte_koden()
    {
        var unntak = new HttpRequestException("Response status code does not indicate success: 403 (Forbidden).", null, HttpStatusCode.Forbidden);

        Assert.Equal("Kilden svarte 403.", Feiltekst.Fra(unntak));
    }

    [Fact]
    public void TimeoutException_gir_kilden_svarte_ikke_i_tide()
    {
        Assert.Equal("Kilden svarte ikke i tide.", Feiltekst.Fra(new TimeoutException()));
    }

    [Fact]
    public void TaskCanceledException_med_timeoutexception_inni_gir_kilden_svarte_ikke_i_tide()
    {
        var unntak = new TaskCanceledException("Tidsavbrudd.", new TimeoutException());

        Assert.Equal("Kilden svarte ikke i tide.", Feiltekst.Fra(unntak));
    }

    [Fact]
    public void Andre_unntak_gir_laget_kunne_ikke_hentes()
    {
        Assert.Equal("Laget kunne ikke hentes.", Feiltekst.Fra(new InvalidOperationException("Interne detaljer som ikke skal lekke.")));
    }

    [Fact]
    public void HttpRequestException_uten_statuskode_gir_laget_kunne_ikke_hentes()
    {
        var unntak = new HttpRequestException("Kilden er nede.");

        Assert.Equal("Laget kunne ikke hentes.", Feiltekst.Fra(unntak));
    }
}
