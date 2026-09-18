Vis varselet for luftkvalitet, ikke bare målingen
enhancement,P3,3 poeng

Luftkvalitetslaget viser hva som er målt nå. Det finnes også et modellert varsel for de neste to døgnene. Vi vil se det.

**Krav**

- Popup-en på et luftkvalitetspunkt skal vise varselet for de neste 24 timene, ikke bare siste måling.
- Høyeste ventede nivå det neste døgnet skal være med.
- Ikonet skal skifte farge når varselet er «Mye» eller verre.

**Datakilde**

Samme kilde som i dag, men en annen operasjon: varselet tar en koordinat og gir en timeserie.

Dette er første gang vi henter to operasjoner i samme lag. Tenk på antall kall: ett varsel per stasjon blir mange.
