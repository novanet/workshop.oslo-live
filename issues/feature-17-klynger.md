Slå sammen punkter som ligger oppå hverandre
enhancement,P2,5 poeng

Med flere lag på samtidig blir sentrum et eneste rot av ikoner. Vi vil ha klynging: punkter som ligger tett samles i én markør med antall, og sprer seg når du zoomer inn.

**Krav**

- Punkter som ligger tett skal vises som én klynge med antall i.
- Zoom inn sprer klyngen.
- Det skal fortsatt gå an å slå lag av og på.

Dette er en ren frontend-oppgave i `wwwroot/index.html`.

Kartet er MapLibre GL JS. Der er klynging innebygd: en GeoJSON-kilde med `cluster: true` gjør mesteparten av jobben, og så tegner du klyngene med et eget lag. Merk at punktene i dag er HTML-markører, ikke en GeoJSON-kilde — så den første delen av oppgaven er å flytte dem over.
