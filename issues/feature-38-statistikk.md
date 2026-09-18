Et statistikkpanel
enhancement,P3,2 poeng

Vi vil se tall om det kartet viser, ikke bare punktene.

**Mål**

Den som lurer på om kartet er friskt kan åpne et panel og se hvor mange punkter hvert lag har, hvor ferske de er og om noe feiler.

**Akseptansekriterier**

- `GET /api/statistikk` svarer 200 med en liste med ett element per registrert lag, med feltene `id`, `navn`, `antall` (heltall), `eldste` og `nyeste` (ISO 8601 eller `null` når laget ikke har tidsstempler), `hentet` (ISO 8601 for siste vellykkede henting) og `feiler` (true eller false).
- Et lag som kaster feil gir `feiler` lik true og `antall` lik `null`, ikke 0. Endepunktet svarer 200 selv om alle lag feiler.
- Endepunktet henter ikke alle lagene på nytt for hvert oppslag. Det bruker det som allerede er hentet, eller mellomlageret, og PR-en sier hvordan.
- Panelet viser tallene i en sammenleggbar seksjon i panelet til venstre, sammenslått som standard. Tilstanden huskes i `localStorage`.
- Lag som feiler vises med teksten «feiler» i rød farge, ikke med 0.
- Panelet oppdateres hvert 15. sekund sammen med kartet.
- En test i `tests/OsloLive.Tester` sjekker at `/api/statistikk` svarer 200 og har ett element per lag i `/api/lag`.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.
