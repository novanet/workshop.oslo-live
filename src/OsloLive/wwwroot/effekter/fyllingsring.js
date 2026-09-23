// Bysykkelstasjonene som fyllingsringer: den fylte delen av ringen er
// «fylling» prosent (sykler delt på sykler pluss låser). Tom (0) får rød kant,
// full (100) oransje kant, så de kan skilles uten å lese tallet. Alle andre
// lag sendes videre til lagSymbol fra symboler.js, som lastes før denne.
(function () {
  const NS = 'http://www.w3.org/2000/svg';
  const LAG = 'bysykkelstasjoner';
  const KONTUR = '#111';
  const SPOR = '#fff';
  const FYLT = '#0072B2';
  const TOM = '#dc2626';
  const FULL = '#f97316';

  function sirkel(r, attributter) {
    const el = document.createElementNS(NS, 'circle');
    el.setAttribute('cx', '32'); el.setAttribute('cy', '32'); el.setAttribute('r', String(r));
    el.setAttribute('fill', 'none');
    for (const [navn, verdi] of Object.entries(attributter)) el.setAttribute(navn, String(verdi));
    return el;
  }

  /** Heltall 0–100, eller null når fylling mangler (prikken i lagvelgeren). */
  function tilProsent(fylling) {
    if (fylling === null || fylling === undefined || fylling === '') return null;
    const tall = Number(fylling);
    if (!Number.isFinite(tall)) return null;
    return Math.min(100, Math.max(0, Math.round(tall)));
  }

  function fyllingsring(fylling) {
    const prosent = tilProsent(fylling);
    const vist = prosent ?? 50;
    const el = document.createElementNS(NS, 'svg');
    el.setAttribute('xmlns', NS);
    el.setAttribute('width', '64');
    el.setAttribute('height', '64');
    el.setAttribute('viewBox', '0 0 64 64');

    const kant = prosent === 0 ? TOM : prosent === 100 ? FULL : null;
    el.appendChild(sirkel(28, { stroke: kant ?? KONTUR, 'stroke-width': kant ? 7 : 3 }));
    el.appendChild(sirkel(20, { stroke: KONTUR, 'stroke-width': 14 }));
    el.appendChild(sirkel(20, { stroke: SPOR, 'stroke-width': 10 }));
    if (vist > 0) {
      // pathLength 100 gjør at dasharray kan oppgis rett i prosent. Starter øverst, går med klokka.
      el.appendChild(sirkel(20, {
        stroke: FYLT, 'stroke-width': 10, pathLength: 100,
        'stroke-dasharray': `${vist} ${100 - vist}`,
        transform: 'rotate(-90 32 32)'
      }));
    }
    return el;
  }

  const forrige = window.lagSymbol;
  window.lagSymbol = function (lagId, properties, farge) {
    if (lagId === LAG) return fyllingsring((properties || {}).fylling);
    return typeof forrige === 'function' ? forrige(lagId, properties, farge) : null;
  };
})();
