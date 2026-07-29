using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>The oracle-diff gate: every watch-mode incremental route must produce the output a full
/// <see cref="SiteGenerator.GenerateAll"/> of the identical post-change source tree would produce. [Story 22.5 AC #5]
///
/// <para><b>Why this class exists.</b> Before it, nothing anywhere in the suite compared an incremental route's
/// output to a full regeneration — which is how a 56-page work-graph divergence shipped in <c>RegenerateEpics</c>
/// and stayed shipped. Story 22.1 found it with a throwaway probe (<c>spike/ir-incremental/</c>); owner decision D4
/// says the probe was the design, not the deliverable. This is the deliverable.</para>
///
/// <para><b>Why a full <c>GenerateAll</c> is a legitimate oracle and not a tautology.</b> It wipes the output root
/// and rebuilds every surface from source, so its output is coherent by construction. The 22.1 spike proved the
/// instrument is real: two independent full generates agree byte-for-byte on every shared page, so any diff a case
/// reports is signal rather than normalization noise. <see cref="NoOpControls"/> re-proves that here on every run
/// via the <c>generate-all</c> control.</para>
///
/// <para><b>The no-op controls are the highest-value assertions in the class.</b> They run a route with NO source
/// change at all, which isolates route-vs-oracle divergence from any change ripple: a route that cannot reproduce
/// the oracle when nothing happened cannot possibly be trusted when something did. That is the exact assertion that
/// caught the shipped defect.</para>
///
/// <para><b>Traps this harness handles explicitly</b> — each one produced a false alarm for an earlier story:</para>
/// <list type="number">
/// <item><c>diagnostics.html</c> echoes the configured OUTPUT ROOT inside its own region, so it is output-path
/// dependent and the two trees here are generated into two different directories. Handled by folding every root to
/// one placeholder in the shared <see cref="GoldenNormalization"/> — not by excluding the page, which would hide a
/// whole class of output. (Story 22.2; Story 22.5 Trap 5.)</item>
/// <item>On a NON-git fixture <c>FallbackCodeWalk</c> skips dot-dirs / <c>bin</c> / <c>obj</c> /
/// <c>node_modules</c> but NOT the output directory, so an output tree nested under the source would feed run 1's
/// HTML into run 2's code map. Both output roots therefore live OUTSIDE <see cref="RepoRoot"/>. (Story 22.5
/// Trap 6.)</item>
/// <item>The volatile-token fold is <see cref="GoldenNormalization"/>, SHARED with the golden fingerprint. A second
/// copy that folds one extra token is a hole in the gate — and the 22.1 spike's private copy had already drifted.
/// (Story 22.5 AC #5.)</item>
/// </list>
///
/// <para><b>Cost.</b> Two full generates per case. The fixture is deliberately small — the
/// <see cref="SiteGeneratorAdapterTests"/> shape plus the two artifacts that actually exercise the work inventory
/// (a <c>deferred-work.md</c> carrying resolver refs and a <c>route: one-shot</c> spec). Without those two the
/// parity path this story exists to fix is not exercised at all: that is precisely why Story 22.4's identical fix
/// was zero-delta against the golden fixture.</para></summary>
public class IncrementalOracleParityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-oracle-").FullName;
    private readonly List<GenerationEvent> _events = new();

    /// <summary>The fixture's repo root. Nested one level under the temp dir so BOTH output roots can be its
    /// SIBLINGS rather than its children — see Trap 6 in the class remarks.</summary>
    private string RepoRoot => Path.Combine(_root, "repo");
    private string Source => Path.Combine(RepoRoot, "_bmad-output");
    private string Adrs => Path.Combine(RepoRoot, "docs", "adrs");
    private string OutIncremental => Path.Combine(_root, "out-incremental");
    private string OutOracle => Path.Combine(_root, "out-oracle");

    private string PlanningDir => Path.Combine(Source, "planning-artifacts");
    private string ImplDir => Path.Combine(Source, "implementation-artifacts");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### NonFunctional Requirements

        NFR1: Generation degrades gracefully

        ### FR Coverage Map

        FR1: Epic 1 - rendering
        NFR1: Epic 1 - degradation

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ### Epic 2: Delivery

        Ship the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).

        ## Epic 2: Delivery

        ### Story 2.1: Delivery Story

        As a maintainer, I want delivery.

        ### Story 2.2: Second Delivery Story

        As a maintainer, I want more delivery.
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string Story21Md = """
        # Story 2.1: Delivery Story

        Status: done

        ## Story

        As a maintainer, I want delivery.

        ## Acceptance Criteria

        1. It ships.

        ## Tasks / Subtasks

        - [x] Task 1: Ship it (AC: #1)
        """;

    private const string Story22Md = """
        # Story 2.2: Second Delivery Story

        Status: in-progress

        ## Story

        As a maintainer, I want more delivery.

        ## Acceptance Criteria

        1. It also ships.

        ## Tasks / Subtasks

        - [ ] Task 1: Ship it too (AC: #1)
        """;

    /// <summary>The artifact that makes this fixture able to SEE the parity defect. Its items carry both a
    /// story-scoped attribution ("stemmed from Story N.M") and RESOLVED markers naming a story and a
    /// <c>spec-*.md</c> one-shot — which is what puts resolver nodes and provenance edges into the work graph.
    /// A fixture without it renders a work graph with nothing to disagree about, which is exactly why Story 22.4's
    /// identical fix measured zero delta against the golden fixture while moving 57 pages on the real repo.</summary>
    private const string DeferredWorkMd = """
        # Deferred Work

        - Harden the foundation loader — stemmed from Story 1.1. **RESOLVED in Story 2.1**
        - Add a second delivery lane — stemmed from Story 2.1. **RESOLVED** (`spec-oracle-one-shot.md`)
        - Revisit the undrafted follow-up — stemmed from Story 1.2.
        - ~~Retire the legacy path — stemmed from Story 1.1.~~
        """;

    private const string QuickDevSpecMd = """
        ---
        route: one-shot
        status: done
        type: enhancement
        ---

        # Spec: Oracle One-Shot

        A quick-dev one-shot change, so the work inventory has a QuickDev entry to resolve against.
        """;

    private const string SprintYamlContent = """
        last_updated: 2026-07-06T22:00:00-04:00
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
          1-2-undrafted: backlog
          epic-2: in-progress
          2-1-delivery: done
          2-2-second-delivery: in-progress
        """;

    public IncrementalOracleParityTests()
    {
        Directory.CreateDirectory(PlanningDir);
        Directory.CreateDirectory(ImplDir);
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(PlanningDir, "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(PlanningDir, "architecture.md"), "# Architecture\n\nThe generic doc.\n");
        File.WriteAllText(Path.Combine(ImplDir, "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(ImplDir, "2-1-delivery.md"), Story21Md);
        File.WriteAllText(Path.Combine(ImplDir, "2-2-second-delivery.md"), Story22Md);
        File.WriteAllText(Path.Combine(ImplDir, "deferred-work.md"), DeferredWorkMd);
        File.WriteAllText(Path.Combine(ImplDir, "spec-oracle-one-shot.md"), QuickDevSpecMd);
        File.WriteAllText(Path.Combine(ImplDir, "sprint-status.yaml"), SprintYamlContent);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
        File.WriteAllText(Path.Combine(Adrs, "0001-first.md"), "# 1. First Decision\n\nStatus: Accepted\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "0002-second.md"), "# 2. Second Decision\n\nStatus: Accepted\n\nBody.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(string output) => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: output, projectName: "SpecScribe", includeReadme: false);

    // ── the harness ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One change class, end to end. Builds the PRE-change tree with a live generator (as watch mode
    /// does), applies <paramref name="mutate"/>, drives the SHIPPED watch dispatch over the paths it reports, then
    /// full-generates the identical POST-change tree into a second output root and diffs the two byte-for-byte.</summary>
    private OracleDiff RunClass(Func<IReadOnlyList<string>> mutate)
    {
        var incrementalOptions = Options(OutIncremental);
        var generator = new SiteGenerator(incrementalOptions);
        Assert.DoesNotContain(generator.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // The REAL dispatch, not a hand-written call sequence: RunDebouncedPass owns the fire-time predicate order
        // (IsDataSource → IsAdr → IsEpicsRelated → GenerateOne/RemoveFor) and a test that re-implemented it could
        // drift from the thing that actually runs in watch mode. It is `internal` precisely as this seam
        // (Story 5.3), and the watchers it constructs are NOT started, so nothing here is timing-dependent.
        using var watcher = new FileWatcherService(incrementalOptions, generator, e => { lock (_events) _events.Add(e); });
        foreach (var path in mutate()) watcher.RunDebouncedPass(path);

        var oracle = new SiteGenerator(Options(OutOracle));
        Assert.DoesNotContain(oracle.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        return Diff(OutIncremental, OutOracle) with { DispatchedEvents = DescribeEvents() };
    }

    /// <summary>What the dispatch actually DID, folded into the failure message. Without it, "this page went stale"
    /// leaves the first diagnostic question — did this class escalate, or take a narrow route? — unanswered, and
    /// answering it by hand means re-running with a debugger.</summary>
    private string DescribeEvents()
    {
        lock (_events)
        {
            return _events.Count == 0
                ? "(no events)"
                : string.Join("; ", _events.Select(e => $"{e.Outcome} {e.RelativePath}"
                    + (string.IsNullOrEmpty(e.Message) ? "" : $" [{e.Message}]")));
        }
    }

    private OracleDiff Diff(string incrementalRoot, string oracleRoot)
    {
        var incremental = Snapshot(incrementalRoot);
        var oracle = Snapshot(oracleRoot);

        var stale = new List<string>();
        var missing = new List<string>();
        var orphaned = new List<string>();

        foreach (var (relative, oracleContent) in oracle)
        {
            if (!incremental.TryGetValue(relative, out var incrementalContent)) missing.Add(relative);
            else if (!string.Equals(incrementalContent, oracleContent, StringComparison.Ordinal)) stale.Add(relative);
        }
        foreach (var relative in incremental.Keys)
        {
            if (!oracle.ContainsKey(relative)) orphaned.Add(relative);
        }

        var ordered = stale.OrderBy(p => p, StringComparer.Ordinal).ToList();
        return new OracleDiff(
            ordered,
            orphaned.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            missing.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            oracle.Count,
            ordered.Count == 0 ? "" : FirstDifference(ordered[0], incremental[ordered[0]], oracle[ordered[0]]));
    }

    /// <summary>The first differing LINE of the first stale page, both sides, trimmed. A bare list of stale
    /// filenames says a page went stale but not what went stale ON it, and these pages are tens of thousands of
    /// characters wide — without this, diagnosing a failure means re-running the harness by hand with a debugger
    /// attached, which is exactly the friction that lets a gate rot.</summary>
    private static string FirstDifference(string path, string incremental, string oracle)
    {
        var left = incremental.Split('\n');
        var right = oracle.Split('\n');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var a = i < left.Length ? left[i] : "(no such line)";
            var b = i < right.Length ? right[i] : "(no such line)";
            if (string.Equals(a, b, StringComparison.Ordinal)) continue;
            // Window on the first differing CHARACTER, not the start of the line: these pages carry single lines
            // tens of thousands of characters wide (one embedded JSON island is one line), and a fixed head excerpt
            // of such a line shows two identical prefixes and tells the reader nothing.
            var at = 0;
            while (at < a.Length && at < b.Length && a[at] == b[at]) at++;
            return $"\n  first difference — {path} line {i + 1}, character {at + 1}:\n"
                 + $"    incremental: {Window(a, at)}\n"
                 + $"    oracle     : {Window(b, at)}";
        }
        return "";

        static string Window(string line, int at)
        {
            var start = Math.Max(0, at - 60);
            var length = Math.Min(200, line.Length - start);
            if (length <= 0) return "(line ends here)";
            return (start > 0 ? "…" : "") + line.Substring(start, length) + (start + length < line.Length ? "…" : "");
        }
    }

    /// <summary>Every output file, keyed by its date-folded relative path, with volatile tokens folded through the
    /// SHARED normalization. Both output roots AND the repo root are folded so a page that echoes a filesystem path
    /// (<c>diagnostics.html</c>) compares equal across the two trees. Read as text throughout: this fixture emits no
    /// binary output.</summary>
    private Dictionary<string, string> Snapshot(string root)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(root)) return snapshot;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = GoldenNormalization.FoldToday(PathUtil.NormalizeSlashes(Path.GetRelativePath(root, path)));
            snapshot[relative] = GoldenNormalization.NormalizeVolatile(
                File.ReadAllText(path), OutIncremental, OutOracle, RepoRoot);
        }
        return snapshot;
    }

    private sealed record OracleDiff(
        IReadOnlyList<string> Stale,
        IReadOnlyList<string> Orphaned,
        IReadOnlyList<string> Missing,
        int OracleFileCount,
        string FirstDifferenceExcerpt)
    {
        public string DispatchedEvents { get; init; } = "(not driven through the watch dispatch)";

        public bool IsClean => Stale.Count == 0 && Orphaned.Count == 0 && Missing.Count == 0;

        public string Describe(string label) =>
            $"{label}: incremental output diverged from the full-regeneration oracle over {OracleFileCount} files.\n"
            + $"  dispatched: {DispatchedEvents}\n"
            + $"  stale ({Stale.Count}): {Join(Stale)}\n"
            + $"  orphaned ({Orphaned.Count}): {Join(Orphaned)}\n"
            + $"  missing ({Missing.Count}): {Join(Missing)}"
            + FirstDifferenceExcerpt;

        private static string Join(IReadOnlyList<string> paths) =>
            paths.Count == 0 ? "(none)" : string.Join(", ", paths.Take(25)) + (paths.Count > 25 ? ", …" : "");
    }

    // ── mutators, one per change class ──────────────────────────────────────────────────────────────────────

    private IReadOnlyList<string> NoChange() => Array.Empty<string>();

    private IReadOnlyList<string> ContentEditGenericDoc()
    {
        var path = Path.Combine(PlanningDir, "architecture.md");
        File.AppendAllText(path, "\n\nA content-only paragraph.\n");
        return new[] { path };
    }

    private IReadOnlyList<string> ContentEditStory()
    {
        var path = Path.Combine(ImplDir, "1-1-foundation.md");
        File.AppendAllText(path, "\n\nA content-only paragraph.\n");
        return new[] { path };
    }

    private IReadOnlyList<string> AddGenericDoc()
    {
        var path = Path.Combine(PlanningDir, "zzz-new-doc.md");
        File.WriteAllText(path, "# New Doc\n\nAdded while the generator was live.\n");
        return new[] { path };
    }

    private IReadOnlyList<string> RenameGenericDoc()
    {
        var from = Path.Combine(PlanningDir, "architecture.md");
        var to = Path.Combine(PlanningDir, "architecture-renamed.md");
        File.Move(from, to);
        // A rename surfaces as delete(old) + create(new); the watcher debounces each into its own dispatch.
        return new[] { from, to };
    }

    private IReadOnlyList<string> DeleteStory()
    {
        // A LATER story whose epic still has a surviving sibling, so this isolates the delete rather than
        // collapsing a whole epic.
        var path = Path.Combine(ImplDir, "2-2-second-delivery.md");
        File.Delete(path);
        return new[] { path };
    }

    private IReadOnlyList<string> DeleteAdr()
    {
        var path = Path.Combine(Adrs, "0002-second.md");
        File.Delete(path);
        return new[] { path };
    }

    private IReadOnlyList<string> DeleteEpics()
    {
        var path = Path.Combine(PlanningDir, "epics.md");
        File.Delete(path);
        return new[] { path };
    }

    // ── the gate ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The controls. A route run against an UNCHANGED tree must reproduce the oracle exactly — there is no
    /// change for it to have missed, so any divergence is the route itself producing different output than a full
    /// rebuild. <c>RegenerateEpics</c> is the one that was measured broken (56 pages, on every epic); the
    /// <c>generate-all</c> row re-proves determinism, i.e. that the instrument itself is sound.
    /// <para>These call the route directly rather than through <see cref="FileWatcherService.RunDebouncedPass"/>,
    /// unlike every change class below. That is forced by what a control IS: there is no changed path, so there is
    /// nothing for the dispatch to classify. The routes themselves are the subject here.</para></summary>
    [Theory]
    [InlineData("no-op RegenerateEpics")]
    [InlineData("no-op RegenerateAdrs")]
    [InlineData("no-op GenerateAll")]
    public void NoOpControls(string label)
    {
        var incrementalOptions = Options(OutIncremental);
        var generator = new SiteGenerator(incrementalOptions);
        Assert.DoesNotContain(generator.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        switch (label)
        {
            case "no-op RegenerateEpics": generator.RegenerateEpics(); break;
            case "no-op RegenerateAdrs": generator.RegenerateAdrs(); break;
            default: generator.GenerateAll(); break;
        }

        var oracle = new SiteGenerator(Options(OutOracle));
        Assert.DoesNotContain(oracle.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var diff = Diff(OutIncremental, OutOracle);
        Assert.True(diff.IsClean, diff.Describe(label));
    }

    [Fact]
    public void ContentEditToGenericDoc_MatchesOracle()
    {
        var diff = RunClass(ContentEditGenericDoc);
        Assert.True(diff.IsClean, diff.Describe("content-doc"));
    }

    [Fact]
    public void ContentEditToStoryArtifact_MatchesOracle()
    {
        var diff = RunClass(ContentEditStory);
        Assert.True(diff.IsClean, diff.Describe("content-story"));
    }

    [Fact]
    public void AddedGenericDoc_MatchesOracle()
    {
        var diff = RunClass(AddGenericDoc);
        Assert.True(diff.IsClean, diff.Describe("add-doc"));
    }

    [Fact]
    public void RenamedGenericDoc_MatchesOracle()
    {
        var diff = RunClass(RenameGenericDoc);
        Assert.True(diff.IsClean, diff.Describe("rename-doc"));
    }

    [Fact]
    public void DeletedStoryArtifact_MatchesOracle()
    {
        var diff = RunClass(DeleteStory);
        Assert.True(diff.IsClean, diff.Describe("delete-story"));
    }

    [Fact]
    public void DeletedAdr_MatchesOracle()
    {
        var diff = RunClass(DeleteAdr);
        Assert.True(diff.IsClean, diff.Describe("delete-adr"));
    }

    /// <summary>Story 22.5 Trap 4, resolved by measurement. Deleting <c>epics.md</c> ESCALATES rather than keeping
    /// Story 5.3 AC #3's bespoke teardown, because the teardown provably cannot reach the oracle: with
    /// <c>epics.md</c> gone the story artifacts stop being consumed by the epics family, and a full rebuild renders
    /// them as ordinary docs — three pages the teardown has no way to produce (it deletes the epics family; it does
    /// not re-render what fell out of it). Exempting the branch first and diffing it is what surfaced that; the
    /// exemption cost 16 stale + 3 missing pages.
    /// <para>The trade is the watch log: the pass now reports <c>&lt;directory change&gt;</c> rather than
    /// "epics.md removed; N stale page(s) deleted". <see cref="SiteGenerator.ClearEpicsFamilyOutputs"/> and its 8
    /// tests are untouched and still reachable through <see cref="SiteGenerator.RegenerateEpics"/> directly, which
    /// is how <c>SiteGeneratorEpicsRemovalTests</c> drives them.</para></summary>
    [Fact]
    public void DeletedEpicsFile_EscalatesAndMatchesOracle()
    {
        var diff = RunClass(DeleteEpics);
        Assert.True(diff.IsClean, diff.Describe("delete-epics"));

        var escalation = Assert.Single(_events);
        Assert.Equal("<directory change>", escalation.RelativePath);
    }

    /// <summary>The classifier's own rule, stated directly: existence at fire time versus what the last completed
    /// pass rendered. Driven on a live generator so the inventory is the real one, not a hand-built stub.</summary>
    [Fact]
    public void ClassifyRebuildScope_SeparatesContentChangesFromTopologyChanges()
    {
        var generator = new SiteGenerator(Options(OutIncremental));
        generator.GenerateAll();

        var existing = Path.Combine(PlanningDir, "architecture.md");
        var story = Path.Combine(ImplDir, "1-1-foundation.md");
        var adr = Path.Combine(Adrs, "0002-second.md");

        // Content: the file was rendered and is still there.
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(existing));
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(story));
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(adr));

        // Topology: appeared (never rendered, exists now) — in BOTH watched roots, since ADRs live outside the
        // source root and strand their own surfaces.
        var added = Path.Combine(PlanningDir, "brand-new.md");
        File.WriteAllText(added, "# New\n");
        Assert.Equal(RebuildScope.Full, generator.ClassifyRebuildScope(added));

        var addedAdr = Path.Combine(Adrs, "0003-third.md");
        File.WriteAllText(addedAdr, "# 3. Third\n\nStatus: Accepted\n");
        Assert.Equal(RebuildScope.Full, generator.ClassifyRebuildScope(addedAdr));

        // Topology: disappeared (was rendered, gone now).
        File.Delete(story);
        Assert.Equal(RebuildScope.Full, generator.ClassifyRebuildScope(story));

        // Never escalates on something that would never have become a page — a non-markdown file, or an ignored
        // editor temp file. Without these guards a lock file appearing beside an artifact rebuilds the whole site.
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(Path.Combine(PlanningDir, "notes.txt")));
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(Path.Combine(PlanningDir, ".hidden.md")));

        // epics.md gets NO special case: edited it is content, deleted it is topology (Trap 4 — see
        // DeletedEpicsFile_EscalatesAndMatchesOracle for why the exemption was measured and dropped).
        var epics = Path.Combine(PlanningDir, "epics.md");
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(epics));
        File.Delete(epics);
        Assert.Equal(RebuildScope.Full, generator.ClassifyRebuildScope(epics));
    }

    /// <summary>AC #7: an escalated pass reports ONE coherent event — the shape
    /// <see cref="SiteGenerator.RegenerateTopology"/> already uses — not a flood of per-page events into the watch
    /// log. Driven through <see cref="FileWatcherService.RunDebouncedPass"/> synchronously, the seam Story 5.3 added
    /// precisely so a regression here surfaces as a test failure instead of taking down the test host.</summary>
    [Fact]
    public void EscalatedPass_ReportsOneCoherentEvent()
    {
        var options = Options(OutIncremental);
        var generator = new SiteGenerator(options);
        generator.GenerateAll();

        using var watcher = new FileWatcherService(options, generator, e => { lock (_events) _events.Add(e); });
        var added = Path.Combine(PlanningDir, "brand-new.md");
        File.WriteAllText(added, "# New\n\nBody.\n");
        watcher.RunDebouncedPass(added);

        var escalation = Assert.Single(_events);
        Assert.Equal(GenerationOutcome.Updated, escalation.Outcome);
        Assert.Equal("<directory change>", escalation.RelativePath);
        Assert.Equal("full rebuild", escalation.Message);
    }
}
