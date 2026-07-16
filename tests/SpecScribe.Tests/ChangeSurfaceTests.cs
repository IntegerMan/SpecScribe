using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>ADR 0007 change-surface projection — File List extraction, path classification, Build.</summary>
public class ChangeSurfaceTests
{
    [Fact]
    public void ExtractFileList_ParsesBulletPaths()
    {
        var raw = """
            # Story 1.1
            ## Dev Agent Record
            ### File List
            - src/SpecScribe/EpicsParser.cs
            - `src/SpecScribe/assets/specscribe.css`
            - tests/SpecScribe.Tests/EpicsParserTests.cs
            """;
        var files = ChangeSurface.ExtractFileList(raw);
        Assert.Equal(3, files.Count);
        Assert.Contains("src/SpecScribe/EpicsParser.cs", files);
        Assert.Contains("src/SpecScribe/assets/specscribe.css", files);
        Assert.Contains("tests/SpecScribe.Tests/EpicsParserTests.cs", files);
    }

    [Fact]
    public void ExtractFileList_ReturnsEmptyWhenAbsent()
        => Assert.Empty(ChangeSurface.ExtractFileList("# Story 1.1\nNo dev record.\n"));

    [Fact]
    public void ExtractFileList_ReturnsEmptyOnNull()
        => Assert.Empty(ChangeSurface.ExtractFileList(null));

    [Fact]
    public void ClassifyPaths_VisualAndRenderedUi()
    {
        var paths = new[]
        {
            "src/SpecScribe/HtmlRenderAdapter.Epics.cs",
            "src/SpecScribe/assets/specscribe.css",
            "tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs",
        };
        var labels = ChangeSurface.ClassifyPaths(paths);
        Assert.Contains("visual", labels);
        Assert.Contains("rendered UI", labels);
        Assert.DoesNotContain("plumbing (no new visible surface)", labels);
    }

    [Fact]
    public void ClassifyPaths_PlumbingOnlyWhenTestsAndLogic()
    {
        var paths = new[]
        {
            "src/SpecScribe/EpicsParser.cs",
            "tests/SpecScribe.Tests/EpicsParserTests.cs",
        };
        var labels = ChangeSurface.ClassifyPaths(paths);
        Assert.Single(labels);
        Assert.Equal("plumbing (no new visible surface)", labels[0]);
    }

    [Fact]
    public void Build_ProducesVerifyChecklistAndShipLine()
    {
        var raw = """
            # Story 1.1
            Status: review
            ## Acceptance Criteria
            1. **Given** a page **When** it renders **Then** a strip appears
            ## Dev Agent Record
            ### File List
            - src/SpecScribe/EpicsParser.cs
            ## Change Log
            - 2026-07-16 — **Implemented (dev-story).** Status → review.
            """;
        var acs = EpicsParser.ExtractAcceptanceCriteria(raw);
        var surface = ChangeSurface.Build(raw, "review", acs);

        Assert.Single(surface.VerifyChecklist);
        Assert.Equal(1, surface.VerifyChecklist[0].Number);
        Assert.Contains("strip appears", surface.VerifyChecklist[0].PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Single(surface.ChangedFiles);
        Assert.Equal("src/SpecScribe/EpicsParser.cs", surface.ChangedFiles[0].Path);
        Assert.NotNull(surface.ShipLine);
        Assert.Contains("review", surface.ShipLine!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-07-16", surface.ShipLine!);
    }

    [Fact]
    public void ExtractFileList_StripsNewAnnotationFromPath()
    {
        var raw = """
            ## Dev Agent Record
            ### File List
            - src/SpecScribe/FollowUpRefs.cs (new)
            """;
        var files = ChangeSurface.ExtractFileList(raw);
        Assert.Single(files);
        Assert.Equal("src/SpecScribe/FollowUpRefs.cs", files[0]);
        var entries = ChangeSurface.ExtractFileListEntries(raw);
        Assert.Equal("FollowUpRefs.cs (new)", Path.GetFileName(entries[0].DisplayLabel));
    }

    [Fact]
    public void NormalizeFileListPath_StripsParentheticalAnnotation()
        => Assert.Equal("src/Foo.cs", ChangeSurface.NormalizeFileListPath("src/Foo.cs (new)"));
}
