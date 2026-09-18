Nytt lag: elsparkesykler og bysykler
enhancement,P2,2 poeng

Vi vil se delt mobilitet i sanntid på kartet: elsparkesykler, bysykler og delebiler som står ledige i Oslo akkurat nå.

**Krav**

- Laget skal ha id `mobilitet`, navn «Delt mobilitet» og et passende ikon.
- Ett punkt per ledig kjøretøy.
- Punktene skal vise operatør (Ryde, Voi, Oslo Bysykkel …) og hva slags kjøretøy det er.

**Datakilde**

Entur samler delt mobilitet fra alle operatørene. Bruk Allemannsdata-MCP-serveren til å finne riktig operasjon og hvilke parametre den tar. Se `README.md` for hvordan du utforsker en kilde.

Følg mønsteret i `Lag/LuftkvalitetLag.cs`, og husk å registrere laget i `Program.cs`.
