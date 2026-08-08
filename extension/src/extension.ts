// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// SpecScribe — the production thin extension-host shim (Story 6.4, governed by ADR 0005).
//
// This file is deliberately the WHOLE TypeScript surface. Its only responsibilities (the "irreducible shim"):
//   1. register commands + menus and set the `specscribe.available` context key (a folder is open — the extension
//      renders in ANY workspace, not only bmad projects; spec-vscode-any-workspace-and-processing-indicators),
//   2. open a WebviewPanel, AND drive the native activity-bar tree + status bar (Story 6.9),
//   3. obtain C#-rendered HTML + the host-neutral `outline` from the `specscribe webview` child process,
//   4. inject the two host-runtime values (cspSource + nonce) the C# shell left as placeholders,
//   5. relay messages: in-webview navigation, open-external, reveal-source (open a `.md` read-only), and
//      file-change live-push (postMessage, in place).
//
// It parses NO markdown, renders NO view, and holds NO project knowledge (AD-1/AD-2). Project *detection* is by
// path existence only (`fs.existsSync`) — not parsing, so AD-2 holds. Every byte of visible content — including
// every tree label, status word, icon stage, count, and helper command — is decided by the C# core; the tree maps
// the core's `outline` records to TreeItems with a single pure lookup (stage → {icon, colorId}) and nothing else.
// If this file grows a rendering brain, the architecture decision was wrong.
// Read-only over PROJECT ARTIFACTS (AD-6, as amended by ADR 0037). Nothing here writes a source artifact, an
// `_bmad-output` file or the output tree. Generate/Watch/scaffold are STAGED into a terminal for the user to run —
// SpecScribe never presses Enter. Tree actions only reveal a surface, open a `.md` read-only, or copy a prompt.
//
// ONE exception, and it is scoped to one file: the settings form (`openProjectSettings`) writes
// `.specscribe/config.json` — and even then the shim does not write it. Save spawns `specscribe config --save`, so
// `SettingsStore` stays the single writer and the tri-state/persist rules are never re-implemented here. ADR 0037
// re-reads AD-6's "explicit external choice" as "an explicit, user-initiated action that writes only the settings
// document", on the grounds that pressing Save is no less deliberate than pressing Enter on a staged command.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

import * as vscode from 'vscode';
import { spawn } from 'node:child_process';
import type { ChildProcess } from 'node:child_process';
import * as crypto from 'node:crypto';
import * as fs from 'node:fs';
import * as path from 'node:path';

interface SurfaceContent {
  title: string;
  content: string; // nav + breadcrumb + body — what an in-place swap installs into #specscribe-surface
  /** The repo-relative markdown this surface was rendered from, for the read-only "Open source" reveal (Story
   * 6.10). Forward-slashed, host-joined to the workspace folder (the ONE convention — no `_bmad-output` literal
   * here). Absent/empty for a source-less surface (the dashboard) → the reveal button stays hidden. [Story 6.10] */
  sourcePath?: string;
}

interface WebviewProgress {
  key: string;
  label: string;
  step: number;
  total: number;
  complete?: boolean;
}

/** One next-step command for a story (mirrors the C# `OutlineStoryCommand`): the literal command string the
 * Quick Pick shows and copies, plus the same description the story page's Next Steps panel renders beside it.
 * Both core-composed — the shim authors neither (AD-2). [spec-vscode-sidebar-shortcuts-…-quickpick] */
interface OutlineStoryCommand {
  command: string;
  description: string;
}

/** One story in the host-neutral outline (mirrors the C# `OutlineStory`). Every field is core-decided; the shim
 * computes none of it (AD-1/AD-2). [Story 6.9] */
interface OutlineStory {
  id: string;
  title: string;
  stage: string;        // done|review|active|ready|drafted — keys the status color + icon map
  stageLabel: string;   // human name for the tooltip (core-emitted, never composed here)
  surfacePath?: string; // a surfaces[...] key to push() to (present for placeholder stories too)
  sourcePath?: string;  // repo-relative artifact path for read-only "Open Source" (host-joined to the folder, one
                        // convention shared with the webview reveal — no _bmad-output literal); absent → no action
  tasksDone: number;
  tasksTotal: number;
  helperCommand?: string; // the most-actionable BMad command, composed core-side; absent → no copy action
  /** The FULL status-gated command list — the exact set the story page's Next Steps panel shows, in its order
   * (empty = no copy action, e.g. a done story). Optional so an older core still parses; the shim then falls
   * back to a one-item list from `helperCommand`. [spec-vscode-sidebar-shortcuts-…-quickpick] */
  commands?: OutlineStoryCommand[];
}

/** One epic in the outline (mirrors the C# `OutlineEpic`); its stage is the retro-gated classifier. [Story 6.9] */
interface OutlineEpic {
  number: number;
  title: string;
  stage: string;        // done|review|active|ready|drafted|pending
  stageLabel: string;
  surfacePath?: string;
  storiesTotal: number;
  storiesDone: number;
  stories: OutlineStory[];
}

/** The status-bar summary, counted core-side (mirrors the C# `OutlineSummary`). [Story 6.9] */
interface OutlineSummary {
  active: number;
  review: number;
  done: number;
  total: number;
}

/** One Shortcuts-pane entry, projected core-side from `SiteNav.QuickLinks` (mirrors the C# `OutlineShortcut`) —
 * a surface this run actually produced, with the description the portal already shows for it. Data, not rendering:
 * `iconKey` is the CORE's icon vocabulary and the shim maps it to a codicon via {@link CONCEPT_ICON}, the same way
 * it maps story stages. [field feedback 2026-08-01] */
interface OutlineShortcut {
  label: string;
  description: string;
  surfacePath: string;
  group: string;
  iconKey: string;
}

/** The whole project outline (mirrors the C# `ProjectOutline`) — data, not rendering (ADR 0005 §1). [Story 6.9] */
interface ProjectOutline {
  epics: OutlineEpic[];
  summary: OutlineSummary;
  /** Optional so a payload from an older core still parses — the pane then shows only its two pinned entries,
   * which is exactly the pre-change behaviour. */
  shortcuts?: OutlineShortcut[];
}

/** One `specscribe webview` spawn's stdout: the full entry document (placeholders unsubstituted) plus every
 * navigable surface, keyed by output-relative path, plus the host-neutral outline. See WebviewBundle in the C#
 * core. */
interface WebviewPayload {
  siteTitle: string;
  entry: string;
  document: string;
  /** Workspace-relative root a plain `generate` writes to (forward-slashed). Host-delivered core datum, not
   * rendering (ADR 0005 §1) — the "Open Generated Site" command joins it to the folder. Optional so an older
   * core still parses. [Story 6.8] */
  configuredOutputRoot?: string;
  /** Resolved watch roots (Story 6.11), all repo/workspace-relative + forward-slashed. `sourceRoot`/`adrRoot` are the
   * source and ADR trees the file watchers are built from; `repoRoot` is the workspace-relative offset from the
   * folder to the real repo root (`.` at the root), so the shim resolves the absolute repo root once and anchors BOTH
   * the watchers AND the reveal-source join to it (correct even when opened on a subdirectory). All optional so an
   * older core still parses — the store falls back to the literal `_bmad-output`/`docs/adrs` globs when absent. */
  sourceRoot?: string;
  adrRoot?: string;
  repoRoot?: string;
  /** Workspace-relative path (forward-slashed) of the settings DOCUMENT the core would read or write — i.e.
   * `.specscribe/config.json` for the current folder format, or a bare `.specscribe` for a not-yet-migrated flat
   * file (ADR 0014). Core-derived so the shim never re-types a SpecScribe filename or re-implements the walk-up in
   * `SettingsStore.FindExisting`. Absent when the core found no settings anywhere above the repo root, and absent
   * on an older core — in both cases the shim falls back to its own walk-up. [C0] */
  settingsPath?: string;
  surfaces: Record<string, SurfaceContent>;
  /** The activity-bar tree + status-bar data. Optional so an older core (pre-6.9) still parses. [Story 6.9] */
  outline?: ProjectOutline;
  /** True on the FIRST-PAINT PRELUDE frame only: the ENTRY SURFACE ALONE is here — exactly what a freshly-opened
   * panel displays — and the epics family plus the ~700 doc/ADR/requirement surfaces are still being rendered
   * and arrive on the very next delta frame. The outline is empty for that window too. The panel paints
   * immediately instead of waiting out the whole bundle, and a click on a surface that has not landed yet says
   * "still loading" rather than the permanent "isn't available in the in-editor view" toast — which during this
   * window would be a lie.
   *
   * <p>Optional and absent-means-false, so a payload from an older core (which never sends it) reads as complete —
   * exactly today's behaviour. Mirrors the C# `partial` field. [spec-vscode-extension-name-latency-and-webview-sunburst
   * Goal 2]</p> */
  partial?: boolean;
  /** Optional render-stage metadata from the core to power host progress text (phase labels + step counts).
   * Absent on older cores; host falls back to generic progress wording. */
  progress?: WebviewProgress;
}

/** The literal `frame` value a Story 22.6 DELTA frame carries. A full payload deliberately carries no `frame`
 * field at all, so every payload every already-shipped VSIX has ever received still reads as full — the
 * discriminator is on the NEW shape, never the old one. Mirrors `WebviewCommand.DeltaFrameDiscriminator`. */
const DELTA_FRAME = 'delta';

/** One incremental push from `specscribe webview --serve --serve-delta`: only the surfaces that moved, the paths
 * that disappeared, and the (small) outline — instead of re-shipping the whole site on every save. Before this,
 * a one-character edit to one story file re-shipped this repo's ~8 MB whole-site payload (see the
 * MAX_RENDERER_STDOUT_BYTES guard's own note).
 *
 * <p>{@link PersistentRenderer} MERGES a frame into its cached payload and hands downstream a complete
 * {@link WebviewPayload}, so the documented invariant that "a live-pushed `--serve` payload and a one-shot spawn
 * payload are indistinguishable" is preserved — no consumer below the renderer learns that deltas exist.</p>
 * [Story 22.6 AC #3] */
interface DeltaFrame {
  frame: typeof DELTA_FRAME;
  /** Monotonic within one serve session. A GAP means a frame was missed, so the cached payload can no longer be
   * trusted — the connection is torn down rather than rendering a half-applied state. */
  sequence: number;
  siteTitle: string;
  entry: string;
  /** Present only when the dashboard document itself moved. `null`/absent means KEEP WHAT YOU HAVE — never
   * "the dashboard is now empty". */
  document?: string | null;
  configuredOutputRoot?: string;
  sourceRoot?: string;
  adrRoot?: string;
  repoRoot?: string;
  /** Deliberately NOT named `surfaces`: a consumer that missed the discriminator and merged a partial `surfaces`
   * map as the whole site would silently drop every unchanged page. Different meaning, different name — the same
   * mistake now degrades to a missing key instead of data loss. */
  changedSurfaces: Record<string, SurfaceContent>;
  removedSurfaces: string[];
  outline?: ProjectOutline;
  /** Always `false` from the core: every delta is computed against a COMPLETE current bundle, so folding one
   * always produces a complete payload — including the frame that completes a first-paint prelude. Read through
   * `?? false` so an older core that omits it also clears a `partial` basis, which is the safe direction: the
   * worst case is the honest-but-permanent "isn't available" toast, never a surface stuck reporting "loading"
   * forever. [spec-vscode-extension-name-latency-and-webview-sunburst Goal 2] */
  partial?: boolean;
  /** Optional render-stage metadata for this frame; carried forward by merge if omitted. */
  progress?: WebviewProgress;
}

function isDeltaFrame(value: WebviewPayload | DeltaFrame): value is DeltaFrame {
  // `typeof null === 'object'`, so the null check has to be explicit — without it a literal `null` JSON line
  // (valid JSON, invalid payload) throws reading `.frame` off it instead of being recognized as "not a delta
  // frame" and falling through to the full-payload branch. [Review][Patch]
  return typeof value === 'object' && value !== null && (value as DeltaFrame).frame === DELTA_FRAME;
}

/** Folds one delta frame onto the payload the consumer currently holds, producing a payload of exactly the shape
 * a one-shot spawn would have returned. Pure: it builds a new object rather than mutating `base`, so a downstream
 * consumer holding the previous payload never observes it change underneath. [Story 22.6 AC #3/#6] */
function applyDeltaFrame(base: WebviewPayload, frame: DeltaFrame): WebviewPayload {
  const surfaces: Record<string, SurfaceContent> = { ...base.surfaces, ...frame.changedSurfaces };
  // `frame.removedSurfaces` is a non-optional array by contract, but a malformed/truncated line (most likely
  // right after a sequence-gap recovery, when data quality is already suspect) could carry a non-array value —
  // `for...of` throws uncaught on a non-iterable, inside a `stdout.on('data', ...)` handler. [Review][Patch]
  const removedSurfaces = Array.isArray(frame.removedSurfaces) ? frame.removedSurfaces : [];
  for (const removed of removedSurfaces) delete surfaces[removed];
  return {
    ...base,
    // `?? base.X`, matching every other field below: a malformed frame missing these must not corrupt the
    // cached payload with `undefined`. [Review][Patch]
    siteTitle: frame.siteTitle ?? base.siteTitle,
    entry: frame.entry ?? base.entry,
    // `?? base.document` is the load-bearing half: an absent/null document means unchanged, and coercing it to
    // '' would blank the dashboard on every unrelated edit.
    document: frame.document ?? base.document,
    configuredOutputRoot: frame.configuredOutputRoot ?? base.configuredOutputRoot,
    sourceRoot: frame.sourceRoot ?? base.sourceRoot,
    adrRoot: frame.adrRoot ?? base.adrRoot,
    repoRoot: frame.repoRoot ?? base.repoRoot,
    surfaces,
    outline: frame.outline ?? base.outline,
    // NOT `?? base.partial`: a delta always completes its basis, so the merged payload is complete even when the
    // basis was the first-paint prelude. Inheriting `base.partial` would leave the panel answering "still loading"
    // for the rest of the session. [spec-vscode-extension-name-latency-and-webview-sunburst Goal 2]
    partial: frame.partial ?? false,
    progress: frame.progress ?? base.progress,
  };
}

/** One core-emitted generation notice, parsed from a JSON line on the `webview` command's stderr. The core owns
 * WHAT the notice says and WHICH file; the shim only decides that VS Code shows the file-anchored ones in the
 * Problems panel (constraint #1). Unknown fields are ignored and a record missing a string `path`/`message` or
 * carrying a `severity` other than `'error'`/`'warning'` is dropped by `parseDiagnostics`, so a future core field
 * never breaks an older shim. [Story 6.12] [Story 6.11 deferred-work cleanup: message/severity now validated too] */
interface RawDiagnostic {
  path: string;
  /** `'error'` and `'warning'` map to VS Code's Problems severities — this is the Problems domain, NOT the six
   * `--status-*` lifecycle stages (constraint #5), which never collapse onto host severities. `parseDiagnostics`
   * only admits these two literal values — any other value is dropped rather than silently coerced into
   * `'warning'` (see its doc comment). */
  severity: 'error' | 'warning';
  message: string;
  fileAnchored?: boolean;
}

/** One `webview` spawn's outcome: the stdout payload plus the notices parsed off its stderr JSON lines. Threaded
 * together so the store can refresh the cache and republish the Problems collection on the same settle. */
interface RendererResult {
  payload: WebviewPayload;
  diagnostics: RawDiagnostic[];
}

interface HostStatusAction {
  id: 'openProjectSettings' | 'refresh' | 'openToolPathSettings';
  label: string;
}

interface HostStatusPayload {
  type: 'hostStatus';
  level: 'progress' | 'info' | 'warning' | 'error';
  text: string;
  actions?: HostStatusAction[];
}

/** The direct-open target an entry point asks for. Resolved against the loaded payload's surface keys — never a
 * hard-coded path — so a renamed epics-index key can't silently open the dashboard instead. [Story 6.8] */
type SurfaceTarget = 'dashboard' | 'epics';

/** A reveal request the panel honors: a well-known target (resolved to a key once the payload lands) OR an exact
 * surface key the tree already holds. Unifies 6.8's Open Dashboard/Epics with 6.9's tree-click reveal so both ride
 * the ONE parametrized open path — no forked second panel. [Story 6.9] */
type Reveal = { kind: 'target'; target: SurfaceTarget } | { kind: 'surface'; key: string };

/** The host-side driver for the one open panel: reveal to a requested surface and force a manual reload. Lets the
 * command handlers (Open Dashboard/Epics/Refresh, tree clicks) steer the singleton without each forking its own
 * open path. */
interface PanelController {
  reveal(reveal: Reveal): void;
  reload(): void;
}

/** Default output root when no payload has been loaded yet to supply `configuredOutputRoot` (memory: the output
 * dir is `SpecScribeOutput`, never `docs/live`). */
const DEFAULT_OUTPUT_ROOT = 'SpecScribeOutput';

/** The settings entry name and the config document inside it — mirroring `SettingsStore.FileName` and
 * `SettingsStore.ConfigFileName`. Used ONLY on the fallback walk-up in {@link resolveSettingsDocument}; when the
 * core has supplied `settingsPath` on the payload, that wins and these are not consulted. Kept as named constants
 * rather than inline literals so the two places the shim knows a SpecScribe filename are greppable. */
const SETTINGS_ENTRY_NAME = '.specscribe';
const SETTINGS_CONFIG_NAME = 'config.json';

let panel: vscode.WebviewPanel | undefined;
/** The settings form's panel (ADR 0037) — a SECOND singleton, deliberately separate from the portal's. The portal
 * panel is live-pushed by `PersistentRenderer` and anything spliced into its surface container is destroyed by the
 * next `push()`, so the form cannot live there. */
let settingsPanel: vscode.WebviewPanel | undefined;
let active: PanelController | undefined;
/** Last payload's configured output root, so "Open Generated Site" needn't re-spawn just to learn the path. */
let lastConfiguredOutputRoot: string | undefined;

/** Last payload's resolved ABSOLUTE repo root (the workspace folder joined to the core-emitted `repoRoot` offset).
 * The ONE anchor shared by the store's watchers and the reveal-source join, so a subdir-open (repo root ≠ workspace
 * folder) watches and reveals the right paths. Undefined until the first payload lands → callers fall back to the
 * workspace folder (today's behavior, correct at the common repo-root open). [Story 6.11] */
let lastRepoRoot: string | undefined;

/** Last payload's core-emitted settings-document path, workspace-relative and forward-slashed (see
 * `WebviewPayload.settingsPath`). Undefined until a payload lands, or when the core found no settings at all —
 * `resolveSettingsDocument` then walks up itself. [C0] */
let lastSettingsPath: string | undefined;

/** Whether a workspace folder is open — the ONLY gate on the status bar, the tree's lazy load, and the manifest
 * `when` clauses (via the `specscribe.available` context key). Deliberately NOT a bmad/git detection: the extension
 * renders value in ANY workspace (README + code map + git-if-present), so the presence of a folder — not of a
 * `_bmad-output` marker — is what enables the surfaces. A non-bmad folder simply shows a "no epics" outline.
 * [spec-vscode-any-workspace-and-processing-indicators] */
let folderOpen = false;

/** The single shared payload provider: one spawn, one cache, driving the panel + tree + status bar, refreshed
 * together (Story 6.9's central refactor). Rebound to folder[0] on activation and whenever the folder set changes.
 * Undefined only before activation or with no workspace folder. */
let store: SpecScribeStore | undefined;

/** One fan-out fired whenever the shared payload (re)loads or the workspace binding changes. The status bar, the
 * tree, and the open panel each subscribe once; the store fires it on every load settle (success OR failure). */
const dataChanged = new vscode.EventEmitter<void>();

/** Terminals currently mid-command via shell integration (`onDidStartTerminalShellExecution` →
 * `onDidEndTerminalShellExecution`), consulted by {@link getOrCreateTerminal} so a busy "SpecScribe" terminal is
 * never reused for a new staged command. A `WeakSet` so a disposed/closed terminal is never kept alive by this
 * bookkeeping alone. */
const busyTerminals = new WeakSet<vscode.Terminal>();

/** The bound folder's URI string the multi-root notice was last shown for — re-shown whenever the ACTUALLY-BOUND
 * folder identity changes while still multi-root (not merely "already shown once this session"), and reset when
 * the workspace drops back to single-root so a later re-expansion notifies again for whichever folder binds then.
 * Multi-root support itself stays out of scope (Story 6.11); this only keeps the notice from going stale about
 * which folder is currently watched. [spec-6-9-deferred-debt-cleanup review] */
let multiRootNoticeShownForFolder: string | undefined;

let statusBar: vscode.StatusBarItem | undefined;
let treeProvider: OutlineTreeProvider | undefined;
/** The Shortcuts pane's provider — at module scope so the `dataChanged` fan-out can refresh it. It became
 * refreshable when its entries stopped being a static array and started coming from `outline.shortcuts`. */
let shortcutsProvider: ShortcutsTreeProvider | undefined;
/** The outline TreeView handle — kept at module scope (not just pushed to subscriptions) so the visibility-aware
 * refresh can read `treeView.visible` and subscribe to `onDidChangeVisibility` (Story 6.11 R6.3). */
let treeView: vscode.TreeView<OutlineNode> | undefined;

/** The one collection the core's per-artifact generation notices publish into — VS Code renders it in the native
 * Problems panel with `SpecScribe` as the source. Rebuilt on every successful store settle (clearing notices a
 * later run resolves); left untouched on a failed load so a transient spawn error doesn't drop last-good
 * diagnostics. Pure host-UI transport — nothing here writes a project artifact (read-only, AD-6). [Story 6.12] */
let diagnosticCollection: vscode.DiagnosticCollection | undefined;

/** Shared output channel for host-side diagnostics/progress notes that complement Problems entries. */
let outputChannel: vscode.OutputChannel | undefined;

function logHost(message: string): void {
  const stamp = new Date().toLocaleTimeString();
  outputChannel?.appendLine(`[${stamp}] ${message}`);
}

function asError(err: unknown, fallbackMessage = 'Unknown error'): Error {
  if (err instanceof Error) return err;
  if (typeof err === 'string' && err.trim().length > 0) return new Error(err);
  return new Error(fallbackMessage);
}

function describeError(err: unknown): string {
  return asError(err).message;
}

export function activate(context: vscode.ExtensionContext) {
  const register = (id: string, handler: (...args: unknown[]) => unknown) =>
    context.subscriptions.push(vscode.commands.registerCommand(id, handler));

  // Open Status stays the original entry point and is what the explorer/editor menus reuse (they receive a
  // resource Uri we deliberately ignore — the panel opens to the dashboard regardless of which file was clicked).
  register('specscribe.openStatus', () => openStatus(context, 'dashboard'));
  register('specscribe.openDashboard', () => openStatus(context, 'dashboard'));
  register('specscribe.openEpics', () => openStatus(context, 'epics'));
  register('specscribe.refresh', () => refreshCommand(context));
  register('specscribe.openGeneratedSite', () => void openGeneratedSite());
  register('specscribe.generateSite', () => stageTerminalCommand(context, 'generate'));
  register('specscribe.watch', () => stageTerminalCommand(context, 'watch'));
  register('specscribe.openProjectSettings', () => void openProjectSettings(context));

  // Story 6.9 native surfaces.
  register('specscribe.refreshOutline', () => refreshCommand(context));
  // Tree-click reveal: the node carries its exact surface key, so this reuses the ONE parametrized open path.
  register('specscribe.revealSurface', (surfacePath: unknown) => {
    if (typeof surfacePath === 'string') openStatus(context, { kind: 'surface', key: surfacePath });
  });
  register('specscribe.openSource', (node: unknown) => void openSource(node));
  register('specscribe.copyStoryCommand', (node: unknown) => void copyStoryCommand(node));

  // Shortcuts: two pinned host-chrome entries (labels are the same class of host chrome as the manifest command
  // titles), followed by this project's own surfaces from the core-emitted `outline.shortcuts`. The view itself is
  // gated on `specscribe.available` (a folder is open) via its manifest `when`.
  shortcutsProvider = new ShortcutsTreeProvider();
  context.subscriptions.push(
    vscode.window.registerTreeDataProvider('specscribe.shortcuts', shortcutsProvider));

  // Status bar: a summary count that opens the panel; hidden until a detected repo has data (Story 6.9 R3.2).
  // RIGHT-aligned. It was Left, where it sat among the source-control/branch cluster and — per owner feedback
  // 2026-08-02 — was simply not where anyone looks for "is the tool busy". The right group is where VS Code's own
  // language servers and background tasks report, which is the convention this item is participating in.
  // Priority 100 keeps it left-most within that group (higher = further left), so it holds a stable position
  // rather than jumping as other extensions come and go.
  statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  statusBar.command = 'specscribe.openStatus';
  context.subscriptions.push(statusBar);

  // Problems: one DiagnosticCollection the core's generation notices publish into (source `SpecScribe`). Disposed
  // with the extension; rebuilt on every successful store load. [Story 6.12]
  diagnosticCollection = vscode.languages.createDiagnosticCollection('SpecScribe');
  context.subscriptions.push(diagnosticCollection);

  outputChannel = vscode.window.createOutputChannel('SpecScribe');
  context.subscriptions.push(outputChannel);

  // Tree: a TreeDataProvider mapping the core outline 1:1. getChildren lazily triggers the first spawn on reveal.
  treeProvider = new OutlineTreeProvider();
  treeView = vscode.window.createTreeView('specscribe.outline', { treeDataProvider: treeProvider });
  context.subscriptions.push(treeView);
  // Visibility-aware refresh (R6.3): when the tree becomes visible, flush a watcher-driven reload deferred while it
  // was hidden. The tree's own lazy FIRST load stays in getChildren (visibility-appropriate already). [Story 6.11]
  context.subscriptions.push(treeView.onDidChangeVisibility((e) => { if (e.visible) store?.flushIfDirty(); }));

  // All consumers subscribe ONCE to the fan-out; the store fires it on every (re)load. The shortcuts pane joined
  // them when its entries stopped being static — without this it would sit at its two pinned entries forever.
  context.subscriptions.push(dataChanged.event(() => {
    renderStatusBar();
    treeProvider?.refresh();
    shortcutsProvider?.refresh();
  }));

  // Terminal busy-tracking for getOrCreateTerminal's reuse guard. Feature-detected, NOT assumed present: this API
  // graduated to stable after the `engines.vscode` floor this extension declares, so an older-but-still-satisfying
  // host may not expose it — calling an absent event constructor would throw and crash the whole activation.
  // Absent → busyTerminals simply stays empty and reuse behaves exactly as it did before this fix.
  if (typeof vscode.window.onDidStartTerminalShellExecution === 'function') {
    context.subscriptions.push(
      vscode.window.onDidStartTerminalShellExecution((e) => busyTerminals.add(e.terminal)),
      vscode.window.onDidEndTerminalShellExecution((e) => busyTerminals.delete(e.terminal)),
    );
  }

  // Bind the shared store to folder[0] and re-bind when the folder set changes (a late-added SpecScribe folder
  // flips detection without a reload). Path existence only.
  bindWorkspace(context);
  context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => bindWorkspace(context)));
}

/** `fs.realpathSync`, degrading to `undefined` (never throwing) on a vanished path or a permission error — the
 * same generic degrade {@link resolveWorkspacePath} and {@link resolveOpenableFile} already use for symlink
 * resolution. [spec-epic6-deferred-debt-cleanup review] */
function tryRealpath(p: string): string | undefined {
  try {
    return fs.realpathSync(p);
  } catch {
    return undefined;
  }
}

/** (Re)bind the shared store + watchers to the current folder[0], refresh the availability context key, and update
 * the native surfaces. Disposes any prior store so a folder change never leaks watchers. [Story 6.9] */
function bindWorkspace(context: vscode.ExtensionContext) {
  store?.dispose();
  store = undefined;

  const folder = vscode.workspace.workspaceFolders?.[0];
  // Scoped to the first folder only, matching every command handler (they all act on workspaceFolders[0]).
  // Availability is now just "a folder is open" — the extension renders in ANY workspace, so there is no
  // bmad/git marker check here anymore. Multi-root support itself stays out of scope (still folder[0]).
  // [spec-vscode-any-workspace-and-processing-indicators]
  folderOpen = !!folder;
  void vscode.commands.executeCommand('setContext', 'specscribe.available', folderOpen);
  // Distinct from `available`, and the distinction is the whole point (owner feedback 2026-08-02). The extension
  // activates on `onStartupFinished`, so for the first seconds of a session NEITHER key is set — and a welcome
  // gated on `!specscribe.available` alone therefore reads "Open a folder to see its SpecScribe insights" to a
  // user who has a folder open and is simply waiting. The manifest's two `viewsWelcome` entries are gated on
  // `!activated` and `activated && !available` respectively, which are mutually exclusive, so exactly one shows.
  void vscode.commands.executeCommand('setContext', 'specscribe.activated', true);

  // Multi-root support stays out of scope, but a silent folder[0]-only pick is confusing when there's more than
  // one root — tell the user which folder is bound instead of leaving them to guess. Purely informational (no
  // "open as single-folder" imperative): folder[0] may already be exactly the project they want watched, so this
  // only ever names the binding, never implies something is wrong. Re-fires whenever the BOUND folder identity
  // changes (folders added/removed/reordered so a different folder becomes [0]), not just once per session, so
  // the notice never goes stale about which folder is now actually watched — and resets once the workspace drops
  // back to single-root, so a later re-expansion notifies again. [spec-6-9-deferred-debt-cleanup review]
  const isMultiRoot = (vscode.workspace.workspaceFolders?.length ?? 0) > 1;
  // realpath-normalized so two folders that are actually the same directory reached via different paths (one
  // through a symlink) read as the same bound identity, not two — the same rigor this pass gave the C#-side
  // RepoRelative. Falls back to the raw URI string if realpath fails (folder vanished, permission error).
  const folderKey = folder && (tryRealpath(folder.uri.fsPath) ?? folder.uri.toString());
  if (isMultiRoot && folderKey !== undefined && multiRootNoticeShownForFolder !== folderKey) {
    multiRootNoticeShownForFolder = folderKey;
    void vscode.window.showInformationMessage(
      `SpecScribe: this is a multi-root workspace, so only the first folder ("${folder?.name ?? ''}") is watched ` +
      '— multi-root support isn’t available yet. Reorder folders, or open a single-folder window, if you ' +
      'need a different one watched.');
  } else if (!isMultiRoot) {
    multiRootNoticeShownForFolder = undefined;
  }

  if (folder) {
    store = new SpecScribeStore(context, folder);
    store.startWatching();
  }

  // Reflect the new binding on both native surfaces (a fresh store has no data yet → status bar hides, tree
  // either lazy-loads on next reveal or shows the welcome).
  dataChanged.fire();
}

/** Resolve a direct-open target to a real surface key from the loaded payload. The epics-index key is matched, not
 * assumed (the payload's keys are the C# OutputRelativePaths), and falls back to the entry surface if absent —
 * mirroring `push`'s own fallback so a missing surface degrades to the dashboard rather than a dead swap. */
function resolveTarget(cache: WebviewPayload, target: SurfaceTarget): string {
  if (target === 'epics') {
    const key = Object.keys(cache.surfaces).find((k) => /(^|\/)epics\.html$/.test(k));
    if (key) return key;
    // ⚠️ While the payload is the first-paint PRELUDE the epics surface has not been rendered YET, and folding to
    // the entry here is the one path that escapes `push`'s own partial-aware guard: both call sites compare the
    // result against `cache.entry` and do nothing when it matches, so "Open Epics" on a cold project would open
    // the DASHBOARD with no message and never honour the request when the delta landed. Returning the unresolved
    // well-known key instead lets `push` see a miss, say "still loading", and replay it. On a COMPLETE payload the
    // original entry fallback still applies — there the surface really is absent.
    // [Blind Hunter finding 2 / Edge Case Hunter finding 3]
    if (cache.partial) return 'epics.html';
    return cache.entry;
  }
  return cache.entry;
}

/** The surface key a reveal request resolves to against the loaded payload. An exact key passes through (push
 * falls back to the entry if it's somehow stale); a well-known target resolves via {@link resolveTarget}. */
function resolveReveal(cache: WebviewPayload, reveal: Reveal): string {
  return reveal.kind === 'surface' ? reveal.key : resolveTarget(cache, reveal.target);
}

function openStatus(context: vscode.ExtensionContext, reveal: SurfaceTarget | Reveal) {
  const request: Reveal = typeof reveal === 'string' ? { kind: 'target', target: reveal } : reveal;
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }
  if (active) {
    active.reveal(request);
    return;
  }
  active = createController(context, folder, request);
}

/** Manual Refresh: reload the shared payload once (coalesced), so the panel, tree, and status bar all refresh
 * together. On failure the tree/status bar show the stale indicator (via the change event); the manual action
 * also surfaces a toast so an explicit Refresh never fails silently. If there is no store yet, fall back to
 * opening the panel (which reports "open a folder first"). */
function refreshCommand(context: vscode.ExtensionContext) {
  if (store) {
    // Wrap the reload in a Window progress heartbeat so a manual Refresh shows a visible busy affordance for its
    // whole duration (the status-bar spinner also lights up via `isLoading`), rather than looking like nothing
    // happened until it settles (Goal B). [spec-vscode-any-workspace-and-processing-indicators]
    const s = store;
    // Same "rendering…" wording as the status-bar spinner and the cold-open notification, so the several busy
    // affordances never disagree on their label (frozen boundary: one coherent busy signal). [review patch]
    void vscode.window.withProgress(
      { location: vscode.ProgressLocation.Window, title: 'SpecScribe: rendering…' },
      () => s.load(),
    ).then(undefined, (err) =>
      vscode.window.showWarningMessage(`SpecScribe refresh failed: ${String(err)}`));
  } else {
    openStatus(context, 'dashboard');
  }
}

/** Stands up the single panel and wires load / navigation / live-push / manual-refresh, returning the controller
 * the command handlers steer. Per-open state (current surface, disposed flag, pending reveal) stays closed over
 * here; the payload cache itself now lives in the shared {@link SpecScribeStore} so the tree and status bar read
 * the SAME data with no panel open (Story 6.9). One open path, parametrized by the initial {@link Reveal}. */
function createController(
  context: vscode.ExtensionContext,
  folder: vscode.WorkspaceFolder,
  initialReveal: Reveal,
): PanelController {
  const p = (panel = createPanel(context));
  let disposed = false;
  let painted = false;                                // true once first-paint set the document (guards live-push)
  let current = '';                                   // the surface the user is looking at — refreshes re-push THIS
  let pendingReveal: Reveal = initialReveal;          // applied once the first payload lands
  /** A surface the user asked for while the payload was still a first-paint PRELUDE (`payload.partial`), i.e.
   * before its delta landed. Held so the click RESOLVES when the remainder arrives instead of needing a second
   * click — the I/O matrix's "Link resolves once the delta lands" row. Cleared the moment it is honored, and
   * overwritten (not queued) by a later click: only the most recent intent is worth honoring.
   * [spec-vscode-extension-name-latency-and-webview-sunburst Goal 2] */
  let awaitingSurface: string | undefined;
  let lastHostStatusKey = '';

  // Read `store` fresh on every use rather than capturing it once: a workspace-folder change (bindWorkspace)
  // disposes the old store and rebinds the module-level `store` to a new one, and this panel must follow that
  // rebind rather than keep reading a disposed, dead store (whose watchers no longer fire).
  const currentStore = () => store ?? (store = new SpecScribeStore(context, folder));

  p.onDidDispose(() => { disposed = true; panel = undefined; active = undefined; });

  function push(target: string, reason: 'navigate' | 'refresh', fragment = '') {
    const cache = currentStore().payload;
    if (!cache) return;
    // Silently swapping to the dashboard because a surface has not been RENDERED YET is exactly the "blank/wrong
    // region, no explanation" failure the prelude split must not introduce. While the payload is partial, say so
    // and honor the request when the remainder lands. A missing target on a COMPLETE payload keeps the original
    // entry fallback — that one really is unreachable. [Goal 2]
    if (cache.partial && target !== '' && !cache.surfaces[target]) {
      awaitingSurface = target;
      void vscode.window.showInformationMessage(
        `SpecScribe: "${target}" is still loading — it will open as soon as this project finishes rendering.`);
      return;
    }
    const surface = cache.surfaces[target] ?? cache.surfaces[cache.entry];
    if (!surface) return;
    current = cache.surfaces[target] ? target : cache.entry;
    // `source` carries the swapped-in surface's repo-relative artifact (Story 6.10) so the bridge can refresh
    // #specscribe-surface's data-source and show/hide the "Open source" button; '' when the surface has none.
    p.webview.postMessage({ type: 'update', html: surface.content, path: current, source: surface.sourcePath ?? '', reason, fragment });
    pushLiveStamp();
  }

  /** Story 22.6 AC #5 — the "Quiet Stamp", host half. Reports the live `--serve` channel's state as WORDS
   * ("Live updates: connected · updated 14:32" / "Live updates: unavailable"), never by color and never by
   * motion, and the webview rewrites the existing element's textContent so nothing shifts.
   *
   * <p>The HOST owns this because the host owns the connection: `persistentUnavailable` is the one place that
   * knows whether a live channel exists at all, and the webview script cannot see it. A panel fed by the
   * one-shot spawn path correctly reads "unavailable" — it is not receiving live updates, and saying otherwise
   * would be a lie the user would only discover by noticing stale content.</p> */
  function pushLiveStamp(): void {
    const live = currentStore().hasLiveChannel;
    const when = new Date();
    const time = `${String(when.getHours()).padStart(2, '0')}:${String(when.getMinutes()).padStart(2, '0')}`;
    p.webview.postMessage({
      type: 'liveStatus',
      text: live ? `Live updates: connected · updated ${time}` : 'Live updates: unavailable',
    });
  }

  function postHostStatus(payload: HostStatusPayload): void {
    p.webview.postMessage(payload);
    const key = `${payload.level}|${payload.text}|${(payload.actions ?? []).map((a) => a.id).join(',')}`;
    if (key !== lastHostStatusKey) {
      lastHostStatusKey = key;
      logHost(`status ${payload.level}: ${payload.text}`);
    }
  }

  function clearHostStatus(): void {
    p.webview.postMessage({ type: 'hostStatus', text: '' });
    if (lastHostStatusKey !== '') {
      lastHostStatusKey = '';
      logHost('status cleared');
    }
  }

  function pushHostStatus(): void {
    const s = currentStore();
    const cache = s.payload;
    const stageText = (progress?: WebviewProgress): string | undefined => {
      if (!progress?.total || progress.total <= 0) return undefined;
      const step = Math.min(Math.max(progress.step, 0), progress.total);
      return `Stage ${step}/${progress.total}: ${progress.label}`;
    };
    if (s.isLoading) {
      const progressText = stageText(cache?.progress);
      postHostStatus({
        type: 'hostStatus',
        level: 'progress',
        text: progressText
          ? `${progressText} (${Object.keys(cache?.surfaces ?? {}).length} surfaces ready)`
          : 'Stage 1/3: Starting render and scanning project content…',
      });
      return;
    }
    if (!cache) {
      postHostStatus({
        type: 'hostStatus',
        level: 'progress',
        text: 'Stage 1/3: Waiting for initial SpecScribe payload…',
      });
      return;
    }
    if (s.lastError) {
      postHostStatus({
        type: 'hostStatus',
        level: 'warning',
        text: `Last refresh failed; showing cached data. ${describeError(s.lastError)}`,
        actions: [{ id: 'refresh', label: 'Retry' }],
      });
      return;
    }
    if (cache.partial) {
      const progressText = stageText(cache.progress) ?? 'Stage 2/3: Dashboard ready; loading remaining surfaces…';
      postHostStatus({
        type: 'hostStatus',
        level: 'progress',
        text: `${progressText} (${Object.keys(cache.surfaces).length} surfaces currently available)`,
      });
      return;
    }

    const sourceRoot = cache.sourceRoot ?? '_bmad-output';
    const sourceAbs = path.resolve(lastRepoRoot ?? folder.uri.fsPath, sourceRoot);
    if (!fs.existsSync(sourceAbs)) {
      postHostStatus({
        type: 'hostStatus',
        level: 'warning',
        text: `No artifact source folder found at ${sourceRoot}. Configure project paths to load planning content.`,
        actions: [
          { id: 'openProjectSettings', label: 'Configure Paths' },
          { id: 'refresh', label: 'Retry' },
        ],
      });
      return;
    }

    const errors = s.diagnostics.filter((d) => d.severity === 'error').length;
    const warnings = s.diagnostics.filter((d) => d.severity === 'warning').length;
    if (errors > 0 || warnings > 0) {
      const summary = `${errors} error${errors === 1 ? '' : 's'} and ${warnings} warning${warnings === 1 ? '' : 's'} detected while rendering.`;
      postHostStatus({
        type: 'hostStatus',
        level: errors > 0 ? 'error' : 'warning',
        text: summary,
        actions: [{ id: 'refresh', label: 'Refresh' }],
      });
      return;
    }

    if ((cache.outline?.epics.length ?? 0) === 0) {
      postHostStatus({
        type: 'hostStatus',
        level: 'info',
        text: 'No epics were detected in this workspace yet. Dashboard code-map and README views are still available.',
        actions: [{ id: 'openProjectSettings', label: 'Configure Paths' }],
      });
      return;
    }

    clearHostStatus();
  }

  p.webview.onDidReceiveMessage(async (msg: { type?: string; target?: string; fragment?: string; href?: string; text?: string; label?: string; path?: string; line?: number; action?: string }) => {
    if (msg?.type === 'copyHelperText' && typeof msg.text === 'string') {
      // Read-only helper handoff (AD-6/NFR-5): the webview generated a prompt; the only thing the host does is put
      // it on the clipboard. NOTHING here writes a project artifact, edits a file, or mutates settings. [Story 6.5]
      await copyToClipboard(msg.text, msg.label ?? 'text');
      return;
    }
    if (msg?.type === 'revealSource' && typeof msg.path === 'string') {
      // Reveal source (AC #1) — open the surface's core-emitted `.md` read-only, optionally at a line (AC #2's
      // line-capable seam, ridden by Story 7.2's code citations). `showTextDocument` OPENS an editor; it never
      // writes (AD-6/ADR 0003/FR-17/NFR-5). The path is core-resolved repo-relative; join it to the resolved REPO
      // ROOT (Story 6.11 — correct on a subdir-open, not just the workspace folder) through the containment guard so
      // a stale/hostile payload can't turn this into "open any file".
      const target = resolveWorkspacePath(lastRepoRoot ?? folder.uri.fsPath, msg.path);
      if (!target) {
        // Mirror openSource's feedback (Story 6.9/6.10 share one convention): a rejected/missing path is never
        // silent, whether triggered from the tree or the webview.
        void vscode.window.showErrorMessage(`SpecScribe: couldn't open ${msg.path} — not found in this workspace.`);
        return;
      }
      const options = typeof msg.line === 'number' && msg.line > 0
        ? { selection: new vscode.Range(msg.line - 1, 0, msg.line - 1, 0) } // data-line is 1-based; Range is 0-based
        : undefined;
      try {
        await vscode.window.showTextDocument(vscode.Uri.file(target), options);
      } catch (err) {
        void vscode.window.showErrorMessage(`SpecScribe: couldn't open ${msg.path}: ${String(err)}`);
      }
      return;
    }
    if (msg?.type === 'hostAction' && typeof msg.action === 'string') {
      if (msg.action === 'openProjectSettings') {
        void vscode.commands.executeCommand('specscribe.openProjectSettings');
      } else if (msg.action === 'refresh') {
        refreshCommand(context);
      } else if (msg.action === 'openToolPathSettings') {
        void vscode.commands.executeCommand('workbench.action.openSettings', 'specscribe.toolPath');
      }
      return;
    }
    if (msg?.type === 'navigate' && typeof msg.target === 'string') {
      const cache = currentStore().payload;
      if (!cache) return;
      if (!cache.surfaces[msg.target]) {
        if (cache.partial) {
          // The payload is still the first-paint PRELUDE: this surface is being rendered right now and lands on
          // the next frame. Saying "isn't available in the in-editor view" here would be simply false, and telling
          // the user to run `specscribe generate` would send them off to fix a non-problem. Remember the target so
          // the click resolves itself when the remainder arrives. [Goal 2]
          awaitingSurface = msg.target;
          void vscode.window.showInformationMessage(
            `SpecScribe: "${msg.target}" is still loading — it will open as soon as this project finishes rendering.`);
          return;
        }
        // Not one of the webview's navigable surfaces. Since spec-webview-doc-page-surfaces the bundle carries
        // the whole site EXCEPT code/commit-drill pages (owner-excluded — they scale with the target repo), so
        // this is the honest fallback for those hrefs and for stale/unknown targets. No promise about what a
        // click "does instead": only 7.2 citation anchors (data-code-path) open real files, not plain hrefs.
        void vscode.window.showInformationMessage(
          `SpecScribe: "${msg.target}" isn't available in the in-editor view. ` +
          'Run "specscribe generate" to browse the full site in a browser.');
        return;
      }
      awaitingSurface = undefined;
      push(msg.target, 'navigate', msg.fragment ?? '');
      return;
    }
    if (msg?.type === 'openExternal' && typeof msg.href === 'string' && /^(https?|mailto):/i.test(msg.href)) {
      // Only web/mail schemes leave the editor; anything else from page content is ignored.
      void vscode.env.openExternal(vscode.Uri.parse(msg.href));
    }
  });

  // Live host-push (AD-8, ADR 0005 §3): when the shared store re-renders (watcher-driven or manual), re-push the
  // surface the user is on, in place. The watchers themselves live in the store now (so the tree stays live with
  // no panel); this panel just reacts to the store's change event. Guarded by `painted` so the very first change
  // (fired as the initial load settles) never posts into a webview whose bridge script isn't installed yet.
  const sub = dataChanged.event(() => {
    if (disposed || !painted) return;
    pushHostStatus();
    // Only re-push on a SETTLED load (fresh payload), never on the load-START fire. Goal B added a start-fire purely
    // to light the status-bar spinner; if the panel also reacted to it, it would swap the surface with the STALE
    // pre-refresh payload at start and again with fresh content at settle — a double swap that resets in-surface
    // state (the Code page's pure-CSS Insights|Code tab) and re-flashes insight animations on every refresh.
    // `isLoading` is true only between the start-fire and the settle-fire, so this cleanly keeps the single swap.
    // [spec-vscode-any-workspace-and-processing-indicators review patch]
    if (currentStore().isLoading) return;
    const cache = currentStore().payload;
    if (!cache) return;
    // A surface clicked while the payload was still the first-paint prelude resolves HERE, the moment the frame
    // carrying it lands — the honest completion of the "still loading" answer, rather than leaving the user to
    // click again. Cleared unconditionally once the payload is complete, so an unknown target that will never
    // arrive falls back to the ordinary refresh instead of being retried forever. [Goal 2]
    if (awaitingSurface !== undefined && !cache.partial) {
      const target = awaitingSurface;
      awaitingSurface = undefined;
      if (cache.surfaces[target]) {
        push(target, 'navigate');
        return;
      }
    }
    push(current, 'refresh');
  });
  p.onDidDispose(() => sub.dispose());

  // Visibility-aware refresh (R6.3): when this panel becomes visible, flush a watcher-driven reload the store
  // deferred while every consumer was hidden. Harmless when nothing is dirty. [Story 6.11]
  const visSub = p.onDidChangeViewState((e) => { if (e.webviewPanel.visible) currentStore().flushIfDirty(); });
  p.onDidDispose(() => visSub.dispose());

  // Cold-start heartbeat (R7.1): the first spawn is cold (~3.5 s), so wrap it in a Notification progress so first
  // paint always has a visible affordance rather than an inert blank panel.
  void (async () => {
    let cache: WebviewPayload;
    try {
      // Reuse the shared cache if the tree (or a prior open) already loaded it — opening the panel then costs no
      // second spawn (Story 6.9). Only a cold store pays the ~3.5 s render, wrapped in the progress heartbeat.
      cache = currentStore().payload ?? await vscode.window.withProgress(
        { location: vscode.ProgressLocation.Notification, title: 'SpecScribe: rendering…' },
        () => currentStore().load(),
      );
    } catch (err) {
      // Show the error page, but drop the singleton so a later "Open Status" re-renders instead of just revealing
      // this dead panel — the user may have fixed the tool path in the meantime. Alongside the (script-free) page,
      // raise an actionable notification with native buttons (R7.2); the page itself stays script-free by design.
      if (!disposed) p.webview.html = errorHtml(String(err));
      panel = undefined;
      active = undefined;
      void showActionableError(context, err);
      return;
    }
    if (disposed) return; // panel closed during the (possibly ~3.5s cold) spawn — never touch a disposed webview

    // First paint: set the full document ONCE (the only place a nonce is minted). Every navigation and every
    // live-push thereafter is an in-place postMessage swap, so the panel never resets (AC #3).
    current = cache.entry;
    p.title = `SpecScribe: ${cache.siteTitle}`;
    p.webview.html = composeEntryHtml(p.webview, cache);
    painted = true;
    const progressTag = cache.progress
      ? `${cache.progress.step}/${cache.progress.total}:${cache.progress.key}`
      : 'n/a';
    logHost(
      `payload loaded: surfaces=${Object.keys(cache.surfaces).length}, partial=${cache.partial === true}, ` +
      `diagnostics=${currentStore().diagnostics.length}, sourceRoot=${cache.sourceRoot ?? '_bmad-output'}, ` +
      `progress=${progressTag}`,
    );
    pushHostStatus();
    // Apply the initial reveal (dashboard is already the entry; epics/a tree surface swaps in place once).
    const initialKey = resolveReveal(cache, pendingReveal);
    if (initialKey !== cache.entry) push(initialKey, 'navigate');
  })();

  return {
    reveal(reveal: Reveal) {
      p.reveal();
      const cache = currentStore().payload;
      if (cache && painted) {
        const key = resolveReveal(cache, reveal);
        if (key !== current) push(key, 'navigate');
      } else {
        pendingReveal = reveal; // not painted yet — the first-paint block will honor this
      }
    },
    reload() { refreshCommand(context); },
  };
}

// ===== Story 6.9: the shared payload provider ================================================================

/** The one owner of the cached `specscribe webview` payload: a single spawn (coalesced), a single cache, and a
 * single change signal the panel + tree + status bar all react to. Promoting acquisition out of the panel closure
 * (6.4/6.8) is what lets the tree stay live with no panel open. The watchers live here too (the tree needs them
 * without a panel). Story 6.11 hardened them: the globs admit the yaml/toml data sources, they rebuild from the
 * core-resolved roots (correct on a subdir-open / non-default roots), and the reload is visibility-gated. Debounce
 * stays 400 ms, scope stays `workspaceFolders[0]` (multi-root is out of scope). Read-only: watching takes no locks
 * and writes nothing. [Story 6.9, Story 6.11] */
class SpecScribeStore {
  private cache: WebviewPayload | undefined;
  private loading: Promise<WebviewPayload> | undefined; // coalesces concurrent spawns (rapid saves, nav during load)
  private error: Error | undefined;
  private readonly watchers: vscode.Disposable[] = [];
  private dirty = false;                 // a watcher fired while no consumer was visible — reload on next reveal (R6.3)
  private rootsKey: string | undefined;  // the resolved-roots signature the current watchers were built from (R6.2)
  private lastDiagnostics: RawDiagnostic[] = [];

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly folder: vscode.WorkspaceFolder,
  ) {}

  get payload(): WebviewPayload | undefined { return this.cache; }
  get outline(): ProjectOutline | undefined { return this.cache?.outline; }
  get lastError(): Error | undefined { return this.error; }
  get isLoaded(): boolean { return this.cache !== undefined; }
  /** True while the cached payload is still the first-paint PRELUDE — the entry surface alone, with the epics
   * family and the long tail arriving on the next delta frame. The tree reads this so an outline that is merely
   * NOT THERE YET is not reported as "no epics here", which for a real BMad project is simply false.
   * [Goal 2, spec-vscode-extension-name-latency-and-webview-sunburst] */
  get isPartial(): boolean { return this.cache?.partial === true; }
  /** True while a spawn is in flight — drives the status bar's "rendering…" busy indicator so an in-progress
   * render never looks inert (Goal B). [spec-vscode-any-workspace-and-processing-indicators] */
  get isLoading(): boolean { return this.loading !== undefined; }
  get diagnostics(): readonly RawDiagnostic[] { return this.lastDiagnostics; }

  /** A live `specscribe webview --serve` connection, once one has started successfully. Undefined before the first
   * attempt, after a permanent fallback ({@link persistentUnavailable}), or after {@link dispose}.
   * [Deferred item, Story 6.4 review — scoped re-render] */
  private persistent: PersistentRenderer | undefined;
  /** Set permanently once a `--serve` attempt exits before its first payload (older core without the flag, or a
   * crash) — every subsequent {@link load} falls back to the one-shot spawn-per-call path instead of retrying
   * `--serve` on every call. */
  private persistentUnavailable = false;

  /** True when a live `--serve` connection is actually up — the datum the Quiet Stamp reports (Story 22.6 AC #5).
   * Deliberately BOTH conditions: `persistent` alone would read true for a connection that has spawned but never
   * produced a payload, and `!persistentUnavailable` alone would read true before the first attempt was even
   * made. The stamp must claim "connected" only when a payload has genuinely arrived over the live channel. */
  get hasLiveChannel(): boolean {
    return this.persistent !== undefined && !this.persistentUnavailable && this.cache !== undefined;
  }

  /** Spawn (or join an in-flight spawn) and update the shared cache. Fires the fan-out on every settle: on success
   * the cache + configured-output-root refresh and the error clears; on failure the LAST-GOOD cache is retained
   * (so the tree keeps showing data) and the error is recorded for the stale indicator. The promise still rejects
   * so a manual Refresh can surface a toast — auto (watcher) callers swallow it and rely on the stale UI.
   * <p>Prefers the persistent `--serve` connection ({@link loadViaPersistent}) so a live edit updates the panel via
   * an already-running process instead of a fresh full-regen spawn (ADR 0005 §3's scoped re-render); falls back to
   * the original per-call spawn ({@link loadViaSpawn}) permanently once `--serve` proves unavailable.</p> */
  load(): Promise<WebviewPayload> {
    if (this.loading) return this.loading;
    const attempt = this.persistentUnavailable ? this.loadViaSpawn() : this.loadViaPersistent();
    this.loading = attempt
      .catch((err) => {
        this.error = asError(err); // keep this.cache as the last-good snapshot
        throw err;
      })
      .finally(() => {
        this.loading = undefined;
        dataChanged.fire();
      });
    // Fire the fan-out on load START too (not only on settle): the status bar reads `isLoading` and shows a
    // spinner while the ~3.5s spawn runs, so an open/refresh never looks inert (Goal B). Reuses the same event the
    // panel/tree/status bar already subscribe to — no new plumbing. [spec-vscode-any-workspace-and-processing-indicators]
    dataChanged.fire();
    return this.loading;
  }

  /** Applies one settled payload (from either path) to the shared cache/roots/diagnostics — the single place both
   * loading strategies converge, so a live-pushed `--serve` payload and a one-shot spawn payload are indistinguishable
   * to every downstream consumer (tree/status bar/panel). */
  private applyPayload(payload: WebviewPayload, diagnostics: RawDiagnostic[]): WebviewPayload {
    this.cache = payload;
    this.error = undefined;
    this.lastDiagnostics = diagnostics;
    lastConfiguredOutputRoot = payload.configuredOutputRoot ?? lastConfiguredOutputRoot;
    // Resolve the absolute repo root ONCE (workspace folder + core-emitted offset) and share it for the watchers
    // AND the reveal-source join, then (re)build the watchers from the payload's resolved roots. [Story 6.11]
    lastRepoRoot = path.resolve(this.folder.uri.fsPath, payload.repoRoot ?? '.');
    lastSettingsPath = payload.settingsPath ?? lastSettingsPath;
    this.rebuildWatchersFromRoots(payload);
    // Rebuild the Problems panel from this run's notices (clearing any a later run resolved). Only on success —
    // a failed load leaves the collection as last-good, mirroring the tree/status-bar stale behavior. [Story 6.12]
    publishDiagnostics(this.folder, diagnostics);
    return payload;
  }

  /** The original behavior: one spawn, one payload, process exits. Used whenever `--serve` is unavailable. */
  private loadViaSpawn(): Promise<WebviewPayload> {
    return runRenderer(this.context, this.folder.uri.fsPath)
      .then(({ payload, diagnostics }) => this.applyPayload(payload, diagnostics));
  }

  /** Starts (or reuses) the persistent `--serve` connection. The returned promise settles on the FIRST payload
   * only — every later push from the same long-lived process updates the cache and fires {@link dataChanged}
   * directly, outside this promise, exactly like a watcher-driven reload used to trigger a fresh {@link load}
   * call. If the process exits before ever producing a payload (older core, or a crash), this call — and every
   * `load()` after it — permanently falls back to {@link loadViaSpawn}. [Deferred item, Story 6.4 review]</p> */
  private loadViaPersistent(): Promise<WebviewPayload> {
    if (this.persistent) {
      // Already running: a running `--serve` connection only pushes on its OWN debounce; there is no
      // "give me the current state now" request in the NDJSON protocol, so a manual reload while persistent
      // mode is live just re-resolves the last-pushed cache (or the last error, if none has landed yet).
      return this.cache
        ? Promise.resolve(this.cache)
        : Promise.reject(this.error ?? new Error('SpecScribe --serve has not produced a payload yet.'));
    }
    return new Promise<WebviewPayload>((resolve, reject) => {
      let initialSettled = false;
      const renderer: PersistentRenderer = new PersistentRenderer(
        this.context,
        this.folder.uri.fsPath,
        (payload, diagnostics) => {
          this.applyPayload(payload, diagnostics);
          if (!initialSettled) {
            initialSettled = true;
            resolve(payload);
          } else {
            // A later live-push, not the call that started this promise — fan out directly.
            dataChanged.fire();
          }
        },
        (err, hadPayload) => {
          this.persistent = undefined;
          if (!initialSettled) {
            // Never produced a payload — `--serve` is unsupported (older core) or failed immediately; stop
            // retrying it for the rest of this session and fall back to the proven one-shot spawn-per-save path.
            this.persistentUnavailable = true;
            initialSettled = true;
            this.loadViaSpawn().then(resolve, reject);
          } else {
            // Died after already streaming at least one payload — likely transient (crash, disk hiccup), not
            // "unsupported"; `persistentUnavailable` stays false so the next load() retries `--serve`. This
            // call's own promise already resolved, so recover by triggering a fresh load() now instead of
            // leaving the panel silently stale until an unrelated save/manual refresh. [Review][Patch]
            this.error = asError(err);
            void this.load().catch(() => { /* stale UI covers it */ });
          }
        },
      );
      this.persistent = renderer;
      renderer.start();
    });
  }

  /** Bootstrap the watchers on the literal fallback globs (anchored to the workspace folder) so an edit BEFORE the
   * first load still triggers a lazy reload. Once a payload lands, {@link load} rebuilds these from the core-resolved
   * roots (bootstrap-then-rebuild — see {@link rebuildWatchersFromRoots}). Story 6.11 un-froze 6.9's watchers: the
   * globs now admit the yaml/toml data sources (sprint-status.yaml, _bmad/config.toml) past *.md, the folder anchor
   * becomes the resolved repo root on rebuild, and the reload is visibility-gated. Debounce stays 400 ms. Watching
   * takes no locks and writes nothing (NFR5). [Story 6.11] */
  startWatching(): void {
    this.installWatchers(this.folder.uri, ['_bmad-output/**/*.{md,yaml,yml}', 'docs/adrs/**/*.md', '_bmad/config.toml']);
  }

  /** Rebuild the watchers from the payload's resolved roots (repo-relative source/ADR globs, anchored to the ABSOLUTE
   * repo root), so a non-default `--source`/`--adrs` or a subdir-open watches the right tree — no path literal in TS
   * beyond the fallback. No-op when the core omitted the roots (older core → keep the bootstrap literals) or when the
   * resolved roots are unchanged (avoid churning watchers on every refresh). [Story 6.11] */
  private rebuildWatchersFromRoots(payload: WebviewPayload): void {
    if (payload.sourceRoot === undefined && payload.adrRoot === undefined && payload.repoRoot === undefined) {
      return; // older core: the bootstrap literal-glob watchers stay
    }
    const repoAbs = path.resolve(this.folder.uri.fsPath, payload.repoRoot ?? '.');
    const source = payload.sourceRoot ?? '_bmad-output';
    const adr = payload.adrRoot ?? 'docs/adrs';
    const key = `${repoAbs}|${source}|${adr}`;
    if (key === this.rootsKey) return; // already watching these exact roots
    this.rootsKey = key;
    this.disposeWatchers();
    this.installWatchers(vscode.Uri.file(repoAbs), [`${source}/**/*.{md,yaml,yml}`, `${adr}/**/*.md`, '_bmad/config.toml']);
  }

  /** Create the file-system watchers for a base + globs, all funneling into ONE debounced, visibility-gated reload
   * ({@link onWatchEvent}). Read-only: createFileSystemWatcher observes; it takes no locks. */
  private installWatchers(base: vscode.Uri, globs: string[]): void {
    const debounced = debounce(() => this.onWatchEvent(), 400);
    for (const glob of globs) {
      const watcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(base, glob));
      watcher.onDidChange(debounced);
      watcher.onDidCreate(debounced);
      watcher.onDidDelete(debounced);
      this.watchers.push(watcher);
    }
  }

  private disposeWatchers(): void {
    for (const w of this.watchers) w.dispose();
    this.watchers.length = 0;
  }

  /** A debounced watcher event: reload if a consumer (panel or tree) is visible, else mark dirty and defer the spawn
   * until one reveals (R6.3 — no ~2 s render burst while nothing is visible). Only ever reloads once something has
   * loaded (before first use the lazy first-load on reveal covers it — unchanged from 6.9). [Story 6.11] */
  private onWatchEvent(): void {
    if (!this.cache) return;
    if (anyConsumerVisible()) {
      void this.load().catch(() => { /* stale UI covers it */ });
    } else {
      this.dirty = true;
    }
  }

  /** Flush a deferred (dirty) watcher-driven reload once, when a consumer becomes visible. The manual Refresh and the
   * tree's lazy first-load are visibility-independent by nature and never touch this flag. [Story 6.11] */
  flushIfDirty(): void {
    if (this.dirty && this.cache) {
      this.dirty = false;
      void this.load().catch(() => { /* stale UI covers it */ });
    }
  }

  dispose(): void {
    this.disposeWatchers();
    this.persistent?.dispose();
    this.persistent = undefined;
  }
}

/** True when the panel OR the outline tree is currently visible — the gate for the store's watcher-driven reload
 * (R6.3). `RelativePattern`/`createFileSystemWatcher` keep firing while hidden; this lets the store defer the spawn
 * until something is on screen to see the result. [Story 6.11] */
function anyConsumerVisible(): boolean {
  return (panel?.visible ?? false) || (treeView?.visible ?? false);
}

// ===== Story 6.9: the activity-bar tree ======================================================================

/** A tree node: an epic (collapsible parent), a story (leaf), or a transient message (loading / empty / stale).
 * Discriminated so `getTreeItem` maps each with zero interpretation. */
type OutlineNode =
  | { kind: 'epic'; epic: OutlineEpic }
  | { kind: 'story'; story: OutlineStory; epic: OutlineEpic }
  | { kind: 'message'; label: string; icon?: string };

/** The ONE piece of "logic" the shim is allowed (Story 6.9 Dev Notes): a pure lookup from the core-emitted stage
 * string to a stable codicon shape. Color comes from the contributed `specscribe.status.<stage>` theme color, so
 * the six-stage vocabulary survives (constraint #5) and the shape reinforces it (UX-DR17: never color-only). NO
 * built-in severity ThemeIcon (iconPassed / problemsError-style) — those collapse six stages onto three. */
const STAGE_ICON: Record<string, string> = {
  done: 'pass-filled',
  review: 'eye',
  active: 'circle-filled',
  ready: 'circle-large-outline',
  drafted: 'circle-outline',
  pending: 'circle-slash',
};

function stageIcon(stage: string): vscode.ThemeIcon {
  const glyph = STAGE_ICON[stage];
  // An unrecognized stage means the core emitted a stage string this map's six-stage vocabulary doesn't cover
  // (drift, or a future 7th stage) — flag it visibly (warning color/shape) rather than blending silently into
  // one of the six known looks.
  return glyph
    ? new vscode.ThemeIcon(glyph, new vscode.ThemeColor(`specscribe.status.${stage}`))
    : new vscode.ThemeIcon('question', new vscode.ThemeColor('problemsWarningIcon.foreground'));
}

class OutlineTreeProvider implements vscode.TreeDataProvider<OutlineNode> {
  private readonly changeEmitter = new vscode.EventEmitter<OutlineNode | undefined>();
  readonly onDidChangeTreeData = this.changeEmitter.event;

  refresh(): void { this.changeEmitter.fire(undefined); }

  getTreeItem(node: OutlineNode): vscode.TreeItem {
    if (node.kind === 'message') {
      const item = new vscode.TreeItem(node.label, vscode.TreeItemCollapsibleState.None);
      if (node.icon) item.iconPath = new vscode.ThemeIcon(node.icon);
      item.contextValue = 'message';
      return item;
    }
    if (node.kind === 'epic') {
      const e = node.epic;
      const collapsible = e.stories.length > 0
        ? vscode.TreeItemCollapsibleState.Expanded
        : vscode.TreeItemCollapsibleState.None;
      const item = new vscode.TreeItem(`Epic ${e.number}: ${e.title}`, collapsible);
      item.description = `${e.storiesDone}/${e.storiesTotal}`;
      item.iconPath = stageIcon(e.stage);
      item.tooltip = `Epic ${e.number}: ${e.title} — ${e.stageLabel} (${e.storiesDone}/${e.storiesTotal} stories done)`;
      item.contextValue = 'epic';
      if (e.surfacePath) {
        item.command = { command: 'specscribe.revealSurface', title: 'Reveal in panel', arguments: [e.surfacePath] };
      }
      return item;
    }
    const s = node.story;
    const item = new vscode.TreeItem(`${s.id} ${s.title}`, vscode.TreeItemCollapsibleState.None);
    if (s.tasksTotal > 0) item.description = `${s.tasksDone}/${s.tasksTotal}`;
    item.iconPath = stageIcon(s.stage);
    item.tooltip = `${s.id} ${s.title} — ${s.stageLabel}` +
      (s.tasksTotal > 0 ? ` (${s.tasksDone}/${s.tasksTotal} tasks)` : '');
    // contextValue gates which read-only context actions appear (Open Source / Copy BMad Command…). The
    // `-helper` gate is simply "the core-decided command list is non-empty" — a done story's list is empty, so
    // it exposes no copy action at all. No status logic here (AD-2): the core decides, the gate relays.
    item.contextValue = 'story' + (s.sourcePath ? '-source' : '') + (availableStoryCommands(s).length > 0 ? '-helper' : '');
    if (s.surfacePath) {
      item.command = { command: 'specscribe.revealSurface', title: 'Reveal in panel', arguments: [s.surfacePath] };
    }
    return item;
  }

  getChildren(node?: OutlineNode): OutlineNode[] {
    if (node) {
      return node.kind === 'epic'
        ? node.epic.stories.map((story) => ({ kind: 'story', story, epic: node.epic }))
        : [];
    }

    // Root. With no folder open, return nothing so the `!specscribe.available` viewsWelcome ("Open a folder…")
    // shows (and never spawn a render there). A folder being open — not a bmad marker — is the single gate; a
    // non-bmad folder still loads and simply shows the "no epics" state below.
    if (!folderOpen || !store) return [];

    const outline = store.outline;
    if (!outline) {
      if (store.isLoaded) {
        // A payload DID load successfully, but it carries no `outline` field — an older (pre-6.9) core binary,
        // not a load failure. Re-spawning would never fix this (the same stale tool would keep answering the
        // same way), so show a static message instead of looping `store.load()` on every tree refresh.
        return [messageNode('⚠ SpecScribe tool is out of date — update it to see the project outline', 'warning')];
      }
      // Detected but nothing loaded yet: lazily trigger the FIRST spawn (this is the natural lazy activation cost —
      // a spawn on first reveal, not on activation), and show a graceful loading node meanwhile. On the failure of
      // that first load, show an error node instead of re-spawning in a loop.
      if (store.lastError) return [messageNode('⚠ Could not load SpecScribe data — check the tool path', 'warning')];
      void store.load().catch(() => { /* the failure re-renders via the change event */ });
      return [messageNode('Loading SpecScribe outline…', 'loading~spin')];
    }

    const nodes: OutlineNode[] = [];
    // Stale/error affordance (AC #2): a failed refresh must be visible — surface it above the last-good data.
    if (store.lastError) nodes.push(messageNode('⚠ Last refresh failed — showing cached data', 'warning'));
    if (outline.epics.length === 0 && store.isPartial) {
      // The payload is still the first-paint PRELUDE (the entry surface alone): the epics family is being
      // rendered right now and lands on the next delta frame. Falling through to the "no epics here" node below
      // would state something false about a real BMad project for the length of that window. [Goal 2]
      nodes.push(messageNode('Loading SpecScribe outline…', 'loading~spin'));
      return nodes;
    }
    if (outline.epics.length === 0) {
      // No epics is the normal state for a non-bmad workspace (or a bmad project with no epics yet) — read it as
      // designed guidance, not an error: the dashboard still renders this folder's code map & README.
      // [spec-vscode-any-workspace-and-processing-indicators]
      nodes.push(messageNode('No epics here — open the dashboard for this folder’s code map & README', 'info'));
      return nodes;
    }
    nodes.push(...outline.epics.map((epic): OutlineNode => ({ kind: 'epic', epic })));
    return nodes;
  }
}

function messageNode(label: string, icon?: string): OutlineNode {
  return { kind: 'message', label, icon };
}

// ===== Sidebar shortcuts (host chrome) =======================================================================

/** One shortcut node. Either a HOST-CHROME entry — an already-registered command whose label mirrors the manifest
 * command title, the one sanctioned class of shim-authored text — or a PROJECT entry projected from the core's
 * `outline.shortcuts`, which reveals a surface this run actually produced.
 * [spec-vscode-sidebar-shortcuts-…-quickpick] */
interface Shortcut { label: string; icon: string; command: string; tooltip: string; arguments?: unknown[] }

// The two view-opening entries stay PINNED AT THE TOP (owner decision, 2026-07-12 F5 review, unchanged): Refresh
// lives on the outline's title bar, and Generate / Watch stay Command-Palette operations rather than permanent
// sidebar real estate. What changed is only what follows them.
const SHORTCUTS: readonly Shortcut[] = [
  { label: 'Open Dashboard', icon: 'dashboard', command: 'specscribe.openDashboard', tooltip: 'Open the SpecScribe status panel on the dashboard' },
  { label: 'Open Epics', icon: 'list-tree', command: 'specscribe.openEpics', tooltip: 'Open the SpecScribe status panel on the epics index' },
  // Promoted from Palette-only when it stopped being "open a file in an editor" and became a form that can
  // actually fix a misconfigured project (ADR 0037). It is the first thing a user needs when the portal is empty
  // because the paths are wrong, which is precisely when they will not think to search the Command Palette.
  { label: 'Project Settings', icon: 'settings-gear', command: 'specscribe.openProjectSettings', tooltip: 'Configure this project’s SpecScribe settings (source, ADR and output roots, deep-git, dates)' },
];

/** Core icon-vocabulary key → codicon. The SAME shape as {@link STAGE_ICON} and for the same AD-2 reason: the core
 * emits its own concept keys (the labels `Icons.ForConcept` is keyed on) and the shim decides only how VS Code
 * draws them. Emitting codicon names from C# would put host vocabulary in the core; emitting SVG would be
 * rendering (ADR 0005 §1). An unmapped key falls back to a neutral glyph rather than breaking the row. */
const CONCEPT_ICON: Readonly<Record<string, string>> = {
  'Code Map': 'symbol-structure',
  'Risk Quadrant': 'warning',
  'Git Insights': 'git-commit',
  'Deep Analytics': 'graph',
  'Activity Timeline': 'history',
  Epics: 'list-tree',
  Requirements: 'checklist',
  Traceability: 'references',
  Sprint: 'project',
  Cadence: 'pulse',
  'Impact Map': 'type-hierarchy',
  'Work Graph': 'type-hierarchy-sub',
  'Deferred Work': 'circle-slash',
  'Action Items': 'tasklist',
  'Follow-ups': 'tasklist',
  Readme: 'book',
  PRD: 'file-text',
  'Product Brief': 'file-text',
  Architecture: 'symbol-namespace',
  ADRs: 'law',
  'Design System': 'symbol-color',
  'Test Artifacts': 'beaker',
  About: 'info',
  Logs: 'output',
};

/** The Shortcuts section pinned above the Project Outline.
 *
 * <p><b>Was:</b> two hard-coded entries, identical in every workspace. Field feedback 2026-08-01, from a project
 * with a PRD and no epics: "Open Epics" led to an empty page and nothing led to the Code Map — the one surface that
 * repository had. The entries below the two pinned ones now come from `outline.shortcuts`, which the core projects
 * from `SiteNav.QuickLinks` — the same list that decides which pages the run writes. The pane therefore cannot
 * offer a link to a page that does not exist, and the shim holds no project knowledge (AD-1/AD-2): it maps a
 * concept key to a codicon and nothing else.</p>
 *
 * <p>Read-only by construction — every node either invokes an already-registered command or reveals a rendered
 * surface in the panel; nothing executes on the user's behalf (AD-6).</p> */
class ShortcutsTreeProvider implements vscode.TreeDataProvider<Shortcut> {
  private readonly emitter = new vscode.EventEmitter<void>();
  readonly onDidChangeTreeData = this.emitter.event;

  /** Re-read `outline.shortcuts` — wired to the same store change event the outline tree uses, so the pane
   * repopulates when the first payload lands rather than staying at its two pinned entries. */
  refresh(): void {
    this.emitter.fire();
  }

  getTreeItem(s: Shortcut): vscode.TreeItem {
    const item = new vscode.TreeItem(s.label, vscode.TreeItemCollapsibleState.None);
    item.iconPath = new vscode.ThemeIcon(s.icon);
    item.tooltip = s.tooltip;
    item.command = { command: s.command, title: s.label, arguments: s.arguments };
    return item;
  }

  getChildren(element?: Shortcut): Shortcut[] {
    if (element) return [];

    const nodes = [...SHORTCUTS];
    // `Home` and `Epics` would duplicate the two pinned entries above; `Delivery`-grouped Epics is the same page
    // "Open Epics" opens. Filtered by PATH, not by label, so a relabelled nav entry cannot slip a duplicate in.
    const pinned = new Set(['index.html', 'epics.html']);
    for (const s of store?.outline?.shortcuts ?? []) {
      if (pinned.has(s.surfacePath)) continue;
      nodes.push({
        label: s.label,
        icon: CONCEPT_ICON[s.iconKey] ?? 'file',
        // Description first: it is the sentence the portal itself uses for this link, and it says more than the
        // group does. The group follows so the pane still conveys the portal's own taxonomy without nesting.
        tooltip: `${s.description} (${s.group})`,
        command: 'specscribe.revealSurface',
        arguments: [s.surfacePath],
      });
    }
    return nodes;
  }
}

// ===== Story 6.9: status bar =================================================================================

/** Re-render the status-bar item from the shared outline summary (core-counted — no TS arithmetic). Hidden in a
 * non-SpecScribe repo or before any data has loaded; a warning presentation when the last refresh failed. */
function renderStatusBar(): void {
  const item = statusBar;
  if (!item) return;
  if (!folderOpen || !store) { item.hide(); return; }

  if (store.isLoading || store.isPartial) {
    // `isPartial` rides the same branch as `isLoading` on purpose: the first-paint PRELUDE settles the load (the
    // panel can paint) while the epic/story outline it counts is still being rendered, so the summary would read
    // "0 active · 0 review" for the length of that window. A busy indicator is the honest answer — the counts are
    // not zero, they are not known yet. [Goal 2, spec-vscode-extension-name-latency-and-webview-sunburst]
    //
    // A spawn is in flight (first render, manual refresh, or watcher rebuild): show a live busy indicator so the
    // user sees that work is happening rather than an inert or stale count (Goal B). Takes precedence over the
    // last-good count and the stale-error state — we're actively re-rendering. [spec-vscode-any-workspace…]
    item.text = '$(sync~spin) SpecScribe: rendering…';
    item.tooltip = 'SpecScribe is rendering the project view…';
    item.backgroundColor = undefined;
    item.show();
    return;
  }

  if (store.lastError) {
    // A failed refresh must not leave the last-good count looking current (AC #2). Word this differently on a
    // first-ever failure (isLoaded false, no cache exists yet) than on a refresh failure with a last-good cache.
    item.text = '$(warning) SpecScribe: data stale';
    item.tooltip = store.isLoaded
      ? `SpecScribe: last refresh failed — showing cached data.\n${describeError(store.lastError)}`
      : `SpecScribe: could not load data.\n${describeError(store.lastError)}`;
    item.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
    item.show();
    return;
  }

  const summary = store.outline?.summary;
  if (!summary) {
    // Bound to a folder but nothing loaded yet (the tree reveal or a panel open triggers the first spawn). This
    // used to `hide()`, which — with the item now in the right-hand group where users look for background
    // activity — read as "the extension isn't running" during exactly the window when they are waiting for it.
    // An idle, clickable affordance is the honest answer: SpecScribe is here, it has no counts yet, and clicking
    // asks for them. [owner feedback 2026-08-02]
    item.text = '$(telescope) SpecScribe';
    item.tooltip = 'SpecScribe is ready. Click to open the status panel and load this project.';
    item.backgroundColor = undefined;
    item.show();
    return;
  }

  item.text = `$(checklist) SpecScribe: ${summary.active} active · ${summary.review} review`;
  item.tooltip = `SpecScribe — ${summary.done}/${summary.total} stories done · ` +
    `${summary.active} in development · ${summary.review} in review.\nClick to open the status panel.`;
  item.backgroundColor = undefined;
  item.show();
}

// ===== Story 6.9: tree context actions (all read-only) =======================================================

/** "Open Source" (tree context action): open the story's source `.md` in a read-only editor. Resolves the
 * core-emitted REPO-relative path against the workspace folder through the SAME containment guard the webview
 * reveal uses — one convention, no `_bmad-output` literal (Story 6.10 AC #1 harmonization). No mutation —
 * `showTextDocument` only opens. Absent `sourcePath` nodes never expose this (contextValue gate). */
async function openSource(node: unknown): Promise<void> {
  const source = storyNode(node)?.story.sourcePath;
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!source || !folder) return;
  // Anchor on the resolved repo root (Story 6.11) so a subdir-open reveals correctly — same convention as the
  // webview reveal; falls back to the workspace folder before the first payload lands.
  const target = resolveWorkspacePath(lastRepoRoot ?? folder.uri.fsPath, source);
  if (!target) {
    void vscode.window.showErrorMessage(`SpecScribe: couldn't open ${source} — not found in this workspace.`);
    return;
  }
  try {
    await vscode.window.showTextDocument(vscode.Uri.file(target));
  } catch (err) {
    void vscode.window.showErrorMessage(`SpecScribe: couldn't open ${source}: ${String(err)}`);
  }
}

/** Resolve a core-emitted repo-relative path against the workspace folder, returning it ONLY if it stays inside
 * the folder, exists on disk, AND is a file. Defense-in-depth (Story 17.2 posture): the path is trusted core
 * output, but the shim must never become an "open any file on disk" primitive on a stale or hostile payload —
 * reject a `..`-escape, an absolute override, a vanished target, a directory, or a symlink that resolves outside
 * the workspace. Containment is checked against the REAL (symlink-resolved) paths, since a lexical prefix check
 * alone can't see a workspace-local symlink pointing elsewhere; on Windows the comparison is case-insensitive to
 * match the filesystem. Read-only-within-`root` is the entire contract; this joins the ONE repo-relative convention
 * (shared by the tree "Open Source" and the webview reveal) to `root`. Callers pass the resolved absolute REPO ROOT
 * (`lastRepoRoot`), so the subdir-open case (repo root ≠ workspace folder) resolves correctly — the watchers anchor
 * to the same root, one convention. [Story 6.10, Story 6.11 anchored on the resolved repo root] */
function resolveWorkspacePath(root: string, rel: string): string | undefined {
  if (!rel || path.isAbsolute(rel)) return undefined;
  const rootResolved = path.resolve(root);
  const target = path.resolve(rootResolved, rel);
  if (!fs.existsSync(target)) return undefined;
  let stat: fs.Stats;
  let realRoot: string;
  let realTarget: string;
  try {
    stat = fs.statSync(target);
    realRoot = fs.realpathSync(rootResolved);
    realTarget = fs.realpathSync(target);
  } catch {
    return undefined;
  }
  if (!stat.isFile()) return undefined;
  const norm = process.platform === 'win32' ? (s: string) => s.toLowerCase() : (s: string) => s;
  const within = norm(realTarget) === norm(realRoot) || norm(realTarget).startsWith(norm(realRoot) + path.sep);
  return within ? realTarget : undefined;
}

/** The story's status-gated command list, exactly as the core emitted it (the story page's Next Steps set, in
 * the page's order). Falls back to a one-item list from the legacy `helperCommand` when an older core omits
 * `commands`. The shim never filters by status, reorders, or composes — an empty result means "show no copy
 * action" (AD-2). [spec-vscode-sidebar-shortcuts-…-quickpick] */
function availableStoryCommands(story: OutlineStory): OutlineStoryCommand[] {
  // Shape-defensive like the rest of the payload handling (this runs inside getTreeItem, where a thrown
  // TypeError would break tree rendering): a non-array `commands`, a null entry, a non-string/blank command,
  // or a non-string description from a stale/hostile payload must degrade to "fewer options", never a crash.
  if (Array.isArray(story.commands)) {
    return story.commands
      .filter((c): c is OutlineStoryCommand =>
        !!c && typeof c.command === 'string' && c.command.trim().length > 0)
      .map((c) => ({ command: c.command, description: typeof c.description === 'string' ? c.description : '' }));
  }
  return story.helperCommand ? [{ command: story.helperCommand, description: '' }] : [];
}

/** "Copy BMad Command…" (tree context action): a Quick Pick whose labels are the LITERAL command strings the
 * core composed for this story's status — the same set, order, and descriptions as the story page's Next Steps
 * panel, so the user always sees exactly what will be copied. Picking one copies that string verbatim and the
 * toast names it; Esc copies nothing. The extension NEVER runs the command (AD-6). Empty-list nodes never
 * expose this (contextValue gate). [spec-vscode-sidebar-shortcuts-…-quickpick] */
async function copyStoryCommand(node: unknown): Promise<void> {
  const story = storyNode(node)?.story;
  if (!story) return;
  const options = availableStoryCommands(story);
  if (options.length === 0) return;
  const picked = await vscode.window.showQuickPick(
    options.map((c) => ({ label: c.command, detail: c.description || undefined })),
    {
      placeHolder: `Copy a BMad command for story ${story.id} — the picked text goes to the clipboard`,
      matchOnDetail: true, // typing filters on the description too, not just the command text
    },
  );
  if (!picked) return; // cancelled — nothing copied, no toast
  // The toast names the copied command verbatim (plain text — notifications don't render markdown).
  await copyToClipboard(picked.label, picked.label);
}

/** The one clipboard-write path (Story 6.5's pattern): write, then a confirmation toast; the try/catch is the 6.5
 * guard against a clipboard that rejects (remote/again headless). Read-only host effect. */
async function copyToClipboard(text: string, label: string): Promise<void> {
  try {
    await vscode.env.clipboard.writeText(text);
    void vscode.window.showInformationMessage(`SpecScribe: copied ${label} to the clipboard.`);
  } catch (err) {
    void vscode.window.showErrorMessage(`SpecScribe: couldn't copy to the clipboard: ${String(err)}`);
  }
}

function storyNode(node: unknown): { kind: 'story'; story: OutlineStory; epic: OutlineEpic } | undefined {
  return node && typeof node === 'object' && (node as OutlineNode).kind === 'story'
    ? (node as { kind: 'story'; story: OutlineStory; epic: OutlineEpic })
    : undefined;
}

function createPanel(context: vscode.ExtensionContext): vscode.WebviewPanel {
  // Open location is a HOST concern (ADR 0003): `beside` (default) puts status next to the file you're editing;
  // `active` reuses the focused column. Read at creation only — a later setting change applies to the next open.
  const location =
    vscode.workspace.getConfiguration('specscribe').get<string>('openLocation', 'beside') === 'active'
      ? vscode.ViewColumn.Active
      : vscode.ViewColumn.Beside;

  const p = vscode.window.createWebviewPanel(
    'specscribeStatus',
    'SpecScribe',
    location,
    {
      enableScripts: true,           // the one nonce'd bridge script (navigation + live-push)
      retainContextWhenHidden: true, // keep scroll/DOM state across tab hides — AC #3 "context remains coherent"
      localResourceRoots: [],        // nothing loads from disk: all CSS/SVG is inlined by the C# renderer
    },
  );
  // Editor-tab icon (R7.3) — distinct from the Marketplace icon (Story 16.5). `iconPath` is a tab affordance and
  // does not load through the webview, so it needs no `localResourceRoots` entry.
  p.iconPath = vscode.Uri.joinPath(context.extensionUri, 'media', 'specscribe.svg');
  p.onDidDispose(() => { panel = undefined; });
  return p;
}

/** Open the already-generated static site's index in the default browser (R2.4). Uses the last payload's
 * `configuredOutputRoot` when a panel has loaded, else the `SpecScribeOutput` default — both resolve to the same
 * root unless the project passed `--output` (which the shim's spawn never does). Read-only: it opens a file, and
 * offers a staged-terminal generate when nothing is there rather than generating silently. */
async function openGeneratedSite() {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }
  const root = lastConfiguredOutputRoot ?? DEFAULT_OUTPUT_ROOT;
  const indexPath = path.isAbsolute(root)
    ? path.join(root, 'index.html')
    : path.join(folder.uri.fsPath, root, 'index.html');
  const resolved = resolveOpenableFile(indexPath);
  if (resolved) {
    void vscode.env.openExternal(vscode.Uri.file(resolved));
    return;
  }
  void vscode.window.showInformationMessage(
    `SpecScribe: no generated site found at ${root}/index.html. ` +
    'Run “SpecScribe: Generate Full Site” first, then try again.');
}

/** Resolves to the REAL (symlink-followed) path only if `p` exists and is a regular file — same
 * doesn't-trust-a-stale-payload rigor `resolveWorkspacePath` applies (Story 17.2 posture), without a repo-root
 * containment check, which would break the deliberate out-of-repo `configuredOutputRoot`/`--output` case this
 * command already supports on purpose (Story 6.8 AC #3, R2.4). A broader "should this be contained at all" policy
 * is Epic 17.2's remit, not a narrow deferred-item fix. Callers must open the RETURNED real path, not `p` — opening
 * `p` itself would re-admit the symlink this validated against. Any exception (permission error, symlink cycle)
 * degrades to "not found", the same generic-failure convention `resolveWorkspacePath` already uses. */
function resolveOpenableFile(p: string): string | undefined {
  try {
    const real = fs.realpathSync(p);
    return fs.statSync(real).isFile() ? real : undefined;
  } catch {
    return undefined;
  }
}

/** Stage `<tool> generate` / `<tool> watch` at a fresh terminal prompt WITHOUT executing it (`sendText(cmd,
 * false)`) — the user presses Enter. This is the letter of AD-6/ADR 0003: SpecScribe never runs a write to the
 * project output; the explicit choice stays with the user. The command is built from the same tool resolution the
 * panel spawn uses, so a working panel never yields a "command not found" in the terminal. */
function stageTerminalCommand(context: vscode.ExtensionContext, sub: 'generate' | 'watch') {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }
  stageCommandLine(getOrCreateTerminal(folder), toolCommandLine(resolveTool(context), sub));
}

/** Show the terminal and stage `commandLine` at its prompt, WITHOUT executing it (`sendText(cmd, false)`) — the
 * user presses Enter. Marks the terminal busy immediately, before shell integration's own start event (which only
 * fires once the user actually presses Enter): otherwise two staged-but-unrun invocations in quick succession
 * would both see the terminal as idle and the second `sendText` would land on top of the first's still-unrun
 * line. This is the letter of AD-6/ADR 0003: SpecScribe never runs a write to the project output; the explicit
 * choice stays with the user. */
function stageCommandLine(terminal: vscode.Terminal, commandLine: string): void {
  terminal.show();
  busyTerminals.add(terminal);
  terminal.sendText(commandLine, false); // staged, not executed
}

/** Reuse the one "SpecScribe" terminal across repeated Generate/Watch/Setup invocations instead of piling up a
 * fresh terminal tab each time — but not while it's mid-`watch` (or any other command): staging text into a
 * terminal that's currently consuming stdin would land as confusing garbage. `busyTerminals` is populated only
 * via shell integration ({@link busyTerminals}); a terminal without shell integration (some remotes/shells)
 * never appears there, so this degrades to the prior always-reuse behavior — never worse than before. */
function getOrCreateTerminal(folder: vscode.WorkspaceFolder): vscode.Terminal {
  const existing = vscode.window.terminals.find((t) =>
    t.name === 'SpecScribe' && t.exitStatus === undefined && !busyTerminals.has(t));
  return existing ?? vscode.window.createTerminal({ name: 'SpecScribe', cwd: folder.uri.fsPath });
}

/** Locate the directory-scoped settings DOCUMENT — the file a user would actually edit — mirroring the core's
 * `SettingsStore.FindExisting` walk-up.
 *
 * <p><b>Why this is not a one-line `existsSync`.</b> Since ADR 0014 `.specscribe` is a FOLDER containing
 * `config.json`, not a flat file. The previous implementation did `existsSync('.specscribe')` and then
 * `showTextDocument` on the result — which, on every project a current CLI has configured, asked VS Code to open a
 * DIRECTORY as a text document and failed. Three states have to be distinguished, and the old code could express
 * only two:</p>
 * <ul>
 *   <li>a folder holding `config.json` → the document is `<dir>/config.json`;</li>
 *   <li>a flat file (pre-ADR-0014, still read directly by `SettingsStore.ReadConfigJson`) → that file IS the
 *       document;</li>
 *   <li>a folder with NO `config.json` — an ADR 0014 container holding only sibling state. The old code saw
 *       `existsSync → true` here and failed; it is genuinely "no settings yet".</li>
 * </ul>
 *
 * <p>Anchors on the resolved REPO ROOT (`lastRepoRoot`), not the workspace folder: a subdirectory-open would
 * otherwise miss the settings the core itself walks up to and reads, so the editor and the CLI would disagree
 * about which file is in force. Prefers the core's own `settingsPath` when a payload has supplied one, so the shim
 * re-types a SpecScribe filename only on the fallback path.</p>
 *
 * <p>Returns the real, symlink-followed path of an existing regular file, or undefined. Callers must open the
 * RETURNED path — {@link resolveOpenableFile} is what makes "opened a directory" structurally impossible here.</p> */
function resolveSettingsDocument(folder: vscode.WorkspaceFolder): string | undefined {
  const anchor = lastRepoRoot ?? folder.uri.fsPath;

  if (lastSettingsPath) {
    const fromCore = resolveOpenableFile(path.resolve(anchor, lastSettingsPath));
    if (fromCore) return fromCore;
    // Fall through rather than give up: the payload may predate a file the user has since created by hand.
  }

  let dir = anchor;
  for (;;) {
    const entry = path.join(dir, SETTINGS_ENTRY_NAME);
    try {
      const stat = fs.statSync(entry);
      // A folder is a container: the document is inside it. A file is the legacy flat form and IS the document.
      const found = resolveOpenableFile(stat.isDirectory() ? path.join(entry, SETTINGS_CONFIG_NAME) : entry);
      if (found) return found;
    } catch {
      // Absent or unreadable at this level — keep walking, exactly as the core's walk-up does.
    }
    const parent = path.dirname(dir);
    if (parent === dir) return undefined;
    dir = parent;
  }
}

/** Open the settings FORM — the extension's one authoring affordance (ADR 0037).
 *
 * <p>The form's HTML is rendered by the core (`specscribe config --form`), the shim substitutes the same two
 * placeholders it substitutes for the portal document, and Save spawns `specscribe config --save`. The shim writes
 * nothing itself: `SettingsStore` stays the single writer, so the persist-only-when-set rules, the date-token
 * vocabulary and the ADR 0014 migration are not re-implemented in TypeScript.</p>
 *
 * <p>Its own panel, not the portal's. The portal panel is live-pushed by `PersistentRenderer` and anything spliced
 * into `#specscribe-surface` is destroyed by the next `push()`. After a successful save this calls `refreshCommand`,
 * which re-renders the portal under the new settings — the two panels stay coherent without either knowing the
 * other exists.</p> */
async function openProjectSettings(context: vscode.ExtensionContext) {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }

  if (settingsPanel) {
    settingsPanel.reveal();
    return;
  }

  let document: string;
  try {
    document = await runConfig(context, folder.uri.fsPath, ['--form']);
  } catch (err) {
    // The form is what a user reaches for when generation is broken, so its OWN failure has to stay actionable
    // rather than silent — and the pre-form behaviour (open the file, or stage the interactive setup) is the
    // honest fallback.
    await settingsFallback(context, folder, String(err));
    return;
  }

  settingsPanel = createSettingsPanel(context);
  settingsPanel.webview.html = substituteHostTokens(settingsPanel.webview, document);
  bindSettingsBridge(context, settingsPanel, folder);
}

/** The pre-ADR-0037 behaviour, kept as the degrade path: reveal the settings document if there is one, else offer
 * the CLI's interactive setup in a terminal. Reached only when the core could not render the form. */
async function settingsFallback(
  context: vscode.ExtensionContext, folder: vscode.WorkspaceFolder, reason: string,
): Promise<void> {
  const document = resolveSettingsDocument(folder);
  if (document) {
    void vscode.window.showWarningMessage(`SpecScribe: could not open the settings form (${reason}) — opening the file instead.`);
    void vscode.window.showTextDocument(vscode.Uri.file(document));
    return;
  }
  const choice = await vscode.window.showInformationMessage(
    `SpecScribe: could not open the settings form (${reason}). Run the interactive setup and choose ` +
    '“Configure paths” to create the settings.',
    'Open Setup in Terminal');
  if (choice === 'Open Setup in Terminal') {
    stageCommandLine(getOrCreateTerminal(folder), toolCommandLine(resolveTool(context))); // bare tool → interactive menu
  }
}

function createSettingsPanel(context: vscode.ExtensionContext): vscode.WebviewPanel {
  const p = vscode.window.createWebviewPanel(
    'specscribeSettings',
    'SpecScribe Settings',
    vscode.ViewColumn.Active,
    {
      enableScripts: true,           // the one nonce'd bridge script the core emitted
      retainContextWhenHidden: true, // half-filled fields must survive a tab hide
      localResourceRoots: [],        // nothing loads from disk: all CSS is inlined by the C# renderer
    },
  );
  p.iconPath = vscode.Uri.joinPath(context.extensionUri, 'media', 'specscribe.svg');
  p.onDidDispose(() => { settingsPanel = undefined; });
  return p;
}

/** Wire the settings form's message channel. A SECOND `onDidReceiveMessage`, on its own panel — deliberately not
 * an extension of the portal panel's handler, whose messages (`navigate`, `revealSource`, host status) mean
 * nothing here and whose surface state this form has none of. */
function bindSettingsBridge(
  context: vscode.ExtensionContext, p: vscode.WebviewPanel, folder: vscode.WorkspaceFolder,
): void {
  p.webview.onDidReceiveMessage(async (msg: unknown) => {
    if (!msg || typeof msg !== 'object') return;
    const message = msg as { type?: unknown; field?: unknown; values?: unknown; cleared?: unknown };
    if (typeof message.type !== 'string') return;

    switch (message.type) {
      case 'settingsPick': {
        if (typeof message.field !== 'string') return;
        const picked = await vscode.window.showOpenDialog({
          canSelectFolders: true,
          canSelectFiles: false,
          canSelectMany: false,
          defaultUri: vscode.Uri.file(lastRepoRoot ?? folder.uri.fsPath),
          openLabel: 'Use this folder',
        });
        if (!picked?.[0]) return;
        // Relative to the anchor and forward-slashed: `SavedSettings` stores the string verbatim, so a relative
        // path must STAY relative or the settings stop being portable across checkouts. An absolute path is kept
        // as-is when the pick lands outside the repo, which is a legitimate `--output` case.
        const anchor = lastRepoRoot ?? folder.uri.fsPath;
        const rel = path.relative(anchor, picked[0].fsPath).split(path.sep).join('/');
        const value = rel && !rel.startsWith('..') ? rel : picked[0].fsPath.split(path.sep).join('/');
        void p.webview.postMessage({ type: 'settingsPicked', field: message.field, value });
        return;
      }

      case 'settingsSave': {
        const args = buildSaveArgs(message.values, message.cleared);
        try {
          const stdout = await runConfig(context, folder.uri.fsPath, args);
          const savedTo = parseSavedTo(stdout);
          void p.webview.postMessage({ type: 'settingsResult', ok: true, savedTo });
          lastSettingsPath = undefined; // the next payload re-supplies it; the old one may now be wrong
          // Re-render the portal under the settings just written — the whole point of configuring.
          refreshCommand(context);
        } catch (err) {
          void p.webview.postMessage({
            type: 'settingsResult',
            ok: false,
            errors: parseFieldErrors(err),
          });
        }
        return;
      }

      case 'settingsRevealFile': {
        const document = resolveSettingsDocument(folder);
        if (document) void vscode.window.showTextDocument(vscode.Uri.file(document));
        else void vscode.window.showInformationMessage('SpecScribe: no settings file yet — save the form to create one.');
        return;
      }

      case 'settingsCancel':
        p.dispose();
    }
  }, undefined, context.subscriptions);
}

/** Turn the form's `{values, cleared}` into `config --save` arguments.
 *
 * <p>An ARGS ARRAY, never a shell string. `toolCommandLine` builds a display string for the terminal handoff and
 * must not be reused here: a project name or path containing a space or a quote would be re-parsed by the shell.
 * `spawn` with an array passes each argument through untouched.</p>
 *
 * <p>Shape-defensive because it reads a webview message: a hostile or stale page cannot make this build an
 * argument out of a non-string.</p> */
function buildSaveArgs(values: unknown, cleared: unknown): string[] {
  const args = ['config', '--save'];
  const optionFor: Readonly<Record<string, string>> = {
    project: '--project-name',
    source: '--source',
    adrs: '--adrs',
    output: '--output',
    code_url: '--code-url',
    today_policy: '--today-policy',
  };

  if (values && typeof values === 'object') {
    for (const [field, raw] of Object.entries(values as Record<string, unknown>)) {
      if (typeof raw !== 'string' || raw.trim() === '') continue;
      const value = raw.trim();
      if (field === 'deep_git') { if (value === 'true') { args.push('--deep-git'); } continue; }
      // `readme` is posted positively ("include the README?") and maps onto the negative flag, exactly as the
      // interactive prompt does. `true` is the default, so it needs no argument — and passing one is impossible,
      // there being no `--readme`. Clearing is how it goes back to unset.
      if (field === 'readme') { if (value === 'false') { args.push('--no-readme'); } continue; }
      const option = optionFor[field];
      if (option) args.push(option, value);
    }
  }

  if (Array.isArray(cleared)) {
    for (const field of cleared) {
      if (typeof field === 'string' && field.trim()) args.push('--clear', field.trim());
    }
  }
  return args;
}

/** `savedTo` from a successful `config --save`, or undefined when the shape is not what we expect — the status
 * line degrades to a generic confirmation rather than printing "undefined". */
function parseSavedTo(stdout: string): string | undefined {
  try {
    const parsed = JSON.parse(stdout.trim()) as { savedTo?: unknown };
    return typeof parsed.savedTo === 'string' ? parsed.savedTo : undefined;
  } catch {
    return undefined;
  }
}

/** Field-attributed errors from a failed `config --save`. The core emits one JSON object per line on stderr — the
 * same convention the Problems-panel wire uses — precisely so the form can attach a message to the offending
 * control instead of screen-scraping a human sentence. A line that is not JSON becomes an unattached message
 * rather than being dropped. */
function parseFieldErrors(err: unknown): { field: string; message: string }[] {
  const text = err instanceof Error ? err.message : String(err);
  const errors: { field: string; message: string }[] = [];
  for (const line of text.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    try {
      const parsed = JSON.parse(trimmed) as { field?: unknown; message?: unknown };
      if (typeof parsed.message === 'string') {
        errors.push({ field: typeof parsed.field === 'string' ? parsed.field : '', message: parsed.message });
        continue;
      }
    } catch { /* not a diagnostic line — fall through */ }
    errors.push({ field: '', message: trimmed });
  }
  return errors.length > 0 ? errors : [{ field: '', message: 'Could not save the settings.' }];
}

/** Raise an actionable failure notification with native buttons (R7.2). The error page stays script-free, so the
 * actions live here: jump to the `toolPath` setting, or retry the open. */
async function showActionableError(context: vscode.ExtensionContext, err: unknown) {
  const choice = await vscode.window.showErrorMessage(
    `SpecScribe could not render: ${String(err)}`, 'Set specscribe.toolPath', 'Retry');
  if (choice === 'Set specscribe.toolPath') {
    void vscode.commands.executeCommand('workbench.action.openSettings', 'specscribe.toolPath');
  } else if (choice === 'Retry') {
    openStatus(context, 'dashboard');
  }
}

/** Build the first-paint document: substitute the two host-runtime placeholders (cspSource + a freshly minted
 * nonce) into the C# shell ONLY, never the rendered content region. The content is lifted out before substitution
 * and spliced back verbatim after, so page content that literally contains the (publicly documented)
 * `__NONCE__`/`__CSP_SOURCE__` tokens can neither be corrupted nor forge a valid script nonce to defeat the CSP. */
/** Substitute the two host-runtime placeholders in a core-rendered document that has NO separately-known content
 * region to lift out — the settings form. `composeEntryHtml` below does the same substitution for the portal
 * document, but must first lift the rendered content so page content can never forge a shell token; here the whole
 * document is the shell (the core authored every byte of it), so there is nothing to protect it from. */
function substituteHostTokens(webview: vscode.Webview, document: string): string {
  const nonce = crypto.randomBytes(16).toString('base64');
  return document.split('__CSP_SOURCE__').join(webview.cspSource).split('__NONCE__').join(nonce);
}

function composeEntryHtml(webview: vscode.Webview, payload: WebviewPayload): string {
  const nonce = crypto.randomBytes(16).toString('base64');
  const content = payload.surfaces[payload.entry]?.content ?? '';
  // Random per-call sentinel (same pattern as the nonce): a fixed literal could collide with pre-existing text in
  // the C# shell (CSS/script) and corrupt it on the final swap-back, defeating the whole point of this technique.
  const sentinel = ` __specscribe_content_${crypto.randomBytes(8).toString('hex')}__ `;
  // The entry content is inlined exactly once (WrapDocument put it at __CONTENT__); pull it out so the token
  // replace below can only ever touch the shell the C# renderer controls.
  const shell = content && payload.document.includes(content)
    ? payload.document.split(content).join(sentinel)
    : payload.document;
  const runtimeShell = shell.split('__CSP_SOURCE__').join(webview.cspSource).split('__NONCE__').join(nonce);
  return runtimeShell.split(sentinel).join(content);
}

/** Resolution order shared by the panel spawn AND the terminal handoff so they can never drift: explicit setting →
 * binary bundled with the extension (populated by Story 16.5's packaging) → `specscribe` on PATH. A `.dll` value
 * runs via `dotnet`, surfaced as a `dotnet` command with the dll as its first prefix arg. */
interface ResolvedTool {
  command: string;
  prefixArgs: string[];
}

function resolveTool(context: vscode.ExtensionContext): ResolvedTool {
  const configured = vscode.workspace.getConfiguration('specscribe').get<string>('toolPath')?.trim();
  const bundled = path.join(context.extensionPath, 'bin', process.platform === 'win32' ? 'specscribe.exe' : 'specscribe');
  const tool = configured || (fs.existsSync(bundled) ? bundled : 'specscribe');
  return tool.toLowerCase().endsWith('.dll')
    ? { command: 'dotnet', prefixArgs: [tool] }
    : { command: tool, prefixArgs: [] };
}

/** A shell command line for the staged terminal handoff. Tokens containing whitespace OR a double quote are
 * double-quoted, with any embedded `"` escaped as `\"`; the common resolved forms (`dotnet <dll> generate`,
 * `specscribe generate`) need no quoting and run as-is in every shell. Staged only (`sendText(cmd, false)`) —
 * the user reviews the line and presses Enter, so this is a display nicety, not a command-injection boundary.
 * Omit `sub` for the bare interactive invocation. */
function toolCommandLine(tool: ResolvedTool, sub?: string): string {
  const parts = [tool.command, ...tool.prefixArgs];
  if (sub) parts.push(sub);
  return parts.map(quoteCommandArg).join(' ');
}

/** Best-effort shell-family detection for {@link quoteCommandArg}. On Windows, `process.platform` alone can't
 * tell PowerShell (doesn't treat `\` as a string escape — needs doubled `""`) apart from a bash-family profile
 * (Git Bash/WSL, both common non-default Windows terminal profiles) where adjacent quoted strings just
 * CONCATENATE — `""` silently drops the embedded quote instead of escaping it, worse than the backslash form it
 * would replace. Reads the user's configured `terminal.integrated.defaultProfile.windows` and only claims
 * PowerShell/cmd-style (doubled-quote) escaping for a profile that doesn't look bash-like; an unset/auto-detected
 * profile defaults to PowerShell-style, matching VS Code's own out-of-the-box Windows default. Non-Windows always
 * uses POSIX escaping. [Blind Hunter + Edge Case Hunter, spec-epic6-deferred-debt-cleanup review] */
function usesPosixStyleQuoting(): boolean {
  if (process.platform !== 'win32') return true;
  const profile = vscode.workspace.getConfiguration('terminal.integrated').get<string>('defaultProfile.windows');
  // The alternation is GROUPED so its precedence is explicit (typescript:S5850): ungrouped, `$` binds only to
  // the final branch and the intent is left to the reader. The asymmetry is deliberate and must be kept —
  // `bash`/`wsl` match anywhere in the profile name ("Git Bash", "bash.exe", "Ubuntu (WSL)"), while `sh` is
  // anchored to the END because unanchored it also matches "PowerShell" (p-o-w-e-r-**sh**-e-l-l), which is the
  // exact profile this predicate must answer NO for. Anchored, it still catches "zsh" and "sh". [Story 17.1]
  return typeof profile === 'string' && /(?:bash|wsl|sh$)/i.test(profile);
}

/** Shell-aware quoting: doubles an embedded `"` (`""`) for a PowerShell/cmd-style profile, backslash-escapes it
 * (`\"`) for a POSIX-style one (see {@link usesPosixStyleQuoting}). PowerShell does not treat `\` as an escape
 * character inside a double-quoted string, so backslash-escaping alone (correct for POSIX shells) could stage a
 * line that mis-parses there; doubling the quote is the form PowerShell string literals (`"a""b"` → `a"b`) and
 * cmd.exe's own line parser recognize — NOT the Win32 `CommandLineToArgvW`/CRT argv-parsing convention used when a
 * program is spawned directly rather than typed at a shell prompt, which backslash-escapes instead. A token
 * ending in `\` immediately before the closing quote is still the classic Windows argv ambiguity — untouched by
 * either escaping choice, and rare enough in combination with an embedded `"` (staged only, never auto-run) to
 * accept rather than build a full per-shell quoting engine for. [spec-6-9-deferred-debt-cleanup review] */
function quoteCommandArg(a: string): string {
  if (!/[\s"]/.test(a)) return a;
  // `replaceAll` with a literal needle rather than a /g regex, and `String.raw` for the backslash form, so the
  // escape sequence reads as the two characters it produces (typescript:S7780/S7781). Behaviour is identical:
  // neither replacement contains a `$`, which is the only way string-replacement semantics could differ.
  return usesPosixStyleQuoting() ? `"${a.replaceAll('"', String.raw`\"`)}"` : `"${a.replaceAll('"', '""')}"`;
}

/** Spawn `specscribe config …` and resolve its stdout, or reject with its stderr. [ADR 0037]
 *
 * <p>Separate from {@link runRenderer} rather than a parameter on it: that one carries a 60 s timeout and a
 * stdout-size cap sized for a multi-megabyte portal payload, streams NDJSON, and rejects on a partial frame — none
 * of which fits a command whose output is a few kilobytes and whose failures are field-attributed JSON lines the
 * caller must see verbatim.</p>
 *
 * <p>REJECTS WITH STDERR, not with a generic message: `config --save` reports validation failures as one JSON
 * object per line there, and {@link parseFieldErrors} attaches each to its control. Collapsing that into "command
 * failed" is what would force the form to screen-scrape.</p> */
function runConfig(context: vscode.ExtensionContext, cwd: string, args: string[]): Promise<string> {
  const tool = resolveTool(context);
  return new Promise<string>((resolve, reject) => {
    // Args array, never a shell string — a path or project name containing a space or a quote must reach the tool
    // as one argument. `toolCommandLine` builds a DISPLAY string for the terminal handoff and must not be used here.
    const proc = spawn(tool.command, [...tool.prefixArgs, ...args], { cwd });
    let out = '';
    let errText = '';
    let aborted = false;

    const abort = (reason: string) => {
      if (aborted) return;
      aborted = true;
      killWithEscalation(proc);
      reject(new Error(reason));
    };
    // Generous for a command that only reads settings and renders one form; short enough that a wedged process
    // does not leave the user staring at a panel that never opens.
    const timer = setTimeout(() => abort('SpecScribe config timed out after 30s.'), 30_000);

    proc.stdout?.on('data', (chunk: Buffer) => { out += chunk.toString('utf8'); });
    proc.stderr?.on('data', (chunk: Buffer) => { errText += chunk.toString('utf8'); });
    proc.on('error', (e) => { clearTimeout(timer); if (!aborted) reject(e); });
    proc.on('close', (code) => {
      clearTimeout(timer);
      if (aborted) return;
      if (code === 0) resolve(out);
      else reject(new Error(errText.trim() || `SpecScribe config exited ${code}.`));
    });
  });
}

/** Kills a renderer process, escalating to SIGKILL after a grace period if it hasn't exited — a bare SIGTERM
 * (`proc.kill()`'s default) is not reliably honored by the `dotnet` host on Windows, which can otherwise leave
 * an orphaned process behind. [Deferred item, Story 6.4 review] */
function killWithEscalation(proc: ChildProcess): void {
  proc.kill();
  const escalate = setTimeout(() => {
    if (proc.exitCode === null && proc.signalCode === null) proc.kill('SIGKILL');
  }, 5_000);
  proc.once('close', () => clearTimeout(escalate));
}

/** A generous ceiling well above this repo's observed ~8 MB whole-site webview payload — guards against a
 * runaway or looping renderer accumulating unbounded memory in the extension host, not normal output.
 * [Deferred item, Story 6.4 review] */
const MAX_RENDERER_STDOUT_BYTES = 256 * 1024 * 1024;

/** Spawn the SpecScribe tool's `webview` command and parse its stdout JSON — the extension↔core data path
 * ADR 0005 ratified. Tool resolution is shared with the terminal handoff via {@link resolveTool}. */
function runRenderer(context: vscode.ExtensionContext, cwd: string): Promise<RendererResult> {
  const tool = resolveTool(context);
  const command = tool.command;
  const args = [...tool.prefixArgs, 'webview'];

  return new Promise<RendererResult>((resolve, reject) => {
    const proc = spawn(command, args, { cwd });
    // Set once the process is being force-aborted (timeout or stdout-size cap) — guards against the timeout
    // firing after the size cap already aborted (or vice versa) and overwriting the real reason, and against
    // 'close' treating an aborted run as a plain non-zero exit. [Review][Patch]
    let abortReason: string | undefined;

    // Rejects IMMEDIATELY on abort rather than waiting for the killed process's 'close' event — the original
    // behavior before SIGKILL escalation was added, which this preserves: the error toast must not wait on
    // however long the (possibly unresponsive) process takes to actually die. [Review][Patch]
    const abort = (reason: string) => {
      if (abortReason) return; // first abort wins
      abortReason = reason;
      killWithEscalation(proc);
      reject(new Error(reason));
    };

    // A renderer that never returns must not hang forever (cold spawns measured ~3.5 s; 60 s is a generous
    // ceiling for very large repos).
    const timer = setTimeout(() => abort('SpecScribe renderer timed out after 60s.'), 60_000);

    let out = '';
    let errText = '';
    let outBytes = 0;
    // Decode as a UTF-8 stream, not per Buffer chunk: a multibyte char (em-dashes are pervasive in the payload)
    // split across a chunk boundary would otherwise decode to replacement chars and corrupt the content.
    proc.stdout.setEncoding('utf8');
    proc.stderr.setEncoding('utf8');
    proc.stdout.on('data', (d: string) => {
      if (abortReason) return;
      outBytes += Buffer.byteLength(d, 'utf8');
      if (outBytes > MAX_RENDERER_STDOUT_BYTES) {
        abort('SpecScribe renderer output exceeded the size ceiling; process killed.');
        return;
      }
      out += d;
    });
    proc.stderr.on('data', (d) => (errText += d));
    proc.on('error', (e) => { clearTimeout(timer); if (!abortReason) reject(e); });
    proc.on('close', (code) => {
      clearTimeout(timer);
      if (abortReason) return; // already rejected via abort() — this is just the (possibly delayed) process death
      // A non-zero exit is a real crash whose stderr is a .NET stack trace, not our notice lines (notices are
      // non-fatal, exit 0), so the error toast keeps using errText verbatim — no diagnostics parsed here.
      if (code !== 0) return reject(new Error(`SpecScribe renderer exited ${code}: ${errText || '(no stderr)'}`));
      let payload: WebviewPayload;
      try {
        payload = JSON.parse(out) as WebviewPayload;
      } catch (e) {
        return reject(new Error(`SpecScribe renderer produced invalid JSON: ${String(e)}`));
      }
      // stderr carries the structured notice lines (Story 6.12); parse them independently of the stdout payload.
      resolve({ payload, diagnostics: parseDiagnostics(errText) });
    });
  });
}

/** Parse the `webview` command's stderr into notice records: split on newlines and `JSON.parse` each non-empty
 * line, skipping any that don't parse or lack a string `path`/`message`, or whose `severity` isn't one of the two
 * values `publishDiagnostics` understands (`'error'`/`'warning'`). Tolerant by design — an older core's human
 * `[specscribe webview] …` line, a future field, or a stray .NET log line must never throw or produce a partial
 * record — but a record that DOES parse as JSON is validated field-by-field rather than admitted on a `path`/
 * `severity`-type check alone: an unrecognized `severity` is dropped here (explicit, visible) instead of silently
 * falling through to `publishDiagnostics`'s `=== 'error' ? Error : Warning` ternary, which would otherwise recolor
 * it as `'warning'` with no signal that the value was unrecognized; a non-string `message` is dropped too, since
 * `vscode.Diagnostic`'s constructor requires one. [Story 6.12] [Story 6.11 deferred-work cleanup] */
function parseDiagnostics(errText: string): RawDiagnostic[] {
  const records: RawDiagnostic[] = [];
  for (const line of errText.split('\n')) {
    const trimmed = line.trim();
    if (trimmed.length === 0) continue;
    try {
      const rec = JSON.parse(trimmed) as RawDiagnostic;
      if (
        typeof rec.path === 'string' &&
        (rec.severity === 'error' || rec.severity === 'warning') &&
        typeof rec.message === 'string'
      ) {
        records.push(rec);
      }
    } catch {
      // Not one of our JSON notice lines — ignore it (backward/forward compatibility).
    }
  }
  return records;
}

/** A long-lived `specscribe webview --serve` connection: spawns once and calls `onPayload` for every NDJSON line
 * on stdout (the initial render AND every subsequent incremental live-push), instead of the one-shot
 * spawn-per-save `runRenderer` path re-running a full generation on every save. Reuses `SerializePayload`'s exact
 * wire shape (one `WebviewPayload` per line) — the same JSON.parse this file already does for the one-shot path,
 * just applied per-line instead of once to the whole stdout buffer. If the process exits before ever producing a
 * payload (an older core without `--serve`, or a crash), `onUnavailable` fires once so the caller can fall back to
 * the one-shot path. [Deferred item, Story 6.4 review — ADR 0005 §3 scoped re-render]</p> */
class PersistentRenderer implements vscode.Disposable {
  private proc: ChildProcess | undefined;
  private buffer = '';
  private errText = '';
  private gotFirstPayload = false;
  private torndown = false;
  /** The last COMPLETE payload handed downstream — the basis each delta frame is merged onto. Undefined until the
   * session's first (full) push. [Story 22.6] */
  private lastPayload: WebviewPayload | undefined;
  /** The last applied delta sequence, reset to 0 by every full payload (which re-bases the session). Used only to
   * detect a GAP; the merge itself never consults it. */
  private lastSequence = 0;

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly cwd: string,
    private readonly onPayload: (payload: WebviewPayload, diagnostics: RawDiagnostic[]) => void,
    // `hadPayload` distinguishes "never produced a payload" (caller must synchronously fall back to resolve its
    // pending initial-load promise) from "died after already streaming at least one" (the initial promise is
    // long settled — the caller must instead trigger a fresh recovery load). Fires exactly once per connection,
    // regardless of when in its lifetime it dies — the previous "only if !gotFirstPayload" gate left a
    // post-first-payload death undetected forever, permanently staling the panel with no fallback. [Review][Patch]
    private readonly onUnavailable: (err: unknown, hadPayload: boolean) => void,
  ) {}

  start(): void {
    const tool = resolveTool(this.context);
    // `--serve-delta` (Story 22.6 AC #3) asks the core to push only what changed after the first full payload.
    // Safe against an OLDER core that does not know the flag: Spectre rejects the unknown option and the process
    // exits before ever writing a payload, which is exactly the `persistentUnavailable` condition — `teardown`
    // fires with hadPayload=false and the store falls back to `loadViaSpawn` permanently. That is the same path
    // an older core without `--serve` at all already takes, so no new failure mode is introduced.
    const args = [...tool.prefixArgs, 'webview', '--serve', '--serve-delta'];
    const proc = spawn(tool.command, args, { cwd: this.cwd });
    this.proc = proc;
    proc.stdout.setEncoding('utf8');
    proc.stderr.setEncoding('utf8');
    proc.stdout.on('data', (d: string) => this.onStdoutChunk(d));
    // stderr in serve mode carries one Problems-notice batch per push, same JSON-lines shape as the one-shot path
    // (Story 6.12) — accumulated and attributed to whichever payload line completes next, a best-effort pairing
    // since stdout/stderr are separate streams (notices are advisory, never load-bearing for correctness).
    proc.stderr.on('data', (d: string) => (this.errText += d));
    proc.on('error', (e) => this.teardown(e));
    proc.on('close', (code) => this.teardown(new Error(`specscribe webview --serve exited ${code}`)));
  }

  private teardown(err: unknown): void {
    if (this.torndown) return; // 'error' and 'close' can both fire for the same spawn failure — report once
    this.torndown = true;
    this.onUnavailable(err, this.gotFirstPayload);
  }

  private onStdoutChunk(chunk: string): void {
    // A stdout listener is never detached once attached (Node has no such API on a stream), so without this
    // guard a process that already tore down for one reason (buffer ceiling, a bad delta frame, a sequence gap)
    // keeps parsing and re-dispatching every further chunk it happens to emit before the kill below actually
    // lands — racing whatever fresh connection `onUnavailable`'s caller spawns to replace it. [Review][Patch]
    if (this.torndown) return;
    this.buffer += chunk;
    // Same rationale as the one-shot path's MAX_RENDERER_STDOUT_BYTES cap, applied to the unterminated-line
    // buffer here — this connection is explicitly designed to run far longer (indefinitely) than the bounded
    // one-shot spawn, making an unbounded buffer the MORE likely place to leak memory, not less. [Review][Patch]
    if (Buffer.byteLength(this.buffer, 'utf8') > MAX_RENDERER_STDOUT_BYTES) {
      if (this.proc) killWithEscalation(this.proc);
      this.teardown(new Error('specscribe webview --serve line buffer exceeded the size ceiling; connection killed.'));
      return;
    }
    let newlineIndex: number;
    while ((newlineIndex = this.buffer.indexOf('\n')) >= 0) {
      const line = this.buffer.slice(0, newlineIndex);
      this.buffer = this.buffer.slice(newlineIndex + 1);
      if (line.trim().length === 0) continue;
      let parsed: WebviewPayload | DeltaFrame;
      try {
        parsed = JSON.parse(line) as WebviewPayload | DeltaFrame;
      } catch {
        continue; // a stray non-JSON line on stdout — ignore rather than tear down the connection
      }

      let payload: WebviewPayload;
      if (isDeltaFrame(parsed)) {
        // A delta with nothing to merge onto cannot be applied. The core never does this (the first push of a
        // session is always full), so reaching here means the stream is not what we think it is — fall back
        // rather than render a payload assembled from a guess.
        //
        // The process is still alive and producing output at this point — unlike the buffer-ceiling branch
        // above, this used to tear down without killing it, leaving the old connection's file watchers running
        // and its (now stale) closures still able to call onPayload/applyPayload on the shared store after a
        // replacement connection had already been spawned. Killing it here, before teardown, closes that.
        // [Review][Patch]
        if (!this.lastPayload) {
          if (this.proc) killWithEscalation(this.proc);
          this.teardown(new Error('specscribe webview --serve sent a delta frame before any full payload'));
          return;
        }
        // A sequence GAP means a frame was missed, so the cached payload is no longer a trustworthy basis.
        // Tearing down is the honest response: `onUnavailable(err, hadPayload=true)` makes the store run a fresh
        // recovery load, where silently applying the frame would leave the panel showing a half-applied state
        // that nothing downstream could detect. [Story 22.6 AC #3]
        //
        // Killed before teardown for the same reason as the "delta before any full payload" branch above: this
        // process is still alive and would otherwise keep pushing frames the replacement connection races
        // against. [Review][Patch]
        const expected = this.lastSequence + 1;
        if (parsed.sequence !== expected) {
          if (this.proc) killWithEscalation(this.proc);
          this.teardown(new Error(
            `specscribe webview --serve delta sequence gap: expected ${expected}, got ${parsed.sequence}`));
          return;
        }
        this.lastSequence = parsed.sequence;
        payload = applyDeltaFrame(this.lastPayload, parsed);
      } else {
        payload = parsed;
        // A full payload RE-BASES the session: the core restarts its own sequence at 1 for the frames that
        // follow, so the consumer must too or the very next frame reads as a gap.
        this.lastSequence = 0;
      }

      this.lastPayload = payload;
      this.gotFirstPayload = true;
      const diagnostics = parseDiagnostics(this.errText);
      this.errText = '';
      // Downstream always receives a COMPLETE payload — the merge happens here and nowhere else, which is what
      // preserves the invariant that a live-pushed payload and a one-shot spawn payload are indistinguishable.
      this.onPayload(payload, diagnostics);
    }
  }

  dispose(): void {
    // Mark torn-down FIRST: killWithEscalation's SIGTERM/SIGKILL still fires an async 'close' event on this
    // process, which must NOT re-invoke onUnavailable — this is a deliberate, owner-initiated teardown (panel
    // closed, falling back after an earlier failure), not an unexpected death. [Review][Patch]
    this.torndown = true;
    if (this.proc) killWithEscalation(this.proc);
    this.proc = undefined;
  }
}

/** Publish the file-anchored notices into the Problems panel, grouped by file. Clears first so notices a later
 * run resolved disappear (AC #1). Non-anchored render-time (`.html`) notices are deliberately skipped — they live
 * on the diagnostics page, their home (the recommended scoping; the fallback, if the owner ever wants them in
 * Problems, is to publish them on a single workspace-folder `Uri`). Read-only: this only tells VS Code what to
 * show.
 * <p>Anchors on the resolved REPO ROOT (`lastRepoRoot`, falling back to the workspace folder before the first
 * payload lands) through the SAME `resolveWorkspacePath` containment guard `revealSource`/`openSource` use — one
 * convention, correct on a subdir-open (repo root ≠ workspace folder), and a stale/hostile `record.path` that
 * escapes the workspace or doesn't exist on disk is silently dropped rather than anchoring a Diagnostic to the
 * wrong (or a nonexistent) file. [Story 6.12] [Review][Patch]</p> */
function publishDiagnostics(folder: vscode.WorkspaceFolder, records: RawDiagnostic[]): void {
  if (!diagnosticCollection) return;
  diagnosticCollection.clear();

  const byPath = new Map<string, vscode.Diagnostic[]>();
  for (const record of records) {
    if (!record.fileAnchored) continue;
    const target = resolveWorkspacePath(lastRepoRoot ?? folder.uri.fsPath, record.path);
    if (!target) continue; // missing / escapes the workspace / not a file — never anchor to it
    const severity = record.severity === 'error'
      ? vscode.DiagnosticSeverity.Error
      : vscode.DiagnosticSeverity.Warning;
    // No source position on a notice — anchor to the file top honestly rather than parse markdown for a line (AD-2).
    const diag = new vscode.Diagnostic(new vscode.Range(0, 0, 0, 0), record.message, severity);
    diag.source = 'SpecScribe';
    const diags = byPath.get(target) ?? [];
    diags.push(diag);
    byPath.set(target, diags);
  }

  for (const [fsPath, diags] of byPath) {
    diagnosticCollection.set(vscode.Uri.file(fsPath), diags);
  }
  const fileAnchored = records.filter((r) => r.fileAnchored).length;
  const errors = records.filter((r) => r.severity === 'error').length;
  const warnings = records.filter((r) => r.severity === 'warning').length;
  logHost(`diagnostics published: records=${records.length}, fileAnchored=${fileAnchored}, errors=${errors}, warnings=${warnings}`);
}

function errorHtml(message: string): string {
  const esc = message.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c] as string));
  return `<!DOCTYPE html><html><head><meta charset="UTF-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline';"></head><body style="font:14px system-ui;padding:1.5rem"><h2>SpecScribe could not render</h2><pre style="white-space:pre-wrap;color:#c33">${esc}</pre><p>Is the SpecScribe tool available? Set <code>specscribe.toolPath</code> to the executable (or a SpecScribe.dll to run via dotnet), or install <code>specscribe</code> on PATH.</p></body></html>`;
}

function debounce<T extends (...args: never[]) => void>(fn: T, ms: number): T {
  let timer: NodeJS.Timeout | undefined;
  return ((...args: never[]) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  }) as T;
}

export function deactivate() {
  store?.dispose();
  store = undefined;
  dataChanged.dispose();
  // The collection is also disposed via context.subscriptions; null it so a re-activate rebinds a fresh one.
  diagnosticCollection = undefined;
  outputChannel = undefined;
}
