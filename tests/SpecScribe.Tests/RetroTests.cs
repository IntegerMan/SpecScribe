using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the retrospective-notes artifact class (Story 2.3 retro pages): filename discovery,
/// meta extraction + action-items badging in <see cref="RetroParser"/>, and the dedicated
/// <see cref="RetroTemplater"/> page (styled header, epic link, participant pills, single main).</summary>
public class RetroTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("specscribe-retro-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private const string RetroMd = """
        # Epic 1 Retrospective: Foundation

        **Date:** 2026-07-07
        **Participants:** Matt (Lead), Amelia (Dev), Alice (PO)

        ## What Went Well

        - Seams held across the epic.

        ## Action Items

        | # | Action | Owner | Status |
        |---|--------|-------|--------|
        | 1 | Route deferred tech debt | Dana | open |
        | 2 | Schedule retros promptly | Amelia | done |
        """;

    private RetroModel Parse()
    {
        var path = Path.Combine(_dir, "epic-1-retro-2026-07-07.md");
        File.WriteAllText(path, RetroMd);
        return RetroParser.Parse(path,
            "implementation-artifacts/epic-1-retro-2026-07-07.md",
            "implementation-artifacts/epic-1-retro-2026-07-07.html");
    }

    [Fact]
    public void IsRetroFile_MatchesEpicRetroNamesOnly()
    {
        Assert.True(RetroParser.IsRetroFile("epic-1-retro-2026-07-07.md"));
        Assert.Equal(new[] { 1 }, RetroParser.EpicNumbersOf("epic-1-retro-2026-07-07.md").ToArray());
        Assert.False(RetroParser.IsRetroFile("1-1-some-story.md"));
        Assert.False(RetroParser.IsRetroFile("epics.md"));
    }

    /// <summary>A JOINT retrospective covers several epics, and every one of them must be attributed — the
    /// original `^epic-(\d+)-retro\b` matched `epic-19-21-retro-*` not at all, so the file was never ingested
    /// and BOTH epics silently lost their "Done" status. [spec-multi-epic-retro-attribution]</summary>
    [Theory]
    // Single epic — the pre-existing shape, which must keep working untouched.
    [InlineData("epic-1-retro-2026-07-07.md", new[] { 1 })]
    // The real joint retro in this repo, plus the other spellings a user might reasonably reach for.
    [InlineData("epic-19-21-retro-2026-07-23.md", new[] { 19, 21 })]
    [InlineData("epic-19-20-21-retro-2026-07-23.md", new[] { 19, 20, 21 })]
    [InlineData("epics-19-21-retro-2026-07-23.md", new[] { 19, 21 })]
    [InlineData("epic-19-and-21-retro-2026-07-23.md", new[] { 19, 21 })]
    [InlineData("epic-19+21-retro-2026-07-23.md", new[] { 19, 21 })]
    // Out of order in the name, de-duplicated, and ascending on the way out.
    [InlineData("epic-21-19-retro-2026-07-23.md", new[] { 19, 21 })]
    [InlineData("epic-19-19-retro-2026-07-23.md", new[] { 19 })]
    public void EpicNumbersOf_CoversEveryEpicNamed(string fileName, int[] expected)
    {
        Assert.True(RetroParser.IsRetroFile(fileName));
        Assert.Equal(expected, RetroParser.EpicNumbersOf(fileName).ToArray());
    }

    /// <summary>The number run is anchored by the literal `-retro`; without that anchor a greedy match would
    /// read the trailing DATE as epic numbers (1, 2026, 7, 7). This pins the anchor.</summary>
    [Fact]
    public void EpicNumbersOf_DoesNotAbsorbTheTrailingDate()
    {
        Assert.Equal(new[] { 1 }, RetroParser.EpicNumbersOf("epic-1-retro-2026-07-07.md").ToArray());
        Assert.Equal(new[] { 19, 21 }, RetroParser.EpicNumbersOf("epic-19-21-retro-2026-07-23.md").ToArray());
    }

    [Fact]
    public void EpicNumbersOf_IsEmptyForNonRetroNames()
    {
        Assert.Empty(RetroParser.EpicNumbersOf("1-1-some-story.md"));
        Assert.Empty(RetroParser.EpicNumbersOf("epics.md"));
    }

    /// <summary>Names that must NOT be read as retros. Each was a real false-accept or silent-drop found in
    /// review; the shared cure is bounding every epic token to 1-3 ASCII digits. Crucially each is still
    /// REPORTED (see below) rather than consumed and attributed to nothing.
    /// [spec-multi-epic-retro-attribution review]</summary>
    [Theory]
    // Date BEFORE `-retro`: the run would otherwise capture 1/2026/07/07 and mark the real Epic 7 retro'd.
    [InlineData("epic-1-2026-07-07-retro.md")]
    // Out of int range: would otherwise match, parse to nothing, and be consumed while attributing to no epic.
    [InlineData("epic-99999999999-retro-2026-01-01.md")]
    [InlineData("epic-19-99999999999-retro-2026-01-01.md")]
    public void IsRetroFile_RejectsNamesThatWouldMisattribute(string fileName)
    {
        Assert.False(RetroParser.IsRetroFile(fileName));
        Assert.Empty(RetroParser.EpicNumbersOf(fileName));
        // Rejected is not the same as ignored — every one of these is surfaced to the user.
        Assert.True(RetroParser.LooksLikeUnrecognizedRetro(fileName));
    }

    /// <summary>`IgnoreCase` alone folds using the CURRENT culture, so under tr-TR/az the dotted/dotless `I`
    /// makes `EPIC-…` fail to match `epic` — a whole retro would go invisible on a Turkish-locale CI box, and
    /// the safety net would miss it too because it shared the flag.</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void IsRetroFile_IsCultureInvariant(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.True(RetroParser.IsRetroFile("EPIC-19-21-RETRO-2026-07-23.md"));
            Assert.Equal(new[] { 19, 21 }, RetroParser.EpicNumbersOf("EPIC-19-21-RETRO-2026-07-23.md").ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>Unicode decimal digits satisfy `\d` but not `int.TryParse`, which would leave the file
    /// recognized, consumed, and attributed to nothing. ASCII-only matching keeps recognition and parsing in
    /// agreement.</summary>
    [Fact]
    public void IsRetroFile_IgnoresNonAsciiDigits()
    {
        Assert.False(RetroParser.IsRetroFile("epic-١٩-retro-2026-07-23.md"));
    }

    /// <summary>The covered set is normalized by the TYPE, not merely by the parser, so a hand-built model
    /// cannot silently corrupt `PrimaryEpicNumber`, the adapter sort, or the retro pager.</summary>
    [Fact]
    public void EpicNumbers_AreNormalizedOnConstruction()
    {
        var retro = new RetroModel
        {
            EpicNumbers = new[] { 21, 19, 21 },
            Title = "Joint", Participants = Array.Empty<string>(), BodyHtml = string.Empty,
            SourceRelativePath = "a.md", OutputRelativePath = "a.html",
        };

        Assert.Equal(new[] { 19, 21 }, retro.EpicNumbers.ToArray());
        Assert.Equal(19, retro.PrimaryEpicNumber);
    }

    /// <summary>An `epic…retro…` name we can't parse must be REPORTED, never silently dropped — a dropped retro
    /// also drops its epics' "Done" status. Unrelated files that merely mention a retro must not trip it.</summary>
    [Fact]
    public void LooksLikeUnrecognizedRetro_FlagsOnlyUnparseableEpicRetroNames()
    {
        Assert.True(RetroParser.LooksLikeUnrecognizedRetro("epic-1-retrospective-2026-07-07.md"));
        Assert.True(RetroParser.LooksLikeUnrecognizedRetro("epic-19_21-retro-2026-07-23.md"));

        // Recognized names are not "unrecognized".
        Assert.False(RetroParser.LooksLikeUnrecognizedRetro("epic-1-retro-2026-07-07.md"));
        Assert.False(RetroParser.LooksLikeUnrecognizedRetro("epic-19-21-retro-2026-07-23.md"));

        // Not an epic-retro file at all — a spec or process doc that merely discusses retros must stay silent,
        // or the diagnostics page fills with noise the user cannot opt out of.
        Assert.False(RetroParser.LooksLikeUnrecognizedRetro("spec-sunburst-retro-review-and-done-story-actions.md"));
        Assert.False(RetroParser.LooksLikeUnrecognizedRetro("epics.md"));
        Assert.False(RetroParser.LooksLikeUnrecognizedRetro("epics-retro-process.md"));
        Assert.False(RetroParser.LooksLikeUnrecognizedRetro("epics-overview-retrospective-plan.md"));
    }

    [Fact]
    public void Parse_ExtractsMetaBadgesActionItemsAndStripsMetaLines()
    {
        var retro = Parse();

        Assert.Equal(new[] { 1 }, retro.EpicNumbers.ToArray());
        Assert.Equal("Epic 1 Retrospective: Foundation", retro.Title);
        Assert.Equal("2026-07-07", retro.DateText);
        Assert.Equal(new[] { "Matt (Lead)", "Amelia (Dev)", "Alice (PO)" }, retro.Participants.ToArray());

        // The leading title h1 is stripped from the body (the styled header already carries the title).
        Assert.DoesNotContain("<h1", retro.BodyHtml);
        // The date/participants lines are lifted out of the narrative (they move to the styled header).
        Assert.DoesNotContain("<strong>Date:</strong>", retro.BodyHtml);
        Assert.DoesNotContain("<strong>Participants:</strong>", retro.BodyHtml);
        // The Action Items table's Status cells are badged (open → ready, done → done); no bare status cells.
        Assert.Contains("status-badge ready js-tip", retro.BodyHtml);
        Assert.Contains("status-badge done js-tip", retro.BodyHtml);
        Assert.DoesNotContain("<td>open</td>", retro.BodyHtml);
        // The Owner column is dropped entirely — header + every owner cell (LLM personas, not real assignees).
        Assert.DoesNotContain("Owner", retro.BodyHtml);
        Assert.DoesNotContain("Dana", retro.BodyHtml);
        Assert.DoesNotContain("Amelia", retro.BodyHtml);
        // The Action text and remaining columns survive.
        Assert.Contains("Route deferred tech debt", retro.BodyHtml);
    }

    [Fact]
    public void RenderPage_StyledHeaderEpicLinkNoPersonasAndSingleMain()
    {
        var retro = Parse();
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 1, Title = "Foundation", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice, Stories = Array.Empty<StoryInfo>(),
                },
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderPage(retro, epics, nav);

        Assert.Contains("class=\"story-kicker\">Epic 1 Retrospective</div>", html);
        // The h1 drops the redundant "Epic 1 Retrospective:" prefix (the kicker above already carries it).
        Assert.Contains("<h1>Foundation</h1>", html);
        Assert.DoesNotContain("<h1>Epic 1 Retrospective", html);
        // The retro date now routes through the single PortalDates token (Story 10.4): bare ISO → "Jul 7, 2026".
        Assert.Contains("<span class=\"pill\">Jul 7, 2026</span>", html);
        // Personas (LLM-generated retro participants) are NOT rendered — noise once the doc exists. [polish #7]
        Assert.DoesNotContain("retro-personas", html);
        Assert.DoesNotContain("persona-pill", html);
        Assert.DoesNotContain(">Personas<", html);
        // Epic link resolves at the retro page's depth-1 prefix.
        Assert.Contains("href=\"../epics/epic-1.html\">Epic 1 &rarr;</a>", html);
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderPage_ListsEpicStoriesAsSprintCards()
    {
        var retro = Parse();
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 1, Title = "Foundation", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                    Stories = new[]
                    {
                        new StoryInfo { Id = "1.1", EpicNumber = 1, Title = "Nav Foundation", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = "epics/story-1-1.html", Status = "Done" },
                        new StoryInfo { Id = "1.2", EpicNumber = 1, Title = "Traceability", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = null }, // undrafted → placeholder
                    },
                },
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderPage(retro, epics, nav);

        Assert.Contains("<section class=\"retro-stories\" id=\"retro-stories\">", html);
        Assert.Contains("Stories in this Epic", html);
        Assert.Contains("class=\"retro-story-grid\">", html);
        // Stories use the shared sprint-card markup (same style as the sprint board), status color on the card.
        Assert.Contains("<a class=\"sprint-card done\" href=\"../epics/story-1-1.html\">", html);
        Assert.Contains("<span class=\"sprint-card-id\">Story 1.1</span>", html);
        Assert.Contains("<span class=\"sprint-card-title\">Nav Foundation</span>", html);
        // Undrafted story links to its placeholder path.
        Assert.Contains("href=\"../epics/story-1-2.html\"", html);
        // No longer a row layout.
        Assert.DoesNotContain("retro-story-row", html);
    }

    /// <summary>A joint retro's page must reach BOTH epics: both kicker names, both back-links, and the stories
    /// of both epics merged into the one grid. [spec-multi-epic-retro-attribution]</summary>
    [Fact]
    public void RenderPage_JointRetroLinksEveryCoveredEpicAndMergesTheirStories()
    {
        var retro = new RetroModel
        {
            EpicNumbers = new[] { 19, 21 },
            Title = "Joint Retrospective — Epic 19 + Epic 21",
            DateText = "2026-07-23",
            Participants = Array.Empty<string>(),
            BodyHtml = string.Empty,
            SourceRelativePath = "implementation-artifacts/epic-19-21-retro-2026-07-23.md",
            OutputRelativePath = "implementation-artifacts/epic-19-21-retro-2026-07-23.html",
        };
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 19, Title = "Directed Work Graph", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                    Stories = new[]
                    {
                        new StoryInfo { Id = "19.1", EpicNumber = 19, Title = "Work Graph Spike", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = "epics/story-19-1.html", Status = "Done" },
                    },
                },
                new EpicInfo
                {
                    Number = 21, Title = "Value & Correlation Insights", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                    Stories = new[]
                    {
                        new StoryInfo { Id = "21.1", EpicNumber = 21, Title = "Traceability Matrix", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = "epics/story-21-1.html", Status = "Done" },
                    },
                },
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderPage(retro, epics, nav);

        // Kicker names both epics (ampersand HTML-escaped exactly once, not double-encoded).
        Assert.Contains("<div class=\"story-kicker\">Epics 19 &amp; 21 Retrospective</div>", html);
        Assert.DoesNotContain("&amp;amp;", html);

        // A back-link per covered epic.
        Assert.Contains("href=\"../epics/epic-19.html\">Epic 19 &rarr;</a>", html);
        Assert.Contains("href=\"../epics/epic-21.html\">Epic 21 &rarr;</a>", html);

        // Both epics' stories in the one grid, under the plural heading.
        Assert.Contains("Stories in these Epics", html);
        Assert.Contains("<span class=\"sprint-card-id\">Story 19.1</span>", html);
        Assert.Contains("<span class=\"sprint-card-id\">Story 21.1</span>", html);
    }

    /// <summary>An epic named by the retro but absent from the model contributes no link and no stories, rather
    /// than throwing or emitting a dangling href.</summary>
    [Fact]
    public void RenderPage_SkipsCoveredEpicsMissingFromTheModel()
    {
        var retro = new RetroModel
        {
            EpicNumbers = new[] { 19, 99 },
            Title = "Joint Retrospective",
            DateText = "2026-07-23",
            Participants = Array.Empty<string>(),
            BodyHtml = string.Empty,
            SourceRelativePath = "implementation-artifacts/epic-19-99-retro-2026-07-23.md",
            OutputRelativePath = "implementation-artifacts/epic-19-99-retro-2026-07-23.html",
        };
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 19, Title = "Directed Work Graph", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                    Stories = Array.Empty<StoryInfo>(),
                },
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderPage(retro, epics, nav);

        Assert.Contains("href=\"../epics/epic-19.html\">Epic 19 &rarr;</a>", html);
        Assert.DoesNotContain("epics/epic-99.html", html);
        // The kicker still names every epic the retro claims to cover, present in the model or not.
        Assert.Contains("Epics 19 &amp; 99 Retrospective", html);
    }

    /// <summary>END-TO-END pin on the actual defect. Every other retro test stops at the parser or the
    /// templater, and every existing <c>HasRetrospective</c> assertion in the suite sets that flag BY HAND — so
    /// the one mechanism that turns "In review" into "Done" (adapter ingest → <c>SetRetros</c> fan-out →
    /// <c>TagEpicRetrospectives</c> → <see cref="StatusStyles.ForEpicWithRetrospective"/>) was unverified, and a
    /// regression that broke only the fan-out would have shipped green. Two all-done epics share ONE joint
    /// retro; both must read Done. [spec-multi-epic-retro-attribution review]</summary>
    [Fact]
    public void GenerateAll_JointRetro_MarksEveryCoveredEpicRetrospected()
    {
        var root = Directory.CreateTempSubdirectory("specscribe-jointretro-").FullName;
        try
        {
            var source = Path.Combine(root, "_bmad-output");
            var adrs = Path.Combine(root, "docs", "adrs");
            var site = Path.Combine(root, "site");
            Directory.CreateDirectory(Path.Combine(source, "planning-artifacts"));
            Directory.CreateDirectory(Path.Combine(source, "implementation-artifacts"));
            Directory.CreateDirectory(adrs);

            File.WriteAllText(Path.Combine(source, "planning-artifacts", "epics.md"), """
                # Epics

                ## Epic List

                ### Epic 1: Foundation

                Stand up the portal.

                ### Epic 2: Delivery

                Ship the portal.

                ## Epic 1: Foundation

                ### Story 1.1: Foundation Story

                As a maintainer, I want the foundation.

                ## Epic 2: Delivery

                ### Story 2.1: Delivery Story

                As a maintainer, I want delivery.
                """);

            foreach (var (file, id, title) in new[]
                     {
                         ("1-1-foundation.md", "1.1", "Foundation Story"),
                         ("2-1-delivery.md", "2.1", "Delivery Story"),
                     })
            {
                File.WriteAllText(Path.Combine(source, "implementation-artifacts", file), $"""
                    # Story {id}: {title}

                    Status: done

                    ## Story

                    As a maintainer, I want it.

                    ## Acceptance Criteria

                    1. It works.

                    ## Tasks / Subtasks

                    - [x] Task 1: Do it (AC: #1)
                    """);
            }

            // ONE retro covering BOTH epics — the shape that was previously not recognized at all.
            File.WriteAllText(Path.Combine(source, "implementation-artifacts", "epic-1-2-retro-2026-07-20.md"), """
                # Joint Retrospective — Epic 1 + Epic 2

                **Date:** 2026-07-20
                **Participants:** Team

                Went well.
                """);

            File.WriteAllText(Path.Combine(source, "implementation-artifacts", "sprint-status.yaml"), """
                last_updated: 2026-07-20T22:00:00-04:00
                development_status:
                  epic-1: done
                  1-1-foundation: done
                  epic-2: done
                  2-1-delivery: done
                """);

            var gen = new SiteGenerator(ForgeOptions.Resolve(
                source: source, adrs: adrs, output: site, projectName: "SpecScribe", includeReadme: false));
            Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

            // BOTH epics reach the retro-gated "done" tier. Before the fix neither did: the joint retro was
            // never ingested, so both all-done epics read "In review".
            foreach (var number in new[] { 1, 2 })
            {
                var epicHtml = File.ReadAllText(Path.Combine(site, "epics", $"epic-{number}.html"));
                Assert.Contains("epic-1-2-retro-2026-07-20.html", epicHtml);
                // The epic's OWN header badge — asserted on the badge class, not on the page text: the status
                // legend renders the word "In review" on every page regardless of this epic's tier.
                Assert.Contains("<span class=\"status-badge done js-tip\"", epicHtml);
                Assert.DoesNotContain("<span class=\"status-badge review js-tip\"", epicHtml);
            }

            // And it is a first-class retro page naming both epics, not a generic document.
            var retroHtml = File.ReadAllText(Path.Combine(site, "implementation-artifacts", "epic-1-2-retro-2026-07-20.html"));
            Assert.Contains("<div class=\"story-kicker\">Epics 1 &amp; 2 Retrospective</div>", retroHtml);
            Assert.Contains("epics/epic-1.html", retroHtml);
            Assert.Contains("epics/epic-2.html", retroHtml);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void RenderIndex_ListsRetrosLinkingToTheirPages()
    {
        var retros = new[]
        {
            new RetroModel
            {
                EpicNumbers = new[] { 1 }, Title = "Epic 1 Retrospective", DateText = "2026-07-07",
                Participants = Array.Empty<string>(), BodyHtml = string.Empty,
                SourceRelativePath = "implementation-artifacts/epic-1-retro-2026-07-07.md",
                OutputRelativePath = "implementation-artifacts/epic-1-retro-2026-07-07.html",
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderIndex(retros, nav);

        Assert.Contains("<h1>Retrospectives</h1>", html);
        Assert.Contains("href=\"implementation-artifacts/epic-1-retro-2026-07-07.html\"", html);
        Assert.Contains("Epic 1", html);
        Assert.Contains("2026-07-07", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void ActionItems_RenderPage_ShowsItemsRetroLinkAndDetailHref()
    {
        var open = new[]
        {
            new SprintActionItem("Route deferred tech debt", "open", 1, "Dana"),
            new SprintActionItem("Schedule retros promptly", "in-progress", 1, "Amelia"),
        };
        var map = new Dictionary<int, string> { [1] = "implementation-artifacts/epic-1-retro-2026-07-07.html" };
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, map, commands, nav);

        Assert.Contains("<h1>Open Action Items", html);
        Assert.Contains("class=\"status-legend\"", html);
        Assert.Contains("Route deferred tech debt", html);
        // Owners are NOT shown — they're LLM-generated retro personas, not real assignees. [polish #7]
        Assert.DoesNotContain(">Dana</span>", html);
        Assert.DoesNotContain(">Amelia</span>", html);
        // Provenance lives on the group heading (Story 9.6) — linked to the epic's retro page.
        Assert.Contains("class=\"action-items-group\"", html);
        Assert.Contains("href=\"implementation-artifacts/epic-1-retro-2026-07-07.html\">From the Epic 1 retrospective", html);
        // Story 9.11 + code review 9.10: Resolve-with-AI on detail page; list is scan + View detail only.
        Assert.Contains("href=\"follow-ups/action-", html);
        Assert.Contains("class=\"followup-row-primary\"", html);
        Assert.DoesNotContain("Resolve with AI on the detail page", html);
        Assert.DoesNotContain("followup-row-detail", html);
        Assert.DoesNotContain("<span class=\"cmd-text\">Resolve with AI</span>", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));

        // No quick-dev command exposed → still no resolve chrome on the list.
        var noCmd = ActionItemsTemplater.RenderPage(open, map, CommandCatalog.Empty, nav);
        Assert.DoesNotContain("Resolve with AI", noCmd);
    }

    [Fact]
    public void ActionItems_RenderPage_WideWrapperAndDeferredTeaserOnlyForDebtItems()
    {
        var open = new[]
        {
            new SprintActionItem("Route deferred tech debt into the backlog", "open", 1, "Dana"),
            new SprintActionItem("Schedule retros promptly", "open", 1, "Amelia"),
        };
        var map = new Dictionary<int, string> { [1] = "implementation-artifacts/epic-1-retro-2026-07-07.html" };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, map, CommandCatalog.Empty, nav, deferredWorkHref: "deferred-work.html");

        // Wider layout wrapper (not the 860 doc column).
        Assert.Contains("class=\"action-items-wrap\"", html);
        // Code review 9.10: list omits deferred teaser when detail URL exists; link lives on detail page.
        Assert.DoesNotContain("action-item-deferred", html);
        Assert.Contains("href=\"follow-ups/action-", html);
        Assert.DoesNotContain("followup-row-detail", html);

        // No deferred href → still no deferred chrome on the list (detail path owns it).
        var noHref = ActionItemsTemplater.RenderPage(open, map, CommandCatalog.Empty, nav);
        Assert.DoesNotContain("action-item-deferred", noHref);
    }

    [Fact]
    public void ActionItems_RenderPage_SummaryLinkifies_ResolveLivesOnDetail()
    {
        var open = new[]
        {
            new SprintActionItem("Fix Story 1.1 heatmap debt before Epic 2", "open", 1, "Dana"),
        };
        var map = new Dictionary<int, string> { [1] = "implementation-artifacts/epic-1-retro.html" };
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var epics = new EpicsModel
        {
            OverviewHtml = "",
            RequirementsInventoryHtml = "",
            Epics =
            [
                new EpicInfo
                {
                    Number = 1,
                    Title = "Foundation",
                    GoalHtml = "",
                    Status = EpicStatus.Drafted,
                    Section = EpicSection.VerticalSlice,
                    Stories =
                    [
                        new StoryInfo
                        {
                            Id = "1.1",
                            EpicNumber = 1,
                            Title = "Foundation",
                            UserStoryHtml = "",
                            AcBlocksHtml = Array.Empty<string>(),
                        },
                    ],
                },
            ],
        };

        var html = ActionItemsTemplater.RenderPage(open, map, commands, nav, epicsModel: epics);

        // Summary line still linkifies Story N.M mentions.
        Assert.Contains("class=\"story-ref\"", html);
        Assert.Contains(">Story 1.1</a>", html);
        Assert.Contains("href=\"follow-ups/action-", html);
        // Resolve payload is on the detail page, not individual list rows (Story 9.11) — the page-level
        // list-batch pane (spec-follow-up-list-batch-actions) is the only data-copy source on this page.
        var rowsMarkup = html[html.IndexOf("<ul class=\"followup-rows-list", StringComparison.Ordinal)..];
        Assert.DoesNotContain("data-copy=", rowsMarkup);
        Assert.Contains("data-copy=", html);
        Assert.Contains("class=\"chart-panel next-steps list-batch-actions\"", html);

        var detail = FollowUpDetailTemplater.RenderActionPage(
            open[0], FollowUpSlug.AssignActionSlugs(open)[open[0]], nav, commands, map, epicsModel: epics);
        Assert.Contains("class=\"chart-panel next-steps\"", detail);
        Assert.Contains("data-copy=\"/bmad-quick-dev Resolve this retrospective action item (Epic 1): Fix Story 1.1 heatmap debt before Epic 2\"", detail);
        Assert.DoesNotContain("data-copy=\"/bmad-quick-dev Resolve this retrospective action item (Epic 1): Fix <a", detail);
        Assert.Contains("Copies a quick-dev prompt", detail);
        Assert.Contains("Close with AI", detail);
        Assert.Contains("data-copy=\"/bmad-quick-dev Close this retrospective action item (Epic 1) in sprint-status.yaml", detail);
    }

    [Fact]
    public void RenderPage_WithPager_RoutesThroughSiteNavRenderWayfinding()
    {
        // Story 10.11: the sibling pager rides SiteNav.RenderWayfinding's coherent strip alongside the
        // breadcrumb, not the body's own header — confirms this non-PageView templater's call-site wiring.
        var retro = Parse();
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var pager = new EntityPager(
            new PagerLink("epic-1-retro.html", "Epic 1 retro"),
            new PagerLink("epic-3-retro.html", "Epic 3 retro"));

        var html = RetroTemplater.RenderPage(retro, null, nav, pager);

        Assert.Contains("<div class=\"page-wayfinding\">", html);
        var wrapperIdx = html.IndexOf("page-wayfinding", StringComparison.Ordinal);
        var crumbIdx = html.IndexOf("class=\"breadcrumb\"", StringComparison.Ordinal);
        var pagerIdx = html.IndexOf("class=\"entity-pager\"", StringComparison.Ordinal);
        Assert.True(wrapperIdx < crumbIdx && crumbIdx < pagerIdx, "expected wrapper, then breadcrumb, then pager");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
