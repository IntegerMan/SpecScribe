using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pins the output-injection surface Story 17.2 Task 1 measured end to end. [ADR 0042]
///
/// <para>The measurement, for the record: a `.md` containing <c>&lt;img src=x onerror="…"&gt;</c> was generated
/// through the real pipeline at baseline <c>e8a689d</c> and the handler survived VERBATIM into the shipped
/// <c>.html</c> — as did <c>&lt;svg onload&gt;</c>, a raw <c>javascript:</c> href, an <c>&lt;iframe srcdoc&gt;</c>
/// and an <c>&lt;object data="data:text/html,…"&gt;</c>. Markdig also manufactured a <c>javascript:</c> href from
/// ordinary <c>[text](javascript:…)</c> markdown link syntax, which needs no raw HTML in the source at all.
/// The static site carries no CSP, so on a GitHub-Pages-published portal that is stored XSS.</para>
///
/// <para>These tests assert on <see cref="DocModel.BodyHtml"/> — the IR payload — because that is what ADR 0016
/// carries verbatim and <c>v-html</c> injects. Asserting on the sanitizer in isolation would not prove the
/// pipeline actually calls it.</para></summary>
public class HtmlSafetyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("specscribe-htmlsafety-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string BodyHtml(string markdown)
    {
        var path = Path.Combine(_dir, "doc.md");
        File.WriteAllText(path, markdown);
        return MarkdownConverter.Convert(path, "doc.md", "doc.html").BodyHtml;
    }

    // ---- The measured vectors, each pinned individually so a regression names the vector it lost. ----

    [Fact]
    public void InlineEventHandlerIsDropped_ImgOnError()
    {
        var html = BodyHtml("""Image: <img src=x onerror="alert(1)">""");

        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", html, StringComparison.Ordinal);
        // The element itself survives — only the handler is removed.
        Assert.Contains("<img", html, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineEventHandlerIsDropped_SvgOnLoad()
    {
        var html = BodyHtml("""Svg: <svg onload="alert(1)"></svg>""");

        Assert.DoesNotContain("onload", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<svg>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void UnquotedEventHandlerIsDropped()
    {
        // The quote-required form of this check let exactly this through once already (Story 18.4 review).
        var html = BodyHtml("""Bare: <img src=x onerror=alert(1)>""");

        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JavascriptHrefIsDropped_FromRawHtml()
    {
        var html = BodyHtml("""Link: <a href="javascript:alert(1)">click</a>""");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        // Link text is the author's prose and stays visible.
        Assert.Contains("click", html, StringComparison.Ordinal);
    }

    [Fact]
    public void JavascriptHrefIsBlanked_FromMarkdownLinkSyntax()
    {
        // No raw HTML at all — this is the cheaper vector and the one a raw-HTML-only fix would miss.
        var html = BodyHtml("Link: [click](javascript:alert(1))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("click", html, StringComparison.Ordinal);
    }

    [Theory]
    // Browsers ignore whitespace and control characters while sniffing a scheme, and decode entities before
    // resolving the URL — so each of these navigates despite not matching a naive "starts with javascript:".
    [InlineData("java\tscript:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    public void JavascriptHrefObfuscationsAreDropped(string url)
    {
        var html = BodyHtml($"""Link: <a href="{url}">click</a>""");

        Assert.DoesNotContain("alert(1)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbeddingElementsAreEscapedNotExecuted()
    {
        // An `iframe srcdoc` executes; ADR 0021 already names the whole family. Escaped to visible text rather
        // than deleted, so a reader can see what the source claimed.
        var html = BodyHtml("""Frame: <iframe srcdoc="&lt;svg onload=alert(1)&gt;"></iframe>""");

        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;iframe", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScriptTagIsEscaped_AndNoLongerFailsTheRender()
    {
        // Before this story a literal <script> in a source .md reached the IR, tripped IrSurface's
        // executable-island throw, and the page 500'd — so hostile markdown could DENY SERVICE to a page, not
        // just inject into it. Escaping it closes both.
        var html = BodyHtml("""Script: <script>alert(1)</script>""");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BaseAndMetaAreEscaped()
    {
        // `<base>` silently re-points every relative URL on the page; `<meta http-equiv=refresh>` redirects.
        // Neither carries a handler or a javascript: URL, so neither is caught by handler-stripping alone.
        var html = BodyHtml("""<base href="https://evil.example/">""");

        Assert.DoesNotContain("<base", html, StringComparison.OrdinalIgnoreCase);
    }

    // ---- The other half: benign markup must survive BYTE-IDENTICALLY. ----
    //
    // This is not politeness. ADR 0016's verbatim carriage is what Epic 23 bought by not reimplementing ~889
    // LOC of custom renderers in Vue; a sanitizer that mangles legitimate HTML forfeits it. This repository's
    // own `epics.md` uses <details> today, so a blanket-escape answer had a real, measurable cost.

    [Theory]
    [InlineData("<details><summary>ok</summary>body</details>")]
    [InlineData("Press <kbd>Ctrl</kbd>")]
    [InlineData("Line one<br>line two")]
    [InlineData("<span class=\"pill\">tag</span>")]
    [InlineData("<sub>2</sub> and <sup>3</sup>")]
    [InlineData("<abbr title=\"as soon as possible\">ASAP</abbr>")]
    public void BenignRawHtmlSurvivesUnchanged(string fragment)
    {
        var html = BodyHtml(fragment);

        Assert.Contains(fragment, html, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryLinksAreUntouched()
    {
        var html = BodyHtml("[docs](https://example.com/a?b=c#d) and [rel](../other.md)");

        Assert.Contains("https://example.com/a?b=c#d", html, StringComparison.Ordinal);
        Assert.Contains("../other.md", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapedProseAboutHandlersIsNotCorrupted()
    {
        // THE TRAP THIS DESIGN EXISTS TO AVOID. This portal renders its own source, so `onerror=` appears
        // legitimately — escaped — inside code spans on the generated Code Map and on this very story's page.
        // A regex pass over finished HTML would rewrite the documentation while every gate stayed green.
        var html = BodyHtml("""Prose about `<img src=x onerror="alert(1)">` in a code span.""");

        Assert.Contains("onerror", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FencedCodeBlocksAreNotCorrupted()
    {
        var html = BodyHtml("""
            ```html
            <img src=x onerror="alert(1)">
            ```
            """);

        Assert.Contains("onerror", html, StringComparison.Ordinal);
    }

    // ---- Unit-level checks on the policy itself. ----

    [Theory]
    [InlineData("javascript:alert(1)", true)]
    [InlineData("vbscript:msgbox(1)", true)]
    [InlineData("data:text/html,<svg onload=1>", true)]
    [InlineData("data:image/svg+xml;base64,AAAA", true)]
    [InlineData("data:image/png;base64,AAAA", false)]
    [InlineData("https://example.com", false)]
    [InlineData("../relative/path.html", false)]
    [InlineData("#anchor", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDangerousUrl_Classifies(string? url, bool expected) =>
        Assert.Equal(expected, HtmlSafety.IsDangerousUrl(url));

    [Fact]
    public void SanitizeRawHtml_FailsClosedOnUnterminatedTag()
    {
        // Being unable to PROVE a fragment inert is treated the same as proving it dangerous.
        var result = HtmlSafety.SanitizeRawHtml("<img src=x onerror=alert(1)");

        // The handler text SURVIVES — as inert, visible text. That is the point of failing closed: the whole
        // fragment stops being markup, so there is no element left for a handler to attach to. Asserting the
        // substring is absent would be asserting the wrong property.
        Assert.StartsWith("&lt;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeRawHtml_PreservesTextAroundTags()
    {
        var result = HtmlSafety.SanitizeRawHtml("before <b>bold</b> after");

        Assert.Equal("before <b>bold</b> after", result);
    }

    [Fact]
    public void ContainsExecutableMarkup_StillDetectsForCarriedArtifacts()
    {
        // IdeaDiscovery's REJECT decision keeps using this; only the pattern's home moved.
        Assert.True(HtmlSafety.ContainsExecutableMarkup("""<div onerror=alert(1)>x</div>"""));
        Assert.True(HtmlSafety.ContainsExecutableMarkup("""<a href="javascript:x">y</a>"""));
        Assert.False(HtmlSafety.ContainsExecutableMarkup("""<p>ordinary <b>prose</b></p>"""));
    }
}
