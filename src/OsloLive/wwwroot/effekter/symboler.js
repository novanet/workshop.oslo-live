// Kartsymboler som SVG laget i koden. index.html gjør elementet om til et
// MapLibre-bilde. Gir null når laget ikke har eget symbol, og punktet
// beholder skiven med emoji som før.
(function () {
  const NS = 'http://www.w3.org/2000/svg';
  const KONTUR = '#111';

  function svg(innhold) {
    const el = document.createElementNS(NS, 'svg');
    el.setAttribute('xmlns', NS);
    el.setAttribute('width', '64');
    el.setAttribute('height', '64');
    el.setAttribute('viewBox', '0 0 64 64');
    el.appendChild(innhold);
    return el;
  }

  function sti(d, farge, bredde) {
    const el = document.createElementNS(NS, 'path');
    el.setAttribute('d', d);
    el.setAttribute('fill', farge);
    el.setAttribute('stroke', KONTUR);
    el.setAttribute('stroke-width', String(bredde));
    el.setAttribute('stroke-linejoin', 'round');
    return el;
  }

  // Linjetegning med optisk kontur: en mørk strek under, en farget over.
  function strek(d, farge) {
    const gruppe = document.createElementNS(NS, 'g');
    const under = document.createElementNS(NS, 'path');
    under.setAttribute('d', d);
    under.setAttribute('fill', 'none');
    under.setAttribute('stroke', KONTUR);
    under.setAttribute('stroke-width', '9');
    under.setAttribute('stroke-linecap', 'round');
    const over = document.createElementNS(NS, 'path');
    over.setAttribute('d', d);
    over.setAttribute('fill', 'none');
    over.setAttribute('stroke', farge);
    over.setAttribute('stroke-width', '5');
    over.setAttribute('stroke-linecap', 'round');
    gruppe.append(under, over);
    return gruppe;
  }

  function sirkel(cx, cy, r, farge) {
    const gruppe = document.createElementNS(NS, 'g');
    const under = document.createElementNS(NS, 'circle');
    under.setAttribute('cx', String(cx)); under.setAttribute('cy', String(cy)); under.setAttribute('r', String(r));
    under.setAttribute('fill', 'none'); under.setAttribute('stroke', KONTUR); under.setAttribute('stroke-width', '5');
    const over = document.createElementNS(NS, 'circle');
    over.setAttribute('cx', String(cx)); over.setAttribute('cy', String(cy)); over.setAttribute('r', String(r));
    over.setAttribute('fill', 'none'); over.setAttribute('stroke', farge); over.setAttribute('stroke-width', '2.5');
    gruppe.append(under, over);
    return gruppe;
  }

  /** Rundt til nærmeste 5°, normalisert til 0–359. Mangler eller ugyldig kurs gir 0 (nord). */
  function kursTilGrader(kurs) {
    const tall = Number(kurs);
    if (!Number.isFinite(tall)) return 0;
    const avrundet = Math.round(tall / 5) * 5;
    return ((avrundet % 360) + 360) % 360;
  }

  function retningssymbol(stiData, kurs, farge) {
    const grader = kursTilGrader(kurs);
    const gruppe = document.createElementNS(NS, 'g');
    if (grader) gruppe.setAttribute('transform', `rotate(${grader} 32 32)`);
    gruppe.appendChild(sti(stiData, farge, 4));
    return svg(gruppe);
  }

  // Båt sett ovenfra: spiss baug øverst (nord), symmetrisk om x=32. Skroget
  // strekker seg fra y=3 til y=60, som gir minst 28 px i høyden ved 32 px
  // symbolstørrelse (kart-bildet har pixelRatio 2 på et 64 px lerret).
  const BAAT = 'M32 3 C37 13 40 22 40 31 L40 52 Q40 60 32 60 Q24 60 24 52 L24 31 C24 22 27 13 32 3 Z';

  // Fly sett ovenfra: spiss nese øverst, vinger og haleplan, symmetrisk om
  // x=32. Silhuetten strekker seg fra y=2 til y=60, samme grunn som over.
  const FLY = 'M32 2 C34 2 35 6 35 10 L35 22 L58 34 L58 39 L35 30 L35 46 L46 54 L46 60 L32 56 L18 60 L18 54 L29 46 L29 30 L6 39 L6 34 L29 22 L29 10 C29 6 30 2 32 2 Z';

  function sykkel(farge) {
    const gruppe = document.createElementNS(NS, 'g');
    gruppe.append(
      strek('M16 42 L30 18 L48 42 M30 18 L36 42 M44 14 L52 14 M20 14 L28 18', farge),
      sirkel(16, 42, 10, farge),
      sirkel(48, 42, 10, farge)
    );
    return svg(gruppe);
  }

  function elsparkesykkel(farge) {
    const gruppe = document.createElementNS(NS, 'g');
    gruppe.append(
      strek('M14 50 L46 50 M46 50 L40 12 M34 12 L48 12', farge),
      sirkel(18, 52, 5, farge),
      sirkel(50, 52, 5, farge)
    );
    return svg(gruppe);
  }

  function bil(farge) {
    const KROPP = 'M10 40 L14 28 Q16 24 22 24 L42 24 Q48 24 50 28 L54 40 Q56 42 56 46 L56 50 L8 50 L8 46 Q8 42 10 40 Z';
    const gruppe = document.createElementNS(NS, 'g');
    gruppe.append(
      sti(KROPP, farge, 3),
      sirkel(18, 50, 6, farge),
      sirkel(46, 50, 6, farge)
    );
    return svg(gruppe);
  }

  window.lagSymbol = function (lagId, properties, farge) {
    const p = properties || {};
    if (lagId === 'skip') return retningssymbol(BAAT, p.kurs, farge);
    if (lagId === 'fly') return retningssymbol(FLY, p.kurs, farge);
    if (lagId === 'mobilitet') {
      if (p.type === 'sykkel') return sykkel(farge);
      if (p.type === 'elsparkesykkel') return elsparkesykkel(farge);
      if (p.type === 'bil') return bil(farge);
      return null;
    }
    return null;
  };
})();
