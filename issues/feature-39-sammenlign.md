Sammenlign to tidspunkt
enhancement,P3,5 poeng

Hvor mange flere elsparkesykler står det i sentrum klokka åtte enn klokka to om natten?

**Mål**

Den som vil se hvordan byen endrer seg gjennom døgnet kan velge et lag og to tidspunkt, og se begge settene på kartet samtidig med differansen som tall.

**Akseptansekriterier**

- Panelet har en seksjon «Sammenlign» med valg av ett lag og to tidspunkt (dato og klokkeslett) fra siste 7 dager.
- Ved klikk på «Sammenlign» hentes punktene for begge tidspunktene fra de lagrede øyeblikksbildene, via `GET /api/lag/{id}?tid=...` eller et tilsvarende endepunkt som PR-en navngir.
- Punktene fra første tidspunkt vises i en lys tone av lagets farge, punktene fra andre tidspunkt i en mørk tone. En liten forklaring i panelet viser hvilken tone som er hvilket tidspunkt.
- Panelet viser antall for hvert tidspunkt og differansen som tall med fortegn, for eksempel «+37» eller «-12».
- Mens sammenligningen er på, hentes ikke levende data for det laget. Klikk på «Avslutt» tar kartet tilbake til levende data.
- Finnes det ikke et bilde nær ett av tidspunktene, viser panelet «ingen data for dette tidspunktet» og kartet forblir uendret.
- Bygger på øyeblikksbildene fra historikk-issuen. PR-en sier hva som er avtalt med den som tar den.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Bygger på øyeblikksbildene. Avklar med den som tar den issuen.
