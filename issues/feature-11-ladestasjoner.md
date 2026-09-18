Nytt lag: ladestasjoner for elbil
enhancement,P3,3 poeng

Vi vil se de offentlige ladestasjonene i Oslo.

**Krav**

- Laget skal ha id `ladestasjoner`, navn «Ladestasjoner» og et passende ikon.
- Ett punkt per ladestasjon.
- Punktene skal vise navn og antall ladepunkter hvis kilden oppgir det.

**Datakilde**

NOBIL fra Enova. Bruk Allemannsdata-MCP-serveren.

**NB:** denne kilden svarer av og til med 502. Det er en del av oppgaven: laget skal ikke ta ned kartet når kilden er nede. Et lag som feiler skal gi tomt resultat eller en tydelig feil, ikke velte resten av siden. Se hvordan `/api/lag/{id}` håndterer det i dag, og vurder om laget selv bør ta ansvaret.
