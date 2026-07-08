using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One canonical planning/workflow artifact family — a row in the dashboard coverage panel. Carries
/// whether the family was discovered in the source tree (<see cref="Present"/>), the best-effort freshness
/// signal (<see cref="LastModified"/>, the source file's last-write-time), the matched source path, and an
/// optional secondary <see cref="MemlogUpdated"/> enrichment (the family's memlog <c>updated:</c> date).
/// Freshness/staleness is a DIFFERENT axis from the lifecycle status tokens — a "present, stale PRD" is not a
/// lifecycle stage — so the panel styles this with neutral palette tones, never <c>--status-*</c>. [Story 3.3]</summary>
public sealed record ArtifactFamily(
    string Label,
    string ConceptIconKey,
    bool Present,
    DateOnly? LastModified,
    string? SourcePath,
    DateOnly? MemlogUpdated)
{
    /// <summary>Staleness is derived, never stored: a present family whose source file was last written more
    /// than <see cref="ArtifactCoverage.StalenessThresholdDays"/> days before <paramref name="today"/>. A
    /// missing family — or a present one with an unknown last-modified date — is never stale (unknown ≠ old).</summary>
    public bool IsStale(DateOnly today) =>
        Present && LastModified is { } d && today.DayNumber - d.DayNumber > ArtifactCoverage.StalenessThresholdDays;
}

/// <summary>A pure, source-artifact-derived view of which canonical planning/workflow families a repo carries
/// and how fresh each is. Mirrors the shape of <see cref="WorkInventory"/> (a pure <see cref="Build"/> over
/// already-gathered inputs, an <see cref="Empty"/> singleton, an <see cref="IsEmpty"/> flag callers use to omit
/// the panel). Coverage (present/missing) and freshness (last-write-time) come 100% from source artifacts;
/// the memlog <c>updated:</c> date is consumed only as optional secondary enrichment, so a repo with no
/// memlogs produces an identical primary coverage picture (PRD FR-11: source-derived insights stay primary).
/// Never throws — the generator degrades any failure to <see cref="Empty"/> so the panel omits and generation
/// still succeeds (AD-4 / NFR2). [Story 3.3]</summary>
public sealed class ArtifactCoverage
{
    /// <summary>Days after a present family's last edit before it is flagged stale. A sensible default; the
    /// settings surface may later expose it (settings-and-signals.md reserves insight controls) — no settings
    /// plumbing is built here, that parity work is out of scope for this story. [Story 3.3]</summary>
    public const int StalenessThresholdDays = 30;

    public required IReadOnlyList<ArtifactFamily> Families { get; init; }

    /// <summary>True when no canonical family was discovered, so the caller omits the whole panel (Story 1.1
    /// graceful omission) rather than render an all-missing board on a repo whose family set we don't recognize.</summary>
    public bool IsEmpty => !Families.Any(f => f.Present);

    /// <summary>Number of canonical families discovered in the source tree — the "N" in the panel headline.</summary>
    public int PresentCount => Families.Count(f => f.Present);

    /// <summary>Number of canonical families NOT found — the key "missing families" AC #1 asks the panel to name.</summary>
    public int MissingCount => Families.Count(f => !f.Present);

    /// <summary>How many present families are stale as of <paramref name="today"/> (derived, never stored).</summary>
    public int StaleCount(DateOnly today) => Families.Count(f => f.IsStale(today));

    public static readonly ArtifactCoverage Empty = new() { Families = Array.Empty<ArtifactFamily>() };

    /// <summary>Pairs a canonical family's label + reused <see cref="Icons.ForConcept"/> glyph key with the
    /// predicate that recognizes its source file (matched by filename anywhere in the tree).</summary>
    private sealed record FamilySpec(string Label, string ConceptIconKey, Func<string, bool> Matches);

    // Canonical filenames not already centralized in ModuleContext.WellKnownDocs. The five that ARE centralized
    // (PRD, Brief, Architecture spine, UX design + experience) are keyed off those constants below rather than
    // re-hard-coded, following the project's one-classifier/one-seam discipline.
    private const string SpecKernelFile = "SPEC.md";
    private const string EpicsFile = "epics.md";
    private const string RequirementsFile = "requirements.md";
    private const string RequirementsCatalogFile = "requirements-catalog.md";

    /// <summary>The V1 "Core + Orchestration" canonical family set (PRD FR-11). THIS list is the coverage seam
    /// Epic 4 generalizes — a future framework adapter swaps this family set, not the panel or the builder.
    /// Filenames key off <see cref="ModuleContext.WellKnownDocs"/> where those constants exist (single source
    /// of truth) and each family is matched by filename ANYWHERE in the source tree, because folder layout
    /// varies (same rationale as ModuleContext's well-known-doc matching). Glyph keys reuse the exact
    /// <see cref="Icons.ForConcept"/> vocabulary so the panel adds no new icon strings.</summary>
    private static readonly IReadOnlyList<FamilySpec> Specs = new[]
    {
        new FamilySpec("PRD", "PRD", NameIs(ModuleContext.WellKnownDocs.Prd)),
        new FamilySpec("Product Brief", "Product Brief", NameIs(ModuleContext.WellKnownDocs.Brief)),
        new FamilySpec("Architecture", "Architecture", NameIs(ModuleContext.WellKnownDocs.ArchitectureSpine)),
        // UX is present if EITHER the design system OR the experience/flows doc exists.
        new FamilySpec("UX", "UX Design",
            p => NameMatches(p, ModuleContext.WellKnownDocs.UxDesign) || NameMatches(p, ModuleContext.WellKnownDocs.UxExperience)),
        // Spec kernel: SPEC.md living under a specs/ path — disjoint from Story 2.1's implementation-artifacts/spec-*.md quick-dev files.
        new FamilySpec("Spec Kernel", "Spec", p => NameMatches(p, SpecKernelFile) && HasSegment(p, "specs")),
        new FamilySpec("Epics", "Epics", NameIs(EpicsFile)),
        // Stories: at least one epic/story artifact file (implementation-artifacts/<n>-<n>-*.md).
        new FamilySpec("Stories", "Implementation Artifacts", IsStoryArtifact),
        new FamilySpec("Requirements", "Requirements",
            p => NameMatches(p, RequirementsFile) || NameMatches(p, RequirementsCatalogFile)),
    };

    /// <summary>Builds the coverage view over already-resolved inputs — NO disk access here, so every
    /// coverage/freshness/staleness rule is unit-testable without a repo (the same IO-in-the-caller split as
    /// <see cref="ProgressCalculator"/> and <see cref="WorkInventory"/>). Path keys in
    /// <paramref name="lastModifiedByPath"/> are normalized-slash source-relative paths;
    /// <paramref name="memlogUpdatedByFamilyLabel"/> is keyed by family <see cref="ArtifactFamily.Label"/> and
    /// is strictly additive — an empty map leaves every primary Present/LastModified value unchanged (AC #2).</summary>
    /// <param name="sourceRelativePaths">Every source-relative markdown path discovered in the tree.</param>
    /// <param name="lastModifiedByPath">Source-file last-write dates, keyed by normalized-slash source path.</param>
    /// <param name="memlogUpdatedByFamilyLabel">Optional secondary memlog <c>updated:</c> dates, keyed by family label.</param>
    /// <param name="today">The generation date; a future-dated mtime (clock/timezone skew) is clamped to it.</param>
    public static ArtifactCoverage Build(
        IReadOnlyList<string> sourceRelativePaths,
        IReadOnlyDictionary<string, DateOnly> lastModifiedByPath,
        IReadOnlyDictionary<string, DateOnly> memlogUpdatedByFamilyLabel,
        DateOnly today)
    {
        var normalized = sourceRelativePaths.Select(PathUtil.NormalizeSlashes).ToList();

        var families = new List<ArtifactFamily>(Specs.Count);
        foreach (var spec in Specs)
        {
            // Deterministic first-match (like WorkInventory's "first match wins") so a repeated family file
            // resolves to a single stable row rather than last-write-wins.
            var match = normalized.FirstOrDefault(spec.Matches);

            DateOnly? lastModified = null;
            if (match is not null && lastModifiedByPath.TryGetValue(match, out var m))
            {
                // Clamp a future-dated mtime (clock/timezone skew) to the generation date so freshness never
                // reads as "edited in the future" — the same future-skew guard the commit heatmap applies.
                lastModified = m > today ? today : m;
            }

            var memlog = memlogUpdatedByFamilyLabel.TryGetValue(spec.Label, out var ml) ? ml : (DateOnly?)null;

            families.Add(new ArtifactFamily(spec.Label, spec.ConceptIconKey, match is not null, lastModified, match, memlog));
        }

        return new ArtifactCoverage { Families = families };
    }

    private static Func<string, bool> NameIs(string fileName) => p => NameMatches(p, fileName);

    private static bool NameMatches(string normalizedPath, string fileName)
    {
        var slash = normalizedPath.LastIndexOf('/');
        var name = slash >= 0 ? normalizedPath[(slash + 1)..] : normalizedPath;
        return string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSegment(string normalizedPath, string segment) =>
        normalizedPath.Split('/').Any(s => string.Equals(s, segment, StringComparison.OrdinalIgnoreCase));

    // A story/epic artifact filename: leading <epic>-<story>- (mirrors SiteGenerator.ArtifactFilenamePattern),
    // so epic-N-retrospective.md / deferred-work.md / spec-*.md are correctly excluded.
    private static readonly Regex StoryFileName = new(@"^\d+-\d+-.*\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsStoryArtifact(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        if (slash < 0) return false;
        if (!StoryFileName.IsMatch(normalizedPath[(slash + 1)..])) return false;

        // Require the immediate parent folder to be implementation-artifacts/ (same scoping as
        // SiteGenerator.IsEpicsRelated) so a like-named file elsewhere in the tree isn't miscounted.
        var parent = normalizedPath[..slash];
        var parentSlash = parent.LastIndexOf('/');
        var parentName = parentSlash >= 0 ? parent[(parentSlash + 1)..] : parent;
        return string.Equals(parentName, "implementation-artifacts", StringComparison.OrdinalIgnoreCase);
    }
}
