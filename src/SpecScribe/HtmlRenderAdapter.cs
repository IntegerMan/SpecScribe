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
        sb.Append(RenderBreadcrumb(page.OutputRelativePath, page.Breadcrumb));
        sb.Append(page.BodyHtml);
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
    public string RenderNavMarkup(NavigationView nav)
    {
        var prefix = PathUtil.RelativePrefix(nav.ActiveOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(nav.ActiveOutputRelativePath);

        var sb = new StringBuilder();
        // The <nav> is the full-bleed sticky bar; an inner wrapper constrains the brand + links to the same
        // centered content column width as the page body, so the brand and last item line up with the page
        // gutters instead of floating at the viewport edges. [Deep Analytics polish]
        sb.Append("<nav class=\"site-nav\" aria-label=\"Document navigation\">\n");
        sb.Append("  <div class=\"site-nav-inner\">\n");
        sb.Append($"    <span class=\"site-nav-brand\">{PathUtil.Html(nav.SiteTitle)}</span>\n");
        sb.Append("    <button class=\"site-nav-toggle\" type=\"button\" aria-label=\"Toggle navigation\" aria-controls=\"site-nav-links\" aria-expanded=\"false\">Menu</button>\n");
        sb.Append("    <div class=\"site-nav-links\" id=\"site-nav-links\">\n");
        foreach (var item in nav.Items)
        {
            var href = prefix + item.OutputRelativePath;
            var isActive = string.Equals(PathUtil.NormalizeSlashes(item.OutputRelativePath), current, StringComparison.OrdinalIgnoreCase);
            var attrs = isActive ? " class=\"active\" aria-current=\"page\"" : string.Empty;
            sb.Append($"      <a href=\"{PathUtil.Html(href)}\"{attrs}>{Icons.ForConcept(item.ConceptKey)}{PathUtil.Html(item.Label)}</a>\n");
        }
        sb.Append("    </div>\n  </div>\n</nav>\n");
        return sb.ToString();
    }

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
        var prefix = PathUtil.RelativePrefix(currentOutputRelativePath);

        var sb = new StringBuilder();
        sb.Append("<div class=\"breadcrumb\" aria-label=\"Breadcrumb\">\n");
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
        sb.Append("</div>\n\n");
        return sb.ToString();
    }
}
