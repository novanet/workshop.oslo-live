using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Museer i Oslo fra Askeladden (Riksantikvarens kulturminnedatabase), med et
/// smakebit-objekt fra museets egen samling i DigitaltMuseum. Begge kildene
/// kommer fra Allemannsdata sin «kulturarv»-kilde.
///
/// Askeladden registrerer museumsbygninger, ikke museer: radene heter ting
/// som «Tilbygg. Nf 325» eller «Tø03 Botanisk museum». <see cref="KjenteMuseer"/>
/// gir derfor museets navn og samlingskode i DigitaltMuseum, mens Askeladden
/// gir koordinatene.
///
/// Hele laget, smakebitene inkludert, mellomlagres i <see cref="IMemoryCache"/>
/// og hentes bare på nytt når museumslisten er utløpt. Senere kall rører ikke
/// nettet og svarer derfor raskt.
/// </summary>
public sealed class MuseumLag(Allemannsdata data, IMemoryCache mellomlager) : ILag
{
    private const string LagNøkkel = "museum-lag";

    /// <summary>Museer flytter seg ikke, så et langt mellomlager gir raske svar etter det første kallet.</summary>
    private static readonly TimeSpan LagLevetid = TimeSpan.FromHours(6);

    /// <summary>Mellomlagring når museumslista er tom, slik at et forbigående tomt svar ikke blanker laget i 6 timer.</summary>
    private static readonly TimeSpan TomtLagLevetid = TimeSpan.FromMinutes(1);

    /// <summary>Tak per smakebitkall, slik at ett tregt museum ikke forsinker resten av laget.</summary>
    private static readonly TimeSpan EksempelFrist = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Tak på museumslistekallet, slik at første kall svarer innen 30 sekunder selv om
    /// kilden henger: 15 sekunder her pluss <see cref="EksempelFrist"/> for smakebitene,
    /// som hentes samtidig.
    /// </summary>
    private static readonly TimeSpan MuseumsFrist = TimeSpan.FromSeconds(15);

    /// <summary>Museets navn og samlingskoden i DigitaltMuseum (null når museet ikke har en samling der).</summary>
    public sealed record Museum(string Navn, string? Samling);

    /// <summary>
    /// Kjente museer i Oslo, etter Askeladden sitt <c>heritage_site_id</c>.
    /// Samlingskodene kommer fra <c>list_museums</c>; overordnede koder som
    /// «NMK» og «KHMUIO» har nesten ingen objekter selv, så vi bruker
    /// undersamlingene.
    /// </summary>
    public static readonly IReadOnlyDictionary<long, Museum> KjenteMuseer = new Dictionary<long, Museum>
    {
        [87641] = new("Nasjonalmuseet – Arkitektur", "NMK-A"),
        [117755] = new("Naturhistorisk museum", null),
        [132953] = new("Nasjonalgalleriet", "NMK-B"),
        [135892] = new("Kunstindustrimuseet", "NMK-D"),
        [137517] = new("Norsk Folkemuseum", "NF"),
        [163416] = new("Kulturhistorisk museum", "KHMUIO-A"),
        [164640] = new("Munchmuseet på Tøyen", null),
        [168378] = new("Norsk Maritimt Museum", "NSM"),
        [168511] = new("Kunstnernes Hus", null),
        [168599] = new("Frammuseet", null),
        [227994] = new("Emanuel Vigelands museum", null),
    };

    /// <summary>
    /// Øvre grense for antall kall mot Allemannsdata per oppdatering av laget:
    /// ett for museumslisten og ett smakebitkall per kjent museum med samling.
    /// Oppdateringen skjer bare når mellomlageret er utløpt, altså hver 6. time.
    /// </summary>
    public static int MaksKallPerOppdatering => 1 + KjenteMuseer.Values.Count(m => m.Samling is not null);

    public string Id => "museum";
    public string Navn => "Museer";
    public string Beskrivelse => "Museer i Oslo fra Askeladden, med et smakebit-objekt fra samlingen i DigitaltMuseum.";
    public string Ikon => "🏛️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        if (mellomlager.TryGetValue(LagNøkkel, out Kartlag? lagret) && lagret is not null)
        {
            return lagret;
        }

        var museer = await HentMuseer(stopp);
        var eksempler = await Task.WhenAll(museer.Select(m => HentEksempel(Oppslag(m).Samling, stopp)));

        var lag = Geo.Samle(museer.Select((m, i) => TilPunkt(m, eksempler[i])));
        mellomlager.Set(LagNøkkel, lag, lag.Features.Count > 0 ? LagLevetid : TomtLagLevetid);
        return lag;
    }

    /// <summary>
    /// Museumsbygninger i Askeladden. Ett museum kan ha flere fredete
    /// bygninger (samme <c>heritage_site_id</c>), så vi tar bare den første
    /// raden per museum, se <see cref="ÉnPerMuseum"/>.
    /// </summary>
    private async Task<IReadOnlyList<JsonElement>> HentMuseer(CancellationToken stopp)
    {
        using var frist = CancellationTokenSource.CreateLinkedTokenSource(stopp);
        frist.CancelAfter(MuseumsFrist);

        var rader = await data.HentListe(
            "kulturarv",
            "search_heritage_sites",
            new Dictionary<string, object>
            {
                ["kommune"] = "Oslo",
                ["art"] = "Museum-galleri",
                ["limit"] = 100,
            },
            liste: "sites",
            frist.Token);

        return ÉnPerMuseum(rader);
    }

    /// <summary>
    /// Slår sammen radene til Askeladden sitt <c>heritage_site_id</c>, slik
    /// at hvert museum blir ett punkt selv om det har flere fredete
    /// bygninger. Rader uten id blir forkastet.
    /// </summary>
    public static IReadOnlyList<JsonElement> ÉnPerMuseum(IReadOnlyList<JsonElement> rader) =>
        rader
            .Where(r => r.TryGetProperty("heritage_site_id", out var id) && id.ValueKind == JsonValueKind.Number)
            .GroupBy(r => r.GetProperty("heritage_site_id").GetInt64())
            .Select(gruppe => gruppe.First())
            .ToList();

    /// <summary>
    /// Museet en Askeladden-rad hører til: fra <see cref="KjenteMuseer"/> når
    /// vi kjenner det, ellers radens eget navn og ingen samling.
    /// </summary>
    public static Museum Oppslag(JsonElement rad)
    {
        if (rad.TryGetProperty("heritage_site_id", out var id)
            && id.ValueKind == JsonValueKind.Number
            && KjenteMuseer.TryGetValue(id.GetInt64(), out var kjent))
        {
            return kjent;
        }

        var navn = rad.TryGetProperty("navn", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? "Ukjent museum"
            : "Ukjent museum";
        return new Museum(navn, null);
    }

    /// <summary>
    /// Henter smakebit-objektet fra museets samling: tittelen på det første
    /// objektet DigitaltMuseum har med samlingskoden som eier. Museer uten
    /// samling gir ikke noe kall. Svikter kallet eller tar det for lang tid,
    /// vises museet uten smakebit til laget hentes på nytt.
    /// </summary>
    private async Task<string?> HentEksempel(string? samling, CancellationToken stopp)
    {
        if (samling is null)
        {
            return null;
        }

        using var frist = CancellationTokenSource.CreateLinkedTokenSource(stopp);
        frist.CancelAfter(EksempelFrist);

        try
        {
            var objekter = await data.HentListe(
                "kulturarv",
                "search_museum_objects",
                new Dictionary<string, object> { ["query"] = "*", ["owner"] = samling, ["limit"] = 1 },
                liste: "objects",
                frist.Token);

            return objekter.Count > 0 && objekter[0].TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Oversetter én museumsrad fra <c>search_heritage_sites</c> til et
    /// kartpunkt, med navnet fra <see cref="Oppslag"/>. Rader uten id eller
    /// koordinater gir null.
    /// </summary>
    public static Kartpunkt? TilPunkt(JsonElement museum, string? eksempel)
    {
        if (!museum.TryGetProperty("heritage_site_id", out var idFelt) || idFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (!museum.TryGetProperty("lon_lat", out var lonLat) || lonLat.ValueKind != JsonValueKind.Array || lonLat.GetArrayLength() < 2)
        {
            return null;
        }

        var lon = lonLat[0].GetDouble();
        var lat = lonLat[1].GetDouble();

        var detaljer = new Dictionary<string, object?>();
        if (eksempel is not null)
        {
            detaljer["eksempel"] = eksempel;
        }

        return Geo.Lag(
            id: idFelt.GetRawText(),
            lat: lat,
            lon: lon,
            navn: Oppslag(museum).Navn,
            kilde: "Askeladden og DigitaltMuseum (Riksantikvaren)",
            detaljer: detaljer);
    }
}
