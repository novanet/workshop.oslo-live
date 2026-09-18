Nytt lag: veiarbeid og stengte veier
enhancement,P3,2 poeng

Statens vegvesen melder fra om veiarbeid, stengte veier og hendelser. Vi vil se dem på kartet så vi vet hvor det er kø.

**Krav**

- Laget skal ha id `veiarbeid`, navn «Veiarbeid» og et passende ikon.
- Ett punkt per melding som har koordinat.
- Punktene skal vise hva slags melding det er, beskrivelsen og hvor lenge den varer.

**Datakilde**

Vegvesenets meldingsfeed (DATEX II). Bruk Allemannsdata-MCP-serveren til å finne operasjonen.

Meldingene har et `severity`-felt. Bruk det til noe: for eksempel ulikt ikon på lav og høy alvorlighet.
