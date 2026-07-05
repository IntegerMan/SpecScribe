using Spectre.Console;

namespace SpecScribe;

/// <summary>All Spectre.Console presentation, kept separate from generation logic.</summary>
public static class ConsoleUi
{
    public static void PrintLogo()
    {
        AnsiConsole.Write(new FigletText("SpecScribe").Color(Color.Orange3));
        AnsiConsole.MarkupLine("[grey]Spec-driven-development artifacts -> human-readable HTML, live.[/]");
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

    public static void PrintPaths(ForgeOptions options)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();
        grid.AddRow("[bold]Project[/]", $"[bold orange3]{Markup.Escape(options.SiteTitle)}[/]");
        grid.AddRow("[bold]Sources[/]", $"[yellow]{Markup.Escape(options.SourceRoot)}[/]");
        grid.AddRow("[bold]ADRs[/]", $"[yellow]{Markup.Escape(options.AdrSourceRoot)}[/]");
        grid.AddRow("[bold]Output[/]", $"[green]{Markup.Escape(options.OutputRoot)}[/]");
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    /// <summary>Runs a full generation pass with a live per-phase progress display.</summary>
    public static IReadOnlyList<GenerationEvent> RunWithProgress(SiteGenerator generator)
    {
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

    public static void PrintInitialSummary(IReadOnlyList<GenerationEvent> events, TimeSpan total)
    {
        var generated = events.Count(e => e.Outcome is GenerationOutcome.Generated or GenerationOutcome.Updated);
        var skipped = events.Count(e => e.Outcome == GenerationOutcome.Skipped);
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey37);
        table.AddColumn("Outcome");
        table.AddColumn(new TableColumn("Count").RightAligned());
        table.AddRow("[green]Generated[/]", generated.ToString());
        if (skipped > 0)
        {
            table.AddRow("[grey]Skipped[/]", skipped.ToString());
        }
        if (errors.Count > 0)
        {
            table.AddRow("[red]Errors[/]", errors.Count.ToString());
        }

        AnsiConsole.Write(table);

        foreach (var err in errors)
        {
            AnsiConsole.MarkupLine($"  [red]x[/] {Markup.Escape(err.RelativePath)} - {Markup.Escape(err.Message ?? "unknown error")}");
        }

        AnsiConsole.MarkupLine($"[grey]Initial build: {generated} page(s) in {total.TotalMilliseconds:0}ms[/]");
        AnsiConsole.WriteLine();
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

    public static void PrintFatalError(Exception ex)
    {
        AnsiConsole.MarkupLine($"[red bold]Fatal error:[/] {Markup.Escape(ex.Message)}");
    }
}
