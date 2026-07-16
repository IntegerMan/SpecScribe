namespace SpecScribe;

/// <summary>One next-step command for a story — a (command, description) pair mirroring exactly one entry of the
/// story page's "Next Steps" panel, projected as data so the VS Code tree's "Copy BMad Command…" Quick Pick can
/// show the LITERAL command it will copy beside the same description the page shows. Composed core-side by
/// <see cref="BmadCommands.StoryCommands"/> from the SAME status-gated list the page renders, so the tree's
/// option set can never disagree with the page (AD-2: no command text authored in TypeScript).
/// [spec-vscode-sidebar-shortcuts-and-story-command-quickpick]</summary>
public sealed record OutlineStoryCommand(string Command, string Description);

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
/// <param name="SourcePath">The story artifact's REPO-relative path (forward-slashed, e.g.
/// <c>_bmad-output/implementation-artifacts/6-9-….md</c>) for the tree's read-only "Open Source" action; null when
/// the story has no drafted artifact yet (the action is then omitted host-side). Repo-relative — not
/// source-root-relative — so the host joins it to the workspace folder with the SAME one convention the webview
/// reveal-source and <c>configuredOutputRoot</c> use, with no <c>_bmad-output</c> literal duplicated in TypeScript
/// (Story 6.10 AC #1 "no duplicated path assumptions").</param>
/// <param name="TasksDone">Checked task/subtask count from the artifact; 0 when there is no artifact.</param>
/// <param name="TasksTotal">Total task/subtask count from the artifact; 0 when there is no artifact.</param>
/// <param name="HelperCommand">The single most-actionable BMad command for this story's status —
/// <see cref="BmadCommands.PrimaryStoryCommand"/> (null when done or when <paramref name="Commands"/> has no
/// primary). Kept for payload back-compat: an older shim reads only this. For non-done stories this is the
/// first entry of <paramref name="Commands"/>; a done story may still list a muted correct-course hatch in
/// <paramref name="Commands"/> while this stays null. Composed in C#, never authored in TypeScript (AD-2).</param>
/// <param name="Commands">The FULL status-gated next-step command list — the exact set the story page's
/// "Next Steps" panel renders (<see cref="BmadCommands.StoryCommands"/>), in the page's order. For a done story
/// this is empty or a single muted correct-course escape hatch when the module exposes it; empty when the
/// module exposes none. The host omits "Copy BMad Command…" when the list is empty; all gating is decided
/// here, never in the shim (AD-2). [spec-vscode-sidebar-shortcuts-and-story-command-quickpick; Story 8.5]</param>
public sealed record OutlineStory(
    string Id,
    string Title,
    string Stage,
    string StageLabel,
    string? SurfacePath,
    string? SourcePath,
    int TasksDone,
    int TasksTotal,
    string? HelperCommand,
    IReadOnlyList<OutlineStoryCommand> Commands);

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
