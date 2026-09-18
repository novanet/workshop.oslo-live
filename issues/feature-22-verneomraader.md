Nytt lag: verneområder og naturtyper
enhancement,P3,3 poeng

Markagrensa, naturreservatene og de vernede områdene rundt Oslo hører hjemme på kartet.

**Mål**

Den som ser på kartet ser hvor verneområdene rundt Oslo ligger, og kan klikke på et for å se hva slags vern det har og når det ble vernet.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `verneomraader`, `navn` lik «Verneområder», en beskrivelse på én setning og et ikon.
- `GET /api/lag/verneomraader` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per område, plassert på et representativt punkt i området (for eksempel midtpunktet). Hvert punkt har `navn` (områdets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `vernetype` og `vernet` (år eller dato).
- PR-en sier hvordan punktet for et område ble valgt og hvorfor.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Naturbase hos Miljødirektoratet. Bruk `describe_source` for å finne operasjonen og parametrene.

Områder er flater, ikke punkter. Kartkontrakten vår tar bare punkter i dag, så du må velge et representativt punkt, midtpunktet for eksempel. Skriv i PR-en hva du valgte og hvorfor.
