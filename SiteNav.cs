using System.Text;

namespace DocsForge;

/// <summary>The site-wide header nav (Home, GDD, Narrative, Game Architecture, Epics) plus breadcrumb rendering.
/// Nav targets are discovered by well-known filename, so a missing doc is simply omitted rather than
/// producing a broken link.</summary>
public sealed class SiteNav
{
    public const string HomeOutputPath = "index.html";
    public const string EpicsOutputPath = "epics.html";
    public const string RequirementsOutputPath = "requirements.html";

    public required IReadOnlyList<(string Label, string OutputRelativePath)> Items { get; init; }

    /// <summary>The project name (from _bmad/config.toml) — used for the nav brand and page-title suffixes.</summary>
    public required string SiteTitle { get; init; }

    public string Brand => $"{SiteTitle} · Live Docs";

    public bool HasEpics => Items.Any(i => i.Label == "Epics");

    public static SiteNav Build(IReadOnlyList<string> sourceRelativePaths, string siteTitle)
    {
        var items = new List<(string, string)> { ("Home", HomeOutputPath) };

        void AddIfFound(string filename, string label)
        {
            var match = sourceRelativePaths.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p), filename, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                items.Add((label, PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(match))));
            }
        }

        AddIfFound("gdd.md", "GDD");
        AddIfFound("narrative-design.md", "Narrative");
        AddIfFound("game-architecture.md", "Game Architecture");

        if (sourceRelativePaths.Any(p => string.Equals(Path.GetFileName(p), "epics.md", StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(("Epics", EpicsOutputPath));
            // Requirements are parsed out of epics.md, so they share its availability guard.
            items.Add(("Requirements", RequirementsOutputPath));
        }

        return new SiteNav { Items = items, SiteTitle = siteTitle };
    }

    public string RenderNavBar(string currentOutputRelativePath)
    {
        var prefix = PathUtil.RelativePrefix(currentOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(currentOutputRelativePath);

        var sb = new StringBuilder();
        sb.Append("<nav class=\"site-nav\" aria-label=\"Document navigation\">\n");
        sb.Append($"  <span class=\"site-nav-brand\">{PathUtil.Html(Brand)}</span>\n");
        sb.Append("  <div class=\"site-nav-links\">\n");
        foreach (var (label, outputPath) in Items)
        {
            var href = prefix + outputPath;
            var isActive = string.Equals(PathUtil.NormalizeSlashes(outputPath), current, StringComparison.OrdinalIgnoreCase);
            var attrs = isActive ? " class=\"active\" aria-current=\"page\"" : string.Empty;
            sb.Append($"    <a href=\"{PathUtil.Html(href)}\"{attrs}>{PathUtil.Html(label)}</a>\n");
        }
        sb.Append("  </div>\n</nav>\n\n");
        return sb.ToString();
    }

    /// <summary>Renders a "Home / Epics / Epic 1 / Story 1.1" trail. The last entry (current page) should
    /// have a null path so it renders as plain text rather than a self-link.</summary>
    public static string RenderBreadcrumb(string currentOutputRelativePath, IReadOnlyList<(string Label, string? OutputRelativePath)> trail)
    {
        if (trail.Count == 0) return string.Empty;
        var prefix = PathUtil.RelativePrefix(currentOutputRelativePath);

        var sb = new StringBuilder();
        sb.Append("<div class=\"breadcrumb\" aria-label=\"Breadcrumb\">\n");
        for (var i = 0; i < trail.Count; i++)
        {
            if (i > 0) sb.Append("  <span class=\"crumb-sep\">/</span>\n");
            var (label, path) = trail[i];
            if (path is not null)
            {
                sb.Append($"  <a href=\"{PathUtil.Html(prefix + path)}\">{PathUtil.Html(label)}</a>\n");
            }
            else
            {
                sb.Append($"  <span class=\"crumb-current\">{PathUtil.Html(label)}</span>\n");
            }
        }
        sb.Append("</div>\n\n");
        return sb.ToString();
    }
}
