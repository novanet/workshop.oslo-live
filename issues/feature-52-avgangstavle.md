Avgangstavle for Gardermoen
enhancement,P3,3 poeng

Vi vil se hvilke fly som går fra og lander på Gardermoen den nærmeste timen, uten å forlate kartet.

**Krav**

- `GET /api/avganger` gir de neste avgangene og ankomstene.
- Et panel viser flynummer, flyselskap, hvor flyet skal eller kommer fra, planlagt tid og status.
- Forsinkede fly skal være tydelig merket.
- Panelet skal kunne slås av og på.

**Datakilde**

Avinor ligger i Allemannsdata og dekker alle norske lufthavner. Bruk `describe_source` for å finne operasjonen, og `get_data` for å se feltene før du skriver koden.

Dette er ikke et kartlag — det er en tavle. `ILag` passer dårlig; legg et eget endepunkt i `Program.cs`, som i issuen om adressesøk.

Merk at flykoder er koder. `OSL` er Gardermoen og `DY` er Norwegian. Skal tavlen være til å lese, må du slå dem opp — og Avinor-kilden kan hjelpe med begge deler.
