using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Covers Story 5.5's date-page "today" policy: the pure <see cref="Charts.ResolveToday"/> resolver (all
/// three policies plus the no-history fallback), the CLI string surface's forgiving-but-never-silent parsing, and
/// the load-bearing invariant that the resolved value is what <see cref="Charts.LinkedCommitDays"/> filters on — so
/// the linked-day set and the generated date-page set are the same set by construction, under every policy.</summary>
public class DatePolicyTests
{
    private static (DateOnly Day, int Count)[] Series(params (int Month, int Day, int Count)[] days) =>
        days.Select(d => (new DateOnly(2026, d.Month, d.Day), d.Count)).ToArray();

    private static IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> Commits(params DateOnly[] days) =>
        days.ToDictionary(
            d => d,
            d => (IReadOnlyList<CommitInfo>)new[] { new CommitInfo("abc1234", $"commit on {d:yyyy-MM-dd}", "Author", $"{d:yyyy-MM-dd} 09:00") });

    // --- Task 1: the resolver ---

    [Fact]
    public void ResolveToday_MachineLocal_IsTheMachineCalendarDay()
    {
        // The pre-5.5 status quo, expressed exactly as the code under test must express it (AC #1).
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), Charts.ResolveToday(DatePolicy.MachineLocal, null));
    }

    [Fact]
    public void ResolveToday_DefaultEnumValueIsMachineLocal()
    {
        // Load-bearing: every ForgeOptions construction that never mentions the policy must land on the status quo.
        Assert.Equal(DatePolicy.MachineLocal, default(DatePolicy));
        Assert.Equal(Charts.ResolveToday(DatePolicy.MachineLocal, null), Charts.ResolveToday(default, null));
    }

    [Fact]
    public void ResolveToday_Utc_IsTheUtcCalendarDay()
    {
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), Charts.ResolveToday(DatePolicy.Utc, null));
    }

    [Fact]
    public void ResolveToday_LastCommit_IsTheSeriesMaximum()
    {
        // Deliberately unordered input: the resolver must take the max, not the last element.
        var series = Series((1, 5, 2), (1, 20, 1), (1, 9, 3));

        Assert.Equal(new DateOnly(2026, 1, 20), Charts.ResolveToday(DatePolicy.LastCommit, series));
    }

    [Fact]
    public void ResolveToday_LastCommit_HonorsAFutureDatedSeriesMax()
    {
        // The whole point of the policy: the cutoff is derived from the authored clock, so a commit authored
        // "tomorrow" relative to the build machine is still on or before its own cutoff and keeps its date page.
        var future = DateOnly.FromDateTime(DateTime.Now).AddDays(3);
        var series = new[] { (Day: future, Count: 1) };

        Assert.Equal(future, Charts.ResolveToday(DatePolicy.LastCommit, series));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveToday_LastCommit_FallsBackToMachineLocalWithoutHistory(bool emptyRatherThanNull)
    {
        // No git, or an empty repo: there are no authored commit days to derive from, so degrade rather than
        // crash or invent a sentinel date (NFR8).
        var series = emptyRatherThanNull ? Array.Empty<(DateOnly Day, int Count)>() : null;

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), Charts.ResolveToday(DatePolicy.LastCommit, series));
    }

    // --- Task 5: AC #2 consistency — the resolved value drives the linked set ---

    [Fact]
    public void ResolvedToday_DrivesTheLinkedDaySet_UnderLastCommitPolicy()
    {
        // A commit authored ahead of the build machine's clock — the exact boundary defect the story exists for.
        var machineToday = DateOnly.FromDateTime(DateTime.Now);
        var ahead = machineToday.AddDays(1);
        var series = new[] { (Day: machineToday.AddDays(-2), Count: 1), (Day: ahead, Count: 1) };
        var commits = Commits(machineToday.AddDays(-2), ahead);

        var machineLinked = Charts.LinkedCommitDays(series, commits, Charts.ResolveToday(DatePolicy.MachineLocal, series));
        var lastCommitLinked = Charts.LinkedCommitDays(series, commits, Charts.ResolveToday(DatePolicy.LastCommit, series));

        // Default policy: the future-skewed day is excluded (status quo, unchanged).
        Assert.DoesNotContain(ahead, machineLinked);
        // LastCommit policy: the cutoff moves with the data, so the day is linked AND therefore generated.
        Assert.Contains(ahead, lastCommitLinked);
    }

    [Fact]
    public void OneResolvedToday_MakesEveryConsumerAgree()
    {
        // The structural invariant: two consumers filtering on the SAME resolved value cannot disagree about which
        // days are linked vs. generated. (Independent re-resolution is what this story removes.)
        var series = Series((1, 5, 2), (1, 9, 1), (1, 20, 0), (3, 1, 4));
        var commits = Commits(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9), new DateOnly(2026, 3, 1));

        foreach (var policy in new[] { DatePolicy.MachineLocal, DatePolicy.Utc, DatePolicy.LastCommit })
        {
            var today = Charts.ResolveToday(policy, series);
            var linked = Charts.LinkedCommitDays(series, commits, today);
            var generated = Charts.LinkedCommitDays(series, commits, today);

            Assert.Equal(linked, generated);
            Assert.All(linked, d => Assert.True(d <= today));
        }
    }

    // --- Task 3: the CLI string surface ---

    [Theory]
    [InlineData("machine-local", DatePolicy.MachineLocal)]
    [InlineData("MACHINE-LOCAL", DatePolicy.MachineLocal)]
    [InlineData("machine", DatePolicy.MachineLocal)]
    [InlineData("local", DatePolicy.MachineLocal)]
    [InlineData("machine_local", DatePolicy.MachineLocal)]
    [InlineData("utc", DatePolicy.Utc)]
    [InlineData("UTC", DatePolicy.Utc)]
    [InlineData("  utc  ", DatePolicy.Utc)]
    [InlineData("last-commit", DatePolicy.LastCommit)]
    [InlineData("last", DatePolicy.LastCommit)]
    [InlineData("LastCommit", DatePolicy.LastCommit)]
    public void TryParse_AcceptsCanonicalAndForgivingSpellings(string input, DatePolicy expected)
    {
        Assert.True(DatePolicies.TryParse(input, out var policy));
        Assert.Equal(expected, policy);
    }

    [Theory]
    [InlineData("utcc")]
    [InlineData("machine-lcoal")]
    [InlineData("yesterday")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RejectsAnythingElse(string? input)
    {
        // A typo must NOT silently become the default — that is a worse failure than an error (NFR8).
        Assert.False(DatePolicies.TryParse(input, out _));
    }

    [Fact]
    public void RejectionMessage_NamesTheValueAndEveryValidOption()
    {
        var message = DatePolicies.RejectionMessage("yesterday");

        Assert.Contains("yesterday", message, StringComparison.Ordinal);
        Assert.All(DatePolicies.CanonicalTokens, t => Assert.Contains(t, message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(DatePolicy.MachineLocal, "machine-local")]
    [InlineData(DatePolicy.Utc, "utc")]
    [InlineData(DatePolicy.LastCommit, "last-commit")]
    public void Token_RoundTripsThroughTryParse(DatePolicy policy, string expected)
    {
        Assert.Equal(expected, DatePolicies.Token(policy));
        Assert.True(DatePolicies.TryParse(DatePolicies.Token(policy), out var parsed));
        Assert.Equal(policy, parsed);
    }

    [Fact]
    public void Label_IsDistinctNonEmptyTextForEveryPolicy()
    {
        // The diagnostics row and the interactive prompt are TEXT — never color-only (UX-DR17 / NFR6).
        var labels = Enum.GetValues<DatePolicy>().Select(DatePolicies.Label).ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }
}
