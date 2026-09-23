using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Aktive avvik i kollektivtrafikken til Ruter: holdeplasser som er flyttet,
/// stasjoner som er stengt, linjer som ikke går. Allemannsdata har ingen kilde
/// for avvik, så dette laget bruker, som <see cref="FlyLag"/>, sin egen
/// navngitte <see cref="HttpClient"/> fra <see cref="IHttpClientFactory"/> og
/// eget mellomlager i delt <see cref="IMemoryCache"/>. Kilden er Enturs
/// Journey Planner v3 (GraphQL), som krever headeren ET-Client-Name (uten
/// API-nøkkel). Svikter kilden, kaster <see cref="Hent"/>, og laget blir
/// rødt i lagvelgeren; det tar ikke ned resten av kartet.
/// </summary>
public sealed class KollektivavvikLag(IHttpClientFactory httpFactory, IMemoryCache mellomlager) : ILag
{
    private const string KlientNavn = "entur";
    private const string MellomlagerNøkkel = "kollektivavvik-mellomlager";
    private const string Kilde = "Entur";
    private const string Url = "https://api.entur.io/journey-planner/v3/graphql";
    private static readonly TimeSpan Levetid = TimeSpan.FromSeconds(60);

    public string Id => "kollektivavvik";
    public string Navn => "Avvik i kollektivtrafikken";
    public string Beskrivelse => "Aktive avvik fra Ruter som rammer holdeplasser og stasjoner i Oslo.";
    public string Ikon => "⚠️";

    /// <summary>
    /// Henter alle avvik (situations) for Ruter, med holdeplassene de berører,
    /// enten direkte (<c>stopPlaces</c>) eller via en linje (<c>stopPlacesOnLines</c>).
    /// </summary>
    public const string Spørring = """
        {
          situations(codespaces: ["RUT"]) {
            id
            summary { value language }
            severity
            validityPeriod { startTime endTime }
            affects {
              stopPlaces { stopPlace { id name latitude longitude } }
              stopPlacesOnLines { stopPlace { id name latitude longitude } }
            }
          }
        }
        """;

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var situasjoner = await HentSituasjoner(stopp);
        return TilPunkter(situasjoner, DateTimeOffset.UtcNow);
    }

    private async Task<JsonElement> HentSituasjoner(CancellationToken stopp)
    {
        if (mellomlager.TryGetValue(MellomlagerNøkkel, out JsonElement lagret) && lagret.ValueKind != JsonValueKind.Undefined)
        {
            return lagret;
        }

        using var http = httpFactory.CreateClient(KlientNavn);
        using var kropp = new StringContent(JsonSerializer.Serialize(new { query = Spørring }), Encoding.UTF8, "application/json");
        using var svar = await http.PostAsync(Url, kropp, stopp);
        svar.EnsureSuccessStatusCode();

        var tekst = await svar.Content.ReadAsStringAsync(stopp);
        var rot = JsonSerializer.Deserialize<JsonElement>(tekst);
        var situasjoner = rot.GetProperty("data").GetProperty("situations").Clone();

        mellomlager.Set(MellomlagerNøkkel, situasjoner, Levetid);
        return situasjoner;
    }

    /// <summary>
    /// Oversetter et sett situasjoner fra Entur til kartpunkter: ett punkt per
    /// kombinasjon av aktivt avvik og berørt holdeplass. Avvik som er utløpt,
    /// ikke har startet ennå, eller ikke berører noen holdeplass med
    /// koordinater, gir ingen punkter.
    /// </summary>
    public static Kartlag TilPunkter(JsonElement situasjoner, DateTimeOffset nå)
    {
        if (situasjoner.ValueKind != JsonValueKind.Array)
        {
            return new Kartlag("FeatureCollection", []);
        }

        var punkter = new List<Kartpunkt?>();
        foreach (var situasjon in situasjoner.EnumerateArray())
        {
            if (situasjon.ValueKind != JsonValueKind.Object || !ErAktiv(situasjon, nå))
            {
                continue;
            }

            if (!situasjon.TryGetProperty("id", out var idFelt) || idFelt.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var avvikId = idFelt.GetString()!;
            var sammendrag = VelgTekst(situasjon);
            var alvorlighet = situasjon.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : "ukjent";
            var gjelderTil = GjelderTil(situasjon);

            foreach (var holdeplass in BerørteHoldeplasser(situasjon))
            {
                punkter.Add(TilPunkt(avvikId, holdeplass, sammendrag, alvorlighet!, gjelderTil));
            }
        }

        return Geo.Samle(punkter);
    }

    /// <summary>
    /// Aktiv nå betyr at «nå» ligger innenfor <c>validityPeriod</c>. Mangler
    /// starttidspunktet, regnes avviket som allerede i gang. Mangler
    /// sluttidspunktet, regnes avviket som aktivt inntil videre.
    /// </summary>
    private static bool ErAktiv(JsonElement situasjon, DateTimeOffset nå)
    {
        if (!situasjon.TryGetProperty("validityPeriod", out var periode) || periode.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        if (periode.TryGetProperty("startTime", out var startFelt)
            && startFelt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(startFelt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            && nå < start)
        {
            return false;
        }

        if (periode.TryGetProperty("endTime", out var sluttFelt)
            && sluttFelt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(sluttFelt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var slutt)
            && nå > slutt)
        {
            return false;
        }

        return true;
    }

    /// <summary>«gjelder til»-teksten i popup-en: sluttidspunktet, eller «inntil videre» uten det.</summary>
    private static string GjelderTil(JsonElement situasjon)
    {
        if (situasjon.TryGetProperty("validityPeriod", out var periode)
            && periode.ValueKind == JsonValueKind.Object
            && periode.TryGetProperty("endTime", out var sluttFelt)
            && sluttFelt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(sluttFelt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var slutt))
        {
            return slutt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return "inntil videre";
    }

    /// <summary>Norsk sammendrag om det finnes, ellers det første som følger med avviket.</summary>
    private static string VelgTekst(JsonElement situasjon)
    {
        if (!situasjon.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Array)
        {
            return "Ukjent avvik";
        }

        JsonElement? valgt = null;
        foreach (var element in summary.EnumerateArray())
        {
            valgt ??= element;

            if (element.TryGetProperty("language", out var språk) && språk.ValueKind == JsonValueKind.String
                && språk.GetString() is "no" or "nb" or "nob")
            {
                valgt = element;
                break;
            }
        }

        return valgt is { } tekst && tekst.TryGetProperty("value", out var verdi) && verdi.ValueKind == JsonValueKind.String
            ? verdi.GetString()!
            : "Ukjent avvik";
    }

    /// <summary>
    /// Holdeplassene et avvik berører, hentet fra både <c>stopPlaces</c>
    /// (<c>AffectedStopPlace</c>) og <c>stopPlacesOnLines</c>
    /// (<c>AffectedStopPlaceOnLine</c>). Avvik som bare berører linjer, uten
    /// noen av delene, gir ingen holdeplasser.
    /// </summary>
    private static IEnumerable<JsonElement> BerørteHoldeplasser(JsonElement situasjon)
    {
        if (!situasjon.TryGetProperty("affects", out var affects) || affects.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var gruppenavn in new[] { "stopPlaces", "stopPlacesOnLines" })
        {
            if (!affects.TryGetProperty(gruppenavn, out var gruppe) || gruppe.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var element in gruppe.EnumerateArray())
            {
                if (element.TryGetProperty("stopPlace", out var holdeplass) && holdeplass.ValueKind == JsonValueKind.Object)
                {
                    yield return holdeplass;
                }
            }
        }
    }

    /// <summary>Én holdeplass berørt av ett avvik blir ett punkt. Mangler id eller koordinater, blir det ikke noe punkt.</summary>
    private static Kartpunkt? TilPunkt(string avvikId, JsonElement holdeplass, string avvik, string alvorlighet, string gjelderTil)
    {
        if (!holdeplass.TryGetProperty("id", out var idFelt) || idFelt.ValueKind != JsonValueKind.String
            || !holdeplass.TryGetProperty("latitude", out var latFelt) || latFelt.ValueKind != JsonValueKind.Number
            || !holdeplass.TryGetProperty("longitude", out var lonFelt) || lonFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var holdeplassId = idFelt.GetString()!;
        var navn = holdeplass.TryGetProperty("name", out var navnFelt) && navnFelt.ValueKind == JsonValueKind.String
            ? navnFelt.GetString()!
            : "Ukjent holdeplass";

        return Geo.Lag(
            id: $"{avvikId}|{holdeplassId}",
            lat: latFelt.GetDouble(),
            lon: lonFelt.GetDouble(),
            navn: navn,
            kilde: Kilde,
            detaljer: new Dictionary<string, object?>
            {
                ["avvik"] = avvik,
                ["alvorlighet"] = alvorlighet,
                ["gjelder til"] = gjelderTil,
            });
    }
}
