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

    public const string StylesheetName = "specscribe.css";
    public const string DefaultSiteTitle = "BMad Live Docs";
    public const string SourceDirName = "_bmad-output";

    /// <summary>Subdirectory of the output root where rendered ADR pages land.</summary>
    public const string AdrOutputSubdir = "adrs";
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
        string? startDirectory = null)
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
            AdrSourceRoot = adrs is { Length: > 0 } ? Path.GetFullPath(adrs) : Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = adrs is { Length: > 0 },
            OutputRoot = output is { Length: > 0 } ? Path.GetFullPath(output) : Path.Combine(repoRoot, "docs", "live"),
            SiteTitle = projectName is { Length: > 0 } ? projectName : ReadProjectName(repoRoot) ?? DefaultSiteTitle,
        };
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
