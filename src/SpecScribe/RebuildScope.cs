namespace SpecScribe;

/// <summary>How much of the site one watch-mode change event needs rebuilt — the answer
/// <see cref="SiteGenerator.ClassifyRebuildScope"/> returns and <see cref="FileWatcherService"/> dispatches on.
///
/// <para>This is the operational form of AD-5 — <i>"watch mode may rebuild narrowly when safe, but topology
/// changes can trigger a broader refresh to keep output coherent"</i> — and of ADR 0008 §Decision 3. Before
/// Story 22.5 the "when safe" half was never actually decided anywhere: the dispatch picked a route from the
/// changed path's FAMILY (ADR / epics-related / generic) and each route then rebuilt whatever it happened to
/// own, so the scope question was answered implicitly, in five places, by omission. Story 22.1 measured what
/// that cost — every file-level add, rename and delete stranded the cross-artifact surfaces no narrow route
/// re-renders.</para>
///
/// <para>Two values rather than a per-surface invalidation map, deliberately (owner decision D3). Story 22.1's
/// stranded-surface list is explicitly a LOWER BOUND — it was measured with deep-git off, so per-commit pages,
/// hotspot/coupling insights and the impact map were structurally invisible to it. A hand-maintained "these
/// surfaces need refreshing when X changes" table would therefore have started incomplete and rotted from
/// there, silently, exactly the way the original omission did. Escalating closes the whole class by
/// construction instead: <see cref="SiteGenerator.GenerateAll"/> wipes the output root and rebuilds from
/// source, so there is nothing left to enumerate.</para></summary>
public enum RebuildScope
{
    /// <summary>Content-only: every source file that had a page still has one, and no page needs to appear or
    /// disappear. The family route (<see cref="SiteGenerator.GenerateOne"/>,
    /// <see cref="SiteGenerator.RegenerateEpics"/>, <see cref="SiteGenerator.RegenerateAdrs"/>) can rebuild the
    /// changed scope and reach the same bytes a full regeneration would. This is the common case — a save — and
    /// keeping it narrow is what the 3×–84× incremental win is made of.</summary>
    Narrow,

    /// <summary>Topology: a source file appeared or disappeared, so the set of pages the site should contain has
    /// changed. The narrow routes key every decision off one changed path and cannot reach the cross-artifact
    /// surfaces derived from the whole tree, so this escalates to
    /// <see cref="SiteGenerator.RegenerateTopology"/> — the full rebuild, reported as one event.</summary>
    Full,
}
