using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Pure inline SVG + CSS chart builders — no JS, no external dependencies, themed entirely via
/// the CSS variables already defined in specscribe.css. Every builder degrades gracefully at zero/low data
/// (a hallmark of a project that's just getting started).</summary>
public static class Charts
{
    /// <summary>A dashboard stat card. When <paramref name="tooltip"/> is supplied the card gains an on-brand
    /// CSS definition tooltip (via <c>data-tooltip</c>) and becomes keyboard-focusable so it's reachable by
    /// hover, focus and touch — used to define what a number actually counts (UX-DR4). [Story 1.5 C2]</summary>
    public static string StatCard(string number, string label, string? sub = null, string? tooltip = null)
    {
        var subHtml = sub is { Length: > 0 } ? $"<div class=\"stat-sub\">{Html(sub)}</div>" : string.Empty;
        var tipAttrs = tooltip is { Length: > 0 } ? $" data-tooltip=\"{Html(tooltip)}\" tabindex=\"0\"" : string.Empty;
        return $"<div class=\"stat-card\"{tipAttrs}><div class=\"stat-number\">{Html(number)}</div><div class=\"stat-label\">{Html(label)}</div>{subHtml}</div>";
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

    /// <summary>The project sunburst: inner ring = epics (angular weight max(1, story count) so pending
    /// epics stay visible), middle ring = stories, thin outer ring = task done/remaining slices where a
    /// story has a task plan — or a faint dashed PLACEHOLDER arc where a story has no task plan yet, so an
    /// unplanned story reads as "no plan" (a call to action) rather than looking identical to a story off the
    /// edge of the data. Every epic/story segment (and the placeholder) is a real link; colors come from
    /// <see cref="StatusStyles"/> via .sb-* CSS classes. When <paramref name="commands"/> exposes a
    /// create-story command, the placeholder tooltip names it. Pure SVG — no JS. [UXO E4]</summary>
    public static string Sunburst(EpicsModel model, int size = 380, CommandCatalog? commands = null)
    {
        var epics = model.Epics.OrderBy(e => e.Number).ToList();
        if (epics.Count == 0) return "<div class=\"chart-empty\">Nothing to chart yet.</div>";

        var c = size / 2.0;
        // Ring radii, proportional to the overall size.
        var epicInner = size * 0.16;
        var epicOuter = size * 0.28;
        var storyInner = size * 0.285;
        var storyOuter = size * 0.415;
        var taskInner = size * 0.42;
        var taskOuter = size * 0.465;

        var totalWeight = epics.Sum(e => Math.Max(1, e.Stories.Count));
        var anglePerUnit = 2 * Math.PI / totalWeight;
        const double pad = 0.006; // radians of breathing room between segments

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"Project progress sunburst\">\n");

        var angle = -Math.PI / 2; // start at 12 o'clock
        foreach (var epic in epics)
        {
            var weight = Math.Max(1, epic.Stories.Count);
            var sweep = weight * anglePerUnit;
            var epicClass = StatusStyles.ForEpic(epic);
            var epicTitle = PathUtil.StripHtmlTags(epic.Title);

            // aria-label carries the same name+status+count as the hover-only <title>, so keyboard and
            // screen-reader users get it on focus without a pointer; the <title> stays for pointer tooltips.
            var epicAria = $"Epic {epic.Number}: {epicTitle} — {StatusStyles.EpicLabel(epicClass)}, {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}";
            sb.Append($"  <a href=\"epics/epic-{epic.Number}.html\" aria-label=\"{Html(epicAria)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{epicClass}\" d=\"{AnnularSector(c, epicInner, epicOuter, angle + pad, angle + sweep - pad)}\">");
            sb.Append($"<title>Epic {epic.Number}: {Html(epicTitle)} — {Html(StatusStyles.EpicLabel(epicClass))}, {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}</title></path>\n");
            sb.Append("  </a>\n");

            if (epic.Stories.Count > 0)
            {
                var storySweep = sweep / epic.Stories.Count;
                var storyAngle = angle;
                foreach (var story in epic.Stories)
                {
                    var storyClass = StatusStyles.ForStory(story);
                    // Undrafted stories still get a generated placeholder page at StoryPagePath (see
                    // SiteGenerator's placeholder emission) — link there instead of the epic page so the
                    // sunburst always drops the reader on the story's own page. [Story 2.3 redesign]
                    var storyHref = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
                    var storyTitle = PathUtil.StripHtmlTags(story.Title);
                    var statusNote = story.Status is { Length: > 0 } s ? $" — {s}" : string.Empty;

                    var storyAria = $"Story {story.Id}: {storyTitle}{statusNote}";
                    sb.Append($"  <a href=\"{Html(storyHref)}\" aria-label=\"{Html(storyAria)}\">\n");
                    sb.Append($"    <path class=\"sb-seg sb-{storyClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, storyAngle + pad, storyAngle + storySweep - pad)}\">");
                    sb.Append($"<title>Story {story.Id}: {Html(storyTitle)}{Html(statusNote)}</title></path>\n  </a>\n");

                    // Task ring: split the story's span by done/remaining, only when tasks exist.
                    if (story.TasksTotal > 0)
                    {
                        var doneSweep = (storySweep - 2 * pad) * story.TasksDone / story.TasksTotal;
                        if (story.TasksDone > 0)
                        {
                            var doneAria = $"Story {story.Id}: {story.TasksDone} of {story.TasksTotal} {Plural(story.TasksTotal, "task", "tasks")} done";
                            sb.Append($"  <a href=\"{Html(storyHref)}\" aria-label=\"{Html(doneAria)}\"><path class=\"sb-seg sb-done\" d=\"{AnnularSector(c, taskInner, taskOuter, storyAngle + pad, storyAngle + pad + doneSweep)}\">");
                            sb.Append($"<title>Story {story.Id}: {story.TasksDone} of {story.TasksTotal} {Plural(story.TasksTotal, "task", "tasks")} done</title></path></a>\n");
                        }
                        if (story.TasksDone < story.TasksTotal)
                        {
                            var remainAria = $"Story {story.Id}: {story.TasksTotal - story.TasksDone} {Plural(story.TasksTotal - story.TasksDone, "task", "tasks")} remaining";
                            sb.Append($"  <a href=\"{Html(storyHref)}\" aria-label=\"{Html(remainAria)}\"><path class=\"sb-seg sb-pending\" d=\"{AnnularSector(c, taskInner, taskOuter, storyAngle + pad + doneSweep, storyAngle + storySweep - pad)}\">");
                            sb.Append($"<title>Story {story.Id}: {story.TasksTotal - story.TasksDone} {Plural(story.TasksTotal - story.TasksDone, "task", "tasks")} remaining</title></path></a>\n");
                        }
                    }
                    else
                    {
                        AppendNoPlanArc(sb, story, storyHref, c, taskInner, taskOuter, storyAngle + pad, storyAngle + storySweep - pad, commands);
                    }

                    storyAngle += storySweep;
                }
            }

            angle += sweep;
        }

        // The chart is organized around its epics (inner ring), so the center headlines the epic count — the
        // story/task rings tell the finer-grained story. [spec-sunburst-epic-focus-and-ready-rollup]
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{epics.Count}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{Plural(epics.Count, "epic", "epics")}</text>\n");
        sb.Append("</svg>\n");

        sb.Append(SunburstLegend(
            ("pending", "Pending"), ("drafted", "Drafted"), ("ready", "Ready for dev"),
            ("active", "In development"), ("review", "In review"), ("done", "Done")));
        sb.Append("<div class=\"sunburst-hint\">Inner ring: epics &middot; middle: stories &middot; outer: task completion. Click any segment to open it.</div>\n\n");
        return sb.ToString();
    }

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
                      $"<span class=\"swatch sb-{status}-sw\"></span>{label}</span>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The E4 placeholder arc: a faint dashed outer-ring sector for a story that has no task plan
    /// yet, turning an otherwise-blank gap into a call to action. Kept a real link (to the story/epic) and
    /// carries a <c>&lt;title&gt;</c> + <c>aria-label</c> so it reads "No task plan yet — run
    /// /…-create-story N.N" for pointer, keyboard and screen-reader users alike; the command is only named
    /// when the active module actually exposes it (never a command that doesn't exist). Pure SVG + CSS — the
    /// dashed look is the <c>.sb-noplan</c> class, not a JS effect. [UXO E4]</summary>
    private static void AppendNoPlanArc(StringBuilder sb, StoryInfo story, string href, double c, double rInner, double rOuter, double a0, double a1, CommandCatalog? commands)
    {
        var command = commands?.Command("create-story", story.Id);
        var title = command is { Length: > 0 }
            ? $"Story {story.Id}: no task plan yet — run {command}"
            : $"Story {story.Id}: no task plan yet";
        var aria = $"Story {story.Id}: no task plan yet";

        sb.Append($"  <a href=\"{Html(href)}\" aria-label=\"{Html(aria)}\"><path class=\"sb-seg sb-noplan\" d=\"{AnnularSector(c, rInner, rOuter, a0, a1)}\">");
        sb.Append($"<title>{Html(title)}</title></path></a>\n");
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

    /// <summary>An epic-scoped sunburst: inner ring = this epic's stories (equal weight, colored by
    /// status), outer ring = task done/remaining per story where a task plan exists. A cropped version of
    /// the project-wide <see cref="Sunburst"/> — no epic ring needed since we're already on that epic's
    /// page. Every segment is a real link: to the story's detail page when one exists, otherwise an
    /// in-page anchor down to its story card (supplied via <paramref name="hrefBuilder"/>). A story with no
    /// task plan yet gets the same faint dashed placeholder arc as the project sunburst. [UXO E4]</summary>
    public static string EpicSunburst(EpicInfo epic, Func<StoryInfo, string> hrefBuilder, int size = 320, CommandCatalog? commands = null)
    {
        if (epic.Stories.Count == 0) return "<div class=\"chart-empty\">No stories drafted for this epic yet.</div>";

        var c = size / 2.0;
        var storyInner = size * 0.16;
        var storyOuter = size * 0.36;
        var taskInner = size * 0.37;
        var taskOuter = size * 0.46;

        var count = epic.Stories.Count;
        var anglePerStory = 2 * Math.PI / count;
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

            var storyAria = $"Story {story.Id}: {storyTitle}{statusNote}";
            sb.Append($"  <a href=\"{Html(href)}\" aria-label=\"{Html(storyAria)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{storyClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, angle + pad, angle + anglePerStory - pad)}\">");
            sb.Append($"<title>Story {story.Id}: {Html(storyTitle)}{Html(statusNote)}</title></path>\n  </a>\n");

            if (story.TasksTotal > 0)
            {
                var doneSweep = (anglePerStory - 2 * pad) * story.TasksDone / story.TasksTotal;
                if (story.TasksDone > 0)
                {
                    var doneAria = $"Story {story.Id}: {story.TasksDone} of {story.TasksTotal} {Plural(story.TasksTotal, "task", "tasks")} done";
                    sb.Append($"  <a href=\"{Html(href)}\" aria-label=\"{Html(doneAria)}\"><path class=\"sb-seg sb-done\" d=\"{AnnularSector(c, taskInner, taskOuter, angle + pad, angle + pad + doneSweep)}\">");
                    sb.Append($"<title>Story {story.Id}: {story.TasksDone} of {story.TasksTotal} {Plural(story.TasksTotal, "task", "tasks")} done</title></path></a>\n");
                }
                if (story.TasksDone < story.TasksTotal)
                {
                    var remainAria = $"Story {story.Id}: {story.TasksTotal - story.TasksDone} {Plural(story.TasksTotal - story.TasksDone, "task", "tasks")} remaining";
                    sb.Append($"  <a href=\"{Html(href)}\" aria-label=\"{Html(remainAria)}\"><path class=\"sb-seg sb-pending\" d=\"{AnnularSector(c, taskInner, taskOuter, angle + pad + doneSweep, angle + anglePerStory - pad)}\">");
                    sb.Append($"<title>Story {story.Id}: {story.TasksTotal - story.TasksDone} {Plural(story.TasksTotal - story.TasksDone, "task", "tasks")} remaining</title></path></a>\n");
                }
            }
            else
            {
                AppendNoPlanArc(sb, story, href, c, taskInner, taskOuter, angle + pad, angle + anglePerStory - pad, commands);
            }

            angle += anglePerStory;
        }

        // The inner ring is this epic's stories, so headline the story count (matching the project sunburst's
        // epic-count center) rather than a task fraction that duplicates the outer ring. [epic-sunburst story-count]
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{count}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{Plural(count, "story", "stories")}</text>\n");
        sb.Append("</svg>\n");

        sb.Append(SunburstLegend(
            ("pending", "Pending"), ("drafted", "Drafted"), ("ready", "Ready for dev"),
            ("active", "In development"), ("review", "In review"), ("done", "Done")));
        sb.Append("<div class=\"sunburst-hint\">Inner ring: stories &middot; outer: task completion. Click any segment to open it.</div>\n\n");
        return sb.ToString();
    }

    /// <summary>A per-story task sunburst: inner ring = top-level tasks, outer ring = their subtasks,
    /// weighted so a task with many subtasks gets proportionally more outer-ring space. Same visual
    /// language as the project sunburst (green = done, grey = not done yet) but scoped to one story's
    /// checklist — there are no task pages to link to, so segments carry a tooltip only, not a link.</summary>
    public static string TaskSunburst(IReadOnlyList<TaskItem> tasks, int size = 280)
    {
        if (tasks.Count == 0) return "<div class=\"chart-empty\">No tasks tracked for this story yet.</div>";

        var c = size / 2.0;
        var taskInner = size * 0.16;
        var taskOuter = size * 0.36;
        var subInner = size * 0.37;
        var subOuter = size * 0.48;

        var totalWeight = tasks.Sum(t => Math.Max(1, t.Subtasks.Count));
        var anglePerUnit = 2 * Math.PI / totalWeight;
        const double pad = 0.01;

        // Center headline is the top-level task tally, consistent with every other page's "tasks" figure
        // (home, epic, project/epic sunburst outer rings) — subtasks stay visible via the outer ring and
        // tooltips instead of being folded into the number so it no longer reads as a subtask count.
        var tasksDone = tasks.Count(t => t.Done);
        var tasksTotal = tasks.Count;

        var sb = new StringBuilder();
        // The chart itself is not focusable (no task pages to drill to); the story page renders the task
        // checklist as real text as the non-pointer equivalent. Its role="img" name carries the tally so the
        // whole chart is still announced. [Story 1.4 AC #1]
        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"{Html($"Task breakdown: {tasksDone} of {tasksTotal} tasks done")}\">\n");

        var angle = -Math.PI / 2;
        foreach (var task in tasks)
        {
            var weight = Math.Max(1, task.Subtasks.Count);
            var sweep = weight * anglePerUnit;
            var cls = task.Done ? "done" : "pending";

            sb.Append($"  <path class=\"sb-seg sb-{cls}\" d=\"{AnnularSector(c, taskInner, taskOuter, angle + pad, angle + sweep - pad)}\">");
            sb.Append($"<title>{Html(task.Text)} — {(task.Done ? "done" : "not done")}</title></path>\n");

            if (task.Subtasks.Count > 0)
            {
                var subSweep = sweep / task.Subtasks.Count;
                var subAngle = angle;
                foreach (var sub in task.Subtasks)
                {
                    var subCls = sub.Done ? "done" : "pending";
                    sb.Append($"  <path class=\"sb-seg sb-{subCls}\" d=\"{AnnularSector(c, subInner, subOuter, subAngle + pad, subAngle + subSweep - pad)}\">");
                    sb.Append($"<title>{Html(sub.Text)} — {(sub.Done ? "done" : "not done")}</title></path>\n");
                    subAngle += subSweep;
                }
            }

            angle += sweep;
        }

        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{tasksDone}/{tasksTotal}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">tasks</text>\n");
        sb.Append("</svg>\n");

        sb.Append(SunburstLegend(("pending", "Not done"), ("done", "Done")));
        sb.Append("<div class=\"sunburst-hint\">Inner ring: tasks &middot; outer ring: subtasks. Hover a segment for details.</div>\n\n");
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
            sb.Append(hasStories
                ? Donut(DeliverySegments(epic.StoryStatusCounts), size: 64)
                : Donut(Array.Empty<(string, int, string)>(), size: 64));
            sb.Append("    </div>\n");
            sb.Append("    <div class=\"epic-mosaic-label\">\n");
            sb.Append($"      <span class=\"epic-mosaic-num\">Epic {epic.Number}</span>\n");
            sb.Append($"      <span class=\"epic-mosaic-title\">{epic.Title}</span>\n");
            sb.Append(hasStories
                ? $"      <span class=\"epic-mosaic-sub\">{epic.StoriesWithArtifact} / {epic.StoryCount} stories detailed</span>\n"
                : "      <span class=\"epic-mosaic-sub\">Not yet drafted</span>\n");
            sb.Append("    </div>\n  </a>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
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
            sb.Append($"<div class=\"heatmap-headline\"><strong>{totalCommits}</strong> {Plural(totalCommits, "commit", "commits")} &middot; " +
                      $"<strong>{activeDays}</strong> active {Plural(activeDays, "day", "days")} &middot; last commit {DReadable(lastCommit)}</div>\n");
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
            var monthName = weekStart.ToString("MMM", CultureInfo.InvariantCulture);
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
    public static string GitPulsePanel(GitPulse git)
    {
        var last = git.LastCommitTimestamp.ToString("ddd, MMM d, yyyy 'at' HH:mm", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append("<div class=\"git-pulse\">\n");

        // Headline signal strip — the summary a stakeholder reads before scanning the grid. Carries the figures
        // the heatmap's own headline used to show, so that headline is suppressed below to avoid duplication.
        sb.Append("  <div class=\"git-pulse-signals\">\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-num\">{git.Last30DayCommitCount}</span>" +
                  $"<span class=\"git-pulse-caption\">{Plural(git.Last30DayCommitCount, "commit", "commits")} in the last 30 days</span></div>\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-when\">{Html(last)}</span>" +
                  "<span class=\"git-pulse-caption\">last commit</span></div>\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-num\">{git.ActiveDays}</span>" +
                  $"<span class=\"git-pulse-caption\">active {Plural(git.ActiveDays, "day", "days")}</span></div>\n");
        sb.Append("  </div>\n");

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
                sb.Append(
                    $"        <li><span class=\"git-pulse-bar-label\" title=\"{Html(path)}\">{Html(path)}</span>" +
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

    /// <summary>The dashboard refinement funnel — an HONEST elaboration pyramid, not a classic narrowing
    /// funnel: planning counts GROW down the pipeline (few epics fan out into many stories and far more
    /// tasks), so each stage's band width is proportional to its true count, normalized to the largest stage,
    /// and is never rescaled to force a narrowing silhouette (that would overstate progress — Story 1.5's
    /// "no chart overstates progress" rule, AC #2). "How much detail work remains" is carried by the per-stage
    /// coverage sub-labels (epics broken into stories, stories with a task plan), and every stage shows its
    /// real count as text (never width/color-only). A zero-count stage keeps its labeled row with a dashed
    /// placeholder band, and a nonzero stage is floored to a visible width so a count of 1 beside hundreds
    /// never renders as a hairline. Pure inline SVG + status-token CSS classes, no JS. [Story 3.6]</summary>
    public static string RefinementFunnel(ProgressModel p)
    {
        if (p.EpicsTotal == 0) return "<div class=\"chart-empty\">Nothing to chart yet.</div>";

        // Stage 3's coverage sub-label needs a story denominator; with no stories drafted yet, "0 of 0" would
        // read as noise, so the sub-label is simply omitted (the 0 count row still renders).
        var planSub = p.StoriesTotal > 0
            ? $"{p.StoriesWithArtifact} of {p.StoriesTotal} {Plural(p.StoriesTotal, "story", "stories")} {Plural(p.StoriesWithArtifact, "has", "have")} a task plan"
            : null;
        var stages = new (string Css, int Count, string Label, string? Sub)[]
        {
            ("funnel-epics", p.EpicsTotal, Plural(p.EpicsTotal, "epic", "epics"), null),
            ("funnel-stories", p.StoriesTotal, Plural(p.StoriesTotal, "story drafted", "stories drafted"),
                $"{p.EpicsDrafted} of {p.EpicsTotal} {Plural(p.EpicsTotal, "epic", "epics")} broken into stories"),
            ("funnel-planned", p.StoriesWithArtifact,
                Plural(p.StoriesWithArtifact, "story with a task plan", "stories with a task plan"), planSub),
            ("funnel-tasks", p.TasksTotal, Plural(p.TasksTotal, "task planned", "tasks planned"), null),
        };

        // Geometry: bands centered in a 640-wide viewBox with 600 usable; nonzero stages floor at 46 so the
        // smallest stage stays a visible, hoverable band. maxStage >= EpicsTotal >= 1, so no divide-by-zero.
        const double usableWidth = 600, minWidth = 46, centerX = 320;
        var maxStage = stages.Max(s => s.Count);

        var aria = "Refinement funnel: " + string.Join(", ", stages.Select(s => $"{s.Count} {s.Label}"));

        var sb = new StringBuilder();
        sb.Append("<div class=\"refinement-funnel\">\n");
        sb.Append($"<svg class=\"funnel\" viewBox=\"0 0 640 252\" width=\"640\" height=\"252\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        var y = 10.0;
        foreach (var (css, count, label, sub) in stages)
        {
            var subTspan = sub is null ? string.Empty : $"<tspan class=\"funnel-stage-sub\"> &middot; {Html(sub)}</tspan>";
            sb.Append($"  <text x=\"{F(centerX)}\" y=\"{F(y + 11)}\" class=\"funnel-stage-text\" text-anchor=\"middle\">" +
                      $"<tspan class=\"funnel-stage-count\">{count}</tspan> {Html(label)}{subTspan}</text>\n");

            var width = count == 0 ? minWidth : Math.Max(minWidth, count / (double)maxStage * usableWidth);
            var cls = count == 0 ? $"funnel-band {css} funnel-zero" : $"funnel-band {css}";
            var title = sub is null ? $"{count} {label}" : $"{count} {label} — {sub}";
            sb.Append($"  <rect class=\"{cls}\" x=\"{F(centerX - width / 2)}\" y=\"{F(y + 20)}\" width=\"{F(width)}\" height=\"26\" rx=\"6\">" +
                      $"<title>{Html(title)}</title></rect>\n");
            y += 62;
        }
        sb.Append("</svg>\n");
        sb.Append("<div class=\"funnel-hint\">Each stage breaks the one above into finer detail &mdash; " +
                  "band width tracks the real count; the sub-labels show how much refinement remains.</div>\n");
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
                    sb.Append($"    <div class=\"{cls}\" data-tip=\"{tip}\" tabindex=\"0\">{body}</div>\n");
                }
            }
            else
            {
                // Missing card: the "what it is" sentence, then a copyable create command (or plain guidance
                // when the detected module exposes none). Not a link — the action is "create it", not "open it".
                var cta = BmadCommands.InlineGuidance(family.CreateCommand, "Create it with",
                    "Add this artifact through your planning workflow.");
                sb.Append($"    <div class=\"coverage-card js-tip {stateClass} {accent}\" data-tip=\"{tip}\" tabindex=\"0\">" +
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

    /// <summary>The opt-in "Git Hotspots" list (FR-10): most-frequently-changed files as proportional bars
    /// (reusing the Git Pulse bar language), width relative to the busiest file. The exact count stays in text
    /// so the bar is decorative, never the sole information carrier (not size/color-only). Degrades to a
    /// friendly note when there is no change history. [Story 3.2]</summary>
    public static string HotspotBars(IReadOnlyList<(string Path, int Changes)> hotspots)
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
                $"  <li><span class=\"git-pulse-bar-label\" title=\"{Html(path)}\">{Html(path)}</span>" +
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
    public static string CouplingTable(IReadOnlyList<(string FileA, string FileB, int CoChanges)> coupling)
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
                $"<td class=\"coupling-file\" title=\"{Html(fileA)}\">{Html(fileA)}</td>" +
                $"<td class=\"coupling-file\" title=\"{Html(fileB)}\">{Html(fileB)}</td>" +
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
    public static string CouplingGraph(IReadOnlyList<(string FileA, string FileB, int CoChanges)> coupling, int size = 460)
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
            sb.Append($"  <circle class=\"coupling-node\" cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"{F(NodeR(d))}\">" +
                      $"<title>{Html(nodes[i])} — {d} coupled {Plural(d, "change", "changes")}</title></circle>\n");

            var ang = -Math.PI / 2 + 2 * Math.PI * i / count;
            var lx = c + (ringR + NodeR(d) + 8) * Math.Cos(ang);
            var ly = c + (ringR + NodeR(d) + 8) * Math.Sin(ang);
            var anchor = Math.Cos(ang) >= 0 ? "start" : "end";
            // font-size is in SVG user units (not a CSS rem) so the labels scale up with the graph when it is
            // enlarged in the page's expand/zoom lightbox — a fixed rem would stay tiny at any display size.
            sb.Append($"  <text class=\"coupling-label\" x=\"{F(lx)}\" y=\"{F(ly)}\" font-size=\"13\" text-anchor=\"{anchor}\" dominant-baseline=\"middle\">{Html(Shorten(Basename(nodes[i]), 22))}</text>\n");
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
    /// format would emit non-Gregorian dates (and mismatched links/filenames) on th-TH/fa-IR hosts.</summary>
    public static string D(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Human-readable date for user-visible text (tooltips, headline, page headings), e.g.
    /// "Mon, Jul 6, 2026". Invariant so it reads the same regardless of host culture.</summary>
    public static string DReadable(DateOnly day) => day.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);

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

    /// <summary>Renders the project/artifact structure tree as native, zero-JS disclosure widgets: every
    /// directory branch is a <c>&lt;details&gt;</c>/<c>&lt;summary&gt;</c> (focusable, Enter/Space toggles it, and
    /// the browser natively announces its expanded/collapsed state — AC #1's expand/collapse and AC #2's
    /// "announced state" with NO JavaScript, exactly like the <c>.dev-agent-details</c> and <c>.story</c>
    /// disclosures already do), and leaves are <c>&lt;li&gt;</c> rows. A leaf that maps to a generated page is an
    /// <c>&lt;a href&gt;</c> (route to the page — AC #2); a leaf without one is plain text — routing is
    /// best-effort, never a broken link. Top-level branches render <c>open</c> (a reviewer landing on the surface
    /// sees the shape, not a closed root); deeper branches render closed. Static — no motion (Story 3.5 owns
    /// insight animation), so reduced-motion is trivially satisfied. Every label/href is HTML-escaped. [Story 3.4]</summary>
    public static string ProjectStructureTree(ProjectTree tree)
    {
        // structure.html sits at the output root (prefix ""), but derive the prefix rather than hardcode "no
        // prefix" so the renderer stays correct if the surface ever moves. [Subtask 2.3]
        var prefix = PathUtil.RelativePrefix(SiteNav.StructureOutputPath);

        var sb = new StringBuilder();
        sb.Append("<div class=\"structure-tree\">\n");
        sb.Append("<ul class=\"tree-list\">\n");
        foreach (var node in tree.Roots)
        {
            AppendTreeNode(sb, node, prefix, depth: 0);
        }
        sb.Append("</ul>\n</div>\n");
        return sb.ToString();
    }

    private static void AppendTreeNode(StringBuilder sb, TreeNode node, string prefix, int depth)
    {
        if (node.IsDirectory)
        {
            // Top-level branches default open so the shape is visible on landing; deeper branches start closed.
            var open = depth == 0 ? " open" : string.Empty;
            sb.Append("<li class=\"tree-branch\">");
            sb.Append($"<details{open}><summary>{Icons.ForConcept("Structure")}<span class=\"tree-label\">{Html(node.Label)}</span></summary>");
            sb.Append("<ul class=\"tree-list\">");
            foreach (var child in node.Children)
            {
                AppendTreeNode(sb, child, prefix, depth + 1);
            }
            sb.Append("</ul></details></li>\n");
        }
        else if (node.OutputHref is { Length: > 0 } href)
        {
            sb.Append($"<li class=\"tree-leaf\"><a class=\"tree-file\" href=\"{Html(prefix + href)}\">{Html(node.Label)}</a></li>\n");
        }
        else
        {
            sb.Append($"<li class=\"tree-leaf\"><span class=\"tree-file\">{Html(node.Label)}</span></li>\n");
        }
    }

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
