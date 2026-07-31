using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>The shared subject the suite asserts against now that C# no longer writes a page. [Story 23.6 AC #8]
///
/// <para><b>What changed and why the substitution was safe.</b> Story 23.4 split every templater into
/// <c>BuildX → PageView</c> plus a thin HTML projection, so ~268 test call sites of the form
/// <c>XTemplater.RenderY(args)</c> were mechanically re-pointed to
/// <c>JsonSpaRenderAdapter.Shared.RenderContent(XTemplater.BuildY(args))</c>. Of those, <b>247 asserted on body
/// content and needed no further change</b> — the body is in the region unchanged. The ~21 that did break were
/// all asserting on CHROME, which is exactly the split this class exists to make explicit.</para>
///
/// <para><b>Where the chrome assertions went, so the loss is stated rather than silent (AC #8).</b> The region is
/// <c>nav markup + wayfinding + body</c>. Everything else that <c>HtmlRenderAdapter.Render</c> used to concatenate
/// — <c>&lt;title&gt;</c>, <c>&lt;meta name="description"&gt;</c>, the favicon data-URI, the skip link, the
/// stylesheet/script tags, the footer, the Mermaid init, and the Hierarchy/Graph anti-flash handshakes — is no
/// longer in any C#-produced string, because no C# code path produces a whole page.</para>
///
/// <list type="bullet">
/// <item><b>Moved to the view model.</b> <c>&lt;title&gt;</c> → <see cref="PageView.Title"/>,
/// <c>&lt;meta name="description"&gt;</c> → <see cref="PageView.MetaDescription"/>, the conditional
/// <c>&lt;script src&gt;</c> and boot handshakes → <c>page.Assets.*</c>. These are STRONGER assertions than the
/// string ones they replace: they check the decision, not one rendering of it.</item>
/// <item><b>Covered by the web gates.</b> The skip link, the footer and the head tags are asserted over emitted
/// HTML by <c>npm run check:a11y</c> (which owns <c>one-main</c>, <c>skip-link</c> and the wayfinding-balance
/// rules over every page) and by <c>npm run check:parity</c>'s <c>pageSha</c>, which hashes the WHOLE page for a
/// frozen 24-route corpus. ⚠️ Note that <c>pageSha</c> is the FIRST gate this project has ever had over that
/// chrome — the old <c>measure:parity</c> oracle hashed <c>&lt;main&gt;</c> only.</item>
/// </list>
///
/// <para><b>What this class deliberately does NOT provide: a full-page composer.</b> AC #1 forecloses it and the
/// reason is concrete — a test-only composer recreates the deleted writer, so a chrome regression would pass a
/// green suite while the shipped page was wrong. Everything here operates on the region or the view model.</para></summary>
internal static class RegionAssert
{
    /// <summary>The page's swappable content region — the same string the IR ships and the webview and SPA
    /// consume, so an assertion here is an assertion about what actually reaches every surface.</summary>
    public static string Of(PageView page) => JsonSpaRenderAdapter.Shared.RenderContent(page);

    /// <summary>The a11y facts that genuinely live in the REGION rather than in chrome: exactly one
    /// <c>&lt;main id="main-content"&gt;</c> landmark, and a wayfinding band that opens before it.
    /// <para>The skip link is NOT checked here — it is emitted by the head, which the region does not carry.
    /// <c>npm run check:a11y</c> owns it over the emitted page, which is the only place it can be checked
    /// honestly now.</para></summary>
    public static void HasSingleMainLandmark(string region)
    {
        var count = Occurrences(region, "id=\"main-content\"");
        Assert.True(count == 1, $"expected exactly one main landmark in the region, found {count}");
    }

    /// <summary>Asserts the page's document title from the VIEW MODEL. Replaces
    /// <c>Assert.Contains("&lt;title&gt;…&lt;/title&gt;", html)</c>, which asserted one rendering of this value.</summary>
    public static void HasTitle(PageView page, string expected) => Assert.Equal(expected, page.Title);

    private static int Occurrences(string haystack, string needle)
    {
        var n = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}
