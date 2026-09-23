using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Historikk;

/// <summary>Ett øyeblikksbilde av et lag: hvor mange punkter det hadde på et gitt tidspunkt.</summary>
public sealed record Bilde(DateTimeOffset Tidspunkt, int Antall);

/// <summary>
/// Lagrer og leser øyeblikksbilder av lag som filer på disk, ett bilde per fil,
/// gruppert i én mappe per lag-id. Tidspunktet ligger i filnavnet, slik at
/// <see cref="Les"/> og <see cref="Rydd"/> kan filtrere uten å åpne filene.
/// </summary>
public sealed class Bildelager(string mappe)
{
    /// <summary>Uten kolon, slik at filnavnet er gyldig på alle filsystem.</summary>
    public const string Filformat = "yyyyMMdd'T'HHmmss'Z'";

    public static readonly TimeSpan Vindu = TimeSpan.FromHours(24);
    public static readonly TimeSpan Levetid = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Mappe => mappe;

    /// <summary>
    /// En relativ mappe (eller ingen) havner under <see cref="Path.GetTempPath"/>,
    /// siden containeren kjører som ikke-root og ikke kan skrive under WORKDIR.
    /// En absolutt sti (f.eks. en montert disk) brukes som den er.
    /// </summary>
    public static string FinnMappe(string? oppsatt) =>
        !string.IsNullOrWhiteSpace(oppsatt) && Path.IsPathRooted(oppsatt)
            ? oppsatt
            : Path.Combine(Path.GetTempPath(), string.IsNullOrWhiteSpace(oppsatt) ? "oslolive-historikk" : oppsatt);

    /// <summary>
    /// Lagrer et bilde av laget. Skriver først til en midlertidig fil og
    /// flytter den på plass, slik at en container som blir avsluttet midt i
    /// skrivingen aldri etterlater en halvskrevet .json-fil.
    /// </summary>
    public void Lagre(string lagId, Kartlag lag, DateTimeOffset tidspunkt)
    {
        var lagMappe = Path.Combine(mappe, lagId);
        Directory.CreateDirectory(lagMappe);

        var navn = tidspunkt.ToUniversalTime().ToString(Filformat, CultureInfo.InvariantCulture);
        var fil = Path.Combine(lagMappe, navn + ".json");
        var midlertidig = fil + ".tmp";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(lag, Json));
        File.Move(midlertidig, fil, overwrite: true);
    }

    /// <summary>Bildene for et lag de siste <see cref="Vindu"/>, eldste først. Tom liste hvis mappa ikke finnes.</summary>
    public IReadOnlyList<Bilde> Les(string lagId, DateTimeOffset nå)
    {
        var lagMappe = Path.Combine(mappe, lagId);
        if (!Directory.Exists(lagMappe))
        {
            return [];
        }

        var bilder = new List<Bilde>();
        foreach (var fil in Directory.EnumerateFiles(lagMappe, "*.json"))
        {
            if (!TryLesTidspunkt(fil, out var tidspunkt) || tidspunkt < nå - Vindu || tidspunkt > nå)
            {
                continue;
            }

            if (TryTellPunkter(fil, out var antall))
            {
                bilder.Add(new Bilde(tidspunkt, antall));
            }
        }

        return bilder.OrderBy(b => b.Tidspunkt).ToList();
    }

    /// <summary>Tidspunktet til det nyeste lesbare bildet for laget, eller null hvis laget ikke har noen ennå.</summary>
    public DateTimeOffset? Siste(string lagId)
    {
        var lagMappe = Path.Combine(mappe, lagId);
        if (!Directory.Exists(lagMappe))
        {
            return null;
        }

        DateTimeOffset? siste = null;
        foreach (var fil in Directory.EnumerateFiles(lagMappe, "*.json"))
        {
            // Innholdet må være lesbart, ellers ville en ødelagt fil få jobben til å
            // vente på neste time mens Les fortsatt gir tom historikk.
            if (TryLesTidspunkt(fil, out var tidspunkt) && (siste is null || tidspunkt > siste) && TryTellPunkter(fil, out _))
            {
                siste = tidspunkt;
            }
        }

        return siste;
    }

    /// <summary>Sletter bilder eldre enn <see cref="Levetid"/> og løse midlertidige filer, slik at mappa ikke vokser uten grense.</summary>
    public void Rydd(DateTimeOffset nå)
    {
        if (!Directory.Exists(mappe))
        {
            return;
        }

        foreach (var lagMappe in Directory.EnumerateDirectories(mappe))
        {
            foreach (var fil in Directory.EnumerateFiles(lagMappe, "*.tmp"))
            {
                File.Delete(fil);
            }

            foreach (var fil in Directory.EnumerateFiles(lagMappe, "*.json"))
            {
                if (TryLesTidspunkt(fil, out var tidspunkt) && tidspunkt < nå - Levetid)
                {
                    File.Delete(fil);
                }
            }
        }
    }

    private static bool TryLesTidspunkt(string fil, out DateTimeOffset tidspunkt)
    {
        var navn = Path.GetFileNameWithoutExtension(fil);
        return DateTimeOffset.TryParseExact(
            navn,
            Filformat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out tidspunkt);
    }

    private static bool TryTellPunkter(string fil, out int antall)
    {
        try
        {
            using var dokument = JsonDocument.Parse(File.ReadAllText(fil));
            antall = dokument.RootElement.GetProperty("features").GetArrayLength();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or KeyNotFoundException or InvalidOperationException)
        {
            antall = 0;
            return false;
        }
    }
}
