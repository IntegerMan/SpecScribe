using System.Text;

namespace SpecScribe;

/// <summary>The THIRD concrete <see cref="IRenderAdapter"/> — the JSON + client-renderer (SPA) delivery form ADR
/// 0006 seated as Architecture B (additive: "JSON + SPA delivery adapter — C# still renders"). It emits, per page,
/// the SWAPPABLE content region — the shared nav markup + breadcrumb + the page's <c>&lt;main&gt;</c> body — that a
/// small vanilla-JS client injects to navigate the whole site from a handful of files instead of thousands of
/// static <c>.html</c> documents. Rendering stays entirely in C# (AC #1): the client fetches these regions,
/// injects them, and updates the URL — it re-renders nothing and re-parses nothing (AD-1/AD-2). Charts ride along
/// as pre-rendered inline SVG exactly as on the HTML surface (this cuts FILE COUNT, not bytes — ADR 0006 axis A;
/// the byte-shrinking TS chart port is option D, deferred).
/// <para><b>Unlike the webview</b> (<see cref="WebviewRenderAdapter"/>), the SPA runs in a real browser, so it
/// keeps the production <c>specscribe.css</c>/<c>specscribe.js</c> — its chrome/asset semantic facts therefore MATCH
/// the <c>html</c> surface and it registers no asset.css/asset.js <see cref="HostRenderException"/> (AC #4). It DOES
/// register one Mermaid exception (<see cref="HostRenderExceptions.Registry"/>: <c>("spa", "mermaid", …)</c>): the
/// client swaps regions via innerHTML, where an injected Mermaid init script never executes, so the roadmap's
/// <c>&lt;pre class="mermaid"&gt;</c> degrades to readable preformatted text — the same accepted fallback as the
/// webview. The region shape is identical to the webview's <see cref="WebviewRenderAdapter.RenderContent"/> (nav
/// markup, no inline toggle script — the client owns nav-toggle via delegation across swaps). [Story 6.7]</para></summary>
public sealed class JsonSpaRenderAdapter : IRenderAdapter
{
    /// <summary>The single shared instance — stateless, like <see cref="HtmlRenderAdapter.Shared"/> and
    /// <see cref="WebviewRenderAdapter.Shared"/>.</summary>
    public static readonly JsonSpaRenderAdapter Shared = new();

    public string Id => "spa";

    /// <summary>The SPA's per-page artifact IS its content region (the JSON layer ships regions, not full
    /// documents — the one shared client shell carries the head/chrome-once). So <see cref="Render"/> returns the
    /// region as the artifact content, keeping the <see cref="IRenderAdapter"/> contract satisfied while reflecting
    /// what this surface actually delivers. [Story 6.7]</summary>
    public RenderedArtifact Render(PageView page) =>
        new(page.OutputRelativePath, RenderContent(page));

    /// <summary>Renders the SWAPPABLE content region for one page: the shared nav markup (via
    /// <see cref="HtmlRenderAdapter.RenderNavMarkup"/> — deliberately WITHOUT the inline nav-toggle script, which
    /// would be dead code after an <c>innerHTML</c> swap; the client renderer owns the toggle through event
    /// delegation), the breadcrumb, then the page body verbatim. Byte-for-byte the same region the webview ships
    /// for the dashboard/epics families, which is what makes the section-fact parity checks (AC #4) meaningful:
    /// the SPA delivers the SAME C#-rendered content, not a re-render. [Story 6.7]</summary>
    public string RenderContent(PageView page)
    {
        var sb = new StringBuilder();
        sb.Append(HtmlRenderAdapter.Shared.RenderNavMarkup(page.Nav));
        sb.Append(HtmlRenderAdapter.Shared.RenderWayfinding(page.OutputRelativePath, page.Breadcrumb, page.Pager));
        sb.Append(page.BodyHtml);
        return sb.ToString();
    }
}
