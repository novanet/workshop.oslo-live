/**
 * Ekte sollys på 3D-byggene (#132).
 *
 * Regner ut hvor sola står over Oslo sentrum akkurat nå, og lar lyset på
 * kartet følge den: retning (azimut) og høyde over horisonten, varmere
 * farge når sola står lavt, og svakt lys når den er under horisonten.
 *
 * Bruker den forenklede NOAA/astronomiske solposisjon-formelen (samme som
 * biblioteket SunCalc bygger på). Rører kun `light`, ikke lagfarger.
 */
(function () {
  const BREDDE = 59.91;
  const LENGDE = 10.75;
  const RAD = Math.PI / 180;
  const DAG_MS = 1000 * 60 * 60 * 24;
  const J1970 = 2440588;
  const J2000 = 2451545;
  const SKRÅSTILLING = RAD * 23.4397; // jordaksens helning

  function tilDager(dato) {
    return dato.valueOf() / DAG_MS - 0.5 + J1970 - J2000;
  }

  function rektascensjon(l, b) {
    return Math.atan2(Math.sin(l) * Math.cos(SKRÅSTILLING) - Math.tan(b) * Math.sin(SKRÅSTILLING), Math.cos(l));
  }

  function deklinasjon(l, b) {
    return Math.asin(Math.sin(b) * Math.cos(SKRÅSTILLING) + Math.cos(b) * Math.sin(SKRÅSTILLING) * Math.sin(l));
  }

  function azimutSørVest(timevinkel, phi, dek) {
    return Math.atan2(Math.sin(timevinkel), Math.cos(timevinkel) * Math.sin(phi) - Math.tan(dek) * Math.cos(phi));
  }

  function høydeVinkel(timevinkel, phi, dek) {
    return Math.asin(Math.sin(phi) * Math.sin(dek) + Math.cos(phi) * Math.cos(dek) * Math.cos(timevinkel));
  }

  function siderisktid(d, lengdegrad) {
    return RAD * (280.16 + 360.9856235 * d) - lengdegrad;
  }

  function solasMiddelanomali(d) {
    return RAD * (357.5291 + 0.98560028 * d);
  }

  function eklipticLengde(m) {
    const sentrumslikning = RAD * (1.9148 * Math.sin(m) + 0.02 * Math.sin(2 * m) + 0.0003 * Math.sin(3 * m));
    const perihel = RAD * 102.9372;
    return m + sentrumslikning + perihel + Math.PI;
  }

  /**
   * Solas posisjon over Oslo sentrum ved gitt tidspunkt.
   * @param {Date} dato
   * @returns {{ azimut: number, hoyde: number }} grader. Azimut fra nord med klokka.
   */
  function solposisjon(dato) {
    const lw = RAD * -LENGDE;
    const phi = RAD * BREDDE;
    const d = tilDager(dato);
    const m = solasMiddelanomali(d);
    const l = eklipticLengde(m);
    const dek = deklinasjon(l, 0);
    const ra = rektascensjon(l, 0);
    const timevinkel = siderisktid(d, lw) - ra;

    const azSørVest = azimutSørVest(timevinkel, phi, dek) / RAD;
    const hoyde = høydeVinkel(timevinkel, phi, dek) / RAD;

    let azimut = (azSørVest + 180) % 360;
    if (azimut < 0) azimut += 360;

    return { azimut, hoyde };
  }

  function osloOffsetMinutter(dato) {
    const deler = new Intl.DateTimeFormat('en-US', {
      timeZone: 'Europe/Oslo',
      hour12: false,
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    }).formatToParts(dato).reduce((o, p) => { o[p.type] = p.value; return o; }, {});

    const somOsloTid = Date.UTC(
      Number(deler.year), Number(deler.month) - 1, Number(deler.day),
      deler.hour === '24' ? 0 : Number(deler.hour), Number(deler.minute), Number(deler.second)
    );

    return (somOsloTid - dato.getTime()) / 60000;
  }

  function osloLokalTilUtc(aar, maaned, dag, time, minutt) {
    let gjetning = Date.UTC(aar, maaned - 1, dag, time, minutt);
    for (let i = 0; i < 2; i++) {
      const offset = osloOffsetMinutter(new Date(gjetning));
      gjetning = Date.UTC(aar, maaned - 1, dag, time, minutt) - offset * 60000;
    }
    return new Date(gjetning);
  }

  function soltidFraUrl() {
    const verdi = new URLSearchParams(location.search).get('soltid');
    if (!verdi) return null;

    const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(verdi);
    if (!m) return null;

    const [, aar, maaned, dag, time, minutt] = m;
    return osloLokalTilUtc(Number(aar), Number(maaned), Number(dag), Number(time), Number(minutt));
  }

  const soltidOverstyring = soltidFraUrl();

  function oppdaterLys() {
    if (typeof kart === 'undefined' || !kart.isStyleLoaded()) return;

    const { azimut, hoyde } = solposisjon(soltidOverstyring ?? new Date());
    const overHorisonten = hoyde >= 0;
    const polarvinkel = overHorisonten ? 90 - Math.min(hoyde, 90) : 90;
    const varmt = hoyde < 10;

    kart.setLight({
      anchor: 'map',
      position: [1.5, azimut, polarvinkel],
      color: varmt ? '#ffb454' : '#ffffff',
      intensity: overHorisonten ? 0.5 : 0.2
    });
  }

  kart.on('style.load', oppdaterLys);
  oppdaterLys();
  setInterval(oppdaterLys, 60000);

  window.solposisjon = solposisjon;
})();
