using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>Ferske hendelser i Oslo: politiloggmeldinger og geolokaliserte nyheter.</summary>
public sealed class HendelserLag(Allemannsdata data) : ILag
{
    public string Id => "hendelser";
    public string Navn => "Hendelser";
    public string Beskrivelse => "Ferske meldinger fra politiloggen og nyheter der de skjedde.";
    public string Ikon => "🚨";

    /// <summary>
    /// Starten på tidsvinduet: ett døgn før <paramref name="nå"/>, avrundet ned til hele
    /// minutter slik at adressen (og dermed mellomlagernøkkelen i <see cref="Allemannsdata"/>)
    /// er stabil innenfor et minutt.
    /// </summary>
    public static DateTimeOffset Fra(DateTimeOffset nå)
    {
        var fra = nå.ToUniversalTime().AddHours(-24);
        return new DateTimeOffset(fra.Year, fra.Month, fra.Day, fra.Hour, fra.Minute, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Kilde: nyheter, operasjon: search_news. Operasjonen har ingen sortering, så vi ber
    /// eksplisitt om «since» i stedet for å stole på kildens udokumenterte standardverdi.
    /// </summary>
    public static Dictionary<string, object> Parametre(DateTimeOffset nå) => new()
    {
        ["lat"] = Geo.OsloLat,
        ["lon"] = Geo.OsloLon,
        ["radius_km"] = 20,
        ["limit"] = 100,
        ["since"] = Fra(nå).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
    };

    /// <summary>Tidspunktet hendelsen ble meldt, eller null om «pub_date» mangler eller ikke lar seg tolke.</summary>
    private static DateTimeOffset? Meldt(JsonElement rad) =>
        rad.TryGetProperty("pub_date", out var t) && t.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var tid)
            ? tid
            : null;

    /// <summary>
    /// Kilden sorterer etter «last_seen», ikke publiseringstidspunkt, så vi sorterer selv etter
    /// «pub_date» synkende (nyeste først) og forkaster hendelser eldre enn <paramref name="fra"/>.
    /// Hendelser uten et tolkbart «pub_date» beholdes, men havner sist.
    /// </summary>
    public static IEnumerable<JsonElement> Ferskeste(IEnumerable<JsonElement> rader, DateTimeOffset fra) =>
        rader
            .Where(rad => Meldt(rad) is not { } meldt || meldt >= fra)
            .OrderByDescending(rad => Meldt(rad) ?? DateTimeOffset.MinValue);

    /// <summary>Oversetter én hendelse fra kilden til et kartpunkt. Null når hendelsen mangler koordinat.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var lat) || lat.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lon) || lon.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var overskrift = rad.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "Ukjent hendelse";
        var id = rad.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString()! : overskrift;
        var kilde = rad.TryGetProperty("source", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString()! : "Politiloggen og nyheter";

        return Geo.Lag(
            id: id,
            lat: lat.GetDouble(),
            lon: lon.GetDouble(),
            navn: overskrift,
            kilde: kilde,
            detaljer: new Dictionary<string, object?>
            {
                ["sammendrag"] = rad.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null,
                ["meldt"] = rad.TryGetProperty("pub_date", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
            });
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var nå = DateTimeOffset.UtcNow;
        var rader = await data.HentListe("nyheter", "search_news", Parametre(nå), liste: "items", stopp);
        return Geo.Samle(Ferskeste(rader, Fra(nå)).Select(TilPunkt));
    }
}
