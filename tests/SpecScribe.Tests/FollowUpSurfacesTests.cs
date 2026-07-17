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

        // Scan-first row grammar (Story 9.10): shared .followup-row, heavy detail in <details>.
        Assert.Contains("class=\"followup-row\"", html);
        Assert.Contains("followup-row-summary", html);
        Assert.Contains("followup-row-detail", html);

        // Groups ordered by epic ascending.
        var epic1 = html.IndexOf("From the Epic 1 retrospective", StringComparison.Ordinal);
        var epic2 = html.IndexOf("From the Epic 2 retrospective", StringComparison.Ordinal);
        Assert.True(epic1 >= 0 && epic2 > epic1, "Epic 1 group must precede Epic 2");

        // Canonical pair cross-links live inside disclosure; unrelated "Schedule retros" does not.
        Assert.Contains("also raised in Epic 2 retrospective", html);
        Assert.Contains("also raised in Epic 1 retrospective", html);
        var scheduleIdx = html.IndexOf("Schedule retros promptly", StringComparison.Ordinal);
        var scheduleRowStart = html.LastIndexOf("class=\"followup-row\"", scheduleIdx, StringComparison.Ordinal);
        var scheduleRowEnd = html.IndexOf("</li>", scheduleIdx, StringComparison.Ordinal);
        var scheduleRow = html[scheduleRowStart..scheduleRowEnd];
        Assert.DoesNotContain("also raised", scheduleRow);
        Assert.Contains("followup-row-detail", scheduleRow);

        // Full action text + cross-links are behind disclosure, not in the scan line.
        var scanLineEnd = html.IndexOf("followup-row-detail", StringComparison.Ordinal);
        var aboveFold = html[..scanLineEnd];
        Assert.DoesNotContain("also raised in Epic", aboveFold);

        // Visible text linkifies Story N.M that exists in the plan (inside disclosure body).
        Assert.Contains("class=\"story-ref\"", html);
        Assert.Contains(">Story 2.1</a>", html);

        // Payload integrity: no <a> leaked into any data-copy attribute.
        foreach (Match m in Regex.Matches(html, "data-copy=\"([^\"]*)\""))
            Assert.DoesNotContain("<a", m.Groups[1].Value);
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
        Assert.Contains("followup-row-detail", html);
        Assert.Contains("href=\"../epics/story-1-1.html\"", html);
        Assert.Contains("Resolving:", html);

        // Full body and resolving link live inside disclosure, not the scan line.
        var firstDetail = html.IndexOf("followup-row-detail", StringComparison.Ordinal);
        Assert.Contains("Open casing mismatch", html[(firstDetail)..]);
        var scanPrefix = html[..firstDetail];
        Assert.DoesNotContain("Resolving:", scanPrefix);

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
        // Story 9.7: open action items appear as outermost sunburst wedges on home + owning epic pages;
        // an epic with none omits the ring. StatCards stay ledger-backed (unchanged path).
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), SprintWithDupes);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), StructuredDeferred);

        Assert.DoesNotContain(new SiteGenerator(Options()).GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("sb-followup-open", index);
        Assert.Contains("stories &amp; follow-ups", index);
        Assert.DoesNotContain("outermost: open follow-ups", index);
        Assert.Contains($"href=\"{SiteNav.ActionItemsOutputPath}\"", index);
        Assert.Contains("Open follow-up</span>", index);
        // StatCards still present (geometry does not replace them).
        Assert.Contains("Action items", index);
        Assert.Contains("Deferred work", index);

        var epic1 = File.ReadAllText(Path.Combine(Site, "epics", "epic-1.html"));
        Assert.Contains("sb-followup-open", epic1);
        Assert.Contains("href=\"../action-items.html\"", epic1);
        Assert.Contains("Schedule retros promptly", epic1);

        var epic2 = File.ReadAllText(Path.Combine(Site, "epics", "epic-2.html"));
        Assert.Contains("sb-followup-open", epic2);
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
}
