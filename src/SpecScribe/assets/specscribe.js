/* SpecScribe progressive-enhancement script — the ONE sanctioned client-side addition (Story 1.5 Task 3).
   Two jobs, both dependency-free and static-host-safe:
     1. On-brand tooltips for SVG chart segments + heatmap cells, reading their existing <title> text so the
        native tooltip and aria-label stay as the no-JS / screen-reader fallback.
     2. Copy buttons on the "Next Steps" commands.
   Everything degrades gracefully: with JS off, <title> tooltips and the visible <code> command remain. */
(function () {
  "use strict";

  // ---- On-brand tooltip for SVG segments -------------------------------------------------
  // A single reused tooltip element positioned near the pointer/focus. Text comes from the segment's
  // <title>, so we never duplicate label strings into markup.
  var tip = null;

  function ensureTip() {
    if (!tip) {
      tip = document.createElement("div");
      tip.className = "ss-tooltip";
      tip.setAttribute("role", "tooltip");
      tip.hidden = true;
      document.body.appendChild(tip);
    }
    return tip;
  }

  function tipText(el) {
    var t = el.querySelector("title");
    return t ? t.textContent : el.getAttribute("aria-label");
  }

  function showTip(el, x, y) {
    var text = tipText(el);
    if (!text) return;
    var t = ensureTip();
    t.textContent = text;
    t.hidden = false;
    // Clamp within the viewport so an edge segment's tooltip never spills off-screen. `x`/`y` are viewport
    // (client) coords; the tooltip is absolutely positioned against the body, so convert BOTH axes to page
    // coords with scrollX/scrollY — otherwise a horizontally-scrolled page misplaces the tooltip.
    var pad = 12;
    var rect = t.getBoundingClientRect();
    var left = Math.min(Math.max(pad, x + 14), window.innerWidth - rect.width - pad);
    var top = Math.max(pad, y - rect.height - 12);
    t.style.left = (left + window.scrollX) + "px";
    t.style.top = (top + window.scrollY) + "px";
  }

  function hideTip() {
    if (tip) tip.hidden = true;
  }

  var SEG = ".sb-seg, .heatmap-cell, .donut-seg";

  document.addEventListener("mouseover", function (e) {
    var seg = e.target.closest ? e.target.closest(SEG) : null;
    if (seg) showTip(seg, e.clientX, e.clientY);
  });
  document.addEventListener("mousemove", function (e) {
    if (!tip || tip.hidden) return;
    var seg = e.target.closest ? e.target.closest(SEG) : null;
    if (seg) showTip(seg, e.clientX, e.clientY);
  });
  document.addEventListener("mouseout", function (e) {
    var seg = e.target.closest ? e.target.closest(SEG) : null;
    if (seg) hideTip();
  });

  // Keyboard focus: a focused chart segment shows the tooltip anchored to its own box. This covers both the
  // link-wrapped sunburst segments AND directly-focusable segments (donut slices carry tabindex=0), so the
  // on-brand tooltip is keyboard-reachable beyond the sunburst. Heatmap cells stay non-focusable by design
  // (a ~100-cell tab order would be a trap) — their whole-chart aria-label is the keyboard/SR affordance.
  document.addEventListener("focusin", function (e) {
    if (!e.target.closest) return;
    var link = e.target.closest("a");
    var seg = link ? link.querySelector(SEG) : e.target.closest(SEG);
    if (seg) {
      var r = seg.getBoundingClientRect();
      showTip(seg, r.left + r.width / 2, r.top);
    }
  });
  document.addEventListener("focusout", hideTip);
  document.addEventListener("scroll", hideTip, true);

  // Touch: give touch users the chart detail that used to hide behind a hover-only <title>. For a link-wrapped
  // segment (sunburst) the first tap shows the tooltip and a second tap on the same link follows it; for a bare
  // segment (donut slice, heatmap cell) a tap simply shows the tooltip. Either way, a tap elsewhere dismisses it.
  var lastTapped = null;
  document.addEventListener("touchstart", function (e) {
    if (!e.target.closest) return;
    var link = e.target.closest("a");
    var seg = link ? link.querySelector(SEG) : e.target.closest(SEG);
    if (!seg) { hideTip(); lastTapped = null; return; }
    if (link) {
      if (lastTapped !== link) {
        // First tap on this link: show the tooltip instead of navigating.
        e.preventDefault();
        var r = seg.getBoundingClientRect();
        showTip(seg, r.left + r.width / 2, r.top);
        lastTapped = link;
      } else {
        // Second tap: let the navigation proceed, but don't strand the tooltip on the way out.
        hideTip();
      }
    } else {
      // Bare segment with no link — just reveal its detail on tap.
      var rb = seg.getBoundingClientRect();
      showTip(seg, rb.left + rb.width / 2, rb.top);
      lastTapped = null;
    }
  }, { passive: false });

  // ---- Copy buttons on the Next Steps commands -------------------------------------------
  function copyText(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      return navigator.clipboard.writeText(text);
    }
    // Fallback for non-secure contexts (file://, plain http) where the async Clipboard API is unavailable.
    return new Promise(function (resolve, reject) {
      try {
        var ta = document.createElement("textarea");
        ta.value = text;
        ta.setAttribute("readonly", "");
        ta.style.position = "absolute";
        ta.style.left = "-9999px";
        document.body.appendChild(ta);
        ta.select();
        document.execCommand("copy");
        document.body.removeChild(ta);
        resolve();
      } catch (err) {
        reject(err);
      }
    });
  }

  document.addEventListener("click", function (e) {
    var btn = e.target.closest ? e.target.closest(".copy-btn") : null;
    if (!btn) return;
    e.preventDefault();
    var text = btn.getAttribute("data-copy");
    if (!text) return;
    copyText(text).then(function () {
      // Capture the resting label ONCE (the first click), so a rapid second click within the reset window
      // doesn't record "Copied" as the label to restore — which would leave the button announcing "Copied"
      // to screen readers permanently. Also clear any pending reset before scheduling a fresh one.
      if (!btn.hasAttribute("data-copy-label")) {
        btn.setAttribute("data-copy-label", btn.getAttribute("aria-label") || "Copy");
      }
      if (btn._copyResetTimer) { window.clearTimeout(btn._copyResetTimer); }
      btn.classList.add("copied");
      btn.setAttribute("aria-label", "Copied");
      btn._copyResetTimer = window.setTimeout(function () {
        btn.classList.remove("copied");
        btn.setAttribute("aria-label", btn.getAttribute("data-copy-label"));
        btn._copyResetTimer = null;
      }, 1600);
    }).catch(function () { /* best-effort — the visible command is still selectable */ });
  });
})();
