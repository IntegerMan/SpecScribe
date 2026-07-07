using System.Text;

namespace SpecScribe;

/// <summary>Renders the sprint status page from <c>sprint-status.yaml</c>: a lifecycle-count summary, every
/// epic with its tracked status badge, its stories grouped underneath with lifecycle badges + resolvable
/// links, and an open retrospective action-items section (only when non-empty). Mirrors the page shell of
/// <see cref="RequirementsTemplater"/> — one <c>&lt;main id="main-content"&gt;</c>, the shared nav/breadcrumb/
/// footer. The view of the authoritative <em>tracking</em> ledger, kept explicitly labeled as coming from the
/// yaml so it never reads as contradicting the derived Now &amp; Next panel (Story 1.5 truthfulness).
/// [Story 2.3 Task 3]</summary>
public static class SprintTemplater
{
    /// <summary>The development lifecycle stages in a fixed display order (done → … → backlog), each paired
    /// with its shared status css class and human label. Story counts roll up over these; the sprint page and
    /// the home widget both iterate this list so they speak one vocabulary and one number. [Story 2.3]</summary>
    private static readonly (string CssClass, string Label)[] StageOrder =
    {
        ("done", "Done"),
        ("review", "In review"),
        ("active", "In progress"),
        ("ready", "Ready for dev"),
        ("pending", "Backlog"),
    };

    /// <summary>Per-stage story counts over the tracked <c>development_status</c> (stories only — epics and
    /// retrospectives are not lifecycle-counted), in <see cref="StageOrder"/>. Shared by the page summary and
    /// the dashboard widget so both "speak one number". [Story 2.3 Task 3/4]</summary>
    public static IReadOnlyList<(string Label, int Count, string CssClass)> StoryStageCounts(SprintStatus sprint)
    {
        var stories = sprint.Entries.Where(e => e.Kind == SprintEntryKind.Story).ToList();
        return StageOrder
            .Select(s => (s.Label, Count: stories.Count(st => StatusStyles.ForSprint(st.Status) == s.CssClass), s.CssClass))
            .ToList();
    }

    /// <summary>The in-progress and in-review stories, in file order — "what is in progress" for the page
    /// summary and the widget. Each carries a resolved link (story page when it exists, else null). [Story 2.3]</summary>
    public static IReadOnlyList<(SprintEntry Entry, string TitleHtml, string? Href)> InProgressStories(SprintStatus sprint, EpicsModel? epics)
        => sprint.Entries
            .Where(e => e.Kind == SprintEntryKind.Story && StatusStyles.ForSprint(e.Status) is "active" or "review")
            .Select(e =>
            {
                var (title, href) = ResolveStory(e, epics);
                return (e, title, href);
            })
            .ToList();

    public static string RenderIndex(SprintStatus sprint, EpicsModel? epics, SiteNav nav)
    {
        var outputPath = SiteNav.SprintOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — sprint.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Sprint Status — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Sprint delivery status for {nav.SiteTitle} — every epic and story with its tracked lifecycle stage."));
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

        AppendLifecycleSummary(sb, sprint);
        AppendEpicGroups(sb, sprint, epics, prefix);
        AppendActionItems(sb, sprint);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The lifecycle-count summary that leads the page — the same per-stage story counts the home
    /// widget shows, so page and widget speak one number. A donut (non-zero legend rows only, per Story 1.5
    /// B4) reusing the shared dashboard/chart classes. [Story 2.3 Task 3]</summary>
    private static void AppendLifecycleSummary(StringBuilder sb, SprintStatus sprint)
    {
        var counts = StoryStageCounts(sprint);
        var total = counts.Sum(c => c.Count);
        if (total == 0) return; // no stories tracked yet — no summary (the epic list still renders).

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

    /// <summary>Every epic (in file order) as a section: the epic header with its tracked status badge + link
    /// and any retrospective note, then its stories as rows with lifecycle badges + resolvable links. Stories
    /// with no generated page render as plain text — never a broken link. [Story 2.3 Task 3]</summary>
    private static void AppendEpicGroups(StringBuilder sb, SprintStatus sprint, EpicsModel? epics, string prefix)
    {
        // Group by epic number in first-seen (file) order, keeping stories in file order within each group.
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

        foreach (var n in order)
        {
            sb.Append("<section class=\"sprint-epic\">\n");
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
                sb.Append("  <ul class=\"sprint-story-list\">\n");
                foreach (var story in stories[n])
                {
                    var (title, href) = ResolveStory(story, epics);
                    sb.Append("    <li class=\"sprint-story-row\">\n");
                    if (href is not null)
                    {
                        sb.Append($"      <a class=\"sprint-story-title\" href=\"{PathUtil.Html(prefix + href)}\">{title}</a>\n");
                    }
                    else
                    {
                        sb.Append($"      <span class=\"sprint-story-title\">{title}</span>\n");
                    }
                    sb.Append($"      <span class=\"status-badge {StatusStyles.ForSprint(story.Status)}\">{PathUtil.Html(StatusStyles.SprintLabel(story.Status))}</span>\n");
                    sb.Append("    </li>\n");
                }
                sb.Append("  </ul>\n");
            }

            sb.Append("</section>\n\n");
        }
    }

    /// <summary>The open retrospective action items (open + in-progress) as status-badged rows. Rendered ONLY
    /// when there is at least one — no empty header when the tracking file carries no action_items (the live
    /// file today), which is the graceful-degradation path. [Story 2.3 Task 3]</summary>
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

    /// <summary>Resolves an epic's display title + link. Prefers the real title from the parsed epics model
    /// (linking to its generated page); falls back to "Epic N" plain text when the epic isn't in the model —
    /// never a broken link. Returned title is HTML (the model title may carry inline markup). [Story 2.3 Task 3]</summary>
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

    /// <summary>Resolves a story's display title + link. Prefers the real title from the epics model, linking
    /// to its generated story page when one exists (<see cref="StoryInfo.ArtifactOutputPath"/>); otherwise
    /// prettifies the yaml slug and renders plain text — never a broken link. [Story 2.3 Task 3]</summary>
    private static (string TitleHtml, string? Href) ResolveStory(SprintEntry entry, EpicsModel? epics)
    {
        var id = entry.EpicNumber is { } e && entry.StoryMinor is { } m ? $"{e}.{m}" : null;
        var story = id is not null ? epics?.Epics.SelectMany(ep => ep.Stories).FirstOrDefault(s => s.Id == id) : null;
        if (story is not null)
        {
            var label = $"Story {story.Id} · {story.Title}";
            return (label, story.ArtifactOutputPath);
        }
        return (PathUtil.Html(PrettifySlug(entry.RawKey)), null);
    }

    /// <summary>Human title from a yaml story slug: strip the leading <c>N-M-</c> id prefix, replace dashes
    /// with spaces, title-case — used only when the epics model has no matching story. [Story 2.3 Task 3]</summary>
    private static string PrettifySlug(string rawKey)
    {
        var withoutPrefix = System.Text.RegularExpressions.Regex.Replace(rawKey, @"^\d+-\d+-", string.Empty);
        var spaced = withoutPrefix.Replace('-', ' ').Trim();
        if (spaced.Length == 0) spaced = rawKey;
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }
}
