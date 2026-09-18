En helsesjekk som betyr noe
enhancement,P3,3 poeng

`/api/helse` svarer «ok» selv om alle kildene er nede.

**Mål**

Den som drifter kartet kan se på ett endepunkt om tjenesten faktisk virker, kilde for kilde, mens Azure fortsatt får et lynraskt svar på om prosessen lever.

**Akseptansekriterier**

- `GET /api/helse` svarer fortsatt 200 med `{ "status": "ok", "tid": ... }` og gjør ingen kall til kildene. Svaret kommer innen 100 ms.
- `GET /api/helse/kilder` svarer 200 med `status` («ok» eller «degradert») og en liste `kilder` med ett element per kilde lagene bruker, med `kilde`, `status` («ok», «feil» eller «ukjent»), `sistSjekket` (ISO 8601 eller `null`) og `varighetMs`.
- `/api/helse/kilder` svarer innen 1 sekund uansett hvor trege kildene er. Den returnerer siste kjente resultat fra en bakgrunnssjekk eller mellomlageret, og venter ikke på kildene i selve forespørselen.
- Bakgrunnssjekken går hvert 60. sekund. Intervallet kan settes i `appsettings.json`.
- `status` er «degradert» når minst én kilde er «feil». HTTP-koden er 200 også da, siden Azure bruker `/api/helse`.
- Før første bakgrunnssjekk er ferdig, har alle kilder `status` lik «ukjent», og endepunktet svarer 200.
- En test i `tests/OsloLive.Tester` sjekker at `/api/helse/kilder` svarer 200 med riktig form uten nettverk.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Forskjellen på «lever prosessen» og «virker tjenesten» er hele poenget her.
