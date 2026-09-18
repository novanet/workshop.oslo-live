Mål hva som skjer
enhancement,P3,3 poeng

Vi vil vite hvor mange kall vi gjør, hvor ofte mellomlageret treffer, og hvor lang tid kildene bruker.

**Mål**

Den som lurer på hvorfor kartet er tregt kan slå opp ett endepunkt og se hvilken kilde som er treg, hvor ofte den kalles og hvor ofte mellomlageret redder oss.

**Akseptansekriterier**

- `GET /api/metrikker` svarer 200 med JSON som har `siden` (ISO 8601 for når tellingen startet) og en liste `kilder` med ett element per kilde som er kalt, med feltene `kilde`, `kall`, `treff`, `bom`, `snittMs` og `feil`.
- `kall` teller alle oppslag i `Allemannsdata.Hent`, `treff` de som ble besvart fra mellomlageret, `bom` de som gikk til kilden. `kall` er lik `treff` pluss `bom`.
- `snittMs` er gjennomsnittlig tid for kallene som gikk til kilden. `feil` teller kall som kastet eller fikk feilstatus.
- To kall til `/api/lag/luftkvalitet` innen 30 sekunder gir `kall` 2, `bom` 1 og `treff` 1 for kilden `luftkvalitet`.
- Tallene nullstilles ikke ved oppslag. To kall til `/api/metrikker` etter hverandre gir samme eller høyere tall.
- Tellingen er trådsikker og ligger i `Allemannsdata` eller en liten klasse ved siden av, ikke i lagene.
- En test i `tests/OsloLive.Tester` sjekker tellingen med en falsk `HttpMessageHandler`, uten nettverk.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Dette er grunnlaget for å svare på «hvorfor er kartet tregt».
