// Vindeffekten (#188): tusenvis av små streker som flyter med vinden over Oslo,
// inspirert av Windy.com. Henter rutenettet fra /api/vind og tegner partikler på
// en gjennomsiktig <canvas> over kartet. Bruker den globale «kart» fra index.html.
(() => {
  'use strict';

  const ANTALL_PARTIKLER = 1500;
  // Kalibrert slik at ca. 10 m/s gir 2–3 px bevegelse per bilde ved zoom ~14 og pitch 0.
  const FAKTOR = 0.18;
  const MELLOMLAGER_LEVETID_MS = 30 * 60 * 1000; // samme levetid som backend

  let på = false;
  let animId = null;
  let flytter = false;
  let rute = null;          // { lats, lons, verdier, latMin, latMaks, lonMin, lonMaks }
  let vindCache = null;     // { data, tid }
  let partikler = [];

  const rolig = window.matchMedia('(prefers-reduced-motion: reduce)');

  // ---------------------------------------------------------------------------
  // Knapp i .kamera-raden, rett før «Sentrum»
  // ---------------------------------------------------------------------------
  const knapp = document.createElement('button');
  knapp.id = 'vind';
  knapp.type = 'button';
  knapp.setAttribute('aria-pressed', 'false');
  knapp.textContent = 'Vind';

  const kameraRad = document.querySelector('.kamera');
  const hjemKnapp = document.getElementById('hjem');
  if (hjemKnapp) kameraRad.insertBefore(knapp, hjemKnapp);
  else kameraRad.appendChild(knapp);

  // ---------------------------------------------------------------------------
  // Canvas over kartet, under kartets egne kontroller. Tar ikke imot klikk,
  // slik at kartet fortsatt kan dras og klikkes gjennom laget.
  // ---------------------------------------------------------------------------
  const lerret = document.createElement('canvas');
  lerret.className = 'vind-lerret';
  Object.assign(lerret.style, {
    position: 'absolute', inset: '0', width: '100%', height: '100%',
    pointerEvents: 'none', zIndex: '2',
  });
  lerret.hidden = true;
  const ctx = lerret.getContext('2d');

  const behold = kart.getContainer();
  const kontroller = behold.querySelector('.maplibregl-control-container');
  if (kontroller) behold.insertBefore(lerret, kontroller);
  else behold.appendChild(lerret);

  function tilpassStoerrelse() {
    const dpr = window.devicePixelRatio || 1;
    lerret.width = Math.round(lerret.clientWidth * dpr);
    lerret.height = Math.round(lerret.clientHeight * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  kart.on('resize', () => { if (på) tilpassStoerrelse(); });

  // ---------------------------------------------------------------------------
  // Data: hentes fra /api/vind, mellomlagres lokalt så vi ikke spør hver gang
  // effekten slås på eller kartet flyttes.
  // ---------------------------------------------------------------------------
  async function hentVind() {
    const nå = Date.now();
    if (vindCache && (nå - vindCache.tid) < MELLOMLAGER_LEVETID_MS) return vindCache.data;

    const svar = await fetch('/api/vind');
    if (!svar.ok) throw new Error('HTTP ' + svar.status);
    const punkter = await svar.json();
    vindCache = { data: punkter, tid: nå };
    return punkter;
  }

  function byggRute(punkter) {
    const lats = [...new Set(punkter.map((p) => p.lat))].sort((a, b) => a - b);
    const lons = [...new Set(punkter.map((p) => p.lon))].sort((a, b) => a - b);
    const verdier = lats.map((lat) => lons.map((lon) => {
      const p = punkter.find((q) => q.lat === lat && q.lon === lon);
      return p ? { u: p.u, v: p.v } : { u: 0, v: 0 };
    }));

    return {
      lats, lons, verdier,
      latMin: lats[0], latMaks: lats[lats.length - 1],
      lonMin: lons[0], lonMaks: lons[lons.length - 1],
    };
  }

  const lerp = (a, b, f) => a + (b - a) * f;
  const clamp = (x, min, max) => Math.min(max, Math.max(min, x));

  /** Bilineær interpolering mellom de fire nærmeste rutepunktene. Utenfor rutenettet klemmes koordinaten til kanten. */
  function vindVed(lon, lat) {
    const { lats, lons, verdier } = rute;
    const nx = lons.length, ny = lats.length;

    const x = clamp((lon - rute.lonMin) / (rute.lonMaks - rute.lonMin), 0, 1) * (nx - 1);
    const y = clamp((lat - rute.latMin) / (rute.latMaks - rute.latMin), 0, 1) * (ny - 1);

    const x0 = Math.floor(x), x1 = Math.min(x0 + 1, nx - 1), fx = x - x0;
    const y0 = Math.floor(y), y1 = Math.min(y0 + 1, ny - 1), fy = y - y0;

    const u = lerp(
      lerp(verdier[y0][x0].u, verdier[y0][x1].u, fx),
      lerp(verdier[y1][x0].u, verdier[y1][x1].u, fx),
      fy);
    const v = lerp(
      lerp(verdier[y0][x0].v, verdier[y0][x1].v, fx),
      lerp(verdier[y1][x0].v, verdier[y1][x1].v, fx),
      fy);

    return { u, v };
  }

  // ---------------------------------------------------------------------------
  // Partikler: hver har en posisjon i skjermpiksler, en alder og en levetid.
  // ---------------------------------------------------------------------------
  function nyPartikkel() {
    return {
      x: Math.random() * lerret.clientWidth,
      y: Math.random() * lerret.clientHeight,
      alder: 0,
      levetid: 60 + Math.random() * 60,
    };
  }

  function nullstillPartikler() {
    partikler = Array.from({ length: ANTALL_PARTIKLER }, nyPartikkel);
  }

  function tegnPartikler() {
    // Blekner forrige bilde i stedet for å viske det helt ut: gir partiklene en hale.
    ctx.globalCompositeOperation = 'destination-in';
    ctx.fillStyle = 'rgba(0,0,0,0.92)';
    ctx.fillRect(0, 0, lerret.clientWidth, lerret.clientHeight);
    ctx.globalCompositeOperation = 'source-over';

    // Skalerer med zoom, slik at farten på skjermen ikke endrer seg bare fordi
    // brukeren zoomer: dobbel zoom betyr dobbelt så mange piksler per grad.
    const skala = FAKTOR / (2 ** kart.getZoom());
    ctx.strokeStyle = 'rgba(17,17,17,0.75)';
    ctx.lineWidth = 1.2;

    for (const p of partikler) {
      const ll = kart.unproject([p.x, p.y]);
      const { u, v } = vindVed(ll.lng, ll.lat);
      // Lengdegrad korrigeres med cos(lat): en grad lengdegrad dekker mindre bakke jo lenger nord.
      const nyLng = ll.lng + (u * skala) / Math.cos(ll.lat * Math.PI / 180);
      const nyLat = ll.lat + v * skala;
      const neste = kart.project([nyLng, nyLat]);

      p.alder++;
      const utenfor = neste.x < 0 || neste.y < 0 || neste.x > lerret.clientWidth || neste.y > lerret.clientHeight;
      if (!Number.isFinite(neste.x) || !Number.isFinite(neste.y) || p.alder > p.levetid || utenfor) {
        Object.assign(p, nyPartikkel());
        continue;
      }

      ctx.beginPath();
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(neste.x, neste.y);
      ctx.stroke();

      p.x = neste.x;
      p.y = neste.y;
    }
  }

  // ---------------------------------------------------------------------------
  // Stillestående piler, for prefers-reduced-motion.
  // ---------------------------------------------------------------------------
  function tegnPil(a, b) {
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();

    const vinkel = Math.atan2(b.y - a.y, b.x - a.x);
    const hodeLengde = 6;
    ctx.beginPath();
    ctx.moveTo(b.x, b.y);
    ctx.lineTo(b.x - hodeLengde * Math.cos(vinkel - Math.PI / 6), b.y - hodeLengde * Math.sin(vinkel - Math.PI / 6));
    ctx.moveTo(b.x, b.y);
    ctx.lineTo(b.x - hodeLengde * Math.cos(vinkel + Math.PI / 6), b.y - hodeLengde * Math.sin(vinkel + Math.PI / 6));
    ctx.stroke();
  }

  function tegnPiler() {
    ctx.clearRect(0, 0, lerret.clientWidth, lerret.clientHeight);
    // Fast skala i grader: pillengden blir proporsjonal med vindstyrken (u, v er i m/s).
    const s = 0.0006;
    ctx.strokeStyle = 'rgba(17,17,17,0.8)';
    ctx.fillStyle = 'rgba(17,17,17,0.8)';
    ctx.lineWidth = 1.5;

    for (const lat of rute.lats) {
      for (const lon of rute.lons) {
        const { u, v } = vindVed(lon, lat);
        const a = kart.project([lon, lat]);
        const b = kart.project([lon + (u * s) / Math.cos(lat * Math.PI / 180), lat + v * s]);
        if (!Number.isFinite(a.x) || !Number.isFinite(b.x)) continue;
        tegnPil(a, b);
      }
    }
  }

  // ---------------------------------------------------------------------------
  // Animasjonsløkke
  // ---------------------------------------------------------------------------
  function tegn() {
    if (!på || document.hidden || flytter) { animId = null; return; }
    tegnPartikler();
    animId = requestAnimationFrame(tegn);
  }

  function start() {
    if (!på || document.hidden || flytter || !rute) return;
    if (rolig.matches) { tegnPiler(); return; }
    if (animId) return;
    animId = requestAnimationFrame(tegn);
  }

  function stopp() {
    if (animId) { cancelAnimationFrame(animId); animId = null; }
  }

  // Under panorering og zoom tømmes lerretet; partiklene starter på nytt når kartet står stille.
  kart.on('movestart', () => {
    flytter = true;
    stopp();
    if (på) ctx.clearRect(0, 0, lerret.clientWidth, lerret.clientHeight);
  });

  kart.on('moveend', () => {
    flytter = false;
    if (!på) return;
    nullstillPartikler();
    start();
  });

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) stopp();
    else if (på && !kart.isMoving()) start();
  });

  rolig.addEventListener('change', () => {
    if (!på) return;
    stopp();
    if (rolig.matches) tegnPiler();
    else { nullstillPartikler(); start(); }
  });

  // ---------------------------------------------------------------------------
  // Av/på-knapp. Standard er av.
  // ---------------------------------------------------------------------------
  knapp.addEventListener('click', async () => {
    på = !på;
    knapp.setAttribute('aria-pressed', String(på));

    if (!på) {
      stopp();
      ctx.clearRect(0, 0, lerret.clientWidth, lerret.clientHeight);
      lerret.hidden = true;
      return;
    }

    lerret.hidden = false;
    tilpassStoerrelse();

    try {
      const punkter = await hentVind();
      rute = byggRute(punkter);
    } catch (e) {
      på = false;
      knapp.setAttribute('aria-pressed', 'false');
      knapp.title = 'Vinddata er utilgjengelig';
      lerret.hidden = true;
      return;
    }

    if (rolig.matches) { tegnPiler(); return; }
    nullstillPartikler();
    start();
  });
})();
