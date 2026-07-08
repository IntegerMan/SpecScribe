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

    [Fact]
    public void CountOpenItems_IgnoresNestedSubBullets()
    {
        // Two top-level items; one has a nested sub-list that must not inflate the count. [Review finding]
        var body = "<ul><li>top one<ul><li>nested a</li><li>nested b</li></ul></li><li>top two</li></ul>";
        Assert.Equal(2, WorkInventory.CountOpenItems(body));
    }

    [Fact]
    public void Build_IgnoresSpecAndDeferredFilesOutsideImplementationArtifacts()
    {
        // spec-*.md / deferred-work.md living outside implementation-artifacts/ (e.g. the spec kernel under
        // specs/spec-specscribe/) must never be swept into quick-dev/deferred work. [Review finding]
        var docs = new[]
        {
            Doc("specs/spec-specscribe/spec-outside.md", "Kernel spec", new Frontmatter { Route = "one-shot" }),
            Doc("docs/deferred-work.md", "Not the real one", Frontmatter.Empty, "<ul><li>a</li></ul>"),
        };

        var inv = WorkInventory.Build(docs);

        Assert.True(inv.IsEmpty);
    }

    [Fact]
    public void Build_FirstDeferredWorkFileWinsWhenDuplicatesExist()
    {
        var docs = new[]
        {
            Doc("implementation-artifacts/deferred-work.md", "First", Frontmatter.Empty, "<ul><li>a</li></ul>"),
            Doc("implementation-artifacts/nested/deferred-work.md", "Second", Frontmatter.Empty, "<ul><li>a</li><li>b</li></ul>"),
        };

        var inv = WorkInventory.Build(docs);

        Assert.Equal("First", inv.Deferred!.Title);
    }
}
