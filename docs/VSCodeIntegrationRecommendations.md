# VS Code Native-Integration Recommendations

**Date:** 2026-07-11 · **Status:** Recommendations — candidates for `correct-course` / story seating. Nothing here is binding; where an item says "recommend against" or "defer," that too is a proposal for the owner to ratify.
**Scope reviewed:** the shipped extension ([extension/src/extension.ts](../extension/src/extension.ts), [extension/package.json](../extension/package.json)), the `specscribe webview` CLI seam ([src/SpecScribe/Commands.cs](../src/SpecScribe/Commands.cs)), Epic 6 stories 6.1–6.7, Epic 16 (esp. 16.5/16.8), Epics 5, 7, and 8 forward-looking stories, and ADRs [0003](adrs/0003-directory-scoped-settings-and-read-only-helpers.md), [0005](adrs/0005-vs-code-webview-runtime-and-packaging.md), [0006](adrs/0006-delivery-architecture-and-distribution.md).

---

## 1. Where things stand today

The Story 6.4/6.5 extension is deliberately minimal — a ~230-line "irreducible shim" per ADR 0005:

| Integration point | Today |
|---|---|
| Commands | One: `SpecScribe: Open Status` |
| Views | One `WebviewPanel` in `ViewColumn.One` (dashboard + epics family) |
| Activation | None declared (command-only activation) |
| Settings | One: `specscribe.toolPath` (machine-overridable) |
| File watching | Two extension-host globs: `_bmad-output/**/*.md`, `docs/adrs/**/*.md`, debounced 400 ms → full re-spawn → in-place `postMessage` patch |
| Theming | Story 6.5 host-variable bridge (`.vscode-*` scoped), six status accents contrast-tuned per theme |
| Helpers | Read-only `copyHelperText` → clipboard (AD-6) |
| Workspace model | `workspaceFolders[0]` only; default path resolution (no `--source`/`--adrs`/`--deep-git` pass-through) |
| Packaging | Local VSIX script only; no binary is bundled yet (tool resolution today: `toolPath` setting → bundled-binary check, which finds nothing → `specscribe` on PATH). Bundling a ~73 MB/RID self-contained binary is the ADR 0005 plan, delivered by Story 16.5 |

Everything below proposes ways to grow the *host-integration* surface without violating the two invariants that shaped 6.4: **rendering stays in C#** and **the extension stays read-only**.

## 2. Constraints every recommendation respects

1. **Thin shim, no rendering brain (ADR 0005, AD-1/AD-2).** The extension parses no markdown and holds no project knowledge. However, ADR 0005 explicitly leaves the door open: *"A JSON export may still be added later if a non-webview consumer needs raw data."* Tree views, status bar text, and diagnostics are **host delivery** of core-emitted *data* — they are the intended use of that clause, not a violation of it. The rule of thumb used below: the C# core decides *what* to say; TypeScript only decides *where VS Code shows it*.
2. **Read-only end to end (AD-6, ADR 0003).** No recommendation writes a project artifact. Helper-style actions stop at "put the command/prompt where the user can fire it themselves."
3. **Directory-scoped settings stay the source of truth (ADR 0003).** VS Code settings should carry *host* concerns only; project behavior lives in the repo's settings file so CLI, watch, and editor agree.
4. **Byte parity of the HTML surface.** Nothing here touches the generated site; all changes are extension-side or additive core exports (the golden fingerprint is unaffected).
5. **The six `--status-*` tokens are the semantic layer.** Any new native surface that shows status (tree icons, status bar) must derive its colors/labels from core-emitted status data, not re-map onto VS Code's 3-severity palette (the Story 6.5 ruling).

## 3. Recommendations

Effort tags: **S** (hours), **M** (story-sized), **L** (multi-story / epic-sized).

### R1. Discoverability and activation — make the extension findable *(mostly S)*

The extension currently does nothing until the user already knows the command exists. Highest leverage per line of code in this whole document:

- **R1.1 — Activate on project detection.** Add `workspaceContains:_bmad/config.toml` (and/or `workspaceContains:_bmad-output`) to `activationEvents`, and on activation set a context key (`specscribe.projectDetected`) via `setContext`. Detection is by *path existence only* — no content parsing, so AD-2 holds. (S)
- **R1.2 — Gate contributions with `when` clauses.** Use the context key so SpecScribe menus/views only appear in repos that actually have spec artifacts — native-feeling, zero noise elsewhere. (S)
- **R1.3 — Explorer/editor context menus.** `explorer/context` on the `_bmad-output` folder and `editor/title` on artifact markdown → "Open in SpecScribe Status". Menu contributions are pure manifest; the handler reuses the existing open-status path. (S)
- **R1.4 — Walkthrough (`contributes.walkthroughs`).** A 4-step first-run: detect/open a spec-driven repo → open the status panel → what "read-only companion" means → where full-site generation lives (`specscribe generate` / watch). Walkthroughs surface automatically on install — this is the single best onboarding lever for the Story 16.5 Marketplace launch and should be seated with it. (M)
- **R1.5 — Welcome views (`viewsWelcome`).** If R3.1's tree view ships, its empty state ("No SpecScribe project detected — open a folder containing `_bmad-output`", with a docs link) replaces a dead view with guidance. (S)
- **R1.6 — Marketplace metadata polish.** Real `categories` (currently `Other` — should be `Visualization`, `Other`), `keywords` (spec-driven development, BMAD, dashboard), an icon, `repository`, and a README with screenshots. Already implied by Story 16.5 AC #1; recording here so the walkthrough + metadata land together. (S, seats in 16.5)

### R2. Command surface — more than one door in *(S)*

All of these reuse the existing spawn/panel machinery; they are routing, not rendering:

- **R2.1 — Direct-open commands.** `SpecScribe: Open Dashboard` and `SpecScribe: Open Epics` — same panel, different initial `push()` target. The bundle already carries every surface keyed by path. (S)
- **R2.2 — Manual refresh command + panel-title button.** `SpecScribe: Refresh Status` bound to the existing debounced refresh; also contribute it to `view/title`-style panel actions. Covers changes the watchers miss (see R6) and gives users an obvious "is this current?" affordance. (S)
- **R2.3 — `SpecScribe: Generate Full Site` / `SpecScribe: Watch`.** Stage the tool invocation in a VS Code **integrated terminal**: `createTerminal` + `sendText(cmd, /* execute: */ false)`, so the command sits at the prompt and the **user presses Enter** — the same staged-handoff treatment as R4.3. This keeps constraint #2 and ADR 0003's "explicit user choice outside SpecScribe" intact to the letter: SpecScribe never executes a write to the project's output; the user does. (Watch, once fired by the user, runs visibly and is user-killable.) If the owner prefers one-click execution here, that is a deliberate, recorded exception to constraint #2 — not the default. (S)
- **R2.4 — `SpecScribe: Open Generated Site`.** If the configured output root exists, open its `index.html` via `vscode.env.openExternal`. Pairs with the current "Run `specscribe generate` to browse the full site" toast (extension.ts:91–93) — today that message names a command the user must go run elsewhere; it should offer a button that does R2.3/R2.4. (S)

### R3. Native surfaces beyond the panel *(M–L — the core of "native integration")*

- **R3.1 — Sidebar tree view: the project outline.** An activity-bar view container ("SpecScribe") hosting a `TreeView`: epics → stories, each with status. This is the canonical "feels native" surface — visible, persistent, and glanceable without opening a panel.
  - **Data path:** a new core export — either a `specscribe outline` command or (better) an `outline` section added to the existing `webview` JSON payload — emitting host-neutral data: epic/story id, title, status (one of the six stages), counts, and the surface path + source artifact path for each node. This is exactly the "JSON export for a non-webview consumer" ADR 0005 §1 reserved. The TS side maps records → `TreeItem`s 1:1 — no interpretation.
  - **Status iconography:** per constraint #5, derive icons from the status *stage name* the core emits, and do **not** collapse onto `testing.iconPassed`-style host severities. Mechanically this is fiddlier than it sounds: `TreeItem.iconPath` SVGs cannot be dynamically tinted, and `ThemeIcon` colors only accept *contributed theme colors* — the Story 6.5 accent values live in a CSS bridge inside the webview and are unreachable from the extension host. The workable options are (a) contribute six `specscribe.status.*` theme colors in the manifest (with light/dark/highContrast defaults mirroring the 6.5 tuning) and use `ThemeIcon` with those, or (b) ship pre-baked per-theme SVG variants. Option (a) is the native-correct one; budget for it in the story's effort.
  - **Interactions (all read-only):** click → reveal that story in the webview panel (existing `push()`); context action → open the source markdown; context action → copy the story's helper prompt (reuses `copyHelperText` semantics).
  - **Seating:** this is a new Epic 6 story (6.8) — it is a genuinely new structural surface and per the Epic 2 retro rule ("split, don't absorb") should not ride along inside another story. (L)
- **R3.2 — Status bar item.** `$(checklist) SpecScribe: 3 active · 2 review` (or the sprint's counts), computed core-side into the payload, clicking opens the status panel. Cheapest persistent-visibility win; also the natural home for a "stale data" indicator if a refresh fails. (S–M, needs the same outline/summary export as R3.1)
- **R3.3 — Panel placement flexibility.** Open in `ViewColumn.Beside` by default (status next to the file you're editing beats replacing it), with a `specscribe.openLocation` host setting (`active` / `beside`). Longer-term, consider a `WebviewView` variant docked in the sidebar/panel for an always-on compact status — but only after R3.1, since a tree view may satisfy the same need for less. (S for ViewColumn; M for WebviewView)
- **R3.4 — Multi-root support.** Replace `workspaceFolders[0]` with: single folder → as today; multi-root → detect which folders contain `_bmad/config.toml` and quick-pick when ambiguous (remember per-session). Also fixes a latent wrong-folder bug for multi-root users. (M)

### R4. Editor ↔ artifact bridges *(M)*

- **R4.1 — "Reveal source" from the webview.** Add source artifact paths to the payload's surface/section metadata; a `revealSource` webview message → `vscode.window.showTextDocument`. The inverse of R1.3 and the biggest "the portal and my files are one thing" moment available. Read-only (opens an editor, changes nothing). (M)
- **R4.2 — Epic 7 code citations should open real editors in the webview context.** Story 7.1/7.2 build in-portal code pages with a shared `#L{n}` anchor convention and a configurable external base URL. In VS Code, the *native* target exists: when a code-citation link is clicked inside the webview, the shim should map it to `showTextDocument(file, { selection: line })` instead of a portal code page. Recommendation: when Story 7.2 defines link resolution, emit code links with structured data attributes (`data-code-path`, `data-line`) so hosts can re-target them — the HTML surface keeps portal/GitHub links, the webview host intercepts. Flag this in 7.1/7.2's create-story so the seam is designed in, not retrofitted. (recorded now: S; implementation rides Epic 7)
- **R4.3 — Story 8.4 next-step commands → terminal handoff.** 8.4 defines a state-aware "next step command" surface. In the webview, pair the existing *copy* helper with **"Open in Terminal"**: `createTerminal` + `sendText(command, /* execute: */ false)` — the command is staged at a prompt and the **user presses Enter**. This preserves the AD-6/ADR 0003 letter (SpecScribe never executes; the explicit choice stays with the user) while feeling dramatically more native than clipboard round-tripping. Worth an explicit AC in 8.4 or a small 6.x follow-on so the read-only ruling is recorded. (S–M)
- **R4.4 — What *not* to build: content-aware CodeLens/hover on artifact markdown.** Lenses like "3 ACs · in review" above a story heading would require the extension to parse markdown or maintain a source-position map in the core. The payoff doesn't justify opening AD-2 exceptions; the tree view (R3.1) + context menu (R1.3) deliver the same navigation without it. Recommend explicitly deferring. (—)

### R5. Configuration and settings *(S–M)*

- **R5.1 — Keep two clean settings tiers.** VS Code settings = *host* concerns only: `toolPath` (exists), plus proposed `specscribe.openLocation` (R3.3), `specscribe.refresh.debounceMs`, `specscribe.refresh.enabled`. Project behavior (source/ADR roots, deep-git, project name) stays in the directory-scoped settings file per ADR 0003 — do **not** mirror those into VS Code settings, or CLI/watch/editor drift becomes possible and provenance (5.2 AC #2) gets murky.
- **R5.2 — But make the project settings *reachable* from the editor.** `SpecScribe: Open Project Settings` — opens the repo's directory-scoped settings file (the `.specscribe` file `SettingsStore` already reads/writes via the interactive Configure Paths flow), or offers to scaffold it via the CLI's interactive flow in a terminal, keeping writes user-driven. The file exists today; what Story 5.2 adds is CLI/interactive *parity and provenance* — this command is the file's discoverability in-editor and can ship independently. (S)
- **R5.3 — Known gap: the webview spawn ignores directory-scoped settings.** `WebviewCommand` (like `generate` and `watch`) calls `SiteSettings.Resolve()` directly and never consults `SettingsStore` — the `.specscribe` file is honored **only** by the interactive menu today. So a repo with saved custom source/ADR/deep-git settings renders with defaults in the webview *right now*. This is exactly the parity Story 5.2 AC #1 promises ("configured defaults are reused from directory-scoped settings … behavior matches equivalent CLI arguments"); route `Resolve()` through the settings store for all commands when 5.2 lands, and add a webview-parity test. Until then the extension inherits the same gap as headless `generate`. (gap recorded; fix seats in 5.2)
- **R5.4 — Workspace Trust posture.** The extension spawns a workspace-adjacent binary and reads a `toolPath` that workspaces can override — the classic untrusted-workspace attack shape (the 6.4 review already scoped toolPath RCE). Declare `capabilities.untrustedWorkspaces` explicitly: recommend `"supported": "limited"` with `restrictedConfigurations: ["specscribe.toolPath"]`. Precisely scoped: that makes VS Code ignore the *workspace-level* `toolPath` value in untrusted workspaces (user/machine-level values still apply, which is the intended behavior — those are the user's own). Small manifest change, closes a real hole, required reading for the 16.5 Marketplace review bar. (S — **do before Marketplace publication**)

### R6. File-change reactivity *(S–M — includes one shipped gap)*

- **R6.1 — Gap: non-markdown sources never trigger refresh.** Both the extension's globs (`**/*.md`) and the core `FileWatcherService` (`Filter = "*.md"`, [FileWatcherService.cs:35](../src/SpecScribe/FileWatcherService.cs)) ignore **`sprint-status.yaml`** — the data source of the sprint board / Now & Next — and **`_bmad/config.toml`** (project name). Editing sprint status while the panel is open silently shows stale data, which undercuts the "stays live" promise of 6.4 AC #3. Fix in both layers, and note the core side is more than the `Filter` property: `.md` is enforced in three places (`Filter = "*.md"`, the re-guard in `Debounce()`, and the fire-time dispatch whose routes — `RegenerateAdrs`/`RegenerateEpics`/`GenerateOne`/`RemoveFor` — assume a markdown artifact; a yaml/toml change needs its own "regenerate the surfaces this feeds" route, likely a full or dashboard-scoped rebuild). Extension side is just the glob list. This is the most defect-adjacent item in this document; recommend seating it as a small story (not a drive-by patch, given the dispatch work) ahead of the cosmetic items. (S–M)
- **R6.2 — Derive watch roots from the core's resolution, not hardcoded globs.** The extension watches literal `_bmad-output/` + `docs/adrs/`; a repo configured with non-default roots (Story 5.1/5.2) live-updates the wrong paths. Have the `webview` payload include the **resolved** source/ADR roots (workspace-relative) from `ForgeOptions`, and build the `RelativePattern` watchers from those. Core stays authoritative; the shim stops duplicating path assumptions. (S–M)
- **R6.3 — Visibility-aware refresh.** With `retainContextWhenHidden`, a hidden panel still re-spawns a ~2 s render per change burst. Track `panel.visible`/`onDidChangeViewState`: while hidden, mark dirty instead of spawning; render once on reveal. Battery/CPU-polite and zero UX cost. (S)
- **R6.4 — Scoped re-render / warm renderer (the ADR 0005 §3 named follow-up).** ADR 0005 already directs Story 6.4+ to bring the ~1.8 s full re-render toward sub-second by scoping ingest like `SiteGenerator.RegenerateEpics`. Options in cost order: (a) pass a `--changed <path>` hint to `specscribe webview` so the core re-ingests narrowly; (b) a long-lived `specscribe webview --serve` mode (line-delimited JSON re-renders on stdin ping) — ADR 0005 explicitly deferred the warm-process trade-off, so (b) needs an ADR note if pursued. Recommend (a) first. (M)
- **R6.5 — Git-state refresh (optional, low priority).** The dashboard's git pulse goes stale after commits, which touch no watched `.md`. The built-in `vscode.git` extension API exposes repository state-change events the shim could subscribe to (data only, read-only) to trigger the same debounced refresh. Defer until users actually notice — the artifact-driven refresh covers the primary loop. (M, defer)

### R7. UX polish *(S)*

- **R7.1 — Progress affordance on cold start.** The ~3.5 s cold spawn currently shows an empty panel. Use `window.withProgress` (notification or the panel itself via a static "Rendering…" splash in `errorHtml`-style inline HTML) so first paint has a heartbeat. (S)
- **R7.2 — Actionable error surface.** `errorHtml` is good; add actions for the two most likely fixes: "Set specscribe.toolPath" (via `workbench.action.openSettings`) and "Retry". Note today's error page is script-free by design (CSP `default-src 'none'`, no message bridge, and the error path drops the panel singleton) — so the cheap implementation is native buttons on the `showErrorMessage` notification rather than in-page links; in-page buttons would need a nonce'd bridge or `enableCommandUris` and aren't worth it. (S as a notification; skip the in-page variant)
- **R7.3 — Panel icon.** Set `panel.iconPath` (bundle a small SVG) so the tab reads as SpecScribe among a row of editors. (S)
- **R7.4 — Close out the deferred a11y items.** Story 6.4 deferred webview nav a11y and 6.5 deferred the light-palette pass + the manual F5 smoke. Fold the nav a11y work into whichever story next touches the shim (R2/R3 candidates) rather than letting it float. (S–M, already-tracked debt)

### R8. Packaging and future-story alignment *(recorded here, executed in their own epics)*

- **R8.1 — Platform-specific VSIX targets (Story 16.5).** `vsce package --target win32-x64` etc. publishes per-platform VSIXs; the Marketplace serves each user only their platform's build. This converts ADR 0005's "~73 MB **per RID**" from a multiplied download into a single-RID one, without changing the architecture. Strongly recommend 16.5 adopt platform-targeted packaging as its default matrix strategy (with a framework-dependent "thin" variant as the documented opt-in for .NET-runtime holders). (seats in 16.5)
- **R8.2 — Story 6.7 (JSON+SPA) as a webview accelerant, not just a site form.** ADR 0006's "pre-generated JSON breaker": once 6.7's JSON layer exists, the webview could *optionally* consume committed/CI-generated JSON for instant first paint, with the spawn refreshing live data behind it. Keep the `WebviewBundle` payload shape compatible with 6.7's data-layer schema so this stays a small step. Note the trade-off ADR 0006 records: a JSON-only consumer cannot regenerate — the binary remains the live path. (design note for 6.7's create-story)
- **R8.3 — Story 4.8 diagnostics → the Problems panel.** `specscribe webview` already streams per-artifact generation errors on stderr (Commands.cs:56–62). Emit them structured (JSON lines: path, message, severity — core-owned format shared with 4.8's diagnostics page), and have the shim map them to VS Code `Diagnostics` on the offending artifact files. Broken artifacts then surface where every other tool's errors live — arguably the most "native" integration in this list, and it's pure data transport. (M; seats with or after 4.8)
- **R8.4 — Framework epics (11–15).** Keep the extension's *dynamic* behavior framework-agnostic: the shim carries zero project knowledge, and R6.2 makes the watch roots core-derived instead of hardcoded `_bmad-output` globs. One honest caveat: the *static manifest* contributions this document proposes (R1.1's `workspaceContains:_bmad/config.toml` activation, R1.3's `_bmad-output` menus, R3.4's folder detection) are necessarily literal paths and today BMAD-specific — a manifest cannot ask the core anything. Treat those as the one known framework-coupled list: when Epics 11–15 add frameworks, each adds its detection marker(s) to `activationEvents`/`when` clauses (a manifest-only change), while everything downstream of activation stays core-driven and framework-free. (guardrail + a small per-framework manifest chore)

## 4. Suggested sequencing

| Wave | Items | Rationale |
|---|---|---|
| **Now (quick-dev sized)** | R5.4 (Workspace Trust), R1.1–R1.3 (activation + menus), R2.1–R2.4 (commands), R3.3 (open beside), R5.2 (open project settings), R7.1–R7.3 | Closes the trust hole before Marketplace and multiplies discoverability for trivial cost |
| **Next (story-sized, Epic 6 additions)** | R6.1 (yaml/toml watch gap — first, it's the live-data defect), R3.1 outline tree view (+ R1.5, R3.2 riding its data export), R4.1 reveal-source, R6.2 derived watch roots, R6.3 visibility-aware refresh, R3.4 multi-root | The live-data fix, the genuinely new native surface, and the reactivity hardening; each is a reviewable story |
| **With their owning stories** | R1.4 + R1.6 + R8.1 → 16.5 · R4.2 → 7.1/7.2 · R4.3 → 8.4 · R5.3 → 5.2 · R8.2 → 6.7 · R8.3 → 4.8 follow-on | Recorded now so the seams are designed in, not retrofitted |
| **Deferred / recommend against** | R6.4b warm renderer (needs ADR note), R6.5 git-event refresh, R4.4 content-aware CodeLens (recommend against), sidebar WebviewView (revisit after tree view) | Real cost or ADR implications; wait for demand |

## 5. References

- [ADR 0005 — VS Code Webview Runtime](adrs/0005-vs-code-webview-runtime-and-packaging.md) (thin shim, C# renders, JSON-export clause, warm-renderer trade-off)
- [ADR 0006 — Delivery Architecture & Distribution](adrs/0006-delivery-architecture-and-distribution.md) (npx channel, JSON+SPA adapter, pre-generated-JSON breaker)
- [ADR 0003 — Directory-Scoped Settings and Read-Only Helpers](adrs/0003-directory-scoped-settings-and-read-only-helpers.md)
- Epic 6 stories 6.1–6.7 and Epic 16 stories 16.1–16.8 in `_bmad-output/planning-artifacts/epics.md`
- [extension/src/extension.ts](../extension/src/extension.ts) · [extension/package.json](../extension/package.json) · [src/SpecScribe/Commands.cs](../src/SpecScribe/Commands.cs) · [src/SpecScribe/FileWatcherService.cs](../src/SpecScribe/FileWatcherService.cs)
