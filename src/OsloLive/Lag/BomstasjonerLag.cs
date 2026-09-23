using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Bomstasjonene i Oslo, fra Statens vegvesens Nasjonal vegdatabank (NVDB),
/// vegobjekttype 45. Allemannsdata har ingen kilde for bomstasjoner, så dette
/// laget bruker, som <see cref="FlyLag"/>, sin egen navngitte
/// <see cref="HttpClient"/> fra <see cref="IHttpClientFactory"/> og eget
/// mellomlager i delt <see cref="IMemoryCache"/>. NVDB krever headeren
/// X-Client (uten API-nøkkel). Svikter kilden, kaster <see cref="Hent"/>, og
/// laget blir rødt i lagvelgeren; det tar ikke ned resten av kartet.
/// </summary>
public sealed class BomstasjonerLag(IHttpClientFactory httpFactory, IMemoryCache mellomlager) : ILag
{
    private const string KlientNavn = "bomstasjoner";
    private const string MellomlagerNøkkel = "bomstasjoner-mellomlager";
    private const string Kilde = "Statens vegvesen, NVDB";
    private static readonly TimeSpan Levetid = TimeSpan.FromHours(1);

    /// <summary>Vegobjekttype 45 (Bomstasjon) i Oslo kommune (301), med geometri og egenskaper i WGS84.</summary>
    public const string Url =
        "https://nvdbapiles.atlas.vegvesen.no/vegobjekter/api/v4/vegobjekter/45?kommune=301&srid=4326&inkluder=geometri,egenskaper&antall=100";

    public string Id => "bomstasjoner";
    public string Navn => "Bomstasjoner";
    public string Beskrivelse => "Bomstasjonene i Oslo, med takst for liten bil i og utenfor rushtiden.";
    public string Ikon => "🚧";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var objekter = await HentObjekter(stopp);
        return Geo.Samle(objekter.Select(TilPunkt));
    }

    private async Task<IReadOnlyList<JsonElement>> HentObjekter(CancellationToken stopp)
    {
        if (mellomlager.TryGetValue(MellomlagerNøkkel, out IReadOnlyList<JsonElement>? lagret) && lagret is not null)
        {
            return lagret;
        }

        using var http = httpFactory.CreateClient(KlientNavn);
        using var svar = await http.GetAsync(Url, stopp);
        svar.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await svar.Content.ReadAsStreamAsync(stopp), cancellationToken: stopp);
        var rot = json.RootElement;

        var objekter = rot.TryGetProperty("objekter", out var o) && o.ValueKind == JsonValueKind.Array
            ? o.EnumerateArray().Select(x => x.Clone()).ToList()
            : [];

        mellomlager.Set(MellomlagerNøkkel, (IReadOnlyList<JsonElement>)objekter, Levetid);
        return objekter;
    }

    /// <summary>
    /// Tolker en WKT-punktstreng fra NVDB på formen «POINT [Z] (breddegrad
    /// lengdegrad [høyde])», altså breddegrad først. Ugyldig tekst gir null i
    /// stedet for å kaste, slik at én dårlig geometri aldri feller hele laget.
    /// </summary>
    public static (double Lat, double Lon)? TolkWkt(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return null;
        }

        var start = wkt.IndexOf('(');
        var slutt = wkt.IndexOf(')');
        if (start < 0 || slutt < 0 || slutt <= start)
        {
            return null;
        }

        var prefiks = wkt[..start].Trim();
        if (!prefiks.StartsWith("POINT", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var deler = wkt[(start + 1)..slutt].Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (deler.Length is < 2 or > 3)
        {
            return null;
        }

        if (!double.TryParse(deler[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(deler[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            return null;
        }

        return (lat, lon);
    }

    /// <summary>Én bomstasjon blir ett punkt. Mangler id eller gyldig geometri, blir det ikke noe punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement objekt)
    {
        if (!objekt.TryGetProperty("id", out var idFelt) || idFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (!objekt.TryGetProperty("geometri", out var geom)
            || !geom.TryGetProperty("wkt", out var wktFelt) || wktFelt.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var koordinat = TolkWkt(wktFelt.GetString());
        if (koordinat is null)
        {
            return null;
        }

        var id = idFelt.GetInt64().ToString(CultureInfo.InvariantCulture);
        var (lat, lon) = koordinat.Value;
        var navn = Egenskap(objekt, "Navn bomstasjon")?.GetString() ?? "Ukjent bomstasjon";

        return Geo.Lag(
            id: id,
            lat: lat,
            lon: lon,
            navn: navn,
            kilde: Kilde,
            detaljer: new Dictionary<string, object?>
            {
                ["takst"] = Kroner(objekt, "Takst liten bil"),
                ["rushtid"] = Kroner(objekt, "Rushtidstakst liten bil"),
            });
    }

    /// <summary>Finner «verdi» i egenskaper-lista der «navn» matcher, eller null om den ikke finnes.</summary>
    private static JsonElement? Egenskap(JsonElement objekt, string navn)
    {
        if (!objekt.TryGetProperty("egenskaper", out var liste) || liste.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var egenskap in liste.EnumerateArray())
        {
            if (egenskap.TryGetProperty("navn", out var navnFelt)
                && navnFelt.ValueKind == JsonValueKind.String && navnFelt.GetString() == navn
                && egenskap.TryGetProperty("verdi", out var verdi))
            {
                return verdi;
            }
        }

        return null;
    }

    /// <summary>Formaterer en takstegenskap (tall eller tekst) som «38 kr», eller null om egenskapen mangler.</summary>
    private static string? Kroner(JsonElement objekt, string navn)
    {
        var verdi = Egenskap(objekt, navn);
        if (verdi is null)
        {
            return null;
        }

        double beløp;
        if (verdi.Value.ValueKind == JsonValueKind.Number)
        {
            beløp = verdi.Value.GetDouble();
        }
        else if (verdi.Value.ValueKind == JsonValueKind.String
            && double.TryParse(verdi.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            beløp = parsed;
        }
        else
        {
            return null;
        }

        return $"{beløp.ToString("0.##", CultureInfo.InvariantCulture)} kr";
    }
}
