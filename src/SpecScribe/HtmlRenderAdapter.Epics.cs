using System.Text;

namespace SpecScribe;

/// <summary>The epics-family page BODY rendering (epics index / epic page / story page / story placeholder),
/// re-homed from <c>EpicsTemplater</c> into the delivery adapter and driven by the host-neutral section view
/// models (Story 6.2). A mechanical RE-HOMING, not a rewrite: data-shaped sections render from records, chart
/// panels call the same <c>Charts.*</c>/<c>Mermaid.*</c> helpers with the views' domain inputs, opaque prose
/// fragments are emitted verbatim, and every conditional maps to an optional section — bytes unchanged. [Story 6.2]</summary>
public sealed partial class HtmlRenderAdapter
{
    // ----- Epics index --------------------------------------------------------------------------------------

    /// <summary>Renders the epics-index <c>&lt;main&gt;…&lt;/main&gt;</c> body from its section view model. [Story 6.2]</summary>
    public string RenderEpicsIndexBody(EpicsIndexView view)
    {
        var model = view.Epics;
        var sb = new StringBuilder();

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Epics &amp; Stories</h1>\n");
        // Story 8.7: the epic/drafted count restatement is trimmed here — the stat-grid tiles below
        // (AppendEpicsProgressPanel) are the single authoritative count display. The subtitle keeps only
        // the site title; no count source or view-model field changes.
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(view.SiteTitle)}</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<section class=\"dashboard\">\n");
        AppendEpicsProgressPanel(sb, view.Progress, view.Counts);
        sb.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Project at a Glance</h3>\n");
        sb.Append(Charts.Sunburst(model, followUps: view.FollowUps, unplanned: view.UnplannedWork));
        sb.Append("</div>\n");

        // Remaining Work by Epic — its own panel (Story 10.7 AC1 follow-up), same helper/markup as Dashboard.
        var companionGrid = Charts.SunburstCompanionList(model, followUps: view.FollowUps, unplanned: view.UnplannedWork);
        if (companionGrid.Length > 0)
        {
            sb.Append("<div class=\"chart-panel epic-remaining-panel\">\n<h3>Remaining Work by Epic</h3>\n");
            sb.Append(companionGrid);
            sb.Append("</div>\n");
        }
        sb.Append("</section>\n\n");

        if (model.OverviewHtml.Length > 0)
        {
            sb.Append($"<div class=\"banner\">{model.OverviewHtml}</div>\n\n");
        }

        if (model.Epics.Count == 0)
        {
            AppendEmptyEpicsGuidance(sb, view.Commands);
        }

        AppendChipSection(sb, "Vertical Slice", view.VerticalSliceChips);
        AppendChipSection(sb, "Further Development", view.FurtherDevelopmentChips);

        if (model.Epics.Count > 0)
        {
            sb.Append("<div class=\"section-divider\">All Epics</div>\n\n");
            foreach (var epic in model.Epics)
            {
                AppendEpicCard(sb, epic, view.Commands);
            }
        }

        sb.Append("<div class=\"section-divider\">Suggested Build Order</div>\n\n");
        sb.Append(Mermaid.Block(Mermaid.RoadmapDiagram(model)));

        if (model.RequirementsInventoryHtml.Length > 0)
        {
            sb.Append("<details class=\"epic-card requirements-inventory\">\n");
            sb.Append("  <summary>Requirements Inventory</summary>\n");
            sb.Append($"  <div class=\"story-body\">{model.RequirementsInventoryHtml}</div>\n");
            sb.Append("</details>\n\n");
        }

        sb.Append("</main>\n\n");
        return sb.ToString();
    }

    /// <summary>The epics-index progress panel (stat-grid + Epic Status donut + mosaic). Headline counts come
    /// from the portal-wide ledger; the mosaic still needs per-epic detail from <paramref name="progress"/>.
    /// Donut segment values are the ledger fields; their sum is the structural total. [Story 6.2; Story 8.3]</summary>
    private void AppendEpicsProgressPanel(StringBuilder sb, ProgressModel progress, ProjectCounts counts)
    {
        sb.Append("<div class=\"stat-grid\">\n");
        sb.Append(Charts.StatCard($"{counts.EpicsDrafted}/{counts.EpicsDefined}", "Epics drafted"));
        sb.Append(Charts.StatCard(counts.StoriesDefined.ToString(), "Stories defined", $"{counts.StoriesWithArtifact} with a task plan"));
        sb.Append(counts.TasksTotal > 0
            ? Charts.StatCard($"{counts.TasksDone}/{counts.TasksTotal}", "Tasks done", $"across {counts.StoriesWithArtifact} planned stor{(counts.StoriesWithArtifact == 1 ? "y" : "ies")}")
            : Charts.StatCard("—", "Tasks done", "none tracked yet"));
        sb.Append("</div>\n\n");

        sb.Append("<div class=\"chart-panel\">\n");
        sb.Append("<div class=\"chart-panel-header-row\"><h3>Epic Status");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h3></div>\n<div class=\"donut-and-legend\">\n");
        var epicStatusSegments = new (string Label, int Value, string CssClass)[]
        {
            ("Drafted", counts.EpicsDrafted, "drafted"),
            ("Pending", counts.EpicsPending, "pending"),
        };
        // Structural: segments are the ledger fields; EpicsDrafted + EpicsPending partition EpicsDefined
        // (every epic is Drafted or Pending — see ProgressCalculator).
        sb.Append(Charts.Donut(epicStatusSegments, ariaLabel: $"Epic status: {counts.EpicsDrafted} drafted, {counts.EpicsPending} pending"));
        sb.Append(Charts.DonutLegend(epicStatusSegments));
        sb.Append("</div>\n</div>\n\n");

        sb.Append("<div class=\"chart-panel\">\n<h3>Progress by Epic</h3>\n");
        sb.Append(Charts.EpicMosaic(progress.PerEpic, e => $"epics/epic-{e.Number}.html"));
        sb.Append("</div>\n\n");
    }

    private void AppendChipSection(StringBuilder sb, string title, IReadOnlyList<EpicChip> chips)
    {
        if (chips.Count == 0) return;

        sb.Append($"<div class=\"section-divider\">{PathUtil.Html(title)}</div>\n<div class=\"epic-overview\">\n");
        foreach (var chip in chips)
        {
            sb.Append($"  <a class=\"epic-chip {chip.StatusClass}\" href=\"{chip.Href}\"><span class=\"num\">{chip.Number:00}</span>{chip.TitleHtml}</a>\n");
        }
        sb.Append("</div>\n\n");
    }

    private void AppendEpicCard(StringBuilder sb, EpicInfo epic, CommandCatalog commands)
    {
        var statusCls = StatusStyles.ForEpicWithRetrospective(epic);
        sb.Append($"<div class=\"epic-card\" id=\"epic-{epic.Number}\">\n");
        sb.Append($"  <h2><span class=\"epic-num\">Epic {epic.Number}</span> {epic.Title} <span class=\"epic-status {statusCls}\">{PathUtil.Html(StatusStyles.EpicLabel(statusCls))}</span></h2>\n");

        if (epic.GoalHtml.Length > 0)
        {
            sb.Append($"  <p class=\"epic-goal\">{epic.GoalHtml}</p>\n");
        }
        if (epic.FrMetaHtml is { Length: > 0 })
        {
            sb.Append($"  <div class=\"epic-meta\">{epic.FrMetaHtml}</div>\n");
        }
        if (epic.Status == EpicStatus.Pending)
        {
            var note = BmadCommands.InlineGuidance(
                commands.Command("create-epics-and-stories"),
                "Stories not yet drafted — draft them with",
                "Stories not yet drafted.");
            sb.Append($"  <div class=\"pending-note\">{note}</div>\n");
        }
        sb.Append($"  <a class=\"view-epic-link\" href=\"epics/epic-{epic.Number}.html\">View Epic {epic.Number} stories &rarr;</a>\n");
        sb.Append("</div>\n\n");
    }

    private void AppendEmptyEpicsGuidance(StringBuilder sb, CommandCatalog commands)
    {
        var note = BmadCommands.InlineGuidance(
            commands.Command("create-epics-and-stories"),
            "No epics yet. Break your plan into epics and stories with",
            "No epics yet — add them to your plan to see them here.");
        // Story 10.8: routes through the promoted ListRow.EmptyState helper (byte-identical output).
        sb.Append(ListRow.EmptyState(note, "epic-card"));
    }

    // ----- Epic page ----------------------------------------------------------------------------------------

    /// <summary>Renders a single epic page's <c>&lt;main&gt;…&lt;/main&gt;</c> body from its section view model. [Story 6.2]</summary>
    public string RenderEpicBody(EpicPageView view)
    {
        var main = new StringBuilder();
        var toc = new List<Toc.Entry>();

        main.Append("<header class=\"doc-header\">\n");
        // Sibling pager rides the chrome-level wayfinding strip alongside the breadcrumb now (PageView.Pager),
        // not the body's own header. [Story 10.11]
        main.Append("  <div class=\"kicker-row\">\n");
        main.Append($"    <span class=\"story-kicker\">Epic {view.Number}</span>\n");
        main.Append($"    {StatusStyles.Badge(view.StatusClass, view.StatusLabel)}\n");
        main.Append(StatusStyles.LegendKey());
        main.Append("  </div>\n");
        main.Append($"  <h1>{view.TitleHtml}</h1>\n");
        main.Append("</header>\n\n");

        if (view.GoalHtml.Length > 0 || view.FrMetaHtml is { Length: > 0 })
        {
            main.Append("<div class=\"epic-card epic-intro\" id=\"sec-overview\">\n");
            if (view.GoalHtml.Length > 0) main.Append($"  <p class=\"epic-goal\">{view.GoalHtml}</p>\n");
            if (view.FrMetaHtml is { Length: > 0 }) main.Append($"  <div class=\"epic-meta\">{view.FrMetaHtml}</div>\n");
            main.Append("</div>\n\n");
            toc.Add(new Toc.Entry(2, "Overview", "sec-overview"));
        }

        if (view.HasStories)
        {
            main.Append("<section class=\"dashboard-narrow\">\n<div class=\"chart-row\">\n");
            main.Append("<div class=\"chart-col\">\n");
            main.Append("<div class=\"chart-panel\">\n<h3>Epic Progress</h3>\n");
            foreach (var bar in view.ProgressBars)
            {
                main.Append(Charts.ProgressBar(bar.Label, bar.Value, bar.Max, bar.RightLabel));
            }
            main.Append("</div>\n\n");
            main.Append(view.NextActionsPanelHtml);
            main.Append("</div>\n\n");

            main.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Story Breakdown</h3>\n");
            // Always the story's own page (drafted detail or undrafted placeholder) — never an in-page
            // #story-N-M card jump. Matches story-card TitleHref and the project sunburst's StoryPagePath
            // fallback so a sunburst click always leaves the epic page for the story surface.
            main.Append(Charts.EpicSunburst(view.Epic, story =>
                view.Prefix + (story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id)),
                followUps: view.FollowUps, unplanned: view.UnplannedWork));
            main.Append("</div>\n");
            main.Append("</div>\n</section>\n\n");
        }
        else if (view.NextStepsHtml.Length > 0)
        {
            main.Append("<section class=\"dashboard-narrow\">\n");
            main.Append(view.NextStepsHtml);
            main.Append("</section>\n\n");
        }

        main.Append(view.RetroAffordanceHtml);
        main.Append(view.UndraftedBannerHtml);

        foreach (var card in view.StoryCards)
        {
            AppendStoryCard(main, card);
            toc.Add(new Toc.Entry(2, $"Story {card.Id}", card.AnchorId));
        }

        if (view.RetiredNoticesHtml.Count > 0)
        {
            main.Append("<details class=\"chart-panel retired-section\" id=\"sec-retired\">\n");
            main.Append("  <summary>Retired</summary>\n");
            foreach (var notice in view.RetiredNoticesHtml)
            {
                main.Append($"  {notice}");
            }
            main.Append("</details>\n\n");
            toc.Add(new Toc.Entry(2, "Retired", "sec-retired"));
        }

        // The Work Graph tab shows on EVERY epic page (owner decision): its subgraph when present, an honest empty
        // state otherwise.
        var content = WrapMainInGraphTab(main.ToString(), view.WorkGraph, TabStrip.GroupName($"epic-{view.Number}", "wg"),
            "Choose a view for this epic",
            "This epic's deferred and open action items, traced back to the story, spec, or quick-dev they stemmed from — and forward to whatever resolved them.",
            "No provenance graph for this epic yet — no deferred or action-item work is attributed to it.");

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(content, toc));
        sb.Append("</main>\n\n");
        return sb.ToString();
    }

    private void AppendStoryCard(StringBuilder sb, StoryCardView card)
    {
        sb.Append($"<div class=\"story-card\" id=\"{card.AnchorId}\">\n");
        sb.Append("  <div class=\"story-card-header\">\n");
        sb.Append($"    <span class=\"story-id\">{PathUtil.Html(card.Id)}</span>\n");
        sb.Append($"    <a class=\"story-title story-title-link\" href=\"{PathUtil.Html(card.TitleHref)}\">{card.TitleHtml}</a>\n");

        // Pair status + task badges into one grouped unit ("[In review] · [✓ 5 tasks]") so progress and
        // workflow state read as one coherent fact. Preserve each badge's bytes; wrap only when both present.
        // [Story 8.4; UX-DR23]
        var hasStatus = card.Status is { Length: > 0 };
        var hasTasks = card.TasksTotal > 0;
        if (hasStatus && hasTasks)
        {
            sb.Append("    <span class=\"story-status-pair\">");
            sb.Append(StatusStyles.Badge(card.StatusStage, card.Status!));
            sb.Append("<span class=\"story-status-pair-sep\" aria-hidden=\"true\"> · </span>");
            sb.Append(TaskBadge(card.TasksDone, card.TasksTotal));
            sb.Append("</span>\n");
        }
        else if (hasStatus)
        {
            sb.Append($"    {StatusStyles.Badge(card.StatusStage, card.Status!)}\n");
        }
        else if (hasTasks)
        {
            sb.Append($"    {TaskBadge(card.TasksDone, card.TasksTotal)}\n");
        }

        // Generation-time absolute date only — omit entirely when unresolved (AC #2). [Story 8.8]
        if (card.UpdatedDate is { } updated)
        {
            sb.Append($"    <span class=\"story-card-updated\">Updated {PortalDates.Day(updated)}</span>\n");
        }
        sb.Append("  </div>\n");

        if (card.UserStoryNoteHtml.Length > 0)
        {
            sb.Append($"  {card.UserStoryNoteHtml}");
        }
        sb.Append($"  <div class=\"user-story\">{card.UserStoryHtml}</div>\n");

        // Undrafted: create-story note above AC (match placeholder). Drafted: AC then view-plan link.
        // [spec-epic9-deferred-debt-cleanup]
        if (card.ViewPlanHref is null)
        {
            sb.Append($"  <div class=\"not-detailed-note\">{card.NoteHtml}</div>\n");
        }

        if (card.AcBlocksHtml.Count > 0)
        {
            sb.Append("  <div class=\"ac-label\">Acceptance Criteria</div>\n  <div class=\"ac-list\">\n");
            foreach (var block in card.AcBlocksHtml)
            {
                sb.Append($"    <div class=\"ac-block\">{block}</div>\n");
            }
            sb.Append("  </div>\n");
        }

        foreach (var note in card.TrailingNotesHtml)
        {
            sb.Append($"  {note}");
        }

        if (card.ViewPlanHref is { } viewHref)
        {
            sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(viewHref)}\">View full story plan &rarr;</a>\n");
        }

        sb.Append("</div>\n\n");
    }

    /// <summary>The story card's task indicator. Re-homed from <c>EpicsTemplater.TaskBadge</c>, keyed on the tally.</summary>
    private static string TaskBadge(int tasksDone, int tasksTotal)
    {
        if (tasksDone >= tasksTotal)
        {
            return $"<span class=\"status-badge task-badge complete\">&#10003; {tasksTotal} tasks</span>";
        }
        if (tasksDone == 0)
        {
            return $"<span class=\"status-badge task-badge none-done\">0/{tasksTotal} tasks</span>";
        }
        return $"<span class=\"status-badge task-badge\">{Charts.MiniDonut(tasksDone, tasksTotal)} {tasksDone}/{tasksTotal} tasks</span>";
    }

    /// <summary>Compact Tasks / Tests / Verified pills under the status badge. Reuses
    /// <see cref="TaskBadge"/> for the tasks pill; missing facts render designed empty-state pills
    /// (dashed/muted) rather than omitting the strip. [Story 9.4]</summary>
    private static string EvidenceStrip(StoryEvidence e, bool linkToDevRecord)
    {
        var tasksPill = e.TasksTotal > 0
            ? TaskBadge(e.TasksDone, e.TasksTotal)
            : EmptyEvidencePill("no tasks recorded", Icons.ForConcept("Tasks"));

        var testsPill = e.TestsSummary is { Length: > 0 } summary
            ? $"<span class=\"status-badge evidence-pill tests-pass\">{Icons.ForConcept("Tests")}{PathUtil.Html(summary)}</span>"
            : EmptyEvidencePill("no test evidence recorded", Icons.ForConcept("Tests"));

        string verifiedPill;
        if (e.VerifiedDate is { } date)
        {
            var label = e.VerifiedIsReview ? "verified" : "updated";
            // Story 10.4: every human-facing portal date routes through PortalDates (one date token).
            var dateText = PortalDates.Day(date);
            verifiedPill =
                $"<span class=\"status-badge evidence-pill\">{Icons.ForConcept("Verified")}{PathUtil.Html($"{label} {dateText}")}</span>";
        }
        else
        {
            verifiedPill = EmptyEvidencePill("no verification recorded", Icons.ForConcept("Verified"));
        }

        var devRecordLink = linkToDevRecord
            ? "<a class=\"evidence-dev-record-link\" href=\"#sec-dev-agent-record\" " +
              "aria-label=\"Jump to Dev Agent Record for full verification evidence\">Dev record</a>"
            : string.Empty;
        return $"  <div class=\"evidence-strip\">{tasksPill}{testsPill}{verifiedPill}{devRecordLink}</div>\n";
    }

    private static string EmptyEvidencePill(string label, string icon = "")
        => $"<span class=\"status-badge evidence-pill empty\">{icon}{PathUtil.Html(label)}</span>";

    private static string ChangeSurfaceChartPanel(StoryChangeSurface surface)
    {
        var sb = new StringBuilder();
        sb.Append("<details class=\"chart-panel change-surface-panel\" id=\"sec-change-surface\" open>\n");
        sb.Append("<summary><span class=\"change-surface-title\">Change Surface</span>");
        if (surface.Classifications.Count > 0)
        {
            sb.Append($" <span class=\"change-surface-class\">{PathUtil.Html(string.Join(" + ", surface.Classifications))}</span>");
        }
        else if (surface.ChangedFiles.Count == 0)
        {
            sb.Append(" <span class=\"change-surface-empty\">no file list recorded</span>");
        }
        sb.Append("</summary>\n");
        sb.Append(ChangeSurfacePanelInner(surface));
        sb.Append("</details>\n");
        return sb.ToString();
    }

    /// <summary>Projects the ADR 0007 change footprint — verify guidance and touched files — below Task
    /// Breakdown / Next Steps. [Story 9.4]</summary>
    private static string ChangeSurfacePanelInner(StoryChangeSurface surface)
    {
        var sb = new StringBuilder();
        sb.Append("  <div class=\"change-surface\">\n");

        // Verify — manual checklist first when authors include it; ACs secondary.
        sb.Append("    <div class=\"change-surface-section\">\n");
        sb.Append("      <div class=\"change-surface-section-label\">Verify</div>\n");
        var hasVerifyManual = surface.VerifyBeforeReviewHtml is { Length: > 0 };
        if (hasVerifyManual)
        {
            sb.Append($"      <div class=\"change-surface-verify-manual\">{surface.VerifyBeforeReviewHtml}</div>\n");
        }
        if (surface.VerifyChecklist.Count > 0)
        {
            if (hasVerifyManual)
                sb.Append("      <div class=\"change-surface-section-sublabel\">Acceptance criteria</div>\n");
            sb.Append("      <ul class=\"change-surface-verify\">\n");
            foreach (var (number, plainText) in surface.VerifyChecklist)
            {
                var summary = TruncatePlainText(plainText, 120);
                sb.Append($"        <li><a href=\"#ac-{number}\">AC #{number}</a> — {PathUtil.Html(summary)}</li>\n");
            }
            sb.Append("      </ul>\n");
        }
        else if (!hasVerifyManual)
        {
            sb.Append("      <p class=\"change-surface-empty\">no verification guidance recorded</p>\n");
        }
        sb.Append("    </div>\n");

        // Touched = source/code/other files. Sprint + story artifacts move to Updated chips below.
        var touched = surface.ChangedFiles
            .Where(f => !ChangeSurfaceFileResolver.IsUpdatedArtifact(f.Kind))
            .ToList();
        var updated = surface.ChangedFiles
            .Where(f => ChangeSurfaceFileResolver.IsUpdatedArtifact(f.Kind))
            .ToList();

        sb.Append("    <div class=\"change-surface-section\">\n");
        sb.Append("      <div class=\"change-surface-section-label\">Touched</div>\n");
        if (touched.Count > 0)
        {
            sb.Append("      <ul class=\"change-surface-files\">\n");
            foreach (var file in touched)
            {
                var css = ChangeSurfaceFileResolver.ChangeSurfaceFileKindCssClass(file.Kind);
                if (file.Href is { Length: > 0 } href)
                    sb.Append($"        <li class=\"{css}\"><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(file.Label)}</a></li>\n");
                else
                    sb.Append($"        <li class=\"{css}\">{PathUtil.Html(file.Label)}</li>\n");
            }
            sb.Append("      </ul>\n");
        }
        else if (updated.Count > 0)
        {
            sb.Append("      <p class=\"change-surface-empty\">no source files listed</p>\n");
        }
        else
        {
            sb.Append("      <p class=\"change-surface-empty\">no file list recorded</p>\n");
        }
        sb.Append("    </div>\n");

        if (updated.Count > 0)
        {
            sb.Append("    <div class=\"change-surface-section\">\n");
            sb.Append("      <div class=\"change-surface-section-label\">Updated</div>\n");
            sb.Append("      <div class=\"change-surface-updated\">\n");
            foreach (var file in updated)
            {
                var concept = file.Kind == ChangeSurfaceFileKind.Sprint ? "Sprint Status" : "Story";
                var icon = Icons.ForConcept(concept);
                var chipClass = file.Kind == ChangeSurfaceFileKind.Sprint
                    ? "change-surface-chip change-surface-chip-sprint"
                    : "change-surface-chip change-surface-chip-story";
                if (file.Href is { Length: > 0 } href)
                {
                    sb.Append($"        <a class=\"{chipClass}\" href=\"{PathUtil.Html(href)}\">{icon}" +
                              $"<span class=\"change-surface-chip-label\">{PathUtil.Html(file.Label)}</span></a>\n");
                }
                else
                {
                    sb.Append($"        <span class=\"{chipClass} is-static\">{icon}" +
                              $"<span class=\"change-surface-chip-label\">{PathUtil.Html(file.Label)}</span></span>\n");
                }
            }
            sb.Append("      </div>\n");
            sb.Append("    </div>\n");
        }

        sb.Append("  </div>\n");
        return sb.ToString();
    }

    private static string TruncatePlainText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        var cut = text[..maxChars].TrimEnd();
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > maxChars / 2) cut = cut[..lastSpace];
        return cut + "…";
    }

    // ----- Story page ---------------------------------------------------------------------------------------

    /// <summary>Renders a drafted story page's <c>&lt;main&gt;…&lt;/main&gt;</c> body from its section view model. [Story 6.2]</summary>
    public string RenderStoryBody(StoryPageView view)
    {
        var main = new StringBuilder();
        var toc = new List<Toc.Entry>();

        main.Append("<header class=\"doc-header\">\n");
        // Sibling pager rides the chrome-level wayfinding strip alongside the breadcrumb now (PageView.Pager),
        // not the body's own header. [Story 10.11]
        main.Append("  <div class=\"kicker-row\">\n");
        main.Append($"    <span class=\"story-kicker\">Story {PathUtil.Html(view.Id)}</span>\n");
        if (view.Status is { Length: > 0 } status)
        {
            main.Append($"    {StatusStyles.Badge(view.StatusStage, status)}\n");
        }
        main.Append(StatusStyles.LegendKey());
        main.Append(view.RetroLinkHtml);
        main.Append("  </div>\n");
        // Evidence strip — compact pills only in the header; change-surface panel sits below charts. [Story 9.4]
        if (view.Status is { Length: > 0 })
        {
            main.Append("  <div class=\"evidence-block\">\n");
            main.Append(EvidenceStrip(view.Evidence, view.DevAgentRecord.Count > 0));
            main.Append("  </div>\n");
        }
        main.Append($"  <h1>{view.TitleHtml}</h1>\n");
        main.Append("</header>\n\n");

        if (view.BlurbHtml.Length > 0)
        {
            main.Append($"<div class=\"story-lead user-story\" id=\"sec-user-story\">{view.BlurbHtml}</div>\n\n");
            toc.Add(new Toc.Entry(2, "User Story", "sec-user-story"));
        }

        main.Append("<section class=\"dashboard-narrow\">\n<div class=\"chart-row\">\n");
        if (view.Tasks.Count > 0 || view.DeferredFromThis.Count > 0)
        {
            main.Append("<div class=\"chart-panel sunburst-panel\" id=\"sec-task-breakdown\">\n<h3>Task Breakdown</h3>\n");
            main.Append(Charts.TaskSunburst(view.Tasks, deferred: view.DeferredFromThis));
            main.Append("</div>\n");
            toc.Add(new Toc.Entry(2, "Task Breakdown", "sec-task-breakdown"));
        }
        main.Append(view.NextStepsHtml);
        main.Append("</div>\n");

        if (view.Status is { Length: > 0 })
        {
            main.Append(ChangeSurfaceChartPanel(view.ChangeSurface));
            toc.Add(new Toc.Entry(2, "Change Surface", "sec-change-surface"));
        }

        if (view.AcceptanceCriteria.Count > 0)
        {
            main.Append("<div class=\"chart-panel ac-panel\" id=\"sec-acceptance-criteria\">\n<h3>Acceptance Criteria</h3>\n<div class=\"ac-criteria\">\n");
            foreach (var ac in view.AcceptanceCriteria)
            {
                main.Append($"  <div class=\"ac-criterion\" id=\"ac-{ac.Number}\">\n");
                main.Append($"    <a class=\"ac-anchor\" href=\"#ac-{ac.Number}\">AC #{ac.Number}</a>\n");
                main.Append($"    <div class=\"ac-criterion-body\">{ac.Html}</div>\n");
                main.Append("  </div>\n");
            }
            main.Append("</div>\n</div>\n");
            toc.Add(new Toc.Entry(2, "Acceptance Criteria", "sec-acceptance-criteria"));
        }

        if (view.DevAgentRecord.Count > 0)
        {
            main.Append("<details class=\"chart-panel dev-agent-details\" id=\"sec-dev-agent-record\">\n<summary>Dev Agent Record</summary>\n<table class=\"dev-agent-table\">\n");
            foreach (var entry in view.DevAgentRecord)
            {
                main.Append($"  <tr><th>{PathUtil.Html(entry.Label)}</th><td>{entry.ContentHtml}</td></tr>\n");
            }
            main.Append("</table>\n</details>\n");
            toc.Add(new Toc.Entry(2, "Dev Agent Record", "sec-dev-agent-record"));
        }

        var deferredPanel = FollowUpRow.RenderDeferredFromArtifactPanel(
            view.DeferredFromThis, deferredListHref: view.DeferredListHref);
        if (deferredPanel.Length > 0)
        {
            main.Append(deferredPanel);
            toc.Add(new Toc.Entry(2, "Deferred from this artifact", "sec-deferred-from-artifact"));
        }
        main.Append("</section>\n\n");

        if (view.ReviewFindingsHtml.Length > 0)
        {
            main.Append("<section class=\"chart-panel review-findings\" id=\"sec-review-findings\">\n<h3>Review Findings</h3>\n");
            main.Append($"<div class=\"doc-body\">{view.ReviewFindingsHtml}</div>\n</section>\n\n");
            toc.Add(new Toc.Entry(2, "Review Findings", "sec-review-findings"));
        }

        main.Append("<article class=\"doc-body epic-card\">\n");
        main.Append(view.RemainderHtml);
        main.Append("\n</article>\n\n");
        toc.AddRange(Toc.ExtractHeadings(view.RemainderHtml));

        if (view.ChangeLogHtml.Length > 0)
        {
            main.Append("<section class=\"chart-panel change-log\" id=\"sec-change-log\">\n<h3>Change Log</h3>\n");
            main.Append($"<div class=\"doc-body\">{view.ChangeLogHtml}</div>\n</section>\n\n");
            toc.Add(new Toc.Entry(2, "Change Log", "sec-change-log"));
        }

        // The Work Graph tab shows on EVERY drafted story page (owner decision): its story-scoped subgraph when
        // present, an honest empty state otherwise.
        var content = WrapMainInGraphTab(main.ToString(), view.WorkGraph, TabStrip.GroupName($"story-{view.Id}", "wg"),
            "Choose a view for this story",
            "Deferred work that stemmed from this story, and what resolved it.",
            "No provenance graph for this story yet — nothing has been deferred from it.");

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(content, toc));
        sb.Append("</main>\n\n");
        return sb.ToString();
    }

    /// <summary>Wraps a detail page's INNER main content (header + sections, BEFORE
    /// <see cref="Toc.WrapWithSidebar"/> adds the page-shell/rail wrapper) in the standard
    /// <see cref="TabStrip"/>: the header stays outside, the sections become the "Overview" panel, and the
    /// "Work Graph" panel shows <paramref name="graph"/> — or <paramref name="emptyMessage"/> when it is null, so
    /// the tab is a consistent, discoverable control on EVERY epic/story page (owner decision). Splits at the
    /// FIRST <c>&lt;/header&gt;</c> — safe here because at this stage the header is a top-level sibling (no grid/
    /// main wrapper to unbalance). If none is found, the content is returned unchanged. [Story 19.2]</summary>
    private static string WrapMainInGraphTab(
        string content, WorkGraphEpic? graph, string group, string legend, string leadIn, string emptyMessage)
    {
        const string marker = "</header>";
        var idx = content.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return content;
        var split = idx + marker.Length;
        var header = content[..split];
        var overview = content[split..];
        var tabs = TabStrip.Render(group, legend, new[]
        {
            new TabStrip.Tab("overview", "Overview", Icons.ForConcept("Overview"), overview),
            new TabStrip.Tab("graph", "Work Graph", Icons.ForConcept("Work Graph"),
                WorkGraphTemplater.RenderEmbedded(graph, leadIn, emptyMessage)),
        });
        return header + "\n" + tabs;
    }

    // ----- Story placeholder --------------------------------------------------------------------------------

    /// <summary>Renders a story placeholder page's <c>&lt;main&gt;…&lt;/main&gt;</c> body from its section view
    /// model. [Story 6.2]</summary>
    public string RenderStoryPlaceholderBody(StoryPlaceholderView view)
    {
        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        // Sibling pager rides the chrome-level wayfinding strip alongside the breadcrumb now (PageView.Pager),
        // not the body's own header. [Story 10.11]
        sb.Append("  <div class=\"kicker-row\">\n");
        sb.Append($"    <span class=\"story-kicker\">Story {PathUtil.Html(view.Id)}</span>\n");
        sb.Append($"    {StatusStyles.Badge(view.StatusStage, "Not yet drafted")}\n");
        sb.Append(StatusStyles.LegendKey());
        sb.Append(view.RetroLinkHtml);
        sb.Append("  </div>\n");
        sb.Append($"  <h1>{view.TitleHtml}</h1>\n");
        sb.Append("</header>\n\n");

        if (view.UserStoryNoteHtml.Length > 0)
        {
            sb.Append($"<div class=\"story-lead\">{view.UserStoryNoteHtml}</div>\n\n");
        }

        if (view.UserStoryHtml.Length > 0)
        {
            sb.Append($"<div class=\"story-lead user-story\">{view.UserStoryHtml}</div>\n\n");
        }

        // Create-story action sits above AC so the primary next step is visible before the criteria dump.
        sb.Append($"<div class=\"epic-card\">\n  <div class=\"pending-note\">{view.NoteHtml}</div>\n</div>\n\n");

        if (view.AcBlocksHtml.Count > 0)
        {
            sb.Append("<section class=\"dashboard-narrow\">\n");
            sb.Append("<div class=\"chart-panel ac-panel\">\n<h3>Acceptance Criteria</h3>\n<div class=\"ac-list\">\n");
            foreach (var block in view.AcBlocksHtml)
            {
                sb.Append($"  <div class=\"ac-block\">{block}</div>\n");
            }
            sb.Append("</div>\n</div>\n</section>\n\n");
        }

        foreach (var note in view.TrailingNotesHtml)
        {
            sb.Append($"<div class=\"story-lead\">{note}</div>\n\n");
        }

        sb.Append("<section class=\"dashboard-narrow\">\n");
        sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(view.BackHref)}\">&larr; Back to Epic {view.EpicNumber}</a>\n");
        sb.Append("</section>\n");
        sb.Append("</main>\n\n");
        return sb.ToString();
    }
}
