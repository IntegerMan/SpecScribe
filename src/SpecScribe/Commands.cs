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
/// diagnostics go to stderr as structured JSON lines (one object per line: <c>path</c>, <c>severity</c>,
/// <c>message</c>, <c>fileAnchored</c>) that the shim maps into the VS Code Problems panel — never intermixed
/// with the stdout payload. [Story 6.4, Story 6.12]
/// <para><b>Read-only by construction (AC #6):</b> the generation pass this command runs to populate the shared
/// models writes its scratch site into a per-project temp directory — never the project's configured output, and
/// never any source artifact — so opening the status panel leaves the project untouched. Pass <c>--output</c> to
/// direct the scratch site somewhere specific. Additive to the CLI: <c>generate</c>/<c>watch</c>/interactive
/// behavior is unchanged, and the pending Story 5.x CLI scope is untouched.</para></summary>
public sealed class WebviewCommand : Command<SiteSettings>
{
    /// <summary>Serializer options for the stdout payload: camelCase property names so the C# records read the
    /// same as the hand-named camelCase fields on the TS side. [Story 6.9]
    /// <para>Relaxed escaping: the payload is now dominated by surface HTML (whole-site surfaces —
    /// spec-webview-doc-page-surfaces), and the default encoder turns every <c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c>/quote
    /// into a 6-byte <c>\uXXXX</c> sequence — a material fraction of the bundle. Safe here because the payload is
    /// only ever <c>JSON.parse</c>d by the shim, never embedded into markup.</para></summary>
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    protected override int Execute(CommandContext context, SiteSettings settings, CancellationToken cancellationToken)
    {
        // Tolerant resolution: the extension must render in ANY workspace, so a folder with no `_bmad-output` marker
        // must NOT throw the CLI's "run from inside a BMad project" error — it falls back to the cwd as the repo root
        // and lets the Directory.Exists-guarded generation degrade to README + Code Map + git-if-present. The
        // interactive/CLI generate/watch commands keep the throwing Resolve() (CLI honesty).
        // [spec-vscode-any-workspace-and-processing-indicators]
        var resolved = settings.ResolveTolerant();
        var options = settings.Output is { Length: > 0 }
            ? resolved
            : RedirectOutputToScratch(resolved);

        var generator = new SiteGenerator(options);
        // Capture every long-tail page at the write seam so RenderWebviewSurfaces can turn docs/ADRs/requirements
        // pages into navigable surfaces — the panel's header nav works in-editor. Memory-only: the scratch output's
        // written bytes are unchanged. [spec-webview-doc-page-surfaces]
        generator.CapturePages = true;
        var events = generator.GenerateAll();
        // The run's non-fatal notices as structured JSON lines on stderr — the SAME DiagnosticNotice.FromEvents
        // projection the Story 4.8 diagnostics page renders (coherence: the two surfaces can never disagree). This
        // covers BOTH Error and Skipped (the pre-6.12 human loop missed Skipped) and emits nothing on a clean run
        // (degrade-clean). Path roots come from the PRE-redirect `resolved`, not the scratch-redirected `options`,
        // so an anchored path points at the project's real source. [Story 6.12]
        var notices = DiagnosticNotice.FromEvents(events);
        Console.Error.Write(SerializeDiagnostics(notices, resolved));

        var bundle = generator.RenderWebviewSurfaces();
        // Resolved roots come from the PRE-redirect `resolved` (the project's real roots), never the scratch-redirected
        // `options`, exactly like configuredOutputRoot. [Story 6.11]
        var payload = SerializePayload(
            bundle,
            ResolveConfiguredOutputRoot(resolved),
            ResolveSourceRoot(resolved),
            ResolveAdrRoot(resolved),
            ResolveRepoRootOffset(resolved));

        if (!settings.Serve)
        {
            Console.Out.Write(payload);
            return 0;
        }

        // Persistent mode (deferred item, Story 6.4 review — ADR 0005 §3's "scoped re-render" follow-up): stream
        // one NDJSON payload line per debounced incremental regen instead of exiting after a single render, by
        // reusing the EXACT debounce/RegenerateEpics() incremental path `specscribe watch` already relies on
        // (FileWatcherService). The extension keeps this one process alive for the panel's lifetime instead of
        // spawning a fresh `specscribe webview` process — and therefore a fresh full GenerateAll() — on every save.
        Console.Out.WriteLine(payload);
        Console.Out.Flush();
        return RunServeLoop(options, resolved, generator);
    }

    /// <summary>The persistent-mode loop: rewrites and re-streams the webview payload after every debounced
    /// incremental regen, then blocks until the process is torn down (Ctrl+C in an interactive shell, or the host
    /// simply killing the process — the same lifecycle <see cref="WatchCommand.RunWatchLoop"/> uses). One JSON
    /// object per line on stdout, same shape as the one-shot path's <see cref="SerializePayload"/> output, so the
    /// extension's parser needs no branch on which mode produced it. [Deferred item, Story 6.4 review]</summary>
    private static int RunServeLoop(ForgeOptions options, ForgeOptions resolved, SiteGenerator generator)
    {
        // FileWatcherService fires one debounce Timer PER distinct changed path, each on its own thread-pool
        // thread — two files touched inside the same debounce window can invoke this callback concurrently.
        // `_pushLock` serializes the whole read-render-write sequence so two regens can never interleave their
        // writes into the single NDJSON stdout stream. [Review][Patch]
        var pushLock = new object();
        using var watcher = new FileWatcherService(options, generator, ev =>
        {
            lock (pushLock)
            {
                var notices = DiagnosticNotice.FromEvents(new[] { ev });
                Console.Error.Write(SerializeDiagnostics(notices, resolved));
                if (ev.Outcome is GenerationOutcome.Error or GenerationOutcome.Skipped)
                {
                    // Error: the generator's in-memory state is unchanged — nothing to re-render or stream, the
                    // notice above already surfaced the failure. Skipped: an admitted-but-unrecognized touch (or
                    // watcher noise) produced no site delta — re-rendering would just waste the exact per-push
                    // cost this mode exists to eliminate and flicker the panel with an unchanged payload.
                    // [Review][Patch]
                    return;
                }
                var bundle = generator.RenderWebviewSurfaces();
                var payload = SerializePayload(
                    bundle,
                    ResolveConfiguredOutputRoot(resolved),
                    ResolveSourceRoot(resolved),
                    ResolveAdrRoot(resolved),
                    ResolveRepoRootOffset(resolved));
                Console.Out.WriteLine(payload);
                Console.Out.Flush();
            }
        });
        watcher.Start();

        using var exitSignal = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitSignal.Set();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => exitSignal.Set();
        exitSignal.Wait();
        return 0;
    }

    /// <summary>Serializes the stdout payload the TS shim parses: the entry document + every navigable surface,
    /// the configured output root, and the host-neutral <c>outline</c> (tree + status-bar summary). Extracted from
    /// <see cref="Execute"/> so the JSON contract the extension depends on — camelCase throughout, <c>surfaces</c>
    /// keyed by output-relative path, <c>outline</c> present — is unit-testable without a spawn. [Story 6.9]</summary>
    public static string SerializePayload(
        WebviewBundle bundle,
        string configuredOutputRoot,
        string sourceRoot = SourceDirDefault,
        string adrRoot = AdrDirDefault,
        string repoRoot = ".")
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
            // Resolved watch roots (Story 6.11), same host-delivery-of-a-core-datum pattern as configuredOutputRoot
            // (ADR 0005 §1 — data, not rendering; the generated site is unaffected): the source/ADR roots the shim
            // builds its file watchers from (repo-relative, so a non-default --source/--adrs still watches the right
            // tree), and the repo-root offset (workspace-relative) it joins those AND Story 6.10's reveal-source
            // against — so a subdir-open (repo root ≠ workspace folder) watches and reveals correctly.
            sourceRoot,
            adrRoot,
            repoRoot,
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

    /// <summary>Projects the run's non-fatal <see cref="DiagnosticNotice"/> notices into the JSON-lines stderr
    /// payload the VS Code shim parses into the Problems panel — one JSON object per line (newline-terminated),
    /// or the empty string when the run is clean (so an all-clear run adds no Problems noise). Pure and
    /// spawn-free, mirroring <see cref="SerializePayload"/>/<see cref="ResolveConfiguredOutputRoot"/> so the wire
    /// contract is unit-testable without launching the command. Each line carries exactly <c>path</c>,
    /// <c>severity</c> (<c>"error"</c>|<c>"warning"</c>), <c>message</c>, and <c>fileAnchored</c>.
    /// <para>Path resolution: a source-anchored notice (<see cref="DiagnosticNotice.AnchorRoot"/> ==
    /// <see cref="DiagnosticAnchorRoot.Source"/>) becomes a repo-relative, forward-slashed path combined with
    /// <see cref="ForgeOptions.SourceRoot"/> (the shim joins it to the workspace folder to build a real file
    /// <c>Uri</c>) — the same convention as <see cref="ResolveConfiguredOutputRoot"/>. An ADR-anchored notice
    /// (<see cref="DiagnosticAnchorRoot.Adr"/>) combines with <see cref="ForgeOptions.AdrSourceRoot"/> instead —
    /// its <see cref="DiagnosticNotice.SourcePath"/> is relative to the ADR OUTPUT subdir, not the source root, so
    /// combining it with <see cref="ForgeOptions.SourceRoot"/> would resolve to a nonexistent file. [Review][Patch]
    /// A render-time notice (<see cref="DiagnosticAnchorRoot.None"/>) keeps its output-relative <c>.html</c> path
    /// verbatim and rides the wire for page-coherence but is not file-anchored (the shim leaves it on the
    /// diagnostics page). Roots come from the PRE-redirect <paramref name="resolved"/> so anchored paths point at
    /// the project's real source, never the scratch output. [Story 6.12]</para></summary>
    public static string SerializeDiagnostics(IReadOnlyList<DiagnosticNotice> notices, ForgeOptions resolved)
    {
        if (notices.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var notice in notices)
        {
            var path = notice.AnchorRoot switch
            {
                DiagnosticAnchorRoot.Source =>
                    Path.GetRelativePath(resolved.RepoRoot, Path.Combine(resolved.SourceRoot, notice.SourcePath)).Replace('\\', '/'),
                DiagnosticAnchorRoot.Adr =>
                    Path.GetRelativePath(resolved.RepoRoot, Path.Combine(resolved.AdrSourceRoot, StripAdrOutputPrefix(notice.SourcePath))).Replace('\\', '/'),
                _ => notice.SourcePath.Replace('\\', '/'),
            };
            // Self-describing single-line Problems entry: prefix the category so a bare skip (null Message) still
            // reads, and the fine ingest category is visible. Raw text — never linkified (Problems shows one line).
            var message = notice.Message is null ? notice.Category : $"{notice.Category}: {notice.Message}";
            var line = new
            {
                path,
                severity = notice.Severity == DiagnosticSeverity.Error ? "error" : "warning",
                message,
                fileAnchored = notice.AnchorRoot != DiagnosticAnchorRoot.None,
            };
            // Same camelCase policy as the stdout payload so the TS RawDiagnostic interface reads one convention.
            sb.Append(JsonSerializer.Serialize(line, CamelCase));
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>An ADR-anchored notice's <see cref="DiagnosticNotice.SourcePath"/> carries the ADR OUTPUT subdir
    /// prefix (<see cref="ForgeOptions.AdrOutputSubdir"/>, e.g. <c>"adrs/0007-foo.md"</c> — the shape
    /// <c>GenerateAdrsInternal</c> uses for the diagnostics page's source-column display), not a path relative to
    /// <see cref="ForgeOptions.AdrSourceRoot"/> directly. Strip it before combining with
    /// <see cref="ForgeOptions.AdrSourceRoot"/>, so the real on-disk ADR file resolves instead of a nonexistent
    /// <c>AdrSourceRoot/adrs/…</c> path. [Story 6.12] [Review][Patch]</summary>
    private static string StripAdrOutputPrefix(string sourceRelative)
    {
        var prefix = ForgeOptions.AdrOutputSubdir + "/";
        return sourceRelative.StartsWith(prefix, StringComparison.Ordinal)
            ? sourceRelative[prefix.Length..]
            : sourceRelative;
    }

    /// <summary>The configured output root expressed relative to the repo root, with forward slashes so the
    /// value is stable across platforms (the VS Code shim joins it to the workspace folder). Falls back to the
    /// absolute path when the output root lives outside the repo (an explicit <c>--output</c> elsewhere) — the
    /// shim then opens it directly. Pure and side-effect-free so it is unit-testable without a spawn. [Story 6.8]</summary>
    public static string ResolveConfiguredOutputRoot(ForgeOptions resolved)
        => Path.GetRelativePath(resolved.RepoRoot, resolved.OutputRoot).Replace('\\', '/');

    /// <summary>Fallback roots the shim uses when an older core omits the Story 6.11 roots — the default source/ADR
    /// layout, matching what those roots resolve to on a default project. Also the <see cref="SerializePayload"/>
    /// parameter defaults so the older 2-arg test/call sites keep a sensible shape. [Story 6.11]</summary>
    private const string SourceDirDefault = ForgeOptions.SourceDirName;
    private const string AdrDirDefault = "docs/adrs";

    /// <summary>The source-artifact root (<c>_bmad-output</c>, or a custom <c>--source</c>) expressed relative to the
    /// repo root with forward slashes — the watcher base the VS Code shim builds its <c>RelativePattern</c> from
    /// (constraint #1: no path literal in TS). Pure and side-effect-free, mirroring
    /// <see cref="ResolveConfiguredOutputRoot"/>. [Story 6.11]</summary>
    public static string ResolveSourceRoot(ForgeOptions resolved)
        => Path.GetRelativePath(resolved.RepoRoot, resolved.SourceRoot).Replace('\\', '/');

    /// <summary>The ADR source root (<c>docs/adrs</c>, or a resolved fallback / explicit <c>--adrs</c>) relative to
    /// the repo root with forward slashes — the second watcher base. Pure. [Story 6.11]</summary>
    public static string ResolveAdrRoot(ForgeOptions resolved)
        => Path.GetRelativePath(resolved.RepoRoot, resolved.AdrSourceRoot).Replace('\\', '/');

    /// <summary>The repo root expressed relative to the process working directory (the workspace folder the shim
    /// spawned us in) with forward slashes — so the shim resolves the ABSOLUTE repo root once
    /// (<c>path.resolve(folder, repoRoot)</c>) and anchors BOTH the watchers and Story 6.10's reveal-source join to
    /// it, correct even when the editor is opened on a subdirectory (repo root ≠ workspace folder). <c>"."</c> at the
    /// repo root; e.g. <c>"../.."</c> opened two levels deep. The working directory is injected for testability
    /// (defaults to the real cwd). Pure. [Story 6.11]</summary>
    public static string ResolveRepoRootOffset(ForgeOptions resolved, string? workingDirectory = null)
        => Path.GetRelativePath(workingDirectory ?? Directory.GetCurrentDirectory(), resolved.RepoRoot).Replace('\\', '/');

    /// <summary>The stable per-project scratch-dir key: a hash of the repo root, folded to a canonical case only
    /// on a case-INSENSITIVE filesystem (Windows — this project's primary target OS) so the same physical repo
    /// referenced with two different path casings (a workspace-folder URI vs. a manually-typed cwd, or drive-letter
    /// casing) still maps to the SAME stable dir there; left verbatim on a case-sensitive filesystem (Linux) so two
    /// genuinely distinct repo paths differing only by case never collide. Matches the OS's own path-comparison
    /// semantics rather than this codebase's git-path Ordinal convention (Epic 8 / Story 7.1), which addresses a
    /// different problem (git's own case-sensitive object model) and would reintroduce the Windows regression a
    /// blanket Ordinal hash here would cause. [Review][Patch]</summary>
    internal static string ScratchKey(string repoRoot)
    {
        var folded = OperatingSystem.IsWindows() ? repoRoot.ToUpperInvariant() : repoRoot;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folded)))[..16].ToLowerInvariant();
    }

    /// <summary>Held for the life of the process once a scratch-dir lock is acquired — a static field roots the
    /// <see cref="FileStream"/> so the GC can never finalize (and thereby silently release) the exclusive lock
    /// before the process actually exits; a discarded local has no such root. [Review][Patch]</summary>
    private static FileStream? _scratchLock;

    /// <summary>Clones the resolved options with the output root moved to a stable per-project temp scratch
    /// directory (keyed by <see cref="ScratchKey"/>, stable so sequential spawns for the same repo overwrite
    /// rather than accumulate). A same-repo spawn that finds the stable dir already exclusively locked by a
    /// still-running concurrent spawn (two VS Code windows, or a manual CLI run alongside the extension) falls
    /// back to a process-id-suffixed sibling dir instead of racing its writes — deliberately NOT stable (each
    /// concurrent spawn gets its own), which is fine since concurrency here is rare and each spawn's scratch
    /// output is read back once (via <see cref="SiteGenerator.RenderWebviewSurfaces"/>) and then abandoned.
    /// [Deferred item, Story 6.4 review]</summary>
    private static ForgeOptions RedirectOutputToScratch(ForgeOptions options)
    {
        var key = ScratchKey(options.RepoRoot);
        var stableRoot = Path.Combine(Path.GetTempPath(), "specscribe-webview", key);
        Directory.CreateDirectory(stableRoot);
        var outputRoot = stableRoot;
        try
        {
            // Rooted in a static field (not discarded) so the GC can't finalize/close it early — the lock must
            // survive for the whole process, including the (potentially long-lived) --serve mode. DeleteOnClose
            // removes the lock marker itself on release, whether that's a clean process exit or an explicit
            // Dispose — no separate cleanup path needed. [Review][Patch]
            _scratchLock = new FileStream(
                Path.Combine(stableRoot, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 1, FileOptions.DeleteOnClose);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            outputRoot = Path.Combine(Path.GetTempPath(), "specscribe-webview", $"{key}-{Environment.ProcessId}");
        }
        return new ForgeOptions
        {
            RepoRoot = options.RepoRoot,
            SourceRoot = options.SourceRoot,
            AdrSourceRoot = options.AdrSourceRoot,
            AdrSourceExplicit = options.AdrSourceExplicit,
            OutputRoot = outputRoot,
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

        // External source base for "view source online" links — a configurable setting, so NFR7 (menu/CLI parity)
        // requires it in the menu too, not just --code-url. Only an already-EXPLICIT settings.CodeUrl pre-fills the
        // prompt's default; a merely auto-detected value (defaults.CodeSourceBaseUrl) is surfaced as an informational
        // line instead, never as the prompt default — otherwise accepting the default on Enter would persist a
        // branch-specific detected URL to disk as if it were an explicit override, freezing future runs onto today's
        // branch instead of re-detecting fresh each time. [Story 7.7]
        if (string.IsNullOrWhiteSpace(settings.CodeUrl) && !string.IsNullOrWhiteSpace(defaults?.CodeSourceBaseUrl))
        {
            AnsiConsole.MarkupLine($"[grey]Auto-detected source base: {Markup.Escape(defaults.CodeSourceBaseUrl)} (leave blank to keep auto-detecting)[/]");
        }
        var codePrompt = new TextPrompt<string>("Source hosting base URL (blank = in-portal only / auto-detect):").AllowEmpty();
        if (!string.IsNullOrWhiteSpace(settings.CodeUrl)) codePrompt = codePrompt.DefaultValue(settings.CodeUrl);
        var codeUrl = AnsiConsole.Prompt(codePrompt);
        settings.CodeUrl = string.IsNullOrWhiteSpace(codeUrl) ? null : codeUrl.Trim();

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
