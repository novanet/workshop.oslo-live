Avgangstavle for Gardermoen
enhancement,P3,3 poeng

Vi vil se hvilke fly som går fra og lander på Gardermoen den nærmeste timen, uten å forlate kartet.

**Mål**

Den som ser på kartet kan åpne en tavle og se hvilke fly som går fra og lander på Gardermoen den neste timen, med lesbare navn på selskap og steder, og se med ett blikk hvilke som er forsinket.

**Akseptansekriterier**

- `GET /api/avganger` svarer 200 med JSON som har `avganger` og `ankomster`. Hvert element har `flynummer`, `flyselskap` (fullt navn, ikke bare kode), `sted` (fullt navn på flyplassen, ikke bare kode), `planlagt` (ISO 8601), `status` (tekst) og `forsinket` (true eller false).
- Listene dekker de neste 60 minuttene fra nå, sortert på `planlagt`, med maks 30 elementer i hver.
- Kodene for flyselskap og flyplass slås opp mot Avinor-kilden i Allemannsdata. `DY` vises som «Norwegian», `OSL` som «Oslo lufthavn» eller tilsvarende.
- Feiler kilden, svarer `/api/avganger` 502 med `{ "feil": "..." }`. Resten av API-et svarer 200 uansett.
- En knapp «Avganger» åpner og lukker et panel med to tabeller (avganger og ankomster) med kolonnene flynummer, flyselskap, sted, planlagt tid og status. Valget huskes i `localStorage`.
- Forsinkede fly er merket med rød tekst eller et eget merke, og statusteksten sier «forsinket».
- Panelet oppdateres hvert 60. sekund mens det er åpent. Feiler kilden, viser panelet «utilgjengelig».
- Endepunktet ligger i `Program.cs`, bruker `Allemannsdata` og har ingen tilstand. Ingen API-nøkkel. `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Avinor ligger i Allemannsdata og dekker alle norske lufthavner. Bruk `describe_source` for å finne operasjonen, og `get_data` for å se feltene før du skriver koden.

Dette er ikke et kartlag, det er en tavle. `ILag` passer dårlig; legg et eget endepunkt i `Program.cs`, som i issuen om adressesøk.

Merk at flykoder er koder. `OSL` er Gardermoen og `DY` er Norwegian. Skal tavlen være til å lese, må du slå dem opp, og Avinor-kilden kan hjelpe med begge deler.
