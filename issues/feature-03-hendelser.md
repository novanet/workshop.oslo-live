Nytt lag: politilogg og nyhetshendelser
enhancement,P2,2 poeng

Vi vil se hva som skjer i byen akkurat nå: meldinger fra politiloggen og geolokaliserte nyhetshendelser.

**Mål**

Den som ser på kartet ser de ferskeste hendelsene i Oslo der de skjedde, og kan lese hva det gjelder uten å forlate kartet.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `hendelser`, `navn` lik «Hendelser», en beskrivelse på én setning og et ikon.
- `GET /api/lag/hendelser` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per hendelse som har koordinat. Hendelser uten koordinat hoppes over uten at laget feiler.
- Hvert punkt har `navn` (overskriften) og `kilde`. Popup-en viser feltene `sammendrag` og `meldt` (tidspunkt).
- Kilden bes om de nyeste hendelsene først, slik at punktene på kartet er fra siste døgn når kilden har nok.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Kilden med geolokaliserte norske nyheter og politiloggmeldinger. Bruk Allemannsdata-MCP-serveren til å finne den.

Hendelser er ferskvare. Be om de nyeste først.
