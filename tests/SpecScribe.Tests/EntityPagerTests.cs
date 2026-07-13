using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the shared prev/next sibling pager: the <see cref="EntityPager.FromSequence{T}"/> neighbor
/// selection (ends, lone member), the rendered markup (links vs disabled ends, tooltips, escaping), and the
/// empty-omission contract.</summary>
public class EntityPagerTests
{
    private static readonly string[] Seq = { "a.html", "b.html", "c.html" };

    private static EntityPager At(int index) =>
        EntityPager.FromSequence(Seq, index, s => s, s => $"page {s}");

    [Fact]
    public void FromSequence_Middle_HasBothNeighbors()
    {
        var pager = At(1);
        Assert.Equal("a.html", pager.Prev!.Href);
        Assert.Equal("c.html", pager.Next!.Href);
        Assert.False(pager.IsEmpty);
    }

    [Fact]
    public void FromSequence_First_HasNoPrev()
    {
        var pager = At(0);
        Assert.Null(pager.Prev);
        Assert.Equal("b.html", pager.Next!.Href);
    }

    [Fact]
    public void FromSequence_Last_HasNoNext()
    {
        var pager = At(2);
        Assert.Equal("b.html", pager.Prev!.Href);
        Assert.Null(pager.Next);
    }

    [Fact]
    public void FromSequence_LoneMember_IsEmpty()
    {
        var pager = EntityPager.FromSequence(new[] { "only.html" }, 0, s => s, s => s);
        Assert.True(pager.IsEmpty);
        Assert.Equal(string.Empty, pager.Render());
    }

    [Fact]
    public void FromSequence_IndexOutOfRange_IsNone()
    {
        Assert.True(EntityPager.FromSequence(Seq, -1, s => s, s => s).IsEmpty);
        Assert.True(EntityPager.FromSequence(Seq, 99, s => s, s => s).IsEmpty);
    }

    [Fact]
    public void Render_Middle_LinksBothSidesWithTooltipsAndRel()
    {
        var html = At(1).Render();

        Assert.Contains("<nav class=\"entity-pager\" aria-label=\"Sibling navigation\">", html);
        Assert.Contains("<a class=\"entity-pager-link entity-pager-prev\" href=\"a.html\" title=\"page a.html\" rel=\"prev\">&lsaquo; Prev</a>", html);
        Assert.Contains("<a class=\"entity-pager-link entity-pager-next\" href=\"c.html\" title=\"page c.html\" rel=\"next\">Next &rsaquo;</a>", html);
        Assert.DoesNotContain("is-disabled", html);
    }

    [Fact]
    public void Render_FirstItem_DisablesPrev()
    {
        var html = At(0).Render();

        Assert.Contains("<span class=\"entity-pager-link entity-pager-prev is-disabled\" aria-disabled=\"true\">&lsaquo; Prev</span>", html);
        Assert.Contains("<a class=\"entity-pager-link entity-pager-next\"", html);
    }

    [Fact]
    public void Render_LastItem_DisablesNext()
    {
        var html = At(2).Render();

        Assert.Contains("<a class=\"entity-pager-link entity-pager-prev\"", html);
        Assert.Contains("<span class=\"entity-pager-link entity-pager-next is-disabled\" aria-disabled=\"true\">Next &rsaquo;</span>", html);
    }

    [Fact]
    public void Render_EscapesHrefAndLabel()
    {
        var pager = new EntityPager(
            new PagerLink("a b.html?x=1&y=2", "fix <div> & \"q\""),
            null);

        var html = pager.Render();

        Assert.Contains("href=\"a b.html?x=1&amp;y=2\"", html);
        Assert.Contains("title=\"fix &lt;div&gt; &amp; &quot;q&quot;\"", html);
        Assert.DoesNotContain("<div>", html);
    }

    [Fact]
    public void None_RendersNothing()
    {
        Assert.True(EntityPager.None.IsEmpty);
        Assert.Equal(string.Empty, EntityPager.None.Render());
    }
}
