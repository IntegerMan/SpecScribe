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

    /// <summary>Resolves these settings into absolute paths. Throws <see cref="DirectoryNotFoundException"/>
    /// with an actionable message when auto-discovery fails.</summary>
    public ForgeOptions Resolve() => ForgeOptions.Resolve(Source, Adrs, Output, ProjectName, includeReadme: !NoReadme);
}
