Søk på tvers av alle lag
enhancement,P3,3 poeng

Et søkefelt som leter i alt som vises på kartet.

**Mål**

Den som leter etter noe på kartet kan skrive et navn og få treff fra alle lagene som er på, og hoppe rett til punktet.

**Akseptansekriterier**

- Panelet har et søkefelt «Søk i kartet». Fra og med to tegn vises treff under feltet mens du skriver, uten å trykke Enter.
- Søket leter i `navn` og i alle detaljfeltene til punktene i lagene som er på, uten å skille mellom store og små bokstaver. «grünerløkka» gir treff på «Grünerløkka».
- Treffene grupperes per lag med lagets ikon, navn og antall treff. Maks 20 treff vises per lag, og en linje sier hvor mange som er skjult.
- Klikk på et treff flyr kartet til punktet (zoom minst 16) og åpner popup-en.
- Søket gjør ingen nye kall til `/api/lag/{id}` eller til kildene. Det søker i dataene som allerede er hentet.
- Tomt felt eller Escape fjerner trefflisten. Ingen treff gir teksten «Ingen treff».
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Dette er søk i data vi allerede har hentet, ikke et nytt kall til kildene.
