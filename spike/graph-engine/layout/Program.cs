// Story 24.6 graph-engine spike — throwaway fixture builder + deterministic generation-time force layout.
//
// WHAT THIS IS FOR
//   AC #3 asks whether node position is DATA (computed in C# at generation time, embedded as coordinates) or
//   PRESENTATION (solved client-side). This program is the evidence for the "data" reading: it computes a seeded
//   Fruchterman-Reingold layout over the real Story 24.1 coupling metric and emits node coordinates as a JSON
//   island shaped like the shipped `sunburst-explorer-data` one, so the client's remaining job is drawing points
//   and lines — which R2 proves Plotly already does for zero marginal bytes.
//
// WHAT IT IS NOT
//   Not production code. Nothing here is referenced by src/SpecScribe. The metric is READ from the shipped
//   Story 24.1 API (GitMetrics.ParseNumstatLog / BuildFileInsights / IsCrossBoundary / CouplingMinSupport) and
//   never re-derived, so the fixture is the surface Epic 24 will actually have rather than an approximation of it.
//
// DETERMINISM DISCIPLINE (AC #3 is tested by repetition, not assertion)
//   Every source of run-to-run variation is closed deliberately:
//     * no System.Random — a private xorshift128+ with a compile-time seed, so the "random" initial placement is
//       a pure function of the node's ordinal index;
//     * no dictionary/HashSet iteration ever reaches a float — every collection is materialised through an
//       explicit ordinal sort before it is walked, because .NET's dictionary order is an implementation detail
//       and floating-point addition is not associative, so an order change moves the last bits of every coordinate;
//     * no wall-clock, no environment, no parallelism;
//     * all formatting through InvariantCulture with a fixed format string.
//   `--runs N` re-runs the whole pipeline N times in one process and reports whether the emitted bytes are
//   identical; `verify-determinism.mjs` does the same ACROSS processes, which is the stronger check.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Spike.GraphEngine;

internal static class Program
{
    /// <summary>Production's own deep-git fetch, verbatim from <c>GitMetrics.TryComputeDeep</c>. Copied rather
    /// than called because <c>TryComputeDeep</c> wraps it in a 3-second budget that silently yields null on a
    /// cold run (a known, recorded hazard); the probe must never measure a truncated window and call it scale.</summary>
    private const string ProductionLogArgs =
        "log --numstat --date=format:%Y-%m-%dT%H:%M --pretty=format:%x01%H%x1f%an%x1f%ad%x1f%s%x1f%b%x1f -n 300";

    private static int Main(string[] args)
    {
        var repoRoot = ArgValue(args, "--repo") ?? Directory.GetCurrentDirectory();
        var outDir = ArgValue(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, "fixtures");
        var runs = int.TryParse(ArgValue(args, "--runs"), out var r) ? Math.Max(1, r) : 1;
        var hubOverride = ArgValue(args, "--hub");
        var window = ArgValue(args, "--window") ?? "300";

        Directory.CreateDirectory(outDir);

        var logArgs = window == "300"
            ? ProductionLogArgs
            : ProductionLogArgs.Replace("-n 300", window == "all" ? "" : $"-n {window}");

        Console.WriteLine($"repo:   {repoRoot}");
        Console.WriteLine($"out:    {outDir}");
        Console.WriteLine($"window: {window} commits");

        var sw = Stopwatch.StartNew();
        var logText = RunGit(repoRoot, logArgs);
        if (logText is null)
        {
            Console.Error.WriteLine("FATAL: git log returned nothing. Not a repo, or git is unavailable.");
            return 1;
        }
        Console.WriteLine($"git log: {logText.Length:N0} B in {sw.ElapsedMilliseconds} ms");

        // The REAL Story 24.1 metric, from the shipped API. Two calls over the same text because the two
        // surfaces production itself uses are computed by two different shipped entry points:
        //   ParseNumstatLog   -> DirectedCoupling (the hub's top-N ranked directed view)
        //   BuildFileInsights -> per-file CoupledFile lists + the UNCAPPED CoChangePairs map
        // The uncapped map is what a whole-repo graph needs; DirectedCoupling is top-10 by construction.
        var pulse = GitMetrics.ParseNumstatLog(logText);
        var commits = GitMetrics.ParseNumstatRecords(logText);
        var insights = GitMetrics.BuildFileInsights(commits, out var coChangePairs);

        Console.WriteLine($"commits parsed:     {commits.Count}");
        Console.WriteLine($"analyzed commits:   {pulse.AnalyzedCommits}");
        Console.WriteLine($"files with insight: {insights.Count}");
        Console.WriteLine($"co-change pairs:    {coChangePairs.Count} (uncapped)");
        Console.WriteLine($"DirectedCoupling:   {pulse.DirectedCoupling.Count} (top-N, shipped hub view)");

        var changeCounts = insights.ToDictionary(kv => kv.Key, kv => kv.Value.ChangeCount, StringComparer.Ordinal);
        var analyzed = pulse.AnalyzedCommits;

        var report = new ScaleReport();
        string? lastWholeJson = null, lastEgoJson = null;

        for (var run = 1; run <= runs; run++)
        {
            foreach (var floor in new[] { 2, 3, 5, 8, 12 })
            {
                var whole = BuildWholeRepo(coChangePairs, changeCounts, analyzed, floor);
                var solveMs = TimeSolve(whole);
                var json = Emit.ToIsland(whole, $"whole-repo, support>={floor}");
                var path = Path.Combine(outDir, $"whole-repo-support-{floor}.json");
                File.WriteAllText(path, json, new UTF8Encoding(false));
                if (floor == GitMetrics.CouplingMinSupport)
                {
                    if (run > 1 && lastWholeJson is not null && lastWholeJson != json)
                        report.DriftedInProcess.Add($"whole-repo-support-{floor} @ run {run}");
                    lastWholeJson = json;
                }
                if (run == 1)
                {
                    report.Rows.Add(new ScaleRow("whole-repo", floor, whole.Nodes.Count, whole.Edges.Count,
                        whole.CrossBoundaryEdges, whole.ProcessEdges, json.Length, whole.MaxDegree, whole.Components,
                        whole.HubLabel, solveMs));
                }
            }

            // The Code-only whole-repo variant. The unfiltered graph's most prominent nodes turn out to be the
            // project's own BOOKKEEPING files, so "what does this default to" is a real Story 24.3 question and
            // the Story 10.6 Code/Process lens is the shipped answer. Measured, not assumed.
            {
                var codeOnly = BuildWholeRepo(coChangePairs, changeCounts, analyzed,
                    GitMetrics.CouplingMinSupport, codeOnly: true);
                var ms = TimeSolve(codeOnly);
                var json = Emit.ToIsland(codeOnly, $"whole-repo CODE-ONLY, support>={GitMetrics.CouplingMinSupport}");
                File.WriteAllText(Path.Combine(outDir, "whole-repo-code-only.json"), json, new UTF8Encoding(false));
                if (run == 1)
                {
                    report.Rows.Add(new ScaleRow("whole-code-only", GitMetrics.CouplingMinSupport,
                        codeOnly.Nodes.Count, codeOnly.Edges.Count, codeOnly.CrossBoundaryEdges,
                        codeOnly.ProcessEdges, json.Length, codeOnly.MaxDegree, codeOnly.Components,
                        codeOnly.HubLabel, ms));
                }
            }

            // The ego fixture: one hub file plus its coupled neighbours, the Story 24.2 shape.
            var hub = hubOverride ?? PickHub(coChangePairs, changeCounts);
            foreach (var hops in new[] { 1, 2 })
            {
                var ego = BuildEgo(hub, coChangePairs, changeCounts, analyzed, GitMetrics.CouplingMinSupport, hops);
                var ms = TimeSolve(ego);
                var json = Emit.ToIsland(ego, $"ego {hub}, {hops} hop(s), support>={GitMetrics.CouplingMinSupport}");
                var path = Path.Combine(outDir, $"ego-{hops}hop.json");
                File.WriteAllText(path, json, new UTF8Encoding(false));
                if (hops == 1)
                {
                    if (run > 1 && lastEgoJson is not null && lastEgoJson != json)
                        report.DriftedInProcess.Add($"ego-1hop @ run {run}");
                    lastEgoJson = json;
                }
                if (run == 1)
                {
                    report.Rows.Add(new ScaleRow($"ego({hops}hop)", GitMetrics.CouplingMinSupport,
                        ego.Nodes.Count, ego.Edges.Count, ego.CrossBoundaryEdges, ego.ProcessEdges, json.Length,
                        ego.MaxDegree, ego.Components, ego.HubLabel, ms));
                }
            }

            // The CAPPED ego fixture — the shape Story 24.2 would actually render. The uncapped 1-hop
            // neighbourhood of the natural hub is 360 nodes, so an uncapped ego graph is not a small graph;
            // production already answers this with FileInsightCoupledCap = 8 on the per-file list.
            foreach (var cap in new[] { 8, 20, 40 })
            {
                var ego = BuildCappedEgo(hub, coChangePairs, changeCounts, analyzed,
                    GitMetrics.CouplingMinSupport, cap);
                var ms = TimeSolve(ego);
                var json = Emit.ToIsland(ego, $"ego {hub}, top-{cap} by confidence");
                File.WriteAllText(Path.Combine(outDir, $"ego-top{cap}.json"), json, new UTF8Encoding(false));
                if (run == 1)
                {
                    report.Rows.Add(new ScaleRow($"ego(top-{cap})", GitMetrics.CouplingMinSupport,
                        ego.Nodes.Count, ego.Edges.Count, ego.CrossBoundaryEdges, ego.ProcessEdges, json.Length,
                        ego.MaxDegree, ego.Components, ego.HubLabel, ms));
                }
            }
        }

        // The filter-interaction probe (R5's named weak point): how many distinct node sets does a continuous
        // confidence slider actually produce? If the answer is small, precompute-per-state is viable; if it is
        // large, it is not, and that is the finding.
        report.FilterStates = FilterProbe.Run(coChangePairs, changeCounts, analyzed);

        var summary = report.ToJson(runs, analyzed, commits.Count, coChangePairs.Count, insights.Count);
        File.WriteAllText(Path.Combine(outDir, "scale.json"), summary, new UTF8Encoding(false));

        Console.WriteLine();
        Console.WriteLine("fixture  floor  nodes  edges  xb  proc  bytes  maxdeg  comps");
        foreach (var row in report.Rows)
        {
            Console.WriteLine($"{row.Fixture,-14} {row.Floor,3}  {row.Nodes,5}  {row.Edges,5}  " +
                              $"{row.CrossBoundary,3} {row.Process,4}  {row.Bytes,7:N0}  {row.MaxDegree,5}  {row.Components,4}");
        }
        Console.WriteLine();
        Console.WriteLine(runs > 1
            ? report.DriftedInProcess.Count == 0
                ? $"IN-PROCESS DETERMINISM: {runs} runs, byte-identical."
                : $"IN-PROCESS DRIFT: {string.Join(", ", report.DriftedInProcess)}"
            : "(single run; use --runs 3 or verify-determinism.mjs for the determinism check)");

        return report.DriftedInProcess.Count == 0 ? 0 : 2;
    }

    /// <summary>Picks the most connected file as the ego hub. A quiet file proves nothing about legibility,
    /// so the choice is "highest coupled-neighbour count, tie-broken by change count then ordinal path" —
    /// deterministic, and it lands on a genuinely hub-like file rather than an arbitrary one.</summary>
    private static string PickHub(
        IReadOnlyDictionary<(string FileA, string FileB), int> pairs,
        IReadOnlyDictionary<string, int> changeCounts)
    {
        var degree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in pairs.Keys.OrderBy(k => k.FileA, StringComparer.Ordinal)
                                      .ThenBy(k => k.FileB, StringComparer.Ordinal))
        {
            if (pairs[key] < GitMetrics.CouplingMinSupport) continue;
            degree[key.FileA] = degree.GetValueOrDefault(key.FileA) + 1;
            degree[key.FileB] = degree.GetValueOrDefault(key.FileB) + 1;
        }
        return degree.OrderByDescending(kv => kv.Value)
                     .ThenByDescending(kv => changeCounts.GetValueOrDefault(kv.Key))
                     .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                     .Select(kv => kv.Key)
                     .FirstOrDefault() ?? "(none)";
    }

    /// <summary>Solves and returns wall-clock milliseconds. AC #3 asks for at-scale performance, and the whole
    /// premise of the generation-time reading is that this cost is paid once by the generator, never by a reader.</summary>
    private static double TimeSolve(Graph g)
    {
        var sw = Stopwatch.StartNew();
        Layout.Solve(g);
        return sw.Elapsed.TotalMilliseconds;
    }

    private static Graph BuildWholeRepo(
        IReadOnlyDictionary<(string FileA, string FileB), int> pairs,
        IReadOnlyDictionary<string, int> changeCounts,
        int analyzed,
        int minSupport,
        bool codeOnly = false)
    {
        // ORDINAL SORT BEFORE ANY FLOAT TOUCHES THIS. See the determinism note at the top of the file.
        var kept = pairs.Where(kv => kv.Value >= minSupport)
                        .Where(kv => !codeOnly || !Graph.IsProcessPair(kv.Key.FileA, kv.Key.FileB))
                        .Select(kv => (kv.Key.FileA, kv.Key.FileB, Support: kv.Value))
                        .OrderBy(p => p.FileA, StringComparer.Ordinal)
                        .ThenBy(p => p.FileB, StringComparer.Ordinal)
                        .ToList();

        var paths = kept.SelectMany(p => new[] { p.FileA, p.FileB })
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToList();

        return Graph.From(paths, kept, changeCounts, analyzed,
            hubLabel: codeOnly ? "(whole repo, code-only)" : "(whole repo)");
    }

    /// <summary>The realistic Story 24.2 shape: hub + its top-<paramref name="cap"/> neighbours ranked by
    /// confidence FROM the hub (Story 24.1's Q4 ordering: confidence desc, support desc, ordinal path), plus every
    /// edge among the survivors so the neighbourhood's internal structure still shows.</summary>
    private static Graph BuildCappedEgo(
        string hub,
        IReadOnlyDictionary<(string FileA, string FileB), int> pairs,
        IReadOnlyDictionary<string, int> changeCounts,
        int analyzed,
        int minSupport,
        int cap)
    {
        var all = pairs.Where(kv => kv.Value >= minSupport)
                       .Select(kv => (kv.Key.FileA, kv.Key.FileB, Support: kv.Value))
                       .OrderBy(p => p.FileA, StringComparer.Ordinal)
                       .ThenBy(p => p.FileB, StringComparer.Ordinal)
                       .ToList();

        var hubChanges = changeCounts.GetValueOrDefault(hub, 0);
        var neighbours = all
            .Where(p => p.FileA == hub || p.FileB == hub)
            .Select(p => (Path: p.FileA == hub ? p.FileB : p.FileA, p.Support))
            .Select(p => (p.Path, p.Support,
                Confidence: hubChanges > 0 ? (double)p.Support / hubChanges : 0d))
            .OrderByDescending(p => p.Confidence)
            .ThenByDescending(p => p.Support)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .Take(cap)
            .Select(p => p.Path)
            .ToList();

        var keptSet = new HashSet<string>(neighbours, StringComparer.Ordinal) { hub };
        var kept = all.Where(p => keptSet.Contains(p.FileA) && keptSet.Contains(p.FileB)).ToList();
        var paths = keptSet.OrderBy(p => p, StringComparer.Ordinal).ToList();
        return Graph.From(paths, kept, changeCounts, analyzed, hubLabel: hub);
    }

    private static Graph BuildEgo(
        string hub,
        IReadOnlyDictionary<(string FileA, string FileB), int> pairs,
        IReadOnlyDictionary<string, int> changeCounts,
        int analyzed,
        int minSupport,
        int hops)
    {
        var all = pairs.Where(kv => kv.Value >= minSupport)
                       .Select(kv => (kv.Key.FileA, kv.Key.FileB, Support: kv.Value))
                       .OrderBy(p => p.FileA, StringComparer.Ordinal)
                       .ThenBy(p => p.FileB, StringComparer.Ordinal)
                       .ToList();

        var frontier = new HashSet<string>(StringComparer.Ordinal) { hub };
        var reached = new HashSet<string>(StringComparer.Ordinal) { hub };
        for (var h = 0; h < hops; h++)
        {
            var next = new List<string>();
            foreach (var (a, b, _) in all)
            {
                if (frontier.Contains(a) && reached.Add(b)) next.Add(b);
                if (frontier.Contains(b) && reached.Add(a)) next.Add(a);
            }
            frontier = new HashSet<string>(next, StringComparer.Ordinal);
        }

        var kept = all.Where(p => reached.Contains(p.FileA) && reached.Contains(p.FileB)).ToList();
        var paths = reached.OrderBy(p => p, StringComparer.Ordinal).ToList();
        return Graph.From(paths, kept, changeCounts, analyzed, hubLabel: hub);
    }

    private static string? RunGit(string repoRoot, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    internal static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}

/// <summary>A node in the coupling graph. <c>Weight</c> is the file's change count (Story 24.2 sizes nodes by
/// change frequency); <c>Boundary</c> is the first path segment <see cref="GitMetrics.IsCrossBoundary"/> compares.</summary>
internal sealed class Node
{
    public required string Id;
    public required string Path;
    public required string Label;
    public required int Weight;
    public required string Boundary;
    public double X, Y;
    public int Degree;
}

/// <summary>An undirected drawn edge carrying BOTH directions' confidence, because coupling confidence is
/// asymmetric (Story 24.1 AC #1) and a single drawn line between two nodes has to report both or lie about one.
/// Story 24.1's own owner decision Q1 took exactly this shape: the ranked TABLE is directed, the drawn GRAPH is
/// shared-commit weighted. This fixture keeps both so the spike can test whether arrowheads are needed at all.</summary>
internal sealed class Edge
{
    public required int A;
    public required int B;
    public required int Support;
    public required double ConfAB;
    public required double ConfBA;
    public required double? LiftAB;
    public required bool CrossBoundary;
    public required GitMetrics.CouplingKind Kind;
}

internal sealed class Graph
{
    public List<Node> Nodes = [];
    public List<Edge> Edges = [];
    public string HubLabel = "";
    public int CrossBoundaryEdges;
    public int ProcessEdges;
    public int MaxDegree;
    public int Components;

    public static Graph From(
        List<string> paths,
        List<(string FileA, string FileB, int Support)> pairs,
        IReadOnlyDictionary<string, int> changeCounts,
        int analyzed,
        string hubLabel)
    {
        var g = new Graph { HubLabel = hubLabel };
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in paths)
        {
            index[p] = g.Nodes.Count;
            g.Nodes.Add(new Node
            {
                Id = $"f{g.Nodes.Count}",
                Path = p,
                Label = p.Contains('/') ? p[(p.LastIndexOf('/') + 1)..] : p,
                Weight = changeCounts.GetValueOrDefault(p, 1),
                Boundary = p.Contains('/') ? p[..p.IndexOf('/')] : "(root)",
            });
        }

        foreach (var (a, b, support) in pairs)
        {
            var ca = changeCounts.GetValueOrDefault(a, 0);
            var cb = changeCounts.GetValueOrDefault(b, 0);
            // Story 24.1's formulas, read from its doc comments rather than reinvented:
            //   confidence(A->B) = support / ChangeCount[A]
            //   lift(A->B)       = confidence / (ChangeCount[B] / analyzedCommits); null when the denominator is 0
            var confAB = ca > 0 ? (double)support / ca : 0d;
            var confBA = cb > 0 ? (double)support / cb : 0d;
            double? liftAB = (cb > 0 && analyzed > 0) ? confAB / ((double)cb / analyzed) : null;

            var e = new Edge
            {
                A = index[a],
                B = index[b],
                Support = support,
                ConfAB = confAB,
                ConfBA = confBA,
                LiftAB = liftAB,
                CrossBoundary = GitMetrics.IsCrossBoundary(a, b),
                // ClassifyCoupling is private; the shipped Process test is pattern-based on either path, and the
                // probe only needs the DRAWN distinction (dashed vs solid), so it reproduces the OR semantics.
                Kind = IsProcessish(a) || IsProcessish(b)
                    ? GitMetrics.CouplingKind.Process
                    : GitMetrics.CouplingKind.Code,
            };
            g.Edges.Add(e);
            if (e.CrossBoundary) g.CrossBoundaryEdges++;
            if (e.Kind == GitMetrics.CouplingKind.Process) g.ProcessEdges++;
            g.Nodes[e.A].Degree++;
            g.Nodes[e.B].Degree++;
        }

        g.MaxDegree = g.Nodes.Count == 0 ? 0 : g.Nodes.Max(n => n.Degree);
        g.Components = CountComponents(g);
        return g;
    }

    public static bool IsProcessPair(string a, string b) => IsProcessish(a) || IsProcessish(b);

    /// <summary>Approximates the shipped <c>ClassifyCoupling</c>'s Process test on path shape alone. The real one
    /// is private to GitMetrics; the probe needs only the drawn dash/solid distinction, and a mismatch here would
    /// change a stroke, not a measurement. Flagged in the report rather than smuggled.</summary>
    private static bool IsProcessish(string path)
    {
        var lower = path.ToLowerInvariant();
        string[] dirs = ["_bmad", "_bmad-output", "docs", ".github", ".claude", "spike", "tools"];
        string[] exts = [".yaml", ".yml", ".json", ".css", ".md", ".toml", ".lock", ".props", ".csproj", ".slnx"];
        if (dirs.Any(d => lower.StartsWith(d + "/", StringComparison.Ordinal))) return true;
        return exts.Any(e => lower.EndsWith(e, StringComparison.Ordinal));
    }

    private static int CountComponents(Graph g)
    {
        var adj = new List<int>[g.Nodes.Count];
        for (var i = 0; i < adj.Length; i++) adj[i] = [];
        foreach (var e in g.Edges) { adj[e.A].Add(e.B); adj[e.B].Add(e.A); }
        var seen = new bool[g.Nodes.Count];
        var components = 0;
        for (var i = 0; i < g.Nodes.Count; i++)
        {
            if (seen[i]) continue;
            components++;
            var stack = new Stack<int>();
            stack.Push(i);
            seen[i] = true;
            while (stack.Count > 0)
            {
                foreach (var n in adj[stack.Pop()])
                {
                    if (seen[n]) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }
        }
        return components;
    }
}

/// <summary>Seeded Fruchterman-Reingold. Deterministic by construction: the initial placement is a pure function
/// of node index, every loop walks an array in index order, and there is no PRNG call after initialisation.</summary>
internal static class Layout
{
    private const int Iterations = 400;
    private const double Width = 1.0;
    private const double Height = 1.0;
    private const ulong Seed = 0x5DEECE66D;

    public static void Solve(Graph g)
    {
        var n = g.Nodes.Count;
        if (n == 0) return;
        if (n == 1) { g.Nodes[0].X = 0.5; g.Nodes[0].Y = 0.5; return; }

        var rng = new XorShift(Seed);
        for (var i = 0; i < n; i++)
        {
            // Seeded jitter around a deterministic ring: a pure ring is a pathological FR start (all repulsion
            // is radial and the graph never unfolds), while pure noise wastes iterations. Ring + seeded jitter
            // converges fast AND reproducibly.
            var theta = 2 * Math.PI * i / n;
            g.Nodes[i].X = 0.5 + 0.35 * Math.Cos(theta) + (rng.NextDouble() - 0.5) * 0.02;
            g.Nodes[i].Y = 0.5 + 0.35 * Math.Sin(theta) + (rng.NextDouble() - 0.5) * 0.02;
        }

        var k = Math.Sqrt(Width * Height / n);
        var dispX = new double[n];
        var dispY = new double[n];
        var temp = Width * 0.1;
        var cooling = temp / (Iterations + 1);

        for (var iter = 0; iter < Iterations; iter++)
        {
            Array.Clear(dispX);
            Array.Clear(dispY);

            // Repulsion, every ordered pair once. O(n^2): fine at this scale, and the report says at what node
            // count it stops being fine rather than assuming Barnes-Hut is needed.
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var dx = g.Nodes[i].X - g.Nodes[j].X;
                    var dy = g.Nodes[i].Y - g.Nodes[j].Y;
                    var d2 = dx * dx + dy * dy;
                    if (d2 < 1e-12) { dx = 1e-6 * (i + 1); dy = 1e-6 * (j + 1); d2 = dx * dx + dy * dy; }
                    var d = Math.Sqrt(d2);
                    var force = k * k / d;
                    var ux = dx / d;
                    var uy = dy / d;
                    dispX[i] += ux * force; dispY[i] += uy * force;
                    dispX[j] -= ux * force; dispY[j] -= uy * force;
                }
            }

            // Attraction along edges, weighted by support so a strong couple pulls harder. Edges are already
            // in ordinal order from the builder, so this loop's accumulation order is fixed.
            foreach (var e in g.Edges)
            {
                var a = g.Nodes[e.A];
                var b = g.Nodes[e.B];
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1e-9) continue;
                var w = 1.0 + Math.Log(e.Support);
                var force = d * d / k * w;
                var ux = dx / d;
                var uy = dy / d;
                dispX[e.A] -= ux * force; dispY[e.A] -= uy * force;
                dispX[e.B] += ux * force; dispY[e.B] += uy * force;
            }

            for (var i = 0; i < n; i++)
            {
                var d = Math.Sqrt(dispX[i] * dispX[i] + dispY[i] * dispY[i]);
                if (d > 1e-12)
                {
                    var limit = Math.Min(d, temp) / d;
                    g.Nodes[i].X += dispX[i] * limit;
                    g.Nodes[i].Y += dispY[i] * limit;
                }
                g.Nodes[i].X = Math.Clamp(g.Nodes[i].X, 0, Width);
                g.Nodes[i].Y = Math.Clamp(g.Nodes[i].Y, 0, Height);
            }

            temp -= cooling;
        }

        Normalize(g);
    }

    /// <summary>Rescales to fill [0,1] on both axes so the client never has to know the solver's units. Degenerate
    /// (zero-extent) axes are centred rather than divided by zero.</summary>
    private static void Normalize(Graph g)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var node in g.Nodes)
        {
            minX = Math.Min(minX, node.X); maxX = Math.Max(maxX, node.X);
            minY = Math.Min(minY, node.Y); maxY = Math.Max(maxY, node.Y);
        }
        var spanX = maxX - minX;
        var spanY = maxY - minY;
        foreach (var node in g.Nodes)
        {
            node.X = spanX > 1e-9 ? (node.X - minX) / spanX : 0.5;
            node.Y = spanY > 1e-9 ? (node.Y - minY) / spanY : 0.5;
        }
    }

    /// <summary>xorshift128+. Replaces System.Random because Random's algorithm is explicitly documented as an
    /// implementation detail that may change between .NET versions — which would make a "deterministic" layout
    /// deterministic only until the SDK moved under it, exactly the class of silent break AC #3 is asking about.</summary>
    private sealed class XorShift(ulong seed)
    {
        private ulong _s0 = seed == 0 ? 0x9E3779B97F4A7C15 : seed;
        private ulong _s1 = seed ^ 0xBF58476D1CE4E5B9;

        public double NextDouble()
        {
            var s1 = _s0;
            var s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return ((_s1 + s0) >> 11) * (1.0 / (1UL << 53));
        }
    }
}

internal static class Emit
{
    /// <summary>Emits the payload island. Shaped after the shipped <c>sunburst-explorer-data</c> island
    /// (SunburstExplorer.cs) — a flat node array plus a flat edge array, no nesting, minimal keys — so the byte
    /// comparison against 23.1's measured 20,915 B sunburst island is like-for-like.</summary>
    public static string ToIsland(Graph g, string title)
    {
        var sb = new StringBuilder();
        sb.Append("{\"title\":").Append(JsonSerializer.Serialize(title));
        sb.Append(",\"hub\":").Append(JsonSerializer.Serialize(g.HubLabel));
        sb.Append(",\"nodes\":[");
        for (var i = 0; i < g.Nodes.Count; i++)
        {
            var node = g.Nodes[i];
            if (i > 0) sb.Append(',');
            sb.Append("{\"id\":").Append(JsonSerializer.Serialize(node.Id))
              .Append(",\"p\":").Append(JsonSerializer.Serialize(node.Path))
              .Append(",\"l\":").Append(JsonSerializer.Serialize(node.Label))
              .Append(",\"x\":").Append(Program.F(node.X))
              .Append(",\"y\":").Append(Program.F(node.Y))
              .Append(",\"w\":").Append(node.Weight)
              .Append(",\"d\":").Append(node.Degree)
              .Append(",\"b\":").Append(JsonSerializer.Serialize(node.Boundary))
              .Append('}');
        }
        sb.Append("],\"edges\":[");
        for (var i = 0; i < g.Edges.Count; i++)
        {
            var e = g.Edges[i];
            if (i > 0) sb.Append(',');
            sb.Append("{\"a\":").Append(e.A)
              .Append(",\"b\":").Append(e.B)
              .Append(",\"s\":").Append(e.Support)
              .Append(",\"cab\":").Append(Program.F(e.ConfAB))
              .Append(",\"cba\":").Append(Program.F(e.ConfBA))
              .Append(",\"lift\":").Append(e.LiftAB is null ? "null" : Program.F(e.LiftAB.Value))
              .Append(",\"xb\":").Append(e.CrossBoundary ? "true" : "false")
              .Append(",\"k\":").Append(e.Kind == GitMetrics.CouplingKind.Process ? "\"proc\"" : "\"code\"")
              .Append('}');
        }
        sb.Append("]}");
        return sb.ToString();
    }
}

/// <summary>R5's named weak point, measured instead of argued: how many DISTINCT node sets does a continuous
/// confidence slider produce over the real data? Precompute-per-state is only viable if that number is small.</summary>
internal static class FilterProbe
{
    public static FilterResult Run(
        IReadOnlyDictionary<(string FileA, string FileB), int> pairs,
        IReadOnlyDictionary<string, int> changeCounts,
        int analyzed)
    {
        var kept = pairs.Where(kv => kv.Value >= GitMetrics.CouplingMinSupport)
                        .Select(kv => (kv.Key.FileA, kv.Key.FileB, Support: kv.Value))
                        .OrderBy(p => p.FileA, StringComparer.Ordinal)
                        .ThenBy(p => p.FileB, StringComparer.Ordinal)
                        .ToList();

        var confidences = new SortedSet<double>();
        foreach (var (a, b, s) in kept)
        {
            var ca = changeCounts.GetValueOrDefault(a, 0);
            var cb = changeCounts.GetValueOrDefault(b, 0);
            if (ca > 0) confidences.Add((double)s / ca);
            if (cb > 0) confidences.Add((double)s / cb);
        }

        // A slider is continuous, but the graph only CHANGES at a value where some edge drops out. The number of
        // distinct such breakpoints bounds the reachable states.
        //
        // BOTH sets are counted, and the distinction is load-bearing. A Fruchterman-Reingold layout is a function
        // of nodes AND edges, so "precompute one layout per reachable state" must be keyed on the EDGE set, not
        // the node set. Reporting only the node count would make precompute-per-state look far cheaper than it is
        // — precisely the error R5 warns this option is most likely to hide.
        var distinctNodeSets = new HashSet<string>(StringComparer.Ordinal);
        var distinctEdgeSets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var threshold in confidences)
        {
            var alive = kept.Where(p =>
            {
                var ca = changeCounts.GetValueOrDefault(p.FileA, 0);
                var cb = changeCounts.GetValueOrDefault(p.FileB, 0);
                var best = Math.Max(ca > 0 ? (double)p.Support / ca : 0, cb > 0 ? (double)p.Support / cb : 0);
                return best >= threshold;
            }).ToList();
            var nodes = alive.SelectMany(p => new[] { p.FileA, p.FileB })
                             .Distinct(StringComparer.Ordinal)
                             .OrderBy(p => p, StringComparer.Ordinal);
            distinctNodeSets.Add(string.Join(" ", nodes));
            distinctEdgeSets.Add(string.Join(" ", alive.Select(p => p.FileA + ">" + p.FileB)));
        }

        var supportBreakpoints = kept.Select(p => p.Support).Distinct().OrderBy(s => s).ToList();
        return new FilterResult(confidences.Count, distinctNodeSets.Count, distinctEdgeSets.Count,
            supportBreakpoints.Count, supportBreakpoints.Max());
    }
}

internal sealed record FilterResult(
    int DistinctConfidenceBreakpoints,
    int DistinctNodeSets,
    int DistinctEdgeSets,
    int DistinctSupportValues,
    int MaxSupport);

internal sealed record ScaleRow(
    string Fixture, int Floor, int Nodes, int Edges, int CrossBoundary, int Process,
    int Bytes, int MaxDegree, int Components, string HubLabel, double SolveMs);

internal sealed class ScaleReport
{
    public List<ScaleRow> Rows = [];
    public List<string> DriftedInProcess = [];
    public FilterResult? FilterStates;

    public string ToJson(int runs, int analyzed, int commits, int pairs, int files) =>
        JsonSerializer.Serialize(new
        {
            runs,
            analyzedCommits = analyzed,
            commitsParsed = commits,
            coChangePairsUncapped = pairs,
            filesWithInsight = files,
            minSupportShipped = GitMetrics.CouplingMinSupport,
            inProcessDeterminism = DriftedInProcess.Count == 0 ? "byte-identical" : "DRIFT",
            drifted = DriftedInProcess,
            filterStates = FilterStates,
            rows = Rows,
        }, new JsonSerializerOptions { WriteIndented = true });
}
