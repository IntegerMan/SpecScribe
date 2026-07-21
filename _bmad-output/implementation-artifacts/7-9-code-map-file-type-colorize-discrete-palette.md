---
baseline_commit: b87a47eadf8ef499888f62d4bf0b8597ca3ea9a5
---

# Story 7.9: Code Map File-Type Colorize (Discrete Palette)

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reviewer exploring an unfamiliar codebase on the Code Map,
I want a colorize dimension that paints tiles by **file type / language** using a **discrete (categorical) color scheme**,
so that I can see at a glance where C#, TypeScript, CSS, config, and other kinds of mass live — without confusing that view with sequential churn/recency ramps.

## Acceptance Criteria

1.
**Given** the Code Map (Story 7.6) with its existing sequential git-metric colorize dimensions
**When** I choose a **File type** (or equivalent) colorize dimension
**Then** each file tile is filled from a **discrete palette** keyed by extension/language family (not a sequential ramp like change-frequency or recency)
**And** a legend lists each category with its swatch and a human label, and color is never the sole signal (path + type remain available as text / tooltip / table). [Source: epics.md#Story 7.9 (lines 1440-1452); FR14]

2.
**Given** unknown or rare extensions
**When** the dimension renders
**Then** they map to a documented "Other" (or similar) bucket rather than inventing unbounded colors
**And** the dimension degrades cleanly when the map has no files (existing empty/neutral path), and reduced-motion / a11y conventions from Story 7.6 are preserved. [Source: epics.md#Story 7.9 (lines 1454-1458); NFR2, NFR8]

3.
**Given** this dimension is categorical
**When** it is implemented
**Then** it does **not** change Story 10.6's coupling process-vs-code classifier (orthogonal concern) and does not require rewriting the sequential metric dimensions
**And** HTML + webview + SPA stay coherent on the shared code-map surface. [Source: epics.md#Story 7.9 (lines 1460-1464); NFR8]

---

## Developer Context

**Read this first — this is a classifier + rendering-dimension story on the existing Code Map, not a new page.** Story 7.6 built `code-map.html`: a pure-SVG squarified treemap (`CodeMap.Build`/`CodeMap.Layout`, rendered by `Charts.CodeTreemap`, page shell by `CodeMapTemplater`) with a "Colorize by" dropdown that today offers **six sequential (git-metric) dimensions** — change frequency (the server-baked default), recency, avg change size, churn, and files-changed-together — all painted on a shared 0–4 gold ramp (`.codemap-cell.level-0..4`, mirroring the commit heatmap's ramp discipline). Your job is to add a **seventh dimension that is fundamentally different in kind**: file type/language, which is **categorical, not ordinal** — there is no "more C# than TypeScript," so it cannot reuse the `level-N` ramp or the `Bucket()` quantizer. It needs its own discrete palette, its own legend shape, and its own client-side re-fill branch.

The one thing that makes this dimension special (and is why it's worth calling out up front, not left as an implicit design choice): **file type is not git-derived.** Every other dimension is `null`/"level-none" for a file with no git record, and the whole colorize control is *hidden* today when `hasMetrics` is false (a "git data unavailable" notice replaces it — see `CodeMapTemplater.AppendVariantPanel`). File type has no such dependency — it's a pure function of the path. Locking the new dimension behind the same `hasMetrics` gate would mean the one colorize dimension that *always* works stays invisible on the (common, no-`--deep-git`) baseline run. This story's design therefore makes file type available **independent of `hasMetrics`**, which is a genuine (if modest) restructuring of `AppendVariantPanel`'s gating — see "Owner-directed design decision" below.

### Owner-directed design decision (this call was made at create-story time — flag if you disagree, but don't silently deviate)

Because Story 7.9 was seated by owner directive post-retrospective (not elicited interactively at create-story — see sprint-status.yaml's 2026-07-18 note), the following implementation shape is a locked default chosen from the epics AC text + the existing architecture, not an owner-confirmed visual. Treat it as the plan; flag any conflict you find during implementation rather than reinterpreting silently.

- **File type becomes available regardless of `hasMetrics`.** Restructure `AppendVariantPanel`/`AppendColorizeControls` so the "Colorize by" dropdown renders whenever the variant has at least one file (it always does past the `IsEmpty` guard):
  - When `hasMetrics` is true: dropdown gets a **7th option**, "File type" (`value="filetype"`), appended after the existing six. The **baked-in server default stays change frequency**, exactly as today — AC #3's "does not require rewriting the sequential metric dimensions" means the default and the six existing options are untouched.
  - When `hasMetrics` is false: the dropdown still renders, but with **only** the "File type" option (the other six need git data they don't have) and it becomes the **baked-in server default** — so a no-`--deep-git` run's Code Map is colorized by file type out of the box instead of flat neutral. The existing "git change data is unavailable…" notice stays, but as a smaller supplementary line under the (now file-type) legend rather than a full replacement of the controls block.
- **This is the one place this story touches `hasMetrics` gating logic.** Everything else (the six sequential dimensions, their JS `metricFor`/`bucket` math, the git-unavailable notice's wording) is unchanged.

### What already exists (reuse / consume — do NOT rebuild)

- **The treemap model: `CodeMap`/`CodeMapNode` (`src/SpecScribe/CodeMap.cs`).** Pure, already computes per-file leaves with `RepoRelativePath` and (nullable) `Metrics`. This is where you add a **new, always-populated** field for the file-type classification — computed in `Build`/`BuildDir`'s file-leaf path, independent of the `gitMetrics` dictionary. [Source: [CodeMap.cs:13-19](../../src/SpecScribe/CodeMap.cs) (`CodeMapNode`), [CodeMap.cs:252-265](../../src/SpecScribe/CodeMap.cs) (`BuildChildren`, where file leaves are constructed)]
- **The existing extension-classification precedent: `CodeFileTemplater.LanguageClass` (Story 7.1).** A private per-extension switch mapping ~25 extensions to Prism grammar names (`language-csharp`, `language-typescript`, …) for syntax highlighting. **Do NOT reuse this directly** — it is a *fine-grained* per-language map (one bucket per language, unbounded-ish, `null` for "don't highlight"), not the *bounded, ~6-category* discrete palette this story needs (AC #1's "C#, TypeScript, CSS, config, and other kinds"). Write a **new, small, purpose-built classifier** (see Technical Requirements) — but keep its extension list internally consistent with `LanguageClass`'s (same `.ts`/`.tsx` → script family, same `.json`/`.yaml`/`.toml` → config family) so a file never reads as one language on its code page and a different family on the map. [Source: [CodeFileTemplater.cs:733-792](../../src/SpecScribe/CodeFileTemplater.cs)]
- **The sequential-ramp precedent to deliberately NOT follow: `Charts.Bucket`/`.codemap-cell.level-N`.** This is the pattern the discrete dimension must look *different from* — no `level-N` classes, no `Bucket()` quantization, no 0–4 gold ramp. [Source: [Charts.cs:2301-2315](../../src/SpecScribe/Charts.cs) (`Bucket`), [specscribe.css:3544-3550](../../src/SpecScribe/assets/specscribe.css) (`.codemap-cell.level-*`)]
- **The rect renderer to extend: `Charts.CodeTreemap`/`AppendTreemapFile`.** Emits one `<rect class="codemap-cell {levelClass} js-tip">` per file with `data-*` attributes for every git metric + a `data-tip-html` rich tooltip card. Add `data-filetype="<key>"` + `data-filetype-label="<human label>"` here — unconditionally, since classification never depends on git — and (per the design above) bake the initial `class` as `type-<key>` instead of `level-*`/`level-none` when `hasMetrics` is false. [Source: [Charts.cs:2209-2259](../../src/SpecScribe/Charts.cs)]
- **The tooltip card builder: `Charts.BuildTreemapCard`.** Add a "Type" row (the human label) so the rich tooltip carries the classification as text too (AC #1 "color is never the sole signal"). [Source: [Charts.cs:2268-2299](../../src/SpecScribe/Charts.cs)]
- **The dropdown + legend: `CodeMapTemplater.AppendColorizeControls`/`AppendLegend`.** `AppendColorizeControls` emits the `<select>` of six `<option>`s; `AppendLegend` emits the fixed "Less…More" ramp legend. Both need a discrete-vs-sequential branch: the sequential legend stays exactly as-is for the six ramp dimensions; file type needs its own legend shape (swatch **per category present** + its label, not a "Less…More" gradient caption). Both currently sit inside the `if (hasMetrics)` branch of `AppendVariantPanel` — that's the gating this story loosens (see design decision above). [Source: [CodeMapTemplater.cs:120-179](../../src/SpecScribe/CodeMapTemplater.cs)]
- **The text-equivalent table: `CodeMapTemplater.AppendFileTable`.** Add a "Type" column so the categorical value is always present as text, independent of which dimension is currently colorizing the SVG (AC #1's text equivalent). This column should appear for every variant (file type has no `hasMetrics` gate), not only when git metrics are present. [Source: [CodeMapTemplater.cs:190-242](../../src/SpecScribe/CodeMapTemplater.cs)]
- **The client-side re-fill: `specscribe.js`'s `recolor(dim)`/`metricFor(cell, dim)`/`DIM_LABELS`.** Add a `"filetype"` branch that is structurally different from the numeric dimensions — it reads `data-filetype`/`data-filetype-label` (strings, not numbers), sets a `type-<key>` class instead of a `level-N` class, and must **not** run the `bucket()`/min-max scan the numeric dimensions use. Critically: switching **between** a numeric dimension and file type (in either direction) must strip the *other* family's classes (`level-0..4`/`level-none` vs `type-*`) — today's loop only clears `level-*`, so add the symmetric clear. [Source: [specscribe.js:760-847](../../src/SpecScribe/assets/specscribe.js)]
- **The discrete/categorical CSS precedent already in this codebase.** The reference graph (Story 7.7/7.8) already distinguishes two *kinds* (citing artifact vs. related file) via shape + a non-ramp color pair, and `--status-unrecognized-hatch` already gives this project's "unknown bucket gets a distinct, honest treatment" precedent (a hatch/pattern, not an invented color). Mirror that spirit for the "Other" file-type bucket if a hatch is easy to add; a plain muted neutral swatch is an acceptable simpler fallback if hatching proves fiddly in SVG `<rect>` fills. [Source: [specscribe.css:44-63](../../src/SpecScribe/assets/specscribe.css) (`--status-unrecognized-hatch`), [[specscribe-status-token-system]]]

### The core design in one paragraph

Add a pure, bounded classifier — `CodeFileType.Classify(repoRelativePath)` returning `(string Key, string Label)` — living beside `CodeMap`'s other pure static helpers (`IsSpecDevPath`/`IsTestPath`). It buckets by extension into a **fixed ~6-category set** (C#, TypeScript/JavaScript, Styles, Markup & Docs, Config & Data, Other), with the Other bucket catching every unrecognized/rare extension (AC #2) — never inventing a new category per extension. Thread the classification onto every `CodeMapNode` file leaf in `CodeMap.Build` (always populated, no git dependency). `Charts.CodeTreemap` emits `data-filetype`/`data-filetype-label` on every file rect and adds a "Type" row to the tooltip card; when `hasMetrics` is false the baked default `class` becomes `type-<key>` instead of neutral. `CodeMapTemplater` adds "File type" as a colorize option — the 7th when git metrics exist, the *only* (and default) one when they don't — with its own discrete legend (swatch + label per category **present in this variant**) and a "Type" column in the always-shown text table. The JS enhancement's `recolor()` gets a `"filetype"` branch reading the string attributes and toggling `type-*` classes (clearing `level-*` and vice versa on every switch). New discrete CSS palette (`.codemap-cell.type-csharp` etc.) using **new, non-`--status-*`, non-ramp** color tokens so it reads as visually distinct from both the lifecycle vocabulary and the sequential gold ramp. Nothing about the six existing sequential dimensions, their math, or Story 10.6's `GitMetrics.ClassifyCoupling` changes (AC #3).

---

## Dependencies / Sequencing

- **Hard prerequisite — Story 7.6 (Source Code Treemap), done.** This story extends `CodeMap`, `Charts.CodeTreemap`, and `CodeMapTemplater` in place; read 7.6's story file for the full squarified-layout/variant/dimension-switch design before touching any of these three files. [Source: [7-6 story](7-6-source-code-treemap-for-codebase-exploration.md)]
- **Orthogonal, not a dependency — Story 10.6 (Insight & Chart Context Polish).** 10.6 added `GitMetrics.ClassifyCoupling` (a *pairwise* process-vs-code coupling heuristic used elsewhere for coupling badges/dashed edges) — AC #3 is explicit that this story must not touch it. If you find yourself editing `ClassifyCoupling` or its call sites, stop; that's a different classifier for a different axis (edge semantics, not per-file color). [[story-10-6-insight-chart-context-polish-review]]
- **No git/computation dependency.** Unlike 7.4/7.5/7.8, this story needs **no** `--deep-git` data, no new git call, and no change to `GitMetrics.TryComputeDeep`/`ParseNumstatLog`/`BuildCodeMapMetrics`. The classification is a pure string→category function over paths the generator already walks (`_codeFiles`, the same list `CodeMap.BuildVariants` already receives). [[deep-git-single-numstat-path]]
- **Concurrent Epic 7/10 work.** Epic 7 was reopened 2026-07-18 specifically to seat this story (7.1–7.8 are `done`; 7.10–7.12 are freshly seated `backlog` siblings, not yet started). No known in-flight collision on `CodeMap.cs`/`CodeMapTemplater.cs`/`Charts.cs`'s codemap section at the time this story was created — re-check `git status`/recent commits before starting in case that has changed. [[worktree-edits-must-target-worktree-path]]

### Scope boundary (read carefully)

- **IN scope:**
  - A new, bounded, pure file-type classifier (extension/name → category key + human label), living in `CodeMap.cs`.
  - Threading that classification onto every `CodeMapNode` file leaf (always populated).
  - A new "File type" colorize dimension: SVG rect fill (discrete palette, not the ramp), tooltip "Type" row, text-table "Type" column, its own legend (categories present + swatch + label).
  - Loosening the `hasMetrics` gate specifically so file type is reachable (and the baked default) even when git metrics are absent — the other six dimensions stay gated exactly as today.
  - Client-side `recolor()` branch for `"filetype"` + the symmetric class-clearing fix (`level-*` vs `type-*`) on every dimension switch.
  - New discrete CSS palette tokens/classes, both SVG cell fills and legend swatches, light theme (this site is single-theme parchment — see Story 7.8's precedent note) plus VS Code webview theme bridge if the code-map page is included in the webview surface (verify — see Technical Requirements).
- **OUT of scope:**
  - Touching `GitMetrics.ClassifyCoupling` (Story 10.6) or any pairwise coupling heuristic — different concern, different axis (AC #3).
  - Rewriting or restructuring the six existing sequential dimensions' math (`Bucket`, `metricFor`, the ramp CSS) — they must keep working byte-identically when selected.
  - Any new git call, fetch, or parse. This story is pure path→category classification.
  - A new page, new output path, or new nav entry — everything stays inside `code-map.html`.
  - Cyclomatic-complexity, dependency-graph, or any non-extension-based classification — file type here means "language/extension family," nothing deeper.
  - JavaScript for anything beyond the existing scoped enhancement pattern (dimension switch, drill zoom) — no new script files, no build step. [[charting-is-pure-svg-no-js]] (Note: unlike most SpecScribe charts, the Code Map already has a *scoped* JS enhancement for the dimension dropdown and zoom — this story extends that existing script, it doesn't introduce a new one.)

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Write a new, bounded classifier — do not extend `CodeFileTemplater.LanguageClass` in place or call it from `CodeMap`.** `LanguageClass` is `private` to `CodeFileTemplater`, returns Prism grammar names (fine-grained, ~25 buckets, `null` for "no highlight"), and lives in the wrong layer (a templater, not the pure model). Add a new pure static helper — e.g. `CodeFileType.Classify(string repoRelativePath) -> (string Key, string Label)` (or a small `CodeFileCategory` record) — in `CodeMap.cs`, following the existing `IsSpecDevPath`/`IsTestPath` pattern: normalize via `PathUtil.NormalizeSlashes`, pure, never throws, well-documented. Keep the extension groupings **consistent with** `LanguageClass`'s existing families (don't let `.ts` map to "script" here but a different grammar family there) without literally sharing code — a short doc comment cross-referencing `LanguageClass` is enough. [Source: [CodeMap.cs:107-131](../../src/SpecScribe/CodeMap.cs), [CodeFileTemplater.cs:737-792](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Bound the category set to ~6, with "Other" as the deterministic catch-all (AC #2).** A reasonable starting set (seed values, your call to refine, but keep it small and documented): C# (`cs`,`csx`) · TypeScript/JavaScript (`ts`,`tsx`,`js`,`jsx`,`mjs`,`cjs`) · Styles (`css`,`scss`) · Markup & Docs (`html`,`htm`,`md`,`markdown`,`xml`,`svg`,`xaml`,`csproj`,`props`,`targets`) · Config & Data (`json`,`json5`,`yaml`,`yml`,`toml`,`ini`) · Other (everything else, including extensionless files and rare extensions). Never grow this list per-extension at render time — every unrecognized extension falls into the one "Other" bucket, so the palette stays a fixed, finite size no matter the repo. [AC #2]
- **Attach classification to every `CodeMapNode` file leaf, unconditionally.** Extend the `FileLeaf`/`CodeMapNode` shape (or add a parallel field) so `Build` computes `(Key, Label)` for every file regardless of whether `gitMetrics` has an entry — this is the load-bearing difference from `Metrics` (nullable, git-dependent). Directories don't need a category (their tiles are unlabeled boundaries, unchanged). [Source: [CodeMap.cs:13-19,144-199,252-265](../../src/SpecScribe/CodeMap.cs)]
- **Emit `data-filetype`/`data-filetype-label` on every file `<rect>`, independent of `hasMetrics`.** This is the one `data-*` pair that's always present (contrast with `data-changes`/`data-churn`/etc., which are conditional on `metrics is not null`). [Source: [Charts.cs:2209-2229](../../src/SpecScribe/Charts.cs)]
- **Bake the file-type fill as the SSR default when `hasMetrics` is false; keep change-frequency as the SSR default when `hasMetrics` is true.** This is the one behavior change to existing bytes when git metrics ARE present — none; when they're absent, today's `level-none` fill becomes `type-<key>`. Update `CodeTreemap`'s signature/call site accordingly (an added parameter or an internal computation — your call), keeping `hasMetrics=true` output otherwise unchanged. [Owner-directed design decision above]
- **Add "Type" to the tooltip card and the text table, for every file, in every variant.** The tooltip (`BuildTreemapCard`) and the table (`AppendFileTable`) both currently branch on `hasMetrics` for their metric rows/columns — the new "Type" row/column should NOT be inside that branch; it's always available. [Source: [Charts.cs:2268-2299](../../src/SpecScribe/Charts.cs), [CodeMapTemplater.cs:190-242](../../src/SpecScribe/CodeMapTemplater.cs)]
- **Restructure `AppendVariantPanel`'s gating precisely as scoped:** dropdown+legend render whenever the variant has files (not only when `hasMetrics`); when `!hasMetrics`, the dropdown offers only "File type" and the "git data unavailable" notice becomes a secondary, smaller note (still honest — the six git-derived dimensions really are unavailable) rather than replacing the whole controls block. [Source: [CodeMapTemplater.cs:100-138](../../src/SpecScribe/CodeMapTemplater.cs)]
- **Build a discrete legend, not a ramp legend, for file type.** `AppendLegend`'s "Less…More" gradient caption is wrong for a categorical dimension — add a sibling legend renderer that lists **only the categories present in this variant's file set**, each a swatch + its human label (e.g. "🟦 C# · 🟨 Styles · ⬜ Other"), analogous to how `RequirementSatisfactionChips`/status legends pair swatch+word (never color-only). Swap between the two legend shapes based on which dimension is server-baked as default; the JS `recolor()` branch should also swap the legend's visible content/shape when the user picks "File type" from the dropdown (mirroring how it already rewrites `.codemap-legend-dim`'s text on every switch) — decide whether a full legend-shape swap needs JS DOM surgery or whether pre-rendering both legend shapes (one hidden) and toggling visibility is simpler/more robust; either is acceptable as long as the visible legend always matches the active dimension. [Source: [CodeMapTemplater.cs:167-179](../../src/SpecScribe/CodeMapTemplater.cs)]
- **New CSS palette: distinct from BOTH the `--status-*` lifecycle tokens AND the `level-0..4` gold ramp.** Add new custom properties or reuse this project's existing *non-status* accent colors (`--rust`/`--rust-light`, `--moss`/`--moss-light`, `--teal`/`--teal-deep`, `--gold`/`--gold-light`, `--ink-light`) for the ~5 real categories, and a muted/hatched neutral for "Other" (mirroring `--status-unrecognized-hatch`'s "unknown gets an honest distinct treatment" precedent — a simple muted swatch is fine if a true SVG hatch pattern proves fiddly). Never repurpose a `--status-*` token here — file type is not a lifecycle signal. [[specscribe-status-token-system]]
- **`recolor(dim)` needs a structurally different branch for `"filetype"`.** No `bucket()`/min-max scan (categorical, not scaled); read `data-filetype`/`data-filetype-label` directly; set `type-<key>` classes. **Critically: before applying either family of classes, strip the OTHER family too** — today's loop only removes `level-0..4`/`level-none`; add removal of any `type-*` class when a numeric dimension is chosen, and removal of `level-*` when file type is chosen, or a cell that was last colorized by file type will carry a stale `type-*` class forever after switching to (say) churn. [Source: [specscribe.js:808-839](../../src/SpecScribe/assets/specscribe.js)]
- **Verify webview/SPA coherence (AC #3).** `code-map.html` is a synthesized page (`WriteOutput`, not `IRenderAdapter`) — confirm whether it's captured into the SPA's `<main id="main-content">` consolidation and/or excluded from the VS Code webview (mirror Story 7.8's finding that code pages are webview-excluded; check whether Code Map is treated the same or differently) and note the answer in Dev Agent Record. If it IS captured for SPA/webview, the new markup/CSS must render correctly there too (no new `HostRenderException`, no parity exception needed if it was already synthesized-and-excluded). [Source: [SiteGenerator.cs:2768-2782](../../src/SpecScribe/SiteGenerator.cs)]
- **Escape everything.** Category labels are static/trusted (your own bounded list), but paths and any derived text remain `PathUtil.Html`-escaped exactly as today. [Source: [Charts.cs:2204-2258](../../src/SpecScribe/Charts.cs)]
- **Reduced-motion / a11y conventions from 7.6 stay untouched.** No new animation is introduced by a color-only dimension switch; the existing zoom-tween `--motion-fast` read is unaffected. [[motion-token-system]]

### DON'T

- **DON'T touch `GitMetrics.ClassifyCoupling` or any Story 10.6 coupling code.** Orthogonal concern (AC #3). [[story-10-6-insight-chart-context-polish-review]]
- **DON'T rewrite `Bucket()`, `metricFor()`, or any of the six existing sequential dimensions' behavior.** Selecting "Change frequency" (or any of the other five) must produce identical output to today. (AC #3)
- **DON'T let "Other" become an excuse to add unbounded per-extension categories later in this story** — the whole point of AC #2 is a fixed, documented, small set. If you're tempted to add a 7th/8th real category, stop and confirm that's actually warranted rather than scope-creeping the palette.
- **DON'T reuse `--status-*` tokens for file-type swatches.** [[specscribe-status-token-system]]
- **DON'T add a new git call, fetch, or parse.** Classification is pure path→category. [[deep-git-single-numstat-path]]
- **DON'T add a new script file or build step.** Extend the existing scoped `<script>` enhancement in `specscribe.js`'s codemap section. [[charting-is-pure-svg-no-js]]
- **DON'T force the Core/Adapters package split** — extend `CodeMap`/`Charts`/`CodeMapTemplater`/`specscribe.js`/`specscribe.css` in place. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **DON'T use `--output docs/live`** in any manual-verify step. [[generate-output-dir-is-specscribeoutput]]

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **NFR-2 — never throw.** The classifier is a pure, total function over any string path (including empty/malformed segments); falls back to "Other" rather than throwing or returning null. Mirrors `IsSpecDevPath`/`IsTestPath`'s never-throw discipline.
- **NFR-8 — graceful degradation.** Empty map → existing `IsEmpty`/"No files match this filter" path, unchanged (AC #2). No-git-metrics variant now gets a *working* colorize dimension (file type) instead of a fully inert control — an *improvement* on the existing degrade path, not a regression of it.
- **Local-only, read-only.** No new reads or writes; classification is computed over the already-walked `_codeFiles` list. [Inherited Invariants]
- **Seed, not invariant.** Extend `CodeMap`/`Charts`/`CodeMapTemplater` in place; no new package/class hierarchy for "colorize dimensions" as a concept. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **Status token discipline (NFR-6 adjacent).** File type is explicitly NOT a lifecycle signal — must not route through `--status-*`/`StatusStyles`. [[specscribe-status-token-system]]
- **Pure-SVG / scoped-JS-enhancement.** The Code Map's dimension dropdown is this codebase's one precedent for a *legitimate* small scoped script (distinct from the "charts are pure SVG, no JS" default for chart *rendering* itself) — extend it, don't add a second enhancement mechanism. [[charting-is-pure-svg-no-js]]

### Delivery-adapter note (does this touch webview/SPA parity?)

`code-map.html` is generated via `SiteGenerator`'s direct `WriteOutput(SiteNav.CodeMapOutputPath, ...)` call (like code pages, commit-day pages, etc.) — **not** routed through `HtmlRenderAdapter.RenderPage`/the `IRenderAdapter` view-model path. So this story does not add a `HostRenderException` or a `RenderParity` registry exception by construction. It *does* change `specscribe.css`/`specscribe.js` bytes and the code-map page HTML, so the **golden content fingerprint will shift** — regenerate it with the standard normalizations. **Verify (don't assume) whether `code-map.html` is captured by the SPA adapter's whole-site `<main id="main-content">` consolidation and whether it's included in or excluded from the VS Code webview surface** (Story 7.8 found code *pages* are webview-excluded; Code Map may or may not follow the same rule — check `SiteNav`/the webview page allowlist and record the finding). [[golden-diff-normalization-gotchas]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.**
- **No new git library, no git call.** [[deep-git-single-numstat-path]]
- **Existing infra to reuse (do not reinvent):**
  - [CodeMap.cs:107-131](../../src/SpecScribe/CodeMap.cs) — `IsSpecDevPath`/`IsTestPath` as the pure-classifier pattern to follow for the new file-type classifier.
  - [CodeFileTemplater.cs:737-792](../../src/SpecScribe/CodeFileTemplater.cs) — `LanguageClass`'s extension groupings, for consistency reference only (do not call it from `CodeMap`).
  - [Charts.cs:2154-2315](../../src/SpecScribe/Charts.cs) — `CodeTreemap`/`AppendTreemapFile`/`BuildTreemapCard`/`Bucket` to extend.
  - [CodeMapTemplater.cs](../../src/SpecScribe/CodeMapTemplater.cs) — `AppendVariantPanel`/`AppendColorizeControls`/`AppendLegend`/`AppendFileTable` to extend.
  - [specscribe.js:730-900ish](../../src/SpecScribe/assets/specscribe.js) — the codemap panel's scoped enhancement script (`recolor`/`metricFor`/`DIM_LABELS`) to extend.
  - [specscribe.css:3540-3620](../../src/SpecScribe/assets/specscribe.css) — `.codemap-cell`/`.codemap-legend` rules to extend with the new discrete classes.
  - [PathUtil.cs](../../src/SpecScribe/PathUtil.cs) — `Html`/`NormalizeSlashes`.

---

## File Structure Requirements

**New files:**

- *(None required.)* A new classifier is a small addition to `CodeMap.cs` (following the file's existing pure-helper pattern), not a new file — unless it grows large enough that a private nested type or a short partial file is genuinely cleaner; your call, but the default is "add to `CodeMap.cs`."

**Modified files (read fully before editing):**

- `src/SpecScribe/CodeMap.cs` — new pure classifier (`CodeFileType.Classify` or equivalent); thread its result onto every `CodeMapNode` file leaf in `Build`/`BuildChildren`.
- `src/SpecScribe/Charts.cs` — `CodeTreemap`/`AppendTreemapFile`: emit `data-filetype`/`data-filetype-label`; bake `type-<key>` as the default fill when `!hasMetrics`; `BuildTreemapCard`: add a "Type" row.
- `src/SpecScribe/CodeMapTemplater.cs` — `AppendVariantPanel`: loosen the `hasMetrics` gate for the dropdown/legend; `AppendColorizeControls`: add the "File type" option (7th when metrics exist, sole option otherwise); new discrete-legend renderer alongside `AppendLegend`; `AppendFileTable`: add an always-present "Type" column.
- `src/SpecScribe/assets/specscribe.js` — the codemap panel script: `DIM_LABELS` gets `filetype`; new `recolor()` branch for the string-attribute, non-scaled file-type dimension; symmetric `level-*`/`type-*` class clearing on every switch.
- `src/SpecScribe/assets/specscribe.css` — new `.codemap-cell.type-*`/`.codemap-legend-swatch.type-*` discrete palette classes (new tokens, not `--status-*`, not the gold ramp); "Other" bucket's distinct muted/hatched treatment.

**Output layout:** No new paths. Everything renders inside the existing `code-map.html`.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Model tests are pure (no IO, following `CodeMapTests`'s pattern); templater/chart tests call render methods directly with synthetic inputs; generation-level tests use the temp-`_bmad-output`-tree pattern. [Source: [CodeMapTests.cs](../../tests/SpecScribe.Tests/CodeMapTests.cs), [CodeMapTemplaterTests.cs](../../tests/SpecScribe.Tests/CodeMapTemplaterTests.cs), [SiteGeneratorCodeMapTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs)]

**Classifier unit tests (new, pure — extend `CodeMapTests.cs` or a sibling file):**
- Each seeded category's representative extensions classify correctly (`.cs`→C#, `.ts`/`.tsx`/`.js`→script family, `.css`→styles, `.md`/`.html`→markup, `.json`/`.yaml`/`.toml`→config).
- An unrecognized/rare extension (and an extensionless filename) classifies to "Other" — never a new/invented category.
- Classification is independent of git metrics: build a `CodeMap` with `NoMetrics` and confirm every file leaf still carries a non-empty type key/label.
- Deterministic: same path always classifies the same; case-insensitive extension matching (`.CS` == `.cs`).
- Never throws on a pathological path (empty segment, no extension, trailing dot).

**`Charts.CodeTreemap`/`AppendTreemapFile` tests (extend `ChartsTests.cs` or a codemap-specific test file):**
- File rects always carry `data-filetype`/`data-filetype-label`, regardless of `hasMetrics`.
- When `hasMetrics` is false, the baked default class is `type-<key>` (not `level-none`) — this is a deliberate behavior change from pre-7.9; assert it explicitly.
- When `hasMetrics` is true, the baked default class is still `level-<n>`/`level-none` (change frequency) — unchanged from Story 7.6 (AC #3 regression guard).
- The tooltip card includes a "Type" row with the human label, for every file.
- Escaping: a synthetic path with `<`/`&`/`"` still escapes correctly with the new attributes present.

**`CodeMapTemplaterTests` (extend):**
- `hasMetrics=false` variant: the colorize dropdown renders with only the "File type" option; the legend is the discrete (category+swatch) shape, not the "Less…More" ramp; the old full-replacement "git data unavailable" notice becomes a secondary note (assert the notice text is still present somewhere, and the controls are no longer hidden/absent).
- `hasMetrics=true` variant: the dropdown has 7 options total (six existing values unchanged + `"filetype"`/"File type"); the legend is still the ramp shape by default (AC #3: sequential default unchanged).
- The discrete legend lists only categories actually present in the given file set (not every possible category) — synthesize a file set with just two categories and assert only two swatches render.
- The "Type" column in `AppendFileTable` is present and populated for every row, in both `hasMetrics` states.

**Generation-level tests (extend `SiteGeneratorCodeMapTests.cs`):**
- A repo with mixed file types + `--deep-git` off → `code-map.html` renders with the file-type dimension as default, a discrete legend, and no error/skip outcome.
- The same repo with `--deep-git` on → `code-map.html`'s default dimension is unchanged (still change frequency); "File type" is present as a selectable 7th option.
- Determinism: two runs over the same repo produce identical `code-map.html` output.
- Verify (and assert, once you've read the code) whether `code-map.html` is included in the SPA/webview capture surfaces, adding or updating a coherence assertion accordingly (AC #3's "HTML + webview + SPA stay coherent").

**Golden fingerprint:** CSS/JS + `code-map.html` bytes change → regenerate the golden content fingerprint (`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint…`) with the standard normalizations. [[golden-diff-normalization-gotchas]]

**Run:** `dotnet test` from repo root. Then two real passes against this repo (default `SpecScribeOutput/`; **do not** pass `--output docs/live`):
1. **Baseline:** `dotnet run --project src/SpecScribe` (no `--deep-git`) → open `code-map.html` → confirm tiles are colorized by file type out of the box (not flat neutral), the legend lists this repo's actual categories (C#, TS/JS if any, CSS, Markdown, etc.) with swatches, the table has a populated "Type" column, and the "git data unavailable" note is present but secondary.
2. **Deep:** `dotnet run --project src/SpecScribe --deep-git` → open `code-map.html` → confirm the default is unchanged (change frequency ramp), "File type" appears as a 7th dropdown option, selecting it swaps to the discrete palette + discrete legend with no leftover `level-*` styling on any cell, and switching back to a sequential dimension leaves no leftover `type-*` styling.

---

## Dev Notes

- **This is a classification + rendering-dimension story — no git work.** If you find yourself touching `GitMetrics.cs` or adding a git call, stop. [[deep-git-single-numstat-path]]
- **File type is categorical; the existing six dimensions are sequential — don't blur the two.** No `level-N`/`Bucket()` reuse for file type; no ramp legend for it. This is the story's central design tension (AC #1 + AC #3 together) and the most likely place to accidentally regress the existing dimensions if you're not careful about which code path you're editing.
- **The `hasMetrics` gating change is intentional and scoped.** File type is the one dimension that doesn't need `--deep-git`; loosening the gate specifically for it (while leaving the other six exactly as gated today) is the locked design — see "Owner-directed design decision" above. This is the one piece of this story most likely to need owner confirmation after the fact if the resulting UX reads oddly; flag it in Completion Notes either way.
- **Bounded palette, honest "Other."** Never let per-extension category proliferation creep in — a fixed ~6-bucket set, documented, with "Other" as the deterministic catch-all (AC #2). [[specscribe-status-token-system]] (the *discipline* of routing color through a documented, bounded vocabulary — not the tokens themselves, which must be new/non-status here)
- **JS symmetry bug to avoid:** the existing `recolor()` loop only clears `level-*` classes; extend it to also clear `type-*` (and vice versa) on every dimension switch, or cells will accumulate stale classes across repeated switches.
- **No JS beyond the existing scoped enhancement.** [[charting-is-pure-svg-no-js]]

### Project Structure Notes

- All changes extend existing renderers/model: `CodeMap`, `Charts` (codemap section), `CodeMapTemplater`, the codemap panel section of `specscribe.js`, `specscribe.css`. No new production file, no new output path, no package restructure (deferred seed, Epics 4/6).
- `code-map.html` is synthesized (not `IRenderAdapter`-routed) → no new `HostRenderException`/RenderParity registry exception expected; only the golden content fingerprint moves. Confirm SPA/webview inclusion as part of Task work (AC #3) rather than assuming.

### References

- [Source: [epics.md:1440-1464](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.9 user story + all three ACs.
- [Source: [epics.md:1437-1439](../../_bmad-output/planning-artifacts/epics.md)] — seating note: orthogonal to Story 10.6's coupling heuristic.
- [Source: [src/SpecScribe/CodeMap.cs](../../src/SpecScribe/CodeMap.cs)] — the treemap model to extend with the new classifier + node field.
- [Source: [src/SpecScribe/Charts.cs:2154-2315](../../src/SpecScribe/Charts.cs)] — `CodeTreemap`/`AppendTreemapFile`/`BuildTreemapCard`/`Bucket`.
- [Source: [src/SpecScribe/CodeMapTemplater.cs](../../src/SpecScribe/CodeMapTemplater.cs)] — page shell, dropdown, legend, text table.
- [Source: [src/SpecScribe/assets/specscribe.js:730-900](../../src/SpecScribe/assets/specscribe.js)] — codemap panel scoped enhancement script.
- [Source: [src/SpecScribe/assets/specscribe.css:3540-3620](../../src/SpecScribe/assets/specscribe.css)] — `.codemap-cell`/`.codemap-legend` rules.
- [Source: [src/SpecScribe/CodeFileTemplater.cs:733-792](../../src/SpecScribe/CodeFileTemplater.cs)] — `LanguageClass`, the fine-grained precedent NOT to reuse directly.
- [Source: [7-6-source-code-treemap-for-codebase-exploration.md](7-6-source-code-treemap-for-codebase-exploration.md)] — the Code Map's full design (model, layout, variants, dimension switch) this story extends.
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (NFR2/NFR8, local/read-only, graceful degradation, seed-not-invariant).
- Project memory: [[deep-git-single-numstat-path]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[golden-diff-normalization-gotchas]], [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]], [[story-10-6-insight-chart-context-polish-review]].

---

## Tasks / Subtasks

- [x] **Task 1 — New bounded file-type classifier (AC: #1, #2)**
  - [x] Add a pure `CodeFileType.Classify(string repoRelativePath) -> (string Key, string Label)` (or equivalent record) to `CodeMap.cs`, following the `IsSpecDevPath`/`IsTestPath` pattern: normalized, case-insensitive extension matching, never throws.
  - [x] Bounded ~6-category set (C# · TypeScript/JavaScript · Styles · Markup & Docs · Config & Data · Other), "Other" as the deterministic catch-all for anything unrecognized or extensionless.
  - [x] Thread the classification onto every `CodeMapNode` file leaf in `Build`/`BuildChildren`, always populated (no git dependency).
- [x] **Task 2 — Render the discrete dimension in the SVG + tooltip + table (AC: #1, #2)**
  - [x] `Charts.CodeTreemap`/`AppendTreemapFile`: emit `data-filetype`/`data-filetype-label` on every file rect, unconditionally.
  - [x] Bake `type-<key>` as the default fill class when `hasMetrics` is false (replacing the current flat `level-none`); keep `hasMetrics=true`'s default (change frequency) byte-identical.
  - [x] `BuildTreemapCard`: add a "Type" row (human label) for every file.
  - [x] `AppendFileTable`: add an always-present "Type" column.
- [x] **Task 3 — Colorize dropdown + discrete legend (AC: #1, #3)**
  - [x] `AppendVariantPanel`/`AppendColorizeControls`: loosen the `hasMetrics` gate — dropdown renders whenever the variant has files; `hasMetrics=true` gets a 7th "File type" option (existing six unchanged, unchanged default); `hasMetrics=false` gets only "File type" (and it's the default).
  - [x] New discrete-legend renderer: swatch + human label per category **present in this variant** (not every possible category); keep the existing ramp legend for the sequential default unchanged.
  - [x] Demote the "git data unavailable" notice to a secondary note when `hasMetrics` is false (controls are no longer fully hidden).
- [x] **Task 4 — Client-side dimension switch (AC: #1, #3)**
  - [x] `specscribe.js`: `DIM_LABELS` gets `filetype: "file type"`; new `recolor()` branch reading `data-filetype`/`data-filetype-label` (no `bucket()`/min-max scan), setting `type-<key>` classes.
  - [x] Fix class-clearing symmetry: strip `type-*` when applying a numeric dimension, strip `level-*`/`level-none` when applying file type.
  - [x] Legend content swap on dimension change (swap to/from the discrete legend shape) — pick and document the mechanism (DOM rewrite vs. pre-rendered/toggled shapes).
- [x] **Task 5 — CSS: new discrete palette (AC: #1, #2)**
  - [x] `.codemap-cell.type-*` + `.codemap-legend-swatch.type-*` for each real category, using new/existing non-`--status-*`, non-ramp tokens.
  - [x] Distinct, honest "Other" treatment (muted/hatched, not an arbitrary 6th hue competing with the real categories).
  - [x] Verify legibility against the existing gold ramp (no confusable overlap when both dimensions' colors could appear near each other in review).
- [x] **Task 6 — AC #3 coherence check (AC: #3)**
  - [x] Confirm `GitMetrics.ClassifyCoupling`/Story 10.6 code is untouched (grep diff before finishing).
  - [x] Confirm the six sequential dimensions still produce byte-identical output to pre-7.9 for the same inputs.
  - [x] Determine and record whether `code-map.html` is captured by the SPA consolidation and/or included in the VS Code webview surface; ensure the new markup/CSS/JS is coherent there if so.
- [x] **Task 7 — Tests (AC: #1, #2, #3)**
  - [x] Classifier unit tests (category coverage, Other fallback, no-git independence, determinism, never-throws).
  - [x] `Charts`/`CodeTreemap` tests (data attrs always present, baked-default behavior split by `hasMetrics`, tooltip Type row, escaping).
  - [x] `CodeMapTemplaterTests` (dropdown option counts per `hasMetrics` state, discrete legend content, table Type column).
  - [x] Generation-level tests (`SiteGeneratorCodeMapTests`): baseline default-is-filetype, deep-git default-unchanged, determinism, SPA/webview coherence per Task 6's finding.
  - [x] Regenerate the golden content fingerprint; confirm inventory unaffected.
- [x] **Task 8 — Full generation pass + manual verify (AC: #1, #2, #3)**
  - [x] `dotnet test` green.
  - [x] Baseline generate (no `--deep-git`) → file-type colorized by default, discrete legend, Type column populated.
  - [x] Deep generate (`--deep-git`) → default unchanged, File type selectable as 7th option, clean switch both directions (no stale classes), sequential dimensions unchanged.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet build src/SpecScribe/SpecScribe.csproj` — clean, 0 warnings/errors, at each checkpoint.
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` — full suite green at 1874/1874 after the final pass (2 pre-existing failures observed mid-session — `SiteGeneratorAdapterTests` golden fingerprint, expected/regenerated, and `MarkdownConverterTests.SCRATCH_DiagnoseTildeBug` — both traced to a **concurrent session actively editing the same shared `main` working tree** (uncommitted `MarkdownConverter.cs` tilde-parsing fix + `sprint-status.yaml` Epic 13/14/15 create-story entries); confirmed via `git status`/`git diff` that neither belongs to this story, left both untouched, and the scratch test resolved itself once the concurrent session's fix landed in the shared working tree — no action taken on my part).
- Golden content fingerprint regenerated after 2 repeated runs confirmed stability: `a7787135415317347850153750453ddc657adb1defd6515412337de5c22e6b35`.
- Manual verify: `dotnet run --project src/SpecScribe -- generate --output <tmp>` (no `--deep-git`) confirmed file-type is the baked default (all 6 categories present, discrete legend visible, ramp legend hidden, secondary notice, Type column) and again with `--deep-git` confirmed the sequential default is unchanged (`value="changes" selected`, ramp legend visible, discrete legend pre-rendered hidden, `filetype` a 7th unselected option, `level-*` classes still baked, zero stray secondary notices).

### Completion Notes List

- New `CodeFileCategory` record + `CodeFileType` static classifier (6 bounded categories: C#, TypeScript/JavaScript, Styles, Markup & Docs, Config & Data, Other) added to `CodeMap.cs`, following the file's `IsSpecDevPath`/`IsTestPath` pure-helper discipline. `CodeMapNode` gained a `Category` field (always populated for files, always `null` for directories) — a positional-record parameter addition, so both existing construction sites (`BuildChildren`, `BuildDir`) were updated in place.
- `Charts.CodeTreemap`/`AppendTreemapFile` now emit `data-filetype`/`data-filetype-label` unconditionally and bake `type-<key>` as the default fill class when `hasMetrics` is false (replacing the old flat `level-none`); the `hasMetrics: true` path is byte-identical to pre-7.9 (AC #3 regression guard, test-covered). `BuildTreemapCard` gained an always-present "Type" row.
- `CodeMapTemplater.AppendVariantPanel`'s gating was loosened exactly as scoped: the colorize dropdown + BOTH legend shapes now render whenever the variant has files; when `hasMetrics` is false the dropdown offers only "File type" (the baked default), the discrete legend ships visible and the ramp legend ships pre-rendered `hidden`, and the "git data unavailable" notice is demoted to `.codemap-notice-secondary`. When `hasMetrics` is true, nothing about the six existing options/default changed — "File type" is purely appended as a 7th option, and the ramp legend stays visible with the discrete legend pre-rendered hidden. `AppendFileTable` gained an always-present "Type" column.
- Chose the **pre-render-both-legends-and-toggle-visibility** design (the story's suggested simpler alternative to JS DOM surgery): both legend shapes are static once rendered — the discrete legend's category list never changes at runtime — so `specscribe.js`'s `swapLegend()` only flips two `hidden` booleans, never rewrites content.
- `recolor()` gained a structurally separate `"filetype"` branch (no `bucket()`/min-max scan — categorical, not scaled) and a shared `clearFillClasses()` helper that strips BOTH `level-*` and `type-*` before applying either family, fixing the class-clearing symmetry bug called out in the story (previously only `level-*` was cleared, so a cell colorized by file type would carry a stale `type-*` class forever after switching to a numeric dimension).
- New CSS palette avoids the gold-ramp hues entirely (uses `--teal-deep`/`--teal`/`--rust`/`--rust-light`/`--moss` for the 5 real categories, muted `--parchment-dark` + `stroke-dasharray` for "Other") so the categorical dimension can never be visually confused with the sequential ramp even side by side. No new custom properties were needed — all existing non-`--status-*` accent tokens.
- **AC #3 coherence (Task 6) confirmed by direct code reading, not assumption**: `GitMetrics.cs`/`ClassifyCoupling` has zero diff (`git diff --stat` confirms). `code-map.html` IS captured for SPA/webview — `SiteGenerator.cs`'s deliberate exclusion set (owner decision 2026-07-12) only covers code *pages*, commit-day pages, and `commit/` detail pages (things that scale with the target repo's git history), not the Code Map itself — added `SiteGeneratorWebviewTests.CapturePages_IncludesCodeMapAsACapturedSurface` to make this explicit and prove the new markup renders coherently there (no `<script>` in captured regions, same as every other captured surface — the JS enhancement simply doesn't run there, matching the page's existing pure-SVG-with-progressive-enhancement design).
- The **owner-directed `hasMetrics` gating change** (file type becomes the baked default when git metrics are absent) reads well in manual verify — a no-`--deep-git` Code Map is no longer flat/inert, and the demoted secondary notice still honestly explains why the six git-derived dimensions specifically are missing. No conflict found with the locked design; not flagging for re-confirmation.
- Test suite additions land across `CodeMapTests.cs` (classifier unit tests), `ChartsTests.cs` (SVG/tooltip/data-attribute coverage split by `hasMetrics`), `CodeMapTemplaterTests.cs` (dropdown/legend/table rewrites for both `hasMetrics` states), `SiteGeneratorCodeMapTests.cs` (2 new generation-level tests: no-deep-git default + determinism, plus a new `--deep-git` real-repo fixture proving the sequential default is unchanged and file type is a selectable 7th option), and `SiteGeneratorWebviewTests.cs` (1 new SPA/webview coherence test). 1874/1874 green.
- **Shared-main note**: a concurrent session was actively editing this same working tree during implementation (uncommitted `MarkdownConverter.cs` + `sprint-status.yaml` changes for Epics 13–15, unrelated to Story 7.9). Verified via `git diff`/`git status` at multiple checkpoints that none of my edits were lost or clobbered and that I did not touch or revert the other session's in-flight work. [[shared-main-concurrent-edit-loss-verify-after-edit]]

### File List

- `src/SpecScribe/CodeMap.cs` — new `CodeFileCategory` record + `CodeFileType` static classifier; `CodeMapNode.Category` field; threaded through `Build`/`BuildChildren`/`BuildDir`.
- `src/SpecScribe/Charts.cs` — `CodeTreemap`/`AppendTreemapFile`: `data-filetype`/`data-filetype-label` always emitted; `type-<key>` baked default when `!hasMetrics`; `BuildTreemapCard`: always-present "Type" row.
- `src/SpecScribe/CodeMapTemplater.cs` — `AppendVariantPanel` gating loosened; `AppendColorizeControls` gained the `hasMetrics` parameter + "File type" option; `AppendLegend` + new `AppendDiscreteLegend` (both pre-rendered, one hidden); `AppendFileTable` gained an always-present "Type" column.
- `src/SpecScribe/assets/specscribe.js` — `DIM_LABELS.filetype`; new `recolor()` `"filetype"` branch; new `clearFillClasses()`/`swapLegend()` helpers; `legendDim` scoped to the ramp legend specifically.
- `src/SpecScribe/assets/specscribe.css` — new `.codemap-cell.type-*`/`.codemap-legend-swatch.type-*` discrete palette; `.codemap-legend[hidden]`; `.codemap-legend-discrete`/`.codemap-legend-label`; `.codemap-notice-secondary`.
- `tests/SpecScribe.Tests/CodeMapTests.cs` — classifier unit tests (category coverage, Other fallback, case-insensitivity, determinism, never-throws, no-git independence, directories carry no category).
- `tests/SpecScribe.Tests/ChartsTests.cs` — file-type data-attribute/baked-default/tooltip coverage split by `hasMetrics`; updated 2 pre-existing tests whose asserted fill class changed from `level-none` to `type-csharp` under the new `hasMetrics: false` default.
- `tests/SpecScribe.Tests/CodeMapTemplaterTests.cs` — rewrote the `hasMetrics: false` test for the new (no-longer-fully-hidden) controls/legend behavior; extended the `hasMetrics: true` test for the 7th option + pre-rendered legend pair + Type column.
- `tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs` — 2 new generation-level tests (no-deep-git default-is-filetype + determinism) plus a new real-git-repo `--deep-git` fixture/test (sequential default unchanged, file type selectable as 7th option) with its own `TryCreateGitHistory`/`RunGit`/`Commit` helpers.
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` — 1 new SPA/webview coherence test proving `code-map.html` is captured and renders the new markup correctly.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden content fingerprint constant regenerated (CSS/JS-only shift for this non-git fixture; documented in the existing changelog-comment convention).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `7-9-code-map-file-type-colorize-discrete-palette` → `review`.
- `_bmad-output/implementation-artifacts/7-9-code-map-file-type-colorize-discrete-palette.md` — this story file (frontmatter/tasks/Dev Agent Record/Change Log only).

## Change Log

| Date | Change |
|------|--------|
| 2026-07-20 | Story implemented: new bounded `CodeFileType` classifier (Task 1); SVG/tooltip/table rendering of the file-type dimension (Task 2); loosened `hasMetrics` gate + dropdown/discrete-legend (Task 3); client-side `recolor()` filetype branch + class-clearing symmetry fix (Task 4); new discrete CSS palette (Task 5); AC #3 coherence confirmed — `ClassifyCoupling` untouched, sequential dimensions byte-identical, `code-map.html` confirmed captured for SPA/webview with a new coherence test (Task 6); full test coverage added across classifier/chart/templater/generation/webview layers, golden fingerprint regenerated (Task 7); `dotnet test` green (1874/1874) + manual baseline and `--deep-git` generation passes verified (Task 8). Status → review. |
