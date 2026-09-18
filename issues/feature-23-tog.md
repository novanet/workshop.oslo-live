Nytt lag: togtrafikk
enhancement,P3,3 poeng

Vi har busser og trikker. Nå vil vi ha togene også, både persontog og godstog gjennom Oslo.

**Mål**

Den som ser på kartet ser togene som er i Oslo akkurat nå, med tognummer og hvor de kommer fra og skal.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `tog`, `navn` lik «Tog», en beskrivelse på én setning og et ikon.
- `GET /api/lag/tog` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer og det går tog.
- Ett punkt per tog med kjent posisjon eller siste kjente stasjon. Hvert punkt har `navn` (tognummer) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `tognummer`, `fra` og `til`. Er posisjonen en stasjon og ikke en GPS-posisjon, viser popup-en `stasjon`.
- Overlapper laget med kollektivlaget, sier PR-en hvordan de skilles, og det samme toget vises ikke to ganger med samme ikon.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Bane NOR har en åpen SIRI-feed i Allemannsdata. Bruk `describe_source` og `get_data` for å se hva den faktisk gir deg.

Merk at kollektivlaget kan ha togene allerede, avhengig av hvordan det er skrevet. Sjekk før du dublerer, og hvis de overlapper, skriv i PR-en hvordan du skiller dem.
