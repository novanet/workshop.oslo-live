Skru tiden tilbake med en tidslinje
enhancement,P2,8 poeng

Kartet viser bare nå. Vi vil kunne dra i en tidslinje og se hvordan byen så ut for en time siden, eller i går morges.

**Mål**

Den som ser på kartet kan dra i en tidslinje og se punktene slik de var på et tidspunkt siste døgn, og slippe den tilbake på «nå» for å få de levende dataene igjen.

**Akseptansekriterier**

- `GET /api/lag/luftkvalitet?tid=2026-09-18T08:00:00Z` svarer 200 med den `FeatureCollection` som ble lagret nærmest det tidspunktet. Uten `tid` svarer endepunktet som i dag.
- Finnes det ikke noe lagret bilde innenfor to timer fra `tid`, svarer endepunktet 200 med tom `FeatureCollection`. Et ugyldig `tid` gir 400.
- En tidslinje nederst på kartet dekker fra 24 timer tilbake til nå, med valgt tidspunkt synlig som tekst.
- Drar du tidslinjen, byttes punktene i alle lag som er på til punktene fra det tidspunktet, og telleren per lag viser antallet fra det tidspunktet.
- Står tidslinjen på «nå», henter kartet levende data hvert 15. sekund som før. Står den på et annet tidspunkt, henter kartet ikke levende data før den settes tilbake.
- Lagringen bygger på øyeblikksbildene fra historikk-issuen eller en tilsvarende jobb. PR-en sier hvilken, og hvordan de to henger sammen.
- En test i `tests/OsloLive.Tester` dekker valget av nærmeste bilde uten nettverk.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker uten at PR-en begrunner det.

Dette krever at noe er lagret. Se issuen om øyeblikksbilder, enten bygger du videre på den, eller så avtaler dere hvem som tar hva.
