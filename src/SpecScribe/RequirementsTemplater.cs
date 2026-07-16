using System.Text;

namespace SpecScribe;

/// <summary>Renders the two requirements page types: the requirements index (FRs, NFRs, and UX-DRs, with
/// progress donuts) and one detail page per requirement (definition, covering epic + its progress,
/// and derived status). Mirrors <see cref="EpicsTemplater"/>.</summary>
public static class RequirementsTemplater
{
    public static string RenderIndex(RequirementsModel model, EpicsModel epics, ProgressModel progress, SiteNav nav)
    {
        var outputPath = SiteNav.RequirementsOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Requirements — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + ForgeOptions.StylesheetName, PathUtil.RelativePrefix(outputPath) + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Requirements", null) }));

        var countParts = new List<string>
        {
            $"{model.Functional.Count} functional",
            $"{model.NonFunctional.Count} non-functional",
        };
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

        sb.Append("<section class=\"dashboard\">\n<div class=\"chart-row\">\n");
        AppendStatusDonut(sb, "Functional", model.Functional);
        AppendStatusDonut(sb, "Non-functional", model.NonFunctional);
        // Design donut absent (not empty) when the project has no UX-DRs — NFR8 degrade-gracefully. [Story 9.2]
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
            sb.Append("<div class=\"section-divider\">Requirements at a glance</div>\n");
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

    public static string RenderRequirement(RequirementInfo req, EpicInfo? coveringEpic, ProgressModel progress, SiteNav nav, EpicsModel epics)
    {
        var outputPath = $"requirements/{req.Slug}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);
        var statusClass = StatusStyles.ForRequirement(req);
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
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.Badge(statusClass, StatusStyles.RequirementLabel(req.Status))}");
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
            sb.Append("  <p class=\"pending-note\">Deferred — not yet assigned to an epic.</p>\n");
            if (req.CoverageNote is { Length: > 0 } dn)
            {
                sb.Append($"  <p class=\"epic-goal\">{PathUtil.Html(dn)}</p>\n");
            }
        }
        // --- Branch B: covered by one or more epics — list every covering epic's stories, grouped by epic. ---
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
        // --- Branch C: not deferred and no covering epic — genuinely UNMAPPED. Distinct wording from the
        //     deferred branch (AC #2); Story 9.3 adds the distinct visual treatment. [seam: Story 9.3] ---
        else
        {
            sb.Append("  <p class=\"pending-note\">Not yet mapped to any epic or story.</p>\n");
        }
        sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(prefix + SiteNav.RequirementsOutputPath)}\">&larr; All requirements</a>\n");
        sb.Append("</div>\n\n");

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
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
    /// NFRs and UX-DRs. Section-local "Not yet mapped" presentation (not a RequirementStatus enum value — the
    /// FR flow/grid iterate that enum and must not change). Story 9.3 will give Deferred vs Unmapped fully
    /// distinct visual treatment; this seam leaves them shareable via the deferred/grey css class with
    /// distinct words. [Story 9.2 Tasks 3–4]</summary>
    private static void AppendNfrUxDrCoverageSection(StringBuilder sb, RequirementsModel model, EpicsModel epics)
    {
        var hasNfr = model.NonFunctional.Count > 0;
        var hasDesign = model.Design.Count > 0;
        if (!hasNfr && !hasDesign) return;

        sb.Append("<div class=\"section-divider\">Non-functional &amp; design coverage</div>\n");
        sb.Append("<div class=\"chart-panel nfr-uxdr-coverage\">\n");
        // Section-level verification approach (AC #2) — a bare per-item badge is never the only signal.
        sb.Append("<p class=\"epic-goal\">Cross-cutting obligations verified across the codebase by tests and architectural invariants, tracked here by the epics that deliver them. Coverage is epic-level — a badge reflects the delivering epic's roll-up, not a per-requirement claim.</p>\n");

        if (hasNfr)
        {
            AppendCoverageSubGroup(sb, "Non-functional requirements", model.NonFunctional, epics);
        }
        if (hasDesign)
        {
            AppendCoverageSubGroup(sb, "UX design requirements", model.Design, epics);
        }

        sb.Append("</div>\n\n");
    }

    private static void AppendCoverageSubGroup(
        StringBuilder sb, string label, IReadOnlyList<RequirementInfo> reqs, EpicsModel epics)
    {
        sb.Append($"<h3 class=\"nfr-uxdr-subgroup-title\">{PathUtil.Html(label)}</h3>\n");
        foreach (var req in reqs)
        {
            AppendCoverageRow(sb, req, epics, prefix: string.Empty);
        }
    }

    /// <summary>One NFR/UX-DR coverage row: id → detail page, text, covering-epic chip(s), state badge.
    /// Reuses req-card / req-epic / status-badge vocabulary. [Story 9.2 Task 4]</summary>
    private static void AppendCoverageRow(StringBuilder sb, RequirementInfo req, EpicsModel epics, string prefix)
    {
        var href = $"{prefix}requirements/{req.Slug}.html";
        var byNumber = epics.Epics.ToDictionary(e => e.Number);

        // Section-local presentation — do not mutate RequirementStatus. [Story 9.2 Task 3]
        string badgeHtml;
        if (req.Deferred)
        {
            badgeHtml = StatusStyles.Badge("deferred", "Deferred");
        }
        else if (req.CoverageEpicNumbers.Count == 0)
        {
            // "Not yet mapped" reuses deferred/grey color; word must read "Not yet mapped", not "Deferred".
            // Story 9.3 will give them fully distinct visual treatment — leave this seam clean.
            badgeHtml = StatusStyles.Badge("deferred", "Not yet mapped");
        }
        else
        {
            badgeHtml = StatusStyles.Badge(StatusStyles.ForRequirement(req), StatusStyles.RequirementLabel(req.Status));
        }

        var rowClass = req.Deferred || req.CoverageEpicNumbers.Count == 0
            ? "deferred"
            : StatusStyles.ForRequirement(req);

        sb.Append($"<div class=\"req-card nfr-uxdr-row {rowClass}\" id=\"cov-{PathUtil.Html(req.Slug)}\">\n");
        sb.Append("  <div class=\"req-card-head\">\n");
        sb.Append($"    <a class=\"req-id-link\" href=\"{PathUtil.Html(href)}\">{PathUtil.Html(req.Id)}</a>\n");
        sb.Append($"    {badgeHtml}\n");

        if (req.Deferred)
        {
            if (req.CoverageNote is { Length: > 0 } note)
            {
                sb.Append($"    <span class=\"req-epic deferred\">{PathUtil.Html(note)}</span>\n");
            }
        }
        else if (req.CoverageEpicNumbers.Count == 0)
        {
            sb.Append("    <span class=\"req-epic deferred\">Not yet mapped to a delivering epic.</span>\n");
        }
        else
        {
            foreach (var n in req.CoverageEpicNumbers)
            {
                if (!byNumber.ContainsKey(n)) continue;
                sb.Append($"    <a class=\"req-epic\" href=\"{PathUtil.Html(prefix)}epics/epic-{n}.html\">Epic {n}</a>\n");
            }
        }
        sb.Append("  </div>\n");
        sb.Append($"  <div class=\"req-text\">{req.TextHtml}</div>\n");
        sb.Append("</div>\n\n");
    }

    private static void AppendRequirementCard(StringBuilder sb, RequirementInfo req, string prefix)
    {
        var statusClass = StatusStyles.ForRequirement(req);
        var href = $"{prefix}requirements/{req.Slug}.html";

        sb.Append($"<div class=\"req-card {statusClass}\" id=\"{PathUtil.Html(req.Slug)}\">\n");
        sb.Append("  <div class=\"req-card-head\">\n");
        sb.Append($"    <a class=\"req-id-link\" href=\"{PathUtil.Html(href)}\">{PathUtil.Html(req.Id)}</a>\n");
        sb.Append($"    {StatusStyles.Badge(statusClass, StatusStyles.RequirementLabel(req.Status))}\n");

        if (req.Deferred)
        {
            sb.Append("    <span class=\"req-epic deferred\">Deferred</span>\n");
        }
        else if (req.CoverageEpicNumber is { } epicNumber)
        {
            sb.Append($"    <a class=\"req-epic\" href=\"{PathUtil.Html(prefix)}epics/epic-{epicNumber}.html\">Epic {epicNumber}</a>\n");
        }
        sb.Append("  </div>\n");

        sb.Append($"  <div class=\"req-text\">{req.TextHtml}</div>\n");
        sb.Append("</div>\n\n");
    }

    private static void AppendStatusDonut(StringBuilder sb, string label, IReadOnlyList<RequirementInfo> reqs)
    {
        var (done, active, ready, planned, deferred) = StatusCounts(reqs);

        sb.Append("<div class=\"chart-panel\">\n");
        sb.Append($"<h3>{PathUtil.Html(label)} ({reqs.Count})</h3>\n<div class=\"donut-and-legend\">\n");
        var statusSegments = StatusSegments(reqs);
        sb.Append(Charts.Donut(statusSegments, ariaLabel: $"{label} requirements: {done} done, {active} partially implemented, {ready} ready for dev, {planned} planned, {deferred} deferred"));
        sb.Append(Charts.DonutLegend(statusSegments));
        sb.Append("</div>\n</div>\n\n");
    }

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

    private static (int Done, int Active, int Ready, int Planned, int Deferred) StatusCounts(IReadOnlyList<RequirementInfo> reqs) => (
        reqs.Count(r => r.Status == RequirementStatus.Done),
        reqs.Count(r => r.Status == RequirementStatus.Active),
        reqs.Count(r => r.Status == RequirementStatus.Ready),
        reqs.Count(r => r.Status == RequirementStatus.Planned),
        reqs.Count(r => r.Status == RequirementStatus.Deferred));

    private static (string, int, string)[] StatusSegments(IReadOnlyList<RequirementInfo> reqs)
    {
        var (done, active, ready, planned, deferred) = StatusCounts(reqs);
        return new (string, int, string)[]
        {
            ("Done", done, "done"),
            ("Partially implemented", active, "active"),
            ("Ready for dev", ready, "ready"),
            ("Planned", planned, "pending"),
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
