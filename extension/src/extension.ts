// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// SpecScribe Status — the production thin extension-host shim (Story 6.4, governed by ADR 0005).
//
// This file is deliberately the WHOLE TypeScript surface. Its only responsibilities (the "irreducible shim"):
//   1. register one command,
//   2. open a WebviewPanel,
//   3. obtain C#-rendered HTML from the `specscribe webview` child process (spawn + stdout JSON),
//   4. inject the two host-runtime values (cspSource + nonce) the C# shell left as placeholders,
//   5. relay messages: in-webview navigation, open-external, and file-change live-push (postMessage, in place).
//
// It parses NO markdown, renders NO view, and holds NO project knowledge (AD-1/AD-2). Every byte of visible
// content is produced by the C# core. If this file grows a rendering brain, the architecture decision was wrong.
// Read-only end to end (AD-6): nothing here writes anywhere.
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
  surfaces: Record<string, SurfaceContent>;
}

export function activate(context: vscode.ExtensionContext) {
  context.subscriptions.push(
    vscode.commands.registerCommand('specscribe.openStatus', () => openStatus(context)),
  );
}

let panel: vscode.WebviewPanel | undefined;

async function openStatus(context: vscode.ExtensionContext) {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage('SpecScribe: open a project folder first.');
    return;
  }
  if (panel) {
    panel.reveal();
    return;
  }

  const cwd = folder.uri.fsPath;
  const p = (panel = createPanel());

  let current = ''; // the surface the user is looking at — refreshes re-push THIS path
  let cache: WebviewPayload | undefined;
  let loading: Promise<WebviewPayload> | undefined; // coalesces concurrent spawns (rapid saves, nav during load)

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

  p.webview.onDidReceiveMessage(async (msg: { type?: string; target?: string; fragment?: string; href?: string }) => {
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

  try {
    cache = await load();
  } catch (err) {
    p.webview.html = errorHtml(String(err));
    return;
  }

  // First paint: set the full document ONCE (the only place a nonce is minted). Every navigation and every
  // live-push thereafter is an in-place postMessage swap, so the panel never resets (AC #3).
  current = cache.entry;
  p.title = `SpecScribe: ${cache.siteTitle}`;
  p.webview.html = withRuntime(p.webview, cache.document);

  // Live host-push (AD-8, ADR 0005 §3): extension-host watchers over the planning sources — the same roots the
  // C# FileWatcherService covers (_bmad-output/**/*.md + hand-authored ADRs) — debounced, then one re-render
  // spawn and an in-place patch of whichever surface the user is on. Watching is read-only: no locks, no writes
  // (NFR5 — the renderer reads with shared access).
  const refresh = debounce(async () => {
    try {
      cache = await load();
      p.title = `SpecScribe: ${cache.siteTitle}`;
      push(current, 'refresh');
    } catch (err) {
      void vscode.window.showWarningMessage(`SpecScribe refresh failed: ${String(err)}`);
    }
  }, 400);
  for (const pattern of ['_bmad-output/**/*.md', 'docs/adrs/**/*.md']) {
    const watcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(folder, pattern));
    watcher.onDidChange(refresh);
    watcher.onDidCreate(refresh);
    watcher.onDidDelete(refresh);
    context.subscriptions.push(watcher);
    p.onDidDispose(() => watcher.dispose());
  }
}

function createPanel(): vscode.WebviewPanel {
  const p = vscode.window.createWebviewPanel(
    'specscribeStatus',
    'SpecScribe Status',
    vscode.ViewColumn.One,
    {
      enableScripts: true,           // the one nonce'd bridge script (navigation + live-push)
      retainContextWhenHidden: true, // keep scroll/DOM state across tab hides — AC #3 "context remains coherent"
      localResourceRoots: [],        // nothing loads from disk: all CSS/SVG is inlined by the C# renderer
    },
  );
  p.onDidDispose(() => { panel = undefined; });
  return p;
}

/** Substitute the two — and only two — host-runtime values the C# shell left as placeholders. */
function withRuntime(webview: vscode.Webview, html: string): string {
  const nonce = crypto.randomBytes(16).toString('base64');
  return html.split('__CSP_SOURCE__').join(webview.cspSource).split('__NONCE__').join(nonce);
}

/** Spawn the SpecScribe tool's `webview` command and parse its stdout JSON — the extension↔core data path
 * ADR 0005 ratified. Resolution order: explicit setting → binary bundled with the extension (populated by
 * Story 16.5's packaging) → `specscribe` on PATH. A `.dll` value runs via `dotnet`. */
function runRenderer(context: vscode.ExtensionContext, cwd: string): Promise<WebviewPayload> {
  const configured = vscode.workspace.getConfiguration('specscribe').get<string>('toolPath')?.trim();
  const bundled = path.join(context.extensionPath, 'bin', process.platform === 'win32' ? 'specscribe.exe' : 'specscribe');

  const tool = configured || (fs.existsSync(bundled) ? bundled : 'specscribe');
  const [command, args] = tool.toLowerCase().endsWith('.dll')
    ? ['dotnet', [tool, 'webview']]
    : [tool, ['webview']];

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
    proc.stdout.on('data', (d) => (out += d.toString()));
    proc.stderr.on('data', (d) => (errText += d.toString()));
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
