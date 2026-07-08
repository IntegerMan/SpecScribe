using System.Text;

namespace SpecScribe;

/// <summary>Renders the sprint status page from <c>sprint-status.yaml</c> as a Jira/Kanban-style board:
/// standard sprint command buttons, a lifecycle-count summary, and a pure-CSS toggle between a status-column
/// board and an epic-grouped view, plus an open retrospective action-items section (only when non-empty).
/// Also exposes <see cref="RenderBoard"/> so the home dashboard's Now &amp; Next can render the same board.
/// Mirrors the page shell of <see cref="RequirementsTemplater"/> — one <c>&lt;main id="main-content"&gt;</c>,
/// shared nav/breadcrumb/footer. The view of the authoritative <em>tracking</em> ledger, labeled as coming
/// from the yaml so it never reads as contradicting the derived Now &amp; Next (Story 1.5). [Story 2.3]</summary>
public static class SprintTemplater
{
    /// <summary>The lifecycle stages in donut/legend display order (done → … → backlog), each paired with its
    /// shared status css class and human label. [Story 2.3]</summary>
    private static readonly (string CssClass, string Label)[] StageOrder =
    {
        ("done", "Done"),
        ("review", "In review"),
        ("active", "In progress"),
        ("ready", "Ready for dev"),
        ("pending", "Backlog"),
    };

    /// <summary>The Kanban board columns, left-to-right in workflow order (Backlog → Done) — the natural
    /// reading direction for a board (todo on the left, done on the right). [Story 2.3 redesign]</summary>
    private static readonly (string CssClass, string Label)[] BoardColumns =
    {
        ("pending", "Backlog"),
        ("ready", "Ready for dev"),
        ("active", "In progress"),
        ("review", "In review"),
        ("done", "Done"),
    };

    /// <summary>Per-stage story counts over the tracked <c>development_status</c> (stories only), in
    /// <see cref="StageOrder"/>. Shared by the page summary and the dashboard widget. [Story 2.3]</summary>
    public static IReadOnlyList<(string Label, int Count, string CssClass)> StoryStageCounts(SprintStatus sprint)
    {
        var stories = sprint.Entries.Where(e => e.Kind == SprintEntryKind.Story).ToList();
        return StageOrder
            .Select(s => (s.Label, Count: stories.Count(st => StatusStyles.ForSprint(st.Status) == s.CssClass), s.CssClass))
            .ToList();
    }

    public static string RenderIndex(SprintStatus sprint, EpicsModel? epics, SiteNav nav, CommandCatalog commands)
    {
        var outputPath = SiteNav.SprintOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — sprint.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Sprint Status — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Sprint delivery status for {nav.SiteTitle} — a board of every epic and story by lifecycle stage."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Sprint Status", null) }));

        var epicCount = sprint.Entries.Count(e => e.Kind == SprintEntryKind.Epic);
        var storyCount = sprint.Entries.Count(e => e.Kind == SprintEntryKind.Story);

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Sprint Status</h1>\n");
        // Name the source explicitly so this reads as the tracking-file view, distinct from the dashboard's
        // derived Now & Next (Story 1.5 truthfulness — never two panels silently contradicting).
        var subtitle = $"{PathUtil.Html(nav.SiteTitle)} &middot; {epicCount} {Charts.Plural(epicCount, "epic", "epics")} &middot; {storyCount} {Charts.Plural(storyCount, "story", "stories")} &middot; from sprint-status.yaml";
        sb.Append($"  <div class=\"doc-subtitle\">{subtitle}</div>\n");
        if (sprint.LastUpdated is { Length: > 0 } lu)
        {
            sb.Append($"  <div class=\"meta-pills\"><span class=\"pill\">Updated {PathUtil.Html(lu)}</span></div>\n");
        }
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\" class=\"sprint-page\">\n\n");

        // Standard sprint-lifecycle command buttons (omitted individually when the module doesn't expose them).
        sb.Append(BmadCommands.RenderCommandBar("Sprint commands", new[]
        {
            commands.Command("sprint-planning"),
            commands.Command("sprint-status"),
            commands.Command("correct-course"),
            commands.Command("retrospective"),
        }));

        AppendLifecycleSummary(sb, sprint);
        AppendBoardToggle(sb, sprint, epics, prefix);
        AppendActionItems(sb, sprint);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The Kanban board: five lifecycle columns (Backlog → Done) of story cards. Story entries only —
    /// epics/retrospectives are not cards. When <paramref name="capPerColumn"/> is set, a column with more than
    /// that many stories shows the first N cards then a "+K more" link to <paramref name="moreHref"/> (the home
    /// board caps; the sprint page does not). Columns render even when empty — an empty "Done" column is
    /// meaningful on a board. Reused by the home Now &amp; Next and the sprint page. [Story 2.3 redesign]</summary>
    public static string RenderBoard(SprintStatus sprint, EpicsModel? epics, int? capPerColumn = null, string? moreHref = null, string prefix = "")
    {
        var stories = sprint.Entries.Where(e => e.Kind == SprintEntryKind.Story).ToList();
        var byColumn = BoardColumns.ToDictionary(c => c.CssClass, _ => new List<SprintEntry>());
        foreach (var s in stories)
        {
            var cls = StatusStyles.ForSprint(s.Status);
            if (byColumn.TryGetValue(cls, out var list)) list.Add(s);
        }

        var sb = new StringBuilder();
        sb.Append("<div class=\"sprint-board\">\n");
        foreach (var (cssClass, label) in BoardColumns)
        {
            var col = byColumn[cssClass];
            sb.Append($"  <section class=\"sprint-lane {cssClass}\" aria-label=\"{PathUtil.Html($"{label}: {col.Count} {Charts.Plural(col.Count, "story", "stories")}")}\">\n");
            sb.Append($"    <div class=\"sprint-lane-head\"><span class=\"sprint-lane-label\">{PathUtil.Html(label)}</span><span class=\"sprint-lane-count\">{col.Count}</span></div>\n");
            sb.Append("    <div class=\"sprint-cards\">\n");

            var shown = capPerColumn is { } cap && col.Count > cap ? col.Take(cap) : col;
            foreach (var story in shown) AppendBoardCard(sb, story, epics, prefix);

            if (capPerColumn is { } c && col.Count > c && moreHref is { Length: > 0 })
            {
                var extra = col.Count - c;
                sb.Append($"      <a class=\"sprint-lane-more\" href=\"{PathUtil.Html(prefix + moreHref)}\">+{extra} more &rarr;</a>\n");
            }

            sb.Append("    </div>\n  </section>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The epic-grouped board view: one swimlane section per epic (file order) with the epic header
    /// (linked, tracked badge, retrospective note) and its stories as status-colored cards in a wrap row.
    /// [Story 2.3 redesign]</summary>
    public static string RenderBoardByEpic(SprintStatus sprint, EpicsModel? epics, string prefix = "")
    {
        var (order, epicEntry, retroEntry, stories) = GroupByEpic(sprint);

        var sb = new StringBuilder();
        foreach (var n in order)
        {
            sb.Append("<section class=\"sprint-epic-lane\">\n");
            sb.Append("  <div class=\"sprint-epic-head\">\n");

            var (epicTitle, epicHref) = ResolveEpic(n, epics);
            if (epicHref is not null)
            {
                sb.Append($"    <a class=\"sprint-epic-title\" href=\"{PathUtil.Html(prefix + epicHref)}\">{epicTitle}</a>\n");
            }
            else
            {
                sb.Append($"    <span class=\"sprint-epic-title\">{epicTitle}</span>\n");
            }
            if (epicEntry.TryGetValue(n, out var ee))
            {
                sb.Append($"    <span class=\"status-badge {StatusStyles.ForSprint(ee.Status)}\">{PathUtil.Html(StatusStyles.SprintLabel(ee.Status))}</span>\n");
            }
            if (retroEntry.TryGetValue(n, out var re))
            {
                sb.Append($"    <span class=\"sprint-retro-note\">Retrospective: {PathUtil.Html(StatusStyles.SprintLabel(re.Status))}</span>\n");
            }
            sb.Append("  </div>\n");

            if (stories[n].Count > 0)
            {
                sb.Append("  <div class=\"sprint-cards-row\">\n");
                foreach (var story in stories[n]) AppendBoardCard(sb, story, epics, prefix);
                sb.Append("  </div>\n");
            }
            sb.Append("</section>\n\n");
        }
        return sb.ToString();
    }

    /// <summary>A pure-CSS toggle (no JS) between the status-column board (default) and the epic-grouped view.
    /// Hidden radios drive sibling <c>.board-view</c> visibility via CSS; the labels are the visible tabs.
    /// [Story 2.3 redesign]</summary>
    private static void AppendBoardToggle(StringBuilder sb, SprintStatus sprint, EpicsModel? epics, string prefix)
    {
        sb.Append("<div class=\"board-tabs\">\n");
        // Radios first so they precede the views as siblings (the CSS `~` selector switches which view shows).
        sb.Append("  <input type=\"radio\" id=\"sv-status\" name=\"sprint-view\" class=\"board-tab-radio\" checked>\n");
        sb.Append("  <input type=\"radio\" id=\"sv-epic\" name=\"sprint-view\" class=\"board-tab-radio\">\n");
        sb.Append("  <div class=\"board-tabbar\">\n");
        sb.Append("    <label for=\"sv-status\" class=\"board-tab\">By status</label>\n");
        sb.Append("    <label for=\"sv-epic\" class=\"board-tab\">By epic</label>\n");
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"board-view board-view-status\">\n");
        sb.Append(RenderBoard(sprint, epics, prefix: prefix));
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"board-view board-view-epic\">\n");
        sb.Append(RenderBoardByEpic(sprint, epics, prefix));
        sb.Append("  </div>\n");
        sb.Append("</div>\n\n");
    }

    /// <summary>The lifecycle-count summary — the same per-stage story counts the home widget shows (one
    /// number). A donut (non-zero legend rows only, Story 1.5 B4). Omitted when no stories are tracked. </summary>
    private static void AppendLifecycleSummary(StringBuilder sb, SprintStatus sprint)
    {
        var counts = StoryStageCounts(sprint);
        var total = counts.Sum(c => c.Count);
        if (total == 0) return;

        var segments = counts.Select(c => (c.Label, c.Count, c.CssClass)).ToList();
        var nonZero = segments.Where(s => s.Count > 0).ToList();
        var done = counts.First(c => c.CssClass == "done").Count;
        var ariaParts = string.Join(", ", nonZero.Select(s => $"{s.Count} {s.Label.ToLowerInvariant()}"));

        sb.Append("<section class=\"dashboard\">\n<div class=\"chart-panel\">\n");
        sb.Append("<h3>Delivery by lifecycle stage</h3>\n<div class=\"donut-and-legend\">\n");
        sb.Append(Charts.Donut(segments, ariaLabel: $"Sprint stories: {ariaParts}", centerText: $"{done}/{total}"));
        sb.Append("<div class=\"donut-legend\">\n");
        foreach (var (label, count, cssClass) in nonZero)
        {
            sb.Append($"  <span><span class=\"swatch {cssClass}\"></span>{PathUtil.Html(label)} ({count})</span>\n");
        }
        sb.Append("</div>\n</div>\n</div>\n</section>\n\n");
    }

    /// <summary>The open retrospective action items (open + in-progress) as status-badged rows. Rendered ONLY
    /// when there is at least one — no empty header when the tracking file carries no action_items. [Story 2.3]</summary>
    private static void AppendActionItems(StringBuilder sb, SprintStatus sprint)
    {
        var open = sprint.OpenActionItems;
        if (open.Count == 0) return;

        sb.Append("<section class=\"sprint-action-items\">\n");
        sb.Append("  <div class=\"section-divider\">Open retrospective action items</div>\n");
        sb.Append("  <ul class=\"sprint-action-list\">\n");
        foreach (var item in open)
        {
            sb.Append("    <li class=\"sprint-action-row\">\n");
            sb.Append($"      <span class=\"sprint-action-text\">{PathUtil.Html(item.Action)}</span>\n");
            if (item.EpicNumber is { } en)
            {
                sb.Append($"      <span class=\"pill\">Epic {en}</span>\n");
            }
            if (item.Owner is { Length: > 0 } owner)
            {
                sb.Append($"      <span class=\"pill\">{PathUtil.Html(owner)}</span>\n");
            }
            sb.Append($"      <span class=\"status-badge {StatusStyles.ForSprint(item.Status)}\">{PathUtil.Html(StatusStyles.SprintLabel(item.Status))}</span>\n");
            sb.Append("    </li>\n");
        }
        sb.Append("  </ul>\n</section>\n\n");
    }

    /// <summary>One board card for a story: the whole card is a link to the story's generated page (real or
    /// placeholder — always exists for a model story), colored by its tracked lifecycle stage. Falls back to a
    /// non-link <c>div</c> only when the yaml key has no matching model story (no page to point at). [Story 2.3]</summary>
    private static void AppendBoardCard(StringBuilder sb, SprintEntry story, EpicsModel? epics, string prefix)
    {
        var (title, href) = ResolveStory(story, epics);
        var cssClass = StatusStyles.ForSprint(story.Status);
        var kicker = story.EpicNumber is { } en ? $"Epic {en}" : "Story";

        if (href is not null)
        {
            sb.Append($"      <a class=\"now-next-card {cssClass}\" href=\"{PathUtil.Html(prefix + href)}\">\n");
            sb.Append($"        <span class=\"now-next-kicker\">{PathUtil.Html(kicker)}</span>\n");
            sb.Append($"        <span class=\"now-next-title\">{title}</span>\n");
            sb.Append("      </a>\n");
        }
        else
        {
            sb.Append($"      <div class=\"now-next-card {cssClass}\">\n");
            sb.Append($"        <span class=\"now-next-kicker\">{PathUtil.Html(kicker)}</span>\n");
            sb.Append($"        <span class=\"now-next-title\">{title}</span>\n");
            sb.Append("      </div>\n");
        }
    }

    /// <summary>Groups the tracked entries by epic number in first-seen (file) order, keeping stories in file
    /// order within each group. Shared by the epic-board view. [Story 2.3]</summary>
    private static (List<int> Order, Dictionary<int, SprintEntry> Epics, Dictionary<int, SprintEntry> Retros, Dictionary<int, List<SprintEntry>> Stories) GroupByEpic(SprintStatus sprint)
    {
        var order = new List<int>();
        var epicEntry = new Dictionary<int, SprintEntry>();
        var retroEntry = new Dictionary<int, SprintEntry>();
        var stories = new Dictionary<int, List<SprintEntry>>();
        foreach (var e in sprint.Entries)
        {
            var n = e.EpicNumber ?? -1;
            if (!stories.ContainsKey(n)) { order.Add(n); stories[n] = new List<SprintEntry>(); }
            switch (e.Kind)
            {
                case SprintEntryKind.Epic: epicEntry[n] = e; break;
                case SprintEntryKind.Retrospective: retroEntry[n] = e; break;
                case SprintEntryKind.Story: stories[n].Add(e); break;
            }
        }
        return (order, epicEntry, retroEntry, stories);
    }

    /// <summary>Resolves an epic's display title + link. Prefers the real title from the parsed epics model
    /// (linking to its generated page); falls back to "Epic N" plain text when the epic isn't in the model —
    /// never a broken link. Returned title is HTML (the model title may carry inline markup). [Story 2.3]</summary>
    private static (string TitleHtml, string? Href) ResolveEpic(int epicNumber, EpicsModel? epics)
    {
        if (epicNumber < 0) return ("Ungrouped", null);
        var epic = epics?.Epics.FirstOrDefault(e => e.Number == epicNumber);
        if (epic is not null)
        {
            return ($"Epic {epic.Number} · {epic.Title}", $"epics/epic-{epic.Number}.html");
        }
        return ($"Epic {epicNumber}", null);
    }

    /// <summary>Resolves a story's display title + link. When the story is in the epics model — drafted OR not
    /// — it links to its generated page via <see cref="StoryEpicLinkifier.StoryPagePath"/>, which always
    /// exists (a placeholder page is emitted for undrafted stories). Only a yaml key with no matching model
    /// story (no page to point at) renders as plain text — never a broken link. [Story 2.3 redesign]</summary>
    private static (string TitleHtml, string? Href) ResolveStory(SprintEntry entry, EpicsModel? epics)
    {
        var id = entry.EpicNumber is { } e && entry.StoryMinor is { } m ? $"{e}.{m}" : null;
        var story = id is not null ? epics?.Epics.SelectMany(ep => ep.Stories).FirstOrDefault(s => s.Id == id) : null;
        if (story is not null)
        {
            var label = $"Story {story.Id} · {story.Title}";
            return (label, StoryEpicLinkifier.StoryPagePath(story.Id));
        }
        return (PathUtil.Html(PrettifySlug(entry.RawKey)), null);
    }

    /// <summary>Human title from a yaml story slug: strip the leading <c>N-M-</c> id prefix, replace dashes
    /// with spaces, title-case — used only when the epics model has no matching story. [Story 2.3]</summary>
    private static string PrettifySlug(string rawKey)
    {
        var withoutPrefix = System.Text.RegularExpressions.Regex.Replace(rawKey, @"^\d+-\d+-", string.Empty);
        var spaced = withoutPrefix.Replace('-', ' ').Trim();
        if (spaced.Length == 0) spaced = rawKey;
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }
}
