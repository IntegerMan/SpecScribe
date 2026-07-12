using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for <see cref="SpaDelivery"/>'s pure string-slicing helpers — the landmark extraction the
/// whole-site consolidation depends on (Story 6.7). Complements the higher-level integration coverage in
/// <see cref="SiteGeneratorSpaTests"/> and <see cref="RenderSpaParityTests"/> with direct, adversarial-input cases
/// review flagged: a page whose raw HTML legitimately contains an EARLIER literal "&lt;/main&gt;"/"&lt;main id=..."
/// occurrence before the real landmark (reachable via Markdig's raw-HTML passthrough on any user-authored doc, not
/// just this repo's own content) must degrade gracefully, never crash the whole `--spa` emit.</summary>
public class SpaDeliveryTests
{
    private const string NavMarkup = "<nav class=\"site-nav\">NAV</nav>";

    [Fact]
    public void ExtractContentRegion_IgnoresAnEarlierLiteralClosingTag_BeforeTheRealLandmark()
    {
        // A doc whose body legitimately shows the landmark markup as an example (raw HTML passthrough), BEFORE the
        // real <main id="main-content"> the page itself carries. mainClose must never resolve to an index earlier
        // than mainOpen — that would make the slice below throw ArgumentOutOfRangeException.
        var page = "<body>"
            + "<p>Example: &lt;/main&gt; is not real markup, just a code sample rendered as text</p>"
            + "</main>" // a raw-HTML passthrough closer that is NOT the real landmark's closer
            + "<div class=\"breadcrumb\"><a href=\"index.html\">Home</a></div>"
            + "<main id=\"main-content\"><p>Real body</p></main>"
            + "</body>";

        var region = SpaDelivery.ExtractContentRegion(page, NavMarkup);

        Assert.Contains("Real body", region);
        Assert.Contains(NavMarkup, region);
    }

    [Fact]
    public void ExtractContentRegion_DegradesToNavOnly_WhenNoLandmarkIsPresent()
    {
        var region = SpaDelivery.ExtractContentRegion("<body>no landmark here</body>", NavMarkup);
        Assert.Equal(NavMarkup, region);
    }

    [Fact]
    public void ExtractBreadcrumb_RecoversLabelsAndTargets_FromCapturedHtml()
    {
        var page = "<div class=\"breadcrumb\" aria-label=\"Breadcrumb\">\n"
            + "  <a href=\"../index.html\">Home</a>\n"
            + "  <span class=\"crumb-sep\">/</span>\n"
            + "  <span class=\"crumb-current\" aria-current=\"page\">Widget</span>\n"
            + "</div>\n\n"
            + "<main id=\"main-content\"></main>";

        var crumbs = SpaDelivery.ExtractBreadcrumb(page, "requirements/widget.html");

        Assert.Equal(2, crumbs.Count);
        Assert.Equal(("Home", "index.html"), (crumbs[0].Label, crumbs[0].OutputRelativePath));
        Assert.Equal(("Widget", (string?)null), (crumbs[1].Label, crumbs[1].OutputRelativePath));
    }

    [Fact]
    public void ExtractBreadcrumb_IsEmpty_WhenPageCarriesNoBreadcrumb()
    {
        var crumbs = SpaDelivery.ExtractBreadcrumb("<main id=\"main-content\"></main>", "index.html");
        Assert.Empty(crumbs);
    }
}
