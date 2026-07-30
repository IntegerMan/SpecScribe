/* SpecScribe JSON+SPA delivery client — Story 6.7 (ADR 0006 Architecture B).
   The small vanilla-JS renderer for the opt-in `--spa` delivery form: it fetches the C#-emitted JSON data layer
   (a manifest + grouped content chunks) and navigates the WHOLE site client-side from a handful of files instead
   of loading thousands of static .html documents.

   By policy this script adds CONVENIENCE, never INFORMATION (NFR6 / progressive enhancement): rendering stays in
   C# — every content region it injects was rendered by the core and ships pre-rendered (charts as inline SVG). It
   fetches, injects, and updates the URL; it re-renders nothing and re-parses nothing. The untouched static site is
   the source of truth and the no-JS fallback: with JS off, the inlined dashboard is readable and its links reach
   the real static pages; if the data layer can't be fetched, links simply fall through to static navigation. */
(function () {
  "use strict";

  var content = document.getElementById("spa-content");
  if (!content) return;

  // Directory of THIS entry document, captured once up front — before any pushState changes location.pathname —
  // so every fetch and pushState target resolves against the site root regardless of the current (possibly
  // nested) URL. On reload of a pushed nested URL the browser simply loads that static page (graceful).
  var basePrefix = location.pathname.slice(0, location.pathname.lastIndexOf("/") + 1);

  // { schemaVersion, siteTitle, entry, nav, oversizedPages, pages: { path: { title, chunk, breadcrumb, parent,
  //   children, head, scriptIslands, contentHash, bytes } } }
  // This client reads only title + chunk. The remaining fields are the canonical IR's contract for OTHER consumers
  // (Story 22.2 / ADR 0016) — head projection, script-island declarations, and per-page delta addressing that
  // Stories 22.5/22.6 will diff against. Deliberately NOT consumed here: 22.2 ships addressing, not a delta
  // channel. Additive fields never bump schemaVersion, so an older cached copy of this script still works.
  var manifest = null;
  var chunkCache = {};          // chunkFile -> { path: regionHtml }
  var currentPath = content.getAttribute("data-path") || "index.html";
  // The build's cache-busting token (identical to specscribe.css/.js's own ?v=) — appended to every manifest/chunk
  // fetch so a redeployed data layer is never masked by a browser/CDN cache of the previous build. [Story 6.7 review]
  var assetVersion = content.getAttribute("data-asset-version") || "";

  function versioned(url) {
    return assetVersion ? url + (url.indexOf("?") >= 0 ? "&" : "?") + "v=" + assetVersion : url;
  }

  function fetchJson(url) {
    return fetch(versioned(url)).then(function (r) {
      if (!r.ok) throw new Error(url + " -> HTTP " + r.status);
      return r.json();
    });
  }

  // Resolve a rendered relative href ("story-1-1.html", "../index.html", "epics.html#epic-2") against the CURRENT
  // surface's output-relative path. The client tracks the current path itself (data-path), never the document URL,
  // because a swapped region's base never changes and the URL may be push-state'd to a nested path.
  function resolve(href, basePath) {
    var baseDir = basePath.indexOf("/") >= 0 ? basePath.slice(0, basePath.lastIndexOf("/") + 1) : "";
    var parts = (baseDir + href).split("/");
    var out = [];
    for (var i = 0; i < parts.length; i++) {
      if (parts[i] === "" || parts[i] === ".") continue;
      if (parts[i] === "..") { out.pop(); continue; }
      out.push(parts[i]);
    }
    return out.join("/");
  }

  function pageInfo(path) {
    return manifest && manifest.pages ? manifest.pages[path] : null;
  }

  function chunkFor(path) {
    var info = pageInfo(path);
    if (!info) return Promise.resolve(null);
    if (chunkCache[info.chunk]) return Promise.resolve(chunkCache[info.chunk]);
    return fetchJson(basePrefix + info.chunk).then(function (data) {
      chunkCache[info.chunk] = data;
      return data;
    });
  }

  function scrollToFragment(fragment) {
    if (fragment) {
      var el = document.getElementById(fragment);
      if (el) { el.scrollIntoView(); return; }
    }
    window.scrollTo(0, 0);
  }

  function hardNavigate(path, fragment) {
    location.href = basePrefix + path + (fragment ? "#" + fragment : "");
  }

  // Tell the enhancement layer (specscribe.js) that the content region now holds different markup. Kept
  // failure-tolerant: a browser without the CustomEvent constructor, or a listener that throws, must never break
  // navigation itself — the swapped region is already complete, readable server truth. [Story 20.2 review]
  function notifyContentSwapped() {
    try {
      document.dispatchEvent(new CustomEvent("specscribe:content-swapped", { detail: { root: content } }));
    } catch (e) { /* enhancement is convenience-only (NFR6); navigation stands */ }
  }

  // Swap the content region in place. Any miss (unknown page, chunk fetch failure) degrades to a real navigation
  // to the static page — never a blank surface.
  function navigate(path, fragment, push) {
    var info = pageInfo(path);
    if (!info) { hardNavigate(path, fragment); return; }
    chunkFor(path).then(function (chunk) {
      var region = chunk ? chunk[path] : null;
      if (region == null) { hardNavigate(path, fragment); return; }
      content.innerHTML = region;                 // nav (with active highlight) + breadcrumb + body travel with it
      content.setAttribute("data-path", path);
      currentPath = path;
      if (info.title) document.title = info.title;
      // An innerHTML swap discards every listener the enhancement layer attached and never executes an injected
      // <script>, so any specscribe.js block that enhances page content must be re-run against the fresh markup —
      // otherwise it works on the entry page and is silently dead for the rest of the session (the same failure
      // mode HostRenderExceptions records for Mermaid). One generic signal, one listener per enhancement block;
      // blocks guard their own idempotence. Currently consumed by the Story 20.2 sunburst explorer.
      notifyContentSwapped();
      if (push) {
        history.pushState({ path: path, fragment: fragment || "" }, "",
          basePrefix + path + (fragment ? "#" + fragment : ""));
      }
      scrollToFragment(fragment);
    }).catch(function () { hardNavigate(path, fragment); });
  }

  document.addEventListener("click", function (e) {
    var t = e.target;
    if (!t || !t.closest) return;

    // Nav toggle: the static page's inline toggle script is intentionally stripped from swapped regions (an
    // innerHTML'd <script> never runs), so the same collapse behavior is delegated here — works across every swap.
    var toggle = t.closest(".site-nav-toggle");
    if (toggle) {
      var nav = toggle.closest(".site-nav");
      if (nav) toggle.setAttribute("aria-expanded", String(nav.classList.toggle("site-nav-open")));
      return;
    }

    var a = t.closest("a[href]");
    if (!a) return;
    // Respect explicit new-tab / download / modified-click intents — don't hijack them.
    if (a.target && a.target !== "_self") return;
    if (a.hasAttribute("download")) return;
    if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;

    var href = a.getAttribute("href") || "";
    if (href === "" || href.charAt(0) === "#") return;          // same-page anchor -> native scroll
    if (/^[a-z][a-z0-9+.-]*:/i.test(href)) return;              // absolute scheme (https:, mailto:) -> native

    // Intercept only when the data layer is loaded AND the resolved target is a known SPA page; otherwise let the
    // browser load the static file (works whether the manifest is absent or the target is a non-page asset).
    if (!manifest) return;
    var target = href, fragment = "";
    var hash = target.indexOf("#");
    if (hash >= 0) { fragment = target.slice(hash + 1); target = target.slice(0, hash); }
    var resolved = resolve(target, currentPath);
    if (!pageInfo(resolved)) return;

    e.preventDefault();
    if (resolved === currentPath) { scrollToFragment(fragment); return; }
    navigate(resolved, fragment, true);
  });

  window.addEventListener("popstate", function (e) {
    var state = e.state;
    if (state && state.path) { navigate(state.path, state.fragment || "", false); return; }
    // No (or a foreign) history state — e.g. the user navigated in from outside this SPA's own history entries,
    // or went back past the initial replaceState. Derive the target from the actual address bar rather than
    // blindly resetting to the dashboard, so the visible content never disagrees with the URL. [Story 6.7 review]
    var fromUrl = location.pathname.indexOf(basePrefix) === 0
      ? location.pathname.slice(basePrefix.length)
      : "";
    var target = fromUrl && pageInfo(fromUrl) ? fromUrl : (manifest ? manifest.entry : currentPath);
    navigate(target, location.hash ? location.hash.slice(1) : "", false);
  });

  // Load the data layer. Until it resolves the inlined dashboard is fully readable and its links navigate to the
  // static site, so a failed or blocked fetch (e.g. file://) degrades to static navigation rather than breaking.
  fetchJson(basePrefix + "spa/manifest.json").then(function (m) {
    manifest = m;
    history.replaceState({ path: currentPath, fragment: "" }, "", location.href);
    startLiveStamp();
  }).catch(function (err) {
    if (window.console) console.warn("[specscribe-spa] data layer unavailable; using static navigation", err);
  });

  // ── Story 22.6 AC #5: the "Quiet Stamp" ──────────────────────────────────────────────────────────────────
  // Reports the delta channel's state as WORDS, never by color and never by motion. The element is already in
  // the server-rendered shell reading "Live updates: unavailable", so this only ever rewrites its textContent —
  // no element is inserted or removed, which is what makes an update produce no layout shift.
  //
  // The channel here is the watch-mode SIDECAR (spa/delta.json), polled — AD-8's "static HTML may hydrate via
  // URL hash plus sidecar polling" clause. There is no socket and no server: a one-shot `generate` writes no
  // sidecar at all, so the 404 below is the NORMAL steady state for a statically-served site and the stamp
  // correctly stays "unavailable" rather than claiming a channel that does not exist.
  function startLiveStamp() {
    var stamp = document.getElementById("spa-live-stamp");
    if (!stamp) return;

    var lastSequence = -1;
    // Bumped on every poll and captured per-call (code review, Story 22.6): setInterval does not wait for the
    // previous fetch to settle, so two overlapping requests can resolve OUT OF ORDER. Without this, a late
    // response for an OLDER poll could overwrite a newer display (or reset lastSequence backwards) after a
    // fresher one already landed. Only the response belonging to the MOST RECENT poll is ever allowed to touch
    // the stamp or lastSequence.
    var pollId = 0;
    function poll() {
      var thisPoll = ++pollId;
      // Deliberately NOT versioned(): the sidecar changes WITHIN a build, so the build's cache-bust token would
      // pin it to the first response forever. cache:"no-store" is the correct freshness control here.
      fetch(basePrefix + "spa/delta.json", { cache: "no-store" }).then(function (r) {
        if (!r.ok) throw new Error("no delta channel");
        return r.json();
      }).then(function (d) {
        if (thisPoll !== pollId) return;           // a newer poll already started; this response is stale
        if (d.sequence === lastSequence) return;   // nothing new since the last poll; leave the stamp alone
        lastSequence = d.sequence;
        var when = new Date(d.generatedAt);
        var time = isNaN(when.getTime())
          ? ""
          : " · updated " + String(when.getHours()).padStart(2, "0") + ":" + String(when.getMinutes()).padStart(2, "0");
        // The full marker is reported honestly rather than dressed up as an ordinary update: it means the
        // consumer must refetch, and a reader watching the stamp should see that a full reload happened.
        stamp.textContent = "Live updates: connected" + time + (d.full ? " · full refresh" : "");
      }).catch(function () {
        if (thisPoll !== pollId) return;           // a newer poll already started; this failure is stale too
        // Any failure — no sidecar, a torn read, a parse error — is reported as the one honest state. Never
        // silently leave a stale "connected" up: a stamp that lies about being live is worse than no stamp.
        //
        // lastSequence is reset here too (code review, Story 22.6): without this, a TRANSIENT failure between
        // two polls that would otherwise report the SAME sequence number wedges the stamp on "unavailable"
        // forever — the next successful poll sees d.sequence === lastSequence and short-circuits before ever
        // re-reporting "connected", which is the exact dishonesty this catch block exists to prevent.
        lastSequence = -1;
        stamp.textContent = "Live updates: unavailable";
      });
    }

    poll();
    setInterval(poll, 2000);
  }
})();
