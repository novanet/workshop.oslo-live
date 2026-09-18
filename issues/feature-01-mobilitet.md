Nytt lag: elsparkesykler og bysykler
enhancement,P2,2 poeng

Vi vil se delt mobilitet i sanntid på kartet: elsparkesykler, bysykler og delebiler som står ledige i Oslo akkurat nå.

**Mål**

Den som åpner kartet ser hvor de ledige elsparkesyklene, bysyklene og delebilene står akkurat nå, og kan klikke på et punkt for å se hvem som eier det og hva slags kjøretøy det er.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `mobilitet`, `navn` lik «Delt mobilitet», en beskrivelse på én setning og et ikon.
- `GET /api/lag/mobilitet` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per ledig kjøretøy. Hvert punkt har `navn` og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `operatør` (for eksempel Ryde, Voi, Oslo Bysykkel) og `type` (elsparkesykkel, sykkel, bil).
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Entur samler delt mobilitet fra alle operatørene. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon og hvilke parametre den tar. Se `README.md` for hvordan du utforsker en kilde.

Følg mønsteret i `Lag/LuftkvalitetLag.cs`, og husk å registrere laget i `Program.cs`.
