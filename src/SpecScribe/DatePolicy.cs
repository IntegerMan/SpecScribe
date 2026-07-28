using System.Globalization;

namespace SpecScribe;

/// <summary>How a run decides which calendar day counts as "today" for the date-page cutoff — the filter
/// <see cref="Charts.LinkedCommitDays"/> applies, and therefore what decides which <c>commits/{date}.html</c> pages
/// exist and which date links are drawn.
/// <para>This governs the DAY CUTOFF only. Commit timestamps keep rendering in each commit's authored offset via
/// <see cref="PortalDates"/> — <see cref="Utc"/> means "use the UTC calendar day as the cutoff", NOT "render times
/// in UTC". Story 10.4's timezone honesty is untouched by every value here. [Story 5.5]</para>
/// <para><see cref="MachineLocal"/> is deliberately the zero value so <c>default(DatePolicy)</c> — and therefore
/// every existing <see cref="ForgeOptions"/> construction that never mentions the policy — is the Story 10.4 status
/// quo, byte for byte.</para></summary>
public enum DatePolicy
{
    /// <summary>The generating machine's local calendar day (<c>DateTime.Now</c>). The default, and the honest
    /// deterministic choice for a single-machine build: what "today" means to the person running it.</summary>
    MachineLocal,

    /// <summary>The UTC calendar day (<c>DateTime.UtcNow</c>). Makes the cutoff independent of where the build ran,
    /// so a laptop just before midnight and a CI runner just after agree on which days get pages.</summary>
    Utc,

    /// <summary>The latest AUTHORED commit day — the maximum of the daily commit series, i.e. the same clock the
    /// commit days themselves are derived from. Never future-skews relative to the data, so no commit is ever
    /// excluded from its own date page. Degrades to <see cref="MachineLocal"/> when there is no git history to
    /// derive from (see <see cref="Charts.ResolveToday"/>).</summary>
    LastCommit,

    /// <summary>An explicit calendar day supplied by the user (<c>--as-of &lt;DATE&gt;</c>) — the only policy whose
    /// cutoff is an INPUT rather than a reading of a live clock or a live series, so a portal regenerated a week
    /// later reproduces the same date-page set. Carries its date on <see cref="DateCutoff.AsOf"/>; the policy member
    /// alone is not a complete configuration, and a dateless <see cref="AsOf"/> degrades to
    /// <see cref="MachineLocal"/> (see <see cref="Charts.ResolveToday"/>). [Story 5.7]</summary>
    AsOf,
}

/// <summary>The complete date-page cutoff configuration: a <see cref="DatePolicy"/> plus, for
/// <see cref="DatePolicy.AsOf"/>, the explicit day it pins to. ONE value because one value means one thing to
/// persist, one thing to attribute in <c>--show-config</c>, and one thing to log — a second parallel
/// <c>DateOnly?</c> field beside the policy could drift out of agreement with it. [Story 5.7]
/// <para>A record STRUCT deliberately: <c>default(DateCutoff)</c> is
/// <c>(<see cref="DatePolicy.MachineLocal"/>, null)</c>, so Story 5.5's "the default is the status quo by
/// construction" guarantee — every <see cref="ForgeOptions"/> construction that never mentions the cutoff renders
/// the Story 10.4 status quo, byte for byte — survives this shape change verbatim rather than having to be
/// re-established by a defaulted constructor argument somewhere.</para></summary>
/// <param name="Policy">How the cutoff day is decided.</param>
/// <param name="AsOf">The pinned day when <paramref name="Policy"/> is <see cref="DatePolicy.AsOf"/>; null for every
/// other policy (and for the dateless <see cref="DatePolicy.AsOf"/>, unreachable through the validated CLI path,
/// which degrades rather than throwing).</param>
public readonly record struct DateCutoff(DatePolicy Policy, DateOnly? AsOf);

/// <summary>Parsing and display for <see cref="DatePolicy"/> — the CLI/settings string surface kept in ONE place so
/// the accepted spellings, the rejection message, and the diagnostics label can never drift apart. [Story 5.5]</summary>
public static class DatePolicies
{
    /// <summary>The canonical spelling of each policy: what <c>--today-policy</c> documents, and what an error
    /// message lists. Forgiving variants are accepted by <see cref="TryParse"/> but deliberately not advertised.</summary>
    public const string MachineLocalToken = "machine-local";
    public const string UtcToken = "utc";
    public const string LastCommitToken = "last-commit";

    /// <summary>Prefix of the COMPOSITE fixed-date token, e.g. <c>as-of:2026-07-27</c>. Deliberately not in
    /// <see cref="CanonicalTokens"/>: that list is consumed as "things that would have worked", and a bare prefix
    /// placeholder there would be a trap (it does not parse). <c>--as-of &lt;DATE&gt;</c> is the documented surface;
    /// this token exists so the fixed date can ride the SAME single <c>today_policy</c> field everything else uses,
    /// and so <see cref="Token"/> → <see cref="TryParse"/> keeps round-tripping. [Story 5.7]</summary>
    public const string AsOfTokenPrefix = "as-of:";

    /// <summary>The canonical tokens in declaration order — the list shown in help text and in the rejection
    /// message, so a typo is answered with the exact set of things that would have worked.</summary>
    public static readonly IReadOnlyList<string> CanonicalTokens = new[] { MachineLocalToken, UtcToken, LastCommitToken };

    /// <summary>The canonical token for a cutoff — the value persisted to <c>.specscribe</c>-adjacent surfaces and
    /// echoed in machine-readable config output. The fixed policy emits the composite <c>as-of:{iso}</c> form, using
    /// the same <see cref="PortalDates.IsoDay"/> vocabulary as the <c>commits/{date}.html</c> filenames, so the
    /// persisted value and the <c>--show-config</c> value both round-trip through <see cref="TryParse"/>.
    /// <para>A dateless <see cref="DatePolicy.AsOf"/> emits <see cref="MachineLocalToken"/> — the same degrade
    /// <see cref="Charts.ResolveToday"/> applies, so the token never claims a pin the run did not actually
    /// use.</para></summary>
    public static string Token(DateCutoff cutoff) => cutoff switch
    {
        { Policy: DatePolicy.Utc } => UtcToken,
        { Policy: DatePolicy.LastCommit } => LastCommitToken,
        { Policy: DatePolicy.AsOf, AsOf: { } day } => AsOfTokenPrefix + PortalDates.IsoDay(day),
        _ => MachineLocalToken,
    };

    /// <summary>Human-readable label for the diagnostics config row and the interactive prompt. Always WORDS (and,
    /// for the fixed policy, digits) — never a color or icon, since the row is plain text in a <c>&lt;dl&gt;</c>, so
    /// "never signalled by color alone" holds by construction (same rule as Story 4.8 AC #2d).</summary>
    public static string Label(DateCutoff cutoff) => cutoff switch
    {
        { Policy: DatePolicy.Utc } => "UTC calendar day",
        { Policy: DatePolicy.LastCommit } => "latest authored commit day",
        { Policy: DatePolicy.AsOf, AsOf: { } day } => $"fixed date {PortalDates.IsoDay(day)}",
        // The interactive menu offers the policy before it has asked for the date — this is the label it shows then.
        { Policy: DatePolicy.AsOf } => "fixed date",
        _ => "machine-local calendar day",
    };

    /// <summary>Parses a user-supplied policy string. Case-insensitive, and accepts a small set of forgiving
    /// spellings (<c>machine</c>, <c>local</c>, <c>last</c>, <c>utc</c>) alongside the canonical tokens. Returns
    /// false for anything else — a typo must be REJECTED with an actionable message rather than silently falling
    /// back to the default, which would be a worse failure than an error (NFR8). A blank/absent value is "not
    /// supplied" and is also false; the caller decides that means "keep the default". [Story 5.5]</summary>
    public static bool TryParse(string? value, out DateCutoff cutoff)
    {
        cutoff = default;
        if (value is not { Length: > 0 }) return false;

        var normalized = value.Trim().Replace('_', '-').ToLowerInvariant();

        // The one argument-bearing spelling, matched by PREFIX before the closed-vocabulary switch below: everything
        // after the colon is the date half, parsed by the same invariant-culture path --as-of uses. A bare `as-of`
        // (no colon) falls through to the switch's default and is rejected — a policy with no date is not a
        // configuration. [Story 5.7]
        if (normalized.StartsWith(AsOfTokenPrefix, StringComparison.Ordinal))
        {
            if (!TryParseAsOfDate(normalized[AsOfTokenPrefix.Length..], out var day)) return false;
            cutoff = new DateCutoff(DatePolicy.AsOf, day);
            return true;
        }

        switch (normalized)
        {
            case MachineLocalToken:
            case "machine":
            case "local":
            case "machinelocal":
                cutoff = new DateCutoff(DatePolicy.MachineLocal, null);
                return true;
            case UtcToken:
            case "utc-day":
                cutoff = new DateCutoff(DatePolicy.Utc, null);
                return true;
            case LastCommitToken:
            case "last":
            case "lastcommit":
            case "commit":
                cutoff = new DateCutoff(DatePolicy.LastCommit, null);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Parses the DATE half of a fixed cutoff — the argument to <c>--as-of</c>, and the part after
    /// <see cref="AsOfTokenPrefix"/> in the composite token. Forgiving about the format (<c>2026-07-27</c>,
    /// <c>07/27/2026</c>, …), which is acceptable ONLY because the resolved date is echoed back on the run
    /// (<see cref="ConsoleUi.PrintPaths"/>) so a misparse is visible immediately rather than silently shifting the
    /// portal. [Story 5.7 D3]
    /// <para><see cref="CultureInfo.InvariantCulture"/> is load-bearing, not a style choice: this story exists to
    /// make the date-page set the same "regardless of WHERE it is generated", and an ambient-culture parse makes one
    /// string mean different days on different hosts (the same th-TH / fa-IR non-Gregorian hazard
    /// <see cref="Charts.D"/> documents for the formatting half).</para></summary>
    public static bool TryParseAsOfDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(value?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>The actionable rejection message for an unrecognized <c>--today-policy</c> value: names what was
    /// given and lists every value that would have worked. Mirrors <c>TryValidateCodeUrl</c>'s reject-don't-silently-
    /// accept discipline. The fixed policy is named as its FLAG rather than added to
    /// <see cref="CanonicalTokens"/> — that list is "things that would have worked", and no placeholder in it would
    /// parse. [Story 5.7]</summary>
    public static string RejectionMessage(string value) =>
        $"Unrecognized --today-policy value '{value}'. Valid values: {string.Join(", ", CanonicalTokens)}. " +
        "For a fixed date, use --as-of <yyyy-MM-dd>.";

    /// <summary>The actionable rejection message for an unparseable <c>--as-of</c> date: names the value that was
    /// given and the expected shape. A typo that silently no-ops would be a worse failure than an error (NFR8).</summary>
    public static string AsOfRejectionMessage(string value) =>
        $"Unrecognized --as-of date '{value}'. Expected a calendar date such as 2026-07-27 (yyyy-MM-dd).";

    /// <summary>The actionable rejection message for <c>--as-of</c> and <c>--today-policy</c> disagreeing: both were
    /// supplied and they do not resolve to the same cutoff. Rejected rather than letting one silently win — the user
    /// asked for two different things and only one of them can happen. [Story 5.7 D1 / AC #2b]</summary>
    public static string ConflictMessage(string todayPolicy, string asOf) =>
        $"--today-policy '{todayPolicy}' conflicts with --as-of '{asOf}'. Pass --as-of on its own to pin the " +
        "date-page cutoff to a fixed date.";
}
