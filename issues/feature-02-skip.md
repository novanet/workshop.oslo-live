Nytt lag: skip i Oslofjorden
enhancement,P2,2 poeng

Oslofjorden er full av ferger, lastebåter og fritidsbåter. Vi vil se dem bevege seg på kartet.

**Mål**

Den som ser på kartet ser skipene i indre Oslofjord der de er akkurat nå, og ser dem flytte seg mellom oppdateringene.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `skip`, `navn` lik «Skipstrafikk», en beskrivelse på én setning og et ikon.
- `GET /api/lag/skip` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per fartøy med kjent posisjon. Hvert punkt har `navn` (fartøyets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `fart` (i knop) og `destinasjon`. Mangler kilden en verdi, utelates feltet i stedet for å vise «null».
- Søkeradiusen rundt sentrum er stor nok til å dekke fjorden ned til Nesodden. Det synes ved at Nesoddbåtene er med når de er i rute.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

AIS-posisjoner fra BarentsWatch. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon og parametre.

Nesoddbåtene går rett forbi Rådhuskaia, så det er lett å se om laget virker. Merk at radiusen rundt Oslo sentrum må være stor nok til å få med fjorden.
