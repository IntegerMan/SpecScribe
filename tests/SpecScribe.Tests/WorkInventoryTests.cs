using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>The work-inventory classifies quick-dev (spec-*.md + route: one-shot) and the deferred-work note
/// as first-class work classes, distinct from the epic/story roll-up and from Story 2.2's spec kernel.</summary>
public class WorkInventoryTests
{
    private static DocModel Doc(string sourceRel, string title, Frontmatter fm, string bodyHtml = "") => new()
    {
        SourceRelativePath = sourceRel,
        OutputRelativePath = System.IO.Path.ChangeExtension(sourceRel, ".html"),
        Title = title,
        Frontmatter = fm,
        BodyHtml = bodyHtml,
        Headings = System.Array.Empty<Heading>(),
    };

    [Fact]
    public void Build_ClassifiesQuickDevAndDeferredAndIgnoresOtherArtifacts()
    {
        var docs = new[]
        {
            Doc("implementation-artifacts/spec-foo.md", "A quick fix", new Frontmatter { Route = "one-shot", Status = "done", Type = "chore" }),
            Doc("implementation-artifacts/deferred-work.md", "Deferred Work", Frontmatter.Empty, "<ul><li>a</li><li>b</li><li>c</li></ul>"),
            // A plain artifact (no route) is NOT quick-dev.
            Doc("implementation-artifacts/some-note.md", "Some Note", Frontmatter.Empty),
            // A spec-*.md WITHOUT route: one-shot is NOT quick-dev (guards against grabbing arbitrary spec files).
            Doc("implementation-artifacts/spec-no-route.md", "No route", Frontmatter.Empty),
        };

        var inv = WorkInventory.Build(docs);

        Assert.False(inv.IsEmpty);
        Assert.Single(inv.QuickDev);
        Assert.Equal("A quick fix", inv.QuickDev[0].Title);
        Assert.Equal("done", inv.QuickDev[0].Status);
        Assert.Equal("chore", inv.QuickDev[0].Type);
        Assert.Equal("implementation-artifacts/spec-foo.html", inv.QuickDev[0].OutputPath);

        Assert.NotNull(inv.Deferred);
        Assert.Equal(3, inv.Deferred!.OpenItemCount);
    }

    [Fact]
    public void Build_EmptyWhenNoQuickDevOrDeferred()
    {
        var inv = WorkInventory.Build(new[] { Doc("planning-artifacts/prd.md", "PRD", Frontmatter.Empty) });
        Assert.True(inv.IsEmpty);
        Assert.Empty(inv.QuickDev);
        Assert.Null(inv.Deferred);
    }

    [Fact]
    public void CountOpenItems_ExcludesStruckThroughResolvedItems()
    {
        // Two open bullets, one resolved (~~…~~ → <del>): open count is 2. [Story 2.1 Task 1]
        var body = "<ul><li>open one</li><li><del>resolved</del> done</li><li>open two</li></ul>";
        Assert.Equal(2, WorkInventory.CountOpenItems(body));
    }
}
