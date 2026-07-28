using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 18.4 — the forged-ideas surface: the <c>.memlog.md</c> reader, the workspace-discovery cascade,
/// the pure verdict/title/summary derivation, the grouped list + detail templaters, the AC #6 carry gate, AC #2's
/// evidence-only forward links, and the AC #3 / NFR8 omit gate.
/// <para>IO-bearing tests use the temp-dir fixture style the generator tests already use. The four
/// <em>non-forge</em> memlog shapes in <see cref="Discover_RealNonForgeMemlogShapes_AreAllRejected"/> are the real
/// frontmatter this repo's own brief / PRD / UX / spec sessions carry — pinned here so the discovery cascade's
/// hardest case (a shared CORE tool's file that is emphatically not a forged idea) is regression-proofed in-repo
/// rather than relying on a scratch fixture that dies with the session.</para></summary>
public class IdeasTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-ideas-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // ---- Memlog: the shared file shape ---------------------------------------------------------------------

    [Fact]
    public void TrySplit_ForgeShapedMemlog_ReadsFrontmatterAndBody()
    {
        var ok = Memlog.TrySplit(
            """
            ---
            idea: A write-through cache
            goal: cut p95 latency
            updated: 2026-07-21T11:02
            status: complete
            ---

            - (decision) write-through, not write-behind
            - (lock) the cache key is (tenant, metric, day)
            """,
            out var fm, out var body);

        Assert.True(ok);
        Assert.Equal("A write-through cache", fm["idea"]);
        Assert.Equal("cut p95 latency", fm["goal"]);
        Assert.Equal("complete", fm["status"]);
        var entries = Memlog.ParseEntries(body);
        Assert.Equal(2, entries.Count);
        Assert.Equal("decision", entries[0].Type);
        Assert.Equal("write-through, not write-behind", entries[0].Text);
        // The `(tenant, metric, day)` parens inside the TEXT must not be mistaken for a second type tag.
        Assert.Equal("lock", entries[1].Type);
        Assert.Equal("the cache key is (tenant, metric, day)", entries[1].Text);
    }

    [Fact]
    public void TrySplit_ValueContainingATripleDash_DoesNotTruncateTheFrontmatter()
    {
        // memlog.py's own split() closes on the first line that is EXACTLY `---`, precisely so a free-text
        // idea:/goal: value carrying one cannot truncate the block. Mirror that, or a real session's frontmatter
        // silently loses its status/updated fields.
        var ok = Memlog.TrySplit(
            "---\nidea: pricing --- or packaging?\ngoal: decide\nstatus: complete\n---\n\n- (note) hi\n",
            out var fm, out _);

        Assert.True(ok);
        Assert.Equal("complete", fm["status"]);
    }

    [Fact]
    public void TrySplit_UnterminatedFrontmatter_Fails()
    {
        Assert.False(Memlog.TrySplit("---\nidea: half a file\n", out _, out _));
        Assert.False(Memlog.TrySplit("no frontmatter at all\n", out _, out _));
    }

    [Fact]
    public void ParseEntries_AttributionTag_KeepsTheKindNotTheAuthor()
    {
        var entries = Memlog.ParseEntries(new[] { "- (idea by user) skip the signup wall", "- (by coach) nudge" });

        Assert.Equal("idea", entries[0].Type);
        Assert.Equal("skip the signup wall", entries[0].Text);
        Assert.Null(entries[1].Type); // "(by coach)" names an author, not a kind
        Assert.Equal("nudge", entries[1].Text);
    }

    [Fact]
    public void ParseUpdated_ReadsTheDayPrefixOnly_AndTolerAtesAbsence()
    {
        Assert.Equal(new DateOnly(2026, 7, 21), Memlog.ParseUpdated("---\nupdated: 2026-07-21T11:02\n---\n"));
        Assert.Null(Memlog.ParseUpdated("---\nidea: no date here\n---\n"));
    }

    // ---- Pure derivation: the four verdicts ---------------------------------------------------------------

    [Fact]
    public void DeriveVerdict_NoCompleteStatus_IsInProgress()
    {
        var (verdict, exit) = IdeaDerivation.DeriveVerdict(Fm(("idea", "x")), NoEntries, hasForgedIdea: false);

        Assert.Equal(IdeaVerdict.InProgress, verdict);
        Assert.Equal("In progress", exit);
    }

    [Fact]
    public void DeriveVerdict_CompleteWithForgedIdea_IsHardened()
    {
        var (verdict, exit) = IdeaDerivation.DeriveVerdict(
            Fm(("idea", "x"), ("status", "complete")), NoEntries, hasForgedIdea: true);

        Assert.Equal(IdeaVerdict.Hardened, verdict);
        Assert.Equal("Hardened", exit);
    }

    [Fact]
    public void DeriveVerdict_CompleteWithKillEntryAndNoForgedIdea_IsKilled()
    {
        var (verdict, exit) = IdeaDerivation.DeriveVerdict(
            Fm(("idea", "x"), ("status", "complete")),
            Memlog.ParseEntries(new[] { "- (crack) telemetry says otherwise", "- (kill) the problem is imaginary" }),
            hasForgedIdea: false);

        Assert.Equal(IdeaVerdict.Killed, verdict);
        Assert.Equal("Killed", exit);
    }

    [Fact]
    public void DeriveVerdict_CompleteWithNoForgedIdeaAndNoKill_IsClarifiedInTheInProgressBucket()
    {
        // Owner decision D2: the forge's THIRD terminal exit folds into the in-progress bucket, but the true exit
        // word survives on the entry so the detail page can state that the session was complete, not unfinished.
        var (verdict, exit) = IdeaDerivation.DeriveVerdict(
            Fm(("idea", "x"), ("status", "complete")),
            Memlog.ParseEntries(new[] { "- (decision) usage means generated pages" }),
            hasForgedIdea: false);

        Assert.Equal(IdeaVerdict.InProgress, verdict);
        Assert.Equal("Clarified", exit);
    }

    [Fact]
    public void DeriveVerdict_ForgedIdeaWins_EvenWhenAKillEntryExists()
    {
        // A session can record a kill for a REJECTED BRANCH and still harden overall; the distilled hand-off
        // existing on disk is the stronger signal, which is why the cascade checks it first.
        var (verdict, _) = IdeaDerivation.DeriveVerdict(
            Fm(("status", "complete")),
            Memlog.ParseEntries(new[] { "- (kill) dropped the multi-tenant branch" }),
            hasForgedIdea: true);

        Assert.Equal(IdeaVerdict.Hardened, verdict);
    }

    // ---- Pure derivation: title / summary / slug ----------------------------------------------------------

    [Fact]
    public void DeriveTitle_CascadesFrontmatterThenH1ThenFolderName()
    {
        Assert.Equal("From the memlog",
            IdeaDerivation.DeriveTitle(Fm(("idea", "From the memlog")), "From the H1", "my-folder"));
        Assert.Equal("From the H1",
            IdeaDerivation.DeriveTitle(Fm(), "From the H1", "my-folder"));
        Assert.Equal("My folder",
            IdeaDerivation.DeriveTitle(Fm(), null, "my-folder"));
    }

    [Fact]
    public void DeriveSummary_CascadesGoalThenLockThenDecision_ElseNull()
    {
        var entries = Memlog.ParseEntries(new[] { "- (decision) d", "- (lock) l" });

        Assert.Equal("the goal", IdeaDerivation.DeriveSummary(Fm(("goal", "the goal")), entries));
        Assert.Equal("l", IdeaDerivation.DeriveSummary(Fm(), entries));
        Assert.Equal("d", IdeaDerivation.DeriveSummary(Fm(), Memlog.ParseEntries(new[] { "- (decision) d" })));
        Assert.Null(IdeaDerivation.DeriveSummary(Fm(), Memlog.ParseEntries(new[] { "- (note) n" })));
    }

    [Fact]
    public void Slugify_MakesAnLlmDerivedFolderNamePathSafe()
    {
        // The workspace name is LLM-derived from free user text and only CONVENTIONALLY kebab-case.
        Assert.Equal("my-idea", IdeaDerivation.Slugify("My Idea"));
        Assert.Equal("caf-au-lait", IdeaDerivation.Slugify("Café au lait"));
        Assert.Equal("a-b", IdeaDerivation.Slugify("  a...b  "));
        Assert.Equal("etc-passwd", IdeaDerivation.Slugify("../../etc/passwd"));
        Assert.Equal("idea", IdeaDerivation.Slugify("???"));
    }

    // ---- Discovery: the cascade and its false positives ---------------------------------------------------

    [Fact]
    public void Discover_RealNonForgeMemlogShapes_AreAllRejected()
    {
        // The four shapes this repo actually carries: product brief, PRD, UX and spec sessions. All four are
        // written by the SHARED CORE memlog.py, all four sit OUTSIDE forge/, none has a forge-report.html sibling,
        // and all four key their subject on `topic:` rather than `idea:`. A naive "a directory with a .memlog.md
        // is a forged idea" rule would list SpecScribe's own PRD as an idea.
        WriteMemlog(Path.Combine(Source, "planning-artifacts", "briefs", "brief-x-2026-07-05"),
            "topic: SpecScribe product brief", "updated: 2026-07-05T20:02");
        WriteMemlog(Path.Combine(Source, "planning-artifacts", "prds", "prd-x-2026-07-05"),
            "topic: SpecScribe PRD", "updated: 2026-07-05T20:28");
        WriteMemlog(Path.Combine(Source, "planning-artifacts", "ux-designs", "ux-x-2026-07-05"),
            "topic: SpecScribe Portal UX", "updated: 2026-07-05T21:26");
        WriteMemlog(Path.Combine(Source, "specs", "spec-x"),
            "topic: SpecScribe CLI-first portal", "updated: 2026-07-05T21:29");

        var diagnostics = new List<AdapterDiagnostic>();
        var model = IdeaDiscovery.Discover(Source, diagnostics);

        Assert.True(model.IsEmpty);
        // Not a problem to report, either — they are another skill's sessions, passed over in silence.
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Discover_PathRule_FindsAnInProgressSessionWithNoOtherMarker()
    {
        // The only rule that can catch a session still running: no forged-idea.md, no report, just the memlog.
        WriteMemlog(Path.Combine(Source, "forge", "still-open"), "idea: A portal surface for ideas", "goal: decide", "updated: 2026-07-20T09:14");

        var idea = Assert.Single(IdeaDiscovery.Discover(Source).Ideas);

        Assert.Equal("forge/still-open", idea.WorkspaceSourceRelative);
        Assert.Equal(IdeaVerdict.InProgress, idea.Verdict);
        Assert.Equal("In progress", idea.ExitWord);
        Assert.Equal("A portal surface for ideas", idea.Title);
        Assert.Equal("decide", idea.Summary);
        Assert.Equal(new DateOnly(2026, 7, 20), idea.Date);
    }

    [Fact]
    public void Discover_PathRule_RejectsAHandMadeForgeFolderWhoseMemlogHasNoIdeaKey()
    {
        // Rule 3, reject-only: corroboration can veto a rule-1 match but never stands alone as a positive.
        WriteMemlog(Path.Combine(Source, "forge", "someones-journal"), "topic: unrelated journal", "updated: 2026-07-25T10:00");

        Assert.True(IdeaDiscovery.Discover(Source).IsEmpty);
    }

    [Fact]
    public void Discover_MarkerRule_FindsARelocatedWorkspaceOutsideTheForgeRoot()
    {
        // An overridden forge_output_path puts the workspace anywhere; the always-rendered report is the marker.
        // Note the corroboration rule does NOT apply here — the report already proves it.
        var dir = Path.Combine(Source, "idea-lab", "relocated");
        WriteMemlog(dir, "idea: Push rendering to the edge", "updated: 2026-07-26T09:00", "status: complete");
        Report(dir, SafeReport("KILLED"));
        File.WriteAllText(Path.Combine(dir, ".memlog.md"),
            File.ReadAllText(Path.Combine(dir, ".memlog.md")).Replace("- (note) seeded", "- (kill) no problem left"));

        var idea = Assert.Single(IdeaDiscovery.Discover(Source).Ideas);

        Assert.Equal("idea-lab/relocated", idea.WorkspaceSourceRelative);
        Assert.Equal(IdeaVerdict.Killed, idea.Verdict);
    }

    [Fact]
    public void Discover_NestedRunFolderPattern_IsFoundRecursively()
    {
        // run_folder_pattern is overridable to nest {date} or other components — which is exactly why the skill's
        // own resume glob is recursive. The slug is the workspace directory's own name, not a direct child.
        var dir = Path.Combine(Source, "forge", "2026-07-24", "observability-budget");
        WriteMemlog(dir, "idea: A hard observability budget", "updated: 2026-07-24T13:30", "status: complete");
        File.WriteAllText(Path.Combine(dir, "forged-idea.md"), "# Observability budget\n\nLocked.\n");

        var idea = Assert.Single(IdeaDiscovery.Discover(Source).Ideas);

        Assert.Equal("forge/2026-07-24/observability-budget", idea.WorkspaceSourceRelative);
        Assert.Equal("observability-budget", idea.Slug);
        Assert.Equal(IdeaVerdict.Hardened, idea.Verdict);
    }

    [Fact]
    public void Discover_MalformedMemlogProvenByReport_StillListsTheIdeaAndReportsMalformed()
    {
        // [Story 18.4 review] A malformed memlog is only listed when something ELSE proves the workspace is a
        // real forge session — here, the sibling report (rule 2). Without a report this same fixture is now
        // rejected instead (see Discover_UnparseableMemlogWithNoReport_IsSkippedRatherThanListed, an owner
        // decision made during the code review, 2026-07-28).
        var dir = Path.Combine(Source, "forge", "half-written");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".memlog.md"), "---\nidea: interrupted mid-write\n");
        Report(dir, SafeReport("CLARIFIED"));

        var diagnostics = new List<AdapterDiagnostic>();
        var idea = Assert.Single(IdeaDiscovery.Discover(Source, diagnostics).Ideas);

        Assert.Equal("Half written", idea.Title); // folder name, de-kebabed
        Assert.Null(idea.Summary);
        var d = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Malformed, d.Category);
        Assert.Equal(DiagnosticAnchorRoot.Source, d.Anchor);
        Assert.Contains("listed with its folder name and no summary", d.Message);
    }

    [Fact]
    public void Discover_TwoWorkspacesSlugifyingTheSame_KeepsTheFirstInPathOrderAndReportsSkipped()
    {
        WriteMemlog(Path.Combine(Source, "forge", "a-idea"), "idea: first", "updated: 2026-07-20T09:00");
        WriteMemlog(Path.Combine(Source, "forge", "A Idea"), "idea: second", "updated: 2026-07-21T09:00");

        var diagnostics = new List<AdapterDiagnostic>();
        var idea = Assert.Single(IdeaDiscovery.Discover(Source, diagnostics).Ideas);

        // Ordinal path order: "A Idea" sorts before "a-idea" (uppercase 'A' is ordinal-lower than 'a').
        Assert.Equal("forge/A Idea", idea.WorkspaceSourceRelative);
        Assert.Equal("a-idea", idea.Slug);
        var d = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Skipped, d.Category);
        Assert.Contains("Duplicate idea slug 'a-idea'", d.Message);
        Assert.Contains("1 other(s) skipped", d.Message);
    }

    [Fact]
    public void Discover_UnparseableMemlogWithNoReport_IsSkippedRatherThanListed()
    {
        // [Story 18.4 review, owner decision 2026-07-28] Rule 1 alone (path only) proved nothing here, and an
        // unparseable memlog can't corroborate via rule 3 either — so unlike Discover_MalformedMemlog... above
        // (which DOES have a report and is proven by rule 2), this unproven, unparseable, report-less folder must
        // be skipped rather than listed as an in-progress idea.
        var dir = Path.Combine(Source, "forge", "junk-folder");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".memlog.md"), "not frontmatter at all, just text");

        var diagnostics = new List<AdapterDiagnostic>();
        var model = IdeaDiscovery.Discover(Source, diagnostics);

        Assert.True(model.IsEmpty);
        var d = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Malformed, d.Category);
        Assert.Contains("skipped rather than listed", d.Message);
    }

    [Fact]
    public void Discover_SlugCollidesWithAnotherIdeasReportPath_SkipsTheCollidingWorkspace()
    {
        // [Story 18.4 review] DetailOutputPath is "ideas/{slug}.html" and ReportOutputPath is
        // "ideas/{slug}-report.html" — so slug "foo-report" aliases onto slug "foo"'s carried-report path even
        // though the two raw slugs are never equal. The plain slug-equality check alone would miss this.
        // (Ordinal order over the full OS path — where '-' (0x2D) sorts below both '/' and '\' — decides which
        // of the two actually wins; the point under test is that exactly one survives either way, never both
        // with colliding output paths.)
        var withReport = Path.Combine(Source, "forge", "foo");
        WriteMemlog(withReport, "idea: has a report", "updated: 2026-07-20T09:00", "status: complete");
        Report(withReport, SafeReport("HARDENED"));
        WriteMemlog(Path.Combine(Source, "forge", "foo-report"), "idea: collides with foo's report path", "updated: 2026-07-21T09:00");

        var diagnostics = new List<AdapterDiagnostic>();
        var idea = Assert.Single(IdeaDiscovery.Discover(Source, diagnostics).Ideas);

        Assert.Contains(idea.Slug, new[] { "foo", "foo-report" });
        Assert.Contains(diagnostics, d => d.Category == AdapterDiagnosticCategory.Skipped
            && d.Message.Contains("collide", StringComparison.OrdinalIgnoreCase));
    }

    // ---- AC #6: the carry safety gate ---------------------------------------------------------------------

    [Fact]
    public void Discover_SelfContainedReport_IsCarriedVerbatim()
    {
        var dir = Path.Combine(Source, "forge", "safe");
        WriteMemlog(dir, "idea: safe report", "updated: 2026-07-21T09:00", "status: complete");
        var html = SafeReport("HARDENED");
        Report(dir, html);

        var idea = Assert.Single(IdeaDiscovery.Discover(Source).Ideas);

        Assert.Equal(html, idea.CarriedReportHtml); // verbatim — never rewritten, restyled, or sanitized
        Assert.Equal("ideas/safe-report.html", idea.ReportOutputPath);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<div onclick=\"go()\">x</div>")]
    [InlineData("<a href=\"javascript:go()\">x</a>")]
    [InlineData("<iframe srcdoc=\"<b>hi</b>\"></iframe>")]
    [InlineData("<link rel=\"stylesheet\" href=\"https://cdn.example.com/r.css\">")]
    [InlineData("<img src=\"//cdn.example.com/seal.png\">")]
    // [Story 18.4 review] Three bypasses the gate originally missed: an UNQUOTED handler (no quote right after
    // `=`), an external image via `srcset=` rather than `src=`, and a CSS `url(...)` reference (inline style
    // attribute) rather than an HTML attribute.
    [InlineData("<div onerror=alert(1)>x</div>")]
    [InlineData("<img srcset=\"https://cdn.example.com/seal.png 1x\">")]
    [InlineData("<div style=\"background:url(https://cdn.example.com/bg.png)\"></div>")]
    public void Discover_ReportThatIsNotSelfContained_IsNotCarriedAndReportsSkipped(string offendingMarkup)
    {
        var dir = Path.Combine(Source, "forge", "unsafe");
        WriteMemlog(dir, "idea: unsafe report", "updated: 2026-07-21T09:00", "status: complete");
        Report(dir, $"<!doctype html><html><body><h1>R</h1>{offendingMarkup}</body></html>");

        var diagnostics = new List<AdapterDiagnostic>();
        var idea = Assert.Single(IdeaDiscovery.Discover(Source, diagnostics).Ideas);

        Assert.Null(idea.CarriedReportHtml);
        Assert.Null(idea.ReportOutputPath); // derived, so the link and the write can never disagree
        var d = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Skipped, d.Category);
        Assert.Contains("not self-contained (script or external resource)", d.Message);
    }

    [Fact]
    public void Discover_ReportOverTheSizeCap_IsNotCarriedAndReportsSkipped()
    {
        var dir = Path.Combine(Source, "forge", "huge");
        WriteMemlog(dir, "idea: huge report", "updated: 2026-07-21T09:00", "status: complete");
        Report(dir, "<!doctype html><html><body>" + new string('x', IdeaDiscovery.MaxCarriedReportBytes) + "</body></html>");

        var diagnostics = new List<AdapterDiagnostic>();
        var idea = Assert.Single(IdeaDiscovery.Discover(Source, diagnostics).Ideas);

        Assert.Null(idea.CarriedReportHtml);
        var d = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Skipped, d.Category);
        Assert.Contains("exceeds the", d.Message);
    }

    // ---- Templater: grouping, empty sections, and the never-colour-only rule ------------------------------

    [Fact]
    public void RenderListPage_GroupsByVerdictInOrder_WithCountsAndNoEmptySections()
    {
        var model = new IdeasModel(new[]
        {
            Entry("h1", IdeaVerdict.Hardened, "Hardened"),
            Entry("h2", IdeaVerdict.Hardened, "Hardened"),
            Entry("k1", IdeaVerdict.Killed, "Killed"),
        });

        var html = IdeasTemplater.RenderListPage(model, Nav());

        Assert.Contains("id=\"ideas-hardened\"", html);
        Assert.Contains("2 ideas", html);
        Assert.Contains("id=\"ideas-killed\"", html);
        Assert.Contains("1 idea", html); // singular, not "1 ideas"
        // NFR8: the empty verdict emits NO section at all — never a heading with a zero beside it.
        Assert.DoesNotContain("id=\"ideas-in-progress\"", html);
        Assert.DoesNotContain("0 ideas", html);
        // D3 section order: strongest outcome first, killed last as history.
        Assert.True(html.IndexOf("id=\"ideas-hardened\"", StringComparison.Ordinal)
            < html.IndexOf("id=\"ideas-killed\"", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderListPage_EveryVerdictCarriesItsWordAndIsReadableWithoutJs()
    {
        var model = new IdeasModel(new[]
        {
            Entry("h", IdeaVerdict.Hardened, "Hardened"),
            Entry("c", IdeaVerdict.InProgress, "Clarified"),
            Entry("o", IdeaVerdict.InProgress, "In progress"),
            Entry("k", IdeaVerdict.Killed, "Killed"),
        });

        var html = IdeasTemplater.RenderListPage(model, Nav());

        // UX-DR17 / "no state signalled by colour alone": the WORD is in the markup for every verdict, including
        // the clarified exit the three-bucket list cannot express on its own.
        Assert.Contains(">Hardened<", html);
        Assert.Contains(">Clarified<", html);
        Assert.Contains(">In progress<", html);
        Assert.Contains(">Killed<", html);
        // ADR 0013 / NFR-5: the CONTENT carries no script of its own — the only <script> on the page is the
        // site-wide progressive-enhancement bundle every page loads from RenderHeadOpen. Under
        // `script-src 'none'` the grouped list is still complete, readable HTML; `js-listable` is the inert
        // Story 10.9 sort/filter opt-in seam, not a rendering dependency.
        var main = html[html.IndexOf("<main", StringComparison.Ordinal)..html.IndexOf("</main>", StringComparison.Ordinal)];
        Assert.DoesNotContain("<script", main);
        Assert.Contains("js-listable", main);
        // The accent bar reinforces, never signals alone.
        Assert.Contains("list-row-accent-done", html);
        Assert.Contains("list-row-accent-deferred", html);
        Assert.Contains("list-row-accent-pending", html);
    }

    [Fact]
    public void RenderDetailPage_StatesTheTrueExitWord_ForAClarifiedSession()
    {
        // The D2 mitigation: the list groups a clarified session under "In progress", so the detail page has to be
        // the record that it was COMPLETE. Without this sentence the bucketing would be the portal's only claim.
        var html = IdeasTemplater.RenderDetailPage(Entry("c", IdeaVerdict.InProgress, "Clarified"), Nav());

        Assert.Contains("clarified", html);
        Assert.Contains("complete", html);
        Assert.Contains("In progress", html); // names the bucket it was filed under, so the grouping isn't a surprise
    }

    [Fact]
    public void RenderDetailPage_NoForwardLinks_EmitsNoForwardLinkElementAtAll()
    {
        // NFR8 / D4: absent, not "none found". A "no downstream artifact" placeholder would be a claim the
        // evidence does not support.
        var html = IdeasTemplater.RenderDetailPage(Entry("x", IdeaVerdict.Hardened, "Hardened"), Nav());

        Assert.DoesNotContain("idea-downstream", html);
        Assert.DoesNotContain("What it became", html);
    }

    [Fact]
    public void RenderDetailPage_NoCarriedReport_OmitsTheReportLink()
    {
        var html = IdeasTemplater.RenderDetailPage(Entry("x", IdeaVerdict.Killed, "Killed"), Nav());

        Assert.DoesNotContain("original forge report", html);
    }

    [Fact]
    public void RenderDetailPage_CarriedReport_LinksItAsASiblingLeaf()
    {
        var entry = Entry("x", IdeaVerdict.Hardened, "Hardened") with { CarriedReportHtml = SafeReport("HARDENED") };

        var html = IdeasTemplater.RenderDetailPage(entry, Nav());

        // Both live under ideas/, so the href is the bare sibling filename — not the page prefix plus the
        // root-relative path, which would climb out of the directory.
        Assert.Contains("href=\"x-report.html\"", html);
        Assert.DoesNotContain("href=\"../ideas/x-report.html\"", html);
        // It is a foreign document carried verbatim, so the page says so rather than stranding the reader.
        Assert.Contains("has no site navigation", html);
    }

    // ---- Nav gating: AC #3 ---------------------------------------------------------------------------------

    [Fact]
    public void SiteNav_HasIdeas_GatesTheEntryAndTheQuickLinkTogether()
    {
        var without = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "T");
        var with = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "T", hasIdeas: true);

        Assert.False(without.HasIdeas);
        Assert.DoesNotContain(without.Items, i => i.OutputRelativePath == SiteNav.IdeasOutputPath);
        Assert.DoesNotContain(without.QuickLinks, q => q.OutputRelativePath == SiteNav.IdeasOutputPath);

        Assert.True(with.HasIdeas);
        Assert.Contains(with.Items, i => i.Label == "Ideas" && i.OutputRelativePath == SiteNav.IdeasOutputPath);
        // Project group — an idea is neither tracked work (Delivery) nor a derived metric (Insights).
        Assert.Contains(with.QuickLinks, q => q.Label == "Ideas" && q.Group == "Project");
    }

    // ---- Generation: the whole surface, end to end ---------------------------------------------------------

    [Fact]
    public void GenerateAll_NoForgeWorkspace_WritesNoIdeasPageNoNavEntryAndNoStructureNotice()
    {
        // AC #3 in full: absent artifacts → absent surfaces (NFR8), and `forge` being a known folder group must not
        // start reporting anything on a repo that has never run the forge.
        SeedEpics();
        var site = Generate(out var events);

        Assert.False(File.Exists(Path.Combine(site, "ideas.html")));
        Assert.False(Directory.Exists(Path.Combine(site, "ideas")));
        Assert.DoesNotContain(events, e => e.RelativePath.Contains("ideas", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("ideas.html", File.ReadAllText(Path.Combine(site, "index.html")));
        // [Story 18.4 review] The test's own name promised this and never asserted it.
        Assert.DoesNotContain(events, e => e.Message != null && e.Message.Contains("unrecognized top-level folder"));
    }

    [Fact]
    public void GenerateAll_WithForgeWorkspaces_WritesTheListDetailAndCarriedReport_AndSuppressesTheOrphanGenericPage()
    {
        SeedEpics();
        var hardened = Path.Combine(Source, "forge", "cache-layer");
        WriteMemlog(hardened, "idea: A write-through cache", "goal: cut p95 latency", "updated: 2026-07-21T11:02", "status: complete");
        File.WriteAllText(Path.Combine(hardened, "forged-idea.md"), "# Write-through cache\n\nLocked.\n");
        Report(hardened, SafeReport("HARDENED"));

        var site = Generate(out var events);

        Assert.True(File.Exists(Path.Combine(site, "ideas.html")));
        Assert.True(File.Exists(Path.Combine(site, "ideas", "cache-layer.html")));
        Assert.True(File.Exists(Path.Combine(site, "ideas", "cache-layer-report.html")));
        // The idea's own markdown is consumed by its detail page — no second, orphan generic page for it.
        Assert.False(File.Exists(Path.Combine(site, "forge", "cache-layer", "forged-idea.html")));
        // The carried report is a LEAF: written verbatim, never wrapped in the portal template (which would nest
        // one complete <html> document inside another).
        var carried = File.ReadAllText(Path.Combine(site, "ideas", "cache-layer-report.html"));
        Assert.Equal(SafeReport("HARDENED"), carried);
        Assert.DoesNotContain("site-nav", carried);
        // And the nav entry the gate promised actually exists.
        Assert.Contains("ideas.html", File.ReadAllText(Path.Combine(site, "index.html")));
        // [Story 18.4 review] The KnownIndexGroups("Ideas", "forge") registration must actually suppress the
        // generic notice for a REAL forge/ folder with a discovered idea — not just an absent one.
        Assert.DoesNotContain(events, e => e.Message != null && e.Message.Contains("unrecognized top-level folder"));
    }

    [Fact]
    public void RenderSpaBundle_CarriedReport_IsOnDiskButNotAnSpaRoute()
    {
        // The carried report is the ONE output page this generator did not compose, so it has no
        // `<main id="main-content">` landmark. SpaDelivery.ExtractContentRegion degrades a landmark-less page to
        // nav-markup-only, so capturing it would ship a CONTENT-EMPTY route in the bundle while the real, perfectly
        // readable file sat on disk beside it. Excluded, the link resolves to that static file — right for a
        // deliberate dead-end leaf.
        SeedEpics();
        var dir = Path.Combine(Source, "forge", "cache-layer");
        WriteMemlog(dir, "idea: A write-through cache", "updated: 2026-07-21T11:02", "status: complete");
        Report(dir, SafeReport("HARDENED"));

        var site = Path.Combine(_root, "site");
        var gen = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Path.Combine(_root, "docs", "adrs"), output: site,
            projectName: "TestProj", emitSpa: true));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var bundle = gen.RenderSpaBundle();

        Assert.True(File.Exists(Path.Combine(site, "ideas", "cache-layer-report.html")));
        Assert.DoesNotContain(bundle.Pages, p => p.OutputRelativePath.Contains("-report.html", StringComparison.Ordinal));
        // The idea's own composed pages ARE routes — only the foreign leaf is held out.
        Assert.Contains(bundle.Pages, p => p.OutputRelativePath == SiteNav.IdeasOutputPath);
        Assert.Contains(bundle.Pages, p => p.OutputRelativePath == "ideas/cache-layer.html");
    }

    [Fact]
    public void GenerateAll_ForwardLink_ReverseDirectionEvidence_ResolvesFromADownstreamDocsSources()
    {
        // [Story 18.4 review] AC #2 / §9 names TWO admissible evidence sources; only the first (a markdown link
        // inside forged-idea.md) had a test. This pins the second: a downstream doc whose OWN frontmatter
        // `sources:` names the forge workspace.
        SeedEpics();
        var dir = Path.Combine(Source, "forge", "cache-layer");
        WriteMemlog(dir, "idea: A write-through cache", "updated: 2026-07-21T11:02", "status: complete");

        var briefDir = Path.Combine(Source, "planning-artifacts", "briefs");
        Directory.CreateDirectory(briefDir);
        File.WriteAllText(Path.Combine(briefDir, "brief-cache.md"),
            "---\nsources:\n  - forge/cache-layer/forged-idea.md\n---\n\n# A downstream brief\n\nBody.\n");

        var site = Generate(out _);
        var detail = File.ReadAllText(Path.Combine(site, "ideas", "cache-layer.html"));

        Assert.Contains("A downstream brief", detail);
        Assert.Contains("Declared in this document&#39;s sources", detail);
    }

    [Fact]
    public void GenerateAll_ForwardLink_ResolvesAnExistingPageAndDropsOneWithNoPage()
    {
        SeedEpics();
        var dir = Path.Combine(Source, "forge", "cache-layer");
        WriteMemlog(dir, "idea: A write-through cache", "updated: 2026-07-21T11:02", "status: complete");
        File.WriteAllText(Path.Combine(dir, "forged-idea.md"),
            "# Cache\n\nFeeds [the epics roster](../../planning-artifacts/epics.md).\n"
            + "Also [a doc that was never written](../../planning-artifacts/ghost.md).\n");

        var site = Generate(out var events);
        var detail = File.ReadAllText(Path.Combine(site, "ideas", "cache-layer.html"));

        // Resolved: routed to the CURATED epics page, not a generic epics.html-of-the-source guess.
        Assert.Contains("href=\"../epics.html\"", detail);
        // Dropped: the missing target never becomes a link, and the omission is reported rather than silent.
        Assert.DoesNotContain("ghost", detail.Replace("ghost.md", string.Empty));
        Assert.Contains(events, e => e.Message is not null && e.Message.Contains("has no generated page"));
        // The hand-off body's own raw markdown href must not survive into the portal as a dead link.
        Assert.DoesNotContain("planning-artifacts/epics.md\"", detail);
    }

    [Fact]
    public void GenerateAll_ForgeMemlog_DoesNotStripTheRootJournalDateFromCoverageCards()
    {
        // Story 18.4 §7(a), the pre-existing behaviour this story had to decide about rather than discover later.
        // SelectMemlogUpdatedByFamily demotes a ROOT-level memlog from every-family fallback the moment ANY scoped
        // memlog exists. A forge workspace's memlog is scoped, so without the exclusion, running the forge ONCE
        // would silently strip the journal date from every coverage card — an unrelated surface changing because a
        // different tool ran. DECISION: exclude forge workspaces from BuildMemlogMap's input.
        SeedEpics();
        File.WriteAllText(Path.Combine(Source, ".memlog.md"),
            "---\ntopic: the project journal\nupdated: 2026-07-19T10:00\n---\n\n- (note) seeded\n");

        var before = File.ReadAllText(Path.Combine(Generate(out _), "index.html"));

        WriteMemlog(Path.Combine(Source, "forge", "an-idea"), "idea: something", "updated: 2026-07-26T10:00");
        var after = File.ReadAllText(Path.Combine(Generate(out _), "index.html"));

        // The coverage panel's journal-derived freshness is unchanged by the forge run.
        Assert.Equal(CoverageFreshnessSignature(before), CoverageFreshnessSignature(after));
    }

    [Fact]
    public void GenerateAll_SlugCollisionLoser_IsStillExcludedFromCoverageJournalFallback()
    {
        // [Story 18.4 review] §7(a)'s fix originally keyed the forge-exclusion set off `_ideas.Ideas` — but a
        // slug-collision LOSER never reaches `Ideas`, so its memlog stayed in BuildMemlogMap's scan and could
        // still flip hasScopedMemlog even though the workspace IS a proven forge session. Two workspaces that
        // slugify to the same name reproduce that gap directly.
        SeedEpics();
        File.WriteAllText(Path.Combine(Source, ".memlog.md"),
            "---\ntopic: the project journal\nupdated: 2026-07-19T10:00\n---\n\n- (note) seeded\n");
        var before = File.ReadAllText(Path.Combine(Generate(out _), "index.html"));

        WriteMemlog(Path.Combine(Source, "forge", "a-idea"), "idea: first", "updated: 2026-07-20T09:00");
        WriteMemlog(Path.Combine(Source, "forge", "A Idea"), "idea: second (slug collision loser)", "updated: 2026-07-21T09:00");
        var after = File.ReadAllText(Path.Combine(Generate(out _), "index.html"));

        Assert.Equal(CoverageFreshnessSignature(before), CoverageFreshnessSignature(after));
    }

    [Fact]
    public void SelectMemlogUpdatedByFamily_AForgeMemlogCouldNeverWinAFamilyAnyway()
    {
        // Story 18.4 §7(b): the second half of the claim above, asserted directly rather than read. A forge
        // workspace lives at forge/{slug}/, and no artifact family's source path is under it, so a forge memlog
        // contributes no date to any family — it only ever flipped the hasScopedMemlog flag.
        var families = new[]
        {
            new ArtifactFamily("PRD", "prd", "The product requirements document", Present: true,
                LastModified: null, SourcePath: "planning-artifacts/prds/prd-x/prd.md", MemlogUpdated: null),
        };

        var map = SiteGenerator.SelectMemlogUpdatedByFamily(
            new[] { ("forge/an-idea", new DateOnly(2026, 7, 26)) }, families);

        Assert.False(map.ContainsKey("PRD"));
    }

    // ---- Helpers -------------------------------------------------------------------------------------------

    private static readonly IReadOnlyList<MemlogEntry> NoEntries = Array.Empty<MemlogEntry>();

    private static IReadOnlyDictionary<string, string> Fm(params (string Key, string Value)[] pairs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in pairs) map[k] = v;
        return map;
    }

    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "TestProj", hasIdeas: true);

    private static IdeaEntry Entry(string slug, IdeaVerdict verdict, string exitWord) => new()
    {
        Slug = slug,
        Title = "Title " + slug,
        Summary = "summary " + slug,
        Verdict = verdict,
        ExitWord = exitWord,
        Date = new DateOnly(2026, 7, 21),
        WorkspaceSourceRelative = "forge/" + slug,
        Entries = Memlog.ParseEntries(new[] { "- (decision) do the thing" }),
    };

    private static string SafeReport(string stamp) =>
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>Forge Report</title>"
        + "<style>body{font-family:Georgia,serif}</style></head><body><h1>Report</h1>"
        + $"<svg width=\"80\" height=\"80\" viewBox=\"0 0 80 80\" role=\"img\" aria-label=\"seal\"><circle cx=\"40\" cy=\"40\" r=\"36\"/></svg><p>{stamp}</p>"
        + "</body></html>";

    /// <summary>Writes a memlog in <c>memlog.py</c>'s exact rendered shape: a plain <c>key: value</c> block, the
    /// closing fence, then one append-only entry.</summary>
    private static void WriteMemlog(string workspaceDir, params string[] frontmatterLines)
    {
        Directory.CreateDirectory(workspaceDir);
        File.WriteAllText(
            Path.Combine(workspaceDir, ".memlog.md"),
            "---\n" + string.Join("\n", frontmatterLines) + "\n---\n\n- (note) seeded\n");
    }

    private static void Report(string workspaceDir, string html)
    {
        Directory.CreateDirectory(workspaceDir);
        File.WriteAllText(Path.Combine(workspaceDir, "forge-report.html"), html);
    }

    private void SeedEpics()
    {
        var planning = Path.Combine(Source, "planning-artifacts");
        Directory.CreateDirectory(planning);
        File.WriteAllText(Path.Combine(planning, "epics.md"),
            """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            FR1: The portal renders artifacts

            ## Epic List

            ## Epic 1: Rendering

            ### Story 1.1: Render a page

            As a reader, I want a page, so that I can read it.
            """);
    }

    private string Generate(out IReadOnlyList<GenerationEvent> events)
    {
        var site = Path.Combine(_root, "site");
        var options = ForgeOptions.Resolve(
            source: Source,
            adrs: Path.Combine(_root, "docs", "adrs"),
            output: site,
            projectName: "TestProj");
        events = new SiteGenerator(options).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        return site;
    }

    /// <summary>The dashboard's journal-derived freshness text, isolated from every volatile token, so the §7(a)
    /// regression compares the thing under test rather than the whole page.</summary>
    private static string CoverageFreshnessSignature(string indexHtml)
    {
        var marker = "coverage-family";
        var start = indexHtml.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        var end = indexHtml.IndexOf("</section>", start, StringComparison.Ordinal);
        return end < 0 ? indexHtml[start..] : indexHtml[start..end];
    }
}
