using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Covers the CLI's non-interactive feedback contract (Story 5.1 AC #3) and the
/// generation-outcome → exit-code mapping (AC #4).
/// <para>Everything under test is deliberately Spectre-free: <see cref="GenerationSummary"/> exists so the shape CI
/// greps and the code CI branches on can be asserted headlessly, exactly as <see cref="ForgeOptions"/> is. Driving
/// a live <c>AnsiConsole</c> would test Spectre, not us.</para></summary>
public class CliFeedbackTests
{
    private static GenerationEvent Ev(GenerationOutcome outcome, string path = "page.md", string? message = null)
        => new(outcome, path, TimeSpan.Zero, message);

    private static string FormatLine(IEnumerable<GenerationEvent> events, TimeSpan elapsed)
        => GenerationSummary.FormatLine(GenerationSummary.Count(events), elapsed);

    private static int ExitCode(IEnumerable<GenerationEvent> events)
        => GenerationSummary.ExitCode(GenerationSummary.Count(events));

    // ---- AC #3: the machine-parseable summary line -------------------------------------------------

    [Fact]
    public void FormatLine_ProducesTheDocumentedKeyValueShape()
    {
        var events = new[]
        {
            Ev(GenerationOutcome.Generated),
            Ev(GenerationOutcome.Generated),
            Ev(GenerationOutcome.Updated),
            Ev(GenerationOutcome.Skipped),
            Ev(GenerationOutcome.Error, "broken.md", "boom"),
        };

        var line = FormatLine(events, TimeSpan.FromMilliseconds(1234));

        Assert.Equal("SpecScribe: generated=2 updated=1 skipped=1 errors=1 elapsed_ms=1234", line);
    }

    [Fact]
    public void FormatLine_IsASingleGreppableLineWithNoMarkup()
    {
        var line = FormatLine(new[] { Ev(GenerationOutcome.Generated) }, TimeSpan.FromSeconds(2));

        // No control characters at all: that covers the newline/carriage-return that would split the record CI
        // greps as a unit, AND the ESC (0x1B) that would open an ANSI colour sequence. The line goes straight to
        // Console.Out rather than through AnsiConsole precisely so neither can appear.
        Assert.DoesNotContain(line, char.IsControl);
        // No Spectre markup either: '[' opens a markup tag, and the line never passes through the markup pipeline.
        Assert.DoesNotContain('[', line);
        Assert.StartsWith(GenerationSummary.LinePrefix, line, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLine_UsesInvariantNumeralsSoALocaleCannotChangeTheShape()
    {
        // A comma-decimal / dot-grouping locale must not turn elapsed_ms=1234567 into elapsed_ms=1.234.567 — the
        // line is a machine contract, not localized prose.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var line = FormatLine(
                new[] { Ev(GenerationOutcome.Generated) }, TimeSpan.FromMilliseconds(1234567));

            Assert.Equal("SpecScribe: generated=1 updated=0 skipped=0 errors=0 elapsed_ms=1234567", line);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatLine_RoundsElapsedToWholeMilliseconds()
    {
        var line = FormatLine(Array.Empty<GenerationEvent>(), TimeSpan.FromTicks(15_006));

        // 1.5006 ms -> 2, never "1.5006" or a truncated 1.
        Assert.EndsWith("elapsed_ms=2", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLine_EmitsZerosForACleanEmptyRunRatherThanOmittingKeys()
    {
        // Stable key set: a consumer's parser must never have to cope with a missing key.
        var line = FormatLine(Array.Empty<GenerationEvent>(), TimeSpan.Zero);

        Assert.Equal("SpecScribe: generated=0 updated=0 skipped=0 errors=0 elapsed_ms=0", line);
    }

    // ---- Counting ----------------------------------------------------------------------------------

    [Fact]
    public void Count_TalliesEveryOutcomeSeparately()
    {
        var counts = GenerationSummary.Count(new[]
        {
            Ev(GenerationOutcome.Generated),
            Ev(GenerationOutcome.Updated),
            Ev(GenerationOutcome.Updated),
            Ev(GenerationOutcome.Removed),
            Ev(GenerationOutcome.Skipped),
            Ev(GenerationOutcome.Error),
        });

        Assert.Equal(new GenerationCounts(Generated: 1, Updated: 2, Removed: 1, Skipped: 1, Errors: 1), counts);
        // What the human "N page(s)" line reports, and what a CI script gets by summing generated+updated.
        Assert.Equal(3, counts.Written);
    }

    // ---- AC #4: exit codes -------------------------------------------------------------------------

    [Fact]
    public void ExitCode_IsZeroWhenEveryPageSucceeded()
    {
        var events = new[] { Ev(GenerationOutcome.Generated), Ev(GenerationOutcome.Updated) };

        Assert.Equal(ExitCodes.Success, ExitCode(events));
    }

    [Fact]
    public void ExitCode_IsNonZeroWhenAnyPageErrored()
    {
        var events = new[] { Ev(GenerationOutcome.Generated), Ev(GenerationOutcome.Error, "broken.md", "boom") };

        Assert.Equal(ExitCodes.Failure, ExitCode(events));
        Assert.NotEqual(0, ExitCode(events));
    }

    [Fact]
    public void ExitCode_TreatsSkippedAsSuccess()
    {
        // NFR2: a malformed or unrecognized artifact degrades with a non-fatal notice. Failing the build on a skip
        // would make every tolerated-but-unparsed file a CI outage.
        var events = new[] { Ev(GenerationOutcome.Generated), Ev(GenerationOutcome.Skipped, "odd.md", "not tracked") };

        Assert.Equal(ExitCodes.Success, ExitCode(events));
    }

    [Fact]
    public void ExitCode_IsZeroForAnEmptyRun()
    {
        Assert.Equal(ExitCodes.Success, ExitCode(Array.Empty<GenerationEvent>()));
    }

    // ---- GenerationRun: delegation to GenerationSummary ---------------------------------------------

    [Fact]
    public void GenerationRun_ComputesExitCodeAndHadErrorsFromCounts()
    {
        // GenerationRun.ExitCode/HadErrors are thin delegations onto GenerationSummary.ExitCode/GenerationCounts.HasErrors
        // (Commands.cs) — asserted here directly against the record, independent of the Generator field, which
        // `generate` never inspects (only `watch` reads it, to keep the loop running on the same SiteGenerator).
        var failed = new GenerationRun(null!, GenerationSummary.Count(new[] { Ev(GenerationOutcome.Error) }));
        var clean = new GenerationRun(null!, GenerationSummary.Count(new[] { Ev(GenerationOutcome.Generated) }));

        Assert.True(failed.HadErrors);
        Assert.Equal(ExitCodes.Failure, failed.ExitCode);
        Assert.False(clean.HadErrors);
        Assert.Equal(ExitCodes.Success, clean.ExitCode);
    }
}
