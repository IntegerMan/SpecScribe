using SpecScribe;

namespace SpecScribe.Tests;

public class SiteNavTests
{
    [Fact]
    public void Build_QuickLinkGroups_MatchTheKeyViewsBandTaxonomy()
    {
        // Story 10.1 deferred debt: SiteNav.QuickLinks' Group is now the single source of truth for the white
        // key-views band classification that HtmlRenderAdapter's (now-deleted) KeyViewGroup switch used to
        // derive independently. Pin every known label's Group here so a future call site can't silently drift
        // from this taxonomy (Delivery/Insights/Follow-ups; everything else defaults to Project).
        var nav = SiteNav.Build(
            new[] { "planning-artifacts/epics.md" }, "SpecScribe",
            hasAdrs: true, hasReadme: true, hasSprint: true, hasCodeMap: true,
            hasActionItems: true, hasDeferredWork: true, deferredWorkOutputPath: "deferred-work.html");
        var groupByLabel = nav.QuickLinks.ToDictionary(q => q.Label, q => q.Group);

        Assert.Equal("Delivery", groupByLabel["Epics"]);
        Assert.Equal("Delivery", groupByLabel["Requirements"]);
        Assert.Equal("Delivery", groupByLabel["Traceability"]);
        Assert.Equal("Delivery", groupByLabel["Sprint"]);
        Assert.Equal("Insights", groupByLabel["Code Map"]);
        Assert.Equal("Insights", groupByLabel["Risk Quadrant"]);
        Assert.Equal("Follow-ups", groupByLabel["Action Items"]);
        Assert.Equal("Follow-ups", groupByLabel["Deferred Work"]);
        Assert.Equal("Help", groupByLabel["How to use SpecScribe"]);
        Assert.Equal("Help", groupByLabel["About Spec-Driven Development"]);
        Assert.Equal("Help", groupByLabel["About"]);
        Assert.Equal("Help", groupByLabel["Logs"]);
        Assert.Equal("Project", groupByLabel["Readme"]);
        Assert.Equal("Project", groupByLabel["ADRs"]);
    }

    [Fact]
    public void Build_OmitsMissingArtifactClasses()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        // Help is always present (How to use / About SDD / About / Logs); Project omits when empty.
        Assert.Equal(new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" }, nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_IncludesSourceDerivedAndAdrLinksWhenAvailable()
    {
        var nav = SiteNav.Build(new[]
        {
            "gdd.md",
            "narrative-design.md",
            "game-architecture.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.GameDevStudio), hasAdrs: true);

        // Journey order: Home → Delivery → Project → Help (How to use / About SDD / About / Logs).
        Assert.Equal(
            new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "GDD", "Narrative", "Game Architecture", "ADRs", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" },
            nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_IncludesReadmeInProjectGroupWhenAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasReadme: true);

        // Readme sits in Project (after Delivery); Help trails with How to use / About SDD / About / Logs.
        Assert.Equal(new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "Readme", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" }, nav.Items.Select(i => i.Label).ToArray());
        Assert.Equal(SiteNav.ReadmeOutputPath, nav.Items.First(i => i.Label == "Readme").OutputRelativePath);
        Assert.True(nav.HasReadme);
    }

    [Fact]
    public void Build_OmitsReadmeWhenNotAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasReadme: false);

        Assert.DoesNotContain("Readme", nav.Items.Select(i => i.Label));
        Assert.False(nav.HasReadme);
    }

    [Fact]
    public void Build_AddsSprintItemAndQuickLinkNextToDeliveryViewsWhenAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        // Help (How to use / About SDD / About / Logs) follows Delivery in group order.
        Assert.Equal(new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "Sprint", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" }, nav.Items.Select(i => i.Label).ToArray());
        Assert.Equal(SiteNav.SprintOutputPath, nav.Items.First(i => i.Label == "Sprint").OutputRelativePath);
        Assert.True(nav.HasSprint);

        var sprintQuick = Assert.Single(nav.QuickLinks, q => q.Label == "Sprint");
        Assert.Equal(SiteNav.SprintOutputPath, sprintQuick.OutputRelativePath);

        var delivery = Assert.Single(nav.Groups, g => g.Label == "Delivery");
        Assert.Equal(new[] { "Epics", "Requirements", "Traceability", "Cadence", "Sprint" }, delivery.Children.Select(c => c.Label).ToArray());
    }

    [Fact]
    public void Build_OmitsSprintWhenNotAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: false);

        Assert.DoesNotContain("Sprint", nav.Items.Select(i => i.Label));
        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Sprint");
        Assert.False(nav.HasSprint);
        Assert.Equal(new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" }, nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_AddsCodeMapAndRiskQuadrantUnderInsights()
    {
        // Story 7.10 review: Risk Quadrant rides the SAME gating signal as Code Map (hasCodeMap) as a sibling
        // Insights surface over the identical source-code walk, so the group now has two children (no longer
        // collapses to a flat link) whenever hasCodeMap is true.
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasCodeMap: true);

        Assert.Equal(
            new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "Code Map", "Risk Quadrant", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" },
            nav.Items.Select(i => i.Label).ToArray());
        Assert.True(nav.HasCodeMap);

        var insights = Assert.Single(nav.Groups, g => g.Label == "Insights");
        Assert.Equal(new[] { "Code Map", "Risk Quadrant" }, insights.Children.Select(c => c.Label).ToArray());
        Assert.Equal(SiteNav.CodeMapOutputPath, insights.Children[0].OutputRelativePath);
        Assert.Equal(SiteNav.RiskQuadrantOutputPath, insights.Children[1].OutputRelativePath);

        Assert.Single(nav.QuickLinks, q => q.Label == "Code Map" && q.OutputRelativePath == SiteNav.CodeMapOutputPath);
        Assert.Single(nav.QuickLinks, q => q.Label == "Risk Quadrant" && q.OutputRelativePath == SiteNav.RiskQuadrantOutputPath);
    }

    [Fact]
    public void Build_OmitsCodeMapAndRiskQuadrantWhenNotAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasCodeMap: false);

        Assert.DoesNotContain("Code Map", nav.Items.Select(i => i.Label));
        Assert.DoesNotContain("Risk Quadrant", nav.Items.Select(i => i.Label));
        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Code Map");
        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Risk Quadrant");
        Assert.False(nav.HasCodeMap);
    }

    [Fact]
    public void Build_InsightsGroup_PresentOnlyWithDeepGitSignals()
    {
        var without = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        Assert.DoesNotContain(without.Groups, g => g.Label == "Insights");
        Assert.DoesNotContain("Git Insights", without.Items.Select(i => i.Label));
        Assert.DoesNotContain("Deep Analytics", without.Items.Select(i => i.Label));

        var with = SiteNav.Build(
            new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false,
            hasGitInsights: true, hasDeepAnalytics: true);

        var insights = Assert.Single(with.Groups, g => g.Label == "Insights");
        Assert.Equal(new[] { "Git Insights", "Deep Analytics" }, insights.Children.Select(c => c.Label).ToArray());
        Assert.Equal(SiteNav.GitInsightsOutputPath, insights.Children[0].OutputRelativePath);
        Assert.Equal(SiteNav.DeepAnalyticsOutputPath, insights.Children[1].OutputRelativePath);
    }

    [Fact]
    public void Build_FollowUpsGroup_GatesOnActionItemsAndDeferredWork()
    {
        var none = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe");
        Assert.DoesNotContain(none.Groups, g => g.Label == "Follow-ups");

        var both = SiteNav.Build(
            new[] { "planning-artifacts/epics.md" }, "SpecScribe",
            hasActionItems: true, hasDeferredWork: true,
            deferredWorkOutputPath: "implementation-artifacts/deferred-work.html");

        var followUps = Assert.Single(both.Groups, g => g.Label == "Follow-ups");
        Assert.Equal(new[] { "Action Items", "Deferred Work" }, followUps.Children.Select(c => c.Label).ToArray());
        Assert.Equal(SiteNav.ActionItemsOutputPath, followUps.Children[0].OutputRelativePath);
        Assert.Equal("implementation-artifacts/deferred-work.html", followUps.Children[1].OutputRelativePath);
    }

    [Fact]
    public void Build_OmitsEmptyFollowUpsAndInsightsGroups()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        Assert.DoesNotContain(nav.Groups, g => g.Label is "Insights" or "Follow-ups");
    }

    [Fact]
    public void Build_DoesNotEmitStructureNavOrQuickLink()
    {
        // Structure was retired (Story 7.6 → Code Map); Story 10.1 confirms it stays gone. [Story 10.1 AC2]
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasCodeMap: true);
        Assert.DoesNotContain("Structure", nav.Items.Select(i => i.Label));
        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Structure");
    }

    [Fact]
    public void Build_PutsBmadMethodDocsInNavAndQuickLinks()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/briefs/brief-x/brief.md",
            "planning-artifacts/ux-designs/ux-x/DESIGN.md",
            "specs/spec-x/ARCHITECTURE-SPINE.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false);

        // Delivery before Project; Help trails with How to use / About SDD / About / Logs. Brief/UX stay quick-links only.
        Assert.Equal(
            new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "PRD", "Architecture", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" },
            nav.Items.Select(i => i.Label).ToArray());

        var quickLabels = nav.QuickLinks.Select(q => q.Label).ToArray();
        Assert.Contains("PRD", quickLabels);
        Assert.Contains("Product Brief", quickLabels);
        Assert.Contains("UX Design", quickLabels);
        Assert.DoesNotContain("Product Brief", nav.Items.Select(i => i.Label));
    }

    [Fact]
    public void Build_AddsSpecKernelToProjectGroupAndQuickLinks()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/epics.md",
            "specs/spec-x/SPEC.md",
            "specs/spec-x/ARCHITECTURE-SPINE.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false);

        var specLink = Assert.Single(nav.QuickLinks, q => q.Label == "Spec");
        Assert.Equal("specs/spec-x/SPEC.html", specLink.OutputRelativePath);

        // Spec rides the Project group in the top nav (Story 10.1) and stays a quick-link too.
        Assert.Contains("Spec", nav.Items.Select(i => i.Label));
        Assert.Equal(
            new[] { "Home", "Epics", "Requirements", "Traceability", "Cadence", "PRD", "Architecture", "Spec", "How to use SpecScribe", "About Spec-Driven Development", "About", "Logs" },
            nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_OmitsSpecKernelQuickLinkWhenNoSpecKernel()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Spec");
    }

    [Fact]
    public void Build_EmitsOneDisambiguatedSpecQuickLinkPerKernelWhenMultiple()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/epics.md",
            "specs/alpha/SPEC.md",
            "specs/beta/SPEC.md",
        }, "SpecScribe", hasAdrs: false);

        Assert.DoesNotContain("Spec", nav.QuickLinks.Select(q => q.Label));
        var alphaLink = Assert.Single(nav.QuickLinks, q => q.Label == "Spec — alpha");
        Assert.Equal("specs/alpha/SPEC.html", alphaLink.OutputRelativePath);
        var betaLink = Assert.Single(nav.QuickLinks, q => q.Label == "Spec — beta");
        Assert.Equal("specs/beta/SPEC.html", betaLink.OutputRelativePath);
        Assert.Equal("Read this SPEC kernel and its companions.", alphaLink.Description);
    }

    [Fact]
    public void Build_CollidingParentFolderNamesDisambiguateWithSpecsRelativeDir()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/epics.md",
            "specs/pkg-a/core/SPEC.md",
            "specs/pkg-b/core/SPEC.md",
        }, "SpecScribe", hasAdrs: false);

        Assert.Contains(nav.QuickLinks, q => q.Label == "Spec — pkg-a/core");
        Assert.Contains(nav.QuickLinks, q => q.Label == "Spec — pkg-b/core");
    }

    [Fact]
    public void Build_KeepsSingleSpecKernelLabelPlainWhenOnlyOneKernelExistsAlongsideOtherSpecsFolders()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/epics.md",
            "specs/only-one/SPEC.md",
        }, "SpecScribe", hasAdrs: false);

        var specLink = Assert.Single(nav.QuickLinks, q => q.Label == "Spec");
        Assert.Equal("specs/only-one/SPEC.html", specLink.OutputRelativePath);
    }

    [Fact]
    public void Build_DuplicateWellKnownModuleDocEmitsOneSkippedDiagnosticNamingChosenPathAndCount()
    {
        var diagnostics = new List<AdapterDiagnostic>();
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-b/prd.md",
            "planning-artifacts/prds/prd-a/prd.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false, diagnostics: diagnostics);

        var prdLink = Assert.Single(nav.QuickLinks, q => q.Label == "PRD");
        Assert.Equal("planning-artifacts/prds/prd-a/prd.html", prdLink.OutputRelativePath);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Skipped, diagnostic.Category);
        Assert.Equal("planning-artifacts/prds/prd-a/prd.md", diagnostic.RelativePath);
        Assert.Contains("1", diagnostic.Message);
    }

    [Fact]
    public void Build_NoDuplicateModuleDocsEmitsNoDiagnostics()
    {
        var diagnostics = new List<AdapterDiagnostic>();
        SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false, diagnostics: diagnostics);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Build_WithoutDiagnosticsSinkStillPicksAlphabeticalFirstOnDuplicateModuleDoc()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-b/prd.md",
            "planning-artifacts/prds/prd-a/prd.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false);

        var prdLink = Assert.Single(nav.QuickLinks, q => q.Label == "PRD");
        Assert.Equal("planning-artifacts/prds/prd-a/prd.html", prdLink.OutputRelativePath);
    }

    [Fact]
    public void FindDeferredWorkOutputPath_ReturnsFirstAlphabeticalMatch()
    {
        var path = SiteNav.FindDeferredWorkOutputPath(new[]
        {
            "implementation-artifacts/zz-other.md",
            "implementation-artifacts/deferred-work.md",
        });
        Assert.Equal("implementation-artifacts/deferred-work.html", path);
    }

    [Fact]
    public void ToNavigationView_ProjectsGroupsAndFlattenedItems()
    {
        var nav = SiteNav.Build(
            new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false,
            hasGitInsights: true, hasDeepAnalytics: true,
            hasActionItems: true, hasDeferredWork: true,
            deferredWorkOutputPath: "implementation-artifacts/deferred-work.html");

        var view = nav.ToNavigationView(SiteNav.EpicsOutputPath);

        Assert.Equal(nav.Items.Select(i => (i.Label, i.OutputRelativePath)).ToList(),
            view.Items.Select(i => (i.Label, i.OutputRelativePath)).ToList());
        Assert.Equal(nav.Groups.Count, view.Groups.Count);
        Assert.Contains(view.Groups, g => g.Label == "Insights" && g.Children.Count == 2);
        Assert.Contains(view.Groups, g => g.Label == "Follow-ups" && g.Children.Count == 2);
        Assert.Contains(view.Groups, g => g.Label == "Delivery");
        Assert.Contains(view.Groups, g => g.Label == "Help" && g.Children.Count == 4);
    }

    [Fact]
    public void Build_HelpGroup_AlwaysContainsSddAboutAndLogs()
    {
        var nav = SiteNav.Build(Array.Empty<string>(), "SpecScribe");

        var help = Assert.Single(nav.Groups, g => g.Label == "Help");
        Assert.Equal(
            new[] {
                ("How to use SpecScribe", SiteNav.HowToReadOutputPath),
                ("About Spec-Driven Development", SiteNav.AboutSddOutputPath),
                ("About", SiteNav.AboutOutputPath),
                ("Logs", SiteNav.DiagnosticsOutputPath),
            },
            help.Children.Select(c => (c.Label, c.OutputRelativePath)).ToArray());
    }

    [Fact]
    public void RenderNavBar_AddsMobileToggleAndActivePageSemantics()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

        var html = nav.RenderNavBar(SiteNav.RequirementsOutputPath);

        Assert.Contains("class=\"site-nav-toggle\"", html);
        Assert.Contains("aria-controls=\"site-nav-links\"", html);
        Assert.Contains("aria-current=\"page\"><svg", html);
        Assert.Contains(">Requirements</a>", html);
        Assert.Contains("site-nav-group", html);
        Assert.Contains("<summary class=\"site-nav-group-summary\"", html);
        Assert.Contains("Delivery", html);
        Assert.Contains("Project", html);
        Assert.Contains("Help", html);
        Assert.Contains("About", html);
        Assert.Contains("Logs", html);
    }

    [Fact]
    public void RenderNavBar_PrependsDecorativeSectionIconToEveryKnownNavItem()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

        var html = nav.RenderNavBar(SiteNav.HomeOutputPath);

        Assert.Contains("aria-hidden=\"true\" focusable=\"false\"", html);
        foreach (var label in nav.Items.Select(i => i.Label))
        {
            var display = label == "Epics" ? "Epics &amp; Stories" : label;
            Assert.Contains($">{display}</a>", html);
        }
    }

    [Fact]
    public void RenderNavBar_MarksActiveGroupAndLeaf_ButDoesNotForceGroupOpen()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var html = nav.RenderNavBar(SiteNav.SprintOutputPath);

        // The containing group carries has-active (a summary highlight) and its leaf is marked active, but the
        // <details> is NOT forced open — a menu that springs open on page load (and stays open through a
        // refresh) reads as stuck. [Story 10.10 review: auto-open removed]
        Assert.Contains("<details class=\"site-nav-group has-active family-epics\">", html);
        Assert.DoesNotContain("has-active family-epics\" open", html);
        Assert.Contains("class=\"site-menu-item active\" aria-current=\"page\"", html);
        Assert.Contains(">Sprint</a>", html);
    }

    [Fact]
    public void RenderBreadcrumb_UsesRelativePathsAndMarksCurrentPage()
    {
        var html = SiteNav.RenderBreadcrumb("epics/stories/1-1.html", new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Epics", SiteNav.EpicsOutputPath),
            ("Story 1.1", null),
        });

        Assert.Contains("href=\"../../index.html\"", html);
        Assert.Contains("href=\"../../epics.html\"", html);
        Assert.Contains("class=\"crumb-current\" aria-current=\"page\">Story 1.1</span>", html);
    }

    [Fact]
    public void RenderNavMarkup_CarriesTheBrandMark_TokenColoredAndDecorative()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var markup = HtmlRenderAdapter.Shared.RenderNavMarkup(nav.ToNavigationView("index.html"));

        var svgStart = markup.IndexOf("<svg class=\"site-nav-mark\"", StringComparison.Ordinal);
        Assert.True(svgStart >= 0, "the brand span carries the site-nav-mark SVG");
        var svg = markup[svgStart..(markup.IndexOf("</svg>", svgStart, StringComparison.Ordinal) + "</svg>".Length)];
        Assert.Contains("aria-hidden=\"true\"", svg);
        Assert.Contains("width=\"16\"", svg);
        Assert.Contains(HtmlRenderAdapter.NibPathData, svg);
        Assert.DoesNotContain("#", svg);
        Assert.Contains("</svg>SpecScribe</span>", markup);
    }

    // ---- Story 10.10: Insights local-context helper ----

    [Fact]
    public void BuildInsightsLocalContext_MultipleInsightPages_ReturnsGroupMembershipWithActiveMarked()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe",
            hasAdrs: false, hasGitInsights: true, hasDeepAnalytics: true, hasCodeMap: true);

        var localContext = nav.BuildInsightsLocalContext(SiteNav.GitInsightsOutputPath);

        Assert.NotNull(localContext);
        Assert.Equal("Insights", localContext!.Title);
        Assert.Equal(new[] { "Git Insights", "Deep Analytics", "Code Map", "Risk Quadrant" }, localContext.Items.Select(i => i.Label).ToArray());
        Assert.True(localContext.Items.Single(i => i.Label == "Git Insights").IsActive);
        Assert.False(localContext.Items.Single(i => i.Label == "Deep Analytics").IsActive);
    }

    [Fact]
    public void BuildInsightsLocalContext_SingleInsightPage_ReturnsNull()
    {
        // Only one Insights child → SiteNav.Build collapses the group to a flat link — nothing to navigate
        // between, so the local-context band must degrade to null (NFR8: no degenerate one-item band).
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasGitInsights: true);

        var localContext = nav.BuildInsightsLocalContext(SiteNav.GitInsightsOutputPath);

        Assert.Null(localContext);
    }

    [Fact]
    public void BuildInsightsLocalContext_NoInsightPages_ReturnsNull()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var localContext = nav.BuildInsightsLocalContext(SiteNav.GitInsightsOutputPath);

        Assert.Null(localContext);
    }

    // ---- Story 21.1 follow-up: Delivery local-context helper (replaces the generic repeated-nav fallback) ----

    [Fact]
    public void BuildDeliveryLocalContext_MultipleDeliveryPages_ReturnsGroupMembershipWithActiveMarked()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var localContext = nav.BuildDeliveryLocalContext(SiteNav.RequirementsOutputPath);

        Assert.NotNull(localContext);
        Assert.Equal("Delivery", localContext!.Title);
        Assert.Equal(new[] { "Epics", "Requirements", "Traceability", "Cadence", "Sprint" }, localContext.Items.Select(i => i.Label).ToArray());
        Assert.True(localContext.Items.Single(i => i.Label == "Requirements").IsActive);
        Assert.False(localContext.Items.Single(i => i.Label == "Epics").IsActive);
    }

    [Fact]
    public void BuildDeliveryLocalContext_NoEpics_ReturnsNull()
    {
        var nav = SiteNav.Build(Array.Empty<string>(), "SpecScribe");

        var localContext = nav.BuildDeliveryLocalContext(SiteNav.HomeOutputPath);

        Assert.Null(localContext);
    }

    [Fact]
    public void ToNavigationView_ThreadsLocalContextOntoNavigationView()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var localContext = new NavLocalContext("Stories in this epic", new[] { new NavLocalItem("Story 1.1", "story-1-1.html", false) });

        var view = nav.ToNavigationView("epics/epic-1.html", localContext);

        Assert.Same(localContext, view.LocalContext);
    }

    [Fact]
    public void ToNavigationView_DefaultsLocalContextToNull()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var view = nav.ToNavigationView("epics/epic-1.html");

        Assert.Null(view.LocalContext);
    }
}
