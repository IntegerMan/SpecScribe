using System.Text.Json;
using SpecScribe;
using SpecScribe.WebviewSpike;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Story 6.3 VS Code Integration SPIKE — throwaway webview renderer.
//
// Proves the "C# core renders the webview HTML" data path: it reuses the SAME host-neutral view models the HTML
// surface uses (BmadArtifactAdapter ingest → DashboardViewBuilder / EpicsViewBuilder → HtmlRenderAdapter body
// render) and wraps the resulting section bodies in a webview-safe document (WebviewShell). It NEVER scrapes the
// generated static site and NEVER re-parses .md in the extension (AD-1/AD-2 — the extension gets finished HTML).
//
// Usage:
//   specscribe-webview-spike [projectDir]            → prints JSON {dashboard, epics, dashboardBody, epicsBody,
//                                                        siteTitle} on stdout (what the thin TS shim consumes)
//   specscribe-webview-spike [projectDir] --out DIR  → also writes dashboard.html / epics.html to DIR for manual
//                                                        inspection (used to eyeball CSP survival during the spike)
//
// Reduced input set on purpose: docs/adrs/coverage are best-effort / omitted so the spike stays small; the
// charts, stat tiles, now/next board, funnel, git pulse, requirements and epics surfaces all render from the
// REAL ingested models. The full input set is available through the identical builder call — that completeness
// is Story 6.4's (the runtime), not the spike's.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

try
{
    var startDir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
    var outDir = GetOption(args, "--out");

    var options = ForgeOptions.Resolve(startDirectory: startDir ?? Directory.GetCurrentDirectory());
    var css = LoadEmbeddedCss();

    var files = Directory.Exists(options.SourceRoot)
        ? Directory.EnumerateFiles(options.SourceRoot, "*.md", SearchOption.AllDirectories)
            .Where(f => !PathUtil.IsIgnoredSourceFile(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList()
        : new List<string>();

    // Real ingest through the framework adapter — the exact seam SiteGenerator uses (AD-1). Progress enrichment
    // (task roll-up + git pulse) rides the same projection callback the generator supplies.
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
    var epicsBody = bundle.Epics is { } epicsModel
        ? HtmlRenderAdapter.Shared.RenderEpicsIndexBody(
            EpicsViewBuilder.BuildIndex(epicsModel, progress, nav, bundle.Module.Commands))
        : "<main id=\"main-content\">\n<header class=\"doc-header\"><h1>Epics &amp; Stories</h1></header>\n"
          + "<p style=\"padding:1rem\">No <code>epics.md</code> found in this project.</p>\n</main>\n";

    var dashboardHtml = WebviewShell.Wrap($"{options.SiteTitle} — Dashboard", dashboardBody, css, "dashboard");
    var epicsHtml = WebviewShell.Wrap($"{options.SiteTitle} — Epics", epicsBody, css, "epics");

    if (outDir is { Length: > 0 })
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "dashboard.html"), dashboardHtml);
        File.WriteAllText(Path.Combine(outDir, "epics.html"), epicsHtml);
        Console.Error.WriteLine($"[spike] wrote dashboard.html + epics.html to {Path.GetFullPath(outDir)}");
        Console.Error.WriteLine($"[spike] project: {options.SiteTitle}  |  source: {options.SourceRoot}  |  {files.Count} md files, {bundle.Epics?.Epics.Count ?? 0} epics");
    }
    else
    {
        // What the thin TS shim consumes in one spawn: full webview docs for the initial webview.html set, plus
        // the bare section bodies for in-place live-push (postMessage → surface.innerHTML), no full panel reset.
        var payload = JsonSerializer.Serialize(new
        {
            siteTitle = options.SiteTitle,
            dashboard = dashboardHtml,
            epics = epicsHtml,
            dashboardBody,
            epicsBody,
        });
        Console.Out.Write(payload);
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[spike] ERROR: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

static string? GetOption(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string LoadEmbeddedCss()
{
    // The stylesheet is an embedded resource on the SpecScribe assembly (same resource SiteGenerator copies to
    // the output root). Loading it here inlines the EXACT production CSS into the webview — the spike changes no
    // byte of it, so any CSS incompatibility we see is a real host-theming finding for Story 6.5, not an artifact.
    var asm = typeof(SiteGenerator).Assembly;
    using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe.css")
        ?? throw new InvalidOperationException("Embedded specscribe.css not found on the SpecScribe assembly.");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

// Best-effort standalone docs for the dashboard's home-index bands — mirrors SiteGenerator.GenerateOneInternal's
// convert step for every non-consumed, non-epics .md. A doc that won't parse is skipped (spike-only tolerance);
// companions/reference-linkification are intentionally not reproduced (they don't affect the index bands).
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
        try
        {
            docs.Add(MarkdownConverter.Convert(file, rel, PathUtil.ToOutputRelative(rel)));
        }
        catch
        {
            // spike: a doc that won't convert is simply omitted from the index bands.
        }
    }
    return docs;
}
