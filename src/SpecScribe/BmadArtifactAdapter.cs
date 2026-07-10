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
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Malformed, ToSourceRelative(options, epicsSourceFile), ex.Message));
        }

        return new EpicsIngest(epicsSourceFile, model, requirements, artifactMap, consumed, diagnostics);
    }

    /// <summary>Locates and parses the sprint tracking file (well-known name, anywhere under the source root —
    /// it's a <c>.yaml</c>, so it's outside the <c>*.md</c> source enumeration). Absent → null with no
    /// diagnostic (graceful omission, as today); present-but-uninterpretable → null PLUS an
    /// <see cref="AdapterDiagnosticCategory.Unsupported"/> diagnostic, giving AC #2's "unsupported shape"
    /// case a visible, categorized, non-fatal report where it used to degrade silently. [Story 4.1]</summary>
    private static SprintStatus? IngestSprint(ForgeOptions options, List<AdapterDiagnostic> diagnostics)
    {
        var sprintPath = Directory.Exists(options.SourceRoot)
            ? Directory.EnumerateFiles(options.SourceRoot, "sprint-status.yaml", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;
        if (sprintPath is null) return null;

        var sprint = SprintStatusParser.ParseFile(sprintPath);
        if (sprint is null)
        {
            diagnostics.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported,
                ToSourceRelative(options, sprintPath),
                "sprint tracking file has no usable development_status map; sprint surfaces are omitted"));
        }

        return sprint;
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
    /// <c>implementation-artifacts/{epic}-{story}-*.md</c> filename convention (carried verbatim; Story 4.2
    /// generalizes it).</summary>
    private static Dictionary<string, string> BuildArtifactMap(IEnumerable<string> files)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            var parentDir = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (!string.Equals(parentDir, "implementation-artifacts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            var m = ArtifactFilenamePattern.Match(name);
            if (!m.Success) continue;

            var key = $"{int.Parse(m.Groups["epic"].Value)}.{int.Parse(m.Groups["story"].Value)}";
            map[key] = path;
        }
        return map;
    }

    private static string? FindEpicsSourceFile(IEnumerable<string> files) =>
        files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), "epics.md", StringComparison.OrdinalIgnoreCase));

    private static string ToSourceRelative(ForgeOptions options, string fullPath) =>
        Path.GetRelativePath(options.SourceRoot, fullPath);
}
