using System.Text;

namespace SpecScribe;

/// <summary>One side of an <see cref="EntityPager"/>: the sibling page's href (already relative to the current
/// page) and its human name (rendered as the link's <c>title</c> tooltip). [Prev/next navigation]</summary>
public sealed record PagerLink(string Href, string Label);

/// <summary>A compact <c>&lsaquo; Prev</c> / <c>Next &rsaquo;</c> sibling-navigation control for a leaf entity page
/// (commit, date, epic, story, ADR, retro, code file). Each side is <c>null</c> when the current page sits at that
/// end of its family's canonical order — rendered disabled, never wrapping.
/// <para>One rule spans every family: <c>Prev</c> is the predecessor and <c>Next</c> the successor in that family's
/// DISPLAY order. A newest-first chronological family (commits, dates) therefore yields Prev = newer / Next = older;
/// an ascending-numbered family (epics, stories, ADRs, retros) yields Prev = lower / Next = higher; code files order
/// alphabetically within their directory. The family only chooses its sort — the pager stays a single code path.
/// The visible text is always the fixed <c>&lsaquo; Prev</c> / <c>Next &rsaquo;</c>; the sibling's real name rides a
/// <c>title=</c> tooltip. Pure HTML/CSS, no JS. [Prev/next navigation]</para></summary>
public sealed record EntityPager(PagerLink? Prev, PagerLink? Next)
{
    /// <summary>The empty pager — no sibling on either side. <see cref="Render"/> emits nothing.</summary>
    public static EntityPager None { get; } = new(null, null);

    /// <summary>Builds a pager for the item at <paramref name="index"/> in a family's already-sorted
    /// <paramref name="canonicalOrder"/>: <c>Prev</c> is the preceding item, <c>Next</c> the following one, each
    /// <c>null</c> at the ends (never wraps). <paramref name="href"/> projects a sibling to its page href (relative
    /// to the CURRENT page) and <paramref name="label"/> to its tooltip name. An out-of-range or negative index
    /// yields <see cref="None"/> rather than throwing.</summary>
    public static EntityPager FromSequence<T>(
        IReadOnlyList<T> canonicalOrder,
        int index,
        Func<T, string> href,
        Func<T, string> label)
    {
        if (index < 0 || index >= canonicalOrder.Count) return None;
        var prev = index > 0
            ? new PagerLink(href(canonicalOrder[index - 1]), label(canonicalOrder[index - 1]))
            : null;
        var next = index < canonicalOrder.Count - 1
            ? new PagerLink(href(canonicalOrder[index + 1]), label(canonicalOrder[index + 1]))
            : null;
        return new EntityPager(prev, next);
    }

    /// <summary>True when neither side has a sibling — the control is omitted entirely (a lone family member).</summary>
    public bool IsEmpty => Prev is null && Next is null;

    /// <summary>Renders the inline control, or the empty string when <see cref="IsEmpty"/>. Each side is a real link
    /// (with the sibling name as its <c>title</c> tooltip) or a disabled <c>&lt;span&gt;</c> at a sequence end. Every
    /// href and label is escaped — sibling names (commit subjects, story/ADR titles, file paths) are free text.</summary>
    public string Render()
    {
        if (IsEmpty) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("  <nav class=\"entity-pager\" aria-label=\"Sibling navigation\">\n");
        sb.Append(RenderSide(Prev, "entity-pager-prev", "&lsaquo; Prev", "prev"));
        sb.Append(RenderSide(Next, "entity-pager-next", "Next &rsaquo;", "next"));
        sb.Append("  </nav>\n");
        return sb.ToString();
    }

    private static string RenderSide(PagerLink? link, string sideClass, string text, string rel) =>
        link is null
            ? $"    <span class=\"entity-pager-link {sideClass} is-disabled\" aria-disabled=\"true\">{text}</span>\n"
            : $"    <a class=\"entity-pager-link {sideClass}\" href=\"{PathUtil.Html(link.Href)}\" title=\"{PathUtil.Html(link.Label)}\" rel=\"{rel}\">{text}</a>\n";
}
