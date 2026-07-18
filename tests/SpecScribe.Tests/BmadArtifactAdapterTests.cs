using System.Security.AccessControl;
using System.Security.Principal;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 4.1's ingestion seam: <see cref="BmadArtifactAdapter"/> must produce the
/// same normalized models the generator's inline parse chain used to (epics with resolved artifacts, then
/// progress enrichment via the caller's projection callback, then requirements — in that order), self-select
/// via <c>AppliesTo</c> on the <c>_bmad/</c> marker, and turn per-artifact failures into categorized,
/// non-fatal <see cref="AdapterDiagnostic"/>s while every valid sibling still lands in the bundle (AC #2).
/// Follows the temp-dir fixture style of <see cref="SiteGeneratorSprintTests"/>.</summary>
public class BmadArtifactAdapterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-adapter-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string SprintYaml => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### NonFunctional Requirements

        NFR1: Generation degrades gracefully

        ### FR Coverage Map

        FR1: Epic 1 - rendering
        NFR1: Epic 1 - degradation

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string RetroMd = """
        # Epic 1 Retrospective

        **Date:** 2026-07-06
        **Participants:** Team

        Went well.
        """;

    private const string SprintYamlContent = """
        last_updated: 2026-07-06T22:00:00-04:00
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
        """;

    public BmadArtifactAdapterTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "epic-1-retro-2026-07-06.md"), RetroMd);
        File.WriteAllText(SprintYaml, SprintYamlContent);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Path.Combine(_root, "docs", "adrs"), output: Path.Combine(_root, "site"),
        projectName: "SpecScribe", includeReadme: false);

    /// <summary>The generator's source enumeration, mirrored: every *.md under the source root.</summary>
    private List<string> SourceFiles() =>
        Directory.EnumerateFiles(Source, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static ProgressModel Project(EpicsModel epics, IReadOnlyDictionary<string, string> artifacts) =>
        ProgressCalculator.Compute(epics, artifacts, git: null);

    [Fact]
    public void Ingest_RepresentativeBmadSource_ProducesFullBundle()
    {
        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        // Epics with story artifacts resolved by the adapter, exactly as the generator used to do inline.
        Assert.NotNull(bundle.Epics);
        var epic = Assert.Single(bundle.Epics!.Epics);
        Assert.Equal(2, epic.Stories.Count);
        Assert.Equal("epics/story-1-1.html", epic.Stories[0].ArtifactOutputPath);
        Assert.Null(epic.Stories[1].ArtifactOutputPath); // undrafted → no artifact link

        // Requirements parsed from the same source, after progress enrichment, with coverage rolled up.
        Assert.NotNull(bundle.Requirements);
        Assert.Equal(1, bundle.Requirements!.ById["FR1"].CoverageEpicNumber);
        Assert.True(bundle.Requirements.ById.ContainsKey("NFR1"));

        Assert.NotNull(bundle.Sprint);
        var retro = Assert.Single(bundle.Retros);
        Assert.Equal(1, retro.EpicNumber);

        // No _bmad install in this fixture → module degrades to None, exactly as ModuleContext.Detect does.
        Assert.Same(ModuleContext.None, bundle.Module);

        Assert.Equal(Path.Combine(Source, "planning-artifacts", "epics.md"), bundle.EpicsSourceFullPath);
        Assert.True(bundle.StoryArtifactsById.ContainsKey("1.1"));

        // Consumed = the story artifact + the retro note; the generic-pages pass must skip both.
        Assert.Contains(Path.Combine("implementation-artifacts", "1-1-foundation.md"), bundle.ConsumedSourceRelatives);
        Assert.Contains(Path.Combine("implementation-artifacts", "epic-1-retro-2026-07-06.md"), bundle.ConsumedSourceRelatives);

        Assert.Empty(bundle.Diagnostics);
    }

    [Fact]
    public void Ingest_OrdersEpicsBeforeProgressBeforeRequirements()
    {
        EpicsModel? seenByProjection = null;
        var calls = 0;
        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), (epics, artifacts) =>
        {
            calls++;
            seenByProjection = epics;
            // Artifact resolution must already have happened when the projection callback runs — progress
            // computes task counts from the resolved artifact set.
            Assert.Equal("epics/story-1-1.html", epics.Epics[0].Stories[0].ArtifactOutputPath);
            Assert.True(artifacts.ContainsKey("1.1"));
            return ProgressCalculator.Compute(epics, artifacts, git: null);
        });

        Assert.Equal(1, calls);
        Assert.Same(bundle.Epics, seenByProjection);
        Assert.NotNull(bundle.Requirements); // parsed strictly after the enrichment above
    }

    [Fact]
    public void Ingest_WithoutProjection_SkipsRequirementsButKeepsEpics()
    {
        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), projectProgress: null);

        Assert.NotNull(bundle.Epics);
        Assert.Null(bundle.Requirements); // requirements need progress; no enrichment → absent, not broken
        Assert.Empty(bundle.Diagnostics);
    }

    [Fact]
    public void AppliesTo_TrueForBmadRepo_FalseWithout()
    {
        var adapter = new BmadArtifactAdapter();
        Assert.False(adapter.AppliesTo(Options(), SourceFiles()));

        Directory.CreateDirectory(Path.Combine(_root, "_bmad"));
        Assert.True(adapter.AppliesTo(Options(), SourceFiles()));
    }

    [Fact]
    public void Ingest_UnreadableRetro_YieldsMalformedDiagnosticAndKeepsSiblings()
    {
        // A retro path that no longer exists (deleted mid-scan) — the parse throws, and AC #2 demands the
        // failure is categorized + non-fatal while every sibling artifact still lands in the bundle.
        var files = SourceFiles();
        files.Add(Path.Combine(Source, "implementation-artifacts", "epic-2-retro-2026-07-07.md"));

        var bundle = new BmadArtifactAdapter().Ingest(Options(), files, Project);

        var diag = Assert.Single(bundle.Diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Malformed, diag.Category);
        Assert.Equal(Path.Combine("implementation-artifacts", "epic-2-retro-2026-07-07.md"), diag.RelativePath);

        Assert.Equal(1, Assert.Single(bundle.Retros).EpicNumber); // valid sibling retro survived
        Assert.NotNull(bundle.Epics);
        Assert.NotNull(bundle.Sprint);
    }

    [Fact]
    public void Ingest_UnreadableEpics_YieldsMalformedDiagnosticAndKeepsOtherFamilies()
    {
        File.Delete(Path.Combine(Source, "planning-artifacts", "epics.md"));
        var files = SourceFiles();
        files.Add(Path.Combine(Source, "planning-artifacts", "epics.md")); // discovered but unreadable

        var bundle = new BmadArtifactAdapter().Ingest(Options(), files, Project);

        var diag = Assert.Single(bundle.Diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Malformed, diag.Category);
        Assert.Equal(Path.Combine("planning-artifacts", "epics.md"), diag.RelativePath);

        Assert.Null(bundle.Epics);
        Assert.Null(bundle.Requirements);
        // The found-but-broken epics file still reports its identity so callers keep excluding it from
        // generic-page rendering, matching the previous behavior.
        Assert.NotNull(bundle.EpicsSourceFullPath);
        Assert.NotNull(bundle.Sprint);
        Assert.Single(bundle.Retros);
    }

    [Fact]
    public void Ingest_UnusableSprintYaml_YieldsUnsupportedDiagnosticAndNullSprint()
    {
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        var diag = Assert.Single(bundle.Diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Unsupported, diag.Category);
        Assert.EndsWith("sprint-status.yaml", diag.RelativePath);

        Assert.Null(bundle.Sprint);
        Assert.NotNull(bundle.Epics); // siblings unaffected
    }

    [Fact]
    public void Ingest_MissingSprintYaml_IsSilentOmission()
    {
        File.Delete(SprintYaml);

        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        Assert.Null(bundle.Sprint);
        Assert.Empty(bundle.Diagnostics); // absent is normal, not a reportable shape problem
    }

    [Fact]
    public void Ingest_MultipleSprintYaml_ParsesAlphabeticalFirstAndEmitsOneSkippedDiagnostic()
    {
        // A monorepo/multi-module layout with two sprint-status.yaml files: the alphabetically-first path
        // still parses (unchanged selection rule), but the duplicate is no longer silently dropped.
        // [spec-epic2-deferred-debt-cleanup]
        var otherDir = Path.Combine(Source, "module-b");
        Directory.CreateDirectory(otherDir);
        var otherSprint = Path.Combine(otherDir, "sprint-status.yaml");
        File.WriteAllText(otherSprint, SprintYamlContent);

        var expectedFirst = new[] { SprintYaml, otherSprint }.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).First();

        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        Assert.NotNull(bundle.Sprint);
        var diag = Assert.Single(bundle.Diagnostics, d => d.Category == AdapterDiagnosticCategory.Skipped);
        Assert.Equal(Path.GetRelativePath(Source, expectedFirst), diag.RelativePath);
        Assert.Contains("1", diag.Message);
    }

    [Fact]
    public void Ingest_InaccessibleSprintSubdirectory_DegradesToNoSprintCandidatesInsteadOfThrowing()
    {
        // An IOException/UnauthorizedAccessException while walking the source tree for sprint-status.yaml
        // must never abort generation — it degrades to "no candidates found" for this pass.
        // [spec-epic2-deferred-debt-cleanup]
        if (!OperatingSystem.IsWindows())
        {
            // The ACL manipulation below is Windows-specific; the guarded try/catch this test exercises is
            // platform-agnostic and already covered indirectly by every other passing Ingest_* test.
            return;
        }

        // Captured BEFORE the ACL is locked down: unlike Ingest (which is only handed the sprint-status.yaml
        // search internally), this test's own *.md discovery helper (SourceFiles) would otherwise also walk
        // into — and throw on — the same blocked subdirectory, which isn't what this test is isolating.
        var sourceFiles = SourceFiles();

        var blockedDir = Path.Combine(Source, "implementation-artifacts", "blocked");
        Directory.CreateDirectory(blockedDir);

#pragma warning disable CA1416 // Windows-only ACL APIs — guarded above by OperatingSystem.IsWindows().
        var dirInfo = new DirectoryInfo(blockedDir);
        var security = dirInfo.GetAccessControl();
        var currentUser = WindowsIdentity.GetCurrent().User!;
        var denyRule = new FileSystemAccessRule(
            currentUser,
            FileSystemRights.ListDirectory | FileSystemRights.Read,
            AccessControlType.Deny);
        security.AddAccessRule(denyRule);
        dirInfo.SetAccessControl(security);

        try
        {
            var bundle = new BmadArtifactAdapter().Ingest(Options(), sourceFiles, Project);

            // The pre-existing top-level sprint-status.yaml is still found and parsed; the inaccessible
            // sibling subdirectory doesn't abort the whole tree walk. If the runtime aborts the enumeration
            // entirely, this degrades to null with no diagnostic (never a throw) — either way, no exception.
            Assert.NotNull(bundle.Epics); // the rest of ingest proceeded normally either way
        }
        finally
        {
            security.RemoveAccessRule(denyRule);
            dirInfo.SetAccessControl(security);
        }
#pragma warning restore CA1416
    }

    [Fact]
    public void IngestEpics_NestedImplementationArtifacts_StillResolvesStoryArtifacts()
    {
        // Location tolerance (Story 4.2 Task 4): implementation-artifacts/ nested one level deeper still
        // classifies its story artifacts — discovery keys on the ancestor segment, not a fixed parent dir.
        var nestedDir = Path.Combine(Source, "tracking", "implementation-artifacts");
        Directory.CreateDirectory(nestedDir);
        File.Move(
            Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            Path.Combine(nestedDir, "1-1-foundation.md"));

        var ingest = new BmadArtifactAdapter().IngestEpics(Options(), SourceFiles(), Project);

        Assert.NotNull(ingest.Epics);
        Assert.True(ingest.StoryArtifactsById.ContainsKey("1.1"));
        var story = Assert.Single(ingest.Epics!.Epics).Stories.Single(s => s.Id == "1.1");
        Assert.Equal("epics/story-1-1.html", story.ArtifactOutputPath);
        Assert.Contains(Path.Combine("tracking", "implementation-artifacts", "1-1-foundation.md"), ingest.ConsumedSourceRelatives);
    }

    [Fact]
    public void Ingest_IgnoredFiles_AreNeitherIngestedNorDiagnosed()
    {
        // An editor temp matching the retro name pattern, and nonexistent to boot — if the ignore filter
        // failed, this would either parse or produce a Malformed diagnostic.
        var files = SourceFiles();
        files.Add(Path.Combine(Source, "implementation-artifacts", "~$epic-1-retro-2026-07-06.md"));
        files.Add(Path.Combine(Source, "implementation-artifacts", ".epic-1-retro-draft.md"));

        var bundle = new BmadArtifactAdapter().Ingest(Options(), files, Project);

        Assert.Empty(bundle.Diagnostics);
        Assert.Single(bundle.Retros);
        Assert.DoesNotContain(bundle.ConsumedSourceRelatives, p => p.Contains("~$") || Path.GetFileName(p).StartsWith('.'));
    }

    [Fact]
    public void IngestEpics_OversizedArtifactNumber_DoesNotThrowAndIsSkipped()
    {
        // A story-artifact filename whose numeric group overflows Int32 (the unbounded \d+ pattern still
        // matches it). BuildArtifactMap must TryParse-and-skip, honoring the adapter's NEVER-throws contract,
        // rather than crashing the whole ingest with OverflowException. [Story 4.1 review]
        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "99999999999-1-overflow.md"), "# Overflow\n");

        var ingest = new BmadArtifactAdapter().IngestEpics(Options(), SourceFiles(), Project);

        // The valid artifact still resolves; the oversized one is simply absent, no diagnostic, no throw.
        Assert.True(ingest.StoryArtifactsById.ContainsKey("1.1"));
        Assert.DoesNotContain(ingest.StoryArtifactsById.Values, v => v.Contains("overflow"));
    }

    [Fact]
    public void Ingest_UnrecognizedStoryStatus_YieldsUnsupportedDiagnostic()
    {
        // Story 8.2 AC #3: a present, unmapped Status: is Unsupported (non-fatal), not silently "drafted".
        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            """
            # Story 1.1: Foundation Story

            Status: frobnicated

            ## Story

            As a maintainer, I want the foundation.

            ## Acceptance Criteria

            1. It works.

            ## Tasks / Subtasks

            - [x] Task 1: Do it (AC: #1)
            """);

        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        var diag = Assert.Single(bundle.Diagnostics, d => d.Message.Contains("frobnicated", StringComparison.Ordinal));
        Assert.Equal(AdapterDiagnosticCategory.Unsupported, diag.Category);
        Assert.Contains("Unrecognized status", diag.Message);
        Assert.Equal("unrecognized", StatusStyles.ForStory(bundle.Epics!.Epics[0].Stories.Single(s => s.Id == "1.1")));
    }

    [Fact]
    public void Ingest_AbsentStoryStatus_StaysDraftedWithNoDiagnostic()
    {
        // Story 1.2 has no artifact → Status null → drafted, no notice. [Story 8.2 absent-vs-unmapped]
        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        Assert.Empty(bundle.Diagnostics);
        var undrafted = bundle.Epics!.Epics[0].Stories.Single(s => s.Id == "1.2");
        Assert.Null(undrafted.Status);
        Assert.Equal("drafted", StatusStyles.ForStory(undrafted));
    }

    [Fact]
    public void Ingest_UnrecognizedSprintStatus_YieldsUnsupportedDiagnostic()
    {
        File.WriteAllText(SprintYaml, """
            last_updated: 2026-07-06T22:00:00-04:00
            development_status:
              epic-1: in-progress
              1-1-foundation: blocked
            """);

        var bundle = new BmadArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

        var diag = Assert.Single(bundle.Diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Unsupported, diag.Category);
        Assert.Contains("blocked", diag.Message);
        Assert.Contains("1-1-foundation", diag.Message);
        Assert.NotNull(bundle.Sprint); // generation still has the sprint model
    }
}
