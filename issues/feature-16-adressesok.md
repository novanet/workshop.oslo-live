Søk etter adresse og hopp dit på kartet
enhancement,P2,3 poeng

Vi vil kunne skrive «Karl Johans gate 1» i et søkefelt og få kartet til å zoome dit.

**Mål**

Den som leter etter et sted kan skrive adressen i panelet, velge et treff og få kartet flyttet dit med en markør på stedet.

**Akseptansekriterier**

- `GET /api/sok?q=Karl+Johans+gate+1` svarer 200 med en JSON-liste der hvert treff har `navn`, `lat` og `lon`. Første treff ligger i Oslo sentrum.
- `GET /api/sok` uten `q`, eller med tom `q`, svarer 400. Et søk uten treff svarer 200 med tom liste.
- Treffene er begrenset til kartutsnittet eller til Oslo, og listen har maks 10 treff.
- Panelet øverst til venstre har et søkefelt. Enter eller et klikk på søkeknappen viser treffene under feltet.
- Klikk på et treff flyr kartet dit og setter en midlertidig markør som forsvinner ved neste søk eller når brukeren lukker den.
- Endepunktet ligger i `Program.cs` ved siden av de andre, bruker `Allemannsdata` og har ingen tilstand. Ingen API-nøkkel, ingen endringer i `Kart/`.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker. En test i `tests/OsloLive.Tester` sjekker at `/api/sok` uten `q` gir 400.

**Datakilde**

Kartverkets adresse- og stedsnavnsøk ligger i Allemannsdata. Bruk MCP-serveren til å finne operasjonen.

Dette er ikke et kartlag, så `ILag` passer ikke. Legg et eget endepunkt i `Program.cs`.
