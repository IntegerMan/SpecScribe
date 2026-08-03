using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 5.3 follow-up: coverage for the CRASH-GUARD class specifically — every place
/// <see cref="FileWatcherService"/> runs code on a thread with no caller to catch anything (a
/// <see cref="System.Threading.Timer"/> callback, a <see cref="FileSystemWatcher"/> event-dispatch thread). An
/// unhandled exception on those threads does not fail one rebuild; it terminates the whole <c>specscribe watch</c>
/// process, which is a far worse outcome than any of the transients that cause it.
///
/// <para><b>Why these tests drive internal seams instead of real timers.</b> The guard only exists for the
/// no-caller case, so testing it through a real <see cref="System.Threading.Timer"/> would mean that a REGRESSION
/// takes down the test host rather than failing an assertion — one broken guard would cost the whole suite run and
/// report as an infrastructure crash rather than as the specific defect it is. Driving the same method bodies
/// synchronously keeps an unguarded throw as an ordinary, attributable test failure. This mirrors the
/// <c>OnConfigDirCreated</c> seam Story 6.11 introduced for the same reason.</para>
///
/// <para>The generator is real (not a stub) because <see cref="SiteGenerator"/> is sealed with no interface; the
/// faults are injected through the two seams that ARE substitutable — the caller-supplied event callback, and the
/// on-disk state the routes read.</para></summary>
public class FileWatcherServiceCrashGuardTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-crashguard-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string DocPath => Path.Combine(Source, "notes", "guide.md");

    public FileWatcherServiceCrashGuardTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "notes"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"),
            "# Epics\n\n## Epic List\n\n### Epic 1: Foundation\n\nStand up the portal.\n\n## Epic 1: Foundation\n\n### Story 1.1: Foundation Story\n\nAs a maintainer, I want the foundation.\n");
        File.WriteAllText(DocPath, "# Guide\n\nA note.\n");
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private SiteGenerator GeneratedSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    // ---- The reporter-throws class: the failure that fires on the SUCCESS path ----

    [Fact]
    public void ThrowingEventCallback_DoesNotEscapeThePerFileDebouncedPass()
    {
        // The production callback is ConsoleUi.LogEvent, which writes to the console. Piping `specscribe watch` into
        // a process that exits first closes stdout, and the very next successful rebuild throws IOException while
        // REPORTING itself — killing the session. RunGuarded alone did not cover this: it guards the generator call
        // and then hands the result to _onEvent outside the try.
        var gen = GeneratedSite();
        using var watcher = new FileWatcherService(
            Options(), gen, _ => throw new IOException("The pipe has been ended."));

        var ex = Record.Exception(() => watcher.RunDebouncedPass(DocPath));

        Assert.Null(ex);
    }

    [Fact]
    public void ThrowingEventCallback_DoesNotEscapeTheTopologyPass()
    {
        var gen = GeneratedSite();
        using var watcher = new FileWatcherService(
            Options(), gen, _ => throw new IOException("The pipe has been ended."));

        var ex = Record.Exception(() => watcher.RunTopologyPass());

        Assert.Null(ex);
    }

    [Fact]
    public void ThrowingEventCallback_DoesNotStopLaterPassesFromDoingRealWork()
    {
        // Swallowing must not also mean giving up: the point of surviving a broken reporter is that generation keeps
        // working. Asserted on the OUTPUT (the pageRoute really regenerates), not merely on "no exception".
        var gen = GeneratedSite();
        using var watcher = new FileWatcherService(
            Options(), gen, _ => throw new InvalidOperationException("reporter is broken"));

        File.WriteAllText(DocPath, "# Guide\n\nFIRST-EDIT\n");
        watcher.RunDebouncedPass(DocPath);
        File.WriteAllText(DocPath, "# Guide\n\nSECOND-EDIT\n");
        watcher.RunDebouncedPass(DocPath);

        var pageRoute = "notes/guide.html";
        Assert.True(SiteRegion.Exists(Site, pageRoute));
        Assert.Contains("SECOND-EDIT", SiteRegion.Read(Site, pageRoute));
    }

    /// <summary>Makes every regeneration route throw, by replacing the output ROOT with a plain file: the first thing
    /// each route does is <c>EnsureScaffold</c> → <c>Directory.CreateDirectory(OutputRoot)</c>, which throws
    /// <see cref="IOException"/> when a file already occupies that exact path. A realistic stand-in for the class of
    /// filesystem faults a long-running watch session actually meets (a path clobbered by another tool, a revoked
    /// permission, a disconnected network drive) — and, unlike a stub, it exercises the real route.
    /// <para>Note the trap this replaced: an earlier version of these tests created a DIRECTORY named
    /// <c>as-directory.md</c>, assuming the read would fail. It does not — <see cref="File.Exists"/> returns false
    /// for a directory, so the route takes the <c>RemoveFor</c> branch and returns a tidy <c>Skipped</c>, and the
    /// test passed while exercising nothing.</para></summary>
    private void BreakTheOutputRoot()
    {
        Directory.Delete(Site, recursive: true);
        File.WriteAllText(Site, "not a directory");
    }

    [Fact]
    public void ThrowingEventCallback_OnTheErrorPath_StillDoesNotEscape()
    {
        // The nastiest ordering: the route fails AND the reporter fails. RunGuarded converts the route failure into
        // an Error event, which is then handed to the same throwing callback — so a fix that guards only the route
        // still dies here, on the very path meant to keep the loop alive.
        var gen = GeneratedSite();
        using var watcher = new FileWatcherService(
            Options(), gen, _ => throw new IOException("reporter down"));
        BreakTheOutputRoot();

        var ex = Record.Exception(() => watcher.RunDebouncedPass(DocPath));

        Assert.Null(ex);
    }

    // ---- The route-throws class: what Story 5.3's RunGuarded originally fixed, pinned as a regression guard ----

    [Fact]
    public void FailingRoute_IsReportedAsAnErrorEvent_NotThrown()
    {
        var gen = GeneratedSite();
        var events = new List<GenerationEvent>();
        using var watcher = new FileWatcherService(Options(), gen, events.Add);
        BreakTheOutputRoot();

        var ex = Record.Exception(() => watcher.RunDebouncedPass(DocPath));

        Assert.Null(ex);
        var ev = Assert.Single(events);
        Assert.Equal(GenerationOutcome.Error, ev.Outcome);
        // The failure is attributed to the artifact whose pass failed, not to a generic label — that attribution is
        // what makes the surviving watch log actionable.
        Assert.Contains("guide.md", ev.RelativePath);
    }

    [Fact]
    public void FailingTopologyPass_IsReportedAsAnErrorEvent_NotThrown()
    {
        var gen = GeneratedSite();
        var events = new List<GenerationEvent>();
        using var watcher = new FileWatcherService(Options(), gen, events.Add);
        BreakTheOutputRoot();

        var ex = Record.Exception(() => watcher.RunTopologyPass());

        Assert.Null(ex);
        Assert.Contains(events, e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void WatchLoopRecovers_OnceTheUnderlyingFaultClears()
    {
        // The whole point of not crashing: the session must be usable again after the transient passes. A guard that
        // survives the fault but leaves the generator wedged would be a hollow fix.
        var gen = GeneratedSite();
        var events = new List<GenerationEvent>();
        using var watcher = new FileWatcherService(Options(), gen, events.Add);

        BreakTheOutputRoot();
        watcher.RunDebouncedPass(DocPath);
        Assert.Contains(events, e => e.Outcome == GenerationOutcome.Error);

        // Clear the fault, exactly as a user would by removing the offending file.
        File.Delete(Site);
        events.Clear();
        File.WriteAllText(DocPath, "# Guide\n\nAFTER-RECOVERY\n");
        watcher.RunDebouncedPass(DocPath);

        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        Assert.Contains("AFTER-RECOVERY", SiteRegion.Read(Site, "notes/guide.html"));
    }

    [Fact]
    public void SuccessfulPass_StillReportsItsRealOutcome_TheGuardIsNotSwallowingEverything()
    {
        // The counter-test that keeps the guards honest: it would be trivial to make every test above pass by
        // swallowing unconditionally, so pin that a NORMAL pass still reports a normal, non-Error outcome.
        var gen = GeneratedSite();
        var events = new List<GenerationEvent>();
        using var watcher = new FileWatcherService(Options(), gen, events.Add);

        File.WriteAllText(DocPath, "# Guide\n\nEDITED\n");
        watcher.RunDebouncedPass(DocPath);

        var ev = Assert.Single(events);
        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);
        Assert.Contains("EDITED", SiteRegion.Read(Site, "notes/guide.html"));
    }

    [Fact]
    public void TopologyPass_ReportsTheDirectoryChangeEvent_WhenTheReporterWorks()
    {
        var gen = GeneratedSite();
        var events = new List<GenerationEvent>();
        using var watcher = new FileWatcherService(Options(), gen, events.Add);

        watcher.RunTopologyPass();

        Assert.Contains(events, e => e.RelativePath == "<directory change>" && e.Message == "full rebuild");
    }

    // ---- The raw FileSystemWatcher handler bodies ----

    [Fact]
    public void ThrowingEventCallback_DoesNotEscapeTheConfigDirHandler()
    {
        // OnConfigDirCreated reports its TOCTOU miss through the same callback, and the registration path runs on the
        // repo-root watcher's dispatch thread. A real present-then-vanished race can't be landed on reliably from a
        // single-threaded test, so drive the TOCTOU catch branch deterministically via the watcher-factory seam
        // instead — the review-fix's whole point being that this test previously never reached that branch at all
        // (the directory was left in place, so the success path ran and _onEvent was never invoked). [Story 5.3 review-fix]
        var gen = GeneratedSite();
        using var watcher = new FileWatcherService(
            Options(), gen, _ => throw new IOException("reporter down"));

        var configDir = Path.Combine(_root, ForgeOptions.ConfigDirName);
        Directory.CreateDirectory(configDir);

        var ex = Record.Exception(() => watcher.OnConfigDirCreated(
            configDir, () => throw new ArgumentException("_bmad vanished between the check and construction")));

        Assert.Null(ex);
    }

    [Fact]
    public void ConfigDirHandler_RegistersOnTheSuccessPath_WhenTheDirectoryIsStillThere()
    {
        // Counter-test to the TOCTOU case above: with the directory genuinely still present and a real watcher
        // factory, registration must actually succeed — the guard must not be satisfiable by swallowing everything.
        // [Story 5.3 review-fix]
        var gen = GeneratedSite();
        using var watcher = new FileWatcherService(Options(), gen, _ => { });

        var configDir = Path.Combine(_root, ForgeOptions.ConfigDirName);
        Directory.CreateDirectory(configDir);

        var countBefore = watcher.WatcherCount;
        watcher.OnConfigDirCreated(configDir);

        Assert.Equal(countBefore, watcher.WatcherCount); // the fallback repo-root detector is retired as the real one registers
    }

    [Fact]
    public void Dispose_AfterAThrowingCallback_IsStillClean()
    {
        // A guard that leaves the service in a half-torn-down state would trade a crash for a leak; watcher handles
        // are OS resources. Pin that the normal teardown still runs after the guards have fired.
        var gen = GeneratedSite();
        var watcher = new FileWatcherService(Options(), gen, _ => throw new IOException("reporter down"));
        watcher.Start();
        watcher.RunDebouncedPass(DocPath);
        watcher.RunTopologyPass();

        var ex = Record.Exception(() =>
        {
            watcher.Stop();
            watcher.Dispose();
        });

        Assert.Null(ex);
    }
}
