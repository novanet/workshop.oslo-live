Ta vare på et øyeblikksbilde hver time
enhancement,P3,5 poeng

Alt vi viser er ferskvare, og forsvinner. Vi vil kunne se tilbake: hvor mange elsparkesykler sto i sentrum klokka åtte i morges?

**Mål**

Den som ser på kartet kan se hvordan antall punkter i et lag har utviklet seg gjennom siste døgn, fordi appen tar vare på et bilde av hvert lag én gang i timen.

**Akseptansekriterier**

- En bakgrunnsjobb lagrer én `FeatureCollection` per registrert lag én gang i timen, på disk under en mappe som kan settes i `appsettings.json`.
- `GET /api/lag/luftkvalitet/historikk` svarer 200 med en liste der hvert element har `tidspunkt` (ISO 8601) og `antall` (heltall), begrenset til siste 24 timer, eldste først.
- `GET /api/lag/finnes-ikke/historikk` svarer 404. Et lag uten lagrede bilder ennå svarer 200 med tom liste.
- Panelet viser en liten graf per lag med antall per time, og grafen oppdateres når nye bilder kommer.
- Bilder eldre enn 7 dager slettes fra disk, slik at mappa ikke vokser uten grense.
- Bakgrunnsjobben går ikke i testene. `dotnet test` skriver ikke filer utenfor testens egen midlertidige mappe. En test i `tests/OsloLive.Tester` dekker lagring og lesing uten nettverk.
- PR-en sier hva som skjer med historikken når containeren startes på nytt.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker uten at PR-en begrunner det.

Hold lagringen enkel, filer på disk holder. Tenk på at jobben kjører i en container som kan bli slått av; skriv i PR-en hva det betyr for løsningen.
