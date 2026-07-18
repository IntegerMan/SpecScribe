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
        Design = reqs.Where(r => r.Kind == RequirementKind.Design)
            .Select(r => Info(r.Kind, r.Number)).ToList(),
    };

    private static RequirementInfo Info(RequirementKind kind, int number) => new()
    {
        Kind = kind,
        Number = number,
        TextHtml = "text",
        CoverageEpicNumbers = Array.Empty<int>(),
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

    [Fact]
    public void Linkify_LinksLowercaseAndMixedCaseKnownIds_PreservingAuthoredCasing()
    {
        var model = Requirements(
            (RequirementKind.Functional, 6),
            (RequirementKind.NonFunctional, 2),
            (RequirementKind.Design, 1));
        var html = RequirementLinkifier.Linkify("<p>see fr6, Nfr2, and Ux-Dr1.</p>", model, "../");

        Assert.Contains("<a class=\"req-ref\" href=\"../requirements/fr6.html\">fr6</a>", html);
        Assert.Contains("<a class=\"req-ref\" href=\"../requirements/nfr2.html\">Nfr2</a>", html);
        Assert.Contains("<a class=\"req-ref\" href=\"../requirements/ux-dr1.html\">Ux-Dr1</a>", html);
    }

    [Fact]
    public void Linkify_LeavesUnknownLowercaseIdsAlone()
    {
        var model = Requirements((RequirementKind.Functional, 1));
        var html = RequirementLinkifier.Linkify("<p>fr99 is not defined.</p>", model, "");

        Assert.Equal("<p>fr99 is not defined.</p>", html);
    }

    [Fact]
    public void Linkify_DoesNotPartialMatchMultiDigitIds()
    {
        var onlyFr6 = Requirements((RequirementKind.Functional, 6));
        Assert.DoesNotContain("<a", RequirementLinkifier.Linkify("<p>FR60 stays plain.</p>", onlyFr6, ""));

        var onlyFr60 = Requirements((RequirementKind.Functional, 60));
        Assert.DoesNotContain("<a", RequirementLinkifier.Linkify("<p>FR6 stays plain.</p>", onlyFr60, ""));
    }

    [Fact]
    public void Linkify_WhenBothFr6AndFr60Known_EachLinksToItsOwnSlug()
    {
        var model = Requirements((RequirementKind.Functional, 6), (RequirementKind.Functional, 60));
        var html = RequirementLinkifier.Linkify("<p>FR6 and FR60.</p>", model, "");

        Assert.Contains("<a class=\"req-ref\" href=\"requirements/fr6.html\">FR6</a>", html);
        Assert.Contains("<a class=\"req-ref\" href=\"requirements/fr60.html\">FR60</a>", html);
    }

    [Fact]
    public void Linkify_SkipIdSuppressesLowercaseSelfMention()
    {
        var model = Requirements((RequirementKind.Functional, 1));
        var html = RequirementLinkifier.Linkify("<p>fr1 details</p>", model, "", skipId: "FR1");

        Assert.Equal("<p>fr1 details</p>", html);
    }

    [Fact]
    public void Linkify_LinksEveryOccurrenceOfARepeatedId()
    {
        var model = Requirements((RequirementKind.Functional, 6));
        var html = RequirementLinkifier.Linkify("<p>FR6 then FR6 again.</p>", model, "");

        Assert.Equal(2, CountOccurrences(html, "class=\"req-ref\""));
    }

    [Fact]
    public void Linkify_LinksKnownAndSkipsUnknownInSameText()
    {
        var model = Requirements((RequirementKind.Functional, 6));
        var html = RequirementLinkifier.Linkify("<p>FR6 and FR99.</p>", model, "../");

        Assert.Contains("<a class=\"req-ref\" href=\"../requirements/fr6.html\">FR6</a>", html);
        Assert.Contains("FR99", html);
        Assert.DoesNotContain("fr99.html", html);
    }

    [Fact]
    public void Linkify_TurnsKnownUxDrIntoLink()
    {
        var model = Requirements((RequirementKind.Design, 25));
        var html = RequirementLinkifier.Linkify("<p>See UX-DR25 for the pattern.</p>", model, "");

        Assert.Contains("<a class=\"req-ref\" href=\"requirements/ux-dr25.html\">UX-DR25</a>", html);
    }

    [Fact]
    public void Linkify_LeavesUnknownUxDrAlone()
    {
        var model = Requirements((RequirementKind.Design, 1));
        var html = RequirementLinkifier.Linkify("<p>UX-DR99 is not defined.</p>", model, "");

        Assert.Equal("<p>UX-DR99 is not defined.</p>", html);
    }

    [Fact]
    public void Linkify_NeverRewritesUxDrInsideExistingAnchors()
    {
        var model = Requirements((RequirementKind.Design, 1));
        var input = "<a href=\"x.html\">UX-DR1</a> but UX-DR1 here";
        var html = RequirementLinkifier.Linkify(input, model, "");

        Assert.StartsWith("<a href=\"x.html\">UX-DR1</a>", html);
        Assert.Contains("but <a class=\"req-ref\" href=\"requirements/ux-dr1.html\">UX-DR1</a>", html);
    }

    [Fact]
    public void Linkify_ReturnsInputUnchanged_WhenNoRequirementsKnown()
    {
        var model = Requirements();
        const string input = "<p>FR6 has nowhere to go.</p>";

        Assert.Equal(input, RequirementLinkifier.Linkify(input, model, ""));
    }

    [Fact]
    public void Linkify_NeverRewritesIdsInsideTagAttributes()
    {
        // Same corruption class StoryEpicLinkifier already guards: a mention inside data-tip / data-copy
        // must stay plain text. Injecting <a href="…"> into the attribute shatters the tag and dumps the
        // payload into the visible button face (Address deferred Next Steps on epic pages).
        var model = Requirements((RequirementKind.Design, 1), (RequirementKind.Functional, 25));
        const string tip = "<div data-tip=\"See UX-DR1 and FR25\">visible</div>";
        Assert.Equal(tip, RequirementLinkifier.Linkify(tip, model, ""));

        const string copy =
            "<button class=\"cmd-copy\" data-copy=\"/bmad-quick-dev Address UX-DR1 — owner deferred trim\" " +
            "aria-label=\"Address deferred — copy command\">" +
            "<span class=\"cmd-text\">Address deferred</span></button>";
        Assert.Equal(copy, RequirementLinkifier.Linkify(copy, model, "../"));
    }

    [Fact]
    public void Linkify_AddressDeferredBadge_KeepsShortLabelWhenPayloadMentionsUxDr()
    {
        var model = Requirements((RequirementKind.Design, 1));
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["quick-dev"] = "/bmad-quick-dev",
            ["sprint-status"] = "/bmad-sprint-status",
        });
        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem(
                "<p>Epic 1 header back-fill tags UX-DR1 — owner deferred confirmation/trim; mappings left as-is.</p>",
                Resolved: false, null, null),
            "code review of 9-2", 9,
            "../follow-ups/deferred-epic-1-header-back-fil-tags-ux-dr1.html",
            "9-2-nfr-and-ux-dr-coverage-maps");
        var epic = new EpicInfo
        {
            Number = 9,
            Title = "E",
            GoalHtml = "",
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories =
            [
                new StoryInfo
                {
                    Id = "9.1", Title = "S", Status = "in-progress", EpicNumber = 9,
                    UserStoryHtml = "", AcBlocksHtml = Array.Empty<string>(),
                }
            ],
        };

        var html = BmadCommands.RenderEpicNextSteps(epic, catalog, [slot]);
        Assert.Contains("UX-DR1", html); // payload carries the mention

        var linked = RequirementLinkifier.Linkify(html, model, "../");

        // Visible face stays the short label — not the spilled copy payload.
        Assert.Contains("cmd-text\">Address deferred<", linked);
        Assert.DoesNotContain("cmd-text\">/bmad-quick-dev", linked);
        // UX-DR1 lived only inside the protected data-copy attribute → no req-ref injection.
        Assert.DoesNotContain("req-ref", linked);
        Assert.DoesNotContain("requirements/ux-dr1.html", linked);
        Assert.Contains("UX-DR1", linked);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
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

    [Fact]
    public void Linkify_LinksMultipleCitationsInOneBlob()
    {
        var html = SourceLinkifier.Linkify(
            "<p>_bmad-output/game-architecture.md and _bmad-output/planning-artifacts/epics.md</p>", Map, "../");

        Assert.Contains("href=\"../game-architecture.html\"", html);
        Assert.Contains("href=\"../planning-artifacts/epics.html\"", html);
    }
}

public class StoryEpicLinkifierTests
{
    /// <summary>A minimal plan: Epic 1 (stories 1.1, 1.10) and Epic 12 (no stories).</summary>
    private static EpicsModel Model() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            Epic(1, Story("1.1", 1), Story("1.10", 1)),
            Epic(12),
        },
    };

    private static EpicInfo Epic(int number, params StoryInfo[] stories) => new()
    {
        Number = number,
        Title = "Epic title",
        GoalHtml = string.Empty,
        Status = stories.Length > 0 ? EpicStatus.Drafted : EpicStatus.Pending,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    private static StoryInfo Story(string id, int epicNumber) => new()
    {
        Id = id,
        EpicNumber = epicNumber,
        Title = "Story title",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
    };

    [Fact]
    public void Linkify_TurnsKnownMentionsIntoLinks()
    {
        var html = StoryEpicLinkifier.Linkify("<p>See Story 1.1 under Epic 1.</p>", Model(), "../");

        Assert.Contains("<a class=\"story-ref\" href=\"../epics/story-1-1.html\">Story 1.1</a>", html);
        Assert.Contains("<a class=\"epic-ref\" href=\"../epics/epic-1.html\">Epic 1</a>", html);
    }

    [Fact]
    public void Linkify_HandlesMultiDigitIdsWithoutPrefixCollision()
    {
        var html = StoryEpicLinkifier.Linkify("<p>Story 1.10 and Epic 12.</p>", Model(), "");

        Assert.Contains("href=\"epics/story-1-10.html\">Story 1.10</a>", html);
        Assert.Contains("href=\"epics/epic-12.html\">Epic 12</a>", html);
    }

    [Fact]
    public void Linkify_LeavesUnknownIdsAlone()
    {
        var html = StoryEpicLinkifier.Linkify("<p>Story 9.9 of Epic 99.</p>", Model(), "");

        Assert.DoesNotContain("<a", html);
    }

    [Fact]
    public void Linkify_NeverRewritesInsideProtectedSpans()
    {
        const string input =
            "<a href=\"x.html\">Story 1.1</a> <code>create-story Story 1.1</code> " +
            "<pre class=\"mermaid\">Epic 1</pre> <svg viewBox=\"0 0 1 1\"><title>Story 1.1</title></svg>";

        Assert.Equal(input, StoryEpicLinkifier.Linkify(input, Model(), ""));
    }

    [Fact]
    public void Linkify_SkipsThePagesOwnStoryAndEpic()
    {
        var html = StoryEpicLinkifier.Linkify("<p>Story 1.1 within Epic 12.</p>", Model(), "",
            skipStoryId: "1.1", skipEpicNumber: 12);

        Assert.DoesNotContain("<a", html);
    }

    [Fact]
    public void Linkify_IsCaseSensitive_SoProseAndCommandsStayPlain()
    {
        var html = StoryEpicLinkifier.Linkify("<p>the story 1.1 work in epic 1</p>", Model(), "");

        Assert.DoesNotContain("<a", html);
    }

    [Fact]
    public void Linkify_ReturnsInputUnchanged_WhenPlanIsEmpty()
    {
        var empty = new EpicsModel { OverviewHtml = "", RequirementsInventoryHtml = "", Epics = Array.Empty<EpicInfo>() };
        const string input = "<p>Story 1.1 has nowhere to go.</p>";

        Assert.Equal(input, StoryEpicLinkifier.Linkify(input, empty, ""));
    }

    [Fact]
    public void Linkify_NeverRewritesInsideHeadTitleOrMeta()
    {
        const string input =
            "<head><title>Epic 1 Retrospective</title>" +
            "<meta name=\"description\" content=\"Reviewing Epic 1 and Story 1.1\"></head>";

        Assert.Equal(input, StoryEpicLinkifier.Linkify(input, Model(), ""));
    }

    [Fact]
    public void Linkify_NeverRewritesInsideScriptOrStyle()
    {
        const string input =
            "<script>var s = \"Story 1.1\";</script><style>/* Epic 1 */</style>";

        Assert.Equal(input, StoryEpicLinkifier.Linkify(input, Model(), ""));
    }

    [Fact]
    public void Linkify_DoesNotCrashOnHugeDigitRuns()
    {
        var html = StoryEpicLinkifier.Linkify("<p>Story 99999999999.1 and Epic 88888888888.</p>", Model(), "");

        Assert.DoesNotContain("<a", html);
    }

    [Fact]
    public void Linkify_LeavesLeadingZeroMentionsPlain()
    {
        // "Story 1.05" must not be normalized to a link to the existing story 1.5.
        var html = StoryEpicLinkifier.Linkify("<p>Story 1.05 and Epic 01.</p>", Model(), "");

        Assert.DoesNotContain("<a", html);
    }

    [Fact]
    public void Linkify_LeavesThreePartIdsWhollyPlain()
    {
        // "Story 1.10.2" must not partially match as "Story 1.10".
        var html = StoryEpicLinkifier.Linkify("<p>See Story 1.10.2 for detail.</p>", Model(), "");

        Assert.DoesNotContain("<a", html);
        Assert.Contains("Story 1.10.2", html);
    }

    [Fact]
    public void Linkify_LinksMentionsWrappedAcrossASourceLine()
    {
        var html = StoryEpicLinkifier.Linkify("<p>See Story\n1.1 below.</p>", Model(), "");

        Assert.Contains("href=\"epics/story-1-1.html\"", html);
    }

    [Fact]
    public void Linkify_NeverRewritesInsideAnAttributeValueOnANonAnchorTag()
    {
        // Regression: a fallback sprint board card with no href renders as
        // <div data-tip="Epic 1: ...\nStory 1.1: ...">, not an <a>. A mention inside that attribute
        // value must never be rewritten — doing so injects a raw <a>...</a> into the attribute and
        // corrupts the tag (the injected </a>'s '>' closes the div early).
        const string input = "<div class=\"sprint-card\" data-tip=\"Epic 1: Foo\nStory 1.1: Bar\">text</div>";

        Assert.Equal(input, StoryEpicLinkifier.Linkify(input, Model(), ""));
    }

    [Fact]
    public void Linkify_StillLinksAnAnchorAdjacentToOtherTags()
    {
        // Guards the alternation order: the specific <a>...</a> alternative must still win over the
        // generic single-tag catch-all when an anchor sits next to other markup.
        const string input = "<span>see</span> <a href=\"x.html\">Story 1.1</a> <span>Epic 1</span>";

        var html = StoryEpicLinkifier.Linkify(input, Model(), "");

        Assert.Contains("<a href=\"x.html\">Story 1.1</a>", html);
        Assert.Contains("<a class=\"epic-ref\" href=\"epics/epic-1.html\">Epic 1</a>", html);
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
