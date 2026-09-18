Varmekart i stedet for punkter
enhancement,P3,5 poeng

Med mange punkter blir sentrum et rot. Vi vil kunne bytte et lag til varmekart, så man ser hvor tettheten er.

**Mål**

Den som ser på et lag med mange punkter kan bytte det til et varmekart og se hvor tettheten er, og valget står seg neste gang kartet åpnes.

**Akseptansekriterier**

- Hver rad i lagvelgeren har en bryter med to tilstander: punkter eller varmekart. Standard er punkter.
- Slår du varmekart på for et lag, forsvinner lagets markører og et MapLibre-lag av typen `heatmap` vises for lagets punkter. Slår du det av, kommer markørene tilbake.
- Varmekartet bruker lagets egen farge fra `FARGER` eller reservepaletten, ikke en fast farge for alle lag.
- Valget per lag lagres i `localStorage` og gjelder fortsatt etter at siden lastes på nytt.
- Å slå laget av i lagvelgeren skjuler både markører og varmekart. Telleren per lag viser fortsatt antall punkter.
- Varmekartet oppdateres når laget hentes på nytt hvert 15. sekund.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

MapLibre har `heatmap`-lagtypen innebygd. Den trenger en GeoJSON-kilde, og punktene er HTML-markører i dag.
