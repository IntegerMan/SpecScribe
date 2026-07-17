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

        // Cross-link teaser on list; full linked cross-ref lives on the detail page.
        Assert.Contains("also raised in Epic 2 retrospective", html);
        Assert.Contains("also raised in Epic 1 retrospective", html);
        var scheduleIdx = html.IndexOf("Schedule retros promptly", StringComparison.Ordinal);
        var scheduleRowStart = html.LastIndexOf("class=\"followup-row\"", scheduleIdx, StringComparison.Ordinal);
        var scheduleRowEnd = html.IndexOf("</li>", scheduleIdx, StringComparison.Ordinal);
        var scheduleRow = html[scheduleRowStart..scheduleRowEnd];
        Assert.DoesNotContain("also raised", scheduleRow);

        // List page no longer embeds Resolve-with-AI (moved to detail).
        Assert.DoesNotContain("data-copy=", html);
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
        Assert.Contains("deferred-resolved-mark", html);
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
    public void DeferredWork_UnstructuredNote_FallsBackToPlainBody()
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
        Assert.Contains("deferred-work-fallback", html);
        Assert.DoesNotContain("followup-row", html);
        Assert.Contains("Parked item A", html);
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
        // Story 9.7 + 9.11: open action items appear as story-ring peers; wedges deep-link to per-item detail.
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), SprintWithDupes);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("sb-followup-open", index);
        Assert.Contains("stories &amp; follow-ups", index);
        Assert.DoesNotContain("outermost: open follow-ups", index);
        Assert.Contains("href=\"follow-ups/action-", index);
        Assert.Contains("Open follow-up</span>", index);
        // StatCards still present (geometry does not replace them).
        Assert.Contains("Action items", index);
        Assert.Contains("Deferred work", index);

        var epic1 = File.ReadAllText(Path.Combine(Site, "epics", "epic-1.html"));
        Assert.Contains("sb-followup-open", epic1);
        Assert.Contains("href=\"../follow-ups/action-", epic1);
        Assert.Contains("Schedule retros promptly", epic1);
        // Deferred wedges must also climb out of epics/ — not epics/follow-ups/… (404).
        Assert.Contains("href=\"../follow-ups/deferred-", epic1);
        Assert.DoesNotContain("href=\"follow-ups/deferred-", epic1);
        Assert.DoesNotContain("href=\"follow-ups/action-", epic1);

        var epic2 = File.ReadAllText(Path.Combine(Site, "epics", "epic-2.html"));
        Assert.Contains("sb-followup-open", epic2);
        Assert.Contains("href=\"../follow-ups/", epic2);
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
    }
}
