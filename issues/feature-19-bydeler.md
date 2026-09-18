Tell punkter per bydel
enhancement,P3,5 poeng

Vi vil vite hvor i byen det skjer noe. Et panel som viser antall punkter per bydel, for det laget som er valgt.

**Krav**

- `GET /api/lag/{id}/bydeler` skal gi tilbake antall punkter per bydel.
- Panelet viser de fem bydelene med flest punkter.
- Tallene skal oppdatere seg sammen med resten av kartet.

Bydelsgrensene i Oslo finnes som åpne data. Alternativt kan du bruke et omvendt oppslag på koordinaten — Kartverket har en operasjon for det i Allemannsdata. Velg selv, men skriv i PR-en hvorfor.
