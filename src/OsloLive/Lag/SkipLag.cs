using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Lag;

/// <summary>
/// Skipstrafikk i indre Oslofjord: ferger, lasteskip og fritidsbåter, med
/// posisjon fra AIS via BarentsWatch. Kilden gir fart allerede i knop.
/// </summary>
public sealed class SkipLag(Allemannsdata data) : ILag
{
    public string Id => "skip";
    public string Navn => "Skipstrafikk";
    public string Beskrivelse => "Ferger, lasteskip og fritidsbåter i indre Oslofjord, med posisjon fra AIS.";
    public string Ikon => "🚢";

    /// <summary>Radius rundt Oslo sentrum som dekker fjorden ned til Nesodden.</summary>
    public const double RadiusKm = 20;

    /// <summary>Tak på antall fartøy, romslig nok til å ikke kutte Nesoddbåtene.</summary>
    public const int MaksFartøy = 200;

    /// <summary>Ett fartøy med kjent posisjon blir ett punkt. Ukjent posisjon gir ikke punkt.</summary>
    public static Kartpunkt? TilPunkt(JsonElement rad)
    {
        if (!rad.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number
            || !rad.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var id = rad.TryGetProperty("vessel_id", out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64().ToString()
            : null;

        var navn = rad.TryGetProperty("navn", out var n) && n.ValueKind == JsonValueKind.String
            ? LesbartNavn(n.GetString()!.Trim())
            : "";
        if (navn.Length == 0)
        {
            navn = "Ukjent fartøy";
        }

        var detaljer = new Dictionary<string, object?>();

        if (rad.TryGetProperty("fart_knop", out var fart) && fart.ValueKind == JsonValueKind.Number)
        {
            detaljer["fart"] = FormaterFart(fart.GetDouble());
        }

        if (rad.TryGetProperty("destinasjon", out var d) && d.ValueKind == JsonValueKind.String)
        {
            var destinasjon = d.GetString()!.Trim();
            if (ErEkteDestinasjon(destinasjon))
            {
                detaljer["destinasjon"] = destinasjon;
            }
        }

        return Geo.Lag(
            id: id ?? navn,
            lat: latEl.GetDouble(),
            lon: lonEl.GetDouble(),
            navn: navn,
            kilde: "BarentsWatch AIS",
            detaljer: detaljer);
    }

    /// <summary>Tekst i popupen formateres alltid på norsk, uavhengig av trådens kultur.</summary>
    private static readonly CultureInfo Norsk = CultureInfo.GetCultureInfo("nb-NO");

    /// <summary>
    /// AIS-destinasjonen er fritekst fra mannskapet, og disse verdiene betyr at den mangler.
    /// Sammenlignes uten mellomrom rundt og uavhengig av store og små bokstaver.
    /// </summary>
    private static readonly string[] TommeDestinasjoner = ["N/A", "NA", "NONE", "-", ".", "UNKNOWN"];

    /// <summary>Tom tekst og verdiene i <see cref="TommeDestinasjoner"/> er ikke ekte destinasjoner og utelates.</summary>
    private static bool ErEkteDestinasjon(string destinasjon)
    {
        var verdi = destinasjon.Trim();
        return verdi.Length > 0 && !TommeDestinasjoner.Contains(verdi, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fart som tekst med én desimal, norsk komma og enhet, for eksempel «12,3 knop».
    /// Er farten 0 etter avrunding, står det «ligger stille».
    /// </summary>
    private static string FormaterFart(double knop)
    {
        var avrundet = Math.Round(knop, 1);
        return avrundet == 0 ? "ligger stille" : $"{avrundet.ToString("0.0", Norsk)} knop";
    }

    /// <summary>
    /// AIS sender navn i versaler. Navn i bare versaler får stor forbokstav per ord
    /// («VISION OF THE FJORDS» blir «Vision Of The Fjords»). Navn med små bokstaver røres ikke.
    /// </summary>
    private static string LesbartNavn(string navn)
    {
        var bareVersaler = navn.Any(char.IsLetter) && !navn.Any(char.IsLower);
        return bareVersaler ? Norsk.TextInfo.ToTitleCase(navn.ToLower(Norsk)) : navn;
    }

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe(
            "ais",
            "find_vessels_nearby",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["radius_km"] = RadiusKm,
                ["limit"] = MaksFartøy,
            },
            liste: "fartoy",
            stopp);

        return Geo.Samle(rader.Select(TilPunkt));
    }
}
