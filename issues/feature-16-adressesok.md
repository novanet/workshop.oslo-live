Søk etter adresse og hopp dit på kartet
enhancement,P2,3 poeng

Vi vil kunne skrive «Karl Johans gate 1» i et søkefelt og få kartet til å zoome dit.

**Krav**

- Et søkefelt i panelet øverst til venstre.
- `GET /api/sok?q=...` skal gi tilbake treff med navn og koordinat.
- Klikk på et treff flytter kartet dit og setter en midlertidig markør.

**Datakilde**

Kartverkets adresse- og stedsnavnsøk ligger i Allemannsdata. Bruk MCP-serveren til å finne operasjonen.

Dette er ikke et kartlag, så `ILag` passer ikke. Legg et eget endepunkt i `Program.cs`.
