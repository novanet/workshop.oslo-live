Del kartutsnittet som en lenke
enhancement,P3,2 poeng

Når noen finner noe interessant, vil vi kunne sende en lenke som åpner akkurat det samme utsnittet med de samme lagene på.

**Mål**

Den som finner noe på kartet kan kopiere adressen fra adressefeltet, sende den til en annen, og den andre får opp det samme utsnittet med de samme lagene på.

**Akseptansekriterier**

- Adressen inneholder senter (lat og lon med 4 desimaler), zoomnivå, pitch, bearing og listen over lag som er på, for eksempel i `#`-delen av adressen.
- Åpner du adressen i en ny fane, står kartet på samme senter og zoom (avvik under 0,001 grader og 0,1 zoomnivå), og bare de lagene som sto i adressen er på.
- Adressen oppdaterer seg når du panorerer, zoomer, roterer eller slår et lag av eller på. Nettleserens tilbakeknapp går ikke gjennom hvert steg (bruk `replaceState`, ikke `pushState`).
- Er adressen tom eller ugyldig, starter kartet som i dag: sentrum, zoom 14.2, alle lag på.
- Et lag i adressen som ikke finnes i `/api/lag` ignoreres uten feil.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Ren frontend-oppgave i `wwwroot/index.html`.
