using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 18.5 — the PURE half of Test Architect (TEA) coverage: the coverage-tier vocabulary, the two
/// JSON schema gates, the <c>traceability-matrix.md</c> grammar reader, and the join-admissibility rule. Every
/// test here runs without touching disk, which is the split's whole point (the same
/// <c>ArtifactCoverage</c>/<c>WorkInventory</c>/<c>IdeaDerivation</c> discipline).
///
/// <para><b>Upstream-pinned fixture provenance</b> [Story 18.5 Task 1; ADR 0015 Decision 7]. Every artifact shape
/// asserted below is VERBATIM structure from <c>bmad-code-org/bmad-method-test-architecture-enterprise</c>, branch
/// <c>main</c>, re-fetched 2026-07-27 and pinned to the commit that last touched each file:</para>
/// <code>
///   src/module.yaml                                              556fd1da9964b9586c5aad60035ead399f5c3498
///   src/module-help.csv                                          4a7522664ad4bf1c5338a1819144de458eaebecd
///   .../bmad-testarch-trace/workflow.yaml                        9b0aad20a89c7d646ec2b0bf6005e7c91e7d9965
///   .../bmad-testarch-nfr/workflow.yaml                          9d347f9b0fb237d2695b60321597cd78c142019b
///   .../bmad-testarch-test-review/workflow.yaml                  4bb48960daf0f1bf691196c1ba3b2dc4899580f6
///   .../bmad-testarch-test-design/workflow.yaml                  4bb48960daf0f1bf691196c1ba3b2dc4899580f6
///   .../bmad-testarch-atdd/workflow.yaml                         30e999615333b1b7b7e7855f810613b1ab3d43a1
///   .../bmad-testarch-trace/trace-template.md                    4a7522664ad4bf1c5338a1819144de458eaebecd
///   .../bmad-testarch-trace/steps-c/step-05-gate-decision.md     81ca8f1d5e7bb5750bc707f31d88bc52b9e1ed6e
///   .../bmad-testarch-trace/steps-c/step-03-map-criteria.md      1d33b3838d36e88c51f0c203a54c5e7d14f93d8e
/// </code>
/// <para>⚠️ <b>The authority is <c>step-05-gate-decision.md</c>, not <c>workflow.yaml</c>.</b> That workflow.yaml
/// carries an inline comment block describing <c>e2e-trace-summary.json</c> as <c>schema_version: 1</c> with
/// <c>generated_at</c> / <c>coverage_statistics</c> / <c>gap_analysis</c> keys. The step file that actually WRITES
/// the file emits <c>schema_version: '0.1.0'</c> with <c>snapshot_at</c> / <c>coverage</c> / <c>risk_summary</c>.
/// The comment is stale; a parser built from it would find nothing.</para></summary>
public class TestArtifactDerivationTests
{
    // ---- Coverage-tier vocabulary (Task 3) -----------------------------------------------------------------

    [Fact]
    public void CoverageTier_EveryValue_CarriesAWordAndADescription()
    {
        foreach (var tier in Enum.GetValues<CoverageTier>())
        {
            Assert.False(string.IsNullOrWhiteSpace(CoverageTiers.Word(tier)));
            Assert.False(string.IsNullOrWhiteSpace(CoverageTiers.Description(tier)));
        }
    }

    [Theory]
    // Interpreted only as prose on its own generated page.
    [InlineData("nfr-assessment.md", CoverageTier.Rendered)]
    [InlineData("test-review.md", CoverageTier.Rendered)]
    [InlineData("test-design-architecture.md", CoverageTier.Rendered)]
    [InlineData("test-design-qa.md", CoverageTier.Rendered)]
    [InlineData("test-design-epic-7.md", CoverageTier.Rendered)]
    [InlineData("atdd-checklist-18-5-priority.md", CoverageTier.Rendered)]
    // A structured headline is extracted on top of (or instead of) the prose page.
    [InlineData("traceability-matrix.md", CoverageTier.Summarized)]
    [InlineData("gate-decision.json", CoverageTier.Summarized)]
    [InlineData("e2e-trace-summary.json", CoverageTier.Summarized)]
    // Discovered and named; nothing interpreted. `bmad-teach-me-testing`'s outputs are unpinned upstream.
    [InlineData("tea-academy-progress.md", CoverageTier.Unsupported)]
    [InlineData("certificate.md", CoverageTier.Unsupported)]
    public void TierFor_AssignsTheDocumentedTier(string fileName, CoverageTier expected) =>
        Assert.Equal(expected, TestArtifactDerivation.TierFor(fileName));

    [Fact]
    public void ProducingSkillFor_NamesTheUpstreamSkill_OrNullWhenUnmodelled()
    {
        Assert.Equal("bmad-testarch-trace", TestArtifactDerivation.ProducingSkillFor("traceability-matrix.md"));
        Assert.Equal("bmad-testarch-trace", TestArtifactDerivation.ProducingSkillFor("gate-decision.json"));
        Assert.Equal("bmad-testarch-nfr", TestArtifactDerivation.ProducingSkillFor("nfr-assessment.md"));
        Assert.Equal("bmad-testarch-test-review", TestArtifactDerivation.ProducingSkillFor("test-review.md"));
        Assert.Equal("bmad-testarch-test-design", TestArtifactDerivation.ProducingSkillFor("test-design-qa.md"));
        Assert.Equal("bmad-testarch-atdd", TestArtifactDerivation.ProducingSkillFor("atdd-checklist-1-2-x.md"));
        Assert.Null(TestArtifactDerivation.ProducingSkillFor("who-knows.md"));
    }

    // ---- Schema-version gate (Task 5) ----------------------------------------------------------------------

    [Theory]
    [InlineData("0.1.0", true)]
    [InlineData("0.2.7", true)]     // same major: forward-compatible within 0.x
    [InlineData("1.0.0", false)]    // a major bump is exactly what the gate exists to catch
    [InlineData("2", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not-a-version", false)]
    public void IsSchemaSupported_GatesOnTheMajorVersionOnly(string? version, bool expected) =>
        Assert.Equal(expected, TestArtifactDerivation.IsSchemaSupported(version));

    // ---- gate-decision.json (Task 5) -----------------------------------------------------------------------

    /// <summary>VERBATIM key set emitted by <c>step-05-gate-decision.md</c>'s <c>gateDecisionSlim</c> literal.
    /// <c>target.id</c>/<c>target.label</c> are null in the schema's own example and are null here too.</summary>
    private const string GateDecisionJson = """
        {
          "schema_version": "0.1.0",
          "evaluated_at": "2026-07-20T14:02:11.000Z",
          "repo": "SpecScribe",
          "target": { "type": "story", "id": null, "label": null },
          "collection_status": "COLLECTED",
          "gate_basis": "priority_thresholds",
          "gate_status": "CONCERNS",
          "rationale": "All P0 criteria met; P1 coverage 88% is below the 90% target.",
          "p0_status": "MET",
          "p1_status": "PARTIAL",
          "overall_status": "MET",
          "critical_open": 0,
          "links": { "trace_report_path": "_bmad-output/test-artifacts/traceability-matrix.md", "trace_report_url": "", "artifact_url": "", "journey_evidence_url": "" }
        }
        """;

    [Fact]
    public void TryParseGateDecision_ReadsTheSlimGateSignal()
    {
        var outcome = TestArtifactDerivation.TryParseGateDecision(GateDecisionJson, out var gate);

        Assert.Equal(TeaJsonOutcome.Parsed, outcome);
        Assert.NotNull(gate);
        Assert.Equal("CONCERNS", gate!.Status);
        Assert.Equal("MET", gate.P0Status);
        Assert.Equal("PARTIAL", gate.P1Status);
        Assert.Equal("MET", gate.OverallStatus);
        Assert.Equal(0, gate.CriticalOpen);
        Assert.Contains("P1 coverage 88%", gate.Rationale);
    }

    [Fact]
    public void TryParseGateDecision_NullTargetIdAndLabel_StillParses_AndKeysOnNothing()
    {
        TestArtifactDerivation.TryParseGateDecision(GateDecisionJson, out var gate);

        // The schema's own example ships both as literal null. Nothing may key on them.
        Assert.Null(gate!.TargetId);
        Assert.Equal("story", gate.TargetType);
    }

    [Fact]
    public void TryParseGateDecision_UnknownSchemaMajor_IsSkippedNotMalformed()
    {
        var json = GateDecisionJson.Replace("\"0.1.0\"", "\"1.0.0\"");

        var outcome = TestArtifactDerivation.TryParseGateDecision(json, out var gate);

        Assert.Equal(TeaJsonOutcome.UnsupportedSchema, outcome);
        Assert.Null(gate);
    }

    [Fact]
    public void TryParseGateDecision_UnparseableJson_IsMalformed_AndNeverThrows()
    {
        var outcome = TestArtifactDerivation.TryParseGateDecision("{ this is not json", out var gate);

        Assert.Equal(TeaJsonOutcome.Malformed, outcome);
        Assert.Null(gate);
    }

    // ---- e2e-trace-summary.json (Task 5) -------------------------------------------------------------------

    /// <summary>VERBATIM key set from <c>step-05</c>'s <c>e2eTraceSummary</c> literal. <c>gate_status</c> and
    /// <c>gate_criteria</c> are appended ONLY when the run is gate-eligible, so both are optional to a reader.</summary>
    private const string TraceSummaryJson = """
        {
          "schema_version": "0.1.0",
          "snapshot_at": "2026-07-20T14:02:11.000Z",
          "repo": "SpecScribe",
          "collection_mode": "contract_static",
          "collection_status": "COLLECTED",
          "inventory_basis": "acceptance_criteria",
          "gate_basis": "priority_thresholds",
          "source_sha": "",
          "target": { "type": "story", "id": null, "label": null },
          "decision_mode": "deterministic",
          "evaluator": "TEA Agent",
          "confidence": "high",
          "oracle": { "resolution_mode": "formal_requirements", "confidence": "high", "sources": ["epics.md"], "external_pointer_status": "not_used", "synthetic": false },
          "coverage": {
            "inventory": { "covered": 7, "total": 10, "pct": 70 },
            "priority_breakdown": {
              "P0": { "total": 4, "covered": 4, "pct": 100 },
              "P1": { "total": 3, "covered": 2, "pct": 66.7 },
              "P2": { "total": 2, "covered": 1, "pct": 50 },
              "P3": { "total": 1, "covered": 0, "pct": 0 }
            },
            "by_level": { "e2e": 6, "api": 4, "component": 2, "unit": 11 }
          },
          "tests": { "files": 5, "cases": 23, "skipped_cases": 1, "fixme_cases": 0, "pending_cases": 0 },
          "risk_summary": { "critical_open": 0, "high_open": 2, "medium_open": 1, "low_open": 3 },
          "heuristics": { "endpoint_gaps": 0, "auth_negative_path_status": "present", "error_path_status": "partial", "ui_journey_status": "not_applicable", "ui_state_status": "not_applicable" },
          "blockers": [],
          "recommendations": [],
          "links": { "trace_report_path": "_bmad-output/test-artifacts/traceability-matrix.md", "trace_report_url": "", "artifact_url": "", "journey_evidence_url": "" },
          "gate_status": "CONCERNS",
          "gate_criteria": { "p0_coverage_required": "100%", "p0_status": "MET", "p1_status": "PARTIAL", "overall_status": "MET" }
        }
        """;

    [Fact]
    public void TryParseTraceSummary_ReadsCoveragePriorityAndLevelBreakdowns()
    {
        var outcome = TestArtifactDerivation.TryParseTraceSummary(TraceSummaryJson, out var summary);

        Assert.Equal(TeaJsonOutcome.Parsed, outcome);
        Assert.NotNull(summary);
        Assert.Equal("acceptance_criteria", summary!.InventoryBasis);
        Assert.Equal("high", summary.Confidence);
        Assert.False(summary.SyntheticOracle);
        Assert.Equal(7, summary.CoveredCount);
        Assert.Equal(10, summary.TotalCount);
        Assert.Equal(70d, summary.OverallPercent);
        Assert.Equal(4, summary.PriorityBreakdown.Count);
        Assert.Equal("P0", summary.PriorityBreakdown[0].Priority);
        Assert.Equal(100d, summary.PriorityBreakdown[0].Percent);
        Assert.Equal(4, summary.ByLevel.Count);
        Assert.Equal(11, summary.ByLevel.Single(l => l.Level == "unit").Tests);
        Assert.Equal(23, summary.TestCases);
        Assert.Equal(0, summary.CriticalOpen);
        Assert.Equal("CONCERNS", summary.GateStatus);
    }

    [Fact]
    public void TryParseTraceSummary_NonGateEligibleRun_OmitsGateStatus_WithoutFailing()
    {
        // step-05 only appends gate_status/gate_criteria inside `if (gateEligible)`. A reader that requires
        // them would report every inventory-only run as malformed.
        // A raw string literal keeps the SOURCE FILE's line endings, so on a CRLF checkout (what a Windows CI
        // runner produces from an LF-committed file under core.autocrlf) every "\n"-anchored Replace below
        // silently matched nothing and the gate keys survived — the assert then read "CONCERNS", not null.
        // Normalize first so this surgery is line-ending agnostic on every host.
        var json = TraceSummaryJson.ReplaceLineEndings("\n")
            .Replace("  \"gate_status\": \"CONCERNS\",\n", string.Empty)
            .Replace("  \"gate_criteria\": { \"p0_coverage_required\": \"100%\", \"p0_status\": \"MET\", \"p1_status\": \"PARTIAL\", \"overall_status\": \"MET\" }\n", string.Empty)
            .Replace("\"journey_evidence_url\": \"\" },\n}", "\"journey_evidence_url\": \"\" }\n}");

        var outcome = TestArtifactDerivation.TryParseTraceSummary(json, out var summary);

        Assert.Equal(TeaJsonOutcome.Parsed, outcome);
        Assert.Null(summary!.GateStatus);
        Assert.Equal(10, summary.TotalCount);
    }

    [Fact]
    public void TryParseTraceSummary_UnknownSchemaMajor_IsSkipped()
    {
        var json = TraceSummaryJson.Replace("\"schema_version\": \"0.1.0\"", "\"schema_version\": \"1.0.0\"");

        Assert.Equal(TeaJsonOutcome.UnsupportedSchema,
            TestArtifactDerivation.TryParseTraceSummary(json, out var summary));
        Assert.Null(summary);
    }

    // ---- traceability-matrix.md grammar (Task 4 / D2) ------------------------------------------------------

    /// <summary>The real <c>trace-template.md</c> grammar. NOTE the Detailed Mapping is an h4 PER CRITERION with
    /// <c>- **Coverage:**</c> / <c>- **Tests:**</c> bullets — it is NOT a table, which is what Story 18.5's own
    /// pinned table claimed before Task 1 re-fetched the template.</summary>
    private const string TraceMatrixMarkdown = """
        ---
        stepsCompleted: ['step-03-map-criteria', 'step-05-gate-decision']
        lastStep: 'step-05-gate-decision'
        lastSaved: '2026-07-20'
        workflowType: 'testarch-trace'
        coverageBasis: 'acceptance_criteria'
        oracleConfidence: 'high'
        oracleResolutionMode: 'formal_requirements'
        externalPointerStatus: 'not_used'
        ---

        # Traceability Matrix & Gate Decision - Epic 18

        ## PHASE 1: REQUIREMENTS TRACEABILITY

        ### Coverage Summary

        | Priority  | Total Criteria | FULL Coverage | Coverage % | Status   |
        | --------- | -------------- | ------------- | ---------- | -------- |
        | P0        | 4              | 4             | 100%       | PASS     |
        | P1        | 3              | 2             | 66.7%      | WARN     |
        | P2        | 2              | 1             | 50%        | WARN     |
        | P3        | 1              | 0             | 0%         | FAIL     |
        | **Total** | **10**         | **7**         | **70%**    | **WARN** |

        ### Detailed Mapping

        #### FR12: Portal renders module artifacts (P0)

        - **Coverage:** FULL ✅
        - **Tests:**
          - `18.5-E2E-001` - tests/e2e/portal.spec.ts:12
          - `18.5-UNIT-001` - tests/unit/discovery.spec.ts:8

        #### 18.4-AC-2: Ideas page omits when empty (P1)

        - **Coverage:** PARTIAL ⚠️
        - **Tests:**
          - `18.4-E2E-003` - tests/e2e/ideas.spec.ts:44

        - **Gaps:**
          - Missing: empty-state assertion

        #### JOURNEY-7: Reviewer opens the gate badge (P2)

        - **Coverage:** NONE ❌

        ### Coverage by Test Level

        | Test Level | Tests  | Criteria Covered | Coverage % |
        | ---------- | ------ | ---------------- | ---------- |
        | E2E        | 6      | 5                | 50%        |
        | API        | 4      | 3                | 30%        |
        | Component  | 2      | 2                | 20%        |
        | Unit       | 11     | 7                | 70%        |
        | **Total**  | **23** | **10**           | **100%**   |

        ## PHASE 2: QUALITY GATE DECISION

        ### GATE DECISION: CONCERNS

        ### Rationale

        All P0 criteria met; P1 coverage is below target.
        """;

    [Fact]
    public void ParseMatrix_ReadsOneRowPerDetailedMappingHeading()
    {
        var matrix = TestArtifactDerivation.ParseMatrix(TraceMatrixMarkdown);

        Assert.Equal(3, matrix.Criteria.Count);
        Assert.Equal("FR12", matrix.Criteria[0].CriterionId);
        Assert.Equal("Portal renders module artifacts", matrix.Criteria[0].Description);
        Assert.Equal("FULL", matrix.Criteria[0].CoverageStatus);
        Assert.Equal("P0", matrix.Criteria[0].Priority);
        Assert.Equal(2, matrix.Criteria[0].TestCount);

        Assert.Equal("18.4-AC-2", matrix.Criteria[1].CriterionId);
        Assert.Equal("PARTIAL", matrix.Criteria[1].CoverageStatus);
        Assert.Equal(1, matrix.Criteria[1].TestCount);

        Assert.Equal("JOURNEY-7", matrix.Criteria[2].CriterionId);
        Assert.Equal("NONE", matrix.Criteria[2].CoverageStatus);
        Assert.Equal(0, matrix.Criteria[2].TestCount);
    }

    [Fact]
    public void ParseMatrix_ReadsTheFrontmatterOracleSignals()
    {
        var matrix = TestArtifactDerivation.ParseMatrix(TraceMatrixMarkdown);

        // These five frontmatter keys carry the join-admissibility signals even when NO JSON was written —
        // an inventory-only run emits e2e-trace-summary.json but the markdown always carries them.
        Assert.Equal("acceptance_criteria", matrix.CoverageBasis);
        Assert.Equal("high", matrix.OracleConfidence);
        Assert.Equal("formal_requirements", matrix.OracleResolutionMode);
    }

    [Fact]
    public void ParseMatrix_ReadsTheGateWordFromTheH3()
    {
        var matrix = TestArtifactDerivation.ParseMatrix(TraceMatrixMarkdown);

        Assert.Equal("CONCERNS", matrix.GateStatus);
    }

    [Fact]
    public void ParseMatrix_ReadsThePrioritySummaryTable()
    {
        var matrix = TestArtifactDerivation.ParseMatrix(TraceMatrixMarkdown);

        Assert.Equal(4, matrix.PriorityBreakdown.Count);
        Assert.Equal("P1", matrix.PriorityBreakdown[1].Priority);
        Assert.Equal(3, matrix.PriorityBreakdown[1].Total);
        Assert.Equal(2, matrix.PriorityBreakdown[1].Covered);
        Assert.Equal(66.7d, matrix.PriorityBreakdown[1].Percent);
    }

    [Fact]
    public void ParseMatrix_UnfilledTemplatePlaceholders_AreNotCriteria()
    {
        // The shipped template's own headings are `#### {CRITERION_ID}: {CRITERION_DESCRIPTION} ({PRIORITY})`
        // plus two `#### Example: …` blocks. A copied-but-never-run template must yield ZERO criteria rather
        // than three fabricated ones.
        var markdown = """
            ### Detailed Mapping

            #### {CRITERION_ID}: {CRITERION_DESCRIPTION} ({PRIORITY})

            - **Coverage:** {COVERAGE_STATUS} {STATUS_ICON}

            #### Example: AC-1: User can login with email and password (P0)

            - **Coverage:** FULL ✅
            """;

        Assert.Empty(TestArtifactDerivation.ParseMatrix(markdown).Criteria);
    }

    [Fact]
    public void ParseMatrix_Garbage_ReturnsEmpty_AndNeverThrows()
    {
        var matrix = TestArtifactDerivation.ParseMatrix("not a traceability matrix at all\n\n# hello\n");

        Assert.Empty(matrix.Criteria);
        Assert.Null(matrix.GateStatus);
    }

    // ---- Join admissibility (the design rule that must NOT be crossed) -------------------------------------

    [Fact]
    public void JudgeJoin_FormalAcceptanceCriteriaAtHighConfidence_IsAdmissible()
    {
        var verdict = TestArtifactDerivation.JudgeJoin("acceptance_criteria", "high", synthetic: false);

        Assert.True(verdict.Admissible);
    }

    [Theory]
    // The oracle is not requirements at all.
    [InlineData("openapi_endpoints", "high", false)]
    [InlineData("user_journeys", "high", false)]
    // `synthetic_requirements` is a FOURTH coverage_basis value the story's pinned table omitted; Task 1's
    // re-fetch of bmad-testarch-trace/workflow.yaml found it in the `coverage_basis` enum.
    [InlineData("synthetic_requirements", "high", false)]
    // Right basis, but the oracle was inferred rather than read.
    [InlineData("acceptance_criteria", "high", true)]
    // Right basis, real oracle, but TEA itself is not confident.
    [InlineData("acceptance_criteria", "medium", false)]
    [InlineData("acceptance_criteria", "low", false)]
    // Nothing resolved at all.
    [InlineData(null, null, false)]
    public void JudgeJoin_AnythingElse_IsNotAdmissible_AndSaysWhy(string? basis, string? confidence, bool synthetic)
    {
        var verdict = TestArtifactDerivation.JudgeJoin(basis, confidence, synthetic);

        Assert.False(verdict.Admissible);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Reason));
    }

    private static readonly string[] RequirementIds = { "FR12", "NFR3", "UX-DR2" };
    private static readonly string[] StoryIds = { "18.4", "18.5", "21.1" };

    [Theory]
    // Whole-id resolution against RequirementsModel.ById.
    [InlineData("FR12", "FR12")]
    [InlineData("fr12", "FR12")]
    [InlineData("NFR3", "NFR3")]
    [InlineData("UX-DR2", "UX-DR2")]
    // Whole-id resolution against an EpicsModel story id.
    [InlineData("18.5", "18.5")]
    // A story-id PREFIX followed by a separator — the only compound form admitted, and only because the prefix
    // itself must literally match a story that exists.
    [InlineData("18.4-AC-2", "18.4")]
    [InlineData("18.4:AC-2", "18.4")]
    [InlineData("21.1 AC-9", "21.1")]
    public void ResolveJoinTarget_ResolvesOnlyAgainstIdsThatActuallyExist(string criterionId, string expected) =>
        Assert.Equal(expected, TestArtifactDerivation.ResolveJoinTarget(criterionId, RequirementIds, StoryIds));

    [Theory]
    [InlineData("AC-1")]            // the template's own example id — scoped to nothing
    [InlineData("JOURNEY-7")]       // a synthetic journey identifier
    [InlineData("FR99")]            // a requirement this repo does not have
    [InlineData("19.9-AC-1")]       // a story prefix that does not exist
    [InlineData("GET /v1/orders")]  // an OpenAPI endpoint item
    [InlineData("")]
    public void ResolveJoinTarget_AnUnresolvableOracleItem_JoinsToNothing(string criterionId) =>
        Assert.Null(TestArtifactDerivation.ResolveJoinTarget(criterionId, RequirementIds, StoryIds));

    [Fact]
    public void BuildJoin_InadmissibleBasis_ProducesNoRows_EvenWhenEveryIdWouldResolve()
    {
        // The killer case: a user_journeys run whose criterion ids happen to look like FR ids. Basis is judged
        // BEFORE any id is resolved, so not one row escapes.
        var criteria = new[]
        {
            new TeaCriterionCoverage("FR12", "looks joinable", "FULL", "P0", 2),
            new TeaCriterionCoverage("NFR3", "also looks joinable", "FULL", "P0", 1),
        };

        var join = TestArtifactDerivation.BuildJoin(
            criteria, TestArtifactDerivation.JudgeJoin("user_journeys", "high", synthetic: true),
            RequirementIds, StoryIds);

        Assert.False(join.Admissible);
        Assert.Empty(join.Rows);
        Assert.Equal(2, join.UnresolvedCount);
    }

    [Fact]
    public void BuildJoin_AdmissibleBasis_KeepsResolvedRowsAndCountsTheRest()
    {
        var criteria = new[]
        {
            new TeaCriterionCoverage("FR12", "resolves to a requirement", "FULL", "P0", 2),
            new TeaCriterionCoverage("18.4-AC-2", "resolves to a story", "PARTIAL", "P1", 1),
            new TeaCriterionCoverage("JOURNEY-7", "resolves to nothing", "NONE", "P2", 0),
        };

        var join = TestArtifactDerivation.BuildJoin(
            criteria, TestArtifactDerivation.JudgeJoin("acceptance_criteria", "high", synthetic: false),
            RequirementIds, StoryIds);

        Assert.True(join.Admissible);
        Assert.Equal(2, join.Rows.Count);
        Assert.Equal("FR12", join.Rows[0].TargetId);
        Assert.Equal("18.4", join.Rows[1].TargetId);
        Assert.Equal(1, join.UnresolvedCount);
    }
}
