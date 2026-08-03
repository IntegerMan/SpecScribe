using System.Text;

namespace SpecScribe;

/// <summary>The SECOND concrete <see cref="IRenderAdapter"/> — the VS Code webview surface ADR 0005 ratified
/// ("C# renders the webview HTML"; the epic's literal "JSON export the TS webview renders" was rejected). It
/// consumes the exact same host-neutral <see cref="PageView"/>s the HTML surface renders (built by the templaters'
/// <c>Build*Page</c> split) and emits a self-contained, CSP-safe webview document: strict Content-Security-Policy,
/// the production stylesheet inlined, the shared nav/breadcrumb chrome, the page body verbatim, and one nonce'd
/// bridge script (navigation + live-push + nav toggle). The thin TS shim substitutes exactly two host-runtime
/// placeholders — <c>__CSP_SOURCE__</c> and <c>__NONCE__</c> — and nothing else; every byte of visible content is
/// produced here (AD-1/AD-2: the extension re-parses nothing and scrapes no generated site). [Story 6.4]
/// <para><b>Sanctioned divergences from the HTML surface</b> (each registered in
/// <see cref="HostRenderExceptions.Registry"/>, per AC #4): no <c>&lt;link&gt;</c> stylesheet (inlined for CSP),
/// no <c>specscribe.js</c> (the enhancement script is convenience-only by policy — the body reaches the same
/// information without it), and no Mermaid init (no script may load under the CSP; the epics roadmap degrades to
/// readable preformatted text — ADR 0005's accepted fallback). Inline SVG charts survive unchanged — the spike
/// measured 107+18 of them injecting cleanly.</para></summary>
public sealed class WebviewRenderAdapter : IRenderAdapter
{
    /// <summary>The single shared instance — stateless, like <see cref="HtmlRenderAdapter.Shared"/>.</summary>
    public static readonly WebviewRenderAdapter Shared = new();

    public string Id => "webview";

    /// <summary>The production stylesheet, inlined once from the same embedded resource
    /// <see cref="SiteGenerator"/> copies to the site root — the webview ships the EXACT site CSS, then layers the
    /// Story 6.5 <see cref="ThemeBridge"/> on top (a second inline sheet) to map host chrome variables. Loaded
    /// lazily so merely referencing the adapter never does resource I/O.</summary>
    private static readonly Lazy<string> Stylesheet = new(() => ReadEmbedded("SpecScribe.assets.specscribe.css"));

    /// <summary>The Story 6.5 host-theme bridge, inlined into a SECOND <c>&lt;style&gt;</c> block right after the
    /// production stylesheet so its <c>.vscode-*</c>-scoped rules win the cascade. It maps VS Code host variables
    /// onto SpecScribe's chrome/container tokens and contrast-tunes the status/insight accents per theme (AD-7).
    /// It is inert outside a webview (those body classes never match in a browser), which is exactly why the
    /// generated HTML surface — which never loads this — stays byte-identical.</summary>
    private static readonly Lazy<string> ThemeBridge = new(() => ReadEmbedded("SpecScribe.assets.specscribe-webview-theme.css"));

    /// <summary>The vendored plotly.js hierarchy/graph engine (ADR 0012 / ADR 0030), inlined into the shell under
    /// the document nonce. <b>Inlined, not <c>&lt;script src&gt;</c>:</b> `localResourceRoots` is empty by design,
    /// so nothing loads from disk — an inline nonce'd block is what satisfies `script-src 'nonce-…'` without
    /// touching the policy string, which is exactly the shape ADR 0032 §3 names. ~1.22 MB, paid once per document
    /// (the shell is built once; navigation swaps only the region), never once per surface. [ADR 0036 §1]</summary>
    private static readonly Lazy<string> ChartEngine = new(() => ReadEmbedded("SpecScribe.assets.plotly-hierarchy.min.js"));

    /// <summary>The production <c>specscribe.js</c> — the SAME file the static site ships, not a webview fork.
    ///
    /// <para>ADR 0036 §2 forbids forking the mount logic, and this is where that is honored: shipping the whole
    /// script means the Hierarchy Explorer and the Story 24.2 relationship graph mount here through the identical
    /// code path they use in a browser, so the two cannot drift. It is safe to ship WHOLE rather than trimmed for
    /// a checked reason: the file registers no nav-toggle handler (the bridge below owns that, and a second one
    /// would double-toggle), and it already listens for <c>specscribe:content-swapped</c> — the re-init seam
    /// Story 20.2 built for precisely this kind of <c>innerHTML</c> swap.</para></summary>
    private static readonly Lazy<string> AppScript = new(() => ReadEmbedded("SpecScribe.assets.specscribe.js"));

    /// <summary>The webview CSP policy, verbatim and in ONE place. Shared with
    /// <see cref="SettingsFormTemplater"/>, which is the second document this host renders (ADR 0037): two copies
    /// of a security policy string is how one of them quietly becomes weaker than the other. Still carries the
    /// <c>__CSP_SOURCE__</c>/<c>__NONCE__</c> seam for the shim to substitute.
    /// <para>⚠️ <c>form-action 'none'</c> is why the settings form emits no <c>&lt;form&gt;</c> element — a submit
    /// would be silently blocked. See <see cref="SettingsFormTemplater"/>.</para></summary>
    internal const string CspPolicy =
        "default-src 'none'; base-uri 'none'; form-action 'none'; img-src __CSP_SOURCE__ data: https:; "
        + "style-src 'unsafe-inline' __CSP_SOURCE__; script-src 'nonce-__NONCE__'; font-src __CSP_SOURCE__ data:;";

    /// <summary>The production stylesheet and the host-theme bridge, for the settings form's own shell — so both
    /// webview documents inline the SAME two sheets rather than the second growing its own styling.</summary>
    internal static string StylesheetCss => Stylesheet.Value;

    internal static string ThemeBridgeCss => ThemeBridge.Value;

    private static string ReadEmbedded(string resourceName)
    {
        using var stream = typeof(WebviewRenderAdapter).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded {resourceName} not found on the SpecScribe assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Renders one page as a full standalone webview document — the artifact the shim assigns to
    /// <c>webview.html</c> (after placeholder substitution). Equivalent to
    /// <c>WrapDocument(page, RenderContent(page))</c>; split so callers that reference-linkify the content region
    /// first (see <see cref="SiteGenerator.RenderWebviewSurfaces"/>) can wrap the finished region without the
    /// linkifier ever walking the shell's CSS/script text.</summary>
    public RenderedArtifact Render(PageView page) =>
        new(page.OutputRelativePath, WrapDocument(page, RenderContent(page)));

    /// <summary>Renders the SWAPPABLE content region for one page: the shared nav markup (no inline toggle
    /// script — the CSP would block it; the bridge script owns the toggle), the breadcrumb, and the page body
    /// verbatim. This region is what in-webview navigation and live-push replace via
    /// <c>#specscribe-surface.innerHTML</c>, so each surface carries its OWN active-nav highlight and breadcrumb
    /// trail — the 6.1 interaction semantics travel with the content, never the shell. The body is byte-identical
    /// to the HTML surface's (same view models, same body renderers), which is what makes the section-fact parity
    /// checks meaningful rather than vacuous.</summary>
    public string RenderContent(PageView page)
    {
        var sb = new StringBuilder();
        sb.Append(HtmlRenderAdapter.Shared.RenderNavMarkup(page.Nav));
        sb.Append(HtmlRenderAdapter.Shared.RenderWayfinding(page.OutputRelativePath, page.Breadcrumb, page.Pager));
        // The body rides VERBATIM, data islands included. [ADR 0036]
        //
        // This used to call StripDataIslands. The strip was never a CSP matter — a <script type="application/json">
        // block is data, never executed, so script-src does not apply and ADR 0032 §2 explicitly PERMITS inert
        // islands inside the region. It existed for DEAD WEIGHT: the webview shipped no engine and no
        // specscribe.js, so nothing here could ever read an island, and Story 20.9 measured 4.5 MB of unreadable
        // payload riding into a document the editor holds in memory.
        //
        // That rationale expired the moment WrapDocument started supplying the chart engine and the mount code as
        // nonce'd chrome (ADR 0036 §1) — which is exactly what ADR 0032 §2 means by "replaced by whichever shell
        // consumes the region". The island is now LIVE DATA on this surface, read by getElementById the same way
        // it is on the static site. What was dead weight is now the payload.
        //
        // The region still carries no EXECUTABLE script, which is the invariant that actually matters and the one
        // the surface tests now pin. [ADR 0036 §3; supersedes Story 20.2 / 20.9's strip]
        sb.Append(page.BodyHtml);
        return sb.ToString();
    }

    // REMOVED BY ADR 0036: `StripDataIslands` and its `JsonDataIsland` regex.
    //
    // Both existed to keep unreadable payload out of a surface that shipped no engine. Now that WrapDocument
    // supplies the engine and the mount code as nonce'd chrome, an island is live data on both the PageView path
    // (RenderContent) and the captured-page path (SiteGenerator.AppendLongTailSurfaces) — so neither has a caller,
    // and a public helper that no longer describes what this adapter does is worse than no helper. Deleted rather
    // than left `[Obsolete]` because the behaviour it named is retired, not deprecated.

    /// <summary>Wraps an already-rendered content region in the webview document shell: CSP meta (script-src
    /// nonce-locked; style-src 'unsafe-inline' for the render's inline style attributes — ADR 0005's measured,
    /// accepted posture), inlined stylesheet, the surface container stamped with the page's output-relative path
    /// (the bridge resolves relative links against it), and the nonce'd bridge script. <c>__CSP_SOURCE__</c> /
    /// <c>__NONCE__</c> are deliberately left for the shim — the two-value seam that keeps the shim dumb.</summary>
    public string WrapDocument(PageView page, string contentHtml, string? sourcePath = null) => DocumentTemplate
        // Substituted from the ONE shared constant so this document and the settings form can never carry two
        // different policies. [ADR 0037]
        .Replace("__CSP__", CspPolicy)
        .Replace("__TITLE__", PathUtil.Html(page.Title))
        .Replace("__PATH__", PathUtil.Html(PathUtil.NormalizeSlashes(page.OutputRelativePath)))
        .Replace("__CSS__", Stylesheet.Value)
        // The theme bridge is inlined AS-IS into its own <style> after __CSS__; a second replace (not string
        // concatenation into __CSS__) keeps the two sheets separable and the base CSS untouched.
        .Replace("__THEME_CSS__", ThemeBridge.Value)
        // The read-only helper prompt rides in a data attribute the bridge script reads on click (AC #2). Attribute-
        // escaped so a project title with quotes can't break out of the attribute; the value is only ever copied to
        // the clipboard by the host, never executed or written anywhere.
        .Replace("__HELPER_PROMPT__", PathUtil.Html(WebviewHelpers.CodeReviewPrompt(page.Nav.SiteTitle)))
        // The repo-relative source artifact this surface was rendered from (Story 6.10 reveal-source). Rides in the
        // surface container's data-source; the bridge posts it as `revealSource` on the toolbar button and toggles
        // that button's visibility on it (empty → hidden, e.g. the dashboard). Attribute-escaped like __PATH__; the
        // host only ever OPENS it read-only, never writes it.
        .Replace("__SOURCE__", PathUtil.Html(PathUtil.NormalizeSlashes(sourcePath ?? "")))
        // The two chrome scripts ADR 0036 §1 moved onto this shell. Substituted BEFORE __CONTENT__ so the region —
        // the one part of this document that is not ours — is still inserted last and therefore never re-scanned
        // for placeholder tokens. That ordering is the same reason the shim lifts the content out before it
        // substitutes __NONCE__/__CSP_SOURCE__: a region must never be able to forge a shell token.
        .Replace("__ENGINE_JS__", ChartEngine.Value)
        .Replace("__APP_JS__", AppScript.Value)
        .Replace("__CONTENT__", contentHtml);

    // The shell around the swappable region. Kept as one template so the CSP policy, container id, and bridge
    // script — the contract the extension's shim relies on — are reviewable in one place. The bridge script is
    // information-free by policy (progressive enhancement): it navigates and patches, it never adds content.
    private const string DocumentTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8" />
        <meta http-equiv="Content-Security-Policy" content="__CSP__" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>__TITLE__</title>
        <style>__CSS__</style>
        <style>__THEME_CSS__</style>
        </head>
        <body>
        <div class="ss-webview-toolbar">
        <span class="ss-webview-toolbar-label">SpecScribe</span>
        <button type="button" class="ss-reveal-src-btn" title="Open the markdown file this view was rendered from (read-only)" hidden>Open source</button>
        <button type="button" class="ss-helper-btn" data-ss-label="a code-review prompt" data-ss-prompt="__HELPER_PROMPT__">Copy code-review prompt</button>
        <span class="spa-live-stamp" id="spa-live-stamp" role="status" aria-live="polite">Live updates: unavailable</span>
        </div>
        <div class="ss-webview-status" id="ss-webview-status" hidden>
        <span class="ss-webview-status-text" id="ss-webview-status-text"></span>
        <span class="ss-webview-status-actions" id="ss-webview-status-actions"></span>
        </div>
        <div id="specscribe-surface" data-path="__PATH__" data-source="__SOURCE__">
        __CONTENT__
        </div>
        <script nonce="__NONCE__">
        (function () {
          var vscode = (typeof acquireVsCodeApi === 'function') ? acquireVsCodeApi() : null;
          var surface = document.getElementById('specscribe-surface');
          var revealBtn = document.querySelector('.ss-reveal-src-btn');
          var hostStatus = document.getElementById('ss-webview-status');
          var hostStatusText = document.getElementById('ss-webview-status-text');
          var hostStatusActions = document.getElementById('ss-webview-status-actions');

          // The current surface's repo-relative source artifact (Story 6.10). Read off #specscribe-surface's
          // data-source, which the entry document stamps and every in-place `update` swap refreshes.
          function currentSource() { return surface ? (surface.getAttribute('data-source') || '') : ''; }
          // Show the "Open source" toolbar button only when the current surface HAS a source (the dashboard has
          // none). Called on first paint and after every content swap so the button always reflects the view.
          function syncRevealBtn() { if (revealBtn) revealBtn.hidden = !currentSource(); }
          syncRevealBtn();

          function renderHostStatus(msg) {
            if (!hostStatus || !hostStatusText || !hostStatusActions) return;
            var text = (typeof msg.text === 'string') ? msg.text.trim() : '';
            if (!text) {
              hostStatus.hidden = true;
              hostStatus.removeAttribute('data-level');
              hostStatusText.textContent = '';
              hostStatusActions.textContent = '';
              return;
            }
            hostStatus.hidden = false;
            hostStatus.setAttribute('data-level', typeof msg.level === 'string' ? msg.level : 'info');
            hostStatusText.textContent = text;
            hostStatusActions.textContent = '';
            var actions = Array.isArray(msg.actions) ? msg.actions : [];
            for (var i = 0; i < actions.length; i++) {
              var action = actions[i];
              if (!action || typeof action.id !== 'string' || typeof action.label !== 'string') continue;
              var btn = document.createElement('button');
              btn.type = 'button';
              btn.className = 'ss-host-action';
              btn.setAttribute('data-action', action.id);
              btn.textContent = action.label;
              hostStatusActions.appendChild(btn);
            }
          }

          // Resolves a rendered relative href (e.g. "story-1-1.html", "../index.html", "epics.html#epic-2")
          // against the CURRENT surface's output-relative path — a webview is not a browser tab, so anchor
          // clicks navigate nowhere by default and the document base never changes across content swaps.
          function resolve(href, basePath) {
            var baseDir = basePath.indexOf('/') >= 0 ? basePath.slice(0, basePath.lastIndexOf('/') + 1) : '';
            var parts = (baseDir + href).split('/');
            var out = [];
            for (var i = 0; i < parts.length; i++) {
              if (parts[i] === '' || parts[i] === '.') continue;
              if (parts[i] === '..') { out.pop(); continue; }
              out.push(parts[i]);
            }
            return out.join('/');
          }

          document.addEventListener('click', function (e) {
            var t = e.target;
            if (!t || !t.closest) return;

            // Read-only helper (AC #2): hand the pre-generated prompt to the host, which copies it to the
            // clipboard. This branch NEVER writes an artifact or mutates state — it posts text and stops. Any use
            // of the copied prompt is a separate, explicit user action outside the webview.
            var helper = t.closest('.ss-helper-btn');
            if (helper) {
              if (vscode) vscode.postMessage({
                type: 'copyHelperText',
                text: helper.getAttribute('data-ss-prompt') || '',
                label: helper.getAttribute('data-ss-label') || 'text'
              });
              return;
            }

            var hostAction = t.closest('.ss-host-action');
            if (hostAction) {
              if (vscode) vscode.postMessage({
                type: 'hostAction',
                action: hostAction.getAttribute('data-action') || ''
              });
              return;
            }

            // Reveal source (AC #1): open the markdown this surface was rendered from, read-only. Posts the
            // surface's repo-relative data-source; the shim joins it to the workspace folder and calls
            // showTextDocument. NEVER writes — it hands over a path and stops.
            var reveal = t.closest('.ss-reveal-src-btn');
            if (reveal) {
              var src = currentSource();
              if (vscode && src) vscode.postMessage({ type: 'revealSource', path: src });
              return;
            }

            // Nav toggle: the HTML surface's inline toggle script is CSP-blocked here, so the same collapse
            // behavior is delegated (narrow editor panels are the norm, so this matters more, not less). Keyboard
            // and focus parity with HtmlRenderAdapter.NavToggleScript: opening focuses the first nav link;
            // Escape-close + focus-return is wired via the document-level keydown listener below (deferred item,
            // Story 6.4 review).
            var toggle = t.closest('.site-nav-toggle');
            if (toggle) {
              var nav = toggle.closest('.site-nav');
              if (nav) {
                var opening = !nav.classList.contains('site-nav-open');
                nav.classList.toggle('site-nav-open');
                toggle.setAttribute('aria-expanded', String(opening));
                if (opening) {
                  var firstLink = nav.querySelector('.site-nav-links a');
                  if (firstLink) firstLink.focus();
                }
              }
              return;
            }

            var a = t.closest('a[href]');
            if (!a || !vscode) return;

            // AC #2 structured-link seam — INERT until Story 7.2 emits these attributes. A code citation that
            // carries data-code-path (+ optional 1-based data-line) is re-targeted to the editor via the SAME
            // line-capable revealSource message this story delivers; the HTML surface keeps its portal/GitHub href
            // (data-* are additive, webview-intercepted, never alter the static site). This branch is what
            // "rides Story 6.10's link seam" means — recognition here, emission in 7.1/7.2.
            var codePath = a.getAttribute('data-code-path');
            if (codePath) {
              e.preventDefault();
              var lineAttr = a.getAttribute('data-line');
              var lineNum = lineAttr ? parseInt(lineAttr, 10) : 0;
              var msg = { type: 'revealSource', path: codePath };
              if (lineNum > 0) msg.line = lineNum;
              vscode.postMessage(msg);
              return;
            }

            // AC #2 command-staging extension point — DOCUMENTED here; owning story is 8.5 (native R4.3). A future
            // next-step-command surface emits an element carrying its command text; a branch like the one above
            // would post `{ type: 'stageCommand', command: <text> }`, and the shim's handler would reuse the
            // existing `stageTerminalCommand` primitive (createTerminal + sendText(command, /* execute: */ false))
            // to STAGE it at a prompt — the user presses Enter, SpecScribe never does (AD-6/ADR 0003). Story 8.5
            // deliberately does NOT build that handler or emit the control; the HTML/copy Next Steps surface
            // designs against this known shape rather than retrofitting it.

            var href = a.getAttribute('href') || '';
            if (href.charAt(0) === '#') return; // same-page anchor: native fragment scroll still works

            e.preventDefault();
            if (/^[a-z][a-z0-9+.-]*:/i.test(href)) {
              // Absolute scheme (https:, mailto:, …): open OUTSIDE the webview via the shim.
              vscode.postMessage({ type: 'openExternal', href: href });
              return;
            }
            var target = href, fragment = '';
            var hash = target.indexOf('#');
            if (hash >= 0) { fragment = target.slice(hash + 1); target = target.slice(0, hash); }
            var current = surface ? (surface.getAttribute('data-path') || '') : '';
            vscode.postMessage({ type: 'navigate', target: resolve(target, current), fragment: fragment });
          });

          // Nav-toggle Escape-close + focus-return (deferred item, Story 6.4 review): a document-level listener
          // (not per-nav, since content swaps replace the nav element without re-running this script) closes
          // whichever `.site-nav` is currently open and returns focus to its toggle button.
          document.addEventListener('keydown', function (e) {
            if (e.key !== 'Escape') return;
            var openNav = document.querySelector('.site-nav.site-nav-open');
            if (!openNav) return;
            e.preventDefault();
            openNav.classList.remove('site-nav-open');
            var toggleBtn = openNav.querySelector('.site-nav-toggle');
            if (toggleBtn) { toggleBtn.setAttribute('aria-expanded', 'false'); toggleBtn.focus(); }
          });

          // Host push (AD-8): both in-webview navigation and live refresh arrive as one message shape and are
          // swapped IN PLACE — the panel document (and its one nonce) is set exactly once, never re-created.
          // Story 22.6 AC #5 — the "Quiet Stamp", webview half. State as WORDS, never by color and never by
          // motion; the element is already in the server-rendered toolbar reading "Live updates: unavailable", so
          // this only ever rewrites textContent — nothing is inserted or removed, so an update shifts no layout.
          // The host posts this whenever it applies a payload, because the host — not this script — is the side
          // that owns the `--serve --serve-delta` connection and knows whether it is alive.
          var liveStamp = document.getElementById('spa-live-stamp');
          window.addEventListener('message', function (e) {
            var m = e.data || {};
            if (m.type === 'liveStatus') {
              if (liveStamp && typeof m.text === 'string') liveStamp.textContent = m.text;
              return;
            }
            if (m.type === 'hostStatus') {
              renderHostStatus(m);
              return;
            }
            if (m.type !== 'update' || typeof m.html !== 'string' || !surface) return;
            surface.innerHTML = m.html;
            if (m.path) surface.setAttribute('data-path', m.path);
            // Reflect the swapped-in surface's source and show/hide the reveal button (Story 6.10). Set
            // unconditionally from m.source so navigating to a source-less surface (the dashboard) clears a stale
            // value and hides the button.
            surface.setAttribute('data-source', m.source || '');
            syncRevealBtn();
            // Re-mount the charts in the swapped-in region (ADR 0036 §1). `innerHTML` does not execute scripts —
            // which is fine, because the engine and specscribe.js live in the SHELL and survive every swap — but
            // it does mean the new region's chart hosts have never been visited. This is the seam Story 20.2
            // already built for exactly this: specscribe.js listens for `specscribe:content-swapped` and re-runs
            // both `initHierarchyExplorers` and `initRelationshipGraphs` over `detail.root`. Mounts are
            // idempotent — an already-mounted host carries `data-hierarchy-ready` and is skipped — so a
            // double-dispatch costs nothing.
            //
            // Dispatched AFTER data-path/data-source are refreshed so anything the mount reads off the container
            // sees the new surface's values, never the outgoing one's.
            try {
              document.dispatchEvent(new CustomEvent('specscribe:content-swapped', { detail: { root: surface } }));
            } catch (err) {
              // A charting failure must never cost the reader the content that already painted. The region is
              // fully rendered by this point; the twin below each chart carries the same information.
            }
            if (m.reason === 'navigate') {
              var el = m.fragment ? document.getElementById(m.fragment) : null;
              if (el) { el.scrollIntoView(); } else { window.scrollTo(0, 0); }
            }
            // reason "refresh" deliberately leaves scroll alone: the swap preserves position, so the user's
            // reading context survives a source edit (AC #3 "refreshes in place without full panel reset").
          });

          // Claim the shared chart-navigation seam (ADR 0036 §2). A Plotly sector is not an anchor, so chart
          // activation navigates programmatically and the delegated `a[href]` listener above can never see it —
          // it would assign `location.href` and try to navigate the PANEL to a relative path that is not a
          // webview resource (`localResourceRoots` is empty), losing this bridge, the inlined stylesheet and the
          // engine with it. Installing the hook routes those activations through the exact same resolve +
          // postMessage path an anchor click takes, so the two can never drift.
          //
          // Defined BEFORE the engine and specscribe.js are parsed — they are emitted after this block — so the
          // seam is in place the first time any chart mounts.
          window.__specscribeNavigate = function (href) {
            if (!href || !vscode) return;
            if (/^[a-z][a-z0-9+.-]*:/i.test(href)) { vscode.postMessage({ type: 'openExternal', href: href }); return; }
            var target = href, fragment = '';
            var hash = target.indexOf('#');
            if (hash >= 0) { fragment = target.slice(hash + 1); target = target.slice(0, hash); }
            // A pure same-page fragment (the drill-scope links) must scroll, not round-trip to the host.
            if (target === '') {
              var el = fragment ? document.getElementById(fragment) : null;
              if (el) el.scrollIntoView();
              return;
            }
            var current = surface ? (surface.getAttribute('data-path') || '') : '';
            vscode.postMessage({ type: 'navigate', target: resolve(target, current), fragment: fragment });
          };

          if (vscode) vscode.postMessage({ type: 'ready' });
        })();
        </script>
        <!-- ── The chrome scripts (ADR 0036 §1) ──────────────────────────────────────────────────────────────
             Both carry the document nonce, so `script-src 'nonce-…'` admits them with NO change to the policy
             string. They sit at the END of the body, after the region has parsed, which is what lets
             specscribe.js mount on first paint without the `defer` the static site uses.

             ORDER IS LOAD-BEARING: the engine defines the global specscribe.js mounts against, so it must parse
             first. Emitted after the bridge script above so the bridge's message listener is registered before
             either of these can throw — a mount failure must never cost the panel its navigation. -->
        <script nonce="__NONCE__">__ENGINE_JS__</script>
        <script nonce="__NONCE__">__APP_JS__</script>
        </body>
        </html>
        """;
}
