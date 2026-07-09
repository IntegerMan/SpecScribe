using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>The artifact-coverage model recognizes the V1 "Core + Orchestration" canonical family set from
/// source paths, derives freshness/staleness from source-file mtimes, and consumes memlog dates only as
/// strictly-additive secondary enrichment (PRD FR-11 / Story 3.3). All pure — no disk.</summary>
public class ArtifactCoverageTests
{
    private static readonly IReadOnlyDictionary<string, DateOnly> NoDates = new Dictionary<string, DateOnly>();
    private static readonly IReadOnlyDictionary<string, DateOnly> NoMemlog = new Dictionary<string, DateOnly>();
    private static readonly DateOnly Today = new(2026, 7, 8);

    private static ArtifactFamily Family(ArtifactCoverage cov, string label) =>
        cov.Families.Single(f => f.Label == label);

    private static string[] AllFamilyPaths() => new[]
    {
        "planning-artifacts/prds/prd-x/prd.md",
        "planning-artifacts/briefs/brief.md",
        "specs/spec-x/ARCHITECTURE-SPINE.md",
        "planning-artifacts/ux-designs/ux-x/DESIGN.md",
        "specs/spec-x/SPEC.md",
        "planning-artifacts/epics.md",
        "implementation-artifacts/3-3-agent-coverage.md",
        "planning-artifacts/requirements.md",
    };

    [Fact]
    public void Build_AllCanonicalFamiliesPresent()
    {
        var cov = ArtifactCoverage.Build(AllFamilyPaths(), NoDates, NoMemlog, Today);

        Assert.False(cov.IsEmpty);
        Assert.Equal(0, cov.MissingCount);
        Assert.All(cov.Families, f => Assert.True(f.Present, $"expected {f.Label} present"));
        // Every family reuses an existing Icons.ForConcept glyph key (no new icon vocabulary).
        Assert.All(cov.Families, f => Assert.NotEqual(string.Empty, Icons.ForConcept(f.ConceptIconKey)));
    }

    [Fact]
    public void Build_MissingFamiliesReportedWithExactAbsentSet()
    {
        // Only PRD + Epics supplied — everything else must read as missing.
        var paths = new[] { "planning-artifacts/prds/prd-x/prd.md", "planning-artifacts/epics.md" };
        var cov = ArtifactCoverage.Build(paths, NoDates, NoMemlog, Today);

        Assert.False(cov.IsEmpty);
        Assert.Equal(2, cov.PresentCount);
        var absent = cov.Families.Where(f => !f.Present).Select(f => f.Label).OrderBy(s => s).ToArray();
        Assert.Equal(
            new[] { "Architecture", "Product Brief", "Requirements", "Spec Kernel", "Stories", "UX" },
            absent);
    }

    [Fact]
    public void Build_EmptyWhenNoRecognizedFamilies()
    {
        // A repo with only unknown/custom files → panel is omitted (IsEmpty), never throws.
        var cov = ArtifactCoverage.Build(
            new[] { "notes/random.md", "README.md", "docs/some-guide.md" }, NoDates, NoMemlog, Today);

        Assert.True(cov.IsEmpty);
        Assert.Equal(0, cov.PresentCount);
    }

    [Fact]
    public void Build_StoryFamilyPresentOnlyWithEpicStoryArtifact()
    {
        // A retrospective / deferred-work file under implementation-artifacts is NOT a story artifact.
        var notStories = new[]
        {
            "implementation-artifacts/epic-1-retrospective.md",
            "implementation-artifacts/deferred-work.md",
            "implementation-artifacts/spec-quick-fix.md",
        };
        Assert.False(Family(ArtifactCoverage.Build(notStories, NoDates, NoMemlog, Today), "Stories").Present);

        // One <n>-<n>-*.md file flips Stories to present.
        var withStory = notStories.Append("implementation-artifacts/1-2-user-auth.md").ToArray();
        Assert.True(Family(ArtifactCoverage.Build(withStory, NoDates, NoMemlog, Today), "Stories").Present);
    }

    [Theory]
    [InlineData("planning-artifacts/ux-designs/ux-x/DESIGN.md")]
    [InlineData("planning-artifacts/ux-designs/ux-x/EXPERIENCE.md")]
    public void Build_UxFamilyPresentWhenEitherDesignOrExperienceExists(string uxPath)
    {
        var cov = ArtifactCoverage.Build(new[] { uxPath }, NoDates, NoMemlog, Today);
        Assert.True(Family(cov, "UX").Present);
    }

    [Fact]
    public void Build_UnknownAndCustomPathsIgnoredWithoutThrowing()
    {
        // AC #2: unknown or custom files do not cause generation failure — they simply aren't matched.
        var cov = ArtifactCoverage.Build(
            new[] { "planning-artifacts/prds/prd-x/prd.md", "custom/weird-thing.md", "prd.md.bak" },
            NoDates, NoMemlog, Today);

        Assert.True(Family(cov, "PRD").Present);
        Assert.Equal(1, cov.PresentCount);
    }

    [Fact]
    public void IsStale_BoundaryIsExclusiveAtThreshold()
    {
        var path = "planning-artifacts/prds/prd-x/prd.md";
        DateOnly Modified(int daysAgo) => Today.AddDays(-daysAgo);

        // Exactly StalenessThresholdDays old → NOT stale; one day older → stale.
        var atThreshold = ArtifactCoverage.Build(new[] { path },
            new Dictionary<string, DateOnly> { [path] = Modified(ArtifactCoverage.StalenessThresholdDays) }, NoMemlog, Today);
        Assert.False(Family(atThreshold, "PRD").IsStale(Today));
        Assert.Equal(0, atThreshold.StaleCount(Today));

        var pastThreshold = ArtifactCoverage.Build(new[] { path },
            new Dictionary<string, DateOnly> { [path] = Modified(ArtifactCoverage.StalenessThresholdDays + 1) }, NoMemlog, Today);
        Assert.True(Family(pastThreshold, "PRD").IsStale(Today));
        Assert.Equal(1, pastThreshold.StaleCount(Today));
    }

    [Fact]
    public void IsStale_MissingOrUnknownDateIsNeverStale()
    {
        // Missing family: never stale. Present-but-no-mtime family: unknown ≠ old, so never stale.
        var cov = ArtifactCoverage.Build(new[] { "planning-artifacts/prds/prd-x/prd.md" }, NoDates, NoMemlog, Today);
        Assert.False(Family(cov, "PRD").IsStale(Today));   // present, LastModified null
        Assert.False(Family(cov, "Epics").IsStale(Today)); // missing entirely
        Assert.Equal(0, cov.StaleCount(Today));
    }

    [Fact]
    public void Build_ClampsFutureDatedMtimeToGenerationDate()
    {
        var path = "planning-artifacts/prds/prd-x/prd.md";
        var cov = ArtifactCoverage.Build(new[] { path },
            new Dictionary<string, DateOnly> { [path] = Today.AddDays(5) }, NoMemlog, Today);

        // Future skew is clamped to today — never reads as "edited in the future", never stale.
        Assert.Equal(Today, Family(cov, "PRD").LastModified);
        Assert.False(Family(cov, "PRD").IsStale(Today));
    }

    [Fact]
    public void Build_PopulatesDescriptionAndLeavesPresentationFieldsNull()
    {
        // Description is static canonical data set by the pure Build; Href/CreateCommand are generator-resolved
        // (page routing + detected module) so Build must leave them null — it stays purely source-derived.
        var cov = ArtifactCoverage.Build(AllFamilyPaths(), NoDates, NoMemlog, Today);

        Assert.All(cov.Families, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));
        Assert.All(cov.Families, f => Assert.Null(f.Href));
        Assert.All(cov.Families, f => Assert.Null(f.CreateCommand));
    }

    [Fact]
    public void CreateStepKeys_CoverEveryFamilyThatHasAWorkflow()
    {
        // The step-key map is the single source the generator uses to resolve a missing family's create
        // command; every family here should map to a workflow step (Spec Kernel intentionally has one too,
        // even if a given module may not expose it — resolution degrades at Command() time, not here).
        var cov = ArtifactCoverage.Build(AllFamilyPaths(), NoDates, NoMemlog, Today);
        foreach (var f in cov.Families)
        {
            Assert.True(ArtifactCoverage.CreateStepKeys.ContainsKey(f.Label), $"no step key for {f.Label}");
        }
    }

    [Fact]
    public void Build_MemlogEnrichmentAttachesToMatchingFamily()
    {
        var path = "planning-artifacts/prds/prd-x/prd.md";
        var memlogDate = new DateOnly(2026, 7, 1);
        var cov = ArtifactCoverage.Build(new[] { path }, NoDates,
            new Dictionary<string, DateOnly> { ["PRD"] = memlogDate }, Today);

        Assert.Equal(memlogDate, Family(cov, "PRD").MemlogUpdated);
    }

    [Fact]
    public void Build_MemlogIsStrictlyAdditive_PrimaryCoverageIdenticalWithOrWithoutMemlog()
    {
        // AC #2: memlog is secondary enrichment only. Present/LastModified must be identical whether or not a
        // memlog map is supplied — memlog adds ONLY the optional MemlogUpdated date on top.
        var paths = AllFamilyPaths();
        var mtimes = new Dictionary<string, DateOnly>
        {
            ["planning-artifacts/prds/prd-x/prd.md"] = new DateOnly(2026, 6, 15),
        };

        var withoutMemlog = ArtifactCoverage.Build(paths, mtimes, NoMemlog, Today);
        var withMemlog = ArtifactCoverage.Build(paths, mtimes,
            new Dictionary<string, DateOnly> { ["PRD"] = new DateOnly(2026, 7, 2) }, Today);

        foreach (var baseline in withoutMemlog.Families)
        {
            var enriched = Family(withMemlog, baseline.Label);
            Assert.Equal(baseline.Present, enriched.Present);
            Assert.Equal(baseline.LastModified, enriched.LastModified);
        }

        // The only difference is the additive memlog date on PRD.
        Assert.Null(Family(withoutMemlog, "PRD").MemlogUpdated);
        Assert.Equal(new DateOnly(2026, 7, 2), Family(withMemlog, "PRD").MemlogUpdated);
    }
}
