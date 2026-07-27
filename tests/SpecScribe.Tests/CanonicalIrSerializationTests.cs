using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 22.2 AC #3: the canonical IR's GOLDEN ROUND-TRIP BOUNDARY — the whole IR document (manifest +
/// content chunks), not just the section view models <see cref="SectionViewModelSerializationTests"/> pins.
/// <para>Generalizes that suite's <c>AssertRoundTripsLossless</c> pattern (serialize → deserialize → RE-serialize
/// → compare the two JSON strings, because record value-equality reference-compares collection members and so
/// cannot stand in for "no data loss") from a single view-model record up to the shipped IR. What it proves is
/// stronger than "the JSON parses": the manifest is fully modellable as PLAIN TYPED DATA — every field, every
/// nesting level, every null — with no lossy corner, which is what makes the IR a contract a non-C# consumer
/// (Epic 23's Nuxt front end) can bind to rather than an opaque blob it must regex.</para>
/// <para><b>Enumerated and justified differences: none.</b> The comparison is byte-for-byte on both halves, so
/// this test carries no exception list. If one is ever needed, it belongs HERE, in this doc comment, next to the
/// assertion that admits it — not in a story file.</para></summary>
public class CanonicalIrSerializationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-ir-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    public CanonicalIrSerializationTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
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
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), """
            # Story 1.1: Foundation Story

            Status: done

            ## Story

            As a maintainer, I want the foundation. Prose with <angle brackets> & ampersands to exercise escaping.

            ## Acceptance Criteria

            1. It works.
            """);
        File.WriteAllText(Path.Combine(Adrs, "0001-a-decision.md"),
            "# ADR 0001: A Decision\n\n**Status:** Accepted\n\nBody with `code` & <markup>.\n");
        File.WriteAllText(Path.Combine(Adrs, "0002-another-decision.md"),
            "# ADR 0002: Another Decision\n\n**Status:** Accepted\n\nSecond body.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, emitSpa: true);

    private IReadOnlyList<SpaDelivery.OutputFile> IrFiles()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return SpaDelivery.BuildDataFiles(gen.RenderSpaBundle());
    }

    // Mirrors SpaDelivery's private manifest records — SAME field order, so a byte-identical re-serialization is
    // a meaningful claim rather than an artifact of reordering. A field added there without a field added here
    // fails this test loudly, which is the point: the IR shape is a contract, and this is where it is written down
    // a second time, independently.
    private sealed record IrCrumb(string Label, string? OutputRelativePath);
    private sealed record IrNavItem(string Label, string OutputRelativePath);
    private sealed record IrHead(string Title, string Description);
    private sealed record IrIsland(string? Id, string Kind);
    private sealed record IrOversizedPage(string Path, long ChunkBytes);
    private sealed record IrEntry(
        string Title,
        string Chunk,
        IReadOnlyList<IrCrumb> Breadcrumb,
        string? Parent,
        IReadOnlyList<string> Children,
        IrHead Head,
        IReadOnlyList<IrIsland> ScriptIslands,
        string ContentHash,
        int Bytes);
    private sealed record IrManifest(
        int SchemaVersion,
        string SiteTitle,
        string Entry,
        IReadOnlyList<IrNavItem> Nav,
        IReadOnlyList<IrOversizedPage> OversizedPages,
        IReadOnlyDictionary<string, IrEntry> Pages);

    private static readonly JsonSerializerOptions IrJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Manifest_RoundTripsByteIdentically_ThroughATypedModel()
    {
        var manifestJson = IrFiles().Single(f => f.OutputRelativePath == SpaDelivery.ManifestPath).Content;

        var model = JsonSerializer.Deserialize<IrManifest>(manifestJson, IrJson);
        Assert.NotNull(model);
        var reserialized = JsonSerializer.Serialize(model, IrJson);

        Assert.Equal(manifestJson, reserialized);

        // Sanity that the fixture actually exercised the interesting corners, so a green here is not vacuous:
        // a real page set, a null parent (the entry page), and a non-empty breadcrumb somewhere.
        Assert.Equal(SpaDelivery.SchemaVersion, model.SchemaVersion);
        Assert.NotEmpty(model.Pages);
        Assert.Contains(model.Pages.Values, p => p.Parent is null);
        Assert.Contains(model.Pages.Values, p => p.Breadcrumb.Count > 0);
    }

    [Fact]
    public void EveryContentChunk_RoundTripsByteIdentically()
    {
        var chunks = IrFiles()
            .Where(f => f.OutputRelativePath != SpaDelivery.ManifestPath)
            .ToList();

        Assert.NotEmpty(chunks);
        foreach (var chunk in chunks)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(chunk.Content, IrJson);
            Assert.NotNull(map);
            Assert.Equal(chunk.Content, JsonSerializer.Serialize(map, IrJson));
        }

        // The escaping corner this matters most for: the regions are HTML, so every chunk is dense with
        // </>/& escapes. A chunk that survived round-trip without carrying any would prove nothing.
        Assert.Contains(chunks, c => c.Content.Contains("\\u003C", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestAndChunks_AgreeOnEveryPage_AndOnItsRecordedHashAndSize()
    {
        var files = IrFiles();
        var manifest = JsonSerializer.Deserialize<IrManifest>(
            files.Single(f => f.OutputRelativePath == SpaDelivery.ManifestPath).Content, IrJson)!;

        var byChunk = files
            .Where(f => f.OutputRelativePath != SpaDelivery.ManifestPath)
            .ToDictionary(
                f => f.OutputRelativePath,
                f => JsonSerializer.Deserialize<Dictionary<string, string>>(f.Content, IrJson)!,
                StringComparer.Ordinal);

        foreach (var (path, entry) in manifest.Pages)
        {
            var region = Assert.Contains(path, byChunk[entry.Chunk]);
            // The addressing fields DESCRIBE the region that actually shipped — they are not a parallel truth.
            Assert.Equal(SpaDelivery.ContentHash(region), entry.ContentHash);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(region), entry.Bytes);
            Assert.Equal(SpaDelivery.ExtractScriptIslands(region).Count, entry.ScriptIslands.Count);
        }

        // Every chunk member is claimed by the manifest — no orphan region shipped that nothing addresses.
        foreach (var (chunkPath, map) in byChunk)
        {
            foreach (var path in map.Keys)
            {
                Assert.True(manifest.Pages.ContainsKey(path), $"{chunkPath} carries unindexed page {path}");
            }
        }
    }
}
