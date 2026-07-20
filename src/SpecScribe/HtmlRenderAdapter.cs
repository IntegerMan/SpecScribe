using System.Text;

namespace SpecScribe;

/// <summary>The FIRST and (Story 6.1) ONLY concrete <see cref="IRenderAdapter"/>: turns a host-neutral
/// <see cref="PageView"/> into today's exact HTML for the shared page chrome (head + nav + breadcrumb + footer),
/// composing the opaque <see cref="PageView.BodyHtml"/> into that shell. This is the delivery-seam mirror of
/// Story 4.1's <see cref="BmadArtifactAdapter"/> — it CONSUMES view models and emits host output; it never
/// re-parses a source artifact (AD-2).
/// <para><b>Byte-for-byte guarantee.</b> This adapter is a mechanical RE-HOMING of the chrome string-building
/// that lived across the templaters and <see cref="SiteNav"/>, not a rewrite: <see cref="RenderNav"/> /
/// <see cref="RenderBreadcrumb"/> hold the verbatim strings, and <see cref="SiteNav.RenderNavBar"/> /
/// <see cref="SiteNav.RenderBreadcrumb"/> now delegate here so every un-migrated page renders identically. The
/// golden-output regression (SiteGeneratorAdapterTests) is the gate: any changed byte fails it. [Story 6.1]</para></summary>
public sealed partial class HtmlRenderAdapter : IRenderAdapter
{
    /// <summary>The single shared instance — the adapter is stateless, so <see cref="SiteNav"/>'s delegating
    /// chrome helpers and the templaters reuse one instance rather than allocating per page.</summary>
    public static readonly HtmlRenderAdapter Shared = new();

    public string Id => "html";

    /// <summary>Composes the full page: head open + nav + breadcrumb + the opaque body + footer + (mermaid init
    /// when needed) + close. The concatenation order and every helper call match what the templaters produced
    /// inline, so the rendered bytes are unchanged (AC #1). The footer's generation timestamp is produced here,
    /// exactly as the templaters produced it. [Story 6.1]</summary>
    public RenderedArtifact Render(PageView page)
    {
        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(page.Title, page.Assets.StylesheetHref, page.Assets.ScriptHref, page.MetaDescription));
        sb.Append(RenderNav(page.Nav));
        sb.Append(RenderWayfinding(page.OutputRelativePath, page.Breadcrumb, page.Pager));
        sb.Append(page.BodyHtml);
        // The active-section tracking script rides the SAME chrome-level seam as the Mermaid init script below —
        // appended AFTER the opaque body, never inside it — so the webview's RenderContent and the SPA family
        // surfaces (both of which use page.BodyHtml directly, not this full Render output) never carry it. That
        // is what gives webview/SPA their clean NFR8 degrade to today's static TOC there, matching their CSP/
        // innerHTML non-execution, without a separate per-surface branch. [Story 10.11]
        if (page.BodyHtml.Contains("class=\"toc-sidebar\"", StringComparison.Ordinal))
        {
            sb.Append(Toc.ActiveSectionScript);
        }
        sb.Append(PathUtil.RenderFooter(PathUtil.RelativePrefix(page.OutputRelativePath)));
        if (page.Assets.MermaidNeeded)
        {
            sb.Append(Mermaid.InitScript());
        }
        sb.Append("</body>\n</html>\n");
        return new RenderedArtifact(page.OutputRelativePath, sb.ToString());
    }

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
        // The Scribe's Nib brand mark (spec-scribes-nib-branding): the SAME nib geometry as the extension's
        // activity-bar/panel icons (see NibPathData), inlined once at this ONE nav seam so all three surfaces
        // (site, webview, SPA) carry it. Decorative (aria-hidden) beside the wordmark; colored purely via
        // currentColor from the brand span (token-system rule — no hex in markup), with the vent and slit as
        // evenodd cutouts so it reads on any bar background. width/height are the unstyled fallback size — a
        // stylesheet miss must degrade to a small icon, never the 300×150 replaced-element default.
        sb.Append("    <span class=\"site-nav-brand\">"
            + "<svg class=\"site-nav-mark\" width=\"16\" height=\"16\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" focusable=\"false\">"
            + $"<path fill-rule=\"evenodd\" d=\"{NibPathData}\"/></svg>"
            + $"{PathUtil.Html(nav.SiteTitle)}</span>\n");
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
    /// stage-tagged dashboard panels. On every other page it keeps the Docs / Architecture / Work
    /// key-views chips. Omits the band when there is nothing to show. [home welcome key-views; Story 9.8]</summary>
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
            pinnedActive = overflow.FirstOrDefault(i => i.IsActive);
            if (pinnedActive is not null)
                overflow = overflow.Where(i => i != pinnedActive).ToList();
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
                var (display, tooltip) = TruncateNavLabel(item.Label, LocalContextPanelLabelMax);
                var titleAttr = tooltip is null ? "" : $" title=\"{PathUtil.Html(tooltip)}\"";
                sb.Append($"          <a class=\"key-view-item\" href=\"{PathUtil.Html(item.Href)}\"{titleAttr}>{PathUtil.Html(display)}</a>\n");
            }
            sb.Append("        </div>\n      </div>\n");
        }

        sb.Append("    </div>\n  </div>\n");
    }

    /// <summary>One local-context pill: the active item as plain text (never a self-link — the same rule
    /// <see cref="RenderBreadcrumb"/>'s last crumb already follows), everything else as a real link. Long
    /// labels are ellipsised to <see cref="LocalContextPillLabelMax"/> with the full text on a <c>title</c>
    /// tooltip, so a verbose follow-up summary or ADR title can't stretch the band. [Story 10.10]</summary>
    private static void AppendLocalContextPill(StringBuilder sb, NavLocalItem item)
    {
        var (display, tooltip) = TruncateNavLabel(item.Label, LocalContextPillLabelMax);
        var titleAttr = tooltip is null ? "" : $" title=\"{PathUtil.Html(tooltip)}\"";
        if (item.IsActive)
        {
            sb.Append($"      <span class=\"local-context-pill active\" aria-current=\"page\"{titleAttr}>{PathUtil.Html(display)}</span>\n");
            return;
        }
        sb.Append($"      <a href=\"{PathUtil.Html(item.Href)}\" class=\"local-context-pill\"{titleAttr}>{PathUtil.Html(display)}</a>\n");
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
    private static readonly string[] KeyViewGroupOrder = { "Delivery", "Insights", "Follow-ups", "Project", "Help" };

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
