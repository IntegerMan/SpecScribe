using System.Globalization;

namespace SpecScribe;

/// <summary>The single source of every human-facing date and clock string in the portal (Story 10.4 "one date
/// token"; feedback T7/F2). Every surface that shows a human a date or a time routes through here, so the footer,
/// the Git Pulse, heatmap tooltips, ADR cards, retro cards, and change logs can never drift into the ~9 different
/// hand-rolled <c>ToString(...)</c> formats they used before. Pure + <see cref="CultureInfo.InvariantCulture"/> so
/// output is byte-identical across machines/locales — the same determinism discipline <see cref="GitMetrics"/>
/// documents for non-Gregorian calendars (a th-TH / fa-IR machine must render the same bytes).
///
/// Machine tokens deliberately do NOT unify: <see cref="IsoDay"/> is the ISO identifier used in heatmap cell hrefs
/// and per-day page filenames (<c>commits/2026-07-04.html</c>) and must stay <c>yyyy-MM-dd</c>; the git parse format
/// <c>--date=format:%Y-%m-%dT%H:%M</c> is a wire contract, not a human string. Only human-visible dates/clocks route
/// here — a reviewer greps <c>src/</c> for stray date <c>ToString</c> formats and should find none outside this class
/// (the sole exception is <c>ConsoleUi</c>'s progress clock, which is console output, not portal HTML — out of scope).
///
/// Timezone policy (owner-flagged, Story 10.4): git commit times stay in the commit's authored offset and the
/// generation clock stays machine-local; callers pass a <paramref name="zoneLabel"/> (or caption the clock once)
/// so the two "local" clocks are distinguishable and each is self-describing — never converted to UTC/viewer-local
/// (that would break cross-machine determinism and misrepresent author-local commit times).</summary>
public static class PortalDates
{
    /// <summary>The one human calendar-date token. Month-name so there is no US/EU <c>MM/dd</c> vs <c>dd/MM</c>
    /// ambiguity, compact enough for cards. e.g. "Jul 9, 2026".</summary>
    public const string DayFormat = "MMM d, yyyy";

    /// <summary>The one clock time-of-day token — 24-hour, matching git and carrying no AM/PM ambiguity, so the
    /// footer and the Git Pulse stop disagreeing. e.g. "17:14".</summary>
    public const string TimeFormat = "HH:mm";

    /// <summary>The one human date string. "Jul 9, 2026".</summary>
    public static string Day(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);

    /// <summary>The one human date string, from a <see cref="DateTime"/> (drops the time component).</summary>
    public static string Day(DateTime dt) => Day(DateOnly.FromDateTime(dt));

    /// <summary>Weekday-prefixed variant for at-a-glance scanning (heatmap headline/tooltips), built from the SAME
    /// <see cref="Day"/> token so it can never drift. "Fri, Jul 4, 2026".</summary>
    public static string DayWithWeekday(DateOnly day) =>
        day.ToString("ddd, ", CultureInfo.InvariantCulture) + Day(day);

    /// <summary>The machine/URL date token — ISO, kept separate from the human token on purpose (heatmap cell hrefs,
    /// per-day page filenames). Never reformat these to a human date. "2026-07-04".</summary>
    public static string IsoDay(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Short month name for the heatmap's month-gutter axis labels — the same month token the human
    /// <see cref="DayFormat"/> uses, exposed here so no surface hand-rolls a <c>ToString("MMM")</c>. "Jul".</summary>
    public static string MonthShort(DateOnly day) => day.ToString("MMM", CultureInfo.InvariantCulture);

    /// <summary>The one time-of-day rendering (24-hour). "17:14".</summary>
    public static string TimeOfDay(DateTime dt) => dt.ToString(TimeFormat, CultureInfo.InvariantCulture);

    /// <summary>A full "date at time (zone)" clock string. <paramref name="zoneLabel"/> is appended when non-empty so
    /// the reader always knows WHICH clock and which zone this is (see the timezone policy on the class). "Jul 4, 2026
    /// at 23:30 UTC-05:00", or just "Jul 4, 2026 at 23:30" when the zone is captioned once elsewhere.</summary>
    public static string Timestamp(DateOnly day, string hhmm, string? zoneLabel = null) =>
        $"{Day(day)} at {hhmm}{(zoneLabel is { Length: > 0 } z ? " " + z : string.Empty)}";

    /// <summary>Convenience overload: format a <see cref="DateTime"/>'s date + 24-hour time as a clock string.</summary>
    public static string Timestamp(DateTime dt, string? zoneLabel = null) =>
        Timestamp(DateOnly.FromDateTime(dt), TimeOfDay(dt), zoneLabel);

    /// <summary>The authored-date shapes tolerated when normalizing a hand-typed frontmatter/card date to the one
    /// portal token. ISO first (what the artifacts use), then a couple of common spellings.</summary>
    private static readonly string[] AuthoredDayFormats =
        { "yyyy-MM-dd", "yyyy/MM/dd", "MMMM d, yyyy", "MMM d, yyyy", "d MMMM yyyy", "M/d/yyyy" };

    /// <summary>The single tolerant authored-date parser shared by every surface that accepts a hand-typed date
    /// (ADR "**Date:**" lines, retro/doc card meta) so they can't disagree on what parses (Story 10.4). Invariant
    /// for determinism. <c>false</c> (no <paramref name="day"/>) when the string isn't one of the tolerated shapes.</summary>
    public static bool TryParseDay(string value, out DateOnly day) =>
        DateOnly.TryParseExact(value.Trim(), AuthoredDayFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    /// <summary>Normalizes a hand-authored date string to the single portal date token when it parses as a date
    /// (e.g. a card's "2026-07-05" → "Jul 5, 2026"); returns the original string verbatim when it doesn't parse, so
    /// free-text meta ("Draft", "TBD") is never mangled (degrade — NFR8). Invariant parse for determinism.</summary>
    public static string ReformatAuthored(string authored) =>
        TryParseDay(authored, out var day) ? Day(day) : authored;

    /// <summary>A compact, deterministic-per-machine UTC-offset label for the generation clock's zone, e.g.
    /// "UTC-04:00". Uses the machine's current offset (DST-correct for <paramref name="local"/>). Kept here so the
    /// footer's zone label routes through the single formatter like everything else. This value legitimately varies
    /// per generating machine — the golden fingerprint normalizes the footer clock to keep output portable.</summary>
    public static string LocalZoneLabel(DateTime local)
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(local);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return "UTC" + sign + offset.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }
}
