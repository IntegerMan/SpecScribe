using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pure-parser coverage for <see cref="DeferredWorkParser"/> — structured cards, resolved
/// markers, and NFR8 degrade paths. [Story 9.6]</summary>
public class DeferredWorkParserTests
{
    private static readonly IReadOnlyDictionary<string, string> HrefMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["8-8-generation-time-recency-signals.md"] = "epics/story-8-8.html",
            ["8-8-generation-time-recency-signals"] = "epics/story-8-8.html",
            ["spec-webview-doc-page-surfaces.md"] = "implementation-artifacts/spec-webview-doc-page-surfaces.html",
            ["spec-webview-doc-page-surfaces"] = "implementation-artifacts/spec-webview-doc-page-surfaces.html",
        };

    [Fact]
    public void Parse_TwoDeferredFromGroups_ResolvesSourceStoryHref()
    {
        var md = """
            # Deferred Work

            ## Deferred from: code review of 8-8-generation-time-recency-signals.md (2026-07-15)

            - Open path-map casing mismatch on Windows.

            ## Deferred from: code review of spec-webview-doc-page-surfaces (2026-07-13)

            - source_spec: `spec-webview-doc-page-surfaces.md`
              summary: ADR-landing hrefs aren't percent-encoded.
            """;

        var model = DeferredWorkParser.Parse(md, HrefMap, "../");

        Assert.True(model.IsStructured);
        Assert.Equal(2, model.Groups.Count);
        Assert.Equal("8.8", model.Groups[0].SourceStoryId);
        Assert.Equal("../epics/story-8-8.html", model.Groups[0].SourceStoryHref);
        Assert.Single(model.Groups[0].Items);
        Assert.False(model.Groups[0].Items[0].Resolved);

        Assert.NotNull(model.Groups[1].SourceStoryHref);
        Assert.Contains("spec-webview-doc-page-surfaces.html", model.Groups[1].SourceStoryHref);
        Assert.Single(model.Groups[1].Items);
    }

    [Fact]
    public void Parse_StruckThroughResolvedItem_SetsResolvedAndResolvingRef()
    {
        var md = """
            ## Deferred from: code review of 6-3-vs-code-integration-spike.md

            - **[RESOLVED in 6.4]** ~~Overlapping debounced re-renders race~~ — fixed in Story 6.4.
            """;

        var model = DeferredWorkParser.Parse(md, HrefMap);

        Assert.True(model.IsStructured);
        var item = Assert.Single(model.Groups[0].Items);
        Assert.True(item.Resolved);
        Assert.Equal("6.4", item.ResolvingRef);
        Assert.Equal("epics/story-6-4.html", item.ResolvingHref);
        Assert.Contains("<del>", item.BodyHtml);
    }

    [Fact]
    public void Parse_PlainOpenItem_IsNotResolved()
    {
        var md = """
            ## Deferred from: review of 1-1-foundation.md

            - Still outstanding path casing issue.
            """;

        var model = DeferredWorkParser.Parse(md);
        var item = Assert.Single(model.Groups[0].Items);
        Assert.False(item.Resolved);
        Assert.Null(item.ResolvingRef);
    }

    [Fact]
    public void Parse_NoDeferredFromHeading_IsUnstructured()
    {
        var md = """
            # Deferred Work

            Just a free-form note with a list:

            - Something parked
            - Another thing
            """;

        var model = DeferredWorkParser.Parse(md);
        Assert.False(model.IsStructured);
        Assert.Empty(model.Groups);
        Assert.False(string.IsNullOrEmpty(model.PlainBodyHtml));
    }

    [Fact]
    public void Parse_EmptyAndGarbage_NeverThrows()
    {
        Assert.False(DeferredWorkParser.Parse(null).IsStructured);
        Assert.False(DeferredWorkParser.Parse("").IsStructured);
        Assert.False(DeferredWorkParser.Parse("   ").IsStructured);
        Assert.False(DeferredWorkParser.Parse("### Not a deferred heading\n- x").IsStructured);
    }
}
