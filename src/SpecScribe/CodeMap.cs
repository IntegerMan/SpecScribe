namespace SpecScribe;

/// <summary>One node in the source-code treemap — a directory container or a source file. <see cref="Label"/> is
/// the segment name shown on the rectangle (a directory name or a file name — never the full path); a collapsed
/// single-child directory chain joins its segments with <c>" / "</c> into one label (exactly as the retired
/// <c>ProjectTree.BuildDir</c> did). <see cref="RepoRelativePath"/> is the FULL repo-relative path
/// (forward-slash) — for a file it is the treemap-cell identity + the key the guarded code-page resolver and the
/// git-metric join use; for a directory it is the deepest directory path after chain collapse.
/// <see cref="Lines"/> is the treemap SIZE key: a file's own line count, a directory's rolled-up Σ of descendant
/// file lines. <see cref="Metrics"/> carries the per-file git-derived colorize dimensions — <c>null</c> for every
/// directory and for any file with no git record (outside the analyzed window, or <c>--deep-git</c> off), which the
/// renderer draws with a neutral fill (per-file graceful degradation, AC #2). <see cref="Category"/> carries the
/// file-type classification (<see cref="CodeFileType.Classify"/>) — ALWAYS populated for a file (pure function of
/// its path, no git dependency) and always <c>null</c> for a directory; the load-bearing difference from
/// <see cref="Metrics"/> is that this field never degrades to "unavailable". [Story 7.6; Story 7.9]</summary>
public sealed record CodeMapNode(
    string Label,
    string RepoRelativePath,
    bool IsDirectory,
    long Lines,
    CodeFileMetrics? Metrics,
    CodeFileCategory? Category,
    IReadOnlyList<CodeMapNode> Children);

/// <summary>One bounded file-type category a source file classifies into (<see cref="CodeFileType.Classify"/>):
/// <see cref="Key"/> is the CSS class suffix / <c>data-filetype</c> attribute value (e.g. <c>"csharp"</c>),
/// <see cref="Label"/> the human-readable name shown in the legend, tooltip, and text table (e.g. <c>"C#"</c>).
/// [Story 7.9]</summary>
public sealed record CodeFileCategory(string Key, string Label);

/// <summary>A pure, bounded, categorical (NOT sequential) classifier mapping a repo-relative source path to a
/// small fixed set of file-type categories by extension — the Code Map's "File type" colorize dimension (Story
/// 7.9, AC #1/#2). Deliberately NOT a reuse of <see cref="CodeFileTemplater"/>'s private <c>LanguageClass</c>
/// (a fine-grained, ~25-bucket Prism-grammar map living in the wrong layer, a templater rather than the pure
/// model) — this classifier is intentionally coarse and bounded so the discrete palette/legend never grows
/// unbounded. Extension groupings are kept consistent with <c>LanguageClass</c>'s families (same <c>.ts</c>/
/// <c>.tsx</c> → script, same <c>.json</c>/<c>.yaml</c>/<c>.toml</c> → config) so a file never reads as one
/// language on its code page and a different family on the map, without literally sharing code. Every
/// unrecognized or extensionless path — including a pathological empty/malformed one — falls into
/// <see cref="Other"/> rather than inventing a new category (AC #2). Pure, case-insensitive, never throws
/// (NFR2), mirroring <see cref="CodeMap.IsSpecDevPath"/>/<see cref="CodeMap.IsTestPath"/>'s discipline.</summary>
public static class CodeFileType
{
    public static readonly CodeFileCategory CSharp = new("csharp", "C#");
    public static readonly CodeFileCategory Python = new("python", "Python");
    public static readonly CodeFileCategory Script = new("script", "TypeScript/JavaScript");
    public static readonly CodeFileCategory Styles = new("styles", "Styles");
    public static readonly CodeFileCategory Markup = new("markup", "Markup & Docs");
    public static readonly CodeFileCategory Config = new("config", "Config & Data");
    // Every OTHER recognized general-purpose/scripting language this repo (or a typical polyglot repo) is likely
    // to contain, grouped rather than given one bucket each — the bounded-palette discipline (AC #2) means real
    // language variety competes for a fixed small hue budget, so anything past the languages with genuinely
    // heavy representation here (C#, Python, TS/JS) shares one "still code, just not one of the above" bucket
    // instead of either vanishing into the unrecognized "Other" catch-all or blowing the category count open.
    public static readonly CodeFileCategory OtherLanguages = new("other-lang", "Other Languages");
    public static readonly CodeFileCategory Other = new("other", "Other");

    /// <summary>The fixed, ordered category set — this order drives the discrete legend's swatch ordering
    /// (real categories before the "Other" catch-all) so the legend reads the same way across every generation
    /// run regardless of which files happen to be present.</summary>
    public static readonly IReadOnlyList<CodeFileCategory> AllCategories =
        new[] { CSharp, Python, Script, Styles, Markup, Config, OtherLanguages, Other };

    /// <summary>Classifies a repo-relative source path into its bounded file-type category by extension.
    /// Normalizes via <see cref="PathUtil.NormalizeSlashes"/>; case-insensitive extension matching; an
    /// extensionless name, an empty/whitespace path, or any extension outside the fixed groupings below all
    /// fall to <see cref="Other"/>. Never throws.</summary>
    public static CodeFileCategory Classify(string repoRelativePath)
    {
        if (string.IsNullOrWhiteSpace(repoRelativePath)) return Other;
        var norm = PathUtil.NormalizeSlashes(repoRelativePath);
        var name = norm[(norm.LastIndexOf('/') + 1)..];

        // Name-based special case, mirroring CodeFileTemplater.LanguageClass's own Dockerfile handling (no real
        // extension to key off, but a real, common file kind that shouldn't fall to the generic Other bucket).
        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase))
        {
            return Markup;
        }

        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1) return Other;

        return name[(dot + 1)..].ToLowerInvariant() switch
        {
            "cs" or "csx" => CSharp,
            "py" or "pyi" or "pyw" => Python,
            "ts" or "tsx" or "js" or "jsx" or "mjs" or "cjs" => Script,
            "css" or "scss" => Styles,
            "html" or "htm" or "md" or "markdown" or "xml" or "svg" or "xaml"
                or "csproj" or "props" or "targets" => Markup,
            "json" or "json5" or "yaml" or "yml" or "toml" or "ini" or "cfg" or "editorconfig" => Config,
            "sh" or "bash" or "zsh" or "ps1" or "psm1" or "psd1" or "rb" or "go" or "rs"
                or "java" or "kt" or "kts" or "php" or "c" or "h" or "cpp" or "cc" or "cxx"
                or "hpp" or "hxx" or "sql" => OtherLanguages,
            _ => Other,
        };
    }
}

/// <summary>One positioned rectangle in the squarified treemap layout: the <see cref="Node"/> it draws, its
/// position/size within the fixed viewBox (<see cref="X"/>/<see cref="Y"/>/<see cref="W"/>/<see cref="H"/>), and
/// its nesting <see cref="Depth"/> (0 = a root-level node). Directory rects contain their children's rects (drawn
/// as unlabeled group boundaries — no directory carries a text label at any depth); file rects are the leaves.
/// Positions are pure functions of the (already deterministic) node order, so the layout is byte-stable for
/// snapshot tests. [Story 7.6]</summary>
public sealed record TreemapRect(CodeMapNode Node, double X, double Y, double W, double H, int Depth);

/// <summary>One precomputed code-map filter combination the page bakes ahead of time (Story 7.6 round 2's "exclude
/// spec-driven development directories" / "exclude tests" checkboxes). Rather than relaying out the treemap client
/// side — which would need a second, JS-ported copy of the squarified algorithm and risks the two implementations
/// silently diverging — the generator computes all four combinations once in C# (the single source of truth for
/// tiling) and the page swaps between four pre-rendered panels via pure CSS, so the toggle needs no JavaScript at
/// all and every combination is fully correct — including re-tiled, gap-free layouts — with JS off.
/// <see cref="Key"/> is the CSS class/id suffix distinguishing the four panels.</summary>
public sealed record CodeMapVariant(string Key, bool ExcludesSpecDev, bool ExcludesTests, CodeMap Map, IReadOnlyList<TreemapRect> Layout);

/// <summary>A pure, source-code-derived treemap model of a repository's files sized by lines of code and nested by
/// directory. Mirrors the shape every pure model in this repo uses (<see cref="WorkInventory"/>,
/// <see cref="ArtifactCoverage"/>, the retired <c>ProjectTree</c> it replaces): a pure <see cref="Build"/> over
/// already-gathered inputs (NO disk access — every nesting/roll-up/collapse rule is unit-testable without a repo),
/// an <see cref="Empty"/> singleton, and an <see cref="IsEmpty"/> flag callers use to omit the whole surface. Never
/// throws — the generator degrades any failure to <see cref="Empty"/> so the code-map omits and generation still
/// succeeds (AD-4 / NFR2).
/// <para>The node set is a <b>source-code walk</b> (repo-relative paths like <c>src/SpecScribe/Foo.cs</c>) — a
/// DIFFERENT path space from the retired artifact tree's <c>_bmad-output</c>-relative <c>*.md</c> set. Git's
/// <c>--numstat</c> emits the same repo-relative paths, so line counts and git metrics join cleanly. The
/// <c>_docs</c> output-href map is NOT the link source here (that was the artifact tree); in-portal code-page links
/// come from a guarded resolver supplied by the caller (dormant until Story 7.1 lands). [Story 7.6]</para></summary>
public sealed class CodeMap
{
    public required IReadOnlyList<CodeMapNode> Roots { get; init; }

    /// <summary>Number of file leaves in the map — the "N files" in the surface headline.</summary>
    public int FileCount { get; init; }

    /// <summary>Number of real directories represented (collapsed chains still count each directory), for the
    /// "across M directories" half of the headline.</summary>
    public int DirectoryCount { get; init; }

    /// <summary>Total lines of code across every file — the treemap's whole-area denominator.</summary>
    public long TotalLines { get; init; }

    /// <summary>True when there is no source code to show, so the caller omits the whole surface (graceful
    /// omission) rather than render an empty treemap.</summary>
    public bool IsEmpty => Roots.Count == 0;

    public static readonly CodeMap Empty = new()
    {
        Roots = Array.Empty<CodeMapNode>(),
        FileCount = 0,
        DirectoryCount = 0,
        TotalLines = 0,
    };

    /// <summary>A mutable directory node used only while assembling the trie; converted to immutable
    /// <see cref="CodeMapNode"/>s at the end.</summary>
    private sealed class Dir
    {
        public required string Name { get; init; }
        // Keyed Ordinal (exact) so two segments differing only in case stay distinct trie slots; ordering and the
        // git-metric lookup handle case-insensitivity separately, matching the project's path discipline.
        public Dictionary<string, Dir> Dirs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, FileLeaf> Files { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>A file leaf captured during the trie build: its full repo-relative path, line count, the
    /// optional per-file git metrics (null when the file has no git record), and its always-populated file-type
    /// category (Story 7.9 — never null, no git dependency).</summary>
    private sealed record FileLeaf(string RepoRelativePath, long Lines, CodeFileMetrics? Metrics, CodeFileCategory Category);

    /// <summary>The path prefixes that hold this repo's own spec-driven-development scaffolding (BMad Method
    /// agents/skills/workflows and their generated planning/implementation artifacts) rather than product source.
    /// Mostly whole top-level directories, but <c>.github/agents/</c> is a narrower sub-path — a mirror of the same
    /// BMad agent definitions for GitHub Copilot, sitting alongside the genuinely-not-spec-dev <c>.github/workflows/</c>
    /// CI config, so only that one subdirectory (not all of <c>.github/</c>) is excluded. Exposed so the code-map's
    /// "exclude spec-driven development directories" filter and its test coverage share one list instead of
    /// duplicating the segment names.</summary>
    private static readonly string[] SpecDevPathPrefixes =
        { ".agents", ".claude", "_bmad", "_bmad-output", ".github/agents" };

    /// <summary>True when a repo-relative path lives under one of the repo's spec-driven-development directories
    /// (<see cref="SpecDevPathPrefixes"/>) — the code-map's "exclude spec-driven development directories" checkbox
    /// filter. Normalizes via <see cref="PathUtil.NormalizeSlashes"/> internally (raw, un-normalized paths are safe
    /// to pass) so callers can filter the flat source-file list the same way <see cref="Build"/> normalizes it.
    /// Ordinal-case-sensitive: these are fixed, dotfile-style directory names this project itself defines, not
    /// user-authored paths that might vary in case. Pure string check, never throws.</summary>
    public static bool IsSpecDevPath(string repoRelativePath)
    {
        var norm = PathUtil.NormalizeSlashes(repoRelativePath);
        foreach (var prefix in SpecDevPathPrefixes)
        {
            if (norm.StartsWith(prefix + "/", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>True when ANY '/'-split segment of a repo-relative path — a directory name or the file's own name —
    /// contains "test" case-insensitively. Matches a whole directory (and therefore every descendant beneath it,
    /// since the filter drops the file from the flat list before <see cref="Build"/> ever nests it) as well as an
    /// individually test-named file inside an otherwise ordinary directory (e.g. <c>src/Foo/HelperTests.cs</c>).
    /// Culture-invariant casing so "Test"/"test"/"TEST" all match identically regardless of host locale.
    /// Normalizes via <see cref="PathUtil.NormalizeSlashes"/> internally. Pure string check, never throws.</summary>
    public static bool IsTestPath(string repoRelativePath)
    {
        var norm = PathUtil.NormalizeSlashes(repoRelativePath);
        foreach (var segment in norm.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Contains("test", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Builds the treemap model over already-resolved inputs — NO disk access. Each source file's
    /// repo-relative path is normalized through <see cref="PathUtil.NormalizeSlashes"/> and split on <c>/</c> into
    /// a nested directory→children trie (the final segment is a file leaf, every prior segment a directory
    /// branch). A file's git metrics are looked up in <paramref name="gitMetrics"/> (both sides normalized +
    /// compared <see cref="StringComparer.OrdinalIgnoreCase"/> for Windows/Git-Bash path-case parity); a file with
    /// no entry gets <c>Metrics = null</c> → neutral fill, never a broken join. Directory <see cref="CodeMapNode.Lines"/>
    /// roll up as Σ of descendant file lines. Ordering is deterministic (directories before files, then
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> within each group) so the output — and therefore the
    /// squarified layout — is byte-stable for the snapshot tests. Never throws (NFR2).</summary>
    /// <param name="sourceFiles">Every source file the generator walked, as (repo-relative path, line count).</param>
    /// <param name="gitMetrics">Per-file git-derived metrics keyed by repo-relative path (empty when unavailable).</param>
    public static CodeMap Build(
        IReadOnlyList<(string RepoRelativePath, long Lines)> sourceFiles,
        IReadOnlyDictionary<string, CodeFileMetrics> gitMetrics)
    {
        // Normalize the git-metric keys once so lookups are slash- and case-insensitive regardless of how git or
        // the caller keyed them.
        var metricLookup = new Dictionary<string, CodeFileMetrics>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in gitMetrics)
        {
            metricLookup[PathUtil.NormalizeSlashes(key)] = value;
        }

        var root = new Dir { Name = string.Empty };
        foreach (var (rawPath, lines) in sourceFiles)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) continue;
            var norm = PathUtil.NormalizeSlashes(rawPath);
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
            // Defensive: a real filesystem can't have a directory and a file with the same name at one level; if
            // the inputs somehow imply it, keep the directory and drop the file rather than throw (NFR2).
            if (cur.Dirs.ContainsKey(fileName)) continue;
            var metric = metricLookup.TryGetValue(norm, out var m) ? m : null;
            // Classify off the already-correctly-split fileName, not norm itself — norm can carry a trailing
            // slash (or other artifact) that would make Classify's own internal name re-derivation see an empty
            // final segment and misclassify a real file to Other.
            var category = CodeFileType.Classify(fileName);
            cur.Files[fileName] = new FileLeaf(norm, Math.Max(lines, 0), metric, category);
        }

        var fileCount = 0;
        var dirCount = 0;
        Count(root, ref fileCount, ref dirCount);

        var roots = BuildChildren(root, string.Empty);
        if (roots.Count == 0) return Empty;

        var totalLines = roots.Sum(r => r.Lines);
        return new CodeMap
        {
            Roots = roots,
            FileCount = fileCount,
            DirectoryCount = dirCount,
            TotalLines = totalLines,
        };
    }

    /// <summary>Builds all four <see cref="CodeMapVariant"/> filter combinations (full / exclude spec-dev dirs /
    /// exclude tests / exclude both) in one pass — the single place the "exclude spec-driven development
    /// directories" and "exclude tests" checkboxes' filtering happens, so the page-level toggle is pure CSS with
    /// no client-side relayout. Each combination gets its own <see cref="Build"/> + <see cref="Layout"/> call (the
    /// SAME deterministic squarified algorithm every other variant uses), so a filtered view genuinely re-tiles to
    /// fill the freed space rather than leaving the excluded files' rectangles as unfilled gaps. Pure, never throws
    /// (an individual combo that filters down to nothing degrades to that variant's own <see cref="Empty"/>, exactly
    /// like the unfiltered page does when there are no source files at all).</summary>
    public static IReadOnlyList<CodeMapVariant> BuildVariants(
        IReadOnlyList<(string RepoRelativePath, long Lines)> sourceFiles,
        IReadOnlyDictionary<string, CodeFileMetrics> gitMetrics)
    {
        var combos = new (string Key, bool ExcludesSpecDev, bool ExcludesTests)[]
        {
            ("full", false, false),
            ("no-spec", true, false),
            ("no-tests", false, true),
            ("no-spec-no-tests", true, true),
        };

        var result = new List<CodeMapVariant>(combos.Length);
        foreach (var (key, excludeSpec, excludeTests) in combos)
        {
            IReadOnlyList<(string RepoRelativePath, long Lines)> filtered = (excludeSpec || excludeTests)
                ? sourceFiles
                    .Where(f => (!excludeSpec || !IsSpecDevPath(f.RepoRelativePath))
                        && (!excludeTests || !IsTestPath(f.RepoRelativePath)))
                    .ToList()
                : sourceFiles;

            var map = Build(filtered, gitMetrics);
            result.Add(new CodeMapVariant(key, excludeSpec, excludeTests, map, map.Layout()));
        }
        return result;
    }

    /// <summary>Tallies real files and directories over the raw trie (the virtual root is not itself a directory),
    /// so the headline counts reflect true structure even where the renderer collapses chains.</summary>
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
    /// group alphabetical by <see cref="StringComparer.OrdinalIgnoreCase"/> (culture-independent, byte-stable).
    /// <paramref name="parentPath"/> is the repo-relative path of the enclosing directory (empty at the root).</summary>
    private static IReadOnlyList<CodeMapNode> BuildChildren(Dir parent, string parentPath)
    {
        var nodes = new List<CodeMapNode>(parent.Dirs.Count + parent.Files.Count);
        foreach (var dir in parent.Dirs.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(BuildDir(dir, parentPath));
        }
        foreach (var file in parent.Files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            var leaf = file.Value;
            nodes.Add(new CodeMapNode(file.Key, leaf.RepoRelativePath, IsDirectory: false, leaf.Lines, leaf.Metrics, leaf.Category, Array.Empty<CodeMapNode>()));
        }
        return nodes;
    }

    /// <summary>Builds one directory branch, collapsing a single-child directory chain into one joined label so
    /// depth reads as real structure, not filesystem noise (UX-DR19) — a directory is collapsed into its child
    /// ONLY when it has exactly one child and that child is itself a directory. The directory's
    /// <see cref="CodeMapNode.RepoRelativePath"/> is the deepest collapsed segment's path; its
    /// <see cref="CodeMapNode.Lines"/> is Σ of its (immutable) children's lines. Never navigable, never carries
    /// git metrics (those are per-file). Bounded loop guard caps pathological depth without throwing (NFR2).</summary>
    private static CodeMapNode BuildDir(Dir dir, string parentPath)
    {
        var label = dir.Name;
        var cur = dir;
        var path = Join(parentPath, dir.Name);

        var guard = 0;
        while (cur.Files.Count == 0 && cur.Dirs.Count == 1 && guard++ < 4096)
        {
            var child = cur.Dirs.Values.First();
            label = $"{label} / {child.Name}";
            path = Join(path, child.Name);
            cur = child;
        }

        var children = BuildChildren(cur, path);
        var lines = children.Sum(c => c.Lines);
        return new CodeMapNode(label, path, IsDirectory: true, lines, Metrics: null, Category: null, children);
    }

    private static string Join(string parentPath, string segment) =>
        parentPath.Length == 0 ? segment : $"{parentPath}/{segment}";

    /// <summary>Flattens the tree to just the file leaves, in deterministic tree order (directories before files,
    /// depth-first). The text-equivalent metrics table and the default-dimension ordering derive from this.</summary>
    public IReadOnlyList<CodeMapNode> Files()
    {
        var files = new List<CodeMapNode>(FileCount);
        void Walk(IReadOnlyList<CodeMapNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.IsDirectory) Walk(n.Children);
                else files.Add(n);
            }
        }
        Walk(Roots);
        return files;
    }

    // ---- Squarified treemap layout (Bruls, Huizing & van Wijk 2000) ----

    /// <summary>The default treemap viewBox width the layout tiles (a fixed coordinate space; the SVG scales to
    /// fit its container). Height is <see cref="DefaultHeight"/>.</summary>
    public const double DefaultWidth = 1000;

    /// <summary>The default treemap viewBox height the layout tiles.</summary>
    public const double DefaultHeight = 640;

    /// <summary>Uniform inset applied on all four sides of a directory rectangle before its children tile — a thin
    /// visible gutter between nested levels so the boundary reads clearly (AC #1). Directories carry no text label
    /// (owner decision), so no extra header band is reserved beyond this gutter; a bigger top-only reservation would
    /// otherwise show through as an unfilled, seemingly "blank" strip (<c>.codemap-dir</c> has no fill).</summary>
    private const double Pad = 3;

    /// <summary>Recursion cap: a pathological directory depth stops nesting rather than risking a deep stack; the
    /// deepest levels simply render as a filled directory rect with no drawn children (NFR2 never-throw).</summary>
    private const int MaxDepth = 32;

    /// <summary>Minimum inner width/height for a directory to reserve its usual gutter before tiling children —
    /// below this the gutter would swamp the region, so children are tiled directly into the directory's full rect
    /// (no inset) instead, guaranteeing every descendant file still gets a (possibly sub-pixel) slice rather than
    /// being dropped from the SVG entirely (AC #1: "each source file... as a rectangle" — legibility degrades
    /// gracefully instead of the subtree vanishing outright).</summary>
    private const double MinTileable = 6;

    /// <summary>Computes the pure, deterministic squarified layout of this map within a fixed viewBox. Emits one
    /// <see cref="TreemapRect"/> per node (directories first, containing their children's rects). Aspect ratios
    /// stay near-1 via the standard squarified algorithm; ordering is a pure function of the (already deterministic)
    /// node order, so the output is byte-stable. Guards zero/negative sizes and caps recursion depth — never throws
    /// (NFR2).</summary>
    public IReadOnlyList<TreemapRect> Layout(double width = DefaultWidth, double height = DefaultHeight)
    {
        var output = new List<TreemapRect>();
        if (IsEmpty || width <= 0 || height <= 0) return output;
        LayoutNodes(Roots, 0, 0, width, height, 0, output);
        return output;
    }

    private static void LayoutNodes(IReadOnlyList<CodeMapNode> nodes, double x, double y, double w, double h, int depth, List<TreemapRect> output)
    {
        if (nodes.Count == 0 || w <= 0 || h <= 0) return;
        // Size by lines; a zero-line file/dir still gets a minimal slice so it stays visible and focusable.
        var items = new List<(CodeMapNode Node, double Size)>(nodes.Count);
        foreach (var n in nodes)
        {
            items.Add((n, Math.Max(n.Lines, 1)));
        }
        Squarify(items, x, y, w, h, depth, output);
    }

    private static void Squarify(List<(CodeMapNode Node, double Size)> items, double x, double y, double w, double h, int depth, List<TreemapRect> output)
    {
        double totalSize = items.Sum(i => i.Size);
        if (totalSize <= 0 || w <= 0 || h <= 0) return;

        var scale = (w * h) / totalSize;
        var scaled = new List<(CodeMapNode Node, double Area)>(items.Count);
        foreach (var (node, size) in items)
        {
            scaled.Add((node, size * scale));
        }

        double rx = x, ry = y, rw = w, rh = h;
        var row = new List<(CodeMapNode Node, double Area)>();
        var index = 0;
        while (index < scaled.Count)
        {
            var shortSide = Math.Min(rw, rh);
            var candidate = scaled[index];
            if (row.Count == 0)
            {
                row.Add(candidate);
                index++;
                continue;
            }

            var currentWorst = Worst(row, shortSide);
            row.Add(candidate);
            var newWorst = Worst(row, shortSide);
            if (newWorst <= currentWorst)
            {
                // Adding the candidate did not worsen the aspect ratio — keep it in the row.
                index++;
            }
            else
            {
                // The candidate worsened the row — remove it, lay the row out, and start a fresh row.
                row.RemoveAt(row.Count - 1);
                LayoutRow(row, ref rx, ref ry, ref rw, ref rh, depth, output);
                row.Clear();
            }
        }

        if (row.Count > 0)
        {
            LayoutRow(row, ref rx, ref ry, ref rw, ref rh, depth, output);
        }
    }

    /// <summary>The worst (largest) aspect ratio in a row laid along the given <paramref name="side"/> length —
    /// the squarified algorithm's cost function (Bruls et al. 2000).</summary>
    private static double Worst(List<(CodeMapNode Node, double Area)> row, double side)
    {
        if (row.Count == 0 || side <= 0) return double.MaxValue;
        double sum = 0, max = 0, min = double.MaxValue;
        foreach (var (_, area) in row)
        {
            sum += area;
            if (area > max) max = area;
            if (area < min) min = area;
        }
        if (sum <= 0 || min <= 0) return double.MaxValue;
        var s2 = sum * sum;
        var side2 = side * side;
        return Math.Max((side2 * max) / s2, s2 / (side2 * min));
    }

    private static void LayoutRow(List<(CodeMapNode Node, double Area)> row, ref double rx, ref double ry, ref double rw, ref double rh, int depth, List<TreemapRect> output)
    {
        double sum = 0;
        foreach (var (_, area) in row) sum += area;
        if (sum <= 0) return;

        var landscape = rw >= rh;            // fill a column on the left when wider than tall, else a row on top
        var shortSide = Math.Min(rw, rh);
        if (shortSide <= 0) return;
        var thickness = sum / shortSide;

        double offset = 0;
        foreach (var (node, area) in row)
        {
            var length = area / thickness;   // extent along the short side
            double cx, cy, cw, ch;
            if (landscape)
            {
                cx = rx;
                cy = ry + offset;
                cw = thickness;
                ch = length;
            }
            else
            {
                cx = rx + offset;
                cy = ry;
                cw = length;
                ch = thickness;
            }
            EmitRect(node, cx, cy, cw, ch, depth, output);
            offset += length;
        }

        if (landscape)
        {
            rx += thickness;
            rw -= thickness;
        }
        else
        {
            ry += thickness;
            rh -= thickness;
        }
    }

    private static void EmitRect(CodeMapNode node, double x, double y, double w, double h, int depth, List<TreemapRect> output)
    {
        output.Add(new TreemapRect(node, x, y, w, h, depth));
        if (!node.IsDirectory || node.Children.Count == 0 || depth >= MaxDepth) return;

        var innerX = x + Pad;
        var innerY = y + Pad;
        var innerW = w - (2 * Pad);
        var innerH = h - (2 * Pad);
        if (innerW < MinTileable || innerH < MinTileable)
        {
            // Too small to reserve the usual gutter — tile children directly into the full parent rect
            // (no inset) instead of dropping the whole subtree, so every descendant still emits a rect.
            if (w <= 0 || h <= 0) return;
            LayoutNodes(node.Children, x, y, w, h, depth + 1, output);
            return;
        }
        LayoutNodes(node.Children, innerX, innerY, innerW, innerH, depth + 1, output);
    }
}
