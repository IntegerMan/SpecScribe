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
}

/// <summary>Parsing and display for <see cref="DatePolicy"/> — the CLI/settings string surface kept in ONE place so
/// the accepted spellings, the rejection message, and the diagnostics label can never drift apart. [Story 5.5]</summary>
public static class DatePolicies
{
    /// <summary>The canonical spelling of each policy: what <c>--today-policy</c> documents, and what an error
    /// message lists. Forgiving variants are accepted by <see cref="TryParse"/> but deliberately not advertised.</summary>
    public const string MachineLocalToken = "machine-local";
    public const string UtcToken = "utc";
    public const string LastCommitToken = "last-commit";

    /// <summary>The canonical tokens in declaration order — the list shown in help text and in the rejection
    /// message, so a typo is answered with the exact set of things that would have worked.</summary>
    public static readonly IReadOnlyList<string> CanonicalTokens = new[] { MachineLocalToken, UtcToken, LastCommitToken };

    /// <summary>The canonical token for a policy — the value persisted to <c>.specscribe</c>-adjacent surfaces and
    /// echoed in machine-readable config output.</summary>
    public static string Token(DatePolicy policy) => policy switch
    {
        DatePolicy.Utc => UtcToken,
        DatePolicy.LastCommit => LastCommitToken,
        _ => MachineLocalToken,
    };

    /// <summary>Human-readable label for the diagnostics config row and the interactive prompt. Always a WORD, never
    /// a color or icon — the row is plain text in a <c>&lt;dl&gt;</c>, so "never signalled by color alone" holds by
    /// construction (same rule as Story 4.8 AC #2d).</summary>
    public static string Label(DatePolicy policy) => policy switch
    {
        DatePolicy.Utc => "UTC calendar day",
        DatePolicy.LastCommit => "latest authored commit day",
        _ => "machine-local calendar day",
    };

    /// <summary>Parses a user-supplied policy string. Case-insensitive, and accepts a small set of forgiving
    /// spellings (<c>machine</c>, <c>local</c>, <c>last</c>, <c>utc</c>) alongside the canonical tokens. Returns
    /// false for anything else — a typo must be REJECTED with an actionable message rather than silently falling
    /// back to the default, which would be a worse failure than an error (NFR8). A blank/absent value is "not
    /// supplied" and is also false; the caller decides that means "keep the default". [Story 5.5]</summary>
    public static bool TryParse(string? value, out DatePolicy policy)
    {
        policy = DatePolicy.MachineLocal;
        if (value is not { Length: > 0 }) return false;

        switch (value.Trim().Replace('_', '-').ToLowerInvariant())
        {
            case MachineLocalToken:
            case "machine":
            case "local":
            case "machinelocal":
                policy = DatePolicy.MachineLocal;
                return true;
            case UtcToken:
            case "utc-day":
                policy = DatePolicy.Utc;
                return true;
            case LastCommitToken:
            case "last":
            case "lastcommit":
            case "commit":
                policy = DatePolicy.LastCommit;
                return true;
            default:
                return false;
        }
    }

    /// <summary>The actionable rejection message for an unrecognized <c>--today-policy</c> value: names what was
    /// given and lists every value that would have worked. Mirrors <c>TryValidateCodeUrl</c>'s reject-don't-silently-
    /// accept discipline.</summary>
    public static string RejectionMessage(string value) =>
        $"Unrecognized --today-policy value '{value}'. Valid values: {string.Join(", ", CanonicalTokens)}.";
}
