Nytt lag: artsobservasjoner
enhancement,P3,5 poeng

Artsdatabanken har verifiserte observasjoner av arter over hele landet. Vi vil se hva folk har observert i Oslo.

**Mål**

Den som ser på kartet ser hvilke arter som er observert i Oslo, hvor, når og av hvem.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `arter`, `navn` lik «Artsobservasjoner», en beskrivelse på én setning og et ikon.
- `GET /api/lag/arter` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per observasjon. Hvert punkt har `navn` (artens navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `art` (norsk navn, vitenskapelig navn hvis norsk mangler), `dato` og `observatør`.
- Filterkodene som brukes mot kilden er gyldige. PR-en sier hvilke som ble brukt og hvordan de ble funnet.
- Antall kall per oppdatering er begrenset. Bruker laget både artssøk og observasjonssøk, står antall kall i PR-en.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Artskart hos Artsdatabanken. Bruk Allemannsdata-MCP-serveren.

Kilden har både et artssøk og et observasjonssøk. Du trenger antagelig begge, og du må finne ut hvilke filterkoder som er gyldige.
