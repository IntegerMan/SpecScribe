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
}
