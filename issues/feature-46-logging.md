Strukturert logging
enhancement,P3,2 poeng

Loggen er tekst i dag. Når kartet kjører i Azure vil vi kunne søke i den.

**Krav**

- Logglinjene skal ha faste felter: kilde, operasjon, varighet, om det traff mellomlageret, og utfall.
- Ingen hemmeligheter eller fulle URL-er med parametre i loggen.
- Nivåene skal brukes riktig: forventede feil fra en kilde er advarsel, ikke feil.
