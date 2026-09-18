Nytt lag: fly i lufta over Oslo
enhancement,P2

Vi vil se flytrafikken. Fly som er i lufta over byen, og fly som står på bakken på Gardermoen. Dette er laget vi har lyst til å ha på storskjermen når noen spør hva kartet er godt for.

**Krav**

- Laget skal ha id `fly`, navn «Flytrafikk» og et passende ikon.
- Ett punkt per fly med kjent posisjon.
- Punktene skal vise kallesignal, høyde, fart og om flyet er i lufta eller på bakken.
- Flyene skal flytte seg mellom oppdateringene. Et fly som står stille i lufta er en feil.

**Datakilde**

Det er her oppgaven begynner. Finn ut om Allemannsdata har det vi trenger. Bruk `search_wiki` og `describe_source`, og se nøye på hva kilden faktisk gir tilbake — ikke bare hva den heter.

Finner du ikke posisjoner der, er det et gyldig svar. Da er oppgaven å foreslå en annen kilde, og begrunne valget i pull requesten:

- Hvorfor akkurat den kilden?
- Hva koster den? Krever den nøkkel, og har den et tak på antall kall?
- Hva skjer med kartet den dagen kilden er nede eller avviser oss?

`ARKITEKTUR.md` sier at lag skal bruke `Allemannsdata`-klienten. Bryter du med det, skal du si fra i PR-en hvorfor, og gjøre det på en måte som ikke ødelegger for de andre lagene.

**Et hint**

Gardermoen ligger et godt stykke nordøst for sentrum. Får du ingen punkter, eller bare punkter fra selve byen, er det verdt å sjekke om de i det hele tatt kommer gjennom `Geo.Lag`.

Vurderer du å endre kartutsnittet: det er delt av alle lagene, og det finnes tester som bygger på hvor grensene går. Tenk over hva «Oslo Live» skal bety før du flytter dem, og skriv resonnementet i PR-en.
