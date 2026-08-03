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
    /// durable half; the value belongs to the stylesheet and is deliberately not repeated here. [Story 3.5]
    /// <para>Internal rather than private so <c>SiteGeneratorDesignSystemTests</c> can assert against THIS
    /// list instead of a hand-typed copy of it. The status half of the page derives from
    /// <see cref="StatusStyles.LegendStages"/> and so cannot fall behind; this half could, and a test that
    /// re-listed the same five names could not detect it. [Story 23.2 re-review 2026-07-28]</para></summary>
    internal static readonly (string Token, string Role)[] MotionTokens =
    [
        ("--motion-fast", "Hover and opacity changes — the shortest deliberate movement on the page."),
        ("--motion-entrance", "The standard reveal, used by charts, panels and cards as they appear."),
        ("--motion-entrance-long", "Movement that travels a distance, such as a progress bar filling."),
        ("--motion-ease", "The single easing curve every entrance shares, so nothing feels out of place."),
        ("--motion-stagger", "The delay between items when a group enters one after another."),
    ];

    /// <summary>Stage → the paired FILL token, for the stages that have one.
    ///
    /// <para>Each <c>--status-*</c> token is a stage's ACCENT (border + text). Four stages also sit on a pale
    /// fill, and until Story 23.2's re-review those fills were inline hexes on
    /// <c>.status-badge.&lt;stage&gt;</c> — invisible to the token bridge, so the Vue counterpart substituted
    /// one flat parchment for all four and the two design systems disagreed about the colours they both exist
    /// to document. Naming the fill here is what makes the pair discoverable to a component author.</para>
    ///
    /// <para><c>ready</c> and <c>drafted</c> deliberately map to the SAME fill, exactly as the stylesheet
    /// pairs them: one visual tier, separated by word and glyph. Internal rather than private for the same
    /// reason as <see cref="MotionTokens"/> — a test asserts against THIS map, never a re-typed copy. Stages
    /// absent from it have no fill of their own and the page says nothing about one.
    /// [Story 23.2 re-review 2026-07-28]</para></summary>
    internal static readonly IReadOnlyDictionary<string, string> StageFillTokens =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["done"] = "--status-done-bg",
            ["active"] = "--status-active-bg",
            ["review"] = "--status-review-bg",
            ["ready"] = "--status-ready-bg",
            ["drafted"] = "--status-ready-bg",
        };

    /// <summary>Builds this page's host-neutral <see cref="PageView"/> — see
    /// <see cref="HowToReadTemplater.BuildPage"/> for why the body starts at the doc-header, not the landmark.
    /// [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(SiteNav nav)
    {
        var outputPath = SiteNav.DesignSystemOutputPath;

        var sb = new StringBuilder();
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
                // No Window. `Charts.ChartMeta` documents the slot as "the ONE place a NUMERIC analysis window
                // is rendered"; this page passed prose into it and the Vue twin passed component filenames — on
                // the two pages whose stated job is to teach the frame, which is where a 23.3 author copies the
                // pattern from. Leaving it empty also demonstrates the slot's own contract: unfilled renders
                // nothing. [Story 23.2 re-review 2026-07-28]
                Ranking: "Title, analysis window, ranking caption, data note, body, and the framing sentence.",
                Note: "A note flags something about the DATA. The italic line below is the generic framing sentence. They sit in different slots because they answer different questions.",
                Why: "Framing every chart the same way means a reader never has to work out what they are looking at from the picture alone."),
            PanelBody()));

        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.About,
            OutputRelativePath = outputPath,
            Title = $"Design System — {nav.SiteTitle}",
            MetaDescription = "The design system behind this portal: the status and motion token families, and the shared visual primitives every page is built from.",
            Nav = nav.ToNavigationView(outputPath),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", SiteNav.HomeOutputPath),
                ("Design System", null),
            }),
            Assets = new AssetManifest
            {
                StylesheetHref = ForgeOptions.StylesheetName,
                ScriptHref = ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
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
            // "retired" borrows deferred's. `unmapped` stays distinct by word AND icon; `retired` stays distinct
            // by WORD ONLY — `Icons.ForStatus("retired")` and `("deferred")` emit byte-identical SVG, so claiming
            // "both, by word and icon" was untrue, and this page must not misstate its own subject. The
            // vocabulary is carried by language, and colour is only ever shorthand for it.
            var (swatchClass, tokenNote) = stage switch
            {
                "unmapped" => ("pending", "shares <code>--status-pending</code>"),
                "retired" => ("deferred", "shares <code>--status-deferred</code>"),
                _ => (stage, $"<code>--status-{stage}</code>"),
            };

            // The accent above is half the pair. Four stages also sit on a pale fill, and a component author
            // binding only the accent gets a badge with the right border on the wrong background — which is
            // precisely what happened to StatusBadge.vue. Named, never valued. [Story 23.2 re-review]
            if (StageFillTokens.TryGetValue(stage, out var fill))
            {
                var shared = stage is "ready" or "drafted" ? ", shared with ready/drafted" : string.Empty;
                tokenNote += $" on <code>{fill}</code>{shared}";
            }

            sb.Append("  <li class=\"status-legend-key-row\">\n");
            sb.Append($"    <span class=\"status-legend-key-swatch {swatchClass}\" aria-hidden=\"true\"></span>\n");
            // Swatch borrows; badge does not (see BadgeBody). Only `unmapped` remaps its colour class, exactly
            // as `StatusStyles.LegendKey` does — `retired` carries `.status-badge.retired`, its own rule.
            var badgeClass = stage == "unmapped" ? "pending" : stage;
            sb.Append($"    <span class=\"status-legend-key-label\">{StatusStyles.Badge(badgeClass, StatusStyles.LegendWord(stage), stage)}</span>\n");
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
            // The SWATCH borrows a colour; the BADGE does not. `.status-badge.retired` is its own rule in
            // specscribe.css, and `StatusStyles.LegendKey` — the real caller — remaps only `unmapped`. Passing
            // the remapped class emitted `class="status-badge deferred"`, which no production caller emits, on
            // the page whose load-bearing claim is "built from the ACTUAL primitives, never look-alike markup".
            // Byte-identical to `.deferred` today, so nothing looked wrong; wrong by construction all the same.
            // [Story 23.2 re-review 2026-07-28]
            var badgeClass = stage == "unmapped" ? "pending" : stage;
            sb.Append($"  {StatusStyles.Badge(badgeClass, StatusStyles.LegendWord(stage), stage)}\n");
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
