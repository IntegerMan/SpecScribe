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
    [Description("Base URL for source-file links (e.g. https://github.com/owner/repo/blob/main). When set, in-portal code pages are NOT generated and citations link to {BASE}/<path>#L<line> instead. Default: unset, so referenced source files render as in-portal pages.")]
    public string? CodeUrl { get; set; }

    /// <summary>Resolves these settings into absolute paths. Throws <see cref="DirectoryNotFoundException"/>
    /// with an actionable message when auto-discovery fails.</summary>
    public ForgeOptions Resolve() => ForgeOptions.Resolve(Source, Adrs, Output, ProjectName, includeReadme: !NoReadme, deepGitAnalytics: DeepGit, emitSpa: Spa, codeSourceBaseUrl: CodeUrl);
}
