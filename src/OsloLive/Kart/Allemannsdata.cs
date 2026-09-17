using System.Collections.Concurrent;
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
public sealed class Allemannsdata(HttpClient http, ILogger<Allemannsdata> logg)
{
    private const string Rot = "https://allemannsdata.com/wiki/api/v1/kilder";

    /// <summary>Hvor lenge et svar gjenbrukes før vi spør kilden på nytt.</summary>
    public const int LevetidSekunder = 30;

    public static readonly TimeSpan Levetid = TimeSpan.FromMinutes(LevetidSekunder);

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
        double d => d.ToString(),
        decimal d => d.ToString(),
        float f => f.ToString(),
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

        using var svar = await http.GetAsync(url, stopp);
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
}
