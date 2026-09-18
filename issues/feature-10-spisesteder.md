Nytt lag: spisesteder
enhancement,P3,2 poeng

Vi vil ha restauranter og kafeer i Oslo sentrum på kartet.

**Mål**

Den som ser på kartet finner restauranter og kafeer i sentrum, og ser hva slags sted det er før de går dit.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `spisesteder`, `navn` lik «Spisesteder», en beskrivelse på én setning og et ikon.
- `GET /api/lag/spisesteder` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per sted. Hvert punkt har `navn` (stedets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltet `kategori` (for eksempel restaurant, kafé, bar).
- Kallet mot kilden filtrerer på spisestedkategorier. Svaret inneholder ingen parkbenker, søppelkasser eller andre punkter som ikke er spisesteder.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

POI-kilden med norske steder fra OpenStreetMap. Bruk Allemannsdata-MCP-serveren til å se hvilke kategorier som finnes før du velger.

Uten kategorifilter får du alt, inkludert hver enkelt parkbenk. Filtrer.
