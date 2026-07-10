using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One canonical planning/workflow artifact family — a card in the dashboard coverage panel. Carries
/// whether the family was discovered in the source tree (<see cref="Present"/>), the best-effort freshness
/// signal (<see cref="LastModified"/>, the source file's last-write-time), the matched source path, an
/// optional secondary <see cref="MemlogUpdated"/> enrichment (the family's memlog <c>updated:</c> date), and a
/// one-line <see cref="Description"/> of what the artifact is. <see cref="Href"/> (the page a present family
/// links to) and <see cref="CreateCommand"/> (the slash command that creates a missing family) are resolved by
/// the generator — the pure <see cref="ArtifactCoverage.Build"/> leaves them null since they depend on page
/// routing and the detected module. Freshness/staleness is a DIFFERENT axis from the lifecycle status tokens —
/// a "present, stale PRD" is not a lifecycle stage — so the panel styles this with neutral palette tones, never
/// <c>--status-*</c>. [Story 3.3]</summary>
public sealed record ArtifactFamily(
    string Label,
    string ConceptIconKey,
    string Description,
    bool Present,
    DateOnly? LastModified,
    string? SourcePath,
    DateOnly? MemlogUpdated,
    string? Href = null,
    string? CreateCommand = null)
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

    /// <summary>Pairs a canonical family's label + reused <see cref="Icons.ForConcept"/> glyph key + one-line
    /// description with the predicate that recognizes its source file (matched by filename anywhere in the
    /// tree). <see cref="StepKey"/> is the workflow-step name (skill minus its module prefix) the generator
    /// feeds to <see cref="CommandCatalog.Command"/> to surface a create command on a missing family; null
    /// when no create workflow applies (the missing card then shows guidance text alone).</summary>
    private sealed record FamilySpec(string Label, string ConceptIconKey, string Description, string? StepKey, Func<string, bool> Matches);

    // Canonical filenames not already centralized in ModuleContext.WellKnownDocs. The five that ARE centralized
    // (PRD, Brief, Architecture spine, UX design + experience) are keyed off those constants below rather than
    // re-hard-coded, following the project's one-classifier/one-seam discipline.
    private const string SpecKernelFile = "SPEC.md";
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
        new FamilySpec("PRD", "PRD", "What you're building and why — the product requirements.", "prd",
            NameIs(ModuleContext.WellKnownDocs.Prd)),
        new FamilySpec("Product Brief", "Product Brief", "The early product concept and framing.", "product-brief",
            NameIs(ModuleContext.WellKnownDocs.Brief)),
        new FamilySpec("Architecture", "Architecture", "The architecture spine — the invariants features and stories stay consistent with.", "architecture",
            NameIs(ModuleContext.WellKnownDocs.ArchitectureSpine)),
        // UX is present if EITHER the design system OR the experience/flows doc exists.
        new FamilySpec("UX", "UX Design", "The UX design system and experience flows.", "ux",
            p => NameMatches(p, ModuleContext.WellKnownDocs.UxDesign) || NameMatches(p, ModuleContext.WellKnownDocs.UxExperience)),
        // Spec kernel: SPEC.md living under a specs/ path — disjoint from Story 2.1's implementation-artifacts/spec-*.md quick-dev files.
        // No standard "create spec" workflow step in the base module, so a missing card shows guidance text alone.
        new FamilySpec("Spec Kernel", "Spec", "The canonical spec contract downstream work is built from.", "spec",
            p => NameMatches(p, SpecKernelFile) && HasSegment(p, "specs")),
        new FamilySpec("Epics", "Epics", "The epic and story breakdown of the work.", "create-epics-and-stories",
            NameIs(BmadArtifactAdapter.EpicsFileName)),
        // Stories: at least one epic/story artifact file (implementation-artifacts/<n>-<n>-*.md).
        new FamilySpec("Stories", "Implementation Artifacts", "Per-story implementation plans.", "create-story",
            IsStoryArtifact),
        // Requirements (FR/NFR) are produced by the PRD workflow, so the create command points there.
        new FamilySpec("Requirements", "Requirements", "The FR/NFR catalog behind the plan.", "prd",
            p => NameMatches(p, RequirementsFile) || NameMatches(p, RequirementsCatalogFile)),
    };

    /// <summary>Maps a family <see cref="ArtifactFamily.Label"/> to the workflow step key that creates it —
    /// the single source the generator feeds to <see cref="CommandCatalog.Command"/> to resolve a missing
    /// family's create command against the detected module. A label absent here (or a step the module doesn't
    /// expose) yields no command, so the missing card degrades to guidance text. [Story 3.3 actionable panel]</summary>
    public static readonly IReadOnlyDictionary<string, string> CreateStepKeys =
        Specs.Where(s => s.StepKey is not null)
            .ToDictionary(s => s.Label, s => s.StepKey!, StringComparer.Ordinal);

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
            var match = SelectCanonicalMatch(normalized, spec, lastModifiedByPath);

            DateOnly? lastModified = null;
            if (match is not null && lastModifiedByPath.TryGetValue(match, out var m))
            {
                // Clamp a future-dated mtime (clock/timezone skew) to the generation date so freshness never
                // reads as "edited in the future" — the same future-skew guard the commit heatmap applies.
                lastModified = m > today ? today : m;
            }

            var memlog = memlogUpdatedByFamilyLabel.TryGetValue(spec.Label, out var ml) ? ml : (DateOnly?)null;

            families.Add(new ArtifactFamily(
                spec.Label, spec.ConceptIconKey, spec.Description, match is not null, lastModified, match, memlog));
        }

        return new ArtifactCoverage { Families = families };
    }

    /// <summary>Every source-relative path that matches ANY canonical family, deduplicated — the set
    /// <see cref="SiteGenerator.BuildArtifactCoverage"/> stats in its second (mtime) gather pass. Broader than
    /// just the winning match per family, so a family with two candidates (e.g. both DESIGN.md and
    /// EXPERIENCE.md present for UX) has both mtimes available when <see cref="Build"/> picks the canonical
    /// one. [Story 3.3 review]</summary>
    public static IReadOnlyList<string> AllCandidatePaths(IReadOnlyList<string> sourceRelativePaths)
    {
        var normalized = sourceRelativePaths.Select(PathUtil.NormalizeSlashes);
        return normalized.Where(p => Specs.Any(s => s.Matches(p))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Picks the single canonical match for a family among every path satisfying its predicate: the
    /// most-recently-modified candidate wins (the freshest file is the one worth surfacing), with ties —
    /// including "no mtime known yet", true during the discovery pass that runs before mtimes are gathered —
    /// broken by ordinal path order so the choice is stable across runs and filesystems regardless of
    /// directory-enumeration order. [Story 3.3 review: replaces the previous "first match" which depended on
    /// unsorted input order and, for OR-predicates like UX's DESIGN.md-or-EXPERIENCE.md, silently ignored the
    /// second candidate's freshness entirely.]</summary>
    private static string? SelectCanonicalMatch(
        IReadOnlyList<string> normalized, FamilySpec spec, IReadOnlyDictionary<string, DateOnly> lastModifiedByPath)
    {
        var candidates = normalized.Where(spec.Matches).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (candidates.Count <= 1) return candidates.Count == 0 ? null : candidates[0];

        return candidates
            .OrderByDescending(p => lastModifiedByPath.TryGetValue(p, out var d) ? d : (DateOnly?)null)
            .ThenBy(p => p, StringComparer.Ordinal)
            .First();
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
    private static readonly Regex StoryFileName = new(@"^\d+-\d+-.+\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsStoryArtifact(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        if (slash < 0) return false;
        if (!StoryFileName.IsMatch(normalizedPath[(slash + 1)..])) return false;

        // Require an implementation-artifacts/ ancestor (any depth — the same shared adapter convention
        // SiteGenerator.IsEpicsRelated and BuildArtifactMap classify by, so coverage can never claim stories
        // the epics pages don't render) while a like-named file elsewhere in the tree isn't miscounted.
        return BmadArtifactAdapter.IsUnderImplementationArtifacts(normalizedPath);
    }
}
