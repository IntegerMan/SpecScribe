// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// SpecScribe Status — the production thin extension-host shim (Story 6.4, governed by ADR 0005).
//
// This file is deliberately the WHOLE TypeScript surface. Its only responsibilities (the "irreducible shim"):
//   1. register commands + menus and set the `specscribe.projectDetected` context key (discoverability, Story 6.8),
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
// Read-only end to end (AD-6): nothing here writes a project artifact or mutates settings. Generate/Watch/scaffold
// are STAGED into a terminal for the user to run — SpecScribe never presses Enter. Tree actions only reveal a
// surface, open a `.md` read-only, or copy a prompt to the clipboard.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

import * as vscode from 'vscode';
import { spawn } from 'node:child_process';
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

/** The whole project outline (mirrors the C# `ProjectOutline`) — data, not rendering (ADR 0005 §1). [Story 6.9] */
interface ProjectOutline {
  epics: OutlineEpic[];
  summary: OutlineSummary;
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
  surfaces: Record<string, SurfaceContent>;
  /** The activity-bar tree + status-bar data. Optional so an older core (pre-6.9) still parses. [Story 6.9] */
  outline?: ProjectOutline;
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

/** Detection markers (path existence only — no reads, no parsing; AD-2). Either present ⇒ a SpecScribe repo. */
const DETECTION_MARKERS = [path.join('_bmad', 'config.toml'), '_bmad-output'];

/** Default output root when no payload has been loaded yet to supply `configuredOutputRoot` (memory: the output
 * dir is `SpecScribeOutput`, never `docs/live`). */
const DEFAULT_OUTPUT_ROOT = 'SpecScribeOutput';

let panel: vscode.WebviewPanel | undefined;
let active: PanelController | undefined;
/** Last payload's configured output root, so "Open Generated Site" needn't re-spawn just to learn the path. */
let lastConfiguredOutputRoot: string | undefined;

/** Whether folder[0] is a SpecScribe repo — gates the status bar's visibility and the tree's lazy load (mirrors
 * the `specscribe.projectDetected` context key the manifest `when` clauses use). [Story 6.9] */
let projectDetected = false;

/** The single shared payload provider: one spawn, one cache, driving the panel + tree + status bar, refreshed
 * together (Story 6.9's central refactor). Rebound to folder[0] on activation and whenever the folder set changes.
 * Undefined only before activation or with no workspace folder. */
let store: SpecScribeStore | undefined;

/** One fan-out fired whenever the shared payload (re)loads or the workspace binding changes. The status bar, the
 * tree, and the open panel each subscribe once; the store fires it on every load settle (success OR failure). */
const dataChanged = new vscode.EventEmitter<void>();

let statusBar: vscode.StatusBarItem | undefined;
let treeProvider: OutlineTreeProvider | undefined;

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
  register('specscribe.copyHelperPrompt', (node: unknown) => void copyHelperPrompt(node));

  // Status bar: a summary count that opens the panel; hidden until a detected repo has data (Story 6.9 R3.2).
  statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
  statusBar.command = 'specscribe.openStatus';
  context.subscriptions.push(statusBar);

  // Tree: a TreeDataProvider mapping the core outline 1:1. getChildren lazily triggers the first spawn on reveal.
  treeProvider = new OutlineTreeProvider();
  context.subscriptions.push(
    vscode.window.createTreeView('specscribe.outline', { treeDataProvider: treeProvider }));

  // All three consumers subscribe ONCE to the fan-out; the store fires it on every (re)load.
  context.subscriptions.push(dataChanged.event(() => { renderStatusBar(); treeProvider?.refresh(); }));

  // Bind the shared store to folder[0] and re-bind when the folder set changes (a late-added SpecScribe folder
  // flips detection without a reload). Path existence only.
  bindWorkspace(context);
  context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => bindWorkspace(context)));
}

function isSpecScribeFolder(folderPath: string): boolean {
  return DETECTION_MARKERS.some((marker) => fs.existsSync(path.join(folderPath, marker)));
}

/** (Re)bind the shared store + watchers to the current folder[0], refresh the detection context key, and update
 * the native surfaces. Disposes any prior store so a folder change never leaks watchers. [Story 6.9] */
function bindWorkspace(context: vscode.ExtensionContext) {
  store?.dispose();
  store = undefined;

  const folder = vscode.workspace.workspaceFolders?.[0];
  // Scoped to the first folder only, matching every command handler (they all act on workspaceFolders[0]).
  // Scanning every folder here would flip the context key true from a SpecScribe folder the commands never
  // touch, enabling commands that then silently act on the wrong folder[0]. Multi-root support itself stays out
  // of scope (Story 6.11) — this just keeps detection and action in sync.
  projectDetected = !!folder && isSpecScribeFolder(folder.uri.fsPath);
  void vscode.commands.executeCommand('setContext', 'specscribe.projectDetected', projectDetected);

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
    return key ?? cache.entry;
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
    void store.load().catch((err) =>
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

  // Read `store` fresh on every use rather than capturing it once: a workspace-folder change (bindWorkspace)
  // disposes the old store and rebinds the module-level `store` to a new one, and this panel must follow that
  // rebind rather than keep reading a disposed, dead store (whose watchers no longer fire).
  const currentStore = () => store ?? (store = new SpecScribeStore(context, folder));

  p.onDidDispose(() => { disposed = true; panel = undefined; active = undefined; });

  function push(target: string, reason: 'navigate' | 'refresh', fragment = '') {
    const cache = currentStore().payload;
    if (!cache) return;
    const surface = cache.surfaces[target] ?? cache.surfaces[cache.entry];
    if (!surface) return;
    current = cache.surfaces[target] ? target : cache.entry;
    // `source` carries the swapped-in surface's repo-relative artifact (Story 6.10) so the bridge can refresh
    // #specscribe-surface's data-source and show/hide the "Open source" button; '' when the surface has none.
    p.webview.postMessage({ type: 'update', html: surface.content, path: current, source: surface.sourcePath ?? '', reason, fragment });
  }

  p.webview.onDidReceiveMessage(async (msg: { type?: string; target?: string; fragment?: string; href?: string; text?: string; label?: string; path?: string; line?: number }) => {
    if (msg?.type === 'copyHelperText' && typeof msg.text === 'string') {
      // Read-only helper handoff (AD-6/NFR-5): the webview generated a prompt; the only thing the host does is put
      // it on the clipboard. NOTHING here writes a project artifact, edits a file, or mutates settings. [Story 6.5]
      await copyToClipboard(msg.text, msg.label ?? 'text');
      return;
    }
    if (msg?.type === 'revealSource' && typeof msg.path === 'string') {
      // Reveal source (AC #1) — open the surface's core-emitted `.md` read-only, optionally at a line (AC #2's
      // line-capable seam, ridden by Story 7.2's code citations). `showTextDocument` OPENS an editor; it never
      // writes (AD-6/ADR 0003/FR-17/NFR-5). The path is core-resolved repo-relative; join it to the workspace
      // folder through the containment guard so a stale/hostile payload can't turn this into "open any file".
      const target = resolveWorkspacePath(folder.uri.fsPath, msg.path);
      if (!target) return;
      const options = typeof msg.line === 'number' && msg.line > 0
        ? { selection: new vscode.Range(msg.line - 1, 0, msg.line - 1, 0) } // data-line is 1-based; Range is 0-based
        : undefined;
      void vscode.window.showTextDocument(vscode.Uri.file(target), options);
      return;
    }
    if (msg?.type === 'navigate' && typeof msg.target === 'string') {
      const cache = currentStore().payload;
      if (!cache) return;
      if (!cache.surfaces[msg.target]) {
        // Not one of the webview's navigable surfaces (e.g. a requirements or doc page): the in-editor set is
        // deliberately dashboard + epics only this story. Say so instead of a dead click.
        void vscode.window.showInformationMessage(
          `SpecScribe: "${msg.target}" isn't part of the in-editor status view (dashboard + epics). ` +
          'Run `specscribe generate` to browse the full site.');
        return;
      }
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
    if (currentStore().payload) push(current, 'refresh');
  });
  p.onDidDispose(() => sub.dispose());

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
 * (6.4/6.8) is what lets the tree stay live with no panel open. The watchers relocate here too (the tree needs
 * them without a panel) — same globs, same 400 ms debounce, same `workspaceFolders[0]` scope as 6.4 (watch-root
 * hardening is Story 6.11). Read-only: watching takes no locks and writes nothing. [Story 6.9] */
class SpecScribeStore {
  private cache: WebviewPayload | undefined;
  private loading: Promise<WebviewPayload> | undefined; // coalesces concurrent spawns (rapid saves, nav during load)
  private error: unknown | undefined;
  private readonly watchers: vscode.Disposable[] = [];

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly folder: vscode.WorkspaceFolder,
  ) {}

  get payload(): WebviewPayload | undefined { return this.cache; }
  get outline(): ProjectOutline | undefined { return this.cache?.outline; }
  get lastError(): unknown | undefined { return this.error; }
  get isLoaded(): boolean { return this.cache !== undefined; }

  /** Spawn (or join an in-flight spawn) and update the shared cache. Fires the fan-out on every settle: on success
   * the cache + configured-output-root refresh and the error clears; on failure the LAST-GOOD cache is retained
   * (so the tree keeps showing data) and the error is recorded for the stale indicator. The promise still rejects
   * so a manual Refresh can surface a toast — auto (watcher) callers swallow it and rely on the stale UI. */
  load(): Promise<WebviewPayload> {
    this.loading ??= runRenderer(this.context, this.folder.uri.fsPath)
      .then((payload) => {
        this.cache = payload;
        this.error = undefined;
        lastConfiguredOutputRoot = payload.configuredOutputRoot ?? lastConfiguredOutputRoot;
        return payload;
      })
      .catch((err) => {
        this.error = err; // keep this.cache as the last-good snapshot
        throw err;
      })
      .finally(() => {
        this.loading = undefined;
        dataChanged.fire();
      });
    return this.loading;
  }

  /** Relocated watchers (from 6.4's panel closure): a source edit debounces into one re-render + a fan-out. Only
   * reloads once something has already loaded — before first use there is nothing to refresh, and the first tree
   * reveal / panel open will lazy-load fresh, so we avoid a cold spawn on every save in an unopened repo. Globs,
   * debounce, and folder scope are frozen until Story 6.11. */
  startWatching(): void {
    const debounced = debounce(() => { if (this.cache) void this.load().catch(() => { /* stale UI covers it */ }); }, 400);
    for (const pattern of ['_bmad-output/**/*.md', 'docs/adrs/**/*.md']) {
      const watcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(this.folder, pattern));
      watcher.onDidChange(debounced);
      watcher.onDidCreate(debounced);
      watcher.onDidDelete(debounced);
      this.watchers.push(watcher);
    }
  }

  dispose(): void {
    for (const w of this.watchers) w.dispose();
    this.watchers.length = 0;
  }
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
    // contextValue gates which read-only context actions appear (Open Source / Copy Helper Prompt).
    item.contextValue = 'story' + (s.sourcePath ? '-source' : '') + (s.helperCommand ? '-helper' : '');
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

    // Root. In a non-SpecScribe workspace, return nothing so the `!projectDetected` viewsWelcome shows (and never
    // spawn a render there). Detection + the manifest `when` are the single gate.
    if (!projectDetected || !store) return [];

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
    if (outline.epics.length === 0) {
      nodes.push(messageNode('No epics found in this project', 'info'));
      return nodes;
    }
    nodes.push(...outline.epics.map((epic): OutlineNode => ({ kind: 'epic', epic })));
    return nodes;
  }
}

function messageNode(label: string, icon?: string): OutlineNode {
  return { kind: 'message', label, icon };
}

// ===== Story 6.9: status bar =================================================================================

/** Re-render the status-bar item from the shared outline summary (core-counted — no TS arithmetic). Hidden in a
 * non-SpecScribe repo or before any data has loaded; a warning presentation when the last refresh failed. */
function renderStatusBar(): void {
  const item = statusBar;
  if (!item) return;
  if (!projectDetected || !store) { item.hide(); return; }

  if (store.lastError) {
    // A failed refresh must not leave the last-good count looking current (AC #2). Word this differently on a
    // first-ever failure (isLoaded false, no cache exists yet) than on a refresh failure with a last-good cache.
    item.text = '$(warning) SpecScribe: data stale';
    item.tooltip = store.isLoaded
      ? `SpecScribe: last refresh failed — showing cached data.\n${String(store.lastError)}`
      : `SpecScribe: could not load data.\n${String(store.lastError)}`;
    item.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
    item.show();
    return;
  }

  const summary = store.outline?.summary;
  if (!summary) { item.hide(); return; } // detected but no data yet — the tree reveal / panel open will load it

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
  const target = resolveWorkspacePath(folder.uri.fsPath, source);
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
 * the folder AND exists on disk. Defense-in-depth (Story 17.2 posture): the path is trusted core output, but the
 * shim must never become an "open any file on disk" primitive on a stale or hostile payload — reject a `..`-escape,
 * an absolute override, or a vanished target. Read-only-within-workspace is the entire contract; this joins the ONE
 * repo-relative convention (shared by the tree "Open Source" and the webview reveal) to the folder. The subdir-open
 * case (repo root ≠ workspace folder) is the same limitation the watchers carry today — Story 6.11, not solved
 * here. [Story 6.10] */
function resolveWorkspacePath(root: string, rel: string): string | undefined {
  if (!rel || path.isAbsolute(rel)) return undefined;
  const rootResolved = path.resolve(root);
  const target = path.resolve(rootResolved, rel);
  const within = target === rootResolved || target.startsWith(rootResolved + path.sep);
  return within && fs.existsSync(target) ? target : undefined;
}

/** "Copy Helper Prompt" (tree context action): copy the story's core-composed helper command to the clipboard for
 * the user to run themselves. The extension NEVER runs it (AD-6). Absent `helperCommand` nodes never expose this. */
async function copyHelperPrompt(node: unknown): Promise<void> {
  const command = storyNode(node)?.story.helperCommand;
  if (!command) return;
  await copyToClipboard(command, 'helper command');
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
    'SpecScribe Status',
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
  if (fs.existsSync(indexPath)) {
    void vscode.env.openExternal(vscode.Uri.file(indexPath));
    return;
  }
  void vscode.window.showInformationMessage(
    `SpecScribe: no generated site found at ${root}/index.html. ` +
    'Run “SpecScribe: Generate Full Site” first, then try again.');
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
  const terminal = getOrCreateTerminal(folder);
  terminal.show();
  terminal.sendText(toolCommandLine(resolveTool(context), sub), false); // staged, not executed
}

/** Reuse the one "SpecScribe" terminal across repeated Generate/Watch/Setup invocations instead of piling up a
 * fresh terminal tab each time. */
function getOrCreateTerminal(folder: vscode.WorkspaceFolder): vscode.Terminal {
  const existing = vscode.window.terminals.find((t) => t.name === 'SpecScribe' && t.exitStatus === undefined);
  return existing ?? vscode.window.createTerminal({ name: 'SpecScribe', cwd: folder.uri.fsPath });
}

/** Reveal the directory-scoped `.specscribe` settings file (R5.2), or — if absent — offer to stage the CLI's own
 * interactive "Configure paths" flow in a terminal. The extension itself performs NO write to `.specscribe`
 * (ADR 0003): any creation is done by the user running the CLI, never by us. */
async function openProjectSettings(context: vscode.ExtensionContext) {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }
  const settingsPath = path.join(folder.uri.fsPath, '.specscribe');
  if (fs.existsSync(settingsPath)) {
    void vscode.window.showTextDocument(vscode.Uri.file(settingsPath));
    return;
  }
  const choice = await vscode.window.showInformationMessage(
    'SpecScribe: no “.specscribe” settings file here yet. SpecScribe won’t create one for you — run the ' +
    'interactive setup and choose “Configure paths” to write it.',
    'Open Setup in Terminal');
  if (choice === 'Open Setup in Terminal') {
    const terminal = getOrCreateTerminal(folder);
    terminal.show();
    terminal.sendText(toolCommandLine(resolveTool(context)), false); // bare tool → interactive menu; staged
  }
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

/** A shell command line for the staged terminal handoff. Tokens containing whitespace are double-quoted; the
 * common resolved forms (`dotnet <dll> generate`, `specscribe generate`) need no quoting and run as-is in every
 * shell. Omit `sub` for the bare interactive invocation. */
function toolCommandLine(tool: ResolvedTool, sub?: string): string {
  const parts = [tool.command, ...tool.prefixArgs];
  if (sub) parts.push(sub);
  return parts.map((a) => (/\s/.test(a) ? `"${a}"` : a)).join(' ');
}

/** Spawn the SpecScribe tool's `webview` command and parse its stdout JSON — the extension↔core data path
 * ADR 0005 ratified. Tool resolution is shared with the terminal handoff via {@link resolveTool}. */
function runRenderer(context: vscode.ExtensionContext, cwd: string): Promise<WebviewPayload> {
  const tool = resolveTool(context);
  const command = tool.command;
  const args = [...tool.prefixArgs, 'webview'];

  return new Promise<WebviewPayload>((resolve, reject) => {
    const proc = spawn(command, args, { cwd });
    // A renderer that never returns must not hang forever (cold spawns measured ~3.5 s; 60 s is a generous
    // ceiling for very large repos).
    const timer = setTimeout(() => {
      proc.kill();
      reject(new Error('SpecScribe renderer timed out after 60s.'));
    }, 60_000);

    let out = '';
    let errText = '';
    // Decode as a UTF-8 stream, not per Buffer chunk: a multibyte char (em-dashes are pervasive in the payload)
    // split across a chunk boundary would otherwise decode to replacement chars and corrupt the content.
    proc.stdout.setEncoding('utf8');
    proc.stderr.setEncoding('utf8');
    proc.stdout.on('data', (d) => (out += d));
    proc.stderr.on('data', (d) => (errText += d));
    proc.on('error', (e) => { clearTimeout(timer); reject(e); });
    proc.on('close', (code) => {
      clearTimeout(timer);
      if (code !== 0) return reject(new Error(`SpecScribe renderer exited ${code}: ${errText || '(no stderr)'}`));
      try {
        resolve(JSON.parse(out) as WebviewPayload);
      } catch (e) {
        reject(new Error(`SpecScribe renderer produced invalid JSON: ${String(e)}`));
      }
    });
  });
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
}
