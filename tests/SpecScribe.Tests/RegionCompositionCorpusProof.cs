using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 23.4 AC #3's REAL-CORPUS byte-equality proof — the one
/// <see cref="RegionCompositionParityTests"/> structurally cannot give.
/// <para>That fixture cites no real repo files, so it emits no <c>code/</c> page and no <c>commit/</c> page: it
/// covers neither <c>CodeFileTemplater</c>'s 254 pages nor <c>CommitDetailTemplater</c>'s 300 — together 40 % of
/// the site. This test runs a full <c>--deep-git --spa</c> generate against THIS repository's own artifacts
/// (~1,408 IR pages) and compares every composed region to its sliced oracle.</para>
/// <para><b>Opt-in by design.</b> A full deep-git generate takes ~65 s and shells out to <c>git log --numstat</c>,
/// so it is gated on <c>SPECSCRIBE_CORPUS_PROOF=1</c> rather than run on every suite pass. It is a gate to run
/// deliberately — before deleting the slice, and again whenever a templater's body boundary moves — not a unit
/// test. Run it with:</para>
/// <code>SPECSCRIBE_CORPUS_PROOF=1 dotnet test --filter FullyQualifiedName~RegionCompositionCorpusProof</code>
/// <para>⚠️ <b>The deep-git surfaces must actually be present or the proof is vacuous.</b>
/// <c>GitMetrics</c> has a hard-coded 3,000 ms budget that <c>git log --numstat</c> has been measured to exceed
/// (6,496 ms cold), and it loses SILENTLY at <c>errors=0</c> — taking <c>git-insights.html</c>,
/// <c>deep-analytics.html</c>, <c>impact-map.html</c> and the whole <c>commit/</c> family with it. A run that
/// quietly produced 1,100 pages instead of 1,408 would report "0 deltas" and mean nothing, so the page count and
/// the three named surfaces are asserted before the comparison is trusted.</para></summary>
public class RegionCompositionCorpusProof : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("specscribe-corpusproof-").FullName;

    /// <summary>The repo root this test suite is running inside — walked up from the test assembly rather than
    /// assumed, so the proof works from any working directory.</summary>
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

    [Fact]
    public void RealCorpus_EveryComposedRegion_MatchesItsSlicedOracle()
    {
        if (Environment.GetEnvironmentVariable("SPECSCRIBE_CORPUS_PROOF") != "1")
        {
            // Deliberately a pass-with-no-assertion rather than Skip: this file exists to be run on purpose, and a
            // permanently-skipped test reads as broken coverage in every report that counts skips.
            return;
        }

        var repo = RepoRoot();
        var options = ForgeOptions.Resolve(
            source: Path.Combine(repo, "_bmad-output"),
            adrs: Path.Combine(repo, "docs", "adrs"),
            output: _output,
            projectName: "SpecScribe",
            includeReadme: true,
            deepGitAnalytics: true,
            emitSpa: true);

        var gen = new SiteGenerator(options);
        gen.GenerateAll();

        // AC #8's hazard guard: prove the deep-git surfaces are really here before trusting any delta count.
        foreach (var required in new[] { "git-insights.html", "deep-analytics.html", "impact-map.html" })
        {
            Assert.True(
                File.Exists(Path.Combine(_output, required)),
                $"{required} is absent — deep-git produced nothing (the 3,000 ms GitMetrics budget lost silently). "
                + "The proof below would be vacuous; raise the budget for this run rather than trusting the result.");
        }
        var commitPages = Directory.Exists(Path.Combine(_output, "commit"))
            ? Directory.GetFiles(Path.Combine(_output, "commit"), "*.html").Length
            : 0;
        Assert.True(commitPages > 200, $"Only {commitPages} commit/ pages — expected ~300. Deep-git ran short.");

        var deltas = gen.RegionCompositionDeltas();

        // deep-analytics.html is the ONE expected delta and it is a FIX, not a regression: its `:target` lightbox
        // sits after </main>, so the slice has been dropping it from the IR all along (its "Expand" link resolves
        // to nothing in the SPA/webview today). Any OTHER delta is a migration defect and must fail here.
        var unexpected = deltas
            .Where(d => !string.Equals(d.Path, "deep-analytics.html", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(unexpected.Count == 0, Report(unexpected, commitPages));

        // And pin the fix positively, rather than merely tolerating it in the filter above. Without this the test
        // would still pass if the lightbox silently stopped being recovered — "no unexpected deltas" is satisfied
        // by zero deltas too, which would mean the composed region had started truncating at </main> like the
        // slice does. The composed region must be STRICTLY LARGER here, by the lightbox it restores.
        var lightbox = deltas.SingleOrDefault(
            d => string.Equals(d.Path, "deep-analytics.html", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            lightbox.Path is not null,
            "deep-analytics.html is no longer a delta — the composed region has stopped recovering the `:target` "
            + "lightbox that sits after </main>, i.e. it is now truncating like the slice it replaces.");
        Assert.True(
            lightbox.Composed.Length > lightbox.Sliced.Length,
            $"deep-analytics.html composed={lightbox.Composed.Length}B is not larger than sliced="
            + $"{lightbox.Sliced.Length}B — expected the recovered lightbox to ADD content.");
        // Assert on the lightbox TARGET ELEMENT, not the bare id. Both regions contain the string "coupling-zoom"
        // because the "Expand" link (`href="#coupling-zoom"`) lives inside <main> and is therefore in the slice too.
        // That is precisely the inherited defect: the LINK ships, its TARGET does not, so the link resolves to
        // nothing in the SPA/webview today. The `id="coupling-zoom"` element is the half that was missing.
        const string target = "id=\"coupling-zoom\"";
        Assert.Contains(target, lightbox.Composed);
        Assert.DoesNotContain(target, lightbox.Sliced);
        Assert.Contains("href=\"#coupling-zoom\"", lightbox.Sliced);
    }

    private static string Report(IReadOnlyList<SiteGenerator.RegionParityDelta> deltas, int commitPages)
    {
        var lines = deltas.Take(10).Select(d =>
        {
            var at = d.FirstDifferenceAt;
            return $"  {d.Path}: composed={d.Composed.Length}B sliced={d.Sliced.Length}B firstDiff@{at}\n"
                + $"    composed: …{Excerpt(d.Composed, at)}…\n"
                + $"    sliced:   …{Excerpt(d.Sliced, at)}…";
        });
        return $"{deltas.Count} unexpected region delta(s) over the real corpus ({commitPages} commit pages):\n"
            + string.Join("\n", lines)
            + (deltas.Count > 10 ? $"\n  …and {deltas.Count - 10} more" : string.Empty);
    }

    private static string Excerpt(string s, int at)
    {
        if (s.Length == 0) return "(empty)";
        var start = Math.Max(0, at - 60);
        var len = Math.Min(160, s.Length - start);
        return s.Substring(start, len).Replace("\n", "\\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_output, recursive: true);
        }
        catch (IOException)
        {
            // A reader holding a generated file open must not fail the run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
