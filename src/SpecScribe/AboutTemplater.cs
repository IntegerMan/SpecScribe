using System.Reflection;
using System.Text;

namespace SpecScribe;

/// <summary>SpecScribe's own product metadata, read via reflection from the assembly attributes the SDK
/// generates from the csproj (<c>Version</c> → <see cref="AssemblyInformationalVersionAttribute"/>,
/// <c>Authors</c> → <see cref="AssemblyCompanyAttribute"/>, <c>Description</c> →
/// <see cref="AssemblyDescriptionAttribute"/>). Read from the assembly rather than re-declared as literals so
/// the About page can never drift from the package (single source of truth = the csproj). [Story 4.8 Task 5]</summary>
/// <param name="Version">The product version, KEEPING any pre-release label (e.g. <c>0.1.0-preview</c>) but with
/// the deterministic-build <c>+commit</c> build-metadata suffix split off into <paramref name="CommitHash"/>.</param>
/// <param name="Description">The package description.</param>
/// <param name="Author">The package author.</param>
/// <param name="RepositoryUrl">The canonical repository URL, shared with the footer via <see cref="PathUtil.RepositoryUrl"/>.</param>
/// <param name="AuthorUrl">The author's homepage, from <see cref="PathUtil.AuthorUrl"/>.</param>
/// <param name="CommitHash">The short (7-char) git commit the build came from, recovered from the informational
/// version's <c>+</c> suffix — or null when the build carries no revision (e.g. built outside a git checkout).</param>
/// <param name="BuildDate">The build date (UTC, <c>yyyy-MM-dd</c>) stamped into assembly metadata by the csproj —
/// or null when the attribute is absent.</param>
public sealed record ProductMetadata(
    string Version, string Description, string Author, string RepositoryUrl,
    string AuthorUrl, string? CommitHash, string? BuildDate)
{
    /// <summary>True when the version carries a pre-release label (a semver <c>-suffix</c>) — drives the About
    /// page's "Preview" badge.</summary>
    public bool IsPrerelease => Version.Contains('-');

    /// <summary>The dynamic build identifier, <c>"{date} · {hash}"</c>, from whichever of the two build stamps
    /// are present — or null when neither is, so the About page can omit the Build row entirely. Best-effort by
    /// design (see <see cref="CommitHash"/> / <see cref="BuildDate"/>).</summary>
    public string? BuildLabel
    {
        get
        {
            var parts = new[] { BuildDate, CommitHash }.Where(p => p is { Length: > 0 }).ToList();
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
    }

    /// <summary>Reads the metadata off the SpecScribe assembly's generated attributes. Local-first: pure
    /// reflection on already-loaded assembly metadata — no file I/O, no network. [Story 4.8 Task 5]</summary>
    public static ProductMetadata FromAssembly()
    {
        var asm = typeof(ProductMetadata).Assembly;

        // Deterministic builds (the SDK default) append "+<commit-sha>" to the informational version. Split there:
        // the left side is the semver (KEEPING any "-preview" pre-release label); the right side, when present, is
        // the git commit — surfaced as a short hash so a generated site says exactly which build produced it.
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string version;
        string? commitHash = null;
        if (informational is { Length: > 0 } v)
        {
            var plus = v.Split('+', 2);
            version = plus[0];
            if (plus.Length == 2 && plus[1] is { Length: > 0 } sha)
            {
                commitHash = sha.Length > 7 ? sha[..7] : sha;
            }
        }
        else
        {
            version = asm.GetName().Version?.ToString() ?? "unknown";
        }

        var description = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
        var author = asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
        var buildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value;

        return new ProductMetadata(version, description, author, PathUtil.RepositoryUrl,
            PathUtil.AuthorUrl, commitHash, buildDate);
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

        // A "Preview" badge beside the title whenever the version is a pre-release, so the pre-release nature is
        // unmistakable at a glance (not just a "-preview" suffix that's easy to miss).
        var previewBadge = meta.IsPrerelease ? " <span class=\"preview-badge\">Preview</span>" : string.Empty;
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>About SpecScribe{previewBadge}</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; generated with SpecScribe v{PathUtil.Html(meta.Version)}</div>\n");
        sb.Append("</header>\n\n");

        // info-page: the shared centered content column (side gutters + max-width) these otherwise-chromeless
        // synthesized pages need — same column edge as .doc-header. [About polish]
        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append("<section class=\"chart-panel about-panel\">\n");
        if (meta.Description is { Length: > 0 })
        {
            sb.Append($"  <p>{PathUtil.Html(meta.Description)}</p>\n");
        }

        sb.Append("  <dl class=\"diag-config\">\n");
        sb.Append($"    <div class=\"cap-row\"><dt>Version</dt><dd>{PathUtil.Html(meta.Version)}</dd></div>\n");
        // The dynamic build identifier (date · short commit hash), shown only when at least one part is available.
        if (meta.BuildLabel is { Length: > 0 } build)
        {
            sb.Append($"    <div class=\"cap-row\"><dt>Build</dt><dd>{PathUtil.Html(build)}</dd></div>\n");
        }
        if (meta.Author is { Length: > 0 })
        {
            sb.Append($"    <div class=\"cap-row\"><dt>Author</dt><dd><a href=\"{PathUtil.Html(meta.AuthorUrl)}\">{PathUtil.Html(meta.Author)}</a></dd></div>\n");
        }
        sb.Append($"    <div class=\"cap-row\"><dt>Repository</dt><dd><a href=\"{PathUtil.Html(meta.RepositoryUrl)}\">{PathUtil.Html(meta.RepositoryUrl)}</a></dd></div>\n");
        sb.Append("  </dl>\n");

        // The prominent link on to the diagnostics run log — the reason About sits on the reachability path.
        sb.Append($"  <p class=\"about-diagnostics-link\"><a href=\"{SiteNav.DiagnosticsOutputPath}\">View the generation diagnostics &amp; run log &rarr;</a></p>\n");
        sb.Append("</section>\n");
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
