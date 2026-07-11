using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Story 6.6 DELIVERY-ARCHITECTURE SPIKE — throwaway JSON-data-layer exporter + bloat meter (axis A).
//
// Reuses the SAME host-neutral view-model path the HTML surface uses (BmadArtifactAdapter ingest →
// DashboardViewBuilder / EpicsViewBuilder → HtmlRenderAdapter body render). It NEVER scrapes generated .html and
// NEVER re-parses .md (AD-1/AD-2). It answers ONE question with numbers: if SpecScribe shipped a JSON data layer
// + a client renderer instead of one static HTML file per artifact, what does that cost in bytes?
//
// It emits, to --out DIR:
//   data.json        the STRUCTURED data layer — the section records that round-trip cleanly (Story 6.2's DATA:
//                    stat tiles, progress bars, now/next cards, index bands, quick links, epic chips, counts).
//                    This is what a TS/SPA renderer would consume to rebuild the NON-chart sections itself.
//   bodies.json      { dashboardBody, epicsBody } — the FULL pre-rendered section bodies (C# renders the HTML).
//                    This is the payload a "JSON layer that just carries finished HTML" (thin client) ships.
//   (the SPA under spike/delivery/spa consumes both.)
//
// And prints a MEASUREMENT REPORT to stderr: the 3-way byte split that decides axis A —
//   (1) full pre-rendered body bytes  (what C#-renders-HTML ships, ≈ today's static page)
//   (2) structured data-layer bytes   (the floor a TS-renders-everything approach could reach for non-chart data)
//   (3) inline-SVG bytes inside (1)    (the chart mass a JSON layer either SHIPS pre-rendered or must RE-GENERATE
//                                       in TS — the port cost). If (3) dominates (1), a JSON+thin-client layer
//                                       does NOT shrink the payload; only porting chart generation to TS does.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

try
{
    var startDir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
    var outDir = GetOption(args, "--out") ?? "delivery-out";

    var options = ForgeOptions.Resolve(startDirectory: startDir ?? Directory.GetCurrentDirectory());

    var files = Directory.Exists(options.SourceRoot)
        ? Directory.EnumerateFiles(options.SourceRoot, "*.md", SearchOption.AllDirectories)
            .Where(f => !PathUtil.IsIgnoredSourceFile(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList()
        : new List<string>();

    // Real ingest through the framework adapter — the exact seam SiteGenerator uses (AD-1).
    var adapter = new BmadArtifactAdapter();
    ProgressModel? progress = null;
    var bundle = adapter.Ingest(options, files, (model, artifactsById) =>
        progress = ProgressCalculator.Compute(model, artifactsById, GitMetrics.TryCompute(options.RepoRoot)));
    progress ??= ProgressModel.Empty;

    var sourceRelatives = files
        .Select(f => PathUtil.NormalizeSlashes(Path.GetRelativePath(options.SourceRoot, f)))
        .ToList();
    var nav = SiteNav.Build(
        sourceRelatives, options.SiteTitle, bundle.Module.Docs,
        hasAdrs: false, hasReadme: false, hasSprint: bundle.Sprint is not null,
        hasStructure: sourceRelatives.Count > 0);

    // ── Dashboard surface: identical builder + adapter path as HtmlTemplater.RenderIndex ──────────────────────
    var docs = BuildDocs(options, files, bundle);
    var work = WorkInventory.Build(docs);
    var dashboardView = DashboardViewBuilder.Build(
        docs, nav, progress, bundle.Epics, bundle.Requirements,
        adrs: Array.Empty<AdrEntry>(), commands: bundle.Module.Commands, work: work,
        sprint: bundle.Sprint, retros: bundle.Retros, coverage: null);
    var dashboardBody = HtmlRenderAdapter.Shared.RenderDashboardBody(dashboardView);

    // ── Epics surface: identical builder + adapter path as EpicsTemplater.RenderIndex ─────────────────────────
    var epicsView = bundle.Epics is { } epicsModel
        ? EpicsViewBuilder.BuildIndex(epicsModel, progress, nav, bundle.Module.Commands)
        : null;
    var epicsBody = epicsView is not null
        ? HtmlRenderAdapter.Shared.RenderEpicsIndexBody(epicsView)
        : "<main id=\"main-content\"><p>No epics.md found.</p></main>";

    // ── (1) The STRUCTURED data layer: only the section records that are pure DATA (Story 6.2). The chart/rich
    //        panels carry already-projected DOMAIN INPUT (EpicsModel/ProgressModel/CommandCatalog) that does not
    //        round-trip (memory: story-6-2-section-view-models-live), so they are DELIBERATELY excluded here —
    //        their bytes live in the pre-rendered body (2) as inline SVG. That exclusion is the whole point: it
    //        measures how thin the *data* layer is once the charts are removed from it. ──────────────────────────
    var dataLayer = new
    {
        dashboard = new
        {
            dashboardView.SiteTitle,
            dashboardView.StatTiles,
            dashboardView.ProgressBars,
            nowNextCards = dashboardView.NowNext?.Cards,          // sprint-board mode is a chart panel → excluded
            dashboardView.QuickLinks,
            dashboardView.IndexBands,
            dashboardView.OpenRetroActionItems,
        },
        epics = epicsView is null ? null : new
        {
            epicsView.SiteTitle,
            epicsView.EpicCount,
            epicsView.DraftedCount,
            epicsView.VerticalSliceChips,
            epicsView.FurtherDevelopmentChips,
        },
    };
    var jsonOpts = new JsonSerializerOptions { WriteIndented = false };
    var dataJson = JsonSerializer.Serialize(dataLayer, jsonOpts);
    var bodiesJson = JsonSerializer.Serialize(new { dashboardBody, epicsBody }, jsonOpts);

    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "data.json"), dataJson);
    File.WriteAllText(Path.Combine(outDir, "bodies.json"), bodiesJson);

    // ── (3) Chart mass inside the pre-rendered bodies ─────────────────────────────────────────────────────────
    var (dashSvgN, dashSvgBytes) = SvgMass(dashboardBody);
    var (epicSvgN, epicSvgBytes) = SvgMass(epicsBody);

    int Bytes(string s) => System.Text.Encoding.UTF8.GetByteCount(s);
    var dashBodyBytes = Bytes(dashboardBody);
    var epicBodyBytes = Bytes(epicsBody);
    var dataBytes = Bytes(dataJson);

    var report = new
    {
        note = "Story 6.6 axis-A measurement — all byte counts are UTF-8. Compare against the static-site figures printed by the harness script.",
        sourceMarkdownFiles = files.Count,
        epics = bundle.Epics?.Epics.Count ?? 0,
        dashboard = new
        {
            preRenderedBodyBytes = dashBodyBytes,      // (1) what C#-renders-HTML ships for this surface
            inlineSvgCount = dashSvgN,
            inlineSvgBytes = dashSvgBytes,             // (3) the chart mass a JSON layer ships-as-string OR ports to TS
            svgShareOfBodyPct = Math.Round(100.0 * dashSvgBytes / dashBodyBytes, 1),
        },
        epicsIndex = new
        {
            preRenderedBodyBytes = epicBodyBytes,
            inlineSvgCount = epicSvgN,
            inlineSvgBytes = epicSvgBytes,
            svgShareOfBodyPct = Math.Round(100.0 * epicSvgBytes / epicBodyBytes, 1),
        },
        structuredDataLayerBytes = dataBytes,          // (2) the floor: both surfaces' NON-chart data, one blob
        interpretation = new[]
        {
            "(1) pre-rendered body ≈ the static HTML page a JSON+thin-client layer would still ship (charts inlined).",
            "(2) structured data layer is what a TS/SPA renderer consumes to rebuild the NON-chart sections itself.",
            "(3) inline SVG is the chart mass. It is IN (1) but NOT in (2). A JSON+thin-client layer must ship it as",
            "    a pre-rendered string (payload ≈ (1)); only porting chart generation to TS removes it (payload → ~(2)+raw chart data).",
        },
    };
    Console.Error.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.Error.WriteLine($"[spike] wrote data.json ({dataBytes:N0} B) + bodies.json to {Path.GetFullPath(outDir)}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[spike] ERROR: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

static (int count, int bytes) SvgMass(string html)
{
    var count = 0; var bytes = 0;
    foreach (Match m in Regex.Matches(html, "<svg\\b.*?</svg>", RegexOptions.Singleline))
    {
        count++;
        bytes += System.Text.Encoding.UTF8.GetByteCount(m.Value);
    }
    return (count, bytes);
}

static string? GetOption(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// Best-effort standalone docs for the dashboard's home-index bands — mirrors the 6.3 spike's BuildDocs.
static List<DocModel> BuildDocs(ForgeOptions options, IReadOnlyList<string> files, ArtifactBundle bundle)
{
    var consumed = new HashSet<string>(bundle.ConsumedSourceRelatives, StringComparer.OrdinalIgnoreCase);
    var docs = new List<DocModel>();
    foreach (var file in files)
    {
        if (bundle.EpicsSourceFullPath is { } ep && string.Equals(file, ep, StringComparison.OrdinalIgnoreCase))
            continue;
        var rel = Path.GetRelativePath(options.SourceRoot, file);
        if (consumed.Contains(rel)) continue;
        try { docs.Add(MarkdownConverter.Convert(file, rel, PathUtil.ToOutputRelative(rel))); }
        catch { /* spike: a doc that won't convert is omitted from the index bands. */ }
    }
    return docs;
}
