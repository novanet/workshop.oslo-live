// Beskytter storskjermen mot innbrenning: flytter paneler, kameraknapper og
// tidslinjen noen få piksler med jevne mellomrom, så de ikke brenner faste
// skygger inn i skjermen. Bruker CSS-egenskapen `translate` (ikke `transform`),
// slik at plasseringen enkelte av elementene allerede setter i `transform`
// (tidslinjas sentrering, mobilvisningen) ikke overskrives.
(() => {
  const modus = new URLSearchParams(location.search).get('innbrenning');
  if (modus === 'av') return;
  if (window.innerWidth < 1000) return;

  const VELGERE = '.panel, .kamera, .tidslinje';

  // Fast runde med forskyvninger, alle innenfor ±4 px i hver retning, slik at
  // elementene aldri havner mer enn 4 px fra der de startet.
  const FORSKYVNINGER = [
    [0, 0], [4, 0], [4, 4], [0, 4], [-4, 4], [-4, 0], [-4, -4], [0, -4], [4, -4],
  ];

  const INTERVALL_MS = modus === 'test' ? 5000 : 5 * 60 * 1000;

  let indeks = 0;

  function flytt() {
    indeks = (indeks + 1) % FORSKYVNINGER.length;
    const [x, y] = FORSKYVNINGER[indeks];
    document.querySelectorAll(VELGERE).forEach((el) => {
      el.style.transition = 'translate 2s ease';
      el.style.translate = `${x}px ${y}px`;
    });
  }

  setInterval(flytt, INTERVALL_MS);
})();
