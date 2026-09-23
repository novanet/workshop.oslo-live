using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Restauranter, kafeer, gatekjøkken, barer og puber i Oslo sentrum, fra
/// OpenStreetMap. Kilden gir alle slags punkter av interesse (helt ned til
/// parkbenker og søppelkasser), så vi henter ett kall per kategori og
/// filtrerer bort alt som ikke er et spisested.
/// </summary>
public sealed class SpisestederLag(Allemannsdata data) : ILag
{
    public string Id => "spisesteder";
    public string Navn => "Spisesteder";
    public string Beskrivelse => "Restauranter, kafeer og barer i Oslo sentrum fra OpenStreetMap.";
    public string Ikon => "🍽️";

    /// <summary>Radius rundt Oslo sentrum vi henter spisesteder innenfor.</summary>
    public const double RadiusKm = 2;

    /// <summary>Tak per kategori, slik at kilden ikke kutter svaret over 64 KB.</summary>
    public const int MaksPerKategori = 100;

    /// <summary>
    /// Kilden tar bare én kategori per kall (ingen kommaseparert liste),
    /// så vi henter én kategori per <c>search_poi</c>-kall.
    /// </summary>
    private static readonly string[] Kategorier = ["restaurant", "cafe", "fast_food", "bar", "pub"];

    /// <summary>Oversetter kildens OSM-kategori til teksten popupen skal vise.</summary>
    public static string Kategori(string? kategori) => kategori switch
    {
        "restaurant" => "restaurant",
        "cafe" => "kafé",
        "fast_food" => "gatekjøkken",
        "bar" => "bar",
        "pub" => "pub",
        _ => kategori ?? "ukjent",
    };

    private static bool ErSpisested(string? kategori) =>
        kategori is not null && Kategorier.Contains(kategori);

    /// <summary>Ett sted blir ett punkt. Steder som ikke er spisesteder gir ikke punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        var kategoriRaw = rad.TryGetProperty("category", out var k) ? k.GetString() : null;
        if (!ErSpisested(kategoriRaw))
        {
            return null;
        }

        var id = rad.GetProperty("id").GetInt64().ToString();
        var lat = rad.GetProperty("lat").GetDouble();
        var lon = rad.GetProperty("lon").GetDouble();
        var navn = rad.TryGetProperty("name", out var n) ? n.GetString() : null;

        return Geo.Lag(
            id: id,
            lat: lat,
            lon: lon,
            navn: navn ?? "Ukjent spisested",
            kilde: "OpenStreetMap",
            detaljer: new Dictionary<string, object?> { ["kategori"] = Kategori(kategoriRaw) });
    }

    public static Kartlag Samle(IEnumerable<JsonElement> rader) =>
        Geo.Samle(rader.Select(TilPunkt));

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var svar = await Task.WhenAll(Kategorier.Select(kategori => data.HentListe(
            "poi_norge",
            "search_poi",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["category"] = kategori,
                ["limit"] = MaksPerKategori,
            },
            liste: "items",
            stopp)));

        return Samle(svar.SelectMany(r => r));
    }
}
