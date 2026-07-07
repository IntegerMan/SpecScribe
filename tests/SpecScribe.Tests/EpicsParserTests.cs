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
