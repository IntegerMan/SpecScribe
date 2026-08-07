namespace SpecScribe;

/// <summary>The epics-scoped slice of an ingest: exactly the epics + story artifacts + requirements re-parse the
/// watch-mode incremental path (<see cref="SiteGenerator.RegenerateEpics"/>) has always done, without re-ingesting
/// the sprint/retro/module state it never refreshes (AD-5: watch behaviour must not regress).
///
/// <para><b>Promoted from a nested <c>BmadArtifactAdapter</c> record to a top-level one by Story 12.2</b>, because
/// it became the return type of <see cref="IArtifactAdapter.IngestEpics"/>. Before the adapter registry,
/// <see cref="SiteGenerator"/> held the CONCRETE <see cref="BmadArtifactAdapter"/> precisely so it could reach this
/// scoped re-ingest; a registry handing back a bare <see cref="IArtifactAdapter"/> would have broken watch-mode
/// incremental regeneration, or forced it to degrade to a full re-ingest. Putting the scoped slice ON the contract
/// is the route Story 12.2 took instead: every adapter implements it, so the watch path keeps calling exactly the
/// method it always called and BMad's bytes are unchanged by construction (ADR 0027 defines "safe" as proven
/// byte-identical to a full rebuild). The record's shape is unchanged from Story 4.1.</para></summary>
/// <param name="SourceFullPath">Set whenever the epics source file was FOUND, independent of parse success, so
/// callers can keep excluding it from generic-page rendering exactly as before.</param>
/// <param name="Epics">The parsed epics model, or null when no source existed or it failed to parse.</param>
/// <param name="Requirements">The requirements model, or null when this framework does not project one.</param>
/// <param name="StoryArtifactsById">Story id → full path of that story's detail artifact.</param>
/// <param name="ConsumedSourceRelatives">Source-relative paths consumed into a dedicated surface.</param>
/// <param name="Diagnostics">Categorized, non-fatal problems hit during this scoped ingest.</param>
public sealed record EpicsIngest(
    string? SourceFullPath,
    EpicsModel? Epics,
    RequirementsModel? Requirements,
    IReadOnlyDictionary<string, string> StoryArtifactsById,
    IReadOnlyCollection<string> ConsumedSourceRelatives,
    IReadOnlyList<AdapterDiagnostic> Diagnostics)
{
    /// <summary>The "this adapter found no epics source" result — the shape every adapter returns when its
    /// framework's epics source is absent.</summary>
    public static EpicsIngest None { get; } = new(
        null, null, null,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<string>(),
        Array.Empty<AdapterDiagnostic>());
}
