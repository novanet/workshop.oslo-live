using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Målestasjonene for luftkvalitet i Oslo, med siste målte nivå.
///
/// Dette er eksempellaget. Bruk det som mal når du legger til nye lag:
///
///   1. Finn kilden og operasjonen med Allemannsdata-MCP-serveren.
///   2. Kall den med <see cref="Allemannsdata.HentListe"/>.
///   3. Lag ett <see cref="Geo.Lag"/>-punkt per rad.
///   4. Returner <see cref="Geo.Samle"/>.
/// </summary>
public sealed class LuftkvalitetLag(Allemannsdata data) : ILag
{
    public string Id => "luftkvalitet";
    public string Navn => "Luftkvalitet";
    public string Beskrivelse => "Målestasjoner med siste målte luftkvalitet.";
    public string Ikon => "🌬️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        // Kilde: luftkvalitet, operasjon: get_air_quality_nearby.
        // «data» er her en liste rett ut, så vi trenger ikke oppgi listenavn.
        var rader = await data.HentListe(
            "luftkvalitet",
            "get_air_quality_nearby",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["limit"] = 50,
            },
            liste: null,
            stopp);

        var punkter = rader.Select(rad =>
        {
            var navn = rad.GetProperty("station").GetString() ?? "Ukjent stasjon";
            var nivå = rad.TryGetProperty("aqi_level", out var n) ? n.GetString() : null;

            return Geo.Lag(
                id: rad.TryGetProperty("eoi", out var eoi) ? eoi.GetString() ?? navn : navn,
                lat: rad.GetProperty("latitude").GetDouble(),
                lon: rad.GetProperty("longitude").GetDouble(),
                navn: navn,
                kilde: "Luftkvalitet i Norge",
                detaljer: new Dictionary<string, object?>
                {
                    ["nivå"] = nivå ?? "ukjent",
                    ["målt"] = rad.TryGetProperty("aqi_time", out var t) ? t.GetString() : null,
                });
        });

        return Geo.Samle(punkter);
    }
}
