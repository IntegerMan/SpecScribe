namespace SpecScribe;

public enum EpicStatus { Drafted, Pending }

public enum EpicSection { VerticalSlice, FurtherDevelopment }

public sealed class StoryInfo
{
    /// <summary>"N.M", e.g. "1.1".</summary>
    public required string Id { get; init; }

    /// <summary>Framework-native human-facing identity when it differs from <see cref="Id"/>. Internal keys,
    /// output paths, and cross-model joins keep using <see cref="Id"/>; reader-facing surfaces use
    /// <see cref="DisplayName"/> so a GSD plan never exposes its synthetic ordinal.</summary>
    public string? NativeDisplayName { get; init; }

    /// <summary>The reader-facing identity. BMad retains its historical <c>Story N.M</c> label; GSD Core emits
    /// its own <c>Plan N.M</c> identity from ROADMAP.md.</summary>
    public string DisplayName => NativeDisplayName ?? $"Story {Id}";

    public required int EpicNumber { get; init; }
    public required string Title { get; init; }
    public required string UserStoryHtml { get; init; }

    /// <summary>Any HTML comment authored above the As-a/I-want narrative in epics.md, pre-rendered as a
    /// block-level <c>.md-comment</c> annotation (markers stripped); "" when the story has no such comment.
    /// Kept separate from <see cref="UserStoryHtml"/> so it renders as its own block, not folded into the
    /// italic blurb (where a block comment collapses to inline text and leaks its <c>&lt;!--</c>/<c>--&gt;</c>
    /// markers). Named opaque fragment.</summary>
    public string UserStoryNoteHtml { get; init; } = string.Empty;

    public required IReadOnlyList<string> AcBlocksHtml { get; init; }

    /// <summary>Non-retirement HTML comments found while scanning this story's AC region in epics.md (e.g. a
    /// correct-course note trailing after the last AC line, before the next story heading), pre-rendered as
    /// block-level <c>.md-comment</c> annotations (markers stripped), in source order; empty when none. Kept
    /// separate from <see cref="AcBlocksHtml"/> so they render as their own sibling blocks after the AC list
    /// instead of leaking as literal gherkin content. Named opaque fragment.</summary>
    public IReadOnlyList<string> TrailingNotesHtml { get; init; } = Array.Empty<string>();

    /// <summary>Set once a matching implementation-artifacts/*.md file is resolved; null if this story
    /// has no drafted detail file yet.</summary>
    public string? ArtifactOutputPath { get; set; }

    /// <summary>The artifact's path relative to _bmad-output/ (e.g. "implementation-artifacts/1-1-....md"),
    /// for BMad commands like /gds-dev-story that need the actual file path, not the generated page.</summary>
    public string? ArtifactSourcePath { get; set; }

    /// <summary>Task checkbox tally from the resolved artifact's "## Tasks / Subtasks" list; 0/0 when
    /// there's no artifact. Set by <see cref="ProgressCalculator"/>.</summary>
    public int TasksDone { get; set; }
    public int TasksTotal { get; set; }

    /// <summary>The artifact's "Status:" line (e.g. "ready-for-dev"); null when no artifact exists.
    /// Set by <see cref="ProgressCalculator"/>.</summary>
    public string? Status { get; set; }

    /// <summary>Framework-native identifier to pass to a workflow command when it differs from the shared
    /// synthetic story id. Null keeps the existing <see cref="Id"/> argument behavior.</summary>
    public string? WorkflowCommandArgument { get; init; }

    /// <summary>Generation-time recency for the story card: the story file's last git change date when
    /// deep-git matched the path, else the latest <c>## Change Log</c> ISO date, else null.
    /// Set by <see cref="ProgressCalculator"/>. Never a wall clock. [Story 8.8]</summary>
    public DateOnly? LastUpdatedDate { get; set; }
}

public sealed class EpicInfo
{
    public required int Number { get; init; }

    /// <summary>Framework-native human-facing identity when it differs from the internal ordinal
    /// <see cref="Number"/>. The ordinal remains the stable int key for paths and joins; this field prevents
    /// reader-facing surfaces from calling GSD Phase 7 "Epic 9".</summary>
    public string? NativeDisplayName { get; init; }

    /// <summary>The reader-facing identity. BMad retains its historical <c>Epic N</c> label; adapters with a
    /// non-integer native hierarchy label populate <see cref="NativeDisplayName"/>.</summary>
    public string DisplayName => NativeDisplayName ?? $"Epic {Number}";

    /// <summary>Framework-native identifier to pass to a workflow command when it differs from the shared
    /// synthetic epic ordinal. Null keeps the existing <see cref="Number"/> argument behavior.</summary>
    public string? WorkflowCommandArgument { get; init; }
    public required string Title { get; init; }
    public required string GoalHtml { get; init; }
    public string? FrMetaHtml { get; init; }

    /// <summary>Planning-relevant sections projected from a framework's phase companion artifact; empty when
    /// no such context exists.</summary>
    public string PhaseContextHtml { get; init; } = string.Empty;

    /// <summary>Whether the framework records that the phase has already been discussed. GSD Core derives this
    /// from its phase-local discussion log so the next-step selector does not offer a redundant discussion prompt.</summary>
    public bool HasDiscussionLog { get; init; }

    /// <summary>Whether the framework records a completed phase-specific UI plan. GSD Core derives this from its
    /// phase-local UI specification so the next-step selector does not offer redundant UI-planning work.</summary>
    public bool HasUiPlan { get; init; }

    public required EpicStatus Status { get; init; }
    public required EpicSection Section { get; init; }
    public required IReadOnlyList<StoryInfo> Stories { get; init; }

    /// <summary>True once a retrospective note has been parsed for this epic — set post-construction from the
    /// same <c>EpicRetroMap</c> the epic/story pages' retro link uses (see SiteGenerator), so it can never
    /// disagree with that link. Gates the sunburst/donut/chip/badge "In review" tier via
    /// <see cref="StatusStyles.ForEpicWithRetrospective"/>: an all-done epic isn't called finished until its
    /// retro closes it out. Default false. Deliberately NOT consumed by requirements roll-up (a retro is a
    /// closure ritual, not an implementation signal).</summary>
    public bool HasRetrospective { get; set; }

    /// <summary>Whether this epic's framework has a retrospective artifact that closes delivery. Defaults to
    /// <see langword="true"/> for BMad; GSD Core sets it false because its plan workflow has no equivalent.
    /// Visual epic status only applies the retrospective gate when this is true.</summary>
    public bool RequiresRetrospective { get; init; } = true;

    /// <summary>Rendered retirement/superseded notices classified out of story leading-comments in this epic
    /// (Story 10.5, AC3) — e.g. Story 3.4's retirement note. Empty when none matched; rendered in a collapsed
    /// "Retired" section after the active story cards instead of inline above the following story.</summary>
    public IReadOnlyList<string> RetiredNoticesHtml { get; init; } = Array.Empty<string>();
}

/// <summary>One numbered acceptance criterion pulled from a story artifact's "## Acceptance Criteria"
/// section. <see cref="Html"/> renders it in its own anchored panel row (<c>id="ac-N"</c>);
/// <see cref="PlainText"/> is the tooltip a "(AC: #N)" task reference shows when it links back to it.</summary>
public sealed record AcceptanceCriterion(int Number, string Html, string PlainText);

/// <summary>One named grouping of epics ABOVE the epic level — GSD Core's Milestone (<c>v1.0</c>, <c>v2.0</c>) and
/// its <c>## Backlog</c> band. A framework with no such level simply produces none, which is why
/// <see cref="EpicsModel.Milestones"/> defaults to empty: BMad is unchanged by CONSTRUCTION rather than by a
/// conditional (AC #4's byte-for-byte guarantee). [Story 12.2 Task 8; owner decision D1]
///
/// <para>Deliberately NOT a third level of the epic/story model. It carries no stories and no goal prose — only
/// what an epics-index band header shows: the name, the state, the declared completion date, and the roll-ups.
/// Widening <see cref="EpicInfo"/>/<see cref="StoryInfo"/> into a three-level hierarchy would touch the sunburst,
/// donut, sprint grouping, requirement roll-up and the IR schema; that is its own story, and D1 rejected it.</para></summary>
/// <param name="Name">The milestone's own label, verbatim from the framework (e.g. <c>v1.0</c>, <c>Backlog</c>).</param>
/// <param name="StatusWord">A CANONICAL lifecycle word (<c>done</c>/<c>in-progress</c>/<c>drafted</c>), already
/// mapped from the framework's native vocabulary by the ADAPTER — never a native word. Surfaces route it through
/// <see cref="StatusStyles.ForStatus"/> for the class and <see cref="StatusStyles.StoryLabel"/> for the visible
/// word, so a band badge can never mint a status the rest of the portal does not have. GSD's own words
/// (<c>Complete</c> / <c>Not started</c>) are mapped in <see cref="GsdCoreArtifactAdapter"/> precisely because
/// <c>"not started"</c> has no <see cref="StatusStyles"/> arm and would otherwise render <c>unrecognized</c>.</param>
/// <param name="CompletedDate">The declared completion date when the framework records one; null omits the marker
/// rather than inventing one (NFR8).</param>
/// <param name="EpicNumbers">The <see cref="EpicInfo.Number"/>s banded under this milestone, in roadmap order. May
/// be empty — a milestone declared with no phases yet — which the surface must render as a stated empty band, never
/// as a bare heading.</param>
public sealed record MilestoneInfo(
    string Name,
    string StatusWord,
    string? CompletedDate,
    IReadOnlyList<int> EpicNumbers);

public sealed class EpicsModel
{
    public required string OverviewHtml { get; init; }
    public required string RequirementsInventoryHtml { get; init; }
    public required IReadOnlyList<EpicInfo> Epics { get; init; }

    /// <summary>The optional milestone grouping above the epic level, in roadmap order. EMPTY for every framework
    /// that has no milestone level (BMad, BMad GDS) — and empty is the signal the epics index reads to render its
    /// chip sections exactly as it always has, byte for byte. [Story 12.2 Task 8; AC #4]</summary>
    public IReadOnlyList<MilestoneInfo> Milestones { get; init; } = Array.Empty<MilestoneInfo>();

    /// <summary>Epic numbers a source file DECLARED MORE THAN ONCE, ascending; empty for a well-formed file.
    ///
    /// <para>Story 17.1 converted eleven epic-number lookups onto <c>NumberIndex.ByFirst</c>, which tolerates a
    /// repeat instead of throwing — the right policy, since a duplicate number is a typo in a hand-authored
    /// planning file rather than a programming error, and SpecScribe's job is to document what a repository
    /// actually contains. The gap that policy left is that NOTHING downstream was taught what a collision
    /// means: both epics render to <c>epics/epic-N.html</c> so one page overwrites the other, both read the
    /// FIRST epic's progress roll-up, a duplicated <c>## Epic N</c> section double-counts its stories in every
    /// tally, and duplicate <c>(AC: #N)</c> links all resolve to the first criterion — with the run reporting
    /// <c>errors=0</c> throughout.</para>
    ///
    /// <para>This carries the fact to the adapter, which raises one non-fatal
    /// <see cref="AdapterDiagnosticCategory.Unsupported"/> notice per repeated number. Populated by
    /// <c>EpicsParser</c>; empty from adapters that cannot produce a collision.
    /// [Story 17.1 code review]</para></summary>
    public IReadOnlyList<int> DuplicateEpicNumbers { get; init; } = Array.Empty<int>();
}
