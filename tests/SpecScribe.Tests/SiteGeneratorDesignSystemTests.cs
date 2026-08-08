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

    /// <summary>The status vocabulary as a reader should see it, pinned INDEPENDENTLY of the production
    /// switches that generate it. [Story 23.2 review 2026-08-07]
    ///
    /// <para>Asserting <c>StatusStyles.LegendWord(stage)</c> against a page rendered from
    /// <c>StatusStyles.LegendWord(stage)</c> is satisfied by construction — it cannot fail while the seam is
    /// wrong, only while the page stops using it. Both switches fall through to a default arm
    /// (<c>StoryLabel</c>'s is "Pending", <c>StageMeaning</c>'s is "Status stage"), so a new canonical stage
    /// added without its own arm rendered plausible-looking wrong words on the page whose entire subject is
    /// this vocabulary. These tables are the second opinion; the completeness assertion below is what forces
    /// them to be extended rather than silently bypassed.</para></summary>
    private static readonly IReadOnlyDictionary<string, string> ExpectedWords =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pending"] = "Pending",
            ["drafted"] = "Drafted",
            ["ready"] = "Ready for dev",
            ["active"] = "In development",
            ["review"] = "In review",
            ["done"] = "Done",
            ["deferred"] = "Deferred",
            ["unmapped"] = "Not yet mapped",
            ["retired"] = "Retired",
            ["unrecognized"] = "Unrecognized",
        };

    private static readonly IReadOnlyDictionary<string, string> ExpectedMeanings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pending"] = "Not yet ready to pick up",
            ["drafted"] = "Stories or a plan exist; work has not started",
            ["ready"] = "Task plan exists and dependencies met",
            ["active"] = "Actively being developed",
            ["review"] = "Implementation complete; awaiting review or retrospective",
            ["done"] = "Finished and closed",
            ["deferred"] = "Shelved on purpose for later",
            ["unmapped"] = "Listed, but not yet mapped to any epic or story",
            ["retired"] = "Removed from the active plan; kept for ledger history",
            ["unrecognized"] = "Native status word has no canonical mapping",
        };

    /// <summary>A new canonical stage must arrive with its word and meaning spelled out here, not inherit a
    /// fallback. Without this, extending <c>LegendStages</c> would simply skip the pinned tables.
    /// [Story 23.2 review 2026-08-07]</summary>
    [Fact]
    public void DesignSystem_PinnedVocabulary_CoversEveryCanonicalStage()
    {
        Assert.NotEmpty(StatusStyles.LegendStages);
        foreach (var stage in StatusStyles.LegendStages)
        {
            Assert.True(ExpectedWords.ContainsKey(stage), $"No pinned WORD for canonical stage '{stage}'.");
            Assert.True(ExpectedMeanings.ContainsKey(stage), $"No pinned MEANING for canonical stage '{stage}'.");
        }

        // ...and nothing pinned here has fallen out of the canonical list, which would leave a dead expectation.
        foreach (var stage in ExpectedWords.Keys)
        {
            Assert.Contains(stage, StatusStyles.LegendStages);
        }
    }

    [Fact]
    public void DesignSystem_DocumentsEveryCanonicalStatusStage_ByNameNotColourAlone()
    {
        var html = Generate();

        // A test that loops over a collection is vacuous whenever that collection can be empty — the lesson
        // this story's own 2026-07-29 pass recorded after its new tests passed with an emptied allowlist.
        // Its three sibling tests in this file each open with this guard; this one was missed until the
        // 2026-08-07 pass. [Story 23.2 review]
        Assert.NotEmpty(StatusStyles.LegendStages);

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
                // ⚠️ WHOLE token, not a prefix. `Assert.Contains($"--status-{stage}", html)` was satisfied by
                // the FILL token the same list item also prints: `--status-ready` is a strict substring of
                // `--status-ready-bg`, and likewise for done/active/review. Dropping the accent from
                // `StatusBody` for any of those four left this green. The closing `</code>` is what makes it
                // the whole name. [Story 23.2 review 2026-08-07]
                Assert.Contains($"--status-{stage}</code>", html);
            }
            // ...the human WORD (UX-DR17: never colour alone)...
            //
            // ⚠️ Against EXPECTED_WORDS, not `StatusStyles.LegendWord(stage)`. Asserting the templater's own
            // call reproduces the page's derivation, so it could only ever detect the page ABANDONING the
            // seam — never the seam returning the wrong word. `LegendWord` falls through to `StoryLabel`,
            // whose fallback is "Pending", so an eleventh stage would have rendered "Pending" on the page
            // whose whole subject is the status vocabulary, green. The same by-construction vacuity this
            // file already fixed for `--status-{stage}` above. [Story 23.2 review 2026-08-07]
            Assert.Contains(PathUtil.Html(ExpectedWords[stage]), html);
            // ...and the plain-language meaning, pinned the same way and for the same reason.
            Assert.Contains(PathUtil.Html(ExpectedMeanings[stage]), html);
        }
    }

    /// <summary>The reduce contract is stated on the page, not just implied — a reader must learn that motion
    /// has an opt-out.
    ///
    /// <para>⚠️ Token COVERAGE deliberately does not live here. It used to, as a hand-typed five-element array
    /// — the exact second copy `MotionTokens` was made `internal` to abolish — and it survived beside its own
    /// derived replacement (<see cref="DesignSystem_DocumentsEveryMotionToken_AndNamesNoneThatIsInvented"/>),
    /// whose comment already described it in the past tense. Renaming a token reddened it for the wrong
    /// reason and invited re-typing the new name in. The array is gone; this test keeps only the assertion
    /// that is genuinely its own. [Story 23.2 review 2026-08-07]</para></summary>
    [Fact]
    public void DesignSystem_StatesTheReducedMotionContract()
    {
        Assert.Contains("prefers-reduced-motion", Generate());
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
    public void DesignSystem_EverySwatchClassResolvesToARuleInTheStylesheet()
    {
        // The CLASS half of the same guarantee `DesignSystem_NamesNoTokenTheStylesheetDoesNotDeclare` gives
        // for token names. The templater's `_ =>` arm emits `status-legend-key-swatch <stage>` for any stage
        // it has no special case for, so an eleventh `LegendStages` entry with a real `--status-<stage>` token
        // but no `.status-legend-key-swatch.<stage>` rule ships a BLANK swatch next to a correct caption —
        // green, and wrong in the direction a component author trusts. [Story 23.2 review 2026-08-07]
        var html = MainOf(Generate());
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        var swatchClasses = Regex.Matches(html, @"status-legend-key-swatch ([a-z0-9-]+)", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(swatchClasses);
        foreach (var cls in swatchClasses)
        {
            Assert.True(
                Regex.IsMatch(css, $@"\.status-legend-key-swatch\.{Regex.Escape(cls)}\b"),
                $"The design-system page renders a swatch with class `{cls}`, but {ForgeOptions.StylesheetName} "
                + $"has no `.status-legend-key-swatch.{cls}` rule — so that swatch renders blank. Add the rule, "
                + "or give the stage an explicit borrow arm in DesignSystemTemplater.StatusBody.");
        }
    }

    /// <summary>The same class guarantee for the BADGE, which had none.
    ///
    /// <para>The swatch half above was closed on 2026-08-07 and the badge half was not, though it takes the
    /// identical <c>_ =&gt;</c> path and emits <c>class="status-badge &lt;stage&gt;"</c> twice per stage.
    /// <c>StylesheetTests</c> pins only <c>.status-badge.unrecognized</c> and <c>.status-badge.retired</c>, so
    /// a new stage's badge fell through to the base <c>.status-badge</c> rule and rendered visually identical
    /// to <c>pending</c> — on the page whose subject is the colour vocabulary. [Story 23.2 review
    /// 2026-08-07]</para></summary>
    [Fact]
    public void DesignSystem_EveryBadgeStageClassResolvesToARuleInTheStylesheet()
    {
        var html = MainOf(Generate());
        var css = File.ReadAllText(Path.Combine(Site, ForgeOptions.StylesheetName));

        // `StatusStyles.Badge` emits `class="status-badge <stage> js-tip"`, so the stage modifier sits between
        // the base class and the tooltip hook — matched explicitly rather than by "the next word", which
        // captured `js-tip` on a bare badge and made this list empty.
        var badgeClasses = Regex.Matches(html, @"class=""status-badge ([a-z0-9-]+) js-tip""", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(badgeClasses);
        foreach (var cls in badgeClasses)
        {
            Assert.True(
                Regex.IsMatch(css, $@"\.status-badge\.{Regex.Escape(cls)}\b"),
                $"The design-system page renders a badge with class `{cls}`, but {ForgeOptions.StylesheetName} "
                + $"has no `.status-badge.{cls}` rule — so it falls through to the base badge rule and reads as "
                + "`pending`. Add the rule, or give the stage an explicit remap in DesignSystemTemplater.");
        }
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
        // Guarded, like the forward loop above it — a rename of the token PREFIX empties the match set and
        // makes this direction vacuous while it still reports success. [Story 23.2 review 2026-08-07]
        var declared = Regex.Matches(css, @"(--motion-[a-z0-9-]+)\s*:", RegexOptions.IgnoreCase);
        Assert.NotEmpty(declared);
        foreach (Match m in declared)
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
        // Guarded, like the forward loop above it: a rename of the `--status-*-bg` convention empties the
        // match set and this direction passes without executing a body. [Story 23.2 review 2026-08-07]
        var declaredFills = Regex.Matches(css, @"(--status-[a-z0-9-]+-bg)\s*:", RegexOptions.IgnoreCase);
        Assert.NotEmpty(declaredFills);
        foreach (Match m in declaredFills)
        {
            Assert.Contains(m.Groups[1].Value, html);
        }

        // `ready` and `drafted` share one fill, exactly as the stylesheet pairs them — the page says so rather
        // than leaving a reader to notice two identical token names and wonder if it is a mistake.
        Assert.Equal(
            DesignSystemTemplater.StageFillTokens["ready"],
            DesignSystemTemplater.StageFillTokens["drafted"]);

        // ⚠️ Asserts the BEHAVIOUR for every shared fill, not the literal "shared with ready/drafted".
        // [Story 23.2 review 2026-08-07] The templater derived that phrase from a hardcoded
        // `stage is "ready" or "drafted"`, so a fifth stage joining an existing fill printed no note at all
        // and this assertion stayed green — it pinned the one pair that happened to be spelled out. The note
        // is now derived, and this checks each sharer is actually named on the page.
        var sharedFills = DesignSystemTemplater.StageFillTokens
            .GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToArray();
        Assert.NotEmpty(sharedFills);
        // ⚠️ Assert the RENDERED note, not `shared with {other}` per member. The templater joins sharers with
        // "/" (`shared with drafted/ready`), so a substring probe for each member passed only because a
        // TWO-member group's join equals its single other name. A third stage on one fill emits correct output
        // that this assertion calls a failure — the guard written to stop pinning "the one pair that happened
        // to be spelled out" was itself pinned to a pair. [Story 23.2 review 2026-08-07]
        foreach (var group in sharedFills)
        {
            var members = group.Select(kv => kv.Key).ToArray();
            foreach (var stage in members)
            {
                var others = members
                    .Where(s => !string.Equals(s, stage, StringComparison.Ordinal))
                    .OrderBy(StatusStyles.CanonicalRank);
                Assert.Contains($"shared with {string.Join("/", others)}", html);
            }
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

        // ⚠️ EVERY top-level `:root` block, not the first. [Story 23.2 review 2026-08-07] The pattern is
        // non-greedy, so it used to stop at block one — and `specscribe.css` has a second top-level `:root`
        // declaring `--impact-lvl-1`…`-5` (`#f3e8c6`, `#e9cd82`, `#dcae4d`, `#c8912b`, `#a86f1e`). That is
        // precisely the block whose omission from the token bridge was this story's headline regression, so
        // the page could have stated one of those five hexes verbatim with this test still green.
        var rootBlocks = Regex.Matches(css, @"^:root\s*\{(.*?)^\}", RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.NotEmpty(rootBlocks);

        // ⚠️ Every notation CSS actually permits, not 6-digit hex alone: `#fff`, `#rrggbbaa`, `rgb()`, `hsl()`
        // and `oklch()` are all legal ways to re-type a token's value, and all of them used to pass.
        var literals = rootBlocks
            .SelectMany(b => Regex.Matches(
                    b.Groups[1].Value,
                    @"#[0-9a-fA-F]{8}\b|#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3,4}\b|\b(?:rgba?|hsla?|oklch|oklab|lab|lch)\([^)]*\)")
                .Select(m => m.Value))
            .Select(v => Regex.Replace(v, @"\s+", " ").Trim())
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
