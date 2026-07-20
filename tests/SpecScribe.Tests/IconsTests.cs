using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

public class IconsTests
{
    [Theory]
    [InlineData("done")]
    [InlineData("active")]
    [InlineData("review")]
    [InlineData("ready")]
    [InlineData("drafted")]
    [InlineData("pending")]
    [InlineData("deferred")]
    public void ForStatus_EveryKnownCssClassReturnsAGlyph(string cssClass)
        => AssertWellFormedIcon(Icons.ForStatus(cssClass));

    [Fact]
    public void ForStatus_UnknownClassReturnsEmpty()
        => Assert.Equal(string.Empty, Icons.ForStatus("not-a-real-status"));

    [Theory]
    [InlineData("Project")]
    [InlineData("Work")]
    [InlineData("Delivery")]
    [InlineData("Home")]
    [InlineData("Readme")]
    [InlineData("PRD")]
    [InlineData("Product Brief")]
    [InlineData("UX Design")]
    [InlineData("UX Experience")]
    [InlineData("Architecture")]
    [InlineData("Epics")]
    [InlineData("Requirements")]
    [InlineData("ADRs")]
    [InlineData("Spec")]
    [InlineData("Spec — alpha")]
    [InlineData("Sprint")]
    [InlineData("Overview")]
    [InlineData("Planning Artifacts")]
    [InlineData("Spec Kernel")]
    [InlineData("Implementation Artifacts")]
    [InlineData("Deferred")]
    [InlineData("Code Map")]
    [InlineData("Spec-Driven Development")]
    [InlineData("Help")]
    [InlineData("About")]
    [InlineData("Logs")]
    public void ForConcept_EveryKnownLabelReturnsAGlyph(string label)
        => AssertWellFormedIcon(Icons.ForConcept(label));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Some Unrecognized Concept")]
    [InlineData("Direct & Quick-Dev Work")] // orphaned home-band key removed — dual-rep debt closed
    public void ForConcept_UnknownOrEmptyLabelReturnsEmpty(string? label)
        => Assert.Equal(string.Empty, Icons.ForConcept(label));

    public static IEnumerable<object[]> EmittedConceptKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        // SiteNav emitters (full fixture so every gated label appears).
        var paths = new[]
        {
            "readme.md",
            "planning-artifacts/prds/x/prd.md",
            "planning-artifacts/briefs/x/brief.md",
            "planning-artifacts/ux-designs/x/DESIGN.md",
            "planning-artifacts/ux-designs/x/EXPERIENCE.md",
            "specs/spec-x/ARCHITECTURE-SPINE.md",
            "specs/spec-x/SPEC.md",
            "planning-artifacts/epics.md",
        };
        var nav = SiteNav.Build(
            paths,
            "SpecScribe",
            moduleDocs: ModuleContext.DocsFor(BmadModule.BmadMethod),
            hasAdrs: true,
            hasReadme: true,
            hasSprint: true,
            hasCodeMap: true);
        foreach (var (label, _) in nav.Items) keys.Add(label);
        foreach (var (label, _, _, _) in nav.QuickLinks) keys.Add(label);

        // ModuleContext well-known BMad Method doc labels.
        foreach (var doc in ModuleContext.DocsFor(BmadModule.BmadMethod))
            keys.Add(doc.Label);

        // ArtifactCoverage ConceptIconKeys (families always listed, present or not).
        var coverage = ArtifactCoverage.Build(
            Array.Empty<string>(),
            new Dictionary<string, DateOnly>(),
            new Dictionary<string, DateOnly>(),
            DateOnly.FromDateTime(DateTime.UtcNow));
        foreach (var family in coverage.Families)
            keys.Add(family.ConceptIconKey);

        // Nav group triggers, work-mode pills, evidence strip, change-surface chips.
        foreach (var key in new[]
                 {
                     "Project", "Architecture", "Work",
                     "Overview", "Requirements", "Plan", "Develop", "Review", "Track",
                     "Tasks", "Tests", "Verified",
                     "Story", "Sprint Status",
                 })
            keys.Add(key);

        foreach (var key in keys.OrderBy(k => k, StringComparer.Ordinal))
            yield return new object[] { key };
    }

    [Fact]
    public void EmittedConceptKeys_HasExpectedMinimumMembership()
    {
        var keys = EmittedConceptKeys().Select(row => (string)row[0]).ToHashSet(StringComparer.Ordinal);
        // Guard against a broken SiteNav fixture silently shrinking reverse coverage.
        Assert.True(keys.Count >= 20, $"Expected ≥20 emitted concept keys, got {keys.Count}");
        foreach (var required in new[] { "Home", "PRD", "Epics", "Project", "Work", "Tasks", "Story" })
            Assert.Contains(required, keys);
    }

    [Fact]
    public void AmpersandDisplay_UsesHtmlEncodeNotHandTypedEntityBesideIconKey()
    {
        // IconKey ≠ display label: glyph from "Epics"; ampersand only via PathUtil.Html.
        AssertWellFormedIcon(Icons.ForConcept("Epics"));
        Assert.Equal("Epics &amp; Stories", PathUtil.Html("Epics & Stories"));
        Assert.DoesNotContain("&amp;", Icons.ForConcept("Epics"));
    }

    [Theory]
    [MemberData(nameof(EmittedConceptKeys))]
    public void ForConcept_EveryEmittedLabelHasAGlyph(string label)
        => AssertWellFormedIcon(Icons.ForConcept(label));

    [Fact]
    public void AssertWellFormedIcon_RejectsNamedColorFill()
    {
        var bad = "<svg class=\"ss-icon\" viewBox=\"0 0 16 16\" aria-hidden=\"true\" focusable=\"false\" " +
                  "fill=\"black\" stroke=\"currentColor\"><path d=\"M0 0\"/></svg>";
        Assert.ThrowsAny<Exception>(() => AssertWellFormedIcon(bad));
    }

    internal static void AssertWellFormedIcon(string svg)
    {
        Assert.False(string.IsNullOrEmpty(svg));
        Assert.Contains("aria-hidden=\"true\"", svg);
        Assert.Contains("focusable=\"false\"", svg);
        Assert.Contains("stroke=\"currentColor\"", svg);
        Assert.Contains("currentColor", svg);
        Assert.DoesNotContain("#", svg);
        Assert.DoesNotContain("rgb(", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rgba(", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hsl(", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hsla(", svg, StringComparison.OrdinalIgnoreCase);
        // Named colors in fill/stroke attrs (currentColor and none are the only allowed paint values).
        Assert.False(
            Regex.IsMatch(
                svg,
                @"\b(?:fill|stroke)\s*=\s*(?:""(?!currentColor|none)[^""]*""|'(?!currentColor|none)[^']*'|(?!currentColor\b|none\b)[a-zA-Z]+)",
                RegexOptions.IgnoreCase),
            "Glyph fill/stroke must be currentColor or none — no named colors.");
    }
}
