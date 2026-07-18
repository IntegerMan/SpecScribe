using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Pure inline SVG + CSS chart builders — no JS, no external dependencies, themed entirely via
/// the CSS variables already defined in specscribe.css. Every builder degrades gracefully at zero/low data
/// (a hallmark of a project that's just getting started).</summary>
public static class Charts
{
    /// <summary>A dashboard stat card. When <paramref name="tooltip"/> is supplied the card opts into the shared
    /// body-level <c>js-tip</c>/<c>data-tip</c> path (never clipped under sticky nav) and becomes keyboard-focusable
    /// so it's reachable by hover, focus and touch — used to define what a number actually counts (UX-DR4).
    /// Native <c>title</c> remains as the no-JS fallback. Optional <paramref name="extraClass"/> carries journey
    /// accent classes; <paramref name="journeyLabel"/> marks the first card of a group with a floating caption.
    /// [Story 1.5 C2; home welcome tooltips]</summary>
    public static string StatCard(
        string number,
        string label,
        string? sub = null,
        string? tooltip = null,
        string? href = null,
        string? extraClass = null,
        string? journeyLabel = null)
    {
        var lead = journeyLabel is { Length: > 0 }
            ? $"<span class=\"tile-journey-label\">{Html(journeyLabel)}</span>"
            : string.Empty;
        var subHtml = sub is { Length: > 0 } ? $"<div class=\"stat-sub\">{Html(sub)}</div>" : string.Empty;
        var inner = $"{lead}<div class=\"stat-number\">{Html(number)}</div><div class=\"stat-label\">{Html(label)}</div>{subHtml}";
        var cls = "stat-card"
            + (href is { Length: > 0 } ? " stat-card-link" : string.Empty)
            + (tooltip is { Length: > 0 } ? " js-tip" : string.Empty)
            + (journeyLabel is { Length: > 0 } ? " journey-lead" : string.Empty)
            + (extraClass is { Length: > 0 } ? " " + extraClass : string.Empty);
        // A tile with a drill target becomes a link (natively focusable — no tabindex needed); otherwise it stays a
        // static div, adding tabindex only when a tooltip makes keyboard focus meaningful. The inner markup is
        // identical in both forms so the stat-tile parity/regression facts stay stable across the fork.
        if (href is { Length: > 0 })
        {
            if (tooltip is { Length: > 0 })
            {
                return $"<a class=\"{cls}\" href=\"{Html(href)}\" data-tip=\"{Html(tooltip)}\" title=\"{Html(tooltip)}\">{inner}</a>";
            }
            return $"<a class=\"{cls}\" href=\"{Html(href)}\">{inner}</a>";
        }
        if (tooltip is { Length: > 0 })
        {
            return $"<div class=\"{cls}\" data-tip=\"{Html(tooltip)}\" title=\"{Html(tooltip)}\" tabindex=\"0\">{inner}</div>";
        }
        return $"<div class=\"{cls}\">{inner}</div>";
    }

    public static string ProgressBar(string label, int value, int max, string? rightLabel = null)
    {
        var pct = max <= 0 ? 0 : Math.Clamp((double)value / max * 100, 0, 100);
        var cls = pct >= 100 && max > 0 ? "done" : pct > 0 ? "partial" : "empty";
        var right = rightLabel ?? $"{value} / {max}";
        // Screen-reader semantics: the bar is a progressbar whose value is the filled percentage (0–100),
        // named by the same label+fraction a sighted reader sees. The visible fraction text stays. [Story 1.4 AC #1]
        // Announce 100 only when genuinely complete and 0 only when genuinely empty; clamp any partial fill to
        // 1–99 so rounding never says "complete" (99.7→100) or "no progress" (0.3→0) mid-way.
        var pctNow = pct >= 100 && max > 0 ? 100 : pct <= 0 ? 0 : Math.Clamp((int)Math.Round(pct), 1, 99);
        var ariaLabel = Html($"{label}: {right}");

        return $"""
            <div class="progress-row">
              <div class="progress-label">{Html(label)}</div>
              <div class="progress-bar" role="progressbar" aria-valuenow="{pctNow}" aria-valuemin="0" aria-valuemax="100" aria-label="{ariaLabel}"><div class="progress-fill {cls}" style="width:{F(pct)}%"></div></div>
              <div class="progress-value">{Html(right)}</div>
            </div>

            """;
    }

    /// <summary>A donut chart from labeled segments; each segment's CSS class picks its color
    /// (e.g. "done"/"pending") via .donut-seg.done { stroke: ... } rules in specscribe.css. When
    /// <paramref name="ariaLabel"/> is supplied the whole chart carries <c>role="img"</c>+that name so it is
    /// reachable without a pointer; when omitted the donut is decorative (<c>aria-hidden</c>) — used where an
    /// enclosing labeled card/legend already names it, so screen readers don't hear it twice. [Story 1.4 AC #1]</summary>
    public static string Donut(IReadOnlyList<(string Label, int Value, string CssClass)> segments, int size = 120, string? ariaLabel = null, string? centerText = null, bool showCenterText = true, bool segmentTitles = true)
    {
        var total = segments.Sum(s => Math.Max(0, s.Value));
        var radius = size / 2.0 - 10;
        var circumference = 2 * Math.PI * radius;
        var center = size / 2.0;

        var a11y = ariaLabel is { Length: > 0 }
            ? $" role=\"img\" aria-label=\"{Html(ariaLabel)}\""
            : " aria-hidden=\"true\"";

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"donut\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\"{a11y}>\n");
        sb.Append($"  <circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" class=\"donut-track\" />\n");

        // When the donut is non-decorative (a caller supplied an aria-label / role="img"), make each slice
        // keyboard-focusable so the on-brand tooltip is reachable by focus, not just hover/pointer. Decorative
        // donuts (aria-hidden) stay out of the tab order. [Story 1.5 review — tooltip keyboard reach]
        var segFocus = ariaLabel is { Length: > 0 } ? " tabindex=\"0\"" : string.Empty;

        var offset = 0.0;
        if (total > 0)
        {
            foreach (var (label, value, cssClass) in segments)
            {
                if (value <= 0) continue;
                var fraction = (double)value / total;
                var dash = fraction * circumference;
                var gap = circumference - dash;
                var segTitle = segmentTitles ? $"<title>{Html(label)}: {value}</title>" : string.Empty;
                sb.Append(
                    $"  <circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" class=\"donut-seg {cssClass}\"{segFocus} " +
                    $"stroke-dasharray=\"{F(dash)} {F(gap)}\" stroke-dashoffset=\"-{F(offset)}\">" +
                    $"{segTitle}</circle>\n");
                offset += dash;
            }
        }

        // The center reads as progress, not a score: when a caller supplies a done/total fraction (E3) show
        // that; otherwise fall back to the bare total. The fraction variant gets a smaller type class so
        // "12/34" fits the ring. A tiny wheel can suppress the center entirely (showCenterText: false) when a
        // sibling label already carries the number. [Story 1.5 E3]
        if (showCenterText)
        {
            var centerContent = centerText is { Length: > 0 } ? Html(centerText) : total.ToString();
            var centerClass = centerText is { Length: > 0 } ? "donut-center-text donut-center-fraction" : "donut-center-text";
            sb.Append($"  <text x=\"{F(center)}\" y=\"{F(center)}\" class=\"{centerClass}\" text-anchor=\"middle\" dominant-baseline=\"central\">{centerContent}</text>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>A tiny inline donut (no center text) for story-card badges — e.g. task completion at a glance.</summary>
    public static string MiniDonut(int done, int total, int size = 22)
    {
        var radius = size / 2.0 - 3;
        var circumference = 2 * Math.PI * radius;
        var center = size / 2.0;

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"mini-donut\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" aria-hidden=\"true\">");
        sb.Append($"<circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" class=\"donut-track\" />");
        if (total > 0 && done > 0)
        {
            var dash = (double)done / total * circumference;
            sb.Append($"<circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" class=\"donut-seg done\" " +
                      $"stroke-dasharray=\"{F(dash)} {F(circumference - dash)}\" stroke-dashoffset=\"0\" />");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>The project sunburst (glance): inner = epics, middle = stories sized by tasks only,
    /// outer = open vs done follow-up aggregates per epic (not every leaf). Per-item wedges live on
    /// <see cref="EpicSunburst"/>. Pure SVG — no JS. [spec-sunburst-remaining-work-hierarchy]</summary>
    public static string Sunburst(
        EpicsModel model,
        int size = 380,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        var epics = model.Epics.OrderBy(e => e.Number).ToList();
        if (epics.Count == 0) return "<div class=\"chart-empty\">Nothing to chart yet.</div>";

        var geometry = followUps ?? FollowUpGeometry.Empty;
        var unplannedGeo = unplanned ?? UnplannedWorkGeometry.Empty;
        var knownEpics = epics.Select(e => e.Number).ToHashSet();
        var unattributed = geometry.OrphanActionItems(knownEpics);
        var orphanSlots = unattributed.Count;
        var unplannedSlots = unplannedGeo.SunburstUnplannedWeight;

        int StoryWeight(StoryInfo s) => Math.Max(1, s.TasksTotal);
        int EpicWeight(EpicInfo e) => Math.Max(1, e.Stories.Sum(StoryWeight));

        var totalWeight = epics.Sum(EpicWeight)
            + (orphanSlots > 0 ? Math.Max(1, orphanSlots) : 0)
            + (unplannedSlots > 0 ? Math.Max(1, unplannedSlots) : 0);
        var anglePerUnit = 2 * Math.PI / totalWeight;
        const double pad = 0.006;

        var c = size / 2.0;
        var epicInner = size * 0.16;
        var epicOuter = size * 0.28;
        var storyInner = size * 0.285;
        var storyOuter = size * 0.415;
        var aggregateInner = size * 0.42;
        var aggregateOuter = size * 0.465;

        var hasAggregates = false;
        var hasUnplanned = unplannedGeo.SunburstUnplannedWeight > 0;

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"Project progress sunburst\">\n");

        var angle = -Math.PI / 2;
        foreach (var epic in epics)
        {
            var weight = EpicWeight(epic);
            var sweep = weight * anglePerUnit;
            var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
            var epicTitle = PathUtil.StripHtmlTags(epic.Title);
            var (openCount, doneCount) = CountEpicFollowUpAggregates(epic, geometry, unplannedGeo);
            if (openCount + doneCount > 0) hasAggregates = true;

            var followNote = openCount + doneCount > 0
                ? $", {openCount} open / {doneCount} done follow-ups"
                : string.Empty;
            var epicAria = $"Epic {epic.Number}: {epicTitle} — {StatusStyles.EpicLabel(epicClass)}, {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}{followNote}";
            sb.Append($"  <a href=\"epics/epic-{epic.Number}.html\" aria-label=\"{Html(epicAria)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{epicClass}\" d=\"{AnnularSector(c, epicInner, epicOuter, InsetStart(angle, sweep, pad), InsetEnd(angle, sweep, pad))}\">");
            sb.Append($"<title>Epic {epic.Number}: {Html(epicTitle)} — {Html(StatusStyles.EpicLabel(epicClass))}, {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}{Html(followNote)}</title></path>\n");
            sb.Append("  </a>\n");

            var storyWeightSum = epic.Stories.Sum(StoryWeight);
            if (storyWeightSum > 0)
            {
                var anglePerUnitSlot = sweep / storyWeightSum;
                var slotAngle = angle;
                foreach (var story in epic.Stories)
                {
                    var sw = StoryWeight(story) * anglePerUnitSlot;
                    AppendWeightedStorySlot(sb, story, geometry, slotAngle, sw, pad, c, storyInner, storyOuter,
                        aggregateInner, aggregateOuter, nestStoryChildren: false);
                    slotAngle += sw;
                }
            }

            var aggregateHref = geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number);
            AppendOpenDoneAggregateRing(sb, openCount, doneCount, angle, sweep, pad, c,
                aggregateInner, aggregateOuter, aggregateHref,
                openLabel: $"Epic {epic.Number}: {openCount} open {Plural(openCount, "follow-up", "follow-ups")}",
                doneLabel: $"Epic {epic.Number}: {doneCount} done {Plural(doneCount, "follow-up", "follow-ups")}");

            angle += sweep;
        }

        if (orphanSlots > 0)
        {
            hasAggregates = true;
            var orphanWeight = Math.Max(1, orphanSlots);
            var sweep = orphanWeight * anglePerUnit;
            var openOrphans = unattributed.Count(a => !FollowUpGeometry.IsDone(a));
            var doneOrphans = orphanSlots - openOrphans;
            var orphanClass = openOrphans > 0 ? "followup-open" : "followup-done";
            var orphanHref = geometry.FollowUpsGroupHref;
            var orphanAria = openOrphans > 0
                ? $"Follow-ups: {orphanSlots} unattributed {Plural(orphanSlots, "item", "items")}"
                : $"Follow-ups: {orphanSlots} completed unattributed {Plural(orphanSlots, "item", "items")}";

            sb.Append($"  <a href=\"{Html(orphanHref)}\" aria-label=\"{Html(orphanAria)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{orphanClass}\" d=\"{AnnularSector(c, epicInner, epicOuter, InsetStart(angle, sweep, pad), InsetEnd(angle, sweep, pad))}\">");
            sb.Append($"<title>{Html(orphanAria)}</title></path>\n  </a>\n");

            AppendOpenDoneAggregateRing(sb, openOrphans, doneOrphans, angle, sweep, pad, c,
                storyInner, storyOuter, orphanHref,
                openLabel: $"Follow-ups: {openOrphans} open unattributed {Plural(openOrphans, "item", "items")}",
                doneLabel: $"Follow-ups: {doneOrphans} done unattributed {Plural(doneOrphans, "item", "items")}");

            angle += sweep;
        }

        if (unplannedSlots > 0)
        {
            // Do not set hasAggregates — Unplanned uses hasUnplanned for legend/hint (avoid follow-up swatches).
            var unplannedWeight = Math.Max(1, unplannedSlots);
            var sweep = unplannedWeight * anglePerUnit;
            var openUnplanned = unplannedGeo.UnplannedQuickDev.Count(q => UnplannedWorkGeometry.IsOpenQuickDev(q.Entry.Status))
                + unplannedGeo.UnattributableDeferred.Count(s => !s.Item.Resolved);
            var doneUnplanned = Math.Max(0, unplannedSlots - openUnplanned);
            var rootClass = openUnplanned > 0 ? "unplanned" : "followup-done";
            var rootHref = unplannedGeo.GroupRootHref ?? "#";
            var rootAria = openUnplanned > 0
                ? $"Unplanned: {unplannedSlots} direct / one-off {Plural(unplannedSlots, "item", "items")}"
                : $"Unplanned: {unplannedSlots} completed direct / one-off {Plural(unplannedSlots, "item", "items")}";

            sb.Append($"  <a href=\"{Html(rootHref)}\" aria-label=\"{Html(rootAria)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{rootClass}\" d=\"{AnnularSector(c, epicInner, epicOuter, InsetStart(angle, sweep, pad), InsetEnd(angle, sweep, pad))}\">");
            sb.Append($"<title>Unplanned / Direct work: {Html(rootAria)}</title></path>\n  </a>\n");

            AppendOpenDoneAggregateRing(sb, openUnplanned, doneUnplanned, angle, sweep, pad, c,
                storyInner, storyOuter, rootHref,
                openLabel: $"Unplanned: {openUnplanned} open {Plural(openUnplanned, "item", "items")}",
                doneLabel: $"Unplanned: {doneUnplanned} done {Plural(doneUnplanned, "item", "items")}",
                openClass: "unplanned",
                doneClass: "followup-done");

            angle += sweep;
        }

        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{epics.Count}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{Plural(epics.Count, "epic", "epics")}</text>\n");
        sb.Append("</svg>\n");

        sb.Append(SunburstLegend(BuildSunburstLegendItems(hasAggregates, hasUnplanned)));
        sb.Append(BuildSunburstHint(hasAggregates, hasUnplanned));
        return sb.ToString();
    }

    /// <summary>Open vs done counts for everything attributed to an epic on the project glance
    /// (actions, deferred under that epic, attributed quick-dev).</summary>
    private static (int Open, int Done) CountEpicFollowUpAggregates(
        EpicInfo epic, FollowUpGeometry geometry, UnplannedWorkGeometry unplanned)
    {
        var open = 0;
        var done = 0;
        foreach (var item in geometry.ForEpicNumber(epic.Number))
        {
            if (FollowUpGeometry.IsDone(item)) done++;
            else open++;
        }

        foreach (var slot in geometry.DeferredForEpicNumber(epic.Number))
        {
            if (slot.Item.Resolved) done++;
            else open++;
        }

        foreach (var qd in unplanned.ForEpic(epic.Number))
        {
            if (UnplannedWorkGeometry.IsOpenQuickDev(qd.Entry.Status)) open++;
            else done++;
        }

        return (open, done);
    }

    /// <summary>Open/done aggregate wedges under a parent sweep. Omits empty sides (NFR8).</summary>
    private static void AppendOpenDoneAggregateRing(
        StringBuilder sb, int openCount, int doneCount,
        double angle, double sweep, double pad,
        double c, double inner, double outer, string href,
        string openLabel, string doneLabel,
        string openClass = "followup-open", string doneClass = "followup-done")
    {
        var total = openCount + doneCount;
        if (total <= 0) return;

        var usable = Math.Max(0, sweep - 2 * Math.Min(pad, sweep / 2));
        var cursor = InsetStart(angle, sweep, pad);
        if (openCount > 0)
        {
            var openSweep = usable * openCount / total;
            AppendFollowUpSlot(sb, openLabel, href, openClass, cursor, openSweep, pad: 0, c, inner, outer);
            cursor += openSweep;
        }
        if (doneCount > 0)
        {
            var doneSweep = usable * doneCount / total;
            AppendFollowUpSlot(sb, doneLabel, href, doneClass, cursor, doneSweep, pad: 0, c, inner, outer);
        }
    }

    private static string BuildSunburstHint(bool hasFollowUps, bool hasUnplanned)
    {
        if (!hasFollowUps && !hasUnplanned)
            return "<div class=\"sunburst-hint\">Inner ring: epics &middot; middle: stories (sized by tasks). Click any segment to open it.</div>\n\n";

        var parts = new List<string>
        {
            "Inner ring: epics &middot; middle: stories (sized by tasks) &middot; outer: open vs done follow-ups (aggregated).",
        };
        if (hasFollowUps)
            parts.Add("Orange = open; green = done. Click an aggregate to open that group.");
        if (hasUnplanned)
            parts.Add("Unplanned = direct / one-shot work outside the epic plan.");
        return $"<div class=\"sunburst-hint\">{string.Join(" ", parts)}</div>\n\n";
    }

    private static bool HasAnyStoryChildDeferred(FollowUpGeometry geometry, IEnumerable<EpicInfo> epics) =>
        epics.Any(e => e.Stories.Any(s => geometry.StoryChildDeferred(e.Number, s.Id).Count > 0));

    /// <summary>The shared sunburst legend — one focusable entry per status, each carrying a status class
    /// (<c>sb-&lt;status&gt;-item</c>) and <c>tabindex="0"</c> so the pure-CSS interactive-legend emphasis
    /// (specscribe.css: <c>.sunburst-panel:has(.sb-&lt;status&gt;-item:hover/:focus) …</c>) is reachable by
    /// both pointer and keyboard. The always-visible swatch + label keep status readable without the emphasis
    /// affordance (never color-only). [Story 3.5 Task 3, UXO C3]</summary>
    private static string SunburstLegend(params (string Status, string Label)[] items)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"sunburst-legend\">\n");
        foreach (var (status, label) in items)
        {
            sb.Append($"  <span class=\"sb-legend-item sb-{status}-item\" tabindex=\"0\">" +
                      $"<span class=\"swatch sb-{status}-sw\"></span>{Html(label)}</span>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The shared donut legend — the donut half of Subtask 3.1's "sunburst **or** donut" interactive-
    /// legend emphasis (UXO C3), mirroring <see cref="SunburstLegend"/>'s pattern exactly: one focusable entry
    /// per status (<c>dn-&lt;status&gt;-item</c>, <c>tabindex="0"</c>) so the pure-CSS
    /// <c>.donut-and-legend:has(.dn-&lt;status&gt;-item:hover/:focus-visible) …</c> rule in specscribe.css can
    /// dim the non-matching <c>.donut-seg</c> slices and emphasize the match. The existing <c>swatch
    /// {CssClass}</c> styling and "Label (Count)" text are unchanged — status stays readable at rest without
    /// the affordance (never color-only). [Story 3.5 Task 3, UXO C3]</summary>
    public static string DonutLegend(IEnumerable<(string Label, int Value, string CssClass)> items)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"donut-legend\">\n");
        foreach (var (label, value, cssClass) in items)
        {
            sb.Append($"  <span class=\"dn-legend-item dn-{cssClass}-item\" tabindex=\"0\">" +
                      $"<span class=\"swatch {cssClass}\"></span>{Html(label)} ({value})</span>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>Renders a task-weighted story in the middle ring. When
    /// <paramref name="nestStoryChildren"/> is true (epic detail), story-child deferred fills the outer
    /// ring under this story; the project glance passes false and draws open/done aggregates instead.
    /// [spec-sunburst-remaining-work-hierarchy]</summary>
    private static void AppendWeightedStorySlot(
        StringBuilder sb, StoryInfo story, FollowUpGeometry geometry,
        double angle, double sweep, double pad,
        double c, double storyInner, double storyOuter, double deferredInner, double deferredOuter,
        bool nestStoryChildren = true)
    {
        var storyClass = StatusStyles.ForStory(story);
        var storyHref = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
        var storyTitle = PathUtil.StripHtmlTags(story.Title);
        var statusNote = story.Status is { Length: > 0 } s ? $" — {s}" : string.Empty;
        var taskNote = story.TasksTotal > 0
            ? $", {story.TasksDone}/{story.TasksTotal} tasks"
            : string.Empty;

        var storyAria = $"Story {story.Id}: {storyTitle}{statusNote}{taskNote}";
        sb.Append($"  <a href=\"{Html(storyHref)}\" aria-label=\"{Html(storyAria)}\">\n");
        sb.Append($"    <path class=\"sb-seg sb-{storyClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, InsetStart(angle, sweep, pad), InsetEnd(angle, sweep, pad))}\">");
        sb.Append($"<title>Story {story.Id}: {Html(storyTitle)}{Html(statusNote)}{Html(taskNote)}</title></path>\n  </a>\n");

        if (!nestStoryChildren) return;

        var children = geometry.StoryChildDeferred(story.EpicNumber, story.Id);
        if (children.Count > 0)
        {
            // Parent already inset by pad — children divide the usable sweep with no second pad.
            var usable = Math.Max(0, sweep - 2 * Math.Min(pad, sweep / 2));
            var childSweep = usable / children.Count;
            var childAngle = InsetStart(angle, sweep, pad);
            foreach (var slot in children)
            {
                AppendDeferredItemSlot(sb, slot, childAngle, childSweep, pad: 0, c, deferredInner, deferredOuter);
                childAngle += childSweep;
            }
        }
    }

    private static void AppendActionItemSlot(
        StringBuilder sb, SprintActionItem item, string href, double angle, double sweep, double pad,
        double c, double storyInner, double storyOuter)
    {
        var done = FollowUpGeometry.IsDone(item);
        var text = TruncateFollowUpText(PathUtil.StripHtmlTags(item.Action));
        if (string.IsNullOrWhiteSpace(text)) text = "(no action text)";
        var label = done ? $"Action item (done): {text}" : $"Action item: {text}";
        AppendFollowUpSlot(sb, label, href, done ? "followup-done" : "followup-open", angle, sweep, pad, c, storyInner, storyOuter);
    }

    private static void AppendDeferredItemSlot(
        StringBuilder sb, FollowUpDeferredSlot slot, double angle, double sweep, double pad,
        double c, double storyInner, double storyOuter)
    {
        var resolved = slot.Item.Resolved;
        var text = TruncateFollowUpText(
            PathUtil.StripHtmlTags(FollowUpRow.SummarizeFromHtml(slot.Item.BodyHtml)));
        if (string.IsNullOrWhiteSpace(text)) text = "(no deferred text)";
        var from = DeferredSourceSuffix(slot);
        var label = resolved
            ? $"Deferred item (resolved): {text}{from}"
            : $"Deferred item: {text}{from}";
        AppendFollowUpSlot(sb, label, slot.DetailHref, resolved ? "followup-done" : "followup-open", angle, sweep, pad, c, storyInner, storyOuter);
    }

    /// <summary>Names the story or quick-dev this deferred item stemmed from (code-review provenance),
    /// so residual work stays readable as a child of its source — never a free-floating "Story". [Story 9.12]</summary>
    private static string DeferredSourceSuffix(FollowUpDeferredSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.SourceKey)) return string.Empty;
        var key = slot.SourceKey.Trim().Trim('`');
        if (key.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) key = key[..^3];
        if (key.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) key = key[..^5];

        var storyId = FollowUpRefs.StoryIdFromKey(key);
        if (storyId is not null)
            return $" (from Story {storyId})";
        if (key.StartsWith("spec-", StringComparison.OrdinalIgnoreCase))
            return $" (from Direct change: {key})";
        return $" (from {key})";
    }

    private static void AppendQuickDevSlot(
        StringBuilder sb, UnplannedQuickDevSlot slot, double angle, double sweep, double pad,
        double c, double storyInner, double storyOuter)
    {
        var title = TruncateFollowUpText(UnplannedWorkGeometry.DisplayTitle(slot.Entry.Title));
        var open = UnplannedWorkGeometry.IsOpenQuickDev(slot.Entry.Status);
        var label = open ? $"Direct change: {title}" : $"Direct change (done): {title}";
        AppendFollowUpSlot(sb, label, slot.Href, open ? "unplanned" : "followup-done", angle, sweep, pad, c, storyInner, storyOuter);
    }

    private static void AppendFollowUpSlot(
        StringBuilder sb, string label, string href, string cssClass, double angle, double sweep, double pad,
        double c, double storyInner, double storyOuter)
    {
        sb.Append($"  <a href=\"{Html(href)}\" aria-label=\"{Html(label)}\">");
        sb.Append($"<path class=\"sb-seg sb-{cssClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, InsetStart(angle, sweep, pad), InsetEnd(angle, sweep, pad))}\">");
        sb.Append($"<title>{Html(label)}</title></path></a>\n");
    }

    /// <summary>Pad inset that never exceeds half the sweep (avoids inverted annular sectors on tiny wedges).</summary>
    private static double InsetStart(double angle, double sweep, double pad) =>
        angle + Math.Min(pad, Math.Max(0, sweep) / 2);

    private static double InsetEnd(double angle, double sweep, double pad) =>
        angle + sweep - Math.Min(pad, Math.Max(0, sweep) / 2);

    private static (string Status, string Label)[] BuildSunburstLegendItems(bool hasFollowUps, bool hasUnplanned = false)
    {
        var items = new List<(string, string)>
        {
            ("pending", "Pending"), ("drafted", "Drafted"), ("ready", "Ready for dev"),
            ("active", "In development"), ("review", "In review"), ("done", "Done"),
        };
        if (hasFollowUps)
        {
            items.Add(("followup-open", "Open follow-up"));
            items.Add(("followup-done", "Done follow-up"));
        }
        if (hasUnplanned)
            items.Add(("unplanned", "Direct change"));
        return items.ToArray();
    }

    private static string TruncateFollowUpText(string text, int max = 80)
    {
        if (text.Length <= max) return text;
        return text[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>SVG path for an annular sector (donut slice) between two angles at two radii.</summary>
    private static string AnnularSector(double c, double rInner, double rOuter, double a0, double a1)
    {
        if (a1 <= a0) a1 = a0 + 0.0001;
        var largeArc = a1 - a0 > Math.PI ? 1 : 0;

        var x1 = c + rOuter * Math.Cos(a0); var y1 = c + rOuter * Math.Sin(a0);
        var x2 = c + rOuter * Math.Cos(a1); var y2 = c + rOuter * Math.Sin(a1);
        var x3 = c + rInner * Math.Cos(a1); var y3 = c + rInner * Math.Sin(a1);
        var x4 = c + rInner * Math.Cos(a0); var y4 = c + rInner * Math.Sin(a0);

        return $"M {F(x1)} {F(y1)} A {F(rOuter)} {F(rOuter)} 0 {largeArc} 1 {F(x2)} {F(y2)} " +
               $"L {F(x3)} {F(y3)} A {F(rInner)} {F(rInner)} 0 {largeArc} 0 {F(x4)} {F(y4)} Z";
    }

    /// <summary>An epic-scoped sunburst: inner ring = this epic's stories (task-weighted, colored by
    /// status) plus epic-level peers (action items, attributed quick-dev, epic-only deferred),
    /// outer ring = story-child deferred under each parent story when any exist. Does <em>not</em>
    /// draw the project-level Unplanned root. [spec-sunburst-remaining-work-hierarchy]</summary>
    public static string EpicSunburst(
        EpicInfo epic,
        Func<StoryInfo, string> hrefBuilder,
        int size = 320,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        var geometry = (followUps ?? FollowUpGeometry.Empty).ForEpic(epic.Number);
        var epicFollowUps = geometry.ActionItems;
        var storyIds = epic.Stories.Select(s => s.Id);
        var epicLevelDeferred = geometry.EpicLevelDeferred(epic.Number, storyIds);
        var epicQuickDev = (unplanned ?? UnplannedWorkGeometry.Empty).ForEpic(epic.Number);

        int StoryWeight(StoryInfo s) => Math.Max(1, s.TasksTotal);
        var totalWeight = epic.Stories.Sum(StoryWeight) + epicFollowUps.Count + epicLevelDeferred.Count + epicQuickDev.Count;
        if (totalWeight == 0) return "<div class=\"chart-empty\">No stories drafted for this epic yet.</div>";

        var hasFollowUps = geometry.HasAny;
        var hasDirect = epicQuickDev.Count > 0;
        var hasStoryChildDeferred = HasAnyStoryChildDeferred(geometry, new[] { epic });
        var c = size / 2.0;
        var storyInner = size * 0.16;
        var storyOuter = size * 0.36;
        var deferredInner = size * 0.37;
        var deferredOuter = size * 0.46;

        var anglePerUnit = 2 * Math.PI / totalWeight;
        const double pad = 0.012;

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"Epic story breakdown\">\n");

        var angle = -Math.PI / 2;
        foreach (var story in epic.Stories)
        {
            var storyClass = StatusStyles.ForStory(story);
            var href = hrefBuilder(story);
            var storyTitle = PathUtil.StripHtmlTags(story.Title);
            var statusNote = story.Status is { Length: > 0 } s ? $" — {s}" : string.Empty;
            var taskNote = story.TasksTotal > 0
                ? $", {story.TasksDone}/{story.TasksTotal} tasks"
                : string.Empty;
            var sw = StoryWeight(story) * anglePerUnit;

            var storyAria = $"Story {story.Id}: {storyTitle}{statusNote}{taskNote}";
            sb.Append($"  <a href=\"{Html(href)}\" aria-label=\"{Html(storyAria)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{storyClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, InsetStart(angle, sw, pad), InsetEnd(angle, sw, pad))}\">");
            sb.Append($"<title>Story {story.Id}: {Html(storyTitle)}{Html(statusNote)}{Html(taskNote)}</title></path>\n  </a>\n");

            var children = geometry.StoryChildDeferred(epic.Number, story.Id);
            if (children.Count > 0)
            {
                var usable = Math.Max(0, sw - 2 * Math.Min(pad, sw / 2));
                var childSweep = usable / children.Count;
                var childAngle = InsetStart(angle, sw, pad);
                foreach (var slot in children)
                {
                    AppendDeferredItemSlot(sb, slot, childAngle, childSweep, pad: 0, c, deferredInner, deferredOuter);
                    childAngle += childSweep;
                }
            }

            angle += sw;
        }

        foreach (var item in epicFollowUps)
        {
            var slotSweep = 1.0 * anglePerUnit;
            AppendActionItemSlot(sb, item, geometry.HrefFor(item), angle, slotSweep, pad, c, storyInner, storyOuter);
            angle += slotSweep;
        }

        foreach (var slot in epicLevelDeferred)
        {
            var slotSweep = 1.0 * anglePerUnit;
            AppendDeferredItemSlot(sb, slot, angle, slotSweep, pad, c, storyInner, storyOuter);
            angle += slotSweep;
        }

        foreach (var qd in epicQuickDev)
        {
            var slotSweep = 1.0 * anglePerUnit;
            AppendQuickDevSlot(sb, qd, angle, slotSweep, pad, c, storyInner, storyOuter);
            angle += slotSweep;
        }

        var storyCount = epic.Stories.Count;
        if (storyCount > 0)
        {
            sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{storyCount}</text>\n");
            sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{Plural(storyCount, "story", "stories")}</text>\n");
        }
        else
        {
            var peerCount = epicFollowUps.Count + epicLevelDeferred.Count + epicQuickDev.Count;
            sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{peerCount}</text>\n");
            sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{Plural(peerCount, "item", "items")}</text>\n");
        }
        sb.Append("</svg>\n");

        sb.Append(SunburstLegend(BuildSunburstLegendItems(hasFollowUps, hasDirect)));
        if (hasFollowUps || hasDirect || hasStoryChildDeferred)
        {
            var hint = "Inner ring: stories (sized by tasks)";
            if (hasFollowUps) hint += " &amp; follow-ups";
            if (hasDirect) hint += " &amp; direct work";
            if (hasStoryChildDeferred) hint += " &middot; outer: story-child deferred";
            hint += ".";
            if (hasFollowUps) hint += " Dashed wedges = follow-ups (orange open / green done) — never story stages.";
            if (hasDirect) hint += " Direct changes are one-shot work attributed to this epic.";
            sb.Append($"<div class=\"sunburst-hint\">{hint}</div>\n\n");
        }
        else
        {
            sb.Append("<div class=\"sunburst-hint\">Inner ring: stories (sized by tasks). Click any segment to open it.</div>\n\n");
        }
        return sb.ToString();
    }

    /// <summary>A per-story task sunburst: inner ring = top-level tasks, middle = subtasks (when present),
    /// optional outer ring = deferred items stemmed from this story (open/done follow-up treatment, linked
    /// to detail pages). Same visual language as the project/epic sunbursts for lifecycle greens/greys;
    /// deferred reuse <c>sb-followup-*</c>. Task/subtask segments are tooltip-only (no task pages).
    /// [spec-sunburst-remaining-work-hierarchy]</summary>
    public static string TaskSunburst(
        IReadOnlyList<TaskItem> tasks,
        int size = 280,
        IReadOnlyList<FollowUpDeferredSlot>? deferred = null)
    {
        var deferredItems = deferred ?? Array.Empty<FollowUpDeferredSlot>();
        if (tasks.Count == 0 && deferredItems.Count == 0)
            return "<div class=\"chart-empty\">No tasks tracked for this story yet.</div>";

        var hasDeferred = deferredItems.Count > 0;
        var c = size / 2.0;
        // When deferred share the chart, pull task/subtask rings in to leave an outer fringe.
        var taskInner = size * (hasDeferred ? 0.14 : 0.16);
        var taskOuter = size * (hasDeferred ? 0.30 : 0.36);
        var subInner = size * (hasDeferred ? 0.31 : 0.37);
        var subOuter = size * (hasDeferred ? 0.40 : 0.48);
        var deferredInner = size * 0.41;
        var deferredOuter = size * 0.48;
        const double pad = 0.01;

        var sb = new StringBuilder();
        var tasksDone = tasks.Count(t => t.Done);
        var tasksTotal = tasks.Count;
        var openDeferred = deferredItems.Count(s => !s.Item.Resolved);
        var centerLabel = tasksTotal > 0 ? "tasks" : "deferred";
        var centerNum = tasksTotal > 0
            ? $"{tasksDone}/{tasksTotal}"
            : $"{openDeferred}/{deferredItems.Count}";
        var aria = tasksTotal > 0
            ? $"Task breakdown: {tasksDone} of {tasksTotal} tasks done"
            : $"Deferred breakdown: {openDeferred} of {deferredItems.Count} open";
        if (tasksTotal > 0 && hasDeferred)
            aria += $", {openDeferred} open deferred {Plural(openDeferred, "item", "items")}";

        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        if (tasksTotal > 0)
        {
            var totalWeight = tasks.Sum(t => Math.Max(1, t.Subtasks.Count));
            var anglePerUnit = 2 * Math.PI / totalWeight;
            var angle = -Math.PI / 2;
            foreach (var task in tasks)
            {
                var weight = Math.Max(1, task.Subtasks.Count);
                var sweep = weight * anglePerUnit;
                var cls = task.Done ? "done" : "pending";

                sb.Append($"  <path class=\"sb-seg sb-{cls}\" d=\"{AnnularSector(c, taskInner, taskOuter, InsetStart(angle, sweep, pad), InsetEnd(angle, sweep, pad))}\">");
                sb.Append($"<title>{Html(task.Text)} — {(task.Done ? "done" : "not done")}</title></path>\n");

                if (task.Subtasks.Count > 0)
                {
                    var subSweep = sweep / task.Subtasks.Count;
                    var subAngle = angle;
                    foreach (var sub in task.Subtasks)
                    {
                        var subCls = sub.Done ? "done" : "pending";
                        sb.Append($"  <path class=\"sb-seg sb-{subCls}\" d=\"{AnnularSector(c, subInner, subOuter, InsetStart(subAngle, subSweep, pad), InsetEnd(subAngle, subSweep, pad))}\">");
                        sb.Append($"<title>{Html(sub.Text)} — {(sub.Done ? "done" : "not done")}</title></path>\n");
                        subAngle += subSweep;
                    }
                }

                angle += sweep;
            }
        }

        if (hasDeferred)
        {
            var slotSweep = 2 * Math.PI / deferredItems.Count;
            var slotAngle = -Math.PI / 2;
            foreach (var slot in deferredItems)
            {
                AppendDeferredItemSlot(sb, slot, slotAngle, slotSweep, pad, c, deferredInner, deferredOuter);
                slotAngle += slotSweep;
            }
        }

        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{centerNum}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{centerLabel}</text>\n");
        sb.Append("</svg>\n");

        var legend = new List<(string, string)> { ("pending", "Not done"), ("done", "Done") };
        if (hasDeferred)
        {
            legend.Add(("followup-open", "Open follow-up"));
            legend.Add(("followup-done", "Done follow-up"));
        }
        sb.Append(SunburstLegend(legend.ToArray()));

        var hint = hasDeferred
            ? (tasksTotal > 0
                ? "Inner ring: tasks &middot; middle: subtasks &middot; outer: deferred from this story. Click a deferred segment to open it."
                : "Outer ring: deferred from this story. Click a segment to open it.")
            : "Inner ring: tasks &middot; outer ring: subtasks. Hover a segment for details.";
        sb.Append($"<div class=\"sunburst-hint\">{hint}</div>\n\n");
        return sb.ToString();
    }

    /// <summary>A grid of clickable per-epic mini-donuts — per-story DELIVERY status at a glance (done /
    /// in-review / in-dev / ready / drafted / pending, same palette + tokens as the sunburst via
    /// <see cref="StatusStyles"/>), with the epic's full name always visible and a direct link to the epic
    /// page. The "N/N detailed" figure is demoted to the sub-label so a mid-development epic no longer draws
    /// a full ring that reads as "complete." Pending epics (no stories yet) show an empty ring rather than a
    /// misleading 0%/full fill. [UXO A6]</summary>
    public static string EpicMosaic(IReadOnlyList<EpicProgress> epics, Func<EpicProgress, string> hrefBuilder)
    {
        if (epics.Count == 0) return "<div class=\"chart-empty\">Nothing to chart yet.</div>";

        var sb = new StringBuilder();
        sb.Append("<div class=\"epic-mosaic\">\n");
        foreach (var epic in epics.OrderBy(e => e.Number))
        {
            var hasStories = epic.StoryCount > 0;
            var href = hrefBuilder(epic);

            sb.Append($"  <a class=\"epic-mosaic-card\" href=\"{Html(href)}\">\n");
            sb.Append("    <div class=\"epic-mosaic-donut\">\n");
            // Visible delivery sentence restates dual counts as one fact ("6 of 7 done, 1 in review").
            // Donut stays decorative (no ariaLabel): naming it would enable per-slice tabindex inside this
            // <a>, nesting interactives. Keep "N/N stories detailed" — planning depth, not delivery.
            // [Story 8.4; UX-DR23; code-review 2026-07-15]
            var delivery = hasStories ? DeliverySentence(epic.StoryStatusCounts) : null;
            sb.Append(hasStories
                ? Donut(DeliverySegments(epic.StoryStatusCounts), size: 64)
                : Donut(Array.Empty<(string, int, string)>(), size: 64));
            sb.Append("    </div>\n");
            sb.Append("    <div class=\"epic-mosaic-label\">\n");
            sb.Append($"      <span class=\"epic-mosaic-num\">Epic {epic.Number}</span>\n");
            sb.Append($"      <span class=\"epic-mosaic-title\">{epic.Title}</span>\n");
            if (hasStories)
            {
                sb.Append($"      <span class=\"epic-mosaic-delivery\">{Html(delivery!)}</span>\n");
                sb.Append($"      <span class=\"epic-mosaic-sub\">{epic.StoriesWithArtifact} / {epic.StoryCount} stories detailed</span>\n");
            }
            else
            {
                sb.Append("      <span class=\"epic-mosaic-sub\">Not yet drafted</span>\n");
            }
            sb.Append("    </div>\n  </a>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>Restates an epic's per-status story tally as one ordered plain-language sentence over
    /// <see cref="StatusStyles.StoryStages"/> — e.g. "6 of 7 done, 1 in review". Total is Σ segments
    /// (never a parallel field); zero stages are omitted; stage words come from <see cref="StatusStyles.StoryLabel"/>.
    /// Pure projection of existing counts — no recount. [Story 8.4; UX-DR23]</summary>
    public static string DeliverySentence(IReadOnlyDictionary<string, int> counts)
    {
        var total = StatusStyles.StoryStages.Sum(stage => Math.Max(0, counts.GetValueOrDefault(stage)));
        if (total == 0) return "0 of 0 done";

        var parts = new List<string>();
        foreach (var stage in StatusStyles.StoryStages)
        {
            var n = counts.GetValueOrDefault(stage);
            if (n <= 0) continue;
            var word = StatusStyles.StoryLabel(stage).ToLowerInvariant();
            parts.Add(parts.Count == 0 ? $"{n} of {total} {word}" : $"{n} {word}");
        }
        return string.Join(", ", parts);
    }

    /// <summary>Turns an epic's per-status story tally into ordered donut segments (done → … → pending) for
    /// the delivery mosaic, colored by the shared status tokens. Zero-count stages are dropped so the ring
    /// carries only the stages actually present. [UXO A6]</summary>
    private static (string Label, int Value, string CssClass)[] DeliverySegments(IReadOnlyDictionary<string, int> counts) =>
        StatusStyles.StoryStages
            .Select(stage => (Label: StatusStyles.StoryLabel(stage), Value: counts.GetValueOrDefault(stage), CssClass: stage))
            .Where(s => s.Value > 0)
            .ToArray();

    /// <summary>A GitHub-style commit heatmap: one column per week, one row per day-of-week (Sun top to
    /// Sat bottom), shaded by commit count, with month labels along the top and Mon/Wed/Fri labels on the
    /// left. Pads the window to a minimum of ~8 weeks so a young project's grid isn't just a single sliver.
    /// <para>When <paramref name="commitsByDay"/> is provided, each active day's cell becomes an in-SVG link
    /// to an inline details panel below the chart (short hash + subject per commit, prev/next links between
    /// active days). Panel visibility is pure-CSS <c>:target</c>, so the drill-down needs no JS.</para></summary>
    public static string CommitHeatmap(
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>>? commitsByDay = null,
        bool showHeadline = true)
    {
        if (series.Count == 0) return "<div class=\"chart-empty\">No git history available.</div>";

        var byDay = series.ToDictionary(s => s.Day, s => s.Count);
        var firstCommit = series.Min(s => s.Day);
        var lastCommit = series.Max(s => s.Day);
        var today = DateOnly.FromDateTime(DateTime.Now);

        // The grid never runs past the generation date: future-dated commits (clock/timezone skew) would
        // otherwise extend it into all-blank suppressed weeks and let the headline name a day the grid can't
        // show. [Story 1.5 A4 + review]
        var end = today;
        // The heatmap is the primary "how has the work gone" visual, so show a fuller ~15-week window (it
        // scales up to fill its panel via CSS) rather than the old 7-week postage stamp. [Story 1.5 E1]
        var minStart = end.AddDays(-7 * 15);
        var start = firstCommit < minStart ? firstCommit : minStart;

        // Snap to full weeks (Sunday..Saturday) so the grid is rectangular.
        start = start.AddDays(-(int)start.DayOfWeek);
        end = end.AddDays(6 - (int)end.DayOfWeek);

        var totalDays = end.DayNumber - start.DayNumber + 1;
        var weeks = (int)Math.Ceiling(totalDays / 7.0);
        // Scale the heat over only the days the grid actually renders (<= today). A future-dated commit is
        // suppressed from the cells, so it must not inflate maxCount and depress every visible cell's level. [review]
        var maxCount = series.Where(s => s.Day <= today).Select(s => s.Count).DefaultIfEmpty(0).Max();

        const int cell = 11;
        const int gap = 3;
        const int leftGutter = 26;
        const int topGutter = 16;
        var width = leftGutter + weeks * (cell + gap);
        var height = topGutter + 7 * (cell + gap);

        // Whole-chart accessible name so the per-cell <title> tooltips (pointer-only) aren't the sole way to
        // read the heatmap: total commits, active days, and the date span. [Story 1.4 AC #1, UXO E6/H3]
        var totalCommits = series.Sum(s => s.Count);
        var activeDays = series.Count(s => s.Count > 0);
        var heatAria = $"Commit activity: {totalCommits} commit{(totalCommits == 1 ? string.Empty : "s")} across {activeDays} active day{(activeDays == 1 ? string.Empty : "s")}, {DReadable(firstCommit)} to {DReadable(lastCommit)}";

        // The linked days, resolved up front: they decide each cell's link wrapper and the SVG role. Each
        // links to its generated per-day page (commits/{date}.html). Shared with the SiteGenerator so the
        // set of linked cells and the set of generated pages can never disagree.
        var linkedDays = LinkedCommitDays(series, commitsByDay, today);
        var linkedSet = new HashSet<DateOnly>(linkedDays);

        var sb = new StringBuilder();
        // Visible one-line headline so a stakeholder reads the summary before scanning the grid. [Story 1.5 E1]
        // Suppressed when the heatmap is embedded in the consolidated Git Pulse panel, whose signal strip
        // already carries these figures (avoids a duplicate summary line). [Story 3.1 consolidation]
        if (showHeadline)
        {
            // The "last commit" date is a date in the context of a change — link it to that day's date page so the
            // reader can click through (Story 7.3/10.4). Guarded on it being a linked day: normally it is (lastCommit
            // has commits), but a future-skewed lastCommit is excluded from the linked set, so we fall back to plain
            // text. Uses the same root-relative commits/ href the cells use — never a dead link.
            var lastCommitText = DReadable(lastCommit);
            var lastCommitHtml = linkedSet.Contains(lastCommit)
                ? $"<a class=\"date-link\" href=\"commits/{D(lastCommit)}.html\">{Html(lastCommitText)}</a>"
                : Html(lastCommitText);
            sb.Append($"<div class=\"heatmap-headline\"><strong>{totalCommits}</strong> {Plural(totalCommits, "commit", "commits")} &middot; " +
                      $"<strong>{activeDays}</strong> active {Plural(activeDays, "day", "days")} &middot; last commit {lastCommitHtml}</div>\n");
        }
        // role="group" only when the day-page links exist (an img role would hide them from assistive
        // tech); a link-free render keeps role="img" so AT treats it as one named graphic.
        var role = linkedDays.Count > 0 ? "group" : "img";
        sb.Append($"<svg class=\"heatmap\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" role=\"{role}\" aria-label=\"{Html(heatAria)}\">\n");

        // Axis labels are aria-hidden: under role="group" they'd otherwise be announced as stray text;
        // the whole-chart aria-label plus per-link names carry the accessible reading. Same for month labels.
        var dayLabels = new (int Row, string Label)[] { (1, "Mon"), (3, "Wed"), (5, "Fri") };
        foreach (var (row, label) in dayLabels)
        {
            var y = topGutter + row * (cell + gap) + cell - 2;
            sb.Append($"  <text x=\"0\" y=\"{y}\" class=\"heatmap-daylabel\" aria-hidden=\"true\">{Html(label)}</text>\n");
        }

        string? lastMonth = null;
        for (var w = 0; w < weeks; w++)
        {
            var weekStart = start.AddDays(w * 7);
            var monthName = PortalDates.MonthShort(weekStart);
            if (monthName != lastMonth)
            {
                var x = leftGutter + w * (cell + gap);
                sb.Append($"  <text x=\"{x}\" y=\"{topGutter - 5}\" class=\"heatmap-monthlabel\" aria-hidden=\"true\">{Html(monthName)}</text>\n");
                lastMonth = monthName;
            }
        }

        for (var w = 0; w < weeks; w++)
        {
            for (var d = 0; d < 7; d++)
            {
                var day = start.AddDays(w * 7 + d);
                if (day > end) continue;
                // Days after generation aren't zero-commit days, they haven't happened — render nothing (no
                // fill, no tooltip) so a partial final week doesn't read as a run of inactivity. [Story 1.5 A4]
                if (day > today) continue;

                var count = byDay.GetValueOrDefault(day, 0);
                var level = HeatLevel(count, maxCount);
                var x = leftGutter + w * (cell + gap);
                var y = topGutter + d * (cell + gap);

                // Only active days link to their per-day page — zero-commit cells stay out of the tab
                // order (a ~100-cell tab stop run would be a keyboard trap; whole-chart label covers them).
                // Unlinked cells are aria-hidden so their <title>s don't read as ~100 lines of "0 commits"
                // noise under role="group"; the <title> still serves the pointer/JS tooltip. The heatmap
                // only ever renders on the root index.html, so a root-relative "commits/…" href is correct.
                var linked = linkedSet.Contains(day);
                if (linked)
                {
                    sb.Append($"  <a href=\"commits/{D(day)}.html\" aria-label=\"{Html($"{DReadable(day)}: {count} {Plural(count, "commit", "commits")} — view details")}\">");
                }
                sb.Append(linked ? "<rect" : "  <rect aria-hidden=\"true\"");
                // --col is the week index; specscribe.css derives the staggered entrance delay from it and
                // --motion-stagger (capped), so cells wipe in left-to-right one column at a time. Purely
                // decorative: it drives an opacity-only reveal that is fully neutralized under reduced motion,
                // and carries no meaning of its own. [Story 3.5 Task 2]
                sb.Append($" x=\"{x}\" y=\"{y}\" width=\"{cell}\" height=\"{cell}\" rx=\"2\" class=\"heatmap-cell level-{level}\" style=\"--col:{w}\">");
                sb.Append($"<title>{DReadable(day)}: {count} commit{(count == 1 ? string.Empty : "s")}</title></rect>");
                sb.Append(linked ? "</a>\n" : "\n");
            }
        }

        sb.Append("</svg>\n");

        sb.Append("<div class=\"heatmap-legend\">Less ");
        for (var l = 0; l <= 4; l++)
        {
            sb.Append($"<span class=\"heatmap-legend-swatch level-{l}\"></span>");
        }
        sb.Append(" More</div>\n");

        return sb.ToString();
    }

    /// <summary>The consolidated dashboard "Git Pulse" panel body — one panel that merges what used to be two
    /// (the separate "Commit Activity" heatmap and the baseline pulse). A headline signal strip (30-day count,
    /// exact last-commit timestamp, active days) sits over a two-part body: the activity heatmap and the
    /// most-changed files rendered as proportional bars. Pure HTML/CSS + inline SVG (no JS), matching the other
    /// chart builders. When the bounded name-only git call came back empty but the rest of the pulse succeeded,
    /// the files section degrades to a graceful note rather than vanishing (partial data beats none; AD-4). The
    /// whole-panel null case (no git at all) is the caller's `p.Git is {}` fallback. [Story 3.1 + consolidation]</summary>
    public static string GitPulsePanel(GitPulse git, Func<string, string?>? fileHref = null)
    {
        // The exact last-commit clock, routed through the single PortalDates formatter (Story 10.4): 24-hour,
        // no per-row zone suffix — the git clock's zone is explained once by the caption below (owner-chosen
        // "captioned git" treatment). Kept in the commit's authored offset, never converted.
        var last = PortalDates.Timestamp(git.LastCommitTimestamp);
        // The last-commit date is a date in the context of a change → link it to that day's date page, guarded on
        // actual membership in the generated date-page set (the SAME LinkedCommitDays the heatmap uses) rather than
        // a bare date comparison, so this guard can never drift from what pages actually exist — never a dead link.
        var lastDay = DateOnly.FromDateTime(git.LastCommitTimestamp);
        var linkedDays = LinkedCommitDays(git.DailySeries, git.CommitsByDay, DateOnly.FromDateTime(DateTime.Now));
        var lastLinked = linkedDays.Contains(lastDay)
            ? $"<a class=\"date-link\" href=\"commits/{D(lastDay)}.html\">{Html(last)}</a>"
            : Html(last);

        var sb = new StringBuilder();
        sb.Append("<div class=\"git-pulse\">\n");

        // Headline signal strip — the summary a stakeholder reads before scanning the grid. Carries the figures
        // the heatmap's own headline used to show, so that headline is suppressed below to avoid duplication.
        sb.Append("  <div class=\"git-pulse-signals\">\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-num\">{git.Last30DayCommitCount}</span>" +
                  $"<span class=\"git-pulse-caption\">{Plural(git.Last30DayCommitCount, "commit", "commits")} in the last 30 days</span></div>\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-when\">{lastLinked}</span>" +
                  "<span class=\"git-pulse-caption\">last commit</span></div>\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-num\">{git.ActiveDays}</span>" +
                  $"<span class=\"git-pulse-caption\">active {Plural(git.ActiveDays, "day", "days")}</span></div>\n");
        sb.Append("  </div>\n");
        // One caption for the git clock's zone (owner-chosen "captioned git"): commit times stay in each commit's
        // authored offset, so a reader knows this differs from the machine-local, zone-labeled generation footer.
        sb.Append("  <p class=\"git-pulse-zone-note\">Commit times shown in each commit&rsquo;s local time zone.</p>\n");

        sb.Append("  <div class=\"git-pulse-body\">\n");

        // Activity heatmap (headline suppressed — the signal strip above already carries those numbers).
        sb.Append("    <div class=\"git-pulse-activity\">\n");
        sb.Append(CommitHeatmap(git.DailySeries, git.CommitsByDay, showHeadline: false));
        sb.Append("    </div>\n");

        // Top changed files as proportional bars (bar width relative to the most-changed file).
        sb.Append("    <div class=\"git-pulse-files\">\n");
        sb.Append("      <div class=\"git-pulse-files-title\">Top changed files</div>\n");
        if (git.TopChangedFiles.Count > 0)
        {
            var maxChanges = git.TopChangedFiles.Max(f => f.ChangeCount);
            sb.Append("      <ol class=\"git-pulse-bars\">\n");
            foreach (var (path, changeCount) in git.TopChangedFiles)
            {
                // Floor the fill so the least-changed file still shows a visible sliver; the exact count stays
                // in text so the bar is decorative, not the sole information carrier (never color/size-only).
                var pct = maxChanges <= 0 ? 0 : Math.Clamp((int)Math.Round((double)changeCount / maxChanges * 100), 6, 100);
                // The label links to the file's in-portal code page (or external fallback) via the same seam the
                // hotspots list uses; when no resolver is supplied (e.g. the webview path) CodeItemLink returns the
                // plain escaped path, so the output is byte-identical to before.
                sb.Append(
                    $"        <li><span class=\"git-pulse-bar-label\" title=\"{Html(path)}\">{CodeItemLink(path, fileHref)}</span>" +
                    $"<span class=\"git-pulse-bar-track\"><span class=\"git-pulse-bar-fill\" style=\"width:{pct}%\"></span></span>" +
                    $"<span class=\"git-pulse-bar-count\">{changeCount} {Plural(changeCount, "change", "changes")}</span></li>\n");
            }
            sb.Append("      </ol>\n");
        }
        else
        {
            sb.Append("      <div class=\"chart-empty\">No file changes in recent history.</div>\n");
        }
        sb.Append("    </div>\n");

        sb.Append("  </div>\n");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The dashboard "Story Pipeline" — a SIDEWAYS funnel of stories flowing through delivery stages
    /// (Drafted → Ready for dev → In development → In review → Done). Counts are CUMULATIVE: each band counts
    /// the stories that have reached AT LEAST that stage, so the sequence is monotonically non-increasing and
    /// the funnel genuinely narrows left → right — the truthful silhouette falls out of the semantics instead
    /// of being forced (Story 1.5's "no chart overstates progress" rule, AC #2). Band heights are proportional
    /// to the true cumulative counts (normalized to the drafted total), joined by data-free tapered connector
    /// polygons; every stage shows its real count as text plus a %-of-stories sub-label (never
    /// height/color-only), and the hint line + per-band tooltips spell out the reached-at-least reading. A
    /// zero-count stage keeps its labeled column with a dashed placeholder band, and a nonzero stage is
    /// floored to a visible height so 1-beside-dozens never renders as a hairline. Stage tallies come from the
    /// portal-wide <see cref="ProjectCounts.DefinedStoryStages"/> ledger — no re-parsing at render time.
    /// Pure inline SVG + status-token CSS classes, no JS. [Story 3.6; Story 8.3]</summary>
    public static string RefinementFunnel(ProgressModel p) =>
        RefinementFunnel(ProjectCounts.Build(p, null, WorkInventory.Empty));

    /// <summary>Story Pipeline funnel from the portal-wide count ledger. [Story 8.3]</summary>
    public static string RefinementFunnel(ProjectCounts counts)
    {
        var total = counts.StoriesDefined;
        if (total == 0) return "<div class=\"chart-empty\">Nothing to chart yet.</div>";

        int StageCount(string css) =>
            counts.DefinedStoryStages.FirstOrDefault(s => s.CssClass == css).Count;

        var done = StageCount("done");
        var reachedReview = done + StageCount("review");
        var reachedDev = reachedReview + StageCount("active");
        var reachedReady = reachedDev + StageCount("ready");
        var stages = new (string Css, int Count, string Label, string AriaPhrase)[]
        {
            ("funnel-drafted", total, "Drafted", $"{total} {Plural(total, "story", "stories")} drafted"),
            ("funnel-ready", reachedReady, "Ready for dev", $"{reachedReady} reached ready for dev"),
            ("funnel-active", reachedDev, "In development", $"{reachedDev} reached development"),
            ("funnel-review", reachedReview, "In review", $"{reachedReview} reached review"),
            ("funnel-done", done, "Done", $"{done} done"),
        };

        // Geometry: five 104-wide columns with 24-wide connector gaps in a 640×240 viewBox, bands centered on
        // a shared midline. Nonzero stages floor at 12 tall so the smallest stage stays a visible, hoverable
        // band; a zero stage gets a slightly thinner dashed placeholder. The drafted total is the largest
        // stage by construction and >= 1 here, so the height math never divides by zero.
        const double bandWidth = 104, gapWidth = 24, xStart = 12, midY = 132, maxHeight = 136, minHeight = 12, zeroHeight = 10;
        double BandHeight(int count) => count == 0 ? zeroHeight : Math.Min(maxHeight, Math.Max(minHeight, count / (double)total * maxHeight));
        double BandX(int i) => xStart + i * (bandWidth + gapWidth);

        var aria = "Story pipeline: " + string.Join(", ", stages.Select(s => s.AriaPhrase));

        var sb = new StringBuilder();
        sb.Append("<div class=\"refinement-funnel\">\n");
        sb.Append($"<svg class=\"funnel\" viewBox=\"0 0 640 240\" width=\"640\" height=\"240\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        // Connector taper between adjacent bands first, so the colored bands render on top of the joints.
        for (var i = 0; i < stages.Length - 1; i++)
        {
            var x1 = BandX(i) + bandWidth;
            var x2 = BandX(i + 1);
            var hL = BandHeight(stages[i].Count);
            var hR = BandHeight(stages[i + 1].Count);
            sb.Append($"  <polygon class=\"funnel-connector\" points=\"{F(x1)},{F(midY - hL / 2)} {F(x2)},{F(midY - hR / 2)} " +
                      $"{F(x2)},{F(midY + hR / 2)} {F(x1)},{F(midY + hL / 2)}\" />\n");
        }

        for (var i = 0; i < stages.Length; i++)
        {
            var (css, count, label, _) = stages[i];
            var x = BandX(i);
            var cx = x + bandWidth / 2;
            var h = BandHeight(count);

            sb.Append($"  <text x=\"{F(cx)}\" y=\"20\" class=\"funnel-stage-count\" text-anchor=\"middle\">{count}</text>\n");
            sb.Append($"  <text x=\"{F(cx)}\" y=\"36\" class=\"funnel-stage-label\" text-anchor=\"middle\">{Html(label)}</text>\n");

            var cls = count == 0 ? $"funnel-band {css} funnel-zero" : $"funnel-band {css}";
            var title = i == 0
                ? $"{count} {Plural(count, "story", "stories")} drafted"
                : i == stages.Length - 1
                    ? $"{count} of {total} {Plural(count, "story is", "stories are")} done"
                    : $"{count} of {total} {Plural(count, "story has", "stories have")} reached {label}";
            sb.Append($"  <rect class=\"{cls}\" x=\"{F(x)}\" y=\"{F(midY - h / 2)}\" width=\"{F(bandWidth)}\" height=\"{F(h)}\" rx=\"3\">" +
                      $"<title>{Html(title)}</title></rect>\n");

            // %-of-stories sub-label under every stage after the first — the at-a-glance conversion figure.
            if (i > 0)
            {
                var pct = (int)Math.Round(count * 100.0 / total);
                // A nonzero stage that rounds down to 0% would contradict the real count shown above it, so
                // floor it to "<1%" ("&lt;" — the bare "<" would break the SVG parse). [Review][Patch]
                var pctText = count > 0 && pct == 0 ? "&lt;1%" : $"{pct}%";
                sb.Append($"  <text x=\"{F(cx)}\" y=\"216\" class=\"funnel-stage-sub\" text-anchor=\"middle\">{pctText} of stories</text>\n");
            }
        }
        sb.Append("</svg>\n");
        sb.Append("<div class=\"funnel-hint\">Left to right: each band counts the stories that have reached at least " +
                  "that stage &mdash; band height tracks the real count, so the taper shows work still in flight.</div>\n");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The compact coverage meter for the panel's header row (top-right): the present/total count, a
    /// small progress bar, and the % present. Kept deliberately narrow so it reads as an at-a-glance summary
    /// rather than a dominant band. Carries <c>role="progressbar"</c> semantics for assistive tech. [Story 3.3]</summary>
    public static string CoverageMeter(ArtifactCoverage coverage, DateOnly today)
    {
        var total = coverage.Families.Count;
        var present = coverage.PresentCount;
        var stale = coverage.StaleCount(today);
        var pct = total > 0 ? (int)Math.Round(present * 100.0 / total) : 0;

        var count = stale > 0
            ? $"<strong>{present}</strong>/<strong>{total}</strong> &middot; {stale} stale"
            : $"<strong>{present}</strong>/<strong>{total}</strong>";

        return "<div class=\"coverage-meter-group\">" +
               $"<span class=\"coverage-count\">{count}</span>" +
               $"<span class=\"coverage-meter\" role=\"progressbar\" aria-valuenow=\"{pct}\" aria-valuemin=\"0\" aria-valuemax=\"100\" " +
               $"aria-valuetext=\"{pct}% ({present} of {total} present)\" " +
               $"aria-label=\"Planning artifact coverage: {present} of {total} present\"><span class=\"coverage-meter-fill\" style=\"width:{pct}%\"></span></span>" +
               $"<span class=\"coverage-pct\">{pct}%</span></div>";
    }

    /// <summary>The dashboard "Planning Artifacts" panel body — a full-width intro over a 2-column card grid,
    /// one card per canonical artifact family (the coverage meter rides the panel header row via
    /// <see cref="CoverageMeter"/>). A card explains what the artifact is
    /// and carries its family accent color (matching the "Explore Key Views" pills); a PRESENT family's whole
    /// card links to its page, while a MISSING family's card carries a copyable create command (via
    /// <see cref="BmadCommands.InlineGuidance"/>, degrading to guidance text when the module exposes none). Each
    /// card is a body-level <c>js-tip</c> whose rich tooltip explains the dates (source mtime + decision-journal
    /// memlog). Only the exceptional states (Missing / Stale) get a chip — "present &amp; fresh" is the quiet
    /// default. Coverage is NOT a lifecycle axis, so cards never use the <c>--status-*</c> tokens; dates use the
    /// invariant <see cref="DReadable"/>. Rendered only when <c>!coverage.IsEmpty</c> (graceful omission). [Story 3.3]</summary>
    public static string ArtifactCoveragePanel(ArtifactCoverage coverage, DateOnly today)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"coverage\">\n");

        // Full-width intro so a reader knows what the panel answers and why it's useful. The coverage meter
        // lives in the panel header row (top-right), rendered by the caller via CoverageMeter.
        sb.Append("  <p class=\"coverage-intro\">The core planning &amp; workflow artifacts this project should have — " +
                  "which exist, how recently they changed, and how to create any that are missing.</p>\n");

        sb.Append("  <div class=\"coverage-grid\">\n");
        foreach (var family in coverage.Families)
        {
            var isStale = family.IsStale(today);
            var stateClass = !family.Present ? "missing" : isStale ? "stale" : "present";
            var accent = FamilyAccentClass(family.Label);
            var tip = Html(BuildCoverageTip(family, today));

            // Only the exceptional states carry a chip; a present, fresh family is the quiet default — no chip
            // noise (review: "we don't need Present much here").
            var chip = !family.Present ? "<span class=\"coverage-chip missing\">Missing</span>"
                : isStale ? "<span class=\"coverage-chip stale\">Stale</span>" : string.Empty;

            var head =
                $"<div class=\"coverage-card-head\"><span class=\"coverage-family\">{Icons.ForConcept(family.ConceptIconKey)}{Html(family.Label)}</span>{chip}</div>";
            var desc = $"<div class=\"coverage-desc\">{Html(family.Description)}</div>";

            if (family.Present)
            {
                // Visible freshness = the PRIMARY source mtime only; the secondary decision-journal (memlog)
                // date and full detail move into the rich tooltip, so the card stays uncluttered and "journal"
                // is explained on hover/focus rather than shown as a cryptic inline bullet.
                var freshness = family.LastModified is { } modified ? $"Updated {DReadable(modified)}" : "In the project";
                var body = head + desc + $"<div class=\"coverage-freshness\">{freshness}</div>";
                var cls = $"coverage-card js-tip {stateClass} {accent}";

                if (family.Href is { Length: > 0 } href)
                {
                    sb.Append($"    <a class=\"{cls}\" href=\"{Html(href)}\" data-tip=\"{tip}\">{body}</a>\n");
                }
                else
                {
                    // No href (e.g. the page failed to generate) and nothing else interactive on the card, so
                    // it stays non-focusable — a tabindex here would be a dead keyboard stop with no action.
                    // The tooltip content is already present in the card body for sighted/AT users either way.
                    sb.Append($"    <div class=\"{cls}\" data-tip=\"{tip}\">{body}</div>\n");
                }
            }
            else
            {
                // Missing card: the "what it is" sentence, then a copyable create command (or plain guidance
                // when the detected module exposes none). The outer card itself isn't a tab stop — the real
                // interactive control is whatever BmadCommands.InlineGuidance renders inside it, so a keyboard
                // user reaches one real action per card, not an inert card stop followed by the actual control.
                var cta = BmadCommands.InlineGuidance(family.CreateCommand, "Create it with",
                    "Add this artifact through your planning workflow.");
                sb.Append($"    <div class=\"coverage-card js-tip {stateClass} {accent}\" data-tip=\"{tip}\">" +
                          $"{head}{desc}<div class=\"coverage-cta\">{cta}</div></div>\n");
            }
        }
        sb.Append("  </div>\n");

        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>Maps a coverage family to the same artifact-family accent class the "Explore Key Views" pills use
    /// (planning=gold, architecture=teal, epics=moss, requirements=rust), so color literacy carries across the
    /// dashboard. Mirrors <c>HtmlTemplater.QuickLinkFamily</c>'s palette; labels are the fixed
    /// <see cref="ArtifactCoverage"/> family set.</summary>
    private static string FamilyAccentClass(string label) => label switch
    {
        "Architecture" => "family-architecture",
        "Epics" or "Stories" => "family-epics",
        "Requirements" => "family-requirements",
        _ => "family-planning", // PRD, Product Brief, UX, Spec Kernel — the planning (gold) family
    };

    /// <summary>The rich, body-level (never-clipped) tooltip text for a coverage card — plain, <c>\n</c>-separated
    /// for the <c>js-tip</c> node (rendered via <c>white-space: pre-line</c>). Spells out what the inline card
    /// keeps terse: the source-file freshness, the staleness note, and — clarifying the previously-cryptic
    /// "journal" — the decision-journal (<c>.memlog</c>) date; or, for a missing family, how to create it.</summary>
    private static string BuildCoverageTip(ArtifactFamily f, DateOnly today)
    {
        var sb = new StringBuilder();
        if (!f.Present)
        {
            sb.Append($"{f.Label} — missing\n{f.Description}\nNot found in this project.");
            if (f.CreateCommand is { Length: > 0 } c) sb.Append($" Create it with {c}");
            return sb.ToString();
        }

        sb.Append($"{f.Label} — present{(f.IsStale(today) ? ", stale" : string.Empty)}\n{f.Description}");
        if (f.LastModified is { } m)
        {
            sb.Append($"\nSource last edited {DReadable(m)}");
            if (f.IsStale(today)) sb.Append($" — over {ArtifactCoverage.StalenessThresholdDays} days ago");
        }
        if (f.MemlogUpdated is { } j) sb.Append($"\nDecision journal (.memlog) updated {DReadable(j)}");
        if (f.Href is { Length: > 0 }) sb.Append("\nClick to open →");
        return sb.ToString();
    }

    /// <summary>Wraps an escaped file path in a guarded code-item link (Story 7.2's dual-mode resolution,
    /// reused): a resolver returning a non-empty href renders a real link to the file's in-portal
    /// <c>code/…html</c> page (or, in external <c>--code-url</c> mode, the hosted source); no resolver or no
    /// target renders the same path plain — never a dead link. Shared by the deep-analytics hotspot list and
    /// coupling table so a file item is clickable wherever a code target exists. [[epic-7-code-link-strategy]]</summary>
    private static string CodeItemLink(string path, Func<string, string?>? fileHref)
    {
        var escaped = Html(path);
        var href = fileHref?.Invoke(path);
        return href is { Length: > 0 } ? $"<a href=\"{Html(href)}\">{escaped}</a>" : escaped;
    }

    /// <summary>The opt-in "Git Hotspots" list (FR-10): most-frequently-changed files as proportional bars
    /// (reusing the Git Pulse bar language), width relative to the busiest file. The exact count stays in text
    /// so the bar is decorative, never the sole information carrier (not size/color-only). Degrades to a
    /// friendly note when there is no change history. [Story 3.2]</summary>
    public static string HotspotBars(IReadOnlyList<(string Path, int Changes)> hotspots, Func<string, string?>? fileHref = null)
    {
        if (hotspots.Count == 0) return "<div class=\"chart-empty\">No file-change history to rank yet.</div>\n";

        var sb = new StringBuilder();
        var maxChanges = hotspots.Max(h => h.Changes);
        sb.Append("<ol class=\"git-pulse-bars deep-git-hotspots\">\n");
        foreach (var (path, changes) in hotspots)
        {
            // Floor the fill so the least-changed file still shows a visible sliver.
            var pct = maxChanges <= 0 ? 0 : Math.Clamp((int)Math.Round((double)changes / maxChanges * 100), 6, 100);
            sb.Append(
                $"  <li><span class=\"git-pulse-bar-label\" title=\"{Html(path)}\">{CodeItemLink(path, fileHref)}</span>" +
                $"<span class=\"git-pulse-bar-track\"><span class=\"git-pulse-bar-fill\" style=\"width:{pct}%\"></span></span>" +
                $"<span class=\"git-pulse-bar-count\">{changes} {Plural(changes, "change", "changes")}</span></li>\n");
        }
        sb.Append("</ol>\n");
        return sb.ToString();
    }

    /// <summary>Change coupling as a ranked table (the precise, screen-reader-friendly companion to the
    /// <see cref="CouplingGraph"/>): one row per coupled file pair with its co-change count, headed and aligned
    /// so the counts scan as a column. Full paths shown as real text (ellipsis-truncated via CSS, full value in
    /// the cell <c>title</c>) so the visual graph is never the sole information carrier. Not statuses, so no
    /// <c>--status-*</c> tokens. Degrades to a friendly note when nothing crosses the coupling threshold. [Story 3.2]</summary>
    public static string CouplingTable(IReadOnlyList<(string FileA, string FileB, int CoChanges)> coupling, Func<string, string?>? fileHref = null)
    {
        if (coupling.Count == 0) return "<div class=\"chart-empty\">No significant change coupling detected.</div>\n";

        var sb = new StringBuilder();
        sb.Append("<table class=\"coupling-table\">\n");
        sb.Append("  <thead><tr>" +
                  "<th scope=\"col\">File</th>" +
                  "<th scope=\"col\">Coupled with</th>" +
                  "<th scope=\"col\" class=\"coupling-num\">Together</th>" +
                  "</tr></thead>\n");
        sb.Append("  <tbody>\n");
        foreach (var (fileA, fileB, coChanges) in coupling)
        {
            sb.Append(
                "    <tr>" +
                $"<td class=\"coupling-file\" title=\"{Html(fileA)}\">{CodeItemLink(fileA, fileHref)}</td>" +
                $"<td class=\"coupling-file\" title=\"{Html(fileB)}\">{CodeItemLink(fileB, fileHref)}</td>" +
                $"<td class=\"coupling-num\">{coChanges}&times;</td></tr>\n");
        }
        sb.Append("  </tbody>\n</table>\n");
        return sb.ToString();
    }

    /// <summary>Change coupling as a node-link graph: one node per file, one edge per coupled pair. Nodes are
    /// laid out on a circle (deterministic — ordered by coupling degree, then ordinal path), sized by degree;
    /// edges are weighted (stroke width + opacity) by co-change count. Layout is computed here at generation
    /// time, so the whole thing is static inline SVG — no JS, matching the project's chart convention. Coupling
    /// is symmetric, so edges are undirected (no arrowheads). Colors come from the existing neutral chart
    /// tokens via CSS classes. Pointer users get per-edge/per-node <c>&lt;title&gt;</c> tooltips; the whole
    /// graph carries a summarizing <c>role="img"</c> name, and the sibling <see cref="CouplingTable"/> is the
    /// exact text equivalent so the graph is never the sole carrier. [Story 3.2]</summary>
    public static string CouplingGraph(IReadOnlyList<(string FileA, string FileB, int CoChanges)> coupling, int size = 460, Func<string, string?>? fileHref = null)
    {
        if (coupling.Count == 0) return "<div class=\"chart-empty\">No significant change coupling detected.</div>\n";

        // Node degree = total co-change weight on incident edges; drives both node size and layout order.
        var degree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (a, b, w) in coupling)
        {
            degree[a] = degree.GetValueOrDefault(a) + w;
            degree[b] = degree.GetValueOrDefault(b) + w;
        }
        var nodes = degree.Keys
            .OrderByDescending(k => degree[k])
            .ThenBy(k => k, StringComparer.Ordinal)
            .ToList();
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++) order[nodes[i]] = i;

        var c = size / 2.0;
        var ringR = size * 0.28;
        var count = nodes.Count;

        (double X, double Y) Pos(int i)
        {
            var ang = -Math.PI / 2 + 2 * Math.PI * i / count;
            return (c + ringR * Math.Cos(ang), c + ringR * Math.Sin(ang));
        }

        var maxW = coupling.Max(e => e.CoChanges);
        var minW = coupling.Min(e => e.CoChanges);
        double ScaleW(int w, double lo, double hi) =>
            maxW == minW ? (lo + hi) / 2 : lo + (hi - lo) * (w - minW) / (maxW - minW);

        var maxDeg = degree.Values.Max();
        var minDeg = degree.Values.Min();
        double NodeR(int d) => maxDeg == minDeg ? 9 : 6 + 7.0 * (d - minDeg) / (maxDeg - minDeg);

        var sb = new StringBuilder();
        var aria = $"Change coupling graph: {coupling.Count} coupled file {Plural(coupling.Count, "pair", "pairs")} across {count} {Plural(count, "file", "files")}";
        sb.Append($"<svg class=\"coupling-graph\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        // Edges first so nodes render on top of them. Width + opacity scale with the co-change count.
        foreach (var (a, b, w) in coupling)
        {
            var (x1, y1) = Pos(order[a]);
            var (x2, y2) = Pos(order[b]);
            sb.Append($"  <line class=\"coupling-edge\" x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" " +
                      $"stroke-width=\"{F(ScaleW(w, 1.5, 6))}\" stroke-opacity=\"{F(ScaleW(w, 0.35, 0.9))}\">" +
                      $"<title>{Html(Basename(a))} &harr; {Html(Basename(b))}: {w}&times; together</title></line>\n");
        }

        // Nodes + labels, placed just outside the ring and anchored away from center so text clears the circle.
        for (var i = 0; i < count; i++)
        {
            var (x, y) = Pos(i);
            var d = degree[nodes[i]];

            // Guarded code-item link (Story 7.2 dual-mode resolver): when the file has an in-portal code page
            // (or an external source base), the whole node — circle + label — is wrapped in an SVG <a> so a
            // click navigates to it; no target → the node renders exactly as before (never a dead link). The
            // <title> tooltip and role="img" stay put either way.
            var nodeHref = fileHref?.Invoke(nodes[i]);
            var linked = nodeHref is { Length: > 0 };
            if (linked) sb.Append($"  <a class=\"coupling-node-link\" href=\"{Html(nodeHref!)}\">\n");

            sb.Append($"  <circle class=\"coupling-node\" cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"{F(NodeR(d))}\">" +
                      $"<title>{Html(nodes[i])} — {d} coupled {Plural(d, "change", "changes")}</title></circle>\n");

            var ang = -Math.PI / 2 + 2 * Math.PI * i / count;
            var lx = c + (ringR + NodeR(d) + 8) * Math.Cos(ang);
            var ly = c + (ringR + NodeR(d) + 8) * Math.Sin(ang);
            var anchor = Math.Cos(ang) >= 0 ? "start" : "end";
            // font-size is in SVG user units (not a CSS rem) so the labels scale up with the graph when it is
            // enlarged in the page's expand/zoom lightbox — a fixed rem would stay tiny at any display size.
            sb.Append($"  <text class=\"coupling-label\" x=\"{F(lx)}\" y=\"{F(ly)}\" font-size=\"13\" text-anchor=\"{anchor}\" dominant-baseline=\"middle\">{Html(Shorten(Basename(nodes[i]), 22))}</text>\n");

            if (linked) sb.Append("  </a>\n");
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>The default cap on the number of citing-artifact nodes drawn on the reference-graph ring (Story 7.8,
    /// AC #2 — "node/edge counts stay bounded so a hub file's graph remains legible"). A heavily-cited hub file would
    /// otherwise crowd the ring into illegibility, and Story 7.8 adds a second (co-changed-file) population on the same
    /// ring. Over the cap, only the first <see cref="RefGraphArtifactNodeCap"/> artifacts are DRAWN; the overflow count
    /// is surfaced honestly (an on-graph "+N more" chip + the true total in the summary <c>aria-label</c>), and the
    /// caller's sr-only list still enumerates ALL citers so the accessible equivalent never drops one. A seed value,
    /// not a contract.</summary>
    public const int RefGraphArtifactNodeCap = 14;

    /// <summary>Story 7.1 / 7.8 — the relationship-first hero of a code page: a pure-SVG hub-and-spoke graph with the
    /// source file at the center and, around a ring, TWO node populations distinguished by shape AND edge style (never
    /// colour alone, NFR6/UX-DR16): (1) one linked node per citing artifact (story/epic/ADR/doc) as a gold circle on a
    /// solid spoke — the "referenced by" citations, artifact&#8594;file only — and (2, Story 7.8, when
    /// <paramref name="related"/> is supplied) one node per file this file most often changes alongside, as a neutral
    /// diamond on a DASHED spoke. Both edge kinds read honestly as citations / temporal co-changes — never as code
    /// call/dependency edges. Each artifact node is a real link to its page; each related node links to the coupled
    /// file's <c>code/…html</c> page when it has one (<c>Href</c> set) or renders as a non-link chip otherwise (never a
    /// dead link). Full titles / co-change strength ride each node's <c>&lt;title&gt;</c>/<c>aria-label</c>; a compact
    /// label stays legible on the ring. No JS. Neutral ink/gold/border tokens only — the <c>--status-*</c> lifecycle
    /// tokens are off-limits on code surfaces. The artifact ring is capped at <paramref name="artifactCap"/> (overflow
    /// surfaced honestly); related files arrive already capped upstream. Both populations empty returns nothing (the
    /// caller omits the whole block). When <paramref name="related"/> is null/empty AND the artifact count is within
    /// the cap, the output is byte-identical to the pre-7.8 single-population graph.</summary>
    public static string ReferenceGraph(
        string centerLabel,
        IReadOnlyList<(string Href, string Title, string Short)> refs,
        int size = 0,
        IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)>? related = null,
        int artifactCap = RefGraphArtifactNodeCap,
        IReadOnlyList<(int EpicNumber, string EpicTitle)?>? refEpics = null,
        bool groupByEpic = false,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? crossEdges = null,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedEdges = null)
    {
        related ??= Array.Empty<(string?, string, string, int)>();
        var refCount = refs.Count;
        var relCount = related.Count;
        if (refCount == 0 && relCount == 0) return string.Empty;

        // Bound the artifact ring (AC #2): draw at most the cap, keep the true total for the honest overflow signal.
        // Coupled files arrive pre-capped upstream (FileInsightCoupledCap). total = the nodes actually drawn.
        // EPIC-GROUPING CAP RULE (reference-graph epic grouping + relationships): the cap applies to the FLAT citer
        // list BEFORE any epic bucketing — once a citer survives this single global cap it is drawn in full under
        // its epic hub with no second, per-hub truncation layer. This keeps one honest overflow count (no "shown
        // under this hub" vs "shown overall" discrepancy) and keeps a hub's member count implicitly bounded by the
        // same artifactCap that already bounds the whole ring.
        var shownRefs = Math.Min(refCount, artifactCap);
        var overflow = refCount - shownRefs;
        var total = shownRefs + relCount;

        // Grow the canvas with the drawn node count so the ring never crowds; bounded (the graph now lives in a
        // ~320-360px sidebar and scales to fit, so an over-large viewBox would render its labels illegibly small).
        // The SAME formula (keyed off shownRefs+relCount, never the hub count) is used regardless of groupByEpic so
        // all four precomputed toggle variants share one canvas size — toggling never causes a visual "jump".
        if (size <= 0) size = Math.Clamp(360 + total * 14, 380, 560);
        var c = size / 2.0;
        var ringR = size * 0.26;

        // Both populations share one ring: artifacts first (indices 0..shownRefs-1), then related files. A single
        // deterministic sweep keeps the layout stable for the golden/parity fixtures.
        double Ang(int i) => -Math.PI / 2 + 2 * Math.PI * i / total;
        (double X, double Y) Pos(int i)
        {
            var ang = Ang(i);
            return (c + ringR * Math.Cos(ang), c + ringR * Math.Sin(ang));
        }

        // Epic-hub bucketing (only consulted when groupByEpic + refEpics are both supplied): a single forward pass
        // over the shown refs assigns each ref either its own top-level main-ring slot (no epic, e.g. an ADR/doc
        // citer) or — the first time an epic number is seen — a NEW hub slot that subsequent same-epic refs attach
        // to instead of getting their own slot. This keeps slot order tied directly to input order (deterministic,
        // no secondary sort) and non-story citers untouched (AC: "non-story citers unaffected").
        var mainSlots = new List<(bool IsHub, int EpicNumber, string EpicTitle, List<int> Members)>();
        var refSlotOf = new int[shownRefs]; // main-ring slot index that owns refs[i]'s spoke-from-center (hub or itself)
        if (groupByEpic && refEpics is not null)
        {
            var epicSlotIndex = new Dictionary<int, int>();
            for (var i = 0; i < shownRefs; i++)
            {
                var epic = i < refEpics.Count ? refEpics[i] : null;
                if (epic is { } e)
                {
                    if (!epicSlotIndex.TryGetValue(e.EpicNumber, out var slotIdx))
                    {
                        slotIdx = mainSlots.Count;
                        mainSlots.Add((true, e.EpicNumber, e.EpicTitle, new List<int>()));
                        epicSlotIndex[e.EpicNumber] = slotIdx;
                    }
                    mainSlots[slotIdx].Members.Add(i);
                    refSlotOf[i] = slotIdx;
                }
                else
                {
                    var slotIdx = mainSlots.Count;
                    mainSlots.Add((false, 0, "", new List<int> { i }));
                    refSlotOf[i] = slotIdx;
                }
            }
        }
        else
        {
            for (var i = 0; i < shownRefs; i++)
            {
                mainSlots.Add((false, 0, "", new List<int> { i }));
                refSlotOf[i] = i;
            }
        }

        var grouped = groupByEpic && refEpics is not null && mainSlots.Any(s => s.IsHub);
        var mainSlotCount = mainSlots.Count;
        // Ring-position resolver: in flat mode (the default / pre-grouping shape) this is IDENTICAL to the original
        // Pos(i)/Ang(i) sweep. In grouped mode the main ring now holds slots (hubs + ungrouped refs), not raw ref
        // indices, and each hub's member story nodes sit on a small secondary arc just outside their hub.
        double SlotAng(int slot) => grouped
            ? -Math.PI / 2 + 2 * Math.PI * slot / (mainSlotCount + relCount)
            : Ang(slot);
        (double X, double Y) SlotPos(int slot)
        {
            var ang = SlotAng(slot);
            return (c + ringR * Math.Cos(ang), c + ringR * Math.Sin(ang));
        }
        (double X, double Y) RefPos(int i)
        {
            if (!grouped) return Pos(i);
            var slot = refSlotOf[i];
            var (isHub, _, _, members) = mainSlots[slot];
            if (!isHub) return SlotPos(slot);
            // Spread this hub's members over a small arc just outside the hub's own ring angle so they read as
            // "nested under" the hub rather than colliding with it or their neighbours.
            var hubAng = SlotAng(slot);
            var idx = members.IndexOf(i);
            var m = members.Count;
            var halfWidth = Math.Min(Math.PI / (mainSlotCount + relCount), 0.5); // never wider than the slot gap
            var offset = m == 1 ? 0.0 : -halfWidth + 2 * halfWidth * (idx + 1) / (m + 1);
            var ang = hubAng + offset;
            var subR = ringR + 34;
            return (c + subR * Math.Cos(ang), c + subR * Math.Sin(ang));
        }
        (double X, double Y) RelatedPos(int j) => grouped ? SlotPos(mainSlotCount + j) : Pos(shownRefs + j);
        double RelatedAng(int j) => grouped ? SlotAng(mainSlotCount + j) : Ang(shownRefs + j);

        var sb = new StringBuilder();
        // Summary label is the real accessible name (the SVG is role="img"); it reflects BOTH populations and the true
        // (uncapped) artifact total so assistive tech never sees a smaller number than the sr-only list enumerates.
        string aria;
        if (refCount == 0)
        {
            aria = $"Reference graph: {centerLabel} changes alongside {relCount} {Plural(relCount, "file", "files")}";
        }
        else
        {
            aria = $"Reference graph: {centerLabel} is referenced by {refCount} {Plural(refCount, "artifact", "artifacts")}";
            if (overflow > 0) aria += $" ({shownRefs} shown)";
            if (relCount > 0) aria += $", and changes alongside {relCount} {Plural(relCount, "file", "files")}";
        }
        sb.Append($"<svg class=\"ref-graph\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        // Edges first so the nodes sit on top of them. Artifact spokes are solid (.ref-edge); related-file spokes are
        // dashed (.ref-edge-file) — the edge style is a primary distinguisher, so the two populations read apart even
        // in monochrome / for colour-vision-deficient readers.
        if (!grouped)
        {
            for (var i = 0; i < shownRefs; i++)
            {
                var (x, y) = Pos(i);
                sb.Append($"  <line class=\"ref-edge\" x1=\"{F(c)}\" y1=\"{F(c)}\" x2=\"{F(x)}\" y2=\"{F(y)}\" />\n");
            }
        }
        else
        {
            // One solid center-spoke per main-ring slot (a hub counts once here, not once per member) plus one
            // dashed hub->story spoke per member — so a hub reads as "file -> epic -> story" instead of every
            // story fanning straight back to the file.
            for (var slot = 0; slot < mainSlotCount; slot++)
            {
                var (x, y) = SlotPos(slot);
                sb.Append($"  <line class=\"ref-edge\" x1=\"{F(c)}\" y1=\"{F(c)}\" x2=\"{F(x)}\" y2=\"{F(y)}\" />\n");
                var (isHub, _, _, members) = mainSlots[slot];
                if (!isHub) continue;
                foreach (var i in members)
                {
                    var (sx, sy) = RefPos(i);
                    sb.Append($"  <line class=\"ref-hub-spoke\" x1=\"{F(x)}\" y1=\"{F(y)}\" x2=\"{F(sx)}\" y2=\"{F(sy)}\" />\n");
                }
            }
        }
        for (var j = 0; j < relCount; j++)
        {
            var (x, y) = RelatedPos(j);
            sb.Append($"  <line class=\"ref-edge-file\" x1=\"{F(c)}\" y1=\"{F(c)}\" x2=\"{F(x)}\" y2=\"{F(y)}\" />\n");
        }

        // "Show relationships" cross edges (opt-in): story<->related-file and related-file<->related-file. Drawn
        // in a visually lighter/thinner neutral style than either citation spoke so all edge kinds — solid gold
        // spoke, dashed related spoke, dashed hub spoke, and this dash-dot cross edge — stay distinguishable
        // (never colour alone, NFR6/UX-DR16). Indices are validated defensively (never throw on a stale index).
        if (crossEdges is { Count: > 0 })
        {
            foreach (var (refIndex, relatedIndex) in crossEdges)
            {
                if (refIndex < 0 || refIndex >= shownRefs || relatedIndex < 0 || relatedIndex >= relCount) continue;
                var (x1, y1) = RefPos(refIndex);
                var (x2, y2) = RelatedPos(relatedIndex);
                sb.Append($"  <line class=\"ref-edge-cross\" x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" />\n");
            }
        }
        if (relatedEdges is { Count: > 0 })
        {
            foreach (var (aIdx, bIdx) in relatedEdges)
            {
                if (aIdx < 0 || aIdx >= relCount || bIdx < 0 || bIdx >= relCount || aIdx == bIdx) continue;
                var (x1, y1) = RelatedPos(aIdx);
                var (x2, y2) = RelatedPos(bIdx);
                sb.Append($"  <line class=\"ref-edge-cross\" x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" />\n");
            }
        }

        // Center node: the file itself (a chip, not a link — you are already on its page).
        var cw = Math.Max(90.0, Shorten(centerLabel, 24).Length * 7 + 20);
        sb.Append("  <g class=\"ref-center\">\n");
        sb.Append($"    <rect class=\"ref-center-box\" x=\"{F(c - cw / 2)}\" y=\"{F(c - 16)}\" width=\"{F(cw)}\" height=\"32\" rx=\"6\" />\n");
        sb.Append($"    <text class=\"ref-center-label\" x=\"{F(c)}\" y=\"{F(c)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"15\">{Html(Shorten(centerLabel, 22))}</text>\n");
        sb.Append("  </g>\n");

        // Epic hub nodes (grouped mode only): a distinct neutral chip — deliberately not gold (artifact) or a
        // diamond (related file) — between the center file and its nested story nodes.
        if (grouped)
        {
            for (var slot = 0; slot < mainSlotCount; slot++)
            {
                var (isHub, epicNumber, epicTitle, _) = mainSlots[slot];
                if (!isHub) continue;
                var (x, y) = SlotPos(slot);
                var label = $"Epic {epicNumber}";
                var shortLabel = Shorten(label, 12);
                var tip = string.IsNullOrEmpty(epicTitle) ? label : $"{label}: {epicTitle}";
                // Sized from the shortened label (mirrors the center chip's cw computation) so a wider epic number
                // never clips inside a hardcoded box. The label sits INSIDE the box, centered on the same (x,y) the
                // box itself is centered on — unlike the ring/related nodes' labels (which sit beside a small dot
                // and so are pushed outward to ringR+14), the hub IS the box, so its label has nowhere else to go.
                var hw = Math.Max(30.0, shortLabel.Length * 6.5 + 14);
                sb.Append($"  <g class=\"ref-epic-hub\" role=\"img\" aria-label=\"{Html(tip)}\">")
                  .Append($"<title>{Html(tip)}</title>")
                  .Append($"<rect class=\"ref-epic-hub-box\" x=\"{F(x - hw / 2)}\" y=\"{F(y - 11)}\" width=\"{F(hw)}\" height=\"22\" rx=\"5\" />")
                  .Append($"<text class=\"ref-epic-hub-label\" x=\"{F(x)}\" y=\"{F(y)}\" font-size=\"12\" text-anchor=\"middle\" dominant-baseline=\"middle\">{Html(shortLabel)}</text>")
                  .Append("</g>\n");
            }
        }

        // Artifact ring nodes: one link per citing artifact, gold circle, label anchored away from center so text
        // clears the ring. Node shape/colour unchanged from Story 7.1 — only its (x,y) moves under epic grouping.
        for (var i = 0; i < shownRefs; i++)
        {
            var (x, y) = RefPos(i);
            var (href, title, shortLabel) = refs[i];
            // Non-hub slots sit exactly at SlotAng(slot) (no need to re-derive the angle via atan2); only a
            // hub-nested node's angle must be recomputed, since RefPos offset it off the slot's own ring angle.
            double ang;
            if (!grouped)
            {
                ang = Ang(i);
            }
            else
            {
                var slot = refSlotOf[i];
                ang = mainSlots[slot].IsHub ? Math.Atan2(y - c, x - c) : SlotAng(slot);
            }
            var lx = c + (ringR + 14) * Math.Cos(ang);
            var ly = c + (ringR + 14) * Math.Sin(ang);
            // For nested story nodes the label anchors off their own (already offset) position, not the ring radius,
            // so it doesn't fly back toward the center when a hub pushes the node off-ring.
            if (grouped && mainSlots[refSlotOf[i]].IsHub)
            {
                lx = x + 14 * Math.Cos(ang);
                ly = y + 14 * Math.Sin(ang);
            }
            var anchor = Math.Cos(ang) >= 0 ? "start" : "end";
            sb.Append($"  <a class=\"ref-node\" href=\"{Html(href)}\" aria-label=\"{Html(title)}\">")
              .Append($"<title>{Html(title)}</title>")
              .Append($"<circle class=\"ref-dot\" cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"7\" />")
              .Append($"<text class=\"ref-label\" x=\"{F(lx)}\" y=\"{F(ly)}\" font-size=\"14\" text-anchor=\"{anchor}\" dominant-baseline=\"middle\">{Html(Shorten(shortLabel, 18))}</text>")
              .Append("</a>\n");
        }

        // Related-file ring nodes (Story 7.8): neutral diamonds, linked to the coupled file's code page when one
        // exists (Href set) or a non-link chip otherwise. Tooltip/aria carry the full path + co-change strength.
        for (var j = 0; j < relCount; j++)
        {
            var (href, title, shortLabel, coChanges) = related[j];
            var (x, y) = RelatedPos(j);
            var ang = RelatedAng(j);
            var lx = c + (ringR + 14) * Math.Cos(ang);
            var ly = c + (ringR + 14) * Math.Sin(ang);
            var anchor = Math.Cos(ang) >= 0 ? "start" : "end";
            var tip = $"{title} — changed together {coChanges} {Plural(coChanges, "time", "times")}";
            // A diamond centred on (x,y), radius 7 to match the artifact dot's footprint.
            var diamond = $"<polygon class=\"ref-file-dot\" points=\"{F(x)},{F(y - 7)} {F(x + 7)},{F(y)} {F(x)},{F(y + 7)} {F(x - 7)},{F(y)}\" />";
            var label = $"<text class=\"ref-file-label\" x=\"{F(lx)}\" y=\"{F(ly)}\" font-size=\"14\" text-anchor=\"{anchor}\" dominant-baseline=\"middle\">{Html(Shorten(shortLabel, 18))}</text>";
            var linked = href is { Length: > 0 };
            if (linked)
            {
                sb.Append($"  <a class=\"ref-file-node\" href=\"{Html(href!)}\" aria-label=\"{Html(tip)}\">")
                  .Append($"<title>{Html(tip)}</title>").Append(diamond).Append(label).Append("</a>\n");
            }
            else
            {
                sb.Append($"  <g class=\"ref-file-node ref-file-node--chip\" role=\"img\" aria-label=\"{Html(tip)}\">")
                  .Append($"<title>{Html(tip)}</title>").Append(diamond).Append(label).Append("</g>\n");
            }
        }

        // Honest overflow (AC #2): the artifact ring is capped, so tell the reader how many citers are not drawn. The
        // full set stays in the caller's sr-only list, so nothing is hidden from assistive tech.
        if (overflow > 0)
        {
            sb.Append($"  <text class=\"ref-overflow\" x=\"{F(c)}\" y=\"{F(size - 8)}\" text-anchor=\"middle\" font-size=\"13\">+{overflow} more {Plural(overflow, "artifact", "artifacts")}</text>\n");
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>The last path segment (filename) of a forward-slash path — compact graph labels while the full
    /// path stays in the node's tooltip.</summary>
    private static string Basename(string path)
    {
        var i = path.LastIndexOf('/');
        return i >= 0 && i < path.Length - 1 ? path[(i + 1)..] : path;
    }

    /// <summary>Ellipsis-truncates a label to <paramref name="max"/> characters for the graph's fixed geometry.</summary>
    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>Invariant ISO date for heatmap hrefs and per-day page filenames — a culture-sensitive
    /// format would emit non-Gregorian dates (and mismatched links/filenames) on th-TH/fa-IR hosts. Delegates to
    /// the single <see cref="PortalDates.IsoDay"/> machine token (Story 10.4).</summary>
    public static string D(DateOnly day) => PortalDates.IsoDay(day);

    /// <summary>Human-readable date for user-visible text (tooltips, headline, page headings), e.g.
    /// "Mon, Jul 6, 2026". Delegates to the single <see cref="PortalDates.DayWithWeekday"/> token so the heatmap's
    /// weekday-prefixed date can never drift from the portal-wide date format (Story 10.4).</summary>
    public static string DReadable(DateOnly day) => PortalDates.DayWithWeekday(day);

    /// <summary>The single source of truth for which days get a heatmap link AND a generated per-day page:
    /// active days (count &gt; 0), on or before <paramref name="today"/>, that carry a non-empty commit
    /// list — returned in ascending order. Both the heatmap (links) and the SiteGenerator (pages) call this
    /// so a linked cell can never point at a page that wasn't generated, and vice versa.</summary>
    public static IReadOnlyList<DateOnly> LinkedCommitDays(
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>>? commitsByDay,
        DateOnly today) =>
        commitsByDay is null
            ? Array.Empty<DateOnly>()
            : series
                .Where(s => s.Count > 0 && s.Day <= today &&
                            commitsByDay.TryGetValue(s.Day, out var c) && c.Count > 0)
                .Select(s => s.Day)
                .OrderBy(d => d)
                .ToList();

    /// <summary>Renders the source-code treemap as pure, server-computed SVG (Story 7.6). One <c>&lt;rect&gt;</c>
    /// per node from the precomputed squarified <paramref name="layout"/>: directory rects draw group boundaries +
    /// a clipped label, file rects are the leaves — sized by lines of code, filled by the default colorize
    /// dimension (change frequency when git metrics exist, else a neutral fill). Every file rect is focusable
    /// (<c>tabindex="0"</c>) with a descriptive <c>aria-label</c> (name + active metric) and carries every metric as
    /// <c>data-*</c> attributes so the scoped JS enhancement re-fills it without a round-trip and the tooltip (the
    /// body-level <c>js-tip</c>/<c>data-tip</c> node) reads it. A file routes to its in-portal code page ONLY when
    /// the guarded <paramref name="fileHref"/> returns non-null — otherwise a plain, focusable rect, never a broken
    /// link; when linked, <paramref name="prefix"/> is prepended to match the text table's link discipline
    /// (<see cref="CodeMapTemplater"/>). No <c>&lt;script&gt;</c> is required for this baseline to be correct.
    /// Every label/path is HTML-escaped. [Story 7.6]</summary>
    public static string CodeTreemap(
        IReadOnlyList<TreemapRect> layout,
        double width,
        double height,
        bool hasMetrics,
        Func<string, string?>? fileHref,
        string prefix = "")
    {
        if (layout.Count == 0) return "<div class=\"chart-empty\">No source files to map.</div>";

        // The DEFAULT server-baked dimension is change frequency; compute its max once so the level buckets match
        // the JS re-bucketing (which derives the same max from the DOM). A metric-less file → neutral (level-none).
        double maxChanges = 0;
        foreach (var r in layout)
        {
            if (!r.Node.IsDirectory && r.Node.Metrics is { } m && m.Changes > maxChanges) maxChanges = m.Changes;
        }

        var sb = new StringBuilder();
        // No `id` — the page can render up to four of these (one per filter combination, Story 7.6 round 2), and
        // the JS enhancement scopes every lookup to the enclosing `.codemap-view` panel rather than a global id.
        sb.Append($"<svg class=\"codemap\" viewBox=\"0 0 {F(width)} {F(height)}\" ")
          .Append($"width=\"{F(width)}\" height=\"{F(height)}\" role=\"img\" ")
          .Append("aria-label=\"Source-code treemap: each rectangle is a file sized by its line count, nested by directory. The text table below lists every file and its metrics.\" preserveAspectRatio=\"xMidYMid meet\">\n");

        // Directories first (drawn as the containing boundaries), then files on top — the layout already emits them
        // in that depth order, so a single pass preserves stacking.
        foreach (var rect in layout)
        {
            if (rect.Node.IsDirectory)
            {
                AppendTreemapDir(sb, rect);
            }
            else
            {
                AppendTreemapFile(sb, rect, maxChanges, hasMetrics, fileHref, prefix);
            }
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>Draws only the directory boundary rect — no text label at any depth (owner decision: labels
    /// competed with the color signal at every nesting level, including the top-level project rects that used to
    /// keep one). The treemap reads as pure boxes + color; every directory/file's identity lives entirely in the
    /// tooltip card and the text-equivalent table.</summary>
    private static void AppendTreemapDir(StringBuilder sb, TreemapRect rect)
    {
        if (rect.W <= 0 || rect.H <= 0) return;
        var path = Html(rect.Node.RepoRelativePath);
        sb.Append($"  <rect class=\"codemap-dir\" data-path=\"{path}\" data-depth=\"{rect.Depth}\" ")
          .Append($"x=\"{F(rect.X)}\" y=\"{F(rect.Y)}\" width=\"{F(rect.W)}\" height=\"{F(rect.H)}\" aria-hidden=\"true\"></rect>\n");
    }

    private static void AppendTreemapFile(StringBuilder sb, TreemapRect rect, double maxChanges, bool hasMetrics, Func<string, string?>? fileHref, string prefix)
    {
        if (rect.W <= 0 || rect.H <= 0) return;
        var node = rect.Node;
        var metrics = node.Metrics;

        // Level for the default (change-frequency) dimension; a file with no git record is neutral (level-none).
        var levelClass = metrics is { } m0 ? $"level-{Bucket(m0.Changes, maxChanges)}" : "level-none";

        // Machine-readable data-* for the JS re-fill + text derivation (always path + lines; git metrics only when
        // present so the enhancement treats a metric-less file as neutral).
        var data = new StringBuilder();
        data.Append($" data-path=\"{Html(node.RepoRelativePath)}\" data-lines=\"{node.Lines.ToString(CultureInfo.InvariantCulture)}\"");
        if (metrics is { } m)
        {
            data.Append($" data-changes=\"{m.Changes.ToString(CultureInfo.InvariantCulture)}\"");
            data.Append($" data-churn=\"{m.TotalChurn.ToString(CultureInfo.InvariantCulture)}\"");
            if (m.FirstDate is { } fd) data.Append($" data-first=\"{fd.DayNumber.ToString(CultureInfo.InvariantCulture)}\"");
            if (m.LastDate is { } ld) data.Append($" data-last=\"{ld.DayNumber.ToString(CultureInfo.InvariantCulture)}\"");
            if (m.AvgCoChanged is { } co) data.Append($" data-cochanged=\"{co.ToString("0.###", CultureInfo.InvariantCulture)}\"");
        }

        // Accessible name (name + the active metric value) — color is never the sole signal (AC #4).
        var ariaLabel = metrics is { } ma
            ? $"{node.Label}, {node.Lines} {Plural((int)Math.Min(node.Lines, int.MaxValue), "line", "lines")}, {ma.Changes} {Plural(ma.Changes, "change", "changes")}"
            : $"{node.Label}, {node.Lines} {Plural((int)Math.Min(node.Lines, int.MaxValue), "line", "lines")}";

        // Rich, stylized tooltip: a server-built HTML card served through the shared body-level js-tip node (never a
        // clipped ::after on the rect). The card markup is escaped ONCE more for the attribute so getAttribute →
        // innerHTML round-trips it back to real markup (its dynamic parts are already Html-escaped inside the card).
        var card = BuildTreemapCard(node);

        var href = fileHref?.Invoke(node.RepoRelativePath);
        var isLink = href is { Length: > 0 };
        // role is omitted when wrapped in a real <a> — nesting role="link" inside an interactive <a> is invalid
        // ARIA and doubles screen-reader announcements; the anchor itself already carries the link semantics.
        var roleAttr = isLink ? string.Empty : "role=\"img\" ";
        var rectMarkup =
            $"<rect class=\"codemap-cell {levelClass} js-tip\" tabindex=\"0\"{data} " +
            $"x=\"{F(rect.X)}\" y=\"{F(rect.Y)}\" width=\"{F(rect.W)}\" height=\"{F(rect.H)}\" " +
            $"{roleAttr}aria-label=\"{Html(ariaLabel)}\" data-tip-html=\"{Html(card)}\"></rect>";

        if (isLink)
        {
            sb.Append($"  <a href=\"{Html(prefix + href)}\">{rectMarkup}</a>\n");
        }
        else
        {
            sb.Append("  ").Append(rectMarkup).Append('\n');
        }
    }

    /// <summary>Builds the stylized HTML tooltip card for a treemap file rect (served through the shared body-level
    /// js-tip node via <c>data-tip-html</c>): a name heading, the monospaced repo path, and a definition list of every
    /// available metric (lines, changes, churn, average change size, files changed together, first/last change days) —
    /// each row present only when its metric exists. Dynamic parts are HTML-escaped here; the caller escapes the whole
    /// card ONCE more for the attribute so the tip node's getAttribute → innerHTML path round-trips it back to real
    /// markup. Because every metric is a labeled text row, color is never the sole signal for whichever dimension is
    /// active (AC #4). Single-quoted internal attributes keep only the dynamic content in need of escaping. [Story 7.6]</summary>
    private static string BuildTreemapCard(CodeMapNode node)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='codemap-card'>");
        sb.Append("<strong class='codemap-card-name'>").Append(Html(node.Label)).Append("</strong>");
        sb.Append("<code class='codemap-card-path'>").Append(Html(node.RepoRelativePath)).Append("</code>");
        sb.Append("<dl class='codemap-card-metrics'>");
        Row(sb, "Lines", node.Lines.ToString("N0", CultureInfo.InvariantCulture));
        if (node.Metrics is { } m)
        {
            Row(sb, "Changes", m.Changes.ToString("N0", CultureInfo.InvariantCulture));
            Row(sb, "Churn", m.TotalChurn.ToString("N0", CultureInfo.InvariantCulture));
            if (m.Changes > 0)
            {
                var avg = (double)m.TotalChurn / m.Changes;
                Row(sb, "Avg change size", avg.ToString("N0", CultureInfo.InvariantCulture));
            }
            if (m.AvgCoChanged is { } co)
            {
                Row(sb, "Files changed together", co.ToString("N1", CultureInfo.InvariantCulture) + " avg");
            }
            if (m.FirstDate is { } fd && m.LastDate is { } ld)
            {
                Row(sb, "First / last", DReadable(fd) + " \u00b7 " + DReadable(ld));
            }
        }
        sb.Append("</dl></div>");
        return sb.ToString();

        static void Row(StringBuilder sb, string label, string value) =>
            sb.Append("<div><dt>").Append(Html(label)).Append("</dt><dd>").Append(Html(value)).Append("</dd></div>");
    }

    /// <summary>Quantizes a value onto the shared sequential ramp's 0..4 levels (the SAME discipline
    /// <see cref="HeatLevel"/> uses for the commit heatmap — a non-<c>--status-*</c> scale, since code mass/churn is
    /// not a lifecycle stage). Mirrored byte-for-byte by the treemap's JS re-bucketing. [Story 7.6]</summary>
    private static int Bucket(double value, double max)
    {
        if (max <= 0 || value <= 0) return 0;
        var ratio = value / max;
        return ratio switch
        {
            <= 0.25 => 1,
            <= 0.5 => 2,
            <= 0.75 => 3,
            _ => 4,
        };
    }

    /// <summary>Proportional bar over the four readings (Satisfied · In flight · Deferred · Unmapped), each
    /// reading a visually separated bracket whose width is proportional to its count. The <b>In flight</b>
    /// bracket sub-divides into its three real tier colors (Partially implemented / Ready / Planned) so the
    /// bar's colors match the Sankey and the six-tier donuts, and Planned is visibly part of In flight rather
    /// than an orphan segment. Colors route through <c>--status-*</c> via the tier css class (Unmapped and
    /// Planned both <c>pending</c>). Zero-count readings are omitted; empty satisfaction renders nothing
    /// (NFR8). Aria-label is the accessible text twin. Bracket order matches the chip order below it. [Story 9.9]</summary>
    public static string RequirementSatisfactionBar(ProjectCounts.RequirementSatisfaction sat)
    {
        if (sat.Total <= 0) return string.Empty;

        var aria = Html(
            $"Requirement satisfaction across {sat.Total} requirements: " +
            $"{sat.Satisfied} satisfied, " +
            $"{sat.InFlight} in flight ({sat.Active} partially implemented, {sat.Ready} ready for dev, {sat.Planned} planned), " +
            $"{sat.Deferred} deferred on purpose, {sat.Unmapped} not yet mapped");

        var sb = new StringBuilder();
        sb.Append($"<div class=\"satisfaction-bar\" role=\"img\" aria-label=\"{aria}\">\n");

        AppendSatisfactionBracket(sb, "satisfied", sat.Satisfied, new[]
        {
            ("done", sat.Done, "Done"),
        });
        // In flight keeps its three real tier colors so the bracket reads as the Sankey does — Planned (tan)
        // sits inside In flight rather than reading as a separate, unexplained bar segment. [Story 9.9 coherence]
        AppendSatisfactionBracket(sb, "in-flight", sat.InFlight, new[]
        {
            ("active", sat.Active, "Partially implemented"),
            ("ready", sat.Ready, "Ready for dev"),
            ("pending", sat.Planned, "Planned"),
        });
        AppendSatisfactionBracket(sb, "deferred", sat.Deferred, new[]
        {
            ("deferred", sat.Deferred, "Deferred"),
        });
        // Unmapped shares the tan pending token but carries a distinct css hook (+word/icon on its chip). [Story 9.9]
        AppendSatisfactionBracket(sb, "unmapped", sat.Unmapped, new[]
        {
            ("pending unmapped", sat.Unmapped, "Not yet mapped"),
        });

        sb.Append("</div>\n");
        return sb.ToString();
    }

    private static void AppendSatisfactionBracket(
        StringBuilder sb, string readingClass, int readingCount, (string SegClass, int Count, string Label)[] tiers)
    {
        if (readingCount <= 0) return;
        // flex-grow carries the proportion; the outer gap + a CSS min-width keep tiny readings visible without
        // distorting the larger ones (deterministic — integer grows, no per-visitor state). [Story 9.9]
        sb.Append($"  <span class=\"satisfaction-bracket {readingClass}\" style=\"flex-grow:{readingCount}\">\n");
        foreach (var (segClass, count, label) in tiers)
        {
            if (count <= 0) continue;
            var title = Html($"{label}: {count}");
            sb.Append($"    <span class=\"seg {segClass}\" style=\"flex-grow:{count}\" title=\"{title}\"></span>\n");
        }
        sb.Append("  </span>\n");
    }

    /// <summary>Four-reading satisfaction chips (Satisfied · In flight · Deferred on purpose · Unmapped).
    /// Each chip pairs color + icon + word (never color-only). Optional hrefs deep-link to on-page detail.
    /// In-flight tooltip expands Active/Ready/Planned. [Story 9.9]</summary>
    public static string RequirementSatisfactionChips(
        ProjectCounts.RequirementSatisfaction sat,
        string? satisfiedHref = null,
        string? inFlightHref = null,
        string? deferredHref = null,
        string? unmappedHref = null,
        string? linkPrefix = null)
    {
        if (sat.Total <= 0) return string.Empty;

        var prefix = linkPrefix ?? string.Empty;
        string? Href(string? h) => h is { Length: > 0 } ? prefix + h : null;

        var sb = new StringBuilder();
        sb.Append("<div class=\"satisfaction-chips\">\n");
        AppendSatisfactionChip(sb, "Satisfied", sat.Satisfied, "done", "done",
            StatusStyles.StageMeaning("done"), Href(satisfiedHref));
        var inFlightTip =
            $"{sat.Active} partially implemented · {sat.Ready} ready for dev · {sat.Planned} planned";
        AppendSatisfactionChip(sb, "In flight", sat.InFlight, "active", "active", inFlightTip, Href(inFlightHref));
        AppendSatisfactionChip(sb, "Deferred on purpose", sat.Deferred, "deferred", "deferred",
            StatusStyles.StageMeaning("deferred"), Href(deferredHref));
        AppendSatisfactionChip(sb, "Unmapped", sat.Unmapped, "pending", "unmapped",
            StatusStyles.StageMeaning("unmapped"), Href(unmappedHref),
            displayWord: StatusStyles.RequirementLabel(RequirementStatus.Unmapped));
        sb.Append("</div>\n");
        return sb.ToString();
    }

    private static void AppendSatisfactionChip(
        StringBuilder sb, string reading, int count, string cssClass, string iconClass,
        string tip, string? href, string? displayWord = null)
    {
        var word = displayWord ?? reading;
        var tipEsc = Html(tip);
        var label = $"{Icons.ForStatus(iconClass)}<span class=\"satisfaction-chip-word\">{Html(word)}</span>" +
                    $"<span class=\"satisfaction-chip-count\">{count}</span>";
        var cls = $"satisfaction-chip status-badge {cssClass} js-tip";
        // Zero-count chips stay visible (four-reading row) but are not links. [Story 9.9 review]
        if (href is { Length: > 0 } && count > 0)
        {
            sb.Append($"  <a class=\"{cls}\" href=\"{Html(href)}\" data-tip=\"{tipEsc}\" title=\"{tipEsc}\">{label}</a>\n");
        }
        else
        {
            sb.Append($"  <span class=\"{cls}\" data-tip=\"{tipEsc}\" title=\"{tipEsc}\">{label}</span>\n");
        }
    }

    /// <summary>The FR/NFR status-tile grid (Story 3.7 AC #1): one small square tile per requirement in source
    /// order that wraps to the next line, each an <c>&lt;a&gt;</c> to its detail page. Three redundant channels
    /// so status is never color-only (UX-DR17): the fill is the status color (via the shared
    /// <c>var(--status-*)</c> class from <see cref="StatusStyles.ForRequirement"/>), the id is visible text, and
    /// a <em>kind</em> icon (<see cref="Icons.ForRequirementKind"/>) distinguishes FR from NFR by shape. The
    /// rich, never-clipped body-level tooltip (<c>js-tip</c> + multi-line <c>data-tip</c>, served by
    /// specscribe.js) carries id + the human status word + a text snippet; a plain <c>title</c> is the no-JS
    /// fallback, and the requirement cards below remain the full text equivalent. Empty list renders nothing.
    /// HTML, not SVG — a sibling of <see cref="EpicMosaic"/>. [Story 3.7 + follow-up]</summary>
    public static string RequirementStatusGrid(IReadOnlyList<RequirementInfo> reqs, string prefix)
    {
        if (reqs.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"req-status-grid\">\n");
        foreach (var req in reqs)
        {
            var cls = StatusStyles.ForRequirement(req);
            var label = StatusStyles.RequirementLabel(req.Status);
            var snippet = Shorten(PathUtil.StripHtmlTags(req.TextHtml).Trim(), 96);
            var kind = req.Kind == RequirementKind.Functional ? "Functional" : "Non-functional";
            // Multi-line rich tooltip (white-space: pre-line): id + kind + status word + a definition snippet.
            var richTip = $"{req.Id} · {kind}\n{label}\n{snippet}";
            var titleTip = $"{req.Id} — {label}";
            var href = $"{prefix}requirements/{req.Slug}.html";
            sb.Append($"  <a class=\"req-status-block js-tip {cls}\" href=\"{Html(href)}\" data-tip=\"{Html(richTip)}\" title=\"{Html(titleTip)}\">" +
                      $"<span class=\"req-block-icon\">{Icons.ForRequirementKind(req.Kind)}</span>" +
                      $"<span class=\"req-block-id\">{Html(req.Id)}</span></a>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The six implementation-state buckets, in narrative order (most → least complete), keyed by
    /// <see cref="FlowStateKey"/> (which equals <see cref="StatusStyles.ForRequirement"/> everywhere except it
    /// splits Unmapped out of the shared tan <c>pending</c> class into its own <c>unmapped</c> bucket). The
    /// single source both the flow's state column and <see cref="RequirementFlowConservation"/> iterate, so they
    /// can never disagree. "active" is the story-derived "partially implemented" tier (Story 3.7 follow-up); the
    /// <c>unmapped</c> bucket sits alongside — not replacing — <c>pending</c>/"planned" so the Sankey and its
    /// aria text twin carry the deferred/unmapped/planned split even though the badge color reuses
    /// <c>--status-pending</c> for both (owner decision #1). [Story 9.3 Task 3]</summary>
    private static readonly (string Css, string Word)[] FlowStates =
    {
        ("done", "done"), ("active", "partially implemented"), ("ready", "ready for dev"),
        ("pending", "planned"), ("unmapped", "not yet mapped"), ("deferred", "deferred"),
    };

    /// <summary>The requirements-flow's own terminal-state bucket key. Identical to
    /// <see cref="StatusStyles.ForRequirement"/> everywhere except it routes <see cref="RequirementStatus.Unmapped"/>
    /// to its own <c>unmapped</c> bucket instead of the shared tan <c>pending</c> class the badge/card/grid/donut
    /// deliberately reuse for it. This is the ONE documented place the flow needs a bucket distinct from
    /// <see cref="StatusStyles.ForRequirement"/> — AC #2 requires the deferred/unmapped/planned split visible in
    /// the diagram (and its aria twin) even though the badge color does not get a 7th token. Do not let this
    /// exception leak into any other consumer. [Story 9.3 Task 3]</summary>
    private static string FlowStateKey(RequirementInfo req) =>
        req.Status == RequirementStatus.Unmapped ? "unmapped" : StatusStyles.ForRequirement(req);

    /// <summary>The conservation contract behind <see cref="RequirementFlow"/>, exposed for testing: the count
    /// of requirements ENTERING the flow at "definition" (= the requirement total) and how they partition across
    /// the terminal implementation states. Every requirement exits at exactly ONE state
    /// (<see cref="StatusStyles.ForRequirement"/>), so the state counts sum back to the entering total — nothing
    /// dropped, nothing double-counted, even for a multi-epic requirement (its middle-layer epic split is
    /// fractional but it still terminates in one state). [Story 3.7 + follow-up]</summary>
    public static (int Entering, IReadOnlyDictionary<string, int> ByState) RequirementFlowConservation(
        IReadOnlyList<RequirementInfo> requirements)
    {
        var byState = FlowStates.ToDictionary(s => s.Css, _ => 0);
        foreach (var req in requirements) byState[FlowStateKey(req)]++;
        return (requirements.Count, byState);
    }

    /// <summary>The requirements flow — a Sankey-style diagram of ALL requirements (FR + NFR) maturing left →
    /// right through three layers: (1) Definition (every requirement), (2) Epic coverage (one node per covering
    /// epic, plus an explicit "No coverage" node), (3) Implementation state (the five
    /// <see cref="RequirementStatus"/> buckets). A requirement covered by k epics is SPLIT evenly across them
    /// (each covering epic gets weight 1/k), so a multi-epic requirement is visibly connected to every epic that
    /// covers it — conservation still holds because its total outgoing weight is 1. A requirement with no covering
    /// epic (an unmapped FR, a deferred FR, or a — currently uncovered — NFR) routes to the "No coverage" node,
    /// then on to its own terminal state (so deferred vs planned stays visible), never dropped. Ribbon thickness
    /// and node height are proportional to the (possibly fractional) requirement weight, so the count entering at
    /// definition equals the sum reaching the states (see <see cref="RequirementFlowConservation"/>). Terminal
    /// states are the epic-rolled-up status on each requirement — "partially implemented" is an informed
    /// epic-level approximation, never a fabricated per-requirement claim (<see cref="RequirementStatus"/>). State
    /// nodes/ribbons color through the shared <c>--status-*</c> tokens; the structural definition/epic nodes use
    /// neutral base-palette chrome. Layout is computed at generation time like <see cref="CouplingGraph"/> — pure
    /// inline SVG + CSS, no JS. Whole-diagram <c>role="img"</c> name; per-node/per-ribbon <c>&lt;title&gt;</c>s
    /// for pointer users; the status-tile grid + requirement cards are the text equivalent. [Story 3.7 + follow-up]</summary>
    public static string RequirementFlow(RequirementsModel reqs, EpicsModel epics)
    {
        var all = reqs.All.ToList();
        if (all.Count == 0) return "<div class=\"chart-empty\">Nothing to chart yet.</div>";

        var n = all.Count;
        const int Sentinel = -1; // the "No coverage" node's key

        // A requirement with no covering epic routes to the "No coverage" node (weight 1); otherwise its unit
        // weight splits evenly across its covering epics (1/k each). Deterministic and conserves to 1 per req.
        static bool NoCoverage(RequirementInfo r) => r.CoverageEpicNumbers.Count == 0;
        static double Weight(RequirementInfo r, int key) =>
            key == Sentinel
                ? (NoCoverage(r) ? 1.0 : 0.0)
                : r.CoverageEpicNumbers.Contains(key) ? 1.0 / r.CoverageEpicNumbers.Count : 0.0;

        var epicKeys = all.SelectMany(r => r.CoverageEpicNumbers).Distinct().OrderBy(k => k).ToList();
        var hasNoCoverage = all.Any(NoCoverage);

        // Ordered L1 nodes: covering epics ascending, then the "No coverage" node last (if any req routes there).
        var l1Keys = new List<int>(epicKeys);
        if (hasNoCoverage) l1Keys.Add(Sentinel);

        var epicTitleByNumber = epics.Epics.ToDictionary(e => e.Number, e => PathUtil.StripHtmlTags(e.Title));
        string L1Label(int key) => key == Sentinel ? "No coverage" : $"Epic {key}";

        // L1 throughput = summed fractional weight (drives node height + ribbon thickness); L1 req count = the
        // number of DISTINCT requirements touching the node (the honest integer shown in the label/tooltip).
        double L1Throughput(int key) => all.Sum(r => Weight(r, key));
        int L1ReqCount(int key) => key == Sentinel ? all.Count(NoCoverage) : all.Count(r => r.CoverageEpicNumbers.Contains(key));

        // State nodes: the buckets actually populated (a zero state draws no node/ribbon). Shares
        // RequirementFlowConservation's counting so the two can never disagree.
        var (_, stateCount) = RequirementFlowConservation(all);
        var stateKeys = FlowStates.Where(s => stateCount[s.Css] > 0).Select(s => s.Css).ToList();

        // Fractional weight flowing from an L1 node into a terminal state. Keys by FlowStateKey (not
        // StatusStyles.ForRequirement) so Unmapped routes to its own bucket, kept split from Planned. [Story 9.3]
        double PairWeight(int l1, string state) =>
            all.Where(r => FlowStateKey(r) == state).Sum(r => Weight(r, l1));

        // Geometry. Three node columns joined by proportional ribbons; a single unit-height (pixels per
        // requirement) is shared across all columns so a ribbon of a given weight has the SAME thickness at both
        // ends. Each column's weights sum to n, so the tallest column (most gaps) fills usableH; others center.
        const double width = 760, topPad = 46, usableH = 320, gap = 14, nodeW = 15;
        const double defX = 60, epicX = 372, stateX = 628;
        var height = topPad + usableH + 26;

        var maxNodes = Math.Max(1, Math.Max(l1Keys.Count, stateKeys.Count));
        var unitH = Math.Max(2.0, (usableH - gap * (maxNodes - 1)) / n);

        // Lay a column out: ordered node weights → their (top y, height), the whole stack vertically centered.
        (double Y, double H)[] LayoutColumn(IReadOnlyList<double> weights)
        {
            var totalH = weights.Sum() * unitH + gap * Math.Max(0, weights.Count - 1);
            var y = topPad + (usableH - totalH) / 2;
            var result = new (double, double)[weights.Count];
            for (var i = 0; i < weights.Count; i++)
            {
                var h = weights[i] * unitH;
                result[i] = (y, h);
                y += h + gap;
            }
            return result;
        }

        var defLayout = LayoutColumn(new double[] { n });
        var l1Layout = LayoutColumn(l1Keys.Select(L1Throughput).ToList());
        var stateLayout = LayoutColumn(stateKeys.Select(k => (double)stateCount[k]).ToList());

        var sb = new StringBuilder();

        var done = stateCount["done"];
        var active = stateCount["active"];
        var ready = stateCount["ready"];
        var planned = stateCount["pending"];
        var unmapped = stateCount["unmapped"];
        var deferred = stateCount["deferred"];
        var noCov = all.Count(NoCoverage);
        // Unmapped is reported as its own count, never folded into "planned" — this aria string doubles as the
        // accessibility text twin AC #2 requires the split to reach. [Story 9.3 Task 3]
        var aria = $"Requirements flow: {n} {Plural(n, "requirement", "requirements")}: " +
                   $"{done} done, {active} partially implemented, {ready} ready for dev, {planned} planned, " +
                   $"{unmapped} not yet mapped, {deferred} deferred; " +
                   $"{epicKeys.Count} covering {Plural(epicKeys.Count, "epic", "epics")}, {noCov} with no coverage";

        sb.Append("<div class=\"req-flow\">\n");
        sb.Append($"<svg class=\"req-flow-svg\" viewBox=\"0 0 {F(width)} {F(height)}\" width=\"{F(width)}\" height=\"{F(height)}\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        // Column headers.
        sb.Append($"  <text x=\"{F(defX + nodeW / 2)}\" y=\"22\" class=\"req-flow-header\" text-anchor=\"middle\">Definition</text>\n");
        sb.Append($"  <text x=\"{F(epicX + nodeW / 2)}\" y=\"22\" class=\"req-flow-header\" text-anchor=\"middle\">Epic coverage</text>\n");
        sb.Append($"  <text x=\"{F(stateX + nodeW / 2)}\" y=\"22\" class=\"req-flow-header\" text-anchor=\"middle\">Implementation state</text>\n");

        // --- Ribbons first, so the node rectangles render crisply on top of them. ---

        // L0 → L1: one ribbon per L1 node, stacked on the definition node's right edge in node order.
        var (defY, defH) = defLayout[0];
        var defCursor = defY;
        for (var i = 0; i < l1Keys.Count; i++)
        {
            var (ly, lh) = l1Layout[i];
            var thickness = L1Throughput(l1Keys[i]) * unitH;
            var reqCount = L1ReqCount(l1Keys[i]);
            var ribbonTitle = $"{reqCount} {Plural(reqCount, "requirement", "requirements")} → {L1Label(l1Keys[i])}";
            sb.Append($"  <path class=\"req-flow-ribbon\" d=\"{RibbonPath(defX + nodeW, defCursor, defCursor + thickness, epicX, ly, ly + lh)}\">" +
                      $"<title>{Html(ribbonTitle)}</title></path>\n");
            defCursor += thickness;
        }

        // L1 → L2: a ribbon per (L1 node, state) pair. Outer loop L1 (so each state node's incoming stacks in
        // L1 order); inner loop states (so each L1 node's outgoing stacks in state order). Colored by target
        // state so the flow INTO "Done" reads green, etc. (routes through --status-* — a real lifecycle fill).
        var l1RightCursor = l1Keys.Select((_, i) => l1Layout[i].Y).ToArray();
        var stateLeftCursor = stateKeys.Select((_, i) => stateLayout[i].Y).ToArray();
        for (var i = 0; i < l1Keys.Count; i++)
        {
            for (var j = 0; j < stateKeys.Count; j++)
            {
                var w = PairWeight(l1Keys[i], stateKeys[j]);
                if (w <= 1e-9) continue;
                var thickness = w * unitH;
                var word = FlowStates.First(s => s.Css == stateKeys[j]).Word;
                var title = $"{L1Label(l1Keys[i])} → {word}";
                sb.Append($"  <path class=\"req-flow-ribbon {stateKeys[j]}\" d=\"{RibbonPath(epicX + nodeW, l1RightCursor[i], l1RightCursor[i] + thickness, stateX, stateLeftCursor[j], stateLeftCursor[j] + thickness)}\">" +
                          $"<title>{Html(title)}</title></path>\n");
                l1RightCursor[i] += thickness;
                stateLeftCursor[j] += thickness;
            }
        }

        // --- Nodes on top. ---

        // Definition node (structural chrome).
        sb.Append($"  <rect class=\"req-flow-node req-flow-def\" x=\"{F(defX)}\" y=\"{F(defY)}\" width=\"{F(nodeW)}\" height=\"{F(defH)}\" rx=\"2\">" +
                  $"<title>{n} {Plural(n, "requirement", "requirements")}</title></rect>\n");
        sb.Append($"  <text x=\"{F(defX + nodeW / 2)}\" y=\"{F(defY - 6)}\" class=\"req-flow-nodelabel\" text-anchor=\"middle\">{n} {Plural(n, "req", "reqs")}</text>\n");

        // Epic-coverage nodes (structural chrome; the "No coverage" node shares the class but is labeled honestly).
        for (var i = 0; i < l1Keys.Count; i++)
        {
            var (ly, lh) = l1Layout[i];
            var key = l1Keys[i];
            var count = L1ReqCount(key);
            var titleExtra = key == Sentinel
                ? "deferred, unmapped, or non-functional"
                : epicTitleByNumber.TryGetValue(key, out var t) ? t : $"Epic {key}";

            // A multi-epic requirement is split across all its covering epics, so it appears in each epic node.
            // Note how many of this node's requirements are shared with another epic, so the split is legible.
            var shared = key == Sentinel
                ? 0
                : all.Count(r => r.CoverageEpicNumbers.Contains(key) && r.CoverageEpicNumbers.Count > 1);
            var sharedNote = shared > 0 ? $" · {shared} shared with other epics" : string.Empty;

            sb.Append($"  <rect class=\"req-flow-node req-flow-epic\" x=\"{F(epicX)}\" y=\"{F(ly)}\" width=\"{F(nodeW)}\" height=\"{F(lh)}\" rx=\"2\">" +
                      $"<title>{Html(L1Label(key))}: {count} {Plural(count, "requirement", "requirements")} ({Html(titleExtra)}){Html(sharedNote)}</title></rect>\n");
            sb.Append($"  <text x=\"{F(epicX + nodeW / 2)}\" y=\"{F(ly - 6)}\" class=\"req-flow-nodelabel\" text-anchor=\"middle\">{Html(L1Label(key))}</text>\n");
        }

        // Implementation-state nodes (lifecycle fills via --status-* tokens); labels to the right, clear of ribbons.
        for (var i = 0; i < stateKeys.Count; i++)
        {
            var (sy, sh) = stateLayout[i];
            var css = stateKeys[i];
            var word = FlowStates.First(s => s.Css == css).Word;
            var count = stateCount[css];
            sb.Append($"  <rect class=\"req-flow-node req-flow-state {css}\" x=\"{F(stateX)}\" y=\"{F(sy)}\" width=\"{F(nodeW)}\" height=\"{F(sh)}\" rx=\"2\">" +
                      $"<title>{count} {Plural(count, "requirement", "requirements")} {word}</title></rect>\n");
            sb.Append($"  <text x=\"{F(stateX + nodeW + 6)}\" y=\"{F(sy + sh / 2)}\" class=\"req-flow-statelabel\" text-anchor=\"start\" dominant-baseline=\"central\">{Html(CapitalizeFirst(word))} ({count})</text>\n");
        }

        sb.Append("</svg>\n");
        sb.Append("<div class=\"req-flow-hint\">Requirements flow left to right: from definition, through the epic(s) that cover each one, into its honest implementation state. A requirement covered by several epics is split across them; deferred, unmapped, and not-yet-covered requirements are shown under &ldquo;No coverage,&rdquo; not dropped. The status tiles and requirement list below carry the same information as text.</div>\n");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>A filled Sankey ribbon (smooth band) joining a vertical span on a source node's right edge to a
    /// vertical span on a target node's left edge, with horizontal cubic control points at the midpoint so the
    /// band eases between the two columns. Pure geometry — the fill/opacity come from the CSS class.</summary>
    private static string RibbonPath(double sx, double sTop, double sBottom, double tx, double tTop, double tBottom)
    {
        var midX = (sx + tx) / 2;
        return $"M {F(sx)} {F(sTop)} " +
               $"C {F(midX)} {F(sTop)} {F(midX)} {F(tTop)} {F(tx)} {F(tTop)} " +
               $"L {F(tx)} {F(tBottom)} " +
               $"C {F(midX)} {F(tBottom)} {F(midX)} {F(sBottom)} {F(sx)} {F(sBottom)} Z";
    }

    /// <summary>Upper-cases only the first character (for a state word already lower-cased for the aria summary,
    /// reused as a visible node label) — invariant so it reads the same on any host culture.</summary>
    private static string CapitalizeFirst(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static int HeatLevel(int count, int maxCount)
    {
        if (count <= 0) return 0;
        // Uniform single-commit history (busiest day is one commit, so maxCount == 1 and count is necessarily 1):
        // render light, not maxed. Relative scaling would otherwise paint a sparse one-commit-per-day project as
        // maximally busy, which the visual-truthfulness rule forbids; level 1 reads it as light activity. Repos
        // with a busier day (maxCount >= 2) fall through to the ratio buckets below. [heatmap-debt-triage]
        if (maxCount <= 1) return 1;
        var ratio = (double)count / maxCount;
        return ratio switch
        {
            <= 0.25 => 1,
            <= 0.5 => 2,
            <= 0.75 => 3,
            _ => 4,
        };
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Grammatical pluralization for accessible names and count-bearing labels — a one-count value
    /// reads "1 story"/"1 commit", not "1 stories"/"1 commits". Shared with dashboard stat labels. [Story 1.4 AC #1, Story 1.5 A2]</summary>
    public static string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;

    private static string Html(string s) => PathUtil.Html(s);
}
