(() => {
  'use strict';
  // Meteornedslag: en kort, tydelig merket simulering. Bruker den eksisterende
  // `kart`-instansen fra index.html, ingen egne kilder utenfra.

  const RADIUS = 400;      // meter, ringens sluttstørrelse og byggenes påvirkningsradius
  const SYNK = 1500;        // ms, ringen vokser ut og byggene synker
  const NEDE = 3000;        // ms, byggene står nede
  const REIS = 4000;        // ms, byggene reiser seg igjen
  const MAKS = 14000;       // ms, sikkerhetsstopp innenfor kravet på 15 sekunder
  const BUNN = 0.1;         // laveste høydefaktor, 10 % av opprinnelig høyde
  const RING = 'meteor-ring';
  const BYGG = 'building-3d';

  let klar = false;
  let aktiv = false;
  let tikk = null;
  let stopp = null;
  let banner = null;
  let førHøyde;
  let harHøyde = false;

  const knapp = document.createElement('button');
  knapp.id = 'meteor';
  knapp.type = 'button';
  knapp.textContent = 'Meteor';
  knapp.setAttribute('aria-pressed', 'false');
  document.querySelector('.kamera')?.appendChild(knapp);

  function tegnerOmrade() {
    return document.getElementById('tegn-omrade')?.getAttribute('aria-pressed') === 'true';
  }

  function væpne() {
    klar = true;
    knapp.setAttribute('aria-pressed', 'true');
    kart.getCanvas().style.cursor = 'crosshair';
  }

  function avvæpne() {
    klar = false;
    knapp.setAttribute('aria-pressed', 'false');
    // Ikke fjern trådkorset hvis «Tegn område» akkurat overtok det (se knappTegn-lytteren under).
    if (!tegnerOmrade()) kart.getCanvas().style.cursor = '';
  }

  function veksle() {
    if (klar) { avvæpne(); return; }
    if (aktiv || tegnerOmrade()) return;
    væpne();
  }

  knapp.addEventListener('click', veksle);

  // Punktlagene sin `mouseleave`-håndtering nullstiller markøren; hold trådkorset mens vi venter.
  kart.on('mousemove', () => {
    if (klar) kart.getCanvas().style.cursor = 'crosshair';
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && klar) { avvæpne(); return; }
    if (e.key.toLowerCase() !== 'm' || e.repeat || e.ctrlKey || e.altKey || e.metaKey) return;
    if (e.target?.closest?.('input, textarea, select, [contenteditable]') || e.target?.isContentEditable) return;
    veksle();
  });

  // «Tegn område» skal ikke måtte konkurrere om neste klikk med et væpnet nedslag.
  document.getElementById('tegn-omrade')?.addEventListener('click', () => {
    if (klar) avvæpne();
  });

  kart.on('click', (e) => {
    if (!klar) return;
    avvæpne();
    slipp([e.lngLat.lng, e.lngLat.lat]);
  });

  function visBanner() {
    banner = document.createElement('div');
    banner.setAttribute('role', 'status');
    banner.textContent = 'SIMULERING';
    banner.style.cssText = 'position:fixed;top:16px;left:50%;transform:translateX(-50%);' +
      'z-index:30;pointer-events:none;padding:8px 24px;border-radius:10px;' +
      'font-size:32px;font-weight:700;letter-spacing:.08em;color:#fff;' +
      'background:#c2410c;box-shadow:0 6px 20px rgba(0,0,0,.25)';
    document.body.appendChild(banner);
  }

  function sirkel(sted, meter) {
    const [lon, lat] = sted;
    const punkter = [];
    const antall = 64;
    for (let i = 0; i <= antall; i++) {
      const vinkel = (i / antall) * 2 * Math.PI;
      const dLat = (meter / 111320) * Math.cos(vinkel);
      const dLon = (meter / (111320 * Math.cos(lat * Math.PI / 180))) * Math.sin(vinkel);
      punkter.push([lon + dLon, lat + dLat]);
    }
    return { type: 'Feature', properties: {}, geometry: { type: 'Polygon', coordinates: [punkter] } };
  }

  function fjernRing() {
    try {
      if (kart.getLayer(RING)) kart.removeLayer(RING);
      if (kart.getSource(RING)) kart.removeSource(RING);
    } catch (e) { /* stilt: laget/kilden finnes ikke lenger */ }
  }

  function slipp(sted) {
    aktiv = true;
    visBanner();
    stopp = setTimeout(avslutt, MAKS);

    if (kart.getZoom() < 14) {
      kart.easeTo({ center: sted, zoom: 15, duration: 1000 });
      kart.once('moveend', () => { if (aktiv) start(sted); });
    } else {
      start(sted);
    }
  }

  function start(sted) {
    const rolig = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    try {
      kart.addSource(RING, { type: 'geojson', data: sirkel(sted, rolig ? RADIUS : 1) });
      kart.addLayer({
        id: RING, type: 'line', source: RING,
        paint: { 'line-color': '#ea580c', 'line-width': 4, 'line-opacity': 1 },
      });
    } catch (e) { /* stilt: ringen er bare pynt */ }

    let harBygg = false;
    try {
      harBygg = !!kart.getLayer(BYGG);
      if (harBygg) {
        førHøyde = kart.getPaintProperty(BYGG, 'fill-extrusion-height');
        harHøyde = true;
      }
    } catch (e) { /* stilt: stilen er ikke klar ennå */ }

    const punkt = { type: 'Point', coordinates: sted };
    const settFaktor = (f) => {
      if (!harBygg) return;
      try {
        kart.setPaintProperty(BYGG, 'fill-extrusion-height', [
          '*', ['get', 'render_height'],
          ['interpolate', ['linear'], ['distance', punkt], 0, f, RADIUS, 1],
        ]);
      } catch (e) { /* stilt: uttrykket støttes ikke, ringen og banneret kjører uansett */ }
    };

    if (rolig) {
      settFaktor(BUNN);
      setTimeout(avslutt, NEDE);
      return;
    }

    const t0 = performance.now();
    tikk = setInterval(() => {
      const t = performance.now() - t0;

      const p = Math.min(1, t / SYNK);
      try {
        kart.getSource(RING)?.setData(sirkel(sted, Math.max(1, RADIUS * p)));
        kart.setPaintProperty(RING, 'line-opacity', 1 - p);
      } catch (e) { /* stilt */ }
      if (p >= 1) fjernRing();

      let faktor;
      if (t < SYNK) {
        faktor = 1 - (1 - BUNN) * p;
      } else if (t < SYNK + NEDE) {
        faktor = BUNN;
      } else if (t < SYNK + NEDE + REIS) {
        faktor = BUNN + (1 - BUNN) * (t - SYNK - NEDE) / REIS;
      } else {
        avslutt();
        return;
      }
      settFaktor(faktor);
    }, 100);
  }

  function avslutt() {
    if (!aktiv) return;
    aktiv = false;
    clearInterval(tikk);
    clearTimeout(stopp);

    if (harHøyde) {
      try {
        if (kart.getLayer(BYGG)) kart.setPaintProperty(BYGG, 'fill-extrusion-height', førHøyde);
      } catch (e) { /* stilt */ }
      harHøyde = false;
    }

    fjernRing();

    banner?.remove();
    banner = null;
  }
})();
