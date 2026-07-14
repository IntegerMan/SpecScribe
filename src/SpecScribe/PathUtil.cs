using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Small stateless helpers shared by every page-rendering class (relative-link math, HTML escaping, the common page shell).</summary>
public static class PathUtil
{
    public static string NormalizeSlashes(string path) => path.Replace('\\', '/');

    /// <summary>Number of "../" segments needed to reach the output root from this file's directory.
    /// Deliberately avoids Path.GetDirectoryName, which renormalizes separators to the OS style on
    /// Windows (back to '\') and silently breaks a naive forward-slash split.</summary>
    public static string RelativePrefix(string outputRelativePath)
    {
        var segments = NormalizeSlashes(outputRelativePath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var depth = segments.Length - 1;
        return depth <= 0 ? string.Empty : string.Concat(Enumerable.Repeat("../", depth));
    }

    public static string ToOutputRelative(string sourceRelativePath) => Path.ChangeExtension(sourceRelativePath, ".html");

    /// <summary>True for working files no pipeline stage should touch — editor temps (<c>~$…</c>,
    /// <c>*.tmp</c>, <c>*.crswap</c>) and dotfiles (e.g. <c>.memlog.md</c>). One predicate shared by the
    /// generator's source enumeration and the framework adapters' ingest, so ignored files are neither
    /// rendered nor reported as unsupported wherever discovery happens. [Story 4.1]</summary>
    public static bool IsIgnoredSourceFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("~$", StringComparison.Ordinal)
            || name.StartsWith('.')
            || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".crswap", StringComparison.OrdinalIgnoreCase);
    }

    public static string Html(string s) => WebUtility.HtmlEncode(s);

    /// <summary>A small inline-SVG favicon — the Scribe's Nib brand mark on a teal tile — emitted as a data URI
    /// so every page carries a real tab icon and <c>/favicon.ico</c> stops 404-ing, with no extra asset file to
    /// ship. This is the browser-tab rendition of the same mark as the extension's panel icon
    /// (<c>extension/media/specscribe.svg</c>): a teal rounded tile, the parchment nib silhouette, and a gold
    /// vent. The nib geometry is REUSED verbatim from <see cref="HtmlRenderAdapter.NibPathData"/> (the 24-box path
    /// with the vent + tip slit as <c>evenodd</c> cutouts) rather than re-drawn, so there is no fourth divergent
    /// copy of the mark to keep in sync; the gold <c>circle</c> fills the evenodd vent hole. The hardcoded hex is
    /// intentional — a data-URI favicon is an isolated asset outside the CSS token system (as is
    /// <c>specscribe.svg</c>), so the "no hex in markup" rule (which governs CSS-driven chrome) does not apply.
    /// [spec-website-nib-favicon]</summary>
    private const string FaviconSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>" +
        "<rect x='1' y='1' width='22' height='22' rx='5' fill='#2e6b7a'/>" +
        "<path fill-rule='evenodd' fill='#f5f0e8' d='" + HtmlRenderAdapter.NibPathData + "'/>" +
        "<circle cx='12' cy='9.2' r='2.1' fill='#d4a017'/>" +
        "</svg>";

    // Percent-encode the whole SVG so the data URI is valid per RFC 3986 — raw spaces and '<'/'>' inside the
    // href can break the icon in stricter browsers/validators (only '#' was encoded before). [Story 1.5 G1 review]
    private static readonly string FaviconDataUri = "data:image/svg+xml," + Uri.EscapeDataString(FaviconSvg);

    // A short cache-busting token appended to the shared css/js hrefs so a redeployed stylesheet/script is
    // never masked by a browser- or CDN-cached copy of the previous build (the failure mode behind stale-CSS
    // "unstyled new elements + old colors" artifacts). Deterministic builds (SDK default) make the module
    // version id a content hash of the assembly — which embeds the css+js — so it changes exactly when those
    // assets change. [Story 1.5 review — cache-busting]
    private static readonly string AssetVersion =
        typeof(PathUtil).Assembly.ManifestModule.ModuleVersionId.ToString("N").Substring(0, 8);

    /// <summary>The same cache-busting token used on <c>specscribe.css</c>/<c>specscribe.js</c>, exposed for other
    /// emitted assets (e.g. the SPA delivery form's script + JSON data layer) that need the identical
    /// redeploy-invalidation guarantee. [Story 6.7 review]</summary>
    public static string CurrentAssetVersion => AssetVersion;

    public static string RenderHeadOpen(string title, string cssHref, string scriptHref, string? description = null, string? extraHead = null)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"UTF-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n");
        sb.Append($"<title>{Html(title)}</title>\n");
        // Description + minimal Open Graph so a shared link renders with a title/summary rather than bare. [Story 1.5 G2]
        var desc = description is { Length: > 0 } ? description : title;
        sb.Append($"<meta name=\"description\" content=\"{Html(desc)}\">\n");
        sb.Append("<meta property=\"og:type\" content=\"website\">\n");
        sb.Append($"<meta property=\"og:title\" content=\"{Html(title)}\">\n");
        sb.Append($"<meta property=\"og:description\" content=\"{Html(desc)}\">\n");
        sb.Append($"<link rel=\"icon\" href=\"{FaviconDataUri}\">\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{Html(cssHref)}?v={AssetVersion}\">\n");
        // The one sanctioned progressive-enhancement script (on-brand tooltips + copy buttons); `defer` so it
        // never blocks render and runs after the DOM is parsed. Degrades to <title>/aria-label with JS off. [Story 1.5 Task 3]
        // Both shared assets carry a build-versioned query so a cached copy can't mask a redeployed one.
        sb.Append($"<script src=\"{Html(scriptHref)}?v={AssetVersion}\" defer></script>\n");
        // Page-specific head additions (e.g. a code page's Prism stylesheet + highlighter script). Emitted verbatim
        // by the caller, which owns the exact tags — kept out of the shared path so a normal page carries nothing extra.
        if (extraHead is { Length: > 0 })
        {
            sb.Append(extraHead);
        }
        sb.Append("</head>\n<body>\n");
        // Skip link is the first focusable element on every page — a keyboard user can jump straight past
        // the nav to the page's single <main id="main-content"> landmark. [Story 1.4 AC #1, UX-DR16]
        sb.Append("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>\n");
        return sb.ToString();
    }

    /// <summary>The canonical SpecScribe repository URL — the single source shared by the footer's "SpecScribe"
    /// credit link and the About page's repository link, so the two can never drift. [Story 4.8 Task 5]</summary>
    public const string RepositoryUrl = "https://github.com/IntegerMan/SpecScribe";

    /// <summary>The author's homepage — surfaced as the About page's author link. There is no standard assembly
    /// attribute for an author URL, so it lives here as a constant, mirroring <see cref="RepositoryUrl"/>.</summary>
    public const string AuthorUrl = "https://MattEland.dev";

    /// <summary>The site-wide footer at the bottom of every page: the SpecScribe credit link, the generation
    /// timestamp (human-friendly), and a "View generation details" link on to the About page — the owner-chosen
    /// reachability path to the About page and, through it, the diagnostics run log, so it appears on every page.
    /// Preceded by the shared status legend key (Story 8.2) so every HTML page teaches the lifecycle vocabulary;
    /// webview/SPA inject the same <see cref="StatusStyles.LegendKey"/> after BodyHtml because they omit this
    /// footer. The generation date is formatted here (single source) rather than by each caller. The details link's href is
    /// resolved from <paramref name="relativePrefix"/> (the same <c>../</c> math the nav uses) so it points at the
    /// output-root <c>about.html</c> correctly from a nested page (e.g. <c>adrs/index.html</c>). Root pages pass the
    /// empty default. [Story 4.8 Task 5; About polish; Story 8.2]</summary>
    public static string RenderFooter(string relativePrefix = "")
    {
        // Routed through the single PortalDates formatter (Story 10.4 "one date token"): 24-hour clock + an
        // explicit machine-local zone label, so this generation clock is self-describing and distinguishable from
        // the git-commit clock (which stays in each commit's authored offset). The zone/time legitimately varies per
        // generating machine — the golden fingerprint normalizes the footer clock to keep output portable.
        var now = DateTime.Now;
        var generatedOn = PortalDates.Timestamp(now, PortalDates.LocalZoneLabel(now));
        return StatusStyles.LegendKey()
            + $"<footer class=\"doc-footer\">\n  Generated using <a href=\"{Html(RepositoryUrl)}\">SpecScribe</a> on {generatedOn} &middot; <a href=\"{Html(relativePrefix + SiteNav.AboutOutputPath)}\">View generation details</a>\n</footer>\n\n";
    }

    // Singleline so a tag whose attributes contain newlines still strips cleanly — e.g. an "(AC: #N)"
    // reference linkified inside a heading carries a multi-line `title` (the criterion's Given/When/Then
    // text), and without Singleline the `.` would stop at the first newline and leave the raw tag behind.
    private static readonly Regex TagStripRegex = new("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Strips HTML tags AND decodes entities, leaving true plain text — used where
    /// already-rendered inline HTML (e.g. an epic title with an embedded &lt;code&gt; span) needs to appear
    /// in a context that will be re-encoded (page titles, breadcrumbs, SVG tooltips). Without the decode,
    /// "&amp;amp;" in the source would double-encode to a literal "&amp;amp;amp;" on screen.</summary>
    public static string StripHtmlTags(string html) => WebUtility.HtmlDecode(TagStripRegex.Replace(html, string.Empty));
}
