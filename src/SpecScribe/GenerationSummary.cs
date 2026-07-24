using System.Globalization;

namespace SpecScribe;

/// <summary>Process exit codes the CLI contracts on. Named rather than inlined so the CI-facing meaning of a
/// non-zero exit lives in one place. [Story 5.1 AC #4]</summary>
public static class ExitCodes
{
    /// <summary>Everything the run attempted succeeded.</summary>
    public const int Success = 0;

    /// <summary>At least one page reported <see cref="GenerationOutcome.Error"/>, or a fatal exception reached
    /// <c>Program.cs</c>. One code for both: CI only needs "the build is not trustworthy".</summary>
    public const int Failure = 1;
}

/// <summary>Outcome tallies for one generation pass — the whole numeric payload of both the human summary table
/// and the machine-parseable line, derived once so the two can never disagree.</summary>
/// <param name="Removed">Structurally zero for a full <see cref="SiteGenerator.GenerateAll"/> (only the
/// incremental watch route deletes pages), so <see cref="GenerationSummary.FormatLine"/> does not emit it — the
/// count is carried here for Story 5.3's per-rebuild line to surface without re-deriving the tally.</param>
public readonly record struct GenerationCounts(int Generated, int Updated, int Removed, int Skipped, int Errors)
{
    /// <summary>Pages actually written this pass — what "N page(s)" means to a human, and what a CI script gets by
    /// summing the <c>generated</c> and <c>updated</c> keys.</summary>
    public int Written => Generated + Updated;

    public bool HasErrors => Errors > 0;
}

/// <summary>Builds the run summary and the error-to-exit-code decision as pure functions, with no reference to
/// Spectre or <c>AnsiConsole</c> — the same seam that keeps <see cref="ForgeOptions"/> headlessly testable, and
/// the reason <see cref="ConsoleUi"/> stays the only file that knows how output is painted. [Story 5.1]</summary>
public static class GenerationSummary
{
    /// <summary>Leading token of the machine-parseable line, so CI can select it with a single fixed-string grep
    /// regardless of what human prose surrounds it.</summary>
    public const string LinePrefix = "SpecScribe:";

    /// <summary>Tallies a run's events by outcome. Total-count only — the per-error detail lines are rendered
    /// straight from the events so a failing path is never reduced to a number. [AC #4]</summary>
    public static GenerationCounts Count(IEnumerable<GenerationEvent> events)
    {
        int generated = 0, updated = 0, removed = 0, skipped = 0, errors = 0;
        foreach (var ev in events)
        {
            switch (ev.Outcome)
            {
                case GenerationOutcome.Generated: generated++; break;
                case GenerationOutcome.Updated: updated++; break;
                case GenerationOutcome.Removed: removed++; break;
                case GenerationOutcome.Skipped: skipped++; break;
                case GenerationOutcome.Error: errors++; break;
            }
        }

        return new GenerationCounts(generated, updated, removed, skipped, errors);
    }

    /// <summary>The single machine-parseable summary line (UX-DR15): one line, no markup, no color, no padding,
    /// stable key order, <see cref="CultureInfo.InvariantCulture"/> numerals so a comma-decimal locale can never
    /// change the shape a CI regex has to match.
    /// <para>Shape: <c>SpecScribe: generated=&lt;n&gt; updated=&lt;n&gt; skipped=&lt;n&gt; errors=&lt;n&gt;
    /// elapsed_ms=&lt;n&gt;</c>. Key=value rather than JSON deliberately: it stays greppable/awk-able with no
    /// parser, and remains forward-compatible with an opt-in <c>--json</c> emitting a richer object later.</para></summary>
    public static string FormatLine(GenerationCounts counts, TimeSpan elapsed)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{LinePrefix} generated={counts.Generated} updated={counts.Updated} skipped={counts.Skipped} errors={counts.Errors} elapsed_ms={(long)Math.Round(elapsed.TotalMilliseconds)}");

    /// <summary>Convenience overload for callers holding the raw events.</summary>
    public static string FormatLine(IEnumerable<GenerationEvent> events, TimeSpan elapsed)
        => FormatLine(Count(events), elapsed);

    /// <summary>Maps a run's outcome onto a process exit code: any <see cref="GenerationOutcome.Error"/> makes the
    /// run untrustworthy, so CI must see a non-zero exit. Deliberately NOT triggered by
    /// <see cref="GenerationOutcome.Skipped"/> — a skip is NFR2's "degrade with a non-fatal notice", not a
    /// failure. [AC #4]</summary>
    public static int ExitCode(GenerationCounts counts) => counts.HasErrors ? ExitCodes.Failure : ExitCodes.Success;

    /// <summary>Convenience overload for callers holding the raw events.</summary>
    public static int ExitCode(IEnumerable<GenerationEvent> events) => ExitCode(Count(events));
}
