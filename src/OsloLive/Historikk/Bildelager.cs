using System.Globalization;
using System.Text.Json;
using OsloLive.Kart;

namespace OsloLive.Historikk;

/// <summary>
/// Lagrer og henter øyeblikksbilder av kartlagene på disk, slik at tidslinjen
/// kan vise hvordan kartet så ut tidligere. Se <see cref="Øyeblikksjobb"/> for
/// jobben som tar bildene.
/// </summary>
public sealed class Bildelager(string mappe)
{
    /// <summary>Hvor langt unna et lagret bilde kan være fra et etterspurt tidspunkt og fortsatt telle som «nærmest».</summary>
    public static readonly TimeSpan MaksAvstand = TimeSpan.FromHours(2);

    /// <summary>Hvor lenge bilder blir liggende før de slettes.</summary>
    public static readonly TimeSpan Oppbevaring = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Valg = new(JsonSerializerDefaults.Web);

    private const string Tidsformat = "yyyyMMdd'T'HHmmss'Z'";

    private static string FilNavn(DateTimeOffset tidspunkt) =>
        tidspunkt.UtcDateTime.ToString(Tidsformat, CultureInfo.InvariantCulture) + ".json";

    private static bool TolkTidspunkt(string filnavn, out DateTimeOffset tidspunkt) =>
        DateTimeOffset.TryParseExact(
            Path.GetFileNameWithoutExtension(filnavn),
            Tidsformat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out tidspunkt);

    // lagId kommer bare fra ILag.Id på registrerte lag, aldri direkte fra spørringen,
    // så det er ikke mulig å bryte ut av mappa med stiforflytning.
    private string LagMappe(string lagId) => Path.Combine(mappe, lagId);

    /// <summary>
    /// Velger tidspunktet i <paramref name="tidspunkter"/> som ligger nærmest
    /// <paramref name="tid"/>, innenfor <paramref name="maksAvstand"/>. Ved lik
    /// avstand velges det eldste, slik at valget er deterministisk. Returnerer
    /// <c>null</c> hvis ingen er nære nok.
    /// </summary>
    public static DateTimeOffset? VelgNærmeste(IEnumerable<DateTimeOffset> tidspunkter, DateTimeOffset tid, TimeSpan maksAvstand) =>
        tidspunkter
            .Select(t => (Tidspunkt: t, Avstand: (t - tid).Duration()))
            .Where(p => p.Avstand <= maksAvstand)
            .OrderBy(p => p.Avstand)
            .ThenBy(p => p.Tidspunkt)
            .Select(p => (DateTimeOffset?)p.Tidspunkt)
            .FirstOrDefault();

    /// <summary>Alle tidspunkt med lagrede bilder for et lag, eldste først.</summary>
    public IReadOnlyList<DateTimeOffset> Tidspunkter(string lagId)
    {
        var sti = LagMappe(lagId);
        if (!Directory.Exists(sti))
        {
            return [];
        }

        return Directory.EnumerateFiles(sti, "*.json")
            .Select(f => (Fil: f, Ok: TolkTidspunkt(f, out var t), Tid: t))
            .Where(p => p.Ok)
            .Select(p => p.Tid)
            .OrderBy(t => t)
            .ToList();
    }

    /// <summary>Tar vare på et bilde av laget for et gitt tidspunkt.</summary>
    public async Task Lagre(string lagId, Kartlag lag, DateTimeOffset tidspunkt, CancellationToken stopp = default)
    {
        var sti = LagMappe(lagId);
        Directory.CreateDirectory(sti);

        var fil = Path.Combine(sti, FilNavn(tidspunkt));
        var midlertidig = fil + ".tmp";

        await using (var strøm = File.Create(midlertidig))
        {
            await JsonSerializer.SerializeAsync(strøm, lag, Valg, stopp);
        }

        // Skriv til en midlertidig fil og flytt den på plass, slik at en avbrutt
        // container-nedstenging aldri etterlater en halvskrevet .json-fil.
        File.Move(midlertidig, fil, overwrite: true);
    }

    /// <summary>Henter bildet lagret nærmest <paramref name="tid"/>, eller <c>null</c> hvis ingen er innenfor <see cref="MaksAvstand"/>.</summary>
    public async Task<Kartlag?> HentNærmest(string lagId, DateTimeOffset tid, CancellationToken stopp = default)
    {
        var valgt = VelgNærmeste(Tidspunkter(lagId), tid, MaksAvstand);
        if (valgt is null)
        {
            return null;
        }

        var fil = Path.Combine(LagMappe(lagId), FilNavn(valgt.Value));
        await using var strøm = File.OpenRead(fil);
        return await JsonSerializer.DeserializeAsync<Kartlag>(strøm, Valg, stopp);
    }

    /// <summary>Sletter bilder eldre enn <paramref name="grense"/>. Returnerer antallet slettet.</summary>
    public int SlettEldreEnn(DateTimeOffset grense)
    {
        if (!Directory.Exists(mappe))
        {
            return 0;
        }

        var antall = 0;
        foreach (var lagMappe in Directory.EnumerateDirectories(mappe))
        {
            foreach (var fil in Directory.EnumerateFiles(lagMappe, "*.json"))
            {
                if (TolkTidspunkt(fil, out var tid) && tid < grense)
                {
                    File.Delete(fil);
                    antall++;
                }
            }
        }

        return antall;
    }
}
