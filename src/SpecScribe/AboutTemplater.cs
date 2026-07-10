using System.Reflection;
using System.Text;

namespace SpecScribe;

/// <summary>SpecScribe's own product metadata, read via reflection from the assembly attributes the SDK
/// generates from the csproj (<c>Version</c> → <see cref="AssemblyInformationalVersionAttribute"/>,
/// <c>Authors</c> → <see cref="AssemblyCompanyAttribute"/>, <c>Description</c> →
/// <see cref="AssemblyDescriptionAttribute"/>). Read from the assembly rather than re-declared as literals so
/// the About page can never drift from the package (single source of truth = the csproj). [Story 4.8 Task 5]</summary>
/// <param name="Version">The product version, with any deterministic-build <c>+commit</c> suffix trimmed.</param>
/// <param name="Description">The package description.</param>
/// <param name="Author">The package author.</param>
/// <param name="RepositoryUrl">The canonical repository URL, shared with the footer via <see cref="PathUtil.RepositoryUrl"/>.</param>
public sealed record ProductMetadata(string Version, string Description, string Author, string RepositoryUrl)
{
    /// <summary>Reads the metadata off the SpecScribe assembly's generated attributes. Local-first: pure
    /// reflection on already-loaded assembly metadata — no file I/O, no network. [Story 4.8 Task 5]</summary>
    public static ProductMetadata FromAssembly()
    {
        var asm = typeof(ProductMetadata).Assembly;

        // Deterministic builds (the SDK default) append "+<commit-sha>" to the informational version; trim it so
        // the About page shows a clean "0.1.0" rather than the build's git hash.
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational is { Length: > 0 } v
            ? v.Split('+', 2)[0]
            : asm.GetName().Version?.ToString() ?? "unknown";

        var description = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
        var author = asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

        return new ProductMetadata(version, description, author, PathUtil.RepositoryUrl);
    }
}

/// <summary>Renders the About page (<c>about.html</c>): SpecScribe's own product metadata (version,
/// description, author, repository link) plus a prominent link to the diagnostics run log. It is the
/// owner-chosen reachability path for the diagnostics page — linked from the site-wide footer, and linking on
/// to <see cref="SiteNav.DiagnosticsOutputPath"/>. A small, static informational page (no JS). [Story 4.8 Task 5]</summary>
public static class AboutTemplater
{
    public static string RenderPage(SiteNav nav)
    {
        var outputPath = SiteNav.AboutOutputPath; // "" prefix — about.html is at the output root.
        var meta = ProductMetadata.FromAssembly();

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"About — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            $"About SpecScribe, the generator behind {nav.SiteTitle}'s documentation portal."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("About", null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>About SpecScribe</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; generated with SpecScribe v{PathUtil.Html(meta.Version)}</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<section class=\"chart-panel about-panel\">\n");
        if (meta.Description is { Length: > 0 })
        {
            sb.Append($"  <p>{PathUtil.Html(meta.Description)}</p>\n");
        }

        sb.Append("  <dl class=\"diag-config\">\n");
        sb.Append($"    <div class=\"cap-row\"><dt>Version</dt><dd>{PathUtil.Html(meta.Version)}</dd></div>\n");
        if (meta.Author is { Length: > 0 })
        {
            sb.Append($"    <div class=\"cap-row\"><dt>Author</dt><dd>{PathUtil.Html(meta.Author)}</dd></div>\n");
        }
        sb.Append($"    <div class=\"cap-row\"><dt>Repository</dt><dd><a href=\"{PathUtil.Html(meta.RepositoryUrl)}\">{PathUtil.Html(meta.RepositoryUrl)}</a></dd></div>\n");
        sb.Append("  </dl>\n");

        // The prominent link on to the diagnostics run log — the reason About sits on the reachability path.
        sb.Append($"  <p class=\"about-diagnostics-link\"><a href=\"{SiteNav.DiagnosticsOutputPath}\">View the generation diagnostics &amp; run log &rarr;</a></p>\n");
        sb.Append("</section>\n");
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
