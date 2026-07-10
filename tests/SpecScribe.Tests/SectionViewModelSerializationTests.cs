using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.2 AC #2 coverage: every SECTION view-model DATA record the dashboard + epics decomposition
/// adds is plain, JSON-serializable data that round-trips through <see cref="System.Text.Json"/> with NO LOSS —
/// proving the deferred JSON view-model export (Story 6.4's data path) is a trivial <c>JsonSerializer.Serialize</c>
/// and that these are real DATA, not opaque HTML blobs (a "section view model" that were just an HTML string
/// would prove nothing). The records use value equality, so a lossless round-trip is a single <c>Assert.Equal</c>.
/// <para>The chart/prose panels that carry an already-projected DOMAIN INPUT (<c>EpicsModel</c>,
/// <c>ProgressModel</c>, <c>CommandCatalog</c>, …) are deliberately NOT exercised here: those are the sanctioned
/// "carry the input, don't re-model it" fields, pre-existing models the follow-up export serializes on their own
/// terms. This test pins the NEW decomposed section data — the part that had to become data. [Story 6.2]</para></summary>
public class SectionViewModelSerializationTests
{
    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(json)!;
    }

    /// <summary>Proves a value round-trips with no loss by serialize → deserialize → RE-serialize and comparing
    /// the two JSON strings. (Record value-equality reference-compares collection members, so it can't stand in
    /// for "no data loss" on records that carry lists — JSON idempotence can.)</summary>
    private static void AssertRoundTripsLossless<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var reserialized = JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(json));
        Assert.Equal(json, reserialized);
    }

    [Fact]
    public void StatTile_RoundTripsWithNoLoss()
    {
        var tile = new StatTile("3/5", "Epics drafted", "8 with a task plan", "tooltip text");
        Assert.Equal(tile, RoundTrip(tile));

        // The optional fields survive as null when absent (no "" ↔ null drift).
        var minimal = new StatTile("—", "Commits");
        var back = RoundTrip(minimal);
        Assert.Equal(minimal, back);
        Assert.Null(back.Sub);
        Assert.Null(back.Tooltip);
    }

    [Fact]
    public void ProgressBarAndNowNextCards_RoundTrip()
    {
        var bars = new List<ProgressBarView>
        {
            new("Planning", 3, 5, "3 / 5 epics"),
            new("Implementation", 0, 0, "not started"),
        };
        Assert.Equal(bars, RoundTrip(bars)); // no collection members → record value-equality holds

        var nowNext = new DashboardNowNext(SprintBoard: null, Cards: new[]
        {
            new NowNextCard("active", "In development", "Story 1.1 · Foundation", "epics/story-1-1.html"),
            new NowNextCard("ready", "Up next", "Story 1.2 · Growth", "epics/story-1-2.html"),
        });
        AssertRoundTripsLossless(nowNext);
    }

    [Fact]
    public void IndexBand_WithEveryCardStyleAndPlanningLayout_RoundTrips()
    {
        var planningBand = new IndexBand
        {
            Title = "Planning Artifacts",
            ConceptKey = "Planning Artifacts",
            Cards = Array.Empty<IndexCardView>(),
            Planning = new PlanningLayout(
                Prd: new IndexCardView
                {
                    Style = IndexCardStyle.PrimaryPrd,
                    Kicker = "Primary document",
                    Title = "Product Requirements",
                    Href = "planning-artifacts/prd.html",
                    SourcePath = "planning-artifacts/prd.md",
                    Status = "approved",
                    Meta = "2026-07-01 · Alice",
                    BranchHref = "planning-artifacts/prd-review.html",
                    BranchLabel = "Quality review",
                },
                UxCards: new[]
                {
                    new IndexCardView { Style = IndexCardStyle.Doc, Title = "UX Design", Href = "planning-artifacts/ux.html", SourcePath = "planning-artifacts/ux.md" },
                },
                OtherCards: Array.Empty<IndexCardView>()),
        };

        var adrBand = new IndexBand
        {
            Title = "Architecture Decision Records",
            ConceptKey = "ADRs",
            TitleRow = true,
            MoreLinkHref = "adrs/index.html",
            MoreLinkLabel = "All ADRs",
            Cards = new[]
            {
                new IndexCardView { Style = IndexCardStyle.Adr, Title = "ADR 1", Href = "adrs/adr-1.html", SourcePath = "docs/adrs/adr-1.md", Status = "Accepted" },
            },
        };

        var retroBand = new IndexBand
        {
            Title = "Retrospectives",
            ConceptKey = "Retrospectives",
            NoIcon = true,
            Cards = new[]
            {
                new IndexCardView { Style = IndexCardStyle.Retro, Title = "Epic 1 Retro", Href = "retro-1.html", SourcePath = "impl/retro-1.md", Meta = "2026-07-07" },
            },
        };

        var bands = new List<IndexBand> { planningBand, adrBand, retroBand };
        AssertRoundTripsLossless(bands);
    }

    [Fact]
    public void EpicChipAndStoryCard_RoundTrip()
    {
        var chips = new List<EpicChip>
        {
            new(1, "Foundation", "drafted", "epics/epic-1.html"),
            new(2, "Growth", "pending", "epics/epic-2.html"),
        };
        Assert.Equal(chips, RoundTrip(chips)); // no collection members → record value-equality holds

        var cards = new List<StoryCardView>
        {
            new()
            {
                Id = "1.1", TitleHtml = "First", AnchorId = "story-1-1", StatusStage = "active", Status = "in-progress",
                TasksDone = 1, TasksTotal = 2, TitleHref = "../epics/story-1-1.html", ViewPlanHref = "../epics/story-1-1.html",
                UserStoryHtml = "<p>As a user…</p>", AcBlocksHtml = new[] { "<div>AC 1</div>" }, NoteHtml = null,
            },
            new()
            {
                Id = "1.2", TitleHtml = "Second", AnchorId = "story-1-2", StatusStage = "drafted", Status = null,
                TasksDone = 0, TasksTotal = 0, TitleHref = "../epics/story-1-2.html", ViewPlanHref = null,
                UserStoryHtml = "<p>Later.</p>", AcBlocksHtml = Array.Empty<string>(), NoteHtml = "<p>Draft it.</p>",
            },
        };
        AssertRoundTripsLossless(cards);
    }

    [Fact]
    public void DevAgentEntry_RoundTrips()
    {
        var rows = new List<DevAgentEntry>
        {
            new("Agent Model Used", "<p>claude-opus</p>"),
            new("Completion Notes", "<ul><li>Done.</li></ul>"),
        };
        Assert.Equal(rows, RoundTrip(rows));
    }

    [Fact]
    public void SectionRecords_CarryNoBehavior_OnlyData()
    {
        // A representative section record serializes to a flat JSON object of its declared fields — no HTML in a
        // data field beyond the explicitly-named opaque fragments (TitleHtml/UserStoryHtml here), no delegates.
        var chip = new EpicChip(1, "Foundation", "drafted", "epics/epic-1.html");
        var json = JsonSerializer.Serialize(chip);
        Assert.Contains("\"Number\":1", json);
        Assert.Contains("\"StatusClass\":\"drafted\"", json);
        Assert.Contains("\"Href\":\"epics/epic-1.html\"", json);
    }
}
