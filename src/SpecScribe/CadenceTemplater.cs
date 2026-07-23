using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>cadence.html</c> page — delivery cadence (Story 21.2): a story-completion
/// calendar heatmap (<see cref="Charts.DeliveryCadenceHeatmap"/>) over the days stories reached done, plus a
/// cycle-time distribution histogram (<see cref="Charts.CycleTimeHistogram"/>) where first-touch → done is
/// derivable. Both framed with the mandatory Story 10.2 metadata; the cycle-time frame carries the "approximate —
/// story-file age, not a tracked workflow timestamp" honesty caveat (AC #2). Mirrors the synthesized-page shell
/// every standalone Delivery/Insights page uses (<see cref="TraceabilityTemplater"/> is the freshest precedent)
/// rather than <see cref="HtmlTemplater.RenderPage"/>. Degrades honestly: with no done stories / no derivable
/// dates each chart shows its own empty state — the page is still written (shared <c>hasEpics</c> gate). [Story 21.2]</summary>
public static class CadenceTemplater
{
    /// <summary>Renders the whole page. <paramref name="today"/> is the single per-run "today" (FR31) used to bound
    /// the heatmap grid and the strip's recent-window so a from-scratch regen on unchanged inputs is byte-identical.
    /// <paramref name="data"/> is <see cref="DeliveryCadenceData.Empty"/> (not null) when there is simply nothing to
    /// chart yet.</summary>
    public static string RenderPage(DeliveryCadenceData data, SiteNav nav, DateOnly today)
    {
        var outputPath = SiteNav.CadenceOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — cadence.html is at the output root.

        // Every done story has a page (its artifact page, or the always-generated placeholder), so this resolver
        // never returns a dead link. Same convention every Delivery surface uses. [[epic-7-code-link-strategy]]
        Func<StoryInfo, string?> storyHref = s => prefix + (s.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(s.Id));

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Delivery Cadence — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Delivery cadence for {nav.SiteTitle} — when stories reached done over time, plus story cycle-time where it can be derived from git history."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildDeliveryLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Delivery Cadence", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Delivery Cadence</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; how story completions have flowed over time</p>\n\n");

        // A short orientation lede (owner feedback): what's measured, how to read it, and links out to the ideas
        // behind it. Framework-neutral (NFR8) — never names a specific repo. External references open in a new tab.
        sb.Append("<p class=\"doc-lede cadence-lede\">This page tracks <strong>when stories reach done</strong> — the project&rsquo;s delivery rhythm — and, where git history allows, <strong>how long each story took</strong> from its first commit to done. A regular cadence and short, consistent cycle-times generally point to healthy flow; long quiet gaps, or cycle-times that stretch out and vary widely, can signal work piling up or stories that stall. Read these as directional signals, not targets. More on the ideas behind them: "
            + "<a href=\"https://en.wikipedia.org/wiki/Lead_time\" target=\"_blank\" rel=\"noopener noreferrer\">lead &amp; cycle time</a> and "
            + "<a href=\"https://en.wikipedia.org/wiki/Little%27s_law\" target=\"_blank\" rel=\"noopener noreferrer\">Little&rsquo;s Law</a>.</p>\n\n");

        // Completion cadence — framed with the metric-generic why sentence; the heatmap builder emits its own
        // window + real-value legend + text-equivalent completion log.
        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Story Completion Cadence",
                Why: Charts.WhyText(Charts.ChartMetric.DeliveryCadence)),
            Charts.DeliveryCadenceHeatmap(data.CompletionSeries, data.CompletionsByDay, storyHref, today)));

        // Cycle-time distribution — its "window" is the set of stories it covers; the honesty caveat lives in the
        // Note slot (visible, not buried). Bucket edges are stated in the Ranking caption so the reader knows them.
        var cycleCount = data.CycleTimes.Count;
        var cycleWindow = cycleCount > 0
            ? $"{cycleCount.ToString("N0", CultureInfo.InvariantCulture)} completed {Charts.Plural(cycleCount, "story", "stories")} with a derivable cycle-time"
            : null;
        var cycleRanking = cycleCount > 0
            ? "Buckets: 0–3, 4–7, 8–14, 15–30, and 30+ days from first touch to done"
            : null;
        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Story Cycle-Time",
                Window: cycleWindow,
                Ranking: cycleRanking,
                Note: "Approximate: cycle-time here measures each story file's age in git — its first commit to its last touch while done — not a tracked ready→done workflow timestamp, so a story seeded early or reworked after first reaching done can over- or under-state the real effort.",
                Why: Charts.WhyText(Charts.ChartMetric.DeliveryCadence)),
            Charts.CycleTimeHistogram(data.CycleTimes)));

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
