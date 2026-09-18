Vis varselet for luftkvalitet, ikke bare målingen
enhancement,P3,3 poeng

Luftkvalitetslaget viser hva som er målt nå. Det finnes også et modellert varsel for de neste to døgnene. Vi vil se det.

**Mål**

Den som klikker på en målestasjon ser ikke bare hva lufta er nå, men hvor ille den blir det neste døgnet, og ser på kartet hvilke stasjoner som får dårlig luft.

**Akseptansekriterier**

- `GET /api/lag/luftkvalitet` svarer fortsatt 200 med samme `id`, `navn` og punkter som før. Feltene `nivå` og `målt` er fortsatt med.
- Popup-en viser i tillegg `varsel` (kort tekst om de neste 24 timene) og `verste nivå neste døgn` (høyeste ventede nivå, for eksempel «Moderat» eller «Mye»).
- Stasjoner der verste nivå neste døgn er «Mye» eller verre får feltet `advarsel` satt, og markøren skiller seg fra de andre på kartet (annen farge eller annet ikon).
- Antall kall mot varseloperasjonen per oppdatering er begrenset og står i PR-en. `/api/lag/luftkvalitet` svarer innen 15 sekunder når kilden svarer normalt.
- Svikter varselkallet for én stasjon, vises stasjonen fortsatt med målingen. Laget feiler ikke som helhet.
- Laget bruker `Allemannsdata` for begge operasjonene. Ingen egen `HttpClient`, ingen API-nøkkel, ingen endringer i `Kart/`.
- `dotnet build` og `dotnet test` er grønne. Ingen nye NuGet-pakker.

**Datakilde**

Samme kilde som i dag, men en annen operasjon: varselet tar en koordinat og gir en timeserie.

Dette er første gang vi henter to operasjoner i samme lag. Tenk på antall kall: ett varsel per stasjon blir mange.
