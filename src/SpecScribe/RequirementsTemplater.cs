using System.Text;

namespace SpecScribe;

/// <summary>Renders the two requirements page types: the requirements index (all FRs &amp; NFRs, with
/// FR/NFR progress donuts) and one detail page per requirement (definition, covering epic + its progress,
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

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Requirements</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {model.Functional.Count} functional &middot; {model.NonFunctional.Count} non-functional</div>\n");
        sb.Append("</header>\n\n");

        // Category groups in source order (LINQ GroupBy preserves first-seen key order), plus the NFRs
        // as one final group — the single ordered list that drives both the navigator and the sections
        // below, so their anchors always line up.
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

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public static string RenderRequirement(RequirementInfo req, EpicInfo? coveringEpic, ProgressModel progress, SiteNav nav)
    {
        var outputPath = $"requirements/{req.Slug}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);
        var statusClass = StatusStyles.ForRequirement(req);
        var kindLabel = req.Kind == RequirementKind.Functional ? "Functional Requirement" : "Non-Functional Requirement";

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
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <div class=\"story-kicker\">{PathUtil.Html(kindLabel)}</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(req.Id)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.Badge(statusClass, StatusStyles.RequirementLabel(req.Status))}");
        if (req.Category is { Length: > 0 } cat)
        {
            sb.Append($"<span class=\"pill\">{PathUtil.Html(cat)}</span>");
        }
        sb.Append("</div>\n</header>\n\n");

        sb.Append($"<div class=\"story-lead\"><p>{req.TextHtml}</p></div>\n\n");

        sb.Append("<div class=\"epic-card\">\n  <h3>Coverage</h3>\n");
        if (req.Deferred)
        {
            sb.Append("  <p class=\"pending-note\">Deferred — not yet assigned to an epic.</p>\n");
            if (req.CoverageNote is { Length: > 0 } dn)
            {
                sb.Append($"  <p class=\"epic-goal\">{PathUtil.Html(dn)}</p>\n");
            }
        }
        else if (coveringEpic is not null)
        {
            var epicProgress = progress.PerEpic.FirstOrDefault(p => p.Number == coveringEpic.Number);
            sb.Append("  <div class=\"coverage-cards\">\n");
            AppendCoverageCard(sb, coveringEpic, epicProgress, prefix);
            sb.Append("  </div>\n");

            if (req.CoverageNote is { Length: > 0 } note)
            {
                sb.Append($"  <p class=\"epic-goal coverage-note\">{PathUtil.Html(note)}</p>\n");
            }
        }
        else
        {
            sb.Append("  <p class=\"pending-note\">No covering epic recorded.</p>\n");
        }
        sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(prefix + SiteNav.RequirementsOutputPath)}\">&larr; All requirements</a>\n");
        sb.Append("</div>\n\n");

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}", prefix));
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
