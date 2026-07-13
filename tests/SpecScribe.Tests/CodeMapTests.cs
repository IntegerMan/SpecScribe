using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>The code-map builder nests repo-relative source paths into a directory/file tree sized by lines of
/// code, rolls line counts up each directory, orders deterministically (directories before files, alphabetical
/// within each group), collapses single-child directory chains, attaches per-file git metrics when present (null
/// otherwise), and never throws (NFR2). The squarified layout tiles each region without overlap, keeps file area
/// proportional to lines, and is byte-stable. Pure — no disk, mirroring <see cref="WorkInventoryTests"/>. Replaced
/// the retired Story 3.4 ProjectTreeTests. [Story 7.6]</summary>
public class CodeMapTests
{
    private static readonly IReadOnlyDictionary<string, CodeFileMetrics> NoMetrics = new Dictionary<string, CodeFileMetrics>();

    private static CodeMap Build(params (string Path, long Lines)[] files) =>
        CodeMap.Build(files.Select(f => (f.Path, f.Lines)).ToList(), NoMetrics);

    // ---- Model: nesting, ordering, roll-up, collapse, metrics (Task 7.1) ----

    [Fact]
    public void Build_NestsFilesUnderTheirDirectories()
    {
        var map = Build(("src/A.cs", 10), ("src/B.cs", 20));

        var dir = Assert.Single(map.Roots);
        Assert.True(dir.IsDirectory);
        Assert.Equal("src", dir.Label);
        Assert.Equal("src", dir.RepoRelativePath);
        Assert.Equal(new[] { "A.cs", "B.cs" }, dir.Children.Select(c => c.Label).ToArray());
        Assert.Equal(2, map.FileCount);
        Assert.Equal(1, map.DirectoryCount);
    }

    [Fact]
    public void Build_RollsLineCountsUpEachDirectory()
    {
        // A parent directory's Lines is the sum of all descendant file lines.
        var map = CodeMap.Build(
            new[] { ("src/App/Main.cs", 100L), ("src/App/Util.cs", 40L), ("src/Root.cs", 10L) },
            NoMetrics);

        var src = Assert.Single(map.Roots);
        Assert.Equal("src", src.Label);
        Assert.Equal(150, src.Lines); // 100 + 40 + 10
        var app = src.Children.Single(c => c.IsDirectory);
        Assert.Equal("App", app.Label);
        Assert.Equal(140, app.Lines); // 100 + 40
        Assert.Equal(150, map.TotalLines);
    }

    [Fact]
    public void Build_OrdersDirectoriesBeforeFilesThenAlphabetically()
    {
        var map = Build(
            ("readme.md", 1),
            ("about.md", 1),
            ("zeta-dir/inner.cs", 1),
            ("alpha-dir/inner.cs", 1));

        var labels = map.Roots.Select(r => r.Label).ToArray();
        Assert.Equal(new[] { "alpha-dir", "zeta-dir", "about.md", "readme.md" }, labels);
        Assert.True(map.Roots[0].IsDirectory);
        Assert.True(map.Roots[1].IsDirectory);
        Assert.False(map.Roots[2].IsDirectory);
        Assert.False(map.Roots[3].IsDirectory);
    }

    [Fact]
    public void Build_AttachesGitMetricToFileWithOneAndLeavesOthersNull()
    {
        var metrics = new Dictionary<string, CodeFileMetrics>
        {
            ["src/A.cs"] = new CodeFileMetrics(5, 120, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1)),
        };

        var map = CodeMap.Build(new[] { ("src/A.cs", 10L), ("src/B.cs", 20L) }, metrics);

        var dir = Assert.Single(map.Roots);
        var a = dir.Children.Single(c => c.Label == "A.cs");
        var b = dir.Children.Single(c => c.Label == "B.cs");
        Assert.NotNull(a.Metrics);
        Assert.Equal(5, a.Metrics!.Changes);
        Assert.Null(b.Metrics); // per-file graceful degradation (AC #2): a file with no git record has no metric
        Assert.Null(dir.Metrics); // directories never carry per-file metrics
    }

    [Fact]
    public void Build_MetricLookupIsSlashAndCaseInsensitive()
    {
        // git may emit / the caller may key with back-slashes or different case (Windows/Git-Bash parity).
        var metrics = new Dictionary<string, CodeFileMetrics>
        {
            [@"Src\A.cs"] = new CodeFileMetrics(3, 10, null, null),
        };

        var map = CodeMap.Build(new[] { ("src/A.cs", 10L) }, metrics);

        var a = map.Roots.Single().Children.Single();
        Assert.NotNull(a.Metrics);
        Assert.Equal(3, a.Metrics!.Changes);
    }

    [Fact]
    public void Build_CollapsesSingleChildDirectoryChainsIntoOneLabelAndKeepsDeepestPath()
    {
        var map = Build(("src/SpecScribe/Assets/prism.js", 42));

        var branch = Assert.Single(map.Roots);
        Assert.True(branch.IsDirectory);
        Assert.Equal("src / SpecScribe / Assets", branch.Label);
        Assert.Equal("src/SpecScribe/Assets", branch.RepoRelativePath); // deepest collapsed path
        var file = Assert.Single(branch.Children);
        Assert.Equal("prism.js", file.Label);
        Assert.Equal(42, branch.Lines);
        Assert.Equal(1, map.FileCount);
        Assert.Equal(3, map.DirectoryCount); // every real directory still counted for the headline
    }

    [Fact]
    public void Build_DoesNotCollapseDirectoryWithMultipleChildrenOrAFileChild()
    {
        var map = Build(("a/b/leaf.cs", 1), ("a/loose.cs", 1));

        var a = Assert.Single(map.Roots);
        Assert.Equal("a", a.Label); // not joined with "b" — it has two children
        Assert.Equal(2, a.Children.Count);
        var b = a.Children.Single(c => c.IsDirectory);
        Assert.Equal("b", b.Label); // not joined with "leaf.cs" — its single child is a file
        Assert.Equal("leaf.cs", Assert.Single(b.Children).Label);
    }

    [Fact]
    public void Build_EmptyInputYieldsEmptySingleton()
    {
        var map = CodeMap.Build(Array.Empty<(string, long)>(), NoMetrics);
        Assert.True(map.IsEmpty);
        Assert.Empty(map.Roots);
        Assert.Equal(0, map.FileCount);
        Assert.Equal(0, map.TotalLines);
        Assert.Same(CodeMap.Empty, map);
    }

    [Fact]
    public void Build_OddOrDegeneratePathsNeverThrow()
    {
        var map = CodeMap.Build(
            new[]
            {
                (@"windows\style\path.cs", 3L),
                ("/leading-slash.cs", 1L),
                ("trailing-slash-dir//", 1L),
                ("   ", 1L),
                ("", 1L),
                ("bare.cs", 2L),
                ("neg.cs", -5L), // negative line count clamped to 0, never throws
            },
            NoMetrics);

        Assert.False(map.IsEmpty);
        Assert.Contains(map.Roots, r => r is { IsDirectory: true, Label: "windows / style" });
        Assert.Contains(map.Roots, r => r is { IsDirectory: false, Label: "bare.cs" });
        Assert.Contains(map.Roots, r => r is { IsDirectory: false, Label: "leading-slash.cs" });
        Assert.Equal(0, map.Roots.Single(r => r.Label == "neg.cs").Lines);
    }

    [Fact]
    public void Build_DuplicatePathsAreDedupedToOneLeaf()
    {
        var map = Build(("src/a.cs", 10), ("src/a.cs", 10));

        var dir = Assert.Single(map.Roots);
        Assert.Single(dir.Children);
        Assert.Equal(1, map.FileCount);
    }

    [Fact]
    public void Files_FlattensToLeavesInTreeOrder()
    {
        var map = Build(("src/z.cs", 1), ("src/a.cs", 1), ("root.cs", 1));

        var files = map.Files();
        Assert.Equal(3, files.Count);
        Assert.All(files, f => Assert.False(f.IsDirectory));
        // src/ (dir, first) then its files a.cs, z.cs, then the root loose file.
        Assert.Equal(new[] { "a.cs", "z.cs", "root.cs" }, files.Select(f => f.Label).ToArray());
    }

    // ---- Squarified layout (Task 7.2) ----

    [Fact]
    public void Layout_FileRectAreaIsProportionalToLines()
    {
        // A flat root (no directories) tiles the whole viewBox, so each file rect's area is exactly proportional
        // to its line count (no header/gutter space is reserved at the root level).
        var map = Build(("a.cs", 100), ("b.cs", 200), ("c.cs", 300), ("d.cs", 400));
        var layout = map.Layout(1000, 640);

        Assert.Equal(4, layout.Count);
        var totalArea = layout.Sum(r => r.W * r.H);
        Assert.Equal(1000.0 * 640.0, totalArea, 1); // the whole viewBox is tiled

        double AreaOf(string label) => layout.Single(r => r.Node.Label == label) is { } r ? r.W * r.H : 0;
        // b (200 lines) has twice a's area (100 lines); d (400) has four times a's.
        Assert.Equal(2.0, AreaOf("b.cs") / AreaOf("a.cs"), 2);
        Assert.Equal(4.0, AreaOf("d.cs") / AreaOf("a.cs"), 2);
    }

    [Fact]
    public void Layout_FileRectsTileWithoutOverlapAndWithinViewBox()
    {
        var map = Build(
            ("src/App/Main.cs", 120),
            ("src/App/Util.cs", 40),
            ("src/Core/Engine.cs", 200),
            ("docs/readme.cs", 30));
        var layout = map.Layout(1000, 640);

        var files = layout.Where(r => !r.Node.IsDirectory).ToList();
        Assert.NotEmpty(files);

        const double eps = 0.01;
        foreach (var r in files)
        {
            Assert.True(r.X >= -eps && r.Y >= -eps, "rect starts within the viewBox");
            Assert.True(r.X + r.W <= 1000 + eps && r.Y + r.H <= 640 + eps, "rect ends within the viewBox");
            Assert.True(r.W > 0 && r.H > 0, "rect has positive area");
        }

        // No two FILE rects overlap (directory rects contain their children by design and are excluded).
        for (var i = 0; i < files.Count; i++)
        {
            for (var j = i + 1; j < files.Count; j++)
            {
                var a = files[i];
                var b = files[j];
                var overlap = a.X < b.X + b.W - eps && b.X < a.X + a.W - eps &&
                              a.Y < b.Y + b.H - eps && b.Y < a.Y + a.H - eps;
                Assert.False(overlap, $"{a.Node.Label} overlaps {b.Node.Label}");
            }
        }
    }

    [Fact]
    public void Layout_IsDeterministicAcrossRuns()
    {
        var map = Build(
            ("src/A.cs", 130),
            ("src/nested/B.cs", 70),
            ("src/nested/C.cs", 55),
            ("other/D.cs", 90));

        var first = map.Layout();
        var second = map.Layout();

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Node.RepoRelativePath, second[i].Node.RepoRelativePath);
            Assert.Equal(first[i].X, second[i].X, 6);
            Assert.Equal(first[i].Y, second[i].Y, 6);
            Assert.Equal(first[i].W, second[i].W, 6);
            Assert.Equal(first[i].H, second[i].H, 6);
        }
    }

    [Fact]
    public void Layout_ZeroLineSingleFileAndDeepNestingNeverThrow()
    {
        // Zero-line files still get a minimal, positive slice (visible + focusable).
        var zero = Build(("a.cs", 0), ("b.cs", 0));
        Assert.All(zero.Layout(), r => Assert.True(r.W > 0 && r.H > 0));

        // Single file fills the whole viewBox.
        var single = Build(("only.cs", 50));
        var rect = Assert.Single(single.Layout());
        Assert.Equal(1000.0 * 640.0, rect.W * rect.H, 1);

        // A very deep chain never throws or loops.
        var deepPath = string.Join('/', Enumerable.Range(0, 40).Select(i => $"d{i}")) + "/leaf.cs";
        var deep = Build((deepPath, 10));
        Assert.NotEmpty(deep.Layout());
    }

    [Fact]
    public void Layout_EmptyMapYieldsNoRects()
    {
        Assert.Empty(CodeMap.Empty.Layout());
    }

    [Fact]
    public void Layout_TinyDirectoryStillEmitsRectsForEveryDescendantFile()
    {
        // Review fix: a directory allotted a sub-pixel-header region (dwarfed by a huge sibling file) used to
        // drop its ENTIRE file subtree from the layout, silently under-representing "each source file... as a
        // rectangle" (AC #1). It must now still tile its children directly into its full rect (no header/gutter)
        // rather than vanish outright.
        var files = new List<(string, long)> { ("huge/Big.cs", 999_990) };
        for (var i = 0; i < 5; i++) files.Add(($"tiny/Small{i}.cs", 1));

        var map = CodeMap.Build(files, NoMetrics);
        var layout = map.Layout();

        var tinyFileRects = layout.Where(r => !r.Node.IsDirectory && r.Node.RepoRelativePath.StartsWith("tiny/")).ToList();
        Assert.Equal(5, tinyFileRects.Count);
    }
}
