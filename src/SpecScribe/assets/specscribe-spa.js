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

  var manifest = null;          // { siteTitle, entry, pages: { path: { title, chunk } } }
  var chunkCache = {};          // chunkFile -> { path: regionHtml }
  var currentPath = content.getAttribute("data-path") || "index.html";

  function fetchJson(url) {
    return fetch(url).then(function (r) {
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
    navigate(manifest ? manifest.entry : currentPath, "", false);
  });

  // Load the data layer. Until it resolves the inlined dashboard is fully readable and its links navigate to the
  // static site, so a failed or blocked fetch (e.g. file://) degrades to static navigation rather than breaking.
  fetchJson(basePrefix + "spa/manifest.json").then(function (m) {
    manifest = m;
    history.replaceState({ path: currentPath, fragment: "" }, "", location.href);
  }).catch(function (err) {
    if (window.console) console.warn("[specscribe-spa] data layer unavailable; using static navigation", err);
  });
})();
