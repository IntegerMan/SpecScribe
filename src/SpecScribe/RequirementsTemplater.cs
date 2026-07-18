using System.Text;

namespace SpecScribe;

/// <summary>Renders the two requirements page types: the requirements index (FRs, NFRs, and UX-DRs, with
/// progress donuts) and one detail page per requirement (definition, covering epic + its progress,
/// and derived status). Mirrors <see cref="EpicsTemplater"/>.</summary>
public static class RequirementsTemplater
{
    public static string RenderIndex(RequirementsModel model, EpicsModel epics, ProgressModel progress, SiteNav nav,
        ProjectCounts? counts = null)
    {
        var outputPath = SiteNav.RequirementsOutputPath;
        // Prefer the shared ledger; fall back to an ephemeral Build so unit tests that omit counts stay correct.
        var sat = (counts ?? ProjectCounts.Build(progress, sprint: null, WorkInventory.Empty, epics, model))
            .RequirementsOverall;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Requirements — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + ForgeOptions.StylesheetName, PathUtil.RelativePrefix(outputPath) + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Requirements", null) }));

        var countParts = new List<string>
        {
            $"{model.Functional.Count} functional",
        };
        if (model.NonFunctional.Count > 0)
        {
            countParts.Add($"{model.NonFunctional.Count} non-functional");
        }
        if (model.Design.Count > 0)
        {
            countParts.Add($"{model.Design.Count} design");
        }

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Requirements");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {string.Join(" &middot; ", countParts)}</div>\n");
        sb.Append("</header>\n\n");

        // Category groups in source order (LINQ GroupBy preserves first-seen key order), plus the NFRs
        // as one final group — the single ordered list that drives both the navigator and the sections
        // below, so their anchors always line up. UX-DRs live in the dedicated coverage section below,
        // not in this FR-scoped navigator (keeps FR flow/grid byte-identical). [Story 9.2]
        var groups = model.Functional
            .GroupBy(r => r.Category ?? "Functional Requirements")
            .Select(g => (Label: g.Key, Items: (IReadOnlyList<RequirementInfo>)g.ToList()))
            .ToList();
        if (model.NonFunctional.Count > 0)
        {
            groups.Add(("Non-Functional Requirements", model.NonFunctional));
        }

        sb.Append("<main id=\"main-content\" class=\"req-index\">\n\n");

        // Holistic four-reading summary over Everything (FR+NFR+UX-DR) — additive above the donut row.
        // Absent when no requirements (NFR8). Does not re-render or replace the FR+NFR grid/flow. [Story 9.9]
        AppendSatisfactionBand(sb, sat, model);

        sb.Append("<section class=\"dashboard\">\n<div class=\"chart-row\">\n");
        // Overall six-tier donut over Everything leads the row: same vocabulary as the Sankey / per-kind
        // donuts; fills the 2×2 fourth cell. Segments read the ProjectCounts ledger (not a local recount).
        // [Story 9.9 + review patch]
        if (sat.Total > 0 && (model.NonFunctional.Count > 0 || model.Design.Count > 0))
        {
            AppendSatisfactionDonut(sb, "Overall", sat);
        }
        AppendStatusDonut(sb, "Functional", model.Functional);
        // Non-functional / Design donuts absent (not empty) when the project has none — NFR8. [Story 9.2]
        if (model.NonFunctional.Count > 0)
        {
            AppendStatusDonut(sb, "Non-functional", model.NonFunctional);
        }
        if (model.Design.Count > 0)
        {
            AppendStatusDonut(sb, "Design", model.Design);
        }
        sb.Append("</div>\n</section>\n\n");

        // Requirements at a glance: every FR/NFR as a colorized status block (AC #1), then the Sankey-style flow
        // of ALL requirements from definition → epic coverage → implementation state (AC #2). The blocks
        // and the requirement cards below are the flow's text equivalent (AC #3, never diagram-only).
        var allReqs = model.Functional.Concat(model.NonFunctional).ToList();
        if (allReqs.Count > 0)
        {
            sb.Append("<div class=\"section-divider\" id=\"at-a-glance\">Requirements at a glance</div>\n");
            sb.Append("<div class=\"chart-panel\">\n");
            sb.Append(Charts.RequirementStatusGrid(allReqs, prefix: string.Empty));
            sb.Append("</div>\n\n");

            // Gated on allReqs (FR+NFR), matching RequirementFlow's actual scope — an NFR-only project must
            // not silently lose the flow panel just because it has zero FRs.
            sb.Append("<div class=\"section-divider\">Requirements flow</div>\n");
            sb.Append("<div class=\"chart-panel\">\n");
            sb.Append(Charts.RequirementFlow(model, epics));
            sb.Append("</div>\n\n");
        }

        // Non-functional & design coverage — additive section below the FR-scoped flow. Per-item verification
        // treatment (not FR "delivered-by-story" semantics). [Story 9.2 Tasks 3–4]
        AppendNfrUxDrCoverageSection(sb, model, epics);

        // Jump-to-group navigator: one clickable card per group, each a status pie chart, anchoring down
        // to its section below.
        if (groups.Count > 0)
        {
            sb.Append("<div class=\"section-divider\">Jump to a group</div>\n");
            sb.Append("<div class=\"req-group-grid\">\n");
            foreach (var (label, items) in groups)
            {
                AppendGroupCard(sb, label, items);
            }
            sb.Append("</div>\n\n");
        }

        // The grouped requirement lists, each divider carrying the anchor its navigator card links to.
        foreach (var (label, items) in groups)
        {
            sb.Append($"<div class=\"section-divider\" id=\"{PathUtil.Html(GroupAnchor(label))}\">{PathUtil.Html(label)}</div>\n\n");
            foreach (var req in items)
            {
                AppendRequirementCard(sb, req, prefix: string.Empty);
            }
        }

        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Four-reading "Satisfaction at a glance" band over <see cref="RequirementsModel.Everything"/>.
    /// Stacked bar = six canonical tiers; chips = Satisfied / In flight / Deferred / Unmapped. [Story 9.9]</summary>
    private static void AppendSatisfactionBand(
        StringBuilder sb, ProjectCounts.RequirementSatisfaction sat, RequirementsModel model)
    {
        if (sat.Total <= 0) return;

        // Chip targets (review 2026-07-17): prefer #at-a-glance whenever FR+NFR exist; otherwise coverage
        // (Design-only / NFR-only without FR glance) or the band itself. Never leave Satisfied/In-flight unlinked
        // when the band is showing. [Story 9.9]
        var hasCoverage = model.NonFunctional.Count > 0 || model.Design.Count > 0;
        var detailHref = model.All.Any()
            ? "#at-a-glance"
            : hasCoverage ? "#nfr-uxdr-coverage" : "#satisfaction";

        sb.Append("<div class=\"section-divider\" id=\"satisfaction\">Satisfaction at a glance</div>\n");
        sb.Append("<section class=\"satisfaction-band chart-panel\" aria-label=\"Requirement satisfaction summary\">\n");
        sb.Append(Charts.RequirementSatisfactionBar(sat));
        sb.Append(Charts.RequirementSatisfactionChips(
            sat,
            satisfiedHref: detailHref,
            inFlightHref: detailHref,
            deferredHref: detailHref,
            unmappedHref: detailHref));
        // Names the rollup so the four readings read as a grouping of the six canonical tiers (which the bar's
        // In-flight bracket and the Overall donut below both show in full), not a parallel vocabulary. [Story 9.9]
        sb.Append("<p class=\"satisfaction-note\">A rollup of the six status tiers over all "
            + $"{sat.Total} requirements — <strong>In flight</strong> groups partially implemented, ready for dev, and planned. "
            + "The donuts and flow below break the same requirements down tier by tier.</p>\n");
        sb.Append("</section>\n\n");
    }

    public static string RenderRequirement(RequirementInfo req, ProgressModel progress, SiteNav nav, EpicsModel epics,
        IReadOnlyDictionary<int, string>? epicRetroMap = null, string? deferredWorkHref = null)
    {
        var outputPath = $"requirements/{req.Slug}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);
        var kindLabel = req.Kind switch
        {
            RequirementKind.Functional => "Functional Requirement",
            RequirementKind.Design => "UX Design Requirement",
            _ => "Non-Functional Requirement",
        };

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"{req.Id} — {nav.SiteTitle}", prefix + ForgeOptions.StylesheetName, prefix + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Requirements", SiteNav.RequirementsOutputPath),
            (req.Id, null),
        }));

        // Single <main id="main-content"> landmark / skip-link target for the requirement detail page. [Story 1.4 AC #1]
        // req-detail stretches the page to the same 1100px column as the index. [Story 9.2 UX]
        sb.Append("<main id=\"main-content\" class=\"req-detail\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <div class=\"story-kicker\">{PathUtil.Html(kindLabel)}</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(req.Id)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.RequirementBadge(req)}");
        sb.Append(StatusStyles.LegendKey());
        if (req.Category is { Length: > 0 } cat)
        {
            sb.Append($"<span class=\"pill\">{PathUtil.Html(cat)}</span>");
        }
        sb.Append("</div>\n</header>\n\n");

        sb.Append($"<div class=\"story-lead\"><p>{req.TextHtml}</p></div>\n\n");

        // Resolve every covering epic (not just the primary), in coverage order, skipping numbers with no
        // matching epic — the same best-effort resolution as RequirementsParser.StoriesFor. This also fixes the
        // pre-existing gap where a multi-epic requirement (e.g. "Epics 1 & 2") showed only the primary epic.
        var byNumber = epics.Epics.ToDictionary(e => e.Number);
        var coveringEpics = req.CoverageEpicNumbers
            .Where(byNumber.ContainsKey)
            .Select(n => byNumber[n])
            .ToList();

        sb.Append("<div class=\"epic-card\">\n  <h3>Coverage</h3>\n");

        // --- Branch A: deferred on purpose (kept structurally separate from the unmapped branch below so Story
        //     9.3 can give the two DISTINCT visual treatment + link the deferred item to its deferral source;
        //     9.1 distinguishes them in COPY only — do not merge these branches). [seam: Story 9.3] ---
        if (req.Deferred)
        {
            sb.Append($"  <p class=\"pending-note\">{StatusStyles.RequirementBadge(req)} Deferred — not yet assigned to an epic.</p>\n");
            if (req.CoverageNote is { Length: > 0 } dn)
            {
                sb.Append($"  <p class=\"epic-goal\">{PathUtil.Html(dn)}</p>\n");
            }
            AppendDeferralSourceLinks(sb, req, epicRetroMap, deferredWorkHref, prefix);
        }
        // --- Branch B: covered by one or more resolvable epics — list every covering epic's stories, grouped by epic. ---
        else if (coveringEpics.Count > 0)
        {
            // Honest framing: the FR Coverage Map is epic-level, so these are the stories in the covering
            // epic(s), NOT a per-requirement-mapped subset. Grouping by epic makes that granularity visible.
            sb.Append("  <p class=\"epic-goal\">Stories in the covering epic(s), grouped by epic — the coverage map is epic-level.</p>\n");

            foreach (var epic in coveringEpics)
            {
                var epicProgress = progress.PerEpic.FirstOrDefault(p => p.Number == epic.Number);
                sb.Append("  <div class=\"coverage-group\">\n");
                sb.Append("    <div class=\"coverage-cards\">\n");
                AppendCoverageCard(sb, epic, epicProgress, prefix);
                sb.Append("    </div>\n");

                if (epic.Stories.Count == 0)
                {
                    sb.Append("    <p class=\"pending-note\">No stories drafted in this epic yet.</p>\n");
                }
                else
                {
                    sb.Append("    <div class=\"coverage-story-cards\">\n");
                    foreach (var story in epic.Stories)
                    {
                        AppendStoryCard(sb, story, prefix);
                    }
                    sb.Append("    </div>\n");
                }
                sb.Append("  </div>\n");
            }

            if (req.CoverageNote is { Length: > 0 } note)
            {
                sb.Append($"  <p class=\"epic-goal coverage-note\">{PathUtil.Html(note)}</p>\n");
            }
        }
        // --- Branch C: not deferred and no covering epic NAMED — genuinely UNMAPPED. Gate on
        //     CoverageEpicNumbers.Count (author intent), not on resolved coveringEpics — a map line that names
        //     only missing epics is Planned (DeriveStatus), not Unmapped. [Story 9.1 review patch] ---
        else if (req.CoverageEpicNumbers.Count == 0)
        {
            sb.Append($"  <p class=\"pending-note\">{StatusStyles.RequirementBadge(req)} Not yet mapped to any epic or story.</p>\n");
        }
        // --- Branch D: coverage map named epic number(s) but none resolve in the model (typo / removed epic).
        //     Distinct from Unmapped — author intent to map exists; status stays Planned. ---
        else
        {
            sb.Append($"  <p class=\"pending-note\">{StatusStyles.RequirementBadge(req)} Covering epic(s) named in the map were not found in the epic list.</p>\n");
            if (req.CoverageNote is { Length: > 0 } phantomNote)
            {
                sb.Append($"  <p class=\"epic-goal coverage-note\">{PathUtil.Html(phantomNote)}</p>\n");
            }
        }
        sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(prefix + SiteNav.RequirementsOutputPath)}\">&larr; All requirements</a>\n");
        sb.Append("</div>\n\n");

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Best-effort deferral-source links for a deferred requirement (AC #2): when its free-text
    /// <see cref="RequirementInfo.CoverageNote"/> names an "Epic N" whose retrospective page exists, link to that
    /// retro; when the note reads as deferred/debt language and a deferred-work backlog page exists, link to it.
    /// Reuses the SAME <see cref="DeferralHeuristics"/> pattern <see cref="ActionItemsTemplater"/> ships — no new
    /// authoring schema. Renders nothing when nothing resolves (explicitly optional — AC #2's "when one exists");
    /// never fabricates a link or throws on a missing target. The hrefs (retro / deferred-work paths) are
    /// output-root-relative, so they are prefixed to the requirement detail page's depth. [Story 9.3 Task 5]</summary>
    private static void AppendDeferralSourceLinks(
        StringBuilder sb, RequirementInfo req, IReadOnlyDictionary<int, string>? epicRetroMap, string? deferredWorkHref, string prefix)
    {
        var note = req.CoverageNote;
        if (note is not { Length: > 0 }) return;

        var links = new List<string>();

        if (DeferralHeuristics.EpicMention(note) is { } epicNumber &&
            epicRetroMap is not null && epicRetroMap.TryGetValue(epicNumber, out var retroPath) &&
            retroPath is { Length: > 0 })
        {
            var href = prefix + PathUtil.NormalizeSlashes(retroPath);
            links.Add($"<a class=\"deferral-source-link\" href=\"{PathUtil.Html(href)}\">From Epic {epicNumber} retrospective &rarr;</a>");
        }

        if (deferredWorkHref is { Length: > 0 } dw && DeferralHeuristics.IsDebtRelated(note))
        {
            var href = prefix + PathUtil.NormalizeSlashes(dw);
            links.Add($"<a class=\"deferral-source-link\" href=\"{PathUtil.Html(href)}\">In deferred-work backlog &rarr;</a>");
        }

        if (links.Count == 0) return;

        sb.Append("  <p class=\"deferral-sources\">Deferral source: ");
        sb.Append(string.Join(" &middot; ", links));
        sb.Append("</p>\n");
    }

    /// <summary>The covering epic rendered as a linked card: a task-completion donut (falling back to
    /// story-detail coverage, then an empty ring for a pending epic), the epic's status badge, and a
    /// one-line tally — the same visual language as the home page's epic mosaic.</summary>
    private static void AppendCoverageCard(StringBuilder sb, EpicInfo epic, EpicProgress? ep, string prefix)
    {
        var statusClass = StatusStyles.ForEpic(epic);
        var href = $"{prefix}epics/epic-{epic.Number}.html";

        sb.Append($"    <a class=\"epic-mosaic-card coverage-card {statusClass}\" href=\"{PathUtil.Html(href)}\">\n");
        sb.Append("      <div class=\"epic-mosaic-donut\">\n");
        if (ep is { TasksTotal: > 0 })
        {
            sb.Append(Charts.Donut(new (string, int, string)[]
            {
                ("Done", ep.TasksDone, "done"),
                ("Remaining", ep.TasksTotal - ep.TasksDone, "pending"),
            }, size: 72));
        }
        else if (ep is { StoryCount: > 0 })
        {
            sb.Append(Charts.Donut(new (string, int, string)[]
            {
                // "Detailed" = has a task plan (ready), not finished — gold, never green.
                ("Detailed", ep.StoriesWithArtifact, "ready"),
                ("Not yet detailed", ep.StoryCount - ep.StoriesWithArtifact, "pending"),
            }, size: 72));
        }
        else
        {
            sb.Append(Charts.Donut(Array.Empty<(string, int, string)>(), size: 72));
        }
        sb.Append("      </div>\n");

        sb.Append("      <div class=\"epic-mosaic-label\">\n");
        sb.Append($"        <span class=\"epic-mosaic-num\">Epic {epic.Number}</span>\n");
        sb.Append($"        <span class=\"epic-mosaic-title\">{epic.Title}</span>\n");
        sb.Append($"        {StatusStyles.Badge(statusClass, StatusStyles.EpicLabel(statusClass))}\n");
        var tally = ep switch
        {
            { TasksTotal: > 0 } => $"{ep.TasksDone} / {ep.TasksTotal} tasks · {ep.StoriesWithArtifact} / {ep.StoryCount} stories detailed",
            { StoryCount: > 0 } => $"{ep.StoriesWithArtifact} / {ep.StoryCount} stories detailed",
            _ => "Not yet drafted",
        };
        sb.Append($"        <span class=\"epic-mosaic-sub\">{PathUtil.Html(tally)}</span>\n");
        sb.Append("      </div>\n    </a>\n");
    }

    /// <summary>One story rendered as a compact card under its covering-epic group: a small task-completion
    /// ring, the story id + linked title, and a canonical status badge (color + icon + word). Reuses the
    /// epic-mosaic card layout and the shared status vocabulary — every story always has a page (real artifact
    /// or generated placeholder), so the title is never a dead-ended plain string.</summary>
    private static void AppendStoryCard(StringBuilder sb, StoryInfo story, string prefix)
    {
        var statusClass = StatusStyles.ForStory(story);
        var label = StatusStyles.StoryLabel(statusClass);
        var href = prefix + (story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id));
        var donutAria = story.TasksTotal > 0
            ? $"Story {story.Id} tasks: {story.TasksDone} of {story.TasksTotal} done"
            : $"Story {story.Id}: no task plan yet";

        sb.Append($"      <a class=\"epic-mosaic-card coverage-story-card {statusClass}\" href=\"{PathUtil.Html(href)}\">\n");
        sb.Append("        <div class=\"epic-mosaic-donut\">\n");
        sb.Append(Charts.Donut(new (string, int, string)[]
        {
            ("Done", story.TasksDone, "done"),
            ("Remaining", Math.Max(0, story.TasksTotal - story.TasksDone), "pending"),
        }, size: 64, ariaLabel: donutAria, showCenterText: false));
        sb.Append("        </div>\n");
        sb.Append("        <div class=\"epic-mosaic-label\">\n");
        sb.Append($"          <span class=\"epic-mosaic-num\">Story {PathUtil.Html(story.Id)}</span>\n");
        sb.Append($"          <span class=\"epic-mosaic-title\">{story.Title}</span>\n");
        sb.Append($"          {StatusStyles.Badge(statusClass, label)}\n");
        sb.Append("        </div>\n      </a>\n");
    }

    /// <summary>Dedicated "Non-functional &amp; design coverage" section — per-item verification treatment for
    /// NFRs and UX-DRs. Status badges read through the real <see cref="RequirementStatus"/> enum (including the
    /// Story 9.3 <see cref="RequirementStatus.Unmapped"/> tier) via <see cref="StatusStyles.RequirementBadge"/> —
    /// one "Not yet mapped" implementation portal-wide, not a section-local special-case. [Story 9.2 Tasks 3–4;
    /// Story 9.3 Task 6]</summary>
    private static void AppendNfrUxDrCoverageSection(StringBuilder sb, RequirementsModel model, EpicsModel epics)
    {
        var hasNfr = model.NonFunctional.Count > 0;
        var hasDesign = model.Design.Count > 0;
        if (!hasNfr && !hasDesign) return;

        sb.Append("<div class=\"section-divider\" id=\"nfr-uxdr-coverage\">Non-functional &amp; design coverage</div>\n");
        sb.Append("<div class=\"chart-panel nfr-uxdr-coverage\">\n");
        // Section-level verification approach (AC #2) — a bare per-item badge is never the only signal.
        sb.Append("<p class=\"epic-goal\">Cross-cutting obligations verified across the codebase by tests and architectural invariants, tracked here by the epics that deliver them. Coverage is epic-level — a badge reflects the delivering epic's roll-up, not a per-requirement claim.</p>\n");

        // Once per coverage section (not per row, not per subgroup). [Story 9.2 deferred]
        var byNumber = epics.Epics.ToDictionary(e => e.Number);

        if (hasNfr)
        {
            AppendCoverageSubGroup(sb, "Non-functional requirements", model.NonFunctional, byNumber);
        }
        if (hasDesign)
        {
            AppendCoverageSubGroup(sb, "UX design requirements", model.Design, byNumber);
        }

        sb.Append("</div>\n\n");
    }

    private static void AppendCoverageSubGroup(
        StringBuilder sb, string label, IReadOnlyList<RequirementInfo> reqs,
        IReadOnlyDictionary<int, EpicInfo> byNumber)
    {
        sb.Append($"<h3 class=\"nfr-uxdr-subgroup-title\">{PathUtil.Html(label)}</h3>\n");
        foreach (var req in reqs)
        {
            AppendCoverageRow(sb, req, byNumber, prefix: string.Empty);
        }
    }

    /// <summary>One NFR/UX-DR coverage row: id + badge, then description, then a "Delivered by" epic card
    /// list (never jammed into the header). Reuses req-card / status-badge vocabulary. [Story 9.2 Task 4]</summary>
    private static void AppendCoverageRow(
        StringBuilder sb, RequirementInfo req, IReadOnlyDictionary<int, EpicInfo> byNumber, string prefix)
    {
        var href = $"{prefix}requirements/{req.Slug}.html";

        // Story 9.3 Task 6: retired 9.2's section-local Deferred/"Not yet mapped" special-casing. RequirementStatus
        // now carries a real Unmapped tier (DeriveStatus derives it from empty CoverageEpicNumbers + not deferred),
        // so these NFR/UX-DR rows read the SAME canonical badge + class as every other requirement surface — one
        // "Not yet mapped" implementation, not two. Unmapped now sits in the tan pending family (owner decision #1)
        // instead of borrowing the grey deferred color; Deferred keeps its own grey. [Story 9.3 Task 6]
        var badgeHtml = StatusStyles.RequirementBadge(req);
        var rowClass = StatusStyles.ForRequirement(req);

        sb.Append($"<div class=\"req-card nfr-uxdr-row {rowClass}\" id=\"cov-{PathUtil.Html(req.Slug)}\">\n");
        sb.Append("  <div class=\"req-card-head\">\n");
        sb.Append($"    <a class=\"req-id-link\" href=\"{PathUtil.Html(href)}\">{PathUtil.Html(req.Id)}</a>\n");
        sb.Append($"    {badgeHtml}\n");
        sb.Append("  </div>\n");
        sb.Append($"  <div class=\"req-text\">{req.TextHtml}</div>\n");

        // Covering epics (or honest absence) sit AFTER the description as a labeled list of cards —
        // not as header chips. [Story 9.2 UX follow-up]
        sb.Append("  <div class=\"nfr-uxdr-epics\">\n");
        if (req.Deferred)
        {
            sb.Append("    <p class=\"nfr-uxdr-epics-note\">Deferred — not yet assigned to a delivering epic.");
            if (req.CoverageNote is { Length: > 0 } note)
            {
                sb.Append($" {PathUtil.Html(note)}");
            }
            sb.Append("</p>\n");
        }
        else if (req.CoverageEpicNumbers.Count == 0)
        {
            sb.Append("    <p class=\"nfr-uxdr-epics-note\">Not yet mapped to a delivering epic.</p>\n");
        }
        else
        {
            var resolved = req.CoverageEpicNumbers
                .Where(byNumber.ContainsKey)
                .Select(n => byNumber[n])
                .ToList();
            if (resolved.Count == 0)
            {
                // Named covering epic(s) but none resolve — avoid an empty "Delivered by" list.
                // Status stays Planned (author intent to map; Story 9.3). [Story 9.2 review]
                sb.Append("    <p class=\"nfr-uxdr-epics-note\">Covering epic not found in the epic list.</p>\n");
            }
            else
            {
                sb.Append("    <div class=\"nfr-uxdr-epics-label\">Delivered by</div>\n");
                sb.Append("    <ul class=\"nfr-uxdr-epic-list\">\n");
                foreach (var epic in resolved)
                {
                    var epicHref = $"{prefix}epics/epic-{epic.Number}.html";
                    var statusClass = StatusStyles.ForEpic(epic);
                    sb.Append($"      <li><a class=\"nfr-uxdr-epic-card {statusClass}\" href=\"{PathUtil.Html(epicHref)}\">");
                    sb.Append($"<span class=\"nfr-uxdr-epic-num\">Epic {epic.Number}</span>");
                    sb.Append($"<span class=\"nfr-uxdr-epic-title\">{epic.Title}</span>");
                    sb.Append($"{StatusStyles.Badge(statusClass, StatusStyles.EpicLabel(statusClass))}");
                    sb.Append("</a></li>\n");
                }
                sb.Append("    </ul>\n");
            }
        }
        sb.Append("  </div>\n");
        sb.Append("</div>\n\n");
    }

    private static void AppendRequirementCard(StringBuilder sb, RequirementInfo req, string prefix)
    {
        var statusClass = StatusStyles.ForRequirement(req);
        var href = $"{prefix}requirements/{req.Slug}.html";

        sb.Append($"<div class=\"req-card {statusClass}\" id=\"{PathUtil.Html(req.Slug)}\">\n");
        sb.Append("  <div class=\"req-card-head\">\n");
        sb.Append($"    <a class=\"req-id-link\" href=\"{PathUtil.Html(href)}\">{PathUtil.Html(req.Id)}</a>\n");
        sb.Append($"    {StatusStyles.RequirementBadge(req)}\n");

        if (req.Deferred)
        {
            sb.Append("    <span class=\"req-epic deferred\">Deferred</span>\n");
        }
        else if (req.CoverageEpicNumber is { } epicNumber)
        {
            sb.Append($"    <a class=\"req-epic\" href=\"{PathUtil.Html(prefix)}epics/epic-{epicNumber}.html\">Epic {epicNumber}</a>\n");
        }
        else
        {
            // Genuinely unmapped (not deferred, no covering epic): today this else was silent, leaving the card
            // with no coverage chip at all. Flag it explicitly so an unmapped requirement is visibly called out,
            // mirroring the deferred chip's shape but in the pending/tan family (owner decision #1). [Story 9.3 Task 4]
            sb.Append("    <span class=\"req-epic unmapped\">Not yet mapped</span>\n");
        }
        sb.Append("  </div>\n");

        sb.Append($"  <div class=\"req-text\">{req.TextHtml}</div>\n");
        sb.Append("</div>\n\n");
    }

    private static void AppendStatusDonut(StringBuilder sb, string label, IReadOnlyList<RequirementInfo> reqs)
    {
        var (done, active, ready, planned, unmapped, deferred) = StatusCounts(reqs);

        sb.Append("<div class=\"chart-panel\">\n");
        sb.Append($"<h3>{PathUtil.Html(label)} ({reqs.Count})</h3>\n<div class=\"donut-and-legend\">\n");
        var statusSegments = StatusSegments(reqs);
        sb.Append(Charts.Donut(statusSegments, ariaLabel: $"{label} requirements: {done} done, {active} partially implemented, {ready} ready for dev, {planned} planned, {unmapped} not yet mapped, {deferred} deferred"));
        sb.Append(Charts.DonutLegend(statusSegments));
        sb.Append("</div>\n</div>\n\n");
    }

    /// <summary>Overall (or other ledger-backed) donut — six tiers from <see cref="ProjectCounts.RequirementSatisfaction"/>
    /// so the donut cannot drift from the satisfaction band. [Story 9.9 review]</summary>
    private static void AppendSatisfactionDonut(
        StringBuilder sb, string label, ProjectCounts.RequirementSatisfaction sat)
    {
        var statusSegments = SatisfactionSegments(sat);
        sb.Append("<div class=\"chart-panel\">\n");
        sb.Append($"<h3>{PathUtil.Html(label)} ({sat.Total})</h3>\n<div class=\"donut-and-legend\">\n");
        sb.Append(Charts.Donut(statusSegments, ariaLabel:
            $"{label} requirements: {sat.Done} done, {sat.Active} partially implemented, {sat.Ready} ready for dev, "
            + $"{sat.Planned} planned, {sat.Unmapped} not yet mapped, {sat.Deferred} deferred"));
        sb.Append(Charts.DonutLegend(statusSegments));
        sb.Append("</div>\n</div>\n\n");
    }

    private static (string, int, string)[] SatisfactionSegments(ProjectCounts.RequirementSatisfaction sat) =>
    [
        ("Done", sat.Done, "done"),
        ("Partially implemented", sat.Active, "active"),
        ("Ready for dev", sat.Ready, "ready"),
        ("Planned", sat.Planned, "pending"),
        ("Not yet mapped", sat.Unmapped, "pending"),
        ("Deferred", sat.Deferred, "deferred"),
    ];

    /// <summary>A clickable navigator card for one group: a compact status pie chart, the group name, and
    /// its requirement count, anchoring down to the group's section. Reuses the epic-mosaic card layout.</summary>
    private static void AppendGroupCard(StringBuilder sb, string label, IReadOnlyList<RequirementInfo> reqs)
    {
        sb.Append($"  <a class=\"epic-mosaic-card req-group-card\" href=\"#{PathUtil.Html(GroupAnchor(label))}\">\n");
        sb.Append("    <div class=\"epic-mosaic-donut\">\n");
        sb.Append(Charts.Donut(StatusSegments(reqs), size: 64));
        sb.Append("    </div>\n");
        sb.Append("    <div class=\"epic-mosaic-label\">\n");
        sb.Append($"      <span class=\"epic-mosaic-title\">{PathUtil.Html(label)}</span>\n");
        sb.Append($"      <span class=\"epic-mosaic-sub\">{reqs.Count} requirement{(reqs.Count == 1 ? "" : "s")}</span>\n");
        sb.Append("    </div>\n  </a>\n");
    }

    private static (int Done, int Active, int Ready, int Planned, int Unmapped, int Deferred) StatusCounts(IReadOnlyList<RequirementInfo> reqs) => (
        // Per-kind / group-card donuts keep a local count (scoped to the list they render). The Overall
        // donut and satisfaction band read the ProjectCounts ledger. [Story 9.9]
        reqs.Count(r => r.Status == RequirementStatus.Done),
        reqs.Count(r => r.Status == RequirementStatus.Active),
        reqs.Count(r => r.Status == RequirementStatus.Ready),
        reqs.Count(r => r.Status == RequirementStatus.Planned),
        reqs.Count(r => r.Status == RequirementStatus.Unmapped),
        reqs.Count(r => r.Status == RequirementStatus.Deferred));

    private static (string, int, string)[] StatusSegments(IReadOnlyList<RequirementInfo> reqs)
    {
        var (done, active, ready, planned, unmapped, deferred) = StatusCounts(reqs);
        return new (string, int, string)[]
        {
            ("Done", done, "done"),
            ("Partially implemented", active, "active"),
            ("Ready for dev", ready, "ready"),
            ("Planned", planned, "pending"),
            // Unmapped reuses the pending/tan swatch (owner decision #1) but is a distinct, separately-counted
            // legend segment so "no plan exists yet" never hides inside "Planned". [Story 9.3 Task 4]
            ("Not yet mapped", unmapped, "pending"),
            ("Deferred", deferred, "deferred"),
        };
    }

    /// <summary>A stable in-page anchor id for a group heading, e.g. "Core Loop &amp; Time" → "grp-core-loop-time".</summary>
    private static string GroupAnchor(string label)
    {
        var chars = label.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return "grp-" + slug.Trim('-');
    }
}
