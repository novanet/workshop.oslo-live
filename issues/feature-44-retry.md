Prøv én gang til når en kilde svarer dårlig
enhancement,P2,3 poeng

Offentlige API-er er ustabile. Ett hikk skal ikke gi rødt lag i fem minutter.

**Mål**

Et enkelt hikk hos en kilde er usynlig for den som ser på kartet: appen prøver på nytt før den gir opp, og laget blir bare rødt når kilden faktisk er nede.

**Akseptansekriterier**

- Et kall i `Allemannsdata.Hent` som feiler med 5xx, tidsavbrudd eller nettverksfeil prøves på nytt inntil to ganger, altså maks tre forsøk totalt.
- Mellom forsøkene venter klienten, minst 500 ms før andre forsøk og lengre før tredje. Ventetiden respekterer `CancellationToken`.
- Et kall som svarer 404 prøves ikke på nytt. Feilen kastes etter første forsøk, og `/api/lag/{id}` svarer 502 som i dag.
- Lykkes et nytt forsøk, får laget svaret som normalt, og svaret legges i mellomlageret.
- Hvert nye forsøk logges på nivå Warning med kilde, operasjon og forsøksnummer. Et kall som lykkes første gang logger ikke noe ekstra.
- Logikken ligger i `Allemannsdata`, ikke i lagene. Ingen lag er endret.
- Enhetstester i `tests/OsloLive.Tester` med en falsk `HttpMessageHandler` dekker: lykkes på andre forsøk, 404 gir ingen nye forsøk, og gir opp etter tredje forsøk. Testene går ikke mot nettet og venter ikke i sanntid.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker (ingen Polly).

Legg dette i `Allemannsdata`, ikke i hvert enkelt lag.
