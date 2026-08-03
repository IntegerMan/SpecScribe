using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.5's per-commit detail pages. The load-bearing AC #2 pin: with
/// <c>DeepGitAnalytics == false</c> no <c>commit/</c> directory is produced, no error is reported, and the
/// per-day pages render plain <c>&lt;code&gt;</c> hashes — the gate lives at the option/render boundary, never a
/// wall-clock timing test. The enabled path (real git history) exercises page emission, the day-page + hub hash
/// links lighting up, reference linkification, and determinism; it no-ops gracefully when git is unavailable on
/// the host. Follows the temp-dir fixture style of <see cref="SiteGeneratorGitInsightsTests"/>.</summary>
public class SiteGeneratorCommitDetailsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-commitdetail-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string CommitDir => Path.Combine(Site, "commit");
    private string CommitsDayDir => Path.Combine(Site, "commits");
    private string HubRoute => "git-insights.html";

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    public SiteGeneratorCommitDetailsTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(bool deepGit, string? output = null) => ForgeOptions.Resolve(
        source: Source, output: output ?? Site, projectName: "SpecScribe", includeReadme: false,
        deepGitAnalytics: deepGit);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GenerateAll_FlagOff_EmitsNoCommitDirAndPlainDayHashes()
    {
        // AC #2's performance guarantee is this gate: flag off -> the deep path (and per-commit pages behind it)
        // never runs, so no commit/ dir, no error, and the day-page hashes stay plain <code> (no commit/ links).
        var events = new SiteGenerator(Options(deepGit: false)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(SiteRegion.HasRoutesUnder(Site, "commit/"), "commit/ must not exist when --deep-git is off");

        // This fixture is not a git repo, so there are no date pages at all now (Story 7.3's date pages are
        // git-derived — no mtime fallback). If any did render, none links into commit/ when --deep-git is off
        // (the resolver has no pages) and any commit hashes stay plain <code>.
        if (SiteRegion.HasRoutesUnder(Site, "commits/"))
        {
            foreach (var page in SiteRegion.RoutesUnder(Site, "commits/"))
            {
                var day = SiteRegion.Read(Site, page);
                Assert.DoesNotContain("../commit/", day);
                if (day.Contains("commit-day-list"))
                {
                    Assert.Contains("<code class=\"commit-hash\">", day);
                }
            }
        }
    }

    [Fact]
    public void GenerateAll_FlagOnWithoutGitHistory_DegradesToNoCommitPagesWithoutError()
    {
        // The temp fixture is not a git repository, so the deep pass yields null. NFR-2: no commit/ dir, no error,
        // and the rest of the site still generates.
        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(SiteRegion.HasRoutesUnder(Site, "commit/"));
        Assert.True(SiteRegion.Exists(Site, "epics.html"), "baseline generation must still succeed");
    }

    [SkippableFact]
    public void GenerateAll_FlagOnWithHistory_EmitsBoundedCommitPagesAndSubjectAndAuthor()
    {
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise gated commit-page generation (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(SiteRegion.HasRoutesUnder(Site, "commit/"), "commit/ must be generated when --deep-git has data");

        var pages = SiteRegion.RoutesUnder(Site, "commit/");
        Assert.NotEmpty(pages);
        Assert.True(pages.Count <= 300, "per-commit pages are bounded by the -n 300 deep window");

        var allPages = string.Concat(pages.Select(r => SiteRegion.Read(Site, r)));
        Assert.Contains("Implement Story 1.1 foundation", allPages);   // a known commit subject
        Assert.Contains("by Detail Tester", allPages);                  // author shown as attribution, not a rank
    }

    [SkippableFact]
    public void GenerateAll_CommitDetailPager_IsChronological_PrevIsEarlierCommit_NextIsLater()
    {
        // Two commits, oldest first: "Implement Story 1.1 foundation" then "Second commit". [Prev/next navigation]
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise the commit pager (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        // The subject "Story 1.1" gets linkified inside <h1> (see the reference-linkification test below), so
        // match on the h1's inner text loosely rather than the exact escaped subject string.
        var pages = SiteRegion.RoutesUnder(Site, "commit/");
        var olderPage = pages.Single(p => Regex.Match(SiteRegion.Read(Site, p), "<h1>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Contains("foundation"));
        var olderHtml = SiteRegion.Read(Site, olderPage);
        var newerPage = pages.Single(p => Regex.Match(SiteRegion.Read(Site, p), "<h1>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Contains("Second commit"));
        var newerHtml = SiteRegion.Read(Site, newerPage);

        // The oldest commit's page has no earlier sibling (Prev disabled) and Next points at the newer commit.
        Assert.Contains("entity-pager-prev is-disabled", olderHtml);
        var olderNext = Regex.Match(olderHtml, "entity-pager-next\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.EndsWith(Path.GetFileName(newerPage), olderNext);

        // The newest commit's page has no later sibling (Next disabled) and Prev points back at the older commit.
        Assert.Contains("entity-pager-next is-disabled", newerHtml);
        var newerPrev = Regex.Match(newerHtml, "entity-pager-prev\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.EndsWith(Path.GetFileName(olderPage), newerPrev);
    }

    [SkippableFact]
    public void GenerateAll_CommitDetailPage_LocalContextBand_ListsSiblingCommitsWithCurrentMarkedActive()
    {
        // [Story 10.10 review — patch] no direct test previously exercised the commit-page NavLocalContext
        // builder; only the generic seam mechanics were covered.
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise the commit local-context band (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        var pages = SiteRegion.RoutesUnder(Site, "commit/");
        var olderPage = pages.Single(p => Regex.Match(SiteRegion.Read(Site, p), "<h1>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Contains("foundation"));
        var olderHtml = SiteRegion.Read(Site, olderPage);

        Assert.Contains("site-nav-local-context", olderHtml);
        Assert.Contains("Recent commits", olderHtml);
        // The current commit renders as an inactive-safe <span>, never a self-link, while the sibling commit
        // is a real link — same "current page never self-links" rule the pager and breadcrumb already follow.
        Assert.Contains("local-context-pill active", olderHtml);
        Assert.Matches(new Regex("<a[^>]*class=\"local-context-pill\"[^>]*>[^<]*Second commit"), olderHtml);
    }

    [SkippableFact]
    public void GenerateAll_FlagOnWithHistory_LightsUpDayPageHashLinks()
    {
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise gated hash-link wiring (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        // The per-day page's hash is now a link into commit/ (from commits/ depth → ../commit/…).
        var dayPages = SiteRegion.RoutesUnder(Site, "commits/");
        Assert.NotEmpty(dayPages);
        var anyDayLinks = dayPages.Any(p => SiteRegion.Read(Site, p).Contains("class=\"commit-hash-link\" href=\"../commit/"));
        Assert.True(anyDayLinks, "a day page's hash should link into commit/ when a per-commit page exists");

        // The Git Insights hub no longer carries a commit-hash link (Story 7.11 rewrite removed the
        // per-file "latest {hash}" line along with the master-detail panel it lived in) — it still exists,
        // just gated on the same deep-git signal.
        Assert.True(SiteRegion.Exists(Site, HubRoute));
    }

    [SkippableFact]
    public void GenerateAll_FlagOnWithHistory_LinkifiesReferencesInCommitMessages()
    {
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise reference linkification (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        // The commit subject "Implement Story 1.1 foundation" becomes a guarded story link via ApplyReferenceLinks.
        var allPages = string.Concat(SiteRegion.RoutesUnder(Site, "commit/").Select(r => SiteRegion.Read(Site, r)));
        Assert.Contains("class=\"story-ref\" href=\"../epics/story-1-1.html\"", allPages);
    }

    [SkippableFact]
    public void GenerateAll_TwoRunsProduceIdenticalCommitMarkup()
    {
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise determinism (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var site2 = Path.Combine(_root, "site2");
        var events1 = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        var events2 = new SiteGenerator(Options(deepGit: true, output: site2)).GenerateAll();
        AssertNoErrors(events1);
        AssertNoErrors(events2);

        // Strip the human-friendly footer timestamp (24h + zone, Story 10.4), then every commit page must be
        // byte-identical run to run. Only the trailing zone-label token is matched generically (word chars plus
        // +/-/: rather than PortalDates.LocalZoneLabel's current signed "UTC±HH:MM" shape) so a zone-label format
        // change doesn't degrade this into an un-normalized compare; the date/time portion still assumes
        // PortalDates.Day's current wording and is not shape-proofed by this change.
        static string Stable(string html) =>
            Regex.Replace(html, @"on \w+ \d{1,2}, \d{4} at \d{1,2}:\d{2} [\w+\-:]+", "on <t>");

        var pages1 = SiteRegion.RoutesUnder(Site, "commit/").OrderBy(p => p, StringComparer.Ordinal).ToList();
        var pages2 = SiteRegion.RoutesUnder(site2, "commit/").OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(pages1, pages2);
        for (var i = 0; i < pages1.Count; i++)
        {
            Assert.Equal(Stable(SiteRegion.Read(Site, pages1[i])), Stable(SiteRegion.Read(site2, pages2[i])));
        }
    }

    [SkippableFact]
    public void GenerateAll_LastCommitPolicy_LinkedHeatmapDaySetMatchesGeneratedDayPageSet()
    {
        // Story 5.5 AC #2's central guarantee, exercised through the REAL production call sites (SiteGenerator's
        // shared _today threaded into GenerateDatePagesInternal and, separately, into the git-insights heatmap via
        // GitInsightsTemplater/Charts.CommitHeatmap) — not a synthetic double-call on the pure resolver, which is
        // trivially true of any pure function and proves nothing about the actual wiring. Under a non-default
        // policy, the day set the heatmap links into commits/ must be EXACTLY the day set commits/ actually
        // contains: one shared resolved "today", never two independent clock reads. [Review][Patch — closes the
        // AC #2 integration-test gap the code review found]
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise the LastCommit policy end-to-end (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var options = ForgeOptions.Resolve(source: Source, output: Site, projectName: "SpecScribe", includeReadme: false,
            deepGitAnalytics: true, dateCutoff: new DateCutoff(DatePolicy.LastCommit, null));
        var events = new SiteGenerator(options).GenerateAll();
        AssertNoErrors(events);

        var hub = SiteRegion.Read(Site, HubRoute);
        var linkedDays = LinkedDaysOn(hub);
        var generatedDays = GeneratedDayPages();

        Assert.NotEmpty(linkedDays);
        Assert.Equal(generatedDays, linkedDays);
    }

    [SkippableFact]
    public void GenerateAll_AsOfPolicy_LinkedHeatmapDaySetMatchesGeneratedDayPageSet()
    {
        // Story 5.7 AC #1's central guarantee, exercised through the same REAL production wiring as the LastCommit
        // sibling above rather than through the pure resolver: the fixed date must become the run's ONE resolved
        // today for every consumer, so the day set the heatmap links into commits/ is EXACTLY the day set commits/
        // actually contains. The fixture's commits are authored now, so pinning to the machine's own day includes
        // them — this is the "the pin agrees with reality" half; the counter-test below is the other half.
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise the --as-of policy end-to-end (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var options = ForgeOptions.Resolve(source: Source, output: Site, projectName: "SpecScribe", includeReadme: false,
            deepGitAnalytics: true, dateCutoff: new DateCutoff(DatePolicy.AsOf, DateOnly.FromDateTime(DateTime.Now)));
        var events = new SiteGenerator(options).GenerateAll();
        AssertNoErrors(events);

        var linkedDays = LinkedDaysOn(SiteRegion.Read(Site, HubRoute));
        var generatedDays = GeneratedDayPages();

        Assert.NotEmpty(linkedDays);
        Assert.Equal(generatedDays, linkedDays);
    }

    [SkippableFact]
    public void GenerateAll_AsOfBeforeTheFirstCommit_EmitsNoDayPagesAndClaimsNoCommitsInTheHeatmapText()
    {
        // Story 5.7 D2 / AC #1a — the counter-test for the guard above. An out-of-range pin is the CORRECT answer
        // for a historical snapshot, so it is accepted verbatim: no crash (the naive series.Min() on an empty
        // filtered set would throw InvalidOperationException), no rejection, no warning. And the heatmap's text
        // twin must describe only the rendered window: with nothing rendered it must not name the fixture's real
        // commits, which the pre-5.7 whole-series aria-label and headline both would have.
        Skip.IfNot(GitAvailable(), "git CLI unavailable on this host — install git to exercise the --as-of policy end-to-end (skipped, not failed)");
        Assert.True(TryCreateGitHistory(), "git is available but the test fixture's git setup failed unexpectedly");

        var options = ForgeOptions.Resolve(source: Source, output: Site, projectName: "SpecScribe", includeReadme: false,
            deepGitAnalytics: true, dateCutoff: new DateCutoff(DatePolicy.AsOf, new DateOnly(2000, 1, 1)));
        var events = new SiteGenerator(options).GenerateAll();
        AssertNoErrors(events);

        Assert.Empty(GeneratedDayPages());

        var hub = SiteRegion.Read(Site, HubRoute);
        Assert.Empty(LinkedDaysOn(hub));
        // The designed empty state (UX-DR22), naming the cutoff so the state explains itself.
        Assert.Contains("No commits on or before", hub, StringComparison.Ordinal);
        // The accessible name and the visible headline restate the same figures, so BOTH must be gone — one of them
        // surviving is exactly the text-twin disagreement ADR 0013 forbids.
        Assert.DoesNotContain("aria-label=\"Commit activity:", hub, StringComparison.Ordinal);
        Assert.DoesNotContain("heatmap-headline", hub, StringComparison.Ordinal);
    }

    /// <summary>The <c>commits/{date}.html</c> days a rendered page links to — the heatmap's linked-cell set.</summary>
    private static HashSet<string> LinkedDaysOn(string html) =>
        Regex.Matches(html, "href=\"commits/(\\d{4}-\\d{2}-\\d{2})\\.html\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

    /// <summary>The <c>commits/{date}.html</c> pages actually written. Tolerates the directory being absent, which
    /// is itself a legitimate outcome under a cutoff that precedes every commit.</summary>
    private HashSet<string> GeneratedDayPages() =>
        SiteRegion.HasRoutesUnder(Site, "commits/")
            ? SiteRegion.RoutesUnder(Site, "commits/").Select(p => Path.GetFileNameWithoutExtension(p)!).ToHashSet()
            : new HashSet<string>();

    /// <summary>Probes for a usable git CLI on PATH, independent of fixture setup — callers use this to decide
    /// Skip (environment gap) vs. Assert.True/fail (a real regression) on <see cref="TryCreateGitHistory"/>, so a
    /// broken `git init`/`git commit` on a host that DOES have git surfaces as a genuine test failure rather than
    /// being silently swallowed into the same Skip path.</summary>
    private bool GitAvailable() => RunGit("--version");

    /// <summary>Initializes a real git repo in the fixture root with two commits by a known author — the first
    /// referencing Story 1.1 (for the linkification check). Only call after confirming <see cref="GitAvailable"/>;
    /// a false return here means the fixture setup itself failed, not that git is missing. Identity and signing
    /// are forced via -c overrides so a host's global config can't break the fixture.</summary>
    private bool TryCreateGitHistory()
    {
        if (!RunGit("init")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\n");
        if (!RunGit("add .")) return false;
        if (!Commit("Implement Story 1.1 foundation")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\ntwo\n");
        return RunGit("add .") && Commit("Second commit");
    }

    private bool Commit(string message) => RunGit(
        $"-c user.name=\"Detail Tester\" -c user.email=detail@example.com -c commit.gpgsign=false commit -m \"{message}\"");

    private bool RunGit(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
