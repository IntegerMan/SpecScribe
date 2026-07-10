using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 6.1 host-neutral view-model contract — the DELIVERY-seam records
/// (<see cref="NavigationView"/>, <see cref="BreadcrumbTrail"/>, <see cref="AssetManifest"/>,
/// <see cref="InteractionState"/>, <see cref="PageView"/>) that the render adapters consume. These pin the data
/// shape and the drill/status semantics; the adapter round-trip + parity live in
/// <see cref="HtmlRenderAdapterTests"/>/<see cref="RenderParityTests"/>. [Story 6.1]</summary>
public class RenderViewModelTests
{
    [Fact]
    public void ToNavigationView_ProjectsSiteNavDataWithLabelAsConceptKey()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

        var view = nav.ToNavigationView(SiteNav.EpicsOutputPath);

        // The projection lifts SiteNav's already host-neutral data verbatim — same title, same ordered items,
        // and the concept key defaults to the label (the mapping RenderNavBar always used).
        Assert.Equal("SpecScribe", view.SiteTitle);
        Assert.Equal(SiteNav.EpicsOutputPath, view.ActiveOutputRelativePath);
        Assert.Equal(nav.Items.Select(i => (i.Label, i.OutputRelativePath)).ToList(),
            view.Items.Select(i => (i.Label, i.OutputRelativePath)).ToList());
        Assert.All(view.Items, i => Assert.Equal(i.Label, i.ConceptKey));
        Assert.Equal(nav.QuickLinks.Select(q => q.OutputRelativePath).ToList(),
            view.QuickLinks.Select(q => q.OutputRelativePath).ToList());
    }

    [Fact]
    public void BreadcrumbTrail_ParentTarget_IsTheLastCrumbWithARealPath()
    {
        // Home → Epics → Epic N → Story N.M: the current page is the final crumb (null path), so the drill-up
        // parent is the crumb before it — the epic page.
        var trail = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", "epics/epic-1.html"),
            ("Story 1.1", null),
        });

        Assert.Equal("epics/epic-1.html", trail.ParentTarget);
    }

    [Fact]
    public void BreadcrumbTrail_Empty_HasNoParent()
    {
        // The home page carries no breadcrumb, so it has no drill-up parent.
        Assert.Empty(BreadcrumbTrail.Empty.Crumbs);
        Assert.Null(BreadcrumbTrail.Empty.ParentTarget);
    }

    [Fact]
    public void InteractionState_SourcesParentFromBreadcrumbAndChildrenFromHierarchy()
    {
        // A Story-page interaction: parent resolves to its Epic (via the breadcrumb), no children (a leaf).
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", "epics/epic-1.html"),
            ("Story 1.1", null),
        });
        var story = new InteractionState { ParentTarget = breadcrumb.ParentTarget };
        Assert.Equal("epics/epic-1.html", story.ParentTarget);
        Assert.Empty(story.ChildTargets);

        // An Epic-page interaction drills DOWN to each story page and UP to the epics index.
        var epicCrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", null),
        });
        var epic = new InteractionState
        {
            ParentTarget = epicCrumb.ParentTarget,
            ChildTargets = new[] { "epics/story-1-1.html", "epics/story-1-2.html" },
        };
        Assert.Equal(SiteNav.EpicsOutputPath, epic.ParentTarget);
        Assert.Equal(new[] { "epics/story-1-1.html", "epics/story-1-2.html" }, epic.ChildTargets);
    }

    [Fact]
    public void InteractionState_StatusStage_RoutesThroughStatusStylesNotALocalCopy()
    {
        // The stage carried on the interaction model must be exactly what StatusStyles produces — the contract
        // CONSUMES the status→stage seam, never re-models it. An in-progress story rolls up to "active".
        var story = new StoryInfo
        {
            Id = "1.1", EpicNumber = 1, Title = "Foundation",
            UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(),
            Status = "in-progress", TasksDone = 0, TasksTotal = 0,
        };
        var interaction = new InteractionState { StatusStage = StatusStyles.ForStory(story) };

        Assert.Equal(StatusStyles.ForStory(story), interaction.StatusStage);
        Assert.Equal("active", interaction.StatusStage);
    }

    [Fact]
    public void ViewModelRecords_CarryNoHtml()
    {
        // The view models are plain DATA — escaping and markup are delivery concerns that stay in the adapter.
        // A representative set carries zero angle brackets.
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe & Co", hasAdrs: true).ToNavigationView(SiteNav.EpicsOutputPath);
        Assert.DoesNotContain('<', nav.SiteTitle);
        Assert.All(nav.Items, i => Assert.DoesNotContain('<', i.Label + i.OutputRelativePath + i.ConceptKey));

        var manifest = new AssetManifest { StylesheetHref = "specscribe.css", ScriptHref = "specscribe.js", MermaidNeeded = false };
        Assert.DoesNotContain('<', manifest.StylesheetHref + manifest.ScriptHref);
    }
}
