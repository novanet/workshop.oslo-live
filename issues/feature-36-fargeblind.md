Fargene må virke for alle
enhancement,P3,2 poeng

Lagene skilles med farge i dag. Rundt åtte prosent av menn ser rødt og grønt likt.

**Mål**

Den som har rød-grønn fargeblindhet kan skille lagene fra hverandre på kartet og i lagvelgeren like godt som alle andre.

**Akseptansekriterier**

- Paletten i `FARGER` og `RESERVE` i `wwwroot/index.html` er byttet til farger som kan skilles ved deuteranopi og protanopi. Ingen to lag som ligger ved siden av hverandre i `/api/lag` har farger som blir like i en simulering.
- Hvert punkt skilles på noe annet enn farge i tillegg: ikonet fra laget vises i markøren, og markørene har ulik form eller kantstil per lag, eller et tilsvarende grep som er synlig på skjerm.
- Prikken ved hvert lag i lagvelgeren har samme tilleggsmarkering som punktene, slik at det går an å koble lag og punkt uten farge.
- Kontrasten mellom ikonet og bakgrunnen i markøren er minst 3:1 for alle lag.
- PR-en sier hvilket verktøy eller hvilken simulering som ble brukt til å teste, og har et skjermbilde eller en beskrivelse av resultatet.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Dette er en ekte tilgjengelighetsoppgave, ikke pynt. Kartet skal vises for en hel sal.
