using System.ComponentModel;
using Spectre.Console.Cli;

namespace SpecScribe;

/// <summary>CLI options shared by every command; anything omitted falls back to BMad auto-discovery.</summary>
public class SiteSettings : CommandSettings
{
    [CommandOption("-s|--source <DIR>")]
    [Description("Directory of spec artifacts to render. Default: walks up from the current directory to find _bmad-output.")]
    public string? Source { get; set; }

    [CommandOption("-a|--adrs <DIR>")]
    [Description("Directory of hand-authored architecture decision records. Default: <repo root>/docs/adrs.")]
    public string? Adrs { get; set; }

    [CommandOption("-o|--output <DIR>")]
    [Description("Directory the HTML site is written to. Default: <repo root>/SpecScribeOutput.")]
    public string? Output { get; set; }

    [CommandOption("-p|--project-name <NAME>")]
    [Description("Name the site is branded with. Default: project_name from _bmad/config.toml.")]
    public string? ProjectName { get; set; }

    [CommandOption("--no-readme")]
    [Description("Exclude the repository README.md from the generated site. Default: the README is included.")]
    public bool NoReadme { get; set; }

    [CommandOption("--deep-git")]
    [Description("Enable deeper git analytics (change coupling and hotspots) as an opt-in dashboard panel. Default: off, so baseline generation performance is unaffected.")]
    public bool DeepGit { get; set; }

    [CommandOption("--spa")]
    [Description("Also emit a JSON + client-renderer (SPA) delivery form alongside the static site: one entry shell, a manifest, and a few content chunks that navigate the whole site client-side (fewer files for large repos). Default: off; the static site is unchanged and is the no-JS fallback.")]
    public bool Spa { get; set; }

    [CommandOption("--code-url <BASE>")]
    [Description("Base URL for source-file links (e.g. https://github.com/owner/repo/blob/main). Adds a 'view source online' link to each in-portal code page (the pages are always generated). Default: unset, and auto-detected from the git remote or GitHub Pages context when available.")]
    public string? CodeUrl { get; set; }

    [CommandOption("--serve")]
    [Description("`webview` only: stay resident and stream one JSON payload per line (NDJSON) on stdout after every incremental regen, instead of rendering once and exiting. Reuses the same debounced file-watch/incremental-regen path as `specscribe watch`, so a live-push no longer reruns a full generation from scratch. Default: off (render once and exit).")]
    public bool Serve { get; set; }

    /// <summary>Resolves these settings into absolute paths. Throws <see cref="DirectoryNotFoundException"/>
    /// with an actionable message when auto-discovery fails. This is the CLI entry path, so it opts into git-remote /
    /// CI auto-detection of the external source base when <c>--code-url</c> is not given (library/test callers use
    /// <see cref="ForgeOptions.Resolve"/> directly, which leaves detection off for deterministic output).</summary>
    public ForgeOptions Resolve() => ForgeOptions.Resolve(Source, Adrs, Output, ProjectName, includeReadme: !NoReadme, deepGitAnalytics: DeepGit, emitSpa: Spa, codeSourceBaseUrl: CodeUrl, autoDetectCodeUrl: true);

    /// <summary>Like <see cref="Resolve"/>, but does NOT throw when no <c>_bmad-output</c> marker is found up-tree —
    /// it falls back to the current directory as the repo root with a (possibly absent) conventional source root.
    /// Used only by the <c>webview</c>/extension path so the VS Code extension is usable in ANY workspace: generation
    /// then degrades to README + Code Map + git-if-present rather than failing. The interactive/CLI
    /// <c>generate</c>/<c>watch</c> commands keep <see cref="Resolve"/> and its actionable error (CLI honesty).
    /// [spec-vscode-any-workspace-and-processing-indicators]</summary>
    public ForgeOptions ResolveTolerant() => ForgeOptions.Resolve(Source, Adrs, Output, ProjectName, includeReadme: !NoReadme, deepGitAnalytics: DeepGit, emitSpa: Spa, codeSourceBaseUrl: CodeUrl, autoDetectCodeUrl: true, requireSource: false);
}
