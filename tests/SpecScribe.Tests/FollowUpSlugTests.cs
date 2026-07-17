using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 9.11 AC #2 — slug stability: pure function of authored text; content-hash
/// disambiguation on collision only; never positional; survives reordering.</summary>
public class FollowUpSlugTests
{
    [Fact]
    public void BaseForAction_IsKindPrefixedKebab_FromActionText()
    {
        var item = new SprintActionItem("Fix the heatmap debt now", "open", 1, "Dana");
        Assert.Equal("action-fix-the-heatmap-debt-now", FollowUpSlug.BaseForAction(item));
    }

    [Fact]
    public void BaseForDeferred_UsesLeadAndProvenance()
    {
        var item = new DeferredWorkItem("<p>Open casing mismatch still outstanding.</p>", false, null, null);
        var slug = FollowUpSlug.BaseForDeferred(item, "code review of 1-1-foundation.md");
        Assert.StartsWith("deferred-", slug);
        Assert.Contains("open-casing-mismatch", slug);
        Assert.Matches(@"^[a-z0-9-]+$", slug);
    }

    [Fact]
    public void AssignActionSlugs_SameText_SameSlug_RegardlessOfOrder()
    {
        var a = new SprintActionItem("Schedule retros promptly", "open", 1, "Amelia");
        var b = new SprintActionItem("Ship the release notes", "open", 2, "Dana");
        var c = new SprintActionItem("Unscoped cleanup", "open", null, null);

        var forward = FollowUpSlug.AssignActionSlugs(new[] { a, b, c });
        var reverse = FollowUpSlug.AssignActionSlugs(new[] { c, b, a });

        Assert.Equal(forward[a], reverse[a]);
        Assert.Equal(forward[b], reverse[b]);
        Assert.Equal(forward[c], reverse[c]);
        Assert.Equal("action-schedule-retros-promptly", forward[a]);
        Assert.DoesNotContain("-2", forward[a]);
        Assert.DoesNotContain("-3", forward[a]);
    }

    [Fact]
    public void AssignActionSlugs_NearIdenticalTexts_GetDistinctStableSlugs()
    {
        var a = new SprintActionItem("Route Epic 1 deferred tech debt into backlog", "open", 1, "Dana");
        var b = new SprintActionItem("Route Epic 1 deferred tech debt into backlog review", "open", 2, "Winston");

        // Force same base by using identical action text with different epics.
        var twinA = new SprintActionItem("Same action text for collision", "open", 1, "Dana");
        var twinB = new SprintActionItem("Same action text for collision", "open", 2, "Winston");

        var slugs = FollowUpSlug.AssignActionSlugs(new[] { twinA, twinB, a, b });

        Assert.NotEqual(slugs[twinA], slugs[twinB]);
        Assert.StartsWith("action-same-action-text-for-collision-", slugs[twinA]);
        Assert.StartsWith("action-same-action-text-for-collision-", slugs[twinB]);
        Assert.Matches(@"-[a-f0-9]{6}$", slugs[twinA]);
        Assert.Matches(@"-[a-f0-9]{6}$", slugs[twinB]);

        // Reorder must not change either twin's slug.
        var reordered = FollowUpSlug.AssignActionSlugs(new[] { twinB, a, twinA, b });
        Assert.Equal(slugs[twinA], reordered[twinA]);
        Assert.Equal(slugs[twinB], reordered[twinB]);
    }

    [Fact]
    public void AssignActionSlugs_UniqueItems_StayCleanWithoutHashSuffix()
    {
        var item = new SprintActionItem("Only one of these", "open", 1, null);
        var slug = FollowUpSlug.AssignActionSlugs(new[] { item })[item];
        Assert.Equal("action-only-one-of-these", slug);
        Assert.DoesNotMatch(@"-[a-f0-9]{6}$", slug);
    }

    [Fact]
    public void Kebabize_IsFilesystemAndUrlSafe_CapsWords()
    {
        var slug = FollowUpSlug.Kebabize("Hello, World!!! Foo Bar Baz Qux Quux Corge Grault Garply");
        Assert.Matches(@"^[a-z0-9-]+$", slug);
        Assert.Equal(8, slug.Split('-').Length);
        Assert.DoesNotContain("--", slug);
    }

    [Fact]
    public void OutputPath_IsUnderFollowUpsFolder()
    {
        Assert.Equal("follow-ups/action-fix-it.html", FollowUpSlug.OutputPath("action-fix-it"));
    }

    [Fact]
    public void ContentSuffix_IsDeterministicSixHex()
    {
        var a = FollowUpSlug.ContentSuffix("same identity");
        var b = FollowUpSlug.ContentSuffix("same identity");
        var c = FollowUpSlug.ContentSuffix("other identity");
        Assert.Equal(a, b);
        Assert.Equal(6, a.Length);
        Assert.Matches(@"^[a-f0-9]{6}$", a);
        Assert.NotEqual(a, c);
    }
}
