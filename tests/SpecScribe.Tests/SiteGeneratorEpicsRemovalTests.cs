using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 5.3 coverage for the watch-mode paths a full <see cref="SiteGenerator.GenerateAll"/> hides.
/// <c>GenerateAll</c> wipes the output root before rebuilding, so a page whose source vanished simply never comes
/// back; the incremental <see cref="SiteGenerator.RegenerateEpics"/> path never wiped anything, so every writer's
/// "model is null → don't write" guard prevented writing a stale page but never REMOVED one already on disk. These
/// tests drive the generator headlessly (no <see cref="FileWatcherService"/>, no FS-event timing) so the removal
/// contract, the topology escalation, and the single-writer lock are all asserted deterministically.
/// Temp-dir fixture in the style of <see cref="SiteGeneratorDataSourceTests"/>.</summary>
public class SiteGeneratorEpicsRemovalTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-epicsremoval-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string EpicsPath => Path.Combine(Source, "planning-artifacts", "epics.md");
    private string SprintPath => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");

    private const string EpicsMd = """
        # Epics

        ## Requirements

        - FR1: The portal renders.

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).
        """;

    private const string SprintYaml = """
        last_updated: 2026-07-24
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
          1-2-undrafted: backlog
        """;

    public SiteGeneratorEpicsRemovalTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(EpicsPath, EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            "# Story 1.1: Foundation Story\n\nStatus: in-progress\n\n## Story\n\nAs a maintainer, I want it.\n");
        File.WriteAllText(SprintPath, SprintYaml);
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

    private SiteGenerator GeneratedSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    private string SitePath(string relative) =>
        Path.Combine(Site, relative.Replace('/', Path.DirectorySeparatorChar));

    // Every local href on the page must resolve to a real file — the assertion that actually catches a page left
    // linking into an output subtree that was deleted out from under it.
    private void AssertNoBrokenLocalLinks(string pageFullPath)
    {
        var html = File.ReadAllText(pageFullPath);
        var pageDir = Path.GetDirectoryName(pageFullPath)!;
        foreach (Match m in Regex.Matches(html, "href=\"(?<href>[^\"]+)\""))
        {
            var href = m.Groups["href"].Value;
            // Anything with a URI scheme (http:, data:, mailto:, vscode:, command:) or a bare fragment is not a
            // local file reference — only same-site relative hrefs can dangle.
            if (href.StartsWith("#", StringComparison.Ordinal)
                || Regex.IsMatch(href, @"^[a-zA-Z][a-zA-Z0-9+.\-]*:"))
            {
                continue;
            }

            var target = href.Split('#')[0].Split('?')[0];
            if (target.Length == 0) continue;

            var resolved = Path.GetFullPath(Path.Combine(pageDir, target.Replace('/', Path.DirectorySeparatorChar)));
            // Surrounding markup in the message: a dangling href is only actionable if you can see which widget
            // emitted it.
            var from = Math.Max(0, m.Index - 220);
            var context = html.Substring(from, Math.Min(440, html.Length - from));
            Assert.True(File.Exists(resolved) || Directory.Exists(resolved),
                $"{Path.GetFileName(pageFullPath)} links to '{href}', which does not exist on disk.\nContext:\n…{context}…");
        }
    }

    [Fact]
    public void RegenerateEpics_WhenEpicsFileDeleted_RemovesTheWholeEpicsOutputFamily()
    {
        var gen = GeneratedSite();

        // Baseline: the whole epics-derived family exists.
        Assert.True(File.Exists(SitePath("epics.html")));
        Assert.True(File.Exists(SitePath("requirements.html")));
        Assert.True(File.Exists(SitePath("traceability.html")));
        Assert.True(File.Exists(SitePath("cadence.html")));
        Assert.True(Directory.Exists(SitePath("epics")));
        Assert.True(Directory.Exists(SitePath("requirements")));
        Assert.True(File.Exists(SitePath("epics/epic-1.html")));
        Assert.True(File.Exists(SitePath("epics/story-1-1.html")));

        // The topology change under test: epics.md itself disappears while watch mode is live.
        File.Delete(EpicsPath);
        var ev = gen.RegenerateEpics();

        // AC #3: a real destructive change to the output tree reports as such, not as the old Skipped no-op.
        Assert.Equal(GenerationOutcome.Removed, ev.Outcome);
        Assert.Contains("removed", ev.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.False(File.Exists(SitePath("epics.html")), "stale epics.html must be deleted");
        Assert.False(File.Exists(SitePath("requirements.html")), "stale requirements.html must be deleted");
        Assert.False(File.Exists(SitePath("traceability.html")), "stale traceability.html must be deleted");
        Assert.False(File.Exists(SitePath("cadence.html")), "stale cadence.html must be deleted");
        Assert.False(Directory.Exists(SitePath("epics")), "stale epics/ subtree must be deleted");
        Assert.False(Directory.Exists(SitePath("requirements")), "stale requirements/ subtree must be deleted");
    }

    [Fact]
    public void RegenerateEpics_WhenEpicsFileDeleted_LeavesNoNavEntryOrLinkPointingAtARemovedPage()
    {
        var gen = GeneratedSite();
        Assert.Contains("href=\"epics.html\"", File.ReadAllText(SitePath("index.html")));

        File.Delete(EpicsPath);
        gen.RegenerateEpics();

        var index = File.ReadAllText(SitePath("index.html"));
        Assert.DoesNotContain("href=\"epics.html\"", index);
        Assert.DoesNotContain("href=\"requirements.html\"", index);
        Assert.DoesNotContain("href=\"traceability.html\"", index);
        Assert.DoesNotContain("href=\"cadence.html\"", index);

        // The whole point of AC #3: nothing still on disk points into the subtree that was just removed.
        AssertNoBrokenLocalLinks(SitePath("index.html"));
    }

    [Fact]
    public void RegenerateEpics_WhenEpicsFileDeleted_SprintPageDegradesInPlaceRatherThanDangling()
    {
        // Open Question #2's chosen default: sprint-status.yaml is still present, so its page stays — but it must be
        // RE-RENDERED, because the version on disk was written with live links into the epics pages now deleted.
        var gen = GeneratedSite();
        var before = File.ReadAllText(SitePath("sprint.html"));
        Assert.Contains("href=\"epics/epic-1.html\"", before);

        File.Delete(EpicsPath);
        gen.RegenerateEpics();

        Assert.True(File.Exists(SitePath("sprint.html")), "sprint.html survives — its own source is untouched");
        var after = File.ReadAllText(SitePath("sprint.html"));
        Assert.DoesNotContain("href=\"epics/epic-1.html\"", after);
        Assert.DoesNotContain("href=\"epics/story-1-1.html\"", after);
        AssertNoBrokenLocalLinks(SitePath("sprint.html"));
    }

    [Fact]
    public void RegenerateEpics_WithNoEpicsFileEverPresent_StillReportsSkippedNotRemoved()
    {
        // Behaviour guard: a project that simply has no epics.md must keep reporting the long-standing Skipped
        // no-op. Only a pass that actually tore down a stale family escalates to Removed.
        File.Delete(EpicsPath);
        var gen = GeneratedSite();

        var ev = gen.RegenerateEpics();

        Assert.Equal(GenerationOutcome.Skipped, ev.Outcome);
        Assert.Contains("not found", ev.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegenerateEpics_AfterRemoval_IsIdempotent()
    {
        // A burst can fire the same route twice; the second pass has nothing left to delete and must not throw or
        // claim a second removal.
        var gen = GeneratedSite();
        File.Delete(EpicsPath);

        Assert.Equal(GenerationOutcome.Removed, gen.RegenerateEpics().Outcome);
        Assert.Equal(GenerationOutcome.Skipped, gen.RegenerateEpics().Outcome);
    }

    [Fact]
    public void RegenerateEpics_WhenEpicsFileRestored_RebuildsTheFamily()
    {
        // The removal must not be a one-way door: a rename-away-and-back (or a git checkout) has to self-heal
        // without restarting watch mode.
        var gen = GeneratedSite();
        File.Delete(EpicsPath);
        gen.RegenerateEpics();
        Assert.False(File.Exists(SitePath("epics.html")));

        File.WriteAllText(EpicsPath, EpicsMd);
        var ev = gen.RegenerateEpics();

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        Assert.True(File.Exists(SitePath("epics.html")));
        Assert.True(File.Exists(SitePath("epics/epic-1.html")));
        Assert.True(File.Exists(SitePath("requirements.html")));
        Assert.Contains("href=\"epics.html\"", File.ReadAllText(SitePath("index.html")));
    }

    [Fact]
    public void RegenerateTopology_RebuildsEverythingAndReportsOneDirectoryChangeEvent()
    {
        // AC #5's escalation, asserted at the generator level: a whole directory of documents moves, and the full
        // rebuild is what makes the output coherent — pages exist at the new location with no orphan at the old.
        // Uses a plain notes folder rather than implementation-artifacts/, because a story artifact CONSUMED by
        // epics.md renders as epics/story-N-M.html instead of a standalone doc page, which would not exercise the
        // "page moves with its folder" behaviour under test.
        var notes = Path.Combine(Source, "notes");
        Directory.CreateDirectory(notes);
        File.WriteAllText(Path.Combine(notes, "guide.md"), "# Guide\n\nA note that lives in a folder.\n");

        var gen = GeneratedSite();
        Assert.True(File.Exists(SitePath("notes/guide.html")), "baseline page exists under the original folder name");

        Directory.Move(notes, Path.Combine(Source, "handbook"));
        var ev = gen.RegenerateTopology();

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        Assert.Equal("<directory change>", ev.RelativePath);
        Assert.Equal("full rebuild", ev.Message);

        Assert.True(File.Exists(SitePath("handbook/guide.html")), "page exists at the new location");
        Assert.False(File.Exists(SitePath("notes/guide.html")), "no orphan page survives at the old location");
    }

    [Fact]
    public void ConcurrentRegenerations_SerializeOnTheWriterLock_AndConvergeToCoherentOutput()
    {
        // AC #1/#6: several debounce timers can fire at once for different files. Every write-producing route takes
        // the same _gate, so they serialize — the assertion is on the CONVERGED state (no torn or empty HTML, same
        // file set a from-scratch ground-truth pass produces), never on which order actually won.

        // The GenerateOne leg below needs a doc the WATCH DISPATCH would actually hand to GenerateOne. A plain
        // notes/ document is that; a story artifact is NOT, for the same reason the directory-rename test above
        // avoids one. FileWatcherService routes on IsDataSource → IsAdr → IsEpicsRelated → GenerateOne, and
        // IsEpicsRelated claims EVERYTHING under implementation-artifacts/ (BmadArtifactAdapter
        // .IsUnderImplementationArtifacts), so a story artifact always reaches RegenerateEpics and can never
        // reach GenerateOne in production. Driving GenerateOne with one anyway wrote a standalone
        // implementation-artifacts/1-1-foundation.html that no full rebuild ever produces — GenerateAll excludes
        // consumed artifacts from its page pass and renders them as epics/story-N-M.html — so this assertion
        // failed ~23% of the time, whenever GenerateOne won the tail of the race against a RegenerateEpics that
        // would otherwise have pruned the orphan. Keep this a non-artifact doc.
        var notes = Path.Combine(Source, "notes");
        Directory.CreateDirectory(notes);
        var genericDoc = Path.Combine(notes, "concurrent-doc.md");
        File.WriteAllText(genericDoc, "# Concurrent Doc\n\nA plain document the generic single-file route owns.\n");

        var gen = GeneratedSite();

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        // Degree capped deliberately. Contention is what this test needs, but each iteration can run a FULL
        // rebuild, and an uncapped Parallel.For saturates every core — which starves the suite's git-fixture
        // tests (they spawn real `git` processes under their own timeouts) and turns them flaky for reasons that
        // have nothing to do with what is under test here. Four concurrent writers still interleave every route
        // pair; the assertion is on convergence, not on how many threads raced.
        Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            try
            {
                switch (i % 4)
                {
                    case 0: gen.RegenerateEpics(); break;
                    case 1: gen.RegenerateAdrs(); break;
                    case 2: gen.GenerateOne(genericDoc); break;
                    default: gen.RegenerateFromDataSource(SprintPath); break;
                }
            }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        Assert.Empty(exceptions);

        // No file was left mid-write: every generated page is non-empty and closes its document.
        foreach (var page in Directory.GetFiles(Site, "*.html", SearchOption.AllDirectories))
        {
            var html = File.ReadAllText(page);
            Assert.False(string.IsNullOrWhiteSpace(html), $"{page} is empty — a torn write");
            Assert.Contains("</html>", html);
        }

        // Ground truth: a fresh from-scratch generation into a clean directory produces the same page set the
        // concurrent run converged to.
        var truthRoot = Path.Combine(_root, "truth");
        var truth = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: truthRoot, projectName: "SpecScribe", includeReadme: false));
        Assert.DoesNotContain(truth.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var converged = RelativeFileSet(Site);
        var expected = RelativeFileSet(truthRoot);
        // Report the symmetric difference by name. xUnit's set-differs message truncates both sides after a few
        // entries, which on a ~40-file portal tells you nothing about WHICH file diverged. [Story 20.5]
        var onlyConverged = converged.Except(expected, StringComparer.OrdinalIgnoreCase).ToList();
        var onlyTruth = expected.Except(converged, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(onlyConverged.Count == 0 && onlyTruth.Count == 0,
            $"Converged output diverged from a from-scratch run.\n"
            + $"  only in the concurrent run: {string.Join(", ", onlyConverged)}\n"
            + $"  only in the from-scratch run: {string.Join(", ", onlyTruth)}");
    }

    private static SortedSet<string> RelativeFileSet(string root) =>
        new(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);
}
