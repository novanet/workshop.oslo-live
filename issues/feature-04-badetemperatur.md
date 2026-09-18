Nytt lag: badetemperaturer
enhancement,P3,1 poeng

Hvor kaldt er det egentlig i fjorden? Vi vil ha badeplassene i og rundt Oslo på kartet med siste målte temperatur.

**Mål**

Den som lurer på om det er badetemperatur, kan åpne kartet, klikke på en badeplass og se siste målte temperatur og når den ble målt.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `badetemperatur`, `navn` lik «Badetemperatur», en beskrivelse på én setning og et ikon.
- `GET /api/lag/badetemperatur` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per badeplass. Hvert punkt har `navn` (badeplassens navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `temperatur` (tall i grader celsius) og `målt` (tidspunkt for målingen).
- Laget filtrerer ikke på Oslo selv. Punkter utenfor utsnittet siles bort av `Geo.Lag`, og ingen av dem dukker opp i svaret.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Målte badetemperaturer, levert av Yr. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon.

Kilden svarer med badeplasser i en vid omkrets. Kartet vårt siler selv bort alt utenfor Oslo, så du trenger ikke gjøre det i laget.
