using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>The project-tree builder nests source-relative paths into a directory/file tree, orders it
/// deterministically (directories before files, alphabetical within each group), collapses single-child
/// directory chains, links only files with a known generated page, and never throws (NFR2). Pure — no disk,
/// mirroring <see cref="WorkInventoryTests"/>. [Story 3.4]</summary>
public class ProjectTreeTests
{
    private static readonly IReadOnlyDictionary<string, string> NoHrefs = new Dictionary<string, string>();

    [Fact]
    public void Build_NestsFilesUnderTheirDirectories()
    {
        var tree = ProjectTree.Build(
            new[] { "planning-artifacts/epics.md", "planning-artifacts/notes.md" },
            NoHrefs);

        // One top-level directory branch holding two file leaves.
        var dir = Assert.Single(tree.Roots);
        Assert.True(dir.IsDirectory);
        Assert.Equal("planning-artifacts", dir.Label);
        Assert.All(dir.Children, c => Assert.False(c.IsDirectory));
        Assert.Equal(new[] { "epics.md", "notes.md" }, dir.Children.Select(c => c.Label).ToArray());
        Assert.Equal(2, tree.FileCount);
        Assert.Equal(1, tree.DirectoryCount);
    }

    [Fact]
    public void Build_OrdersDirectoriesBeforeFilesThenAlphabetically()
    {
        // At the root level: a directory ("zeta-dir") must sort before loose files even though 'z' > 'a'; and
        // files/dirs are each alphabetical (OrdinalIgnoreCase). [Subtask 1.3]
        var tree = ProjectTree.Build(
            new[]
            {
                "readme.md",
                "about.md",
                "zeta-dir/inner.md",
                "alpha-dir/inner.md",
            },
            NoHrefs);

        var labels = tree.Roots.Select(r => r.Label).ToArray();
        // Both directories first (alpha), then both files (alpha).
        Assert.Equal(new[] { "alpha-dir", "zeta-dir", "about.md", "readme.md" }, labels);
        Assert.True(tree.Roots[0].IsDirectory);
        Assert.True(tree.Roots[1].IsDirectory);
        Assert.False(tree.Roots[2].IsDirectory);
        Assert.False(tree.Roots[3].IsDirectory);
    }

    [Fact]
    public void Build_LinksFilesWithAKnownPageAndLeavesOthersNull()
    {
        var hrefs = new Dictionary<string, string>
        {
            ["planning-artifacts/epics.md"] = "epics.html",
        };

        var tree = ProjectTree.Build(
            new[] { "planning-artifacts/epics.md", "planning-artifacts/orphan.md" },
            hrefs);

        var dir = Assert.Single(tree.Roots);
        var epics = dir.Children.Single(c => c.Label == "epics.md");
        var orphan = dir.Children.Single(c => c.Label == "orphan.md");
        Assert.Equal("epics.html", epics.OutputHref); // mapped file gets its page
        Assert.Null(orphan.OutputHref);               // unmapped file is never a broken link (AC #2)
    }

    [Fact]
    public void Build_HrefLookupIsSlashAndCaseInsensitive()
    {
        // The caller may key the map with back-slashes or different case (Windows/Git-Bash parity).
        var hrefs = new Dictionary<string, string>
        {
            [@"Planning-Artifacts\Epics.md"] = "epics.html",
        };

        var tree = ProjectTree.Build(new[] { "planning-artifacts/epics.md" }, hrefs);

        var epics = tree.Roots.Single().Children.Single();
        Assert.Equal("epics.html", epics.OutputHref);
    }

    [Fact]
    public void Build_CollapsesSingleChildDirectoryChainsIntoOneLabel()
    {
        // A pure single-child chain becomes one joined branch label rather than three nested carets. [Subtask 1.4]
        var tree = ProjectTree.Build(
            new[] { "planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md" },
            NoHrefs);

        var branch = Assert.Single(tree.Roots);
        Assert.True(branch.IsDirectory);
        Assert.Equal("planning-artifacts / prds / prd-SpecScribe-2026-07-05", branch.Label);
        // The chain collapses to ONE branch, but the leaf file still hangs off it.
        var file = Assert.Single(branch.Children);
        Assert.Equal("prd.md", file.Label);
        // Every real directory is still counted for the headline even though they render as one row.
        Assert.Equal(1, tree.FileCount);
        Assert.Equal(3, tree.DirectoryCount);
    }

    [Fact]
    public void Build_DoesNotCollapseDirectoryWithMultipleChildrenOrAFileChild()
    {
        // "a" has two children (a dir and a file) → NOT collapsed. "a/b" has one FILE child → NOT collapsed.
        var tree = ProjectTree.Build(
            new[] { "a/b/leaf.md", "a/loose.md" },
            NoHrefs);

        var a = Assert.Single(tree.Roots);
        Assert.Equal("a", a.Label); // not joined with "b" — it has two children
        Assert.Equal(2, a.Children.Count);
        var b = a.Children.Single(c => c.IsDirectory);
        Assert.Equal("b", b.Label);  // not joined with "leaf.md" — its single child is a file
        Assert.Equal("leaf.md", Assert.Single(b.Children).Label);
    }

    [Fact]
    public void Build_EmptyInputYieldsEmptySingleton()
    {
        var tree = ProjectTree.Build(Array.Empty<string>(), NoHrefs);
        Assert.True(tree.IsEmpty);
        Assert.Empty(tree.Roots);
        Assert.Equal(0, tree.FileCount);
        Assert.Equal(0, tree.DirectoryCount);
    }

    [Fact]
    public void Build_OddOrDegeneratePathsNeverThrow()
    {
        // Backslashes, leading/trailing/duplicate slashes, blank entries, a bare filename — all handled, no throw.
        var tree = ProjectTree.Build(
            new[]
            {
                @"windows\style\path.md",
                "/leading-slash.md",
                "trailing-slash-dir//",
                "   ",
                "",
                "bare.md",
            },
            NoHrefs);

        Assert.False(tree.IsEmpty);
        // The backslash path nests, the bare file and leading-slash file are top-level leaves.
        Assert.Contains(tree.Roots, r => r is { IsDirectory: true, Label: "windows / style" });
        Assert.Contains(tree.Roots, r => r is { IsDirectory: false, Label: "bare.md" });
        Assert.Contains(tree.Roots, r => r is { IsDirectory: false, Label: "leading-slash.md" });
    }

    [Fact]
    public void Build_DuplicatePathsAreDedupedToOneLeaf()
    {
        var tree = ProjectTree.Build(
            new[] { "docs/a.md", "docs/a.md" },
            NoHrefs);

        var dir = Assert.Single(tree.Roots);
        Assert.Single(dir.Children);
        Assert.Equal(1, tree.FileCount);
    }
}
