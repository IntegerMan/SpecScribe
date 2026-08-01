using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Goal 2 of <c>spec-vscode-extension-name-latency-and-webview-sunburst</c>: the `webview` command's
/// first-paint latency work, asserted at the seam it actually changed — <see cref="SiteGenerator"/>.
///
/// <para>Three switches are covered, and every one of them is checked the SAME way: <b>the webview surface set must
/// come out identical to the un-optimised one</b>. That is the only assertion that can catch this work's real
/// failure mode, which is not an exception but a quietly smaller panel — the shape Story 23.4 finding 4 and Story
/// 23.6 dependent #6 both hit, where content was dropped by a layer with no test failing.</para>
///
/// <list type="bullet">
/// <item><see cref="SiteGenerator.EmitIr"/> — skips the ~1,400-file canonical IR emit the panel never reads.</item>
/// <item><see cref="SiteGenerator.WriteStaticPages"/> — skips the full static document render/write at the
/// <c>WritePage</c> seam, keeping the composed region the panel does read.</item>
/// <item><c>RenderWebviewSurfaces(includeLongTail: false)</c> + <see cref="SiteGenerator.WithLongTailSurfaces"/> —
/// the prelude/delta split.</item>
/// <item><c>RenderWebviewSurfaces(includeEpicsFamily: false)</c> — the NARROWER boundary the split is actually
/// drawn at: the entry surface alone, with the epics family riding the same delta as the long tail.</item>
/// <item><see cref="SiteGenerator.OnFirstPaintReady"/> — the opt-in checkpoint that fires before the long-tail
/// page phases, which is what moves the cadence build and every page write behind first paint.</item>
/// </list>
/// Fixture style follows <see cref="SiteGeneratorWebviewTests"/>.</summary>
public class WebviewFirstPaintTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-firstpaint-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### FR Coverage Map

        FR1: Epic 1 - rendering

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

        Status: done

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    public WebviewFirstPaintTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "prd.md"), "# PRD\n\nA requirement.\n");
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Adrs, "0001-a-decision.md"),
            "# ADR 0001: A Decision\n\n**Status:** Accepted\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "README.md"),
            "# Decisions\n\n- [ADR 0001: A Decision](0001-a-decision.md)\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    /// <summary>A generator run exactly as the <c>webview</c> command runs it, with the two skips under test
    /// individually settable so a test can compare an optimised run against the un-optimised one.</summary>
    private SiteGenerator Generated(bool emitIr, bool writeStaticPages)
    {
        var gen = new SiteGenerator(Options()) { CapturePages = true, EmitIr = emitIr, WriteStaticPages = writeStaticPages };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    private static IReadOnlyList<(string Path, string Title, string Content, string? Source)> Fingerprint(
        WebviewBundle bundle) =>
        bundle.Surfaces
            .Select(s => (s.OutputRelativePath, s.Title, s.ContentHtml, s.SourcePath))
            .OrderBy(s => s.OutputRelativePath, StringComparer.Ordinal)
            .ToList();

    // ===== EmitIr =============================================================================================

    /// <summary>The default must stay ON. Since Story 23.6 / ADR 0022 the IR is the only artifact `generate` and
    /// `watch` produce, so a default flip here would hand the user an empty output root with errors=0.</summary>
    [Fact]
    public void EmitIr_AndWriteStaticPages_DefaultToOn()
    {
        var gen = new SiteGenerator(Options());

        Assert.True(gen.EmitIr);
        Assert.True(gen.WriteStaticPages);
    }

    [Fact]
    public void EmitIr_Off_WritesNoIr_ButTheWebviewBundleIsUnchanged()
    {
        var withIr = Fingerprint(Generated(emitIr: true, writeStaticPages: true).RenderWebviewSurfaces());

        // A second, independent run of the SAME fixture with the emit skipped.
        Directory.Delete(Site, recursive: true);
        var withoutIr = Generated(emitIr: false, writeStaticPages: true);

        Assert.False(Directory.Exists(Path.Combine(Site, "spa")));
        Assert.False(File.Exists(Path.Combine(Site, "app.html")));
        Assert.Equal(withIr, Fingerprint(withoutIr.RenderWebviewSurfaces()));
    }

    // ===== WriteStaticPages ===================================================================================

    [Fact]
    public void WriteStaticPages_Off_WritesNoPageHtml_ButTheWebviewBundleIsUnchanged()
    {
        var withPages = Fingerprint(Generated(emitIr: false, writeStaticPages: true).RenderWebviewSurfaces());
        Assert.True(File.Exists(Path.Combine(Site, "about.html")), "the default run must still write its pages");

        Directory.Delete(Site, recursive: true);
        var withoutPages = Generated(emitIr: false, writeStaticPages: false);

        Assert.False(File.Exists(Path.Combine(Site, "about.html")));
        Assert.False(File.Exists(Path.Combine(Site, "adrs", "0001-a-decision.html")));
        // The two DOCUMENTED exceptions, pinned so a later "tidy-up" does not silently widen the switch: the
        // embedded assets are not page writes (EnsureScaffold), and index.html is written by WriteIndex, which is
        // not the WritePage seam this switch gates. See SiteGenerator.WriteStaticPages.
        Assert.True(File.Exists(Path.Combine(Site, ForgeOptions.StylesheetName)));
        Assert.True(File.Exists(Path.Combine(Site, "index.html")));
        Assert.Equal(withPages, Fingerprint(withoutPages.RenderWebviewSurfaces()));
    }

    // ===== The prelude / delta split ==========================================================================

    /// <summary>The prelude is the dashboard + epics families and nothing else — that is what makes it small enough
    /// to be worth emitting first. The long tail (docs, ADRs, requirements) must be genuinely absent, not merely
    /// re-ordered, or the split saves nothing.</summary>
    [Fact]
    public void RenderWebviewSurfaces_PreludeOnly_CarriesTheFamiliesAndNoLongTail()
    {
        var prelude = Generated(emitIr: false, writeStaticPages: false).RenderWebviewSurfaces(includeLongTail: false);

        Assert.Equal("index.html", prelude.EntryPath);
        Assert.Equal(
            new[] { "epics.html", "epics/epic-1.html", "epics/story-1-1.html", "epics/story-1-2.html", "index.html" },
            prelude.Surfaces.Select(s => s.OutputRelativePath).OrderBy(p => p, StringComparer.Ordinal).ToArray());
        // The long tail the CapturePages run produced is provably present in the complete bundle, so its absence
        // above is the split doing its job rather than the fixture having nothing to omit.
        Assert.Contains(
            Generated(emitIr: false, writeStaticPages: false).RenderWebviewSurfaces().Surfaces,
            s => s.OutputRelativePath.StartsWith("adrs/", StringComparison.Ordinal));
    }

    /// <summary><b>No surface is lost across the split.</b> Completing the prelude must yield exactly the bundle a
    /// single un-split call produces — same paths, same titles, same content, same source paths, same entry
    /// document.</summary>
    [Fact]
    public void WithLongTailSurfaces_CompletesThePrelude_ToExactlyTheUnsplitBundle()
    {
        var gen = Generated(emitIr: false, writeStaticPages: false);

        var unsplit = gen.RenderWebviewSurfaces();
        var completed = gen.WithLongTailSurfaces(gen.RenderWebviewSurfaces(includeLongTail: false));

        Assert.Equal(Fingerprint(unsplit), Fingerprint(completed));
        Assert.Equal(unsplit.EntryPath, completed.EntryPath);
        Assert.Equal(unsplit.EntryDocument, completed.EntryDocument);
        Assert.Equal(unsplit.SiteTitle, completed.SiteTitle);
    }

    /// <summary>Without <see cref="SiteGenerator.CapturePages"/> there is no long tail to append, so completing a
    /// prelude is a no-op rather than an error — the flag, not this method, decides whether a tail exists.</summary>
    [Fact]
    public void WithLongTailSurfaces_WithoutCapturePages_ReturnsThePreludeUnchanged()
    {
        var gen = new SiteGenerator(Options()) { EmitIr = false };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var prelude = gen.RenderWebviewSurfaces(includeLongTail: false);

        Assert.Same(prelude, gen.WithLongTailSurfaces(prelude));
    }

    // ===== The ENTRY-ONLY prelude + the first-paint checkpoint =================================================

    /// <summary>The prelude the <c>--serve --serve-delta</c> path actually emits is the ENTRY SURFACE ALONE — not
    /// the epics family, not the long tail. That is the boundary the panel is drawn at: it displays exactly one
    /// surface when it opens.</summary>
    [Fact]
    public void RenderWebviewSurfaces_EntryOnly_CarriesTheDashboardAndNothingElse()
    {
        var prelude = Generated(emitIr: false, writeStaticPages: false)
            .RenderWebviewSurfaces(includeLongTail: false, includeEpicsFamily: false);

        Assert.Equal("index.html", prelude.EntryPath);
        Assert.Equal(new[] { "index.html" }, prelude.Surfaces.Select(s => s.OutputRelativePath).ToArray());
        // The outline is genuinely empty for that window — stated here so the host's "still loading" affordance
        // has a pinned reason to exist rather than being read as "this project has no epics".
        Assert.Empty(prelude.Outline.Epics);
    }

    /// <summary><b>No surface is lost across the NARROWER split either.</b> Completing an entry-only prelude must
    /// yield exactly the paths a single un-split call produces — the epics family and the long tail both arrive on
    /// the one completing frame.</summary>
    [Fact]
    public void WithLongTailSurfaces_AddEpicsFamily_CompletesAnEntryOnlyPrelude_LosingNoSurface()
    {
        var gen = Generated(emitIr: false, writeStaticPages: false);

        var unsplit = gen.RenderWebviewSurfaces();
        var completed = gen.WithLongTailSurfaces(
            gen.RenderWebviewSurfaces(includeLongTail: false, includeEpicsFamily: false),
            addEpicsFamily: true);

        Assert.Equal(Fingerprint(unsplit), Fingerprint(completed));
        Assert.Equal(unsplit.EntryPath, completed.EntryPath);
        Assert.Equal(unsplit.EntryDocument, completed.EntryDocument);
        // The outline is rebuilt over the family the completion rendered, so the tree is whole on this frame.
        Assert.Equal(
            unsplit.Outline.Epics.Select(e => e.SurfacePath),
            completed.Outline.Epics.Select(e => e.SurfacePath));
    }

    /// <summary>The checkpoint is opt-in: null by default, invoked exactly once, and BEFORE the long-tail page
    /// phases. The ordering half is asserted against a phase that provably runs after it — the delivery-cadence
    /// build, which is the reason the prelude's dashboard differs from the completed one at all.</summary>
    [Fact]
    public void OnFirstPaintReady_FiresOnceBeforeTheLongTailPhases_AndIsNullByDefault()
    {
        Assert.Null(new SiteGenerator(Options()).OnFirstPaintReady);

        var gen = new SiteGenerator(Options()) { CapturePages = true, EmitIr = false, WriteStaticPages = false };
        var calls = 0;
        WebviewBundle? atCheckpoint = null;
        gen.OnFirstPaintReady = g =>
        {
            calls++;
            atCheckpoint = g.RenderWebviewSurfaces(includeLongTail: false, includeEpicsFamily: false);
            // The code-map page is written after the checkpoint, so it cannot be on disk yet. This is the
            // ordering assertion: a checkpoint that drifted below the long-tail phases would still return a
            // usable bundle and would still be worth nothing.
            Assert.False(File.Exists(Path.Combine(Site, "code-map.html")),
                "the checkpoint must fire before the long-tail page phases");
        };

        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        Assert.Equal(1, calls);
        Assert.NotNull(atCheckpoint);
        Assert.Equal(new[] { "index.html" }, atCheckpoint!.Surfaces.Select(s => s.OutputRelativePath).ToArray());
        // …and the cached prelude was invalidated on the way out, so the dashboard the completing frame carries is
        // rebuilt against the LATER state (cadence included) rather than replaying the checkpoint's snapshot.
        var completed = gen.WithLongTailSurfaces(atCheckpoint, addEpicsFamily: true);
        Assert.Equal(
            Fingerprint(gen.RenderWebviewSurfaces()),
            Fingerprint(completed));
    }
}
