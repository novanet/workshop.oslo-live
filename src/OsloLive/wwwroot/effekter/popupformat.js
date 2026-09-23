// Gjør egenskapene i popupen lesbare: norske tider i Oslo-tid, desimalkomma,
// enheter og stor forbokstav på nøkkelen. Felt som er ment for markøren skjules.
// Gir { nøkkel, verdi } som ren tekst (index.html escaper), eller null for å skjule raden.
(function () {
  const SKJULT = new Set(['ikon', 'advarsel']);
  const ENHETER = { temperatur: ' °C', kurs: '°' };
  const MÅNEDER = ['jan.', 'feb.', 'mar.', 'apr.', 'mai', 'jun.',
                   'jul.', 'aug.', 'sep.', 'okt.', 'nov.', 'des.'];
  const TIDSPUNKT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})$/;
  const DATO = /^(\d{4})-(\d{2})-(\d{2})$/;
  const OSLO = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/Oslo', year: 'numeric', month: 'numeric', day: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
  });

  function osloDeler(d) {
    const del = {};
    for (const p of OSLO.formatToParts(d)) del[p.type] = p.value;
    return { år: +del.year, måned: +del.month, dag: +del.day, time: del.hour, minutt: del.minute };
  }

  function tidspunkt(tekst) {
    const d = new Date(tekst);
    if (isNaN(d)) return tekst;
    const t = osloDeler(d), nå = osloDeler(new Date());
    const klokke = `kl. ${t.time}:${t.minutt}`;
    if (t.år === nå.år && t.måned === nå.måned && t.dag === nå.dag) return `i dag ${klokke}`;
    return `${t.dag}. ${MÅNEDER[t.måned - 1]} ${klokke}`;
  }

  function verdiTekst(verdi) {
    if (typeof verdi === 'number') return String(verdi).replace('.', ',');
    const tekst = String(verdi);
    if (TIDSPUNKT.test(tekst)) return tidspunkt(tekst);
    const dato = DATO.exec(tekst);
    if (dato && MÅNEDER[+dato[2] - 1]) return `${+dato[3]}. ${MÅNEDER[+dato[2] - 1]} ${dato[1]}`;
    return tekst;
  }

  window.formaterEgenskap = function (nøkkel, verdi) {
    if (SKJULT.has(nøkkel)) return null;
    const navn = String(nøkkel).replaceAll('_', ' ');
    return {
      nøkkel: navn.charAt(0).toUpperCase() + navn.slice(1),
      verdi: verdiTekst(verdi) + (ENHETER[nøkkel] ?? '')
    };
  };
})();
