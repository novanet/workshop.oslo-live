// Et døgn med bysykkelturer, avspilt på 90 sekunder (#191). Lastes med <script defer>
// etter hovedskriptet i index.html, og bruker derfor de globale bindingene kart,
// lagene, settPå, hent, hentBydeler, demo og stoppDemo direkte ved navn.
//
// Målt 23. september 2026 i headless Chromium 1280x800 med 8 153 turer i døgnet: 58 bilder/s
// over 3 s under avspilling (i ettermiddagsrushet kl. 16:43 i døgnet med 188 sykler ute;
// 118 bilder/s kl. 06:05 med 6 ute, på en skjerm som oppdaterer 120 ganger i sekundet);
// 0 kall til /api/lag/* på 20 s under avspilling; lagvelgeren deaktivert under avspilling
// og aktiv igjen etter stopp; se docs/bilder/bysykkeldogn-avspilling.png og
// bysykkeldogn-stoppet.png. Målingen telte requestAnimationFrame-kall i siden mens
// avspillingen gikk, og kall til /api/lag/ fra klikk på knappen og 20 s framover.
(function () {
  'use strict';

  const VARIGHET_MS = 90000;   // ett helt døgn spilles av på 90 sekunder
  const DØGN = 86400;          // sekunder i et døgn
  const HALE = 600;            // halen blekner over 10 minutter døgntid
  const MAKS_VARIGHET_S = 3 * 3600;
  const FARGE = '#7fdcff';
  const PRIKKRADIUS = 2.5;
  const ALFANIVÅER = [0.1, 0.25, 0.45, 0.7];

  // Bildebudsjett: målet er minst 30 bilder i sekundet med over 5 000 turer. Tegningen er
  // O(aktive turer) per bilde med ett beginPath/stroke per alfanivå, ikke per tur. Går
  // gjennomsnittlig bildetid over BUDSJETT_MS i 30 bilder på rad, fjernes ett halenivå
  // (færre linjer per tur); ligger den under halvparten i 120 bilder, legges det tilbake.
  const BUDSJETT_MS = 1000 / 30;
  let aktiveNivåer = ALFANIVÅER.length;
  let forrigeBilde = 0;
  let tregeBilder = 0;
  let raskeBilder = 0;
  let låsteKontroller = [];

  const lenke = document.createElement('link');
  lenke.rel = 'stylesheet';
  lenke.href = 'effekter/bysykkeldogn.css';
  document.head.appendChild(lenke);

  const knapp = document.createElement('button');
  knapp.id = 'sykkeldogn';
  knapp.type = 'button';
  knapp.setAttribute('aria-pressed', 'false');
  knapp.textContent = 'Sykkeldøgn';
  document.querySelector('.kamera').prepend(knapp);

  let gjeldendeDøgn = null;   // { dato, turer }, hentet én gang per sideinnlasting
  let spiller = false;
  let lerret = null;
  let ctx = null;
  let klokkepanel = null;
  let timeGlidebryter = null;
  let animasjonsId = null;
  let t0 = 0;
  let før = null;             // Map(id -> på) før avspillingen skjulte lagene
  let orgHent = null;
  let orgHentBydeler = null;
  let statiskModus = false;

  function reduserBevegelse() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  knapp.addEventListener('click', () => {
    if (spiller) {
      stopp();
      return;
    }

    if (gjeldendeDøgn) {
      spill();
      return;
    }

    knapp.disabled = true;
    forsøkStart();
  });

  async function forsøkStart() {
    let svar;
    try {
      svar = await fetch('/api/bysykkeldogn');
    } catch {
      visFeil();
      return;
    }

    if (svar.status === 202) {
      knapp.textContent = 'Forbereder …';
      knapp.disabled = true;
      setTimeout(forsøkStart, 10000);
      return;
    }

    if (!svar.ok) {
      visFeil();
      return;
    }

    gjeldendeDøgn = await svar.json();
    knapp.disabled = false;
    knapp.textContent = 'Sykkeldøgn';
    spill();
  }

  function visFeil() {
    knapp.textContent = 'Sykkeldøgn (feil)';
    knapp.disabled = true;
    setTimeout(() => {
      knapp.textContent = 'Sykkeldøgn';
      knapp.disabled = false;
    }, 5000);
  }

  function skjulLagene() {
    før = new Map([...lagene].map(([id, o]) => [id, o.på]));
    for (const id of før.keys()) settPå(id, false);

    // hent og hentBydeler er toppnivå-funksjonsdeklarasjoner i det klassiske skriptet i
    // index.html, og dermed egenskaper på window. Kallene `hent(id)` i 15-sekunders-
    // intervallet slår opp navnet på window ved hvert kall, så denne overstyringen gjør at
    // ingen levende data hentes så lenge avspillingen går; visLageneIgjen setter dem tilbake.
    orgHent = window.hent;
    orgHentBydeler = window.hentBydeler;
    window.hent = async () => {};
    window.hentBydeler = async () => {};

    // Lagvelgeren låses, ellers kunne et klikk under avspillingen vist et lag igjen.
    låsteKontroller = [...document.querySelectorAll('#lagliste input, #lagliste button')].filter((el) => !el.disabled);
    for (const el of låsteKontroller) el.disabled = true;
  }

  function visLageneIgjen() {
    for (const el of låsteKontroller) el.disabled = false;
    låsteKontroller = [];

    window.hent = orgHent;
    window.hentBydeler = orgHentBydeler;

    if (før) {
      for (const [id, på] of før) settPå(id, på);
      før = null;
    }

    Promise.allSettled([...lagene.keys()].map((id) => hent(id)));
    hentBydeler();
  }

  function lagKlokkepanel() {
    const panel = document.createElement('div');
    panel.className = 'sykkeldogn-klokke';
    panel.innerHTML =
      '<div class="tid">00:00</div>' +
      '<div class="teller"><span class="antall">0</span> sykler ute</div>' +
      '<div class="dato"></div>';
    document.body.appendChild(panel);
    return panel;
  }

  function lagLerret() {
    const c = document.createElement('canvas');
    c.className = 'sykkeldogn-lerret';
    document.body.appendChild(c);
    return c;
  }

  function tilpassLerret() {
    if (!lerret) return;
    const dpr = window.devicePixelRatio || 1;
    const boks = kart.getContainer().getBoundingClientRect();
    lerret.width = boks.width * dpr;
    lerret.height = boks.height * dpr;
    lerret.style.width = boks.width + 'px';
    lerret.style.height = boks.height + 'px';
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  function spill() {
    if (demo.på) stoppDemo();
    skjulLagene();

    statiskModus = reduserBevegelse();
    lerret = lagLerret();
    ctx = lerret.getContext('2d');
    tilpassLerret();
    window.addEventListener('resize', tilpassLerret);

    klokkepanel = lagKlokkepanel();
    const datoDiv = klokkepanel.querySelector('.dato');
    const dato = new Date(gjeldendeDøgn.dato + 'T12:00:00');
    datoDiv.textContent = dato.toLocaleDateString('nb-NO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });

    knapp.setAttribute('aria-pressed', 'true');
    knapp.textContent = 'Stopp';
    spiller = true;

    if (statiskModus) {
      startStatiskModus();
    } else {
      t0 = performance.now();
      forrigeBilde = 0; tregeBilder = 0; raskeBilder = 0; aktiveNivåer = ALFANIVÅER.length;
      animasjonsId = requestAnimationFrame(tegn);
    }
  }

  function startStatiskModus() {
    lerret.classList.add('statisk');
    timeGlidebryter = document.createElement('input');
    timeGlidebryter.type = 'range';
    timeGlidebryter.min = '0';
    timeGlidebryter.max = '23';
    timeGlidebryter.step = '1';
    timeGlidebryter.value = '8';
    timeGlidebryter.setAttribute('aria-label', 'Time i døgnet');
    klokkepanel.appendChild(timeGlidebryter);

    timeGlidebryter.addEventListener('input', tegnStatisk);
    kart.on('move', tegnStatisk);
    tegnStatisk();
  }

  function stopp() {
    spiller = false;
    if (animasjonsId) cancelAnimationFrame(animasjonsId);
    animasjonsId = null;

    window.removeEventListener('resize', tilpassLerret);
    if (statiskModus) kart.off('move', tegnStatisk);

    lerret?.remove();
    klokkepanel?.remove();
    lerret = null;
    ctx = null;
    klokkepanel = null;
    timeGlidebryter = null;

    visLageneIgjen();

    knapp.setAttribute('aria-pressed', 'false');
    knapp.textContent = 'Sykkeldøgn';
  }

  function kontrollpunkt(a, b) {
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const dist = Math.hypot(dx, dy);
    if (dist === 0) return { x: a.x, y: a.y };
    const nx = -dy / dist;
    const ny = dx / dist;
    return { x: (a.x + b.x) / 2 + nx * 0.2 * dist, y: (a.y + b.y) / 2 + ny * 0.2 * dist };
  }

  function bezierPunkt(a, c, b, p) {
    const m = 1 - p;
    return {
      x: m * m * a.x + 2 * m * p * c.x + p * p * b.x,
      y: m * m * a.y + 2 * m * p * c.y + p * p * b.y,
    };
  }

  function lavesteIndeksEtter(turer, grense) {
    let lav = 0;
    let høy = turer.length;
    while (lav < høy) {
      const midt = (lav + høy) >> 1;
      if (turer[midt].start < grense) lav = midt + 1;
      else høy = midt;
    }
    return lav;
  }

  // Turer sortert på start. Vanlig tilfelle: turer som startet nylig nok til å
  // fortsatt kunne være aktive. Spesialtilfelle: turer som startet sent i går
  // (høyt i lista) og krysser midnatt inn i dette laget av animasjonen.
  function finnAktive(turer, t) {
    const aktive = [];
    let lav = lavesteIndeksEtter(turer, t - MAKS_VARIGHET_S);
    for (let i = lav; i < turer.length && turer[i].start <= t; i++) {
      if (t < turer[i].slutt) aktive.push({ tur: turer[i], t });
    }

    lav = lavesteIndeksEtter(turer, DØGN - MAKS_VARIGHET_S);
    for (let i = lav; i < turer.length; i++) {
      if (turer[i].start <= t) continue;
      if (t + DØGN < turer[i].slutt) aktive.push({ tur: turer[i], t: t + DØGN });
    }

    return aktive;
  }

  function tidTekst(t) {
    const timer = Math.floor(t / 3600) % 24;
    const min = Math.floor((t % 3600) / 60);
    return String(timer).padStart(2, '0') + ':' + String(min).padStart(2, '0');
  }

  function tegn(nå) {
    if (!spiller) return;

    // Tilpass halene til det maskinen rekker, se BUDSJETT_MS.
    if (forrigeBilde) {
      const bildetid = nå - forrigeBilde;
      if (bildetid > BUDSJETT_MS) { tregeBilder++; raskeBilder = 0; } else { raskeBilder++; if (bildetid < BUDSJETT_MS / 2) tregeBilder = 0; }
      if (tregeBilder >= 30 && aktiveNivåer > 1) { aktiveNivåer--; tregeBilder = 0; }
      if (raskeBilder >= 120 && aktiveNivåer < ALFANIVÅER.length) { aktiveNivåer++; raskeBilder = 0; }
    }
    forrigeBilde = nå;

    const forløpt = (nå - t0) % VARIGHET_MS;
    const t = (forløpt / VARIGHET_MS) * DØGN;

    ctx.save();
    ctx.clearRect(0, 0, lerret.width, lerret.height);
    ctx.globalCompositeOperation = 'lighter';

    const aktive = finnAktive(gjeldendeDøgn.turer, t);
    const halePerNivå = ALFANIVÅER.map(() => []);
    const prikker = [];

    for (const { tur, t: lokalT } of aktive) {
      const a = kart.project(tur.fra);
      const b = kart.project(tur.til);
      const c = kontrollpunkt(a, b);
      const varighet = tur.slutt - tur.start;

      const p = (lokalT - tur.start) / varighet;
      prikker.push(bezierPunkt(a, c, b, p));

      const haleStart = Math.max(tur.start, lokalT - HALE);
      for (let i = 0; i < aktiveNivåer; i++) {
        const p0 = (haleStart + (lokalT - haleStart) * (i / aktiveNivåer) - tur.start) / varighet;
        const p1 = (haleStart + (lokalT - haleStart) * ((i + 1) / aktiveNivåer) - tur.start) / varighet;
        halePerNivå[i].push([bezierPunkt(a, c, b, p0), bezierPunkt(a, c, b, p1)]);
      }
    }

    ctx.strokeStyle = FARGE;
    ctx.lineWidth = 1.5;
    for (let i = 0; i < ALFANIVÅER.length; i++) {
      if (!halePerNivå[i].length) continue;
      ctx.globalAlpha = ALFANIVÅER[i];
      ctx.beginPath();
      for (const [p0, p1] of halePerNivå[i]) {
        ctx.moveTo(p0.x, p0.y);
        ctx.lineTo(p1.x, p1.y);
      }
      ctx.stroke();
    }

    ctx.globalAlpha = 1;
    ctx.fillStyle = FARGE;
    ctx.beginPath();
    for (const p of prikker) {
      ctx.moveTo(p.x + PRIKKRADIUS, p.y);
      ctx.arc(p.x, p.y, PRIKKRADIUS, 0, Math.PI * 2);
    }
    ctx.fill();
    ctx.restore();

    klokkepanel.querySelector('.tid').textContent = tidTekst(t);
    klokkepanel.querySelector('.antall').textContent = String(aktive.length);

    animasjonsId = requestAnimationFrame(tegn);
  }

  function tegnStatisk() {
    if (!spiller) return;

    tilpassLerret();
    ctx.save();
    ctx.clearRect(0, 0, lerret.width, lerret.height);
    ctx.globalCompositeOperation = 'lighter';
    ctx.globalAlpha = 0.55;
    ctx.strokeStyle = FARGE;
    ctx.lineWidth = 1.5;
    ctx.beginPath();

    const time = Number(timeGlidebryter.value);
    const fra = time * 3600;
    const til = fra + 3600;
    let antall = 0;

    for (const tur of gjeldendeDøgn.turer) {
      if (tur.start < fra || tur.start >= til) continue;
      antall++;
      const a = kart.project(tur.fra);
      const b = kart.project(tur.til);
      const c = kontrollpunkt(a, b);
      ctx.moveTo(a.x, a.y);
      ctx.quadraticCurveTo(c.x, c.y, b.x, b.y);
    }

    ctx.stroke();
    ctx.restore();

    klokkepanel.querySelector('.tid').textContent = String(time).padStart(2, '0') + ':00';
    klokkepanel.querySelector('.antall').textContent = String(antall);
  }
})();
