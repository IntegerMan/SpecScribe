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

    /// <summary>The delta sidecar (<c>spa/delta.json</c>) written beside the manifest on each WATCH-MODE regen —
    /// AD-8's "sidecar polling" transport clause, operationalized (Story 22.6 AC #2). Never written by a one-shot
    /// <c>generate</c>: it carries a wall-clock <c>generatedAt</c> by nature, and a cold build must stay
    /// byte-reproducible (NFR9).</summary>
    public const string DeltaPath = "spa/delta.json";

    /// <summary>The DELTA document's own schema version — deliberately SEPARATE from <see cref="SchemaVersion"/>,
    /// and governed by the same monotonically-increasing-integer compatibility rule that constant's doc comment
    /// states (bump on a removed/renamed field, a changed type, a changed meaning; do NOT bump for a purely
    /// additive field).
    /// <para><b>Why a second constant rather than a bump.</b> A new sidecar file is strictly ADDITIVE to the IR —
    /// every existing consumer that reads <c>manifest.json</c> and the chunks is bit-for-bit unaffected by a file
    /// it never opens, which is exactly the case <see cref="SchemaVersion"/>'s own doc comment says NOT to bump
    /// for. Versioning the delta independently also lets the delta contract move (it is young; the IR's is not)
    /// without forcing every IR consumer through a compatibility check it does not need.</para>
    /// <para>The delta ALSO carries the <see cref="SchemaVersion"/> it was computed against, as a separate
    /// <c>schemaVersion</c> field: a consumer holding state from a different IR schema cannot safely apply a page
    /// delta to it, so a mismatch means refetch. <see cref="BuildDelta"/> enforces that itself — see its
    /// degrade-to-full rules.</para></summary>
    public const int DeltaSchemaVersion = 1;

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

    /// <summary>The universal Story 1.4 landmark every templater emits, shared by <see cref="ExtractContentRegion"/>
    /// (as its region's end-anchor) and <see cref="ExtractNavMarkup"/> (as the boundary a nav match must precede) —
    /// one literal so the two extractors can never anchor against different ideas of "where the chrome ends"
    /// (code review).</summary>
    private const string MainLandmarkMarker = "<main id=\"main-content\"";

    /// <summary>The same universal Story 1.4 landmark, exposed so the COMPOSED region path can test for it with
    /// the identical string rather than a second literal. Story 23.4's <c>CapturedRegions</c> uses it to decide
    /// whether a composed region degraded — replacing the <c>ReferenceEquals</c> sentinel the slice signalled
    /// with, which only worked because the slicer returned the very nav-markup instance it was handed and which
    /// any copy or re-concatenation would have silently broken. [Story 23.4 AC #3]</summary>
    public const string MainLandmark = MainLandmarkMarker;

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
        const string mainCloser = "</main>";
        var mainOpen = fullPageHtml.IndexOf(MainLandmarkMarker, StringComparison.Ordinal);
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
    /// Returns null when the page carries no site nav, leaving the caller on its re-rendered fallback.</para>
    /// <para>⚠️ <b>Anchored the same way <see cref="ExtractContentRegion"/> anchors its own markers</b> (code
    /// review): a match is accepted only when it precedes the page's <see cref="MainLandmarkMarker"/>. Without
    /// this, a doc page whose own PROSE quotes this exact nav markup (this file's own doc comments do, for
    /// instance) could be mistaken for the page's real chrome nav — the same self-reference class
    /// <see cref="ExtractContentRegion"/>'s own comment names.</para>
    /// [Story 22.2]</summary>
    public static string? ExtractNavMarkup(string fullPageHtml)
    {
        var mainOpen = fullPageHtml.IndexOf(MainLandmarkMarker, StringComparison.Ordinal);
        var m = NavBlockRegex.Match(fullPageHtml);
        return m.Success && (mainOpen < 0 || m.Index < mainOpen) ? m.Value : null;
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

    /// <summary>Diffs two IR manifests into the watch-mode delta document (<see cref="DeltaPath"/>) — AD-8's
    /// "sidecar polling" half, addressed by the per-page <see cref="ContentHash"/> Story 22.2 emits. Pure and
    /// side-effect-free like every other method in this file: it takes two manifest STRINGS and the caller's
    /// session facts, and returns the JSON to write. <see cref="SiteGenerator"/> owns where the bytes land and
    /// when this is called at all. [Story 22.6 AC #2/#7]
    ///
    /// <para><b>The document is a CONTRACT, not an implementation detail.</b> Story 22.5 and any future consumer
    /// bind to these field names. Changing one is a <see cref="DeltaSchemaVersion"/> bump.</para>
    ///
    /// <para><b>Degrade to FULL, loudly, rather than emit a wrong delta</b> (AC #7). Every condition under which
    /// the previous state is absent or untrustworthy resolves to <c>"full": true</c> with empty page lists,
    /// meaning "refetch the manifest". Those conditions are enforced HERE rather than at the call site precisely
    /// so no caller can forget one:</para>
    /// <list type="bullet">
    /// <item>no previous manifest — the first emit of a watch session;</item>
    /// <item><paramref name="forceFull"/> — the caller knows the basis is untrustworthy, which is how a
    /// <see cref="SiteGenerator.RegenerateTopology"/> escalation reports itself. A literal diff there would produce
    /// a thousand-entry <c>changed</c> list, larger and slower than the full payload it was meant to replace;</item>
    /// <item>either manifest is unparseable or structurally unrecognizable;</item>
    /// <item>the two manifests carry DIFFERENT <see cref="SchemaVersion"/>s — page content means something
    /// different across a schema bump (version 2 moved the content region's start marker and moved 594 pages'
    /// hashes by +30 bytes each), so a page-level diff across that boundary is meaningless;</item>
    /// <item>site-level metadata (<c>siteTitle</c>, <c>entry</c>, or the <c>nav</c> tree) differs between the two
    /// manifests even when no individual page's <see cref="ContentHash"/> moved — e.g. a retitle or a nav-label
    /// rename with zero content edits. A page-keyed diff has no way to represent that, so rather than silently
    /// shipping an empty, non-full delta that never tells the consumer the title/nav changed, this degrades to
    /// full (code review finding, Story 22.6).</item>
    /// </list>
    ///
    /// <para><b>⚠ THE TRUST BOUNDARY, stated in code because AC #7 requires it to be.</b> This delta is only ever
    /// as accurate as the manifest it is handed, and that manifest is only as accurate as
    /// <see cref="SiteGenerator"/>'s <c>_spaCapture</c> — which has a DOCUMENTED watch-mode drift class (four
    /// eviction/repopulation sites carry a <c>[deferred-work: story-6-7 watch-mode _spaCapture drift]</c> marker).
    /// A stale capture yields a stale <see cref="ContentHash"/>, which yields a false <i>unchanged</i> — a page
    /// omitted from <c>changed</c> that really did change. That is strictly worse than a false <i>changed</i>: a
    /// spurious entry costs bytes, a missing one costs correctness, and the consumer has no way to detect it.
    /// This function cannot close that gap (it never sees a page's content, only its hash); it can only refuse to
    /// widen it, which is why every uncertain case above degrades to full instead of guessing.</para></summary>
    /// <param name="previousManifestJson">The manifest emitted by the PREVIOUS regen of this watch session, or
    /// <c>null</c> for the session's first emit (⇒ full).</param>
    /// <param name="currentManifestJson">The manifest just emitted. Required.</param>
    /// <param name="sequence">Monotonic within one watch session, reset at session start. Lets a polling consumer
    /// detect a missed delta (a gap ⇒ refetch) without a clock comparison.</param>
    /// <param name="trigger">The changed path, or <see cref="FileWatcherService.TopologyEventLabel"/> for an
    /// escalated topology pass — reused verbatim rather than re-spelled, so the sidecar, the watch log and
    /// <see cref="SiteGenerator.RegenerateTopology"/> can never drift apart.</param>
    /// <param name="generatedAt">Watch-only wall clock. Never reaches a one-shot <c>generate</c> artifact: the
    /// caller does not write this file at all in that mode (NFR9).</param>
    /// <param name="forceFull">Caller-known untrustworthy basis — see the degrade list above.</param>
    public static string BuildDelta(
        string? previousManifestJson,
        string currentManifestJson,
        long sequence,
        string trigger,
        DateTimeOffset generatedAt,
        bool forceFull = false)
    {
        var current = TryReadPageIndex(currentManifestJson, out var currentSchema, out var currentSiteFingerprint);
        var previous = TryReadPageIndex(previousManifestJson, out var previousSchema, out var previousSiteFingerprint);
        // A caller-declared untrustworthy basis and a cross-schema basis are the same answer as no basis at all.
        if (forceFull || previousSchema != currentSchema) previous = null;

        // Site-level identity (title/entry/nav) is invisible to a page-keyed diff: a retitle or nav-rename with
        // zero page-content edits would otherwise produce an empty, non-full delta that never tells the consumer
        // anything changed. Only meaningful once both sides are readable; an already-null `previous` already
        // means `full` below. (code review finding, Story 22.6)
        var siteMetadataChanged = previous is not null && current is not null
            && !string.Equals(previousSiteFingerprint, currentSiteFingerprint, StringComparison.Ordinal);

        // `full` is the honest answer whenever the basis is missing, the current manifest itself could not be
        // read (a delta computed from an unreadable "current" would report every page as removed), or site-level
        // metadata moved without any page-level signal of it.
        var full = current is null || previous is null || siteMetadataChanged;

        var changed = new List<string>();
        var added = new List<string>();
        var removed = new List<string>();
        var chunks = new SortedSet<string>(StringComparer.Ordinal);

        if (!full)
        {
            // Ordinal ordering throughout so two runs over the same change emit byte-identical documents — the
            // same determinism discipline BuildDataFiles applies to chunk membership.
            foreach (var (path, entry) in current!.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!previous!.TryGetValue(path, out var before))
                {
                    added.Add(path);
                }
                else if (string.Equals(before.ContentHash, entry.ContentHash, StringComparison.Ordinal)
                    && string.Equals(before.Chunk, entry.Chunk, StringComparison.Ordinal))
                {
                    // Genuinely unchanged only when BOTH the content hash AND the chunk assignment match. A page
                    // can keep identical content but still move to a different chunk file when a sibling page
                    // earlier in the same top-level group is added/removed and BuildDataFiles' batch packer
                    // reseats the boundary — that page still needs to be named here so a consumer refetches the
                    // chunk it now actually lives in. (code review finding, Story 22.6)
                    continue;
                }
                else
                {
                    changed.Add(path);
                }
                // Only changed + added carry chunks: a REMOVED page's chunk may not exist any more, and a
                // consumer applying a removal needs no bytes to do it.
                chunks.Add(entry.Chunk);
            }
            foreach (var path in previous!.Keys.Where(k => !current!.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
            {
                removed.Add(path);
            }
        }

        var doc = new DeltaDocument(
            DeltaSchemaVersion,
            currentSchema,
            sequence,
            // Round-trip "O" against UTC so the stamp is unambiguous and machine-parseable. Serialized as a
            // STRING rather than left to the serializer's DateTimeOffset handling, which would emit a numeric
            // offset (+00:00) instead of the Z the contract specifies.
            generatedAt.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            trigger,
            full,
            changed,
            added,
            removed,
            chunks.ToList());
        return JsonSerializer.Serialize(doc, JsonOptions);
    }

    /// <summary>Reads the things <see cref="BuildDelta"/> diffs on — each page's <see cref="ContentHash"/> and
    /// its chunk, the manifest's own <see cref="SchemaVersion"/>, and a site-level identity fingerprint
    /// (<paramref name="siteFingerprint"/>, covering <c>siteTitle</c>/<c>entry</c>/<c>nav</c>). Returns
    /// <c>null</c> for ANY unusable input (absent, malformed, or structurally unrecognizable) rather than
    /// throwing: a delta computation is a best-effort optimization sitting on a watch loop, and NFR2 says degrade,
    /// never amplify. Every null here becomes a <c>"full": true</c> document, which is always correct — merely
    /// expensive.</summary>
    private static Dictionary<string, DeltaPage>? TryReadPageIndex(
        string? manifestJson, out int schemaVersion, out string? siteFingerprint)
    {
        schemaVersion = 0;
        siteFingerprint = null;
        if (manifestJson is not { Length: > 0 }) return null;
        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            schemaVersion = doc.RootElement.TryGetProperty("schemaVersion", out var sv) && sv.TryGetInt32(out var v)
                ? v
                // A manifest with no schemaVersion key at all is the pre-22.2 shape — version 0 by implication,
                // exactly as SchemaVersion's own doc comment defines it.
                : 0;
            if (!doc.RootElement.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Site-level identity, independent of any single page's hash — a retitle or nav-rename with zero
            // page-content edits still needs to force `full` (see BuildDelta). Serialized as a small JSON array
            // rather than delimiter-joined, so no field's own content can ever be mistaken for a separator.
            var siteTitle = doc.RootElement.TryGetProperty("siteTitle", out var st) && st.ValueKind == JsonValueKind.String
                ? st.GetString() ?? string.Empty
                : string.Empty;
            var entry = doc.RootElement.TryGetProperty("entry", out var en) && en.ValueKind == JsonValueKind.String
                ? en.GetString() ?? string.Empty
                : string.Empty;
            var navJson = doc.RootElement.TryGetProperty("nav", out var nav) ? nav.GetRawText() : string.Empty;
            siteFingerprint = JsonSerializer.Serialize(new[] { siteTitle, entry, navJson });

            var index = new Dictionary<string, DeltaPage>(StringComparer.Ordinal);
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.ValueKind != JsonValueKind.Object) return null;
                if (!page.Value.TryGetProperty("contentHash", out var h) || h.ValueKind != JsonValueKind.String) return null;
                if (!page.Value.TryGetProperty("chunk", out var c) || c.ValueKind != JsonValueKind.String) return null;
                index[page.Name] = new DeltaPage(h.GetString()!, c.GetString()!);
            }
            return index;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private readonly record struct DeltaPage(string ContentHash, string Chunk);

    /// <summary>The on-the-wire shape of <see cref="DeltaPath"/>. Property ORDER is the emitted field order and
    /// is part of the readable contract; property NAMES are camelCased by <see cref="JsonOptions"/>.</summary>
    /// <param name="SchemaVersion">The IR <see cref="SpaDelivery.SchemaVersion"/> this delta was computed against.
    /// A consumer holding state from a different IR schema must refetch rather than apply.</param>
    /// <param name="Full"><c>true</c> ⇒ every list below is empty and the consumer must refetch the manifest.</param>
    private sealed record DeltaDocument(
        int DeltaSchemaVersion,
        int SchemaVersion,
        long Sequence,
        string GeneratedAt,
        string Trigger,
        bool Full,
        IReadOnlyList<string> Changed,
        IReadOnlyList<string> Added,
        IReadOnlyList<string> Removed,
        IReadOnlyList<string> Chunks);

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
                // Whitespace-only counts as absent (code review): { Length: > 0 } alone let a blank
                // "content=\" \"" ship instead of falling back to the title, same fallback PathUtil.RenderHeadOpen
                // applies when it builds the tag.
                new ManifestHead(page.Title, page.MetaDescription is { Length: > 0 } d && !string.IsNullOrWhiteSpace(d) ? d : page.Title),
                ExtractScriptIslands(page.ContentHtml),
                ContentHash(page.ContentHtml),
                // The page's own JSON-ENCODED byte count — the same exact measurement (not raw UTF-8 content
                // bytes) the chunk ceiling above budgets against, reusing the token already produced for it so
                // this costs no extra serialization. Raw content bytes under-report by up to 6x on escape-heavy
                // regions (</>/& each balloon to 6 bytes), which is exactly the imprecision Story 22.2 closed for
                // the chunk ceiling; `bytes` shipped that same imprecision until this fix (code review).
                Encoding.UTF8.GetByteCount(encoded[page.OutputRelativePath].ValueJson));
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
    /// <summary>The Quiet Stamp's id — the single hook the client updates and the one selector a test asserts on.</summary>
    public const string LiveStampId = "spa-live-stamp";

    /// <summary>The "Quiet Stamp" (Story 22.6 AC #5): a small, motionless line of page chrome that reports the live
    /// delta channel's state as WORDS. Present in the initial server-rendered markup so it is not a JS-only
    /// artifact, and updated in place by the client (<c>textContent</c> only — no element insertion or removal, so
    /// there is no layout shift on update).
    ///
    /// <para><b>State is never signalled by color or motion</b> (CLAUDE.md § Verification: no state by color alone).
    /// The text itself carries the state — "Live updates: unavailable" vs "Live updates: connected · updated
    /// 14:32" — so it reads identically to a screen reader, a monochrome display, and a colorblind reader. There is
    /// deliberately no <c>--motion-*</c> token here and no <c>prefers-reduced-motion</c> block: a stamp that never
    /// animates needs no reduced-motion variant, and adding one would imply motion exists.</para>
    ///
    /// <para><b>Server-rendered as "unavailable", not "connecting".</b> With JS off — or with a stale cached
    /// script — the stamp is never updated, and "unavailable" is then TRUE while "connecting…" would be a
    /// permanent lie. Honest at rest is worth more than optimistic on arrival.</para>
    ///
    /// <para><b>Why it lives here and NOT in <see cref="PathUtil.RenderHeadOpen"/></b>: that helper is shared with
    /// every static page, and a static page has no live channel — putting the stamp there would both claim a
    /// capability those pages do not have and move every page's bytes, breaking AC #4's byte-identity and the
    /// <c>GoldenContentFingerprint</c> gate. The SPA entry shell and the webview chrome are the only two surfaces
    /// that ever have a delta channel, so they are the only two that carry it.</para>
    ///
    /// <para><c>aria-live="polite"</c> announces an update without stealing focus; <c>role="status"</c> gives it
    /// the right semantics for assistive tech reading a state line rather than a heading.</para></summary>
    public const string LiveStampMarkup =
        "<p class=\"spa-live-stamp\" id=\"" + LiveStampId + "\" role=\"status\" aria-live=\"polite\">"
        + "Live updates: unavailable</p>\n";

    public static string BuildEntryShell(string siteTitle, string dashboardRegion)
    {
        var description =
            $"Single-page delivery of {siteTitle} — the same C#-rendered content as the static site, navigated "
            + "client-side. Works without JavaScript via the static site.";

        var sb = new StringBuilder();
        // Reuse the canonical head (title, meta/OG, favicon, versioned specscribe.css + specscribe.js, skip link,
        // <body>) so the SPA shell can never drift from the static pages' chrome.
        sb.Append(PathUtil.RenderHeadOpen(siteTitle, ForgeOptions.StylesheetName, ForgeOptions.ScriptName, description));
        sb.Append(LiveStampMarkup);
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
    /// the number that exceeds the ceiling. This is the whole assembled chunk (key + value + envelope), whereas
    /// the page entry's own <c>bytes</c> field measures just that page's JSON-encoded content VALUE — both are
    /// exact-encoded now (code review), so they agree on unit, just not on scope.</param>
    private sealed record ManifestOversizedPage(string Path, int ChunkBytes);

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
