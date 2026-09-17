namespace OsloLive.Kart;

/// <summary>
/// Ett lag på kartet. Legg til et nytt lag ved å lage en klasse som
/// implementerer denne og registrere den i Program.cs.
/// </summary>
public interface ILag
{
    /// <summary>Kortnavn brukt i adressen: /api/lag/{Id}. Bare små bokstaver.</summary>
    string Id { get; }

    /// <summary>Navnet som vises i lagvelgeren på kartet.</summary>
    string Navn { get; }

    /// <summary>Én setning om hva laget viser.</summary>
    string Beskrivelse { get; }

    /// <summary>Emojien som markerer punktene på kartet.</summary>
    string Ikon { get; }

    /// <summary>Henter punktene som skal tegnes akkurat nå.</summary>
    Task<Kartlag> Hent(CancellationToken stopp = default);
}
