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
        return SiteRegion.Read(Site, SiteNav.DesignSystemOutputPath);
    }

    [Fact]
    public void GenerateAll_WritesDesignSystemOnEveryRun_ReachableFromHelpNav()
    {
        var html = Generate();

        Assert.True(SiteRegion.Exists(Site, SiteNav.DesignSystemOutputPath));
        Assert.Contains("<h1>Design System</h1>", html);

        // The Help nav group + the dashboard's Help quick-links band both carry it, on every page.
        var index = SiteRegion.Read(Site, "index.html");
        Assert.Contains($"href=\"{SiteNav.DesignSystemOutputPath}\"", index);
        Assert.Contains("Design System", index);
    }

    [Fact]
    public void DesignSystem_NavEntry_ResolvesToAWrittenFile()
    {
        // Nav coherence: every Help child must point at a page this run actually produced. A dangling Help
        // link is the exact failure the always-written guarantee exists to prevent.
        // [Story 23.6 AC #8] "a file this run produced" is now "a ROUTE this run emitted" — the IR is what a
        // completed generate produces, and it is what the renderer turns into the file the link resolves to.
        new SiteGenerator(Options()).GenerateAll();
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe");

        var entry = Assert.Single(nav.Items, i => i.Label == "Design System");
        Assert.Equal(SiteNav.DesignSystemOutputPath, entry.OutputRelativePath);
        Assert.True(SiteRegion.Exists(Site, entry.OutputRelativePath));
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

    /// <summary>The stage FILL tokens are documented in both directions, derived from the templater's own map.
    ///
    /// <para>An accent without its fill is half a pair, and half a pair is what shipped: the four fills lived
    /// as inline hexes on <c>.status-badge.&lt;stage&gt;</c>, so the token bridge could not carry them and the
    /// Vue badge substituted one flat parchment for four distinct tints. Documenting only the accent is what
    /// let a component author reproduce that mistake from this very page. [Story 23.2 re-review
    /// 2026-07-28]</para></summary>
    [Fact]
    public void DesignSystem_DocumentsEveryStageFillToken_AndNamesNoneThatIsInvented()
    {
        var html = MainOf(Generate());
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        Assert.NotEmpty(DesignSystemTemplater.StageFillTokens);
        foreach (var (stage, fill) in DesignSystemTemplater.StageFillTokens)
        {
            Assert.Contains(stage, StatusStyles.LegendStages); // the map cannot name a stage that is not real
            Assert.Contains(fill, html);
            Assert.Contains($"{fill}:", css);
        }

        // The other direction: every `--status-*-bg` the stylesheet declares is documented here. Adding a fifth
        // fill without teaching it is the same silent-omission failure the motion family already guards.
        foreach (Match m in Regex.Matches(css, @"(--status-[a-z0-9-]+-bg)\s*:", RegexOptions.IgnoreCase))
        {
            Assert.Contains(m.Groups[1].Value, html);
        }

        // `ready` and `drafted` share one fill, exactly as the stylesheet pairs them — the page says so rather
        // than leaving a reader to notice two identical token names and wonder if it is a mistake.
        Assert.Equal(
            DesignSystemTemplater.StageFillTokens["ready"],
            DesignSystemTemplater.StageFillTokens["drafted"]);
        Assert.Contains("shared with ready/drafted", html);
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

        // ── The positive control that makes the two assertions above load-bearing ────────────────────────
        //
        // Absence assertions alone were VACUOUS, and the 2026-07-28 re-review said so: `AbbreviationExpander`
        // wraps only FR/NFR/AC/ADR/PRD and `ReferenceChipRenderer` needs `[[wiki]]`/`file:line`, none of which
        // this page's prose contains — so both passed with the bypass and passed without it.
        //
        // The re-review's own attempt to fix it looked for expander output on some OTHER page and FAILED,
        // because this minimal fixture has no glossary and no requirements. It then recorded three costly
        // options (extend the fixture, add a write-path seam, delete the test). All three miss that
        // `ApplyReferenceLinks` is FIVE linkifiers, not one: `StoryEpicLinkifier` needs neither a glossary nor
        // a requirement, only an "Epic N"/"Story N.M" mention in a text node — and this page renders exactly
        // one, in the ListRow demo chip, while the fixture defines Epic 1. So the control needs no new fixture
        // and no new production seam.
        //
        // ⚠️ It is deliberately NOT the abbreviation expander. Every FR/NFR/PRD occurrence on this page sits
        // inside an `<a>` in the nav band, and `ProtectedSplit` protects whole anchors — so an expander-based
        // control would be a second vacuous assertion wearing a positive control's clothes.
        var main = MainOf(html);
        var model = EpicsParser.Parse(File.ReadAllText(Path.Combine(Source, "planning-artifacts", "epics.md")));
        Assert.Contains(1, model.Epics.Select(e => e.Number));

        // The mention is present, and present as PLAIN TEXT — the exact primitive output, unlinkified.
        var epicHref = StoryEpicLinkifier.EpicPagePath(1);
        Assert.False(
            main.Contains(epicHref, StringComparison.Ordinal),
            $"The design-system page links to `{epicHref}`, which means its \"Epic 1\" chip was rewritten by "
            + "StoryEpicLinkifier — i.e. WriteDesignSystem is now running the page through ApplyReferenceLinks. "
            + "A page whose subject IS the portal's vocabulary must not self-expand its own terms; write it "
            + "directly, as How-to-read and About do.");
        Assert.Contains(ListRow.Chip("Epic 1"), main);

        // ...and `ApplyReferenceLinks` WOULD have rewritten it. This is the assertion that fails the moment
        // `WriteDesignSystem` starts running the page through the linkifier chain.
        var linkified = StoryEpicLinkifier.Linkify(main, model, string.Empty);
        Assert.NotEqual(main, linkified);
        Assert.Contains(epicHref, linkified);
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
