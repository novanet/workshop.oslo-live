Nytt lag: trafikkregistreringspunkt
enhancement,P3,1 poeng

Statens vegvesen teller biler på faste punkter langs veinettet. Vi vil se punktene i Oslo på kartet.

**Mål**

Den som ser på kartet ser hvor Vegvesenet teller trafikk i Oslo, og kan klikke på et punkt for å se hva det heter og hvilken vei det ligger på.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `trafikk`, `navn` lik «Trafikk», en beskrivelse på én setning og et ikon.
- `GET /api/lag/trafikk` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per registreringspunkt. Hvert punkt har `navn` (punktets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltet `vei` (veinummer eller veinavn fra kilden).
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Statens vegvesen sine trafikkregistreringspunkt. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon.

Denne kilden gir `data` som en liste rett ut, akkurat som luftkvalitetslaget. Det er ikke alle kilder som gjør det.
