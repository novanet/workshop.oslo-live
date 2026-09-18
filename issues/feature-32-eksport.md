Eksporter det du ser
enhancement,P3,2 poeng

Vi vil kunne laste ned punktene som vises akkurat nå, som GeoJSON eller CSV.

**Mål**

Den som vil jobbe videre med dataene kan laste ned nøyaktig de punktene som vises på kartet akkurat nå, som én fil.

**Akseptansekriterier**

- En knapp «Last ned» i panelet laster ned én GeoJSON-fil med en `FeatureCollection` som inneholder alle punkter fra lagene som er slått på. Lag som er slått av er ikke med.
- Hvert punkt i filen har `properties` med `id`, `navn`, `kilde` og et felt `lag` med lagets id, i tillegg til detaljene.
- Et valg ved siden av knappen gir CSV i stedet. CSV-filen har én overskriftsrad og én rad per punkt, med kolonnene `lag`, `id`, `navn`, `kilde`, `lat`, `lon` først, og deretter én kolonne per detaljfelt som finnes i minst ett punkt.
- Filnavnet er på formen `oslo-live-ÅÅÅÅ-MM-DD-TTMM.geojson` eller `.csv`, med dato og klokkeslett for nedlastingen.
- Er ingen lag på, er knappen deaktivert.
- Nedlastingen skjer i nettleseren fra dataene som allerede er hentet, uten nye kall til API-et.
- Endringen er begrenset til `wwwroot/index.html`. `dotnet build` og `dotnet test` er grønne.

Bare lagene som er slått på skal være med. Det er «det du ser» som er kontrakten.
