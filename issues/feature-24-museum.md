Nytt lag: museer og samlinger
enhancement,P3

DigitaltMuseum har over ti millioner objekter fra norske museer. Vi vil se museene i Oslo på kartet, med et smakebit-objekt i popup-en.

**Krav**

- Laget skal ha id `museum`, navn «Museer» og et passende ikon.
- Ett punkt per museum.
- Popup-en skal vise museets navn og ett eksempelobjekt fra samlingen.

**Datakilde**

Kulturarvkilden i Allemannsdata dekker både Askeladden og DigitaltMuseum. Bruk `describe_source`.

Dette er et lag som trenger to kall: ett for museene, ett for objektene. Tenk på mellomlageret — ett kall per museum per oppdatering blir mange.
