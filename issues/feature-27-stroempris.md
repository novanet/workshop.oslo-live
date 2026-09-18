Vis strømprisen for Oslo
enhancement,P3,2 poeng

Oslo ligger i prisområde NO1. Vi vil se hva strømmen koster nå, og når den er billigst i dag.

**Mål**

Den som ser på kartet ser hva strømmen koster i Oslo akkurat nå, og når på dagen den er billigst og dyrest.

**Akseptansekriterier**

- `GET /api/stroempris` svarer 200 med JSON som har `naa` (øre per kWh), `billigst` og `dyrest` (hver med `time` og `pris`), og `timer` (liste med 24 elementer med `time` og `pris`) for prisområde NO1 i dag.
- Prisene er i øre per kWh inkludert mva, og PR-en sier om mva er med.
- Feiler kilden, svarer `/api/stroempris` 502 med `{ "feil": "..." }`. Resten av API-et svarer 200 uansett.
- Panelet viser pris nå, billigste og dyreste time med klokkeslett.
- En liten graf over døgnet vises hvis du får det til. Uten graf er de tre tallene fortsatt med.
- Frontenden henter `/api/stroempris` maks én gang i minuttet, ikke hvert 15. sekund.
- Endepunktet ligger i `Program.cs`, bruker `Allemannsdata` og har ingen tilstand. Ingen API-nøkkel.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Strømpriskilden i Allemannsdata, som henter fra hvakosterstrommen.no.

Ikke et kartlag. Eget endepunkt.
