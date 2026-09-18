Nytt lag: sykkelparkering
enhancement,P3,1 poeng

Hvor kan man sette fra seg sykkelen? Vi vil ha sykkelparkeringene i sentrum på kartet.

**Mål**

Den som sykler til byen kan se på kartet hvor det går an å sette fra seg sykkelen, og om det er plass og tak.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `sykkelparkering`, `navn` lik «Sykkelparkering», en beskrivelse på én setning og et ikon.
- `GET /api/lag/sykkelparkering` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per parkering. Hvert punkt har `navn` og `kilde`, og koordinatene ligger som `[lon, lat]`. Mangler kilden navn, brukes «Sykkelparkering» som `navn`.
- Popup-en viser feltene `plasser` (antall) og `under tak` (ja eller nei) når kilden oppgir dem. Mangler verdien, utelates feltet.
- Kategorinavnet som brukes mot kilden er hentet fra `list_categories`, og står i PR-en.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

POI-kilden fra OpenStreetMap. Bruk `list_categories` for å finne hva kategorien faktisk heter før du gjetter.
