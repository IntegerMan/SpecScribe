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

    public static string RenderIndex(SprintStatus sprint, EpicsModel? epics, SiteNav nav, CommandCatalog commands,
        IReadOnlyList<RetroModel>? retros = null)
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

        // One consolidated control row INSIDE main: title/subtitle on the left; the wheel, the view toggle, and
        // the Commands / Retros / flag buttons on the right — the whole chrome in a single row, leaving the
        // board the vertical space. [Story 2.3 polish #5]
        sb.Append("<main id=\"main-content\" class=\"sprint-page\">\n\n");
        sb.Append("<div class=\"sprint-topbar\">\n");
        sb.Append("  <div class=\"sprint-topbar-head\">\n");
        sb.Append("    <h1>Sprint Status</h1>\n");
        sb.Append($"    <div class=\"doc-subtitle\">{subtitle}</div>\n");
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"sprint-topbar-aside\">\n");
        sb.Append(RenderProgressWheel(sprint));
        sb.Append(RenderBoardTabs());
        sb.Append(BmadCommands.RenderCommandMenu("Commands", new (string?, string)[]
        {
            (commands.Command("sprint-planning"), "Plan the sprint from epics"),
            (commands.Command("sprint-status"), "Summarize status & risks"),
            (commands.Command("correct-course"), "Handle a mid-sprint change"),
            (commands.Command("retrospective"), "Capture lessons after an epic"),
        }));
        AppendRetroButtons(sb, sprint, retros ?? Array.Empty<RetroModel>());
        sb.Append("  </div>\n");
        sb.Append("</div>\n\n");

        // The toggle radios live in the control row above; the views sit here and toggle via CSS :has().
        AppendBoardViews(sb, sprint, epics, prefix);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
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

    /// <summary>The pure-CSS view toggle tabs (radios + labels) for the control row. The radios drive the two
    /// board views via CSS <c>:has()</c> on <c>.sprint-page</c>, so the tabs no longer need to sit next to the
    /// views. [Story 2.3 polish #5]</summary>
    private static string RenderBoardTabs()
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"board-tabs\">");
        sb.Append("<input type=\"radio\" id=\"sv-status\" name=\"sprint-view\" class=\"board-tab-radio\" checked>");
        sb.Append("<input type=\"radio\" id=\"sv-epic\" name=\"sprint-view\" class=\"board-tab-radio\">");
        sb.Append("<div class=\"board-tabbar\">");
        sb.Append("<label for=\"sv-status\" class=\"board-tab\">By status</label>");
        sb.Append("<label for=\"sv-epic\" class=\"board-tab\">By epic</label>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static void AppendBoardViews(StringBuilder sb, SprintStatus sprint, EpicsModel? epics, string prefix)
    {
        sb.Append("<div class=\"board-view board-view-status\">\n");
        sb.Append(RenderBoard(sprint, epics, prefix: prefix));
        sb.Append("</div>\n");
        sb.Append("<div class=\"board-view board-view-epic\">\n");
        sb.Append(RenderBoardByEpic(sprint, epics, prefix));
        sb.Append("</div>\n\n");
    }

    /// <summary>The control-row buttons that link to the retrospectives index and the open-action-items page.
    /// "Retros" shows when any retro exists (rich tooltip: count + latest); the flag "⚑ N" shows only when there
    /// are open action items. Both are plain links to real pages (no modal). [Story 2.3 polish #5]</summary>
    private static void AppendRetroButtons(StringBuilder sb, SprintStatus sprint, IReadOnlyList<RetroModel> retros)
    {
        if (retros.Count > 0)
        {
            var latest = retros[^1]; // ordered epic-then-name; the last is the newest.
            var tip = $"{retros.Count} {Charts.Plural(retros.Count, "retrospective", "retrospectives")}";
            if (latest.DateText is { Length: > 0 } d) tip += $"\nLatest: {PathUtil.StripHtmlTags(latest.Title)} ({d})";
            sb.Append($"<a class=\"cmd-menu-toggle js-tip\" href=\"{SiteNav.RetrosOutputPath}\" data-tip=\"{PathUtil.Html(tip)}\">Retros</a>");
        }

        var open = sprint.OpenActionItems.Count;
        if (open > 0)
        {
            var tip = $"{open} open action {Charts.Plural(open, "item", "items")} — review & resolve";
            sb.Append($"<a class=\"sprint-flag js-tip\" href=\"{SiteNav.ActionItemsOutputPath}\" data-tip=\"{PathUtil.Html(tip)}\" aria-label=\"{open} open action items\">⚑ {open}</a>");
        }
    }

    /// <summary>A compact status progress wheel: the lifecycle segments as a small donut with NO center number
    /// (the sibling "N / M done" label carries it — a number crammed into a tiny ring just reads as noise) and a
    /// hover tooltip of the breakdown. Shared by the sprint page's top strip and the home Now &amp; Next header.
    /// Returns empty when no stories are tracked. [Story 2.3 redesign]</summary>
    public static string RenderProgressWheel(SprintStatus sprint)
    {
        var counts = StoryStageCounts(sprint);
        var total = counts.Sum(c => c.Count);
        if (total == 0) return string.Empty;

        var segments = counts.Select(c => (c.Label, c.Count, c.CssClass)).ToList();
        var nonZero = segments.Where(s => s.Count > 0).ToList();
        var done = counts.First(c => c.CssClass == "done").Count;
        var ariaParts = string.Join(", ", nonZero.Select(s => $"{s.Count} {s.Label.ToLowerInvariant()}"));

        // Rich single tooltip via the body-level (never-clipped) js-tip node; suppress the donut's per-segment
        // <title> so the tiny wheel shows just this one clean breakdown. [Story 2.3 polish]
        var tip = "Sprint delivery\n" + string.Join("\n", nonZero.Select(s => $"{s.Label}: {s.Count}"));
        var sb = new StringBuilder();
        sb.Append($"<div class=\"sprint-wheel js-tip\" data-tip=\"{PathUtil.Html(tip)}\">");
        sb.Append(Charts.Donut(segments, size: 46, showCenterText: false, segmentTitles: false, ariaLabel: $"Sprint delivery: {ariaParts}"));
        sb.Append($"<span class=\"sprint-wheel-label\">{done} / {total} done</span>");
        sb.Append("</div>");
        return sb.ToString();
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

        // Rich, non-clipped hover tooltip (body-level js-tip): epic + story names + high-level task info.
        var dataTip = PathUtil.Html(BuildCardTip(entry, story, epics));
        var tag = href is not null ? "a" : "div";
        var hrefAttr = href is not null ? $" href=\"{PathUtil.Html(prefix + href)}\"" : string.Empty;
        // Unmatched entries (no model story, so no href) still need to be keyboard-reachable — they're
        // exactly the stale/unmatched cases a maintainer most wants to inspect. [Story 2.3 review]
        var focusAttr = href is null ? " tabindex=\"0\" role=\"group\"" : string.Empty;

        sb.Append($"      <{tag} class=\"sprint-card js-tip {cssClass}\"{hrefAttr}{focusAttr} data-tip=\"{dataTip}\">\n");
        sb.Append("        <div class=\"sprint-card-head\">\n");
        sb.Append($"          <span class=\"sprint-card-id\">Story {PathUtil.Html(id)}</span>\n");
        sb.Append("        </div>\n");
        sb.Append($"        <span class=\"sprint-card-title\">{title}</span>\n");

        // A hairline task-completion bar at the card bottom, only when the story has a task plan (mirrors the
        // TaskBadge gate). Colors + fraction reuse the shared progress vocabulary. [redesign]
        if (story is { TasksTotal: > 0 })
        {
            AppendCardProgress(sb, story.TasksDone, story.TasksTotal);
        }

        sb.Append($"      </{tag}>\n");
    }

    /// <summary>The rich card tooltip text (plain, <c>\n</c>-separated for the js-tip node): epic name, story
    /// name, and high-level task progress. [Story 2.3 polish]</summary>
    private static string BuildCardTip(SprintEntry entry, StoryInfo? story, EpicsModel? epics)
    {
        var lines = new List<string>();
        if (entry.EpicNumber is { } en)
        {
            var epicTitle = epics?.Epics.FirstOrDefault(e => e.Number == en)?.Title;
            lines.Add(epicTitle is { Length: > 0 } et ? $"Epic {en}: {PathUtil.StripHtmlTags(et)}" : $"Epic {en}");
        }
        var idText = story?.Id ?? (entry.EpicNumber is { } e2 && entry.StoryMinor is { } m2 ? $"{e2}.{m2}" : entry.RawKey);
        var storyTitle = story is not null ? PathUtil.StripHtmlTags(story.Title) : PrettifySlug(entry.RawKey);
        lines.Add($"Story {idText}: {storyTitle}");
        lines.Add(story is { TasksTotal: > 0 }
            ? $"{story.TasksDone} of {story.TasksTotal} {Charts.Plural(story.TasksTotal, "task", "tasks")} done"
            : "No task plan yet");
        return string.Join("\n", lines);
    }

    /// <summary>The thin per-card task-completion bar: a <c>role="progressbar"</c> track + a
    /// done/partial/empty-colored fill, labeled via <c>aria-label</c> (no separate tooltip — the card's own
    /// body-level <c>js-tip</c> already surfaces task progress). [Story 2.3 redesign]</summary>
    private static void AppendCardProgress(StringBuilder sb, int done, int total)
    {
        var pct = total > 0 ? Math.Clamp((int)Math.Round((double)done / total * 100), 0, 100) : 0;
        var fill = pct >= 100 ? "done" : pct > 0 ? "partial" : "empty";
        var aria = $"{done} of {total} {Charts.Plural(total, "task", "tasks")} done";
        sb.Append($"        <span class=\"sprint-card-progress\" role=\"progressbar\" aria-valuenow=\"{pct}\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-label=\"{PathUtil.Html(aria)}\">");
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
