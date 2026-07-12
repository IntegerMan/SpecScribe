using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SpecScribe;

/// <summary>`specscribe generate` — build the site once and exit.</summary>
public sealed class GenerateCommand : Command<SiteSettings>
{
    protected override int Execute(CommandContext context, SiteSettings settings, CancellationToken cancellationToken)
    {
        var options = settings.Resolve();
        ConsoleUi.PrintLogo();
        ConsoleUi.PrintPaths(options);
        RunGeneration(options);
        return 0;
    }

    /// <summary>Full generation pass with per-phase progress and a summary; shared with watch/interactive runs.</summary>
    public static SiteGenerator RunGeneration(ForgeOptions options)
    {
        var generator = new SiteGenerator(options);
        var sw = Stopwatch.StartNew();
        var events = ConsoleUi.RunWithProgress(generator);
        sw.Stop();
        ConsoleUi.PrintInitialSummary(events, sw.Elapsed);
        ConsoleUi.PrintOutputLink(options);
        return generator;
    }
}

/// <summary>`specscribe webview` — render the VS Code webview surface bundle (dashboard + epics family) as one
/// JSON payload on stdout, then exit. This is the extension↔core data path ADR 0005 ratified: the thin TS shim
/// spawns this command and reads finished, CSP-safe HTML — all rendering stays in C#. A machine channel, not a
/// human one: stdout carries ONLY the JSON (no logo, no progress — Spectre output would corrupt the payload);
/// diagnostics go to stderr. [Story 6.4]
/// <para><b>Read-only by construction (AC #6):</b> the generation pass this command runs to populate the shared
/// models writes its scratch site into a per-project temp directory — never the project's configured output, and
/// never any source artifact — so opening the status panel leaves the project untouched. Pass <c>--output</c> to
/// direct the scratch site somewhere specific. Additive to the CLI: <c>generate</c>/<c>watch</c>/interactive
/// behavior is unchanged, and the pending Story 5.x CLI scope is untouched.</para></summary>
public sealed class WebviewCommand : Command<SiteSettings>
{
    /// <summary>Serializer options for the stdout payload: camelCase property names so the C# records read the
    /// same as the hand-named camelCase fields on the TS side. [Story 6.9]</summary>
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override int Execute(CommandContext context, SiteSettings settings, CancellationToken cancellationToken)
    {
        var resolved = settings.Resolve();
        var options = settings.Output is { Length: > 0 }
            ? resolved
            : RedirectOutputToScratch(resolved);

        var generator = new SiteGenerator(options);
        var events = generator.GenerateAll();
        foreach (var error in events.Where(e => e.Outcome == GenerationOutcome.Error))
        {
            // Non-fatal per the generator's own contract (sibling pages still rendered); surfaced for the shim's
            // stderr capture so a broken artifact is diagnosable from the extension host log.
            Console.Error.WriteLine($"[specscribe webview] {error.RelativePath}: {error.Message}");
        }

        var bundle = generator.RenderWebviewSurfaces();
        Console.Out.Write(SerializePayload(bundle, ResolveConfiguredOutputRoot(resolved)));
        return 0;
    }

    /// <summary>Serializes the stdout payload the TS shim parses: the entry document + every navigable surface,
    /// the configured output root, and the host-neutral <c>outline</c> (tree + status-bar summary). Extracted from
    /// <see cref="Execute"/> so the JSON contract the extension depends on — camelCase throughout, <c>surfaces</c>
    /// keyed by output-relative path, <c>outline</c> present — is unit-testable without a spawn. [Story 6.9]</summary>
    public static string SerializePayload(WebviewBundle bundle, string configuredOutputRoot)
    {
        var payload = new
        {
            siteTitle = bundle.SiteTitle,
            entry = bundle.EntryPath,
            document = bundle.EntryDocument,
            // Host-delivery of a core-resolved DATUM (not rendering — ADR 0005 §1): the workspace-relative
            // root a plain `generate` would write to. Sourced from the PRE-redirect resolved options so it is
            // the project's real configured output, never the temp scratch dir this command actually renders
            // into. The extension's "Open Generated Site" command joins this to the workspace folder to find
            // an already-generated index.html. [Story 6.8 AC #3, R2.4]
            configuredOutputRoot,
            // `sourcePath` (Story 6.10): the repo-relative artifact each surface was rendered from, so the webview's
            // "Open source" control can post `revealSource` and the shim opens it read-only. Null (dashboard) omits
            // the button host-side. A per-surface datum, not rendering — the generated site is unaffected.
            surfaces = bundle.Surfaces.ToDictionary(s => s.OutputRelativePath, s => new { title = s.Title, content = s.ContentHtml, sourcePath = s.SourcePath }),
            // Host-neutral epic/story outline + status-bar summary for the VS Code native surfaces (activity-bar
            // tree, status bar). Data, not rendering (ADR 0005 §1); its SurfacePaths are exactly the surface keys
            // above, so a tree click reveals the right surface. Emits no HTML — the generated site is unaffected.
            // [Story 6.9]
            outline = bundle.Outline,
        };
        // CamelCase so the nested `outline` record (PascalCase properties) serializes to the same convention as
        // the hand-named camelCase fields above — one consistent shape for the TS interface. The naming policy
        // touches property NAMES only, never string values or the `surfaces` dictionary KEYS (those are
        // output-relative paths, governed by DictionaryKeyPolicy, which stays default/none). [Story 6.9]
        return JsonSerializer.Serialize(payload, CamelCase);
    }

    /// <summary>The configured output root expressed relative to the repo root, with forward slashes so the
    /// value is stable across platforms (the VS Code shim joins it to the workspace folder). Falls back to the
    /// absolute path when the output root lives outside the repo (an explicit <c>--output</c> elsewhere) — the
    /// shim then opens it directly. Pure and side-effect-free so it is unit-testable without a spawn. [Story 6.8]</summary>
    public static string ResolveConfiguredOutputRoot(ForgeOptions resolved)
        => Path.GetRelativePath(resolved.RepoRoot, resolved.OutputRoot).Replace('\\', '/');

    /// <summary>Clones the resolved options with the output root moved to a stable per-project temp scratch
    /// directory (keyed by a hash of the repo root so concurrent projects never collide, stable so successive
    /// spawns overwrite rather than accumulate).</summary>
    private static ForgeOptions RedirectOutputToScratch(ForgeOptions options)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            options.RepoRoot.ToUpperInvariant())))[..16].ToLowerInvariant();
        return new ForgeOptions
        {
            RepoRoot = options.RepoRoot,
            SourceRoot = options.SourceRoot,
            AdrSourceRoot = options.AdrSourceRoot,
            AdrSourceExplicit = options.AdrSourceExplicit,
            OutputRoot = Path.Combine(Path.GetTempPath(), "specscribe-webview", key),
            SiteTitle = options.SiteTitle,
            IncludeReadme = options.IncludeReadme,
            DeepGitAnalytics = options.DeepGitAnalytics,
        };
    }
}

/// <summary>`specscribe watch` — build the site, then regenerate whenever a source file changes.</summary>
public sealed class WatchCommand : Command<SiteSettings>
{
    protected override int Execute(CommandContext context, SiteSettings settings, CancellationToken cancellationToken)
    {
        var options = settings.Resolve();
        ConsoleUi.PrintLogo();
        ConsoleUi.PrintPaths(options);
        var generator = GenerateCommand.RunGeneration(options);
        return RunWatchLoop(options, generator);
    }

    /// <summary>Blocks watching for changes until Ctrl+C (or process exit); shared with interactive runs.</summary>
    public static int RunWatchLoop(ForgeOptions options, SiteGenerator generator)
    {
        using var watcher = new FileWatcherService(options, generator, ConsoleUi.LogEvent);
        watcher.Start();
        ConsoleUi.PrintWatchingFooter();

        using var exitSignal = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitSignal.Set();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => exitSignal.Set();

        exitSignal.Wait();
        AnsiConsole.MarkupLine("[grey]Stopping...[/]");
        return 0;
    }
}

/// <summary>Default command: no arguments (or a failed parse) lands here and offers an interactive menu.</summary>
public sealed class InteractiveCommand : Command<SiteSettings>
{
    protected override int Execute(CommandContext context, SiteSettings settings, CancellationToken cancellationToken)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            // No terminal to prompt in (CI, redirected output) — show usage instead of hanging.
            ConsoleUi.PrintLogo();
            ConsoleUi.PrintUsage();
            return 0;
        }

        return RunMenu(settings);
    }

    public static int RunMenu(SiteSettings settings)
    {
        ConsoleUi.PrintLogo();
        ConsoleUi.PrintUsage();

        // Restore any previously saved paths, letting explicit CLI options take precedence.
        if (SettingsStore.TryLoad() is { } saved)
        {
            SettingsStore.ApplyTo(saved, settings);
            ConsoleUi.PrintSettingsLoaded(SettingsStore.ResolvePath(), saved);
        }

        while (true)
        {
            AnsiConsole.WriteLine();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]What would you like to do?[/]")
                    .AddChoices("Generate the site once", "Watch for changes", "Configure paths", "Exit"));

            switch (choice)
            {
                case "Generate the site once":
                {
                    if (TryResolve(settings) is not { } options) break;
                    ConsoleUi.PrintPaths(options);
                    GenerateCommand.RunGeneration(options);
                    break;
                }
                case "Watch for changes":
                {
                    if (TryResolve(settings) is not { } options) break;
                    ConsoleUi.PrintPaths(options);
                    var generator = GenerateCommand.RunGeneration(options);
                    return WatchCommand.RunWatchLoop(options, generator);
                }
                case "Configure paths":
                    ConfigurePaths(settings);
                    break;
                default:
                    return 0;
            }
        }
    }

    /// <summary>Resolves settings, turning a discovery failure into a hint instead of a crash so the
    /// user can fix things via "Configure paths" without restarting.</summary>
    private static ForgeOptions? TryResolve(SiteSettings settings)
    {
        try
        {
            return settings.Resolve();
        }
        catch (DirectoryNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            AnsiConsole.MarkupLine("[grey]Choose \"Configure paths\" to point SpecScribe at your project.[/]");
            return null;
        }
    }

    private static void ConfigurePaths(SiteSettings settings)
    {
        var defaults = TryResolveSilently(settings);

        settings.Source = PromptPath("Source artifacts directory", settings.Source ?? defaults?.SourceRoot);
        settings.Adrs = PromptPath("ADR directory", settings.Adrs ?? defaults?.AdrSourceRoot);
        settings.Output = PromptPath("Output directory", settings.Output ?? defaults?.OutputRoot);

        var name = AnsiConsole.Prompt(
            new TextPrompt<string>("Project name:")
                .DefaultValue(settings.ProjectName ?? defaults?.SiteTitle ?? ForgeOptions.DefaultSiteTitle));
        settings.ProjectName = name.Trim();

        // Deep git analytics is a configurable feature, so NFR7 (menu/CLI parity) requires it be reachable from
        // the interactive menu too, not just the --deep-git flag. Defaults to the current value so re-running
        // Configure paths doesn't silently flip it. [Story 3.2 Subtask 4.1]
        settings.DeepGit = AnsiConsole.Confirm(
            "Enable deep git analytics (change coupling and hotspots)?", defaultValue: settings.DeepGit);

        // Persist the choices so they're restored on the next run.
        if (SettingsStore.TrySave(settings) is { } savedPath)
        {
            ConsoleUi.PrintSettingsSaved(savedPath);
        }
    }

    private static ForgeOptions? TryResolveSilently(SiteSettings settings)
    {
        try
        {
            return settings.Resolve();
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string? PromptPath(string label, string? current)
    {
        var prompt = new TextPrompt<string>($"{label}:").AllowEmpty();
        if (current is { Length: > 0 })
        {
            prompt.DefaultValue(current);
        }

        var value = AnsiConsole.Prompt(prompt).Trim();
        if (value.Length > 0 && !Directory.Exists(value))
        {
            AnsiConsole.MarkupLine($"[yellow]Note:[/] [grey]{Markup.Escape(value)} does not exist yet.[/]");
        }

        return value.Length > 0 ? value : null;
    }
}
