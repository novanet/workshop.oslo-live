Tall i URL-ene får norsk desimalkomma
bug,P1,3 poeng

Under en kodegjennomgang fant vi at adressene vi bygger mot Allemannsdata ser slik ut:

```
.../get_air_quality_nearby?lat=59%2C9139&lon=10%2C7522&limit=50
```

`59,9139` med komma. Det virker i dag, men bare fordi Allemannsdata er snill og leser komma som punktum. Den neste kilden vi kobler oss på gjør det kanskje ikke, og da feiler det bare på maskiner som kjører med norsk kultur. Det er den verste typen feil å lete etter: den virker hos utvikleren og ryker hos kunden.

**Slik ser du det**

Du ser det ikke på kartet. Det er hele problemet. Kall `Allemannsdata.ByggUrl` med norsk kultur og se på hva som kommer ut.

**Mål**

Adressene mot Allemannsdata er like uansett hvilken kultur maskinen kjører med: desimaltall skrives alltid med punktum. Appen kjører fortsatt på norsk ellers.

**Akseptansekriterier**

- Med `CultureInfo.CurrentCulture` satt til `nb-NO` gir `Allemannsdata.ByggUrl("luftkvalitet", "get_air_quality_nearby", { lat = 59.9139, lon = 10.7522 })` en URL som inneholder `lat=59.9139` og `lon=10.7522`.
- Samme URL inneholder verken `59,9139` eller `59%2C9139`.
- Det gjelder `double`, `float` og `decimal`. Heltall og tekst formateres som før, for eksempel `limit=50`.
- Kulturoppsettet i `Program.cs` (`nb-NO`) er uendret.
- En ny enhetstest i `tests/OsloLive.Tester` setter norsk kultur, kaller `ByggUrl` og sjekker punktum. Den setter kulturen tilbake etterpå og går ikke mot nettet.
- `dotnet build` og `dotnet test` er grønne. Ingen eksisterende tester er endret eller fjernet.

En feil uten synlig symptom kommer tilbake med mindre noe holder den fast. Skriv testen som gjør det.
