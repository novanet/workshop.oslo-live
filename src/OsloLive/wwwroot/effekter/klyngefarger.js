// Klyngefarger: en klynge skal vise hvilke lag den består av, ikke bare et
// tall på svart bunn. Løsningen bruker MapLibre sin egen `clusterProperties`
// (én teller per lag, regnet ut i klyngingen selv) i stedet for én kilde per
// lag, slik issuen krever.
//
// `clusterProperties` er en egenskap ved KILDEN og må være kjent når kilden
// lages. Men leggTilPunktlag() kjører i kart.on('style.load', ...), som er
// før start() har fått svar fra /api/lag – da finnes ikke lagene ennå. Vi
// bygger derfor kilden første gang med tomme tellere (samme svarte klynger
// som før), og bygger den på nytt, med riktige tellere og farger, så snart
// lagene er kjent. Signaturen (id+farge per lag) hindrer at vi bygger på nytt
// når ingenting har endret seg, så det blir ingen evig løkke.
(function () {
  'use strict';

  const TELLERPREFIKS = 'kf_';
  const RADIUS_UTTRYKK = ['step', ['get', 'point_count'], 16, 10, 20, 50, 26];
  const ANDRE_LAG_IDER = ['punkter-antall', 'punkter-blink', 'punkter-enkelt'];

  function tellernavn(id) {
    return TELLERPREFIKS + id;
  }

  // Én teller per lag: summen av punkter i klyngen som har akkurat den lag-id-en.
  function clusterProperties(lagene) {
    const cp = {};
    for (const id of lagene.keys()) {
      cp[tellernavn(id)] = ['+', ['case', ['==', ['get', 'lag'], id], 1, 0]];
    }
    return cp;
  }

  // Indeksen (i tellere) til laget med flest punkter. Ved likt antall vinner
  // det som står først i lista, samme rekkefølge hver gang.
  function vinnerUttrykk(tellere) {
    const uttrykk = ['case'];
    tellere.forEach((c, i) => {
      const betingelser = tellere
        .map((a, j) => (j === i ? null : ['>=', c, a]))
        .filter(Boolean);
      uttrykk.push(betingelser.length ? ['all', ...betingelser] : true, i);
    });
    uttrykk.push(-1);
    return uttrykk;
  }

  // Indeksen til laget med nest flest punkter: størst blant lagene som ikke
  // vant og som faktisk har punkter i klyngen (uten det siste kravet ville et
  // lag med null punkter kunne bli valgt som "nest størst" bare fordi det står
  // tidlig i lista).
  function nestUttrykk(tellere) {
    const uttrykk = ['case'];
    tellere.forEach((c, i) => {
      const betingelser = [['!=', ['var', 'vinner'], i], ['>', c, 0]];
      tellere.forEach((a, j) => {
        if (j === i) return;
        betingelser.push(['any', ['==', ['var', 'vinner'], j], ['>=', c, a]]);
      });
      uttrykk.push(['all', ...betingelser], i);
    });
    uttrykk.push(-1);
    return uttrykk;
  }

  // Pakker et uttrykk inn i variablene 'vinner' (størst lag) og 'nest' (nest
  // størst lag, eller -1 hvis klyngen bare består av ett lag).
  function medVinnerOgNest(idListe, indre) {
    const tellere = idListe.map((id) => ['get', tellernavn(id)]);
    return ['let', 'vinner', vinnerUttrykk(tellere),
      ['let', 'nest', nestUttrykk(tellere), indre]];
  }

  function klyngePaint(lagene) {
    const idListe = [...lagene.keys()];
    const FALLBACK = '#111';
    if (!idListe.length) {
      return {
        'circle-color': FALLBACK,
        'circle-radius': RADIUS_UTTRYKK,
        'circle-stroke-width': 2,
        'circle-stroke-color': '#fff'
      };
    }

    const fargeMatch = ['match', ['var', 'vinner']];
    idListe.forEach((id, i) => fargeMatch.push(i, lagene.get(id).farge));
    fargeMatch.push(FALLBACK);

    const ringMatch = ['match', ['var', 'nest']];
    idListe.forEach((id, i) => ringMatch.push(i, lagene.get(id).farge));
    ringMatch.push('#fff');

    return {
      'circle-color': medVinnerOgNest(idListe, fargeMatch),
      'circle-radius': RADIUS_UTTRYKK,
      'circle-stroke-width': 2,
      'circle-stroke-color': medVinnerOgNest(idListe,
        ['case', ['!=', ['var', 'nest'], -1], ringMatch, '#fff'])
    };
  }

  function kildeSpec(lagene) {
    return {
      type: 'geojson',
      data: { type: 'FeatureCollection', features: [] },
      cluster: true,
      clusterRadius: 50,
      clusterMaxZoom: 15,
      clusterProperties: clusterProperties(lagene)
    };
  }

  function klyngeLag(lagene) {
    return {
      id: 'punkter-klynge', type: 'circle', source: 'punkter',
      filter: ['has', 'point_count'],
      paint: klyngePaint(lagene)
    };
  }

  // Rekkefølgen på lag+farger sist vi bygde kilden på nytt. Uendret signatur
  // betyr ingenting å gjøre; unngår evig ombygging fra den jevnlige sjekken.
  let sisteSignatur = null;

  function signaturFor(lagene) {
    return [...lagene].map(([id, l]) => id + ':' + l.farge).sort().join('|');
  }

  function byggPaNytt(kart, lagene) {
    const andre = kart.getStyle().layers.filter((l) => ANDRE_LAG_IDER.includes(l.id));

    for (const id of ['punkter-klynge', ...ANDRE_LAG_IDER]) {
      if (kart.getLayer(id)) kart.removeLayer(id);
    }
    if (kart.getSource('punkter')) kart.removeSource('punkter');

    kart.addSource('punkter', kildeSpec(lagene));
    kart.addLayer(klyngeLag(lagene));
    for (const spec of andre) kart.addLayer(spec);

    if (typeof oppdaterKilde === 'function') oppdaterKilde();
  }

  function sjekk() {
    if (typeof kart === 'undefined' || typeof lagene === 'undefined') return;
    if (!kart.getSource('punkter')) return;

    const signatur = signaturFor(lagene);
    if (signatur === sisteSignatur) return;
    sisteSignatur = signatur;
    byggPaNytt(kart, lagene);
  }

  setInterval(sjekk, 300);

  window.klyngefarger = { kildeSpec, klyngeLag };
})();
