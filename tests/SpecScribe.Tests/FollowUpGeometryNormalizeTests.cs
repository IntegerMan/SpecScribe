using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Source-key strip + FindQuickDev matching. [spec-9-13-deferred-glance-weight-noplan-sourcekey]</summary>
public class FollowUpGeometryNormalizeTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("`spec-foo.md`", "spec-foo")]
    [InlineData("spec-foo.html", "spec-foo")]
    [InlineData("9-13-slug.md", "9-13-slug")]
    [InlineData("spec-foo.md.html", "spec-foo")]
    public void NormalizeSourceKey_StripsExtensionsAndBackticks(string? input, string expected)
    {
        Assert.Equal(expected, FollowUpGeometry.NormalizeSourceKey(input));
    }

    [Fact]
    public void FindQuickDev_MatchesStemMdAndHtmlKeys()
    {
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("Demo", "quick/spec-demo.html", "ready", "chore"),
            },
            Deferred = null,
        };

        Assert.NotNull(FollowUpGeometry.FindQuickDev(work, "spec-demo"));
        Assert.NotNull(FollowUpGeometry.FindQuickDev(work, "spec-demo.md"));
        Assert.NotNull(FollowUpGeometry.FindQuickDev(work, "`spec-demo.html`"));
        Assert.Null(FollowUpGeometry.FindQuickDev(work, "spec-other"));
        Assert.Null(FollowUpGeometry.FindQuickDev(work, null));
    }

    [Fact]
    public void UnstructuredItems_NestedList_PreservesInnerHtmlAndSibling()
    {
        var body = "<ul><li>Outer with nested<ul><li>inner</li></ul> tail</li><li>Sibling</li></ul>";
        var items = FollowUpGeometry.UnstructuredItems(body);

        Assert.Equal(2, items.Count);
        Assert.Contains("<ul><li>inner</li></ul>", items[0].BodyHtml);
        Assert.Contains("Outer with nested", items[0].BodyHtml);
        Assert.Equal("Sibling", items[1].BodyHtml);
    }

    [Fact]
    public void UnstructuredItems_UnclosedTopLevel_StillExtractsLaterSibling()
    {
        // Missing </li> on first item must not abort the scan. [spec-epic9-deferred-debt-cleanup]
        var body = "<ul><li>Broken open<li>Later sibling</li></ul>";
        var items = FollowUpGeometry.UnstructuredItems(body);

        Assert.Contains(items, i => i.BodyHtml.Contains("Later sibling", StringComparison.Ordinal));
    }

    [Fact]
    public void UnstructuredItems_LinkTag_DoesNotCountAsListItem()
    {
        // <link> shares the <li prefix — must not inflate LI depth. [review patch]
        var body = "<ul><li>Item with <link rel=\"x\" href=\"y\"> and text</li><li>Sibling</li></ul>";
        var items = FollowUpGeometry.UnstructuredItems(body);

        Assert.Equal(2, items.Count);
        Assert.Contains("<link", items[0].BodyHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Sibling", items[1].BodyHtml);
    }
}
