Tall i URL-ene får norsk desimalkomma
bug,P1,3 poeng

Under en kodegjennomgang fant vi at adressene vi bygger mot Allemannsdata ser slik ut:

```
.../get_air_quality_nearby?lat=59%2C9139&lon=10%2C7522&limit=50
```

`59,9139` med komma. Det virker i dag — men bare fordi Allemannsdata er snill og leser komma som punktum. Den neste kilden vi kobler oss på gjør det kanskje ikke, og da feiler det bare på maskiner som kjører med norsk kultur. Det er den verste typen feil å lete etter: den virker hos utvikleren og ryker hos kunden.

**Slik ser du det**

Du ser det ikke på kartet. Det er hele problemet. Kall `Allemannsdata.ByggUrl` med norsk kultur og se på hva som kommer ut.

**Forventet**

Tall i en URL skal skrives med punktum uansett hvilket språk maskinen kjører med. Appen skal fortsatt kjøre med norsk kultur ellers, så ikke fjern kulturoppsettet i `Program.cs`.

En feil uten synlig symptom kommer tilbake med mindre noe holder den fast. Skriv testen som gjør det.
