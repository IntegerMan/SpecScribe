using SpecScribe;

namespace SpecScribe.Tests;

public class StatusStylesTests
{
    private static StoryInfo Story(string? status) => new()
    {
        Id = "1.1",
        EpicNumber = 1,
        Title = "A story",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
    };

    private static EpicInfo Epic(EpicStatus status, params StoryInfo[] stories) => new()
    {
        Number = 1,
        Title = "An epic",
        GoalHtml = string.Empty,
        Status = status,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    [Theory]
    [InlineData("done", "done")]
    [InlineData("Complete", "done")]
    [InlineData("ready-for-review", "review")]
    [InlineData("in progress", "active")]
    [InlineData("in-dev", "active")]
    [InlineData("active", "active")]
    [InlineData("WIP", "active")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData("drafted", "drafted")]
    [InlineData("something else", "unrecognized")]
    [InlineData("frobnicated", "unrecognized")]
    [InlineData(null, "drafted")]
    [InlineData("", "drafted")]
    [InlineData("   ", "drafted")]
    public void ForStory_MapsStatusKeywords(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForStory(Story(status)));

    [Fact]
    public void ForStoryDisplay_UsesNoPlanOnlyWhenStatusAndTaskTallyAreAbsent()
    {
        var doneWithoutChecklist = Story("done");
        doneWithoutChecklist.TasksTotal = 0;

        var unclassifiedWithoutChecklist = Story(null);
        unclassifiedWithoutChecklist.TasksTotal = 0;

        Assert.Equal("done", StatusStyles.ForStoryDisplay(doneWithoutChecklist));
        Assert.Equal("noplan", StatusStyles.ForStoryDisplay(unclassifiedWithoutChecklist));
    }

    [Fact]
    public void ForEpic_PendingOrStorylessEpicsArePending()
    {
        Assert.Equal("pending", StatusStyles.ForEpic(Epic(EpicStatus.Pending, Story("done"))));
        Assert.Equal("pending", StatusStyles.ForEpic(Epic(EpicStatus.Drafted)));
    }

    [Fact]
    public void ForEpic_StorylessPreparedPhaseIsDrafted()
    {
        var phase = new EpicInfo
        {
            Number = 1,
            Title = "Prepared GSD phase",
            GoalHtml = string.Empty,
            HasDiscussionLog = true,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = Array.Empty<StoryInfo>(),
        };

        Assert.Equal("drafted", StatusStyles.ForEpic(phase));
    }

    [Fact]
    public void ForEpic_DoneOnlyWhenEveryStoryIsDone()
    {
        Assert.Equal("done", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"), Story("complete"))));
        Assert.Equal("active", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"), Story("ready-for-dev"))));
    }

    [Fact]
    public void ForEpic_ReadyWhenAnyStoryIsReadyAndNoneFurther()
    {
        // Any ready-for-dev story (with none in dev/review/done) lifts the epic to the ready tier, mirroring
        // the "any active → active" rule. [spec-sunburst-epic-focus-and-ready-rollup]
        Assert.Equal("ready", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null), Story("ready-for-dev"))));
        Assert.Equal("ready", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("ready-for-dev"), Story("ready-for-dev"))));
    }

    [Fact]
    public void ForEpic_DraftedOnlyWhenNoStoryIsReadyOrFurther()
        => Assert.Equal("drafted", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null), Story("something else"))));

    [Fact]
    public void ForEpic_AllUnrecognizedStoriesAreUnrecognized()
        => Assert.Equal("unrecognized",
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("frobnicated"), Story("something else"))));

    [Fact]
    public void EpicStages_CoversEveryForEpicOutputAndEachHasALabel()
    {
        // Representative epics exercising each reachable epic-class branch. EpicStages is the single list the Epic
        // Status donut iterates (over ForEpicWithRetrospective, which adds a "review" tier for all-done-no-retro
        // epics), so binding those real outputs to it (both directions) guarantees a class can never silently
        // drop from the donut, nor an EpicStages member be dead. [heatmap-debt-triage; spec-sunburst-retro]
        var outputs = new[]
        {
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"))),                          // done
            // Story 8.9: `retired` is reachable ONLY from an all-retired epic (a mixed done+retired epic reads
            // done, per owner decision D1), so that is the representative this list needs to stay non-dead.
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("retired"))),                       // retired
            StatusStyles.ForEpicWithRetrospective(Epic(EpicStatus.Drafted, Story("done"))),         // review (no retro)
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("in progress"))),                   // active
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("ready-for-dev"))),                 // ready
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null))),                            // drafted
            StatusStyles.ForEpic(Epic(EpicStatus.Pending, Story("done"))),                          // pending
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("frobnicated"))),                   // unrecognized
        };

        Assert.All(outputs, o => Assert.Contains(o, StatusStyles.EpicStages));
        Assert.Equal(StatusStyles.EpicStages.OrderBy(s => s), outputs.Distinct().OrderBy(s => s));
        // Each stage maps to its OWN non-empty label. Distinctness is the real guard: a stage added to
        // EpicStages but missing from EpicLabel's switch would fall through to the `_ => "Pending"` default
        // and collide with the genuine "pending" label — a plain non-empty check could never catch that.
        var labels = StatusStyles.EpicStages.Select(StatusStyles.EpicLabel).ToList();
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    [Theory]
    [InlineData("done", "Done")]
    [InlineData("review", "In review")]
    [InlineData("active", "In development")]
    [InlineData("ready", "Ready for dev")]
    [InlineData("drafted", "Stories drafted")]
    [InlineData("pending", "Pending")]
    [InlineData("unrecognized", "Unrecognized")]
    public void EpicLabel_MapsEachTier(string cssClass, string expected)
        => Assert.Equal(expected, StatusStyles.EpicLabel(cssClass));

    [Fact]
    public void ForEpicWithRetrospective_DowngradesDoneToReviewOnlyWhenNoRetro()
    {
        // All stories done, no retro parsed yet → "review" (delivered, retro pending).
        var noRetro = Epic(EpicStatus.Drafted, Story("done"), Story("complete"));
        Assert.False(noRetro.HasRetrospective);
        Assert.Equal("review", StatusStyles.ForEpicWithRetrospective(noRetro));

        // Same epic once a retrospective exists → back to "done".
        var withRetro = Epic(EpicStatus.Drafted, Story("done"), Story("complete"));
        withRetro.HasRetrospective = true;
        Assert.Equal("done", StatusStyles.ForEpicWithRetrospective(withRetro));
    }

    [Fact]
    public void ForEpicWithRetrospective_LeavesDoneWhenTheFrameworkHasNoRetroWorkflow()
    {
        var gsdPhase = new EpicInfo
        {
            Number = 1,
            Title = "A GSD phase",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("done"), Story("complete") },
            RequiresRetrospective = false,
        };

        Assert.False(gsdPhase.HasRetrospective);
        Assert.Equal("done", StatusStyles.ForEpicWithRetrospective(gsdPhase));
    }

    [Fact]
    public void ForEpicWithRetrospective_LeavesNonDoneTiersUntouchedRegardlessOfRetro()
    {
        // Only the "done" tier is retro-gated; every other tier is exactly what ForEpic returns, even if a
        // (spurious) retro flag is set — the downgrade must never invent a "review" from a partial epic.
        var active = Epic(EpicStatus.Drafted, Story("done"), Story("ready-for-dev"));
        Assert.Equal("active", StatusStyles.ForEpicWithRetrospective(active));

        var ready = Epic(EpicStatus.Drafted, Story("ready-for-dev"));
        ready.HasRetrospective = true;
        Assert.Equal("ready", StatusStyles.ForEpicWithRetrospective(ready));

        var pending = Epic(EpicStatus.Pending, Story("done"));
        Assert.Equal("pending", StatusStyles.ForEpicWithRetrospective(pending));
    }

    [Theory]
    [InlineData("done", "Done")]
    [InlineData("review", "In review")]
    [InlineData("active", "In development")]
    [InlineData("ready", "Ready for dev")]
    [InlineData("drafted", "Drafted")]
    [InlineData("pending", "Pending")]
    [InlineData("unrecognized", "Unrecognized")]
    // Story 8.9: without its own arm "retired" lands on the `_ => "Pending"` fallback and a grey retired badge
    // would read as active-plan language — a quieter mislabel than the "Unrecognized" this story removes.
    [InlineData("retired", "Retired")]
    public void StoryLabel_MapsEachStage(string cssClass, string expected)
        => Assert.Equal(expected, StatusStyles.StoryLabel(cssClass));

    [Fact]
    public void StoryLabel_AndEpicLabel_CoverEveryStageTheirOwnListDeclares()
    {
        // The partition lists and the label switches are two halves of one contract: a stage present in the
        // list but absent from the switch renders the fallback word under its own class. Asserting "not the
        // fallback" catches a future stage added to one half only — which is exactly how `retired` shipped
        // with a first-class colour and no first-class word. [Story 8.9 Trap 1]
        foreach (var stage in StatusStyles.StoryStages)
            Assert.NotEqual("Pending", StatusStyles.StoryLabel(stage));
        foreach (var stage in StatusStyles.EpicStages.Where(s => s != "pending"))
            Assert.NotEqual("Pending", StatusStyles.EpicLabel(stage));
        Assert.Equal("Pending", StatusStyles.EpicLabel("pending"));
    }

    [Theory]
    [InlineData("done", "done")]
    [InlineData("complete", "done")]
    [InlineData("completed", "done")]
    [InlineData("done.", "done")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData("Ready for Dev", "ready")]
    [InlineData("ready_for_dev", "ready")]
    [InlineData("in progress", "active")]
    [InlineData("in-progress", "active")]
    [InlineData("still-in-dev", "active")]
    [InlineData("incomplete", "unrecognized")]
    [InlineData("not-complete", "unrecognized")]
    [InlineData("almost-complete", "unrecognized")]
    [InlineData("frobnicated", "unrecognized")]
    [InlineData(null, "drafted")]
    [InlineData("", "drafted")]
    // ---- Story 8.9 AC #1: the six-word retirement vocabulary, in every form ForStatus already tolerates ----
    [InlineData("retired", "retired")]
    [InlineData("Retired", "retired")]
    [InlineData("RETIRED", "retired")]
    [InlineData("retired.", "retired")]
    [InlineData("  retired  ", "retired")]
    [InlineData("superseded", "retired")]
    [InlineData("Superseded", "retired")]
    [InlineData("deprecated", "retired")]
    [InlineData("cancelled", "retired")]
    [InlineData("obsolete", "retired")]
    [InlineData("wontfix", "retired")]
    [InlineData("wont-fix", "retired")]
    [InlineData("wont_fix", "retired")]
    [InlineData("wont fix", "retired")]
    [InlineData("WontFix", "retired")]
    // The apostrophe forms are SUPPORTED, not merely tolerated: Normalize lowercases and kebabs but leaves the
    // apostrophe, so "won't fix" arrives as "won't-fix"; IsRetirementWord strips it (and the typographic ’ a
    // smart-quoting editor produces) before matching. Pinning both so neither can silently regress.
    [InlineData("won't fix", "retired")]
    [InlineData("Won't Fix", "retired")]
    [InlineData("won’t fix", "retired")]
    // Narrowed, never removed (Story 8.2 AC #3): a genuinely unmapped word still reads unrecognized, and a
    // retirement word embedded in a longer phrase is NOT a retirement — the exact-match discipline holds.
    [InlineData("not-retired", "unrecognized")]
    [InlineData("retired?maybe", "unrecognized")]
    [InlineData("rejected", "unrecognized")]
    public void ForStatus_MapsRawStatusText(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForStatus(status));

    [Fact]
    public void RetirementStatusWords_IsTheOwnerLockedSixWordVocabulary()
    {
        // Owner decision D3. The list is the CONTRACT, not an implementation detail: EpicsParser's
        // comment detector builds its regex from this same array, so a word added here widens both seams at
        // once and a word added anywhere else is a finding. [Story 8.9 AC #1]
        Assert.Equal(
            new[] { "retired", "superseded", "deprecated", "cancelled", "obsolete", "wontfix" },
            StatusStyles.RetirementStatusWords);
        foreach (var word in StatusStyles.RetirementStatusWords)
            Assert.Equal("retired", StatusStyles.ForStatus(word));
    }

    [Fact]
    public void ForSprint_StaysNarrowerThanForStatus_OnPurpose()
    {
        // Story 8.9 Trap 8 / scope guard #1: ForSprint reads a CLOSED set of sprint-status.yaml values where
        // only "retired" is ever written, and FreeTextBadge consults it FIRST — so teaching it "superseded"
        // would flip an ADR whose status line reads "Superseded" from its muted strikethrough pill to a
        // canonical Retired badge. The asymmetry is deliberate; this test is what keeps it from being "fixed".
        Assert.Equal("retired", StatusStyles.ForSprint("retired"));
        Assert.Equal("unrecognized", StatusStyles.ForSprint("superseded"));
        Assert.Equal("unrecognized", StatusStyles.ForSprint("wontfix"));
        Assert.Contains("pill status-superseded", StatusStyles.FreeTextBadge("Superseded by ADR 2"));
    }

    [Fact]
    public void LegendKey_StageWordsComeFromLabelHelpers()
    {
        var html = StatusStyles.LegendKey();
        foreach (var stage in StatusStyles.LegendStages)
        {
            var word = stage switch
            {
                "deferred" => StatusStyles.RequirementLabel(RequirementStatus.Deferred),
                "unmapped" => StatusStyles.RequirementLabel(RequirementStatus.Unmapped),
                "retired" => StatusStyles.SprintLabel("retired"),
                _ => StatusStyles.StoryLabel(stage),
            };
            Assert.Contains($">{word}</span>", html);
        }
    }

    [Theory]
    // development_status lifecycle onto the shared six-stage vocabulary. [Story 2.3 Task 2]
    [InlineData("done", "done")]
    [InlineData("review", "review")]
    [InlineData("in-progress", "active")]
    [InlineData("in progress", "active")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData("ready for dev", "ready")]
    [InlineData("backlog", "pending")]
    // retrospective + action-item statuses ride the same colors.
    [InlineData("optional", "pending")]
    [InlineData("open", "ready")]
    // present-but-unmapped → unrecognized; retired is first-class; empty/null stays pending. [Story 8.2 AC #3]
    [InlineData("blocked", "unrecognized")]
    [InlineData("retired", "retired")]
    [InlineData("Retired", "retired")]
    [InlineData("RETIRED", "retired")]
    [InlineData("", "pending")]
    [InlineData(null, "pending")]
    public void ForSprint_MapsLifecycleOntoSharedColors(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForSprint(status));

    [Theory]
    [InlineData("done", "Done")]
    [InlineData("review", "In review")]
    [InlineData("in-progress", "In progress")]
    [InlineData("ready-for-dev", "Ready for dev")]
    [InlineData("backlog", "Backlog")]
    [InlineData("optional", "Optional")]
    [InlineData("open", "Open")]
    [InlineData("retired", "Retired")]
    // forward-compat value still reads as a real word (title-cased), never a raw token.
    [InlineData("blocked", "Blocked")]
    // [Story 18.4 review] Spaced aliases of a KNOWN value now kebab-normalize onto the same canonical label
    // ForSprint already uses — before this story, a spaced "in progress" fell through to the TitleCase fallback
    // and rendered "In Progress" (capital P), disagreeing with ForSprint's colour for the same value.
    [InlineData("in progress", "In progress")]
    [InlineData("ready for dev", "Ready for dev")]
    // An UNMAPPED spaced value still falls through to the TitleCase fallback untouched — only known kebab forms
    // are affected by the normalization.
    [InlineData("code review", "Code Review")]
    public void SprintLabel_MapsEachLifecycleValueToAWord(string status, string expected)
        => Assert.Equal(expected, StatusStyles.SprintLabel(status));

    // ---- Story 2.5: status icon anchored to this one seam --------------------------------------

    [Theory]
    [InlineData("done")]
    [InlineData("active")]
    [InlineData("review")]
    [InlineData("ready")]
    [InlineData("drafted")]
    [InlineData("pending")]
    [InlineData("deferred")]
    [InlineData("retired")]
    [InlineData("unrecognized")]
    public void Icon_ReturnsAGlyphForEveryKnownCssClass(string cssClass)
        => Assert.False(string.IsNullOrEmpty(StatusStyles.Icon(cssClass)));

    [Fact]
    public void Icon_UnknownCssClassReturnsEmpty()
        => Assert.Equal(string.Empty, StatusStyles.Icon("not-a-real-status"));

    [Fact]
    public void Badge_RendersIconAndTextInsideTheStatusBadgeSpan()
    {
        var badge = StatusStyles.Badge("done", "Done");
        // One combined assert: class + icon + label share the same span (Story 2.5 deferred co-location).
        Assert.Contains(
            $"class=\"status-badge done js-tip\" data-tip=\"{PathUtil.Html(StatusStyles.StageMeaning("done"))}\" " +
            $"title=\"{PathUtil.Html(StatusStyles.StageMeaning("done"))}\">{Icons.ForStatus("done")}Done</span>",
            badge);
        Assert.StartsWith("<span ", badge);
        Assert.EndsWith("</span>", badge);
    }

    [Fact]
    public void Badge_EscapesHostileCssClass()
    {
        var hostile = "done\" onmouseover=\"x";
        var badge = StatusStyles.Badge(hostile, "X");
        var escaped = PathUtil.Html(hostile);
        var tip = PathUtil.Html(StatusStyles.StageMeaning(hostile));
        // Full escaped badge: no attribute breakout; icon (empty for unknown class) + label in same span.
        Assert.Equal(
            $"<span class=\"status-badge {escaped} js-tip\" data-tip=\"{tip}\" title=\"{tip}\">{Icons.ForStatus(hostile)}X</span>",
            badge);
        Assert.DoesNotContain("onmouseover=\"", badge);
    }

    // ---- Story 8.2: stage meanings, tooltips, legend key --------------------------------------

    [Theory]
    [InlineData("pending")]
    [InlineData("drafted")]
    [InlineData("ready")]
    [InlineData("active")]
    [InlineData("review")]
    [InlineData("done")]
    [InlineData("deferred")]
    [InlineData("retired")]
    [InlineData("unrecognized")]
    public void StageMeaning_ReturnsNonEmptyMeaningForEveryLegendStage(string cssClass)
        => Assert.False(string.IsNullOrWhiteSpace(StatusStyles.StageMeaning(cssClass)));

    [Fact]
    public void StageMeaning_RetiredIsDistinctFromDeferred()
    {
        Assert.NotEqual(StatusStyles.StageMeaning("retired"), StatusStyles.StageMeaning("deferred"));
        Assert.Contains("ledger", StatusStyles.StageMeaning("retired"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Badge_AttachesJsTipAndDataTipFromStageMeaning()
    {
        var tip = StatusStyles.StageMeaning("ready");
        var badge = StatusStyles.Badge("ready", "Ready for dev");
        Assert.Contains(
            $"class=\"status-badge ready js-tip\" data-tip=\"{PathUtil.Html(tip)}\" title=\"{PathUtil.Html(tip)}\">" +
            $"{Icons.ForStatus("ready")}Ready for dev</span>",
            badge);
    }

    [Fact]
    public void LegendKey_RendersOnDemandDisclosureWithEveryCanonicalStage()
    {
        var html = StatusStyles.LegendKey();
        Assert.Contains("class=\"status-legend\"", html);
        Assert.Contains("status-legend-toggle", html);
        Assert.Contains("Show status legend", html);
        Assert.Contains("Status legend", html);
        Assert.DoesNotContain("status-legend-key-text", html); // single-column row, not stacked footer cells
        foreach (var stage in StatusStyles.LegendStages)
        {
            // Unmapped reuses the pending/tan swatch (no 7th token) while icon + meaning stay on "unmapped". [Story 9.9]
            var swatchClass = stage == "unmapped" ? "pending" : stage;
            Assert.Contains($"status-legend-key-swatch {swatchClass}", html);
            Assert.Contains(StatusStyles.StageMeaning(stage), html);
        }
        // Static reference key — no zero-suppression (all legend stages present).
        Assert.Equal(StatusStyles.LegendStages.Count, System.Text.RegularExpressions.Regex.Matches(html, "status-legend-key-row").Count);
        Assert.Contains("retired", StatusStyles.LegendStages);
        Assert.Contains("unmapped", StatusStyles.LegendStages);
        Assert.Contains("Not yet mapped", html);
        Assert.Contains(Icons.ForStatus("unmapped"), html);
    }

    [Fact]
    public void IsUnrecognizedStatus_AbsentStaysFalse_PresentUnmappedIsTrue()
    {
        Assert.False(StatusStyles.IsUnrecognizedStatus(null));
        Assert.False(StatusStyles.IsUnrecognizedStatus(""));
        Assert.False(StatusStyles.IsUnrecognizedStatus("ready-for-dev"));
        Assert.True(StatusStyles.IsUnrecognizedStatus("frobnicated"));
    }

    [Fact]
    public void IsUnrecognizedSprintStatus_EmptyStaysFalse_PresentUnmappedIsTrue()
    {
        Assert.False(StatusStyles.IsUnrecognizedSprintStatus(null));
        Assert.False(StatusStyles.IsUnrecognizedSprintStatus(""));
        Assert.False(StatusStyles.IsUnrecognizedSprintStatus("in-progress"));
        Assert.False(StatusStyles.IsUnrecognizedSprintStatus("retired"));
        Assert.True(StatusStyles.IsUnrecognizedSprintStatus("blocked"));
    }

    [Fact]
    public void StoryStages_IncludesUnrecognized()
        => Assert.Contains("unrecognized", StatusStyles.StoryStages);

    // ============ Story 8.9: `retired` is a first-class STORY status ============

    [Fact]
    public void StoryStages_IncludesRetired_DirectlyAfterDone()
    {
        // Membership is what makes the defined (epics.md) tally able to name the stage the tracked (yaml)
        // tally already names — Story 8.3's "every count agrees" invariant. Position is narrative: retired is
        // the SECOND terminal stage (owner decision D1), so everything below it is still work owed.
        Assert.Contains("retired", StatusStyles.StoryStages);
        Assert.Equal(1, StatusStyles.StoryStages.ToList().IndexOf("retired"));
        Assert.Equal(0, StatusStyles.StoryStages.ToList().IndexOf("done"));
    }

    [Fact]
    public void EpicStages_IncludesRetired_SoNoRollUpConsumerCanDropIt()
    {
        // Same reason "unrecognized" is already here: ForEpic can now RETURN it, and a consumer that buckets
        // epics by iterating this list would otherwise draw nothing for an all-retired epic.
        Assert.Contains("retired", StatusStyles.EpicStages);
        Assert.Equal("Retired", StatusStyles.EpicLabel("retired"));
    }

    [Fact]
    public void RetiredStatus_IsNotADiagnostic_ButAnUnmappedWordStillIs()
    {
        // AC #2, both halves against the same code path. IsUnrecognizedStatus follows ForStatus, so this
        // needed no edit of its own — which is exactly why it needs a test that says so.
        foreach (var word in StatusStyles.RetirementStatusWords)
            Assert.False(StatusStyles.IsUnrecognizedStatus(word), $"'{word}' must not raise a notice");
        Assert.False(StatusStyles.IsUnrecognizedStatus("Superseded"));
        Assert.False(StatusStyles.IsUnrecognizedStatus("won't fix"));
        Assert.True(StatusStyles.IsUnrecognizedStatus("frobnicated"));
    }

    [Fact]
    public void ForEpic_DoneOrRetiredReadsDone_AllRetiredReadsRetired()
    {
        // Owner decision D1. Before Story 8.9 the gate was All(c => c == "done"), so an epic that retired a
        // single story could NEVER read done no matter how much of it shipped.
        Assert.Equal("done", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"), Story("retired"))));
        Assert.Equal("done", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("retired"), Story("complete"))));
        // All retired = abandoned, not finished. A distinct word, so no surface can claim delivery.
        Assert.Equal("retired", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("retired"), Story("superseded"))));
        // Retired never LIFTS a live epic: outstanding work still decides the tier.
        Assert.Equal("active", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("retired"), Story("in-progress"))));
        Assert.Equal("ready", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("retired"), Story("ready-for-dev"))));
        Assert.Equal("drafted", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("retired"), Story("drafted"))));
    }

    [Fact]
    public void ForEpic_RetiredDoesNotMaskAnAllUnrecognizedEpic()
    {
        // Terminal ledger history says nothing about whether the REST of the epic is merely unmapped. Letting
        // one retired story downgrade this to "drafted" would hide the notice Story 8.2 AC #3 exists to raise.
        Assert.Equal("unrecognized", StatusStyles.ForEpic(
            Epic(EpicStatus.Drafted, Story("retired"), Story("frobnicated"))));
        Assert.Equal("drafted", StatusStyles.ForEpic(
            Epic(EpicStatus.Drafted, Story("retired"), Story("frobnicated"), Story("drafted"))));
    }

    [Fact]
    public void ForEpicWithRetrospective_GatesTheDoneCase_ButNotTheAllRetiredCase()
    {
        // A delivered epic still owes a retro; a fully-abandoned one does not — reading it "In review" would
        // put it back on the list of things someone owes work on. [AC #4]
        var delivered = Epic(EpicStatus.Drafted, Story("done"), Story("retired"));
        Assert.Equal("review", StatusStyles.ForEpicWithRetrospective(delivered));
        delivered.HasRetrospective = true;
        Assert.Equal("done", StatusStyles.ForEpicWithRetrospective(delivered));

        var abandoned = Epic(EpicStatus.Drafted, Story("retired"), Story("cancelled"));
        Assert.Equal("retired", StatusStyles.ForEpicWithRetrospective(abandoned));
        abandoned.HasRetrospective = true;
        Assert.Equal("retired", StatusStyles.ForEpicWithRetrospective(abandoned));
    }

    [Fact]
    public void ForEpic_PinsEpic22Shape_FiveLiveStoriesPlusOneRetired()
    {
        // The concrete case that provoked the story: Epic 22 is 22.1/22.2/22.4/22.5/22.6 plus retired 22.3.
        // While work remains it reads active; once the five close it reaches done instead of being stuck
        // permanently short of it. [AC #4]
        var midFlight = Epic(EpicStatus.Drafted,
            Story("done"), Story("review"), Story("retired"),
            Story("ready-for-dev"), Story("drafted"), Story("drafted"));
        Assert.Equal("active", StatusStyles.ForEpic(midFlight));

        var closed = Epic(EpicStatus.Drafted,
            Story("done"), Story("done"), Story("retired"), Story("done"), Story("done"), Story("done"));
        Assert.Equal("done", StatusStyles.ForEpic(closed));
    }

    [Fact]
    public void RetiredBadge_CarriesColourAndGlyphAndWord_NeverColourAlone()
    {
        // UX-DR17. Trap 6: Icons.ForStatus("retired") and ("deferred") are BYTE-IDENTICAL glyphs by design, so
        // asserting on the icon alone cannot distinguish them — the class and the word are what must be pinned.
        var html = StatusStyles.Badge("retired", StatusStyles.StoryLabel("retired"));
        Assert.Contains("status-badge retired", html);
        Assert.Contains(">Retired</span>", html);
        Assert.Contains(StatusStyles.StageMeaning("retired"), html);
        Assert.Equal(Icons.ForStatus("deferred"), Icons.ForStatus("retired"));
        Assert.DoesNotContain("status-badge deferred", html);
    }

    // ---- Story 9.3: Unmapped requirement tier ----

    private static RequirementInfo Requirement(RequirementStatus status, bool deferred = false) => new()
    {
        Kind = RequirementKind.Functional,
        Number = 1,
        TextHtml = "A requirement",
        Status = status,
        Deferred = deferred,
        CoverageEpicNumbers = System.Array.Empty<int>(),
    };

    [Fact]
    public void ForRequirement_UnmappedSharesPendingColor_ButDeferredKeepsItsOwn()
    {
        // Owner decision #1: Unmapped reuses the tan pending token (no 7th --status-* token); Deferred keeps grey.
        Assert.Equal("pending", StatusStyles.ForRequirement(Requirement(RequirementStatus.Unmapped)));
        Assert.Equal("pending", StatusStyles.ForRequirement(Requirement(RequirementStatus.Planned)));
        Assert.Equal("deferred", StatusStyles.ForRequirement(Requirement(RequirementStatus.Deferred, deferred: true)));
        // Planned and Unmapped intentionally SHARE the class; Deferred is a different class.
        Assert.NotEqual(
            StatusStyles.ForRequirement(Requirement(RequirementStatus.Unmapped)),
            StatusStyles.ForRequirement(Requirement(RequirementStatus.Deferred, deferred: true)));
    }

    [Fact]
    public void RequirementLabel_UnmappedReadsNotYetMapped_DistinctFromPlannedAndDeferred()
    {
        Assert.Equal("Not yet mapped", StatusStyles.RequirementLabel(RequirementStatus.Unmapped));
        Assert.Equal("Planned", StatusStyles.RequirementLabel(RequirementStatus.Planned));
        Assert.Equal("Deferred", StatusStyles.RequirementLabel(RequirementStatus.Deferred));
    }

    [Fact]
    public void RequirementBadge_Unmapped_UsesPendingColorButDistinctUnmappedIconAndWord()
    {
        var badge = StatusStyles.RequirementBadge(Requirement(RequirementStatus.Unmapped));

        // Color class stays pending (tan family)...
        Assert.Contains("class=\"status-badge pending js-tip\"", badge);
        // ...word reads "Not yet mapped"...
        Assert.Contains("Not yet mapped", badge);
        // ...and the icon is the DISTINCT unmapped glyph, not pending's clock — so it never reads color-only.
        Assert.Contains(Icons.ForStatus("unmapped"), badge);
        Assert.NotEqual(Icons.ForStatus("unmapped"), Icons.ForStatus("pending"));

        // A Planned requirement in the same color family still uses pending's own icon (the two differ by glyph).
        var planned = StatusStyles.RequirementBadge(Requirement(RequirementStatus.Planned));
        Assert.Contains(Icons.ForStatus("pending"), planned);
        Assert.DoesNotContain(Icons.ForStatus("unmapped"), planned);
    }

    // ---- Story 8.9 review: Retired requirement tier ----

    [Fact]
    public void ForRequirement_RetiredSharesDeferredColor_ButLabelStaysDistinct()
    {
        // Same no-7th-token pattern as Unmapped/Planned, but Retired shares DEFERRED's grey (not pending's tan):
        // both mean "not progressing", Retired specifically because the covering epic was abandoned.
        Assert.Equal("deferred", StatusStyles.ForRequirement(Requirement(RequirementStatus.Retired)));
        Assert.Equal(
            StatusStyles.ForRequirement(Requirement(RequirementStatus.Deferred, deferred: true)),
            StatusStyles.ForRequirement(Requirement(RequirementStatus.Retired)));
    }

    [Fact]
    public void RequirementLabel_RetiredReadsRetired_NotTheGenericDeferredFallback()
    {
        // The `_ => "Deferred"` fallback arm is exactly Trap 1's hazard (StoryLabel/EpicLabel) applied to
        // requirements: without its own arm, a Retired requirement would silently print "Deferred".
        Assert.Equal("Retired", StatusStyles.RequirementLabel(RequirementStatus.Retired));
        Assert.NotEqual(
            StatusStyles.RequirementLabel(RequirementStatus.Retired),
            StatusStyles.RequirementLabel(RequirementStatus.Deferred));
    }

    [Fact]
    public void RequirementBadge_Retired_SharesDeferredGlyphByDesign_ButWordDiffers()
    {
        // Mirrors the story/epic-level Trap 6 precedent: retired and deferred are byte-identical glyphs on
        // purpose (Icons.ForStatus("retired") == ("deferred")) — class + WORD keep them distinct, never the icon.
        var retired = StatusStyles.RequirementBadge(Requirement(RequirementStatus.Retired));
        var deferred = StatusStyles.RequirementBadge(Requirement(RequirementStatus.Deferred, deferred: true));

        Assert.Contains("class=\"status-badge deferred js-tip\"", retired);
        Assert.Contains("Retired", retired);
        Assert.DoesNotContain("Deferred", retired);
        Assert.Contains(Icons.ForStatus("deferred"), retired);
        Assert.Contains(Icons.ForStatus("deferred"), deferred);
    }

    [Fact]
    public void Icon_UnmappedHasItsOwnGlyph()
        => Assert.False(string.IsNullOrEmpty(StatusStyles.Icon("unmapped")));

    [Fact]
    public void StageMeaning_UnmappedIsDistinctFromDeferredAndPending()
    {
        Assert.NotEqual(StatusStyles.StageMeaning("unmapped"), StatusStyles.StageMeaning("deferred"));
        Assert.NotEqual(StatusStyles.StageMeaning("unmapped"), StatusStyles.StageMeaning("pending"));
    }

    [Fact]
    public void FreeTextBadge_KnownLifecycleWord_RoutesThroughCanonicalBadge()
    {
        var badge = StatusStyles.FreeTextBadge("Accepted");
        // "Accepted" isn't itself a sprint token, but this pins the escape hatch: anything ForSprint recognizes
        // (e.g. "done") must route through the canonical Badge, never the raw slugged-pill fallback.
        var known = StatusStyles.FreeTextBadge("done");
        Assert.Contains("status-badge done", known);
        Assert.DoesNotContain("pill status-", known);
    }

    [Fact]
    public void FreeTextBadge_UnrecognizedWord_DegradesToSluggedPill()
    {
        var badge = StatusStyles.FreeTextBadge("Superseded by ADR 2");
        // First-word CSS class so multi-word ADR states hit .pill.status-superseded (Story 10.4 AC2); full phrase stays visible.
        Assert.Contains("class=\"pill status-superseded\"", badge);
        Assert.Contains("Superseded by ADR 2", badge);
        Assert.DoesNotContain("status-superseded-by", badge);
    }

    [Fact]
    public void FreeTextBadge_TrailingPunctuation_StillRoutesAndSlugsCleanly()
    {
        // Story 10.8 review: a trailing sentence mark on an authored status must not defeat the canonical match
        // ("Done." → green badge, not a grey pill) or leak a dotted CSS class ("Accepted." → status-accepted).
        var known = StatusStyles.FreeTextBadge("Done.");
        Assert.Contains("status-badge done", known);
        Assert.DoesNotContain("pill status-", known);

        var unknown = StatusStyles.FreeTextBadge("Accepted.");
        Assert.Contains("class=\"pill status-accepted\"", unknown);
        Assert.DoesNotContain("status-accepted.", unknown);
    }

    [Theory]
    [InlineData("Accepted", "done")]
    [InlineData("approved", "done")]
    [InlineData("Proposed", "pending")]
    [InlineData("Superseded by ADR 2", "deferred")]
    [InlineData("Deprecated.", "deferred")]
    public void AdrAccentToken_MapsStatusToStageAccent(string status, string expected)
    {
        Assert.Equal(expected, StatusStyles.AdrAccentToken(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Draftedish")]
    public void AdrAccentToken_UnknownOrEmpty_ReturnsNullForNeutralAccent(string? status)
    {
        Assert.Null(StatusStyles.AdrAccentToken(status));
    }
}
