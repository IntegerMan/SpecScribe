using System.Diagnostics;
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
