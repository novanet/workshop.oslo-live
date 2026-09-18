Varsle når noe skjer
enhancement,P3,5 poeng

Vi vil bli gjort oppmerksom på ting uten å stirre på skjermen.

**Mål**

Den som har kartet oppe i bakgrunnen får beskjed når noe viktig skjer: dårlig luft, en alvorlig hendelse eller en stengt vei, uten å måtte lete etter det selv.

**Akseptansekriterier**

- Minst tre regler finnes i koden: luftkvalitet med nivå «Mye» eller verre, en hendelse med høy alvorlighet, og et veiarbeid som stenger en vei. Hver regel er én funksjon som tar et punkt og gir true eller false, og reglene ligger samlet ett sted i `wwwroot/index.html`.
- Når et punkt oppfyller en regel første gang, blinker markøren (minst 3 blink) og en melding med lagets ikon, punktets navn og hva regelen gjelder vises i en varselliste i panelet.
- Hver melding har en lukkeknapp. Et lukket varsel kommer ikke tilbake for samme punkt-id og samme regel så lenge siden er åpen.
- Et punkt som fortsatt oppfyller regelen ved neste henting gir ikke en ny melding. Et punkt som slutter å oppfylle regelen og så oppfyller den igjen, gir ny melding.
- Varsellisten viser maks 10 meldinger, nyeste øverst.
- Reglene kjører på dataene som allerede hentes hvert 15. sekund, uten nye kall til API-et.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Hold reglene i kode først. Blir det bra, er konfigurasjon en senere issue.
