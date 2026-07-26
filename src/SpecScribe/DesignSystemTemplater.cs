using System.Text;

namespace SpecScribe;

/// <summary>Renders the design-system reference page (<c>design-system.html</c>): the <c>--status-*</c> and
/// <c>--motion-*</c> token families and the shared visual primitives every surface is built from.
/// <para>
/// Two audiences, one page. A reader learns what a status colour <em>means</em> and how to read a framed
/// panel; a component author learns which token to reference. Both are served by the same content, so
/// splitting them would only create two documents free to disagree.
/// </para>
/// <para>
/// Built from the ACTUAL primitives — <see cref="StatusStyles.Badge"/>, <see cref="ListRow"/>,
/// <see cref="Charts.Framed"/> — never from look-alike markup. A gallery that mocked up its own badges could
/// drift from the real ones the moment either changed, and a design-system page that misrepresents the design
/// system is worse than none. For the same reason nothing here states a token's VALUE: each swatch shows its
/// colour by using <c>var(--status-*)</c> through the shared stylesheet, so the page cannot claim a value the
/// portal does not render.
/// </para>
/// Written on every full run so its Help-nav link never 404s, and — like About/How-to-read — written directly
/// rather than through <c>ApplyReferenceLinks</c>, so a page about the portal's vocabulary never self-expands
/// its own terms. [Story 23.2 AC #6; Help nav]
/// <para>
/// Deliberately simple markup: Story 23.4 retires the C# HTML renderer and this page is re-authored as the
/// Nuxt <c>/design-system</c> route (see <c>web/pages/design-system.vue</c>, which mirrors it). The owner
/// accepted that duplication to get the design system documented in the portal now; keeping the markup plain
/// is what keeps that removal cheap.
/// </para></summary>
public static class DesignSystemTemplater
{
    /// <summary>The motion vocabulary as roles rather than durations. Naming what each token is FOR is the
    /// durable half; the value belongs to the stylesheet and is deliberately not repeated here. [Story 3.5]</summary>
    private static readonly (string Token, string Role)[] MotionTokens =
    [
        ("--motion-fast", "Hover and opacity changes — the shortest deliberate movement on the page."),
        ("--motion-entrance", "The standard reveal, used by charts, panels and cards as they appear."),
        ("--motion-entrance-long", "Movement that travels a distance, such as a progress bar filling."),
        ("--motion-ease", "The single easing curve every entrance shares, so nothing feels out of place."),
        ("--motion-stagger", "The delay between items when a group enters one after another."),
    ];

    public static string RenderPage(SiteNav nav)
    {
        var outputPath = SiteNav.DesignSystemOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Design System — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            "The design system behind this portal: the status and motion token families, and the shared visual primitives every page is built from."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Design System", null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Design System</h1>\n");
        sb.Append("  <div class=\"doc-subtitle\">The visual vocabulary this portal is built from — what each status colour means, and which token to reach for when you build on it.</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                "Reading this page",
                Why: "A shared visual vocabulary is what lets a reader learn a page once and then recognise every other page in the portal."),
            IntroBody()));

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                "Status tokens",
                Ranking: "Every lifecycle stage the portal can show, in the order it teaches them.",
                Note: "Colour is always reinforcement, never the message. Every status also carries its word, so nothing here depends on distinguishing two shades.",
                Why: "One token per stage means a stage reads as the same colour on every chart, legend, badge and row — and changing it changes all of them at once."),
            StatusBody()));

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                "Motion tokens",
                Ranking: "Five named timings; no surface invents its own.",
                Note: "Every timing below is switched off for readers whose system asks for reduced motion (the prefers-reduced-motion setting). Motion never carries meaning on its own.",
                Why: "Naming the timings makes motion a vocabulary instead of a scattering of one-off numbers, so the whole portal accelerates and settles with one feel."),
            MotionBody()));

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                "Status badge",
                Ranking: "The same badge component every page uses, shown in each stage.",
                Why: "Because the badge always pairs a colour with a word and an icon, a status stays legible in greyscale, at a glance, and to a screen reader alike."),
            BadgeBody()));

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                "List row",
                Ranking: "Summary, status, metadata, and one primary link — the shape every index page shares.",
                Why: "One row anatomy across requirements, epics, decision records and timelines means a reader learns to scan once."),
            ListRowBody()));

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                "Framed panel",
                Window: "the panel you are reading",
                Ranking: "Title, analysis window, ranking caption, data note, body, and the framing sentence.",
                Note: "A note flags something about the DATA. The italic line below is the generic framing sentence. They sit in different slots because they answer different questions.",
                Why: "Framing every chart the same way means a reader never has to work out what they are looking at from the picture alone."),
            PanelBody()));

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string IntroBody()
    {
        var sb = new StringBuilder();
        sb.Append("<p>Everything in this portal is assembled from a small set of shared pieces: a palette of ");
        sb.Append("named colours for delivery status, a named set of timings for movement, and a handful of ");
        sb.Append("visual primitives. Each one is defined in exactly one place, so a change lands everywhere ");
        sb.Append("at once and no two pages can drift apart.</p>\n");
        sb.Append("<p>If you are reading the portal, the sections below tell you what each status colour ");
        sb.Append("means. If you are building on it, they name the token to reference — the values themselves ");
        sb.Append("live in the stylesheet, and are shown here only by being used.</p>\n");
        return sb.ToString();
    }

    private static string StatusBody()
    {
        var sb = new StringBuilder();
        sb.Append("<ul class=\"status-legend-key-list\">\n");
        foreach (var stage in StatusStyles.LegendStages)
        {
            // Two stages have no token of their own, and saying so is part of the documentation: "unmapped"
            // borrows the pending swatch (it is a requirement-level state, not a seventh lifecycle stage) and
            // "retired" borrows deferred's. Both stay distinct by word and icon, which is the point — the
            // vocabulary is carried by language, and colour is only ever shorthand for it.
            var (swatchClass, tokenNote) = stage switch
            {
                "unmapped" => ("pending", "shares <code>--status-pending</code>"),
                "retired" => ("deferred", "shares <code>--status-deferred</code>"),
                _ => (stage, $"<code>--status-{stage}</code>"),
            };

            sb.Append("  <li class=\"status-legend-key-row\">\n");
            sb.Append($"    <span class=\"status-legend-key-swatch {swatchClass}\" aria-hidden=\"true\"></span>\n");
            sb.Append($"    <span class=\"status-legend-key-label\">{StatusStyles.Badge(swatchClass, StatusStyles.LegendWord(stage), stage)}</span>\n");
            sb.Append($"    <span class=\"status-legend-key-meaning\">{PathUtil.Html(StatusStyles.StageMeaning(stage))} &middot; {tokenNote}</span>\n");
            sb.Append("  </li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string MotionBody()
    {
        var sb = new StringBuilder();
        sb.Append("<dl class=\"howtoread-glossary\">\n");
        foreach (var (token, role) in MotionTokens)
        {
            sb.Append($"  <div class=\"cap-row\"><dt><code>{PathUtil.Html(token)}</code></dt><dd>{PathUtil.Html(role)}</dd></div>\n");
        }
        sb.Append("</dl>\n");
        return sb.ToString();
    }

    private static string BadgeBody()
    {
        var sb = new StringBuilder();
        sb.Append("<p class=\"chart-lead\">Hover or focus any badge for its meaning.</p>\n");
        sb.Append("<div class=\"story-status-pair\">\n");
        foreach (var stage in StatusStyles.LegendStages)
        {
            var swatchClass = stage == "unmapped" ? "pending" : stage == "retired" ? "deferred" : stage;
            sb.Append($"  {StatusStyles.Badge(swatchClass, StatusStyles.LegendWord(stage), stage)}\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    private static string ListRowBody()
    {
        var sb = new StringBuilder();
        sb.Append("<ul class=\"list-rows-list\">\n");

        ListRow.Render(sb, "A row with nothing but a summary.", null, Array.Empty<string>(), null);

        ListRow.Render(
            sb,
            "A row carrying a status, two metadata chips, and its one primary link.",
            StatusStyles.Badge("review", StatusStyles.LegendWord("review")),
            new[] { ListRow.Chip("Epic 1"), ListRow.Chip("3 tasks") },
            ListRow.PrimaryLink(SiteNav.HomeOutputPath, "Open"));

        ListRow.Render(
            sb,
            "A deferred row. The left edge reinforces the badge — it never replaces it.",
            StatusStyles.Badge("deferred", StatusStyles.LegendWord("deferred")),
            Array.Empty<string>(),
            null,
            extraRowClass: "list-row-accent-deferred");

        ListRow.Render(
            sb,
            "A resolved row steps back without disappearing.",
            null,
            new[] { ListRow.Chip("closed") },
            null,
            resolved: true);

        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string PanelBody()
    {
        var sb = new StringBuilder();
        sb.Append("<p>Every section on this page is a framed panel, including this one. The frame is filled ");
        sb.Append("from one shared definition, so a panel cannot grow a heading its neighbours do not have — ");
        sb.Append("and a slot with nothing to say renders nothing at all, rather than an empty heading.</p>\n");
        sb.Append("<p>Charts add one more guarantee on top of the frame: each carries a text equivalent, so ");
        sb.Append("the information is available whether or not the picture is.</p>\n");
        return sb.ToString();
    }
}
