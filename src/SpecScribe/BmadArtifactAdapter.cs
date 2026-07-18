using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The first (and, in Story 4.1, only) <see cref="IArtifactAdapter"/>: wraps the BMad discovery and
/// parsing that previously lived inline in <see cref="SiteGenerator"/> — sprint tracking, module detection,
/// retrospectives, and the epics → progress → requirements chain — behind the ingestion contract, emitting the
/// same models byte-for-byte. The hardcoded BMad conventions carried here verbatim (<c>epics.md</c> by name,
/// <c>implementation-artifacts/</c> story artifacts, <c>epic-N-retro-*</c> notes, <c>sprint-status.yaml</c>)
/// are deliberate: generalizing them is Story 4.2's job; this story only names the seam. [Story 4.1]</summary>
public sealed class BmadArtifactAdapter : IArtifactAdapter
{
    /// <summary>BMad's epic/story breakdown file, matched by filename anywhere in the source tree. The ONE
    /// home for this literal — <see cref="SiteNav"/>, <see cref="SiteGenerator"/>, and
    /// <see cref="ArtifactCoverage"/> all classify against this constant rather than re-hard-coding the
    /// string, so BMad's naming lives in exactly one place (NFR4). [Story 4.2 Task 4]</summary>
    public const string EpicsFileName = "epics.md";

    /// <summary>BMad's per-story artifact folder. Like <see cref="EpicsFileName"/>, the single source of
    /// truth for the name; classification is by directory SEGMENT (any depth, via
    /// <see cref="IsUnderImplementationArtifacts(string)"/>), not a fixed parent, so a project that nests the
    /// folder deeper doesn't lose its stories (AC #1). [Story 4.2 Task 4]</summary>
    public const string ImplementationArtifactsDirName = "implementation-artifacts";

    /// <summary>BMad's sprint tracking file (the sprint board / Now &amp; Next data source), matched by filename
    /// anywhere under the source root. The ONE home for this literal — <see cref="IngestSprint"/> discovers it and
    /// <see cref="SiteGenerator.IsDataSource"/> classifies watch-mode events against this same constant rather than
    /// re-hard-coding the string (NFR4). It is a <c>.yaml</c>, so it is deliberately outside the <c>*.md</c> source
    /// enumeration and needs the widened watch route. [Story 6.11]</summary>
    public const string SprintStatusFileName = "sprint-status.yaml";

    /// <summary>True when <paramref name="path"/> (full or relative) is the sprint tracking file, by filename
    /// regardless of folder depth — the same location tolerance the ingest discovery applies. [Story 6.11]</summary>
    public static bool IsSprintStatusFile(string path) =>
        string.Equals(Path.GetFileName(path), SprintStatusFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="path"/> (full or relative) is the epics breakdown file, by
    /// filename regardless of folder depth — the same location tolerance <see cref="SiteNav"/> has always
    /// applied. [Story 4.2 Task 4]</summary>
    public static bool IsEpicsFile(string path) =>
        string.Equals(Path.GetFileName(path), EpicsFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when any DIRECTORY segment of <paramref name="path"/> (full or relative, either slash
    /// style) is the implementation-artifacts folder — location-tolerant, not parent-dir-fixed, so
    /// <c>tracking/implementation-artifacts/1-1-x.md</c> still classifies as a story artifact while a file
    /// merely NAMED like the folder does not. [Story 4.2 Task 4]</summary>
    public static bool IsUnderImplementationArtifacts(string path)
    {
        var dir = Path.GetDirectoryName(path.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(dir)) return false;
        return dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(s => string.Equals(s, ImplementationArtifactsDirName, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Regex ArtifactFilenamePattern = new(@"^(?<epic>\d+)-(?<story>\d+)-", RegexOptions.Compiled);

    /// <summary>The epics-scoped slice of an ingest, exposed separately so the watch-mode incremental path
    /// (<see cref="SiteGenerator.RegenerateEpics"/>) can re-parse exactly the scope it always has — epics +
    /// story artifacts + requirements — without re-ingesting sprint/retro/module state it doesn't refresh
    /// (AD-5: watch behavior must not regress). <paramref name="SourceFullPath"/> is set whenever the epics
    /// file was FOUND, independent of parse success, so callers can keep excluding it from generic-page
    /// rendering exactly as before. [Story 4.1]</summary>
    public sealed record EpicsIngest(
        string? SourceFullPath,
        EpicsModel? Epics,
        RequirementsModel? Requirements,
        IReadOnlyDictionary<string, string> StoryArtifactsById,
        IReadOnlyCollection<string> ConsumedSourceRelatives,
        IReadOnlyList<AdapterDiagnostic> Diagnostics);

    /// <summary>BMad's self-selection signal is the framework's marker directory: an <c>_bmad/</c> install
    /// root at the repo root — the same signal <see cref="ModuleContext.Detect"/> keys off. Deliberately NOT
    /// consulted by <see cref="SiteGenerator"/> in Story 4.1 (BMad is the only adapter, and a bare
    /// <c>_bmad-output</c> tree without an install must keep rendering as it does today); the adapter
    /// registry of Stories 4.3+ is its consumer.</summary>
    public bool AppliesTo(ForgeOptions options, IReadOnlyList<string> sourceFiles) =>
        Directory.Exists(Path.Combine(options.RepoRoot, "_bmad"));

    public ArtifactBundle Ingest(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress)
    {
        // Defensive re-filter: SiteGenerator's enumeration already excludes ignored working files, but a
        // direct caller (tests, future registry) must get the same "neither ingested nor diagnosed" behavior.
        var files = sourceFiles.Where(f => !PathUtil.IsIgnoredSourceFile(f)).ToList();
        var sourceRelatives = files.Select(f => ToSourceRelative(options, f)).ToList();
        var diagnostics = new List<AdapterDiagnostic>();

        var module = ModuleContext.Detect(options.RepoRoot, sourceRelatives);
        var sprint = IngestSprint(options, diagnostics);

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var retros = IngestRetros(options, files, consumed, diagnostics);

        var epics = IngestEpics(options, files, projectProgress);
        foreach (var rel in epics.ConsumedSourceRelatives) consumed.Add(rel);
        diagnostics.AddRange(epics.Diagnostics);

        return new ArtifactBundle
        {
            Module = module,
            Sprint = sprint,
            Retros = retros,
            Epics = epics.Epics,
            Requirements = epics.Requirements,
            EpicsSourceFullPath = epics.SourceFullPath,
            StoryArtifactsById = epics.StoryArtifactsById,
            ConsumedSourceRelatives = consumed,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Ingests the epics chain in the order BMad mandates: parse epics, resolve story artifacts,
    /// obtain the caller's progress enrichment, THEN parse requirements (they roll up from epics + progress
    /// out of the same source file). The whole chain shares one try/catch so a mid-chain failure leaves
    /// exactly the models produced before the failure — matching the generator's previous partial-failure
    /// semantics — and reports one <see cref="AdapterDiagnosticCategory.Malformed"/> diagnostic. [Story 4.1]</summary>
    public EpicsIngest IngestEpics(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress)
    {
        var files = sourceFiles.Where(f => !PathUtil.IsIgnoredSourceFile(f)).ToList();
        var diagnostics = new List<AdapterDiagnostic>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifactMap = BuildArtifactMap(files);
        var epicsSourceFile = FindEpicsSourceFile(files);
        if (epicsSourceFile is null)
        {
            return new EpicsIngest(null, null, null, artifactMap, consumed, diagnostics);
        }

        EpicsModel? model = null;
        RequirementsModel? requirements = null;
        try
        {
            var raw = MarkdownConverter.ReadAllTextShared(epicsSourceFile);
            var parsed = EpicsParser.Parse(raw);

            foreach (var epic in parsed.Epics)
            {
                foreach (var story in epic.Stories)
                {
                    if (artifactMap.TryGetValue(story.Id, out var artifactFullPath))
                    {
                        story.ArtifactOutputPath = $"epics/story-{story.Id.Replace('.', '-')}.html";
                        story.ArtifactSourcePath = PathUtil.NormalizeSlashes(ToSourceRelative(options, artifactFullPath));
                        consumed.Add(ToSourceRelative(options, artifactFullPath));
                    }
                }
            }

            // The model only escapes this ingest once artifacts are resolved AND the projection enrichment
            // succeeded — the same point the generator previously cached it — so a progress failure can't
            // leave a half-enriched model visible downstream.
            var progress = projectProgress?.Invoke(parsed, artifactMap);
            model = parsed;
            if (progress is not null)
            {
                requirements = RequirementsParser.Parse(raw, parsed, progress);
                // Story 8.2 AC #3: present-but-unmapped Status: lines → Unsupported AdapterDiagnostic
                // (typed channel from Story 4.1 — not a bespoke GenerationEvent).
                CollectUnrecognizedStoryStatuses(parsed, ToSourceRelative(options, epicsSourceFile), diagnostics);
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Malformed, ToSourceRelative(options, epicsSourceFile), ex.Message));
        }

        return new EpicsIngest(epicsSourceFile, model, requirements, artifactMap, consumed, diagnostics);
    }

    /// <summary>Emits one non-fatal <see cref="AdapterDiagnosticCategory.Unsupported"/> notice per story whose
    /// free-text <c>Status:</c> is present but unmapped. Absent/empty status stays "drafted" with no notice.
    /// [Story 8.2 AC #3]</summary>
    private static void CollectUnrecognizedStoryStatuses(
        EpicsModel model, string epicsSourceRel, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var epic in model.Epics)
        {
            foreach (var story in epic.Stories)
            {
                if (!StatusStyles.IsUnrecognizedStatus(story.Status)) continue;
                var path = story.ArtifactSourcePath ?? epicsSourceRel;
                diagnostics.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Unsupported,
                    path,
                    $"Unrecognized status '{story.Status}' — no canonical lifecycle mapping; rendered as unrecognized"));
            }
        }
    }

    /// <summary>Locates and parses the sprint tracking file (well-known name, anywhere under the source root —
    /// it's a <c>.yaml</c>, so it's outside the <c>*.md</c> source enumeration). Absent → null with no
    /// diagnostic (graceful omission, as today); present-but-uninterpretable → null PLUS an
    /// <see cref="AdapterDiagnosticCategory.Unsupported"/> diagnostic, giving AC #2's "unsupported shape"
    /// case a visible, categorized, non-fatal report where it used to degrade silently. [Story 4.1]
    /// <para>Discovery is IO-safe: inaccessible subdirectories are skipped
    /// (<see cref="EnumerationOptions.IgnoreInaccessible"/>) so one locked folder does not discard reachable
    /// <c>sprint-status.yaml</c> files; a residual IO/permissions failure still degrades to "no candidates"
    /// instead of aborting ingest. When more than one <c>sprint-status.yaml</c> exists (a monorepo/multi-module
    /// layout), alphabetical OrdinalIgnoreCase first-wins exactly as before, but the pick is no longer silent —
    /// one <see cref="AdapterDiagnosticCategory.Skipped"/> diagnostic names the chosen path and how many siblings
    /// were skipped. [spec-epic2-deferred-debt-cleanup]</para></summary>
    private static SprintStatus? IngestSprint(ForgeOptions options, List<AdapterDiagnostic> diagnostics)
    {
        var candidates = FindSprintStatusCandidates(options.SourceRoot);
        var sprintPath = candidates.Count > 0 ? candidates[0] : null;
        if (sprintPath is null) return null;

        if (candidates.Count > 1)
        {
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Skipped,
                ToSourceRelative(options, sprintPath),
                $"{candidates.Count - 1} duplicate '{SprintStatusFileName}' file(s) skipped in favor of this one"));
        }

        var sprint = SprintStatusParser.ParseFile(sprintPath);
        if (sprint is null)
        {
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported,
                ToSourceRelative(options, sprintPath),
                "sprint tracking file has no usable development_status map; sprint surfaces are omitted"));
            return null;
        }

        // Story 8.2 AC #3: present-but-unmapped sprint ledger values → Unsupported (non-fatal).
        CollectUnrecognizedSprintStatuses(sprint, ToSourceRelative(options, sprintPath), diagnostics);
        return sprint;
    }

    /// <summary>Every <c>sprint-status.yaml</c> under <paramref name="sourceRoot"/>, alphabetical
    /// OrdinalIgnoreCase (first-wins selection stays with the caller). Inaccessible subdirectories are
    /// skipped via <see cref="EnumerationOptions.IgnoreInaccessible"/> so one locked folder does not wipe
    /// reachable candidates; a residual <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/>
    /// still degrades to an empty list rather than aborting generation. [spec-epic2-deferred-debt-cleanup]</summary>
    private static List<string> FindSprintStatusCandidates(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot)) return new List<string>();
        try
        {
            return Directory.EnumerateFiles(sourceRoot, SprintStatusFileName, new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                })
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    /// <summary>One <see cref="AdapterDiagnosticCategory.Unsupported"/> notice per sprint ledger or action-item
    /// status that is present but has no canonical mapping. [Story 8.2 AC #3]</summary>
    private static void CollectUnrecognizedSprintStatuses(
        SprintStatus sprint, string sprintSourceRel, List<AdapterDiagnostic> diagnostics)
    {
        foreach (var entry in sprint.Entries)
        {
            if (!StatusStyles.IsUnrecognizedSprintStatus(entry.Status)) continue;
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported,
                sprintSourceRel,
                $"Unrecognized sprint status '{entry.Status}' on '{entry.RawKey}' — no canonical lifecycle mapping; rendered as unrecognized"));
        }

        foreach (var item in sprint.ActionItems)
        {
            if (!StatusStyles.IsUnrecognizedSprintStatus(item.Status)) continue;
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported,
                sprintSourceRel,
                $"Unrecognized action-item status '{item.Status}' — no canonical lifecycle mapping; rendered as unrecognized"));
        }
    }

    /// <summary>Parses the retrospective notes (<c>epic-N-retro-*.md</c>), ordered by epic then filename.
    /// Every recognized retro file is consumed (so the generic pages pass never renders it) even when its
    /// parse fails; a failure reports a <see cref="AdapterDiagnosticCategory.Malformed"/> diagnostic and the
    /// sibling retros still parse (AC #2 — previously a throwing retro aborted the whole run). [Story 4.1]</summary>
    private static IReadOnlyList<RetroModel> IngestRetros(
        ForgeOptions options, IReadOnlyList<string> sourceFiles, HashSet<string> consumed, List<AdapterDiagnostic> diagnostics)
    {
        var retros = new List<RetroModel>();
        foreach (var file in sourceFiles.Where(RetroParser.IsRetroFile))
        {
            var sourceRel = ToSourceRelative(options, file);
            consumed.Add(sourceRel);
            try
            {
                var outputRel = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(sourceRel));
                retros.Add(RetroParser.Parse(file, sourceRel, outputRel));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new AdapterDiagnostic(AdapterDiagnosticCategory.Malformed, sourceRel, ex.Message));
            }
        }

        return retros
            .OrderBy(r => r.EpicNumber)
            .ThenBy(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Story id (e.g. "1.2") → full path of its detail artifact, discovered by BMad's
    /// <c>{epic}-{story}-*.md</c> filename convention under an <c>implementation-artifacts/</c> ancestor at
    /// ANY depth (location-tolerant since Story 4.2; the canonical direct child layout is unchanged as the
    /// primary shape).</summary>
    private static Dictionary<string, string> BuildArtifactMap(IEnumerable<string> files)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            if (!IsUnderImplementationArtifacts(path))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            var m = ArtifactFilenamePattern.Match(name);
            if (!m.Success) continue;

            // TryParse, not Parse: an absurdly long digit run (still matched by the unbounded \d+ pattern)
            // would otherwise throw OverflowException and abort the whole ingest, breaking this adapter's
            // NEVER-throws contract — the same hardening SprintStatusParser already applies to its numeric
            // keys ([Story 2.3 review]); a non-parseable name simply isn't a story artifact. [Story 4.1 review]
            if (!int.TryParse(m.Groups["epic"].Value, out var epicNum) ||
                !int.TryParse(m.Groups["story"].Value, out var storyNum)) continue;

            var key = $"{epicNum}.{storyNum}";
            map[key] = path;
        }
        return map;
    }

    private static string? FindEpicsSourceFile(IEnumerable<string> files) =>
        files.FirstOrDefault(IsEpicsFile);

    private static string ToSourceRelative(ForgeOptions options, string fullPath) =>
        Path.GetRelativePath(options.SourceRoot, fullPath);
}
