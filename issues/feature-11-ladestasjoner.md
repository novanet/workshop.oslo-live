Nytt lag: ladestasjoner for elbil
enhancement,P3,3 poeng

Vi vil se de offentlige ladestasjonene i Oslo.

**Mål**

Den som ser på kartet finner de offentlige ladestasjonene i Oslo. Er kilden nede, ser den som ser på kartet det på akkurat dette laget, mens resten av kartet virker som før.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `ladestasjoner`, `navn` lik «Ladestasjoner», en beskrivelse på én setning og et ikon.
- `GET /api/lag/ladestasjoner` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per ladestasjon. Hvert punkt har `navn` (stasjonens navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltet `ladepunkter` (antall) når kilden oppgir det. Mangler tallet, utelates feltet.
- Når kilden svarer 502, svarer `/api/lag/ladestasjoner` enten 502 med `{ "feil": "..." }` eller 200 med en tom `FeatureCollection`. `/api/lag` og de andre lagene svarer 200 uansett. PR-en sier hvilket av de to du valgte og hvorfor.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

NOBIL fra Enova. Bruk Allemannsdata-MCP-serveren.

**NB:** denne kilden svarer av og til med 502. Det er en del av oppgaven: laget skal ikke ta ned kartet når kilden er nede. Et lag som feiler skal gi tomt resultat eller en tydelig feil, ikke velte resten av siden. Se hvordan `/api/lag/{id}` håndterer det i dag, og vurder om laget selv bør ta ansvaret.
