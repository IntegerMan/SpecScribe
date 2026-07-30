using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 23.4 AC #3's byte-equality gate: the content region COMPOSED from a page's own
/// <see cref="PageView"/> must equal, byte for byte, the region SLICED out of that page's rendered document by
/// <c>SpaDelivery.ExtractContentRegion</c>.
/// <para><b>Why this test is the whole story's hinge.</b> For ~1,217 of the site's 1,408 pages the IR is produced
/// by the very code Story 23.4 retires: the page is rendered, reference-linkified as a WHOLE DOCUMENT, captured at
/// the write seam, and the region is then cut back out of it. Deleting the page writer without first standing up a
/// composed-region producer takes the IR dark for 82 % of the site, so the story's own Dev Notes forbid deleting
/// anything until this gate is green. It is also the only gate that can see the failure class it exists for: a
/// region that silently loses its reference links, its <c>&lt;abbr&gt;</c> expansions or its own doc-header renders
/// a perfectly valid static page and passes every other harness in the suite.</para>
/// <para>⚠️ <b>Fixture-green is necessary, not sufficient.</b> This fixture cites no real repo files, so it emits
/// no <c>code/</c> page and no <c>commit/</c> page — it covers neither <c>CodeFileTemplater</c>'s 254 pages nor
/// <c>CommitDetailTemplater</c>'s 300. The real corpus proof is a <c>--deep-git --spa</c> generate run through
/// <see cref="SiteGenerator.RegionCompositionDeltas"/>; this test keeps the guarantee alive in the suite.</para>
/// Follows the temp-dir fixture style of <see cref="SiteGeneratorAdapterTests"/>.</summary>
public class RegionCompositionParityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-regionparity-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts
        FR2: The portal links references

        ### NonFunctional Requirements

        NFR1: Generation degrades gracefully

        ### FR Coverage Map

        FR1: Epic 1 - rendering
        FR2: Epic 1 - linking
        NFR1: Epic 1 - degradation

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        **Acceptance Criteria:**

        1.
        **Given** a fixture
        **When** the site generates
        **Then** FR1 and Story 1.1 are linkified and ADR mentions expand

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).
        """;

    private const string SprintYamlText = """
        development_status:
          1-1-foundation-story: done
          1-2-undrafted-story: backlog
        """;

    private const string StoryArtifact = """
        # Story 1.1: Foundation Story

        Status: done

        ## Story

        As a maintainer, I want the foundation, so that FR1 is satisfied per ADR 0001 and Story 1.2 follows.

        ## Dev Notes

        This page mentions FR2 and Epic 1 so the reference linkifiers have real work to do.
        """;

    private const string AdrText = """
        # ADR 0001: Use a static portal

        Status: Accepted

        ## Context

        The portal renders FR1 and FR2 for Story 1.1.

        ## Decision

        Render statically.
        """;

    private void Seed()
    {
        var planning = Path.Combine(Source, "planning-artifacts");
        var impl = Path.Combine(Source, "implementation-artifacts");
        Directory.CreateDirectory(planning);
        Directory.CreateDirectory(impl);
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(planning, "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(impl, "sprint-status.yaml"), SprintYamlText);
        File.WriteAllText(Path.Combine(impl, "1-1-foundation-story.md"), StoryArtifact);
        File.WriteAllText(Path.Combine(Adrs, "0001-use-a-static-portal.md"), AdrText);
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, emitSpa: true);

    /// <summary>The gate. Every captured page's composed region must equal its sliced region byte for byte.
    /// <para>A page captured as HTML with NO view model reports as a delta with an empty composed region — that is
    /// deliberate. A page still on the un-migrated <c>WriteOutput(path, ApplyReferenceLinks(...))</c> path is
    /// exactly the silent gap this proof exists to catch, so it must fail here rather than being skipped.</para></summary>
    [Fact]
    public void EveryCapturedPage_ComposedRegion_IsByteIdenticalToTheSlicedRegion()
    {
        Seed();
        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        var deltas = gen.RegionCompositionDeltas();

        Assert.True(deltas.Count == 0, Describe(deltas));
    }

    /// <summary>Renders the first few deltas with enough context to diagnose them without re-running a generate:
    /// the path, both lengths, the first differing offset, and a window around it. A bare count would make this
    /// gate useless in CI, where the generate is not reproducible by hand.</summary>
    private static string Describe(IReadOnlyList<SiteGenerator.RegionParityDelta> deltas)
    {
        var lines = deltas.Take(5).Select(d =>
        {
            var at = d.FirstDifferenceAt;
            var window = at < 0
                ? "(one is a prefix of the other)"
                : $"composed…{Excerpt(d.Composed, at)}… vs sliced…{Excerpt(d.Sliced, at)}…";
            return $"  {d.Path}: composed={d.Composed.Length}B sliced={d.Sliced.Length}B firstDiff@{at} {window}";
        });
        return $"{deltas.Count} page(s) whose composed region differs from the sliced region:\n"
            + string.Join("\n", lines)
            + (deltas.Count > 5 ? $"\n  …and {deltas.Count - 5} more" : string.Empty);
    }

    private static string Excerpt(string s, int at)
    {
        if (s.Length == 0) return "(empty)";
        var start = Math.Max(0, at - 40);
        var len = Math.Min(120, s.Length - start);
        return s.Substring(start, len).Replace("\n", "\\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A reader still holding a generated file open must not fail the test run.
        }
    }
}
