using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for <see cref="SpaDelivery"/>'s pure string-slicing helpers — the landmark extraction the
/// whole-site consolidation depends on (Story 6.7). Complements the higher-level integration coverage in
/// <see cref="SiteGeneratorSpaTests"/> and <see cref="RenderSpaParityTests"/> with direct, adversarial-input cases
/// review flagged: a page whose raw HTML legitimately contains an EARLIER literal "&lt;/main&gt;"/"&lt;main id=..."
/// occurrence before the real landmark (reachable via Markdig's raw-HTML passthrough on any user-authored doc, not
/// just this repo's own content) must degrade gracefully, never crash the whole `--spa` emit.</summary>
public class SpaDeliveryTests
{
    private const string NavMarkup = "<nav class=\"site-nav\">NAV</nav>";

    [Fact]
    public void ExtractContentRegion_IgnoresAnEarlierLiteralClosingTag_BeforeTheRealLandmark()
    {
        // A doc whose body legitimately shows the landmark markup as an example (raw HTML passthrough), BEFORE the
        // real <main id="main-content"> the page itself carries. mainClose must never resolve to an index earlier
        // than mainOpen — that would make the slice below throw ArgumentOutOfRangeException.
        var page = "<body>"
            + "<p>Example: &lt;/main&gt; is not real markup, just a code sample rendered as text</p>"
            + "</main>" // a raw-HTML passthrough closer that is NOT the real landmark's closer
            + "<div class=\"breadcrumb\"><a href=\"index.html\">Home</a></div>"
            + "<main id=\"main-content\"><p>Real body</p></main>"
            + "</body>";

        var region = SpaDelivery.ExtractContentRegion(page, NavMarkup);

        Assert.Contains("Real body", region);
        Assert.Contains(NavMarkup, region);
    }

    [Fact]
    public void ExtractContentRegion_DegradesToNavOnly_WhenNoLandmarkIsPresent()
    {
        var region = SpaDelivery.ExtractContentRegion("<body>no landmark here</body>", NavMarkup);
        Assert.Equal(NavMarkup, region);
    }

    [Fact]
    public void ExtractBreadcrumb_RecoversLabelsAndTargets_FromCapturedHtml()
    {
        var page = "<div class=\"breadcrumb\" aria-label=\"Breadcrumb\">\n"
            + "  <a href=\"../index.html\">Home</a>\n"
            + "  <span class=\"crumb-sep\">/</span>\n"
            + "  <span class=\"crumb-current\" aria-current=\"page\">Widget</span>\n"
            + "</div>\n\n"
            + "<main id=\"main-content\"></main>";

        var crumbs = SpaDelivery.ExtractBreadcrumb(page, "requirements/widget.html");

        Assert.Equal(2, crumbs.Count);
        Assert.Equal(("Home", "index.html"), (crumbs[0].Label, crumbs[0].OutputRelativePath));
        Assert.Equal(("Widget", (string?)null), (crumbs[1].Label, crumbs[1].OutputRelativePath));
    }

    [Fact]
    public void ExtractBreadcrumb_IsEmpty_WhenPageCarriesNoBreadcrumb()
    {
        var crumbs = SpaDelivery.ExtractBreadcrumb("<main id=\"main-content\"></main>", "index.html");
        Assert.Empty(crumbs);
    }

    private static SpaBundle SyntheticBundle(IEnumerable<string> outputRelativePaths, string entryPath = "index.html")
    {
        var pages = outputRelativePaths
            .Select(p => new SpaPage(p, p, $"<main id=\"main-content\">{p}</main>", Array.Empty<BreadcrumbCrumb>()))
            .ToList();
        return new SpaBundle("Test Site", entryPath, Array.Empty<(string, string)>(), pages);
    }

    private static Dictionary<string, string> ChunkContent(IReadOnlyList<SpaDelivery.OutputFile> files, string chunkFile) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(files.Single(f => f.OutputRelativePath == chunkFile).Content)!;

    /// <summary>Deferred item (Story 6.7 review): the "split oversized groups into numbered files" branch of
    /// <see cref="SpaDelivery.BuildDataFiles"/> had zero test coverage at the <see cref="SpaDelivery.MaxPagesPerChunk"/>
    /// (75) boundary — an off-by-one in the batch arithmetic (<c>count / MaxPagesPerChunk + 1</c>) would go
    /// undetected. Pins the exact split point (74 stays in one chunk, 75 stays in one chunk, 76 spills into a
    /// second) plus a double-boundary case (150/151) so a future change to the cap or the arithmetic can't drift
    /// silently.</summary>
    [Theory]
    [InlineData(74, 1)]
    [InlineData(75, 1)]
    [InlineData(76, 2)]
    [InlineData(150, 2)]
    [InlineData(151, 3)]
    public void BuildDataFiles_SplitsOversizedGroups_AtTheMaxPagesPerChunkBoundary(int pageCount, int expectedChunkFiles)
    {
        var paths = Enumerable.Range(1, pageCount).Select(i => $"docs/page-{i:0000}.html");
        var bundle = SyntheticBundle(paths);

        var files = SpaDelivery.BuildDataFiles(bundle);

        var chunkFiles = files
            .Where(f => f.OutputRelativePath.StartsWith($"{SpaDelivery.ChunkDir}/pages-docs", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(expectedChunkFiles, chunkFiles.Count);

        // Every page lands in EXACTLY one chunk — no page dropped, none duplicated.
        var totalPagesAcrossChunks = chunkFiles.Sum(f => ChunkContent(files, f.OutputRelativePath).Count);
        Assert.Equal(pageCount, totalPagesAcrossChunks);

        // Every non-final chunk is exactly full (no premature split, no off-by-one short chunk).
        for (var batch = 1; batch < chunkFiles.Count; batch++)
        {
            var chunkFile = batch == 1 ? $"{SpaDelivery.ChunkDir}/pages-docs.json" : $"{SpaDelivery.ChunkDir}/pages-docs-{batch}.json";
            Assert.Equal(SpaDelivery.MaxPagesPerChunk, ChunkContent(files, chunkFile).Count);
        }
    }

    /// <summary>Deferred item (at-scale SPA perf pass, Story 6.7): the count-only cap "cannot" bound the largest
    /// chunk once one page in a group is itself huge — measured at a real large-repo scale, a single 82.5 MB
    /// <c>code-map.html</c> dragged its whole 18-page top-level group into one 112.9 MB <c>pages-root.json</c>,
    /// penalizing every co-located page's fetch. Pins the fix: a page whose content alone exceeds
    /// <see cref="SpaDelivery.MaxChunkBytes"/> is isolated into its own dedicated chunk — its neighbors before and
    /// after land in NORMAL, budget-sized chunks that never carry the mega-page's bytes.</summary>
    [Fact]
    public void BuildDataFiles_IsolatesAnOversizedPage_IntoItsOwnDedicatedChunk_LeavingNeighborsUnburdened()
    {
        var normalHtml = new string('n', 1_000); // ~1 KB — trivially small relative to the budget
        var hugeHtml = new string('h', SpaDelivery.MaxChunkBytes + 1); // exceeds the byte budget on its own

        var pages = new List<SpaPage>
        {
            new("root/a.html", "a", normalHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/b.html", "b", normalHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/c-huge.html", "c", hugeHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/d.html", "d", normalHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/e.html", "e", normalHtml, Array.Empty<BreadcrumbCrumb>()),
        };
        var bundle = new SpaBundle("Test Site", "index.html", Array.Empty<(string, string)>(), pages);

        var files = SpaDelivery.BuildDataFiles(bundle);
        var rootChunks = files
            .Where(f => f.OutputRelativePath.StartsWith($"{SpaDelivery.ChunkDir}/pages-root", StringComparison.Ordinal))
            .ToList();

        // Three chunks: [a, b] normal-sized, [c-huge] alone, [d, e] normal-sized again — never four-in-one.
        Assert.Equal(3, rootChunks.Count);

        var byMembership = rootChunks.ToDictionary(f => f.OutputRelativePath, f => ChunkContent(files, f.OutputRelativePath).Keys.ToList());
        var chunkWithHuge = byMembership.Single(kv => kv.Value.Contains("root/c-huge.html"));
        Assert.Single(chunkWithHuge.Value); // the huge page shares its chunk with nobody

        var otherChunks = byMembership.Where(kv => kv.Key != chunkWithHuge.Key).ToList();
        Assert.Equal(2, otherChunks.Count);
        Assert.All(otherChunks, kv => Assert.DoesNotContain("root/c-huge.html", kv.Value));
        var otherChunkSets = otherChunks.Select(kv => kv.Value.OrderBy(p => p, StringComparer.Ordinal).ToList()).ToList();
        Assert.Contains(otherChunkSets, set => set.SequenceEqual(new[] { "root/a.html", "root/b.html" }));
        Assert.Contains(otherChunkSets, set => set.SequenceEqual(new[] { "root/d.html", "root/e.html" }));
    }

    /// <summary>Review follow-up: the mid-group isolation test above doesn't pin the FIRST- or LAST-in-group
    /// boundary positions for the same isolation logic — both are handled by different branches of the batch
    /// reset condition inside <see cref="SpaDelivery.BuildDataFiles"/> (first: the empty-batch case where the
    /// running page count is 0 never triggers a split; last: nothing AFTER it to accidentally merge with). Pins
    /// both explicitly rather than leaving them to a manual trace.</summary>
    [Fact]
    public void BuildDataFiles_IsolatesAnOversizedPage_WhenItIsFirstInItsGroup()
    {
        var normalHtml = new string('n', 1_000);
        var hugeHtml = new string('h', SpaDelivery.MaxChunkBytes + 1);
        var pages = new List<SpaPage>
        {
            new("root/a-huge.html", "a", hugeHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/b.html", "b", normalHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/c.html", "c", normalHtml, Array.Empty<BreadcrumbCrumb>()),
        };
        var bundle = new SpaBundle("Test Site", "index.html", Array.Empty<(string, string)>(), pages);

        var files = SpaDelivery.BuildDataFiles(bundle);
        var rootChunks = files
            .Where(f => f.OutputRelativePath.StartsWith($"{SpaDelivery.ChunkDir}/pages-root", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, rootChunks.Count); // [a-huge] alone, [b, c] together
        var byMembership = rootChunks.Select(f => ChunkContent(files, f.OutputRelativePath).Keys.ToList()).ToList();
        Assert.Contains(byMembership, set => set.SequenceEqual(new[] { "root/a-huge.html" }));
        Assert.Contains(byMembership, set => set.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(new[] { "root/b.html", "root/c.html" }));
    }

    [Fact]
    public void BuildDataFiles_IsolatesAnOversizedPage_WhenItIsLastInItsGroup()
    {
        var normalHtml = new string('n', 1_000);
        var hugeHtml = new string('h', SpaDelivery.MaxChunkBytes + 1);
        var pages = new List<SpaPage>
        {
            new("root/a.html", "a", normalHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/b.html", "b", normalHtml, Array.Empty<BreadcrumbCrumb>()),
            new("root/c-huge.html", "c", hugeHtml, Array.Empty<BreadcrumbCrumb>()),
        };
        var bundle = new SpaBundle("Test Site", "index.html", Array.Empty<(string, string)>(), pages);

        var files = SpaDelivery.BuildDataFiles(bundle);
        var rootChunks = files
            .Where(f => f.OutputRelativePath.StartsWith($"{SpaDelivery.ChunkDir}/pages-root", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, rootChunks.Count); // [a, b] together, [c-huge] alone
        var byMembership = rootChunks.Select(f => ChunkContent(files, f.OutputRelativePath).Keys.ToList()).ToList();
        Assert.Contains(byMembership, set => set.SequenceEqual(new[] { "root/c-huge.html" }));
        Assert.Contains(byMembership, set => set.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(new[] { "root/a.html", "root/b.html" }));
    }

    /// <summary>Deferred item (Story 6.7 review): chunk-batch assignment was said to "depend on unstated stable
    /// enumeration order of _docs.Values" — but <see cref="SpaDelivery.BuildDataFiles"/> already sorts pages by
    /// <c>OutputRelativePath</c> (Ordinal) before assigning batch numbers, so the upstream enumeration order should
    /// never actually matter. This pins that guarantee directly: three different input orderings of the SAME page
    /// set must produce IDENTICAL chunk-file names and IDENTICAL page-to-chunk membership.</summary>
    [Fact]
    public void BuildDataFiles_ChunkBatchAssignment_IsIndependentOfInputEnumerationOrder()
    {
        var paths = Enumerable.Range(1, 200).Select(i => $"epics/story-{i:0000}.html").ToList();

        var forward = SpaDelivery.BuildDataFiles(SyntheticBundle(paths));
        var reversed = SpaDelivery.BuildDataFiles(SyntheticBundle(Enumerable.Reverse(paths)));
        var shuffled = SpaDelivery.BuildDataFiles(SyntheticBundle(Shuffle(paths)));

        AssertSameChunkAssignment(forward, reversed);
        AssertSameChunkAssignment(forward, shuffled);

        static List<string> Shuffle(List<string> items)
        {
            var rng = new Random(42);
            return items.OrderBy(_ => rng.Next()).ToList();
        }

        static void AssertSameChunkAssignment(IReadOnlyList<SpaDelivery.OutputFile> a, IReadOnlyList<SpaDelivery.OutputFile> b)
        {
            var aFileNames = a.Select(f => f.OutputRelativePath).OrderBy(p => p, StringComparer.Ordinal).ToList();
            var bFileNames = b.Select(f => f.OutputRelativePath).OrderBy(p => p, StringComparer.Ordinal).ToList();
            Assert.Equal(aFileNames, bFileNames);

            foreach (var file in aFileNames.Where(f => f != SpaDelivery.ManifestPath))
            {
                var aPages = ChunkContent(a, file).Keys.OrderBy(k => k, StringComparer.Ordinal);
                var bPages = ChunkContent(b, file).Keys.OrderBy(k => k, StringComparer.Ordinal);
                Assert.Equal(aPages, bPages);
            }
        }
    }
}
