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
    public void Parse_StoryHyphenHeading_ResolvesSourceStoryId()
    {
        var md = """
            ## Deferred from: code review of story-3-8 (2026-07-09)

            - Commit body containing a literal 0x1F control char could truncate numstat rows.
            """;

        var model = DeferredWorkParser.Parse(md);

        Assert.True(model.IsStructured);
        var group = Assert.Single(model.Groups);
        Assert.Equal("3.8", group.SourceStoryId);
        Assert.False(Assert.Single(group.Items).Resolved);
    }

    [Fact]
    public void Parse_StoryDottedHeading_ResolvesSourceStoryId()
    {
        var md = """
            ## Deferred from: code review of story-3.5 (2026-07-08)

            - Legend arrays can drift between project and epic sunbursts.
            """;

        var model = DeferredWorkParser.Parse(md);
        Assert.Equal("3.5", Assert.Single(model.Groups).SourceStoryId);
    }

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

    [Fact]
    public void Parse_OpenItemSourceSpec_DoesNotBecomeResolvingRef()
    {
        var md = """
            ## Deferred from: code review of 7-3-activity-timeline-and-date-pages.md

            - source_spec: `7-3-activity-timeline-and-date-pages.md`
              summary: Watch-mode never refreshes the timeline.
            """;

        var model = DeferredWorkParser.Parse(md, HrefMap);
        var item = Assert.Single(model.Groups[0].Items);
        Assert.False(item.Resolved);
        Assert.Null(item.ResolvingRef);
        Assert.Null(item.ResolvingHref);
    }

    [Fact]
    public void Parse_BareResolvedWordWithoutBrackets_IsNotResolved()
    {
        var md = """
            ## Deferred from: review of 1-1-foundation.md

            - Not RESOLVED yet — still outstanding.
            """;

        var model = DeferredWorkParser.Parse(md);
        var item = Assert.Single(model.Groups[0].Items);
        Assert.False(item.Resolved);
    }

    [Fact]
    public void Parse_PreambleBeforeFirstDeferredFrom_IsPreserved()
    {
        var md = """
            # Deferred Work

            Real-but-not-now items surfaced during reviews.

            ## Deferred from: review of 1-1-foundation.md

            - Park this for later.
            """;

        var model = DeferredWorkParser.Parse(md);
        Assert.True(model.IsStructured);
        Assert.False(string.IsNullOrEmpty(model.PreambleHtml));
        Assert.Contains("Real-but-not-now", model.PreambleHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DeferredFromHeadingsWithNoListItems_FallsBackToUnstructured()
    {
        var md = """
            # Deferred Work

            Intro prose that must not vanish.

            ## Deferred from: review of 1-1-foundation.md

            Just a note with no bullets.
            """;

        var model = DeferredWorkParser.Parse(md);
        Assert.False(model.IsStructured);
        Assert.False(string.IsNullOrEmpty(model.PlainBodyHtml));
        Assert.Contains("Intro prose", model.PlainBodyHtml!, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_NonDeferredHeadingInsideSection_DoesNotDropLaterItems()
    {
        var md = """
            ## Deferred from: review of 1-1-foundation.md

            - First item stays.

            ## Notes

            - Second item must still parse.
            """;

        var model = DeferredWorkParser.Parse(md);
        Assert.True(model.IsStructured);
        Assert.Equal(2, model.Groups[0].Items.Count);
    }

    [Fact]
    public void Templater_StructuredPreamble_RendersAboveGroups()
    {
        var model = new DeferredWorkModel(
            true,
            new[]
            {
                new DeferredWorkGroup(
                    "review of 1-1-foundation.md",
                    "1.1",
                    "epics/story-1-1.html",
                    new[] { new DeferredWorkItem("<p>Park this.</p>", false, null, null) }),
            },
            PreambleHtml: "<p>Real-but-not-now items.</p>");

        var nav = SiteNav.Build(Array.Empty<string>(), "Test", hasAdrs: false);
        var html = DeferredWorkTemplater.RenderPage(model, nav, "deferred-work.html");

        Assert.Contains("deferred-work-preamble", html);
        Assert.Contains("Real-but-not-now items.", html);
        var preambleAt = html.IndexOf("deferred-work-preamble", StringComparison.Ordinal);
        var groupAt = html.IndexOf("deferred-group", StringComparison.Ordinal);
        Assert.True(preambleAt >= 0 && groupAt > preambleAt);
    }
}
