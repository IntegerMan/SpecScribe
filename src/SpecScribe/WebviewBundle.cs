namespace SpecScribe;

/// <summary>One webview-navigable surface: its output-relative path (the navigation key the bridge script's
/// resolved link targets are matched against), its document title (the shim mirrors it onto the panel title), and
/// its rendered content region (nav + breadcrumb + body — what an in-place <c>postMessage</c> swap installs into
/// <c>#specscribe-surface</c>). [Story 6.4]</summary>
public sealed record WebviewSurface(string OutputRelativePath, string Title, string ContentHtml);

/// <summary>Everything one <c>specscribe webview</c> spawn hands the VS Code extension: the full entry document
/// (dashboard, with the <c>__CSP_SOURCE__</c>/<c>__NONCE__</c> host-runtime placeholders still unsubstituted —
/// the shim's only job) plus the complete navigable surface set for instant in-webview navigation and live-push.
/// Shipping every surface per spawn is deliberate: ingest dominates render cost (ADR 0005 measured ~1.8–2.0 s
/// warm, almost all ingest + git), so per-surface spawns would pay the same latency per CLICK; one bundle pays it
/// once per (re)load and navigation is instant thereafter (AC #2 "responsive"). [Story 6.4]</summary>
public sealed record WebviewBundle(
    string SiteTitle,
    string EntryPath,
    string EntryDocument,
    IReadOnlyList<WebviewSurface> Surfaces);
