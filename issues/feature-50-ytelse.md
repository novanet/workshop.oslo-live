Kartet blir tregt med mange punkter
enhancement,P3,5 poeng

Hvert punkt er et HTML-element. Med flere tusen markører sliter nettleseren.

**Mål**

Den som har alle lag på, med tusenvis av punkter, kan panorere og zoome uten hakking, og popup og lagvelger virker som før.

**Akseptansekriterier**

- Punktene tegnes gjennom en GeoJSON-kilde og et MapLibre-lag (`symbol` eller `circle`) per kartlag, ikke som HTML-markører i DOM-en. Etter endringen finnes ingen elementer med klassen `merke` for vanlige punkter.
- Med minst 5 000 punkter på kartet samtidig (bruk et midlertidig testlag eller genererte data) er panorering og zoom flytende: minst 30 bilder i sekundet målt i nettleserens ytelsesverktøy.
- PR-en har tall før og etter for samme antall punkter: bilder per sekund, tid for en full oppdatering, og minnebruk.
- Klikk på et punkt åpner fortsatt popup med `navn`, detaljene og `kilde`. Lagets farge og ikon vises fortsatt per punkt.
- Å slå et lag av og på i lagvelgeren virker, og telleren per lag og summen nederst viser riktig antall.
- Den 15-sekunders oppdateringen bytter data med `setData` på kilden, uten å fjerne og lage lag på nytt.
- Endringen er begrenset til `wwwroot/index.html`. Ingen npm, ingen byggesteg. `dotnet build` og `dotnet test` er grønne.

MapLibre tegner GeoJSON-kilder på skjermkortet i stedet for i DOM-en. Det er sannsynligvis dit du vil.
