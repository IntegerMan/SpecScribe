// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Story 6.3 VS Code Integration SPIKE — throwaway thin extension-host shim.
//
// This file is deliberately the WHOLE TypeScript surface. Its only responsibilities (the "irreducible shim"):
//   1. register one command,
//   2. open a WebviewPanel,
//   3. obtain C#-rendered HTML from the `specscribe-webview-spike` child process (spawn + stdout JSON),
//   4. inject the two host-runtime values (cspSource + nonce) the C# template left as placeholders,
//   5. relay file-change events into an in-place live-push (postMessage), no panel reset.
//
// It parses NO markdown, renders NO view, and holds NO project knowledge (AD-1/AD-2). Every byte of visible
// content is produced by the C# core. If this file grows a rendering brain, the architecture decision was wrong.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

import * as vscode from 'vscode';
import { spawn } from 'node:child_process';
import * as crypto from 'node:crypto';
import * as path from 'node:path';

type Surface = 'dashboard' | 'epics';

interface SpikePayload {
  siteTitle: string;
  dashboard: string;      // full webview document for the dashboard surface
  epics: string;          // full webview document for the epics surface
  dashboardBody: string;  // bare <main> body — used for in-place live-push / surface swap
  epicsBody: string;
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
    void vscode.window.showErrorMessage('SpecScribe spike: open a project folder first.');
    return;
  }
  const cwd = folder.uri.fsPath;

  let current: Surface = 'dashboard';
  let cache: SpikePayload | undefined;

  const p = panel ??= createPanel(context);

  // Surface toggle + webview "ready" handshake come back over postMessage.
  p.webview.onDidReceiveMessage(async (msg: { type?: string; surface?: Surface }) => {
    if (msg?.type === 'switch' && (msg.surface === 'dashboard' || msg.surface === 'epics')) {
      current = msg.surface;
      if (!cache) cache = await load(context, cwd);
      p.webview.postMessage({ type: 'update', surface: current, html: bodyFor(cache, current) });
    }
  });

  try {
    cache = await load(context, cwd);
  } catch (err) {
    p.webview.html = errorHtml(p.webview, String(err));
    return;
  }

  // First paint: set the full document ONCE (this is the only place a nonce is minted). Thereafter every
  // surface switch and every live-push is an in-place postMessage, so the panel never resets.
  p.webview.html = withRuntime(p.webview, cache.dashboard);

  // Live-push (AD-8): re-render on any source .md change, patched in place.
  const watcher = vscode.workspace.createFileSystemWatcher(
    new vscode.RelativePattern(folder, '_bmad-output/**/*.md'),
  );
  const refresh = debounce(async () => {
    try {
      cache = await load(context, cwd);
      p.webview.postMessage({ type: 'update', surface: current, html: bodyFor(cache, current) });
    } catch (err) {
      void vscode.window.showWarningMessage(`SpecScribe spike refresh failed: ${String(err)}`);
    }
  }, 400);
  watcher.onDidChange(refresh);
  watcher.onDidCreate(refresh);
  watcher.onDidDelete(refresh);
  context.subscriptions.push(watcher);
  p.onDidDispose(() => watcher.dispose());
}

function createPanel(context: vscode.ExtensionContext): vscode.WebviewPanel {
  const p = vscode.window.createWebviewPanel(
    'specscribeStatus',
    'SpecScribe Status (Spike)',
    vscode.ViewColumn.One,
    {
      enableScripts: true,
      retainContextWhenHidden: true, // keep scroll/DOM state across tab hides — required for AC #2 "no reset"
      localResourceRoots: [],        // nothing loads from disk: all CSS/SVG is inlined by the C# renderer
    },
  );
  p.onDidDispose(() => { panel = undefined; });
  return p;
}

function bodyFor(payload: SpikePayload, surface: Surface): string {
  return surface === 'epics' ? payload.epicsBody : payload.dashboardBody;
}

/** Substitute the two — and only two — host-runtime values the C# template left as placeholders. */
function withRuntime(webview: vscode.Webview, html: string): string {
  const nonce = crypto.randomBytes(16).toString('base64');
  return html.split('__CSP_SOURCE__').join(webview.cspSource).split('__NONCE__').join(nonce);
}

/** Spawn the C# renderer and parse its stdout JSON. THIS is the extension↔core data path under test. */
function load(context: vscode.ExtensionContext, cwd: string): Promise<SpikePayload> {
  // Resolution order (spike-only): explicit override → published self-contained exe alongside the extension →
  // `dotnet <renderer.dll>`. Story 6.4/16.5 decides the shipped form (single-file publish vs. `dotnet` on PATH).
  const override = process.env.SPECSCRIBE_SPIKE_RENDERER;
  const exe = path.join(context.extensionPath, 'renderer', process.platform === 'win32' ? 'specscribe-webview-spike.exe' : 'specscribe-webview-spike');
  const dll = path.join(context.extensionPath, 'renderer', 'specscribe-webview-spike.dll');

  const [command, args] = override
    ? (override.endsWith('.dll') ? ['dotnet', [override, cwd]] : [override, [cwd]])
    : ['dotnet', [dll, cwd]]; // simplest cross-platform default for the spike

  return new Promise<SpikePayload>((resolve, reject) => {
    const proc = spawn(command as string, args as string[], { cwd });
    let out = '';
    let errText = '';
    proc.stdout.on('data', (d) => (out += d.toString()));
    proc.stderr.on('data', (d) => (errText += d.toString()));
    proc.on('error', reject);
    proc.on('close', (code) => {
      if (code !== 0) return reject(new Error(`renderer exited ${code}: ${errText || '(no stderr)'}`));
      try {
        resolve(JSON.parse(out) as SpikePayload);
      } catch (e) {
        reject(new Error(`bad renderer JSON: ${String(e)}`));
      }
    });
  });
}

function errorHtml(webview: vscode.Webview, message: string): string {
  const esc = message.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c] as string));
  return `<!DOCTYPE html><html><head><meta charset="UTF-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline';"></head><body style="font:14px system-ui;padding:1.5rem"><h2>SpecScribe spike could not render</h2><pre style="white-space:pre-wrap;color:#c33">${esc}</pre><p>Is the .NET renderer built? Try <code>dotnet build spike/vscode/renderer</code>.</p></body></html>`;
}

function debounce<T extends (...args: never[]) => void>(fn: T, ms: number): T {
  let timer: NodeJS.Timeout | undefined;
  return ((...args: never[]) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  }) as T;
}

export function deactivate() { /* nothing to clean up beyond context.subscriptions */ }
