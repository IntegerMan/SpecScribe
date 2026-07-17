using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Best-effort, read-only heuristics for linking free-text coverage/action notes to where a deferral or
/// piece of tech debt was actually decided — the retro page or the deferred-work backlog page. Shared by
/// <see cref="ActionItemsTemplater"/> (retro action items) and <see cref="RequirementsTemplater"/> (a deferred
/// requirement's deferral-source link, Story 9.3 AC #2).
/// <para><b>No new authoring schema.</b> These are inferences over text that already exists — never a new tag,
/// field, or required convention in <c>epics.md</c>. A note that matches nothing renders as plain text; a link
/// is only ever offered when both the heuristic matches AND the target page exists (degrade gracefully, NFR2).
/// A more precise, explicitly-tagged deferral-source link would be an authoring-burden tradeoff that belongs in
/// an ADR, not a silent addition here (a load-bearing framework-agnostic project value). [Story 9.3]</para></summary>
public static class DeferralHeuristics
{
    // Whole-word match, not a bare substring: a raw Contains("deferred") would also fire on an unrelated word
    // that happens to embed it (e.g. "nondeferred"). Still a simple keyword heuristic for a cosmetic link, not
    // full free-text classification — phrasing like "pay down the hack" won't match. Exact Story 2.3 pattern —
    // do not broaden (e.g. bare "defer") without an intentional ActionItemsTemplater behavior change.
    // [Story 2.3 review; Story 9.3 review]
    private static readonly Regex DebtWords =
        new(@"\b(deferred|tech(nical)?\s+debt)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A single "Epic N" mention — enough to resolve a retro link, deliberately NOT a generalized reference
    // parser. "Epics 1 & 2" does not match (trailing "s" on "Epics" breaks Epic\s+), by design. Case-insensitive
    // so a note like "deferred from epic 1" still resolves. [Story 9.3 Task 5; review]
    private static readonly Regex EpicMentionRe =
        new(@"\bEpic\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>True when text is about deferred work / tech debt — the signal for surfacing a link to the
    /// deferred-work backlog page beside it.</summary>
    public static bool IsDebtRelated(string? text) => text is { Length: > 0 } && DebtWords.IsMatch(text);

    /// <summary>The first "Epic N" number mentioned in the text, or null when none is named. Used to resolve a
    /// deferred requirement's note to the matching epic's retrospective page.</summary>
    public static int? EpicMention(string? text)
    {
        if (text is not { Length: > 0 }) return null;
        var m = EpicMentionRe.Match(text);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }
}
