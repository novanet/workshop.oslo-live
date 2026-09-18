Tell punkter per bydel
enhancement,P3,5 poeng

Vi vil vite hvor i byen det skjer noe. Et panel som viser antall punkter per bydel, for det laget som er valgt.

**Mål**

Den som ser på kartet kan velge et lag og se hvilke bydeler som har flest punkter akkurat nå, uten å telle selv.

**Akseptansekriterier**

- `GET /api/lag/luftkvalitet/bydeler` svarer 200 med en JSON-liste der hvert element har `bydel` (navn) og `antall` (heltall). Summen av `antall` er lik antall punkter som fikk en bydel.
- Punkter som ikke treffer noen bydel telles under `bydel` lik «Utenfor bydelene» eller utelates. PR-en sier hvilket.
- `GET /api/lag/finnes-ikke/bydeler` svarer 404. Feiler laget, svarer endepunktet 502, som `/api/lag/{id}`.
- Panelet viser de fem bydelene med flest punkter for laget brukeren har valgt, med navn og antall, sortert synkende.
- Tallene oppdateres i samme takt som kartet, hvert 15. sekund.
- Bydelsoppslaget gjør ikke ett nettverkskall per punkt per oppdatering. Antall kall per oppdatering står i PR-en.
- Endepunktet ligger i `Program.cs`, bruker `Allemannsdata` for eksterne kall og har ingen tilstand. Ingen API-nøkkel. En test i `tests/OsloLive.Tester` dekker tellingen uten å gå mot nettet.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Bydelsgrensene i Oslo finnes som åpne data. Alternativt kan du bruke et omvendt oppslag på koordinaten, Kartverket har en operasjon for det i Allemannsdata. Velg selv, men skriv i PR-en hvorfor.
