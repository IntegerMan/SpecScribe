namespace SpecScribe;

/// <summary>What a <c>sprint-status.yaml</c> <c>development_status</c> key denotes: an epic
/// (<c>epic-N</c>), a story (<c>N-M-slug</c>), or an epic's retrospective (<c>epic-N-retrospective</c>).</summary>
public enum SprintEntryKind { Epic, Story, Retrospective }

/// <summary>One <c>development_status</c> row: its kind, the raw yaml key, the parsed epic/story numbers
/// (for grouping + linking, null where not applicable), and its tracked lifecycle status verbatim
/// (<c>backlog</c>/<c>ready-for-dev</c>/<c>in-progress</c>/<c>review</c>/<c>done</c>, or <c>optional</c>/<c>done</c>
/// for a retrospective). A plain record — no rendering here. [Story 2.3 Task 2]</summary>
public sealed record SprintEntry(SprintEntryKind Kind, string RawKey, int? EpicNumber, int? StoryMinor, string Status);

/// <summary>One retrospective action item from the optional <c>action_items:</c> list (shape:
/// <c>epic</c>/<c>action</c>/<c>owner</c>/<c>status</c>). <see cref="Action"/> is the visible text;
/// <see cref="Status"/> is <c>open</c>/<c>in-progress</c>/<c>done</c>. [Story 2.3 Task 1]</summary>
public sealed record SprintActionItem(string Action, string Status, int? EpicNumber, string? Owner);

/// <summary>The parsed <c>sprint-status.yaml</c>: <c>development_status</c> entries in file order, the optional
/// <c>last_updated</c> scalar, and the optional retrospective action items. The authoritative sprint <em>tracking</em>
/// ledger — distinct from the status the dashboard derives from each story artifact's <c>Status:</c> frontmatter;
/// the two are kept labeled and separate (Story 1.5 truthfulness). Sized like <see cref="RequirementsModel"/>/
/// <c>AdrEntry</c> — no rendering in the model. [Story 2.3 Task 2]</summary>
public sealed class SprintStatus
{
    public required IReadOnlyList<SprintEntry> Entries { get; init; }

    public string? LastUpdated { get; init; }

    public required IReadOnlyList<SprintActionItem> ActionItems { get; init; }

    /// <summary>The action items sprint-status surfaces: open and in-progress ones (done items are settled).
    /// Preserves file order. Empty when there are no action items — the page renders nothing (not an empty
    /// header). [Story 2.3 Task 3]</summary>
    public IReadOnlyList<SprintActionItem> OpenActionItems =>
        ActionItems.Where(a => !a.Status.Equals("done", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>True when there is no tracked work — the single signal the page, widget, and nav all gate on
    /// so missing/partial data omits cleanly (AC#1/AC#2, NFR2).</summary>
    public bool IsEmpty => Entries.Count == 0;

    public static readonly SprintStatus Empty =
        new() { Entries = Array.Empty<SprintEntry>(), ActionItems = Array.Empty<SprintActionItem>() };
}
