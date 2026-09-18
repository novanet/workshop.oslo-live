Nytt lag: idretts- og friluftsanlegg
enhancement,P3,2 poeng

Anleggsregisteret vet om hver eneste fotballbane, svømmehall og lysløype i landet. Vi vil ha Oslo-anleggene på kartet.

**Mål**

Den som ser på kartet finner idrettsanleggene i Oslo, og ser hva slags anlegg det er og om det er i drift.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `idrettsanlegg`, `navn` lik «Idrettsanlegg», en beskrivelse på én setning og et ikon.
- `GET /api/lag/idrettsanlegg` svarer 200 med en `FeatureCollection` med minst ett punkt innenfor kartutsnittet når kilden svarer.
- Ett punkt per anlegg i Oslo kommune. Hvert punkt har `navn` (anleggets navn) og `kilde`, og koordinatene ligger som `[lon, lat]`.
- Popup-en viser feltene `type` (anleggstype) og `status`.
- Laget filtrerer på Oslo kommune i kallet mot kilden, ikke ved å hente hele landet og sile etterpå.
- Laget er én klasse i `Lag/` som bruker `Allemannsdata.HentListe`, `Geo.Lag` og `Geo.Samle`, pluss én linje i `Program.cs`. Ingen endringer i `Kart/`, ingen egen `HttpClient`, ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Anleggsregisteret hos Kultur- og likestillingsdepartementet. Bruk Allemannsdata-MCP-serveren.

Operasjonen kan filtrere på kommune. Bruk det, så slipper du å hente hele landet.
