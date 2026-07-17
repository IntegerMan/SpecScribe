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
}
