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
    config.UseStrictParsing(); // typo'd options should fail loudly (and fall back to the menu), not be ignored
    config.AddCommand<GenerateCommand>("generate")
        .WithDescription("Generate the documentation site once and exit.");
    config.AddCommand<WatchCommand>("watch")
        .WithDescription("Generate the site, then regenerate whenever a source file changes.");
    config.AddCommand<WebviewCommand>("webview")
        .WithDescription("Render the VS Code webview surface bundle as JSON on stdout (used by the SpecScribe extension).");
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
    if (AnsiConsole.Profile.Capabilities.Interactive)
    {
        AnsiConsole.WriteLine();
        return InteractiveCommand.RunMenu(new SiteSettings());
    }
    return 1;
}
catch (DirectoryNotFoundException ex)
{
    ConsoleUi.PrintFatalError(ex);
    return 1;
}
catch (Exception ex)
{
    ConsoleUi.PrintFatalError(ex);
    return 1;
}
