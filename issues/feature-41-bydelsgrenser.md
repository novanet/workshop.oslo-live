Tegn bydelsgrensene
enhancement,P3,5 poeng

Vi vil se hvor bydelene går, som tynne svarte streker over kartet.

**Mål**

Den som ser på kartet ser hvor bydelsgrensene i Oslo går og hva bydelene heter, og kan slå grensene av og på som et annet lag.

**Akseptansekriterier**

- `GET /api/bydelsgrenser` svarer 200 med en `FeatureCollection` der hver `feature` er en `Polygon` eller `MultiPolygon` med `properties.navn` lik bydelens navn. Alle 15 bydeler i Oslo er med.
- Grensene tegnes på kartet som linjer (MapLibre-lag av typen `line`) med svart farge og linjebredde 1 til 2 px, uten fyll.
- Bydelens navn vises som tekst på kartet ved zoomnivå 12 eller nærmere, og skjules ved lavere zoom.
- Lagvelgeren har en rad «Bydelsgrenser» med avkrysningsboks som slår grensene og navnene av og på. Raden viser ikke en teller, siden det ikke er punkter.
- `GET /api/lag` og `GET /api/lag/{id}` er uendret. Bydelsgrensene går ikke gjennom `ILag` og `Geo.Lag`, og de andre lagene virker som før.
- Grensene hentes én gang ved oppstart av siden, ikke hvert 15. sekund. Svaret fra endepunktet mellomlagres på serveren i minst 1 time.
- PR-en sier hvordan laget er løst utenfor kartkontrakten uten å endre den for de andre lagene.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Oslo kommune har bydelsgrensene som åpne data. Kartkontrakten vår tar bare punkter, så dette laget bryter med den, og det er en del av oppgaven. Skriv i PR-en hvordan du løste det uten å ødelegge for de andre lagene.
