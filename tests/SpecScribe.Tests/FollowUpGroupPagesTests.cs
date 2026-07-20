using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 9.13 — stable group paths, membership projection, and group page emission.</summary>
public class FollowUpGroupPagesTests
{
    [Fact]
    public void Paths_AreStableUnderFollowUpsFolder_WithGroupPrefix()
    {
        Assert.Equal("follow-ups/group-follow-ups.html", FollowUpGroupPages.FollowUpsPath);
        Assert.Equal("follow-ups/group-unplanned.html", FollowUpGroupPages.UnplannedPath);
        Assert.Equal("follow-ups/group-epic-3.html", FollowUpGroupPages.EpicPath(3));
        Assert.Equal("group-epic-3", FollowUpGroupPages.EpicSlug(3));
        Assert.True(FollowUpGroupPages.IsGroupSlug("group-follow-ups"));
        Assert.False(FollowUpGroupPages.IsGroupSlug("action-fix-it"));
        Assert.False(FollowUpGroupPages.IsGroupSlug("deferred-open-item"));
    }

    [Fact]
    public void Paths_DoNotCollideWithActionOrDeferredDetailPrefixes()
    {
        Assert.StartsWith("group-", FollowUpGroupPages.FollowUpsSlug);
        Assert.StartsWith("group-", FollowUpGroupPages.UnplannedSlug);
        Assert.StartsWith("group-", FollowUpGroupPages.EpicSlug(1));
        Assert.DoesNotContain("action-", FollowUpGroupPages.FollowUpsSlug);
        Assert.DoesNotContain("deferred-", FollowUpGroupPages.UnplannedSlug);
        Assert.NotEqual(
            FollowUpSlug.OutputPath("action-group-follow-ups"),
            FollowUpGroupPages.FollowUpsPath);
    }

    [Fact]
    public void Enumerate_FollowUps_IncludesNullAndUnknownEpicOrphans()
    {
        var model = OneEpicModel();
        var items = new[]
        {
            new SprintActionItem("Attributed", "open", 1, null),
            new SprintActionItem("Orphan A", "open", null, null),
            new SprintActionItem("Orphan B", "done", null, null),
            new SprintActionItem("Ghost epic debt", "open", 99, null),
        };
        var work = new WorkInventory { QuickDev = Array.Empty<QuickDevEntry>(), Deferred = null };
        var geometry = FollowUpGeometry.From(items, ProjectCounts.Empty with { OpenActionItems = 3 }, work, epics: model);
        var unplanned = UnplannedWorkGeometry.Empty;

        var groups = FollowUpGroupPages.Enumerate(geometry, unplanned, model);
        var followUps = Assert.Single(groups, g => g.Slug == FollowUpGroupPages.FollowUpsSlug);
        Assert.Equal(3, followUps.Count);
        Assert.All(followUps.Members, m => Assert.Equal("action", m.Kind));
        Assert.Contains(followUps.Members, m => m.SummaryHtml.Contains("Orphan A", StringComparison.Ordinal));
        Assert.Contains(followUps.Members, m => m.SummaryHtml.Contains("Ghost epic debt", StringComparison.Ordinal));
        Assert.DoesNotContain(followUps.Members, m => m.SummaryHtml.Contains("Attributed", StringComparison.Ordinal));
        Assert.DoesNotContain(groups, g => g.Slug == FollowUpGroupPages.EpicSlug(99));
        Assert.DoesNotContain(groups, g => g.Slug == FollowUpGroupPages.UnplannedSlug);
    }

    [Fact]
    public void Enumerate_Unplanned_UsesUnplannedSet_OmitsEmpty()
    {
        var model = OneEpicModel();
        var deferredMarkdown = """
            ## Deferred from: misc (2026-07-15)

            - Parked item.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("One-shot", "spec-one.html", "ready", null),
            },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(),
            ProjectCounts.Empty with { DeferredOpenItems = 1, DirectChanges = 1 },
            work,
            deferredModel: deferredModel,
            epics: model);
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);

        Assert.Equal("deferred-work.html", unplanned.DeferredListHref);

        var groups = FollowUpGroupPages.Enumerate(geometry, unplanned, model);
        Assert.DoesNotContain(groups, g => g.Slug == FollowUpGroupPages.FollowUpsSlug);
        var page = Assert.Single(groups, g => g.Slug == FollowUpGroupPages.UnplannedSlug);
        Assert.Equal(2, page.Count);
        Assert.Contains(page.Members, m => m.Kind == "direct");
        Assert.Contains(page.Members, m => m.Kind == "deferred");
        Assert.Equal("deferred-work.html", page.WholeSiteListHref);
        Assert.Equal("All deferred work", page.WholeSiteListLabel);
    }

    [Fact]
    public void Enumerate_EpicGroup_AttributedActionsAndDeferred_OnlyWhenNonEmpty()
    {
        var model = OneEpicModel();
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Epic deferred.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var geometry = FollowUpGeometry.From(
            new[] { new SprintActionItem("Epic action", "open", 1, "Dana") },
            ProjectCounts.Empty with { OpenActionItems = 1, DeferredOpenItems = 1 },
            work,
            deferredModel: deferredModel,
            epics: model);
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);

        var groups = FollowUpGroupPages.Enumerate(geometry, unplanned, model);
        var epic = Assert.Single(groups, g => g.Slug == FollowUpGroupPages.EpicSlug(1));
        Assert.Equal(2, epic.Count);
        Assert.Contains(epic.Members, m => m.Kind == "action");
        Assert.Contains(epic.Members, m => m.Kind == "deferred");
        Assert.DoesNotContain(groups, g => g.Slug == FollowUpGroupPages.FollowUpsSlug);
    }

    [Fact]
    public void Enumerate_EpicGroup_EmitsWhenQuickDevOnly()
    {
        var model = OneEpicModel();
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("Story 1.1 hotfix", "spec-hotfix.html", "ready", null),
            },
            Deferred = null,
        };
        var geometry = FollowUpGeometry.Empty;
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);
        Assert.NotEmpty(unplanned.ForEpic(1));

        var groups = FollowUpGroupPages.Enumerate(geometry, unplanned, model);
        var epic = Assert.Single(groups, g => g.Slug == FollowUpGroupPages.EpicSlug(1));
        Assert.Contains(epic.Members, m => m.Kind == "direct");
        Assert.Contains(epic.Members, m => m.SummaryHtml.Contains("hotfix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Enumerate_MixedInventory_MembershipSourcesAreMutuallyExclusiveByDetailHref()
    {
        // Story 10.10 deferred debt: SiteGenerator.WriteFollowUpDetails builds a href->group lookup assuming
        // Enumerate's three sources (orphans / unplanned / per-epic) never share a DetailHref. Pins that
        // invariant across a realistic mixed inventory (an attributed epic action, an orphan action, unplanned
        // quick-dev, and epic-attributed deferred) so a future change to Enumerate that breaks it is caught.
        var model = TwoEpicModel();
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Epic 1 deferred item.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("Story 2.1 hotfix", "spec-hotfix.html", "ready", null) },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var items = new[]
        {
            new SprintActionItem("Epic 1 action", "open", 1, "Dana"),
            new SprintActionItem("Orphan action", "open", null, null),
        };
        var geometry = FollowUpGeometry.From(
            items,
            ProjectCounts.Empty with { OpenActionItems = 2, DeferredOpenItems = 1, DirectChanges = 1 },
            work,
            deferredModel: deferredModel,
            epics: model);
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);

        var groups = FollowUpGroupPages.Enumerate(geometry, unplanned, model);
        Assert.True(groups.Count >= 2, "Test setup should produce at least two groups to make a collision possible.");

        var seenHrefToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var member in group.Members)
            {
                if (member.DetailHref.Length == 0) continue;
                if (seenHrefToSlug.TryGetValue(member.DetailHref, out var existingSlug))
                {
                    Assert.Fail(
                        $"DetailHref '{member.DetailHref}' appears in both '{existingSlug}' and '{group.Slug}' — " +
                        "Enumerate's membership sources are no longer mutually exclusive.");
                }
                seenHrefToSlug[member.DetailHref] = group.Slug;
            }
        }
    }

    [Fact]
    public void Enumerate_EmptyInputs_YieldsNoGroups()
    {
        Assert.Empty(FollowUpGroupPages.Enumerate(FollowUpGeometry.Empty, UnplannedWorkGeometry.Empty));
    }

    [Fact]
    public void Enumerate_EmptyActionText_GetsFallbackSummary()
    {
        var model = OneEpicModel();
        var items = new[] { new SprintActionItem("   ", "open", null, null) };
        var work = new WorkInventory { QuickDev = Array.Empty<QuickDevEntry>(), Deferred = null };
        var geometry = FollowUpGeometry.From(items, ProjectCounts.Empty with { OpenActionItems = 1 }, work, epics: model);
        var groups = FollowUpGroupPages.Enumerate(geometry, UnplannedWorkGeometry.Empty, model);
        var followUps = Assert.Single(groups, g => g.Slug == FollowUpGroupPages.FollowUpsSlug);
        Assert.Contains("(no action text)", Assert.Single(followUps.Members).SummaryHtml);
    }

    [Fact]
    public void Templater_RendersRows_RejectsEmpty()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var group = new FollowUpGroupSpec(
            FollowUpGroupPages.FollowUpsSlug,
            "Follow-ups",
            "Unattributed",
            new[]
            {
                new FollowUpGroupMember(
                    "action",
                    "Cleanup",
                    "follow-ups/action-cleanup.html",
                    "open",
                    "Open",
                    "Unattributed",
                    false),
            },
            SiteNav.ActionItemsOutputPath,
            "All open action items");

        var html = FollowUpGroupTemplater.RenderPage(group, nav);
        Assert.Contains("main id=\"main-content\"", html);
        Assert.Contains("followup-rows-list", html);
        Assert.Contains("followup-row", html);
        Assert.Contains("Cleanup", html);
        Assert.Contains("href=\"../follow-ups/action-cleanup.html\"", html);
        Assert.Contains("All open action items", html);
        Assert.DoesNotContain("data-copy=", html);
        Assert.DoesNotContain("?filter=", html);
        Assert.DoesNotContain("#group=", html);

        var empty = new FollowUpGroupSpec("group-x", "X", "x", Array.Empty<FollowUpGroupMember>());
        Assert.Throws<ArgumentException>(() => FollowUpGroupTemplater.RenderPage(empty, nav));
    }

    // ---- List-batch Address/Close pane (spec-follow-up-list-batch-actions) ----------------------------

    private static readonly CommandCatalog QuickDevOnly = new(
        "BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });

    private static FollowUpGroupMember OpenMember(string kind, string label) => new(
        kind,
        PathUtil.Html(label),
        $"follow-ups/{kind}-{label}.html",
        "open",
        "Open",
        PathUtil.Html("Epic 1"),
        Resolved: false,
        RawSummary: label,
        RawProvenance: "Epic 1");

    [Fact]
    public void Templater_ListBatchPane_MixedPair_BothKindsSixOrMore()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var members = Enumerable.Range(1, 6)
            .SelectMany(i => new[] { OpenMember("deferred", $"Deferred {i}"), OpenMember("action", $"Action {i}") })
            .ToList();
        var group = new FollowUpGroupSpec("group-epic-1", "Epic 1 follow-ups", "Attributed follow-ups for Epic 1", members);

        var html = FollowUpGroupTemplater.RenderPage(group, nav, QuickDevOnly);

        Assert.Contains("class=\"chart-panel next-steps list-batch-actions\"", html);
        Assert.Contains(">Address all<", html);
        Assert.Contains(">Address first 5<", html);
        Assert.Contains(">Close all<", html);
        Assert.Contains("class=\"next-step-command-group\"", html);
        // Every one of the three cards carries both a Deferred and an Action items button (dual pair).
        Assert.Equal(3, Regex.Matches(html, "<span class=\"cmd-text\">Deferred</span>").Count);
        Assert.Equal(3, Regex.Matches(html, "<span class=\"cmd-text\">Action items</span>").Count);
        Assert.Contains("data-copy=\"/bmad-quick-dev Address open deferred work on Epic 1 follow-ups (6 items)", html);
        Assert.Contains("data-copy=\"/bmad-quick-dev Resolve open retrospective action items on Epic 1 follow-ups (6 items)", html);

        foreach (Match m in Regex.Matches(html, "data-copy=\"([^\"]*)\""))
            Assert.DoesNotContain("<a", m.Groups[1].Value);
    }

    [Fact]
    public void Templater_ListBatchPane_SingleKindOnly_WhenOtherKindEmpty()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var members = Enumerable.Range(1, 6).Select(i => OpenMember("deferred", $"Deferred {i}")).ToList();
        var group = new FollowUpGroupSpec("group-epic-1", "Epic 1 follow-ups", "x", members);

        var html = FollowUpGroupTemplater.RenderPage(group, nav, QuickDevOnly);

        Assert.DoesNotContain("next-step-command-group", html);
        Assert.Contains("<span class=\"cmd-text\">Deferred</span>", html);
        Assert.DoesNotContain("Action items</span>", html);
    }

    [Fact]
    public void Templater_ListBatchPane_OmitsFirst5ForLowCountKind_OnMixedPage()
    {
        // 6 deferred (>=6) + 3 actions (<6): Address first 5 shows only the Deferred button.
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var members = Enumerable.Range(1, 6).Select(i => OpenMember("deferred", $"Deferred {i}"))
            .Concat(Enumerable.Range(1, 3).Select(i => OpenMember("action", $"Action {i}")))
            .ToList();
        var group = new FollowUpGroupSpec("group-epic-1", "Epic 1 follow-ups", "x", members);

        var html = FollowUpGroupTemplater.RenderPage(group, nav, QuickDevOnly);

        var first5Start = html.IndexOf(">Address first 5<", StringComparison.Ordinal);
        Assert.True(first5Start >= 0);
        var nextKicker = html.IndexOf("next-step-kicker", first5Start + 1, StringComparison.Ordinal);
        var first5Card = nextKicker > 0 ? html[first5Start..nextKicker] : html[first5Start..];
        Assert.Contains("Deferred</span>", first5Card);
        Assert.DoesNotContain("Action items</span>", first5Card);

        // Address all / Close all still pair both kinds despite the low action count.
        Assert.Equal(2, Regex.Matches(html, "<span class=\"cmd-text\">Action items</span>").Count);
    }

    [Fact]
    public void Templater_ListBatchPane_IgnoresDirectKind()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var members = new List<FollowUpGroupMember>
        {
            new(
                "direct",
                PathUtil.Html("Direct change UNIQUE-MARKER"),
                "follow-ups/direct.html",
                "open",
                "Open",
                PathUtil.Html("Direct change"),
                Resolved: false,
                RawSummary: "Direct change UNIQUE-MARKER",
                RawProvenance: "Direct change"),
        };
        var group = new FollowUpGroupSpec("group-unplanned", "Unplanned", "x", members);

        var html = FollowUpGroupTemplater.RenderPage(group, nav, QuickDevOnly);

        // No deferred/action open items in scope (the only member is Kind == "direct") → pane omitted
        // (NFR8), and the direct row's text never reaches a batch command payload.
        Assert.DoesNotContain("list-batch-actions", html);
        Assert.DoesNotContain("data-copy=", html);
        Assert.Contains("Direct change UNIQUE-MARKER", html); // still rendered as an ordinary row
    }

    [Fact]
    public void Templater_ListBatchPane_Omitted_WhenNoQuickDev()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var members = new List<FollowUpGroupMember> { OpenMember("deferred", "Deferred x") };
        var group = new FollowUpGroupSpec("group-epic-1", "Epic 1 follow-ups", "x", members);

        var html = FollowUpGroupTemplater.RenderPage(group, nav, CommandCatalog.Empty);

        Assert.DoesNotContain("list-batch-actions", html);
        Assert.DoesNotContain("data-copy=", html);
    }

    private static EpicsModel OneEpicModel() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "Foundation",
                GoalHtml = string.Empty,
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = new[]
                {
                    new StoryInfo
                    {
                        Id = "1.1",
                        EpicNumber = 1,
                        Title = "Foundation",
                        UserStoryHtml = string.Empty,
                        AcBlocksHtml = Array.Empty<string>(),
                    },
                },
            },
        },
    };

    private static EpicsModel TwoEpicModel() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "Foundation",
                GoalHtml = string.Empty,
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = new[]
                {
                    new StoryInfo
                    {
                        Id = "1.1",
                        EpicNumber = 1,
                        Title = "Foundation",
                        UserStoryHtml = string.Empty,
                        AcBlocksHtml = Array.Empty<string>(),
                    },
                },
            },
            new EpicInfo
            {
                Number = 2,
                Title = "Second",
                GoalHtml = string.Empty,
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = new[]
                {
                    new StoryInfo
                    {
                        Id = "2.1",
                        EpicNumber = 2,
                        Title = "Second",
                        UserStoryHtml = string.Empty,
                        AcBlocksHtml = Array.Empty<string>(),
                    },
                },
            },
        },
    };
}
