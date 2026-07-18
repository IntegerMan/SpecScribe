using System.Text;
using SpecScribe;

namespace SpecScribe.Tests;

public class FollowUpRowTests
{
    [Fact]
    public void SummarizePlainText_PrefersSummaryField_OverSourceSpecNoise()
    {
        var plain = """
            source_spec: none
            summary: Stronger SpecScribe branding/iconography — a distinctive brand mark in the sidebar.
            evidence: Split from the 2026-07-12 extension-polish intent.
            """;

        var lead = FollowUpRow.SummarizePlainText(plain, maxChars: 80);

        Assert.StartsWith("Stronger SpecScribe branding/iconography", lead);
        Assert.DoesNotContain("source_spec", lead);
        Assert.DoesNotContain("summary:", lead);
    }

    [Fact]
    public void SummarizeFromHtml_PrefersSummaryField()
    {
        var html = "<p>source_spec: none</p>\n<p>summary: Webview navigation breadth — export the doc pages.</p>";

        var lead = FollowUpRow.SummarizeFromHtml(html, maxChars: 80);

        Assert.StartsWith("Webview navigation breadth", lead);
        Assert.DoesNotContain("source_spec", lead);
    }

    [Fact]
    public void SummarizePlainText_WithoutSummary_SkipsMetadataLines()
    {
        var plain = """
            source_spec: `spec-example.md`
            Still outstanding path casing issue on Windows.
            """;

        var lead = FollowUpRow.SummarizePlainText(plain);

        Assert.Contains("Still outstanding path casing", lead);
        Assert.DoesNotContain("source_spec", lead);
    }

    [Fact]
    public void SummarizePlainText_MetadataOnly_ReturnsEmpty()
    {
        var lead = FollowUpRow.SummarizePlainText("source_spec: `spec-example.md`\nevidence: review note");
        Assert.Equal(string.Empty, lead);
    }

    [Fact]
    public void SummarizePlainText_SameLineSourceSpec_KeepsTrailingProse()
    {
        var lead = FollowUpRow.SummarizePlainText("source_spec: foo.md Still outstanding path casing.");
        Assert.StartsWith("Still outstanding path casing", lead);
        Assert.DoesNotContain("source_spec", lead);
        Assert.DoesNotContain("foo.md", lead);
    }

    [Fact]
    public void SummarizePlainText_SkipsTitleAbbreviationPeriods()
    {
        var lead = FollowUpRow.SummarizePlainText("Talk to Dr. Smith about the heatmap debt before Epic 2.");
        Assert.Contains("Dr. Smith about the heatmap debt", lead);
    }

    [Fact]
    public void SummarizePlainText_SkipsEgAbbreviation()
    {
        var lead = FollowUpRow.SummarizePlainText("Use e.g. configure the path before shipping.");
        Assert.StartsWith("Use e.g. configure the path", lead);
    }

    [Fact]
    public void Render_WithDetailHref_OmitsDisclosure()
    {
        var sb = new StringBuilder();
        FollowUpRow.Render(
            sb,
            summaryHtml: "Short lead",
            statusToken: "open",
            statusLabel: "Open",
            sourceChipHtml: "Epic 1",
            detailBodyHtml: "<span>teaser should not render</span>",
            detailHref: "follow-ups/action-short-lead.html");

        var html = sb.ToString();
        Assert.Contains("followup-row-primary", html);
        Assert.Contains("follow-ups/action-short-lead.html", html);
        Assert.DoesNotContain("followup-row-detail", html);
        Assert.DoesNotContain("teaser should not render", html);
    }

    [Fact]
    public void Render_WithoutDetailHref_ShowsDisclosureBody()
    {
        var sb = new StringBuilder();
        FollowUpRow.Render(
            sb,
            summaryHtml: "Short lead",
            statusToken: "open",
            statusLabel: "Open",
            sourceChipHtml: "Epic 1",
            detailBodyHtml: "<div class=\"heavy\">full body</div>");

        var html = sb.ToString();
        Assert.Contains("followup-row-detail", html);
        Assert.Contains("full body", html);
        Assert.DoesNotContain("href=", html);
    }

    [Fact]
    public void Render_NoHrefEmptyBody_OmitsPrimary()
    {
        var sb = new StringBuilder();
        FollowUpRow.Render(
            sb,
            summaryHtml: "Short lead",
            statusToken: "open",
            statusLabel: "Open",
            sourceChipHtml: "Epic 1",
            detailBodyHtml: "",
            detailHref: null);

        var html = sb.ToString();
        Assert.Contains("followup-row-summary", html);
        Assert.DoesNotContain("followup-row-primary", html);
        Assert.DoesNotContain("followup-row-detail", html);
        Assert.DoesNotContain("href=", html);
    }
}
