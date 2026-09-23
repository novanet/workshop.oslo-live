using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace OsloLive.Kart;

/// <summary>
/// Klienten mot Allemannsdata. Alle lag henter data herfra.
///
/// Allemannsdata er norske offentlige data pakket som MCP-servere, men hver
/// operasjon har også en vanlig JSON-adresse du kan kalle med GET:
///
///     https://allemannsdata.com/wiki/api/v1/kilder/{kilde}/{operasjon}?param=verdi
///
/// Ingen API-nøkkel. Svaret ser alltid slik ut:
///
///     { "source": "...", "operation": "...", "parameters": {...}, "data": ... }
///
/// «data» er enten en liste rett ut, eller et objekt med listen inni
/// (for eksempel { "vehicles": [...] }). Bruk MCP-serveren til å finne ut
/// hvilken form den kilden du jobber med har - se README.
/// </summary>
public sealed class Allemannsdata(HttpClient http, ILogger<Allemannsdata> logg, TimeProvider? klokke = null)
{
    private const string Rot = "https://allemannsdata.com/wiki/api/v1/kilder";

    /// <summary>Hvor lenge et svar gjenbrukes før vi spør kilden på nytt.</summary>
    public const int LevetidSekunder = 30;

    public static readonly TimeSpan Levetid = TimeSpan.FromSeconds(LevetidSekunder);

    /// <summary>Maks antall forsøk mot en kilde, inkludert det første.</summary>
    private const int MaksForsøk = 3;

    /// <summary>Ventetid før andre og tredje forsøk. Lengre for hvert forsøk.</summary>
    private static readonly TimeSpan[] Ventetider = [TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1500)];

    private static readonly ConcurrentDictionary<string, (DateTimeOffset Hentet, JsonElement Svar)> Mellomlager = new();

    private static readonly JsonSerializerOptions Valg = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Bygger adressen til en operasjon. Parameterverdier må skrives på
    /// engelsk tallformat uansett hvilket språk maskinen kjører med.
    /// </summary>
    public static string ByggUrl(string kilde, string operasjon, IReadOnlyDictionary<string, object> parametre)
    {
        var deler = parametre.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(Formater(p.Value))}");
        return $"{Rot}/{kilde}/{operasjon}?{string.Join("&", deler)}";
    }

    private static string Formater(object verdi) => verdi switch
    {
        double d => d.ToString(CultureInfo.InvariantCulture),
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => verdi.ToString() ?? "",
    };

    /// <summary>
    /// Kaller en operasjon og gir tilbake innholdet i «data».
    /// Svaret mellomlagres i <see cref="Levetid"/>, slik at ti brukere på
    /// storskjermen ikke blir ti kall mot kilden.
    /// </summary>
    public async Task<JsonElement> Hent(
        string kilde,
        string operasjon,
        IReadOnlyDictionary<string, object> parametre,
        CancellationToken stopp = default)
    {
        var url = ByggUrl(kilde, operasjon, parametre);
        var start = (klokke ?? TimeProvider.System).GetTimestamp();

        if (Mellomlager.TryGetValue(url, out var lagret) && DateTimeOffset.UtcNow - lagret.Hentet < Levetid)
        {
            LoggUtfall(LogLevel.Information, null, kilde, operasjon, start, mellomlager: true, "ok");
            return lagret.Svar;
        }

        try
        {
            using var svar = await HentMedNyeForsøk(kilde, operasjon, url, stopp);
            svar.EnsureSuccessStatusCode();

            var tekst = await svar.Content.ReadAsStringAsync(stopp);
            var rot = JsonSerializer.Deserialize<JsonElement>(tekst, Valg);

            if (!rot.TryGetProperty("data", out var data))
            {
                throw new InvalidOperationException($"Svaret fra {kilde}/{operasjon} hadde ingen «data».");
            }

            // JsonElement peker inn i dokumentet sitt, så vi tar en kopi som overlever.
            var kopi = data.Clone();
            Mellomlager[url] = (DateTimeOffset.UtcNow, kopi);
            LoggUtfall(LogLevel.Information, null, kilde, operasjon, start, mellomlager: false, "ok");
            return kopi;
        }
        catch (Exception feil)
        {
            var (nivå, utfall) = Klassifiser(feil, stopp);
            LoggUtfall(nivå, feil, kilde, operasjon, start, mellomlager: false, utfall);
            throw;
        }
    }

    /// <summary>
    /// Skriver én logglinje per kall til <see cref="Hent"/>, med navngitte felter slik at loggen
    /// kan filtreres i Azure. Aldri med url eller parametre; se <see cref="ByggUrl"/> for de.
    /// </summary>
    private void LoggUtfall(LogLevel nivå, Exception? feil, string kilde, string operasjon, long start, bool mellomlager, string utfall) =>
        logg.Log(nivå, feil,
            "Hentet {Kilde}/{Operasjon} på {VarighetMs} ms, mellomlager {Mellomlager}, utfall {Utfall}",
            kilde, operasjon, (long)(klokke ?? TimeProvider.System).GetElapsedTime(start).TotalMilliseconds, mellomlager, utfall);

    /// <summary>
    /// Skiller feil kilden har skylden for (Warning) fra feil i vår egen tolkning av svaret (Error).
    /// Et avbrudd via <paramref name="stopp"/> er verken/eller og logges informativt.
    /// </summary>
    private static (LogLevel Nivå, string Utfall) Klassifiser(Exception feil, CancellationToken stopp) => feil switch
    {
        HttpRequestException { StatusCode: { } status } => (LogLevel.Warning, ((int)status).ToString(CultureInfo.InvariantCulture)),
        HttpRequestException => (LogLevel.Warning, "nettverksfeil"),
        OperationCanceledException when stopp.IsCancellationRequested => (LogLevel.Information, "avbrutt"),
        OperationCanceledException => (LogLevel.Warning, "tidsavbrudd"),
        _ => (LogLevel.Error, "feil"),
    };

    /// <summary>Om <paramref name="feil"/> skyldes kilden (Warning), ikke koden vår (Error). Brukes av endepunktene i Program.cs.</summary>
    public static bool ErKildefeil(Exception feil, CancellationToken stopp) =>
        feil is HttpRequestException || (feil is OperationCanceledException && !stopp.IsCancellationRequested);

    /// <summary>
    /// Som <see cref="Hent"/>, men plukker ut listen når «data» er et objekt
    /// med listen inni. <paramref name="liste"/> er navnet på feltet, for
    /// eksempel «vehicles» eller «items». Er «data» allerede en liste,
    /// brukes den som den er.
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> HentListe(
        string kilde,
        string operasjon,
        IReadOnlyDictionary<string, object> parametre,
        string? liste = null,
        CancellationToken stopp = default)
    {
        var data = await Hent(kilde, operasjon, parametre, stopp);

        if (data.ValueKind == JsonValueKind.Object && liste is not null && data.TryGetProperty(liste, out var inni))
        {
            data = inni;
        }

        return data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToList()
            : [];
    }

    /// <summary>Tømmer mellomlageret. Brukes av testene.</summary>
    public static void TømMellomlager() => Mellomlager.Clear();

    /// <summary>
    /// Henter <paramref name="url"/> og prøver på nytt inntil <see cref="MaksForsøk"/> ganger
    /// ved 5xx-svar, tidsavbrudd eller nettverksfeil. En 404 eller annen 4xx-feil kastes
    /// videre etter første forsøk. Ventetiden mellom forsøkene respekterer <paramref name="stopp"/>.
    /// </summary>
    private async Task<HttpResponseMessage> HentMedNyeForsøk(string kilde, string operasjon, string url, CancellationToken stopp)
    {
        for (var forsøk = 1; ; forsøk++)
        {
            HttpResponseMessage? svar = null;

            try
            {
                svar = await http.GetAsync(url, stopp);
            }
            catch (HttpRequestException) when (forsøk < MaksForsøk)
            {
            }
            catch (TaskCanceledException) when (forsøk < MaksForsøk && !stopp.IsCancellationRequested)
            {
            }

            if (svar is not null)
            {
                if ((int)svar.StatusCode < 500 || forsøk == MaksForsøk)
                {
                    return svar;
                }

                svar.Dispose();
            }

            logg.LogWarning(
                "Prøver {Kilde}/{Operasjon} på nytt, forsøk {Forsøk} av {MaksForsøk}",
                kilde, operasjon, forsøk + 1, MaksForsøk);

            await Task.Delay(Ventetider[forsøk - 1], klokke ?? TimeProvider.System, stopp);
        }
    }
}
