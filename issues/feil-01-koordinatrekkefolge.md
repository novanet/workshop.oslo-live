Alle punkter havner i havet, langt utenfor Norge
bug,P1,2 poeng

Zoomer du ut på kartet, ligger punktene våre midt i Indiahavet i stedet for i Oslo. Selve dataene ser riktige ut: luftkvalitetsstasjonene har fornuftige koordinater når jeg kaller `/api/lag/luftkvalitet` og leser tallene. Det er plasseringen på kartet som er feil.

**Slik ser du det**

1. Start appen og åpne `http://localhost:5199`.
2. Zoom helt ut.
3. Punktene ligger i havet sørøst for Afrika.

**Mål**

Alle punkter tegnes der koordinatene fra kilden sier de er, altså i Oslo. Feilen kan ikke snike seg inn igjen uten at en test blir rød.

**Akseptansekriterier**

- `Geo.Punkt(59.9139, 10.7522)` gir `Coordinates` lik `[10.7522, 59.9139]`: lengdegrad først, breddegrad sist, slik GeoJSON-standarden krever.
- I svaret fra `GET /api/lag/luftkvalitet` ligger `coordinates[0]` mellom 10.45 og 10.98 og `coordinates[1]` mellom 59.80 og 60.14 for alle punkter.
- Åpner du kartet, ligger luftkvalitetspunktene i Oslo. Ingen punkter utenfor Norge.
- Signaturen `Geo.Lag(id, lat, lon, navn, kilde, detaljer)` er uendret, så lagene som kaller den trenger ingen endring.
- En ny enhetstest i `tests/OsloLive.Tester` sjekker rekkefølgen på koordinatene fra `Geo.Punkt` eller `Geo.Lag`. Den er rød før fiksen og grønn etter, og går ikke mot nettet.
- `dotnet build` og `dotnet test` er grønne. Ingen eksisterende tester er endret eller fjernet.

Kartet leser GeoJSON. Sjekk hva GeoJSON-standarden sier om rekkefølgen på koordinatene i et `Point`.
