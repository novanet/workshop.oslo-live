Nytt lag: fly i lufta over Oslo
enhancement,P2,8 poeng

Vi vil se flytrafikken. Fly som er i lufta over byen, og fly som står på bakken på Gardermoen. Dette er laget vi har lyst til å ha på storskjermen når noen spør hva kartet er godt for.

**Mål**

Den som ser på storskjermen ser flyene over Oslo bevege seg, og kan klikke på ett for å se kallesignal, høyde og fart. Valget av datakilde er begrunnet slik at den som drifter kartet vet hva det koster og hva som skjer når kilden svikter.

**Akseptansekriterier**

- `GET /api/lag` inneholder et lag med `id` lik `fly`, `navn` lik «Flytrafikk», en beskrivelse på én setning og et ikon.
- `GET /api/lag/fly` svarer 200 med en `FeatureCollection`. Når kilden svarer og det er fly innenfor utsnittet, er de med som punkter med `[lon, lat]`.
- Ett punkt per fly med kjent posisjon. Hvert punkt har `navn` (kallesignal) og `kilde`. Popup-en viser feltene `kallesignal`, `høyde` (med enhet), `fart` (knop) og `status` («i lufta» eller «på bakken»).
- To kall til `/api/lag/fly` med mer enn 30 sekunder mellom gir ulike koordinater for fly som er i lufta.
- Finnes posisjonene i Allemannsdata, brukes `Allemannsdata`-klienten. Brukes en annen kilde, svarer PR-en på de tre spørsmålene under, og ingen nøkkel eller token ligger i repoet. Krever kilden nøkkel, leses den fra miljøvariabel og laget svarer med tom `FeatureCollection` uten den.
- Svikter kilden, svarer bare `/api/lag/fly` 502. `/api/lag` og de andre lagene svarer 200.
- Endres kartutsnittet i `Geo` for å få med Gardermoen, er alle eksisterende tester fortsatt grønne uten å være endret, og PR-en begrunner hvorfor «Oslo Live» skal dekke det området.
- `dotnet build` og `dotnet test` er grønne. Nye NuGet-pakker bare hvis PR-en begrunner dem.

**Datakilde**

Det er her oppgaven begynner. Finn ut om Allemannsdata har det vi trenger. Bruk `search_wiki` og `describe_source`, og se nøye på hva kilden faktisk gir tilbake, ikke bare hva den heter.

Finner du ikke posisjoner der, er det et gyldig svar. Da er oppgaven å foreslå en annen kilde, og begrunne valget i pull requesten:

- Hvorfor akkurat den kilden?
- Hva koster den? Krever den nøkkel, og har den et tak på antall kall?
- Hva skjer med kartet den dagen kilden er nede eller avviser oss?

`ARKITEKTUR.md` sier at lag skal bruke `Allemannsdata`-klienten. Bryter du med det, skal du si fra i PR-en hvorfor, og gjøre det på en måte som ikke ødelegger for de andre lagene.

**Et hint**

Gardermoen ligger et godt stykke nordøst for sentrum. Får du ingen punkter, eller bare punkter fra selve byen, er det verdt å sjekke om de i det hele tatt kommer gjennom `Geo.Lag`.

Vurderer du å endre kartutsnittet: det er delt av alle lagene, og det finnes tester som bygger på hvor grensene går. Tenk over hva «Oslo Live» skal bety før du flytter dem, og skriv resonnementet i PR-en.
