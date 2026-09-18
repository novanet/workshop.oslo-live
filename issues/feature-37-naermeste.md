Hva er nærmest her?
enhancement,P3,3 poeng

Klikk et sted på kartet og få vite hva som er nærmest fra hvert lag.

**Mål**

Den som står et sted i byen kan peke på kartet og få vite hva det nærmeste er fra hvert lag, og hvor langt unna det er.

**Akseptansekriterier**

- Høyreklikk, eller langt trykk på berøringsskjerm, setter en markør på kartet og åpner et panel «Nærmest her».
- Panelet viser ett innslag per lag som er på: lagets ikon og navn, navnet på det nærmeste punktet og avstanden i meter som heltall. Lag uten punkter vises ikke.
- Innslagene er sortert med korteste avstand øverst.
- Klikk på et innslag flyr kartet til punktet og åpner popup-en.
- Et nytt høyreklikk flytter markøren og regner ut på nytt. En lukkeknapp fjerner markør og panel.
- Avstanden beregnes med haversine-formelen. Funksjonen ligger i `Kart/` (for eksempel `Geo.Avstand(lat1, lon1, lat2, lon2)`) med en enhetstest i `tests/OsloLive.Tester` som sjekker at Rådhuset til Sofienbergparken gir mellom 1 200 og 1 300 meter, og at samme punkt gir 0.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Avstand mellom to koordinater er ikke Pythagoras. Bruk haversine, legg den i `Kart/`, og skriv en test for den.
