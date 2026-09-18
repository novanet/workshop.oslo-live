Nytt lag: kollektivtrafikk som beveger seg
enhancement,P2,3 poeng

Dette er laget vi vil ha på storskjermen. Busser, trikker, T-bane og tog i Oslo rapporterer posisjonen sin i sanntid. Vi vil se dem kjøre.

**Krav**

- Laget skal ha id `kollektiv`, navn «Kollektiv» og et passende ikon.
- Ett punkt per kjøretøy som rapporterer posisjon akkurat nå.
- Punktene skal vise linjenummer, hva slags kjøretøy det er (buss, trikk, tog) og hvor det er på vei.
- Velg ikon ut fra transportmiddel hvis du får det til: buss og trikk skal ikke se like ut.

**Datakilde**

Entur samler sanntidsposisjoner for hele landet. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon.

Kartet henter lagene på nytt hvert 15. sekund, så punktene flytter seg av seg selv når laget virker.
