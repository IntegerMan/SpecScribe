// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// SpecScribe Status — the production thin extension-host shim (Story 6.4, governed by ADR 0005).
//
// This file is deliberately the WHOLE TypeScript surface. Its only responsibilities (the "irreducible shim"):
//   1. register commands + menus and set the `specscribe.projectDetected` context key (discoverability, Story 6.8),
//   2. open a WebviewPanel,
//   3. obtain C#-rendered HTML from the `specscribe webview` child process (spawn + stdout JSON),
//   4. inject the two host-runtime values (cspSource + nonce) the C# shell left as placeholders,
//   5. relay messages: in-webview navigation, open-external, and file-change live-push (postMessage, in place).
//
// It parses NO markdown, renders NO view, and holds NO project knowledge (AD-1/AD-2). Project *detection* is by
// path existence only (`fs.existsSync`) — not parsing, so AD-2 holds. Every byte of visible content is produced by
// the C# core. If this file grows a rendering brain, the architecture decision was wrong.
// Read-only end to end (AD-6): nothing here writes a project artifact or mutates settings. Generate/Watch/scaffold
// are STAGED into a terminal for the user to run — SpecScribe never presses Enter.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

import * as vscode from 'vscode';
import { spawn } from 'node:child_process';
import * as crypto from 'node:crypto';
import * as fs from 'node:fs';
import * as path from 'node:path';

interface SurfaceContent {
  title: string;
  content: string; // nav + breadcrumb + body — what an in-place swap installs into #specscribe-surface
}

/** One `specscribe webview` spawn's stdout: the full entry document (placeholders unsubstituted) plus every
 * navigable surface, keyed by output-relative path. See WebviewBundle in the C# core. */
interface WebviewPayload {
  siteTitle: string;
  entry: string;
  document: string;
  /** Workspace-relative root a plain `generate` writes to (forward-slashed). Host-delivered core datum, not
   * rendering (ADR 0005 §1) — the "Open Generated Site" command joins it to the folder. Optional so an older
   * core still parses. [Story 6.8] */
  configuredOutputRoot?: string;
  surfaces: Record<string, SurfaceContent>;
}

/** The direct-open target an entry point asks for. Resolved against the loaded payload's surface keys — never a
 * hard-coded path — so a renamed epics-index key can't silently open the dashboard instead. [Story 6.8] */
type SurfaceTarget = 'dashboard' | 'epics';

/** The host-side driver for the one open panel: reveal to a requested surface and force a manual reload. Lets the
 * command handlers (Open Dashboard/Epics/Refresh) steer the singleton without each forking its own open path. */
interface PanelController {
  reveal(target: SurfaceTarget): void;
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

  // Detect once now and re-evaluate whenever the folder set changes, so a late-added SpecScribe folder flips the
  // key (and the menus/commands appear) without a reload. Path existence only.
  updateDetection();
  context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => updateDetection()));
}

function isSpecScribeFolder(folderPath: string): boolean {
  return DETECTION_MARKERS.some((marker) => fs.existsSync(path.join(folderPath, marker)));
}

function updateDetection() {
  const detected = (vscode.workspace.workspaceFolders ?? []).some((f) => isSpecScribeFolder(f.uri.fsPath));
  void vscode.commands.executeCommand('setContext', 'specscribe.projectDetected', detected);
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

function openStatus(context: vscode.ExtensionContext, target: SurfaceTarget) {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }
  if (active) {
    active.reveal(target);
    return;
  }
  active = createController(context, folder, target);
}

/** Manual Refresh Status: reload the open panel in place (sharing the in-flight `load()` coalescing guard), or
 * open one to the dashboard if none exists yet. */
function refreshCommand(context: vscode.ExtensionContext) {
  if (active) {
    active.reload();
  } else {
    openStatus(context, 'dashboard');
  }
}

/** Stands up the single panel and wires load / navigation / live-push / manual-refresh, returning the controller
 * the command handlers steer. All the per-open state (cache, current surface, disposed flag) stays closed over
 * here — one open path, parametrized by the initial `SurfaceTarget`, never four copies. */
function createController(
  context: vscode.ExtensionContext,
  folder: vscode.WorkspaceFolder,
  initialTarget: SurfaceTarget,
): PanelController {
  const cwd = folder.uri.fsPath;
  const p = (panel = createPanel(context));
  let disposed = false;
  let current = '';                                   // the surface the user is looking at — refreshes re-push THIS path
  let cache: WebviewPayload | undefined;
  let loading: Promise<WebviewPayload> | undefined;   // coalesces concurrent spawns (rapid saves, nav during load)
  let pendingTarget: SurfaceTarget = initialTarget;   // applied once the first payload lands

  p.onDidDispose(() => { disposed = true; panel = undefined; active = undefined; });

  function load(): Promise<WebviewPayload> {
    loading ??= runRenderer(context, cwd).finally(() => (loading = undefined));
    return loading;
  }

  function push(target: string, reason: 'navigate' | 'refresh', fragment = '') {
    if (!cache) return;
    const surface = cache.surfaces[target] ?? cache.surfaces[cache.entry];
    if (!surface) return;
    current = cache.surfaces[target] ? target : cache.entry;
    p.webview.postMessage({ type: 'update', html: surface.content, path: current, reason, fragment });
  }

  // Manual + watcher refresh share this one reload path (and thus the same in-flight `load()` guard), so a manual
  // Refresh Status during an auto re-render can't double-spawn.
  async function reload(reason: 'refresh' = 'refresh') {
    try {
      const next = await load();
      if (disposed) return; // panel closed while the re-render was in flight — nothing to patch
      cache = next;
      lastConfiguredOutputRoot = cache.configuredOutputRoot ?? lastConfiguredOutputRoot;
      p.title = `SpecScribe: ${cache.siteTitle}`;
      push(current, reason);
    } catch (err) {
      if (!disposed) void vscode.window.showWarningMessage(`SpecScribe refresh failed: ${String(err)}`);
    }
  }

  p.webview.onDidReceiveMessage(async (msg: { type?: string; target?: string; fragment?: string; href?: string; text?: string; label?: string }) => {
    if (msg?.type === 'copyHelperText' && typeof msg.text === 'string') {
      // Read-only helper handoff (AD-6/NFR-5): the webview generated a prompt; the only thing the host does is put
      // it on the clipboard. NOTHING here writes a project artifact, edits a file, or mutates settings — clipboard
      // is the explicit handoff, and any use of the copied text is a separate user action. [Story 6.5]
      try {
        await vscode.env.clipboard.writeText(msg.text);
        void vscode.window.showInformationMessage(`SpecScribe: copied ${msg.label ?? 'text'} to the clipboard.`);
      } catch (err) {
        void vscode.window.showErrorMessage(`SpecScribe: couldn't copy to the clipboard: ${String(err)}`);
      }
      return;
    }
    if (msg?.type === 'navigate' && typeof msg.target === 'string') {
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

  // Cold-start heartbeat (R7.1): the first spawn is cold (~3.5 s), so wrap it in a Notification progress so first
  // paint always has a visible affordance rather than an inert blank panel.
  void (async () => {
    try {
      cache = await vscode.window.withProgress(
        { location: vscode.ProgressLocation.Notification, title: 'SpecScribe: rendering…' },
        () => load(),
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

    lastConfiguredOutputRoot = cache.configuredOutputRoot ?? lastConfiguredOutputRoot;

    // First paint: set the full document ONCE (the only place a nonce is minted). Every navigation and every
    // live-push thereafter is an in-place postMessage swap, so the panel never resets (AC #3).
    current = cache.entry;
    p.title = `SpecScribe: ${cache.siteTitle}`;
    p.webview.html = composeEntryHtml(p.webview, cache);
    // Apply the initial direct-open target (dashboard is already the entry; epics swaps in place once).
    if (pendingTarget !== 'dashboard') push(resolveTarget(cache, pendingTarget), 'navigate');

    // Live host-push (AD-8, ADR 0005 §3): extension-host watchers over the planning sources — the same roots the
    // C# FileWatcherService covers (_bmad-output/**/*.md + hand-authored ADRs) — debounced, then one re-render
    // spawn and an in-place patch of whichever surface the user is on. Watching is read-only: no locks, no writes.
    const debouncedRefresh = debounce(() => { void reload(); }, 400);
    for (const pattern of ['_bmad-output/**/*.md', 'docs/adrs/**/*.md']) {
      const watcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(folder, pattern));
      watcher.onDidChange(debouncedRefresh);
      watcher.onDidCreate(debouncedRefresh);
      watcher.onDidDelete(debouncedRefresh);
      context.subscriptions.push(watcher);
      p.onDidDispose(() => watcher.dispose());
    }
  })();

  return {
    reveal(target: SurfaceTarget) {
      p.reveal();
      if (cache) {
        if (target !== 'dashboard' || current !== cache.entry) push(resolveTarget(cache, target), 'navigate');
      } else {
        pendingTarget = target; // not painted yet — the first-paint block will honor this
      }
    },
    reload() { void reload(); },
  };
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
  const indexPath = path.join(folder.uri.fsPath, root, 'index.html');
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
  const terminal = vscode.window.createTerminal({ name: 'SpecScribe', cwd: folder.uri.fsPath });
  terminal.show();
  terminal.sendText(toolCommandLine(resolveTool(context), sub), false); // staged, not executed
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
    const terminal = vscode.window.createTerminal({ name: 'SpecScribe', cwd: folder.uri.fsPath });
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
    // A renderer that never returns must not hang the panel forever (cold spawns measured ~3.5 s; 60 s is a
    // generous ceiling for very large repos).
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

export function deactivate() { /* nothing to clean up beyond context.subscriptions */ }
