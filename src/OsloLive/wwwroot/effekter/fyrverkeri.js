// Fyrverkeri over Rådhuset når en PR i repoet blir merget. Spør GitHub
// direkte fra nettleseren, uten token. Bruker det eksisterende kartet `kart`
// fra index.html og lager ingen nye globale variabler.
(function () {
  'use strict';

  const REPO = 'novanet/workshop.oslo-live';
  const API_URL = `https://api.github.com/repos/${REPO}/pulls?state=closed&sort=updated&direction=desc&per_page=20`;
  const POLL_MS = 60000;
  const TEKST_MS = 8000;
  const RÅDHUSET = [10.7336, 59.9116]; // [lon, lat]
  const FARGER = ['#ff595e', '#ffca3a', '#8ac926', '#1982c4', '#6a4c93', '#ffffff'];

  let etag = null;
  let rateBegrensetTil = 0;
  let grunnlagKlar = false;
  const kjenteNumre = new Set();
  // Bare merger etter at siden ble lastet feires. En eldre merget PR som dukker opp blant de
  // 20 sist oppdaterte (for eksempel etter en ny kommentar) er ikke ny, selv om nummeret er ukjent.
  const lastetTidspunkt = Date.now();
  const kø = [];
  let køKjører = false;
  let aktivAvslutt = null;

  const lerret = document.createElement('canvas');
  Object.assign(lerret.style, {
    position: 'fixed', inset: '0', zIndex: '35', pointerEvents: 'none', display: 'none'
  });
  document.body.appendChild(lerret);

  const tekst = document.createElement('div');
  Object.assign(tekst.style, {
    position: 'fixed', inset: '0', zIndex: '36', pointerEvents: 'none', display: 'none',
    placeContent: 'center', textAlign: 'center', fontSize: 'clamp(32px, 4vw, 56px)',
    fontWeight: '800', color: '#fff', textShadow: '0 2px 14px rgba(0,0,0,.8)', padding: '0 24px'
  });
  document.body.appendChild(tekst);

  function finnAgentnavn(body) {
    if (!body) return 'en agent';
    const treff = body.match(/<!--\s*mini-nils\s*(\{[\s\S]*?\})\s*-->/);
    if (!treff) return 'en agent';
    try {
      const data = JSON.parse(treff[1]);
      return typeof data.agent === 'string' && data.agent.trim() ? data.agent : 'en agent';
    } catch (e) {
      return 'en agent';
    }
  }

  function tilpassLerretstørrelse() {
    lerret.width = innerWidth;
    lerret.height = innerHeight;
  }
  window.addEventListener('resize', () => {
    if (lerret.style.display !== 'none') tilpassLerretstørrelse();
  });

  function målpunkt() {
    try {
      const p = kart.project(RÅDHUSET);
      if (p.x >= 0 && p.x <= innerWidth && p.y >= 0 && p.y <= innerHeight) return [p.x, p.y];
    } catch (e) {
      // kartet er ikke klart ennå; bruk skjermens midtpunkt
    }
    return [innerWidth / 2, innerHeight / 2];
  }

  function lagRaketter(x, y) {
    const raketter = [];
    for (let i = 0; i < 4; i++) {
      const farge = FARGER[Math.floor(Math.random() * FARGER.length)];
      const partikler = [];
      const antall = 40;
      for (let p = 0; p < antall; p++) {
        const vinkel = (p / antall) * Math.PI * 2;
        const fart = 1.4 + Math.random() * 2.4;
        partikler.push({ vx: Math.cos(vinkel) * fart, vy: Math.sin(vinkel) * fart });
      }
      raketter.push({
        x: x + (Math.random() - 0.5) * 140,
        y: y + (Math.random() - 0.5) * 80,
        farge,
        partikler,
        forsinkelse: i * 650,
        levetid: 1400,
        _t0: null,
        startTid: null
      });
    }
    return raketter;
  }

  function tegnRakett(kontekst, rakett, nå) {
    if (rakett.startTid === null) {
      if (rakett._t0 === null) rakett._t0 = nå;
      if (nå - rakett._t0 < rakett.forsinkelse) return true;
      rakett.startTid = nå;
    }
    const alder = nå - rakett.startTid;
    if (alder > rakett.levetid) return false;

    const tSek = alder / 1000;
    kontekst.globalAlpha = Math.max(0, 1 - alder / rakett.levetid);
    kontekst.fillStyle = rakett.farge;
    for (const partikkel of rakett.partikler) {
      const px = rakett.x + partikkel.vx * tSek * 80;
      const py = rakett.y + partikkel.vy * tSek * 80 + 0.5 * 260 * tSek * tSek;
      kontekst.beginPath();
      kontekst.arc(px, py, 2.5, 0, Math.PI * 2);
      kontekst.fill();
    }
    kontekst.globalAlpha = 1;
    return true;
  }

  function startFyrverkeri() {
    tilpassLerretstørrelse();
    lerret.style.display = 'block';
    const kontekst = lerret.getContext('2d');
    const [x, y] = målpunkt();
    const raketter = lagRaketter(x, y);
    let ferdig = false;
    let rafId = null;

    function tegn(nå) {
      if (ferdig) return;
      kontekst.clearRect(0, 0, lerret.width, lerret.height);
      const noenLever = raketter.map((r) => tegnRakett(kontekst, r, nå)).some(Boolean);
      rafId = noenLever ? requestAnimationFrame(tegn) : null;
    }
    rafId = requestAnimationFrame(tegn);

    return function stopp() {
      ferdig = true;
      if (rafId) cancelAnimationFrame(rafId);
      kontekst.clearRect(0, 0, lerret.width, lerret.height);
      lerret.style.display = 'none';
    };
  }

  function feirePR(pr) {
    return new Promise((ferdig) => {
      const agent = finnAgentnavn(pr.body);
      tekst.textContent = `🎉 ${agent}: ${pr.title}`;
      tekst.style.display = 'grid';

      const redusertBevegelse = matchMedia('(prefers-reduced-motion: reduce)').matches;
      const stoppAnimasjon = redusertBevegelse ? null : startFyrverkeri();

      const tidsavbrudd = setTimeout(avslutt, TEKST_MS);

      function avslutt() {
        clearTimeout(tidsavbrudd);
        tekst.style.display = 'none';
        tekst.textContent = '';
        if (stoppAnimasjon) stoppAnimasjon();
        aktivAvslutt = null;
        ferdig();
      }

      aktivAvslutt = avslutt;
    });
  }

  // Fanges i capture-fasen, så et klikk som stopper propagering i UI-et (for eksempel «Varme»-knappen)
  // også avbryter. Klikket tømmer køen: den som klikker vil ha ro, ikke neste feiring.
  document.addEventListener('click', () => { kø.length = 0; if (aktivAvslutt) aktivAvslutt(); }, true);

  async function kjørKø() {
    if (køKjører) return;
    køKjører = true;
    while (kø.length) await feirePR(kø.shift());
    køKjører = false;
  }

  async function hentPRer() {
    const headers = {};
    if (etag) headers['If-None-Match'] = etag;
    const svar = await fetch(API_URL, { headers });

    if (svar.status === 304) return null;
    if (svar.status === 403 || svar.status === 429) {
      const reset = Number(svar.headers.get('X-RateLimit-Reset'));
      rateBegrensetTil = reset ? reset * 1000 : Date.now() + POLL_MS;
      console.warn('Fyrverkeri: GitHub svarte', svar.status, '- venter til rate-begrensningen nullstilles');
      return null;
    }
    if (!svar.ok) {
      console.warn('Fyrverkeri: uventet svar fra GitHub', svar.status);
      return null;
    }

    etag = svar.headers.get('ETag') || etag;
    return svar.json();
  }

  function mergedeSortertEldstFørst(prer) {
    return prer.filter((pr) => pr.merged_at).sort((a, b) => new Date(a.merged_at) - new Date(b.merged_at));
  }

  async function sjekkMerger() {
    if (document.hidden) return;
    if (Date.now() < rateBegrensetTil) return;

    let prer;
    try {
      prer = await hentPRer();
    } catch (e) {
      console.warn('Fyrverkeri: kall mot GitHub feilet', e);
      return;
    }
    if (!prer) return;

    const mergede = mergedeSortertEldstFørst(prer);
    if (!grunnlagKlar) {
      for (const pr of mergede) kjenteNumre.add(pr.number);
      grunnlagKlar = true;
      return;
    }

    for (const pr of mergede) {
      if (kjenteNumre.has(pr.number)) continue;
      kjenteNumre.add(pr.number);
      if (new Date(pr.merged_at).getTime() < lastetTidspunkt) continue;   // merget før siden ble åpnet
      kø.push(pr);
    }
    kjørKø();
  }

  async function kjørTest() {
    let prer;
    try {
      prer = await hentPRer();
    } catch (e) {
      console.warn('Fyrverkeri: testkall mot GitHub feilet', e);
      return;
    }
    if (!prer) return;

    const mergede = mergedeSortertEldstFørst(prer);
    for (const pr of mergede) kjenteNumre.add(pr.number);
    grunnlagKlar = true;

    if (mergede.length === 0) return;
    kø.push(mergede[mergede.length - 1]);
    kjørKø();
  }

  const testmodus = new URLSearchParams(location.search).get('fyrverkeri') === 'test';
  if (testmodus) kjørTest(); else sjekkMerger();
  setInterval(sjekkMerger, POLL_MS);
})();
