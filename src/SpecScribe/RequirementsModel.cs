namespace SpecScribe;

public enum RequirementKind { Functional, NonFunctional }

/// <summary>Derived roll-up of a requirement's progress, sourced from the epic that covers it. Because the
/// FR→Epic map is epic-level, we can't attribute a specific in-flight story to one requirement, so we
/// deliberately don't claim "in development": the honest buckets are Planned (no task plans yet),
/// Ready (the covering epic's stories have task plans), Done (the entire covering epic is complete), and
/// Deferred. Ordered least→most complete.</summary>
public enum RequirementStatus { Deferred, Planned, Ready, Done }

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

    /// <summary>Primary covering epic from the FR Coverage Map; null when deferred or unmapped.</summary>
    public int? CoverageEpicNumber { get; init; }

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
