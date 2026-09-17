Gjør kartutsnittet konfigurerbart
enhancement,P3

Oslo-boksen er hardkodet i `Geo`. Vi vil kunne flytte kartet til en annen by uten å endre kode.

**Krav**

- Utsnittet og sentrum leses fra konfigurasjon, med dagens verdier som standard.
- `appsettings.json` er stedet.
- Testene skal fortsatt virke uten at man må sette opp noe.

Tenk på at `Geo` er statisk i dag. Det er en del av oppgaven.
