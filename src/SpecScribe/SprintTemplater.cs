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
    /// <summary>The Kanban board columns, left-to-right in workflow order (Backlog → Done), then Retired,
    /// then Unrecognized for present-but-unmapped ledger values. Tallies mirror
    /// <see cref="ProjectCounts.TrackedStageOrder"/>. [Story 2.3 redesign; Story 8.2]</summary>
    private static readonly (string CssClass, string Label)[] BoardColumns =
    {
        ("pending", "Backlog"),
        ("ready", "Ready for dev"),
        ("active", "In progress"),
        ("review", "In review"),
        ("done", "Done"),
        ("retired", "Retired"),
        ("unrecognized", "Unrecognized"),
    };

    /// <summary>Column-specific empty-lane guidance keyed on <see cref="BoardColumns"/> cssClass — designed
    /// empty states, not bare blank columns. [Story 8.6; UX-DR9]</summary>
    private static string EmptyLaneCopy(string cssClass) => cssClass switch
    {
        "pending" => "No cards in backlog",
        "ready" => "Nothing ready to pick up — draft or refine the next story.",
        "active" => "Nothing in progress — pick from Ready.",
        "review" => "Nothing awaiting review.",
        "done" => "Nothing finished yet.",
        "retired" => "Nothing retired from the plan.",
        _ => "Nothing here yet.",
    };

    /// <summary>Empty-lane HTML: Ready (and any lane whose copy already implies drafting) carries an
    /// <see cref="BmadCommands.InlineGuidance"/> create-story badge when an undrafted target + catalog
    /// allow; otherwise the designed 8.6 plain copy (HTML-escaped). Target selection matches
    /// <see cref="BmadCommands"/> ForProject (drafted/ready/active epics only) so Home and the board
    /// never disagree on the next draft id. Never a wrong badge. [Story 9.8]</summary>
    private static string EmptyLaneHtml(string cssClass, EpicsModel? epics, CommandCatalog? commands)
    {
        if (cssClass == "ready" && commands is not null && epics is not null)
        {
            var epicNeedingStory = epics.Epics.FirstOrDefault(e =>
            {
                var cls = StatusStyles.ForEpicWithRetrospective(e);
                return (cls is "drafted" or "ready" or "active")
                    && e.Stories.Any(s => s.ArtifactOutputPath is null);
            });
            if (epicNeedingStory is not null)
            {
                var next = epicNeedingStory.Stories.First(s => s.ArtifactOutputPath is null);
                return BmadCommands.InlineGuidance(
                    commands.Command("create-story", next.Id),
                    "Nothing ready to pick up — draft the next story with",
                    PathUtil.Html(EmptyLaneCopy("ready")));
            }
        }

        return PathUtil.Html(EmptyLaneCopy(cssClass));
    }

    /// <summary>Per-stage story counts from the portal-wide ledger (tracked tally). Shared by the page summary
    /// and the dashboard widget — one computation in <see cref="ProjectCounts.Build"/>, consumed everywhere.
    /// [Story 2.3; Story 8.3]</summary>
    public static IReadOnlyList<(string Label, int Count, string CssClass)> StoryStageCounts(ProjectCounts counts) =>
        counts.TrackedStoryStages.Select(s => (s.Label, s.Count, s.CssClass)).ToList();

    /// <summary>Convenience overload for call sites that only have a sprint — builds a ephemeral ledger from
    /// the yaml alone so tracked stage counts stay defined. Prefer the <see cref="ProjectCounts"/> overload
    /// when the shared generation ledger is available. [Story 8.3]</summary>
    public static IReadOnlyList<(string Label, int Count, string CssClass)> StoryStageCounts(SprintStatus sprint) =>
        StoryStageCounts(ProjectCounts.Build(ProgressModel.Empty, sprint, WorkInventory.Empty));

    public static string RenderIndex(SprintStatus sprint, EpicsModel? epics, SiteNav nav, CommandCatalog commands,
        IReadOnlyList<RetroModel>? retros = null, ProjectCounts? counts = null, UnplannedWorkGeometry? unplanned = null)
    {
        var ledger = counts ?? ProjectCounts.Build(ProgressModel.Empty, sprint, WorkInventory.Empty);
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

        var epicCount = ledger.EpicsTracked;
        var storyCount = ledger.StoriesTracked;
        var updated = sprint.LastUpdated is { Length: > 0 } lu ? $" &middot; updated {PathUtil.Html(lu)}" : string.Empty;
        var subtitle = $"{PathUtil.Html(nav.SiteTitle)} &middot; {epicCount} {Charts.Plural(epicCount, "epic", "epics")} &middot; {storyCount} {Charts.Plural(storyCount, "story", "stories")} &middot; from sprint-status.yaml{updated}";

        // One consolidated control row INSIDE main: title/subtitle on the left; the wheel, the view toggle, and
        // the Commands / Retros / flag buttons on the right — the whole chrome in a single row, leaving the
        // board the vertical space. [Story 2.3 polish #5]
        sb.Append("<main id=\"main-content\" class=\"sprint-page\">\n\n");
        sb.Append("<div class=\"sprint-topbar\">\n");
        sb.Append("  <div class=\"sprint-topbar-head\">\n");
        sb.Append("    <h1>Sprint Status");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h1>\n");
        sb.Append($"    <div class=\"doc-subtitle\">{subtitle}</div>\n");
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"sprint-topbar-aside\">\n");
        sb.Append(RenderProgressWheel(ledger));
        sb.Append(RenderBoardTabs());
        sb.Append(BmadCommands.RenderCommandMenu("Commands", new (string?, string)[]
        {
            (commands.Command("sprint-planning"), "Plan the sprint from epics"),
            (commands.Command("sprint-status"), "Summarize status & risks"),
            (commands.Command("correct-course"), "Handle a mid-sprint change"),
            (commands.Command("retrospective"), "Capture lessons after an epic"),
        }));
        AppendRetroButtons(sb, ledger, retros ?? Array.Empty<RetroModel>());
        sb.Append("  </div>\n");
        sb.Append("</div>\n\n");

        // The toggle radios live in the control row above; the views sit here and toggle via CSS :has().
        AppendBoardViews(sb, sprint, epics, prefix, commands, unplanned);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Epics that should be selected by default on sprint boards: any epic with ≥1 story in
    /// <c>in-progress</c>/<c>review</c>/<c>done</c> whose yaml <c>epic-N-retrospective</c> is not <c>done</c>;
    /// if none, the first <c>epic-N</c> in file order whose status is not <c>done</c>. Yaml ledger only.
    /// [spec-sprint-epic-filter-and-home-layout]</summary>
    public static IReadOnlyList<int> ActiveEpicNumbers(SprintStatus sprint)
    {
        var (order, epicEntry, retroEntry, stories) = GroupByEpic(sprint);
        var active = new List<int>();
        foreach (var n in order)
        {
            if (n < 0) continue;
            if (RetroIsDone(retroEntry, n)) continue;
            if (!stories.TryGetValue(n, out var list)) continue;
            if (list.Any(IsActiveStoryStatus)) active.Add(n);
        }
        if (active.Count > 0) return active;

        foreach (var n in order)
        {
            if (n < 0) continue;
            if (!epicEntry.TryGetValue(n, out var ee)) continue;
            if (ee.Status.Equals("done", StringComparison.OrdinalIgnoreCase)) continue;
            if (!stories.TryGetValue(n, out var list) || list.Count == 0) continue;
            return new[] { n };
        }
        return Array.Empty<int>();
    }

    private static bool RetroIsDone(Dictionary<int, SprintEntry> retros, int epicNumber) =>
        retros.TryGetValue(epicNumber, out var re)
        && re.Status.Equals("done", StringComparison.OrdinalIgnoreCase);

    private static bool IsActiveStoryStatus(SprintEntry story)
    {
        var n = story.Status.Trim();
        return n.Equals("in-progress", StringComparison.OrdinalIgnoreCase)
            || n.Equals("review", StringComparison.OrdinalIgnoreCase)
            || n.Equals("done", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The Kanban board: lifecycle columns (Backlog → Done), then Retired, then Unrecognized, then
    /// an Unplanned lane when <paramref name="unplanned"/> has members (Story 9.12). Story entries only for
    /// lifecycle lanes — epics/retrospectives are not cards. When <paramref name="capPerColumn"/> is set, the
    /// cap applies <em>after</em> the default epic filter (active epics). Non-default and overflow cards stay
    /// in the DOM as <c>hidden</c> so the progressive epic selector can reveal them. Core columns (Backlog →
    /// Done) render even when empty. Unplanned is never filtered away by epic multi-select.
    /// <paramref name="commands"/> enables Ready-lane InlineGuidance when an undrafted target is knowable.
    /// [Story 2.3 redesign; spec-sprint-epic-filter-and-home-layout; Story 9.8; Story 9.12]</summary>
    public static string RenderBoard(
        SprintStatus sprint,
        EpicsModel? epics,
        int? capPerColumn = null,
        string? moreHref = null,
        string prefix = "",
        bool wrapWithEpicFilter = true,
        CommandCatalog? commands = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        var defaultEpics = ActiveEpicNumbers(sprint);
        var grouped = GroupByEpic(sprint);
        var epicIdsWithStories = grouped.Order
            .Where(n => n >= 0 && grouped.Stories.TryGetValue(n, out var list) && list.Count > 0)
            .ToList();
        // When no active/fallback epic remains (e.g. every epic-N is done), default to all epics so the board
        // is not an unexplained blank — selector still lets the user narrow. [review patch]
        var effectiveDefaults = defaultEpics.Count > 0 ? defaultEpics : epicIdsWithStories;
        var defaultSet = effectiveDefaults.ToHashSet();
        var filtering = defaultSet.Count > 0;

        var stories = sprint.Entries.Where(e => e.Kind == SprintEntryKind.Story).ToList();
        var byColumn = BoardColumns.ToDictionary(c => c.CssClass, _ => new List<SprintEntry>());
        foreach (var s in stories)
        {
            var cls = StatusStyles.ForSprint(s.Status);
            if (byColumn.TryGetValue(cls, out var list)) list.Add(s);
        }

        // Core lanes always show; retired/unrecognized only when they hold cards (width + noise).
        var visible = BoardColumns
            .Where(c => byColumn[c.CssClass].Count > 0 || c.CssClass is not ("retired" or "unrecognized"))
            .ToList();

        var unplannedGeo = unplanned ?? UnplannedWorkGeometry.Empty;
        var unplannedMembers = unplannedGeo.UnplannedSet;
        var laneCount = visible.Count + (unplannedMembers.Count > 0 ? 1 : 0);

        var sb = new StringBuilder();
        if (wrapWithEpicFilter) AppendEpicFilterOpen(sb, sprint, epics, effectiveDefaults, capPerColumn);

        sb.Append($"<div class=\"sprint-board\" style=\"--lane-count: {laneCount}\">\n");
        foreach (var (cssClass, label) in visible)
        {
            var col = byColumn[cssClass];
            var matching = filtering
                ? col.Where(s => s.EpicNumber is { } n && defaultSet.Contains(n)).ToList()
                : col.ToList();
            var other = filtering
                ? col.Where(s => s.EpicNumber is not { } n || !defaultSet.Contains(n)).ToList()
                : new List<SprintEntry>();
            // Cap after filter so home columns stay relevant to the default epic set.
            var shown = capPerColumn is { } cap && matching.Count > cap ? matching.Take(cap).ToList() : matching;
            var overflow = capPerColumn is { } c && matching.Count > c ? matching.Skip(c).ToList() : new List<SprintEntry>();
            var filteredCount = matching.Count;

            var tip = PathUtil.Html(ColumnMeaning(cssClass, label));
            sb.Append($"  <section class=\"sprint-lane {cssClass}\" data-lane-label=\"{PathUtil.Html(label)}\" aria-label=\"{PathUtil.Html($"{label}: {filteredCount} {Charts.Plural(filteredCount, "story", "stories")}")}\">\n");
            sb.Append($"    <div class=\"sprint-lane-head js-tip\" data-tip=\"{tip}\" title=\"{tip}\" tabindex=\"0\"><span class=\"sprint-lane-label\">{PathUtil.Html(label)}</span><span class=\"sprint-lane-count\">{filteredCount}</span></div>\n");
            sb.Append("    <div class=\"sprint-cards\">\n");

            if (col.Count == 0)
            {
                sb.Append($"      <div class=\"sprint-lane-empty\">{EmptyLaneHtml(cssClass, epics, commands)}</div>\n");
            }
            else if (matching.Count == 0)
            {
                // Default filter emptied this lane — filter-specific copy, with other-epic cards kept as hidden.
                sb.Append($"      <div class=\"sprint-lane-empty\" data-filter-empty=\"1\">{PathUtil.Html(FilteredEmptyLaneCopy())}</div>\n");
                foreach (var story in other) AppendBoardCard(sb, story, epics, prefix, hidden: true);
            }
            else
            {
                foreach (var story in shown) AppendBoardCard(sb, story, epics, prefix, hidden: false);
                foreach (var story in overflow) AppendBoardCard(sb, story, epics, prefix, hidden: true, capOverflow: true);
                foreach (var story in other) AppendBoardCard(sb, story, epics, prefix, hidden: true);

                if (capPerColumn is { } capLimit && matching.Count > capLimit && moreHref is { Length: > 0 })
                {
                    var extra = matching.Count - capLimit;
                    sb.Append($"      <a class=\"sprint-lane-more\" href=\"{PathUtil.Html(prefix + moreHref)}\">+{extra} more &rarr;</a>\n");
                }
            }

            sb.Append("    </div>\n  </section>\n");
        }

        if (unplannedMembers.Count > 0)
            AppendUnplannedLane(sb, unplannedMembers, prefix, capPerColumn, moreHref);

        sb.Append("</div>\n");
        if (wrapWithEpicFilter) AppendEpicFilterClose(sb);
        return sb.ToString();
    }

    private static string FilteredEmptyLaneCopy() =>
        "No stories from the selected epics in this column.";


    /// <summary>The epic-grouped board view: one swimlane section per epic (file order) with the epic header
    /// (linked, tracked badge, retrospective note) and its stories as status-colored cards in a wrap row.
    /// Non-default epics start <c>hidden</c> for the epic filter default. Trailing Unplanned / Direct work
    /// swimlane when <paramref name="unplanned"/> is non-empty (always visible; not filtered by epic).
    /// [Story 2.3 redesign; spec-sprint-epic-filter; Story 9.12]</summary>
    public static string RenderBoardByEpic(
        SprintStatus sprint,
        EpicsModel? epics,
        string prefix = "",
        bool wrapWithEpicFilter = true,
        UnplannedWorkGeometry? unplanned = null)
    {
        var (order, epicEntry, retroEntry, stories) = GroupByEpic(sprint);
        var defaultEpics = ActiveEpicNumbers(sprint);
        var epicIdsWithStories = order
            .Where(n => n >= 0 && stories.TryGetValue(n, out var list) && list.Count > 0)
            .ToList();
        var effectiveDefaults = defaultEpics.Count > 0 ? defaultEpics : epicIdsWithStories;
        var defaultSet = effectiveDefaults.ToHashSet();

        var sb = new StringBuilder();
        if (wrapWithEpicFilter) AppendEpicFilterOpen(sb, sprint, epics, effectiveDefaults, capPerColumn: null);

        foreach (var n in order)
        {
            // Align with status board: hide non-selected epics whenever a default set exists.
            var laneHidden = n >= 0 && defaultSet.Count > 0 && !defaultSet.Contains(n);
            var hiddenAttr = laneHidden ? " hidden" : string.Empty;
            var epicAttr = n >= 0 ? $" data-epic=\"{n}\"" : string.Empty;
            sb.Append($"<section class=\"sprint-epic-lane\"{epicAttr}{hiddenAttr}>\n");
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
                foreach (var story in stories[n]) AppendBoardCard(sb, story, epics, prefix, hidden: false);
                sb.Append("  </div>\n");
            }
            sb.Append("</section>\n\n");
        }

        var unplannedMembers = (unplanned ?? UnplannedWorkGeometry.Empty).UnplannedSet;
        if (unplannedMembers.Count > 0)
            AppendUnplannedEpicSwimlane(sb, unplannedMembers, prefix);

        if (wrapWithEpicFilter) AppendEpicFilterClose(sb);
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

    private static void AppendBoardViews(
        StringBuilder sb,
        SprintStatus sprint,
        EpicsModel? epics,
        string prefix,
        CommandCatalog commands,
        UnplannedWorkGeometry? unplanned = null)
    {
        // One filter drives both status and by-epic views (CSS :has() swaps which view is visible).
        var defaultEpics = ActiveEpicNumbers(sprint);
        var grouped = GroupByEpic(sprint);
        var epicIdsWithStories = grouped.Order
            .Where(n => n >= 0 && grouped.Stories.TryGetValue(n, out var list) && list.Count > 0)
            .ToList();
        var effectiveDefaults = defaultEpics.Count > 0 ? defaultEpics : epicIdsWithStories;
        AppendEpicFilterOpen(sb, sprint, epics, effectiveDefaults, capPerColumn: null);
        sb.Append("<div class=\"board-view board-view-status\">\n");
        sb.Append(RenderBoard(sprint, epics, prefix: prefix, wrapWithEpicFilter: false, commands: commands, unplanned: unplanned));
        sb.Append("</div>\n");
        sb.Append("<div class=\"board-view board-view-epic\">\n");
        sb.Append(RenderBoardByEpic(sprint, epics, prefix, wrapWithEpicFilter: false, unplanned: unplanned));
        sb.Append("</div>\n");
        AppendEpicFilterClose(sb);
        sb.Append('\n');
    }

    /// <summary>Opens a <c>.sprint-filterable</c> root with epic catalog data attributes (no UI — JS injects
    /// the dropdown). Used by the sprint page and the home board (home places a filter host in the panel
    /// header). [spec-sprint-epic-filter-and-home-layout]</summary>
    public static string OpenEpicFilterable(SprintStatus sprint, EpicsModel? epics, int? capPerColumn = null)
    {
        var defaultEpics = ActiveEpicNumbers(sprint);
        var (order, _, _, stories) = GroupByEpic(sprint);
        var epicIds = order.Where(n => n >= 0 && stories.TryGetValue(n, out var list) && list.Count > 0).ToList();
        var effectiveDefaults = defaultEpics.Count > 0 ? defaultEpics : epicIds;
        var sb = new StringBuilder();
        AppendEpicFilterOpen(sb, sprint, epics, effectiveDefaults, capPerColumn, emitEmptyHint: false);
        return sb.ToString();
    }

    public static string CloseEpicFilterable() => "</div>\n";

    /// <summary>Mount point for the JS-injected epic dropdown in the home board header.</summary>
    public static string EpicFilterHostMarkup =>
        "<div class=\"sprint-epic-filter-host\"></div>";

    public static string EpicFilterEmptyHintMarkup =>
        "<p class=\"sprint-filter-empty\" hidden role=\"status\">Select an epic to show stories on the board.</p>\n";

    /// <summary>Filter root + epic catalog as data attributes. The checkbox UI is JS-injected (like
    /// <c>js-sortable</c> table filters) so no-JS pages never show inert controls — SSR already applies the
    /// default active-epic visibility/cap. [spec-sprint-epic-filter-and-home-layout]</summary>
    private static void AppendEpicFilterOpen(StringBuilder sb, SprintStatus sprint, EpicsModel? epics,
        IReadOnlyList<int> defaultEpics, int? capPerColumn, bool emitEmptyHint = true)
    {
        var (order, _, _, stories) = GroupByEpic(sprint);
        var epicIds = order.Where(n => n >= 0 && stories.TryGetValue(n, out var list) && list.Count > 0).ToList();
        var defaults = string.Join(",", defaultEpics);
        var capAttr = capPerColumn is { } c ? $" data-cap=\"{c}\"" : string.Empty;
        var catalog = epicIds.Select(n =>
        {
            var (titleHtml, _) = ResolveEpic(n, epics);
            return new Dictionary<string, object> { ["id"] = n, ["label"] = PathUtil.StripHtmlTags(titleHtml) };
        }).ToList();
        var epicsJson = System.Text.Json.JsonSerializer.Serialize(catalog);
        sb.Append($"<div class=\"sprint-filterable\" data-default-epics=\"{PathUtil.Html(defaults)}\" data-epics=\"{PathUtil.Html(epicsJson)}\"{capAttr}>\n");
        if (emitEmptyHint) sb.Append(EpicFilterEmptyHintMarkup);
    }

    private static void AppendEpicFilterClose(StringBuilder sb) => sb.Append(CloseEpicFilterable());


    /// <summary>The control-row buttons that link to the retrospectives index and the open-action-items page.
    /// "Retros" shows when any retro exists (rich tooltip: count + latest); the flag "⚑ N" shows only when there
    /// are open action items. Both are plain links to real pages (no modal). [Story 2.3 polish #5; Story 8.3]</summary>
    private static void AppendRetroButtons(StringBuilder sb, ProjectCounts counts, IReadOnlyList<RetroModel> retros)
    {
        if (retros.Count > 0)
        {
            var latest = retros[^1]; // ordered epic-then-name; the last is the newest.
            var tip = $"{retros.Count} {Charts.Plural(retros.Count, "retrospective", "retrospectives")}";
            if (latest.DateText is { Length: > 0 } d) tip += $"\nLatest: {PathUtil.StripHtmlTags(latest.Title)} ({d})";
            sb.Append($"<a class=\"cmd-menu-toggle js-tip\" href=\"{SiteNav.RetrosOutputPath}\" data-tip=\"{PathUtil.Html(tip)}\">Retros</a>");
        }

        var open = counts.OpenActionItems;
        if (open > 0)
        {
            var tip = $"{open} open action {Charts.Plural(open, "item", "items")} — review & resolve";
            sb.Append($"<a class=\"sprint-flag js-tip\" href=\"{SiteNav.ActionItemsOutputPath}\" data-tip=\"{PathUtil.Html(tip)}\" aria-label=\"{open} open action items\">⚑ {open}</a>");
        }
    }

    /// <summary>A compact status progress wheel: the lifecycle segments as a small donut with NO center number
    /// (the sibling "N / M done" label carries it — a number crammed into a tiny ring just reads as noise) and a
    /// hover tooltip of the breakdown. Shared by the sprint page's top strip and the home Now &amp; Next header.
    /// Denominator <c>M</c> and donut segments exclude retired (ledger history must not inflate incomplete work);
    /// retired may still appear as a tip line. Returns empty when no stories are tracked.
    /// [Story 2.3 redesign; Story 8.3]</summary>
    public static string RenderProgressWheel(ProjectCounts counts)
    {
        var stages = counts.TrackedStoryStages;
        if (stages.Sum(c => c.Count) == 0) return string.Empty;

        var retired = stages.FirstOrDefault(c => c.CssClass == "retired").Count;
        var segments = stages.Where(c => c.CssClass != "retired").Select(c => (c.Label, c.Count, c.CssClass)).ToList();
        var total = segments.Sum(s => s.Count); // M excludes retired
        // All-retired (or otherwise zero active-plan weight): no delivery wheel — avoid "0 / 0 done".
        if (total == 0) return string.Empty;

        var nonZero = segments.Where(s => s.Count > 0).ToList();
        var done = stages.First(c => c.CssClass == "done").Count;
        var ariaParts = string.Join(", ", nonZero.Select(s => $"{s.Count} {s.Label.ToLowerInvariant()}"));
        if (retired > 0) ariaParts = $"{ariaParts}, {retired} retired";

        // Rich single tooltip via the body-level (never-clipped) js-tip node; suppress the donut's per-segment
        // <title> so the tiny wheel shows just this one clean breakdown. [Story 2.3 polish]
        var tipLines = nonZero.Select(s => $"{s.Label}: {s.Count}").ToList();
        if (retired > 0) tipLines.Add($"Retired: {retired}");
        var tip = "Sprint delivery\n" + string.Join("\n", tipLines);
        var sb = new StringBuilder();
        sb.Append($"<div class=\"sprint-wheel js-tip\" data-tip=\"{PathUtil.Html(tip)}\">");
        sb.Append(Charts.Donut(segments, size: 46, showCenterText: false, segmentTitles: false, ariaLabel: $"Sprint delivery: {ariaParts}"));
        sb.Append($"<span class=\"sprint-wheel-label\">{done} / {total} done</span>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>Convenience overload — builds an ephemeral ledger from the yaml alone. Prefer
    /// <see cref="RenderProgressWheel(ProjectCounts)"/> when the shared generation ledger is available. [Story 8.3]</summary>
    public static string RenderProgressWheel(SprintStatus sprint) =>
        RenderProgressWheel(ProjectCounts.Build(ProgressModel.Empty, sprint, WorkInventory.Empty));


    /// <summary>One board card for a story: the whole card is a link to the story's generated page (real or
    /// placeholder — always exists for a model story), colored by its tracked lifecycle stage. Falls back to a
    /// non-link <c>div</c> only when the yaml key has no matching model story (no page to point at). [Story 2.3]</summary>
    private static void AppendBoardCard(StringBuilder sb, SprintEntry entry, EpicsModel? epics, string prefix,
        bool hidden = false, bool capOverflow = false)
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
        // No-plan cards (null story or TasksTotal == 0) get a dashed/muted treatment — the visual inverse of
        // the progress-bar gate — so they separate from actionable cards at a glance. [Story 8.4; UX-DR24]
        var noPlan = story is null || story.TasksTotal == 0;
        var noPlanClass = noPlan ? " no-plan" : string.Empty;
        var epicAttr = entry.EpicNumber is { } en ? $" data-epic=\"{en}\"" : string.Empty;
        var hiddenAttr = hidden ? " hidden" : string.Empty;
        var overflowAttr = capOverflow ? " data-cap-overflow=\"1\"" : string.Empty;

        sb.Append($"      <{tag} class=\"sprint-card js-tip {cssClass}{noPlanClass}\"{hrefAttr}{focusAttr}{epicAttr}{hiddenAttr}{overflowAttr} data-tip=\"{dataTip}\">\n");
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

    /// <summary>Trailing Unplanned lane on the by-status board — same membership as the sunburst Unplanned
    /// root. Cards are never mislabeled as stories. [Story 9.12]</summary>
    private static void AppendUnplannedLane(
        StringBuilder sb,
        IReadOnlyList<UnplannedMember> members,
        string prefix,
        int? capPerColumn,
        string? moreHref)
    {
        var shown = capPerColumn is { } cap && members.Count > cap
            ? members.Take(cap).ToList()
            : members.ToList();
        var overflow = capPerColumn is { } c && members.Count > c
            ? members.Skip(c).ToList()
            : new List<UnplannedMember>();
        var tip = PathUtil.Html("Direct / one-shot work outside the epic plan — not tracked as stories.");
        sb.Append($"  <section class=\"sprint-lane unplanned\" data-lane-label=\"Unplanned\" aria-label=\"{PathUtil.Html($"Unplanned: {members.Count} {Charts.Plural(members.Count, "item", "items")}")}\">\n");
        sb.Append($"    <div class=\"sprint-lane-head js-tip\" data-tip=\"{tip}\" title=\"{tip}\" tabindex=\"0\"><span class=\"sprint-lane-label\">Unplanned</span><span class=\"sprint-lane-count\">{members.Count}</span></div>\n");
        sb.Append("    <div class=\"sprint-cards\">\n");
        foreach (var m in shown) AppendUnplannedCard(sb, m, prefix, hidden: false);
        foreach (var m in overflow) AppendUnplannedCard(sb, m, prefix, hidden: true, capOverflow: true);
        if (capPerColumn is { } capLimit && members.Count > capLimit && moreHref is { Length: > 0 })
        {
            var extra = members.Count - capLimit;
            sb.Append($"      <a class=\"sprint-lane-more\" href=\"{PathUtil.Html(prefix + moreHref)}\">+{extra} more &rarr;</a>\n");
        }
        sb.Append("    </div>\n  </section>\n");
    }

    /// <summary>Trailing Unplanned / Direct work swimlane on the by-epic board. Always visible when non-empty
    /// (not subject to epic multi-select). [Story 9.12]</summary>
    private static void AppendUnplannedEpicSwimlane(
        StringBuilder sb,
        IReadOnlyList<UnplannedMember> members,
        string prefix)
    {
        sb.Append("<section class=\"sprint-epic-lane unplanned\" data-unplanned=\"1\">\n");
        sb.Append("  <div class=\"sprint-epic-head\">\n");
        sb.Append("    <span class=\"sprint-epic-title\">Unplanned / Direct work</span>\n");
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"sprint-cards-row\">\n");
        foreach (var m in members) AppendUnplannedCard(sb, m, prefix, hidden: false);
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    private static void AppendUnplannedCard(
        StringBuilder sb,
        UnplannedMember member,
        string prefix,
        bool hidden = false,
        bool capOverflow = false)
    {
        var isDirect = string.Equals(member.Kind, "direct", StringComparison.Ordinal);
        var kindLabel = isDirect ? "Direct change" : "Deferred item";
        var cssClass = member.IsDone
            ? "done"
            : isDirect ? "unplanned" : StatusStyles.ForSprint(member.Status ?? "open");
        var tip = PathUtil.Html($"{kindLabel}\n{member.Title}");
        var hiddenAttr = hidden ? " hidden" : string.Empty;
        var overflowAttr = capOverflow ? " data-cap-overflow=\"1\"" : string.Empty;
        var href = PathUtil.Html(prefix + member.Href);

        sb.Append($"      <a class=\"sprint-card js-tip unplanned-card {cssClass}\"{hiddenAttr}{overflowAttr} href=\"{href}\" data-tip=\"{tip}\">\n");
        sb.Append("        <div class=\"sprint-card-head\">\n");
        sb.Append($"          <span class=\"sprint-card-id\">{PathUtil.Html(kindLabel)}</span>\n");
        sb.Append("        </div>\n");
        sb.Append($"        <span class=\"sprint-card-title\">{PathUtil.Html(member.Title)}</span>\n");
        sb.Append("      </a>\n");
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

    /// <summary>One-line column meaning for a board lane header: <c>"{Label} = {StageMeaning}"</c>, sourced
    /// from <see cref="StatusStyles.StageMeaning"/> so badge tips, legend, and column tips share one seam.
    /// [Story 8.4; UX-DR24]</summary>
    private static string ColumnMeaning(string cssClass, string label) =>
        $"{label} = {StatusStyles.StageMeaning(cssClass)}";

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
