/* Story 20.4 Task 7 — the UX-DR7 survival harness.
 *
 * The a11y decision rule in the story hangs PASS (configured around) vs FAIL on ONE question: does a
 * roving-tabindex layer applied over Plotly's emitted <path> nodes SURVIVE Plotly re-rendering them? A layer that
 * works until the first drill is a FAIL, not a pass. So each named event is driven individually and the DOM is
 * re-audited after each — no event's verdict is inferred from another's.
 *
 * Everything here WAITS ON THE DOM, never on a Plotly promise: Plotly resolves those off an animation frame, so
 * a non-compositing tab hangs the harness forever instead of failing. That is an environment fact worth knowing,
 * not a Plotly defect.
 *
 * Run from the browser session:  window.__runSurvival()   then read window.__res
 */
;(function () {
  'use strict'

  function snap(label, extra) {
    var a = window.__probe.audit()
    return Object.assign({
      step: label,
      shape: a.shape,
      level: a.level,
      sectors: a.sectors,
      roleTreeitem: a.roleTreeitem,
      withAriaLabel: a.withAriaLabel,
      rovingTabindexZero: a.rovingTabindexZero,
      svgRole: a.svgRole,
      svgAriaLabel: a.svgAriaLabel,
      foreignFills: a.foreignFills,
      liveRegionText: a.liveRegionText,
      a11yApplications: a.a11yApplications,
      // The survival predicate. Every sector still carries role + accessible name, and exactly one is tabbable.
      INTACT: a.sectors > 0 && a.roleTreeitem === a.sectors && a.withAriaLabel === a.sectors && a.rovingTabindexZero === 1,
    }, extra || {})
  }

  var sleep = function (ms) { return new Promise(function (r) { setTimeout(r, ms) }) }

  // Wait until the a11y layer has been (re)applied, or give up. Returns whether it reapplied at all.
  async function awaitReapply(fromCount, budgetMs) {
    var t = 0
    while (t < (budgetMs || 2500)) {
      if (window.__probe.a11y.applications > fromCount) return true
      await sleep(50); t += 50
    }
    return false
  }

  function sectors() { return Array.prototype.slice.call(document.querySelectorAll('#probe-chart g.slice path.surface')) }
  function branchSector() { return sectors().find(function (el) { return /^Epic \d+/.test(el.getAttribute('aria-label') || '') }) }
  function key(el, k) { el.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true })) }

  window.__runSurvival = function () {
    window.__res = null
    ;(async function () {
      var steps = []
      steps.push(snap('0. initial render'))

      // --- keyboard reachability, before anything re-renders ----------------------------------------------
      var s = sectors()[0]
      s.focus()
      var landed = document.activeElement === s
      key(s, 'ArrowRight')
      await sleep(80)
      var moved = document.activeElement !== s && !!(document.activeElement && document.activeElement.closest('#probe-chart'))
      steps.push(snap('1. keyboard reachability', { focusLandsOnSector: landed, arrowMovesFocus: moved, focusedName: document.activeElement.getAttribute('aria-label') }))

      // --- EVENT 1: drill-in via Enter --------------------------------------------------------------------
      var before = window.__probe.a11y.applications
      var lvl0 = window.__probe.state.level
      var b = branchSector()
      b.focus()
      key(b, 'Enter')
      var re1 = await awaitReapply(before)
      steps.push(snap('2. EVENT drill-in (Enter on an epic)', { layerReapplied: re1, levelChanged: window.__probe.state.level !== lvl0 }))

      // --- EVENT 2: Escape back up ------------------------------------------------------------------------
      before = window.__probe.a11y.applications
      var lvl1 = window.__probe.state.level
      sectors()[0].focus()
      key(sectors()[0], 'Escape')
      var re2 = await awaitReapply(before)
      steps.push(snap('3. EVENT drill-up (Escape)', { layerReapplied: re2, levelChanged: window.__probe.state.level !== lvl1 }))

      // --- EVENT 3: shape switch sunburst -> treemap -------------------------------------------------------
      before = window.__probe.a11y.applications
      window.__probe.setShape('treemap')
      var re3 = await awaitReapply(before)
      steps.push(snap('4. EVENT shape switch -> treemap', { layerReapplied: re3 }))

      // --- drill inside treemap, to prove the layer is not sunburst-only -----------------------------------
      before = window.__probe.a11y.applications
      var tb = branchSector()
      if (tb) { tb.focus(); key(tb, 'Enter') }
      var re4 = await awaitReapply(before)
      steps.push(snap('5. EVENT drill-in inside treemap', { layerReapplied: re4, drove: !!tb }))

      before = window.__probe.a11y.applications
      window.__probe.setShape('sunburst')
      await awaitReapply(before)
      steps.push(snap('6. EVENT shape switch back -> sunburst'))

      // --- EVENT 4: resize ---------------------------------------------------------------------------------
      before = window.__probe.a11y.applications
      var chart = document.getElementById('probe-chart')
      chart.style.width = '420px'
      window.__probe.resize()
      var re5 = await awaitReapply(before)
      steps.push(snap('7. EVENT resize (Plotly.Plots.resize)', { layerReapplied: re5 }))
      chart.style.width = '640px'
      window.__probe.resize()
      await sleep(200)

      // --- EVENT 5: a bare Plotly.react the component did not initiate --------------------------------------
      // The adversarial case. If the layer only survives because OUR redraw() reapplies it, a foreign update
      // would strip it. It goes through plotly_afterplot too — that is exactly the claim under test.
      before = window.__probe.a11y.applications
      window.__probe.reactUpdate()
      var re6 = await awaitReapply(before)
      steps.push(snap('8. EVENT bare Plotly.react (not initiated by the component)', { layerReapplied: re6 }))

      // --- EVENT 6: Plotly.relayout, a third independent update path ---------------------------------------
      before = window.__probe.a11y.applications
      Plotly.relayout(document.getElementById('probe-chart'), { 'margin.t': 4 })
      var re7 = await awaitReapply(before)
      steps.push(snap('9. EVENT Plotly.relayout', { layerReapplied: re7 }))

      window.__res = steps
    })().catch(function (e) { window.__res = [{ error: String((e && e.stack) || e) }] })
    return 'running'
  }
})()
