Steder langt utenfor Oslo slipper inn i kartet
bug,P1,2 poeng

Kartet skal bare vise ting i Oslo og indre Oslofjord. Flere lag tar likevel med punkter fra helt andre deler av landet. Vi så blant annet en badeplass i Lillehammer og et målepunkt i Trøndelag.

**Slik ser du det**

Alt som ligger på omtrent samme lengdegrad som Oslo slipper gjennom, uansett hvor langt nord det er. Det samme gjelder motsatt vei: et punkt på samme breddegrad som Oslo slipper inn selv om det ligger langt ute i Nordsjøen.

**Mål**

Bare punkter som ligger innenfor kartutsnittet, både i nord-sør og i øst-vest, kommer med på kartet. Grensene selv er riktige, det er sjekken som slipper for mye gjennom.

**Akseptansekriterier**

- `Geo.IOslo(59.9139, 10.7522)` (Rådhuset) og `Geo.IOslo(59.8960, 10.6270)` (Nesoddtangen) gir `true`.
- `Geo.IOslo(61.1150, 10.4660)` (Lillehammer, omtrent samme lengdegrad som Oslo) og `Geo.IOslo(63.4305, 10.3951)` (Trondheim) gir `false`.
- `Geo.IOslo(60.3913, 5.3221)` (Bergen) og `Geo.IOslo(59.9139, 4.0000)` (samme breddegrad som Oslo, helt annen lengdegrad) gir `false`.
- `Geo.Lag` returnerer `null` for et punkt utenfor utsnittet, og `Geo.Samle` tar det ikke med.
- Konstantene `MinLat`, `MaksLat`, `MinLon` og `MaksLon` i `Geo` er uendret.
- En ny enhetstest i `tests/OsloLive.Tester` dekker minst ett punkt som feiler bare på breddegrad og ett som feiler bare på lengdegrad. Den går ikke mot nettet.
- `dotnet build` og `dotnet test` er grønne. Ingen eksisterende tester er endret eller fjernet.
