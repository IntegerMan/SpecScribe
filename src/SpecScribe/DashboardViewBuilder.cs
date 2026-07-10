namespace SpecScribe;

/// <summary>Builds the host-neutral <see cref="DashboardView"/> from the already-projected domain models — the
/// rendering-core half of Story 6.2's dashboard decomposition. ALL of the classification / grouping / fork logic
/// that used to sit inline in <c>HtmlTemplater.RenderIndex</c> + <c>AppendDashboard</c> lives here (which stat
/// tile the tasks/commits fork resolves to, which now/next cards the derived view yields, which home band each
/// doc lands in, PRD prominence, unrecognized-folder degradation). The <see cref="HtmlRenderAdapter"/> then maps
/// the resulting DATA to bytes with no branching of its own (memory: story-6-1-delivery-seam-live — the
/// re-home-don't-rewrite discipline, one level down from 6.1's chrome). [Story 6.2]</summary>
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
    /// <c>HtmlTemplater.RenderIndex</c> so the templater becomes a thin builder → adapter call. [Story 6.2]</summary>
    public static DashboardView Build(
        IReadOnlyList<DocModel> docs,
        SiteNav nav,
        ProgressModel progress,
        EpicsModel? epicsModel,
        RequirementsModel? requirements,
        IReadOnlyList<AdrEntry> adrs,
        CommandCatalog commands,
        WorkInventory work,
        SprintStatus? sprint,
        IReadOnlyList<RetroModel>? retros,
        ArtifactCoverage? coverage)
    {
        return new DashboardView
        {
            SiteTitle = nav.SiteTitle,
            StatTiles = BuildStatTiles(progress, work),
            NowNext = BuildNowNext(epicsModel, sprint),
            Epics = epicsModel,
            Commands = commands,
            Progress = progress,
            ProgressBars = BuildProgressBars(progress),
            Requirements = requirements,
            Coverage = coverage,
            QuickLinks = nav.QuickLinks.Select(q => new NavQuickLink(q.Label, q.OutputRelativePath, q.Description)).ToList(),
            Work = work,
            OpenRetroActionItems = sprint?.OpenActionItems.Count ?? 0,
            IndexBands = BuildIndexBands(docs, epicsModel, adrs, retros, work),
        };
    }

    // ----- Stat tiles ---------------------------------------------------------------------------------------

    /// <summary>The headline stat-grid row, forks resolved. Mirrors <c>AppendDashboard</c>'s five
    /// <see cref="Charts.StatCard"/> calls exactly (the fifth "Direct changes" tile only when there is
    /// quick-dev/deferred work — a byte-load-bearing conditional). [Story 6.2]</summary>
    private static IReadOnlyList<StatTile> BuildStatTiles(ProgressModel p, WorkInventory work)
    {
        var tiles = new List<StatTile>
        {
            new($"{p.EpicsDrafted}/{p.EpicsTotal}", "Epics drafted",
                Tooltip: "Epics with at least one story drafted, out of all epics."),
            new(p.StoriesTotal.ToString(), "Stories defined", $"{p.StoriesWithArtifact} with a task plan",
                "Stories listed across every epic; the sub-line counts those with a BMad task checklist."),
            p.TasksTotal > 0
                ? new($"{p.TasksDone}/{p.TasksTotal}", "Planned tasks done", $"{p.StoriesWithArtifact}/{p.StoriesTotal} stories planned",
                    $"Checklist tasks done across the {p.StoriesWithArtifact} stories that have a task plan — not the whole project.")
                : new("—", "Planned tasks done", "none tracked yet"),
            p.Git is { } git
                ? new(git.TotalCommits.ToString(), Charts.Plural(git.TotalCommits, "Commit", "Commits"), CommitStatSub(git),
                    "Total commits in the repository; the sub-line shows active days and how recently work landed.")
                : new("—", "Commits", "no git history"),
        };

        if (!work.IsEmpty)
        {
            var deferredCount = work.Deferred?.OpenItemCount ?? 0;
            var sub = work.Deferred is not null
                ? $"{deferredCount} deferred {Charts.Plural(deferredCount, "item", "items")}"
                : "outside the epic plan";
            tiles.Add(new(work.QuickDev.Count.ToString(), "Direct changes", sub,
                "Quick-dev / one-shot changes and deferred-work notes — tracked separately from the epic/story plan, never counted as epic or story completion."));
        }

        return tiles;
    }

    /// <summary>The commit stat's sub-line: active days plus a recency signal. Relocated verbatim from
    /// <c>HtmlTemplater.CommitStatSub</c>. [Story 1.5 F3]</summary>
    private static string CommitStatSub(GitPulse git)
    {
        var daysAgo = DateOnly.FromDateTime(DateTime.Now).DayNumber - git.LastCommitDate.DayNumber;
        var recency = daysAgo <= 0 ? "last commit today"
            : daysAgo == 1 ? "last commit yesterday"
            : $"last commit {daysAgo}d ago";
        return $"{git.ActiveDays} active {Charts.Plural(git.ActiveDays, "day", "days")} · {recency}";
    }

    // ----- Overall Progress bars ----------------------------------------------------------------------------

    /// <summary>The two "Overall Progress" bars, the tasks fork resolved. Mirrors <c>AppendDashboard</c>. [Story 6.2]</summary>
    private static IReadOnlyList<ProgressBarView> BuildProgressBars(ProgressModel p) => new[]
    {
        new ProgressBarView("Planning", p.EpicsDrafted, p.EpicsTotal, $"{p.EpicsDrafted} / {p.EpicsTotal} epics"),
        p.TasksTotal > 0
            ? new ProgressBarView("Implementation", p.TasksDone, p.TasksTotal, $"{p.TasksDone} / {p.TasksTotal} tasks ({p.StoriesWithArtifact} of {p.StoriesTotal} stories planned)")
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

    // ----- Index bands --------------------------------------------------------------------------------------

    /// <summary>Groups every home-index doc into ordered bands exactly as <c>HtmlTemplater.RenderIndex</c>'s band
    /// loop did: the well-known groups (with the planning band's special layout), then each unrecognized top-level
    /// folder as its own coherently-titled band, then the ADR band and the Retrospectives band. [Story 6.2]</summary>
    private static IReadOnlyList<IndexBand> BuildIndexBands(
        IReadOnlyList<DocModel> docs, EpicsModel? epicsModel, IReadOnlyList<AdrEntry> adrs,
        IReadOnlyList<RetroModel>? retros, WorkInventory work)
    {
        var bands = new List<IndexBand>();

        // Quick-dev + deferred docs are promoted to the work section (rendered separately), so keep them out of
        // the generic grids here — same as the templater's promotedOutputs/used seeding.
        var promotedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in work.QuickDev) promotedOutputs.Add(q.OutputPath);
        if (work.Deferred is { } def) promotedOutputs.Add(def.OutputPath);

        var used = new HashSet<DocModel>(
            docs.Where(d => promotedOutputs.Contains(PathUtil.NormalizeSlashes(d.OutputRelativePath))));

        foreach (var (groupTitle, groupPrefix) in KnownIndexGroups)
        {
            var inGroup = docs
                .Where(d => !used.Contains(d))
                .Where(d => groupPrefix.Length == 0
                    ? !PathUtil.NormalizeSlashes(d.SourceRelativePath).Contains('/')
                    : groupPrefix.Equals(BmadArtifactAdapter.ImplementationArtifactsDirName, StringComparison.OrdinalIgnoreCase)
                        ? BmadArtifactAdapter.IsUnderImplementationArtifacts(d.SourceRelativePath)
                        : PathUtil.NormalizeSlashes(d.SourceRelativePath).StartsWith(groupPrefix + "/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (inGroup.Count == 0) continue;
            foreach (var d in inGroup) used.Add(d);

            if (groupPrefix == "planning-artifacts")
            {
                bands.Add(BuildPlanningBand(inGroup));
                continue;
            }

            bands.Add(new IndexBand
            {
                Title = groupTitle,
                ConceptKey = groupTitle,
                Cards = inGroup.Select(BuildDocCard).ToList(),
            });
        }

        // Unrecognized top-level folders → their own coherently-titled bands (NFR8), appended after the known
        // groups, ordered by folder key.
        var remaining = docs.Where(d => !used.Contains(d)).ToList();
        foreach (var folderGroup in remaining
            .GroupBy(d => TopLevelFolder(d.SourceRelativePath), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var title = folderGroup.Key.Length == 0 ? "Other" : HumanizeFolderName(folderGroup.Key);
            bands.Add(new IndexBand
            {
                Title = title,
                ConceptKey = title,
                Cards = folderGroup.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).Select(BuildDocCard).ToList(),
            });
        }

        if (adrs.Count > 0)
        {
            bands.Add(new IndexBand
            {
                Title = "Architecture Decision Records",
                ConceptKey = "ADRs",
                TitleRow = true,
                MoreLinkHref = PathUtil.NormalizeSlashes(SiteNav.AdrsLandingOutputPath),
                MoreLinkLabel = "All ADRs",
                Cards = adrs.Select(BuildAdrCard).ToList(),
            });
        }

        if (retros is { Count: > 0 })
        {
            bands.Add(new IndexBand
            {
                Title = "Retrospectives",
                ConceptKey = "Retrospectives",
                NoIcon = true,
                Cards = retros.Select(BuildRetroCard).ToList(),
            });
        }

        return bands;
    }

    /// <summary>The special "Planning Artifacts" band: PRD-prominent primary card, paired UX subgroup, and the
    /// remaining planning docs as ordinary cards. Reproduces <c>HtmlTemplater.AppendPlanningSection</c>'s
    /// classification (well-known-filename match, rubric-folds-under-PRD, claimed-set bookkeeping). [Story 6.2]</summary>
    private static IndexBand BuildPlanningBand(IReadOnlyList<DocModel> docs)
    {
        DocModel? prd = FindByFileName(docs, ModuleContext.WellKnownDocs.Prd);
        DocModel? rubric = FindByFileName(docs, ModuleContext.WellKnownDocs.PrdReviewRubric);
        DocModel? uxDesign = FindByFileName(docs, ModuleContext.WellKnownDocs.UxDesign);
        DocModel? uxExperience = FindByFileName(docs, ModuleContext.WellKnownDocs.UxExperience);
        DocModel? brief = FindByFileName(docs, ModuleContext.WellKnownDocs.Brief);

        var rubricFolded = prd is not null && rubric is not null;

        var claimed = new HashSet<DocModel>();
        void Claim(DocModel? d) { if (d is not null) claimed.Add(d); }
        Claim(prd);
        Claim(uxDesign);
        Claim(uxExperience);
        Claim(brief);
        if (rubricFolded) Claim(rubric);

        var uxCards = new List<IndexCardView>();
        if (uxDesign is not null) uxCards.Add(BuildDocCard(uxDesign));
        if (uxExperience is not null) uxCards.Add(BuildDocCard(uxExperience));

        var others = new List<DocModel>();
        if (brief is not null) others.Add(brief);
        others.AddRange(docs.Where(d => !claimed.Contains(d))
            .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase));

        return new IndexBand
        {
            Title = "Planning Artifacts",
            ConceptKey = "Planning Artifacts",
            Cards = Array.Empty<IndexCardView>(),
            Planning = new PlanningLayout(
                Prd: prd is not null ? BuildPrimaryPrdCard(prd, rubricFolded ? rubric : null) : null,
                UxCards: uxCards,
                OtherCards: others.Select(BuildDocCard).ToList()),
        };
    }

    /// <summary>An ordinary artifact card's data. Title is the SPEC-hub-aware <see cref="IndexCardTitle"/>; status
    /// is the trimmed raw word (null when blank); meta is the "date · author" join. [Story 6.2]</summary>
    private static IndexCardView BuildDocCard(DocModel d) => new()
    {
        Style = IndexCardStyle.Doc,
        Title = IndexCardTitle(d),
        Href = PathUtil.NormalizeSlashes(d.OutputRelativePath),
        SourcePath = PathUtil.NormalizeSlashes(d.SourceRelativePath),
        Status = d.Frontmatter.Status?.Trim() is { Length: > 0 } s ? s : null,
        Meta = CardMeta(d),
    };

    private static IndexCardView BuildPrimaryPrdCard(DocModel prd, DocModel? rubric) => new()
    {
        Style = IndexCardStyle.PrimaryPrd,
        Kicker = "Primary document",
        Title = IndexCardTitle(prd),
        Href = PathUtil.NormalizeSlashes(prd.OutputRelativePath),
        SourcePath = PathUtil.NormalizeSlashes(prd.SourceRelativePath),
        Status = prd.Frontmatter.Status?.Trim() is { Length: > 0 } s ? s : null,
        Meta = CardMeta(prd),
        BranchHref = rubric is not null ? PathUtil.NormalizeSlashes(rubric.OutputRelativePath) : null,
        BranchLabel = rubric is not null ? "Quality review" : null,
    };

    private static IndexCardView BuildAdrCard(AdrEntry adr) => new()
    {
        Style = IndexCardStyle.Adr,
        Title = adr.Title,
        Href = PathUtil.NormalizeSlashes(adr.OutputRelativePath),
        SourcePath = PathUtil.NormalizeSlashes(adr.SourceRelativePath),
        Status = adr.Status is { Length: > 0 } s ? s : null,
    };

    private static IndexCardView BuildRetroCard(RetroModel r) => new()
    {
        Style = IndexCardStyle.Retro,
        Title = r.Title,
        Href = PathUtil.NormalizeSlashes(r.OutputRelativePath),
        SourcePath = PathUtil.NormalizeSlashes(r.SourceRelativePath),
        Meta = r.DateText is { Length: > 0 } d ? d : null,
    };

    /// <summary>The de-emphasized "date · author" meta line, or null when neither is present. Mirrors
    /// <c>HtmlTemplater.AppendCardMeta</c>. [Story 6.2]</summary>
    private static string? CardMeta(DocModel d)
    {
        var descParts = new List<string>();
        if (d.Frontmatter.Date is { Length: > 0 } dt) descParts.Add(dt);
        if (d.Frontmatter.Author is { Length: > 0 } a) descParts.Add(a);
        return descParts.Count > 0 ? string.Join(" · ", descParts) : null;
    }

    /// <summary>The first path segment of a source-relative path, or "" for a root-level doc. Relocated from
    /// <c>HtmlTemplater.TopLevelFolder</c>. [Story 4.2 Task 3]</summary>
    private static string TopLevelFolder(string sourceRelativePath)
    {
        var norm = PathUtil.NormalizeSlashes(sourceRelativePath);
        var slash = norm.IndexOf('/');
        return slash < 0 ? string.Empty : norm[..slash];
    }

    /// <summary>A human band title for an unrecognized top-level folder. Relocated verbatim from
    /// <c>HtmlTemplater.HumanizeFolderName</c>. [Story 4.2 Task 3]</summary>
    private static string HumanizeFolderName(string folder)
    {
        var ti = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        var words = folder.Split('-', '_', ' ', '.')
            .Where(w => w.Length > 0)
            .Select(w => ti.ToTitleCase(w.ToLowerInvariant()))
            .ToList();
        return words.Count == 0 ? folder : string.Join(" ", words);
    }

    /// <summary>The first doc whose filename matches <paramref name="fileName"/> (case-insensitive, anywhere in
    /// its source path). Relocated from <c>HtmlTemplater.FindByFileName</c>. [Story 2.4 Task 3]</summary>
    private static DocModel? FindByFileName(IReadOnlyList<DocModel> docs, string fileName) =>
        docs.FirstOrDefault(d => string.Equals(
            Path.GetFileName(PathUtil.NormalizeSlashes(d.SourceRelativePath)), fileName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The title to show on a doc's index card — the SPEC-hub rename. Relocated from
    /// <c>HtmlTemplater.IndexCardTitle</c>. [Story 2.2 Task 2]</summary>
    private static string IndexCardTitle(DocModel d) =>
        d.Frontmatter.Id is { Length: > 0 } id && id.StartsWith("SPEC-", StringComparison.OrdinalIgnoreCase)
            ? "SPEC — Canonical Contract"
            : d.Title;
}
