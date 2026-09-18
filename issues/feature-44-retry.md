Prøv én gang til når en kilde svarer dårlig
enhancement,P2,3 poeng

Offentlige API-er er ustabile. Ett hikk skal ikke gi rødt lag i fem minutter.

**Krav**

- Et mislykket kall prøves på nytt, med litt venting mellom forsøkene.
- Maks to nye forsøk, så gir vi opp.
- 404 skal ikke prøves på nytt — den blir ikke bedre av å vente.
- Det skal logges når et kall måtte prøves om igjen.

Legg dette i `Allemannsdata`, ikke i hvert enkelt lag.
