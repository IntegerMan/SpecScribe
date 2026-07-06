using System.Text;

namespace SpecScribe;

/// <summary>The site-wide header nav (Home, module planning docs, ADRs, Epics) plus breadcrumb rendering.
/// Nav targets are discovered by well-known filename, so a missing doc is simply omitted rather than
/// producing a broken link.</summary>
public sealed class SiteNav
{
    public const string HomeOutputPath = "index.html";
    public const string EpicsOutputPath = "epics.html";
    public const string RequirementsOutputPath = "requirements.html";
    public const string AdrsLandingOutputPath = "adrs/index.html";
    public const string ReadmeOutputPath = "readme.html";

    public required IReadOnlyList<(string Label, string OutputRelativePath)> Items { get; init; }

    /// <summary>Every discoverable key view for the dashboard's quick-link grid. A superset of the nav bar:
    /// it also carries module docs kept out of the top nav (brief, UX) plus a short description per entry.</summary>
    public required IReadOnlyList<(string Label, string OutputRelativePath, string Description)> QuickLinks { get; init; }

    /// <summary>The project name (from _bmad/config.toml) — used for the nav brand and page-title suffixes.</summary>
    public required string SiteTitle { get; init; }

    public string Brand => SiteTitle;

    public bool HasEpics => Items.Any(i => i.Label == "Epics");

    public bool HasAdrs => Items.Any(i => i.Label == "ADRs");

    public bool HasReadme => Items.Any(i => i.Label == "Readme");

    public static SiteNav Build(
        IReadOnlyList<string> sourceRelativePaths,
        string siteTitle,
        IReadOnlyList<ModuleDoc>? moduleDocs = null,
        bool hasAdrs = false,
        bool hasReadme = false)
    {
        var items = new List<(string, string)> { ("Home", HomeOutputPath) };
        var quickLinks = new List<(string, string, string)>();

        // The README is the project's front-door narrative, so it sits first after Home.
        if (hasReadme)
        {
            items.Add(("Readme", ReadmeOutputPath));
            quickLinks.Add(("Readme", ReadmeOutputPath, "Read the project overview."));
        }

        // Module docs (PRD/Architecture, or GDD/Narrative/etc.) are matched by filename anywhere in the
        // source tree, so a missing doc is simply skipped rather than producing a broken link. In-nav docs
        // ride the top nav; all discovered docs appear in the dashboard quick links.
        foreach (var doc in moduleDocs ?? Array.Empty<ModuleDoc>())
        {
            var match = sourceRelativePaths.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p), doc.FileName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                continue;
            }

            var outputPath = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(match));
            if (doc.InNav)
            {
                items.Add((doc.Label, outputPath));
            }

            quickLinks.Add((doc.Label, outputPath, doc.Description));
        }

        // ADRs sit next to the architecture doc conceptually; they live in docs/adrs, not _bmad-output,
        // so their availability is signalled by the caller rather than the source-file list.
        if (hasAdrs)
        {
            items.Add(("ADRs", AdrsLandingOutputPath));
        }

        var hasEpics = sourceRelativePaths.Any(p => string.Equals(Path.GetFileName(p), "epics.md", StringComparison.OrdinalIgnoreCase));
        if (hasEpics)
        {
            items.Add(("Epics", EpicsOutputPath));
            // Requirements are parsed out of epics.md, so they share its availability guard.
            items.Add(("Requirements", RequirementsOutputPath));
            quickLinks.Add(("Epics", EpicsOutputPath, "Track epic and story delivery progress."));
            quickLinks.Add(("Requirements", RequirementsOutputPath, "Review FR/NFR coverage and status."));
        }

        if (hasAdrs)
        {
            quickLinks.Add(("ADRs", AdrsLandingOutputPath, "Browse architecture decisions."));
        }

        return new SiteNav { Items = items, QuickLinks = quickLinks, SiteTitle = siteTitle };
    }

    public string RenderNavBar(string currentOutputRelativePath)
    {
        var prefix = PathUtil.RelativePrefix(currentOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(currentOutputRelativePath);

        var sb = new StringBuilder();
        sb.Append("<nav class=\"site-nav\" aria-label=\"Document navigation\">\n");
        sb.Append($"  <span class=\"site-nav-brand\">{PathUtil.Html(Brand)}</span>\n");
        sb.Append("  <button class=\"site-nav-toggle\" type=\"button\" aria-label=\"Toggle navigation\" aria-controls=\"site-nav-links\" aria-expanded=\"false\">Menu</button>\n");
        sb.Append("  <div class=\"site-nav-links\" id=\"site-nav-links\">\n");
        foreach (var (label, outputPath) in Items)
        {
            var href = prefix + outputPath;
            var isActive = string.Equals(PathUtil.NormalizeSlashes(outputPath), current, StringComparison.OrdinalIgnoreCase);
            var attrs = isActive ? " class=\"active\" aria-current=\"page\"" : string.Empty;
            sb.Append($"    <a href=\"{PathUtil.Html(href)}\"{attrs}>{PathUtil.Html(label)}</a>\n");
        }
        sb.Append("  </div>\n</nav>\n");
        sb.Append("<script>(function(){var script=document.currentScript;if(!script)return;var nav=script.previousElementSibling;if(!nav||!nav.classList.contains('site-nav'))return;var toggle=nav.querySelector('.site-nav-toggle');var links=nav.querySelector('.site-nav-links');if(!toggle||!links)return;var mq=window.matchMedia('(max-width: 640px)');function closeNav(){nav.classList.remove('site-nav-open');toggle.setAttribute('aria-expanded','false');}function openNav(){nav.classList.add('site-nav-open');toggle.setAttribute('aria-expanded','true');var first=links.querySelector('a');if(first)first.focus();}toggle.addEventListener('click',function(){if(nav.classList.contains('site-nav-open')){closeNav();}else{openNav();}});links.querySelectorAll('a').forEach(function(link){link.addEventListener('click',function(){if(mq.matches){closeNav();}});});nav.addEventListener('keydown',function(evt){if(evt.key==='Escape'&&nav.classList.contains('site-nav-open')){evt.preventDefault();closeNav();toggle.focus();}});window.addEventListener('resize',function(){if(!mq.matches){closeNav();}});})();</script>\n\n");
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
                sb.Append($"  <span class=\"crumb-current\" aria-current=\"page\">{PathUtil.Html(label)}</span>\n");
            }
        }
        sb.Append("</div>\n\n");
        return sb.ToString();
    }
}
