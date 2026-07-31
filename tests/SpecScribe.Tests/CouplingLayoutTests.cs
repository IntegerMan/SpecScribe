using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 24.2 generation-time layout solver. Two things are being pinned here and
/// they are different in kind: the SHAPE (owner decision D1's evolved hub-and-spoke — focal pinned, ring relaxed,
/// radius carrying strength) and the DETERMINISM CONSTRUCTION that ADR 0030 §3 makes normative, because a layout
/// that is only usually reproducible produces a golden artifact that is only usually stable.</summary>
public class CouplingLayoutTests
{
    private static CouplingLayout.LayoutNode Focal(string id) => new(id, IsFocal: true, Strength: 0);
    private static CouplingLayout.LayoutNode Ring(string id, double strength = 0) => new(id, IsFocal: false, strength);

    private static IReadOnlyList<CouplingLayout.LayoutNode> Sample() => new[]
    {
        Focal("f"),
        Ring("a", 0.9), Ring("b", 0.6), Ring("c", 0.3), Ring("d", 0.1), Ring("e"),
    };

    private static IReadOnlyList<CouplingLayout.LayoutEdge> SampleEdges() => new CouplingLayout.LayoutEdge[]
    {
        new(0, 1), new(0, 2), new(0, 3), new(0, 4), new(0, 5), new(1, 2), new(2, 3),
    };

    [Fact]
    public void Solve_PinsTheFocalNodeAtTheExactCentreAndExcludesItFromRelaxation()
    {
        // Owner decision D1: the focal file sits dead-centre and NEVER moves. Not "near the middle" — exactly the
        // centre, because that is what makes the hub-and-spoke read survive a graph whose ring is lopsided.
        var points = CouplingLayout.Solve(Sample(), SampleEdges());

        Assert.Equal(CouplingLayout.Centre, points[0].X);
        Assert.Equal(CouplingLayout.Centre, points[0].Y);
    }

    [Fact]
    public void Solve_RadiusCarriesStrength_StrongerCouplesSitNearerTheHub()
    {
        // The graph's one CONTINUOUS channel for confidence. It is not stroke width, and that is a consequence of
        // the engine rather than a preference: Plotly's line style is trace-level, so width can only be banded
        // (ADR 0030 §5). Distance can be continuous, so distance is what carries it.
        var points = CouplingLayout.Solve(Sample(), SampleEdges());

        double R(int i) => Math.Sqrt(
            Math.Pow(points[i].X - CouplingLayout.Centre, 2) + Math.Pow(points[i].Y - CouplingLayout.Centre, 2));

        // a (0.9) < b (0.6) < c (0.3) < d (0.1) < e (0) in radius, monotonically.
        Assert.True(R(1) < R(2), "a stronger couple must be drawn nearer the hub than a weaker one");
        Assert.True(R(2) < R(3));
        Assert.True(R(3) < R(4));
        Assert.True(R(4) < R(5));
    }

    [Fact]
    public void Solve_StrengthIsClampedRatherThanTrusted()
    {
        // Strength arrives from a computed confidence. A caller passing 1.4 or NaN must get a sane ring position,
        // never a node inside the hub or a NaN coordinate that would reach the island as literal "NaN".
        var nodes = new[] { Focal("f"), Ring("hi", 1.4), Ring("lo", -3), Ring("nan", double.NaN) };
        var points = CouplingLayout.Solve(nodes, Array.Empty<CouplingLayout.LayoutEdge>());

        foreach (var p in points)
        {
            Assert.False(double.IsNaN(p.X));
            Assert.False(double.IsNaN(p.Y));
            Assert.InRange(p.X, 0, 1);
            Assert.InRange(p.Y, 0, 1);
        }
    }

    [Fact]
    public void Solve_IsDeterministicAcrossRepeatedCallsWithinAProcess()
    {
        var a = CouplingLayout.Solve(Sample(), SampleEdges());
        var b = CouplingLayout.Solve(Sample(), SampleEdges());

        Assert.Equal(a, b);
    }

    [Fact]
    public void Solve_IsIndependentOfTheCallersEdgeORDER()
    {
        // ADR 0030 §3: no dictionary or set iteration order may reach a floating-point accumulation, because
        // floating-point addition is not associative and an order change moves the last bits of every coordinate.
        // The solver defends against that by ordinal-sorting the edge list before accumulating anything — so a
        // caller that happens to emit its edges in a different order MUST get identical output.
        var forward = SampleEdges();
        var shuffled = forward.Reverse().ToList();

        var a = CouplingLayout.Solve(Sample(), forward);
        var b = CouplingLayout.Solve(Sample(), shuffled);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Solve_DropsSelfEdgesSpokesAndOutOfRangeOrdinalsRatherThanThrowing()
    {
        // This sits on a rendering path: a malformed edge from an index-aligned upstream builder must degrade, not
        // take down a code page on somebody's repository.
        var edges = new CouplingLayout.LayoutEdge[]
        {
            new(0, 1),      // a spoke — radial by construction, no angular force
            new(2, 2),      // self-edge
            new(-1, 3),     // out of range
            new(1, 99),     // out of range
            new(1, 2),      // the only real ring-to-ring edge
        };

        var ex = Record.Exception(() => CouplingLayout.Solve(Sample(), edges));
        Assert.Null(ex);
    }

    [Fact]
    public void Solve_DegenerateInputs_ReturnCentredOrEmptyRatherThanThrowing()
    {
        Assert.Empty(CouplingLayout.Solve(Array.Empty<CouplingLayout.LayoutNode>(), Array.Empty<CouplingLayout.LayoutEdge>()));

        // A lone focal node: the whole graph is the hub.
        var lone = CouplingLayout.Solve(new[] { Focal("f") }, Array.Empty<CouplingLayout.LayoutEdge>());
        Assert.Equal(CouplingLayout.Centre, lone[0].X);
        Assert.Equal(CouplingLayout.Centre, lone[0].Y);

        // A single neighbour has no angular neighbour to avoid — it goes due east, so the one spoke is horizontal
        // rather than at whatever angle a one-element ring formula happens to produce.
        var one = CouplingLayout.Solve(new[] { Focal("f"), Ring("a") }, Array.Empty<CouplingLayout.LayoutEdge>());
        Assert.True(one[1].X > CouplingLayout.Centre);
        Assert.Equal(CouplingLayout.Centre, one[1].Y, 9);

        // No focal node at all: the whole population relaxes on the ring and the centre stays empty.
        var noFocal = CouplingLayout.Solve(new[] { Ring("a"), Ring("b") }, Array.Empty<CouplingLayout.LayoutEdge>());
        Assert.Equal(2, noFocal.Count);
    }

    [Fact]
    public void Solve_SeveralFocalNodes_PinsTheFirstAndRingsTheRest()
    {
        // Deterministic on a caller mistake rather than throwing on one, because the alternative surfaces only on
        // somebody else's repository.
        var nodes = new[] { Focal("f1"), Focal("f2"), Ring("a") };
        var points = CouplingLayout.Solve(nodes, Array.Empty<CouplingLayout.LayoutEdge>());

        Assert.Equal(CouplingLayout.Centre, points[0].X);
        Assert.Equal(CouplingLayout.Centre, points[0].Y);
        Assert.NotEqual(CouplingLayout.Centre, points[1].X);
    }

    [Fact]
    public void Solve_AngularDriftIsBoundedSoMarkersCannotStack()
    {
        // THE COLLISION GUARANTEE, and it is the reason the relaxation is bounded rather than free. An unbounded
        // version shipped and a live browser found it: on this repository's own Charts.cs page the 203 ring-to-ring
        // cross edges dragged the coupled arc into a knot — 13 overlapping marker pairs, the worst at 40% of the
        // separation its two markers needed. No assertion saw it, because "the solver terminated" and "the chart is
        // legible" are different claims.
        //
        // Bounding each node to ±35% of the natural spacing around its evenly-spaced home makes the worst case
        // arithmetic rather than empirical: two neighbours leaning toward each other close at most 70% of the gap,
        // so at least 30% always survives. Asserted here on a DENSELY connected ring — every ring pair joined —
        // because that is the input that produced the defect.
        const int k = 35;
        var nodes = new List<CouplingLayout.LayoutNode> { Focal("f") };
        for (var i = 0; i < k; i++) nodes.Add(Ring("n" + i.ToString(CultureInfo.InvariantCulture), i / (double)k));
        var edges = new List<CouplingLayout.LayoutEdge>();
        for (var i = 1; i <= k; i++)
        {
            for (var j = i + 1; j <= k; j++) edges.Add(new CouplingLayout.LayoutEdge(i, j));
        }

        var points = CouplingLayout.Solve(nodes, edges);

        // Angular separation between the two nodes sharing a home-angle gap must stay above 30% of that gap.
        var spacing = 2 * Math.PI / k;
        var minKept = double.MaxValue;
        for (var i = 1; i <= k; i++)
        {
            var a = Math.Atan2(points[i].Y - CouplingLayout.Centre, points[i].X - CouplingLayout.Centre);
            var nextIdx = i == k ? 1 : i + 1;
            var b = Math.Atan2(points[nextIdx].Y - CouplingLayout.Centre, points[nextIdx].X - CouplingLayout.Centre);
            var gap = b - a;
            while (gap <= 0) gap += 2 * Math.PI;
            while (gap > 2 * Math.PI) gap -= 2 * Math.PI;
            minKept = Math.Min(minKept, gap);
        }

        Assert.True(minKept > 0.30 * spacing,
            $"neighbouring ring nodes closed to {minKept:F4} rad, below the 30% of {spacing:F4} the drift bound guarantees");
    }

    [Fact]
    public void Format_UsesInvariantCultureSoACommaDecimalLocaleCannotEmitTwoNumbers()
    {
        // ADR 0030 §3's fourth clause. On a comma-decimal machine an uncultured ToString would emit `0,4275`, which
        // does not merely look wrong in the island — inside a JSON array it PARSES as two values.
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("0.4275", CouplingLayout.Format(0.42749));
            Assert.Equal("0.5", CouplingLayout.Format(0.5));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
