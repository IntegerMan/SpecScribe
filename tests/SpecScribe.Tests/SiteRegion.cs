using System.Text.Json;
using System.Text.RegularExpressions;
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

        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
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
        using var chunk = JsonDocument.Parse(ReadShared(chunkFile));
        return chunk.RootElement.GetProperty(path).GetString()!;
    }

    /// <summary>True when the IR carries a route — the region-side replacement for
    /// <c>File.Exists(Path.Combine(Site, "….html"))</c>.</summary>
    public static bool Exists(string siteRoot, string outputRelativePath)
    {
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestFile)) return false;
        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
        return manifest.RootElement.GetProperty("pages")
            .TryGetProperty(PathUtil.NormalizeSlashes(outputRelativePath), out _);
    }

    /// <summary>Every route in the IR, sorted — the replacement for a <c>Directory.EnumerateFiles(Site, "*.html")</c>
    /// walk.</summary>
    public static IReadOnlyList<string> Routes(string siteRoot)
    {
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
        return manifest.RootElement.GetProperty("pages").EnumerateObject()
            .Select(p => p.Name).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>True when the IR carries at least one route under <paramref name="routePrefix"/> (e.g.
    /// <c>"epics/"</c>) — the replacement for <c>Directory.Exists(Path.Combine(Site, "epics"))</c>.
    ///
    /// <para><b>Why this is not a directory check any more, and why that matters.</b> Those assertions
    /// (<c>Assert.False(Directory.Exists(…), "stale epics/ subtree must be deleted")</c>) were checking that a
    /// removal actually propagated. Once C# writes no pages there is no <c>epics/</c> directory under ANY
    /// condition, so the negative form would pass without the removal having happened at all — the vacuous-gate
    /// failure AC #2 exists to prevent. The route set is where the subtree lives now, so that is where the
    /// question is asked.</para></summary>
    public static bool HasRoutesUnder(string siteRoot, string routePrefix) =>
        RoutesUnder(siteRoot, routePrefix).Count > 0;

    /// <summary>Every route under <paramref name="routePrefix"/>, sorted — the replacement for
    /// <c>Directory.GetFiles(Path.Combine(Site, "commit"), "*.html")</c>. Returns empty rather than throwing when
    /// no IR exists, mirroring <c>Directory.GetFiles</c> on an absent directory.</summary>
    public static IReadOnlyList<string> RoutesUnder(string siteRoot, string routePrefix)
    {
        var prefix = PathUtil.NormalizeSlashes(routePrefix).TrimEnd('/') + "/";
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestFile)) return Array.Empty<string>();
        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
        return manifest.RootElement.GetProperty("pages").EnumerateObject()
            .Select(p => p.Name)
            .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>A page's head projection from the IR — the <c>&lt;title&gt;</c> and
    /// <c>&lt;meta name="description"&gt;</c> the renderer emits for it (Story 22.2 AC #5).
    /// <para>[Story 23.6 AC #8] The replacement for asserting those two values as substrings of a written
    /// document. Stronger than the string form it replaces: it checks the projected VALUE, not one rendering
    /// of it, so an escaping or attribute-order change cannot break the test and a genuinely wrong description
    /// cannot pass it by appearing somewhere else on the page.</para></summary>
    public static (string Title, string Description) Head(string siteRoot, string outputRelativePath)
    {
        var path = PathUtil.NormalizeSlashes(outputRelativePath);
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
        if (!manifest.RootElement.GetProperty("pages").TryGetProperty(path, out var entry))
        {
            throw new InvalidOperationException($"'{path}' is not in the IR manifest — the page was not emitted.");
        }
        var head = entry.GetProperty("head");
        return (head.GetProperty("title").GetString()!, head.GetProperty("description").GetString()!);
    }

    /// <summary>The IR's site-level CHROME block — the scripts and constants no page's region carries because
    /// they belong to the document, not to the content. [Story 23.6]
    ///
    /// <para><b>This is where the deleted writer's chrome assertions go.</b> A test that used to look for
    /// <c>mermaid.esm.min.mjs</c> or <c>data-ss-relgraph-boot</c> in a written page is asking two questions at
    /// once: "does the site carry this script at all?" and "does THIS page get it?". The first is a C# question
    /// and is answered here. The second is the renderer's, derived structurally from the region
    /// (<c>web/ir/adapter.ts</c> § <c>chromeNeeds</c>) and pinned by <c>web/test/chrome-needs.test.ts</c>.</para></summary>
    public static ChromeBlock Chrome(string siteRoot)
    {
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
        if (!manifest.RootElement.TryGetProperty("chrome", out var chrome))
        {
            throw new InvalidOperationException(
                "the IR manifest carries no 'chrome' block. Since Story 23.6 the renderer reads the favicon, the "
                + "asset cache-bust and every chrome script from there; without it the rendered pages lose all of "
                + "them SILENTLY, which is the failure this accessor exists to make loud.");
        }
        string Get(string name) => chrome.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;
        return new ChromeBlock(
            Get("assetVersion"), Get("faviconDataUri"), Get("hierarchyBootScript"),
            Get("graphBootScript"), Get("mermaidInitScript"), Get("tocActiveSectionScript"));
    }

    internal sealed record ChromeBlock(
        string AssetVersion,
        string FaviconDataUri,
        string HierarchyBootScript,
        string GraphBootScript,
        string MermaidInitScript,
        string TocActiveSectionScript);

    /// <summary>Overwrites a route's region in the IR with <paramref name="sentinel"/>, so a later read proves
    /// whether a regeneration actually refreshed that route.
    ///
    /// <para><b>The region-side form of the sentinel-clobber proof.</b> Several watch-mode tests wrote
    /// <c>"STALE-SENTINEL"</c> over a generated <c>.html</c> and then asserted the incremental path replaced it —
    /// the only way to tell "re-rendered" from "left alone", since a correct re-render is byte-identical to what
    /// was already there. Story 23.6 leaves no <c>.html</c> to clobber, so the sentinel goes into the chunk the
    /// route's region lives in.</para>
    ///
    /// <para>Written as VALID JSON on purpose: a stale read then returns the sentinel and the assertion fails with
    /// its own message, rather than throwing a parse error three frames down that says nothing about
    /// staleness.</para></summary>
    public static void PoisonRoute(string siteRoot, string route, string sentinel = "STALE-SENTINEL")
    {
        var path = PathUtil.NormalizeSlashes(route);
        var manifestFile = Path.Combine(siteRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        using var manifest = JsonDocument.Parse(ReadShared(manifestFile));
        if (!manifest.RootElement.GetProperty("pages").TryGetProperty(path, out var entry))
        {
            throw new InvalidOperationException($"cannot poison '{path}' — it is not in the IR manifest");
        }

        var chunkFile = Path.Combine(
            siteRoot,
            entry.GetProperty("chunk").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        Dictionary<string, string> chunk;
        using (var doc = JsonDocument.Parse(ReadShared(chunkFile)))
        {
            chunk = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        }
        chunk[path] = sentinel;
        File.WriteAllText(chunkFile, JsonSerializer.Serialize(chunk));
    }

    /// <summary>Asserts every same-site <c>href</c> in a page's region resolves to something that exists.
    ///
    /// <para><b>Consolidates six near-identical private copies</b> that lived in
    /// <c>SiteGeneratorCadenceTests</c>, <c>…CodeMapTests</c>, <c>…EpicsRemovalTests</c>, <c>…GroupedNavTests</c>,
    /// <c>…SprintTests</c> and <c>…TraceabilityMatrixTests</c>. Each read the written document and resolved every
    /// link against the filesystem; Story 23.6 leaves no written document, so all six had to change and there was
    /// no reason for them to change six different ways.</para>
    ///
    /// <para><b>A page link is checked against the IR, an asset link against the disk.</b> That split is the whole
    /// substance of the change: <c>.html</c> targets are ROUTES now — nothing writes them during a unit-suite
    /// generate (<see cref="SiteGenerator.PrerenderHtml"/> is off) — while stylesheets, scripts and directories are
    /// still real files C# places. Resolving a page link on disk would assert the writer still exists, which is
    /// exactly what this story removes.</para>
    ///
    /// <para><b>Coverage note, stated rather than left to be found.</b> The region is nav + wayfinding + body, so
    /// this sees every in-content and navigation link but NOT links emitted in chrome (the footer). Chrome links
    /// belong to <c>npm run check:links</c> over the rendered site.</para></summary>
    public static void AssertNoBrokenLocalLinks(string siteRoot, string route)
    {
        var region = Read(siteRoot, route);
        var routes = Routes(siteRoot).ToHashSet(StringComparer.Ordinal);
        var routeDir = PathUtil.NormalizeSlashes(Path.GetDirectoryName(route) ?? string.Empty);

        foreach (Match m in Regex.Matches(region, "href=\"(?<href>[^\"]+)\""))
        {
            var href = m.Groups["href"].Value;
            // Anything with a URI scheme (http:, data:, mailto:, vscode:, command:) or a bare fragment is not a
            // local reference — only same-site relative hrefs can dangle.
            if (href.StartsWith('#') || Regex.IsMatch(href, @"^[a-zA-Z][a-zA-Z0-9+.\-]*:")) continue;

            var target = href.Split('#')[0].Split('?')[0];
            if (target.Length == 0) continue;

            // Surrounding markup in the message: a dangling href is only actionable if you can see which widget
            // emitted it.
            var from = Math.Max(0, m.Index - 220);
            var context = region.Substring(from, Math.Min(440, region.Length - from));

            if (target.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolveRoute(routeDir, target);
                Assert.True(routes.Contains(resolved),
                    $"{route} links to '{href}', which is not a route in the IR (resolved: {resolved})."
                    + $"\nContext:\n…{context}…");
            }
            else
            {
                var onDisk = Path.GetFullPath(Path.Combine(
                    siteRoot,
                    ResolveRoute(routeDir, target).Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(onDisk) || Directory.Exists(onDisk),
                    $"{route} links to '{href}', which does not exist on disk (resolved: {onDisk})."
                    + $"\nContext:\n…{context}…");
            }
        }
    }

    /// <summary>Resolves a page-relative href against the linking page's directory, collapsing <c>..</c> and
    /// <c>.</c> segments — the route-space equivalent of <see cref="Path.GetFullPath(string)"/>, done without
    /// touching the filesystem because routes are not paths any more.</summary>
    private static string ResolveRoute(string routeDir, string target)
    {
        var stack = new List<string>();
        if (routeDir.Length > 0) stack.AddRange(routeDir.Split('/'));
        foreach (var segment in target.Split('/'))
        {
            if (segment is "" or ".") continue;
            if (segment == "..") { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); continue; }
            stack.Add(segment);
        }
        return string.Join('/', stack);
    }

    /// <summary>Reads a file the GENERATOR may be writing concurrently, without blocking it.
    ///
    /// <para>[Story 23.6] <c>File.ReadAllText</c> opens with <c>FileShare.Read</c>, which denies a concurrent
    /// WRITER. That is harmless when each test reads its own settled output, but the watch-mode tests read the
    /// live output root while the watcher is still regenerating — and once pages became IR routes, every one of
    /// those reads targets the same handful of chunk files instead of a page each. <c>BurstOfSaves</c> duly
    /// failed with "the process cannot access the file … because it is being used by another process", reported
    /// as a GENERATOR error: the test had locked the chunk the generator was trying to write. Sharing the handle
    /// keeps a read-only assertion from perturbing the thing it is asserting about.</para></summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
