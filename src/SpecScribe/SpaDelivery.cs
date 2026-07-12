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
        var mainOpen = fullPageHtml.IndexOf(mainMarker, StringComparison.Ordinal);
        var mainClose = fullPageHtml.IndexOf("</main>", StringComparison.Ordinal);
        if (mainOpen < 0 || mainClose < 0)
        {
            return navMarkup;
        }
        mainClose += "</main>".Length;

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

        // Manifest: site title, entry, and the ordered page index (path → title + chunk).
        var pages = new Dictionary<string, ManifestEntry>(StringComparer.Ordinal);
        foreach (var page in ordered)
        {
            pages[page.OutputRelativePath] = new ManifestEntry(page.Title, pathToChunk[page.OutputRelativePath]);
        }
        var manifest = new Manifest(bundle.SiteTitle, bundle.EntryPath, pages);
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
        sb.Append("<div id=\"spa-content\" data-path=\"index.html\">\n");
        sb.Append(dashboardRegion);
        sb.Append("\n</div>\n");
        sb.Append($"<script src=\"{ScriptName}\" defer></script>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private sealed record ManifestEntry(string Title, string Chunk);

    private sealed record Manifest(string SiteTitle, string Entry, IReadOnlyDictionary<string, ManifestEntry> Pages);
}
