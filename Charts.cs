using System.Globalization;
using System.Text;

namespace DocsForge;

/// <summary>Pure inline SVG + CSS chart builders — no JS, no external dependencies, themed entirely via
/// the CSS variables already defined in docsforge.css. Every builder degrades gracefully at zero/low data
/// (a hallmark of a project that's just getting started).</summary>
public static class Charts
{
    public static string StatCard(string number, string label, string? sub = null)
    {
        var subHtml = sub is { Length: > 0 } ? $"<div class=\"stat-sub\">{Html(sub)}</div>" : string.Empty;
        return $"<div class=\"stat-card\"><div class=\"stat-number\">{Html(number)}</div><div class=\"stat-label\">{Html(label)}</div>{subHtml}</div>";
    }

    public static string ProgressBar(string label, int value, int max, string? rightLabel = null)
    {
        var pct = max <= 0 ? 0 : Math.Clamp((double)value / max * 100, 0, 100);
        var cls = pct >= 100 && max > 0 ? "done" : pct > 0 ? "partial" : "empty";
        var right = rightLabel ?? $"{value} / {max}";

        return $"""
            <div class="progress-row">
              <div class="progress-label">{Html(label)}</div>
              <div class="progress-bar"><div class="progress-fill {cls}" style="width:{F(pct)}%"></div></div>
              <div class="progress-value">{Html(right)}</div>
            </div>

            """;
    }

    /// <summary>A donut chart from labeled segments; each segment's CSS class picks its color
    /// (e.g. "done"/"pending") via .donut-seg.done { stroke: ... } rules in docsforge.css.</summary>
    public static string Donut(IReadOnlyList<(string Label, int Value, string CssClass)> segments, int size = 120)
    {
        var total = segments.Sum(s => Math.Max(0, s.Value));
        var radius = size / 2.0 - 10;
        var circumference = 2 * Math.PI * radius;
        var center = size / 2.0;

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"donut\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\">\n");
        sb.Append($"  <circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" class=\"donut-track\" />\n");

        var offset = 0.0;
        if (total > 0)
        {
            foreach (var (label, value, cssClass) in segments)
            {
                if (value <= 0) continue;
                var fraction = (double)value / total;
                var dash = fraction * circumference;
                var gap = circumference - dash;
                sb.Append(
                    $"  <circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" class=\"donut-seg {cssClass}\" " +
                    $"stroke-dasharray=\"{F(dash)} {F(gap)}\" stroke-dashoffset=\"-{F(offset)}\">" +
                    $"<title>{Html(label)}: {value}</title></circle>\n");
                offset += dash;
            }
        }

        sb.Append($"  <text x=\"{F(center)}\" y=\"{F(center)}\" class=\"donut-center-text\" text-anchor=\"middle\" dominant-baseline=\"central\">{total}</text>\n");
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
    /// story has a task plan. Every epic/story segment is a real link; colors come from
    /// <see cref="StatusStyles"/> via .sb-* CSS classes. Pure SVG — no JS.</summary>
    public static string Sunburst(EpicsModel model, int size = 380)
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

            sb.Append($"  <a href=\"epics/epic-{epic.Number}.html\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{epicClass}\" d=\"{AnnularSector(c, epicInner, epicOuter, angle + pad, angle + sweep - pad)}\">");
            sb.Append($"<title>Epic {epic.Number}: {Html(epicTitle)} — {Html(StatusStyles.EpicLabel(epicClass))}, {epic.Stories.Count} stories</title></path>\n");
            sb.Append("  </a>\n");

            if (epic.Stories.Count > 0)
            {
                var storySweep = sweep / epic.Stories.Count;
                var storyAngle = angle;
                foreach (var story in epic.Stories)
                {
                    var storyClass = StatusStyles.ForStory(story);
                    var storyHref = story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html";
                    var storyTitle = PathUtil.StripHtmlTags(story.Title);
                    var statusNote = story.Status is { Length: > 0 } s ? $" — {s}" : string.Empty;

                    sb.Append($"  <a href=\"{Html(storyHref)}\">\n");
                    sb.Append($"    <path class=\"sb-seg sb-{storyClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, storyAngle + pad, storyAngle + storySweep - pad)}\">");
                    sb.Append($"<title>Story {story.Id}: {Html(storyTitle)}{Html(statusNote)}</title></path>\n  </a>\n");

                    // Task ring: split the story's span by done/remaining, only when tasks exist.
                    if (story.TasksTotal > 0)
                    {
                        var doneSweep = (storySweep - 2 * pad) * story.TasksDone / story.TasksTotal;
                        if (story.TasksDone > 0)
                        {
                            sb.Append($"  <a href=\"{Html(storyHref)}\"><path class=\"sb-seg sb-done\" d=\"{AnnularSector(c, taskInner, taskOuter, storyAngle + pad, storyAngle + pad + doneSweep)}\">");
                            sb.Append($"<title>Story {story.Id}: {story.TasksDone} of {story.TasksTotal} tasks done</title></path></a>\n");
                        }
                        if (story.TasksDone < story.TasksTotal)
                        {
                            sb.Append($"  <a href=\"{Html(storyHref)}\"><path class=\"sb-seg sb-pending\" d=\"{AnnularSector(c, taskInner, taskOuter, storyAngle + pad + doneSweep, storyAngle + storySweep - pad)}\">");
                            sb.Append($"<title>Story {story.Id}: {story.TasksTotal - story.TasksDone} tasks remaining</title></path></a>\n");
                        }
                    }

                    storyAngle += storySweep;
                }
            }

            angle += sweep;
        }

        var storiesTotal = epics.Sum(e => e.Stories.Count);
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{storiesTotal}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">stories</text>\n");
        sb.Append("</svg>\n");

        sb.Append("""
            <div class="sunburst-legend">
              <span><span class="swatch sb-pending-sw"></span>Pending</span>
              <span><span class="swatch sb-drafted-sw"></span>Drafted</span>
              <span><span class="swatch sb-ready-sw"></span>Ready for dev</span>
              <span><span class="swatch sb-active-sw"></span>In development</span>
              <span><span class="swatch sb-review-sw"></span>In review</span>
              <span><span class="swatch sb-done-sw"></span>Done</span>
            </div>
            <div class="sunburst-hint">Inner ring: epics &middot; middle: stories &middot; outer: task completion. Click any segment to open it.</div>

            """);
        return sb.ToString();
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
    /// in-page anchor down to its story card (supplied via <paramref name="hrefBuilder"/>).</summary>
    public static string EpicSunburst(EpicInfo epic, Func<StoryInfo, string> hrefBuilder, int size = 320)
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

        var totalTasks = epic.Stories.Sum(s => s.TasksTotal);
        var doneTasks = epic.Stories.Sum(s => s.TasksDone);

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"Epic story breakdown\">\n");

        var angle = -Math.PI / 2;
        foreach (var story in epic.Stories)
        {
            var storyClass = StatusStyles.ForStory(story);
            var href = hrefBuilder(story);
            var storyTitle = PathUtil.StripHtmlTags(story.Title);
            var statusNote = story.Status is { Length: > 0 } s ? $" — {s}" : string.Empty;

            sb.Append($"  <a href=\"{Html(href)}\">\n");
            sb.Append($"    <path class=\"sb-seg sb-{storyClass}\" d=\"{AnnularSector(c, storyInner, storyOuter, angle + pad, angle + anglePerStory - pad)}\">");
            sb.Append($"<title>Story {story.Id}: {Html(storyTitle)}{Html(statusNote)}</title></path>\n  </a>\n");

            if (story.TasksTotal > 0)
            {
                var doneSweep = (anglePerStory - 2 * pad) * story.TasksDone / story.TasksTotal;
                if (story.TasksDone > 0)
                {
                    sb.Append($"  <a href=\"{Html(href)}\"><path class=\"sb-seg sb-done\" d=\"{AnnularSector(c, taskInner, taskOuter, angle + pad, angle + pad + doneSweep)}\">");
                    sb.Append($"<title>Story {story.Id}: {story.TasksDone} of {story.TasksTotal} tasks done</title></path></a>\n");
                }
                if (story.TasksDone < story.TasksTotal)
                {
                    sb.Append($"  <a href=\"{Html(href)}\"><path class=\"sb-seg sb-pending\" d=\"{AnnularSector(c, taskInner, taskOuter, angle + pad + doneSweep, angle + anglePerStory - pad)}\">");
                    sb.Append($"<title>Story {story.Id}: {story.TasksTotal - story.TasksDone} tasks remaining</title></path></a>\n");
                }
            }

            angle += anglePerStory;
        }

        var centerText = totalTasks > 0 ? $"{doneTasks}/{totalTasks}" : count.ToString();
        var centerLabel = totalTasks > 0 ? "tasks" : "stories";
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{Html(centerText)}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">{Html(centerLabel)}</text>\n");
        sb.Append("</svg>\n");

        sb.Append("""
            <div class="sunburst-legend">
              <span><span class="swatch sb-pending-sw"></span>Pending</span>
              <span><span class="swatch sb-drafted-sw"></span>Drafted</span>
              <span><span class="swatch sb-ready-sw"></span>Ready for dev</span>
              <span><span class="swatch sb-active-sw"></span>In development</span>
              <span><span class="swatch sb-review-sw"></span>In review</span>
              <span><span class="swatch sb-done-sw"></span>Done</span>
            </div>
            <div class="sunburst-hint">Inner ring: stories &middot; outer: task completion. Click any segment to open it.</div>

            """);
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

        var totalCheckboxes = tasks.Sum(t => 1 + t.Subtasks.Count);
        var doneCheckboxes = tasks.Sum(t => (t.Done ? 1 : 0) + t.Subtasks.Count(s => s.Done));

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"sunburst\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"Task breakdown\">\n");

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

        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c - 8)}\" class=\"sunburst-center-num\" text-anchor=\"middle\">{doneCheckboxes}/{totalCheckboxes}</text>\n");
        sb.Append($"  <text x=\"{F(c)}\" y=\"{F(c + 12)}\" class=\"sunburst-center-label\" text-anchor=\"middle\">tasks</text>\n");
        sb.Append("</svg>\n");

        sb.Append("""
            <div class="sunburst-legend">
              <span><span class="swatch sb-pending-sw"></span>Not done</span>
              <span><span class="swatch sb-done-sw"></span>Done</span>
            </div>
            <div class="sunburst-hint">Inner ring: tasks &middot; outer ring: subtasks. Hover a segment for details.</div>

            """);
        return sb.ToString();
    }

    /// <summary>A grid of clickable per-epic mini-donuts — story-detail coverage at a glance, with the
    /// epic's full name always visible (no more decoding "e07" abbreviations) and a direct link to the
    /// epic page. Pending epics (no stories yet) show an empty ring rather than a misleading 0% fill.</summary>
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
                ? Donut(new (string, int, string)[]
                    {
                        // "Detailed" = has a task plan (ready), not finished — gold, never green.
                        ("Detailed", epic.StoriesWithArtifact, "ready"),
                        ("Not yet detailed", epic.StoryCount - epic.StoriesWithArtifact, "pending"),
                    }, size: 64)
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

    /// <summary>A GitHub-style commit heatmap: one column per week, one row per day-of-week (Sun top to
    /// Sat bottom), shaded by commit count, with month labels along the top and Mon/Wed/Fri labels on the
    /// left. Pads the window to a minimum of ~8 weeks so a young project's grid isn't just a single sliver.</summary>
    public static string CommitHeatmap(IReadOnlyList<(DateOnly Day, int Count)> series)
    {
        if (series.Count == 0) return "<div class=\"chart-empty\">No git history available.</div>";

        var byDay = series.ToDictionary(s => s.Day, s => s.Count);
        var firstCommit = series.Min(s => s.Day);
        var lastCommit = series.Max(s => s.Day);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var end = lastCommit > today ? lastCommit : today;
        var minStart = end.AddDays(-7 * 7);
        var start = firstCommit < minStart ? firstCommit : minStart;

        // Snap to full weeks (Sunday..Saturday) so the grid is rectangular.
        start = start.AddDays(-(int)start.DayOfWeek);
        end = end.AddDays(6 - (int)end.DayOfWeek);

        var totalDays = end.DayNumber - start.DayNumber + 1;
        var weeks = (int)Math.Ceiling(totalDays / 7.0);
        var maxCount = series.Max(s => s.Count);

        const int cell = 11;
        const int gap = 3;
        const int leftGutter = 26;
        const int topGutter = 16;
        var width = leftGutter + weeks * (cell + gap);
        var height = topGutter + 7 * (cell + gap);

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"heatmap\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\">\n");

        var dayLabels = new (int Row, string Label)[] { (1, "Mon"), (3, "Wed"), (5, "Fri") };
        foreach (var (row, label) in dayLabels)
        {
            var y = topGutter + row * (cell + gap) + cell - 2;
            sb.Append($"  <text x=\"0\" y=\"{y}\" class=\"heatmap-daylabel\">{Html(label)}</text>\n");
        }

        string? lastMonth = null;
        for (var w = 0; w < weeks; w++)
        {
            var weekStart = start.AddDays(w * 7);
            var monthName = weekStart.ToString("MMM", CultureInfo.InvariantCulture);
            if (monthName != lastMonth)
            {
                var x = leftGutter + w * (cell + gap);
                sb.Append($"  <text x=\"{x}\" y=\"{topGutter - 5}\" class=\"heatmap-monthlabel\">{Html(monthName)}</text>\n");
                lastMonth = monthName;
            }
        }

        for (var w = 0; w < weeks; w++)
        {
            for (var d = 0; d < 7; d++)
            {
                var day = start.AddDays(w * 7 + d);
                if (day > end) continue;

                var count = byDay.GetValueOrDefault(day, 0);
                var level = HeatLevel(count, maxCount);
                var x = leftGutter + w * (cell + gap);
                var y = topGutter + d * (cell + gap);

                sb.Append($"  <rect x=\"{x}\" y=\"{y}\" width=\"{cell}\" height=\"{cell}\" rx=\"2\" class=\"heatmap-cell level-{level}\">");
                sb.Append($"<title>{day:yyyy-MM-dd}: {count} commit{(count == 1 ? string.Empty : "s")}</title></rect>\n");
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

    private static int HeatLevel(int count, int maxCount)
    {
        if (count <= 0) return 0;
        if (maxCount <= 1) return 4;
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

    private static string Html(string s) => PathUtil.Html(s);
}
