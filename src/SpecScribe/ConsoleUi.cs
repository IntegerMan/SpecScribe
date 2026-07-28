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
        // The non-path preferences are restored too and materially change the run, so a CLI user who never opened
        // the menu can see why the README vanished or why the deep-git pass is running. [Story 5.2 AC #4]
        AddSettingRow(grid, "README", saved.IncludeReadme switch { true => "included", false => "excluded", null => null });
        AddSettingRow(grid, "Deep git", saved.DeepGit == true ? "enabled" : null);
        AddSettingRow(grid, "Code URL", saved.CodeUrl);
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

    /// <summary>The always-printed paths block. When <paramref name="provenance"/> is supplied (every CLI and menu
    /// run since Story 5.2), each row carries a dim tag naming what supplied the value — the flag, the settings
    /// file, or auto-discovery — so "which source won" is answerable at a glance on an ordinary run rather than
    /// only under a diagnostic flag. Omitting it keeps the pre-5.2 unannotated rendering for any caller that has
    /// only a bare <see cref="ForgeOptions"/>. [Story 5.2 AC #2]</summary>
    public static void PrintPaths(ForgeOptions options, IReadOnlyList<ConfigProvenance>? provenance = null)
    {
        var by = provenance?.ToDictionary(p => p.Field, StringComparer.Ordinal);
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();
        grid.AddRow("[bold]Project[/]", $"[bold orange3]{Markup.Escape(options.SiteTitle)}[/]{Tag(by, SettingsResolver.Fields.Project)}");
        grid.AddRow("[bold]Sources[/]", $"[yellow]{Markup.Escape(options.SourceRoot)}[/]{Tag(by, SettingsResolver.Fields.Source)}");
        grid.AddRow("[bold]ADRs[/]", FormatAdrPath(options) + Tag(by, SettingsResolver.Fields.Adrs));
        grid.AddRow("[bold]Output[/]", $"[green]{Markup.Escape(options.OutputRoot)}[/]{Tag(by, SettingsResolver.Fields.Output)}");
        AnsiConsole.Write(grid);

        // ADRs are optional, so a missing default folder is silent — but an explicitly pointed-at folder that
        // doesn't exist is almost always a typo, so call it out loudly.
        if (options.AdrSourceExplicit && !Directory.Exists(options.AdrSourceRoot))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]![/] [yellow]ADR directory not found:[/] [grey]{Markup.Escape(options.AdrSourceRoot)}[/] [grey](no ADRs will be rendered)[/]");
        }

        // A pinned date-page cutoff is echoed back in ISO form on an ORDINARY run, not only under --show-config:
        // --as-of parses forgivingly (07/27/2026 is accepted), and forgiving input is only acceptable while a
        // misparse is immediately visible rather than silently shifting which date pages exist. Conditional, and
        // shaped like the ADR line above rather than a grid row — a permanent row would change every ordinary run's
        // output for a setting almost nobody sets. [Story 5.7 D3 / AC #2a]
        if (FormatPinnedCutoffLine(options.DateCutoff) is { } pinnedCutoffLine)
        {
            AnsiConsole.MarkupLine(pinnedCutoffLine);
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>The AC #2a pinned-cutoff line's content, or null when the cutoff isn't a dated fixed policy — split
    /// out as a pure, Spectre-free function (same discipline as <see cref="GenerationSummary.FormatLine"/>) so this
    /// story's echo requirement is unit-testable without driving a live <c>AnsiConsole</c>, which would test
    /// Spectre, not us (see <c>CliFeedbackTests</c>). [Review][Patch]</summary>
    internal static string? FormatPinnedCutoffLine(DateCutoff cutoff) => cutoff is { Policy: DatePolicy.AsOf, AsOf: { } pinned }
        ? $"[grey]i[/] [grey]Date-page cutoff pinned to[/] [cyan]{PortalDates.IsoDay(pinned)}[/] [grey](--as-of)[/]"
        : null;

    /// <summary>The standard "what am I about to build, and from where" preamble for a resolved run: what the
    /// settings file restored (when one contributed) followed by the provenance-annotated paths block. One helper so
    /// <c>generate</c>, <c>watch</c>, and the interactive menu cannot drift into showing different amounts of the
    /// same information. [Story 5.2 AC #1]</summary>
    public static void PrintResolvedConfig(ResolvedConfig resolved)
    {
        if (resolved.Saved is { } saved && resolved.SavedSettingsPath is { } path)
        {
            PrintSettingsLoaded(path, saved);
        }

        PrintPaths(resolved.Options, resolved.Provenance);
    }

    /// <summary>The dim provenance suffix for one row, or nothing when the run supplied no provenance. The tag text
    /// itself comes from <see cref="SettingsResolver.DisplayTag"/> — this file paints, it does not decide.</summary>
    private static string Tag(IReadOnlyDictionary<string, ConfigProvenance>? by, string field)
        => by is not null && by.TryGetValue(field, out var entry)
            ? $" [grey]({Markup.Escape(SettingsResolver.DisplayTag(entry))})[/]"
            : string.Empty;

    /// <summary>The <c>--show-config</c> surface: the effective configuration and its provenance as machine-parseable
    /// lines, written straight to <see cref="Console.Out"/> rather than through <see cref="AnsiConsole"/> — Spectre
    /// word-wraps at the profile width (80 columns once output is redirected) and would split lines carrying absolute
    /// paths, which are exactly the ones long enough to wrap. Same reasoning, and the same channel discipline, as the
    /// run summary line. The human-readable view is the annotated paths block; this is its <c>grep</c>-able twin, and
    /// neither substitutes for the other. [Story 5.2 AC #2]</summary>
    public static void PrintConfigDiagnostics(ResolvedConfig resolved)
    {
        try
        {
            foreach (var line in SettingsResolver.FormatConfigLines(resolved))
            {
                Console.Out.WriteLine(line);
            }
        }
        catch (IOException)
        {
            // Downstream reader gone (e.g. `specscribe generate --show-config | head`) — the run itself already
            // succeeded, so this must not surface as a fatal error. Same guard, same reason, as PrintMachineSummary.
            // [Review][Patch]
        }
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

    /// <summary>True when there is a real terminal to paint — the single TTY signal the whole CLI branches on.
    /// Reused as-is by <c>Program.cs</c>'s menu fallback and by <see cref="InteractiveCommand"/> so the
    /// interactive/non-interactive decision has one name and one source rather than three separate calls to
    /// <see cref="AnsiConsole.Profile"/>. [Story 5.1 AC #3]</summary>
    internal static bool IsInteractive => AnsiConsole.Profile.Capabilities.Interactive;

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
    {
        try
        {
            Console.Out.WriteLine(GenerationSummary.FormatLine(counts, total));
        }
        catch (IOException)
        {
            // Downstream reader gone (e.g. `specscribe generate | head`) — generation itself already succeeded, so
            // this must not surface as a fatal error and flip an otherwise-successful run's exit code.
        }
    }

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
