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
  wwwroot/index.html      hele frontenden, én fil, ingen byggesteg
tests/OsloLive.Tester/    xUnit. ApiTester.cs (WebApplicationFactory), KartTester.cs (Geo, Allemannsdata)
issues/                   issuetekstene. Ikke rør.
```

Prinsipp: kartet er en liste med lag. `ILag → Kartlag (GeoJSON) → MapLibre`. Et lag kjenner bare sin egen kilde. Nytt lag = én fil i `Lag/` + én linje i `Program.cs`.

## Endepunkter

| Rute | Svar |
|---|---|
| `GET /api/lag` | `[{ id, navn, beskrivelse, ikon }]` |
| `GET /api/lag/{id}` | `Kartlag` som GeoJSON `FeatureCollection`. 404 ved ukjent id. 502 `{ feil }` hvis laget kaster; de andre lagene påvirkes ikke. |
| `GET /api/helse` | `{ status: "ok", tid }` |

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

## Frontend

`wwwroot/index.html`: HTML, CSS og JavaScript i én fil. MapLibre GL JS 4.7.1 fra cdnjs (pinnet), vektorfliser fra OpenFreeMap (stil `liberty`), terreng fra Mapterhorn. Kartet står i 3D med `pitch: 55`. Farger og terreng settes i `varmTema(kart)`; utseendeendringer gjøres der. Punkter tegnes som HTML-markører per lag; popup viser `navn` og alle `detaljer` unntatt `id`, `navn`, `kilde`. Nye script lastes fra cdnjs med pinnet versjon.

## Samtidige agenter

Flere agenter jobber i repoet samtidig. Derfor:

- Minste mulige diff. Ikke rør filer oppgaven ikke krever.
- Workeren sjekker før start om en annen agent er i gang med issuen eller om den er tildelt noen andre, og lar den i så fall ligge. Ender to agenter likevel med PR på samme issue, merges den som virker og kom først.
- Ser du en åpen PR fra en annen agent på issuen din som løser den: si fra i rapporten, ikke lever en konkurrerende PR.
