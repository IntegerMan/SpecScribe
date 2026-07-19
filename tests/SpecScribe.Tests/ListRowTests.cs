using System.Text;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 10.8: the shared list-row primitive extracted from Story 9.10's FollowUpRow anatomy.</summary>
public class ListRowTests
{
    [Fact]
    public void Render_EmitsSummaryBadgeChipsAndPrimaryLink()
    {
        var sb = new StringBuilder();
        ListRow.Render(
            sb,
            summaryHtml: "My summary",
            badgeHtml: "<span class=\"status-badge done\">Done</span>",
            chipsHtml: new[] { ListRow.Chip("2026-07-19") },
            primaryLinkHtml: ListRow.PrimaryLink("target.html", "View record"));

        var html = sb.ToString();
        Assert.Contains("<li class=\"list-row\">", html);
        Assert.Contains("<span class=\"list-row-summary\">My summary</span>", html);
        Assert.Contains("<span class=\"status-badge done\">Done</span>", html);
        Assert.Contains("<span class=\"list-row-chip pill\">2026-07-19</span>", html);
        Assert.Contains("<a class=\"list-row-primary\" href=\"target.html\">View record &rarr;</a>", html);
    }

    [Fact]
    public void Render_Resolved_AddsResolvedClass()
    {
        var sb = new StringBuilder();
        ListRow.Render(sb, "Summary", badgeHtml: null, chipsHtml: Array.Empty<string>(), primaryLinkHtml: null, resolved: true);

        Assert.Contains("<li class=\"list-row resolved\">", sb.ToString());
    }

    [Fact]
    public void Render_NoBadgeOrChipsOrLink_OmitsThem()
    {
        var sb = new StringBuilder();
        ListRow.Render(sb, "Bare summary", badgeHtml: null, chipsHtml: Array.Empty<string>(), primaryLinkHtml: null);

        var html = sb.ToString();
        Assert.Contains("<span class=\"list-row-summary\">Bare summary</span>", html);
        Assert.DoesNotContain("status-badge", html);
        Assert.DoesNotContain("list-row-chip", html);
        Assert.DoesNotContain("list-row-primary", html);
    }

    [Fact]
    public void PrimaryLink_WithExtraClass_AppendsIt()
    {
        var html = ListRow.PrimaryLink("x.html", "Go", "extra-class");
        Assert.Equal("<a class=\"list-row-primary extra-class\" href=\"x.html\">Go &rarr;</a>", html);
    }

    [Fact]
    public void EmptyState_UsesPromotedConvention()
    {
        var html = ListRow.EmptyState("Nothing here yet.", "epic-card");
        Assert.Equal("<div class=\"epic-card empty-state\">\n  <div class=\"pending-note\">Nothing here yet.</div>\n</div>\n\n", html);
    }
}
