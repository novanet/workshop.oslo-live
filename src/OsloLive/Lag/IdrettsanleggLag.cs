using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Idretts- og friluftsanlegg i Oslo fra Anleggsregisteret (Kultur- og
/// likestillingsdepartementet), med anleggstype og driftsstatus.
///
/// Kilden filtrerer på Oslo kommune selv («municipality»), så vi henter ikke
/// hele landet. Operasjonen gir maks 500 anlegg per kall, og Oslo har over
/// 2000, så <see cref="Hent"/> henter flere sider til en side kommer tilbake
/// mindre enn full.
/// </summary>
public sealed class IdrettsanleggLag(Allemannsdata data) : ILag
{
    public string Id => "idrettsanlegg";
    public string Navn => "Idrettsanlegg";
    public string Beskrivelse => "Idretts- og friluftsanlegg i Oslo med anleggstype og status fra Anleggsregisteret.";
    public string Ikon => "🏟️";

    private const string Kilde = "anleggsregisteret";
    private const string Operasjon = "find_facilities";
    private const string Liste = "facilities";
    private const string KildeNavn = "Anleggsregisteret";

    /// <summary>Maks rader per side. Operasjonen tillater opptil 500.</summary>
    private const int SideStørrelse = 500;

    /// <summary>Øvre grense for antall sider, som ekstra sikring mot en uendelig løkke.</summary>
    private const int MaksSider = 10;

    private static readonly Dictionary<string, string> Statustekster = new()
    {
        ["EXISTING"] = "Eksisterende",
        ["PLANNED"] = "Planlagt",
        ["CLOSED_DOWN"] = "Nedlagt",
        ["UNREALIZED"] = "Ble ikke realisert",
    };

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var alleRader = new List<JsonElement>();

        for (var side = 0; side < MaksSider; side++)
        {
            var rader = await data.HentListe(
                Kilde,
                Operasjon,
                new Dictionary<string, object>
                {
                    ["municipality"] = "Oslo",
                    ["limit"] = SideStørrelse,
                    ["offset"] = side * SideStørrelse,
                },
                liste: Liste,
                stopp);

            alleRader.AddRange(rader);

            if (rader.Count < SideStørrelse)
            {
                break;
            }
        }

        return Bygg(alleRader);
    }

    /// <summary>Bygger laget fra rader. Nettverksfri, så den kan testes.</summary>
    public static Kartlag Bygg(IEnumerable<JsonElement> rader) => Geo.Samle(rader.Select(TilPunkt));

    /// <summary>Ett kartpunkt per anlegg, eller null utenfor kartutsnittet.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        var navn = rad.TryGetProperty("name", out var n) ? n.GetString() ?? "Ukjent anlegg" : "Ukjent anlegg";
        var statuskode = rad.TryGetProperty("status", out var s) ? s.GetString() : null;

        return Geo.Lag(
            id: rad.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture),
            lat: rad.GetProperty("lat").GetDouble(),
            lon: rad.GetProperty("lon").GetDouble(),
            navn: navn,
            kilde: KildeNavn,
            detaljer: new Dictionary<string, object?>
            {
                ["type"] = rad.TryGetProperty("facility_type", out var t) ? t.GetString() : null,
                ["status"] = statuskode is null ? null : Statustekster.GetValueOrDefault(statuskode, statuskode),
            });
    }
}
