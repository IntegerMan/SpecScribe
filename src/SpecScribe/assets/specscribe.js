/* SpecScribe progressive-enhancement script — the ONE sanctioned client-side addition (Story 1.5 Task 3).
   Two jobs, both dependency-free and static-host-safe:
     1. On-brand tooltips for SVG chart segments + heatmap cells, reading their existing <title> text so the
        native tooltip and aria-label stay as the no-JS / screen-reader fallback.
     2. Copy buttons on the "Next Steps" commands.
   Everything degrades gracefully: with JS off, <title> tooltips and the visible <code> command remain. */
(function () {
  "use strict";

  // ---- The ONE navigation seam every chart activation goes through --------------------------------
  // Charts navigate PROGRAMMATICALLY (a Plotly sector is not an <a>), and a host that intercepts anchor
  // clicks therefore cannot see them. The VS Code webview is exactly such a host: its bridge listens for
  // `a[href]` clicks and posts `{type:'navigate'}`, so a bare `location.href = …` slipped straight past it
  // and attempted a top-level navigation inside the panel — at best inert, at worst replacing the document
  // (and with it the bridge, the inlined stylesheet and the chart engine) with no way back but reopening.
  //
  // A host may install `window.__specscribeNavigate` to claim these. Absent one — the static site and the
  // SPA, both real browsers — the default is the original assignment, so their behaviour is byte-for-byte
  // unchanged. This is a SEAM rather than a webview branch on purpose: ADR 0036 §2 forbids forking the
  // mount logic, and one shared hook is what keeps hierarchy and graph activation on the same code path.
  // [ADR 0036 §2; Blind Hunter finding 1]
  function navigateTo(href) {
    if (!href) return;
    var hook = window.__specscribeNavigate;
    if (typeof hook === "function") { hook(href); return; }
    location.href = href;
  }

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
  // `.ss-relgraph-node` / `.ss-relgraph-edge` join the same family for Story 24.2's ego coupling graph, for the same
  // reason and via the same `data-tip-html` path. Routing through the BODY-LEVEL `.ss-tooltip` node also avoids the
  // CSS `::after` clipping trap: the graph lives inside a chart panel with its own overflow, and a pseudo-element
  // tooltip would be cut off by it.
  var SEG = ".sb-seg, .heatmap-cell, .donut-seg, .ss-hierarchy-sector, .ss-relgraph-node, .ss-relgraph-edge";
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

  // ---- Code Map file table: client-side pagination -> RETIRED -----------------------------
  // `initCodemapTablePager` (Story 7.12 review; Story 20.10 Task 4.5) was deleted here together with the
  // `.codemap-table-pager` markup and stylesheet family. Owner feedback 2026-08-01 turned "All files" into a
  // DIRECTORY TREE of native <details> disclosures, which answers the complaint the pager was added for
  // ('hundreds/thousands of rows with no way to page through it') structurally rather than arbitrarily — and
  // does it with JavaScript off, which the pager could not.
  //
  // The two could not honestly coexist: a pager over a partially-expanded tree reports a page count that changes
  // on every disclosure click, and this function already had to reconcile TWO hiding mechanisms (its own `hidden`
  // attribute and the pure-CSS `#cm-exclude-*:checked` filter) — <details> would have been a third.
  //
  // `initRiskGridPager` above is UNAFFECTED: separate class family, separate surface, still a flat grid.

  // ---- Source-code treemap + code-ownership sunburst -> RETIRED by Story 20.9 --------------
  // `initCodeMapPanel` (Story 7.6/7.12) and `initOwnershipSunburst` (Story 7.11, ADR 0010) were deleted here,
  // together with the four `Charts` entry points and all remaining hand-rolled arc geometry they enhanced. Both
  // surfaces now render through the Hierarchy Explorer component below — `ProjectCodeMap` and `ProjectOwnership`
  // payloads, with their eleven colorize dimensions expressed through the component's generic DIMENSION CONTRACT
  // rather than two bespoke recolour loops that each knew their own page.
  //
  // What moved rather than disappeared, because it was the careful part: the `Charts.Bucket` mirror and its
  // deliberate degenerate-range rule; the [min,max] window scaling that keeps absolute day-numbers from
  // collapsing to one level; share's fixed 25/50/75 cut points and the spotlight's 30/90/180-day ones; and the
  // per-node accessible-name wording, whose honesty was hard-won — the bucket LEVEL rather than a raw value the
  // colour does not represent, "not among this file's most-active tracked contributors" rather than the stronger
  // and sometimes-false "has not worked on this file", and an explicit "(date unknown)" rather than a coercion
  // into the oldest bucket. All of it now lives in the dimension declarations the emitter writes.
  //
  // Story 20.9 recorded here that `initCodemapTablePager` was KEPT, paginating the Code Map's file table — the
  // twin Story 20.6 D1 audited. That table is now a directory tree of native <details> and the pager is retired
  // (see the block above). The twin decision is UNCHANGED: the listing is still this surface's text equivalent and
  // still `HierarchyTwinDisplay.External`; only its shape moved. [Story 20.9 Task 4.3; owner feedback 2026-08-01]

  // ---- Planning <-> Code Impact Map ---------------------------------------------------------
  // Story 21.3's hand-rolled squarified treemap and arc renderer (`initImpactMap` / `renderTreemap` /
  // `renderSunburst` / `arcPath`) were DELETED by Story 20.7. The Impact Map now renders through the Hierarchy
  // Explorer component below, from a `ProjectImpactMap` payload, with its epic multi-select driving the
  // component's generic root-subtree filter. `arcPath` was the last of the three independent arc renderers ADR
  // 0010 §6 was supposed to have prevented. [Story 20.7 Task 8.3]

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
  // Progressive enhancement, and after Story 20.7 the thing it degrades TO has changed. There is no retained
  // server SVG on any converted surface any more, and no takeover handshake with Story 20.2's drill-in, because
  // neither exists. A missing bundle, a CSP block, or a throw anywhere below leaves the reader with the
  // server-rendered TEXT TWIN — complete, navigable, non-colour, and requiring no script to read (ADR 0013 §2).
  // The mount markers that remain are about the BOOT PLACEHOLDER only: they tell the inline chrome script whether
  // to keep showing "Initializing..." or hand the page back.
  var hierarchyMounts = [];

  function initHierarchyExplorers(scope) {
    var host = scope && scope.querySelectorAll ? scope : document;
    // Purge instances whose host left the document (the SPA swaps the content region via innerHTML, which detaches
    // the graph div while `responsive: true` keeps a window listener alive — a naive re-init leaks one per swap).
    for (var i = hierarchyMounts.length - 1; i >= 0; i--) {
      if (!document.contains(hierarchyMounts[i])) {
        try { if (window.Plotly && Plotly.purge) Plotly.purge(hierarchyMounts[i]); } catch (e) { /* already gone */ }
        // Plotly.purge releases Plotly's OWN listener; the probe host and this instance's window listeners are ours
        // to release, and they retain the whole node payload until we do. [Story 20.5 review]
        var cleanup = hierarchyMounts[i].__ssHierarchyCleanup;
        if (typeof cleanup === "function") { try { cleanup(); } catch (e) { /* best effort */ } }
        hierarchyMounts.splice(i, 1);
      }
    }
    Array.prototype.forEach.call(host.querySelectorAll("[data-hierarchy]"), function (root) {
      if (root.getAttribute("data-hierarchy-ready")) return;
      // --- The reveal hook. Plotly CANNOT lay out in a zero-width container, and it does not complain: it draws a
      // chart with no sectors that looks fine until someone reveals the panel. The component ships
      // `responsive: true` and sets only the HEIGHT — width comes from the container — and `responsive`'s
      // window-resize listener does NOT fire on a CSS-only reveal, so an eager mount inside a `display:none`
      // ancestor is permanently broken.
      //
      // Generic by construction: the condition is MEASURED, never declared, so no instance has to know it might be
      // hidden and no surface name reaches this file. Deferring the first mount is also the cheaper default — a
      // chart never drawn is work never done. [Story 20.9 F1]
      //
      // Measured on the PANEL, not on the host. The host's own `.ss-hierarchy` rule is `display: none` until this
      // block reveals it, so `root.clientWidth` is zero for EVERY instance at this point — testing it deferred the
      // dashboard along with the three hidden Code Map panels, i.e. every chart on the site. The panel is the
      // nearest ancestor that is laid out before any script runs, so its width is the honest answer to "is this
      // subtree rendered at all". Caught live rather than by the suite, which is what F1 said to expect.
      if (!hierarchyPanelOf(root).clientWidth) { deferHierarchyMount(root); return; }
      try {
        if (initHierarchyExplorer(root)) {
          root.setAttribute("data-hierarchy-ready", "1");
          hierarchyMounts.push(root);
        } else {
          // Declined rather than threw (no engine, no island) — same outcome for the reader, so release the
          // placeholder immediately and let the already-rendered text twin be the page.
          //
          // `|| root.parentNode` matters: `data-explorer` is an OPT-IN hook the dashboard call site happens to pass
          // via `panelAttributes`, which defaults to "". A Story 20.7 call site that omits it got `closest(...)` ===
          // null here, so nothing recorded the failure at all and the boot placeholder sat there until the inline
          // script's 5 s expiry. The mount path already resolves its panel with exactly this fallback. [20.5 review]
          var declined = root.closest("[data-explorer]") || root.parentNode;
          if (declined && declined.setAttribute) declined.setAttribute("data-hierarchy-failed", "1");
        }
      } catch (err) {
        // Degrade to the untouched server chart, and do it NOW rather than leaving the visitor watching a
        // placeholder until the inline script's timer expires. Per root, so one bad instance cannot down the page.
        //
        // A throw can land here AFTER Plotly.newPlot has already succeeded — `data-hierarchy-ready` is set before
        // plotting, and `root.on`, the controls loop and applyState all run after it. Marking the panel failed and
        // stopping there left the reader with BOTH charts (the CSS re-shows svg.sunburst while the Plotly chart is
        // still mounted), the instance absent from `hierarchyMounts` so it was never purged on a later swap, and
        // `data-hierarchy-ready` still set so re-init skipped this root forever. Unwind properly instead.
        // [Story 20.5 review]
        try { if (window.Plotly && Plotly.purge) Plotly.purge(root); } catch (e) { /* nothing plotted */ }
        root.removeAttribute("data-hierarchy-ready");
        root.style.height = "";
        var cleanup = root.__ssHierarchyCleanup;
        if (typeof cleanup === "function") { try { cleanup(); } catch (e) { /* best effort */ } }
        var failed = root.closest("[data-explorer]") || root.parentNode;
        if (failed && failed.setAttribute) {
          failed.removeAttribute("data-hierarchy-mounted");
          failed.setAttribute("data-hierarchy-failed", "1");
        }
      }
    });
    flushHierarchyReveals();
  }

  /* --- Deferred mounts: hosts that were zero-width when we first reached them [Story 20.9 F1] ---------------
     Two things happen on a reveal, and only one of them is a mount: a host that has never been plotted gets its
     FIRST mount, and a host that was plotted while visible and has since been resized gets `Plotly.Plots.resize`,
     which is the documented way to re-lay-out a plot whose container changed size without a window event.

     The trigger is a single delegated `change` listener on `[data-hierarchy-reveal]` controls, registered once.
     Deliberately not one listener per pending host: the SPA replaces the content region wholesale, and a
     per-host listener would retain a detached node on every swap. */
  var hierarchyPending = [];
  // The nearest laid-out ancestor. `|| root.parentNode` matters: `data-explorer` is an opt-in hook a call site may
  // omit, and the mount path resolves its panel with exactly this fallback.
  function hierarchyPanelOf(root) {
    return (root.closest && root.closest("[data-explorer]")) || root.parentNode || root;
  }
  function deferHierarchyMount(root) {
    if (hierarchyPending.indexOf(root) === -1) hierarchyPending.push(root);
  }
  function flushHierarchyReveals() {
    var still = [];
    for (var i = 0; i < hierarchyPending.length; i++) {
      var root = hierarchyPending[i];
      // Dropped by an SPA swap — forget it rather than retaining a detached host forever.
      if (!document.contains(root)) continue;
      if (root.getAttribute("data-hierarchy-ready")) continue;
      if (!hierarchyPanelOf(root).clientWidth) { still.push(root); continue; }
      try {
        if (initHierarchyExplorer(root)) {
          root.setAttribute("data-hierarchy-ready", "1");
          hierarchyMounts.push(root);
        }
      } catch (err) { /* one bad instance must not down the others; the text twin stands */ }
    }
    hierarchyPending = still;

    // Already mounted, newly re-sized. `responsive: true` fits the width on a WINDOW resize only, so a CSS-only
    // reveal leaves the plot at whatever width it had when it was drawn.
    for (var j = 0; j < hierarchyMounts.length; j++) {
      var m = hierarchyMounts[j];
      if (!document.contains(m) || !m.clientWidth) continue;
      try { if (window.Plotly && Plotly.Plots) Plotly.Plots.resize(m); } catch (e) { /* purged */ }
    }
  }
  document.addEventListener("change", function (e) {
    var t = e && e.target;
    if (t && t.getAttribute && t.getAttribute("data-hierarchy-reveal") !== null) flushHierarchyReveals();
  });

  // --- Reveal-by-hash [Story 20.9] -> RETIRED by Story 20.10 F5 --------------------------------------------
  // `revealPanelsNamedByHash()` and `data-hierarchy-reveal-when` existed because Code Map's four filter panels
  // were each `display:none` except the default one, so a deep link naming a scope inside a NON-default panel
  // needed to check the right boxes before that panel's own `initHierarchyExplorer` could ever run. Story 20.10
  // collapsed those four panels into ONE always-visible instance (D2) with a client-side view switch instead, so
  // the hash-driven CHECKBOX-CHECKING this function did is now handled by `initHierarchyExplorer`'s own
  // `viewKeyFromHash()` + view-toggle wiring, scoped to the one instance that exists. `data-hierarchy-reveal` and
  // the zero-width deferred-mount guard (`deferHierarchyMount`/`flushHierarchyReveals`, below) are a DIFFERENT,
  // still-live capability — the component's general answer to "I may be mounted inside a hidden container" — and
  // are unaffected.

  // The two runtime-argument kinds a dimension may take (HierarchyDimensionArg). Named once so the marker
  // attribute and the emitted declaration cannot drift on a typo.
  var HIERARCHY_ARG_ROSTER = "roster";
  var HIERARCHY_ARG_THRESHOLD = "threshold";

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
    // Story 20.10: an optional set of server-declared VIEWS over this same shared NODES bag (Code Map's four
    // filter combinations are the first consumer). Null on every other surface, so nothing below changes for them.
    var VIEWS = (payload && payload.views && payload.views.length) ? payload.views : null;

    var panel = root.closest("[data-explorer]") || root.parentNode;
    var live = panel.querySelector(".ss-hierarchy-live");
    var drillBar = panel.querySelector(".ss-hierarchy-drill");
    var crumbList = panel.querySelector(".ss-hierarchy-breadcrumb");
    var controls = panel.querySelector(".ss-hierarchy-controls");
    var selectMode = cfg.mode === "select";

    /* --- Views (Story 20.10) --------------------------------------------------------------------------------
       A view names its own directory SCAFFOLD (never shared across views — F2: a single-child directory chain's
       collapse depends on which files survived the filter, so the same directory can carry a different id, label
       AND parent per view) plus which of the shared NODES it contains and where each hangs in THIS view
       (`files`/`parent`, parallel integer-indexed arrays — Task 1.4). `activeView()` and `activeViewRawNodes()`
       are the ONLY place that reads them; everything else below keeps working over whatever `reindex()` last
       built, exactly as it did before views existed. */
    var viewIndex = 0;
    function activeView() { return VIEWS ? (VIEWS[viewIndex] || VIEWS[0]) : null; }
    function activeViewKey() { var v = activeView(); return v ? v.key : null; }
    function activeViewRawNodes() {
      var v = activeView();
      if (!v) return NODES;
      var out = v.scaffold.slice();
      for (var i = 0; i < v.files.length; i++) {
        var n = NODES[v.files[i]];
        if (!n) continue;
        var parentNode = v.scaffold[v.parent[i]];
        // [Review][Patch] Skip, exactly as the `!n` arm above does. A missing parent used to fall through to
        // `parentId: null`, which does not degrade — it mints a SECOND parentless node, and a treemap/sunburst
        // under `branchvalues: "total"` rejects two roots outright and draws nothing but a console message. It also
        // dropped that file's lines from every ancestor total in `rollUpChildrenWin`. Only reachable from a
        // truncated or hand-edited island (the emitter keeps `files`/`parent` parallel and in range), but the whole
        // point of the integer encoding replacing self-describing path strings is that an index can be wrong.
        if (!parentNode) continue;
        var copy = {};
        for (var k in n) { if (Object.prototype.hasOwnProperty.call(n, k)) copy[k] = n[k]; }
        copy.parentId = parentNode.id;
        out.push(copy);
      }
      return out;
    }

    // Prototype-less maps: node ids come from author-controlled markdown (`### Story N.M:` headings, which nothing
    // dedupes), so an id of "constructor" or "__proto__" would otherwise resolve to an inherited Object member and
    // blow up every lookup below — reachable from a crafted hash. Same hardening the Story 20.2 block carries.
    // Story 20.10: these are now REASSIGNED (not just built once) by `reindex()` on every view switch — every
    // function below closes over the SAME `var` bindings, so a reassignment is visible everywhere without having
    // to thread a view parameter through the whole file.
    var byId, childrenOf, indexOf, depthOf, ROOT_ID, currentRawNodes;
    function reindex(list) {
      currentRawNodes = list;
      byId = Object.create(null); childrenOf = Object.create(null); indexOf = Object.create(null); depthOf = Object.create(null);
      list.forEach(function (n, i) {
        if (byId[n.id] === undefined) { byId[n.id] = n; indexOf[n.id] = i; }
        if (n.parentId) { (childrenOf[n.parentId] = childrenOf[n.parentId] || []).push(n); }
      });
      ROOT_ID = list[0] && !list[0].parentId ? list[0].id : null;
    }
    reindex(VIEWS ? activeViewRawNodes() : NODES);
    function depth(id) {
      if (depthOf[id] !== undefined) return depthOf[id];
      var d = 0, cur = byId[id], guard = 0;
      while (cur && cur.parentId && byId[cur.parentId] && guard++ < 64) { d++; cur = byId[cur.parentId]; }
      depthOf[id] = d;
      return d;
    }
    function hasChildren(id) { return !!(childrenOf[id] && childrenOf[id].length); }

    /* --- Tokens: resolved from the SHIPPED cascade, never re-typed ------------------------------------------
       NOTHING about a colour family is written here any more. The server emits each node's resolvable CLASS LIST
       (`colorClass`) and this applies it verbatim to a probe element, reading fill/stroke back out of
       specscribe.css. A hard-coded hex would survive a token change and quietly lie about it (AD-7).

       Story 20.7 removed the `STATUS_CLASS` map that used to live here. It was a second copy of the status
       vocabulary the client had to keep in step by hand, and — decisively — it was the reason this component
       could only ever speak ONE colour family: `"sb-seg " + STATUS_CLASS[cls]` cannot express the Impact Map's
       `impact-tm-tile impact-level-3`. Story 20.9's eleven colorize dimensions land on this same seam. */

    // UX-DR17: the shipped SVG distinguishes follow-up and no-plan wedges by a DASHED STROKE as well as fill.
    // Plotly's marker.line has no `dash`, so per-sector hatching replaces that channel — a stronger one, and the
    // reason no state here is signalled by colour alone. Keyed by CLASS TOKEN and matched against the node's whole
    // class list, so a second family can bring its own non-colour channel without this becoming a status map again.
    // Story 20.9 adds the three states whose non-colour channel in the shipped CSS is `stroke-dasharray: 2 1` —
    // `.codemap-cell.type-other`, `.ownership-wedge.owner-author-other` and `.ownership-wedge.owner-stale`. A dash
    // is exactly what `marker.line` cannot express, which is the limit Story 20.5 already hit; hatching is the
    // channel that survives the engine swap. Keyed by class token, so a dimension declares its non-colour channel
    // the same way it declares its fill.
    var PATTERN_SHAPE = {
      "sb-followup-open": "/", "sb-followup-done": "\\", "sb-noplan": ".", "sb-unplanned": "x",
      "type-other": ".", "owner-author-other": ".", "owner-stale": "x"
    };

    var probeHost = document.createElement("div");
    probeHost.setAttribute("aria-hidden", "true");
    probeHost.style.cssText = "position:absolute;left:-9999px;width:0;height:0;overflow:hidden";
    // Appended under `panel` (a real descendant of the page's `.ir-content` wrapper), NOT `document.body`.
    // `web/assets/ir-content.css` nests every rule under `.ir-content` (ADR 0029); a probe hung directly off
    // `document.body` sits OUTSIDE that ancestor and no scoped selector — `.ir-content .sb-done`, etc. — can
    // ever match it, so every resolved fill/stroke silently falls back to the SVG default (black). [incident:
    // sunburst rendered all-black even after the CSS rules themselves were restored]
    panel.appendChild(probeHost);
    var tokenCache = Object.create(null);
    var DEFAULT_COLOR_CLASS = "sb-seg sb-unrecognized";
    function tokenFor(classList) {
      var cls = classList || DEFAULT_COLOR_CLASS;
      if (tokenCache[cls]) return tokenCache[cls];
      var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      // `ss-hierarchy-probe` lets the stylesheet give this component a chart fill where the SVG's own rule is
      // `fill: transparent` — today that is `.sb-noplan`, which is drawn as a dashed OUTLINE in the SVG and so had
      // no fill to resolve. Still the live cascade, still no token typed in this file. [Story 20.5 review]
      svg.setAttribute("class", "sunburst ss-hierarchy-probe");
      var path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      // VERBATIM — the server decided the whole class list, including the wedge/tile class. Composing anything
      // here would re-introduce family knowledge this file no longer has. [Story 20.7 Task 1.1]
      path.setAttribute("class", cls);
      svg.appendChild(path);
      probeHost.appendChild(svg);
      var cs = getComputedStyle(path);
      // fill-opacity is read and COMPOSITED into the colour rather than dropped. Several structural classes carry
      // it (`.impact-arc-dir` is fill-opacity 0.7), and a resolver that returned only `fill` would paint them at
      // full strength with no test able to see the difference — the family renders, just wrong. Story 20.9 depends
      // on this for five more states (`.codemap-cell.level-0` 0.35, `.level-none`/`.type-other` 0.55,
      // `.owner-author-other` 0.55, `.owner-spotlight-off` 0.35).
      //
      // `stroke-width` joins them for the same reason. It is a real, SECOND channel on at least one state —
      // `.spotlight-touched` layers `stroke: var(--ink); stroke-width: 1.2` on top of a level ramp — and a
      // resolver that returned one global edge colour could not express it. Every already-shipped family declares
      // one uniform stroke (`.sb-seg` is warm-white at 1), so resolving per sector reproduces exactly what those
      // charts already draw.
      tokenCache[cls] = { fill: withOpacity(cs.fill, cs.fillOpacity), stroke: cs.stroke, width: parseFloat(cs.strokeWidth) };
      return tokenCache[cls];
    }
    // rgb(a) + a separate fill-opacity -> one rgba Plotly can use. Anything unparseable is passed through
    // untouched rather than guessed at.
    function withOpacity(color, opacity) {
      var a = parseFloat(opacity);
      if (!color || !isFinite(a) || a >= 1) return color;
      var m = /^rgba?\(([^)]+)\)/.exec(color);
      if (!m) return color;
      var parts = m[1].split(",");
      if (parts.length < 3) return color;
      var existing = parts.length > 3 ? parseFloat(parts[3]) : 1;
      if (!isFinite(existing)) existing = 1;
      return "rgba(" + parts[0].trim() + "," + parts[1].trim() + "," + parts[2].trim() + "," + (existing * a) + ")";
    }
    /* --- The dimension contract (config-gated) [Story 20.9 AC#1] ---------------------------------------------
       A surface may offer several colorize dimensions, and switching one re-colours IN PLACE: no geometry is
       re-derived, nothing is re-counted, no fetch is issued. Every rule below reads only values the emitter
       embedded at generation time (ADR 0012 §7 / ADR 0010 §3) — including `asof`, which is the tree's most-recent
       commit day and never wall-clock `now` (FR31).

       Nothing here names a surface or a colour. A dimension DECLARES which metric it reads, which class prefix it
       paints with and how its accessible name reads; this resolves that declaration and hands the class list to
       the same probe every other family goes through (AD-7). The two dimensions that cannot be precomputed —
       a spotlight on an arbitrary contributor, a free 1–60 month staleness threshold — are exactly why the payload
       carries raw values rather than a frozen class per node (owner decision D1). */
    var DIMS = (cfg.dimensions && cfg.dimensions.length) ? cfg.dimensions : null;
    var CONSTANTS = cfg.constants || {};
    // Reserved constant: the payload's reference day. Part of the contract rather than a surface's private key,
    // because "how long ago" is only answerable against a fixed, embedded day.
    var AS_OF = CONSTANTS.asof === undefined ? NaN : parseFloat(CONSTANTS.asof);
    var dimState = { key: DIMS ? DIMS[0].key : null, roster: null, threshold: null };
    var dimClassOf = Object.create(null);
    var dimTextOf = Object.create(null);

    function metricOf(n, key) {
      var m = n && n.metrics;
      if (!m || !key) return null;
      var v = m[key];
      return (v === undefined || v === null || v === "") ? null : v;
    }
    function numOf(n, key) {
      var raw = metricOf(n, key);
      if (raw === null) return null;
      var v = parseFloat(raw);
      return isNaN(v) ? null : v;
    }
    // Mirrors Charts.Bucket's <=0.25/0.5/0.75 cut points exactly, INCLUDING its one deliberate difference: a
    // degenerate single-point range (max <= 0 but a positive value) reads as the TOP bucket rather than falling
    // through to "no activity", because the one file that does have data must not render identically to files
    // with none. Carried over verbatim from the renderer this replaces.
    function bucket(value, max) {
      if (max <= 0) return value > 0 ? 4 : 0;
      if (value <= 0) return 0;
      var r = value / max;
      return r <= 0.25 ? 1 : r <= 0.5 ? 2 : r <= 0.75 ? 3 : 4;
    }
    // The bucket LEVEL is exactly what the colour encodes, so it is the honest text equivalent — never the raw
    // day-number or count the colour does not literally represent.
    function levelWord(l) { return l === 0 ? "lowest" : l === 4 ? "highest" : "level " + l + " of 4"; }
    function tmpl(text, vars) {
      return String(text == null ? "" : text).replace(/\{(\w+)\}/g, function (whole, k) {
        return vars[k] === undefined || vars[k] === null ? whole : String(vars[k]);
      });
    }
    var constantCache = Object.create(null);
    function constantList(name) {
      if (!name) return [];
      if (constantCache[name]) return constantCache[name];
      var out = [];
      try { var p = JSON.parse(CONSTANTS[name] || "[]"); if (Array.isArray(p)) out = p; } catch (e) { out = []; }
      constantCache[name] = out;
      return out;
    }
    function tupleList(n, key) {
      var raw = metricOf(n, key);
      if (!raw) return [];
      try { var p = JSON.parse(raw); return Array.isArray(p) ? p : []; } catch (e) { return []; }
    }
    function dimValue(n, d) {
      var v = numOf(n, d.metric);
      if (v === null) return null;
      // The shipped `!ch` guard: a file with zero changes has no average, and "no data" is honest where dividing
      // by zero is not.
      if (d.divisor) { var by = numOf(n, d.divisor); if (!by) return null; v = v / by; }
      return v;
    }
    function activeDim() {
      if (!DIMS) return null;
      for (var i = 0; i < DIMS.length; i++) { if (DIMS[i].key === dimState.key) return DIMS[i]; }
      return DIMS[0];
    }
    /// The alphabetical UNION of every node's own bounded contributor list — never the panel-wide top-N palette,
    /// and never sorted by volume. FR-10 / ADR 0010 §4: attribution, never a ranking.
    // [Review][Patch] Scans the ACTIVE VIEW, not the whole shared bag — the third scan in this family, and the one
    // F3's fix missed. The two below it were re-scoped to `currentRawNodes` precisely because a whole-payload scan
    // silently becomes the rejected "one scale across all views" design; a roster built from the union across all
    // four views would offer contributor entries whose files the active view excluded. Not reachable today (Code Map
    // declares no roster dimension) — fixed because the first views surface that declares one would inherit it.
    function dimRoster(d) {
      var seenNames = Object.create(null), out = [];
      currentRawNodes.forEach(function (n) {
        tupleList(n, d.metric).forEach(function (entry) {
          if (entry && typeof entry[0] === "string" && !seenNames[entry[0]]) { seenNames[entry[0]] = true; out.push(entry[0]); }
        });
      });
      return out.sort(function (a, b) { return a.localeCompare(b); });
    }

    function classifyNode(n, d, scale) {
      var T = d.text || {};
      var vars = { label: d.label };
      var extra = d.extraClass ? " " + d.extraClass : "";

      if (d.kind === "categorical") {
        var key = metricOf(n, d.metric);
        vars.value = metricOf(n, d.labelMetric) || key || "";
        return { cls: key ? d.classPrefix + key : d.noneClass, text: tmpl(T.value, vars) };
      }
      if (d.kind === "ramp" || d.kind === "ramp-window") {
        var v = dimValue(n, d);
        if (v === null) return { cls: d.noneClass, text: tmpl(T.none, vars) };
        var lvl = scale.window ? bucket(v - scale.min, scale.max - scale.min) : bucket(v, scale.max);
        vars.level = levelWord(lvl);
        return { cls: d.classPrefix + lvl, text: tmpl(T.value, vars) };
      }
      if (d.kind === "cutoff") {
        var raw = numOf(n, d.metric);
        if (raw === null) return { cls: d.noneClass, text: tmpl(T.none, vars) };
        var cuts = d.cutoffs || [];
        var band = cuts.length + 1;
        for (var i = 0; i < cuts.length; i++) { if (raw <= cuts[i]) { band = i + 1; break; } }
        vars.value = metricOf(n, d.metric);
        return { cls: d.classPrefix + band, text: tmpl(T.value, vars) };
      }
      if (d.kind === "roster") {
        var name = metricOf(n, d.metric);
        if (name === null) return { cls: d.noneClass, text: tmpl(T.none, vars) };
        var idx = constantList(d.rosterConstant).indexOf(name);
        vars.name = name;
        return { cls: idx >= 0 ? d.classPrefix + idx : d.classPrefix + "other", text: tmpl(T.value, vars) };
      }
      if (d.kind === "spotlight") {
        var who = dimState.roster;
        vars.name = who;
        var entry = null, list = tupleList(n, d.metric);
        for (var j = 0; j < list.length; j++) { if (list[j] && list[j][0] === who) { entry = list[j]; break; } }
        // Absence means "not among this file's own embedded (capped) contributor list", NOT a proven "never
        // touched" — a file with more contributors than the per-file cap can have a real, spotlighted contributor
        // who simply ranks below it here. The declared wording says so. [Review 2026-07-22, preserved]
        if (!entry) return { cls: d.offClass || d.noneClass, text: tmpl(T.off, vars) };
        var lastDay = entry[2];
        var daysAgo = (lastDay === null || lastDay === undefined || isNaN(AS_OF)) ? null : (AS_OF - lastDay);
        // Touched, but their own last-touch date was not embedded — an honest "unknown", never coerced into the
        // oldest bucket, which would fabricate a "long ago" claim the data does not support.
        if (daysAgo === null) return { cls: d.noneClass + extra, text: tmpl(T.unknown, vars) };
        // MORE recent is a HIGHER level, so this ramp runs the opposite way from `cutoff`'s.
        var rcuts = d.cutoffs || [];
        var level = 1;
        for (var k = 0; k < rcuts.length; k++) { if (daysAgo <= rcuts[k]) { level = rcuts.length + 1 - k; break; } }
        vars.days = daysAgo + (daysAgo === 1 ? " day" : " days");
        return { cls: d.classPrefix + level + extra, text: tmpl(T.hit, vars) };
      }
      if (d.kind === "threshold") {
        var last = numOf(n, d.metric);
        if (last === null || isNaN(AS_OF)) return { cls: d.noneClass, text: tmpl(T.none, vars) };
        var months = dimState.threshold;
        var monthsAgo = (AS_OF - last) / 30;
        var stale = monthsAgo >= months;
        vars.months = months;
        vars.monthsAgo = Math.round(monthsAgo);
        return { cls: d.classPrefix + (stale ? "stale" : "fresh"), text: tmpl(stale ? T.stale : T.fresh, vars) };
      }
      return null;
    }

    function resolveDimension() {
      dimClassOf = Object.create(null);
      dimTextOf = Object.create(null);
      var d = activeDim();
      if (!d) return;

      // The scan spans the WHOLE payload, not the drilled scope — the renderer this replaces scanned every cell in
      // the panel regardless of zoom, so a level means the same thing before and after a drill. Story 20.10 F3:
      // with views, "whole payload" means the ACTIVE VIEW's own nodes (`currentRawNodes`, kept in step by
      // `reindex()` on every view switch) — scanning the full shared NODES bag here would silently become the
      // rejected "one scale across all views" design and recolour views whose ramp should normalize on their own.
      var scale = null;
      if (d.kind === "ramp" || d.kind === "ramp-window") {
        var min = Infinity, max = 0;
        currentRawNodes.forEach(function (n) {
          if (!n.metrics) return;
          var v = dimValue(n, d);
          if (v === null) return;
          if (v > max) max = v;
          if (v < min) min = v;
        });
        scale = { min: isFinite(min) ? min : 0, max: max, window: d.kind === "ramp-window" };
      }

      currentRawNodes.forEach(function (n) {
        // Structural nodes — directories and the synthesized root — carry no metric bag and never participate in
        // a dimension. The SVG never recoloured a directory rect either; a directory has no dominant author.
        if (!n.metrics) return;
        var out = classifyNode(n, d, scale);
        if (!out) return;
        dimClassOf[n.id] = (n.colorClass ? n.colorClass + " " : "") + out.cls;
        dimTextOf[n.id] = out.text;
      });
    }

    // The one place fill, hatch and stroke all read a node's class list from, so a dimension switch cannot move
    // one channel and leave another behind.
    function classOf(n) {
      if (!n) return DEFAULT_COLOR_CLASS;
      return dimClassOf[n.id] || n.colorClass || DEFAULT_COLOR_CLASS;
    }

    function fillFor(n) {
      var t = tokenFor(classOf(n));
      var f = t.fill;
      // Last-resort fallback for a class whose shipped rule paints no fill at all. It is deliberately NOT how
      // no-plan is resolved any more: falling back to the STROKE token gave `.sb-noplan` the value of
      // `--status-pending`, so a no-plan sector came out byte-identical to a Pending one while the legend showed a
      // pale hatched chip — the correspondence was simply broken. `.ss-hierarchy-probe .sb-noplan` in the
      // stylesheet now gives it a real chart fill, so this branch is reached only by a class nobody has styled.
      // [Story 20.5 review]
      if (!f || f === "none" || f === "transparent" || f === "rgba(0, 0, 0, 0)") return t.stroke;
      return f;
    }
    // The hatch channel resolves from the SAME class list as the fill, so a family declares both together — and
    // so a dimension switch moves both at once.
    function patternFor(n) {
      var cls = classOf(n);
      if (!cls) return "";
      var tokens = cls.split(/\s+/);
      for (var i = 0; i < tokens.length; i++) {
        if (PATTERN_SHAPE[tokens[i]]) return PATTERN_SHAPE[tokens[i]];
      }
      return "";
    }
    // Per-sector stroke, resolved from the same class list. `.spotlight-touched` is the state that needs it: it
    // layers a darker, wider stroke on top of a level ramp, which is a second channel on the same node and the
    // only thing keeping that dimension from being colour-only.
    function strokeFor(n) {
      var s = tokenFor(classOf(n)).stroke;
      return (!s || s === "none") ? edgeColor : s;
    }
    function strokeWidthFor(n) {
      var w = tokenFor(classOf(n)).width;
      return isFinite(w) && w > 0 ? w : 1;
    }
    var inkColor = tokenFor("sb-seg sb-unrecognized").fill;
    var edgeColor = tokenFor("sb-seg sb-done").stroke || inkColor;

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
    function textOn(n) {
      return luminance(fillFor(n)) < 0.55 ? onDarkColor : onLightColor;
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
      // A surface that ships its own card wins. Story 20.5 made `.ss-tooltip` + `data-tip-html` the one tooltip
      // system site-wide precisely so swapping the drawing engine never swaps the tooltip's look — and the two
      // colorize surfaces' cards carry per-file git metrics the generic card has no field for. [Story 20.9 F8]
      if (n && n.tip) return n.tip;
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

    /* --- The node filter (config-gated) --------------------------------------------------------------------
       Generic by construction: it is given a set of ROOT-CHILD ids to keep and knows nothing else. No surface
       name appears here — an Impact-Map-shaped branch inside the shared component is exactly the drift this epic
       exists to end, and Story 20.9 is the second consumer.

       It re-projects an ALREADY-EMBEDDED payload; it never re-derives from live state (ADR 0012 §7 / ADR 0010 §3).
       The parent roll-up is re-run with the SAME rule the emitter uses — children win — because a filtered parent
       that kept an unfiltered total would draw a sector larger than the sum of what is inside it, and with
       `branchvalues: "total"` Plotly renders that wrong rather than complaining. [Story 20.7 Task 1.3] */
    var filterState = null;   // null = unfiltered; otherwise a map of kept root-child id -> true

    // Story 20.10 Task 2.1: the children-win roll-up, extracted so the EXISTING root-child filter below and the
    // NEW view switch both end on the SAME rule — a second implementation is exactly how two views (or a view and
    // a filter) would start disagreeing about a parent's value. `list` must already be parent-before-child.
    function rollUpChildrenWin(list) {
      var present = Object.create(null);
      for (var p = 0; p < list.length; p++) present[list[p].id] = true;
      var sum = Object.create(null), hasKids = Object.create(null);
      for (var i = list.length - 1; i >= 0; i--) {
        var n = list[i];
        var own = hasKids[n.id] ? sum[n.id] : n.value;
        if (n.parentId && present[n.parentId]) {
          sum[n.parentId] = (sum[n.parentId] || 0) + own;
          hasKids[n.parentId] = true;
        }
      }
      return list.map(function (n) {
        return hasKids[n.id] ? shallowWithValue(n, sum[n.id]) : n;
      });
    }

    function visibleNodes() {
      // Story 20.10: a view-bearing instance projects its OWN scaffold+files set — `currentRawNodes`, kept in
      // step by `reindex()` on every view switch (Task 2.1 — extending this seam, not minting a second one).
      //
      // [Review][Patch] The two mechanisms COMPOSE rather than short-circuiting. This used to `return` on `VIEWS`
      // before the `filterable` check, so an instance that was both view-bearing and filterable silently ignored
      // its own filter AND lost the `kept.length <= 1` empty guard below. Not reachable for Code Map (not
      // filterable), but the shared-payload contract is generic now (ADR 0012 Ratified decision #8) and a config
      // pair that renders one of two declared behaviours is exactly the drift this component exists to end.
      var base = VIEWS ? currentRawNodes : NODES;
      // A view's raw nodes carry no rolled-up parent values (the server ships directory values as 0 — the roll-up
      // is the client's job for a shared payload); the un-viewed path is already rolled up server-side.
      if (!cfg.filterable || !filterState) return VIEWS ? rollUpChildrenWin(base) : base;

      var keep = Object.create(null);
      // Keep the root itself, the selected root children, and every descendant of those.
      if (ROOT_ID) keep[ROOT_ID] = true;
      base.forEach(function (n) {
        if (n.parentId === ROOT_ID && filterState[n.id]) keep[n.id] = true;
      });
      // The list is parent-before-child (both `NODES` as emitted and a view's scaffold-then-files order), so one
      // forward pass propagates. A node whose parent is absent from `keep` is simply dropped, which also drops
      // anything under it.
      base.forEach(function (n) {
        if (n.parentId && keep[n.parentId] && n.parentId !== ROOT_ID) keep[n.id] = true;
      });

      var kept = base.filter(function (n) { return keep[n.id]; });
      // Nothing selected leaves only the synthesized root. Draw NOTHING rather than a lone zero-value root, which
      // Plotly renders as an empty frame with a stale-looking centre label. The live region says what happened.
      if (kept.length <= 1) return [];

      return rollUpChildrenWin(kept);
    }
    function shallowWithValue(n, value) {
      var out = {};
      for (var k in n) { if (Object.prototype.hasOwnProperty.call(n, k)) out[k] = n[k]; }
      out.value = value;
      return out;
    }

    function buildTrace() {
      var VIS = visibleNodes();
      var t = {
        type: state.shape,
        ids: VIS.map(function (n) { return n.id; }),
        parents: VIS.map(function (n) { return n.parentId || ""; }),
        // The SHORT label is what gets drawn in a sector; the full one rides in customdata for the hover card.
        // uniformtext sizes every label alike and hides what will not fit, so one long title silences the chart.
        labels: VIS.map(function (n) { return n.shortLabel || n.label; }),
        customdata: VIS.map(function (n) { return n.label; }),
        // Every value is a NUMBER. A single null anywhere in `values` collapses calcdata to one point and renders
        // nothing — no error, no console warning. The emitter guarantees it; this never re-derives it.
        values: VIS.map(function (n) { return n.value; }),
        // Emitted by the server alongside the payload, because a payload/branchvalues mismatch draws a blank or
        // wrong chart with only a console warning. The two must be decided together, so they travel together.
        branchvalues: cfg.branchvalues || "total",
        marker: {
          colors: VIS.map(function (n) { return fillFor(n); }),
          // Per-sector, because this is ALSO the selection ring. CSS cannot draw it: setting `stroke` on one of
          // Plotly's `path.surface` nodes is inert (verified against ink geometry, and inert even from an inline
          // `!important`). `marker.line` is the channel that paints the separators, so it is the one that works.
          // Width AND colour both change, so the selection is never signalled by colour alone (UX-DR17).
          line: {
            // The ring takes the SAME per-sector contrast pick the labels use, not one fixed accent: a gold ring
            // on a gold "ready" sector is invisible, and the selection can land on any status. When nothing is
            // selected the sector's OWN resolved stroke is used, which is what carries `.spotlight-touched`.
            color: VIS.map(function (n) { return n.id === state.selected ? textOn(n) : strokeFor(n); }),
            width: VIS.map(function (n) { return n.id === state.selected ? 4 : strokeWidthFor(n); })
          },
          pattern: {
            shape: VIS.map(function (n) { return patternFor(n); }),
            fillmode: "overlay",
            // MUST be per-sector and explicit: left unset, Plotly paints the pattern's backing rect BLACK (67
            // occurrences measured), which is a default colour reaching the output.
            bgcolor: VIS.map(function (n) { return fillFor(n); }),
            // Full ink, DELIBERATELY — the hatch is softened by `stroke-opacity` in the stylesheet's
            // `.ss-hierarchy defs pattern > path` rule, not here. Passing an `rgba()` to `fgcolor` is the
            // obvious-looking way to do it and is silently discarded: Plotly writes only the RGB back onto the
            // pattern path's `stroke` attribute and drops the alpha. Verified in a live browser — the trace
            // held `rgba(92,101,112,0.45)` while the rendered path read `stroke="rgb(92,101,112)"` with
            // computed stroke-opacity 1. Do not "simplify" this back into an rgba here; it will do nothing.
            fgcolor: VIS.map(function () { return inkColor; }),
            // Softened on the owner's verify round 2026-08-06 ("harder to read with the hatched fills"). The
            // hatch STAYS — it is the non-colour status channel UX-DR17 and ADR 0012 §6 require, and it is what
            // replaced the dashed stroke `marker.line` cannot express. Only its weight drops: wider spacing and
            // thinner strokes here, lower opacity in the stylesheet.
            size: 9,
            solidity: 0.14
          }
        },
        // Status as TEXT, so nothing is signalled by colour alone even to a viewer who cannot distinguish fill or
        // hatch at all. Prose, never the CSS class.
        text: VIS.map(function (n) { return n.statusLabel; }),
        // Plotly's own hover card is switched OFF: the portal already has one tooltip and this component uses it
        // (see `.ss-hierarchy-sector` in SEG above), so a chart does not get a second look just because a different
        // engine drew it. [owner verify round: "we lost some of the pretty formatting we used on our tooltips"]
        hoverinfo: "none",
        // Draw order stays the emitter's order, which is the SVG's draw order.
        sort: false,
        // Both font slots plus layout.font below. With only `insidetextfont` set, the ROOT label alone took
        // Plotly's default rgb(68,68,68) — one element out of 119, exactly the kind of miss a config-level
        // assertion never catches.
        insidetextfont: { color: VIS.map(function (n) { return textOn(n); }), weight: 700 },
        outsidetextfont: { color: onLightColor, weight: 700 }
      };
      if (state.shape === "sunburst") {
        t.leaf = { opacity: 1 };
        t.textinfo = cfg.labels ? "label" : "none";
        t.insidetextorientation = "radial";
      } else {
        // `label` only — NEVER `label+value`. `value` is the layout number Plotly sizes sectors by, and the owner's
        // verify round called it "a confusing value ... not helpful or intuitive for the reader" and had it removed
        // from the tooltip, the accessible name and the text twin. `label+value` put it straight back, printed on
        // every tile — a strictly MORE prominent placement than the one it was removed from. [Story 20.5 review]
        t.textinfo = cfg.labels ? "label" : "none";
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
          //
          // The dimension suffix is appended to that BASE name, recomposed on every switch — never stacked onto a
          // previously-appended one, because the base is rebuilt from the payload each time. This is the clause
          // AC#1's "the non-colour channel holds across every dimension" actually refers to: a dimension whose
          // fill changes and whose accessible name does not is a UX-DR17 failure that ships green. [Story 20.9 F3]
          var name = n.label + (n.statusLabel ? " — " + n.statusLabel : "") + (n.detail ? ", " + n.detail : "");
          if (dimTextOf[n.id]) name += " — " + dimTextOf[n.id];
          el.setAttribute("aria-label", name);
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
      // The synthesized root at top level is already the whole view: `drillTo` normalizes its id to `null`, finds
      // that equal to the current level and returns early. Without this branch that path cleared `state.selected`
      // without redrawing (leaving a ring painted on a sector nothing had selected) and announced nothing at all,
      // so the first keyboard-reachable sector read as dead. [Story 20.5 review]
      if (id === ROOT_ID && !state.level) { announce("Already showing the whole project."); return; }
      if (hasChildren(id)) { drillTo(id, true); return; }
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
      if (n.href) navigateTo(n.href);
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
      // Clearing the selection is part of CHANGING SCOPE, not of drilling in specifically. Doing it only on the
      // drill-in branch of `activate` left Escape / a crumb click / a hashchange with the 4px ring still painted on
      // a sector while the rail had already fallen back to the project card — chart and rail disagreeing about what
      // is selected. One place, so every scope change agrees. [Story 20.5 review]
      state.selected = null;
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
      // [Review][Patch] An empty node set has no root to name, and the `"All epics"` fallback below is the work
      // graph's wording — on a views-bearing surface whose active view filtered everything out, this revealed a
      // breadcrumb reading "All epics" on the Code Map page. There is no scope to show, so show no scope bar; the
      // empty-state notice `paintViewChrome` reveals is what answers the reader here.
      if (!ROOT_ID || !byId[ROOT_ID]) {
        while (crumbList.firstChild) crumbList.removeChild(crumbList.firstChild);
        if (drillBar) drillBar.hidden = true;
        return;
      }
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
    // Story 20.10 Task 2.7: a second fragment key for the active VIEW, alongside the drilled scope — retiring the
    // four per-variant `#cm-{key}=` HashKeys Story 20.9 shipped (never a documented stable scheme) in favour of
    // ONE HashKey plus this sibling, so a shared link lands on the right filter AND the right scope together.
    var VIEW_HASH_KEY = VIEWS ? (cfg.hashKey || "sb") + "-view=" : null;
    var spaHost = document.getElementById("spa-content");
    function hashWith(id) {
      var raw = location.hash.replace(/^#/, "");
      var parts = raw ? raw.split("&") : [];
      var kept = [];
      for (var i = 0; i < parts.length; i++) {
        if (parts[i].indexOf(HASH_KEY) !== 0 && (!VIEW_HASH_KEY || parts[i].indexOf(VIEW_HASH_KEY) !== 0) && parts[i]) kept.push(parts[i]);
      }
      if (VIEW_HASH_KEY) {
        var vk = activeViewKey();
        if (vk) kept.unshift(VIEW_HASH_KEY + encodeURIComponent(vk));
      }
      if (id) kept.unshift(HASH_KEY + encodeURIComponent(id));
      return kept.length ? "#" + kept.join("&") : location.pathname + location.search;
    }
    function viewKeyFromHash() {
      if (!VIEW_HASH_KEY) return null;
      var raw = location.hash.replace(/^#/, "");
      var parts = raw ? raw.split("&") : [];
      for (var i = 0; i < parts.length; i++) {
        if (parts[i].indexOf(VIEW_HASH_KEY) === 0) {
          try { return decodeURIComponent(parts[i].slice(VIEW_HASH_KEY.length)); } catch (e) { return null; }
        }
      }
      return null;
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
    // The shared tooltip hides on a DELEGATED `mouseout` (see the tooltip block at the top of this file), which can
    // never fire for a node that no longer exists — and `Plotly.react` replaces every `path.surface` it draws. A
    // pointer-driven drill therefore left the card pinned on screen describing a sector that had gone. `focusout`
    // and `scroll` already cover the keyboard and scroll paths; this covers the mouse one. [Story 20.5 review]
    function redraw() {
      hideTip();
      return Plotly.react(root, [buildTrace()], layout(), CONFIG);
    }

    function applyState(pushHash) {
      renderCrumbs();
      publishScopeState();
      publishSelection();
      if (pushHash) syncHistory();
    }

    // --- Mount. Reveal the host and give it its configured height first (never a literal in this file), then plot.
    // If newPlot throws, the host is hidden again and the text twin is simply the page.
    //
    // The height is CAPPED to the host's own width rather than taken flat from config. `responsive: true` fits the
    // WIDTH to the container and leaves the height exactly as set, so a flat `cfg.size` left a 375 px phone drawing
    // a ~375 px sunburst inside a 375x560 box — ~185 px of dead canvas below it, and a treemap stretched to an
    // aspect it was never sized for. Story 20.5's `.explorer-layout-labelled` breakpoint fixed the RAIL stacking,
    // not the chart's own box. Never taller than configured; never taller than it is wide. [Story 20.5 review]
    var configuredSize = cfg.size || 380;
    function hostHeight() {
      var w = root.clientWidth || configuredSize;
      return Math.max(240, Math.min(configuredSize, w));
    }
    root.style.maxWidth = "100%";
    // `data-hierarchy-ready` MUST be set before `hostHeight()` reads `clientWidth` — `.ss-hierarchy` is
    // `display:none` until that attribute exists, so a read taken beforehand always sees 0 and the width cap
    // silently no-ops on first paint (only a later `resize` event would apply it). [Story 20.7 review]
    root.setAttribute("data-hierarchy-ready", "1");
    root.style.height = hostHeight() + "px";
    state.level = scopeFromHash();
    // Resolve the DEFAULT dimension before the first plot, so the chart is never drawn once in the payload's
    // structural colours and then re-coloured a frame later.
    if (DIMS) {
      var argRoster = dimArgInput(HIERARCHY_ARG_ROSTER);
      var argThreshold = dimArgInput(HIERARCHY_ARG_THRESHOLD);
      dimState.roster = argRoster && argRoster.value ? argRoster.value : null;
      dimState.threshold = readThreshold(argThreshold);
      resolveDimension();
    }
    // `Plotly.newPlot` returns a promise. A SYNCHRONOUS throw lands in the catch below; an ASYNCHRONOUS rejection
    // would sail straight past it, leaving the component reporting a successful mount over an empty panel with the
    // text twin already collapsed. Both routes must reach the same failure exit. [Story 20.5 review]
    function abandonMount() {
      root.removeAttribute("data-hierarchy-ready");
      root.style.height = "";
      try { if (Plotly.purge) Plotly.purge(root); } catch (e2) { /* nothing plotted */ }
      var panelEl = root.closest("[data-explorer]") || root.parentNode;
      if (panelEl && panelEl.setAttribute) {
        panelEl.removeAttribute("data-hierarchy-mounted");
        panelEl.setAttribute("data-hierarchy-failed", "1");
      }
    }
    try {
      var plotted = Plotly.newPlot(root, [buildTrace()], layout(), CONFIG);
      if (plotted && typeof plotted.catch === "function") plotted.catch(abandonMount);
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

    /* --- The surface's own dimension controls [Story 20.9 Task 1.3/1.6] --------------------------------------
       Declarative markers, exactly like `data-hierarchy-filter`: nothing here knows what a dimension MEANS, only
       that a control publishes a key and two optional inputs feed the two rules that cannot be precomputed. The
       controls ride inside the SAME hidden bar as the shape selector, so they inherit the reveal handshake and a
       JS-off visitor never sees an inert control. */
    function dimArgInput(kind) {
      return controls ? controls.querySelector("[data-hierarchy-arg=\"" + kind + "\"]") : null;
    }
    function readThreshold(input) {
      if (!input) return null;
      var v = parseInt(input.value, 10);
      // The shipped fallback, preserved: an empty or nonsensical entry falls back rather than colouring nothing.
      return (isNaN(v) || v < 1) ? 6 : v;
    }

    function applyDimension(announceIt) {
      var d = activeDim();
      if (!d) return;
      // Show only the argument control this dimension actually takes.
      Array.prototype.forEach.call(panel.querySelectorAll("[data-hierarchy-arg-wrap]"), function (wrap) {
        wrap.hidden = wrap.getAttribute("data-hierarchy-arg-wrap") !== (d.arg || "");
      });
      // Exactly one legend block visible per active dimension (AND, Story 20.10, per active VIEW), so the legend
      // can never disagree with what is coloured. The caption is a TEMPLATE the surface wrote — the words are the
      // surface's, not this file's.
      var vKey = activeViewKey();
      Array.prototype.forEach.call(panel.querySelectorAll("[data-hierarchy-legend]"), function (block) {
        var matchesDim = block.getAttribute("data-hierarchy-legend") === (d.legendKey || "");
        var matchesView = !VIEWS || block.getAttribute("data-hierarchy-legend-view") === vKey;
        block.hidden = !(matchesDim && matchesView);
      });
      var legendSelector = "[data-hierarchy-legend=\"" + (d.legendKey || "") + "\"]" + (VIEWS ? "[data-hierarchy-legend-view=\"" + vKey + "\"]" : "");
      var caption = panel.querySelector(legendSelector + " [data-hierarchy-legend-caption]");
      var captionText = caption ? tmpl(caption.getAttribute("data-hierarchy-legend-caption"), { label: d.label }) : "";
      if (caption) caption.textContent = captionText;
      resolveDimension();
      redraw();
      // Re-run Story 20.5's survival predicate path: a dimension change is a RE-RENDER, and the a11y layer has to
      // survive it exactly as it survives a drill. `plotly_afterplot` fires for this redraw and re-applies it.
      if (announceIt) announce(captionText || d.label);
    }

    // --- Shape selector. Revealed only now, because switching a trace type needs script: with JS off it would be
    // an inert control, which is why the server ships it [hidden].
    if (controls) {
      controls.hidden = false;
      // A legend describes a CHART, and on a surface whose chart only exists once this file runs, so does its
      // legend — same reveal-on-mount handshake the controls take, for the same reason. [Story 20.9 Task 1.6]
      var legendBar = panel.querySelector(".ss-hierarchy-legends");
      if (legendBar) legendBar.hidden = false;
      Array.prototype.forEach.call(controls.querySelectorAll(".ss-hierarchy-shape"), function (radio) {
        radio.addEventListener("change", function () {
          if (!radio.checked) return;
          state.shape = radio.value === "treemap" ? "treemap" : "sunburst";
          redraw();
          announce("Showing the " + state.shape);
        });
      });

      if (DIMS) {
        var dimSelect = controls.querySelector("[data-hierarchy-dimension]");
        var rosterInput = dimArgInput(HIERARCHY_ARG_ROSTER);
        var thresholdInput = dimArgInput(HIERARCHY_ARG_THRESHOLD);

        // Populate the roster picker from the payload itself — the alphabetical union of every node's own
        // bounded list, never the panel-wide top-N palette and never sorted by volume (FR-10). Built here rather
        // than server-side because the roster is a property of the DATA, and the emitter would otherwise publish
        // the same list a second time.
        if (rosterInput && rosterInput.tagName === "SELECT" && !rosterInput.options.length) {
          for (var di = 0; di < DIMS.length; di++) {
            if (DIMS[di].arg !== HIERARCHY_ARG_ROSTER) continue;
            dimRoster(DIMS[di]).forEach(function (nm) {
              var opt = document.createElement("option");
              opt.value = nm;
              opt.textContent = nm;
              rosterInput.appendChild(opt);
            });
            break;
          }
          dimState.roster = rosterInput.value || null;
        }

        if (dimSelect) {
          dimSelect.addEventListener("change", function () {
            dimState.key = dimSelect.value;
            applyDimension(true);
          });
        }
        if (rosterInput) {
          rosterInput.addEventListener("change", function () {
            dimState.roster = rosterInput.value || null;
            applyDimension(false);
          });
        }
        if (thresholdInput) {
          thresholdInput.addEventListener("input", function () {
            dimState.threshold = readThreshold(thresholdInput);
            applyDimension(false);
          });
        }
        // Sync once at init rather than trusting the server-baked default to match the control: a bfcache or
        // back-navigation restore of a non-default select value would otherwise leave the chart showing colours
        // the visible control disagrees with. [Review 2026-07-22, preserved]
        if (dimSelect && dimSelect.value) dimState.key = dimSelect.value;
        applyDimension(false);
      }

      // --- Root-subtree filter (config-gated). Same reveal, same bar: a surface's own controls inherit the
      // handshake rather than re-inventing it. The control's `value` IS a root child's node id — that pairing is
      // the entire contract, and nothing here knows what those ids mean.
      if (cfg.filterable) {
        var filterBoxes = Array.prototype.slice.call(controls.querySelectorAll("[data-hierarchy-filter]"));
        // Scoped to this panel, not the document — a page can carry more than one filterable instance.
        var filterEmptyMsg = panel.querySelector(".ss-hierarchy-filter-empty");
        if (filterBoxes.length) {
          var applyFilter = function () {
            var next = Object.create(null), kept = 0;
            filterBoxes.forEach(function (box) { if (box.checked) { next[box.value] = true; kept++; } });
            filterState = kept === filterBoxes.length ? null : next;
            // A drilled scope that the filter just removed would leave Plotly pointing at a level that no longer
            // exists — reset to the top rather than render an empty chart with a stale breadcrumb.
            if (state.level && filterState && !next[state.level]) { state.level = null; }
            redraw();
            applyState(true);
            // A sighted visitor who filters out everything needs a visible reason, not only the aria-live
            // announcement below — the aria-live text is easy to miss without a screen reader running.
            if (filterEmptyMsg) filterEmptyMsg.hidden = kept !== 0;
            announce(kept === filterBoxes.length
              ? "Showing all " + filterBoxes.length
              : "Showing " + kept + " of " + filterBoxes.length);
          };
          filterBoxes.forEach(function (box) { box.addEventListener("change", applyFilter); });
          // The All / None shortcuts the sprint-board dropdown ships. They drive the same one path.
          Array.prototype.forEach.call(controls.querySelectorAll(".impact-select-all, .impact-select-none"), function (btn) {
            var on = btn.classList.contains("impact-select-all");
            btn.addEventListener("click", function () {
              filterBoxes.forEach(function (box) { box.checked = on; });
              applyFilter();
            });
          });
        }
      }
    }

    // --- View switch (Story 20.10 Task 2.3). Declarative, exactly like `data-hierarchy-filter` above: the
    // checkboxes live OUTSIDE this panel (D2 collapsed four panels into one), so this reads them from the WHOLE
    // DOCUMENT via a generic marker rather than a surface-specific id, matching each checkbox's own id + checked
    // state against a view's `when` string. Nothing here learns what "cm-exclude-spec" means.
    //
    // [Review][Patch] NOT nested inside `if (controls)` any more. It was, and nothing in it uses `controls` — so a
    // views-bearing surface shipping no `.ss-hierarchy-controls` bar mounted fine, serialized all its views and was
    // then permanently pinned to VIEWS[0] with no error, while its `when`-declaring checkboxes still APPEARED to
    // work because their pure-CSS half kept filtering the text twin. Two independent config options must not be
    // coupled by where a block happens to sit. It still runs AFTER the controls block so the dimension select's own
    // init sync lands first, exactly as before.
    var syncViewFromHash = null;
    if (VIEWS) {
      var viewToggles = Array.prototype.slice.call(document.querySelectorAll("[data-hierarchy-view-toggle]"));
      var titleEl = panel.querySelector(".chart-frame-head h3");
      var windowEl = panel.querySelector(".chart-frame-window");
      var viewEmptyMsg = panel.querySelector(".ss-hierarchy-filter-empty");

      // [Review][Patch] Matches only the checkboxes a view actually NAMES, by id, in any order. The previous form
      // joined every `[data-hierarchy-view-toggle]` in the document positionally and compared the whole string to
      // `when`, so one extra toggle anywhere on the page — or a second view-bearing instance — made every state
      // match nothing: the chart froze on the default view forever while the table's pure-CSS filter kept
      // responding, with no thrown error and no live-region announcement. Chart and declared twin disagreeing
      // silently is the one failure mode this surface cannot have.
      function viewMatchesToggles(v) {
        if (!v.when) return false;
        var pairs = v.when.split(";");
        for (var i = 0; i < pairs.length; i++) {
          var eq = pairs[i].indexOf("=");
          if (eq < 1) continue;
          var box = document.getElementById(pairs[i].slice(0, eq));
          if (!box) return false;
          if (!!box.checked !== (pairs[i].slice(eq + 1) === "1")) return false;
        }
        return true;
      }
      function viewIndexFromToggles() {
        for (var i = 0; i < VIEWS.length; i++) { if (viewMatchesToggles(VIEWS[i])) return i; }
        return -1;
      }

      // The chrome that tracks the active view, in one place so the init path and every switch paint it identically.
      // [Review][Patch] The analysis window ships `hidden` from the server now (`Charts.FrameWindowSlot`'s
      // `hiddenUntilMount`): its counts are a per-VIEW fact, so a baked value read "every file · 1,220 files" above
      // a table the pure-CSS filter had already cut to 461 rows. Revealed here, with the active view's own string.
      // The empty-view notice is the chart's half of NFR8 — the file table says "No files match this filter." with
      // JS off, and before this the chart said nothing at all.
      function paintViewChrome() {
        var v = activeView();
        if (!v) return;
        if (titleEl && v.title) titleEl.textContent = v.title;
        if (windowEl) {
          windowEl.textContent = v.window || "";
          windowEl.hidden = !v.window;
        }
        if (viewEmptyMsg) viewEmptyMsg.hidden = currentRawNodes.length > 0;
      }

      // Task 2.4: a drilled scope the new view does not contain is reset to the top rather than left pointing at a
      // level that no longer exists — the same precedent `applyFilter` sets above.
      function applyView(pushHash, announceIt) {
        var v = activeView();
        reindex(activeViewRawNodes());
        if (state.level && !byId[state.level]) state.level = null;
        state.selected = null;
        paintViewChrome();
        // Task 2.5: re-run dimension resolution (the ramp re-scales to the ACTIVE view, F3) on every view change,
        // not only a dimension change — nothing was both dimension-bearing and filterable before Code Map's
        // shared payload existed.
        if (DIMS) { applyDimension(false); } else { redraw(); }
        applyState(pushHash);
        // An empty view needs saying out loud too: "Showing … excluding tests" over a blank frame is not an answer.
        if (announceIt && v.title) {
          announce(currentRawNodes.length ? "Showing " + v.title : "Showing " + v.title + " — no items match this filter");
        }
      }

      // Set while THIS block is driving the checkboxes itself (deep-link init, or a Back/Forward restore). The
      // dispatched `change` below must still reach other consumers — the file-table pager needs it — but must not
      // re-enter the switch, which would push a fresh history entry in the middle of handling a popstate.
      var syncingFromHash = false;

      if (viewToggles.length) {
        // Task 2.7 / F4: a deep link naming a view (`{hashKey}-view=`) checks the boxes that view declares it needs
        // BEFORE reading their state back — the checkbox state IS the reader-visible "which filter is active"
        // affordance, so the chart's view and what the page visibly shows must agree, never diverge.
        //
        // [Review][Patch] Setting `.checked` in script fires NO `change` event, and other page enhancements listen
        // for exactly that: the Code Map file-table pager re-pages on `change` because the pure-CSS row filter
        // changes how many rows the reader can see. Without this dispatch a shared `#cm-view=no-tests` link paged
        // the UNFILTERED 1,220 rows — "Page 1 of 41" over short, half-empty and partly blank pages, the very
        // failure Task 4.5 was written to prevent, surviving on the deep-link path. Dispatched BEFORE this block
        // attaches its own listeners below, so it reaches those other consumers and never re-enters the switch.
        var applyViewFromHash = function () {
          var hashView = viewKeyFromHash();
          if (!hashView) return;
          var named = null;
          for (var hv = 0; hv < VIEWS.length; hv++) { if (VIEWS[hv].key === hashView) { named = VIEWS[hv]; break; } }
          if (!named || !named.when) return;
          named.when.split(";").forEach(function (pair) {
            var eq = pair.indexOf("=");
            if (eq < 1) return;
            var wantId = pair.slice(0, eq), wantOn = pair.slice(eq + 1) === "1";
            var box = document.getElementById(wantId);
            if (!box || !box.hasAttribute("data-hierarchy-view-toggle")) return;
            if (!!box.checked === wantOn) return;
            box.checked = wantOn;
            try { box.dispatchEvent(new Event("change", { bubbles: true })); } catch (e) { /* pre-Event-ctor host */ }
          });
        };
        applyViewFromHash();

        viewToggles.forEach(function (box) {
          box.addEventListener("change", function () {
            if (syncingFromHash) return;
            var idx = viewIndexFromToggles();
            if (idx < 0 || idx === viewIndex) return;
            viewIndex = idx;
            applyView(true, true);
          });
        });

        // [Review][Patch] Back/Forward across a view switch used to restore the HASH and nothing else: the history
        // listener read the drilled scope alone, so the URL reverted to `#cm-view=full` while `viewIndex`, the
        // checkbox and the chart all stayed on `no-tests` — and every switch left an entry that could not be undone.
        // Re-checking the boxes from the hash and re-deriving the index puts all three back in agreement; returning
        // true tells `onHistoryScope` the view already redrew, so it does not also redraw for the scope.
        syncViewFromHash = function () {
          syncingFromHash = true;
          try { applyViewFromHash(); } finally { syncingFromHash = false; }
          var idx = viewIndexFromToggles();
          if (idx < 0 || idx === viewIndex) return false;
          viewIndex = idx;
          applyView(false, false);
          return true;
        };

        // Sync once at init — same precedent as the dimension select above: a bfcache/back-navigation restore or a
        // hash-driven check above must not leave the chart on the default view while the visible checkboxes (or the
        // shared link) disagree.
        var initialIdx = viewIndexFromToggles();
        if (initialIdx >= 0 && initialIdx !== viewIndex) { viewIndex = initialIdx; applyView(false, false); }
      }
      // Unconditional: the default view still owes the reader its window text and its empty-state answer, and
      // `applyView` above only runs when the initial view is NOT the default one.
      paintViewChrome();
    }

    // Both listeners are on `window`, so an SPA swap that detaches this host leaves them behind; without the
    // containment check they would call Plotly.react on a node that is no longer in the document.
    function onHistoryScope() {
      if (!document.contains(root)) return;
      // The active view is part of the history entry too (Story 20.10's `{hashKey}-view=`), and it has to be
      // restored BEFORE the scope is validated — `scopeFromHash` checks the id against `byId`, which the view
      // switch rebuilds. [Review][Patch]
      // `syncViewFromHash` already re-applied the view and its own `applyState`, so only the scope is left to check.
      if (syncViewFromHash) syncViewFromHash();
      var next = scopeFromHash();
      if ((next || null) !== state.level) { state.level = next; redraw(); applyState(false); }
    }
    window.addEventListener("hashchange", onHistoryScope);
    window.addEventListener("popstate", onHistoryScope);

    // `responsive: true` re-fits the WIDTH on resize and leaves the height exactly as set, so the cap has to be
    // re-applied or a phone rotated to landscape keeps the portrait box. Debounced on the same 150 ms the ownership
    // sunburst above uses. [Story 20.5 review]
    var sizeTimer = null;
    function onViewportResize() {
      if (sizeTimer) clearTimeout(sizeTimer);
      sizeTimer = setTimeout(function () {
        if (!document.contains(root)) return;
        var h = hostHeight() + "px";
        if (root.style.height !== h) { root.style.height = h; try { Plotly.Plots.resize(root); } catch (e) { /* purged */ } }
      }, 150);
    }
    window.addEventListener("resize", onViewportResize);

    // Everything this instance attached OUTSIDE its own subtree, in one place. The purge loop calls it when the SPA
    // detaches the host: `Plotly.purge` releases Plotly's own listener, but the probe host and these three window
    // listeners are ours, and each closes over the whole NODES payload — so without this every content swap leaked
    // one detached <div> and three live listeners retaining a ~190-node array. Task 4.11 names this exact failure
    // ("leaks one per swap") and only the Plotly half of it had been done. [Story 20.5 review]
    root.__ssHierarchyCleanup = function () {
      window.removeEventListener("hashchange", onHistoryScope);
      window.removeEventListener("popstate", onHistoryScope);
      window.removeEventListener("resize", onViewportResize);
      if (sizeTimer) clearTimeout(sizeTimer);
      if (probeHost && probeHost.parentNode) probeHost.parentNode.removeChild(probeHost);
    };
    // --- There is no longer a server-rendered chart to take over from. Story 20.7 retired the SVG on every
    // surface this component serves, so the hide/restore pair, the restore hook and the ready flag this block used
    // to set are all deleted: they existed to coordinate with Story 20.2's drill-in over a shared SVG, and both
    // the SVG and 20.2's block are gone.
    //
    // What stands behind a failed mount is now the TEXT TWIN, which is ADR 0013 §2's contract and is
    // server-rendered on every instance regardless of what happens here. That is a stronger fallback than the one
    // it replaces, not a weaker one: the twin is complete and navigable with no script at all, whereas the
    // retained SVG needed this file to be reachable in order to be drilled.
    // Ends the boot placeholder and disarms the inline script's expiry timer.
    panel.setAttribute("data-hierarchy-mounted", "1");

    // A surface that ships its OWN visible listing can mark it to collapse once the chart is live. Declarative and
    // generic, exactly like `data-hierarchy-filter`: nothing here knows what the element is. The Impact Map's
    // epic-grouped <details> is the first user — it is `open` in the served HTML so a JS-off visitor gets the
    // whole content, and the shipped `initImpactMap` collapsed it on mount for the same reason this does.
    // Collapsing NEVER removes it: it stays one click away, which is what makes this presentation and not loss.
    Array.prototype.forEach.call(
      document.querySelectorAll("[data-hierarchy-collapse-on-mount]"),
      function (el) { if (el.open) el.open = false; });

    applyState(false);
    return true;
  }

  initHierarchyExplorers(document);
  document.addEventListener("specscribe:content-swapped", function (e) {
    initHierarchyExplorers(e && e.detail ? e.detail.root : document);
  });

  /* ==== The relationship graph component [Story 24.2 / ADR 0030] ==============================================
     A code page's ego coupling graph: the focal file pinned dead-centre, its citing artifacts and most-coupled
     files on a ring, drawn with the ALREADY-VENDORED Plotly `scatter` trace over a layout the C# side solved at
     generation time. Marginal bundle cost: zero bytes.

     Three things this block deliberately does NOT do, each an ADR 0030 clause rather than a preference:
       1. NO client-side force simulation, no iterative solver, no physics. Node position is DATA. It arrives in
          the island and is drawn; the client never computes one.
       2. NO re-layout on filtering. The two filters HIDE edges (and the epic hubs those edges connect); every
          surviving node keeps the exact coordinate it was solved with. Measured in the 24.6 spike at 44-75 ms
          with nodePositionsMoved:false — which is also what makes a filter feel like a filter and not a redraw.
       3. NO style derivation. The server resolved every edge to a style CLASS and shipped the table, so the
          legend, the payload and the drawn chart are physically incapable of disagreeing.

     Progressive enhancement throughout: a missing bundle, a blocked script, a zero-node payload or a throw
     anywhere below leaves the reader with the server-rendered TEXT TWIN, which is complete, navigable, non-colour
     and needs no script at all (ADR 0013 §2). */
  var relGraphMounts = [];
  var relGraphPending = [];

  // The nearest ancestor that is laid out before any script runs. Same `|| parentNode` fallback the hierarchy
  // component uses, and for the same reason: the panel hook is opt-in and a call site may omit it.
  function relGraphPanelOf(root) {
    return (root.closest && root.closest("[data-relgraph-panel]")) || root.parentNode || root;
  }

  function initRelationshipGraphs(scope) {
    var host = scope && scope.querySelectorAll ? scope : document;
    // Purge instances whose host left the document. The SPA swaps the content region via innerHTML, which detaches
    // the graph div while `responsive: true` keeps a window listener alive — a naive re-init leaks one per swap.
    for (var i = relGraphMounts.length - 1; i >= 0; i--) {
      if (!document.contains(relGraphMounts[i])) {
        try { if (window.Plotly && Plotly.purge) Plotly.purge(relGraphMounts[i]); } catch (e) { /* already gone */ }
        var cleanup = relGraphMounts[i].__ssRelGraphCleanup;
        if (typeof cleanup === "function") { try { cleanup(); } catch (e2) { /* best effort */ } }
        relGraphMounts.splice(i, 1);
      }
    }
    Array.prototype.forEach.call(host.querySelectorAll("[data-relgraph]"), function (root) {
      if (root.getAttribute("data-relgraph-ready")) return;
      // ⚠ THE ZERO-WIDTH MOUNT TRAP. The code page's tabs are pure-CSS radios, so whenever an Insights panel exists
      // the Relationships panel is `display:none` — zero width — right now. Plotly cannot lay out in a zero-width
      // container and does not complain: it draws a chart of the wrong size, which looks fine until someone reveals
      // the panel. MEASURED on the panel, never on the host: the host's own `.ss-relgraph` rule is `display:none`
      // until this block reveals it, so `root.clientWidth` is zero for every instance at this point.
      if (!relGraphPanelOf(root).clientWidth) {
        if (relGraphPending.indexOf(root) === -1) relGraphPending.push(root);
        return;
      }
      try {
        if (initRelationshipGraph(root)) {
          root.setAttribute("data-relgraph-ready", "1");
          relGraphMounts.push(root);
        } else {
          // Declined rather than threw (no engine, no island, empty payload) — same outcome for the reader, so
          // release the boot placeholder now instead of leaving it until the inline script's expiry, and let the
          // already-rendered text twin be the page.
          var declined = relGraphPanelOf(root);
          if (declined && declined.setAttribute) declined.setAttribute("data-relgraph-failed", "1");
        }
      } catch (err) {
        // A throw can land here AFTER Plotly.newPlot already succeeded — the ready flag is set before plotting and
        // the control wiring runs after it. Marking the panel failed and stopping would leave the instance mounted
        // but absent from the purge registry (never cleaned on a later swap) with the ready flag still set (re-init
        // skips this root forever). Unwind properly instead. [the Story 20.5 failure-unwind finding, inherited]
        unwindRelGraph(root);
      }
    });
    flushRelGraphReveals();
  }

  function unwindRelGraph(root) {
    try { if (window.Plotly && Plotly.purge) Plotly.purge(root); } catch (e) { /* nothing plotted */ }
    root.removeAttribute("data-relgraph-ready");
    root.style.height = "";
    var cleanup = root.__ssRelGraphCleanup;
    if (typeof cleanup === "function") { try { cleanup(); } catch (e) { /* best effort */ } }
    var panel = relGraphPanelOf(root);
    if (panel && panel.setAttribute) {
      panel.removeAttribute("data-relgraph-mounted");
      panel.setAttribute("data-relgraph-failed", "1");
    }
  }

  /* Deferred mounts: hosts that were zero-width when we first reached them. Two things happen on a reveal and only
     one is a mount — a host never plotted gets its FIRST mount, and a host plotted while visible and since resized
     gets `Plotly.Plots.resize`, the documented way to re-lay-out a plot whose container changed without a window
     event. The trigger is ONE delegated listener, never one per pending host: the SPA replaces the content region
     wholesale and a per-host listener would retain a detached node on every swap. */
  function flushRelGraphReveals() {
    var still = [];
    for (var i = 0; i < relGraphPending.length; i++) {
      var root = relGraphPending[i];
      if (!document.contains(root)) continue;              // dropped by an SPA swap — forget it
      if (root.getAttribute("data-relgraph-ready")) continue;
      if (!relGraphPanelOf(root).clientWidth) { still.push(root); continue; }
      try {
        if (initRelationshipGraph(root)) {
          root.setAttribute("data-relgraph-ready", "1");
          relGraphMounts.push(root);
        }
      } catch (err) { /* one bad instance must not down the others; the text twin stands */ }
    }
    relGraphPending = still;

    for (var j = 0; j < relGraphMounts.length; j++) {
      var m = relGraphMounts[j];
      if (!document.contains(m) || !m.clientWidth) continue;
      try { if (window.Plotly && Plotly.Plots) Plotly.Plots.resize(m); } catch (e) { /* purged */ }
    }
  }
  document.addEventListener("change", function (e) {
    var t = e && e.target;
    if (t && t.getAttribute && t.getAttribute("data-relgraph-reveal") !== null) flushRelGraphReveals();
  });

  function initRelationshipGraph(root) {
    // No engine, no takeover. Checked first so a blocked or absent bundle costs nothing and changes nothing.
    if (typeof Plotly === "undefined" || !Plotly.newPlot || !Plotly.restyle) return false;

    var dataEl = document.getElementById(root.id + "-data");
    if (!dataEl) return false;
    var payload;
    try { payload = JSON.parse(dataEl.textContent); } catch (e) { return false; }
    var cfg = payload && payload.config;
    var NODES = (payload && payload.nodes) || [];
    var EDGES = (payload && payload.edges) || [];
    var STYLES = (payload && payload.styles) || [];
    if (!cfg || !NODES.length) return false;

    var panel = relGraphPanelOf(root);
    var live = panel.querySelector(".ss-relgraph-live");
    var controls = panel.querySelector(".ss-relgraph-controls");

    /* --- Presentation comes from SpecScribe's TOKENS, resolved through the real cascade — never a Plotly colorway
           (ADR 0012 §6). The payload ships token NAMES; nothing here types a colour, so a theme switch is free and
           the `--status-*` lifecycle tokens (off-limits on code surfaces) cannot leak in. */
    function token(name, fallback) {
      var v = "";
      try { v = getComputedStyle(root).getPropertyValue(name); } catch (e) { v = ""; }
      v = (v || "").trim();
      return v || fallback;
    }
    var ink = token(cfg.tokens.ink, "#333");
    var palette = {
      focal: token(cfg.tokens.focal, "#b8860b"),
      artifact: token(cfg.tokens.artifact, "#b8860b"),
      epic: token(cfg.tokens.epic, "#333"),
      coupled: token(cfg.tokens.coupled, "#777"),
      surface: token(cfg.tokens.surface, "#fff"),
      border: token(cfg.tokens.border, "#ccc")
    };

    // Shape carries node kind, never hue alone (UX-DR17) — and it is the same vocabulary the retired SVG used, so
    // a returning reader is not re-taught the graph (owner decision D1).
    var SYMBOL = { focal: "square", artifact: "circle", epic: "square-open", coupled: "diamond" };
    var FILL = { focal: palette.focal, artifact: palette.artifact, epic: palette.epic, coupled: palette.coupled };

    /* --- The per-KIND table: which filter governs an edge of each kind, and the phrase describing it. Both are
           properties of the kind, so the server ships one row per kind instead of two fields on every edge —
           measured, not guessed: the fully-composed form put one real code page's island at 55,012 B, 56% of it
           cross-edge sentences re-spelling paths already in the node array. */
    var KINDS = {};
    ((cfg.kinds) || []).forEach(function (k) { KINDS[k.k] = k; });

    // The wording is entirely SERVER-authored; this substitutes two values it already holds into it. That is a
    // different thing from the client inventing prose, and it is what keeps one phrase in one language.
    function edgeText(e) {
        if (e.t) return e.t;
        var k = KINDS[e.e];
        if (!k || !k.phrase) return "";
        var a = NODES[e.a], b = NODES[e.b];
        if (!a || !b) return "";
        return k.phrase.replace("{a}", a.p).replace("{b}", b.p);
    }

    /* --- Filter state. Owner decision D3: BOTH toggles survive as edge-visibility filters over the ONE solved
           layout. Unchecked by default, matching the retired pure-CSS toggles' default. */
    var filters = { epic: false, cross: false };

    function edgeFilter(e) { var k = KINDS[e.e]; return k ? k.f : null; }
    function edgeVisible(e) { var f = edgeFilter(e); return !f || filters[f] === true; }
    // An epic hub is drawn only while its own filter is on: hiding its edges but keeping the chip would leave a
    // disconnected node floating with nothing to say. Hiding a node is not re-laying-out — every node that DOES
    // survive keeps its solved coordinate, which is the ADR 0030 §4 invariant.
    function nodeVisible(n) { return n.k !== "epic" || filters.epic === true; }

    /* --- Edge traces, grouped by the server's style class. THIS IS THE PLOTLY CONSTRAINT, MADE VISIBLE: `line` is
           a TRACE-level attribute, so per-edge dash/width is only reachable by one trace per style class — which is
           exactly why stroke width is BANDED and why the legend says so (ADR 0030 §5). Within a trace, segments are
           separated by a null vertex so one trace draws many disjoint lines. */
    var styleIndex = {};
    STYLES.forEach(function (s, i) { styleIndex[s.k] = i; });

    function edgeCoords() {
      var xs = [], ys = [], members = [];
      for (var i = 0; i < STYLES.length; i++) { xs.push([]); ys.push([]); members.push([]); }
      for (var e = 0; e < EDGES.length; e++) {
        var edge = EDGES[e];
        if (!edgeVisible(edge)) continue;
        var a = NODES[edge.a], b = NODES[edge.b];
        if (!a || !b || !nodeVisible(a) || !nodeVisible(b)) continue;
        var t = styleIndex[edge.s];
        if (t === undefined) continue;
        xs[t].push(+a.x, +b.x, null);
        ys[t].push(+a.y, +b.y, null);
        members[t].push(e);
      }
      return { xs: xs, ys: ys, members: members };
    }

    var edgeGeom = edgeCoords();
    var edgeTraces = STYLES.map(function (s, i) {
      return {
        type: "scatter", mode: "lines", x: edgeGeom.xs[i], y: edgeGeom.ys[i],
        hoverinfo: "skip", showlegend: false, name: s.k,
        line: { color: token(s.tok, palette.coupled), width: s.w, dash: s.dash }
      };
    });

    /* --- Per-edge hover needs its own invisible midpoint trace: a `lines` trace hovers on VERTICES, not segments.
           Recorded in ADR 0030 as a real cost of this engine choice rather than hidden behind a working tooltip. */
    function midpoints() {
      var x = [], y = [], members = [];
      for (var e = 0; e < EDGES.length; e++) {
        var edge = EDGES[e];
        if (!edgeVisible(edge)) continue;
        var a = NODES[edge.a], b = NODES[edge.b];
        if (!a || !b || !nodeVisible(a) || !nodeVisible(b)) continue;
        x.push((+a.x + +b.x) / 2);
        y.push((+a.y + +b.y) / 2);
        members.push(e);
      }
      return { x: x, y: y, members: members };
    }
    var midGeom = midpoints();
    var midTrace = {
      type: "scatter", mode: "markers", x: midGeom.x, y: midGeom.y,
      marker: { size: 9, opacity: 0.001, color: ink },
      hoverinfo: "skip", showlegend: false, name: "edge-midpoints"
    };

    /* --- Nodes: ONE trace, per-point size / colour / SYMBOL. `marker.symbol` accepts an array, which is the
           non-colour channel available for a node. */
    var maxWeight = Math.max(1, +cfg.maxWeight || 1);
    function nodeGeom() {
      var g = { x: [], y: [], size: [], color: [], symbol: [], members: [] };
      for (var i = 0; i < NODES.length; i++) {
        var n = NODES[i];
        if (!nodeVisible(n)) continue;
        g.x.push(+n.x);
        g.y.push(+n.y);
        // The hub takes a FIXED size, above the ring's whole range: it is what the graph is about, not a
        // participant in the ranking, and the server excludes it from `maxWeight` for the same reason. Ring
        // markers use sqrt so AREA tracks weight rather than radius — a 4x weight must not read as a 16x blob.
        // The 9..24 px band is bounded by RING DENSITY, not by taste: at the D2 caps the innermost arc gives each
        // coupled marker ~28 px of room, and a band topping out at 30 px overlapped its neighbours on the live
        // page. Widening it again means widening the ring (RelationshipGraph.Size) in the same change.
        g.size.push(n.k === "focal" ? 30 : 9 + 15 * Math.sqrt(Math.min(1, (+n.w || 1) / maxWeight)));
        g.color.push(FILL[n.k] || palette.coupled);
        g.symbol.push(SYMBOL[n.k] || "circle");
        g.members.push(i);
      }
      return g;
    }
    var nGeom = nodeGeom();
    var nodeTrace = {
      type: "scatter", mode: "markers", x: nGeom.x, y: nGeom.y,
      marker: { size: nGeom.size, color: nGeom.color, symbol: nGeom.symbol, line: { width: 1.5, color: palette.surface } },
      hoverinfo: "skip", showlegend: false, name: "nodes"
    };
    // Which payload node each drawn point IS. Recomputed on every filter so the a11y layer never stamps a label
    // onto the wrong marker after the epic hubs come and go.
    var drawnNodes = nGeom.members;
    var drawnEdges = midGeom.members;

    var layout = {
      margin: { l: 6, r: 6, t: 6, b: 6 },
      // The aspect lock anchors X TO Y, not the other way round, and that direction is load-bearing. The panel is
      // wide and short (measured 886x420), so anchoring y to x makes Plotly SHRINK the y range to match x's
      // px-per-unit — 1.16 units over 886px is 764 px/unit, which leaves y showing only 0.55 units, and every node
      // outside 0.225..0.775 is drawn beyond the visible box. Observed live as a 618px vertical spread inside a
      // 420px host. Anchoring x to y keeps the SHORT axis whole and widens x instead, so the whole unit square is
      // visible and the ring stays a circle rather than an ellipse.
      xaxis: { visible: false, range: [-0.08, 1.08], scaleanchor: "y", fixedrange: true },
      yaxis: { visible: false, range: [-0.08, 1.08], fixedrange: true },
      paper_bgcolor: "rgba(0,0,0,0)",
      plot_bgcolor: "rgba(0,0,0,0)",
      font: { color: ink },
      showlegend: false,
      hovermode: false,
      dragmode: false
    };
    var CONFIG = { displaylogo: false, displayModeBar: false, responsive: true, scrollZoom: false, doubleClick: false };

    /* --- Reading order. The story's spike recommended degree-desc for a WHOLE-REPO graph; here the requirement it
           serves — "twin and graph must agree" — is met more exactly by the server's own emission order, because
           that order IS the twin's: citing artifacts in the twin's first section, then the coupled files in the
           twin's confidence-desc sub-list (Story 24.1 Q4). Deriving a degree ranking client-side would put a
           high-degree coupled file ahead of the citers and DISAGREE with the listing directly underneath it. So the
           rove order is the payload order, filtered to what is drawn — never the DOM order Plotly happens to emit. */
    var focusIndex = 0;

    function announce(msg) { if (live) live.textContent = msg; }

    function nodePaths() {
      var traces = root.querySelectorAll("g.scatterlayer g.trace");
      var group = traces[traces.length - 1];
      return group ? group.querySelectorAll("path.point") : [];
    }

    function applyA11yLayer() {
      var svg = root.querySelector("svg.main-svg");
      if (svg) {
        Array.prototype.forEach.call(root.querySelectorAll("svg"), function (s) {
          s.setAttribute("role", "presentation");
        });
      }
      // The graphics role lives on the HOST, not on Plotly's <svg>: the markers are nested inside presentational
      // <g> wrappers, so a role on the <svg> would put wrappers between the document and its items.
      root.setAttribute("role", "application");
      root.setAttribute("aria-roledescription", "relationship graph");
      root.setAttribute("aria-label",
        (cfg.title || "Relationships") + " — " + drawnNodes.length + " items, " + drawnEdges.length + " connections");

      var pts = nodePaths();
      // Clamp the roving index on EVERY reapply. Story 20.4's sixth finding was an unclamped roving index leaving
      // the chart Tab-unreachable after the node count shrank — and this component SHRINKS its node count every
      // time the epic filter is switched off, so the failure is reachable here, not hypothetical.
      if (focusIndex >= drawnNodes.length) focusIndex = drawnNodes.length ? drawnNodes.length - 1 : 0;
      if (focusIndex < 0) focusIndex = 0;

      for (var i = 0; i < pts.length; i++) {
        var el = pts[i];
        var n = NODES[drawnNodes[i]];
        if (!n) continue;
        el.setAttribute("role", n.h ? "link" : "img");
        el.setAttribute("tabindex", i === focusIndex ? "0" : "-1");
        // Prose composed SERVER-side, so the accessible name, the tooltip and the twin's row are one string in one
        // language and cannot drift apart.
        el.setAttribute("aria-label", n.t);
        el.setAttribute("data-tip-html", escapeTip(n.t));
        el.setAttribute("data-relgraph-index", String(i));
        if (n.h) el.setAttribute("data-relgraph-href", n.h);
        else el.removeAttribute("data-relgraph-href");
        if (el.classList) el.classList.add("ss-relgraph-node");
        if (!el.__ssRelBound) {
          el.__ssRelBound = true;
          el.addEventListener("keydown", onNodeKeydown);
          el.addEventListener("click", onNodeClick);
          el.addEventListener("focus", onNodeFocus);
        }
      }

      // Edge midpoints carry the edge's own text. Plotly emits the midpoint trace immediately before the node
      // trace, so it is addressed by position from the END rather than by an absolute index that would shift as
      // style classes come and go.
      var traces = root.querySelectorAll("g.scatterlayer g.trace");
      var midGroup = traces.length >= 2 ? traces[traces.length - 2] : null;
      if (midGroup) {
        var mids = midGroup.querySelectorAll("path.point");
        for (var m = 0; m < mids.length; m++) {
          var edge = EDGES[drawnEdges[m]];
          if (!edge) continue;
          var text = edgeText(edge);
          mids[m].setAttribute("role", "img");
          mids[m].setAttribute("aria-label", text);
          mids[m].setAttribute("data-tip-html", escapeTip(text));
          if (mids[m].classList) mids[m].classList.add("ss-relgraph-edge");
        }
      }
    }

    // `data-tip-html` is injected as markup by the shared tooltip, and these strings carry repository paths — which
    // are author-controlled text. Escaped here rather than trusted.
    function escapeTip(s) {
      return String(s == null ? "" : s)
        .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
    }

    function onNodeFocus(ev) {
      var idx = ev.currentTarget.getAttribute("data-relgraph-index");
      if (idx !== null) focusIndex = +idx;
    }

    function focusAt(i) {
      var pts = nodePaths();
      if (!pts.length) return;
      focusIndex = ((i % pts.length) + pts.length) % pts.length;
      for (var j = 0; j < pts.length; j++) pts[j].setAttribute("tabindex", j === focusIndex ? "0" : "-1");
      pts[focusIndex].focus();
      announce(pts[focusIndex].getAttribute("aria-label") || "");
    }

    // Mode is `navigate` (ADR 0012 §3): activating a node follows its href to that file's or artifact's own page.
    // `select` mode and a details pane are not in scope; if one is ever wanted it must ride the SHIPPED
    // `specscribe:explorer-select` seam, never a parallel event (ADR 0030 §1).
    function activate(el) {
      var href = el.getAttribute("data-relgraph-href");
      if (!href) { announce(el.getAttribute("aria-label") || ""); return; }
      navigateTo(href);
    }

    function onNodeClick(ev) { activate(ev.currentTarget); }

    function onNodeKeydown(ev) {
      if (ev.key === "ArrowRight" || ev.key === "ArrowDown") { ev.preventDefault(); focusAt(focusIndex + 1); }
      else if (ev.key === "ArrowLeft" || ev.key === "ArrowUp") { ev.preventDefault(); focusAt(focusIndex - 1); }
      else if (ev.key === "Home") { ev.preventDefault(); focusAt(0); }
      else if (ev.key === "End") { ev.preventDefault(); focusAt(-1); }
      else if (ev.key === "Enter" || ev.key === " ") { ev.preventDefault(); activate(ev.currentTarget); }
      else if (ev.key === "Escape") { ev.preventDefault(); root.blur(); hideTip(); }
    }

    /* --- The two filters. They RESTYLE — they never re-plot and never re-lay-out. Positions are data; a filter
           changes which of them are drawn, not what they are (ADR 0030 §4). */
    function applyFilters() {
      var eg = edgeCoords();
      var mg = midpoints();
      var ng = nodeGeom();
      drawnNodes = ng.members;
      drawnEdges = mg.members;
      // The NODE trace's geometry and its marker arrays go in ONE call. Splitting them left a window in which the
      // trace held 40 positions and 35 marker entries, and `plotly_afterplot` fires inside that window — so the
      // a11y layer could stamp labels against a DOM that had not caught up, mapping a node's accessible name onto
      // a different node's marker. Observed live as "20 survivors moved" on a filter toggle; the settled state was
      // correct both before and after, which is exactly why only a live pass could see it.
      Plotly.restyle(root, {
        x: [ng.x], y: [ng.y],
        "marker.size": [ng.size], "marker.color": [ng.color], "marker.symbol": [ng.symbol]
      }, [STYLES.length + 1]);

      var xs = [], ys = [], idx = [];
      for (var i = 0; i < STYLES.length; i++) { xs.push(eg.xs[i]); ys.push(eg.ys[i]); idx.push(i); }
      xs.push(mg.x); ys.push(mg.y); idx.push(STYLES.length);
      Plotly.restyle(root, { x: xs, y: ys }, idx);
      announce(drawnNodes.length + " items and " + drawnEdges.length + " connections shown");
    }

    if (controls) {
      // Revealed only NOW, because both filters need script: the server ships the bar [hidden] precisely so a
      // JS-off, webview, SPA or engine-blocked reader never sees two checkboxes that do nothing. Caught in the live
      // pass — the bar stayed hidden after a successful mount, which is the mirror-image failure and just as
      // invisible to the suite: every assertion about the `hidden` attribute passed, because emitting it hidden was
      // never the half that was missing.
      controls.hidden = false;
      Array.prototype.forEach.call(controls.querySelectorAll("[data-relgraph-filter]"), function (input) {
        var key = input.getAttribute("data-relgraph-filter");
        filters[key] = !!input.checked;
        input.addEventListener("change", function () {
          filters[key] = !!input.checked;
          applyFilters();
          // `plotly_afterplot` fires for the restyle, so the a11y layer re-applies itself and the roving index is
          // re-clamped against the new count. Nothing here calls applyA11yLayer directly.
        });
      });
    }

    root.style.height = (+cfg.size || 420) + "px";

    /* `Plotly.newPlot` returns a promise. A SYNCHRONOUS throw lands in the catch below; an ASYNCHRONOUS rejection
       would sail straight past it, leaving the component reporting a successful mount over an empty panel. Both
       routes must reach the same failure exit. [inherited from the Story 20.5 review] */
    try {
      var plotted = Plotly.newPlot(root, edgeTraces.concat([midTrace, nodeTrace]), layout, CONFIG);
      if (plotted && typeof plotted.catch === "function") plotted.catch(function () { unwindRelGraph(root); });
    } catch (e) {
      root.style.height = "";
      return false;
    }

    /* The a11y layer is applied ONLY through `plotly_afterplot`, Plotly's public post-render event, over its
       emitted DOM. No Plotly internal is patched. Two reasons for that hook and not the promise `newPlot` returns:
         1. it is the only hook that ALSO fires for re-renders this component did not initiate (a responsive resize,
            a host-driven relayout, a bare `Plotly.react`) — which is what "the layer survives" has to mean;
         2. Plotly resolves its own promises off an animation frame, so awaiting one never settles in a
            non-compositing tab. Measured in the 24.6 spike, not assumed. */
    if (root.on) root.on("plotly_afterplot", function () { applyA11yLayer(); });
    applyA11yLayer();

    // Already-mounted hosts whose container later changes size. `responsive: true` refits on a WINDOW resize only,
    // so a CSS-only reveal (switching tabs) leaves the plot at whatever width it was drawn with.
    var sizeTimer = null;
    function onViewportResize() {
      if (sizeTimer) clearTimeout(sizeTimer);
      sizeTimer = setTimeout(function () {
        if (!document.contains(root)) return;
        try { Plotly.Plots.resize(root); } catch (e) { /* purged */ }
      }, 150);
    }
    window.addEventListener("resize", onViewportResize);

    // Everything this instance attached OUTSIDE its own subtree, in one place. The purge loop calls it when the SPA
    // detaches the host: `Plotly.purge` releases Plotly's OWN listener, but this one is ours and it closes over the
    // whole payload.
    root.__ssRelGraphCleanup = function () {
      window.removeEventListener("resize", onViewportResize);
      if (sizeTimer) clearTimeout(sizeTimer);
    };

    // The legend is revealed on the SAME successful mount, and outside the `if (controls)` block above because an
    // instance can have a legend and no filters at all (a citations-only card). It ships `hidden` because a legend
    // describes a CHART: with JS off the text twin carries the information, and a key to a picture nobody can see
    // is chrome for nothing. Same convention the hierarchy component's legend bar already follows.
    var legend = panel.querySelector(".ss-relgraph-legend");
    if (legend) legend.hidden = false;
    var legendNote = panel.querySelector(".ss-relgraph-legend-note");
    if (legendNote) legendNote.hidden = false;

    // Ends the boot placeholder and disarms the inline script's expiry timer.
    panel.setAttribute("data-relgraph-mounted", "1");
    announce("Relationship graph ready: " + drawnNodes.length + " items");
    return true;
  }

  initRelationshipGraphs(document);
  document.addEventListener("specscribe:content-swapped", function (e) {
    initRelationshipGraphs(e && e.detail ? e.detail.root : document);
  });

  // ---- Remaining-work sunburst explorer [Story 20.2] -> RETIRED by Story 20.7 ----------------
  // 20.2's client drill-in (`initSunburstExplorers` / `initSunburstExplorer`) and its arc RE-LAYOUT port of
  // Charts.AnnularSector/InsetStart/InsetEnd were deleted here, together with the `sunburst-explorer-data` island
  // and the server-rendered SVG they both enhanced. The Hierarchy Explorer component above is now the only route
  // to a planning hierarchy chart, and the text twin — not a retained SVG — is what stands behind a failed mount
  // (ADR 0013 §2). Much of 20.2's hard-won knowledge stopped mattering with it: SVGAElement has no .click(), an
  // SVG <a> at display:none stays focusable, and the re-layout had to restore each wedge's original `d`. None of
  // those are properties of anything this file still draws. [Story 20.7 Task 8.3]

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
      // The story-tier disclosure exists for the JS-OFF reader (see RelatedWorkTemplater: 179 stacked cards
      // otherwise). With JS on, every `[data-related-node]` card is display:none until it is the current one — but a
      // CLOSED <details> would hide the current one too, so open it once here and let the CSS drop its summary.
      // [Story 20.5 review]
      Array.prototype.forEach.call(pane.querySelectorAll("details.related-work-more"), function (d) { d.open = true; });
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
      var hit = selecting && cardAnswersFor(c, nodeId);
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

  // Does this card answer for `nodeId`? Its own id, or one of the REDIRECTS the server published on
  // `data-related-alias` (Story 20.8 D3 — `epic-N~summary` resolves to `epic-N`, whose page it already links to).
  // The alias list is data the C# decided (RelatedWorkCards.CanonicalIslandId); this function only reads it, so
  // there is no second place that knows one id can stand for another.
  function cardAnswersFor(card, nodeId) {
    if (card.getAttribute("data-related-node") === nodeId) return true;
    var alias = card.getAttribute("data-related-alias");
    if (!alias) return false;
    return (" " + alias + " ").indexOf(" " + nodeId + " ") !== -1;
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
