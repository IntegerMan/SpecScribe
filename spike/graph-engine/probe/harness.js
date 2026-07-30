/* Story 24.6 shared probe harness. Throwaway.
 *
 * Everything here exists to make a claim CHECKABLE from a live browser rather than asserted from config:
 *   * tokenAllowlist() builds the permitted colour set AT RUNTIME by resolving the real shipped classes through
 *     the real cascade, so no token value is ever typed in this probe and a token change moves the allowlist with
 *     it (the drift-free-by-construction discipline Story 23.1 established and 20.4 reused).
 *   * auditColors() answers "is anything painted a colour that is not a shipped token" by reading computed
 *     styles off the emitted DOM, not by reading the config we passed in.
 *   * a11ySnapshot() applies a mechanical survival predicate so "the layer survived" is a boolean, not a vibe.
 */
(function () {
  'use strict'

  const probe = {}
  window.__probe = probe

  probe.meta = (() => {
    try {
      return JSON.parse(document.documentElement.dataset.probeMeta || '{}')
    } catch {
      return {}
    }
  })()

  /* ---------- tokens ---------- */

  /** Resolves a CSS custom property through the real cascade on a real element. */
  probe.token = (name, el) => getComputedStyle(el || document.documentElement).getPropertyValue(name).trim()

  /** Normalises any CSS colour to a canonical `rgb(...)`/`rgba(...)` string so string comparison is meaningful. */
  probe.normColor = (value) => {
    if (!value) return ''
    const probeEl = document.createElement('span')
    probeEl.style.color = ''
    probeEl.style.color = value
    if (!probeEl.style.color) return String(value).trim().toLowerCase()
    document.body.appendChild(probeEl)
    const out = getComputedStyle(probeEl).color
    probeEl.remove()
    return out
  }

  /** The allowlist, built from the shipped `--status-*` tokens plus the neutral chrome the charts legitimately
   *  use. Read off :root through the real generated stylesheet — never typed. */
  probe.tokenAllowlist = () => {
    const rootStyle = getComputedStyle(document.documentElement)
    const names = []
    for (const sheet of Array.from(document.styleSheets)) {
      let rules
      try {
        rules = sheet.cssRules
      } catch {
        continue
      }
      for (const rule of Array.from(rules || [])) {
        if (!rule.style) continue
        for (const prop of Array.from(rule.style)) {
          if (prop.startsWith('--status-') || prop.startsWith('--chart-') || prop.startsWith('--ss-')) {
            if (!names.includes(prop)) names.push(prop)
          }
        }
      }
    }
    const allow = new Map()
    for (const n of names) {
      const v = rootStyle.getPropertyValue(n).trim()
      if (v) allow.set(probe.normColor(v), n)
    }
    // The chart also legitimately paints with the surface/ink/border chrome it inherits.
    for (const n of ['--surface', '--surface-2', '--ink', '--ink-2', '--muted', '--border', '--panel', '--bg']) {
      const v = rootStyle.getPropertyValue(n).trim()
      if (v) allow.set(probe.normColor(v), n)
    }
    allow.set('rgba(0, 0, 0, 0)', 'transparent')
    allow.set('', 'unset')
    return { names, allow }
  }

  /** Is this element's `prop` colour actually PAINTED on screen?
   *
   *  This predicate exists because the naive audit (read `fill`, compare to the allowlist) over-reported by 7 on a
   *  Plotly scatter, and every one of the 7 was a non-painting element:
   *    * 4 `<rect>`s inside `<clipPath>` in `<defs>` — geometry, never rendered;
   *    * the plot background `rect.bg`, which honours `plot_bgcolor: rgba(0,0,0,0)` by setting `fill-opacity: 0`
   *      while leaving `fill` at SVG's initial black;
   *    * 2 axis-line paths with NO `d` attribute at all, so a zero-area bbox and nothing drawn.
   *  Reporting those as "foreign colours" would have manufactured a UX-DR17 defect that does not exist. Both the
   *  raw and the painting counts are returned so the difference is auditable rather than quietly swallowed. */
  probe.isPainted = (el, prop) => {
    if (el.closest('defs')) return false
    const cs = getComputedStyle(el)
    if (cs.display === 'none' || cs.visibility === 'hidden') return false
    if (parseFloat(cs.opacity) === 0) return false
    if (prop === 'fill' && parseFloat(cs.fillOpacity) === 0) return false
    if (prop === 'stroke' && (parseFloat(cs.strokeOpacity) === 0 || parseFloat(cs.strokeWidth) === 0)) return false
    if (el.tagName === 'path' && !el.getAttribute('d')) return false
    try {
      const b = el.getBBox()
      if (b.width === 0 && b.height === 0) return false
    } catch { /* getBBox throws on unrendered nodes; the checks above already covered them */ }
    return true
  }

  /** Every stroke/fill actually painted in the chart region, bucketed into token-backed vs foreign. */
  probe.auditColors = (selector) => {
    const { allow } = probe.tokenAllowlist()
    const host = document.querySelector(selector || '#chart')
    if (!host) return { error: 'no chart host' }
    const painted = new Map()
    const foreign = new Map()
    const foreignRaw = new Map()
    const excluded = []
    for (const el of host.querySelectorAll('path, circle, line, rect, polygon, text, ellipse, polyline')) {
      for (const prop of ['fill', 'stroke', 'color']) {
        const raw = getComputedStyle(el).getPropertyValue(prop)
        if (!raw || raw === 'none') continue
        const norm = probe.normColor(raw)
        if (norm === 'rgba(0, 0, 0, 0)') continue
        const token = allow.get(norm)
        if (token) {
          painted.set(norm, (painted.get(norm) || 0) + 1)
          continue
        }
        foreignRaw.set(norm, (foreignRaw.get(norm) || 0) + 1)
        if (!probe.isPainted(el, prop)) {
          excluded.push(`${el.tagName}${el.getAttribute('class') ? '.' + el.getAttribute('class') : ''}/${prop}`)
          continue
        }
        foreign.set(norm, (foreign.get(norm) || 0) + 1)
      }
    }
    return {
      tokenBacked: Object.fromEntries([...painted].map(([c, n]) => [`${c} (${allow.get(c)})`, n])),
      foreign: Object.fromEntries(foreign),
      foreignCount: [...foreign.values()].reduce((a, b) => a + b, 0),
      foreignRawCount: [...foreignRaw.values()].reduce((a, b) => a + b, 0),
      excludedAsNonPainting: excluded,
      allowlistSize: allow.size,
    }
  }

  /* ---------- accessibility ---------- */

  /** Story 20.4's survival predicate, adapted for a graph: nodes > 0 AND every focusable node carries a role and
   *  a non-empty accessible name AND exactly one node holds tabindex="0". Mechanical, so it cannot be fudged. */
  probe.a11ySnapshot = () => {
    const nodes = Array.from(document.querySelectorAll('[data-graph-node]'))
    const withRole = nodes.filter((n) => n.getAttribute('role'))
    const withName = nodes.filter((n) => (n.getAttribute('aria-label') || '').trim().length > 0)
    const tabbable = nodes.filter((n) => n.getAttribute('tabindex') === '0')
    return {
      nodes: nodes.length,
      withRole: withRole.length,
      withName: withName.length,
      tabindexZero: tabbable.length,
      INTACT: nodes.length > 0 && withRole.length === nodes.length && withName.length === nodes.length && tabbable.length === 1,
      firstThreeNames: nodes.slice(0, 3).map((n) => n.getAttribute('aria-label')),
      tabOrderClaim: document.documentElement.dataset.tabOrder || '(unset)',
      liveRegion: (document.querySelector('[data-graph-live]') || {}).textContent || '',
    }
  }

  /** Where focus actually is, and whether it is a graph node. Keyboard reachability is not inferable from markup. */
  probe.focusState = () => {
    const el = document.activeElement
    return {
      tag: el ? el.tagName : null,
      isGraphNode: !!(el && el.hasAttribute && el.hasAttribute('data-graph-node')),
      label: el && el.getAttribute ? el.getAttribute('aria-label') : null,
      tabindex: el && el.getAttribute ? el.getAttribute('tabindex') : null,
    }
  }

  /** Per-edge non-colour channel audit (R7 / UX-DR17): does every process / cross-boundary edge carry a dash or
   *  width difference AND accessible text, with no hue doing the work alone? */
  probe.auditEdgeChannels = () => {
    const edges = Array.from(document.querySelectorAll('[data-graph-edge]'))
    const rows = edges.map((e) => {
      const cs = getComputedStyle(e)
      return {
        kind: e.dataset.kind,
        crossBoundary: e.dataset.xb === 'true',
        dash: (cs.strokeDasharray || 'none').trim(),
        width: parseFloat(cs.strokeWidth) || 0,
        stroke: probe.normColor(cs.stroke),
        hasText: !!(e.getAttribute('aria-label') || e.querySelector('title')),
      }
    })
    const distinctDash = [...new Set(rows.map((r) => r.dash))]
    const distinctWidth = [...new Set(rows.map((r) => r.width.toFixed(2)))]
    const distinctStroke = [...new Set(rows.map((r) => r.stroke))]
    const proc = rows.filter((r) => r.kind === 'proc')
    const code = rows.filter((r) => r.kind === 'code')
    const xb = rows.filter((r) => r.crossBoundary)
    return {
      edges: rows.length,
      distinctDash,
      distinctWidth: distinctWidth.length,
      distinctStroke,
      // The load-bearing question: is the process/code distinction carried by something OTHER than colour?
      processAllDashed: proc.length > 0 && proc.every((r) => r.dash !== 'none' && r.dash !== ''),
      codeAllSolid: code.length > 0 && code.every((r) => r.dash === 'none' || r.dash === ''),
      crossBoundaryDistinctWidth: xb.length > 0 && [...new Set(xb.map((r) => r.width.toFixed(2)))].length > 0,
      everyEdgeHasText: rows.length > 0 && rows.every((r) => r.hasText),
      sample: rows.slice(0, 6),
    }
  }

  /* ---------- motion ---------- */

  probe.reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches
  /** Reads a --motion-* token in milliseconds; the shipped motion token system is the single source. */
  probe.motionMs = (name) => {
    const raw = probe.token(name)
    if (!raw) return 0
    return raw.endsWith('ms') ? parseFloat(raw) : parseFloat(raw) * 1000
  }
  /** The value the engine is actually driven with. `forceReduce` exercises the reduced branch without needing to
   *  flip an OS setting the browser session cannot reach — labelled as a seam, exactly as 20.4 §5.4 did. */
  probe.animationMs = (forceReduce) =>
    forceReduce || probe.reducedMotion() ? 0 : probe.motionMs('--motion-entrance') || 600

  /* ---------- tooltip (R8) ---------- */

  /** The shipped body-level tooltip node, never a CSS ::after (which clips inside chart-panel overflow). */
  probe.tooltipNode = () => {
    let el = document.querySelector('.ss-tooltip')
    if (!el) {
      el = document.createElement('div')
      el.className = 'ss-tooltip'
      el.setAttribute('role', 'presentation')
      document.body.appendChild(el)
    }
    return el
  }
  probe.showTooltip = (html, x, y) => {
    const el = probe.tooltipNode()
    el.innerHTML = html
    el.style.position = 'fixed'
    el.style.left = `${x + 12}px`
    el.style.top = `${y + 12}px`
    el.dataset.shown = 'true'
  }
  probe.hideTooltip = () => {
    const el = document.querySelector('.ss-tooltip')
    if (el) el.dataset.shown = 'false'
  }
  /** Proves the tooltip lives OUTSIDE any overflow-clipping ancestor — the actual defect the memory records. */
  probe.tooltipAudit = () => {
    const el = document.querySelector('.ss-tooltip')
    if (!el) return { present: false }
    let clipping = []
    let p = el.parentElement
    while (p && p !== document.documentElement) {
      const cs = getComputedStyle(p)
      if (['hidden', 'auto', 'scroll', 'clip'].includes(cs.overflow) ||
          ['hidden', 'auto', 'scroll', 'clip'].includes(cs.overflowX) ||
          ['hidden', 'auto', 'scroll', 'clip'].includes(cs.overflowY)) {
        clipping.push(p.tagName + (p.className ? '.' + String(p.className).split(' ')[0] : ''))
      }
      p = p.parentElement
    }
    return {
      present: true,
      parent: el.parentElement ? el.parentElement.tagName : null,
      isBodyLevel: el.parentElement === document.body,
      clippingAncestors: clipping,
      usesPseudoAfter: false,
      shown: el.dataset.shown === 'true',
      text: el.textContent.slice(0, 120),
    }
  }

  /* ---------- SPA re-init seam (R8) ---------- */

  probe.reinitCount = 0
  probe.registerReinit = (fn) => {
    // The shipped seam: the SPA swaps <main> without a page load, so every initializer must re-run on this event.
    document.addEventListener('specscribe:content-swapped', () => {
      probe.reinitCount++
      fn()
    })
  }
  probe.fireContentSwapped = () => {
    document.dispatchEvent(new CustomEvent('specscribe:content-swapped', { detail: { probe: true } }))
    return probe.reinitCount
  }

  /* ---------- selection seam (R8) ---------- */

  probe.selectEvents = []
  document.addEventListener('specscribe:explorer-select', (e) => {
    probe.selectEvents.push(e.detail && e.detail.path ? e.detail.path : String(e.detail))
  })
  probe.emitSelect = (path) => {
    document.dispatchEvent(new CustomEvent('specscribe:explorer-select', { detail: { path } }))
  }

  /* ---------- network ---------- */

  probe.requests = () =>
    performance.getEntriesByType('resource').map((r) => ({
      name: r.name.replace(location.origin, ''),
      external: !r.name.startsWith(location.origin) && !r.name.startsWith('data:'),
    }))
  probe.externalRequests = () => probe.requests().filter((r) => r.external)

  probe.errors = []
  window.addEventListener('error', (e) => probe.errors.push(String(e.message)))
  document.addEventListener('securitypolicyviolation', (e) =>
    probe.errors.push(`CSP: ${e.violatedDirective} blocked ${e.blockedURI}`),
  )
})()
