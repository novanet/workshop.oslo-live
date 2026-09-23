using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Verifiserte artsobservasjoner i Oslo, hentet fra Artskart hos Artsdatabanken.
///
/// Kilde: artskart, operasjon: find_observations. «data.observations» er
/// listen med observasjoner.
///
/// Geografisk filter: kommunenummer. Det offisielle firesifrede
/// kommunenummeret for Oslo, «0301», gir null treff hos kilden. «301» uten
/// ledende null gir observasjoner med county/municipality lik Oslo og
/// koordinater i Oslo-området. Funnet ved å prøve begge koder mot kilden og
/// sammenligne antall treff.
///
/// Datofilter: «from_date» settes til 30 dager tilbake i tid, slik at laget
/// alltid viser ferske observasjoner uten å måtte søke etter art først.
/// Observasjonene har allerede norsk og vitenskapelig artsnavn, så det
/// trengs ikke noe eget artssøk. Ett kall per oppdatering.
/// </summary>
public sealed class ArterLag(Allemannsdata data) : ILag
{
    private const string OsloKommunenummer = "301";
    private const int Antall = 100;

    public string Id => "arter";
    public string Navn => "Artsobservasjoner";
    public string Beskrivelse => "Verifiserte observasjoner av arter i Oslo fra Artskart.";
    public string Ikon => "🦋";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var fraDato = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");

        var rader = await data.HentListe(
            "artskart",
            "find_observations",
            new Dictionary<string, object>
            {
                ["municipality"] = OsloKommunenummer,
                ["from_date"] = fraDato,
                ["limit"] = Antall,
                ["page_size"] = Antall,
            },
            liste: "observations",
            stopp);

        return Geo.Samle(rader.Select(TilPunkt));
    }

    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("latitude", out var latEl) || !latEl.TryGetDouble(out var lat) ||
            !rad.TryGetProperty("longitude", out var lonEl) || !lonEl.TryGetDouble(out var lon))
        {
            return null;
        }

        var norsk = rad.TryGetProperty("name", out var n) ? n.GetString() : null;
        var vitenskapelig = rad.TryGetProperty("scientific_name", out var v) ? v.GetString() : null;
        var art = !string.IsNullOrWhiteSpace(norsk) ? norsk : vitenskapelig ?? "Ukjent art";

        return Geo.Lag(
            id: rad.TryGetProperty("obs_url", out var url) ? url.GetString() ?? art : art,
            lat: lat,
            lon: lon,
            navn: art,
            kilde: "Artsdatabanken (Artskart)",
            detaljer: new Dictionary<string, object?>
            {
                ["art"] = art,
                ["dato"] = rad.TryGetProperty("collected_date", out var d) ? d.GetString() : null,
                ["observatør"] = rad.TryGetProperty("collector", out var c) ? c.GetString() : null,
            });
    }
}
