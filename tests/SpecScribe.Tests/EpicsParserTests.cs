using SpecScribe;

namespace SpecScribe.Tests;

public class EpicsParserTests
{
    private const string SampleEpicsMd = """
        ---
        title: Epics
        ---
        # Epics

        ## Overview

        The plan of record.

        ## Requirements Inventory

        ### Functional Requirements

        **Core Loop**
        FR1: The game runs a day cycle
        FR2: Patients arrive

        ### NonFunctional Requirements

        NFR1: Loads in under two seconds

        ### FR Coverage Map

        FR1: Epic 1 - core loop foundation
        FR2: Deferred - post vertical slice
        NFR1: Epic 1 - startup path

        ## Epic List

        ### Epic 1: Foundation

        Get the project standing up.

        **FRs covered:** FR1

        *Vertical slice complete — everything below is further development.*

        ### Epic 2: Expansion

        Grow the systems.

        **FRs covered:** FR2

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold the project

        As a developer, I want a skeleton, so that work can begin.

        **Acceptance Criteria:**

        **Given** nothing
        **When** the project builds
        **Then** it succeeds
        """;

    [Fact]
    public void Parse_SplitsDraftedAndPendingEpics()
    {
        var model = EpicsParser.Parse(SampleEpicsMd);

        Assert.Equal(2, model.Epics.Count);
        Assert.Equal(EpicStatus.Drafted, model.Epics[0].Status); // has a full H2 section
        Assert.Equal(EpicStatus.Pending, model.Epics[1].Status); // list-only
    }

    [Fact]
    public void Parse_AssignsSectionsAroundVerticalSliceDivider()
    {
        var model = EpicsParser.Parse(SampleEpicsMd);

        Assert.Equal(EpicSection.VerticalSlice, model.Epics[0].Section);
        Assert.Equal(EpicSection.FurtherDevelopment, model.Epics[1].Section);
    }

    [Fact]
    public void Parse_ExtractsStoriesFromEpicSections()
    {
        var model = EpicsParser.Parse(SampleEpicsMd);

        var story = Assert.Single(model.Epics[0].Stories);
        Assert.Equal("1.1", story.Id);
        Assert.Contains("Scaffold the project", story.Title);
    }

    [Fact]
    public void Parse_RendersOverviewHtml()
        => Assert.Contains("plan of record", EpicsParser.Parse(SampleEpicsMd).OverviewHtml);

    [Fact]
    public void Parse_AcBlockWithoutANumberLineGetsNoLabel()
    {
        var story = EpicsParser.Parse(SampleEpicsMd).Epics[0].Stories[0];

        var block = Assert.Single(story.AcBlocksHtml);
        Assert.DoesNotContain("ac-num", block);
        Assert.Contains("class=\"gherkin-line\"", block);
    }

    private const string CommentedStoryEpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Goal.

        ## Epic 1: Foundation

        ### Story 1.1: Commented story

        <!-- Seats R3.1 (tree view) via the new outline export, keeping icons on the
             semantic --status-* stages rather than host severities. Split, don't absorb. -->

        As a VS Code user,
        I want an outline in the sidebar,
        So that I can glance at status.

        **Acceptance Criteria:**

        **Given** nothing
        **When** it builds
        **Then** it succeeds
        """;

    [Fact]
    public void Parse_LeadingCommentRendersAsOwnBlock_NotFoldedIntoUserStory()
    {
        var story = EpicsParser.Parse(CommentedStoryEpicsMd).Epics[0].Stories[0];

        // The comment becomes its own marker-free .md-comment block (the block-comment renderer permits the
        // "--" in "--status-*" that would break an inline HTML comment and leak the literal markers).
        Assert.Contains("class=\"md-comment\"", story.UserStoryNoteHtml);
        Assert.Contains("Seats R3.1", story.UserStoryNoteHtml);
        Assert.DoesNotContain("<!--", story.UserStoryNoteHtml);
        Assert.DoesNotContain("-->", story.UserStoryNoteHtml);
        Assert.DoesNotContain("&lt;!--", story.UserStoryNoteHtml);

        // The narrative blurb carries only the story, with no comment content or leaked markers.
        Assert.Contains("As a VS Code user", story.UserStoryHtml);
        Assert.DoesNotContain("Seats R3.1", story.UserStoryHtml);
        Assert.DoesNotContain("<!--", story.UserStoryHtml);
        Assert.DoesNotContain("&lt;!--", story.UserStoryHtml);
    }

    [Fact]
    public void Parse_StoryWithoutComment_HasEmptyNote()
    {
        var story = EpicsParser.Parse(SampleEpicsMd).Epics[0].Stories[0];
        Assert.Equal(string.Empty, story.UserStoryNoteHtml);
    }

    /// <summary>Wraps a raw user-story-region body into a minimal epics.md and returns the parsed first story,
    /// so the leading-comment edge cases can be exercised without repeating the epics scaffold each time.</summary>
    private static StoryInfo ParseStoryWithBody(string body) => EpicsParser.Parse($"""
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Goal.

        ## Epic 1: Foundation

        ### Story 1.1: Edge story

        {body}

        **Acceptance Criteria:**

        **Given** nothing
        **When** it builds
        **Then** it succeeds
        """).Epics[0].Stories[0];

    [Fact]
    public void Parse_UnterminatedComment_DoesNotSwallowNarrative()
    {
        // A "<!--" with no closing "-->" is malformed and never occurs in real epics.md, but it must not eat
        // the story: nothing is lifted into a note block, and the narrative text still renders (no data loss)
        // rather than being blanked. Formatting degrades — the stray "<!--" has no clean boundary — but the
        // content survives, which is the honest contract for malformed input.
        var story = ParseStoryWithBody("<!-- dangling note with no close\n\nAs a user, I want the thing.");

        Assert.Equal(string.Empty, story.UserStoryNoteHtml);
        Assert.Contains("the thing", story.UserStoryHtml);
    }

    [Fact]
    public void Parse_NarrativeAfterCloseMarker_StaysInNarrative()
    {
        // The closing "-->" sharing its line with narrative text must keep that text in the blurb, not drop it.
        var story = ParseStoryWithBody("<!-- brief note --> As a user, I want the thing.");

        Assert.Contains("class=\"md-comment\"", story.UserStoryNoteHtml);
        Assert.Contains("brief note", story.UserStoryNoteHtml);
        Assert.Contains("As a user", story.UserStoryHtml);
        Assert.DoesNotContain("brief note", story.UserStoryHtml);
    }

    [Fact]
    public void Parse_EmptyComment_YieldsNoNoteBlock()
    {
        var story = ParseStoryWithBody("<!-- -->\n\nAs a user, I want the thing.");

        Assert.Equal(string.Empty, story.UserStoryNoteHtml);
        Assert.Contains("As a user", story.UserStoryHtml);
    }

    private const string NumberedAcEpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Goal.

        ## Epic 1: Foundation

        ### Story 1.1: Numbered criteria

        As a developer, I want numbered ACs.

        **Acceptance Criteria:**

        1.
        **Given** nothing
        **When** it builds

        2.
        **Given** something
        **Then** it links
        """;

    [Fact]
    public void Parse_BareNumberLinesBecomeAcLabels_NotEmptyLists()
    {
        var story = EpicsParser.Parse(NumberedAcEpicsMd).Epics[0].Stories[0];

        Assert.Equal(2, story.AcBlocksHtml.Count);
        Assert.Contains("<span class=\"ac-num\">AC #1</span>", story.AcBlocksHtml[0]);
        Assert.Contains("<span class=\"ac-num\">AC #2</span>", story.AcBlocksHtml[1]);
        // The bare "1."/"2." lines are consumed into labels — never rendered as markdown, where they
        // become stray empty <ol> fragments.
        Assert.All(story.AcBlocksHtml, b => Assert.DoesNotContain("<ol", b));
        Assert.Contains("class=\"ac-block-body\"", story.AcBlocksHtml[0]);
        Assert.Contains("gherkin-kw kw-given", story.AcBlocksHtml[0]);
    }

    [Fact]
    public void Parse_EmptyInputYieldsEmptyModel()
        => Assert.Empty(EpicsParser.Parse(string.Empty).Epics);

    [Fact]
    public void ExtractStatus_ReadsPlainStatusLine()
        => Assert.Equal("ready-for-dev", EpicsParser.ExtractStatus("# Story 1.1\nStatus: ready-for-dev\n"));

    [Fact]
    public void ExtractStatus_ReturnsNullWhenAbsent()
        => Assert.Null(EpicsParser.ExtractStatus("# Story 1.1\nNo status here.\n"));

    // ---- Story 8.8 change-log recency dates ---------------------------------------------------------------

    [Fact]
    public void ExtractLatestChangeLogDate_PicksMaxAcrossListForm()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            - 2026-07-06: First entry
            - 2026-07-14: Later entry
            - 2026-07-08: Middle entry
            """;
        Assert.Equal(new DateOnly(2026, 7, 14), EpicsParser.ExtractLatestChangeLogDate(raw));
    }

    [Fact]
    public void ExtractLatestChangeLogDate_ParsesTableForm()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            | Date       | Version | Description |
            | ---------- | ------- | ----------- |
            | 2026-07-08 | 0.1.0   | Drafted     |
            | 2026-07-14 | 1.0     | Implemented |
            """;
        Assert.Equal(new DateOnly(2026, 7, 14), EpicsParser.ExtractLatestChangeLogDate(raw));
    }

    [Fact]
    public void ExtractLatestChangeLogDate_ReturnsNullWhenSectionAbsent()
        => Assert.Null(EpicsParser.ExtractLatestChangeLogDate("# Story 1.1\nNo change log here.\n"));

    [Fact]
    public void ExtractLatestChangeLogDate_ReturnsNullWhenSectionHasNoIsoDate()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            | Date | Description |
            | ---- | ----------- |
            | TBD  | Nothing yet |
            """;
        Assert.Null(EpicsParser.ExtractLatestChangeLogDate(raw));
    }

    [Fact]
    public void ExtractLatestChangeLogDate_SkipsMalformedRowsWithoutThrowing()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            - not-a-date: garbage
            - 2026-13-99: impossible
            - 2026-07-09: real
            """;
        Assert.Equal(new DateOnly(2026, 7, 9), EpicsParser.ExtractLatestChangeLogDate(raw));
    }

    [Fact]
    public void ExtractLatestChangeLogDate_AcceptsH3Heading()
    {
        var raw = """
            # Story 1.1
            ### Change Log
            - 2026-07-08: Drafted under H3
            - 2026-07-12: Reviewed
            """;
        Assert.Equal(new DateOnly(2026, 7, 12), EpicsParser.ExtractLatestChangeLogDate(raw));
    }

    [Fact]
    public void ExtractLatestChangeLogDate_ReturnsNullOnNullRaw()
        => Assert.Null(EpicsParser.ExtractLatestChangeLogDate(null));

    [Fact]
    public void ExtractLatestChangeLogDate_IgnoresProseLineStartingWithIsoDate()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            2026-12-31 was mentioned in prose but is not a row
            - 2026-07-09: Real list entry
            """;
        Assert.Equal(new DateOnly(2026, 7, 9), EpicsParser.ExtractLatestChangeLogDate(raw));
    }

    // ---- Story 9.4 test evidence + change-log verification ------------------------------------------------

    [Theory]
    [InlineData("586 tests green", "586 passing tests")]
    [InlineData("759 C# tests green", "759 passing tests")]
    [InlineData("429 tests pass", "429 passing tests")]
    [InlineData("440 tests passing", "440 passing tests")]
    public void ExtractTestEvidence_NormalizesKnownShapes(string phrase, string expected)
    {
        var raw = $"""
            # Story 1.1
            ## Dev Agent Record
            ### Completion Notes List
            Suite {phrase} after the change.
            """;
        Assert.Equal(expected, EpicsParser.ExtractTestEvidence(raw));
    }

    [Fact]
    public void ExtractTestEvidence_PrefersDevAgentRecordOverLaterBodyMatch()
    {
        var raw = """
            # Story 1.1
            ## Dev Agent Record
            ### Completion Notes List
            12 tests green in the final tally.
            ## Change Log
            - 2026-07-11 — **Implemented.** Mentions 999 tests green elsewhere.
            """;
        Assert.Equal("12 passing tests", EpicsParser.ExtractTestEvidence(raw));
    }

    [Fact]
    public void ExtractTestEvidence_FallsBackToChangeLogWhenDevRecordHasNone()
    {
        var raw = """
            # Story 1.1
            ## Dev Agent Record
            ### Completion Notes List
            No tallies here.
            ## Change Log
            - 2026-07-11 — **Shipped with 88 tests passing.**
            """;
        Assert.Equal("88 passing tests", EpicsParser.ExtractTestEvidence(raw));
    }

    [Fact]
    public void ExtractTestEvidence_DoesNotReadExamplesFromDevNotes()
    {
        var raw = """
            # Story 1.1
            ## Acceptance Criteria
            Then an example like 586 tests green appears in prose.

            ## Dev Notes
            This story explains that other stories might say 759 tests green.

            ## Dev Agent Record
            ### Completion Notes List
            No final test tally was recorded.
            """;
        Assert.Null(EpicsParser.ExtractTestEvidence(raw));
    }

    [Fact]
    public void ExtractTestEvidence_RecognizesH3DevAgentRecord()
    {
        var raw = """
            # Story 1.1
            ### Dev Agent Record
            ### Completion Notes List
            42 tests green.
            """;
        Assert.Equal("42 passing tests", EpicsParser.ExtractTestEvidence(raw));
    }

    [Fact]
    public void ExtractTestEvidence_ReturnsNullWhenAbsent()
        => Assert.Null(EpicsParser.ExtractTestEvidence("# Story 1.1\nNo tests mentioned.\n"));

    [Fact]
    public void ExtractTestEvidence_ReturnsNullOnNullRaw()
        => Assert.Null(EpicsParser.ExtractTestEvidence(null));

    [Fact]
    public void ExtractChangeLogVerification_TakesTopEntryAndFlagsReview()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            - 2026-07-11 — **Code review passed; Status → done.**
            - 2026-07-10 — **Implemented the strip.**
            """;
        var result = EpicsParser.ExtractChangeLogVerification(raw);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 7, 11), result!.Value.Date);
        Assert.True(result.Value.IsVerification);
        Assert.Contains("Code review passed", result.Value.Action);
    }

    [Fact]
    public void ExtractChangeLogVerification_PlainEditIsNotVerification()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            - 2026-07-09 — **Tweaked the CSS spacing.**
            """;
        var result = EpicsParser.ExtractChangeLogVerification(raw);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 7, 9), result!.Value.Date);
        Assert.False(result.Value.IsVerification);
    }

    [Theory]
    [InlineData("Verified generated output.", true)]
    [InlineData("Reviewed and passed.", true)]
    [InlineData("Needs another review pass before closing.", false)]
    public void ExtractChangeLogVerification_ClassifiesVerificationLanguagePrecisely(string action, bool expected)
    {
        var raw = $"""
            # Story 1.1
            ## Change Log
            - 2026-07-09 — **{action}**
            """;
        var result = EpicsParser.ExtractChangeLogVerification(raw);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value.IsVerification);
    }

    [Theory]
    [InlineData("- 2026-07-09: Code review passed.")]
    [InlineData("- 2026-07-09 — Code review passed.")]
    [InlineData("| 2026-07-09 | Code review passed. |")]
    public void ExtractChangeLogVerification_AcceptsCommonDatedRows(string row)
    {
        var raw = $"""
            # Story 1.1
            ## Change Log
            {row}
            """;
        var result = EpicsParser.ExtractChangeLogVerification(raw);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 7, 9), result!.Value.Date);
        Assert.True(result.Value.IsVerification);
    }

    [Fact]
    public void ExtractChangeLogVerification_ReturnsNullWhenSectionAbsent()
        => Assert.Null(EpicsParser.ExtractChangeLogVerification("# Story 1.1\nNo change log.\n"));

    [Fact]
    public void ExtractChangeLogVerification_SkipsMalformedDate()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            - 2026-13-99 — **Code review passed.**
            - 2026-07-09 — **Verified generated output.**
            """;
        var result = EpicsParser.ExtractChangeLogVerification(raw);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 7, 9), result!.Value.Date);
    }

    [Fact]
    public void ExtractChangeLogVerification_ReturnsNullWhenNoMatchingShape()
    {
        var raw = """
            # Story 1.1
            ## Change Log
            - No dated entry here
            """;
        Assert.Null(EpicsParser.ExtractChangeLogVerification(raw));
    }

    private const string SampleArtifact = """
        # Story 1.1: Scaffold
        Status: in progress

        ## Story

        As a developer, I want scaffolding.

        ## Acceptance Criteria

        1. The build passes
        2. Tests exist

        ## Tasks / Subtasks

        - [x] Do the thing (AC: #1)

        ## Dev Agent Record

        ### Agent Model Used

        Some model
        """;

    [Fact]
    public void SplitStoryArtifact_SeparatesBlurbFromRemainder()
    {
        var (blurb, remainder) = EpicsParser.SplitStoryArtifact(SampleArtifact);

        Assert.Contains("I want scaffolding", blurb);
        Assert.Contains("Do the thing", remainder);
        Assert.DoesNotContain("Dev Agent Record", remainder);       // excised: rendered as its own table
        Assert.DoesNotContain("The build passes", remainder);       // excised: ACs render as their own panel
        Assert.DoesNotContain("Status: in progress", remainder);    // leading status line never leaks
    }

    // A story whose remainder cites source files via markdown-link "[Source: ...]" citations — the Story 7.2
    // shape. Regression guard for the bug where the decorative-bracket stripper's non-greedy "(.*?)" matched
    // the FIRST inner label's "]", corrupting "[Source: [A.cs:1](../a.cs), ...]" into visible "[A.cs:1(../a.cs)".
    private const string CitationArtifact = """
        # Story 7.9: Linked citations
        Status: in progress

        ## Story

        As a developer, I want source citations to render as links.

        ## Dev Notes

        - Extend the fetch. [Source: [A.cs:1](../a.cs), [B.cs:2-3](../b.cs)]
        - Guard the gate. [Source: [C.cs:5](../c.cs)]
        - Doc note. [Source: _bmad-output/x.md — Overview]
        """;

    [Fact]
    public void SplitStoryArtifact_PreservesMarkdownLinkCitationsWhenStrippingSourceWrapper()
    {
        var (_, remainder) = EpicsParser.SplitStoryArtifact(CitationArtifact);

        // Multi-link citation: BOTH inner links survive as real anchors (this is the reported bug).
        Assert.Contains("<a href=\"../a.cs\">A.cs:1</a>", remainder);
        Assert.Contains("<a href=\"../b.cs\">B.cs:2-3</a>", remainder);
        // Single-link citation: the link survives too.
        Assert.Contains("<a href=\"../c.cs\">C.cs:5</a>", remainder);

        // The decorative "[Source: ... ]" wrapper is stripped, never left as literal text.
        Assert.DoesNotContain("[Source:", remainder);
        // Bug signature: a link whose "]" was eaten leaves "(../" hanging as visible text. Must never appear.
        Assert.DoesNotContain("A.cs:1(", remainder);
        Assert.DoesNotContain("C.cs:5(", remainder);

        // Plain-path citation still strips to bare text — no regression for the original single-shape case.
        Assert.Contains("_bmad-output/x.md", remainder);
    }

    [Fact]
    public void ExtractAcceptanceCriteria_NumbersEachCriterion()
    {
        var acs = EpicsParser.ExtractAcceptanceCriteria(SampleArtifact);

        Assert.Equal(2, acs.Count);
        Assert.Equal(1, acs[0].Number);
        Assert.Contains("The build passes", acs[0].Html);
        Assert.Contains("Tests exist", acs[1].PlainText);
    }

    [Fact]
    public void LinkifyAcReferences_LinksKnownNumbersOnly()
    {
        var criteria = new Dictionary<int, string> { [1] = "The build passes" };
        var html = EpicsParser.LinkifyAcReferences("<li>Do the thing (AC: #1, #9)</li>", criteria);

        Assert.Contains("<a class=\"ac-ref\" href=\"#ac-1\" title=\"The build passes\">#1</a>", html);
        Assert.Contains("#9", html);
        Assert.DoesNotContain("href=\"#ac-9\"", html);
    }

    [Fact]
    public void LinkifyAcReferences_LinksEveryKnownNumberInACommaGroup()
    {
        var criteria = new Dictionary<int, string> { [1] = "First", [2] = "Second", [3] = "Third" };
        var html = EpicsParser.LinkifyAcReferences("<li>Task (AC: #1, #2, #3)</li>", criteria);

        Assert.Contains("href=\"#ac-1\" title=\"First\">#1</a>", html);
        Assert.Contains("href=\"#ac-2\" title=\"Second\">#2</a>", html);
        Assert.Contains("href=\"#ac-3\" title=\"Third\">#3</a>", html);
    }

    [Fact]
    public void LinkifyAcReferences_HandlesSpacelessAndColonlessForms()
    {
        var criteria = new Dictionary<int, string> { [4] = "Fourth" };
        // "AC #4" (no colon) and "AC:#4" (no space) are both real authored forms in the artifacts.
        Assert.Contains("href=\"#ac-4\"", EpicsParser.LinkifyAcReferences("<li>x (AC #4)</li>", criteria));
        Assert.Contains("href=\"#ac-4\"", EpicsParser.LinkifyAcReferences("<li>x (AC:#4)</li>", criteria));
    }

    [Fact]
    public void LinkifyAcReferences_UsesFullCriterionTextAsTooltip()
    {
        var criteria = new Dictionary<int, string> { [1] = "Given X, when Y, then Z & W" };
        var html = EpicsParser.LinkifyAcReferences("<li>See (AC: #1)</li>", criteria);

        // The tooltip is the criterion's plain text, HTML-escaped so an ampersand is safe in the attribute.
        Assert.Contains("title=\"Given X, when Y, then Z &amp; W\"", html);
    }

    [Fact]
    public void LinkifyAcReferences_LeavesUnresolvedNumberAsPlainTextNeverAnchored()
    {
        var criteria = new Dictionary<int, string> { [1] = "Only one" };
        var html = EpicsParser.LinkifyAcReferences("<li>Task (AC: #7)</li>", criteria);

        Assert.Contains("#7", html);
        Assert.DoesNotContain("href=\"#ac-7\"", html);
        Assert.DoesNotContain(">#7</a>", html);
    }

    [Fact]
    public void LinkifyAcReferences_IsIdempotentAndDoesNotDoubleLinkify()
    {
        var criteria = new Dictionary<int, string> { [1] = "The build passes" };
        var once = EpicsParser.LinkifyAcReferences("<li>Do the thing (AC: #1)</li>", criteria);
        var twice = EpicsParser.LinkifyAcReferences(once, criteria);

        // An already-rendered "#ac-1" reference link must not be re-wrapped into a nested anchor.
        Assert.Equal(once, twice);
        Assert.DoesNotContain("<a class=\"ac-ref\" href=\"#ac-1\" title=\"The build passes\"><a", twice);
    }

    [Fact]
    public void LinkifyAcReferences_NoOpWhenNoCriteria()
    {
        var html = "<li>Do the thing (AC: #1)</li>";
        Assert.Equal(html, EpicsParser.LinkifyAcReferences(html, new Dictionary<int, string>()));
    }

    [Fact]
    public void ExtractDevAgentRecord_ReturnsLabelContentPairs()
    {
        var record = EpicsParser.ExtractDevAgentRecord(SampleArtifact);

        var (label, content) = Assert.Single(record);
        Assert.Equal("Agent Model Used", label);
        Assert.Contains("Some model", content);
    }
}
