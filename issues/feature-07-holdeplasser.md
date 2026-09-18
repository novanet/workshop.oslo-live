Nytt lag: holdeplasser og stasjoner
enhancement,P3,2 poeng

Vi vil ha holdeplassene i Oslo på kartet, slik at kollektivlaget får noe å forholde seg til.

**Mål**

Den som ser på kartet ser hvor holdeplassene og stasjonene i Oslo ligger, og kan klikke på en for å se hva den heter og hva slags trafikk som går der.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `holdeplasser`, `navn` lik «Holdeplasser», en beskrivelse på én setning og et ikon.
- `GET /api/lag/holdeplasser` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per holdeplass eller stasjon. Hvert punkt har `navn` (holdeplassens navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltet `transport` (buss, trikk, T-bane, tog, båt, eller en kombinasjon).
- Laget ber kilden om et avgrenset antall eller et avgrenset område rundt Oslo, ikke hele landet. Svaret på `/api/lag/holdeplasser` kommer innen 10 sekunder når kilden svarer normalt.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Entur sitt nasjonale stoppestedsregister. Bruk Allemannsdata-MCP-serveren.

Det er mange tusen holdeplasser i landet. Be om et fornuftig antall, kartet vårt siler selv bort alt utenfor Oslo.
