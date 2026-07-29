using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 5.3 end-to-end coverage for <see cref="FileWatcherService"/> driven by REAL filesystem events —
/// the layer <see cref="SiteGeneratorEpicsRemovalTests"/> deliberately skips. What is under test here is the
/// routing and debouncing that only a live <see cref="FileSystemWatcher"/> exercises: that a folder-level change is
/// observed at all (the name-filtered file watchers structurally cannot see one), that a burst collapses to one
/// rebuild, and that the whole thing survives an edit without taking a write lock on the file being edited.
/// <para>These tests wait on real event delivery, so they are the slow, timing-sensitive corner of the suite. Every
/// wait is bounded and polls for the OUTCOME rather than sleeping a fixed multiple of
/// <see cref="ForgeOptions.DebounceInterval"/> — a fixed sleep is what makes this class of test flaky under a loaded
/// CI machine.</para></summary>
public class FileWatcherServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-watcher-").FullName;
    private readonly List<GenerationEvent> _events = new();
    private readonly object _eventsLock = new();

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string EpicsPath => Path.Combine(Source, "planning-artifacts", "epics.md");
    private string SprintPath => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");

    // Generous relative to the 400ms debounce: FileSystemWatcher delivery latency is not bounded by anything we
    // control, and the assertion is "this eventually happened", never "this happened within N ms".
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(20);

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    private const string SprintYaml = """
        last_updated: MARKER-V1
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
        """;

    public FileWatcherServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "notes"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(EpicsPath, EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            "# Story 1.1: Foundation Story\n\nStatus: in-progress\n\n## Story\n\nAs a maintainer, I want it.\n");
        File.WriteAllText(Path.Combine(Source, "notes", "guide.md"), "# Guide\n\nORIGINAL-BODY\n");
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

    private string SitePath(string relative) =>
        Path.Combine(Site, relative.Replace('/', Path.DirectorySeparatorChar));

    private SiteGenerator GeneratedSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    private FileWatcherService StartedWatcher(SiteGenerator gen)
    {
        var watcher = new FileWatcherService(Options(), gen, ev =>
        {
            lock (_eventsLock) { _events.Add(ev); }
        });
        watcher.Start();
        return watcher;
    }

    private GenerationEvent[] Observed()
    {
        lock (_eventsLock) { return _events.ToArray(); }
    }

    /// <summary>Polls until <paramref name="condition"/> holds or the bound elapses, then returns whether it held.
    /// Polling the real outcome (not sleeping a fixed interval) is what keeps these tests honest AND fast: a
    /// machine that delivers the event in 450ms doesn't pay for the worst case.</summary>
    private static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + SettleTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Evaluate(condition)) return true;
            Thread.Sleep(25);
        }
        return Evaluate(condition);
    }

    /// <summary>A poll predicate that reads generated files is racing a rebuild in progress — and the whole-tree
    /// routes wipe the output root before repopulating it, so "file missing" and "file locked" are both NORMAL
    /// transient states here, not failures. Swallowing them is what makes the poll mean "did this converge?"
    /// rather than "was the very first read lucky?".</summary>
    private static bool Evaluate(Func<bool> condition)
    {
        try { return condition(); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>Waits for an emitted event to match. Event delivery is asynchronous, so asserting on
    /// <see cref="Observed"/> synchronously right after the on-disk outcome lands is a race: the file-level and
    /// topology routes run on independent timers and either can win.</summary>
    private void AssertEventuallyObserved(Func<GenerationEvent, bool> predicate, string because)
    {
        Assert.True(WaitFor(() => Observed().Any(predicate)),
            $"{because}\nObserved events: [{string.Join(", ", Observed().Select(e => $"{e.Outcome} {e.RelativePath} \"{e.Message}\""))}]");
    }

    [Fact]
    public void EditingAStoryFile_RegeneratesThroughTheOrdinaryMarkdownRoute()
    {
        // Regression guard on the pre-existing route: this story ADDS watchers, it must not disturb the .md path.
        var gen = GeneratedSite();
        using var watcher = StartedWatcher(gen);

        File.WriteAllText(Path.Combine(Source, "notes", "guide.md"), "# Guide\n\nEDITED-BODY\n");

        Assert.True(WaitFor(() => File.Exists(SitePath("notes/guide.html"))
                && File.ReadAllText(SitePath("notes/guide.html")).Contains("EDITED-BODY")),
            "an ordinary .md save should regenerate its page");
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void DeletingEpicsFile_RemovesTheStaleEpicsOutputFamily_WithoutThrowing()
    {
        // AC #3 through the real watcher: the delete is observed, debounced, routed to RegenerateEpics, and the
        // stale subtree is gone. epics.md sits under planning-artifacts/, so this also proves the .md watcher's
        // Deleted handler reaches the epics route.
        var gen = GeneratedSite();
        Assert.True(File.Exists(SitePath("epics.html")));
        using var watcher = StartedWatcher(gen);

        File.Delete(EpicsPath);

        // All four conditions are waited for TOGETHER, not asserted at the moment the first two happen to hold.
        // Story 22.5 routes an epics.md deletion through the escalated full rebuild (see ClassifyRebuildScope's
        // remarks on Trap 4), and GenerateAll's `Directory.Delete(OutputRoot, recursive: true)` is not atomic — it
        // removes entries one at a time. So there is a real window in which epics.html and epics/ are already gone
        // while requirements.html has not been reached yet, and a wait that stops at the first two lands the next
        // three assertions inside it. Waiting for the settled state asserts the same thing without racing the wipe.
        Assert.True(WaitFor(() =>
                !File.Exists(SitePath("epics.html"))
                && !Directory.Exists(SitePath("epics"))
                && !File.Exists(SitePath("requirements.html"))
                && !Directory.Exists(SitePath("requirements"))),
            "deleting epics.md should remove epics.html, requirements.html, and both subtrees");
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void SprintStatusYaml_AddedThenEditedThenRemoved_RefreshesTheSprintSurfaceEachTime()
    {
        // AC #4. The watcher-side half of this was already closed by Story 6.11 (the widened Filters + the
        // IsDataSource route); what was missing was any end-to-end proof that a real yaml event actually drives it —
        // including the REMOVAL case, where the page must disappear rather than strand a board with no source.
        var gen = GeneratedSite();
        Assert.False(File.Exists(SitePath("sprint.html")), "no sprint page before the yaml exists");
        using var watcher = StartedWatcher(gen);

        File.WriteAllText(SprintPath, SprintYaml);
        Assert.True(WaitFor(() => File.Exists(SitePath("sprint.html"))
                && File.ReadAllText(SitePath("sprint.html")).Contains("MARKER-V1")),
            "adding sprint-status.yaml should produce the sprint page");

        File.WriteAllText(SprintPath, SprintYaml.Replace("MARKER-V1", "MARKER-V2"));
        Assert.True(WaitFor(() => File.ReadAllText(SitePath("sprint.html")).Contains("MARKER-V2")),
            "editing sprint-status.yaml should refresh the board");

        File.Delete(SprintPath);
        Assert.True(WaitFor(() => !File.Exists(SitePath("sprint.html"))),
            "removing sprint-status.yaml should remove the sprint page");
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void RenamingAWholeDirectory_EscalatesToAFullRebuild()
    {
        // AC #5 — the gap this story exists to close. Before the directory watcher, `Filters = "*.md"` matched no
        // bare folder name, so this operation produced NO watcher event at all and the page silently stranded at
        // its old path forever.
        var gen = GeneratedSite();
        Assert.True(File.Exists(SitePath("notes/guide.html")));
        using var watcher = StartedWatcher(gen);

        Directory.Move(Path.Combine(Source, "notes"), Path.Combine(Source, "handbook"));

        Assert.True(WaitFor(() => File.Exists(SitePath("handbook/guide.html"))),
            "a folder rename should rebuild the page at its new location");
        Assert.True(WaitFor(() => !File.Exists(SitePath("notes/guide.html"))),
            "no orphan page may survive at the old location");

        AssertEventuallyObserved(
            e => e.RelativePath == "<directory change>" && e.Message == "full rebuild",
            "the folder rename should surface as a single escalated full-rebuild event");
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void DeletingAWholeDirectory_EscalatesToAFullRebuild()
    {
        var gen = GeneratedSite();
        Assert.True(File.Exists(SitePath("notes/guide.html")));
        using var watcher = StartedWatcher(gen);

        Directory.Delete(Path.Combine(Source, "notes"), recursive: true);

        Assert.True(WaitFor(() => !File.Exists(SitePath("notes/guide.html"))),
            "deleting a folder should remove the pages it produced");
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void BurstOfSaves_CoalescesAndLeavesCoherentOutput()
    {
        // AC #1/#6 at the watcher layer: a bulk find/replace touching many files at once must settle into coherent
        // output, and the per-file debounce must collapse the burst rather than emitting an event per raw FS
        // notification (a single save alone typically produces several).
        var gen = GeneratedSite();
        var docs = Path.Combine(Source, "notes");
        for (var i = 0; i < 8; i++)
        {
            File.WriteAllText(Path.Combine(docs, $"bulk-{i}.md"), $"# Bulk {i}\n\nORIGINAL\n");
        }
        gen.GenerateAll();

        using var watcher = StartedWatcher(gen);

        // The burst: rewrite all eight back-to-back, the shape a find/replace or a git checkout produces. bulk-0 is
        // additionally rewritten several more times in the same tight loop — well inside one DebounceInterval — so
        // its final event COUNT (asserted below), not just its final content, directly proves the per-file timer
        // coalesces repeated notifications into one fire rather than one per raw FS event. [Story 5.3 review-fix]
        for (var i = 0; i < 8; i++)
        {
            File.WriteAllText(Path.Combine(docs, $"bulk-{i}.md"), $"# Bulk {i}\n\nREPLACED\n");
        }
        for (var extra = 0; extra < 4; extra++)
        {
            File.WriteAllText(Path.Combine(docs, "bulk-0.md"), $"# Bulk 0\n\nREPLACED-{extra}\n");
        }

        Assert.True(WaitFor(() => Enumerable.Range(0, 8).All(i =>
                File.Exists(SitePath($"notes/bulk-{i}.html"))
                && File.ReadAllText(SitePath($"notes/bulk-{i}.html")).Contains("REPLACED"))),
            "every file in the burst should end up regenerated");

        // Nothing was left mid-write by the overlapping regenerations — the single writer lock's job.
        foreach (var page in Directory.GetFiles(Site, "*.html", SearchOption.AllDirectories))
        {
            var html = File.ReadAllText(page);
            Assert.False(string.IsNullOrWhiteSpace(html), $"{page} is empty — a torn write");
            Assert.Contains("</html>", html);
        }
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);

        // The coalescing assertion itself: bulk-0.md was rewritten 5 times total in one burst, well inside one
        // DebounceInterval, so a working per-file debounce collapses that into exactly ONE regeneration event for
        // its page — a regression that regenerated on every raw notification would fail this. GenerateOneInternal
        // labels its event with the SOURCE-relative path (.md) via Path.GetRelativePath (OS separator), not the
        // output page (.html) with the site's normalized forward slashes.
        //
        // Wait for the EVENT, not just the page. The page is written partway through GenerateOne, and the event is
        // only published once the whole route returns — so "bulk-0.html says REPLACED" does not imply "its event has
        // been observed". That gap has always existed; Story 22.5 widened it by giving the route real work to do
        // after the page write (the code-surface refresh + source-inventory rewalk), which is what turned a latent
        // race into a reproducible failure. Waiting on the observable the assertion is actually about removes it
        // without weakening the assertion: it still has to be exactly ONE, and a per-notification regression would
        // overshoot to five and fail.
        var bulk0Relative = Path.Combine("notes", "bulk-0.md");
        Assert.True(WaitFor(() => Observed().Any(e => e.RelativePath == bulk0Relative)),
            "bulk-0.md's own regeneration event should be observed");
        Assert.Equal(1, Observed().Count(e => e.RelativePath == bulk0Relative));
    }

    [Fact]
    public void WatchedSourceFileStaysWritableAndDeletableDuringRegeneration()
    {
        // NFR5 as a regression guard (AC #1's second clause): the generator reads through
        // MarkdownConverter.ReadAllTextShared, so watch mode must never hold a write lock on an observed file.
        // Asserted by doing the thing a lock would block: rewrite the same file repeatedly while rebuilds are in
        // flight, then delete it outright.
        var gen = GeneratedSite();
        using var watcher = StartedWatcher(gen);

        var doc = Path.Combine(Source, "notes", "guide.md");
        for (var i = 0; i < 10; i++)
        {
            File.WriteAllText(doc, $"# Guide\n\nREVISION-{i}\n");
            Thread.Sleep(30);
        }

        Assert.True(WaitFor(() => File.ReadAllText(SitePath("notes/guide.html")).Contains("REVISION-9")),
            "the last revision should win");

        // A delete would fail with a sharing violation if generation held the file open.
        File.Delete(doc);
        Assert.True(WaitFor(() => !File.Exists(SitePath("notes/guide.html"))),
            "deleting the source should remove its page");
        Assert.DoesNotContain(Observed(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void OutputRootInsideTheSourceRoot_DoesNotSelfTriggerARebuildLoop()
    {
        // The hazard the directory watcher introduces and IsUnderOutputRoot closes: generation recreates the whole
        // output tree on every full rebuild, so if --output points INSIDE a watched source root, each rebuild's own
        // directory writes would re-arm the topology timer and the loop would never terminate. Asserted by letting
        // it run well past several debounce windows and checking the rebuild count stops growing.
        var nestedOutput = Path.Combine(Source, "site-output");
        var options = ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: nestedOutput, projectName: "SpecScribe", includeReadme: false);
        var gen = new SiteGenerator(options);
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        using var watcher = new FileWatcherService(options, gen, ev =>
        {
            lock (_eventsLock) { _events.Add(ev); }
        });
        watcher.Start();

        // One genuine topology change to prove the watcher is live at all, then let everything settle.
        Directory.CreateDirectory(Path.Combine(Source, "fresh-folder"));
        AssertEventuallyObserved(
            e => e.RelativePath == "<directory change>",
            "a real directory change should still be observed when the output root is nested");

        // Poll for a run of consecutive stable readings rather than two blind fixed-length sleeps (the class's own
        // anti-flakiness rule): a self-triggering loop keeps growing on every sample, so this fails fast on a broken
        // guard instead of only checking once at the far end of a fixed gap — and a slow/loaded box just shifts
        // where the plateau lands within the window rather than producing a false failure. [Story 5.3 review-fix]
        const int requiredStableSamples = 5;
        var deadline = DateTime.UtcNow + SettleTimeout;
        var stableRun = 0;
        var lastCount = -1;
        while (DateTime.UtcNow < deadline && stableRun < requiredStableSamples)
        {
            var count = Observed().Count(e => e.RelativePath == "<directory change>");
            stableRun = count == lastCount ? stableRun + 1 : 1;
            lastCount = count;
            Thread.Sleep(50);
        }

        Assert.True(stableRun >= requiredStableSamples,
            $"'<directory change>' event count never stabilized (still climbing at {lastCount} — a self-triggered rebuild loop)");
    }

    // ===== Story 22.6: the delta sidecar under the watcher's real concurrency ===============================

    /// <summary>Story 22.6 Trap 1, driven rather than reasoned about. <see cref="FileWatcherService"/> fires one
    /// debounce <see cref="Timer"/> PER distinct changed path, each on its own thread-pool thread, so two files
    /// saved inside the same window invoke the delta computation CONCURRENTLY. If the previous-manifest basis were
    /// read and replaced outside a lock, a delta would be emitted against the wrong basis — a page that changed
    /// reported unchanged, or vice versa, with no test failing.
    /// <para>The protection is that every <c>EmitSpaSite</c> call site already holds the generator's <c>_gate</c>,
    /// so the basis is serialized by construction and no second lock was added. This pins that: after N concurrent
    /// passes the sidecar is a complete, parseable document whose sequence equals the number of emits — a torn
    /// write or a lost update shows up as a parse failure or a sequence gap.</para></summary>
    [Fact]
    public void ConcurrentDebouncedPasses_LeaveTheDeltaSidecarCoherent()
    {
        var options = ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, emitSpa: true);
        var gen = new SiteGenerator(options) { EmitDeltaSidecar = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        using var watcher = new FileWatcherService(options, gen, _ => { });

        // Eight distinct paths, dispatched from eight threads at once — the exact shape the per-path timers
        // produce. RunDebouncedPass is the internal synchronous seam (see its own doc comment: driving the body
        // directly is what turns a crash into an ordinary test failure instead of a lost suite run).
        const int Passes = 8;
        var docs = Enumerable.Range(1, Passes)
            .Select(i => Path.Combine(Source, "notes", $"concurrent-{i}.md")).ToList();
        foreach (var (doc, i) in docs.Select((d, i) => (d, i)))
        {
            File.WriteAllText(doc, $"# Concurrent {i}\n\nBody {i}.\n");
        }

        using var start = new ManualResetEventSlim(false);
        var threads = docs.Select(doc => new Thread(() =>
        {
            start.Wait();
            watcher.RunDebouncedPass(doc);
        })).ToList();
        foreach (var t in threads) t.Start();
        start.Set();
        foreach (var t in threads) Assert.True(t.Join(TimeSpan.FromMinutes(2)), "a debounced pass never completed");

        // Complete and parseable — never observed torn, and no temp file survived.
        var deltaPath = Path.Combine(Site, SpaDelivery.DeltaPath.Replace('/', Path.DirectorySeparatorChar));
        var delta = System.Text.Json.JsonDocument.Parse(File.ReadAllText(deltaPath)).RootElement;
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(Site, SpaDelivery.ChunkDir), "*.tmp"));

        // One emit per pass on top of the session's first — no lost update, no double-increment.
        Assert.Equal(1 + Passes, delta.GetProperty("sequence").GetInt64());

        // Every page really did land in the IR: the concurrency must not have dropped one, which is the failure a
        // sequence check alone would miss.
        var manifest = System.Text.Json.JsonDocument
            .Parse(File.ReadAllText(Path.Combine(Site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar))))
            .RootElement.GetProperty("pages");
        foreach (var i in Enumerable.Range(1, Passes))
        {
            Assert.True(
                manifest.TryGetProperty($"notes/concurrent-{i}.html", out _),
                $"notes/concurrent-{i}.html is missing from the IR after concurrent passes");
        }
    }
}
