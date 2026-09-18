Vis været over kartet
enhancement,P3,3 poeng

Temperatur, vind og nedbør for Oslo, øverst i panelet.

**Mål**

Den som ser på kartet ser med ett blikk hva slags vær det er i Oslo nå: temperatur, vind og om det regner.

**Akseptansekriterier**

- `GET /api/vaer` svarer 200 med JSON som har `temperatur` (grader celsius), `vind` (m/s), `vindretning` (grader eller himmelretning), `nedboer` (mm siste time eller neste time) og `symbol` (værtype fra kilden).
- Feiler kilden, svarer `/api/vaer` 502 med `{ "feil": "..." }`. Resten av API-et svarer 200 uansett.
- Panelet viser temperatur, vindstyrke og -retning, om det regner, og et ikon som passer til `symbol`, øverst i panelet.
- Frontenden henter `/api/vaer` hvert 15. minutt, ikke hvert 15. sekund. To kall til `/api/vaer` med under 15 minutter mellom gir ett kall mot MET, synlig i loggen.
- Levetiden på 30 sekunder for de andre lagene er uendret. `Allemannsdata.Levetid` er fortsatt 30 sekunder.
- PR-en sier hvordan den lengre levetiden for været er løst uten å endre mellomlageret for alle.
- Endepunktet ligger i `Program.cs`, bruker `Allemannsdata` og har ingen tilstand utover mellomlagring. Ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

MET Norge gjennom værkilden i Allemannsdata.

Merk kravet om ulik oppdateringstakt. Mellomlageret i `Allemannsdata` har én fast levetid for alle kall. Løs det uten å ødelegge for de andre lagene, og forklar valget i PR-en.
