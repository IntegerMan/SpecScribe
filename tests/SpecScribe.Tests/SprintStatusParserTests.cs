using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Parser coverage for Story 2.3 Task 1: order-preserving development_status classification
/// (epic/story/retrospective), robust degradation (missing/malformed/empty → null), and the optional
/// action_items list (present → parsed, absent → empty). Uses inline yaml strings — no disk needed. </summary>
public class SprintStatusParserTests
{
    private const string ValidYaml = """
        generated: 2026-07-05T22:16:50-04:00
        last_updated: 2026-07-06T22:00:00-04:00
        project: SpecScribe

        development_status:
          epic-1: in-progress
          1-1-foundation: done
          1-2-traceability: review
          epic-1-retrospective: optional

          epic-2: backlog
          2-1-something: ready-for-dev
          2-6-later: backlog
        """;

    [Fact]
    public void Parse_ClassifiesEpicStoryAndRetrospectiveKeysInFileOrder()
    {
        var sprint = SprintStatusParser.Parse(ValidYaml);

        Assert.NotNull(sprint);
        // File order is preserved exactly — epics interleaved with their stories and retro.
        Assert.Equal(
            new[] { "epic-1", "1-1-foundation", "1-2-traceability", "epic-1-retrospective", "epic-2", "2-1-something", "2-6-later" },
            sprint!.Entries.Select(e => e.RawKey).ToArray());

        var epic1 = sprint.Entries[0];
        Assert.Equal(SprintEntryKind.Epic, epic1.Kind);
        Assert.Equal(1, epic1.EpicNumber);
        Assert.Equal("in-progress", epic1.Status);

        var story11 = sprint.Entries[1];
        Assert.Equal(SprintEntryKind.Story, story11.Kind);
        Assert.Equal(1, story11.EpicNumber);
        Assert.Equal(1, story11.StoryMinor);
        Assert.Equal("done", story11.Status);

        var retro = sprint.Entries[3];
        Assert.Equal(SprintEntryKind.Retrospective, retro.Kind);
        Assert.Equal(1, retro.EpicNumber);
        Assert.Equal("optional", retro.Status);
    }

    [Fact]
    public void Parse_MissingOrEmptyOrMalformedDegradesToNull()
    {
        // No development_status map at all.
        Assert.Null(SprintStatusParser.Parse("project: SpecScribe\nlast_updated: today\n"));
        // Whitespace / empty.
        Assert.Null(SprintStatusParser.Parse(string.Empty));
        Assert.Null(SprintStatusParser.Parse("   \n  "));
        // Malformed yaml (bad indentation / unclosed) — caught, not thrown.
        Assert.Null(SprintStatusParser.Parse("development_status:\n  epic-1: [unclosed"));
    }

    [Fact]
    public void ParseFile_MissingFileReturnsNull()
    {
        Assert.Null(SprintStatusParser.ParseFile(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".yaml")));
        Assert.Null(SprintStatusParser.ParseFile(null));
    }

    [Fact]
    public void Parse_IgnoresKeysMatchingNoPattern()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              not-a-story-or-epic: whatever
              1-1-real: done
            """);

        Assert.NotNull(sprint);
        Assert.Equal(new[] { "epic-1", "1-1-real" }, sprint!.Entries.Select(e => e.RawKey).ToArray());
    }

    [Fact]
    public void Parse_ActionItemsPresentAreParsedOtherwiseEmpty()
    {
        var withItems = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              epic-1-retrospective: done

            action_items:
              - epic: 1
                action: "Add error-handling review to the checklist"
                owner: "Charlie"
                status: open
              - epic: 1
                action: "Already handled"
                status: done
            """);

        Assert.NotNull(withItems);
        Assert.Equal(2, withItems!.ActionItems.Count);
        var first = withItems.ActionItems[0];
        Assert.Equal("Add error-handling review to the checklist", first.Action);
        Assert.Equal("open", first.Status);
        Assert.Equal(1, first.EpicNumber);
        Assert.Equal("Charlie", first.Owner);
        // Open surface hides the settled (done) item.
        Assert.Single(withItems.OpenActionItems);
        Assert.Equal("Add error-handling review to the checklist", withItems.OpenActionItems[0].Action);

        // The live-repo shape: no action_items block at all → empty list, not an error.
        Assert.Empty(SprintStatusParser.Parse(ValidYaml)!.ActionItems);
        Assert.Empty(SprintStatusParser.Parse(ValidYaml)!.OpenActionItems);
    }

    [Fact]
    public void Parse_SurvivesMalformedSiblingKeys()
    {
        // Real BMad-generated files carry `story_location: {project-root}/…`, whose unquoted `{` is invalid
        // YAML. A whole-document parse would throw on it; development_status must still be read. [Story 2.3]
        var sprint = SprintStatusParser.Parse("""
            generated: 2026-07-05T22:16:50-04:00
            last_updated: 2026-07-06T22:00:00-04:00
            project: SpecScribe
            story_location: {project-root}/_bmad-output/implementation-artifacts

            development_status:
              epic-1: in-progress
              1-1-foundation: done
            """);

        Assert.NotNull(sprint);
        Assert.Equal(new[] { "epic-1", "1-1-foundation" }, sprint!.Entries.Select(e => e.RawKey).ToArray());
        Assert.Equal("2026-07-06T22:00:00-04:00", sprint.LastUpdated);
    }

    [Fact]
    public void Parse_ReadsOptionalLastUpdatedScalar()
    {
        Assert.Equal("2026-07-06T22:00:00-04:00", SprintStatusParser.Parse(ValidYaml)!.LastUpdated);
        // Absent last_updated → null (never an error).
        Assert.Null(SprintStatusParser.Parse("development_status:\n  epic-1: done\n")!.LastUpdated);
    }

    [Theory]
    [InlineData("last_updated: >\ndevelopment_status:\n  epic-1: done\n")]
    [InlineData("last_updated: |\ndevelopment_status:\n  epic-1: done\n")]
    [InlineData("last_updated: >-\ndevelopment_status:\n  epic-1: done\n")]
    [InlineData("last_updated: |+\ndevelopment_status:\n  epic-1: done\n")]
    [InlineData("last_updated: |2-\ndevelopment_status:\n  epic-1: done\n")]
    [InlineData("last_updated: >1+\ndevelopment_status:\n  epic-1: done\n")]
    [InlineData("last_updated: > # folded date\ndevelopment_status:\n  epic-1: done\n")]
    public void Parse_BlockScalarLastUpdatedDegradesToNullRatherThanTheBareIndicator(string yaml)
    {
        // A `>`/`|` block-scalar form (with an optional `+`/`-` chomp indicator) has its actual body on the
        // FOLLOWING indented lines — this single-line regex never reads those, so the value must degrade to
        // null rather than surfacing the bare indicator character as if it were the date.
        // [spec-epic2-deferred-debt-cleanup]
        var sprint = SprintStatusParser.Parse(yaml);

        Assert.NotNull(sprint);
        Assert.Null(sprint!.LastUpdated);
    }

    [Fact]
    public void Parse_DuplicateTopLevelDevelopmentStatusKeyFailsClosedRatherThanTruncating()
    {
        // A malformed file with two `development_status:` blocks must not silently truncate at the second
        // header and drop every entry after it — the whole block extraction fails closed (null), matching
        // the existing "no usable development_status map" degrade path. [spec-epic2-deferred-debt-cleanup]
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              1-1-foundation: done

            development_status:
              epic-2: backlog
            """);

        Assert.Null(sprint);
    }

    [Fact]
    public void Parse_DuplicateTopLevelActionItemsKeyFailsClosedToEmptyActionItems()
    {
        // Same fail-closed contract for the other block-sliced key: action_items degrades to empty rather
        // than the truncated first-occurrence content. [spec-epic2-deferred-debt-cleanup]
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done

            action_items:
              - action: "First"
                status: open

            action_items:
              - action: "Second"
                status: open
            """);

        Assert.NotNull(sprint);
        Assert.Empty(sprint!.ActionItems);
    }
}
