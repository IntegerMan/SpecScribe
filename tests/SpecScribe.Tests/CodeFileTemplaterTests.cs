using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the in-portal code file page (Story 7.1): the a11y shell, the locked <c>id="L{n}"</c>
/// line-anchor convention, 1:1 numbering (blank lines included), HTML escaping, and the placeholder page.</summary>
public class CodeFileTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private const string OutputPath = "code/src/SpecScribe/Sample.cs.html";
    private const string RepoRelative = "src/SpecScribe/Sample.cs";

    [Fact]
    public void RenderPage_RendersTitleBreadcrumbAndA11yShell()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav());

        Assert.Contains($"<title>{RepoRelative} — SpecScribe</title>", html);
        Assert.Contains($"<h1>{RepoRelative}</h1>", html);
        Assert.Contains("<div class=\"story-kicker\">Source File</div>", html);
        // Site a11y contract: skip-link first, single main landmark.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        // Breadcrumb: Home / <file path>. The nested page's Home link carries the correct ../ depth prefix.
        Assert.Contains("Home", html);
        var skipIndex = html.IndexOf("skip-link", StringComparison.Ordinal);
        var mainIndex = html.IndexOf("id=\"main-content\"", StringComparison.Ordinal);
        Assert.True(skipIndex >= 0 && skipIndex < mainIndex, "skip-link must precede the main landmark");
        // Exactly one main landmark.
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderPage_EmitsOneAnchoredLinePerSourceLineNumberedFromOne()
    {
        var lines = new[] { "line one", "line two", "line three" };

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, lines, Nav());

        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("id=\"L3\"", html);
        Assert.DoesNotContain("id=\"L4\"", html);
        // Count matches the input line count exactly (1:1).
        Assert.Equal(lines.Length, CountOccurrences(html, "class=\"code-line\""));
        // Each line carries its 1-based number in data-ln (a CSS gutter counter, not tokenized text) and the source
        // text sits directly in the anchored span so Prism's tokenizer sees pure source.
        Assert.Contains("<span class=\"code-line\" id=\"L1\" data-ln=\"1\">line one</span>", html);
        // A .cs file routes to the csharp grammar so Prism highlights it.
        Assert.Contains("<code class=\"language-csharp\">", html);
        // Prism's stylesheet + highlighter are loaded on a rendered code page.
        Assert.Contains("prism.css", html);
        Assert.Contains("prism.js", html);
        // Line-count meta pill.
        Assert.Contains("<span class=\"pill\">3 lines</span>", html);
    }

    [Fact]
    public void RenderPage_BlankLineStillEmitsAnchoredRowSoNumberingStays1To1()
    {
        var lines = new[] { "before", "", "after" };

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, lines, Nav());

        // Three lines, three anchors — the blank middle line is not collapsed away.
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("id=\"L3\"", html);
        Assert.Equal(3, CountOccurrences(html, "class=\"code-line\""));
        // The blank line renders an empty (but present) anchored span.
        Assert.Contains("<span class=\"code-line\" id=\"L2\" data-ln=\"2\"></span>", html);
    }

    [Fact]
    public void RenderPage_EscapesHtmlMetacharactersInSource()
    {
        var lines = new[] { "if (a < b && c > d) return \"x\";" };

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, lines, Nav());

        Assert.Contains("if (a &lt; b &amp;&amp; c &gt; d) return &quot;x&quot;;", html);
        // The raw, unescaped angle bracket form must never reach the output.
        Assert.DoesNotContain("a < b", html);
    }

    [Fact]
    public void RenderPage_SingleLineUsesSingularPill()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "only" }, Nav());

        Assert.Contains("<span class=\"pill\">1 line</span>", html);
    }

    [Fact]
    public void RenderPage_UnknownExtensionRendersPlainCodeBlockWithoutLanguageClass()
    {
        // A file type not in the vendored grammar bundle falls back to plain monospace rather than a wrong grammar.
        const string path = "docs/notes.unknownext";
        const string output = "code/docs/notes.unknownext.html";

        var html = CodeFileTemplater.RenderPage(path, output, new[] { "just text" }, Nav());

        Assert.Contains("<pre class=\"code-file\"><code>", html);
        Assert.DoesNotContain("language-", html);
    }

    [Fact]
    public void RenderPlaceholder_RendersShellAndReasonWithoutLineTable()
    {
        var html = CodeFileTemplater.RenderPlaceholder(RepoRelative, OutputPath, "This file is too large to render inline.", Nav());

        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Contains($"<h1>{RepoRelative}</h1>", html);
        Assert.Contains("<p class=\"code-placeholder\">This file is too large to render inline.</p>", html);
        // No line table on a placeholder page.
        Assert.DoesNotContain("class=\"code-line\"", html);
        // A placeholder renders no <code> block, so it does not pull in the highlighter.
        Assert.DoesNotContain("prism.js", html);
        Assert.Contains("<span class=\"pill\">Not rendered</span>", html);
    }

    private static readonly (string OutputUrl, string Title)[] Refs =
    {
        ("epics/story-7-1.html", "Story 7.1: In-Portal Code File Browsing"),
        ("epics/epic-8.html", "Epic 8: Dashboard Command Center"),
    };

    [Fact]
    public void RenderPage_WithReferences_LeadsWithRelationshipGraphThenSecondarySource()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);

        // The relationships block (graph + accessible list) is present and is the hero — it precedes the source.
        Assert.Contains("<section class=\"code-relationships\">", html);
        Assert.Contains("class=\"ref-graph\"", html);
        Assert.Contains("<section class=\"code-source-section\"", html);
        var relIndex = html.IndexOf("code-relationships", StringComparison.Ordinal);
        var srcIndex = html.IndexOf("code-source-section", StringComparison.Ordinal);
        Assert.True(relIndex >= 0 && relIndex < srcIndex, "relationships must lead the page, source is secondary");

        // Each citing artifact is a real graph node link carrying its full title, with a compact ring label.
        Assert.Contains("class=\"ref-node\" href=\"", html);
        Assert.Contains("epics/story-7-1.html", html);
        Assert.Contains("<title>Story 7.1: In-Portal Code File Browsing</title>", html);
        Assert.Contains(">Story 7.1</text>", html);   // compact ring label (identifier before the colon)

        // The always-present accessible list carries the FULL titles and meaningful link text — visually hidden
        // (sr-only) so the visible surface is just the graph, but present in the DOM for assistive tech.
        Assert.Contains("class=\"ref-list sr-only\"", html);
        Assert.Contains(">Story 7.1: In-Portal Code File Browsing</a>", html);
        Assert.Contains(">Epic 8: Dashboard Command Center</a>", html);

        // The locked line anchors survive the redesign (source is de-emphasized, never removed).
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("data-code-path=\"src/SpecScribe/Sample.cs\"", html);
    }

    [Fact]
    public void RenderPage_NoReferences_OmitsRelationshipsBlockButKeepsSource()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav());

        Assert.DoesNotContain("code-relationships", html);
        Assert.DoesNotContain("ref-graph", html);
        // Source still renders with its anchors.
        Assert.Contains("<section class=\"code-source-section\"", html);
        Assert.Contains("id=\"L1\"", html);
    }

    [Fact]
    public void RenderPage_WithExternalUrl_AddsAdditiveViewSourceLink()
    {
        const string external = "https://github.com/owner/repo/blob/main/src/SpecScribe/Sample.cs";
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, external);

        Assert.Contains("class=\"code-external-link\"", html);
        Assert.Contains($"href=\"{external}\"", html);
        Assert.Contains("View on GitHub", html);
        // The in-portal source is still fully rendered — the external link is additive, not a replacement.
        Assert.Contains("class=\"code-file\"", html);
        Assert.Contains("<span class=\"code-line\" id=\"L1\" data-ln=\"1\">using System;</span>", html);
    }

    [Fact]
    public void RenderPage_NoExternalUrl_OmitsViewSourceLink()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);

        Assert.DoesNotContain("code-external-link", html);
    }

    // ---- Story 7.4: opt-in "Advanced coverage" section ----

    private static FileInsight SampleInsight() => new(
        ChangeCount: 7,
        Contributors: new[] { ("Alice", 5), ("Bob", 2) },
        CoupledFiles: new[] { ("src/SpecScribe/Other.cs", 4), ("docs/notes.md", 1) },
        History: new[]
        {
            new CommitTouch("abc1234", new DateOnly(2026, 7, 3), "Alice", "Refine the thing"),
            new CommitTouch("def5678", new DateOnly(2026, 7, 1), "Bob", "Seed the thing"),
        });

    [Fact]
    public void RenderPage_NullInsight_RendersNoAdvancedCoverageSection()
    {
        // A null insight (deep-git off / no data) must leave the page byte-identical to a plain render.
        var baseline = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);
        var withNull = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: null);

        Assert.DoesNotContain("code-insights", withNull);
        Assert.Equal(baseline, withNull);
    }

    [Fact]
    public void RenderPage_PopulatedInsight_RendersContributorsFrequencyCoupledAndHistory()
    {
        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight());

        Assert.Contains("<section class=\"code-insights\"", html);
        // Change frequency line.
        Assert.Contains("Changed in <strong>7</strong> commits", html);
        // Contributors — "N commits" attribution wording, no ranking language.
        Assert.Contains(">Alice</span> <span class=\"contributor-count\">5 commits</span>", html);
        Assert.Contains(">Bob</span> <span class=\"contributor-count\">2 commits</span>", html);
        Assert.DoesNotContain("rank", html.ToLowerInvariant());
        Assert.DoesNotContain("leaderboard", html.ToLowerInvariant());
        Assert.DoesNotContain("top developer", html.ToLowerInvariant());
        // Coupled files with co-change counts.
        Assert.Contains("docs/notes.md", html);
        Assert.Contains("class=\"coupled-count\">4&times;</span>", html);
        // History rows: date, hash, author, subject, newest-first.
        Assert.Contains("<table class=\"code-history-table\">", html);
        Assert.Contains("2026-07-03", html);
        Assert.Contains("Refine the thing", html);
        var newer = html.IndexOf("Refine the thing", StringComparison.Ordinal);
        var older = html.IndexOf("Seed the thing", StringComparison.Ordinal);
        Assert.True(newer >= 0 && newer < older, "history must be newest-first");
    }

    [Fact]
    public void RenderPage_CoupledLink_GuardedOnCodePageExistence()
    {
        // Only src/SpecScribe/Other.cs has a code page; docs/notes.md does not → plain <code>, never a dead link.
        string? Resolve(string path) => path == "src/SpecScribe/Other.cs" ? "code/src/SpecScribe/Other.cs.html" : null;

        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(), coupledFileHref: Resolve);

        // Resolved coupled file → a real link (prefixed to the code page's depth).
        Assert.Contains("<a href=\"../../../code/src/SpecScribe/Other.cs.html\">src/SpecScribe/Other.cs</a>", html);
        // Unresolved coupled file → plain <code>, no anchor.
        Assert.Contains("<code>docs/notes.md</code>", html);
        Assert.DoesNotContain("<a href=\"../../../docs/notes.md", html);
    }

    [Fact]
    public void RenderPage_HistoryHashLink_GuardedOnCommitPageExistence()
    {
        // abc1234 has a per-commit page; def5678 does not → plain <code>, never a dead link.
        string? Resolve(string shortHash) => shortHash == "abc1234" ? "commit/abc1234.html" : null;

        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(), commitHref: Resolve);

        Assert.Contains("<a href=\"../../../commit/abc1234.html\"><code>abc1234</code></a>", html);
        Assert.Contains("<code>def5678</code>", html);
        Assert.DoesNotContain("<a href=\"../../../commit/def5678", html);
    }

    [Fact]
    public void RenderPage_Insight_EscapesAuthorSubjectAndPath()
    {
        var insight = new FileInsight(
            ChangeCount: 1,
            Contributors: new[] { ("A<b>&\"lice", 1) },
            CoupledFiles: new[] { ("src/<x>&.cs", 1) },
            History: new[] { new CommitTouch("aaa1111", new DateOnly(2026, 7, 1), "E<v>il", "sub&<ject>\"") });

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight);

        Assert.Contains("A&lt;b&gt;&amp;&quot;lice", html);
        Assert.Contains("src/&lt;x&gt;&amp;.cs", html);
        Assert.Contains("E&lt;v&gt;il", html);
        Assert.Contains("sub&amp;&lt;ject&gt;&quot;", html);
        // No raw metacharacters from the insight leak through.
        Assert.DoesNotContain("E<v>il", html);
    }

    [Fact]
    public void RenderPage_Insight_OmitsEmptySubPartsWithoutEmptyHeadings()
    {
        // Contributors present, but no coupling and no history → only the contributors + frequency parts render.
        var insight = new FileInsight(
            ChangeCount: 3,
            Contributors: new[] { ("Alice", 3) },
            CoupledFiles: Array.Empty<(string, int)>(),
            History: Array.Empty<CommitTouch>());

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight);

        Assert.Contains("code-insights", html);
        Assert.Contains("Contributors to this file", html);
        Assert.DoesNotContain("Often changed with", html);
        Assert.DoesNotContain("Change history", html);
        Assert.DoesNotContain("code-history-table", html);
    }

    [Fact]
    public void RenderPage_Insight_DisclosesTruncatedContributorList()
    {
        // Shown list is capped at 2, but TotalContributors (12) says the file really has 12 — the page must not
        // let the capped list read as complete (code review addition, mirrors FileChangeStat.TotalContributors).
        var insight = new FileInsight(
            ChangeCount: 12,
            Contributors: new[] { ("Alice", 5), ("Bob", 4) },
            CoupledFiles: Array.Empty<(string, int)>(),
            History: Array.Empty<CommitTouch>(),
            TotalContributors: 12);

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight);

        Assert.Contains("+10 more contributors", html);
    }

    [Fact]
    public void RenderPage_Insight_OmitsMoreContributorsNoteWhenListIsComplete()
    {
        // TotalContributors equals the shown count (or is unset/default) — nothing was truncated, so no note.
        var insight = new FileInsight(
            ChangeCount: 2,
            Contributors: new[] { ("Alice", 2) },
            CoupledFiles: Array.Empty<(string, int)>(),
            History: Array.Empty<CommitTouch>(),
            TotalContributors: 1);

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight);

        Assert.DoesNotContain("code-insight-more", html);
    }

    // ---- Tab split: Insights | Relationships | History | Code, each iconed ----

    [Fact]
    public void RenderPage_FullData_RendersFourIconedTabsWithInsightsDefault()
    {
        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight());

        // Four tabs, in order, each with its modifier + a visible text label.
        foreach (var (mod, label) in new[] { ("insights", "Insights"), ("relationships", "Relationships"), ("history", "History"), ("source", "Code") })
        {
            Assert.Contains($"code-tab code-tab--{mod}", html);
            Assert.Contains($"code-tabpanel code-tabpanel--{mod}", html);
            Assert.Contains($"<span>{label}</span>", html);
        }

        // Each tab carries a decorative icon before its label — count within the tablist only (the reference graph
        // renders its own node icons elsewhere in the page).
        var tablist = Between(html, "code-tablist", "</fieldset>");
        Assert.Equal(4, CountOccurrences(tablist, "class=\"ss-icon\""));

        // Insights is the first tab and the only one checked (leads by default).
        var insightsTab = html.IndexOf("code-tab--insights", StringComparison.Ordinal);
        var relTab = html.IndexOf("code-tab--relationships", StringComparison.Ordinal);
        Assert.True(insightsTab >= 0 && insightsTab < relTab, "Insights must be the first tab");
        // Structural: exactly one radio carries the checked attribute (matching the input's closing bracket avoids
        // false hits on fixture text — an author or path containing the word "checked").
        Assert.Equal(1, CountOccurrences(html, " checked>"));
        // The checked attribute sits on the Insights radio (before the relationships tab appears).
        var checkedIndex = html.IndexOf("checked", StringComparison.Ordinal);
        Assert.True(checkedIndex > insightsTab && checkedIndex < relTab, "the checked radio must be the Insights tab");
    }

    [Fact]
    public void RenderPage_Graph_LivesInRelationshipsTabOnly_NotInsights()
    {
        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight());

        // The reference graph section renders exactly once, inside the relationships panel.
        Assert.Equal(1, CountOccurrences(html, "<section class=\"code-relationships\">"));
        var insightsPanel = Between(html, "code-tabpanel--insights", "code-tabpanel--relationships");
        var relPanel = Between(html, "code-tabpanel--relationships", "code-tabpanel--history");
        Assert.DoesNotContain("code-relationships", insightsPanel);
        Assert.DoesNotContain("ref-graph", insightsPanel);
        Assert.Contains("code-relationships", relPanel);
        Assert.Contains("ref-graph", relPanel);
    }

    [Fact]
    public void RenderPage_History_LivesInHistoryTabOnly_NotInsights()
    {
        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight());

        // The history table renders exactly once, inside the history panel — not in Insights.
        Assert.Equal(1, CountOccurrences(html, "code-history-table"));
        var insightsPanel = Between(html, "code-tabpanel--insights", "code-tabpanel--relationships");
        var historyPanel = Between(html, "code-tabpanel--history", "code-tabpanel--source");
        Assert.DoesNotContain("code-history-table", insightsPanel);
        Assert.Contains("code-history-table", historyPanel);
        Assert.Contains("Change history", historyPanel);
    }

    [Fact]
    public void RenderPage_CoupledCard_AppearsInBothInsightsAndRelationships()
    {
        var html = CodeFileTemplater.RenderPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight());

        // "Often changed with" (coupled files) is a relationship signal that owner-decision places in BOTH tabs.
        Assert.Equal(2, CountOccurrences(html, "Often changed with"));
        var insightsPanel = Between(html, "code-tabpanel--insights", "code-tabpanel--relationships");
        var relPanel = Between(html, "code-tabpanel--relationships", "code-tabpanel--history");
        Assert.Contains("Often changed with", insightsPanel);
        Assert.Contains("Often changed with", relPanel);
    }

    [Fact]
    public void RenderPage_RefsOnlyNoInsight_ShowsRelationshipsAndCodeWithRelationshipsDefault()
    {
        // No deep-git insight → no Insights tab and no History tab. Relationships leads (first surviving tab).
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);

        Assert.Contains("code-tab--relationships", html);
        Assert.Contains("code-tab--source", html);
        Assert.DoesNotContain("code-tab--insights", html);
        Assert.DoesNotContain("code-tab--history", html);
        // Exactly one checked radio, and it is the (leading) relationships tab.
        // Structural: exactly one radio carries the checked attribute (matching the input's closing bracket avoids
        // false hits on fixture text — an author or path containing the word "checked").
        Assert.Equal(1, CountOccurrences(html, " checked>"));
        var relTab = html.IndexOf("code-tab--relationships", StringComparison.Ordinal);
        var checkedIndex = html.IndexOf("checked", StringComparison.Ordinal);
        var sourceTab = html.IndexOf("code-tab--source", StringComparison.Ordinal);
        Assert.True(checkedIndex > relTab && checkedIndex < sourceTab, "relationships must be the default-checked tab");
    }

    [Fact]
    public void RenderPage_Uncited_NoInsight_RendersNoTabChrome()
    {
        // Only the source has content → no tabs at all; the source spans full width exactly as pre-tab.
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav());

        Assert.DoesNotContain("code-tabs", html);
        Assert.DoesNotContain("code-tablist", html);
        Assert.Contains("<section class=\"code-source-section\"", html);
    }

    /// <summary>The HTML slice between the first occurrence of <paramref name="startMarker"/> and the next occurrence
    /// of <paramref name="endMarker"/> — a coarse but reliable way to assert which tab panel a fragment lands in,
    /// since the panels render as ordered siblings.</summary>
    private static string Between(string html, string startMarker, string endMarker)
    {
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker not found: {startMarker}");
        var end = html.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0) end = html.Length;
        return html[start..end];
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
