using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The single construction point for every <see cref="Regex"/> in SpecScribe, so that a match
/// timeout is a property of the codebase rather than of 175 individual call sites.
/// [Story 17.2 Task 3, Sonar <c>csharpsquid:S6444</c>]
///
/// <para><b>Why this shape and not a sweep.</b> The finding count was 156 on 2026-07-27, 174 on 2026-08-07 and
/// <b>175</b> at this story's baseline — it grew while the story was being written. A one-time pass over 175
/// sites would have been re-rotting before it landed, because nothing would stop the 176th. Routing
/// construction through one factory converts a recurring chore into an invariant: a new regex gets the timeout
/// by default, and <c>TimedRegexTests.EveryRegexIsConstructedThroughTheFactory</c> fails the build if someone
/// writes a bare <c>new Regex(</c> again. This mirrors what Story 17.1 did for SSOT drift.</para>
///
/// <para><b>This is not a rule suppression.</b> ADR 0035 §Decision 5 rules that route out. The Regex objects
/// really are constructed with a timeout — Sonar's finding is satisfied at the one place construction now
/// happens, and disappears from the other 174 because there is no longer a Regex constructor there to flag.</para>
///
/// <para><b>Why a timeout rather than <see cref="RegexOptions.NonBacktracking"/> as the house default.</b>
/// Measured across all 163 construction sites, not assumed: <c>NonBacktracking</c> rejects lookarounds,
/// backreferences and atomic groups at CONSTRUCTION time, and <b>33 of the 46 regex-bearing files use a
/// lookaround</b> while <b>2 use a backreference</b> (<c>RetroActionStyler</c>, <c>Toc</c>). Forcing it as the
/// house default would throw at type-initialization across most of the codebase — on patterns that are mostly
/// not backtracking risks in the first place. A timeout applies uniformly and bounds ANY pattern, including
/// ones added later. Individual patterns may still opt into <c>NonBacktracking</c> through the
/// <c>options</c> argument; the two compose.</para>
///
/// <para><b>Why the surface matters.</b> SpecScribe parses markdown, epics and sprint files from arbitrary
/// third-party repositories, so catastrophic backtracking is an input-driven denial of service, not a
/// theoretical one. This band is the sole driver of the project's <b>C</b> security rating.</para></summary>
public static class TimedRegex
{
    /// <summary>The house match timeout.
    ///
    /// <para>Two seconds is chosen against the workload, not picked round: the longest single pattern
    /// application in a normal run is over one artifact file, and generation of this repository's 1,262 pages
    /// applies these patterns hundreds of thousands of times with no single match approaching a millisecond.
    /// Two seconds is therefore ~3 orders of magnitude above any legitimate match while still bounding a
    /// catastrophic one to something a human notices as a failure rather than a hang. A
    /// <see cref="RegexMatchTimeoutException"/> is a loud, attributable failure with the pattern in the
    /// message — deliberately preferred over a silent wrong answer.</para></summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Creates a <see cref="Regex"/> bounded by <see cref="DefaultTimeout"/>.</summary>
    public static Regex New(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options, DefaultTimeout);

    /// <summary>Creates a <see cref="Regex"/> with an explicit timeout, for the rare pattern that genuinely
    /// needs a different bound. Kept separate so a non-default timeout is visible at the call site rather than
    /// hidden in an argument list that usually ends at <c>options</c>.</summary>
    public static Regex New(string pattern, RegexOptions options, TimeSpan timeout) =>
        new(pattern, options, timeout);
}
