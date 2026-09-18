Hvert lag viser bare ett eneste punkt
bug,P1,2 poeng

Luftkvalitetslaget henter 18 målestasjoner fra kilden, men det tegnes bare én stasjon på kartet. Telleren i lagvelgeren viser «1». Det samme skjer for alle lag vi legger til.

**Slik ser du det**

1. Åpne kartet. Luftkvalitet viser 1 punkt.
2. Kall `/api/lag/luftkvalitet` direkte. Det er også bare én `feature` i svaret.

**Mål**

Alle punktene et lag henter kommer med på kartet. Duplikatfjerningen i `Geo.Samle` fjerner bare det som faktisk er samme punkt to ganger, ikke alt som kommer fra samme kilde.

**Akseptansekriterier**

- `Geo.Samle` med tre punkter som har ulik `id` men samme `kilde` gir en `FeatureCollection` med 3 `features`.
- `Geo.Samle` med to punkter som har samme `id` gir 1 `feature`. Ekte duplikater fjernes fortsatt.
- `GET /api/lag/luftkvalitet` svarer med flere enn ett punkt når kilden svarer, og telleren i lagvelgeren viser det samme tallet.
- Hvert punkt har fortsatt `id`, `navn` og `kilde` i `properties`.
- En ny enhetstest i `tests/OsloLive.Tester` dekker begge tilfellene over på `Geo.Samle`, uten å gå mot nettet.
- `dotnet build` og `dotnet test` er grønne. Ingen eksisterende tester er endret eller fjernet.
