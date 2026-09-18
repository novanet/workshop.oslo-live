Kartet oppdaterer seg ikke, dataene står stille
bug,P1,1 poeng

Poenget med Oslo Live er at dataene er ferske. Frontenden henter lagene på nytt hvert 15. sekund, men svarene er identiske i en halvtime av gangen. Det merkes godt på lag som beveger seg: skip står bom stille i fjorden.

**Slik ser du det**

1. Kall `/api/lag/luftkvalitet` to ganger med noen minutters mellomrom.
2. Se i loggen: «Henter luftkvalitet/get_air_quality_nearby» kommer bare én gang.

**Mål**

Et svar fra en kilde gjenbrukes i 30 sekunder, ikke lenger. Etter det spør appen kilden på nytt, og kartet viser ferske data.

**Akseptansekriterier**

- `Allemannsdata.Levetid.TotalSeconds` er 30.
- Konstanten `LevetidSekunder` er fortsatt 30. Det er ikke den som er feil.
- To kall til `/api/lag/luftkvalitet` med mer enn 30 sekunder mellom gir to linjer «Henter luftkvalitet/get_air_quality_nearby» i loggen.
- To kall innenfor 30 sekunder gir fortsatt bare én logglinje. Mellomlageret skal ikke fjernes, bare vare riktig lenge.
- En ny enhetstest i `tests/OsloLive.Tester` sjekker levetiden på `Allemannsdata` uten å gå mot nettet og uten å vente i sanntid.
- `dotnet build` og `dotnet test` er grønne. Ingen eksisterende tester er endret eller fjernet.
