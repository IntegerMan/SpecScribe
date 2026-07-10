using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Resolved absolute paths and settings for a run.</summary>
public sealed class ForgeOptions
{
    public required string RepoRoot { get; init; }
    public required string SourceRoot { get; init; }

    /// <summary>Hand-authored Architecture Decision Records (<c>docs/adrs</c>). A read-only second source: SpecScribe
    /// renders these into the live site but never writes back to this folder.</summary>
    public required string AdrSourceRoot { get; init; }

    /// <summary>True when the ADR directory was set explicitly (via <c>--adrs</c>) rather than defaulted. ADRs are
    /// always optional, but an explicit-yet-missing directory is surfaced as a warning since it likely signals a typo.</summary>
    public required bool AdrSourceExplicit { get; init; }

    public required string OutputRoot { get; init; }

    /// <summary>The project's name, read from _bmad/config.toml's project_name — the site is branded with
    /// this rather than a generic tool name.</summary>
    public required string SiteTitle { get; init; }

    /// <summary>When true (the default), a <c>README.md</c> found at the repo root is rendered into the site
    /// as a stylized page and surfaced from the home index. Disabled via <c>--no-readme</c>.</summary>
    public required bool IncludeReadme { get; init; }

    /// <summary>When true (opt-in via <c>--deep-git</c>), the generator runs the heavier deep-git pass
    /// (change-coupling + hotspots) and renders its distinct dashboard panel. Off by default: the deep git
    /// process is never invoked when this is false, so baseline generation performance cannot regress. The
    /// flag is the FR-10 performance guarantee — the gate, not a timing test. [Story 3.2]</summary>
    public required bool DeepGitAnalytics { get; init; }

    public const string StylesheetName = "specscribe.css";

    /// <summary>The one sanctioned progressive-enhancement script (on-brand chart tooltips + Next Steps copy
    /// buttons). Delivered self-contained the same way the stylesheet is — an embedded resource copied to the
    /// output root — so the global-tool package needs no loose asset files. Degrades to native
    /// <c>&lt;title&gt;</c>/<c>aria-label</c> when JS is unavailable. [Story 1.5 Task 3]</summary>
    public const string ScriptName = "specscribe.js";

    public const string DefaultSiteTitle = "BMad Live Docs";
    public const string SourceDirName = "_bmad-output";

    /// <summary>Default output directory (a single top-level folder under the repo root, not nested under
    /// <c>docs/</c> where the hand-authored ADR source lives). Matches the <c>--output SpecScribeOutput</c>
    /// convention used by the README and the GitHub Pages workflow.</summary>
    public const string OutputDirName = "SpecScribeOutput";

    /// <summary>Subdirectory of the output root where rendered ADR pages land.</summary>
    public const string AdrOutputSubdir = "adrs";

    /// <summary>The conventional ADR homes probed (in this order, first match with any markdown content wins)
    /// when <c>--adrs</c> is not given AND the canonical default (<c>docs/adrs</c>) is absent. Detection over
    /// configuration: a repo using another mainstream convention just works, while the canonical default stays
    /// the first-checked branch so this repo's own resolution is byte-identical. Probing finds nothing ⇒ the
    /// default (absent) path is kept and the ADR section simply omits, as today. [Story 4.2 Task 1]</summary>
    public static readonly IReadOnlyList<string> AdrFallbackProbeSubdirs = new[]
    {
        Path.Combine("docs", "adr"),
        Path.Combine("docs", "decisions"),
        Path.Combine("docs", "architecture", "decisions"),
        Path.Combine("docs", "architecture", "adr"),
        "adr",
        "adrs",
    };
    public static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(400);

    /// <summary>Resolves paths for a run. Explicit values win; anything omitted is derived from the repo root,
    /// which is either the parent of an explicit <paramref name="source"/> or found by walking up from
    /// <paramref name="startDirectory"/> (defaults to the current working directory) until a directory
    /// containing <c>_bmad-output</c> is found.</summary>
    public static ForgeOptions Resolve(
        string? source = null,
        string? adrs = null,
        string? output = null,
        string? projectName = null,
        string? startDirectory = null,
        bool includeReadme = true,
        bool deepGitAnalytics = false)
    {
        string repoRoot;
        string sourceRoot;
        if (source is { Length: > 0 })
        {
            sourceRoot = Path.GetFullPath(source);
            repoRoot = Path.GetDirectoryName(sourceRoot) ?? sourceRoot;
        }
        else
        {
            // Deliberately walks up from the cwd only — never the executable directory, which for an
            // installed global tool lives in the tool store under the user profile.
            var dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, SourceDirName)))
            {
                dir = dir.Parent;
            }

            if (dir is null)
            {
                throw new DirectoryNotFoundException(
                    $"Could not locate a repo root (a directory containing '{SourceDirName}') at or above the " +
                    "current directory. Run from inside a BMad project, or pass --source to point at your artifacts.");
            }

            repoRoot = dir.FullName;
            sourceRoot = Path.Combine(repoRoot, SourceDirName);
        }

        return new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = sourceRoot,
            AdrSourceRoot = adrs is { Length: > 0 } ? Path.GetFullPath(adrs) : ResolveAdrSourceRoot(repoRoot),
            AdrSourceExplicit = adrs is { Length: > 0 },
            OutputRoot = output is { Length: > 0 } ? Path.GetFullPath(output) : Path.Combine(repoRoot, OutputDirName),
            SiteTitle = projectName is { Length: > 0 } ? projectName : ReadProjectName(repoRoot) ?? DefaultSiteTitle,
            IncludeReadme = includeReadme,
            DeepGitAnalytics = deepGitAnalytics,
        };
    }

    /// <summary>Resolves the implicit ADR root: the canonical <c>docs/adrs</c> whenever that directory exists
    /// (even if empty — today's behavior, untouched), otherwise the first
    /// <see cref="AdrFallbackProbeSubdirs"/> candidate holding at least one markdown file within one directory
    /// level. Resolved once here, at option time, so watch routing (<c>SiteGenerator.IsAdr</c> compares
    /// against this same path) and generation can never disagree about where ADRs live. A probe that finds
    /// nothing keeps the canonical (absent) default silently — ADRs are optional; only an EXPLICIT missing
    /// <c>--adrs</c> warns (see <see cref="AdrSourceExplicit"/>). [Story 4.2 Task 1]</summary>
    private static string ResolveAdrSourceRoot(string repoRoot)
    {
        var canonical = Path.Combine(repoRoot, "docs", "adrs");
        if (Directory.Exists(canonical))
        {
            return canonical;
        }

        foreach (var subdir in AdrFallbackProbeSubdirs)
        {
            var candidate = Path.Combine(repoRoot, subdir);
            if (HasMarkdownWithinOneLevel(candidate))
            {
                return candidate;
            }
        }

        return canonical;
    }

    /// <summary>True when <paramref name="dir"/> holds at least one non-ignored, non-README <c>*.md</c>
    /// directly or in a direct subdirectory — the same one-level-deep window the ADR enumeration reads, so a
    /// probe never resolves to a directory generation would then find empty. Bounded on purpose (never a
    /// whole-tree walk). README is excluded from the content check (though it still renders as the ADR
    /// landing page if the candidate is chosen) so a folder holding only landing-page prose doesn't win the
    /// probe ahead of a later candidate that actually holds decision records. Never throws: an unreadable
    /// candidate is treated as empty. [Story 4.2 Task 1] [Review][Patch]</summary>
    private static bool HasMarkdownWithinOneLevel(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            return Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateDirectories(dir)
                    .SelectMany(d => Directory.EnumerateFiles(d, "*.md", SearchOption.TopDirectoryOnly)))
                .Any(p => !PathUtil.IsIgnoredSourceFile(p)
                    && !string.Equals(Path.GetFileName(p), "README.md", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static readonly Regex ProjectNamePattern = new(
        "^\\s*project_name\\s*=\\s*\"(?<name>.+?)\"",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Pulls project_name from _bmad/config.toml. A full TOML parser would be overkill for one
    /// key — a line regex with shared-read access keeps this dependency-free and lock-free.</summary>
    private static string? ReadProjectName(string repoRoot)
    {
        try
        {
            var configPath = Path.Combine(repoRoot, "_bmad", "config.toml");
            if (!File.Exists(configPath)) return null;

            var text = MarkdownConverter.ReadAllTextShared(configPath);
            var match = ProjectNamePattern.Match(text);
            return match.Success ? match.Groups["name"].Value.Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
