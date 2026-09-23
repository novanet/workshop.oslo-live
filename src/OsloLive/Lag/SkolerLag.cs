using System.Collections.Concurrent;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Skoler og barnehager i Oslo, fra Udir sine registre (kilden «utdanning»).
///
/// Søkeoperasjonene <c>search_schools</c> og <c>search_kindergartens</c> gir bare orgnr og
/// type-/eierform-flagg, ikke koordinater. Koordinat og adresse må slås opp per enhet med
/// <c>get_unit</c>. Oslo har til sammen rundt 1300 rader i de to søkene, samme størrelsesorden
/// som Mattilsynets tilsynsobjekter i <see cref="SmilefjesLag"/>. Laget bygges derfor opp
/// gradvis på samme måte:
///
///   - <see cref="Skolebok"/> og <see cref="Barnehagebok"/> husker hver enhet vi har sett,
///     én side (100 rader) fra hvert søk per oppdatering, med roterende offset.
///   - <see cref="Enhetsbok"/> husker koordinat og adresse for hvert orgnr vi har slått opp,
///     slik at vi ikke spør <c>get_unit</c> om samme enhet igjen. En nedlagt enhet eller en
///     enhet uten koordinat lagres som null; feil og tidsavbrudd lagres ikke, så de prøves
///     på nytt neste oppdatering.
///
/// Maks <see cref="MaksNyeOppslag"/> nye <c>get_unit</c>-kall per oppdatering, i grupper på
/// <see cref="Samtidige"/> samtidig. Ingen Kartverket-kall: koordinatene kommer fra samme kilde.
/// Per oppdatering: 2 søkekall + maks 60 get_unit (8 samtidig).
/// </summary>
public sealed class SkolerLag(Allemannsdata data) : ILag
{
    public string Id => "skoler";
    public string Navn => "Skoler";
    public string Beskrivelse => "Skoler og barnehager i Oslo, med type og eierform fra Udir sine registre.";
    public string Ikon => "🏫";

    private const string Kilde = "utdanning";
    private const string OperasjonSkoler = "search_schools";
    private const string OperasjonBarnehager = "search_kindergartens";
    private const string OperasjonEnhet = "get_unit";
    private const string Liste = "units";
    private const string KommunenummerOslo = "0301";
    private const string KildeNavn = "Nasjonalt skoleregister / barnehageregister (Udir)";

    /// <summary>Rader per side. Kildene gir uansett maks ca. 100 igjen.</summary>
    private const int SideStorrelse = 100;

    /// <summary>Maks nye enhetsoppslag (get_unit) per oppdatering.</summary>
    private const int MaksNyeOppslag = 60;

    /// <summary>Maks samtidige enhetsoppslag.</summary>
    private const int Samtidige = 8;

    private static readonly ConcurrentDictionary<string, JsonElement> Skolebok = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, JsonElement> Barnehagebok = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Enhet?> Enhetsbok = new(StringComparer.OrdinalIgnoreCase);
    private static int _nesteSkoleoffset;
    private static int _nesteBarnehageoffset;

    /// <summary>Koordinat og adresse for én enhet, fra <c>get_unit</c>.</summary>
    public sealed record Enhet(double Lat, double Lon, string? Adresse);

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        await HentNesteSide(OperasjonSkoler, Skolebok, erBarnehage: false, stopp);
        await HentNesteSide(OperasjonBarnehager, Barnehagebok, erBarnehage: true, stopp);

        var rader = Skolebok.Values.Select(rad => (Rad: rad, ErBarnehage: false))
            .Concat(Barnehagebok.Values.Select(rad => (Rad: rad, ErBarnehage: true)))
            .ToList();

        var trengerOppslag = rader
            .Select(r => Tekst(r.Rad, "organization_id"))
            .Where(orgnr => !string.IsNullOrEmpty(orgnr) && !Enhetsbok.ContainsKey(orgnr!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaksNyeOppslag)
            .Select(orgnr => orgnr!)
            .ToList();

        await SlåOppEnheter(trengerOppslag, stopp);

        return Bygg(rader, rad =>
        {
            var orgnr = Tekst(rad, "organization_id") ?? "";
            return Enhetsbok.TryGetValue(orgnr, out var enhet) ? enhet : null;
        });
    }

    /// <summary>Henter neste side fra ett av de to søkene og legger nye rader i boken.</summary>
    private async Task HentNesteSide(
        string operasjon,
        ConcurrentDictionary<string, JsonElement> bok,
        bool erBarnehage,
        CancellationToken stopp)
    {
        var offset = erBarnehage ? _nesteBarnehageoffset : _nesteSkoleoffset;

        var side = await data.Hent(
            Kilde,
            operasjon,
            new Dictionary<string, object>
            {
                ["kommunenr"] = KommunenummerOslo,
                ["limit"] = SideStorrelse,
                ["offset"] = offset,
            },
            stopp);

        var rader = side.TryGetProperty(Liste, out var liste) && liste.ValueKind == JsonValueKind.Array
            ? liste.EnumerateArray().ToList()
            : [];

        foreach (var rad in rader.Where(rad => Type(rad, erBarnehage) is not null))
        {
            var id = Tekst(rad, "organization_id");
            if (!string.IsNullOrEmpty(id))
            {
                bok[id] = rad.Clone();
            }
        }

        var total = side.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == JsonValueKind.Number
            ? totalProp.GetInt32()
            : (int?)null;
        var nesteOffset = offset + rader.Count;
        var nyOffset = ErSisteSide(rader.Count, nesteOffset, total) ? 0 : nesteOffset;

        if (erBarnehage)
        {
            _nesteBarnehageoffset = nyOffset;
        }
        else
        {
            _nesteSkoleoffset = nyOffset;
        }
    }

    /// <summary>
    /// Er dette siste side fra kilden? Bruker «total» når kilden gir det, ellers antar vi at
    /// en side som ikke er full må være den siste, slik at vi aldri sitter fast på side 1.
    /// </summary>
    public static bool ErSisteSide(int antallRader, int nesteOffset, int? total) =>
        antallRader == 0 || antallRader < SideStorrelse || (total is not null && nesteOffset >= total);

    private async Task SlåOppEnheter(IReadOnlyList<string> orgnumre, CancellationToken stopp)
    {
        using var samtidighet = new SemaphoreSlim(Samtidige);

        var oppslag = orgnumre.Select(async orgnr =>
        {
            await samtidighet.WaitAsync(stopp);
            try
            {
                var enhet = await data.Hent(
                    Kilde,
                    OperasjonEnhet,
                    new Dictionary<string, object> { ["organization_id"] = orgnr },
                    stopp);

                Enhetsbok[orgnr] = TilEnhet(enhet);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ett feilende enhetsoppslag skal ikke stoppe de andre eller kartet;
                // orgnr er ikke lagret i Enhetsbok, så det prøves på nytt neste oppdatering.
            }
            finally
            {
                samtidighet.Release();
            }
        });

        await Task.WhenAll(oppslag);
    }

    /// <summary>Leser tekstfeltet <paramref name="navn"/> av en rad, eller null hvis det mangler eller ikke er tekst.</summary>
    private static string? Tekst(JsonElement rad, string navn) =>
        rad.TryGetProperty(navn, out var verdi) && verdi.ValueKind == JsonValueKind.String ? verdi.GetString() : null;

    /// <summary>Leser flagget <paramref name="navn"/> av en rad, uten å kaste hvis det mangler eller ikke er en bool.</summary>
    private static bool Flagg(JsonElement rad, string navn) =>
        rad.TryGetProperty(navn, out var verdi) && verdi.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Type ut fra registerets egne flagg: barnehage for barnehagesøket, ellers grunnskole
    /// eller videregående. Enheter uten noen av flaggene (fagskoler, treningssentre og annet
    /// Udir har registrert som «skole») gir null og vises ikke.
    /// </summary>
    public static string? Type(JsonElement rad, bool erBarnehage)
    {
        if (erBarnehage)
        {
            return "barnehage";
        }

        if (Flagg(rad, "ErGrunnskole"))
        {
            return "grunnskole";
        }

        if (Flagg(rad, "ErVideregaaendeSkole"))
        {
            return "videregående";
        }

        return null;
    }

    /// <summary>
    /// Eierform ut fra privat-flagget og typen: privat hvis privat-flagget er satt, ellers
    /// fylkeskommunal for videregående og kommunal for grunnskole og barnehage.
    /// </summary>
    public static string Eierform(JsonElement rad, string type)
    {
        if (Flagg(rad, "ErPrivatskole") || Flagg(rad, "ErPrivatBarnehage"))
        {
            return "privat";
        }

        return type == "videregående" ? "fylkeskommunal" : "kommunal";
    }

    /// <summary>
    /// Tolker svaret fra <c>get_unit</c>: null hvis enheten er nedlagt, mangler koordinat,
    /// eller koordinatfeltene ikke er tall.
    /// </summary>
    public static Enhet? TilEnhet(JsonElement enhet)
    {
        if (enhet.TryGetProperty("ErAktiv", out var aktiv) && aktiv.ValueKind == JsonValueKind.False)
        {
            return null;
        }

        if (!enhet.TryGetProperty("Koordinat", out var koordinat) || koordinat.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!koordinat.TryGetProperty("Breddegrad", out var lat) || lat.ValueKind != JsonValueKind.Number
            || !koordinat.TryGetProperty("Lengdegrad", out var lon) || lon.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        string? adresse = null;
        if (enhet.TryGetProperty("Beliggenhetsadresse", out var beliggenhet) && beliggenhet.ValueKind == JsonValueKind.Object)
        {
            var gate = Tekst(beliggenhet, "Adresse");
            var postnr = Tekst(beliggenhet, "Postnr");
            var poststed = Tekst(beliggenhet, "Poststed");

            var postDel = string.Join(" ", new[] { postnr, poststed }.Where(d => !string.IsNullOrWhiteSpace(d)));
            var deler = new[] { gate, postDel }.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            adresse = deler.Count == 0 ? null : string.Join(", ", deler);
        }

        return new Enhet(lat.GetDouble(), lon.GetDouble(), adresse);
    }

    /// <summary>Bygger ett kartpunkt for en rad, eller null hvis raden ikke har en gyldig type eller orgnr.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad, bool erBarnehage, Enhet enhet)
    {
        var type = Type(rad, erBarnehage);
        if (type is null)
        {
            return null;
        }

        var orgnr = Tekst(rad, "organization_id");
        if (string.IsNullOrEmpty(orgnr))
        {
            return null;
        }

        var navn = Tekst(rad, "Navn");
        var standardnavn = erBarnehage ? "Ukjent barnehage" : "Ukjent skole";

        return Geo.Lag(
            id: (erBarnehage ? "barnehage:" : "skole:") + orgnr,
            lat: enhet.Lat,
            lon: enhet.Lon,
            navn: string.IsNullOrWhiteSpace(navn) ? standardnavn : navn,
            kilde: KildeNavn,
            detaljer: new Dictionary<string, object?>
            {
                ["type"] = type,
                ["eierform"] = Eierform(rad, type),
                ["adresse"] = enhet.Adresse,
                ["ikon"] = erBarnehage ? "🧸" : "🏫",
            });
    }

    /// <summary>Bygger det ferdige laget. Nettverksfri: koordinaten kommer fra <paramref name="enhet"/>.</summary>
    public static Kartlag Bygg(IEnumerable<(JsonElement Rad, bool ErBarnehage)> rader, Func<JsonElement, Enhet?> enhet)
    {
        var punkter = rader.Select(r =>
        {
            var funnet = enhet(r.Rad);
            return funnet is null ? null : TilPunkt(r.Rad, r.ErBarnehage, funnet);
        });

        return Geo.Samle(punkter);
    }
}
