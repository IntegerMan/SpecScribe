using System.Text.RegularExpressions;
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
            //
            // ⚠️ This used to be `Assert.Contains($"--status-{stage}", html)` and nothing else — a string the
            // templater derives from the SAME loop variable, so it could not fail. An eleventh LegendStages
            // entry with no matching `:root` declaration would ship a blank swatch captioned with a token that
            // does not exist, green. The assertion that matters is the other direction, and it is below:
            // every `--status-*` the page NAMES must actually be declared in the generated stylesheet.
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
    public void DesignSystem_NamesNoTokenTheStylesheetDoesNotDeclare()
    {
        // THE assertion a component author depends on: every `--…` custom property this page names is real.
        // Nothing checked this in either direction before — the C# page had a `_ =>` arm that fabricated
        // `--status-<stage>` for any future LegendStages entry, and the Vue twin published `--status-retired`,
        // a property declared nowhere. A design-system page that names a token that does not exist is worse
        // than one that omits it: it is an instruction that silently produces an unstyled element.
        // [Story 23.2 re-review 2026-07-28]
        var html = MainOf(Generate());
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        var named = Regex.Matches(html, @"--[a-z0-9-]+", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(named);
        foreach (var token in named)
        {
            Assert.True(
                css.Contains($"{token}:", StringComparison.Ordinal),
                $"The design-system page names `{token}`, but {ForgeOptions.StylesheetName} declares no such "
                + "custom property. Either the page is teaching a token that does not exist, or the token was "
                + "renamed and the page was not updated.");
        }
    }

    [Fact]
    public void DesignSystem_DocumentsEveryMotionToken_AndNamesNoneThatIsInvented()
    {
        // Derived from the templater's OWN list, not a hand-typed copy of it. The previous test asserted a
        // literal five-element array against a literal five-element array, so adding `--motion-exit` to the
        // stylesheet left both design-system pages silently incomplete with the suite green.
        var html = MainOf(Generate());
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        Assert.NotEmpty(DesignSystemTemplater.MotionTokens);
        foreach (var (token, role) in DesignSystemTemplater.MotionTokens)
        {
            Assert.Contains(token, html);
            Assert.Contains(PathUtil.Html(role), html);
            Assert.Contains($"{token}:", css);
        }

        // And the other direction: no `--motion-*` declared in the stylesheet is missing from the page.
        foreach (Match m in Regex.Matches(css, @"(--motion-[a-z0-9-]+)\s*:", RegexOptions.IgnoreCase))
        {
            Assert.Contains(m.Groups[1].Value, html);
        }
    }

    [Fact]
    public void DesignSystem_NeverStatesATokenValueAsALiteral()
    {
        // The page shows a token's value by USING it (a swatch painted `var(--status-*)`), never by
        // re-typing the hex. A literal here would be a second definition free to drift from the stylesheet —
        // exactly what the whole token system exists to prevent, and doubly wrong on the page that teaches it.
        //
        // ⚠️ Derived from the stylesheet's OWN `:root` declarations rather than a hand-listed six. The old list
        // missed `#d4a017`, `#1e4a5a` and `#e8ecf0` entirely, AND coupled the test to the palette's values —
        // so changing `--status-pending` broke a design-system test, and the fix was to hand-retype the new hex
        // into it, growing the second copy of the palette this test exists to forbid.
        var html = MainOf(Generate());
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        var rootBlock = Regex.Match(css, @"^:root\s*\{(.*?)^\}", RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(rootBlock.Success, "Could not locate the `:root` block in the generated stylesheet.");

        var literals = Regex.Matches(rootBlock.Groups[1].Value, @"#[0-9a-fA-F]{6}\b")
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(literals);
        foreach (var literal in literals)
        {
            Assert.False(
                html.Contains(literal, StringComparison.OrdinalIgnoreCase),
                $"The design-system page states the literal `{literal}`. It must show a token's value by USING "
                + "the token, never by re-typing it — a literal here is a second definition free to drift.");
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
        //
        // ⚠️ Asserting only the ABSENCE of `<abbr`/`ref-chip` was vacuous: `AbbreviationExpander` wraps only
        // FR/NFR/AC/ADR/PRD and `ReferenceChipRenderer` needs `[[wiki]]` or `file:line`, and this page contains
        // none of those — so the guard passed identically with and without the bypass. The test now proves the
        // expander WOULD have fired on this page's text, which is what makes the bypass load-bearing.
        var html = Generate();
        Assert.DoesNotContain("<abbr", html);
        Assert.DoesNotContain("class=\"ref-chip", html);

        // ⚠️ KNOWN-VACUOUS, and left that way deliberately rather than made to look stronger than it is.
        //
        // A contrast control was tried during the 2026-07-28 re-review — "assert some OTHER page in this site
        // carries <abbr>/ref-chip output" — and it FAILED, which is the finding rather than a bug in the
        // control: this minimal fixture (one epic, one story, no glossary, no requirements) produces no
        // expander output ANYWHERE, so the two assertions above pass with the bypass and pass without it.
        //
        // Making this real needs one of: a fixture carrying a glossary term + requirement IDs so the
        // linkifiers actually fire, or a seam that asserts the WRITE PATH directly (that WriteDesignSystem
        // reaches WriteOutput without ApplyReferenceLinks). Both are larger than a review patch and neither is
        // unambiguous, so this is recorded as an open decision on Story 23.2 rather than papered over.
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
