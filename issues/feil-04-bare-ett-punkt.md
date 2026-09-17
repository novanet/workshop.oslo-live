Hvert lag viser bare ett eneste punkt
bug,P1

Luftkvalitetslaget henter 18 målestasjoner fra kilden, men det tegnes bare én stasjon på kartet. Telleren i lagvelgeren viser «1». Det samme skjer for alle lag vi legger til.

**Slik ser du det**

1. Åpne kartet. Luftkvalitet viser 1 punkt.
2. Kall `/api/lag/luftkvalitet` direkte. Det er også bare én `feature` i svaret.

**Forventet**

Alle punktene skal være med. Duplikatfjerningen skal bare fjerne det som faktisk er samme punkt to ganger, ikke alt som kommer fra samme kilde.
