using System.Net;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 23.6 AC #4 — the Node prerequisite's failure paths, and AC #7's artefact resolution. Extended
/// by Story 16.3 AC #5 with the two defects the 16.1 packaging spike routed here.
///
/// <para><b>Why this file exists at all.</b> ADR 0022 §Decision 5 assigned Node DETECTION to Story 16.3. When
/// Story 23.6 made Node load-bearing for every run, the failure path the ADR promised did not exist, so 23.6
/// built it. AC #4 requires it to be VERIFIED TO FIRE, not merely documented, and the failure it names is
/// specific: a user with no Node must get an actionable error naming the supported range, <b>not</b> a silent
/// empty output root reported at <c>errors=0</c>.</para>
///
/// <para>Story 16.3 has now been built, and it deliberately did <b>not</b> touch Node detection — 16.1 § 9 is
/// explicit that detection shipped in 23.6 and only its PLACEMENT (ADR 0022 said "at startup"; the shipped check
/// runs at prerender time) plus the consumer-facing surfaces remain open, the latter routed to 16.6. What 16.3
/// added here is the repo-root walk's worktree case and the non-200 diagnostics helper.</para>
///
/// <para>These tests are deliberately Node-free and artefact-free. Story 23.6's Dev Notes forbid making the C#
/// unit suite depend on either, so the range arithmetic, the resolution failure, the repo-root walk and the
/// failure-text extraction are all tested directly rather than by installing runtimes or building artefacts.
/// <c>InternalsVisibleTo</c> is what makes that possible for the last two.</para></summary>
public class NuxtPrerenderTests
{
    // ── The supported range: ^22.19.0 || ^24.11.0 || >=26.0.0 ───────────────────────────────────────────────

    [Theory]
    // ^22.19.0 — same major, at or above 19.0. The caret is why 22.18 is BELOW range rather than merely older.
    [InlineData(22, 19, 0, true)]
    [InlineData(22, 19, 7, true)]
    [InlineData(22, 20, 0, true)]
    [InlineData(22, 18, 9, false)]
    [InlineData(22, 0, 0, false)]
    // ^24.11.0
    [InlineData(24, 11, 0, true)]
    [InlineData(24, 12, 3, true)]
    [InlineData(24, 10, 5, false)]
    // >=26.0.0
    [InlineData(26, 0, 0, true)]
    [InlineData(27, 4, 1, true)]
    // The ODD majors are outside the range entirely — they are not "newer and therefore fine". Node's odd
    // majors are non-LTS, and a caret range never spans a major boundary.
    [InlineData(23, 9, 9, false)]
    [InlineData(25, 0, 0, false)]
    // Ancient
    [InlineData(18, 20, 4, false)]
    public void IsSupported_MatchesTheRangeTheErrorMessageQuotes(int major, int minor, int patch, bool expected)
    {
        Assert.Equal(expected, NuxtPrerender.IsSupported(major, minor, patch));
    }

    [Fact]
    public void SupportedNodeRange_MatchesWebPackageJsonEnginesField()
    {
        // The constant is quoted verbatim into every failure message, so a user who reads the error and installs
        // what it names must end up with something `web/` also accepts. Two hand-maintained copies of a version
        // range is exactly how a user gets told to install a Node the build then rejects.
        var packageJson = File.ReadAllText(Path.Combine(RepoRoot(), "web", "package.json"));
        Assert.Contains(NuxtPrerender.SupportedNodeRange, packageJson);
    }

    // ── AC #4, failure path 2: Node is present but BELOW the supported range ────────────────────────────────

    [Theory]
    [InlineData("v20.11.1")]  // the previous LTS — the single most likely version a real user has
    [InlineData("v22.18.0")]  // right major, below the caret floor: the case a ">= major" check would wrongly pass
    [InlineData("v18.20.4")]
    [InlineData("v23.9.0")]   // odd major, NUMERICALLY newer than 22.19 and still out of range
    public void ValidateNodeVersion_BelowRange_ThrowsNamingTheSupportedRangeAndTheVersionFound(string version)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => NuxtPrerender.ValidateNodeVersion(version));

        // The message must name BOTH what is required and what was found. "Unsupported Node" alone leaves the
        // user guessing which half is wrong.
        Assert.Contains(NuxtPrerender.SupportedNodeRange, ex.Message);
        Assert.Contains(version, ex.Message);
        Assert.Contains("nodejs.org", ex.Message);
        // AC #4's headline: the consequence must be stated, not implied. A user who reads this must understand
        // that the run produces no HTML — the failure this story creates is a SILENTLY empty output root.
        Assert.Contains("cannot produce any HTML page", ex.Message);
    }

    [Theory]
    [InlineData("v22.19.0")]
    [InlineData("v24.11.1")]
    [InlineData("v26.0.0")]
    public void ValidateNodeVersion_InRange_DoesNotThrow(string version)
    {
        NuxtPrerender.ValidateNodeVersion(version);
    }

    [Fact]
    public void ValidateNodeVersion_UnparseableOutput_IsTreatedAsAbsentRatherThanAssumedFine()
    {
        // Fail closed. If `node --version` prints something unexpected (a shim, a wrapper, a proxy banner), the
        // safe reading is "I cannot establish that Node is usable", not "probably fine".
        var ex = Assert.Throws<InvalidOperationException>(() => NuxtPrerender.ValidateNodeVersion("not-a-version"));
        Assert.Contains(NuxtPrerender.SupportedNodeRange, ex.Message);
    }

    // ── AC #4, failure path 3: the artefact directory is missing ────────────────────────────────────────────

    [Fact]
    public void ResolveArtefactDirectory_WithNoOverrideAndNoArtefact_ThrowsNamingAllThreeLocations()
    {
        // The DEFAULT search path, with SPECSCRIBE_RENDERER_DIR unset — and started from a directory outside any
        // git checkout, which is the case that originally produced a two-of-three list.
        var empty = Directory.CreateTempSubdirectory("specscribe-no-artefact-");
        var previous = Environment.GetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR");
        try
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", null);

            var ex = Assert.Throws<InvalidOperationException>(
                () => NuxtPrerender.ResolveArtefactDirectory(empty.FullName));

            // A miss must name every location it looked in and the command that fixes it. "Renderer not found"
            // alone leaves a user with no next step, and the whole point of AC #4 is that the failure is
            // ACTIONABLE rather than merely present.
            Assert.Contains("SPECSCRIBE_RENDERER_DIR", ex.Message);
            Assert.Contains("renderer/", ex.Message);
            Assert.Contains("web/.output/", ex.Message);
            Assert.Contains("npm run build:package", ex.Message);
            Assert.Contains("server/index.mjs", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", previous);
            empty.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveArtefactDirectory_HonoursTheEnvironmentOverrideAheadOfEverythingElse()
    {
        var dir = Directory.CreateTempSubdirectory("specscribe-artefact-");
        var previous = Environment.GetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "server"));
            File.WriteAllText(Path.Combine(dir.FullName, "server", "index.mjs"), "// stub");
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", dir.FullName);

            Assert.Equal(dir.FullName, NuxtPrerender.ResolveArtefactDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", previous);
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveArtefactDirectory_AnExplicitOverrideThatDoesNotResolve_FailsRatherThanFallingThrough()
    {
        // Regression guard for a real defect this file caught. Resolution used to treat the override as merely
        // the FIRST candidate, so an operator who pointed SPECSCRIBE_RENDERER_DIR at a typo'd or half-built
        // directory silently got the repo's own `web/.output/` instead — rendering with a different artefact
        // than the one they named, and reporting success. Same class as Story 23.5's Nitro-serves-public/-first
        // finding: a wrong answer with a success status.
        var dir = Directory.CreateTempSubdirectory("specscribe-halfbuilt-");
        var previous = Environment.GetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "server")); // present but EMPTY — no index.mjs
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", dir.FullName);

            // Started from the real repo root, where `web/.output/` genuinely exists — so a fall-through would
            // SUCCEED and this assertion is the thing standing between that and shipping.
            var ex = Assert.Throws<InvalidOperationException>(
                () => NuxtPrerender.ResolveArtefactDirectory(RepoRoot()));
            Assert.Contains(dir.FullName, ex.Message);
            Assert.Contains("npm run build:package", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", previous);
            dir.Delete(recursive: true);
        }
    }

    // ── Story 16.3 AC #5(a): the repo-root walk must recognise a git WORKTREE ───────────────────────────────

    [Fact]
    public void FindRepoRoot_StopsAtAWorktreeRoot_WhereDotGitIsAFileRatherThanADirectory()
    {
        // THE DEFECT THIS PINS, in the shape it actually occurred. `.git` is a DIRECTORY in a normal checkout and
        // a ~56-byte `gitdir:` FILE in a worktree. The walk used to test Directory.Exists only, so a worktree was
        // invisible and the walk continued to the ENCLOSING checkout — and candidate 3 then resolved that other
        // checkout's web/.output. The generate did not fail; it rendered from the wrong artefact and reported
        // success. Story 16.1 observed exactly this from inside .claude/worktrees/story-16-1-dev.
        //
        // The layout below is the real one: an outer checkout with a .git DIRECTORY, and a worktree nested under
        // it whose .git is a FILE. Before the fix this returned `outer`; it must return the worktree.
        var scratch = Directory.CreateTempSubdirectory("specscribe-worktree-walk-");
        try
        {
            var outer = Directory.CreateDirectory(Path.Combine(scratch.FullName, "outer"));
            Directory.CreateDirectory(Path.Combine(outer.FullName, ".git"));

            var worktree = Directory.CreateDirectory(Path.Combine(outer.FullName, "worktrees", "story-x"));
            File.WriteAllText(Path.Combine(worktree.FullName, ".git"), "gitdir: ../../.git/worktrees/story-x\n");

            var deep = Directory.CreateDirectory(Path.Combine(worktree.FullName, "web", ".output"));

            Assert.Equal(Canonical(worktree.FullName), Canonical(NuxtPrerender.FindRepoRoot(deep.FullName)!));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindRepoRoot_StillStopsAtANormalCheckout_WhereDotGitIsADirectory()
    {
        // The other half: widening the test to "file OR directory" must not have broken the ordinary case.
        var scratch = Directory.CreateTempSubdirectory("specscribe-normal-walk-");
        try
        {
            var repo = Directory.CreateDirectory(Path.Combine(scratch.FullName, "repo"));
            Directory.CreateDirectory(Path.Combine(repo.FullName, ".git"));
            var deep = Directory.CreateDirectory(Path.Combine(repo.FullName, "src", "nested"));

            Assert.Equal(Canonical(repo.FullName), Canonical(NuxtPrerender.FindRepoRoot(deep.FullName)!));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindRepoRoot_OutsideAnyCheckout_ReturnsNullRatherThanGuessing()
    {
        // Null is what makes ResolveArtefactDirectory print "(skipped — no git repository above the working
        // directory)" instead of a fabricated path, which is the actionable half of its three-location error.
        var scratch = Directory.CreateTempSubdirectory("specscribe-no-repo-");
        try
        {
            // The temp root is not inside a checkout; if it ever were, this asserts the walk found SOMETHING
            // above rather than silently passing for the wrong reason.
            var found = NuxtPrerender.FindRepoRoot(scratch.FullName);
            Assert.True(found is null || Directory.Exists(Path.Combine(found, ".git")) || File.Exists(Path.Combine(found, ".git")));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    /// <summary>Temp paths can arrive via a symlinked or 8.3-shortened <c>%TEMP%</c>, and the walk returns
    /// <c>DirectoryInfo.FullName</c> — so the two sides are normalised before comparison rather than asserting on
    /// a string that is right but spelled differently.</summary>
    private static string Canonical(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToLowerInvariant();

    // ── Story 16.3 AC #5(b): a non-200 must carry the RENDERER'S OWN error text ─────────────────────────────

    [Fact]
    public void DescribeRouteFailure_NitroJsonBody_LiftsOutTheMessageAndLeavesTheStackBehind()
    {
        // The real shape. Story 16.1 could only obtain this sentence by booting the artefact by hand — a packaged
        // consumer with no web/ checkout has no such path, which is why the message has to come back in-band.
        var body = """
            {"statusCode":500,"statusMessage":"","message":"The epics index IR entry declares no child pages, so the surface cannot render.","stack":["at EpicsIndexSurface (./chunks/build/EpicsIndexSurface.mjs:41:11)","at renderComponentSubTree (./chunks/build/server.mjs:1102:9)"]}
            """;

        var described = NuxtPrerender.DescribeRouteFailure(HttpStatusCode.InternalServerError, body);

        Assert.Contains("HTTP 500", described);
        Assert.Contains("The epics index IR entry declares no child pages", described);
        // The stack is deliberately NOT carried: it is the bulkiest and least actionable part, and the server-log
        // tail (emitted once per run) is where that class of detail belongs.
        Assert.DoesNotContain("renderComponentSubTree", described);
        Assert.DoesNotContain("statusCode", described);
    }

    [Fact]
    public void DescribeRouteFailure_NonJsonBody_KeepsTheRawTextRatherThanDiscardingIt()
    {
        // An HTML error page or an interposed proxy banner. Unparseable is not the same as uninformative, and the
        // failure this whole helper exists to prevent is a bare status code with the cause thrown away.
        var described = NuxtPrerender.DescribeRouteFailure(
            HttpStatusCode.BadGateway, "<html><body><h1>502 Bad Gateway</h1><p>upstream closed</p></body></html>");

        Assert.Contains("HTTP 502", described);
        Assert.Contains("upstream closed", described);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void DescribeRouteFailure_EmptyBody_FallsBackToTheBareStatusSentenceWithNoDanglingColon(string? body)
    {
        var described = NuxtPrerender.DescribeRouteFailure(HttpStatusCode.NotFound, body);

        Assert.Equal("the renderer answered HTTP 404 for a route the manifest names.", described);
        Assert.DoesNotContain(": ", described);
    }

    [Fact]
    public void DescribeRouteFailure_IsBounded_SoOneRunCannotFloodTheConsole()
    {
        // 373 routes × an unbounded stack trace is not a diagnostic, it is a denial of service against the console
        // and against the diagnostics surface that carries these events. The cap is the reason this can be safely
        // attached to EVERY failing route.
        var described = NuxtPrerender.DescribeRouteFailure(
            HttpStatusCode.InternalServerError, new string('x', 20_000));

        Assert.True(described.Length < 700, $"one route's failure text grew to {described.Length} chars");
        Assert.Contains("(truncated)", described);
    }

    [Fact]
    public void DescribeRouteFailure_CollapsesNewlines_SoOneFailureStaysOneConsoleLine()
    {
        var described = NuxtPrerender.DescribeRouteFailure(
            HttpStatusCode.InternalServerError, "{\"message\":\"line one\\nline two\\n\\n   line three\"}");

        Assert.Contains("line one line two line three", described);
        Assert.DoesNotContain("\n", described);
    }

    // ── The prerender must never be a SILENT no-op ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithNoArtefact_ThrowsRatherThanReturningAnEmptySuccess()
    {
        // This is AC #4's headline failure restated as a test: "not a silent empty output root". A `Result` with
        // Rendered=0 and Failed=0 would report `errors=0` to the CLI and the diagnostics page, which reads as
        // success on a run that produced no HTML at all.
        var output = Directory.CreateTempSubdirectory("specscribe-prerender-");
        var previous = Environment.GetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR");
        try
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", Path.Combine(output.FullName, "absent"));
            Assert.Throws<InvalidOperationException>(
                () => NuxtPrerender.Render(output.FullName, ["index.html"], artefactDir: null));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", previous);
            output.Delete(recursive: true);
        }
    }

    // ── The live prerequisite, on the machine actually running the suite ────────────────────────────────────

    [Fact]
    public void VerifyNodeAvailable_OnThisMachine_ReturnsAVersionInsideTheSupportedRange()
    {
        // Not a tautology: this suite's own CI runners and the owner's box are exactly the environments where a
        // wrong range constant would be discovered late. If Node is genuinely absent here, the assertion below
        // is the actionable message a USER would see, which is the thing AC #4 is about.
        var version = NuxtPrerender.VerifyNodeAvailable();
        Assert.StartsWith("v", version);
    }

    // ── The prerequisite failure must reach the DIAGNOSTICS PAGE, not only the console ───────────────────────

    [Fact]
    public void GenerateAll_WithPrerenderOnAndNoArtefact_PutsTheRendererErrorOnTheDiagnosticsPage()
    {
        // Regression guard for a defect found in the field (VS Code extension, 2026-08-01): the user's output
        // channel showed `x (renderer) - The SpecScribe renderer artefact could not be found` and `errors=1`,
        // while diagnostics.html — a complete, readable page, because the prerender never overwrote the C#-written
        // one — showed no such notice at all.
        //
        // The cause was ORDERING, not routing. WriteDiagnostics snapshots its notice list; the prerender runs
        // ~35 lines later because it needs EmitSpaSite's manifest on disk; so anything the prerender raised could
        // reach the console, the `errors=N` line and the exit code, but never the page. Two reporting surfaces
        // disagreeing is precisely what this page exists to prevent, so the PREREQUISITE check was split out and
        // moved ahead of the write (SiteGenerator.PrerenderPreflight).
        //
        // Node is verified BEFORE the artefact (NuxtPrerender.Render documents why), and this suite already pins
        // that Node is present on any machine running it, so the failure this test forces is the artefact one.
        //
        // Specifically the EXPLICIT-OVERRIDE variant, not the search-exhausted one the field report quoted. Both
        // are the same class — ResolveArtefactDirectory throwing before any route renders — but only the override
        // is reachable from inside this checkout: the search path walks up from the working directory and would
        // find the repo's own web/.output/ and succeed. What is under test is the ORDERING, which is indifferent
        // to which of the two messages ends up being carried.
        var root = Directory.CreateTempSubdirectory("specscribe-preflight-").FullName;
        var previous = Environment.GetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR");
        try
        {
            var source = Path.Combine(root, "_bmad-output", "planning-artifacts");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "epics.md"), """
                # Epics

                ## Epic 1: Rendering

                ### Story 1.1: Render a page

                As a reader, I want a page, so that I can read it.
                """);

            // An explicit override that cannot resolve is a hard error by design, which makes it the cheapest way
            // to force the prerequisite failure without uninstalling Node or hiding the repo's own web/.output/.
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", Path.Combine(root, "no-such-renderer"));

            var site = Path.Combine(root, "site");
            var options = ForgeOptions.Resolve(
                source: Path.Combine(root, "_bmad-output"),
                adrs: Path.Combine(root, "docs", "adrs"),
                output: site,
                projectName: "TestProj");
            var events = new SiteGenerator(options) { PrerenderHtml = true }.GenerateAll();

            // 1. The run still fails loudly — the old behaviour that was already correct, pinned so the fix cannot
            //    be mistaken for "swallow the error earlier".
            var renderer = Assert.Single(events.Where(e => e.RelativePath == "(renderer)"));
            Assert.Equal(GenerationOutcome.Error, renderer.Outcome);
            Assert.Contains("renderer artefact", renderer.Message, StringComparison.OrdinalIgnoreCase);

            // 2. …and exactly ONCE. The preflight failing must skip the render rather than let it re-raise the
            //    identical failure, or the user reads the same problem twice and doubts both copies.
            Assert.Single(events.Where(e =>
                e.Outcome == GenerationOutcome.Error &&
                e.Message.Contains("renderer artefact", StringComparison.OrdinalIgnoreCase)));

            // 3. The headline: it is ON THE PAGE. Read from the IR because that is what every surface projects
            //    from and what the Nuxt renderer renders (see SiteRegion).
            var diagnostics = SiteRegion.Read(site, "diagnostics.html");
            Assert.Contains("(renderer)", diagnostics, StringComparison.Ordinal);
            Assert.Contains("is not a renderer artefact", diagnostics, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR", previous);
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "_bmad-output")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repo root (no _bmad-output above the test assembly).");
    }
}
