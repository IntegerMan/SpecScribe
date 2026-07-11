---
baseline_commit: b58d78740621a64f27ec7fc27d47e6d218ff7c06
---

# Story 6.3: VS Code Integration Spike — Webview Feasibility & Core↔Extension Seam Decision

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the SpecScribe maintainer,
I want the greenfield VS Code surface **de-risked by a hands-on spike** that proves whether a *thin TypeScript host shim + a C# renderer* can deliver a read-only, live-updating dashboard/epics webview that "just works" on install — and that **records the core↔extension seam decision as an ADR**,
so that the eventual webview **runtime** story starts from an empirically-validated architecture (not guesses) and we avoid stumbling into an unplanned core-language rewrite.

## Scope Decision — READ FIRST (this story was redirected at create-story; confirm or veto before dev)

**This story was originally seated at 6.4 as the webview RUNTIME** (split out of Story 6.2 on 2026-07-10, carrying former 6.2 AC #2 + #3 — the in-editor webview UI + live host-push — with a JSON view-model export as its AC #1). **At create-story (2026-07-10) the owner (Matthew-Hope) redirected it to a SPIKE and authorized a clean renumber**, so it now sits at **6.3** (the spike) and the webview **runtime reclaims 6.4** (see Dependencies & Sequencing for the final numbering). Two decisions were captured live:

1. **Keep the eventual runtime as ONE story (do NOT split the export out).** The JSON/HTML export + webview + live-push are the one coherent "live in-editor surface" deliverable. **But that runtime is now Story 6.4 (the webview runtime — reclaiming the 6.4 slot and slug), to be seated after this spike.** *This* **Story 6.3** is the feasibility spike that seats it. (This mirrors the project's spike-led pattern for greenfield surfaces: Epics 11–15 each open with an integration spike; memory: [[epic-4-adapter-contract-scope]].)
2. **The architecture to VALIDATE (not yet commit to):** an *irreducible* thin TS extension-host shim (~150 lines: register command → open `WebviewPanel` → obtain content from the C# tool → relay live-push) plus a **C# renderer** (a second `IRenderAdapter` — a `WebviewRenderAdapter` — rendering Story 6.2's dashboard/epics section view models to webview-safe HTML). Goal: **minimize TypeScript, keep all rendering/logic in C#, "install and just works."** The spike must **prove or disprove** this and **record the decision**.

**Platform constraint the owner accepted (state it plainly to the dev):** a VS Code extension **cannot be zero-TypeScript** — the extension *host* runs in VS Code's Node.js and needs a `package.json` manifest + a JS/TS entry point. There is no C# entry point into the extension host. **"Pure C# webview" is therefore realized as _thin TS shim + all rendering in C#_**, not zero TS. The spike's job is to confirm that shim can stay thin and dumb.

**This redirection reinterprets the epic's Story 6.4 AC #1** ("expose a **JSON** view-model export the TS webview renders") toward "**C# renders the webview HTML** from the shared view models." Same view models, different delivery boundary. **That reinterpretation is exactly what the spike's ADR must ratify or reject** — do not silently bake it in; the ADR is where it becomes (or doesn't become) the decision. epics.md still numbers host-theming as its Story 6.3 (impl renumbered theming to 6.5) and has no spike story — reconciling epics.md's Epic 6 numbering is a follow-up correct-course, not this story's edit.

**The one-line test for "is this in scope?":** if the change **proves feasibility**, **measures a real constraint** (CSP, theming, packaging, live-push, spawn latency), or **records the seam decision (the ADR)** → in. If it **builds the production runtime**, ships the real export contract, implements host theming, or lands extension code on `main` as product → **out** (that is Story 6.4 runtime / Story 6.5 host-theming / Epic 16).

**If you disagree with this spike framing, raise it before writing code, not at review** (Epic 3 retro action item: don't defer a defining decision to the dev and correct it later; memory: [[create-story-elicit-visual-intent]]).

## Dependencies & Sequencing (read before starting)

- **Story 6.2 (section view models) is a HARD dependency for the RUNTIME (Story 6.4) — and only a SOFT dependency for THIS spike.** As of create-story, [6.2](6-2-read-only-vs-code-dashboard-and-epics-experience.md) is `review` on the board — but **verify its `DashboardView`/`EpicsIndexView`/`EpicPageView`/`StoryPageView` records actually exist in `src/SpecScribe/` before relying on them** (its story file still showed unchecked tasks at create-story). If they are not present, the spike must **not** build a real export against non-existent view models — drive the prototype from whatever exists today (the rendered dashboard/epics body strings as a stand-in, or a tiny hand-built sample), and **document the exact 6.2 shapes the real C# renderer will need** so 6.2 can confirm them. Surface any gap back to 6.2 before Story 6.4 (runtime) starts.
- **Story 6.1 (delivery contract) is done-enough:** [6.1](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) is `review` and shipped `PageView` / `IRenderAdapter` / `HtmlRenderAdapter` / `RenderParity` / the empty `HostRenderException` registry. The prototype `WebviewRenderAdapter` is the **second concrete `IRenderAdapter`** those types were designed for — mirror `HtmlRenderAdapter`.
- **Downstream this spike unblocks / informs:** **Story 6.4** (the webview runtime — seated by this spike), **Story 6.5** (host theming — depends on the webview existing), Epic 16 Story 16.5 (extension VSIX packaging + Marketplace publish, `blocked on Epic 6`) and Story 16.3 (CLI packaging).
- **Final numbering (renumbered 2026-07-10, owner-authorized).** Epic 6 impl stories now run in dependency order: **6.1 contract → 6.2 section view models → 6.3 (this spike) → 6.4 webview runtime → 6.5 host theming.** Numeric order == dependency order, no inversions. (History: host-theming was briefly renumbered 6.3→6.5 to sort after the webview; this spike then took the vacated 6.3 slot, and the runtime kept 6.4.) The only residual drift is **epics.md**, which still numbers host-theming as its Story 6.3 and has no spike entry — reconcile via `correct-course`/`sprint-planning` when convenient; `sprint-status.yaml` is the operational truth.
- **The owner's "single-command install-and-run CLI for CI" goal is Epic 16's, not this spike's** (FR32/FR33; Story 16.3 CLI packaging, Story 16.5 extension publish). The spike only needs to confirm nothing in the chosen seam *blocks* it, and to feed the packaging decision into 16.5.

## Acceptance Criteria

The epic's Story 6.4 ACs (below, verbatim) are **reinterpreted as feasibility questions** for the spike; the production behaviors they describe move to Story 6.4 (the webview runtime).

**Epic source — [epics.md:909–927](../planning-artifacts/epics.md) (Story 6.4 AC #1–#3), verbatim:**

> 1. **Given** Story 6.2's section view models describe the dashboard and epics surfaces as host-neutral data **When** the webview needs that data **Then** the rendering core exposes a JSON view-model export of those section view models **And** the export carries the section data itself (not scraped HTML) with no dependence on the HTML surface's enhancement scripts.
> 2. **Given** the extension opens the status webview **When** project data is loaded **Then** dashboard and epics views display with the same core interaction-state semantics as HTML **And** in-editor navigation is responsive and readable.
> 3. **Given** source artifacts change while the webview is open **When** host updates are pushed **Then** visible status refreshes in place without full panel reset **And** drill/breadcrumb context remains coherent.

**THIS story's acceptance criteria (spike — the dev must satisfy these):**

1. **Feasibility proof (throwaway, isolated).** **Given** a minimal VS Code extension on an isolated spike branch, **When** its command opens a `WebviewPanel` for this repo as the sample project, **Then** the panel renders the **dashboard AND epics** surfaces with content **produced by the C# core** (rendered from view models — **not** scraped from the static site's `.html` files) **And** the proof demonstrates end-to-end that (a) the thin TS shim can obtain that content from the `specscribe` tool, (b) the two surfaces render acceptably inside a webview's Content-Security-Policy, and (c) inline-SVG charts + status semantics survive the webview. It need **not** be production-quality, cover every section, or be host-themed.

2. **Live-push feasibility.** **Given** the panel is open, **When** a source `.md` artifact is edited, **Then** the webview content updates **in place without a full panel reset** — proving the AD-8 host-push transport is achievable with the chosen seam (even if crude) — **And** the spike records which mechanism was used (bridging the C# `FileWatcherService`/`watch` events to `postMessage`, vs. an extension-host `FileSystemWatcher` + one-shot render).

3. **Seam decision recorded as an ADR (the primary durable deliverable).** **Given** the spike's hands-on findings, **When** it concludes, **Then** a new **`docs/adrs/0005-*.md`** (format per [ADR 0004](../../docs/adrs/0004-cross-surface-interaction-and-theme-contract.md): Status/Date/Deciders/Context/Decision/Consequences/References) records and justifies: (a) the **extension↔core data path** — spawn the `specscribe` dotnet CLI as a child process vs. bundled single-file publish vs. other; **JSON payload vs. C#-rendered webview HTML** — and **explicitly ratifies or rejects the reinterpretation of epic 6.4 AC #1**; (b) the **live-push transport**; (c) the **CSP + host-theme constraints** (what of the static site's CSS / inline SVG / tooltip-copy JS survives, what the host-theming story (Story 6.5) must change); (d) the **packaging/distribution** implications for Epic 16.5 (VSIX via `@vscode/vsce`; how the .NET tool ships so it "just works" on install); (e) whether the delivery contract Story 6.2's view models bind to is a **`WebviewRenderAdapter` (2nd `IRenderAdapter`)** or a **JSON export**. **And** `docs/adrs/README.md`'s index is updated to list ADR 0005.

4. **Implementation story seated.** **Given** the ADR's decision, **When** the spike concludes, **Then** **Story 6.4** (the webview runtime — the epic's 6.4 runtime ACs, shaped by the decision) is **seated**, with its scope, the Story 6.2 hard-dependency, and the Story 6.5 host-theming / Epic 16.5 linkages made explicit **And** the **6.2 → 6.3 (spike) → 6.4 (runtime) → 6.5 (theming)** order is confirmed. (This AC is satisfied by a follow-up `create-story`/`correct-course` for Story 6.4; the spike's job is to make the runtime's scope **unambiguous** — at minimum, name it precisely in the ADR + Completion Notes.)

5. **No half-baked runtime on `main`.** **Given** a spike produces throwaway code, **When** it lands, **Then** **no production runtime/extension code is merged to `main` as product** — the spike branch stays unmerged, or only clearly-quarantined `spike/` artifacts land (excluded from the shipped tool's build/package) **And** the generated site stays byte-identical (the spike adds no product rendering to the shipped CLI) **And** read-only is honored (AD-6): no helper path writes source artifacts. The **only** artifacts intended to land on `main` are ADR 0005, the README index update, this story's completion record, and (optionally) a short `spike/README.md` pointing at the branch/findings.

## Tasks / Subtasks

- [x] **Task 1 — Stand up the throwaway extension shell** (AC: #1, #5)
  - [x] Work on an **isolated spike branch** `spike/vscode-6-3` (do NOT develop this on `main`; there is a background auto-committer on `main` that pushes edits straight to `main` — memory: [[worktree-edits-must-target-worktree-path]]). Record the `baseline_commit` (frontmatter) you branched from. *(Branched from HEAD `8ebca9e`; frontmatter `baseline_commit` preserved at `b58d787` per create-story — actual branch point noted in Debug Log.)*
  - [x] Scaffold a minimal VS Code extension in a **self-contained, quarantined** folder (`spike/vscode/`): `package.json` (`specscribe.openStatus` command, `engines.vscode ^1.90.0`), `tsconfig.json`, `esbuild.js`, and a **~180-line** `src/extension.ts` shim that registers the command and creates a `WebviewPanel`. Dumb probe: no rendering logic.
  - [x] Confirmed current VS Code toolchain live: TypeScript 5.9.3 + `esbuild` 0.24.2 bundling; `@types/vscode` 1.125.0; `@vscode/vsce` for VSIX. Not wired into `SpecScribe.csproj` (no `.sln`; `dotnet build src/SpecScribe` never sees `spike/`), the solution, or the site build.

- [x] **Task 2 — Get dashboard + epics content from the C# core into the panel** (AC: #1, #3)
  - [x] Prototyped the **recommended path (a) C#-renders-webview-HTML** deeply: `spike/vscode/renderer` reuses the SAME view-model path the HTML surface uses (`BmadArtifactAdapter.Ingest` → `DashboardViewBuilder.Build` / `EpicsViewBuilder.BuildIndex` → `HtmlRenderAdapter.RenderDashboardBody` / `RenderEpicsIndexBody`) and wraps the bodies in a webview-safe doc; the shim spawns it (child process + stdout JSON) and sets `webview.html`. Path **(b) JSON-rendered-by-TS** evaluated for comparison and **rejected in ADR 0005** (pushes rendering into TS, against the owner's "less TS" goal). No scraping of generated `.html`; no `.md` re-parse in the extension (AD-1/AD-2).
  - [x] **Story 6.2 view models exist on `main`** (`DashboardView.cs`, `EpicsView.cs` — verified before relying on them), so the prototype drove from the REAL section view models, not a hand-built stand-in. The exact shapes the runtime needs are the five 6.2 views (`DashboardView` / `EpicsIndexView` / `EpicPageView` / `StoryPageView` / `StoryPlaceholderView`), recorded in ADR 0005 §1.
  - [x] **Captured the CSP reality** against real output (306 KB dashboard, 237 KB epics): inline SVG charts survive (107 + 18 `<svg>`, no external `src`); the body carries **no scripts** (enhancement JS lives in chrome/head the shell replaces) so `script-src 'nonce-…'` suffices; **126 inline `style="--col:N"` attributes** require `style-src 'unsafe-inline'`; no `?v=` tokens in the body; **Mermaid** roadmap is the one casualty (`<pre class="mermaid">` needs a script → degrades to text). Full findings in ADR 0005 §4.

- [x] **Task 3 — Prove live-push** (AC: #2)
  - [x] Implemented an **in-place** update seam: extension-host `FileSystemWatcher('_bmad-output/**/*.md')`, debounced 400 ms, re-spawns the renderer and `postMessage`s the fresh section body to a nonce'd bridge that swaps `#specscribe-surface.innerHTML` with `retainContextWhenHidden: true` — no panel re-create. Chose the **extension-host watcher over bridging the C# `FileWatcherService`** (keeps C# a stateless one-shot renderer, avoids a long-lived process + second IPC channel) — rationale in ADR 0005 §3. *(The actual in-editor refresh paint is the single manual-verify step — see Completion Notes / ADR 0005.)*

- [x] **Task 4 — Measure packaging / "just works"** (AC: #3d)
  - [x] Measured (numbers, not vibes): self-contained single-file publish **~73 MB / RID** (zero prereqs) vs framework-dependent portable **~3.5 MB** (needs .NET 10 runtime); spawn-plus-full-render latency **~1.8–2.0 s warm / ~3.5 s cold** (dominated by ingest + `git` subprocess, not .NET startup); extension bundle **3.4 KB minified**. Decision: **bundle the self-contained tool** for zero-prereq install; Epic 16.5 owns the per-RID matrix. ADR 0005 §2.
  - [x] Confirmed the spawn seam does **not** block Epic 16.3's single-command install-and-run CLI (the same self-contained tool serves it); linkage noted, 16.3 not implemented here.

- [x] **Task 5 — Write the ADR + seat the implementation story** (AC: #3, #4)
  - [x] Authored **`docs/adrs/0005-vs-code-webview-runtime-and-packaging.md`** (ADR 0004 format; **Accepted**, Matt Eland) recording the seam across data path, live-push transport, CSP/theming, packaging — and the **explicit RATIFICATION of the C#-rendered-webview-HTML reinterpretation of epic 6.4 AC #1** (JSON export rejected).
  - [x] Added ADR 0005 to [docs/adrs/README.md](../../docs/adrs/README.md).
  - [x] **Story 6.4 (webview runtime) is seated** — its file [6-4-…-webview-runtime](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) already exists context-complete with a HARD GATE on ADR 0005; the ADR now clears that gate and selects its fork (build `WebviewRenderAdapter`, not the JSON export). 6.2 → 6.3 → 6.4 → 6.5 order confirmed; hard-dependency on 6.2 + linkages to 6.5 / 16.5 recorded in the ADR.

- [x] **Task 6 — Quarantine & land only the decision** (AC: #5)
  - [x] No production runtime on `main`: all spike code is under the quarantined `spike/` folder (excluded from the shipped tool — no `.sln`, not in `SpecScribe.csproj`, not in the site pipeline), plus a `spike/.gitignore` for build outputs. The durable artifacts are ADR 0005 + README index + this story record + `spike/README.md`. The shipped `specscribe` CLI gains **no** new product rendering path.
  - [x] **Zero `src/SpecScribe/*.cs` files were touched** (`git status src/` is empty), so the generated site is byte-identical by construction — the golden gate is a genuine no-op (memory: [[golden-diff-normalization-gotchas]], [[generate-output-dir-is-specscribeoutput]]). Read-only honored (AD-6): no helper path writes source artifacts.

### Review Findings

_Code review 2026-07-11 (adversarial + edge-case + acceptance layers). All five ACs substantively MET; the central ADR conclusions (C# renders webview HTML behind `IRenderAdapter`; inline SVG survives; nonce-locked `script-src` sufficient) are evidence-backed. The items below are ADR-accuracy gaps and spike-code defects to carry into the Story 6.4 runtime — none block the spike's decision. The one honest remainder is the disclosed manual pixel-paint/`F5` gap._

- [x] [Review][Decision→Resolved in 6.4] ADR §4 overstated CSP `img-src` tightness — the spike (and the first 6.4 cut) shipped `img-src __CSP_SOURCE__ data: https:` (permits **any** HTTPS image / call-home) while ADR §4 claimed a "tight"/"clean" CSP. **Resolved 2026-07-11 in Story 6.4's review:** the runtime `WebviewRenderAdapter` CSP was tightened to `img-src __CSP_SOURCE__ data:` (no remote origins), ADR §4 corrected to state the sealed policy, and a test pins it (`Render_EmitsTheCspLockedShellWithTheTwoHostPlaceholders`; 701 green).
- [x] [Review][Decision→Dismissed] ADR §2 packaging claim ("proved this path end-to-end", "spawn the SpecScribe .NET tool") read ahead of the spike's throwaway reality. **Dismissed — reality caught up:** Story 6.4 built the real `specscribe webview` stdout command (`Commands.cs`) and the extension now spawns the real tool (`toolPath` → bundled `bin/specscribe` → PATH; `.dll`→`dotnet`) with read-only temp-scratch output. ADR §2 is now accurate; no amendment needed. (Self-contained bundling itself remains Story 16.5, as the ADR states.)
- [x] [Review][Defer] Live-push watcher glob is anchored to the workspace folder while the renderer resolves the repo root by walking **up** [spike/vscode/src/extension.ts:71] — deferred to Story 6.4. When the opened folder is a subdirectory of the repo, first paint works (renderer walks up to find `_bmad-output`) but `RelativePattern(folder, '_bmad-output/**/*.md')` never matches, so the watcher never fires and live-push is silently dead — invalidating the AD-8 evidence for that layout.
- [x] [Review][Defer] Overlapping debounced re-renders race [spike/vscode/src/extension.ts:74-77] — deferred to Story 6.4. The 400 ms debounce only coalesces sub-400 ms bursts; two saves within one ~1.8 s render window spawn concurrent renders with no in-flight/generation guard, so a stale render can overwrite fresher content. (ADR §3 already flags scoped re-render as 6.4 work — add a generation token there.)
- [x] [Review][Defer] Re-invoking `specscribe.openStatus` while the panel is open leaks watchers/handlers and resets the panel [spike/vscode/src/extension.ts:48-86] — deferred to Story 6.4. `panel ??= createPanel` reuses the panel but the rest of `openStatus` re-runs: another `onDidReceiveMessage`, another `createFileSystemWatcher`, and a fresh `webview.html =` (no `reveal()`, no dedupe). N invocations ⇒ N watchers, N-fold live-push, and a panel reset (contra ADR §3 "never resets"). Register watcher/handler once (in `createPanel`) and early-return+`reveal()` on reuse.

### Review Findings — Parallel Adversarial Review (2026-07-11, bmad-code-review)

_Second review pass: three parallel layers (Blind Hunter adversarial + Edge Case Hunter + Acceptance Auditor, all Opus), diff scoped to the merged spike (`1c9270b`). **The core decision in ADR 0005 is SOUND and safe to keep** — all 5 spike ACs substantively met (AC#1 partial only on the honestly-disclosed manual-`F5` pixel-paint gap; quarantine/read-only/byte-identity all clean). One cross-layer false positive was caught and rejected in triage. The surviving items are (a) durable-ADR accuracy overstatements worth softening on `main`, and (b) throwaway spike-code defects that carry to the Story 6.4 runtime (several re-confirm the prior pass's deferrals)._

**Patch — durable ADR 0005 accuracy (on `main`; decision unaffected):**
- [x] [Review][Patch] ADR §4 overstates CSP as "tight"/"clean" while the spike shell ships a permissive `img-src __CSP_SOURCE__ data: https:` [docs/adrs/0005-vs-code-webview-runtime-and-packaging.md:100,113,135] — **APPLIED 2026-07-11:** §4 now states the spike ran the looser any-HTTPS `img-src` and that the *content* (no remote origins) permits the tight policy the runtime seals; Consequences reworded from "the CSP story is clean" to the strict-policy-the-runtime-seals framing.
- [x] [Review][Patch] ADR §2 + "Not yet proven" overstate "spawn → stdout → `webview.html` … proven end-to-end" [docs/adrs/0005-vs-code-webview-runtime-and-packaging.md:71,150-156] — **APPLIED 2026-07-11:** §2 now scopes the headless proof to the C# side and marks the extension-host spawn/parse/inject path as sharing the manual-`F5` gap; "Not yet proven" rewritten accordingly (and now notes the unwired default spawn path / `SPECSCRIBE_SPIKE_RENDERER` requirement).
- [x] [Review][Patch] ADR "evidence base" figures (306/237 KB, 107/18 `<svg>`) come from a deliberately reduced render (`adrs: Array.Empty`, `coverage: null`, `hasAdrs:false`) [docs/adrs/0005-…:43-49; spike/vscode/renderer/Program.cs:61-62] — **APPLIED 2026-07-11:** added a "reduced input set" caveat to the evidence-base bullet noting the runtime output will be larger and the CSP conclusions are unaffected.

**Defer — throwaway spike code, carry to Story 6.4 runtime:**
- [x] [Review][Defer] Default renderer spawn path is never populated + dead `exe` fallback [spike/vscode/src/extension.ts:120-125] — deferred to 6.4. Default spawns `dotnet <extensionPath>/renderer/specscribe-webview-spike.dll`, but no build step places a dll there (`npm run build` bundles only `dist/extension.js`; the README builds the renderer to `spike-out/`). The computed `exe` path (line 120) is never referenced in command selection. **Consequence: the one remaining manual `F5` verification needs `SPECSCRIBE_SPIKE_RENDERER` set** — the "just works" default resolves nothing. Whoever closes the paint gap must know this.
- [x] [Review][Defer] Surface-switch `await load()` has no error handling, asymmetric with `refresh`'s try/catch [spike/vscode/src/extension.ts:52-58] — deferred to 6.4. A renderer failure on toggle produces an unhandled rejection and no user feedback.
- [x] [Review][Defer] `withRuntime` does a whole-document `split/join` of `__NONCE__`/`__CSP_SOURCE__` [spike/vscode/src/extension.ts:110-113] — deferred to 6.4. Rendered content containing the literal placeholders would be silently rewritten; undercuts the "exactly two opaque strings" framing (negligible probability today).
- [x] [Review][Defer] Live-push watcher scoped to `*.md` only [spike/vscode/src/extension.ts:73] — deferred to 6.4. Edits to `sprint-status.yaml` and other non-`.md` inputs that feed the dashboard never trigger a live refresh (AC#2 literally names `.md`, so the spike AC still passes).
- [x] [Review][Defer] No spawn timeout / kill [spike/vscode/src/extension.ts:127-142] — deferred to 6.4. A hung renderer or `git` subprocess leaves the webview blank indefinitely; debounced refreshes can pile up.
- [x] [Review][Defer] Multi-root workspace uses `workspaceFolders[0]` unconditionally [spike/vscode/src/extension.ts:39] — deferred to 6.4. If the SpecScribe project isn't the first root, it renders/watches the wrong folder. (Same root cause as the pre-logged watcher-glob finding.)
- [x] [Review][Defer] Renderer arg/enumeration edges [spike/vscode/renderer/Program.cs:27,33-38] — deferred to 6.4. `--out <dir>`'s value is also matched by `FirstOrDefault(a => !a.StartsWith("--"))`, so `renderer --out X` (no project dir) mis-reads `X` as the project dir (dead via the shim, which always passes `[dll, cwd]`); a missing `_bmad-output` silently renders an empty dashboard rather than signalling not-a-SpecScribe folder; an unreadable subdir aborts the whole enumerate.
- [x] [Review][Defer] Re-confirms prior pass: watcher-glob anchored to workspace folder while renderer walks up, overlapping-debounce race (no generation token), and re-invoke leaks/panel-reset all reproduced in the current code — see the four items above in the prior Review Findings block. Fix all together in 6.4 (register watcher/handler once; add a generation/in-flight guard; derive the watched path from the resolved source root).

**Dismissed (noise / false positive):**
- UTF-8 stdout corruption (raised by BOTH hunters — chunk-boundary and Windows-codepage variants): **FALSE POSITIVE.** `Program.cs:87` serializes via `System.Text.Json` with default options, whose `JavaScriptEncoder.Default` escapes every non-ASCII codepoint to `\uXXXX`, so the stdout payload is pure ASCII — no multibyte sequences exist to split or mis-decode; `JSON.parse` reconstructs the characters faithfully.
- Shim "~180 line" / "3.4 KB minified" figures slightly off (158 lines incl. header; minified size unverifiable without committed `dist/`) — nitpick.
- AC#1 "in-webview paint measured statically, not demonstrated" — not a defect; it is the accepted, disclosed manual-`F5` gap already recorded in ADR 0005, `spike/README.md`, and the prior review findings.

## Dev Notes

### This is a SPIKE — decision-first, timeboxed, throwaway
The deliverable is **knowledge + an ADR + a seated Story 6.4 (webview runtime)**, not a shippable webview. Build only as much extension as it takes to make the seam decision **defensible**, then stop. Resist gold-plating (full section coverage, theming, polish) — that is Story 6.4 runtime / Story 6.5 theming. Spike-led de-risking is the project's established pattern for greenfield surfaces (Epics 11–15 each open with an integration spike; memory: [[epic-4-adapter-contract-scope]], [[story-1-4-split-into-1-4-1-5-1-6]]).

### Why a spike, and the "pure C# / just works" north star (the owner's framing)
The owner wants users to **install the extension and have it just work**, with **as little TypeScript as possible**, and is wary that a "we must render in TS" answer could imply a core-language rewrite (its own epic). The platform reality: a VS Code extension needs a **thin, irreducible** TS host shim, but the **webview panel content is just HTML/CSS/SVG that C# can produce**. So the target architecture to validate is: **thin dumb TS shim + a C# `WebviewRenderAdapter` (the 2nd `IRenderAdapter` [6.1](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) was built for) that renders the [6.2](6-2-read-only-vs-code-dashboard-and-epics-experience.md) section view models to webview-safe HTML.** The spike proves whether that holds under real CSP, theming, live-push, and packaging constraints — and whether the shim can truly stay thin. Rendering-architecture already anticipates this: "Webview adapter: serves view models and host integration glue for VS Code" ([rendering-architecture.md:26–28,47–50](../specs/spec-specscribe/rendering-architecture.md)), and the Evolution Sequence step 4 is literally "Add webview adapter once relevance gates are met" ([rendering-architecture.md:111–117](../specs/spec-specscribe/rendering-architecture.md)).

### The seam decision the ADR must settle (this is the point of the story)
Five coupled choices; the spike gathers evidence, the ADR commits:
1. **Data path:** C#-rendered webview HTML (recommended, minimizes TS) vs. JSON-that-TS-renders (epic AC #1 as literally written). Ratify or reject the epic AC #1 reinterpretation explicitly.
2. **Invocation/packaging:** spawn `specscribe` child process (reuse the Spectre CLI in [Program.cs](../../src/SpecScribe/Program.cs)) vs. bundled single-file publish vs. PATH dependency — driven by the "just works on install" goal and Epic 16.5.
3. **Live-push transport (AD-8):** bridge the C# [FileWatcherService](../../src/SpecScribe/FileWatcherService.cs) events → `postMessage` vs. an extension-host `FileSystemWatcher` + one-shot render.
4. **CSP + theming boundary (AD-7):** what survives the webview CSP unchanged, what the host-theming story (Story 6.5) must remap to VS Code host variables. Charts (pure SVG) survive; enhancement JS does not without nonces (and per policy must not carry information anyway).
5. **Interaction parity (AD-8):** confirm the drill/breadcrumb semantics carry as *data/links*, so the webview reaches the same info without the HTML surface's enhancement scripts — the [RenderParity](../../src/SpecScribe/RenderParity.cs) harness is the eventual gate; note whether the webview would register any `HostRenderException`.

### Architecture invariants that BOUND the spike (from the spine)
- **AD-1 / AD-2** ([ARCHITECTURE-SPINE.md:34–48](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): one shared core; adapters translate view models to host delivery **without reinterpreting source artifacts**. A `WebviewRenderAdapter` re-parses **nothing** — it renders view models, exactly like `HtmlRenderAdapter`. Do NOT let the extension re-parse `.md`.
- **AD-6 / Read-only** ([ARCHITECTURE-SPINE.md:74–80](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md)): the webview is **read-only**; no helper writes source artifacts. The spike must not prototype any write path.
- **AD-7 / AD-8** ([ARCHITECTURE-SPINE.md:82–96](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [ADR 0004](../../docs/adrs/0004-cross-surface-interaction-and-theme-contract.md)): shared presentation tokens + host-owned chrome (theming is the **Story 6.5 host-theming story**, not here — the spike only *measures* the boundary); shared interaction shape + adapter-specific transport (live-push is the transport the spike proves).
- **No package/namespace split** (still seed-level, still forbidden — [ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): any throwaway C# lives in `namespace SpecScribe;` in the single [SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj). Do not stand up `SpecScribe.Delivery.Webview` — the sketch's package boundaries ([rendering-architecture.md:100–109](../specs/spec-specscribe/rendering-architecture.md)) are aspirational seeds, not this story.

### VS Code webview specifics to verify hands-on (spike checklist, not gospel)
- **Webview API:** `window.createWebviewPanel(viewType, title, column, { enableScripts, retainContextWhenHidden, localResourceRoots })`; set `panel.webview.html`; message both ways via `webview.postMessage` / `acquireVsCodeApi().postMessage` + `onDidReceiveMessage`.
- **CSP:** a strict `<meta http-equiv="Content-Security-Policy">` is expected; scripts need `nonce-…`; local assets load via `panel.webview.asWebviewUri(...)`. Inline SVG is fine; inline `<style>` needs `style-src` allowance/nonce.
- **Live-push:** prefer `retainContextWhenHidden: true` + targeted `postMessage` patches so the panel does not lose scroll/drill context on refresh (AC #2 "without full panel reset").
- **Packaging:** current tool is `@vscode/vsce` (produces a `.vsix`); `engines.vscode` pins the min host version; `esbuild` is the standard bundler. **Confirm exact current versions/flags during the spike** rather than trusting these notes — VS Code API + tooling move quickly and this story predates any extension in the repo.
- **.NET delivery:** `dotnet publish -c Release -r <rid> -p:PublishSingleFile=true --self-contained` yields a standalone `specscribe` the shim can spawn; weigh size vs. requiring an installed .NET runtime.

### Risk centers (where a spike goes wrong)
1. **Scope creep into a real build** — the single biggest trap. It is a probe: stop at "the decision is defensible + the ADR is written." Full section coverage, theming, and polish are Story 6.4 runtime / Story 6.5 theming.
2. **Building the real export against non-existent 6.2 view models** — 6.2 is not implemented; stand in and *document the needed shapes*, don't fabricate a contract.
3. **Landing half-baked runtime on `main`** — violates the coherent-`main` + read-only posture. Quarantine to `spike/` or keep the branch unmerged; only the ADR/decision lands.
4. **Under-answering the ADR** — the code is disposable; the decision is the deliverable. An ADR that hand-waves the data path / packaging / CSP has failed the story even if a demo worked.
5. **Missing the "just works" packaging reality** — measure single-file-publish size + spawn latency + install flow now; it is the crux of the owner's goal and Epic 16.5's blocker.
6. **Re-parsing in the extension** — the extension must never parse `.md` itself (AD-1/AD-2); all data comes from the C# core.

### Project Structure Notes
- **Single C# project:** [src/SpecScribe/SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj) (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). Any throwaway C# adapter goes here in `namespace SpecScribe;` **on the spike branch only** — no new project, no namespace split.
- **Extension (greenfield):** self-contained under a quarantined path (suggest `spike/vscode/`) with its own `package.json` / `tsconfig.json` / `esbuild` config — **not** part of the .NET solution or the site build. It exists to be thrown away.
- **ADR home:** [docs/adrs/](../../docs/adrs) — hand-authored, numbered by filename prefix, rendered into the live site (per [docs/adrs/README.md](../../docs/adrs/README.md)). ADR 0005 is the next number. Match the [ADR 0004](../../docs/adrs/0004-cross-surface-interaction-and-theme-contract.md) shape.
- **Output dir** for any generate verification is `SpecScribeOutput` (memory: [[generate-output-dir-is-specscribeoutput]]); never `--output docs/live`.
- **Develop on `spike/vscode-6-3`, not `main`** — background auto-committer caveat (memory: [[worktree-edits-must-target-worktree-path]]).

### Latest-tech reminders (verify live during the spike — this repo has no prior VS Code work)
- The extension packaging CLI is `@vscode/vsce` (the old `vsce` package was renamed); `npx @vscode/vsce package` builds the VSIX.
- `esbuild` is the current recommended extension bundler (faster than webpack for this size); a plain `tsc` build is also fine for a ~150-line shim.
- Webview security has tightened over time — assume a strict default CSP and `localResourceRoots` gating; test with `enableScripts: true` only where needed.
- Pin `engines.vscode` to a recent-but-not-bleeding-edge version and confirm the `@types/vscode` matches. Treat all specific APIs above as *to-confirm*, since the spike is the first VS Code code in the repo.

### References
- **Epic + story source:** [epics.md:815–927](../planning-artifacts/epics.md) — Epic 6 goal (FR13) + Story 6.4 ACs (the 3 quoted above; the 2026-07-10 split note at :893–903 records 6.4's origin from 6.2); [epics.md:48,74](../planning-artifacts/epics.md) — FR13 (read-only webview reusing shared parsing/projection) + FR33 (Marketplace publish, Epic 16.5); [epics.md:101,126](../planning-artifacts/epics.md) — the cross-surface interaction-state line + UX-DR14 (webview adaptation rules reuse core semantics, honor host theme).
- **Prior Epic 6 stories:** [6-1-shared-view-model-contract-for-html-and-webview-adapters.md](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) — the delivery contract (`PageView`/`IRenderAdapter`/`HtmlRenderAdapter`/`RenderParity`/`HostRenderException`) the webview adapter plugs into; copy its scope discipline. [6-2-read-only-vs-code-dashboard-and-epics-experience.md](6-2-read-only-vs-code-dashboard-and-epics-experience.md) — the section view models the runtime serializes/renders (the hard dependency; its "Follow-up" section already names Story 6.4 as the runtime home).
- **Architecture:** [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) AD-1/AD-2 (:34–48), AD-6 (:74–80), AD-7/AD-8 (:82–96), Seed/no-split (:98–101), Runtime Flow (:105–117, the HTML + webview adapters both consuming host-neutral view models). [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md) — Delivery Adapter Layer (:26–28), `IRenderAdapter` (:47–50), client-side enhancement + webview-parity policy (:84–92), read-only IDE helper pattern (:94–98), Evolution Sequence step 4 (:111–117).
- **ADRs:** [0002 shared rendering core + host-neutral view models](../../docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md), [0003 read-only helpers](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md), [0004 cross-surface interaction + theme contract](../../docs/adrs/0004-cross-surface-interaction-and-theme-contract.md) — the format + prior decisions ADR 0005 extends; [README.md](../../docs/adrs/README.md) — the index to update.
- **Source to reuse/mirror:** [HtmlRenderAdapter.cs](../../src/SpecScribe/HtmlRenderAdapter.cs) + [IRenderAdapter.cs](../../src/SpecScribe/IRenderAdapter.cs) + [PageView.cs](../../src/SpecScribe/PageView.cs) (the adapter pattern a `WebviewRenderAdapter` mirrors); [FileWatcherService.cs](../../src/SpecScribe/FileWatcherService.cs) (live-push source); [Program.cs](../../src/SpecScribe/Program.cs) (Spectre CLI the shim would spawn); [RenderParity.cs](../../src/SpecScribe/RenderParity.cs) + [HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs) (the parity gate for the runtime); [assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js) (the enhancement JS that must not carry information into the webview).
- **Downstream:** [epics.md:1817–1840](../planning-artifacts/epics.md) — Story 16.5 (extension VSIX + Marketplace, blocked on Epic 6); [epics.md:1778–1795](../planning-artifacts/epics.md) — Story 16.3 (CLI packaging, the owner's CI single-command goal).
- **Memory:** [[epic-4-adapter-contract-scope]] (spike-led greenfield pattern), [[charting-is-pure-svg-no-js]] (SVG survives the webview; enhancement JS is the exception), [[worktree-edits-must-target-worktree-path]] (develop off `main`), [[generate-output-dir-is-specscribeoutput]], [[golden-diff-normalization-gotchas]] (only if the core is touched), [[create-story-elicit-visual-intent]] (defining decisions belong up front — why this was redirected at create-story, not review).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8) — dev-story workflow.

### Debug Log References

- Spike branch `spike/vscode-6-3` created from HEAD `8ebca9e` (frontmatter `baseline_commit` preserved at `b58d787` per create-story convention — the workflow does not overwrite an existing baseline).
- Renderer build: `dotnet build spike/vscode/renderer` → succeeded, 0 warnings. Run against this repo: `79` md files, `16` epics ingested; wrote `dashboard.html` (306,726 B) + `epics.html` (237,238 B).
- CSP survival scan of rendered output: dashboard `107` inline `<svg>`, `1` `<script>` (the nonce'd bridge only), `126` inline `style="` attrs, `0` `?v=` tokens, `0` external refs; epics `18` `<svg>`, `5` mermaid refs (`<pre class="mermaid">` + a `.mermaid{}` rule — not a script).
- Shim: `npm install` (6 pkgs), `tsc --noEmit` clean under `strict`, `esbuild` bundle 6.2 KB dev / 3.4 KB minified. Live tool versions: esbuild 0.24.2, TypeScript 5.9.3, @types/vscode 1.125.0.
- Packaging: self-contained single-file `win-x64` = 76,555,782 B exe (~73 MB dir); framework-dependent portable = ~3.5 MB. Spawn+render latency (Measure-Command): self-contained ~1.9 s ×3, portable-`dotnet` ~1.76 s ×2 warm; ~3.5 s cold first run.
- `git status --porcelain src/` empty at completion — core byte-identical.

### Completion Notes List

- **This is a SPIKE — the durable deliverable is [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md), not shippable code.** All five ACs met with empirical evidence.
- **Seam decision (ADR 0005):** C# renders the webview HTML from the shared 6.2 section view models via a **`WebviewRenderAdapter` (2nd `IRenderAdapter`)**; the epic 6.4 AC #1 "JSON export the TS renders" is **rejected**. Extension↔core data path = **spawn the .NET tool as a child process, HTML/JSON on stdout**. Live-push = **extension-host `FileSystemWatcher` → re-render → in-place `postMessage`**. Packaging = **bundle a self-contained single-file publish** (~73 MB/RID) for zero-prereq install. The shim needs to inject exactly **two** host-runtime values (`cspSource`, `nonce`) — proof it can stay dumb.
- **Story 6.4 (runtime) is unambiguously scoped:** build `WebviewRenderAdapter : IRenderAdapter` (`Id = "webview"`) over all five 6.2 views; its existing story file's ADR-0005 gate is now satisfiable and its fork selected (§41 of 6.4, not §42). Order 6.2 → 6.3 → 6.4 → 6.5 confirmed.
- **Single manual-verification gap (honest):** everything up to `webview.html = <string>` is proven headlessly (renderer output, CSP policy, shim compile/bundle, spawn+JSON, packaging sizes/latency). The **actual pixel paint + live refresh inside VS Code's Electron** cannot be exercised in this non-interactive CLI environment — it needs one `F5` run of `spike/vscode` in a real VS Code. Called out in ADR 0005 ("Not yet proven") and `spike/README.md`; Story 6.4 must close it.
- **Promotion note (branch orchestration):** the durable artifacts (ADR 0005, `docs/adrs/README.md` index, this story record, sprint-status → review) and the quarantined `spike/` folder all currently live on branch `spike/vscode-6-3`. To land only the decision on `main` while keeping the throwaway code quarantined, cherry-pick / `git checkout spike/vscode-6-3 -- docs/adrs/0005-*.md docs/adrs/README.md _bmad-output/…/6-3-*.md _bmad-output/…/sprint-status.yaml` (and optionally `spike/`). No `src/` changes exist to promote — core is untouched.

### File List

_New (all quarantined under `spike/`, throwaway):_
- `spike/README.md`
- `spike/.gitignore`
- `spike/vscode/package.json`
- `spike/vscode/tsconfig.json`
- `spike/vscode/esbuild.js`
- `spike/vscode/.vscodeignore`
- `spike/vscode/src/extension.ts`
- `spike/vscode/renderer/SpecScribe.WebviewSpike.csproj`
- `spike/vscode/renderer/Program.cs`
- `spike/vscode/renderer/WebviewShell.cs`

_New (durable — the decision):_
- `docs/adrs/0005-vs-code-webview-runtime-and-packaging.md`

_Modified (durable):_
- `docs/adrs/README.md` (ADR 0005 index entry)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (6.3 → in-progress → review)
- `_bmad-output/implementation-artifacts/6-3-vs-code-integration-spike.md` (this record)

_Untouched:_ `src/SpecScribe/**` (core byte-identical — the shipped tool gains no spike code).

## Change Log

- 2026-07-10 — **Spike executed (dev-story).** Built a throwaway VS Code extension shim (`spike/vscode/src/extension.ts`, ~180 lines) + a C# webview renderer (`spike/vscode/renderer`) that renders the dashboard + epics surfaces from the REAL Story 6.2 section view models (no scraping, no `.md` re-parse, zero `src/SpecScribe` edits). Gathered empirical evidence for the data path, CSP survival, live-push transport, and packaging. Authored **ADR 0005** ratifying **C#-rendered webview HTML via a `WebviewRenderAdapter`** (rejecting the JSON-export reinterpretation of epic 6.4 AC #1), **child-process spawn** invocation, **extension-host watcher → in-place postMessage** live-push, and **self-contained single-file bundling** for zero-prereq install. Updated the ADR README index; confirmed Story 6.4 (runtime) is seated with its ADR-0005 gate now clearable. All spike code quarantined under `spike/`; core untouched (generated site byte-identical). One manual-verify gap recorded: actual VS Code pixel paint + live refresh needs an `F5` run. Status → review.

- 2026-07-10 — Story 6.4 drafted (create-story). **Redirected at create-story (owner-confirmed) from "webview runtime" to a "VS Code Integration Spike."** The owner surfaced a "pure C# webview / install-and-just-works" goal and the risk that a TS-render answer could imply a core-language rewrite (its own epic); chose a spike-first path (matching the Epics 11–15 spike-led pattern). Decisions captured: (1) keep the eventual runtime as one story, but **defer it to Story 6.4 (the webview runtime)** — this **Story 6.3** is the spike that seats it; (2) validate (not yet commit) a **thin TS host shim + C# `WebviewRenderAdapter`** rendering Story 6.2's view models to webview HTML, which **reinterprets epic 6.4 AC #1** (JSON export → C#-rendered webview HTML) — to be ratified or rejected by the spike's **ADR 0005**. Platform constraint acknowledged: a VS Code extension cannot be zero-TS (Node extension host). Spike deliverables: a throwaway feasibility proof (dashboard + epics in a webview from the C# core), live-push feasibility, CSP/theming/packaging measurements, **ADR 0005** (the durable output), and a seated Story 6.4. Story 6.2 is a hard dependency for the runtime, a soft one for the spike (stand in + document needed shapes). **Renumbered (owner-authorized, same day):** Epic 6 impl stories now run in dependency order 6.1 → 6.2 → **6.3 (this spike)** → **6.4 (webview runtime)** → 6.5 (host theming); this story took the 6.3 slot vacated when host-theming moved to 6.5, and the runtime kept 6.4. Story file renamed `6-4-…-webview-runtime` → `6-3-vs-code-integration-spike`; sprint-status updated; epics.md numbering reconciliation (its 6.3=theming, no spike entry) remains a follow-up correct-course. The runtime build, real export contract, host theming (Story 6.5), and extension packaging/publish (Epic 16.5) are explicitly out of scope; nothing production lands on `main`.
