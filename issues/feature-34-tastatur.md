Tastatursnarveier
enhancement,P3,1 poeng

Når kartet står på storskjerm vil vi ikke lete etter museknapper.

**Mål**

Den som står ved storskjermen kan styre lag og kamera fra tastaturet uten å finne fram til knappene med musa.

**Akseptansekriterier**

- Tastene `1` til `9` slår lag nummer 1 til 9 i lagvelgeren av og på, i rekkefølgen fra `/api/lag`. Avkrysningsboksen og telleren nederst oppdateres som ved museklikk.
- `r` starter og stopper rotasjonen, som knappen «Snurr». `3` veksler mellom 2D og 3D, som knappen «2D»/«3D». `h` flyr hjem til sentrum, som knappen «Sentrum».
- `?` åpner en liste over snarveiene. `?` eller Escape lukker den igjen.
- Snarveiene slår ikke inn når fokus står i et `input`, `textarea` eller `select`, eller i et element med `contenteditable`.
- Snarveiene slår ikke inn når Ctrl, Alt eller Cmd holdes nede, så nettleserens egne snarveier virker som før.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.
