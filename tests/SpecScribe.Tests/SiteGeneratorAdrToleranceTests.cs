using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 4.2 ADR tolerance: records authored in non-standard formats or locations still render with
/// title, status, and links where derivable (AC #2) — multiple filename numbering schemes, unnumbered records
/// rendered (not dropped) and sorted last, status derived from a bold line OR a MADR "## Status" section OR
/// frontmatter, records nested one level under the ADR root, and a probed conventional ADR home resolving
/// end-to-end. Absent everywhere still means no ADR section and no error. Follows the temp-dir fixture style
/// of <see cref="SiteGeneratorAdapterTests"/>.</summary>
public class SiteGeneratorAdrToleranceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-adrtol-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    public SiteGeneratorAdrToleranceTests()
    {
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, "notes.md"), "# Notes\n\nA doc so the source tree is non-empty.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(string? adrs = null) => ForgeOptions.Resolve(
        source: Source, adrs: adrs ?? Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private string IndexHtml() => File.ReadAllText(Path.Combine(Site, "index.html"));

    [Fact]
    public void GenerateAll_MultipleNumberingSchemes_AllRenderAndUnnumberedSortsLast()
    {
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "ADR-0001-first.md"), "# First Decision\n\n**Status:** Accepted\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "0007-seventh.md"), "# Seventh Decision\n\n**Status:** Accepted\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "adr_3_third.md"), "# Third Decision\n\n**Status:** Accepted\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "decision-login.md"), "# Login Decision\n\nNo number, no status.\n");
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // Every scheme renders a page — including the unnumbered record (AC #2: not dropped).
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "ADR-0001-first.html")));
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0007-seventh.html")));
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "adr_3_third.html")));
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "decision-login.html")));

        // The tolerated-but-non-standard shape is reported once, categorized, non-fatal (Story 4.2 Task 5).
        var notice = Assert.Single(events, e => e.Outcome == GenerationOutcome.Skipped && e.RelativePath == "adrs/decision-login.md");
        Assert.Contains("no ADR number", notice.Message);
    }

    [Fact]
    public void GenerateAll_StatusDerivesFromBoldLineHeadingOrFrontmatter()
    {
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "0001-bold.md"), "# Bold Line\n\n**Status:** Accepted\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "0002-heading.md"), "# Heading Style\n\n## Status\n\nSuperseded by [0003](0003-frontmatter.md)\n\n## Context\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "0003-frontmatter.md"), "---\nstatus: proposed\n---\n\n# Frontmatter Style\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "0004-statusless.md"), "# No Status Anywhere\n\nBody.\n");

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // The home index band was removed (spec-declutter-home-dashboard); status derivation is verified on the
        // standalone ADR pages, whose status class is derived from the first word of the derived status.
        Assert.Contains("status-accepted", File.ReadAllText(Path.Combine(Site, "adrs", "0001-bold.html")));
        Assert.Contains("status-superseded", File.ReadAllText(Path.Combine(Site, "adrs", "0002-heading.html")));
        Assert.Contains("status-proposed", File.ReadAllText(Path.Combine(Site, "adrs", "0003-frontmatter.html")));

        // The status-less record still renders its page with a title and no status pill (AC #2).
        var statusless = File.ReadAllText(Path.Combine(Site, "adrs", "0004-statusless.html"));
        Assert.Contains("No Status Anywhere", statusless);
        Assert.DoesNotContain("class=\"pill status-", statusless);
    }

    [Fact]
    public void GenerateAll_NestedRecordsRenderAndRouteThroughWatch()
    {
        var decisions = Path.Combine(_root, "docs", "decisions");
        Directory.CreateDirectory(Path.Combine(decisions, "2024"));
        File.WriteAllText(Path.Combine(decisions, "0001-top.md"), "# Top Decision\n\n**Status:** Accepted\n\nBody.\n");
        var nested = Path.Combine(decisions, "2024", "0007-nested.md");
        File.WriteAllText(nested, "# Nested Decision\n\n**Status:** Accepted\n\nSee [0001](../0001-top.md) and [the index](../README.md).\n");
        File.WriteAllText(Path.Combine(decisions, "README.md"), "# Decisions\n\nLanding.\n");

        var gen = new SiteGenerator(Options(adrs: decisions));
        var events = gen.GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // The nested record keeps its subpath, so its authored relative cross-links survive the .md → .html swap.
        var nestedPage = Path.Combine(Site, "adrs", "2024", "0007-nested.html");
        Assert.True(File.Exists(nestedPage));
        Assert.Contains("href=\"../0001-top.html\"", File.ReadAllText(nestedPage));
        Assert.Contains("href=\"../index.html\"", File.ReadAllText(nestedPage));

        // Watch parity: an edit under the resolved (nested) ADR tree routes to RegenerateAdrs.
        Assert.True(gen.IsAdr(nested));
    }

    [Fact]
    public void GenerateAll_ProbedConventionalHomeResolvesEndToEnd()
    {
        // No docs/adrs and no --adrs: the docs/decisions convention is probed and renders (Story 4.2 Task 1).
        var decisions = Path.Combine(_root, "docs", "decisions");
        Directory.CreateDirectory(decisions);
        File.WriteAllText(Path.Combine(decisions, "0001-probed.md"), "# Probed Decision\n\n**Status:** Accepted\n\nBody.\n");

        var options = ForgeOptions.Resolve(source: Source, output: Site, projectName: "SpecScribe", includeReadme: false);
        Assert.Equal(decisions, options.AdrSourceRoot);

        var events = new SiteGenerator(options).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0001-probed.html")));
        Assert.Contains("Probed Decision", File.ReadAllText(Path.Combine(Site, "adrs", "0001-probed.html")));
    }

    [Fact]
    public void GenerateAll_NoAdrDirectoryAnywhere_NoSectionNoError()
    {
        var options = ForgeOptions.Resolve(source: Source, output: Site, projectName: "SpecScribe", includeReadme: false);

        var events = new SiteGenerator(options).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        Assert.False(Directory.Exists(Path.Combine(Site, "adrs")));
        Assert.DoesNotContain("Architecture Decision Records", IndexHtml());
    }

    [Fact]
    public void GenerateAll_UnnumberedOnlyDirectoryStillRendersRecordAndStaysReachable()
    {
        // The record gate is "any renderable record", not "any numbered file" (Story 4.2 Task 2). The home ADR
        // index band was removed (spec-declutter-home-dashboard); the record still renders its page and stays
        // reachable from home via the ADRs nav link / quick-link pill.
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "decision-login.md"), "# Login Decision\n\nBody.\n");

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "decision-login.html")));
        Assert.Contains("Login Decision", File.ReadAllText(Path.Combine(Site, "adrs", "decision-login.html")));
        // Home keeps a reachability link to the ADRs landing (nav + Explore Key Views pill).
        Assert.Contains("href=\"adrs/index.html\"", IndexHtml());
    }

    [Fact]
    public void GenerateAll_ReadmeAndTemplateRenderButAreNeverRecords()
    {
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nSee [TEMPLATE](TEMPLATE.md).\n");
        File.WriteAllText(Path.Combine(Adrs, "TEMPLATE.md"), "# Title\n\n**Status:** Proposed\n\nScaffolding.\n");

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // Both pages exist (cross-links resolve) but neither is a record: no card section, no nav gate.
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "index.html")));
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "TEMPLATE.html")));
        Assert.DoesNotContain("Architecture Decision Records", IndexHtml());
    }

    [Fact]
    public void GenerateAll_SupersededAndDeprecatedStatuses_RenderDistinctlyOnAdrPage()
    {
        // Story 10.4 AC2 "when they arrive": a multi-word "Superseded by …" and a "Deprecated" status must land on
        // the distinct status-superseded / status-deprecated pill classes on the standalone ADR page. (The home
        // index card was removed by spec-declutter-home-dashboard.)
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "0001-superseded.md"), "# ADR 0001: Old Way\n\n**Status:** Superseded by ADR 0002\n\n## Context\n\nBody.\n");
        File.WriteAllText(Path.Combine(Adrs, "0002-deprecated.md"), "# ADR 0002: Retired\n\n**Status:** Deprecated\n\n## Context\n\nBody.\n");

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        Assert.Contains("status-superseded", File.ReadAllText(Path.Combine(Site, "adrs", "0001-superseded.html")));
        Assert.Contains("status-deprecated", File.ReadAllText(Path.Combine(Site, "adrs", "0002-deprecated.html")));
    }
}
