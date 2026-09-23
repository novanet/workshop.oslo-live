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
public sealed class Allemannsdata(HttpClient http, ILogger<Allemannsdata> logg, TimeProvider? klokke = null, Kalltak? tak = null)
{
    private readonly Kalltak kalltak = tak ?? Kalltak.Felles;

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

        if (Mellomlager.TryGetValue(url, out var lagret) && DateTimeOffset.UtcNow - lagret.Hentet < Levetid)
        {
            return lagret.Svar;
        }

        logg.LogInformation("Henter {Kilde}/{Operasjon}", kilde, operasjon);

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
        return kopi;
    }

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

            await kalltak.Vent(stopp);
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
            finally
            {
                kalltak.Slipp();
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

/// <summary>
/// Taket på samtidige kall mot Allemannsdata for hele appen. Kall over taket
/// venter i kø til det blir ledig plass; de blir aldri avvist.
/// </summary>
public sealed class Kalltak
{
    /// <summary>Standard når «Allemannsdata:MaksSamtidigeKall» mangler eller er ugyldig.</summary>
    public const int StandardMaks = 4;

    /// <summary>Brukes når ingen tak er gitt inn, for eksempel i tester som lager klienten selv.</summary>
    public static readonly Kalltak Felles = new(StandardMaks);

    private readonly SemaphoreSlim plasser;

    public Kalltak(int maks)
    {
        Maks = maks < 1 ? StandardMaks : maks;
        plasser = new SemaphoreSlim(Maks, Maks);
    }

    public int Maks { get; }

    public static Kalltak FraKonfigurasjon(IConfiguration konfigurasjon) =>
        new(konfigurasjon.GetValue("Allemannsdata:MaksSamtidigeKall", StandardMaks));

    /// <summary>Venter på ledig plass. Avbrytes <paramref name="stopp"/>, forlater kallet køen.</summary>
    public Task Vent(CancellationToken stopp) => plasser.WaitAsync(stopp);

    public void Slipp() => plasser.Release();
}
