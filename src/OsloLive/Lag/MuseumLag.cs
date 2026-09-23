using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Museer i Oslo fra Askeladden (Riksantikvarens kulturminnedatabase), med et
/// smakebit-objekt fra museets samling i DigitaltMuseum. Begge kildene
/// kommer fra Allemannsdata sin «kulturarv»-kilde.
///
/// Museumslisten har egen mellomlagring i <see cref="IMemoryCache"/> utover
/// <see cref="Allemannsdata"/> sitt 30-sekunders mellomlager, fordi
/// smakebit-kallet er ett kall per museum: uten et lengre mellomlager ville
/// hvert oppslag i lagvelgeren (hvert 15. sekund) gitt et helt nytt sett med
/// museumskall.
/// </summary>
public sealed class MuseumLag(Allemannsdata data, IMemoryCache mellomlager) : ILag
{
    private const string MuseumsNøkkel = "museum-museer";
    private const string EksempelNøkkelPrefiks = "museum-eksempel-";

    /// <summary>Museer flytter seg ikke, så et langt mellomlager gir raske svar etter det første kallet.</summary>
    private static readonly TimeSpan MuseumsLevetid = TimeSpan.FromHours(6);

    /// <summary>Mellomlagring når museumslista er tom, slik at et forbigående tomt svar ikke blanker laget i 6 timer.</summary>
    private static readonly TimeSpan TomMuseumslisteLevetid = TimeSpan.FromMinutes(1);

    /// <summary>Smakebiten for et museum endrer seg sjelden; mellomlagres lenge når den lykkes.</summary>
    private static readonly TimeSpan EksempelLevetid = TimeSpan.FromHours(24);

    /// <summary>
    /// Kortere mellomlagring når smakebitkallet feiler eller ikke gir treff, slik at
    /// vi prøver på nytt en gang i blant i stedet for å gi opp for et helt døgn.
    /// </summary>
    private static readonly TimeSpan EksempelFeilLevetid = TimeSpan.FromMinutes(10);

    /// <summary>Tak per smakebitkall, slik at ett tregt museum ikke forsinker resten av laget.</summary>
    private static readonly TimeSpan EksempelFrist = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Tak på museumslistekallet, slik at første kall svarer innen 30 sekunder selv om
    /// kilden henger: <see cref="Allemannsdata"/> sin HttpClient har 30 sekunders tidsavbrudd
    /// pluss inntil to nye forsøk.
    /// </summary>
    private static readonly TimeSpan MuseumsFrist = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Antall museer vi henter et smakebit-objekt for per oppdatering.
    /// Smakebiten tar museumsnavnet som søketekst, ikke en museumsliste, så
    /// ett kall per museum er nødvendig; vi setter et tak for ikke å belaste
    /// kilden for mye selv om museumslisten i Oslo skulle vokse. Museer som
    /// ikke rekker en tur denne runden, blir ikke mellomlagret uten smakebit,
    /// så de får en ny sjanse neste oppdatering: over noen runder får alle
    /// museene etter hvert en smakebit.
    /// </summary>
    public const int MaksEksempelkall = 20;

    public string Id => "museum";
    public string Navn => "Museer";
    public string Beskrivelse => "Museer i Oslo fra Askeladden, med et smakebit-objekt fra DigitaltMuseum.";
    public string Ikon => "🏛️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var museer = await HentMuseer(stopp);

        var eksempler = new Dictionary<long, string?>();
        var uhentede = new List<JsonElement>();
        foreach (var museum in museer)
        {
            var id = museum.GetProperty("heritage_site_id").GetInt64();
            if (mellomlager.TryGetValue(EksempelNøkkelPrefiks + id, out string? lagretEksempel))
            {
                eksempler[id] = lagretEksempel;
            }
            else
            {
                uhentede.Add(museum);
            }
        }

        var denneRunden = uhentede.Take(MaksEksempelkall).ToList();
        var resultater = await Task.WhenAll(denneRunden.Select(m => HentEksempel(m, stopp)));
        for (var i = 0; i < denneRunden.Count; i++)
        {
            eksempler[denneRunden[i].GetProperty("heritage_site_id").GetInt64()] = resultater[i];
        }

        var punkter = museer.Select(m => TilPunkt(m, eksempler.GetValueOrDefault(m.GetProperty("heritage_site_id").GetInt64())));
        return Geo.Samle(punkter);
    }

    /// <summary>
    /// Museumsbygninger i Askeladden. Ett museum kan ha flere fredete
    /// bygninger (samme <c>heritage_site_id</c>), så vi tar bare den første
    /// raden per museum, se <see cref="ÉnPerMuseum"/>.
    /// </summary>
    private async Task<IReadOnlyList<JsonElement>> HentMuseer(CancellationToken stopp)
    {
        if (mellomlager.TryGetValue(MuseumsNøkkel, out IReadOnlyList<JsonElement>? lagret) && lagret is not null)
        {
            return lagret;
        }

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

        var museer = ÉnPerMuseum(rader);
        mellomlager.Set(MuseumsNøkkel, museer, museer.Count > 0 ? MuseumsLevetid : TomMuseumslisteLevetid);
        return museer;
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
    /// Henter smakebit-objektet for ett museum: tittelen på det første
    /// treffet i DigitaltMuseum for museumsnavnet. Svikter kallet eller tar
    /// det for lang tid, gir vi opp smakebiten for dette museet uten å felle
    /// hele laget.
    /// </summary>
    private async Task<string?> HentEksempel(JsonElement museum, CancellationToken stopp)
    {
        var id = museum.GetProperty("heritage_site_id").GetInt64();
        var nøkkel = EksempelNøkkelPrefiks + id;

        if (mellomlager.TryGetValue(nøkkel, out string? lagretEksempel))
        {
            return lagretEksempel;
        }

        using var frist = CancellationTokenSource.CreateLinkedTokenSource(stopp);
        frist.CancelAfter(EksempelFrist);

        string? eksempel;
        try
        {
            var navn = museum.TryGetProperty("navn", out var n) ? n.GetString() ?? "" : "";
            var objekter = await data.HentListe(
                "kulturarv",
                "search_museum_objects",
                new Dictionary<string, object> { ["query"] = navn, ["limit"] = 1 },
                liste: "objects",
                frist.Token);

            eksempel = objekter.Count > 0 && objekter[0].TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
        }
        catch (OperationCanceledException) when (stopp.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            eksempel = null;
        }

        mellomlager.Set(nøkkel, eksempel, eksempel is null ? EksempelFeilLevetid : EksempelLevetid);
        return eksempel;
    }

    /// <summary>
    /// Oversetter én museumsrad fra <c>search_heritage_sites</c> til et
    /// kartpunkt. Rader uten id eller koordinater gir null.
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

        var navn = museum.TryGetProperty("navn", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? "Ukjent museum"
            : "Ukjent museum";

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
            navn: navn,
            kilde: "Askeladden og DigitaltMuseum (Riksantikvaren)",
            detaljer: detaljer);
    }
}
