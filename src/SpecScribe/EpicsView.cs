namespace SpecScribe;

/// <summary>One epic "chip" (the compact vertical-slice / further-development overview links) as DATA: the epic
/// number, its title (already-projected inline HTML), its <see cref="StatusStyles.ForEpicWithRetrospective"/>
/// status class, and its drill href. Mirrors the story's <c>EpicChip { Number, Title, Status, Href }</c>. [Story 6.2]</summary>
/// <param name="Number">The epic number.</param>
/// <param name="TitleHtml">The epic title as already-projected inline HTML (carried verbatim, as the chip renders it raw).</param>
/// <param name="StatusClass">The <see cref="StatusStyles.ForEpicWithRetrospective"/> css class (done/review/active/ready/drafted/pending — a retro-gated visual surface).</param>
/// <param name="Href">The drill href to the epic page (e.g. <c>epics/epic-1.html</c>).</param>
public sealed record EpicChip(int Number, string TitleHtml, string StatusClass, string Href);

/// <summary>One story card on an epic page as DATA — the story's identity + status + task tally + drill targets
/// (the checkable "section facts" the parity harness asserts), PLUS the inherently-HTML prose the card also
/// renders as NAMED OPAQUE fragments (<see cref="UserStoryHtml"/>, <see cref="AcBlocksHtml"/>,
/// <see cref="NoteHtml"/>) — the deferred prose-decomposition seam (memory: charting-is-pure-svg-no-js applies to
/// prose too: Markdig HTML is carried, not re-modelled). Hrefs are already prefix-resolved for the epic page.
/// Mirrors the story's <c>StoryCardView { Id, Title, StatusStage, TaskBadge, Href }</c>. [Story 6.2]</summary>
public sealed record StoryCardView
{
    /// <summary>The story id "N.M".</summary>
    public required string Id { get; init; }

    /// <summary>The story title as already-projected inline HTML.</summary>
    public required string TitleHtml { get; init; }

    /// <summary>The in-page anchor id for the card (<c>story-N-M</c>) — the epic-sunburst jump target for an
    /// undrafted story and the epic-page TOC entry.</summary>
    public required string AnchorId { get; init; }

    /// <summary>The <see cref="StatusStyles.ForStory"/> status stage (active/review/ready/drafted/…).</summary>
    public required string StatusStage { get; init; }

    /// <summary>The RAW status word for the badge, or null when the story has no status (no badge rendered).</summary>
    public string? Status { get; init; }

    /// <summary>Done/total task-checkbox tally — the "task badge" data (badge shown only when total &gt; 0).</summary>
    public required int TasksDone { get; init; }

    /// <summary>Total task-checkbox count.</summary>
    public required int TasksTotal { get; init; }

    /// <summary>The prefix-resolved href the title links to: the drafted artifact page, or the story's generated
    /// placeholder page for an undrafted story (never a dead end).</summary>
    public required string TitleHref { get; init; }

    /// <summary>The prefix-resolved "View full story plan →" href, or null for an undrafted story (which shows a
    /// create-story note instead).</summary>
    public string? ViewPlanHref { get; init; }

    /// <summary>The story narrative as already-projected HTML (named opaque fragment).</summary>
    public required string UserStoryHtml { get; init; }

    /// <summary>The epics.md comment above the narrative, pre-rendered as a block-level <c>.md-comment</c>
    /// annotation; "" when absent. Rendered as its own block above the user-story blurb (named opaque
    /// fragment).</summary>
    public string UserStoryNoteHtml { get; init; } = string.Empty;

    /// <summary>The acceptance-criteria blocks as already-projected HTML (named opaque fragments).</summary>
    public required IReadOnlyList<string> AcBlocksHtml { get; init; }

    /// <summary>The pre-rendered "no plan yet — draft it with…" guidance HTML for an undrafted story, or null
    /// when the story has an artifact. Command-catalog driven, so pre-rendered (named opaque fragment).</summary>
    public string? NoteHtml { get; init; }

    /// <summary>Generation-time "Updated &lt;date&gt;" marker for the card header — resolved from git or the
    /// story change log by <see cref="ProgressCalculator"/>; null omits the marker (AC #2). Never a wall clock.
    /// [Story 8.8]</summary>
    public DateOnly? UpdatedDate { get; init; }
}

/// <summary>One "Dev Agent Record" row as DATA: the row label + its already-projected content HTML (a named
/// opaque fragment). Replaces the former inline tuple so the record serializes cleanly (AC #2). [Story 6.2]</summary>
public sealed record DevAgentEntry(string Label, string ContentHtml);

/// <summary>The host-neutral SECTION view model for the EPICS INDEX page body. The data-shaped sections are
/// records (header counts, the two <see cref="EpicChip"/> chip sections); the progress panel + sunburst carry
/// their already-projected domain inputs (<see cref="Progress"/>, <see cref="Epics"/>); the overview banner,
/// requirements inventory, and roadmap are NAMED OPAQUE fragments carried on the <see cref="Epics"/> model
/// (<c>OverviewHtml</c>/<c>RequirementsInventoryHtml</c>) or rebuilt from it (the Mermaid roadmap). The epic
/// cards render from the <see cref="Epics"/> domain input + <see cref="Commands"/> (command-driven guidance is
/// disproportionate to model as data). [Story 6.2]</summary>
public sealed record EpicsIndexView
{
    /// <summary>The project name — the header subtitle.</summary>
    public required string SiteTitle { get; init; }

    /// <summary>Total epic count (header subtitle).</summary>
    public required int EpicCount { get; init; }

    /// <summary>Count of epics with stories drafted (header subtitle).</summary>
    public required int DraftedCount { get; init; }

    /// <summary>The progress snapshot — mosaic + funnel detail (per-epic). Headline counts come from
    /// <see cref="Counts"/>. [Story 6.2; Story 8.3]</summary>
    public required ProgressModel Progress { get; init; }

    /// <summary>The portal-wide count ledger — EpicCount/DraftedCount/stat-grid sources. [Story 8.3]</summary>
    public required ProjectCounts Counts { get; init; }

    /// <summary>The epics model — the sunburst input, the chip/card source, and the carrier of the overview +
    /// requirements-inventory opaque fragments and the roadmap diagram input.</summary>
    public required EpicsModel Epics { get; init; }

    /// <summary>The command catalog the sunburst + empty-state guidance + epic cards consume.</summary>
    public required CommandCatalog Commands { get; init; }

    /// <summary>The "Vertical Slice" chip section (empty → omitted).</summary>
    public required IReadOnlyList<EpicChip> VerticalSliceChips { get; init; }

    /// <summary>The "Further Development" chip section (empty → omitted).</summary>
    public required IReadOnlyList<EpicChip> FurtherDevelopmentChips { get; init; }

    /// <summary>Open follow-ups for the project sunburst's story-ring peers + Follow-ups orphan. Defaults to
    /// <see cref="FollowUpGeometry.Empty"/>. [Story 9.7]</summary>
    public FollowUpGeometry FollowUps { get; init; } = FollowUpGeometry.Empty;

    /// <summary>Unplanned / one-off work for the project sunburst Unplanned root. [Story 9.12]</summary>
    public UnplannedWorkGeometry UnplannedWork { get; init; } = UnplannedWorkGeometry.Empty;
}

/// <summary>The host-neutral SECTION view model for a single EPIC page body. The header + progress bars + the
/// <see cref="StoryCards"/> list are data; the epic sunburst renders from the <see cref="Epic"/> domain input;
/// the intro goal/meta, the up-next / next-steps panel, and the retro affordance are NAMED OPAQUE fragments
/// (command/prose driven, disproportionate to model). [Story 6.2]</summary>
public sealed record EpicPageView
{
    /// <summary>The epic number (header kicker).</summary>
    public required int Number { get; init; }

    /// <summary>The epic title as already-projected HTML (the <c>&lt;h1&gt;</c>).</summary>
    public required string TitleHtml { get; init; }

    /// <summary>The <see cref="StatusStyles.ForEpicWithRetrospective"/> status class — the header badge stage AND
    /// the page's interaction status stage (a retro-gated visual surface: an all-done epic with no retro reads
    /// "review").</summary>
    public required string StatusClass { get; init; }

    /// <summary>The header status badge label (<see cref="StatusStyles.EpicLabel"/>).</summary>
    public required string StatusLabel { get; init; }

    /// <summary>The epic goal as already-projected HTML (named opaque fragment; empty when absent).</summary>
    public required string GoalHtml { get; init; }

    /// <summary>The epic FR-meta line as already-projected HTML (named opaque fragment), or null.</summary>
    public string? FrMetaHtml { get; init; }

    /// <summary>True when the epic has at least one story — selects the progress-bars + sunburst layout over the
    /// next-steps-only layout.</summary>
    public required bool HasStories { get; init; }

    /// <summary>The "Epic Progress" bars (Stories detailed + optional Tasks), forks resolved (data).</summary>
    public required IReadOnlyList<ProgressBarView> ProgressBars { get; init; }

    /// <summary>The pre-rendered "Up Next" + next-actions panel HTML (command/prefix driven named opaque
    /// fragment), rendered in the has-stories layout.</summary>
    public required string NextActionsPanelHtml { get; init; }

    /// <summary>The pre-rendered epic next-steps HTML for the NO-stories layout (named opaque fragment; empty
    /// when the module exposes no command).</summary>
    public required string NextStepsHtml { get; init; }

    /// <summary>The pre-rendered retrospective-affordance HTML (named opaque fragment; empty when nothing shows).</summary>
    public required string RetroAffordanceHtml { get; init; }

    /// <summary>The pre-rendered consolidated undrafted-story banner HTML (named opaque fragment; empty when
    /// fewer than two stories lack a task plan). [Story 8.6]</summary>
    public required string UndraftedBannerHtml { get; init; }

    /// <summary>The epic — the Story-Breakdown sunburst's chart input.</summary>
    public required EpicInfo Epic { get; init; }

    /// <summary>The command catalog the sunburst consumes.</summary>
    public required CommandCatalog Commands { get; init; }

    /// <summary>The epic page's relative link prefix (for the sunburst's per-story hrefs).</summary>
    public required string Prefix { get; init; }

    /// <summary>The story cards, in epic order (data + named opaque prose).</summary>
    public required IReadOnlyList<StoryCardView> StoryCards { get; init; }

    /// <summary>The prev/next sibling pager (adjacent epics by number), rendered inline in the header. Defaults
    /// to <see cref="EntityPager.None"/> so non-generator constructions render no pager. [Prev/next navigation]</summary>
    public EntityPager Pager { get; init; } = EntityPager.None;

    /// <summary>Open follow-ups scoped to this epic for the epic sunburst's story-ring peers. Defaults to
    /// <see cref="FollowUpGeometry.Empty"/> (no follow-up wedges when this epic has none). [Story 9.7]</summary>
    public FollowUpGeometry FollowUps { get; init; } = FollowUpGeometry.Empty;

    /// <summary>Epic-attributed quick-dev only (project Unplanned root is never drawn on epic pages).
    /// [Story 9.12]</summary>
    public UnplannedWorkGeometry UnplannedWork { get; init; } = UnplannedWorkGeometry.Empty;

    /// <summary>Rendered retirement/superseded notices for this epic (Story 10.5, AC3) — rendered in a
    /// collapsed "Retired" section after the story cards; empty omits the section entirely (NFR8).</summary>
    public IReadOnlyList<string> RetiredNoticesHtml { get; init; } = Array.Empty<string>();
}

/// <summary>Compact verification facts for the story-page evidence strip — tasks (from
/// <see cref="ProgressCalculator"/>), optional free-text test tally, and the top Change Log date with a
/// verification vs. plain-edit label. Data only; honest-absence wording and pill markup live in the renderer.
/// [Story 9.4]</summary>
public sealed record StoryEvidence(
    int TasksDone,
    int TasksTotal,
    string? TestsSummary,
    DateOnly? VerifiedDate,
    bool VerifiedIsReview);

/// <summary>How a touched file should read in the change-surface panel.</summary>
public enum ChangeSurfaceFileKind
{
    Code,
    CodeNew,
    Sprint,
    StoryArtifact,
    Other,
}

/// <summary>One file from the Dev Agent Record File List with an optional link and surface kind.</summary>
public sealed record ChangeSurfaceFile(string Path, string Label, string? Href, ChangeSurfaceFileKind Kind);

/// <summary>Host-neutral projection of a story's testable change footprint — derived from standard BMAD
/// sections only (File List, Acceptance Criteria, Status/Change Log). Data only; markup lives in the
/// renderer. [ADR 0007; Story 9.4]</summary>
public sealed record StoryChangeSurface(
    IReadOnlyList<string> Classifications,
    IReadOnlyList<(int Number, string PlainText)> VerifyChecklist,
    IReadOnlyList<ChangeSurfaceFile> ChangedFiles,
    string? VerifyBeforeReviewHtml);

/// <summary>The host-neutral SECTION view model for a drafted STORY page body. Its identity/status/drill are
/// data; the task-breakdown sunburst renders from <see cref="Tasks"/>; everything else the story page shows is
/// inherently Markdig-rendered prose carried as NAMED OPAQUE fragments (the deferred prose-decomposition seam) —
/// the blurb, acceptance criteria, dev-agent record, review findings, the remainder body, and the change log.
/// [Story 6.2]</summary>
public sealed record StoryPageView
{
    /// <summary>The story id "N.M".</summary>
    public required string Id { get; init; }

    /// <summary>The story title as already-projected HTML (the <c>&lt;h1&gt;</c>).</summary>
    public required string TitleHtml { get; init; }

    /// <summary>The <see cref="StatusStyles.ForStory"/> status stage — the header badge + interaction stage
    /// (badge shown only when <see cref="Status"/> is present).</summary>
    public required string StatusStage { get; init; }

    /// <summary>The raw status word, or null (no badge).</summary>
    public string? Status { get; init; }

    /// <summary>Verification evidence for the header strip (tasks / tests / verified-or-updated date). Present
    /// on every drafted story page; the renderer shows the strip only when <see cref="Status"/> is set.
    /// [Story 9.4]</summary>
    public required StoryEvidence Evidence { get; init; }

    /// <summary>Change footprint projected from standard BMAD sections (File List, ACs, Status/Change Log).
    /// The renderer shows the surface panel only when <see cref="Status"/> is set. [ADR 0007; Story 9.4]</summary>
    public required StoryChangeSurface ChangeSurface { get; init; }

    /// <summary>The pre-rendered "Epic N retro →" kicker link HTML (named opaque fragment; empty when none).</summary>
    public required string RetroLinkHtml { get; init; }

    /// <summary>The narrative lead ("As a X, I want Y") as HTML (named opaque fragment; empty when absent).</summary>
    public required string BlurbHtml { get; init; }

    /// <summary>The task checklist — the Task-Breakdown sunburst's chart input (empty → panel omitted).</summary>
    public required IReadOnlyList<TaskItem> Tasks { get; init; }

    /// <summary>The pre-rendered next-steps commands HTML (named opaque fragment).</summary>
    public required string NextStepsHtml { get; init; }

    /// <summary>The acceptance criteria — each carries its own already-projected HTML (named opaque per-item).</summary>
    public required IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria { get; init; }

    /// <summary>The Dev Agent Record rows (label + opaque content HTML).</summary>
    public required IReadOnlyList<DevAgentEntry> DevAgentRecord { get; init; }

    /// <summary>The review-findings HTML (named opaque fragment; empty when absent).</summary>
    public required string ReviewFindingsHtml { get; init; }

    /// <summary>The remainder body — the story's main Markdig-rendered prose (named opaque RichHtml fragment).</summary>
    public required string RemainderHtml { get; init; }

    /// <summary>The change-log HTML (named opaque fragment; empty when absent).</summary>
    public required string ChangeLogHtml { get; init; }

    /// <summary>The prev/next sibling pager (adjacent stories in global epic→story order), rendered inline in the
    /// header. Defaults to <see cref="EntityPager.None"/> so non-generator constructions render no pager. [Prev/next navigation]</summary>
    public EntityPager Pager { get; init; } = EntityPager.None;

    /// <summary>Deferred-work items whose provenance names this story (reverse index). Empty → panel omitted
    /// (NFR8). [artifact-review-nav-and-deferred]</summary>
    public IReadOnlyList<FollowUpDeferredSlot> DeferredFromThis { get; init; } = Array.Empty<FollowUpDeferredSlot>();

    /// <summary>Fallback href for reverse-panel rows whose detail URL is missing (deferred-work list).</summary>
    public string? DeferredListHref { get; init; }
}

/// <summary>The host-neutral SECTION view model for a STORY PLACEHOLDER page body (a story listed in epics.md
/// with no drafted artifact yet). Identity/status/drill are data; the narrative + AC blocks + create-story note
/// are named opaque fragments. [Story 6.2]</summary>
public sealed record StoryPlaceholderView
{
    /// <summary>The story id "N.M".</summary>
    public required string Id { get; init; }

    /// <summary>The story title as already-projected HTML (the <c>&lt;h1&gt;</c>).</summary>
    public required string TitleHtml { get; init; }

    /// <summary>The <see cref="StatusStyles.ForStory"/> status stage — the "Not yet drafted" badge stage.</summary>
    public required string StatusStage { get; init; }

    /// <summary>The pre-rendered "Epic N retro →" kicker link HTML (named opaque fragment; empty when none).</summary>
    public required string RetroLinkHtml { get; init; }

    /// <summary>The narrative HTML (named opaque fragment; empty when absent).</summary>
    public required string UserStoryHtml { get; init; }

    /// <summary>The epics.md comment above the narrative, pre-rendered as a block-level <c>.md-comment</c>
    /// annotation; "" when absent. Rendered as its own block above the narrative (named opaque fragment).</summary>
    public string UserStoryNoteHtml { get; init; } = string.Empty;

    /// <summary>The epics.md acceptance-criteria blocks as HTML (named opaque fragments; empty → panel omitted).</summary>
    public required IReadOnlyList<string> AcBlocksHtml { get; init; }

    /// <summary>The pre-rendered "create its plan with…" note HTML (named opaque fragment).</summary>
    public required string NoteHtml { get; init; }

    /// <summary>The epic number (the "← Back to Epic N" link).</summary>
    public required int EpicNumber { get; init; }

    /// <summary>The prefix-resolved "← Back to Epic N" href.</summary>
    public required string BackHref { get; init; }

    /// <summary>The prev/next sibling pager (adjacent stories in global epic→story order), rendered inline in the
    /// header. Defaults to <see cref="EntityPager.None"/> so non-generator constructions render no pager. [Prev/next navigation]</summary>
    public EntityPager Pager { get; init; } = EntityPager.None;
}
