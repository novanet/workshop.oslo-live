Nytt lag: skoler og barnehager
enhancement,P3,3 poeng

Vi vil se skolene og barnehagene i Oslo på kartet.

**Krav**

- Laget skal ha id `skoler`, navn «Skoler» og et passende ikon.
- Ett punkt per skole eller barnehage i Oslo.
- Punktene skal vise navn, type og eierform.

**Datakilde**

Udir sine registre ligger i Allemannsdata under utdanningskilden. Bruk `describe_source` for å se hvilke operasjoner den har, og `get_data` for å se om radene har koordinater.

Har ikke kilden koordinater, men adresse, kan du slå opp adressen hos Kartverket — men skriv i PR-en hvor mange kall det koster.
