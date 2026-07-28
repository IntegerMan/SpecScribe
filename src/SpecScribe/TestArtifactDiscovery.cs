namespace SpecScribe;

/// <summary>Discovers Test Architect (TEA) artifacts under the source root and projects each into a
/// <see cref="TestArtifactEntry"/> with its coverage tier. This is the IO half of the Test Artifacts surface;
/// every RULE it applies lives in the pure <see cref="TestArtifactDerivation"/> beside it — the same IO/logic split
/// <c>IdeaDiscovery</c>/<c>IdeaDerivation</c>, <c>ArtifactCoverage</c>, <c>WorkInventory</c> and
/// <c>ProgressCalculator</c> already use.
///
/// <para><b>This story is <em>interpret and label</em>, not <em>make visible</em>.</b> TEA's
/// <c>src/module.yaml</c> declares <c>test_artifacts</c> default <c>{output_folder}/test-artifacts</c>, and
/// <c>{output_folder}</c> IS <see cref="ForgeOptions.SourceDirName"/> — so in a default install TEA's markdown is
/// already inside the scanned source tree and already renders as generic pages. What was missing was interpretation:
/// a coverage tier per artifact, the gate verdict, and the two JSON files the <c>*.md</c>-only source scan
/// structurally cannot see (<c>gate-decision.json</c>, <c>e2e-trace-summary.json</c> — see ADR 0020).</para>
///
/// <para><b>Presence gates discovery, not filenames.</b> A repo with a coincidental <c>test-review.md</c> and no
/// <c>_bmad/tea/</c> install produces nothing at all. The check is Story 18.2's own
/// <see cref="ModuleContext.IsModulePresent"/> keyed on the module CODE string <c>tea</c> — never a new
/// <see cref="BmadModule"/> case, because ADR 0015 Decisions 1/2 are open-world by necessity.</para>
///
/// <para><b>Nothing here re-detects the module.</b> Story 18.2 made <see cref="ModuleContext.Detect"/>
/// once-per-run on purpose; the caller hands in the already-detected <see cref="CommandCatalog"/> so the module
/// LABEL comes from the parsed <c>module-help.csv</c> (which says "Test Architecture Enterprise", while
/// <c>module.yaml</c> says "Test Architect" — neither string may be hard-coded).</para>
///
/// <para>Never throws (AD-4 / NFR2): any failure degrades to <see cref="TestArtifactsModel.Empty"/> or drops one
/// artifact with a categorized non-fatal diagnostic, so the surface omits and generation still succeeds.</para>
/// [Story 18.5]</summary>
public static class TestArtifactDiscovery
{
    /// <summary>Scans <paramref name="sourceRoot"/> for TEA artifacts, gated on the module being installed under
    /// <paramref name="repoRoot"/>. <paramref name="diagnostics"/> collects the categorized non-fatal notices.
    /// <para>Every path here is under the SOURCE root, so every diagnostic anchors to
    /// <see cref="DiagnosticAnchorRoot.Source"/> — the default. <see cref="DiagnosticAnchorRoot.Repo"/> is Story
    /// 18.2's addition for <c>_bmad/{code}/…</c> subjects and is deliberately not reused here.</para></summary>
    public static TestArtifactsModel Discover(
        string repoRoot,
        string sourceRoot,
        List<AdapterDiagnostic>? diagnostics = null)
    {
        try
        {
            if (!ModuleContext.IsModulePresent(repoRoot, TestArtifactDerivation.ModuleCode))
            {
                return TestArtifactsModel.Empty;
            }

            if (!Directory.Exists(sourceRoot)) return TestArtifactsModel.Empty;

            var artifactsRoot = FindArtifactsRoot(sourceRoot);
            if (artifactsRoot is null)
            {
                // The module is installed but its declared output directory is not inside the scanned source tree.
                // Either `test_artifacts` was overridden to a path outside SourceRoot, or no TEA workflow has run
                // yet. Reading `_bmad/tea/config.yaml` to tell those apart is an explicit non-goal — it needs the
                // same cross-cutting config-reading decision Story 18.4 defers for `forge_output_path` — so this
                // states what is observable and stops. One Informational notice, and nothing else. [Story 18.5]
                diagnostics?.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Informational,
                    TestArtifactDerivation.ArtifactsDirName + "/",
                    $"The '{TestArtifactDerivation.ModuleCode}' module is installed but no '{TestArtifactDerivation.ArtifactsDirName}/' "
                    + "directory was found in the scanned source tree, so no test artifacts are shown. Either none have been "
                    + "produced yet, or the module's test_artifacts path points outside this tree."));
                return TestArtifactsModel.Empty;
            }

            var entries = new List<TestArtifactEntry>();
            var unmodelledFamilies = new List<string>();
            TestGateDecision? gate = null;
            TestTraceSummary? trace = null;
            var matrix = TeaMatrix.Empty;

            // Ordinal path order so a from-scratch regeneration is byte-identical, the same discipline
            // IdeaDiscovery's workspace walk uses.
            var files = SafeEnumerateFiles(artifactsRoot)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            foreach (var fullPath in files)
            {
                var sourceRelative = PathUtil.NormalizeSlashes(Path.GetRelativePath(sourceRoot, fullPath));
                var fileName = Path.GetFileName(fullPath);
                var isMarkdown = fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
                var isAdmittedJson = TestArtifactDerivation.JsonFileNames
                    .Any(n => string.Equals(n, fileName, StringComparison.OrdinalIgnoreCase));

                // ADR 0020's narrowest-thing rule: markdown as always, plus EXACTLY the two declared JSON
                // filenames inside this one directory. Never a widened `*.json` glob, and never a walk of the
                // whole tree looking for JSON.
                if (!isMarkdown && !isAdmittedJson) continue;

                var tier = TestArtifactDerivation.TierFor(fileName);
                var skill = TestArtifactDerivation.ProducingSkillFor(fileName);
                string? headline = null;

                if (isAdmittedJson)
                {
                    headline = IngestJson(fullPath, fileName, sourceRelative, diagnostics, ref gate, ref trace);
                    // A JSON that would not parse (or whose schema major is unknown) is still a DISCOVERED
                    // artifact — it is listed, tier-demoted to unsupported, and its diagnostic says why. Dropping
                    // the row would hide the file that the whole point of ADR 0020 was to stop hiding.
                    if (headline is null) tier = CoverageTier.Unsupported;
                }
                else if (string.Equals(fileName, TestArtifactDerivation.TraceMatrixFileName, StringComparison.OrdinalIgnoreCase))
                {
                    matrix = ReadMatrix(fullPath, sourceRelative, diagnostics);
                    headline = MatrixHeadline(matrix);
                    if (headline is null) tier = CoverageTier.Unsupported;
                }

                if (tier == CoverageTier.Unsupported && skill is null) unmodelledFamilies.Add(sourceRelative);

                entries.Add(new TestArtifactEntry(
                    SourceRelative: sourceRelative,
                    Title: TestArtifactDerivation.TitleFor(fileName),
                    ProducingSkill: skill,
                    Tier: tier,
                    // Only markdown gets a page — it is the generic `*.md` pass that writes it. The Test
                    // Artifacts page LINKS that page rather than re-rendering it, so no artifact is written twice.
                    OutputRelativePath: isMarkdown ? PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(sourceRelative)) : null,
                    Headline: headline));
            }

            if (entries.Count == 0) return TestArtifactsModel.Empty;

            ReportUnmodelledFamilies(unmodelledFamilies, diagnostics);

            return new TestArtifactsModel
            {
                ModuleCode = TestArtifactDerivation.ModuleCode,
                // The label comes from THIS module's own module-help.csv, never from the run's PRIMARY module.
                // In the realistic BMM+TEA repo the primary is BMad Method, so reading the primary catalog would
                // label Test Architect's artifacts "BMad Method" — the silent misattribution ADR 0015 exists to
                // prevent, and a defect this story's own generation test caught. Empty label ⇒ the surface names
                // the module by nothing rather than inventing a name (ADR 0015 Decision 2b).
                ModuleLabel = ModuleContext.ForCode(repoRoot, TestArtifactDerivation.ModuleCode)?.Commands.ModuleLabel
                    ?? string.Empty,
                Artifacts = entries,
                Gate = gate,
                Trace = trace,
                Matrix = matrix,
            };
        }
        catch (Exception)
        {
            // AD-4: an optional module-coverage provider never owns baseline success.
            return TestArtifactsModel.Empty;
        }
    }

    /// <summary>Completes the D2 join once the caller holds both the requirements model and the epics model —
    /// the only layer that does. Kept here rather than in the generator so the admissibility rule and the id
    /// resolution stay in one place, and so a caller cannot accidentally join on a basis it never judged.
    /// <para>The oracle signals are read from the JSON summary FIRST and the matrix frontmatter second: an
    /// inventory-only run writes <c>e2e-trace-summary.json</c>, but a run that only reached Phase 1 leaves the
    /// signals in the markdown's own frontmatter alone.</para></summary>
    public static TestArtifactsModel WithJoin(
        TestArtifactsModel model, RequirementsModel? requirements, EpicsModel? epics)
    {
        if (model.IsEmpty || model.Matrix.Criteria.Count == 0) return model;

        var basis = model.Trace?.InventoryBasis ?? model.Matrix.CoverageBasis;
        var confidence = model.Trace?.Confidence ?? model.Matrix.OracleConfidence;
        var synthetic = model.Trace?.SyntheticOracle ?? false;

        var requirementIds = requirements?.Everything.Select(r => r.Id).ToList() ?? new List<string>();
        var storyIds = epics?.Epics.SelectMany(e => e.Stories.Select(s => s.Id)).ToList() ?? new List<string>();

        return model with
        {
            Join = TestArtifactDerivation.BuildJoin(
                model.Matrix.Criteria,
                TestArtifactDerivation.JudgeJoin(basis, confidence, synthetic),
                requirementIds,
                storyIds),
        };
    }

    /// <summary>The module's declared output directory inside the source root, resolved by case-insensitive
    /// ENUMERATION rather than by constructing the path — the same reason <see cref="ModuleContext"/> enumerates:
    /// path construction is case-sensitive on Linux, so a <c>Test-Artifacts/</c> directory would otherwise be
    /// invisible on one platform and found on another.</summary>
    private static string? FindArtifactsRoot(string sourceRoot)
    {
        foreach (var dir in SafeEnumerateDirectories(sourceRoot))
        {
            if (string.Equals(Path.GetFileName(dir), TestArtifactDerivation.ArtifactsDirName, StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }
        }
        return null;
    }

    /// <summary>Reads one admitted JSON file into <paramref name="gate"/> or <paramref name="trace"/> and returns
    /// its one-line headline, or null when it was skipped or malformed (the diagnostic then says which).</summary>
    private static string? IngestJson(
        string fullPath,
        string fileName,
        string sourceRelative,
        List<AdapterDiagnostic>? diagnostics,
        ref TestGateDecision? gate,
        ref TestTraceSummary? trace)
    {
        string raw;
        try
        {
            raw = MarkdownConverter.ReadAllTextShared(fullPath);
        }
        catch (Exception)
        {
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Error, sourceRelative,
                $"'{fileName}' could not be read; its quality-gate signal is omitted."));
            return null;
        }

        if (string.Equals(fileName, TestArtifactDerivation.GateDecisionFileName, StringComparison.OrdinalIgnoreCase))
        {
            var outcome = TestArtifactDerivation.TryParseGateDecision(raw, out var parsed);
            if (outcome != TeaJsonOutcome.Parsed) { ReportJsonOutcome(outcome, fileName, sourceRelative, diagnostics); return null; }
            gate = parsed;
            return GateHeadline(parsed!);
        }

        var summaryOutcome = TestArtifactDerivation.TryParseTraceSummary(raw, out var summary);
        if (summaryOutcome != TeaJsonOutcome.Parsed) { ReportJsonOutcome(summaryOutcome, fileName, sourceRelative, diagnostics); return null; }
        trace = summary;
        return TraceHeadline(summary!);
    }

    /// <summary>Maps a JSON read outcome onto the CLOSED five-value <see cref="AdapterDiagnosticCategory"/> — an
    /// unknown schema major is <see cref="AdapterDiagnosticCategory.Skipped"/> (deliberately not ingested), a
    /// broken file is <see cref="AdapterDiagnosticCategory.Malformed"/>. No sixth category is invented.</summary>
    private static void ReportJsonOutcome(
        TeaJsonOutcome outcome, string fileName, string sourceRelative, List<AdapterDiagnostic>? diagnostics)
    {
        if (diagnostics is null) return;

        diagnostics.Add(outcome == TeaJsonOutcome.UnsupportedSchema
            ? new AdapterDiagnostic(
                AdapterDiagnosticCategory.Skipped, sourceRelative,
                $"'{fileName}' declares a schema version this build does not understand, so it was not read; "
                + "it is listed as discovered but uninterpreted rather than parsed on a guess.")
            : new AdapterDiagnostic(
                AdapterDiagnosticCategory.Malformed, sourceRelative,
                $"'{fileName}' is present but could not be parsed; its quality-gate signal is omitted."));
    }

    private static TeaMatrix ReadMatrix(string fullPath, string sourceRelative, List<AdapterDiagnostic>? diagnostics)
    {
        string raw;
        try
        {
            raw = MarkdownConverter.ReadAllTextShared(fullPath);
        }
        catch (Exception)
        {
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Error, sourceRelative,
                $"'{TestArtifactDerivation.TraceMatrixFileName}' could not be read; its coverage figures are omitted."));
            return TeaMatrix.Empty;
        }

        var matrix = TestArtifactDerivation.ParseMatrix(raw);
        if (matrix.Criteria.Count == 0 && matrix.PriorityBreakdown.Count == 0 && matrix.GateStatus is null)
        {
            // Discovered and recognized by name, but not in a shape this reader interprets — the textbook
            // Unsupported case. The document still renders in full on its own page.
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported, sourceRelative,
                $"'{TestArtifactDerivation.TraceMatrixFileName}' does not follow the traceability-matrix structure, "
                + "so no coverage figures were extracted; the document itself still renders in full."));
        }

        return matrix;
    }

    /// <summary>One aggregated <see cref="AdapterDiagnosticCategory.Unsupported"/> notice naming the artifact
    /// families SpecScribe does not model (<c>bmad-teach-me-testing</c>'s progress file / session notes /
    /// certificate are unpinned upstream, and anything else a module drops in this directory). Aggregated rather
    /// than one-per-file so a learning-mode repo does not bury its real notices.</summary>
    private static void ReportUnmodelledFamilies(IReadOnlyList<string> paths, List<AdapterDiagnostic>? diagnostics)
    {
        if (diagnostics is null || paths.Count == 0) return;

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Unsupported,
            paths[0],
            paths.Count == 1
                ? "This test artifact is not one SpecScribe models, so it is listed as unsupported and nothing is claimed about its contents."
                : $"{paths.Count} test artifacts are not ones SpecScribe models, so they are listed as unsupported and nothing is claimed about their contents."));
    }

    // ---- Headlines: the one line a Summarized artifact contributes -----------------------------------------

    private static string GateHeadline(TestGateDecision gate)
    {
        var parts = new List<string> { $"Gate {gate.Status}" };
        // The module writes SCREAMING_SNAKE status words (MET / NOT_MET / PARTIAL). Lower-casing alone leaks
        // "not_met" into prose, so the underscore is spoken too. [caught in live-browser verification]
        if (gate.P0Status is { Length: > 0 } p0) parts.Add($"P0 {Spoken(p0)}");
        if (gate.P1Status is { Length: > 0 } p1) parts.Add($"P1 {Spoken(p1)}");
        if (gate.CriticalOpen is { } critical && critical > 0)
        {
            parts.Add($"{critical} critical {Charts.Plural(critical, "gap", "gaps")} open");
        }
        return string.Join(" · ", parts);
    }

    /// <summary>A module's SCREAMING_SNAKE status word as prose: <c>NOT_MET</c> → <c>not met</c>. Changes how the
    /// word is spelled, never which word it is.</summary>
    private static string Spoken(string statusWord) =>
        statusWord.Trim().Replace('_', ' ').ToLowerInvariant();

    private static string TraceHeadline(TestTraceSummary trace)
    {
        var parts = new List<string>();
        if (trace is { CoveredCount: { } covered, TotalCount: { } total } && total > 0)
        {
            parts.Add($"{covered}/{total} oracle items covered");
        }
        if (trace.TestCases is { } cases && cases > 0)
        {
            parts.Add($"{cases} test {Charts.Plural(cases, "case", "cases")}");
        }
        if (trace.InventoryBasis is { Length: > 0 } basis) parts.Add($"basis: {basis.Replace('_', ' ')}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Machine-readable trace summary";
    }

    private static string? MatrixHeadline(TeaMatrix matrix)
    {
        var parts = new List<string>();
        if (matrix.GateStatus is { Length: > 0 } gate) parts.Add($"Gate {gate}");

        var total = matrix.PriorityBreakdown.Sum(p => p.Total);
        var covered = matrix.PriorityBreakdown.Sum(p => p.Covered);
        if (total > 0) parts.Add($"{covered}/{total} criteria fully covered");
        else if (matrix.Criteria.Count > 0) parts.Add($"{matrix.Criteria.Count} criteria mapped");

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    // ---- Safe enumeration (NFR2) ---------------------------------------------------------------------------

    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try { return Directory.EnumerateDirectories(root).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }
}
