using Spectre.Console;

namespace SpecScribe;

/// <summary>All Spectre.Console presentation, kept separate from generation logic.</summary>
public static class ConsoleUi
{
    public static void PrintLogo()
    {
        AnsiConsole.Write(new FigletText("SpecScribe").Color(Color.Orange3));
        AnsiConsole.MarkupLine("[grey][link=https://github.com/IntegerMan/SpecScribe]Interactive documentation generator for Spec-Driven Development[/] · Created by [link=https://MattEland.dev]Matthew-Hope Eland[/][/]");
        AnsiConsole.Write(new Rule().RuleStyle("grey37"));
    }

    public static void PrintUsage()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();
        grid.AddRow("[bold]specscribe generate[/]", "[grey]build the site once and exit[/]");
        grid.AddRow("[bold]specscribe watch[/]", "[grey]build, then regenerate on every save (Ctrl+C to stop)[/]");
        grid.AddRow("[bold]specscribe --help[/]", "[grey]all options (--source, --adrs, --output, --project-name)[/]");
        AnsiConsole.Write(grid);
        AnsiConsole.MarkupLine("[grey]Run from inside a BMad project and paths are discovered automatically.[/]");
    }

    /// <summary>Notes that persisted settings were found on startup, listing the values that were restored.</summary>
    public static void PrintSettingsLoaded(string path, SavedSettings saved)
    {
        AnsiConsole.MarkupLine($"[grey]Loaded saved settings from[/] [green]{Markup.Escape(path)}[/]");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();
        AddSettingRow(grid, "Source", saved.Source);
        AddSettingRow(grid, "ADRs", saved.Adrs);
        AddSettingRow(grid, "Output", saved.Output);
        AddSettingRow(grid, "Project", saved.ProjectName);
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    private static void AddSettingRow(Grid grid, string label, string? value)
    {
        if (value is { Length: > 0 })
        {
            grid.AddRow($"[grey]{label}[/]", $"[grey]{Markup.Escape(value)}[/]");
        }
    }

    /// <summary>Confirms that the just-configured settings were written to disk.</summary>
    public static void PrintSettingsSaved(string path)
    {
        AnsiConsole.MarkupLine($"[grey]Saved settings to[/] [green]{Markup.Escape(path)}[/]");
    }

    public static void PrintPaths(ForgeOptions options)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();
        grid.AddRow("[bold]Project[/]", $"[bold orange3]{Markup.Escape(options.SiteTitle)}[/]");
        grid.AddRow("[bold]Sources[/]", $"[yellow]{Markup.Escape(options.SourceRoot)}[/]");
        grid.AddRow("[bold]ADRs[/]", FormatAdrPath(options));
        grid.AddRow("[bold]Output[/]", $"[green]{Markup.Escape(options.OutputRoot)}[/]");
        AnsiConsole.Write(grid);

        // ADRs are optional, so a missing default folder is silent — but an explicitly pointed-at folder that
        // doesn't exist is almost always a typo, so call it out loudly.
        if (options.AdrSourceExplicit && !Directory.Exists(options.AdrSourceRoot))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]![/] [yellow]ADR directory not found:[/] [grey]{Markup.Escape(options.AdrSourceRoot)}[/] [grey](no ADRs will be rendered)[/]");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>Renders the ADR path, tagging a defaulted-and-absent folder as optional so the user knows the
    /// missing directory is expected rather than an error.</summary>
    private static string FormatAdrPath(ForgeOptions options)
    {
        var path = $"[yellow]{Markup.Escape(options.AdrSourceRoot)}[/]";
        if (!options.AdrSourceExplicit && !Directory.Exists(options.AdrSourceRoot))
        {
            path += " [grey](optional, none found)[/]";
        }
        return path;
    }

    /// <summary>Prints a clickable <c>file://</c> URL to the generated index so the site can be opened straight
    /// from the terminal (Ctrl+Click in most terminals).</summary>
    public static void PrintOutputLink(ForgeOptions options)
    {
        var indexPath = Path.Combine(options.OutputRoot, "index.html");
        if (!File.Exists(indexPath))
        {
            return;
        }

        var uri = new Uri(indexPath).AbsoluteUri;
        AnsiConsole.MarkupLine($"[grey]Open the site (Ctrl+Click):[/] [link={uri}]{Markup.Escape(uri)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>True when there is a real terminal to paint — the single TTY signal the whole CLI branches on
    /// (also used by <c>Program.cs</c>'s menu fallback and <see cref="InteractiveCommand"/>). Wrapped here so the
    /// interactive/non-interactive decision has one name rather than a repeated property chain. [Story 5.1 AC #3]</summary>
    private static bool IsInteractive => AnsiConsole.Profile.Capabilities.Interactive;

    /// <summary>Runs a full generation pass, with a live per-phase progress display when a terminal can animate one.
    /// <para>The non-interactive branch (CI, piped or redirected stdout) is EXPLICIT rather than leaning on Spectre's
    /// own degradation of <see cref="AnsiConsole.Progress"/>: nothing about the run should depend on cursor control,
    /// and a live display that silently becomes a no-op is a behavior we would not notice regressing. [AC #3]</para></summary>
    public static IReadOnlyList<GenerationEvent> RunWithProgress(SiteGenerator generator)
    {
        if (!IsInteractive)
        {
            // No reporter at all — GenerateAll null-checks every phase callback, so this is the silent path.
            return generator.GenerateAll();
        }

        IReadOnlyList<GenerationEvent> events = Array.Empty<GenerationEvent>();
        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn(), new SpinnerColumn())
            .Start(ctx =>
            {
                events = generator.GenerateAll(new SpectreGenerationReporter(ctx));
            });
        return events;
    }

    /// <summary>Prints the end-of-build feedback: the rounded counts table for humans, every failing path either
    /// way, and — always, in both modes — the machine-parseable summary line. [AC #1, #3, #4]</summary>
    public static void PrintInitialSummary(IReadOnlyList<GenerationEvent> events, TimeSpan total)
    {
        var counts = GenerationSummary.Count(events);
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();

        if (IsInteractive)
        {
            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey37);
            table.AddColumn("Outcome");
            table.AddColumn(new TableColumn("Count").RightAligned());
            table.AddRow("[green]Generated[/]", counts.Written.ToString());
            if (counts.Skipped > 0)
            {
                table.AddRow("[grey]Skipped[/]", counts.Skipped.ToString());
            }
            if (counts.Errors > 0)
            {
                table.AddRow("[red]Errors[/]", counts.Errors.ToString());
            }

            AnsiConsole.Write(table);
        }

        // Failing paths are surfaced in BOTH modes — swallowing them would leave a non-zero exit code with no way
        // to tell WHICH page failed, which is exactly what CI needs. [AC #4]
        foreach (var err in errors)
        {
            AnsiConsole.MarkupLine($"  [red]x[/] {Markup.Escape(err.RelativePath)} - {Markup.Escape(err.Message ?? "unknown error")}");
        }

        // Prose counts in BOTH modes — plain text, no cursor control, so it degrades cleanly. The rounded table is
        // the part that is suppressed when non-interactive, not the numbers themselves. [AC #3]
        AnsiConsole.MarkupLine($"[grey]Initial build: {counts.Written} page(s) in {total.TotalMilliseconds:0}ms[/]");

        PrintMachineSummary(counts, total);

        if (IsInteractive)
        {
            AnsiConsole.WriteLine();
        }
    }

    /// <summary>Emits the one-line machine-parseable summary (UX-DR15). Written straight to
    /// <see cref="Console.Out"/>, NOT through <see cref="AnsiConsole"/>: Spectre word-wraps at the profile width
    /// (80 columns once output is redirected), which would split the very line CI is meant to grep as a unit.
    /// Bypassing the markup pipeline also guarantees no escape sequences and no accidental markup interpretation.
    /// <para>Printed on every run, interactive or not — the pretty table is for humans and this is for machines;
    /// neither substitutes for the other. Kept as one helper so Story 5.3's per-rebuild watch summary can reuse the
    /// identical shape rather than growing a second, drifting format.</para></summary>
    private static void PrintMachineSummary(GenerationCounts counts, TimeSpan total)
        => Console.Out.WriteLine(GenerationSummary.FormatLine(counts, total));

    public static void LogEvent(GenerationEvent ev)
    {
        var (icon, color, verb) = ev.Outcome switch
        {
            GenerationOutcome.Generated => ("+", "green", "generated"),
            GenerationOutcome.Updated => ("~", "yellow", "updated"),
            GenerationOutcome.Removed => ("-", "orange3", "removed"),
            GenerationOutcome.Skipped => (".", "grey50", "skipped"),
            GenerationOutcome.Error => ("x", "red", "error"),
            _ => ("?", "grey", "unknown"),
        };

        var time = DateTime.Now.ToString("HH:mm:ss");
        var path = Markup.Escape(ev.RelativePath.Replace('\\', '/'));
        var detail = ev.Message is { Length: > 0 } msg
            ? Markup.Escape(msg)
            : $"{ev.Elapsed.TotalMilliseconds:0}ms";

        AnsiConsole.MarkupLine($"[grey58]{time}[/]  [{color}]{icon} {verb}[/]  {path}  [grey50]{detail}[/]");
    }

    public static void PrintWatchingFooter()
    {
        AnsiConsole.Write(new Rule().RuleStyle("grey37"));
        AnsiConsole.MarkupLine("[grey]Watching for changes - press [bold]Ctrl+C[/] to stop.[/]");
    }

    /// <summary>A Spectre console bound to <see cref="Console.Error"/> so fatal errors land on stderr, never stdout.
    /// The <c>webview</c> command's stdout is a machine-parsed JSON contract and the VS Code extension only surfaces
    /// the renderer's stderr on a non-zero exit — writing fatal errors to stdout (the old behavior) both corrupted
    /// that channel and left the extension reporting a useless "(no stderr)". Lazily created so we never touch the
    /// stderr stream on the common no-error path.</summary>
    private static readonly Lazy<IAnsiConsole> ErrorConsole = new(() =>
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) }));

    public static void PrintFatalError(Exception ex)
    {
        ErrorConsole.Value.MarkupLine($"[red bold]Fatal error:[/] {Markup.Escape(ex.Message)}");
    }
}
