# ADR 0005: VS Code Webview Runtime — Core↔Extension Seam and Packaging

**Status:** Accepted
**Date:** 2026-07-10
**Deciders:** Matt Eland
**Amended by:** [ADR 0006 — Delivery Architecture & Distribution](0006-delivery-architecture-and-distribution.md) (re-affirms this ADR's C#-render + self-contained-binary core; adds npx distribution + an optional JSON+SPA delivery form)

## Context

Epic 6 (FR13) commits SpecScribe to a read-only, live-updating VS Code webview that reuses the shared
parsing/projection/rendering core rather than reimplementing it. Story 6.1 shipped the delivery seam
(`PageView` / `IRenderAdapter` / `HtmlRenderAdapter` / `RenderParity` / `HostRenderException`) and Story 6.2
decomposed the dashboard and epics bodies into host-neutral **section view models**
(`DashboardView` / `EpicsIndexView` and friends). What remained unproven — and what this ADR settles — is the
**boundary between the extension host and the C# core**: how the webview obtains its content, how live updates are
pushed, what survives a webview Content-Security-Policy, and how the whole thing ships so it "just works" on
install.

The owner's north star is explicit: users **install the extension and it just works**, with **as little
TypeScript as possible**, and without any answer that implies a core-language rewrite (rendering must stay in C#).
The epic's Story 6.4 AC #1, as literally written, says the core exposes a **JSON view-model export the TS webview
renders** — which would push rendering logic into TypeScript, against that north star. This ADR was preceded by a
hands-on spike (Story 6.3, branch `spike/vscode-6-3`, quarantined under `spike/`) built specifically to prove or
disprove an alternative — a **thin TS shim + a C# renderer** — and to ratify or reject that AC #1 reinterpretation
with evidence rather than argument.

### Platform constraint (accepted)

A VS Code extension **cannot be zero-TypeScript**: the extension host runs in VS Code's Node.js and requires a
`package.json` manifest plus a JS/TS entry point. There is no C# entry point into the extension host. "Pure C#
webview" is therefore realized as **thin TS shim + all rendering in C#**, not zero TS. The spike's job was to
confirm that shim can stay thin and dumb.

### What the spike built and measured (evidence base)

- A throwaway C# renderer (`spike/vscode/renderer`, `specscribe-webview-spike`) that reuses the **exact** ingest
  and view-model path the HTML surface uses — `BmadArtifactAdapter.Ingest` → `DashboardViewBuilder.Build` /
  `EpicsViewBuilder.BuildIndex` → `HtmlRenderAdapter.RenderDashboardBody` / `RenderEpicsIndexBody` — and wraps the
  resulting section bodies in a webview-safe document. It **scrapes no generated `.html`** and the extension
  **re-parses no `.md`** (AD-1/AD-2). Zero edits to `src/SpecScribe`.
- A ~180-line TS shim (`spike/vscode/src/extension.ts`): register command → open `WebviewPanel` → `spawn` the
  renderer → parse stdout JSON → inject two host-runtime values → live-push on file change.
- Run against this repo (16 epics, 79 markdown files) the renderer produced a **306 KB** dashboard document and a
  **237 KB** epics document, containing **107** and **18** inline `<svg>` charts respectively. (These figures are
  from a deliberately **reduced** input set — ADRs and coverage were omitted to keep the spike small — so the
  runtime's full-fidelity output will be somewhat larger; the CSP-survival conclusions below are unaffected, since
  SVG/style behaviour is identical regardless of input completeness.)

Toolchain confirmed live during the spike (this repo had no prior VS Code work): `esbuild` 0.24.2 (bundler),
TypeScript 5.9.3, `@types/vscode` 1.125.0 (`engines.vscode ^1.90.0`), `@vscode/vsce` for VSIX packaging, .NET SDK
10.0.301.

## Decision

### 1. Data path — **C# renders the webview HTML** (epic 6.4 AC #1 reinterpretation is **RATIFIED**)

The core produces **finished, webview-ready HTML** for the dashboard and epics surfaces from the shared section
view models; the extension sets it as `webview.html` verbatim (after a trivial placeholder substitution, below).
We **reject** the "JSON export that TypeScript renders" form of the epic's Story 6.4 AC #1. Same view models,
different delivery boundary: rendering stays 100% in C#, the shim carries no rendering logic. The spike proved the
shim needs to inject exactly **two** opaque host-runtime strings the C# template leaves as placeholders —
`__CSP_SOURCE__` (the webview's `cspSource`) and `__NONCE__` (a per-load script nonce) — and nothing else. That
two-value seam is the concrete evidence the shim can stay dumb.

Delivery contract: the webview surface is realized as a **second `IRenderAdapter` — a `WebviewRenderAdapter`** —
the exact extension point Story 6.1 designed for, mirroring `HtmlRenderAdapter`. It renders Story 6.2's section
view models to webview-safe HTML. It is **not** a JSON export. (A JSON export may still be added later if a
non-webview consumer needs raw data, but it is not the webview's delivery mechanism and is out of scope for the
runtime.)

### 2. Invocation / packaging — spawn the .NET tool as a child process; ship self-contained

The extension obtains content by **spawning the SpecScribe .NET tool as a child process and reading HTML/JSON from
stdout**. The **C# side** of this path — ingest → render → JSON on stdout — is proven headlessly (`dotnet run`);
the **extension-host side** (`spawn`, `JSON.parse`, injecting the two placeholders into a live `webview.html`) is
exercised only in the same manual `F5` run as the pixel paint (see "Not yet proven"), not in the headless
environment. Measured spawn-plus-full-render latency against this repo (C# side): **~1.8–2.0 s warm, ~3.5 s cold**
— dominated by ingest + the `git` subprocess calls, not .NET startup.

To honor "just works on install", the VSIX **bundles a self-contained single-file publish** of the tool
(`dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true`), so no separately-installed .NET runtime is
required. Measured cost: **~73 MB per runtime identifier (RID)** self-contained, vs **~3.5 MB** framework-dependent
(which would require the user to have the .NET 10 runtime installed). We accept the self-contained size for
zero-prerequisite install; Epic 16.5 owns the per-RID packaging matrix and may offer a framework-dependent variant
as a smaller opt-in. This same self-contained tool is what Epic 16.3's "single-command install-and-run CLI for CI"
ships — nothing in this seam blocks it.

### 3. Live-push transport (AD-8) — extension-host `FileSystemWatcher` → re-render → in-place `postMessage`

The spike used a VS Code `workspace.createFileSystemWatcher('_bmad-output/**/*.md')`, debounced (400 ms), to
re-spawn the renderer and push the fresh **section body** to the webview via `postMessage`; a small nonce'd bridge
script swaps `#specscribe-surface.innerHTML` **in place** with `retainContextWhenHidden: true`, so the panel never
resets (AC #2). We choose the **extension-host watcher** over bridging the C# `FileWatcherService` to `postMessage`:
it keeps the C# side a stateless one-shot renderer (spawn → render → exit), avoids a long-lived C# process and a
second IPC channel, and reuses VS Code's own file events. The runtime (Story 6.4) should scope the re-render like
`SiteGenerator.RegenerateEpics` does (re-ingest only what changed) to bring the ~1.8 s full re-render toward
sub-second; the transport itself is settled.

### 4. CSP + host-theme boundary (AD-7)

Measured against the spike's real output:

- **Inline SVG charts survive unchanged** — 107 (dashboard) + 18 (epics) `<svg>` elements inject directly, no
  external `src`, no external references of any kind (no `<img>`, `<link>`, `src=`, or `http(s)://` in the body).
  Because the body needs no remote origins, `default-src 'none'` with a **tight** `img-src`/`font-src` (`data:`
  only, no remote hosts) is viable — and the runtime (Story 6.4) seals exactly that. Note the spike's throwaway
  shell shipped a **looser** `img-src __CSP_SOURCE__ data: https:` (any-HTTPS) that the runtime tightened; the
  point proven here is that the *content* permits the tight policy, not that the spike artifact already ran it.
  This vindicates the pure-SVG charting decision.
- **The body carries no scripts of its own** — the only `<script>` in each document is the shim's own nonce'd
  bridge. The tooltip/copy enhancement JS (`assets/specscribe.js`) and the nav-toggle inline script live in the
  page *chrome/head*, which the webview shell replaces — so the progressive-enhancement policy holds: the body
  reaches the same information **without** the HTML surface's enhancement scripts. `script-src 'nonce-…'` (no
  `'unsafe-inline'`) is therefore sufficient and is the security-critical lock we keep strict.
- **Inline `style="…"` attributes require `style-src 'unsafe-inline'`** — the render emits **126** inline style
  attributes on the dashboard (e.g. `style="--col:N"` driving the heatmap's decorative column-stagger animation).
  A nonce on `style-src` would *not* cover attribute styles, and a nonce makes `'unsafe-inline'` be ignored. We
  therefore use `style-src 'unsafe-inline' <cspSource>` (no style nonce) while keeping `script-src` nonce-locked.
  Styles carry no execution risk, so this is an accepted, conventional VS Code webview posture.
- **Asset `?v=` version tokens are a non-issue** for this delivery form: they only appear on the stylesheet/script
  `<link>`/`<script src>` tags in the page head, which the webview shell replaces by inlining the CSS. No `?v=`
  token appears in the rendered body.
- **Mermaid is the one CSP casualty.** The epics "Suggested Build Order" roadmap renders as `<pre class="mermaid">`
  and needs the Mermaid script to paint. With no such script under CSP it degrades to readable preformatted text.
  Story 6.4/6.5 decides whether to bundle Mermaid with a nonce, render the diagram server-side, or accept the
  text fallback.
- **Theming** stays the Story 6.5 concern; the spike only *measured* the boundary. The site CSS is inlined
  as-authored (dark-first brand tokens). Host-aware theming — mapping container/chrome to VS Code CSS variables
  (`--vscode-*`) while keeping SpecScribe semantic accents product-owned (per ADR 0004) — is **not** done here.

### 5. Interaction parity (AD-8)

Drill/breadcrumb/status semantics carry as **data and links** in the rendered body, so the webview reaches the
same information without enhancement scripts. The `RenderParity` harness remains the eventual gate for the
`WebviewRenderAdapter`; the spike surfaced **no** case requiring a `HostRenderException` for these two surfaces.

## Consequences

**Positive**

- Rendering stays entirely in C# (the owner's north star); the TS shim is ~180 lines, **3.4 KB minified** — a
  probe, not a product. No core-language rewrite is implied.
- The webview plugs into the Story 6.1 `IRenderAdapter` seam as designed — additive, not a rewrite (NFR4).
- Pure-SVG charts and the "no information in enhancement JS" policy pay off directly: the body needs no script and
  no remote origins, so the runtime can seal a strict CSP (Story 6.4 does — nonce-locked `script-src`, `data:`-only
  `img-src`).
- Zero-prerequisite install is achievable via a bundled self-contained tool; the same artifact serves Epic 16.3.

**Negative / trade-offs**

- Self-contained bundling costs ~73 MB per RID in the VSIX; Epic 16.5 must manage the per-RID matrix (or offer a
  framework-dependent opt-in).
- A per-change full re-ingest is ~1.8 s; Story 6.4 must add scoped re-render to feel live on large repos.
- `style-src 'unsafe-inline'` is required for attribute styles — an accepted relaxation (styles only; scripts stay
  nonce-locked).
- Mermaid diagrams need an explicit decision in Story 6.4/6.5 (bundle-with-nonce, server-render, or text
  fallback).
- The extension spawns a process per (re)render; a future optimization could keep a warm renderer, at the cost of
  a longer-lived process and a second IPC channel we deliberately avoided for the spike.

**Not yet proven (single manual-verification gap)**

- Headlessly validated: the **C# renderer output** (JSON via `dotnet run`), the **CSP policy string**, the
  **shim compile/bundle** (`tsc --noEmit` + `esbuild`), and **packaging sizes/latency**. What a headless
  environment cannot exercise is the **extension-host runtime path**: the shim actually `spawn`-ing the child,
  `JSON.parse`-ing its stdout, injecting `webview.cspSource`/nonce, and the **pixel paint + live in-editor refresh**
  inside VS Code's Electron webview — these need the VS Code host and share one manual `F5` verification. (The
  spike's default spawn path also isn't wired for that run — the built renderer isn't copied under
  `<extensionPath>/renderer/`, so the `F5` must set `SPECSCRIBE_SPIKE_RENDERER`; Story 6.4 owns the real wiring.)
  Story 6.4 must open the panel in a real VS Code once (`F5` on the spike, or the first runtime build) to confirm
  spawn+consume + paint + in-place update before closing the loop.

## References

- Story 6.3 (this spike): `_bmad-output/implementation-artifacts/6-3-vs-code-integration-spike.md`; spike code on
  branch `spike/vscode-6-3` under `spike/vscode/` (`renderer/` C#, `src/extension.ts` shim, `spike/README.md`).
- [ADR 0002 — Shared Rendering Core and Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md)
- [ADR 0003 — Directory-Scoped Settings and Read-Only Helpers](0003-directory-scoped-settings-and-read-only-helpers.md)
- [ADR 0004 — Cross-Surface Interaction and Theme Contract](0004-cross-surface-interaction-and-theme-contract.md)
- [SpecScribe Architecture Spine](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1/AD-2, AD-6, AD-7/AD-8, seed/no-split.
- [Rendering Architecture](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md) — Delivery Adapter Layer, `IRenderAdapter`, webview-parity policy, Evolution Sequence step 4.
- Story 6.1 (delivery seam) and Story 6.2 (section view models); downstream: Story 6.4 (webview runtime), Story 6.5 (host theming), Epic 16.3/16.5 (CLI + extension packaging).
