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

    /// <summary>⚠️ <b>The FALSE path is gone. This property is retained, defaulted to <c>true</c>, and no longer
    /// consulted by <see cref="SiteGenerator"/>.</b> [Story 23.6 AC #6]
    /// <para>It was the opt-in behind <c>--spa</c>: generation emitted the JSON delivery form ALONGSIDE the static
    /// site, and with the flag off no IR was written at all. That inverted in Story 23.6. The IR under <c>spa/</c>
    /// is the canonical output (ADR 0016) and the static pages are RENDERED FROM IT (ADR 0022 §Decision 3), so a
    /// run that emitted no IR would emit nothing at all — C# no longer writes a content page.</para>
    /// <para>It is kept rather than deleted so the ~40 existing <see cref="ForgeOptions"/> constructions and their
    /// <c>emitSpa:</c> arguments keep compiling; the default flipped from <c>false</c> to <c>true</c> so a caller
    /// that omits it gets the only behaviour that now exists. Setting it <c>false</c> does nothing.</para></summary>
    public bool EmitSpa { get; init; } = true;

    /// <summary>The base URL of the file's source on its hosting platform, e.g.
    /// <c>https://github.com/owner/repo/blob/main</c> (Story 7.7). When set — explicitly via <c>--code-url</c> or
    /// auto-detected from the git remote / GitHub Pages CI context — in-portal code pages still generate as always and
    /// each one gains an <em>additive</em> "view source online" link to <c>{CodeSourceBaseUrl}/&lt;repo-relative-path&gt;</c>.
    /// This never diverts citations away from the in-portal pages; it only adds a way out to the hosted original
    /// (which also supplies syntax highlighting for free). Not <c>required</c> so every existing
    /// <see cref="ForgeOptions"/> construction defaults to no external link. [Story 7.7, was 7.1]</summary>
    public string? CodeSourceBaseUrl { get; init; }

    /// <summary>How this run decides which calendar day is "today" for the date-page cutoff (<c>--today-policy</c>,
    /// or <c>--as-of &lt;DATE&gt;</c> for a fixed day). Governs which <c>commits/{date}.html</c> pages are generated
    /// and which date links are drawn — NEVER how a timestamp is displayed (commit times stay in their authored
    /// offset, Story 10.4). Not <c>required</c>, and <c>default(DateCutoff)</c> is
    /// <c>(<see cref="DatePolicy.MachineLocal"/>, null)</c>, so every existing <see cref="ForgeOptions"/>
    /// construction defaults to the Story 10.4 status quo. [Story 5.5, retyped in Story 5.7]</summary>
    public DateCutoff DateCutoff { get; init; }

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

    /// <summary>The vendored plotly.js custom bundle (Story 20.5) that renders the Hierarchy Explorer's sunburst
    /// and treemap. An embedded resource like the ones above, copied to the output root ONLY when the site
    /// rendered at least one hierarchy chart. <b>Never a CDN URL</b> — ADR 0012 §1 / NFR-3: the portal must render
    /// offline, from <c>file://</c>, and under the webview's CSP. Built by hand from a pinned v3.7.0 clone; see
    /// <c>tools/plotly-vendor/README.md</c>. With JS unavailable the component's server-rendered text twin is the
    /// whole contract (ADR 0013).</summary>
    public const string HierarchyEngineScriptName = "plotly-hierarchy.min.js";

    public const string DefaultSiteTitle = "BMad Live Docs";
    public const string SourceDirName = "_bmad-output";

    /// <summary>The framework marker directories <see cref="Resolve"/> probes when no explicit <c>--source</c> is
    /// given, in priority order. Each entry is BOTH the marker that identifies a repo root AND the source root
    /// derived from it — the layout every framework here shares (its planning artifacts live inside its marker).
    ///
    /// <para><b>Framework INSTALL markers precede BMad's OUTPUT directory, deliberately</b> — and this is the one
    /// place Story 12.2's task list was followed in intent rather than to the letter. That task said to keep
    /// <c>_bmad-output</c> first "so this repo's own resolution is byte-identical"; the stated GOAL is what is
    /// preserved here, and preserved exactly. <c>_bmad-output</c> is an OUTPUT folder — a BMad project writes one
    /// whether or not any other framework is present — whereas <c>.planning</c>/<c>.gsd</c>/<c>.specify</c> are
    /// framework install markers. Probing the output folder first makes it a universal winner, and the real
    /// reference repository (<c>CORA</c>) carries BOTH: its <c>_bmad-output</c> holds six planning documents while
    /// its <c>.planning</c> holds 168 files, 11 phases and 58 plans. Ordering BMad first would have resolved
    /// <see cref="SourceRoot"/> to the six-file tree and left every GSD artifact OUTSIDE the source root — where
    /// <see cref="PathUtil.EscapesRepoRoot"/> rejects it — so the framework this story exists to support could not
    /// render at all (AC #1). Ordering by specificity costs nothing in the case the instruction was protecting: a
    /// BMad-only repo, this repository included, has none of the other markers, so it resolves to
    /// <c>_bmad-output</c> exactly as it always did. It also matches the same story's registry guidance verbatim —
    /// "specific markers before BMad's fallback".</para>
    ///
    /// <para><b>The cost, stated rather than hidden.</b> In a repo carrying two frameworks the NON-primary
    /// framework's documents sit outside the single source root and do not render as pages. That is the bounded
    /// compromise D5 accepts — bundle-level merging is cheap, file-discovery-level merging is not — and
    /// <see cref="AdapterRegistry"/> reports it as an <see cref="AdapterDiagnosticCategory.Informational"/> notice
    /// naming the marker that was not made primary, so the omission is a stated boundary rather than a silent gap
    /// (NFR8).</para>
    ///
    /// <para><b>What this does and does not decide.</b> It decides which single directory the <c>*.md</c>
    /// enumeration walks and which root every source-relative path is computed against — <see cref="SourceRoot"/>
    /// is single-valued and anchors both. It does NOT decide which adapters run: that is
    /// <see cref="AdapterRegistry"/>'s ordered <c>AppliesTo</c> sweep, and a repo carrying two frameworks merges at
    /// the <see cref="ArtifactBundle"/> level with the non-primary framework's documents rendering through whatever
    /// this primary root already sees. Multi-ROOTED source discovery is deliberately out of scope (Story 4.9 AC #2);
    /// see ADR 0038.</para>
    ///
    /// <para><c>.specify</c> is Spec Kit's install marker; whether its source root should instead be the sibling
    /// <c>specs/</c> tree is Story 11.2's call, not this one's. Listing the marker here is what stops a Spec Kit
    /// repo failing before any adapter is consulted; refining where it points is that story's.</para>
    /// [Story 12.2 Task 2; ADR 0038]</summary>
    public static readonly IReadOnlyList<string> SourceDirNames = new[]
    {
        GsdCoreArtifactAdapter.MarkerDirName, // GSD Core [Story 12.2]
        ".gsd",                               // GSD Pi [Story 12.3]
        ".specify",                           // Spec Kit [Story 11.2]
        SourceDirName,                        // BMad's output folder — the least specific marker, so it probes LAST
    };

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
        // Story 23.6 AC #6: the IR is unconditional, so the only remaining behaviour is `true`. Kept in the
        // signature so existing `emitSpa:` call sites compile unchanged; the value is no longer consulted.
        bool emitSpa = true,
        string? codeSourceBaseUrl = null,
        bool autoDetectCodeUrl = false,
        bool requireSource = true,
        DateCutoff dateCutoff = default)
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
            //
            // Story 12.2: the probe is now the ordered SourceDirNames marker set rather than the single
            // _bmad-output literal. NEAREST DIRECTORY WINS, then marker order within it — so a nested BMad project
            // under a GSD parent still resolves to the nested one, exactly as the single-marker walk did, and a
            // repo carrying several markers at the SAME level resolves by SourceDirNames order (BMad first).
            var dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
            string? markerDirName = null;
            while (dir is not null && (markerDirName = FindSourceMarker(dir.FullName)) is null)
            {
                dir = dir.Parent;
            }

            if (dir is not null && markerDirName is not null)
            {
                repoRoot = dir.FullName;
                sourceRoot = Path.Combine(repoRoot, markerDirName);
            }
            else if (requireSource)
            {
                throw new DirectoryNotFoundException(
                    $"Could not locate a repo root (a directory containing one of {string.Join(", ", SourceDirNames.Select(m => $"'{m}'"))}) " +
                    "at or above the current directory. Run from inside a spec-driven project, or pass --source to " +
                    "point at your artifacts.");
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
            SiteTitle = projectName is { Length: > 0 } ? projectName : ResolveSiteTitle(repoRoot, sourceRoot),
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
            DateCutoff = dateCutoff,
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

    /// <summary>The first <see cref="SourceDirNames"/> marker present directly under <paramref name="dir"/>, or
    /// null when none is. Never throws: an unreadable candidate simply is not a marker. [Story 12.2 Task 2]</summary>
    private static string? FindSourceMarker(string dir)
    {
        foreach (var marker in SourceDirNames)
        {
            try
            {
                if (Directory.Exists(Path.Combine(dir, marker))) return marker;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // An unreadable/malformed candidate is not a marker; keep probing the rest.
            }
        }
        return null;
    }

    /// <summary>Brands the site when <c>--project-name</c> was not given: BMad's <c>_bmad/config.toml</c> first
    /// (unchanged, and still the only probe that can fire for a BMad root), then the source root's own
    /// <c>PROJECT.md</c> H1 for a non-BMad framework, then a neutral fallback.
    ///
    /// <para><b>Why the default is no longer unconditional.</b> <see cref="DefaultSiteTitle"/> is the literal
    /// "BMad Live Docs". Before Story 12.2 a repo with no <c>_bmad/config.toml</c> got that name whatever framework
    /// produced it — harmless while BMad was the only resolvable root, actively wrong the moment a GSD Core repo
    /// could generate at all: the portal would brand a project that has never installed BMad with BMad's name. A
    /// non-BMad root now falls back to the repo's own directory name, which is honest and never claims a framework
    /// the repo does not use. A BMad root's fallback is untouched, so this repo and every existing BMad project
    /// resolve exactly as before. [Story 12.2 Task 2; ADR 0038]</para></summary>
    private static string ResolveSiteTitle(string repoRoot, string sourceRoot)
    {
        if (ReadProjectName(repoRoot) is { Length: > 0 } fromConfig) return fromConfig;

        var isBmadRoot = string.Equals(
            Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            SourceDirName, StringComparison.OrdinalIgnoreCase);
        if (isBmadRoot) return DefaultSiteTitle;

        if (ReadProjectDocTitle(sourceRoot) is { Length: > 0 } fromDoc) return fromDoc;

        // The repo's own folder name — never a framework's name. Empty only for a filesystem root, which falls
        // back to the historical default rather than an empty <title>.
        var folder = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return folder is { Length: > 0 } ? folder : DefaultSiteTitle;
    }

    private static readonly Regex ProjectDocTitlePattern = new(
        @"^\#\s+(?<name>\S.*?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>The first level-1 heading of <c>&lt;sourceRoot&gt;/PROJECT.md</c> — GSD Core's project brief, whose
    /// H1 is the project name (<c># CORA</c>). Probed only for a NON-BMad source root (see
    /// <see cref="ResolveSiteTitle"/>), so a BMad tree that happens to hold a <c>PROJECT.md</c> cannot change the
    /// title it resolves today. Never throws. [Story 12.2 Task 2]</summary>
    private static string? ReadProjectDocTitle(string sourceRoot)
    {
        try
        {
            var path = Path.Combine(sourceRoot, "PROJECT.md");
            if (!File.Exists(path)) return null;

            var m = ProjectDocTitlePattern.Match(MarkdownConverter.ReadAllTextShared(path));
            return m.Success ? m.Groups["name"].Value.Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
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
