using System.ComponentModel;
using Spectre.Console;
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

    [CommandOption("--today-policy <POLICY>")]
    [Description("How to decide which calendar day is \"today\" when generating date pages and date links: machine-local (the generating machine's day), utc (the UTC calendar day), or last-commit (the latest authored commit day). Default: machine-local. Governs only the day cutoff — commit times always render in their own authored offset.")]
    public string? TodayPolicy { get; set; }

    [CommandOption("--show-config")]
    [Description("Print the effective configuration and where each value came from (command line, .specscribe, or auto-discovery), then exit without generating. Default: off.")]
    public bool ShowConfig { get; set; }

    [CommandOption("--serve")]
    [Description("`webview` only: stay resident and stream one JSON payload per line (NDJSON) on stdout after every incremental regen, instead of rendering once and exiting. Reuses the same debounced file-watch/incremental-regen path as `specscribe watch`, so a live-push no longer reruns a full generation from scratch. Default: off (render once and exit).")]
    public bool Serve { get; set; }

    /// <summary>Spectre's parse-time validation gate — the earliest, cleanest place to reject a typo'd
    /// <c>--today-policy</c>: it fails before any path resolution or generation work, and the message is rendered as
    /// a plain CLI error rather than an exception. <see cref="ResolveDatePolicy"/> keeps the same check as a backstop
    /// for the surfaces Spectre never validates (the interactive menu, library callers). [Story 5.5]</summary>
    public override ValidationResult Validate() =>
        TodayPolicy is { Length: > 0 } && !DatePolicies.TryParse(TodayPolicy, out _)
            ? ValidationResult.Error(DatePolicies.RejectionMessage(TodayPolicy))
            : ValidationResult.Success();

    /// <summary>Resolves these settings into absolute paths. Throws <see cref="DirectoryNotFoundException"/>
    /// with an actionable message when auto-discovery fails. This is the CLI entry path, so it opts into git-remote /
    /// CI auto-detection of the external source base when <c>--code-url</c> is not given (library/test callers use
    /// <see cref="ForgeOptions.Resolve"/> directly, which leaves detection off for deterministic output).
    /// <para><paramref name="startDirectory"/> is the walk-up origin for auto-discovery, defaulting to the process
    /// working directory. Injected only for headless testing — production callers omit it. [Story 5.2]</para></summary>
    public ForgeOptions Resolve(string? startDirectory = null) => ForgeOptions.Resolve(Source, Adrs, Output, ProjectName, startDirectory: startDirectory, includeReadme: !NoReadme, deepGitAnalytics: DeepGit, emitSpa: Spa, codeSourceBaseUrl: CodeUrl, autoDetectCodeUrl: true, datePolicy: ResolveDatePolicy());

    /// <summary>Like <see cref="Resolve"/>, but does NOT throw when no <c>_bmad-output</c> marker is found up-tree —
    /// it falls back to the current directory as the repo root with a (possibly absent) conventional source root.
    /// Used only by the <c>webview</c>/extension path so the VS Code extension is usable in ANY workspace: generation
    /// then degrades to README + Code Map + git-if-present rather than failing. The interactive/CLI
    /// <c>generate</c>/<c>watch</c> commands keep <see cref="Resolve"/> and its actionable error (CLI honesty).
    /// [spec-vscode-any-workspace-and-processing-indicators]</summary>
    public ForgeOptions ResolveTolerant() => ForgeOptions.Resolve(Source, Adrs, Output, ProjectName, includeReadme: !NoReadme, deepGitAnalytics: DeepGit, emitSpa: Spa, codeSourceBaseUrl: CodeUrl, autoDetectCodeUrl: true, requireSource: false, datePolicy: ResolveDatePolicy());

    /// <summary>Parses <c>--today-policy</c> into the typed <see cref="DatePolicy"/>. An absent value keeps the
    /// default; an UNRECOGNIZED one throws rather than silently defaulting — a typo that quietly no-ops is a worse
    /// failure than an error, and the message lists every value that would have worked. Same reject-don't-silently-
    /// accept discipline as <c>--code-url</c>'s validation. Thrown as <see cref="ArgumentException"/> so both
    /// <see cref="Resolve"/> and <see cref="ResolveTolerant"/> surface it identically — tolerant mode is about a
    /// missing project root, not about accepting bad input. [Story 5.5]</summary>
    public DatePolicy ResolveDatePolicy()
    {
        if (TodayPolicy is not { Length: > 0 }) return DatePolicy.MachineLocal;
        if (DatePolicies.TryParse(TodayPolicy, out var policy)) return policy;
        // No paramName: the message already names the option the user typed, and appending "(Parameter
        // 'TodayPolicy')" would leak an internal property name into a user-facing CLI error.
        throw new ArgumentException(DatePolicies.RejectionMessage(TodayPolicy));
    }
}
