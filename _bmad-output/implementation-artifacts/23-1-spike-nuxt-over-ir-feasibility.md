---
baseline_commit: b8be08d0f139c3dca487a7cab9ef87234a1a5630
implements_decision: docs/adrs/0009-frontend-framework-for-projection-layer.md # ADR 0009 (Accepted) locked the direction (Vue + Nuxt 3, universal/SSR) and mandated Epic 23 open with a feasibility spike (§Disposition #2). This story does NOT amend the ADR — it de-risks its build.
gates: [23-2, 23-3, 23-4, 23-5] # spike findings decide whether these proceed as scoped or are re-scoped (AC #3)
depends_on_unbuilt: 22-2 # Epic 22's CANONICAL IR schema (Story 22.2) is still backlog — see "The IR does not exist yet". Spike consumes the shipped SpaDelivery form as a PROXY IR (as Story 22.1 did).
---

# Story 23.1: Spike — Nuxt-over-IR Feasibility

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer evaluating whether Vue + Nuxt 3 can replace the C# presentation layer,
I want a feasibility spike proving the riskiest technical assumptions before committing to migration,
So that Epic 23's implementation stories (23.2–23.5) are scoped by evidence — mirroring exactly how Story 6.6 de-risked the delivery pivot and Story 22.1 de-risked the IR/incremental pivot.

## Why this spike exists — READ FIRST

[ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) (**Accepted**, owner-ratified 2026-07-20) **locked the direction**: replace SpecScribe's C# presentation/templating layer with **Vue + Nuxt 3 (universal/SSR)** rendering from the Epic 22 canonical IR. It ratified **Axis 1 = Option B (universal/SSR)** — Nuxt prerenders every route to static HTML at build (NFR6 baseline by construction) then hydrates for interactivity — and **Axis 2 = Vue + Nuxt 3** (Vite, Nitro, TypeScript, scoped-SFC CSS). Charts stay pre-rendered C# SVG carried *in* the IR; analysis stays in C# (ADR 0008 axis C **not** reopened).

But the ADR explicitly **did not schedule the build**. It seated Epic 23 as backlog and ruled the epic must **"open with a spike"** proving the integration risks it could not settle by argument. [ADR 0009 §Disposition #2, §"Remaining spike-owned unknowns"]

This story **is** that spike. Its single most valuable output is a defensible, evidence-backed answer to the four unknowns ADR 0009 named as **feasibility, not direction**:
1. **NFR6 survives** — does `nuxt generate` prerender actually produce a JS-optional-navigable static baseline for a real SpecScribe surface?
2. **Parity is achievable** — can one representative surface, rebuilt as scoped-CSS Vue consuming chart-SVG + Markdig-derived prose from the IR, match the golden output?
3. **Webview CSP survives** — does Nuxt's hydration script run under the webview's nonce-locked `script-src` (no `unsafe-inline`)?
4. **Node-in-pipeline cost** — what does adding a Node build step to the generation pipeline cost against the self-contained-binary distribution model (ADR 0005/0006)?

**This is a SPIKE — decision-first, timeboxed, throwaway (same discipline as Stories 6.3, 6.6, 22.1).** The durable deliverable is a **spike report artifact**, not shippable production code. **No production code changes ship from this story** (`src/SpecScribe/**` and `tests/**` untouched; generated site byte-identical — AC #3). The findings **gate** whether Stories 23.2–23.5 proceed as scoped or are re-scoped.

### ⚠️ The IR does not exist yet — consume the shipped SpaDelivery form as a PROXY IR

Epic 23 "depends on Epic 22 (consumes the IR)", but **Epic 22's canonical IR schema is Story 22.2, which is still `backlog` — not built.** Do not block on it and do not design it (that is 22.2's job, and 22.1 already re-scoped 22.2 toward page-level delta addressing). Instead, do exactly what Story 22.1 did: **consume the already-shipped `SpaDelivery` output** (`spa/manifest.json` + `spa/pages-*.json` content chunks) as a **proxy IR** — it is the serialized form of AD-2's view models + pre-rendered inline SVG, which is precisely what 22.2 will generalize. Generate it against this repo with `specscribe generate --spa` and point the Nuxt app at it.

**Consequence for the spike design:** keep the Vue↔IR binding **thin and adapter-shaped** so the feasibility finding survives the eventual 22.2 schema change. If the migration's viability depends on a specific SpaDelivery field name, that is a finding to report (it tells 22.2 what the front end needs), not a reason to harden a coupling.

### Scope guard — five things this spike is NOT (resist all five)
1. It is **not** the component library / design-token bridge (that is **Story 23.2** — this spike ports just enough tokens to render one surface, it does not establish the CSS-module conventions or port the full `--status-*`/`--motion-*` families).
2. It is **not** the baseline-surface migration (that is **Story 23.3** — this spike migrates ONE surface to *measure* parity, it does not deliver production dashboard/epics pages).
3. It is **not** retiring the C# `HtmlRenderAdapter` (that is **Story 23.4** — the C# renderer stays fully intact; the spike renders a parallel throwaway copy).
4. It is **not** the packaging reconciliation (that is **Story 23.5** — AC #3 requires *reporting the cost/impact* of Node-in-pipeline, NOT solving the self-contained-binary story).
5. It does **not** reopen the chart port or the C# analysis core. **Charts stay pre-rendered SVG in the IR; analysis stays in C#** (ADR 0009 non-goal; ADR 0008 axis C; ADR 0006 stands).

## Acceptance Criteria (spike — decision-first, throwaway, timeboxed)

Verbatim from [epics.md](../planning-artifacts/epics.md) Story 23.1, with measurement obligations made explicit:

1. **NFR6 JS-optional baseline holds under Nuxt prerender.**
   **Given** ADR 0009's universal/SSR direction,
   **When** the spike builds a representative Nuxt prerender (`nuxt generate` / Nitro prerender) of one existing surface,
   **Then** it **proves** the prerendered route is **fully rendered HTML, navigable without JavaScript** — measured by loading the generated route with JS disabled and confirming content + navigation are present (no blank-shell/`<div id="app">`-only output),
   **And** the finding is captured in a **spike report artifact** (mirroring Stories 6.6 / 22.1).

2. **Visual + functional parity verified for one representative surface.**
   **Given** the spike migrates one representative surface to **scoped-CSS Vue components**, **chart-SVG injection from the IR**, and **Markdig-derived prose**,
   **When** the migrated surface is compared to the existing golden output,
   **Then** it **verifies visual and functional parity** for that surface — reported as byte-diff (using the same `NormalizeVolatile` discipline the `GoldenContentFingerprint` gate uses) OR a documented, enumerated list of intentional/unavoidable equivalent-but-not-identical differences (e.g. attribute ordering, whitespace),
   **And** all three sub-risks are exercised: token-driven scoped CSS, at least one **chart SVG injected from the IR** (not re-rendered in JS), and at least one block of **Markdig-rendered prose** exercising the custom renderers (comment annotations, reference chips, gherkin/capability stylers, Mermaid).

3. **Webview-CSP verdict + Node-in-pipeline cost reported.**
   **Given** the VS Code webview's CSP constraints (Stories 6.5, 6.12 — `script-src 'nonce-…'`, no `unsafe-inline`),
   **When** the spike evaluates hydration,
   **Then** it **reports whether a hydration nonce survives the webview's CSP** — specifically whether Nuxt's hydration/entry scripts can be emitted with a per-render nonce and no inline-without-nonce script, and if not, what the gap is,
   **And** it **reports the cost and impact of adding a Node build step to the generation pipeline** against the self-contained-binary distribution model (ADR 0005/0006) — distinguishing today's *build-time-only, developer-side* Node (the `extension/` + `tools/prism-vendor/` npm toolchains) from putting Node in the **`specscribe generate` critical path** or the **end-user runtime**,
   **And** **no production code changes ship** (`src/SpecScribe/**` and `tests/**` untouched; generated site byte-identical), and the findings **gate whether Stories 23.2–23.5 proceed as scoped or are re-scoped**, named explicitly in the report + Completion Notes.

## Tasks / Subtasks

- [ ] **Task 1 — Branch + baseline + quarantine** (AC: #3)
  - [ ] Work on an isolated spike branch (e.g. `spike/nuxt-ir-23-1`) or worktree; do **NOT** develop on `main` (background auto-committer — memory: [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]]). Confirm `baseline_commit` in frontmatter matches the HEAD you branch off (`b8be08d` at authoring time).
  - [ ] Reuse the established quarantine discipline ([spike/README.md](../../spike/README.md)): all throwaway code lives under `spike/nuxt-ir/` (or similar) — the Nuxt app + any emitter probe. **Nothing** joins `SpecScribe.slnx`, `dotnet build src/SpecScribe`, `dotnet pack`, or the site build. The generated `specscribe` site must stay byte-identical with or without the spike folder. Add `spike/nuxt-ir/node_modules/`, `.nuxt/`, `.output/`, `dist/` to `spike/.gitignore`.
  - [ ] Verify the toolchain: this spike needs Node/npm (Nuxt). Node already exists in-repo for `extension/` and `tools/prism-vendor/` — confirm the version and note it for AC #3's cost analysis (today Node is **build-time-only + developer-side**, NOT required to run `specscribe generate`).

- [ ] **Task 2 — Produce the proxy IR (the input the Nuxt app consumes)** (AC: #2)
  - [ ] Generate the shipped SpaDelivery form against this repo: `specscribe generate --spa` (+ `--deep-git` if the chosen surface needs git insight data), output to `SpecScribeOutput` (memory: [[generate-output-dir-is-specscribeoutput]]; never `--output docs/live`). This yields `spa/manifest.json` + `spa/pages-*.json` — the **proxy IR** (see "The IR does not exist yet").
  - [ ] Confirm the manifest/chunk shape carries what the chosen surface needs: page HTML/body, pre-rendered inline chart SVG, nav/breadcrumb graph, titles ([SpaDelivery.cs](../../src/SpecScribe/SpaDelivery.cs) — `Manifest` record :245, `BuildDataFiles` :163, `OutputFile` :158). Note any field the front end wishes existed — that is a finding for Story 22.2.
  - [ ] Keep the Vue↔IR binding thin/adapter-shaped so it survives 22.2's eventual schema (do not harden on a specific SpaDelivery field name).

- [ ] **Task 3 — Migrate ONE representative surface + prove NFR6 baseline** (AC: #1, #2)
  - [ ] **Choose the representative surface** (see Dev Notes "Which surface"). Recommended: the **Dashboard** (chart-SVG-heavy — 69.3% of body is inline SVG; token-heavy; the canonical surface `DashboardViewBuilder`/6.2/6.3/22.1 all use and 23.3 targets) as the scoped-CSS + chart-SVG-injection probe, **plus one Markdig-heavy prose page** (an ADR or a story page) as the prose-fidelity probe — because the dashboard alone does not exercise the ~889 LOC custom Markdig renderers.
  - [ ] Build a minimal **Nuxt 3** app under `spike/nuxt-ir/` that reads the proxy IR and renders the chosen surface(s) with **scoped-SFC `<style scoped>` / CSS modules**, porting only the tokens the surface needs (`--status-*`, `--motion-*` — memory: [[specscribe-status-token-system]], [[motion-token-system]]; do NOT duplicate/drift the values — AD-7).
  - [ ] **Chart-SVG injection:** inject at least one chart's pre-rendered SVG from the IR as data (e.g. `v-html` of a trusted, build-produced SVG string) — do NOT re-render the chart in JS (charts stay C#-SVG per ADR 0009).
  - [ ] **Markdig prose:** render at least one block of Markdig-derived prose from the IR and inspect the custom-renderer output survives: comment annotations ([CommentAnnotationRenderer.cs](../../src/SpecScribe/CommentAnnotationRenderer.cs)), reference chips ([ReferenceChipRenderer.cs](../../src/SpecScribe/ReferenceChipRenderer.cs)), Mermaid ([MermaidCodeBlockRenderer.cs](../../src/SpecScribe/MermaidCodeBlockRenderer.cs)), gherkin/capability stylers, color swatches, table tagging ([MarkdownConverter.cs](../../src/SpecScribe/MarkdownConverter.cs)).
  - [ ] **Prove NFR6 (AC #1):** run `nuxt generate` (SSG/prerender) and load the emitted static route **with JavaScript disabled**. Confirm the content + in-page navigation are fully present in the prerendered HTML (not a hydration-only shell). Capture the evidence (the emitted HTML has real content; a JS-off screenshot or `curl` of the static file) in the report.

- [ ] **Task 4 — Verify parity against the golden output** (AC: #2)
  - [ ] Compare the migrated surface to the existing golden output for that surface. Use the SAME normalization the byte-parity gate uses — neutralize the wall-clock footer, `?v=<ModuleVersionId>` asset cache-bust, CRLF, and build-derived product version ([SiteGeneratorAdapterTests.cs:230 `GenerateAll_GoldenContentFingerprint_…`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs); memory: [[golden-diff-normalization-gotchas]]).
  - [ ] Report parity as **byte-identical** (confirm with a **repeated run** before trusting it — the stale-build first-captured-hash trap) OR as an **enumerated list** of intentional/unavoidable equivalent differences (attribute order, whitespace, self-closing style), each with a one-line rationale. A diff that is only footer-clock/build-token noise is not a parity failure.

- [ ] **Task 5 — Webview-CSP hydration verdict + Node-in-pipeline cost** (AC: #3)
  - [ ] **Webview CSP:** evaluate whether Nuxt's hydration/entry scripts can run under the webview CSP — `script-src 'nonce-__NONCE__'`, `style-src 'unsafe-inline' __CSP_SOURCE__`, no `unsafe-inline` for script ([WebviewRenderAdapter.cs:102 CSP meta; :117 nonce'd bridge](../../src/SpecScribe/WebviewRenderAdapter.cs); Stories 6.5, 6.12). Report whether Nuxt can emit its hydration scripts with a per-render nonce and no un-nonced inline script (Nuxt inline-script config, `<NuxtIsland>`/partial-hydration, or CSP nonce integration), and name the concrete gap if it cannot. Static analysis of Nuxt's emitted `<script>` tags is acceptable evidence; an actual VS Code paint is not required (mirror ADR 0005's "everything up to pixel-paint is evidence-backed" honesty).
  - [ ] **Node-in-pipeline cost:** report the cost/impact of adding Node to the generation pipeline vs the self-contained-binary model (ADR 0005/0006). Distinguish three states: (a) today — Node is build-time-only + developer-side (`extension/`, `tools/prism-vendor/`), NOT needed to run `specscribe generate`; (b) Nuxt build at **package/CI time** producing pre-built assets shipped in the binary; (c) Nuxt/Node in the **`specscribe generate` critical path** or the **end-user runtime**. Measure or estimate: Nuxt build wall-clock for the surface(s), output asset size, and whether the npx (Story 16.8) + VS Code Marketplace (16.5) channels can stay Node-runtime-free for end users. This is the input to Story 23.5's reconciliation — do not solve 23.5.

- [ ] **Task 6 — Write the spike report + gate the epic** (AC: #1, #2, #3)
  - [ ] Author a **spike report artifact** (Context → Measured Evidence → Findings → Gate), mirroring Story 22.1's [`22-1-spike-report.md`](22-1-spike-report.md). Recommended home: `_bmad-output/implementation-artifacts/23-1-spike-report.md`.
  - [ ] The report **states** verdicts for all four unknowns: NFR6 baseline (holds/gap), parity (achieved/enumerated deltas), webview-CSP nonce survival (survives/gap), Node-in-pipeline cost.
  - [ ] The report **names the gate** for 23.2–23.5 (proceed-as-scoped vs re-scope, with the reason for each).
  - [ ] **This spike does NOT author a new ADR** — ADR 0009 already decided the direction. Reserve a new ADR only if findings genuinely *contradict* ADR 0009 and force an amendment (e.g. webview CSP is fundamentally incompatible with Nuxt hydration, or NFR6 cannot hold under prerender) — if so, propose it explicitly rather than burying a reversal in the story record (memory: [[adr-creation-trigger-gap-epic-10-retro]]).

- [ ] **Task 7 — Quarantine check & land only the decision** (AC: #3)
  - [ ] Confirm **no `src/SpecScribe/**/*.cs` or `tests/**` touched** (git-confirmed: only `spike/nuxt-ir/` is new). Run the full suite (`dotnet test SpecScribe.slnx -c Release`) and confirm the site is byte-identical (same `GoldenContentFingerprint`) before/after — flag any pre-existing golden failure as pre-existing (reproduce on clean `main`; memory: [[golden-diff-normalization-gotchas]] — the 22.1 run already noted a stale golden-constant drift on `main`).
  - [ ] The **only** artifacts that land on `main`: the **spike report**, this **story record**, the **sprint-status update**, and the quarantined **Nuxt app/probe** under `spike/nuxt-ir/`. Throwaway — deletable with the branch.

## Dev Notes

### This is a SPIKE — decision-first, timeboxed, throwaway (same discipline as Stories 6.3, 6.6 & 22.1)
The deliverable is a **numbers-backed spike report that gates 23.2–23.5**, not a shippable Vue/Nuxt front end or a migrated production surface. Build only as much throwaway code as it takes to make the four feasibility decisions **defensible with real evidence against this repo**, then stop. **The single biggest trap is scope-creeping into an actual migration** — the component library is 23.2, the production dashboard/epics migration is 23.3, retiring `HtmlRenderAdapter` is 23.4, packaging is 23.5. Resist all four. Spike-led de-risking of foundational moves is this project's established pattern (6.3 → ADR 0005, 6.6 → ADR 0006, 22.1 → the IR-build gate). Memory: [[adr-0005-reopened-delivery-arch-spike]], [[static-prerendering-vs-json-spa-architecture-question]], [[story-22-1-ir-incremental-spike-done]].

### Ground the decision in ADR 0009's own analysis (don't re-litigate the direction)
ADR 0009 **ratified** topology (Option B / universal-SSR) and framework (Vue + Nuxt 3). This spike does **not** re-open either — it proves the *integration*. The four unknowns ADR 0009 explicitly handed to the spike are: **Markdig-prose fidelity, chart-SVG injection ergonomics, webview-CSP under a hydration nonce, and Node-in-pipeline packaging reconciliation** [ADR 0009 §"Remaining spike-owned unknowns"]. Reuse the ADR's already-costed figures rather than re-deriving them:
- **~6,100 LOC** of C# rendering is the eventual axis-B reimplementation (templaters ~4,691 + charts ~1,425); charts stay C#-SVG so ~1,425 is out of scope. The real fidelity risk is the **~889 LOC of custom Markdig renderers** (Mermaid, comment annotations, link rewriters, gherkin/capability stylers) [ADR 0009 §Consequences]. This is why AC #2 requires exercising real Markdig prose, not lorem-ipsum.
- **NFR6 stops being free** the moment rendering leaves C# — the whole point of Option B (prerender) is to keep it free by construction. AC #1 is the *proof* that it actually does for a real surface. A blank-shell prerender = the load-bearing failure. [ADR 0009 §"The load-bearing tension"]
- Inline chart SVG is **69.3% of the dashboard body / 58.9% of epics** [ADR 0008 §Context] — so the dashboard/epics surfaces are the strongest chart-SVG-injection test, and the injected-SVG mass dominates their byte parity.

### The C# presentation layer this spike renders a parallel copy of (do NOT modify it)
The migration target (for later stories) — read enough to render a faithful parallel copy; the spike touches **none** of it:
- **`HtmlRenderAdapter`** ([HtmlRenderAdapter.cs](../../src/SpecScribe/HtmlRenderAdapter.cs) :422, [.Dashboard.cs](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs) :526, [.Epics.cs](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs) :716) — the content renderer 23.4 eventually retires. The `IRenderAdapter` contract ([IRenderAdapter.cs](../../src/SpecScribe/IRenderAdapter.cs)) is the seam; `JsonSpaRenderAdapter` + `SpaDelivery` are the already-shipped IR-producing adapter this spike consumes.
- **View builders** ([DashboardViewBuilder.cs](../../src/SpecScribe/DashboardViewBuilder.cs), [EpicsViewBuilder.cs](../../src/SpecScribe/EpicsViewBuilder.cs)) — the AD-2 host-neutral view models the IR serializes. Story 6.2's records already round-trip losslessly through `System.Text.Json` (memory: [[story-6-2-section-view-models-live]]). The `spike/vscode/renderer` probe is the precedent for a project-referencing C# probe that reuses these builders — but note **that** spike emitted C#-rendered HTML; **this** spike emits the IR JSON and renders it in Nuxt.
- **Charts** ([Charts.cs](../../src/SpecScribe/Charts.cs), 4,745 LOC) — pure-SVG generators. Stay in C#; injected as data. Do not port to JS (memory: [[charting-is-pure-svg-no-js]]).
- **Markdig custom renderers** — the fidelity risk surface: [MarkdownConverter.cs](../../src/SpecScribe/MarkdownConverter.cs) (pipeline config, `EmphasisExtra` minus Subscript — memory: [[markdig-subscript-tilde-strikethrough-bug]]; `ColorSwatchRewriter`, `TagTables`), [CommentAnnotationRenderer.cs](../../src/SpecScribe/CommentAnnotationRenderer.cs), [ReferenceChipRenderer.cs](../../src/SpecScribe/ReferenceChipRenderer.cs), [MermaidCodeBlockRenderer.cs](../../src/SpecScribe/MermaidCodeBlockRenderer.cs).

### Which surface to migrate (a genuine choice — recommendation + rationale)
AC #2 wants one surface exercising **scoped CSS + chart-SVG injection + Markdig prose**. No single existing surface exercises all three heavily: the **dashboard/epics** are chart-and-token-heavy but thin on Markdig prose; a **story/ADR/doc page** is Markdig-heavy but light on charts. Recommended split: **Dashboard** as the primary (scoped CSS + chart-SVG injection + token parity, and it is 23.3's target so the finding transfers directly) **plus one Markdig-heavy page** (an ADR or a story detail page) as the prose-fidelity probe. The dev may instead pick the **epics** page if a single surface is preferred — it has status tokens, some charts, and structured content, at the cost of lighter Markdig coverage. Record the choice and rationale in the report. *(Saved as an open question for the owner at the end — a single-surface run is acceptable if the owner prefers to timebox harder.)*

### Webview CSP is a hard, specific constraint (not a soft goal)
The webview document ships `script-src 'nonce-__NONCE__'` with **no `unsafe-inline` for scripts** ([WebviewRenderAdapter.cs:102](../../src/SpecScribe/WebviewRenderAdapter.cs)); the single bridge script is nonce'd and the panel document (with its one nonce) is set exactly once, swapped in place ([WebviewRenderAdapter.cs:117, 250](../../src/SpecScribe/WebviewRenderAdapter.cs)). Nuxt hydration emits its own entry/inline scripts — the spike's job is to determine whether those can all carry the per-render nonce (Nuxt CSP/nonce support, `app.head` script attrs, or partial-hydration to minimize/eliminate inline script) under this exact policy. If Nuxt cannot avoid an un-nonced inline script, that is a **reportable gap** that scopes 23.4 (or forces an ADR 0009 amendment). Memory: [[story-6-5-webview-theming-live]], [[story-6-12-problems-panel-diagnostics-live]].

### Correctness/parity measurement discipline (avoid a false "it works")
- The parity oracle is the **existing golden output** for the chosen surface. Use the golden gate's normalization ([SiteGeneratorAdapterTests.cs:230](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)); a footer-clock/build-token-only diff is not a failure (memory: [[golden-diff-normalization-gotchas]]).
- Confirm any "byte-identical" or "NFR6 holds" claim with a **repeated run** before trusting it (the stale-build first-captured-hash trap).
- Output dir for any `specscribe generate` is `SpecScribeOutput` (memory: [[generate-output-dir-is-specscribeoutput]]); never `--output docs/live`.
- Be as honest as ADR 0005/Story 6.3 were about the un-provable step: an actual VS Code Electron paint under CSP needs a manual F5 and may be out of a headless run's reach — static analysis of Nuxt's emitted script tags against the CSP is legitimate evidence; call out precisely what was and wasn't proven.

### Project Structure Notes
- **Throwaway Nuxt app + any emitter:** self-contained under `spike/nuxt-ir/` — its own `package.json`/Nuxt project; may consume the shipped `spa/` output or (optionally) a small C# probe that `<ProjectReference>`s `src/SpecScribe` to emit the proxy IR. **Not** added to `SpecScribe.slnx`, `dotnet build src/SpecScribe`, `dotnet pack`, the site build, or the shipped `extension/` bundle. Exists to be deleted with the branch. Gitignore `node_modules/`, `.nuxt/`, `.output/`.
- **No new production project, namespace split, or Node dependency on `main`** (seed-level; the whole point of AC #3 is to *decide* whether Node-in-pipeline is acceptable, not to introduce it).
- **Branch/worktree, not `main`:** `main` has a background auto-committer; edits must target the worktree path if using a worktree (memory: [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]]).

### Where the report lives
Mirror Story 22.1, which produced a **spike report** (not a new ADR) because ADR 0008 had already decided the direction. Same here: ADR 0009 is Accepted; this spike de-risks its *build*. So the durable output is a **spike report artifact** at `_bmad-output/implementation-artifacts/23-1-spike-report.md`. Do **not** create a new `docs/adrs/00XX-*.md` unless findings force an ADR 0009 amendment (then propose it explicitly).

### References
- **The decision this spike de-risks the build of:** [ADR 0009 — Front-End Framework for the Projection Layer](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) (Accepted; §Disposition mandates this spike; §"Remaining spike-owned unknowns" names the four risks). Depends on [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) (the IR this consumes — schema still unbuilt, Story 22.2). Partially un-defers [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) axis B; revises [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md)'s "as little TypeScript as possible" north star for the presentation layer.
- **The spikes this one mirrors:** [22-1-spike-incremental-recompute-and-ir-delta-transport.md](22-1-spike-incremental-recompute-and-ir-delta-transport.md) + [22-1-spike-report.md](22-1-spike-report.md) (decision-first, throwaway, quarantine, report-is-the-deliverable, gate-the-epic); [6-6-delivery-architecture-and-distribution-spike.md](6-6-delivery-architecture-and-distribution-spike.md); [6-3-vs-code-integration-spike.md](6-3-vs-code-integration-spike.md) (→ ADR 0005, the "prove up to pixel-paint, be honest about the rest" precedent); [spike/README.md](../../spike/README.md) (quarantine pattern) + [spike/vscode/renderer](../../spike/vscode) (a probe that project-references `src/SpecScribe` and reuses the view builders).
- **Epic + story text:** [epics.md Epic 23](../planning-artifacts/epics.md) (§3371 epic; §3390 Story 23.1 ACs; §3414–3484 Stories 23.2–23.5 the findings gate).
- **The proxy IR to consume:** [SpaDelivery.cs](../../src/SpecScribe/SpaDelivery.cs) (`ManifestPath`:27, `BuildDataFiles`:163, `Manifest`:245, `MaxChunkBytes=2MB`:56), [JsonSpaRenderAdapter.cs](../../src/SpecScribe/JsonSpaRenderAdapter.cs), [SpaBundle.cs](../../src/SpecScribe/SpaBundle.cs).
- **The C# presentation layer (render-a-parallel-copy target; do not modify):** [HtmlRenderAdapter.cs](../../src/SpecScribe/HtmlRenderAdapter.cs) (+ `.Dashboard.cs`, `.Epics.cs`), [DashboardViewBuilder.cs](../../src/SpecScribe/DashboardViewBuilder.cs), [EpicsViewBuilder.cs](../../src/SpecScribe/EpicsViewBuilder.cs), [Charts.cs](../../src/SpecScribe/Charts.cs), [MarkdownConverter.cs](../../src/SpecScribe/MarkdownConverter.cs), [CommentAnnotationRenderer.cs](../../src/SpecScribe/CommentAnnotationRenderer.cs), [ReferenceChipRenderer.cs](../../src/SpecScribe/ReferenceChipRenderer.cs), [MermaidCodeBlockRenderer.cs](../../src/SpecScribe/MermaidCodeBlockRenderer.cs).
- **Webview CSP:** [WebviewRenderAdapter.cs](../../src/SpecScribe/WebviewRenderAdapter.cs) (:102 CSP meta, :117 nonce'd bridge). **Parity oracle:** [SiteGeneratorAdapterTests.cs:230](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) (`GoldenContentFingerprint`). **Existing Node footprint (AC #3):** `extension/package.json`, `tools/prism-vendor/package.json`.
- **Architecture:** [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1/AD-2 (one renderer / host-neutral view models; the drift hazard a single renderer removes), AD-7 (:82 presentation tokens shared, host chrome host-owned), NFR6 (JS-optional baseline — the invariant AC #1 protects), NFR4 (additive).
- **Memory:** [[story-22-1-ir-incremental-spike-done]] (the immediate predecessor spike — model + the IR's real state), [[static-prerendering-vs-json-spa-architecture-question]] (ADR 0008/0009 sequencing: Epic 7 → 22 → 23), [[specscribe-status-token-system]] + [[motion-token-system]] (tokens to port without drift — AD-7), [[css-comment-star-slash-silent-truncation]] (the CSS-fragility incident that motivates scoped CSS — the class of failure this migration ends), [[markdig-subscript-tilde-strikethrough-bug]] (a real Markdig-fidelity gotcha), [[charting-is-pure-svg-no-js]] (charts stay SVG), [[story-6-5-webview-theming-live]] + [[story-6-12-problems-panel-diagnostics-live]] (webview CSP context), [[story-6-2-section-view-models-live]] (the lossless view-model serialization the IR uses), [[golden-diff-normalization-gotchas]] (parity + repeated-run discipline), [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]], [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR explicitly if findings force an ADR-0009 amendment).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-23 — Story 23.1 drafted (create-story). Epic 23 opened (backlog → in-progress) with its ADR-0009-mandated feasibility spike — the Story-6.6/22.1-style de-risking of the four unknowns ADR 0009 handed the spike: **NFR6 JS-optional baseline under Nuxt prerender**, **visual+functional parity of one representative surface** (scoped-CSS Vue + chart-SVG injection from the IR + Markdig-prose fidelity), **webview-CSP survival under a hydration nonce**, and **Node-in-pipeline cost** vs the self-contained-binary distribution (ADR 0005/0006). Decision-first, throwaway, no production code ships (`src`/`tests` untouched; site byte-identical); the durable deliverable is a **spike report** (NOT a new ADR — ADR 0009 already decided the direction) whose findings **gate** whether Stories 23.2–23.5 proceed as scoped or are re-scoped. Consumes the shipped `SpaDelivery` output as a **proxy IR** because Epic 22's canonical IR schema (Story 22.2) is still backlog/unbuilt. Quarantined under `spike/nuxt-ir/`; does not disturb Epic 7 or the current roadmap.
