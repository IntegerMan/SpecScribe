namespace SpecScribe;

/// <summary>One webview-navigable surface: its output-relative path (the navigation key the bridge script's
/// resolved link targets are matched against), its document title (the shim mirrors it onto the panel title), its
/// rendered content region (nav + breadcrumb + body — what an in-place <c>postMessage</c> swap installs into
/// <c>#specscribe-surface</c>), and the repo-relative source artifact that produced it. [Story 6.4]</summary>
/// <param name="SourcePath">The markdown artifact this surface was rendered from, expressed relative to the repo
/// root with forward slashes (exactly like <c>configuredOutputRoot</c> — the host joins it to the workspace folder
/// with NO duplicated path assumption). A story page → its <c>.md</c>; an epic/index/placeholder page → the epics
/// file; the dashboard → <c>null</c> (it aggregates many artifacts, so its "Open source" affordance is hidden).
/// The reveal control (Story 6.10 AC #1) reads this off the surface's <c>data-source</c> to post
/// <c>revealSource</c>; <c>null</c>/empty means "no source" and the button stays hidden. [Story 6.10]</param>
public sealed record WebviewSurface(string OutputRelativePath, string Title, string ContentHtml, string? SourcePath = null);

/// <summary>Everything one <c>specscribe webview</c> spawn hands the VS Code extension: the full entry document
/// (dashboard, with the <c>__CSP_SOURCE__</c>/<c>__NONCE__</c> host-runtime placeholders still unsubstituted —
/// the shim's only job) plus the complete navigable surface set for instant in-webview navigation and live-push.
/// Shipping every surface per spawn is deliberate: ingest dominates render cost (ADR 0005 measured ~1.8–2.0 s
/// warm, almost all ingest + git), so per-surface spawns would pay the same latency per CLICK; one bundle pays it
/// once per (re)load and navigation is instant thereafter (AC #2 "responsive"). [Story 6.4]</summary>
/// <param name="Outline">The host-neutral epic/story outline + status-bar summary the VS Code native surfaces
/// (activity-bar tree, status bar) consume — data, not rendering (ADR 0005 §1). Built from the SAME
/// <see cref="EpicsModel"/> loop that produces the surfaces, so its <c>SurfacePath</c>s are exactly the
/// <see cref="Surfaces"/> keys. Emits no HTML: the generated site is byte-identical with or without it. [Story 6.9]</param>
public sealed record WebviewBundle(
    string SiteTitle,
    string EntryPath,
    string EntryDocument,
    IReadOnlyList<WebviewSurface> Surfaces,
    ProjectOutline Outline);
