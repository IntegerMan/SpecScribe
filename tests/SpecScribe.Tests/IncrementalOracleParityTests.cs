using System.Text.RegularExpressions;
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

    private ForgeOptions Options(string output, bool emitSpa = false) => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: output, projectName: "SpecScribe", includeReadme: false,
        emitSpa: emitSpa);

    /// <summary>Floor on the oracle's file count, below which a "clean" diff means the harness produced nothing
    /// rather than that the routes agreed. Without it every assertion here passes vacuously the moment a fixture or
    /// option change stops the generator emitting — the failure mode Story 22.4's equivalent test guards with its own
    /// VACUOUS assertion and this file originally shipped without. The fixture emits ~40 files. [code review
    /// 2026-07-29]</summary>
    private const int OracleFileFloor = 20;

    // ── the harness ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One change class, end to end. Builds the PRE-change tree with a live generator (as watch mode
    /// does), applies <paramref name="mutate"/>, drives the SHIPPED watch dispatch over the paths it reports, then
    /// full-generates the identical POST-change tree into a second output root and diffs the two byte-for-byte.</summary>
    private OracleDiff RunClass(Func<IReadOnlyList<string>> mutate, bool emitSpa = false)
    {
        var incrementalOptions = Options(OutIncremental, emitSpa);
        var generator = new SiteGenerator(incrementalOptions);
        Assert.DoesNotContain(generator.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // The REAL dispatch, not a hand-written call sequence: RunDebouncedPass owns the fire-time predicate order
        // (IsDataSource → IsAdr → IsEpicsRelated → GenerateOne/RemoveFor) and a test that re-implemented it could
        // drift from the thing that actually runs in watch mode. It is `internal` precisely as this seam
        // (Story 5.3), and the watchers it constructs are NOT started, so nothing here is timing-dependent.
        using var watcher = new FileWatcherService(incrementalOptions, generator, e => { lock (_events) _events.Add(e); });
        foreach (var path in mutate()) watcher.RunDebouncedPass(path);

        // A route that ERRORED could still leave two trees that happen to agree — most obviously when the failure was
        // in a refresh step whose output the fixture does not exercise. Byte parity over a broken pass is not parity;
        // the pass has to have succeeded for the comparison to mean anything. [code review 2026-07-29]
        lock (_events)
        {
            Assert.DoesNotContain(_events, e => e.Outcome == GenerationOutcome.Error);
        }

        var oracle = new SiteGenerator(Options(OutOracle, emitSpa));
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

        Assert.True(
            oracle.Count >= OracleFileFloor,
            $"VACUOUS: the oracle produced only {oracle.Count} files (floor {OracleFileFloor}), so a clean diff "
            + "would prove nothing. The fixture or the generate options stopped producing output.");

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

        // Directories as well as files: a narrow route that deletes the last page in a subtree leaves the now-empty
        // directory behind, where the oracle's output-root wipe never creates it. A file-only comparison reports that
        // as clean. [code review 2026-07-29]
        var oracleDirs = RelativeDirectories(oracleRoot);
        foreach (var relative in RelativeDirectories(incrementalRoot))
        {
            if (!oracleDirs.Contains(relative)) orphaned.Add(relative + "/ (empty directory)");
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
            var content = GoldenNormalization.NormalizeVolatile(
                File.ReadAllText(path), OutIncremental, OutOracle, RepoRoot);
            snapshot[relative] = relative == "spa/manifest.json" ? FoldOutputPathDependentHash(content) : content;
        }
        return snapshot;
    }

    /// <summary>Trap 5, resurfacing one level down. <c>diagnostics.html</c> echoes the configured OUTPUT ROOT inside
    /// its own region, and this harness generates the two trees into two different directories — handled for PAGE bytes
    /// by folding every root to one placeholder. The manifest cannot be handled that way: its per-page
    /// <c>contentHash</c> and <c>bytes</c> are computed over the page's RAW, unfolded content, so they differ by
    /// construction (measured: exactly the 5-character difference between the two output directory names) even though
    /// the page itself compares equal. Folding those two FIELDS for that ONE page keeps every other page's hash and
    /// byte count fully under the gate, which is the whole value of comparing the manifest at all.
    /// <para>Deliberately local to this harness rather than added to the shared <see cref="GoldenNormalization"/>: the
    /// golden fingerprint gate compares one tree against a stored constant, never two trees in two directories, so it
    /// has no such artifact — and widening the shared fold to cover a problem only this file has would blunt that gate
    /// for no reason. [code review 2026-07-29]</para></summary>
    private static string FoldOutputPathDependentHash(string manifestJson) =>
        Regex.Replace(
            manifestJson,
            @"(""diagnostics\.html"":\{.*?""contentHash"":"")[0-9a-f]+("",""bytes"":)\d+",
            "$1<output-path-dependent>$2-1",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>Every directory under <paramref name="root"/>, date-folded the same way <see cref="Snapshot"/> folds
    /// file paths so a date-named subtree does not read as an orphan. [code review 2026-07-29]</summary>
    private static HashSet<string> RelativeDirectories(string root)
    {
        var dirs = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(root)) return dirs;
        foreach (var path in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            dirs.Add(GoldenNormalization.FoldToday(PathUtil.NormalizeSlashes(Path.GetRelativePath(root, path))));
        }
        return dirs;
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

    /// <summary>Content edit to an ADR — the <see cref="SiteGenerator.RegenerateAdrs"/> narrow class. The class→scope
    /// table claimed this was proven byte-identical when no case in the harness or the repo-scale matrix ever edited an
    /// ADR's content; the only ADR coverage was a no-change control and a delete. [code review 2026-07-29]</summary>
    private IReadOnlyList<string> ContentEditAdr()
    {
        var path = Path.Combine(Adrs, "0002-second.md");
        File.AppendAllText(path, "\n\nA content-only paragraph.\n");
        return new[] { path };
    }

    /// <summary>Content edit to <c>epics.md</c> itself — distinct from a story-artifact edit, since it re-parses the
    /// epic set rather than one artifact's fragments, and distinct from its DELETION, which escalates (Trap 4). Also
    /// claimed proven with no case behind it. [code review 2026-07-29]</summary>
    private IReadOnlyList<string> ContentEditEpicsFile()
    {
        var path = Path.Combine(PlanningDir, "epics.md");
        File.AppendAllText(path, "\n\nA trailing note that adds no epic.\n");
        return new[] { path };
    }

    /// <summary>The same rename, dispatched new-path-first. Production arms one debounce timer per changed path on its
    /// own thread-pool thread, so the delete and the create can settle in EITHER order — the single order the original
    /// case pinned was an assumption, not a guarantee. [code review 2026-07-29]</summary>
    private IReadOnlyList<string> RenameGenericDocReversedOrder()
    {
        var from = Path.Combine(PlanningDir, "architecture.md");
        var to = Path.Combine(PlanningDir, "architecture-renamed.md");
        File.Move(from, to);
        return new[] { to, from };
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

    /// <summary>AC #2's third clause, stated as its own assertion rather than left implicit in the whole-tree byte
    /// diff: <b>per-epic work-graph NODE and EDGE counts must be equal between the narrow route and a full
    /// rebuild.</b> The Story 22.1 spike asked for exactly this — <i>"22.5's parity fix should add a node/edge
    /// assertion to the harness so this becomes a measured, regression-guarded number"</i> — because the defect it
    /// found was numeric and per-epic (Epic 1: 16 items / 20 links from <c>RegenerateEpics</c> against 13 / 12 from
    /// <c>GenerateAll</c>, across 56 pages), and a byte diff reports "some page differs" where the number is the
    /// diagnosis. Added in code review 2026-07-29: the story checked this subtask off, but no such assertion existed
    /// anywhere in the suite — the only node/edge comparison was Story 22.4's static-page-versus-IR test, a different
    /// pair of paths entirely.
    /// <para>The counts are read from the shipped ADR 0013 text twin (<c>Charts.cs</c>'s "Work graph for X: N work
    /// items and M provenance links"), so this asserts on what a reader actually gets rather than on a private field —
    /// and it fails LOUDLY if the fixture stops rendering any work-graph summary at all, which is the vacuity trap
    /// that would otherwise make this assertion decorative.</para></summary>
    [Fact]
    public void RegenerateEpics_ProducesTheSamePerEpicWorkGraphCountsAsAFullRebuild()
    {
        var generator = new SiteGenerator(Options(OutIncremental));
        Assert.DoesNotContain(generator.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        generator.RegenerateEpics();

        var oracle = new SiteGenerator(Options(OutOracle));
        Assert.DoesNotContain(oracle.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var narrow = WorkGraphCounts(OutIncremental);
        var full = WorkGraphCounts(OutOracle);

        Assert.True(
            full.Count > 0,
            "VACUOUS: no work-graph summary was rendered anywhere in the oracle output, so this assertion compared "
            + "two empty sets. The node/edge counts AC #2 requires would be unguarded.");

        Assert.Equal(full, narrow);
    }

    /// <summary>Every rendered work-graph bucket's node/edge counts, keyed by bucket name, across the whole output
    /// tree. Matches the ADR 0013 text twin emitted by <c>Charts.WorkGraph</c>. [code review 2026-07-29]</summary>
    private static SortedDictionary<string, string> WorkGraphCounts(string root)
    {
        var counts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(root)) return counts;

        var pattern = new Regex(
            @"Work graph for (?<epic>[^:<]+): (?<nodes>\d+) work items and (?<edges>\d+) provenance links",
            RegexOptions.CultureInvariant);

        foreach (var path in Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories))
        {
            var relative = PathUtil.NormalizeSlashes(Path.GetRelativePath(root, path));
            foreach (var match in pattern.Matches(File.ReadAllText(path)).Cast<Match>())
            {
                // Keyed by page AND bucket: the same epic's graph can appear on more than one surface, and a route
                // that refreshed one of them but not another is precisely the divergence class being guarded.
                counts[$"{relative}::{match.Groups["epic"].Value.Trim()}"] =
                    $"{match.Groups["nodes"].Value} nodes / {match.Groups["edges"].Value} edges";
            }
        }
        return counts;
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
    public void ContentEditToAdr_MatchesOracle()
    {
        var diff = RunClass(ContentEditAdr);
        Assert.True(diff.IsClean, diff.Describe("content-adr"));
    }

    [Fact]
    public void ContentEditToEpicsFile_MatchesOracle()
    {
        var diff = RunClass(ContentEditEpicsFile);
        Assert.True(diff.IsClean, diff.Describe("content-epics"));
    }

    /// <summary>The narrow content classes again with the opt-in IR form ON. Trap 3's whole argument for fixing the
    /// recompute rather than the emit is that <c>EmitSpaSite</c> rewrites the entire manifest from current state, so
    /// "the IR inherits every recompute defect verbatim" — but the gate ran with <c>emitSpa: false</c>, which means no
    /// <c>spa/</c> file was ever compared and that inheritance was asserted rather than measured. [code review
    /// 2026-07-29]</summary>
    [Theory]
    [InlineData("content-doc")]
    [InlineData("content-story")]
    public void NarrowContentClasses_MatchOracle_WithTheIrEmitted(string label)
    {
        var diff = RunClass(label == "content-doc" ? ContentEditGenericDoc : ContentEditStory, emitSpa: true);
        Assert.True(diff.IsClean, diff.Describe($"{label} (--spa)"));

        // The IR really was emitted, so the row above is not silently the non-SPA case again.
        Assert.Contains(Directory.EnumerateFiles(OutIncremental, "*", SearchOption.AllDirectories),
            p => PathUtil.NormalizeSlashes(Path.GetRelativePath(OutIncremental, p)).StartsWith("spa/", StringComparison.Ordinal));
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
    public void RenamedGenericDoc_MatchesOracle_WhenTheNewPathSettlesFirst()
    {
        var diff = RunClass(RenameGenericDocReversedOrder);
        Assert.True(diff.IsClean, diff.Describe("rename-doc (new path first)"));
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
    /// <para>The trade is the watch log: the pass reports one escalated <c>full rebuild</c> event labelled with the
    /// deleted path, rather than "epics.md removed; N stale page(s) deleted" with its page count.
    /// <see cref="SiteGenerator.ClearEpicsFamilyOutputs"/> and its 8 tests are untouched and still reachable through
    /// <see cref="SiteGenerator.RegenerateEpics"/> directly, which is how <c>SiteGeneratorEpicsRemovalTests</c> drives
    /// them — but note that is TEST reachability, not production reachability: the watch dispatch no longer selects
    /// that branch for any input. Owner decision (code review 2026-07-29) accepted that, on the measured ground that
    /// escalation is strictly more correct; ADR 0027 records it so the 8 green tests are not misread as proof the
    /// dispatch still covers the teardown.</para></summary>
    [Fact]
    public void DeletedEpicsFile_EscalatesAndMatchesOracle()
    {
        var diff = RunClass(DeleteEpics);
        Assert.True(diff.IsClean, diff.Describe("delete-epics"));

        // ONE event, labelled with the file that fired — not the <directory change> sentinel, which means "a directory
        // changed, so do not attribute this to some arbitrary contained file" and is therefore the wrong label when a
        // named file IS the whole event. [code review 2026-07-29]
        var escalation = Assert.Single(_events);
        Assert.Equal("_bmad-output/planning-artifacts/epics.md", escalation.RelativePath);
        Assert.Equal("full rebuild", escalation.Message);
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
        //
        // These files are CREATED first, deliberately (code review 2026-07-29). Asserted against paths that do not
        // exist, both assertions passed for the wrong reason: exists=false and wasRendered=false agree, so the rule
        // itself returns Narrow and the `.md`/IsIgnored guards could both be deleted with the test still green. Only a
        // file that exists on disk while being absent from the inventory actually exercises them.
        var notMarkdown = Path.Combine(PlanningDir, "notes.txt");
        var ignoredMarkdown = Path.Combine(PlanningDir, ".hidden.md");
        File.WriteAllText(notMarkdown, "not markdown\n");
        File.WriteAllText(ignoredMarkdown, "# ignored\n");
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(notMarkdown));
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(ignoredMarkdown));

        // A null or relative path answers Narrow rather than throwing out of the dispatch or resolving against the
        // process working directory. [code review 2026-07-29]
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(null!));
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(""));

        // An ADR nested DEEPER than EnumerateAdrFiles walks (root + one level) can never enter the inventory, so a
        // naive existence comparison would answer Full on every save forever — a non-convergent full-rebuild loop.
        // [code review 2026-07-29]
        var deepAdrDir = Path.Combine(Adrs, "decisions", "2026");
        Directory.CreateDirectory(deepAdrDir);
        var deepAdr = Path.Combine(deepAdrDir, "0009-nested.md");
        File.WriteAllText(deepAdr, "# 9. Nested\n\nStatus: Accepted\n");
        Assert.Equal(RebuildScope.Narrow, generator.ClassifyRebuildScope(deepAdr));

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
        // The triggering path, not the <directory change> sentinel — see DeletedEpicsFile_EscalatesAndMatchesOracle.
        // A DIRECTORY-level pass (RunTopologyPass) still reports the sentinel, since it has no single honest path.
        Assert.Equal("_bmad-output/planning-artifacts/brand-new.md", escalation.RelativePath);
        Assert.Equal("full rebuild", escalation.Message);
    }
}
