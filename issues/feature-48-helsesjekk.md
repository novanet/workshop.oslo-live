En helsesjekk som betyr noe
enhancement,P3

`/api/helse` svarer «ok» selv om alle kildene er nede.

**Krav**

- En dypere sjekk som sier noe om hver kilde vi faktisk bruker.
- Den skal svare raskt — ikke vente på alle kildene hver gang.
- Den vanlige `/api/helse` skal fortsatt være lynrask, for den brukes av Azure til å avgjøre om containeren lever.

Forskjellen på «lever prosessen» og «virker tjenesten» er hele poenget her.
