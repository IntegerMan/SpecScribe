namespace SpecScribe.Tests;

/// <summary>Covers <see cref="ConsoleUi.FormatPinnedCutoffLine"/> — Story 5.7 AC #2a's ordinary-run echo of a
/// pinned <c>--as-of</c> cutoff. Deliberately Spectre-free, same discipline as <c>CliFeedbackTests</c>: the pure
/// content decision is what's under test, not <c>AnsiConsole</c> itself.</summary>
public class ConsoleUiTests
{
    [Fact]
    public void FormatPinnedCutoffLine_EchoesTheIsoDateForAFixedCutoff()
    {
        var line = ConsoleUi.FormatPinnedCutoffLine(new DateCutoff(DatePolicy.AsOf, new DateOnly(2026, 7, 27)));

        Assert.NotNull(line);
        Assert.Contains("2026-07-27", line);
        Assert.Contains("--as-of", line);
    }

    [Fact]
    public void FormatPinnedCutoffLine_IsNullForMachineLocal()
    {
        Assert.Null(ConsoleUi.FormatPinnedCutoffLine(new DateCutoff(DatePolicy.MachineLocal, null)));
    }

    [Fact]
    public void FormatPinnedCutoffLine_IsNullForUtc()
    {
        Assert.Null(ConsoleUi.FormatPinnedCutoffLine(new DateCutoff(DatePolicy.Utc, null)));
    }

    [Fact]
    public void FormatPinnedCutoffLine_IsNullForLastCommit()
    {
        Assert.Null(ConsoleUi.FormatPinnedCutoffLine(new DateCutoff(DatePolicy.LastCommit, null)));
    }

    [Fact]
    public void FormatPinnedCutoffLine_IsNullForADatelessAsOf()
    {
        // Unreachable through the validated CLI path (Charts.ResolveToday degrades this to MachineLocal), but
        // ForgeOptions.Resolve is also an NFR8 library entry point — a dateless AsOf must not echo a line naming
        // a date that doesn't exist.
        Assert.Null(ConsoleUi.FormatPinnedCutoffLine(new DateCutoff(DatePolicy.AsOf, null)));
    }
}
