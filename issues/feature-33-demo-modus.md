Demomodus for storskjerm
enhancement,P2,3 poeng

Kartet skal stå på en skjerm hele dagen. Vi vil ha en modus som viser fram innholdet av seg selv.

**Mål**

Kartet kan stå på en storskjerm uten at noen rører det, og viser fram lagene ett etter ett med rolige kamerabevegelser og stor tekst som kan leses fra andre siden av rommet.

**Akseptansekriterier**

- En knapp «Demo» ved kameraknappene starter demomodus. Knappen viser at modusen er på (`aria-pressed="true"`).
- I demomodus flyr kartet mellom minst fire faste steder i Oslo (for eksempel Rådhuset, Grünerløkka, Bjørvika, Frognerparken). Hver flytur varer minst 8 sekunder, og kartet står i ro minst 10 sekunder på hvert sted.
- På hvert sted trekkes ett lag fram: bare det laget vises, og lagets ikon og navn står midt på skjermen i skriftstørrelse minst 48 px. Lagene rulleres i rekkefølgen fra `/api/lag`.
- Lag som er røde (feilende) eller har null punkter hoppes over.
- Museklikk, tastetrykk eller berøring avslutter modusen. Etterpå er de lagene brukeren hadde på før demoen på igjen, og knappen viser av.
- Lagene hentes fortsatt på nytt hvert 15. sekund mens demoen går.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Dette er laget for å se bra ut på avstand. Tenk stor skrift og rolige bevegelser.
