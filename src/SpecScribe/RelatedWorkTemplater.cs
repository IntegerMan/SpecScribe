using System.Text;

namespace SpecScribe;

/// <summary>Renders the Story 20.3 details rail — a compact card beside the explorer sunburst that augments the
/// current selection: the node's name, a one-line summary, a single most-relevant AI action (a read-only BMad
/// command badge, AD-6), and a link to its full detail page. When nothing is selected it shows a project-level
/// default card and a prompt to pick a node.
///
/// <para><b>Progressive enhancement (AC #2 / NFR8).</b> Every card is server-rendered. With JavaScript OFF the rail
/// shows the project card plus every scope's card stacked, and each card's relationship groups are expanded in a
/// native <c>&lt;details&gt;</c> — the complete work-graph relationship data, never JS-gated. With JavaScript ON
/// (<c>data-related-ready</c> on the pane) the CSS collapses that to the fancy single-card behaviour: the project
/// card by default, one scope card on selection, its <c>&lt;details&gt;</c> hidden in favour of the "View details"
/// link. The client only ever toggles which card is current; it fetches nothing and computes no count.</para>
///
/// <para><b>Empty means absent.</b> A project with no work-graph signal renders NO rail
/// (<see cref="RelatedWorkPaneModel.IsEmpty"/>) — never dead chrome. [Story 20.3]</summary>
public static class RelatedWorkTemplater
{
    /// <summary>DOM attribute marking the rail root — the one place the class ↔ script contract is named. [Story 20.3]</summary>
    public const string PaneAttribute = "data-related-pane";

    public static string RenderPane(RelatedWorkPaneModel model)
    {
        if (model.IsEmpty) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<aside class=\"chart-panel related-work-panel wm-panel wm-show-overview wm-show-track\" ");
        sb.Append(PaneAttribute);
        sb.Append(" aria-labelledby=\"related-work-h\">\n");
        sb.Append("<div class=\"chart-panel-header-row\"><h3 id=\"related-work-h\">Related work</h3>");
        if (model.WorkGraphHref is { Length: > 0 } wg)
            sb.Append($"<a class=\"view-epic-link\" href=\"{PathUtil.Html(wg)}\">View the full work graph &rarr;</a>");
        sb.Append("</div>\n");
        // Selection changes announce here, not on the sunburst's own live region — two live regions updating from
        // one activation would talk over each other. [Story 20.3]
        sb.Append("<div class=\"related-work-live sr-only\" aria-live=\"polite\"></div>\n");

        RenderProjectCard(sb, model.Project);

        // Scope cards (epics, the orphan/unplanned roots, and — since Story 20.8 D3 — the follow-up aggregates)
        // render flat; STORY cards go behind one disclosure.
        //
        // Why: with JS off — or before this script runs, or when the chart engine is blocked — there is no
        // selection, so `[data-related-ready]`'s single-card CSS never applies and EVERY card renders stacked in a
        // ~320px column. Story 20.5 made that acute: removing the story->epic fold and expanding dense epics took
        // the rail from ~30 cards to 179 (416,433 B, 45.7% of the dashboard), i.e. a JS-off reader met a page tens
        // of thousands of pixels tall. Owner decision 2026-07-26: cap what the rail RENDERS VISIBLY, not how many
        // cards exist. Every card stays in the DOM — select mode may not fetch (AC #1, `file://`-safe), so a card
        // that is not in the document is a selection that shows nothing — and ADR 0013's availability contract is
        // satisfied by a disclosure the reader can open. With JS on the script opens this and the CSS hides its
        // summary, so the single-card behaviour is byte-for-byte what it was. [Story 20.5 review]
        //
        // This disclosure remains the right answer after Story 20.8 D1 restored the fold: D1 attacks the rail's
        // BYTES (each relationship set rendered once, not twice), while this attacks the JS-off reader's SCROLL
        // HEIGHT. The card count is unchanged by D1 — every selectable node still owes a card — so removing this
        // would put ~160 story cards back in one column. Two different problems, two different fixes.
        var scopeCards = model.Cards.Where(c => !IsStoryCard(c)).ToList();
        var storyCards = model.Cards.Where(IsStoryCard).ToList();

        foreach (var card in scopeCards)
            RenderCard(sb, card, model.WorkGraphHref);

        if (storyCards.Count > 0)
        {
            sb.Append("<details class=\"related-work-more\">\n");
            sb.Append($"  <summary>{storyCards.Count} {Charts.Plural(storyCards.Count, "story", "stories")}</summary>\n");
            foreach (var card in storyCards)
                RenderCard(sb, card, model.WorkGraphHref);
            sb.Append("</details>\n");
        }

        // The work graph's own honestly-reported draw overflow (Story 20.1 spike §1a rule 5) — surfaced, never
        // silently dropped, mirroring the per-group "+N more" pattern below. [Story 20.3]
        if (model.Overflow > 0)
        {
            var link = model.WorkGraphHref is { Length: > 0 } wgHref
                ? $" <a href=\"{PathUtil.Html(wgHref)}\">See the full work graph &rarr;</a>"
                : string.Empty;
            sb.Append($"<p class=\"related-work-overflow\">{model.Overflow} more related {Charts.Plural(model.Overflow, "item", "items")} not drawn.{link}</p>\n");
        }

        // Designed empty state (AC #2) for a selection whose node has no card — revealed by JS only. Server-rendered
        // hidden: with JS off there is no selection, so every scope's card is already showing.
        sb.Append("<p class=\"related-work-empty\" data-related-empty hidden>No related work items for this selection.</p>\n");

        sb.Append("</aside>\n\n");
        return sb.ToString();
    }

    /// <summary>A story-tier card. Story island ids are the bare <c>{epic}.{story}</c> id (see
    /// <c>Charts.SunburstExplorerNodes</c>); every scope id is either <c>epic-N</c> or one of the named roots, and
    /// none of them contains a dot. Keyed on the id rather than <c>KindWord</c> so a wording change cannot silently
    /// re-tier the rail. [Story 20.5 review]</summary>
    private static bool IsStoryCard(RelatedCard card) => card.IslandId.Contains('.');

    private static void RenderProjectCard(StringBuilder sb, RelatedProjectCard card)
    {
        sb.Append("<div class=\"related-card related-card-project\" data-related-default>\n");
        sb.Append($"  <h4 class=\"related-card-title\">{PathUtil.Html(card.Title)}</h4>\n");
        sb.Append($"  <p class=\"related-card-summary\">{PathUtil.Html(card.Summary)}</p>\n");
        AppendAction(sb, card.PrimaryCommand);
        sb.Append($"  <p class=\"related-card-hint\">{PathUtil.Html(card.Hint)}</p>\n");
        sb.Append("</div>\n");
    }

    private static void RenderCard(StringBuilder sb, RelatedCard card, string? workGraphHref)
    {
        sb.Append($"<article class=\"related-card\" data-related-node=\"{PathUtil.Html(card.IslandId)}\"");
        // The ids that resolve to THIS card rather than one of their own (Story 20.8 D3 — today only
        // `epic-N~summary` → `epic-N`). Space-separated, decided in C# by RelatedWorkCards.CanonicalIslandId, so
        // the script matches on published data instead of re-deriving the redirect with a second string rule.
        if (card.Aliases is { Count: > 0 } aliases)
            sb.Append($" data-related-alias=\"{PathUtil.Html(string.Join(" ", aliases))}\"");
        sb.Append(">\n");
        // Name. The KindWord leads as a muted eyebrow (stated in words, never colour) so an epic vs story reads at a
        // glance without a coloured badge.
        sb.Append($"  <p class=\"related-card-kind\">{PathUtil.Html(card.KindWord)}</p>\n");
        sb.Append($"  <h4 class=\"related-card-title\">{PathUtil.Html(card.Title)}</h4>\n");
        sb.Append($"  <p class=\"related-card-summary\">{PathUtil.Html(card.Summary)}</p>\n");
        AppendAction(sb, card.PrimaryCommand);
        AppendMoreCommands(sb, card.MoreCommands);
        AppendChildren(sb, card);
        // The "more details" link — a distinct button to the node's own page, where its full work-graph tab lives.
        if (card.DetailHref is { Length: > 0 } href)
            sb.Append($"  <a class=\"related-card-more\" href=\"{PathUtil.Html(href)}\">View details &rarr;</a>\n");

        // The relationship groups — the JS-off relationship block (AC #2). A native <details>, expanded with JS off
        // (CSS hides it entirely once JS takes over, since the "View details" link then carries the reader onward).
        var rel = card.Relationships;
        if (rel.Groups.Count > 0 || rel.Subjects.Count > 0)
        {
            // `open` is the JS-off truth (AC #2/NFR8): the CSS hides the whole element once
            // [data-related-ready] is set, so this attribute never fights the JS-on single-card view.
            sb.Append("  <details class=\"related-card-full\" open>\n");
            sb.Append($"    <summary>Related items ({rel.EntryCount})</summary>\n");
            AppendGroups(sb, rel.Groups, rel.ScopeAnchor, workGraphHref, indent: "    ");
            foreach (var subject in rel.Subjects)
            {
                sb.Append("    <div class=\"related-subject\">\n");
                sb.Append("      <p class=\"related-subject-title\">");
                if (subject.Href is { Length: > 0 } sh)
                    sb.Append($"<a href=\"{PathUtil.Html(sh)}\">{PathUtil.Html(subject.Label)}</a>");
                else
                    sb.Append(PathUtil.Html(subject.Label));
                sb.Append("</p>\n");
                AppendGroups(sb, subject.Groups, rel.ScopeAnchor, workGraphHref, indent: "      ");
                sb.Append("    </div>\n");
            }
            sb.Append("  </details>\n");
        }

        sb.Append("</article>\n");
    }

    private static void AppendAction(StringBuilder sb, string? command)
    {
        var badge = WorkflowCommands.RenderPrimaryActionBadge(command);
        if (badge.Length == 0) return;
        sb.Append("  <div class=\"related-card-action\">");
        sb.Append(badge);
        sb.Append("</div>\n");
    }

    /// <summary>The rest of the story's status-gated command set, behind a COLLAPSED native
    /// <c>&lt;details&gt;</c> (Story 20.8 D2).
    ///
    /// <para><b>Native, and no JS-on hide rule.</b> A JS-only disclosure would hide these commands from exactly the
    /// reader AC #2 protects, so it is a real <c>&lt;details&gt;</c> — openable with the script blocked. It also
    /// gets no <c>[data-related-ready]</c> counterpart to <c>.related-card-full</c>'s hide: unlike the relationship
    /// block, nothing else on the page carries these commands, so hiding them with JS on would LOSE information
    /// rather than de-duplicate it.</para>
    ///
    /// <para><b>Not <see cref="WorkflowCommands.RenderCommandMenu"/>.</b> That helper's <c>.cmd-menu-pop</c> is an
    /// absolutely-positioned popout with <c>min-width: 22rem</c> — wider than the rail's own
    /// <c>minmax(240px, 320px)</c> column, so it would overhang the panel it lives in. This renders in flow and
    /// reuses the same command badge, which is the part that must not be re-authored (AD-2).</para>
    ///
    /// <para>The primary is already removed upstream (<c>RelatedWorkCards.MoreCommandsFor</c>): repeating it here
    /// is the "EpicEpic 19" / "Story Story 19.1" duplication class Story 20.3's live round caught. An empty set
    /// renders nothing at all rather than an empty control.</para></summary>
    private static void AppendMoreCommands(StringBuilder sb, IReadOnlyList<OutlineStoryCommand>? more)
    {
        if (more is not { Count: > 0 }) return;

        sb.Append("  <details class=\"related-card-commands\">\n");
        sb.Append($"    <summary>More actions ({more.Count})</summary>\n");
        sb.Append("    <ul class=\"related-cmd-list\">\n");
        foreach (var entry in more)
        {
            sb.Append("      <li>");
            sb.Append(WorkflowCommands.RenderPrimaryActionBadge(entry.Command));
            sb.Append($"<span class=\"related-cmd-desc\">{PathUtil.Html(entry.Description)}</span></li>\n");
        }
        sb.Append("    </ul>\n");
        sb.Append("  </details>\n");
    }

    /// <summary>The story's OPEN deferred children, by name (Story 20.8 D2). Each is a real link where the slot has
    /// a detail page and plain text where it does not — never a dead <c>&lt;a&gt;</c>. A capped list STATES its
    /// remainder in the shipped <c>+N more</c> idiom rather than truncating silently (NFR8).</summary>
    private static void AppendChildren(StringBuilder sb, RelatedCard card)
    {
        if (card.Children is not { Count: > 0 } children) return;

        sb.Append("  <div class=\"related-card-children\">\n");
        sb.Append($"    <p class=\"related-group-title\">Open follow-ups ({children.Count + card.HiddenChildren})</p>\n");
        sb.Append("    <ul class=\"related-list\">\n");
        foreach (var child in children)
        {
            sb.Append("      <li class=\"related-row\">");
            if (child.Href is { Length: > 0 } href)
                sb.Append($"<a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(child.Label)}</a>");
            else
                sb.Append($"<span class=\"related-chip\">{PathUtil.Html(child.Label)}</span>");
            sb.Append("</li>\n");
        }
        sb.Append("    </ul>\n");
        if (card.HiddenChildren > 0)
        {
            var link = card.DetailHref is { Length: > 0 } detail
                ? $" <a href=\"{PathUtil.Html(detail)}\">See them all &rarr;</a>"
                : string.Empty;
            sb.Append($"    <p class=\"related-more\">+{card.HiddenChildren} more not shown.{link}</p>\n");
        }
        sb.Append("  </div>\n");
    }

    private static void AppendGroups(
        StringBuilder sb,
        IReadOnlyList<RelatedWorkGroup> groups,
        string scopeAnchor,
        string? workGraphHref,
        string indent)
    {
        foreach (var group in groups)
        {
            sb.Append($"{indent}<div class=\"related-group\">\n");
            sb.Append($"{indent}  <p class=\"related-group-title\">{PathUtil.Html(group.Heading)}</p>\n");
            sb.Append($"{indent}  <ul class=\"related-list\">\n");
            foreach (var entry in group.Entries)
            {
                // The node kind is already named in RelatedWork.NodeText where it matters ("Deferred item: …"),
                // so no colour-only signal here.
                sb.Append($"{indent}    <li class=\"related-row\">");
                var title = entry.Title is { Length: > 0 } ? $" title=\"{PathUtil.Html(entry.Title)}\"" : string.Empty;
                if (entry.Href is { Length: > 0 })
                    sb.Append($"<a href=\"{PathUtil.Html(entry.Href)}\"{title}>{PathUtil.Html(entry.Label)}</a>");
                else
                    sb.Append($"<span class=\"related-chip\"{title}>{PathUtil.Html(entry.Label)}</span>");
                sb.Append("</li>\n");
            }
            sb.Append($"{indent}  </ul>\n");
            if (group.Hidden > 0)
            {
                var link = workGraphHref is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(workGraphHref)}#{PathUtil.Html(scopeAnchor)}\">See all on the work graph &rarr;</a>"
                    : string.Empty;
                sb.Append($"{indent}  <p class=\"related-more\">+{group.Hidden} more not shown. {link}</p>\n");
            }
            sb.Append($"{indent}</div>\n");
        }
    }
}
