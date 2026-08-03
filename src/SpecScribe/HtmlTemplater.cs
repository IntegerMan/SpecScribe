using System.Text;

namespace SpecScribe;

/// <summary>Wraps rendered markdown bodies in full HTML pages, and builds the generated site's index.</summary>
public static class HtmlTemplater
{
    /// <summary>Optional parent + deferred chrome for quick-dev (<c>route: one-shot</c>) doc pages.
    /// Additive on the generic doc template — no second page pipeline. [artifact-review-nav]</summary>
    public sealed record QuickDevPageChrome(
        IReadOnlyList<FollowUpDeferredSlot> DeferredFromThis,
        int? EpicNumber = null,
        string? EpicCrumbLabel = null,
        string? EpicHref = null,
        string? UnplannedHref = null,
        string? DeferredListHref = null);

    /// <summary>Builds a generic doc page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract, and
    /// the single largest family this story moves (every <c>adrs/</c>, <c>implementation-artifacts/</c>,
    /// <c>planning-artifacts/</c> and <c>specs/</c> page). Story 23.4 moved every standalone templater onto it so
    /// the IR's content region can be COMPOSED (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup +
    /// wayfinding + body) instead of sliced back out of a rendered full page. <see cref="RenderPage"/> is the
    /// unchanged HTML projection of this same model, so the bytes are identical.
    /// <para>The TOC active-section script stays chrome-level: <see cref="HtmlRenderAdapter.Render"/> emits it when
    /// the body carries a <c>toc-sidebar</c>. That is the old <c>tocEntries.Count &gt; 0</c> condition for every
    /// page in this repo, and the golden gate is what proves it — <see cref="Toc.RenderSidebar"/> emits the marker
    /// unconditionally, so a page with a companion rail but NO headings would be the one shape where the two rules
    /// could disagree. [Story 23.4 AC #3]</para></summary>
    public static PageView BuildDocPage(DocModel doc, SiteNav nav, EntityPager? pager = null, QuickDevPageChrome? quickDev = null, NavLocalContext? localContext = null)
    {
        var prefix = PathUtil.RelativePrefix(doc.OutputRelativePath);
        var cssHref = prefix + ForgeOptions.StylesheetName;
        var scriptHref = prefix + ForgeOptions.ScriptName;

        var sb = new StringBuilder();
        // Main content (header + article) is composed separately so it can be wrapped in the two-column page
        // shell alongside the TOC sidebar. Source render order equals DocModel.Headings order for a straight
        // full-page render, so those headings feed the shared TOC seam directly.
        var main = new StringBuilder();
        main.Append("<header class=\"doc-header\">\n");
        main.Append($"  <h1>{Html(doc.Title)}</h1>\n");
        if (doc.Frontmatter.Project is { Length: > 0 } project)
        {
            main.Append($"  <div class=\"doc-subtitle\">{Html(project)}</div>\n");
        }
        main.Append("  <div class=\"meta-pills\">\n");
        AppendPill(main, doc.Frontmatter.Author, "author");
        // Story 10.4: normalize parseable authored dates through PortalDates; free text stays verbatim (NFR8).
        AppendPill(main,
            doc.Frontmatter.Date is { Length: > 0 } d ? PortalDates.ReformatAuthored(d) : null,
            "date");
        AppendPill(main, doc.Frontmatter.Version, v => $"v{v}");
        AppendStatusPill(main, doc.Frontmatter.Status);
        main.Append($"    <span class=\"pill\">{Html(PathUtil.NormalizeSlashes(doc.SourceRelativePath))}</span>\n");
        main.Append("  </div>\n</header>\n\n");

        // Spec-kernel bodies get the Capabilities list restyled into scannable cards; every other page's body
        // passes through untouched (the styler no-ops without the authored CAP pattern anyway). [Story 2.2 polish]
        var bodyHtml = IsSpecKernelPage(doc) ? CapabilityStyler.Style(doc.BodyHtml) : doc.BodyHtml;
        main.Append("<article class=\"doc-body\">\n");
        main.Append(bodyHtml);
        main.Append("\n</article>\n");

        if (quickDev is { DeferredFromThis.Count: > 0 })
        {
            main.Append("\n<section class=\"dashboard-narrow\">\n");
            main.Append(FollowUpRow.RenderDeferredFromArtifactPanel(
                quickDev.DeferredFromThis, deferredListHref: quickDev.DeferredListHref));
            main.Append("</section>\n");
        }

        var tocEntries = doc.Headings
            .Where(h => h.Level is 2 or 3)
            .Select(h => new Toc.Entry(h.Level, h.Text, h.Id))
            .ToList();
        if (quickDev is { DeferredFromThis.Count: > 0 })
            tocEntries.Add(new Toc.Entry(2, "Deferred from this artifact", "sec-deferred-from-artifact"));

        sb.Append("<main id=\"main-content\">\n");
        // The companion-docs cross-link block (spec-kernel pages only) rides in the sidebar rail beneath the
        // TOC, not the content column, so it reads as a related-docs sidebar. [Story 2.2 polish]
        sb.Append(Toc.WrapWithSidebar(main.ToString(), tocEntries, BuildCompanionRail(doc)));
        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.Doc,
            OutputRelativePath = doc.OutputRelativePath,
            Title = $"{doc.Title} — {nav.SiteTitle}",
            MetaDescription = $"{doc.Title} — living documentation for {nav.SiteTitle}, generated by SpecScribe.",
            Nav = nav.ToNavigationView(doc.OutputRelativePath, localContext),
            Breadcrumb = BreadcrumbTrail.From(BuildDocCrumbs(doc, quickDev)),
            // Sibling pager (ADR record pages pass one; every other doc passes null → byte-identical) rides the
            // same coherent wayfinding strip as the breadcrumb now, not the body's own header. [Story 10.11]
            Pager = pager,
            Assets = new AssetManifest
            {
                StylesheetHref = cssHref,
                ScriptHref = scriptHref,
                MermaidNeeded = doc.HasMermaid,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }

    /// <summary>Home → (Epics → Epic | Unplanned) → title when quick-dev chrome supplies a parent;
    /// otherwise Home → title (unchanged for ordinary docs).</summary>
    private static (string, string?)[] BuildDocCrumbs(DocModel doc, QuickDevPageChrome? quickDev)
    {
        if (quickDev is null)
            return [("Home", "index.html"), (doc.Title, null)];

        if (quickDev.EpicNumber is { } en
            && quickDev.EpicCrumbLabel is { Length: > 0 } label
            && quickDev.EpicHref is { Length: > 0 } epicHref)
        {
            return
            [
                ("Home", "index.html"),
                ("Epics", SiteNav.EpicsOutputPath),
                (label, epicHref),
                (doc.Title, null),
            ];
        }

        if (quickDev.UnplannedHref is { Length: > 0 } unplannedHref)
        {
            return
            [
                ("Home", "index.html"),
                ("Unplanned", unplannedHref),
                (doc.Title, null),
            ];
        }

        return [("Home", "index.html"), (doc.Title, null)];
    }

    /// <summary>True for a page generated from a doc under the <c>specs/</c> kernel directory — the pages that
    /// get the capability-card and companion-rail treatment. Keyed by directory, disjoint from Story 2.1's
    /// <c>implementation-artifacts/spec-*.md</c>. [Story 2.2 polish]</summary>
    private static bool IsSpecKernelPage(DocModel doc) =>
        PathUtil.NormalizeSlashes(doc.SourceRelativePath).StartsWith("specs/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the "Companion documents" related-docs nav for the sidebar rail from a spec page's
    /// resolved companions, or null when there are none (so the rail shows only the TOC, or nothing). Each
    /// entry was resolved to a real generated page by the SiteGenerator — never a broken link. [Story 2.2 polish]</summary>
    private static string? BuildCompanionRail(DocModel doc)
    {
        if (doc.Companions.Count == 0) return null;

        var sb = new StringBuilder();
        sb.Append("<nav class=\"companion-docs\" aria-label=\"Companion documents\">\n");
        sb.Append("  <span class=\"companion-label\">Companion documents</span>\n");
        sb.Append("  <ul>\n");
        foreach (var (label, href) in doc.Companions)
        {
            sb.Append($"    <li><a href=\"{Html(href)}\">{Html(label)}</a></li>\n");
        }
        sb.Append("  </ul>\n</nav>\n");
        return sb.ToString();
    }

    /// <summary>Whether a top-level source folder is one of the well-known home-index groups — the signal the
    /// generator uses to emit an "unrecognized structure" notice for anything else (whose docs degrade to
    /// their own coherently-titled band rather than a silent dump). Delegates to the rendering-core builder that
    /// now owns the group set (Story 6.2 relocated <c>KnownIndexGroups</c> there). [Story 4.2 Task 3/5]</summary>
    public static bool IsWellKnownTopLevelFolder(string folder) =>
        DashboardViewBuilder.IsWellKnownTopLevelFolder(folder);

    /// <summary>Builds the dashboard's <see cref="PageView"/> without committing to a surface — the mechanical
    /// split of <see cref="RenderIndex"/> (which now just feeds this through the HTML adapter, bytes unchanged)
    /// that lets the webview surface render the SAME page model through its own <see cref="IRenderAdapter"/>
    /// instead of duplicating the view/PageView assembly. [Story 6.4]</summary>
    public static PageView BuildIndexPage(IReadOnlyList<DocModel> docs, SiteNav nav, ProgressModel progress, EpicsModel? epicsModel, RequirementsModel? requirements, IReadOnlyList<AdrEntry> adrs, CommandCatalog commands, WorkInventory? work = null, SprintStatus? sprint = null, IReadOnlyList<RetroModel>? retros = null, ArtifactCoverage? coverage = null, bool hasTimeline = false, Func<string, string?>? codeItemHref = null, ProjectCounts? counts = null, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null, DateOnly? today = null, DeliveryCadenceData? cadence = null, WorkGraphModel? workGraph = null, DateOnly? dateCutoff = null, TestArtifactsModel? testArtifacts = null)
    {
        var inventory = work ?? WorkInventory.Empty;

        var view = DashboardViewBuilder.Build(nav, progress, epicsModel, requirements, commands, inventory, sprint, coverage, hasTimeline, counts, followUps, unplanned, cadence, workGraph, testArtifacts);
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view, codeItemHref, today, dateCutoff);

        // Home carries a descriptive title (sub-pages are already "Title — Site") + OG/description, no
        // breadcrumb, and its one drill child is the Epics index (when epics exist). [Story 1.5 G2; Story 6.1]
        var page = new PageView
        {
            Kind = PageKind.Home,
            OutputRelativePath = SiteNav.HomeOutputPath,
            Title = $"{nav.SiteTitle} — Project Dashboard",
            MetaDescription = $"Project dashboard and living documentation for {nav.SiteTitle}, generated by SpecScribe.",
            Nav = nav.ToNavigationView(SiteNav.HomeOutputPath) with
            {
                // NFR8: no epics model → Overview-only strip (omit empty stages). [Story 9.8]
                FullHomeWorkModeStrip = epicsModel is not null,
            },
            Breadcrumb = BreadcrumbTrail.Empty,
            Assets = new AssetManifest
            {
                StylesheetHref = ForgeOptions.StylesheetName,
                ScriptHref = ForgeOptions.ScriptName,
                MermaidNeeded = Mermaid.ContainsBlock(body),
                // Same discipline as MermaidNeeded: derived from the rendered body, so the flag cannot claim a
                // 1.2 MB engine a page does not host (nor omit one it does). [Story 20.5]
                HierarchyEngineNeeded = HierarchyExplorer.ContainsHost(body),
                // Both: this family emits the boot marker INLINE (pre-body) and pulls the engine. [Story 23.4]
                HierarchyBootInline = HierarchyExplorer.ContainsHost(body),
            },
            Interaction = new InteractionState
            {
                ChildTargets = nav.HasEpics ? new[] { SiteNav.EpicsOutputPath } : Array.Empty<string>(),
            },
            BodyHtml = body,
        };
        return page;
    }

    private static void AppendPill(StringBuilder sb, string? value, string _)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append($"    <span class=\"pill\">{Html(value)}</span>\n");
    }

    private static void AppendPill(StringBuilder sb, string? value, Func<string, string> format)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append($"    <span class=\"pill\">{Html(format(value))}</span>\n");
    }

    private static void AppendStatusPill(StringBuilder sb, string? status)
    {
        if (string.IsNullOrEmpty(status)) return;
        // Lifecycle frontmatter (done / in-progress / …) uses the shared status-badge vocabulary
        // so "done" reads green like every other surface — not a grey generic pill. [Story 10.8: FreeTextBadge]
        sb.Append("    ").Append(StatusStyles.FreeTextBadge(status)).Append('\n');
    }

    private static string Html(string s) => PathUtil.Html(s);
}
