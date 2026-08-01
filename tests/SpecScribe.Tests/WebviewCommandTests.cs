using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.8 coverage for the one new datum the <c>specscribe webview</c> payload carries: the
/// project's configured output root, expressed workspace-relative with forward slashes so the VS Code shim's
/// "Open Generated Site" command can join it to the workspace folder and find an already-generated
/// <c>index.html</c>. Pure string resolution — no spawn, no generation — so it is unit-testable in isolation.</summary>
public class WebviewCommandTests
{
    [Fact]
    public void ResolveConfiguredOutputRoot_DefaultsToSpecScribeOutput_RelativeToRepoRoot()
    {
        // The shim spawns `webview` without --output and never consults .specscribe (R5.3), so the resolved
        // output is always the default SpecScribeOutput under the repo root — expressed relative, forward-slashed.
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-cor-default");
        var options = new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = Path.Combine(repoRoot, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };

        Assert.Equal("SpecScribeOutput", WebviewCommand.ResolveConfiguredOutputRoot(options));
    }

    [Fact]
    public void ResolveConfiguredOutputRoot_NestedOutput_UsesForwardSlashes()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-cor-nested");
        var options = new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = Path.Combine(repoRoot, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = Path.Combine(repoRoot, "build", "site"),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };

        // Never emit a backslash even on Windows: the shim treats the value as a POSIX-joinable relative path.
        Assert.Equal("build/site", WebviewCommand.ResolveConfiguredOutputRoot(options));
    }

    // ===== Story 6.11: the resolved watch roots the shim builds its file watchers from ============================

    private static ForgeOptions RootedOptions(string repoRoot, string? source = null, string? adrs = null) =>
        new()
        {
            RepoRoot = repoRoot,
            SourceRoot = source ?? Path.Combine(repoRoot, ForgeOptions.SourceDirName),
            AdrSourceRoot = adrs ?? Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = adrs is not null,
            OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };

    [Fact]
    public void ResolveSourceRoot_And_AdrRoot_DefaultLayout_RepoRelative_ForwardSlashed()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-roots-default");
        var options = RootedOptions(repoRoot);

        Assert.Equal("_bmad-output", WebviewCommand.ResolveSourceRoot(options));
        Assert.Equal("docs/adrs", WebviewCommand.ResolveAdrRoot(options));
    }

    [Fact]
    public void ResolveSourceRoot_And_AdrRoot_CustomRoots_AreProjected()
    {
        // A repo with non-default --source/--adrs (Story 5.1/5.2) must watch the CUSTOM trees, not the literals.
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-roots-custom");
        var options = RootedOptions(
            repoRoot,
            source: Path.Combine(repoRoot, "spec", "artifacts"),
            adrs: Path.Combine(repoRoot, "decisions"));

        Assert.Equal("spec/artifacts", WebviewCommand.ResolveSourceRoot(options));
        Assert.Equal("decisions", WebviewCommand.ResolveAdrRoot(options));
    }

    [Fact]
    public void ResolveRepoRootOffset_AtRepoRoot_IsDot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-offset-root");
        // The shim spawns `webview` with cwd == the workspace folder; opened at the repo root they coincide → ".".
        Assert.Equal(".", WebviewCommand.ResolveRepoRootOffset(RootedOptions(repoRoot), workingDirectory: repoRoot));
    }

    [Fact]
    public void ResolveRepoRootOffset_OpenedOnSubdir_IsTheUpwardOffset()
    {
        // Opened two levels deep, the workspace folder is a descendant and the repo root is two up → "../..", so the
        // shim resolves the real repo root and anchors both watchers and reveal-source to it (the subdir-open fix).
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-offset-subdir");
        var workingDirectory = Path.Combine(repoRoot, "packages", "app");

        Assert.Equal("../..", WebviewCommand.ResolveRepoRootOffset(RootedOptions(repoRoot), workingDirectory));
    }

    // ===== Deferred item, Story 6.4 review: the scratch-dir key folds case only where the OS filesystem does ======

    [Fact]
    public void ScratchKey_IsStableForTheSameRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-scratch-stable");
        Assert.Equal(WebviewCommand.ScratchKey(repoRoot), WebviewCommand.ScratchKey(repoRoot));
    }

    [Fact]
    public void ScratchKey_CaseDifferingRepoRoots_MatchTheOsFilesystemsOwnCaseSensitivity()
    {
        // On Windows (case-INSENSITIVE filesystem, this project's primary target OS) two path casings of the
        // SAME physical repo (a workspace-folder URI vs. a manually-typed cwd, or drive-letter casing) must fold
        // to the SAME stable scratch dir — a blanket no-fold would silently reintroduce the "successive spawns
        // accumulate instead of overwrite" bug this key exists to prevent. On a case-sensitive filesystem
        // (Linux) two such paths ARE distinct repos and must not collide. [Review][Patch]
        var lower = "/home/dev/myrepo";
        var upper = "/home/dev/MYREPO";

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(WebviewCommand.ScratchKey(lower), WebviewCommand.ScratchKey(upper));
        }
        else
        {
            Assert.NotEqual(WebviewCommand.ScratchKey(lower), WebviewCommand.ScratchKey(upper));
        }
    }

    // ===== Regression: the scratch `.lock` must live OUTSIDE the wiped output root =================================

    [Fact]
    public void RedirectOutputToScratch_HeldLock_DoesNotBlockRecursiveWipeOfTheOutputRoot()
    {
        // The webview generation holds the scratch `.lock` open for the process's life (FileShare.None +
        // DeleteOnClose), then SiteGenerator.GenerateAll wipes the output root recursively before every full
        // rebuild. If the lock lived INSIDE the output root (the original bug) that wipe tried to delete a file the
        // SAME process holds exclusively locked and threw a sharing-violation IOException on every single run — a
        // deterministic self-deadlock the VS Code extension surfaced as "renderer exited 1: (no stderr)". This test
        // reproduces the exact mechanism (hold the lock, then wipe the output root) and asserts it no longer throws.
        var repoRoot = Path.Combine(Path.GetTempPath(), $"specscribe-scratch-wipe-{Guid.NewGuid():N}");
        try
        {
            var options = new ForgeOptions
            {
                RepoRoot = repoRoot,
                SourceRoot = Path.Combine(repoRoot, ForgeOptions.SourceDirName),
                AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
                AdrSourceExplicit = false,
                OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
                SiteTitle = "SpecScribe",
                IncludeReadme = false,
                DeepGitAnalytics = false,
            };

            var redirected = WebviewCommand.RedirectOutputToScratch(options);

            // Simulate a rebuild's clean step against a populated output tree while the lock is still held.
            Directory.CreateDirectory(redirected.OutputRoot);
            File.WriteAllText(Path.Combine(redirected.OutputRoot, "index.html"), "<html></html>");

            var ex = Record.Exception(() => Directory.Delete(redirected.OutputRoot, recursive: true));

            Assert.Null(ex); // with the fix the lock is a sibling of OutputRoot, so the wipe never touches it
        }
        finally
        {
            WebviewCommand.ReleaseScratchLockForTests();
            try { Directory.Delete(Path.Combine(Path.GetTempPath(), "specscribe-webview", WebviewCommand.ScratchKey(repoRoot)), recursive: true); }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    // ===== Story 22.6: delta frames on the NDJSON channel ===================================================

    private static readonly ProjectOutline EmptyOutline =
        new(Array.Empty<OutlineEpic>(), new OutlineSummary(0, 0, 0, 0));

    private static WebviewBundle Bundle(string entryDocument, params WebviewSurface[] surfaces) =>
        new("Test Site", "index.html", entryDocument, surfaces, EmptyOutline);

    private static WebviewSurface Surface(string path, string content, string? title = null, string? source = null) =>
        new(path, title ?? path, content, source);

    private static JsonElement Frame(WebviewBundle? previous, WebviewBundle current, long sequence = 1) =>
        JsonDocument.Parse(
            WebviewCommand.SerializeDeltaPayload(previous, current, sequence, "SpecScribeOutput")).RootElement;

    /// <summary>AC #3, and the compatibility guarantee the whole opt-in rests on: with no basis, the delta
    /// serializer returns a payload BYTE-IDENTICAL to <see cref="WebviewCommand.SerializePayload"/>'s. A cold
    /// consumer therefore needs no special case, and the first frame of a session is indistinguishable from what
    /// every already-shipped VSIX has always received.</summary>
    [Fact]
    public void SerializeDeltaPayload_WithNoBasis_IsByteIdenticalToAFullPayload()
    {
        var bundle = Bundle("<html>dash</html>", Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B</p>"));

        var delta = WebviewCommand.SerializeDeltaPayload(previous: null, bundle, 1, "SpecScribeOutput");
        var full = WebviewCommand.SerializePayload(bundle, "SpecScribeOutput");

        Assert.Equal(full, delta);
        // And it carries NO discriminator — that is what makes it indistinguishable rather than merely similar.
        Assert.False(JsonDocument.Parse(delta).RootElement.TryGetProperty("frame", out _));
    }

    /// <summary>The defect this story closes, stated as a test: a one-surface change must ship ONE surface, not
    /// the whole site. The extension's own guard comment measures today's cost as a ~8 MB whole-site payload per
    /// push.</summary>
    [Fact]
    public void SerializeDeltaPayload_ShipsOnlyTheChangedSurface()
    {
        var before = Bundle("<html>dash</html>",
            Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B</p>"), Surface("docs/b.html", "<p>C</p>"));
        var after = Bundle("<html>dash</html>",
            Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B EDITED</p>"), Surface("docs/b.html", "<p>C</p>"));

        var frame = Frame(before, after, sequence: 2);

        Assert.Equal(WebviewCommand.DeltaFrameDiscriminator, frame.GetProperty("frame").GetString());
        Assert.Equal(2, frame.GetProperty("sequence").GetInt64());

        var changed = frame.GetProperty("changedSurfaces");
        Assert.Equal(1, changed.EnumerateObject().Count());
        Assert.Equal("<p>B EDITED</p>", changed.GetProperty("docs/a.html").GetProperty("content").GetString());
        Assert.Empty(frame.GetProperty("removedSurfaces").EnumerateArray());
    }

    /// <summary>Hashing CONTENT alone would report a retitled or re-sourced surface as unchanged — the panel title
    /// and the "Open source" affordance both read off these fields, so a stale one is a visible defect.</summary>
    [Theory]
    [InlineData("New Title", null)]
    [InlineData(null, "docs/new-source.md")]
    public void SerializeDeltaPayload_DetectsATitleOrSourcePathChange_NotJustContent(string? title, string? source)
    {
        var before = Bundle("<html>d</html>", Surface("docs/a.html", "<p>same</p>", "Old Title", "docs/old.md"));
        var after = Bundle("<html>d</html>",
            Surface("docs/a.html", "<p>same</p>", title ?? "Old Title", source ?? "docs/old.md"));

        var changed = Frame(before, after).GetProperty("changedSurfaces");

        Assert.True(changed.TryGetProperty("docs/a.html", out _), "a title/sourcePath change must reach the wire");
    }

    /// <summary>An unchanged site produces an EMPTY delta frame — the panel does not flicker and no bytes ride.</summary>
    [Fact]
    public void SerializeDeltaPayload_IsEmpty_WhenNothingChanged()
    {
        var bundle = Bundle("<html>dash</html>", Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B</p>"));

        var frame = Frame(bundle, bundle);

        Assert.Empty(frame.GetProperty("changedSurfaces").EnumerateObject());
        Assert.Empty(frame.GetProperty("removedSurfaces").EnumerateArray());
        // `document` null means "keep what you have" — never "the dashboard is now empty".
        Assert.Equal(JsonValueKind.Null, frame.GetProperty("document").ValueKind);
    }

    [Fact]
    public void SerializeDeltaPayload_ReportsRemovedAndAddedSurfaces()
    {
        var before = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"), Surface("docs/gone.html", "<p>X</p>"));
        var after = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"), Surface("docs/new.html", "<p>Y</p>"));

        var frame = Frame(before, after);

        Assert.Equal(new[] { "docs/gone.html" },
            frame.GetProperty("removedSurfaces").EnumerateArray().Select(e => e.GetString()).ToArray());
        // An ADDED surface rides in changedSurfaces — it is content the consumer does not have.
        Assert.True(frame.GetProperty("changedSurfaces").TryGetProperty("docs/new.html", out _));
    }

    /// <summary>The entry document is the single biggest string on the wire, so it rides only when it moved.
    /// Absent/null must mean "keep what you have"; a consumer treating it as an empty document would blank the
    /// dashboard on every unrelated edit.</summary>
    [Fact]
    public void SerializeDeltaPayload_ShipsTheEntryDocument_OnlyWhenItChanged()
    {
        var before = Bundle("<html>OLD</html>", Surface("index.html", "<p>A</p>"));

        var unchanged = Frame(before, Bundle("<html>OLD</html>", Surface("index.html", "<p>A EDITED</p>")));
        Assert.Equal(JsonValueKind.Null, unchanged.GetProperty("document").ValueKind);

        var moved = Frame(before, Bundle("<html>NEW</html>", Surface("index.html", "<p>A</p>")));
        Assert.Equal("<html>NEW</html>", moved.GetProperty("document").GetString());
    }

    /// <summary>The partial map is deliberately NOT called <c>surfaces</c>. A consumer that missed the
    /// discriminator and merged a partial <c>surfaces</c> map as the whole site would silently drop every
    /// unchanged page; with a different name, the same mistake degrades to a missing key instead of data loss.</summary>
    [Fact]
    public void DeltaFrame_DoesNotReuseTheFullPayloadsSurfacesKey()
    {
        var before = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B</p>"));
        var after = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B!</p>"));

        var frame = Frame(before, after);

        Assert.False(frame.TryGetProperty("surfaces", out _));
        Assert.True(frame.TryGetProperty("changedSurfaces", out _));
    }

    /// <summary>The outline is the navigation spine (activity-bar tree + status bar) and rides whole on every
    /// frame — small relative to the surface set, and diffing it would trade real complexity for negligible bytes.
    /// Pinned so a future "optimization" does not quietly strip it and leave the tree stale.</summary>
    [Fact]
    public void DeltaFrame_AlwaysCarriesTheOutline()
    {
        var bundle = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"));

        var frame = Frame(bundle, bundle);

        Assert.True(frame.TryGetProperty("outline", out var outline));
        Assert.True(outline.TryGetProperty("epics", out _));
        Assert.True(outline.TryGetProperty("summary", out _));
    }

    // ===== Goal 2 (spec-vscode-extension-name-latency-and-webview-sunburst): the first-paint prelude split =====

    /// <summary><c>partial</c> is the ONE additive field the split adds, and it must be true on exactly one frame
    /// in a session: the first-paint prelude. Everywhere else it is false, so a panel can never be left answering
    /// "still loading" for a surface that is genuinely unreachable.</summary>
    [Fact]
    public void SerializePayload_IsPartial_OnlyWhenTheCallerAsksForThePreludeFrame()
    {
        var bundle = Bundle("<html>dash</html>", Surface("index.html", "<p>A</p>"));

        var complete = JsonDocument.Parse(
            WebviewCommand.SerializePayload(bundle, "SpecScribeOutput")).RootElement;
        var prelude = JsonDocument.Parse(
            WebviewCommand.SerializePayload(bundle, "SpecScribeOutput", partial: true)).RootElement;

        Assert.False(complete.GetProperty("partial").GetBoolean());
        Assert.True(prelude.GetProperty("partial").GetBoolean());
    }

    /// <summary>A delta always completes its basis, so it must CLEAR <c>partial</c> — including the frame that
    /// completes a prelude. A frame that inherited the basis's flag would strand the panel in "still loading".</summary>
    [Fact]
    public void DeltaFrame_ClearsPartial()
    {
        var bundle = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"));

        Assert.False(Frame(bundle, bundle).GetProperty("partial").GetBoolean());
    }

    /// <summary>The row the whole split lives or dies on: <b>no surface is lost across the split</b>. The prelude
    /// frame plus the delta frame, folded the way <c>applyDeltaFrame</c> folds them host-side, must reconstruct
    /// exactly the surface set a single complete payload would have carried.</summary>
    [Fact]
    public void PreludeThenDelta_ReconstructsExactlyTheOneShotSurfaceSet()
    {
        var families = new[] { Surface("index.html", "<p>dash</p>"), Surface("epics.html", "<p>epics</p>") };
        var longTail = new[] { Surface("docs/a.html", "<p>A</p>", source: "docs/a.md"), Surface("adrs/1.html", "<p>1</p>") };
        var prelude = Bundle("<html>dash</html>", families);
        var complete = Bundle("<html>dash</html>", families.Concat(longTail).ToArray());

        var preludeFrame = JsonDocument.Parse(
            WebviewCommand.SerializePayload(prelude, "SpecScribeOutput", partial: true)).RootElement;
        var deltaFrame = Frame(prelude, complete);

        // Fold exactly as the TS store does: base surfaces, then changed, then removals.
        var merged = preludeFrame.GetProperty("surfaces").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetProperty("content").GetString(), StringComparer.Ordinal);
        foreach (var changed in deltaFrame.GetProperty("changedSurfaces").EnumerateObject())
        {
            merged[changed.Name] = changed.Value.GetProperty("content").GetString();
        }
        foreach (var removed in deltaFrame.GetProperty("removedSurfaces").EnumerateArray())
        {
            merged.Remove(removed.GetString()!);
        }

        Assert.Equal(
            complete.Surfaces.Select(s => s.OutputRelativePath).OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            merged.Keys.OrderBy(p => p, StringComparer.Ordinal).ToArray());
        Assert.All(complete.Surfaces, s => Assert.Equal(s.ContentHtml, merged[s.OutputRelativePath]));
        // The dashboard document does NOT re-ride on the completing frame (it never moved), and the prelude
        // already carried it — so first paint is complete and the 1.5 MB document is shipped exactly once.
        Assert.Equal(JsonValueKind.Null, deltaFrame.GetProperty("document").ValueKind);
        Assert.Equal(complete.EntryDocument, preludeFrame.GetProperty("document").GetString());
        Assert.Equal(complete.EntryPath, preludeFrame.GetProperty("entry").GetString());
    }

    /// <summary>A `--serve` session that never split still behaves as before: the first frame is whole and carries
    /// no <c>partial</c> claim, so an older VSIX (and the one-shot path) are untouched by this work.</summary>
    [Fact]
    public void OneShotShapedPayload_NeverClaimsToBePartial()
    {
        var bundle = Bundle("<html>d</html>", Surface("index.html", "<p>A</p>"), Surface("docs/a.html", "<p>B</p>"));

        var payload = JsonDocument.Parse(WebviewCommand.SerializePayload(bundle, "SpecScribeOutput")).RootElement;

        Assert.False(payload.GetProperty("partial").GetBoolean());
        Assert.Equal(2, payload.GetProperty("surfaces").EnumerateObject().Count());
    }
}
