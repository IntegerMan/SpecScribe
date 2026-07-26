using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for <c>design-system.html</c> — the portal's own design-system
/// reference: the <c>--status-*</c> / <c>--motion-*</c> token families and the shared visual primitives.
/// Written on EVERY full run (like about/how-to-read/diagnostics) so its Help-nav link can never dangle.
/// Follows the temp-dir fixture style of <see cref="SiteGeneratorHowToReadTests"/>. [Story 23.2 AC #6]</summary>
public class SiteGeneratorDesignSystemTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-designsystem-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    public SiteGeneratorDesignSystemTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"),
            "# Epics\n\n## Epic List\n\n### Epic 1: Foundation\n\nStand up the portal.\n\n## Epic 1: Foundation\n\n### Story 1.1: Foundation Story\n\nAs a maintainer, I want the foundation.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private string Generate()
    {
        new SiteGenerator(Options()).GenerateAll();
        return File.ReadAllText(Path.Combine(Site, SiteNav.DesignSystemOutputPath));
    }

    [Fact]
    public void GenerateAll_WritesDesignSystemOnEveryRun_ReachableFromHelpNav()
    {
        var html = Generate();

        Assert.True(File.Exists(Path.Combine(Site, SiteNav.DesignSystemOutputPath)));
        Assert.Contains("<h1>Design System</h1>", html);

        // The Help nav group + the dashboard's Help quick-links band both carry it, on every page.
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains($"href=\"{SiteNav.DesignSystemOutputPath}\"", index);
        Assert.Contains("Design System", index);
    }

    [Fact]
    public void DesignSystem_NavEntry_ResolvesToAWrittenFile()
    {
        // Nav coherence: every Help child must point at a file this run actually produced. A dangling Help
        // link is the exact failure the always-written guarantee exists to prevent.
        new SiteGenerator(Options()).GenerateAll();
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe");

        var entry = Assert.Single(nav.Items, i => i.Label == "Design System");
        Assert.Equal(SiteNav.DesignSystemOutputPath, entry.OutputRelativePath);
        Assert.True(File.Exists(Path.Combine(Site, entry.OutputRelativePath)));
        Assert.Contains(nav.QuickLinks, q => q.OutputRelativePath == SiteNav.DesignSystemOutputPath && q.Group == "Help");
    }

    [Fact]
    public void DesignSystem_DocumentsEveryCanonicalStatusStage_ByNameNotColourAlone()
    {
        var html = Generate();

        foreach (var stage in StatusStyles.LegendStages)
        {
            // The token NAME (so a component author knows what to reference)...
            if (stage is not ("unmapped" or "retired"))
            {
                Assert.Contains($"--status-{stage}", html);
            }
            // ...the human WORD (UX-DR17: never colour alone)...
            Assert.Contains(PathUtil.Html(StatusStyles.LegendWord(stage)), html);
            // ...and the plain-language meaning, from the same seam the portal's legend uses.
            Assert.Contains(PathUtil.Html(StatusStyles.StageMeaning(stage)), html);
        }
    }

    [Fact]
    public void DesignSystem_DocumentsTheMotionTokenFamily()
    {
        var html = Generate();

        foreach (var token in new[]
                 {
                     "--motion-fast", "--motion-entrance", "--motion-entrance-long",
                     "--motion-ease", "--motion-stagger",
                 })
        {
            Assert.Contains(token, html);
        }

        // The reduce contract is stated, not just implied — a reader must learn that motion has an opt-out.
        Assert.Contains("prefers-reduced-motion", html);
    }

    [Fact]
    public void DesignSystem_ShowsTheSharedPrimitives()
    {
        var html = Generate();

        // Status badge, list row, and the framed chart panel — the primitives 23.3 consumes.
        Assert.Contains("status-badge", html);
        Assert.Contains("list-row", html);
        Assert.Contains("chart-frame-head", html);
        Assert.Contains("chart-frame-why", html);
        Assert.Contains("list-row-primary", html);
        Assert.Contains("list-row-chip", html);
    }

    [Fact]
    public void DesignSystem_IsBuiltFromTheRealPrimitives_NotLookAlikeMarkup()
    {
        // The load-bearing property of this page: a gallery that mocked up its own badges and rows could drift
        // from the real ones the moment either changed, and a design-system page that misrepresents the design
        // system is worse than having none. Asserting the EXACT primitive output — not just its class names —
        // is what makes that impossible.
        var html = Generate();

        Assert.Contains(StatusStyles.Badge("done", StatusStyles.LegendWord("done")), html);
        Assert.Contains(StatusStyles.Badge("review", StatusStyles.LegendWord("review")), html);
        Assert.Contains(ListRow.Chip("Epic 1"), html);
        Assert.Contains(ListRow.PrimaryLink(SiteNav.HomeOutputPath, "Open"), html);

        // The panels are Charts.Framed output, so the page cannot grow a frame anatomy charts do not have.
        Assert.Contains(Charts.FrameWhySlot("Framing every chart the same way means a reader never has to work out what they are looking at from the picture alone."), html);
    }

    [Fact]
    public void DesignSystem_AndTheLegendKey_ShareOneStageWordSeam()
    {
        // Both surfaces name the same stages; if they ever disagreed, the page teaching the vocabulary would
        // be the one that was wrong. StatusStyles.LegendWord is the single seam, and this pins that.
        var html = Generate();
        var legend = StatusStyles.LegendKey();

        foreach (var stage in StatusStyles.LegendStages)
        {
            var word = PathUtil.Html(StatusStyles.LegendWord(stage));
            Assert.Contains(word, legend);
            Assert.Contains(word, html);
        }
    }

    [Fact]
    public void DesignSystem_NeverStatesATokenValueAsALiteral()
    {
        // The page shows a token's value by USING it (a swatch painted `var(--status-*)`), never by
        // re-typing the hex. A literal here would be a second definition free to drift from the stylesheet —
        // exactly what the whole token system exists to prevent, and doubly wrong on the page that teaches it.
        var html = Generate();
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        foreach (var literal in new[] { "#b8b2a8", "#e8d9a8", "#7a6250", "#5c6570", "#6b8f62", "#2e6b7a" })
        {
            Assert.Contains(literal, css);                 // the value lives in the stylesheet...
            Assert.DoesNotContain(literal, html);          // ...and nowhere in the page that documents it.
        }
    }

    [Fact]
    public void DesignSystem_IsReadableWithJavaScriptOff()
    {
        var html = Generate();

        // NFR-5/NFR6: the page carries no script island of its own, and its content is in the served HTML
        // rather than assembled at runtime. (The shared specscribe.js is a progressive enhancement the page
        // does not depend on; a page-local <script> block would be a dependency.)
        var main = MainOf(html);
        Assert.DoesNotContain("<script", main);

        // Everything the page teaches is present as text, not as a colour a reader has to interpret.
        Assert.Contains("Ready for dev", main);
        Assert.Contains("In review", main);
        Assert.Contains("--status-done", main);
        Assert.Contains("--motion-entrance", main);
    }

    [Fact]
    public void DesignSystem_BypassesApplyReferenceLinks()
    {
        // A page whose subject IS the portal's vocabulary must not self-expand its own terms into reference
        // chips or nested <abbr> — the same rule How-to-read and About follow.
        var html = Generate();
        Assert.DoesNotContain("<abbr", html);
        Assert.DoesNotContain("class=\"ref-chip", html);
    }

    /// <summary>The page's own <c>&lt;main&gt;</c>, so an assertion can't be satisfied or defeated by the
    /// surrounding nav bar, head, or footer.</summary>
    private static string MainOf(string html)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "page should have a <main> landmark");
        return html[start..end];
    }
}
