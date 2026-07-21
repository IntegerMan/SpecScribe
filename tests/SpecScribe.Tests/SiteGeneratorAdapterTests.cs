using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 4.1: with ingestion routed through
/// <see cref="BmadArtifactAdapter"/>, the generated site is exactly what the inline parse chain produced —
/// pinned here as a golden inventory of every output file a representative BMad fixture yields — and adapter
/// diagnostics surface on the existing event channel without failing the run or suppressing sibling pages
/// (AC #2). The full byte-for-byte before/after diff was performed against a frozen copy of this repo's own
/// artifacts at implementation time (zero diffs, modulo the wall-clock footer and the build-derived asset
/// cache-bust token); this fixture keeps the shape of that guarantee alive in the suite. Follows the temp-dir
/// fixture style of <see cref="SiteGeneratorSprintTests"/>.</summary>
public class SiteGeneratorAdapterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-adaptergen-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string SprintYaml => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### NonFunctional Requirements

        NFR1: Generation degrades gracefully

        ### FR Coverage Map

        FR1: Epic 1 - rendering
        NFR1: Epic 1 - degradation

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ### Epic 2: Delivery

        Ship the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).

        **Acceptance Criteria:**

        1.
        **Given** an undrafted story
        **When** the site generates
        **Then** a placeholder page exists

        ## Epic 2: Delivery

        ### Story 2.1: Delivery Story

        As a maintainer, I want delivery.
        """;

    // Epic 2 is all-done but has NO retrospective — the ForEpic vs ForEpicWithRetrospective divergence (an
    // all-done-without-retro epic reads as "In review" on the visual status surfaces). Keeping this in the golden
    // fixture is what makes the byte gate actually EXERCISE that retro-gated branch, which it previously did not.
    // [Story 6.2 review]
    private const string Story21Md = """
        # Story 2.1: Delivery Story

        Status: done

        ## Story

        As a maintainer, I want delivery.

        ## Acceptance Criteria

        1. It ships.

        ## Tasks / Subtasks

        - [x] Task 1: Ship it (AC: #1)
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string RetroMd = """
        # Epic 1 Retrospective

        **Date:** 2026-07-06
        **Participants:** Team

        Went well.
        """;

    private const string SprintYamlContent = """
        last_updated: 2026-07-06T22:00:00-04:00
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
          1-2-undrafted: backlog
          epic-2: done
          2-1-delivery: done
        """;

    public SiteGeneratorAdapterTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "2-1-delivery.md"), Story21Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "epic-1-retro-2026-07-06.md"), RetroMd);
        File.WriteAllText(SprintYaml, SprintYamlContent);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    [Fact]
    public void GenerateAll_GoldenOutputInventory_IsExactlyThePreAdapterPageSet()
    {
        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // The activity timeline + date pages are now git-derived (Story 7.3 bug fix): they no longer fire on the
        // filesystem-mtime signal, so this NON-git fixture yields NEITHER timeline.html NOR any commits/ date page —
        // the honest degradation of "drop the claim when git can't verify it" (the mtime signal collapsed every
        // artifact onto the checkout day). The date-fold is kept defensively in case any surface still stamps today.
        var todayIso = Charts.D(DateOnly.FromDateTime(DateTime.Now));
        var actual = Directory.EnumerateFiles(Site, "*", SearchOption.AllDirectories)
            .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p)).Replace(todayIso, "<date>"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // The exact page set the pre-adapter pipeline produced for this fixture — a new, missing, or
        // relocated output file is a rendering-behavior change and must be a deliberate decision, never a
        // side effect of adapter work (AC #1: rendering stays framework-agnostic and unchanged).
        var expected = new[]
        {
            // about.html + diagnostics.html are the Story 4.8 additions to the page set — deliberate output
            // change (this story adds pages + a site-wide footer link), unlike the byte-parity 4.1/4.2 stories.
            "about.html",
            "adrs/index.html",
            // Story 7.6: code-map.html replaced the retired Story 3.4 structure.html (source-code treemap; the
            // fixture's repo-root walk finds its markdown files, so the surface generates).
            "code-map.html",
            // Story 7.3 bug fix: timeline.html + commits/ date pages are git-derived now, so this non-git fixture
            // emits none of them (previously the mtime signal produced a today-stamped date page + timeline here).
            "diagnostics.html",
            "epics.html",
            "epics/epic-1.html",
            "epics/epic-2.html",
            "epics/story-1-1.html",
            "epics/story-1-2.html",
            "epics/story-2-1.html",
            // Story 10.3: the how-to-read orientation page is written on every full run, like about.html/diagnostics.html.
            "how-to-read.html",
            // About Spec-Driven Development hub + per-framework sub-pages (always written).
            "about-sdd.html",
            "about-sdd-bmad.html",
            "about-sdd-gds.html",
            "about-sdd-speckit.html",
            "about-sdd-gsd.html",
            "about-sdd-gsd-pi.html",
            "about-sdd-superpowers.html",
            "implementation-artifacts/epic-1-retro-2026-07-06.html",
            "index.html",
            "requirements.html",
            "requirements/fr1.html",
            "requirements/nfr1.html",
            "retros.html",
            "specscribe.css",
            "specscribe.js",
            "sprint.html",
        }.OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens()
    {
        // AC #1's real guarantee is byte-for-byte-unchanged RENDERED OUTPUT, not merely a matching file set:
        // GoldenOutputInventory (above) pins the page SET, so a silent CONTENT drift that keeps filenames
        // stable — risk center #2, the exact failure the manual implementation-time byte-diff guarded once but
        // the suite did not — would sail through it. This pins the CONTENT: a SHA-256 over every output file,
        // after neutralizing the only volatile tokens (the wall-clock footer, the ?v=<ModuleVersionId> asset
        // cache-bust, CRLF, and the build-derived product version) — the same normalizations that diff used
        // (memory: golden-diff-normalization-gotchas). Regenerate the constant ONLY as a deliberate, reviewed
        // decision; an unexpected flip is a rendering regression. [Story 4.1 review]
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var fingerprint = FingerprintTree(Site);

        // Regenerated for Story 7.1 (rework): specscribe.css gained Prism syntax-highlight token rules and moved
        // the code-page line gutter to a CSS ::before counter. The fixture cites no real repo files, so no code
        // page (and no vendored prism.js/prism.css) is emitted here — the inventory is unchanged; only the
        // stylesheet content shifted the fingerprint. Regenerated again for Story 7.4: specscribe.css gained the
        // opt-in ".code-insights" advanced-coverage styles (CSS-only; the fixture is not a git repo and cites no
        // real files, so no per-file insight section renders — again only the stylesheet content shifted).
        // Regenerated for spec-scribes-nib-branding: the nav brand span gained the inline Scribe's Nib mark
        // (every page — the ONE RenderNavMarkup seam), and specscribe.css gained .site-nav-mark, the
        // --funnel-connector token, and the AA-deepened --ink-light (#7a6250 → #6b5442).
        // Regenerated for Story 7.6: the retired structure.html became the source-code treemap code-map.html
        // (new page content), the nav item/quick link "Structure" → "Code Map" on every page (the shared nav
        // seam), and specscribe.css/.js gained the .codemap-* treemap styles + the scoped zoom/dimension-switch
        // enhancement (structure-tree CSS removed).
        // Regenerated once more for spec-scribes-nib-branding's review patches: the funnel connector dropped
        // its opacity (the token now ships raw), --status-deferred froze at its pre-pass literal (decoupled
        // from --ink-light), and the brand-mark SVG gained fallback width/height + the widened nib cutouts.
        // Regenerated for spec-website-nib-favicon: the every-page favicon in RenderHeadOpen changed from the
        // retired gold-quill-spark star to the Scribe's Nib mark on a teal tile (reusing HtmlRenderAdapter.NibPathData),
        // so every page's <head> data-URI icon shifted. No file-set change; content-only.
        // Regenerated for spec-code-page-relationships-history-tabs: the code page's tab strip grew from two views
        // to four (Insights | Relationships | History | Code) with per-tab icons, so specscribe.css gained the
        // four-modifier panel-toggle rule, a generalized deep-link :target override, and a .code-tab .ss-icon reset.
        // CSS-only for this fixture (it cites no real repo files, so no code page renders here) — only the
        // every-page stylesheet content shifted the fingerprint; the file set is unchanged.
        // Regenerated for the Story 7.4 code review pass: FileInsight gained TotalContributors + a
        // ".code-insight-more" truncation-disclosure style so a capped contributor list no longer reads as
        // complete. CSS-only for this fixture (no real repo files cited, so no insight section renders here).
        // Regenerated for the Story 7.6 code review pass: the codemap legend gained a "Colorized by …" dimension
        // label span + supporting CSS; specscribe.js gained the recolor()/setViewBox() review fixes. CSS/JS-only
        // for this fixture (no real repo files cited, so no code-map treemap cells render here).
        // Regenerated for spec-7-3-10-4-honest-navigable-portal-dates: (1) the footer clock moved to the single
        // PortalDates token — 24-hour + a machine-local UTC-offset zone label (FooterClock regex widened above);
        // (2) the non-git fixture no longer emits timeline.html or any commits/ date page (Story 7.3's artifact
        // signal is git-derived now, not filesystem-mtime, so it drops when git can't verify it — inventory above
        // lost both); (3) ADR cards gained date + one-line summary lines; (4) doc/retro card dates + change-log
        // dates now route through PortalDates. Every change is a deliberate, reviewed rendering change (AC #1).
        // [golden-diff-normalization-gotchas]
        // Regenerated for the spec-7-3-10-4 code-review patch pass: GitPulsePanel's date-link guard now checks
        // actual LinkedCommitDays membership instead of a bare day<=today comparison; ExtractAdrSummary's H1-tail
        // dash split now uses the LAST dash occurrence, not the first; PortalDates dropped the ambiguous
        // "M/d/yyyy" authored-date format. CSS/content-only for this fixture where those paths are exercised.
        // Regenerated for Story 7.8: specscribe.css gained the reference graph's related-file node styles
        // (.ref-edge-file / .ref-file-dot / .ref-file-label / .ref-overflow) and dropped the retired visible
        // coupled-list styles (.code-insight-coupled). The fixture is not a git repo and cites no real files, so
        // no code page or per-file coupling renders here — again only the stylesheet content shifted the hash.
        // [golden-diff-normalization-gotchas]
        // Regenerated for spec-entity-prev-next-navigation: leaf entity pages gained an inline "‹ Prev / Next ›"
        // sibling pager in their .doc-header — epic + story + ADR + retro pages render here (the fixture is not a
        // git repo, so no commit/date/code pages exist to gain one). specscribe.css gained the .entity-pager* rules
        // + .doc-header position:relative and dropped the retired bottom-of-page .commit-day-nav styles. Every
        // change is a deliberate, reviewed rendering change (AC #1).
        // Regenerated for spec-code-map-declutter-and-cochange: this fixture is not a git repo (no --deep-git), so
        // code-map.html never renders here — only specscribe.css shifted the hash (.codemap-dir-label rule removed;
        // .codemap-dir comment updated). The treemap's own label-removal/date-format/co-change changes are covered
        // by CodeMap/Charts/CodeMapTemplater tests, not this fixture.
        // Regenerated for spec-code-map-declutter-and-cochange round 2: the colorize-dropdown/churn-option/exclude-
        // filter-checkbox styles landed in specscribe.css (again, this non-git fixture never renders code-map.html
        // itself — only the shared stylesheet content shifted the hash).
        // Regenerated for spec-reference-graph-epic-grouping-and-relationships: specscribe.css gained the "Group by
        // epic"/"Show relationships" toggle-checkbox styles, the four .ref-graph-view sibling-combinator rules, the
        // epic-hub node styles, and the hub-spoke/cross-edge line styles. This non-git fixture cites no real repo
        // files, so no code page's reference graph itself renders here — only the shared stylesheet content shifted
        // the hash (the new toggle/hub/edge behavior is covered by Charts/CodeFileTemplater/SiteGenerator tests).
        // Regenerated post-merge review fixes: removed the redundant margin-top on .refgraph-toggle (it shifted the
        // checkbox out of vertical alignment with its own label — spacing from the note above is already covered by
        // .code-relationships-note's margin-bottom). CSS-only for this non-git fixture.
        // Regenerated for the Story 7.8 code-review patch pass: the related-file node hover/focus-visible rules in
        // specscribe.css are now scoped to `a.ref-file-node` (link nodes only) instead of the shared `.ref-file-node`
        // class, so non-link chip nodes no longer get link-style hover/focus affordance. CSS-only for this non-git
        // fixture (no code page renders here) — only the stylesheet content shifted the hash.
        // Regenerated for Story 8.2: every page gained the shared status-legend-key (via RenderFooter /
        // webview+SPA BodyHtml append), every StatusStyles.Badge gained js-tip + data-tip + title, specscribe.css
        // gained --status-unrecognized + legend/badge rules, and sprint board gained an unrecognized lane.
        // Fixture statuses are all known keywords — no unrecognized badges render here; delta is chrome + CSS.
        // Reconfirmed during Story 8.3: a before/after fixture diff of HEAD (8.2) vs the ProjectCounts ledger
        // is byte-identical for every rendered page (only the diagnostics absolute repo-root path differed —
        // already folded by NormalizeVolatile). The previous constant was stale on current runners; both
        // trees produce this hash, so regenerating is a baseline refresh, not a count-number change. [Story 8.3]
        // Regenerated for Story 8.4: epic-page story cards wrap status+task badges in .story-status-pair;
        // epic mosaic gains DeliverySentence as visible .epic-mosaic-delivery (Donut stays decorative so
        // ariaLabel does not nest per-slice tabindex inside the card <a>); sprint lane heads gain
        // js-tip/data-tip/title/tabindex from StageMeaning; no-plan sprint cards gain .no-plan; specscribe.css
        // gains the four companion rules. StageMeaning pending/ready phrasings also update badge tips + legend.
        // Regenerated for Story 8.4 code-review patch: mosaic Donut drops aria-label/role=img (decorative
        // again); visible delivery sentence remains the accessible restatement inside the card link.
        // Regenerated for Story 8.5: every Next Steps panel gains primary/alternate hierarchy markup
        // (.next-steps-primary + optional .next-steps-alternates / .next-steps-alt) and specscribe.css gains the
        // companion rules; done panels stay celebratory when the fixture catalog lacks correct-course. [Story 8.5]
        // Regenerated for Story 8.6: empty sprint/home board lanes gain .sprint-lane-empty ghost-card placeholders;
        // multi-undrafted epics would gain .epic-undrafted-banner (this fixture's epics stay under the 2+ threshold);
        // specscribe.css gains both companion rules. [Story 8.6]
        // Regenerated for Story 8.7: the home Requirements panel consolidates its two renderings behind a
        // panel-scoped pure-CSS radio-toggle — the coverage flow is the default-visible primary (rendered
        // FIRST now, a deliberate order flip) inside .req-view-flow, the status-block grid the demoted
        // alternate inside .req-view-grid (kept in the DOM as the flow's Story-3.7 text-twin); the epics-index
        // header subtitle drops its epic/drafted count restatement (the stat grid below is the single count
        // display); specscribe.css gains the .req-view/.req-panel:has(#rv-grid:checked) toggle rules. A before/
        // after byte-diff confirmed those are the ONLY rendering changes. [Story 8.7; golden-diff-normalization-gotchas]
        // Regenerated for Story 8.8: specscribe.css gained .story-card-updated (muted recency marker on epic-page
        // story cards). This non-git fixture's story artifacts have no ## Change Log, so no "Updated <date>" span
        // renders here — only the shared stylesheet content shifted the hash. Commits-tile absolute-date fix is
        // likewise invisible (fixture has "no git history"). Marker + CommitStatSub determinism are covered by
        // unit tests. [Story 8.8; golden-diff-normalization-gotchas]
        // Regenerated for status-legend on-demand polish: always-visible footer/multi-column legend
        // removed; LegendKey is a "?" <details> popover (single-column) beside status-bearing surfaces;
        // webview/SPA no longer append a page-level legend. Also absorbs the prior sprint-board epic-filter
        // layout commit's fixture delta. [Story 8.2 UI feedback]
        // Regenerated for sprint-board density polish: empty retired/unrecognized lanes omit entirely;
        // --lane-count drives grid columns; lane labels use nowrap + a slightly wider min track so
        // "Ready for Dev" doesn't wrap alone under width pressure. [board UX feedback]
        // Regenerated for spec-declutter-home-dashboard: the home dashboard body dropped the quick-dev card grid
        // and ALL home index bands (planning/spec/implementation/overview/ADR/retro card lists); the work-section
        // omit gate changed to `work.Deferred is null && openRetro == 0` (a quick-dev-only project renders no
        // orphan heading). Every kept pulse panel, the Explore Key Views pills, and the Deferred/Retro callouts
        // are byte-unchanged — the fingerprint delta is the removed grid + removed bands, plus the dashboard
        // relayout: a two-row header (project name + SpecScribe brand badge, and the Explore Key Views pills as a
        // sub-header menu), linked stat tiles, Overall Progress collapsed to a single completion ring + legend,
        // and white Deferred/Retro cards. Confirmed by inspecting the generated home page. [spec-declutter-home-dashboard]
        // Regenerated: dark bar holds Home/Docs/Architecture/Work (icons; Code Map under Architecture);
        // white bar groups related docs under a Docs dropdown (Architecture / Work peers); dashboard unifies
        // tiles in journey-segmented flex-wrap band; Epic Status drops its side legend (hover titles only);
        // Overall Progress is a compact ring without the repetitive Planning/Implementation legend;
        // Retro → "Action Items" with an icon; Stories defined → Requirements; redundant View epics/sprint
        // CTAs removed; first-row stat tooltips use body-level js-tip. [journey-nav-key-views-unified-tiles;
        // home-welcome-screen-flow-and-nav]
        // Regenerated after the home-welcome tooltip follow-up: specscribe.js temporarily suppresses native
        // title tooltips while body-level data-tip/data-tip-html tooltips are active, avoiding duplicate tips.
        // Regenerated after review patches: key-view aria-expanded/controls + panel ids; journey aria-labelledby;
        // empty overall-progress omitted; stat-card-link exempt from two-tap touch; key-view click toggle.
        // Regenerated for dense two-row tile band: journey accents on cards, floating group captions, StatCard
        // follow-ups, donut tiles with bottom labels, commits sub-line without active-day count.
        // Regenerated after reverting journey *clusters* (they broke the compact 2-row wrap) — back to floating
        // left-aligned captions; Direct changes stays under Execution; Follow up rename; planned-tasks sub removed.
        // Regenerated for Story 8.3 review patches: funnel reads ProjectCounts (byte-identical counts; hash
        // refresh after ledger wiring + OpenActionItems empty-entries fix).
        // Regenerated for Story 8.6 review: empty Backlog lane copy shortened to "No cards in backlog".
        // Regenerated for Story 8.7 review patches: Requirements panel header wraps Flow/Status-grid toggle +
        // View Requirements CTA in .req-panel-header-aside (title | controls, mirroring Now & Next).
        // Regenerated for Story 9.1: each requirement detail page (requirements/*.html) replaced its single
        // primary-epic Coverage card with per-covering-epic GROUPS — each group is the covering-epic header
        // (now emitted for EVERY covering epic, not just the primary) followed by that epic's stories as
        // compact linked+badged cards; deferred vs unmapped empty states now read distinctly. specscribe.css
        // gained the .coverage-group / .coverage-story-cards / .coverage-story-card.* layout rules (status
        // color routed through the shared --status-* tokens). Every FR/NFR detail page shifts; the file set is
        // unchanged. Deliberate, reviewed rendering change (AC #1). [Story 9.1; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.2 UX follow-up: covering epics in the NFR/UX-DR coverage section moved from
        // header chips to a labeled "Delivered by" card list under each requirement description
        // (.nfr-uxdr-epics / .nfr-uxdr-epic-card). Fixture has no UX-DR inventory — delta is primarily CSS.
        // Regenerated for Story 9.4: drafted story pages gained a verification evidence strip under the status
        // badge (tasks / tests / verified-or-updated pills + CSS). Fixture story pages with a Status line now
        // emit .evidence-strip; stylesheet gained .evidence-strip / .evidence-pill / evidence link styles. [Story 9.4]
        // Regenerated for Story 9.5: .ac-criterion resting card (border + parchment + gold left accent; :target
        // stronger) + Dev Notes/References collapse via CollapsibleSections on story remainder HTML +
        // .collapsible-section caret CSS + webview resting-tint companion. Fixture stories without Dev Notes
        // keep remainder expanded (NFR8); CSS + any story with matching H2s shift content. Also absorbs the
        // in-tree Story 9.4 UX polish (evidence-block / Latest-change cue / tests-pass pill wording). [Story 9.5]
        // Regenerated for Story 9.4 polish + ADR 0007: change-surface panel (classification, AC verify list,
        // touched files, ship line), passing-tests pill, .change-surface CSS; removed Latest-change cue.
        // Regenerated for Story 9.6: action-items.html groups by epic-retro + cross-link near-dupes + visible-text
        // linkify; deferred-work.html becomes structured .deferred-item-card pages (overwrite at same path);
        // specscribe.css gains .action-items-group / .action-item-cross / .deferred-* resolved treatment.
        // Golden fixture has no open action_items and no deferred-work.md — delta is primarily shared CSS.
        // [Story 9.6; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.4 change-surface UX polish (round 2): collapsible panel (open by default),
        // classification in summary header, two-column touched list with typed links (code/new/sprint/story),
        // Ship section removed, File List paths discovered for code pages. [Story 9.4]
        // Regenerated for Story 9.7 redesign + code-review patches: story-ring peers, dashed
        // .sb-followup-open/.sb-followup-done (never .sb-done alone), Done follow-up legend, deferred
        // aggregate/gating fixes. Zero-follow-up golden fixture still omits wedges (NFR8). [Story 9.7]
        // Regenerated for Story 9.2 code-review patches: requirements tablet/mobile breakpoints (1100px
        // desktop retained), empty Non-functional donut gated, deferred∪header coverage skip, orphan epic
        // absence note, CoverageMapLine UX-DR ingest, RequirementStatTile Unmapped/Deferred sub-line.
        // [Story 9.2 review; golden-diff-normalization-gotchas]
        // Regenerated for change-surface Updated chips (sprint/story out of Touched) + Story/Sprint Status
        // icons + Dev Notes collapsible moved to end of remainder. [change-surface UX]
        // Regenerated for Story 9.8: Home gains work-mode toggle strip + Project Next Steps (wired
        // RenderProjectNextSteps) before Now & Next; Ready empty-lane InlineGuidance when undrafted;
        // specscribe.css stage filters use display:none on non-matching .wm-panel (hide-tabs IA).
        // [Story 9.8; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.9: requirements.html "Satisfaction at a glance" band (stacked bar + four
        // chips over Everything); Home satisfaction rollup → #satisfaction; LegendKey gains "Not yet mapped";
        // ProjectCounts satisfaction buckets; specscribe.css satisfaction-* / .seg.active. [Story 9.9]
        // Regenerated for Story 9.10: action-items + deferred-work list pages compress to shared
        // .followup-row scan-first grammar; heavy detail in per-row <details>; specscribe.css gains
        // .followup-row* rules and retires .action-item-card / .deferred-item-card item rules. Golden
        // fixture has no open action_items and no deferred-work.md — delta is primarily shared CSS.
        // [Story 9.10; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.8 next-steps card row: horizontal command cards with kickers/accent rails.
        // [Story 9.8 polish; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.8 work-mode jump strip: Home white bar hosts
        // Overview/Requirements/Plan/Develop/Review/Track radio toggles (hide non-matching panels).
        // [Story 9.8 work-mode strip; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.9 coherence pass: the satisfaction bar is now four proportional brackets
        // (Satisfied · In flight · Deferred · Unmapped) whose In-flight bracket keeps its real Partially/Ready/
        // Planned tier colors (so the bar matches the Sankey + donuts and Planned isn't an orphan segment); the
        // hub band gains a .satisfaction-note rollup caption; the requirements donut row leads with an "Overall"
        // six-tier donut (2×2 with the kinds); the Home rollup gains the same bracketed bar above its chips;
        // specscribe.css swaps the flat .satisfaction-bar for .satisfaction-bracket* + .satisfaction-note rules.
        // [Story 9.9 UI coherence; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.8 work-stage toggle strip: Track stage + radio visibility toggles
        // (Overview/Requirements/Plan/Develop/Review/Track); wm-show-track panels for status views.
        // [Story 9.8 Track stage; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.11: follow-ups/{slug}.html detail pages + sunburst/list per-item hrefs;
        // specscribe.css gains main.followup-detail / .followup-detail-* rules. Golden fixture has no
        // open action_items and no deferred-work.md — delta is primarily shared CSS.
        // [Story 9.11; golden-diff-normalization-gotchas]
        // Regenerated for sunburst deferred attribution: per-item deferred wedges under their epic via
        // SourceStoryId; synthetic Follow-ups slice only for unattributed action + deferred items (no
        // aggregate "N open items" wedge with mismatched sizing). Golden fixture still has no follow-ups.
        // Regenerated for Story 9.11 Next Steps polish on follow-up detail pages (reuse story-page
        // chart-panel.next-steps cards; .followup-detail .next-steps spacing).
        // Regenerated for Story 9.8 tile-band polish: 5-col CSS grid, compact tiles, curated Overview/Track
        // 2×5 (Direct/Commits demoted off Overview), Review 2×4. [Story 9.8 grid; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.9 review patches: Overall donut from ProjectCounts ledger; chip hrefs prefer
        // #at-a-glance when FR+NFR exist; Home panel gates on Everything; zero-count chips are non-links;
        // a.satisfaction-chip cursor only. [Story 9.9 code review; golden-diff-normalization-gotchas]
        // Regenerated for FollowUpRow summary: preference — deferred titles prefer authored summary:
        // over source_spec: noise (FollowUpRow.cs code page also shifts).
        // Regenerated for epic-sunburst story wedges: always link to story pages (placeholder when
        // undrafted), never #story-N-M in-page card jumps. [epic sunburst navigation]
        // Regenerated for Story 9.12: Unplanned sunburst root + sprint Unplanned lane (open quick-dev +
        // unattributable deferred); Follow-ups orphan holds unattributed action items only; CSS .sb-unplanned.
        // [Story 9.12; golden-diff-normalization-gotchas]
        // spec-accent-kicker-slug-heuristics: C#/tests only — pending-kicker CSS color rule was rejected in review
        // (contrast regression vs --ink-light); golden unchanged by that polish alone.
        // Regenerated for Story 9.13: filtered follow-ups/group-*.html pages + sunburst group-root hrefs
        // (Follow-ups → group-follow-ups, Unplanned → group-unplanned). Golden fixture still has no
        // open action_items / deferred / quick-dev — fingerprint may still move from 9.12 geometry/CSS
        // already in tree plus Charts/GroupRootHref seam. [Story 9.13; golden-diff-normalization-gotchas]
        // Regenerated for Story 9.12 provenance follow-up: deferred SourceKey; code-review-of-spec/story
        // attaches to parent quick-dev / epic; aria "from …"; residual done parents. [Story 9.12]
        // Regenerated for follow-up list column polish: .followup-group-wrap gains the shared 1040px
        // content column (+ header alignment) so group pages no longer stretch edge-to-edge; row meta
        // clustered in .followup-row-meta. [follow-up list UX]
        // Regenerated for spec-sunburst-remaining-work-hierarchy: removed task/noplan outer wedges from
        // project + epic sunbursts; weight story middle wedges by max(1, TasksTotal); nest story-child
        // deferred under parent stories (outer ring); date-based quick-dev epic attribution via retro
        // DateText and story LastUpdatedDate; review patches (hint gating, dead commands param, cascade).
        // Regenerated for 9.13 code-review patches: DeferredListHref on Unplanned pages, sprint
        // provenance <a>, watch WriteFollowUpGroupPages + group-* prune, unplanned-only legend, CSS
        // div.sprint-card hover / source-ref links. [Story 9.13 review]
        // Regenerated 2026-07-18: fingerprint on main already drifted from expected 0e02f9c5… (same
        // 9821c3f2… with or without watch RefreshFollowUpSurfaces); refresh the byte-parity gate.
        // Regenerated 2026-07-18: TaskSunburst nests deferred under an inner Deferred parent wedge
        // (shared angular budget with tasks) instead of a full-circle outer fringe.
        // Regenerated 2026-07-18: story weight includes nested story-child deferred count
        // (max(1, TasksTotal + nested)); sunburst hints say "tasks + nested deferred" when present.
        // [spec-9-7-deferred-angular-weight-and-ledger-assert]
        // Regenerated 2026-07-18: unify sticky-nav scroll-margin-top onto var(--nav-offset)
        // (.ac-criterion, .req-index .section-divider[id], .code-line) + case-insensitive req linkify
        // and deferred-work Epic 1 ledger closes. [spec-epic1-deferred-debt-cleanup]
        // Regenerated 2026-07-18: undrafted Story 1.2 gains minimal AC; epic-card note-above-AC
        // reorder — exposes AC reorder branch to byte-parity. [spec-epic9-deferred-debt-cleanup]
        // Regenerated 2026-07-18: Git Pulse title/empty copy name the 200-commit window; file-bar rows gain
        // aria-label + decorative track aria-hidden; comment CSS host comment only (no selector change).
        // [spec-3-1-deferred-debt-cleanup]
        // Regenerated 2026-07-18: Story 10.1 journey nav — Home/Delivery/Insights/Follow-ups/Project groups
        // via native <details>, Spec in Project, Structure stays retired; every page's nav bytes change.
        // Regenerated 2026-07-18: Story 10.2 chart metadata — Charts.Framed + real-value heatmap legend +
        // ranking/window/why slots on Git Pulse / deep analytics / git insights; specscribe.css .chart-frame-*.
        // Regenerated 2026-07-18: Story 10.3 glossary + in-place vocabulary — new how-to-read.html orientation
        // page (written every run); every page's nav gains a "How to read this portal" Project-group entry +
        // icon; specscribe.css gains abbr[title]/.howtoread-* rules. This non-module fixture has no _bmad
        // folder, so ModuleContext is None — the glossary is empty and the abbr expander is a no-op (AC2);
        // the delta here is purely the new page + the shared nav/CSS additions.
        // Regenerated 2026-07-18: Story 10.5 document-rendering legibility — [[wiki-link]]/bare file:line/
        // [ASSUMPTION:] chips via ReferenceChipRenderer (AC1); Toc.RenderSidebar groups h3 children under a
        // collapsible <details class="toc-group"> parent on every long TOC-bearing page (AC2); epic pages gain
        // a collapsed Retired <details> section for classified retirement/superseded story leading-comments,
        // e.g. Story 3.4 on epic-3 (AC3); specscribe.css gains .ref-chip/.assumption-tag/.toc-group/.retired-section.
        // Regenerated 2026-07-18: Story 10.6 insight-chart context polish — GitMetrics.ClassifyCoupling adds a
        // Kind column + badge to the coupling table and a dashed-edge class to process-coupled graph edges, plus
        // Charts.ChartMeta gains a Note slot (unused by this fixture — no process-classified pairs, so byte
        // delta here is limited to the new empty Kind <th>/<td> cells); CommitHeatmap's young-repo trim/
        // first-commit marker and GitInsightsTemplater's sole-contributor reword are both no-ops on this fixture
        // (old enough repo, multi-contributor); specscribe.css gains .chart-frame-note/.coupling-kind*/
        // .process-edge/.heatmap-first-commit* rules (shared stylesheet, so every page's <link> byte count moves).
        // Regenerated for Story 10.7: home + epics-index glance sunburst panels gain a NEW "Remaining Work by
        // Epic" panel — Charts.SunburstCompanionList emits a small tile grid (own chart-panel, owner-directed
        // polish pass: a plain link list read as underwhelming, so it became its own bordered/accented panel
        // matching the .next-step-card/.epic-mosaic-card grammar) below the sunburst panel on every page with
        // an epic; specscribe.css gains .epic-remaining-grid/.epic-remaining-tile/.sb-story-summary rules
        // (this fixture's epics stay under the 8-story density-collapse threshold, so the summary-wedge class
        // itself never renders here — only the shared stylesheet + new tile-grid panel shift the hash).
        // EpicSunburst's epic-level peers (action/deferred/quick-dev) now aggregate into one open/done wedge
        // instead of per-item leaves, but this fixture has no open action_items/deferred-work.md, so that
        // branch is a no-op here too.
        // Regenerated for deferred-diagnostic-severity-bucketing: specscribe.css gained .status-badge.diag-info
        // (a third diagnostics-severity badge tone). This fixture emits no unrecognized-top-level-folder
        // notice, so only the shared stylesheet content shifted the hash.
        // Regenerated for a Story 10.7 code-review self-catch + owner follow-up: (1) fixed a real bug — the
        // tile-grid CSS comment literally contained "--status-*/chart-local" (no space), and CSS reads `*/` as
        // a comment terminator wherever it appears, so that single character sequence silently truncated the
        // ENTIRE rest of specscribe.css from the browser's perspective (only ~446 of ~1400 rules ever parsed,
        // corrupting nearly every page's visible styling — caught by the owner reporting the home page looked
        // broken, root-caused via document.styleSheets[0].cssRules.length in-browser). (2) owner review of the
        // live tile grid: status was color-only (left accent bar, no text) — added a visible
        // .epic-remaining-status label span (UX-DR17); a fully-done epic with zero open follow-ups now has
        // nothing left to report so it's omitted from the panel entirely (NFR8) rather than showing a bare,
        // uninformative "N stories" tile — this fixture's two epics both stay under 8 stories and have no
        // follow-up geometry, so the visible delta here is the label span/status text + the CSS byte fix only.
        // Regenerated for Story 10.8: specscribe.css gains the shared .list-row(-scan|-summary|-meta|-chip|-primary)
        // family (combined selectors alongside .followup-row/.timeline-row so their shared visuals stay in one
        // place — shifts every page's shared stylesheet content). The epics-index empty-state guidance now routes
        // through ListRow.EmptyState (byte-identical output; this fixture always has epics so the branch never
        // fires). Stable across 3 repeated runs before locking in (known stale-first-hash trap).
        // Regenerated for Story 10.10: the white sub-header band now carries page-type-specific local context
        // (NavigationView.LocalContext, rendered by a new AppendKeyViewsBand branch) instead of the generic
        // quick-links band on every epic/story/code/ADR/commit/requirement/follow-up-detail page this fixture
        // exercises; specscribe.css gains .site-nav-local-context/.local-context-* rules (shared stylesheet, so
        // every page's byte count moves too). Stable across 3 repeated runs before locking in.
        // Regenerated again same story: the fallback rule was tightened so a local context containing ONLY the
        // current (active) item — nothing else to navigate to, e.g. this fixture's lone-file code directories —
        // degrades to the generic band instead of a degenerate one-item band (NFR8). Stable across 2 runs.
        // Regenerated for Story 10.9: ListRow.Render/FollowUpRow.Render gain additive data-sort-* attrs, and the
        // four in-scope <ul> wrappers (action items, deferred work, follow-up groups, synthesized ADR landing)
        // gain the js-listable opt-in class; specscribe.js/specscribe.css gain the enhanceListRows enhancement +
        // .list-controls*/.list-row-group-heading rules (shared assets, so every page's byte count moves too).
        // Two other stories landed on main concurrently with this one (unrelated commits between HEAD f0f30bd
        // and d274cee), each independently shifting this fixture's byte content — the hash was re-verified
        // stable across 2 repeated runs against the final concurrent state before locking in.
        // Regenerated for Story 10.10 follow-up: local-context bands beyond HtmlRenderAdapter.LocalContextInlineLimit
        // (8) now collapse into a "More (N)" disclosure reusing the existing .key-view-group/.key-view-trigger/
        // .key-view-panel pattern (no new CSS/JS — same class family, so the shared assets are byte-identical this
        // time). This fixture's every local-context family (2 stories/epic, 1 FR, 1 NFR) stays well under 8, so the
        // disclosure branch itself never fires here, and the refactored non-overflow code path is verified
        // byte-for-byte equivalent to what it replaced. The hash was genuinely UNSTABLE run-to-run while this
        // repo's background auto-committer was actively landing unrelated concurrent commits on `main` during
        // this session (same shared-main gotcha noted above for Story 10.9) — three consecutive back-to-back
        // runs finally agreed once that settled; locked in against that final stable state.
        // Regenerated for Story 10.11: sibling pagers (epic/story/ADR/retro pages) moved from floating inside
        // .doc-header to a new .page-wayfinding strip alongside the breadcrumb, and .entity-pager dropped its
        // absolute positioning (now a flex item); the TOC gained a chrome-level active-section-tracking script
        // appended after every TOC-bearing HTML page's <main> (epic/story pages here — the non-git fixture has
        // no code/commit/date pages). specscribe.css gained .page-wayfinding/.toc-link.is-current and dropped
        // .doc-header's position:relative. Verified stable across 3 repeated runs before locking in.
        // Regenerated for Story 10.1 code-review patches: Project group child order now Readme/PRD/Architecture/
        // Spec/ADRs (Spec before ADRs, matching the taxonomy table); Follow-ups gained a dedicated
        // family-followups CSS accent (was inheriting family-epics/Delivery's color). Verified stable across
        // repeated runs before locking in.
        // Regenerated for Story 10.1 deferred debt cleanup: the .list-batch-actions .next-steps-cards CSS
        // comment was corrected (no markup/rule change, but the shared stylesheet's byte content moves, so
        // every page's asset-cache-busted byte count shifts too). This repo's background auto-committer was
        // landing unrelated concurrent commits on `main` during this session (same shared-main gotcha as
        // Story 10.10) — re-verified stable across repeated runs against the settled state before locking in.
        // Regenerated for Story 10.2 code-review patches: dropped the redundant .chart-frame-head
        // .git-pulse-files-title CSS rule, added .heatmap-window { white-space: normal; }, Timeline's heatmap
        // now gets a chart-frame-why sentence, and the heatmap legend skips levels no cell can reach at the
        // current maxCount (so small/young repos no longer show duplicate "—" swatches).
        // Regenerated for Story 10.10 review round 2: (1) dark-bar journey dropdowns no longer render `open` on
        // page load when a child is active (they stayed open through a refresh, reading as a stuck menu) — the
        // active group keeps has-active for its summary highlight but the <details> stays collapsed; every
        // fixture page whose active leaf lives in a multi-child group loses its baked-in " open" attribute.
        // (2) local-context pills + overflow-panel rows now ellipsise long labels (>28 inline / >44 panel chars)
        // with the full text on a native title tooltip, and .local-context-pill gained white-space:nowrap — this
        // fixture's local-context labels (Story N.M, FR1, NFR1) are all short so no truncation fires here; the
        // byte delta is the removed " open" attrs + the one CSS declaration. Verified stable across 3 runs.
        // Regenerated 2026-07-20 (spec-7-1-deferred-debt-cleanup): SoftSlugify encodes `/` as `x2f` (with
        // literal-x2f escaping) so SPA tab radio names stay unique; placeholder pages can emit Insights/History tabs.
        // Fixture cites no real repo files — refresh the byte-parity gate for any shared markup delta.
        // Regenerated 2026-07-20 (Story 10.4 code-review patches): evidence-strip verified dates route through
        // PortalDates.Day; FreeTextBadge CSS class uses the first status word so "Superseded by …" hits
        // .pill.status-superseded; doc header date pills normalize via ReformatAuthored; ### Change Log
        // panels sequence like ## Change Log.
        // Regenerated 2026-07-20 (spec-10-4-deferred-debt-cleanup): linked Code Map treemap cells move tip/aria
        // onto the wrapping <a> and drop nested tabindex on the <rect>; review patches also update colorize JS
        // labelHost + CSS :focus-within ring for the same Tile pattern (shared asset delta).
        // Regenerated for Help nav: SDD/About/Logs moved into an always-present Help group (dark bar + key-views);
        // Spec-Driven Development no longer leads Project. Verified stable across 2 runs before locking in.
        // Regenerated 2026-07-20 (Story 10.7 code-review patches): specscribe.css gains one .epic-remaining-unrecognized
        // accent rule (shared stylesheet, so the byte-parity gate moves); the paired EpicSunburst peer-aggregate
        // radii nudge (0.47/0.505 → 0.465/0.495) is a no-op on this fixture, which has no epic-level follow-up peers.
        // Regenerated for Story 10.9 code-review patches: ListRow/FollowUpRow now also emit data-sort-status-rank
        // (StatusStyles.CanonicalRank) beside data-sort-status so the client sorts/groups by severity without a
        // hardcoded status order in JS; specscribe.js enhanceListRows drops the STATUS_GROUP_RANK array, reads the
        // rank attribute, no longer sets role="group" on group headings, and suppresses a group heading whose rows
        // are all filtered-out (shared assets + every opted-in <li>, so the byte-parity gate moves). Stable across
        // 2 repeated runs before locking in.
        // Regenerated for About SDD hub+subpages: Help nav splits How to use SpecScribe / About Spec-Driven
        // Development; how-to-read loses framework tabs; seven new about-sdd*.html pages + shared nav/CSS delta.
        // Regenerated 2026-07-20 (Story 10.11 code-review patches): Toc.ActiveSectionScript gains an
        // initial-load fallback (highlights the first heading when nothing intersects the rootMargin band yet),
        // and HtmlRenderAdapter.Render's TOC-detection substring now matches the full <nav class="toc-sidebar"
        // aria-label="On this page"> opening tag instead of just the class attribute (shared script/detection
        // delta on every TOC-bearing page). Verified stable across 2 repeated runs before locking in.
        // Regenerated 2026-07-20 (Story 10.11, sticky-section-nav rework): the prev/next entity Pager is
        // RETIRED from EpicPageView/StoryPageView/StoryPlaceholderView (dropped from EpicsViewBuilder's build
        // methods entirely) — every epic/story/placeholder page header loses its "‹ Prev / Next ›" pager markup,
        // superseded by this story's sticky section nav + breadcrumb coherence. FollowUpRow also gains the
        // data-sort-status-rank attribute (StatusStyles.CanonicalRank) alongside data-sort-status, bringing it to
        // parity with ListRow's existing Story 10.9 convention.
        // Regenerated for About SDD matrix: ArtifactBundle family columns (Epics & Stories, Requirements,
        // Sprint, Retros, Planning docs, Commands); Supported (green) + Detected (blue) badges; dropped
        // "In this project" matrix column.
        // Regenerated for About SDD BMad methodology: vertical state diagram gains "In a Sprint" composite
        // (create → develop → review loop + optional /bmad-correct-course), explicit output labels
        // ("Product Brief Created", etc.), and get-started copy places the official-docs sentence after install.
        // Regenerated 2026-07-20 (Story 10.10 code-review patches): the local-context band's "More" overflow
        // panel now reuses AppendLocalContextPill (self-link guard) instead of hand-rendering its own <a> —
        // cosmetic indentation (10sp → 6sp) and attribute-order (class/href → href/class) delta on every
        // panel row, shared markup so the byte-parity gate moves. Verified stable across 3 repeated runs
        // before locking in.
        // Regenerated 2026-07-20 (Epic 10 deferred-work cleanup): Charts.Sunburst now tracks hasVisibleNoPlan
        // (only true when a no-plan story wedge is actually un-collapsed and drawn) instead of hasNoPlan over
        // every story regardless of dense-collapse — this fixture's project-glance sunburst has a dense (8+
        // story) epic whose no-plan stories were previously advertising an orphaned "No task plan" legend
        // swatch matching no visible wedge; the swatch is now correctly suppressed. Isolated via stash-bisection
        // against the other 5 fixes in this pass (ReferenceChipRenderer kbd/samp shielding, EpicsParser preamble
        // retirement-comment scan, AbbreviationExpander punctuation separators, SiteGenerator groupByHref
        // assert) — none of those touch this non-git fixture's content; only Charts.cs shifted the hash.
        // Verified stable across 3 repeated runs before locking in.
        // Regenerated 2026-07-20 (Story 6.4 deferred-work cleanup, not a rendering change): the file-separator
        // literal `"\n \n"` above had a single byte corrupted to a NUL in this repo's on-disk source (this
        // working tree's shared-main concurrent-write gotcha — same class of issue documented above for
        // Stories 10.9/10.10/10.1) — confirmed via a raw byte compare against the last-good commit (exactly one
        // byte differed, file size unchanged) and via stash-bisection isolating the hash shift to this fix alone
        // (unrelated to this pass's actual production changes, none of which touch the non-git fixture's static
        // HTML path). Restoring the intended space changes the hash input, not any rendered page.
        // Regenerated for Story 7.9: this fixture is not a git repo (no --deep-git), so code-map.html never
        // renders here — only specscribe.css/.js shifted the hash (the new .codemap-cell.type-*/.codemap-legend-
        // swatch.type-* discrete palette + .codemap-legend-discrete/.codemap-notice-secondary rules; the JS
        // recolor()'s new "filetype" branch, swapLegend, and clearFillClasses helper). The file-type classifier
        // itself and its rendering (data-filetype/data-filetype-label, the Type tooltip row/table column, the
        // loosened hasMetrics gate) are covered by CodeMap/Charts/CodeMapTemplater tests, not this fixture.
        // Verified stable across 2 repeated runs before locking in. [golden-diff-normalization-gotchas]
        // Regenerated for Story 7.10: this fixture is not a git repo (no --deep-git) and has too few files, so
        // the new refactor-target risk quadrant section always renders below Charts.RiskQuadrantMinFiles here —
        // only specscribe.css gained the .risk-quadrant/.risk-point/.risk-quadrant-list rules; the section's
        // "Refactor-Target Risk Quadrant" heading + chart-empty body appear on code-map.html itself (a content
        // change, not CSS-only, since the section is always framed even at zero data), and the live scatter is
        // covered by Charts/CodeMapTemplater/SiteGeneratorCodeMap tests, not this fixture. Verified stable
        // across 2 repeated runs before locking in.
        // Regenerated for a Story 7.9 review-feedback follow-up (layered on top of the Story 7.10 change above,
        // which was concurrently in-flight on this shared working tree): the file-type classifier gained Python
        // (its own category — 20 real files live under this repo's .agents/.claude/_bmad scaffolding) and a
        // bounded "Other Languages" catch-all (Rust/Go/Java/Ruby/PHP/C/C++/shell/PowerShell/SQL) so a polyglot
        // repo's real language variety shows up as more than one undifferentiated "Other" swatch; the two
        // exclude-filter checkboxes' label also gained a left-margin gap (was flush against its checkbox). This
        // non-git fixture cites no real repo files, so only specscribe.css shifted the hash (two new
        // .codemap-cell.type-*/.codemap-legend-swatch.type-* rules + the checkbox-label spacing fix). Verified
        // stable across 2 repeated runs before locking in. [golden-diff-normalization-gotchas]
        // Regenerated again: a concurrent session's commit (5331d11 "fix: Story 7.2 deferred-work cleanup
        // (code-reference resolution hardening)") landed on this shared main working tree after the Story 7.9
        // follow-up hash above was locked, shifting this fixture's rendered output independent of any Story 7.9
        // change. Verified stable across 2 repeated runs before locking in.
        // Regenerated for Story 7.11 (Ownership & Bus-Factor Insights): a new section on the Git Insights hub
        // (git-insights.html), no other page affected. This fixture is not a git repo (no --deep-git), so
        // git-insights.html is never generated here — the shift comes purely from specscribe.css's two new
        // .gi-risk-badge/.gi-solo-repo-note rules. Verified stable across 2 repeated runs before locking in.
        // Regenerated for Story 7.12 (Code Freshness / Age Map): a new "Code Freshness" section (server-rendered
        // sunburst + legend + caption) landed on code-map.html, plus specscribe.css gained the
        // .freshness-sunburst/.freshness-wedge*/.freshness-legend* rules. This fixture's repo-root walk finds its
        // own markdown files, so code-map.html DOES render here — both the new markup and the CSS shift the hash
        // (all-neutral wedges, since this fixture is not a git repo). Verified stable across 2 repeated runs
        // before locking in. [golden-diff-normalization-gotchas]
        // Regenerated for Story 7.9 code-review patches: the SSR-baked aria-label on a hasMetrics:false treemap
        // cell now includes the file-type category (color is never the sole signal, AC #1) — a content change on
        // code-map.html since this fixture's repo-root walk renders it; plus a new --violet custom property and
        // two rule value changes (.codemap-cell.type-other-lang/.codemap-legend-swatch.type-other-lang moved off
        // --ink-light) in specscribe.css. The Dockerfile/.editorconfig classifier consistency fix and the
        // Classify-call-site trailing-slash fix are pure classification logic with no effect on this fixture's
        // file set. Verified stable across 2 repeated runs before locking in. [golden-diff-normalization-gotchas]
        const string expected = "d68934f0058d26841e29d24fd8a56ea1c3fcb5c3ffaefd848f7de32218c91ba1";
        Assert.True(
            expected == fingerprint,
            $"Rendered output content changed. If this was an intentional rendering change, update the constant "
            + $"to:\n  {fingerprint}\nOtherwise this is an unexpected drift from the byte-parity baseline (AC #1).");
    }

    // The footer date is now human-friendly + culture-invariant, e.g. "on July 10, 2026 at 5:14 PM".
    // Story 10.4: the footer clock is now "on {PortalDates day} at HH:mm UTC±HH:mm" (24-hour + machine-local zone
    // label). Widened from the old 12-hour "[AP]M" shape so the per-machine time+zone can't leak into the hash.
    private static readonly Regex FooterClock = new(@"on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}", RegexOptions.Compiled);
    private static readonly Regex AssetCacheBust = new(@"\?v=[0-9a-fA-F]+", RegexOptions.Compiled);
    private static readonly Regex SubtitleVersion = new(@"SpecScribe v[^<]+", RegexOptions.Compiled);
    private static readonly Regex VersionRow = new(@"(<dt>Version</dt><dd>)[^<]*(</dd>)", RegexOptions.Compiled);
    // The About page's dynamic build identifier (build date · short commit hash) varies per build/commit.
    private static readonly Regex BuildRow = new(@"(<dt>Build</dt><dd>)[^<]*(</dd>)", RegexOptions.Compiled);

    /// <summary>SHA-256 over every output file's normalized content (path-prefixed, ordinal-sorted), so ANY
    /// rendered-byte change flips one hash. Normalizes only per-run/per-build/per-machine noise, never artifact
    /// content — so the constant is portable across machines and CI, not pinned to this box.</summary>
    private string FingerprintTree(string root)
    {
        var sb = new StringBuilder();
        foreach (var rel in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(root, p)))
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(FoldToday(rel)).Append('\n')
              .Append(NormalizeVolatile(File.ReadAllText(Path.Combine(root, rel)))).Append("\n \n");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>Folds today's date (the ISO filename/href form and the readable heading form) to stable
    /// placeholders. Story 7.3's artifact-mtime date page + timeline are stamped with the generation date, so
    /// without this the fingerprint would drift day to day even with no rendering change.</summary>
    private static string FoldToday(string s)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return s.Replace(Charts.DReadable(today), "<date-readable>").Replace(Charts.D(today), "<date-iso>");
    }

    private string NormalizeVolatile(string content)
    {
        content = content.Replace("\r\n", "\n");
        content = FoldToday(content);
        // The diagnostics page prints the ABSOLUTE repo root (a random per-run temp dir, and machine-specific);
        // fold every form of the fixture root to a placeholder so the golden pins rendered content, not the box.
        content = content.Replace(PathUtil.NormalizeSlashes(_root), "<root>").Replace(_root, "<root>");
        content = FooterClock.Replace(content, "on <ts>");
        content = AssetCacheBust.Replace(content, "?v=<ver>");
        content = SubtitleVersion.Replace(content, "SpecScribe v<ver>");
        content = VersionRow.Replace(content, "$1<ver>$2");
        content = BuildRow.Replace(content, "$1<build>$2");
        return content;
    }

    [Fact]
    public void GenerateAll_UnusableSprintYaml_ReportsSkippedDiagnosticAndSiblingsStillRender()
    {
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();

        // AC #2: the unsupported shape is categorized and reported as non-fatal on the existing event
        // channel — never an Error, never an abort…
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        var diag = Assert.Single(events, e => e.Outcome == GenerationOutcome.Skipped && e.RelativePath.EndsWith("sprint-status.yaml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("development_status", diag.Message);

        // …and every successful artifact still renders, while the sprint surfaces omit cleanly.
        Assert.False(File.Exists(Path.Combine(Site, "sprint.html")));
        Assert.True(File.Exists(Path.Combine(Site, "index.html")));
        Assert.True(File.Exists(Path.Combine(Site, "epics.html")));
        Assert.True(File.Exists(Path.Combine(Site, "epics", "story-1-1.html")));
        Assert.DoesNotContain("href=\"sprint.html\"", File.ReadAllText(Path.Combine(Site, "index.html")));
    }

    [Fact]
    public void GenerateAll_UnrecognizedTopLevelFolder_RendersPageAndReportsStructureNotice()
    {
        // Story 4.2 Tasks 3/5: an unknown folder emits one categorized non-fatal structure notice on the
        // diagnostic channel (input for Story 4.8's page) and still renders its doc's page. The home index band
        // for the folder was removed by spec-declutter-home-dashboard (the page stays reachable by direct URL).
        Directory.CreateDirectory(Path.Combine(Source, "design-notes"));
        File.WriteAllText(Path.Combine(Source, "design-notes", "ideas.md"), "# Ideas\n\nBody.\n");

        var events = new SiteGenerator(Options()).GenerateAll();

        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        var notice = Assert.Single(events, e => e.Outcome == GenerationOutcome.Skipped && e.RelativePath == "design-notes/");
        Assert.Contains("unrecognized top-level folder", notice.Message);
        // Informational (not Unsupported): a benign structural notice must not share a diagnostics-page bucket
        // with a genuine per-artifact ingestion failure. [deferred-diagnostic-severity-bucketing]
        Assert.StartsWith("[Informational]", notice.Message);

        // The doc page still renders; the home no longer carries the (removed) unrecognized-folder index band.
        Assert.True(File.Exists(Path.Combine(Site, "design-notes", "ideas.html")));
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.DoesNotContain("Design Notes</div>", index);
        Assert.DoesNotContain("href=\"design-notes/ideas.html\"", index);
    }

    [Fact]
    public void GenerateAll_NormalBmadLayout_DoesNotEmitUnrecognizedNoticeForAdrsDocsOrRetros()
    {
        // Pins the path model behind the closed Epic 4 KnownIndexGroups debt: UnrecognizedTopLevelFolders walks
        // SourceRoot only. Separate AdrSourceRoot (docs/adrs) never enters sourceRelatives; retros live under
        // already-well-known implementation-artifacts/. A normal BMad fixture must not emit unrecognized-folder
        // notices — and adrs/docs/retros must stay OUT of the well-known set (a no-op whitelist must fail this pin).
        // [spec-close-known-index-groups-misdiagnosis]
        Assert.False(HtmlTemplater.IsWellKnownTopLevelFolder("adrs"));
        Assert.False(HtmlTemplater.IsWellKnownTopLevelFolder("docs"));
        Assert.False(HtmlTemplater.IsWellKnownTopLevelFolder("retros"));

        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        Assert.DoesNotContain(events, e =>
            e.Outcome == GenerationOutcome.Skipped
            && e.Message is not null
            && e.Message.Contains("unrecognized top-level folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateAll_CleanFixture_ProducesAboutAndAllClearDiagnostics()
    {
        // Story 4.8: both pages are written on every full run. This fixture is clean (valid sprint yaml, only
        // well-known folders), so the diagnostics page renders the all-clear state, not an empty table.
        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        Assert.True(File.Exists(Path.Combine(Site, "about.html")));
        var diag = File.ReadAllText(Path.Combine(Site, "diagnostics.html"));
        Assert.Contains("No notices", diag);
        Assert.DoesNotContain("diagnostics-table", diag);
        // AC #2: the effective-config disclosure still renders in the all-clear case, carrying the run's config.
        Assert.Contains("Effective configuration", diag);
        Assert.Contains("<dt>Output directory</dt>", diag);
        Assert.Contains("<dt>Deep-git analytics</dt>", diag);
    }

    [Fact]
    public void GenerateAll_UnusableSprintYaml_DiagnosticsPageListsNoticeExactlyOnce()
    {
        // The same unsupported-sprint fixture the diagnostic-channel test uses: it must surface as exactly ONE
        // row on the diagnostics page (no double-count — each adapter diagnostic is mapped into the events list
        // once), carrying its fine "Unsupported" category word. [Story 4.8 Task 2/7]
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        new SiteGenerator(Options()).GenerateAll();
        var diag = File.ReadAllText(Path.Combine(Site, "diagnostics.html"));

        Assert.Contains("diagnostics-table", diag);
        // The doc-subtitle pins the notice count — "1 notice" proves the single mapped diagnostic isn't doubled.
        Assert.Contains("&middot; 1 notice &middot;", diag);
        Assert.Equal(1, Count(diag, "diagnostics-source"));
        Assert.Contains(">Unsupported</span>", diag);
        Assert.Contains("sprint-status.yaml", diag);
    }

    [Fact]
    public void GenerateAll_UnusableSprintYaml_DiagnosticsWireMirrorsThePagesNoticeSet()
    {
        // AC #2 coherence (Story 6.12): the `webview` command's JSON-lines stderr channel and the Story 4.8
        // diagnostics page derive from the SAME DiagnosticNotice.FromEvents(events) projection, so the two
        // surfaces can never disagree. DiagnosticsPageListsNoticeExactlyOnce (above) pins "1 notice" on this exact
        // malformed-sprint fixture for the PAGE; here the same fixture feeds the WIRE — same count, same anchored
        // source path, no double-count.
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        var options = Options();
        var events = new SiteGenerator(options).GenerateAll();
        var notices = DiagnosticNotice.FromEvents(events);

        // Exactly the page's set: one non-fatal, source-anchored sprint-status.yaml skip.
        var notice = Assert.Single(notices);
        Assert.Equal(DiagnosticAnchorRoot.Source, notice.AnchorRoot);
        Assert.EndsWith("sprint-status.yaml", notice.SourcePath, StringComparison.OrdinalIgnoreCase);

        // …and the wire is a faithful mirror of that same set: one anchored, repo-relative, forward-slashed line.
        var line = Assert.Single(WebviewCommand.SerializeDiagnostics(notices, options)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToList());
        Assert.True(line.GetProperty("fileAnchored").GetBoolean());
        Assert.EndsWith("sprint-status.yaml", line.GetProperty("path").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\\', line.GetProperty("path").GetString()!);
    }

    [Fact]
    public void GenerateAll_FooterAboutLink_ResolvesFromRootAndNestedPages()
    {
        // The site-wide footer gains an About link on EVERY page (the deliberate Story 4.8 output change); its
        // relative href must resolve from both a root page and a nested one.
        new SiteGenerator(Options()).GenerateAll();

        // Root page → bare href; depth-1 pages (adrs/, epics/) → "../about.html".
        Assert.Contains("href=\"about.html\"", File.ReadAllText(Path.Combine(Site, "index.html")));
        Assert.Contains("href=\"../about.html\"", File.ReadAllText(Path.Combine(Site, "adrs", "index.html")));
        Assert.Contains("href=\"../about.html\"", File.ReadAllText(Path.Combine(Site, "epics", "story-1-1.html")));
        // The About page links on to the diagnostics run log (the reachability path's final hop).
        Assert.Contains("href=\"diagnostics.html\"", File.ReadAllText(Path.Combine(Site, "about.html")));
    }

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    [Fact]
    public void IsEpicsRelated_ToleratesNestedLocations()
    {
        // Watch routing classifies via the adapter's shared conventions (Story 4.2 Task 4): the epics file by
        // name anywhere, story artifacts by implementation-artifacts/ ancestor at any depth.
        var gen = new SiteGenerator(Options());

        Assert.True(gen.IsEpicsRelated(Path.Combine(Source, "nested", "epics.md")));
        Assert.True(gen.IsEpicsRelated(Path.Combine(Source, "tracking", "implementation-artifacts", "1-4-x.md")));
        Assert.False(gen.IsEpicsRelated(Path.Combine(Source, "planning-artifacts", "prd.md")));
    }

    [Fact]
    public void GenerateAll_AllDoneEpicWithoutRetrospective_RendersAsInReview()
    {
        // Story 6.2 harmonized the epic-status VISUAL surfaces onto StatusStyles.ForEpicWithRetrospective: an epic
        // whose every story is done but which has NO retrospective reads as "In review" (delivered, retro pending)
        // rather than "Done". Epic 2 in this fixture is exactly that case (Story 2.1 done, no epic-2 retro). This
        // pins the branch the golden fingerprint now exercises — it was previously invisible because the only
        // fixture epic had an in-progress story. [Story 6.2 review]
        new SiteGenerator(Options()).GenerateAll();

        // The epic HEADER badge reads "In review". No story here is in review (Story 2.1 is done), so a
        // review-class status badge on this page can only be the epic's own header badge.
        var epic2 = File.ReadAllText(Path.Combine(Site, "epics", "epic-2.html"));
        Assert.Contains("<span class=\"status-badge review js-tip\"", epic2);

        // …and the epics-index chip for Epic 2 agrees (the same retro-gated classifier), so the surfaces are
        // consistent rather than one reading "Done" and another "In review".
        var epicsIndex = File.ReadAllText(Path.Combine(Site, "epics.html"));
        Assert.Contains("epic-chip review", epicsIndex);
    }

    [Fact]
    public void GenerateAll_ThenRegenerateEpics_KeepsWatchParity()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // A watch-mode epics edit: retitle the story, then run the incremental path the watcher uses.
        File.WriteAllText(
            Path.Combine(Source, "planning-artifacts", "epics.md"),
            EpicsMd.Replace("Foundation Story", "Renamed Story"));
        var ev = gen.RegenerateEpics();

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        Assert.Contains("Renamed Story", File.ReadAllText(Path.Combine(Site, "epics", "story-1-1.html")));
        Assert.Contains("Renamed Story", File.ReadAllText(Path.Combine(Site, "epics.html")));
    }
}
