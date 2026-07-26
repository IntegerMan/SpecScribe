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

  // `.ss-hierarchy-sector` is the Story 20.5 component's Plotly sectors opting into this SAME tooltip rather than
  // Plotly's own hover card — one tooltip system site-wide, so the chart engine changing does not change how a
  // tooltip looks. They carry `data-tip-html` (the rich-card path the code map already uses). [Story 20.5]
  var SEG = ".sb-seg, .heatmap-cell, .donut-seg, .ss-hierarchy-sector";
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

  // ---- Code-page tabs: release the #L{n} deep-link lock on the first tab click ---------------
  // The code page's tabs are pure-CSS radios, but a #L{n} deep link forces the Code panel forward through
  // `.code-tabs:has(.code-tabpanel--source .code-line:target)` — rules deliberately authored to WIN the
  // specificity tie against the :checked rules so the anchor survives the default tab. :target is sticky
  // though: it keeps matching for as long as the hash sits in the URL. So after landing on
  // code/<path>.html#L350, every later tab click flipped its radio and changed nothing on screen — the tab
  // strip read as frozen. CSS can't tell a default :checked from a user-clicked one, so the tie itself can't
  // be re-tuned; the override is gated on .code-tabs--released instead and we set that here, on the first
  // activation. From then on the radios govern and the deep link has had its say.
  //
  // The hash is left alone on purpose: refresh and copy/paste keep working, the cited line stays highlighted
  // when you tab back to Code, and dropping it wouldn't help anyway — browsers only recompute :target on real
  // fragment navigation, so a replaceState that strips the hash leaves :target matching regardless.
  // With JS off nothing changes: the deep link still lands and still pins the Code panel.
  Array.prototype.forEach.call(document.querySelectorAll(".code-tabs"), function (tabs) {
    tabs.addEventListener("change", function (e) {
      if (e.target.classList && e.target.classList.contains("code-tab-input")) {
        tabs.classList.add("code-tabs--released");
      }
    });
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

    // ---- Pagination -----------------------------------------------------------------------
    // Client-side paging over the CURRENT row set (post sort/filter), so a long table (e.g. the
    // Git Insights file list) doesn't dump every row on one huge page. Filter hiding uses its own
    // "gi-filtered-out" marker class (rather than gi-row-hidden directly) so the two reasons a row
    // can be hidden — filtered out vs. off the current page — compose instead of fighting each other.
    var PAGE_SIZE = 20;
    var currentPage = 1;
    var pager = null, pagerStatus = null, pagerPrev = null, pagerNext = null;

    function matchingRows() {
      return rows().filter(function (row) { return !row.classList.contains("gi-filtered-out"); });
    }

    function createPager() {
      pager = document.createElement("div");
      pager.className = "gi-pager";
      pagerPrev = document.createElement("button");
      pagerPrev.type = "button";
      pagerPrev.className = "gi-pager-prev";
      pagerPrev.textContent = "Prev";
      pagerPrev.addEventListener("click", function () { currentPage--; paginate(); });
      pagerStatus = document.createElement("span");
      pagerStatus.className = "gi-pager-status";
      // No aria-live here: the filter input's own aria-live count already announces on every keystroke,
      // and re-typing a query would otherwise re-announce "Page 1 of N" on top of it — noise, not help.
      pagerNext = document.createElement("button");
      pagerNext.type = "button";
      pagerNext.className = "gi-pager-next";
      pagerNext.textContent = "Next";
      pagerNext.addEventListener("click", function () { currentPage++; paginate(); });
      pager.appendChild(pagerPrev);
      pager.appendChild(pagerStatus);
      pager.appendChild(pagerNext);
      var host = table.closest(".table-scroll") || table;
      if (host.parentNode) host.parentNode.insertBefore(pager, host.nextSibling);
    }

    function paginate() {
      var matching = matchingRows();
      if (matching.length <= PAGE_SIZE) {
        rows().forEach(function (row) { row.classList.toggle("gi-row-hidden", row.classList.contains("gi-filtered-out")); });
        if (pager) pager.hidden = true;
        return;
      }

      var totalPages = Math.max(1, Math.ceil(matching.length / PAGE_SIZE));
      if (currentPage > totalPages) currentPage = totalPages;
      if (currentPage < 1) currentPage = 1;

      var start = (currentPage - 1) * PAGE_SIZE;
      var end = start + PAGE_SIZE;
      matching.forEach(function (row, i) { row.classList.toggle("gi-row-hidden", i < start || i >= end); });
      rows().forEach(function (row) {
        if (row.classList.contains("gi-filtered-out")) row.classList.add("gi-row-hidden");
      });

      if (!pager) createPager();
      pager.hidden = false;
      pagerStatus.textContent = "Page " + currentPage + " of " + totalPages;
      pagerPrev.disabled = currentPage <= 1;
      pagerNext.disabled = currentPage >= totalPages;
    }

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
      // A re-sort changes what "page 1" means, so land back on it rather than stranding the
      // reader on a page whose rows just scattered elsewhere in the new order.
      currentPage = 1;
      paginate();
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
          row.classList.toggle("gi-filtered-out", !match);
          if (match) shown++;
        });
        count.textContent = q ? shown + " of " + all.length + " rows" : "";
        // A new filter query changes which rows are in play, so page count restarts at 1.
        currentPage = 1;
        paginate();
      });
    }

    paginate();
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
  // acts — the server order stands as the true default (AC #2). Severity ordering for status sort/grouping
  // comes from the server-emitted data-sort-status-rank (StatusStyles.CanonicalRank) — no status vocabulary
  // or ordering is hardcoded here (Story 10.9 guardrail; StatusStyles is the single source).
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

    function rowStatusRank(li) {
      // Canonical severity rank is emitted server-side (StatusStyles.CanonicalRank) as data-sort-status-rank,
      // so grouping/status-sort never hardcodes a second status order here. Missing/unranked rows sort last.
      var r = li.getAttribute("data-sort-status-rank");
      if (r === null) return Number.MAX_SAFE_INTEGER;
      var n = parseInt(r, 10);
      return isNaN(n) ? Number.MAX_SAFE_INTEGER : n;
    }

    function sortKey(li, mode) {
      if (mode === "date") return li.getAttribute("data-sort-date") || "";
      if (mode === "status") return rowStatusRank(li);
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
        var lastHeadingToken = null;
        ordered.sort(function (a, b) { return rowStatusRank(a) - rowStatusRank(b); });
        ordered.forEach(function (li) {
          var token = li.getAttribute("data-sort-status") || "";
          // Emit a heading only on the first VISIBLE row of a status group — a heading whose rows are all
          // hidden by the active filter would announce an empty group (a11y/UX regression when filter+group
          // compose). Hidden rows still get appended (invisible) so clearing the filter restores them in place.
          if (!li.classList.contains("list-row-hidden") && token !== lastHeadingToken) {
            var heading = document.createElement("li");
            heading.className = "list-row-group-heading";
            var h3 = document.createElement("h3");
            var badge = li.querySelector(".status-badge");
            h3.textContent = badge ? badge.textContent : (token || "Other");
            heading.appendChild(h3);
            container.appendChild(heading);
            lastHeadingToken = token;
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

  // ---- Risk-quadrant elevated-files grid: client-side pagination [Story 7.10] ---------------
  // Progressive enhancement ONLY. The server ships every elevated-risk file as a plain <li> inside the
  // ".risk-grid", already in rank order — the complete, correct, no-JS truth. This only chunks that already-
  // complete list into pages once there's more than one page's worth, revealing a Prev/Next pager (emitted
  // `hidden` by the server, positioned AFTER the grid — review-pass owner feedback: controls belong at the
  // bottom of the list they page) rather than leaving a static "N of M" control with nothing to do.
  function initRiskGridPager(grid) {
    var pager = grid.nextElementSibling;
    if (!pager || !pager.classList.contains("risk-pager")) return;
    var items = Array.prototype.slice.call(grid.querySelectorAll(".risk-grid-item"));
    var pageSize = parseInt(grid.getAttribute("data-page-size"), 10) || 12;
    if (items.length <= pageSize) return; // everything already fits on one screen — leave the pager hidden

    var prevBtn = pager.querySelector(".risk-pager-prev");
    var nextBtn = pager.querySelector(".risk-pager-next");
    var status = pager.querySelector(".risk-pager-status");
    var totalPages = Math.ceil(items.length / pageSize);
    var page = 0;

    function render() {
      items.forEach(function (item, i) {
        item.hidden = Math.floor(i / pageSize) !== page;
      });
      status.textContent = "Page " + (page + 1) + " of " + totalPages;
      prevBtn.disabled = page === 0;
      nextBtn.disabled = page === totalPages - 1;
    }

    prevBtn.addEventListener("click", function () {
      if (page === 0) return;
      page--;
      render();
      grid.scrollIntoView({ block: "nearest" });
    });
    nextBtn.addEventListener("click", function () {
      if (page === totalPages - 1) return;
      page++;
      render();
      grid.scrollIntoView({ block: "nearest" });
    });

    pager.hidden = false;
    render();
  }

  Array.prototype.forEach.call(document.querySelectorAll(".risk-grid"), function (grid) {
    try { initRiskGridPager(grid); } catch (err) { /* degrade silently — the full server-ordered grid stands */ }
  });

  // ---- Code Map file table: client-side pagination [Story 7.12 review] -----------------------
  // Progressive enhancement ONLY, mirroring initRiskGridPager above (same shape, its own class family so the
  // two pagers can never cross-wire). The server ships every file as a plain <tr> inside ".codemap-table",
  // already in significance order — the complete, correct, no-JS truth. Up to four independent tables can exist
  // on one page (one per exclude-filter combination), so each is paginated independently by its own state.
  function initCodemapTablePager(table) {
    var pager = table.nextElementSibling;
    if (!pager || !pager.classList.contains("codemap-table-pager")) return;
    var rows = Array.prototype.slice.call(table.querySelectorAll(".codemap-table-row"));
    var pageSize = parseInt(table.getAttribute("data-page-size"), 10) || 30;
    if (rows.length <= pageSize) return; // everything already fits on one screen — leave the pager hidden

    var prevBtn = pager.querySelector(".codemap-table-pager-prev");
    var nextBtn = pager.querySelector(".codemap-table-pager-next");
    var status = pager.querySelector(".codemap-table-pager-status");
    var totalPages = Math.ceil(rows.length / pageSize);
    var page = 0;

    function render() {
      rows.forEach(function (row, i) {
        row.hidden = Math.floor(i / pageSize) !== page;
      });
      status.textContent = "Page " + (page + 1) + " of " + totalPages;
      prevBtn.disabled = page === 0;
      nextBtn.disabled = page === totalPages - 1;
    }

    prevBtn.addEventListener("click", function () {
      if (page === 0) return;
      page--;
      render();
      table.scrollIntoView({ block: "nearest" });
    });
    nextBtn.addEventListener("click", function () {
      if (page === totalPages - 1) return;
      page++;
      render();
      table.scrollIntoView({ block: "nearest" });
    });

    pager.hidden = false;
    render();
  }

  Array.prototype.forEach.call(document.querySelectorAll(".codemap-table"), function (table) {
    try { initCodemapTablePager(table); } catch (err) { /* degrade silently — the full server-ordered table stands */ }
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

    // Story 7.12 review: the panel now hosts TWO shapes (Treemap + Sunburst) behind a "View as" toggle, both
    // colorized by the SAME dimension dropdown — so the cell query spans the whole panel, not just the treemap's
    // own <svg>, and a dimension switch recolors whichever shape is showing (and the other, off-screen one, so
    // neither can drift stale). Directory-zoom below stays scoped to `svg` (.codemap-dir only exists there — the
    // sunburst's directory wedges carry the unrelated, non-zoomable .codemap-dir-sunburst class).
    var cells = Array.prototype.slice.call(panel.querySelectorAll(".codemap-cell"));
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
      churn: "churn",
      filetype: "file type"
    };

    // Capture each cell's server-baked base label/tooltip once, before any recolor, so repeated dimension
    // switches append to the ORIGINAL text rather than stacking onto a previously-appended suffix.
    // The tooltip is a static, server-built HTML card (data-tip-html) listing every metric, so it already satisfies
    // "color is never the sole signal" for any active dimension — no per-dimension tooltip rewrite is needed. Only
    // the aria-label (and the legend) track the active dimension, so we snapshot just the base label.
    // Linked cells put aria-label on the wrapping <a> (Tile pattern); unlinked cells keep it on the rect.
    function labelHost(c) {
      var a = c.closest && c.closest("a");
      return a || c;
    }
    cells.forEach(function (c) {
      if (!c.hasAttribute("data-base-label")) {
        c.setAttribute("data-base-label", labelHost(c).getAttribute("aria-label") || "");
      }
    });

    // Scoped to the ramp legend specifically — the discrete (file-type) legend's caption is static (there is only
    // ever one categorical dimension, so its text never needs rewriting on a dimension switch).
    var legendDim = panel.querySelector(".codemap-legend-ramp .codemap-legend-dim");
    var legendRamp = panel.querySelector(".codemap-legend-ramp");
    var legendDiscrete = panel.querySelector(".codemap-legend-discrete");

    // Both legend shapes are pre-rendered server-side (one hidden, matching whichever dimension is baked as the
    // default); a dimension switch only toggles which one is visible, never rewrites either one's content.
    function swapLegend(showDiscrete) {
      if (legendRamp) legendRamp.hidden = showDiscrete;
      if (legendDiscrete) legendDiscrete.hidden = !showDiscrete;
    }

    // Strips BOTH class families before applying the new one — a cell last colorized by file type must not carry
    // a stale type-* class after switching to a numeric dimension, and vice versa (the two are mutually exclusive
    // fill vocabularies, never combined).
    function clearFillClasses(c) {
      for (var l = 0; l <= 4; l++) c.classList.remove("level-" + l);
      c.classList.remove("level-none");
      Array.prototype.slice.call(c.classList).forEach(function (cls) {
        if (cls.indexOf("type-") === 0) c.classList.remove(cls);
      });
    }

    function recolor(dim) {
      var dimLabel = DIM_LABELS[dim] || dim;

      if (dim === "filetype") {
        // Categorical, not scaled — no bucket()/min-max scan (that machinery is for the numeric dimensions only).
        cells.forEach(function (c) {
          clearFillClasses(c);
          var key = c.getAttribute("data-filetype");
          var label = c.getAttribute("data-filetype-label") || key || "";
          if (key) c.classList.add("type-" + key);
          var baseLabel = c.getAttribute("data-base-label") || "";
          labelHost(c).setAttribute("aria-label", baseLabel + " — " + dimLabel + ": " + label);
        });
        swapLegend(true);
        return;
      }

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
        clearFillClasses(c);
        var v = metricFor(c, dim);
        var baseLabel = c.getAttribute("data-base-label") || "";
        var host = labelHost(c);
        if (v === null) {
          c.classList.add("level-none");
          host.setAttribute("aria-label", baseLabel + " — no data for " + dimLabel);
          return;
        }
        var lvl = bucket(isDate ? (v - min) : v, range);
        c.classList.add("level-" + lvl);
        // The bucket level (0-4) IS exactly what the color encodes, so it's the honest text equivalent —
        // never a raw day-number or other value the color itself doesn't literally represent.
        var levelText = lvl === 0 ? "lowest" : lvl === 4 ? "highest" : "level " + lvl + " of 4";
        host.setAttribute("aria-label", baseLabel + " — " + dimLabel + ": " + levelText);
      });
      if (legendDim) legendDim.textContent = "Colorized by " + dimLabel;
      swapLegend(false);
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

  // ---- Code ownership sunburst: live mode selector [Story 7.11, ADR 0010] ------------------
  // Progressive enhancement ONLY (NFR-5, reinterpreted by ADR 0010 for this opt-in surface): the server ships
  // a complete, correct sunburst pre-colored in the default share-% mode plus its full text-equivalent tree; with
  // JS off this block never runs and both stand on their own. Every wedge carries its generation-time-embedded
  // per-file data (data-share/data-dominant/data-contributors/data-last/data-owner) and the SVG root carries the
  // bounded top-author roster (data-top-authors) and the whole-tree "as of" day (data-asof) — nothing here ever
  // fetches live data or reads wall-clock time, so a mode switch is a pure re-read of already-embedded values
  // (FR31). The individual-author picker is built from the UNION of every wedge's own data-owner list (the full
  // roster present in the data), not just the bounded top-author palette — an alphabetical list, never a "top
  // contributors" ranking (FR-10).
  Array.prototype.forEach.call(document.querySelectorAll(".ownership-panel"), function (panel) {
    try { initOwnershipSunburst(panel); } catch (err) { /* degrade — the server-rendered share-% mode stands */ }
  });

  function initOwnershipSunburst(panel) {
    // data-top-authors/data-asof are panel-wide (both views share one dataset), so they're only ever read off
    // the sunburst SVG root — but wedges/cells are gathered across BOTH views (sunburst .ownership-wedge paths
    // AND treemap .ownership-cell rects) so a mode switch recolors whichever view the reader has toggled to,
    // and the other, currently-hidden one never goes stale for when they toggle back.
    var svg = panel.querySelector(".ownership-sunburst");
    var controls = panel.querySelector(".ownership-controls");
    if (!svg || !controls) return;

    var wedges = Array.prototype.slice.call(panel.querySelectorAll(".ownership-wedge, .ownership-cell"));
    if (wedges.length === 0) return;

    var topAuthors = [];
    try { topAuthors = JSON.parse(svg.getAttribute("data-top-authors") || "[]"); } catch (err) { topAuthors = []; }
    var asofRaw = svg.getAttribute("data-asof");
    var asof = asofRaw === null ? NaN : parseInt(asofRaw, 10);

    function ownerData(w) {
      var raw = w.getAttribute("data-owner");
      if (!raw) return [];
      try { var parsed = JSON.parse(raw); return Array.isArray(parsed) ? parsed : []; } catch (err) { return []; }
    }

    // Full contributor roster (alphabetical, union of every wedge's own bounded list) — the spotlight picker's
    // source. Never capped to the top-author palette (AC #2c: any contributor, not just a bounded top-N).
    var rosterSet = {};
    wedges.forEach(function (w) {
      ownerData(w).forEach(function (entry) {
        if (entry && typeof entry[0] === "string") rosterSet[entry[0]] = true;
      });
    });
    var roster = Object.keys(rosterSet).sort(function (a, b) { return a.localeCompare(b); });
    if (roster.length === 0) return; // no embedded contributor data at all — nothing to switch modes over

    var modeSelect = controls.querySelector(".ownership-mode-select");
    var authorSelect = controls.querySelector(".ownership-author-select");
    var authorWrap = controls.querySelector(".ownership-author-wrap");
    var thresholdInput = controls.querySelector(".ownership-threshold-input");
    var thresholdWrap = controls.querySelector(".ownership-threshold-wrap");
    if (!modeSelect || !authorSelect || !authorWrap || !thresholdInput || !thresholdWrap) return;

    roster.forEach(function (name) {
      var opt = document.createElement("option");
      opt.value = name;
      opt.textContent = name;
      authorSelect.appendChild(opt);
    });

    var FILL_CLASSES = ["level-0", "level-1", "level-2", "level-3", "level-4", "level-none",
      "owner-author-other", "spotlight-touched", "owner-spotlight-off", "owner-fresh", "owner-stale"];
    topAuthors.forEach(function (name, i) { FILL_CLASSES.push("owner-author-" + i); });

    function clearFillClasses(w) {
      FILL_CLASSES.forEach(function (cls) { w.classList.remove(cls); });
    }

    // Four mode-specific legend blocks (Charts.OwnershipLegend/-TopAuthorsLegend/-SpotlightLegend/
    // -StalenessLegend) — show exactly the one matching the active mode so the visible legend can never disagree
    // with what's actually colored (owner feedback: colors and legend must always match up).
    var legendShare = Array.prototype.slice.call(panel.querySelectorAll(".ownership-legend-share"));
    var legendTop = Array.prototype.slice.call(panel.querySelectorAll(".ownership-legend-top"));
    var legendSpotlight = Array.prototype.slice.call(panel.querySelectorAll(".ownership-legend-spotlight"));
    var legendStaleness = Array.prototype.slice.call(panel.querySelectorAll(".ownership-legend-staleness"));

    function swapLegend(mode) {
      function setHidden(list, hidden) { list.forEach(function (el) { el.hidden = hidden; }); }
      setHidden(legendShare, mode !== "share");
      setHidden(legendTop, mode !== "top");
      setHidden(legendSpotlight, mode !== "spotlight");
      setHidden(legendStaleness, mode !== "staleness");
    }

    function labelHost(w) {
      var a = w.closest && w.closest("a");
      return a || w;
    }

    // Snapshot each wedge's server-baked base label ONCE, before any recolor, so repeated mode switches append
    // to the ORIGINAL text rather than stacking suffixes (mirrors the Code Map dimension switch's own pattern).
    wedges.forEach(function (w) {
      if (!w.hasAttribute("data-base-label")) {
        w.setAttribute("data-base-label", labelHost(w).getAttribute("aria-label") || "");
      }
    });

    function setLabel(w, suffix) {
      var base = w.getAttribute("data-base-label") || "";
      var text = base + " — " + suffix;
      labelHost(w).setAttribute("aria-label", text);
      var title = w.querySelector("title");
      if (title) title.textContent = text;
    }

    function recolorShare() {
      wedges.forEach(function (w) {
        clearFillClasses(w);
        var raw = w.getAttribute("data-share");
        if (raw === null) { w.classList.add("level-none"); setLabel(w, "no git history"); return; }
        var pct = parseInt(raw, 10);
        var level = pct <= 25 ? 1 : pct <= 50 ? 2 : pct <= 75 ? 3 : 4;
        w.classList.add("level-" + level);
        setLabel(w, pct + "% dominant-author share");
      });
    }

    function recolorTopAuthors() {
      wedges.forEach(function (w) {
        clearFillClasses(w);
        var dominant = w.getAttribute("data-dominant");
        if (!dominant) { w.classList.add("level-none"); setLabel(w, "no git history"); return; }
        var idx = topAuthors.indexOf(dominant);
        if (idx >= 0) { w.classList.add("owner-author-" + idx); } else { w.classList.add("owner-author-other"); }
        setLabel(w, "dominant contributor: " + dominant);
      });
    }

    // Fixed real-unit day cutoffs (owner feedback: not a binary touched/not-touched flag — a recency spectrum,
    // "days since THIS contributor last touched the file"). Mirrors OwnershipShareLevel's fixed-cutoff
    // reasoning (meaningful on its own scale, never a moving target) rather than a per-render quartile split.
    // Only called with a real, known day-count — an unknown last-touch date is a distinct "unknown" state
    // handled by the caller, never silently coerced into the oldest bucket (that would fabricate a "long ago"
    // claim the embedded data never actually supports). [Review 2026-07-22]
    function spotlightRecencyLevel(daysAgo) {
      if (daysAgo <= 30) return 4;
      if (daysAgo <= 90) return 3;
      if (daysAgo <= 180) return 2;
      return 1;
    }

    function recolorSpotlight(name) {
      wedges.forEach(function (w) {
        clearFillClasses(w);
        var entry = ownerData(w).filter(function (e) { return e[0] === name; })[0];
        // Absence here means "not among this file's own embedded (capped) contributor list," not proven "never
        // touched this file" — a file with more contributors than the per-file cap could have a real, spotlighted
        // contributor who simply ranks below it for THIS file. Wording says "not among the tracked contributors,"
        // never the stronger (and sometimes false) "has not worked on this file." [Review 2026-07-22]
        if (!entry) { w.classList.add("owner-spotlight-off"); setLabel(w, name + " is not among this file's most-active tracked contributors"); return; }
        var lastDay = entry[2];
        var daysAgo = (lastDay === null || lastDay === undefined || isNaN(asof)) ? null : (asof - lastDay);
        if (daysAgo === null) {
          // Touched, but their own last-touch date wasn't embedded — an honest "unknown," never coerced into a
          // recency bucket the data doesn't actually support. [Review 2026-07-22]
          w.classList.add("level-none", "spotlight-touched");
          setLabel(w, name + " worked on this file (date unknown)");
          return;
        }
        var level = spotlightRecencyLevel(daysAgo);
        w.classList.add("level-" + level, "spotlight-touched");
        setLabel(w, name + " worked on this file (" + daysAgo + (daysAgo === 1 ? " day" : " days") + " ago)");
      });
    }

    function recolorStaleness(months) {
      wedges.forEach(function (w) {
        clearFillClasses(w);
        var raw = w.getAttribute("data-last");
        if (raw === null || isNaN(asof)) { w.classList.add("level-none"); setLabel(w, "no git history"); return; }
        var monthsAgo = (asof - parseInt(raw, 10)) / 30;
        var stale = monthsAgo >= months;
        w.classList.add(stale ? "owner-stale" : "owner-fresh");
        // Measures the FILE's own last-touch date, not anything contributor-specific — the label said "no
        // current contributor" before, which claimed more than the data (data-last has no author attached).
        // [Review 2026-07-22]
        setLabel(w, stale
          ? "not touched in " + Math.round(monthsAgo) + "+ months"
          : "touched within the last " + months + " months");
      });
    }

    function applyMode() {
      var mode = modeSelect.value;
      authorWrap.hidden = mode !== "spotlight";
      thresholdWrap.hidden = mode !== "staleness";
      swapLegend(mode);
      if (mode === "top") recolorTopAuthors();
      else if (mode === "spotlight") recolorSpotlight(authorSelect.value || roster[0]);
      else if (mode === "staleness") {
        var months = parseInt(thresholdInput.value, 10);
        recolorStaleness(isNaN(months) || months < 1 ? 6 : months);
      }
      else recolorShare();
    }

    controls.hidden = false;
    modeSelect.addEventListener("change", applyMode);
    authorSelect.addEventListener("change", applyMode);
    thresholdInput.addEventListener("input", applyMode);
    // Sync once at init: relying on the server-baked default (share mode) matching modeSelect's own default
    // option is fragile — a bfcache/back-navigation restore of a non-default select value would otherwise leave
    // the chart showing stale colors until the next manual interaction. [Review 2026-07-22]
    applyMode();
  }

  // ---- Planning <-> Code Impact Map: interactive weighted treemap (Story 21.3) -----------
  // The visitor multi-selects epics; we merge their touched files into one shared directory hierarchy and lay out
  // a squarified treemap — tiles SIZED by churn (lines changed), COLORED by commit count. Owner-directed redesign;
  // a deliberate crossing of the "pure-SVG, no JS" rule. Fully degrades: with JS off this block never runs, the
  // controls stay hidden, and the epic-grouped text list below is the content. [Story 21.3]
  var SVGNS = "http://www.w3.org/2000/svg";
  if (document.getElementById("impact-treemap")) {
    try { initImpactMap(); } catch (err) { /* degrade silently — the text list below stands */ }
  }

  function initImpactMap() {
    var dataEl = document.getElementById("impact-map-data");
    if (!dataEl) return;
    var payload = null;
    try { payload = JSON.parse(dataEl.textContent); } catch (err) { return; }
    if (!payload || !payload.epics || !payload.epics.length) return;

    var controls = document.querySelector(".impact-controls");
    var fallback = document.getElementById("impact-fallback");
    var treemapMount = document.getElementById("impact-treemap");
    var sunburstMount = document.getElementById("impact-sunburst");
    var countEl = document.querySelector(".impact-epic-filter .sprint-epic-filter-count");
    var boxes = Array.prototype.slice.call(document.querySelectorAll(".impact-epic-toggle"));
    if (!boxes.length || !treemapMount) return;

    // Reveal the interactive controls (emitted hidden for no-JS) and tuck the text list away (still one click).
    if (controls) controls.hidden = false;
    if (fallback) fallback.open = false;

    function el(name, attrs) {
      var e = document.createElementNS(SVGNS, name);
      for (var k in attrs) if (Object.prototype.hasOwnProperty.call(attrs, k)) e.setAttribute(k, attrs[k]);
      return e;
    }
    function dirOf(p) { var i = p.lastIndexOf("/"); return i < 0 ? "" : p.substring(0, i); }
    function baseOf(p) { var i = p.lastIndexOf("/"); return i < 0 ? p : p.substring(i + 1); }
    function fileTitle(f) {
      return f.p + " — " + f.c + (f.c === 1 ? " line" : " lines") + " changed · " + f.k + (f.k === 1 ? " commit" : " commits");
    }
    function emptyNote(container) {
      container.textContent = "";
      var note = document.createElement("p");
      note.className = "impact-treemap-empty";
      note.textContent = "Select at least one epic to see the code areas it touched.";
      container.appendChild(note);
    }

    // Merge the checked epics' files into one path -> {churn, commits, href} map.
    function mergedFiles() {
      var sel = Object.create(null);
      boxes.forEach(function (cb) { if (cb.checked) sel[cb.value] = true; });
      // A prototype-less map: repo file paths are attacker/repo-controlled strings, and a path literally named
      // "__proto__" would otherwise collide with the prototype setter on a plain {} and silently vanish.
      // [Review][Patch]
      var byPath = Object.create(null);
      payload.epics.forEach(function (ep) {
        if (!sel[String(ep.n)]) return;
        ep.f.forEach(function (f) {
          var cur = byPath[f.p];
          if (cur) { cur.c += f.c; cur.k += f.k; }
          else byPath[f.p] = { p: f.p, c: f.c, k: f.k, h: f.h };
        });
      });
      var arr = [];
      for (var key in byPath) if (Object.prototype.hasOwnProperty.call(byPath, key)) arr.push(byPath[key]);
      return arr;
    }

    // Group the merged files into a shared directory hierarchy (one directory level, then files within). Both the
    // group nodes AND the file nodes carry `.value` (churn) so the layout algorithms can size them.
    function groupByDir(files) {
      var map = Object.create(null);
      files.forEach(function (f) {
        f.value = f.c;
        var d = dirOf(f.p);
        if (!map[d]) map[d] = { name: d, value: 0, files: [] };
        map[d].value += f.c; map[d].files.push(f);
      });
      var groups = [];
      for (var g in map) if (Object.prototype.hasOwnProperty.call(map, g)) groups.push(map[g]);
      groups.sort(function (a, b) { return b.value - a.value; });
      groups.forEach(function (grp) { grp.files.sort(function (a, b) { return b.c - a.c; }); });
      return groups;
    }

    // Squarified treemap of {value} nodes into a rect; sets node.rect = {x,y,w,h}. Nodes pre-sorted desc by value.
    // Classic Bruls et al. worst-aspect-ratio strips. [Story 21.3]
    function worst(areas, len) {
      var sum = 0, max = -Infinity, min = Infinity, i;
      for (i = 0; i < areas.length; i++) { sum += areas[i]; if (areas[i] > max) max = areas[i]; if (areas[i] < min) min = areas[i]; }
      var s2 = sum * sum, len2 = len * len;
      return Math.max((len2 * max) / s2, s2 / (len2 * min));
    }
    function squarify(nodes, x, y, w, h) {
      var items = nodes.filter(function (n) { return n.value > 0; });
      var total = 0; items.forEach(function (n) { total += n.value; });
      if (total <= 0 || w <= 0 || h <= 0) return false;
      var scale = (w * h) / total;
      var areas = items.map(function (n) { return { n: n, a: n.value * scale }; });
      var cx = x, cy = y, cw = w, ch = h, i = 0;
      while (i < areas.length) {
        var len = Math.min(cw, ch);
        var row = [], rowA = 0, best = Infinity, j = i;
        while (j < areas.length) {
          var candA = rowA + areas[j].a;
          var cand = row.map(function (r) { return r.a; }); cand.push(areas[j].a);
          var wst = worst(cand, len);
          if (row.length === 0 || wst <= best) { row.push(areas[j]); rowA = candA; best = wst; j++; }
          else break;
        }
        if (cw >= ch) {
          var stripW = rowA / ch, yy = cy;
          row.forEach(function (r) { var hh = r.a / stripW; r.n.rect = { x: cx, y: yy, w: stripW, h: hh }; yy += hh; });
          cx += stripW; cw -= stripW;
        } else {
          var stripH = rowA / cw, xx = cx;
          row.forEach(function (r) { var ww = r.a / stripH; r.n.rect = { x: xx, y: cy, w: ww, h: stripH }; xx += ww; });
          cy += stripH; ch -= stripH;
        }
        i = j;
      }
    }

    function renderTreemap(container, groups, levelOf) {
      container.textContent = "";
      // A non-empty selection can still sum to zero total churn (e.g. binary-only attribution, which
      // legitimately counts as an attributed file with zero churn) — mirror renderSunburst's guard so the
      // treemap shows the same honest message instead of a blank SVG. [Review][Patch]
      var total = 0; groups.forEach(function (g) { total += g.value; });
      if (total <= 0) { emptyNote(container); return; }

      var W = Math.max(container.clientWidth || 640, 320);
      var H = Math.max(Math.min(Math.round(W * 0.6), 620), 360);
      var svg = el("svg", { "class": "impact-tm", viewBox: "0 0 " + W + " " + H, width: "100%", height: H, preserveAspectRatio: "xMidYMid meet" });

      squarify(groups, 0, 0, W, H);
      groups.forEach(function (grp) {
        if (!grp.rect || grp.rect.w < 2 || grp.rect.h < 2) return;
        var gx = grp.rect.x + 1, gy = grp.rect.y + 1, gw = grp.rect.w - 2, gh = grp.rect.h - 2;
        var labelH = (gw > 60 && gh > 30) ? 15 : 0;
        if (labelH) {
          var lbl = el("text", { "class": "impact-tm-dir", x: gx + 2, y: gy + 11 });
          lbl.textContent = grp.name || "(root)";
          svg.appendChild(el("rect", { "class": "impact-tm-dir-bg", x: gx, y: gy, width: gw, height: labelH }));
          svg.appendChild(lbl);
        }
        squarify(grp.files, gx, gy + labelH, gw, gh - labelH);
        grp.files.forEach(function (f) {
          if (!f.rect || f.rect.w < 1 || f.rect.h < 1) return;
          var host = f.h ? el("a", { href: f.h, "class": "impact-tm-link" }) : el("g", {});
          var rect = el("rect", { "class": "impact-tm-tile impact-level-" + levelOf(f.k), x: f.rect.x, y: f.rect.y, width: Math.max(f.rect.w - 1, 0.5), height: Math.max(f.rect.h - 1, 0.5) });
          var title = el("title", {}); title.textContent = fileTitle(f); rect.appendChild(title);
          host.appendChild(rect);
          if (f.rect.w > 46 && f.rect.h > 16) {
            var t = el("text", { "class": "impact-tm-label", x: f.rect.x + 3, y: f.rect.y + 12 });
            t.textContent = baseOf(f.p);
            host.appendChild(t);
          }
          svg.appendChild(host);
        });
      });
      container.appendChild(svg);
    }

    // Two-ring radial sunburst of the SAME merged hierarchy: an inner directory ring and an outer file ring, arcs
    // sized by churn (angular span) and — for files — colored by commit level. Same tooltips + click-through as the
    // treemap; the shared view toggle above swaps between them. [Story 21.3]
    var TAU = Math.PI * 2;
    function renderSunburst(container, groups, levelOf, fileCount) {
      container.textContent = "";
      var total = 0; groups.forEach(function (g) { total += g.value; });
      if (total <= 0) { emptyNote(container); return; }

      var W = Math.max(container.clientWidth || 640, 320);
      var size = Math.max(Math.min(W, 560), 320);
      var cx = size / 2, cy = size / 2;
      var rHole = size * 0.16, rDir = size * 0.30, rFile = size * 0.47;
      var svg = el("svg", { "class": "impact-sb", viewBox: "0 0 " + size + " " + size, width: "100%", height: "auto", preserveAspectRatio: "xMidYMid meet" });

      function polar(r, a) { return [cx + r * Math.cos(a), cy + r * Math.sin(a)]; }
      function arcPath(r0, r1, a0, a1) {
        if (a1 - a0 >= TAU) a1 = a0 + TAU - 1e-3; // a full circle can't be one arc segment — leave a hair's gap
        var large = (a1 - a0) > Math.PI ? 1 : 0;
        var p0 = polar(r1, a0), p1 = polar(r1, a1), p2 = polar(r0, a1), p3 = polar(r0, a0);
        return "M" + p0[0].toFixed(2) + " " + p0[1].toFixed(2) +
          "A" + r1.toFixed(2) + " " + r1.toFixed(2) + " 0 " + large + " 1 " + p1[0].toFixed(2) + " " + p1[1].toFixed(2) +
          "L" + p2[0].toFixed(2) + " " + p2[1].toFixed(2) +
          "A" + r0.toFixed(2) + " " + r0.toFixed(2) + " 0 " + large + " 0 " + p3[0].toFixed(2) + " " + p3[1].toFixed(2) + "Z";
      }

      var ang = -Math.PI / 2; // start at 12 o'clock
      groups.forEach(function (grp) {
        var gspan = (grp.value / total) * TAU;
        var gEnd = ang + gspan;
        var dseg = el("path", { "class": "impact-arc-dir", d: arcPath(rHole, rDir, ang, gEnd) });
        var dt = el("title", {}); dt.textContent = (grp.name || "(root)") + " — " + grp.files.length + (grp.files.length === 1 ? " file" : " files");
        dseg.appendChild(dt); svg.appendChild(dseg);

        var fang = ang;
        grp.files.forEach(function (f) {
          // grp.value can be 0 (a group whose only files are zero-churn) while other groups keep total > 0;
          // 0/0 would be NaN here even though gspan is already 0 for this group. [Review][Patch]
          var fspan = grp.value > 0 ? (f.c / grp.value) * gspan : 0;
          var fEnd = fang + fspan;
          var host = f.h ? el("a", { href: f.h, "class": "impact-sb-link" }) : el("g", {});
          var seg = el("path", { "class": "impact-arc impact-level-" + levelOf(f.k), d: arcPath(rDir, rFile, fang, fEnd) });
          var t = el("title", {}); t.textContent = fileTitle(f); seg.appendChild(t);
          host.appendChild(seg); svg.appendChild(host);
          fang = fEnd;
        });
        ang = gEnd;
      });

      var center = el("text", { "class": "impact-sb-center", x: cx.toFixed(1), y: cy.toFixed(1), "text-anchor": "middle", "dominant-baseline": "central" });
      center.textContent = fileCount + (fileCount === 1 ? " file" : " files");
      svg.appendChild(center);
      container.appendChild(svg);
    }

    function updateSummary() {
      if (!countEl) return;
      var n = 0; boxes.forEach(function (b) { if (b.checked) n++; });
      countEl.textContent = n === 0 ? "none" : n === boxes.length ? "all (" + n + ")" : n + " selected";
    }

    function render() {
      updateSummary();
      var files = mergedFiles();
      if (!files.length) {
        emptyNote(treemapMount);
        if (sunburstMount) emptyNote(sunburstMount);
        return;
      }
      // Commit-count -> 1..5 color buckets, relative to the current selection's max so a narrow filter still
      // reads a full ramp. Computed once and shared by both shapes so their colors agree.
      var maxK = 1;
      files.forEach(function (f) { if (f.k > maxK) maxK = f.k; });
      function levelOf(k) { var lv = Math.ceil((5 * k) / maxK); return lv < 1 ? 1 : lv > 5 ? 5 : lv; }

      var groups = groupByDir(files);
      renderTreemap(treemapMount, groups, levelOf);
      if (sunburstMount) renderSunburst(sunburstMount, groups, levelOf, files.length);
    }

    boxes.forEach(function (cb) { cb.addEventListener("change", render); });
    var allBtn = document.querySelector(".impact-select-all");
    var noneBtn = document.querySelector(".impact-select-none");
    if (allBtn) allBtn.addEventListener("click", function () { boxes.forEach(function (c) { c.checked = true; }); render(); });
    if (noneBtn) noneBtn.addEventListener("click", function () { boxes.forEach(function (c) { c.checked = false; }); render(); });

    // Re-render on Treemap|Sunburst toggle too — the shape being revealed was hidden (0 clientWidth) at the last
    // render, so its layout used the hardcoded fallback width instead of its real, now-visible container size.
    // [Review][Patch]
    Array.prototype.forEach.call(document.querySelectorAll('input[name="impact-view"]'), function (r) {
      r.addEventListener("change", render);
    });

    // Re-layout on resize (debounced) so both shapes track the container width.
    var resizeTimer = null;
    window.addEventListener("resize", function () {
      if (resizeTimer) clearTimeout(resizeTimer);
      resizeTimer = setTimeout(render, 150);
    });

    render();
  }

  // Work graph (Story 19.2): the scope <select> filters the page to one epic's subgraph (or "All epics").
  // Progressive enhancement — with JS off every .work-graph-section stays visible (the server default); JS only
  // focuses the chosen scope. Never throws: a failure leaves all sections shown.
  Array.prototype.forEach.call(document.querySelectorAll(".work-graph-scope-select"), function (sel) {
    try { initWorkGraphScope(sel); } catch (err) { /* degrade: all sections remain visible */ }
  });
  function initWorkGraphScope(sel) {
    var sections = document.querySelectorAll(".work-graph-section");
    if (!sections.length) return;
    function apply() {
      var v = sel.value;
      Array.prototype.forEach.call(sections, function (s) {
        s.hidden = v !== "__all__" && s.id !== v;
      });
    }
    sel.addEventListener("change", apply);
    apply(); // honor a restored (bfcache) selection on load
  }

  // ---- The Hierarchy Explorer component [Story 20.5 / ADR 0012 / ADR 0013] -------------------
  // ONE component renders every sunburst and treemap in the portal: one datasource, one selector, one explicit
  // activation mode. ADR 0010 §6 already required one shared charting engine as a CONVENTION and it did not hold —
  // three concurrent sessions produced three independent arc renderers in this very file. A shared component is
  // much harder to accidentally reinvent than a shared rule, which is the whole point of this block existing.
  //
  // Progressive enhancement, and in this story deliberately conservative about it (owner decision D1): the server
  // still emits the complete static sunburst SVG beneath the (hidden) chart host, and this block hides it ONLY
  // once Plotly has actually mounted. So a missing bundle, a CSP block, or a throw anywhere below leaves the page
  // exactly as the server rendered it. Nothing retires here; Story 20.7 does the deletions.
  //
  // THE TAKEOVER HANDSHAKE, and it must run before the Story 20.2 block below: on a successful mount we set
  // `data-explorer-ready` on the panel root, which is already 20.2's own skip guard. Success -> the component owns
  // the chart and 20.2's drill-in stands down with no new coordination code. Failure -> the flag is never set and
  // 20.2's drill-in takes over the still-visible SVG unchanged. Any scheme that hid the SVG before the mount
  // succeeded could leave a page with no chart at all, which is why this is the only mechanism used.
  var hierarchyMounts = [];

  function initHierarchyExplorers(scope) {
    var host = scope && scope.querySelectorAll ? scope : document;
    // Purge instances whose host left the document (the SPA swaps the content region via innerHTML, which detaches
    // the graph div while `responsive: true` keeps a window listener alive — a naive re-init leaks one per swap).
    for (var i = hierarchyMounts.length - 1; i >= 0; i--) {
      if (!document.contains(hierarchyMounts[i])) {
        try { if (window.Plotly && Plotly.purge) Plotly.purge(hierarchyMounts[i]); } catch (e) { /* already gone */ }
        hierarchyMounts.splice(i, 1);
      }
    }
    Array.prototype.forEach.call(host.querySelectorAll("[data-hierarchy]"), function (root) {
      if (root.getAttribute("data-hierarchy-ready")) return;
      try {
        if (initHierarchyExplorer(root)) {
          root.setAttribute("data-hierarchy-ready", "1");
          hierarchyMounts.push(root);
        } else {
          // Declined rather than threw (no engine, no island) — same outcome for the reader, so release the
          // placeholder immediately and let the server SVG be the page.
          var declined = root.closest("[data-explorer]");
          if (declined) declined.setAttribute("data-hierarchy-failed", "1");
        }
      } catch (err) {
        // Degrade to the untouched server chart, and do it NOW rather than leaving the visitor watching a
        // placeholder until the inline script's timer expires. Per root, so one bad instance cannot down the page.
        var failed = root.closest("[data-explorer]");
        if (failed) failed.setAttribute("data-hierarchy-failed", "1");
      }
    });
  }

  function initHierarchyExplorer(root) {
    // No engine, no takeover. Checked first so a blocked or absent bundle costs nothing and changes nothing.
    if (typeof Plotly === "undefined" || !Plotly.react || !Plotly.newPlot) return false;

    var dataEl = document.getElementById(root.id + "-data");
    if (!dataEl) return false;
    var payload;
    try { payload = JSON.parse(dataEl.textContent); } catch (e) { return false; }
    var cfg = payload && payload.config;
    var NODES = (payload && payload.nodes) || [];
    if (!cfg || !NODES.length) return false;

    var panel = root.closest("[data-explorer]") || root.parentNode;
    var live = panel.querySelector(".ss-hierarchy-live");
    var drillBar = panel.querySelector(".ss-hierarchy-drill");
    var crumbList = panel.querySelector(".ss-hierarchy-breadcrumb");
    var controls = panel.querySelector(".ss-hierarchy-controls");
    var selectMode = cfg.mode === "select";

    // Prototype-less maps: node ids come from author-controlled markdown (`### Story N.M:` headings, which nothing
    // dedupes), so an id of "constructor" or "__proto__" would otherwise resolve to an inherited Object member and
    // blow up every lookup below — reachable from a crafted hash. Same hardening the Story 20.2 block carries.
    var byId = Object.create(null), childrenOf = Object.create(null), indexOf = Object.create(null), depthOf = Object.create(null);
    NODES.forEach(function (n, i) {
      if (byId[n.id] === undefined) { byId[n.id] = n; indexOf[n.id] = i; }
      if (n.parentId) { (childrenOf[n.parentId] = childrenOf[n.parentId] || []).push(n); }
    });
    function depth(id) {
      if (depthOf[id] !== undefined) return depthOf[id];
      var d = 0, cur = byId[id], guard = 0;
      while (cur && cur.parentId && byId[cur.parentId] && guard++ < 64) { d++; cur = byId[cur.parentId]; }
      depthOf[id] = d;
      return d;
    }
    function hasChildren(id) { return !!(childrenOf[id] && childrenOf[id].length); }
    var ROOT_ID = NODES[0] && !NODES[0].parentId ? NODES[0].id : null;

    /* --- Tokens: resolved from the SHIPPED cascade, never re-typed ------------------------------------------
       Only the statusClass -> CSS class mapping is written here; every colour VALUE is read back out of
       specscribe.css through a real element carrying the real class. A hard-coded hex would survive a token
       change and quietly lie about it (AD-7). */
    var STATUS_CLASS = {
      done: "sb-done", active: "sb-active", review: "sb-review", ready: "sb-ready",
      drafted: "sb-drafted", pending: "sb-pending", noplan: "sb-noplan",
      "followup-open": "sb-followup-open", "followup-done": "sb-followup-done",
      unplanned: "sb-unplanned", unrecognized: "sb-unrecognized"
    };
    // UX-DR17: the shipped SVG distinguishes follow-up and no-plan wedges by a DASHED STROKE as well as fill.
    // Plotly's marker.line has no `dash`, so per-sector hatching replaces that channel — a stronger one, and the
    // reason no state here is signalled by colour alone.
    var PATTERN_SHAPE = {
      "sb-followup-open": "/", "sb-followup-done": "\\", "sb-noplan": ".", "sb-unplanned": "x"
    };

    var probeHost = document.createElement("div");
    probeHost.setAttribute("aria-hidden", "true");
    probeHost.style.cssText = "position:absolute;left:-9999px;width:0;height:0;overflow:hidden";
    document.body.appendChild(probeHost);
    var tokenCache = Object.create(null);
    function tokenFor(cls) {
      if (tokenCache[cls]) return tokenCache[cls];
      var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      svg.setAttribute("class", "sunburst");
      var path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      path.setAttribute("class", "sb-seg " + cls);
      svg.appendChild(path);
      probeHost.appendChild(svg);
      var cs = getComputedStyle(path);
      tokenCache[cls] = { fill: cs.fill, stroke: cs.stroke };
      return tokenCache[cls];
    }
    function fillFor(statusClass) {
      var t = tokenFor(STATUS_CLASS[statusClass] || "sb-unrecognized");
      var f = t.fill;
      // `.sb-noplan` is fill:transparent in the shipped chart, and Plotly needs a real paint per sector — so fall
      // back to the token that rule uses for its STROKE. Still a shipped token, still no literal.
      if (!f || f === "none" || f === "transparent" || f === "rgba(0, 0, 0, 0)") return t.stroke;
      return f;
    }
    function patternFor(statusClass) { return PATTERN_SHAPE[STATUS_CLASS[statusClass]] || ""; }
    var inkColor = tokenFor("sb-unrecognized").fill;
    var edgeColor = tokenFor("sb-done").stroke || inkColor;

    // Label legibility (owner verify round 2026-07-25: "font readability is tough"). One ink colour across every
    // sector cannot work: the palette spans a dark teal and a pale parchment, so a single mid-grey is unreadable on
    // one end or the other. Pick per sector by the fill's own relative luminance, and take BOTH candidate colours
    // from the shipped cascade rather than typing them.
    var rootStyle = getComputedStyle(document.documentElement);
    function cssVar(name, fallback) {
      var v = rootStyle.getPropertyValue(name).trim();
      return v || fallback;
    }
    var onDarkColor = cssVar("--warm-white", "#fff");
    var onLightColor = cssVar("--ink", inkColor);
    function luminance(color) {
      var m = /rgba?\(([^)]+)\)/.exec(color);
      if (!m) return 1;
      var p = m[1].split(",").map(parseFloat);
      // Rec. 601 luma — enough to choose between two candidates, and cheap.
      return (0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]) / 255;
    }
    function textOn(statusClass) {
      return luminance(fillFor(statusClass)) < 0.55 ? onDarkColor : onLightColor;
    }

    /* --- State --------------------------------------------------------------------------------------------- */
    var state = { shape: cfg.shape === "treemap" ? "treemap" : "sunburst", level: null, focusIndex: 0, selected: null };

    function esc(v) {
      return String(v == null ? "" : v)
        .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
    }
    // The rich tooltip card, matching the code map's `.codemap-card` shape: a muted kind eyebrow, the FULL title,
    // the prose status, and the human-meaningful detail. `value` — the layout number Plotly sizes sectors by — is
    // deliberately absent: the owner's verify round called it "a confusing value ... not helpful or intuitive for
    // the reader", and it is a rendering input, not a fact about the work.
    function tipCardFor(n) {
      var out = '<span class="ss-hierarchy-card">';
      out += '<span class="ss-hierarchy-card-kind">' + esc(kindWord(n)) + "</span>";
      out += '<span class="ss-hierarchy-card-name">' + esc(n.label) + "</span>";
      out += '<span class="ss-hierarchy-card-status">' + esc(n.statusLabel) + "</span>";
      if (n.detail) out += '<span class="ss-hierarchy-card-detail">' + esc(n.detail) + "</span>";
      if (n.href) out += '<span class="ss-hierarchy-card-hint">' + esc(hintFor(n)) + "</span>";
      return out + "</span>";
    }
    function kindWord(n) {
      switch (n.kind) {
        case "project": return "Project";
        case "epic": return "Epic";
        case "story": return "Story";
        case "story-summary": return "Stories";
        case "unplanned": return "Direct work";
        default: return "Follow-ups";
      }
    }
    // States the ONE thing activating this node will do, so the drill-vs-activate grammar is discoverable rather
    // than something a visitor has to infer by clicking and being surprised.
    function hintFor(n) {
      if (hasChildren(n.id)) return "Click to zoom in";
      return selectMode ? "Click to select" : "Click to open";
    }

    function buildTrace() {
      var t = {
        type: state.shape,
        ids: NODES.map(function (n) { return n.id; }),
        parents: NODES.map(function (n) { return n.parentId || ""; }),
        // The SHORT label is what gets drawn in a sector; the full one rides in customdata for the hover card.
        // uniformtext sizes every label alike and hides what will not fit, so one long title silences the chart.
        labels: NODES.map(function (n) { return n.shortLabel || n.label; }),
        customdata: NODES.map(function (n) { return n.label; }),
        // Every value is a NUMBER. A single null anywhere in `values` collapses calcdata to one point and renders
        // nothing — no error, no console warning. The emitter guarantees it; this never re-derives it.
        values: NODES.map(function (n) { return n.value; }),
        // Emitted by the server alongside the payload, because a payload/branchvalues mismatch draws a blank or
        // wrong chart with only a console warning. The two must be decided together, so they travel together.
        branchvalues: cfg.branchvalues || "total",
        marker: {
          colors: NODES.map(function (n) { return fillFor(n.statusClass); }),
          // Per-sector, because this is ALSO the selection ring. CSS cannot draw it: setting `stroke` on one of
          // Plotly's `path.surface` nodes is inert (verified against ink geometry, and inert even from an inline
          // `!important`). `marker.line` is the channel that paints the separators, so it is the one that works.
          // Width AND colour both change, so the selection is never signalled by colour alone (UX-DR17).
          line: {
            // The ring takes the SAME per-sector contrast pick the labels use, not one fixed accent: a gold ring
            // on a gold "ready" sector is invisible, and the selection can land on any status.
            color: NODES.map(function (n) { return n.id === state.selected ? textOn(n.statusClass) : edgeColor; }),
            width: NODES.map(function (n) { return n.id === state.selected ? 4 : 1; })
          },
          pattern: {
            shape: NODES.map(function (n) { return patternFor(n.statusClass); }),
            fillmode: "overlay",
            // MUST be per-sector and explicit: left unset, Plotly paints the pattern's backing rect BLACK (67
            // occurrences measured), which is a default colour reaching the output.
            bgcolor: NODES.map(function (n) { return fillFor(n.statusClass); }),
            fgcolor: NODES.map(function () { return inkColor; }),
            size: 6,
            solidity: 0.28
          }
        },
        // Status as TEXT, so nothing is signalled by colour alone even to a viewer who cannot distinguish fill or
        // hatch at all. Prose, never the CSS class.
        text: NODES.map(function (n) { return n.statusLabel; }),
        // Plotly's own hover card is switched OFF: the portal already has one tooltip and this component uses it
        // (see `.ss-hierarchy-sector` in SEG above), so a chart does not get a second look just because a different
        // engine drew it. [owner verify round: "we lost some of the pretty formatting we used on our tooltips"]
        hoverinfo: "none",
        // Draw order stays the emitter's order, which is the SVG's draw order.
        sort: false,
        // Both font slots plus layout.font below. With only `insidetextfont` set, the ROOT label alone took
        // Plotly's default rgb(68,68,68) — one element out of 119, exactly the kind of miss a config-level
        // assertion never catches.
        insidetextfont: { color: NODES.map(function (n) { return textOn(n.statusClass); }), weight: 700 },
        outsidetextfont: { color: onLightColor, weight: 700 }
      };
      if (state.shape === "sunburst") {
        t.leaf = { opacity: 1 };
        t.textinfo = cfg.labels ? "label" : "none";
        t.insidetextorientation = "radial";
      } else {
        t.textinfo = cfg.labels ? "label+value" : "none";
      }
      if (state.level) t.level = state.level;
      return t;
    }

    function layout() {
      return {
        margin: { l: 0, r: 0, t: 0, b: 0 },
        // Belt and braces. The per-sector colour array above does the real work; these make a MISS impossible
        // rather than merely unlikely, so Plotly's own palette can never reach the page.
        colorway: [inkColor],
        sunburstcolorway: [inkColor],
        extendsunburstcolors: false,
        treemapcolorway: [inkColor],
        extendtreemapcolors: false,
        paper_bgcolor: "rgba(0,0,0,0)",
        plot_bgcolor: "rgba(0,0,0,0)",
        font: { family: getComputedStyle(document.body).fontFamily, color: inkColor },
        transition: { duration: 0 },
        // `hide`, not shrink: a label Plotly cannot fit is dropped rather than scaled down to illegibility.
        uniformtext: { mode: "hide", minsize: 9 }
      };
    }

    // Privacy and offline are SETTINGS here, not defaults. `displayModeBar:false` is load-bearing rather than
    // cosmetic: plotly.js 3.7.0 changed the modebar's cloud button to upload the chart to Plotly Cloud, and a
    // local-first generator must never ship that control. The empty URLs remove every remote origin the bundle
    // could otherwise consult.
    var CONFIG = {
      displayModeBar: false,
      displaylogo: false,
      plotlyServerURL: "",
      topojsonURL: "",
      showTips: false,
      scrollZoom: false,
      doubleClick: false,
      responsive: true
    };

    /* --- Accessibility layer -------------------------------------------------------------------------------
       Applied ONLY through `plotly_afterplot`, Plotly's public post-render event, over its emitted DOM. No Plotly
       internal is patched or forked. Two reasons that specific hook and not the promise Plotly.react returns:
         1. it is the only hook that also fires for re-renders this component did NOT initiate (a responsive
            resize, a host-driven relayout) — which is what "survives" has to mean;
         2. Plotly resolves its own promises off an animation frame, so awaiting one never settles in a
            non-compositing tab. Measured, not assumed. */
    function sectorNodes() {
      var els = Array.prototype.slice.call(root.querySelectorAll("g.slice path.surface"));
      // Rove order is RING order — level first, then angular position — not Plotly's DOM order. Within a ring the
      // payload's own index is the angular order, because the emitter's order is the draw order and `sort:false`
      // preserves it.
      els.sort(function (a, b) {
        var na = idOf(a), nb = idOf(b);
        var da = na ? depth(na) : 0, db = nb ? depth(nb) : 0;
        if (da !== db) return da - db;
        return (na && indexOf[na] !== undefined ? indexOf[na] : 0) - (nb && indexOf[nb] !== undefined ? indexOf[nb] : 0);
      });
      return els;
    }
    function idOf(el) {
      var d = el.parentNode && el.parentNode.__data__;
      return d && d.data && d.data.data ? d.data.data.id : null;
    }
    function announce(msg) { if (live) live.textContent = msg; }

    function applyA11yLayer() {
      var els = sectorNodes();
      // Clamp on EVERY re-render. If the previously focused sector's index exceeds the new (smaller) count after a
      // drill, no element receives tabindex="0" and the chart becomes unreachable by Tab until an arrow key or a
      // fresh click re-establishes focus. The probe's version did not fire only because the tested epic's index
      // happened to stay in bounds.
      if (state.focusIndex >= els.length) state.focusIndex = els.length ? els.length - 1 : 0;
      if (state.focusIndex < 0) state.focusIndex = 0;

      // The tree lives on the HOST, not on Plotly's <svg>: an <svg> carrying role="tree" with the items nested
      // inside its <g>s puts presentational wrappers between a tree and its treeitems. The wrappers are marked
      // presentational instead, and each item carries aria-level / aria-posinset / aria-setsize — the standard
      // ARIA pattern for a tree whose items cannot be physically nested.
      root.setAttribute("role", "tree");
      root.setAttribute("aria-label", (cfg.title || "Work hierarchy") + " — " + state.shape);
      Array.prototype.forEach.call(root.querySelectorAll("svg"), function (s) { s.setAttribute("role", "presentation"); });
      Array.prototype.forEach.call(root.querySelectorAll("g.slice text"), function (t) { t.setAttribute("aria-hidden", "true"); });

      els.forEach(function (el, i) {
        var id = idOf(el);
        var n = id ? byId[id] : null;
        el.setAttribute("role", "treeitem");
        el.setAttribute("tabindex", i === state.focusIndex ? "0" : "-1");
        // Opt this sector into the portal's shared tooltip (see SEG) and mark the current selection so it reads as
        // selected rather than merely focused. Both re-applied on every render — Plotly rebuilds these nodes.
        if (el.classList) el.classList.add("ss-hierarchy-sector");
        if (n) {
          el.setAttribute("data-tip-html", tipCardFor(n));
          if (state.selected && n.id === state.selected) el.setAttribute("data-ss-selected", "1");
          else el.removeAttribute("data-ss-selected");
          // Status as PROSE, never the CSS class. The 20.4 probe read "— done, weight 44" precisely because it
          // used the class; UX-DR17/19 want words.
          el.setAttribute("aria-label", n.label + " — " + n.statusLabel + (n.detail ? ", " + n.detail : ""));
          el.setAttribute("aria-level", String(depth(n.id) + 1));
          var sibs = n.parentId ? (childrenOf[n.parentId] || []) : [n];
          var pos = 0;
          for (var k = 0; k < sibs.length; k++) { if (sibs[k].id === n.id) { pos = k + 1; break; } }
          el.setAttribute("aria-posinset", String(pos || 1));
          el.setAttribute("aria-setsize", String(sibs.length || 1));
          // Every parent that is DRAWN has its children drawn too (the payload is three levels deep and no
          // maxdepth is set), so a drawn parent is by definition expanded. If a future instance sets maxdepth this
          // must become a check against the sectors actually present.
          if (hasChildren(n.id)) el.setAttribute("aria-expanded", "true");
          else el.removeAttribute("aria-expanded");
        } else if (!el.getAttribute("aria-label")) {
          el.setAttribute("aria-label", "chart sector");
        }
        if (!el.__ssHierBound) {
          el.__ssHierBound = true;
          el.addEventListener("keydown", onKeydown);
          el.addEventListener("focus", function () {
            var idx = sectorNodes().indexOf(el);
            if (idx >= 0) state.focusIndex = idx;
          });
        }
      });

      if (state.refocusAfterPlot && els.length) {
        state.refocusAfterPlot = false;
        // Only when the caret was already inside the chart — never steal focus from the rail or the page.
        if (!document.activeElement || document.activeElement === document.body || root.contains(document.activeElement)) {
          els[state.focusIndex].focus();
        }
      }
    }

    function focusAt(i) {
      var els = sectorNodes();
      if (!els.length) return;
      state.focusIndex = ((i % els.length) + els.length) % els.length;
      els.forEach(function (el, j) { el.setAttribute("tabindex", j === state.focusIndex ? "0" : "-1"); });
      els[state.focusIndex].focus();
      announce(els[state.focusIndex].getAttribute("aria-label") || "");
    }

    function onKeydown(ev) {
      var els = sectorNodes();
      var i = els.indexOf(ev.currentTarget);
      switch (ev.key) {
        case "ArrowRight": case "ArrowDown": ev.preventDefault(); focusAt(i + 1); break;
        case "ArrowLeft": case "ArrowUp": ev.preventDefault(); focusAt(i - 1); break;
        case "Home": ev.preventDefault(); focusAt(0); break;
        case "End": ev.preventDefault(); focusAt(els.length - 1); break;
        case "Enter": case " ": case "Spacebar": ev.preventDefault(); activate(idOf(ev.currentTarget)); break;
        case "Escape": ev.preventDefault(); drillUp(); break;
        default: return;
      }
    }

    /* --- The activation grammar, stated once [AC #3] -------------------------------------------------------
       Plotly drills on click by default; that is CANCELLED (the click handler honours a `false` return, and the
       event carries the level it would have moved to) and the level re-applied here, so exactly one thing happens
       per activation and drill-in stays a distinct affordance from activation:
         node WITH children -> primary action is DRILL IN. Its own destination stays reachable from the
                               breadcrumb's "Open page" link once drilled, and from the text twin.
         LEAF               -> primary action is ACTIVATE: `navigate` mode follows the node's href (the Story 9.13
                               destination contract); `select` mode raises the selection and does NOT navigate.
         Escape / crumb     -> DRILL UP.
       This is the Story 20.2 grammar extended by mode, chosen because a per-sector secondary control is not
       expressible in a chart sector at all. */
    function activate(id) {
      var n = id ? byId[id] : null;
      if (!n) return;
      if (id === state.level) { drillUp(); return; }
      if (hasChildren(id)) { state.selected = null; drillTo(id, true); return; }
      if (selectMode) {
        // A selection is a THIRD state, distinct from focus and from the drill scope: the owner's verify round
        // showed a picked leaf reading as nothing at all. Paint it, announce it, and publish it for the rail.
        state.selected = id;
        // The ring lives in the trace, so selecting redraws. Redraw replaces every sector node, which would drop
        // keyboard focus to <body> — the exact defect Story 20.3's review had to fix on its own pane — so the
        // a11y pass restores it once the new nodes exist.
        state.refocusAfterPlot = true;
        redraw();
        publishSelection(id);
        announce("Selected " + n.label + ". " + n.statusLabel + (n.detail ? ", " + n.detail : "") + ".");
        return;
      }
      if (n.href) location.href = n.href;
    }

    function drillTo(id, pushHash) {
      if (id && !byId[id]) id = null;
      // The synthesized project root is a TREE root, not a scope. Left as a level it draws exactly what no level
      // draws, but it would publish `data-sb-scope="__project__"` and `#sb=__project__` — sending the Story 20.3
      // rail hunting for a card that cannot exist and putting a meaningless id in a shareable link. "Everything"
      // has one representation here, and it is the absence of a scope.
      if (id === ROOT_ID) id = null;
      if ((id || null) === state.level) return;
      state.level = id || null;
      redraw();
      applyState(pushHash);
      announce(state.level ? "Zoomed into " + byId[state.level].label : "Showing the whole project");
    }

    function drillUp() {
      if (!state.level) { announce("Already at the top of the hierarchy"); return; }
      var cur = byId[state.level];
      drillTo(cur && cur.parentId ? cur.parentId : null, true);
    }

    /* --- Selection seam: ADOPTED, never minted -------------------------------------------------------------
       `specscribe:explorer-select` is the event Story 20.3's details rail already listens for, and 20.3's record
       is explicit that 20.5 and 20.8 must adopt it rather than mint a second. `nodeId` is null at root scope.
       ORDERING HAZARD, already documented by 20.3: this block runs earlier in this file than the rail's listener,
       so the first event fires before that listener exists. The rail re-syncs on its own init by reading
       `data-sb-scope` off the panel — so publishing that attribute is not optional decoration; dropping it
       silently breaks a deep-linked page. */
    var publishedTokens = [];
    function publishSelection(explicitId) {
      var id = explicitId !== undefined ? explicitId : state.level;
      try {
        panel.dispatchEvent(new CustomEvent("specscribe:explorer-select", {
          bubbles: true,
          detail: { nodeId: id || null, label: id && byId[id] ? byId[id].label : null, root: panel }
        }));
      } catch (e) { /* no CustomEvent, or a throwing listener — the rail's server-rendered default stands */ }
    }

    // State only. The script publishes `data-sb-scope` plus one `data-tok-<status>` per status still drawn, and
    // the stylesheet decides what that means for the swatch strip — the pure-CSS contract a guard test pins, which
    // this block keeps true by naming no swatch class and touching no swatch node. `data-sb-scope` stays the DRILL
    // scope (never a leaf selection), because that is what "the statuses currently on screen" is derived from.
    function publishScopeState() {
      publishedTokens.forEach(function (t) { panel.removeAttribute("data-tok-" + t); });
      publishedTokens = [];
      if (!state.level) { panel.removeAttribute("data-sb-scope"); return; }
      panel.setAttribute("data-sb-scope", state.level);
      var seen = Object.create(null);
      (function walk(id) {
        var kids = childrenOf[id] || [];
        kids.forEach(function (k) { seen[k.statusClass] = true; walk(k.id); });
      })(state.level);
      seen[byId[state.level].statusClass] = true;
      for (var t in seen) {
        if (Object.prototype.hasOwnProperty.call(seen, t) && /^[a-z][a-z0-9-]*$/.test(t)) {
          panel.setAttribute("data-tok-" + t, "");
          publishedTokens.push(t);
        }
      }
    }

    /* --- Breadcrumb ---------------------------------------------------------------------------------------- */
    function renderCrumbs() {
      if (!crumbList) return;
      var chain = [];
      // Stops at ROOT_ID: the synthesized root is already the "top" crumb built below, so walking through it would
      // render the project name twice in a row.
      var cur = state.level, guard = 0;
      while (cur && cur !== ROOT_ID && byId[cur] && guard++ < 64) { chain.unshift(byId[cur]); cur = byId[cur].parentId; }
      while (crumbList.firstChild) crumbList.removeChild(crumbList.firstChild);
      var topLabel = ROOT_ID && byId[ROOT_ID] ? byId[ROOT_ID].label : "All epics";
      var top = document.createElement("li");
      if (chain.length) {
        var btn = document.createElement("button");
        btn.type = "button";
        btn.className = "ss-hierarchy-crumb";
        btn.textContent = topLabel;
        btn.addEventListener("click", function () { drillTo(null, true); });
        top.appendChild(btn);
      } else {
        var span = document.createElement("span");
        span.className = "ss-hierarchy-crumb ss-hierarchy-crumb-current";
        span.textContent = topLabel;
        top.appendChild(span);
      }
      crumbList.appendChild(top);
      chain.forEach(function (n, i) {
        var li = document.createElement("li");
        if (i === chain.length - 1) {
          var cs = document.createElement("span");
          cs.className = "ss-hierarchy-crumb ss-hierarchy-crumb-current";
          cs.textContent = n.label;
          li.appendChild(cs);
          // A drilled node's OWN destination stays reachable here — the counterpart to "a node with children
          // drills instead of opening". Without this the grammar would make group pages unreachable by pointer.
          if (n.href) {
            var open = document.createElement("a");
            open.className = "ss-hierarchy-crumb-open";
            open.href = n.href;
            open.textContent = "Open page →";
            li.appendChild(open);
          }
        } else {
          var b = document.createElement("button");
          b.type = "button";
          b.className = "ss-hierarchy-crumb";
          b.textContent = n.label;
          (function (id) { b.addEventListener("click", function () { drillTo(id, true); }); })(n.id);
          li.appendChild(b);
        }
        crumbList.appendChild(li);
      });
      if (drillBar) drillBar.hidden = false;
    }

    /* --- Hash deep-linking (UX-DR6) ------------------------------------------------------------------------
       Reuses the Story 20.2 fragment and history semantics rather than re-deriving them, and keeps the key
       configurable so 20.7's other instances can differ while the dashboard's existing `sb=` links keep working.
       Two behaviours that are corrections, not preferences: never destroy other fragment pairs on zoom-out (that
       ate in-page anchors like #glance), and under the SPA REPLACE rather than push, carrying the router's own
       {path, fragment} keys — a foreign state entry sends the SPA popstate handler down its "unknown state" path
       and tears the chart down mid-interaction. Extracted to component scope so 20.7's deletion of the 20.2 block
       does not take them with it. */
    var HASH_KEY = (cfg.hashKey || "sb") + "=";
    var spaHost = document.getElementById("spa-content");
    function hashWith(id) {
      var raw = location.hash.replace(/^#/, "");
      var parts = raw ? raw.split("&") : [];
      var kept = [];
      for (var i = 0; i < parts.length; i++) { if (parts[i].indexOf(HASH_KEY) !== 0 && parts[i]) kept.push(parts[i]); }
      if (id) kept.unshift(HASH_KEY + encodeURIComponent(id));
      return kept.length ? "#" + kept.join("&") : location.pathname + location.search;
    }
    function scopeFromHash() {
      var raw = location.hash.replace(/^#/, "");
      var parts = raw ? raw.split("&") : [];
      for (var i = 0; i < parts.length; i++) {
        if (parts[i].indexOf(HASH_KEY) === 0) {
          var id;
          // A hostile or merely stale fragment must not throw: decodeURIComponent rejects a lone '%'.
          try { id = decodeURIComponent(parts[i].slice(HASH_KEY.length)); } catch (e) { return null; }
          // Same normalization as drillTo: the tree root is not a scope, and an unknown or leaf id from a
          // stale/hostile fragment resolves to "no scope" rather than throwing or drilling somewhere odd.
          return id !== ROOT_ID && byId[id] && hasChildren(id) ? id : null;
        }
      }
      return null;
    }
    function syncHistory() {
      if (!window.history || !history.pushState) return;
      var url = hashWith(state.level);
      try {
        if (spaHost) {
          var path = spaHost.getAttribute("data-path") || "";
          history.replaceState({ path: path, fragment: url.charAt(0) === "#" ? url.slice(1) : "" }, "", url);
        } else {
          history.pushState({ ssHierarchy: state.level || "" }, "", url);
        }
      } catch (e) { /* history is unavailable (file:// in some engines) — the chart still works, links just aren't shareable */ }
    }

    /* --- Render -------------------------------------------------------------------------------------------- */
    function redraw() { return Plotly.react(root, [buildTrace()], layout(), CONFIG); }

    function applyState(pushHash) {
      renderCrumbs();
      publishScopeState();
      publishSelection();
      if (pushHash) syncHistory();
    }

    // --- Mount. Reveal the host and give it its configured height first (never a literal in this file), plot,
    // and only then hide the server chart. If newPlot throws, the host is hidden again and the SVG is simply the
    // page — which is the whole reason the SVG is still there in this story.
    root.style.height = (cfg.size || 380) + "px";
    root.style.maxWidth = "100%";
    root.setAttribute("data-hierarchy-ready", "1");
    state.level = scopeFromHash();
    try {
      Plotly.newPlot(root, [buildTrace()], layout(), CONFIG);
    } catch (e) {
      root.removeAttribute("data-hierarchy-ready");
      root.style.height = "";
      return false;
    }

    // Bound so even the FIRST render goes through the same public seam every later one does.
    root.on("plotly_afterplot", function () { applyA11yLayer(); });
    // Cancel Plotly's own drill animation — a 750 ms module constant with no schema attribute — and re-apply the
    // level ourselves. The re-apply goes through Plotly.react, which never animates, so the drill snaps by
    // construction and `prefers-reduced-motion` selects that same instant path a fortiori (UX-DR18). Any duration
    // this component ever does use is read from the shipped --motion-* tokens, never typed.
    root.on("plotly_sunburstclick", function (e) {
      activate(clickedId(e));
      return false;
    });
    root.on("plotly_treemapclick", function (e) {
      activate(clickedId(e));
      return false;
    });
    function clickedId(e) {
      var p = e && e.points && e.points[0];
      if (p && p.id !== undefined && p.id !== null) return p.id;
      return e && e.nextLevel ? e.nextLevel : null;
    }

    // --- Shape selector. Revealed only now, because switching a trace type needs script: with JS off it would be
    // an inert control, which is why the server ships it [hidden].
    if (controls) {
      controls.hidden = false;
      Array.prototype.forEach.call(controls.querySelectorAll(".ss-hierarchy-shape"), function (radio) {
        radio.addEventListener("change", function () {
          if (!radio.checked) return;
          state.shape = radio.value === "treemap" ? "treemap" : "sunburst";
          redraw();
          announce("Showing the " + state.shape);
        });
      });
    }

    // Both listeners are on `window`, so an SPA swap that detaches this host leaves them behind; without the
    // containment check they would call Plotly.react on a node that is no longer in the document.
    function onHistoryScope() {
      if (!document.contains(root)) return;
      var next = scopeFromHash();
      if ((next || null) !== state.level) { state.level = next; redraw(); applyState(false); }
    }
    window.addEventListener("hashchange", onHistoryScope);
    window.addEventListener("popstate", onHistoryScope);

    // --- The takeover. TWO parts, and the second is the one that has already bitten this epic: an SVG <a> at
    // display:none STAYS FOCUSABLE (unlike HTML), which is exactly how the Story 20.2 review's phantom tab stop
    // got in. The test suite structurally cannot see a stray tab stop, so both halves are done here and the tab
    // order is checked in a real browser.
    var legacySvg = panel.querySelector("svg.sunburst");
    if (legacySvg) {
      legacySvg.style.display = "none";
      legacySvg.setAttribute("aria-hidden", "true");
      Array.prototype.forEach.call(legacySvg.querySelectorAll("a"), function (a) { a.setAttribute("tabindex", "-1"); });
    }
    var legacyDrill = panel.querySelector(".sb-explorer-drill");
    if (legacyDrill) legacyDrill.hidden = true;
    // The handshake: 20.2's own skip guard. Set LAST, and only here, so it can only ever mean "mounted".
    panel.setAttribute("data-explorer-ready", "1");
    // Ends the boot placeholder and disarms the inline script's expiry timer (which would otherwise un-hide the
    // server SVG under a chart that mounted perfectly well).
    panel.setAttribute("data-hierarchy-mounted", "1");

    applyState(false);
    return true;
  }

  initHierarchyExplorers(document);
  document.addEventListener("specscribe:content-swapped", function (e) {
    initHierarchyExplorers(e && e.detail ? e.detail.root : document);
  });

  // ---- Remaining-work sunburst explorer: click-to-zoom drill-in [Story 20.2] ----------------
  // Progressive enhancement ONLY (NFR8). The server ships the complete static Story 10.7 sunburst (every wedge an
  // <a> to its Story 9.13 destination) PLUS one inline JSON island of the SAME weights/hierarchy the SVG drew. With
  // JS off this block never runs: the static chart + its links are the whole, correct experience, and the island is
  // inert data. This block adds, over that EXACT markup: activate a non-leaf wedge (epic with drawn stories) to zoom
  // in — its children re-lay to fill the rings via client arc RE-LAYOUT (the codemap's viewBox-pan does NOT transfer
  // to a sunburst; children must expand angularly, so we port Charts.AnnularSector/InsetStart/InsetEnd here) — with a
  // breadcrumb + center control to zoom back out. A LEAF wedge keeps its native link (opens the 9.13 destination the
  // server put on the <a> — never a parallel scheme). Zoom-out always restores each wedge's ORIGINAL server `d`, so
  // the un-drilled chart is byte-for-byte the static baseline. Presentation math only — no counts, no fetch. [Story 20.2]
  // Bootstrap is re-runnable: the SPA (specscribe-spa.js) replaces the content region with innerHTML, which both
  // discards our listeners and never executes an injected <script> — so a once-at-parse pass would leave the
  // explorer dead for the rest of an SPA session (the same class of defect HostRenderExceptions records for
  // Mermaid). The SPA therefore fires `specscribe:content-swapped` after every swap and we re-enhance the fresh
  // markup. `data-explorer-ready` keeps a root from being wired twice. [Story 20.2 review]
  function initSunburstExplorers(scope) {
    var host = scope && scope.querySelectorAll ? scope : document;
    Array.prototype.forEach.call(host.querySelectorAll("[data-explorer]"), function (root) {
      if (root.getAttribute("data-explorer-ready")) return;
      root.setAttribute("data-explorer-ready", "1");
      try { initSunburstExplorer(root); } catch (err) { /* degrade: static sunburst + 9.13 links stand */ }
    });
  }
  initSunburstExplorers(document);
  document.addEventListener("specscribe:content-swapped", function (e) {
    initSunburstExplorers(e && e.detail ? e.detail.root : document);
  });

  function initSunburstExplorer(root) {
    var svg = root.querySelector("svg.sunburst");
    // Select the island BY ID, not by first-match-on-type: document order must not decide which payload the
    // explorer parses if a second inline JSON island ever lands in this panel. (Story 20.3 was expected to add one
    // for the work-graph edges and did NOT — see the `edges` note in Charts.SunburstExplorerIsland — but keeping
    // the lookup id-anchored costs nothing and removes the hazard permanently.)
    var dataEl = root.querySelector('script[type="application/json"]#sunburst-explorer-data')
      || root.querySelector('script[type="application/json"]');
    if (!svg || !dataEl) return;
    var data;
    try { data = JSON.parse(dataEl.textContent); } catch (e) { return; }
    var meta = data && data.meta, nodes = (data && data.nodes) || [];
    if (!meta || !nodes.length) return;

    // Prototype-less maps throughout: node ids are derived from author-controlled markdown (story ids come straight
    // from `### Story N.M:` headings), so an id of "constructor" or "__proto__" would otherwise resolve to an
    // inherited Object member and blow up the lookups below — reachable from a crafted `#sb=` hash. Same hardening
    // the impact map already carries. [Story 20.2 review]
    var byId = Object.create(null), childrenOf = Object.create(null);
    nodes.forEach(function (n) {
      byId[n.id] = n;
      if (n.parentId) { (childrenOf[n.parentId] = childrenOf[n.parentId] || []).push(n); }
    });

    // Join each payload node to its wedge <path> + wrapping <a>, and CAPTURE the original `d` so zoom-out restores
    // the exact server geometry (keeping the un-drilled chart identical to the golden baseline).
    // Ids are NOT guaranteed unique (a repeated story heading yields two wedges with the same data-node-id), and a
    // last-write-wins map would leave the shadowed wedge permanently visible and un-restorable on drill. Keep EVERY
    // colliding element under one entry so hide/re-lay/restore always act on all of them.
    var wedges = Object.create(null);
    Array.prototype.forEach.call(svg.querySelectorAll(".sb-seg[data-node-id]"), function (p) {
      var id = p.getAttribute("data-node-id");
      var entry = { path: p, link: p.closest("a"), d0: p.getAttribute("d") };
      if (wedges[id]) { wedges[id].dupes = (wedges[id].dupes || []).concat([entry]); }
      else { wedges[id] = entry; }
    });
    // Apply fn across a node id's primary wedge AND any duplicates sharing that id.
    function eachWedge(id, fn) {
      var w = wedges[id];
      if (!w) return;
      fn(w);
      if (w.dupes) w.dupes.forEach(fn);
    }
    function eachWedgeAll(fn) {
      for (var id in wedges) { if (Object.prototype.hasOwnProperty.call(wedges, id)) eachWedge(id, fn); }
    }

    var TWO_PI = Math.PI * 2;
    var scope = null; // null = root (all epics)
    var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var live = root.querySelector(".sb-explorer-live");
    var drill = root.querySelector(".sb-explorer-drill");
    var crumbs = root.querySelector(".sb-explorer-breadcrumb");
    var centerBtn = null, animTimer = null;

    // The ring a wedge sits on is stated by the payload (`node.ring`), never inferred from `kind`: the server draws
    // an EPIC's open/done aggregates on the aggregate ring but the orphan/unplanned roots' aggregates on the STORY
    // ring, so a kind→ring guess is wrong by ~55px for those four wedges. `kind` stays semantic (it drives the
    // zoom-vs-open rule); `ring` is the presentation fact. Fallback keeps an older island shape working.
    // [Story 20.2 review]
    function bandFor(name) {
      if (name === "story") return [meta.storyInner, meta.storyOuter];
      if (name === "aggregate") return [meta.aggInner, meta.aggOuter];
      return [meta.epicInner, meta.epicOuter];
    }
    function ring(node) {
      if (node && node.ring) return bandFor(node.ring);
      var kind = node && node.kind;
      if (kind === "story" || kind === "story-summary") return bandFor("story");
      if (kind === "aggregate") return bandFor("aggregate");
      return bandFor("epic");
    }
    // A wedge zooms only if the chart actually DREW child stories under it (a dense-collapsed epic has just a
    // story-summary child and stays a leaf that opens its epic page — we never invent wedges the static chart hid).
    function drillable(id) {
      var ch = childrenOf[id] || [];
      for (var i = 0; i < ch.length; i++) { if (ch[i].kind === "story") return true; }
      return false;
    }

    // Ported presentation math (Charts.cs F()/AnnularSector/InsetStart/InsetEnd) — angles → SVG `d`. Not byte-exact
    // with the server (drilled arcs are a fresh view); the un-drilled restore uses the captured server `d`.
    function f(v) { return (Math.round(v * 100) / 100).toString(); }
    function annular(c, rI, rO, a0, a1) {
      if (a1 <= a0) a1 = a0 + 0.0001;
      var la = (a1 - a0) > Math.PI ? 1 : 0;
      var x1 = c + rO * Math.cos(a0), y1 = c + rO * Math.sin(a0),
        x2 = c + rO * Math.cos(a1), y2 = c + rO * Math.sin(a1),
        x3 = c + rI * Math.cos(a1), y3 = c + rI * Math.sin(a1),
        x4 = c + rI * Math.cos(a0), y4 = c + rI * Math.sin(a0);
      return "M " + f(x1) + " " + f(y1) + " A " + f(rO) + " " + f(rO) + " 0 " + la + " 1 " + f(x2) + " " + f(y2) +
        " L " + f(x3) + " " + f(y3) + " A " + f(rI) + " " + f(rI) + " 0 " + la + " 0 " + f(x4) + " " + f(y4) + " Z";
    }
    // A full annulus (the drilled epic's own inner band): outer circle CW + inner circle CCW so the non-zero winding
    // leaves the center hole open for the zoom-out control.
    function fullRing(c, rI, rO) {
      return "M " + f(c + rO) + " " + f(c) + " A " + f(rO) + " " + f(rO) + " 0 1 1 " + f(c - rO) + " " + f(c) +
        " A " + f(rO) + " " + f(rO) + " 0 1 1 " + f(c + rO) + " " + f(c) + " Z" +
        " M " + f(c + rI) + " " + f(c) + " A " + f(rI) + " " + f(rI) + " 0 1 0 " + f(c - rI) + " " + f(c) +
        " A " + f(rI) + " " + f(rI) + " 0 1 0 " + f(c + rI) + " " + f(c) + " Z";
    }
    function insetStart(a, s, pad) { return a + Math.min(pad, Math.max(0, s) / 2); }
    function insetEnd(a, s, pad) { return a + s - Math.min(pad, Math.max(0, s) / 2); }

    // Lay a set of sibling nodes across [a0, a0+total] on their ring, sized by weight. `pad` is per-call because the
    // server does NOT pad uniformly: AppendFollowUpSlot draws the open/done aggregate halves with pad:0 so they read
    // as one continuous band, and insetting them here opened a seam the static chart never has. [Story 20.2 review]
    function layRing(kids, a0, total, pad) {
      var slotPad = pad === undefined ? meta.pad : pad;
      var sum = 0;
      kids.forEach(function (k) { sum += Math.max(0, k.weight) || 0; });
      // No positive weight anywhere in this band: leaving the siblings at their un-drilled angles would paint a
      // coherent-looking ring that belongs to the previous scope, so hide them instead of returning silently.
      if (sum <= 0) {
        kids.forEach(function (k) { eachWedge(k.id, function (w) { (w.link || w.path).style.display = "none"; }); });
        return;
      }
      var per = total / sum, ang = a0;
      kids.forEach(function (k) {
        var sw = (Math.max(0, k.weight) || 0) * per, r = ring(k);
        var d = annular(meta.cx, r[0], r[1], insetStart(ang, sw, slotPad), insetEnd(ang, sw, slotPad));
        eachWedge(k.id, function (w) { w.path.setAttribute("d", d); });
        ang += sw;
      });
    }

    function motionFastMs() {
      try {
        var raw = getComputedStyle(document.documentElement).getPropertyValue("--motion-fast").trim();
        var ms = raw.indexOf("ms") >= 0 ? parseFloat(raw) : parseFloat(raw) * 1000;
        return ms > 0 ? ms : 240;
      } catch (e) { return 240; }
    }
    // The "tween": a brief token-timed fade on the re-laid wedges. Snaps under reduced motion (no class → no anim).
    function pulse() {
      if (reduce) return;
      svg.classList.add("is-anim");
      if (animTimer) clearTimeout(animTimer);
      animTimer = setTimeout(function () { svg.classList.remove("is-anim"); }, motionFastMs());
    }
    function announce(msg) { if (live) live.textContent = msg; }

    function restoreAll() {
      eachWedgeAll(function (w) {
        w.path.setAttribute("d", w.d0);
        (w.link || w.path).style.display = "";
      });
    }

    function drawScope(id) {
      var keep = Object.create(null); keep[id] = true;
      var kids = childrenOf[id] || [];
      kids.forEach(function (k) { keep[k.id] = true; });
      for (var wid in wedges) {
        if (!Object.prototype.hasOwnProperty.call(wedges, wid)) continue;
        var vis = keep[wid] ? "" : "none";
        eachWedge(wid, function (w) { (w.link || w.path).style.display = vis; });
      }
      var er = bandFor("epic");
      eachWedge(id, function (w) { w.path.setAttribute("d", fullRing(meta.cx, er[0], er[1])); });
      layRing(kids.filter(function (k) { return k.kind === "story" || k.kind === "story-summary"; }), meta.start, TWO_PI);
      // pad 0 mirrors AppendFollowUpSlot's own pad:0 — the open/done halves must meet with no seam.
      layRing(kids.filter(function (k) { return k.kind === "aggregate"; }), meta.start, TWO_PI, 0);
    }

    // ---- Text twin: keep the legend, hint and accessible name describing what is ACTUALLY on screen ----------
    // Under ADR 0013 the text equivalent IS the accessibility contract, so a drilled chart whose legend still
    // advertises statuses with zero visible wedges (and whose aria-label still says "Project progress sunburst")
    // is wrong, not merely untidy — the same phantom-entry class Story 10.7 and 21.1 each closed once.
    // [Story 20.2 review]
    var svgLabel0 = svg.getAttribute("aria-label") || "";
    var hintEl = root.querySelector(".sunburst-hint");
    var hint0 = hintEl ? hintEl.textContent : null;
    var publishedTokens = [];

    // Which status tokens the CURRENTLY VISIBLE wedges carry. Wedges are `sb-seg sb-<token>`; the same <token>
    // names the matching legend swatch, which is how the stylesheet pairs the two.
    function visibleTokens() {
      var seen = Object.create(null);
      Array.prototype.forEach.call(svg.querySelectorAll(".sb-seg[data-node-id]"), function (p) {
        var a = p.closest("a");
        if (a && a.style.display === "none") return;
        Array.prototype.forEach.call(p.classList, function (c) {
          if (c.indexOf("sb-") === 0 && c !== "sb-seg") seen[c.slice(3)] = true;
        });
      });
      return seen;
    }

    // Keep the chart's TEXT TWIN describing what is actually on screen — under ADR 0013 the text equivalent IS the
    // no-JS/accessibility contract, so a drilled chart still advertising statuses it no longer draws is wrong, not
    // untidy (the phantom-entry class Story 10.7 and 21.1 each closed once).
    //
    // The script publishes STATE ONLY — `data-sb-scope` plus one `data-tok-<token>` per status still on screen — and
    // the stylesheet decides which swatches show. Legend presentation stays pure CSS, which is the Story 3.5
    // contract `StylesheetTests.Script_DoesNotImplementLegendEmphasis` pins: this block names no legend class and
    // touches no legend node. [Story 20.2 review]
    function syncTextTwin() {
      svg.setAttribute("aria-label", scope && byId[scope]
        ? svgLabel0 + " — zoomed into " + byId[scope].label
        : svgLabel0);
      if (hintEl && hint0 !== null) {
        hintEl.textContent = scope && byId[scope]
          ? "Zoomed into " + byId[scope].label + " — the rings now show only this epic. Use the breadcrumb above, or the centre of the chart, to zoom back out."
          : hint0;
      }
      publishedTokens.forEach(function (t) { root.removeAttribute("data-tok-" + t); });
      publishedTokens = [];
      if (!scope) { root.removeAttribute("data-sb-scope"); return; }
      root.setAttribute("data-sb-scope", scope);
      var seen = visibleTokens();
      for (var t in seen) {
        // Attribute-name hygiene: only ever publish the known kebab-case status tokens.
        if (Object.prototype.hasOwnProperty.call(seen, t) && /^[a-z][a-z0-9-]*$/.test(t)) {
          root.setAttribute("data-tok-" + t, "");
          publishedTokens.push(t);
        }
      }
    }

    // The zoom-out control: a POINTER-ONLY center hit-area, present only while drilled. "center → zoom out" (AC #1).
    // Deliberately not focusable and not exposed to AT: the host <svg> carries role="img", whose descendants are
    // presentational, so a role="button"/tabindex="0" circle in here is a focus stop that assistive tech may never
    // name (WCAG 4.1.2). The keyboard/AT path to zoom out is the breadcrumb's real HTML "All epics" <button>, which
    // lives outside the SVG and is announced properly. [Story 20.2 review]
    function ensureCenter(show) {
      if (show) {
        if (!centerBtn) {
          centerBtn = document.createElementNS("http://www.w3.org/2000/svg", "circle");
          centerBtn.setAttribute("class", "sb-center-zoom");
          centerBtn.setAttribute("cx", f(meta.cx));
          centerBtn.setAttribute("cy", f(meta.cx));
          centerBtn.setAttribute("r", f(meta.epicInner));
          centerBtn.setAttribute("aria-hidden", "true");
          var out = document.createElementNS("http://www.w3.org/2000/svg", "title");
          out.textContent = "Zoom out to all epics";
          centerBtn.appendChild(out);
          centerBtn.addEventListener("click", function () { zoomTo(null, true); focusScope(); });
          svg.appendChild(centerBtn);
        }
        centerBtn.style.display = "";
      } else if (centerBtn) {
        centerBtn.style.display = "none";
      }
    }

    function renderCrumbs() {
      if (!crumbs) return;
      crumbs.innerHTML = "";
      var trail = [{ id: null, label: "All epics" }];
      if (scope) { var n = byId[scope]; trail.push({ id: scope, label: n ? n.label : scope }); }
      trail.forEach(function (t, idx) {
        var li = document.createElement("li"), last = idx === trail.length - 1;
        if (last) {
          var span = document.createElement("span");
          span.className = "sb-crumb-current";
          span.setAttribute("aria-current", "true");
          span.textContent = t.label;
          li.appendChild(span);
          // The drilled scope's OWN 9.13 group/detail page stays reachable via an explicit "open" link so a group
          // page is never orphaned by the zoom interaction (AC #2).
          if (t.id) {
            var w = wedges[t.id], href = w && w.link ? w.link.getAttribute("href") : null;
            if (href) {
              var a = document.createElement("a");
              a.className = "sb-crumb-open";
              a.href = href;
              a.textContent = "Open page";
              li.appendChild(a);
            }
          }
        } else {
          var btn = document.createElement("button");
          btn.type = "button";
          btn.className = "sb-crumb";
          btn.textContent = t.label;
          btn.addEventListener("click", function () { zoomTo(t.id || null, true); focusScope(); });
          li.appendChild(btn);
        }
        crumbs.appendChild(li);
      });
      if (drill) drill.hidden = !scope; // the bar shows only when there's somewhere to zoom back to
    }

    // Roving tabindex over the CURRENT scope's visible wedges (one tab stop; arrows move). Rebuilt on every zoom so
    // the tab order always matches what's on screen; never ships in the no-JS page (set at runtime only).
    function roveLinks() {
      var out = [];
      Array.prototype.forEach.call(svg.querySelectorAll(".sb-seg[data-node-id]"), function (p) {
        var a = p.closest("a");
        if (a && a.style.display !== "none") out.push(a);
      });
      return out;
    }
    function setRoving() {
      // Clear EVERY wedge link before re-arming the current scope's. Unlike an HTML element, an SVG <a> carrying
      // tabindex stays focusable at display:none, so a wedge hidden by a drill would otherwise keep the tabindex="0"
      // it was given at root state — a phantom tab stop on an invisible wedge. Verified in-browser. [Story 20.2 review]
      Array.prototype.forEach.call(svg.querySelectorAll(".sb-seg[data-node-id]"), function (p) {
        var a = p.closest("a");
        if (a) { a.setAttribute("tabindex", "-1"); a.removeAttribute("data-sb-rove"); }
      });
      roveLinks().forEach(function (a, i) {
        a.setAttribute("tabindex", i === 0 ? "0" : "-1");
        a.setAttribute("data-sb-rove", "1");
      });
    }
    function focusScope() { var l = roveLinks(); if (l.length) l[0].focus(); }
    // Activate a wedge the way Enter/Space should: drillable → zoom, leaf → follow its Story 9.13 destination.
    // NB these are SVG <a> elements (SVGAElement), which — unlike HTMLElement — have NO .click() method, so the
    // obvious `a.click()` throws and (because preventDefault ran first) silently eats the keypress. [Story 20.2 review]
    function activateWedge(a) {
      var p = a.querySelector(".sb-seg[data-node-id]");
      var id = p ? p.getAttribute("data-node-id") : null;
      if (id && drillable(id)) { zoomTo(id, true); return; }
      var href = a.getAttribute("href");
      if (href) location.href = href;
    }
    root.addEventListener("keydown", function (e) {
      var a = e.target.closest ? e.target.closest("a[data-sb-rove]") : null;
      if (!a) return;
      if (e.key === "ArrowRight" || e.key === "ArrowDown") { e.preventDefault(); rove(a, 1); }
      else if (e.key === "ArrowLeft" || e.key === "ArrowUp") { e.preventDefault(); rove(a, -1); }
      else if (e.key === " ") { e.preventDefault(); activateWedge(a); } // links ignore Space by default
    });
    function rove(a, d) {
      var l = roveLinks(), i = l.indexOf(a);
      if (i < 0) return;
      var n = (i + d + l.length) % l.length;
      l.forEach(function (x) { x.setAttribute("tabindex", "-1"); });
      l[n].setAttribute("tabindex", "0");
      l[n].focus();
    }

    function applyState(animate) {
      restoreAll();
      if (scope) { drawScope(scope); svg.classList.add("is-drilled"); ensureCenter(true); }
      else { svg.classList.remove("is-drilled"); ensureCenter(false); }
      renderCrumbs();
      setRoving();
      syncTextTwin();
      publishSelection();
      if (animate) pulse();
    }

    // THE named selection seam [Story 20.3]. Story 20.1's contract reserved a selection signal but never named one,
    // and 20.2 shipped without it — the explorer's only notion of "the item I am looking at" is its zoom scope. So
    // that is what is published, under one name, from the ONE place every scope change funnels through (applyState
    // covers click, Enter/Space, breadcrumb, centre control, hash and popstate alike). `nodeId` is null at the root
    // scope. Story 20.5's component and Story 20.8's details pane inherit this event rather than minting a second.
    // Guarded because a browser without the CustomEvent constructor must not break the drill-in the visitor
    // actually asked for; the pane then keeps its server-rendered default, which is a correct view rather than a
    // broken one. (A throwing listener does not need catching here — the DOM reports those to window.onerror
    // instead of propagating to the dispatcher — but the guard costs nothing and covers both.)
    function publishSelection() {
      try {
        root.dispatchEvent(new CustomEvent("specscribe:explorer-select", {
          bubbles: true,
          detail: { nodeId: scope, label: scope && byId[scope] ? byId[scope].label : null, root: root },
        }));
      } catch (e) { /* no CustomEvent, or a throwing listener — the pane's no-JS default stands */ }
    }

    // Rewrite location.hash's `sb=` pair WITHOUT destroying any other fragment the visitor arrived with — clearing
    // the whole hash on zoom-out silently ate in-page anchors like #glance. [Story 20.2 review]
    function hashWith(id) {
      var raw = location.hash.replace(/^#/, "");
      var parts = raw ? raw.split("&") : [];
      var kept = [];
      for (var i = 0; i < parts.length; i++) { if (parts[i].indexOf("sb=") !== 0 && parts[i]) kept.push(parts[i]); }
      if (id) kept.unshift("sb=" + encodeURIComponent(id));
      return kept.length ? "#" + kept.join("&") : location.pathname + location.search;
    }
    // Under the SPA the router owns history: a foreign state entry sends its popstate handler down the "unknown
    // state" path, which re-swaps the content region and tears the explorer down mid-interaction. So in SPA mode we
    // REPLACE rather than push — the drilled scope stays shareable/bookmarkable without minting entries the router
    // will misread — and we carry the router's own {path, fragment} keys so the state is never foreign. In the
    // static site nothing else owns history, so real pushState entries (and Back-to-zoom-out) are kept.
    var spaHost = document.getElementById("spa-content");
    function syncHistory() {
      if (!window.history || !history.pushState) return;
      var url = hashWith(scope);
      if (spaHost) {
        var path = spaHost.getAttribute("data-path") || "";
        history.replaceState({ path: path, fragment: url.charAt(0) === "#" ? url.slice(1) : "", sb: scope || "" }, "", url);
      } else {
        history.pushState({ sb: scope || "" }, "", url);
      }
    }

    function zoomTo(id, pushHash) {
      if (id && !byId[id]) id = null;
      if (id && !drillable(id)) return; // leaf: let the native <a> open its 9.13 destination
      if ((id || null) === scope) return; // already here — don't mint a duplicate history entry for a no-op zoom
      scope = id || null;
      applyState(true);
      announce(scope ? ("Zoomed into " + (byId[scope] ? byId[scope].label : scope)) : "Showing all epics");
      if (pushHash) syncHistory();
    }

    // Intercept activation on drillable wedges → zoom (Enter + click). Leaves keep their native link untouched.
    Array.prototype.forEach.call(svg.querySelectorAll(".sb-seg[data-node-id]"), function (p) {
      var id = p.getAttribute("data-node-id"), link = p.closest("a");
      if (!link || !drillable(id)) return;
      link.addEventListener("click", function (e) {
        // Respect explicit new-tab / new-window intents — a modified click still means "open the 9.13 destination",
        // exactly as specscribe-spa.js guards its own delegated navigation. Without this, Ctrl+click zoomed instead.
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        e.preventDefault();
        zoomTo(id, true);
      });
      link.addEventListener("keydown", function (e) { if (e.key === "Enter") { e.preventDefault(); zoomTo(id, true); } });
      var base = link.getAttribute("aria-label") || "";
      link.setAttribute("aria-label", base + " — activate to zoom in, or press the arrow keys to move between wedges");
    });

    function applyHash() {
      var m = /[#&]sb=([^&]+)/.exec(location.hash);
      var id = null;
      // A malformed percent-escape (#sb=100%) makes decodeURIComponent throw; on the popstate path that escapes
      // the init try/catch entirely. Treat an undecodable id as "no scope". [Story 20.2 review]
      if (m) { try { id = decodeURIComponent(m[1]); } catch (e) { id = null; } }
      if (id && (!byId[id] || !drillable(id))) id = null;
      var changed = (id || null) !== scope;
      scope = id;
      applyState(false); // snap on load / back-forward (no entrance animation)
      // Back/Forward changes the view just as the crumb does, so it must announce just as the crumb does.
      if (changed) announce(scope ? ("Zoomed into " + (byId[scope] ? byId[scope].label : scope)) : "Showing all epics");
    }
    window.addEventListener("popstate", applyHash);
    applyHash();
  }

  // ---- Related-work details rail: show the selected scope's card [Story 20.3] ---------------
  // Progressive enhancement ONLY (NFR8, AC #2). The server renders the project card PLUS one card per selectable
  // scope, each with its relationships expanded in a <details>. With JS off every card shows — the complete
  // relationship data. This block adds the fancy single-card behaviour: mark the pane [data-related-ready] (the CSS
  // then shows the project card by default and hides the per-scope cards + their <details>), and on a selection
  // (`specscribe:explorer-select`, raised by the Story 20.2 block above) reveal the ONE matching card. It never
  // fetches, never counts a project statistic, and never invents a link — every card was already server-rendered.
  // Re-runnable because the SPA replaces the content region with innerHTML.
  //
  // ONE document-level listener, registered once — deliberately NOT one per pane. The pane is a SIBLING of the
  // explorer root, so a bubbling event never passes through it; and re-registering inside the per-pane init would
  // leak a listener holding a detached pane on every SPA content swap.
  document.addEventListener("specscribe:explorer-select", function (e) {
    var d = (e && e.detail) || {};
    applyRelatedSelection(d.nodeId === undefined ? null : d.nodeId, d.label || null);
  });

  function initRelatedPanes(scope) {
    var host = scope && scope.querySelectorAll ? scope : document;
    var found = false;
    Array.prototype.forEach.call(host.querySelectorAll("[data-related-pane]"), function (pane) {
      if (pane.getAttribute("data-related-ready")) return;
      // Setting this attribute is what flips the rail from the JS-off "all cards" view to the single-card view —
      // it is the CSS hook, not just an init guard. [Story 20.3]
      pane.setAttribute("data-related-ready", "1");
      found = true;
    });
    // Sync a freshly-mounted rail to the scope the explorer is ALREADY in. The explorer block runs earlier in this
    // IIFE, so its first `specscribe:explorer-select` has already fired by the time we get here — arriving on
    // `#sb=epic-20` would otherwise leave the rail on the project card while the chart shows one epic. The scope is
    // read from the attribute Story 20.2 publishes, so this is not a second source of truth.
    if (!found) return;
    var drilled = document.querySelector("[data-explorer][data-sb-scope]");
    applyRelatedSelection(drilled ? drilled.getAttribute("data-sb-scope") : null, null);
  }

  function applyRelatedSelection(nodeId, label) {
    Array.prototype.forEach.call(document.querySelectorAll("[data-related-pane]"), function (pane) {
      try { revealRelatedCard(pane, nodeId, label); } catch (err) { /* degrade: the full rail stands */ }
    });
  }

  function revealRelatedCard(pane, nodeId, label) {
    var cards = pane.querySelectorAll(".related-card[data-related-node]");
    if (!cards.length) return;
    var empty = pane.querySelector("[data-related-empty]");
    var live = pane.querySelector(".related-work-live");
    var selecting = nodeId !== null && nodeId !== undefined && nodeId !== "";
    // Card the currently-focused element sits in, BEFORE the toggle loop below changes what is showing — used to
    // redirect focus if that card is about to stop being the current/visible one. [Story 20.3 review]
    var activeCard = closestCard(document.activeElement, cards);

    var match = null;
    Array.prototype.forEach.call(cards, function (c) {
      var hit = selecting && c.getAttribute("data-related-node") === nodeId;
      if (hit) match = c;
      // `.is-related-current` is the CSS hook (display is CSS's job via [data-related-ready]); `hidden` on the
      // NON-current cards additionally drops their links from the a11y tree and tab order, so a card that isn't
      // showing can never leave a phantom tab stop (the class of defect the 20.2 review found on hidden <a>s).
      c.classList.toggle("is-related-current", hit);
      c.hidden = selecting && !hit;
    });

    // A card that just lost focus-ability (hidden, or simply no longer `.is-related-current` under
    // [data-related-ready]) must not silently drop focus to <body> — move it to the newly-current card, or the
    // pane heading when there is none (deselecting back to the project default). [Story 20.3 review]
    if (activeCard && activeCard !== match) {
      var target = match || pane.querySelector("#related-work-h");
      if (target) {
        if (!target.hasAttribute("tabindex")) target.setAttribute("tabindex", "-1");
        target.focus();
      }
    }

    if (!selecting) {
      // No selection → the project card. Clearing the attribute lets the CSS show the project card and hide the
      // scope cards; nothing was force-shown, so nothing has to be restored. No announcement here: the explorer's
      // own live region already says "Showing all epics" for this same activation — a second message would be
      // redundant. [Story 20.3 review]
      pane.removeAttribute("data-related-scope");
      if (empty) empty.hidden = true;
      return;
    }

    pane.setAttribute("data-related-scope", nodeId);
    // A selection with no card has no work-graph relationships — the DESIGNED empty state, never a blank rail.
    if (empty) empty.hidden = !!match;
    // Announce ONLY the empty-state case: it's information the explorer's own "Zoomed into X" announcement never
    // carries. A match needs no second announcement — the explorer already said which node is selected.
    // [Story 20.3 review]
    if (!match) {
      var name = label || nodeId;
      say(live, "No related work items for " + name + ".");
    }
  }

  // The nearest ancestor card (or null) containing el, if any.
  function closestCard(el, cards) {
    if (!el) return null;
    for (var i = 0; i < cards.length; i++) { if (cards[i].contains(el)) return cards[i]; }
    return null;
  }
  function say(live, msg) { if (live) live.textContent = msg; }

  initRelatedPanes(document);
  document.addEventListener("specscribe:content-swapped", function (e) {
    initRelatedPanes(e && e.detail ? e.detail.root : document);
  });
})();
