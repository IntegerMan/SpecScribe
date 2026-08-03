using System.Text;

namespace SpecScribe;

/// <summary>The shared composer of a page's NAVIGATION AND WAYFINDING MARKUP — the parts of the chrome that live
/// INSIDE the content region, and so belong to every surface rather than to one of them.
///
/// <para><b>[Story 23.6 AC #1] This is no longer an <see cref="IRenderAdapter"/>.</b> It was the FIRST and only
/// concrete one: it turned a <see cref="PageView"/> into a whole HTML document. That method is gone (see the
/// tombstone below) and with it the last C# code path that emits a page, so this type no longer implements the
/// DELIVERY seam at all — it is a helper the two surviving delivery adapters
/// (<see cref="JsonSpaRenderAdapter"/> for the IR and the SPA, <see cref="WebviewRenderAdapter"/> for the panel)
/// both compose with. Keeping the interface would have claimed a capability the class no longer has.</para>
///
/// <para>What survives is still a mechanical re-homing of the string-building that used to live across the
/// templaters and <see cref="SiteNav"/>, not a rewrite: <see cref="RenderNavMarkup"/> /
/// <see cref="RenderBreadcrumb"/> hold the verbatim strings, and <see cref="SiteNav.RenderNavBar"/> /
/// <see cref="SiteNav.RenderBreadcrumb"/> delegate here so there is one definition of each. The gate is
/// <c>npm run check:parity</c>'s <c>mainSha</c>, which carries the C# lineage for this markup across a frozen
/// 24-route corpus (ADR 0033). [Story 6.1, Story 23.6]</para></summary>
public sealed partial class HtmlRenderAdapter
{
    /// <summary>The single shared instance — the composer is stateless, so <see cref="SiteNav"/>'s delegating
    /// chrome helpers and the templaters reuse one instance rather than allocating per page.</summary>
    public static readonly HtmlRenderAdapter Shared = new();

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════════
    // `Render` was DELETED here by Story 23.6 (AC #1). NOTHING IN C# COMPOSES A WHOLE PAGE ANY MORE.
    //
    // WHAT IT DID. head open + nav + wayfinding + the two anti-flash handshakes + the opaque body + the TOC
    // tracker + footer + the charting engine + the mermaid init + close. It was the last writer of SpecScribe's
    // HTML; Nuxt renders every page from the IR now (ADR 0022 §Decision 3, ADR 0034).
    //
    // ⚠️ WHERE ITS CHROME WENT — the part that does NOT survive by itself. Six things this method emitted live
    // outside any page's content region, so the IR did not carry them and nothing else produced them:
    //
    //   `<title>` / `<meta name="description">` ...... the IR's per-page `head` projection (Story 22.2)
    //   favicon, the `?v=` asset cache-bust ......... the IR's site-level `chrome` block (Story 23.6)
    //   HierarchyExplorer.BootScript ................ ditto, keyed on `chromeNeeds().needsHierarchyEngine`
    //   RelationshipGraph.BootScript ................ ditto, keyed on `needsGraphEngine`
    //   Toc.ActiveSectionScript ..................... ditto, keyed on `needsToc`
    //   Mermaid.InitScript() ........................ ditto, keyed on `needsMermaid`
    //   plotly-hierarchy.min.js `<script src>` ...... emitted by the renderer on `hierarchy || graph` (ADR 0030)
    //
    // Three of those seven were ALREADY MISSING from the rendered portal before this deletion — the renderer had
    // no mermaid, graph or TOC handling at all, so from the moment Task 3 made the prerender the writer, every
    // mermaid diagram was an inert `<pre>` block and every relationship graph shipped without its engine. Found
    // by auditing this method line by line against `web/components/surfaces/IrSurface.vue`, not by any gate:
    // `check:parity`'s `pageSha` is a renderer SNAPSHOT, so it had pinned the broken output as correct.
    //
    // `RenderNavMarkup`, `RenderBreadcrumb`, `RenderWayfinding`, `RenderDashboardBody` and `RenderEpicsBody`
    // survive below — they compose the REGION, which is exactly what the IR carries and what the webview and
    // SPA consume through the one seam ADR 0024 defines.
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Renders the site nav bar from a <see cref="NavigationView"/>. The verbatim string-building that
    /// used to live on <see cref="SiteNav.RenderNavBar"/>, re-homed here behind the render adapter — the icon key
    /// now comes from <see cref="NavItem.ConceptKey"/> rather than reusing the label. Output is unchanged. [Story 6.1]</summary>
    public string RenderNav(NavigationView nav) => RenderNavMarkup(nav) + NavToggleScript;

    /// <summary>The nav bar's MARKUP alone — <see cref="RenderNav"/> minus the trailing inline toggle script.
    /// Split out (a pure mechanical extraction; <see cref="RenderNav"/>'s concatenation is byte-identical) so the
    /// <see cref="WebviewRenderAdapter"/> can reuse the exact nav element under the webview's strict
    /// Content-Security-Policy, where a non-nonce'd inline script would simply be blocked: the webview's own
    /// nonce'd bridge script owns the toggle behavior there instead. [Story 6.4]</summary>
    /// <remarks>The Scribe's Nib path data (24×24 viewBox): a filled nib silhouette with the vent hole and tip
    /// slit as <c>evenodd</c> cutouts, sized so the cutouts survive ~14px header rendering (slit 2.2 units wide,
    /// vent r 2.1). The extension's <c>media/specscribe-outline.svg</c> carries THIS SAME geometry and
    /// <c>media/specscribe.svg</c> a 16-box scaled variant — keep the three in step when the mark changes
    /// (no build-step sync exists yet; see deferred-work). [spec-scribes-nib-branding]</remarks>
    public const string NibPathData =
        "M12 1.6 C7.6 1.6 4.6 4.9 4.6 9.3 C4.6 14.8 8.6 19.3 12 22.6 "
        + "C15.4 19.3 19.4 14.8 19.4 9.3 C19.4 4.9 16.4 1.6 12 1.6 Z "
        + "M12 7.1 a2.1 2.1 0 1 0 0 4.2 a2.1 2.1 0 1 0 0 -4.2 Z "
        + "M10.9 12.5 L13.1 12.5 L12.5 18.6 L12 19.9 L11.5 18.6 Z";

    public string RenderNavMarkup(NavigationView nav)
    {
        var prefix = PathUtil.RelativePrefix(nav.ActiveOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(nav.ActiveOutputRelativePath);

        var sb = new StringBuilder();
        // Two-tier chrome (one sticky <nav class="site-nav"> so the toggle script + webview bridge stay intact):
        // a dark IDENTITY bar holding the project name, journey-grouped menu (Home / Delivery / Insights /
        // Follow-ups / Project — each with an icon), and the "Generated by SpecScribe" badge; beneath it a white
        // KEY-VIEWS band with compact grouped wayfinding. [Story 10.1]
        sb.Append("<nav class=\"site-nav\" aria-label=\"Document navigation\">\n");
        sb.Append("  <div class=\"site-nav-inner\">\n");
        // The upper-left of the identity bar is the PROJECT's, and carries the project name alone.
        //
        // The Scribe's Nib mark used to sit here too, immediately left of the wordmark. Owner feedback from the
        // field (2026-08-01): SpecScribe's icon does not belong in the position that names the project — reading
        // "<nib> TakeTheSky" invites the mark to be taken for the project's own. The tool's identity has a place on
        // this bar already, and it is the correct one: the "Generated by <nib> SpecScribe" attribution pinned
        // upper-RIGHT below, which is a statement about who produced the page rather than about whose page it is.
        // NibPathData stays — the badge and the extension icons still share that one geometry.
        sb.Append($"    <span class=\"site-nav-brand\">{PathUtil.Html(nav.SiteTitle)}</span>\n");
        sb.Append("    <button class=\"site-nav-toggle\" type=\"button\" aria-label=\"Toggle navigation\" aria-controls=\"site-nav-links\" aria-expanded=\"false\">Menu</button>\n");
        sb.Append("    <div class=\"site-nav-links\" id=\"site-nav-links\">\n");
        AppendNavMenu(sb, nav, prefix, current);
        sb.Append("    </div>\n");
        // "Generated by SpecScribe" — the output-tool attribution, upper-right of the identity bar, linking to the
        // About / generation-details page (the generation timestamp itself stays in the per-page footer).
        sb.Append($"    <a class=\"site-nav-attribution\" href=\"{PathUtil.Html(prefix + SiteNav.AboutOutputPath)}\" data-tooltip=\"Generated by SpecScribe — view generation details\">"
            + "<span class=\"site-nav-attribution-by\">Generated by</span>"
            + "<span class=\"specscribe-badge\">"
            + "<svg class=\"specscribe-badge-mark\" width=\"16\" height=\"16\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" focusable=\"false\">"
            + $"<path fill-rule=\"evenodd\" d=\"{NibPathData}\"/></svg>"
            + "<span class=\"specscribe-badge-text\">SpecScribe</span></span></a>\n");
        sb.Append("  </div>\n");
        AppendKeyViewsBand(sb, nav, prefix, current);
        sb.Append("</nav>\n");
        return sb.ToString();
    }

    /// <summary>Renders the dark-bar journey menu from <see cref="NavigationView.Groups"/>: flat top-level
    /// links (empty group label — Home, or a single-child collapse) as <c>&lt;a class="site-menu-link"&gt;</c>;
    /// multi-child groups as native <c>&lt;details class="site-nav-group"&gt;</c> disclosures (no JS — webview CSP
    /// + SPA innerHTML swaps). The active leaf is marked and its containing group carries <c>has-active</c> (a
    /// summary highlight so the reader sees which section they are in), but the group is NOT forced
    /// <c>open</c> — a disclosure that springs open on every page load (and stays open through a refresh, since
    /// the state is baked into the HTML) reads as a stuck menu covering the page. It opens on hover/focus/click
    /// like the others. [Story 10.1; auto-open removed Story 10.10 review]</summary>
    private void AppendNavMenu(StringBuilder sb, NavigationView nav, string prefix, string current)
    {
        string LinkHtml(string cls, NavItem item)
        {
            var isActive = string.Equals(PathUtil.NormalizeSlashes(item.OutputRelativePath), current, StringComparison.OrdinalIgnoreCase);
            var attrs = isActive ? $" class=\"{cls} active\" aria-current=\"page\"" : $" class=\"{cls}\"";
            var display = QuickLinkTitle(item.Label);
            return $"<a href=\"{PathUtil.Html(prefix + item.OutputRelativePath)}\"{attrs}>{Icons.ForConcept(item.ConceptKey)}{PathUtil.Html(display)}</a>";
        }

        foreach (var group in nav.Groups)
        {
            if (group.Children.Count == 0) continue;

            // Empty label = flat top-level link(s) (Home, or single-child collapse from SiteNav.Build).
            if (string.IsNullOrEmpty(group.Label))
            {
                foreach (var child in group.Children)
                    sb.Append($"      {LinkHtml("site-menu-link", child)}\n");
                continue;
            }

            var hasActive = group.Children.Any(c =>
                string.Equals(PathUtil.NormalizeSlashes(c.OutputRelativePath), current, StringComparison.OrdinalIgnoreCase));
            var groupCls = hasActive ? "site-nav-group has-active" : "site-nav-group";
            var family = QuickLinkFamily(group.Label);
            sb.Append($"      <details class=\"{groupCls} {family}\">\n");
            sb.Append($"        <summary class=\"site-nav-group-summary\">{Icons.ForConcept(group.ConceptKey)}{PathUtil.Html(group.Label)}<span class=\"site-menu-caret\" aria-hidden=\"true\">&#9662;</span></summary>\n");
            sb.Append("        <div class=\"site-nav-group-panel\">\n");
            foreach (var child in group.Children)
                sb.Append($"          {LinkHtml("site-menu-item", child)}\n");
            sb.Append("        </div>\n      </details>\n");
        }
    }

    /// <summary>The white sub-header band. On Home it is the Driver work-stage toggle strip
    /// (Overview · Requirements · Plan · Develop · Review · Track) — pure-CSS radios that show/hide
    /// stage-tagged dashboard panels. Off Home, a page-type-specific <see cref="NavLocalContext"/>
    /// (epic's stories, code file siblings, ADR family, ...) takes over when one is available and has
    /// a navigable (non-active) item; otherwise it falls back to the generic Docs / Architecture / Work
    /// key-views chips. Omits the band when there is nothing to show. [home welcome key-views; Story 9.8;
    /// local-context branch: Story 10.10]</summary>
    private void AppendKeyViewsBand(StringBuilder sb, NavigationView nav, string prefix, string current)
    {
        var onHome = string.Equals(current, SiteNav.HomeOutputPath, StringComparison.OrdinalIgnoreCase);
        if (onHome)
        {
            AppendWorkModeJumpStrip(sb, nav.FullHomeWorkModeStrip);
            return;
        }

        // NFR8: at least one NAVIGABLE (non-active) item must exist, or the band is either empty or a
        // degenerate "here you are, with nowhere else to go" single self-link — both fall back to the
        // generic band rather than rendering a band that looks broken.
        if (nav.LocalContext is { } localContext && localContext.Items.Any(i => !i.IsActive))
        {
            AppendLocalContextBand(sb, localContext);
            return;
        }

        if (nav.QuickLinks.Count == 0) return;

        // Fall back to "Project" for a Group value that isn't one of KeyViewGroupOrder's literals — the
        // same safety net the old exhaustive KeyViewGroup switch's `_ => "Project"` default arm gave every
        // label, now preserved even though the mapping itself moved to per-call-site data. [Story 10.1
        // deferred debt cleanup; Help nav]
        var entries = nav.QuickLinks
            .Select(q => (Label: q.Label, Title: QuickLinkTitle(q.Label), Path: q.OutputRelativePath, Desc: q.Description,
                Group: KeyViewGroupOrder.Contains(q.Group) ? q.Group : "Project"))
            .ToList();

        sb.Append("  <div class=\"site-nav-key-views\" aria-label=\"Key views\">\n");
        sb.Append("    <div class=\"quick-link-pills\">\n");
        foreach (var group in KeyViewGroupOrder)
        {
            var members = entries.Where(e => e.Group == group).ToList();
            if (members.Count == 0) continue;

            if (members.Count == 1)
            {
                var only = members[0];
                sb.Append($"      <a class=\"quick-link-pill {QuickLinkFamily(only.Label)}\" href=\"{PathUtil.Html(prefix + only.Path)}\" data-tooltip=\"{PathUtil.Html(only.Desc)}\">{Icons.ForConcept(only.Label)}{PathUtil.Html(only.Title)}</a>\n");
                continue;
            }

            var panelId = $"key-view-panel-{group.ToLowerInvariant()}";
            sb.Append($"      <div class=\"key-view-group {QuickLinkFamily(group)}\">\n");
            sb.Append($"        <button class=\"quick-link-pill key-view-trigger\" type=\"button\" aria-haspopup=\"true\" aria-expanded=\"false\" aria-controls=\"{panelId}\">{Icons.ForConcept(group)}{PathUtil.Html(group)}<span class=\"site-menu-caret\" aria-hidden=\"true\">&#9662;</span></button>\n");
            sb.Append($"        <div class=\"key-view-panel\" id=\"{panelId}\">\n");
            foreach (var m in members)
            {
                sb.Append($"          <a class=\"key-view-item\" href=\"{PathUtil.Html(prefix + m.Path)}\" data-tooltip=\"{PathUtil.Html(m.Desc)}\">{Icons.ForConcept(m.Label)}{PathUtil.Html(m.Title)}</a>\n");
            }
            sb.Append("        </div>\n      </div>\n");
        }
        sb.Append("    </div>\n  </div>\n");
    }

    /// <summary>Above this many items, the local-context band stops growing inline and tucks the remainder
    /// behind a "More" disclosure — otherwise a large epic/ADR/requirement family wraps the white band across
    /// several lines and dominates the header. [Story 10.10]</summary>
    private const int LocalContextInlineLimit = 8;

    /// <summary>Max characters shown on an inline local-context pill / an overflow-panel item before the label
    /// is ellipsised. A follow-up summary or a long ADR title would otherwise stretch a single pill across most
    /// of the bar (or wrap it onto several lines); the full text always rides a native <c>title</c> tooltip so
    /// nothing is lost. Panel rows get a little more room since they stack vertically. [Story 10.10 review]</summary>
    private const int LocalContextPillLabelMax = 28;
    private const int LocalContextPanelLabelMax = 44;

    /// <summary>Ellipsise <paramref name="label"/> to <paramref name="max"/> chars when it's longer, returning
    /// the display text plus the full text to surface as a <c>title</c> tooltip (null when no truncation
    /// happened, so an untruncated label gets no redundant tooltip). [Story 10.10 review]</summary>
    private static (string Display, string? Tooltip) TruncateNavLabel(string label, int max)
    {
        if (label.Length <= max) return (label, null);
        return (label[..(max - 1)].TrimEnd() + "…", label);
    }

    /// <summary>The white sub-header band's page-type-specific local-context branch: a small title label + a pill
    /// per <see cref="NavLocalItem"/> (the active one marked), reusing the <c>.quick-link-pill</c> visual
    /// language under a distinct CSS family (<c>.site-nav-local-context</c>/<c>.local-context-pill</c>) so it can
    /// be told apart from the generic quick-links band. <see cref="NavLocalItem.Href"/> is already relative to
    /// the current page (the <c>PagerLink.Href</c> convention), so this never recomputes a prefix per item. The
    /// active item renders as plain text (a <c>&lt;span&gt;</c>), never a self-link — the same "current page
    /// never self-links" rule <see cref="RenderBreadcrumb"/> already applies to its last crumb. Beyond
    /// <see cref="LocalContextInlineLimit"/> items, the remainder collapses into a "More" disclosure reusing the
    /// SAME <c>.key-view-group</c>/<c>.key-view-trigger</c>/<c>.key-view-panel</c> pattern (and its existing
    /// hover/focus-within CSS + <c>specscribe.js</c> click handler) the generic quick-links band already uses —
    /// no new JS, no webview CSP exception, since that handler is already class-selector-generic. [Story 10.10]</summary>
    private static void AppendLocalContextBand(StringBuilder sb, NavLocalContext localContext)
    {
        sb.Append("  <div class=\"site-nav-key-views site-nav-local-context\" aria-label=\"" + PathUtil.Html(localContext.Title) + "\">\n");
        sb.Append("    <div class=\"local-context-pills\">\n");
        sb.Append($"      <span class=\"local-context-label\">{PathUtil.Html(localContext.Title)}</span>\n");

        var items = localContext.Items;
        var visible = items.Count > LocalContextInlineLimit ? items.Take(LocalContextInlineLimit).ToList() : items.ToList();
        var overflow = items.Count > LocalContextInlineLimit ? items.Skip(LocalContextInlineLimit).ToList() : new List<NavLocalItem>();

        // The active item must stay visible without opening the "More" panel (so a reader always sees "you
        // are here"); if it fell into the overflow window, pin it into view instead of leaving it buried.
        NavLocalItem? pinnedActive = null;
        if (overflow.Count > 0 && !visible.Any(i => i.IsActive))
        {
            var pinnedIndex = overflow.FindIndex(i => i.IsActive);
            if (pinnedIndex >= 0)
            {
                pinnedActive = overflow[pinnedIndex];
                overflow.RemoveAt(pinnedIndex);
            }
        }

        foreach (var item in visible)
        {
            AppendLocalContextPill(sb, item);
        }
        if (pinnedActive is not null)
        {
            AppendLocalContextPill(sb, pinnedActive);
        }

        if (overflow.Count > 0)
        {
            const string panelId = "local-context-more-panel";
            sb.Append("      <div class=\"key-view-group\">\n");
            sb.Append($"        <button class=\"local-context-pill key-view-trigger\" type=\"button\" aria-haspopup=\"true\" aria-expanded=\"false\" aria-controls=\"{panelId}\">More ({overflow.Count})<span class=\"site-menu-caret\" aria-hidden=\"true\">&#9662;</span></button>\n");
            sb.Append($"        <div class=\"key-view-panel\" id=\"{panelId}\">\n");
            foreach (var item in overflow)
            {
                AppendLocalContextPill(sb, item, "key-view-item", LocalContextPanelLabelMax);
            }
            sb.Append("        </div>\n      </div>\n");
        }

        sb.Append("    </div>\n  </div>\n");
    }

    /// <summary>One local-context pill: the active item as plain text (never a self-link — the same rule
    /// <see cref="RenderBreadcrumb"/>'s last crumb already follows), everything else as a real link. Long
    /// labels are ellipsised to <paramref name="labelMax"/> with the full text on a <c>title</c> tooltip, so
    /// a verbose follow-up summary or ADR title can't stretch the band. Shared by both the inline pill row
    /// and the "More" overflow panel (<paramref name="cssClass"/> swaps the visual family) so the panel gets
    /// the same self-link guard instead of hand-rendering its own anchor. Carries the SAME <see cref="Icons.ForConcept"/>
    /// glyph the dark-bar Insights dropdown already shows for this exact label (owner feedback: the white-bar
    /// Insights pills — Git Insights/Deep Analytics/Code Map/Risk Quadrant — looked inconsistent without one);
    /// an un-curated label (e.g. a per-epic story pager item) gracefully renders no icon, unchanged from before.
    /// [Story 10.10; icon: Story 7.12 review]</summary>
    private static void AppendLocalContextPill(StringBuilder sb, NavLocalItem item, string cssClass = "local-context-pill", int labelMax = LocalContextPillLabelMax)
    {
        var (display, tooltip) = TruncateNavLabel(item.Label, labelMax);
        var titleAttr = tooltip is null ? "" : $" title=\"{PathUtil.Html(tooltip)}\"";
        var icon = Icons.ForConcept(item.Label);
        if (item.IsActive)
        {
            sb.Append($"      <span class=\"{cssClass} active\" aria-current=\"page\"{titleAttr}>{icon}{PathUtil.Html(display)}</span>\n");
            return;
        }
        sb.Append($"      <a href=\"{PathUtil.Html(item.Href)}\" class=\"{cssClass}\"{titleAttr}>{icon}{PathUtil.Html(display)}</a>\n");
    }

    /// <summary>Home-only white-bar work-stage strip: pure-CSS radios + labels (icons + words) that toggle
    /// which dashboard panels are visible via <c>display:none</c>. Overview is the default. When
    /// <paramref name="fullStages"/> is false (no epics model), only Overview is emitted. [Story 9.8]</summary>
    private static void AppendWorkModeJumpStrip(StringBuilder sb, bool fullStages)
    {
        sb.Append("  <div class=\"site-nav-key-views work-mode-jumps\" aria-label=\"Work stage\">\n");
        sb.Append("    <div class=\"work-mode-pills board-tabs\" role=\"group\">\n");
        sb.Append("      <input type=\"radio\" id=\"wm-overview\" name=\"work-mode\" class=\"board-tab-radio\" checked>\n");
        if (fullStages)
        {
            sb.Append("      <input type=\"radio\" id=\"wm-requirements\" name=\"work-mode\" class=\"board-tab-radio\">\n");
            sb.Append("      <input type=\"radio\" id=\"wm-plan\" name=\"work-mode\" class=\"board-tab-radio\">\n");
            sb.Append("      <input type=\"radio\" id=\"wm-develop\" name=\"work-mode\" class=\"board-tab-radio\">\n");
            sb.Append("      <input type=\"radio\" id=\"wm-review\" name=\"work-mode\" class=\"board-tab-radio\">\n");
            sb.Append("      <input type=\"radio\" id=\"wm-track\" name=\"work-mode\" class=\"board-tab-radio\">\n");
        }
        sb.Append($"      <label for=\"wm-overview\" class=\"work-mode-pill\">{Icons.ForConcept("Overview")}Overview</label>\n");
        if (fullStages)
        {
            sb.Append($"      <label for=\"wm-requirements\" class=\"work-mode-pill\">{Icons.ForConcept("Requirements")}Requirements</label>\n");
            sb.Append($"      <label for=\"wm-plan\" class=\"work-mode-pill\">{Icons.ForConcept("Plan")}Plan</label>\n");
            sb.Append($"      <label for=\"wm-develop\" class=\"work-mode-pill\">{Icons.ForConcept("Develop")}Develop</label>\n");
            sb.Append($"      <label for=\"wm-review\" class=\"work-mode-pill\">{Icons.ForConcept("Review")}Review</label>\n");
            sb.Append($"      <label for=\"wm-track\" class=\"work-mode-pill\">{Icons.ForConcept("Track")}Track</label>\n");
        }
        sb.Append("    </div>\n  </div>\n");
    }

    /// <summary>The (display label, output-relative target) pairs in the exact order the dark-bar menu emits its
    /// anchors — flattened <see cref="NavigationView.Items"/> with display titles. The render-parity harness
    /// reuses this so its declared nav graph matches the markup the adapter produces. [Story 10.1]</summary>
    public IReadOnlyList<(string Label, string OutputRelativePath)> NavMenuOrder(NavigationView nav) =>
        nav.Items.Select(i => (QuickLinkTitle(i.Label), i.OutputRelativePath)).ToList();

    /// <summary>Display order for the key-views band groups (white sub-header off Home). Per-label group
    /// membership itself is single-sourced on <see cref="SiteNav.QuickLinks"/>'s <c>Group</c> element, set at
    /// <see cref="SiteNav.Build"/> time — this array only decides render order among the groups that appear.
    /// [Story 10.1; Story 10.1 deferred debt cleanup]</summary>
    /// <remarks>Also consumed by <c>SiteGenerator.BuildOutlineShortcuts</c> so the VS Code Shortcuts pane and this
    /// band present the same groups in the same order. Internal rather than private for exactly that reason: the
    /// alternative was a second copy of the order, and a second copy is how the two surfaces disagree.</remarks>
    internal static readonly string[] KeyViewGroupOrder = { "Delivery", "Insights", "Follow-ups", "Project", "Help" };

    /// <summary>The HTML surface's inline nav-toggle script, verbatim (self-locating via
    /// <c>document.currentScript</c>, so it must directly follow the nav element). Deliberately NOT emitted by the
    /// webview surface — its CSP blocks non-nonce'd inline scripts. [Story 6.1; split out Story 6.4]</summary>
    private const string NavToggleScript = "<script>(function(){var script=document.currentScript;if(!script)return;var nav=script.previousElementSibling;if(!nav||!nav.classList.contains('site-nav'))return;var toggle=nav.querySelector('.site-nav-toggle');var links=nav.querySelector('.site-nav-links');if(!toggle||!links)return;var mq=window.matchMedia('(max-width: 640px)');function closeNav(){nav.classList.remove('site-nav-open');toggle.setAttribute('aria-expanded','false');}function openNav(){nav.classList.add('site-nav-open');toggle.setAttribute('aria-expanded','true');var first=links.querySelector('a');if(first)first.focus();}toggle.addEventListener('click',function(){if(nav.classList.contains('site-nav-open')){closeNav();}else{openNav();}});links.querySelectorAll('a').forEach(function(link){link.addEventListener('click',function(){if(mq.matches){closeNav();}});});nav.addEventListener('keydown',function(evt){if(evt.key==='Escape'&&nav.classList.contains('site-nav-open')){evt.preventDefault();closeNav();toggle.focus();}});window.addEventListener('resize',function(){if(!mq.matches){closeNav();}});})();</script>\n\n";

    /// <summary>Renders a "Home / Epics / Epic 1 / Story 1.1" trail from a <see cref="BreadcrumbTrail"/>. The last
    /// crumb (current page) has a null path so it renders as plain text rather than a self-link. Verbatim
    /// re-homing of <see cref="SiteNav.RenderBreadcrumb"/>'s former body — the current output path supplies the
    /// relative-link prefix (a delivery concern). Output is unchanged. [Story 6.1]</summary>
    public string RenderBreadcrumb(string currentOutputRelativePath, BreadcrumbTrail trail)
    {
        if (trail.Crumbs.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"breadcrumb\" aria-label=\"Breadcrumb\">\n");
        AppendCrumbs(sb, currentOutputRelativePath, trail);
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    private static void AppendCrumbs(StringBuilder sb, string currentOutputRelativePath, BreadcrumbTrail trail)
    {
        var prefix = PathUtil.RelativePrefix(currentOutputRelativePath);
        for (var i = 0; i < trail.Crumbs.Count; i++)
        {
            if (i > 0) sb.Append("  <span class=\"crumb-sep\">/</span>\n");
            var (label, path) = (trail.Crumbs[i].Label, trail.Crumbs[i].OutputRelativePath);
            if (path is not null)
            {
                sb.Append($"  <a href=\"{PathUtil.Html(prefix + path)}\">{PathUtil.Html(label)}</a>\n");
            }
            else
            {
                sb.Append($"  <span class=\"crumb-current\" aria-current=\"page\">{PathUtil.Html(label)}</span>\n");
            }
        }
    }

    /// <summary>Renders the breadcrumb and the sibling <see cref="EntityPager"/> as ONE coherent wayfinding
    /// strip — the unification AC1 asks for (they used to answer "where am I / where can I go" in two unrelated
    /// visual registers: breadcrumb as a full-width strip, pager floated inside the body's own header). Absent a
    /// pager (null or <see cref="EntityPager.IsEmpty"/>), this is BYTE-IDENTICAL to <see cref="RenderBreadcrumb"/>
    /// alone — the vast majority of pages have no pager, and their markup must not change. [Story 10.11]</summary>
    public string RenderWayfinding(string currentOutputRelativePath, BreadcrumbTrail trail, EntityPager? pager)
    {
        var pagerHtml = pager?.Render() ?? string.Empty;
        if (pagerHtml.Length == 0) return RenderBreadcrumb(currentOutputRelativePath, trail);

        var sb = new StringBuilder();
        sb.Append("<div class=\"page-wayfinding\">\n");
        if (trail.Crumbs.Count > 0)
        {
            sb.Append("<div class=\"breadcrumb\" aria-label=\"Breadcrumb\">\n");
            AppendCrumbs(sb, currentOutputRelativePath, trail);
            sb.Append("</div>\n");
        }
        sb.Append(pagerHtml);
        sb.Append("</div>\n\n");
        return sb.ToString();
    }
}
