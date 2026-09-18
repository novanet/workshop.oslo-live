Hent lagene parallelt
enhancement,P2,1 poeng

Frontenden henter lagene ett om gangen. Med tjue lag tar en full oppdatering altfor lang tid.

**Mål**

Den som åpner kartet ser punktene fra de raske lagene med en gang, uten å vente på det tregeste laget. En full oppdatering tar like lang tid som det tregeste laget, ikke summen av alle.

**Akseptansekriterier**

- Ved oppstart og ved hver 15-sekunders oppdatering starter kallene til `/api/lag/{id}` for alle lag innen samme sekund. I nettleserens nettverksfane overlapper forespørslene i tid.
- Et lag som bruker 10 sekunder eller mer på å svare holder ikke igjen de andre: tellerne for de raske lagene viser tall før det trege laget har svart.
- Telleren og markørene for hvert lag oppdateres når akkurat det lagets svar kommer, ikke først når alle er ferdige. Klokkeslettet nederst i panelet oppdateres når det siste svaret er inne.
- Et lag som feiler viser «feil» i rødt, og de andre lagene viser tallene sine som normalt.
- Er et lags forrige kall fortsatt i gang når neste oppdatering starter, startes ikke et nytt kall for det laget. Kallene stabler seg ikke opp mot et tregt lag.
- PR-en sier hvor mange samtidige kall dette gir mot Allemannsdata per bruker, og hvorfor det er greit, eller hva som begrenser det.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Tenk på hvor mange samtidige kall det blir mot Allemannsdata, og om det er greit.
