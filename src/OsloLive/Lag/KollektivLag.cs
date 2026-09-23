using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Busser, trikker, T-baner og tog i Oslo, med sanntidsposisjon fra Entur.
///
/// Kilde: entur, operasjon: find_live_vehicles_nearby. «data» er et objekt
/// med listen «vehicles» inni, så vi henter via <see cref="Allemannsdata.HentListe"/>
/// med sidevis <c>offset</c>. Gatewayen har et tak på 64 KB per svar, så vi
/// henter 50 rader om gangen og stopper når en side er mindre enn full, eller
/// etter <see cref="MaksSider"/> sider. <see cref="Geo.Samle"/> fjerner
/// duplikater dersom samme kjøretøy skulle dukke opp på to sider.
/// </summary>
public sealed class KollektivLag(Allemannsdata data) : ILag
{
    public string Id => "kollektiv";
    public string Navn => "Kollektiv";
    public string Beskrivelse => "Busser, trikker, T-baner og tog i Oslo der de er akkurat nå.";
    public string Ikon => "🚌";

    private const string Kilde = "Entur sanntidsposisjoner";

    /// <summary>Rader per side. Holder svaret godt under gatewayens tak på 64 KB.</summary>
    public const int SideStørrelse = 50;

    /// <summary>Maks antall sider per oppdatering, altså opptil 200 kjøretøy.</summary>
    public const int MaksSider = 4;

    /// <summary>Radiusen dekker Oslo-boksen bortsett fra de ytterste hjørnene; <see cref="Geo.Lag"/> silrer resten.</summary>
    private const double RadiusKm = 20;

    /// <summary>Hvor gammel en posisjon kan være og fortsatt telle som «rapporterer nå».</summary>
    public static readonly TimeSpan MaksAlder = TimeSpan.FromMinutes(5);

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var nå = DateTimeOffset.UtcNow;
        var punkter = new List<Kartpunkt?>();

        for (var side = 0; side < MaksSider; side++)
        {
            var rader = await data.HentListe(
                "entur",
                "find_live_vehicles_nearby",
                new Dictionary<string, object>
                {
                    ["lat"] = Geo.OsloLat,
                    ["lon"] = Geo.OsloLon,
                    ["radius_km"] = RadiusKm,
                    ["limit"] = SideStørrelse,
                    ["offset"] = side * SideStørrelse,
                },
                liste: "vehicles",
                stopp);

            punkter.AddRange(rader.Select(rad => TilPunkt(rad, nå)));

            if (rader.Count < SideStørrelse)
            {
                break;
            }
        }

        return Geo.Samle(punkter);
    }

    /// <summary>Norsk type for et Entur-«mode», eller null når det ikke er buss, trikk, T-bane eller tog.</summary>
    public static string? Type(string? mode) => mode switch
    {
        "BUS" or "COACH" => "buss",
        "TRAM" => "trikk",
        "METRO" => "T-bane",
        "RAIL" => "tog",
        _ => null,
    };

    public static string IkonFor(string type) => type switch
    {
        "buss" => "🚌",
        "trikk" => "🚊",
        "T-bane" => "🚇",
        "tog" => "🚆",
        _ => "🚌",
    };

    private static string? Tekst(JsonElement rad, string felt) =>
        rad.TryGetProperty(felt, out var verdi) && verdi.ValueKind == JsonValueKind.String && verdi.GetString() is { Length: > 0 } tekst
            ? tekst
            : null;

    public static Kartpunkt? TilPunkt(JsonElement rad, DateTimeOffset nå)
    {
        if (rad.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var kjøretøyId = Tekst(rad, "vehicle_id");
        if (kjøretøyId is null)
        {
            return null;
        }

        if (!rad.TryGetProperty("lat", out var latFelt) || latFelt.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonFelt) || lonFelt.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var type = Type(Tekst(rad, "mode"));
        if (type is null)
        {
            return null;
        }

        if (rad.TryGetProperty("last_updated", out var sistOppdatertFelt)
            && sistOppdatertFelt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(sistOppdatertFelt.GetString(), out var sistOppdatert)
            && nå - sistOppdatert > MaksAlder)
        {
            return null;
        }

        var linje = Tekst(rad, "line") ?? Tekst(rad, "line_name") ?? "ukjent";
        var destinasjon = Tekst(rad, "destination") ?? Tekst(rad, "line_name") ?? "ukjent";

        var navn = destinasjon == linje || destinasjon == "ukjent"
            ? linje
            : $"{linje} {destinasjon}";

        var codespace = Tekst(rad, "codespace");
        var id = codespace is not null ? $"{codespace}:{kjøretøyId}" : kjøretøyId;

        var detaljer = new Dictionary<string, object?>
        {
            ["linje"] = linje,
            ["type"] = type,
            ["destinasjon"] = destinasjon,
            ["ikon"] = IkonFor(type),
        };

        return Geo.Lag(
            id: id,
            lat: latFelt.GetDouble(),
            lon: lonFelt.GetDouble(),
            navn: navn,
            kilde: Kilde,
            detaljer: detaljer);
    }
}
