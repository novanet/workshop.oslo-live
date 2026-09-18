Vis vannstanden i fjorden
enhancement,P3,2 poeng

Hvor høyt står sjøen akkurat nå? Vi vil se vannstand og tidevann for Oslo havn.

**Mål**

Den som ser på kartet ser hvor høyt sjøen står i Oslo havn akkurat nå, og når neste høyvann eller lavvann kommer.

**Akseptansekriterier**

- `GET /api/vannstand` svarer 200 med JSON som har `naa` (vannstand i cm), `neste` med `type` («høyvann» eller «lavvann»), `tidspunkt` (ISO 8601) og `verdi` (cm), og `maalt` (tidspunkt for målingen).
- Feiler kilden, svarer `/api/vannstand` 502 med `{ "feil": "..." }`. `/api/lag` og lagene svarer 200 uansett.
- Panelet har et felt som viser vannstand nå og neste høyvann eller lavvann med klokkeslett. Feiler kilden, viser feltet «utilgjengelig», ikke tomt.
- Feltet oppdateres i samme takt som kartet, hvert 15. sekund. Mellomlageret i `Allemannsdata` sørger for at kilden ikke kalles oftere enn hvert 30. sekund.
- Endepunktet ligger i `Program.cs`, bruker `Allemannsdata` og har ingen tilstand. Ingen API-nøkkel, ingen endringer i `Kart/`.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Kartverkets tidevannsdata ligger under værkilden i Allemannsdata. Bruk `describe_source` for å finne riktig operasjon.

Dette er ikke et kartlag med mange punkter, så `ILag` passer dårlig. Lag et eget endepunkt, som i issuen om adressesøk.
