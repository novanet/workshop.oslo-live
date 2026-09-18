Vær grei mot kildene
enhancement,P2,3 poeng

Med tjue lag og mange som ser på kartet samtidig kan vi lage mye trafikk mot API-er som ingen tar betalt for.

**Mål**

Uansett hvor mange som ser på kartet, sender appen aldri flere samtidige kall mot Allemannsdata enn et tak vi har satt. Kall over taket venter på tur i stedet for å bli avvist.

**Akseptansekriterier**

- `Allemannsdata` slipper aldri gjennom flere samtidige kall mot kilden enn taket. Med tak 2 og 5 samtidige kall er maks 2 i gang om gangen, og alle 5 fullfører.
- Kall over taket venter i kø. De blir ikke avvist, ikke kastet, og de får svar når det er ledig.
- Ventingen respekterer `CancellationToken`. Lukker brukeren fanen mens kallet står i kø, forlater det køen.
- Treff i mellomlageret tar ikke opp en plass under taket.
- Taket leses fra `appsettings.json`, nøkkel `Allemannsdata:MaksSamtidigeKall`, med en standardverdi når nøkkelen mangler. PR-en sier hva standarden er og hvorfor.
- Logikken ligger i `Allemannsdata`, ikke i lagene. Ingen lag er endret.
- En enhetstest i `tests/OsloLive.Tester` med en falsk `HttpMessageHandler` viser at taket holdes, uten nettverk.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

Skriv i PR-en hva du satte taket til, og hvorfor.
