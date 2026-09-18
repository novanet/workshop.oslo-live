Kartet må virke på mobil
enhancement,P3,3 poeng

Panelet dekker halve skjermen på en telefon.

**Mål**

Den som åpner kartet på en telefon ser kartet, kan slå lag av og på, og kan lese popup-ene uten at panelet ligger i veien.

**Akseptansekriterier**

- Ved skjermbredde under 700 px ligger panelet nederst på skjermen, sammenslått til en linje med tittel og et håndtak, og dekker maks 20 prosent av høyden.
- Panelet kan dras opp med fingeren eller åpnes med et trykk på håndtaket, og dekker da maks 60 prosent av høyden med lagvelgeren rullbar inni.
- Kameraknappene («Snurr», «2D», «Sentrum») ligger over det sammenslåtte panelet og overlapper det ikke, verken lukket eller åpent.
- En popup som åpnes på et punkt nederst på skjermen er synlig i sin helhet, ikke skjult bak panelet.
- Kartet kan tiltes og roteres med to fingre (MapLibre sine `touchPitch` og `touchZoomRotate` er på).
- Ved skjermbredde 700 px eller mer ser kartet ut som i dag.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.
