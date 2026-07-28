using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Covers Story 5.5's date-page "today" policy and Story 5.7's fixed-date extension: the pure
/// <see cref="Charts.ResolveToday"/> resolver (all four policies plus the two degrade cases), the CLI string
/// surface's forgiving-but-never-silent parsing, and the load-bearing invariant that the resolved value is what
/// <see cref="Charts.LinkedCommitDays"/> filters on — so the linked-day set and the generated date-page set are the
/// same set by construction, under every policy.</summary>
public class DatePolicyTests
{
    /// <summary>The cutoff for a dateless policy — the three Story 5.5 policies, spelled as the record they now
    /// travel in.</summary>
    private static DateCutoff Cutoff(DatePolicy policy) => new(policy, null);

    /// <summary>A pinned cutoff, the Story 5.7 shape.</summary>
    private static DateCutoff AsOf(int year, int month, int day) =>
        new(DatePolicy.AsOf, new DateOnly(year, month, day));

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
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), Charts.ResolveToday(Cutoff(DatePolicy.MachineLocal), null));
    }

    [Fact]
    public void ResolveToday_DefaultCutoffIsMachineLocal()
    {
        // Load-bearing, and the reason DateCutoff is a record STRUCT: every ForgeOptions construction that never
        // mentions the cutoff must land on the status quo. [Story 5.7 Task 1]
        Assert.Equal(new DateCutoff(DatePolicy.MachineLocal, null), default(DateCutoff));
        Assert.Equal(DatePolicy.MachineLocal, default(DateCutoff).Policy);
        Assert.Null(default(DateCutoff).AsOf);
        Assert.Equal(Charts.ResolveToday(Cutoff(DatePolicy.MachineLocal), null), Charts.ResolveToday(default, null));
    }

    [Fact]
    public void ResolveToday_Utc_IsTheUtcCalendarDay()
    {
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), Charts.ResolveToday(Cutoff(DatePolicy.Utc), null));
    }

    [Fact]
    public void ResolveToday_LastCommit_IsTheSeriesMaximum()
    {
        // Deliberately unordered input: the resolver must take the max, not the last element.
        var series = Series((1, 5, 2), (1, 20, 1), (1, 9, 3));

        Assert.Equal(new DateOnly(2026, 1, 20), Charts.ResolveToday(Cutoff(DatePolicy.LastCommit), series));
    }

    [Fact]
    public void ResolveToday_LastCommit_HonorsAFutureDatedSeriesMax()
    {
        // The whole point of the policy: the cutoff is derived from the authored clock, so a commit authored
        // "tomorrow" relative to the build machine is still on or before its own cutoff and keeps its date page.
        var future = DateOnly.FromDateTime(DateTime.Now).AddDays(3);
        var series = new[] { (Day: future, Count: 1) };

        Assert.Equal(future, Charts.ResolveToday(Cutoff(DatePolicy.LastCommit), series));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveToday_LastCommit_FallsBackToMachineLocalWithoutHistory(bool emptyRatherThanNull)
    {
        // No git, or an empty repo: there are no authored commit days to derive from, so degrade rather than
        // crash or invent a sentinel date (NFR8).
        var series = emptyRatherThanNull ? Array.Empty<(DateOnly Day, int Count)>() : null;

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), Charts.ResolveToday(Cutoff(DatePolicy.LastCommit), series));
    }

    [Fact]
    public void ResolveToday_AsOf_IsTheSuppliedDate()
    {
        // The only policy whose answer is an INPUT — no clock, no series, so the same flag yields the same cutoff
        // on any host on any day. A live series is deliberately passed to prove it is ignored. [Story 5.7 AC #1]
        var series = Series((1, 5, 2), (3, 1, 4));

        Assert.Equal(new DateOnly(2026, 2, 10), Charts.ResolveToday(AsOf(2026, 2, 10), series));
        Assert.Equal(new DateOnly(2026, 2, 10), Charts.ResolveToday(AsOf(2026, 2, 10), series: null));
    }

    [Fact]
    public void ResolveToday_AsOf_WithoutADate_FallsBackToMachineLocal()
    {
        // Unreachable through the validated CLI path (a dateless `as-of` token is rejected by TryParse), but this
        // resolver is the LIBRARY entry point too, so a hand-built cutoff must degrade rather than throw —
        // mirroring LastCommit-without-history. [Story 5.7 Task 1, NFR8]
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), Charts.ResolveToday(Cutoff(DatePolicy.AsOf), null));
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

        var machineLinked = Charts.LinkedCommitDays(series, commits, Charts.ResolveToday(Cutoff(DatePolicy.MachineLocal), series));
        var lastCommitLinked = Charts.LinkedCommitDays(series, commits, Charts.ResolveToday(Cutoff(DatePolicy.LastCommit), series));

        // Default policy: the future-skewed day is excluded (status quo, unchanged).
        Assert.DoesNotContain(ahead, machineLinked);
        // LastCommit policy: the cutoff moves with the data, so the day is linked AND therefore generated.
        Assert.Contains(ahead, lastCommitLinked);
    }

    [Fact]
    public void ResolvedToday_DrivesTheLinkedDaySet_UnderAsOfPolicy()
    {
        // The pinned cutoff is what LinkedCommitDays filters on: days after it are excluded even though the build
        // machine's own clock is well past them. [Story 5.7 AC #1]
        var series = Series((1, 5, 1), (1, 9, 1), (3, 1, 1));
        var commits = Commits(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9), new DateOnly(2026, 3, 1));

        var linked = Charts.LinkedCommitDays(series, commits, Charts.ResolveToday(AsOf(2026, 1, 31), series));

        Assert.Equal(new[] { new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9) }, linked);
    }

    [Fact]
    public void OneResolvedToday_MakesEveryConsumerAgree()
    {
        // The structural invariant: two consumers filtering on the SAME resolved value cannot disagree about which
        // days are linked vs. generated. (Independent re-resolution is what this story removes.)
        var series = Series((1, 5, 2), (1, 9, 1), (1, 20, 0), (3, 1, 4));
        var commits = Commits(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9), new DateOnly(2026, 3, 1));

        var cutoffs = new[]
        {
            Cutoff(DatePolicy.MachineLocal),
            Cutoff(DatePolicy.Utc),
            Cutoff(DatePolicy.LastCommit),
            AsOf(2026, 1, 31),
        };

        foreach (var cutoff in cutoffs)
        {
            var today = Charts.ResolveToday(cutoff, series);
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
        Assert.True(DatePolicies.TryParse(input, out var cutoff));
        Assert.Equal(expected, cutoff.Policy);
        // A dateless policy never carries a date — the two halves of the record cannot drift apart.
        Assert.Null(cutoff.AsOf);
    }

    [Theory]
    [InlineData("as-of:2026-07-27")]
    [InlineData("AS-OF:2026-07-27")]
    [InlineData("as_of:2026-07-27")]
    [InlineData("  as-of:2026-07-27  ")]
    public void TryParse_AcceptsTheCompositeFixedDateToken(string input)
    {
        // The composite token is how the fixed date rides the single today_policy field: persistence and
        // --show-config both round-trip through exactly this path. [Story 5.7 D1]
        Assert.True(DatePolicies.TryParse(input, out var cutoff));
        Assert.Equal(new DateCutoff(DatePolicy.AsOf, new DateOnly(2026, 7, 27)), cutoff);
    }

    [Theory]
    [InlineData("as-of")]
    [InlineData("as-of:")]
    [InlineData("as-of:notadate")]
    [InlineData("as-of:2026-13-45")]
    [InlineData("as-of:2026-02-30")]
    public void TryParse_RejectsAFixedPolicyWithoutAUsableDate(string input)
    {
        // A policy with no date is not a configuration — accepting it would produce a token that silently degrades
        // back to machine-local, which is precisely the "typo quietly no-ops" failure NFR8 forbids.
        Assert.False(DatePolicies.TryParse(input, out _));
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
    public void TryParseAsOfDate_IsCultureInvariant()
    {
        // Load-bearing for the story's own premise ("the same date-page set regardless of WHERE it is generated"):
        // an ambient-culture parse would make one string mean different days on different hosts. Pinned by the
        // unambiguous ISO form plus the invariant month/day/year order. [Story 5.7 D3]
        Assert.True(DatePolicies.TryParseAsOfDate("2026-07-27", out var iso));
        Assert.Equal(new DateOnly(2026, 7, 27), iso);
        Assert.True(DatePolicies.TryParseAsOfDate("07/27/2026", out var slashed));
        Assert.Equal(new DateOnly(2026, 7, 27), slashed);
        // 27/07/2026 is day-first — valid in many ambient cultures, NOT in the invariant one.
        Assert.False(DatePolicies.TryParseAsOfDate("27/07/2026", out _));
    }

    [Fact]
    public void RejectionMessage_NamesTheValueAndEveryValidOption()
    {
        var message = DatePolicies.RejectionMessage("yesterday");

        Assert.Contains("yesterday", message, StringComparison.Ordinal);
        Assert.All(DatePolicies.CanonicalTokens, t => Assert.Contains(t, message, StringComparison.Ordinal));
        // The fixed policy is named as its FLAG, never as an unparseable placeholder in CanonicalTokens — that list
        // is consumed as "things that would have worked". [Story 5.7 Task 1]
        Assert.Contains("--as-of", message, StringComparison.Ordinal);
        Assert.All(DatePolicies.CanonicalTokens, t => Assert.True(DatePolicies.TryParse(t, out _)));
        Assert.DoesNotContain(DatePolicies.AsOfTokenPrefix, DatePolicies.CanonicalTokens);
    }

    [Fact]
    public void AsOfRejectionMessage_NamesTheValueAndTheExpectedShape()
    {
        var message = DatePolicies.AsOfRejectionMessage("lastweek");

        Assert.Contains("lastweek", message, StringComparison.Ordinal);
        Assert.Contains("yyyy-MM-dd", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictMessage_NamesBothFlags()
    {
        var message = DatePolicies.ConflictMessage("utc", "2026-07-27");

        Assert.Contains("--today-policy", message, StringComparison.Ordinal);
        Assert.Contains("--as-of", message, StringComparison.Ordinal);
        Assert.Contains("utc", message, StringComparison.Ordinal);
        Assert.Contains("2026-07-27", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DatePolicy.MachineLocal, "machine-local")]
    [InlineData(DatePolicy.Utc, "utc")]
    [InlineData(DatePolicy.LastCommit, "last-commit")]
    public void Token_RoundTripsThroughTryParse(DatePolicy policy, string expected)
    {
        var cutoff = Cutoff(policy);

        Assert.Equal(expected, DatePolicies.Token(cutoff));
        Assert.True(DatePolicies.TryParse(DatePolicies.Token(cutoff), out var parsed));
        Assert.Equal(cutoff, parsed);
    }

    [Fact]
    public void Token_RoundTripsTheCompositeFixedDateToken()
    {
        var cutoff = AsOf(2026, 7, 27);

        // ISO, so the token shares the commits/{date}.html filename vocabulary.
        Assert.Equal("as-of:2026-07-27", DatePolicies.Token(cutoff));
        Assert.True(DatePolicies.TryParse(DatePolicies.Token(cutoff), out var parsed));
        Assert.Equal(cutoff, parsed);
    }

    [Fact]
    public void Token_ForADatelessFixedPolicy_DegradesToMachineLocal()
    {
        // The token must never claim a pin the run did not actually use: ResolveToday degrades this same cutoff to
        // the machine-local day, so the reported token has to agree with it.
        Assert.Equal(DatePolicies.MachineLocalToken, DatePolicies.Token(Cutoff(DatePolicy.AsOf)));
    }

    [Fact]
    public void Label_IsDistinctNonEmptyTextForEveryPolicy()
    {
        // The diagnostics row and the interactive prompt are TEXT — never color-only (UX-DR17 / NFR6). Every enum
        // member is constructed as a REALISTIC cutoff, so the fixed policy is judged with its date attached.
        var labels = Enum.GetValues<DatePolicy>()
            .Select(p => DatePolicies.Label(p == DatePolicy.AsOf ? AsOf(2026, 7, 27) : Cutoff(p)))
            .ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("2026-07-27", DatePolicies.Label(AsOf(2026, 7, 27)), StringComparison.Ordinal);
    }
}
