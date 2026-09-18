Nytt lag: smilefjes fra Mattilsynet
enhancement,P3,8 poeng

Mattilsynet gir spisesteder smilefjes etter tilsyn. Vi vil se karakterene på kartet.

**Mål**

Den som ser på kartet ser hvilke spisesteder i Oslo som har fått smilefjes, hvilken karakter de fikk og når, uten at kartet blir tregt av alle adresseoppslagene.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `smilefjes`, `navn` lik «Smilefjes», en beskrivelse på én setning og et ikon.
- `GET /api/lag/smilefjes` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kildene svarer.
- Ett punkt per spisested i Oslo med registrert karakter. Hvert punkt har `navn` (spisestedets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `karakter` (blid, streng eller sur), `tilsyn` (dato) og `adresse`.
- Feltet `ikon` i `detaljer` eller lagets ikon følger karakteren, slik at blid, streng og sur kan skilles fra hverandre på kartet.
- Adresseoppslag mot Kartverket gjøres bare for steder som mangler koordinat, og resultatet gjenbrukes mellom oppdateringer. Antall kall per oppdatering står i PR-en. Første kall til `/api/lag/smilefjes` svarer innen 60 sekunder, senere kall innen 5 sekunder.
- Laget bruker `Allemannsdata` for begge kildene, går via `Geo.Lag` og `Geo.Samle`, og registreres med én linje i `Program.cs`. Ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Mattilsynets smilefjesregister. Bruk Allemannsdata-MCP-serveren.

**Dette er den vanskeligste oppgaven i repoet.** Smilefjeskilden gir adresse, ikke koordinater. Du må slå opp adressen for å finne punktet. Kartverkets adressesøk ligger også i Allemannsdata. Det betyr to kilder i samme lag, og mange oppslag. Tenk på hvor mange kall du gjør.
