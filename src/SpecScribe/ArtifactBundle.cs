namespace SpecScribe;

/// <summary>The normalized, host-neutral output of one <see cref="IArtifactAdapter.Ingest"/> pass — the single
/// container the projection/rendering pipeline consumes, whatever framework produced the source tree. This is
/// deliberately a typed carrier of the ALREADY host-neutral parsed models (<see cref="EpicsModel"/>,
/// <see cref="RequirementsModel"/>, <see cref="SprintStatus"/>, <see cref="RetroModel"/>), not a re-modeling
/// of them: those records carry plain data and zero HTML today, so the gap Story 4.1 fills is the named
/// boundary that produces them, not their shape (wrap, don't rewrite). Any absent artifact family is simply
/// null/empty — surfaces degrade to absent, not broken (NFR8). [Story 4.1]</summary>
public sealed record ArtifactBundle
{
    /// <summary>The detected methodology module (command catalog + well-known planning docs). Never null —
    /// an undetectable module is <see cref="ModuleContext.None"/>, which degrades to nav without module docs
    /// and no command panels, exactly as today.</summary>
    public required ModuleContext Module { get; init; }

    /// <summary>Parsed sprint tracking data, or null when the tracking file is absent or unusable — the single
    /// "no sprint data" signal the page, widget, and nav item all gate on (an unusable-but-present file also
    /// reports an <see cref="AdapterDiagnosticCategory.Unsupported"/> diagnostic).</summary>
    public SprintStatus? Sprint { get; init; }

    /// <summary>Parsed retrospectives, ordered by epic then filename (the adapter owns this normalization —
    /// downstream consumers must not re-sort). A retro file that fails to parse is reported as a diagnostic
    /// and omitted here; its siblings still appear (AC #2).</summary>
    public IReadOnlyList<RetroModel> Retros { get; init; } = Array.Empty<RetroModel>();

    /// <summary>The parsed epics/stories model, or null when no epics source exists or it failed to parse
    /// (the failure then rides <see cref="Diagnostics"/>). Story artifact links
    /// (<c>ArtifactOutputPath</c>/<c>ArtifactSourcePath</c>) are already resolved by the adapter.</summary>
    public EpicsModel? Epics { get; init; }

    /// <summary>The FR/NFR requirements model, or null when it could not be produced. BMad parses these from
    /// the same source as the epics and rolls their progress up from the epics model + progress enrichment —
    /// that ordering constraint lives inside the adapter (see <see cref="ProgressProjection"/>), never in the
    /// generator.</summary>
    public RequirementsModel? Requirements { get; init; }

    /// <summary>Full path of the source file the epics model came from, or null when none was found. Kept on
    /// the bundle so the generator can keep excluding it from the generic-pages pass (it renders specially as
    /// epics.html) — set whenever the file was FOUND, independent of whether it parsed, matching today's
    /// exclusion behavior.</summary>
    public string? EpicsSourceFullPath { get; init; }

    /// <summary>Story id (e.g. "1.2") → full path of that story's detail artifact. The projection layer needs
    /// this to compute task-level progress from the artifact files, and the renderer to build the per-story
    /// pages; discovery of the mapping is framework knowledge, so the adapter owns it.</summary>
    public IReadOnlyDictionary<string, string> StoryArtifactsById { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Source-relative paths (OS-native separators, matching the generator's relative-path
    /// convention) of every file this ingest consumed into a dedicated surface — story artifacts and retro
    /// notes — so the generic-pages pass doesn't also render them as standalone pages.</summary>
    public IReadOnlyCollection<string> ConsumedSourceRelatives { get; init; } = Array.Empty<string>();

    /// <summary>Categorized, non-fatal problems hit during ingestion (AC #2). Never causes the run to abort;
    /// the generator maps these onto the existing <see cref="GenerationEvent"/> reporting surface.</summary>
    public IReadOnlyList<AdapterDiagnostic> Diagnostics { get; init; } = Array.Empty<AdapterDiagnostic>();
}
