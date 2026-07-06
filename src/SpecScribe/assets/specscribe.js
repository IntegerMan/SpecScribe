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
    // Clamp within the viewport so an edge segment's tooltip never spills off-screen.
    var pad = 12;
    var rect = t.getBoundingClientRect();
    var left = Math.min(Math.max(pad, x + 14), window.innerWidth - rect.width - pad);
    var top = Math.max(pad, y - rect.height - 12);
    t.style.left = left + "px";
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

  // Keyboard focus: a focused chart link shows the tooltip anchored to its own box.
  document.addEventListener("focusin", function (e) {
    var link = e.target.closest ? e.target.closest("a") : null;
    var seg = link ? link.querySelector(SEG) : null;
    if (seg) {
      var r = seg.getBoundingClientRect();
      showTip(seg, r.left + r.width / 2, r.top);
    }
  });
  document.addEventListener("focusout", hideTip);
  document.addEventListener("scroll", hideTip, true);

  // Touch: first tap on a chart link shows the tooltip; a second tap on the same link follows it. Gives
  // touch users the chart detail that used to hide behind a hover-only <title>.
  var lastTapped = null;
  document.addEventListener("touchstart", function (e) {
    var link = e.target.closest ? e.target.closest("a") : null;
    var seg = link ? link.querySelector(SEG) : (e.target.closest ? e.target.closest(SEG) : null);
    if (!seg) { hideTip(); lastTapped = null; return; }
    if (link && lastTapped !== link) {
      e.preventDefault();
      var r = seg.getBoundingClientRect();
      showTip(seg, r.left + r.width / 2, r.top);
      lastTapped = link;
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
      var prev = btn.getAttribute("aria-label") || "Copy";
      btn.classList.add("copied");
      btn.setAttribute("aria-label", "Copied");
      window.setTimeout(function () {
        btn.classList.remove("copied");
        btn.setAttribute("aria-label", prev);
      }, 1600);
    }).catch(function () { /* best-effort — the visible command is still selectable */ });
  });
})();
