Alle kall mot Allemannsdata feiler på norske maskiner
bug,P1

På maskinen min er hvert eneste lag rødt, og loggen viser at kallene mot Allemannsdata svarer med feil. På en kollegas engelskspråklige maskin virker akkurat den samme koden.

**Slik ser du det**

Adressen som bygges ser slik ut:

```
.../kilder/luftkvalitet/get_air_quality_nearby?lat=59%2C9139&lon=10%2C7522&limit=50
```

`59,9139` med komma. API-et forventer punktum.

**Forventet**

Tall i en URL skal skrives på samme måte uansett hvilket språk maskinen kjører med. Appen skal fortsatt kjøre med norsk kultur ellers, så ikke fjern kulturoppsettet i `Program.cs`.
