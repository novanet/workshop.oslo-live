Nytt lag: kollektivtrafikk som beveger seg
enhancement,P2,3 poeng

Dette er laget vi vil ha på storskjermen. Busser, trikker, T-bane og tog i Oslo rapporterer posisjonen sin i sanntid. Vi vil se dem kjøre.

**Mål**

Den som ser på storskjermen ser bussene, trikkene og togene i Oslo flytte seg langs rutene sine, og kan se hvilken linje et kjøretøy er og hvor det skal.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `kollektiv`, `navn` lik «Kollektiv», en beskrivelse på én setning og et ikon.
- `GET /api/lag/kollektiv` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per kjøretøy som rapporterer posisjon nå. Hvert punkt har `navn` (for eksempel linjenummer og destinasjon) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `linje`, `type` (buss, trikk, T-bane eller tog) og `destinasjon`.
- To kall til `/api/lag/kollektiv` med mer enn 30 sekunder mellom gir ulike koordinater for kjøretøy som er i bevegelse. Punktene flytter seg på kartet.
- Buss og trikk har ulikt ikon i popup-en eller i `detaljer` hvis du får det til. Er det ikke mulig med dagens frontend, skriv i PR-en hvorfor.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Entur samler sanntidsposisjoner for hele landet. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon.

Kartet henter lagene på nytt hvert 15. sekund, så punktene flytter seg av seg selv når laget virker.
