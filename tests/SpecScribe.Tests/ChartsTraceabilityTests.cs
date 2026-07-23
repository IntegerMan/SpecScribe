namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 21.1's traceability matrix builders: <see cref="Charts.TraceabilityMatrix"/>
/// (the requirement x covering-epic grid), <see cref="Charts.TraceabilityLegend"/>, and
/// <see cref="Charts.TraceabilityStrip"/> (the dashboard/requirements.html teaser). Fixture is real markdown
/// parsed through <see cref="EpicsParser"/>/<see cref="RequirementsParser"/> (mirrors <c>ChartsTests</c>'
/// <c>FlowFixture</c>) so coverage/status derivation is exercised end to end, not hand-rolled.</summary>
public class ChartsTraceabilityTests
{
    private const string Md = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: Done requirement covered by epic 1
        FR2: Requirement covered by both epics
        FR3: Requirement naming a phantom epic

        ### NonFunctional Requirements

        NFR1: Deferred requirement

        ### UX Design Requirements

        UX-DR1: Unmapped design requirement

        ### FR Coverage Map

        FR1: Epic 1 - core
        FR2: Epics 1 & 2 - spans
        FR3: Epic 99 - phantom, since removed
        NFR1: Deferred - shelved for later

        ## Epic List

        ### Epic 1: Foundation

        Base.

        ### Epic 2: Expansion

        More.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want scaffolding.

        ### Story 1.2: Widen

        As a dev, I want more.

        ## Epic 2: Expansion

        ### Story 2.1: Grow

        As a dev, I want growth.
        """;

    private static (RequirementsModel Reqs, EpicsModel Epics) Fixture()
    {
        var epics = EpicsParser.Parse(Md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        return (RequirementsParser.Parse(Md, epics, progress), epics);
    }

    // ---- Cells (AC #1) ----

    [Fact]
    public void TraceabilityMatrix_CoveredCell_ShowsMarkerAndStoryRollupTooltip()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("<td class=\"trace-cell covered\">", html);
        // Never color-only: an icon-bearing link plus an sr-only accessible name.
        Assert.Contains("Covered by Epic 1</span>", html);
        // Rich tooltip: requirement/epic pair + the epic-level honesty framing + the covering epic's own stories.
        // The middle dot is HTML-escaped by PathUtil.Html to its numeric entity inside the attribute value.
        Assert.Contains("FR1 &#183; Epic 1", html);
        Assert.Contains("Stories in Epic 1 (epic-level coverage)", html);
        Assert.Contains("1.1 (Drafted)", html);
        Assert.Contains("1.2 (Drafted)", html);
    }

    [Fact]
    public void TraceabilityMatrix_RowHeader_CarriesARichTooltipNamingTheRequirement()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("<th scope=\"row\" class=\"trace-row-head\"><a class=\"js-tip\" href=\"requirements/fr1.html\" data-tip=\"FR1 &#183; Functional", html);
        Assert.Contains("Done requirement covered by epic 1", html);
    }

    [Fact]
    public void TraceabilityMatrix_NonCoveringCell_RendersBlankNeutralCell()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        // FR1 only covers Epic 1 — its Epic 2 cell must be present but blank, never flagged as a gap.
        Assert.Contains("<td class=\"trace-cell\"></td>", html);
    }

    [Fact]
    public void TraceabilityMatrix_MultiEpicRequirement_CoversEveryNamedEpicColumn()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("FR2 &#183; Epic 1", html);
        Assert.Contains("FR2 &#183; Epic 2", html);
    }

    [Fact]
    public void TraceabilityMatrix_PhantomCoveringEpicNumber_NeverGetsAColumnOrBrokenLink()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        // FR3 names "Epic 99", which doesn't resolve in the epic list — never a broken link/column.
        Assert.DoesNotContain("epic-99.html", html);
        Assert.DoesNotContain(">Epic 99<", html);
    }

    // ---- Row-level state (AC #1) ----

    [Fact]
    public void TraceabilityMatrix_DeferredRow_CarriesDeferredTreatmentAndBadge()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("<tr class=\"deferred\">", html);
        Assert.Contains("status-badge deferred", html);
        Assert.Contains(">Deferred</span>", html);
    }

    [Fact]
    public void TraceabilityMatrix_FullyUnmappedRow_CarriesNotYetMappedTreatmentAndBadge()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("<tr class=\"unmapped\">", html);
        Assert.Contains(">Not yet mapped</span>", html);
    }

    // ---- Links, prefix, and semantics ----

    [Fact]
    public void TraceabilityMatrix_ColumnHeadersLinkToEpicPages_RowHeadersLinkToRequirementPages()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("href=\"epics/epic-1.html\"", html);
        Assert.Contains("href=\"epics/epic-2.html\"", html);
        Assert.Contains("href=\"requirements/fr1.html\"", html);
        Assert.Contains("href=\"requirements/nfr1.html\"", html);
        Assert.Contains("href=\"requirements/ux-dr1.html\"", html);
    }

    [Fact]
    public void TraceabilityMatrix_PrefixesEveryLink()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: "../");

        Assert.Contains("href=\"../epics/epic-1.html\"", html);
        Assert.Contains("href=\"../requirements/fr1.html\"", html);
    }

    [Fact]
    public void TraceabilityMatrix_IsARealTable_WithCaptionAndScopedHeaders()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("<table class=\"trace-matrix\">", html);
        Assert.Contains("<caption", html);
        Assert.Contains("th scope=\"col\"", html);
        Assert.Contains("th scope=\"row\"", html);
    }

    [Fact]
    public void TraceabilityMatrix_WrapsInAScrollContainer_NoHorizontalPageOverflow()
    {
        var (reqs, epics) = Fixture();
        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("class=\"table-scroll trace-matrix-wrap\"", html);
    }

    // ---- Degrade honestly (AC #2) ----

    [Fact]
    public void TraceabilityMatrix_EmptyEverything_RendersHonestNoteNotAnEmptyGrid()
    {
        var epics = new EpicsModel { OverviewHtml = "", RequirementsInventoryHtml = "", Epics = Array.Empty<EpicInfo>() };
        var reqs = RequirementsModel.Empty;

        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public void TraceabilityMatrix_NoRequirementNamesAResolvableCoveringEpic_RendersHonestNote()
    {
        // Every requirement is deferred or unmapped — CoverageEpicNumbers is empty for both, so there is
        // nothing to chart even though the requirement list itself is non-empty.
        const string md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            FR1: Deferred requirement

            ### FR Coverage Map

            FR1: Deferred - shelved
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);

        var html = Charts.TraceabilityMatrix(reqs, epics, prefix: string.Empty);

        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<table", html);
    }

    // ---- WhyText ----

    [Fact]
    public void WhyText_RequirementTraceability_ReturnsNonEmptyFrameworkNeutralSentence()
    {
        var why = Charts.WhyText(Charts.ChartMetric.RequirementTraceability);

        Assert.False(string.IsNullOrWhiteSpace(why));
        Assert.DoesNotContain("BMad", why);
        Assert.DoesNotContain("epics.md", why);
    }

    // ---- Legend + strip (Task 2 legend; Task 4 strip) ----

    private static ProjectCounts.RequirementSatisfaction Sat(int done, int active, int ready, int planned, int unmapped, int deferred) => new()
    {
        Done = done,
        Active = active,
        Ready = ready,
        Planned = planned,
        Unmapped = unmapped,
        Deferred = deferred,
    };

    [Fact]
    public void TraceabilityLegend_EmptySatisfaction_RendersNothing()
        => Assert.Equal(string.Empty, Charts.TraceabilityLegend(ProjectCounts.RequirementSatisfaction.Empty));

    [Fact]
    public void TraceabilityLegend_CollapsesSatisfiedAndInFlightIntoOneCoveredCount()
    {
        // Satisfied (Done=1) + In flight (Active=1, Ready=0, Planned=1) = 3 Covered.
        var sat = Sat(done: 1, active: 1, ready: 0, planned: 1, unmapped: 2, deferred: 3);

        var html = Charts.TraceabilityLegend(sat);

        Assert.Contains("Covered", html);
        Assert.Contains("<span class=\"satisfaction-chip-count\">3</span>", html);
        Assert.Contains("Deferred on purpose", html);
        Assert.Contains("Not yet mapped", html);
    }

    [Fact]
    public void TraceabilityStrip_EmptySatisfaction_RendersNothing()
        => Assert.Equal(string.Empty, Charts.TraceabilityStrip(ProjectCounts.RequirementSatisfaction.Empty, "traceability.html"));

    [Fact]
    public void TraceabilityStrip_LinksToTheDedicatedMatrixPage()
    {
        var sat = Sat(done: 1, active: 0, ready: 0, planned: 0, unmapped: 0, deferred: 0);

        var html = Charts.TraceabilityStrip(sat, "traceability.html");

        Assert.Contains("href=\"traceability.html\"", html);
        Assert.Contains("View full traceability matrix", html);
    }

    [Fact]
    public void TraceabilityStrip_AndLegend_NeverDisagree_SameCountsSameMarkup()
    {
        var sat = Sat(done: 2, active: 1, ready: 1, planned: 1, unmapped: 4, deferred: 5);

        var legend = Charts.TraceabilityLegend(sat);
        var strip = Charts.TraceabilityStrip(sat, "traceability.html");

        // Both derive from the SAME ledger via the same shared chip renderer — the chip block itself must be
        // byte-identical between the dedicated page's legend and the teaser strip.
        Assert.Contains("<span class=\"satisfaction-chip-count\">4</span>", legend); // Satisfied+InFlight = 2+1+1+1
        Assert.Contains("<span class=\"satisfaction-chip-count\">4</span>", strip);
        Assert.Contains("<span class=\"satisfaction-chip-count\">5</span>", legend); // Deferred
        Assert.Contains("<span class=\"satisfaction-chip-count\">5</span>", strip);
        Assert.Contains("<span class=\"satisfaction-chip-count\">4</span>", legend); // Unmapped
    }

    [Fact]
    public void TraceabilityStrip_NeverColorOnly_EveryChipCarriesAWordAndAnIcon()
    {
        var sat = Sat(done: 1, active: 0, ready: 0, planned: 0, unmapped: 1, deferred: 1);

        var html = Charts.TraceabilityStrip(sat, "traceability.html");

        Assert.Contains("satisfaction-chip-word\">Covered</span>", html);
        Assert.Contains("satisfaction-chip-word\">Deferred on purpose</span>", html);
        Assert.Contains("satisfaction-chip-word\">Not yet mapped</span>", html);
        Assert.Contains("<svg class=\"ss-icon\"", html);
    }
}
