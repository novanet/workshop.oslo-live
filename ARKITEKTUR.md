# Arkitektur

Hvorfor Oslo Live er satt sammen som det er, og hva du bør holde deg til når du utvider det.

Les dette før du legger til noe større enn ett lag. Agenten din har fått den korte versjonen i `CLAUDE.md`.

## Én idé: kartet er en liste med lag

Hele produktet er én abstraksjon:

```
ILag  →  Kartlag (GeoJSON)  →  MapLibre
```

Et lag vet én ting: hvordan hente sine egne punkter. Det vet ingenting om de andre lagene, om kartet, eller om hvordan det blir tegnet. Legger du til et lag, rører du to filer: din egen i `Lag/`, og én linje i `Program.cs`.

Det er med vilje. Titalls agenter jobber i dette repoet samtidig. Jo mindre overflate et bidrag trenger å ta på, jo færre konflikter blir det. **Et lag som må endre `Kart/` for å virke, er et lag som er designet feil.**

```
src/OsloLive/
  Program.cs              oppsett, endepunkter, laglisten
  Kart/
    Geo.cs                GeoJSON-typene, Oslo-utsnittet, Lag() og Samle()
    Allemannsdata.cs      HTTP-klienten mot datakildene, med mellomlager
    ILag.cs               kontrakten et lag oppfyller
  Lag/
    LuftkvalitetLag.cs    malen. Kopier denne.
  wwwroot/index.html      kartet
```

## Lagene

```csharp
public interface ILag
{
    string Id { get; }            // brukes i /api/lag/{id}
    string Navn { get; }          // vises i lagvelgeren
    string Beskrivelse { get; }
    string Ikon { get; }
    Task<Kartlag> Hent(CancellationToken stopp = default);
}
```

Et lag er registrert som singleton og deler `Allemannsdata`-klienten. Det betyr:

- **Lag ingen tilstand i laget.** Det kalles fra flere forespørsler samtidig. Felter som endrer seg mellom kall er en feil som først viser seg på storskjermen.
- **Ta imot `CancellationToken` og send den videre.** Lukker noen fanen midt i et treigt kall, skal det stoppe.
- **Ikke lag din egen `HttpClient`.** Bruk `Allemannsdata`. Den har mellomlager, tidsavbrudd og riktig `User-Agent` allerede.

### Feil i et lag skal ikke ta ned kartet

`/api/lag/{id}` fanger opp alt et lag kaster og svarer 502 for akkurat det laget. Resten av kartet lever videre. Frontenden markerer laget rødt i lagvelgeren.

Det gir deg et valg når en kilde er ustabil, og valget er ditt å begrunne:

| Du velger | Konsekvens |
|---|---|
| La det kaste | Laget blir rødt. Ærlig, men synlig for alle på storskjermen. |
| Fang og returner tomt | Kartet ser rolig ut, men du skjuler at kilden er nede. |
| Fang, returner det du har | Best når du henter fra flere kilder og bare én svikter. |

NOBIL (issue 11) svarer med 502 fra tid til annen, og den issuen handler egentlig om dette valget.

## Utsnittet og koordinatene

To ting har bitt oss før, og begge er kodet inn i `Geo`:

**GeoJSON har lengdegrad først.** `[lon, lat]`, ikke `[lat, lon]`. Dette er den vanligste feilen i kartkode som finnes, og den er lett å overse fordi begge tallene ser ut som koordinater. Bruk alltid `Geo.Punkt`.

**Kartet dekker Oslo og indre Oslofjord.** `Geo.Lag` returnerer `null` for alt utenfor, og `Geo.Samle` siler bort nullene. Det betyr at et lag trygt kan be kilden om et vidt område — filtreringen er felles, og du skal ikke gjenta den i laget ditt.

```csharp
public const double MinLat = 59.80, MaksLat = 60.14;
public const double MinLon = 10.45, MaksLon = 10.98;
```

Trenger du et punkt utenfor boksen, er det en diskusjon i en issue, ikke en endring du gjør i forbifarten.

## Mellomlageret

`Allemannsdata` mellomlagrer hvert svar i 30 sekunder, med adressen som nøkkel.

Det er ikke en ytelsesoptimalisering — det er hensyn til kildene. Ti personer med kartet oppe, som henter hvert 15. sekund, med et titalls lag, blir fort mange hundre kall i minuttet mot offentlige API-er som ingen tar betalt for. Mellomlageret gjør det til en håndfull.

Trenger du ferskere data enn 30 sekunder, snakk med noen først. Trenger du å hente noe tungt sjelden, er det en annen sak — men skriv det i PR-en.

## Å legge til noe som ikke er et lag

Noen issuer ber om ting som ikke passer i `ILag` — adressesøk, historikk, telling per bydel. Da:

- Legg endepunktet i `Program.cs` ved siden av de andre.
- Hold det på samme form: `/api/<substantiv>`, JSON ut, ingen tilstand.
- Frontendendringer hører hjemme i `wwwroot/index.html`. Den er med vilje én fil uten byggesteg — ingen npm, ingen bundler. Det holder for det vi driver med, og det gjør at en agent kan endre den uten å sette opp en verktøykjede først. Kartet er MapLibre GL JS med vektorfliser fra OpenFreeMap, og står i 3D med bygninger.

## Gode vaner i akkurat dette repoet

**Gjør den minste endringen som løser issuen.** Repoet har mange samtidige bidragsytere. Omformatering, opprydding og «mens jeg først var inne» er hvordan du garantert får konflikt med noen andre.

**Ikke svekk en test for å få den grønn.** Feiler en test etter endringen din, er det endringen som skal vurderes. Dette er også regel nummer én i `CLAUDE.md`, fordi det er den mest fristende snarveien for en agent som står fast.

**Skriv testen der feilen kunne oppstått igjen.** De fem P1-feilene er alle av typen som kan snike seg inn på nytt. En test på `Geo` eller `Allemannsdata` er verdt mer enn en test som går mot nettet.

**Nettverket hører ikke hjemme i en test som må være grønn.** Testene i `tests/` skal kunne kjøres på et tog. Vil du teste at et lag faktisk henter data, gjør det for hånd med appen kjørende.

**Norsk i kode og tekst.** Klassenavn, medlemmer, kommentarer, commit-meldinger og PR-tekst. Det er ikke en smakssak her — det er slik resten av koden ser ut, og blandingsspråk blir fort stygt.

## Når to agenter har gjort det samme

Det kommer til å skje. Når det gjør det:

- Den PR-en som virker, vinner. Ikke den som kom først.
- Er begge grønne, vinner den minste diffen.
- Den andre lukkes med en kommentar om hvorfor. Ikke bare slett den — begrunnelsen er poenget.

Merker du at noen allerede har en åpen PR på issuen du er på vei inn i, er det billigere å finne en annen issue enn å kappes.
