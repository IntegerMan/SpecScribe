using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>work-graph.html</c> page — one epic-scoped provenance subgraph per
/// epic-with-signal (<see cref="Charts.WorkGraph"/>), each with a visible circular-provenance query panel and a
/// complete sr-only enumeration of every drawn node and edge (NFR6 — the SVG is <c>role="img"</c>). A top scope
/// picker jumps to each epic's subgraph. Mirrors the synthesized-page shell every standalone insight page uses
/// (<see cref="TraceabilityTemplater"/> is the freshest precedent) rather than
/// <see cref="HtmlTemplater.RenderPage"/>. Progressive-enhancement: navigation is plain <c>&lt;a&gt;</c> and the
/// query results are static HTML, so the page is fully usable with JS off. [Story 19.2]</summary>
public static class WorkGraphTemplater
{
    public static string RenderPage(WorkGraphModel model, SiteNav nav)
    {
        var outputPath = SiteNav.WorkGraphOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — work-graph.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Work Graph — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Directed work graph for {nav.SiteTitle} — where each epic's deferred and action items came from, and any circular provenance."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildInsightsLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Work Graph", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Work Graph</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; where each epic's follow-up work came from</p>\n\n");
        sb.Append("<p class=\"work-graph-intro\">Each epic's diagram traces its deferred and open action items back to the story, spec, or quick-dev they stemmed from — and forward to whatever resolved them. Arrows point from the item that carries a reference to the item it refers to. Dashed arrows mark the softer &ldquo;also raised in&rdquo; cross-links between retrospectives.</p>\n\n");

        sb.Append(RenderLegend());

        // Scope picker: jump to any single epic's subgraph (progressive-enhancement — plain anchors, no JS). AC #1.
        sb.Append("<nav class=\"work-graph-scope\" aria-label=\"Jump to an epic's work graph\">\n");
        sb.Append("  <span class=\"work-graph-scope-label\">Jump to:</span>\n");
        foreach (var e in model.Epics)
            sb.Append($"  <a class=\"work-graph-scope-chip\" href=\"#{e.Anchor}\">{PathUtil.Html(e.DisplayName)}</a>\n");
        sb.Append("</nav>\n\n");

        foreach (var epic in model.Epics)
            sb.Append(RenderEpicSection(epic));

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string RenderLegend()
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"work-graph-legend\" aria-hidden=\"true\">\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-epic\"></span>Epic</span>\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-story\"></span>Story</span>\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-deferred\"></span>Deferred item</span>\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-action\"></span>Action item</span>\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-spec\"></span>Source / resolver</span>\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-edge\"></span>stemmed-from / resolves</span>\n");
        sb.Append("  <span class=\"work-graph-legend-item\"><span class=\"wg-key wg-key-edge-soft\"></span>also raised in</span>\n");
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    private static string RenderEpicSection(WorkGraphEpic epic)
    {
        var sb = new StringBuilder();
        var heading = epic.BucketLabel is not null
            ? PathUtil.Html(epic.DisplayName)
            : epic.EpicTitle.Length > 0
                ? $"Epic {epic.EpicNumber} — {PathUtil.Html(epic.EpicTitle)}"
                : $"Epic {epic.EpicNumber}";
        sb.Append($"<section class=\"work-graph-section\" id=\"{epic.Anchor}\" aria-labelledby=\"{epic.Anchor}-h\">\n");
        sb.Append($"  <h2 id=\"{epic.Anchor}-h\">{heading}</h2>\n");
        sb.Append("  <div class=\"work-graph-wrap\">\n");
        sb.Append(Charts.WorkGraph(epic));
        sb.Append("  </div>\n");

        if (epic.Overflow > 0)
            sb.Append($"  <p class=\"work-graph-overflow\">+{epic.Overflow} more follow-up {Charts.Plural(epic.Overflow, "item", "items")} not drawn (listed below).</p>\n");

        sb.Append(RenderSrEnumeration(epic));
        sb.Append(RenderQueryPanel(epic));
        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    /// <summary>Complete text equivalent of the SVG for assistive tech (a <c>role="img"</c> collapses its
    /// descendants). Every drawn node and every directed edge, so nothing in the graphic is AT-invisible.</summary>
    private static string RenderSrEnumeration(WorkGraphEpic epic)
    {
        var byId = epic.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.Append("  <div class=\"sr-only\">\n");
        sb.Append($"    <h3>{PathUtil.Html(epic.DisplayName)} work-graph nodes</h3>\n    <ul>\n");
        foreach (var n in epic.Nodes)
        {
            var where = n.Href is { Length: > 0 } ? $" (links to {PathUtil.Html(n.Href)})" : " (no page)";
            sb.Append($"      <li>{PathUtil.Html(NodeText(n))}{where}</li>\n");
        }
        sb.Append("    </ul>\n");
        sb.Append($"    <h3>{PathUtil.Html(epic.DisplayName)} work-graph links</h3>\n    <ul>\n");
        foreach (var e in epic.Edges)
        {
            if (!byId.TryGetValue(e.FromId, out var from) || !byId.TryGetValue(e.ToId, out var to)) continue;
            sb.Append($"      <li>{PathUtil.Html(NodeText(from))} {PathUtil.Html(EdgeVerb(e.Kind))} {PathUtil.Html(NodeText(to))}</li>\n");
        }
        sb.Append("    </ul>\n  </div>\n");
        return sb.ToString();
    }

    /// <summary>The circular-OR-ambiguous provenance query result (AC #1 — "surfaces ambiguous or circular
    /// provenance when present"). Static HTML, so it works with JS off. Reports (a) any simple directed cycle as a
    /// node chain closed on itself — the carrier→target projection is a DAG by construction, so this is empty in
    /// practice but stays correct if a future edge kind reintroduces a loop — and (b) any action item whose
    /// obligation is <em>also raised in</em> two or more other epics' retros (Story 19.1 query #2 — a genuinely
    /// ambiguous "which retro owns this?" reverse-link, the one that actually fires on live data). Honest empty
    /// message when neither is present.</summary>
    private static string RenderQueryPanel(WorkGraphEpic epic)
    {
        var byId = epic.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);

        // Ambiguous ownership: an action node with ≥2 distinct raised-in targets (raised across multiple epics).
        var ambiguous = epic.Edges
            .Where(e => e.Kind == WorkEdgeKind.RaisedIn)
            .GroupBy(e => e.FromId, StringComparer.Ordinal)
            .Where(g => g.Select(e => e.ToId).Distinct(StringComparer.Ordinal).Count() >= 2)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("  <div class=\"work-graph-query\">\n");
        sb.Append("    <h3>Circular or ambiguous provenance</h3>\n");

        if (epic.Cycles.Count == 0 && ambiguous.Count == 0)
        {
            sb.Append("    <p class=\"work-graph-empty\">No circular or ambiguous provenance in this scope.</p>\n");
            sb.Append("  </div>\n");
            return sb.ToString();
        }

        if (epic.Cycles.Count > 0)
        {
            sb.Append($"    <p>{epic.Cycles.Count} circular {Charts.Plural(epic.Cycles.Count, "chain", "chains")} found — an item's provenance loops back on itself:</p>\n");
            sb.Append("    <ul class=\"work-graph-cycles\">\n");
            foreach (var cycle in epic.Cycles)
            {
                var labels = cycle.Where(byId.ContainsKey).Select(id => NodeText(byId[id])).ToList();
                if (labels.Count == 0) continue;
                labels.Add(labels[0]); // close the loop for readability
                sb.Append($"      <li>{PathUtil.Html(string.Join(" → ", labels))}</li>\n");
            }
            sb.Append("    </ul>\n");
        }

        if (ambiguous.Count > 0)
        {
            sb.Append("    <p>Ambiguous ownership — the same obligation is raised across multiple retrospectives, so no single one clearly owns it:</p>\n");
            sb.Append("    <ul class=\"work-graph-cycles\">\n");
            foreach (var group in ambiguous)
            {
                if (!byId.TryGetValue(group.Key, out var action)) continue;
                var targets = group
                    .Select(e => byId.TryGetValue(e.ToId, out var t) ? NodeText(t) : null)
                    .Where(t => t is not null)
                    .Distinct(StringComparer.Ordinal);
                sb.Append($"      <li>{PathUtil.Html(NodeText(action))} — also raised in {PathUtil.Html(string.Join(", ", targets))}</li>\n");
            }
            sb.Append("    </ul>\n");
        }

        sb.Append("  </div>\n");
        return sb.ToString();
    }

    // Epic/Story/Retro labels are already self-describing ("Epic 7", "Story 7.11", "Epic 3 retro",
    // "Unattributed"); only the summary/key labels of the other kinds need a kind-word prefix.
    private static string NodeText(WorkNode n) => n.Kind switch
    {
        WorkNodeKind.Deferred => $"Deferred item: {n.Label}",
        WorkNodeKind.Action => $"Action item: {n.Label}",
        WorkNodeKind.Spec => $"Source: {n.Label}",
        _ => n.Label,
    };

    private static string EdgeVerb(WorkEdgeKind kind) => kind switch
    {
        WorkEdgeKind.Contains => "is part of",
        WorkEdgeKind.StemmedFrom => "stemmed from",
        WorkEdgeKind.Resolves => "was resolved by",
        WorkEdgeKind.RaisedIn => "was also raised in",
        _ => "links to",
    };
}
