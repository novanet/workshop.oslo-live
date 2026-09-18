Gjør kartutsnittet konfigurerbart
enhancement,P3,5 poeng

Oslo-boksen er hardkodet i `Geo`. Vi vil kunne flytte kartet til en annen by uten å endre kode.

**Mål**

Den som vil kjøre kartet for Bergen eller Trondheim kan gjøre det ved å endre `appsettings.json`, uten å røre kode, og uten at noe endrer seg for Oslo når innstillingen mangler.

**Akseptansekriterier**

- `appsettings.json` har en seksjon `Kart` med `MinLat`, `MaksLat`, `MinLon`, `MaksLon`, `SentrumLat` og `SentrumLon`. Standardverdiene er dagens: 59.80, 60.14, 10.45, 10.98, 59.9139 og 10.7522.
- Mangler seksjonen, bruker appen dagens verdier. `dotnet test` er grønn uten at noen konfigurasjon settes opp.
- `Geo.IOslo` og `Geo.Lag` bruker de konfigurerte grensene. Med `Kart` satt til Bergen (for eksempel 60.30 til 60.45, 5.20 til 5.40) gir `Geo.Lag` for Rådhuset i Oslo `null`.
- `GET /api/kart` svarer 200 med utsnittet og sentrum, og frontenden leser `SENTRUM` og startposisjon derfra i stedet for en hardkodet konstant.
- Lagene som bruker `Geo.OsloLat` og `Geo.OsloLon` får sentrum fra konfigurasjonen uten at hvert lag må endres.
- En test i `tests/OsloLive.Tester` setter et annet utsnitt, sjekker at et Oslo-punkt faller utenfor, og setter tilbake, uten å påvirke andre tester.
- PR-en sier hvordan den statiske `Geo`-klassen ble håndtert og hvorfor.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Tenk på at `Geo` er statisk i dag. Det er en del av oppgaven.
