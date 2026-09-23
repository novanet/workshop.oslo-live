/* Et kort, lavt pling når et lag får punkter det ikke hadde før. Av som standard. */
(() => {
  'use strict';

  const TONER = [523.25, 587.33, 659.25, 783.99, 880.0, 987.77, 1046.5]; // Hz, fast liste
  const VARIGHET = 0.24;   // sekunder, under 250 ms
  const VOLUM = 0.04;      // lavt
  const PAUSE_MS = 2000;   // høyst ett pling per 2 sekunder
  const SJEKK_MS = 1000;

  const Lydmotor = window.AudioContext || window.webkitAudioContext;
  let på = false;
  let ctx = null;
  let sistPling = 0;
  const sett = new Map();     // lag-id -> { liste, ider: Set }
  const frosset = new Map();  // lag-id -> punkter-listen som vistes mens tidslinja var flyttet

  function toneFor(id) {
    let h = 0;
    for (const t of String(id)) h = (h * 31 + t.codePointAt(0)) >>> 0;
    return TONER[h % TONER.length];
  }

  function pling(id) {
    if (!ctx) return;
    const t = ctx.currentTime;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.value = toneFor(id);
    gain.gain.setValueAtTime(0.0001, t);
    gain.gain.linearRampToValueAtTime(VOLUM, t + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.0001, t + VARIGHET - 0.02);
    osc.connect(gain).connect(ctx.destination);
    osc.start(t);
    osc.stop(t + VARIGHET);
  }

  function sjekk() {
    if (typeof lagene === 'undefined') return;
    const tidsreise = typeof valgtTid !== 'undefined' && valgtTid;
    if (tidsreise) {                       // ingen lyd, og start på nytt når vi er tilbake på «nå»
      sett.clear();
      for (const [id, oppf] of lagene) frosset.set(id, oppf.punkter);
      return;
    }
    let nytt = null;
    for (const [id, oppf] of lagene) {
      if (oppf.feil || !oppf.sistOk) continue;         // ikke hentet levende ennå, eller feiler: behold grunnlaget
      if (frosset.has(id)) {
        if (frosset.get(id) === oppf.punkter) continue; // fortsatt historiske punkter
        frosset.delete(id);
      }
      const forrige = sett.get(id);
      if (forrige && forrige.liste === oppf.punkter) continue;
      const ider = new Set(oppf.punkter.map((p) => p.id));
      if (forrige && !nytt && [...ider].some((i) => !forrige.ider.has(i))) nytt = id;
      sett.set(id, { liste: oppf.punkter, ider });
    }
    if (nytt && på && Date.now() - sistPling >= PAUSE_MS) {
      sistPling = Date.now();
      pling(nytt);
    }
  }

  const rad = document.querySelector('.kamera');
  if (rad) {
    const knapp = document.createElement('button');   // ingen id: en id ville blitt en global på window
    knapp.type = 'button';
    knapp.textContent = 'Lyd';
    knapp.title = 'Et kort pling når et lag får nye punkter';
    knapp.setAttribute('aria-pressed', 'false');
    if (!Lydmotor) knapp.disabled = true;
    knapp.addEventListener('click', () => {
      på = !på;
      knapp.setAttribute('aria-pressed', String(på));
      if (på) {
        ctx ??= new Lydmotor();
        ctx.resume?.();
      }
    });
    rad.appendChild(knapp);
  }

  setInterval(sjekk, SJEKK_MS);
})();
