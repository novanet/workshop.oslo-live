using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>Badeplasser i og rundt Oslo, med siste målte badetemperatur fra Yr.</summary>
public sealed class BadetemperaturLag(Allemannsdata data) : ILag
{
    public string Id => "badetemperatur";
    public string Navn => "Badetemperatur";
    public string Beskrivelse => "Målte badetemperaturer i og rundt Oslo.";
    public string Ikon => "🌡️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: badetemp, operasjon: get_nearest_water_temperatures.
        // «data» er et objekt med listen inni feltet «temperatures».
        var rader = await data.HentListe(
            "badetemp",
            "get_nearest_water_temperatures",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["limit"] = 50,
            },
            liste: "temperatures",
            stopp);

        return Geo.Samle(rader.Select(TilPunkt));
    }

    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        var navn = rad.TryGetProperty("name", out var n) ? n.GetString() ?? "Ukjent badeplass" : "Ukjent badeplass";

        return Geo.Lag(
            id: rad.GetProperty("location_id").GetString() ?? navn,
            lat: rad.GetProperty("lat").GetDouble(),
            lon: rad.GetProperty("lon").GetDouble(),
            navn: navn,
            kilde: "Badetemperaturer fra Yr",
            detaljer: new Dictionary<string, object?>
            {
                ["temperatur"] = rad.TryGetProperty("temperature_c", out var t) && t.ValueKind == JsonValueKind.Number
                    ? t.GetDouble()
                    : null,
                ["målt"] = rad.TryGetProperty("time", out var tid) ? tid.GetString() : null,
            });
    }
}
