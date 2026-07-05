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
    public void ExtractDevAgentRecord_ReturnsLabelContentPairs()
    {
        var record = EpicsParser.ExtractDevAgentRecord(SampleArtifact);

        var (label, content) = Assert.Single(record);
        Assert.Equal("Agent Model Used", label);
        Assert.Contains("Some model", content);
    }
}
