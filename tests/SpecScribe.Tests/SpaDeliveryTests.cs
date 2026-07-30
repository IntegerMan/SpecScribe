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

    // ===== Story 22.6: the watch-mode delta document ========================================================

    /// <summary>Builds a manifest through the REAL <see cref="SpaDelivery.BuildDataFiles"/> rather than
    /// hand-writing one, so these tests exercise the shape the emitter actually produces — a hand-rolled fixture
    /// would keep passing after a manifest change that broke the delta.</summary>
    private static string Manifest(params (string Path, string Body)[] pages)
    {
        var bundle = new SpaBundle(
            "Test Site", "index.html", Array.Empty<(string, string)>(),
            pages.Select(p => new SpaPage(
                p.Path, p.Path, $"<main id=\"main-content\">{p.Body}</main>", Array.Empty<BreadcrumbCrumb>())).ToList());
        return SpaDelivery.BuildDataFiles(bundle).Single(f => f.OutputRelativePath == SpaDelivery.ManifestPath).Content;
    }

    private static JsonElement Delta(
        string? previous, string current, long sequence = 1, string trigger = "docs/a.md", bool forceFull = false) =>
        JsonDocument.Parse(SpaDelivery.BuildDelta(
            previous, current, sequence, trigger, DateTimeOffset.UnixEpoch, forceFull)).RootElement;

    private static string[] Arr(JsonElement delta, string name) =>
        delta.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToArray();

    /// <summary>The baseline every other delta case is measured against: a regen that changed nothing must emit an
    /// EMPTY delta, not a full one. If this degraded to <c>full</c> the sidecar would be worthless — a watch
    /// session's debounce fires on saves that frequently change no rendered output at all (Task 1's no-op control
    /// measured exactly zero churn on all four routes), and every one of them would tell the consumer to refetch
    /// the whole IR.</summary>
    [Fact]
    public void BuildDelta_EmitsAnEmptyDelta_WhenNothingChanged()
    {
        var manifest = Manifest(("index.html", "home"), ("docs/a.html", "alpha"));

        var delta = Delta(manifest, manifest);

        Assert.False(delta.GetProperty("full").GetBoolean());
        Assert.Empty(Arr(delta, "changed"));
        Assert.Empty(Arr(delta, "added"));
        Assert.Empty(Arr(delta, "removed"));
        Assert.Empty(Arr(delta, "chunks"));
    }

    /// <summary>The whole point of Story 22.2's per-page <c>contentHash</c>: one page edited names EXACTLY that
    /// page, not its neighbours and not its whole chunk. Task 1 measured this route re-shipping 39.9 % of the IR
    /// at chunk granularity before page addressing existed.</summary>
    [Fact]
    public void BuildDelta_NamesOnlyTheEditedPage_AndTheChunkCarryingIt()
    {
        var before = Manifest(("index.html", "home"), ("docs/a.html", "alpha"), ("docs/b.html", "beta"));
        var after = Manifest(("index.html", "home"), ("docs/a.html", "ALPHA EDITED"), ("docs/b.html", "beta"));

        var delta = Delta(before, after);

        Assert.False(delta.GetProperty("full").GetBoolean());
        Assert.Equal(new[] { "docs/a.html" }, Arr(delta, "changed"));
        Assert.Empty(Arr(delta, "added"));
        Assert.Empty(Arr(delta, "removed"));
        Assert.Equal(new[] { "spa/pages-docs.json" }, Arr(delta, "chunks"));
    }

    [Fact]
    public void BuildDelta_ReportsAnAddedPage_AndItsChunk()
    {
        var before = Manifest(("index.html", "home"));
        var after = Manifest(("index.html", "home"), ("docs/new.html", "fresh"));

        var delta = Delta(before, after);

        Assert.Equal(new[] { "docs/new.html" }, Arr(delta, "added"));
        Assert.Empty(Arr(delta, "changed"));
        Assert.Equal(new[] { "spa/pages-docs.json" }, Arr(delta, "chunks"));
    }

    /// <summary>A removed page carries NO chunk: the chunk that held it may not exist any more, and a consumer
    /// applying a removal needs no bytes to do it. Pinning this stops a future "symmetry" refactor from telling
    /// consumers to fetch a file that was just deleted.</summary>
    [Fact]
    public void BuildDelta_ReportsARemovedPage_AndAsksForNoChunkToApplyIt()
    {
        var before = Manifest(("index.html", "home"), ("docs/gone.html", "bye"));
        var after = Manifest(("index.html", "home"));

        var delta = Delta(before, after);

        Assert.Equal(new[] { "docs/gone.html" }, Arr(delta, "removed"));
        Assert.Empty(Arr(delta, "changed"));
        Assert.Empty(Arr(delta, "added"));
        Assert.Empty(Arr(delta, "chunks"));
    }

    /// <summary>AC #7, first degrade condition: the first emit of a watch session has no basis to diff against.</summary>
    [Fact]
    public void BuildDelta_DegradesToFull_WhenThereIsNoPreviousManifest()
    {
        var delta = Delta(previous: null, current: Manifest(("index.html", "home")));

        AssertIsFullMarker(delta);
    }

    /// <summary>AC #7: a <see cref="SpaDelivery.SchemaVersion"/> change between emits makes a page-level diff
    /// meaningless — version 2 moved the content region's start marker and moved 594 pages' hashes by +30 bytes
    /// each, none of which was a content change. Simulated by rewriting the version on an otherwise IDENTICAL
    /// manifest, so the ONLY difference is the schema: without the guard this would report zero changes, which is
    /// the false-unchanged failure AC #7 exists to prevent.</summary>
    [Fact]
    public void BuildDelta_DegradesToFull_WhenTheIrSchemaVersionMovedBetweenEmits()
    {
        var current = Manifest(("index.html", "home"), ("docs/a.html", "alpha"));
        var previous = current.Replace(
            $"\"schemaVersion\":{SpaDelivery.SchemaVersion}",
            $"\"schemaVersion\":{SpaDelivery.SchemaVersion - 1}",
            StringComparison.Ordinal);
        Assert.NotEqual(previous, current); // the rewrite must actually have bitten

        AssertIsFullMarker(Delta(previous, current));
    }

    /// <summary>AC #7: the caller-declared untrustworthy basis — how a <c>RegenerateTopology</c> escalation
    /// reports itself. A literal diff there would produce a thousand-entry <c>changed</c> list, larger and slower
    /// than the full payload it was meant to replace.</summary>
    [Fact]
    public void BuildDelta_DegradesToFull_WhenTheCallerForcesIt_EvenWithAPerfectlyGoodBasis()
    {
        var manifest = Manifest(("index.html", "home"));

        AssertIsFullMarker(Delta(manifest, manifest, forceFull: true));
    }

    /// <summary>AC #7 / NFR2: an unparseable or structurally alien basis degrades rather than throwing. This runs
    /// on a watch loop; an exception here would take down the session over a best-effort optimization.</summary>
    [Theory]
    [InlineData("{ not json at all")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":2}")]                       // no pages object
    [InlineData("{\"schemaVersion\":2,\"pages\":[]}")]           // pages is the wrong kind
    [InlineData("{\"schemaVersion\":2,\"pages\":{\"a.html\":{}}}")] // page entry missing contentHash/chunk
    public void BuildDelta_DegradesToFull_RatherThanThrowing_OnAnUnusableBasis(string previous)
    {
        AssertIsFullMarker(Delta(previous, Manifest(("index.html", "home"))));
    }

    /// <summary>The mirror case: an unreadable CURRENT manifest must not be diffed into "every page removed".</summary>
    [Fact]
    public void BuildDelta_DegradesToFull_WhenTheCurrentManifestIsUnreadable()
    {
        var delta = Delta(Manifest(("index.html", "home")), "{ truncated");

        AssertIsFullMarker(delta);
    }

    /// <summary>The document is a CONTRACT (AC #2) — Story 22.5 and any future consumer bind to these names. This
    /// pins the envelope: every field present, correctly named, correctly typed, with the version constants
    /// SEPARATE (AC #4 forbids bumping <see cref="SpaDelivery.SchemaVersion"/> for this feature).</summary>
    [Fact]
    public void BuildDelta_EmitsTheContractedEnvelope_WithBothVersionsCarriedSeparately()
    {
        var manifest = Manifest(("index.html", "home"));

        var delta = Delta(manifest, manifest, sequence: 7, trigger: "_bmad-output/planning-artifacts/epics.md");

        Assert.Equal(SpaDelivery.DeltaSchemaVersion, delta.GetProperty("deltaSchemaVersion").GetInt32());
        Assert.Equal(SpaDelivery.SchemaVersion, delta.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(7, delta.GetProperty("sequence").GetInt64());
        Assert.Equal("_bmad-output/planning-artifacts/epics.md", delta.GetProperty("trigger").GetString());
        Assert.Equal("1970-01-01T00:00:00.0000000Z", delta.GetProperty("generatedAt").GetString());
        Assert.Equal(JsonValueKind.False, delta.GetProperty("full").ValueKind);
        foreach (var list in new[] { "changed", "added", "removed", "chunks" })
        {
            Assert.Equal(JsonValueKind.Array, delta.GetProperty(list).ValueKind);
        }
    }

    /// <summary>The escalated topology pass reuses <see cref="FileWatcherService.TopologyEventLabel"/> VERBATIM
    /// rather than introducing a third spelling — that constant is already shared between the watcher and
    /// <c>SiteGenerator.RegenerateTopology</c> "so the two can never drift", and the sidecar joins that pact.</summary>
    [Fact]
    public void BuildDelta_CarriesTheSharedTopologyLabel_AsItsTrigger()
    {
        var manifest = Manifest(("index.html", "home"));

        var delta = Delta(manifest, manifest, trigger: FileWatcherService.TopologyEventLabel, forceFull: true);

        Assert.Equal("<directory change>", delta.GetProperty("trigger").GetString());
        Assert.Equal(FileWatcherService.TopologyEventLabel, delta.GetProperty("trigger").GetString());
    }

    /// <summary>NFR9-adjacent determinism: the same two manifests must produce a BYTE-identical document, and page
    /// order must not leak input enumeration order. <see cref="SpaDelivery.BuildDataFiles"/> holds this same
    /// discipline for chunk membership; the delta is written to disk on every watch regen, so a nondeterministic
    /// ordering would churn the sidecar's bytes for no reason.</summary>
    [Fact]
    public void BuildDelta_IsDeterministic_AndOrdersPathsOrdinally()
    {
        var before = Manifest(("index.html", "h"), ("docs/b.html", "b"), ("docs/a.html", "a"), ("docs/c.html", "c"));
        var after = Manifest(("index.html", "h"), ("docs/c.html", "C!"), ("docs/a.html", "A!"), ("docs/b.html", "B!"));

        var first = SpaDelivery.BuildDelta(before, after, 3, "docs/a.md", DateTimeOffset.UnixEpoch);
        var second = SpaDelivery.BuildDelta(before, after, 3, "docs/a.md", DateTimeOffset.UnixEpoch);

        Assert.Equal(first, second);
        Assert.Equal(
            new[] { "docs/a.html", "docs/b.html", "docs/c.html" },
            Arr(JsonDocument.Parse(first).RootElement, "changed"));
    }

    /// <summary>Code review finding (Story 22.6): a page's <c>ContentHash</c> is not the whole story. Once a
    /// top-level group holds exactly <see cref="SpaDelivery.MaxPagesPerChunk"/> pages, inserting one more
    /// earlier-sorting page pushes the ordinally-LAST pre-existing page into a second chunk batch — its content
    /// never changed, but the FILE that carries it did. Before this fix, <c>BuildDelta</c> compared only
    /// <c>ContentHash</c> and silently omitted that page from <c>changed</c>/<c>chunks</c>, leaving a polling
    /// consumer holding a stale chunk pointer for a page it never knew moved.</summary>
    [Fact]
    public void BuildDelta_NamesAPage_WhenOnlyItsChunkAssignmentMoved_NotItsContent()
    {
        var pageCount = SpaDelivery.MaxPagesPerChunk;
        var beforePages = Enumerable.Range(1, pageCount)
            .Select(i => ($"docs/page-{i:0000}.html", "same"))
            .ToArray();
        var before = Manifest(beforePages);

        // One new, ordinally-EARLIER page pushes every "docs" page's batch index up by one slot once the group
        // exceeds MaxPagesPerChunk — the ordinally-last pre-existing page spills into a second chunk file.
        var after = Manifest(new[] { ("docs/page-0000.html", "new") }.Concat(beforePages).ToArray());

        var delta = Delta(before, after);

        Assert.False(delta.GetProperty("full").GetBoolean());
        Assert.Contains("docs/page-0000.html", Arr(delta, "added"));
        var pushedPage = $"docs/page-{pageCount:0000}.html";
        Assert.Contains(pushedPage, Arr(delta, "changed"));
        Assert.Contains($"{SpaDelivery.ChunkDir}/pages-docs-2.json", Arr(delta, "chunks"));
    }

    /// <summary>Code review finding (Story 22.6): site-level identity (title/nav) is invisible to a page-keyed
    /// diff. A retitle with zero page-content edits must not ship as an empty, non-full delta — the consumer
    /// would never learn the title changed and has no other signal to refetch on.</summary>
    [Fact]
    public void BuildDelta_DegradesToFull_WhenSiteTitleChanges_WithNoPageContentEdits()
    {
        var before = Manifest(("index.html", "home"), ("docs/a.html", "alpha"));
        const string titleField = "\"siteTitle\":\"Test Site\"";
        Assert.Contains(titleField, before); // sanity: the substitution target actually exists in the manifest
        var after = before.Replace(titleField, "\"siteTitle\":\"Renamed Site\"", StringComparison.Ordinal);

        var delta = Delta(before, after);

        AssertIsFullMarker(delta);
    }

    /// <summary>Same code review finding, exercised on the <c>nav</c> half of the site-identity fingerprint
    /// rather than <c>siteTitle</c> — a nav-label rename with zero page-content edits must also force full.
    /// Built directly through <see cref="SpaDelivery.BuildDataFiles"/> (bypassing the <see cref="Manifest"/>
    /// helper, which always emits an empty nav) so the bundle can carry a real nav item.</summary>
    [Fact]
    public void BuildDelta_DegradesToFull_WhenNavChanges_WithNoPageContentEdits()
    {
        var page = new SpaPage("index.html", "index.html", "<main id=\"main-content\">home</main>", Array.Empty<BreadcrumbCrumb>());
        string ManifestWithNavLabel(string label) =>
            SpaDelivery.BuildDataFiles(new SpaBundle("Test Site", "index.html", new[] { (label, "index.html") }, new[] { page }))
                .Single(f => f.OutputRelativePath == SpaDelivery.ManifestPath).Content;

        var before = ManifestWithNavLabel("Home");
        var after = ManifestWithNavLabel("Dashboard");

        var delta = Delta(before, after);

        AssertIsFullMarker(delta);
    }

    /// <summary>A full marker is not merely <c>full: true</c> — AC #7 requires the page lists be EMPTY, so a
    /// consumer can never half-apply a delta it was told to distrust.</summary>
    private static void AssertIsFullMarker(JsonElement delta)
    {
        Assert.True(delta.GetProperty("full").GetBoolean());
        Assert.Empty(Arr(delta, "changed"));
        Assert.Empty(Arr(delta, "added"));
        Assert.Empty(Arr(delta, "removed"));
        Assert.Empty(Arr(delta, "chunks"));
    }

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

    // ===== Story 22.2: byte-bounded chunking with no silent escape hatch (AC #2) ==============================

    /// <summary>The chunk file is assembled from pre-encoded key/value tokens rather than by serializing a
    /// <c>Dictionary&lt;string, string&gt;</c> — that is what lets the byte budget be measured against the EXACT
    /// bytes that get written. Pins the equivalence directly so the two can never drift: the assembled file must
    /// be byte-identical to what <see cref="System.Text.Json"/> would have produced for the same map in the same
    /// order with the same options. Includes the escaping corner that matters (<c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c>
    /// each become a 6-byte <c>\uXXXX</c>) and a non-ASCII character. [Story 22.2]</summary>
    [Fact]
    public void BuildDataFiles_ChunkJson_IsByteIdenticalToSerializingTheEquivalentDictionary()
    {
        var pages = new List<SpaPage>
        {
            new("docs/a.html", "a", "<main id=\"main-content\"><p>a &amp; b &lt;tag&gt;</p></main>", Array.Empty<BreadcrumbCrumb>()),
            new("docs/b.html", "b", "<main id=\"main-content\"><p>café — naïve \"quoted\"</p></main>", Array.Empty<BreadcrumbCrumb>()),
            new("docs/c.html", "c", "<main id=\"main-content\"><p>line\nbreak\ttab</p></main>", Array.Empty<BreadcrumbCrumb>()),
        };
        var bundle = new SpaBundle("Test Site", "index.html", Array.Empty<(string, string)>(), pages);

        var chunk = SpaDelivery.BuildDataFiles(bundle)
            .Single(f => f.OutputRelativePath == $"{SpaDelivery.ChunkDir}/pages-docs.json");

        // Same insertion order BuildDataFiles walks (ordinal by path, entry page first — none here is the entry).
        var expectedMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in pages.OrderBy(p => p.OutputRelativePath, StringComparer.Ordinal))
        {
            expectedMap[p.OutputRelativePath] = p.ContentHtml;
        }
        var expected = JsonSerializer.Serialize(expectedMap, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.Equal(expected, chunk.Content);
        Assert.Contains("\\u003C", chunk.Content); // the escaping really is in play
    }

    /// <summary>Story 22.2 AC #2, the case the pre-22.2 budget could not see: pages whose RAW UTF-8 size fits
    /// comfortably under <see cref="SpaDelivery.MaxChunkBytes"/> but whose JSON-ESCAPED size does not, because
    /// every <c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c> balloons 1 byte → 6. Budgeting raw bytes (the old behavior) put
    /// all of these in one chunk and wrote a file several times over the ceiling — the "approximation, not an
    /// exact ceiling" caveat the old doc comment admitted. Budgeting the encoded tokens splits them, and NO
    /// emitted multi-page chunk exceeds the ceiling.</summary>
    [Fact]
    public void BuildDataFiles_NoChunkExceedsTheCeiling_WhenJsonEscapingInflatesTheContent()
    {
        // Every "<&>" (3 raw bytes) escapes to "<&>" (18 bytes) — a 6x inflation. Sized so that
        // each page is comfortably UNDER the ceiling on its own (120 KB raw / 720 KB encoded — this is not the
        // isolated-oversized-page path), the four together fit the ceiling on RAW bytes (480 KB, so the pre-22.2
        // budget put all four in ONE file), but exceed it on ENCODED bytes (2.88 MB) and so must split.
        var inflating = string.Concat(Enumerable.Repeat("<&>", 40_000)); // 120 000 raw bytes → ~720 002 encoded
        var pages = Enumerable.Range(1, 4)
            .Select(i => new SpaPage($"docs/p{i}.html", $"p{i}", inflating, Array.Empty<BreadcrumbCrumb>()))
            .ToList();
        Assert.True(pages.Sum(p => System.Text.Encoding.UTF8.GetByteCount(p.ContentHtml)) < SpaDelivery.MaxChunkBytes,
            "fixture invariant: the RAW total must fit the budget, or this proves nothing about escaping");
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(inflating)) < SpaDelivery.MaxChunkBytes,
            "fixture invariant: no SINGLE page may be over-cap, or this tests isolation instead of budgeting");

        var bundle = new SpaBundle("Test Site", "index.html", Array.Empty<(string, string)>(), pages);
        var files = SpaDelivery.BuildDataFiles(bundle);
        var chunks = files.Where(f => f.OutputRelativePath != SpaDelivery.ManifestPath).ToList();

        // More than one chunk (the raw budget would have produced exactly one)…
        Assert.True(chunks.Count > 1, $"expected the encoded budget to split these pages; got {chunks.Count} chunk(s)");
        // …and every emitted chunk file is genuinely at or under the ceiling.
        foreach (var chunk in chunks)
        {
            var size = System.Text.Encoding.UTF8.GetByteCount(chunk.Content);
            Assert.True(size <= SpaDelivery.MaxChunkBytes,
                $"{chunk.OutputRelativePath} is {size} B, over the {SpaDelivery.MaxChunkBytes} B ceiling");
        }
        // No page was oversized ON ITS OWN, so nothing is declared.
        Assert.DoesNotContain("\"oversizedPages\":[{", ManifestOf(files));
    }

    /// <summary>The ONE declared exception (Story 22.2 AC #2): a single page whose own encoded size exceeds the
    /// ceiling cannot be split — its content region is atomic — so its dedicated chunk IS written over-cap. That
    /// must never be silent: the manifest names the page and records the real size of the file it produces.
    /// Story 22.1 measured exactly this shape live (a 3.08 MB chunk against a 2 MB guard).</summary>
    [Fact]
    public void BuildDataFiles_DeclaresAnUnavoidablyOversizedSinglePage_InTheManifest()
    {
        var huge = new string('h', SpaDelivery.MaxChunkBytes + 1);
        var pages = new List<SpaPage>
        {
            new("root/a.html", "a", new string('n', 1_000), Array.Empty<BreadcrumbCrumb>()),
            new("root/b-huge.html", "b", huge, Array.Empty<BreadcrumbCrumb>()),
        };
        var files = SpaDelivery.BuildDataFiles(
            new SpaBundle("Test Site", "index.html", Array.Empty<(string, string)>(), pages));

        using var manifest = JsonDocument.Parse(ManifestOf(files));
        var declared = manifest.RootElement.GetProperty("oversizedPages").EnumerateArray().ToList();
        var only = Assert.Single(declared);
        Assert.Equal("root/b-huge.html", only.GetProperty("path").GetString());

        // The declared size is the size of the FILE that actually gets written — not the raw content size, and
        // not an estimate. A consumer can compare it against the ceiling itself.
        var hugeChunk = files.Single(f =>
            f.OutputRelativePath != SpaDelivery.ManifestPath &&
            f.Content.Contains("root/b-huge.html", StringComparison.Ordinal));
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(hugeChunk.Content), only.GetProperty("chunkBytes").GetInt64());
        Assert.True(only.GetProperty("chunkBytes").GetInt64() > SpaDelivery.MaxChunkBytes);

        // The normal neighbour is untouched and under the ceiling — isolation still holds.
        var normalChunk = files.Single(f =>
            f.OutputRelativePath != SpaDelivery.ManifestPath &&
            f.Content.Contains("root/a.html", StringComparison.Ordinal));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(normalChunk.Content) <= SpaDelivery.MaxChunkBytes);
    }

    [Fact]
    public void Manifest_CarriesTheSchemaVersion_AndAnEmptyOversizedListWhenNothingIsOverCap()
    {
        var files = SpaDelivery.BuildDataFiles(SyntheticBundle(new[] { "docs/a.html", "docs/b.html" }));

        using var manifest = JsonDocument.Parse(ManifestOf(files));
        Assert.Equal(SpaDelivery.SchemaVersion, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        // Present-and-empty, not absent: "no page is over cap" is an assertion the IR makes, not a silence.
        Assert.Empty(manifest.RootElement.GetProperty("oversizedPages").EnumerateArray());
    }

    private static string ManifestOf(IReadOnlyList<SpaDelivery.OutputFile> files) =>
        files.Single(f => f.OutputRelativePath == SpaDelivery.ManifestPath).Content;

    // ===== Story 22.2: the capture extractors (AC #5) ========================================================

    /// <summary>The nav slice keeps the page's OWN nav — including the page-local context band the re-render
    /// path cannot reproduce (there is no path → NavLocalContext resolver) — and stops at the nav element's own
    /// closer, EXCLUDING the inline toggle script that follows it on the HTML surface. [Story 22.2 AC #5]</summary>
    [Fact]
    public void ExtractNavMarkup_TakesThePagesOwnNav_AndStopsBeforeTheInlineToggleScript()
    {
        var page = "<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>\n"
            + "<nav class=\"site-nav\" aria-label=\"Document navigation\">\n"
            + "  <div class=\"site-nav-inner\"><a href=\"index.html\">Home</a></div>\n"
            + "  <div class=\"site-nav-key-views site-nav-local-context\" aria-label=\"ADRs\">"
            + "<a href=\"0001-a.html\">ADR 1</a></div>\n"
            + "</nav>\n"
            + "<script>NAV_TOGGLE()</script>\n"
            + "<div class=\"breadcrumb\"><a href=\"../index.html\">Home</a></div>\n"
            + "<main id=\"main-content\"><p>Body</p></main>\n";

        var nav = SpaDelivery.ExtractNavMarkup(page);

        Assert.NotNull(nav);
        Assert.StartsWith("<nav class=\"site-nav\"", nav);
        Assert.EndsWith("</nav>\n", nav);
        Assert.Contains("site-nav-local-context", nav);
        Assert.Contains("aria-label=\"ADRs\"", nav);
        Assert.DoesNotContain("NAV_TOGGLE", nav);
        Assert.DoesNotContain("<script", nav);
    }

    [Fact]
    public void ExtractNavMarkup_IsNull_WhenThePageCarriesNoSiteNav()
    {
        Assert.Null(SpaDelivery.ExtractNavMarkup("<main id=\"main-content\">no nav</main>"));
    }

    [Fact]
    public void ExtractMetaDescription_RecoversAndDecodes_OrReturnsNullWhenAbsent()
    {
        var page = "<head><title>T</title>\n"
            + "<meta name=\"description\" content=\"Docs &amp; specs for &quot;SpecScribe&quot;\">\n"
            + "<meta property=\"og:description\" content=\"ignored\">\n</head>";

        Assert.Equal("Docs & specs for \"SpecScribe\"", SpaDelivery.ExtractMetaDescription(page));
        Assert.Null(SpaDelivery.ExtractMetaDescription("<head><title>T</title></head>"));
    }

    /// <summary>The strip-or-nonce declaration (Story 22.2 AC #5). Both kinds that exist in SpecScribe output are
    /// pinned here: the inert <c>application/json</c> islands (the sunburst/hierarchy/impact-map payloads), and an
    /// EXECUTABLE bare <c>&lt;script&gt;</c>. The executable case is not hypothetical — Story 20.5's
    /// anti-flash handshake (<c>HierarchyExplorer.BootScript</c>) is emitted on the CHROME seam between the
    /// breadcrumb and <c>&lt;main&gt;</c>, which is inside the captured slice, so any captured page that gains a
    /// hierarchy host (Stories 20.7/20.9) ships it into the IR. The webview's own <c>JsonDataIsland</c> regex
    /// matches only the first kind, which is exactly why the IR declares both rather than leaving each consumer
    /// to recognize them.</summary>
    [Fact]
    public void ExtractScriptIslands_SeparatesInertDataFromExecutableScript()
    {
        var region = "<nav class=\"site-nav\"></nav>\n"
            + "<div class=\"breadcrumb\"></div>\n"
            + "<script>(function(){var r=document.documentElement;r.setAttribute('data-ss-hierarchy-boot','1');})();</script>\n"
            + "<main id=\"main-content\">\n"
            + "<script type=\"application/json\" id=\"sunburst-explorer-data\">{}</script>\n"
            + "<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"dashboard-hierarchy-data\">{}</script>\n"
            + "<script type=\"module\" id=\"mermaid-init\">import 'x';</script>\n"
            + "</main>";

        var islands = SpaDelivery.ExtractScriptIslands(region);

        Assert.Equal(4, islands.Count);
        Assert.Equal(new (string?, string)[]
        {
            (null, SpaDelivery.ExecutableScriptKind),
            ("sunburst-explorer-data", SpaDelivery.DataIslandKind),
            ("dashboard-hierarchy-data", SpaDelivery.DataIslandKind),
            ("mermaid-init", SpaDelivery.ExecutableScriptKind),
        }, islands.Select(i => (i.Id, i.Kind)).ToArray());
    }

    [Fact]
    public void ExtractScriptIslands_IsEmpty_ForAScriptFreeRegion()
    {
        Assert.Empty(SpaDelivery.ExtractScriptIslands("<main id=\"main-content\"><p>plain</p></main>"));
    }

    /// <summary>Story 22.2 AC #6: the hash is a pure function of the region's bytes — same content, same hash,
    /// on any run and any machine (NFR9); one byte different, different hash. No clock, no RNG, no path.</summary>
    [Fact]
    public void ContentHash_IsDeterministicForTheSameContent_AndMovesWhenTheContentDoes()
    {
        const string region = "<main id=\"main-content\"><p>Stable</p></main>";

        Assert.Equal(SpaDelivery.ContentHash(region), SpaDelivery.ContentHash(region));
        // string.Copy is obsolete (SYSLIB0050, code review) — string.Concat forces the same distinct,
        // non-interned reference without a deprecated API call.
        Assert.Equal(SpaDelivery.ContentHash(region), SpaDelivery.ContentHash(string.Concat(region)));
        Assert.NotEqual(SpaDelivery.ContentHash(region), SpaDelivery.ContentHash(region + " "));
        Assert.Matches("^[0-9a-f]{16}$", SpaDelivery.ContentHash(region));
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
