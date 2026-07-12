namespace SpecScribe;

/// <summary>One story as the VS Code native outline (activity-bar tree + status bar) needs it: a flat,
/// host-neutral record derived entirely from the already-ingested <see cref="StoryInfo"/> and classified by the
/// existing <see cref="StatusStyles"/> — NOT a new render, no markdown re-parse, no HTML. This is the
/// "JSON export for a non-webview consumer" clause ADR 0005 §1 reserved: the C# core decides <em>what to say</em>
/// (id, title, the six-stage status, counts, the surface to reveal, the source to open, a ready-to-run helper
/// command); the thin TS shim decides only <em>where VS Code shows it</em> and maps this 1:1 to a
/// <c>TreeItem</c> with zero interpretation (AD-1/AD-2). [Story 6.9]</summary>
/// <param name="Id">"N.M", e.g. "6.9" — the tree node's identity and the argument a helper command carries.</param>
/// <param name="Title">The story title, verbatim; the node label is composed host-side as "N.M &lt;title&gt;".</param>
/// <param name="Stage">The six-stage lifecycle css-class from <see cref="StatusStyles.ForStory"/>
/// (<c>done</c>/<c>review</c>/<c>active</c>/<c>ready</c>/<c>drafted</c>) — the exact key the contributed
/// <c>specscribe.status.&lt;stage&gt;</c> theme color and the host icon map are keyed on. Emitted here so the
/// stage vocabulary is never re-spelled in TypeScript (constraint #5).</param>
/// <param name="StageLabel">The human, on-brand name for <paramref name="Stage"/> from
/// <see cref="StatusStyles.StoryLabel"/> (e.g. "In development") — emitted so the tree tooltip shows a stage word
/// without the shim authoring a stage→label map (AD-2: no labels computed in TypeScript).</param>
/// <param name="SurfacePath">The story page's output-relative path — one of the <c>surfaces[...]</c> keys the
/// webview bundle emits — so a tree click can <c>push()</c> straight to it. Populated for placeholder stories
/// too (the placeholder page IS a surface), so every story node is clickable; only null defensively.</param>
/// <param name="SourcePath">The story artifact's path relative to <c>_bmad-output/</c> (e.g.
/// <c>implementation-artifacts/6-9-….md</c>) for the tree's read-only "Open Source" action; null when the story
/// has no drafted artifact yet (the action is then omitted host-side).</param>
/// <param name="TasksDone">Checked task/subtask count from the artifact; 0 when there is no artifact.</param>
/// <param name="TasksTotal">Total task/subtask count from the artifact; 0 when there is no artifact.</param>
/// <param name="HelperCommand">The single most-actionable BMad command for this story's status (dev-story when
/// ready/active, code-review when in review, create-story when undrafted), composed core-side via
/// <see cref="BmadCommands.PrimaryStoryCommand"/>; null when the detected module exposes none (the "Copy Helper
/// Prompt" action is then omitted). Composed in C#, never authored in TypeScript (AD-2).</param>
public sealed record OutlineStory(
    string Id,
    string Title,
    string Stage,
    string StageLabel,
    string? SurfacePath,
    string? SourcePath,
    int TasksDone,
    int TasksTotal,
    string? HelperCommand);

/// <summary>One epic as the native outline needs it: a collapsible parent node over its stories. Its
/// <paramref name="Stage"/> uses the retro-gated classifier (<see cref="StatusStyles.ForEpicWithRetrospective"/>)
/// because the tree is a VISUAL status surface — an all-done epic without a retrospective reads as "review", not
/// "done", matching every other epic-status surface (sunburst, donut, chips). [Story 6.9]</summary>
/// <param name="Number">The epic number; the node label is composed host-side as "Epic N: &lt;title&gt;".</param>
/// <param name="Title">The epic title, verbatim.</param>
/// <param name="Stage">The six-stage epic css-class from <see cref="StatusStyles.ForEpicWithRetrospective"/>
/// (adds <c>pending</c>; downgrades an all-done-but-un-retro'd epic to <c>review</c>).</param>
/// <param name="StageLabel">The human name for <paramref name="Stage"/> from
/// <see cref="StatusStyles.EpicLabel"/> — for the tree tooltip, so the shim authors no label (AD-2).</param>
/// <param name="SurfacePath">The epic page's output-relative <c>surfaces[...]</c> key, so a click reveals it.</param>
/// <param name="StoriesTotal">Number of stories in the epic.</param>
/// <param name="StoriesDone">Number of stories whose stage is <c>done</c> — the node description shows
/// <c>StoriesDone/StoriesTotal</c>.</param>
/// <param name="Stories">The child story records, in the core's order (the host does not re-sort).</param>
public sealed record OutlineEpic(
    int Number,
    string Title,
    string Stage,
    string StageLabel,
    string? SurfacePath,
    int StoriesTotal,
    int StoriesDone,
    IReadOnlyList<OutlineStory> Stories);

/// <summary>The status-bar summary, computed core-side so the shim does no counting (R3.2). Counts STORIES by
/// stage across all epics. [Story 6.9]</summary>
/// <param name="Active">Stories whose stage is <c>active</c> (in development).</param>
/// <param name="Review">Stories whose stage is <c>review</c> (in review).</param>
/// <param name="Done">Stories whose stage is <c>done</c>.</param>
/// <param name="Total">All stories across all epics.</param>
public sealed record OutlineSummary(int Active, int Review, int Done, int Total);

/// <summary>The whole host-neutral project outline the <c>specscribe webview</c> payload carries as its
/// <c>outline</c> field: the epic/story tree plus the status-bar <see cref="Summary"/>. Data, not rendering
/// (ADR 0005 §1) — built from the already-ingested <see cref="EpicsModel"/>, so it emits no HTML and leaves the
/// generated site byte-identical (the golden fingerprint is unaffected by construction). [Story 6.9]</summary>
public sealed record ProjectOutline(
    IReadOnlyList<OutlineEpic> Epics,
    OutlineSummary Summary);
