using SpecScribe;

namespace SpecScribe.Tests;

public class SiteNavTests
{
    [Fact]
    public void Build_OmitsMissingArtifactClasses()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        Assert.Equal(new[] { "Home", "Epics", "Requirements" }, nav.Items.Select(i => i.Label).ToArray());
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

        Assert.Equal(
            new[] { "Home", "GDD", "Narrative", "Game Architecture", "ADRs", "Epics", "Requirements" },
            nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_IncludesReadmeRightAfterHomeWhenAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasReadme: true);

        Assert.Equal(new[] { "Home", "Readme", "Epics", "Requirements" }, nav.Items.Select(i => i.Label).ToArray());
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

        // Sprint sits in the delivery-tracking neighborhood (after Epics/Requirements); existing labels stay
        // exactly as they were, with no duplicates. [Story 2.3 Task 5]
        Assert.Equal(new[] { "Home", "Epics", "Requirements", "Sprint" }, nav.Items.Select(i => i.Label).ToArray());
        Assert.Equal(SiteNav.SprintOutputPath, nav.Items.First(i => i.Label == "Sprint").OutputRelativePath);
        Assert.True(nav.HasSprint);

        var sprintQuick = Assert.Single(nav.QuickLinks, q => q.Label == "Sprint");
        Assert.Equal(SiteNav.SprintOutputPath, sprintQuick.OutputRelativePath);
    }

    [Fact]
    public void Build_OmitsSprintWhenNotAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: false);

        Assert.DoesNotContain("Sprint", nav.Items.Select(i => i.Label));
        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Sprint");
        Assert.False(nav.HasSprint);
        // The existing delivery views are untouched (no Sprint injected between them).
        Assert.Equal(new[] { "Home", "Epics", "Requirements" }, nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_AddsStructureItemAndQuickLinkWhenAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasStructure: true);

        // Structure sits in the insight/tracking neighborhood after Epics/Requirements. [Story 3.4 Task 3.2]
        Assert.Equal(new[] { "Home", "Epics", "Requirements", "Structure" }, nav.Items.Select(i => i.Label).ToArray());
        Assert.Equal(SiteNav.StructureOutputPath, nav.Items.First(i => i.Label == "Structure").OutputRelativePath);
        Assert.True(nav.HasStructure);

        var quick = Assert.Single(nav.QuickLinks, q => q.Label == "Structure");
        Assert.Equal(SiteNav.StructureOutputPath, quick.OutputRelativePath);
    }

    [Fact]
    public void Build_OmitsStructureWhenNotAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasStructure: false);

        Assert.DoesNotContain("Structure", nav.Items.Select(i => i.Label));
        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Structure");
        Assert.False(nav.HasStructure);
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

        // PRD + Architecture ride the top nav; the brief and UX docs are quick-links only.
        Assert.Equal(
            new[] { "Home", "PRD", "Architecture", "Epics", "Requirements" },
            nav.Items.Select(i => i.Label).ToArray());

        var quickLabels = nav.QuickLinks.Select(q => q.Label).ToArray();
        Assert.Contains("PRD", quickLabels);
        Assert.Contains("Product Brief", quickLabels);
        Assert.Contains("UX Design", quickLabels);
        Assert.DoesNotContain("Product Brief", nav.Items.Select(i => i.Label));
    }

    [Fact]
    public void Build_AddsSpecKernelQuickLinkWhenSpecKernelPresentWithoutDuplicatingArchitecture()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/epics.md",
            "specs/spec-x/SPEC.md",
            "specs/spec-x/ARCHITECTURE-SPINE.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false);

        // The kernel quick-link points at the SPEC hub's generated page (the natural entry point). Its label
        // reads "Spec" — the friendlier pill label, not the internal "Spec Kernel" jargon. [Story 2.2 polish]
        var specLink = Assert.Single(nav.QuickLinks, q => q.Label == "Spec");
        Assert.Equal("specs/spec-x/SPEC.html", specLink.OutputRelativePath);

        // It is a quick-link only — no new top-nav "Spec"/"Specs" item — and the existing ARCHITECTURE-SPINE
        // "Architecture" nav entry stays exactly once (not duplicated or removed). [Story 2.2 Task 3]
        Assert.DoesNotContain("Spec", nav.Items.Select(i => i.Label));
        Assert.Equal(new[] { "Home", "PRD", "Architecture", "Epics", "Requirements" }, nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_OmitsSpecKernelQuickLinkWhenNoSpecKernel()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        Assert.DoesNotContain(nav.QuickLinks, q => q.Label == "Spec");
    }

    [Fact]
    public void RenderNavBar_AddsMobileToggleAndActivePageSemantics()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true);

        var html = nav.RenderNavBar(SiteNav.RequirementsOutputPath);

        Assert.Contains("class=\"site-nav-toggle\"", html);
        Assert.Contains("aria-controls=\"site-nav-links\"", html);
        // The active nav item carries its section icon (decorative) ahead of the label text. [Story 2.5 Task 4]
        Assert.Contains("aria-current=\"page\"><svg", html);
        Assert.Contains(">Requirements</a>", html);
    }

    [Fact]
    public void RenderNavBar_PrependsDecorativeSectionIconToEveryKnownNavItem()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

        var html = nav.RenderNavBar(SiteNav.HomeOutputPath);

        // Every nav item this build produces (Home/Readme/ADRs/Epics/Requirements) is a curated concept, so
        // each carries a decorative icon paired with its still-present text label (never icon-only). [Story 2.5]
        Assert.Contains("aria-hidden=\"true\" focusable=\"false\"", html);
        foreach (var label in nav.Items.Select(i => i.Label))
        {
            Assert.Contains($">{label}</a>", html);
        }
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
}
