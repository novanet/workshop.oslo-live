Nytt lag: skoler og barnehager
enhancement,P3,3 poeng

Vi vil se skolene og barnehagene i Oslo på kartet.

**Mål**

Den som ser på kartet finner skolene og barnehagene i Oslo, og ser om det er en skole eller barnehage og om den er kommunal eller privat.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `skoler`, `navn` lik «Skoler», en beskrivelse på én setning og et ikon.
- `GET /api/lag/skoler` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per skole eller barnehage i Oslo. Hvert punkt har `navn` og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `type` (grunnskole, videregående, barnehage) og `eierform` (kommunal, privat, fylkeskommunal).
- Må adresser slås opp hos Kartverket, står antall kall per oppdatering i PR-en, og `/api/lag/skoler` svarer innen 60 sekunder første gang og innen 5 sekunder etterpå.
- Laget bruker `Allemannsdata`, `Geo.Lag` og `Geo.Samle`, og registreres med én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Udir sine registre ligger i Allemannsdata under utdanningskilden. Bruk `describe_source` for å se hvilke operasjoner den har, og `get_data` for å se om radene har koordinater.

Har ikke kilden koordinater, men adresse, kan du slå opp adressen hos Kartverket, men skriv i PR-en hvor mange kall det koster.
