namespace SpecScribe;

/// <summary>Builds the host-neutral <see cref="DashboardView"/> from the already-projected domain models — the
/// rendering-core half of Story 6.2's dashboard decomposition. The fork/derivation logic that used to sit inline
/// in <c>HtmlTemplater.RenderIndex</c> + <c>AppendDashboard</c> lives here (which stat tile the tasks/commits fork
/// resolves to, which now/next cards the derived view yields, the overall-progress bars). The
/// <see cref="HtmlRenderAdapter"/> then maps the resulting DATA to bytes with no branching of its own (memory:
/// story-6-1-delivery-seam-live — the re-home-don't-rewrite discipline, one level down from 6.1's chrome).
/// [Story 6.2; home-index bands removed in spec-declutter-home-dashboard]</summary>
public static class DashboardViewBuilder
{
    /// <summary>The ordered WELL-KNOWN home-index groups (friendly title + source-path prefix), fixed order
    /// Overview → Planning → Spec Kernel → Implementation. Relocated verbatim from <c>HtmlTemplater</c> (its
    /// former <c>KnownIndexGroups</c>) — the known set renders exactly as it always has; only folders OUTSIDE it
    /// get the appended structure-derived bands. [Story 4.2 Task 3; relocated Story 6.2]</summary>
    private static readonly (string Title, string Prefix)[] KnownIndexGroups =
    {
        ("Overview", ""),
        ("Planning Artifacts", "planning-artifacts"),
        ("Spec Kernel", "specs"),
        ("Implementation Artifacts", BmadArtifactAdapter.ImplementationArtifactsDirName),
    };

    /// <summary>Whether a top-level source folder is one of the well-known home-index groups — the signal the
    /// generator uses to emit an "unrecognized structure" notice for anything else. Relocated from
    /// <c>HtmlTemplater</c>; <see cref="HtmlTemplater.IsWellKnownTopLevelFolder"/> now delegates here. [Story 4.2 Task 3/5]</summary>
    public static bool IsWellKnownTopLevelFolder(string folder) =>
        KnownIndexGroups.Any(g => g.Prefix.Length > 0 && string.Equals(g.Prefix, folder, StringComparison.OrdinalIgnoreCase));

    /// <summary>Assembles the full dashboard section view model. Same inputs (and defaults) as the former
    /// <c>HtmlTemplater.RenderIndex</c> so the templater becomes a thin builder → adapter call. Summary counts
    /// come exclusively from <paramref name="counts"/> (the portal-wide ledger). [Story 6.2; Story 8.3]</summary>
    public static DashboardView Build(
        SiteNav nav,
        ProgressModel progress,
        EpicsModel? epicsModel,
        RequirementsModel? requirements,
        CommandCatalog commands,
        WorkInventory work,
        SprintStatus? sprint,
        ArtifactCoverage? coverage,
        bool hasTimeline = false,
        ProjectCounts? counts = null)
    {
        // Production always passes the shared SiteGenerator ledger. Null → build an equivalent ephemeral
        // ledger from the same inputs so tests/stubs that omit counts keep correct Defined/Tracked numbers.
        var ledger = counts ?? ProjectCounts.Build(progress, sprint, work, epicsModel);
        return new DashboardView
        {
            SiteTitle = nav.SiteTitle,
            StatTiles = BuildStatTiles(ledger, progress, work, epicsModel, sprint, hasTimeline, requirements),
            NowNext = BuildNowNext(epicsModel, sprint),
            Epics = epicsModel,
            Commands = commands,
            Progress = progress,
            ProgressBars = BuildProgressBars(ledger),
            Requirements = requirements,
            Coverage = coverage,
            QuickLinks = nav.QuickLinks.Select(q => new NavQuickLink(q.Label, q.OutputRelativePath, q.Description)).ToList(),
            Work = work,
            OpenRetroActionItems = ledger.OpenActionItems,
            Counts = ledger,
            HasTimeline = hasTimeline,
        };
    }

    // ----- Stat tiles ---------------------------------------------------------------------------------------

    /// <summary>The headline stat-grid row, forks resolved. Count values come from the portal-wide ledger;
    /// the fifth "Direct changes" tile still gates on <paramref name="work"/>.IsEmpty (byte-load-bearing).
    /// Git/commit tile stays on <paramref name="progress"/> (out of Story 8.3 scope). Requirement tiles lead
    /// the band when a requirements model exists so a Stakeholder entering to check FR/NFR/UX-DR progress
    /// lands on a click-through to requirements.html first. Each other tile drills to the most relevant
    /// standalone view when that page exists. [Story 6.2; Story 8.3; Story 9.2 UX]</summary>
    private static IReadOnlyList<StatTile> BuildStatTiles(
        ProjectCounts c, ProgressModel p, WorkInventory work, EpicsModel? epicsModel, SprintStatus? sprint,
        bool hasTimeline, RequirementsModel? requirements)
    {
        var epicsHref = epicsModel is { Epics.Count: > 0 } ? SiteNav.EpicsOutputPath : null;
        // Stories defined → Requirements (traceability journey); tasks still prefer the sprint board when tracked.
        var storiesHref = epicsModel is { Epics.Count: > 0 } ? SiteNav.RequirementsOutputPath : null;
        var tasksHref = sprint is { IsEmpty: false } ? SiteNav.SprintOutputPath : epicsHref;
        var commitsHref = hasTimeline ? SiteNav.TimelineOutputPath : null;
        var reqHref = SiteNav.RequirementsOutputPath;

        var tiles = new List<StatTile>();

        // Requirements lead the band — clickable entry points into the requirements journey. [Story 9.2 UX]
        if (requirements is not null)
        {
            if (requirements.Functional.Count > 0)
            {
                tiles.Add(RequirementStatTile("Functional reqs", requirements.Functional, reqHref));
            }
            if (requirements.NonFunctional.Count > 0)
            {
                tiles.Add(RequirementStatTile("Non-functional", requirements.NonFunctional, reqHref));
            }
            if (requirements.Design.Count > 0)
            {
                tiles.Add(RequirementStatTile("Design reqs", requirements.Design, reqHref));
            }
        }

        tiles.Add(new($"{c.EpicsDrafted}/{c.EpicsDefined}", "Epics drafted",
            Tooltip: "Epics with at least one story drafted, out of all epics.", Href: epicsHref));
        tiles.Add(new(c.StoriesDefined.ToString(), "Stories defined", $"{c.StoriesWithArtifact} with a task plan",
            "Stories listed across every epic; the sub-line counts those with a BMad task checklist.", storiesHref));
        tiles.Add(c.TasksTotal > 0
            ? new($"{c.TasksDone}/{c.TasksTotal}", "Planned tasks done", $"{c.StoriesWithArtifact}/{c.StoriesDefined} stories planned",
                $"Checklist tasks done across the {c.StoriesWithArtifact} stories that have a task plan — not the whole project.", tasksHref)
            : new("—", "Planned tasks done", "none tracked yet", Href: tasksHref));
        tiles.Add(p.Git is { } git
            ? new(git.TotalCommits.ToString(), Charts.Plural(git.TotalCommits, "Commit", "Commits"), CommitStatSub(git),
                "Total commits in the repository; the sub-line shows active days and how recently work landed.", commitsHref)
            : new("—", "Commits", "no git history"));

        if (!work.IsEmpty)
        {
            var deferredCount = c.DeferredOpenItems;
            var sub = work.Deferred is not null
                ? $"{deferredCount} deferred {Charts.Plural(deferredCount, "item", "items")}"
                : "outside the epic plan";
            tiles.Add(new(c.DirectChanges.ToString(), "Direct changes", sub,
                "Quick-dev / one-shot changes and deferred-work notes — tracked separately from the epic/story plan, never counted as epic or story completion.",
                work.Deferred?.OutputPath));
        }

        return tiles;
    }

    /// <summary>One clickable requirements-kind tile: done/total with an in-progress sub-line, drilling to
    /// requirements.html. [Story 9.2 UX]</summary>
    private static StatTile RequirementStatTile(string label, IReadOnlyList<RequirementInfo> reqs, string href)
    {
        var done = reqs.Count(r => r.Status == RequirementStatus.Done);
        var active = reqs.Count(r => r.Status == RequirementStatus.Active);
        var sub = active > 0
            ? $"{active} partially implemented"
            : $"{reqs.Count(r => r.Status == RequirementStatus.Ready)} ready · {reqs.Count(r => r.Status == RequirementStatus.Planned)} planned";
        return new($"{done}/{reqs.Count}", label, sub,
            $"{label}: {done} done of {reqs.Count}. Open the requirements view to refine coverage and follow the epic → story chain.",
            href);
    }

    /// <summary>The commit stat's sub-line: active days plus a deterministic absolute-date recency signal.
    /// Uses <see cref="PortalDates.Day"/> (never <c>DateTime.Now</c>) so a from-scratch regen of the same
    /// inputs is byte-identical. [Story 1.5 F3; Story 8.8]</summary>
    private static string CommitStatSub(GitPulse git)
        => $"{git.ActiveDays} active {Charts.Plural(git.ActiveDays, "day", "days")} · last commit {PortalDates.Day(git.LastCommitDate)}";

    // ----- Overall Progress bars ----------------------------------------------------------------------------

    /// <summary>The two "Overall Progress" bars, the tasks fork resolved — values from the ledger. [Story 6.2; Story 8.3]</summary>
    private static IReadOnlyList<ProgressBarView> BuildProgressBars(ProjectCounts c) => new[]
    {
        new ProgressBarView("Planning", c.EpicsDrafted, c.EpicsDefined, $"{c.EpicsDrafted} / {c.EpicsDefined} epics"),
        c.TasksTotal > 0
            ? new ProgressBarView("Implementation", c.TasksDone, c.TasksTotal, $"{c.TasksDone} / {c.TasksTotal} tasks ({c.StoriesWithArtifact} of {c.StoriesDefined} stories planned)")
            : new ProgressBarView("Implementation", 0, 0, "not started"),
    };

    // ----- Now & Next ---------------------------------------------------------------------------------------

    /// <summary>The "Now &amp; Next" panel view, or null when it is omitted. Reproduces <c>AppendNowAndNext</c>'s
    /// gating: nothing without an epics model; the sprint board when a sprint is tracked; otherwise the derived
    /// in-dev/review/up-next/next-to-draft cards (and null when even those are empty). [Story 6.2]</summary>
    private static DashboardNowNext? BuildNowNext(EpicsModel? epicsModel, SprintStatus? sprint)
    {
        if (epicsModel is null) return null;

        if (sprint is { IsEmpty: false })
        {
            return new DashboardNowNext(sprint, Array.Empty<NowNextCard>());
        }

        var allStories = epicsModel.Epics.SelectMany(e => e.Stories.Select(s => (Epic: e, Story: s))).ToList();
        var inDev = allStories.Where(x => StatusStyles.ForStory(x.Story) == "active").ToList();
        var inReview = allStories.Where(x => StatusStyles.ForStory(x.Story) == "review").ToList();
        var upNext = allStories.Where(x => StatusStyles.ForStory(x.Story) == "ready").ToList();

        var nextStoryToDraft = allStories
            .Where(x => x.Epic.Status == EpicStatus.Drafted && StatusStyles.ForStory(x.Story) == "drafted")
            .OrderBy(x => x.Epic.Number)
            .ThenBy(x => StoryMinor(x.Story.Id))
            .Select(x => (x.Epic, x.Story))
            .FirstOrDefault();

        var nextEpicToDraft = epicsModel.Epics.OrderBy(e => e.Number).FirstOrDefault(e => e.Status == EpicStatus.Pending);

        if (inDev.Count == 0 && inReview.Count == 0 && upNext.Count == 0
            && nextStoryToDraft.Story is null && nextEpicToDraft is null) return null;

        var cards = new List<NowNextCard>();

        foreach (var (epic, story) in inDev)
        {
            cards.Add(new NowNextCard("active", "In development",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        foreach (var (epic, story) in inReview)
        {
            cards.Add(new NowNextCard("review", "In review",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        foreach (var (epic, story) in upNext)
        {
            cards.Add(new NowNextCard("ready", "Up next",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        if (nextStoryToDraft.Story is not null)
        {
            var (epic, story) = nextStoryToDraft;
            cards.Add(new NowNextCard("drafted", "Next story to draft",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        if (nextEpicToDraft is not null)
        {
            cards.Add(new NowNextCard("pending", "Next epic to draft",
                $"Epic {nextEpicToDraft.Number} · {PathUtil.StripHtmlTags(nextEpicToDraft.Title)}",
                $"epics/epic-{nextEpicToDraft.Number}.html"));
        }

        return new DashboardNowNext(null, cards);
    }

    /// <summary>The "M" from a story id "N.M"; <see cref="int.MaxValue"/> for ids that don't parse (sort last).
    /// Relocated from <c>HtmlTemplater.StoryMinor</c>.</summary>
    private static int StoryMinor(string storyId)
    {
        var dot = storyId.LastIndexOf('.');
        return dot >= 0 && int.TryParse(storyId.AsSpan(dot + 1), out var minor) ? minor : int.MaxValue;
    }

}
