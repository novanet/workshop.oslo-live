// Havet stiger: viser hvilke ruter i det synlige utsnittet som ligger lavere
// enn en valgt havstigning, ut fra terrenget kartet allerede har lastet
// (kilden «terreng», Mapterhorn). Grovt anslag, ikke et flomkap.
(function () {
  // Ute i fjorden, brukt som «null meter»-referanse: queryTerrainElevation
  // gir høyde relativt til kartets midtpunkt, ikke et absolutt havnivå.
  const REFERANSEPUNKT = [10.7335, 59.9050];
  const KILDE = 'havet-stiger';
  const RUTER_X = 80;
  const RUTER_Y = 60;
  const MIN_INTERVALL_MS = 200; // maks fem ganger i sekundet

  /** Høyde over havet i meter for et punkt synlig på kartet, eller null om terrenget ikke er lastet der. */
  function hoydeOverHavet(lngLat) {
    const terreng = kart.getTerrain();
    if (!terreng) return null;

    let punkt, referanse;
    try {
      punkt = kart.queryTerrainElevation(lngLat);
      referanse = kart.queryTerrainElevation(REFERANSEPUNKT);
    } catch (e) {
      return null;
    }
    if (punkt === null || punkt === undefined || referanse === null || referanse === undefined) return null;

    const forsterkning = terreng.exaggeration || 1;
    return (punkt - referanse) / forsterkning;
  }
  window.hoydeOverHavet = hoydeOverHavet;

  let niva = 0;
  let panelApent = false;

  /** Deler det synlige utsnittet i et rutenett og finner rutene som havner under valgt nivå. */
  function beregnRuter() {
    const terreng = kart.getTerrain();
    if (!terreng) return { type: 'FeatureCollection', features: [] };

    let referanse;
    try {
      referanse = kart.queryTerrainElevation(REFERANSEPUNKT);
    } catch (e) {
      referanse = null;
    }
    if (referanse === null || referanse === undefined) return { type: 'FeatureCollection', features: [] };
    const forsterkning = terreng.exaggeration || 1;

    const grenser = kart.getBounds();
    const vest = grenser.getWest(), ost = grenser.getEast();
    const sor = grenser.getSouth(), nord = grenser.getNorth();
    const bredde = (ost - vest) / RUTER_X;
    const hoyde = (nord - sor) / RUTER_Y;

    const features = [];
    for (let i = 0; i < RUTER_X; i++) {
      const v = vest + i * bredde, o = v + bredde, senterLng = v + bredde / 2;
      for (let j = 0; j < RUTER_Y; j++) {
        const s = sor + j * hoyde, n = s + hoyde, senterLat = s + hoyde / 2;

        let punkt;
        try {
          punkt = kart.queryTerrainElevation([senterLng, senterLat]);
        } catch (e) {
          continue;
        }
        if (punkt === null || punkt === undefined) continue;

        const h = (punkt - referanse) / forsterkning;
        if (h <= 0 || h > niva) continue;

        features.push({
          type: 'Feature',
          geometry: { type: 'Polygon', coordinates: [[[v, s], [o, s], [o, n], [v, n], [v, s]]] },
          properties: {}
        });
      }
    }
    return { type: 'FeatureCollection', features };
  }

  /** Id-en til det foerste bygningslaget i stilen (uansett om «building» eller «building-3d» kommer foerst), slik at det blå laget legges under begge. */
  function forsteBygningslag() {
    const lag = (kart.getStyle().layers) || [];
    const funnet = lag.find((l) => l.id === 'building' || l.id === 'building-3d');
    return funnet ? funnet.id : undefined;
  }

  function sikreKilde() {
    if (kart.getSource(KILDE)) return;
    kart.addSource(KILDE, { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
    kart.addLayer({
      id: KILDE, type: 'fill', source: KILDE,
      paint: { 'fill-color': '#2563eb', 'fill-opacity': 0.5 }
    }, forsteBygningslag());
  }

  function tomLag() {
    const kilde = kart.getSource(KILDE);
    if (kilde) kilde.setData({ type: 'FeatureCollection', features: [] });
  }

  function oppdaterHavet() {
    if (!panelApent || niva <= 0) { tomLag(); return; }
    if (!kart.isStyleLoaded() || !kart.getTerrain()) { tomLag(); return; }
    sikreKilde();
    kart.getSource(KILDE).setData(beregnRuter());
  }

  let planlagtId = null, sisteKjoring = 0;
  function planlagtOppdatering() {
    const naa = Date.now();
    const gjenstaar = MIN_INTERVALL_MS - (naa - sisteKjoring);
    clearTimeout(planlagtId);
    if (gjenstaar <= 0) {
      sisteKjoring = naa;
      oppdaterHavet();
    } else {
      planlagtId = setTimeout(() => { sisteKjoring = Date.now(); oppdaterHavet(); }, gjenstaar);
    }
  }

  function byggUI() {
    const kamera = document.querySelector('.kamera');
    if (!kamera) return;

    const knapp = document.createElement('button');
    knapp.type = 'button';
    knapp.id = 'havet-stiger-knapp';
    knapp.textContent = 'Havet stiger';
    knapp.setAttribute('aria-pressed', 'false');
    kamera.appendChild(knapp);

    const panel = document.createElement('div');
    panel.className = 'havet-stiger-panel';
    panel.id = 'havet-stiger-panel';
    panel.hidden = true;

    const tekst = document.createElement('p');
    tekst.className = 'havet-stiger-tekst';
    tekst.id = 'havet-stiger-tekst';

    const skriv = () => { tekst.textContent = 'Havet stiger ' + niva.toFixed(1) + ' m'; };
    skriv();

    const slider = document.createElement('input');
    slider.type = 'range';
    slider.id = 'havet-stiger-slider';
    slider.min = '0';
    slider.max = '10';
    slider.step = '0.5';
    slider.value = String(niva);
    slider.setAttribute('aria-label', 'Havet stiger, meter');

    const advarsel = document.createElement('p');
    advarsel.className = 'havet-stiger-advarsel';
    advarsel.textContent = 'Grovt anslag fra terrengmodell. Ikke et flomkart.';

    panel.append(tekst, slider, advarsel);
    document.body.appendChild(panel);

    knapp.addEventListener('click', () => {
      panelApent = !panelApent;
      knapp.setAttribute('aria-pressed', String(panelApent));
      panel.hidden = !panelApent;
      oppdaterHavet();
    });

    slider.addEventListener('input', () => {
      niva = Number(slider.value);
      skriv();
      planlagtOppdatering();
    });
  }

  byggUI();
  kart.on('moveend', oppdaterHavet);
})();
