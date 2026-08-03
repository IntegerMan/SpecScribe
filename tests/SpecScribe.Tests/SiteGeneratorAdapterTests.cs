using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 4.1: with ingestion routed through
/// <see cref="BmadArtifactAdapter"/>, the generated site is exactly what the inline parse chain produced —
/// pinned here as a golden inventory of every output file a representative BMad fixture yields — and adapter
/// diagnostics surface on the existing event channel without failing the run or suppressing sibling pages
/// (AC #2). The full byte-for-byte before/after diff was performed against a frozen copy of this repo's own
/// artifacts at implementation time (zero diffs, modulo the wall-clock footer and the build-derived asset
/// cache-bust token); this fixture keeps the shape of that guarantee alive in the suite. Follows the temp-dir
/// fixture style of <see cref="SiteGeneratorSprintTests"/>.</summary>
public class SiteGeneratorAdapterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-adaptergen-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
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

        ### Epic 2: Delivery

        Ship the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).

        **Acceptance Criteria:**

        1.
        **Given** an undrafted story
        **When** the site generates
        **Then** a placeholder page exists

        ## Epic 2: Delivery

        ### Story 2.1: Delivery Story

        As a maintainer, I want delivery.
        """;

    // Epic 2 is all-done but has NO retrospective — the ForEpic vs ForEpicWithRetrospective divergence (an
    // all-done-without-retro epic reads as "In review" on the visual status surfaces). Keeping this in the golden
    // fixture is what makes the byte gate actually EXERCISE that retro-gated branch, which it previously did not.
    // [Story 6.2 review]
    private const string Story21Md = """
        # Story 2.1: Delivery Story

        Status: done

        ## Story

        As a maintainer, I want delivery.

        ## Acceptance Criteria

        1. It ships.

        ## Tasks / Subtasks

        - [x] Task 1: Ship it (AC: #1)
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
          1-2-undrafted: backlog
          epic-2: done
          2-1-delivery: done
        """;

    public SiteGeneratorAdapterTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "2-1-delivery.md"), Story21Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "epic-1-retro-2026-07-06.md"), RetroMd);
        File.WriteAllText(SprintYaml, SprintYamlContent);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    [Fact]
    public void GenerateAll_GoldenOutputInventory_IsExactlyThePreAdapterPageSet()
    {
        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // The activity timeline + date pages are now git-derived (Story 7.3 bug fix): they no longer fire on the
        // filesystem-mtime signal, so this NON-git fixture yields NEITHER timeline.html NOR any commits/ date page —
        // the honest degradation of "drop the claim when git can't verify it" (the mtime signal collapsed every
        // artifact onto the checkout day). The date-fold is kept defensively in case any surface still stamps today.
        var todayIso = Charts.D(DateOnly.FromDateTime(DateTime.Now));
        var actual = Directory.EnumerateFiles(Site, "*", SearchOption.AllDirectories)
            .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p)).Replace(todayIso, "<date>"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // The exact page set the pre-adapter pipeline produced for this fixture — a new, missing, or
        // relocated output file is a rendering-behavior change and must be a deliberate decision, never a
        // side effect of adapter work (AC #1: rendering stays framework-agnostic and unchanged).
        var expected = new[]
        {
            // about.html + diagnostics.html are the Story 4.8 additions to the page set — deliberate output
            // change (this story adds pages + a site-wide footer link), unlike the byte-parity 4.1/4.2 stories.
            "about.html",
            "adrs/index.html",
            // ── Story 23.6 AC #6: the IR is now emitted UNCONDITIONALLY, so its files join this inventory. ──
            //
            // These four are not new artifacts — they are the same `spa/` delivery form this project has
            // emitted since Story 6.7. What changed is that they used to require `--spa`, and this fixture does
            // not pass it. The IR is now the canonical output (ADR 0016) and the static pages are rendered FROM
            // it (ADR 0022 §Decision 3), so a run that emitted none of these would emit nothing at all.
            //
            // ⚠️ THIS TEST IS RE-PINNED, NOT SUPERSEDED — the AC #2 disposition for dependent #3.
            // `check:parity` does NOT cover it: that gate renders a deliberately FROZEN 24-route corpus, so it
            // is structurally blind to a whole page family silently ceasing to be emitted by a real generate.
            // This test is exactly that check, and the two are complementary rather than redundant: this one
            // pins the SET a live generate produces, check:parity pins the CONTENT a fixed input renders to.
            "app.html",
            "specscribe-spa.js",
            "spa/manifest.json",
            // One chunk per top-level content group — the bounded grouping Story 6.7 chose over one file per
            // page. A NEW chunk name appearing here means a new content group reached the IR, which is exactly
            // the kind of deliberate output change this inventory exists to make visible.
            "spa/pages-adrs.json",
            "spa/pages-epics.json",
            "spa/pages-implementation-artifacts.json",
            "spa/pages-requirements.json",
            "spa/pages-root.json",
            // Story 7.6: code-map.html replaced the retired Story 3.4 structure.html (source-code treemap; the
            // fixture's repo-root walk finds its markdown files, so the surface generates).
            "code-map.html",
            // Story 7.10 (review pass): the refactor-target risk quadrant moved off code-map.html onto its own
            // Insights page — rides the same source-code-walk gating signal, so it generates whenever code-map.html
            // does.
            "risk-quadrant.html",
            // Story 7.3 bug fix: timeline.html + commits/ date pages are git-derived now, so this non-git fixture
            // emits none of them (previously the mtime signal produced a today-stamped date page + timeline here).
            "diagnostics.html",
            "epics.html",
            "epics/epic-1.html",
            "epics/epic-2.html",
            "epics/story-1-1.html",
            "epics/story-1-2.html",
            "epics/story-2-1.html",
            // Story 10.3: the how-to-read orientation page is written on every full run, like about.html/diagnostics.html.
            "how-to-read.html",
            // Story 23.2: the design-system reference — same always-written guarantee, so its Help link never dangles.
            "design-system.html",
            // About Spec-Driven Development hub + per-framework sub-pages (always written).
            "about-sdd.html",
            "about-sdd-bmad.html",
            "about-sdd-gds.html",
            "about-sdd-speckit.html",
            "about-sdd-gsd.html",
            "about-sdd-gsd-pi.html",
            "about-sdd-superpowers.html",
            "cadence.html",
            "implementation-artifacts/epic-1-retro-2026-07-06.html",
            "index.html",
            // Story 20.5: the vendored plotly.js hierarchy engine. Present because this fixture HAS epics, so its
            // dashboard hosts a Hierarchy Explorer — the conditional-emission guard is what keeps it out of a
            // fixture without one (SiteGeneratorSpaTests.HierarchyEngineBundle_ShipsOnlyWhereAHierarchyChartWasRendered
            // pins both directions).
            "plotly-hierarchy.min.js",
            "requirements.html",
            "requirements/fr1.html",
            "requirements/nfr1.html",
            "retros.html",
            "specscribe.css",
            "specscribe.js",
            "sprint.html",
            "traceability.html",
        }.OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
    }

    // ── RETIRED: GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens ────────────
    //
    // [Story 23.6 Task 5, dependent #2 — AC #2 requires an explicit disposition, so here it is.]
    //
    // WHAT IT WAS. A single SHA-256 over every generated output file, after neutralizing the wall-clock
    // footer, the ?v=<ModuleVersionId> asset cache-bust, CRLF and the build-derived product version. It was
    // this project's content-drift gate from Story 4.1 until now, and it did real work: it is the check that
    // caught silent rendering drift which kept the file set stable, which GoldenOutputInventory cannot see.
    //
    // WHY IT IS RETIRED, and it is NOT because it became inconvenient. ⚠️ ITS SUBJECT NO LONGER EXISTS.
    // Story 23.6 deletes the C# page writer, so there are no C#-rendered .html bytes left to hash. The pages
    // in the output root are now written by the Nuxt renderer from the IR (ADR 0022 §Decision 3). Left in
    // place it would have hashed a tree it no longer describes.
    //
    // ⚠️ IT WAS DELIBERATELY *NOT* RE-POINTED AT THE IR. ADR 0033 names this story and forbids exactly that:
    // "whatever replaces it when the C# page writer is retired takes the targeted shape rather than being
    // re-pointed at the IR as another whole-tree hash." Story 23.4 had already tried it — GoldenIrFingerprint
    // produced three different hashes across this box, CI-Windows and CI-Ubuntu for one identical commit and
    // was removed (70b72ab).
    //
    // ITS SUCCESSOR is `npm run check:parity` (web/scripts/check-parity.mjs), which is strictly stronger on
    // the axis that matters here:
    //   · it names the PAGE that moved instead of printing one changed hex string;
    //   · it renders a FROZEN corpus, so it cannot go red for a reason unrelated to the change under test;
    //   · it hashes the WHOLE PAGE, not just <main> — so <title>, meta, the favicon, the footer, <script src>,
    //     the nav toggle, the Mermaid init and the Hierarchy/Graph anti-flash handshakes are covered for the
    //     first time. Those are precisely what HtmlRenderAdapter.Render emitted and what this story deletes,
    //     and NOTHING hashed them before;
    //   · it is regenerated by a command producing a reviewable per-route diff (`npm run pin:parity`), never
    //     by editing a hex literal.
    //
    // The ~148 KB of accumulated "Regenerated for Story X" commentary this method carried is not reproduced
    // here. It is provenance, and it is preserved where provenance belongs — in this file's git history, at
    // and before the Story 23.6 commit.
    //
    // GoldenOutputInventory (above) SURVIVES and is re-pinned; see its own note for that disposition.


    // The volatile-token folds themselves live in GoldenNormalization, SHARED with the Story 22.5 oracle-diff
    // harness. They were inlined here until Story 22.5 AC #5 required one copy rather than two: this gate pins
    // full-generation output and that one pins incremental output AGAINST full-generation output, so a fold in
    // one and not the other is a hole in whichever gate lacks it. The fold set is byte-for-byte what this file
    // carried before the extraction — the constant below did not move.

    // ── GenerateAll_GoldenIrFingerprint_IsStableAfterNormalizingVolatileTokens — REMOVED 2026-07-30 ──
    //
    // This was Story 23.4 AC #5's IR-level content-drift gate (a SHA-256 over spa/manifest.json + its content
    // chunks, folding the manifest's per-page contentHash values so a fresh temp fixture path never moved it).
    // Removed by owner decision during Story 22.6's code-review follow-up, after the gate proved
    // non-deterministic in a way that stopped being economical to keep chasing:
    //
    //   - CI's `portability-probe` (Ubuntu) and `build-test-analyze` (Windows) jobs produced DIFFERENT actual
    //     hashes for the SAME commit — the signature that first proved this wasn't "just needs regeneration."
    //   - One real cause WAS found and fixed: `SiteGenerator.FallbackCodeWalk` (the non-git code-map source
    //     walk this fixture exercised) fed an unsorted `Directory.GetFileSystemEntries` result into a
    //     stack-based walk — genuinely non-portable (NTFS vs. ext4/APFS enumerate a directory differently).
    //     That fix is KEPT (see `FallbackCodeWalk`'s own comment) and is pinned by
    //     `SiteGeneratorSpaTests.CodeMapFallbackWalk_ListsFiles_InDeterministicSortedOrder_NotFilesystemEnumerationOrder`.
    //   - After that fix, THREE different environments (this repo's dev machine, CI Windows, CI Ubuntu) still
    //     produced THREE different hashes on the identical commit — proving a SECOND, unidentified source of
    //     non-determinism (not merely an OS-pair difference, since even two Windows environments disagreed).
    //     Not root-caused before this decision; a real gap in this project's build determinism, not a false
    //     alarm dismissed without investigation.
    //
    // `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` (above) is UNAFFECTED — its
    // fixture never exercises `--spa`/the IR path, so it never depended on any of this. Story 23.4's own AC #5
    // intent (a content-drift gate that survives the eventual C# page-writer deletion) is left uncovered by
    // this removal; whoever next touches Story 23.4 / the IR pipeline should either rebuild this gate on
    // genuinely deterministic normalization or accept `measure:parity`'s committed per-page hashes (main-region
    // only) as the interim substitute. Recorded in `_bmad-output/implementation-artifacts/deferred-work.md`.
    private string FingerprintTree(string root) => FingerprintTree(root, static s => s);

    private string FingerprintTree(string root, Func<string, string> extraFold)
    {
        var sb = new StringBuilder();
        foreach (var rel in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(root, p)))
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            var full = Path.Combine(root, rel);
            sb.Append(FoldToday(rel)).Append('\n')
              .Append(IsVendoredAsset(rel) ? VendoredAssetToken(full)
                  : IsCopiedAsset(rel) ? FoldLineEndings(File.ReadAllText(full))
                  : extraFold(NormalizeVolatile(File.ReadAllText(full))))
              .Append("\n \n");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>Third-party bundles SpecScribe vendors verbatim and never renders. Story 20.5's plotly bundle is
    /// 1.2 MB of minified JavaScript, and folding it in whole was a bad trade twice over: every run pushed it
    /// through <see cref="NormalizeVolatile"/>'s regexes (which are written for RENDERED markup and have no
    /// business pattern-matching a minifier's output — a coincidental match there would move the golden constant
    /// with no rendering change behind it), and a diff would point at an opaque blob rather than at anything a
    /// reader could act on. The asset's IDENTITY is what the fingerprint actually needs to pin. [Story 20.5]</summary>
    /// <summary>SpecScribe's OWN embedded assets, copied to the output byte-for-byte by
    /// <c>CopyEmbeddedAsset</c> and never rendered. They must be pinned by CONTENT — a stylesheet change is a real
    /// rendering change — but they must NOT go through <see cref="NormalizeVolatile"/>, whose folds are written for
    /// generated markup and actively corrupt static source.
    ///
    /// <para><b>The concrete bug this fixes.</b> <c>FoldToday</c> rewrites TODAY's date to the <c>&lt;date-iso&gt;</c>
    /// placeholder so the Story 7.3 artifact-mtime date pages don't drift the constant day to day. Both strings are
    /// exactly ten characters, so the substitution is length-neutral and invisible to any size check. specscribe.css
    /// carries a dated SOURCE COMMENT ("[owner verify round 2026-07-25]") that has nothing to do with generation —
    /// and on the one calendar day that date IS today, it got folded. The golden constant therefore depended on the
    /// wall-clock DATE and the machine's TIME ZONE: it was captured on a box whose local date was 2026-07-25, and
    /// failed on CI runners already at 2026-07-26 UTC. It would equally have started failing on the author's own
    /// machine the next morning, with no code change behind it — read as a rendering regression, inviting a
    /// needless regeneration. Only rendered HTML can contain a GENERATED date, so only rendered HTML is folded.
    /// [Story 25.1 CI; golden-diff-normalization-gotchas]</para></summary>
    /// <summary>Single source of truth for both <see cref="IsCopiedAsset"/> and <see cref="IsVendoredAsset"/>.
    /// Story 25.1's review found the original shape — two independently hand-maintained boolean predicates —
    /// let one list drift from the other with no error. A shared map cannot disagree with itself. [Review][Patch,
    /// Story 25.1 code review]</summary>
    private static readonly IReadOnlyDictionary<string, bool> KnownStaticAssets = new Dictionary<string, bool>
    {
        [ForgeOptions.HierarchyEngineScriptName] = true, // vendored
        [ForgeOptions.CodeHighlightScriptName] = true, // vendored
        [ForgeOptions.CodeHighlightStyleName] = true, // vendored
        [ForgeOptions.StylesheetName] = false, // copied, first-party
        [ForgeOptions.ScriptName] = false, // copied, first-party
        [SpaDelivery.ScriptName] = false, // copied, first-party
    };

    private static bool IsCopiedAsset(string relativePath) =>
        KnownStaticAssets.TryGetValue(relativePath, out var vendored) && !vendored;

    /// <summary>The one normalization a verbatim-copied asset still needs. The repo now pins <c>eol=lf</c> in
    /// <c>.gitattributes</c>, so a fresh checkout is LF on every platform — but this fold is deliberately KEPT:
    /// a working tree predating that file (or written by a tool that ignores it) still carries CRLF, and
    /// content, not checkout, is what the fingerprint pins. Removing it would make the golden hash depend on
    /// how the tree happened to be checked out.</summary>
    private static string FoldLineEndings(string content) => content.Replace("\r\n", "\n");

    private static bool IsVendoredAsset(string relativePath) =>
        KnownStaticAssets.TryGetValue(relativePath, out var vendored) && vendored;

    /// <summary>Identity token for a vendored asset: name, exact byte length, and a content hash — so a changed or
    /// re-vendored bundle still flips the fingerprint (a length-only token would let a same-size rebuild through),
    /// without the bytes themselves entering the normalization path.</summary>
    private static string VendoredAssetToken(string fullPath)
    {
        // Line endings are folded to LF FIRST — the same normalization NormalizeVolatile applies to every other
        // file, and the form these assets have in git's index. Without it the token was checkout-dependent, not
        // content-dependent: this repo has no .gitattributes, so a vendored bundle is classified as text and
        // materializes as CRLF wherever core.autocrlf=true (a typical Windows dev box) and as LF everywhere else,
        // including every GitHub runner. `git ls-files --eol` reports `i/lf w/crlf attr/` for it. plotly's bundle
        // carries 48 line breaks, so the two checkouts differ by 48 bytes and produce different SHA-256s — which
        // made the golden constant pass on the machine that captured it and fail in CI on both windows-latest and
        // ubuntu-latest, reading as a rendering regression when nothing rendered differently at all. That directly
        // contradicted FingerprintTree's own contract ("portable across machines and CI, not pinned to this box").
        // The asset's IDENTITY is still fully pinned: a re-vendored or edited bundle changes its non-newline bytes
        // and still flips the token. [Story 25.1 CI]
        //
        // The fold operates on the RAW bytes, not via File.ReadAllText — a text round-trip auto-detects and strips
        // a BOM and silently replaces any invalid byte sequence with U+FFFD, which would let the token stay
        // unchanged across an added/removed BOM or drift unpredictably for a non-UTF-8 vendored asset. Neither
        // risk touches the file's actual identity. [Story 25.1 code review]
        var bytes = FoldCrLfBytes(File.ReadAllBytes(fullPath));
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return $"<vendored asset: {Path.GetFileName(fullPath)}, {bytes.Length} bytes, sha256:{sha[..16]}>";
    }

    /// <summary>Removes CRLF -> LF byte pairs without any text-encoding round-trip, so a BOM or a non-UTF-8 byte
    /// sequence in a vendored asset passes through untouched.</summary>
    private static byte[] FoldCrLfBytes(byte[] raw)
    {
        var result = new List<byte>(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
            result.Add(raw[i]);
        }
        return result.ToArray();
    }

    /// <summary>Folds today's date (the ISO filename/href form and the readable heading form) to stable
    /// placeholders. Story 7.3's artifact-mtime date page + timeline are stamped with the generation date, so
    /// without this the fingerprint would drift day to day even with no rendering change.</summary>
    private static string FoldToday(string s) => GoldenNormalization.FoldToday(s);

    /// <summary>The fixture root is folded because the diagnostics page prints the ABSOLUTE repo root (a random
    /// per-run temp dir, and machine-specific), so the golden pins rendered content, not the box.</summary>
    private string NormalizeVolatile(string content) => GoldenNormalization.NormalizeVolatile(content, _root);

    [Fact]
    public void GenerateAll_UnusableSprintYaml_ReportsSkippedDiagnosticAndSiblingsStillRender()
    {
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();

        // AC #2: the unsupported shape is categorized and reported as non-fatal on the existing event
        // channel — never an Error, never an abort…
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        var diag = Assert.Single(events, e => e.Outcome == GenerationOutcome.Skipped && e.RelativePath.EndsWith("sprint-status.yaml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("development_status", diag.Message);

        // …and every successful artifact still renders, while the sprint surfaces omit cleanly.
        Assert.False(SiteRegion.Exists(Site, "sprint.html"));
        Assert.True(SiteRegion.Exists(Site, "index.html"));
        Assert.True(SiteRegion.Exists(Site, "epics.html"));
        Assert.True(SiteRegion.Exists(Site, "epics/story-1-1.html"));
        Assert.DoesNotContain("href=\"sprint.html\"", SiteRegion.Read(Site, "index.html"));
    }

    [Fact]
    public void GenerateAll_UnrecognizedTopLevelFolder_RendersPageAndReportsStructureNotice()
    {
        // Story 4.2 Tasks 3/5: an unknown folder emits one categorized non-fatal structure notice on the
        // diagnostic channel (input for Story 4.8's page) and still renders its doc's page. The home index band
        // for the folder was removed by spec-declutter-home-dashboard (the page stays reachable by direct URL).
        Directory.CreateDirectory(Path.Combine(Source, "design-notes"));
        File.WriteAllText(Path.Combine(Source, "design-notes", "ideas.md"), "# Ideas\n\nBody.\n");

        var events = new SiteGenerator(Options()).GenerateAll();

        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        var notice = Assert.Single(events, e => e.Outcome == GenerationOutcome.Skipped && e.RelativePath == "design-notes/");
        Assert.Contains("unrecognized top-level folder", notice.Message);
        // Informational (not Unsupported): a benign structural notice must not share a diagnostics-page bucket
        // with a genuine per-artifact ingestion failure. [deferred-diagnostic-severity-bucketing]
        Assert.StartsWith("[Informational]", notice.Message);

        // The doc page still renders; the home no longer carries the (removed) unrecognized-folder index band.
        Assert.True(SiteRegion.Exists(Site, "design-notes/ideas.html"));
        var index = SiteRegion.Read(Site, "index.html");
        Assert.DoesNotContain("Design Notes</div>", index);
        Assert.DoesNotContain("href=\"design-notes/ideas.html\"", index);
    }

    [Fact]
    public void GenerateAll_NormalBmadLayout_DoesNotEmitUnrecognizedNoticeForAdrsDocsOrRetros()
    {
        // Pins the path model behind the closed Epic 4 KnownIndexGroups debt: UnrecognizedTopLevelFolders walks
        // SourceRoot only. Separate AdrSourceRoot (docs/adrs) never enters sourceRelatives; retros live under
        // already-well-known implementation-artifacts/. A normal BMad fixture must not emit unrecognized-folder
        // notices — and adrs/docs/retros must stay OUT of the well-known set (a no-op whitelist must fail this pin).
        // [spec-close-known-index-groups-misdiagnosis]
        Assert.False(HtmlTemplater.IsWellKnownTopLevelFolder("adrs"));
        Assert.False(HtmlTemplater.IsWellKnownTopLevelFolder("docs"));
        Assert.False(HtmlTemplater.IsWellKnownTopLevelFolder("retros"));

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        Assert.DoesNotContain(events, e =>
            e.Outcome == GenerationOutcome.Skipped
            && e.Message is not null
            && e.Message.Contains("unrecognized top-level folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateAll_CleanFixture_ProducesAboutAndAllClearDiagnostics()
    {
        // Story 4.8: both pages are written on every full run. This fixture is clean (valid sprint yaml, only
        // well-known folders), so the diagnostics page renders the all-clear state, not an empty table.
        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        Assert.True(SiteRegion.Exists(Site, "about.html"));
        var diag = SiteRegion.Read(Site, "diagnostics.html");
        Assert.Contains("No notices", diag);
        Assert.DoesNotContain("diagnostics-table", diag);
        // AC #2: the effective-config disclosure still renders in the all-clear case, carrying the run's config.
        Assert.Contains("Effective configuration", diag);
        Assert.Contains("<dt>Output directory</dt>", diag);
        Assert.Contains("<dt>Deep-git analytics</dt>", diag);
    }

    [Fact]
    public void GenerateAll_UnusableSprintYaml_DiagnosticsPageListsNoticeExactlyOnce()
    {
        // The same unsupported-sprint fixture the diagnostic-channel test uses: it must surface as exactly ONE
        // row on the diagnostics page (no double-count — each adapter diagnostic is mapped into the events list
        // once), carrying its fine "Unsupported" category word. [Story 4.8 Task 2/7]
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        new SiteGenerator(Options()).GenerateAll();
        var diag = SiteRegion.Read(Site, "diagnostics.html");

        Assert.Contains("diagnostics-table", diag);
        // The doc-subtitle pins the notice count — "1 notice" proves the single mapped diagnostic isn't doubled.
        Assert.Contains("&middot; 1 notice &middot;", diag);
        Assert.Equal(1, Count(diag, "diagnostics-source"));
        Assert.Contains(">Unsupported</span>", diag);
        Assert.Contains("sprint-status.yaml", diag);
    }

    [Fact]
    public void GenerateAll_UnusableSprintYaml_DiagnosticsWireMirrorsThePagesNoticeSet()
    {
        // AC #2 coherence (Story 6.12): the `webview` command's JSON-lines stderr channel and the Story 4.8
        // diagnostics page derive from the SAME DiagnosticNotice.FromEvents(events) projection, so the two
        // surfaces can never disagree. DiagnosticsPageListsNoticeExactlyOnce (above) pins "1 notice" on this exact
        // malformed-sprint fixture for the PAGE; here the same fixture feeds the WIRE — same count, same anchored
        // source path, no double-count.
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        var options = Options();
        var events = new SiteGenerator(options).GenerateAll();
        var notices = DiagnosticNotice.FromEvents(events);

        // Exactly the page's set: one non-fatal, source-anchored sprint-status.yaml skip.
        var notice = Assert.Single(notices);
        Assert.Equal(DiagnosticAnchorRoot.Source, notice.AnchorRoot);
        Assert.EndsWith("sprint-status.yaml", notice.SourcePath, StringComparison.OrdinalIgnoreCase);

        // …and the wire is a faithful mirror of that same set: one anchored, repo-relative, forward-slashed line.
        var line = Assert.Single(WebviewCommand.SerializeDiagnostics(notices, options)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToList());
        Assert.True(line.GetProperty("fileAnchored").GetBoolean());
        Assert.EndsWith("sprint-status.yaml", line.GetProperty("path").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\\', line.GetProperty("path").GetString()!);
    }

    [Fact]
    public void GenerateAll_FooterAboutLink_ResolvesFromRootAndNestedPages()
    {
        // The site-wide footer gains an About link on EVERY page (the deliberate Story 4.8 output change); its
        // relative href must resolve from both a root page and a nested one.
        new SiteGenerator(Options()).GenerateAll();

        // Root page → bare href; depth-1 pages (adrs/, epics/) → "../about.html".
        Assert.Contains("href=\"about.html\"", SiteRegion.Read(Site, "index.html"));
        Assert.Contains("href=\"../about.html\"", SiteRegion.Read(Site, "adrs/index.html"));
        Assert.Contains("href=\"../about.html\"", SiteRegion.Read(Site, "epics/story-1-1.html"));
        // The About page links on to the diagnostics run log (the reachability path's final hop).
        Assert.Contains("href=\"diagnostics.html\"", SiteRegion.Read(Site, "about.html"));
    }

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    [Fact]
    public void IsEpicsRelated_ToleratesNestedLocations()
    {
        // Watch routing classifies via the adapter's shared conventions (Story 4.2 Task 4): the epics file by
        // name anywhere, story artifacts by implementation-artifacts/ ancestor at any depth.
        var gen = new SiteGenerator(Options());

        Assert.True(gen.IsEpicsRelated(Path.Combine(Source, "nested", "epics.md")));
        Assert.True(gen.IsEpicsRelated(Path.Combine(Source, "tracking", "implementation-artifacts", "1-4-x.md")));
        Assert.False(gen.IsEpicsRelated(Path.Combine(Source, "planning-artifacts", "prd.md")));
    }

    [Fact]
    public void GenerateAll_AllDoneEpicWithoutRetrospective_RendersAsInReview()
    {
        // Story 6.2 harmonized the epic-status VISUAL surfaces onto StatusStyles.ForEpicWithRetrospective: an epic
        // whose every story is done but which has NO retrospective reads as "In review" (delivered, retro pending)
        // rather than "Done". Epic 2 in this fixture is exactly that case (Story 2.1 done, no epic-2 retro). This
        // pins the branch the golden fingerprint now exercises — it was previously invisible because the only
        // fixture epic had an in-progress story. [Story 6.2 review]
        new SiteGenerator(Options()).GenerateAll();

        // The epic HEADER badge reads "In review". No story here is in review (Story 2.1 is done), so a
        // review-class status badge on this page can only be the epic's own header badge.
        var epic2 = SiteRegion.Read(Site, "epics/epic-2.html");
        Assert.Contains("<span class=\"status-badge review js-tip\"", epic2);

        // …and the epics-index chip for Epic 2 agrees (the same retro-gated classifier), so the surfaces are
        // consistent rather than one reading "Done" and another "In review".
        var epicsIndex = SiteRegion.Read(Site, "epics.html");
        Assert.Contains("epic-chip review", epicsIndex);
    }

    [Fact]
    public void GenerateAll_ThenRegenerateEpics_KeepsWatchParity()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // A watch-mode epics edit: retitle the story, then run the incremental path the watcher uses.
        File.WriteAllText(
            Path.Combine(Source, "planning-artifacts", "epics.md"),
            EpicsMd.Replace("Foundation Story", "Renamed Story"));
        var ev = gen.RegenerateEpics();

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        Assert.Contains("Renamed Story", SiteRegion.Read(Site, "epics/story-1-1.html"));
        Assert.Contains("Renamed Story", SiteRegion.Read(Site, "epics.html"));
    }
}
