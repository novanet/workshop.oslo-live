namespace OsloLive;

/// <summary>
/// Gjør et unntak fra en kildehenting om til en kort norsk feiltekst for API-svaret.
/// Ingen interne detaljer fra unntaket lekker ut; hele unntaket logges separat der det kastes.
/// </summary>
public static class Feiltekst
{
    public static string Fra(Exception unntak) => unntak switch
    {
        HttpRequestException { StatusCode: { } kode } => $"Kilden svarte {(int)kode}.",
        TimeoutException => "Kilden svarte ikke i tide.",
        TaskCanceledException { InnerException: TimeoutException } => "Kilden svarte ikke i tide.",
        _ => "Laget kunne ikke hentes.",
    };
}
