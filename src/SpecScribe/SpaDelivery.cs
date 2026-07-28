using System.Net;
using System.Security.Cryptography;
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

    /// <summary>The canonical IR's SCHEMA VERSION, stamped on the manifest as <c>schemaVersion</c> (Story 22.2
    /// AC #1). ADR 0008 seated this file set as the canonical intermediate representation and Story 22.2 promoted
    /// it in place — <c>spa/manifest.json</c> + <c>spa/pages-*.json</c> ARE the IR; there is no second directory
    /// and no second capture path.
    /// <para><b>Compatibility rule — a monotonically increasing integer, not semver.</b> Consumers do a single
    /// integer compare, and there is no independent release cadence to justify a three-part version. BUMP it on
    /// any BREAKING change to the manifest or chunk shape: a removed or renamed field, a changed field type, a
    /// changed meaning for an existing field, or a change to how a page's content region is delimited. Do NOT
    /// bump for a purely ADDITIVE field — an older consumer ignores what it does not read, and every field this
    /// schema has added so far arrived that way.</para>
    /// <para>Version <b>1</b> is the first stamped shape. The pre-22.2 unversioned manifest is version <b>0</b>
    /// by implication: it carried no <c>schemaVersion</c> key at all, so a consumer reading a manifest without
    /// one is looking at version 0. No migration shim exists, and none is needed — the SPA client ships in this
    /// repo alongside the emitter, and there is no shipped consumer outside it.</para>
    /// <para>Version <b>2</b> (Story 22.4 AC #4/#6) moved the content region's START marker. Before it,
    /// <see cref="ExtractContentRegion"/> sliced a captured page from the inner <c>&lt;div class="breadcrumb"&gt;</c>
    /// even when the page's pager had put that breadcrumb inside a <c>&lt;div class="page-wayfinding"&gt;</c>
    /// wrapper — so those regions carried the wrapper's closing tag without its opener. It now slices from the
    /// band's outermost marker, and the IR has ONE region shape.</para>
    /// <para><b>The measurement that decided the bump</b> (this repo, <c>--spa --deep-git</c>, 1,400 IR pages):
    /// 594 pages' <c>contentHash</c> moved, every one of them by exactly <b>+30 bytes</b> — the length of the
    /// literal <c>&lt;div class="page-wayfinding"&gt;\n</c> opener. Regions carrying an unbalanced band went from
    /// 594 to 0; pages carrying the wrapper went from 189 to 783; no page was added, removed, or lost its
    /// landmark. That is squarely "a change to how a page's content region is delimited", so this is a bump and
    /// not an additive change. Consumers move in the same change: <c>EXPECTED_SCHEMA_VERSION</c> in BOTH
    /// <c>web/ir/adapter.ts</c> and <c>web/ir/adapter.client.ts</c> (the adapter only warns on a mismatch, so a
    /// missed one is silent).</para></summary>
    public const int SchemaVersion = 2;

    /// <summary>The per-chunk page cap. Chunking groups pages by their top-level output segment (so a navigation
    /// typically pulls one small, category-scoped chunk), then splits any group past this cap into numbered files —
    /// the invariant ADR 0006 axis A demands: FEW files, never one-per-page (no file-count win) and never a single
    /// monolith (a multi-MB first fetch at Epic-7 scale). Tunable; 75 keeps this repo to a handful of chunks while
    /// bounding the largest at Epic-7 scale.</summary>
    public const int MaxPagesPerChunk = 75;

    /// <summary>The per-chunk UTF-8 byte budget — a second, independent split trigger alongside
    /// <see cref="MaxPagesPerChunk"/> (deferred item, Story 6.7 at-scale perf pass: the count-only cap "cannot"
    /// bound the largest chunk, per its own doc comment, once one PAGE in a group is itself huge — a single
    /// multi-tens-of-MB page like a large-repo code-map dragged its entire top-level group's chunk to 112.9 MB,
    /// since 17 ordinary neighbors shared the file with it). A group starts a new batch as soon as either cap
    /// would be exceeded, and any single page whose OWN content already exceeds this budget is isolated into its
    /// own dedicated chunk (see <see cref="BuildDataFiles"/>) rather than dragging neighbors along or being
    /// split itself (a page's content region is atomic). 2 MB comfortably covers this repo's largest real chunk
    /// (the epics group, low single-digit MB across ~90+ pages) without ever tripping at normal/Epic-7 scale, so
    /// default generation is unaffected — it exists purely to isolate the pathological long tail.
    /// <para><b>A real output-file ceiling, with ONE declared exception</b> (Story 22.2 AC #2). Until 22.2 this
    /// budgeted each page's RAW UTF-8 <see cref="SpaPage.ContentHtml"/> bytes, not the JSON-serialized size — and
    /// since the chunk is written with default HTML-safe escaping (<c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c> each
    /// balloon to a 6-byte <c>\uXXXX</c> escape, plus per-page key/quote/comma overhead) the emitted file could
    /// exceed this number by several times over. It now budgets the EXACT JSON tokens the chunk will carry
    /// (see <see cref="BuildDataFiles"/>), so a multi-page chunk can no longer overshoot.</para>
    /// <para><b>The declared exception:</b> a page's content region is ATOMIC — splitting one mid-HTML would
    /// hand the client a broken fragment — so a SINGLE page whose own encoded size already exceeds this budget
    /// must still be written whole, in a chunk of its own, above the ceiling. Story 22.1 measured exactly this
    /// (a 3.08 MB chunk against a 2 MB guard). That case is never silent: every such page is listed, with its
    /// encoded size, in the manifest's <c>oversizedPages</c> array. Read the ceiling as "no chunk exceeds this
    /// except a declared single-page chunk, and the manifest names each one".</para></summary>
    public const int MaxChunkBytes = 2_000_000;

    /// <summary>The two braces a chunk's JSON object costs beyond its member tokens — counted into the byte
    /// budget so <see cref="MaxChunkBytes"/> bounds the FILE, not merely its contents.</summary>
    private const int ChunkEnvelopeBytes = 2;

    /// <summary>Hex characters kept from the SHA-256 of a page's content region (Story 22.2 AC #6). 16 hex chars
    /// = 64 bits: at SpecScribe's page counts (thousands, not billions) an accidental collision is far below the
    /// noise floor of anything 22.5/22.6 would do with it, and the full 64-char digest would add ~48 bytes per
    /// page to a manifest the client fetches FIRST. Truncation is a deliberate, documented trade — the hash is a
    /// change detector for delta addressing, never a security or integrity claim.</summary>
    private const int ContentHashHexLength = 16;

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
    /// owns) plus the page's contiguous wayfinding band + <c>&lt;main id="main-content"&gt;…&lt;/main&gt;</c> block —
    /// the universal Story 1.4 landmark every templater emits. A page missing the landmark degrades to nav-only
    /// rather than aborting the whole SPA emit.
    /// <para>The band is sliced from its OUTERMOST marker so every emitted region has ONE shape and is
    /// element-balanced — see the two-marker note below. [Story 6.7; Story 22.4 AC #4]</para></summary>
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

        // The wayfinding band (when present) immediately precedes <main> and carries no script, so nav + [band +
        // main] is contiguous and script-free — exactly the RenderContent shape (nav markup + wayfinding + body).
        //
        // TWO markers, because the band has two shapes (Story 22.4 AC #4). A page whose pager renders non-empty
        // gets HtmlRenderAdapter.RenderWayfinding's <div class="page-wayfinding"> WRAPPER around the breadcrumb;
        // every other page gets the bare breadcrumb, byte-identically to RenderBreadcrumb. Slicing from the inner
        // breadcrumb on a wrapped page carried the wrapper's closing </div> WITHOUT its opener — an IR region
        // unbalanced by one element on 594 of this repo's 1,400 pages, which the TS adapter then had to detect
        // and repair. Preferring the wrapper emits ONE region shape for the whole IR, and the repair is deleted.
        //
        // Take the EARLIEST candidate that precedes mainOpen (the wrapper always encloses the breadcrumb, so it
        // is the earlier of the two when both are present). Anchoring on "precedes <main>" is what keeps a
        // breadcrumb-shaped string inside the page BODY — a doc's raw-HTML code sample — from splitting the
        // region; region-split.test.ts pins that case. Only the slice's START moves: the end is still </main>,
        // which HtmlTemplater's section-nav script placement depends on. [Story 22.4 AC #4]
        const string wrapMarker = "<div class=\"page-wayfinding\"";
        const string crumbMarker = "<div class=\"breadcrumb\"";
        var wrapOpen = fullPageHtml.IndexOf(wrapMarker, StringComparison.Ordinal);
        var crumbOpen = fullPageHtml.IndexOf(crumbMarker, StringComparison.Ordinal);
        var bodyStart = mainOpen;
        if (wrapOpen >= 0 && wrapOpen < bodyStart) bodyStart = wrapOpen;
        if (crumbOpen >= 0 && crumbOpen < bodyStart) bodyStart = crumbOpen;
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

    private static readonly Regex MetaDescriptionRegex = new(
        "<meta name=\"description\" content=\"(?<d>[^\"]*)\">", RegexOptions.Compiled);

    /// <summary>The page's <c>&lt;meta name="description"&gt;</c> text (entity-decoded), for the IR's head
    /// projection — the same extraction discipline (and the same HTML-decode step) <see cref="ExtractTitle"/>
    /// already applies to <c>&lt;title&gt;</c>, over the render pipeline's OWN captured output rather than a
    /// disk read-back. Null when a captured page somehow carries none; the caller falls back to the title,
    /// exactly as <see cref="PathUtil.RenderHeadOpen"/> does when it builds the tag in the first place.
    /// [Story 22.2]</summary>
    public static string? ExtractMetaDescription(string fullPageHtml)
    {
        var m = MetaDescriptionRegex.Match(fullPageHtml);
        return m.Success ? WebUtility.HtmlDecode(m.Groups["d"].Value) : null;
    }

    private static readonly Regex NavBlockRegex = new(
        "<nav class=\"site-nav\".*?</nav>\\n?", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Slices the page's OWN rendered nav element out of the captured page string, so a captured page
    /// keeps the page-local context band (<c>site-nav-local-context</c>) the static renderer computed for it
    /// (Story 22.2 AC #5). Before this, every captured page's nav was RE-rendered from
    /// <c>nav.ToNavigationView(path)</c>, which takes no local-context argument — so an ADR page silently lost
    /// its "ADRs" band and got the generic key-views nav instead (Story 23.1's enumerated difference #2). There
    /// is no path → <see cref="NavLocalContext"/> resolver to re-derive it from: every call site builds one
    /// inline at render time and discards it.
    /// <para>The slice ends at the FIRST <c>&lt;/nav&gt;</c> — <see cref="HtmlRenderAdapter.RenderNavMarkup"/>
    /// nests no second <c>&lt;nav&gt;</c> inside <c>.site-nav</c> (its groups are <c>&lt;details&gt;</c>, its
    /// bands are <c>&lt;div&gt;</c>) — and therefore stops exactly where the markup does, EXCLUDING the inline
    /// <c>NavToggleScript</c> that immediately follows it on the HTML surface (the SPA client and the webview
    /// bridge each own the toggle through delegation; an injected script would never execute anyway).</para>
    /// <para>Byte-faithful and plumbing-free: it consumes the same captured string
    /// <see cref="ExtractContentRegion"/> slices, never a disk read-back, so the AD-1/AD-2 boundary holds.
    /// Returns null when the page carries no site nav, leaving the caller on its re-rendered fallback.
    /// [Story 22.2]</para></summary>
    public static string? ExtractNavMarkup(string fullPageHtml)
    {
        var m = NavBlockRegex.Match(fullPageHtml);
        return m.Success ? m.Value : null;
    }

    /// <summary>One embedded <c>&lt;script&gt;</c> a consumer of a page's content region must deal with — the
    /// strip-or-nonce decision, made declarable rather than something each consumer re-derives with its own
    /// regex (Story 22.2 AC #5).</summary>
    /// <param name="Id">The element's <c>id</c>, or null when it carries none.</param>
    /// <param name="Kind"><see cref="DataIslandKind"/> for an inert data island (a <c>type</c> that is not a
    /// JavaScript type — today always <c>application/json</c>), <see cref="ExecutableScriptKind"/> for anything
    /// the browser would actually run. The distinction IS the decision: inert data can simply be dropped;
    /// executable script must be dropped or nonce'd, and under a strict CSP silently fails if it is neither.</param>
    public sealed record ScriptIsland(string? Id, string Kind);

    /// <summary>Kind for an inert, never-executed data island (<c>&lt;script type="application/json"&gt;</c>).</summary>
    public const string DataIslandKind = "data";

    /// <summary>Kind for a script the browser executes — no <c>type</c>, or a JavaScript/module type.</summary>
    public const string ExecutableScriptKind = "executable";

    private static readonly Regex ScriptTagRegex = new(
        "<script(?<attrs>[^>]*)>", RegexOptions.Compiled);

    private static readonly Regex ScriptTypeAttrRegex = new(
        "\\btype=\"(?<v>[^\"]*)\"", RegexOptions.Compiled);

    private static readonly Regex ScriptIdAttrRegex = new(
        "\\bid=\"(?<v>[^\"]*)\"", RegexOptions.Compiled);

    /// <summary>Declares every <c>&lt;script&gt;</c> embedded in a page's CONTENT REGION, classified into the
    /// strip-or-nonce decision a consumer has to make (Story 22.2 AC #5 / #1). Derived FROM the region rather
    /// than tracked alongside it, on the same principle as <c>HierarchyExplorer.ContainsHost</c>: a declaration
    /// computed from the page can never disagree with the page.
    /// <para>This is also the ADR 0013 §5 "chart data + component configuration" hook. The Hierarchy Explorer's
    /// island already carries the component CONFIG next to its nodes; declaring the islands makes that
    /// first-class IR metadata instead of something a consumer must recognize by regex — which is exactly how
    /// the webview handles it today, and exactly why its regex covers only the <c>application/json</c> half.</para>
    /// [Story 22.2]</summary>
    public static IReadOnlyList<ScriptIsland> ExtractScriptIslands(string contentHtml)
    {
        var matches = ScriptTagRegex.Matches(contentHtml);
        if (matches.Count == 0) return Array.Empty<ScriptIsland>();

        var islands = new List<ScriptIsland>(matches.Count);
        foreach (Match m in matches)
        {
            var attrs = m.Groups["attrs"].Value;
            var typeMatch = ScriptTypeAttrRegex.Match(attrs);
            var idMatch = ScriptIdAttrRegex.Match(attrs);
            // No type, or a JavaScript/module type, means the browser runs it. Anything else is an inert data
            // block the HTML spec tells the browser to ignore.
            var type = typeMatch.Success ? typeMatch.Groups["v"].Value.Trim() : string.Empty;
            var executable = type.Length == 0
                || type.Equals("module", StringComparison.OrdinalIgnoreCase)
                || type.Contains("javascript", StringComparison.OrdinalIgnoreCase)
                || type.Equals("text/ecmascript", StringComparison.OrdinalIgnoreCase);
            islands.Add(new ScriptIsland(
                idMatch.Success ? WebUtility.HtmlDecode(idMatch.Groups["v"].Value) : null,
                executable ? ExecutableScriptKind : DataIslandKind));
        }
        return islands;
    }

    /// <summary>A page's stable content hash — the addressing half of Story 22.2 AC #6, so 22.5/22.6 can diff at
    /// PAGE granularity instead of re-shipping whole chunks (22.1 measured a one-line edit re-shipping 39.9 % of
    /// a 48 MB IR at chunk granularity). SHA-256 over the region's UTF-8 bytes, lowercase hex, truncated to
    /// <see cref="ContentHashHexLength"/>.
    /// <para>Deterministic by construction — no clock, no RNG, no machine-dependent input (NFR9). It inherits
    /// whatever volatility lives INSIDE the region, and nothing more: the footer clock, the <c>?v=</c> asset
    /// cache-bust and the version/build rows the golden gate normalizes all sit OUTSIDE the
    /// <c>&lt;nav&gt;…&lt;main&gt;</c> slice, which is why the emitted manifest is byte-identical across two
    /// consecutive runs of unchanged input.</para></summary>
    public static string ContentHash(string contentHtml)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(contentHtml));
        return Convert.ToHexStringLower(digest)[..ContentHashHexLength];
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

        // Pre-encode each page's chunk key and content ONCE — these are the EXACT JSON tokens the chunk file will
        // carry, so budgeting against them (rather than against raw UTF-8 content bytes, as this did before Story
        // 22.2) is what turns MaxChunkBytes from an approximation into a real file ceiling. Encoding once and
        // reusing the tokens for the emitted file also means this costs no extra serialization work, not double.
        var encoded = new Dictionary<string, EncodedPage>(StringComparer.Ordinal);
        foreach (var page in ordered)
        {
            var keyJson = JsonSerializer.Serialize(page.OutputRelativePath, JsonOptions);
            var valueJson = JsonSerializer.Serialize(page.ContentHtml, JsonOptions);
            // key + ':' + value + ',' — the page's full cost as a member of the chunk object. (The final member
            // carries no comma, so this over-counts a finished chunk by exactly one byte: the safe direction.)
            var cost = Encoding.UTF8.GetByteCount(keyJson) + Encoding.UTF8.GetByteCount(valueJson) + 2;
            encoded[page.OutputRelativePath] = new EncodedPage(keyJson, valueJson, cost);
        }

        // Assign each page to a chunk file. Group by top-level segment; split oversized groups into numbered files
        // whenever EITHER the page-count cap or the byte-size budget would be exceeded — two independent triggers,
        // since a group can go bad on either axis (too many small pages, or one huge one). A page whose own encoded
        // size already exceeds MaxChunkBytes always gets a fresh, dedicated batch: it never joins a non-empty batch
        // (so it can't inflate an otherwise-normal neighbor's fetch), and the batch after it starts fresh too (so it
        // never inflates the NEXT page's chunk either). Its region is ATOMIC, so that dedicated chunk is still
        // written above the ceiling — and is DECLARED in the manifest's oversizedPages rather than left silent
        // (Story 22.2 AC #2). Batch state is per top-level group, walked in the same deterministic `ordered`
        // sequence used everywhere else in this method.
        var pathToChunk = new Dictionary<string, string>(StringComparer.Ordinal);
        var chunkMembers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var groupBatches = new Dictionary<string, GroupBatchState>(StringComparer.Ordinal);

        foreach (var page in ordered)
        {
            var key = ChunkKey(page.OutputRelativePath);
            var slot = encoded[page.OutputRelativePath];
            if (!groupBatches.TryGetValue(key, out var state))
            {
                state = new GroupBatchState();
                groupBatches[key] = state;
            }

            if (state.PageCount > 0 &&
                (state.PageCount >= MaxPagesPerChunk || state.Bytes + slot.Cost > MaxChunkBytes))
            {
                state.Batch++;
                state.PageCount = 0;
                state.Bytes = ChunkEnvelopeBytes;
            }

            state.PageCount++;
            state.Bytes += slot.Cost;

            var chunkFile = state.Batch == 1
                ? $"{ChunkDir}/pages-{key}.json"
                : $"{ChunkDir}/pages-{key}-{state.Batch}.json";

            pathToChunk[page.OutputRelativePath] = chunkFile;
            if (!chunkMembers.TryGetValue(chunkFile, out var members))
            {
                members = new List<string>();
                chunkMembers[chunkFile] = members;
            }
            members.Add(page.OutputRelativePath);
        }

        // Content chunks, ordinal by file name so the emitted set is deterministic. Assembled from the SAME
        // pre-encoded tokens the byte budget was computed from — byte-identical to serializing the equivalent
        // Dictionary<string, string> (System.Text.Json writes a dictionary in insertion order with no whitespace,
        // and these keys/values came out of the same serializer with the same options), which
        // SpaDeliveryTests pins directly so the equivalence cannot drift. Built BEFORE the manifest so the
        // over-cap declaration below can quote each file's REAL size rather than a prediction of it.
        var chunkFiles = new List<(string Path, string Content, List<string> Members)>();
        foreach (var (chunkFile, members) in chunkMembers.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var sb = new StringBuilder("{");
            for (var i = 0; i < members.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var slot = encoded[members[i]];
                sb.Append(slot.KeyJson).Append(':').Append(slot.ValueJson);
            }
            sb.Append('}');
            chunkFiles.Add((chunkFile, sb.ToString(), members));
        }

        // The declared exception to the byte ceiling (AC #2): measured on the assembled files, so what the
        // manifest says is exactly what landed on disk. By construction an over-cap chunk holds exactly one page
        // — a page whose own encoded size already exceeds the budget, isolated by the batching loop above and
        // unsplittable because its content region is atomic. Every member is named anyway rather than assuming
        // that invariant, so the declaration stays truthful even if the batching rule is ever changed.
        var oversized = new List<ManifestOversizedPage>();
        foreach (var (_, content, members) in chunkFiles)
        {
            var chunkBytes = Encoding.UTF8.GetByteCount(content);
            if (chunkBytes <= MaxChunkBytes) continue;
            foreach (var member in members)
            {
                oversized.Add(new ManifestOversizedPage(member, chunkBytes));
            }
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
                page.Title, pathToChunk[page.OutputRelativePath], crumbs, parentByPath[page.OutputRelativePath], children,
                // Head projection (AC #5): the derivation rule PathUtil.RenderHeadOpen already applies is resolved
                // HERE, once, so a consumer reproduces the whole head without the IR shipping four near-duplicate
                // strings per page. description falls back to title when the page carries none; og:title mirrors
                // title, og:description mirrors description, og:type is the constant "website", and the favicon is
                // a constant data URI. The ?v= asset cache-bust is deliberately NOT carried: it is a build token
                // (already exposed as PathUtil.CurrentAssetVersion and the shell's data-asset-version), and putting
                // it in per-page data would churn every page's bytes on every build.
                new ManifestHead(page.Title, page.MetaDescription is { Length: > 0 } d ? d : page.Title),
                ExtractScriptIslands(page.ContentHtml),
                ContentHash(page.ContentHtml),
                Encoding.UTF8.GetByteCount(page.ContentHtml));
        }
        var navGraph = bundle.Nav.Select(n => new ManifestNavItem(n.Label, n.OutputRelativePath)).ToList();
        var manifest = new Manifest(SchemaVersion, bundle.SiteTitle, bundle.EntryPath, navGraph, oversized, pages);
        files.Add(new OutputFile(ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions)));
        foreach (var (chunkFile, content, _) in chunkFiles)
        {
            files.Add(new OutputFile(chunkFile, content));
        }

        return files;
    }

    /// <summary>One page's already-serialized chunk tokens plus the exact byte cost of carrying it as a member of
    /// a chunk object (<c>key</c> + <c>:</c> + <c>value</c> + <c>,</c>). Computed once per
    /// <see cref="BuildDataFiles"/> call and used for BOTH the byte budget and the emitted file, so the number the
    /// cap is enforced against and the bytes actually written can never disagree. [Story 22.2]</summary>
    private sealed record EncodedPage(string KeyJson, string ValueJson, int Cost);

    /// <summary>Mutable running state for the CURRENT (last) batch of one top-level chunk group, walked once
    /// per <see cref="BuildDataFiles"/> call — never reused across calls. <see cref="Batch"/> is the 1-based
    /// numbered-file suffix (1 = the bare <c>pages-{key}.json</c>, no suffix); <see cref="PageCount"/>/
    /// <see cref="Bytes"/> reset to 0 whenever a new batch starts.</summary>
    private sealed class GroupBatchState
    {
        public int Batch = 1;
        public int PageCount;
        /// <summary>Seeded with the chunk object's own <c>{}</c> so the budget bounds the FILE, not just its
        /// members; reset to the same seed whenever a new batch starts.</summary>
        public long Bytes = ChunkEnvelopeBytes;
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

    /// <summary>The per-page head projection (Story 22.2 AC #5) — see the derivation comment in
    /// <see cref="BuildDataFiles"/> for how a consumer rebuilds the full <c>&lt;head&gt;</c> from these two
    /// fields.</summary>
    private sealed record ManifestHead(string Title, string Description);

    /// <summary>A page whose own encoded size exceeds <see cref="MaxChunkBytes"/>, so the dedicated chunk holding
    /// it is necessarily above the ceiling (its content region is atomic and cannot be split). Declared, never
    /// silent — Story 22.2 AC #2.</summary>
    /// <param name="ChunkBytes">The size of the chunk FILE this page produces, in JSON-encoded UTF-8 bytes —
    /// which is the number that exceeds the ceiling, not the raw content size the page entry's own
    /// <c>bytes</c> field carries.</param>
    private sealed record ManifestOversizedPage(string Path, long ChunkBytes);

    private sealed record ManifestEntry(
        string Title,
        string Chunk,
        IReadOnlyList<ManifestCrumb> Breadcrumb,
        string? Parent,
        IReadOnlyList<string> Children,
        ManifestHead Head,
        IReadOnlyList<ScriptIsland> ScriptIslands,
        string ContentHash,
        int Bytes);

    private sealed record Manifest(
        int SchemaVersion,
        string SiteTitle,
        string Entry,
        IReadOnlyList<ManifestNavItem> Nav,
        IReadOnlyList<ManifestOversizedPage> OversizedPages,
        IReadOnlyDictionary<string, ManifestEntry> Pages);
}
