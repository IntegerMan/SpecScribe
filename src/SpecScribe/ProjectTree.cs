namespace SpecScribe;

/// <summary>One node in the project/artifact structure tree. <see cref="Label"/> is the segment name shown in
/// the row (a directory name or a file name — never the full path); collapsed single-child directory chains
/// join their segments with <c>" / "</c> into one label. <see cref="OutputHref"/> is the <b>output-relative</b>
/// link target when this node maps to a generated page (leaf files, and any directory that has an index page);
/// <c>null</c> means a non-navigable node — a pure directory container, or a source file with no generated page
/// — which the renderer emits as plain text, never a broken link (Story 1.1). <see cref="IsDirectory"/> drives
/// branch/leaf styling and whether the row renders as a <c>&lt;details&gt;</c> branch or a <c>&lt;li&gt;</c> leaf.</summary>
public sealed record TreeNode(string Label, string? OutputHref, bool IsDirectory, IReadOnlyList<TreeNode> Children);

/// <summary>A pure, source-artifact-derived tree of a project's directory/artifact structure. Mirrors the shape
/// of <see cref="WorkInventory"/>/<see cref="ArtifactCoverage"/>: a pure <see cref="Build"/> over already-gathered
/// inputs (NO disk access — every nesting/ordering/collapse rule is unit-testable without a repo), an
/// <see cref="Empty"/> singleton, and an <see cref="IsEmpty"/> flag callers use to omit the surface entirely
/// rather than render an empty tree. Never throws — the generator degrades any failure to <see cref="Empty"/> so
/// the surface omits and generation still succeeds (AD-4 / NFR2).
/// <para>The node set is the <b>source-artifact</b> file set — the <c>_bmad-output</c> <c>*.md</c> tree the
/// generator already enumerates and renders into pages — so every navigable leaf has a known generated page and
/// links can never dangle. Epic 4 (<c>FR1</c>/adapter contract) and Epic 7 (<c>FR15</c> code-file browsing) will
/// later broaden the input file set; the renderer and the page stay put — only the file set grows. [Story 3.4]</para></summary>
public sealed class ProjectTree
{
    public required IReadOnlyList<TreeNode> Roots { get; init; }

    /// <summary>Number of file leaves in the tree — the "N files" in the surface headline.</summary>
    public int FileCount { get; init; }

    /// <summary>Number of real directories represented (collapsed chains still count each directory), for the
    /// "across M directories" half of the headline.</summary>
    public int DirectoryCount { get; init; }

    /// <summary>True when there is no structure to show, so the caller omits the whole surface (Story 1.1
    /// graceful omission) rather than render an empty tree.</summary>
    public bool IsEmpty => Roots.Count == 0;

    public static readonly ProjectTree Empty = new() { Roots = Array.Empty<TreeNode>(), FileCount = 0, DirectoryCount = 0 };

    /// <summary>A mutable directory node used only while assembling the trie; converted to immutable
    /// <see cref="TreeNode"/>s at the end.</summary>
    private sealed class Dir
    {
        public required string Name { get; init; }
        // Keyed Ordinal (exact) so two segments that differ only in case stay distinct trie slots; ordering and
        // the output href lookup handle case-insensitivity separately, matching the project's path discipline.
        public Dictionary<string, Dir> Dirs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string?> Files { get; } = new(StringComparer.Ordinal); // file name -> output href (or null)
    }

    /// <summary>Builds the tree over already-resolved inputs — NO disk access. Each source-relative path (already
    /// <c>_bmad-output</c>-relative, e.g. <c>planning-artifacts/prds/prd-…/prd.md</c>) is normalized through
    /// <see cref="PathUtil.NormalizeSlashes"/> and split on <c>/</c> into a nested directory→children structure:
    /// the final segment is a file leaf, every prior segment a directory branch. A file's output href is looked
    /// up in <paramref name="outputHrefBySourcePath"/> (compared normalized + <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// for Windows/Git-Bash path-case parity); a file with no entry gets <c>OutputHref = null</c> — routing is
    /// best-effort, never a broken link (AC #2). Ordering is deterministic (directories before files, then
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> within each group) so the output is byte-stable for the
    /// fidelity/snapshot tests. Never throws (NFR2).</summary>
    /// <param name="sourceRelativePaths">Every source-relative markdown path discovered in the tree.</param>
    /// <param name="outputHrefBySourcePath">Output-relative link targets, keyed by (normalizable) source path.</param>
    public static ProjectTree Build(
        IReadOnlyList<string> sourceRelativePaths,
        IReadOnlyDictionary<string, string> outputHrefBySourcePath)
    {
        // Normalize the href map once so lookups are slash- and case-insensitive regardless of how the caller
        // keyed it (Windows back-slashes / mixed case).
        var hrefLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in outputHrefBySourcePath)
        {
            hrefLookup[PathUtil.NormalizeSlashes(key)] = value;
        }

        var root = new Dir { Name = string.Empty };
        foreach (var raw in sourceRelativePaths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var norm = PathUtil.NormalizeSlashes(raw);
            var segments = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) continue;

            var cur = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var seg = segments[i];
                if (!cur.Dirs.TryGetValue(seg, out var next))
                {
                    next = new Dir { Name = seg };
                    cur.Dirs[seg] = next;
                }
                cur = next;
            }

            var fileName = segments[^1];
            // Defensive: a real filesystem can't have a directory and a file with the same name at one level;
            // if the inputs somehow imply it, keep the directory and drop the file rather than throw (NFR2).
            if (cur.Dirs.ContainsKey(fileName)) continue;
            cur.Files[fileName] = hrefLookup.TryGetValue(norm, out var h) ? h : null;
        }

        var fileCount = 0;
        var dirCount = 0;
        Count(root, ref fileCount, ref dirCount);

        var roots = BuildChildren(root);
        return roots.Count == 0
            ? Empty
            : new ProjectTree { Roots = roots, FileCount = fileCount, DirectoryCount = dirCount };
    }

    /// <summary>Tallies real files and directories over the raw trie (the virtual root is not itself a
    /// directory), so the headline counts reflect true structure even where the renderer collapses chains.</summary>
    private static void Count(Dir dir, ref int files, ref int dirs)
    {
        files += dir.Files.Count;
        foreach (var sub in dir.Dirs.Values)
        {
            dirs++;
            Count(sub, ref files, ref dirs);
        }
    }

    /// <summary>Converts a directory's children to ordered immutable nodes: directories first, then files, each
    /// group alphabetical by <see cref="StringComparer.OrdinalIgnoreCase"/> (culture-independent, byte-stable).</summary>
    private static IReadOnlyList<TreeNode> BuildChildren(Dir parent)
    {
        var nodes = new List<TreeNode>(parent.Dirs.Count + parent.Files.Count);
        foreach (var dir in parent.Dirs.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(BuildDir(dir));
        }
        foreach (var file in parent.Files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(new TreeNode(file.Key, file.Value, IsDirectory: false, Array.Empty<TreeNode>()));
        }
        return nodes;
    }

    /// <summary>Builds one directory branch, collapsing a single-child directory chain into one joined label so
    /// depth reads as real structure, not filesystem noise (UX-DR19). A directory is collapsed into its child
    /// ONLY when it has exactly one child and that child is itself a directory; a directory with ≥2 children, or
    /// whose single child is a file, is not collapsed. Directory nodes are never navigable in v1 (no per-directory
    /// index page in the source-artifact set), so their href is always <c>null</c>.</summary>
    private static TreeNode BuildDir(Dir dir)
    {
        var label = dir.Name;
        var cur = dir;

        // Bounded loop (a finite trie can't loop forever; the guard just caps pathological depth defensively
        // without throwing, per NFR2).
        var guard = 0;
        while (cur.Files.Count == 0 && cur.Dirs.Count == 1 && guard++ < 4096)
        {
            var child = cur.Dirs.Values.First();
            label = $"{label} / {child.Name}";
            cur = child;
        }

        return new TreeNode(label, OutputHref: null, IsDirectory: true, BuildChildren(cur));
    }
}
