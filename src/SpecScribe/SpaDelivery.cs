using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The pure layout + serialization helpers for the opt-in JSON+SPA delivery form (Story 6.7): it turns a
/// <see cref="SpaBundle"/> into the bounded, small file set the client renderer consumes — a manifest, a handful of
/// grouped content chunks, and the entry shell — and owns the <c>&lt;main id="main-content"&gt;</c> landmark slice
/// that lets the WHOLE site (not just the dashboard/epics families) be consolidated without a per-page view-model
/// rewrite. Every method here is side-effect-free string work; <see cref="SiteGenerator"/> owns where the bytes
/// land (always under <c>OutputRoot</c> — AC #6). [Story 6.7]</summary>
public static class SpaDelivery
{
    /// <summary>The client entry shell — a real page at the output root, so its relative links resolve exactly like
    /// the static pages'. The dashboard region is inlined into it for instant first paint; with JS off the inlined
    /// nav links reach the static site directly (the <c>&lt;noscript&gt;</c> fallback — AC #2).</summary>
    public const string EntryFileName = "app.html";

    /// <summary>The client renderer, shipped as an embedded asset copied to the output root (mirrors
    /// <c>specscribe.js</c>). A small vanilla-JS bundle — no framework.</summary>
    public const string ScriptName = "specscribe-spa.js";

    /// <summary>The manifest the client fetches first: site title, entry path, and the page index (path → title +
    /// which content chunk holds it).</summary>
    public const string ManifestPath = "spa/manifest.json";

    /// <summary>Directory (under the output root) the manifest and content chunks live in.</summary>
    public const string ChunkDir = "spa";

    /// <summary>The per-chunk page cap. Chunking groups pages by their top-level output segment (so a navigation
    /// typically pulls one small, category-scoped chunk), then splits any group past this cap into numbered files —
    /// the invariant ADR 0006 axis A demands: FEW files, never one-per-page (no file-count win) and never a single
    /// monolith (a multi-MB first fetch at Epic-7 scale). Tunable; 75 keeps this repo to a handful of chunks while
    /// bounding the largest at Epic-7 scale.</summary>
    public const int MaxPagesPerChunk = 75;

    // JSON is fetched and JSON.parse'd by the client (never inlined into a <script>), so default (HTML-safe)
    // escaping is used — <, >, & become \uXXXX in the payload and decode back to the exact HTML on parse. Compact
    // (no indentation) because this is a delivery payload, not a hand-edited file.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly Regex TitleRegex =
        new("<title>(?<t>.*?)</title>", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Slices one page's SWAPPABLE content region out of the render pipeline's OWN output — the full page
    /// string the generator already holds before it writes the file (NOT a read-back of a generated <c>.html</c>,
    /// which would be scraping and an AD-1/AD-2 violation — see the Story 6.7 Dev Notes boundary). The region is the
    /// freshly-rendered nav markup (byte-identical to the page's own nav, minus the inline toggle script the client
    /// owns) plus the page's contiguous breadcrumb + <c>&lt;main id="main-content"&gt;…&lt;/main&gt;</c> block — the
    /// universal Story 1.4 landmark every templater emits. A page missing the landmark degrades to nav-only rather
    /// than aborting the whole SPA emit. [Story 6.7]</summary>
    public static string ExtractContentRegion(string fullPageHtml, string navMarkup)
    {
        const string mainMarker = "<main id=\"main-content\"";
        const string mainCloser = "</main>";
        var mainOpen = fullPageHtml.IndexOf(mainMarker, StringComparison.Ordinal);
        // Search for the closer starting AT the opener, not from index 0 — a page whose body legitimately
        // contains an earlier literal "</main>" (e.g. a doc's raw-HTML code sample) must never be allowed to
        // put mainClose before mainOpen, which would make the slice below throw. [Story 6.7 review]
        var mainClose = mainOpen >= 0
            ? fullPageHtml.IndexOf(mainCloser, mainOpen, StringComparison.Ordinal)
            : -1;
        if (mainOpen < 0 || mainClose < 0)
        {
            return navMarkup;
        }
        mainClose += mainCloser.Length;

        // The breadcrumb (when present) immediately precedes <main> and carries no script, so nav + [breadcrumb +
        // main] is contiguous and script-free — exactly the RenderContent shape (nav markup + breadcrumb + body).
        const string crumbMarker = "<div class=\"breadcrumb\"";
        var crumbOpen = fullPageHtml.IndexOf(crumbMarker, StringComparison.Ordinal);
        var bodyStart = crumbOpen >= 0 && crumbOpen < mainOpen ? crumbOpen : mainOpen;
        return navMarkup + fullPageHtml[bodyStart..mainClose];
    }

    /// <summary>The page title as the browser tab shows it — the full page's <c>&lt;title&gt;</c> (entity-decoded).
    /// Empty when a captured page somehow carries none. [Story 6.7]</summary>
    public static string ExtractTitle(string fullPageHtml)
    {
        var m = TitleRegex.Match(fullPageHtml);
        return m.Success ? WebUtility.HtmlDecode(m.Groups["t"].Value) : string.Empty;
    }

    // Matches the breadcrumb markup HtmlRenderAdapter.RenderBreadcrumb produces, in document order: either a
    // linked crumb (<a href="...">Label</a>) or the current, unlinked crumb (<span class="crumb-current" ...>Label</span>).
    private static readonly Regex CrumbRegex = new(
        "<a href=\"(?<href>[^\"]*)\">(?<alabel>[^<]*)</a>|<span class=\"crumb-current\"[^>]*>(?<clabel>[^<]*)</span>",
        RegexOptions.Compiled);

    /// <summary>Recovers the page's breadcrumb trail as structured <see cref="BreadcrumbCrumb"/> data from the
    /// render pipeline's OWN captured output — the same string <see cref="ExtractContentRegion"/> slices, never a
    /// re-read of a generated file. Every dashboard/epics family page already carries this structurally via its
    /// <see cref="PageView.Breadcrumb"/> (<see cref="SiteGenerator.AddSpaSurface"/> uses that directly); this
    /// extraction exists so every OTHER captured page — docs, ADRs, sprint, requirements, commits, etc. — gets the
    /// SAME structured parent/drill data the manifest ships (Story 6.7 review: the manifest previously carried none
    /// of this for non-family pages). A linked crumb's href always equals <c>RelativePrefix(currentOutputRelativePath)
    /// + targetPath</c> (see <see cref="PathUtil.RenderHeadOpen"/>'s sibling <c>RenderBreadcrumb</c>), so stripping
    /// that exact, independently-computed prefix recovers the output-relative target with no dot-segment parsing.
    /// [Story 6.7 review]</summary>
    public static IReadOnlyList<BreadcrumbCrumb> ExtractBreadcrumb(string fullPageHtml, string currentOutputRelativePath)
    {
        var crumbStart = fullPageHtml.IndexOf("<div class=\"breadcrumb\"", StringComparison.Ordinal);
        if (crumbStart < 0)
        {
            return Array.Empty<BreadcrumbCrumb>();
        }
        var crumbEnd = fullPageHtml.IndexOf("</div>", crumbStart, StringComparison.Ordinal);
        if (crumbEnd < 0)
        {
            return Array.Empty<BreadcrumbCrumb>();
        }

        var prefix = PathUtil.RelativePrefix(currentOutputRelativePath);
        var crumbSection = fullPageHtml.Substring(crumbStart, crumbEnd - crumbStart);
        var crumbs = new List<BreadcrumbCrumb>();
        foreach (Match m in CrumbRegex.Matches(crumbSection))
        {
            if (m.Groups["href"].Success)
            {
                var href = WebUtility.HtmlDecode(m.Groups["href"].Value);
                var target = href.StartsWith(prefix, StringComparison.Ordinal) ? href[prefix.Length..] : href;
                crumbs.Add(new BreadcrumbCrumb(WebUtility.HtmlDecode(m.Groups["alabel"].Value), target));
            }
            else
            {
                crumbs.Add(new BreadcrumbCrumb(WebUtility.HtmlDecode(m.Groups["clabel"].Value), null));
            }
        }
        return crumbs;
    }

    /// <summary>One serialized SPA output file: its output-relative path and its content bytes (UTF-8 text).</summary>
    public sealed record OutputFile(string OutputRelativePath, string Content);

    /// <summary>Turns a bundle into the exact JSON files to write: the manifest plus one file per content chunk.
    /// Pure — the caller writes them under <c>OutputRoot</c>. The page → chunk assignment groups by top-level output
    /// segment and caps each chunk at <see cref="MaxPagesPerChunk"/> pages. [Story 6.7]</summary>
    public static IReadOnlyList<OutputFile> BuildDataFiles(SpaBundle bundle)
    {
        // Deterministic order: entry first, then ordinal — so chunk membership (and thus the emitted files) is
        // stable run to run.
        var ordered = bundle.Pages
            .OrderBy(p => p.OutputRelativePath == bundle.EntryPath ? 0 : 1)
            .ThenBy(p => p.OutputRelativePath, StringComparer.Ordinal)
            .ToList();

        // Assign each page to a chunk file. Group by top-level segment; split oversized groups into numbered files.
        var pathToChunk = new Dictionary<string, string>(StringComparer.Ordinal);
        var chunkContents = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var groupCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var page in ordered)
        {
            var key = ChunkKey(page.OutputRelativePath);
            var count = groupCounts.TryGetValue(key, out var c) ? c : 0;
            var batch = count / MaxPagesPerChunk + 1;
            groupCounts[key] = count + 1;

            var chunkFile = batch == 1
                ? $"{ChunkDir}/pages-{key}.json"
                : $"{ChunkDir}/pages-{key}-{batch}.json";

            pathToChunk[page.OutputRelativePath] = chunkFile;
            if (!chunkContents.TryGetValue(chunkFile, out var map))
            {
                map = new Dictionary<string, string>(StringComparer.Ordinal);
                chunkContents[chunkFile] = map;
            }
            map[page.OutputRelativePath] = page.ContentHtml;
        }

        var files = new List<OutputFile>();

        // Each page's drill-UP parent is the same "last crumb carrying a real path" rule
        // BreadcrumbTrail.ParentTarget already defines, so the manifest's structured parent/child graph can never
        // disagree with what the embedded breadcrumb HTML shows. [Story 6.7 review]
        var parentByPath = ordered.ToDictionary(
            p => p.OutputRelativePath,
            p => new BreadcrumbTrail { Crumbs = p.Breadcrumb }.ParentTarget,
            StringComparer.Ordinal);
        var childrenByParent = ordered
            .Where(p => parentByPath[p.OutputRelativePath] is not null)
            .GroupBy(p => parentByPath[p.OutputRelativePath]!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.OutputRelativePath).ToList());

        // Manifest: site title, entry, the top nav graph, and the ordered page index (path → title + chunk +
        // breadcrumb + drill parent/children — AC #1's InteractionState semantics, structured rather than only
        // embedded in HTML).
        var pages = new Dictionary<string, ManifestEntry>(StringComparer.Ordinal);
        foreach (var page in ordered)
        {
            var crumbs = page.Breadcrumb.Select(c => new ManifestCrumb(c.Label, c.OutputRelativePath)).ToList();
            var children = childrenByParent.TryGetValue(page.OutputRelativePath, out var kids)
                ? kids
                : Array.Empty<string>();
            pages[page.OutputRelativePath] = new ManifestEntry(
                page.Title, pathToChunk[page.OutputRelativePath], crumbs, parentByPath[page.OutputRelativePath], children);
        }
        var navGraph = bundle.Nav.Select(n => new ManifestNavItem(n.Label, n.OutputRelativePath)).ToList();
        var manifest = new Manifest(bundle.SiteTitle, bundle.EntryPath, navGraph, pages);
        files.Add(new OutputFile(ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions)));

        // Content chunks, ordinal by file name so the emitted set is deterministic.
        foreach (var (chunkFile, map) in chunkContents.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            files.Add(new OutputFile(chunkFile, JsonSerializer.Serialize(map, JsonOptions)));
        }

        return files;
    }

    /// <summary>The top-level output segment a page belongs to (its chunk group): the first path segment, or
    /// <c>root</c> for a page at the output root. Every SpecScribe output segment (<c>epics</c>, <c>requirements</c>,
    /// <c>adrs</c>, <c>commits</c>, <c>implementation-artifacts</c>, …) is filename-safe.</summary>
    private static string ChunkKey(string outputRelativePath)
    {
        var normalized = PathUtil.NormalizeSlashes(outputRelativePath);
        var slash = normalized.IndexOf('/');
        return slash < 0 ? "root" : normalized[..slash];
    }

    /// <summary>Builds the client entry shell (<see cref="EntryFileName"/>): the canonical site head (so the SPA
    /// carries the same stylesheet, favicon, and enhancement script as every static page), a <c>&lt;noscript&gt;</c>
    /// fallback link to the static site, the dashboard region inlined for instant first paint, and the client
    /// renderer script. The inlined region's own nav links are ordinary relative links to the static <c>.html</c>
    /// files, so navigation works with JS disabled too (AC #2 / NFR6). [Story 6.7]</summary>
    public static string BuildEntryShell(string siteTitle, string dashboardRegion)
    {
        var description =
            $"Single-page delivery of {siteTitle} — the same C#-rendered content as the static site, navigated "
            + "client-side. Works without JavaScript via the static site.";

        var sb = new StringBuilder();
        // Reuse the canonical head (title, meta/OG, favicon, versioned specscribe.css + specscribe.js, skip link,
        // <body>) so the SPA shell can never drift from the static pages' chrome.
        sb.Append(PathUtil.RenderHeadOpen(siteTitle, ForgeOptions.StylesheetName, ForgeOptions.ScriptName, description));
        sb.Append("<noscript>\n");
        sb.Append("  <div class=\"spa-noscript\">JavaScript is disabled — ");
        sb.Append("<a href=\"index.html\">open the full static site</a>.</div>\n");
        sb.Append("</noscript>\n");
        // The client swaps THIS element's innerHTML on navigation; data-path is the current page's key the client
        // resolves relative links against (it never trusts the URL, which may be push-state'd to a nested path).
        // data-asset-version is the same build token used above, read by the client so its manifest/chunk fetches
        // carry it too (a stale cached JSON layer would otherwise survive a redeploy indefinitely). [Story 6.7 review]
        sb.Append($"<div id=\"spa-content\" data-path=\"index.html\" data-asset-version=\"{PathUtil.CurrentAssetVersion}\">\n");
        sb.Append(dashboardRegion);
        sb.Append("\n</div>\n");
        // Versioned exactly like specscribe.css/specscribe.js (PathUtil.RenderHeadOpen above): a redeployed script
        // must never be masked by a browser/CDN cache of the previous build. The SAME token is appended to the
        // client's own manifest/chunk fetches (see the data-asset-version attribute below) so the whole SPA data
        // layer invalidates together on every redeploy — not just the script. [Story 6.7 review — cache-busting]
        sb.Append($"<script src=\"{ScriptName}?v={PathUtil.CurrentAssetVersion}\" defer></script>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private sealed record ManifestCrumb(string Label, string? OutputRelativePath);

    private sealed record ManifestNavItem(string Label, string OutputRelativePath);

    private sealed record ManifestEntry(
        string Title,
        string Chunk,
        IReadOnlyList<ManifestCrumb> Breadcrumb,
        string? Parent,
        IReadOnlyList<string> Children);

    private sealed record Manifest(
        string SiteTitle,
        string Entry,
        IReadOnlyList<ManifestNavItem> Nav,
        IReadOnlyDictionary<string, ManifestEntry> Pages);
}
