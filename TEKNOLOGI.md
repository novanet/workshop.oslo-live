# Teknologi og kodestil

## Stakk

| Del | Valg |
|---|---|
| Språk | C# 13 på .NET 10. `Nullable` og `ImplicitUsings` er på. |
| Web | ASP.NET Core minimal API i `Program.cs`. Ingen controllere. |
| JSON | `System.Text.Json`. Ingen Newtonsoft. |
| HTTP | `HttpClient` via `AddHttpClient<Allemannsdata>`. |
| Kart | MapLibre GL JS 4.7.1 fra cdnjs. OpenFreeMap-fliser, Mapterhorn-terreng. Ingen npm. |
| Tester | xUnit + `WebApplicationFactory<Program>`. |
| Data | Allemannsdata, JSON over HTTP. MCP-serveren `allemannsdata` brukes bare til å utforske. |

Ingen database. Ingen autentisering. Ingen NuGet-pakker utover .NET og xUnit. Ny pakke krever at issuen ber om det, og en begrunnelse i PR-teksten.

## Kommandoer

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
dotnet run --project src/OsloLive --urls http://localhost:5199
```

Ingen `npm install`, ingen migreringer, ingen containere lokalt.

## C#-stil

Mal for et lag:

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

Regler:

- Primærkonstruktør for avhengigheter. Ingen felter, ingen konstruktørkropp.
- `sealed` på klasser som ikke skal arves.
- Uttrykkskropp `=>` for egenskaper og korte metoder.
- Samlingsuttrykk `[...]` der det passer.
- Norske navn på klasser, medlemmer, lokale variabler og tester.
- `async`/`await` hele veien. Aldri `.Result` eller `.Wait()`.
- Feltnavn i kildesvar er engelske slik kilden gir dem. Nøkler i `detaljer` er norske; de vises i popupen.

## JSON fra kildene

```csharp
var lat  = rad.GetProperty("lat").GetDouble();                                   // påkrevd: skal kaste hvis feltet mangler
var fart = rad.TryGetProperty("fart_knop", out var f) ? f.GetDouble() : (double?)null;   // valgfritt
var navn = rad.TryGetProperty("navn", out var n) ? n.GetString() ?? "Ukjent" : "Ukjent"; // tekst som kan være null
```

Koordinater og id er påkrevd. Alt annet er valgfritt.

## Tall og kultur

`Program.cs` setter `nb-NO`. `(59.9139).ToString()` gir `"59,9139"`. Alle tall som skal i en URL, en fil eller ut på nettet formateres med `CultureInfo.InvariantCulture`. `Allemannsdata.ByggUrl` gjør det; egen strengbygging må gjøre det selv.

## Tester

```csharp
[Fact]
public void Punkt_utenfor_utsnittet_blir_forkastet()
{
    var punkt = Geo.Lag("id", 61.115, 10.466, "Lillehammer", "Test");

    Assert.Null(punkt);
}
```

- Prosjekt: `tests/OsloLive.Tester`. `KartTester.cs` for `Geo` og `Allemannsdata`, `ApiTester.cs` for endepunkter via `WebApplicationFactory<Program>`.
- Testnavn på norsk: `Hva_som_skjer_naar_noe_er_tilfelle`.
- Arrange, act, assert skilt med blank linje. Én ting per test. `[Theory]` + `[InlineData]` for flere verdier.
- Ingen nettverkskall i tester. Test funksjonen som oversetter en rad, ikke hentingen.
- `public partial class Program;` nederst i `Program.cs` skal stå. Testprosjektet trenger den.
- Bugfiks skal ha en test som feiler før og passerer etter.

## Frontend

`wwwroot/index.html` er én fil uten byggesteg. Endringer gjøres direkte i den. Eksterne script lastes fra cdnjs med pinnet versjon. Kartutseende settes i `varmTema(kart)`. Markører per lag: `.merke` med lagets farge fra `FARGER` i samme fil; nye lag får farge fra `RESERVE` automatisk, og kan legges til i `FARGER` med lagets `Id` som nøkkel.

## Før levering

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```

Begge grønne. Rødt bygg gir PR som utkast og trekk på resultattavlen.
