Slå sammen punkter som ligger oppå hverandre
enhancement,P2,5 poeng

Med flere lag på samtidig blir sentrum et eneste rot av ikoner. Vi vil ha klynging: punkter som ligger tett samles i én markør med antall, og sprer seg når du zoomer inn.

**Mål**

Den som ser på kartet med mange lag på ser ryddige klynger med tall i stedet for hundrevis av ikoner oppå hverandre, og kan zoome inn for å se enkeltpunktene.

**Akseptansekriterier**

- Punktene i hvert lag ligger i en GeoJSON-kilde i MapLibre med `cluster: true`, ikke som HTML-markører.
- På zoomnivå 12 i sentrum, med luftkvalitet og minst ett lag til på, vises punkter som ligger tett som én klynge med antallet skrevet i markøren.
- Zoomer du inn til nivå 16 eller mer, er klyngene i sentrum delt opp i enkeltpunkter. Klikk på en klynge zoomer inn til den sprer seg.
- Klikk på et enkeltpunkt åpner fortsatt en popup med `navn`, detaljene og `kilde`, som i dag.
- Å slå et lag av i lagvelgeren fjerner lagets punkter fra kartet og fra klyngene. Å slå det på igjen tar dem med igjen.
- Telleren per lag i lagvelgeren og summen nederst viser fortsatt antall punkter, ikke antall klynger.
- Endringen er begrenset til `wwwroot/index.html`. Ingen endringer i API-et.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker, ingen npm eller byggesteg.

Dette er en ren frontend-oppgave i `wwwroot/index.html`.

Kartet er MapLibre GL JS. Der er klynging innebygd: en GeoJSON-kilde med `cluster: true` gjør mesteparten av jobben, og så tegner du klyngene med et eget lag. Merk at punktene i dag er HTML-markører, ikke en GeoJSON-kilde, så den første delen av oppgaven er å flytte dem over.
