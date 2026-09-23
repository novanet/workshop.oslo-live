using System.Collections.Concurrent;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Spisesteder i Oslo med siste smilefjes fra Mattilsynet.
///
/// Mattilsynet gir adresse, ikke koordinater, så hvert sted må slås opp mot
/// Kartverkets adressesøk (kilden «geonorge»). Oslo har over 1300 tilsynsobjekter,
/// og Mattilsynet-kilden gir maks ca. 100 rader per side uansett hvor stor «limit»
/// vi ber om. Derfor bygges laget opp gradvis mellom oppdateringer:
///
///   - <see cref="Stedebok"/> husker hvert sted vi har sett fra Mattilsynet, én
///     side om gangen (roterende offset), slik at vi ikke mister steder som
///     falt utenfor forrige sides ~100 rader.
///   - <see cref="Adressebok"/> husker koordinaten til hver adresse vi har slått
///     opp, slik at vi ikke spør Kartverket om samme adresse igjen. Et definitivt
///     bomtreff lagres som null; feil og tidsavbrudd lagres ikke, så de prøves på
///     nytt neste oppdatering.
///
/// Maks <see cref="MaksNyeOppslag"/> nye Kartverket-kall per oppdatering, i
/// grupper på <see cref="Samtidige"/> samtidig.
/// </summary>
public sealed class SmilefjesLag(Allemannsdata data) : ILag
{
    public string Id => "smilefjes";
    public string Navn => "Smilefjes";
    public string Beskrivelse => "Spisesteder i Oslo med siste smilefjes fra Mattilsynet.";
    public string Ikon => "😊";

    private const string KildeSmilefjes = "mattilsynet";
    private const string OperasjonSmilefjes = "search_establishments";
    private const string ListeSmilefjes = "establishments";

    private const string KildeAdresser = "geonorge";
    private const string OperasjonAdresser = "search_address";
    private const string ListeAdresser = "addresses";
    private const string KommunenummerOslo = "0301";

    private const string KildeNavn = "Smilefjes fra Mattilsynet";

    /// <summary>Rader per side fra Mattilsynet. Kilden gir uansett maks ca. 100 igjen.</summary>
    private const int SideStorrelse = 100;

    /// <summary>Maks nye Kartverket-oppslag per oppdatering.</summary>
    private const int MaksNyeOppslag = 60;

    /// <summary>Maks samtidige Kartverket-kall.</summary>
    private const int Samtidige = 8;

    private static readonly ConcurrentDictionary<string, JsonElement> Stedebok = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (double Lat, double Lon)?> Adressebok = new(StringComparer.OrdinalIgnoreCase);
    private static int _nesteOffset;

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        await HentNesteSide(stopp);

        var trengerOppslag = Stedebok.Values
            .Select(Adresse)
            .Where(adresse => !string.IsNullOrWhiteSpace(adresse) && !Adressebok.ContainsKey(adresse))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaksNyeOppslag)
            .ToList();

        await SlåOppAdresser(trengerOppslag, stopp);

        return Bygg(Stedebok.Values, rad =>
            Adressebok.TryGetValue(Adresse(rad), out var koordinat) ? koordinat : null);
    }

    private async Task HentNesteSide(CancellationToken stopp)
    {
        var offset = _nesteOffset;

        var side = await data.Hent(
            KildeSmilefjes,
            OperasjonSmilefjes,
            new Dictionary<string, object>
            {
                ["poststed"] = "Oslo",
                ["min_karakter"] = 0,
                ["limit"] = SideStorrelse,
                ["offset"] = offset,
            },
            stopp);

        var rader = side.TryGetProperty(ListeSmilefjes, out var liste) && liste.ValueKind == JsonValueKind.Array
            ? liste.EnumerateArray().ToList()
            : [];

        foreach (var rad in rader)
        {
            var id = rad.TryGetProperty("establishment_id", out var idProp) ? idProp.GetString() : null;
            if (!string.IsNullOrEmpty(id))
            {
                Stedebok[id] = rad.Clone();
            }
        }

        var total = side.TryGetProperty("total", out var totalProp) ? totalProp.GetInt32() : (int?)null;
        var nesteOffset = offset + rader.Count;
        _nesteOffset = ErSisteSide(rader.Count, nesteOffset, total) ? 0 : nesteOffset;
    }

    /// <summary>
    /// Er dette siste side fra Mattilsynet? Bruker «total» når kilden gir det,
    /// ellers antar vi at en side som ikke er full må være den siste, slik at
    /// vi aldri sitter fast på side 1 om «total» skulle mangle fra svaret.
    /// </summary>
    public static bool ErSisteSide(int antallRader, int nesteOffset, int? total) =>
        antallRader == 0 || antallRader < SideStorrelse || (total is not null && nesteOffset >= total);

    private async Task SlåOppAdresser(IReadOnlyList<string> adresser, CancellationToken stopp)
    {
        using var samtidighet = new SemaphoreSlim(Samtidige);

        var oppslag = adresser.Select(async adresse =>
        {
            await samtidighet.WaitAsync(stopp);
            try
            {
                var treff = await data.HentListe(
                    KildeAdresser,
                    OperasjonAdresser,
                    new Dictionary<string, object>
                    {
                        ["text"] = adresse,
                        ["kommunenummer"] = KommunenummerOslo,
                        ["limit"] = 1,
                    },
                    ListeAdresser,
                    stopp);

                var rad = treff.FirstOrDefault();
                Adressebok[adresse] = rad.ValueKind == JsonValueKind.Object
                    && rad.TryGetProperty("lat", out var latProp) && latProp.ValueKind == JsonValueKind.Number
                    && rad.TryGetProperty("lon", out var lonProp) && lonProp.ValueKind == JsonValueKind.Number
                    ? (latProp.GetDouble(), lonProp.GetDouble())
                    : null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ett feilende adresseoppslag skal ikke stoppe de andre eller kartet;
                // adressen er ikke lagret i Adressebok, så den prøves på nytt neste oppdatering.
            }
            finally
            {
                samtidighet.Release();
            }
        });

        await Task.WhenAll(oppslag);
    }

    /// <summary>Karakter og ikon for en smilefjeskarakter, eller null for en ukjent verdi.</summary>
    public static (string Karakter, string Ikon)? Karakter(int totalKarakter) => totalKarakter switch
    {
        0 or 1 => ("blid", "😊"),
        2 => ("streng", "😐"),
        3 => ("sur", "😠"),
        _ => null,
    };

    /// <summary>Bygger en lesbar adresse av «adresse», «postnr» og «poststed».</summary>
    public static string Adresse(JsonElement rad)
    {
        var gate = rad.TryGetProperty("adresse", out var a) ? a.GetString() : null;
        var postnr = rad.TryGetProperty("postnr", out var p) ? p.GetString() : null;
        var poststed = rad.TryGetProperty("poststed", out var s) ? s.GetString() : null;

        var postDel = string.Join(" ", new[] { postnr, poststed }.Where(d => !string.IsNullOrWhiteSpace(d)));
        var deler = new[] { gate, postDel }.Where(d => !string.IsNullOrWhiteSpace(d));

        return string.Join(", ", deler);
    }

    /// <summary>Bygger ett kartpunkt for en rad, eller null hvis karakteren er ukjent eller mangler.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad, double lat, double lon)
    {
        if (!rad.TryGetProperty("karakter", out var k) || k.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var karakter = Karakter(k.GetInt32());
        if (karakter is null)
        {
            return null;
        }

        var id = rad.TryGetProperty("establishment_id", out var idProp) ? idProp.GetString() ?? "" : "";
        var navn = rad.TryGetProperty("navn", out var navnProp) ? navnProp.GetString() : null;
        var dato = rad.TryGetProperty("dato", out var datoProp) ? datoProp.GetString() : null;

        return Geo.Lag(
            id: id,
            lat: lat,
            lon: lon,
            navn: navn ?? "Ukjent spisested",
            kilde: KildeNavn,
            detaljer: new Dictionary<string, object?>
            {
                ["karakter"] = karakter.Value.Karakter,
                ["tilsyn"] = dato,
                ["adresse"] = Adresse(rad),
                ["ikon"] = karakter.Value.Ikon,
            });
    }

    /// <summary>Bygger det ferdige laget. Nettverksfri: koordinaten kommer fra <paramref name="koordinat"/>.</summary>
    public static Kartlag Bygg(IEnumerable<JsonElement> steder, Func<JsonElement, (double Lat, double Lon)?> koordinat)
    {
        var punkter = steder.Select(rad =>
        {
            var funnet = koordinat(rad);
            return funnet is null ? null : TilPunkt(rad, funnet.Value.Lat, funnet.Value.Lon);
        });

        return Geo.Samle(punkter);
    }
}
