namespace DocsForge;

public enum EpicStatus { Drafted, Pending }

public enum EpicSection { VerticalSlice, FurtherDevelopment }

public sealed class StoryInfo
{
    /// <summary>"N.M", e.g. "1.1".</summary>
    public required string Id { get; init; }
    public required int EpicNumber { get; init; }
    public required string Title { get; init; }
    public required string UserStoryHtml { get; init; }
    public required IReadOnlyList<string> AcBlocksHtml { get; init; }

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
}

public sealed class EpicInfo
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string GoalHtml { get; init; }
    public string? FrMetaHtml { get; init; }
    public required EpicStatus Status { get; init; }
    public required EpicSection Section { get; init; }
    public required IReadOnlyList<StoryInfo> Stories { get; init; }
}

/// <summary>One numbered acceptance criterion pulled from a story artifact's "## Acceptance Criteria"
/// section. <see cref="Html"/> renders it in its own anchored panel row (<c>id="ac-N"</c>);
/// <see cref="PlainText"/> is the tooltip a "(AC: #N)" task reference shows when it links back to it.</summary>
public sealed record AcceptanceCriterion(int Number, string Html, string PlainText);

public sealed class EpicsModel
{
    public required string OverviewHtml { get; init; }
    public required string RequirementsInventoryHtml { get; init; }
    public required IReadOnlyList<EpicInfo> Epics { get; init; }
}
