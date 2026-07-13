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
  // <title>, so we never duplicate label strings into markup. While our tooltip is showing we detach the
  // <title> node from the DOM — otherwise the browser's own native tooltip fires after its hover delay and
  // shows alongside ours. The node is reattached on hide/blur so <title> remains the no-JS/SR fallback.
  var tip = null;
  var activeSeg = null;
  var activeTitle = null;
  var activeText = null;

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

  function activate(el) {
    if (activeSeg === el) return;
    deactivate();
    // HTML elements opt into the (body-level, never-clipped) tooltip via data-tip — used for rich, multi-line
    // card/wheel tips that a clipped CSS ::after can't show. SVG segments keep the <title> path.
    var dataTip = el.getAttribute ? el.getAttribute("data-tip") : null;
    if (dataTip) {
      activeSeg = el;
      activeTitle = null;
      activeText = dataTip;
      return;
    }
    var t = el.querySelector("title");
    activeSeg = el;
    activeTitle = t;
    activeText = t ? t.textContent : el.getAttribute("aria-label");
    if (t) t.remove();
  }

  function deactivate() {
    if (activeTitle && activeSeg) activeSeg.insertBefore(activeTitle, activeSeg.firstChild);
    activeSeg = null;
    activeTitle = null;
    activeText = null;
  }

  function showTip(el, x, y) {
    activate(el);
    if (!activeText) { deactivate(); return; }
    var t = ensureTip();
    t.textContent = activeText;
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
    deactivate();
  }

  var SEG = ".sb-seg, .heatmap-cell, .donut-seg";
  // Hover/focus/touch also fire on HTML elements that opt in with .js-tip (rich card/wheel tooltips).
  var HOVER = SEG + ", .js-tip";

  document.addEventListener("mouseover", function (e) {
    var seg = e.target.closest ? e.target.closest(HOVER) : null;
    if (seg) showTip(seg, e.clientX, e.clientY);
  });
  document.addEventListener("mousemove", function (e) {
    if (!tip || tip.hidden) return;
    var seg = e.target.closest ? e.target.closest(HOVER) : null;
    if (seg) showTip(seg, e.clientX, e.clientY);
  });
  document.addEventListener("mouseout", function (e) {
    var seg = e.target.closest ? e.target.closest(HOVER) : null;
    if (seg) hideTip();
  });

  // Keyboard focus: a focused chart segment shows the tooltip anchored to its own box. This covers both the
  // link-wrapped sunburst segments AND directly-focusable segments (donut slices carry tabindex=0), so the
  // on-brand tooltip is keyboard-reachable beyond the sunburst. Zero-commit heatmap cells stay non-focusable
  // by design (a ~100-cell tab order would be a trap; the whole-chart aria-label covers them), while
  // active-day cells are link-wrapped for the drill-down and ride the same link branch as the sunburst.
  document.addEventListener("focusin", function (e) {
    if (!e.target.closest) return;
    // A focused .js-tip element (e.g. a card link) is its own tip source; anchor to its box.
    var jt = e.target.closest(".js-tip");
    if (jt) {
      var rj = jt.getBoundingClientRect();
      showTip(jt, rj.left + rj.width / 2, rj.top);
      return;
    }
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
  // segment (sunburst slice, active-day heatmap cell) the first tap shows the tooltip and a second tap on the
  // same link follows it; for a bare segment (donut slice, zero-commit heatmap cell) a tap simply shows the
  // tooltip. Either way, a tap elsewhere dismisses it.
  var lastTapped = null;
  document.addEventListener("touchstart", function (e) {
    if (!e.target.closest) return;
    var link = e.target.closest("a");
    // A .js-tip element is its own tip source (may itself be the link → two-tap show-then-navigate).
    var jt = e.target.closest(".js-tip");
    var seg = jt || (link ? link.querySelector(SEG) : e.target.closest(SEG));
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
    // Any element carrying data-copy is a copy trigger: the badge's icon button, the menu's
    // "Copy command" row, and the inline-guidance button all qualify.
    var btn = e.target.closest ? e.target.closest("[data-copy]") : null;
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
        // Remember the resting tooltip too, so the rich tooltip can flip to "Copied" and back. This is the
        // click-to-copy button's visible confirmation now that the icon no longer swaps to a check.
        if (btn.hasAttribute("data-tooltip")) {
          btn.setAttribute("data-tooltip-label", btn.getAttribute("data-tooltip"));
        }
      }
      if (btn._copyResetTimer) { window.clearTimeout(btn._copyResetTimer); }
      btn.classList.add("copied");
      btn.setAttribute("aria-label", "Copied");
      if (btn.hasAttribute("data-tooltip")) { btn.setAttribute("data-tooltip", "Copied"); }
      btn._copyResetTimer = window.setTimeout(function () {
        btn.classList.remove("copied");
        btn.setAttribute("aria-label", btn.getAttribute("data-copy-label"));
        if (btn.hasAttribute("data-tooltip-label")) {
          btn.setAttribute("data-tooltip", btn.getAttribute("data-tooltip-label"));
        }
        btn._copyResetTimer = null;
      }, 1600);
    }).catch(function () { /* best-effort — the visible command is still selectable */ });
  });

  // ---- Send-menu dismissal ----------------------------------------------------------------
  // The send menu is a native <details>, which by itself only closes when you click its own caret.
  // These handlers give it real menu behavior: a click anywhere outside an open menu closes it (so at
  // most one is ever open), picking a destination closes it, and Escape closes it. With JS off the
  // native disclosure still toggles — this only adds the click-away/Escape niceties.
  // Covers both the per-command send menu and the header "Sprint commands" popout (.cmd-menu). The popout can
  // contain command badges that each have their own send-menu, so dismissal is by containment: a click closes
  // every open menu that does NOT contain the click target — this keeps an ancestor popout open while you use a
  // badge inside it, and closes unrelated menus. Escape closes all.
  var MENU_SELECTOR = "details.send-menu[open], details.cmd-menu[open]";

  document.addEventListener("click", function (e) {
    var target = e.target;
    var open = document.querySelectorAll(MENU_SELECTOR);
    for (var i = 0; i < open.length; i++) {
      if (!open[i].contains(target)) open[i].removeAttribute("open");
    }
    // Picking a destination inside a per-command send menu closes that send menu (the popout, if any, stays).
    var item = target.closest ? target.closest(".send-item") : null;
    if (item) {
      var menu = item.closest("details.send-menu");
      if (menu) menu.removeAttribute("open");
    }
  });

  document.addEventListener("keydown", function (e) {
    if (e.key !== "Escape") return;
    var open = document.querySelectorAll(MENU_SELECTOR);
    for (var i = 0; i < open.length; i++) open[i].removeAttribute("open");
  });

  // ---- Sortable / filterable tables (Git Insights hub) [Story 3.8] -------------------------
  // Progressive enhancement ONLY (NFR-5): every table.js-sortable arrives complete and server-sorted, so
  // with JS off the page already reads correctly and this block simply never runs. When it does run it
  // upgrades opt-in tables: column headers become real <button>s that re-order the already-present <tbody>
  // rows (announcing state via aria-sort + a direction glyph, never color alone), and a labeled filter box
  // (created HERE, so no dead control ships in the no-JS page) hides non-matching rows. Nothing is fetched
  // and no new information appears — the server-rendered rows are the single source of truth. Row hiding is
  // display-based (no animation), so the reduced-motion contract is satisfied by construction.
  function enhanceSortableTable(table) {
    var headers = table.querySelectorAll("thead th");
    var tbody = table.tBodies[0];
    if (!tbody || headers.length === 0) return;

    function rows() { return Array.prototype.slice.call(tbody.rows); }

    function cellKey(row, index, numeric) {
      var cell = row.cells[index];
      if (!cell) return numeric ? -Infinity : "";
      var explicit = cell.getAttribute("data-sort-value");
      var text = explicit !== null ? explicit : cell.textContent.trim();
      if (!numeric) return text.toLowerCase();
      var n = parseFloat(text.replace(/[^0-9.+-]/g, ""));
      return isNaN(n) ? -Infinity : n;
    }

    function applySort(th, dir) {
      var index = Array.prototype.indexOf.call(th.parentNode.children, th);
      var numeric = th.getAttribute("data-sort") === "num";
      var sorted = rows().sort(function (a, b) {
        var ka = cellKey(a, index, numeric);
        var kb = cellKey(b, index, numeric);
        if (ka < kb) return dir === "ascending" ? -1 : 1;
        if (ka > kb) return dir === "ascending" ? 1 : -1;
        return 0;
      });
      sorted.forEach(function (row) { tbody.appendChild(row); });
      Array.prototype.forEach.call(headers, function (h) {
        if (h === th) h.setAttribute("aria-sort", dir);
        else h.removeAttribute("aria-sort");
        var glyph = h.querySelector(".gi-sort-glyph");
        if (glyph) glyph.textContent = h === th ? (dir === "ascending" ? "▲" : "▼") : "";
      });
    }

    Array.prototype.forEach.call(headers, function (th) {
      var btn = document.createElement("button");
      btn.type = "button";
      btn.className = "gi-sort-btn";
      while (th.firstChild) btn.appendChild(th.firstChild);
      var glyph = document.createElement("span");
      glyph.className = "gi-sort-glyph";
      glyph.setAttribute("aria-hidden", "true");
      // Reflect the server-rendered initial sort (aria-sort emitted at generation time) in the glyph.
      var initial = th.getAttribute("aria-sort");
      glyph.textContent = initial === "descending" ? "▼" : initial === "ascending" ? "▲" : "";
      btn.appendChild(glyph);
      th.appendChild(btn);
      btn.addEventListener("click", function () {
        var current = th.getAttribute("aria-sort");
        var numeric = th.getAttribute("data-sort") === "num";
        // First activation: numbers read best big-first, text A-first; afterwards, toggle.
        var dir = current ? (current === "descending" ? "ascending" : "descending") : (numeric ? "descending" : "ascending");
        applySort(th, dir);
      });
    });

    // Optional per-table filter, opted in via data-filter-label.
    var filterLabel = table.getAttribute("data-filter-label");
    if (filterLabel) {
      var wrap = document.createElement("div");
      wrap.className = "gi-filter";
      var label = document.createElement("label");
      label.appendChild(document.createTextNode(filterLabel + " "));
      var input = document.createElement("input");
      input.type = "search";
      label.appendChild(input);
      var count = document.createElement("span");
      count.className = "gi-filter-count";
      count.setAttribute("aria-live", "polite");
      wrap.appendChild(label);
      wrap.appendChild(count);
      var host = table.closest(".table-scroll") || table;
      host.parentNode.insertBefore(wrap, host);
      input.addEventListener("input", function () {
        var q = input.value.trim().toLowerCase();
        var all = rows();
        var shown = 0;
        all.forEach(function (row) {
          var match = !q || row.textContent.toLowerCase().indexOf(q) >= 0;
          row.classList.toggle("gi-row-hidden", !match);
          if (match) shown++;
        });
        count.textContent = q ? shown + " of " + all.length + " rows" : "";
      });
    }
  }

  Array.prototype.forEach.call(document.querySelectorAll("table.js-sortable"), function (table) {
    try { enhanceSortableTable(table); } catch (err) { /* degrade silently — the server-sorted table stands */ }
  });

  // ---- Source-code treemap: dimension switch + directory zoom [Story 7.6] ------------------
  // Progressive enhancement ONLY. The server ships a correct, sized-by-LOC treemap with the default
  // (change-frequency) colorize baked in, a legend, and a full text-equivalent table; with JS off this block
  // never runs and all of that stands. When it runs it (1) reveals the hidden colorize controls + drill
  // breadcrumb, (2) re-fills the rects when the dimension changes (reading the same data-* the server wrote,
  // re-bucketing with the SAME thresholds Charts.Bucket uses so the default matches byte-for-byte), and
  // (3) zooms the SVG viewBox into a directory — deep-linkable via the URL hash — respecting reduced motion
  // (the reduce branch snaps instead of tweening).
  (function initCodeMap() {
    var svg = document.getElementById("codemap-svg");
    if (!svg) return;

    var cells = Array.prototype.slice.call(svg.querySelectorAll(".codemap-cell"));
    var baseViewBox = svg.getAttribute("viewBox");
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // Mirror Charts.Bucket exactly (<=0.25/0.5/0.75) so the client re-fill agrees with the server-baked default.
    function bucket(value, max) {
      if (max <= 0 || value <= 0) return 0;
      var r = value / max;
      return r <= 0.25 ? 1 : r <= 0.5 ? 2 : r <= 0.75 ? 3 : 4;
    }

    function num(cell, name) { var v = cell.getAttribute(name); return v === null ? null : parseFloat(v); }

    function metricFor(cell, dim) {
      if (dim === "changes") return num(cell, "data-changes");
      if (dim === "last") return num(cell, "data-last");
      if (dim === "created") return num(cell, "data-first");
      if (dim === "avgchange") {
        var churn = num(cell, "data-churn"), ch = num(cell, "data-changes");
        return (churn === null || !ch) ? null : churn / ch;
      }
      return null;
    }

    function recolor(dim) {
      // Dates are huge absolute day numbers, so they must be scaled against the file set's own [min,max]
      // window; counts/averages scale against max (min 0), matching the server's default (change-frequency) fill.
      var isDate = dim === "last" || dim === "created";
      var min = Infinity, max = 0;
      cells.forEach(function (c) {
        var v = metricFor(c, dim);
        if (v === null) return;
        if (v > max) max = v;
        if (v < min) min = v;
      });
      var range = isDate ? (max - min) : max;
      cells.forEach(function (c) {
        for (var l = 0; l <= 4; l++) c.classList.remove("level-" + l);
        c.classList.remove("level-none");
        var v = metricFor(c, dim);
        if (v === null) { c.classList.add("level-none"); return; }
        c.classList.add("level-" + bucket(isDate ? (v - min) : v, range));
      });
    }

    // Reveal the colorize controls (hidden in the server HTML so no inert control ships in the no-JS page).
    var controls = document.getElementById("codemap-controls");
    if (controls) {
      controls.hidden = false;
      controls.addEventListener("change", function (e) {
        if (e.target && e.target.name === "codemap-dim") recolor(e.target.value);
      });
    }

    var drill = document.querySelector(".codemap-drill");
    var crumbs = document.getElementById("codemap-breadcrumb");
    var dirs = Array.prototype.slice.call(svg.querySelectorAll(".codemap-dir"));

    function cssEscape(s) {
      return (window.CSS && CSS.escape) ? CSS.escape(s) : s.replace(/["\\]/g, "\\$&");
    }

    function viewBoxFor(path) {
      if (!path) return baseViewBox;
      var rect = svg.querySelector('.codemap-dir[data-path="' + cssEscape(path) + '"]');
      if (!rect) return baseViewBox;
      return rect.getAttribute("x") + " " + rect.getAttribute("y") + " " +
        rect.getAttribute("width") + " " + rect.getAttribute("height");
    }

    function labelFor(path) {
      if (!path) return "All files";
      var i = path.lastIndexOf("/");
      return i >= 0 ? path.slice(i + 1) : path;
    }

    // Tween the viewBox with requestAnimationFrame when motion is allowed; snap instantly under reduced motion.
    function setViewBox(target, animate) {
      if (!animate || !window.requestAnimationFrame) { svg.setAttribute("viewBox", target); return; }
      var from = svg.getAttribute("viewBox").split(/\s+/).map(Number);
      var to = target.split(/\s+/).map(Number);
      if (from.length !== 4 || to.length !== 4) { svg.setAttribute("viewBox", target); return; }
      var start = null, dur = 240;
      function step(ts) {
        if (start === null) start = ts;
        var t = Math.min(1, (ts - start) / dur);
        var e = t * (2 - t); // easeOutQuad
        svg.setAttribute("viewBox", from.map(function (v, i) { return v + (to[i] - v) * e; }).join(" "));
        if (t < 1) window.requestAnimationFrame(step);
      }
      window.requestAnimationFrame(step);
    }

    function renderCrumbs(path) {
      if (!crumbs) return;
      crumbs.innerHTML = "";
      var trail = [{ p: "", l: "All files" }];
      if (path) {
        var acc = "";
        path.split("/").forEach(function (s) { acc = acc ? acc + "/" + s : s; trail.push({ p: acc, l: s }); });
      }
      trail.forEach(function (t, idx) {
        var li = document.createElement("li");
        var btn = document.createElement("button");
        btn.type = "button";
        btn.className = "codemap-crumb";
        btn.textContent = t.l;
        btn.setAttribute("data-path", t.p);
        if (idx === trail.length - 1) btn.setAttribute("aria-current", "true");
        btn.addEventListener("click", function () { zoomTo(t.p, true); });
        li.appendChild(btn);
        crumbs.appendChild(li);
      });
    }

    function zoomTo(path, pushHash) {
      setViewBox(viewBoxFor(path), !reduceMotion);
      renderCrumbs(path);
      if (pushHash && window.history && history.pushState) {
        if (path) history.pushState({ dir: path }, "", "#dir=" + encodeURIComponent(path));
        else history.pushState({ dir: "" }, "", location.pathname + location.search);
      }
    }

    if (drill) drill.hidden = false;

    // A directory rect becomes an activatable zoom target (click + keyboard). Made focusable/labelled at runtime
    // so the no-JS page never ships inert tab stops; aria-hidden is dropped since it's now interactive.
    dirs.forEach(function (rect) {
      var path = rect.getAttribute("data-path");
      rect.removeAttribute("aria-hidden");
      rect.setAttribute("tabindex", "0");
      rect.setAttribute("role", "button");
      rect.setAttribute("aria-label", "Zoom into " + labelFor(path));
      rect.addEventListener("click", function () { zoomTo(path, true); });
      rect.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); zoomTo(path, true); }
      });
    });

    function applyHash() {
      var m = /#dir=([^&]+)/.exec(location.hash);
      var path = m ? decodeURIComponent(m[1]) : "";
      svg.setAttribute("viewBox", viewBoxFor(path)); // snap on load/back-forward (no entrance animation)
      renderCrumbs(path);
    }
    window.addEventListener("popstate", applyHash);
    applyHash();
  })();
})();
