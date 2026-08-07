using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using SpecScribe;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Output is redirected somewhere that doesn't support changing the encoding (e.g. a pipe) — fine, Spectre falls back gracefully.
}

var app = new CommandApp<InteractiveCommand>();
app.Configure(config =>
{
    config.SetApplicationName("specscribe");
    // Enables Spectre's built-in `-v` / `--version`, which "requires ApplicationVersion to be configured in the
    // application" — without this call the global flag is not a flag at all, and UseStrictParsing below turns it
    // into `Unknown option 'version'` (exit 1). On a terminal that then falls through to the interactive menu
    // via the CommandParseException arm, so `specscribe --version` misfired as a MENU rather than as an error.
    // [Story 16.3 AC #2]
    //
    // Sourced from the assembly through ProductMetadata rather than from a literal, so the CLI and the About
    // page cannot drift: both now read the same MinVer-derived InformationalVersion, with the "+<sha>" build
    // metadata split off (the hash is shown on the About page's Build row, not in `--version`).
    config.SetApplicationVersion(ProductMetadata.FromAssembly().Version);
    config.UseStrictParsing(); // typo'd options should fail loudly (and fall back to the menu), not be ignored

    // Real, runnable invocations on `specscribe --help`. The zero-flag forms come first because auto-discovery is
    // the headline promise (AC #1); the explicit-path form documents the non-default-layout escape hatch (AC #2).
    // [Story 5.1 Task 3]
    config.AddExample(["generate"]);
    config.AddExample(["watch"]);
    config.AddExample(["generate", "--source", "./_bmad-output", "--output", "./site"]);
    config.AddExample(["generate", "--deep-git"]);

    config.AddCommand<GenerateCommand>("generate")
        .WithDescription("Generate the documentation site once and exit. Exits non-zero if any page fails to render.")
        .WithExample(["generate"])
        .WithExample(["generate", "--source", "./_bmad-output", "--output", "./site"]);
    config.AddCommand<WatchCommand>("watch")
        .WithDescription("Generate the site, then regenerate whenever a source file changes. Ctrl+C to stop.")
        .WithExample(["watch"])
        // Single-token values only in examples: Spectre renders an example by joining its args with spaces, so a
        // value containing a space would print unquoted and be un-runnable as shown.
        .WithExample(["watch", "--adrs", "./docs/decisions"]);
    config.AddCommand<WebviewCommand>("webview")
        .WithDescription("Render the VS Code webview surface bundle as JSON on stdout (used by the SpecScribe extension).");
    // The non-interactive door onto `.specscribe` — until this existed, the settings could only be written by the
    // interactive menu, which is why the extension could not offer to configure anything. [ADR 0037]
    config.AddCommand<ConfigCommand>("config")
        .WithDescription("Show, export or write the directory-scoped .specscribe settings.")
        .WithExample(["config"])
        .WithExample(["config", "--json"])
        .WithExample(["config", "--save", "--deep-git"])
        .WithExample(["config", "--save", "--clear", "code_url"]);
    config.PropagateExceptions();
});

try
{
    return app.Run(args);
}
catch (CommandParseException ex)
{
    // Bad or unknown arguments: explain, then drop into the interactive menu when a human is present.
    ConsoleUi.PrintFatalError(ex);
    if (ConsoleUi.IsInteractive)
    {
        AnsiConsole.WriteLine();
        return InteractiveCommand.RunMenu(new SiteSettings());
    }
    return ExitCodes.Failure;
}
catch (DirectoryNotFoundException ex)
{
    ConsoleUi.PrintFatalError(ex);
    return ExitCodes.Failure;
}
catch (Exception ex)
{
    ConsoleUi.PrintFatalError(ex);
    return ExitCodes.Failure;
}
