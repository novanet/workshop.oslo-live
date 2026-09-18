Nytt lag: kulturminner
enhancement,P3,3 poeng

Riksantikvaren har rundt 630 000 kulturminner i Askeladden. Vi vil se dem som ligger i Oslo.

**Mål**

Den som ser på kartet finner kulturminnene i Oslo, og ser hva de er, hvor gamle de er og hvilket vern de har.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `kulturminner`, `navn` lik «Kulturminner», en beskrivelse på én setning og et ikon.
- `GET /api/lag/kulturminner` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per kulturminne. Hvert punkt har `navn` (kulturminnets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `datering` og `vernestatus`.
- Kallet mot kilden bruker gyldige parametre fra `describe_operation`. `/api/lag/kulturminner` svarer ikke 502 på grunn av 400 fra kilden.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Askeladden hos Riksantikvaren. Bruk Allemannsdata-MCP-serveren.

Operasjonen er kresen på parametre og svarer med 400 hvis du gjetter feil. Bruk `describe_operation` før du skriver koden.
