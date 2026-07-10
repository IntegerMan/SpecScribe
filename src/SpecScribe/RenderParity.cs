using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One nav entry as a semantic fact: label + drill target + whether it is the active page.</summary>
public sealed record NavFact(string Label, string Target, bool Active);

/// <summary>One breadcrumb entry as a semantic fact: label + target (null for the current page).</summary>
public sealed record CrumbFact(string Label, string? Target);

/// <summary>The host-neutral MEANING of a rendered page, distilled to the facts a delivery surface must
/// reproduce: the nav graph (ordered targets + labels + which is active), the breadcrumb/drill trail, the
/// drill-up parent and drill-down child targets, the page's status stage, the asset hrefs, and whether a mermaid
/// diagram is present. Two forms exist per page: what the <see cref="PageView"/> DECLARES
/// (<see cref="RenderParity.FromPageView"/>) and what a surface's rendered output EVIDENCES
/// (<see cref="RenderParity.Extract"/>). Parity means the two are equal — the surface dropped and reinterpreted
/// nothing. Deliberately semantic, NOT a byte differ (the golden test covers bytes): a surface that emits
/// different markup but the same meaning still matches. [Story 6.1]</summary>
public sealed record SemanticFacts
{
    public required string SiteTitle { get; init; }
    public required IReadOnlyList<NavFact> Nav { get; init; }
    public required IReadOnlyList<CrumbFact> Breadcrumb { get; init; }
    public required string? ParentDrillTarget { get; init; }
    public required IReadOnlyList<string> ChildDrillTargets { get; init; }
    public required string? StatusStage { get; init; }
    public required string Stylesheet { get; init; }
    public required string Script { get; init; }
    public required bool MermaidPresent { get; init; }
}

/// <summary>The AC #2 semantic-parity harness: it extracts the semantic facts back out of a surface's rendered
/// output and asserts they equal the source <see cref="PageView"/>'s view models — proving the adapter neither
/// dropped nor reinterpreted a fact. Story 6.1 runs it against the <see cref="HtmlRenderAdapter"/> (the only
/// surface, so the view models ARE the reference); it is THE hook a future <see cref="IRenderAdapter"/> (6.2's
/// webview) runs against — assert ITS extracted facts against the same reference, minus any registered
/// <see cref="HostRenderException"/>. A divergence not covered by an exception is a bug (AC #2). [Story 6.1]</summary>
public static class RenderParity
{
    // The chrome regions the facts are recovered from. Scoped to their container so the footer's own <a>s (the
    // SpecScribe credit + About link) can't be mistaken for nav/breadcrumb entries.
    private static readonly Regex NavRegion = new("<nav class=\"site-nav\".*?</nav>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex NavLinks = new("<div class=\"site-nav-links\".*?</div>\\s*</div>\\s*</nav>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex BreadcrumbRegion = new("<div class=\"breadcrumb\".*?</div>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Brand = new("<span class=\"site-nav-brand\">(?<t>.*?)</span>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Anchor = new("<a href=\"(?<href>[^\"]*)\"(?<attrs>[^>]*)>(?<content>.*?)</a>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex CrumbEntry = new("<a href=\"(?<href>[^\"]*)\">(?<label>.*?)</a>|<span class=\"crumb-current\"[^>]*>(?<current>.*?)</span>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Stylesheet = new("<link rel=\"stylesheet\" href=\"(?<href>[^\"]*)\">", RegexOptions.Compiled);
    private static readonly Regex Script = new("<script src=\"(?<href>[^\"]*)\" defer>", RegexOptions.Compiled);

    /// <summary>The facts a <see cref="PageView"/> DECLARES — the reference every surface is checked against.</summary>
    public static SemanticFacts FromPageView(PageView page) => new()
    {
        SiteTitle = page.Nav.SiteTitle,
        Nav = page.Nav.Items
            .Select(i => new NavFact(i.Label, NormalizeTarget(i.OutputRelativePath),
                PathsEqual(i.OutputRelativePath, page.Nav.ActiveOutputRelativePath)))
            .ToList(),
        Breadcrumb = page.Breadcrumb.Crumbs
            .Select(c => new CrumbFact(c.Label, c.OutputRelativePath is null ? null : NormalizeTarget(c.OutputRelativePath)))
            .ToList(),
        ParentDrillTarget = page.Interaction.ParentTarget is { } p ? NormalizeTarget(p) : null,
        ChildDrillTargets = page.Interaction.ChildTargets.Select(NormalizeTarget).ToList(),
        StatusStage = page.Interaction.StatusStage,
        Stylesheet = NormalizeTarget(page.Assets.StylesheetHref),
        Script = NormalizeTarget(page.Assets.ScriptHref),
        MermaidPresent = page.Assets.MermaidNeeded,
    };

    /// <summary>The facts a surface's rendered <paramref name="html"/> EVIDENCES. The <paramref name="reference"/>
    /// supplies the checklist of drill children / status stage to look for, so a fact the output DROPPED (a child
    /// link that isn't there, a status badge that isn't rendered) simply doesn't appear here and the comparison
    /// with <see cref="FromPageView"/> flags it. [Story 6.1]</summary>
    public static SemanticFacts Extract(string html, PageView reference)
    {
        var brandMatch = Brand.Match(html);
        var siteTitle = brandMatch.Success ? PathUtil.StripHtmlTags(brandMatch.Groups["t"].Value) : string.Empty;

        var nav = ExtractNav(html);
        var breadcrumb = ExtractBreadcrumb(html);
        var parent = breadcrumb.LastOrDefault(c => c.Target is not null)?.Target;

        var cssMatch = Stylesheet.Match(html);
        var scriptMatch = Script.Match(html);

        return new SemanticFacts
        {
            SiteTitle = siteTitle,
            Nav = nav,
            Breadcrumb = breadcrumb,
            ParentDrillTarget = parent,
            // Only children whose target actually appears in the rendered output survive — a dropped child is
            // absent here, so it differs from the reference's full set.
            ChildDrillTargets = reference.Interaction.ChildTargets
                .Where(c => html.Contains(NormalizeTarget(c), StringComparison.Ordinal))
                .Select(NormalizeTarget)
                .ToList(),
            // The status stage is evidenced only if the page actually renders a badge carrying it.
            StatusStage = reference.Interaction.StatusStage is { } s && html.Contains($"status-badge {s}", StringComparison.Ordinal) ? s : null,
            Stylesheet = cssMatch.Success ? NormalizeTarget(cssMatch.Groups["href"].Value) : string.Empty,
            Script = scriptMatch.Success ? NormalizeTarget(scriptMatch.Groups["href"].Value) : string.Empty,
            MermaidPresent = html.Contains("mermaid.initialize", StringComparison.Ordinal),
        };
    }

    /// <summary>Compares what a <see cref="PageView"/> declares to what <paramref name="html"/> evidences and
    /// returns one entry per diverging semantic fact (empty = full parity). A divergence whose fact id is a
    /// registered <see cref="HostRenderException"/> for <paramref name="surfaceId"/> is filtered out (a sanctioned
    /// host-specific exception, not a bug). [Story 6.1]</summary>
    public static IReadOnlyList<string> FindDivergences(
        PageView page, string html, string surfaceId, IReadOnlyList<HostRenderException>? exceptions = null)
    {
        var expected = FromPageView(page);
        var actual = Extract(html, page);
        var excepted = (exceptions ?? HostRenderExceptions.Registry)
            .Where(e => e.SurfaceId == surfaceId)
            .Select(e => e.FactId)
            .ToHashSet(StringComparer.Ordinal);

        var divergences = new List<string>();
        void Check(string factId, bool equal, Func<string> detail)
        {
            if (!equal && !excepted.Contains(factId)) divergences.Add($"{factId}: {detail()}");
        }

        Check("siteTitle", expected.SiteTitle == actual.SiteTitle, () => $"'{expected.SiteTitle}' vs '{actual.SiteTitle}'");
        Check("nav", NavTargetsEqual(expected.Nav, actual.Nav), () => $"expected [{Describe(expected.Nav)}] got [{Describe(actual.Nav)}]");
        Check("nav.active", ActiveEqual(expected.Nav, actual.Nav), () => $"expected active [{ActiveTargets(expected.Nav)}] got [{ActiveTargets(actual.Nav)}]");
        Check("breadcrumb", CrumbsEqual(expected.Breadcrumb, actual.Breadcrumb), () => $"expected [{Describe(expected.Breadcrumb)}] got [{Describe(actual.Breadcrumb)}]");
        Check("drill.parent", expected.ParentDrillTarget == actual.ParentDrillTarget, () => $"'{expected.ParentDrillTarget}' vs '{actual.ParentDrillTarget}'");
        Check("drill.child", expected.ChildDrillTargets.SequenceEqual(actual.ChildDrillTargets), () => $"missing [{string.Join(", ", expected.ChildDrillTargets.Except(actual.ChildDrillTargets))}]");
        Check("status", expected.StatusStage == actual.StatusStage, () => $"'{expected.StatusStage}' vs '{actual.StatusStage}'");
        Check("asset.css", expected.Stylesheet == actual.Stylesheet, () => $"'{expected.Stylesheet}' vs '{actual.Stylesheet}'");
        Check("asset.js", expected.Script == actual.Script, () => $"'{expected.Script}' vs '{actual.Script}'");
        Check("mermaid", expected.MermaidPresent == actual.MermaidPresent, () => $"{expected.MermaidPresent} vs {actual.MermaidPresent}");
        return divergences;
    }

    private static IReadOnlyList<NavFact> ExtractNav(string html)
    {
        var region = NavRegion.Match(html);
        if (!region.Success) return Array.Empty<NavFact>();
        // Scope anchors to the links container so the brand/toggle (not anchors anyway) and any sibling markup
        // never leak in.
        var links = NavLinks.Match(region.Value);
        var scope = links.Success ? links.Value : region.Value;

        var facts = new List<NavFact>();
        foreach (Match m in Anchor.Matches(scope))
        {
            var target = NormalizeTarget(m.Groups["href"].Value);
            var label = PathUtil.StripHtmlTags(m.Groups["content"].Value);
            var active = m.Groups["attrs"].Value.Contains("active", StringComparison.Ordinal);
            facts.Add(new NavFact(label, target, active));
        }
        return facts;
    }

    private static IReadOnlyList<CrumbFact> ExtractBreadcrumb(string html)
    {
        var region = BreadcrumbRegion.Match(html);
        if (!region.Success) return Array.Empty<CrumbFact>();

        var crumbs = new List<CrumbFact>();
        foreach (Match m in CrumbEntry.Matches(region.Value))
        {
            if (m.Groups["current"].Success)
            {
                crumbs.Add(new CrumbFact(PathUtil.StripHtmlTags(m.Groups["current"].Value), null));
            }
            else
            {
                crumbs.Add(new CrumbFact(PathUtil.StripHtmlTags(m.Groups["label"].Value), NormalizeTarget(m.Groups["href"].Value)));
            }
        }
        return crumbs;
    }

    /// <summary>Folds a rendered href back to its output-relative target: normalize slashes, drop the
    /// <c>?v=</c> cache-bust token, and strip the leading <c>../</c> prefix segments so a link rendered from a
    /// nested page compares equal to the unprefixed view-model target. [Story 6.1]</summary>
    private static string NormalizeTarget(string href)
    {
        var p = PathUtil.NormalizeSlashes(href);
        var q = p.IndexOf('?');
        if (q >= 0) p = p[..q];
        while (p.StartsWith("../", StringComparison.Ordinal)) p = p[3..];
        return p;
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(NormalizeTarget(a), NormalizeTarget(b), StringComparison.OrdinalIgnoreCase);

    private static bool NavTargetsEqual(IReadOnlyList<NavFact> a, IReadOnlyList<NavFact> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Label == p.Second.Label && p.First.Target == p.Second.Target);

    private static bool ActiveEqual(IReadOnlyList<NavFact> a, IReadOnlyList<NavFact> b) =>
        ActiveTargets(a) == ActiveTargets(b);

    private static bool CrumbsEqual(IReadOnlyList<CrumbFact> a, IReadOnlyList<CrumbFact> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Label == p.Second.Label && p.First.Target == p.Second.Target);

    private static string ActiveTargets(IReadOnlyList<NavFact> nav) =>
        string.Join(",", nav.Where(n => n.Active).Select(n => n.Target));

    private static string Describe(IReadOnlyList<NavFact> nav) =>
        string.Join(", ", nav.Select(n => $"{n.Label}→{n.Target}"));

    private static string Describe(IReadOnlyList<CrumbFact> crumbs) =>
        string.Join(", ", crumbs.Select(c => $"{c.Label}→{c.Target ?? "(current)"}"));
}
