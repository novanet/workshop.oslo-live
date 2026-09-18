Ta vare på et øyeblikksbilde hver time
enhancement,P3,5 poeng

Alt vi viser er ferskvare, og forsvinner. Vi vil kunne se tilbake: hvor mange elsparkesykler sto i sentrum klokka åtte i morges?

**Krav**

- Et øyeblikksbilde av hvert lag lagres én gang i timen.
- `GET /api/lag/{id}/historikk` gir antall punkter per time siste døgn.
- Panelet viser en liten graf.

Hold lagringen enkel — filer på disk holder. Tenk på at jobben kjører i en container som kan bli slått av; skriv i PR-en hva det betyr for løsningen.
