using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.DeltaTransportSpike;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Story 22.6 Task 1 — THE GATE.  Measure the PAGE-LEVEL delta before building anything.
//
// Story 22.1 measured IR-delta transport at CHUNK granularity and only ever through RegenerateEpics, and flagged
// its own blind spot: "the byte-perfect GenerateOne route was NEVER delta-measured." Story 22.2 then delivered
// page-level addressing (manifest `contentHash` + `bytes`), which is what makes a page-granular measurement
// possible at all. This harness discharges that gate.
//
// For each of the FOUR watch routes FileWatcherService.RunDebouncedPass can dispatch to on a content-only edit —
// GenerateOne, RegenerateEpics, RegenerateAdrs, RegenerateFromDataSource — it:
//
//   1. snapshots the IR manifest (path → contentHash + bytes) and the full webview payload,
//   2. applies ONE content-only source edit,
//   3. invokes the SHIPPED route (mirroring RunDebouncedPass's predicate order — never guessing),
//   4. re-snapshots, and diffs by contentHash to get changed / added / removed pages,
//   5. reports delta bytes against BOTH the full IR bytes and the full webview payload bytes.
//
// THE GATE: a single-file content edit via GenerateOne must produce a delta under 5 % of both totals.
//
// Everything here drives the REAL SiteGenerator / SpaDelivery / WebviewCommand. No .md is re-parsed, no .html is
// scraped (AD-1/AD-2). Throwaway — see the .csproj header.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
internal static class Program
{
    /// <summary>AC #1's gate: a GenerateOne single-file content edit must produce a delta under this share of
    /// BOTH the full IR and the full webview payload. Not a tuning knob — the story's binding threshold.</summary>
    private const double GatePct = 5.0;

    private static int Main(string[] args)
    {
        var repoRoot = Path.GetFullPath(GetOption(args, "--repo") ?? FindRepoRoot() ?? Directory.GetCurrentDirectory());
        var scratch = Path.GetFullPath(GetOption(args, "--out") ?? Path.Combine(Path.GetTempPath(), "ss-delta-spike"));

        Console.Error.WriteLine($"[22.6-gate] repo    = {repoRoot}");
        Console.Error.WriteLine($"[22.6-gate] scratch = {scratch}");
        if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
        Directory.CreateDirectory(scratch);

        // Pristine mutable sandbox of ONLY what the core ingests (_bmad-output + docs + README). No .git, so
        // deep-git is off and every run reads identical inputs — the delta measured is change-driven, never
        // git-driven.
        var template = Path.Combine(scratch, "template");
        CopyIngestedSources(repoRoot, template);
        Console.Error.WriteLine($"[22.6-gate] sandbox template built at {template}");

        var routes = new (string Id, string Desc, string Expected, Func<string, SiteGenerator, string> Select)[]
        {
            ("generate-one", "Content-only edit of a generic planning DOC → GenerateOne (THE GATED ROUTE)", "GenerateOne", SelectGenericDoc),
            ("regenerate-epics", "Content-only edit of an implementation-artifacts STORY → RegenerateEpics", "RegenerateEpics", SelectStory),
            ("regenerate-adrs", "Content-only edit of an ADR → RegenerateAdrs", "RegenerateAdrs", SelectAdr),
            ("regenerate-data-source", "Comment-only edit of sprint-status.yaml → RegenerateFromDataSource", "RegenerateFromDataSource", SelectDataSource),
        };

        var results = new List<RouteResult>();
        foreach (var (id, desc, expected, select) in routes)
        {
            try
            {
                results.Add(MeasureRoute(id, desc, expected, template, scratch, select));
                Console.Error.WriteLine($"[22.6-gate] {id}: done");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[22.6-gate] ROUTE {id} ERROR: {ex}");
                throw;
            }
        }

        var gated = results.Single(r => r.Route == "generate-one");
        // A route that did NOT actually run cannot pass a gate. The first run of this harness reported 0.000 %
        // for a GenerateOne that returned Skipped (an ignored dotfile) — a delta of nothing measured against
        // everything. Both liveness conditions are part of the gate, not preconditions to it.
        var ranForReal = gated.ObservedRoute == gated.ExpectedRoute
            && gated.Outcome != nameof(GenerationOutcome.Skipped)
            && gated.PagesChanged + gated.PagesAdded > 0;
        var passed = ranForReal
            && gated.DeltaSharePctOfIr < GatePct
            && gated.DeltaSharePctOfWebview < GatePct;

        var report = new
        {
            note = "Story 22.6 Task 1 gate. Page-level delta (manifest contentHash diff) per watch route, against "
                 + "the full IR bytes (spa/manifest.json + spa/pages-*.json) and the full webview NDJSON payload "
                 + "(WebviewCommand.SerializePayload over RenderWebviewSurfaces).",
            gate = new
            {
                thresholdPct = GatePct,
                route = "generate-one",
                ranForReal,
                observedRoute = gated.ObservedRoute,
                outcome = gated.Outcome,
                deltaSharePctOfIr = gated.DeltaSharePctOfIr,
                deltaSharePctOfWebview = gated.DeltaSharePctOfWebview,
                attributableSharePctOfIr = gated.AttributableSharePctOfIr,
                attributableSharePctOfWebview = gated.AttributableSharePctOfWebview,
                passed,
            },
            irSchemaVersion = SpaDelivery.SchemaVersion,
            routes = results,
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var reportPath = Path.Combine(scratch, "report.json");
        File.WriteAllText(reportPath, json);
        Console.Error.WriteLine($"\n[22.6-gate] wrote {reportPath}");
        Console.Error.WriteLine($"[22.6-gate] GATE {(passed ? "PASSED" : "FAILED")}");
        Console.WriteLine(json);
        return passed ? 0 : 1;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // Measurement
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The one-line content edit, in a form the target's own parser accepts. A markdown artifact takes a
    /// prose line; <c>sprint-status.yaml</c> takes a YAML COMMENT — appending prose to it would fail the parse and
    /// <c>RegenerateFromDataSource</c> would report <see cref="GenerationOutcome.Skipped"/>, measuring nothing
    /// (its own doc comment says a Skipped notice means the data source did not parse).</summary>
    private static string EditTextFor(string path) =>
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            ? "\n# Story 22.6 gate: comment-only edit.\n"
            : "\n\nStory 22.6 gate: one-line content edit.\n";

    private static RouteResult MeasureRoute(
        string id, string desc, string expectedRoute, string template, string scratch,
        Func<string, SiteGenerator, string> select)
    {
        var caseDir = Path.Combine(scratch, id);
        var sandbox = Path.Combine(caseDir, "src");
        CopyDir(template, sandbox);
        var srcRoot = Path.Combine(sandbox, ForgeOptions.SourceDirName);
        var adrRoot = Path.Combine(sandbox, "docs", "adrs");
        var outDir = Path.Combine(caseDir, "out");

        // ONE generator producing BOTH measured artifacts: --spa gives the IR, CapturePages gives the webview's
        // long-tail surfaces. That is exactly the pairing `specscribe webview --serve` runs under, so the two
        // totals below are commensurable rather than taken from two different runs.
        var opts = ForgeOptions.Resolve(source: srcRoot, adrs: adrRoot, output: outDir, emitSpa: true);
        var gen = new SiteGenerator(opts) { CapturePages = true };
        gen.GenerateAll();

        var target = select(sandbox, gen);
        var baseline = Snapshot(gen, opts, outDir);

        // ── THE CONTROL, and it is not optional. ──────────────────────────────────────────────────────────────
        // Run the SAME route against the SAME file with NO source edit first. Whatever moves here moved for
        // reasons unrelated to any change — per-regen churn — and it is charged to every real delta on this
        // route regardless of what the user edited. Without this control a route's headline number silently
        // conflates "what the edit cost" with "what a regen costs", which is precisely the inflation Story 22.1's
        // RegenerateEpics figure carried and warned about. Measured, reported separately, and subtracted.
        var noopEv = Dispatch(gen, target);
        var afterNoop = Snapshot(gen, opts, outDir, retainContent: true);
        var noopDelta = Diff(baseline, afterNoop);

        // The real edit diffs against the POST-no-op state, so churn is not double-counted into it.
        File.AppendAllText(target, EditTextFor(target));
        var ev = Dispatch(gen, target);
        var after = Snapshot(gen, opts, outDir, retainContent: true);
        var delta = Diff(afterNoop, after);

        // Explain each changed page rather than merely counting it — the dominant term in every route's delta
        // needs a cause, not a size.
        var diagnoses = delta.Changed.Concat(delta.Added)
            .OrderByDescending(p => after.Pages.TryGetValue(p, out var e) ? e.EncodedBytes : 0)
            .Select(p => Diagnose(
                p,
                after.Pages.TryGetValue(p, out var e) ? e.EncodedBytes : 0,
                afterNoop.Content.GetValueOrDefault(p),
                after.Content.GetValueOrDefault(p)))
            .ToList();

        // What the edit ACTUALLY cost: the pages the real edit moved that a no-op did not also move.
        var churnPaths = new HashSet<string>(noopDelta.Changed.Concat(noopDelta.Added), StringComparer.Ordinal);
        var attributable = delta.Changed.Concat(delta.Added).Where(p => !churnPaths.Contains(p)).ToList();
        var attributableBytes = attributable.Sum(p => after.Pages.TryGetValue(p, out var e) ? e.EncodedBytes : 0);

        return new RouteResult(
            Route: id,
            Description: desc,
            ExpectedRoute: expectedRoute,
            ObservedRoute: ev.Route,
            Outcome: ev.Outcome.ToString(),
            ChangedSource: PathUtil.NormalizeSlashes(Path.GetRelativePath(sandbox, target)),
            PagesTotal: after.Pages.Count,
            NoopPagesChanged: noopDelta.Changed.Count + noopDelta.Added.Count,
            NoopPaths: noopDelta.Changed.Concat(noopDelta.Added).OrderBy(p => p, StringComparer.Ordinal).Take(25).ToList(),
            NoopEncodedBytes: noopDelta.EncodedBytes,
            NoopOutcome: noopEv.Outcome.ToString(),
            PagesChanged: delta.Changed.Count,
            PagesAdded: delta.Added.Count,
            PagesRemoved: delta.Removed.Count,
            ChangedPaths: delta.Changed.Concat(delta.Added).OrderBy(p => p, StringComparer.Ordinal).Take(25).ToList(),
            AttributablePaths: attributable.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            DeltaContentBytes: delta.ContentBytes,
            DeltaEncodedBytes: delta.EncodedBytes,
            AttributableEncodedBytes: attributableBytes,
            FullIrBytes: after.IrBytes,
            FullWebviewPayloadBytes: after.WebviewPayloadBytes,
            DeltaSharePctOfIr: Pct(delta.EncodedBytes, after.IrBytes),
            DeltaSharePctOfWebview: Pct(delta.EncodedBytes, after.WebviewPayloadBytes),
            AttributableSharePctOfIr: Pct(attributableBytes, after.IrBytes),
            AttributableSharePctOfWebview: Pct(attributableBytes, after.WebviewPayloadBytes),
            ChunksTouched: delta.Chunks,
            Diagnoses: diagnoses);
    }

    /// <summary>Mirrors <c>FileWatcherService.RunDebouncedPass</c>'s predicate order EXACTLY (Story 22.5 inserted
    /// the ClassifyRebuildScope escalation between IsDataSource and IsAdr). Copied rather than guessed, per the
    /// story's Task 1 instruction — if this drifts from the shipped dispatcher the numbers describe a route the
    /// watcher never takes.</summary>
    private static (string Route, GenerationOutcome Outcome) Dispatch(SiteGenerator gen, string fullPath)
    {
        if (gen.IsDataSource(fullPath)) return ("RegenerateFromDataSource", gen.RegenerateFromDataSource(fullPath).Outcome);
        if (gen.ClassifyRebuildScope(fullPath) == RebuildScope.Full) return ("RegenerateTopology", gen.RegenerateTopology().Outcome);
        if (gen.IsAdr(fullPath)) return ("RegenerateAdrs", gen.RegenerateAdrs().Outcome);
        if (gen.IsEpicsRelated(fullPath)) return ("RegenerateEpics", gen.RegenerateEpics().Outcome);
        if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return ("Skipped", GenerationOutcome.Skipped);
        return File.Exists(fullPath)
            ? ("GenerateOne", gen.GenerateOne(fullPath).Outcome)
            : ("RemoveFor", gen.RemoveFor(fullPath).Outcome);
    }

    /// <summary>The two totals a delta is measured against, plus the page index it is diffed by. The IR total is
    /// the bytes actually on disk under <c>spa/</c>; the webview total is the exact NDJSON line
    /// <c>RunServeLoop</c> writes today — which is the thing 22.6 exists to shrink.</summary>
    /// <param name="retainContent">Keep each page's decoded content region so a later diff can say WHERE it
    /// diverged. Off by default and deliberately so: this repo's IR is ~68 MB and holding three snapshots' worth
    /// of decoded content would cost more than the measurement is worth. Only the two snapshots that bracket the
    /// real edit retain.</param>
    private static Snap Snapshot(SiteGenerator gen, ForgeOptions opts, string outDir, bool retainContent = false)
    {
        var spaDir = Path.Combine(outDir, SpaDelivery.ChunkDir);
        long irBytes = 0;
        if (Directory.Exists(spaDir))
        {
            foreach (var f in Directory.EnumerateFiles(spaDir, "*.json"))
            {
                irBytes += new FileInfo(f).Length;
            }
        }

        var manifestPath = Path.Combine(outDir, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        var pages = new Dictionary<string, PageEntry>(StringComparer.Ordinal);
        var content = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(manifestPath))
        {
            var chunks = new ChunkReader(outDir);
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            foreach (var p in doc.RootElement.GetProperty("pages").EnumerateObject())
            {
                var chunk = p.Value.GetProperty("chunk").GetString()!;
                if (retainContent) content[p.Name] = chunks.Page(chunk, p.Name) ?? string.Empty;
                pages[p.Name] = new PageEntry(
                    p.Value.GetProperty("contentHash").GetString()!,
                    p.Value.GetProperty("bytes").GetInt32(),
                    chunk,
                    // The EXACT wire cost of carrying this page in a delta: the same `key : value ,` member
                    // tokens BuildDataFiles budgets and writes, read back out of the emitted chunk. Measured,
                    // not estimated — the manifest's own `bytes` is the RAW content size, and HTML-safe escaping
                    // (< > & each → a 6-byte \uXXXX) makes the wire form substantially larger. Dividing a raw
                    // numerator by the on-disk encoded denominator would flatter the gate; this keeps both sides
                    // in the same units.
                    EncodedMemberBytes(chunks, chunk, p.Name));
            }
        }

        var payload = WebviewCommand.SerializePayload(
            gen.RenderWebviewSurfaces(),
            WebviewCommand.ResolveConfiguredOutputRoot(opts),
            WebviewCommand.ResolveSourceRoot(opts),
            WebviewCommand.ResolveAdrRoot(opts),
            WebviewCommand.ResolveRepoRootOffset(opts));

        return new Snap(pages, content, irBytes, Encoding.UTF8.GetByteCount(payload));
    }

    private static DeltaResult Diff(Snap before, Snap after)
    {
        var changed = new List<string>();
        var added = new List<string>();
        long contentBytes = 0;
        long encodedBytes = 0;
        var chunks = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (path, entry) in after.Pages.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!before.Pages.TryGetValue(path, out var old))
            {
                added.Add(path);
            }
            else if (old.ContentHash == entry.ContentHash)
            {
                continue;
            }
            else
            {
                changed.Add(path);
            }
            contentBytes += entry.Bytes;
            encodedBytes += entry.EncodedBytes;
            // The manifest's `chunk` is ALREADY output-relative and already carries the `spa/` prefix — do not
            // re-prepend ChunkDir (the first run of this harness printed `spa/spa/pages-epics-2.json`).
            chunks.Add(entry.Chunk);
        }

        var removed = before.Pages.Keys.Where(k => !after.Pages.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        return new DeltaResult(changed, added, removed, contentBytes, encodedBytes, chunks.ToList());
    }

    /// <summary>One page's exact cost as a member of its emitted chunk — <c>key</c> + <c>:</c> + <c>value</c> +
    /// <c>,</c>, the identical accounting <see cref="SpaDelivery.BuildDataFiles"/> budgets against
    /// <see cref="SpaDelivery.MaxChunkBytes"/>. Re-serializing the parsed value reproduces the chunk's own tokens
    /// byte-for-byte: the emitter serializes each page's <c>ContentHtml</c> with default (HTML-safe) escaping and
    /// no naming policy applies to a bare string, so this is a measurement of the shipped bytes, not a model of
    /// them.</summary>
    private static long EncodedMemberBytes(ChunkReader chunks, string chunk, string pagePath)
    {
        var value = chunks.Page(chunk, pagePath);
        if (value is null) return 0;
        var keyJson = JsonSerializer.Serialize(pagePath);
        var valueJson = JsonSerializer.Serialize(value);
        return Encoding.UTF8.GetByteCount(keyJson) + 1 + Encoding.UTF8.GetByteCount(valueJson) + 1;
    }

    /// <summary>Chunk parse cache scoped to ONE snapshot — a manifest names the same chunk for dozens of pages and
    /// the epics chunk is multi-MB, so re-parsing per page would dominate the run. Deliberately per-snapshot and
    /// never shared: reusing one across the before/after pair would make the "after" delta describe the "before"
    /// bytes, which is precisely the class of silent error this harness exists to rule out.</summary>
    private sealed class ChunkReader(string outDir)
    {
        private readonly Dictionary<string, JsonDocument> _docs = new(StringComparer.Ordinal);

        public string? Page(string chunk, string pagePath)
        {
            if (!_docs.TryGetValue(chunk, out var doc))
            {
                var full = Path.Combine(outDir, chunk.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) return null;
                doc = JsonDocument.Parse(File.ReadAllText(full));
                _docs[chunk] = doc;
            }
            return doc.RootElement.TryGetProperty(pagePath, out var v) ? v.GetString() : null;
        }
    }

    private static double Pct(long part, long whole) => whole > 0 ? Math.Round(100.0 * part / whole, 3) : 0;

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // Content-only edits — one per route. Each appends a line to an EXISTING file: no add, no delete, no rename,
    // so ClassifyRebuildScope stays Incremental and the route under test is the one that actually fires.
    // ════════════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The GATED file: a generic planning doc that reaches <c>GenerateOne</c>. Selected by asking the
    /// SHIPPED predicates (<c>IsDataSource</c>/<c>IsAdr</c>/<c>IsEpicsRelated</c>/<c>ClassifyRebuildScope</c>)
    /// which files the dispatcher would actually route this way, rather than pattern-matching filenames — a
    /// filename guess picked the wrong file on the first run of this harness, and a wrong file here silently
    /// measures a different route.</summary>
    private static string SelectGenericDoc(string sandbox, SiteGenerator gen) =>
        Directory.EnumerateFiles(Path.Combine(sandbox, ForgeOptions.SourceDirName), "*.md", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .First(f => !gen.IsDataSource(f) && !gen.IsAdr(f) && !gen.IsEpicsRelated(f)
                     // An IGNORED file (dotfile, ~$, .tmp, .crswap) still classifies Narrow and still reaches
                     // GenerateOne — which then returns Skipped and renders nothing. The FIRST run of this
                     // harness selected `.memlog.md` on exactly that path and reported a 0.000 % delta as a
                     // PASS. A gate that passes because nothing happened is worse than one that fails.
                     && !PathUtil.IsIgnoredSourceFile(f)
                     && gen.ClassifyRebuildScope(f) == RebuildScope.Narrow);

    private static string SelectStory(string sandbox, SiteGenerator gen) =>
        Directory.EnumerateFiles(Path.Combine(sandbox, ForgeOptions.SourceDirName, "implementation-artifacts"), "*.md")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d+-\d+-") && gen.IsEpicsRelated(f)
                     && !PathUtil.IsIgnoredSourceFile(f))
            .OrderBy(f => f, StringComparer.Ordinal).First();

    private static string SelectAdr(string sandbox, SiteGenerator gen) =>
        Directory.EnumerateFiles(Path.Combine(sandbox, "docs", "adrs"), "0*.md")
            .Where(f => gen.IsAdr(f) && !PathUtil.IsIgnoredSourceFile(f))
            .OrderBy(f => f, StringComparer.Ordinal).First();

    private static string SelectDataSource(string sandbox, SiteGenerator gen) =>
        Directory.EnumerateFiles(
            Path.Combine(sandbox, ForgeOptions.SourceDirName), "sprint-status.yaml", SearchOption.AllDirectories)
            .Where(gen.IsDataSource).OrderBy(f => f, StringComparer.Ordinal).First();

    // ════════════════════════════════════════════════════════════════════════════════════════════════════════
    // plumbing (mirrors spike/ir-incremental)
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

    private sealed record PageEntry(string ContentHash, int Bytes, string Chunk, long EncodedBytes);

    private sealed record Snap(
        Dictionary<string, PageEntry> Pages,
        Dictionary<string, string> Content,
        long IrBytes,
        long WebviewPayloadBytes);

    private sealed record DeltaResult(
        List<string> Changed, List<string> Added, List<string> Removed,
        long ContentBytes, long EncodedBytes, List<string> Chunks);

    /// <summary>Why a page landed in the delta, in a form a reader can act on: its size, and a window around the
    /// FIRST byte at which its content region actually diverged. A page-level delta says "code-map.html changed";
    /// this says what changed in it — the difference between a number and a finding. Without it the gate's
    /// dominant term (a ~2 MB page present in EVERY route's delta) is an unexplained mass.</summary>
    private sealed record PageDiagnosis(string Path, long EncodedBytes, int FirstDiffOffset, string Before, string After);

    private static PageDiagnosis Diagnose(string path, long encodedBytes, string? before, string? after)
    {
        before ??= string.Empty;
        after ??= string.Empty;
        var i = 0;
        while (i < before.Length && i < after.Length && before[i] == after[i]) i++;
        const int Window = 160;
        string Slice(string s) => i >= s.Length ? "<end of content>" : s.Substring(i, Math.Min(Window, s.Length - i));
        return new PageDiagnosis(path, encodedBytes, i, Slice(before), Slice(after));
    }

    private sealed record RouteResult(
        string Route,
        string Description,
        string ExpectedRoute,
        string ObservedRoute,
        string Outcome,
        string ChangedSource,
        int PagesTotal,
        // The NO-OP CONTROL: the same route, same file, no edit. Per-regen churn, charged to every delta.
        int NoopPagesChanged,
        List<string> NoopPaths,
        long NoopEncodedBytes,
        string NoopOutcome,
        // The REAL edit, measured against the post-no-op state.
        int PagesChanged,
        int PagesAdded,
        int PagesRemoved,
        List<string> ChangedPaths,
        // The real edit MINUS the churn: what the edit itself actually cost.
        List<string> AttributablePaths,
        long DeltaContentBytes,
        long DeltaEncodedBytes,
        long AttributableEncodedBytes,
        long FullIrBytes,
        long FullWebviewPayloadBytes,
        double DeltaSharePctOfIr,
        double DeltaSharePctOfWebview,
        double AttributableSharePctOfIr,
        double AttributableSharePctOfWebview,
        List<string> ChunksTouched,
        List<PageDiagnosis> Diagnoses);
}
