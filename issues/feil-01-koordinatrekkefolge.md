Alle punkter havner i havet, langt utenfor Norge
bug,P1,2 poeng

Zoomer du ut på kartet, ligger punktene våre midt i Indiahavet i stedet for i Oslo. Selve dataene ser riktige ut: luftkvalitetsstasjonene har fornuftige koordinater når jeg kaller `/api/lag/luftkvalitet` og leser tallene. Det er plasseringen på kartet som er feil.

**Slik ser du det**

1. Start appen og åpne `http://localhost:5199`.
2. Zoom helt ut.
3. Punktene ligger i havet sørøst for Afrika.

**Forventet**

Punktene skal ligge der koordinatene sier, altså i Oslo.

Kartet leser GeoJSON. Sjekk hva GeoJSON-standarden sier om rekkefølgen på koordinatene i et `Point`.
