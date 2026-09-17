Kartet oppdaterer seg ikke, dataene står stille
bug,P1

Poenget med Oslo Live er at dataene er ferske. Frontenden henter lagene på nytt hvert 15. sekund, men svarene er identiske i en halvtime av gangen. Det merkes godt på lag som beveger seg: skip står bom stille i fjorden.

**Slik ser du det**

1. Kall `/api/lag/luftkvalitet` to ganger med noen minutters mellomrom.
2. Se i loggen: «Henter luftkvalitet/get_air_quality_nearby» kommer bare én gang.

**Forventet**

Mellomlageret skal holde på et svar i 30 sekunder, slik konstanten `LevetidSekunder` sier. Det er ikke konstanten som er feil.
