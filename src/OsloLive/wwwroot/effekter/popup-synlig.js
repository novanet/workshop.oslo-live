/**
 * Sørger for at en nyåpnet popup er synlig (#203): på mobil lukkes panelet og
 * kartet flyttes over det; på PC panoreres kartet bort fra synlige paneler.
 */
(function () {
  const LUFT = 12;
  const PANELER = ['hovedpanel', 'naermest', 'omrade-panel'];

  function synligePaneler() {
    return PANELER.map((id) => document.getElementById(id))
      .filter((el) => el && !el.hidden && el.getClientRects().length > 0
        && getComputedStyle(el).opacity !== '0')
      .map((el) => el.getBoundingClientRect());
  }

  function overlapper(a, b) {
    return a.left < b.right + LUFT && a.right > b.left - LUFT
      && a.top < b.bottom + LUFT && a.bottom > b.top - LUFT;
  }

  /** Minste forskyvning [dx, dy] av popupen som får den fri av panelene. */
  function forskyvning(boks, paneler) {
    let dx = 0, dy = 0;
    for (let runde = 0; runde < 3; runde++) {
      const b = { left: boks.left + dx, right: boks.right + dx, top: boks.top + dy, bottom: boks.bottom + dy };
      const p = paneler.find((p) => overlapper(b, p));
      if (!p) break;
      const valg = [
        [p.right + LUFT - b.left, 0], [p.left - LUFT - b.right, 0],
        [0, p.bottom + LUFT - b.top], [0, p.top - LUFT - b.bottom],
      ];
      valg.sort((x, y) => Math.abs(x[0]) + Math.abs(x[1]) - Math.abs(y[0]) - Math.abs(y[1]));
      dx += valg[0][0]; dy += valg[0][1];
    }
    return [dx, dy];
  }

  /** Flytter kartet slik at en nyåpnet popup ikke havner bak et panel. */
  function sikreSynligPopup(boble) {
    if (erMobil()) settPanelApen(false);
    const flytt = () => requestAnimationFrame(() => {
      if (!boble.isOpen()) return;
      const el = boble.getElement();
      if (!el) return;
      const boks = el.getBoundingClientRect();
      if (erMobil()) {
        const grense = window.innerHeight - hovedpanel.offsetHeight - LUFT;
        const overheng = boks.bottom - grense;
        if (overheng > 0) kart.panBy([0, overheng], { duration: 300 });
        return;
      }
      const [dx, dy] = forskyvning(boks, synligePaneler());
      if (dx || dy) kart.panBy([-dx, -dy], { duration: 300 });
    });
    if (kart.isMoving()) kart.once('moveend', flytt); else flytt();
  }

  window.sikreSynligPopup = sikreSynligPopup;
})();
