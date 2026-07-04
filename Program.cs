using System.Diagnostics;
using System.Text;
using DocsForge;
using Spectre.Console;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Output is redirected somewhere that doesn't support changing the encoding (e.g. a pipe) — fine, Spectre falls back gracefully.
}

var once = args.Contains("--once", StringComparer.OrdinalIgnoreCase);

ForgeOptions options;
try
{
    options = ForgeOptions.Resolve();
}
catch (DirectoryNotFoundException ex)
{
    ConsoleUi.PrintFatalError(ex);
    return 1;
}

try
{
    ConsoleUi.PrintBanner(options);

    var generator = new SiteGenerator(options);

    var sw = Stopwatch.StartNew();
    var initialEvents = generator.GenerateAll();
    sw.Stop();
    ConsoleUi.PrintInitialSummary(initialEvents, sw.Elapsed);

    if (once)
    {
        return 0;
    }

    using var watcher = new FileWatcherService(options, generator, ConsoleUi.LogEvent);
    watcher.Start();
    ConsoleUi.PrintWatchingFooter();

    var exitSignal = new ManualResetEventSlim(false);
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
catch (Exception ex)
{
    ConsoleUi.PrintFatalError(ex);
    return 1;
}
