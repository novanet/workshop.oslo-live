using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Sykkelparkeringer i Oslo sentrum, fra OpenStreetMap.
/// </summary>
public sealed class SykkelparkeringLag(Allemannsdata data) : ILag
{
    public string Id => "sykkelparkering";
    public string Navn => "Sykkelparkering";
    public string Beskrivelse => "Sykkelparkeringer i Oslo sentrum fra OpenStreetMap.";
    public string Ikon => "🚲";

    /// <summary>Radius rundt Oslo sentrum vi henter sykkelparkeringer innenfor.</summary>
    public const double RadiusKm = 2;

    /// <summary>Tak, slik at kilden ikke kutter svaret over 64 KB.</summary>
    public const int Maks = 200;

    /// <summary>Kategorinavnet slik det står i kildens <c>list_categories</c>.</summary>
    public const string Kategori = "bicycle_parking";

    public const string Standardnavn = "Sykkelparkering";

    /// <summary>Ett sted blir ett punkt. Andre kategorier enn sykkelparkering gir ikke punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        var kategoriRaw = rad.TryGetProperty("category", out var k) ? k.GetString() : null;
        if (kategoriRaw != Kategori)
        {
            return null;
        }

        var id = rad.GetProperty("id").GetInt64().ToString();
        var lat = rad.GetProperty("lat").GetDouble();
        var lon = rad.GetProperty("lon").GetDouble();
        var navn = rad.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()
            : null;

        // search_poi gir verken antall plasser eller om det er tak over; popupen viser derfor ingen av delene.
        return Geo.Lag(
            id: id,
            lat: lat,
            lon: lon,
            navn: string.IsNullOrWhiteSpace(navn) ? Standardnavn : navn,
            kilde: "OpenStreetMap");
    }

    public static Kartlag Samle(IEnumerable<JsonElement> rader) =>
        Geo.Samle(rader.Select(TilPunkt));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe(
            "poi_norge",
            "search_poi",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["category"] = Kategori,
                ["limit"] = Maks,
            },
            liste: "items",
            stopp);

        return Samle(rader);
    }
}
