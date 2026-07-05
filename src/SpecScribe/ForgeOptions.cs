using System.Text.RegularExpressions;

namespace DocsForge;

/// <summary>Resolved absolute paths and settings for a run.</summary>
public sealed class ForgeOptions
{
    public required string RepoRoot { get; init; }
    public required string SourceRoot { get; init; }

    /// <summary>Hand-authored Architecture Decision Records (<c>docs/adrs</c>). A read-only second source: DocsForge
    /// renders these into the live site but never writes back to this folder.</summary>
    public required string AdrSourceRoot { get; init; }
    public required string OutputRoot { get; init; }
    public required string StylesheetSourcePath { get; init; }

    /// <summary>The game's name, read from _bmad/config.toml's project_name — the site is branded with
    /// this rather than a generic tool name.</summary>
    public required string SiteTitle { get; init; }

    public const string StylesheetName = "docsforge.css";
    public const string DefaultSiteTitle = "BMad Live Docs";

    /// <summary>Subdirectory of the output root where rendered ADR pages land.</summary>
    public const string AdrOutputSubdir = "adrs";
    public static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(400);

    /// <summary>Walks up from the executable/working directory to find the repo root (marked by the presence of _bmad-output).</summary>
    public static ForgeOptions Resolve()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "_bmad-output")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            // Fall back to walking up from the current working directory (covers `dotnet run`).
            dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "_bmad-output")))
            {
                dir = dir.Parent;
            }
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                "Could not locate the repo root (a directory containing '_bmad-output') from the executable or working directory.");
        }

        var repoRoot = dir.FullName;
        return new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = Path.Combine(repoRoot, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
            OutputRoot = Path.Combine(repoRoot, "docs", "live"),
            StylesheetSourcePath = Path.Combine(AppContext.BaseDirectory, "assets", StylesheetName),
            SiteTitle = ReadProjectName(repoRoot) ?? DefaultSiteTitle,
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
