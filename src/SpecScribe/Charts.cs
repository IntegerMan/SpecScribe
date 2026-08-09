using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Pure inline SVG + CSS chart builders — no JS, no external dependencies, themed entirely via
/// the CSS variables already defined in specscribe.css. Every builder degrades gracefully at zero/low data
/// (a hallmark of a project that's just getting started).</summary>
public static partial class Charts
{
    /// <summary>Metric keys for <see cref="WhyText"/> — the ONE shared source of framing sentences so a new
    /// chart inherits the standard by construction rather than re-typing copy (Story 10.2 AC2).</summary>
    public enum ChartMetric
    {
        /// <summary>Commit-activity cadence (heatmap / activity window).</summary>
        ActivityCadence,
        /// <summary>File churn / hotspots (defect-risk framing).</summary>
        FileChurn,
        /// <summary>Change coupling between files (hidden-dependency framing).</summary>
        ChangeCoupling,
        /// <summary>Refactor-target risk: files that are both large and frequently changed (Story 7.10).</summary>
        RefactorRisk,
        /// <summary>Code ownership / bus-factor: how concentrated authorship is across the codebase — dominant-author
        /// share, top contributors, an individual-author spotlight, and staleness (Story 7.11).</summary>
        CodeOwnership,
        /// <summary>Requirement-to-epic traceability: which requirements have a delivering epic, which are
        /// deliberately deferred, and which are gaps (Story 21.1).</summary>
        RequirementTraceability,
        /// <summary>Delivery cadence: how often stories reach done over time, plus (where derivable) story
        /// cycle-time. Distinct from <see cref="ActivityCadence"/> — that's raw commit activity; this is
        /// story-completion rhythm (Story 21.2).</summary>
        DeliveryCadence,
        /// <summary>Planning ↔ code impact: which code areas an epic's/story's commits actually touched, correlated
        /// best-effort from commit-message and merge-branch naming (Story 21.3).</summary>
        PlanningCodeImpact,
        /// <summary>Work hierarchy: the shape of the plan itself — epics, their stories, and the follow-up work
        /// hanging off both, each sized by how much work it represents. The Hierarchy Explorer's framing, so the
        /// component inherits the Story 10.2 standard by construction instead of a call site typing a sentence
        /// (Story 20.5).</summary>
        WorkHierarchy,
    }

    /// <summary>The standard metadata every framed chart carries. Slots are optional so a chart uses only what
    /// applies (a status donut has no time window; a heatmap has no ranking caption). <paramref name="Note"/> is
    /// a caveat about the data itself (e.g. "some pairs are process-coupling, not a code dependency") — distinct
    /// from <paramref name="Why"/>'s generic "why this metric matters" framing. [Story 10.2; Note: Story 10.6]</summary>
    public sealed record ChartMeta(
        string Title,
        string? Window = null,
        string? Ranking = null,
        string? Why = null,
        string? Note = null);

    /// <summary>ONE shared source of metric-generic framing sentences (NFR8 — never project-specific). Callers
    /// reference these via <see cref="WhyText"/>; after Story 10.2 there must be no second hand-rolled
    /// "why this matters" copy at call sites. [Story 10.2 AC2]</summary>
    public static string WhyText(ChartMetric metric) => metric switch
    {
        ChartMetric.ActivityCadence =>
            "Commit activity over time shows where work concentrated — busy and quiet stretches are both signals.",
        ChartMetric.FileChurn =>
            "Files that change most often are where defects tend to cluster.",
        ChartMetric.ChangeCoupling =>
            "Files that change together often may hide a dependency worth a second look.",
        ChartMetric.RefactorRisk =>
            "Files that are both large and frequently changed are the costliest place for a defect to hide — refactoring them tends to pay off fastest.",
        ChartMetric.CodeOwnership =>
            "Files with a single dominant author are a knowledge-silo risk if that person leaves or moves on.",
        ChartMetric.RequirementTraceability =>
            "A requirement with no delivering epic is a coverage gap; one that is deferred is a deliberate choice — the two look different so neither hides.",
        ChartMetric.DeliveryCadence =>
            "How often stories reach done reveals the project's real delivery rhythm — steady drips and bursts both tell you something commit activity alone doesn't.",
        ChartMetric.PlanningCodeImpact =>
            "Seeing which code areas an epic's commits actually touched turns “what did this work change” from a guess into a fact — even a best-effort one.",
        ChartMetric.WorkHierarchy =>
            "Sizing each epic and story by the work it actually holds shows where the plan’s weight really sits — which is rarely where the story count suggests.",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null),
    };

    /// <summary>The impact map's provenance caveat (Story 21.3): the correlation is mined best-effort from commit
    /// and merge-branch naming that already exists in the repo — never a tracked or authoritative mapping, and the
    /// merge backfill is a linear approximation of branch membership, not exact ancestry. Pattern-generic (NFR8):
    /// names no specific repo's branches or commits. Rendered in the frame's <see cref="ChartMeta.Note"/> slot.</summary>
    public const string PlanningCodeImpactNote =
        "This is a best-effort correlation mined from commit messages and merge-branch names — not a tracked or " +
        "authoritative mapping. Commits that don’t name a story or epic are left out, and merge attribution " +
        "is approximate (a linear window of a branch’s commits, not exact ancestry).";

    /// <summary>The dedicated impact-map page body (Story 21.3): one epic-grouped section per epic that has at
    /// least one linkable touched file, each a plain semantic list of code-page links — a list, never a chart the
    /// data can't support. Epics render in roster order; files arrive already ordinal-sorted + link-gated from
    /// <see cref="PlanningCodeImpact"/> so nothing here can emit a dead link. <paramref name="prefix"/> is the
    /// consuming page's relative prefix ("" for the root <c>impact-map.html</c>), applied to both the epic-page
    /// and code-page hrefs. Degrades to the shared <c>chart-empty</c> note when nothing correlated (AC #2 — an
    /// honest empty state, never a misleading empty grid). Wrap in <see cref="Framed"/> with the
    /// <see cref="PlanningCodeImpactNote"/> caveat.</summary>
    public static string ImpactMapBody(EpicsModel epics, PlanningCodeImpactData data, string prefix)
    {
        if (!data.HasAnyFiles)
        {
            return "<div class=\"chart-empty\">No commits could be correlated to a story or epic yet.</div>\n";
        }

        var sb = new StringBuilder();
        foreach (var epic in epics.Epics.OrderBy(e => e.Number))
        {
            if (!data.FilesByEpic.TryGetValue(epic.Number, out var files) || files.Count == 0) continue;

            var epicHref = prefix + $"epics/epic-{epic.Number}.html";
            sb.Append("<section class=\"impact-epic\">\n");
            // epic.Title is already-projected inline HTML (rendered raw, as the epic chips do).
            sb.Append($"  <h4 class=\"impact-epic-head\"><a href=\"{Html(epicHref)}\">Epic {epic.Number} &middot; {epic.Title}</a></h4>\n");
            sb.Append($"  <p class=\"chart-lead\">{files.Count.ToString("N0", CultureInfo.InvariantCulture)} {Plural(files.Count, "code file", "code files")} touched.</p>\n");
            sb.Append("  <ul class=\"impact-file-list\">\n");
            // Ranked by churn (most-changed first) so the text-equivalent leads with the same signal the treemap's
            // tile size conveys; the churn/commit meta makes the weights legible without the visual.
            foreach (var f in files.OrderByDescending(x => x.Churn).ThenBy(x => x.Path, StringComparer.Ordinal))
            {
                var link = f.CodePageHref is { Length: > 0 } h
                    ? $"<a href=\"{Html(prefix + h)}\">{Html(f.Path)}</a>"
                    : Html(f.Path);
                var meta = $"<span class=\"impact-file-meta\">{f.Churn.ToString("N0", CultureInfo.InvariantCulture)} {Plural(f.Churn, "line", "lines")} &middot; {f.Commits.ToString("N0", CultureInfo.InvariantCulture)} {Plural(f.Commits, "commit", "commits")}</span>";
                sb.Append($"    <li>{link} {meta}</li>\n");
            }
            sb.Append("  </ul>\n");
            sb.Append("</section>\n");
        }
        return sb.ToString();
    }

    /// <summary>The change-coupling panel's process-vs-code explanatory note (Story 10.6, AC1): shown once,
    /// only when at least one coupled pair classifies as <see cref="GitMetrics.CouplingKind.Process"/>, so a
    /// project with purely code-to-code coupling never sees copy about a case that doesn't apply to it.
    /// Pattern/extension-generic (NFR8) — never names a specific repo's config or stylesheet file.</summary>
    public const string ProcessCouplingNote =
        "Pairs marked “Process” involve config, lockfile, build-output, or stylesheet files, which often " +
        "change together as routine upkeep rather than a real code dependency.";

    /// <summary>Window slot markup — the ONE place a numeric analysis window is rendered. Empty/null → omit.
    /// [Story 10.2]</summary>
    /// <param name="hiddenUntilMount">Emits the span <c>hidden</c> so a client can reveal it on mount. Added by
    /// Story 20.10's review for the one case where the server CANNOT bake an honest value: a shared-payload
    /// hierarchy instance whose window is a per-VIEW fact, resolved only once the component picks the active view.
    /// A stale window is worse than none — it read "every file · 1,220 files" above a table the pure-CSS filter had
    /// already cut to 461 rows. Defaults to false, so every other caller's bytes are unchanged.</param>
    public static string FrameWindowSlot(string? window, bool hiddenUntilMount = false) =>
        string.IsNullOrEmpty(window)
            ? string.Empty
            : $"<span class=\"chart-frame-window\"{(hiddenUntilMount ? " hidden" : string.Empty)}>{Html(window)}</span>";

    /// <summary>Ranking slot markup — the ONE place a ranked-list metric caption is rendered. [Story 10.2]</summary>
    public static string FrameRankingSlot(string? ranking) =>
        string.IsNullOrEmpty(ranking) ? string.Empty : $"<p class=\"chart-frame-ranking\">{Html(ranking)}</p>\n";

    /// <summary>Why-it-matters slot markup — the ONE place a framing sentence is rendered. [Story 10.2]</summary>
    public static string FrameWhySlot(string? why) =>
        string.IsNullOrEmpty(why) ? string.Empty : $"<p class=\"chart-frame-why\">{Html(why)}</p>\n";

    /// <summary>Note slot markup — the ONE place a panel-level data caveat is rendered (e.g. process-vs-code
    /// coupling), distinct from the generic <see cref="FrameWhySlot"/> framing. Empty/null → omit. [Story 10.6]</summary>
    public static string FrameNoteSlot(string? note) =>
        string.IsNullOrEmpty(note) ? string.Empty : $"<p class=\"chart-frame-note\">{Html(note)}</p>\n";

    /// <summary>Wraps a chart body in the standard panel scaffold so title/window/ranking/note/why are
    /// metadata-consistent by construction. Optional slots omit their element entirely when null/empty. [Story
    /// 10.2 AC2; Note slot: Story 10.6]</summary>
    /// <param name="panelAttributes">Extra attributes for the panel element, already serialized and starting with
    /// a space (e.g. <c> data-explorer</c>). Added for Story 20.5: the Hierarchy Explorer's framed panel IS the
    /// dashboard's chart panel, and that panel carries the opt-in hooks the client blocks key on — so the
    /// alternative was a call site hand-writing the frame, which is the thing this helper exists to prevent.
    /// Defaults to nothing, so every existing caller's bytes are unchanged.</param>
    /// <param name="windowHiddenUntilMount">Forwarded to <see cref="FrameWindowSlot"/> — see its remarks. Only a
    /// shared-payload hierarchy instance sets it; defaults to false so no existing caller's bytes move.</param>
    public static string Framed(ChartMeta meta, string body, string panelClass = "chart-panel", string panelAttributes = "", bool windowHiddenUntilMount = false)
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"{Html(panelClass)}\"{panelAttributes}>\n");
        sb.Append("  <div class=\"chart-frame-head\">\n");
        sb.Append($"    <h3>{Html(meta.Title)}</h3>\n");
        var window = FrameWindowSlot(meta.Window, windowHiddenUntilMount);
        if (window.Length > 0) sb.Append($"    {window}\n");
        sb.Append("  </div>\n");
        sb.Append(FrameRankingSlot(meta.Ranking));
        sb.Append(FrameNoteSlot(meta.Note));
        sb.Append(body);
        sb.Append(FrameWhySlot(meta.Why));
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>Inclusive count-range label for a heatmap legend swatch, derived from the SAME thresholds
    /// <see cref="HeatLevel"/> uses — so cell shade and legend text can never disagree. [Story 10.2]</summary>
    public static string HeatLevelRange(int level, int maxCount)
    {
        if (level is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(level), level, "Heat level must be 0..4.");
        if (level == 0) return "0";
        if (maxCount <= 0)
        {
            // No visible cell carries any count above zero (e.g. every commit is future-dated and suppressed
            // from the grid) — level-1 can never render either, so it must degrade like the other unused levels
            // rather than claiming "1" for a bucket no cell will ever show. [Story 10.2 review]
            return "—";
        }
        if (maxCount == 1)
        {
            // Uniform/sparse history: HeatLevel only ever paints nonzero cells as level-1 — levels 2–4 are unused.
            return level == 1 ? "1" : "\u2014";
        }

        var (t1, t2, t3) = HeatThresholds(maxCount);
        return level switch
        {
            1 => FormatHeatRange(1, t1),
            2 => FormatHeatRange(t1 + 1, t2),
            3 => FormatHeatRange(t2 + 1, t3),
            4 => FormatHeatRange(t3 + 1, maxCount, openEnded: true),
            _ => "0",
        };
    }

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

    /// <summary>Per-epic middle-ring density collapse (AC1): epics with this many stories or more render one
    /// summary wedge instead of one per story, so a crowded epic never turns the middle ring into unhittable
    /// slices. [Story 10.7]</summary>
    public const int StoryDensityCollapseThreshold = 8;

    // The project-glance ring-radius factors (SbEpicInnerF … SbStartAngle) and `SunburstGlanceSize` were DELETED
    // by Story 20.7. They were PRESENTATION GEOMETRY for a hand-rolled SVG — the radii the client re-laid drilled
    // arcs against — and Plotly computes its own. Confirmed dead by search, not assumed: after `Charts.Sunburst`
    // and Story 20.2's island went, the only remaining mention of `SunburstGlanceSize` was a doc comment.
    // Keeping a dead constant is the same error as deleting a live one, in the other direction. [Task 8.2]

    /// <summary>The open/done split of one epic's follow-up aggregate ring — the SINGLE source the SVG builder and
    /// the explorer payload both read. [Story 20.2]</summary>
    internal static (int Open, int Done) SunburstEpicAggregates(
        EpicInfo epic, FollowUpGeometry geometry, UnplannedWorkGeometry unplannedGeo) =>
        CountEpicFollowUpAggregates(epic, geometry, unplannedGeo);

    /// <summary>The open/done split of the unattributed ("orphan") follow-up root. Extracted per Story 20.1's seam
    /// note so the SVG and the explorer payload share the arithmetic rather than each copying it. [Story 20.2 review]</summary>
    internal static (int Open, int Done) SunburstOrphanAggregates(IReadOnlyList<SprintActionItem> unattributed)
    {
        var open = unattributed.Count(a => !FollowUpGeometry.IsDone(a));
        return (open, unattributed.Count - open);
    }

    /// <summary>The open/done split of the unplanned / direct-work root. Extracted alongside
    /// <see cref="SunburstOrphanAggregates"/> for the same reason. [Story 20.2 review]</summary>
    internal static (int Open, int Done) SunburstUnplannedAggregates(UnplannedWorkGeometry unplannedGeo)
    {
        var slots = unplannedGeo.SunburstUnplannedWeight;
        var open = unplannedGeo.UnplannedQuickDev.Count(q => UnplannedWorkGeometry.IsOpenQuickDev(q.Entry.Status))
            + unplannedGeo.UnattributableDeferred.Count(s => !s.Item.Resolved);
        return (open, Math.Max(0, slots - open));
    }

    /// <summary>The project-glance middle-ring weight of one story: tasks + nested story-child deferred. A story with
    /// no plan yet (zero tasks, zero nested deferred) has an <em>unknown</em> true size, so rather than collapsing to a
    /// hairline 1-unit sliver that reads as trivial it takes <paramref name="noPlanWeight"/> — the average weight of the
    /// drafted stories (see <see cref="SunburstNoPlanStoryWeight"/>) — so un-drafted work renders at a typical size.
    /// Drafted stories always keep their honest weight (this only lifts the floor, never shrinks a real wedge).
    /// <paramref name="noPlanWeight"/> defaults to 1, which reproduces the historical <c>Math.Max(1, …)</c> floor for
    /// callers that don't opt in. Extracted from the <see cref="Sunburst"/> local closure (per Story 20.1's contract) so
    /// the SVG builder and the Story 20.2 explorer payload emit the SAME number — no second geometry.
    /// [Story 20.2; no-plan average bump per owner 2026-07-24]</summary>
    internal static int SunburstStoryWeight(FollowUpGeometry geometry, int epicNumber, StoryInfo story, int noPlanWeight = 1)
    {
        var raw = story.TasksTotal + geometry.StoryChildDeferred(epicNumber, story.Id).Count;
        return raw > 0 ? raw : Math.Max(1, noPlanWeight);
    }

    /// <summary>The weight given to a "no plan yet" story wedge on the project glance: the rounded <em>mean</em> raw
    /// weight (tasks + nested story-child deferred) of the DRAFTED stories across the whole model — so an un-drafted
    /// story renders at a typical size instead of a hairline sliver that reads as trivial. Falls back to 1 when no
    /// story has a plan yet (nothing to average against), which keeps a brand-new project's glance byte-identical to
    /// the old floor. Mean (not median) matches the owner's "bump to average" choice. Computed once per chart and
    /// threaded to <see cref="SunburstStoryWeight"/>/<see cref="SunburstEpicWeight"/> so the SVG and the Story 20.2
    /// explorer payload share the exact number. [owner 2026-07-24]</summary>
    internal static int SunburstNoPlanStoryWeight(EpicsModel model, FollowUpGeometry geometry)
    {
        var drafted = model.Epics
            .SelectMany(e => e.Stories.Select(s => s.TasksTotal + geometry.StoryChildDeferred(e.Number, s.Id).Count))
            .Where(raw => raw > 0)
            .ToList();
        return drafted.Count == 0 ? 1 : Math.Max(1, (int)Math.Round(drafted.Average(), MidpointRounding.AwayFromZero));
    }

    /// <summary>The project-glance inner-ring weight of one epic: its story weights plus epic-level follow-up
    /// peers (actions / epic-level deferred / attributed quick-dev), min 1. Extracted from the
    /// <see cref="Sunburst"/> local closure so the SVG builder and the explorer payload agree. <paramref name="noPlanWeight"/>
    /// threads the same no-plan average through the story-weight sum so a plan-less epic reads as typically sized.
    /// [Story 20.2]</summary>
    internal static int SunburstEpicWeight(FollowUpGeometry geometry, UnplannedWorkGeometry unplannedGeo, EpicInfo epic, int noPlanWeight = 1) =>
        Math.Max(1,
            epic.Stories.Sum(s => SunburstStoryWeight(geometry, epic.Number, s, noPlanWeight))
            + geometry.ForEpicNumber(epic.Number).Count
            + geometry.EpicLevelDeferred(epic.Number, epic.Stories.Select(s => s.Id)).Count
            + unplannedGeo.ForEpic(epic.Number).Count);

    // `Charts.Sunburst` — the project-glance SVG — was DELETED by Story 20.7. The dashboard and the epics index
    // now render through HierarchyExplorer.ProjectDashboard + HierarchyExplorer.Render. Its weight helpers
    // (SunburstEpicWeight / SunburstStoryWeight / SunburstNoPlanStoryWeight) SURVIVE and are the projector's
    // inputs — SunburstNoPlanStoryWeight in particular carries Story 20.5 AC#4's owner-directed no-plan average
    // bump, so deleting it as "part of the sunburst" would silently undo an owner decision. [Story 20.7 Task 8.1]

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

    /// <summary>Companion scannable tile grid for the project-glance sunburst (AC1): one clickable tile per
    /// epic that still has remaining work (plus Follow-ups / Unplanned roots when non-empty) — a small
    /// responsive grid with a left status accent bar AND a visible status-label span (reused
    /// <c>--status-*</c>/chart-local tokens; never color-only — the label text always restates the same
    /// status the accent conveys) — so a reader is never limited to hitting shrinking SVG wedges. A fully
    /// done epic with zero open follow-ups has nothing left to report here (NFR8) — it's still reachable via
    /// the sunburst's own inner-ring wedge, the Epic Status tile, and the Progress-by-Epic panel, so omitting
    /// it isn't a lost destination, just a decluttered "what's left" list. Same destinations as the chart
    /// (epic page / generated group page); counts reuse the existing aggregate helpers, never a second ledger
    /// parse. Returns just the grid (no panel wrapper or heading — callers own those, exactly like
    /// <see cref="Sunburst"/>) so the Dashboard and Epics-index glance panels render this identically. Returns
    /// "" when there is nothing left to show at all.</summary>
    public static string SunburstCompanionList(
        EpicsModel model,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        var epics = model.Epics.OrderBy(e => e.Number).ToList();
        if (epics.Count == 0) return string.Empty;

        var geometry = followUps ?? FollowUpGeometry.Empty;
        var unplannedGeo = unplanned ?? UnplannedWorkGeometry.Empty;
        var knownEpics = epics.Select(e => e.Number).ToHashSet();

        var sb = new StringBuilder();
        var tileCount = 0;

        foreach (var epic in epics)
        {
            var (openCount, _) = CountEpicFollowUpAggregates(epic, geometry, unplannedGeo);
            var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
            // Nothing remaining to report: every story done, retro closed, no open follow-ups (NFR8).
            if (epicClass == "done" && openCount == 0) continue;

            tileCount++;
            var epicTitle = PathUtil.StripHtmlTags(epic.Title);
            var statusLabel = StatusStyles.EpicLabel(epicClass);
            var storyNote = $"{epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}";
            var followNote = openCount > 0
                ? $"{openCount} open {Plural(openCount, "follow-up", "follow-ups")}"
                : string.Empty;
            var aria = $"Epic {epic.Number}: {epicTitle} — {statusLabel}, {storyNote}" + (followNote.Length > 0 ? $", {followNote}" : string.Empty);
            sb.Append($"  <a class=\"epic-remaining-tile epic-remaining-{epicClass}\" href=\"epics/epic-{epic.Number}.html\" aria-label=\"{Html(aria)}\">\n");
            sb.Append($"    <span class=\"epic-remaining-num\">Epic {epic.Number}</span>\n");
            sb.Append($"    <span class=\"epic-remaining-title\">{Html(epicTitle)}</span>\n");
            sb.Append($"    <span class=\"epic-remaining-status\">{Html(statusLabel)}</span>\n");
            sb.Append($"    <span class=\"epic-remaining-count\">{Html(storyNote)}</span>\n");
            if (followNote.Length > 0)
                sb.Append($"    <span class=\"epic-remaining-count epic-remaining-followups\">{Html(followNote)}</span>\n");
            sb.Append("  </a>\n");
        }

        var unattributed = geometry.OrphanActionItems(knownEpics);
        if (unattributed.Count > 0)
        {
            tileCount++;
            var open = unattributed.Count(a => !FollowUpGeometry.IsDone(a));
            var itemNote = $"{unattributed.Count} unattributed {Plural(unattributed.Count, "item", "items")}";
            var aria = itemNote + (open > 0 ? $", {open} open" : string.Empty);
            sb.Append($"  <a class=\"epic-remaining-tile epic-remaining-followup-open\" href=\"{Html(geometry.FollowUpsGroupHref)}\" aria-label=\"{Html(aria)}\">\n");
            sb.Append("    <span class=\"epic-remaining-num\">Follow-ups</span>\n");
            sb.Append("    <span class=\"epic-remaining-title\">Unattributed items</span>\n");
            sb.Append($"    <span class=\"epic-remaining-count\">{Html(itemNote)}</span>\n");
            sb.Append("  </a>\n");
        }

        if (unplannedGeo.SunburstUnplannedWeight > 0 && unplannedGeo.GroupRootHref is { Length: > 0 } rootHref)
        {
            tileCount++;
            var count = unplannedGeo.SunburstUnplannedWeight;
            var countNote = $"{count} open {Plural(count, "item", "items")}";
            sb.Append($"  <a class=\"epic-remaining-tile epic-remaining-unplanned\" href=\"{Html(rootHref)}\" aria-label=\"Unplanned: {Html(countNote)}\">\n");
            sb.Append("    <span class=\"epic-remaining-num\">Unplanned</span>\n");
            sb.Append("    <span class=\"epic-remaining-title\">Direct / one-off work</span>\n");
            sb.Append($"    <span class=\"epic-remaining-count\">{Html(countNote)}</span>\n");
            sb.Append("  </a>\n");
        }

        if (tileCount == 0) return string.Empty;

        return "<div class=\"epic-remaining-grid\">\n" + sb + "</div>\n";
    }

    /// <summary>The shared sunburst legend — one focusable entry per status, each carrying a status class
    /// (<c>sb-&lt;status&gt;-item</c>) and <c>tabindex="0"</c> so the pure-CSS interactive-legend emphasis
    /// (specscribe.css: <c>.sunburst-panel:has(.sb-&lt;status&gt;-item:hover/:focus) …</c>) is reachable by
    /// both pointer and keyboard. The always-visible swatch + label keep status readable without the emphasis
    /// affordance (never color-only). [Story 3.5 Task 3, UXO C3]</summary>
    internal static string SunburstLegend(params (string Status, string Label)[] items)
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

    /// <summary>The reader-facing name of one deferred item, wherever it is drawn or listed. Extracted from
    /// <see cref="AppendDeferredItemSlot"/> unchanged so the Story 20.7 projectors label a deferred node with the
    /// SAME string the SVG's wedge title used — a second phrasing here would put two names on one item across the
    /// chart, its tooltip, its accessible name and the text twin. [Story 20.7 Task 5.2]</summary>
    internal static string DeferredSlotLabel(FollowUpDeferredSlot slot)
    {
        var text = TruncateFollowUpText(
            PathUtil.StripHtmlTags(FollowUpRow.SummarizeFromHtml(slot.Item.BodyHtml)));
        if (string.IsNullOrWhiteSpace(text)) text = "(no deferred text)";
        var from = DeferredSourceSuffix(slot);
        return slot.Item.Resolved
            ? $"Deferred item (resolved): {text}{from}"
            : $"Deferred item: {text}{from}";
    }

    /// <summary>Names the story or quick-dev this deferred item stemmed from (code-review provenance),
    /// so residual work stays readable as a child of its source — never a free-floating "Story". [Story 9.12]</summary>
    private static string DeferredSourceSuffix(FollowUpDeferredSlot slot)
    {
        var key = FollowUpGeometry.NormalizeSourceKey(slot.SourceKey);
        if (key.Length == 0) return string.Empty;

        var storyId = FollowUpRefs.StoryIdFromKey(key);
        if (storyId is not null)
            return $" (from Story {storyId})";
        if (key.StartsWith("spec-", StringComparison.OrdinalIgnoreCase))
            return $" (from Direct change: {key})";
        return $" (from {key})";
    }

    /// <summary>Pad inset that never exceeds half the sweep (avoids inverted annular sectors on tiny wedges).</summary>
    private static double InsetStart(double angle, double sweep, double pad) =>
        angle + Math.Min(pad, Math.Max(0, sweep) / 2);

    private static double InsetEnd(double angle, double sweep, double pad) =>
        angle + sweep - Math.Min(pad, Math.Max(0, sweep) / 2);

    /// <summary>Prose label for the four CHART-LOCAL sunburst statuses — the ones with no lifecycle
    /// <c>--status-*</c> token and therefore no <see cref="StatusStyles.EpicLabel"/>/<see cref="StatusStyles.StoryLabel"/>
    /// entry. Returns null for anything else, so a caller falls through to the <see cref="StatusStyles"/> vocabulary
    /// rather than inventing a second phrasing for a lifecycle stage.
    /// <para>This is the ONE source: the sunburst legend below and the Story 20.5 Hierarchy Explorer's accessible
    /// names + text twin all read it. Story 20.3's live-browser round caught exactly this class of drift ("EpicEpic 19"),
    /// so a chart and its own legend disagreeing about what a colour means is a defect, not a nicety. [Story 20.5]</para></summary>
    public static string? SunburstLocalStatusLabel(string cssClass) => cssClass switch
    {
        "noplan" => "No task plan",
        "followup-open" => "Open follow-up",
        "followup-done" => "Done follow-up",
        "unplanned" => "Direct change",
        _ => null,
    };

    /// <summary>Prose status for a TASK or SUBTASK — the story-detail chart's vocabulary, and the wording its own
    /// legend has always used ("Not done" / "Done").
    ///
    /// <para>It is a separate entry point rather than two more cases in <see cref="SunburstLocalStatusLabel"/> on
    /// purpose. Tasks reuse the <c>pending</c>/<c>done</c> COLOUR tokens but they have no lifecycle: a task is
    /// never "Ready for dev" and an undone one is not "Pending", it is simply not done. Teaching the shared map
    /// that <c>pending</c> means "Not done" would have renamed every pending STORY on the dashboard, the epics
    /// index and the epic pages — one datasource's vocabulary silently overwriting another's. This keeps the task
    /// wording in exactly one place without touching anyone else's. [Story 20.7 Task 6.2]</para></summary>
    public static string TaskStatusLabel(TaskState state) => state switch
    {
        TaskState.Done => "Done",
        TaskState.NotDone => "Not done",
        TaskState.Unmarked => "Planned step",
        _ => "Task state unknown",
    };

    public static string TaskStatusLabel(bool done) => TaskStatusLabel(done ? TaskState.Done : TaskState.NotDone);

    /// <summary>The legend item set for a Hierarchy Explorer payload — Story 20.7 Task 2.1's "expose what
    /// <see cref="BuildSunburstLegendItems"/> computes rather than duplicating it".
    ///
    /// <para><b>Order and roster</b> come from <see cref="BuildSunburstLegendItems"/> with every optional family
    /// switched on, so the component cannot invent an ordering the SVG's legend did not have. <b>Membership</b> is
    /// the payload's, not a flag's: a status no sector carries gets no swatch. That is the stricter rule — the
    /// entry-point legends were built from "does this chart have follow-ups" booleans and could show a row pointing
    /// at zero wedges, which is exactly the phantom-entry defect Stories 10.7 and 21.1 each had to close.
    /// <b>Wording</b> is each node's own already-resolved <see cref="HierarchyNode.StatusLabel"/>, because that is
    /// the one prose source chart, twin, tooltip and legend all read — a task chart says "Not done" where a story
    /// chart says "Pending", and the legend must say whatever the sectors say.</para>
    ///
    /// <para>A status outside the canonical roster keeps its first-drawn position at the end rather than being
    /// dropped: a swatch the chart draws must always be explicable.</para></summary>
    internal static (string Status, string Label)[] SunburstLegendItemsPresent(
        IReadOnlyList<(string Status, string Label)> present)
    {
        if (present.Count == 0) return Array.Empty<(string, string)>();

        var labelOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var firstSeen = new List<string>();
        foreach (var (status, label) in present)
        {
            if (labelOf.ContainsKey(status)) continue;
            labelOf[status] = label;
            firstSeen.Add(status);
        }

        var canonical = BuildSunburstLegendItems(hasFollowUps: true, hasUnplanned: true, hasNoPlan: true)
            .Select(i => i.Status)
            .ToList();

        var ordered = new List<(string, string)>(labelOf.Count);
        foreach (var status in canonical)
            if (labelOf.TryGetValue(status, out var label)) ordered.Add((status, label));
        foreach (var status in firstSeen)
            if (!canonical.Contains(status)) ordered.Add((status, labelOf[status]));
        return ordered.ToArray();
    }

    internal static (string Status, string Label)[] BuildSunburstLegendItems(
        bool hasFollowUps, bool hasUnplanned = false, bool hasNoPlan = false)
    {
        var items = new List<(string, string)>
        {
            ("pending", "Pending"), ("drafted", "Drafted"), ("ready", "Ready for dev"),
            ("active", "In development"), ("review", "In review"), ("done", "Done"),
        };
        // The four chart-local swatches take their wording from the shared map above, never a second literal.
        if (hasNoPlan)
            items.Add(("noplan", SunburstLocalStatusLabel("noplan")!));
        if (hasFollowUps)
        {
            items.Add(("followup-open", SunburstLocalStatusLabel("followup-open")!));
            items.Add(("followup-done", SunburstLocalStatusLabel("followup-done")!));
        }
        if (hasUnplanned)
            items.Add(("unplanned", SunburstLocalStatusLabel("unplanned")!));
        return items.ToArray();
    }

    private static string TruncateFollowUpText(string text, int max = 80)
    {
        if (text.Length <= max) return text;
        return text[..(max - 1)].TrimEnd() + "…";
    }

    // `Charts.AnnularSector` — the SVG path builder for a donut slice — was DELETED by Story 20.9, and it is the
    // last hand-rolled arc geometry in this codebase. Story 20.7 retired its planning callers and Story 20.9's
    // `BuildSunburstSvg` deletion took the last one; it had been sitting here with zero callers, which nothing but
    // a search would have shown (an unused private method is not a build warning).
    //
    // That is the concrete, checkable form of Epic 20 AC#2's "exactly one implementation of a hierarchy chart
    // exists in the codebase": Charts.cs no longer contains polar geometry at all. `HierarchyRolloutTests` asserts
    // both this name and `BuildSunburstSvg` are absent, so the state cannot quietly regress.
    // [Story 20.9 Task 4.2 / F8]

    // `Charts.EpicSunburst` was DELETED by Story 20.7. The epic page renders through
    // HierarchyExplorer.ProjectEpic, which covers the same node set — stories, story-child deferred, and the
    // open/done follow-up aggregates — with the per-story destination lifted verbatim rather than re-derived.
    // [Story 20.7 Task 8.1]

    // `Charts.TaskSunburst` was DELETED by Story 20.7. The story page renders through
    // HierarchyExplorer.ProjectStoryTasks. Its task vocabulary ("Done" / "Not done") moved to
    // Charts.TaskStatusLabel rather than into SunburstLocalStatusLabel, which is consulted by every surface.
    // [Story 20.7 Task 8.1]

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
    /// left. Young repos (&lt; ~15 weeks of history) trim to roughly first-commit minus one week of lead-in
    /// instead of padding months of empty pre-project cells; older repos start at the first commit. [Story 10.6 AC2a]
    /// <para>When <paramref name="commitsByDay"/> is provided, each active day's cell becomes an in-SVG link
    /// to an inline details panel below the chart (short hash + subject per commit, prev/next links between
    /// active days). Panel visibility is pure-CSS <c>:target</c>, so the drill-down needs no JS.</para>
    /// <para><paramref name="today"/> is the run's ONE policy-resolved cutoff day (<see cref="ResolveToday"/>),
    /// threaded in by the <see cref="SiteGenerator"/> so the grid extent, the future-skew suppression, and the linked
    /// cells all move together with the generated page set. Omitting it falls back to the machine-local day — the
    /// status quo — so library/test callers are unaffected. [Story 5.5]</para></summary>
    public static string CommitHeatmap(
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>>? commitsByDay = null,
        bool showHeadline = true,
        DateOnly? today = null)
    {
        if (series.Count == 0) return "<div class=\"chart-empty\">No git history available.</div>";

        var todayValue = today ?? ResolveToday(new DateCutoff(DatePolicy.MachineLocal, null), series: null);

        // Everything the summary text claims is derived from the days the grid ACTUALLY renders (<= the cutoff), not
        // from the whole series — otherwise the aria-label and the headline name commits the cells don't show, which
        // ADR 0013 forbids and which a past --as-of (or plain clock skew) makes routine rather than exotic. The
        // already-migrated twin is DeliveryCadenceHeatmap, which bounds its own summary figures to `visible` the
        // same way — though its empty state stays generic ("No completed stories to chart yet.") rather than
        // naming the cutoff the way the guard below does; that gap is DeliveryCadenceHeatmap's, not this story's
        // to fix. [Story 5.7 D2 / AC #1a] [Review][Patch]
        var visible = series.Where(s => s.Day <= todayValue).ToList();
        // The crash guard, not merely an empty state: firstCommit/lastCommit below are Min/Max over this set and
        // firstCommit also drives start/isYoungRepo, so an --as-of date before every commit would throw
        // InvalidOperationException on a naive Min(). Answered with a designed empty state (UX-DR22) naming the
        // cutoff, so the state explains itself rather than reading as "no git history".
        if (visible.Count == 0)
        {
            return $"<div class=\"chart-empty\">No commits on or before {Html(DReadable(todayValue))}.</div>";
        }

        var byDay = visible.ToDictionary(s => s.Day, s => s.Count);
        var firstCommit = visible.Min(s => s.Day);
        var lastCommit = visible.Max(s => s.Day);

        // The grid never runs past the generation date: future-dated commits (clock/timezone skew) would
        // otherwise extend it into all-blank suppressed weeks and let the headline name a day the grid can't
        // show. [Story 1.5 A4 + review]
        var end = todayValue;
        // The heatmap is the primary "how has the work gone" visual, so show a fuller ~15-week window (it
        // scales up to fill its panel via CSS) rather than the old 7-week postage stamp. [Story 1.5 E1]
        var minStart = end.AddDays(-7 * 15);
        // A project younger than the 15-week floor used to pad the grid all the way back to minStart, painting
        // months of pre-project blank cells (the "dead zone" misreading). Trim to a short ~1-week lead-in
        // instead, so the grid starts near the actual first commit; the marker below highlights exactly where.
        // Old-repo behavior (grid already fuller than 15 weeks) is unchanged. [Story 10.6 AC2a]
        var isYoungRepo = firstCommit >= minStart;
        var start = isYoungRepo ? firstCommit.AddDays(-7) : firstCommit;
        // Future-dated first commits (clock skew) can put the young-repo lead-in after today — clamp so the
        // window never inverts into a negative week count / broken SVG. [Story 10.6 review]
        if (start > end) start = end;

        // Snap to full weeks (Sunday..Saturday) so the grid is rectangular.
        start = start.AddDays(-(int)start.DayOfWeek);
        end = end.AddDays(6 - (int)end.DayOfWeek);

        var totalDays = end.DayNumber - start.DayNumber + 1;
        var weeks = Math.Max(1, (int)Math.Ceiling(totalDays / 7.0));
        // Scale the heat over only the days the grid actually renders (<= today). A future-dated commit is
        // suppressed from the cells, so it must not inflate maxCount and depress every visible cell's level. [review]
        // (Reading `visible` rather than re-filtering `series` is a simplification, not a behavior change.)
        var maxCount = visible.Select(s => s.Count).DefaultIfEmpty(0).Max();

        const int cell = 11;
        const int gap = 3;
        const int leftGutter = 26;
        const int topGutter = 16;
        var width = leftGutter + weeks * (cell + gap);
        var height = topGutter + 7 * (cell + gap);
        // The stylesheet's .heatmap rule stretches the SVG to fill up to 460px of panel width — great for an
        // old repo's many-week grid, but a young repo's short grid (e.g. 5 weeks) gets blown up into huge,
        // disproportionate tiles when scaled to the same cap. Bound the stretch to a multiple of the grid's own
        // natural size instead, so short grids stay near their natural ~11-14px cells; a grid already near/over
        // 460px at 1.8x hits the same 460px ceiling as before. Inline style wins over the stylesheet class.
        var maxRenderWidth = Math.Min(460, (int)Math.Round(width * 1.8));

        // Whole-chart accessible name so the per-cell <title> tooltips (pointer-only) aren't the sole way to
        // read the heatmap: total commits, active days, and the date span. [Story 1.4 AC #1, UXO E6/H3]
        // Bound to `visible`, so this accessible name and the visible headline below (which restate the SAME
        // figures) can never name a commit outside the rendered window — they must move together or the text twin
        // disagrees with the visual (ADR 0013). [Story 5.7 AC #1a]
        var totalCommits = visible.Sum(s => s.Count);
        var activeDays = visible.Count(s => s.Count > 0);
        var heatAria = $"Commit activity: {totalCommits} commit{(totalCommits == 1 ? string.Empty : "s")} across {activeDays} active day{(activeDays == 1 ? string.Empty : "s")}, {DReadable(firstCommit)} to {DReadable(lastCommit)}";

        // The linked days, resolved up front: they decide each cell's link wrapper and the SVG role. Each
        // links to its generated per-day page (commits/{date}.html). Shared with the SiteGenerator so the
        // set of linked cells and the set of generated pages can never disagree.
        var linkedDays = LinkedCommitDays(series, commitsByDay, todayValue);
        var linkedSet = new HashSet<DateOnly>(linkedDays);

        var sb = new StringBuilder();
        // Visible one-line headline so a stakeholder reads the summary before scanning the grid. [Story 1.5 E1]
        // Suppressed when the heatmap is embedded in the consolidated Git Pulse panel, whose signal strip
        // already carries these figures (avoids a duplicate summary line). [Story 3.1 consolidation]
        if (showHeadline)
        {
            // The "last commit" date is a date in the context of a change — link it to that day's date page so the
            // reader can click through (Story 7.3/10.4). Guarded on it being a linked day: normally it is (lastCommit
            // is the newest VISIBLE day and has commits), but a render without commitsByDay has no linked set at all,
            // so we fall back to plain text. Uses the same root-relative commits/ href the cells use — never a dead
            // link.
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
        sb.Append($"<svg class=\"heatmap\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" style=\"max-width:{maxRenderWidth}px\" role=\"{role}\" aria-label=\"{Html(heatAria)}\">\n");

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
                if (day > todayValue) continue;

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

        // First-commit accent (Story 10.6, AC2a): a thin vertical marker at the boundary of the first-commit
        // week, drawn only for the young-repo trim case — an old repo's grid already starts exactly at
        // firstCommit, so there is no lead-in to mark. Decorative (aria-hidden); the caption below is the
        // accessible/text-equivalent half of the "never color-only" pairing. Both halves share the in-range
        // week gate so a future-skewed firstCommit never gets a caption without a mark (or vice versa).
        var showFirstCommitMark = false;
        if (isYoungRepo)
        {
            var firstCommitWeek = (firstCommit.DayNumber - start.DayNumber) / 7;
            showFirstCommitMark = firstCommitWeek is >= 0 && firstCommitWeek < weeks;
            if (showFirstCommitMark)
            {
                var markX = leftGutter + firstCommitWeek * (cell + gap) - gap / 2.0 - 1;
                var markHeight = 7 * (cell + gap) - gap + 4;
                sb.Append($"  <rect class=\"heatmap-first-commit-mark\" x=\"{F(markX)}\" y=\"{topGutter - 2}\" width=\"2\" height=\"{markHeight}\" aria-hidden=\"true\">" +
                          $"<title>First commit {Html(DReadable(firstCommit))}</title></rect>\n");
            }
        }

        sb.Append("</svg>\n");

        // Text-equivalent half of the first-commit marker (never color/accent-only) — same gate as the SVG mark.
        if (showFirstCommitMark)
        {
            sb.Append($"<p class=\"heatmap-first-commit\">First commit {Html(DReadable(firstCommit))}</p>\n");
        }

        // Real-value legend + numeric window (Story 10.2): per-level count ranges from the SAME HeatLevel
        // thresholds the cells use; window is the grid span (weeks + first..last), distinct from the Git Pulse
        // 30-day signal. Lives in the builder so it appears both standalone and when the headline is suppressed.
        var windowText = $"{weeks.ToString(CultureInfo.InvariantCulture)} {Plural(weeks, "week", "weeks")} · {DReadable(firstCommit)} \u2013 {DReadable(lastCommit)}";
        sb.Append("<div class=\"heatmap-meta\">\n");
        sb.Append("<div class=\"heatmap-legend\">");
        for (var l = 0; l <= 4; l++)
        {
            // Skip levels no cell can ever render at this maxCount (Story 10.2 review) — otherwise a small
            // maxCount collapses several levels together and the legend shows duplicate, indistinguishable "—"
            // swatches for buckets that are all equally (and identically) unused.
            if (IsHeatLevelUnreachable(l, maxCount)) continue;
            sb.Append($"<span class=\"heatmap-legend-item\"><span class=\"heatmap-legend-swatch level-{l}\"></span>" +
                      $"<span class=\"heatmap-legend-label\">{Html(HeatLevelRange(l, maxCount))}</span></span>");
        }
        sb.Append("</div>\n");
        sb.Append($"<span class=\"chart-frame-window heatmap-window\">{Html(windowText)}</span>\n");
        sb.Append("</div>\n");

        return sb.ToString();
    }

    // ----- Delivery cadence (Story 21.2) --------------------------------------------------------------------

    /// <summary>Story-completion calendar heatmap — the delivery-cadence sibling of <see cref="CommitHeatmap"/>:
    /// the SAME day-grid visual language (week columns, month labels, <see cref="HeatLevel"/> ramp, real-value
    /// legend, young-repo trim) but keyed on the days stories reached <em>done</em>, not commit days. A day on which
    /// exactly one story completed links to that story's page; a day with several completions carries a rich hover
    /// tooltip (shared body-level <c>js-tip</c> node — never a clipped CSS <c>::after</c>) listing all of them. Every
    /// cell pairs its shade with a <c>&lt;title&gt;</c>, and a full text-equivalent completion log renders below the
    /// SVG so a screen-reader (or no-JS) reader learns every completion date without the graphic (never color-only).
    /// Written as an INDEPENDENT builder rather than refactoring <see cref="CommitHeatmap"/> so that shipped chart's
    /// output stays byte-identical (Story 21.2 guardrail). Empty series → a friendly empty state.
    /// <paramref name="today"/> bounds the grid so it never runs past the generation date — pinnable for
    /// deterministic tests, defaulting to the codebase's accepted single-call-per-run "today" (FR31). [Story 21.2]</summary>
    public static string DeliveryCadenceHeatmap(
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<StoryInfo>>? completionsByDay = null,
        Func<StoryInfo, string?>? storyHref = null,
        DateOnly? today = null)
    {
        if (series.Count == 0) return "<div class=\"chart-empty\">No completed stories to chart yet.</div>";

        var todayValue = today ?? DateOnly.FromDateTime(DateTime.Now);
        // Only completions on/before the run's "today" are ever drawn (the grid never extends past today). Bound
        // EVERY summary derived below — firstDay/lastDay, the aria counts, the legend max, the window label — to
        // that same visible set, so a story carrying a future-dated Change-Log date can't make the aria-label /
        // window overstate what the cells actually render (the project's truthfulness invariant). GroupBy (not
        // ToDictionary) also keeps this total on a duplicate-day series instead of throwing.
        var visible = series.Where(s => s.Day <= todayValue).ToList();
        if (visible.Count == 0) return "<div class=\"chart-empty\">No completed stories to chart yet.</div>";

        var byDay = visible.GroupBy(s => s.Day).ToDictionary(g => g.Key, g => g.Sum(s => s.Count));
        var firstDay = visible.Min(s => s.Day);
        var lastDay = visible.Max(s => s.Day);

        // Same windowing math as CommitHeatmap: never past today, ~15-week floor, young-repo lead-in trim.
        var end = todayValue;
        var minStart = end.AddDays(-7 * 15);
        var isYoungRepo = firstDay >= minStart;
        var start = isYoungRepo ? firstDay.AddDays(-7) : firstDay;
        if (start > end) start = end;
        start = start.AddDays(-(int)start.DayOfWeek);
        end = end.AddDays(6 - (int)end.DayOfWeek);

        var totalDays = end.DayNumber - start.DayNumber + 1;
        var weeks = Math.Max(1, (int)Math.Ceiling(totalDays / 7.0));
        var maxCount = visible.Select(s => s.Count).DefaultIfEmpty(0).Max();

        const int cell = 11;
        const int gap = 3;
        const int leftGutter = 26;
        const int topGutter = 16;
        var width = leftGutter + weeks * (cell + gap);
        var height = topGutter + 7 * (cell + gap);
        var maxRenderWidth = Math.Min(460, (int)Math.Round(width * 1.8));

        var totalCompletions = visible.Sum(s => s.Count);
        var activeDays = visible.Count(s => s.Count > 0);
        var heatAria = $"Story completions: {totalCompletions} {Plural(totalCompletions, "story", "stories")} across {activeDays} active {Plural(activeDays, "day", "days")}, {DReadable(firstDay)} to {DReadable(lastDay)}";

        // A day is linked only when EXACTLY one story completed that day AND its page resolves — an unambiguous
        // single target. Multi-completion days get the tooltip + the text log instead of a guessed link.
        var linkedSet = new HashSet<DateOnly>();
        if (completionsByDay is not null && storyHref is not null)
        {
            foreach (var s in series)
            {
                if (s.Count == 1 && s.Day <= todayValue
                    && completionsByDay.TryGetValue(s.Day, out var stories) && stories.Count == 1
                    && storyHref(stories[0]) is { Length: > 0 })
                {
                    linkedSet.Add(s.Day);
                }
            }
        }

        var sb = new StringBuilder();
        var role = linkedSet.Count > 0 ? "group" : "img";
        sb.Append($"<svg class=\"heatmap\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" style=\"max-width:{maxRenderWidth}px\" role=\"{role}\" aria-label=\"{Html(heatAria)}\">\n");

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
                if (day > todayValue) continue;

                var count = byDay.GetValueOrDefault(day, 0);
                var level = HeatLevel(count, maxCount);
                var x = leftGutter + w * (cell + gap);
                var y = topGutter + d * (cell + gap);
                var stories = completionsByDay is not null && completionsByDay.TryGetValue(day, out var list)
                    ? list
                    : (IReadOnlyList<StoryInfo>)Array.Empty<StoryInfo>();

                var linked = linkedSet.Contains(day);
                var multi = count > 1;
                var titleText = $"{DReadable(day)}: {count} {Plural(count, "story", "stories")} completed";

                if (linked)
                {
                    var story = stories.Count == 1 ? stories[0] : null;
                    var storyLabel = story is not null ? $" — {story.Title}" : string.Empty;
                    sb.Append($"  <a href=\"{Html(storyHref!(stories[0])!)}\" aria-label=\"{Html($"{DReadable(day)}: 1 story completed{storyLabel} — view story")}\">");
                    sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"{cell}\" height=\"{cell}\" rx=\"2\" class=\"heatmap-cell level-{level}\" style=\"--col:{w}\">");
                    sb.Append($"<title>{Html(titleText)}</title></rect></a>\n");
                }
                else if (multi)
                {
                    // Every story that day listed in ONE native <title> (no clip, no second overlapping tooltip):
                    // deliberately NOT a js-tip here, so a cell shows a single, consistent tooltip like every other
                    // cell. The collapsed completion log below carries the linked/rich version; the cell stays
                    // aria-hidden like the zero cells, with the log as its screen-reader / no-JS twin.
                    var titleFull = titleText + "\n" + string.Join("\n", stories.Select(s => $"Story {s.Id} — {s.Title}"));
                    sb.Append($"  <rect aria-hidden=\"true\" x=\"{x}\" y=\"{y}\" width=\"{cell}\" height=\"{cell}\" rx=\"2\" class=\"heatmap-cell level-{level}\" style=\"--col:{w}\">");
                    sb.Append($"<title>{Html(titleFull)}</title></rect>\n");
                }
                else
                {
                    sb.Append($"  <rect aria-hidden=\"true\" x=\"{x}\" y=\"{y}\" width=\"{cell}\" height=\"{cell}\" rx=\"2\" class=\"heatmap-cell level-{level}\" style=\"--col:{w}\">");
                    sb.Append($"<title>{Html(titleText)}</title></rect>\n");
                }
            }
        }

        // First-completion accent (mirrors CommitHeatmap's first-commit marker), young-repo trim only.
        var showFirstMark = false;
        if (isYoungRepo)
        {
            var firstWeek = (firstDay.DayNumber - start.DayNumber) / 7;
            showFirstMark = firstWeek is >= 0 && firstWeek < weeks;
            if (showFirstMark)
            {
                var markX = leftGutter + firstWeek * (cell + gap) - gap / 2.0 - 1;
                var markHeight = 7 * (cell + gap) - gap + 4;
                sb.Append($"  <rect class=\"heatmap-first-commit-mark\" x=\"{F(markX)}\" y=\"{topGutter - 2}\" width=\"2\" height=\"{markHeight}\" aria-hidden=\"true\">" +
                          $"<title>First completion {Html(DReadable(firstDay))}</title></rect>\n");
            }
        }

        sb.Append("</svg>\n");

        if (showFirstMark)
        {
            sb.Append($"<p class=\"heatmap-first-commit\">First completion {Html(DReadable(firstDay))}</p>\n");
        }

        // Real-value legend + window span — same HeatLevel thresholds the cells use, so shade and label can't
        // disagree. The window is the grid's own date span (distinct from any commit-based window).
        var windowText = $"{weeks.ToString(CultureInfo.InvariantCulture)} {Plural(weeks, "week", "weeks")} · {DReadable(firstDay)} – {DReadable(lastDay)}";
        sb.Append("<div class=\"heatmap-meta\">\n");
        sb.Append("<div class=\"heatmap-legend\">");
        // Unit label so the shade ramp isn't misread as commits-per-day (this is a story-completion heatmap, a
        // sparser signal than commit activity). [Story 21.2 review]
        sb.Append("<span class=\"heatmap-legend-caption\">Stories completed / day</span>");
        for (var l = 0; l <= 4; l++)
        {
            if (IsHeatLevelUnreachable(l, maxCount)) continue;
            sb.Append($"<span class=\"heatmap-legend-item\"><span class=\"heatmap-legend-swatch level-{l}\"></span>" +
                      $"<span class=\"heatmap-legend-label\">{Html(HeatLevelRange(l, maxCount))}</span></span>");
        }
        sb.Append("</div>\n");
        sb.Append($"<span class=\"chart-frame-window heatmap-window\">{Html(windowText)}</span>\n");
        sb.Append("</div>\n");

        // Text-equivalent completion log — the accessible / no-JS twin of the grid: every active day (newest
        // first), the count, and links to the stories completed that day. This is how a screen-reader reader (and
        // any no-JS visitor) learns the completion dates the SVG shows (UX-DR17 "never color-only").
        AppendCadenceLog(sb, series, completionsByDay, storyHref, todayValue);

        return sb.ToString();
    }

    /// <summary>The newest-first text log paired with <see cref="DeliveryCadenceHeatmap"/> (its accessible twin).
    /// Collapsed into a <c>&lt;details&gt;</c> so the tile grid stays the primary view (owner feedback) while the
    /// full list remains one click away — and still reachable by screen readers / with JS off (never lost). Only
    /// active days on/before <paramref name="today"/> appear; each lists the stories completed that day, linked
    /// when a page resolves. [Story 21.2 review]</summary>
    private static void AppendCadenceLog(
        StringBuilder sb,
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<StoryInfo>>? completionsByDay,
        Func<StoryInfo, string?>? storyHref,
        DateOnly today)
    {
        var activeDays = series
            .Where(s => s.Count > 0 && s.Day <= today)
            .OrderByDescending(s => s.Day)
            .ToList();
        if (activeDays.Count == 0) return;

        sb.Append("<details class=\"cadence-log-details\">\n");
        sb.Append($"  <summary class=\"cadence-log-summary\">Completion log — {activeDays.Count.ToString(CultureInfo.InvariantCulture)} active {Plural(activeDays.Count, "day", "days")}</summary>\n");
        sb.Append("<ol class=\"cadence-log\">\n");
        foreach (var (day, count) in activeDays)
        {
            var stories = completionsByDay is not null && completionsByDay.TryGetValue(day, out var list)
                ? list
                : (IReadOnlyList<StoryInfo>)Array.Empty<StoryInfo>();
            sb.Append("  <li class=\"cadence-log-row\">\n");
            sb.Append($"    <span class=\"cadence-log-date\">{Html(DReadable(day))}</span>\n");
            sb.Append($"    <span class=\"cadence-log-count\">{count} {Plural(count, "story", "stories")} completed</span>\n");
            if (stories.Count > 0)
            {
                sb.Append("    <span class=\"cadence-log-stories\">");
                for (var i = 0; i < stories.Count; i++)
                {
                    var story = stories[i];
                    if (i > 0) sb.Append(", ");
                    var href = storyHref?.Invoke(story);
                    var label = $"Story {story.Id}";
                    sb.Append(href is { Length: > 0 }
                        ? $"<a href=\"{Html(href)}\" title=\"{Html(story.Title)}\">{Html(label)}</a>"
                        : $"<span title=\"{Html(story.Title)}\">{Html(label)}</span>");
                }
                sb.Append("</span>\n");
            }
            sb.Append("  </li>\n");
        }
        sb.Append("</ol>\n");
        sb.Append("</details>\n");
    }

    /// <summary>The story cycle-time distribution as a bucketed bar chart (Story 21.2) — modeled on
    /// <see cref="HotspotBars"/>' proportional-bar language: one bar per human-readable day-range bucket, width
    /// relative to the busiest bucket, the real count in text beside every bar (never size/color-only). Buckets are
    /// fixed and stated in the caller's caption. Degrades to a friendly note when no story has a derivable
    /// cycle-time (a common, expected case for young/small projects — not an error). Cycle-time is
    /// APPROXIMATE (story-file age, not a tracked workflow timestamp) — the caller's frame carries that caveat.
    /// [Story 21.2]</summary>
    public static string CycleTimeHistogram(IReadOnlyList<(string StoryId, int Days)> cycleTimes)
    {
        if (cycleTimes.Count == 0)
            return "<div class=\"chart-empty\">No story has a derivable cycle-time yet.</div>\n";

        var counts = new int[CycleTimeBuckets.Length];
        foreach (var (_, days) in cycleTimes)
        {
            counts[CycleTimeBucketIndex(days)]++;
        }

        var maxCount = counts.Max();
        var sb = new StringBuilder();
        sb.Append("<ol class=\"git-pulse-bars cycle-time-histogram\">\n");
        for (var i = 0; i < CycleTimeBuckets.Length; i++)
        {
            var (label, _) = CycleTimeBuckets[i];
            var count = counts[i];
            // Empty buckets still render (the distribution's shape is the information) at a hairline; a nonzero
            // bucket floors at 6% so a lone story never renders as an invisible sliver.
            var pct = maxCount <= 0 ? 0 : count == 0 ? 0 : Math.Clamp((int)Math.Round((double)count / maxCount * 100), 6, 100);
            var countText = $"{count} {Plural(count, "story", "stories")}";
            sb.Append(
                $"  <li aria-label=\"{Html($"{label}: {countText}")}\"><span class=\"git-pulse-bar-label\">{Html(label)}</span>" +
                $"<span class=\"git-pulse-bar-track\" aria-hidden=\"true\"><span class=\"git-pulse-bar-fill\" style=\"width:{pct}%\"></span></span>" +
                $"<span class=\"git-pulse-bar-count\">{Html(countText)}</span></li>\n");
        }
        sb.Append("</ol>\n");
        return sb.ToString();
    }

    /// <summary>The fixed, human-readable cycle-time buckets (inclusive lower bound, inclusive upper bound; the
    /// last is open-ended). Stated in the chart's caption so the reader knows the edges. [Story 21.2]</summary>
    private static readonly (string Label, int MaxDaysInclusive)[] CycleTimeBuckets =
    {
        ("0–3 days", 3),
        ("4–7 days", 7),
        ("8–14 days", 14),
        ("15–30 days", 30),
        ("30+ days", int.MaxValue),
    };

    /// <summary>The bucket a day-delta falls into. Non-negative by construction (the builder skips negatives).</summary>
    private static int CycleTimeBucketIndex(int days)
    {
        for (var i = 0; i < CycleTimeBuckets.Length; i++)
        {
            if (days <= CycleTimeBuckets[i].MaxDaysInclusive) return i;
        }
        return CycleTimeBuckets.Length - 1;
    }

    /// <summary>The compact delivery-cadence teaser for the dashboard (Story 21.2 Task 4): recent + all-time
    /// completion readings and a link to the dedicated <c>cadence.html</c> page. Not a second full heatmap — a
    /// teaser. Reuses the Git Pulse signal-strip classes (the visuals genuinely match) rather than inventing
    /// near-duplicates. Empty data renders nothing (NFR8 — omit, don't show an empty panel). [Story 21.2]</summary>
    public static string DeliveryCadenceStrip(DeliveryCadenceData data, string cadenceHref, DateOnly? today = null)
    {
        if (data.IsEmpty) return string.Empty;

        var todayValue = today ?? DateOnly.FromDateTime(DateTime.Now);
        const int recentWeeks = 8;
        var cutoff = todayValue.AddDays(-7 * recentWeeks);
        var recent = data.CompletionSeries.Where(s => s.Day >= cutoff && s.Day <= todayValue).Sum(s => s.Count);
        var total = data.TotalCompletions;

        var sb = new StringBuilder();
        sb.Append("<div class=\"cadence-strip\">\n");
        sb.Append("  <div class=\"git-pulse-signals\">\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-num\">{recent}</span>" +
                  $"<span class=\"git-pulse-caption\">{Plural(recent, "story", "stories")} completed in the last {recentWeeks} weeks</span></div>\n");
        sb.Append($"    <div class=\"git-pulse-signal\"><span class=\"git-pulse-num\">{total}</span>" +
                  $"<span class=\"git-pulse-caption\">completed all-time</span></div>\n");
        sb.Append("  </div>\n");
        sb.Append($"  <a class=\"view-epic-link cadence-strip-link\" href=\"{Html(cadenceHref)}\">View delivery cadence &rarr;</a>\n");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The consolidated dashboard "Git Pulse" panel body — one panel that merges what used to be two
    /// (the separate "Commit Activity" heatmap and the baseline pulse). A headline signal strip (30-day count,
    /// exact last-commit timestamp, active days) sits over a two-part body: the activity heatmap and the
    /// most-changed files rendered as proportional bars. Pure HTML/CSS + inline SVG (no JS), matching the other
    /// chart builders. When the bounded name-only git call came back empty but the rest of the pulse succeeded,
    /// the files section degrades to a graceful note rather than vanishing (partial data beats none; AD-4). The
    /// whole-panel null case (no git at all) is the caller's `p.Git is {}` fallback. [Story 3.1 + consolidation]
    /// <para><paramref name="today"/> is the run's ONE policy-resolved cutoff day (<see cref="ResolveToday"/>),
    /// threaded in by the <see cref="SiteGenerator"/> — see the guard below. Omitting it falls back to the
    /// machine-local day, same as <see cref="CommitHeatmap"/>, so library/test callers are unaffected; every
    /// production call site threads the run's real value and must keep doing so, or this panel's linked day and
    /// the heatmap it embeds can silently disagree with what was actually generated. [Story 5.5]</para></summary>
    public static string GitPulsePanel(GitPulse git, Func<string, string?>? fileHref = null, DateOnly? today = null)
    {
        // The exact last-commit clock, routed through the single PortalDates formatter (Story 10.4): 24-hour,
        // no per-row zone suffix — the git clock's zone is explained once by the caption below (owner-chosen
        // "captioned git" treatment). Kept in the commit's authored offset, never converted.
        var last = PortalDates.Timestamp(git.LastCommitTimestamp);
        // The last-commit date is a date in the context of a change → link it to that day's date page, guarded on
        // actual membership in the generated date-page set (the SAME LinkedCommitDays the heatmap uses) rather than
        // a bare date comparison, so this guard can never drift from what pages actually exist — never a dead link.
        var lastDay = DateOnly.FromDateTime(git.LastCommitTimestamp);
        // The run's ONE policy-resolved cutoff (Story 5.5), threaded from the SiteGenerator — not a second
        // independent clock read. This guard and the page generator must filter on the same day or this link can
        // point at a page that was never written.
        var todayValue = today ?? ResolveToday(new DateCutoff(DatePolicy.MachineLocal, null), series: null);
        var linkedDays = LinkedCommitDays(git.DailySeries, git.CommitsByDay, todayValue);
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
        // Window caption lives inside the heatmap builder; why-sentence from the shared ChartMeta source.
        sb.Append("    <div class=\"git-pulse-activity\">\n");
        sb.Append(CommitHeatmap(git.DailySeries, git.CommitsByDay, showHeadline: false, today: todayValue));
        sb.Append(FrameWhySlot(WhyText(ChartMetric.ActivityCadence)));
        sb.Append("    </div>\n");

        // Top changed files as proportional bars. Ranking + window + why come from the shared frame slots
        // (Story 10.2) — honest window is min(200, TotalCommits), never a lying literal "200".
        var filesWindowCommits = Math.Min(200, git.TotalCommits);
        var filesWindow = $"Last {filesWindowCommits.ToString(CultureInfo.InvariantCulture)} commits";
        var filesRanking = git.TopChangedFiles.Count > 0
            ? $"Top {git.TopChangedFiles.Count.ToString(CultureInfo.InvariantCulture)} files by change count"
            : null;
        sb.Append("    <div class=\"git-pulse-files\">\n");
        sb.Append($"      <div class=\"chart-frame-head\"><span class=\"git-pulse-files-title\">Top changed files</span>{FrameWindowSlot(filesWindow)}</div>\n");
        sb.Append(FrameRankingSlot(filesRanking));
        if (git.TopChangedFiles.Count > 0)
        {
            var maxChanges = git.TopChangedFiles.Max(f => f.ChangeCount);
            sb.Append("      <ol class=\"git-pulse-bars\">\n");
            foreach (var (path, changeCount) in git.TopChangedFiles)
            {
                // Floor the fill so the least-changed file still shows a visible sliver; the exact count stays
                // in text so the bar is decorative, not the sole information carrier (never color/size-only).
                var pct = maxChanges <= 0 ? 0 : Math.Clamp((int)Math.Round((double)changeCount / maxChanges * 100), 6, 100);
                var countText = $"{changeCount} {Plural(changeCount, "change", "changes")}";
                // Unifying accessible name for label + decorative bar + count (ProgressBar pattern).
                var rowAria = Html($"{path}: {countText}");
                // The label links to the file's in-portal code page (or external fallback) via the same seam the
                // hotspots list uses; when no resolver is supplied (e.g. the webview path) CodeItemLink returns the
                // plain escaped path, so the output is byte-identical to before.
                sb.Append(
                    $"        <li aria-label=\"{rowAria}\"><span class=\"git-pulse-bar-label\" title=\"{Html(path)}\">{CodeItemLink(path, fileHref)}</span>" +
                    $"<span class=\"git-pulse-bar-track\" aria-hidden=\"true\"><span class=\"git-pulse-bar-fill\" style=\"width:{pct}%\"></span></span>" +
                    $"<span class=\"git-pulse-bar-count\">{countText}</span></li>\n");
            }
            sb.Append("      </ol>\n");
        }
        else
        {
            sb.Append($"      <div class=\"chart-empty\">No file changes in the last {filesWindowCommits.ToString(CultureInfo.InvariantCulture)} commits.</div>\n");
        }
        sb.Append(FrameWhySlot(WhyText(ChartMetric.FileChurn)));
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
                    // No href (e.g. the page failed to generate): the card itself has no other interactive
                    // control, but its js-tip tooltip carries the decision-journal (memlog) date that is NOT
                    // shown in the card body (Task 3.3 above keeps only the primary mtime visible). Without a
                    // tabindex this secondary date would be reachable by hover only — mirroring the same
                    // "tabindex only when a tooltip makes keyboard focus meaningful" convention used by Tile
                    // (Charts.cs) keeps it reachable by keyboard/AT focus too.
                    sb.Append($"    <div class=\"{cls}\" data-tip=\"{tip}\" tabindex=\"0\">{body}</div>\n");
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
    /// <see cref="CouplingGraph"/>): one row per DIRECTED couple, headed and aligned so the counts scan as columns.
    /// Full paths shown as real text (ellipsis-truncated via CSS, full value in the cell <c>title</c>) so the visual
    /// graph is never the sole information carrier. Not statuses, so no <c>--status-*</c> tokens. Degrades to a
    /// friendly note when nothing crosses the coupling threshold.
    /// <para><b>Story 24.1</b> made the rows directional (<see cref="DirectedCouple"/>, ranked by confidence
    /// upstream in <see cref="DeepGitPulse.DirectedCoupling"/>) and added the <b>Confidence</b> column: "when the
    /// first file changes, the second changes this often". The old symmetric "N× together" count survives as the
    /// Together column — support is what makes a confidence trustworthy, so both are shown rather than one
    /// replacing the other. Because rows are directed, the same pair may legitimately appear in both directions
    /// with different confidence; that asymmetry is the finding, not a duplicate.</para>
    /// <para>The trailing "Kind" cell carries up to two independent TEXT badges (never colour alone, UX-DR19/NFR8):
    /// "Process" when either file is process signal (<see cref="GitMetrics.ClassifyCoupling"/>, Story 10.6) and
    /// "Cross-boundary" when the pair spans top-level directories (<see cref="GitMetrics.IsCrossBoundary"/>, Story
    /// 24.1). Both flags are read off the record — computed once upstream, never re-derived here. An ordinary
    /// same-module code pair leaves the cell blank rather than carrying a redundant label on the majority
    /// case.</para>
    /// [Story 3.2; Kind column: Story 10.6; directional + Confidence + Cross-boundary: Story 24.1]</summary>
    public static string CouplingTable(IReadOnlyList<DirectedCouple> coupling, Func<string, string?>? fileHref = null)
    {
        if (coupling.Count == 0) return "<div class=\"chart-empty\">No significant change coupling detected.</div>\n";

        var sb = new StringBuilder();
        sb.Append("<table class=\"coupling-table\">\n");
        sb.Append("  <thead><tr>" +
                  "<th scope=\"col\">File</th>" +
                  "<th scope=\"col\">Coupled with</th>" +
                  "<th scope=\"col\" class=\"coupling-num\">Together</th>" +
                  "<th scope=\"col\" class=\"coupling-num\">Confidence</th>" +
                  "<th scope=\"col\" class=\"coupling-kind\">Kind</th>" +
                  "</tr></thead>\n");
        sb.Append("  <tbody>\n");
        foreach (var couple in coupling)
        {
            var isProcess = couple.Kind == GitMetrics.CouplingKind.Process;
            // Both markers are TEXT inside one cell (never colour alone, UX-DR19/NFR8) and independent, because the
            // two lenses are orthogonal: a pair can be routine upkeep AND span a module boundary.
            var badges = new StringBuilder();
            if (isProcess)
            {
                badges.Append("<span class=\"coupling-kind-badge\" title=\"At least one file is config, lockfile, build output, or a stylesheet — routine upkeep, not necessarily a code dependency.\">Process</span>");
            }
            if (couple.CrossBoundary)
            {
                if (badges.Length > 0) badges.Append(' ');
                badges.Append("<span class=\"coupling-boundary-badge\" title=\"These two files live in different top-level directories — coupling that crosses a module boundary is a stronger architectural signal.\">Cross-boundary</span>");
            }
            // Confidence is directional and read from the FROM file: "when this file changes, the other usually
            // changes too". Lift rides the cell's title — the specialist's number, not the headline.
            //
            // Basenames keep the sentence short, but two coupled files routinely SHARE one (index.ts, README.md,
            // __init__.py, mod.rs), and then the sentence names the same word twice — "When index.ts changes,
            // index.ts changes 80% of the time" — which reads as nonsense and identifies neither file. Fall back
            // to full paths for BOTH endpoints so the sentence stays parallel instead of mixing a bare name with
            // a path. [Story 24.1 code review]
            var fromLabel = Basename(couple.FromPath);
            var toLabel = Basename(couple.ToPath);
            if (string.Equals(fromLabel, toLabel, StringComparison.Ordinal))
            {
                fromLabel = couple.FromPath;
                toLabel = couple.ToPath;
            }
            var confidenceTitle = couple.Lift is { } lift
                ? $" title=\"When {Html(fromLabel)} changes, {Html(toLabel)} changes {Percent(couple.Confidence)} of the time — {LiftLabel(lift)} {Html(toLabel)}'s usual rate\""
                : $" title=\"When {Html(fromLabel)} changes, {Html(toLabel)} changes {Percent(couple.Confidence)} of the time\"";
            sb.Append(
                "    <tr>" +
                $"<td class=\"coupling-file\" title=\"{Html(couple.FromPath)}\">{CodeItemLink(couple.FromPath, fileHref)}</td>" +
                $"<td class=\"coupling-file\" title=\"{Html(couple.ToPath)}\">{CodeItemLink(couple.ToPath, fileHref)}</td>" +
                $"<td class=\"coupling-num\">{couple.Support}&times;</td>" +
                $"<td class=\"coupling-num\"{confidenceTitle}>{Percent(couple.Confidence)}</td>" +
                $"<td class=\"coupling-kind\">{badges}</td></tr>\n");
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
    public static string CouplingGraph(IReadOnlyList<CoupledPair> coupling, int size = 460, Func<string, string?>? fileHref = null)
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

        // Edges first so nodes render on top of them. Width + opacity scale with the co-change count. Process
        // pairs (Story 10.6, AC1) get a second class for a dashed stroke (never color-only) plus a title suffix.
        //
        // Both classifications are READ OFF THE PAIR, never re-derived here (AC #2): they were computed once in
        // GitMetrics.ParseNumstatLog beside the identical computation feeding DirectedCoupling, so the graph and
        // the table can never disagree about whether a pair crosses a module boundary. Cross-boundary rides the
        // title as WORDS — the edge carries no colour or stroke of its own for it (UX-DR19/NFR8), and the ranked
        // table beside this graph remains the surface that marks it in visible text. [Story 24.1 code review]
        foreach (var pair in coupling)
        {
            var (a, b, w) = pair;
            var (x1, y1) = Pos(order[a]);
            var (x2, y2) = Pos(order[b]);
            var isProcess = pair.Kind == GitMetrics.CouplingKind.Process;
            var edgeClass = isProcess ? "coupling-edge process-edge" : "coupling-edge";
            var titleSuffix = isProcess ? " (process-coupling)" : string.Empty;
            if (pair.CrossBoundary) titleSuffix += " (cross-boundary)";
            sb.Append($"  <line class=\"{edgeClass}\" x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" " +
                      $"stroke-width=\"{F(ScaleW(w, 1.5, 6))}\" stroke-opacity=\"{F(ScaleW(w, 0.35, 0.9))}\">" +
                      $"<title>{Html(Basename(a))} &harr; {Html(Basename(b))}: {w}&times; together{titleSuffix}</title></line>\n");
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

    /// <summary>Max characters for a drawn work-graph node label (the full label stays in the node tooltip +
    /// the templater's sr-only list). [Story 19.2]</summary>
    private const int WorkGraphLabelChars = 18;

    /// <summary>Renders one epic's provenance subgraph as pure, deterministic SVG (Story 19.2). A left→right
    /// LAYERED directed layout — Epic · Stories · Follow-ups (deferred/action) · Origins &amp; outcomes
    /// (sources/resolvers/retros) — NOT the hub-and-spoke ego model the code page uses
    /// (<see cref="RelationshipGraph"/>, which superseded this file's server-rendered <c>ReferenceGraph</c> SVG in
    /// Story 24.2). Node kind is shown
    /// by SHAPE (epic = rounded rect, story = circle, deferred = diamond, action = triangle, spec/source = small
    /// rect, retro = muted rect) — never a lifecycle colour; edge kind by STYLE (solid for structural /
    /// stemmed-from / resolves, dashed for the soft <c>raised-in</c>) with decorative <c>marker-end</c> arrowheads.
    /// Nodes with an href are <c>&lt;a&gt;</c> (navigation needs no JS); href-less nodes are non-link chips (guarded,
    /// mirroring Epic 7). The SVG is <c>role="img"</c> with a summary label; the caller
    /// (<see cref="WorkGraphTemplater"/>) supplies the complete sr-only node/edge enumeration (NFR6 — a
    /// <c>role="img"</c> collapses its descendants for assistive tech). Empty input → <c>""</c>. Every label is
    /// HTML-escaped. [Story 19.2; a11y idiom from Story 7.8]</summary>
    public static string WorkGraph(WorkGraphEpic epic)
    {
        if (epic.Nodes.Count == 0) return string.Empty;

        var epicNodeId = epic.Nodes.FirstOrDefault(n => n.Kind == WorkNodeKind.Epic)?.Id;
        // In-epic stories are the carriers of a Contains edge into the epic root; any other story node is an
        // external provenance source and belongs in the right-hand origins layer.
        var inEpicStories = new HashSet<string>(
            epic.Edges
                .Where(e => e.Kind == WorkEdgeKind.Contains && string.Equals(e.ToId, epicNodeId, StringComparison.Ordinal))
                .Select(e => e.FromId),
            StringComparer.Ordinal);

        int Layer(WorkNode n) => n.Kind switch
        {
            WorkNodeKind.Epic => 0,
            WorkNodeKind.Story => inEpicStories.Contains(n.Id) ? 1 : 3,
            WorkNodeKind.Deferred or WorkNodeKind.Action => 2,
            _ => 3, // Spec sources/resolvers, Retro
        };

        // Bucket nodes into their four layers, preserving builder (insertion) order for determinism.
        var columns = new List<WorkNode>[4];
        for (var i = 0; i < 4; i++) columns[i] = new List<WorkNode>();
        foreach (var n in epic.Nodes) columns[Layer(n)].Add(n);

        const double marginY = 40, rowGap = 58, nodeR = 9;
        var colX = new[] { 95.0, 320.0, 545.0, 770.0 };
        const double width = 865;
        var maxRows = Math.Max(1, columns.Max(c => c.Count));
        var height = Math.Max(150.0, marginY * 2 + (maxRows - 1) * rowGap);

        // Deterministic centred position per node: column x, evenly spaced y within the column.
        var pos = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        for (var col = 0; col < 4; col++)
        {
            var list = columns[col];
            if (list.Count == 0) continue;
            var span = (list.Count - 1) * rowGap;
            var top = (height - span) / 2.0;
            for (var k = 0; k < list.Count; k++)
                pos[list[k].Id] = (colX[col], top + k * rowGap);
        }

        var idToNode = epic.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);

        var aria = epic.Cycles.Count > 0
            ? $"Work graph for {epic.DisplayName}: {epic.Nodes.Count} work items, {epic.Edges.Count} provenance links, {epic.Cycles.Count} circular {Plural(epic.Cycles.Count, "chain", "chains")}. The list below enumerates every node and link."
            : $"Work graph for {epic.DisplayName}: {epic.Nodes.Count} work items and {epic.Edges.Count} provenance links, no circular provenance. The list below enumerates every node and link.";

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"work-graph\" viewBox=\"0 0 {F(width)} {F(height)}\" width=\"{F(width)}\" height=\"{F(height)}\" role=\"img\" aria-label=\"{Html(aria)}\" preserveAspectRatio=\"xMidYMid meet\">\n");
        // Decorative arrowhead marker (aria-hidden — the accessible equivalent is the sr-only edge list).
        sb.Append("  <defs aria-hidden=\"true\">\n");
        sb.Append("    <marker id=\"work-arrow\" class=\"work-arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" /></marker>\n");
        sb.Append("  </defs>\n");

        // Edges first so nodes sit on top. Pull the target end back to the node boundary so the arrowhead reads.
        foreach (var e in epic.Edges)
        {
            if (!pos.TryGetValue(e.FromId, out var a) || !pos.TryGetValue(e.ToId, out var b)) continue;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            var (bx, by) = len > 0.001 ? (b.X - dx / len * (nodeR + 6), b.Y - dy / len * (nodeR + 6)) : (b.X, b.Y);
            var cls = e.Kind == WorkEdgeKind.RaisedIn ? "work-edge work-edge-soft" : "work-edge";
            sb.Append($"  <line class=\"{cls}\" x1=\"{F(a.X)}\" y1=\"{F(a.Y)}\" x2=\"{F(bx)}\" y2=\"{F(by)}\" marker-end=\"url(#work-arrow)\" />\n");
        }

        foreach (var n in epic.Nodes)
        {
            if (!pos.TryGetValue(n.Id, out var p)) continue;
            AppendWorkNode(sb, n, p.X, p.Y, nodeR);
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>Draws one work-graph node: a kind-specific shape (never colour-as-sole-signal) plus a label
    /// beneath it (epic label sits inside its box). Linked nodes wrap in <c>&lt;a&gt;</c>; href-less nodes are a
    /// non-link <c>&lt;g&gt;</c> chip. The full label rides <c>&lt;title&gt;</c> + <c>aria-label</c>. [Story 19.2]</summary>
    private static void AppendWorkNode(StringBuilder sb, WorkNode n, double x, double y, double r)
    {
        var full = string.IsNullOrEmpty(n.Title) ? n.Label : $"{n.Label} — {n.Title}";
        // Epic/Story/Retro labels are already self-describing ("Epic 7", "Story 7.11", "Epic 3 retro",
        // "Unattributed") — only the summary/key labels of the other kinds need a kind-word prefix.
        var tip = n.Kind switch
        {
            WorkNodeKind.Deferred => $"Deferred item: {full}",
            WorkNodeKind.Action => $"Action item: {full}",
            WorkNodeKind.Spec => $"Source: {full}",
            _ => full,
        };
        var shortLabel = Shorten(n.Label, WorkGraphLabelChars);

        string glyph;
        switch (n.Kind)
        {
            case WorkNodeKind.Epic:
            {
                var w = Math.Max(84.0, Shorten(n.Label, 16).Length * 8 + 20);
                glyph = $"<rect class=\"work-node-epic\" x=\"{F(x - w / 2)}\" y=\"{F(y - 15)}\" width=\"{F(w)}\" height=\"30\" rx=\"7\" />"
                      + $"<text class=\"work-epic-label\" x=\"{F(x)}\" y=\"{F(y)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"14\">{Html(shortLabel)}</text>";
                break;
            }
            case WorkNodeKind.Story:
                glyph = $"<circle class=\"work-node-story\" cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"{F(r)}\" />"
                      + WorkNodeLabel(x, y, shortLabel);
                break;
            case WorkNodeKind.Deferred:
                glyph = $"<polygon class=\"work-node-deferred\" points=\"{F(x)},{F(y - r)} {F(x + r)},{F(y)} {F(x)},{F(y + r)} {F(x - r)},{F(y)}\" />"
                      + WorkNodeLabel(x, y, shortLabel);
                break;
            case WorkNodeKind.Action:
                glyph = $"<polygon class=\"work-node-action\" points=\"{F(x)},{F(y - r)} {F(x + r)},{F(y + r * 0.85)} {F(x - r)},{F(y + r * 0.85)}\" />"
                      + WorkNodeLabel(x, y, shortLabel);
                break;
            case WorkNodeKind.Retro:
                glyph = $"<rect class=\"work-node-retro\" x=\"{F(x - r)}\" y=\"{F(y - r * 0.8)}\" width=\"{F(r * 2)}\" height=\"{F(r * 1.6)}\" rx=\"2\" />"
                      + WorkNodeLabel(x, y, shortLabel);
                break;
            default: // Spec / source / resolver
                glyph = $"<rect class=\"work-node-spec\" x=\"{F(x - r)}\" y=\"{F(y - r * 0.8)}\" width=\"{F(r * 2)}\" height=\"{F(r * 1.6)}\" rx=\"3\" />"
                      + WorkNodeLabel(x, y, shortLabel);
                break;
        }

        if (n.Href is { Length: > 0 })
        {
            sb.Append($"  <a class=\"work-node\" href=\"{Html(n.Href)}\" aria-label=\"{Html(tip)}\"><title>{Html(tip)}</title>{glyph}</a>\n");
        }
        else
        {
            sb.Append($"  <g class=\"work-node work-node--chip\" role=\"img\" aria-label=\"{Html(tip)}\"><title>{Html(tip)}</title>{glyph}</g>\n");
        }
    }

    private static string WorkNodeLabel(double x, double y, string shortLabel) =>
        $"<text class=\"work-label\" x=\"{F(x)}\" y=\"{F(y + 20)}\" text-anchor=\"middle\" font-size=\"12\">{Html(shortLabel)}</text>";

    /// <summary>The last path segment (filename) of a path — compact graph labels while the full path stays in the
    /// node's tooltip. Backslashes are normalized first, exactly as <see cref="GitMetrics.IsCrossBoundary"/> does
    /// (which has a test pinning why it matters): without it a backslash-bearing path has no <c>/</c> to find, so
    /// the "filename" returned is the ENTIRE path and every label and tooltip built from it names a path where it
    /// meant to name a file. [normalization: Story 24.1 code review]</summary>
    private static string Basename(string path)
    {
        var normalized = path.Replace('\\', '/');
        var i = normalized.LastIndexOf('/');
        return i >= 0 && i < normalized.Length - 1 ? normalized[(i + 1)..] : normalized;
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

    /// <summary>Resolves the ONE <see cref="DateOnly"/> a run treats as "today" for the date-page cutoff, from the
    /// configured <paramref name="cutoff"/>. Pure: the only impurity is the wall clock the policy itself asks for
    /// (<see cref="DatePolicy.AsOf"/> asks for none — its day is an input).
    /// <para>Deliberately co-located with <see cref="LinkedCommitDays"/>, the consumer whose guarantee this value
    /// underwrites. A run must call this ONCE and share the result — the guarantee that a linked cell always has a
    /// generated page holds only while every consumer filters on the SAME day, and independent re-resolution (a
    /// <c>Utc</c> boundary crossed mid-run, a series re-read) is exactly how that would break. [Story 5.5]</para>
    /// <para><see cref="DatePolicy.LastCommit"/> degrades to <see cref="DatePolicy.MachineLocal"/> when
    /// <paramref name="series"/> is null or empty: with no git history (or an empty repo) there are no authored
    /// commit days to derive a cutoff from, so the honest fallback is the machine's own day rather than a crash or a
    /// default-date sentinel (NFR8).</para>
    /// <para><see cref="DatePolicy.AsOf"/> without a date degrades the same way, for the same reason: the validated
    /// CLI path cannot produce one (<c>--as-of</c> carries its date, and a dateless <c>as-of</c> token is rejected by
    /// <see cref="DatePolicies.TryParse"/>), but this resolver is also the LIBRARY entry point, where a hand-built
    /// <see cref="DateCutoff"/> can reach it. Degrade, do not throw. [Story 5.7]</para></summary>
    public static DateOnly ResolveToday(DateCutoff cutoff, IReadOnlyList<(DateOnly Day, int Count)>? series) => cutoff switch
    {
        { Policy: DatePolicy.Utc } => DateOnly.FromDateTime(DateTime.UtcNow),
        { Policy: DatePolicy.LastCommit } when series is { Count: > 0 } => series.Max(s => s.Day),
        // The one policy whose answer is an INPUT: no clock is read, so the same flag on any host on any day yields
        // the same cutoff — which is the whole point of Story 5.7.
        { Policy: DatePolicy.AsOf, AsOf: { } day } => day,
        // MachineLocal, plus the LastCommit-without-history and AsOf-without-a-date fallbacks. Written exactly as
        // the pre-5.5 status quo expressed it so the default policy is byte-identical, not merely equivalent.
        _ => DateOnly.FromDateTime(DateTime.Now),
    };

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

    /// <summary>Above this many file leaves, the treemap stops paying for a rich per-file tooltip card on every
    /// rectangle (deferred item, at-scale SPA perf pass: at large-repo/<c>--deep-git</c> scale a single
    /// <c>code-map.html</c> reached ~82.5 MB, because every file rect carries a multi-row HTML card doubly
    /// HTML-escaped into a <c>data-tip-html</c> attribute — the single biggest per-rect cost). Every file still
    /// gets its own correctly sized, correctly colored, keyboard-focusable rectangle with a real
    /// <c>aria-label</c> either way — a long-tail file's label additionally carries the SAME metrics (type,
    /// changes, churn, avg size, co-change, first/last) the card would have shown, as compact inline text instead
    /// of an HTML card, so AC #4 ("color never the sole signal") holds in TEXT form too, not just geometry+color
    /// (see <see cref="AppendTreemapFile"/>'s non-detailed branch) — only the CONVENIENCE hover popup is capped for
    /// the long tail, selected by the same "most significant first" ordering the file table already uses
    /// (<see cref="CodeMapTemplater"/>, via the shared <see cref="OrderBySignificance"/>). 4000 comfortably covers
    /// every real project this generator has been run against (Epic-7 scale is ~1,060 files) without ever
    /// tripping — default generation is byte-identical.</summary>
    public const int MaxDetailedCodeMapFiles = 4000;

    /// <summary>The ONE "most significant first" ordering both the treemap's detail cap and
    /// <see cref="CodeMapTemplater"/>'s file table sort by (change frequency descending, then size descending,
    /// then path for determinism) — shared so the two text-equivalents of the code-map visualization can never
    /// silently drift apart on which files count as "most significant."</summary>
    internal static IEnumerable<CodeMapNode> OrderBySignificance(IEnumerable<CodeMapNode> files) => files
        .OrderByDescending(f => f.Metrics?.Changes ?? -1)
        .ThenByDescending(f => f.Lines)
        .ThenBy(f => f.RepoRelativePath, StringComparer.OrdinalIgnoreCase);

    /// <summary>Σ of a subtree's own-file change counts — a DIRECTORY's significance, which it does not otherwise
    /// have. <see cref="CodeMapNode.Metrics"/> is null on every directory by construction
    /// (<see cref="CodeMap"/>'s type doc), so a directory sorted through
    /// <see cref="OrderBySignificance"/> keys on <c>-1</c> and sinks below every file that has any git record at
    /// all — which is why the tree needs its own comparer rather than reusing that one.
    /// <para>Files with no git record contribute 0, not -1: a directory of never-changed files is genuinely
    /// less significant than one with changes, but it is not less significant than nothing.</para></summary>
    internal static long SubtreeChanges(CodeMapNode node)
    {
        if (!node.IsDirectory) return node.Metrics?.Changes ?? 0;

        long total = 0;
        foreach (var child in node.Children) total += SubtreeChanges(child);
        return total;
    }

    /// <summary>Orders ONE level of the code-map tree — the directories and files that share a parent — by the same
    /// notion of significance <see cref="OrderBySignificance"/> uses, with a directory standing for the rolled-up
    /// weight of everything beneath it.
    ///
    /// <para><b>Directories and files interleave, deliberately.</b> "Directories first" is the conventional file
    /// explorer ordering and it is wrong here: the lead sentence promises a listing ordered by change frequency,
    /// and a busy file sitting under a quiet directory should not be pushed below it. The reader is looking for
    /// the active parts of the codebase, not for a filesystem browser.</para>
    ///
    /// <para>Applied by <see cref="CodeMapTemplater"/> at render time only. <see cref="CodeMap"/>'s own
    /// <c>BuildChildren</c> ordering (directories first, then alphabetical) is left untouched: it is the chart
    /// payload's byte-stability contract and is pinned by tests.</para></summary>
    internal static IEnumerable<CodeMapNode> OrderCodeMapLevel(IEnumerable<CodeMapNode> level) => level
        .OrderByDescending(SubtreeChanges)
        .ThenByDescending(n => n.Lines)
        .ThenBy(n => n.RepoRelativePath, StringComparer.OrdinalIgnoreCase);

    /// <summary>The set of file <see cref="CodeMapNode.RepoRelativePath"/>s that get the full rich tooltip card —
    /// every file when <paramref name="totalFileCount"/> is at or under <see cref="MaxDetailedCodeMapFiles"/>
    /// (returns <c>null</c>, the "no cap" sentinel so the byte-identical default-scale path skips the
    /// <see cref="OrderBySignificance"/> sort/take entirely — it still does one linear pass over <paramref
    /// name="files"/> to materialize the list), otherwise the top <see cref="MaxDetailedCodeMapFiles"/> by
    /// <see cref="OrderBySignificance"/>. <paramref name="totalFileCount"/> is passed explicitly (rather than
    /// inferred from <paramref name="files"/>'s own count) so the treemap and the file table always agree on
    /// WHETHER the cap trips even when their file lists come from different sources
    /// (<see cref="CodeMap.Layout"/> can omit a file nested past its own <c>MaxDepth</c>, while
    /// <see cref="CodeMap.Files"/> — the table's source — never does).</summary>
    internal static HashSet<string>? SelectDetailedCodeMapFiles(IReadOnlyList<CodeMapNode> files, int totalFileCount)
    {
        if (totalFileCount <= MaxDetailedCodeMapFiles) return null;
        return OrderBySignificance(files)
            .Take(MaxDetailedCodeMapFiles)
            .Select(f => f.RepoRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Builds the stylized HTML tooltip card for a treemap file rect (served through the shared body-level
    /// js-tip node via <c>data-tip-html</c>): a name heading, the monospaced repo path, and a definition list of every
    /// available metric (lines, changes, churn, average change size, files changed together, first/last change days) —
    /// each row present only when its metric exists. Dynamic parts are HTML-escaped here; the caller escapes the whole
    /// card ONCE more for the attribute so the tip node's getAttribute → innerHTML path round-trips it back to real
    /// markup. Because every metric is a labeled text row, color is never the sole signal for whichever dimension is
    /// active (AC #4). Single-quoted internal attributes keep only the dynamic content in need of escaping. [Story 7.6]</summary>
    /// <summary>The rich tooltip card's metric rows, folded into compact comma-joined PLAIN TEXT for a file's
    /// <c>aria-label</c> when it's past <see cref="MaxDetailedCodeMapFiles"/> and doesn't get the card itself —
    /// the same underlying data (<see cref="BuildTreemapCard"/>'s rows), just without the HTML/escaping cost.
    /// Type is included only when <paramref name="hasMetrics"/> (the base aria-label already carries it
    /// otherwise); every metric row is included only when present, mirroring the card's own per-row guards.
    /// Empty (no leading comma) when there is nothing to add beyond the base label.
    /// <para>Story 20.9 deleted the Code Map treemap that introduced this, and KEPT this: the Risk Quadrant's own
    /// past-the-cap branch calls it too, so it was never chart-exclusive. Confirmed by the compiler rather than
    /// assumed — which is the whole point of Task 4.4's keep-list.</para></summary>
    private static string CompactMetricsTail(CodeMapNode node, CodeFileCategory category, bool hasMetrics)
    {
        var parts = new List<string>();
        if (hasMetrics) parts.Add(category.Label);
        if (node.Metrics is { } m)
        {
            parts.Add($"{m.TotalChurn.ToString("N0", CultureInfo.InvariantCulture)} churn");
            if (m.Changes > 0)
            {
                var avg = (double)m.TotalChurn / m.Changes;
                parts.Add($"avg change size {avg.ToString("N0", CultureInfo.InvariantCulture)}");
            }
            if (m.AvgCoChanged is { } co)
            {
                parts.Add($"{co.ToString("0.#", CultureInfo.InvariantCulture)} files changed together");
            }
            if (m.FirstDate is { } fd && m.LastDate is { } ld)
            {
                parts.Add($"{DReadable(fd)} to {DReadable(ld)}");
            }
        }
        return parts.Count == 0 ? string.Empty : ", " + string.Join(", ", parts);
    }

    internal static string BuildTreemapCard(CodeMapNode node)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='codemap-card'>");
        sb.Append("<strong class='codemap-card-name'>").Append(Html(node.Label)).Append("</strong>");
        sb.Append("<code class='codemap-card-path'>").Append(Html(node.RepoRelativePath)).Append("</code>");
        sb.Append("<dl class='codemap-card-metrics'>");
        Row(sb, "Lines", node.Lines.ToString("N0", CultureInfo.InvariantCulture));
        // Always present — file type has no git dependency, unlike every row below (AC #1 "color is never the
        // sole signal": the categorical dimension gets a text row exactly like the sequential ones do). [Story 7.9]
        Row(sb, "Type", (node.Category ?? CodeFileType.Other).Label);
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

    // ---- Refactor-target risk quadrant (Story 7.10) ----------------------------------------

    /// <summary>Minimum number of metric-bearing (<c>Metrics is not null</c>) files required before the risk
    /// quadrant renders as a live chart (AC #2). Below this, a scatter of one or two points on live axes would
    /// overstate confidence, so the chart degrades to the shared empty-chart notice instead. There is no existing
    /// NFR8 "too few files to be meaningful" threshold anywhere else in this codebase to reuse (confirmed at
    /// create-story time) — this is this story's own small, locally-documented constant, not a shared one.</summary>
    public const int RiskQuadrantMinFiles = 6;

    /// <summary>One plotted file's precomputed geometry — shared by <see cref="RiskQuadrant"/> (the SVG) and
    /// <see cref="RiskQuadrantElevatedFiles"/> (the text-equivalent ranked list) so both derive from exactly the
    /// SAME median split and can never disagree about which files are flagged. <see cref="Changes"/> is kept as
    /// the raw (un-logged) count for ranking/display; <see cref="LogChanges"/> is the log-scaled plotting
    /// coordinate.</summary>
    private readonly record struct RiskPoint(CodeMapNode Node, double LogSize, double LogChanges, int Changes, bool Elevated);

    /// <summary>Computes the plotted points and their elevated-risk flag once: X = <c>Math.Log(Math.Max(Lines,
    /// 1))</c> (size, log-scaled — file sizes are heavy-tailed; the <c>Max(.., 1)</c> guard avoids
    /// <c>-Infinity</c>/NaN on a zero-line file), Y = <c>Math.Log(Math.Max(Metrics.Changes, 1))</c> (churn
    /// FREQUENCY, not <see cref="CodeFileMetrics.TotalChurn"/> volume — the AC asks for change-frequency
    /// specifically; also log-scaled — a real-repo pass showed churn is JUST as heavy-tailed as size, so a linear
    /// Y axis crushed nearly every point against the baseline and made the median cutoff line look arbitrary
    /// (review-pass owner feedback)). A file is "elevated risk" when it is strictly above BOTH axis medians (the
    /// high-size/high-churn quadrant, AC #1) — computed in the SAME log space the axes plot in, so the boundary
    /// and the visual spread of points agree. Pure function of already-computed <see cref="CodeMapNode"/> data —
    /// no new git call, no new parse (<c>CodeMap.Files()</c> is the one and only source for both axes).
    /// Deterministic: same input, same output, ordered by repo-relative path so point emission order never
    /// varies between runs.</summary>
    private static IReadOnlyList<RiskPoint> BuildRiskPoints(IReadOnlyList<CodeMapNode> files)
    {
        var plottable = files
            .Where(f => !f.IsDirectory && f.Metrics is not null)
            .OrderBy(f => f.RepoRelativePath, StringComparer.Ordinal)
            .ToList();
        if (plottable.Count == 0) return Array.Empty<RiskPoint>();

        var logSizes = plottable.Select(f => Math.Log(Math.Max(f.Lines, 1))).ToList();
        var logChanges = plottable.Select(f => Math.Log(Math.Max(f.Metrics!.Changes, 1))).ToList();
        var medianLogSize = Median(logSizes);
        var medianLogChanges = Median(logChanges);

        var points = new List<RiskPoint>(plottable.Count);
        for (var i = 0; i < plottable.Count; i++)
        {
            var elevated = logSizes[i] > medianLogSize && logChanges[i] > medianLogChanges;
            points.Add(new RiskPoint(plottable[i], logSizes[i], logChanges[i], plottable[i].Metrics!.Changes, elevated));
        }
        return points;
    }

    /// <summary>The sorted-list median (average of the two middle values on an even count) — adequate for this
    /// story's small in-memory per-file lists; no statistics package needed. Does not mutate the caller's list.</summary>
    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    /// <summary>The elevated-risk quadrant's files as a ranked list (busiest first) — the mandatory text
    /// equivalent of <see cref="RiskQuadrant"/>'s shaded quadrant (this project pairs every chart with a text
    /// equivalent, never an SVG-only signal). Below <see cref="RiskQuadrantMinFiles"/> metric-bearing files this
    /// returns empty, matching the chart's own below-threshold degrade — callers should render "no files flagged"
    /// copy for an empty result the same way they would for a genuinely empty risk quadrant, since a below-
    /// threshold repo has neither a chart nor a list to show. [Story 7.10 AC #1, AC #2]</summary>
    public static IReadOnlyList<CodeMapNode> RiskQuadrantElevatedFiles(IReadOnlyList<CodeMapNode> files)
    {
        var points = BuildRiskPoints(files);
        if (points.Count < RiskQuadrantMinFiles) return Array.Empty<CodeMapNode>();

        return points
            .Where(p => p.Elevated)
            .OrderByDescending(p => p.Changes)
            .ThenByDescending(p => p.Node.Lines)
            .ThenBy(p => p.Node.RepoRelativePath, StringComparer.Ordinal)
            .Select(p => p.Node)
            .ToList();
    }

    /// <summary>Renders the refactor-target risk quadrant as pure, server-computed SVG (Story 7.10): one point per
    /// metric-bearing source file, X = size (lines of code, log-scaled) and Y = churn frequency (commits touching
    /// the file, ALSO log-scaled — see <see cref="BuildRiskPoints"/>). Both axes carry real-unit tick labels at
    /// their extremes, and the two median cutoff lines carry their own real-unit label right where they meet the
    /// axis — a log-scaled/quadrant chart with unlabeled axes and an unlabeled cutoff line reads as arbitrary
    /// (review-pass owner feedback: "the Y axis feels unclear... not sure about the cutoff"). "Size" is a
    /// LINES-OF-CODE PROXY ONLY — this is not, and must never silently become, a
    /// cyclomatic-complexity analyzer; a real complexity metric is out of scope and would need its own story (AC
    /// #2). Both axes are median-split into four quadrants; the high-size/high-churn quadrant is flagged as
    /// elevated risk by BOTH a shaded background rect AND a distinguishing point class
    /// (<c>risk-point-elevated</c>) — never color alone, mirroring Story 7.8's shape+edge a11y discipline. Every
    /// point ALSO carries a <c>level-0..4</c> gradient class (the shared gold intensity ramp — Bucket — reused
    /// from a combined size+churn position) as an additional, non-load-bearing visual signal; the elevated flag
    /// remains the accessible, never-color-alone one. Points carry the SAME rich <c>data-tip-html</c> card the
    /// treemap's cells use (<see cref="BuildTreemapCard"/>) via the shared body-level tooltip, plus a plain-text
    /// <c>aria-label</c>. Points route to their in-portal code page only when the guarded
    /// <paramref name="fileHref"/> resolver (<c>CodeItemHref</c>, the Story 7.2 seam) returns a target;
    /// otherwise a plain, still-tooltipped, focusable point — never a dead link. Below
    /// <see cref="RiskQuadrantMinFiles"/> metric-bearing files, degrades to the shared
    /// <c>chart-empty</c> notice rather than plotting an axis of one or two dots (AC #2, NFR8). Deterministic:
    /// identical input always produces byte-identical output (no wall-clock, no randomness), so golden/parity
    /// fixtures stay stable (FR31). [Story 7.10]</summary>
    public static string RiskQuadrant(
        IReadOnlyList<CodeMapNode> files,
        int width = 640,
        int height = 420,
        Func<string, string?>? fileHref = null)
    {
        var points = BuildRiskPoints(files);
        if (points.Count < RiskQuadrantMinFiles)
        {
            return "<div class=\"chart-empty\">Not enough files with change history yet to plot a refactor-risk " +
                   $"quadrant (needs at least {RiskQuadrantMinFiles.ToString(CultureInfo.InvariantCulture)}).</div>\n";
        }

        // Raw (unpadded) extremes are kept separately from the plotting extremes below — tick labels must show
        // the TRUE min/max, never a value inflated by the degenerate-axis padding.
        var rawMinX = points.Min(p => p.LogSize);
        var rawMaxX = points.Max(p => p.LogSize);
        var rawMinY = points.Min(p => p.LogChanges);
        var rawMaxY = points.Max(p => p.LogChanges);
        var minX = rawMinX;
        var maxX = rawMaxX;
        var minY = rawMinY;
        var maxY = rawMaxY;
        // A degenerate axis (every file the exact same size or churn) still needs a non-zero span to place points
        // and the median split without dividing by zero.
        if (maxX <= minX) { minX -= 0.5; maxX += 0.5; }
        if (maxY <= minY) { minY -= 0.5; maxY += 0.5; }

        const double marginLeft = 64, marginBottom = 50, marginTop = 14, marginRight = 14;
        var plotW = width - marginLeft - marginRight;
        var plotH = height - marginTop - marginBottom;

        double PlotX(double x) => marginLeft + (x - minX) / (maxX - minX) * plotW;
        double PlotY(double y) => marginTop + plotH - (y - minY) / (maxY - minY) * plotH; // SVG Y grows downward
        // The log-scaled coordinate's real-world unit (whole lines / whole changes) — the tick/cutoff labels below.
        static string RealUnit(double logValue) => Math.Round(Math.Exp(logValue)).ToString("N0", CultureInfo.InvariantCulture);

        var medianLogSize = Median(points.Select(p => p.LogSize).ToList());
        var medianLogChanges = Median(points.Select(p => p.LogChanges).ToList());
        var quadX = PlotX(medianLogSize);
        var quadY = PlotY(medianLogChanges);

        var elevatedCount = points.Count(p => p.Elevated);
        var aria = $"Refactor-target risk quadrant: {points.Count} {Plural(points.Count, "file", "files")} " +
                   $"plotted by size and change frequency; {elevatedCount} {Plural(elevatedCount, "file", "files")} flagged as elevated risk.";

        // Same rich-card detail cap the sibling CodeTreemap uses (Story 7.9 review-fix) — past
        // MaxDetailedCodeMapFiles, BuildTreemapCard's doubly-escaped HTML `<dl>` is the single biggest per-point
        // cost and is skipped for the long tail (this is exactly what previously bloated code-map.html to ~82.5MB
        // before the cap existed). Every point still gets a real aria-label either way; long-tail points fold the
        // card's own metrics into that label as compact text instead (CompactMetricsTail), so nothing accessible
        // is lost, only the pretty popup. totalFileCount = points.Count since every plotted point here already
        // comes straight from the same list being capped (no treemap-style Layout()-vs-Files() mismatch).
        var detailedFiles = SelectDetailedCodeMapFiles(points.Select(p => p.Node).ToList(), points.Count);

        // Coincident points (identical Lines AND Changes — plausible once Math.Max(_, 1) floors near-zero values)
        // would otherwise render at the exact same (cx, cy) and fully occlude one another, leaving only the
        // last-drawn circle mouse/hover-reachable. Group by the shared plotting key and give same-key points a
        // small deterministic (path-order, not random) radial offset so every one stays independently reachable —
        // purely a rendering nicety, never applied to the underlying LogSize/LogChanges used for the median split
        // or the elevated flag. [Review][Patch]
        var coincidentGroups = new Dictionary<(double LogSize, double LogChanges), List<int>>();
        for (var i = 0; i < points.Count; i++)
        {
            var key = (points[i].LogSize, points[i].LogChanges);
            if (!coincidentGroups.TryGetValue(key, out var group)) coincidentGroups[key] = group = new List<int>();
            group.Add(i);
        }
        const double jitterRadius = 4.0;

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"risk-quadrant\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" role=\"img\" aria-label=\"{Html(aria)}\">\n");

        // Elevated-risk quadrant shading (top-right: high size, high churn) drawn FIRST so points render on top.
        // Always shaded + labeled even when currently empty — the median split itself doesn't move with the
        // contents of the quadrant, and an empty shaded region is still an honest "nothing here right now".
        var elevatedX = quadX;
        var elevatedY = marginTop;
        var elevatedW = marginLeft + plotW - quadX;
        var elevatedH = quadY - marginTop;
        if (elevatedW > 0 && elevatedH > 0)
        {
            sb.Append($"  <rect class=\"risk-quadrant-elevated\" x=\"{F(elevatedX)}\" y=\"{F(elevatedY)}\" width=\"{F(elevatedW)}\" height=\"{F(elevatedH)}\" aria-hidden=\"true\"></rect>\n");
            sb.Append($"  <text class=\"risk-quadrant-shade-label\" x=\"{F(elevatedX + elevatedW - 6)}\" y=\"{F(elevatedY + 14)}\" text-anchor=\"end\" aria-hidden=\"true\">Elevated risk</text>\n");
        }

        // Axis lines + median split lines (dashed, distinct class) so the quadrant boundary reads even where the
        // shading is very light.
        sb.Append($"  <line class=\"risk-axis-line\" x1=\"{F(marginLeft)}\" y1=\"{F(marginTop)}\" x2=\"{F(marginLeft)}\" y2=\"{F(marginTop + plotH)}\"></line>\n");
        sb.Append($"  <line class=\"risk-axis-line\" x1=\"{F(marginLeft)}\" y1=\"{F(marginTop + plotH)}\" x2=\"{F(marginLeft + plotW)}\" y2=\"{F(marginTop + plotH)}\"></line>\n");
        sb.Append($"  <line class=\"risk-median-line\" x1=\"{F(quadX)}\" y1=\"{F(marginTop)}\" x2=\"{F(quadX)}\" y2=\"{F(marginTop + plotH)}\"></line>\n");
        sb.Append($"  <line class=\"risk-median-line\" x1=\"{F(marginLeft)}\" y1=\"{F(quadY)}\" x2=\"{F(marginLeft + plotW)}\" y2=\"{F(quadY)}\"></line>\n");

        // Axis titles (Story 10.2-adjacent framing; the panel's own Why sentence is added by the caller via
        // Charts.Framed, not hand-rolled here).
        sb.Append($"  <text class=\"risk-axis-label\" x=\"{F(marginLeft + plotW / 2)}\" y=\"{F(height - 4)}\" text-anchor=\"middle\">Lines of code (log scale)</text>\n");
        sb.Append($"  <text class=\"risk-axis-label risk-axis-label-y\" x=\"12\" y=\"{F(marginTop + plotH / 2)}\" text-anchor=\"middle\" transform=\"rotate(-90 12 {F(marginTop + plotH / 2)})\">Changes in the analyzed window (log scale)</text>\n");

        // Real-unit tick labels at each axis's extremes — without these, a log-scaled axis gives no sense of
        // actual magnitude (review-pass owner feedback: "the Y axis feels unclear").
        sb.Append($"  <text class=\"risk-tick-label\" x=\"{F(marginLeft)}\" y=\"{F(marginTop + plotH + 16)}\" text-anchor=\"start\">{Html(RealUnit(rawMinX))}</text>\n");
        sb.Append($"  <text class=\"risk-tick-label\" x=\"{F(marginLeft + plotW)}\" y=\"{F(marginTop + plotH + 16)}\" text-anchor=\"end\">{Html(RealUnit(rawMaxX))}</text>\n");
        sb.Append($"  <text class=\"risk-tick-label\" x=\"{F(marginLeft - 6)}\" y=\"{F(marginTop + plotH + 3)}\" text-anchor=\"end\">{Html(RealUnit(rawMinY))}</text>\n");
        sb.Append($"  <text class=\"risk-tick-label\" x=\"{F(marginLeft - 6)}\" y=\"{F(marginTop + 8)}\" text-anchor=\"end\">{Html(RealUnit(rawMaxY))}</text>\n");

        // The two median cutoff lines get their OWN real-unit label right where they meet the axis — the direct
        // fix for "not sure about the cutoff": the dashed line is no longer an unlabeled, seemingly arbitrary
        // boundary, it reads as "median = N lines" / "median = N changes". Distinct (bold/rust) class from the
        // plain min/max ticks above so the cutoff value stands out as the one that actually matters.
        sb.Append($"  <text class=\"risk-median-tick-label\" x=\"{F(quadX)}\" y=\"{F(marginTop + plotH + 30)}\" text-anchor=\"middle\">median {Html(RealUnit(medianLogSize))}</text>\n");
        sb.Append($"  <text class=\"risk-median-tick-label\" x=\"{F(marginLeft - 6)}\" y=\"{F(quadY - 5)}\" text-anchor=\"end\">median {Html(RealUnit(medianLogChanges))}</text>\n");

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var node = point.Node;
            var cx = PlotX(point.LogSize);
            var cy = PlotY(point.LogChanges);

            // Deterministic jitter for exactly-coincident points (same LogSize+LogChanges) — see the
            // coincidentGroups comment above. Untouched (offset 0) for the overwhelmingly common non-colliding case.
            var group = coincidentGroups[(point.LogSize, point.LogChanges)];
            if (group.Count > 1)
            {
                var slot = group.IndexOf(i);
                var angle = 2 * Math.PI * slot / group.Count;
                cx += jitterRadius * Math.Cos(angle);
                cy += jitterRadius * Math.Sin(angle);
            }

            // Gradation (owner request, review pass): fill intensity reflects the file's COMBINED size+churn
            // position (0..1 average of its normalized X/Y), bucketed onto the SAME 5-level gold ramp the
            // treemap/heatmap already use (Bucket — the one shared "intensity" palette this project has; never a
            // new hue). This is purely an additional gradient signal — the elevated flag (shading + the
            // `risk-point-elevated` stroke below) stays the load-bearing, never-color-alone flag per Story 7.8's
            // shape+edge discipline; the gradient alone never carries the elevated/not-elevated distinction.
            var normX = (point.LogSize - minX) / (maxX - minX);
            var normY = (point.LogChanges - minY) / (maxY - minY);
            var level = Bucket((normX + normY) / 2.0, 1.0);
            var pointClass = point.Elevated ? $"risk-point level-{level} risk-point-elevated" : $"risk-point level-{level}";

            var lines = node.Lines.ToString("N0", CultureInfo.InvariantCulture);
            var changesStr = point.Changes.ToString("N0", CultureInfo.InvariantCulture);
            var ariaLabel = $"{node.RepoRelativePath}, {lines} {Plural((int)Math.Min(node.Lines, int.MaxValue), "line", "lines")}, " +
                            $"{changesStr} {Plural(point.Changes, "change", "changes")}";
            // Richer tooltip (owner request, review pass): the SAME stylized HTML card the treemap's cells use
            // (BuildTreemapCard — lines, type, changes, churn, avg change size, files changed together,
            // first/last change dates whenever each metric exists), served through the shared body-level js-tip
            // node exactly like the treemap, instead of a plain native <title>. Past MaxDetailedCodeMapFiles
            // (isDetailed=false), the card is skipped — same long-tail cap CodeTreemap uses — and its metrics fold
            // into the aria-label as compact text instead (CompactMetricsTail), so nothing accessible is lost.
            var isDetailed = detailedFiles is null || detailedFiles.Contains(node.RepoRelativePath);
            string tipAttrs;
            if (isDetailed)
            {
                var card = BuildTreemapCard(node);
                tipAttrs = $"aria-label=\"{Html(ariaLabel)}\" data-tip-html=\"{Html(card)}\"";
            }
            else
            {
                var category = node.Category ?? CodeFileType.Other;
                tipAttrs = $"aria-label=\"{Html(ariaLabel + CompactMetricsTail(node, category, hasMetrics: true))}\"";
            }

            var href = fileHref?.Invoke(node.RepoRelativePath);
            var linked = href is { Length: > 0 };
            if (linked)
            {
                var aClass = isDetailed ? "risk-point-link js-tip" : "risk-point-link";
                sb.Append($"  <a class=\"{aClass}\" href=\"{Html(href!)}\" {tipAttrs}>");
                sb.Append($"<circle class=\"{pointClass}\" cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"5\"></circle></a>\n");
            }
            else
            {
                var circleClass = isDetailed ? $"{pointClass} js-tip" : pointClass;
                sb.Append($"  <circle class=\"{circleClass}\" tabindex=\"0\" role=\"img\" cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"5\" {tipAttrs}></circle>\n");
            }
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    // ---- Code map change-frequency scale (the ramp legend's denominator) ------------------------------
    //
    // Story 20.9 deleted `CodeMapSunburst` and every piece of hand-rolled arc geometry with it. What survives here
    // is the SCALE, not the chart: the ramp legend still has to print real change-count ranges, and the client's
    // ramp dimension still has to bucket against the same denominator.

    /// <summary>Defensive recursion cap for the tree walk below — inherited from the retired sunburst's own guard
    /// so a pathological or cyclic tree can never hang generation (NFR2, never-throw).</summary>
    private const int CodeMapRecursionGuard = 256;

    /// <summary>Sums lines-of-code-weighted max change count across the whole tree — the SAME denominator
    /// <see cref="AppendTreemapFile"/>'s baked-in default (<see cref="Bucket"/> over <see cref="CodeFileMetrics.Changes"/>)
    /// uses for the treemap, computed once here so <see cref="CodeMapSunburst"/>'s wedges bucket identically.</summary>
    private static void CollectMaxChanges(IReadOnlyList<CodeMapNode> nodes, ref double maxChanges, int depth = 0)
    {
        if (depth > CodeMapRecursionGuard) return; // defensive depth cap (NFR2, never-throw)
        foreach (var node in nodes)
        {
            if (node.IsDirectory) { CollectMaxChanges(node.Children, ref maxChanges, depth + 1); continue; }
            if (node.Metrics is { } m && m.Changes > maxChanges) maxChanges = m.Changes;
        }
    }

    /// <summary>Public entry point onto <see cref="CollectMaxChanges"/> for callers outside <c>Charts</c> (the
    /// Code Map templater's real-value change-frequency legend) that need the SAME denominator the sunburst's
    /// wedges bucket against, without duplicating the tree walk. [Review 2026-07-22]</summary>
    internal static double ComputeMaxChanges(IReadOnlyList<CodeMapNode> roots)
    {
        double maxChanges = 0;
        CollectMaxChanges(roots, ref maxChanges);
        return maxChanges;
    }

    /// <summary>Real-value range text for the change-frequency ramp's level 1..4 swatches — derived from the
    /// EXACT SAME ratio-of-max thresholds <see cref="Bucket"/> applies when it colors a file wedge/rect, so the
    /// legend can never disagree with the color it explains. Distinct from <see cref="HeatLevelRange"/> (the
    /// commit-heatmap's own range function): that one floors a uniform/sparse history to level-1, which
    /// <see cref="Bucket"/> does not do (a lone max-value file there lands on level-4, since its ratio is 1.0).
    /// [Review 2026-07-22 — replaces the "Less … More" placeholder AC #1 forbids.]</summary>
    internal static string CodeMapChangeLevelRange(int level, double maxChanges)
    {
        if (level is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(level), level, "Change level must be 1..4.");
        var max = (int)Math.Round(maxChanges, MidpointRounding.AwayFromZero);
        if (max <= 0) return "—";

        var t1 = (int)Math.Floor(0.25 * max);
        var t2 = Math.Max(t1, (int)Math.Floor(0.5 * max));
        var t3 = Math.Max(t2, (int)Math.Floor(0.75 * max));

        return level switch
        {
            1 => FormatChangeRange(1, t1),
            2 => FormatChangeRange(t1 + 1, t2),
            3 => FormatChangeRange(t2 + 1, t3),
            4 => FormatChangeRange(t3 + 1, max, openEnded: true),
            _ => "—",
        };
    }

    /// <summary>True when no wedge/rect can ever render <paramref name="level"/> at this <paramref name="maxChanges"/>
    /// — used to skip duplicate "—" swatches in the legend. Mirrors <see cref="IsHeatLevelUnreachable"/>'s
    /// discipline for <see cref="CodeMapChangeLevelRange"/>'s own thresholds.</summary>
    internal static bool IsCodeMapChangeLevelUnreachable(int level, double maxChanges) =>
        CodeMapChangeLevelRange(level, maxChanges) == "—";

    private static string FormatChangeRange(int lo, int hi, bool openEnded = false)
    {
        if (lo > hi) return "—"; // collapsed unused bucket at low maxChanges
        var loText = lo.ToString(CultureInfo.InvariantCulture);
        if (lo == hi) return loText;
        if (openEnded) return loText + "+";
        return loText + "–" + hi.ToString(CultureInfo.InvariantCulture);
    }

    // ---- Code ownership / bus-factor: the per-file facts (Story 7.11) -----------------------
    //
    // Story 20.9 deleted the two SVG writers these fed and KEPT the facts, because they are what the chart, its
    // hover card and its text twin all read — one vocabulary rather than three. `BuildOwnershipDataAttrs`, the
    // attribute-STRING builder, went with the SVG: the payload reads BuildOwnerJson directly, so an escaped
    // attribute blob had no remaining consumer. (Story 20.9's Task 4.4 keep-list named it; the compiler and a
    // search agreed it was unreachable, and keeping an unreachable builder is not the same as keeping a helper.)

    /// <summary>A file leaf's ownership description — the SAME facts the payload, the hover card and the text
    /// twin all read, derived once so they cannot disagree. <see cref="Href"/> is the already-guarded resolved
    /// target (null when unresolved — never a dead link). <see cref="DominantName"/>/<see cref="SharePct"/>/
    /// <see cref="TotalContributors"/>/<see cref="LastDate"/> are null/0/unknown together exactly when the file
    /// carries no git contributor record at all. [Story 7.11; Story 20.9]</summary>
    internal readonly record struct OwnershipFileInfo(
        string LevelClass, string Title, string? Href,
        string? DominantName, int? SharePct, int TotalContributors, DateOnly? LastDate);

    /// <summary>Buckets a dominant-author commit share percentage (0–100, an inherently bounded real unit, unlike
    /// freshness's unbounded day-count) onto a fixed real-value 1–4 ramp — deliberately fixed cut points rather
    /// than a data-relative quartile split (<see cref="HeatThresholds"/>'s approach): a share percentage is
    /// already meaningful on its own scale, so "76–100%" means the same thing on every repo's chart, never a
    /// moving target. [Story 7.11]</summary>
    private static int OwnershipShareLevel(int sharePct) => sharePct switch
    {
        <= 25 => 1,
        <= 50 => 2,
        <= 75 => 3,
        _ => 4,
    };

    internal static OwnershipFileInfo DescribeOwnershipFile(CodeMapNode node, Func<string, string?>? fileHref)
    {
        var href = fileHref?.Invoke(node.RepoRelativePath);
        var resolvedHref = href is { Length: > 0 } ? href : null;
        var contributors = node.Metrics?.Contributors ?? Array.Empty<FileContributor>();

        if (node.Metrics is null || contributors.Count == 0)
        {
            var noneTitle = $"{node.RepoRelativePath} — no git history";
            return new OwnershipFileInfo("level-none", noneTitle, resolvedHref, null, null, 0, null);
        }

        var dominant = contributors[0];
        // Clamped to 100 (Review 2026-07-22): Commits/Changes come from two different accumulators (per-author
        // commit tallies vs. per-commit change tallies), so a dominant author's commit count can exceed the
        // file's own tracked change count in rare cases — without the clamp this prints ">100%" and still pins
        // to level-4 either way, so the clamp only fixes the displayed/embedded number, not the color.
        var sharePct = node.Metrics.Changes > 0
            ? Math.Min(100, (int)Math.Round(100.0 * dominant.Commits / node.Metrics.Changes, MidpointRounding.AwayFromZero))
            : 0;
        var level = OwnershipShareLevel(sharePct);
        var title = $"{node.RepoRelativePath} — {dominant.Name} {sharePct}% ({node.Metrics.TotalContributors} " +
                    $"{Plural(node.Metrics.TotalContributors, "contributor", "contributors")})";
        return new OwnershipFileInfo($"level-{level}", title, resolvedHref, dominant.Name, sharePct, node.Metrics.TotalContributors, node.Metrics.LastDate);
    }

    /// <summary>Hand-rolled compact JSON for a file's capped contributor list — small and fixed enough in shape
    /// (a flat array of 3-element tuples) that pulling in a general serializer would be pure overhead. Escapes
    /// only what JSON string literals require (<c>"</c>, <c>\</c>, control chars); the caller HTML-escapes the
    /// whole result afterwards for safe attribute embedding.</summary>
    /// <summary>The bounded top-author roster as compact JSON — the discrete PALETTE the "top contributors"
    /// dimension matches a file's dominant author against (<see cref="GitMetrics.BuildTopAuthors"/> bounds it to
    /// <see cref="OwnershipTopAuthorPaletteSize"/>). Extracted from <c>CodeOwnershipSunburst</c>'s own inline
    /// builder when Story 20.9 retired that method, so the roster still travels exactly one way; it is a colour
    /// palette, never a leaderboard (FR-10 / ADR 0010 §4).</summary>
    internal static string BuildTopAuthorsJson(IReadOnlyList<string> topAuthors)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < topAuthors.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonStringLiteral(topAuthors[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }

    internal static string BuildOwnerJson(IReadOnlyList<FileContributor> contributors)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < contributors.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var c = contributors[i];
            sb.Append('[').Append(JsonStringLiteral(c.Name)).Append(',').Append(c.Commits.ToString(CultureInfo.InvariantCulture));
            sb.Append(',').Append(c.LastCommitDate is { } d ? d.DayNumber.ToString(CultureInfo.InvariantCulture) : "null");
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string JsonStringLiteral(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>Bounded roster size for the discrete top-author PALETTE mode specifically (distinct from
    /// <see cref="GitMetrics.CodeMapFileContributorCap"/>, which bounds how many contributors show up per FILE —
    /// a different concern). Fixed at 7 to reuse the SAME 7-hue categorical palette Story 7.9's file-type legend
    /// already established (owner feedback: author colors must draw from the one discrete color scheme this
    /// codebase uses elsewhere, not a bespoke 12-hue set) — any author beyond the top 7 by total commits falls
    /// into the shared "Other" overflow bucket, exactly as file types beyond the classified set do.</summary>
    public const int OwnershipTopAuthorPaletteSize = 7;

    /// <summary>Builds the stylized HTML tooltip card for an ownership wedge/cell (owner feedback — richer
    /// hover info than the plain <c>&lt;title&gt;</c> it replaces), served through the SAME shared body-level
    /// js-tip node and <c>.codemap-card</c> class family <see cref="BuildTreemapCard"/> established (reused
    /// verbatim, exactly as <see cref="RiskQuadrant"/> already does for a third page — one card style, not a
    /// parallel one per component): name, path, dominant author + share, contributor count, last-active date,
    /// and the full per-author commit breakdown (already bounded at <see cref="GitMetrics.CodeMapFileContributorCap"/>).
    /// Dynamic parts are HTML-escaped here; the caller escapes the whole card once more for the attribute.</summary>
    internal static string BuildOwnershipCard(CodeMapNode node, OwnershipFileInfo info)
    {
        var contributors = node.Metrics?.Contributors ?? Array.Empty<FileContributor>();

        var sb = new StringBuilder();
        sb.Append("<div class='codemap-card'>");
        sb.Append("<strong class='codemap-card-name'>").Append(Html(node.Label)).Append("</strong>");
        sb.Append("<code class='codemap-card-path'>").Append(Html(node.RepoRelativePath)).Append("</code>");
        sb.Append("<dl class='codemap-card-metrics'>");
        if (contributors.Count == 0)
        {
            Row(sb, "Git history", "none");
        }
        else
        {
            Row(sb, "Dominant author", $"{info.DominantName} ({info.SharePct}%)");
            Row(sb, "Contributors", info.TotalContributors.ToString(CultureInfo.InvariantCulture));
            if (info.LastDate is { } d) Row(sb, "Last active", PortalDates.Day(d));
            Row(sb, "By commits", string.Join(", ", contributors.Select(c => $"{c.Name} {c.Commits}")));
        }
        sb.Append("</dl></div>");
        return sb.ToString();

        static void Row(StringBuilder sb, string label, string value) =>
            sb.Append("<div><dt>").Append(Html(label)).Append("</dt><dd>").Append(Html(value)).Append("</dd></div>");
    }

    /// <summary>The ownership sunburst/treemap's default-mode real-value legend (Story 10.2 AC — never
    /// "Less … More"): one swatch + its fixed share-percentage range per level, highest-concentration first, plus
    /// a trailing "no git history" swatch whenever at least one file has no contributor record. Degrades to a
    /// plain note (AC #2 graceful degradation) when NO file in the set carries any contributor data at all — the
    /// same real-value-legend discipline the Code Map's own change-frequency ramp legend follows
    /// (<see cref="CodeMapChangeLevelRange"/>). One of FOUR mode-specific legend blocks
    /// (alongside <see cref="OwnershipTopAuthorsLegend"/>/<see cref="OwnershipSpotlightLegend"/>/
    /// <see cref="OwnershipStalenessLegend"/>) — the live JS mode switcher shows exactly one at a time so the
    /// visible legend can never disagree with what the active mode actually colored (owner feedback: colors and
    /// legend must always match up). This is the only one visible without JS (the share-% default).</summary>
    public static string OwnershipLegend(IReadOnlyList<CodeMapNode> files, string attributes = "")
    {
        var withMetrics = files.Where(f => f.Metrics?.Contributors is { Count: > 0 }).ToList();
        if (withMetrics.Count == 0)
        {
            return "<p class=\"ownership-legend-empty\">Git contributor data is unavailable (run with <code>--deep-git</code> " +
                   "in a git repository to colorize by ownership) — every file renders neutral.</p>\n";
        }

        var hasUnmetriced = files.Count != withMetrics.Count;

        var sb = new StringBuilder();
        sb.Append($"<div class=\"ownership-legend ownership-legend-share\"{attributes}>");
        sb.Append("<span class=\"ownership-legend-dim\">Colorized by dominant-author commit share</span> ");
        (int Level, string Label)[] ranges = { (4, "76–100%"), (3, "51–75%"), (2, "26–50%"), (1, "0–25%") };
        foreach (var (level, label) in ranges)
        {
            sb.Append($"<span class=\"ownership-legend-swatch level-{level}\"></span>");
            sb.Append($"<span class=\"ownership-legend-label\">{label}</span> ");
        }
        if (hasUnmetriced)
        {
            sb.Append("<span class=\"ownership-legend-swatch level-none\"></span>");
            sb.Append("<span class=\"ownership-legend-label\">No git history</span>");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The top-contributors discrete-palette legend (JS-only mode): one named swatch per bounded
    /// top-author (<see cref="OwnershipTopAuthorPaletteSize"/>, the SAME 7-hue categorical palette Story 7.9's
    /// file-type legend uses), plus the shared "Other"/"No git history" swatches. Ships <c>hidden</c> — the mode
    /// selector that reaches this mode rides inside the Hierarchy Explorer component's own hidden control bar, and
    /// the component's dimension-driven <c>data-hierarchy-legend</c> swap (Story 20.9 Task 1.6) reveals exactly one
    /// of the four legend blocks per the active dimension. [Review][Patch: was stale, still named the retired
    /// <c>initOwnershipSunburst</c> renderer this legend no longer routes through.]</summary>
    public static string OwnershipTopAuthorsLegend(IReadOnlyList<string> topAuthors, string attributes = "")
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"ownership-legend ownership-legend-top\" hidden{attributes}>");
        sb.Append("<span class=\"ownership-legend-dim\">Colorized by dominant contributor</span> ");
        for (var i = 0; i < topAuthors.Count; i++)
        {
            sb.Append($"<span class=\"ownership-legend-swatch owner-author-{i}\"></span>");
            sb.Append($"<span class=\"ownership-legend-label\">{Html(topAuthors[i])}</span> ");
        }
        sb.Append("<span class=\"ownership-legend-swatch owner-author-other\"></span>");
        sb.Append("<span class=\"ownership-legend-label\">Other</span> ");
        sb.Append("<span class=\"ownership-legend-swatch level-none\"></span>");
        sb.Append("<span class=\"ownership-legend-label\">No git history</span>");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The individual-author-spotlight legend (JS-only mode): NOT a binary "has worked on this file"
    /// flag (owner feedback) — files the chosen contributor touched are colored on the SAME level-1..4 ramp
    /// share-% mode uses (reused, not a new gradient — mirrors this codebase's existing level-bucketing
    /// convention), by how recently THAT contributor last touched the file, using fixed real-unit day cutoffs
    /// (meaningful on their own scale, matching <see cref="OwnershipShareLevel"/>'s "never a moving target"
    /// reasoning — every repo's "≤30 days" means the same thing). Files they never touched stay the distinct
    /// muted <c>owner-spotlight-off</c> state — that's not a recency value, so it can't sit on the ramp. Ships
    /// <c>hidden</c>, one of <see cref="OwnershipLegend"/>'s four mode-specific siblings.</summary>
    public static string OwnershipSpotlightLegend(string attributes = "")
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"ownership-legend ownership-legend-spotlight\" hidden{attributes}>");
        sb.Append("<span class=\"ownership-legend-dim\">Colorized by how recently the chosen contributor last worked on each file</span> ");
        (int Level, string Label)[] ranges = { (4, "≤30 days ago"), (3, "31–90 days ago"), (2, "91–180 days ago"), (1, "180+ days ago") };
        foreach (var (level, label) in ranges)
        {
            sb.Append($"<span class=\"ownership-legend-swatch level-{level}\"></span>");
            sb.Append($"<span class=\"ownership-legend-label\">{label}</span> ");
        }
        sb.Append("<span class=\"ownership-legend-swatch level-none\"></span>");
        sb.Append("<span class=\"ownership-legend-label\">Worked on this file, but their last-touch date isn't recorded</span> ");
        sb.Append("<span class=\"ownership-legend-swatch owner-spotlight-off\"></span>");
        // Softened from "has not worked on this file" (Review 2026-07-22): a file with more contributors than the
        // per-file embedded cap could have a real, spotlighted contributor who simply isn't in THIS file's own
        // tracked list — the swatch means "not tracked here," not a proven "never touched," so the legend
        // shouldn't claim more than the data supports.
        sb.Append("<span class=\"ownership-legend-label\">Not among this file's tracked contributors</span>");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The staleness legend (JS-only mode): fresh (touched within the reader's chosen threshold, green)
    /// vs. stale (the file's own last-touch date is beyond it, dashed rust) vs. no git history — the "fresh"
    /// swatch this legend was previously missing entirely (owner feedback: the staleness mode's own green never
    /// appeared in any legend). Ships <c>hidden</c>, one of <see cref="OwnershipLegend"/>'s four mode-specific
    /// siblings.</summary>
    public static string OwnershipStalenessLegend(string attributes = "")
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"ownership-legend ownership-legend-staleness\" hidden{attributes}>");
        // "Colorized by the file's own last-touch date" (Review 2026-07-22, was "whether any current contributor
        // has touched the file recently") — the underlying data (data-last) is a whole-file date with no author
        // attached, so the previous wording claimed a per-contributor signal this mode doesn't actually carry.
        sb.Append("<span class=\"ownership-legend-dim\">Colorized by how recently the file was last touched, by anyone</span> ");
        sb.Append("<span class=\"ownership-legend-swatch owner-fresh\"></span>");
        sb.Append("<span class=\"ownership-legend-label\">Touched within the threshold</span> ");
        sb.Append("<span class=\"ownership-legend-swatch owner-stale\"></span>");
        sb.Append("<span class=\"ownership-legend-label\">Not touched within the threshold</span> ");
        sb.Append("<span class=\"ownership-legend-swatch level-none\"></span>");
        sb.Append("<span class=\"ownership-legend-label\">No git history</span>");
        sb.Append("</div>\n");
        return sb.ToString();
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
            $"{sat.Deferred} deferred on purpose, {sat.Unmapped} not yet mapped, {sat.Retired} retired");

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
        // Retired shares the deferred grey token (Story 8.9/D2: no 7th token) but is its own bracket/segment so
        // it is never silently folded into "deferred on purpose" — the two mean different things (shelved vs.
        // the covering plan was abandoned). [Story 8.9 review]
        AppendSatisfactionBracket(sb, "retired", sat.Retired, new[]
        {
            ("deferred retired", sat.Retired, "Retired"),
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

    /// <summary>Five-reading satisfaction chips (Satisfied · In flight · Deferred on purpose · Unmapped ·
    /// Retired). Each chip pairs color + icon + word (never color-only). Optional hrefs deep-link to on-page
    /// detail. In-flight tooltip expands Active/Ready/Planned. [Story 9.9; Retired added Story 8.9 review]</summary>
    public static string RequirementSatisfactionChips(
        ProjectCounts.RequirementSatisfaction sat,
        string? satisfiedHref = null,
        string? inFlightHref = null,
        string? deferredHref = null,
        string? unmappedHref = null,
        string? retiredHref = null,
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
        AppendSatisfactionChip(sb, "Retired", sat.Retired, "deferred", "deferred",
            StatusStyles.StageMeaning("retired"), Href(retiredHref),
            displayWord: StatusStyles.RequirementLabel(RequirementStatus.Retired));
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

    /// <summary>Max stories listed in a covered cell's tooltip before "+N more" — keeps a large epic's rollup
    /// readable rather than dumping its whole story list into one tooltip. [Story 21.1]</summary>
    private const int TraceabilityCellStoryCap = 8;

    /// <summary>The requirement × covering-epic traceability matrix (Story 21.1): rows are every requirement
    /// (<see cref="RequirementsModel.Everything"/> — FR + NFR + UX-DR, unlike <see cref="RequirementFlow"/>'s
    /// FR+NFR-only <c>All</c> scope), columns are the bounded set of covering epics (reusing
    /// <see cref="CoverageKeys"/>/<see cref="EpicTitlesByNumber"/>, the same epic-axis plumbing
    /// <see cref="RequirementFlow"/> already computes), filtered to epic numbers that actually resolve in
    /// <paramref name="epics"/> so a column never links to a page that doesn't exist. A cell is COVERED when the
    /// column epic is one of the row's <see cref="RequirementInfo.CoverageEpicNumbers"/> (a marker glyph + a rich
    /// tooltip naming the covering epic's own stories — "stories in the covering epic", never a per-requirement
    /// claim); otherwise blank (absence of coverage BY THIS EPIC is normal, not a gap). A deferred row carries the
    /// <c>deferred</c> treatment; a fully-unmapped row (zero covering epics, not deferred) carries
    /// <c>unmapped</c>/"Not yet mapped" — both via <see cref="StatusStyles.Badge(string,string,string)"/> next to
    /// the row header so the row reads even with every cell blank. A real HTML <c>&lt;table&gt;</c>
    /// (<c>caption</c> + <c>th scope</c>) is itself the text equivalent — no separate sr-only list needed. Pure
    /// generation-time HTML, no JS (memory: charting-is-pure-svg-no-js). Degrades to a <c>chart-empty</c> honest
    /// note when there is nothing to chart or no requirement names a resolvable covering epic (NFR8; AC #2).</summary>
    public static string TraceabilityMatrix(RequirementsModel reqs, EpicsModel epics, string prefix)
    {
        var all = reqs.Everything.ToList();
        var epicsByNumber = epics.Epics.ByFirst(e => e.Number);
        var epicKeys = CoverageKeys(all)
            .Where(k => k != NoCoverageKey && epicsByNumber.ContainsKey(k))
            .ToList();

        if (all.Count == 0 || epicKeys.Count == 0)
        {
            // Honest degrade — but keep the deferred/unmapped distinction the matrix exists to preserve: a project
            // that has deliberately deferred every requirement is NOT the same as one with an unmapped-coverage gap.
            var deferredCount = all.Count(r => r.Deferred);
            var note = all.Count > 0 && deferredCount == all.Count
                ? "Every requirement is deferred on purpose — none is currently tied to a delivering epic."
                : "Coverage not yet mapped — no requirement is yet tied to a delivering epic.";
            return $"<div class=\"chart-empty\">{note}</div>";
        }

        var epicTitleByNumber = EpicTitlesByNumber(epics);

        var sb = new StringBuilder();
        sb.Append("<div class=\"table-scroll trace-matrix-wrap\">\n");
        sb.Append("  <table class=\"trace-matrix\">\n");
        sb.Append("    <caption class=\"sr-only\">Requirement traceability matrix: rows are requirements, columns are the epics that could cover them; a filled cell means the epic covers the requirement.</caption>\n");
        sb.Append("    <thead>\n      <tr>\n        <th scope=\"col\" class=\"trace-corner\">Requirement</th>\n");
        foreach (var epicNumber in epicKeys)
        {
            var title = epicTitleByNumber.GetValueOrDefault(epicNumber, string.Empty);
            var tip = Html($"Epic {epicNumber} — {title}");
            sb.Append($"        <th scope=\"col\"><a class=\"js-tip\" href=\"{prefix}epics/epic-{epicNumber}.html\" data-tip=\"{tip}\" title=\"{tip}\">Epic {epicNumber}</a></th>\n");
        }
        sb.Append("      </tr>\n    </thead>\n    <tbody>\n");

        foreach (var req in all)
        {
            // A requirement can name covering epic(s) that no longer resolve (typo'd or since-removed number, e.g.
            // the "Epic 99" phantom shape) — Count > 0 yet no epicKeys column matches, so every cell would be blank.
            // Treat that as effectively unmapped for the ROW so it isn't a silent, unexplained blank row, and flag it
            // with a caution marker (below) that names the dangling epic. [Story 21.1 review — owner option 2]
            var resolvable = req.CoverageEpicNumbers.Any(epicsByNumber.ContainsKey);
            var rowClass = req.Deferred ? "deferred" : !resolvable ? "unmapped" : string.Empty;
            sb.Append(rowClass.Length > 0 ? $"      <tr class=\"{rowClass}\">\n" : "      <tr>\n");
            var kindLabel = req.Kind switch
            {
                RequirementKind.Functional => "Functional",
                RequirementKind.NonFunctional => "Non-functional",
                _ => "UX Design",
            };
            var snippet = Shorten(PathUtil.StripHtmlTags(req.TextHtml).Trim(), 96);
            var rowTip = Html($"{req.Id} · {kindLabel}\n{snippet}");
            sb.Append($"        <th scope=\"row\" class=\"trace-row-head\"><a class=\"js-tip\" href=\"{prefix}requirements/{req.Slug}.html\" data-tip=\"{rowTip}\" title=\"{rowTip}\">{Html(req.Id)}</a>");
            if (req.Deferred)
            {
                sb.Append(' ').Append(StatusStyles.Badge("deferred", "Deferred"));
            }
            else if (req.CoverageEpicNumbers.Count == 0)
            {
                sb.Append(' ').Append(StatusStyles.Badge("pending", "Not yet mapped", "unmapped"));
            }
            else if (!resolvable)
            {
                // Named a covering epic that no longer resolves — reuse the tan "pending" swatch (no 7th token)
                // but a caution glyph + distinct word so it never reads color-only and is legible next to the
                // genuine "Not yet mapped" rows. The tooltip names the dangling epic(s). [Story 21.1 review]
                var named = string.Join(", ", req.CoverageEpicNumbers.Select(n => $"Epic {n}"));
                var danglingTip = Html($"Names {named}, which no longer resolves to a known epic — coverage is dangling.");
                sb.Append(" <span class=\"status-badge pending js-tip\" data-tip=\"")
                  .Append(danglingTip).Append("\" title=\"").Append(danglingTip).Append("\">")
                  .Append(Icons.Caution()).Append("Coverage dangling</span>");
            }
            sb.Append("</th>\n");

            foreach (var epicNumber in epicKeys)
            {
                if (req.CoverageEpicNumbers.Contains(epicNumber))
                {
                    var epic = epicsByNumber[epicNumber];
                    var tip = Html(CoveredCellTip(req, epicNumber, epic));
                    sb.Append($"        <td class=\"trace-cell covered\"><a class=\"trace-cell-link js-tip\" href=\"{prefix}epics/epic-{epicNumber}.html\" data-tip=\"{tip}\" title=\"{tip}\">{Icons.ForStatus("done")}<span class=\"sr-only\">Covered by Epic {epicNumber}</span></a></td>\n");
                }
                else
                {
                    sb.Append("        <td class=\"trace-cell\"></td>\n");
                }
            }

            sb.Append("      </tr>\n");
        }

        sb.Append("    </tbody>\n  </table>\n</div>\n");
        return sb.ToString();
    }

    /// <summary>The covered-cell rich tooltip: the requirement/epic pair plus the covering epic's OWN story
    /// rollup — never a per-requirement mapping (the coverage map is epic-level; see <see cref="RequirementFlow"/>'s
    /// doc comment for the same caveat). Capped at <see cref="TraceabilityCellStoryCap"/> so a large epic's
    /// tooltip stays readable.</summary>
    private static string CoveredCellTip(RequirementInfo req, int epicNumber, EpicInfo epic)
    {
        var header = $"{req.Id} · Epic {epicNumber}\nStories in Epic {epicNumber} (epic-level coverage):";
        if (epic.Stories.Count == 0) return header + "\nNo stories drafted yet.";

        var lines = epic.Stories
            .Take(TraceabilityCellStoryCap)
            .Select(s => $"{s.Id} ({StatusStyles.StoryLabel(StatusStyles.ForStory(s))}) — {PathUtil.StripHtmlTags(s.Title)}");
        var body = string.Join("\n", lines);
        if (epic.Stories.Count > TraceabilityCellStoryCap)
        {
            body += $"\n+{epic.Stories.Count - TraceabilityCellStoryCap} more";
        }
        return header + "\n" + body;
    }

    /// <summary>The three chips (Covered / Deferred on purpose / Not yet mapped) shared by
    /// <see cref="TraceabilityLegend"/> and <see cref="TraceabilityStrip"/> — ONE source so the dedicated page's
    /// legend and the dashboard/requirements teaser can never disagree with each other or with the matrix's own
    /// cell classification (all three derive from the same <see cref="ProjectCounts.RequirementSatisfaction"/>
    /// ledger — no local recount, AC #2). Reuses <see cref="AppendSatisfactionChip"/>'s exact markup, collapsed
    /// from the four <see cref="RequirementSatisfactionChips"/> readings to three (Satisfied + In flight →
    /// Covered) since the matrix is literal 3-state by owner decision.</summary>
    private static string TraceabilityChips(ProjectCounts.RequirementSatisfaction sat)
    {
        // Retired folds into Covered here, not into Deferred: the matrix's "Covered" state means "a delivering
        // epic is named" (a real cell/link exists), which is still true for a retired-epic-covered requirement —
        // only the requirement's own DELIVERY progress (Satisfied/In flight) is what Retired says nothing
        // happened on. Keeping the traceability page's owner-locked 3-state shape (Story 21.1) rather than
        // adding a 4th chip. [Story 8.9 review]
        var covered = sat.Satisfied + sat.InFlight + sat.Retired;
        var sb = new StringBuilder();
        sb.Append("  <div class=\"satisfaction-chips trace-strip-chips\">\n");
        AppendSatisfactionChip(sb, "Covered", covered, "done", "done",
            "Has a delivering epic — done, in progress, or planned.", href: null);
        AppendSatisfactionChip(sb, "Deferred on purpose", sat.Deferred, "deferred", "deferred",
            StatusStyles.StageMeaning("deferred"), href: null);
        AppendSatisfactionChip(sb, "Not yet mapped", sat.Unmapped, "pending", "unmapped",
            StatusStyles.StageMeaning("unmapped"), href: null);
        sb.Append("  </div>\n");
        return sb.ToString();
    }

    /// <summary>The dedicated traceability page's own real-value 3-swatch legend (Story 10.2 AC1) — chart-
    /// intrinsic, prepended to <see cref="TraceabilityMatrix"/>'s body inside the same framed panel rather than a
    /// separate hand-rolled caption. Counts come from the ledger (AC #2), never a recount over the matrix's own
    /// rows. Empty ledger renders nothing (NFR8).</summary>
    public static string TraceabilityLegend(ProjectCounts.RequirementSatisfaction sat) =>
        sat.Total <= 0 ? string.Empty : $"<div class=\"trace-legend\">\n{TraceabilityChips(sat)}</div>\n";

    /// <summary>The compact 3-state coverage-strip teaser (Story 21.1 Task 4): the SAME three chips as
    /// <see cref="TraceabilityLegend"/> plus a link to the dedicated <c>traceability.html</c> page. Not a second
    /// full matrix — a teaser. Empty ledger renders nothing (NFR8).</summary>
    public static string TraceabilityStrip(ProjectCounts.RequirementSatisfaction sat, string traceabilityHref)
    {
        if (sat.Total <= 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"trace-strip\">\n");
        sb.Append(TraceabilityChips(sat));
        sb.Append($"  <a class=\"view-epic-link trace-strip-link\" href=\"{Html(traceabilityHref)}\">View full traceability matrix &rarr;</a>\n");
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
        ("retired", "retired"),
    };

    /// <summary>The requirements-flow's own terminal-state bucket key. Identical to
    /// <see cref="StatusStyles.ForRequirement"/> everywhere except it routes <see cref="RequirementStatus.Unmapped"/>
    /// to its own <c>unmapped</c> bucket instead of the shared tan <c>pending</c> class, and
    /// <see cref="RequirementStatus.Retired"/> to its own <c>retired</c> bucket instead of the shared grey
    /// <c>deferred</c> class — both the badge/card/grid/donut deliberately reuse those shared classes for. This is
    /// the ONE documented place the flow needs buckets distinct from <see cref="StatusStyles.ForRequirement"/> —
    /// AC #2 (Story 9.3) requires the deferred/unmapped/planned split visible in the diagram (and its aria twin)
    /// even though the badge color does not get a 7th token; Retired needs the identical treatment for the same
    /// reason (Story 8.9 review). Do not let this exception leak into any other consumer. [Story 9.3 Task 3]</summary>
    private static string FlowStateKey(RequirementInfo req) => req.Status switch
    {
        RequirementStatus.Unmapped => "unmapped",
        RequirementStatus.Retired => "retired",
        _ => StatusStyles.ForRequirement(req),
    };

    /// <summary>The requirements-flow's "No coverage" epic-coverage-key sentinel. Single source for
    /// <see cref="RequirementFlow"/> and <see cref="RequirementFlowTextEquivalent"/> so a future change to
    /// coverage semantics can't drift the diagram and its text twin apart. [Story 3.7 deferred-debt cleanup]</summary>
    private const int NoCoverageKey = -1;

    /// <summary>A requirement with no covering epic — routes to the flow's "No coverage" node/bucket. Single
    /// source for both <see cref="RequirementFlow"/> and <see cref="RequirementFlowTextEquivalent"/>.</summary>
    private static bool NoCoverage(RequirementInfo r) => r.CoverageEpicNumbers.Count == 0;

    /// <summary>Ordered epic-coverage keys for the flow's L1 column: covering epic numbers ascending, then
    /// <see cref="NoCoverageKey"/> last if any requirement has no covering epic. Single source so the diagram
    /// and its text-equivalent always partition requirements the same way.</summary>
    private static List<int> CoverageKeys(IReadOnlyList<RequirementInfo> all)
    {
        var keys = all.SelectMany(r => r.CoverageEpicNumbers).Distinct().OrderBy(k => k).ToList();
        if (all.Any(NoCoverage)) keys.Add(NoCoverageKey);
        return keys;
    }

    /// <summary>The requirements belonging to one coverage key (a covering epic number, or
    /// <see cref="NoCoverageKey"/>) — a multi-epic requirement belongs under every covering epic. Single source
    /// for both <see cref="RequirementFlow"/>'s per-node counts and <see cref="RequirementFlowTextEquivalent"/>'s
    /// per-epic membership.</summary>
    private static IEnumerable<RequirementInfo> ForCoverageKey(IEnumerable<RequirementInfo> all, int key) =>
        key == NoCoverageKey ? all.Where(NoCoverage) : all.Where(r => r.CoverageEpicNumbers.Contains(key));

    /// <summary>Epic titles by number, stripped of markup — the shared lookup behind both the flow diagram's
    /// node tooltips and its text-equivalent's epic labels.</summary>
    private static Dictionary<int, string> EpicTitlesByNumber(EpicsModel epics) =>
        epics.Epics.ByFirst(e => e.Number, e => PathUtil.StripHtmlTags(e.Title));

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

        // A requirement with no covering epic routes to the "No coverage" node (weight 1); otherwise its unit
        // weight splits evenly across its covering epics (1/k each). Deterministic and conserves to 1 per req.
        static double Weight(RequirementInfo r, int key) =>
            key == NoCoverageKey
                ? (NoCoverage(r) ? 1.0 : 0.0)
                : r.CoverageEpicNumbers.Contains(key) ? 1.0 / r.CoverageEpicNumbers.Count : 0.0;

        // Ordered L1 nodes: covering epics ascending, then the "No coverage" node last (if any req routes there).
        var l1Keys = CoverageKeys(all);
        var epicKeys = l1Keys.Where(k => k != NoCoverageKey).ToList();

        var epicTitleByNumber = EpicTitlesByNumber(epics);
        string L1Label(int key) => key == NoCoverageKey ? "No coverage" : $"Epic {key}";

        // L1 throughput = summed fractional weight (drives node height + ribbon thickness); L1 req count = the
        // number of DISTINCT requirements touching the node (the honest integer shown in the label/tooltip).
        double L1Throughput(int key) => all.Sum(r => Weight(r, key));
        int L1ReqCount(int key) => ForCoverageKey(all, key).Count();

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

        var maxNodes = Math.Max(1, Math.Max(l1Keys.Count, stateKeys.Count));
        var unitH = Math.Max(2.0, (usableH - gap * (maxNodes - 1)) / n);

        // Once `n` is large enough to hit unitH's 2px floor, a column's actual plotted height can exceed
        // usableH — grow the canvas to the tallest column instead of letting it overflow the fixed viewBox
        // (previously the SVG height stayed pinned at usableH regardless of n). [Story 3.7 follow-up]
        double ColumnHeight(int nodeCount, double weightSum) =>
            weightSum * unitH + gap * Math.Max(0, nodeCount - 1);
        var plotH = new[]
        {
            usableH,
            ColumnHeight(1, n),
            ColumnHeight(l1Keys.Count, l1Keys.Sum(L1Throughput)),
            ColumnHeight(stateKeys.Count, stateKeys.Sum(k => (double)stateCount[k])),
        }.Max();
        var height = topPad + plotH + 26;

        // Lay a column out: ordered node weights → their (top y, height), the whole stack vertically centered.
        (double Y, double H)[] LayoutColumn(IReadOnlyList<double> weights)
        {
            var totalH = weights.Sum() * unitH + gap * Math.Max(0, weights.Count - 1);
            var y = topPad + (plotH - totalH) / 2;
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
            var titleExtra = key == NoCoverageKey
                ? "deferred, unmapped, or non-functional"
                : epicTitleByNumber.TryGetValue(key, out var t) ? t : $"Epic {key}";

            // A multi-epic requirement is split across all its covering epics, so it appears in each epic node.
            // Note how many of this node's requirements are shared with another epic, so the split is legible.
            var shared = key == NoCoverageKey
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

    /// <summary>The per-epic × per-status breakdown a sighted user gets by hovering <see cref="RequirementFlow"/>'s
    /// epic-coverage/state ribbons, as a visually-hidden text list — the dashboard requirements panel previously
    /// exposed only the whole-diagram aria total and per-requirement tile tooltips, with no epic-level split for
    /// screen-reader users (unlike requirements.html's requirement cards, grouped by covering epic). Counts are
    /// the same DISTINCT-requirement integers behind each epic node's title (a multi-epic requirement is listed
    /// under every covering epic, not split fractionally). Empty string when there is nothing to chart.
    /// [Story 3.7 follow-up]</summary>
    public static string RequirementFlowTextEquivalent(RequirementsModel reqs, EpicsModel epics)
    {
        var all = reqs.All.ToList();
        if (all.Count == 0) return string.Empty;

        // Unlike RequirementFlow's own `epicKeys` (covering epics only, sentinel handled as a separate node),
        // this walks CoverageKeys' full result INCLUDING the trailing NoCoverageKey — the breakdown lists
        // "No coverage" as just another row.
        var coverageKeys = CoverageKeys(all);
        if (coverageKeys.Count == 0) return string.Empty;

        // Same epic-title lookup RequirementFlow's node tooltips use, so the sr-only reading names the epic
        // the way a sighted user sees it on hover, not just its bare number.
        var epicTitleByNumber = EpicTitlesByNumber(epics);
        string L1Label(int key) => key == NoCoverageKey
            ? "No coverage"
            : epicTitleByNumber.TryGetValue(key, out var title) ? $"Epic {key} ({title})" : $"Epic {key}";
        IEnumerable<RequirementInfo> ForKey(int key) => ForCoverageKey(all, key);

        var sb = new StringBuilder();
        sb.Append("<ul class=\"req-flow-breakdown sr-only\">\n");
        foreach (var key in coverageKeys)
        {
            var members = ForKey(key).ToList();
            // Canonical FlowStates order (done → deferred) so the reading matches the Sankey's state column order.
            var byState = FlowStates
                .Select(s => (s.Word, Count: members.Count(m => FlowStateKey(m) == s.Css)))
                .Where(t => t.Count > 0)
                .Select(t => $"{t.Count} {t.Word}");
            sb.Append($"  <li>{Html(L1Label(key))}: {members.Count} {Plural(members.Count, "requirement", "requirements")} — {Html(string.Join(", ", byState))}</li>\n");
        }
        sb.Append("</ul>\n");
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

    /// <summary>Inclusive upper bounds for heat levels 1/2/3 at a given <paramref name="maxCount"/> —
    /// the SINGLE source of truth shared by <see cref="HeatLevel"/> and <see cref="HeatLevelRange"/>.
    /// [Story 10.2]</summary>
    private static (int T1, int T2, int T3) HeatThresholds(int maxCount)
    {
        var t1 = Math.Max(1, (int)Math.Floor(0.25 * maxCount));
        var t2 = Math.Max(t1, (int)Math.Floor(0.5 * maxCount));
        var t3 = Math.Max(t2, (int)Math.Floor(0.75 * maxCount));
        return (t1, t2, t3);
    }

    /// <summary>True when no cell can ever render <paramref name="level"/> at this <paramref name="maxCount"/> \u2014
    /// used to skip duplicate "\u2014" swatches in the legend rather than showing several indistinguishable unused
    /// entries side by side. Level 0 is always reachable (it's the "no commits" bucket). [Story 10.2 review]</summary>
    private static bool IsHeatLevelUnreachable(int level, int maxCount)
    {
        if (level == 0) return false;
        if (maxCount <= 0) return true;
        if (maxCount == 1) return level != 1;

        var (t1, t2, t3) = HeatThresholds(maxCount);
        return level switch
        {
            2 => t1 + 1 > t2,
            3 => t2 + 1 > t3,
            _ => false,
        };
    }

    private static string FormatHeatRange(int lo, int hi, bool openEnded = false)
    {
        if (lo > hi) return "\u2014"; // collapsed unused bucket at low maxCount
        var loText = lo.ToString(CultureInfo.InvariantCulture);
        if (lo == hi) return loText;
        if (openEnded) return loText + "+";
        return loText + "\u2013" + hi.ToString(CultureInfo.InvariantCulture);
    }

    private static int HeatLevel(int count, int maxCount)
    {
        if (count <= 0) return 0;
        // Uniform single-commit history (busiest day is one commit, so maxCount == 1 and count is necessarily 1):
        // render light, not maxed. Relative scaling would otherwise paint a sparse one-commit-per-day project as
        // maximally busy, which the visual-truthfulness rule forbids; level 1 reads it as light activity. Repos
        // with a busier day (maxCount >= 2) fall through to the ratio buckets below. [heatmap-debt-triage]
        if (maxCount <= 1) return 1;
        var (t1, t2, t3) = HeatThresholds(maxCount);
        if (count <= t1) return 1;
        if (count <= t2) return 2;
        if (count <= t3) return 3;
        return 4;
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Grammatical pluralization for accessible names and count-bearing labels — a one-count value
    /// reads "1 story"/"1 commit", not "1 stories"/"1 commits". Shared with dashboard stat labels. [Story 1.4 AC #1, Story 1.5 A2]</summary>
    public static string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;

    /// <summary>Renders a <c>[0,1]</c> ratio as a whole-number percent label ("80%") — the ONE formatter for Story
    /// 24.1's directional coupling confidence, so the per-file text twin and the hub's table can never disagree
    /// about rounding. Invariant culture (matching every other numeric render here) so a comma-decimal locale can
    /// never emit "0,8". Rounds away from zero at the midpoint for stable, reproducible output, and clamps into
    /// <c>[0,1]</c> defensively so a malformed ratio degrades to a sane label instead of "-40%"/"250%".
    /// <para><b>Neither endpoint is allowed to lie</b> (Story 24.1 code review). Whole-percent rounding alone turned
    /// 0.996 into "100%" — the strongest claim this label can make, asserting a relationship is TOTAL when the data
    /// says otherwise — and 0.004 into "0%", printed beside a row that simultaneously asserts a couple exists. So a
    /// value that is genuinely 1.0 renders "100%" and nothing else may round up into it, and a value that is
    /// genuinely above 0 renders at least "&lt;1%" rather than collapsing to nothing.</para></summary>
    public static string Percent(double ratio)
    {
        if (double.IsNaN(ratio)) return "0%";
        var clamped = Math.Clamp(ratio, 0.0, 1.0);
        if (clamped >= 1.0) return "100%";
        if (clamped <= 0.0) return "0%";

        var whole = Math.Round(clamped * 100, MidpointRounding.AwayFromZero);
        if (whole >= 100) return "99%";
        if (whole <= 0) return "&lt;1%";
        return whole.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Formats Story 24.1's lift VALUE — the ONE place its rounding is decided, for exactly the reason
    /// <see cref="Percent"/> is the one place confidence's is. Before this existed the hub's table rendered lift
    /// through <c>F</c> ("0.##") while the per-file surfaces used "0.0", so a lift of 1.25 read "1.25&#215;" on one
    /// and "1.3&#215;" on the other — the precise disagreement the confidence formatter had been introduced to
    /// prevent, shipped for lift in the same change. Two decimals keep small differences visible (a 0.03 lift must
    /// not flatten to "0.0") while trailing zeros stay suppressed.
    /// <para>The multiplication sign is deliberately NOT included: HTML surfaces want the <c>&amp;#215;</c> entity
    /// (see <see cref="LiftLabel"/>) while the relationship graph's <c>Detail</c> is JSON consumed by script, where
    /// an entity would render literally. Sharing the number is what matters; the glyph is per-surface.</para>
    /// [Story 24.1 code review]</summary>
    public static string LiftNumber(double lift) => lift.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Lift as an HTML label ("1.25&#215;"): <see cref="LiftNumber"/> plus the escaped multiplication
    /// sign, for surfaces whose output is markup. [Story 24.1 code review]</summary>
    public static string LiftLabel(double lift) => LiftNumber(lift) + "&#215;";

    private static string Html(string s) => PathUtil.Html(s);
}
