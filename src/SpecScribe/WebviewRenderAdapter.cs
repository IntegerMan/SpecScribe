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
        sb.Append(HtmlRenderAdapter.Shared.RenderBreadcrumb(page.OutputRelativePath, page.Breadcrumb));
        sb.Append(page.BodyHtml);
        return sb.ToString();
    }

    /// <summary>Wraps an already-rendered content region in the webview document shell: CSP meta (script-src
    /// nonce-locked; style-src 'unsafe-inline' for the render's inline style attributes — ADR 0005's measured,
    /// accepted posture), inlined stylesheet, the surface container stamped with the page's output-relative path
    /// (the bridge resolves relative links against it), and the nonce'd bridge script. <c>__CSP_SOURCE__</c> /
    /// <c>__NONCE__</c> are deliberately left for the shim — the two-value seam that keeps the shim dumb.</summary>
    public string WrapDocument(PageView page, string contentHtml) => DocumentTemplate
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
        .Replace("__CONTENT__", contentHtml);

    // The shell around the swappable region. Kept as one template so the CSP policy, container id, and bridge
    // script — the contract the extension's shim relies on — are reviewable in one place. The bridge script is
    // information-free by policy (progressive enhancement): it navigates and patches, it never adds content.
    private const string DocumentTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8" />
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; base-uri 'none'; form-action 'none'; img-src __CSP_SOURCE__ data: https:; style-src 'unsafe-inline' __CSP_SOURCE__; script-src 'nonce-__NONCE__'; font-src __CSP_SOURCE__ data:;" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>__TITLE__</title>
        <style>__CSS__</style>
        <style>__THEME_CSS__</style>
        </head>
        <body>
        <div class="ss-webview-toolbar">
        <span class="ss-webview-toolbar-label">SpecScribe</span>
        <button type="button" class="ss-helper-btn" data-ss-label="a code-review prompt" data-ss-prompt="__HELPER_PROMPT__">Copy code-review prompt</button>
        </div>
        <div id="specscribe-surface" data-path="__PATH__">
        __CONTENT__
        </div>
        <script nonce="__NONCE__">
        (function () {
          var vscode = (typeof acquireVsCodeApi === 'function') ? acquireVsCodeApi() : null;
          var surface = document.getElementById('specscribe-surface');

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

            // Nav toggle: the HTML surface's inline toggle script is CSP-blocked here, so the same collapse
            // behavior is delegated (narrow editor panels are the norm, so this matters more, not less).
            var toggle = t.closest('.site-nav-toggle');
            if (toggle) {
              var nav = toggle.closest('.site-nav');
              if (nav) toggle.setAttribute('aria-expanded', String(nav.classList.toggle('site-nav-open')));
              return;
            }

            var a = t.closest('a[href]');
            if (!a || !vscode) return;
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

          // Host push (AD-8): both in-webview navigation and live refresh arrive as one message shape and are
          // swapped IN PLACE — the panel document (and its one nonce) is set exactly once, never re-created.
          window.addEventListener('message', function (e) {
            var m = e.data || {};
            if (m.type !== 'update' || typeof m.html !== 'string' || !surface) return;
            surface.innerHTML = m.html;
            if (m.path) surface.setAttribute('data-path', m.path);
            if (m.reason === 'navigate') {
              var el = m.fragment ? document.getElementById(m.fragment) : null;
              if (el) { el.scrollIntoView(); } else { window.scrollTo(0, 0); }
            }
            // reason "refresh" deliberately leaves scroll alone: the swap preserves position, so the user's
            // reading context survives a source edit (AC #3 "refreshes in place without full panel reset").
          });

          if (vscode) vscode.postMessage({ type: 'ready' });
        })();
        </script>
        </body>
        </html>
        """;
}
