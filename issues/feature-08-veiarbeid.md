Nytt lag: veiarbeid og stengte veier
enhancement,P3,2 poeng

Statens vegvesen melder fra om veiarbeid, stengte veier og hendelser. Vi vil se dem på kartet så vi vet hvor det er kø.

**Mål**

Den som ser på kartet ser hvor det er veiarbeid og stengte veier i Oslo akkurat nå, hva som skjer der, og hvor lenge det varer.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `veiarbeid`, `navn` lik «Veiarbeid», en beskrivelse på én setning og et ikon.
- `GET /api/lag/veiarbeid` svarer 200 med en `FeatureCollection`. Når kilden har meldinger med koordinat i Oslo, er de med som punkter.
- Ett punkt per melding som har koordinat. Meldinger uten koordinat hoppes over uten at laget feiler.
- Hvert punkt har `navn` (kort tittel eller veinavn) og `kilde`. Popup-en viser feltene `type`, `beskrivelse`, `alvorlighet` og `varer til`.
- `alvorlighet` bygger på kildens `severity`, og punkter med høy alvorlighet skiller seg fra lav i popup-en eller ikonet.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Vegvesenets meldingsfeed (DATEX II). Bruk Allemannsdata-MCP-serveren til å finne operasjonen.

Meldingene har et `severity`-felt. Bruk det til noe: for eksempel ulikt ikon på lav og høy alvorlighet.
