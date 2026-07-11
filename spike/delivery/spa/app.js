// Story 6.6 DELIVERY-ARCHITECTURE SPIKE — throwaway client renderer (vanilla, no framework: the lightest thing
// that answers axis A). Fetches the C#-emitted JSON data layer and renders the two surfaces:
//   • the NON-chart sections it rebuilds ITSELF from the STRUCTURED data (data.json) — proving a TS/SPA renderer
//     consumes the Story 6.2 data layer directly (this is the ~13.5 KB floor);
//   • the chart/rich panels it INJECTS as pre-rendered HTML from bodies.json — the SVG mass a thin client can't
//     cheaply regenerate (the port cost axis B/C measures).
// It reports client render latency (performance.now) to #perf and to the console, and swaps surfaces in-view
// with NO page reload (proving the "few files, client-side nav" output form).

(async function () {
  const perf = document.getElementById('perf');
  const strip = document.getElementById('strip');
  const surface = document.getElementById('surface');

  async function fetchJson(url) {
    const r = await fetch(url);
    if (!r.ok) throw new Error(url + ' → HTTP ' + r.status);
    return r.json();
  }

  const t0 = performance.now();
  let data, bodies;
  try {
    [data, bodies] = await Promise.all([fetchJson('data.json'), fetchJson('bodies.json')]);
  } catch (e) {
    perf.textContent = 'load failed: ' + e.message +
      ' — serve this dir over HTTP with data.json + bodies.json alongside index.html (see spike/delivery/README.md)';
    console.error('[spike] load failed', e);
    return;
  }
  const tFetched = performance.now();

  const renders = {};      // measured client render time per surface
  let current = null;

  function renderDataStrip(surfaceKey) {
    // Rebuild the stat-tile row from STRUCTURED data (data.json) — the SPA's own DOM, not injected HTML.
    strip.innerHTML = '';
    const label = document.createElement('div');
    label.className = 'strip-label';
    label.textContent = 'Rendered by the client from data.json (structured data layer, ' +
      JSON.stringify(data).length.toLocaleString() + ' B) — charts below are injected pre-rendered from bodies.json';
    strip.appendChild(label);
    const tiles = surfaceKey === 'dashboard'
      ? (data.dashboard?.StatTiles || [])
      : [
          { Number: String(data.epics?.EpicCount ?? '—'), Label: 'Epics' },
          { Number: String(data.epics?.DraftedCount ?? '—'), Label: 'Drafted' },
        ];
    for (const t of tiles) {
      const el = document.createElement('div');
      el.className = 'data-tile';
      el.innerHTML = `<div class="n"></div><div class="l"></div>`;
      el.querySelector('.n').textContent = t.Number;
      el.querySelector('.l').textContent = t.Label;
      strip.appendChild(el);
    }
  }

  function show(surfaceKey) {
    const start = performance.now();
    renderDataStrip(surfaceKey);
    // Inject the pre-rendered chart/rich body (the charts). Site CSS isn't loaded; inline SVG still paints.
    surface.innerHTML = surfaceKey === 'dashboard' ? bodies.dashboardBody : bodies.epicsBody;
    const dur = performance.now() - start;
    renders[surfaceKey] = dur;
    current = surfaceKey;
    document.querySelectorAll('.spa-bar button').forEach(b =>
      b.setAttribute('aria-current', String(b.getAttribute('data-surface') === surfaceKey)));
    const dashBytes = new Blob([bodies.dashboardBody]).size;
    const epicsBytes = new Blob([bodies.epicsBody]).size;
    perf.textContent =
      `fetch: ${(tFetched - t0).toFixed(0)} ms · ` +
      `render[${surfaceKey}]: ${dur.toFixed(1)} ms · ` +
      `bodies.json payload: dashboard ${dashBytes.toLocaleString()} B / epics ${epicsBytes.toLocaleString()} B · ` +
      `data.json: ${JSON.stringify(data).length.toLocaleString()} B`;
    console.log('[spike] render', surfaceKey, dur.toFixed(1), 'ms', { renders });
    window.__spikeRenders = renders;
    window.__spikeTiming = { fetchMs: tFetched - t0, renders };
  }

  document.querySelectorAll('.spa-bar button').forEach(b =>
    b.addEventListener('click', () => show(b.getAttribute('data-surface'))));

  show('dashboard');
  // Warm the epics render too so both timings are captured, then return to dashboard.
  requestAnimationFrame(() => { show('epics'); requestAnimationFrame(() => show('dashboard')); });
})();
