Nytt lag: togtrafikk
enhancement,P3

Vi har busser og trikker. Nå vil vi ha togene også — både persontog og godstog gjennom Oslo.

**Krav**

- Laget skal ha id `tog`, navn «Tog» og et passende ikon.
- Ett punkt per tog med kjent posisjon eller siste kjente stasjon.
- Punktene skal vise togets nummer, hvor det kommer fra og hvor det skal.

**Datakilde**

Bane NOR har en åpen SIRI-feed i Allemannsdata. Bruk `describe_source` og `get_data` for å se hva den faktisk gir deg.

Merk at kollektivlaget kan ha togene allerede, avhengig av hvordan det er skrevet. Sjekk før du dublerer — og hvis de overlapper, skriv i PR-en hvordan du skiller dem.
