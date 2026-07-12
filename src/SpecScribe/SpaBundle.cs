namespace SpecScribe;

/// <summary>One SPA-navigable page as the JSON data layer ships it: its output-relative path (the navigation key
/// the client renderer resolves link targets against), its document title (mirrored onto <c>document.title</c> on
/// navigation), its pre-rendered content region — the shared nav markup + breadcrumb + the page's
/// <c>&lt;main id="main-content"&gt;</c> body, with charts already inline SVG — and its breadcrumb trail (reusing
/// <see cref="BreadcrumbCrumb"/>, the SAME drill/parent data <see cref="BreadcrumbTrail.ParentTarget"/> already
/// derives from, so the manifest's structured parent/child relationships (built in
/// <see cref="SpaDelivery.BuildDataFiles"/>) can never disagree with what the embedded HTML shows). This is the SAME
/// region shape the webview ships (<see cref="WebviewSurface"/>); the SPA difference is that it consolidates the
/// WHOLE site, not the five dashboard/epics families. Rendering stays in C# (AC #1) — the client injects this
/// string, it never re-renders or re-parses anything. [Story 6.7]</summary>
public sealed record SpaPage(
    string OutputRelativePath,
    string Title,
    string ContentHtml,
    IReadOnlyList<BreadcrumbCrumb> Breadcrumb);

/// <summary>Everything the opt-in <c>--spa</c> delivery form needs to emit: the site title, the entry page (the
/// dashboard, inlined into the client shell for instant first paint), the top nav graph (mirrors
/// <see cref="SiteNav.Items"/> — the same Home/Epics/ADRs/… bar every static page carries), and the COMPLETE page
/// set — every page the static site emits, each as a pre-rendered content region (AC #7). The <see cref="SiteGenerator"/>
/// writes these out as a bounded, small set of files (a manifest + a handful of grouped content chunks + the client
/// entry shell + a small JS bundle) alongside the untouched static site, which remains the JS-optional source of
/// truth and the <c>&lt;noscript&gt;</c> fallback (AC #2/#3, NFR6). The static site's bytes never change (AC #5):
/// the SPA files are strictly additive. [Story 6.7]</summary>
public sealed record SpaBundle(
    string SiteTitle,
    string EntryPath,
    IReadOnlyList<(string Label, string OutputRelativePath)> Nav,
    IReadOnlyList<SpaPage> Pages);
