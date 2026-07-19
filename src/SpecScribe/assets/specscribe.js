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
  var activeNativeTitle = null;
  var activeText = null;
  var activeHtml = null; // when set, the tip renders this as innerHTML (rich card) instead of plain text

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
    // Elements opt into a fully stylized HTML card via data-tip-html (e.g. the code-map cells). The markup is
    // server-built and escaped, so setting it as innerHTML is safe — it renders a rich card a plain-text tip can't.
    var dataHtml = el.getAttribute ? el.getAttribute("data-tip-html") : null;
    if (dataHtml) {
      activeSeg = el;
      activeTitle = null;
      activeNativeTitle = el.getAttribute("title");
      if (activeNativeTitle) el.removeAttribute("title");
      activeText = null;
      activeHtml = dataHtml;
      return;
    }
    // HTML elements opt into the (body-level, never-clipped) tooltip via data-tip — used for rich, multi-line
    // card/wheel tips that a clipped CSS ::after can't show. SVG segments keep the <title> path.
    var dataTip = el.getAttribute ? el.getAttribute("data-tip") : null;
    if (dataTip) {
      activeSeg = el;
      activeTitle = null;
      activeNativeTitle = el.getAttribute("title");
      if (activeNativeTitle) el.removeAttribute("title");
      activeText = dataTip;
      activeHtml = null;
      return;
    }
    var t = el.querySelector("title");
    activeSeg = el;
    activeTitle = t;
    activeText = t ? t.textContent : el.getAttribute("aria-label");
    activeHtml = null;
    if (t) t.remove();
  }

  function deactivate() {
    if (activeTitle && activeSeg) activeSeg.insertBefore(activeTitle, activeSeg.firstChild);
    if (activeNativeTitle && activeSeg) activeSeg.setAttribute("title", activeNativeTitle);
    activeSeg = null;
    activeTitle = null;
    activeNativeTitle = null;
    activeText = null;
    activeHtml = null;
  }

  function showTip(el, x, y) {
    activate(el);
    if (!activeText && !activeHtml) { deactivate(); return; }
    var t = ensureTip();
    if (activeHtml) { t.innerHTML = activeHtml; } else { t.textContent = activeText; }
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
    if (!seg) return;
    // Stay showing while the pointer moves between children of the same tip host (e.g. badge icon ↔ text).
    var into = e.relatedTarget;
    if (into && (into === seg || (seg.contains && seg.contains(into)))) return;
    hideTip();
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
    // Primary dashboard drill cards keep one-tap navigation; hover/focus still show the tip.
    if (link && link.classList.contains("stat-card-link")) {
      lastTapped = null;
      return;
    }
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

  // ---- Key-view group toggles (white band Docs / Architecture / Work) -----------------------
  // Desktop keeps hover/focus-within; click sets aria-expanded + .is-open for touch and AT. Narrow
  // viewports force panels open via CSS (mirroring the dark-bar mobile treatment).
  document.addEventListener("click", function (e) {
    var trigger = e.target.closest ? e.target.closest(".key-view-trigger") : null;
    if (trigger) {
      e.preventDefault();
      var group = trigger.closest(".key-view-group");
      var open = trigger.getAttribute("aria-expanded") === "true";
      document.querySelectorAll(".key-view-group.is-open").forEach(function (g) {
        if (g === group) return;
        g.classList.remove("is-open");
        var t = g.querySelector(".key-view-trigger");
        if (t) t.setAttribute("aria-expanded", "false");
      });
      if (group) group.classList.toggle("is-open", !open);
      trigger.setAttribute("aria-expanded", open ? "false" : "true");
      return;
    }
    if (!e.target.closest || !e.target.closest(".key-view-group")) {
      document.querySelectorAll(".key-view-group.is-open").forEach(function (g) {
        g.classList.remove("is-open");
        var t = g.querySelector(".key-view-trigger");
        if (t) t.setAttribute("aria-expanded", "false");
      });
    }
  });
  document.addEventListener("keydown", function (e) {
    if (e.key !== "Escape") return;
    document.querySelectorAll(".key-view-group.is-open").forEach(function (g) {
      g.classList.remove("is-open");
      var t = g.querySelector(".key-view-trigger");
      if (t) { t.setAttribute("aria-expanded", "false"); t.focus(); }
    });
  });

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
  var MENU_SELECTOR = "details.send-menu[open], details.cmd-menu[open], details.status-legend[open], details.sprint-epic-filter[open]";

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

  // ---- Focus management for :target-revealed panels [Deferred, Story 3.8] -----------------
  // Pure-CSS :target reveals the Git Insights file-contributors panel, but never moves focus there — a
  // keyboard/AT user's focus stays on the link they just activated even though the visible panel changed.
  // Progressive enhancement only: with JS off, :target still reveals the panel, just without this focus jump.
  function focusHashTarget() {
    var id = location.hash.slice(1);
    if (!id) return;
    var el = document.getElementById(id);
    if (el && el.classList.contains("gi-contributors-panel")) el.focus();
  }
  window.addEventListener("hashchange", focusHashTarget);
  if (location.hash) focusHashTarget();

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

  // ---- Sprint epic filter (home widget + sprint page) --------------------------------------
  // Progressive enhancement ONLY (mirrors js-sortable): SSR already applies the default active-epic
  // visibility + home cap. This injects a compact epic multi-select dropdown from data-epics /
  // data-default-epics so no-JS never sees inert controls. Progress wheel / totals stay untouched.
  function enhanceSprintEpicFilter(root) {
    if (root.querySelector(".sprint-epic-filter")) return;
    var raw = root.getAttribute("data-epics") || "[]";
    var catalog;
    try { catalog = JSON.parse(raw); } catch (err) { return; }
    if (!Array.isArray(catalog) || catalog.length === 0) return;

    var defaultSet = {};
    String(root.getAttribute("data-default-epics") || "").split(",").forEach(function (part) {
      var id = part.trim();
      if (id) defaultSet[id] = true;
    });

    var emptyHint = root.querySelector(".sprint-filter-empty");
    var filter = document.createElement("details");
    filter.className = "sprint-epic-filter";

    var summary = document.createElement("summary");
    summary.className = "sprint-epic-filter-summary";
    summary.setAttribute("aria-label", "Filter stories by epic");
    var summaryLabel = document.createElement("span");
    summaryLabel.className = "sprint-epic-filter-label";
    summaryLabel.textContent = "Epics";
    var summaryCount = document.createElement("span");
    summaryCount.className = "sprint-epic-filter-count";
    summary.appendChild(summaryLabel);
    summary.appendChild(summaryCount);
    filter.appendChild(summary);

    var panel = document.createElement("div");
    panel.className = "sprint-epic-filter-panel";
    panel.setAttribute("role", "group");
    panel.setAttribute("aria-label", "Epics");

    var allBtn = document.createElement("button");
    allBtn.type = "button";
    allBtn.className = "sprint-epic-filter-all";
    allBtn.textContent = "All";
    panel.appendChild(allBtn);

    catalog.forEach(function (entry) {
      var id = String(entry.id);
      var opt = document.createElement("label");
      opt.className = "sprint-epic-filter-opt";
      var input = document.createElement("input");
      input.type = "checkbox";
      input.value = id;
      if (defaultSet[id]) input.checked = true;
      opt.appendChild(input);
      opt.appendChild(document.createTextNode(" " + (entry.label || ("Epic " + id))));
      panel.appendChild(opt);
    });
    filter.appendChild(panel);

    var host = root.querySelector(".sprint-epic-filter-host");
    if (host) host.appendChild(filter);
    else if (emptyHint && emptyHint.parentNode === root) root.insertBefore(filter, emptyHint);
    else root.insertBefore(filter, root.firstChild);

    var boxes = filter.querySelectorAll("input[type=checkbox]");
    var cap = parseInt(root.getAttribute("data-cap") || "", 10);
    if (isNaN(cap) || cap < 1) cap = 0;

    function selectedSet() {
      var set = {};
      var any = false;
      var count = 0;
      Array.prototype.forEach.call(boxes, function (b) {
        if (b.checked) { set[b.value] = true; any = true; count++; }
      });
      return { set: set, any: any, count: count };
    }

    function updateSummary(sel) {
      if (sel.count === 0) summaryCount.textContent = "none selected";
      else if (sel.count === boxes.length) summaryCount.textContent = "all (" + sel.count + ")";
      else summaryCount.textContent = sel.count + " selected";
    }

    function apply() {
      var sel = selectedSet();
      updateSummary(sel);
      if (emptyHint) emptyHint.hidden = sel.any;

      Array.prototype.forEach.call(root.querySelectorAll(".sprint-card[data-epic]"), function (card) {
        var epic = card.getAttribute("data-epic");
        card.hidden = !sel.any || !sel.set[epic];
        card.removeAttribute("data-cap-overflow");
      });

      Array.prototype.forEach.call(root.querySelectorAll(".sprint-epic-lane[data-epic]"), function (lane) {
        var epic = lane.getAttribute("data-epic");
        lane.hidden = !sel.any || !sel.set[epic];
      });

      Array.prototype.forEach.call(root.querySelectorAll(".sprint-lane"), function (lane) {
        var cardsHost = lane.querySelector(".sprint-cards");
        if (!cardsHost) return;
        var cards = Array.prototype.slice.call(cardsHost.querySelectorAll(".sprint-card[data-epic]"));
        var matching = cards.filter(function (c) {
          var epic = c.getAttribute("data-epic");
          return sel.any && sel.set[epic];
        });
        var empty = cardsHost.querySelector(".sprint-lane-empty");
        if (!empty && matching.length === 0 && cards.length > 0) {
          empty = document.createElement("div");
          empty.className = "sprint-lane-empty";
          empty.setAttribute("data-filter-empty", "1");
          empty.textContent = "No stories from the selected epics in this column.";
          cardsHost.insertBefore(empty, cardsHost.firstChild);
        }
        if (empty) {
          empty.hidden = matching.length > 0 || !sel.any;
        }

        // Cap always applies to the *visible filtered* matching set (home widget).
        if (cap > 0) {
          matching.forEach(function (c, i) {
            if (i >= cap) {
              c.hidden = true;
              c.setAttribute("data-cap-overflow", "1");
            } else {
              c.hidden = false;
            }
          });
        } else {
          matching.forEach(function (c) { c.hidden = false; });
        }

        var countEl = lane.querySelector(".sprint-lane-count");
        var laneLabel = lane.getAttribute("data-lane-label") || "";
        if (countEl) countEl.textContent = String(matching.length);
        if (laneLabel) {
          var plural = matching.length === 1 ? "story" : "stories";
          lane.setAttribute("aria-label", laneLabel + ": " + matching.length + " " + plural);
        }

        var more = cardsHost.querySelector(".sprint-lane-more");
        if (more && cap > 0) {
          if (matching.length > cap) {
            more.hidden = false;
            more.textContent = "+" + (matching.length - cap) + " more →";
          } else {
            more.hidden = true;
          }
        }
      });
    }

    Array.prototype.forEach.call(boxes, function (b) {
      b.addEventListener("change", apply);
    });
    allBtn.addEventListener("click", function () {
      Array.prototype.forEach.call(boxes, function (b) { b.checked = true; });
      apply();
    });
    apply();
  }

  Array.prototype.forEach.call(document.querySelectorAll(".sprint-filterable"), function (root) {
    try { enhanceSprintEpicFilter(root); } catch (err) { /* degrade — server default remains */ }
  });

  // ---- List-row sort / group / filter (action items, deferred work, follow-up groups, ADR
  // landing) [Story 10.9] -----------------------------------------------------------------------
  // Progressive enhancement ONLY (NFR5/NFR8): every ul.js-listable arrives complete and in a sensible
  // server-defined order, so with JS off this block never runs and the page already reads correctly.
  // Generalizes the enhanceSortableTable/enhanceSprintEpicFilter pattern above to <li>-shaped list rows
  // instead of <table> rows or card grids: reads the data-sort-* attributes ListRow/FollowUpRow already
  // emit, offers only the sort keys the page's rows actually populate, and reorders the existing <li>
  // elements in place (no re-render, no data refetch). Sorting/grouping never runs until the reader
  // acts — the server order stands as the true default (AC #2).
  var STATUS_GROUP_RANK = ["pending", "drafted", "ready", "active", "review", "done", "deferred", "retired", "unrecognized"];

  function enhanceListRows(container) {
    var items = Array.prototype.filter.call(container.children, function (el) {
      return el.tagName === "LI" && !el.classList.contains("list-row-group-heading");
    });
    if (items.length === 0) return;

    var hasName = items.some(function (li) { return li.hasAttribute("data-sort-name"); });
    var hasDate = items.some(function (li) { return li.hasAttribute("data-sort-date"); });
    var hasStatus = items.some(function (li) { return li.hasAttribute("data-sort-status"); });
    if (!hasName && !hasDate && !hasStatus) return;

    var bar = document.createElement("div");
    bar.className = "list-controls";

    var sortSelect = null;
    if (hasName || hasDate || hasStatus) {
      var sortWrap = document.createElement("label");
      sortWrap.className = "list-controls-sort";
      sortWrap.appendChild(document.createTextNode("Sort by "));
      sortSelect = document.createElement("select");
      if (hasName) addSortOption(sortSelect, "name", "Name");
      if (hasDate) addSortOption(sortSelect, "date", "Date");
      if (hasStatus) addSortOption(sortSelect, "status", "Status");
      sortWrap.appendChild(sortSelect);
      bar.appendChild(sortWrap);
    }

    var groupBtn = null;
    if (hasStatus) {
      groupBtn = document.createElement("button");
      groupBtn.type = "button";
      groupBtn.className = "list-controls-group";
      groupBtn.setAttribute("aria-pressed", "false");
      groupBtn.textContent = "Group by status";
      bar.appendChild(groupBtn);
    }

    var filterWrap = document.createElement("div");
    filterWrap.className = "gi-filter list-controls-filter";
    var filterLabel = document.createElement("label");
    filterLabel.appendChild(document.createTextNode("Filter "));
    var filterInput = document.createElement("input");
    filterInput.type = "search";
    filterLabel.appendChild(filterInput);
    var filterCount = document.createElement("span");
    filterCount.className = "gi-filter-count";
    filterCount.setAttribute("aria-live", "polite");
    filterWrap.appendChild(filterLabel);
    filterWrap.appendChild(filterCount);
    bar.appendChild(filterWrap);

    container.parentNode.insertBefore(bar, container);

    function addSortOption(select, value, label) {
      var opt = document.createElement("option");
      opt.value = value;
      opt.textContent = label;
      select.appendChild(opt);
    }

    function statusRank(token) {
      var idx = STATUS_GROUP_RANK.indexOf(token || "");
      return idx === -1 ? STATUS_GROUP_RANK.length : idx;
    }

    function sortKey(li, mode) {
      if (mode === "date") return li.getAttribute("data-sort-date") || "";
      if (mode === "status") return statusRank(li.getAttribute("data-sort-status"));
      return (li.getAttribute("data-sort-name") || li.textContent).trim().toLowerCase();
    }

    function applyView() {
      var mode = sortSelect ? sortSelect.value : null;
      var q = filterInput.value.trim().toLowerCase();

      var shown = 0;
      items.forEach(function (li) {
        var match = !q || li.textContent.toLowerCase().indexOf(q) >= 0;
        li.classList.toggle("list-row-hidden", !match);
        if (match) shown++;
      });
      filterCount.textContent = q ? shown + " of " + items.length + " rows" : "";

      var ordered = items.slice();
      if (mode) {
        ordered.sort(function (a, b) {
          var ka = sortKey(a, mode);
          var kb = sortKey(b, mode);
          if (ka < kb) return -1;
          if (ka > kb) return 1;
          return 0;
        });
      }

      Array.prototype.forEach.call(container.querySelectorAll(".list-row-group-heading"), function (h) {
        h.parentNode.removeChild(h);
      });

      var grouping = groupBtn && groupBtn.getAttribute("aria-pressed") === "true";
      if (grouping) {
        var lastToken = null;
        ordered.sort(function (a, b) { return statusRank(a.getAttribute("data-sort-status")) - statusRank(b.getAttribute("data-sort-status")); });
        ordered.forEach(function (li) {
          var token = li.getAttribute("data-sort-status") || "";
          if (token !== lastToken) {
            var heading = document.createElement("li");
            heading.className = "list-row-group-heading";
            var h3 = document.createElement("h3");
            h3.setAttribute("role", "group");
            var badge = li.querySelector(".status-badge");
            h3.textContent = badge ? badge.textContent : (token || "Other");
            heading.appendChild(h3);
            container.appendChild(heading);
            lastToken = token;
          }
          container.appendChild(li);
        });
      } else {
        ordered.forEach(function (li) { container.appendChild(li); });
      }
    }

    if (sortSelect) sortSelect.addEventListener("change", applyView);
    if (groupBtn) {
      groupBtn.addEventListener("click", function () {
        var pressed = groupBtn.getAttribute("aria-pressed") === "true";
        groupBtn.setAttribute("aria-pressed", String(!pressed));
        applyView();
      });
    }
    filterInput.addEventListener("input", applyView);
  }

  Array.prototype.forEach.call(document.querySelectorAll(".js-listable"), function (list) {
    try { enhanceListRows(list); } catch (err) { /* degrade silently — the server-ordered list stands */ }
  });

  // ---- Source-code treemap: dimension switch + directory zoom [Story 7.6, round 2] ---------
  // Progressive enhancement ONLY. The server ships up to four self-contained ".codemap-view" panels (one per
  // exclude-spec-dev / exclude-tests filter combination — Story 7.6 round 2), each with a correct, sized-by-LOC
  // treemap, the default (change-frequency) colorize baked in, a legend, and a full text-equivalent table; with JS
  // off this block never runs and all of that stands. The panel TOGGLE itself (the two checkboxes) is pure CSS and
  // needs no JS at all. This block only wires, PER PANEL, (1) reveals the hidden colorize dropdown + drill
  // breadcrumb, (2) re-fills the rects when the dimension changes (reading the same data-* the server wrote,
  // re-bucketing with the SAME thresholds Charts.Bucket uses so the default matches byte-for-byte), and (3) zooms
  // the SVG viewBox into a directory — deep-linkable via the URL hash — respecting reduced motion (the reduce
  // branch snaps instead of tweening). Nothing here uses a global id (four panels share one shape), so every
  // lookup is scoped with querySelector against the panel it belongs to.
  Array.prototype.forEach.call(document.querySelectorAll(".codemap-view"), function (panel) {
    initCodeMapPanel(panel);
  });

  function initCodeMapPanel(panel) {
    var svg = panel.querySelector(".codemap");
    if (!svg) return;

    var cells = Array.prototype.slice.call(svg.querySelectorAll(".codemap-cell"));
    var baseViewBox = svg.getAttribute("viewBox");
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // Mirror Charts.Bucket exactly (<=0.25/0.5/0.75) so the client re-fill agrees with the server-baked default.
    // A degenerate single-point range (min === max, i.e. exactly one cell carries data for this dimension) always
    // reads as the top bucket rather than falling through the max<=0 guard to "no activity" — the one file that
    // DOES have data must not render identically to files with none.
    function bucket(value, max) {
      if (max <= 0) return value > 0 ? 4 : 0;
      if (value <= 0) return 0;
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
      if (dim === "cochange") return num(cell, "data-cochanged");
      if (dim === "churn") return num(cell, "data-churn");
      return null;
    }

    // Human-readable name for each dimension — used to keep the aria-label/tooltip/legend text equivalents in
    // sync with whatever the color currently encodes (AC #4: color is never the sole signal).
    var DIM_LABELS = {
      changes: "change frequency",
      last: "recency of last change",
      created: "recency of first change",
      avgchange: "average change size",
      cochange: "files changed together",
      churn: "churn"
    };

    // Capture each cell's server-baked base label/tooltip once, before any recolor, so repeated dimension
    // switches append to the ORIGINAL text rather than stacking onto a previously-appended suffix.
    // The tooltip is a static, server-built HTML card (data-tip-html) listing every metric, so it already satisfies
    // "color is never the sole signal" for any active dimension — no per-dimension tooltip rewrite is needed. Only
    // the aria-label (and the legend) track the active dimension, so we snapshot just the base label.
    cells.forEach(function (c) {
      if (!c.hasAttribute("data-base-label")) c.setAttribute("data-base-label", c.getAttribute("aria-label") || "");
    });

    var legendDim = panel.querySelector(".codemap-legend-dim");

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
      var dimLabel = DIM_LABELS[dim] || dim;
      cells.forEach(function (c) {
        for (var l = 0; l <= 4; l++) c.classList.remove("level-" + l);
        c.classList.remove("level-none");
        var v = metricFor(c, dim);
        var baseLabel = c.getAttribute("data-base-label") || "";
        if (v === null) {
          c.classList.add("level-none");
          c.setAttribute("aria-label", baseLabel + " — no data for " + dimLabel);
          return;
        }
        var lvl = bucket(isDate ? (v - min) : v, range);
        c.classList.add("level-" + lvl);
        // The bucket level (0-4) IS exactly what the color encodes, so it's the honest text equivalent —
        // never a raw day-number or other value the color itself doesn't literally represent.
        var levelText = lvl === 0 ? "lowest" : lvl === 4 ? "highest" : "level " + lvl + " of 4";
        c.setAttribute("aria-label", baseLabel + " — " + dimLabel + ": " + levelText);
      });
      if (legendDim) legendDim.textContent = "Colorized by " + dimLabel;
    }

    // Reveal the colorize dropdown (hidden in the server HTML so no inert control ships in the no-JS page).
    var controls = panel.querySelector(".codemap-controls");
    var select = panel.querySelector(".codemap-dim-select");
    if (controls && select) {
      controls.hidden = false;
      select.addEventListener("change", function () { recolor(select.value); });
    }

    var drill = panel.querySelector(".codemap-drill");
    var crumbs = panel.querySelector(".codemap-breadcrumb");
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

    // Zoom-tween duration is read from the shared --motion-* token system (Story 3.5), not a bare hardcoded
    // number, so the treemap's motion feel stays in sync with every other animated surface. --motion-fast is the
    // closest semantic fit (a direct-manipulation UI transition, not a one-time chart-entrance reveal); a
    // 240ms fallback covers browsers/tests where the token can't be read (e.g. no document.documentElement).
    function motionFastMs() {
      try {
        var raw = getComputedStyle(document.documentElement).getPropertyValue("--motion-fast").trim();
        var ms = raw.endsWith("ms") ? parseFloat(raw) : parseFloat(raw) * 1000;
        return ms > 0 ? ms : 240;
      } catch (e) {
        return 240;
      }
    }

    // Tween the viewBox with requestAnimationFrame when motion is allowed; snap instantly under reduced motion.
    function setViewBox(target, animate) {
      if (!animate || !window.requestAnimationFrame) { svg.setAttribute("viewBox", target); return; }
      var from = svg.getAttribute("viewBox").split(/\s+/).map(Number);
      var to = target.split(/\s+/).map(Number);
      if (from.length !== 4 || to.length !== 4) { svg.setAttribute("viewBox", target); return; }
      var start = null, dur = motionFastMs();
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
  }
})();
