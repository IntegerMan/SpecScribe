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
        var updated = sprint.LastUpdated is { Length: > 0 } lu ? $" &middot; updated {PathUtil.Html(lu)}" : string.Empty;
        var subtitle = $"{PathUtil.Html(nav.SiteTitle)} &middot; {epicCount} {Charts.Plural(epicCount, "epic", "epics")} &middot; {storyCount} {Charts.Plural(storyCount, "story", "stories")} &middot; from sprint-status.yaml{updated}";

        // One compact top strip INSIDE main so the header shares the board's centered column (alignment) and
        // the whole chrome costs a single short row: title + subtitle on the left; a compact donut and a
        // header "Sprint commands" popout (descriptions tucked behind it) on the right — leaving the board the
        // vertical space. [Story 2.3 redesign]
        sb.Append("<main id=\"main-content\" class=\"sprint-page\">\n\n");
        sb.Append("<div class=\"sprint-topbar\">\n");
        sb.Append("  <div class=\"sprint-topbar-head\">\n");
        sb.Append("    <h1>Sprint Status</h1>\n");
        sb.Append($"    <div class=\"doc-subtitle\">{subtitle}</div>\n");
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"sprint-topbar-aside\">\n");
        sb.Append(RenderCompactDonut(sprint));
        sb.Append(BmadCommands.RenderCommandMenu("Sprint commands", new (string?, string)[]
        {
            (commands.Command("sprint-planning"), "Plan the sprint from epics"),
            (commands.Command("sprint-status"), "Summarize status & risks"),
            (commands.Command("correct-course"), "Handle a mid-sprint change"),
            (commands.Command("retrospective"), "Capture lessons after an epic"),
        }));
        sb.Append("  </div>\n");
        sb.Append("</div>\n\n");

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
                sb.Append($"    {StatusStyles.Badge(StatusStyles.ForSprint(ee.Status), StatusStyles.SprintLabel(ee.Status))}\n");
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

    /// <summary>The compact lifecycle donut for the top strip — an at-a-glance done/total overview (the board's
    /// column headers already carry the per-stage counts, so no legend here). Returns empty when no stories are
    /// tracked. [Story 2.3 redesign]</summary>
    private static string RenderCompactDonut(SprintStatus sprint)
    {
        var counts = StoryStageCounts(sprint);
        var total = counts.Sum(c => c.Count);
        if (total == 0) return string.Empty;

        var segments = counts.Select(c => (c.Label, c.Count, c.CssClass)).ToList();
        var nonZero = segments.Where(s => s.Count > 0).ToList();
        var done = counts.First(c => c.CssClass == "done").Count;
        var ariaParts = string.Join(", ", nonZero.Select(s => $"{s.Count} {s.Label.ToLowerInvariant()}"));

        var sb = new StringBuilder();
        sb.Append("<div class=\"sprint-topbar-donut\" data-tooltip=\"" + PathUtil.Html($"Sprint delivery: {ariaParts}") + "\">");
        sb.Append(Charts.Donut(segments, size: 58, ariaLabel: $"Sprint delivery: {ariaParts}", centerText: $"{done}/{total}"));
        sb.Append($"<span class=\"sprint-topbar-donut-label\">{done} / {total} done</span>");
        sb.Append("</div>");
        return sb.ToString();
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
            sb.Append($"      {StatusStyles.Badge(StatusStyles.ForSprint(item.Status), StatusStyles.SprintLabel(item.Status))}\n");
            sb.Append("    </li>\n");
        }
        sb.Append("  </ul>\n</section>\n\n");
    }

    /// <summary>One board card for a story: the whole card is a link to the story's generated page (real or
    /// placeholder — always exists for a model story), colored by its tracked lifecycle stage. Falls back to a
    /// non-link <c>div</c> only when the yaml key has no matching model story (no page to point at). [Story 2.3]</summary>
    private static void AppendBoardCard(StringBuilder sb, SprintEntry entry, EpicsModel? epics, string prefix)
    {
        var (title, href, story) = ResolveStory(entry, epics);
        var cssClass = StatusStyles.ForSprint(entry.Status);
        var id = story?.Id
            ?? (entry.EpicNumber is { } e && entry.StoryMinor is { } m ? $"{e}.{m}" : entry.RawKey);

        var tag = href is not null ? "a" : "div";
        var hrefAttr = href is not null ? $" href=\"{PathUtil.Html(prefix + href)}\"" : string.Empty;

        sb.Append($"      <{tag} class=\"sprint-card {cssClass}\"{hrefAttr}>\n");
        sb.Append("        <div class=\"sprint-card-head\">\n");
        sb.Append($"          <span class=\"sprint-card-id\">{PathUtil.Html(id)}</span>\n");
        if (entry.EpicNumber is { } en)
        {
            sb.Append($"          <span class=\"sprint-card-epic\" data-tooltip=\"Epic {en}\" aria-label=\"Epic {en}\">E{en}</span>\n");
        }
        sb.Append("        </div>\n");
        sb.Append($"        <span class=\"sprint-card-title\">{title}</span>\n");

        // A hairline task-completion bar at the card bottom, only when the story has a task plan (mirrors the
        // TaskBadge gate). Colors + fraction reuse the shared progress vocabulary; the tooltip is CSS-only. [redesign]
        if (story is { TasksTotal: > 0 })
        {
            AppendCardProgress(sb, story.TasksDone, story.TasksTotal);
        }

        sb.Append($"      </{tag}>\n");
    }

    /// <summary>The thin per-card task-completion bar: a <c>role="progressbar"</c> track + a
    /// done/partial/empty-colored fill, with a CSS-only <c>data-tooltip</c> reading "N of M tasks done (P%)".
    /// [Story 2.3 redesign]</summary>
    private static void AppendCardProgress(StringBuilder sb, int done, int total)
    {
        var pct = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;
        var fill = pct >= 100 ? "done" : pct > 0 ? "partial" : "empty";
        var aria = $"{done} of {total} {Charts.Plural(total, "task", "tasks")} done";
        sb.Append($"        <span class=\"sprint-card-progress\" role=\"progressbar\" aria-valuenow=\"{pct}\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-label=\"{PathUtil.Html(aria)}\" data-tooltip=\"{PathUtil.Html($"{aria} ({pct}%)")}\">");
        sb.Append($"<span class=\"sprint-card-progress-fill {fill}\" style=\"width:{pct}%\"></span></span>\n");
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
    private static (string TitleHtml, string? Href, StoryInfo? Story) ResolveStory(SprintEntry entry, EpicsModel? epics)
    {
        var id = entry.EpicNumber is { } e && entry.StoryMinor is { } m ? $"{e}.{m}" : null;
        var story = id is not null ? epics?.Epics.SelectMany(ep => ep.Stories).FirstOrDefault(s => s.Id == id) : null;
        if (story is not null)
        {
            // Bare title — the id and epic are shown as their own card elements now. [Story 2.3 redesign]
            return (story.Title, StoryEpicLinkifier.StoryPagePath(story.Id), story);
        }
        return (PathUtil.Html(PrettifySlug(entry.RawKey)), null, null);
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
