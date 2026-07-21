using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the generated date page: readable heading, per-commit rows (hash/time/author/subject,
/// escaped), the guarded per-commit hash link, the "Artifacts updated" section, artifact-only days, the inline
/// sibling pager, and the site a11y contract.</summary>
public class CommitDayTemplaterTests
{
    private static readonly IReadOnlyList<(string Label, string Href)> NoArtifacts = Array.Empty<(string, string)>();

    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static CommitInfo C(string hash, string subject, string author = "Alice", string time = "14:32") =>
        new(hash, subject, author, time);

    [Fact]
    public void RenderPage_ShowsReadableTitleAndCommitRows()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[]
        {
            C("abc1234", "Fix the thing", "Matt Eland", "09:15"),
            C("def5678", "Add the other thing"),
        };

        var html = CommitDayTemplater.RenderPage(day, commits, NoArtifacts, EntityPager.None, Nav());

        Assert.Contains($"<title>Commits on {Charts.DReadable(day)} — SpecScribe</title>", html);
        Assert.Contains($"<h1>Commits on {Charts.DReadable(day)}</h1>", html);
        Assert.Contains("<code class=\"commit-hash\">abc1234</code>", html);
        Assert.Contains("<span class=\"commit-time\">09:15</span>", html);
        Assert.Contains("<span class=\"commit-author\">Matt Eland</span>", html);
        Assert.Contains("<span class=\"commit-subject\">Fix the thing</span>", html);
        // Site a11y contract: skip-link first, single main landmark.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
    }

    [Fact]
    public void RenderPage_EscapesCommitFields()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[] { C("abc1234", "fix <div> & \"q\"", author: "<b>Bob</b>") };

        var html = CommitDayTemplater.RenderPage(day, commits, NoArtifacts, EntityPager.None, Nav());

        Assert.Contains("fix &lt;div&gt; &amp; &quot;q&quot;", html);
        Assert.Contains("&lt;b&gt;Bob&lt;/b&gt;", html);
        Assert.DoesNotContain("fix <div>", html);
    }

    [Fact]
    public void RenderPage_RendersSuppliedPagerInHeader_NotAtFoot()
    {
        var day = new DateOnly(2026, 7, 6);
        // The generator supplies a newest-first pager (Prev = newer day, Next = older); the templater just renders it.
        var pager = new EntityPager(
            new PagerLink("2026-07-09.html", Charts.DReadable(new DateOnly(2026, 7, 9))),
            new PagerLink("2026-07-04.html", Charts.DReadable(new DateOnly(2026, 7, 4))));

        var html = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, NoArtifacts, pager, Nav());

        // Inline header pager, with sibling names as tooltips; the retired bottom nav is gone.
        Assert.Contains("<nav class=\"entity-pager\" aria-label=\"Sibling navigation\">", html);
        Assert.Contains("href=\"2026-07-09.html\"", html);
        Assert.Contains("&lsaquo; Prev", html);
        Assert.Contains("href=\"2026-07-04.html\"", html);
        Assert.Contains("Next &rsaquo;", html);
        Assert.DoesNotContain("commit-day-nav", html);
        // The pager sits inside the header (before the kicker), not after the article.
        Assert.True(html.IndexOf("entity-pager", StringComparison.Ordinal) < html.IndexOf("story-kicker", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderPage_WithPager_RoutesThroughSiteNavRenderWayfinding()
    {
        // Story 10.11: the sibling pager rides SiteNav.RenderWayfinding's coherent strip alongside the
        // breadcrumb, not the body's own header — confirms this non-PageView templater's call-site wiring.
        var day = new DateOnly(2026, 7, 6);
        var pager = new EntityPager(
            new PagerLink("2026-07-09.html", "Jul 9"),
            new PagerLink("2026-07-04.html", "Jul 4"));

        var html = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, NoArtifacts, pager, Nav());

        Assert.Contains("<div class=\"page-wayfinding\">", html);
        var wrapperIdx = html.IndexOf("page-wayfinding", StringComparison.Ordinal);
        var crumbIdx = html.IndexOf("class=\"breadcrumb\"", StringComparison.Ordinal);
        var pagerIdx = html.IndexOf("class=\"entity-pager\"", StringComparison.Ordinal);
        Assert.True(wrapperIdx < crumbIdx && crumbIdx < pagerIdx, "expected wrapper, then breadcrumb, then pager");
    }

    [Fact]
    public void RenderPage_NoPager_OmitsPagerEntirely()
    {
        var day = new DateOnly(2026, 7, 6);

        var html = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, NoArtifacts, EntityPager.None, Nav());

        Assert.DoesNotContain("entity-pager", html);
        Assert.DoesNotContain("commit-day-nav", html);
    }

    [Fact]
    public void RenderPage_GuardedCommitHashLink_LinksWhenPageExists_PlainOtherwise()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[] { C("abc1234", "x") };

        var linked = CommitDayTemplater.RenderPage(
            day, commits, NoArtifacts, EntityPager.None, Nav(), commitHref: _ => "commit/abc1234def.html");
        // Nested page prepends its own ../ prefix; the <code> stays for styling.
        Assert.Contains("<a class=\"commit-hash-link\" href=\"../commit/abc1234def.html\"><code class=\"commit-hash\">abc1234</code></a>", linked);

        var plain = CommitDayTemplater.RenderPage(day, commits, NoArtifacts, EntityPager.None, Nav(), commitHref: _ => null);
        Assert.Contains("<code class=\"commit-hash\">abc1234</code>", plain);
        Assert.DoesNotContain("commit-hash-link", plain);
    }

    [Fact]
    public void RenderPage_ArtifactsUpdatedSection_RendersLinksAndEscapesLabels()
    {
        var day = new DateOnly(2026, 7, 6);
        var artifacts = new (string Label, string Href)[]
        {
            ("Epics & Stories", "epics.html"),
            ("PRD", "planning-artifacts/PRD.html"),
        };

        var html = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, artifacts, EntityPager.None, Nav());

        Assert.Contains("<section class=\"artifacts-updated\">", html);
        Assert.Contains("<h2>Artifacts updated</h2>", html);
        // Output-root-relative hrefs get the nested page's ../ prefix; labels are escaped. Each entry also carries
        // a muted, unprefixed href line beneath the label so two same-titled artifacts stay distinguishable
        // (spec-7-3-deferred-debt-cleanup).
        Assert.Contains("<li><a href=\"../epics.html\">Epics &amp; Stories</a>"
            + "<span class=\"artifact-update-path\">epics.html</span></li>", html);
        Assert.Contains("<li><a href=\"../planning-artifacts/PRD.html\">PRD</a>"
            + "<span class=\"artifact-update-path\">planning-artifacts/PRD.html</span></li>", html);
        // Both a commit pill and an artifacts pill are present when the day has both.
        Assert.Contains("1 commit</span>", html);
        Assert.Contains("2 artifacts updated</span>", html);
    }

    [Fact]
    public void RenderPage_NoArtifacts_OmitsSection()
    {
        var day = new DateOnly(2026, 7, 6);

        var html = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, NoArtifacts, EntityPager.None, Nav());

        Assert.DoesNotContain("artifacts-updated", html);
        Assert.DoesNotContain("Artifacts updated", html);
    }

    [Fact]
    public void RenderPage_ArtifactOnlyDay_NeutralHeading_NoCommitList()
    {
        var day = new DateOnly(2026, 7, 6);
        var artifacts = new (string Label, string Href)[] { ("Architecture", "planning-artifacts/ARCHITECTURE.html") };

        var html = CommitDayTemplater.RenderPage(day, Array.Empty<CommitInfo>(), artifacts, EntityPager.None, Nav());

        // Neutral "Activity on …" heading — never claims commits it doesn't have.
        Assert.Contains($"<h1>Activity on {Charts.DReadable(day)}</h1>", html);
        Assert.Contains($"<title>Activity on {Charts.DReadable(day)} — SpecScribe</title>", html);
        Assert.Contains("<div class=\"story-kicker\">Activity</div>", html);
        // No commit list at all (no empty "0 commits" list).
        Assert.DoesNotContain("commit-day-list", html);
        Assert.DoesNotContain("commit</span>", html);
        // The artifacts section still renders.
        Assert.Contains("<section class=\"artifacts-updated\">", html);
        Assert.Contains("1 artifact updated</span>", html);
    }
}
