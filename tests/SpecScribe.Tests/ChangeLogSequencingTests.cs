using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the tolerant same-day change-log sequencing (Story 10.4 AC2). The pass reformats
/// each visible date through <see cref="PortalDates"/> and adds an ordinal "(k of N)" cue to a run of consecutive
/// same-date items — while a unique date stays uncluttered and any unrecognized shape (a table, free prose) passes
/// through untouched (NFR8). It annotates existing order; it never reorders or drops content.</summary>
public class ChangeLogSequencingTests
{
    [Fact]
    public void SameDateRun_GetsOrdinalMarkers_UniqueDatesDoNot()
    {
        // The real T7 failure shape: two entries share 2026-07-06, one is unique.
        var slice = "- 2026-07-05: Created the story.\n"
                  + "- 2026-07-06: Implemented as a verify-and-harden pass.\n"
                  + "- 2026-07-06: Code review completed.";

        var result = EpicsParser.SequenceChangeLog(slice);

        Assert.Contains("- Jul 5, 2026: Created the story.", result);         // unique date, no marker
        Assert.Contains("- Jul 6, 2026 (1 of 2): Implemented", result);       // run of 2 → ordinal cue
        Assert.Contains("- Jul 6, 2026 (2 of 2): Code review", result);
        Assert.DoesNotContain("Jul 5, 2026 (", result);                       // never marks a unique day
    }

    [Fact]
    public void Dates_AreReformattedThroughPortalDates()
    {
        var slice = "- 2026-07-06: Something happened.";
        var result = EpicsParser.SequenceChangeLog(slice);

        Assert.Contains("Jul 6, 2026", result);
        Assert.DoesNotContain("2026-07-06", result); // bare ISO replaced by the one portal token
    }

    [Fact]
    public void NonConsecutiveSameDate_IsNotTreatedAsOneRun()
    {
        // Same date but separated by a different date: two runs of one, so neither is marked.
        var slice = "- 2026-07-06: First.\n- 2026-07-07: Middle.\n- 2026-07-06: Later, same day as the first.";
        var result = EpicsParser.SequenceChangeLog(slice);

        Assert.DoesNotContain("(1 of", result);
        Assert.DoesNotContain("(2 of", result);
    }

    [Fact]
    public void SameDateItems_SeparatedByAnotherEntry_AreNotOneRun()
    {
        // A distinct entry (here a slash-format date my ISO regex doesn't parse, but still a bullet) sits between two
        // 2026-07-06 items — they must NOT read as "1 of 2 / 2 of 2" across it (source-adjacency guard).
        var slice = "- 2026-07-06: First.\n- 2026/07/07: A differently-formatted middle entry.\n- 2026-07-06: Third.";
        var result = EpicsParser.SequenceChangeLog(slice);

        Assert.DoesNotContain("(1 of 2)", result);
        Assert.DoesNotContain("(2 of 2)", result);
    }

    [Fact]
    public void UnrecognizedShape_TableOrProse_PassesThroughUnchanged()
    {
        var table = "| Date | Change |\n|------|--------|\n| 2026-07-12 | Implemented the feature. |";
        Assert.Equal(table, EpicsParser.SequenceChangeLog(table));

        var prose = "This change log is written as free prose without dated bullets.";
        Assert.Equal(prose, EpicsParser.SequenceChangeLog(prose));
    }

    [Fact]
    public void OrderIsPreserved_NeverReordered()
    {
        var slice = "- 2026-07-08: Third.\n- 2026-07-06: First.\n- 2026-07-06: Second.";
        var result = EpicsParser.SequenceChangeLog(slice);

        var thirdIdx = result.IndexOf("Third", StringComparison.Ordinal);
        var firstIdx = result.IndexOf("First", StringComparison.Ordinal);
        Assert.True(thirdIdx < firstIdx, "the pass must annotate order, never reorder entries");
    }
}
