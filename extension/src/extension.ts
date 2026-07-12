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
  /** Resolved watch roots (Story 6.11), all repo/workspace-relative + forward-slashed. `sourceRoot`/`adrRoot` are the
   * source and ADR trees the file watchers are built from; `repoRoot` is the workspace-relative offset from the
   * folder to the real repo root (`.` at the root), so the shim resolves the absolute repo root once and anchors BOTH
   * the watchers AND the reveal-source join to it (correct even when opened on a subdirectory). All optional so an
   * older core still parses — the store falls back to the literal `_bmad-output`/`docs/adrs` globs when absent. */
  sourceRoot?: string;
  adrRoot?: string;
  repoRoot?: string;
  surfaces: Record<string, SurfaceContent>;
  /** The activity-bar tree + status-bar data. Optional so an older core (pre-6.9) still parses. [Story 6.9] */
  outline?: ProjectOutline;
}

/** One core-emitted generation notice, parsed from a JSON line on the `webview` command's stderr. The core owns
 * WHAT the notice says and WHICH file; the shim only decides that VS Code shows the file-anchored ones in the
 * Problems panel (constraint #1). Unknown fields are ignored and a record missing `path`/`severity` is skipped, so
 * a future core field never breaks an older shim. [Story 6.12] */
interface RawDiagnostic {
  path: string;
  severity: string;
  /** `'error'` and `'warning'` map to VS Code's Problems severities — this is the Problems domain, NOT the six
   * `--status-*` lifecycle stages (constraint #5), which never collapse onto host severities. */
  message: string;
  fileAnchored?: boolean;
}

/** One `webview` spawn's outcome: the stdout payload plus the notices parsed off its stderr JSON lines. Threaded
 * together so the store can refresh the cache and republish the Problems collection on the same settle. */
interface RendererResult {
  payload: WebviewPayload;
  diagnostics: RawDiagnostic[];
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

/** Last payload's resolved ABSOLUTE repo root (the workspace folder joined to the core-emitted `repoRoot` offset).
 * The ONE anchor shared by the store's watchers and the reveal-source join, so a subdir-open (repo root ≠ workspace
 * folder) watches and reveals the right paths. Undefined until the first payload lands → callers fall back to the
 * workspace folder (today's behavior, correct at the common repo-root open). [Story 6.11] */
let lastRepoRoot: string | undefined;

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
/** The outline TreeView handle — kept at module scope (not just pushed to subscriptions) so the visibility-aware
 * refresh can read `treeView.visible` and subscribe to `onDidChangeVisibility` (Story 6.11 R6.3). */
let treeView: vscode.TreeView<OutlineNode> | undefined;

/** The one collection the core's per-artifact generation notices publish into — VS Code renders it in the native
 * Problems panel with `SpecScribe` as the source. Rebuilt on every successful store settle (clearing notices a
 * later run resolves); left untouched on a failed load so a transient spawn error doesn't drop last-good
 * diagnostics. Pure host-UI transport — nothing here writes a project artifact (read-only, AD-6). [Story 6.12] */
let diagnosticCollection: vscode.DiagnosticCollection | undefined;

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

  // Shortcuts: a static host-chrome section pinned above the outline (labels/icons are the same class of host
  // chrome as the manifest command titles; no project content is authored here). The view itself is gated on
  // `specscribe.projectDetected` via its manifest `when`.
  context.subscriptions.push(
    vscode.window.registerTreeDataProvider('specscribe.shortcuts', new ShortcutsTreeProvider()));

  // Status bar: a summary count that opens the panel; hidden until a detected repo has data (Story 6.9 R3.2).
  statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
  statusBar.command = 'specscribe.openStatus';
  context.subscriptions.push(statusBar);

  // Problems: one DiagnosticCollection the core's generation notices publish into (source `SpecScribe`). Disposed
  // with the extension; rebuilt on every successful store load. [Story 6.12]
  diagnosticCollection = vscode.languages.createDiagnosticCollection('SpecScribe');
  context.subscriptions.push(diagnosticCollection);

  // Tree: a TreeDataProvider mapping the core outline 1:1. getChildren lazily triggers the first spawn on reveal.
  treeProvider = new OutlineTreeProvider();
  treeView = vscode.window.createTreeView('specscribe.outline', { treeDataProvider: treeProvider });
  context.subscriptions.push(treeView);
  // Visibility-aware refresh (R6.3): when the tree becomes visible, flush a watcher-driven reload deferred while it
  // was hidden. The tree's own lazy FIRST load stays in getChildren (visibility-appropriate already). [Story 6.11]
  context.subscriptions.push(treeView.onDidChangeVisibility((e) => { if (e.visible) store?.flushIfDirty(); }));

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
    if (msg?.type === 'navigate' && typeof msg.target === 'string') {
      const cache = currentStore().payload;
      if (!cache) return;
      if (!cache.surfaces[msg.target]) {
        // Not one of the webview's navigable surfaces. Since spec-webview-doc-page-surfaces the bundle carries
        // the whole site EXCEPT code/commit-drill pages (owner-excluded — they scale with the target repo), so
        // this is the honest fallback for those hrefs and for stale/unknown targets. No promise about what a
        // click "does instead": only 7.2 citation anchors (data-code-path) open real files, not plain hrefs.
        void vscode.window.showInformationMessage(
          `SpecScribe: "${msg.target}" isn't available in the in-editor view. ` +
          'Run "specscribe generate" to browse the full site in a browser.');
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
  private error: unknown | undefined;
  private readonly watchers: vscode.Disposable[] = [];
  private dirty = false;                 // a watcher fired while no consumer was visible — reload on next reveal (R6.3)
  private rootsKey: string | undefined;  // the resolved-roots signature the current watchers were built from (R6.2)

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
      .then(({ payload, diagnostics }) => {
        this.cache = payload;
        this.error = undefined;
        lastConfiguredOutputRoot = payload.configuredOutputRoot ?? lastConfiguredOutputRoot;
        // Resolve the absolute repo root ONCE (workspace folder + core-emitted offset) and share it for the watchers
        // AND the reveal-source join, then (re)build the watchers from the payload's resolved roots. [Story 6.11]
        lastRepoRoot = path.resolve(this.folder.uri.fsPath, payload.repoRoot ?? '.');
        this.rebuildWatchersFromRoots(payload);
        // Rebuild the Problems panel from this run's notices (clearing any a later run resolved). Only on success —
        // a failed load leaves the collection as last-good, mirroring the tree/status-bar stale behavior. [Story 6.12]
        publishDiagnostics(this.folder, diagnostics);
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

// ===== Sidebar shortcuts (host chrome) =======================================================================

/** One shortcut node: an already-registered host command with a codicon. Pure host chrome — the labels mirror
 * the manifest command titles (the one sanctioned class of shim-authored text); no project content and no core
 * data is interpreted here, so AD-1/AD-2 hold. [spec-vscode-sidebar-shortcuts-…-quickpick] */
interface Shortcut { label: string; icon: string; command: string; tooltip: string }

// Deliberately just the view-opening entries (owner decision, 2026-07-12 F5 review): Refresh already lives on
// the outline's title bar, and Generated Site / Generate / Watch / Project Settings are occasional operations
// that belong in the Command Palette, not permanent sidebar real estate.
const SHORTCUTS: readonly Shortcut[] = [
  { label: 'Open Dashboard', icon: 'dashboard', command: 'specscribe.openDashboard', tooltip: 'Open the SpecScribe status panel on the dashboard' },
  { label: 'Open Epics', icon: 'list-tree', command: 'specscribe.openEpics', tooltip: 'Open the SpecScribe status panel on the epics index' },
];

/** The static Shortcuts section pinned above the Project Outline: one-click nodes for the existing SpecScribe
 * commands, so every surface is reachable from the sidebar without command-palette digging. Read-only by
 * construction — each node only invokes an already-registered command (Generate/Watch stay staged-terminal
 * handoffs; nothing executes on the user's behalf, AD-6). [spec-vscode-sidebar-shortcuts-…-quickpick] */
class ShortcutsTreeProvider implements vscode.TreeDataProvider<Shortcut> {
  getTreeItem(s: Shortcut): vscode.TreeItem {
    const item = new vscode.TreeItem(s.label, vscode.TreeItemCollapsibleState.None);
    item.iconPath = new vscode.ThemeIcon(s.icon);
    item.tooltip = s.tooltip;
    item.command = { command: s.command, title: s.label };
    return item;
  }

  getChildren(element?: Shortcut): Shortcut[] {
    return element ? [] : [...SHORTCUTS];
  }
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
function runRenderer(context: vscode.ExtensionContext, cwd: string): Promise<RendererResult> {
  const tool = resolveTool(context);
  const command = tool.command;
  const args = [...tool.prefixArgs, 'webview'];

  return new Promise<RendererResult>((resolve, reject) => {
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
 * line, skipping any that don't parse or lack `path`/`severity`. Tolerant by design — an older core's human
 * `[specscribe webview] …` line, a future field, or a stray .NET log line must never throw or produce a partial
 * record. [Story 6.12] */
function parseDiagnostics(errText: string): RawDiagnostic[] {
  const records: RawDiagnostic[] = [];
  for (const line of errText.split('\n')) {
    const trimmed = line.trim();
    if (trimmed.length === 0) continue;
    try {
      const rec = JSON.parse(trimmed) as RawDiagnostic;
      if (typeof rec.path === 'string' && typeof rec.severity === 'string') records.push(rec);
    } catch {
      // Not one of our JSON notice lines — ignore it (backward/forward compatibility).
    }
  }
  return records;
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
}
