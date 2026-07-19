using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 9.6 end-to-end coverage: action-items grouping/cross-link/visible-text linkify,
/// deferred-work structured cards, and NFR8 degrade paths.</summary>
public class FollowUpSurfacesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-followups-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ### Epic 2: Delivery

        Ship the work.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted

        As a maintainer, I want the follow-up.

        ## Epic 2: Delivery

        ### Story 2.1: Delivery Story

        As a maintainer, I want delivery.
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: done

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string Story21Md = """
        # Story 2.1: Delivery Story

        Status: in-progress

        ## Story

        As a maintainer, I want delivery.

        ## Acceptance Criteria

        1. It ships.

        ## Tasks / Subtasks

        - [ ] Task 1: Ship it (AC: #1)
        """;

    // Canonical Epic 1↔Epic 2 heatmap-debt pair + an unrelated item.
    private const string SprintWithDupes = """
        last_updated: 2026-07-16T12:00:00-04:00
        development_status:
          epic-1: done
          1-1-foundation: done
          1-2-undrafted: backlog
          epic-1-retrospective: done
          epic-2: in-progress
          2-1-delivery: in-progress
          epic-2-retrospective: done
        action_items:
          - epic: 1
            action: "Route Epic 1's deferred tech debt (heatmap HeatLevel collapse, unmapped ForEpic status classes, non-invariant heatmap date formatting) into the deferred-work backlog for review before Epic 3 planning"
            owner: Dana
            status: open
          - epic: 1
            action: "Schedule retros promptly"
            owner: Amelia
            status: open
          - epic: 2
            action: "Triage Epic 1's deferred heatmap tech debt (HeatLevel collapse on sparse history, unmapped ForEpic status classes, non-invariant heatmap date formatting) before starting Epic 3 Story 2.1 or 3.5 - carried unaddressed across two retrospectives now"
            owner: Winston
            status: open
        """;

    private const string StructuredDeferred = """
        # Deferred Work

        ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

        - Open casing mismatch still outstanding.

        ## Deferred from: code review of 2-1-delivery.md (2026-07-15)

        - **[RESOLVED in 2.1]** ~~Already fixed in Story 2.1~~ — kept for the audit trail.
        """;

    public FollowUpSurfacesTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "2-1-delivery.md"), Story21Md);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "epic-1-retro-2026-07-07.md"),
            "# Epic 1 Retrospective\n\n## Summary\n\nDone.\n");
        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "epic-2-retro-2026-07-08.md"),
            "# Epic 2 Retrospective\n\n## Summary\n\nDone.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    [Fact]
    public void ActionItems_GroupedByEpic_CrossLinksDuplicates_LinkifiesVisibleTextOnly()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), SprintWithDupes);
        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(Path.Combine(Site, "action-items.html"));

        // Scan-first row grammar (Story 9.10) + primary link to detail page (Story 9.11).
        Assert.Contains("class=\"followup-row\"", html);
        Assert.Contains("followup-row-summary", html);
        Assert.Contains("followup-row-primary", html);
        Assert.Contains("href=\"follow-ups/action-", html);

        // Groups ordered by epic ascending.
        var epic1 = html.IndexOf("From the Epic 1 retrospective", StringComparison.Ordinal);
        var epic2 = html.IndexOf("From the Epic 2 retrospective", StringComparison.Ordinal);
        Assert.True(epic1 >= 0 && epic2 > epic1, "Epic 1 group must precede Epic 2");

        // Cross-link lives on the detail page (list is scan + View detail only).
        Assert.DoesNotContain("also raised", html);
        var scheduleIdx = html.IndexOf("Schedule retros promptly", StringComparison.Ordinal);
        Assert.True(scheduleIdx >= 0);

        // List page no longer embeds Resolve-with-AI (moved to detail).
        Assert.DoesNotContain("data-copy=", html);
        Assert.DoesNotContain("followup-row-detail", html);
    }

    [Fact]
    public void FollowUpDetailPages_ExistPerItem_SharedTemplate_RawDataCopy()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), SprintWithDupes);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var followUpsDir = Path.Combine(Site, "follow-ups");
        Assert.True(Directory.Exists(followUpsDir));
        var actionPages = Directory.GetFiles(followUpsDir, "action-*.html");
        var deferredPages = Directory.GetFiles(followUpsDir, "deferred-*.html");
        Assert.Equal(3, actionPages.Length);
        Assert.Equal(2, deferredPages.Length);

        var schedulePage = actionPages
            .Select(p => (Path: p, Html: File.ReadAllText(p)))
            .First(t => t.Html.Contains("Schedule retros promptly", StringComparison.Ordinal));
        Assert.Contains("main id=\"main-content\" class=\"followup-detail\"", schedulePage.Html);
        Assert.Contains("Where it came from", schedulePage.Html);
        Assert.Contains("From the Epic 1 retrospective", schedulePage.Html);
        Assert.Contains("story-kicker\">Action item", schedulePage.Html);

        // Cross-link pair on detail pages.
        var debtPages = actionPages
            .Select(File.ReadAllText)
            .Where(h => h.Contains("heatmap", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(debtPages.Count >= 2);
        Assert.Contains(debtPages, h => h.Contains("also raised in Epic 2 retrospective"));
        Assert.Contains(debtPages, h => h.Contains("also raised in Epic 1 retrospective"));

        var deferredHtml = File.ReadAllText(deferredPages.First(p => File.ReadAllText(p).Contains("Open casing mismatch")));
        Assert.Contains("main id=\"main-content\" class=\"followup-detail\"", deferredHtml);
        Assert.Contains("story-kicker\">Deferred work", deferredHtml);
        Assert.Contains("Where it came from", deferredHtml);
        Assert.Contains("Deferred from:", deferredHtml);

        // Slug stability under regeneration.
        var names1 = Directory.GetFiles(followUpsDir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var names2 = Directory.GetFiles(followUpsDir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(names1, names2);
    }

    [Fact]
    public void FollowUpDetailTemplater_ActionPage_RawDataCopy_NoLinkifyInAttribute()
    {
        // Unit-level: e2e fixtures often have an empty CommandCatalog (no module-help), so pin
        // Resolve-with-AI copy-payload discipline here with an explicit catalog.
        var item = new SprintActionItem(
            "Route Epic 1 debt before Story 2.1 planning",
            "open", 1, "Dana");
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["quick-dev"] = "/bmad-quick-dev",
        });
        var nav = new SiteNav
        {
            SiteTitle = "SpecScribe",
            Items = Array.Empty<(string, string)>(),
            Groups = Array.Empty<(string, IReadOnlyList<(string, string)>)>(),
            QuickLinks = Array.Empty<(string, string, string)>(),
        };
        var html = FollowUpDetailTemplater.RenderActionPage(
            item, "action-route-epic-1-debt", nav, commands);

        Assert.Contains("data-copy=", html);
        Assert.Contains("class=\"followup-detail\"", html);
        Assert.Contains("class=\"chart-panel next-steps\"", html);
        Assert.Contains("next-step-card-primary", html);
        Assert.Contains("Recommended", html);
        Assert.Contains("next-step-desc", html);
        Assert.Contains("Copies a quick-dev prompt", html);
        Assert.Contains("Close with AI", html);
        Assert.Contains(">Close</span>", html); // secondary kicker
        Assert.DoesNotContain("Draft a new story", html);
        Assert.DoesNotContain("Open sprint planning", html);
        foreach (Match m in Regex.Matches(html, "data-copy=\"([^\"]*)\""))
        {
            Assert.DoesNotContain("<a", m.Groups[1].Value);
            Assert.Contains("Route Epic 1 debt before Story 2.1 planning", m.Groups[1].Value);
            Assert.Contains("/bmad-quick-dev", m.Groups[1].Value);
        }
        Assert.Contains("data-copy=\"/bmad-quick-dev Close this retrospective action item", html);
    }

    [Fact]
    public void FollowUpDetailTemplater_DeferredPage_OmitsEpicPill_WhenUnattributed()
    {
        var item = new DeferredWorkItem("<p>Orphan deferred.</p>", false, null, null);
        var nav = new SiteNav
        {
            SiteTitle = "SpecScribe",
            Items = Array.Empty<(string, string)>(),
            Groups = Array.Empty<(string, IReadOnlyList<(string, string)>)>(),
            QuickLinks = Array.Empty<(string, string, string)>(),
        };
        var html = FollowUpDetailTemplater.RenderDeferredPage(
            item, "Deferred work", null, "deferred-orphan", nav, "deferred-work.html");

        Assert.DoesNotContain("pill-link", html);
        Assert.DoesNotContain("epics/epic-", html);
    }

    [Fact]
    public void FollowUpDetailTemplater_DeferredPage_RawDataCopy_AndPrefixedProvenanceHref()
    {
        var item = new DeferredWorkItem(
            "<p>Open FR25 casing mismatch still outstanding.</p>",
            false,
            "1.1",
            "epics/story-1-1.html");
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["quick-dev"] = "/bmad-quick-dev",
        });
        var nav = new SiteNav
        {
            SiteTitle = "SpecScribe",
            Items = Array.Empty<(string, string)>(),
            Groups = Array.Empty<(string, IReadOnlyList<(string, string)>)>(),
            QuickLinks = Array.Empty<(string, string, string)>(),
        };
        var html = FollowUpDetailTemplater.RenderDeferredPage(
            item,
            "code review of 1-1-foundation.md",
            "epics/story-1-1.html",
            "deferred-open-fr25-casing",
            nav,
            "implementation-artifacts/deferred-work.html",
            commands,
            epicNumber: 1);

        Assert.Contains("href=\"../epics/story-1-1.html\"", html);
        Assert.Contains("class=\"pill pill-link\" href=\"../epics/epic-1.html\"", html);
        Assert.Contains("Epic 1", html);
        Assert.Contains("data-copy=", html);
        foreach (Match m in Regex.Matches(html, "data-copy=\"([^\"]*)\""))
        {
            Assert.DoesNotContain("<a", m.Groups[1].Value);
            Assert.Contains("FR25", m.Groups[1].Value);
        }
    }

    [Fact]
    public void DeferredWork_StructuredCards_ResolvedTreatment_HomeCalloutSurvives()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var deferredPath = Path.Combine(Site, "implementation-artifacts", "deferred-work.html");
        Assert.True(File.Exists(deferredPath));
        var html = File.ReadAllText(deferredPath);

        Assert.Contains("class=\"followup-row\"", html);
        Assert.Contains("class=\"followup-row resolved\"", html);
        Assert.Contains(">Resolved</span>", html);
        Assert.DoesNotContain("followup-row-detail", html);
        Assert.Contains("followup-row-primary", html);
        Assert.Contains("href=\"../follow-ups/deferred-", html);

        // Provenance source link still on the group heading.
        Assert.Contains("href=\"../epics/story-1-1.html\"", html);

        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("deferred-work.html", index);
        // One open item (the unresolved casing mismatch) — resolved item must not inflate the count.
        Assert.Contains(">1<", index); // open count chip — loose but WorkInventory contract covered below
    }

    [Fact]
    public void DeferredWork_UnstructuredNote_GetsPerItemRowsAndDetailPages()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), """
            # Deferred Work

            A free-form note without Deferred-from headings.

            - Parked item A
            - Parked item B
            """);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(Path.Combine(Site, "implementation-artifacts", "deferred-work.html"));
        Assert.DoesNotContain("deferred-work-fallback", html);
        Assert.Contains("followup-row", html);
        Assert.Contains("href=\"../follow-ups/deferred-", html);
        Assert.Contains("Parked item A", html);

        var followUpsDir = Path.Combine(Site, "follow-ups");
        Assert.True(Directory.Exists(followUpsDir));
        var deferredPages = Directory.GetFiles(followUpsDir, "deferred-*.html");
        Assert.Equal(2, deferredPages.Length);
        Assert.Contains(deferredPages, p => File.ReadAllText(p).Contains("Parked item A", StringComparison.Ordinal));
        Assert.Contains(deferredPages, p => File.ReadAllText(p).Contains("Parked item B", StringComparison.Ordinal));
    }

    [Fact]
    public void DeferredWork_UnstructuredProseOnly_FallsBackToPlainBody()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), """
            # Deferred Work

            Just prose — no list items at all.
            """);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(Path.Combine(Site, "implementation-artifacts", "deferred-work.html"));
        Assert.Contains("deferred-work-fallback", html);
        Assert.DoesNotContain("followup-row", html);
        Assert.Contains("Just prose", html);
    }

    [Fact]
    public void Degrade_NoActionItems_NoActionItemsPage()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        Assert.False(File.Exists(Path.Combine(Site, "action-items.html")));
        Assert.False(Directory.Exists(Path.Combine(Site, "follow-ups")));
    }

    [Fact]
    public void Degrade_NoDeferredWork_NoDeferredPageOrCallout()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        Assert.False(File.Exists(Path.Combine(Site, "implementation-artifacts", "deferred-work.html")));
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.DoesNotContain("deferred-work.html", index);
    }

    [Fact]
    public void HomeAndEpicSunburst_ShowFollowUpGeometry_WhenOpenItemsExist()
    {
        // Story 9.7 + 9.11 + 10.7: open action items appear as story-ring peers on the project glance; on the
        // epic sunburst, epic-level peers (actions here — the deferred items are story-child, nested under
        // their story) now aggregate into one open/done wedge linking to the group-epic-N page (AC2) instead
        // of one leaf wedge per item.
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), SprintWithDupes);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("sb-followup-open", index);
        Assert.Contains("open vs done follow-ups (aggregated)", index);
        Assert.DoesNotContain("outermost: open follow-ups", index);
        Assert.Contains("href=\"follow-ups/group-", index);
        Assert.Contains("Open follow-up</span>", index);
        // StatCards still present (geometry does not replace them).
        Assert.Contains("Action items", index);
        Assert.Contains("Deferred work", index);

        var epic1 = File.ReadAllText(Path.Combine(Site, "epics", "epic-1.html"));
        Assert.Contains("sb-followup-open", epic1);
        // Epic-level peers (2 open actions) aggregate to the group page — no per-item action wedge/text.
        Assert.Contains("href=\"../follow-ups/group-epic-1.html\"", epic1);
        Assert.DoesNotContain("href=\"../follow-ups/action-", epic1);
        Assert.DoesNotContain("Schedule retros promptly", epic1);
        // Story-child deferred stays nested and unchanged — still climbs out of epics/ (not epics/follow-ups/…, 404).
        Assert.Contains("href=\"../follow-ups/deferred-", epic1);
        Assert.DoesNotContain("href=\"follow-ups/deferred-", epic1);
        Assert.DoesNotContain("href=\"follow-ups/action-", epic1);

        var epic2 = File.ReadAllText(Path.Combine(Site, "epics", "epic-2.html"));
        Assert.Contains("sb-followup-open", epic2);
        Assert.Contains("href=\"../follow-ups/group-epic-2.html\"", epic2);
    }

    [Fact]
    public void FollowUpGroupPages_EmittedForNonEmptyGroups_OnlyThatGroupsItems()
    {
        // Unattributed action → Follow-ups group; epic-attributed → epic group; no Unplanned members.
        const string sprint = """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              1-2-undrafted: backlog
              epic-1-retrospective: done
              epic-2: in-progress
              2-1-delivery: in-progress
              epic-2-retrospective: done
            action_items:
              - epic: 1
                action: "Schedule retros promptly"
                owner: Amelia
                status: open
              - action: "Unscoped cleanup orphan"
                status: open
            """;
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), sprint);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var followUpsDir = Path.Combine(Site, "follow-ups");
        var followUpsGroup = Path.Combine(followUpsDir, "group-follow-ups.html");
        var epic1Group = Path.Combine(followUpsDir, "group-epic-1.html");
        var epic2Group = Path.Combine(followUpsDir, "group-epic-2.html");
        var unplannedGroup = Path.Combine(followUpsDir, "group-unplanned.html");

        Assert.True(File.Exists(followUpsGroup));
        Assert.True(File.Exists(epic1Group));
        // Epic 2 has only a resolved deferred item → still a deferred member → page exists.
        Assert.True(File.Exists(epic2Group));
        // Unattributable deferred from StructuredDeferred? Groups are attributed via story ids 1.1 / 2.1.
        // No unattributable deferred → no Unplanned page unless quick-dev exists.
        Assert.False(File.Exists(unplannedGroup));

        var orphanHtml = File.ReadAllText(followUpsGroup);
        Assert.Contains("Unscoped cleanup orphan", orphanHtml);
        Assert.DoesNotContain("Schedule retros promptly", orphanHtml);
        Assert.Contains("followup-rows-list", orphanHtml);
        Assert.DoesNotContain("data-copy=", orphanHtml);
        Assert.DoesNotContain("?filter=", orphanHtml);
        Assert.DoesNotContain("#group=", orphanHtml);

        var epic1Html = File.ReadAllText(epic1Group);
        Assert.Contains("Schedule retros promptly", epic1Html);
        Assert.Contains("Open casing mismatch", epic1Html);
        Assert.DoesNotContain("Unscoped cleanup orphan", epic1Html);

        // Index sunburst: Follow-ups orphan → filtered group page; epic arc stays epic page;
        // follow-up leaves are aggregated (group-epic-N / group-follow-ups), not per-item detail.
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("href=\"follow-ups/group-follow-ups.html\"", index);
        Assert.Contains("href=\"epics/epic-1.html\"", index);
        Assert.Contains("href=\"follow-ups/group-epic-", index);
        Assert.DoesNotContain("href=\"follow-ups/action-", index);
        // Orphan root must not dump into the whole-site action-items index.
        var orphanIdx = index.IndexOf("aria-label=\"Follow-ups:", StringComparison.Ordinal);
        Assert.True(orphanIdx >= 0);
        var orphanAnchorStart = index.LastIndexOf("<a ", orphanIdx, StringComparison.Ordinal);
        var orphanAnchorEnd = index.IndexOf("</a>", orphanIdx, StringComparison.Ordinal);
        var orphanAnchor = index[orphanAnchorStart..orphanAnchorEnd];
        Assert.Contains("href=\"follow-ups/group-follow-ups.html\"", orphanAnchor);
        Assert.DoesNotContain("href=\"action-items.html\"", orphanAnchor);
    }

    [Fact]
    public void NearDuplicate_CanonicalPairMatches_UnrelatedDoesNot()
    {
        var a = "Route Epic 1's deferred tech debt (heatmap HeatLevel collapse, unmapped ForEpic status classes, non-invariant heatmap date formatting) into the deferred-work backlog for review before Epic 3 planning";
        var b = "Triage Epic 1's deferred heatmap tech debt (HeatLevel collapse on sparse history, unmapped ForEpic status classes, non-invariant heatmap date formatting) before starting Epic 3 Story 3.2 or 3.5 - carried unaddressed across two retrospectives now";
        var c = "Schedule retros promptly";

        Assert.True(ActionItemsTemplater.AreNearDuplicates(a, b));
        Assert.False(ActionItemsTemplater.AreNearDuplicates(a, c));
        Assert.False(ActionItemsTemplater.AreNearDuplicates(b, c));
    }

    [Fact]
    public void FindNearDuplicates_ValueEqualDistinctInstances_BothGetCrossLinks()
    {
        const string epic1Text =
            "Route Epic 1's deferred tech debt (heatmap HeatLevel collapse, unmapped ForEpic status classes, non-invariant heatmap date formatting) into the deferred-work backlog for review before Epic 3 planning";
        const string epic2Text =
            "Triage Epic 1's deferred heatmap tech debt (HeatLevel collapse on sparse history, unmapped ForEpic status classes, non-invariant heatmap date formatting) before starting Epic 3 Story 3.2 or 3.5 - carried unaddressed across two retrospectives now";

        var a = new SprintActionItem(epic1Text, "open", 1, "Dana");
        var a2 = new SprintActionItem(epic1Text, "open", 1, "Dana"); // value-equal, distinct instance
        var b = new SprintActionItem(epic2Text, "open", 2, "Dana");

        Assert.Equal(a, a2);
        Assert.False(ReferenceEquals(a, a2));

        var map = ActionItemsTemplater.FindNearDuplicates(new[] { a, a2, b });
        Assert.True(map.ContainsKey(a));
        Assert.True(map.ContainsKey(a2));
        Assert.True(map.ContainsKey(b));
        Assert.Equal(new[] { 2 }, map[a]);
        Assert.Equal(new[] { 1 }, map[b]);
    }

    [Fact]
    public void FindNearDuplicates_ThreeEpics_ListsAllCounterpartEpicsSorted()
    {
        const string epic1Text =
            "Route Epic 1's deferred tech debt (heatmap HeatLevel collapse, unmapped ForEpic status classes, non-invariant heatmap date formatting) into the deferred-work backlog for review before Epic 3 planning";
        const string epic2Text =
            "Triage Epic 1's deferred heatmap tech debt (HeatLevel collapse on sparse history, unmapped ForEpic status classes, non-invariant heatmap date formatting) before starting Epic 3 Story 3.2 or 3.5 - carried unaddressed across two retrospectives now";
        // Near-duplicate of epic1Text with enough shared significant tokens for Jaccard ≥ 0.45.
        const string epic3Text =
            "Route Epic 1's deferred tech debt (heatmap HeatLevel collapse, unmapped ForEpic status classes, non-invariant heatmap date formatting) into the deferred-work backlog before Epic 3 planning — still open from Epic 3 retro";

        var a = new SprintActionItem(epic1Text, "open", 1, "Dana");
        var b = new SprintActionItem(epic2Text, "open", 2, "Dana");
        var c = new SprintActionItem(epic3Text, "open", 3, "Dana");

        Assert.True(ActionItemsTemplater.AreNearDuplicates(epic1Text, epic3Text));

        var map = ActionItemsTemplater.FindNearDuplicates(new[] { a, b, c });
        Assert.Equal(new[] { 2, 3 }, map[a]);
        Assert.Equal(new[] { 1, 3 }, map[b]);
        Assert.Equal(new[] { 1, 2 }, map[c]);

        var sb = new System.Text.StringBuilder();
        ActionItemsTemplater.AppendCrossLinks(sb, a, map, null, "");
        var cross = sb.ToString();
        Assert.Contains("also raised in Epic 2 retrospective", cross);
        Assert.Contains("also raised in Epic 3 retrospective", cross);
        // Epic 2 before Epic 3 (sorted).
        Assert.True(
            cross.IndexOf("Epic 2", StringComparison.Ordinal) < cross.IndexOf("Epic 3", StringComparison.Ordinal));
    }

    [Fact]
    public void RegenerateEpics_AfterDeferredWorkEdit_RefreshesDeferredListAndDetailPages()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var beforeDetails = Directory.GetFiles(Path.Combine(Site, "follow-ups"), "deferred-*.html");
        Assert.Equal(2, beforeDetails.Length);
        Assert.DoesNotContain(
            Directory.GetFiles(Path.Combine(Site, "follow-ups"), "deferred-*.html")
                .Select(File.ReadAllText),
            h => h.Contains("Brand-new watch deferred item", StringComparison.Ordinal));

        // Watch routes deferred-work.md → RegenerateEpics (IsEpicsRelated), not GenerateOne.
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), """
            # Deferred Work

            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Open casing mismatch still outstanding.
            - Brand-new watch deferred item.

            ## Deferred from: code review of 2-1-delivery.md (2026-07-15)

            - **[RESOLVED in 2.1]** ~~Already fixed in Story 2.1~~ — kept for the audit trail.
            """);

        var ev = gen.RegenerateEpics();
        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);

        var deferredList = File.ReadAllText(Path.Combine(Site, "implementation-artifacts", "deferred-work.html"));
        Assert.Contains("Brand-new watch deferred item", deferredList);

        var afterDetails = Directory.GetFiles(Path.Combine(Site, "follow-ups"), "deferred-*.html");
        Assert.True(afterDetails.Length >= 3);
        Assert.Contains(
            afterDetails.Select(File.ReadAllText),
            h => h.Contains("Brand-new watch deferred item", StringComparison.Ordinal));

        // Group pages also refresh (9.13 prune/membership).
        Assert.True(File.Exists(Path.Combine(Site, "follow-ups", "group-epic-1.html")));
        Assert.Contains(
            "Brand-new watch deferred item",
            File.ReadAllText(Path.Combine(Site, "follow-ups", "group-epic-1.html")));

        // Resolve the new open bullet — list must show it settled; group may still list it as Resolved.
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), """
            # Deferred Work

            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Open casing mismatch still outstanding.
            - **[RESOLVED in 1.1]** ~~Brand-new watch deferred item.~~

            ## Deferred from: code review of 2-1-delivery.md (2026-07-15)

            - **[RESOLVED in 2.1]** ~~Already fixed in Story 2.1~~ — kept for the audit trail.
            """);
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateEpics().Outcome);

        var afterResolve = File.ReadAllText(Path.Combine(Site, "implementation-artifacts", "deferred-work.html"));
        Assert.Contains("Brand-new watch deferred item", afterResolve);
        Assert.Contains("class=\"followup-row resolved\"", afterResolve);
        var groupAfterResolve = File.ReadAllText(Path.Combine(Site, "follow-ups", "group-epic-1.html"));
        Assert.Contains("Brand-new watch deferred item", groupAfterResolve);
        Assert.Contains(">Resolved</span>", groupAfterResolve);
    }

    // ---- List-batch Address/Close pane (spec-follow-up-list-batch-actions) ----------------------------

    [Fact]
    public void DeferredWorkTemplater_ListBatchPane_ThreeCards_SingleDeferredButton_WhenSixOrMoreOpen()
    {
        var markdown = "## Deferred from: misc (2026-07-15)\n\n"
            + string.Join("\n", Enumerable.Range(1, 6).Select(i => $"- Open deferred item {i}."));
        var model = DeferredWorkParser.Parse(markdown);
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = DeferredWorkTemplater.RenderPage(
            model, nav, "implementation-artifacts/deferred-work.html", "Deferred Work", commands);

        Assert.Contains("class=\"chart-panel next-steps list-batch-actions\"", html);
        Assert.Contains(">Address all<", html);
        Assert.Contains(">Address first 5<", html);
        Assert.Contains(">Close all<", html);
        // Deferred-only page: single Deferred button per card, never an Action items pair.
        Assert.DoesNotContain("<span class=\"cmd-text\">Action items</span>", html);
        Assert.DoesNotContain("next-step-command-group", html);
        Assert.Contains("data-copy=\"/bmad-quick-dev Address open deferred work on Deferred Work (6 items)", html);
        Assert.Contains("data-copy=\"/bmad-quick-dev Address open deferred work on Deferred Work (first 5 of 6 open items)", html);
        Assert.Contains("data-copy=\"/bmad-quick-dev Close open deferred work on Deferred Work (6 items)", html);
        Assert.Contains("Only close an item if the work is already complete", html);
        // Item must appear inside a batch data-copy payload — not merely in a list row.
        var batchCopies = Regex.Matches(html, "data-copy=\"([^\"]*)\"")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Contains(batchCopies, payload => payload.Contains("Open deferred item 1.", StringComparison.Ordinal));

        foreach (var payload in batchCopies)
        {
            Assert.DoesNotContain("<a", payload);
            Assert.DoesNotContain("../follow-ups/", payload);
        }
    }

    [Fact]
    public void DeferredWorkTemplater_ListBatchPane_NoFirst5_WhenUnderSixOpen()
    {
        var markdown = "## Deferred from: misc (2026-07-15)\n\n"
            + string.Join("\n", Enumerable.Range(1, 3).Select(i => $"- Open deferred item {i}."));
        var model = DeferredWorkParser.Parse(markdown);
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = DeferredWorkTemplater.RenderPage(
            model, nav, "deferred-work.html", "Deferred Work", commands);

        Assert.Contains("class=\"chart-panel next-steps list-batch-actions\"", html);
        Assert.Contains(">Address all<", html);
        Assert.DoesNotContain(">Address first 5<", html);
        Assert.Contains(">Close all<", html);
    }

    [Fact]
    public void DeferredWorkTemplater_ListBatchPane_Omitted_WhenNoQuickDev()
    {
        var markdown = "## Deferred from: misc (2026-07-15)\n\n- Open deferred item.";
        var model = DeferredWorkParser.Parse(markdown);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = DeferredWorkTemplater.RenderPage(
            model, nav, "deferred-work.html", "Deferred Work", CommandCatalog.Empty);

        Assert.DoesNotContain("list-batch-actions", html);
        Assert.DoesNotContain("data-copy=", html);
    }

    [Fact]
    public void DeferredWorkTemplater_ListBatchPane_Omitted_WhenZeroOpenItems()
    {
        var markdown = """
            ## Deferred from: misc (2026-07-15)

            - **[RESOLVED]** ~~Already fixed.~~
            """;
        var model = DeferredWorkParser.Parse(markdown);
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = DeferredWorkTemplater.RenderPage(model, nav, "deferred-work.html", "Deferred Work", commands);

        Assert.DoesNotContain("list-batch-actions", html);
    }

    [Fact]
    public void ActionItemsTemplater_ListBatchPane_NoFirst5_WhenUnderSixOpen()
    {
        var open = Enumerable.Range(1, 3)
            .Select(i => new SprintActionItem($"Fix issue {i}", "open", 1, "Dana"))
            .ToArray();
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, null, commands, nav);

        Assert.Contains("class=\"chart-panel next-steps list-batch-actions\"", html);
        Assert.Contains(">Address all<", html);
        Assert.DoesNotContain(">Address first 5<", html);
        Assert.Contains(">Close all<", html);
        // Action-only page: single Action items button per card, never a Deferred pair.
        Assert.DoesNotContain("<span class=\"cmd-text\">Deferred</span>", html);
        Assert.DoesNotContain("next-step-command-group", html);
        Assert.Contains("data-copy=\"/bmad-quick-dev Resolve open retrospective action items on Open Action Items (3 items)", html);
        Assert.Contains("data-copy=\"/bmad-quick-dev Close open retrospective action items on Open Action Items (3 items)", html);
    }

    [Fact]
    public void ActionItemsTemplater_ListBatchPane_Omitted_WhenNoQuickDev()
    {
        var open = new[] { new SprintActionItem("Fix issue", "open", 1, "Dana") };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, null, CommandCatalog.Empty, nav);

        Assert.DoesNotContain("list-batch-actions", html);
    }

    [Fact]
    public void ActionItemsTemplater_ListBatchPane_RawDataCopy_NoLinkifyInAttribute()
    {
        // Six-plus open items with Story N.M mentions — the visible row summary linkifies these (separate
        // <a> elements); the batch pane's data-copy payload must stay raw regardless. [Story 9.6 trap]
        var open = Enumerable.Range(1, 6)
            .Select(i => new SprintActionItem($"Fix Story 1.{i} heatmap debt before Epic 2", "open", 1, "Dana"))
            .ToArray();
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, null, commands, nav);

        Assert.Contains("Story 1.1", html);
        var matches = Regex.Matches(html, "data-copy=\"([^\"]*)\"");
        Assert.NotEmpty(matches);
        foreach (Match m in matches)
        {
            Assert.DoesNotContain("<a", m.Groups[1].Value);
            Assert.DoesNotContain("&lt;a", m.Groups[1].Value);
        }
    }

    [Fact]
    public void GenerateOne_RefreshesDeferredListAndDetailPages()
    {
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation: done
              epic-1-retrospective: optional
            """);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);
        var planningDoc = Path.Combine(Source, "planning-artifacts", "watch-parity-note.md");
        File.WriteAllText(planningDoc, "# Watch Parity Note\n\nA non-epics planning doc for GenerateOne.\n");

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), """
            # Deferred Work

            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Open casing mismatch still outstanding.
            - GenerateOne-parity deferred item.

            ## Deferred from: code review of 2-1-delivery.md (2026-07-15)

            - **[RESOLVED in 2.1]** ~~Already fixed in Story 2.1~~ — kept for the audit trail.
            """);
        // Touch the planning doc so GenerateOne has a real source change; deferred sync is inside RefreshFollowUpSurfaces.
        File.WriteAllText(planningDoc, "# Watch Parity Note\n\nUpdated.\n");

        var ev = gen.GenerateOne(planningDoc);
        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);

        Assert.Contains(
            "GenerateOne-parity deferred item",
            File.ReadAllText(Path.Combine(Site, "implementation-artifacts", "deferred-work.html")));
        Assert.Contains(
            Directory.GetFiles(Path.Combine(Site, "follow-ups"), "deferred-*.html").Select(File.ReadAllText),
            h => h.Contains("GenerateOne-parity deferred item", StringComparison.Ordinal));
    }
}
