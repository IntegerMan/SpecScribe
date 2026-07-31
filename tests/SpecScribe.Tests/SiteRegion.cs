using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Reads a generated page's CONTENT REGION back out of the emitted IR. [Story 23.6 AC #8]
///
/// <para><b>The substitute for ~206 <c>File.ReadAllText(Path.Combine(Site, "….html"))</c> reads.</b> Those
/// assertions were written against a document C# wrote; Story 23.6 deletes the writer, and the C# unit suite
/// deliberately does NOT run the Node prerender (see <see cref="SiteGenerator.PrerenderHtml"/> — a Nitro boot per
/// <c>GenerateAll</c> would make the ~2,890-test suite depend on Node and on a built artefact, which this story's
/// Dev Notes rule out in as many words). So after the deletion there is no <c>.html</c> under a test's output root
/// at all, and every one of those reads would throw <see cref="FileNotFoundException"/>.</para>
///
/// <para><b>The IR is the honest replacement, not a convenient one.</b> It is what a completed generate actually
/// produces now (ADR 0016 — <c>spa/</c> IS the IR), it is what the Nuxt renderer renders from, and it is what the
/// webview and SPA consume. An assertion against the region is therefore an assertion about what reaches every
/// surface, where the old one covered only the static page.</para>
///
/// <para><b>What it does NOT cover, stated rather than left to be discovered.</b> The region is
/// <c>nav markup + wayfinding + body</c>. <c>&lt;title&gt;</c>, <c>&lt;meta&gt;</c>, the favicon, the skip link,
/// the footer, <c>&lt;script src&gt;</c> tags and the anti-flash handshakes are NOT here — they are chrome. See
/// <see cref="RegionAssert"/> for where each of those assertions went.</para></summary>
internal static class SiteRegion
{
    /// <summary>The content region for <paramref name="outputRelativePath"/> (e.g. <c>"epics/epic-1.html"</c>)
    /// from the IR under <paramref name="siteRoot"/>.
    /// <para>Fails with an actionable message rather than a <see cref="KeyNotFoundException"/> when the route is
    /// absent: a page silently missing from the IR is exactly the class of defect this suite exists to catch, and
    /// a bare dictionary miss three frames deep does not say so.</para></summary>
    public static string Read(string siteRoot, string outputRelativePath)
    {
        var path = PathUtil.NormalizeSlashes(outputRelativePath);
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestFile))
        {
            throw new FileNotFoundException(
                $"No IR manifest at {manifestFile}. The IR is emitted on every generate since Story 23.6, so its "
                + "absence means GenerateAll did not complete — check the generator's events before this assertion.",
                manifestFile);
        }

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestFile));
        if (!manifest.RootElement.GetProperty("pages").TryGetProperty(path, out var entry))
        {
            var known = manifest.RootElement.GetProperty("pages").EnumerateObject()
                .Select(p => p.Name).Order(StringComparer.Ordinal).Take(8);
            throw new InvalidOperationException(
                $"'{path}' is not in the IR manifest — the page was not emitted at all. "
                + $"First few routes that were: {string.Join(", ", known)}");
        }

        var chunkRel = entry.GetProperty("chunk").GetString()!;
        var chunkFile = Path.Combine(siteRoot, chunkRel.Replace('/', Path.DirectorySeparatorChar));
        using var chunk = JsonDocument.Parse(File.ReadAllText(chunkFile));
        return chunk.RootElement.GetProperty(path).GetString()!;
    }

    /// <summary>True when the IR carries a route — the region-side replacement for
    /// <c>File.Exists(Path.Combine(Site, "….html"))</c>.</summary>
    public static bool Exists(string siteRoot, string outputRelativePath)
    {
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestFile)) return false;
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestFile));
        return manifest.RootElement.GetProperty("pages")
            .TryGetProperty(PathUtil.NormalizeSlashes(outputRelativePath), out _);
    }

    /// <summary>Every route in the IR, sorted — the replacement for a <c>Directory.EnumerateFiles(Site, "*.html")</c>
    /// walk.</summary>
    public static IReadOnlyList<string> Routes(string siteRoot)
    {
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestFile));
        return manifest.RootElement.GetProperty("pages").EnumerateObject()
            .Select(p => p.Name).Order(StringComparer.Ordinal).ToList();
    }
}
