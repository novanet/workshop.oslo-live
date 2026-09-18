Strukturert logging
enhancement,P3,2 poeng

Loggen er tekst i dag. Når kartet kjører i Azure vil vi kunne søke i den.

**Mål**

Den som feilsøker kartet i Azure kan filtrere loggen på kilde, operasjon og utfall, og finne treg kilde eller feilende lag uten å lese fritekst.

**Akseptansekriterier**

- Hvert kall i `Allemannsdata.Hent` gir én logglinje med navngitte felter i loggmalen: `Kilde`, `Operasjon`, `VarighetMs`, `Mellomlager` (true eller false) og `Utfall` (for eksempel `ok`, HTTP-status eller `tidsavbrudd`).
- Ingen logglinje inneholder hele URL-en med parametre. Søk etter `?` og `allemannsdata.com/wiki/api` i loggutskriften fra et kall gir ingen treff.
- Kall som feiler fordi kilden svarer 4xx eller 5xx eller bruker for lang tid logges på nivå Warning. Feil som skyldes koden vår (for eksempel manglende `data` i svaret eller en kastet `NullReferenceException`) logges på nivå Error.
- `/api/lag/{id}` logger et lag som feiler mot kilden på nivå Warning, ikke Error, med `Id` som felt.
- Konsolloggen skrives som JSON (innebygd `AddJsonConsole`) når appen ikke kjører i `Development`, slik at Azure kan indeksere feltene. Lokalt er loggen fortsatt lesbar tekst.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker (ingen Serilog).
