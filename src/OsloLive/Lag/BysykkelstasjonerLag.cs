using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Bysykkelstasjonene til Oslo Bysykkel, med hvor fulle de er akkurat nå.
///
/// Kilden er Oslo Bysykkels åpne GBFS-strøm, ikke Allemannsdata: der finnes
/// ikke antall ledige låser per stasjon. Som i <see cref="FlyLag"/> er bruddet
/// isolert her: eget navngitt <see cref="HttpClient"/> fra
/// <see cref="IHttpClientFactory"/> og mellomlager i delt <see cref="IMemoryCache"/>.
/// Oslo Bysykkel krever headeren «Client-Identifier» på formen firma-appnavn.
/// Laget «mobilitet» viser enkeltsykler via Entur og er upåvirket.
/// </summary>
public sealed class BysykkelstasjonerLag(IHttpClientFactory httpFactory, IMemoryCache mellomlager) : ILag
{
    public const string KlientNavn = "bysykkelstasjoner";
    public const string KlientIdentifikator = "novanet-oslolive";
    private const string MellomlagerNøkkel = "bysykkelstasjoner-mellomlager";
    private const string Kilde = "Oslo Bysykkel (GBFS)";
    private const string Grunnadresse = "https://gbfs.urbansharing.com/oslobysykkel.no/";
    private static readonly TimeSpan Levetid = TimeSpan.FromSeconds(15);

    public string Id => "bysykkelstasjoner";
    public string Navn => "Bysykkelstasjoner";
    public string Beskrivelse => "Hver bysykkelstasjon er en ring som viser hvor full den er, fra tom til full.";
    public string Ikon => "🚲";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        if (mellomlager.TryGetValue(MellomlagerNøkkel, out Kartlag? lagret) && lagret is not null)
        {
            return lagret;
        }

        using var http = httpFactory.CreateClient(KlientNavn);
        http.Timeout = TimeSpan.FromSeconds(10);

        var informasjon = HentFil(http, "station_information.json", stopp);
        var status = HentFil(http, "station_status.json", stopp);
        var kartlag = SlåSammen(await informasjon, await status);

        mellomlager.Set(MellomlagerNøkkel, kartlag, Levetid);
        return kartlag;
    }

    private static async Task<JsonElement> HentFil(HttpClient http, string fil, CancellationToken stopp)
    {
        using var forespørsel = new HttpRequestMessage(HttpMethod.Get, Grunnadresse + fil);
        forespørsel.Headers.Add("Client-Identifier", KlientIdentifikator);

        using var svar = await http.SendAsync(forespørsel, stopp);
        svar.EnsureSuccessStatusCode();

        var tekst = await svar.Content.ReadAsStringAsync(stopp);
        return JsonSerializer.Deserialize<JsonElement>(tekst);
    }

    /// <summary>
    /// Slår sammen station_information.json og station_status.json på «station_id».
    /// Bare stasjoner som finnes i begge filene og er installert, blir punkter.
    /// </summary>
    public static Kartlag SlåSammen(JsonElement informasjon, JsonElement status)
    {
        var statusPerId = new Dictionary<string, JsonElement>();
        foreach (var rad in Stasjoner(status, "station_status.json"))
        {
            if (StasjonsId(rad) is { } id)
            {
                statusPerId[id] = rad;
            }
        }

        return Geo.Samle(Stasjoner(informasjon, "station_information.json")
            .Select(rad => StasjonsId(rad) is { } id && statusPerId.TryGetValue(id, out var s) ? TilPunkt(rad, s) : null));
    }

    private static IEnumerable<JsonElement> Stasjoner(JsonElement rot, string fil)
    {
        if (rot.ValueKind != JsonValueKind.Object
            || !rot.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("stations", out var stasjoner) || stasjoner.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Fant ikke «data.stations» i {fil}.");
        }

        return stasjoner.EnumerateArray();
    }

    private static string? StasjonsId(JsonElement rad) =>
        rad.ValueKind == JsonValueKind.Object && rad.TryGetProperty("station_id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    /// <summary>
    /// Ett kartpunkt av en stasjon fra hver fil. Null hvis stasjonen ikke er
    /// installert, eller mangler posisjon, navn eller antall.
    /// </summary>
    private static Kartpunkt? TilPunkt(JsonElement informasjon, JsonElement status)
    {
        if (!Installert(status)
            || !informasjon.TryGetProperty("lat", out var lat) || lat.ValueKind != JsonValueKind.Number
            || !informasjon.TryGetProperty("lon", out var lon) || lon.ValueKind != JsonValueKind.Number
            || !informasjon.TryGetProperty("name", out var navn) || navn.ValueKind != JsonValueKind.String
            || !Antall(status, "num_bikes_available", out var sykler)
            || !Antall(status, "num_docks_available", out var låser))
        {
            return null;
        }

        return Geo.Lag(
            id: StasjonsId(informasjon)!,
            lat: lat.GetDouble(),
            lon: lon.GetDouble(),
            navn: navn.GetString()!,
            kilde: Kilde,
            detaljer: new Dictionary<string, object?>
            {
                ["ledige sykler"] = sykler,
                ["ledige låser"] = låser,
                ["fylling"] = Fylling(sykler, låser),
            });
    }

    /// <summary>Sykler delt på sykler pluss låser, i hele prosent. En stasjon uten noen av delene regnes som tom.</summary>
    public static int Fylling(int sykler, int låser) =>
        sykler + låser <= 0 ? 0 : (int)Math.Round(100.0 * sykler / (sykler + låser), MidpointRounding.AwayFromZero);

    // Et antall er et ikke-negativt heltall. Alt annet gjør at stasjonen hoppes over.
    private static bool Antall(JsonElement status, string felt, out int antall)
    {
        antall = 0;
        return status.TryGetProperty(felt, out var verdi)
            && verdi.ValueKind == JsonValueKind.Number
            && verdi.TryGetInt32(out antall)
            && antall >= 0;
    }

    // GBFS 2.x bruker true/false, eldre versjoner 1/0. Mangler feltet, regnes stasjonen som installert.
    private static bool Installert(JsonElement status) =>
        !status.TryGetProperty("is_installed", out var felt) || felt.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.Number => felt.GetDouble() != 0,
            _ => true,
        };
}
