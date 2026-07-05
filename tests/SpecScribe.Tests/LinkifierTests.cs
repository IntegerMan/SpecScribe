using SpecScribe;

namespace SpecScribe.Tests;

public class RequirementLinkifierTests
{
    private static RequirementsModel Requirements(params (RequirementKind Kind, int Number)[] reqs) => new()
    {
        Functional = reqs.Where(r => r.Kind == RequirementKind.Functional)
            .Select(r => Info(r.Kind, r.Number)).ToList(),
        NonFunctional = reqs.Where(r => r.Kind == RequirementKind.NonFunctional)
            .Select(r => Info(r.Kind, r.Number)).ToList(),
    };

    private static RequirementInfo Info(RequirementKind kind, int number) => new()
    {
        Kind = kind,
        Number = number,
        TextHtml = "text",
    };

    [Fact]
    public void Linkify_TurnsKnownIdsIntoLinks()
    {
        var model = Requirements((RequirementKind.Functional, 25), (RequirementKind.NonFunctional, 7));
        var html = RequirementLinkifier.Linkify("<p>Covers FR25 and NFR7.</p>", model, "../");

        Assert.Contains("<a class=\"req-ref\" href=\"../requirements/fr25.html\">FR25</a>", html);
        Assert.Contains("<a class=\"req-ref\" href=\"../requirements/nfr7.html\">NFR7</a>", html);
    }

    [Fact]
    public void Linkify_LeavesUnknownIdsAlone()
    {
        var model = Requirements((RequirementKind.Functional, 1));
        var html = RequirementLinkifier.Linkify("<p>FR99 is not defined.</p>", model, "");

        Assert.Equal("<p>FR99 is not defined.</p>", html);
    }

    [Fact]
    public void Linkify_NeverRewritesInsideExistingAnchors()
    {
        var model = Requirements((RequirementKind.Functional, 1));
        var input = "<a href=\"x.html\">FR1</a> but FR1 here";
        var html = RequirementLinkifier.Linkify(input, model, "");

        Assert.StartsWith("<a href=\"x.html\">FR1</a>", html);
        Assert.Contains("but <a class=\"req-ref\"", html);
    }

    [Fact]
    public void Linkify_SkipsTheRequirementsOwnPage()
    {
        var model = Requirements((RequirementKind.Functional, 1));
        var html = RequirementLinkifier.Linkify("<p>FR1 details</p>", model, "", skipId: "FR1");

        Assert.Equal("<p>FR1 details</p>", html);
    }

    [Fact]
    public void Linkify_DoesNotMatchPartialTokens()
    {
        var model = Requirements((RequirementKind.Functional, 1));
        var html = RequirementLinkifier.Linkify("<p>FR1x and XFR1 stay plain.</p>", model, "");

        Assert.DoesNotContain("<a", html);
    }
}

public class SourceLinkifierTests
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["game-architecture.md"] = "game-architecture.html",
        ["planning-artifacts/epics.md"] = "planning-artifacts/epics.html",
    };

    [Fact]
    public void Linkify_LinksKnownSourcePaths()
    {
        var html = SourceLinkifier.Linkify("<p>See _bmad-output/game-architecture.md for detail.</p>", Map, "../");

        Assert.Contains("<a href=\"../game-architecture.html\">_bmad-output/game-architecture.md</a>", html);
    }

    [Fact]
    public void Linkify_LeavesUnknownPathsAlone()
    {
        var input = "<p>_bmad-output/missing.md</p>";
        Assert.Equal(input, SourceLinkifier.Linkify(input, Map, ""));
    }

    [Fact]
    public void Linkify_LeavesFragmentNoteOutsideTheLink()
    {
        var html = SourceLinkifier.Linkify("[Source: _bmad-output/planning-artifacts/epics.md#Epic 1]", Map, "");

        Assert.Contains("</a>#Epic 1]", html);
    }
}

public class AdrLinkRewriterTests
{
    [Fact]
    public void Rewrite_MapsSiblingAdrLinksToHtml()
        => Assert.Equal(
            "<a href=\"0004-title.html\">x</a>",
            AdrLinkRewriter.Rewrite("<a href=\"0004-title.md\">x</a>"));

    [Fact]
    public void Rewrite_MapsBmadOutputLinksUpOneLevel()
        => Assert.Equal(
            "<a href=\"../game-architecture.html\">x</a>",
            AdrLinkRewriter.Rewrite("<a href=\"../../_bmad-output/game-architecture.md\">x</a>"));

    [Fact]
    public void Rewrite_MapsReadmeToAdrIndex()
        => Assert.Equal(
            "<a href=\"index.html\">x</a>",
            AdrLinkRewriter.Rewrite("<a href=\"./README.md\">x</a>"));

    [Fact]
    public void Rewrite_PreservesFragments()
        => Assert.Equal(
            "<a href=\"0002-core.html#decision\">x</a>",
            AdrLinkRewriter.Rewrite("<a href=\"0002-core.md#decision\">x</a>"));

    [Fact]
    public void Rewrite_IgnoresAbsoluteUrlsAndNonMdLinks()
    {
        const string input = "<a href=\"https://example.com/page\">x</a> <a href=\"other.html\">y</a>";
        Assert.Equal(input, AdrLinkRewriter.Rewrite(input));
    }
}
