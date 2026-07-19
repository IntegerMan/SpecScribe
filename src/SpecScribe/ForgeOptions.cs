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

    /// <summary>When true (opt-in via <c>--spa</c>), generation additionally emits the JSON + client-renderer (SPA)
    /// delivery form — a manifest, grouped content chunks, an entry shell, and the client script — ALONGSIDE the
    /// untouched static site (ADR 0006 Architecture B, Story 6.7). Off by default: with the flag off NO SPA files
    /// are written and the static output is byte-identical, so the golden gate is unaffected (AC #3/#5). Not
    /// <c>required</c> precisely so every existing <see cref="ForgeOptions"/> construction defaults to off. [Story 6.7]</summary>
    public bool EmitSpa { get; init; }

    /// <summary>The base URL of the file's source on its hosting platform, e.g.
    /// <c>https://github.com/owner/repo/blob/main</c> (Story 7.7). When set — explicitly via <c>--code-url</c> or
    /// auto-detected from the git remote / GitHub Pages CI context — in-portal code pages still generate as always and
    /// each one gains an <em>additive</em> "view source online" link to <c>{CodeSourceBaseUrl}/&lt;repo-relative-path&gt;</c>.
    /// This never diverts citations away from the in-portal pages; it only adds a way out to the hosted original
    /// (which also supplies syntax highlighting for free). Not <c>required</c> so every existing
    /// <see cref="ForgeOptions"/> construction defaults to no external link. [Story 7.7, was 7.1]</summary>
    public string? CodeSourceBaseUrl { get; init; }

    public const string StylesheetName = "specscribe.css";

    /// <summary>The one sanctioned progressive-enhancement script (on-brand chart tooltips + Next Steps copy
    /// buttons). Delivered self-contained the same way the stylesheet is — an embedded resource copied to the
    /// output root — so the global-tool package needs no loose asset files. Degrades to native
    /// <c>&lt;title&gt;</c>/<c>aria-label</c> when JS is unavailable. [Story 1.5 Task 3]</summary>
    public const string ScriptName = "specscribe.js";

    /// <summary>The vendored Prism.js bundle + theme (Story 7.1 rework) that syntax-highlight in-portal code pages.
    /// Embedded resources like the core stylesheet/script, but copied to the output root ONLY when in-portal code
    /// pages are actually generated (see <see cref="SiteGenerator"/>) so a site with no code pages stays byte-for-byte
    /// unchanged. Loaded only on code pages; the highlighter degrades to plain monospace when JS is unavailable.</summary>
    public const string CodeHighlightScriptName = "prism.js";
    public const string CodeHighlightStyleName = "prism.css";

    public const string DefaultSiteTitle = "BMad Live Docs";
    public const string SourceDirName = "_bmad-output";

    /// <summary>BMad's config directory (repo-root <c>_bmad</c>) and the project-config file inside it whose
    /// <c>project_name</c> brands the site (<see cref="ReadProjectName"/>). Named constants because this file lives
    /// under NEITHER source root — the watch layer (<see cref="FileWatcherService"/>) and the data-source
    /// classifier (<see cref="SiteGenerator.IsDataSource"/>) both need it, and it must be the ONE literal (NFR4).
    /// [Story 6.11]</summary>
    public const string ConfigDirName = "_bmad";
    public const string ConfigFileName = "config.toml";

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
    /// containing <c>_bmad-output</c> is found. When <paramref name="requireSource"/> is <c>true</c> (the default,
    /// the CLI path) and no such directory is found, this throws an actionable
    /// <see cref="DirectoryNotFoundException"/>. When <c>false</c> (the <c>webview</c>/extension path), it instead
    /// falls back to <paramref name="startDirectory"/> as the repo root with a (possibly absent) conventional source
    /// root, so generation degrades gracefully in any workspace rather than failing.
    /// [spec-vscode-any-workspace-and-processing-indicators]</summary>
    public static ForgeOptions Resolve(
        string? source = null,
        string? adrs = null,
        string? output = null,
        string? projectName = null,
        string? startDirectory = null,
        bool includeReadme = true,
        bool deepGitAnalytics = false,
        bool emitSpa = false,
        string? codeSourceBaseUrl = null,
        bool autoDetectCodeUrl = false,
        bool requireSource = true)
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

            if (dir is not null)
            {
                repoRoot = dir.FullName;
                sourceRoot = Path.Combine(repoRoot, SourceDirName);
            }
            else if (requireSource)
            {
                throw new DirectoryNotFoundException(
                    $"Could not locate a repo root (a directory containing '{SourceDirName}') at or above the " +
                    "current directory. Run from inside a BMad project, or pass --source to point at your artifacts.");
            }
            else
            {
                // Tolerant mode (the `webview`/extension path, requireSource:false): no `_bmad-output` marker exists
                // anywhere up-tree, so treat the start directory as the repo root and point the (nonexistent) source
                // root at its conventional location. Every downstream source/ADR read is Directory.Exists-guarded, so
                // generation degrades to README + Code Map + git-if-present instead of failing — the extension must be
                // usable in ANY workspace, not only bmad projects. [spec-vscode-any-workspace-and-processing-indicators]
                repoRoot = Path.GetFullPath(startDirectory ?? Directory.GetCurrentDirectory());
                sourceRoot = Path.Combine(repoRoot, SourceDirName);
            }
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
            EmitSpa = emitSpa,
            // Explicit --code-url always wins; otherwise (CLI only — never in test/library paths, which pass
            // autoDetectCodeUrl=false so generation stays deterministic) fall back to git-remote / CI detection.
            // A malformed value (no scheme, whitespace-only) is rejected rather than accepted verbatim, since it
            // would otherwise silently flow into a broken "view source online" link on every code page.
            CodeSourceBaseUrl = TryValidateCodeUrl(codeSourceBaseUrl, out var validatedCodeUrl)
                ? validatedCodeUrl
                : autoDetectCodeUrl ? CodeSourceUrlResolver.TryDetect(repoRoot) : null,
        };
    }

    /// <summary>Validates a candidate <c>--code-url</c> value: must be non-blank and an absolute http(s) URL.
    /// Rejects whitespace-only input and schemeless values (e.g. <c>example.com/repo</c>) that would otherwise
    /// silently produce a broken external link. [Story 7.1, code-review patch]
    /// <para>Also strips a trailing <c>#...</c> fragment the caller included in the base itself — <see
    /// cref="SiteGenerator.BuildExternalSourceUrl"/> appends the repo-relative path after this base, and a
    /// fragment can only be valid at the very end of a URL, so a base carrying one would corrupt every generated
    /// link (the repo-relative path ends up inside the fragment instead of the path). [Story 7.7 deferred fix]</para>
    /// </summary>
    private static bool TryValidateCodeUrl(string? candidate, out string validated)
    {
        validated = string.Empty;
        if (candidate is not { Length: > 0 }) return false;
        var trimmed = candidate.Trim();
        if (trimmed.Length == 0) return false;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        // Strip only a trailing fragment, on the original string — not a full Uri round-trip — so an
        // already-valid base's exact casing/encoding survives untouched (only the deferred defect is fixed).
        var fragmentIdx = trimmed.IndexOf('#');
        validated = fragmentIdx >= 0 ? trimmed[..fragmentIdx] : trimmed;
        return true;
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
            var configPath = Path.Combine(repoRoot, ConfigDirName, ConfigFileName);
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
