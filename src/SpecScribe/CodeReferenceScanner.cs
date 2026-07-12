using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One citation target pulled out of a <c>[Source: …]</c> reference: the raw path/href text plus how it
/// should be resolved. Markdown-link hrefs are relative to the CITING artifact's own directory
/// (<c>[Source: [X.cs:76-99](../../src/SpecScribe/X.cs)]</c>); inline/code-span targets are repo-relative
/// (<c>[Source: `src/SpecScribe/X.cs:15-17`]</c>).</summary>
public readonly record struct CodeCitation(string Target, bool RelativeToArtifact);

/// <summary>A source artifact's raw markdown paired with the absolute path of the directory it lives in — the base
/// a markdown-link citation's relative href resolves against.</summary>
public sealed record CitedArtifact(string RawMarkdown, string ArtifactDirFullPath);

/// <summary>Pure discovery of the "referenced code file" set (Story 7.1, FR15). Given the source-artifact corpus,
/// it extracts every <c>[Source: …]</c> citation that points at a real repository file — a code/source file under
/// <c>RepoRoot</c> that is NOT one of the generated <c>_bmad-output/*.md</c> docs — resolves each to a repo-relative
/// path, and returns the deterministic, deduped set. The whole repo is deliberately NOT walked: only the small,
/// purposeful citation-referenced set is rendered (NFR1). The regex extraction (<see cref="ExtractTargets"/>) is
/// disk-free so it can be unit-tested without a temp tree, mirroring <see cref="GitMetrics"/>'s pure parse.</summary>
public static class CodeReferenceScanner
{
    // Markdown-link form inside a [Source: …] citation — the href (resolved relative to the citing artifact's dir).
    // e.g. [Source: [X.cs:76-99](../../src/SpecScribe/X.cs)] -> ../../src/SpecScribe/X.cs
    private static readonly Regex SourceMarkdownLink = new(
        @"\[Source:[^\]]*\]\(\s*(?<href>[^)\s]+)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Inline/code-span (or bare) form — a repo-relative path. The path may not start with '[' (that is the
    // markdown-link shape, captured above) and may not contain brackets, backticks, ')', or newlines.
    // e.g. [Source: `src/SpecScribe/X.cs:15-17`] or [Source: src/SpecScribe/X.cs:15]
    private static readonly Regex SourceInline = new(
        @"\[Source:\s*`?(?<path>[^\[\]`)\r\n]+?)`?\s*\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // A trailing ":line" or ":line-line" locator (GitHub-style ranges) stripped before the path is resolved. A
    // Windows drive prefix ("C:\…") never matches — the colon there is followed by a separator, not digits.
    private static readonly Regex LineSuffix = new(@":\d+(?:-\d+)?$", RegexOptions.Compiled);

    /// <summary>Extracts every citation target from one artifact's raw markdown, tagged with its resolution base.
    /// Pure and disk-free: only the <c>[Source: …]</c> citation shapes are matched, never arbitrary prose links,
    /// so the referenced set stays scoped to real citations (AC #1). Order-preserving; dedup/validation is the
    /// caller's job in <see cref="Discover"/>.</summary>
    public static IReadOnlyList<CodeCitation> ExtractTargets(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return Array.Empty<CodeCitation>();

        var results = new List<CodeCitation>();
        foreach (Match m in SourceMarkdownLink.Matches(markdown))
        {
            results.Add(new CodeCitation(m.Groups["href"].Value.Trim(), RelativeToArtifact: true));
        }
        foreach (Match m in SourceInline.Matches(markdown))
        {
            results.Add(new CodeCitation(m.Groups["path"].Value.Trim(), RelativeToArtifact: false));
        }
        return results;
    }

    /// <summary>Strips a trailing <c>#fragment</c> then a trailing <c>:line</c>/<c>:line-line</c> locator, leaving a
    /// bare path. Both are common in citations (<c>X.cs:15-17</c>, <c>architecture.md#Overview</c>) and neither is
    /// part of the file path.</summary>
    public static string StripLocator(string target)
    {
        if (string.IsNullOrEmpty(target)) return string.Empty;
        var path = target;
        var hash = path.IndexOf('#');
        if (hash >= 0) path = path[..hash];
        path = LineSuffix.Replace(path, string.Empty);
        return path.Trim();
    }

    /// <summary>Resolves the citation corpus to a deterministic, deduped set of repo-relative paths for the code
    /// files that should be rendered. A candidate survives only when it (a) resolves INSIDE <paramref name="repoRoot"/>
    /// (path-traversal guard — <c>../../etc/passwd</c> is rejected silently), (b) is NOT under
    /// <paramref name="sourceRoot"/> (<c>_bmad-output</c> docs already render as pages), (c) exists on disk as a file,
    /// and (d) is not an ignored working file. Returns forward-slashed repo-relative paths sorted ordinal-ignore-case
    /// for stable, deterministic output.</summary>
    public static IReadOnlyList<string> Discover(
        IEnumerable<CitedArtifact> artifacts,
        string repoRoot,
        string sourceRoot)
    {
        var repoFull = Path.GetFullPath(repoRoot);
        var sourceFull = Path.GetFullPath(sourceRoot);
        var results = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artifact in artifacts)
        {
            foreach (var citation in ExtractTargets(artifact.RawMarkdown))
            {
                var cleaned = StripLocator(citation.Target);
                if (cleaned.Length == 0) continue;

                var baseDir = citation.RelativeToArtifact ? artifact.ArtifactDirFullPath : repoFull;
                string full;
                try
                {
                    full = Path.GetFullPath(Path.Combine(baseDir, cleaned.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
                {
                    continue;
                }

                // (a) inside the repo, (b) not the _bmad-output source root the doc pipeline already renders.
                if (!IsInside(full, repoFull)) continue;
                if (IsInside(full, sourceFull)) continue;
                // (c) a real file, (d) not an ignored working file.
                if (!File.Exists(full)) continue;
                if (PathUtil.IsIgnoredSourceFile(full)) continue;

                results.Add(PathUtil.NormalizeSlashes(Path.GetRelativePath(repoFull, full)));
            }
        }

        return results.ToList();
    }

    /// <summary>True when <paramref name="fullPath"/> sits strictly inside <paramref name="rootFull"/> (a proper
    /// descendant, separator-anchored so a sibling like <c>repo-secrets</c> can't masquerade as inside
    /// <c>repo</c>).</summary>
    private static bool IsInside(string fullPath, string rootFull) =>
        fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
