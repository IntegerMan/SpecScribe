using System.Net;

namespace SpecScribe.WebviewSpike;

/// <summary>Wraps a shared section-body string (the exact output of <c>HtmlRenderAdapter.RenderDashboardBody</c> /
/// <c>RenderEpicsIndexBody</c>) in a self-contained, webview-safe HTML document: a strict Content-Security-Policy,
/// the site stylesheet inlined, and a tiny nonce'd bridge script for surface-toggle + live-push. This is the
/// concrete <b>"C# renders the webview HTML"</b> seam ADR 0005 evaluates against the epic's "JSON export the TS
/// renders" alternative.
/// <para>Two — and only two — host-runtime values are left as placeholders for the thin TS shim to substitute
/// immediately before <c>webview.html = html</c>: <c>__CSP_SOURCE__</c> (the webview's <c>cspSource</c>) and
/// <c>__NONCE__</c> (a per-load script nonce). Everything else — structure, policy, inlined CSS, body — is
/// produced here in C#. That the shim only injects these two opaque strings is the spike's evidence that the shim
/// can stay dumb. [Story 6.3 SPIKE — throwaway]</para></summary>
public static class WebviewShell
{
    private const string Template = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8" />
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src __CSP_SOURCE__ data: https:; style-src 'unsafe-inline' __CSP_SOURCE__; script-src 'nonce-__NONCE__'; font-src __CSP_SOURCE__ data:;" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>__TITLE__</title>
        <style>
        /* --- spike-only webview chrome (a real host-theming pass is Story 6.5) --- */
        body { margin: 0; }
        .ss-spike-bar { position: sticky; top: 0; z-index: 50; display: flex; gap: .5rem; align-items: center;
          padding: .4rem .8rem; background: var(--surface, #12131a); border-bottom: 1px solid var(--border, #2a2c38);
          font: 13px system-ui, sans-serif; }
        .ss-spike-bar button { cursor: pointer; padding: .25rem .7rem; border-radius: 6px; border: 1px solid var(--border, #2a2c38);
          background: transparent; color: inherit; }
        .ss-spike-bar button[aria-current="true"] { background: var(--accent, #6c8cff); color: #fff; border-color: transparent; }
        .ss-spike-note { margin-left: auto; opacity: .6; }
        </style>
        <style>__CSS__</style>
        </head>
        <body>
        <div class="ss-spike-bar" role="tablist" aria-label="SpecScribe spike surfaces">
          <button type="button" data-surface="dashboard" role="tab">Dashboard</button>
          <button type="button" data-surface="epics" role="tab">Epics</button>
          <span class="ss-spike-note">SpecScribe Story 6.3 spike &middot; C#-rendered &middot; read-only</span>
        </div>
        <div id="specscribe-surface" data-surface="__SURFACE__">
        __BODY__
        </div>
        <script nonce="__NONCE__">
        (function () {
          var vscode = (typeof acquireVsCodeApi === 'function') ? acquireVsCodeApi() : null;
          var surfaceEl = document.getElementById('specscribe-surface');
          function markActive(name) {
            document.querySelectorAll('.ss-spike-bar button').forEach(function (b) {
              b.setAttribute('aria-current', String(b.getAttribute('data-surface') === name));
            });
          }
          markActive(surfaceEl ? surfaceEl.getAttribute('data-surface') : 'dashboard');
          document.querySelectorAll('.ss-spike-bar button').forEach(function (b) {
            b.addEventListener('click', function () {
              if (vscode) vscode.postMessage({ type: 'switch', surface: b.getAttribute('data-surface') });
            });
          });
          // Live-push (AD-8): the host posts the new surface body; swap it IN PLACE — no panel reset.
          window.addEventListener('message', function (e) {
            var m = e.data || {};
            if (m.type === 'update' && typeof m.html === 'string' && surfaceEl) {
              surfaceEl.innerHTML = m.html;
              if (m.surface) { surfaceEl.setAttribute('data-surface', m.surface); markActive(m.surface); }
            }
          });
          if (vscode) vscode.postMessage({ type: 'ready' });
        })();
        </script>
        </body>
        </html>
        """;

    public static string Wrap(string title, string bodyHtml, string css, string surfaceId) => Template
        .Replace("__TITLE__", WebUtility.HtmlEncode(title))
        .Replace("__SURFACE__", surfaceId)
        .Replace("__CSS__", css)
        .Replace("__BODY__", bodyHtml);
    // __CSP_SOURCE__ and __NONCE__ are intentionally NOT replaced here — the TS shim injects them at runtime.
}
