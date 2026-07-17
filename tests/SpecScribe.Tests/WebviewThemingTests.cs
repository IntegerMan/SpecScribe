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
        Assert.DoesNotContain("--vscode-errorForeground", bridge);
        Assert.DoesNotContain("--vscode-editorError", bridge);
        Assert.DoesNotContain("--vscode-editorWarning", bridge);
        // Story 9.5: resting AC tint companion (site parchment doesn't read on dark) beside the :target override.
        // border-color must not wipe the gold left accent — reassert border-left-color after it.
        Assert.Contains(".vscode-dark .ac-criterion,", bridge);
        Assert.Contains(".vscode-dark .ac-criterion:target,", bridge);
        Assert.Contains("border-left-color: var(--gold)", bridge);
        Assert.Contains("border-left-color: var(--gold-light)", bridge);
    }

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
        // The embedded prompt is the read-only code-review prompt, attribute-escaped into the button.
        Assert.Contains("Do NOT modify any files", doc);
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

    // ----- Parity is unchanged: theming re-values tokens, not facts (AC #1/#2, Task 5) -------------------------

    [Fact]
    public void ThemedWebview_StillHasFullChromeParity_AndAddsNoThemingException()
    {
        var page = EpicPage();
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        // The themed document still reproduces every semantic fact under only the three registered asset/mermaid
        // exceptions — theming changed token VALUES, never nav targets / drill trail / status stage.
        var divergences = RenderParity.FindDivergences(page, doc, WebviewRenderAdapter.Shared.Id);
        Assert.True(divergences.Count == 0, "expected parity, got: " + string.Join(" | ", divergences));

        // No section.* or theming exception was added: the WEBVIEW surface stays at exactly its three 6.4
        // chrome/asset entries (theming is not a semantic divergence). Other surfaces' entries — e.g. Story 6.7's
        // single spa mermaid exception — are out of scope here.
        Assert.Equal(3, HostRenderExceptions.Registry.Count(e => e.SurfaceId == "webview"));
    }
}
