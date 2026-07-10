namespace SpecScribe;

public enum RequirementKind { Functional, NonFunctional }

/// <summary>Derived roll-up of a requirement's progress, sourced from the epic(s) that cover it. The buckets,
/// least→most complete: Deferred; Planned (covered but no task plans yet, or no coverage at all); Ready (a
/// covering epic's stories have task plans but none started); Active = "partially implemented" (a covering
/// epic has a story in dev/review/done, but not every covering epic is fully done); Done (every covering epic
/// is complete).
/// <para>Honesty caveat: the FR→Epic map is epic-level, and "Active / Partially implemented" is an
/// <em>epic-level</em> approximation — <see cref="StatusStyles.ForEpic"/> rolls up over the covering epic's
/// entire story list, not just the stories <see cref="RequirementsParser.StoriesFor"/> resolves for this
/// specific requirement. A covering epic can read "active" from a story unrelated to this requirement. This
/// deliberately supersedes the earlier stance (Story 3.7) that refused any mid-development state, but it is
/// not the finer-grained, per-requirement claim the name might suggest — it is the same epic-level signal as
/// every other requirement-status tier, just with a new label for a real epic-level state.</para></summary>
public enum RequirementStatus { Deferred, Planned, Ready, Active, Done }

/// <summary>One Functional or Non-Functional requirement parsed from epics.md's "## Requirements Inventory".
/// Its <see cref="Status"/> is rolled up from the epic named in the FR Coverage Map.</summary>
public sealed class RequirementInfo
{
    public required RequirementKind Kind { get; init; }

    /// <summary>The numeric part, e.g. 25 for "FR25".</summary>
    public required int Number { get; init; }

    /// <summary>"FR25" / "NFR7".</summary>
    public string Id => (Kind == RequirementKind.Functional ? "FR" : "NFR") + Number;

    /// <summary>"fr25" / "nfr7" — the output filename stem under requirements/.</summary>
    public string Slug => Id.ToLowerInvariant();

    /// <summary>The requirement text, rendered as inline HTML (bold/code preserved).</summary>
    public required string TextHtml { get; init; }

    /// <summary>The bold category header this FR sat under (e.g. "Core Loop & Time"); null for NFRs.</summary>
    public string? Category { get; init; }

    /// <summary>Primary covering epic from the FR Coverage Map; null when deferred or unmapped. This is
    /// deliberately <see cref="CoverageEpicNumbers"/>'s first element — the load-bearing single-epic value
    /// every existing consumer (status roll-up, detail-page card, requirements-page link) reads.</summary>
    public int? CoverageEpicNumber { get; init; }

    /// <summary>ALL covering epics from the FR Coverage Map, in source order, de-duplicated; empty when
    /// deferred or unmapped. A coverage line like "FR2: Epics 1 &amp; 2 - …" yields [1, 2] here where the
    /// singular <see cref="CoverageEpicNumber"/> keeps only the first (1). This is the "structured FR→story
    /// mapping" the requirements flow (Story 3.7) stands on — resolve it to stories via
    /// <see cref="RequirementsParser.StoriesFor"/>. [Story 3.7 Task 1]</summary>
    public required IReadOnlyList<int> CoverageEpicNumbers { get; init; }

    /// <summary>The covering epic's title, rendered as inline HTML; null when deferred or unmapped.</summary>
    public string? CoverageEpicTitleHtml { get; init; }

    /// <summary>The trailing note from the coverage-map line (after "Epic N -" / "Deferred -").</summary>
    public string? CoverageNote { get; init; }

    public bool Deferred { get; init; }

    public RequirementStatus Status { get; init; } = RequirementStatus.Planned;
}

/// <summary>All requirements parsed from epics.md, split by kind. Rebuilt whenever epics.md changes,
/// exactly like <see cref="EpicsModel"/>/<see cref="ProgressModel"/>.</summary>
public sealed class RequirementsModel
{
    public required IReadOnlyList<RequirementInfo> Functional { get; init; }
    public required IReadOnlyList<RequirementInfo> NonFunctional { get; init; }

    public IEnumerable<RequirementInfo> All => Functional.Concat(NonFunctional);

    private Dictionary<string, RequirementInfo>? _byId;

    /// <summary>Case-insensitive lookup by id ("FR25") — powers <see cref="RequirementLinkifier"/>.</summary>
    public IReadOnlyDictionary<string, RequirementInfo> ById =>
        _byId ??= All.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

    public static readonly RequirementsModel Empty = new()
    {
        Functional = Array.Empty<RequirementInfo>(),
        NonFunctional = Array.Empty<RequirementInfo>(),
    };
}
