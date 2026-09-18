Nytt lag: museer og samlinger
enhancement,P3,3 poeng

DigitaltMuseum har over ti millioner objekter fra norske museer. Vi vil se museene i Oslo på kartet, med et smakebit-objekt i popup-en.

**Mål**

Den som ser på kartet finner museene i Oslo, og får en smakebit fra samlingen når de klikker på et museum.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `museum`, `navn` lik «Museer», en beskrivelse på én setning og et ikon.
- `GET /api/lag/museum` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per museum. Hvert punkt har `navn` (museets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltet `eksempel` med tittelen på ett objekt fra museets samling. Svikter objektkallet for ett museum, vises museet likevel, uten `eksempel`.
- Antall kall per oppdatering er begrenset og står i PR-en. `/api/lag/museum` svarer innen 30 sekunder første gang og innen 5 sekunder etterpå.
- Laget bruker `Allemannsdata` for begge kallene, `Geo.Lag` og `Geo.Samle`, og registreres med én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Kulturarvkilden i Allemannsdata dekker både Askeladden og DigitaltMuseum. Bruk `describe_source`.

Dette er et lag som trenger to kall: ett for museene, ett for objektene. Tenk på mellomlageret, ett kall per museum per oppdatering blir mange.
