using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

public enum GenerationOutcome { Generated, Updated, Removed, Skipped, Error }

/// <param name="FromAdapterDiagnostic">True only for events produced by <see cref="SiteGenerator.MapDiagnostics"/>
/// — the provenance <see cref="DiagnosticNotice.FromEvents"/> checks before trusting a leading
/// <c>[Category]</c> token in <paramref name="Message"/> as a real category tag, rather than sniffing arbitrary
/// message text (which could coincidentally start with the same bracket shape). [Review][Patch]</param>
public sealed record GenerationEvent(GenerationOutcome Outcome, string RelativePath, TimeSpan Elapsed, string? Message = null, bool FromAdapterDiagnostic = false);

/// <summary>Owns the mapping from _bmad-output/*.md to SpecScribeOutput/*.html, and keeps the generated index,
/// nav, and epics/story pages in sync.</summary>
public sealed class SiteGenerator
{
    // ADR numbers derive from several mainstream filename schemes — 0001-x, ADR-0001-x, adr-1-x, adr_001_x,
    // 1-x: an optional adr token, separators, then the first integer. A filename with no derivable number
    // still renders (sorted last) rather than dropping. [Story 4.2 Task 2]
    private static readonly Regex AdrNumberPattern = new(@"^(?:adr)?[-_\s]*(?<num>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AdrStatusPattern = new(@"^\*\*Status:\*\*\s*(?<status>.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    // The MADR-style "## Status" (or "### Status") section heading whose next non-blank line carries the
    // status value — the second of the three tolerated status shapes. [Story 4.2 Task 2]
    private static readonly Regex AdrStatusHeadingPattern = new(@"^#{2,3}\s+Status\s*$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MarkdownLinkPattern = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);

    private readonly ForgeOptions _options;
    private readonly Dictionary<string, DocModel> _docs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    // The ingestion seam (AD-1): all framework-specific discovery/parsing lives behind this adapter, so the
    // generator only ever consumes the normalized ArtifactBundle. Held as the concrete type (not
    // IArtifactAdapter) because the watch paths also need its scoped epics re-ingest; the adapter registry
    // that selects among IArtifactAdapter implementations arrives with Stories 4.3+. [Story 4.1]
    private readonly BmadArtifactAdapter _adapter = new();

    private SiteNav? _nav;
    private ModuleContext _module = ModuleContext.None;
    private EpicsModel? _epicsModel;
    private ProgressModel? _progress;
    private RequirementsModel? _requirements;
    private List<AdrEntry> _adrs = new();
    private List<CommitDayEntry> _commitDays = new();
    private SprintStatus? _sprint;
    private List<RetroModel> _retros = new();
    private ArtifactCoverage _coverage = ArtifactCoverage.Empty;

    // When --spa is active, every long-tail page's finished HTML is captured here as it is written (output path →
    // full page string) so the SPA bundle can slice its content region from the render pipeline's OWN output rather
    // than re-reading the generated site off disk (AD-1/AD-2). Null on a normal run — no capture, no overhead, and
    // the static output stays byte-identical (AC #3/#5). The dashboard/epics families are NOT captured here; the
    // SPA re-renders them from their view models (RenderSpaBundle) for strongest parity. [Story 6.7]
    private Dictionary<string, string>? _spaCapture;

    public SiteGenerator(ForgeOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<GenerationEvent> GenerateAll(IGenerationReporter? reporter = null)
    {
        reporter?.BeginPhase(GenerationPhase.Scan);
        var files = EnumerateSourceFiles();
        var sourceRelatives = files.Select(ToSourceRelative).ToList();
        reporter?.EndPhase(GenerationPhase.Scan);

        var events = new List<GenerationEvent>();
        lock (_gate)
        {
            // A full rebuild starts from a clean slate so renamed/removed source docs don't leave
            // orphaned HTML behind (incremental watch-mode updates never wipe the whole tree).
            if (Directory.Exists(_options.OutputRoot))
            {
                Directory.Delete(_options.OutputRoot, recursive: true);
            }

            EnsureScaffold();
            _docs.Clear();

            // Fresh capture buffer for this full build when the opt-in SPA form is on (null otherwise → no capture).
            _spaCapture = _options.EmitSpa ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : null;

            // All planning-artifact ingestion (sprint, module detection, retros, epics → requirements) runs
            // through the framework adapter, which returns one normalized bundle + non-fatal diagnostics.
            // Progress/git enrichment stays HERE (projection path, AD-4) and is handed in as a callback so the
            // adapter can keep BMad's epics-before-requirements ordering without owning git. [Story 4.1]
            ProgressModel? progress = null;
            var bundle = _adapter.Ingest(_options, files, (model, artifactsById) => progress = ComputeProgress(model, artifactsById));

            // Sprint presence drives the nav gate, and the sprint page + home widget read this same parsed
            // instance. Missing/malformed → null → the page, widget, and nav item all omit cleanly. [Story 2.3]
            _sprint = bundle.Sprint;
            _module = bundle.Module;
            SetRetros(bundle.Retros);

            var nav = SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable, hasStructure: sourceRelatives.Count > 0);
            _nav = nav;

            // Render the README up front so that, if it fails, we can drop the Readme nav entry before any
            // other page is written — the site never links to a readme.html that wasn't actually produced.
            GenerationEvent? readmeEvent = null;
            if (ReadmeAvailable)
            {
                readmeEvent = GenerateReadmeInternal(nav);
                if (readmeEvent is { Outcome: GenerationOutcome.Error })
                {
                    nav = SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), hasReadme: false, hasSprint: SprintAvailable, hasStructure: sourceRelatives.Count > 0);
                    _nav = nav;
                }
            }

            // Files the adapter consumed into dedicated surfaces (story artifacts, retro notes) — the generic
            // pages loop below must not also render them.
            var epicsSourceFile = bundle.EpicsSourceFullPath;
            var consumedArtifacts = new HashSet<string>(bundle.ConsumedSourceRelatives, StringComparer.OrdinalIgnoreCase);

            // Cache the ingested models with the same granularity the inline parse chain had: epics+progress
            // only once the projection enrichment ran to completion, requirements only when parsed — so a
            // mid-chain failure leaves the previous (or no) values in place rather than nulling everything.
            // Deliberately AFTER the README render above: the README has always rendered before the epics
            // parse, so it is linkified against the PREVIOUS run's models (null on a first run). [Story 4.1]
            if (progress is not null)
            {
                _epicsModel = bundle.Epics;
                _progress = progress;
                TagEpicRetrospectives();
            }
            if (bundle.Requirements is not null)
            {
                _requirements = bundle.Requirements;
            }

            // Adapter diagnostics surface on the same event channel per-file failures always used — the typed
            // AdapterDiagnostic is the only report for a failure the adapter caught (never double-reported).
            events.AddRange(MapDiagnostics(bundle.Diagnostics));

            // Unrecognized top-level source folders render coherently (each gets its own home-index band, see
            // HtmlTemplater.RenderIndex) AND are reported as categorized non-fatal structure notices on the
            // same diagnostic channel — the input Story 4.8's diagnostics page will render. [Story 4.2 Task 5]
            events.AddRange(MapDiagnostics(UnrecognizedTopLevelFolders(sourceRelatives)));

            // Render the epic/story/requirements pages only when the whole ingest chain produced its models —
            // the exact set of writes the previous single try/catch performed after a successful parse.
            if (epicsSourceFile is not null && bundle is { Epics: { } epicsModel, Requirements: { } requirementsModel } && progress is not null)
            {
                reporter?.BeginPhase(GenerationPhase.Epics);
                events.AddRange(RenderEpicsPages(epicsSourceFile, files, bundle.StoryArtifactsById, epicsModel, requirementsModel, progress, nav));
                reporter?.EndPhase(GenerationPhase.Epics);
            }

            RenderRetroPages(nav);

            // Epic/story artifacts were rendered as detail pages above; everything else renders standalone.
            var pageFiles = files
                .Where(file => epicsSourceFile is null || !string.Equals(file, epicsSourceFile, StringComparison.OrdinalIgnoreCase))
                .Where(file => !consumedArtifacts.Contains(ToSourceRelative(file)))
                .ToList();

            reporter?.BeginPhase(GenerationPhase.Pages, pageFiles.Count);
            foreach (var file in pageFiles)
            {
                events.Add(GenerateOneInternal(file, nav));
                reporter?.Tick(GenerationPhase.Pages);
            }
            reporter?.EndPhase(GenerationPhase.Pages);

            reporter?.BeginPhase(GenerationPhase.Adrs);
            events.AddRange(GenerateAdrsInternal(nav));
            reporter?.EndPhase(GenerationPhase.Adrs);

            // Per-day commit pages the heatmap links to. Git is only computed by the epics-phase progress
            // enrichment, so a project without an epics.md has no pulse here — which is consistent: no
            // heatmap renders either, so there's nothing to link to.
            if (_progress?.Git is { } gitPulse)
            {
                reporter?.BeginPhase(GenerationPhase.CommitDays);
                events.AddRange(GenerateCommitDaysInternal(gitPulse, nav));
                reporter?.EndPhase(GenerationPhase.CommitDays);
            }

            // Opt-in deep-git analytics page (hotspots + change-coupling graph). Generated only when --deep-git
            // produced data (DeepGit is only non-null when the flag gated the deep pass on); the dashboard's Git
            // Pulse panel links here. Non-fatal: a null DeepGit simply means no page and no link. [Story 3.2]
            if (_progress?.DeepGit is { } deepPulse)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var html = DeepAnalyticsTemplater.RenderPage(deepPulse, nav);
                    WriteOutput(SiteNav.DeepAnalyticsOutputPath, ApplyReferenceLinks(html, SiteNav.DeepAnalyticsOutputPath));
                    events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.DeepAnalyticsOutputPath, sw.Elapsed));
                }
                catch (Exception ex)
                {
                    events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.DeepAnalyticsOutputPath, sw.Elapsed, ex.Message));
                    // The page was never written — clear DeepGit so the dashboard's "View Deep Analytics" link
                    // (gated on _progress.DeepGit is not null) doesn't point at a page that doesn't exist.
                    _progress!.DeepGit = null;
                }
            }

            // Aggregate Git Insights hub (file change frequency, activity over time, contributor attribution)
            // — Story 3.8's deep-git surface. Gated on the same opt-in data as the deep-analytics page above:
            // Insights is only non-null when --deep-git gated TryComputeDeep on AND the shared numstat fetch
            // produced data, so with the flag off this block (and every byte of git work behind it) never runs
            // and baseline generation is untouched (AC #2). Runs after the commit-days phase so the reused
            // heatmap's per-day links always have their target pages. The file/commit detail-link resolvers
            // stay unwired until Stories 7.1/7.4/7.5 land their cached page maps — the templater renders those
            // cells as plain text (guard-all-links-on-target-availability). [Story 3.8]
            GenerateGitInsightsInternal(nav, events, reporter);

            if (readmeEvent is not null)
            {
                events.Add(readmeEvent);
            }

            // Artifact-family coverage insight — computed here, AFTER epics/pages have run, so ResolveFamilyHref
            // can check the now-fully-populated _docs/_epicsModel/_requirements before linking a "present"
            // family to a page (never a broken link — same ordering WriteStructure relies on for _docs below).
            // Cached so every WriteIndex call shares one instance. Never-throw: any failure degrades to Empty,
            // the panel omits, and generation still succeeds (AD-4 / NFR2). [Story 3.3 Task 2; review: reordered]
            _coverage = BuildArtifactCoverage(sourceRelatives);

            // The sprint page reads the epics model (for real story/epic titles + links), so it's written
            // after the epics phase. Gated on parsed sprint data; a no-op when there is none. [Story 2.3 Task 3/5]
            WriteSprint(nav);
            // The structure tree reads the fully-populated _docs (source→output hrefs), so it's written after the
            // pages phase — like WriteSprint, gated on the same source-artifact signal as its nav item. [Story 3.4]
            WriteStructure(nav, sourceRelatives);
            WriteRetroIndex(nav);
            // Built once and shared with WriteIndex below — both used to rebuild it independently. [Story 2.3 review]
            var workInventory = WorkInventory.Build(_docs.Values.ToList());
            WriteActionItems(nav, workInventory);

            reporter?.BeginPhase(GenerationPhase.Index);
            WriteIndex(nav, workInventory);
            reporter?.EndPhase(GenerationPhase.Index);

            // Diagnostics + About are the whole-run reporting surface (Story 4.8): written LAST, after every
            // phase has appended its events, so the diagnostics page reflects the COMPLETE non-fatal notice set.
            // Both are always written on a full run (the diagnostics page's zero-notice case renders an all-clear
            // state, never a gated-away page), so the site-wide footer "About" link — and the About page's link
            // on to the run log — can never 404. Each write's own Generated event is appended AFTER the
            // diagnostics page reads the notice list, so it never self-references. [Story 4.8 Task 6]
            events.Add(WriteDiagnostics(nav, events));
            events.Add(WriteAbout(nav));

            // Opt-in JSON+SPA delivery form, emitted LAST so every captured page is present. Strictly additive:
            // writes only its own files under OutputRoot, leaving every static page byte-identical (AC #3/#5/#6).
            if (_options.EmitSpa)
            {
                EmitSpaSite(nav);
            }
        }
        return events;
    }

    public GenerationEvent GenerateOne(string sourceFullPath)
    {
        lock (_gate)
        {
            EnsureScaffold();
            var nav = _nav ?? BuildNav(Array.Empty<string>());
            var ev = GenerateOneInternal(sourceFullPath, nav);
            RefreshCoverage();
            WriteIndex(nav);
            // Keep the opt-in SPA form in sync in watch mode: _spaCapture already holds the fresh page (captured by
            // GenerateOneInternal's WriteOutput), so re-emitting rebuilds the manifest/chunks from current state.
            if (_options.EmitSpa) EmitSpaSite(nav);
            return ev;
        }
    }

    public GenerationEvent RemoveFor(string sourceFullPath)
    {
        var sw = Stopwatch.StartNew();
        var relative = ToSourceRelative(sourceFullPath);

        lock (_gate)
        {
            if (_docs.TryGetValue(relative, out var doc))
            {
                var outputFullPath = Path.Combine(_options.OutputRoot, doc.OutputRelativePath);
                if (File.Exists(outputFullPath))
                {
                    File.Delete(outputFullPath);
                }

                _docs.Remove(relative);
                // Drop the removed page from the SPA capture so a stale region can't linger in the next bundle.
                if (_spaCapture is not null) _spaCapture.Remove(PathUtil.NormalizeSlashes(doc.OutputRelativePath));
                RefreshCoverage();
                var nav = _nav ?? BuildNav(Array.Empty<string>());
                WriteIndex(nav);
                if (_options.EmitSpa) EmitSpaSite(nav);
                return new GenerationEvent(GenerationOutcome.Removed, relative, sw.Elapsed);
            }

            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "not tracked");
        }
    }

    /// <summary>Recomputes and re-caches <see cref="_coverage"/> from the current on-disk source tree. Called
    /// by every watch-mode incremental path (<see cref="GenerateOne"/>, <see cref="RemoveFor"/>,
    /// <see cref="RegenerateEpics"/>, <see cref="RegenerateAdrs"/>) right before their own <c>WriteIndex</c>
    /// call, so a live edit (new/changed/removed planning artifact) is reflected in the Planning Artifacts
    /// panel immediately rather than only after the next full <c>generate</c>. [Story 3.3 review]</summary>
    private void RefreshCoverage()
    {
        _coverage = BuildArtifactCoverage(EnumerateSourceFiles().Select(ToSourceRelative).ToList());
    }

    /// <summary>True for the epics file itself, or any file under an implementation-artifacts/ ancestor —
    /// both feed the epics/story pages, so either kind of change should trigger a full
    /// <see cref="RegenerateEpics"/> rather than the generic single-file path. Classifies via the adapter's
    /// shared conventions so watch routing can never disagree with what ingestion discovers. [Story 4.2 Task 4]</summary>
    public bool IsEpicsRelated(string sourceFullPath) =>
        BmadArtifactAdapter.IsEpicsFile(sourceFullPath)
        || BmadArtifactAdapter.IsUnderImplementationArtifacts(sourceFullPath);

    /// <summary>Re-parses epics.md and rewrites epics.html + every epics/epic-N.html + epics/story-N-M.html.
    /// Also refreshes the nav and the artifact map, so added/removed/renamed story files self-heal without
    /// needing a full restart.</summary>
    public GenerationEvent RegenerateEpics()
    {
        var sw = Stopwatch.StartNew();
        lock (_gate)
        {
            EnsureScaffold();

            var files = EnumerateSourceFiles();
            var nav = BuildNav(files.Select(ToSourceRelative).ToList());
            _nav = nav;

            // Scoped re-ingest through the adapter: exactly the epics + story-artifact + requirements re-parse
            // this path always did, without touching the sprint/retro/module state it never refreshed (AD-5
            // watch parity). [Story 4.1]
            ProgressModel? progress = null;
            var ingest = _adapter.IngestEpics(_options, files, (model, artifactsById) => progress = ComputeProgress(model, artifactsById));

            if (ingest.SourceFullPath is null)
            {
                RefreshCoverage();
                WriteIndex(nav);
                if (_options.EmitSpa) EmitSpaSite(nav);
                return new GenerationEvent(GenerationOutcome.Skipped, BmadArtifactAdapter.EpicsFileName, sw.Elapsed, $"{BmadArtifactAdapter.EpicsFileName} not found");
            }

            // Same partial-failure caching rules as GenerateAll: a broken mid-edit save must leave the last
            // good models in place so the rest of the site keeps linkifying against them.
            if (progress is not null)
            {
                _epicsModel = ingest.Epics;
                _progress = progress;
                // Watch-mode re-ingest also re-stamps HasRetrospective from the (preserved) retro map, so a retro'd
                // all-done epic doesn't flip to "In review" on the sunburst/donut/badge after an unrelated story
                // edit — the flag must not reset to false when the model is rebuilt. [spec-sunburst-retro]
                TagEpicRetrospectives();
            }
            if (ingest.Requirements is not null)
            {
                _requirements = ingest.Requirements;
            }

            var epicsEvents = new List<GenerationEvent>(MapDiagnostics(ingest.Diagnostics));
            if (ingest is { Epics: { } epicsModel, Requirements: { } requirementsModel } && progress is not null)
            {
                epicsEvents.AddRange(RenderEpicsPages(ingest.SourceFullPath, files, ingest.StoryArtifactsById, epicsModel, requirementsModel, progress, nav));
            }
            RefreshCoverage();
            WriteIndex(nav);
            if (_options.EmitSpa) EmitSpaSite(nav);

            var errored = epicsEvents.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            return new GenerationEvent(GenerationOutcome.Updated, ToSourceRelative(ingest.SourceFullPath), sw.Elapsed, $"{ingest.ConsumedSourceRelatives.Count} stories");
        }
    }

    /// <summary>True for any file under the ADR source root — routes watch-mode events to
    /// <see cref="RegenerateAdrs"/> rather than the _bmad-output pipeline.</summary>
    public bool IsAdr(string sourceFullPath)
    {
        var full = Path.GetFullPath(sourceFullPath);
        var root = Path.GetFullPath(_options.AdrSourceRoot);
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Re-renders every ADR page (and the index that lists them). ADRs cross-link to one another,
    /// so a single edit is rebuilt as a set rather than one page in isolation.</summary>
    public GenerationEvent RegenerateAdrs()
    {
        var sw = Stopwatch.StartNew();
        lock (_gate)
        {
            EnsureScaffold();
            var nav = _nav ?? BuildNav(Array.Empty<string>());
            var events = GenerateAdrsInternal(nav);
            RefreshCoverage();
            WriteIndex(nav);
            if (_options.EmitSpa) EmitSpaSite(nav);

            var errored = events.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            return new GenerationEvent(GenerationOutcome.Updated, "adrs", sw.Elapsed, $"{_adrs.Count} ADRs");
        }
    }

    /// <summary>Renders each hand-authored record under the resolved ADR root into <c>SpecScribeOutput/adrs</c>.
    /// The ADR root's own README.md becomes the landing page (index.html); every other record becomes a card
    /// on the home index — numbered or not (an underivable number sorts the card last rather than dropping it,
    /// AC #2), with README/template scaffolding files still rendering for cross-links but never carded. Records
    /// nested one level (e.g. <c>decisions/2024/0007-x.md</c>) keep their subpath in the output so authored
    /// relative cross-links survive the .md → .html swap. The whole ADR output directory is rebuilt each pass
    /// so a deleted or renamed record can't leave a stale page behind. [Story 4.2 Tasks 1–2]</summary>
    private List<GenerationEvent> GenerateAdrsInternal(SiteNav nav)
    {
        var events = new List<GenerationEvent>();

        var adrOutputDir = Path.Combine(_options.OutputRoot, ForgeOptions.AdrOutputSubdir);
        if (Directory.Exists(adrOutputDir))
        {
            Directory.Delete(adrOutputDir, recursive: true);
        }

        var entries = new List<AdrEntry>();
        foreach (var file in EnumerateAdrFiles())
        {
            var sw = Stopwatch.StartNew();
            var fileName = Path.GetFileName(file);
            var relativeToRoot = PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.AdrSourceRoot, file));
            // README-as-landing applies to the ADR root's README only; a nested README renders as a regular page.
            var isReadme = string.Equals(relativeToRoot, "README.md", StringComparison.OrdinalIgnoreCase);

            var outputName = isReadme ? "index.html" : Path.ChangeExtension(relativeToRoot, ".html");
            var outputRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{outputName}");
            var sourceRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{relativeToRoot}");

            try
            {
                var raw = MarkdownConverter.ReadAllTextShared(file);
                var parsed = MarkdownConverter.Convert(file, sourceRelative, outputRelative);
                var isRecord = IsAdrRecordFile(relativeToRoot);

                // Computed once, record files only, so the record's own page and its home-index card agree —
                // both derive status from the same three tolerated shapes (bold line / MADR heading /
                // frontmatter) rather than the page reading frontmatter alone. Non-record files (README,
                // template) keep their parsed frontmatter untouched — they were never eligible for a status
                // badge and a coincidental "**Status:**" line in prose shouldn't grant them one. [Review][Patch]
                var tolerantStatus = isRecord ? ExtractAdrStatus(raw, parsed.Frontmatter) : parsed.Frontmatter.Status;
                var frontmatter = tolerantStatus == parsed.Frontmatter.Status
                    ? parsed.Frontmatter
                    : new Frontmatter
                    {
                        Title = parsed.Frontmatter.Title,
                        Project = parsed.Frontmatter.Project,
                        Date = parsed.Frontmatter.Date,
                        Author = parsed.Frontmatter.Author,
                        Version = parsed.Frontmatter.Version,
                        Status = tolerantStatus,
                        Route = parsed.Frontmatter.Route,
                        Type = parsed.Frontmatter.Type,
                        Id = parsed.Frontmatter.Id,
                        Companions = parsed.Frontmatter.Companions,
                        Sources = parsed.Frontmatter.Sources,
                    };
                var doc = new DocModel
                {
                    SourceRelativePath = parsed.SourceRelativePath,
                    OutputRelativePath = parsed.OutputRelativePath,
                    Title = parsed.Title,
                    Frontmatter = frontmatter,
                    BodyHtml = AdrLinkRewriter.Rewrite(parsed.BodyHtml, PathUtil.RelativePrefix(outputRelative)),
                    Headings = parsed.Headings,
                    HasMermaid = parsed.HasMermaid,
                };

                WriteOutput(outputRelative, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));

                if (isRecord)
                {
                    var number = ParseAdrNumber(fileName);
                    entries.Add(new AdrEntry(doc.Title, outputRelative, sourceRelative, tolerantStatus, number));
                    if (number is null)
                    {
                        // The unnumbered shape is tolerated, not silent: one categorized non-fatal notice on
                        // the same channel adapter diagnostics ride, for Story 4.8's diagnostics page to
                        // render. The record itself still generated above. [Story 4.2 Task 5]
                        events.AddRange(MapDiagnostics(new[]
                        {
                            new AdapterDiagnostic(AdapterDiagnosticCategory.Unsupported, sourceRelative,
                                "no ADR number derivable from the filename; record rendered unnumbered and sorted last"),
                        }));
                    }
                }

                events.Add(new GenerationEvent(GenerationOutcome.Generated, sourceRelative, sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, sourceRelative, sw.Elapsed, ex.Message));
            }
        }

        // Numbered records keep their numeric order; unnumbered ones sort after them, alphabetically by
        // title — deterministic without inventing a number. [Story 4.2 Task 2]
        _adrs = entries
            .OrderBy(e => e.Number is null)
            .ThenBy(e => e.Number)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return events;
    }

    // Matches common ADR template-scaffolding stems: "template", "adr-template"/"adr_template", and a
    // numbered variant like "0000-template" — an optional leading digits+separator, an optional "adr"
    // token+separator, then "template". [Review][Patch]
    private static readonly Regex AdrTemplateStemPattern = new(@"^(?:\d+[-_])?(?:adr[-_])?template$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True when an ADR-tree file (path relative to the ADR root) is a decision RECORD — i.e. a card
    /// on the home index and a hit for the <see cref="AdrsExist"/> nav gate. READMEs (landing/index prose at
    /// any depth) and template scaffolding files still render as pages so cross-links resolve, but are never
    /// records — the same treatment TEMPLATE.md always had. [Story 4.2 Task 2]</summary>
    private static bool IsAdrRecordFile(string relativeToRoot)
    {
        if (string.Equals(Path.GetFileName(relativeToRoot), "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(relativeToRoot);
        return !AdrTemplateStemPattern.IsMatch(stem);
    }

    /// <summary>Emits one <c>commits/{yyyy-MM-dd}.html</c> page per linked day (the exact set the heatmap
    /// links to, via <see cref="Charts.LinkedCommitDays"/>), each listing that day's commits with prev/next
    /// links to the adjacent active days. Mirrors <see cref="GenerateAdrsInternal"/>: wipe+recreate the dir,
    /// render a bespoke page, run reference-linkification so "Story N.M"/"FR25" mentions in subjects become
    /// links, and write.</summary>
    private List<GenerationEvent> GenerateCommitDaysInternal(GitPulse git, SiteNav nav)
    {
        var events = new List<GenerationEvent>();

        var commitsDir = Path.Combine(_options.OutputRoot, "commits");
        if (Directory.Exists(commitsDir))
        {
            Directory.Delete(commitsDir, recursive: true);
        }

        var days = Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, DateOnly.FromDateTime(DateTime.Now));
        if (days.Count == 0) return events;

        Directory.CreateDirectory(commitsDir);
        var entries = new List<CommitDayEntry>();
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var sw = Stopwatch.StartNew();
            var outputRelative = PathUtil.NormalizeSlashes($"commits/{Charts.D(day)}.html");
            try
            {
                var prevDay = i > 0 ? days[i - 1] : (DateOnly?)null;
                var nextDay = i < days.Count - 1 ? days[i + 1] : (DateOnly?)null;
                var html = CommitDayTemplater.RenderPage(day, git.CommitsByDay[day], prevDay, nextDay, nav);

                WriteOutput(outputRelative, ApplyReferenceLinks(html, outputRelative));

                entries.Add(new CommitDayEntry(day, outputRelative));
                events.Add(new GenerationEvent(GenerationOutcome.Generated, outputRelative, sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, outputRelative, sw.Elapsed, ex.Message));
            }
        }

        _commitDays = entries;
        return events;
    }

    /// <summary>Writes the aggregate <c>git-insights.html</c> hub when — and only when — the opt-in deep-git
    /// pass produced its hub aggregates (<see cref="DeepGitPulse.Insights"/>). A no-op otherwise: with
    /// <c>--deep-git</c> off the deep pass never ran, <c>DeepGit</c> is null, and no hub work (or page)
    /// happens at all — that gate IS the AC #2 performance guarantee. Mirrors the deep-analytics page's
    /// non-fatal contract: a render/write failure logs an <see cref="GenerationOutcome.Error"/> event, clears
    /// <see cref="DeepGitPulse.Insights"/> so the dashboard's "View all git insights" link can't dangle, and
    /// never fails the run (AD-4 / NFR2). [Story 3.8]</summary>
    private void GenerateGitInsightsInternal(SiteNav nav, List<GenerationEvent> events, IGenerationReporter? reporter)
    {
        if (_progress?.DeepGit is not { Insights: { } insights }) return;

        reporter?.BeginPhase(GenerationPhase.GitInsights);
        var sw = Stopwatch.StartNew();
        try
        {
            var html = GitInsightsTemplater.RenderPage(insights, _progress.Git, nav);
            WriteOutput(SiteNav.GitInsightsOutputPath, ApplyReferenceLinks(html, SiteNav.GitInsightsOutputPath));
            events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.GitInsightsOutputPath, sw.Elapsed));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.GitInsightsOutputPath, sw.Elapsed, ex.Message));
            // The page was never written — clear Insights so the dashboard's "View all git insights" link
            // (gated on _progress.DeepGit.Insights) doesn't point at a page that doesn't exist.
            _progress.DeepGit.Insights = null;
        }
        reporter?.EndPhase(GenerationPhase.GitInsights);
    }

    /// <summary>Renders every epics-phase page (epics.html, requirements pages, per-epic and per-story pages)
    /// from the adapter-ingested models — the render half of what used to be one inline parse+render pass in
    /// this class. Every templater call and write target is unchanged (AC #1: template and page generators
    /// remain framework-agnostic); only where the models come from moved. Note the per-story artifact
    /// FRAGMENT extraction (task list, blurb/remainder split, AC/dev-record sections) deliberately stays in
    /// this render loop: those calls produce page-shaped HTML fragments on demand, and re-seating them is a
    /// contract question for Story 4.2/6.1, not this seam. [Story 4.1]</summary>
    private List<GenerationEvent> RenderEpicsPages(
        string epicsFullPath,
        List<string> files,
        IReadOnlyDictionary<string, string> artifactMap,
        EpicsModel model,
        RequirementsModel requirements,
        ProgressModel progress,
        SiteNav nav)
    {
        var events = new List<GenerationEvent>();
        var sw = Stopwatch.StartNew();
        var epicsSourceRelative = ToSourceRelative(epicsFullPath);

        try
        {
            var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
            var referenceMap = BuildReferenceMap(files, model, artifactMap, PathUtil.NormalizeSlashes(epicsSourceRelative));

            // Cached for the webview delivery path (Story 6.4): RenderWebviewSurfaces re-renders these same
            // epics/story pages through the WebviewRenderAdapter from the generator's cached models, and needs
            // the artifact + reference maps this render loop already resolved. Both full and watch-mode passes
            // come through here, so the cache can never lag the rendered site.
            _storyArtifactsById = artifactMap;
            _referenceMap = referenceMap;

            File.WriteAllText(Path.Combine(_options.OutputRoot, "epics.html"), ApplyReferenceLinks(EpicsTemplater.RenderIndex(model, progress, nav, _module.Commands), "epics.html"));

            // Rebuild the epics output dir each pass so a story removed or renumbered in epics.md — or an
            // undrafted story that got a placeholder and then vanished — can't leave a stale page behind,
            // mirroring the ADR output dir's rebuild. GenerateAll already wiped OutputRoot (no-op here); this
            // matters for watch-mode RegenerateEpics, which doesn't wipe the whole tree.
            var epicsDir = Path.Combine(_options.OutputRoot, "epics");
            if (Directory.Exists(epicsDir)) Directory.Delete(epicsDir, recursive: true);
            Directory.CreateDirectory(epicsDir);

            WriteRequirements(requirements, model, progress, nav);

            foreach (var epic in model.Epics)
            {
                var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                File.WriteAllText(Path.Combine(epicsDir, $"epic-{epic.Number}.html"), ApplyReferenceLinks(EpicsTemplater.RenderEpic(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath), $"epics/epic-{epic.Number}.html", skipEpicNumber: epic.Number));

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null)
                    {
                        // Undrafted story: emit a placeholder page at the exact path its real page will
                        // use, so "Story N.M" mentions always have a live target and a later-drafted
                        // artifact overwrites it in place. ArtifactOutputPath stays null — placeholders
                        // must never count as detailed stories anywhere progress is computed.
                        var placeholderPath = StoryEpicLinkifier.StoryPagePath(story.Id);
                        var placeholderHtml = EpicsTemplater.RenderStoryPlaceholder(epic, story, nav, _module.Commands, epicRetroPath);
                        File.WriteAllText(Path.Combine(_options.OutputRoot, placeholderPath.Replace('/', Path.DirectorySeparatorChar)), ApplyReferenceLinks(placeholderHtml, placeholderPath, skipStoryId: story.Id));
                        continue;
                    }

                    // story.Status/TasksDone were filled by ProgressCalculator above — no re-read needed.
                    var f = BuildStoryPageFragments(story, artifactMap[story.Id], referenceMap);
                    var storyHtml = EpicsTemplater.RenderStory(epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria, f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, nav, _module.Commands, epicRetroPath);
                    File.WriteAllText(Path.Combine(_options.OutputRoot, "epics", $"story-{story.Id.Replace('.', '-')}.html"), ApplyReferenceLinks(storyHtml, story.ArtifactOutputPath!, skipStoryId: story.Id));
                }
            }

            events.Add(new GenerationEvent(GenerationOutcome.Generated, epicsSourceRelative, sw.Elapsed, $"{model.Epics.Count} epics"));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, epicsSourceRelative, sw.Elapsed, ex.Message));
        }

        return events;
    }

    /// <summary>The per-story artifact fragments a drafted story page renders — the extraction + linkify pipeline
    /// that lived inline in <see cref="RenderEpicsPages"/>, re-homed so the webview delivery path
    /// (<see cref="RenderWebviewSurfaces"/>) composes the story page from the IDENTICAL fragments rather than a
    /// drift-prone copy. [Story 6.4]</summary>
    private sealed record StoryPageFragments(
        string ArtifactRelative,
        string BlurbHtml,
        string RemainderHtml,
        IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria,
        IReadOnlyList<(string Label, string ContentHtml)> DevAgentRecord,
        IReadOnlyList<TaskItem> Tasks,
        string ReviewFindingsHtml,
        string ChangeLogHtml);

    /// <summary>Reads one drafted story's artifact and produces its page fragments (task list, blurb/remainder
    /// split, AC / dev-record / review / change-log sections), source-citation-linkified against
    /// <paramref name="referenceMap"/> and with "(AC: #N)" plan references deep-linked. A verbatim re-homing of
    /// the fragment block from <see cref="RenderEpicsPages"/> — bytes unchanged (the golden regression is the
    /// gate). [Story 4.1 fragments; re-homed Story 6.4]</summary>
    private StoryPageFragments BuildStoryPageFragments(StoryInfo story, string artifactFullPath, Dictionary<string, string> referenceMap)
    {
        var artifactRelative = ToSourceRelative(artifactFullPath);
        var artifactRaw = MarkdownConverter.ReadAllTextShared(artifactFullPath);
        var tasks = TaskListParser.Parse(artifactRaw);
        var (blurbHtml, remainderHtml) = EpicsParser.SplitStoryArtifact(artifactRaw);
        var acceptanceCriteria = EpicsParser.ExtractAcceptanceCriteria(artifactRaw);
        var devAgentRecord = EpicsParser.ExtractDevAgentRecord(artifactRaw);
        var reviewFindingsHtml = EpicsParser.ExtractNamedSectionHtml(artifactRaw, "## Review Findings");
        var changeLogHtml = EpicsParser.ExtractNamedSectionHtml(artifactRaw, "## Change Log");

        // Turn "[Source: _bmad-output/path.md]" citations into real links to the generated page. Only drafted
        // stories reach here (both callers guard on ArtifactOutputPath before resolving the artifact path).
        var storyPrefix = PathUtil.RelativePrefix(story.ArtifactOutputPath!);
        blurbHtml = SourceLinkifier.Linkify(blurbHtml, referenceMap, storyPrefix);
        remainderHtml = SourceLinkifier.Linkify(remainderHtml, referenceMap, storyPrefix);
        reviewFindingsHtml = SourceLinkifier.Linkify(reviewFindingsHtml, referenceMap, storyPrefix);
        changeLogHtml = SourceLinkifier.Linkify(changeLogHtml, referenceMap, storyPrefix);
        acceptanceCriteria = acceptanceCriteria
            .Select(ac => ac with { Html = SourceLinkifier.Linkify(ac.Html, referenceMap, storyPrefix) })
            .ToList();
        devAgentRecord = devAgentRecord
            .Select(e => (e.Label, ContentHtml: SourceLinkifier.Linkify(e.ContentHtml, referenceMap, storyPrefix)))
            .ToList();

        // Deep-link every "(AC: #N)" reference in the plan to its criterion panel above.
        var criteriaByNumber = acceptanceCriteria.ToDictionary(ac => ac.Number, ac => ac.PlainText);
        remainderHtml = EpicsParser.LinkifyAcReferences(remainderHtml, criteriaByNumber);

        return new StoryPageFragments(
            artifactRelative, blurbHtml, remainderHtml, acceptanceCriteria, devAgentRecord, tasks,
            reviewFindingsHtml, changeLogHtml);
    }

    /// <summary>The artifact + reference maps the last epics render pass resolved, cached for
    /// <see cref="RenderWebviewSurfaces"/> (both <see cref="GenerateAll"/> and <see cref="RegenerateEpics"/>
    /// route through <see cref="RenderEpicsPages"/>, which refreshes them). [Story 6.4]</summary>
    private IReadOnlyDictionary<string, string> _storyArtifactsById = new Dictionary<string, string>();
    private Dictionary<string, string> _referenceMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Renders the webview's navigable surface set — dashboard, epics index, every epic page, and every
    /// story page/placeholder (the five Story 6.2 surface families) — through the
    /// <see cref="WebviewRenderAdapter"/>, from the SAME cached models, builders, and fragment pipeline the HTML
    /// site was generated from. This is the Story 6.4 delivery seam ADR 0005 ratified: the extension gets
    /// finished, CSP-safe HTML produced entirely in C#; it re-parses no markdown and scrapes no generated site
    /// (AD-1/AD-2). Requires a completed <see cref="GenerateAll"/> pass on this instance (the webview CLI command
    /// runs one first); a pure READ of cached state plus source story artifacts — it writes nothing (AC #6).
    /// [Story 6.4]</summary>
    public WebviewBundle RenderWebviewSurfaces()
    {
        lock (_gate)
        {
            var nav = _nav ?? throw new InvalidOperationException(
                "RenderWebviewSurfaces requires a completed GenerateAll() pass on this generator.");

            var surfaces = new List<WebviewSurface>();
            // Built in lock-step with the surfaces below so every outline SurfacePath is captured from the SAME
            // PageView the surface is rendered from — never re-derived — guaranteeing it matches a surfaces[...]
            // key a tree click can push() to (Story 6.9 fact #5). Data only; no HTML, no re-parse.
            var outlineEpics = new List<OutlineEpic>();

            // Dashboard — the same inputs WriteIndex hands the templater, so the webview dashboard can never
            // disagree with the generated index.html.
            var docs = _docs.Values.ToList();
            var dashboardPage = HtmlTemplater.BuildIndexPage(
                docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands,
                WorkInventory.Build(docs), _sprint, _retros, _coverage);
            surfaces.Add(WebviewSurfaceFor(dashboardPage));

            // Epics family — mirrors RenderEpicsPages' iteration exactly (same retro map, same per-epic
            // progress, same placeholder rule, same fragment pipeline), rendered to webview content instead of
            // written to disk.
            if (_epicsModel is { } model && _progress is { } progress)
            {
                var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
                surfaces.Add(WebviewSurfaceFor(EpicsTemplater.BuildIndexPage(model, progress, nav, _module.Commands)));

                foreach (var epic in model.Epics)
                {
                    var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                    var epicPage = EpicsTemplater.BuildEpicPage(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath);
                    surfaces.Add(WebviewSurfaceFor(epicPage, skipEpicNumber: epic.Number));

                    var outlineStories = new List<OutlineStory>();
                    foreach (var story in epic.Stories)
                    {
                        PageView storyPage;
                        if (story.ArtifactOutputPath is null || !_storyArtifactsById.TryGetValue(story.Id, out var artifactFullPath))
                        {
                            storyPage = EpicsTemplater.BuildStoryPlaceholderPage(epic, story, nav, _module.Commands, epicRetroPath);
                        }
                        else
                        {
                            var f = BuildStoryPageFragments(story, artifactFullPath, _referenceMap);
                            storyPage = EpicsTemplater.BuildStoryPage(
                                epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria,
                                f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, nav,
                                _module.Commands, epicRetroPath);
                        }
                        surfaces.Add(WebviewSurfaceFor(storyPage, skipStoryId: story.Id));

                        var storyStage = StatusStyles.ForStory(story);
                        outlineStories.Add(new OutlineStory(
                            story.Id, story.Title, storyStage, StatusStyles.StoryLabel(storyStage),
                            PathUtil.NormalizeSlashes(storyPage.OutputRelativePath),
                            story.ArtifactSourcePath,
                            story.TasksDone, story.TasksTotal,
                            BmadCommands.PrimaryStoryCommand(story, _module.Commands)));
                    }

                    var epicStage = StatusStyles.ForEpicWithRetrospective(epic);
                    outlineEpics.Add(new OutlineEpic(
                        epic.Number, epic.Title, epicStage, StatusStyles.EpicLabel(epicStage),
                        PathUtil.NormalizeSlashes(epicPage.OutputRelativePath),
                        StoriesTotal: epic.Stories.Count,
                        StoriesDone: epic.Stories.Count(s => StatusStyles.ForStory(s) == "done"),
                        outlineStories));
                }
            }

            var outline = new ProjectOutline(outlineEpics, BuildOutlineSummary(outlineEpics));

            // The entry document embeds the dashboard's ALREADY-linkified content, so wrapping happens after
            // linkification and the linkifier never walks the shell's CSS/bridge-script text.
            var entry = surfaces[0];
            var entryDocument = WebviewRenderAdapter.Shared.WrapDocument(dashboardPage, entry.ContentHtml);
            return new WebviewBundle(_options.SiteTitle, entry.OutputRelativePath, entryDocument, surfaces, outline);
        }
    }

    /// <summary>Tallies the status-bar summary from the assembled outline — stories by stage across all epics,
    /// computed core-side so the shim does no counting (Story 6.9, R3.2). Routes every count through the stage
    /// strings <see cref="StatusStyles.ForStory"/> already produced (carried on each <see cref="OutlineStory"/>),
    /// so it can never disagree with the tree's per-node icons.</summary>
    private static OutlineSummary BuildOutlineSummary(IReadOnlyList<OutlineEpic> epics)
    {
        var stages = epics.SelectMany(e => e.Stories).Select(s => s.Stage).ToList();
        return new OutlineSummary(
            Active: stages.Count(s => s == "active"),
            Review: stages.Count(s => s == "review"),
            Done: stages.Count(s => s == "done"),
            Total: stages.Count);
    }

    /// <summary>Renders one page's webview content region and reference-linkifies it with the same skip rules
    /// the HTML surface uses (a page never self-links its own story/epic mentions). [Story 6.4]</summary>
    private WebviewSurface WebviewSurfaceFor(PageView page, string? skipStoryId = null, int? skipEpicNumber = null)
    {
        var content = ApplyReferenceLinks(
            WebviewRenderAdapter.Shared.RenderContent(page), page.OutputRelativePath,
            skipStoryId: skipStoryId, skipEpicNumber: skipEpicNumber);
        return new WebviewSurface(PathUtil.NormalizeSlashes(page.OutputRelativePath), page.Title, content);
    }

    // ===== Story 6.7: JSON + client-renderer (SPA) delivery form =============================================

    /// <summary>The page write seam for pages the SPA form consolidates: writes <paramref name="html"/> to
    /// <paramref name="outputRelativePath"/> under the output root (creating parent dirs), and — ONLY when
    /// <c>--spa</c> is active — captures the finished page string in memory (<see cref="_spaCapture"/>) for the SPA
    /// bundle. The capture consumes the render pipeline's OWN output at the instant it is produced, one step before
    /// it becomes a file: it never reads a generated <c>.html</c> back off disk and never re-parses a source
    /// <c>.md</c>, so AD-1/AD-2 hold (Story 6.7 Dev Notes "Is landmark-slicing scraping?"). The dashboard/epics
    /// families deliberately do NOT route through here — the SPA re-renders them from their view models (strongest
    /// parity, see <see cref="BuildSpaBundle"/>). Bytes written are identical to the prior direct
    /// <c>File.WriteAllText</c>, so the golden gate is unaffected (AC #5). [Story 6.7]</summary>
    private void WriteOutput(string outputRelativePath, string html)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(full, html);
        if (_spaCapture is not null)
        {
            _spaCapture[PathUtil.NormalizeSlashes(outputRelativePath)] = html;
        }
    }

    /// <summary>Builds the whole-site SPA bundle: the five dashboard/epics families rendered through their view
    /// models (the same strongest-parity path the webview uses), plus EVERY other page's content region sliced from
    /// the render output captured at the write seam (<see cref="_spaCapture"/>) via the universal
    /// <c>&lt;main id="main-content"&gt;</c> landmark. Requires a completed capture pass (only populated under
    /// <c>--spa</c>). A pure read of cached state + captured strings — it writes nothing (AC #6). [Story 6.7]</summary>
    public SpaBundle RenderSpaBundle()
    {
        lock (_gate)
        {
            var nav = _nav ?? throw new InvalidOperationException(
                "RenderSpaBundle requires a completed GenerateAll() pass with --spa on this generator.");
            return BuildSpaBundle(nav);
        }
    }

    private SpaBundle BuildSpaBundle(SiteNav nav)
    {
        var pages = new List<SpaPage>();
        var familyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Dashboard + epics families via their view models — true view-model renders, so section-fact parity is
        // airtight (AC #4). Mirrors RenderWebviewSurfaces' iteration exactly (same models, retro map, placeholder
        // rule, fragment pipeline).
        var docs = _docs.Values.ToList();
        var dashboardPage = HtmlTemplater.BuildIndexPage(
            docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands,
            WorkInventory.Build(docs), _sprint, _retros, _coverage);
        AddSpaSurface(pages, familyPaths, dashboardPage);

        if (_epicsModel is { } model && _progress is { } progress)
        {
            var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
            AddSpaSurface(pages, familyPaths, EpicsTemplater.BuildIndexPage(model, progress, nav, _module.Commands));

            foreach (var epic in model.Epics)
            {
                var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                AddSpaSurface(pages, familyPaths,
                    EpicsTemplater.BuildEpicPage(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath),
                    skipEpicNumber: epic.Number);

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null || !_storyArtifactsById.TryGetValue(story.Id, out var artifactFullPath))
                    {
                        AddSpaSurface(pages, familyPaths,
                            EpicsTemplater.BuildStoryPlaceholderPage(epic, story, nav, _module.Commands, epicRetroPath),
                            skipStoryId: story.Id);
                        continue;
                    }

                    var f = BuildStoryPageFragments(story, artifactFullPath, _referenceMap);
                    AddSpaSurface(pages, familyPaths,
                        EpicsTemplater.BuildStoryPage(
                            epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria,
                            f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, nav,
                            _module.Commands, epicRetroPath),
                        skipStoryId: story.Id);
                }
            }
        }

        // 2) Every OTHER captured page: slice its content region via the landmark. Families are already covered
        // above (skipped here). The nav is re-rendered fresh (byte-identical to the page's own, minus the inline
        // toggle script the client owns); the breadcrumb + <main> come from the page's own captured output. The
        // breadcrumb is ALSO recovered structurally (from that same captured string — never re-read from disk)
        // so the manifest's drill parent/child data covers the whole site, not just the 5 view-model families.
        if (_spaCapture is { } capture)
        {
            foreach (var (path, fullHtml) in capture)
            {
                var normalized = PathUtil.NormalizeSlashes(path);
                if (familyPaths.Contains(normalized)) continue;
                var navMarkup = HtmlRenderAdapter.Shared.RenderNavMarkup(nav.ToNavigationView(normalized));
                var region = SpaDelivery.ExtractContentRegion(fullHtml, navMarkup);
                var breadcrumb = SpaDelivery.ExtractBreadcrumb(fullHtml, normalized);
                pages.Add(new SpaPage(normalized, SpaDelivery.ExtractTitle(fullHtml), region, breadcrumb));
            }
        }

        return new SpaBundle(_options.SiteTitle, "index.html", nav.Items, pages);
    }

    /// <summary>Renders one dashboard/epics family page's SPA content region (nav + breadcrumb + body) through
    /// <see cref="JsonSpaRenderAdapter"/>, reference-linkified with the SAME skip rules the static page uses (a page
    /// never self-links its own story/epic mentions), records the path as a family (so the landmark-slice pass skips
    /// it), and appends it to the bundle. [Story 6.7]</summary>
    private void AddSpaSurface(List<SpaPage> pages, HashSet<string> familyPaths, PageView page,
        string? skipStoryId = null, int? skipEpicNumber = null)
    {
        var region = ApplyReferenceLinks(
            JsonSpaRenderAdapter.Shared.RenderContent(page), page.OutputRelativePath,
            skipStoryId: skipStoryId, skipEpicNumber: skipEpicNumber);
        var path = PathUtil.NormalizeSlashes(page.OutputRelativePath);
        familyPaths.Add(path);
        pages.Add(new SpaPage(path, page.Title, region, page.Breadcrumb.Crumbs));
    }

    /// <summary>Writes the opt-in SPA delivery files — the client script, the manifest + content chunks, and the
    /// entry shell — ALL under <see cref="ForgeOptions.OutputRoot"/>, strictly alongside the untouched static site
    /// (AC #3/#5/#6). Called at the end of a full generate and after each incremental watch update when <c>--spa</c>
    /// is on. Never touches a source artifact or an existing static page. [Story 6.7]</summary>
    private void EmitSpaSite(SiteNav nav)
    {
        var bundle = BuildSpaBundle(nav);
        var dataFiles = SpaDelivery.BuildDataFiles(bundle);

        // Guard against a real doc's output path colliding with one of the SPA form's own reserved paths (e.g. a
        // doc slug that happens to render to "app.html" or "spa/pages-root.json"): writing anyway would silently
        // overwrite either the legitimate static page or the SPA's own delivery file with no diagnostic. Loud and
        // early beats a corrupted output tree. [Story 6.7 review]
        var reservedPaths = dataFiles.Select(f => f.OutputRelativePath)
            .Append(SpaDelivery.ScriptName)
            .Append(SpaDelivery.EntryFileName);
        var bundlePaths = new HashSet<string>(bundle.Pages.Select(p => p.OutputRelativePath), StringComparer.OrdinalIgnoreCase);
        var collision = reservedPaths.FirstOrDefault(bundlePaths.Contains);
        if (collision is not null)
        {
            throw new InvalidOperationException(
                $"--spa cannot be emitted: a generated page already claims the reserved SPA output path "
                + $"'{collision}'. Rename the conflicting source artifact so its output path no longer collides "
                + "with the SPA delivery form's own files (app.html, specscribe-spa.js, spa/*.json).");
        }

        // The client renderer, embedded and copied exactly like specscribe.css/js.
        CopyEmbeddedAsset("SpecScribe.assets.specscribe-spa.js", SpaDelivery.ScriptName);

        // Manifest + bounded content chunks.
        foreach (var file in dataFiles)
        {
            WriteSpaFile(file.OutputRelativePath, file.Content);
        }

        // The entry shell inlines the dashboard region for instant first paint AND the no-JS fallback (AC #2).
        var entryRegion = bundle.Pages.First(p => p.OutputRelativePath == bundle.EntryPath).ContentHtml;
        WriteSpaFile(SpaDelivery.EntryFileName, SpaDelivery.BuildEntryShell(bundle.SiteTitle, entryRegion));
    }

    /// <summary>Writes one SPA output file under the output root (creating parent dirs). Unlike
    /// <see cref="WriteOutput"/> this never captures — SPA files are the delivery output, not site pages to
    /// consolidate. [Story 6.7]</summary>
    private void WriteSpaFile(string outputRelativePath, string content)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
    }

    private GenerationEvent GenerateOneInternal(string sourceFullPath, SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        var relative = ToSourceRelative(sourceFullPath);

        if (IsIgnored(sourceFullPath))
        {
            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "ignored file");
        }

        if (!File.Exists(sourceFullPath))
        {
            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "no longer exists");
        }

        try
        {
            var alreadyExisted = _docs.ContainsKey(relative);
            var outputRelative = PathUtil.ToOutputRelative(relative);
            var doc = MarkdownConverter.Convert(sourceFullPath, relative, outputRelative);
            doc.Companions = ResolveSpecCompanions(doc);

            WriteOutput(outputRelative, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));

            _docs[relative] = doc;
            var outcome = alreadyExisted ? GenerationOutcome.Updated : GenerationOutcome.Generated;
            return new GenerationEvent(outcome, relative, sw.Elapsed);
        }
        catch (IOException)
        {
            // Mid-write from an editor/BMad tool — the watcher's next debounced event will retry.
            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "file busy, will retry");
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, relative, sw.Elapsed, ex.Message);
        }
    }

    private void WriteIndex(SiteNav nav, WorkInventory? work = null)
    {
        var indexPath = Path.Combine(_options.OutputRoot, "index.html");
        var docs = _docs.Values.ToList();
        var inventory = work ?? WorkInventory.Build(docs);
        var html = HtmlTemplater.RenderIndex(docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands, inventory, _sprint, _retros, _coverage);
        File.WriteAllText(indexPath, ApplyReferenceLinks(html, "index.html"));
    }

    /// <summary>The projection-side enrichment handed to the adapter (see <see cref="ProgressProjection"/>):
    /// task/story roll-ups plus the git pulse, computed on the freshly ingested epics model. Kept in the
    /// generator — never the adapter — so insight enrichment stays additive and non-blocking (AD-4).
    /// Git is invoked once per ingest, not per-page. [Story 4.1]</summary>
    private ProgressModel ComputeProgress(EpicsModel model, IReadOnlyDictionary<string, string> artifactsById)
    {
        var gitPulse = GitMetrics.TryCompute(_options.RepoRoot);
        // Deep git analytics are strictly opt-in: when the flag is off this ternary short-circuits so
        // TryComputeDeep — and its extra git process — never runs, and baseline generation timing cannot
        // regress. That gate IS the FR-10 performance guarantee (AC #1). [Story 3.2]
        var deepGit = _options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(_options.RepoRoot) : null;
        return ProgressCalculator.Compute(model, artifactsById, gitPulse, deepGit);
    }

    /// <summary>Surfaces adapter diagnostics on the existing event/reporter channel: malformed artifacts and
    /// ingest errors report as <see cref="GenerationOutcome.Error"/> (exactly how per-file parse failures
    /// always reported), unsupported/skipped shapes as <see cref="GenerationOutcome.Skipped"/>. Always
    /// non-fatal — the run has already continued past whatever these describe (AC #2). [Story 4.1]
    /// <para>The message is prefixed with the fine <see cref="AdapterDiagnosticCategory"/> word (e.g.
    /// <c>[Unsupported]</c>) so the coarse <see cref="GenerationOutcome"/> collapse (four categories → two
    /// outcomes) doesn't lose the distinction the Story 4.8 diagnostics page shows. Additive and harmless on
    /// the console path (which already prints messages); recovered by
    /// <see cref="DiagnosticsTemplater"/> without needing a second channel. [Story 4.8 Task 2]</para></summary>
    private static IEnumerable<GenerationEvent> MapDiagnostics(IReadOnlyList<AdapterDiagnostic> diagnostics) =>
        diagnostics.Select(d => new GenerationEvent(
            d.Category is AdapterDiagnosticCategory.Malformed or AdapterDiagnosticCategory.Error
                ? GenerationOutcome.Error
                : GenerationOutcome.Skipped,
            d.RelativePath, TimeSpan.Zero, $"[{d.Category}] {d.Message}", FromAdapterDiagnostic: true));

    /// <summary>One <see cref="AdapterDiagnosticCategory.Unsupported"/> notice per top-level source folder
    /// outside the well-known home-index set — the "unrecognized structure degrades, visibly" half of the
    /// grouping contract. Derived from the source tree (not the rendered bands) so a folder whose docs were
    /// all consumed into dedicated surfaces still reports its unrecognized shape once. A top-level folder
    /// whose files are ENTIRELY under a nested implementation-artifacts segment (e.g.
    /// <c>tracking/implementation-artifacts/1-1-x.md</c>) is excluded: those docs land in the known
    /// Implementation Artifacts band (see <see cref="HtmlTemplater.RenderIndex"/>'s ancestor-tolerant match),
    /// so flagging the wrapper folder as "unrecognized" would contradict Task 4's location tolerance and the
    /// notice's own claim that the docs "render in their own home-index section." [Story 4.2 Task 5] [Review][Patch]</summary>
    private static IReadOnlyList<AdapterDiagnostic> UnrecognizedTopLevelFolders(IReadOnlyList<string> sourceRelatives)
    {
        var normalized = sourceRelatives.Select(PathUtil.NormalizeSlashes).ToList();
        return normalized
            .Where(p => p.Contains('/'))
            .Select(p => p[..p.IndexOf('/')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !HtmlTemplater.IsWellKnownTopLevelFolder(f))
            .Where(f => !normalized
                .Where(p => p.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase))
                .All(BmadArtifactAdapter.IsUnderImplementationArtifacts))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported, f + "/",
                "unrecognized top-level folder; its documents render in their own home-index section"))
            .ToList();
    }

    /// <summary>Caches the adapter-ingested retros (already ordered by epic, then filename) so
    /// <see cref="EpicRetroMap"/> is available to the epic/story pages and the sprint/home surfaces. The
    /// dedicated pages are written later by <see cref="RenderRetroPages"/>. [Story 2.3 retro pages; ingest
    /// moved behind the adapter in Story 4.1]</summary>
    private void SetRetros(IReadOnlyList<RetroModel> retros)
    {
        _retros = retros.ToList();
        // Computed once here rather than on every access (EpicRetroMap used to be a computed property
        // re-grouping all retros on every epic in the epics render loop). [Story 2.3 review]
        _epicRetroMap = _retros.GroupBy(r => r.EpicNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase).First().OutputRelativePath);
    }

    /// <summary>Stamps each epic's <see cref="EpicInfo.HasRetrospective"/> from <see cref="EpicRetroMap"/> — the
    /// SAME signal the epic/story pages' retro link reads — so the retro-gated "In review" tier
    /// (<see cref="StatusStyles.ForEpicWithRetrospective"/>) and those links can never disagree. Called after the
    /// epics model is (re)cached in BOTH <see cref="GenerateAll"/> and the incremental <see cref="RegenerateEpics"/>,
    /// so a watch-mode rebuild doesn't reset the flag to false and wrongly downgrade a retro'd epic to "In review".
    /// [spec-sunburst-retro]</summary>
    private void TagEpicRetrospectives()
    {
        if (_epicsModel is null) return;
        foreach (var epic in _epicsModel.Epics)
        {
            epic.HasRetrospective = _epicRetroMap.ContainsKey(epic.Number);
        }
    }

    /// <summary>Writes each parsed retrospective into its dedicated <see cref="RetroTemplater"/> page (at the
    /// same <c>implementation-artifacts/…html</c> path the generic pipeline would have used, so existing links
    /// resolve), reference-linkified like every page. Runs after the epics phase — the page needs the epics
    /// model for its epic link and "Stories in this Epic" section. [Story 2.3 retro pages]</summary>
    private void RenderRetroPages(SiteNav nav)
    {
        foreach (var retro in _retros)
        {
            var outputRel = retro.OutputRelativePath;
            WriteOutput(outputRel, ApplyReferenceLinks(RetroTemplater.RenderPage(retro, _epicsModel, nav), outputRel));
        }
    }

    /// <summary>Maps an epic number to the output path of its (latest, by filename) retrospective page — the
    /// link target for an open action item tagged with that epic. Computed once in <see cref="SetRetros"/>.
    /// [Story 2.3 retro pages]</summary>
    private IReadOnlyDictionary<int, string> _epicRetroMap = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, string> EpicRetroMap => _epicRetroMap;

    /// <summary>True once the sprint tracking file parsed into usable data — the single signal the sprint
    /// page, home widget, and nav item all gate on (a present-but-malformed file parses to null and omits
    /// everywhere, matching NFR2 graceful degradation). [Story 2.3 Task 5]</summary>
    private bool SprintAvailable => _sprint is not null;

    /// <summary>Writes <c>sprint.html</c> from the cached parsed sprint status, reusing the epics model for
    /// real titles/links. Reference-linkified like every other page. Omitted entirely (no page) when there is
    /// no sprint data. [Story 2.3 Task 3/5]</summary>
    private void WriteSprint(SiteNav nav)
    {
        if (_sprint is null) return;
        var html = SprintTemplater.RenderIndex(_sprint, _epicsModel, nav, _module.Commands, _retros);
        WriteOutput(SiteNav.SprintOutputPath, ApplyReferenceLinks(html, SiteNav.SprintOutputPath));
    }

    /// <summary>Writes <c>structure.html</c> — the interactive project/artifact structure tree. Gated on the
    /// same source-artifact signal as its nav item (<c>sourceRelatives.Count &gt; 0</c>) so the page and its link
    /// are one signal; a project with no <c>_bmad-output</c> <c>*.md</c> files omits both. Builds the
    /// output-href map from the fully-populated <see cref="_docs"/> (plus the special <c>epics.md → epics.html</c>
    /// mapping) so navigable leaves route to real pages and non-navigable ones render as plain text — never a
    /// broken link. The whole gather+build is wrapped never-throw → <see cref="ProjectTree.Empty"/> so any failure
    /// degrades to omission and generation still succeeds (AD-4 / NFR2). [Story 3.4 Task 3]</summary>
    private void WriteStructure(SiteNav nav, IReadOnlyList<string> sourceRelatives)
    {
        if (sourceRelatives.Count == 0) return;

        ProjectTree tree;
        try
        {
            tree = ProjectTree.Build(sourceRelatives, BuildStructureHrefMap(sourceRelatives));
        }
        catch (Exception)
        {
            tree = ProjectTree.Empty;
        }

        if (tree.IsEmpty) return;

        var html = RenderStructurePage(tree, nav);
        WriteOutput(SiteNav.StructureOutputPath, ApplyReferenceLinks(html, SiteNav.StructureOutputPath));
    }

    /// <summary>Maps each source-artifact path that has a generated page to its output-relative URL, from the
    /// already-populated <see cref="_docs"/> (source→output) plus <c>epics.md → epics.html</c> (which renders
    /// specially and is never a generic <see cref="_docs"/> entry). Consumed story artifacts and any file with no
    /// generated page are simply absent, so <see cref="ProjectTree"/> renders them as plain text. [Story 3.4]</summary>
    private IReadOnlyDictionary<string, string> BuildStructureHrefMap(IReadOnlyList<string> sourceRelatives)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in _docs.Values)
        {
            map[PathUtil.NormalizeSlashes(doc.SourceRelativePath)] = PathUtil.NormalizeSlashes(doc.OutputRelativePath);
        }

        // epics.md is consumed into epics.html, so it never appears in _docs — add it only when the epics page
        // was actually produced (epics.md parsed into a model).
        if (_epicsModel is not null)
        {
            var epicsSource = sourceRelatives.FirstOrDefault(BmadArtifactAdapter.IsEpicsFile);
            if (epicsSource is not null)
            {
                map[PathUtil.NormalizeSlashes(epicsSource)] = SiteNav.EpicsOutputPath;
            }
        }

        return map;
    }

    /// <summary>Assembles the standalone <c>structure.html</c> shell — the same <see cref="PathUtil.RenderHeadOpen"/>
    /// + nav + breadcrumb + <c>&lt;main id="main-content"&gt;</c> + footer every other <c>Write*</c> page uses —
    /// with the tree living in a <c>chart-panel</c>. The tree markup itself comes from the pure, no-JS
    /// <see cref="Charts.ProjectStructureTree"/>. [Story 3.4 Subtask 3.3]</summary>
    private string RenderStructurePage(ProjectTree tree, SiteNav nav)
    {
        var outputPath = SiteNav.StructureOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — structure.html is at the output root.

        var fileWord = Charts.Plural(tree.FileCount, "file", "files");
        var dirWord = Charts.Plural(tree.DirectoryCount, "directory", "directories");
        var headline = $"{tree.FileCount} {fileWord} across {tree.DirectoryCount} {dirWord}";

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Project Structure — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Interactive tree of the project and artifact structure for {nav.SiteTitle} — expand and collapse directories and jump to the generated pages."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Structure", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Project Structure</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {PathUtil.Html(headline)}</p>\n\n");
        sb.Append("<section class=\"chart-panel\">\n");
        sb.Append("  <h3>Project &amp; Artifact Structure</h3>\n");
        sb.Append(Charts.ProjectStructureTree(tree));
        sb.Append("</section>\n\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Writes the retrospectives index (<c>retros.html</c>) when any retro exists — the target of the
    /// sprint page's "Retros" link. [Story 2.3 polish #5]</summary>
    private void WriteRetroIndex(SiteNav nav)
    {
        if (_retros.Count == 0) return;
        var html = RetroTemplater.RenderIndex(_retros, nav);
        WriteOutput(SiteNav.RetrosOutputPath, ApplyReferenceLinks(html, SiteNav.RetrosOutputPath));
    }

    /// <summary>Writes the open-action-items page (<c>action-items.html</c>) when the sprint tracks open items —
    /// the target of the sprint page's flag button and the home retro callout. Each item links to its epic's
    /// retro page and offers a quick-dev "Resolve with AI" command. [Story 2.3 polish #5]</summary>
    private void WriteActionItems(SiteNav nav, WorkInventory? work = null)
    {
        var open = _sprint?.OpenActionItems;
        if (open is null || open.Count == 0) return;
        // Debt-related items link to the deferred-work backlog page when one exists (root-relative — this page
        // is at the site root). Reuses the caller's inventory when supplied instead of rebuilding it. [Story 2.3 review]
        var deferredHref = (work ?? WorkInventory.Build(_docs.Values.ToList())).Deferred?.OutputPath;
        // NOT reference-linkified: the "Resolve with AI" data-copy payload embeds the action text (which can
        // contain "Epic N"/"Story N.M" mentions); the linkifier would wrap those in <a> tags INSIDE the
        // attribute value and corrupt the copyable command. [Story 2.3 polish #5]
        var html = ActionItemsTemplater.RenderPage(open, EpicRetroMap, _module.Commands, nav, deferredHref);
        WriteOutput(SiteNav.ActionItemsOutputPath, html);
    }

    /// <summary>Writes <c>diagnostics.html</c> — the whole-run report of the run's non-fatal notices plus the
    /// effective configuration + detection results (AC #1/#2). Projects the notice list off the single
    /// accumulated <paramref name="events"/> list (no double-count) and reads the config from already-resolved
    /// <see cref="_options"/>/<see cref="_module"/> (local-first — no I/O, no remote calls). Deliberately NOT
    /// run through <see cref="ApplyReferenceLinks"/>: an exception message can embed "Story N.M"/"FR-9"
    /// fragments the linkifier would wrap and distort — the same trap <see cref="WriteActionItems"/> avoids.
    /// Returns its own <see cref="GenerationOutcome.Generated"/> event for the run summary / output inventory.
    /// [Story 4.8 Task 6]</summary>
    private GenerationEvent WriteDiagnostics(SiteNav nav, IReadOnlyList<GenerationEvent> events)
    {
        var sw = Stopwatch.StartNew();
        var notices = DiagnosticNotice.FromEvents(events);
        var config = DiagnosticsConfig.FromRun(_options, _module);
        var html = DiagnosticsTemplater.RenderPage(notices, config, nav);
        WriteOutput(SiteNav.DiagnosticsOutputPath, html);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.DiagnosticsOutputPath, sw.Elapsed);
    }

    /// <summary>Writes <c>about.html</c> — SpecScribe's own product-metadata page (version/description/author/
    /// repository, read from the assembly) plus the prominent link to the diagnostics run log. Static (no run
    /// dependency); written alongside the diagnostics page on every full run so the footer's About link always
    /// resolves. Returns its own <see cref="GenerationOutcome.Generated"/> event. [Story 4.8 Task 6]</summary>
    private GenerationEvent WriteAbout(SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        var html = AboutTemplater.RenderPage(nav);
        WriteOutput(SiteNav.AboutOutputPath, html);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.AboutOutputPath, sw.Elapsed);
    }

    /// <summary>Path to the repo-root README that feeds the optional Readme page.</summary>
    private string ReadmeSourcePath => Path.Combine(_options.RepoRoot, "README.md");

    /// <summary>True when a README page should be produced: the feature is enabled and the file exists.
    /// The nav is derived from this so a missing/disabled README simply omits the card and link.</summary>
    private bool ReadmeAvailable => _options.IncludeReadme && File.Exists(ReadmeSourcePath);

    /// <summary>Renders the repo-root README.md into a standalone stylized <c>readme.html</c>. Kept out of
    /// <see cref="_docs"/> so it never doubles up as a document-grid card — it is surfaced only via the nav
    /// and the Readme quick link. Returns null (no event) when the feature is disabled or no README exists.</summary>
    private GenerationEvent? GenerateReadmeInternal(SiteNav nav)
    {
        if (!ReadmeAvailable) return null;

        var sw = Stopwatch.StartNew();
        try
        {
            var doc = MarkdownConverter.Convert(ReadmeSourcePath, "README.md", SiteNav.ReadmeOutputPath);
            WriteOutput(SiteNav.ReadmeOutputPath, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), SiteNav.ReadmeOutputPath));
            return new GenerationEvent(GenerationOutcome.Generated, "README.md", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, "README.md", sw.Elapsed, ex.Message);
        }
    }

    /// <summary>Writes requirements.html plus one detail page per FR/NFR. Each page is linkified against the
    /// requirement set (the detail page skips its own id so it never self-links).</summary>
    private void WriteRequirements(RequirementsModel requirements, EpicsModel model, ProgressModel progress, SiteNav nav)
    {
        WriteOutput("requirements.html",
            ApplyReferenceLinks(RequirementsTemplater.RenderIndex(requirements, model, progress, nav), "requirements.html"));

        var requirementsDir = Path.Combine(_options.OutputRoot, "requirements");
        Directory.CreateDirectory(requirementsDir);

        foreach (var req in requirements.All)
        {
            var coveringEpic = req.CoverageEpicNumber is { } n
                ? model.Epics.FirstOrDefault(e => e.Number == n)
                : null;
            var outputRelative = $"requirements/{req.Slug}.html";
            var html = RequirementsTemplater.RenderRequirement(req, coveringEpic, progress, nav);
            WriteOutput(outputRelative, ApplyReferenceLinks(html, outputRelative, skipRequirementId: req.Id));
        }
    }

    /// <summary>Whole-page pass that turns every "FR25"/"NFR7" reference into a link to its detail page,
    /// and every "Story N.M"/"Epic N" mention into a link to that story/epic page. A no-op until epics.md
    /// has been parsed (so <see cref="_requirements"/>/<see cref="_epicsModel"/> exist); safe to call on
    /// every page since both linkifiers skip tokens already inside links. Story/epic pages pass their own
    /// id so a page never links to itself.</summary>
    private string ApplyReferenceLinks(string html, string outputRelativePath, string? skipRequirementId = null, string? skipStoryId = null, int? skipEpicNumber = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        if (_requirements is not null)
        {
            html = RequirementLinkifier.Linkify(html, _requirements, prefix, skipRequirementId);
        }
        if (_epicsModel is not null)
        {
            html = StoryEpicLinkifier.Linkify(html, _epicsModel, prefix, skipStoryId, skipEpicNumber);
        }
        return html;
    }

    private void EnsureScaffold()
    {
        Directory.CreateDirectory(_options.OutputRoot);
        // The stylesheet + script ship as embedded resources so the global-tool package needs no loose asset files.
        CopyEmbeddedAsset("SpecScribe.assets.specscribe.css", ForgeOptions.StylesheetName);
        CopyEmbeddedAsset("SpecScribe.assets.specscribe.js", ForgeOptions.ScriptName);
    }

    private void CopyEmbeddedAsset(string resourceName, string outputFileName)
    {
        var dest = Path.Combine(_options.OutputRoot, outputFileName);
        using var source = typeof(SiteGenerator).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded asset '{resourceName}' is missing from the assembly.");
        using var destStream = File.Create(dest);
        source.CopyTo(destStream);
    }

    private List<string> EnumerateSourceFiles() =>
        Directory.Exists(_options.SourceRoot)
            ? Directory.EnumerateFiles(_options.SourceRoot, "*.md", SearchOption.AllDirectories)
                .Where(p => !IsIgnored(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    /// <summary>Markdown files in the resolved ADR root, plus ONE level of subdirectories — enough for nested
    /// year/topic schemes (<c>decisions/2024/0007-x.md</c>) without walking a whole tree of unrelated prose
    /// into the ADR section (deliberately bounded — the same window the ForgeOptions probe reads). [Story 4.2 Task 1]</summary>
    private List<string> EnumerateAdrFiles() =>
        Directory.Exists(_options.AdrSourceRoot)
            ? Directory.EnumerateFiles(_options.AdrSourceRoot, "*.md", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateDirectories(_options.AdrSourceRoot)
                    .SelectMany(d => Directory.EnumerateFiles(d, "*.md", SearchOption.TopDirectoryOnly)))
                .Where(p => !IsIgnored(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    /// <summary>Builds the site nav, folding in whether any ADRs exist (they live outside the _bmad-output
    /// file list the nav is otherwise derived from). Also detects the active BMad module so the nav shows
    /// that module's planning docs and the templaters emit that module's workflow commands.</summary>
    private SiteNav BuildNav(IReadOnlyList<string> sourceRelatives)
    {
        _module = ModuleContext.Detect(_options.RepoRoot, sourceRelatives);
        // StructureAvailable = the source-artifact file set is non-empty — the SINGLE signal that gates both the
        // nav item/quick link here and the WriteStructure page write, so a Structure link is never emitted to a
        // page that wasn't produced. [Story 3.4 Subtask 3.4]
        return SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable, hasStructure: sourceRelatives.Count > 0);
    }

    private static readonly IReadOnlyDictionary<string, DateOnly> EmptyDates = new Dictionary<string, DateOnly>();

    // The memlog frontmatter's single "updated: <date>" field — a one-line regex read (like ForgeOptions'
    // project_name read), NOT a full YAML parse. Captures just the yyyy-MM-dd prefix of the timestamp.
    private static readonly Regex MemlogUpdatedPattern = new(
        @"^\s*updated:\s*(?<date>\d{4}-\d{2}-\d{2})", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Gathers the inputs for and builds the cached <see cref="ArtifactCoverage"/> insight. IO lives
    /// here — source-file last-write-times (the primary freshness signal) and memlog discovery (the secondary
    /// enrichment); the pure <see cref="ArtifactCoverage.Build"/> owns the coverage/freshness/staleness rules.
    /// Never throws: any failure degrades to <see cref="ArtifactCoverage.Empty"/> so the panel omits and
    /// baseline generation still succeeds (AD-4: insight providers never own baseline success; NFR2). [Story 3.3]</summary>
    private ArtifactCoverage BuildArtifactCoverage(IReadOnlyList<string> sourceRelatives)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            // First pass with empty maps discovers which canonical families are present and their matched
            // source paths, so we stat ONLY those files (not every markdown doc). All family-matching logic
            // stays in ArtifactCoverage — the single coverage seam Epic 4 generalizes.
            var discovered = ArtifactCoverage.Build(sourceRelatives, EmptyDates, EmptyDates, today);

            // Stat every candidate matching ANY family (not just each family's provisional first match) so an
            // OR-predicate family (UX: DESIGN.md or EXPERIENCE.md) has both mtimes available when Build picks
            // the freshest one as canonical below. [Story 3.3 review]
            var mtimes = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in ArtifactCoverage.AllCandidatePaths(sourceRelatives))
            {
                try
                {
                    var full = Path.Combine(_options.SourceRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    mtimes[rel] = DateOnly.FromDateTime(File.GetLastWriteTime(full));
                }
                catch
                {
                    // A single unreadable file degrades that one family's freshness to null — never aborts
                    // the pass (partial data beats none, AD-4).
                }
            }

            var memlogByFamily = BuildMemlogMap(discovered);

            var coverage = ArtifactCoverage.Build(sourceRelatives, mtimes, memlogByFamily, today);

            // Enrich each family with presentation data the pure Build can't know: the page a PRESENT family
            // links to, and the create command a MISSING family surfaces for the detected module. Resolved
            // here because both depend on page routing and the module — never on the source-derived truth.
            var enriched = coverage.Families
                .Select(f => f with
                {
                    Href = f.Present ? ResolveFamilyHref(f) : null,
                    CreateCommand = !f.Present && ArtifactCoverage.CreateStepKeys.TryGetValue(f.Label, out var step)
                        ? _module.Commands.Command(step)
                        : null,
                })
                .ToList();

            return new ArtifactCoverage { Families = enriched };
        }
        catch (Exception)
        {
            return ArtifactCoverage.Empty;
        }
    }

    /// <summary>The page a present family's card links to. Epics/Stories point at the epics-and-stories view and
    /// Requirements at the curated FR/NFR page (both special-routed, not generic source pages); every other
    /// family links to its generated page (<c>ToOutputRelative</c> of its matched source, which the generic
    /// page pipeline writes verbatim). Labels mirror <see cref="ArtifactCoverage"/>'s canonical family set.
    /// Only returns an href when the target page was actually produced — <c>_docs</c>/<c>_epicsModel</c>/
    /// <c>_requirements</c> are populated exclusively on a successful page write, so a source-file conversion
    /// failure (or an epics-parse failure) degrades the card to non-linked "present" text instead of a broken
    /// link. Mirrors the same <c>_docs</c>-gated guard <see cref="BuildStructureHrefMap"/> already applies to
    /// the structure tree. [Story 3.3 review]</summary>
    private string? ResolveFamilyHref(ArtifactFamily f)
    {
        if (f.SourcePath is not { } src) return null;
        return f.Label switch
        {
            "Epics" or "Stories" => _epicsModel is not null ? SiteNav.EpicsOutputPath : null,
            "Requirements" => _requirements is not null ? SiteNav.RequirementsOutputPath : null,
            // _docs keys are raw Path.GetRelativePath (OS-native separators); f.SourcePath is normalized to
            // forward slashes, so both sides must be normalized before comparing (same reasoning
            // BuildStructureHrefMap already applies). [Story 3.3 review]
            _ => _docs.Keys.Any(k => string.Equals(PathUtil.NormalizeSlashes(k), src, StringComparison.OrdinalIgnoreCase))
                ? PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(src))
                : null,
        };
    }

    /// <summary>Discovers <c>.memlog.md</c> journals (dotfiles, so excluded from the <c>*.md</c> source
    /// enumeration — scanned separately, like the adapter's sprint-file discovery), reads each one's
    /// <c>updated:</c> date, and associates it with the family whose canonical file sits in the closest
    /// ancestor directory (longest matching memlog dir wins). Strictly additive: no memlogs → an empty map, so
    /// every family's primary Present/LastModified value is unchanged (AC #2). [Story 3.3 Subtask 2.3]</summary>
    private IReadOnlyDictionary<string, DateOnly> BuildMemlogMap(ArtifactCoverage discovered)
    {
        var result = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_options.SourceRoot)) return result;

        var memlogs = new List<(string Dir, DateOnly Updated)>();
        foreach (var full in Directory.EnumerateFiles(_options.SourceRoot, ".memlog.md", SearchOption.AllDirectories))
        {
            DateOnly updated;
            try
            {
                var text = MarkdownConverter.ReadAllTextShared(full);
                var m = MemlogUpdatedPattern.Match(text);
                if (!m.Success || !DateOnly.TryParseExact(
                        m.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out updated))
                {
                    continue; // no parseable updated: date → this memlog adds no enrichment
                }
            }
            catch
            {
                continue;
            }

            var dir = PathUtil.NormalizeSlashes(Path.GetDirectoryName(ToSourceRelative(full)) ?? string.Empty);
            memlogs.Add((dir, updated));
        }

        if (memlogs.Count == 0) return result; // strictly additive: the no-memlog primary picture is unchanged

        // A root-level memlog (Dir.Length == 0) only stands in as every family's fallback when it's the ONLY
        // memlog in the tree — there, it genuinely is the project's one decision journal. Once any nested,
        // family-scoped memlog exists, the root one no longer blanket-applies to families with no closer
        // match, so an unrelated project-root journal can't be misattributed as a specific family's own
        // enrichment. [Story 3.3 review]
        var hasScopedMemlog = memlogs.Any(ml => ml.Dir.Length > 0);

        foreach (var family in discovered.Families)
        {
            if (family.SourcePath is not { } rel) continue;
            var best = memlogs
                .Where(ml => ml.Dir.Length == 0
                    ? !hasScopedMemlog
                    : rel.StartsWith(ml.Dir + "/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(ml => ml.Dir.Length)
                .Select(ml => (DateOnly?)ml.Updated)
                .FirstOrDefault();
            if (best is { } d) result[family.Label] = d;
        }

        return result;
    }

    /// <summary>The nav/section gate: any renderable RECORD present (numbered or not), rather than the old
    /// "any numbered file" — an ADR home holding only non-standard-named records still surfaces its section
    /// (AC #2), while a README-only (or template-only) directory still gates the section off. [Story 4.2 Task 2]</summary>
    private bool AdrsExist() => EnumerateAdrFiles()
        .Any(f => IsAdrRecordFile(PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.AdrSourceRoot, f))));

    private static int? ParseAdrNumber(string fileName)
    {
        var m = AdrNumberPattern.Match(fileName);
        return m.Success && int.TryParse(m.Groups["num"].Value, out var n) ? n : null;
    }

    /// <summary>Derives an ADR's status tolerantly, first derivable value wins: (a) the "**Status:** …" bold
    /// line, (b) a MADR-style "## Status" section's first non-blank line, (c) a <c>status:</c> frontmatter
    /// key. Markdown links flatten to plain text (e.g. "Superseded by [0002](0002-x.md)" → "Superseded by
    /// 0002") and wrapping bold markers are stripped, for the index card. Not derivable ⇒ null — the record
    /// still renders, just without a status badge (AC #2). The raw status STRING renders as-is; mapping status
    /// vocabulary to the canonical model is Story 8.1's domain, not this method's. [Story 4.2 Task 2]</summary>
    private static string? ExtractAdrStatus(string raw, Frontmatter frontmatter)
    {
        var bold = AdrStatusPattern.Match(raw);
        if (bold.Success && CleanAdrStatus(bold.Groups["status"].Value) is { } fromBoldLine)
        {
            return fromBoldLine;
        }

        var heading = AdrStatusHeadingPattern.Match(raw);
        if (heading.Success)
        {
            foreach (var line in raw[(heading.Index + heading.Length)..].Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith('#')) break; // empty Status section — fall through to frontmatter
                if (IsDecorativeLine(trimmed)) continue; // e.g. a "---" rule right under the heading
                if (CleanAdrStatus(trimmed) is { } fromHeading) return fromHeading;
                break;
            }
        }

        return frontmatter.Status is { Length: > 0 } fromFrontmatter ? CleanAdrStatus(fromFrontmatter) : null;
    }

    /// <summary>True for a line made up solely of a single repeated markdown rule/separator character (e.g.
    /// <c>---</c>, <c>***</c>, <c>___</c>) — never a genuine status value, so the MADR heading scan skips
    /// past it rather than capturing it verbatim. [Review][Patch]</summary>
    private static bool IsDecorativeLine(string trimmed) =>
        trimmed.Length >= 3 && trimmed.Distinct().Count() == 1 && "-=*_".Contains(trimmed[0]);

    private static string? CleanAdrStatus(string value)
    {
        var status = MarkdownLinkPattern.Replace(value, "${text}").Trim().Trim('*', '_').Trim();
        return status.Length == 0 ? null : status;
    }

    /// <summary>Maps every known source path (normalized, "_bmad-output/"-relative) to the URL its content
    /// actually lives at — most are a straight extension swap, but epics.md points at the generated
    /// epics.html and a consumed implementation-artifact points at its story page, not their generic
    /// mirrored render. Powers <see cref="SourceLinkifier"/> so "[Source: ...]" citations become real links.</summary>
    private Dictionary<string, string> BuildReferenceMap(
        List<string> files,
        EpicsModel model,
        IReadOnlyDictionary<string, string> artifactMap,
        string epicsSourceRelative)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var rel = PathUtil.NormalizeSlashes(ToSourceRelative(file));
            map[rel] = string.Equals(rel, epicsSourceRelative, StringComparison.OrdinalIgnoreCase)
                ? SiteNav.EpicsOutputPath
                : PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(rel));
        }

        foreach (var epic in model.Epics)
        {
            foreach (var story in epic.Stories)
            {
                if (story.ArtifactOutputPath is { } ap && artifactMap.TryGetValue(story.Id, out var artifactFullPath))
                {
                    map[PathUtil.NormalizeSlashes(ToSourceRelative(artifactFullPath))] = ap;
                }
            }
        }

        return map;
    }

    /// <summary>Resolves a spec-kernel doc's frontmatter <c>companions:</c>/<c>sources:</c> references to real
    /// generated pages, for the "Companion documents" cross-link block. Only docs under <c>specs/</c> carry
    /// these; every other doc yields an empty list (no block). Each reference is resolved relative to the spec
    /// doc's own directory; it becomes a link ONLY when the target file exists on disk, sits inside
    /// <see cref="ForgeOptions.SourceRoot"/>, and isn't an ignored file (so a listed-but-missing companion, or
    /// an ignored <c>.memlog.md</c> that never generates a page, is silently omitted rather than emitting a
    /// broken link — AC #2 / NFR2). Resolution is by file existence, not <see cref="_docs"/> membership, so it
    /// is order-independent during the full-rebuild pass. [Story 2.2 Task 4]</summary>
    private IReadOnlyList<(string Label, string Href)> ResolveSpecCompanions(DocModel doc)
    {
        var sourceRel = PathUtil.NormalizeSlashes(doc.SourceRelativePath);
        if (!sourceRel.StartsWith("specs/", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<(string, string)>();
        }

        var references = doc.Frontmatter.Companions.Concat(doc.Frontmatter.Sources).ToList();
        if (references.Count == 0)
        {
            return Array.Empty<(string, string)>();
        }

        var prefix = PathUtil.RelativePrefix(doc.OutputRelativePath);
        var sourceRootFull = Path.GetFullPath(_options.SourceRoot);
        var sourceDir = Path.GetDirectoryName(Path.Combine(sourceRootFull, sourceRel.Replace('/', Path.DirectorySeparatorChar))) ?? sourceRootFull;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<(string Label, string Href)>();
        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference)) continue;

            var candidateFull = Path.GetFullPath(Path.Combine(sourceDir, reference.Replace('/', Path.DirectorySeparatorChar)));

            // Inside SourceRoot, a markdown file that actually gets a generated page, and not ignored.
            // Otherwise omit — never a broken link (only *.md sources are converted into pages).
            if (!candidateFull.StartsWith(sourceRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(Path.GetExtension(candidateFull), ".md", StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(candidateFull) || IsIgnored(candidateFull)) continue;

            var candidateRel = PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.SourceRoot, candidateFull));
            // Don't cross-link a doc to itself, and list each target once even if named as both companion and source.
            if (string.Equals(candidateRel, sourceRel, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(candidateRel)) continue;

            var href = prefix + PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(candidateRel));
            resolved.Add((PrettyLabel(candidateRel), href));
        }

        return resolved;
    }

    // Known acronym filename tokens preserved verbatim by PrettyLabel. A blanket "all-caps token" rule also
    // catches full English words authored in all-caps (e.g. EXPERIENCE.md, DESIGN.md), producing shouty labels —
    // so only this explicit allowlist is exempted from title-casing. [Story 2.2 review]
    private static readonly HashSet<string> AcronymLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRD", "SPEC", "UX", "API", "FR", "NFR", "AC", "TOC",
    };

    /// <summary>A human, title-cased label for a related-doc cross-link, derived from its filename
    /// (e.g. <c>requirements-catalog.md</c> → "Requirements Catalog"). Kept order-independent — it never reads
    /// the target's own title, which may not have been generated yet during the rebuild pass. [Story 2.2 Task 4]</summary>
    private static string PrettyLabel(string sourceRelativePath)
    {
        var ti = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        var words = Path.GetFileNameWithoutExtension(sourceRelativePath)
            .Split('-', '_', ' ')
            .Where(w => w.Length > 0)
            // Preserve only known acronym tokens (PRD, SPEC, ...) as-is; title-case everything else, including
            // other all-caps filenames (EXPERIENCE.md → "Experience", not "EXPERIENCE").
            .Select(w => AcronymLabels.Contains(w) ? w.ToUpperInvariant() : ti.ToTitleCase(w.ToLowerInvariant()));
        return string.Join(" ", words);
    }

    private string ToSourceRelative(string fullPath) =>
        Path.GetRelativePath(_options.SourceRoot, fullPath);

    // The predicate itself moved to PathUtil so the framework adapters share it (ignored files are neither
    // rendered nor reported as unsupported, wherever discovery happens). [Story 4.1]
    private static bool IsIgnored(string path) => PathUtil.IsIgnoredSourceFile(path);
}
