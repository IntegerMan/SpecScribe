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
    public void ForEpic_PendingOrStorylessEpicsArePending()
    {
        Assert.Equal("pending", StatusStyles.ForEpic(Epic(EpicStatus.Pending, Story("done"))));
        Assert.Equal("pending", StatusStyles.ForEpic(Epic(EpicStatus.Drafted)));
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
    public void StoryLabel_MapsEachStage(string cssClass, string expected)
        => Assert.Equal(expected, StatusStyles.StoryLabel(cssClass));

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
    public void ForStatus_MapsRawStatusText(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForStatus(status));

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

    [Fact]
    public void Icon_UnmappedHasItsOwnGlyph()
        => Assert.False(string.IsNullOrEmpty(StatusStyles.Icon("unmapped")));

    [Fact]
    public void StageMeaning_UnmappedIsDistinctFromDeferredAndPending()
    {
        Assert.NotEqual(StatusStyles.StageMeaning("unmapped"), StatusStyles.StageMeaning("deferred"));
        Assert.NotEqual(StatusStyles.StageMeaning("unmapped"), StatusStyles.StageMeaning("pending"));
    }
}
