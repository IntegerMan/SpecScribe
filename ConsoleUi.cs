using Spectre.Console;

namespace DocsForge;

/// <summary>All Spectre.Console presentation, kept separate from generation logic.</summary>
public static class ConsoleUi
{
    public static void PrintBanner(ForgeOptions options)
    {
        AnsiConsole.Write(new FigletText("DocsForge").Color(Color.Orange3));
        AnsiConsole.MarkupLine("[grey]BMad markdown -> stylized HTML, live.[/]");
        AnsiConsole.Write(new Rule().RuleStyle("grey37"));

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();
        grid.AddRow("[bold]Watching[/]", $"[yellow]{Markup.Escape(options.SourceRoot)}[/]");
        grid.AddRow("[bold]ADRs[/]", $"[yellow]{Markup.Escape(options.AdrSourceRoot)}[/]");
        grid.AddRow("[bold]Output[/]", $"[green]{Markup.Escape(options.OutputRoot)}[/]");
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
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
