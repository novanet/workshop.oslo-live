Tegn et område og tell hva som er inni
enhancement,P3,5 poeng

Vi vil kunne tegne et polygon på kartet og få vite hvor mange punkter fra hvert lag som ligger innenfor.

**Mål**

Den som lurer på hva som finnes i et bestemt område kan tegne det opp på kartet og få antall punkter per lag innenfor, uten å telle selv.

**Akseptansekriterier**

- En knapp «Tegn område» i panelet eller ved kameraknappene starter tegnemodus. Knappen viser at modusen er på (`aria-pressed="true"`).
- I tegnemodus setter hvert klikk et hjørne, og polygonet tegnes fortløpende. Dobbeltklikk lukker polygonet og avslutter modusen. Escape avbryter uten å lage et område.
- Når et område er lukket, viser panelet antall punkter innenfor per lag som er på, med lagets navn og tall, og en sum.
- Tallene oppdateres når lagene hentes på nytt hvert 15. sekund, så lenge området står.
- En knapp «Fjern område» fjerner polygonet og tallene.
- Punkt-i-polygon ligger som en funksjon i `Kart/` med en enhetstest i `tests/OsloLive.Tester` som dekker et punkt innenfor, et utenfor og et punkt på kanten. Frontenden kan bruke samme algoritme i JavaScript, men testen ligger i `tests/`.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker, ingen npm eller byggesteg.

Punkt-i-polygon er en klassiker. Skriv testen for den funksjonen, den hører hjemme i `tests/`, ikke i nettleseren.
