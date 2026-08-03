using System.Reflection;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.5 AC #1/#2: the webview host-theme bridge + the read-only helper affordance. These pin that
/// the bridge (a) is inlined into the webview document as a SECOND stylesheet, (b) maps host chrome variables and
/// contrast-tunes the SpecScribe accents under the <c>.vscode-*</c> theme scopes, (c) is webview-ONLY so the
/// generated HTML surface can never inherit it (the byte-parity guardrail), and (d) that the helper button
/// generates + hands off text without any write path. Semantic parity is unchanged because theming re-values
/// TOKENS, not markup — so no new <see cref="HostRenderException"/> is needed. [Story 6.5]</summary>
public class WebviewThemingTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

    private static PageView EpicPage()
    {
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", null),
        });
        var body =
            "<main id=\"main-content\">\n" +
            StatusStyles.Badge("active", "In development") + "\n" +
            "<a href=\"../epics/story-1-1.html\">Story 1.1</a>\n" +
            "</main>\n\n";
        return new PageView
        {
            Kind = PageKind.Epic,
            OutputRelativePath = "epics/epic-1.html",
            Title = "Epic 1: Foundation — SpecScribe",
            Nav = Nav().ToNavigationView("epics/epic-1.html"),
            Breadcrumb = breadcrumb,
            Assets = new AssetManifest
            {
                StylesheetHref = "../" + ForgeOptions.StylesheetName,
                ScriptHref = "../" + ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = new[] { "epics/story-1-1.html" },
                StatusStage = "active",
            },
            BodyHtml = body,
        };
    }

    private static string EmbeddedResource(string name)
    {
        using var stream = typeof(WebviewRenderAdapter).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ----- The bridge is present, second, and host-mapped (AC #1) ----------------------------------------------

    [Fact]
    public void Render_InlinesTheThemeBridge_AsASecondStylesheet()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage()).Content;

        // Two inline stylesheets now: the production sheet, then the theme bridge (which must come AFTER so its
        // scoped rules win the cascade).
        var first = doc.IndexOf("<style>", StringComparison.Ordinal);
        var second = doc.IndexOf("<style>", first + 1, StringComparison.Ordinal);
        Assert.True(second > first && first >= 0, "expected two <style> blocks (production sheet + theme bridge)");

        // The bridge's signature: it keys off every VS Code body-class scope and reads host variables.
        Assert.Contains(".vscode-light", doc);
        Assert.Contains(".vscode-dark", doc);
        Assert.Contains(".vscode-high-contrast", doc);
        Assert.Contains(".vscode-high-contrast-light", doc);
        Assert.Contains("--vscode-editor-background", doc);
        Assert.Contains("--vscode-foreground", doc);
    }

    [Fact]
    public void ThemeBridge_MapsChromeTokensToHostVariables()
    {
        var bridge = EmbeddedResource("SpecScribe.assets.specscribe-webview-theme.css");

        // Chrome/container tokens resolve from the host (AD-7: host owns chrome).
        Assert.Contains("--cream: var(--vscode-editor-background)", bridge);
        Assert.Contains("--ink: var(--vscode-foreground)", bridge);
        Assert.Contains("--border: var(--vscode-panel-border", bridge);
        // The literal-colored nav bar is remapped to the host title-bar palette rather than left near-black.
        Assert.Contains(".vscode-dark .site-nav", bridge);
        Assert.Contains("--vscode-titleBar-activeBackground", bridge);
    }

    [Fact]
    public void ThemeBridge_ContrastTunesTheStatusAndInsightAccents_WithoutBridgingOntoHostSeverity()
    {
        var bridge = EmbeddedResource("SpecScribe.assets.specscribe-webview-theme.css");

        // The six stage tokens + the chart accents are re-valued under the dark + high-contrast scopes (accents
        // stay SpecScribe-owned, contrast-tuned) — NOT mapped onto --vscode error/warning/success severities.
        Assert.Contains(".vscode-dark {", bridge);
        Assert.Contains(".vscode-high-contrast {", bridge);
        foreach (var token in new[] { "--status-active", "--status-review", "--status-done", "--status-ready", "--status-pending", "--teal", "--gold", "--rust" })
            Assert.Contains(token + ":", bridge);
        // The explicitly-rejected direction must NOT appear: no stage bridged onto a host severity color.
        //
        // ⚠️ This was three whole-file `DoesNotContain` substring checks, and it was MIS-SCOPED — it forbade the
        // severity variables ANYWHERE in the sheet. Story 6.5 rejected one specific thing: mapping SpecScribe's
        // SIX LIFECYCLE STAGES onto VS Code's ~3 severities, because that collapses a six-way distinction the
        // whole insight system depends on ("Bridge accents onto the host palette (map stages to --vscode-*
        // error/warning/success) … Explicitly rejected", 6-5 story record). The SAME decision says chrome DOES
        // adopt host vars, "because chrome has no SpecScribe semantic to protect".
        //
        // The substring check could not tell those apart, so it failed on two pieces of correct chrome: the host
        // status banner (`.ss-webview-status[data-level="error"]` — an actual host error, reported in the host's
        // error color) and the settings form's validation messages. Both are host severities being rendered as
        // host severities, which is the RIGHT half of AD-7.
        //
        // Replaced with a guard on the actual rule, which is strictly stronger where it matters: no protected
        // token may be ASSIGNED a severity variable, and severity variables may appear only on an allowlist of
        // host-chrome selectors — so a drive-by cannot put one on `.status-badge` either. [2026-08-02]
        AssertNoSemanticTokenBridgesOntoHostSeverity(bridge);
        // Story 9.5: resting AC tint companion (site parchment doesn't read on dark) beside the :target override.
        // border-color must not wipe the gold left accent — reassert border-left-color after it.
        Assert.Contains(".vscode-dark .ac-criterion,", bridge);
        Assert.Contains(".vscode-dark .ac-criterion:target,", bridge);
        Assert.Contains("border-left-color: var(--gold)", bridge);
        Assert.Contains("border-left-color: var(--gold-light)", bridge);
    }

    // ----- Guard-of-the-guard -----------------------------------------------------------------------------------
    //
    // Narrowing a gate is how a gate quietly stops gating. The check above replaced three whole-file substring
    // assertions, so these two pin that it still REJECTS both shapes of the thing Story 6.5 rejected — run against
    // synthetic CSS so they never depend on what the shipped sheet happens to contain today.

    [Fact]
    public void SeverityGuard_RejectsAStageTokenDeclaredInTermsOfAHostSeverity()
    {
        // The literal rejected direction: "map stages to --vscode-* error/warning/success".
        var offending = ".vscode-dark {\n  --status-review: var(--vscode-editorWarning-foreground);\n}\n";

        var ex = Assert.Throws<Xunit.Sdk.FalseException>(() => AssertNoSemanticTokenBridgesOntoHostSeverity(offending));
        Assert.Contains("--status-review", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SeverityGuard_RejectsAHostSeverityUsedOnASpecScribeStageSelector()
    {
        // The sneakier shape the old substring check also covered and a naive replacement would miss: styling a
        // stage with a severity colour WITHOUT ever naming its token.
        var offending = ".vscode-dark .status-badge.done {\n  color: var(--vscode-errorForeground);\n}\n";

        var ex = Assert.Throws<Xunit.Sdk.TrueException>(() => AssertNoSemanticTokenBridgesOntoHostSeverity(offending));
        Assert.Contains("status-badge", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SeverityGuard_AllowsHostChromeToUseHostSeverityColours()
    {
        // The other half of AD-7, and the false positive that made the old check unusable: a banner reporting an
        // actual host error renders in the host's error colour. That is correct, not a violation.
        AssertNoSemanticTokenBridgesOntoHostSeverity(
            ".ss-webview-status[data-level=\"error\"] {\n  background: var(--vscode-errorForeground);\n}\n");
    }

    /// <summary>SpecScribe's protected colour vocabulary: the six lifecycle stages plus the insight accents. These
    /// carry MEANING — the six-way stage distinction the sunburst/donut/funnel exist to show — so they may be
    /// contrast-tuned per theme but never re-pointed at a host severity variable. [Story 6.5]</summary>
    private static readonly string[] SemanticTokens =
    [
        "--status-active", "--status-review", "--status-done", "--status-ready", "--status-drafted", "--status-pending",
        "--teal", "--teal-deep", "--gold", "--gold-light", "--moss", "--moss-light", "--rust", "--rust-light",
    ];

    /// <summary>Selectors permitted to reference a host severity variable, because what they style IS a host
    /// severity rather than a SpecScribe stage: the extension's status banner (missing source root, renderer
    /// failure, diagnostics present) and the settings form's validation messages. Chrome adopting host vars is the
    /// sanctioned half of AD-7; this list is what keeps "chrome" from quietly widening to mean "anything".</summary>
    private static readonly string[] HostSeverityChromeSelectors =
    [
        ".ss-webview-status", ".ss-form-status", ".ss-form-error",
    ];

    /// <summary>The Story 6.5 rule, enforced precisely rather than by substring.</summary>
    private static void AssertNoSemanticTokenBridgesOntoHostSeverity(string bridge)
    {
        const string severityPattern = @"--vscode-(?:[\w-]*[Ee]rror[\w-]*|[\w-]*[Ww]arning[\w-]*|[\w-]*[Ss]uccess[\w-]*)";

        // 1. The rejected direction itself: a protected token DECLARED in terms of a host severity. This is what
        //    "map stages to --vscode-* error/warning/success" would literally look like in CSS.
        foreach (var declaration in System.Text.RegularExpressions.Regex.Matches(
                     bridge, @"(--[\w-]+)\s*:\s*([^;}]*)").Cast<System.Text.RegularExpressions.Match>())
        {
            var token = declaration.Groups[1].Value;
            if (!SemanticTokens.Contains(token, StringComparer.Ordinal)) continue;
            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(declaration.Groups[2].Value, severityPattern),
                $"`{token}` is bridged onto a host severity — the direction Story 6.5 explicitly rejected, because "
                + $"it collapses six lifecycle stages into VS Code's ~3 severities. Declaration: {declaration.Value.Trim()}");
        }

        // 2. Containment: a severity variable may appear only inside an allowlisted host-chrome rule. Without this
        //    the check above would miss `.status-badge.done { color: var(--vscode-errorForeground) }` — styling a
        //    stage without ever naming its token.
        foreach (var use in System.Text.RegularExpressions.Regex.Matches(bridge, severityPattern)
                     .Cast<System.Text.RegularExpressions.Match>())
        {
            var braceStart = bridge.LastIndexOf('{', use.Index);
            Assert.True(braceStart > 0, "a severity variable outside any rule block");
            var preludeStart = Math.Max(bridge.LastIndexOf('}', braceStart), bridge.LastIndexOf("*/", braceStart, StringComparison.Ordinal));
            var selector = bridge[(preludeStart + 1)..braceStart].Trim();
            Assert.True(
                HostSeverityChromeSelectors.Any(allowed => selector.Contains(allowed, StringComparison.Ordinal)),
                $"`{use.Value}` is used by `{selector}`, which is not host chrome. Host severity colours belong "
                + "only on surfaces that report a HOST condition; a SpecScribe stage must keep its own hue.");
        }
    }

    // Deferred-work (Story 6.5 review): the ".vscode-light has no dedicated contrast-tuning block" gap is verified
    // by computed WCAG contrast + drift-from-:root checks in StylesheetTests.VscodeLightBlock_MatchesRootValues_
    // AndRealTextTokensClearWcagAA (reusing that file's existing ContrastRatio/TokenValue helpers) — not here, so
    // there is exactly one place that owns the "is it really safe" claim instead of two divergent assertions.

    // ----- Webview-only: the theme can never leak into the generated HTML surface (byte-parity guardrail) -------

    [Fact]
    public void ProductionStylesheet_CarriesNoWebviewThemeScope_SoTheHtmlSurfaceCannotInheritIt()
    {
        // The HTML surface loads ONLY specscribe.css. If a .vscode-* scope or a --vscode-* var ever appeared there,
        // theming would leak onto generated pages and break the golden byte-parity. The bridge lives in a separate
        // embedded resource that the HTML surface never references.
        var production = EmbeddedResource("SpecScribe.assets.specscribe.css");
        Assert.DoesNotContain(".vscode-", production);
        Assert.DoesNotContain("--vscode-", production);
    }

    // ----- The read-only helper affordance (AC #2) -------------------------------------------------------------

    [Fact]
    public void Render_CarriesTheHelperButton_InTheShellOutsideTheSwappableSurface()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage()).Content;

        // The helper toolbar + button exist and carry the pre-generated prompt in a data attribute…
        Assert.Contains("ss-helper-btn", doc);
        Assert.Contains("data-ss-prompt=\"", doc);
        // …and they sit BEFORE #specscribe-surface, i.e. in the persistent shell — so an in-place content swap
        // (which only replaces the surface's innerHTML) never destroys the helper.
        var toolbar = doc.IndexOf("ss-webview-toolbar", StringComparison.Ordinal);
        var surface = doc.IndexOf("id=\"specscribe-surface\"", StringComparison.Ordinal);
        Assert.True(toolbar >= 0 && toolbar < surface, "helper toolbar must precede the swappable surface");
    }

    [Fact]
    public void RenderContent_DoesNotCarryTheHelperButton_SoSwapsNeverDuplicateIt()
    {
        // The swappable region (what postMessage installs into #specscribe-surface) must NOT contain the helper —
        // it belongs to the shell. This guards against the button being duplicated on every navigation.
        var content = WebviewRenderAdapter.Shared.RenderContent(EpicPage());
        Assert.DoesNotContain("ss-helper-btn", content);
    }

    [Fact]
    public void Render_HelperPath_HandsOffTextOnly_NeverWritingAnArtifact()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage()).Content;

        // The bridge's helper branch posts a copy message and nothing else — a pure text handoff (AD-6/NFR-5).
        Assert.Contains("copyHelperText", doc);
        // The embedded prompt is the read-only code-review prompt, attribute-escaped into the button. Asserts
        // against the named constant so a copy-edit to the directive's wording can't desync this test. [deferred-work]
        Assert.Contains(WebviewHelpers.ReadOnlyDirective, doc);
    }

    [Fact]
    public void Render_EscapesTheHelperPrompt_WhenTheSiteTitleContainsQuotes()
    {
        // A project title with a double-quote must not break out of the data attribute (it is HTML-attribute
        // escaped), so the button markup stays well-formed and the prompt cannot inject markup.
        var page = EpicPage();
        var quoted = page with { Nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "Ac\"me", hasAdrs: true, hasReadme: true).ToNavigationView("epics/epic-1.html") };
        var doc = WebviewRenderAdapter.Shared.Render(quoted).Content;

        Assert.Contains("&quot;", doc);
        Assert.DoesNotContain("data-ss-prompt=\"Please perform a thorough code review of the current uncommitted changes in Ac\"me", doc);
    }

    [Fact]
    public void Render_EscapesTheHelperPrompt_WhenTheSiteTitleContainsAngleBrackets()
    {
        // A project title carrying `<`/`>` (e.g. a stray HTML fragment in a repo name) must not let a raw tag
        // reach the data attribute — the same PathUtil.Html path that the quote case relies on must also
        // neutralize markup-injection characters, not just quotes. [deferred-work]
        var page = EpicPage();
        var tagged = page with { Nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "Ac<script>me", hasAdrs: true, hasReadme: true).ToNavigationView("epics/epic-1.html") };
        var doc = WebviewRenderAdapter.Shared.Render(tagged).Content;

        Assert.Contains("&lt;script&gt;", doc);
        Assert.DoesNotContain("<script>me", doc);
    }

    // ----- Parity is unchanged: theming re-values tokens, not facts (AC #1/#2, Task 5) -------------------------

    [Fact]
    public void ThemedWebview_StillHasFullChromeParity_AndAddsNoThemingException()
    {
        var page = EpicPage();
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        // The themed document still reproduces every semantic fact under only the registered asset/mermaid/data-island
        // exceptions — theming changed token VALUES, never nav targets / drill trail / status stage.
        var divergences = RenderParity.FindDivergences(page, doc, WebviewRenderAdapter.Shared.Id);
        Assert.True(divergences.Count == 0, "expected parity, got: " + string.Join(" | ", divergences));

        // No section.* or THEMING exception was added: the WEBVIEW surface stays at its three 6.4 chrome/asset
        // entries — asset.css, asset.js and mermaid (theming itself is not a semantic divergence). It was five
        // until ADR 0036 retired `data-island` and `hierarchy-chart`; the count is back to the original three, and
        // asset.js now records a CARRIER difference (specscribe.js is inlined, not linked) rather than an absence.
        // Other surfaces' entries — e.g. Story 6.7's single spa mermaid exception — are out of scope here.
        // [Story 23.6 AC #1] Was 3. The webview's asset.css / asset.js / mermaid entries each registered a
        // difference against the C#-rendered PAGE, and no C# code path renders one — see the retirement note in
        // HostRenderExceptions.Registry. Zero is the assertion now: this surface diverges on nothing.
        Assert.Equal(0, HostRenderExceptions.Registry.Count(e => e.SurfaceId == "webview"));
        Assert.DoesNotContain(HostRenderExceptions.Registry,
            e => e.SurfaceId == "webview" && e.FactId.Contains("theme", StringComparison.OrdinalIgnoreCase));
    }
}
