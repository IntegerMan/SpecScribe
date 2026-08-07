namespace SpecScribe;

/// <summary>Computes the projection-side progress enrichment for a freshly ingested epics model: task/story
/// roll-ups plus the optional git pulse. Supplied BY the generator (projection layer) TO
/// <see cref="IArtifactAdapter.Ingest"/> so the adapter can honor a framework-mandated parse ordering that
/// depends on progress (BMad's requirements roll up from epics + progress) WITHOUT owning any git or progress
/// computation itself — insight enrichment stays additive, non-blocking, and in the projection path (AD-4),
/// and the <c>--deep-git</c> gate never moves. [Story 4.1]</summary>
/// <param name="epics">The just-parsed epics model, with story artifact links already resolved.</param>
/// <param name="storyArtifactsById">Story id → full path of that story's detail artifact.</param>
public delegate ProgressModel ProgressProjection(EpicsModel epics, IReadOnlyDictionary<string, string> storyArtifactsById);

/// <summary>The INGESTION seam of the rendering architecture: given one framework's source tree, discover and
/// parse its artifact families into a normalized <see cref="ArtifactBundle"/> plus non-fatal
/// <see cref="AdapterDiagnostic"/>s. This is the contract every framework adapter (Stories 4.3–4.7) implements
/// so projection and rendering consume one shared host-neutral model and never learn framework specifics
/// (FR1, NFR4). Deliberately distinct from the DELIVERY adapters of AD-2/Epic 6 (<c>IRenderAdapter</c>): this
/// boundary is source → normalized records; that one is view models → host output. [Story 4.1]</summary>
public interface IArtifactAdapter
{
    /// <summary>Whether this adapter recognizes the repo as one produced by its framework — the self-selection
    /// signal a future adapter registry (Stories 4.3+) will use to pick which adapters to run. Must be cheap
    /// (a marker-directory/file sniff, not a parse) and never throw.</summary>
    /// <param name="options">Resolved run paths/settings; adapters typically sniff <see cref="ForgeOptions.RepoRoot"/>.</param>
    /// <param name="sourceFiles">Full paths of the discovered source artifacts, for adapters whose signal is
    /// artifact shape rather than a marker directory.</param>
    bool AppliesTo(ForgeOptions options, IReadOnlyList<string> sourceFiles);

    /// <summary>Discovers and parses this framework's artifact families out of <paramref name="sourceFiles"/>
    /// into a normalized bundle. NEVER throws for a bad artifact: per-artifact failures are categorized onto
    /// <see cref="ArtifactBundle.Diagnostics"/> and every successfully parsed sibling still appears in the
    /// bundle (AC #2). Ignored working files (editor temps, dotfiles) are neither ingested nor diagnosed.</summary>
    /// <param name="options">Resolved run paths/settings (source root, repo root, flags).</param>
    /// <param name="sourceFiles">Full paths of the discovered source artifacts to ingest.</param>
    /// <param name="projectProgress">Projection-layer enrichment callback (see <see cref="ProgressProjection"/>);
    /// null skips progress-dependent families (BMad: requirements).</param>
    ArtifactBundle Ingest(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress);

    /// <summary>The EPICS-SCOPED slice of <see cref="Ingest"/> — epics + story artifacts + requirements only —
    /// for watch mode's incremental regeneration, which must not re-parse the sprint/retro/module state it never
    /// refreshes (AD-5). Same never-throws contract as <see cref="Ingest"/>; returns
    /// <see cref="EpicsIngest.None"/> when this framework's epics source is absent.
    ///
    /// <para><b>On the contract deliberately, not discovered by a type test</b> [Story 12.2 Task 3]. Until the
    /// adapter registry landed, <see cref="SiteGenerator"/> held the concrete <see cref="BmadArtifactAdapter"/>
    /// SOLELY so the watch path could reach this method. A registry returning <see cref="IArtifactAdapter"/> would
    /// therefore have broken watch-mode incremental regeneration unless the scoped re-ingest was lifted here or the
    /// watch path degraded to a full re-ingest for adapters that lack it. Lifting it is the route taken: BMad's
    /// implementation is the same method body it always had, so watch output for a BMad repo is byte-identical by
    /// construction rather than by measurement (ADR 0027; ADR 0038).</para></summary>
    /// <param name="options">Resolved run paths/settings (source root, repo root, flags).</param>
    /// <param name="sourceFiles">Full paths of the discovered source artifacts to ingest.</param>
    /// <param name="projectProgress">Projection-layer enrichment callback (see <see cref="ProgressProjection"/>).</param>
    EpicsIngest IngestEpics(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress);
}
