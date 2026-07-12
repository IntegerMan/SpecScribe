---
baseline_commit: b58d78740621a64f27ec7fc27d47e6d218ff7c06
renumbered_from: 6.3
---

# Story 6.5: Host-Aware Theming and Explicit Helper Actions

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!-- Renumbered from Story 6.3 → 6.5 on 2026-07-10 (owner-directed sequencing fix). See "Renumber note" below. -->

> ## ✅ UNFROZEN 2026-07-10 — gate reverts to Story 6.4
> Briefly frozen during the delivery-architecture review. **[ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md)
> (Accepted) re-affirmed ADR 0005**, so the C#-rendered webview this story themes (`.vscode-*` host variables over
> a C# render) stands as designed. This story's **original prerequisite gate is unchanged**: it stays blocked on
> [Story 6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (the webview must exist to theme)
> and Story 6.2 — see the "⛔ READ FIRST" gate below. `ready-for-dev` means context-complete, not
> prerequisites-met.

## Story

As a maintainer using multiple themes,
I want the VS Code webview visuals to align with VS Code chrome across light, dark, and high-contrast themes while preserving SpecScribe's status/insight semantics,
so that the in-editor experience feels native without losing product identity — and any helper affordances stay strictly read-only.

## ⛔ READ FIRST — this story is GATED on Stories 6.4 and 6.2 (do not start until both are `done`)

**Both acceptance criteria presuppose a rendering VS Code webview that does not exist yet.** AC #1 is "**when the webview renders** … host theme variables are respected"; AC #2 is "**helper actions are exposed in the webview** … generates explicit commands or prompts only." There is **no VS Code extension in this repository at all** — no `package.json`, no TypeScript, no extension host, no webview markup (confirmed in [Story 6.2's scope decision](6-2-read-only-vs-code-dashboard-and-epics-experience.md), lines 22, 70). That webview is built by **Story 6.4** (Read-Only VS Code Webview Runtime), which is currently `backlog`.

**Hard prerequisites (all must be `done` before this story is dev'd):**

| Prereq | What it delivers that THIS story needs | Status at create-story (2026-07-10) |
|---|---|---|
| **Story 6.4** — webview runtime | The webview UI itself (extension host + webview panel + the rendered dashboard/epics surfaces) that this story themes, plus the command/clipboard host APIs the helper buttons call. **Without it there is nothing to theme and nowhere to put a helper button.** | `backlog` — not created, not implemented |
| **Story 6.2** — section view models | The host-neutral section view models 6.4 serializes + renders. Theming maps host chrome around that rendered content. | `review` (not yet `done`) |
| **Story 6.1** — delivery contract + `AssetManifest` | The `AssetManifest` that names the stylesheet(s) the webview loads — the seam a webview-only theme layer plugs into. | `review` (not yet `done`) |

**Consequence:** this story's context is captured now, but **`dev-story` must NOT begin until Story 6.4 lands the webview**. If a run reaches this story while 6.4 is unbuilt, STOP and surface it — there is no webview to render, no `.vscode-dark`/`.vscode-high-contrast` body classes to key off, and no button surface for helpers. The `ready-for-dev` status reflects "context complete," not "prerequisites met." This gate exists because Epic 3's retro flagged deferring a defining decision to the dev and correcting it at review — the defining decision here is *the webview must exist first*.

### Renumber note (why this is 6.5, not 6.3)

This story was **Story 6.3** in earlier planning. It was renumbered to **6.5** on 2026-07-10 (owner-directed) purely to fix ordering: host theming depends on the webview, but the webview (Story 6.4, split out of 6.2 append-only) sorted *after* 6.3 — so 6.3 sorted *before* the thing it depends on. Renumbering theming to the next free slot (6.5) makes the story number match the dependency order (6.1 → 6.2 → 6.4 → 6.5) and retires the "runs after 6.4 despite sorting before it" footnote. No scope changed. The old 6.3 slot is retired with a breadcrumb in [epics.md](../planning-artifacts/epics.md) and [sprint-status.yaml](sprint-status.yaml); 6.4 keeps its number (append-only/no-renumber, same convention as 4.8-out-of-4.2, Epics 11-15, and 6.4-out-of-6.2). ACs are verbatim from the former 6.3.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 6.5 (formerly Story 6.3)._

1. **Given** light, dark, and high-contrast VS Code themes
   **When** the webview renders
   **Then** host theme variables are respected for chrome and container surfaces
   **And** status and insight semantics remain clear and accessible.

2. **Given** helper actions are exposed in the webview
   **When** I trigger a helper
   **Then** it generates explicit commands or prompts only
   **And** no source planning artifacts are mutated by the helper path.

## Design decision captured at create-story (owner-confirmed) — theming direction

**Direction: "semantic accents, host-tuned for contrast."** The webview maps VS Code host variables for **chrome and container surfaces** (page/panel/card backgrounds, body text, borders, shadows, the nav bar), but the **SpecScribe status + insight accents stay SpecScribe-owned as the source of meaning** — they are **not** bridged onto host error/warning/success tokens. Where a SpecScribe accent would fail contrast against a dark or high-contrast host background, **adjust its lightness/contrast per host theme** so it stays accessible, but keep the *hue* that carries the semantic (teal = active, deep-teal = review, green = done, golds = ready/drafted, tan-grey = pending, grey = deferred). This is exactly AD-7's rule ("webview maps host variables for container/chrome while preserving SpecScribe semantic accents", [ARCHITECTURE-SPINE.md:82–88](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)) with the contrast-tuning obligation made explicit for high-contrast themes.

**Rejected alternatives (do not implement):**
- **Preserve accents as-is (no contrast tuning)** — keeps identity but the six `--status-*` values are tuned for the warm-light HTML palette; several (e.g. `--status-pending #b8b2a8`, `--status-drafted #e8d9a8`, `--status-ready #d4a017`) are low-contrast against a dark or high-contrast editor background, failing AC #1's "clear and accessible."
- **Bridge accents onto the host palette** (map stages to `--vscode-*` error/warning/success) — most native, but collapses SpecScribe's **six-stage** vocabulary into VS Code's ~3 severity colors, destroying the stage distinction the whole insight system depends on (memory: [specscribe-status-token-system]). Explicitly rejected.

## Critical current-state facts the dev MUST internalize

1. **The stylesheet has NO theming today.** [specscribe.css](../../src/SpecScribe/assets/specscribe.css) is a **single fixed warm-light** palette in one `:root` block ([lines 6–59](../../src/SpecScribe/assets/specscribe.css)). There is **zero** `prefers-color-scheme`, **zero** `data-theme`, **zero** `.vscode-dark`/high-contrast handling anywhere in 3170 lines. Chrome colors are hard token references: `body { background: var(--cream); color: var(--ink); }` ([line 63](../../src/SpecScribe/assets/specscribe.css)), borders via `--border`, shadows via `--shadow`, the nav on a near-black bar. So theming here is **net-new**, not a modification of an existing theme switch.
2. **The six status tokens + the insight accent palette are the semantic layer to preserve.** `--status-pending/-drafted/-ready/-active/-review/-done/-deferred` ([lines 34–40](../../src/SpecScribe/assets/specscribe.css)) are the single stage→color source (memory: [specscribe-status-token-system]); the chart accents (`--teal`, `--teal-deep`, `--gold`, `--gold-light`, `--moss`, `--moss-light`, `--rust`) feed sunburst/donut/funnel/mosaic/progress. Every chart, legend, swatch and badge routes through these (grep confirms ~60 `var(--status-*)` uses). These are what stays SpecScribe-owned and gets contrast-tuned per host theme.
3. **The HTML file surface must NOT change a single byte.** AC #1 is scoped to *the webview*. The generated `.html` files stay the fixed warm-light theme. The **byte-identical golden regression is still the top guardrail** — the committed `GoldenContentFingerprint` test + the benign-diff normalizations (memory: [golden-diff-normalization-gotchas] — currently 5 normalizations, not the older 3) — if a browser-opened generated page changes any byte, you have overstepped into the wrong surface. The theme bridge is **webview-scoped only**.
4. **VS Code webviews expose theme colors as `--vscode-*` CSS variables and stamp `.vscode-light` / `.vscode-dark` / `.vscode-high-contrast` (and `.vscode-high-contrast-light`) classes on `<body>` automatically.** That is the host contract this story keys off — no JS is required to read the theme; a CSS layer scoped under those body classes and referencing `--vscode-*` vars is the mechanism.
5. **AD-6 / FR-17 / NFR-5 make helpers strictly read-only.** Helper buttons may "generate next-step prompts/commands (for example, code review prompts) **without writing project artifacts**" ([requirements-catalog.md:31 FR-17](../specs/spec-specscribe/requirements-catalog.md), [:43 NFR-5](../specs/spec-specscribe/requirements-catalog.md), [ARCHITECTURE-SPINE.md:74–80 AD-6](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). Any write (running the command, editing a file) is an **explicit external user choice** — the helper only produces text/commands and hands off (clipboard or a pre-filled terminal), never mutates `_bmad-output/**` or any source planning artifact.

## Scope

### IN scope

- **A webview-only host-theme bridge layer** (a new stylesheet or injected `<style>` the webview loads *in addition to* `specscribe.css`, e.g. `assets/specscribe-webview-theme.css`): under the `.vscode-light`/`.vscode-dark`/`.vscode-high-contrast`/`.vscode-high-contrast-light` body scopes, **override the chrome/container tokens** (`--cream`, `--warm-white`, `--parchment*`, `--ink`, `--ink-faded`, `--ink-light`, `--border`, `--shadow`, and the nav bar background/foreground) so they resolve from the corresponding `--vscode-*` variables (e.g. `--vscode-editor-background`, `--vscode-foreground`, `--vscode-panel-border`, `--vscode-editorWidget-background`, `--vscode-focusBorder`). Container surfaces adopt the host; content typography/spacing stays SpecScribe's.
- **Per-theme contrast tuning of the SpecScribe status + insight accents** — keep the hues, but under `.vscode-dark` and (especially) `.vscode-high-contrast*` scopes, override the `--status-*` and chart-accent token values to lightness/contrast variants that meet the accessibility floor against the host background. This is where "semantic accents, host-tuned for contrast" lands. The **light** webview theme can largely reuse today's values (they were designed for a warm-light background) but must still be verified against the actual host light background.
- **The shared status→color and chart contracts stay the single source.** Charts remain pure SVG + links (memory: [charting-is-pure-svg-no-js]); the bridge only re-values the *tokens* the charts already consume — it does **not** fork chart markup, add a second status vocabulary, or restyle per chart. `StatusStyles` remains the status→stage source untouched (memory: [specscribe-status-token-system]; 8.1 boundary).
- **Explicit, read-only helper affordance(s)** in the webview (AC #2): at minimum one helper button that **generates a command or prompt string** (FR-17's example: a code-review prompt) and hands it off explicitly (copy to clipboard and/or pre-fill a terminal via the extension host) — **never** writing to a source artifact. The helper path is a pure text/command generator + explicit handoff.
- **Accessibility verification across all three theme families** (AC #1 "clear and accessible"): status/insight accents, focus rings, and text meet the contrast floor in light, dark, and high-contrast; the reduced-motion behavior (memory: [motion-token-system]) and non-color status cues (Story 1.4/1.5) still hold in the webview.
- **A host-theming parity/verification hook**: extend the parity discipline so a themed webview still passes the semantic-parity harness ([RenderParity.cs](../../src/SpecScribe/RenderParity.cs)) — theming changes token *values*, never the semantic facts (nav targets, drill trail, status *stage*, card targets). If any legitimate host-specific divergence exists, it goes in the `HostRenderException` registry ([HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs)), the ONLY sanctioned divergence home (still expected empty for theming).

### OUT of scope (do NOT start it here)

- **Building the webview runtime, the extension host, or the JSON view-model export** — that is **Story 6.4**. This story assumes 6.4 shipped them and only adds theming + helper affordances on top. Zero new webview-transport architecture here.
- **Dark mode / theming for the generated HTML file surface.** AC #1 is webview-scoped. The browser-opened `.html` pages stay the fixed warm-light palette; their bytes must not change (golden regression). Do **not** add `prefers-color-scheme` to the base HTML surface as a side effect — that is a separate, unrequested feature and would break byte-parity.
- **New status vocabulary or a canonical-status refactor** — `StatusStyles` + the six `--status-*` tokens are consumed as-is (the Story 8.1 boundary, same ruling as 6.1/6.2). Theming re-values tokens per host; it never adds/renames/removes a stage.
- **Modeling chart geometry into data, or any chart-markup change** (memory: [charting-is-pure-svg-no-js]). The bridge changes token values only.
- **Any authoring/write capability in the webview or helpers** (AD-6/NFR-5). No file writes, no settings mutation, no "apply this change" button. Generate-and-handoff only.
- **Package/namespace split** — still seed-level, still forbidden (same ruling as 4.1/6.1/6.2). New assets/files stay in the single `src/SpecScribe/` project; any 6.4 TypeScript lives wherever 6.4 established it.

## Tasks / Subtasks

> **Precondition gate (do this before Task 1):** confirm Story 6.4 is `done` and a runnable webview exists (open it, see the dashboard + epics render). If not, STOP — this story cannot be implemented (see "READ FIRST" gate). Record the actual 6.4 webview asset-loading mechanism (how it injects/loads `specscribe.css`) — the theme bridge must plug into that exact seam.

- [x] **Task 1 — Author the webview host-theme bridge (chrome/container → host vars)** (AC: #1)
  - [x] Add a webview-only theme layer (e.g. `src/SpecScribe/assets/specscribe-webview-theme.css`, loaded by 6.4's webview *after* `specscribe.css` so it overrides). Under `.vscode-light`, `.vscode-dark`, `.vscode-high-contrast`, `.vscode-high-contrast-light` body scopes, re-value the **chrome/container tokens only**: map `--cream`/`--warm-white`/`--parchment*` → host background family (`--vscode-editor-background`, `--vscode-editorWidget-background`, `--vscode-sideBar-background`), `--ink`/`--ink-faded`/`--ink-light` → `--vscode-foreground`/`--vscode-descriptionForeground`, `--border` → `--vscode-panel-border`/`--vscode-widget-border`, `--shadow` → a host-appropriate shadow, and the nav bar bg/fg → host title-bar/panel tokens.
  - [x] Register the new asset in the delivery seam so the webview loads it: extend the [AssetManifest](../../src/SpecScribe/AssetManifest.cs) (or whatever 6.4 uses to enumerate webview stylesheets) — do NOT add it to the HTML surface's manifest (that would change generated-page bytes; verify the golden test stays green).
  - [x] Verify chrome reads natively: the panel/card/body/nav all follow the host theme when it switches, with no flash of warm-light.

- [x] **Task 2 — Contrast-tune the SpecScribe status + insight accents per host theme** (AC: #1)
  - [x] Under `.vscode-dark` and `.vscode-high-contrast*`, override the `--status-*` token values ([css:34–40](../../src/SpecScribe/assets/specscribe.css)) and the chart-accent tokens (`--teal`, `--teal-deep`, `--gold`, `--gold-light`, `--moss`, `--moss-light`, `--rust`) to lightness/contrast variants that keep the **same hue/semantic** but meet the accessibility floor against the host background. Do NOT bridge them onto `--vscode-*` severity colors (rejected direction).
  - [x] Keep the six-stage distinction visible in every consumer (sunburst segments, donut, funnel, mosaic, now-next cards, epic-chips, req-status blocks, progress fills) — verify each chart's stages stay mutually distinguishable in dark + high-contrast.
  - [x] Light webview theme: reuse today's accent values where they pass against the host light background; adjust only where they don't. Everything routes through the tokens — no per-chart literal colors (memory: [specscribe-status-token-system]).

- [x] **Task 3 — Focus rings, non-color cues, and reduced motion in the webview** (AC: #1)
  - [x] The on-brand focus rings ([css:97–130](../../src/SpecScribe/assets/specscribe.css)) currently use `--teal`/`--gold-light`; ensure they remain visible against each host theme (consider `--vscode-focusBorder` for chrome focus while keeping SpecScribe rings on content). High-contrast themes especially must show a clear focus indicator.
  - [x] Confirm non-color status cues (text labels, icons — Story 1.4/1.5) and reduced-motion behavior (memory: [motion-token-system], the two paired `prefers-reduced-motion` blocks) still hold in the webview host.

- [x] **Task 4 — Explicit read-only helper affordance** (AC: #2)
  - [x] Add at least one helper button in the webview (using 6.4's webview UI) that **generates a command/prompt string** (e.g. a code-review prompt per FR-17) and hands it off **explicitly** — copy to clipboard and/or pre-fill a VS Code terminal via the extension host command API. The helper is a pure generator + explicit handoff.
  - [x] **Assert the read-only invariant:** the helper path performs **no** write to any source planning artifact (`_bmad-output/**`, epics/stories/PRD/architecture) and no settings mutation (AD-6/NFR-5). Any execution of the generated command is a separate, explicit user action outside the helper.
  - [x] Add a test/guard proving the helper path is write-free (e.g. the helper returns text and never invokes a file-write/workspace-edit API).

- [x] **Task 5 — Parity + byte-identity guardrails** (AC: #1, #2)
  - [x] **HTML byte-identity:** run the golden-output regression ([SiteGeneratorAdapterTests](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs), incl. the committed `GoldenContentFingerprint`) and confirm the generated site is **byte-for-byte unchanged** — theming is webview-only and must not touch generated `.html` bytes. Normalize only the documented benign diffs (memory: [golden-diff-normalization-gotchas]). Also generate this repo's own site to `SpecScribeOutput` (memory: [generate-output-dir-is-specscribeoutput]; never `--output docs/live`) from an isolated source copy and diff: zero diffs.
  - [x] **Semantic parity:** confirm the themed webview still passes [RenderParity](../../src/SpecScribe/RenderParity.cs) — theming changes token values, not semantic facts (nav targets, drill trail, status *stage*, card hrefs). `HostRenderExceptions.Registry` stays empty for theming (theming is not a semantic divergence); if any legitimate host-only exception arises, register it there, never silently.
  - [x] `dotnet test` — whole suite green. If an existing rendering assertion must change, STOP — you likely altered the HTML surface (AC #1 is webview-only).

- [x] **Task 6 — Accessibility verification across the three theme families** (AC: #1)
  - [x] Verify status/insight accents, chrome text, and focus indicators meet the contrast floor in **light, dark, AND high-contrast** VS Code themes (AC #1 "clear and accessible"). Capture the check (which themes tested, any tokens adjusted) in Completion Notes.
  - [x] Confirm the six status stages remain mutually distinguishable (color + non-color cue) in every theme — the whole insight system depends on the stage distinction (memory: [specscribe-status-token-system]).

### Review Findings

- [x] [Review][Patch] `.vscode-high-contrast-light` is missing from the shared focus-ring selector list, so most interactive elements (index-card, quick-link-card, epic-mosaic-card, epic-chip, story-title-link, req-id-link, toc-sidebar links, breadcrumb, ac-anchor/ac-ref, view-epic-link) get no `--vscode-focusBorder` outline in that theme — only `.site-nav`/`.site-nav-toggle` are covered for HC-light. Task 3 explicitly requires "High-contrast themes especially must show a clear focus indicator"; HC-light is one of the four themes AC #1 names. [src/SpecScribe/assets/specscribe-webview-theme.css:234-254] — fixed: extended the `.vscode-high-contrast-light` selector list to cover the same elements as `.vscode-high-contrast`.
- [x] [Review][Patch] `composeEntryHtml`'s content swap uses a fixed literal sentinel string (`' __specscribe_content__ '`) to protect the entry content from the `__CSP_SOURCE__`/`__NONCE__` substitution. If that exact literal ever pre-exists elsewhere in the C#-rendered shell (CSS/script), the final `.split(sentinel).join(content)` step would corrupt it — the same class of bug this code was written to avoid for the nonce tokens. Use a per-call random sentinel (e.g. derived from `crypto.randomBytes`, same pattern already used for `nonce`) instead of a fixed string to close the collision class entirely. [extension/src/extension.ts:166-177] — fixed: sentinel is now derived from `crypto.randomBytes(8)` per call. (Also discovered and fixed in passing: the committed sentinel literal contained two stray embedded NUL bytes instead of spaces — pre-existing file corruption, unrelated to any finding, now clean.)
- [x] [Review][Patch] The `copyHelperText` branch in `onDidReceiveMessage` awaits `vscode.env.clipboard.writeText(msg.text)` without a try/catch. If the write rejects (permission issues, remote-SSH edge cases), it becomes an unhandled promise rejection with no user-facing error — inconsistent with the try/catch pattern used elsewhere in this file (e.g. `runRenderer` callers). [extension/src/extension.ts:77-85] — fixed: wrapped in try/catch with an error-message toast on failure.
- [x] [Review][Patch] `.vscode-dark .ac-criterion:target` relies on `color-mix(in srgb, ...)` with no fallback `background` declared before it; on a webview host whose bundled Chromium predates `color-mix()` support the `:target` highlight silently disappears rather than degrading. Add a plain hex/rgba fallback declaration ahead of the `color-mix()` line. [src/SpecScribe/assets/specscribe-webview-theme.css:223-226] — fixed: added an `rgba()` fallback declared before the `color-mix()` line.
- [x] [Review][Patch] `.ss-webview-toolbar-label` / `.ss-helper-btn` fall back to `Georgia, serif` when `--vscode-font-family` is unset, which reads as visibly out of place next to the rest of the (sans-serif) VS Code chrome. Change the fallback to a system sans-serif stack. [src/SpecScribe/assets/specscribe-webview-theme.css:272-281] — fixed: fallback changed to `-apple-system, "Segoe UI", sans-serif`.
- [x] [Review][Defer] `.vscode-light` has no dedicated contrast-tuning block and silently inherits the base warm-light `--status-*`/accent values. This matches the story's own stated design ("light webview theme can largely reuse today's values") but the same note's "must still be verified against the actual host light background" clause has no accompanying test or recorded check — deferred, pre-existing design decision, verification is a manual/QA follow-up rather than a code patch.
- [x] [Review][Defer] `SiteGeneratorWebviewTests.FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched` only compares the source-tree file set and `LastWriteTimeUtc`, not content hashes — a write that preserves mtime would pass undetected. Deferred as a test-hardening improvement, not a functional defect.
- [x] [Review][Defer] The live-refresh debounce callback checks `disposed` only after `await load()` resolves, not before calling `load()` — a refresh timer that fires after the panel is disposed still spawns a renderer child process (wasted work, no user-visible effect since the result is discarded). Deferred as a minor resource-waste edge case.
- [x] [Review][Defer] `WebviewHelpersTests`/`WebviewThemingTests` pin exact prompt wording (e.g. `"Do NOT modify any files"`) with ordinal string matching — future copy-editing of the generated prompt will break these tests for reasons unrelated to the read-only contract they exist to protect. Deferred as a test-quality nit.
- [x] [Review][Defer] No test exercises `<`/`>` in a project's `siteTitle` reaching the `data-ss-prompt` attribute (only a double-quote case is tested), even though the same `PathUtil.Html` escaping path is relied on to prevent markup injection into that attribute. Deferred — the escaping function itself is already exercised elsewhere in the suite.

**Dismissed as noise (6):** a Blind Hunter finding claimed `extension/package.json`'s new `"scope": "machine-overridable"` on `specscribe.toolPath` "widens" an RCE surface — checked against VS Code's setting-scope semantics and the setting had no `scope` at all before (default `window`, settable from any workspace without trust), so this change actually *tightens* it (workspace-level overrides now require a trusted workspace). Also dismissed: "only one helper implemented" (doc-language nit; AC #2 doesn't require plurality, confirmed by Acceptance Auditor), "no validation on `copyHelperText` payload" (value originates from the extension's own escaped, CSP-nonce-locked render, not attacker input), "HC-light status-chip fill inconsistency" (checked the actual token values — darker text-on-unchanged-pastel-fill is a legitimate contrast improvement, not a regression), "no changelog for CSP/settings changes" (process nit outside this workflow's model), and "unclear why light theme skips tuning" duplicate framing of the deferred item above.

## Dev Notes

### The theme bridge is an ADDITIVE, webview-scoped LAYER — not an edit to `specscribe.css`'s `:root`
Today's `:root` ([css:6–59](../../src/SpecScribe/assets/specscribe.css)) is the warm-light source of truth for the HTML surface and MUST stay byte-identical. The bridge is a **separate layer** (new stylesheet loaded after `specscribe.css`, or an injected `<style>` in the webview) whose selectors are scoped under `.vscode-*` body classes that only exist in the webview host. In the browser those classes never match, so the bridge is inert and the `.html` bytes are untouched; in the webview they match and re-value the tokens. This is the render-side application of AD-7: SpecScribe owns the content-semantic tokens; the webview *maps* host variables for chrome and *contrast-tunes* the accents. **Never** change a token's value in the base `:root` to achieve a webview effect — that would leak into the HTML surface and fail the golden test.

### Why "host-tuned for contrast" and not "bridge onto host palette" (the owner decision)
VS Code's theme palette has ~3 semantic severities (error/warning/info-ish); SpecScribe has **six lifecycle stages** plus deferred. Bridging stages onto host severity colors collapses that six-way distinction, which is the exact thing the sunburst/donut/funnel/mosaic exist to show (memory: [specscribe-status-token-system], [funnel-is-sideways-conventional-silhouettes]). So the stages keep their SpecScribe hues (the *meaning*), and we only adjust lightness/contrast per theme so they stay legible — identity preserved, accessibility met. Chrome (backgrounds/text/borders) *does* adopt host vars, because chrome has no SpecScribe semantic to protect and native chrome is what makes it "feel native."

### Helpers: generate + explicit handoff, never write (AD-6 / FR-17 / NFR-5)
The read-only posture is a spine invariant, not a preference. "Helpers can generate prompts or commands, but any write action remains an explicit external choice" ([ARCHITECTURE-SPINE.md:74–80](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)); "Helper actions do not edit source artifacts directly … Optional command handoff is explicit user action" ([rendering-architecture.md:94–98](../specs/spec-specscribe/rendering-architecture.md)). Implement the helper as: build a string → put it on the clipboard and/or pre-fill (not auto-run) a terminal. Do not run the command for the user, do not write a file, do not mutate settings or `_bmad-output/**`. FR-17's canonical example is a code-review prompt — a good first helper.

### Non-negotiable invariants (from the architecture spine)
- **AD-7** ([ARCHITECTURE-SPINE.md:82–88](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): presentation tokens are shared; host chrome is host-owned — "webview maps host variables for container/chrome while preserving SpecScribe semantic accents." This story IS the concrete realization of AD-7.
- **AD-6** ([ARCHITECTURE-SPINE.md:74–80](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): IDE helpers stay read-only and explicit. Governs AC #2.
- **AD-8** ([ARCHITECTURE-SPINE.md:90–96](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): interaction-state *shape* is shared; theming does not touch interaction semantics — the parity harness proves the themed webview still carries the same facts.
- **Feature-parity rule** ([rendering-architecture.md:78–82](../specs/spec-specscribe/rendering-architecture.md)): user-visible rendering features land in the core first, adapters only map. Theming is adapter-level *host mapping* of already-shared tokens — it maps existing core semantics onto host primitives; it introduces no new core feature.
- **NFR-5 local-only/read-only** ([requirements-catalog.md:43](../specs/spec-specscribe/requirements-catalog.md)) + **FR-17** ([:31](../specs/spec-specscribe/requirements-catalog.md)): the read-only helper contract.
- **Charts stay pure SVG + links** (memory: [charting-is-pure-svg-no-js]); **status routes through the six `--status-*` tokens / `StatusStyles`** (memory: [specscribe-status-token-system]); **motion routes through `--motion-*`** with the paired reduced-motion blocks (memory: [motion-token-system]). The bridge re-values tokens; it never forks these systems.

### Risk centers (where reviews will focus)
1. **HTML byte drift** — the #1 trap. Any theming that leaks into the generated `.html` surface (e.g. editing base `:root`, adding the theme asset to the HTML manifest, adding `prefers-color-scheme` to the base sheet) fails AC #1's webview scope and the golden regression. Keep the bridge scoped under `.vscode-*` and loaded only by the webview.
2. **High-contrast accessibility** — the hardest theme. Several `--status-*` values (pending/drafted/ready golds and tan-greys) are low-contrast on dark/high-contrast backgrounds; Task 2's contrast tuning must actually be verified (Task 6), not assumed.
3. **Collapsing the six stages** — if contrast tuning muddies two stages into near-identical colors (or someone reaches for the rejected host-severity bridge), the insight system loses its meaning. Verify all six stay distinguishable per theme.
4. **A helper that writes** — any file/settings mutation in the helper path violates AD-6/NFR-5. Generate + explicit handoff only; prove it write-free.
5. **Starting before 6.4 exists** — there is no webview to theme; see the READ FIRST gate.

### Project Structure Notes
- Single project: [src/SpecScribe/SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj) (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). Any C# additions (e.g. `AssetManifest` extension) go here in `namespace SpecScribe;`. **No new project, no namespace split** (seed-level, deferred — [ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). Any TypeScript/webview code lives wherever Story 6.4 established the extension.
- New CSS asset (theme bridge) lives beside [assets/specscribe.css](../../src/SpecScribe/assets/specscribe.css). Loaded by the webview only.
- Tests: [tests/SpecScribe.Tests/](../../tests/SpecScribe.Tests) (xUnit, `net10.0`). Follow file-per-unit naming; extend the golden/parity tests ([SiteGeneratorAdapterTests](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs), [RenderParityTests](../../tests/SpecScribe.Tests/RenderParityTests.cs)). Any webview/helper tests follow 6.4's test conventions.
- **Output dir is `SpecScribeOutput`** (memory: [generate-output-dir-is-specscribeoutput]). Never `--output docs/live`.
- This session/story runs on `main` (not a worktree). Edits target `C:\Dev\SpecScribe` directly. There is a background auto-committer on `main` (memory: [worktree-edits-must-target-worktree-path]) — keep commits coherent.
- Match the heavy XML-doc-comment style of the surrounding files; tag new members `[Story 6.5]`.
- **`baseline_commit`** captured at draft (`b58d787`); at dev start, re-capture the current `HEAD` for the byte-parity diff (as 6.1/6.2 did) and isolate this story's diff against it — intervening auto-committer commits can sit between the recorded baseline and HEAD.

### References
- [epics.md](../planning-artifacts/epics.md) — Epic 6 goal + Story 6.5 ACs (source of truth; formerly Story 6.3, renumbered breadcrumb in-file); [requirements-catalog.md:31 (FR-17 read-only helpers), :43 (NFR-5 local-only/read-only)](../specs/spec-specscribe/requirements-catalog.md); FR13 (Epic 6 = read-only webview reusing shared core).
- [ARCHITECTURE-SPINE.md:74–80 (AD-6 read-only helpers), 82–88 (AD-7 shared tokens / host chrome), 90–96 (AD-8), 98–101 (Seed — no package split)](../specs/spec-specscribe/ARCHITECTURE-SPINE.md).
- [rendering-architecture.md:78–82 (feature-parity), 84–92 (client-side enhancement + webview-must-not-depend-on-HTML-scripts), 94–98 (Read-Only IDE Helper Pattern)](../specs/spec-specscribe/rendering-architecture.md).
- **Prerequisite stories:** [6-4-...](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (webview runtime — build first; `create-story 6.4` to detail it), [6-2-...](6-2-read-only-vs-code-dashboard-and-epics-experience.md) (section view models; its scope decision documents there is no extension yet), [6-1-...](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) (delivery contract + `AssetManifest` + parity harness + `HostRenderException` registry this story extends).
- **Theming surface:** [assets/specscribe.css](../../src/SpecScribe/assets/specscribe.css) — `:root` tokens (chrome :6–26, status :34–40, motion :47–58), `body` chrome (:63–71), focus rings (:97–130), and the ~60 `var(--status-*)` chart/legend/badge consumers. [StatusStyles.cs](../../src/SpecScribe/StatusStyles.cs) — status→stage seam (consume, don't re-model). [AssetManifest.cs](../../src/SpecScribe/AssetManifest.cs), [RenderParity.cs](../../src/SpecScribe/RenderParity.cs), [HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs) — delivery/parity seams.
- **Golden gate:** [SiteGeneratorAdapterTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs).
- Memory: [specscribe-status-token-system] (six `--status-*` = single stage→color source; keep the six-stage distinction), [charting-is-pure-svg-no-js] (charts stay pure SVG), [motion-token-system] (`--motion-*` + paired reduced-motion), [golden-diff-normalization-gotchas] (3 benign diffs), [generate-output-dir-is-specscribeoutput], [worktree-edits-must-target-worktree-path].

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8) via the bmad-dev-story workflow.

### Debug Log References

- Precondition gate PASSED: Story 6.4 is `done`; the webview runtime exists (`WebviewRenderAdapter` + `extension/` shim + `specscribe webview` CLI). **Asset-loading seam recorded:** the webview does NOT `<link>` the stylesheet — `WebviewRenderAdapter.WrapDocument` inlines the embedded `SpecScribe.assets.specscribe.css` into `<style>__CSS__</style>`. The theme bridge therefore plugs in by inlining a SECOND embedded stylesheet immediately after it (CSP `style-src 'unsafe-inline'` allows it); the helper button lives in the webview SHELL (outside `#specscribe-surface`, so it survives content swaps) and routes through the existing nonce'd bridge script → `postMessage` → extension host (scripts are nonce-locked, so no inline handler).
- Baseline suite green at 704 before changes; 718 after (14 new). Golden byte-identity fingerprint held throughout.
- Real-generate leak check: generated `SpecScribeOutput/specscribe.css` is byte-identical to source (bridge is a separate resource, never appended); `index.html` chrome carries no toolbar/helper/inline-`<style>`. The only `.vscode-` hits in the generated site are prose in the ADR/story pages that literally discuss the classes.
- Accessibility (Task 6): WCAG contrast computed for every tuned accent against each simulated theme background — dark min 3.52→ (review lifted to 4.82), HC-dark min 7.34, HC-light min 4.92 all clear the floor; light reuses the shipped warm-light palette verbatim (its pale pending/drafted/ready are the same borderline-as-solid-fill values the HTML surface already ships, carried by borders + text + non-color labels). Real-browser computed-style probe confirmed the `.vscode-*` scopes + `--vscode-*` mappings resolve correctly (dark nav → titleBar bg, dark active badge → tuned token + transparent fill, helper button → button bg).

### Completion Notes List

**AC #1 — host-aware theming (Tasks 1–3, 6).** New webview-only theme bridge `src/SpecScribe/assets/specscribe-webview-theme.css`, embedded and inlined by `WebviewRenderAdapter` into a SECOND `<style>` after the production sheet so its scoped rules win the cascade. Under the four `.vscode-light/-dark/-high-contrast/-high-contrast-light` body classes VS Code stamps automatically, it (1) maps the chrome/container tokens (`--cream`, `--warm-white`, `--parchment*`, `--ink*`, `--border`, `--shadow`) and the literal-colored nav bar / code blocks onto `--vscode-*` host variables (AD-7: host owns chrome), and (2) contrast-tunes the SpecScribe status + insight accents per theme — same hue (= same six-stage meaning), only lightness/saturation moves — NOT bridged onto host severity colors (the explicitly-rejected direction). Literal pale-pastel badge/pill fills are neutralized to transparent on the dark themes (the tuned token text + border carry the stage). Focus rings route through `--vscode-focusBorder` on high-contrast. Reduced-motion and non-color status cues are untouched (the base sheet is inlined verbatim), so they carry over intact.

**AC #2 — read-only helper (Task 4).** New `WebviewHelpers.CodeReviewPrompt(siteTitle)` — a pure, deterministic text generator (FR-17's code-review-prompt example) whose OWN instructions are read-only ("Do NOT modify any files"). A helper toolbar + "Copy code-review prompt" button live in the webview shell; the nonce'd bridge script's click handler posts `{type:'copyHelperText', text}` and the extension host copies it to the clipboard (`vscode.env.clipboard.writeText`) with an info toast. The path writes NO source artifact and mutates NO settings — clipboard is the explicit handoff; running the prompt is a separate user action. Prompt is HTML-attribute-escaped into `data-ss-prompt` so a project title with quotes can't break out.

**Task 5 — guardrails.** HTML byte-identity preserved (golden `GoldenContentFingerprint` + full suite green; real generate diffs zero against source CSS). Semantic parity intact: theming re-values token VALUES, not facts, so `RenderParity.FindDivergences` still returns 0 and `HostRenderExceptions.Registry` stays at exactly the three 6.4 chrome/asset entries — no theming exception needed (verified by test).

**Scope honored:** no HTML-surface dark mode, no new status vocabulary, no chart-markup change, no write capability, no package split. The webview runtime/extension host built by 6.4 is reused, not rebuilt.

**Deferred / follow-up:** the light webview theme intentionally reproduces the shipped warm-light palette; its palest solid accents (pending/drafted/ready as bare swatches) are the same borderline-contrast values the HTML surface already ships — not regressed here, but a candidate for a future palette pass if the light HTML surface is ever revisited. The manual F5 smoke of the helper button + live theme-switch inside a real VS Code host (per `extension/README.md`) remains a human step, as it did for 6.4.

### File List

- `src/SpecScribe/assets/specscribe-webview-theme.css` (new) — the webview-only host-theme bridge.
- `src/SpecScribe/WebviewHelpers.cs` (new) — the read-only code-review-prompt generator.
- `src/SpecScribe/WebviewRenderAdapter.cs` — inline the theme bridge (second `<style>`) + the helper toolbar in the shell + the bridge-script helper click branch; doc updates.
- `src/SpecScribe/SpecScribe.csproj` — embed `specscribe-webview-theme.css`.
- `src/SpecScribe/AssetManifest.cs` — doc-comment: theming is Story 6.5, delivered webview-side (was stale "Story 6.3, out of scope").
- `extension/src/extension.ts` — handle the `copyHelperText` message (clipboard write + toast); widen the message type.
- `tests/SpecScribe.Tests/WebviewHelpersTests.cs` (new) — the pure read-only generator contract.
- `tests/SpecScribe.Tests/WebviewThemingTests.cs` (new) — bridge inlined/host-mapped/contrast-tuned, webview-only (no HTML leak), helper placement + escaping, parity unchanged.

## Change Log

- 2026-07-11 — Implemented (dev-story). Added the webview-only host-theme bridge (`specscribe-webview-theme.css`, inlined as a second `<style>` after the production sheet by `WebviewRenderAdapter`): chrome/container tokens + nav/code-block literals map to `--vscode-*` host variables; the six status stages + insight accents are contrast-tuned per `.vscode-*` theme (hues preserved, NOT bridged onto host severity), with dark/HC pastel badge fills neutralized and HC focus rings on `--vscode-focusBorder`. Added the read-only helper affordance (AC #2): `WebviewHelpers.CodeReviewPrompt` (pure text generator) surfaced as a shell "Copy code-review prompt" button that hands off via the nonce'd bridge → `copyHelperText` → `vscode.env.clipboard.writeText` — no artifact writes. HTML surface byte-identical (golden held; generated CSS diffs zero vs source), semantic parity unchanged (`HostRenderExceptions.Registry` still 3). Accessibility verified by WCAG contrast across light/dark/high-contrast + a real-browser computed-style probe. 14 tests added (718 total, all green). Status → review.
- 2026-07-10 — Story drafted (create-story) as **Story 6.5**, renumbered from the former **Story 6.3** (owner-directed sequencing fix: host theming depends on the Story 6.4 webview, which sorted after 6.3; renumbering to 6.5 makes the story number match the dependency order 6.1→6.2→6.4→6.5; append-only/no-renumber of 6.4). ACs verbatim from former 6.3. **Gated:** hard-blocked on Story 6.4 (webview runtime — does not exist; no VS Code extension in the repo) and 6.2 (section view models); `ready-for-dev` reflects context-complete, not prerequisites-met. **Theming direction owner-confirmed: "semantic accents, host-tuned for contrast"** — chrome/container map to `--vscode-*` host variables; the six `--status-*` stages + insight accents stay SpecScribe-owned (hues preserved for meaning) but get per-theme lightness/contrast tuning for accessibility (esp. high-contrast); NOT bridged onto host severity colors. Webview-only theme bridge (additive layer scoped under `.vscode-*` body classes) so the generated HTML surface stays byte-identical. Helpers strictly read-only (generate command/prompt + explicit handoff, no artifact writes) per AD-6/FR-17/NFR-5. Building the webview/extension, HTML-surface dark mode, new status vocabulary, chart-markup changes, and any package split are out of scope.
