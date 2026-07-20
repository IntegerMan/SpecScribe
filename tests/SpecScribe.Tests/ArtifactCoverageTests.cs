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

    [Fact]
    public void Build_StoryFilenameWithEmptyTitleSegmentIsNotAStoryArtifact()
    {
        // A malformed "1-2-.md" (empty title after the epic-story prefix) must not count as a story artifact.
        // [Story 3.3 review]
        var malformed = new[] { "implementation-artifacts/1-2-.md" };
        Assert.False(Family(ArtifactCoverage.Build(malformed, NoDates, NoMemlog, Today), "Stories").Present);
    }

    [Fact]
    public void Build_StoriesTolerateNestedImplementationArtifacts()
    {
        // Location tolerance (Story 4.2 Task 4): the folder may sit deeper in the tree — coverage must agree
        // with the adapter's story discovery, which classifies by ancestor segment, not fixed parent.
        var nested = new[] { "tracking/implementation-artifacts/1-2-user-auth.md" };
        Assert.True(Family(ArtifactCoverage.Build(nested, NoDates, NoMemlog, Today), "Stories").Present);

        // But a like-named file with no implementation-artifacts/ ancestor still isn't a story.
        var elsewhere = new[] { "tracking/other/1-2-user-auth.md" };
        Assert.False(Family(ArtifactCoverage.Build(elsewhere, NoDates, NoMemlog, Today), "Stories").Present);
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
    public void Build_UxFamilyWithBothFilesPicksTheMoreRecentlyModifiedOne()
    {
        // When both DESIGN.md and EXPERIENCE.md exist, the canonical match is whichever has the fresher mtime
        // — not just whichever sorts first — so freshness/staleness reflects the file actually most recently
        // touched instead of silently ignoring the other candidate. [Story 3.3 review]
        var design = "planning-artifacts/ux-designs/ux-x/DESIGN.md";
        var experience = "planning-artifacts/ux-designs/ux-x/EXPERIENCE.md";
        var dates = new Dictionary<string, DateOnly>
        {
            [design] = new DateOnly(2026, 6, 1),
            [experience] = new DateOnly(2026, 7, 1),
        };

        var cov = ArtifactCoverage.Build(new[] { design, experience }, dates, NoMemlog, Today);

        var ux = Family(cov, "UX");
        Assert.Equal(experience, ux.SourcePath);
        Assert.Equal(new DateOnly(2026, 7, 1), ux.LastModified);
    }

    [Fact]
    public void Build_DuplicateFamilyFileResolvesDeterministicallyRegardlessOfInputOrder()
    {
        // Two files matching the same family (e.g. a stray duplicate prd.md) must resolve to the same
        // canonical match no matter which order the caller's path list happens to enumerate them in — the
        // previous "first match" behavior depended on unsorted input order. [Story 3.3 review]
        var pathA = "planning-artifacts/prds/prd-a/prd.md";
        var pathB = "planning-artifacts/prds/prd-b/prd.md";

        var forward = ArtifactCoverage.Build(new[] { pathA, pathB }, NoDates, NoMemlog, Today);
        var reversed = ArtifactCoverage.Build(new[] { pathB, pathA }, NoDates, NoMemlog, Today);

        Assert.Equal(Family(forward, "PRD").SourcePath, Family(reversed, "PRD").SourcePath);
    }

    [Fact]
    public void AllCandidatePaths_ReturnsEveryMatchNotJustTheWinner()
    {
        var design = "planning-artifacts/ux-designs/ux-x/DESIGN.md";
        var experience = "planning-artifacts/ux-designs/ux-x/EXPERIENCE.md";
        var unrelated = "custom/notes.md";

        var candidates = ArtifactCoverage.AllCandidatePaths(new[] { design, experience, unrelated });

        Assert.Contains(design, candidates);
        Assert.Contains(experience, candidates);
        Assert.DoesNotContain(unrelated, candidates);
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

/// <summary>Direct unit coverage for <see cref="SiteGenerator.SelectMemlogUpdatedByFamily"/> — the pure
/// closest-ancestor memlog-selection core of <c>BuildMemlogMap</c> extracted for testability (Story 3.3
/// deferred-debt cleanup). All pure — no disk. Families are hand-built with only the two fields the method
/// reads (<c>Label</c>, <c>SourcePath</c>); every other <see cref="ArtifactFamily"/> field is irrelevant here.</summary>
public class SiteGeneratorMemlogSelectionTests
{
    private static ArtifactFamily Family(string label, string sourcePath) =>
        new(Label: label, ConceptIconKey: label, Description: "", Present: true, LastModified: null,
            SourcePath: sourcePath, MemlogUpdated: null);

    [Fact]
    public void NoMemlogCandidates_ReturnsEmptyMap()
    {
        var families = new[] { Family("PRD", "planning-artifacts/prds/prd-x/prd.md") };

        var map = SiteGenerator.SelectMemlogUpdatedByFamily(Array.Empty<(string Dir, DateOnly Updated)>(), families);

        Assert.Empty(map);
    }

    [Fact]
    public void RootOnlyMemlog_AppliesToEveryPresentFamily()
    {
        var families = new[]
        {
            Family("PRD", "planning-artifacts/prds/prd-x/prd.md"),
            Family("Epics", "planning-artifacts/epics.md"),
        };
        var rootDate = new DateOnly(2026, 6, 1);

        var map = SiteGenerator.SelectMemlogUpdatedByFamily(
            new (string Dir, DateOnly Updated)[] { (Dir: "", Updated: rootDate) }, families);

        Assert.Equal(rootDate, map["PRD"]);
        Assert.Equal(rootDate, map["Epics"]);
    }

    [Fact]
    public void RootOnlyMemlog_FamilyLookupIsCaseInsensitive()
    {
        // The returned dictionary is built with StringComparer.OrdinalIgnoreCase (production code's deliberate
        // choice) — a caller looking up a family by a differently-cased label must still find it.
        var families = new[] { Family("PRD", "planning-artifacts/prds/prd-x/prd.md") };
        var rootDate = new DateOnly(2026, 6, 1);

        var map = SiteGenerator.SelectMemlogUpdatedByFamily(
            new (string Dir, DateOnly Updated)[] { (Dir: "", Updated: rootDate) }, families);

        Assert.True(map.TryGetValue("prd", out var lowercase));
        Assert.Equal(rootDate, lowercase);
    }

    [Fact]
    public void RootAndScopedMemlogsCoexist_RootExcludedFromFallback()
    {
        // PRD sits under planning-artifacts/prds; a scoped memlog there wins over root. Epics has no scoped
        // ancestor memlog, and once ANY scoped memlog exists in the tree, root no longer blanket-applies.
        var families = new[]
        {
            Family("PRD", "planning-artifacts/prds/prd-x/prd.md"),
            Family("Epics", "planning-artifacts/epics.md"),
        };
        var rootDate = new DateOnly(2026, 6, 1);
        var scopedDate = new DateOnly(2026, 7, 1);

        var map = SiteGenerator.SelectMemlogUpdatedByFamily(
            new (string Dir, DateOnly Updated)[] { (Dir: "", Updated: rootDate), (Dir: "planning-artifacts/prds", Updated: scopedDate) },
            families);

        Assert.Equal(scopedDate, map["PRD"]);
        Assert.False(map.ContainsKey("Epics"));
    }

    [Fact]
    public void NonAncestorSubstringDir_DoesNotMatch()
    {
        // "planning-artifacts/prds" must not match "planning-artifacts/prdsomethingelse/..." — the trailing
        // "/" in the StartsWith check guards true ancestor containment, not a raw string-prefix match.
        var families = new[] { Family("PRD", "planning-artifacts/prdsomethingelse/x.md") };

        var map = SiteGenerator.SelectMemlogUpdatedByFamily(
            new (string Dir, DateOnly Updated)[] { (Dir: "planning-artifacts/prds", Updated: new DateOnly(2026, 7, 1)) }, families);

        Assert.Empty(map);
    }

    [Fact]
    public void EqualAncestorDepth_StableOrderDecidesTie()
    {
        // Two candidate memlogs at the SAME ancestor dir (the only way two prefixes of one path can tie on
        // length — distinct-content prefixes of equal length can't both be true ancestors of one path) —
        // current behavior is a stable sort on dir length only, so whichever candidate is enumerated first
        // wins. This test pins that documented (not "fixed") behavior rather than asserting either date is
        // objectively correct.
        var families = new[] { Family("PRD", "planning-artifacts/prds/prd-x/prd.md") };
        (string Dir, DateOnly Updated) first = ("planning-artifacts/prds", new DateOnly(2026, 6, 1));
        (string Dir, DateOnly Updated) second = ("planning-artifacts/prds", new DateOnly(2026, 7, 1)); // same dir, different date

        var mapFirstWins = SiteGenerator.SelectMemlogUpdatedByFamily(new[] { first, second }, families);
        var mapSecondWins = SiteGenerator.SelectMemlogUpdatedByFamily(new[] { second, first }, families);

        Assert.Equal(first.Item2, mapFirstWins["PRD"]);
        Assert.Equal(second.Item2, mapSecondWins["PRD"]);
    }
}
