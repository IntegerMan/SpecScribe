---
baseline_commit: 6e12d0d79bbd891e20603759218699b0b4f1aeef
implements_decision: docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md # ADR 0012 is ALREADY RATIFIED (owner chose ratify-now 2026-07-24). This spike VALIDATES and records an addendum — it does NOT gate.
companion_decision: docs/adrs/0013-text-twin-is-the-no-js-contract.md # removes the server-SVG fallback, which is WHY the a11y finding is escalation-grade
informs: [20-5, 20-6, 20-7] # the component, the twin audit, the rollout
sequencing: 20.7 must land before 24.2 begins # unchanged and strengthened by ADR 0012
replaces: 20-4-shared-client-side-geometry-engine # seated 2026-07-23 by the Epics 19+21 retro; INVALIDATED by ADR 0012 §"Ratified decisions" #7
---

# Story 20.4: Plotly Engine-Adoption Spike — Vendoring, Budget, CSP, and Accessibility

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer adopting SpecScribe's first third-party runtime dependency,
I want Plotly's real cost and conformance measured against this codebase before the component is built on it,
So that ADR 0012's ratified direction is validated by numbers — and its two named escalation triggers fire early if they are going to fire at all.

## ⛔ Read first — what this story IS and IS NOT

**This story is NOT the one seated on 2026-07-23.** The old Story 20.4 ("extract the shared arc/radial math from
the three hand-rolled renderers into one module") was **invalidated** by [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
(Ratified decision #7): Plotly owns arcs, so the three renderers are **deleted by Story 20.7, not consolidated**.
If you find a task plan or notes describing arc-math extraction, they are stale — ignore them.

**ADR 0012 is already Accepted.** The owner chose ratify-now on 2026-07-24 (SCP 2026-07-24 § "Owner decisions
taken during this workflow" → *Ratification: Accepted now, spike validates via addendum*). So:

| | |
|---|---|
| **This spike does** | Measure. Report numbers. Write an **addendum onto ADR 0012**. Report a11y as **explicit pass/fail**. |
| **This spike does NOT** | Re-decide the engine. Gate Stories 20.5–20.8. Ship production code. Land the ADR 0005 CSP amendment. |

**The two escalation triggers ADR 0012 names by hand** (§"Spike validation" #3 and #4) — know which is which before
you start, because they behave differently:

| Trigger | If it fires | Consequence |
|---|---|---|
| **(a) Webview CSP failure** | Plotly cannot render under a CSP the owner accepts | Selects the ADR 0012 §5 **text-twin fallback** for the webview. **Does NOT reopen the engine choice.** Report it, move on. |
| **(b) Hard a11y failure Plotly cannot be configured around** | UX-DR7/16/17/18 conformance is unreachable | **This is the one finding that reopens ADR 0012** (toward the deferred ECharts option). It must be reported as **pass/fail**, never as a polish note. |

**Discipline:** decision-first, timeboxed, throwaway — the same discipline as Stories 6.3, 6.6, 22.1, 23.1. The
durable deliverable is a **spike report artifact** at `_bmad-output/implementation-artifacts/20-4-spike-report.md`
plus an **addendum section appended to ADR 0012**. Everything else is disposable. Suggested timebox: **2 days**;
if the a11y axis alone eats the box, finish that axis and report the others as unmeasured rather than half-measuring
all five.

## 🔴 Reconciliations against shipped code — verified 2026-07-24, honor these

These are the places where the epic/ADR/sprint-status prose does **not** match what is actually in the repo today.
Each one will waste your time or produce a wrong number if you take the prose at face value.

### R1 — "sunburst + treemap + heatmap only" is not literally achievable

ADR 0012 §1 and AC #1 both say the custom build is *"limited to the `sunburst` + `treemap` + `heatmap` traces."*
Plotly's own custom-bundle tooling states: **`scatter` is included in all bundles and cannot be removed** — it
lives in `lib/core.js`, not `lib/index.js`. So the real floor is `scatter + sunburst + treemap + heatmap`.
**Report the true trace list and the true floor size**; do not silently ship a four-trace bundle described as
three, and do not treat this as a blocker — it is a documentation correction for the addendum.

### R2 — Plotly ships a `--strict` bundle mode built specifically for CSP. Try it FIRST.

Plotly's `strict` variant exists precisely to avoid the `Function` constructor / `eval` paths that force
`script-src 'unsafe-eval'`. Build **both** variants and report both, because they have different sizes and
different CSP outcomes:

```bash
npm run custom-bundle -- --traces sunburst,treemap,heatmap --out specscribe-hierarchy
npm run custom-bundle -- --traces sunburst,treemap,heatmap --strict --out specscribe-hierarchy-strict
```

Reference points from Plotly's published stats (v3.7.0): **full bundle 4.6 MB minified / 1.4 MB min+gzip**; the
**full strict bundle 1.5 MB min+gzip** across 36 traces. A four-trace custom build should land far below both —
that is the number AC #1 wants.

### R3 — `style-src 'unsafe-inline'` is ALREADY in the shipped webview CSP

ADR 0012 §5 worries that *"`style-src` must accommodate the runtime `<style>` Plotly injects."* Check the shipped
policy before assuming that is an open problem — [`WebviewRenderAdapter.cs:113`](../../src/SpecScribe/WebviewRenderAdapter.cs):

```
default-src 'none'; base-uri 'none'; form-action 'none'; img-src __CSP_SOURCE__ data: https:;
style-src 'unsafe-inline' __CSP_SOURCE__; script-src 'nonce-__NONCE__'; font-src __CSP_SOURCE__ data:;
```

`style-src 'unsafe-inline'` is already present (ADR 0005's *"measured, accepted posture"* for the render's inline
style attributes). So Plotly's inline-CSS requirement is **plausibly already satisfied today**, and the live
question is narrower than the ADR implies: **does `script-src 'nonce-…'` alone suffice, or does Plotly need
`'unsafe-eval'`?** Report the style axis and the script axis **separately** — collapsing them will over-report the
gap.

### R4 — the 82.5 MB `code-map.html` figure is a HISTORICAL peak that was already partially mitigated

AC #1, ADR 0012 §"Spike validation" #2, and the sprint-status note all cite `code-map.html` at 82.5 MB. That
measurement is from Story 6.6's at-scale pass (2026-07-20) and was **partially resolved on 2026-07-21** — see
[`deferred-work.md:453`](./deferred-work.md): `Charts.MaxDetailedCodeMapFiles` (4000) now caps the
doubly-escaped `data-tip-html` tooltip card that was the confirmed dominant per-file cost, and the text-equivalent
table caps at the same set with an honest "+N more files" row. Rect geometry was **not** reduced.

**Consequence:** measure `code-map.html`'s size **as it is today** on this repo at `--deep-git` scale and use
*that* as the SVG-side baseline. Quoting 82.5 MB as a current figure would overstate the win. Report both the
current number and the historical peak so the addendum is honest either way.

### R5 — Epic 16 is entirely `backlog`. Two of the "three channels" do not exist yet.

AC #1 asks for *"packaging impact across all three channels (self-contained binary, npx Story 16.8, VSIX Story
16.5)."* Every Epic 16 story is `backlog` — there is **no release pipeline to measure**. What exists:

| Channel | What exists today | What "impact" means for this spike |
|---|---|---|
| **Self-contained binary** (Story 16.3) | Nothing. `SpecScribe.csproj` packs as a **.NET global tool** (`PackAsTool`, `ToolCommandName=specscribe`). | Design-level: the asset is an `EmbeddedResource` like `prism.js`, so it rides the same path. Report the **byte delta to the packed artifact**. |
| **npx** (Story 16.8) | Nothing. ADR 0006 §C describes a "~1.5 KB npm wrapper … resolves and spawns the self-contained binary." | Design-level: the wrapper fetches the binary, so the cost is the binary's delta (above). Say so; do not invent a measurement. |
| **VSIX** (Story 16.5) | **This one is real**: [`extension/package.json:283`](../../extension/package.json) has a working `package` script (`esbuild --production && npx @vscode/vsce package --no-dependencies`). | **Measurable.** Actually run it and report the VSIX byte delta with/without the vendored bundle. This is the only channel where a number is available. |

Report the analysis honestly split into *measured* (VSIX) and *design-level* (binary, npx). Do not present a
design-level estimate as a measurement — that is exactly the correction Story 23.1's report had to absorb.

### R6 — Story 20.2 IS BUILT. Its payload island is your real comparison baseline.

Sprint status shows `20-2-zoomable-drill-in-sunburst-navigation: review` — the code shipped.
[`SunburstExplorer.cs`](../../src/SpecScribe/SunburstExplorer.cs) exists, and `SunburstExplorerDataId =
"sunburst-explorer-data"` (`:48`) names the payload island. Story 23.1's spike **measured that island at 20,915 B
on the dashboard**, against chart SVG at **69.3% of the dashboard body** (366,910 B total → ≈ 254 KB of SVG).

That is the shape of the AC #1 output-size answer already sitting in the repo: **per page, payload ≪ SVG; the
offset is one shared bundle, amortized across every page.** Use the real island, not a synthetic one, and express
the net delta as `Σ(SVG removed) − Σ(payload added) − (one vendored bundle)` across the **whole generated portal**,
not one page.

### R7 — Story 23.1 already measured this webview CSP, in a real browser. Reuse the method and inherit the caveats.

[`23-1-spike-report.md` § Axis 3](./23-1-spike-report.md) replayed the **byte-verbatim** shipped policy string
over real output using [`spike/nuxt-ir/scripts/csp-probe.mjs`](../../spike/nuxt-ir/scripts/csp-probe.mjs). Reuse
that harness shape. Four findings transfer directly:

1. **A nonce does not propagate to a module's static imports** — if the vendored bundle is emitted as an ES
   module with imports, they will be blocked. Prefer a **single classic `<script nonce>` file** (which is how
   `prism.js` already ships) and the problem does not arise.
2. **`default-src 'none'` with no `connect-src` blocks every fetch.** Plotly must not fetch anything. Confirm.
3. **A half-applied CSP relaxation is catastrophically worse than none** — 23.1 measured a page going from 148
   SVGs to **0** because hydration started and then failed. Under ADR 0013 there is no server SVG behind the
   chart, so the equivalent failure is a **blank chart region**. Test the partial-relaxation state explicitly.
4. **The honesty boundary is real and it is inherited**: 23.1's probe delivered CSP as an **HTTP response
   header** over an **HTTP-served** asset graph. The webview delivers it in a **`<meta http-equiv>` tag** with
   **no server** (CSS inlined, one self-contained document, `vscode-resource:` URIs). Meta-delivered CSP ignores
   some directives and does not apply to resources requested before the tag is parsed. **State this boundary in
   your report the same way** — a browser-measured verdict is a *lower bound* on the webview gap, not a
   characterization of it.

Also note: `WebviewRenderAdapter.cs:79` already **strips every `<script type="application/json">` island** from
webview documents by regex. That means the webview does not receive the 20.2 payload today — a fact the CSP
analysis must account for (the webview would need the island *kept and nonced*, or it takes the text-twin
fallback).

**The ADR 0005 CSP amendment is SHARED with Story 23.4 and must be landed ONCE, not twice** (ADR 0012 §5; 23.1
report § "Security note"). **This spike does not land it** — it produces the evidence that amendment will cite.

### R8 — `tools/prism-vendor/` is the exact vendoring precedent. Copy its shape.

SpecScribe has already vendored a third-party JS bundle once, with a hand-run build script and a committed
artifact:

- [`tools/prism-vendor/build.js`](../../tools/prism-vendor/build.js) — assembles the bundle from `node_modules`,
  writes to `src/SpecScribe/assets/`. **"NOT part of the app build — run by hand… The produced files are
  committed; `node_modules` here is throwaway (gitignored)."**
- [`SpecScribe.csproj:62-63`](../../src/SpecScribe/SpecScribe.csproj) — `<EmbeddedResource Include="assets\prism.js" />`
- [`SiteGenerator.cs:1779-1780`](../../src/SpecScribe/SiteGenerator.cs) — `CopyEmbeddedAsset(...)` **conditionally**,
  *"only when in-portal code pages are generated, so sites without code pages stay byte-identical."*

**The size yardstick:** the shipped `assets/prism.js` is **100,409 bytes** uncompressed. Report the Plotly bundle
as a multiple of that — it is the honest in-repo comparison for "how big a vendored dependency has this project
already accepted."

### R9 — do not chase the golden fingerprint, and do not repeat 23.1's vacuous claim

The `GoldenContentFingerprint` test builds its site from a **synthetic `Directory.CreateTempSubdirectory`
fixture** and never walks the repository ([`SiteGeneratorAdapterTests.cs:19`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)),
so adding a `spike/plotly-…/` folder **cannot** move it. 23.1's first draft offered "the hash is unchanged" as
evidence of non-invasiveness and had to retract it as *structurally vacuous*. **The load-bearing evidence is
simply that no `src/` or `tests/` file was modified — `git` confirms that directly.** 23.1 also reported the
constant is in **pre-existing stale drift on `main`**; if the test fails, verify it fails identically on a clean
worktree before spending any time on it.

## Acceptance Criteria

Verbatim from [epics.md](../planning-artifacts/epics.md) Story 20.4, with measurement obligations made explicit.

1.
**Given** ADR 0012's decision to vendor Plotly locally (never CDN, `file://`-safe)
**When** the spike produces a custom build limited to the `sunburst` + `treemap` + `heatmap` traces
**Then** it reports that build's size, and the **net output-size delta** against today's inline SVG across a real generated portal — including `code-map.html`, which has previously reached 82.5 MB
**And** it confirms the vendored asset loads offline and from `file://`, and reports the packaging impact across all three channels (self-contained binary, npx Story 16.8, VSIX Story 16.5).

> **Measurement obligations:** report the true trace floor (R1) and **both** the standard and `--strict` variants
> (R2), minified and gzipped. Net delta = `Σ(SVG bytes removed) − Σ(payload bytes added) − (one bundle)` measured
> across the **whole portal** generated from this repo at `--deep-git` scale, with `code-map.html` reported
> **at its current size** alongside the historical 82.5 MB peak (R4). `file://` means literally opening the file
> from disk with no server, air-gapped. Packaging split into measured (VSIX) vs design-level (binary, npx) (R5).

2.
**Given** ADR 0012's two named escalation triggers
**When** the spike evaluates the webview and accessibility
**Then** it reports whether Plotly renders under the VS Code webview CSP (`script-src 'nonce-…'`, plus Plotly's runtime `<style>` injection) — a failure selects the ADR 0012 §5 text-twin fallback and does **not** reopen the engine choice
**And** it reports **explicit pass/fail** conformance against UX-DR7 (Tab order, Enter/Space drill, Escape up), UX-DR16, UX-DR17, and UX-DR18 reduced-motion — a hard a11y failure Plotly cannot be configured around is the one finding that reopens ADR 0012, and must be reported as such rather than as a polish note.

> **Measurement obligations:** the CSP verdict must separate the **script** axis from the **style** axis (R3),
> test the partial-relaxation state (R7 #3), and carry 23.1's honesty boundary about meta-delivery and
> `vscode-resource:` (R7 #4). The a11y verdict must be a **table with a literal PASS or FAIL per UX-DR**, plus the
> configured-around decision rule below. "Partial", "mostly", and "with work" are not verdicts.

3.
**Given** ADR 0012's requirement that presentation stays SpecScribe's
**When** the spike renders a representative hierarchy
**Then** it demonstrates the chart driven entirely by the existing `--status-*` and brand tokens with Plotly's default colorways disabled
**And** its findings are recorded back onto ADR 0012 as an addendum (the ADR is already ratified — this validates, it does not gate).

> **Measurement obligations:** drive the probe from the **real** `sunburst-explorer-data` island emitted by
> `SunburstExplorer.cs` (R6) and the **shipped** `specscribe.css` token values — do not re-type token values (AD-7
> drift). "Colorways disabled" means demonstrated, not asserted: show that no Plotly default color reaches the
> rendered output.

### The a11y decision rule — settle this BEFORE you test, then apply it mechanically

ADR 0012 hangs an ADR reopen on the phrase *"a hard a11y failure Plotly cannot be configured around."* That phrase
needs an operational definition or the verdict becomes a judgment call under pressure. Use this:

| Verdict | Definition |
|---|---|
| **PASS** | Conformant with Plotly's documented configuration surface alone. |
| **PASS (configured around)** | Conformant via **post-render DOM augmentation** over Plotly's emitted SVG plus its **public event/API surface**, where the augmentation **survives** a drill-in, a shape switch, a resize, and a `Plotly.react` update. |
| **FAIL** | Conformance requires forking/patching Plotly internals, **or** the augmentation is destroyed by Plotly's own re-render with no supported hook to reapply it, **or** it is unreachable at all. |

The load-bearing case is UX-DR7 keyboard traversal: Plotly hierarchy traces are **not** keyboard-focusable
per-node by default. So the real experiment is: *can a roving-tabindex layer be applied over Plotly's emitted
`<path>` nodes and survive Plotly re-rendering them?* Answer that one question well and AC #2 is essentially
discharged. Note the stakes are higher than they were before ADR 0013 — **there is no server-rendered SVG behind
the chart any more**, so what fails here fails with nothing beneath it except the text twin.

**What each UX-DR means here** (this project's own usage, not a generic reading):

| | Requirement | Test |
|---|---|---|
| **UX-DR5/6** | Breadcrumb drill-up + URL-hash deep-link | Not in this spike's ACs — that is Story 20.5. Note feasibility only if it costs you nothing. |
| **UX-DR7** | Tab order, Enter/Space to drill, Escape to go up; per-node accessible names | The crux. Apply the decision rule above. |
| **UX-DR16** | Keyboard/screen-reader floor — landmarks, whole-chart accessible name, announced state | Does the Plotly container get a real `role`/name, and can drill-scope changes be announced? |
| **UX-DR17** | **Status is never signalled by color alone** | With colorways disabled and `--status-*` driving fills, is every status still readable as text/shape? |
| **UX-DR18** | `prefers-reduced-motion` — transitions snap, nothing loops | Find Plotly's transition knob for sunburst/treemap drill and prove it can be set to zero duration. |

## Tasks / Subtasks

- [x] **Task 1 — Branch, quarantine, baseline** (AC: #1, #3)
  - [x] Work on an isolated spike branch or worktree (e.g. `spike/plotly-20-4`); do **NOT** develop on `main` — it carries a background auto-committer and concurrent sessions ([[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]]). Confirm `baseline_commit` in the frontmatter matches the HEAD you branch off (`6e12d0d` at authoring time). If you use a worktree, **re-root every relative path at the worktree**, not at `C:\Dev\SpecScribe`.
  - [x] All throwaway code lives under `spike/plotly/` per [`spike/README.md`](../../spike/README.md). **Nothing** joins `SpecScribe.slnx`, `dotnet build src/SpecScribe`, `dotnet pack`, or the `extension/` bundle. Add `node_modules/`, `dist/` to `spike/.gitignore`.
  - [x] **Capture the SVG-side baseline before touching anything.** Generate this repo at `--deep-git` scale into `SpecScribeOutput/` ([[generate-output-dir-is-specscribeoutput]] — never `--output docs/live`). Record: total portal bytes, per-page bytes for every surface in the Epic 20 rollout inventory, `code-map.html`'s **current** size (R4), and the byte size of each `sunburst-explorer-data` island (R6).
  - [x] Record the toolchain (Node/npm versions). Node is **build-time-only and developer-side** today (`extension/`, `tools/prism-vendor/`) and `specscribe generate` does not need it — the Plotly custom build must keep that property (it is a hand-run vendoring step, exactly like `prism-vendor`).

- [x] **Task 2 — Build and size the custom Plotly bundle** (AC: #1)
  - [x] Stand up a `tools/plotly-vendor/`-shaped throwaway workspace under `spike/plotly/` that mirrors [`tools/prism-vendor/`](../../tools/prism-vendor/) (R8): `package.json` with `plotly.js` as a **devDependency**, a `build.js`, a `README.md` explaining regeneration. Do **not** create the real `tools/plotly-vendor/` yet — that is Story 20.5/20.7's to land with the component.
  - [x] Build **both** variants (R1, R2) and record the exact trace list each actually contains:
    - `npm run custom-bundle -- --traces sunburst,treemap,heatmap --out specscribe-hierarchy`
    - `npm run custom-bundle -- --traces sunburst,treemap,heatmap --strict --out specscribe-hierarchy-strict`
  - [x] Report each bundle **minified** and **min+gzip**, as a multiple of the shipped `assets/prism.js` (100,409 B), and against Plotly's published full-bundle figures (v3.7.0: 4.6 MB min / 1.4 MB gz; full strict 1.5 MB gz).
  - [x] Confirm the bundle is a **single classic script** (no ES-module static imports) — this is what makes `<script nonce>` sufficient under the webview CSP (R7 #1) and matches how `prism.js` already ships.
  - [x] Note the license and provenance for the eventual NFR10 supply-chain audit (Epic 17): Plotly.js version, license, transitive footprint of the committed artifact (which should be zero — it is one self-contained file).

- [x] **Task 3 — Net output-size delta across a real portal** (AC: #1)
  - [x] For each surface in the Epic 20 rollout inventory (dashboard, epics, epic detail, story detail, Code Map, Git Insights ownership, Impact Map), measure the **inline chart SVG bytes** that Story 20.7 would remove. Reuse 23.1's `<main>`-region extraction method rather than eyeballing; 23.1 already established chart SVG at **69.3% of the dashboard body**.
  - [x] Estimate the **payload bytes that replace them** from the real 20.2 island where one exists (dashboard/epics: measured 20,915 B), and by projecting the same node shape (`id`, `parentId`, `label`, weight, `statusClass`, `href`, `kind`) for surfaces that do not have one yet. **Say which numbers are measured and which are projected** — do not blur them.
  - [x] Report `Σ(SVG removed) − Σ(payload added) − (one bundle)` for the whole portal, and separately for `code-map.html` alone (the surface with the most to gain). State the **break-even page count** — the point at which the one-time bundle is amortized. This is the single most decision-relevant number in AC #1.
  - [x] If the delta is **not** a reduction, say so plainly and prominently. ADR 0012 §"Spike validation" #2 says it *"is expected to be a reduction; it must be verified, not assumed"* — an unexpected result here is the spike doing its job, not a failure.

- [~] **Task 4 — Offline and `file://`** (AC: #1) — partial, see below
  - [ ] Render the probe from a local `file://` path with **no server running and networking disabled**. Confirm the chart draws. Any fetch/XHR, any CDN reference, any `import` of a sibling chunk is a **finding**, not a detail — NFR-3 local-first and the portal is routinely opened as loose files. **NOT PERFORMED — the in-app preview pane gave no live `file://` context and no external browser was connected this session (spike-report.md §3.4/§9). Structural evidence is strong (zero requests, zero imports measured over HTTP) but the `file://` run itself is owed; repro step given in §3.4.**
  - [x] Confirm no telemetry / no outbound request (`read_network_requests` on the preview, or DevTools network with the network disabled).

- [x] **Task 5 — Packaging impact** (AC: #1) — honor R5's measured-vs-design-level split
  - [x] **VSIX (measurable):** run `npm run package` in [`extension/`](../../extension/package.json) with and without the vendored bundle included, and report the byte delta. This is the only channel with a real pipeline today.
  - [x] **Self-contained binary + npx (design-level):** state that Epic 16 is entirely `backlog`, that the asset rides the existing `EmbeddedResource` + conditional `CopyEmbeddedAsset` path (R8), and report the delta to the packed artifact (`dotnet pack`) as the proxy. Label it design-level analysis, not measurement.
  - [x] Confirm the conditional-emission property holds: a generated site with **no** hierarchy chart must not receive the bundle, exactly as `prism.js` is emitted only when code pages exist. This keeps existing outputs byte-identical and is a real constraint on 20.7.

- [x] **Task 6 — Webview CSP** (AC: #2) — escalation trigger (a)
  - [x] Replay the **byte-verbatim** shipped policy string from [`WebviewRenderAdapter.cs:113`](../../src/SpecScribe/WebviewRenderAdapter.cs) over the real probe output in a real browser, reusing 23.1's [`csp-probe.mjs`](../../spike/nuxt-ir/scripts/csp-probe.mjs) harness shape.
  - [x] Report the **script axis** and the **style axis separately** (R3). Specifically: does the standard bundle need `'unsafe-eval'`? Does the `--strict` bundle avoid it? Is the already-present `style-src 'unsafe-inline'` sufficient for Plotly's runtime `<style>` injection, or would a nonce/hash be needed?
  - [x] Test the **partial-relaxation** state explicitly (R7 #3) — under ADR 0013 there is no SVG beneath the chart, so a half-fixed policy yields a **blank chart region**, not a degraded one. Report what a JS-blocked / CSP-blocked webview visitor actually sees.
  - [x] Account for [`WebviewRenderAdapter.cs:79`](../../src/SpecScribe/WebviewRenderAdapter.cs) stripping every `<script type="application/json">` island: state what would have to change for the webview to receive the payload at all.
  - [x] **Carry the honesty boundary** (R7 #4): `<meta>` delivery ≠ header delivery; `vscode-resource:` asset delivery untested; no Electron paint. Your verdict is a **lower bound** on the webview gap.
  - [x] **Do NOT author or land the ADR 0005 amendment.** Produce the evidence; ADR 0012 §5 says it lands **once**, jointly with Story 23.4's owed amendment.

- [x] **Task 7 — Accessibility pass/fail** (AC: #2) — escalation trigger (b), the highest-value axis
  - [x] Render the probe in a **live browser** (CLAUDE.md § Verification — the test suite structurally cannot see this) and produce a table with a literal **PASS / PASS (configured around) / FAIL** per UX-DR7, UX-DR16, UX-DR17, UX-DR18, applying the decision rule above.
  - [x] **UX-DR7 is the crux.** Determine whether per-node keyboard focus is reachable, and whether a roving-tabindex layer over Plotly's emitted `<path>` nodes **survives** drill-in, shape switch, resize, and `Plotly.react`. Test each of those four events explicitly — a layer that works until the first drill is a **FAIL**, not a pass.
  - [x] **UX-DR18:** find the concrete config knob that zeroes Plotly's sunburst/treemap drill transition, and confirm it can be driven from a `prefers-reduced-motion` media query at runtime (the existing `--motion-*` token idiom + paired reduced-motion blocks — [[motion-token-system]]).
  - [x] **UX-DR17:** with colorways disabled and `--status-*` driving fills, confirm every status remains readable as text/shape, not color.
  - [x] **UX-DR16:** confirm the chart container can carry a real accessible name and that drill-scope changes can be announced (live region).
  - [x] **If any verdict is FAIL:** say so in the report's opening summary, name it as ADR-0012-reopening per ADR 0012 §"Spike validation" #4, and **escalate via `correct-course`** rather than softening it. Do not bury it in a task note ([[adr-creation-trigger-gap-epic-10-retro]]).

- [x] **Task 8 — Tokens and colorways** (AC: #3)
  - [x] Render a representative hierarchy from the **real** `sunburst-explorer-data` island (R6), colored entirely from the **shipped** `specscribe.css` `--status-*` and brand token values — import the stylesheet, do not re-type values (AD-7 drift; this is the discipline 23.1 used and called out as "drift-free by construction").
  - [x] Disable Plotly's default colorways and **demonstrate** no default color reaches the output (compute styles live; do not assert from config).
  - [x] Note the six-token status system ([[specscribe-status-token-system]]) is the single stage→color source — the eventual component routes through it, so confirm nothing about Plotly's color model prevents that.

- [x] **Task 9 — Write the spike report and the ADR 0012 addendum** (AC: #1, #2, #3)
  - [x] Write `_bmad-output/implementation-artifacts/20-4-spike-report.md`, mirroring the structure of [`23-1-spike-report.md`](./23-1-spike-report.md) and [`22-1-spike-report.md`](./22-1-spike-report.md): Context · Method · Measured Evidence (per axis) · Findings · **AC coverage table with explicit boundaries** · What was NOT done.
  - [x] **Label every number's provenance** — harness-derived / session-measured / projected. 23.1's report had to be corrected post-review for exactly this; do not repeat it.
  - [x] Append an **addendum section to [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)** recording the measurements against its §"Spike validation" items 1–6, plus the R1 trace-floor correction. **The ADR's Status stays `Accepted`** — this validates, it does not ratify. If a FAIL fires trigger (b), the addendum states it and `correct-course` handles the ADR change, not this story.
  - [x] State what each finding **hands to 20.5/20.6/20.7** — the bundle variant chosen, the a11y augmentation approach, the webview disposition (render vs text twin), and the per-surface size expectation.
  - [x] Verify by `git` that `src/SpecScribe/**` and `tests/**` are untouched (R9). Do **not** offer the golden fingerprint as evidence — it is structurally vacuous here.

- [x] **Task 10 — Completion Notes** (AC: #1, #2, #3)
  - [x] Record: the R1 trace-floor correction, the bundle variant recommended, the current `code-map.html` size vs the historical 82.5 MB, the CSP verdict with its boundary, the a11y pass/fail table, whether either escalation trigger fired, and the timebox actually spent.

### Review Findings

Reviewed 2026-07-24 via `bmad-code-review`, scoped to Story 20.4's own File List (Blind Hunter + Edge Case Hunter + Acceptance Auditor, run in parallel; `review_mode = full` against this story file). Sibling-story content bundled into the same commits (`8db18aa`, `9369ca4` — Stories 20.1/20.2/20.3/5.2/5.3/5.5/5.6/24.6) was excluded per CLAUDE.md's scope-by-File-List rule; `spike/plotly/package-lock.json` and `spike/plotly/measurements/*.json` were excluded from line-by-line review as generated/raw-data, not hand-authored logic.

**decision-needed:** 0 · **patch:** 15 · **defer:** 0 · **dismissed as noise:** 9

**All 15 patches applied 2026-07-25** — edits landed in `20-4-spike-report.md` (§3.1, §3.3, §4, §5.1, §5.4, §7, §9), `spike/plotly/package.json`, `.claude/launch.json` (new `plotly-csp-unsafe-eval` entry), Task 4's checklist item above, and the probe scripts `build-probe.mjs`/`csp-probe.mjs` (nonce substitution) and `measure-baseline.mjs` (missing-input warnings). No headline byte figures changed — all fixes are report-clarity, checklist-accuracy, and script-robustness corrections; none alter a reported measurement.

- [x] [Review][Patch] Break-even "27 of 130 pages" is an unweighted average that masks a 2-page concentration — `code-map.html` (−3,493,000 B) and `git-insights.html` (−1,510,735 B) alone account for ≈105% of the −4,787,124 B net delta, so the other 128 pages plausibly net near-zero or negative individually; the report never orders pages by actual savings. [20-4-spike-report.md: "NET OUTPUT-SIZE DELTA" section; spike/plotly/scripts/measure-baseline.mjs:247-250]
- [x] [Review][Patch] Bundle table still labels the recommended build "4 traces" one paragraph after correcting the true floor to 5 modules (`core`+`heatmap`+`sunburst`+`treemap`+`calendars`) — internally inconsistent, unreconciled. [20-4-spike-report.md:80,86]
- [x] [Review][Patch] CSP headline/Completion Notes state the trigger "did not fire" unqualified; the report's own §4.5 lower-bound caveat (`vscode-resource:`/Electron untested, narrower than Story 23.1's coverage) doesn't travel with the headline. [20-4-spike-report.md headline table; §4.5]
- [x] [Review][Patch] UX-DR18 headline row doesn't disclose that the real click-driven drill never animates at all (`redraw()` always calls `Plotly.react`, which never animates) — the PASS reflects "nothing to suppress," not a suppressed transition; the report's own §5.4 body says this but the table row doesn't. [20-4-spike-report.md:342; §5.4]
- [x] [Review][Patch] UX-DR7's DOM-order-vs-ring-order Tab-order gap is filed under "Refinements 20.5 owes (none of them change the verdict)" rather than flagged against the PASS verdict it actually qualifies. [20-4-spike-report.md:379-382]
- [x] [Review][Patch] `spike/plotly/package.json`'s `"build": "node build.js"` script references a file that was never created — Task 2 explicitly asked for a `build.js` mirroring `tools/prism-vendor/`'s shape; `npm run build` fails today. [spike/plotly/package.json:6]
- [x] [Review][Patch] `.claude/launch.json` ships 3 of `csp-probe.mjs`'s 4 CSP variants (`webview`, `off`, `no-inline-style`); missing `plotly-csp-unsafe-eval`, the one variant that would let a future reviewer directly reproduce the "no `unsafe-eval` needed" claim via one click. [.claude/launch.json]
- [x] [Review][Patch] Task 4's checklist item is checked `[x]` though §3.4/Completion Notes state the `file://` run was **not** performed (structural evidence only, no live run). A reader trusting the checklist alone would believe the obligation was discharged. [20-4-plotly-engine-adoption-spike.md Task 4; 20-4-spike-report.md §3.4]
- [x] [Review][Patch] The report's boundaries section (§9) names the missing `file://` run and missing screenshot but not the absence of any real screen-reader/AT run — all four a11y PASS verdicts rest solely on DOM/computed-style introspection, which is arguably the single most consequential unstated caveat given AC #2 is the ADR-reopening trigger. [20-4-spike-report.md §9]
- [x] [Review][Patch] A rapid/overlapping re-render (two drills back-to-back, or a drill immediately followed by a resize, before the prior `plotly_afterplot` settles) is untested by the survival harness and not named among the report's boundaries, despite being exactly the class of risk Task 7 exists to find. [spike/plotly/probe-src/survival.js; 20-4-spike-report.md §9]
- [x] [Review][Patch] "10/10 survival steps intact" language counts 2 non-survival snapshots (initial render, keyboard reachability) alongside the 8 rows that actually test re-render survival — used elsewhere as shorthand for "ten hazards cleared." [20-4-spike-report.md §5.1; spike/plotly/probe-src/survival.js:57-66]
- [x] [Review][Patch] `build-probe.mjs`/`csp-probe.mjs`'s `__NONCE__` substitution uses `.replace('__NONCE__', n)` — a plain string, not a `/g` regex — so only the first occurrence is replaced. The shipped CSP has exactly one `__NONCE__` today so this didn't fire, but it silently breaks the "drift-free by construction, read from source" guarantee the moment a second nonce placeholder is added. [spike/plotly/scripts/build-probe.mjs:107,112; spike/plotly/scripts/csp-probe.mjs:36]
- [x] [Review][Patch] `measure-baseline.mjs` silently prices every projected page's payload at 0 B (via `bytesPerNode ?? 0`) if no page carries a real `sunburst-explorer-data` island, with no warning printed — this protects the single most decision-relevant number in the report (net delta) with zero self-check. [spike/plotly/scripts/measure-baseline.mjs:229-231,245]
- [x] [Review][Patch] `measure-baseline.mjs` silently treats the vendored bundle's one-time cost as 0 B (via `bundleMin ?? 0`) if the bundle file is missing on disk, with no warning printed — same headline-number exposure as above. [spike/plotly/scripts/measure-baseline.mjs:225-226,247]
- [x] [Review][Patch] `explorer.js`'s roving-tabindex `state.focusIndex` isn't reclamped after a drill shrinks the sector count, so tabbing into a sector whose index exceeds the new (smaller) child count leaves **no** element focusable — didn't fire in this session's actual run (the tested epic's index stayed in-bounds) but isn't documented as a hand-off risk for Story 20.5's real implementation. [spike/plotly/probe-src/explorer.js:242-246]

**Dismissed as noise / not actionable in 20.4's scope (9):**
- `sprint-status.yaml`'s diff bundles Story 20.3's edit to the stale top-of-file comment and Story 24.6's "blocked" status definition + entry — confirmed sibling-story content in a shared file, excluded per CLAUDE.md's scope-by-File-List rule, not a defect in this story's own work.
- Remaining `explorer.js`/`survival.js` robustness gaps against malformed data (unguarded `JSON.parse`, id-collision with the synthesized root, leaf-weight-null passthrough, unguarded `.focus()` in the survival harness) — real, but confined to disposable throwaway code; none fired against this session's actual data and none corrupted the reported results.
- UX-DR18's "PASS (configured around)" classification (event-interception via `plotly_<type>click`, not literal DOM augmentation) stretches a decision rule written with UX-DR7 in mind — already transparently disclosed in the report; a reader can independently judge the fit.
- VSIX packaging measured via a proxy (manual bundle drop into `extension/bin/` rather than the real `EmbeddedResource` inclusion path) — reasonable spike methodology; the resulting delta numbers independently verified accurate.
- "Conditional emission must be preserved or golden fixtures gain 1.2 MB" stated with declarative confidence — this is an explicit hand-off requirement for Story 20.7, not a claim that this spike validated it; appropriate framing for a spike.
- No accounting of whether every cited number came from one consistent snapshot of `main` — inherent to measuring on shared `main` under CLAUDE.md's accepted concurrent-work condition; the two actual instances of drift that occurred were caught and disclosed.
- Task 1's "work on an isolated branch/worktree" checkbox reads `[x]` despite the disclosed main-not-worktree deviation — the deviation is prominently bolded in the Debug Log immediately below; checkbox literalism isn't materially misleading here.
- `WebviewRenderAdapter.cs:113` citation is stale (the policy is now at line 116) — cosmetic; both harnesses read the policy live by regex at runtime, so no reported conclusion is affected.
- R1's "calendars" trace-list finding has no corresponding entry in `measurements/*.json`, unlike other `[HARNESS]`-tagged claims — independently re-verified true against the real generated `lib/index-specscribe-hierarchy.js`; a provenance-logging completeness nit, not an inaccuracy.

## Dev Notes

### Why this spike exists, in one paragraph

ADR 0010 §6 already required *"ONE shared engine/module… not independently reinvented per story."* **That rule did
not hold** — three concurrent sessions produced three arc renderers in one file, three divergent Treemap|Sunburst
toggles, and seven `Charts.cs` hierarchy entry points. The remedy is a shared **component**, not a shared
convention, and ADR 0012 chose Plotly to own the drawing. The owner named Plotly on 2026-07-22 (*"You can do this
via Plotly and it's amazing"*) with the epic body flagging it as needing *"its own dependency-budget decision at
spike time, not an assumed yes."* ADR 0012 is that decision; **this spike is the "not an assumed yes" part** —
the numbers that make a ratified direction defensible instead of merely chosen.

### The inventory this spike is sizing (verified in code 2026-07-24)

| Surface | Call site | Server-side entry points |
|---|---|---|
| Dashboard | `HtmlRenderAdapter.Dashboard.cs:54` | `Charts.Sunburst` + `SunburstCompanionList` |
| Epics | `HtmlRenderAdapter.Epics.cs:32` | `Charts.Sunburst` + `SunburstCompanionList` |
| Epic detail | `HtmlRenderAdapter.Epics.cs:208` | `Charts.EpicSunburst` |
| Story detail | `HtmlRenderAdapter.Epics.cs:550` | `Charts.TaskSunburst` |
| Code Map | `CodeMapTemplater.cs:152,158` | `Charts.CodeTreemap` + `CodeMapSunburst` |
| Git Insights (ownership) | `GitInsightsTemplater.cs:173,178` | `Charts.CodeOwnershipSunburst` + `CodeOwnershipTreemap` |
| Impact Map | `ImpactMapTemplater.cs:126` | client-rendered treemap/sunburst (Story 21.3) |

**Verified present at authoring time** (`Charts.cs`, 4,777 lines): `Sunburst` :379 · `EpicSunburst` :912 ·
`TaskSunburst` :1037 · `CodeTreemap` :2820 · `CodeMapSunburst` :3377 · `CodeOwnershipSunburst` :3838 ·
`CodeOwnershipTreemap` :3897.
**And in `assets/specscribe.js`** (1,961 lines / 98,114 B): `initOwnershipSunburst` :1208 · `renderSunburst` :1570
/ `arcPath` :1582 · `initSunburstExplorer` :1702 / `annular` :1749 / `fullRing` :1761.

These are what Story 20.7 deletes. **This spike deletes nothing** — it measures what deleting them would buy.

### Latest technical information (researched 2026-07-24)

- **Plotly.js current version: 3.7.0.** Full bundle **4.6 MB minified / 1.4 MB min+gzip**. Published partial
  bundles range 366 kB (basic) to 1.5 MB gz (strict, 36 traces). *(plotly.js `dist/README.md`)*
- **Custom bundle command:** `npm run custom-bundle -- --traces <list> [--strict] [--unminified] [--out <name>]`,
  emitting `dist/plotly-<name>.min.js`. *(plotly.js `CUSTOM_BUNDLE.md`)*
- **`scatter` cannot be excluded** from any bundle — it lives in `lib/core.js`. This is R1.
- **The `--strict` variant exists specifically for CSP** — it avoids the `Function`-constructor/eval paths that
  force `script-src 'unsafe-eval'`. Known caveat: `scattergl` still needed `unsafe-eval` even in strict, but
  `scattergl` is not in our trace list. This is R2 and it is the single highest-leverage experiment for Task 6.
- **Plotly injects inline CSS**, requiring `style-src 'unsafe-inline'` or a nonce/hash. The shipped webview policy
  already grants `'unsafe-inline'` for `style-src` — see R3.
- **Plotly's accessibility is its weakest dimension** (ADR 0012 §Consequences states this outright), and hierarchy
  traces are not keyboard-focusable per-node by default. Plan Task 7 around that, not around discovering it.

Sources: [plotly.js dist/README.md](https://github.com/plotly/plotly.js/blob/master/dist/README.md) ·
[plotly.js CUSTOM_BUNDLE.md](https://github.com/plotly/plotly.js/blob/master/CUSTOM_BUNDLE.md) ·
[plotly.js#4585 Document compatibility with CSP](https://github.com/plotly/plotly.js/issues/4585) ·
[plotly.js#7349 Improve CSP Documentation](https://github.com/plotly/plotly.js/issues/7349) ·
[plotly.js#2355 Plotly uses inline CSS](https://github.com/plotly/plotly.js/issues/2355)

### Architecture compliance

- **AD-7 / token discipline:** presentation is SpecScribe's tokens, never Plotly's colorways (ADR 0012 §6). Import
  the shipped stylesheet; never re-type a token value. [[specscribe-status-token-system]]
- **ADR 0010 §3 stands (unchanged by 0012):** data is computed **once at generation time and embedded** — never
  re-derived client-side from live git state or wall-clock "now." A probe that computes anything client-side is
  measuring the wrong architecture.
- **ADR 0010 §4 stands:** FR-10's no-productivity-ranking constraint is unaffected by rendering technology.
- **NFR-3 local-first:** vendored, never CDN, `file://`-safe. Non-negotiable, and Task 4 is its test.
- **NFR-5 as amended (PRD § 8, landed 2026-07-24):** *information and navigation* must survive JS-off;
  **visualization need not**, provided a server-rendered text equivalent carries the information. This is why a
  chart that needs JS is now acceptable at all — and why Story 20.6's twin audit is a hard gate, not this spike's
  problem.
- **NFR10 supply-chain (Epic 17):** this is the project's **first third-party runtime dependency**. Record version,
  license, and artifact provenance so the eventual audit has something to audit.

### Anti-patterns to prevent

- **Re-litigating the engine choice.** ADR 0012 is Accepted. Only a hard a11y FAIL reopens it, and only via
  `correct-course`. A CSP failure does **not**.
- **Softening the a11y verdict into prose.** "Mostly works with some effort" is not a verdict. Use the table.
- **Quoting 82.5 MB as today's `code-map.html` size** (R4).
- **Presenting a design-level packaging estimate as a measurement** (R5).
- **Building the real `tools/plotly-vendor/` or touching `src/SpecScribe/**`.** Vendoring for real is Story
  20.5/20.7. This spike ships **no production code**.
- **Landing the ADR 0005 CSP amendment.** Shared with 23.4; lands once (ADR 0012 §5).
- **Deleting a renderer, an entry point, or an SVG.** That is 20.7, and it is gated per-surface on 20.6.
- **Collapsing the script and style CSP axes into one verdict** (R3).
- **Offering the golden fingerprint as evidence of non-invasiveness** (R9).
- **Working on `main`** — background auto-committer + concurrent sessions.
- **Generating to `docs/live`** — vestigial and gitignored ([[generate-output-dir-is-specscribeoutput]]).

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/20-4-plotly-engine-adoption-spike.md`
- Sprint key: `20-4-plotly-engine-adoption-spike`
- **Durable deliverables:** `_bmad-output/implementation-artifacts/20-4-spike-report.md` + an addendum section on
  `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`
- Throwaway probe: `spike/plotly/**` (quarantined per [`spike/README.md`](../../spike/README.md))
- Vendoring precedent to mirror: `tools/prism-vendor/{build.js,package.json,README.md}` → `src/SpecScribe/assets/prism.js`
- Embedding + conditional emission precedent: `SpecScribe.csproj:62-63`, `SiteGenerator.cs:1779-1780`
- Payload island (real, shipped): `src/SpecScribe/SunburstExplorer.cs` (`SunburstExplorerDataId` :48)
- Webview CSP + island stripping: `src/SpecScribe/WebviewRenderAdapter.cs:113` (policy), `:79` (island strip regex)
- CSP probe harness to reuse: `spike/nuxt-ir/scripts/csp-probe.mjs`, `spike/nuxt-ir/scripts/measure.mjs`
- VSIX pipeline (the one real packaging channel): `extension/package.json:283`
- Sanctioned cross-surface divergence registry (where a webview text-twin fallback would eventually be recorded —
  **not by this story**): `src/SpecScribe/HostRenderException.cs`

### Testing standards summary

- **No new tests ship** — this story adds no production code, so there is nothing to unit-test. If you find
  yourself writing a test in `tests/**`, you have left the spike.
- **Live-browser verification is mandatory** for Tasks 4, 6, 7, and 8 (CLAUDE.md § Verification). The suite
  structurally cannot see CSP behavior, keyboard focus survival, computed colors, or what a JS-off visitor gets —
  all three defects that shipped past 2,158 green tests were caught only by looking at the rendered page.
- Run the suite once at the end to confirm you did not disturb it. If `GenerateAll_GoldenContentFingerprint_…`
  fails, verify it fails identically on a clean worktree of the baseline before spending time (R9) — 23.1 reported
  it in pre-existing stale drift on `main`.
- Determinism (FR31) is not exercised by this spike; the probe is not part of generation.

### Previous story intelligence

- **Story 23.1 (`done`, spike):** The methodological parent. Its report structure, its `<main>`-region extraction,
  its byte-verbatim CSP replay, its provenance labelling, and its "honesty boundary" section are all directly
  reusable — and its post-review corrections are the exact mistakes to not repeat (a claimed-reproducible number
  that was not, a structurally vacuous fingerprint claim, a warm-build headline hiding a 3× cold path, a
  distribution-channel analysis that got the Node-availability premise backwards). It also measured the two
  numbers this spike builds on: **chart SVG = 69.3% of the dashboard body**, and **the 20.2 island = 20,915 B**.
  [[story-23-1-nuxt-over-ir-spike-seeded]]
- **Story 22.1 (`review`, spike):** The report-structure ancestor 23.1 mirrored. No production code shipped; the
  durable output was the report. Same discipline applies here. [[story-22-1-ir-incremental-spike-done]]
- **Story 6.6 (`done`, spike):** Where the 82.5 MB `code-map.html` and 112.9 MB `pages-root.json` figures
  originate — and both were subsequently mitigated (2026-07-21), which is R4. Also the ADR 0006 delivery decision
  this spike's packaging analysis sits inside. [[story-6-6-deferred-cleanup-done-spa-at-scale-perf]]
- **Story 20.2 (`review`, BUILT):** Shipped `SunburstExplorer.cs`, the `sunburst-explorer-data` island, and the
  `data-explorer` root. Its node shape (`id`, `parentId`, `label`, weight, `statusClass`, `href`, `kind`) is
  exactly what ADR 0012 §2 adopts as the component's datasource — so the payload side of the size question is
  already real code, not a projection. [[story-20-2-zoomable-drill-in-done]]
- **Story 20.1 (`review`, spike):** Recommended zero-dependency client JS. **That recommendation is superseded by
  ADR 0012** — do not treat it as current authority, and do not re-litigate it. Its degrade contract is superseded
  by ADR 0013. [[story-20-1-interactive-explorer-spike-seeded]]
- **Story 7.1 (`done`):** Established the Prism vendoring path this spike's packaging analysis mirrors (custom
  driver, hand-run build, committed artifact, conditional emission).
- **Story 21.3 (`done`):** Its interactive treemap cited a stale memory over a two-day-old ADR that already
  permitted what it thought it was crossing. **Read `docs/adrs/` before declaring you are crossing a project
  rule** — here, ADRs 0012 and 0013 are the authority and the pure-SVG memory is explicitly superseded for
  hierarchy charts. [[adr-consultation-gap-three-arc-renderers]] · [[charting-is-pure-svg-no-js]]

### Git intelligence summary

HEAD at create-story is `6e12d0d` ("5.1, course correction on epic 20") — the commit that applied SCP 2026-07-24,
so ADRs 0012/0013, the amended PRD NFR-5, and the rewritten Epic 20 are all **on `main` and current**. Preceding
commits: `0f0af50` (Epics 19+21 retro — the retro that seated the *old* 20.4), `2be7f6d` (Story 23.1),
`7d8ce24` (22.2 folding in 23.1's SpaDelivery findings), `268485b`-era 23.1 spike merge. The working tree at
authoring time had uncommitted edits to the retro doc and `sprint-status.yaml` — **assume a concurrent session**
(CLAUDE.md § Concurrent work): grep-verify any symbol you rely on rather than trusting a prior read, and never
`git reset --hard` / `git checkout --` / `git clean` to tidy up.

### References

- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md` — §1 vendored/never-CDN; §2 the component contract + 20.2 node shape; §3 `navigate`|`select`; §4 engine-family boundary; §5 the ADR 0005 CSP amendment (shared with 23.4, land once); §6 tokens not colorways; §7 ADR 0010 §3/§4 stand; §"Spike validation" items 1–6 = this story's measurement list; §"Ratified decisions" #7 = the old 20.4's invalidation]
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md` — §1 amended NFR-5; §2 twin is contract; §3 the hard per-surface gate (Story 20.6); §6 the golden-fingerprint replacement; §7 webview text-twin fallback]
- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 20 body (~L3070), rollout inventory table (~L3089), Story 20.4 ACs (~L3191-3215), the 2026-07-24 replacement note (~L3181)]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-24.md` — §1 the verified three-way drift evidence; §2.3 the 69.3% figure and the "must be measured, not assumed" size claim; §2.4 the risk table naming this story's pass/fail obligation]
- [Source: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md:227` — NFR-5 as amended]
- [Source: `_bmad-output/implementation-artifacts/23-1-spike-report.md` — Axis 3 CSP matrix + honesty boundary + the ADR 0005 security note; Axis 2 the 69.3%/island measurements; the provenance-labelling and AC-coverage-table conventions]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:453` — the 82.5 MB `code-map.html` item and its 2026-07-21 partial resolution via `Charts.MaxDetailedCodeMapFiles`]
- [Source: `src/SpecScribe/WebviewRenderAdapter.cs:113` (CSP policy string), `:79` (JSON-island strip regex)]
- [Source: `src/SpecScribe/SunburstExplorer.cs:48` — `SunburstExplorerDataId`; the shipped 20.2 payload island]
- [Source: `src/SpecScribe/Charts.cs` — the seven hierarchy entry points at :379/:912/:1037/:2820/:3377/:3838/:3897]
- [Source: `src/SpecScribe/assets/specscribe.js` — the three arc renderers at :1208/:1570/:1702]
- [Source: `tools/prism-vendor/{README.md,build.js,package.json}` + `src/SpecScribe/SpecScribe.csproj:62-63` + `src/SpecScribe/SiteGenerator.cs:1779-1780` — the vendoring, embedding, and conditional-emission precedent]
- [Source: `extension/package.json:283` — the working VSIX package script]
- [Source: `spike/README.md` — the quarantine discipline; `spike/nuxt-ir/scripts/{csp-probe,measure}.mjs` — the harnesses to reuse]
- [Source: `CLAUDE.md` — § Verification (live-browser rule), § Concurrent work on shared `main`, § Decision records]

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Opus 5)

### Debug Log References

**Timebox spent: 1 session against a suggested 2 days.** All five axes measured; two boundaries reported as
unmeasured rather than half-measured (see Completion Notes).

**Deviation from Task 1's first subtask — worked on `main`, not a worktree.** The subtask says "do NOT develop on
`main`". [`CLAUDE.md` § Concurrent work](../../CLAUDE.md) — the project-level authority, which overrides — states
the opposite as settled fact: *"The primary machine cannot run parallel git worktrees, so isolation is not
available and is not the fix. This is an accepted working condition."* The substitute controls the subtask exists
to provide were applied instead: **every file this story created is under `spike/plotly/**`**, a directory that did
not previously exist and that no other session touches; nothing joins `SpecScribe.slnx`, `dotnet build`,
`dotnet pack`, or the `extension/` bundle; and each new symbol was grep-verified after writing. Flagged here rather
than buried because it is a knowing departure from the written task.

**The concurrency hazard was not hypothetical — it fired mid-spike.** The first `--deep-git` baseline was taken
against `SpecScribeOutput/`. Partway through, a concurrent session regenerated that directory *without*
`--deep-git`, deleting `git-insights.html` and `deep-analytics.html` out from under the in-flight measurement
(1,273 files / 89.7 MB → 964 HTML files / 59.5 MB). **All published baseline numbers were re-taken against a
session-private output directory** so they are stable and re-derivable. Do not compare them against whatever
`SpecScribeOutput/` contains now.

**The Release build this measurement sits on top of** carried a concurrent session's uncommitted edits to
`HowToReadTemplater.cs`, `SiteNav.cs` and three test files (their story is
`5-6-how-to-use-specscribe-cli-guidance`). None touches `Charts.cs` or any hierarchy renderer, so the SVG-side
figures are unaffected. Recorded because CLAUDE.md requires saying whose changes a measurement sits on.

**Two harness bugs worth recording, both of which would have produced a wrong verdict silently:**
1. `requestAnimationFrame` is throttled to a standstill when the preview pane is not compositing, so the first
   survival harness *hung* rather than failed — and so does `await Plotly.react(...)`, because Plotly resolves its
   promises off an animation frame. Rewritten to wait on the DOM via `plotly_afterplot`. This turned into
   **Finding E** in the report: the event is the right seam anyway, because it is the only one that fires for
   re-renders the component did not initiate.
2. The first node-count pass matched the wrong CSS classes and undercounted the code-map and ownership charts by
   ~5,900 nodes, which would have *understated* the payload side and *overstated* the win by ~1.15 MB. Corrected
   by enumerating the class attributes inside each extracted SVG on the real portal rather than inferring them
   from `Charts.cs`.

**Live-browser boundary:** the preview pane never composited a frame in this session, so **no screenshot could be
captured** and the in-app browser refuses a live `file://` context. All visual claims are computed-style,
DOM-geometry, focus-model and network evidence. Both gaps are named in the report §9 and in the ADR addendum's
Boundaries section; neither was papered over.

**Test suite after the work: 2,347 passed, 0 failed, 3 skipped** (`GenerateAll_GoldenContentFingerprint_…`
included — it did not move, as R9 predicted it structurally cannot).

### Completion Notes List

**Both escalation triggers were evaluated and NEITHER FIRED. ADR 0012 is not reopened; the §5 text-twin fallback
is not selected.** Durable deliverables: [`20-4-spike-report.md`](./20-4-spike-report.md) and the
addendum appended to [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
(**Status left `Accepted`**).

**R1 trace-floor correction — worse than the story predicted.** `npm run custom-bundle` resolves the list to
`heatmap, scatter, sunburst, treemap`, and the generated index also registers `calendars`. True floor: **`core`
(incl. `scatter`) + `heatmap` + `sunburst` + `treemap` + `calendars`** — five modules, not three.

**Bundle variant recommended: the STANDARD build, not `--strict`.** R2 named `--strict` as the highest-leverage
experiment. It resolved against expectation: **7 bytes larger, byte-identical CSP-construct profile**. The
`Function`-constructor paths `--strict` exists to avoid live in the gl/regl traces this build already excludes, so
trace exclusion did the job first. Standard bundle: **1,223,515 B min / 413,449 B gzip** — 12.19× `prism.js`
uncompressed, 4.12× gzipped, 25% of the full distribution.

**`code-map.html` is 5,682,933 B today (448,533 B gzipped), not 82.5 MB.** The 82.5 MB figure is Story 6.6's
2026-07-20 peak, mitigated 2026-07-21 by `Charts.MaxDetailedCodeMapFiles`. Both are reported; quoting the peak as
current would have overstated the single biggest win by 14×.

**Net output-size delta: −4,787,124 B across the portal — a REDUCTION, verified not assumed.** Σ SVG removed
8,484,973 B over 130 pages; Σ payload added 2,474,334 B over 12,660 nodes (1 page measured, 129 projected from the
measured 195.4 B/node basis); one 1,223,515 B bundle. **Break-even at 27 chart-carrying pages; the portal has 130.**
`code-map.html` −3,493,000 B and `git-insights.html` −1,510,735 B carry most of it. `impact-map.html` gains **zero**
bytes — Story 21.3 already made it client-rendered.

**CSP verdict, script and style axes reported separately (R3).** Byte-verbatim shipped policy, **header- AND
`<meta>`-delivered: full render, 119 sectors, 64 Plotly style rules applied, zero console errors, no
`'unsafe-eval'`, no relaxation.** Removing `style-src 'unsafe-inline'` **still renders** — Plotly detects the block
(`Cannot addRelatedStyleRule, probably due to strict CSP...`) and loses only hover cosmetics, so the style axis was
never the risk. **Boundary:** header + meta over `http://localhost`; `vscode-resource:` and an Electron paint are
untested, so the verdict is a **lower bound** on the webview gap (narrower than Story 23.1's, which could not test
meta at all). The ADR 0005 amendment was **not** authored — it lands once, jointly with Story 23.4, and the
evidence it will cite is that **no policy-string relaxation is required**.

**Partial-relaxation state (R7 #3) confirmed and it is as bad as feared:** a wrong nonce yields a literal
**640×640 empty box** — 0 bytes of chart HTML, 0 SVGs, 0 anchors. Under ADR 0013 there is nothing beneath. Also
recorded for Story 20.6: **CSP violations did not surface in the browser tooling's console capture** — a twin audit
that greps the console will pass while the chart is blank.

**Accessibility pass/fail table — no FAIL:**

| UX-DR | Verdict |
|---|---|
| **UX-DR7** (Tab order, Enter/Space drill, Escape up, per-node names) | **PASS (configured around)** |
| **UX-DR16** (chart name, announced state) | **PASS** |
| **UX-DR17** (never color alone) | **PASS** |
| **UX-DR18** (reduced motion) | **PASS (configured around)** |

UX-DR7 was the crux and it held: a roving-tabindex layer applied **only** through `plotly_afterplot` over Plotly's
emitted DOM survived **10/10** steps — drill-in, drill-up, shape switch, drill inside treemap, resize, and a bare
`Plotly.react`/`Plotly.relayout` **the component did not initiate**. That last case is what makes the verdict
trustworthy; a layer surviving only its own `redraw()` would have been a FAIL under the decision rule. Measured
under the shipped webview CSP. No Plotly internal was patched or forked.

**UX-DR18's knob does not exist — the workaround does.** Plotly's drill animation is `CLICK_TRANSITION_TIME: 750`
in `src/traces/sunburst/constants.js`, a module constant with **no attribute in the trace schema**. A config-only
search would have concluded UX-DR18 unreachable. It is reachable through the event surface: returning `false` from
`plotly_<type>click` cancels the animation and the event carries `nextLevel`. Measured on a real mouse click:
**0 `Plotly.animate` calls**, drill still happened, layer reapplied. No `@keyframes`, no CSS animation, zero SVG
`<animate>` elements.

**AC #3 demonstrated, not asserted — and it took three fixes to reach zero foreign colors.** (1) `marker.pattern`
with no `bgcolor` paints its backing rect **black** (67 occurrences). (2) The root label alone took Plotly's
default `rgb(68,68,68)` — `insidetextfont` is not enough, `outsidetextfont` and `layout.font.color` are needed.
(3) **Plotly emits the hatch `<path>` inside every `<pattern>` with a stroke but NO fill**, so SVG's initial black
paints beneath it (21 occurrences) — **no Plotly attribute exists for this**; the fix is one CSS rule,
`defs pattern > path { fill: none; }`, which took the foreign-color count 1 → 0. **Story 20.5 must ship that rule.**
Also: the shipped chart's stroke-dash channel cannot be expressed by Plotly's `marker.line`; **`marker.pattern`
hatching replaces it** and is stronger.

**Four data-contract defects — the most valuable thing this spike produced, none of them anticipated:**
1. **Plotly hierarchy traces need exactly ONE root.** The 20.2 island is a **25-root forest** and Plotly refuses it
   outright. A project root must be synthesized.
2. **Parent weight ≠ Σ children** (14 of 25 parents disagree) because an epic's weight counts stories while its
   children also include `aggregate` follow-ups. `branchvalues: 'total'` is invalid. **A visible-geometry decision
   Story 20.5 owes.** The hand-rolled SVG never noticed because it scales each ring independently.
3. **A single `null` in `values` silently renders NOTHING** — calcdata collapses to one point, no error, no
   warning. Branch values must be `0`.
4. **`npm run custom-bundle` is unavailable from the npm package** (`plotly.js@3.7.0` ships no `tasks/`).
   Vendoring needs `git clone --branch v3.7.0 --depth 1`, so `tools/plotly-vendor/` cannot be a straight copy of
   `tools/prism-vendor/`'s shape.

**Packaging, split measured vs design-level (R5).** VSIX **measured**: 1,978,282 B → 2,392,561 B, **+414,279 B
(+20.9%)** — within 830 B of the standalone gzip size, so the packaged cost is just "the gzip number". Both
artifacts deleted; `git status` on `extension/` clean. Binary and npx are **design-level**: Epic 16 is entirely
`backlog`. Global-tool nupkg baseline **1,877,099 B**, projected ≈ +22.1%. **Conditional emission
(`SiteGenerator.cs:1983`) must be preserved by Story 20.7** or every golden fixture gains 1.2 MB.

**Supply chain (NFR10/Epic 17):** plotly.js **3.7.0**, **MIT**, one self-contained classic script, **zero**
transitive runtime footprint, build-time `node_modules` throwaway and gitignored exactly like `tools/prism-vendor`.
`specscribe generate` still needs no Node. `npm audit` on the upstream clone's dev tree reports 9 vulnerabilities
(1 low, 1 moderate, 7 high) — **all in upstream build-time devDependencies, none in the emitted artifact**.

**Two axes NOT fully measured, stated plainly rather than half-measured:**
- **`file://`** — the in-app preview pane gives no live `file://` context and no external browser was connected.
  Structural evidence is strong (4 same-origin requests after the *full* interaction suite, zero external origins,
  zero `fetch`, zero ESM imports) but the run is **owed**: `start spike/plotly/probe/index.html` with networking
  disabled, confirm 119 sectors.
- **No pixel screenshot** — the preview pane never composited. A human eyeball is owed, and Story 20.5's
  create-story elicitation is the natural place for it since the silhouette is the owner's call there anyway.

**Nothing under `src/` or `tests/` was modified by this story.** The File List below is the load-bearing evidence;
the golden fingerprint is **not** offered (R9 — it builds from a synthetic temp fixture and structurally cannot
move for a spike; Story 23.1 had to retract that exact claim).

### File List

**New — durable:**
- `_bmad-output/implementation-artifacts/20-4-spike-report.md`

**Modified — durable:**
- `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md` (addendum appended; **Status unchanged, still `Accepted`**)
- `_bmad-output/implementation-artifacts/20-4-plotly-engine-adoption-spike.md` (this record)
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**New — throwaway probe, quarantined under `spike/plotly/` per `spike/README.md`:**
- `spike/plotly/README.md`
- `spike/plotly/.gitignore`
- `spike/plotly/package.json`
- `spike/plotly/package-lock.json`
- `spike/plotly/probe-src/explorer.js`
- `spike/plotly/probe-src/survival.js`
- `spike/plotly/scripts/build-probe.mjs`
- `spike/plotly/scripts/csp-probe.mjs`
- `spike/plotly/scripts/measure-baseline.mjs`
- `spike/plotly/scripts/measure-bundle.mjs`
- `spike/plotly/measurements/baseline.json`
- `spike/plotly/measurements/bundle.json`
- `spike/plotly/measurements/session.json`

**Modified — tooling only:**
- `.claude/launch.json` (three CSP-probe server entries: `plotly-csp-webview`, `plotly-csp-off`, `plotly-csp-no-inline-style`)

**Gitignored, not committed:** `spike/plotly/{node_modules,plotly-src,probe}/`.
**Not in this list, deliberately:** any path under `src/` or `tests/`. Modifications visible there in `git status`
belong to a concurrent session working on `5-6-how-to-use-specscribe-cli-guidance`.

## Change Log

- 2026-07-24 — Story 20.4 drafted (create-story) as the **replacement** for the invalidated "shared client-side geometry engine" story, per ADR 0012 Ratified decision #7 and SCP 2026-07-24. Ultimate context engine analysis completed — comprehensive developer guide created. Nine reconciliations recorded against shipped code and current library facts, four of which change what the dev would otherwise measure: **(R1)** Plotly's `scatter` trace cannot be excluded from any custom bundle, so ADR 0012's "sunburst+treemap+heatmap only" is not literally achievable — report the true floor; **(R2)** Plotly ships a `--strict` bundle mode built specifically for CSP compliance (avoids the `Function`-constructor/eval paths) — build and report both variants, and try strict first on the CSP axis; **(R3)** the shipped webview CSP **already grants `style-src 'unsafe-inline'`** (`WebviewRenderAdapter.cs:113`), so ADR 0012 §5's style-injection worry is plausibly already satisfied — the script and style axes must be reported separately; **(R4)** the 82.5 MB `code-map.html` figure quoted in AC #1/ADR 0012/sprint-status is a **historical peak already partially mitigated 2026-07-21** (`Charts.MaxDetailedCodeMapFiles`, `deferred-work.md:453`) — re-measure current size or the size win is overstated; **(R5)** Epic 16 is entirely `backlog`, so of AC #1's "three channels" only the **VSIX** has a real pipeline (`extension/package.json:283`) — binary and npx are design-level analysis, and must be labelled as such; **(R6)** Story 20.2 is **built** (`SunburstExplorer.cs`, island id `sunburst-explorer-data`), giving a real payload baseline (23.1 measured it at 20,915 B against chart SVG = 69.3% of the dashboard body); **(R7)** Story 23.1 already replayed this exact CSP string in a real browser — reuse its harness and inherit its honesty boundary (`<meta>` ≠ header delivery, `vscode-resource:` untested), and note the webview currently **strips** every JSON island (`:79`); **(R8)** `tools/prism-vendor/` is the exact vendoring precedent (hand-run build, committed artifact, `EmbeddedResource`, conditional `CopyEmbeddedAsset`) with `prism.js` at 100,409 B as the in-repo size yardstick; **(R9)** the golden fingerprint builds from a synthetic temp fixture and cannot move for a spike — do not repeat 23.1's retracted "structurally vacuous" claim. Added an operational **decision rule** for ADR 0012's otherwise-unfalsifiable phrase *"a hard a11y failure Plotly cannot be configured around"* (PASS / PASS-configured-around / FAIL, keyed on whether a roving-tabindex layer survives drill-in, shape switch, resize, and `Plotly.react`), since that verdict is the single finding that can reopen a ratified ADR. Web-researched current library facts (Plotly 3.7.0; full 4.6 MB min / 1.4 MB gz; `npm run custom-bundle -- --traces … [--strict]`; inline-CSS and eval CSP posture). Sequencing unchanged and strengthened: Story 20.7 must land before Story 24.2 begins.
- 2026-07-24 — **dev-story: Story 20.4 implemented → `review`. Both ADR 0012 escalation triggers evaluated; NEITHER FIRED — ADR 0012 stays `Accepted` and is not reopened, and the §5 text-twin fallback is not selected.** Durable outputs: `20-4-spike-report.md` + an addendum appended to ADR 0012. Throwaway probe quarantined under `spike/plotly/`; **no production code, no new tests** (suite after: 2,347 passed / 0 failed / 3 skipped). **Headline numbers** — custom bundle **1,223,515 B min / 413,449 B gzip** (12.19× `prism.js`, 25% of the full distribution); **net output-size delta −4,787,124 B across the portal, a REDUCTION**, break-even at **27** chart-carrying pages against **130** present; `code-map.html` **5,682,933 B today**, not the 82.5 MB historical peak (R4 honored, both reported); VSIX **+414,279 B (+20.9%)** measured, binary/npx design-level only (R5 honored — Epic 16 is entirely `backlog`). **R2 resolved against expectation: `--strict` buys nothing** (7 bytes larger, identical CSP-construct profile — the eval paths live in gl/regl traces already excluded), so the **standard** bundle is recommended. **R1 corrected upward**: the true floor is five modules (`core` incl. `scatter` + `heatmap` + `sunburst` + `treemap` + `calendars`), not three. **CSP PASS on both delivery mechanisms** — the byte-verbatim shipped policy renders Plotly fully as an HTTP header *and* as `<meta http-equiv>`, with no `'unsafe-eval'`; R3 confirmed (`style-src 'unsafe-inline'` was already granted) and the style axis proved not even load-bearing — with it removed Plotly detects the block and still renders. R7 #3 confirmed: a wrong nonce yields a literal **640×640 blank box** with nothing beneath it under ADR 0013, and **CSP violations do not appear in console captures** — a warning Story 20.6's twin audit must absorb. **Accessibility: no FAIL.** UX-DR7 **PASS (configured around)** — a roving-tabindex layer applied only through the public `plotly_afterplot` event survived **10/10** events including a bare `Plotly.react`/`Plotly.relayout` the component did not initiate; UX-DR16/17 **PASS**; UX-DR18 **PASS (configured around)** — Plotly's 750 ms drill is a module constant with no public attribute, but returning `false` from `plotly_<type>click` cancels it outright (measured: **0 `Plotly.animate` calls** on a real click). **AC #3 demonstrated over computed styles: zero foreign colors** — after fixing three leaks a config-level assertion would have missed, one of which (`<pattern>` hatch paths emitted with no `fill`, painting SVG's initial black) **has no Plotly attribute** and needs the CSS rule `defs pattern > path { fill: none; }` that Story 20.5 must ship. **Four unanticipated data-contract defects between the Story 20.2 island and Plotly's hierarchy model — the spike's most valuable output**, all blocking for 20.5: Plotly requires exactly ONE root (the island is a 25-root forest and is refused outright); parent weight ≠ Σ children (14 of 25 parents disagree, so `branchvalues:'total'` is invalid — a *visible-geometry* decision 20.5 owes); a single `null` in `values` silently renders nothing with no error; and `npm run custom-bundle` is unavailable from the npm package, so `tools/plotly-vendor/` cannot copy `tools/prism-vendor/`'s shape. **Two axes reported unmeasured rather than half-measured**: a literal `file://` run (the preview pane gives no live `file://` context and no external browser was connected; structural evidence strong — 4 same-origin requests after the full interaction suite, zero external origins) and a pixel screenshot (the pane never composited a frame). **Concurrency:** a concurrent session regenerated `SpecScribeOutput/` without `--deep-git` mid-measurement, so all baseline numbers were re-taken against a session-private output directory; the Release build sat on that session's uncommitted `HowToReadTemplater.cs`/`SiteNav.cs` edits, which touch no hierarchy renderer. Worked on `main` with `spike/plotly/**` quarantine rather than a worktree, per CLAUDE.md § Concurrent work overriding Task 1's subtask — flagged explicitly in the Debug Log.
