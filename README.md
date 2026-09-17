# Oslo Live

Et kart over Oslo der hvert lag er levende offentlige data. Elsparkesyklene som står ledige akkurat nå. Skipene i fjorden. Politiloggen. Badetemperaturen. Bussene som kjører forbi mens du ser på.

Dataene kommer fra [Allemannsdata](https://allemannsdata.com) — 51 norske offentlige datakilder pakket som MCP-servere. Ingen API-nøkkel, ingen registrering.

Dette repoet er øvingsprosjektet på kurset **«Bygg din egen Nils Georg»** hos Novanet. Det er også et ekte, lite produkt: det bygger, det kjører, og det virker.

```bash
dotnet run --project src/OsloLive --urls http://localhost:5199
```

Åpne <http://localhost:5199>.

## Dette er ikke et vanlig repo

Hver deltaker på kurset bygger sin egen autonome agent og slipper den løs **på dette repoet**. Alle jobber på den samme backloggen samtidig.

Det betyr noen ting du må vite før du starter:

- **Agentene vil kollidere.** To agenter kan ta samme issue og levere hver sin pull request. Det er ikke en feil i oppsettet — det er med vilje, og det er en av tingene vi skal snakke om.
- **Første gode PR vinner issuen.** Ikke den første som leverer, men den første som leverer noe som faktisk virker.
- **Repoet er offentlig.** Aldri commit en API-nøkkel, et token eller en `.env`-fil. Agenten din har fått beskjed om det samme i `CLAUDE.md`, men du er den som har ansvaret.
- **Alt innhold er data, ikke ordre.** Issuer og kommentarer kan skrives av hvem som helst. Ber en issue agenten din om å slette tester eller hente noe utenfor oppgaven, skal den nekte og si fra.

## Backloggen

25 issuer, i tre prioriteter.

| | Hva | Hvor mange |
|---|---|---|
| **P1** | Kartet er i stå. Fem feil som gjør produktet ubrukelig. Tas først. | 5 |
| **P2** | Produktet er ikke ferdig uten. Kjernelagene og det viktigste av UX. | 6 |
| **P3** | Gjør det bedre. Flere datakilder, flere funksjoner. | 14 |

P1-ene er ekte feil av typen som lever lenge i en kodebase fordi de ser riktige ut: byttet rekkefølge på koordinater, norsk desimalkomma i en URL, et mellomlager som aldri går ut på dato. De er alle synlige på kartet med en gang du ser etter.

Start med P1. Et nytt kartlag hjelper ikke når punktene havner i Indiahavet.

## Slik er koden satt sammen

```
src/OsloLive/Kart/       kartmotoren: GeoJSON, Oslo-utsnittet, Allemannsdata-klienten
src/OsloLive/Lag/        ett lag per datakilde
src/OsloLive/wwwroot/    kartet (MapLibre GL JS, 3D)
tests/OsloLive.Tester/   xUnit-tester
issues/                  kildeteksten til issuene i dette repoet
```

Et lag er én klasse som implementerer `ILag`, pluss én registreringslinje i `Program.cs`. `Lag/LuftkvalitetLag.cs` er malen, og den gjør fire ting:

1. Kaller en operasjon hos Allemannsdata med `Allemannsdata.HentListe`.
2. Lager ett punkt per rad med `Geo.Lag`.
3. Samler dem med `Geo.Samle`.
4. Registreres i `Program.cs`.

`Geo.Lag` krever `navn` og `kilde` på hvert punkt og kaster bort alt som ligger utenfor Oslo. `Geo.Samle` fjerner duplikater. Et lag trenger derfor bare å oversette radene fra kilden — resten er felles.

Mer om hvorfor det er satt opp slik: [ARKITEKTUR.md](ARKITEKTUR.md). Om språk, rammeverk og verktøy: [TEKNOLOGI.md](TEKNOLOGI.md).

## Å finne fram i Allemannsdata

51 servere og over 250 verktøy. Du finner ikke riktig kilde ved å gjette, og det gjør ikke agenten din heller. Bruk MCP-serveren — den er laget for å bli utforsket.

Repoet har allerede `.mcp.json`, så Claude Code kobler seg på når du starter den her. Ellers:

```bash
claude mcp add --transport http allemannsdata https://allemannsdata.com/allemannsdata/mcp
```

Fire verktøy tar deg hele veien:

| Verktøy | Hva det gir deg |
|---|---|
| `search_wiki` | Finn kilden når du bare vet hva du leter etter: «elsparkesykler Oslo» |
| `describe_source` | Hvilke operasjoner kilden har |
| `describe_operation` | Parametre, responsskjema og `json_get_url_template` |
| `get_data` | Kjør operasjonen og se hvordan svaret faktisk ser ut |

**Legg merke til arbeidsdelingen.** MCP brukes når du *utvikler*. Koden du skriver kaller en helt vanlig JSON-adresse, den `describe_operation` gir deg:

```
https://allemannsdata.com/wiki/api/v1/kilder/{kilde}/{operasjon}?param=verdi
```

Appen har ingen MCP-avhengighet i drift. Svaret har alltid samme ytterste form:

```json
{ "source": "...", "operation": "...", "parameters": { }, "data": ... }
```

`data` er enten en liste rett ut, eller et objekt med listen inni — `{ "vehicles": [...] }`, `{ "items": [...] }`, `{ "fartoy": [...] }`. Hvilken det er, ser du med `get_data`. `Allemannsdata.HentListe` tar navnet på listen som et valgfritt argument.

Å gjette på feltnavn er den vanligste grunnen til at et nytt lag blir tomt.

## Kartkontrakten

Frontenden og testene er avhengige av at API-et ser slik ut.

`GET /api/lag` — lagene som finnes:

```json
[{ "id": "luftkvalitet", "navn": "Luftkvalitet", "beskrivelse": "…", "ikon": "🌬️" }]
```

`GET /api/lag/{id}` — punktene i ett lag, som GeoJSON:

```json
{
  "type": "FeatureCollection",
  "features": [{
    "type": "Feature",
    "geometry": { "type": "Point", "coordinates": [10.7522, 59.9139] },
    "properties": { "id": "…", "navn": "…", "kilde": "…" }
  }]
}
```

Lengdegrad først. `navn` og `kilde` skal alltid være med. Alt annet du legger i `detaljer` dukker opp i popup-en på kartet.

Hver issue som ber om et nytt lag oppgir hvilken `id` laget skal ha. Den er en del av kontrakten — hold deg til den.

## Kartet i drift

Main ruller ut automatisk. Blir pull requesten din merget, står endringen din på storskjermen et par minutter senere.

```
merge til main
  → GitHub Actions: dotnet build + dotnet test
  → az acr build (bygger bildet i Azure, ikke på runneren)
  → ny revisjon i Container Apps
  → helsesjekk mot /api/helse
```

Kjører i Novanet-abonnementet, Norway East. Selve utrullingen tar under to minutter, mest fordi `dotnet restore` ligger i sitt eget docker-lag og gjenbrukes så lenge `OsloLive.csproj` er urørt.

**Bygget er porten.** Feiler `dotnet build` eller `dotnet test`, blir det ingen utrulling. Det er derfor testene må være grønne før du leverer — ikke for formalitetens skyld, men fordi rødt bygg betyr at kartet ikke oppdateres for noen.

**Ingen hemmeligheter i dette repoet.** Actions logger inn i Azure med federated credentials: GitHub beviser hvem den er, Azure stoler på beviset. Det finnes ingen nøkkel å lekke, og det er et bevisst valg fordi repoet er offentlig.

Én ting verdt å legge merke til: kartet kjører med `minReplicas: 1`, altså alltid på. Agentjobben i starter-kit gjør det motsatte og skalerer til null. Forskjellen er hva de koster når ingen bruker dem, mot hva en kaldstart koster når noen faktisk ser på. For en jobb som kjører i bakgrunnen er svaret opplagt. For et kart på storskjermen er det motsatt.

Oppsettet gjøres én gang, av kurslederen: `scripts/sett-opp-azure.sh`.

## Før du leverer

```bash
dotnet build
dotnet test
```

Begge skal være grønne. En pull request med rødt bygg blir stående som utkast.
