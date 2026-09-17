# Teknologi

Hva Oslo Live er bygget med, og hvorfor. Kort nok til at en agent kan lese hele fila før den begynner.

## Stakken

| Del | Valg | Hvorfor |
|---|---|---|
| Språk | C# 13 på .NET 10 | Novanet-stakken. Nullable og `ImplicitUsings` er på. |
| Web | ASP.NET Core minimal API | Hele API-et er 40 linjer i `Program.cs`. Ingen controllere, ingen MVC. |
| JSON | `System.Text.Json` | Følger med. Ingen Newtonsoft. |
| HTTP | `HttpClient` via `AddHttpClient<T>` | Riktig livssyklus på socketene uten at du trenger tenke på det. |
| Kart | Leaflet 1.9.4 fra CDN | Én `<script>`-tag. Ingen npm, ingen bundler, ingen byggesteg for frontend. |
| Kartfliser | OpenStreetMap, mørklagt i CSS | Ingen API-nøkkel. Merk: CARTO krever nøkkel nå — derfor ikke dem. |
| Tester | xUnit + `WebApplicationFactory` | Standard i .NET. Fabrikken starter hele appen i minnet. |
| Data | [Allemannsdata](https://allemannsdata.com) | Norske offentlige data. MCP for å utforske, vanlig JSON over HTTP i drift. |

Ingen database. Ingen autentisering. Ingen pakker utover det .NET og xUnit gir deg.

**Ikke legg til en NuGet-pakke uten at issuen ber om det.** Repoet skal kunne bygges av tolv personer samtidig uten at noen bruker Lab 1 på å løse pakkekonflikter. Trenger du noe som ikke finnes i rammeverket, skriv i PR-en hvorfor, og regn med å bli spurt.

## Kommandoene

```bash
dotnet build                                                  # bygger alt
dotnet test                                                   # kjører testene
dotnet run --project src/OsloLive --urls http://localhost:5199  # starter kartet
```

Ingenting annet trengs. Det er ingen `npm install`, ingen migreringer, ingen containere for å utvikle lokalt.

## C#-stilen i dette repoet

Koden er skrevet på norsk, og det gjelder også nye bidrag. Det er ikke pynt — resten av kodebasen ser slik ut, og halvveis engelsk blir stygt fort.

```csharp
public sealed class BadetemperaturLag(Allemannsdata data) : ILag
{
    public string Id => "badetemperatur";
    public string Navn => "Badetemperatur";
    public string Beskrivelse => "Målte badetemperaturer i og rundt Oslo.";
    public string Ikon => "🌡️";

    public async Task<Kartlag> Hent(CancellationToken stopp = default)
    {
        var rader = await data.HentListe(
            "badetemp",
            "get_nearest_water_temperatures",
            new Dictionary<string, object>
            {
                ["lat"] = Geo.OsloLat,
                ["lon"] = Geo.OsloLon,
                ["limit"] = 50,
            },
            liste: "temperatures",
            stopp);

        return Geo.Samle(rader.Select(rad => Geo.Lag(
            id: rad.GetProperty("location_id").GetString() ?? "",
            lat: rad.GetProperty("lat").GetDouble(),
            lon: rad.GetProperty("lon").GetDouble(),
            navn: rad.GetProperty("name").GetString() ?? "Ukjent",
            kilde: "Badetemperaturer fra Yr",
            detaljer: new Dictionary<string, object?>
            {
                ["grader"] = rad.GetProperty("temperature_c").GetDouble(),
                ["målt"] = rad.GetProperty("time").GetString(),
            })));
    }
}
```

Det som er verdt å legge merke til:

- **Primærkonstruktør** for avhengigheter: `class XLag(Allemannsdata data)`. Ingen felter, ingen konstruktørkropp.
- **`sealed`** på alt som ikke er ment å arves fra.
- **Uttrykkskropp** (`=>`) for egenskaper og korte metoder.
- **Samlingsuttrykk** (`[...]`) der det passer.
- **Norske navn** på klasser, medlemmer og lokale variabler.
- **`async`/`await` hele veien.** Aldri `.Result` eller `.Wait()` — det låser tråden i en webapp.

### Å lese JSON trygt

Datakildene endrer seg, og et felt som var der i går kan mangle i dag. `GetProperty` på noe som ikke finnes kaster.

```csharp
// Påkrevd: la det kaste hvis kilden har endret seg fundamentalt.
var lat = rad.GetProperty("lat").GetDouble();

// Valgfritt: fall tilbake.
var fart = rad.TryGetProperty("fart_knop", out var f) ? f.GetDouble() : (double?)null;

// Tekst som kan være null i JSON:
var navn = rad.TryGetProperty("navn", out var n) ? n.GetString() ?? "Ukjent" : "Ukjent";
```

Regelen: **koordinater og id er påkrevd, resten er valgfritt.** Et punkt uten posisjon er ikke et punkt. Et punkt uten fart er bare et punkt vi vet mindre om.

### Tall og kultur

Appen kjører med norsk kultur (`nb-NO`), satt i `Program.cs`. Det er med vilje — den er norsk.

Det betyr at `(59.9139).ToString()` gir `"59,9139"` med komma. I en URL er det feil. **Alle tall som skal ut på nettet formateres med `CultureInfo.InvariantCulture`.** `Allemannsdata.ByggUrl` gjør dette for deg; gjør du strengbygging selv, må du huske det.

Dette er ikke et konstruert eksempel — det er en av de fem P1-feilene, fordi det er den typen feil som virker på utviklerens maskin og ryker i produksjon.

## Testene

```csharp
[Fact]
public void Punkt_utenfor_utsnittet_blir_forkastet()
{
    var punkt = Geo.Lag("id", 61.115, 10.466, "Lillehammer", "Test");

    Assert.Null(punkt);
}
```

- Testnavn på norsk: `Hva_som_skjer_naar_noe_er_tilfelle`.
- Arrange, act og assert skilt med blank linje.
- Én ting per test.
- `[Theory]` med `[InlineData]` når du tester det samme med flere verdier.
- **Ingen nettverkskall.** Testene skal kunne kjøres uten internett. Skal du teste et lag, test funksjonen som oversetter en rad — ikke hentingen.

`WebApplicationFactory<Program>` starter appen i minnet for API-testene. `Program` er gjort synlig for testprosjektet med `public partial class Program;` nederst i `Program.cs` — ikke fjern den linjen.

## Frontenden

`wwwroot/index.html` er én fil: HTML, CSS og JavaScript i samme dokument, uten byggesteg.

Det er et bevisst valg. En agent kan endre den uten å sette opp node, og du kan lese hele frontenden på to minutter. Vokser den ut av det, er det en issue verdt å ta — ikke noe du gjør på si.

Leaflet lastes fra `unpkg.com`. Trenger du et Leaflet-tillegg (issue 17 om klynging, for eksempel), last det fra samme sted og pin versjonen.

## Verifisering før du leverer

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```

Begge grønne. En pull request med rødt bygg blir stående som utkast, og teller negativt på resultattavlen.
