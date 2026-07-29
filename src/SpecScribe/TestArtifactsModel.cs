using System.Globalization;
using System.Text.Json;

namespace SpecScribe;

/// <summary>How deeply SpecScribe interprets one discovered module artifact — the PRD's
/// <c>rendered</c> / <c>summarized</c> / <c>unsupported</c> coverage-tier vocabulary, made a real type.
///
/// <para>That vocabulary was written down in the PRD (§"Framework Coverage") and left as an OPEN QUESTION in both
/// the PRD and <c>SPEC.md</c> — <em>"How should coverage tiers be communicated so users understand interpretation
/// boundaries?"</em> — and existed nowhere in <c>src/</c>. Story 18.5 answers it: a closed three-value enum with one
/// <see cref="CoverageTiers.Word"/> and one <see cref="CoverageTiers.Description"/> per value, assigned per
/// artifact, shown as a badge that always carries its WORD (never colour alone, UX-DR17).</para>
///
/// <para>The three are about INTERPRETATION DEPTH, not about whether a file was found — everything in the model was
/// found by definition. They are deliberately ordered best-understood last-understood so a caller can compare
/// them.</para> [Story 18.5]</summary>
public enum CoverageTier
{
    /// <summary>A full page exists for the artifact and SpecScribe interprets nothing beyond rendering its prose.
    /// The honest majority case for TEA: <c>{output_folder}</c> IS SpecScribe's source root, so TEA's markdown is
    /// already discovered by the generic <c>*.md</c> pass and already has a page.</summary>
    Rendered,

    /// <summary>SpecScribe additionally extracts a STRUCTURED headline from the artifact — the gate verdict, the
    /// coverage percentages, the per-priority breakdown — onto the Test Artifacts page and the dashboard panel,
    /// while the file itself is not fully modelled. Both TEA JSON files are summarized-only: they have no prose
    /// page at all, because the source scan is <c>*.md</c>.</summary>
    Summarized,

    /// <summary>Discovered and named; nothing interpreted. Not a failure and not an error — an honest statement of
    /// the interpretation boundary, which is exactly what the PRD's open question asked for.</summary>
    Unsupported,
}

/// <summary>The one place the coverage-tier vocabulary turns into words a human reads — the same
/// one-classifier discipline <see cref="StatusStyles"/> holds for lifecycle stages. No surface may spell a tier
/// itself. [Story 18.5]</summary>
public static class CoverageTiers
{
    /// <summary>Render order for the tier legend and the panel's counts — best-understood first.</summary>
    public static readonly IReadOnlyList<CoverageTier> Order =
        new[] { CoverageTier.Summarized, CoverageTier.Rendered, CoverageTier.Unsupported };

    public static string Word(CoverageTier tier) => tier switch
    {
        CoverageTier.Rendered => "Rendered",
        CoverageTier.Summarized => "Summarized",
        _ => "Unsupported",
    };

    /// <summary>The one-line statement of what the tier PROMISES — the interpretation boundary in words.</summary>
    public static string Description(CoverageTier tier) => tier switch
    {
        CoverageTier.Rendered =>
            "The document is rendered in full on its own page; SpecScribe reads its prose but interprets none of its structure.",
        CoverageTier.Summarized =>
            // "…shows that here" until the Epic 18 documentation pass, when the About-SDD page began rendering
            // these same descriptions as the coverage-tier legend. "Here" meant the Test Artifacts page and
            // silently became wrong on the second surface — the hazard of a shared vocabulary string that names
            // its own location. Now location-neutral, so any surface may render it.
            "SpecScribe extracts a structured headline from this artifact — its verdict and coverage figures — and surfaces that alongside it; the file itself is not fully modelled.",
        _ =>
            "Discovered and named, but not interpreted. SpecScribe does not model this artifact family, so nothing is claimed about its contents.",
    };

    /// <summary>Canonical stage token for the list-row LEFT ACCENT bar. Never the sole signal — the row's badge
    /// always carries <see cref="Word"/> (UX-DR17). Reuses the existing six-token vocabulary rather than minting a
    /// new <c>--status-*</c> family ([[specscribe-status-token-system]]).</summary>
    public static string AccentToken(CoverageTier tier) => tier switch
    {
        CoverageTier.Summarized => "done",
        CoverageTier.Rendered => "ready",
        _ => "deferred",
    };
}

/// <summary>One discovered module artifact, with the tier SpecScribe assigns it. [Story 18.5]</summary>
/// <param name="SourceRelative">Forward-slashed path relative to the source root.</param>
/// <param name="Title">Human label for the row.</param>
/// <param name="ProducingSkill">The upstream skill that writes this file (e.g. <c>bmad-testarch-trace</c>), or
/// null when the filename matches no pinned output.</param>
/// <param name="Tier">How deeply SpecScribe interprets it.</param>
/// <param name="OutputRelativePath">The generated page for this artifact when one exists (the generic
/// <c>*.md</c> pass writes it), else null — the two JSON files never have one.</param>
/// <param name="Headline">The extracted structured signal for a <see cref="CoverageTier.Summarized"/> artifact.</param>
public sealed record TestArtifactEntry(
    string SourceRelative,
    string Title,
    string? ProducingSkill,
    CoverageTier Tier,
    string? OutputRelativePath = null,
    string? Headline = null);

/// <summary>The slim gate signal from <c>gate-decision.json</c> — the single most decision-relevant thing TEA
/// produces, and a file SpecScribe's <c>*.md</c>-only source scan structurally cannot see (ADR 0020).
/// <para>⚠️ <see cref="TargetId"/> and <see cref="TargetLabel"/> are literally <c>null</c> in upstream's own schema
/// example (<c>coverageMatrix.trace_target || { type, id: null, label: null }</c>). NOTHING may be keyed on
/// them.</para> [Story 18.5]</summary>
public sealed record TestGateDecision(
    string Status,
    string? Rationale,
    int? CriticalOpen,
    string? P0Status,
    string? P1Status,
    string? OverallStatus,
    string? TargetType,
    string? TargetId,
    string? TargetLabel)
{
    /// <summary>The four verdicts upstream's <c>gateDecisionSlim</c> guard admits. Anything else is a word we
    /// still SHOW (never invented, never dropped) but never style as a known verdict.</summary>
    public static readonly IReadOnlyList<string> KnownStatuses = new[] { "PASS", "CONCERNS", "FAIL", "WAIVED" };
}

/// <summary>Per-priority coverage (P0–P3) — from the JSON's <c>coverage.priority_breakdown</c> or the markdown's
/// own Coverage Summary table.</summary>
public sealed record TeaPriorityCoverage(string Priority, int Total, int Covered, double? Percent);

/// <summary>Per-test-level counts — the JSON's <c>coverage.by_level</c> (e2e / api / component / unit).</summary>
public sealed record TeaLevelCoverage(string Level, int Tests);

/// <summary>The rich machine summary from <c>e2e-trace-summary.json</c>.
/// <para><see cref="GateStatus"/> is nullable BY CONTRACT: upstream appends <c>gate_status</c> and
/// <c>gate_criteria</c> only inside <c>if (gateEligible)</c>, so an inventory-only or waived run writes the file
/// without them. A reader that required them would report every such run as malformed.</para> [Story 18.5]</summary>
public sealed record TestTraceSummary
{
    /// <summary>Which oracle the coverage inventory was built from — <c>acceptance_criteria</c>,
    /// <c>synthetic_requirements</c>, <c>openapi_endpoints</c>, or <c>user_journeys</c>. The FIRST of the two
    /// signals that decide whether a requirement join is admissible at all.
    /// <para>Task 1 correction: Story 18.5's pinned table named only three values; <c>bmad-testarch-trace</c>'s
    /// <c>workflow.yaml</c> declares FOUR in its <c>coverage_basis</c> enum. The fourth,
    /// <c>synthetic_requirements</c>, is non-joinable — upstream's own <c>syntheticOracle</c> test includes
    /// it.</para></summary>
    public string? InventoryBasis { get; init; }

    /// <summary>TEA's own confidence in the oracle (<c>high</c>/<c>medium</c>/<c>low</c>). The second join gate.</summary>
    public string? Confidence { get; init; }

    /// <summary>True when the oracle was INFERRED from source rather than read from a formal artifact. Never
    /// joinable, whatever the basis says.</summary>
    public bool SyntheticOracle { get; init; }

    public string? CollectionStatus { get; init; }

    public int? CoveredCount { get; init; }
    public int? TotalCount { get; init; }
    public double? OverallPercent { get; init; }

    public IReadOnlyList<TeaPriorityCoverage> PriorityBreakdown { get; init; } = Array.Empty<TeaPriorityCoverage>();
    public IReadOnlyList<TeaLevelCoverage> ByLevel { get; init; } = Array.Empty<TeaLevelCoverage>();

    public int? TestFiles { get; init; }
    public int? TestCases { get; init; }

    public int? CriticalOpen { get; init; }

    /// <summary>Present only on a gate-eligible run — see the type summary.</summary>
    public string? GateStatus { get; init; }

    /// <summary>The <c>gate_criteria</c> sub-object's <c>p0_status</c>/<c>p1_status</c>/<c>overall_status</c> —
    /// present only alongside <see cref="GateStatus"/> on a gate-eligible run. Read here so a repo with a trace
    /// summary and no separate <c>gate-decision.json</c> still shows the P0/P1 breakdown rather than the bare
    /// gate word alone; the data was already parsed into memory either way. [Review][Patch]</summary>
    public string? P0Status { get; init; }

    public string? P1Status { get; init; }

    public string? OverallStatus { get; init; }
}

/// <summary>One row of TEA's Detailed Mapping: an oracle item, how well it is covered, and by how many tests.
/// <para>The upstream grammar is an <c>#### {CRITERION_ID}: {DESCRIPTION} ({PRIORITY})</c> heading followed by
/// <c>- **Coverage:**</c> and <c>- **Tests:**</c> bullets — NOT a table. (Story 18.5's own pinned table described
/// it as a five-column table; Task 1's re-fetch of <c>trace-template.md</c> corrected that.)</para></summary>
public sealed record TeaCriterionCoverage(
    string CriterionId,
    string Description,
    string CoverageStatus,
    string? Priority,
    int TestCount);

/// <summary>Everything <c>traceability-matrix.md</c> yields on its own — usable even when neither JSON was
/// written, because the markdown's frontmatter carries the same oracle signals.</summary>
public sealed record TeaMatrix
{
    public static readonly TeaMatrix Empty = new();

    public string? CoverageBasis { get; init; }
    public string? OracleConfidence { get; init; }
    public string? OracleResolutionMode { get; init; }
    public string? GateStatus { get; init; }
    public IReadOnlyList<TeaPriorityCoverage> PriorityBreakdown { get; init; } = Array.Empty<TeaPriorityCoverage>();
    public IReadOnlyList<TeaCriterionCoverage> Criteria { get; init; } = Array.Empty<TeaCriterionCoverage>();
}

/// <summary>Whether TEA's coverage may be projected onto SpecScribe's own requirement × epic traceability
/// surface at all, and the plain-English reason when it may not. Never a bool alone: an inadmissible join has to
/// SAY why, or the absence reads as a bug (NFR8). [Story 18.5 D2]</summary>
public sealed record TeaJoinVerdict(bool Admissible, string Reason);

/// <summary>One admissible join: a TEA oracle item that resolved to an id SpecScribe actually holds.</summary>
/// <param name="TargetId">The resolved <c>RequirementsModel.ById</c> id or <c>EpicsModel</c> story id.</param>
/// <param name="Criterion">The TEA row it came from — kept whole so the surface can attribute it.</param>
public sealed record TeaJoinRow(string TargetId, TeaCriterionCoverage Criterion);

/// <summary>The result of attempting the D2 join: the rows that resolved, how many did not, and the verdict that
/// governed the attempt. An inadmissible verdict yields ZERO rows regardless of how many ids would have resolved
/// — the basis is judged before any id is looked at, which is what stops a synthetic-journey run whose ids happen
/// to look like FR ids from fabricating coverage. [Story 18.5 D2]</summary>
public sealed record TeaJoin(bool Admissible, string Reason, IReadOnlyList<TeaJoinRow> Rows, int UnresolvedCount)
{
    public static readonly TeaJoin None = new(false, "No traceability matrix was found.", Array.Empty<TeaJoinRow>(), 0);
}

/// <summary>Every Test Architect artifact discovered under the source root, with its coverage tier, plus the
/// structured signals extracted from the two JSON files and the traceability matrix.
///
/// <para>A pure <c>Build</c>-shaped model over already-gathered inputs with an <see cref="Empty"/> singleton and an
/// <see cref="IsEmpty"/> flag callers use to omit the whole surface — the same shape
/// <see cref="ArtifactCoverage"/> / <see cref="WorkInventory"/> / <see cref="IdeasModel"/> use. Never throws by its
/// producer's contract: any failure degrades to <see cref="Empty"/>, so the page, the nav entry and the dashboard
/// panel all omit and baseline generation still succeeds (AD-4 / NFR2).</para>
///
/// <para><b>Module identity is a CODE STRING, never an enum case.</b> TEA stays
/// <see cref="BmadModule.Unmodeled"/> in a TEA-only repo and is simply present in a BMM+TEA one; ADR 0015
/// Decisions 1/2 are open-world on purpose because BMad Builder mints arbitrary codes. <see cref="ModuleLabel"/>
/// comes from the parsed <c>module-help.csv</c> via <see cref="CommandCatalog.ModuleLabel"/> — note that the
/// installed CSV says "Test Architecture Enterprise" while <c>module.yaml</c> says "Test Architect", so NEITHER
/// string may be hard-coded.</para> [Story 18.5]</summary>
public sealed record TestArtifactsModel
{
    public static readonly TestArtifactsModel Empty = new();

    /// <summary>The module's install-directory code (<c>tea</c>). Empty on <see cref="Empty"/>.</summary>
    public string ModuleCode { get; init; } = string.Empty;

    /// <summary>The module's own declared label, read from its CSV. Empty when unavailable — the surface then
    /// names the module by nothing at all rather than inventing a name (ADR 0015 Decision 2b).</summary>
    public string ModuleLabel { get; init; } = string.Empty;

    public IReadOnlyList<TestArtifactEntry> Artifacts { get; init; } = Array.Empty<TestArtifactEntry>();

    public TestGateDecision? Gate { get; init; }
    public TestTraceSummary? Trace { get; init; }
    public TeaMatrix Matrix { get; init; } = TeaMatrix.Empty;

    /// <summary>The D2 join outcome. Populated by the generator, which is the only layer that holds both the
    /// requirements model and the epics model.</summary>
    public TeaJoin Join { get; init; } = TeaJoin.None;

    /// <summary>True when nothing was discovered — no page, no nav entry, no dashboard panel, no notice
    /// (NFR8: absent, never an empty surface). A BMM-only repo is ALWAYS empty here.</summary>
    public bool IsEmpty => Artifacts.Count == 0;

    public int CountIn(CoverageTier tier) => Artifacts.Count(a => a.Tier == tier);

    /// <summary>The gate verdict to SHOW, preferring the machine-readable slim file, then the rich summary, then
    /// the word in the matrix markdown's own <c>### GATE DECISION:</c> heading. Null when no run was gate-eligible
    /// — the panel then shows coverage without a verdict rather than inventing one.</summary>
    public string? GateWord => Gate?.Status ?? Trace?.GateStatus ?? Matrix.GateStatus;

    /// <summary>Tier → render-rank, computed once rather than re-allocating a list and linear-scanning it with
    /// <c>IndexOf</c> per artifact inside the <see cref="Ordered"/> comparator. [Review][Patch]</summary>
    private static readonly IReadOnlyDictionary<CoverageTier, int> TierRank =
        CoverageTiers.Order.Select((tier, rank) => (tier, rank)).ToDictionary(x => x.tier, x => x.rank);

    /// <summary>Artifacts in render order: tier group (Summarized → Rendered → Unsupported), then path, so a
    /// from-scratch regeneration is byte-identical.</summary>
    public IReadOnlyList<TestArtifactEntry> Ordered => Artifacts
        .OrderBy(a => TierRank[a.Tier])
        .ThenBy(a => a.SourceRelative, StringComparer.Ordinal)
        .ToList();
}

/// <summary>Outcome of reading one TEA JSON file. Maps onto the closed five-value
/// <see cref="AdapterDiagnosticCategory"/> at the call site — <see cref="UnsupportedSchema"/> →
/// <see cref="AdapterDiagnosticCategory.Skipped"/>, <see cref="Malformed"/> →
/// <see cref="AdapterDiagnosticCategory.Malformed"/>. No sixth diagnostic category is invented.</summary>
public enum TeaJsonOutcome
{
    Parsed,

    /// <summary>The file's <c>schema_version</c> major is one this build does not know. Gated BEFORE any field is
    /// read, so an incompatible future shape is skipped rather than silently misparsed.</summary>
    UnsupportedSchema,

    Malformed,
}

/// <summary>The pure derivation rules for Test Architect coverage — tier assignment, the two JSON schema gates and
/// readers, the <c>traceability-matrix.md</c> grammar, and the join-admissibility rule. Split from
/// <see cref="TestArtifactDiscovery"/>'s disk walk the same way <see cref="IdeaDerivation"/> /
/// <see cref="ArtifactCoverage.Build"/> / <c>ProgressCalculator</c> are split from their callers' IO, so every rule
/// here is unit-testable without a repo on disk.
///
/// <para><b>Everything here is pinned to upstream, not to doc-site prose.</b> Story 18.1 built its TEA facts from
/// the documentation site and got the filenames wrong (<c>traceability-matrix.csv</c>, <c>nfr-report.md</c>); a
/// parser built from those would have found nothing. Every filename and every JSON key below was read from the
/// producing skill's own <c>workflow.yaml</c> and <c>steps-c/step-05-gate-decision.md</c> — see
/// <c>TestArtifactDerivationTests</c> for the per-file commit SHAs.</para>
///
/// <para><b>Never throws.</b> Each reader returns an outcome or an empty model; the callers turn that into a
/// categorized non-fatal diagnostic (AD-4 / NFR2).</para> [Story 18.5]</summary>
public static class TestArtifactDerivation
{
    /// <summary>Test Architect's module code — its <c>_bmad/tea/</c> install-directory name. The ONE home for this
    /// literal. Coverage keys on this string, never on a new <see cref="BmadModule"/> case (ADR 0015 Decisions
    /// 1/2 are open-world because BMad Builder mints arbitrary codes).</summary>
    public const string ModuleCode = "tea";

    /// <summary>The default <c>test_artifacts</c> directory, relative to the source root.
    /// <c>src/module.yaml</c> declares <c>default: "{output_folder}/test-artifacts"</c> and
    /// <c>{output_folder}</c> resolves to <see cref="ForgeOptions.SourceDirName"/> — so in a default install this
    /// folder is INSIDE the scanned source tree and TEA's markdown already renders. Also the
    /// <see cref="DashboardViewBuilder"/> folder-group key, so <c>test-artifacts/</c> stops reading as an
    /// unrecognized top-level folder.</summary>
    public const string ArtifactsDirName = "test-artifacts";

    public const string TraceMatrixFileName = "traceability-matrix.md";
    public const string GateDecisionFileName = "gate-decision.json";
    public const string TraceSummaryFileName = "e2e-trace-summary.json";
    public const string NfrAssessmentFileName = "nfr-assessment.md";
    public const string TestReviewFileName = "test-review.md";

    /// <summary>The two non-markdown sources this story admits, by EXACT filename. ADR 0020 scopes the whole
    /// non-markdown ingest to this pair inside the discovered <c>test-artifacts</c> root — it is emphatically not
    /// a general "ingest any JSON" seam.</summary>
    public static readonly IReadOnlyList<string> JsonFileNames = new[] { GateDecisionFileName, TraceSummaryFileName };

    /// <summary>The <c>schema_version</c> major this build understands. Both files ship <c>"0.1.0"</c> today.
    /// A minor bump inside the same major is accepted (additive by convention); a major bump is
    /// <see cref="TeaJsonOutcome.UnsupportedSchema"/> — skipped, never guessed at.</summary>
    public const int SupportedSchemaMajor = 0;

    // ---- Tier + identity ------------------------------------------------------------------------------------

    /// <summary>The tier for one discovered filename. See <see cref="CoverageTier"/> for what each promises.</summary>
    public static CoverageTier TierFor(string fileName)
    {
        var name = LeafName(fileName);

        if (Matches(name, TraceMatrixFileName) || Matches(name, GateDecisionFileName) || Matches(name, TraceSummaryFileName))
        {
            return CoverageTier.Summarized;
        }

        return ProducingSkillFor(name) is null ? CoverageTier.Unsupported : CoverageTier.Rendered;
    }

    /// <summary>The upstream skill that writes a given output filename, or null when the name matches no pinned
    /// output. Every mapping below comes from that skill's own <c>workflow.yaml</c>
    /// (<c>default_output_file</c> / <c>outputs[].path</c>).</summary>
    public static string? ProducingSkillFor(string fileName)
    {
        var name = LeafName(fileName);

        if (Matches(name, TraceMatrixFileName) || Matches(name, GateDecisionFileName) || Matches(name, TraceSummaryFileName))
            return "bmad-testarch-trace";
        if (Matches(name, NfrAssessmentFileName)) return "bmad-testarch-nfr";
        if (Matches(name, TestReviewFileName)) return "bmad-testarch-test-review";
        // test-design-architecture.md, test-design-qa.md, test-design-epic-{n}.md, and the
        // test-design/{project}-handoff.md hand-off all come from the one mode-switching skill.
        if (name.StartsWith("test-design", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("-handoff.md", StringComparison.OrdinalIgnoreCase))
            return "bmad-testarch-test-design";
        if (name.StartsWith("atdd-checklist", StringComparison.OrdinalIgnoreCase)) return "bmad-testarch-atdd";

        return null;
    }

    /// <summary>The row's human label: the artifact's own well-known name where it has one, else the filename
    /// de-kebabed. Never invents a description the file doesn't have.</summary>
    public static string TitleFor(string fileName)
    {
        var name = LeafName(fileName);
        if (Matches(name, TraceMatrixFileName)) return "Traceability matrix";
        if (Matches(name, GateDecisionFileName)) return "Quality gate decision";
        if (Matches(name, TraceSummaryFileName)) return "End-to-end trace summary";
        if (Matches(name, NfrAssessmentFileName)) return "NFR evidence audit";
        if (Matches(name, TestReviewFileName)) return "Test quality review";
        // The mode-switching test-design skill's four declared outputs. Named rather than de-kebabed because
        // `test-design-qa.md` de-kebabs to "Test design qa" — a label that reads like a bug. [live-browser pass]
        if (Matches(name, "test-design-architecture.md")) return "System test architecture";
        if (Matches(name, "test-design-qa.md")) return "System test design (QA)";
        if (name.StartsWith("test-design-epic-", StringComparison.OrdinalIgnoreCase)) return "Epic test plan";
        if (name.EndsWith("-handoff.md", StringComparison.OrdinalIgnoreCase)) return "Test design hand-off";
        if (name.StartsWith("atdd-checklist", StringComparison.OrdinalIgnoreCase)) return "ATDD checklist";

        var stem = name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name[..^3]
            : name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name[..^5]
            : name;
        return IdeaDerivation.DeKebab(stem);
    }

    // ---- JSON schema gate + readers ------------------------------------------------------------------------

    /// <summary>True when a <c>schema_version</c> string's MAJOR component is one this build understands. An
    /// absent, empty or unparseable version is not supported — a file that will not say what shape it is gets
    /// skipped, never guessed at.</summary>
    public static bool IsSchemaSupported(string? schemaVersion)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion)) return false;

        var head = schemaVersion.Trim().Split('.')[0];
        return int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            && major == SupportedSchemaMajor
            // "2" alone parses as major 2 and is correctly rejected; "0" alone would parse as major 0 but is not a
            // version this file's own writer can emit, so require the dotted form the schema actually ships.
            && schemaVersion.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>Reads <c>gate-decision.json</c>. Version-gated before any field is touched.</summary>
    public static TeaJsonOutcome TryParseGateDecision(string json, out TestGateDecision? gate)
    {
        gate = null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return TeaJsonOutcome.Malformed;

            if (!IsSchemaSupported(Str(root, "schema_version"))) return TeaJsonOutcome.UnsupportedSchema;

            var status = Str(root, "gate_status");
            if (string.IsNullOrWhiteSpace(status)) return TeaJsonOutcome.Malformed;

            var target = Obj(root, "target");
            gate = new TestGateDecision(
                Status: status!.Trim().ToUpperInvariant(),
                Rationale: Str(root, "rationale"),
                CriticalOpen: Int(root, "critical_open"),
                P0Status: Str(root, "p0_status"),
                P1Status: Str(root, "p1_status"),
                OverallStatus: Str(root, "overall_status"),
                TargetType: target is { } t ? Str(t, "type") : null,
                // Both are literally null in upstream's own example. Read for display only; never keyed on.
                TargetId: target is { } t2 ? Str(t2, "id") : null,
                TargetLabel: target is { } t3 ? Str(t3, "label") : null);
            return TeaJsonOutcome.Parsed;
        }
        catch (Exception)
        {
            return TeaJsonOutcome.Malformed;
        }
    }

    /// <summary>Reads <c>e2e-trace-summary.json</c>. Version-gated before any field is touched.
    /// <c>gate_status</c> is OPTIONAL by upstream contract (gate-eligible runs only).</summary>
    public static TeaJsonOutcome TryParseTraceSummary(string json, out TestTraceSummary? summary)
    {
        summary = null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return TeaJsonOutcome.Malformed;

            if (!IsSchemaSupported(Str(root, "schema_version"))) return TeaJsonOutcome.UnsupportedSchema;

            var oracle = Obj(root, "oracle");
            var coverage = Obj(root, "coverage");
            var inventory = coverage is { } c1 ? Obj(c1, "inventory") : null;
            var tests = Obj(root, "tests");
            var risk = Obj(root, "risk_summary");
            // Present only alongside gate_status, on a gate-eligible run (same upstream `if (gateEligible)` guard).
            var gateCriteria = Obj(root, "gate_criteria");

            summary = new TestTraceSummary
            {
                InventoryBasis = Str(root, "inventory_basis"),
                Confidence = Str(root, "confidence") ?? (oracle is { } o1 ? Str(o1, "confidence") : null),
                SyntheticOracle = oracle is { } o2 && Bool(o2, "synthetic") == true,
                CollectionStatus = Str(root, "collection_status"),
                CoveredCount = inventory is { } i1 ? Int(i1, "covered") : null,
                TotalCount = inventory is { } i2 ? Int(i2, "total") : null,
                OverallPercent = inventory is { } i3 ? Num(i3, "pct") : null,
                PriorityBreakdown = coverage is { } c2 ? ReadPriorityBreakdown(c2) : Array.Empty<TeaPriorityCoverage>(),
                ByLevel = coverage is { } c3 ? ReadByLevel(c3) : Array.Empty<TeaLevelCoverage>(),
                TestFiles = tests is { } t1 ? Int(t1, "files") : null,
                TestCases = tests is { } t2 ? Int(t2, "cases") : null,
                CriticalOpen = risk is { } r ? Int(r, "critical_open") : null,
                GateStatus = Str(root, "gate_status")?.Trim().ToUpperInvariant(),
                P0Status = gateCriteria is { } gc1 ? Str(gc1, "p0_status")?.Trim().ToUpperInvariant() : null,
                P1Status = gateCriteria is { } gc2 ? Str(gc2, "p1_status")?.Trim().ToUpperInvariant() : null,
                OverallStatus = gateCriteria is { } gc3 ? Str(gc3, "overall_status")?.Trim().ToUpperInvariant() : null,
            };
            return TeaJsonOutcome.Parsed;
        }
        catch (Exception)
        {
            return TeaJsonOutcome.Malformed;
        }
    }

    private static IReadOnlyList<TeaPriorityCoverage> ReadPriorityBreakdown(JsonElement coverage)
    {
        if (Obj(coverage, "priority_breakdown") is not { } breakdown) return Array.Empty<TeaPriorityCoverage>();

        var rows = new List<TeaPriorityCoverage>();
        // Fixed P0..P3 order rather than document order — upstream's own gate logic requires all four, and a
        // fixed order keeps a from-scratch regeneration byte-identical.
        foreach (var key in new[] { "P0", "P1", "P2", "P3" })
        {
            if (Obj(breakdown, key) is not { } p) continue;
            rows.Add(new TeaPriorityCoverage(key, Int(p, "total") ?? 0, Int(p, "covered") ?? 0, Num(p, "pct")));
        }
        return rows;
    }

    private static IReadOnlyList<TeaLevelCoverage> ReadByLevel(JsonElement coverage)
    {
        if (Obj(coverage, "by_level") is not { } byLevel) return Array.Empty<TeaLevelCoverage>();

        var rows = new List<TeaLevelCoverage>();
        // `coverage_levels` in trace/workflow.yaml is exactly "e2e,api,component,unit".
        foreach (var level in new[] { "e2e", "api", "component", "unit" })
        {
            if (Int(byLevel, level) is { } count) rows.Add(new TeaLevelCoverage(level, count));
        }
        return rows;
    }

    // ---- traceability-matrix.md grammar --------------------------------------------------------------------

    /// <summary>Reads what <c>traceability-matrix.md</c> yields on its own: the frontmatter oracle signals, the
    /// Coverage Summary table, the Detailed Mapping headings, and the Phase 2 gate word. Never throws — an
    /// unrecognizable document yields <see cref="TeaMatrix.Empty"/>, which the caller reports as
    /// <see cref="AdapterDiagnosticCategory.Unsupported"/>.
    ///
    /// <para><b>Why its own frontmatter reader and not <c>Memlog.TrySplit</c>.</b> That splitter documents itself
    /// as mirroring <c>memlog.py</c>'s <c>split()</c> EXACTLY; binding TEA's file format to a different BMad
    /// tool's contract would mean a change in <c>memlog.py</c> silently breaks TEA parsing. The two formats are
    /// look-alikes, not the same format.</para></summary>
    public static TeaMatrix ParseMatrix(string markdown)
    {
        try
        {
            var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var frontmatter = ReadFrontmatter(lines, out var bodyStart);

            string? gate = null;
            var priorities = new List<TeaPriorityCoverage>();
            var criteria = new List<TeaCriterionCoverage>();

            // Detailed-mapping accumulator: a heading opens a criterion, the next heading (or EOF) closes it.
            string? openId = null, openDescription = null, openPriority = null, openCoverage = null;
            var openTestCount = 0;
            var inTestsBlock = false;
            // Scopes TryReadPriorityRow to the "Coverage Summary" table only. Any `|`-prefixed line anywhere in
            // the document used to be tried as a priority row — currently safe only because the sibling
            // "Coverage by Test Level" table's row labels (E2E/API/Component/Unit) don't collide with the
            // `P<digit>` shape it matches on, an incidental save rather than a structural guard. [Review][Patch]
            var inCoverageSummary = false;

            void CloseOpenCriterion()
            {
                if (openId is null) return;
                // A missing OR unrecognized "- **Coverage:**" bullet both leave `openCoverage` null (see
                // TryReadCoverageBullet) and used to default to "NONE" here — asserting zero coverage, a
                // substantive claim, rather than admitting the value could not be read. "UNRECOGNIZED" is not one
                // of the five words TryReadCoverageBullet accepts, so CoverageBadge's own fallback branch renders
                // it distinctly (an "unrecognized" style, the word shown verbatim) instead of the "NONE" styling
                // a real explicit NONE gets — an honest gap rather than a fabricated coverage claim, matching
                // this story's own design principle. [Review][Patch]
                criteria.Add(new TeaCriterionCoverage(
                    openId, openDescription ?? string.Empty, openCoverage ?? "UNRECOGNIZED", openPriority, openTestCount));
                openId = null; openDescription = null; openPriority = null; openCoverage = null;
                openTestCount = 0; inTestsBlock = false;
            }

            for (var i = bodyStart; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (trimmed.StartsWith("#### ", StringComparison.Ordinal))
                {
                    CloseOpenCriterion();
                    inCoverageSummary = false;
                    if (TryReadCriterionHeading(trimmed[5..].Trim(), out var id, out var desc, out var priority))
                    {
                        openId = id; openDescription = desc; openPriority = priority;
                    }
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    CloseOpenCriterion();
                    inCoverageSummary = trimmed.Contains("Coverage Summary", StringComparison.OrdinalIgnoreCase);
                    // "### GATE DECISION: CONCERNS" — the Phase 2 verdict as an h3. The template's own
                    // placeholder line reads "{PASS | CONCERNS | FAIL | WAIVED}", which TryReadGateWord rejects.
                    if (TryReadGateWord(trimmed, out var word)) gate = word;
                    continue;
                }

                if (openId is not null)
                {
                    if (TryReadCoverageBullet(trimmed, out var coverage)) { openCoverage = coverage; inTestsBlock = false; continue; }
                    if (trimmed.StartsWith("- **Tests:**", StringComparison.OrdinalIgnoreCase)) { inTestsBlock = true; continue; }
                    // Any other top-level bullet ends the tests block (Gaps, Recommendation).
                    if (trimmed.StartsWith("- **", StringComparison.Ordinal)) { inTestsBlock = false; continue; }
                    // A test line is a nested bullet naming a backticked test id.
                    if (inTestsBlock && trimmed.StartsWith("- ", StringComparison.Ordinal) && trimmed.Contains('`')) openTestCount++;
                    continue;
                }

                if (inCoverageSummary && TryReadPriorityRow(trimmed, out var row)) priorities.Add(row!);
            }

            CloseOpenCriterion();

            return new TeaMatrix
            {
                CoverageBasis = Unquote(frontmatter.GetValueOrDefault("coverageBasis")),
                OracleConfidence = Unquote(frontmatter.GetValueOrDefault("oracleConfidence")),
                OracleResolutionMode = Unquote(frontmatter.GetValueOrDefault("oracleResolutionMode")),
                GateStatus = gate,
                PriorityBreakdown = priorities,
                Criteria = criteria,
            };
        }
        catch (Exception)
        {
            return TeaMatrix.Empty;
        }
    }

    /// <summary>Splits leading <c>---</c>-fenced frontmatter into a flat key→value map. Deliberately minimal: TEA
    /// writes flat scalars plus two inline arrays, and nothing here needs the arrays.</summary>
    private static Dictionary<string, string> ReadFrontmatter(string[] lines, out int bodyStart)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bodyStart = 0;
        if (lines.Length == 0 || lines[0].Trim() != "---") return map;

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---") { bodyStart = i + 1; break; }
            var colon = lines[i].IndexOf(':');
            if (colon < 0) continue;
            map[lines[i][..colon].Trim()] = lines[i][(colon + 1)..].Trim();
        }

        return map;
    }

    private static string? Unquote(string? value)
    {
        if (value is null) return null;
        var v = value.Trim().Trim('\'', '"').Trim();
        return v.Length == 0 || IsPlaceholder(v) ? null : v;
    }

    /// <summary>An un-substituted template token — <c>{CRITERION_ID}</c>, <c>{PASS | CONCERNS | FAIL | WAIVED}</c>.
    /// A copied-but-never-run template must yield nothing rather than fabricated rows.</summary>
    private static bool IsPlaceholder(string value) =>
        value.StartsWith('{') && value.EndsWith('}');

    /// <summary>Reads <c>{CRITERION_ID}: {DESCRIPTION} ({PRIORITY})</c>. Rejects the template's own placeholder
    /// heading and its two worked <c>Example:</c> blocks.
    /// <para>Splits on the first <c>": "</c> (colon-SPACE), not the first bare colon: <see cref="ResolveJoinTarget"/>
    /// accepts <c>:</c> as one of the compound-id separators a criterion id may carry internally (e.g.
    /// <c>18.4:AC-2</c>), and the template's own id/description boundary is always colon-space by construction.
    /// A bare first-colon split would cut a colon-separated compound id in half. Falls back to the first bare
    /// colon when no colon-space exists, so a template deviating from its own convention still parses.
    /// [Review][Patch]</para></summary>
    private static bool TryReadCriterionHeading(string heading, out string id, out string description, out string? priority)
    {
        id = string.Empty; description = string.Empty; priority = null;
        if (heading.Length == 0) return false;
        if (heading.StartsWith("Example:", StringComparison.OrdinalIgnoreCase)) return false;

        var colon = heading.IndexOf(": ", StringComparison.Ordinal);
        if (colon < 0) colon = heading.IndexOf(':');
        if (colon <= 0) return false;

        var rawId = heading[..colon].Trim();
        if (rawId.Length == 0 || IsPlaceholder(rawId)) return false;

        var rest = heading[(colon + 1)..].Trim();
        var open = rest.LastIndexOf('(');
        if (open >= 0 && rest.EndsWith(')'))
        {
            var token = rest[(open + 1)..^1].Trim();
            if (!IsPlaceholder(token) && token.Length is 2 && token[0] is 'P' or 'p' && char.IsDigit(token[1]))
            {
                priority = token.ToUpperInvariant();
                rest = rest[..open].Trim();
            }
        }

        id = rawId;
        description = rest;
        return true;
    }

    private static readonly IReadOnlySet<string> CoverageWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FULL", "PARTIAL", "NONE", "UNIT-ONLY", "INTEGRATION-ONLY" };

    /// <summary>Reads <c>- **Coverage:** FULL ✅</c>. The status icon is decoration; only the word is read.</summary>
    private static bool TryReadCoverageBullet(string line, out string coverage)
    {
        coverage = string.Empty;
        const string marker = "- **Coverage:**";
        if (!line.StartsWith(marker, StringComparison.OrdinalIgnoreCase)) return false;

        var word = line[marker.Length..].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (word is null || !CoverageWords.Contains(word)) return false;

        coverage = word.ToUpperInvariant();
        return true;
    }

    /// <summary>Reads the <c>### GATE DECISION: {WORD}</c> heading, accepting only one of the four verdicts
    /// upstream's own guard admits — so the template's literal <c>{PASS | CONCERNS | FAIL | WAIVED}</c> line
    /// never becomes a verdict.</summary>
    private static bool TryReadGateWord(string heading, out string word)
    {
        word = string.Empty;
        var idx = heading.IndexOf("GATE DECISION:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var candidate = heading[(idx + "GATE DECISION:".Length)..].Trim().Trim('*', '#', ' ').ToUpperInvariant();
        if (!TestGateDecision.KnownStatuses.Contains(candidate)) return false;

        word = candidate;
        return true;
    }

    /// <summary>Reads one <c>| P0 | 4 | 4 | 100% | PASS |</c> row of the Coverage Summary table. The bolded
    /// <c>**Total**</c> row is not a priority and is skipped.</summary>
    private static bool TryReadPriorityRow(string line, out TeaPriorityCoverage? row)
    {
        row = null;
        if (!line.StartsWith('|')) return false;

        var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
        if (cells.Length < 4) return false;

        var priority = cells[0].Trim('*', ' ');
        if (priority.Length != 2 || priority[0] is not ('P' or 'p') || !char.IsDigit(priority[1])) return false;

        if (!int.TryParse(Clean(cells[1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var total)) return false;
        if (!int.TryParse(Clean(cells[2]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var covered)) return false;

        double? pct = double.TryParse(Clean(cells[3]).TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p)
            ? p : null;

        row = new TeaPriorityCoverage(priority.ToUpperInvariant(), total, covered, pct);
        return true;

        static string Clean(string cell) => cell.Replace("*", string.Empty).Trim();
    }

    // ---- Join admissibility (D2) ---------------------------------------------------------------------------

    /// <summary>Judges whether TEA's matrix may be projected onto SpecScribe's requirement × epic traceability
    /// surface AT ALL, from the two oracle signals — before any criterion id is looked at.
    ///
    /// <para><b>Why this gate exists.</b> SpecScribe's <c>traceability.html</c> is a REQUIREMENT × COVERING-EPIC
    /// matrix built from <c>RequirementsModel</c> (parsed from <c>epics.md</c>'s "## Requirements Inventory");
    /// TEA's matrix is an ORACLE-ITEM × TEST matrix keyed P0–P3. Different axes. Upstream's
    /// <c>step-03-map-criteria.md</c> states the criterion ID format is NOT specified — an oracle item may be a
    /// formal requirement, an OpenAPI endpoint, or a synthetic journey — so only the
    /// <c>acceptance_criteria</c> basis, from a non-synthetic oracle, at high confidence, describes the same
    /// things SpecScribe's requirements do.</para>
    ///
    /// <para>Story 21.1's own review already caught the failure mode this prevents: a phantom-covered requirement
    /// that counted as covered and drew blank. Prefer an honest gap to a fabricated link. See also ADR 0019
    /// (proposed) — every TEA artifact is LLM-authored, so the matrix ENRICHES SpecScribe's own FR→epic coverage
    /// and never overrides it.</para></summary>
    public static TeaJoinVerdict JudgeJoin(string? basis, string? confidence, bool synthetic)
    {
        var b = (basis ?? string.Empty).Trim().ToLowerInvariant();
        var c = (confidence ?? string.Empty).Trim().ToLowerInvariant();

        if (b.Length == 0)
        {
            return new TeaJoinVerdict(false,
                "the traceability matrix does not record which coverage oracle it was built from, so its items cannot be matched to requirements");
        }

        if (b != "acceptance_criteria")
        {
            return new TeaJoinVerdict(false,
                $"its coverage oracle is '{b}', not formal acceptance criteria, so its items describe endpoints or journeys rather than this project's requirements");
        }

        if (synthetic)
        {
            return new TeaJoinVerdict(false,
                "its coverage oracle was inferred from source rather than read from a formal artifact, so its items are not this project's requirements");
        }

        if (c != "high")
        {
            return new TeaJoinVerdict(false,
                c.Length == 0
                    ? "it records no confidence in its coverage oracle"
                    : $"it records only '{c}' confidence in its coverage oracle");
        }

        return new TeaJoinVerdict(true, "its coverage oracle is this project's own acceptance criteria, read at high confidence");
    }

    /// <summary>Resolves ONE TEA oracle item to an id SpecScribe actually holds, or null.
    ///
    /// <para>Exactly two forms are admitted, and both require a literal match against an id that EXISTS:</para>
    /// <list type="number">
    /// <item>the whole item is a requirement id (<c>FR12</c>, <c>NFR3</c>, <c>UX-DR2</c>) or a story id
    /// (<c>18.5</c>);</item>
    /// <item>the item is a story id followed by a separator and a criterion suffix (<c>18.4-AC-2</c>), where the
    /// PREFIX itself matches a story that exists.</item>
    /// </list>
    /// <para>Nothing else. No fuzzy matching, no title similarity, no "AC-1 probably means the first AC of the
    /// story this run was about" — the run's <c>target.id</c> is literally null in upstream's own schema example,
    /// so there is no scope to attach a bare <c>AC-n</c> to. An unresolvable item is <c>unsupported</c> with a
    /// notice.</para></summary>
    public static string? ResolveJoinTarget(
        string criterionId, IReadOnlyCollection<string> requirementIds, IReadOnlyCollection<string> storyIds)
    {
        var id = (criterionId ?? string.Empty).Trim();
        if (id.Length == 0) return null;

        var requirement = requirementIds.FirstOrDefault(r => string.Equals(r, id, StringComparison.OrdinalIgnoreCase));
        if (requirement is not null) return requirement;

        var story = storyIds.FirstOrDefault(s => string.Equals(s, id, StringComparison.OrdinalIgnoreCase));
        if (story is not null) return story;

        // Form 2: a story-id prefix followed by one of the separators TEA's own examples use.
        foreach (var candidate in storyIds)
        {
            if (candidate.Length >= id.Length) continue;
            if (!id.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)) continue;
            var next = id[candidate.Length];
            if (next is '-' or ':' or ' ' or '.' or '/') return candidate;
        }

        return null;
    }

    /// <summary>Applies <paramref name="verdict"/> and then resolves each criterion. An inadmissible verdict
    /// yields ZERO rows however many ids would have resolved — the basis is judged first, on purpose.
    /// <see cref="TeaJoin.UnresolvedCount"/> is the honest count of TEA rows the surface cannot place.</summary>
    public static TeaJoin BuildJoin(
        IReadOnlyList<TeaCriterionCoverage> criteria,
        TeaJoinVerdict verdict,
        IReadOnlyCollection<string> requirementIds,
        IReadOnlyCollection<string> storyIds)
    {
        if (!verdict.Admissible)
        {
            return new TeaJoin(false, verdict.Reason, Array.Empty<TeaJoinRow>(), criteria.Count);
        }

        var rows = new List<TeaJoinRow>();
        var unresolved = 0;
        foreach (var criterion in criteria)
        {
            var target = ResolveJoinTarget(criterion.CriterionId, requirementIds, storyIds);
            if (target is null) { unresolved++; continue; }
            rows.Add(new TeaJoinRow(target, criterion));
        }

        return new TeaJoin(true, verdict.Reason, rows, unresolved);
    }

    // ---- Small JSON helpers (System.Text.Json only — no new package dependency, ADR 0010) ------------------

    private static JsonElement? Obj(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value : null;

    private static string? Str(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static int? Int(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var d)
            ? (int)Math.Round(d) : null;

    private static double? Num(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var d)
            ? d : null;

    private static bool? Bool(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : null;

    private static string LeafName(string path)
    {
        var normalized = PathUtil.NormalizeSlashes(path ?? string.Empty);
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? normalized : normalized[(slash + 1)..];
    }

    private static bool Matches(string name, string wellKnown) =>
        string.Equals(name, wellKnown, StringComparison.OrdinalIgnoreCase);
}
