namespace SpecScribe;

/// <summary>Parsed YAML frontmatter from a BMad markdown doc. Fields are optional — BMad docs vary widely in what they set.</summary>
public sealed class Frontmatter
{
    public string? Title { get; init; }
    public string? Project { get; init; }
    public string? Date { get; init; }

    /// <summary>Optional authored <c>created:</c> field (quick-dev / BMad specs). Observed when present —
    /// never required. Prefer over <see cref="Date"/> for timing attribution.</summary>
    public string? Created { get; init; }

    public string? Author { get; init; }
    public string? Version { get; init; }
    public string? Status { get; init; }

    /// <summary>The <c>route</c> field a <c>bmad-quick-dev</c> spec artifact carries (e.g. <c>one-shot</c>),
    /// used to classify a doc as quick-dev/direct work. Null for the vast majority of BMad docs that don't
    /// set it.</summary>
    public string? Route { get; init; }

    /// <summary>The <c>type</c> field a quick-dev spec artifact carries (e.g. <c>chore</c>, <c>feature</c>) —
    /// optional context alongside <see cref="Route"/>. Null when unset.</summary>
    public string? Type { get; init; }

    /// <summary>The <c>id</c> field a spec-kernel doc carries (e.g. <c>SPEC-specscribe</c>,
    /// <c>architecture-spine-specscribe</c>). Used to give the generic-H1 SPEC hub a clear index-card title.
    /// Null for the vast majority of BMad docs. [Story 2.2 Task 2]</summary>
    public string? Id { get; init; }

    /// <summary>The <c>companions</c> YAML list a SPEC kernel declares (its companion documents). Defaulted to
    /// empty so every non-spec doc is unaffected. [Story 2.2 Task 4]</summary>
    public IReadOnlyList<string> Companions { get; init; } = Array.Empty<string>();

    /// <summary>The <c>sources</c> YAML list a spec-kernel doc declares (traceability inputs). Defaulted to
    /// empty so every non-spec doc is unaffected. [Story 2.2 Task 4]</summary>
    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();

    public static readonly Frontmatter Empty = new();

    /// <summary>Best-effort calendar day from existing <c>created</c> then <c>date</c> frontmatter.
    /// Accepts portal day shapes and ISO datetimes (<c>yyyy-MM-ddTHH:mm…</c> → date part).
    /// Null when neither parses — never invents a date.</summary>
    public DateOnly? AuthoredDay()
    {
        if (Created is { Length: > 0 } created && TryAuthoredDay(created, out var fromCreated))
            return fromCreated;
        if (Date is { Length: > 0 } date && TryAuthoredDay(date, out var fromDate))
            return fromDate;
        return null;
    }

    private static bool TryAuthoredDay(string value, out DateOnly day)
    {
        if (PortalDates.TryParseDay(value, out day)) return true;
        var t = value.IndexOf('T');
        if (t > 0 && PortalDates.TryParseDay(value[..t], out day)) return true;
        day = default;
        return false;
    }
}
