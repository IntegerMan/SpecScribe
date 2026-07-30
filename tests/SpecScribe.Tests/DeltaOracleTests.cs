using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 22.6 AC #6 — THE ORACLE. A delta applied to a consumer at state N−1 must yield a page set
/// BYTE-IDENTICAL to what a cold consumer fetching manifest N would hold.
///
/// <para><b>Why this suite exists separately from <see cref="SiteGeneratorSpaTests"/>'s sidecar tests.</b> Those
/// assert the delta DOCUMENT's shape — that it names the right paths. That is necessary and not sufficient: a
/// delta can name exactly the right pages and still be wrong, if the bytes a consumer ends up holding for those
/// pages differ from a full fetch. AC #6 says so explicitly ("not by asserting the delta document's shape
/// alone"), so it never asserts on the document's page lists as the outcome. It builds a real consumer, applies
/// real deltas produced by real regenerations, and diffs the RESULT against a cold fetch of the same manifest —
/// the oracle-diff shape Story 22.1's spike used for recompute correctness, pointed at transport instead.
/// (The document IS inspected in one narrow place — <see cref="ReadIncrementalDelta"/>'s vacuity guard — but only
/// to prove the incremental path was exercised at all, never as the thing being verified.)</para>
///
/// <para><b>The failure this is really hunting</b> is a false <i>unchanged</i>: a page that moved but was left
/// out of <c>changed</c>, so the consumer keeps stale bytes forever with nothing to detect it. A shape assertion
/// cannot see that — the document looks perfectly well-formed. A byte diff against the oracle sees it
/// immediately, on the one page that was omitted.</para></summary>
public class DeltaOracleTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-delta-oracle-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    public DeltaOracleTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "notes"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), """
            # Epics

            ## Epic List

            ### Epic 1: Foundation

            Stand up the portal.

            ## Epic 1: Foundation

            ### Story 1.1: Foundation Story

            As a maintainer, I want the foundation.
            """);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            "# Story 1.1: Foundation Story\n\nStatus: in-progress\n\n## Story\n\nAs a maintainer, I want it.\n");
        File.WriteAllText(Path.Combine(Source, "notes", "guide.md"), "# Guide\n\nORIGINAL BODY\n");
        File.WriteAllText(Path.Combine(Source, "notes", "second.md"), "# Second\n\nAnother note.\n");
        File.WriteAllText(Path.Combine(Adrs, "0001-a-decision.md"),
            "# ADR 0001: A Decision\n\n**Status:** Accepted\n\nBody.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(string output) => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: output, projectName: "SpecScribe", includeReadme: false, emitSpa: true);

    /// <summary>A consumer holding a page set, exactly as a polling client would: path → content region, plus the
    /// manifest it was built from. Nothing here knows about deltas — it is the thing deltas are applied TO.</summary>
    private sealed record ConsumerState(Dictionary<string, string> Pages, string ManifestJson);

    /// <summary>What a COLD consumer fetching the current manifest would hold: every page, read out of the chunks
    /// the emitter just wrote. This is the oracle.</summary>
    private static ConsumerState ColdFetch(string site)
    {
        var manifestJson = File.ReadAllText(
            Path.Combine(site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar)));
        using var manifest = JsonDocument.Parse(manifestJson);
        var chunks = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var page in manifest.RootElement.GetProperty("pages").EnumerateObject())
            {
                var chunk = page.Value.GetProperty("chunk").GetString()!;
                pages[page.Name] = ReadPage(site, chunks, chunk, page.Name);
            }
        }
        finally
        {
            foreach (var d in chunks.Values) d.Dispose();
        }
        return new ConsumerState(pages, manifestJson);
    }

    /// <summary>Applies ONE delta document to a held state the way a real polling consumer must: refetch on a
    /// <c>full</c> marker, otherwise fetch only the named chunks and take only the named pages from them, and
    /// drop what the delta says was removed. Deliberately naive — it trusts the delta completely, because
    /// trusting the delta completely is exactly the claim under test.</summary>
    private static ConsumerState ApplyDelta(string site, ConsumerState held, string deltaJson)
    {
        using var delta = JsonDocument.Parse(deltaJson);
        var root = delta.RootElement;

        if (root.GetProperty("full").GetBoolean())
        {
            // The contract's own instruction for a full marker: refetch. A consumer that tried to merge here
            // would be doing something the document explicitly told it not to.
            return ColdFetch(site);
        }

        var pages = new Dictionary<string, string>(held.Pages, StringComparer.Ordinal);
        var chunks = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        try
        {
            foreach (var name in new[] { "changed", "added" })
            {
                foreach (var p in root.GetProperty(name).EnumerateArray())
                {
                    var path = p.GetString()!;
                    // The consumer does NOT get to consult the manifest for the chunk — the delta's own `chunks`
                    // list is what it was given, so resolving through it is what proves that list is sufficient.
                    var chunk = ChunkCarrying(site, root, chunks, path)
                        ?? throw new Xunit.Sdk.XunitException(
                            $"delta named '{path}' but none of its `chunks` carries it — the chunks list is incomplete");
                    pages[path] = ReadPage(site, chunks, chunk, path);
                }
            }
            foreach (var p in root.GetProperty("removed").EnumerateArray())
            {
                pages.Remove(p.GetString()!);
            }
        }
        finally
        {
            foreach (var d in chunks.Values) d.Dispose();
        }

        var manifestJson = File.ReadAllText(
            Path.Combine(site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar)));
        return new ConsumerState(pages, manifestJson);
    }

    private static string? ChunkCarrying(
        string site, JsonElement delta, Dictionary<string, JsonDocument> cache, string path)
    {
        foreach (var c in delta.GetProperty("chunks").EnumerateArray())
        {
            var chunk = c.GetString()!;
            if (Chunk(site, cache, chunk).RootElement.TryGetProperty(path, out _)) return chunk;
        }
        return null;
    }

    private static string ReadPage(
        string site, Dictionary<string, JsonDocument> cache, string chunk, string path) =>
        Chunk(site, cache, chunk).RootElement.GetProperty(path).GetString()!;

    private static JsonDocument Chunk(string site, Dictionary<string, JsonDocument> cache, string chunk)
    {
        if (!cache.TryGetValue(chunk, out var doc))
        {
            doc = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(site, chunk.Replace('/', Path.DirectorySeparatorChar))));
            cache[chunk] = doc;
        }
        return doc;
    }

    private string ReadDelta() =>
        File.ReadAllText(Path.Combine(Site, SpaDelivery.DeltaPath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Reads the delta AND proves it is a genuine incremental one. Without this every non-topology case
    /// here is VACUOUS: a <c>full</c> marker makes <see cref="ApplyDelta"/> refetch everything, which trivially
    /// equals the oracle no matter how broken the diff is. So the cases that are supposed to exercise the
    /// incremental path assert they actually did, and name at least one page the delta had to deliver.</summary>
    private string ReadIncrementalDelta(params string[] mustName)
    {
        var json = ReadDelta();
        using var delta = JsonDocument.Parse(json);
        Assert.False(
            delta.RootElement.GetProperty("full").GetBoolean(),
            "VACUOUS: this case degraded to a `full` marker, so applying it is just a refetch and proves nothing "
            + "about the diff. Fix the cause rather than deleting this guard.");

        var named = new[] { "changed", "added", "removed" }
            .SelectMany(k => delta.RootElement.GetProperty(k).EnumerateArray().Select(e => e.GetString()!))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var path in mustName)
        {
            Assert.True(named.Contains(path), $"the delta never named '{path}', so this case exercised nothing");
        }
        return json;
    }

    /// <summary>THE ORACLE, and its scope is deliberate: what a cold consumer fetching <b>manifest N</b> — the
    /// manifest this watch session just emitted — would hold. AC #6's wording is exactly that, and the choice is
    /// load-bearing rather than convenient.
    ///
    /// <para><b>A separately-generated site was tried FIRST and rejected, on evidence.</b> Running an independent
    /// cold <c>GenerateAll</c> into its own directory and diffing against that failed all five cases here on two
    /// pages — <c>diagnostics.html</c> and <c>code-map.html</c>. Neither was a transport defect:</para>
    /// <list type="bullet">
    /// <item><c>diagnostics.html</c> ECHOES THE CONFIGURED OUTPUT ROOT inside its own content region — Story 22.2
    /// found it is "THE one page whose contentHash is output-path dependent" and deliberately normalized nothing,
    /// so the hash describes the shipped bytes. Two different output directories therefore produce two different
    /// pages by design, and any oracle built on a second directory is comparing a page against a different
    /// page.</item>
    /// <item><c>code-map.html</c> differed because an incremental route's output can diverge from a cold full
    /// rebuild's — which is RECOMPUTE correctness, the axis Story 22.1 measured and Story 22.5 owns. This story
    /// transports whatever the generator emitted; it cannot and must not be held responsible for whether the
    /// generator should have emitted something else.</item>
    /// </list>
    /// <para>Conflating the two would have made this suite fail for reasons outside its own contract — the worst
    /// kind of test, since the obvious "fix" is an exception list that quietly blinds it to real transport bugs.
    /// Scoped to the same emit, it stays sharp on exactly the failure it hunts: if the delta omits a page that
    /// moved, `applied` keeps the old bytes while this oracle has the new ones, and it reports STALE by name.</para></summary>
    private ConsumerState ColdOracle() => ColdFetch(Site);

    /// <summary>The assertion AC #6 actually asks for: the applied-delta page set equals the cold-fetch page set,
    /// page for page, BYTE for byte. Reported as named differences rather than a bare inequality, because "these
    /// two dictionaries differ" is useless at 800 pages and the whole value of an oracle is naming the page that
    /// went stale.</summary>
    private static void AssertMatchesOracle(ConsumerState applied, ConsumerState oracle)
    {
        var missing = oracle.Pages.Keys.Where(k => !applied.Pages.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var orphaned = applied.Pages.Keys.Where(k => !oracle.Pages.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var stale = oracle.Pages
            .Where(kv => applied.Pages.TryGetValue(kv.Key, out var got) && !string.Equals(got, kv.Value, StringComparison.Ordinal))
            .Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0 && orphaned.Count == 0 && stale.Count == 0,
            $"applied-delta state diverged from a cold fetch.\n"
            + $"  MISSING (delta never delivered): {string.Join(", ", missing)}\n"
            + $"  ORPHANED (delta never removed):  {string.Join(", ", orphaned)}\n"
            + $"  STALE (false 'unchanged'):       {string.Join(", ", stale)}");

        // A vacuity guard: an empty page set would satisfy every check above.
        Assert.NotEmpty(applied.Pages);
    }

    private SiteGenerator StartSession()
    {
        if (Directory.Exists(Site)) Directory.Delete(Site, recursive: true);
        var gen = new SiteGenerator(Options(Site)) { EmitDeltaSidecar = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    /// <summary>AC #6 over the GATED route — a content-only edit through <c>GenerateOne</c>, the one Task 1's
    /// measurement gate was taken on.</summary>
    [Fact]
    public void ContentEditViaGenerateOne_AppliedDelta_EqualsAColdFetch()
    {
        var gen = StartSession();
        var held = ColdFetch(Site);

        var doc = Path.Combine(Source, "notes", "guide.md");
        File.WriteAllText(doc, "# Guide\n\nEDITED BODY with <angle brackets> & ampersands.\n");
        gen.SetWatchTrigger("_bmad-output/notes/guide.md");
        Assert.NotEqual(GenerationOutcome.Error, gen.GenerateOne(doc).Outcome);

        AssertMatchesOracle(ApplyDelta(Site, held, ReadIncrementalDelta("notes/guide.html")), ColdOracle());
    }

    /// <summary>AC #6 over <c>RegenerateEpics</c> — a story artifact edit, which moves both the story page and
    /// the aggregate surfaces the epics family re-renders.</summary>
    [Fact]
    public void StoryEditViaRegenerateEpics_AppliedDelta_EqualsAColdFetch()
    {
        var gen = StartSession();
        var held = ColdFetch(Site);

        var story = Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md");
        File.WriteAllText(story, "# Story 1.1: Foundation Story\n\nStatus: done\n\n## Story\n\nNow finished.\n");
        gen.SetWatchTrigger("_bmad-output/implementation-artifacts/1-1-foundation.md");
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateEpics().Outcome);

        AssertMatchesOracle(ApplyDelta(Site, held, ReadIncrementalDelta("epics/story-1-1.html")), ColdOracle());
    }

    /// <summary>AC #6 over a DELETE (<c>RemoveFor</c>). The interesting half is `removed`: a consumer that
    /// applied `changed`/`added` correctly but ignored `removed` would keep an ORPHANED page and still look fine
    /// to any shape assertion.</summary>
    [Fact]
    public void FileDeleteViaRemoveFor_AppliedDelta_EqualsAColdFetch()
    {
        var gen = StartSession();
        var held = ColdFetch(Site);
        var doomed = Path.Combine(Source, "notes", "second.md");
        Assert.Contains("notes/second.html", held.Pages.Keys);

        File.Delete(doomed);
        gen.SetWatchTrigger("_bmad-output/notes/second.md");
        Assert.NotEqual(GenerationOutcome.Error, gen.RemoveFor(doomed).Outcome);

        var applied = ApplyDelta(Site, held, ReadIncrementalDelta("notes/second.html"));

        AssertMatchesOracle(applied, ColdOracle());
        // Named explicitly: the removal is the point of this case, and AssertMatchesOracle would also pass if the
        // oracle somehow still carried the page.
        Assert.DoesNotContain("notes/second.html", applied.Pages.Keys);
    }

    /// <summary>AC #6 + AC #7 together: a topology escalation must DEGRADE TO FULL, and the consumer that obeys
    /// the full marker must still land byte-identical to a cold fetch. Both halves matter — a delta that degraded
    /// to full but left the consumer wrong would be no better than one that never degraded.</summary>
    [Fact]
    public void TopologyEscalation_DegradesToFull_AndTheRefetchEqualsAColdFetch()
    {
        var gen = StartSession();
        var held = ColdFetch(Site);

        // A new directory of artifacts — the change class that strands cross-artifact surfaces no family route
        // re-renders, which is why it escalates rather than routing narrowly.
        Directory.CreateDirectory(Path.Combine(Source, "notes", "nested"));
        File.WriteAllText(Path.Combine(Source, "notes", "nested", "deep.md"), "# Deep\n\nNested note.\n");
        gen.SetWatchTrigger(FileWatcherService.TopologyEventLabel);
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateTopology().Outcome);

        var deltaJson = ReadDelta();
        using (var delta = JsonDocument.Parse(deltaJson))
        {
            Assert.True(
                delta.RootElement.GetProperty("full").GetBoolean(),
                "a topology escalation must degrade to a full marker (Trap 5: a literal diff there is larger than "
                + "the full payload it was meant to replace)");
        }

        AssertMatchesOracle(ApplyDelta(Site, held, deltaJson), ColdOracle());
    }

    /// <summary>Code review finding (Story 22.6): Task 7's own checklist names "a directory RENAME" as a minimum
    /// required scenario, and the test above exercises a directory CREATION instead (an undisclosed
    /// substitution). This is the literal case — <see cref="Directory.Move"/> on a pre-existing directory, old
    /// path gone, new path present — proving the rename converges to a full marker AND that the renamed path's
    /// old and new locations are exactly right, not merely that "something changed" produced a full delta.</summary>
    [Fact]
    public void DirectoryRename_DegradesToFull_AndTheRefetchEqualsAColdFetch()
    {
        var gen = StartSession();

        // A real rename needs a pre-existing directory to move — create and commit it via an ordinary topology
        // pass first, so the rename below is the only thing that changes.
        Directory.CreateDirectory(Path.Combine(Source, "notes", "movable"));
        File.WriteAllText(Path.Combine(Source, "notes", "movable", "inside.md"), "# Inside\n\nOriginal.\n");
        gen.SetWatchTrigger(FileWatcherService.TopologyEventLabel);
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateTopology().Outcome);

        var held = ColdFetch(Site);
        Assert.Contains("notes/movable/inside.html", held.Pages.Keys);

        // The actual rename: the old path disappears and the new path appears in the SAME filesystem operation —
        // not a fresh CreateDirectory, which is what the sibling test above exercises instead.
        Directory.Move(Path.Combine(Source, "notes", "movable"), Path.Combine(Source, "notes", "relocated"));
        gen.SetWatchTrigger(FileWatcherService.TopologyEventLabel);
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateTopology().Outcome);

        var deltaJson = ReadDelta();
        using (var delta = JsonDocument.Parse(deltaJson))
        {
            Assert.True(
                delta.RootElement.GetProperty("full").GetBoolean(),
                "a directory rename must degrade to a full marker exactly like a directory creation does");
        }

        var applied = ApplyDelta(Site, held, deltaJson);
        var oracle = ColdOracle();
        AssertMatchesOracle(applied, oracle);
        // Named explicitly, the same way the removal case elsewhere in this file does: the rename is the point,
        // and AssertMatchesOracle alone would also pass if the old path had simply been left behind as stale.
        Assert.DoesNotContain("notes/movable/inside.html", oracle.Pages.Keys);
        Assert.Contains("notes/relocated/inside.html", oracle.Pages.Keys);
    }

    /// <summary>The multi-step case, and the one most likely to expose a basis bug: THREE consecutive deltas
    /// applied in sequence to a single held state. A basis that advanced at the wrong moment (or failed to)
    /// survives one delta easily and falls apart by the third.</summary>
    [Fact]
    public void ThreeConsecutiveDeltas_AppliedInSequence_StillEqualAColdFetch()
    {
        var gen = StartSession();
        var held = ColdFetch(Site);

        var guide = Path.Combine(Source, "notes", "guide.md");
        File.WriteAllText(guide, "# Guide\n\nFirst edit.\n");
        gen.SetWatchTrigger("_bmad-output/notes/guide.md");
        gen.GenerateOne(guide);
        held = ApplyDelta(Site, held, ReadIncrementalDelta("notes/guide.html"));

        var fresh = Path.Combine(Source, "notes", "third.md");
        File.WriteAllText(fresh, "# Third\n\nBrand new.\n");
        gen.SetWatchTrigger("_bmad-output/notes/third.md");
        gen.GenerateOne(fresh);
        held = ApplyDelta(Site, held, ReadIncrementalDelta("notes/third.html"));

        File.WriteAllText(guide, "# Guide\n\nSecond edit, different bytes again.\n");
        gen.SetWatchTrigger("_bmad-output/notes/guide.md");
        gen.GenerateOne(guide);
        held = ApplyDelta(Site, held, ReadIncrementalDelta("notes/guide.html"));

        AssertMatchesOracle(held, ColdOracle());
        Assert.Contains("notes/third.html", held.Pages.Keys);
    }
}
