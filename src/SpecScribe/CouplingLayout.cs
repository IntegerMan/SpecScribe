using System.Globalization;

namespace SpecScribe;

/// <summary>The generation-time layout solver for the Story 24.2 ego coupling graph — <b>node position is DATA,
/// not presentation</b> (<see href="../../docs/adrs/0030-epic-24-graph-engine.md">ADR 0030</see> §2). The client
/// draws what this computes; it runs no force simulation, no iterative solver and no physics of its own.
///
/// <para><b>The shape is owner decision D1 — "evolved hub-and-spoke".</b> The focal file is pinned dead-centre and
/// is excluded from the relaxation entirely, so it never drifts; both node populations sit on a relaxed ring
/// around it. Only the ring's <em>angles</em> are relaxed (to keep labels off each other) and each node's
/// <em>radius</em> is a pure function of its coupling strength — a strong couple sits nearer the hub. Distance is
/// therefore a real, non-colour reading of strength (UX-DR17), not decoration. Directions B (free constellation)
/// and C (concentric orbit) were offered at create-story and not chosen.</para>
///
/// <para><b>Pure, repo-free, no I/O</b> — mirroring <see cref="GitMetrics.BuildFileInsights"/>. Given the same
/// input it returns the same doubles in the same order in a fresh process, which is what makes the embedded
/// coordinates safe to byte-compare.</para>
///
/// <para><b>ADR 0030 §3's determinism construction is normative here, and all four clauses apply:</b>
/// <list type="number">
/// <item><b>No <see cref="Random"/>.</b> There is no PRNG at all: the initial ring placement is a pure function of
/// node ordinal, so the seeded-jitter the Story 24.6 spike's Fruchterman–Reingold start needed is not needed
/// either. (A pure ring is a pathological FR start; it is the *intended* start for a hub-and-spoke.)
/// <see cref="Random"/>'s algorithm is documented as an implementation detail that may change between .NET
/// versions, so a layout seeded from it would stay deterministic only until an SDK bump moved under it.</item>
/// <item><b>No <see cref="Dictionary{TKey,TValue}"/>/<see cref="HashSet{T}"/> iteration order reaches a
/// floating-point accumulation.</b> Every accumulator is a <c>double[]</c> indexed by ordinal and every loop walks
/// an array in index order. Floating-point addition is not associative, so an iteration-order change would move
/// the last bits of every coordinate — silently, and only on some machines.</item>
/// <item>No wall-clock, no environment, no parallelism.</item>
/// <item>All formatting through <see cref="CultureInfo.InvariantCulture"/> with a fixed format string
/// (<see cref="Format"/>).</item>
/// </list></para>
///
/// <para><b>Why edges may safely be passed in any order:</b> they are materialised through an explicit ordinal
/// sort (<see cref="OrderEdges"/>) before a single force is accumulated, so a caller that builds its edge list from
/// a dictionary walk cannot perturb the result.</para>
/// [Story 24.2 Task 2]</summary>
public static class CouplingLayout
{
    /// <summary>Relaxation passes over the ring. Enough for a 20-node ring to settle its angular spacing (measured
    /// stationary well before this), few enough that the whole solve stays inside a code page's generation budget —
    /// the Story 24.6 spike measured 15.1 ms for the 21-node/210-edge top-20 fixture with a heavier solver.</summary>
    private const int Iterations = 240;

    /// <summary>Radius of a MINIMUM-strength ring node, in canvas units where the focal node is at
    /// (<see cref="Centre"/>, <see cref="Centre"/>) and the canvas is the unit square. Kept below 0.5 so the widest
    /// ring still leaves room for a label outside the marker.</summary>
    private const double RingOuter = 0.46;

    /// <summary>Radius of a MAXIMUM-strength ring node. The gap to <see cref="RingOuter"/> is what makes proximity
    /// legible as strength; it is deliberately narrow enough that the hub-and-spoke silhouette survives — a wide
    /// band reads as a cloud, which is direction B and was not chosen.</summary>
    private const double RingInner = 0.30;

    /// <summary>The pinned focal position, and the centre every ring angle is measured from.</summary>
    public const double Centre = 0.5;

    /// <summary>Coordinate rounding, applied by <see cref="Format"/>.
    ///
    /// <para><b>This is a data decision, taken deliberately (Task 2), not a cosmetic one.</b> Four decimals over a
    /// unit canvas is 1e-4 of the panel's width; the code page's relationship panel measures ~320–700 px, so the
    /// quantum is at most ~0.07 px — below one device pixel even at 2× DPR. Nothing that survives rounding here can
    /// move a marker on screen, and it keeps the island small on a surface that repeats across every code page.</para>
    ///
    /// <para><b>The spike's warning does not apply to coordinates, and that distinction is the point.</b> Story 24.6
    /// found 4-decimal rounding <em>collapsing distinct confidence values</em> — 452 survived where 453 existed
    /// upstream. Two files that genuinely differ in confidence must not read as equal, so <b>confidence is never
    /// rounded through this path</b>: it reaches the reader from the text twin and the tooltip at its own precision
    /// (<see cref="Charts.Percent"/>), and the graph encodes it as position and dash class, not as a rounded number.
    /// Collapsing two coordinates that differ by a thousandth of a pixel is invisible; collapsing two confidences is
    /// a lie. Same rounding, opposite verdicts, because they are different kinds of number.</para></summary>
    private const string CoordinateFormat = "0.####";

    /// <summary>One node handed to the solver. <paramref name="Strength"/> is a normalised 0..1 pull toward the hub
    /// (1 = strongest couple, drawn nearest the centre); a caller with no strength signal passes 0 and gets the
    /// outer ring. <paramref name="IsFocal"/> marks the single pinned node — see <see cref="Solve"/> for what
    /// happens if a caller marks several.</summary>
    public readonly record struct LayoutNode(string Id, bool IsFocal, double Strength);

    /// <summary>An undirected edge between two node ORDINALS in the caller's own node list. Ordinals rather than
    /// ids so the solver never needs a dictionary — see the class remarks on why that matters.</summary>
    public readonly record struct LayoutEdge(int A, int B);

    /// <summary>A solved position in the unit square. The focal node is exactly
    /// (<see cref="Centre"/>, <see cref="Centre"/>).</summary>
    public readonly record struct LayoutPoint(double X, double Y);

    /// <summary>Solves the layout. Returns positions index-aligned with <paramref name="nodes"/>.
    ///
    /// <para>Degenerate inputs return rather than throw, because this sits on a rendering path: an empty list
    /// yields an empty result, and a lone node lands at the centre. If <paramref name="nodes"/> contains no focal
    /// node the whole population relaxes on the ring and the centre stays empty; if it contains several, the FIRST
    /// is pinned and the rest are treated as ring nodes — deterministic in both cases rather than throwing on a
    /// caller mistake that would only surface on somebody's repository.</para></summary>
    public static IReadOnlyList<LayoutPoint> Solve(
        IReadOnlyList<LayoutNode> nodes, IReadOnlyList<LayoutEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var n = nodes.Count;
        if (n == 0) return Array.Empty<LayoutPoint>();

        var focal = -1;
        for (var i = 0; i < n; i++)
        {
            if (nodes[i].IsFocal) { focal = i; break; }
        }

        // Ring membership, in the caller's order — which is already the confidence-desc, support-desc, ordinal-path
        // order Story 24.1 fixed, so the ring's angular sweep is itself the ranked order. That is why no sort
        // happens here: re-sorting would silently disagree with the text twin.
        var ring = new int[focal >= 0 ? n - 1 : n];
        var ringOf = new int[n];
        var k = 0;
        for (var i = 0; i < n; i++)
        {
            if (i == focal) { ringOf[i] = -1; continue; }
            ringOf[i] = k;
            ring[k++] = i;
        }

        var result = new LayoutPoint[n];
        if (focal >= 0) result[focal] = new LayoutPoint(Centre, Centre);
        if (k == 0) return result;
        if (k == 1)
        {
            // A single neighbour has no angular neighbour to avoid; put it due east of the hub so the one spoke is
            // horizontal rather than at whatever angle a one-element ring formula happens to produce.
            result[ring[0]] = Polar(0, RadiusFor(nodes[ring[0]].Strength));
            return result;
        }

        // --- Initial placement: evenly spaced, a pure function of ring ordinal. No PRNG (see class remarks).
        var angle = new double[k];
        var home = new double[k];
        var radius = new double[k];
        for (var r = 0; r < k; r++)
        {
            angle[r] = home[r] = 2 * Math.PI * r / k;
            radius[r] = RadiusFor(nodes[ring[r]].Strength);
        }

        // The relaxation is BOUNDED: a node may drift at most `MaxDriftFraction` of the natural spacing away from
        // its evenly-spaced home angle. Because homes are evenly spaced and the bound is symmetric, two adjacent
        // nodes can close at most 2 x that fraction of the gap between them — so a bound below 0.5 makes collision
        // IMPOSSIBLE BY CONSTRUCTION rather than merely unlikely.
        //
        // This exists because the unbounded version shipped a defect a live browser found and no assertion did:
        // on this repository's own Charts.cs page, the 203 ring-to-ring cross edges dragged the coupled arc into a
        // knot — 13 overlapping marker pairs, the worst at 40% of the separation its two markers needed. A cluster
        // should LEAN together; it should not stack.
        var spacing = 2 * Math.PI / k;
        var drift = MaxDriftFraction * spacing;

        // --- Edges, ordinal-sorted and projected to RING ordinals. Spokes (edges touching the focal node) are
        // dropped: they are radial by construction and exert no angular force, so including them would only add
        // floating-point noise in a fixed but meaningless direction.
        var ringEdges = OrderEdges(edges, n, focal, ringOf);

        // Ring degree, counted from the SAME ordered edge array the forces walk — so the normalisation below can
        // never disagree with the pulls it is normalising.
        var ringDegree = new int[k];
        for (var e = 0; e < ringEdges.Length; e++)
        {
            ringDegree[ringEdges[e].A]++;
            ringDegree[ringEdges[e].B]++;
        }

        // --- Relaxation. ANGLE only: the radius is strength and must not be negotiated away by neighbours.
        var disp = new double[k];
        // Half the natural spacing: the largest correction any one pass may apply. Cooled linearly to zero so late
        // passes only polish, which is what makes the result stable rather than oscillating between two arrangements.
        var temp = Math.PI / k;
        var cooling = temp / (Iterations + 1);

        for (var iter = 0; iter < Iterations; iter++)
        {
            Array.Clear(disp);

            // Angular repulsion, every unordered pair once, walked in index order.
            for (var a = 0; a < k; a++)
            {
                for (var b = a + 1; b < k; b++)
                {
                    var delta = Wrap(angle[a] - angle[b]);
                    // Two nodes exactly on top of each other have no defined direction to separate along; break the
                    // tie by ordinal so the choice is fixed rather than dependent on the last bit of a subtraction.
                    if (Math.Abs(delta) < 1e-12) delta = 1e-9 * (b - a);
                    var force = RepulsionScale / (k * delta);
                    disp[a] += force;
                    disp[b] -= force;
                }
            }

            // Attraction along ring-to-ring edges: two files that are themselves co-changed pull together, so a
            // cluster reads as a cluster. Weaker than repulsion by design — the ring must stay a ring.
            //
            // NORMALISED BY RING DEGREE. Without it a node sitting on 19 cross edges is pulled 19x harder than one
            // sitting on a single edge, so the densely-connected coupled arc collapses toward its own centroid
            // while sparse nodes barely move — which is precisely the knot the live pass found. Degree is a
            // property of the graph's shape, not a statement about how strongly THIS pair belongs together.
            for (var e = 0; e < ringEdges.Length; e++)
            {
                var (a, b) = ringEdges[e];
                var delta = Wrap(angle[a] - angle[b]);
                disp[a] -= AttractionScale * delta / Math.Max(1, ringDegree[a]);
                disp[b] += AttractionScale * delta / Math.Max(1, ringDegree[b]);
            }

            for (var r = 0; r < k; r++)
            {
                var d = disp[r];
                var limited = Math.Abs(d) > temp ? Math.CopySign(temp, d) : d;
                // Clamp to the drift bound around the node's HOME angle, every pass — not once at the end, so the
                // relaxation genuinely explores the space it is allowed rather than being snapped back from
                // somewhere it should never have reached.
                var offset = Wrap(angle[r] + limited - home[r]);
                if (offset > drift) offset = drift;
                else if (offset < -drift) offset = -drift;
                angle[r] = Wrap(home[r] + offset);
            }

            temp -= cooling;
        }

        for (var r = 0; r < k; r++)
        {
            result[ring[r]] = Polar(angle[r], radius[r]);
        }
        return result;
    }

    /// <summary>How hard two ring neighbours push apart. Scaled by <c>1/k</c> inside the loop so a 20-node ring and
    /// a 4-node ring settle at comparable spacing rather than the big one exploding.</summary>
    private const double RepulsionScale = 0.35;

    /// <summary>How hard a co-changed pair pulls together. An order of magnitude below
    /// <see cref="RepulsionScale"/>: clustering is a hint, not a re-layout.</summary>
    private const double AttractionScale = 0.02;

    /// <summary>The furthest a ring node may drift from its evenly-spaced home angle, as a fraction of the natural
    /// spacing between neighbours. <b>Strictly below 0.5, and that is the whole guarantee</b>: two adjacent nodes
    /// drifting toward each other can close at most <c>2 x 0.35 = 0.7</c> of the gap between them, so at least 30%
    /// of the natural spacing always survives and markers cannot stack. Large enough that a genuine cluster still
    /// visibly leans together; small enough that "ring" remains an honest description of the shape.</summary>
    private const double MaxDriftFraction = 0.35;

    /// <summary>Strength → radius. Linear, so the reading "nearer the hub means a stronger couple" is uniform
    /// rather than compressed at one end. Strength is clamped rather than trusted: it arrives from a computed
    /// confidence and a caller passing 1.2 should get the inner ring, not a node inside the hub.</summary>
    private static double RadiusFor(double strength)
    {
        var s = double.IsNaN(strength) ? 0 : Math.Clamp(strength, 0, 1);
        return RingOuter - (RingOuter - RingInner) * s;
    }

    private static LayoutPoint Polar(double theta, double r) =>
        new(Centre + r * Math.Cos(theta), Centre + r * Math.Sin(theta));

    /// <summary>Wraps an angle difference into (-π, π] so "just past 0" and "just before 2π" read as adjacent.</summary>
    private static double Wrap(double theta)
    {
        var t = theta;
        while (t > Math.PI) t -= 2 * Math.PI;
        while (t <= -Math.PI) t += 2 * Math.PI;
        return t;
    }

    /// <summary>Materialises the caller's edges as ring-ordinal pairs in an explicit, total ordinal order — the
    /// ADR 0030 §3 clause that keeps a dictionary walk upstream from reaching a floating-point accumulation.
    /// Out-of-range ordinals, self-edges and spokes are dropped rather than throwing (this is a rendering path).
    /// Duplicates are KEPT: two identical edges are two real pulls, and silently de-duplicating would make the
    /// result depend on whether the caller happened to emit a pair twice.</summary>
    private static (int A, int B)[] OrderEdges(
        IReadOnlyList<LayoutEdge> edges, int n, int focal, int[] ringOf)
    {
        var kept = new List<(int A, int B)>(edges.Count);
        foreach (var e in edges)
        {
            if (e.A < 0 || e.A >= n || e.B < 0 || e.B >= n) continue;
            if (e.A == e.B || e.A == focal || e.B == focal) continue;
            var a = ringOf[e.A];
            var b = ringOf[e.B];
            kept.Add(a <= b ? (a, b) : (b, a));
        }
        kept.Sort(static (x, y) => x.A != y.A ? x.A.CompareTo(y.A) : x.B.CompareTo(y.B));
        return kept.ToArray();
    }

    /// <summary>The ONE coordinate formatter. Fixed format string, invariant culture — so a machine whose current
    /// culture writes a decimal comma cannot emit an island that parses as a different number, or as two.</summary>
    public static string Format(double value) =>
        value.ToString(CoordinateFormat, CultureInfo.InvariantCulture);
}
