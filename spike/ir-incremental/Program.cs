using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.IrIncrementalSpike;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Story 22.1 SPIKE — Incremental Recompute + IR-Delta Transport.  THROWAWAY (see SpecScribe.IrIncrementalSpike.csproj).
//
// Answers three questions against THIS repo with real numbers, driving the REAL shipped SiteGenerator:
//
//   AXIS 1  LATENCY   — full GenerateAll wall-clock, deep-git ON vs OFF (to isolate the git-subprocess share that
//                       ADR 0008 says dominates gen-time), plus per-change-class incremental-route latency.
//   AXIS 2  CORRECTNESS — for each change class, run the shipped watch-mode incremental route on a live generator,
//                       then a FULL regenerate of the identical post-change source tree (the oracle), and diff the
//                       two output trees byte-for-byte with the SAME per-run/per-build noise folding the
//                       GoldenContentFingerprint gate uses. Byte-identical ⇒ the narrow route is correct for that
//                       class; any diff ⇒ staleness, and we enumerate exactly which pages went stale.
//   AXIS 3  IR-DELTA  — using the shipped SpaDelivery JSON chunk form as the IR, measure the delta a single content
//                       edit vs a topology change actually re-ships, and how the byte-bounded chunker caps it.
//
// It never re-parses .md itself or scrapes .html: it serializes / diffs what the core already produced (AD-1/AD-2).
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
internal static class Program
{
    private static int Main(string[] args)
    {
        var repoRoot = Path.GetFullPath(GetOption(args, "--repo") ?? FindRepoRoot() ?? Directory.GetCurrentDirectory());
        var scratch = Path.GetFullPath(GetOption(args, "--out") ?? Path.Combine(Path.GetTempPath(), "ss-ir-spike"));

        Console.Error.WriteLine($"[spike] repo   = {repoRoot}");
        Console.Error.WriteLine($"[spike] scratch= {scratch}");
        if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
        Directory.CreateDirectory(scratch);

        var report = new Dictionary<string, object?>();
        var mode = GetOption(args, "--mode") ?? "full";

        // ── Pristine mutable sandbox (only what the core ingests): _bmad-output + docs. No .git ⇒ deep-git off for
        //    the correctness matrix, so the oracle and the incremental run read the identical inputs. ────────────
        var template = Path.Combine(scratch, "template");
        CopyIngestedSources(repoRoot, template);
        Console.Error.WriteLine($"[spike] sandbox template built at {template}");

        // ── CONTROL: does a NO-OP incremental route already diverge from a cold GenerateAll on the same tree?
        //    Isolates route-vs-oracle divergence from any change ripple. (correct ⇒ divergence is change-driven;
        //    stale ⇒ the route inherently produces different output than a full rebuild.) ────────────────────────
        report["controls"] = new[]
        {
            RunControl("noop-regenerate-epics", template, scratch, g => g.RegenerateEpics()),
            RunControl("noop-regenerate-adrs", template, scratch, g => g.RegenerateAdrs()),
            RunControl("noop-generate-all", template, scratch, g => g.GenerateAll()), // pure determinism sanity check
        };
        if (mode == "controls")
        {
            var cjson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(scratch, "controls.json"), cjson);
            Console.WriteLine(cjson);
            return 0;
        }

        // ── AXIS 1: full-regen latency against the REAL repo (deep-git ON vs OFF) ──────────────────────────────
        report["latency"] = MeasureBaselineLatency(repoRoot, Path.Combine(scratch, "latency"));

        // ── AXIS 2: correctness matrix ────────────────────────────────────────────────────────────────────────
        var caseResults = new List<CaseResult>();
        var cases = new (string id, string desc, Func<string, ChangePlan> mutate)[]
        {
            ("content-story", "Content-only edit of an implementation-artifacts STORY (routes → RegenerateEpics)", MutateContentStory),
            ("content-doc",   "Content-only edit of a generic planning DOC (routes → GenerateOne)", MutateContentDoc),
            ("add-doc",       "TOPOLOGY add: brand-new generic doc appears (routes → GenerateOne, cached nav)", MutateAddDoc),
            ("delete-story",  "TOPOLOGY delete: remove a story artifact (routes → RegenerateEpics)", MutateDeleteStory),
            ("rename-doc",    "TOPOLOGY rename: generic doc old→new path (RemoveFor(old)+GenerateOne(new))", MutateRenameDoc),
            ("delete-adr",    "TOPOLOGY delete: remove an ADR (routes → RegenerateAdrs)", MutateDeleteAdr),
        };
        foreach (var (id, desc, mutate) in cases)
        {
            try { caseResults.Add(RunCase(id, desc, template, scratch, mutate)); }
            catch (Exception ex) { Console.Error.WriteLine($"[spike] CASE {id} ERROR: {ex}"); }
        }
        report["correctness"] = caseResults;

        // ── AXIS 3: IR-delta transport via the shipped SpaDelivery chunk form ────────────────────────────────
        report["irDelta"] = MeasureIrDelta(template, scratch);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var reportPath = Path.Combine(scratch, "report.json");
        File.WriteAllText(reportPath, json);
        Console.Error.WriteLine($"\n[spike] wrote {reportPath}");
        Console.WriteLine(json);
        return 0;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // AXIS 1 — latency
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    private static object MeasureBaselineLatency(string repoRoot, string outBase)
    {
        var (onWarm, onCold, pages) = TimeFullGen(repoRoot, Path.Combine(outBase, "deepgit-on"), deepGit: true);
        var (offWarm, offCold, _) = TimeFullGen(repoRoot, Path.Combine(outBase, "deepgit-off"), deepGit: false);
        return new
        {
            note = "Full GenerateAll against the real repo. cold = first run, warm = best-of-2 subsequent runs (ms).",
            pages,
            deepGitOn = new { coldMs = onCold, warmMs = onWarm },
            deepGitOff = new { coldMs = offCold, warmMs = offWarm },
            gitSubprocessShareWarmMs = Math.Max(0, onWarm - offWarm),
            gitSubprocessSharePct = onWarm > 0 ? Math.Round(100.0 * Math.Max(0, onWarm - offWarm) / onWarm, 1) : 0,
        };
    }

    private static (double warmMedian, double cold, int pages) TimeFullGen(string repoRoot, string outDir, bool deepGit)
    {
        double Run()
        {
            var opts = ForgeOptions.Resolve(startDirectory: repoRoot, output: outDir, deepGitAnalytics: deepGit);
            var gen = new SiteGenerator(opts);
            var sw = Stopwatch.StartNew();
            gen.GenerateAll();
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        var cold = Run();
        var warm = new[] { Run(), Run() }.Min(); // best-of-2 warm (least GC/JIT noise)
        var pages = Directory.Exists(outDir) ? Directory.EnumerateFiles(outDir, "*.html", SearchOption.AllDirectories).Count() : 0;
        return (Math.Round(warm), Math.Round(cold), pages);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // AXIS 2 — correctness: one change class = incremental route output vs full-regen oracle
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    private static CaseResult RunCase(string id, string desc, string template, string scratch, Func<string, ChangePlan> mutate)
    {
        var caseDir = Path.Combine(scratch, "case-" + id);
        var sandbox = Path.Combine(caseDir, "src");
        CopyDir(template, sandbox);
        var srcRoot = Path.Combine(sandbox, ForgeOptions.SourceDirName);
        var adrRoot = Path.Combine(sandbox, "docs", "adrs");

        var outIncremental = Path.Combine(caseDir, "out-incremental");
        var outOracle = Path.Combine(caseDir, "out-oracle");

        // G1: full build of the PRE-change tree, then keep it alive to receive the incremental event (like watch mode).
        var g1 = new SiteGenerator(ForgeOptions.Resolve(source: srcRoot, adrs: adrRoot, output: outIncremental));
        g1.GenerateAll();

        var plan = mutate(sandbox);

        // Drive the SHIPPED watch dispatch on the live G1 — same predicate order FileWatcherService uses.
        var incrementalSw = Stopwatch.StartNew();
        foreach (var (fullPath, _) in plan.Events) Dispatch(g1, fullPath);
        incrementalSw.Stop();

        // G2: full regenerate of the identical POST-change tree = the correctness oracle.
        var g2 = new SiteGenerator(ForgeOptions.Resolve(source: srcRoot, adrs: adrRoot, output: outOracle));
        var oracleSw = Stopwatch.StartNew();
        g2.GenerateAll();
        oracleSw.Stop();

        var fold = new[] { outIncremental, outOracle, sandbox };
        var incr = SnapshotNormalized(outIncremental, fold);
        var orac = SnapshotNormalized(outOracle, fold);

        var stale = new List<string>();      // present in both, content differs
        var orphaned = new List<string>();   // in incremental only (narrow route failed to delete)
        var missing = new List<string>();    // in oracle only (narrow route failed to create)
        foreach (var (rel, content) in orac)
        {
            if (!incr.TryGetValue(rel, out var ic)) missing.Add(rel);
            else if (ic != content) stale.Add(rel);
        }
        foreach (var rel in incr.Keys) if (!orac.ContainsKey(rel)) orphaned.Add(rel);

        var correct = stale.Count == 0 && orphaned.Count == 0 && missing.Count == 0;
        Console.Error.WriteLine(
            $"[case {id,-14}] {(correct ? "CORRECT (byte-identical)" : "STALE")}  " +
            $"stale={stale.Count} orphaned={orphaned.Count} missing={missing.Count}  " +
            $"incr={Math.Round(incrementalSw.Elapsed.TotalMilliseconds)}ms full={Math.Round(oracleSw.Elapsed.TotalMilliseconds)}ms");

        return new CaseResult(
            id, desc, plan.RouteNote, correct, orac.Count,
            stale.Count, orphaned.Count, missing.Count,
            stale.OrderBy(x => x, StringComparer.Ordinal).Take(25).ToList(),
            orphaned.OrderBy(x => x, StringComparer.Ordinal).Take(25).ToList(),
            missing.OrderBy(x => x, StringComparer.Ordinal).Take(25).ToList(),
            Math.Round(incrementalSw.Elapsed.TotalMilliseconds, 1),
            Math.Round(oracleSw.Elapsed.TotalMilliseconds, 1));
    }

    // A no-op control: build the tree, run one incremental route with NO source change, diff vs a cold full regen.
    private static CaseResult RunControl(string id, string template, string scratch, Action<SiteGenerator> route)
    {
        var caseDir = Path.Combine(scratch, "control-" + id);
        var sandbox = Path.Combine(caseDir, "src");
        CopyDir(template, sandbox);
        var srcRoot = Path.Combine(sandbox, ForgeOptions.SourceDirName);
        var adrRoot = Path.Combine(sandbox, "docs", "adrs");
        var outIncremental = Path.Combine(caseDir, "out-incremental");
        var outOracle = Path.Combine(caseDir, "out-oracle");

        var g1 = new SiteGenerator(ForgeOptions.Resolve(source: srcRoot, adrs: adrRoot, output: outIncremental));
        g1.GenerateAll();
        var sw = Stopwatch.StartNew();
        route(g1);                 // NO source change first — pure route-vs-oracle divergence
        sw.Stop();

        var g2 = new SiteGenerator(ForgeOptions.Resolve(source: srcRoot, adrs: adrRoot, output: outOracle));
        g2.GenerateAll();

        var fold = new[] { outIncremental, outOracle, sandbox };
        var incr = SnapshotNormalized(outIncremental, fold);
        var orac = SnapshotNormalized(outOracle, fold);
        var stale = new List<string>();
        var orphaned = new List<string>();
        var missing = new List<string>();
        foreach (var (rel, content) in orac)
        {
            if (!incr.TryGetValue(rel, out var ic)) missing.Add(rel);
            else if (ic != content) stale.Add(rel);
        }
        foreach (var rel in incr.Keys) if (!orac.ContainsKey(rel)) orphaned.Add(rel);
        var correct = stale.Count == 0 && orphaned.Count == 0 && missing.Count == 0;
        Console.Error.WriteLine($"[control {id,-22}] {(correct ? "CORRECT" : "DIVERGES")}  stale={stale.Count} orphaned={orphaned.Count} missing={missing.Count}");
        return new CaseResult(id, "no-op control", "route with NO source change vs cold GenerateAll", correct, orac.Count,
            stale.Count, orphaned.Count, missing.Count,
            stale.OrderBy(x => x, StringComparer.Ordinal).Take(25).ToList(),
            orphaned.OrderBy(x => x, StringComparer.Ordinal).Take(25).ToList(),
            missing.OrderBy(x => x, StringComparer.Ordinal).Take(25).ToList(),
            Math.Round(sw.Elapsed.TotalMilliseconds, 1), 0);
    }

    // Replica of FileWatcherService's fire-time routing (same predicate order). [FileWatcherService.cs:231-241]
    private static void Dispatch(SiteGenerator g, string fullPath)
    {
        if (g.IsDataSource(fullPath)) { g.RegenerateFromDataSource(fullPath); return; }
        if (g.IsAdr(fullPath)) { g.RegenerateAdrs(); return; }
        if (g.IsEpicsRelated(fullPath)) { g.RegenerateEpics(); return; }
        if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return; // non-md, not a data source → skipped
        if (File.Exists(fullPath)) g.GenerateOne(fullPath); else g.RemoveFor(fullPath);
    }

    // ── Change-class mutators. Each returns the watch events + a note on which route they exercise. ────────────
    private static ChangePlan MutateContentStory(string sandbox)
    {
        var impl = Path.Combine(sandbox, ForgeOptions.SourceDirName, "implementation-artifacts");
        var story = Directory.EnumerateFiles(impl, "*.md").First(f => Regex.IsMatch(Path.GetFileName(f), @"^\d+-\d+-"));
        File.AppendAllText(story, "\n\nSpike content-only edit — one appended paragraph to measure a narrow rebuild.\n");
        return new ChangePlan(new[] { (story, true) }, "IsEpicsRelated ⇒ RegenerateEpics");
    }

    private static ChangePlan MutateContentDoc(string sandbox)
    {
        var doc = PickGenericDoc(sandbox);
        File.AppendAllText(doc, "\n\nSpike content-only edit to a generic doc.\n");
        return new ChangePlan(new[] { (doc, true) }, "generic .md ⇒ GenerateOne");
    }

    private static ChangePlan MutateAddDoc(string sandbox)
    {
        var dir = Path.Combine(sandbox, ForgeOptions.SourceDirName, "planning-artifacts");
        Directory.CreateDirectory(dir);
        var doc = Path.Combine(dir, "zzz-spike-new-doc.md");
        File.WriteAllText(doc, "# Spike New Doc\n\nA brand-new generic doc added while the generator is live.\n");
        return new ChangePlan(new[] { (doc, true) }, "new generic .md ⇒ GenerateOne (nav is cached _nav)");
    }

    private static ChangePlan MutateDeleteStory(string sandbox)
    {
        var impl = Path.Combine(sandbox, ForgeOptions.SourceDirName, "implementation-artifacts");
        // Delete a LATER-numbered story so its epic still has surviving siblings (isolates the delete, not an epic wipe).
        var story = Directory.EnumerateFiles(impl, "*.md")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d+-\d+-"))
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).First();
        File.Delete(story);
        return new ChangePlan(new[] { (story, false) }, "IsEpicsRelated ⇒ RegenerateEpics (delete)");
    }

    private static ChangePlan MutateRenameDoc(string sandbox)
    {
        var doc = PickGenericDoc(sandbox);
        var renamed = Path.Combine(Path.GetDirectoryName(doc)!, "renamed-" + Path.GetFileName(doc));
        File.Move(doc, renamed);
        // A rename surfaces as delete(old)+create(new); the watcher debounces each into its own dispatch.
        return new ChangePlan(new[] { (doc, false), (renamed, true) }, "RemoveFor(old) + GenerateOne(new)");
    }

    private static ChangePlan MutateDeleteAdr(string sandbox)
    {
        var adrDir = Path.Combine(sandbox, "docs", "adrs");
        var adr = Directory.EnumerateFiles(adrDir, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("0001", StringComparison.Ordinal)) // keep the first ADR
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).First();
        File.Delete(adr);
        return new ChangePlan(new[] { (adr, false) }, "IsAdr ⇒ RegenerateAdrs (delete)");
    }

    // A generic doc = a scanned .md that is NOT epics-related, NOT an ADR, NOT a data source → the GenerateOne route.
    private static string PickGenericDoc(string sandbox)
    {
        var src = Path.Combine(sandbox, ForgeOptions.SourceDirName);
        foreach (var f in Directory.EnumerateFiles(src, "*.md", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(f);
            if (name.Equals("epics.md", StringComparison.OrdinalIgnoreCase)) continue;
            if (f.Replace('\\', '/').Contains("/implementation-artifacts/")) continue; // epics-related route
            if (Regex.IsMatch(name, @"^\d+-\d+-")) continue;                            // a story file
            return f;
        }
        throw new InvalidOperationException("no generic doc found in sandbox");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // AXIS 3 — IR-delta transport (SpaDelivery chunk form as the IR)
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    private static object MeasureIrDelta(string template, string scratch)
    {
        var caseDir = Path.Combine(scratch, "irdelta");
        var sandbox = Path.Combine(caseDir, "src");
        CopyDir(template, sandbox);
        var srcRoot = Path.Combine(sandbox, ForgeOptions.SourceDirName);
        var adrRoot = Path.Combine(sandbox, "docs", "adrs");
        var outDir = Path.Combine(caseDir, "out");

        var gen = new SiteGenerator(ForgeOptions.Resolve(source: srcRoot, adrs: adrRoot, output: outDir, emitSpa: true));
        gen.GenerateAll();

        var before = ReadSpaChunks(outDir);

        // (a) content-only edit of one story → RegenerateEpics re-emits the SPA. Measure which chunks changed.
        var impl = Path.Combine(srcRoot, "implementation-artifacts");
        var story = Directory.EnumerateFiles(impl, "*.md").First(f => Regex.IsMatch(Path.GetFileName(f), @"^\d+-\d+-"));
        File.AppendAllText(story, "\n\nIR-delta content edit.\n");
        gen.RegenerateEpics();
        var afterContent = ReadSpaChunks(outDir);
        var contentDelta = ChunkDelta(before, afterContent);

        // (b) topology delete of a story → RegenerateEpics re-emits the SPA. Measure the re-shipped chunk mass.
        var story2 = Directory.EnumerateFiles(impl, "*.md")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d+-\d+-"))
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).First();
        File.Delete(story2);
        gen.RegenerateEpics();
        var afterTopo = ReadSpaChunks(outDir);
        var topoDelta = ChunkDelta(afterContent, afterTopo);

        return new
        {
            note = "IR = shipped SpaDelivery manifest + content chunks. A 'delta' = the chunk files whose bytes changed.",
            chunkCount = before.Count,
            totalIrBytes = before.Sum(kv => (long)Encoding.UTF8.GetByteCount(kv.Value)),
            maxChunkBytesGuard = SpaDelivery.MaxChunkBytes,
            maxPagesPerChunkGuard = SpaDelivery.MaxPagesPerChunk,
            largestChunkBytes = before.Count == 0 ? 0 : before.Max(kv => (long)Encoding.UTF8.GetByteCount(kv.Value)),
            contentEdit = contentDelta,
            topologyDelete = topoDelta,
        };
    }

    private static Dictionary<string, string> ReadSpaChunks(string outDir)
    {
        var spa = Path.Combine(outDir, "spa");
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(spa)) return d;
        foreach (var f in Directory.EnumerateFiles(spa, "*.json")) d[Path.GetFileName(f)] = File.ReadAllText(f);
        return d;
    }

    private static object ChunkDelta(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        long changedBytes = 0; var changed = new List<string>();
        foreach (var (name, content) in after)
        {
            before.TryGetValue(name, out var old);
            if (old != content) { changed.Add(name); changedBytes += Encoding.UTF8.GetByteCount(content); }
        }
        var totalAfter = after.Sum(kv => (long)Encoding.UTF8.GetByteCount(kv.Value));
        return new
        {
            changedChunkFiles = changed.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            changedChunkCount = changed.Count,
            reshippedBytes = changedBytes,   // what a naive whole-changed-chunk delta must transmit
            totalIrBytes = totalAfter,
            reshippedSharePct = totalAfter > 0 ? Math.Round(100.0 * changedBytes / totalAfter, 1) : 0,
        };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // Normalization — the SAME per-run/per-build noise folding GoldenContentFingerprint uses [SiteGeneratorAdapterTests.cs]
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    private static readonly Regex FooterClock = new(@"on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}", RegexOptions.Compiled);
    private static readonly Regex AssetCacheBust = new(@"\?v=[0-9a-fA-F]+", RegexOptions.Compiled);
    private static readonly Regex SubtitleVersion = new(@"SpecScribe v[^<]+", RegexOptions.Compiled);
    private static readonly Regex VersionRow = new(@"(<dt>Version</dt><dd>)[^<]*(</dd>)", RegexOptions.Compiled);
    private static readonly Regex BuildRow = new(@"(<dt>Build</dt><dd>)[^<]*(</dd>)", RegexOptions.Compiled);

    private static Dictionary<string, string> SnapshotNormalized(string root, string[] pathsToFold)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(root)) return d;
        foreach (var p in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = FoldToday(PathUtil.NormalizeSlashes(Path.GetRelativePath(root, p)));
            d[rel] = NormalizeVolatile(File.ReadAllText(p), pathsToFold);
        }
        return d;
    }

    private static string FoldToday(string s)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return s.Replace(Charts.DReadable(today), "<date-readable>").Replace(Charts.D(today), "<date-iso>");
    }

    private static string NormalizeVolatile(string content, string[] pathsToFold)
    {
        content = content.Replace("\r\n", "\n");
        content = FoldToday(content);
        foreach (var p in pathsToFold)
            content = content.Replace(PathUtil.NormalizeSlashes(p), "<out>").Replace(p, "<out>");
        content = FooterClock.Replace(content, "on <ts>");
        content = AssetCacheBust.Replace(content, "?v=<ver>");
        content = SubtitleVersion.Replace(content, "SpecScribe v<ver>");
        content = VersionRow.Replace(content, "$1<ver>$2");
        content = BuildRow.Replace(content, "$1<build>$2");
        return content;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // plumbing
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    private static void CopyIngestedSources(string repoRoot, string dest)
    {
        if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
        Directory.CreateDirectory(dest);
        CopyDir(Path.Combine(repoRoot, ForgeOptions.SourceDirName), Path.Combine(dest, ForgeOptions.SourceDirName));
        var docs = Path.Combine(repoRoot, "docs");
        if (Directory.Exists(docs)) CopyDir(docs, Path.Combine(dest, "docs"));
        var readme = Path.Combine(repoRoot, "README.md");
        if (File.Exists(readme)) File.Copy(readme, Path.Combine(dest, "README.md"), overwrite: true);
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(src, dst));
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(src, dst), overwrite: true);
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ForgeOptions.SourceDirName)))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private static string? GetOption(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        if (i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) return args[i + 1];
        var prefix = name + "=";
        return args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }

    private record ChangePlan((string FullPath, bool ExistsAfter)[] Events, string RouteNote);

    private record CaseResult(
        string id, string description, string routeNote, bool correct,
        int oracleFiles, int staleCount, int orphanedCount, int missingCount,
        List<string> stalePages, List<string> orphanedPages, List<string> missingPages,
        double incrementalMs, double fullRegenMs);
}
