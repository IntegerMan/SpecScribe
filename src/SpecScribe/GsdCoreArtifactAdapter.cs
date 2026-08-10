using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The GSD Core (<i>Get Shit Done</i>) <see cref="IArtifactAdapter"/> — the SECOND framework surface, and
/// the first non-BMad one. GSD Core keeps every artifact as plain Markdown and JSON under a <c>.planning/</c>
/// directory and decomposes work as Milestone → Phase → Plan, with no database: what is on disk is the project.
///
/// <para><b>⚠️ <c>gsd</c> is not <c>gds</c>.</b> <c>gds</c> is BMad GDS (Game Dev Studio), an installable BMad
/// module under <c>_bmad/gds</c> that rides <see cref="BmadArtifactAdapter"/> and is already fully supported. This
/// class is GSD Core, keyed on <c>.planning/</c>. The two are near-anagrams and have been conflated before.</para>
///
/// <para><b>Level mapping (owner decision D1).</b> Phase → <see cref="EpicInfo"/>, Plan
/// (<c>NN-YY-PLAN.md</c>) → <see cref="StoryInfo"/>, Task → nothing, Milestone → a band on the epics index
/// (<see cref="EpicsModel.Milestones"/>), not a third hierarchy level.</para>
///
/// <para><b>Three things this adapter deliberately does NOT do</b>, each because the live reference repository
/// contradicted the documentation-derived plan:</para>
/// <list type="number">
/// <item><b>It never counts <c>- [x]</c> inside a <c>PLAN.md</c>.</b> Across 58 plans in the reference repo there
/// are ZERO checked boxes and 39 unchecked ones — every one a <c>## Verification</c> box left unchecked on a plan
/// whose work is finished. <see cref="TaskListParser"/> therefore returns 0/0 for every GSD plan, and the tally
/// badge is suppressed at <c>total == 0</c>, which is the honest outcome. Tasks live in <c>&lt;task&gt;</c> XML
/// blocks; synthesizing a tally from them is explicitly out of scope.</item>
/// <item><b>It never reads identity from plan frontmatter.</b> The <c>phase:</c> key takes EIGHT different
/// encodings in one repo (<c>01-identity-scope-and-boundaries</c>, <c>"02.1"</c>, <c>"4.5"</c>, <c>"5"</c>,
/// <c>"06"</c>, bare <c>4</c>) and only 17 of 58 files carry an <c>id:</c> at all. The FILENAME is the only stable
/// key, so <see cref="PlanFilePattern"/> is the identity source.</item>
/// <item><b>It never lets a finished plan render as drafted.</b> No GSD plan carries a <c>Status:</c> line or a
/// <c>status:</c> frontmatter key, so <see cref="EpicsParser.ExtractStatus"/> yields null for all of them. Combined
/// with the 0/0 tally that would render every completed plan as a drafted story with no task plan — precisely the
/// defect class <see cref="BmadArtifactAdapter"/>'s artifact-map doc comment already warns about. Status comes from
/// ROADMAP's per-plan checkbox instead, and <see cref="ProgressCalculator"/> was taught not to overwrite a status
/// an adapter had already established.</item>
/// </list>
///
/// <para><b>Requirements stay null (owner decision D3).</b> <c>REQUIREMENTS.md</c> renders as a document through
/// the generic <c>*.md</c> pass. Its ids are project-defined prefixes — <c>CONV-01</c>, <c>CAP-01</c>,
/// <c>GADM-01</c>, twelve distinct prefixes in one repo, none of them <c>REQ</c> — so the set is OPEN and cannot be
/// enumerated into <c>RequirementKind</c>, whose <c>Id</c> throws on an unmodeled kind. Mapping <c>CONV-01</c> onto
/// <c>FR1</c> would render an id GSD never wrote (NFR8).</para>
///
/// <para><b>Retros stay empty, with no diagnostic.</b> <c>RetroModel</c> requires participants and an
/// <c>## Action Items</c> table, and <see cref="EpicInfo.HasRetrospective"/> gates the "In review → finished" tier
/// on every visual surface — so forcing a <c>-SUMMARY.md</c> into a retro would silently mark phases closed out on
/// the strength of a build log. Honest absence.</para>
///
/// <para><b>The module ceiling.</b> <see cref="ArtifactBundle.Module"/> is <c>required</c> but
/// <see cref="ModuleContext"/> is BMad-typed to the bone (a closed <c>BmadModule</c> enum keyed on
/// <c>_bmad/{code}/</c>), so this adapter can only return <see cref="ModuleContext.None"/>. Its workflow catalog
/// is deliberately separate: installed definitions under <c>.claude/commands/gsd/</c> populate GSD next-step
/// prompts without fabricating BMad module documentation or glossary metadata.</para>
/// [Story 12.2; ADR 0038]</summary>
public sealed class GsdCoreArtifactAdapter : IArtifactAdapter
{
    /// <summary>GSD Core's install marker and artifact root: a <c>.planning/</c> directory at the repo root. The ONE
    /// home for this literal — <see cref="ForgeOptions.SourceDirNames"/> probes it as a source-root marker and
    /// <see cref="AppliesTo"/> sniffs it as the self-selection signal, rather than either re-hardcoding the string
    /// (NFR4).</summary>
    public const string MarkerDirName = ".planning";

    /// <summary>The roadmap — GSD Core's epics source. Phases, their plans, and the milestone bands all come from
    /// this one file, and its per-plan checkbox is the authoritative completion signal (see
    /// <see cref="ReconcileCompletionSignals"/>).</summary>
    public const string RoadmapFileName = "ROADMAP.md";

    /// <summary>Live project state — the sprint projection's source (YAML frontmatter only).</summary>
    public const string StateFileName = "STATE.md";

    /// <summary>The phase directories' parent. Plans live at <c>.planning/phases/NN-slug/NN-YY-PLAN.md</c>.</summary>
    public const string PhasesDirName = "phases";

    /// <summary>GSD's machine config. Non-markdown and outside the <c>*.md</c> source scan; ADR 0020's gate for
    /// reading a non-markdown source is <c>ModuleContext.IsModulePresent</c>, which is BMad-keyed, so this file
    /// meets none of the machinery today. Reported as a boundary, not read.</summary>
    public const string ConfigFileName = "config.json";

    private static readonly IReadOnlyDictionary<string, string> WorkflowCommandFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["discuss-phase"] = "discuss-phase",
            ["ui-phase"] = "ui-phase",
            ["research-phase"] = "research-phase",
            ["create-epics-and-stories"] = "plan-phase",
            ["create-story"] = "plan-phase",
            ["dev-story"] = "execute-phase",
            ["code-review"] = "code-review",
            ["sprint-status"] = "progress",
            ["sprint-planning"] = "progress",
        };

    // ---- ROADMAP.md grammar -------------------------------------------------------------------------------------
    // Every pattern below tolerates the inconsistencies the live reference repo actually contains: decimal and
    // zero-padded phase numbers, em-dash AND hyphen separators on the same kind of line, and `(INSERTED)`/
    // `(BACKLOG)` suffixes on detail headings.

    private static readonly Regex MilestoneOverviewHeading = TimedRegex.New(
        @"^###\s+Milestone:\s*(?<name>.+?)\s*(?:\(completed\s+(?<date>[^)]+)\))?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PhaseDetailsSectionHeading = TimedRegex.New(
        @"^##\s+Milestone:\s*(?<name>.+?)\s+[—–-]\s+Phase\s+Details\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PhaseOverviewLine = TimedRegex.New(
        @"^\s*-\s*\[(?<mark>[ xX])\]\s*\*\*Phase\s+(?<num>\d+(?:\.\d+)?)\s*:\s*(?<title>.+?)\*\*\s*(?:[—–-]\s*(?<desc>.*?))?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PhaseDetailHeading = TimedRegex.New(
        @"^###\s+Phase\s+(?<num>\d+(?:\.\d+)?)\s*:\s*(?<title>.+?)\s*$",
        RegexOptions.Compiled);

    /// <summary>A plan line under a phase's <c>Plans:</c> block. Decimal-tolerant by design, unlike
    /// <c>BmadArtifactAdapter.ArtifactFilenamePattern</c> (<c>^(?&lt;epic&gt;\d+)-(?&lt;story&gt;\d+)-</c>), which
    /// does not match <c>02.1-01-PLAN.md</c>. Both the em-dash and the hyphen separator occur in the same file.
    /// Backlog placeholders (<c>- [ ] TBD (promote with …)</c>) deliberately do not match: they name no plan file,
    /// so they are not plans.</summary>
    private static readonly Regex PlanLine = TimedRegex.New(
        @"^\s*-\s*\[(?<mark>[ xX])\]\s*(?<file>(?<phase>\d+(?:\.\d+)?)-(?<plan>\d+)-PLAN\.md)\s*(?:[—–-]\s*(?<desc>.*?))?\s*$",
        RegexOptions.Compiled);

    /// <summary>The plan/summary filename grammar, used to identify files on disk (never frontmatter).</summary>
    private static readonly Regex PlanFilePattern = TimedRegex.New(
        @"^(?<phase>\d+(?:\.\d+)?)-(?<plan>\d+)-PLAN\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SummaryFilePattern = TimedRegex.New(
        @"^(?<phase>\d+(?:\.\d+)?)-(?<plan>\d+)-SUMMARY\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ContextFilePattern = TimedRegex.New(
        @"^(?<phase>\d+(?:\.\d+)?)-CONTEXT\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DiscussionLogFilePattern = TimedRegex.New(
        @"^(?<phase>\d+(?:\.\d+)?)-DISCUSSION-LOG\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UiSpecFilePattern = TimedRegex.New(
        @"^(?<phase>\d+(?:\.\d+)?)-UI-SPEC\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GoalLine = TimedRegex.New(@"^\*\*Goal:?\*\*:?\s*(?<v>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex RequirementsLine = TimedRegex.New(@"^\*\*Requirements:?\*\*:?\s*(?<v>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex CompletedSuffix = TimedRegex.New(@"\s*\(completed\s+(?<date>[^)]+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex PhaseMarkerSuffix = TimedRegex.New(@"\s*\((?:INSERTED|BACKLOG)\)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingPhaseNumber = TimedRegex.New(@"^(?<num>\d+(?:\.\d+)?)-", RegexOptions.Compiled);

    /// <summary>The band name for phases GSD lists under <c>## Backlog</c> — a peer of the milestone groups in
    /// <c>## Phases</c>, carrying GSD's own word rather than an invented one.</summary>
    private const string BacklogBandName = "Backlog";

    /// <summary>GSD Core's self-selection signal: a <c>.planning/</c> directory at the repo root, mirroring how
    /// <see cref="BmadArtifactAdapter.AppliesTo"/> sniffs <c>_bmad/</c>. Cheap (one directory probe, no parse) and
    /// never throws, as the contract requires.</summary>
    public bool AppliesTo(ForgeOptions options, IReadOnlyList<string> sourceFiles)
    {
        try
        {
            return Directory.Exists(Path.Combine(options.RepoRoot, MarkerDirName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public ArtifactBundle Ingest(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress)
    {
        var diagnostics = new List<AdapterDiagnostic>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var epics = IngestEpics(options, sourceFiles, projectProgress);
        foreach (var rel in epics.ConsumedSourceRelatives) consumed.Add(rel);
        diagnostics.AddRange(epics.Diagnostics);

        var sprint = IngestSprint(options, epics.Epics, diagnostics);
        ReportUnsupportedArtifacts(options, diagnostics);

        return new ArtifactBundle
        {
            // Gap 3: ModuleContext is BMad-typed (closed enum keyed on `_bmad/{code}/`), so a GSD repo can only
            // ever be None. Stated on the framework page as a ceiling rather than left looking unfinished.
            Module = ModuleContext.None,
            WorkflowCommands = DiscoverWorkflowCommands(options),
            Sprint = sprint,
            // Honest absence, no diagnostic — see the class remarks.
            Retros = Array.Empty<RetroModel>(),
            Epics = epics.Epics,
            // Owner decision D3. Never a fabricated FR id.
            Requirements = null,
            EpicsSourceFullPath = epics.SourceFullPath,
            StoryArtifactsById = epics.StoryArtifactsById,
            ConsumedSourceRelatives = consumed,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>The epics-scoped slice: <c>ROADMAP.md</c> → <see cref="EpicsModel"/> plus the plan artifact map.
    /// GSD Core projects no requirements (D3), so that leg of BMad's epics → progress → requirements chain is
    /// simply absent; the <paramref name="projectProgress"/> callback still runs so the projection layer's
    /// task/story roll-up and git pulse are computed exactly as they are for BMad (AD-4).</summary>
    public EpicsIngest IngestEpics(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress)
    {
        // Defensive re-filter, matching BmadArtifactAdapter: ignored working files are neither ingested nor
        // diagnosed, wherever discovery happens.
        var files = sourceFiles.Where(f => !PathUtil.IsIgnoredSourceFile(f)).ToList();
        var diagnostics = new List<AdapterDiagnostic>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifactMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var planningRoot = ResolvePlanningRoot(options, diagnostics);
        if (planningRoot is null) return new EpicsIngest(null, null, null, artifactMap, consumed, diagnostics);

        var roadmapPath = Path.Combine(planningRoot, RoadmapFileName);
        if (!File.Exists(roadmapPath))
        {
            // Absent → graceful omission with no diagnostic, exactly as BMad treats a missing epics.md.
            return new EpicsIngest(null, null, null, artifactMap, consumed, diagnostics);
        }

        var roadmapRel = ToSourceRelative(options, roadmapPath);
        EpicsModel? model = null;
        try
        {
            var raw = MarkdownConverter.ReadAllTextShared(roadmapPath);
            var roadmap = RoadmapParser.Parse(raw);
            if (roadmap.Phases.Count == 0)
            {
                diagnostics.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Malformed, roadmapRel,
                    $"'{RoadmapFileName}' was read but no phase entries were recognized; "
                    + "epics, stories and milestone bands are omitted for this framework"));
            }
            else
            {
                model = BuildEpicsModel(options, planningRoot, roadmap, files, artifactMap, consumed, diagnostics);
                ReconcileCompletionSignals(options, planningRoot, roadmap, roadmapRel, diagnostics);
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new AdapterDiagnostic(AdapterDiagnosticCategory.Malformed, roadmapRel, ex.Message));
        }

        if (model is not null)
        {
            projectProgress?.Invoke(model, artifactMap);
        }

        // SourceFullPath is set whenever the file was FOUND, independent of parse success — the same contract BMad
        // follows, so the generic-pages pass keeps excluding it either way.
        return new EpicsIngest(roadmapPath, model, null, artifactMap, consumed, diagnostics);
    }

    /// <summary>The <c>.planning/</c> directory, but ONLY when it actually sits inside
    /// <see cref="ForgeOptions.SourceRoot"/>.
    ///
    /// <para><b>Why the containment check is load-bearing.</b> <c>SourceRoot</c> is single-valued and anchors both
    /// the <c>*.md</c> enumeration and every source-relative path
    /// (<c>Path.GetRelativePath(SourceRoot, fullPath)</c>). If a repo resolved its primary source root to another
    /// framework's marker, <c>.planning/ROADMAP.md</c> would relativize to <c>..\.planning\ROADMAP.md</c>, which
    /// <see cref="PathUtil.EscapesRepoRoot"/> exists to reject — so every path this adapter produced would be
    /// unusable and every plan page would be a dead link. Rather than improvise a second path scheme (multi-rooted
    /// source discovery is Story 4.9 AC #2, explicitly not this story's), the adapter contributes NOTHING but one
    /// <see cref="AdapterDiagnosticCategory.Informational"/> notice saying so. Absent, not broken (NFR8).</para></summary>
    private static string? ResolvePlanningRoot(ForgeOptions options, List<AdapterDiagnostic> diagnostics)
    {
        var planningRoot = Path.Combine(options.RepoRoot, MarkerDirName);
        if (!Directory.Exists(planningRoot)) return null;

        var rel = PathUtil.NormalizeSlashes(Path.GetRelativePath(options.SourceRoot, planningRoot));
        if (!PathUtil.EscapesRepoRoot(rel) && rel != ".") return planningRoot;
        if (rel == ".") return planningRoot;

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational,
            MarkerDirName + "/",
            $"GSD Core artifacts were detected at '{MarkerDirName}/' but that directory is outside this run's source "
            + "root, so its paths cannot be expressed relative to it; GSD epics, stories and sprint state are omitted. "
            + $"Point --source at '{MarkerDirName}' to render them.",
            DiagnosticAnchorRoot.Repo));
        return null;
    }

    // ---- ROADMAP.md → EpicsModel (Tasks 5, 6, 8) ----------------------------------------------------------------

    /// <summary>Projects the parsed roadmap onto the shared epics model, assigning owner decision D2's SYNTHETIC
    /// SEQUENTIAL ORDINAL to <see cref="EpicInfo.Number"/> in roadmap order and carrying the real label
    /// (<c>Phase 2.1: UI Foundation and Style System</c>) in <see cref="EpicInfo.Title"/>.
    ///
    /// <para><b>Why an ordinal and not the real number.</b> <see cref="EpicInfo.Number"/> is an <c>int</c>, and GSD
    /// phase numbers are decimal in practice — the reference repo's eight shipped v1 phases include <c>2.1</c> and
    /// <c>4.5</c>, and its entire backlog is <c>999.1</c>/<c>.2</c>/<c>.3</c>. Widening the field to a string would
    /// touch the sunburst, donut, sprint grouping, requirement roll-up, <see cref="StatusStyles"/>, the work graph
    /// and the IR schema — its own story — and skipping decimal phases would drop a quarter of the shipped work.
    /// The ordinal keeps the <c>"N.M"</c> story-id contract intact while the human-facing label stays exact.</para></summary>
    private static EpicsModel BuildEpicsModel(
        ForgeOptions options,
        string planningRoot,
        RoadmapModel roadmap,
        IReadOnlyList<string> sourceFiles,
        Dictionary<string, string> artifactMap,
        HashSet<string> consumed,
        List<AdapterDiagnostic> diagnostics)
    {
        var planFiles = IndexPhaseFiles(planningRoot, PlanFilePattern);
        var summaryFiles = IndexPhaseFiles(planningRoot, SummaryFilePattern);
        var epics = new List<EpicInfo>();

        for (var i = 0; i < roadmap.Phases.Count; i++)
        {
            var phase = roadmap.Phases[i];
            var ordinal = i + 1; // D2's synthetic sequential ordinal, in roadmap order.
            var stories = new List<StoryInfo>();
            var companionFiles = FindPhaseCompanionFiles(planningRoot, phase);

            foreach (var plan in phase.Plans)
            {
                var id = $"{ordinal}.{plan.Number}";
                if (stories.Any(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add(new AdapterDiagnostic(
                        AdapterDiagnosticCategory.Skipped, ToSourceRelative(options, planningRoot, plan.FileName),
                        $"Plan '{plan.FileName}' collides with an already-ingested plan on story id {id}; "
                        + "the first listed plan was kept"));
                    continue;
                }

                stories.Add(new StoryInfo
                {
                    Id = id,
                    EpicNumber = ordinal,
                    WorkflowCommandArgument = phase.Number,
                    Title = MarkdownConverter.RenderInline(
                        plan.Description is { Length: > 0 } d ? d : Path.GetFileNameWithoutExtension(plan.FileName)),
                    // GSD plans carry no As-a/I-want narrative and no gherkin ACs. Honest absence: the card simply
                    // renders without a blurb rather than showing invented prose.
                    UserStoryHtml = string.Empty,
                    AcBlocksHtml = Array.Empty<string>(),
                    // Finding #8: derived from ROADMAP's checkbox, NOT from the plan file — which has neither a
                    // `Status:` line nor a `status:` frontmatter key, so ExtractStatus yields null for all 58.
                    // Both words map cleanly through StatusStyles.ForStatus with no new vocabulary.
                    Status = plan.Done ? "done" : "drafted",
                });

                // The plan file is the story's detail artifact. Discovered by FILENAME (finding #5).
                if (planFiles.TryGetValue(plan.FileName, out var planFullPath))
                {
                    artifactMap[id] = planFullPath;
                    stories[^1].ArtifactOutputPath = $"epics/story-{id.Replace('.', '-')}.html";
                    consumed.Add(ToSourceRelative(options, planFullPath));
                }

                // The sibling summary is a COMPANION, never the story artifact — and never a retro. It is
                // deliberately NOT consumed: `ConsumedSourceRelatives` means "projected onto a dedicated surface",
                // and the summary is not, so leaving it out is what keeps its own generated page (coverage tier
                // Rendered). The Skipped notice records that the plan won the story-artifact slot.
                var summaryName = plan.FileName.Replace("-PLAN.md", "-SUMMARY.md", StringComparison.OrdinalIgnoreCase);
                if (summaryFiles.ContainsKey(summaryName))
                {
                    diagnostics.Add(new AdapterDiagnostic(
                        AdapterDiagnosticCategory.Skipped, ToSourceRelative(options, summaryFiles[summaryName]),
                        $"'{summaryName}' is a completion summary companion to '{plan.FileName}', which was ingested "
                        + $"as story {id}'s artifact; the summary still renders as its own page"));
                }
            }

            epics.Add(new EpicInfo
            {
                Number = ordinal,
                WorkflowCommandArgument = phase.Number,
                // The REAL label, so nothing about the ordinal hides which phase this is.
                Title = MarkdownConverter.RenderInline($"Phase {phase.Number}: {phase.Title}"),
                GoalHtml = phase.Goal is { Length: > 0 } g ? MarkdownConverter.RenderInline(g) : string.Empty,
                // GSD's per-phase requirement map, shown verbatim. D3 keeps it OUT of the requirements model (no
                // fabricated ids); rendering the line it actually wrote costs nothing and hides nothing.
                FrMetaHtml = phase.Requirements is { Length: > 0 } r
                    ? $"<strong>Requirements:</strong> {MarkdownConverter.RenderInline(r)}"
                    : null,
                PhaseContextHtml = ReadPlanningContext(companionFiles.ContextPath, companionFiles.UiSpecPath),
                HasDiscussionLog = companionFiles.DiscussionLogPath is not null,
                HasUiPlan = companionFiles.UiSpecPath is not null,
                // Mirrors EpicsParser's own rule (`stories.Count > 0 ? Drafted : Pending`) rather than inventing a
                // GSD-specific one — a phase with no plans listed is genuinely pending.
                Status = stories.Count > 0 ? EpicStatus.Drafted : EpicStatus.Pending,
                // GSD Core has no retrospective artifact or close-out workflow. Its ROADMAP completion checkbox
                // is the terminal signal, so a completed phase must not remain in review forever.
                RequiresRetrospective = false,
                // SEMANTICALLY EMPTY for this framework. EpicSection encodes BMad's epics.md "Vertical Slice" vs
                // "Further Development" split, which GSD Core has no analog for; every phase takes the same constant
                // so the value can never imply a distinction GSD did not make. The epics index renders MILESTONE
                // bands instead of these chip sections whenever Milestones is non-empty, so the unused label is
                // never shown to a reader.
                Section = EpicSection.VerticalSlice,
                Stories = stories,
            });
        }

        return new EpicsModel
        {
            OverviewHtml = roadmap.OverviewHtml,
            // D3: no requirements inventory is synthesized. REQUIREMENTS.md renders as its own document.
            RequirementsInventoryHtml = string.Empty,
            Epics = epics,
            Milestones = BuildMilestones(roadmap),
        };
    }

    /// <summary>Finds GSD's phase-local context and discussion artifacts by their documented filename grammar.
    /// The numeric prefix is matched by decimal value, preserving GSD's tolerance for zero-padded and decimal
    /// phase identifiers without relying on a directory slug or a frontmatter field.</summary>
    private static (string? ContextPath, string? DiscussionLogPath, string? UiSpecPath) FindPhaseCompanionFiles(
        string planningRoot, RoadmapPhase phase)
    {
        var phasesRoot = Path.Combine(planningRoot, PhasesDirName);
        if (!Directory.Exists(phasesRoot)) return (null, null, null);

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(phasesRoot, "*", SearchOption.TopDirectoryOnly))
            {
                var prefix = LeadingPhaseNumber.Match(Path.GetFileName(directory));
                if (!prefix.Success || !decimal.TryParse(prefix.Groups["num"].Value, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var number) || number != phase.SortKey)
                    continue;

                string? context = null;
                string? discussion = null;
                string? uiSpec = null;
                foreach (var path in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(path);
                    if (ContextFilePattern.IsMatch(fileName)) context = path;
                    if (DiscussionLogFilePattern.IsMatch(fileName)) discussion = path;
                    if (UiSpecFilePattern.IsMatch(fileName)) uiSpec = path;
                }
                return (context, discussion, uiSpec);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Companion details are optional; retain the roadmap projection if they cannot be read.
        }

        return (null, null, null);
    }

    /// <summary>Promotes only the context sections that describe the phase's active scope and locked planning
    /// decisions. Reference inventories, implementation notes, and deferred ideas remain on the source document
    /// instead of overwhelming the phase detail page.</summary>
    private static string ReadPlanningContext(string? contextPath, string? uiSpecPath)
    {
        var sections = new List<string>();

        try
        {
            if (contextPath is not null)
            {
                var context = SelectSections(MarkdownConverter.ReadAllTextShared(contextPath),
                    "Phase Boundary", "Implementation Decisions");
                if (context.Length > 0) sections.Add(context);
            }
            if (uiSpecPath is not null)
            {
                var uiPlan = SelectSections(MarkdownConverter.ReadAllTextShared(uiSpecPath),
                    "0. Scope and Inheritance", "6. Routes", "7. Component Inventory");
                if (uiPlan.Length > 0) sections.Add(uiPlan);
            }
            return sections.Count > 0 ? MarkdownConverter.RenderBlock(string.Join("\n\n", sections)) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string SelectSections(string raw, params string[] selectedHeadings)
    {
        var selected = new HashSet<string>(selectedHeadings, StringComparer.OrdinalIgnoreCase);
        var output = new List<string>();
        var include = false;
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
                include = selected.Contains(line[3..].Trim());
            if (include) output.Add(line);
        }
        return string.Join("\n", output).Trim();
    }

    /// <summary>Discovers the GSD Core workflow commands actually installed in the repository. A missing command
    /// file removes only its matching suggestion; it never falls back to a co-installed BMad command.</summary>
    private static CommandCatalog DiscoverWorkflowCommands(ForgeOptions options)
    {
        var commandsRoot = Path.Combine(options.RepoRoot, ".claude", "commands", "gsd");
        var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(commandsRoot))
        {
            foreach (var (step, fileStem) in WorkflowCommandFiles)
            {
                if (File.Exists(Path.Combine(commandsRoot, fileStem + ".md")))
                    commands[step] = "/gsd:" + fileStem;
            }
        }

        return new CommandCatalog("GSD Core", commands, usesPhaseArguments: true);
    }

    /// <summary>The milestone bands (AC #4), in roadmap order, each naming the phase ordinals banded under it.
    /// <para>The state word is CANONICAL, mapped here rather than left to a surface: GSD's own words are
    /// <c>Complete</c> and <c>Not started</c>, and <see cref="StatusStyles.ForStatus"/> has no arm for
    /// <c>"not started"</c> — it would fall through to <c>unrecognized</c> and render a hatched neutral badge on a
    /// milestone that is simply not begun. Mapping in the adapter is exactly where the native-vocabulary seam
    /// says this belongs.</para></summary>
    private static IReadOnlyList<MilestoneInfo> BuildMilestones(RoadmapModel roadmap)
    {
        var bands = new List<MilestoneInfo>();
        foreach (var band in roadmap.Milestones)
        {
            var numbers = new List<int>();
            var anyDone = false;
            var allDone = true;
            for (var i = 0; i < roadmap.Phases.Count; i++)
            {
                if (!string.Equals(roadmap.Phases[i].Milestone, band.Name, StringComparison.Ordinal)) continue;
                numbers.Add(i + 1);
                var phase = roadmap.Phases[i];
                var done = phase.Plans.Count > 0 && phase.Plans.All(p => p.Done);
                anyDone |= done || phase.Plans.Any(p => p.Done);
                allDone &= done;
            }

            var word =
                band.CompletedDate is { Length: > 0 } ? "done"
                : numbers.Count > 0 && allDone ? "done"
                : anyDone ? "in-progress"
                : "drafted";

            bands.Add(new MilestoneInfo(band.Name, word, band.CompletedDate, numbers));
        }
        return bands;
    }

    /// <summary>Every <c>NN-YY-PLAN.md</c> / <c>NN-YY-SUMMARY.md</c> under <c>.planning/phases/</c>, keyed by
    /// filename. Discovery is IO-safe and location-tolerant within the phases tree; ignored working files are
    /// neither indexed nor diagnosed.</summary>
    private static Dictionary<string, string> IndexPhaseFiles(string planningRoot, Regex pattern)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var phasesRoot = Path.Combine(planningRoot, PhasesDirName);
        if (!Directory.Exists(phasesRoot)) return map;

        try
        {
            foreach (var path in Directory.EnumerateFiles(phasesRoot, "*.md", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            }))
            {
                if (PathUtil.IsIgnoredSourceFile(path)) continue;
                var name = Path.GetFileName(path);
                if (!pattern.IsMatch(name)) continue;
                // Deterministic first-wins on the (pathological) duplicate-filename-in-two-phase-dirs case.
                map.TryAdd(name, path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Degrade to what was indexed before the failure rather than aborting ingest.
        }
        return map;
    }

    /// <summary>Finding #7: THREE completion signals disagree in the reference repo, and this adapter must not
    /// silently reconcile them. ROADMAP marks 58 of 58 plans <c>[x]</c>; <c>STATE.md</c>'s frontmatter says
    /// <c>completed_plans: 42</c> of <c>total_plans: 50</c>; and 42 <c>-SUMMARY.md</c> files exist on disk, with 16
    /// <c>[x]</c> plans carrying none.
    ///
    /// <para><b>ROADMAP's per-plan checkbox is authoritative</b> — it is the only per-plan signal that exists for
    /// every plan. The other two are reported, once, as an <see cref="AdapterDiagnosticCategory.Informational"/>
    /// notice naming all three counts. Nothing is averaged, and SUMMARY-presence never quietly overrides a declared
    /// <c>[x]</c>: doing either would produce a confidently wrong number with no way for a reader to see the
    /// disagreement.</para></summary>
    private static void ReconcileCompletionSignals(
        ForgeOptions options, string planningRoot, RoadmapModel roadmap, string roadmapRel, List<AdapterDiagnostic> diagnostics)
    {
        var allPlans = roadmap.Phases.SelectMany(p => p.Plans).ToList();
        if (allPlans.Count == 0) return;

        var roadmapDone = allPlans.Count(p => p.Done);
        var summaryCount = IndexPhaseFiles(planningRoot, SummaryFilePattern).Count;
        var state = TryReadStateProgress(planningRoot);

        var disagreements = new List<string>();
        if (state is { } s && (s.CompletedPlans != roadmapDone || s.TotalPlans != allPlans.Count))
        {
            disagreements.Add(
                $"{StateFileName} reports {s.CompletedPlans}/{s.TotalPlans} plans complete");
        }
        if (summaryCount != roadmapDone)
        {
            disagreements.Add($"{summaryCount} '-SUMMARY.md' file(s) exist on disk");
        }
        if (disagreements.Count == 0) return;

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational, roadmapRel,
            $"GSD Core's completion signals disagree: {RoadmapFileName} marks {roadmapDone}/{allPlans.Count} plans "
            + $"complete, while {string.Join("; and ", disagreements)}. {RoadmapFileName}'s per-plan checkbox is the "
            + "signal SpecScribe treats as authoritative — it is the only one recorded for every plan — and the "
            + "others are reported rather than averaged in."));
    }

    // ---- STATE.md → SprintStatus (Task 7) -----------------------------------------------------------------------

    /// <summary>Projects <c>STATE.md</c>'s YAML frontmatter plus the roadmap's per-phase/per-plan status onto the
    /// shared sprint ledger, using the SAME synthetic ordinal as the epics model so the two surfaces cannot
    /// disagree about which phase is which.
    ///
    /// <para><b>Why both epic-kind and story-kind entries.</b> <c>SprintTemplater.GroupByEpic</c> buckets entries by
    /// kind and renders story rows underneath each epic; a ledger carrying only phase entries would produce a sprint
    /// page whose entire body is empty — the "misleadingly empty" surface NFR8 forbids, which is worse than the
    /// honest omission below. Both kinds come from the one authoritative signal (ROADMAP's checkbox), so no second
    /// source of truth is introduced.</para>
    ///
    /// <para><b>No per-phase status recoverable ⇒ null, with a notice.</b> <c>STATE.md</c> carries a milestone-level
    /// roll-up only; if the roadmap yielded no phases there is nothing per-phase to report, so the ledger is null
    /// and the sprint page, home widget and nav gate all omit cleanly on the single signal they already share.
    /// <c>ActionItems</c> is always empty — GSD Core has no analog, and that is honest absence, not a
    /// diagnostic.</para></summary>
    private static SprintStatus? IngestSprint(ForgeOptions options, EpicsModel? epics, List<AdapterDiagnostic> diagnostics)
    {
        var planningRoot = Path.Combine(options.RepoRoot, MarkerDirName);
        var statePath = Path.Combine(planningRoot, StateFileName);
        var stateExists = File.Exists(statePath);

        if (epics is null || epics.Epics.Count == 0)
        {
            if (stateExists)
            {
                diagnostics.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Unsupported, ToSourceRelative(options, statePath),
                    $"'{StateFileName}' records a milestone-level roll-up only, and no per-phase status could be "
                    + $"recovered from '{RoadmapFileName}'; sprint surfaces are omitted"));
            }
            return null;
        }

        var entries = new List<SprintEntry>();
        foreach (var epic in epics.Epics)
        {
            var storyClasses = epic.Stories.Select(s => StatusStyles.ForStatus(s.Status)).ToList();
            var phaseStatus =
                storyClasses.Count == 0 ? "backlog"
                : storyClasses.All(c => c == "done") ? "done"
                : storyClasses.Any(c => c == "done") ? "in-progress"
                : "backlog";

            entries.Add(new SprintEntry(SprintEntryKind.Epic, $"phase-{epic.Number}", epic.Number, null, phaseStatus));
            foreach (var story in epic.Stories)
            {
                // "done" → done; "drafted" → backlog. ForSprint has no "drafted" arm (it reads the yaml ledger's
                // closed vocabulary), so the words are chosen from ITS vocabulary here, not ForStatus's — the same
                // native→canonical mapping discipline, applied to the right classifier.
                var status = string.Equals(story.Status, "done", StringComparison.Ordinal) ? "done" : "backlog";
                var minor = story.Id.Split('.') is [_, var m] && int.TryParse(m, out var parsed) ? parsed : (int?)null;
                entries.Add(new SprintEntry(SprintEntryKind.Story, story.Id, epic.Number, minor, status));
            }
        }

        return new SprintStatus
        {
            Entries = entries,
            LastUpdated = stateExists ? TryReadStateLastUpdated(planningRoot) : null,
            // GSD Core has no retrospective action-item analog. Empty renders nothing, not an empty header.
            ActionItems = Array.Empty<SprintActionItem>(),
        };
    }

    private sealed record StateProgress(int CompletedPlans, int TotalPlans);

    private static readonly Regex StateCompletedPlans = TimedRegex.New(@"^\s*completed_plans:\s*(?<v>\d+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex StateTotalPlans = TimedRegex.New(@"^\s*total_plans:\s*(?<v>\d+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex StateLastUpdated = TimedRegex.New("^\\s*last_updated:\\s*\"?(?<v>[^\"\\r\\n]+?)\"?\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Reads only the two roll-up scalars this adapter needs out of <c>STATE.md</c>'s frontmatter, with
    /// line regexes rather than a whole-document deserialize — the house pattern <c>SprintStatusParser</c> already
    /// follows, so one malformed region elsewhere in the file cannot take the read down. Never throws.</summary>
    private static StateProgress? TryReadStateProgress(string planningRoot)
    {
        var raw = TryReadState(planningRoot);
        if (raw is null) return null;

        var completed = StateCompletedPlans.Match(raw);
        var total = StateTotalPlans.Match(raw);
        if (!completed.Success || !total.Success) return null;
        return int.TryParse(completed.Groups["v"].Value, out var c) && int.TryParse(total.Groups["v"].Value, out var t)
            ? new StateProgress(c, t)
            : null;
    }

    private static string? TryReadStateLastUpdated(string planningRoot)
    {
        var raw = TryReadState(planningRoot);
        if (raw is null) return null;
        var m = StateLastUpdated.Match(raw);
        return m.Success ? m.Groups["v"].Value.Trim() : null;
    }

    private static string? TryReadState(string planningRoot)
    {
        try
        {
            var path = Path.Combine(planningRoot, StateFileName);
            return File.Exists(path) ? MarkdownConverter.ReadAllTextShared(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The one artifact GSD Core ships that SpecScribe deliberately does not read: <c>config.json</c>.
    /// ADR 0020 permits a non-markdown source only when it is module-declared, exact-filename, directory-scoped AND
    /// presence-gated — and the presence gate is <c>ModuleContext.IsModulePresent</c>, which is BMad-keyed, so this
    /// file meets none of the machinery today. Widening ADR 0020's gate to non-BMad frameworks is a real question;
    /// this NAMES it rather than silently doing it.</summary>
    private static void ReportUnsupportedArtifacts(ForgeOptions options, List<AdapterDiagnostic> diagnostics)
    {
        var configPath = Path.Combine(options.RepoRoot, MarkerDirName, ConfigFileName);
        if (!File.Exists(configPath)) return;

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational, ToSourceRelative(options, configPath),
            $"'{ConfigFileName}' is GSD Core machine configuration and is not interpreted: the source scan is "
            + "*.md, and ADR 0020's gate for reading a non-markdown source is keyed on BMad module presence, which "
            + "a GSD repository cannot satisfy. Nothing is claimed about its contents."));
    }

    private static string ToSourceRelative(ForgeOptions options, string fullPath) =>
        Path.GetRelativePath(options.SourceRoot, fullPath);

    private static string ToSourceRelative(ForgeOptions options, string planningRoot, string fileName) =>
        Path.GetRelativePath(options.SourceRoot, Path.Combine(planningRoot, PhasesDirName, fileName));

    // ---- The roadmap grammar, parsed --------------------------------------------------------------------------

    /// <summary>One plan line as ROADMAP wrote it. <paramref name="Number"/> is the <c>YY</c> of
    /// <c>NN-YY-PLAN.md</c>, kept verbatim (GSD numbers plans from <c>00</c>) so a story id stays traceable back to
    /// a real file.</summary>
    internal sealed record RoadmapPlan(string FileName, int Number, bool Done, string? Description);

    internal sealed record RoadmapPhase(
        string Number,
        decimal SortKey,
        string Title,
        string? Milestone,
        string? Goal,
        string? Requirements,
        string? CompletedDate,
        IReadOnlyList<RoadmapPlan> Plans);

    internal sealed record RoadmapMilestone(string Name, string? CompletedDate);

    internal sealed record RoadmapModel(
        string OverviewHtml,
        IReadOnlyList<RoadmapPhase> Phases,
        IReadOnlyList<RoadmapMilestone> Milestones);

    /// <summary>Parses <c>ROADMAP.md</c>'s four load-bearing sections — <c>## Phases</c> (the milestone-grouped
    /// overview that establishes ROADMAP ORDER, and therefore D2's ordinals), the per-milestone
    /// <c>## Milestone: … — Phase Details</c> sections (goal, requirements, and the authoritative per-plan
    /// checkboxes), <c>## Backlog</c>, and <c>## Overview</c>. All four are treated as OPTIONAL: a roadmap missing
    /// any of them still yields whatever the others describe.
    ///
    /// <para>Phase identity is reconciled across sections by DECIMAL VALUE, not by string: the same phase is written
    /// <c>Phase 2.1</c> in the overview, <c>Phase 2.1</c> in its detail heading, <c>02.1-…</c> in its directory and
    /// <c>02.1-03-PLAN.md</c> in its plans. Parsing to <see cref="decimal"/> (exact, invariant-culture) is what
    /// makes <c>01</c>, <c>1</c> and <c>1.0</c> one phase.</para></summary>
    internal static class RoadmapParser
    {
        public static RoadmapModel Parse(string raw)
        {
            var lines = raw.Replace("\r\n", "\n").Split('\n');

            var order = new List<RoadmapPhase>();
            var milestones = new List<RoadmapMilestone>();
            var overview = new List<string>();

            // Pass 1 — `## Phases` (+ `## Backlog`) establishes ROADMAP ORDER and the milestone bands.
            string? currentMilestone = null;
            var section = string.Empty;
            foreach (var line in lines)
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = line[3..].Trim();
                    if (string.Equals(section, "Backlog", StringComparison.OrdinalIgnoreCase))
                    {
                        currentMilestone = BacklogBandName;
                        if (!milestones.Any(m => string.Equals(m.Name, BacklogBandName, StringComparison.Ordinal)))
                            milestones.Add(new RoadmapMilestone(BacklogBandName, null));
                    }
                    else
                    {
                        currentMilestone = null;
                    }
                    continue;
                }

                if (string.Equals(section, "Overview", StringComparison.OrdinalIgnoreCase))
                {
                    overview.Add(line);
                    continue;
                }

                var isPhasesSection = string.Equals(section, "Phases", StringComparison.OrdinalIgnoreCase);
                var isBacklogSection = string.Equals(section, "Backlog", StringComparison.OrdinalIgnoreCase);
                if (!isPhasesSection && !isBacklogSection) continue;

                if (isPhasesSection && line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var mh = MilestoneOverviewHeading.Match(line);
                    if (mh.Success)
                    {
                        currentMilestone = mh.Groups["name"].Value.Trim();
                        var date = mh.Groups["date"].Success ? mh.Groups["date"].Value.Trim() : null;
                        if (!milestones.Any(m => string.Equals(m.Name, currentMilestone, StringComparison.Ordinal)))
                            milestones.Add(new RoadmapMilestone(currentMilestone, date));
                    }
                    continue;
                }

                if (isBacklogSection && line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var bh = PhaseDetailHeading.Match(line);
                    if (bh.Success) AddPhase(order, bh.Groups["num"].Value, CleanTitle(bh.Groups["title"].Value), currentMilestone, null);
                    continue;
                }

                if (!isPhasesSection) continue;
                var po = PhaseOverviewLine.Match(line);
                if (!po.Success) continue;

                var desc = po.Groups["desc"].Success ? po.Groups["desc"].Value.Trim() : null;
                string? completed = null;
                if (desc is { Length: > 0 })
                {
                    var cm = CompletedSuffix.Match(desc);
                    if (cm.Success) completed = cm.Groups["date"].Value.Trim();
                }
                AddPhase(order, po.Groups["num"].Value, CleanTitle(po.Groups["title"].Value), currentMilestone, completed);
            }

            // Pass 2 — the per-milestone detail sections supply goal / requirements / PLANS, and contribute any
            // phase the overview never listed (appended in encounter order so ordinals stay deterministic).
            var byKey = order.ToDictionary(p => p.SortKey);
            RoadmapPhase? current = null;
            var plans = new List<RoadmapPlan>();
            string? goal = null, requirements = null;
            var inDetails = false;

            void FlushDetail()
            {
                if (current is null) return;
                var updated = current with
                {
                    Goal = goal ?? current.Goal,
                    Requirements = requirements ?? current.Requirements,
                    Plans = plans.ToList(),
                };
                var idx = order.FindIndex(p => p.SortKey == updated.SortKey);
                if (idx >= 0) order[idx] = updated; else order.Add(updated);
                byKey[updated.SortKey] = updated;
                current = null;
                plans = new List<RoadmapPlan>();
                goal = requirements = null;
            }

            foreach (var line in lines)
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    FlushDetail();
                    inDetails = PhaseDetailsSectionHeading.IsMatch(line)
                        || string.Equals(line[3..].Trim(), "Backlog", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inDetails) continue;

                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    FlushDetail();
                    var dh = PhaseDetailHeading.Match(line);
                    if (!dh.Success) continue;
                    var key = ParseKey(dh.Groups["num"].Value);
                    current = byKey.TryGetValue(key, out var known)
                        ? known
                        : new RoadmapPhase(dh.Groups["num"].Value, key, CleanTitle(dh.Groups["title"].Value),
                            null, null, null, null, Array.Empty<RoadmapPlan>());
                    continue;
                }
                if (current is null) continue;

                var gm = GoalLine.Match(line);
                if (gm.Success) { goal ??= gm.Groups["v"].Value.Trim(); continue; }

                var rm = RequirementsLine.Match(line);
                if (rm.Success) { requirements ??= rm.Groups["v"].Value.Trim(); continue; }

                var pl = PlanLine.Match(line);
                if (!pl.Success) continue;
                if (!int.TryParse(pl.Groups["plan"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var planNo)) continue;
                plans.Add(new RoadmapPlan(
                    pl.Groups["file"].Value,
                    planNo,
                    pl.Groups["mark"].Value is "x" or "X",
                    pl.Groups["desc"].Success ? pl.Groups["desc"].Value.Trim() : null));
            }
            FlushDetail();

            var overviewText = string.Join("\n", overview).Trim();
            return new RoadmapModel(
                overviewText.Length > 0 ? MarkdownConverter.RenderInline(overviewText) : string.Empty,
                order,
                milestones);
        }

        private static void AddPhase(List<RoadmapPhase> order, string number, string title, string? milestone, string? completed)
        {
            var key = ParseKey(number);
            if (order.Any(p => p.SortKey == key)) return;
            order.Add(new RoadmapPhase(number, key, title, milestone, null, null, completed, Array.Empty<RoadmapPlan>()));
        }

        /// <summary>The one reconciliation key across the four sections, the phase directories and the plan
        /// filenames. Decimal (not double) so <c>02.1</c> and <c>2.1</c> compare exactly, and invariant-culture so a
        /// comma-decimal locale cannot silently change what a phase number means.</summary>
        private static decimal ParseKey(string number) =>
            decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

        private static string CleanTitle(string title) =>
            PhaseMarkerSuffix.Replace(CompletedSuffix.Replace(title.Trim(), string.Empty), string.Empty).Trim();
    }

    /// <summary>The leading phase number of a <c>NN-slug</c> phase DIRECTORY name, as a
    /// <see cref="decimal"/> — exposed for tests that pin the decimal-tolerant identity rule.</summary>
    internal static decimal? PhaseNumberFromDirectoryName(string dirName)
    {
        var m = LeadingPhaseNumber.Match(dirName);
        return m.Success && decimal.TryParse(m.Groups["num"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }
}
