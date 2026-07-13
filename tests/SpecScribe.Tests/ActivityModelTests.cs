using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the pure Story 7.3 activity helpers: the per-day artifact grouping (deterministic
/// ordering + de-duplication) and the union date-page day set (distinct, ascending).</summary>
public class ActivityModelTests
{
    [Fact]
    public void GroupArtifactsByDay_GroupsByDay_OrdersWithinDay_Deterministically()
    {
        var d1 = new DateOnly(2026, 7, 6);
        var d2 = new DateOnly(2026, 7, 4);
        var grouped = ActivityModel.GroupArtifactsByDay(new[]
        {
            (d1, "Zeta", "z.html"),
            (d1, "Alpha", "a.html"),
            (d2, "Only", "o.html"),
        });

        Assert.Equal(2, grouped.Count);
        // Within a day, ordered by label (ordinal): Alpha before Zeta.
        Assert.Equal(new[] { ("Alpha", "a.html"), ("Zeta", "z.html") }, grouped[d1]);
        Assert.Equal(new[] { ("Only", "o.html") }, grouped[d2]);
    }

    [Fact]
    public void GroupArtifactsByDay_CollapsesDuplicateLabelHrefOnSameDay()
    {
        var day = new DateOnly(2026, 7, 6);
        var grouped = ActivityModel.GroupArtifactsByDay(new[]
        {
            (day, "Epics", "epics.html"),
            (day, "Epics", "epics.html"),
        });

        Assert.Single(grouped[day]);
    }

    [Fact]
    public void GroupArtifactsByDay_EmptyInput_YieldsEmptyMap()
    {
        var grouped = ActivityModel.GroupArtifactsByDay(Array.Empty<(DateOnly, string, string)>());
        Assert.Empty(grouped);
    }

    [Fact]
    public void UnionDays_MergesDistinctAscending()
    {
        var commitDays = new[] { new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 4) };
        var artifactDays = new[] { new DateOnly(2026, 7, 4), new DateOnly(2026, 7, 8) };

        var union = ActivityModel.UnionDays(commitDays, artifactDays);

        Assert.Equal(new[]
        {
            new DateOnly(2026, 7, 4),
            new DateOnly(2026, 7, 6),
            new DateOnly(2026, 7, 8),
        }, union);
    }

    [Fact]
    public void UnionDays_EmptyInputs_YieldEmpty()
    {
        Assert.Empty(ActivityModel.UnionDays(Array.Empty<DateOnly>(), Array.Empty<DateOnly>()));
    }

    [Fact]
    public void UnionDays_ArtifactOnlyDays_AppearInSet()
    {
        var union = ActivityModel.UnionDays(
            Array.Empty<DateOnly>(),
            new[] { new DateOnly(2026, 7, 10) });

        Assert.Equal(new[] { new DateOnly(2026, 7, 10) }, union);
    }
}
