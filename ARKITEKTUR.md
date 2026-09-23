# Arkitektur

Struktur og kontrakter. Les før du legger til lag eller endepunkter.

## Struktur

```
src/OsloLive/
  Program.cs              kultur nb-NO, DI, endepunkter, laglisten
  Kart/
    Geo.cs                GeoJSON-typer, utsnitt, Punkt(), IOslo(), Lag(), Samle()
    Allemannsdata.cs      HTTP-klient mot Allemannsdata, mellomlager 30 s
    ILag.cs               kontrakten et lag oppfyller
  Lag/
    LuftkvalitetLag.cs    mal for nye lag
    FlyLag.cs             unntak: egen kilde (airplanes.live), se «Unntak: flylaget»
  Historikk/
    Bildelager.cs         øyeblikksbilder på disk, nærmeste bilde, sletting etter sju dager
    Øyeblikksjobb.cs      bakgrunnsjobb: bilde av hvert lag hver time, se «Historikk og tidslinjen»
  wwwroot/index.html      hele frontenden, én fil, ingen byggesteg
tests/OsloLive.Tester/    xUnit. ApiTester.cs (WebApplicationFactory), KartTester.cs (Geo, Allemannsdata)
issues/                   issuetekstene. Ikke rør.
```

Prinsipp: kartet er en liste med lag. `ILag → Kartlag (GeoJSON) → MapLibre`. Et lag kjenner bare sin egen kilde. Nytt lag = én fil i `Lag/` + én linje i `Program.cs`.

## Endepunkter

| Rute | Svar |
|---|---|
| `GET /api/lag` | `[{ id, navn, beskrivelse, ikon }]` |
| `GET /api/lag/{id}` | `Kartlag` som GeoJSON `FeatureCollection`. 404 ved ukjent id. 502 `{ feil }` hvis laget kaster; de andre lagene påvirkes ikke. Med `?tid=` (ISO 8601) svares det med bildet lagret nærmest det tidspunktet i stedet for levende data; tom `FeatureCollection` hvis ingen bilde er innenfor to timer, 400 ved ugyldig `tid`. Se `Historikk/`. |
| `GET /api/lag/{id}/bydeler` | `[{ bydel, antall }]`, antall punkter i laget per bydel, sortert synkende. Bydel = nærmeste bydelssenter fra Kartverket; punkter lenger enn 5 km fra alle sentre utelates. 404 ved ukjent id, 502 `{ feil }` hvis laget eller oppslaget svikter. Én oppdatering gjør 9 kall mot Allemannsdata (ett per forbokstav i `Bydeler.Prefikser`), uavhengig av antall punkter, aldri ett kall per punkt. Svarene mellomlagres 30 s som alt annet. Med `?tid=` telles bildet lagret nærmest tidspunktet, som for `/api/lag/{id}`, så tellingen følger tidslinjen. |
| `GET /api/stroempris` | Strømprisen i Oslo (NO1) i dag: `{ naa, billigst: { time, pris }, dyrest: { time, pris }, timer: [{ time, pris }, …] }`, øre/kWh inkl. mva. 502 `{ feil }` hvis kilden svikter. |
| `GET /api/helse` | `{ status: "ok", tid }` |
| `GET /api/helse` | `{ status: "ok", tid }`. Lever prosessen? Ingen kall til kildene, svarer alltid umiddelbart. |
| `GET /api/helse/kilder` | `{ status: "ok"\|"degradert", kilder: [{ kilde, status: "ok"\|"feil"\|"ukjent", sistSjekket, varighetMs }] }`. Virker tjenesten? Leser siste resultat fra `HelseSjekker`, en bakgrunnstjeneste som sjekker hvert lags kilde med et intervall satt i `appsettings.json` (`Helse:IntervallSekunder`, standard 60). Venter aldri på kildene i selve forespørselen. |

Frontenden henter `/api/lag` ved oppstart og hvert lag hvert 15. sekund. Et lag som svarer 502 markeres rødt i lagvelgeren.

## Kontrakten for et lag

```csharp
public interface ILag
{
    string Id { get; }            // små bokstaver, brukes i /api/lag/{id}, gitt i issuen
    string Navn { get; }          // vises i lagvelgeren
    string Beskrivelse { get; }   // én setning
    string Ikon { get; }          // én emoji
    Task<Kartlag> Hent(CancellationToken stopp = default);
}
```

Krav til implementasjonen:

- Registrert som singleton. Ingen muterbar tilstand i klassen.
- Ta imot `Allemannsdata` via primærkonstruktør. Ikke lag egen `HttpClient`.
- Send `CancellationToken` videre til `Allemannsdata`.
- Bygg punkter med `Geo.Lag(...)` og returner `Geo.Samle(...)`. Ikke filtrer på utsnitt selv; `Geo.Lag` returnerer `null` utenfor Oslo og `Geo.Samle` fjerner nullene.
- Kaster kilden, la det kaste (laget blir rødt). Henter du fra flere kilder og én svikter, returner det du har. Begrunn valget i PR-teksten.

## Geo

```csharp
Geo.MinLat = 59.80; Geo.MaksLat = 60.14; Geo.MinLon = 10.45; Geo.MaksLon = 10.98;
Geo.OsloLat = 59.9139; Geo.OsloLon = 10.7522;
Geo.Punkt(lat, lon)                 // GeoJSON-geometri, [lon, lat]-rekkefølge
Geo.IOslo(lat, lon)                 // true når både lat og lon er innenfor utsnittet
Geo.Lag(id, lat, lon, navn, kilde, detaljer)   // Kartpunkt, eller null utenfor utsnittet
Geo.Samle(punkter)                  // Kartlag uten null og uten duplikater på id
```

GeoJSON har lengdegrad først: `[lon, lat]`. Bruk alltid `Geo.Punkt`. Utsnittet endres ikke i en vanlig oppgave; det finnes tester på grensene.

## Allemannsdata

```
GET https://allemannsdata.com/wiki/api/v1/kilder/{kilde}/{operasjon}?param=verdi
Svar: { "source", "operation", "parameters", "data": <liste eller objekt med liste> }
```

```csharp
data.HentListe(kilde, operasjon, parametre, liste: "<navn på listen i data>" | null, stopp)
data.Hent(kilde, operasjon, parametre, stopp)      // rå JsonElement fra "data"
Allemannsdata.ByggUrl(kilde, operasjon, parametre) // formaterer tall med InvariantCulture
```

- Ingen API-nøkkel. Ingen egen `HttpClient`.
- Mellomlager 30 sekunder per adresse. Ikke omgå det. Trenger du ferskere data, skriv det i PR-en.
- Tallparametre må ut som `59.91`, ikke `59,91`. Appen kjører med `nb-NO`; `ByggUrl` håndterer det, egen strengbygging må bruke `CultureInfo.InvariantCulture`.
- Finn `kilde`, `operasjon`, parametre og feltnavn med MCP-serveren `allemannsdata` før du skriver kode.

## Unntak: flylaget (airplanes.live)

`FlyLag` bryter regelen om at lag skal bruke `Allemannsdata`. Issue #56 krever at
bruddet begrunnes med svar på tre spørsmål. Svarene står her, ved siden av regelen.

**Hvorfor akkurat denne kilden?** Allemannsdata har ingen kilde med flyposisjoner.
Kilden `avinor` gir rutetider og status per flyplass, uten lat/lon, og ingen annen
kilde i wikien gir posisjoner for fly. airplanes.live er et åpent ADS-B-nettverk
drevet av entusiaster. Endepunktet `/v2/point/{lat}/{lon}/{nm}` gir fly innenfor en
radius, med feltene laget trenger: `hex`, `flight`, `lat`, `lon`, `alt_baro` (tall,
eller `"ground"` for fly på bakken) og `gs`. Ingen registrering, ingen nøkkel.

**Hva koster den? Krever den nøkkel, og har den et tak på antall kall?** Gratis og
nøkkelfri, så ingen hemmelighet ligger i repoet eller i miljøet. Kilden ber om maks
ett kall i sekundet per IP. `FlyLag` mellomlagrer svaret i 30 sekunder i `IMemoryCache`,
så kartet gjør maksimalt to kall i minuttet uansett hvor mange som ser på det.
HttpClient-en `fly` har 10 sekunders tidsavbrudd.

**Hva skjer den dagen kilden er nede eller avviser oss?** `FlyLag.Hent` kaster, og
`/api/lag/{id}` i `Program.cs` gjør det om til 502 for `/api/lag/fly` alene. `/api/lag`
og de andre lagene svarer 200, og lagvelgeren viser flylaget som rødt. Testen
`Svikt_i_flykilden_gir_502_bare_for_flylaget` i `ApiTester.cs` bytter ut flylagets
HttpClient med en som alltid feiler og bekrefter dette. Rader uten posisjon eller
`hex` forkastes én og én i `TilPunkt`, så én dårlig rad feller ikke laget.

Bruddet er isolert: `FlyLag` har egen navngitt `HttpClient` («fly») og eget mellomlager,
og rører verken `Allemannsdata` eller de andre lagene. Kartutsnittet i `Geo` er ikke
utvidet til Gardermoen; det er en egen beslutning om hva «Oslo Live» skal dekke.

## Historikk og tidslinjen

`Historikk/Øyeblikksjobb` tar et bilde (`Kartlag` som JSON) av hvert registrerte lag én gang i timen, første gang ved oppstart, og legger det i `Historikk:Mappe/<lagId>/<yyyyMMddTHHmmssZ>.json`. `Bildelager` velger bildet nærmest `?tid=` (maks to timer unna) og sletter bilder eldre enn sju dager. Et lag som svikter, eller et tidsavbrudd mot kilden, logges og hoppes over; jobben stopper bare når verten selv stopper.

Konfigurasjon i `appsettings.json`:

- `Historikk:Mappe`: hvor bildene ligger. Tom verdi betyr `App_Data/historikk` under appens rotmappe (`/app/App_Data/historikk` i containeren). Mappa er ikke i git.
- `Historikk:Jobb`: `false` skrur jobben av. Testene gjør det via `TestVert`.

Standardmappa ligger i containerens eget filsystem. Den overlever en omstart av prosessen, men i Container Apps hører filsystemet til replikaen, så etter en ny revisjon starter tidslinjen tom til jobben har tatt nye bilder. Skal historikken overleve en utrulling, monter et volum (for eksempel Azure Files) på `/app/App_Data/historikk`, eller pek `Historikk:Mappe` (miljøvariabelen `Historikk__Mappe`) på volumet. `infra/kart.bicep` monterer ikke noe volum i dag. Dockerfile lager mappa med rettigheter for brukeren `app`, som kjørebildet kjører som.

## Frontend

`wwwroot/index.html`: HTML, CSS og JavaScript i én fil. MapLibre GL JS 4.7.1 fra cdnjs (pinnet), vektorfliser fra OpenFreeMap (stil `liberty`), terreng fra Mapterhorn. Kartet står i 3D med `pitch: 55`. Farger og terreng settes i `varmTema(kart)`; utseendeendringer gjøres der. Punkter tegnes som HTML-markører per lag; popup viser `navn` og alle `detaljer` unntatt `id`, `navn`, `kilde`. Nye script lastes fra cdnjs med pinnet versjon.

## Samtidige agenter

Flere agenter jobber i repoet samtidig. Derfor:

- Minste mulige diff. Ikke rør filer oppgaven ikke krever.
- Workeren sjekker før start om en annen agent er i gang med issuen eller om den er tildelt noen andre, og lar den i så fall ligge. Ender to agenter likevel med PR på samme issue, merges den som virker og kom først.
- Ser du en åpen PR fra en annen agent på issuen din som løser den: si fra i rapporten, ikke lever en konkurrerende PR.
