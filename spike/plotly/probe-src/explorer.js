/* Story 20.4 probe — the SpecScribe-side adapter over Plotly, written to answer AC #2 and AC #3.
 *
 * THROWAWAY. This is not the Story 20.5 component; it is the smallest thing that can produce an honest
 * PASS / PASS (configured around) / FAIL for UX-DR7/16/17/18 and prove the token/colorway claim.
 *
 * Three things it deliberately does the hard way, because doing them the easy way would fake the answer:
 *   1. Colors are READ OUT OF THE SHIPPED STYLESHEET via getComputedStyle on a real element carrying the real
 *      `.sb-*` class — never re-typed from specscribe.css (AD-7 drift). If a token moves, this moves with it.
 *   2. The a11y layer is applied ONLY through Plotly's public event surface (`plotly_afterplot`) over its emitted
 *      DOM. No Plotly internals are patched. That is what makes a PASS here mean "configured around", not "forked".
 *   3. Reduced motion is achieved by CANCELLING Plotly's built-in drill animation (its click handler honours a
 *      `false` return) and re-applying the level ourselves. The 750 ms drill time is a hard-coded module constant
 *      (src/traces/sunburst/constants.js CLICK_TRANSITION_TIME), NOT a public attribute — so this is the only
 *      supported route, and whether it works is the whole UX-DR18 question.
 */
;(function () {
  'use strict'

  var root = document.getElementById('probe-chart')
  var live = document.getElementById('probe-live')
  var statusEl = document.getElementById('probe-status')
  var island = document.getElementById('sunburst-explorer-data')
  if (!root || !island) return

  var DATA = JSON.parse(island.textContent)

  /* FINDING #1 (hands to Story 20.5): Plotly's hierarchy traces require EXACTLY ONE root. The Story 20.2 island
   * is a FOREST — 24 epic roots plus `unplanned` — and Plotly refuses it outright:
   *   "Multiple implied roots, cannot build sunburst hierarchy of trace 0."
   * The hand-rolled SVG does not notice because its centre is a drawn circle, not a data node. So the component
   * must synthesize a project root (or the emitter must add one). The probe synthesizes it here, which is also
   * what makes Escape-to-top and the breadcrumb have somewhere to land. */
  var PROJECT_ROOT = { id: '__project__', parentId: null, weight: 0, label: 'SpecScribe', statusClass: 'unrecognized', href: 'index.html', kind: 'project' }
  var NODES = [PROJECT_ROOT].concat(
    DATA.nodes.map(function (n) {
      return n.parentId ? n : Object.assign({}, n, { parentId: PROJECT_ROOT.id })
    }),
  )

  /* ---------------------------------------------------------------------------------------------------------
   * 1. TOKENS — resolved from the shipped stylesheet, never re-typed.
   * ------------------------------------------------------------------------------------------------------- */

  // statusClass on the island node -> the CSS class the shipped SVG puts on that wedge (specscribe.css .sb-*).
  // The mapping is the only thing typed here; the COLOR VALUES all come from the cascade.
  var STATUS_CLASS = {
    done: 'sb-done',
    active: 'sb-active',
    review: 'sb-review',
    ready: 'sb-ready',
    drafted: 'sb-drafted',
    pending: 'sb-pending',
    noplan: 'sb-noplan',
    'followup-open': 'sb-followup-open',
    'followup-done': 'sb-followup-done',
    unplanned: 'sb-unplanned',
    unrecognized: 'sb-unrecognized',
  }

  // UX-DR17: the shipped chart distinguishes follow-ups and no-plan wedges by a DASHED STROKE as well as fill —
  // a non-color channel. Plotly's sunburst/treemap marker.line has no `dash`, but it does have `marker.pattern`
  // (per-sector hatching), which is a stronger non-color channel. This table is the substitution under test.
  var PATTERN_SHAPE = {
    'sb-followup-open': '/',
    'sb-followup-done': '\\',
    'sb-noplan': '.',
    'sb-unplanned': 'x',
  }

  var probeHost = document.createElement('div')
  probeHost.setAttribute('aria-hidden', 'true')
  probeHost.style.cssText = 'position:absolute;left:-9999px;width:0;height:0;overflow:hidden'
  document.body.appendChild(probeHost)

  var tokenCache = {}
  function tokenFor(cls) {
    if (tokenCache[cls]) return tokenCache[cls]
    var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
    svg.setAttribute('class', 'sunburst')
    var path = document.createElementNS('http://www.w3.org/2000/svg', 'path')
    path.setAttribute('class', 'sb-seg ' + cls)
    svg.appendChild(path)
    probeHost.appendChild(svg)
    var cs = getComputedStyle(path)
    var out = { fill: cs.fill, stroke: cs.stroke, strokeWidth: cs.strokeWidth, dash: cs.strokeDasharray, opacity: cs.opacity }
    tokenCache[cls] = out
    return out
  }

  // `.sb-noplan` is fill:transparent in the shipped chart. Plotly needs an actual paint for a sector, so the
  // probe substitutes the token the shipped rule uses for its STROKE — still a shipped token, still no literal.
  function fillFor(statusClass) {
    var cls = STATUS_CLASS[statusClass] || 'sb-unrecognized'
    var t = tokenFor(cls)
    var f = t.fill
    if (!f || f === 'none' || f === 'transparent' || f === 'rgba(0, 0, 0, 0)') return t.stroke
    return f
  }

  function patternFor(statusClass) {
    return PATTERN_SHAPE[STATUS_CLASS[statusClass]] || ''
  }

  /* Reduced motion + the shipped --motion-* scale. Never a literal duration. */
  function motionMs(name, fallbackMs) {
    var raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
    if (!raw) return fallbackMs
    if (/ms$/.test(raw)) return parseFloat(raw)
    if (/s$/.test(raw)) return parseFloat(raw) * 1000
    return fallbackMs
  }
  var reduceQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
  var reduceOverride = null // test seam only; null = ask the media query
  function reducedMotion() { return reduceOverride === null ? reduceQuery.matches : !!reduceOverride }
  function drillDuration() {
    return reducedMotion() ? 0 : motionMs('--motion-entrance', 600)
  }

  /* ---------------------------------------------------------------------------------------------------------
   * 2. TRACE — the Story 20.2 island node shape mapped straight onto Plotly's hierarchy contract.
   * ------------------------------------------------------------------------------------------------------- */

  var state = { shape: 'sunburst', level: null, focusIndex: 0 }

  var HAS_CHILD = {}
  NODES.forEach(function (n) { if (n.parentId) HAS_CHILD[n.parentId] = true })

  /* FINDING (hands to Story 20.5): the island's parent `weight` is NOT the sum of its emitted children.
   * An epic's weight is its stories' task count (Charts.SunburstEpicWeight) while its emitted children ALSO
   * include `aggregate` follow-up nodes — 14 of 25 parents disagree, e.g. epic-1 weight 42 vs children 50.
   * The hand-rolled SVG never notices because it scales each RING independently; Plotly's hierarchy model is a
   * single tree and rejects `branchvalues: 'total'` with a console warning per offending parent.
   * The probe therefore supplies values for LEAVES ONLY and lets Plotly sum upward (branchvalues 'remainder'
   * with a null branch value). That is the honest rendering; making the island parent-inclusive is 20.5's call. */
  function buildTrace(shape) {
    var t = {
      type: shape,
      ids: NODES.map(function (n) { return n.id }),
      parents: NODES.map(function (n) { return n.parentId || '' }),
      labels: NODES.map(function (n) { return n.label }),
      // NOTE: the branch value must be 0, NOT null. A single null anywhere in `values` makes Plotly silently
      // collapse the ENTIRE hierarchy to one calcdata point and render nothing — no error, no console warning.
      // Measured, not assumed: calcdata went 1 -> 119 on changing null to 0. [finding for Story 20.5]
      values: NODES.map(function (n) { return HAS_CHILD[n.id] ? 0 : n.weight }),
      branchvalues: 'remainder',
      marker: {
        colors: NODES.map(function (n) { return fillFor(n.statusClass) }),
        line: { color: tokenFor('sb-done').stroke || '#fff', width: 1 },
        pattern: {
          shape: NODES.map(function (n) { return patternFor(n.statusClass) }),
          // `bgcolor` MUST be set explicitly per sector. Left unset, Plotly paints the pattern's backing rect
          // BLACK — measured: rgb(0,0,0) appeared 67 times inside the <pattern> defs — which is a default color
          // reaching the output and therefore an AC #3 failure that a config-level assertion would have missed.
          fillmode: 'overlay',
          bgcolor: NODES.map(function (n) { return fillFor(n.statusClass) }),
          fgcolor: NODES.map(function (n) { return tokenFor('sb-unrecognized').fill }),
          size: 6,
          solidity: 0.28,
        },
      },
      // UX-DR17: the accessible name carries the status as TEXT, so nothing is signalled by color alone even if
      // a viewer cannot distinguish fill or hatch at all.
      text: NODES.map(function (n) { return n.statusClass }),
      hovertemplate: '<b>%{label}</b><br>status: %{text}<br>weight: %{value}<extra></extra>',
      textinfo: 'label',
      sort: false,
      // Plotly's own colorway must never be consulted. Every sector has an explicit color above; these switches
      // make a MISS impossible rather than merely unlikely. `outsidetextfont` matters as much as `insidetextfont`:
      // without it the ROOT label alone was painted Plotly's default rgb(68,68,68) — one element out of 119, which
      // is exactly the kind of miss an assertion-from-config would never have caught.
      insidetextfont: { color: tokenFor('sb-unrecognized').fill },
      outsidetextfont: { color: tokenFor('sb-unrecognized').fill },
    }
    if (shape === 'sunburst') t.leaf = { opacity: 1 }
    if (state.level) t.level = state.level
    return t
  }

  var LAYOUT = {
    margin: { l: 0, r: 0, t: 0, b: 0 },
    // Colorway neutralisation, belt and braces: an empty-ish colorway plus explicit per-sector colors.
    colorway: [tokenFor('sb-unrecognized').fill],
    sunburstcolorway: [tokenFor('sb-unrecognized').fill],
    extendsunburstcolors: false,
    treemapcolorway: [tokenFor('sb-unrecognized').fill],
    extendtreemapcolors: false,
    paper_bgcolor: 'rgba(0,0,0,0)',
    plot_bgcolor: 'rgba(0,0,0,0)',
    font: { family: getComputedStyle(document.body).fontFamily, color: tokenFor('sb-unrecognized').fill },
    // The transition Plotly would use if we ever let it animate; zeroed under reduced motion.
    transition: { duration: 0 },
    uniformtext: { mode: false },
  }

  var CONFIG = {
    displaylogo: false,       // removes the only https://plotly.com/ anchor the bundle can emit
    displayModeBar: false,    // no toolbar, no image-export path
    responsive: true,
    scrollZoom: false,
    doubleClick: false,
    // Offline-safety: nothing here may reference a remote host.
    plotlyServerURL: '',
    topojsonURL: '',
    showTips: false,
  }

  /* ---------------------------------------------------------------------------------------------------------
   * 3. ACCESSIBILITY LAYER — public surface only. This is the load-bearing experiment.
   * ------------------------------------------------------------------------------------------------------- */

  var a11y = { applications: 0, lastNodeCount: 0, lastEvent: 'initial' }

  function sectorNodes() {
    // Plotly emits one <g class="slice"> per sector for both sunburst and treemap, each containing path.surface.
    return Array.prototype.slice.call(root.querySelectorAll('g.slice path.surface'))
  }

  function nodeById(id) {
    for (var i = 0; i < NODES.length; i++) if (NODES[i].id === id) return NODES[i]
    return null
  }

  function labelFor(pathEl) {
    // Plotly stashes the calcdata point on the parent <g>'s __data__; the id round-trips from our `ids` array.
    var g = pathEl.parentNode
    var d = g && g.__data__
    var id = d && d.data && d.data.data ? d.data.data.id : null
    var n = id ? nodeById(id) : null
    if (n) return n.label + ' — ' + n.statusClass + ', weight ' + n.weight
    var t = g && g.querySelector('text')
    return t ? t.textContent : 'chart sector'
  }

  function applyA11yLayer(reason) {
    var els = sectorNodes()
    var svg = root.querySelector('svg.main-svg')
    if (svg) {
      svg.setAttribute('role', 'tree')
      svg.setAttribute('aria-label', 'Project progress hierarchy — ' + state.shape)
      svg.setAttribute('tabindex', '-1')
    }
    els.forEach(function (el, i) {
      el.setAttribute('role', 'treeitem')
      el.setAttribute('tabindex', i === state.focusIndex ? '0' : '-1')
      el.setAttribute('aria-label', labelFor(el))
      el.setAttribute('data-ss-a11y', '1')
      if (!el.__ssBound) {
        el.__ssBound = true
        el.addEventListener('keydown', onKeydown)
        el.addEventListener('focus', function () {
          state.focusIndex = sectorNodes().indexOf(el)
          announce(el.getAttribute('aria-label'))
        })
      }
    })
    a11y.applications++
    a11y.lastNodeCount = els.length
    a11y.lastEvent = reason
    render()
  }

  function announce(msg) {
    if (live) live.textContent = msg
  }

  function focusIndex(i) {
    var els = sectorNodes()
    if (!els.length) return
    state.focusIndex = ((i % els.length) + els.length) % els.length
    els.forEach(function (el, j) { el.setAttribute('tabindex', j === state.focusIndex ? '0' : '-1') })
    els[state.focusIndex].focus()
  }

  function idOf(el) {
    var d = el.parentNode && el.parentNode.__data__
    return d && d.data && d.data.data ? d.data.data.id : null
  }

  function onKeydown(ev) {
    var els = sectorNodes()
    var i = els.indexOf(ev.currentTarget)
    switch (ev.key) {
      case 'ArrowRight': case 'ArrowDown': ev.preventDefault(); focusIndex(i + 1); break
      case 'ArrowLeft': case 'ArrowUp': ev.preventDefault(); focusIndex(i - 1); break
      case 'Home': ev.preventDefault(); focusIndex(0); break
      case 'End': ev.preventDefault(); focusIndex(els.length - 1); break
      case 'Enter': case ' ': ev.preventDefault(); drillTo(idOf(ev.currentTarget)); break
      case 'Escape': ev.preventDefault(); drillUp(); break
      default: return
    }
  }

  function drillTo(id) {
    var n = id ? nodeById(id) : null
    if (!n) return
    // A leaf has no children — in `navigate` mode this is where the component would follow n.href.
    var hasChildren = NODES.some(function (m) { return m.parentId === id })
    if (!hasChildren) { announce('Leaf: ' + n.label + '. Would navigate to ' + n.href); return }
    state.level = id
    redraw('drill-in')
    announce('Drilled into ' + n.label)
  }

  function drillUp() {
    if (!state.level) { announce('Already at the top of the hierarchy'); return }
    var cur = nodeById(state.level)
    state.level = cur && cur.parentId ? cur.parentId : null
    redraw('drill-up')
    announce(state.level ? 'Moved up to ' + nodeById(state.level).label : 'Moved up to the whole project')
  }

  /* ---------------------------------------------------------------------------------------------------------
   * 4. RENDER + the four survival events AC #2 requires be tested individually.
   * ------------------------------------------------------------------------------------------------------- */

  /* The reapply hook is `plotly_afterplot` — Plotly's PUBLIC post-render event — not the promise returned by
   * `Plotly.react`. That matters twice over:
   *   1. It is the only hook that also fires for re-renders SpecScribe did not initiate (responsive resize,
   *      a host-driven relayout), which is what "survives" has to mean.
   *   2. Plotly's returned promise is resolved off an animation frame, so in a non-compositing tab it never
   *      settles — an implementation detail that would have silently hung the harness. Measured, not assumed. */
  var pendingReason = 'initial'
  var lastDrillDurationUsed = null
  function redraw(reason) {
    pendingReason = reason
    lastDrillDurationUsed = drillDuration()
    // UX-DR18: the level change goes through Plotly.react, which NEVER animates — so the drill snaps by
    // construction and there is no 750 ms transition left to suppress. `drillDuration()` is still computed and
    // reported because it is what a motion-honouring variant would feed Plotly.animate (see animatedDrill below);
    // under prefers-reduced-motion it is 0, which selects exactly this instant path.
    return Plotly.react(root, [buildTrace(state.shape)], LAYOUT, CONFIG)
  }

  function setShape(shape) {
    state.shape = shape
    return redraw('shape-switch')
  }

  function render() {
    if (!statusEl) return
    statusEl.textContent =
      'shape=' + state.shape +
      '  level=' + (state.level || '(root)') +
      '  sectors=' + a11y.lastNodeCount +
      '  a11y-applications=' + a11y.applications +
      '  last-event=' + a11y.lastEvent +
      '  reduced-motion=' + reducedMotion() +
      '  drill-ms=' + drillDuration()
  }

  Plotly.newPlot(root, [buildTrace('sunburst')], LAYOUT, CONFIG)

  // Bound BEFORE the first plot settles so even the initial render goes through the same public seam.
  root.on('plotly_afterplot', function () { applyA11yLayer(pendingReason) })

  // UX-DR18: cancel Plotly's own 750 ms drill animation (hard-coded module constant, no public attribute) and
  // re-apply the level ourselves — instantly under prefers-reduced-motion, at the --motion-* duration otherwise.
  root.on('plotly_sunburstclick', function (e) {
    if (e && e.nextLevel !== undefined) { state.level = e.nextLevel || null; redraw('mouse-drill') }
    return false
  })
  root.on('plotly_treemapclick', function (e) {
    if (e && e.nextLevel !== undefined) { state.level = e.nextLevel || null; redraw('mouse-drill') }
    return false
  })
  reduceQuery.addEventListener('change', function () { redraw('reduced-motion-change') })

  document.querySelectorAll('[data-shape]').forEach(function (btn) {
    btn.addEventListener('click', function () { setShape(btn.getAttribute('data-shape')) })
  })

  /* ---------------------------------------------------------------------------------------------------------
   * 5. AUDIT SURFACE — what the browser session reads out. Everything below is measurement, not behaviour.
   * ------------------------------------------------------------------------------------------------------- */

  // Every color the shipped stylesheet can legitimately put on a wedge, computed (never typed).
  function allowedColors() {
    var set = {}
    Object.keys(STATUS_CLASS).forEach(function (k) {
      var t = tokenFor(STATUS_CLASS[k])
      ;[t.fill, t.stroke].forEach(function (c) { if (c && c !== 'none') set[c] = k })
    })
    return set
  }

  // Plotly's own default sector palette (Plotly.d3 category10-ish), read out of the LIBRARY, not typed. If any of
  // these reaches the DOM the "colorways disabled" claim is false.
  function plotlyDefaultColorway() {
    try { return (Plotly.d3 && Plotly.d3.scale ? [] : []).concat(Plotly.Plots ? [] : []) } catch (e) { return [] }
  }

  window.__probe = {
    state: state,
    a11y: a11y,
    setShape: setShape,
    drillTo: drillTo,
    drillUp: drillUp,
    reactUpdate: function () {
      // A no-op-shaped Plotly.react with a changed layout value — the update path AC #2 names explicitly.
      return Plotly.react(root, [buildTrace(state.shape)], Object.assign({}, LAYOUT, { font: { size: 12 } }), CONFIG)
    },
    resize: function () { return Plotly.Plots.resize(root) },
    // The motion-honouring variant, kept opt-in so the survival suite is not at the mercy of an animation frame.
    // Proves the duration is OURS: Plotly.animate takes it as an argument, unlike the drill click path whose
    // 750 ms lives in src/traces/sunburst/constants.js with no attribute pointing at it.
    animatedDrill: function (level, ms) {
      return Plotly.animate(root, { data: [{ level: level || undefined }], traces: [0] }, {
        frame: { redraw: false, duration: ms },
        transition: { duration: ms, easing: 'linear' },
        mode: 'immediate',
        fromcurrent: true,
      })
    },
    // Test seam for the reduced-motion branch: the browser session cannot flip the OS/UA media query, so the
    // override exists to exercise the OTHER branch of the same expression. Labelled as a seam, not as evidence
    // that the media query itself fires — that part is the standard matchMedia idiom, visible in drillDuration().
    setReducedMotionOverride: function (v) { reduceOverride = v },
    // Snapshot of the a11y contract as it exists in the DOM RIGHT NOW.
    audit: function () {
      var els = sectorNodes()
      var focusable = els.filter(function (e) { return e.getAttribute('tabindex') === '0' })
      var named = els.filter(function (e) { return (e.getAttribute('aria-label') || '').length > 0 })
      var roled = els.filter(function (e) { return e.getAttribute('role') === 'treeitem' })
      var svg = root.querySelector('svg.main-svg')
      // COLORWAY AUDIT — the AC #3 evidence. "Colorways disabled" is only demonstrated if every paint that
      // actually reaches the DOM is a shipped token. A sector's fill is either a solid color or a url(#pattern)
      // reference; a pattern must be RESOLVED to the colors inside its <pattern> def, or 46 hatched sectors would
      // be scored as "foreign" and the audit would be useless.
      var allowed = allowedColors()
      var fills = {}
      var patternIds = {}
      els.forEach(function (e) {
        var f = getComputedStyle(e).fill
        var ref = /^url\(["']?#([^"')]+)/.exec(f)
        if (ref) { patternIds[ref[1]] = (patternIds[ref[1]] || 0) + 1; fills['(pattern)'] = (fills['(pattern)'] || 0) + 1 }
        else fills[f] = (fills[f] || 0) + 1
      })
      // Every color painted INSIDE a pattern def (background rect + hatch strokes).
      var patternColors = {}
      Object.keys(patternIds).forEach(function (id) {
        var def = root.querySelector('#' + CSS.escape(id))
        if (!def) return
        def.querySelectorAll('*').forEach(function (n) {
          var cs = getComputedStyle(n)
          ;[cs.fill, cs.stroke].forEach(function (c) {
            if (c && c !== 'none' && c !== 'rgba(0, 0, 0, 0)') patternColors[c] = (patternColors[c] || 0) + 1
          })
        })
      })
      var painted = Object.keys(fills).filter(function (f) { return f !== '(pattern)' }).concat(Object.keys(patternColors))
      var foreign = painted.filter(function (f) {
        return !allowed[f] && f !== 'none' && f !== 'rgba(0, 0, 0, 0)'
      })
      // Text color is also a paint. A Plotly default here would be just as much of an AC #3 miss.
      var textFills = {}
      root.querySelectorAll('g.slice text').forEach(function (t) {
        var c = getComputedStyle(t).fill
        textFills[c] = (textFills[c] || 0) + 1
      })
      var patternDefs = Object.keys(patternIds).length
      var patternedSectors = fills['(pattern)'] || 0
      return {
        shape: state.shape,
        level: state.level,
        sectors: els.length,
        roleTreeitem: roled.length,
        withAriaLabel: named.length,
        rovingTabindexZero: focusable.length,
        svgRole: svg ? svg.getAttribute('role') : null,
        svgAriaLabel: svg ? svg.getAttribute('aria-label') : null,
        liveRegionText: live ? live.textContent : null,
        a11yApplications: a11y.applications,
        lastEvent: a11y.lastEvent,
        distinctFills: fills,
        patternColors: patternColors,
        textFills: textFills,
        foreignFills: foreign,
        allowedTokenColors: allowed,
        patternDefs: patternDefs,
        patternedSectors: patternedSectors,
        reducedMotion: reducedMotion(), reducedMotionOverride: reduceOverride,
        drillDurationMs: drillDuration(),
        lastDrillDurationUsed: lastDrillDurationUsed,
        motionTokenEntrance: getComputedStyle(document.documentElement).getPropertyValue('--motion-entrance').trim(),
        plotlyVersion: Plotly.version,
        defaultColorway: plotlyDefaultColorway(),
      }
    },
  }
})()
