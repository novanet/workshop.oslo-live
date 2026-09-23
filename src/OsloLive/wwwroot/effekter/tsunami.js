// Simulering: en tsunami ruller inn Oslofjorden fra Drøbak og farger byggene
// langs havna blå. Bare på lat. Bruker det eksisterende kartet `kart` fra index.html
// og lager ingen nye globale variabler.
(function () {
  'use strict';

  const BYGG = 'building-3d';
  const FARGE = 'fill-extrusion-color';
  const BLÅ = '#2b6cb0';
  const RESERVEFARGE = '#e6dcc6';
  const KILDE_ID = 'tsunami-front';
  const LAG_ID = 'tsunami-front';

  const UTZOOM_MS = 2500;
  const BØLGE_MS = 16000;     // krav: 12 til 20 s
  const FLY_MS = 3000;
  const VOKS_MS = 4000;
  const HOLD_MS = 5000;       // krav: minst 4 s
  const KRYMP_MS = 4000;
  const ROLIG_HOLD_MS = 8000; // prefers-reduced-motion
  const MAKS_MS = 55000;      // krav: stopper av seg selv innen 60 s
  const MAKS_RADIUS = 300;    // meter, krav: minst 250

  const DRØBAK = [10.63, 59.66];
  const HAVNA = { center: [10.735, 59.908], zoom: 15, pitch: 55, bearing: -20 };

  // Seilingsleia fra Drøbak inn til havna i sentrum.
  const LEIA = [
    DRØBAK,
    [10.607, 59.677],
    [10.585, 59.73],
    [10.565, 59.78],
    [10.575, 59.83],
    [10.625, 59.865],
    [10.665, 59.883],
    [10.7, 59.897],
    [10.733, 59.906]
  ];

  // Kaikanten langs havna fra Frognerkilen til Sørenga.
  const KAIKANT = {
    type: 'LineString',
    coordinates: [
      [10.7005, 59.9135],
      [10.713, 59.908],
      [10.721, 59.907],
      [10.727, 59.909],
      [10.733, 59.9105],
      [10.738, 59.905],
      [10.745, 59.906],
      [10.752, 59.907],
      [10.755, 59.901]
    ]
  };

  const tidtakere = new Set();
  let aktiv = false;
  let kjøring = 0;
  let rafId = null;
  let før;
  let kamera = null;

  const knapp = document.createElement('button');
  knapp.type = 'button';
  knapp.textContent = 'Tsunami';
  document.querySelector('.kamera')?.appendChild(knapp);

  const banner = document.createElement('div');
  banner.setAttribute('role', 'status');
  banner.hidden = true;
  Object.assign(banner.style, {
    position: 'fixed', top: '16px', left: '50%', transform: 'translateX(-50%)', zIndex: '50',
    padding: '8px 28px', borderRadius: '8px', background: '#c53030', color: '#fff',
    fontSize: '40px', fontWeight: '800', letterSpacing: '.12em', pointerEvents: 'none'
  });
  document.body.appendChild(banner);

  const prøv = (f) => { try { f(); } catch (e) { /* rydd videre selv om ett steg feiler */ } };

  function vent(ms) {
    return new Promise((ferdig) => {
      const id = setTimeout(() => { tidtakere.delete(id); ferdig(); }, ms);
      tidtakere.add(id);
    });
  }

  // Meter per grad rundt fjorden, nok for en bølgefront på noen kilometer.
  const M_LAT = 111320;
  const M_LON = M_LAT * Math.cos(59.8 * Math.PI / 180);
  const tilMeter = ([lon, lat]) => [lon * M_LON, lat * M_LAT];
  const tilGrader = ([x, y]) => [x / M_LON, y / M_LAT];

  const LEIA_M = LEIA.map(tilMeter);
  const STREKK = LEIA_M.slice(1).map((p, i) => Math.hypot(p[0] - LEIA_M[i][0], p[1] - LEIA_M[i][1]));
  const LENGDE = STREKK.reduce((a, b) => a + b, 0);

  // Bølgefronten som en linje på tvers av leia, andel t (0–1) av veien inn.
  function front(t) {
    let igjen = t * LENGDE;
    let i = 0;
    while (i < STREKK.length - 1 && igjen > STREKK[i]) { igjen -= STREKK[i]; i++; }
    const [ax, ay] = LEIA_M[i], [bx, by] = LEIA_M[i + 1];
    const dx = (bx - ax) / STREKK[i], dy = (by - ay) / STREKK[i];
    const andel = Math.min(1, igjen / STREKK[i]);
    const px = ax + (bx - ax) * andel, py = ay + (by - ay) * andel;
    const halv = 1500 - 900 * t;
    return {
      type: 'Feature',
      properties: {},
      geometry: {
        type: 'LineString',
        coordinates: [tilGrader([px - dy * halv, py + dx * halv]), tilGrader([px + dy * halv, py - dx * halv])]
      }
    };
  }

  function settRadius(r) {
    const grunn = typeof før === 'string' ? før : RESERVEFARGE;
    kart.setPaintProperty(BYGG, FARGE, ['case', ['<', ['distance', KAIKANT], r], BLÅ, grunn]);
  }

  function rull(nr) {
    return new Promise((ferdig) => {
      const t0 = performance.now();
      const steg = (nå) => {
        if (nr !== kjøring) return;
        const t = Math.min(1, (nå - t0) / BØLGE_MS);
        kart.getSource(KILDE_ID)?.setData(front(t));
        if (t < 1) rafId = requestAnimationFrame(steg); else { rafId = null; ferdig(); }
      };
      rafId = requestAnimationFrame(steg);
    });
  }

  // Radius opp, hold og ned igjen, oppdatert rundt ti ganger i sekundet.
  function flomme(nr) {
    return new Promise((ferdig) => {
      const t0 = performance.now();
      const id = setInterval(() => {
        if (nr !== kjøring) { clearInterval(id); return; }
        const t = performance.now() - t0;
        let r;
        if (t < VOKS_MS) r = MAKS_RADIUS * t / VOKS_MS;
        else if (t < VOKS_MS + HOLD_MS) r = MAKS_RADIUS;
        else r = Math.max(0, MAKS_RADIUS * (1 - (t - VOKS_MS - HOLD_MS) / KRYMP_MS));
        settRadius(r);
        if (t >= VOKS_MS + HOLD_MS + KRYMP_MS) { clearInterval(id); tidtakere.delete(id); ferdig(); }
      }, 100);
      tidtakere.add(id);
    });
  }

  async function full(nr) {
    const utsnitt = kart.cameraForBounds(
      LEIA.reduce((b, p) => b.extend(p), new maplibregl.LngLatBounds(LEIA[0], LEIA[0])),
      { padding: 60 });
    kart.flyTo({ ...utsnitt, pitch: 0, bearing: 0, duration: UTZOOM_MS, essential: true });
    await vent(UTZOOM_MS);
    if (nr !== kjøring) return;

    kart.addSource(KILDE_ID, { type: 'geojson', data: front(0) });
    kart.addLayer({
      id: LAG_ID, type: 'line', source: KILDE_ID,
      layout: { 'line-cap': 'round' },
      paint: { 'line-color': BLÅ, 'line-width': 7, 'line-blur': 1 }
    });
    await rull(nr);
    if (nr !== kjøring) return;

    kart.flyTo({ ...HAVNA, duration: FLY_MS, essential: true });
    await vent(FLY_MS);
    if (nr !== kjøring) return;

    await flomme(nr);
  }

  async function rolig(nr) {
    kart.jumpTo(HAVNA);
    settRadius(MAKS_RADIUS);
    await vent(ROLIG_HOLD_MS);
  }

  async function start() {
    if (aktiv || !kart.getLayer(BYGG)) return;
    aktiv = true;
    const nr = ++kjøring;

    // Snurringen i index.html ville slåss med kameraet; slå den av via knappen.
    const snurr = document.getElementById('snurr');
    if (snurr?.getAttribute('aria-pressed') === 'true') snurr.click();

    før = kart.getPaintProperty(BYGG, FARGE);
    kamera = { center: kart.getCenter(), zoom: kart.getZoom(), pitch: kart.getPitch(), bearing: kart.getBearing() };
    knapp.disabled = true;
    banner.textContent = 'SIMULERING';
    banner.hidden = false;

    const sikkerhet = setTimeout(avslutt, MAKS_MS);
    tidtakere.add(sikkerhet);

    try {
      if (matchMedia('(prefers-reduced-motion: reduce)').matches) await rolig(nr);
      else await full(nr);
    } catch (e) {
      // Faller gjennom til oppryddingen.
    }
    if (nr === kjøring) avslutt();
  }

  function avslutt() {
    if (!aktiv) return;
    aktiv = false;
    kjøring++;
    for (const id of tidtakere) { clearTimeout(id); clearInterval(id); }
    tidtakere.clear();
    if (rafId) cancelAnimationFrame(rafId);
    rafId = null;

    prøv(() => kart.stop());
    prøv(() => { if (kart.getLayer(BYGG)) kart.setPaintProperty(BYGG, FARGE, før); });
    prøv(() => { if (kart.getLayer(LAG_ID)) kart.removeLayer(LAG_ID); });
    prøv(() => { if (kart.getSource(KILDE_ID)) kart.removeSource(KILDE_ID); });
    prøv(() => { if (kamera) kart.jumpTo(kamera); });

    banner.hidden = true;
    banner.textContent = '';
    knapp.disabled = false;
  }

  knapp.addEventListener('click', start);

  document.addEventListener('keydown', (e) => {
    if (e.key !== 't' || e.repeat || e.ctrlKey || e.altKey || e.metaKey) return;
    const mål = e.target;
    if (mål instanceof Element && (mål.closest('input, textarea, select') || mål.isContentEditable)) return;
    start();
  });
})();
