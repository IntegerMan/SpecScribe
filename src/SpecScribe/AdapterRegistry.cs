namespace SpecScribe;

/// <summary>Selects which <see cref="IArtifactAdapter"/>s run for a repo and merges what they produce into one
/// <see cref="ArtifactBundle"/> — the seam <see cref="SiteGenerator"/> used to short-circuit with a single
/// hardcoded <c>private readonly BmadArtifactAdapter _adapter = new();</c> field whose comment promised a registry
/// "with Stories 4.3+". Those stories relocated into Epics 11–15 and the field was left with no owner, so a
/// non-BMad repository could not be projected at all no matter how good its adapter was. [Story 12.2 Task 3; ADR 0038]
///
/// <para><b>Every matching adapter runs.</b> <see cref="IArtifactAdapter.AppliesTo"/> is a boolean per adapter, and
/// a real repo can legitimately answer yes twice — the reference GSD Core project plans in BMad and delivers in
/// GSD, carrying <c>_bmad/</c>, <c>_bmad-output/</c> AND <c>.planning/</c>. Rather than picking a winner and
/// discarding the rest, the registry runs them in <see cref="Default"/> order and merges (owner decision D5).</para>
///
/// <para><b>The merge is deliberately MINIMAL, and deliberately not the strategic answer.</b> Single-valued
/// families take the first non-null contribution and every dropped contribution is reported as a
/// <see cref="AdapterDiagnosticCategory.Skipped"/> diagnostic naming the adapter and the family — never a silent
/// overwrite. Collections union. What this does NOT attempt is multi-ROOTED source discovery:
/// <see cref="ForgeOptions.SourceRoot"/> is single-valued and anchors both the <c>*.md</c> enumeration and every
/// source-relative path, so merging at file-discovery level would need a path scheme this story is explicitly
/// forbidden from improvising. That is Story 4.9's AC #2.</para>
///
/// <para><b>A BMad-only repo is unchanged, by construction.</b> With one matching adapter the merge is an identity
/// — no cross-adapter diagnostic is emitted, because a notice that fired on every existing project would itself be
/// the regression. A repo matching NOTHING (a bare <c>_bmad-output</c> tree with no install) falls back to
/// <see cref="BmadArtifactAdapter"/> alone, which is exactly what the generator did before this type existed.</para></summary>
public sealed class AdapterRegistry
{
    private readonly IReadOnlyList<IArtifactAdapter> _adapters;
    private readonly BmadArtifactAdapter _fallback;

    /// <summary>The shipped roster, in probe order: framework-specific markers first, BMad LAST as both the final
    /// entry and the no-match fallback. The order matters twice — it decides which adapter wins a single-valued
    /// family, and it keeps a bare <c>_bmad-output</c> tree rendering exactly as it does today.</summary>
    public static IReadOnlyList<IArtifactAdapter> Default { get; } = new IArtifactAdapter[]
    {
        new GsdCoreArtifactAdapter(),
        new BmadArtifactAdapter(),
    };

    public AdapterRegistry() : this(Default) { }

    public AdapterRegistry(IReadOnlyList<IArtifactAdapter> adapters)
    {
        _adapters = adapters;
        _fallback = adapters.OfType<BmadArtifactAdapter>().FirstOrDefault() ?? new BmadArtifactAdapter();
    }

    /// <summary>The adapters that recognize this repo, in roster order; the BMad fallback alone when none do.
    /// <see cref="IArtifactAdapter.AppliesTo"/> is contractually cheap and non-throwing, but a defensive catch
    /// keeps one misbehaving adapter from aborting selection for its siblings.</summary>
    public IReadOnlyList<IArtifactAdapter> Select(ForgeOptions options, IReadOnlyList<string> sourceFiles)
    {
        var matched = new List<IArtifactAdapter>();
        foreach (var adapter in _adapters)
        {
            try
            {
                if (adapter.AppliesTo(options, sourceFiles)) matched.Add(adapter);
            }
            catch (Exception)
            {
                // A marker sniff that throws is a defect in that adapter, not a reason to stop selecting.
            }
        }
        return matched.Count > 0 ? matched : new IArtifactAdapter[] { _fallback };
    }

    public ArtifactBundle Ingest(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress)
    {
        var matched = Select(options, sourceFiles);
        var bundles = new List<(IArtifactAdapter Adapter, ArtifactBundle Bundle)>();
        foreach (var adapter in matched)
        {
            bundles.Add((adapter, adapter.Ingest(options, sourceFiles, projectProgress)));
        }

        // Single adapter → identity merge. Returning the bundle verbatim (rather than rebuilding an identical one)
        // is what makes "a BMad-only repository's output is unchanged" true by construction rather than by
        // inspection: there is no code path between that adapter and the generator at all.
        if (bundles.Count == 1) return bundles[0].Bundle;

        var diagnostics = new List<AdapterDiagnostic>();
        var retros = new List<RetroModel>();
        var storyArtifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var module = ModuleContext.None;
        string? moduleOwner = null;
        SprintStatus? sprint = null;
        string? sprintOwner = null;

        // The epics FAMILY (Epics + Requirements + EpicsSourceFullPath) is claimed together by the first adapter
        // that FOUND an epics source, not field-by-field. Splitting them would let a bundle carry adapter A's
        // source path beside adapter B's parsed model — and requirements roll up from the same file as the epics,
        // so a mismatched pair is incoherent rather than merely odd.
        EpicsModel? epics = null;
        RequirementsModel? requirements = null;
        string? epicsSource = null;
        string? epicsOwner = null;
        CommandCatalog? workflowCommands = null;
        var planningVocabulary = PlanningVocabulary.Default;

        foreach (var (adapter, bundle) in bundles)
        {
            var name = Name(adapter);
            diagnostics.AddRange(bundle.Diagnostics);
            retros.AddRange(bundle.Retros);
            foreach (var rel in bundle.ConsumedSourceRelatives) consumed.Add(rel);

            if (!HasModuleIdentity(module) && HasModuleIdentity(bundle.Module)) { module = bundle.Module; moduleOwner = name; }

            if (bundle.Sprint is not null)
            {
                if (sprint is null) { sprint = bundle.Sprint; sprintOwner = name; }
                else diagnostics.Add(Dropped(name, "sprint tracking", sprintOwner!));
            }

            if (bundle.EpicsSourceFullPath is not null || bundle.Epics is not null)
            {
                if (epicsOwner is null)
                {
                    epics = bundle.Epics;
                    requirements = bundle.Requirements;
                    epicsSource = bundle.EpicsSourceFullPath;
                    epicsOwner = name;
                    workflowCommands = bundle.WorkflowCommands;
                    planningVocabulary = bundle.PlanningVocabulary;
                }
                else
                {
                    diagnostics.Add(Dropped(name, "epics & stories", epicsOwner));
                }
            }

            foreach (var (id, path) in bundle.StoryArtifactsById)
            {
                if (storyArtifacts.TryAdd(id, path)) continue;
                diagnostics.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Skipped, PathUtil.NormalizeSlashes(path),
                    $"{name} also resolved a story artifact for id {id}; the earlier adapter's artifact was kept. "
                    + "Two frameworks numbering work independently can collide on an id — the collision is reported "
                    + "rather than silently overwritten."));
            }
        }

        diagnostics.Add(DescribeMatchSet(bundles, moduleOwner, sprintOwner, epicsOwner));
        AppendNonPrimaryMarkerNotice(options, diagnostics);

        return new ArtifactBundle
        {
            Module = module,
            WorkflowCommands = workflowCommands,
            PlanningVocabulary = planningVocabulary,
            Sprint = sprint,
            Retros = retros,
            Epics = epics,
            Requirements = requirements,
            EpicsSourceFullPath = epicsSource,
            StoryArtifactsById = storyArtifacts,
            ConsumedSourceRelatives = consumed,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>The watch-mode scoped re-ingest (AD-5). Runs every matching adapter's
    /// <see cref="IArtifactAdapter.IngestEpics"/> and returns the first that FOUND an epics source, with the others'
    /// artifact maps, consumed paths and diagnostics merged in — the same ownership rule
    /// <see cref="Ingest"/> applies to the epics family, so a watch pass and a full build can never disagree about
    /// which adapter owns the epics.
    ///
    /// <para>With one matching adapter this is the adapter's own return value, unchanged — which is what makes
    /// watch output for a BMad repo byte-identical to a full rebuild by construction (ADR 0027's definition of
    /// "safe"), not by measurement.</para></summary>
    public EpicsIngest IngestEpics(ForgeOptions options, IReadOnlyList<string> sourceFiles, ProgressProjection? projectProgress)
    {
        var matched = Select(options, sourceFiles);
        if (matched.Count == 1) return matched[0].IngestEpics(options, sourceFiles, projectProgress);

        var diagnostics = new List<AdapterDiagnostic>();
        var storyArtifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        EpicsIngest? owner = null;
        string? ownerName = null;

        foreach (var adapter in matched)
        {
            var ingest = adapter.IngestEpics(options, sourceFiles, projectProgress);
            diagnostics.AddRange(ingest.Diagnostics);
            foreach (var rel in ingest.ConsumedSourceRelatives) consumed.Add(rel);
            foreach (var (id, path) in ingest.StoryArtifactsById) storyArtifacts.TryAdd(id, path);

            if (ingest.SourceFullPath is null && ingest.Epics is null) continue;
            if (owner is null) { owner = ingest; ownerName = Name(adapter); }
            else diagnostics.Add(Dropped(Name(adapter), "epics & stories", ownerName!));
        }

        return new EpicsIngest(
            owner?.SourceFullPath, owner?.Epics, owner?.Requirements,
            storyArtifacts, consumed, diagnostics);
    }

    private static AdapterDiagnostic Dropped(string adapter, string family, string winner) =>
        new(AdapterDiagnosticCategory.Skipped, ".",
            $"{adapter} also produced {family}, which was not used: {winner} supplied that family first. Only one "
            + "framework can own a single-valued artifact family in a merged run.");

    /// <summary>The one <see cref="AdapterDiagnosticCategory.Informational"/> notice a multi-framework run emits:
    /// which adapters matched, and which of them supplied each family. Without it a reader looking at a merged
    /// portal has no way to tell which framework produced what — and the whole point of merging rather than picking
    /// is that the answer is not obvious.</summary>
    private static AdapterDiagnostic DescribeMatchSet(
        IReadOnlyList<(IArtifactAdapter Adapter, ArtifactBundle Bundle)> bundles,
        string? moduleOwner, string? sprintOwner, string? epicsOwner)
    {
        var names = string.Join(", ", bundles.Select(b => Name(b.Adapter)));
        var supplied = new List<string>
        {
            $"epics & stories: {epicsOwner ?? "none"}",
            $"sprint tracking: {sprintOwner ?? "none"}",
            $"module identity: {moduleOwner ?? "none"}",
        };
        return new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational, ".",
            $"{bundles.Count} framework adapters recognized this repository ({names}) and their results were merged. "
            + $"Supplied by — {string.Join("; ", supplied)}.");
    }

    /// <summary>Names any framework marker present at the repo root that did NOT become this run's source root.
    /// Its documents are outside <see cref="ForgeOptions.SourceRoot"/>, which is single-valued, so they do not
    /// render as pages — a real and deliberate limitation of bundle-level merging (D5), and one that reads as an
    /// unexplained gap unless it is said out loud (NFR8).</summary>
    private static void AppendNonPrimaryMarkerNotice(ForgeOptions options, List<AdapterDiagnostic> diagnostics)
    {
        var primary = Path.GetFileName(
            options.SourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var others = ForgeOptions.SourceDirNames
            .Where(m => !string.Equals(m, primary, StringComparison.OrdinalIgnoreCase))
            .Where(m => Directory.Exists(Path.Combine(options.RepoRoot, m)))
            .ToList();
        if (others.Count == 0) return;

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational, ".",
            $"This repository also carries {string.Join(", ", others.Select(o => $"'{o}/'"))}, which did not become "
            + $"this run's source root ('{primary}/'). Artifact families from those frameworks are still merged into "
            + "the portal, but their loose documents are outside the single source root and do not render as their "
            + "own pages. Re-run with --source pointing at one of them to render that tree instead.",
            DiagnosticAnchorRoot.Repo));
    }

    /// <summary>Whether a bundle's module context carries a real detected identity. <c>Module</c> is
    /// <c>required</c> and never null — an undetectable one is <see cref="ModuleContext.None"/>, whose
    /// <see cref="BmadModule.Unknown"/> is the signal here. Checked on the ENUM rather than by reference so an
    /// adapter that builds its own empty context (instead of returning the shared singleton) is treated the same.</summary>
    private static bool HasModuleIdentity(ModuleContext module) => module.Module != BmadModule.Unknown;

    private static string Name(IArtifactAdapter adapter) => adapter switch
    {
        BmadArtifactAdapter => "BMad",
        GsdCoreArtifactAdapter => "GSD Core",
        _ => adapter.GetType().Name,
    };
}
