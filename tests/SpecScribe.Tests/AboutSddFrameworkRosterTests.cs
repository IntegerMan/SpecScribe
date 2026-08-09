using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pins the framework roster's PRODUCT IDENTITY — the canonical documentation URL and the
/// what-it-actually-is blurb each planned framework carries — separately from the roster's routing/labels
/// (covered by <see cref="SiteGeneratorHowToReadTests"/>).
///
/// <para>Exists because Story 12.1's spike found the roster's two GSD entries were ambiguous to the point of
/// being wrong: <c>README.md</c> recorded no URL for either, the roster carried only an id and a label, and the
/// story's own create-story research consequently pinned "GSD" to <c>gsd-build/gsd-2</c> — the RETIRED
/// predecessor — rather than to GSD Core, the current-version product. The two are not variants of one thing:
/// GSD Core is markdown-native under <c>.planning/</c> with no database, while GSD Pi is SQLite-authoritative
/// under <c>.gsd/</c>. A roster that names neither invites exactly that conflation again in Epic 12's coverage
/// stories.</para>
///
/// <para>These tests therefore assert the DISTINGUISHING facts, not prose: the marker directory and the docs
/// host per entry. A future edit that swaps GSD Core's <c>.planning/</c> for <c>.gsd/</c> — the precise mistake
/// this story corrected — turns them red.</para> [Story 12.1]</summary>
public class AboutSddFrameworkRosterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-sddroster-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    public AboutSddFrameworkRosterTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), "# Epics\n\n## Epic List\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    /// <summary>GSD's page must resolve to GSD Core — markdown-native, <c>.planning/</c> — and link the
    /// <c>core</c> documentation. The <c>.gsd/</c> assertion is the negative half and is the whole point: that
    /// marker belongs to the OTHER entry, and asserting only the positive would let the two swap silently.</summary>
    [Fact]
    public void AboutSdd_Gsd_PinsGsdCoreIdentity_MarkdownNativeUnderPlanningDir()
    {
        new SiteGenerator(Options()).GenerateAll();
        var gsd = SiteRegion.Read(Site, "about-sdd-gsd.html");

        Assert.Contains("GSD Core", gsd);
        Assert.Contains("<code>.planning/</code>", gsd);
        Assert.Contains("https://docs.opengsd.net/core", gsd);

        // GSD Core has no database and does not use the .gsd/ marker — both belong to GSD Pi.
        Assert.DoesNotContain("<code>.gsd/</code>", gsd);
        Assert.DoesNotContain("gsd.db", gsd);
    }

    /// <summary>GSD Pi's page must carry the SQLite-authoritative fact, because it is what forces a different
    /// coverage tier than GSD Core's — the markdown under <c>.gsd/</c> is a projection, not the source.</summary>
    [Fact]
    public void AboutSdd_GsdPi_PinsGsdPiIdentity_SqliteAuthoritativeUnderGsdDir()
    {
        new SiteGenerator(Options()).GenerateAll();
        var pi = SiteRegion.Read(Site, "about-sdd-gsd-pi.html");

        Assert.Contains("<code>.gsd/</code>", pi);
        Assert.Contains("gsd.db", pi);
        Assert.Contains("https://docs.opengsd.net/pi", pi);

        // The .planning/ marker is GSD Core's.
        Assert.DoesNotContain("<code>.planning/</code>", pi);
    }

    /// <summary>Every roster entry that records a canonical URL renders it as a real link on its own page. Guards
    /// the general contract rather than the two GSD rows, so a future framework added without a URL is a visible
    /// choice rather than a silent omission.</summary>
    [Fact]
    public void AboutSdd_EveryFrameworkWithACanonicalUrl_RendersItAsALink()
    {
        new SiteGenerator(Options()).GenerateAll();

        var withUrl = AboutSddTemplater.Frameworks.Where(f => !string.IsNullOrEmpty(f.Url)).ToList();
        Assert.NotEmpty(withUrl);

        foreach (var fw in withUrl)
        {
            var html = SiteRegion.Read(Site, fw.OutputPath);
            Assert.Contains($"href=\"{fw.Url}\"", html);
        }
    }

    /// <summary>The two GSD entries are DISTINCT products, not two versions of one — so their canonical URLs
    /// must differ. This is the single assertion that would have caught Story 12.1's original wrong premise.</summary>
    [Fact]
    public void Roster_GsdAndGsdPi_AreDistinctProductsWithDistinctCanonicalUrls()
    {
        var gsd = AboutSddTemplater.Frameworks.Single(f => f.Id == "gsd");
        var pi = AboutSddTemplater.Frameworks.Single(f => f.Id == "gsd-pi");

        Assert.False(string.IsNullOrEmpty(gsd.Url));
        Assert.False(string.IsNullOrEmpty(pi.Url));
        Assert.NotEqual(gsd.Url, pi.Url);
        Assert.Equal("1.42.3", gsd.Version);
        Assert.Null(pi.Version);

        // Neither may point at gsd-build/gsd-2 — the retired predecessor that "now continues as GSD Pi".
        Assert.DoesNotContain("gsd-2", gsd.Url);
        Assert.DoesNotContain("gsd-2", pi.Url);
    }
}
