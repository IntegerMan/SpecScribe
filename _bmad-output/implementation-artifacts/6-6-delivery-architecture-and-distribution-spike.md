---
baseline_commit: 1c9270b069a7fbd22d210b880b8372b6223ef987
supersedes_decision: docs/adrs/0005-vs-code-webview-runtime-and-packaging.md
---

# Story 6.6: Delivery Architecture & Distribution Spike — JSON + SPA + npx vs. the C# Static-Site + Bundled-Binary Path

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the SpecScribe maintainer,
I want a hands-on spike that **measures** whether SpecScribe's delivery architecture should pivot toward a **JSON data layer + a small client-side renderer (SPA), distributed via npx** — versus the current **C# static-site generator + (per ADR 0005) a bundled self-contained binary** — **decided by real numbers, not argument**, and recorded as **ADR 0006**,
so that we either commit to the pivot with evidence (and re-plan Epics 6/16 accordingly) or re-affirm the C# path knowing exactly what we're trading away — before any code is rewritten and before Story 6.4 is built on a premise that may not hold.

## Why this spike exists — READ FIRST (owner-directed, 2026-07-10)

**ADR 0005 was Accepted on 2026-07-10 on the premise "rendering stays in C#, minimize TypeScript," with packaging = "bundle a ~73 MB/RID self-contained .NET binary in the VSIX."** Immediately after it landed, the owner (Matthew-Hope) reopened that premise, citing three concerns:

1. **Output-file bloat** — the static-site generator emits one HTML file per artifact (113 files for *this* repo today; Epic 7's per-source-file / per-commit / per-date pages multiply that into the thousands on a large repo).
2. **Extension bloat** — ADR 0005's answer to "just works on install" is a ~73 MB/RID self-contained binary bundled in the VSIX; the owner wants a **thin** rendering surface driven largely by a JSON layer, not a fat binary.
3. **CI/distribution friction** — a .NET tool needs the runtime or a self-contained publish; **npx** is far lower-friction for SpecScribe's spec-driven-dev / JS audience. (.NET 10's `dnx` / `dotnet tool exec` narrows but does not close the gap.)

The owner is **leaning toward pure TypeScript — possibly a SPA framework — for the application, distributed via npx**. This spike does **not** commit to that; it de-risks it, mirroring exactly how Story 6.3 de-risked the webview seam (memory: [[epic-4-adapter-contract-scope]], [[epic-6-vscode-spike-and-renumber]]). **The durable deliverable is ADR 0006** (supersedes-or-reaffirms ADR 0005); the code is disposable evidence.

**This spike GATES the rest of Epic 6 and Epic 16 packaging.** Stories 6.4 (webview runtime) and 6.5 (host theming) are **frozen** (demoted in [sprint-status.yaml](sprint-status.yaml)), and Epic 16's packaging stories (16.1/16.3/16.4/16.5) wait on ADR 0006, because all of them are built on ADR 0005's now-questioned foundation. See [sprint-change-proposal-2026-07-10-delivery-architecture.md](../planning-artifacts/sprint-change-proposal-2026-07-10-delivery-architecture.md).

## The decision is four separable axes — the spike's job is to measure each, not conflate them

The owner's "pure TypeScript SPA via npx" bundles four axes that are individually addressable. The spike must report on each **separately**, because only their *combination* forces a full rewrite:

| Axis | The two poles | Cheapest option that hits the concern |
|---|---|---|
| **A. Output form** | Many static HTML files ↔ few files + a JSON data layer a client renders | A JSON data layer + small renderer can be a **new C# delivery adapter** — the ADR-0005 spike already renders the whole dashboard as *one* 306 KB doc, proving C# can consolidate. Does **not** require TS. |
| **B. Rendering language** | C# emits finished HTML ↔ TS/SPA renders from JSON | Independent of A and D. |
| **C. Analysis language** | C# core stays (parsers/projection/git/coverage) ↔ ported to TS | The **expensive** axis: ~60 `.cs` files, 667 tests, the bulk of Epics 1–4 + 3 (Markdig, `GitMetrics` deep-git, coverage). |
| **D. Distribution** | `dotnet tool` / `dnx` ↔ npm / npx | An **npm wrapper around the self-contained binary** (the esbuild/Biome pattern) gives `npx specscribe` with no .NET SDK on CI and **no rewrite**. |

**The coupling the spike must confirm or break:** *thin extension (no 73 MB binary) + live in-editor updates + npx* ⇒ generation must run in Node ⇒ analysis in TS (axis C). If that chain holds, the pivot implies porting the C# core; if it can be broken (e.g. WASM-compiled core callable from Node, or a pre-generated JSON the extension just loads), the pivot is far cheaper. **Naming which of these is true is the single most valuable output of this spike.**

## Acceptance Criteria (spike — decision-first, throwaway, timeboxed)

1. **Output-form + bloat measured (axis A).** **Given** a thinnest end-to-end slice where the C# core emits a **JSON data layer** for the dashboard + epics section view models and a **minimal client renderer (a small SPA, framework or vanilla)** renders those two surfaces from it, **When** it runs against this repo, **Then** the spike **measures and records**: output-file count vs. today's static-site count (and an extrapolation to Epic-7 scale), total byte size, JSON payload size (and whether one blob or chunked/lazy-loaded is needed), and client render/interaction performance for the largest realistic dataset. It need not cover every section or be production-quality.

2. **npx distribution measured (axis D).** **Given** the owner wants `npx`-executable distribution, **When** the spike prototypes it, **Then** it proves end-to-end **at least the npm-wrapper-around-native-binary path** (`npx <pkg>` runs the self-contained `specscribe` with no .NET SDK present) — measuring package size, cold-run latency, and cross-platform story — **And** records how this compares to `dnx`/`dotnet tool` and to a hypothetical full-TS CLI (npm install size, no runtime dependency).

3. **Rewrite surface enumerated (axis C), not performed.** **Given** "pure TypeScript for the application" implies porting the analysis core, **When** the spike assesses it, **Then** it **enumerates** exactly what would need porting (parsers, projection, `GitMetrics`/deep-git, coverage classification, chart SVG generation, the 667-test suite), estimates the effort/risk, and evaluates the **coupling-breakers** (WASM-compiled C# core callable from Node — time-boxed feasibility, may be desk-research; or a pre-generated-JSON model where CI runs the C# tool once and the SPA/extension only consumes JSON). **No production port is written.**

4. **ADR 0006 recorded (the primary durable deliverable).** **Given** the measured evidence, **When** the spike concludes, **Then** a new **`docs/adrs/0006-*.md`** (format per [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md): Status/Date/Deciders/Context/Decision/Consequences/References) records the decision across all four axes and **explicitly supersedes-or-reaffirms ADR 0005** (its C#-renders-HTML data path + 73 MB bundled-binary packaging), with the numbers backing it. It also states the **accessibility posture** for any JS-rendered surfaces (the progressive-enhancement / "JS never carries information" policy at [rendering-architecture.md:84–92](../specs/spec-specscribe/rendering-architecture.md) and NFR6 are on the line — the ADR must rule: static fallback, `noscript`, or accept JS-required). **And** [docs/adrs/README.md](../../docs/adrs/README.md) is updated. If ADR 0006 supersedes 0005, 0005's header gets a `Superseded by ADR 0006` note.

5. **Downstream re-plan seated.** **Given** ADR 0006's decision, **When** the spike concludes, **Then** it names the concrete follow-on: if **pivot** → a `correct-course` to re-plan Epics 6 (6.4/6.5 reshaped or replaced) and 16 (packaging → npm/npx), and whether the C#-core-port is its own epic; if **re-affirm 0005** → unfreeze 6.4/6.5 and 16.1 unchanged. The spike need not execute the re-plan, but must make its scope unambiguous in the ADR + Completion Notes.

6. **No production pivot on `main`.** **Given** a spike produces throwaway code, **When** it lands, **Then** **no production rendering/CLI/extension pivot merges to `main` as product** — spike code stays on its branch or quarantined under `spike/` (excluded from the shipped tool's build/pack, like the 6.3 spike), **And** the generated site stays byte-identical (pinned `GoldenContentFingerprint` + the 5 benign normalizations — memory: [[golden-diff-normalization-gotchas]]), **And** read-only is honored (AD-6): no spike path writes source artifacts. The **only** artifacts landing on `main` are ADR 0006, the README index update, the 0005 supersede note, this story's completion record, and (optionally) `spike/README.md` additions.

## Tasks / Subtasks

- [x] **Task 1 — Branch + baseline** (AC: #6)
  - [x] Work on an isolated spike branch (e.g. `spike/delivery-arch-6-6`); do **not** develop on `main` (background auto-committer — memory: [[worktree-edits-must-target-worktree-path]]). Re-capture `baseline_commit` (frontmatter) from the HEAD you branch off. → branched off `1c9270b`; frontmatter re-captured.
  - [x] Reuse the 6.3 spike's quarantine discipline ([spike/README.md](../../spike/README.md)): nothing here joins the `.sln`, `dotnet pack`, or the site build. → all spike code under `spike/delivery/`; no `.sln` in repo; `dotnet build src/SpecScribe` never sees it.

- [x] **Task 2 — Axis A: JSON data layer + SPA slice, measure bloat** (AC: #1)
  - [x] Emit a **JSON data layer** for the dashboard + epics from Story 6.2's section view models. Heeded the 6.2 serialization gotcha: serialized only the clean DATA records; chart/domain-carrier panels excluded from the data layer and carried as pre-rendered inline-SVG strings in `bodies.json`. → `spike/delivery/exporter`.
  - [x] Build a **minimal client renderer** — a ~90-line vanilla-JS SPA (`spike/delivery/spa`) renders both surfaces from the JSON with in-view navigation; verified in the browser (fetch 35 ms, render 7.9 ms dashboard / 6.9 ms epics; 107 chart SVGs paint; 5 tiles rendered from `data.json`).
  - [x] **Measured and recorded** (see Completion Notes + [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md)): static site 198 files / 5.88 MB; chart mass 69.3% (dashboard) / 58.9% (epics); structured data layer 13.5 KB; Epic-7 extrapolation +863 pages → ~1,060 files (thousands on large repos). The 113-file figure in this task was stale — the repo is at 196 HTML files today.

- [x] **Task 3 — Axis D: npx distribution** (AC: #2)
  - [x] Prototyped the **npm-wrapper-around-native-binary** path (`spike/delivery/npm-wrapper`): 1.5 KB wrapper whose `bin` resolves/spawns the self-contained `specscribe`. `npm install`+run of the wrapper generated all 196 files in 3.7 s with **no .NET SDK/runtime used**. Self-contained binary = 73.0 MiB raw / 34.2 MB gzipped per RID; per-RID optional-deps strategy documented.
  - [x] Compared on paper against `dnx`/`dotnet tool` (needs .NET or per-RID) and a hypothetical full-TS CLI (small install, zero runtime dep, but needs the axis-C port) — in ADR 0006.

- [x] **Task 4 — Axis C: rewrite surface + coupling-breakers** (AC: #3)
  - [x] Enumerated the port surface: ~14,200 production LOC / 87 `.cs` files + ~667 tests, by subsystem, with Markdig-extension fidelity and deep-git parsing flagged as the genuine-risk clusters (table in ADR 0006). No production port written.
  - [x] Evaluated coupling-breakers: (a) **WASM** — desk-research verdict: WASI can't spawn `git`, a hard blocker for the git analytics; removes the per-RID matrix but needs a git-host bridge, not a drop-in. (b) **pre-generated JSON** — cheapest breaker, zero port, but live in-editor regen still needs the binary. Both recorded in ADR 0006.

- [x] **Task 5 — Write ADR 0006 + seat the re-plan** (AC: #4, #5)
  - [x] Authored **`docs/adrs/0006-delivery-architecture-and-distribution.md`** (renders cleanly through SpecScribe). Decides axes A–D with the measured numbers; **re-affirms ADR 0005** (C#-render + self-contained binary) with two additive amendments (npx channel + optional JSON+SPA delivery adapter); rules the NFR6/progressive-enhancement posture (static fallback required; JS never sole carrier). Coupling verdict recorded: the pivot does **not** force a port for any single concern.
  - [x] Updated [docs/adrs/README.md](../../docs/adrs/README.md); added an `**Amended by:** ADR 0006` note to [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md)'s header (re-affirmed, so "Amended", not "Superseded").
  - [x] Named the follow-on: re-affirm path → unfreeze 6.4/6.5 + 16.1 and seat two additive stories (JSON+SPA delivery adapter; npx wrapper channel); if the owner overrides toward the pivot → seat a C#-port epic gated on a WASM-git-bridge spike. Re-plan seated in ADR 0006, not executed here.

- [x] **Task 6 — Quarantine & land only the decision** (AC: #6)
  - [x] Spike branch `spike/delivery-arch-6-6`; only quarantined `spike/delivery/` + ADR 0006 + README index + the 0005 amend note + this story's record + the sprint-status update land. Shipped `specscribe` gains **no** pivot code.
  - [x] **No `src/SpecScribe/*.cs` touched** (git-confirmed). Full suite green — **676 tests pass incl. the `GoldenContentFingerprint` byte-parity gate** → generated site byte-identical. Read-only honored (exporter writes only to its `--out`).

## Dev Notes

### This is a SPIKE — decision-first, timeboxed, throwaway (same discipline as Story 6.3)
The deliverable is **ADR 0006 + a seated re-plan**, not a shippable SPA or a ported core. Build only as much as it takes to make the four-axis decision **defensible with numbers**, then stop. The single biggest trap is scope-creeping into an actual rewrite — resist it; the port (if chosen) is its own epic. Spike-led de-risking of foundational/greenfield surfaces is the project's established pattern (6.3, Epics 11–15).

### Ground the decision in ADR 0005's own numbers (don't re-measure what's already measured)
[ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) already measured, against *this* repo: dashboard doc **306 KB / 107 inline SVGs**, epics **237 KB / 18 SVGs**; render latency **~1.8–2.0 s warm / ~3.5 s cold** (dominated by ingest + `git` subprocess, not .NET startup); self-contained bundle **~73 MB/RID** vs **~3.5 MB** framework-dependent; CSP posture (inline SVG survives, `script-src 'nonce'`, `style-src 'unsafe-inline'` for the 126 attribute styles, Mermaid the one casualty). **Reuse these.** The new measurements this spike adds are: static-site **file count** at scale (axis A), **JSON payload** size + client render perf (axis A), and **npx package** size/latency (axis D).

### What the pivot would cost, stated honestly (so the ADR weighs it, not glosses it)
"Pure TypeScript for the application" ⇒ porting a mature, 667-test C# core: Markdig markdown fidelity, `GitMetrics` deep-git parsing, coverage classification, projection, SVG chart generation — the bulk of Epics 1–4 + 3, discarded. Upside to weigh against it: idiomatic for the SDD/JS audience; a JSON-first layer other tools can consume; a SPA that natively solves file-bloat + interactivity + live updates; a trivially thin VS Code extension (loads JSON + a JS bundle, not a 73 MB binary). The ADR's job is to make this trade with numbers, not sentiment.

### Architecture invariants that BOUND the spike (from the spine)
- **AD-1 / AD-2** ([ARCHITECTURE-SPINE.md:34–48](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): one shared core; adapters translate view models without reinterpreting sources. A JSON export or SPA renderer is a delivery concern over the *same* view models — it must not re-parse `.md`. (If the pivot moves *analysis* to TS, that is a spine-level change the ADR must call out explicitly, not smuggle in.)
- **AD-6 / read-only** (:74–80): no spike path writes source artifacts.
- **AD-7 / AD-8** (:82–96): theming is Story 6.5; interaction-state shape is shared, transport adapter-specific — a SPA/JSON transport is a legitimate AD-8 transport, but must preserve the interaction-state semantics.
- **NFR4** (extensible, no core rewrite for new adapters), **NFR6** (accessibility semantics are contract — the crux the ADR must rule on for JS-rendered surfaces), **NFR9** (reproducible CI builds — the distribution axis feeds this).
- **Seed, not invariant** (:98–101): the package/namespace layout is a seed; the *shared-core contract* is the invariant. A pivot may change the seed drastically but must preserve (or consciously replace) the shared-core contract — the ADR states which.

### Relationship to Story 16.1 (packaging spike) and ADR 0005
- **Story 16.1** ("Release & Distribution Packaging Spike") decides packaging *mechanics* (channels, secrets, versioning). **This spike is upstream of it**: it decides the delivery *architecture* (rendering language, output form, npx-vs-dotnet) that 16.1 then packages. 16.1 is frozen/gated on ADR 0006 so it doesn't decide "NuGet global tool" only for a pivot to moot it. If the pivot lands, 16.1's AC1 (CLI channel) is largely pre-decided by ADR 0006.
- **ADR 0005** stands as the *current* decision until ADR 0006 supersedes or re-affirms it. Story 6.4's file already defers to whichever ADR governs; ADR 0006 becomes that governor if it supersedes.

### Project Structure Notes
- **C#:** any throwaway prototype code in [src/SpecScribe/](../../src/SpecScribe) `namespace SpecScribe;` **on the branch only**, or under `spike/` — no new project on `main`, no namespace split (seed-level, still forbidden — [ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- **SPA / npm prototype:** self-contained under a quarantined path (e.g. `spike/delivery/`) with its own `package.json` — not in the .NET solution or site build; exists to be thrown away.
- **ADR home:** [docs/adrs/](../../docs/adrs); ADR 0006 is the next number; match [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md)'s shape.
- **Output dir** for any generate verification is `SpecScribeOutput` (memory: [[generate-output-dir-is-specscribeoutput]]); never `--output docs/live`.

### References
- **Owner directive + freeze:** [sprint-change-proposal-2026-07-10-delivery-architecture.md](../planning-artifacts/sprint-change-proposal-2026-07-10-delivery-architecture.md) — the correct-course that seats this spike and freezes 6.4/6.5 + Epic 16 packaging.
- **The decision being reopened:** [docs/adrs/0005-vs-code-webview-runtime-and-packaging.md](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) (C#-renders-HTML + 73 MB bundled-binary — the premise this spike tests); the 6.3 spike that produced it: [6-3-vs-code-integration-spike.md](6-3-vs-code-integration-spike.md) + [spike/README.md](../../spike/README.md) (quarantine pattern to mirror).
- **What a pivot touches:** [6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (frozen), [6-5-host-aware-theming-and-explicit-helper-actions.md](6-5-host-aware-theming-and-explicit-helper-actions.md) (frozen), Epic 16 packaging stories ([epics.md:1744–1840](../planning-artifacts/epics.md)).
- **Section view models the JSON layer serializes:** [6-2-read-only-vs-code-dashboard-and-epics-experience.md](6-2-read-only-vs-code-dashboard-and-epics-experience.md) + `DashboardView.cs`/`EpicsView.cs` + builders; the serialization boundary in [SectionViewModelSerializationTests.cs](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs).
- **Policy on the line:** [rendering-architecture.md:84–92](../specs/spec-specscribe/rendering-architecture.md) (progressive enhancement — "JS never carries information"), [epics.md:86](../planning-artifacts/epics.md) (NFR6), [epics.md:89–91](../planning-artifacts/epics.md) (NFR9).
- **Architecture:** [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) (AD-1/AD-2, AD-6, AD-7/AD-8, Seed/no-split, NFR4); [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md) (Delivery Adapter Layer, Evolution Sequence).
- **Memory:** [[epic-6-vscode-spike-and-renumber]] + [[epic-6-sequencing-and-6-5-theming]] (Epic 6 spike-led history), [[story-6-2-section-view-models-live]] (the JSON gotcha), [[charting-is-pure-svg-no-js]] (SVG survives; the CSP win ADR 0005 measured), [[golden-diff-normalization-gotchas]] + [[generate-output-dir-is-specscribeoutput]] (byte-identity gates), [[worktree-edits-must-target-worktree-path]] (branch, not main), [[epic-4-adapter-contract-scope]] (spike-led greenfield pattern).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Dev Story workflow)

### Debug Log References

- Axis-A bloat report: `dotnet run --project spike/delivery/exporter -c Release -- "." --out <dir>` (prints the 3-way byte split to stderr).
- SPA client-render timings captured in-browser (`window.__spikeTiming`): fetch 35 ms, render dashboard 7.9 ms / epics 6.9 ms.
- Self-contained publish: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` → 76,526,642 B exe (34.2 MB gz).
- npx chain: `npm install <wrapper.tgz>` + run installed bin (`SPECSCRIBE_BIN` → native exe) generated 196 files in 3.7 s.
- Regression + byte-parity: `dotnet test tests/SpecScribe.Tests` → **676 passed, 0 failed** (incl. `GoldenContentFingerprint`).

### Completion Notes List

**This is a SPIKE — decision-first, throwaway code, ADR 0006 is the durable deliverable.** No production pivot on `main`; `src/SpecScribe` untouched (git-confirmed); 676 tests green including the golden byte-parity gate.

**Measured (this repo, 2026-07-10) — four axes:**
- **Axis A (output form):** Static site = 198 files (196 HTML) / 5.88 MB; cold+warm generate ~3.2 s. Inline SVG is **69.3%** of the dashboard body and **58.9%** of epics (2,469 SVGs / 1.37 MB site-wide). Structured non-chart data layer for both surfaces = **13.5 KB**. Epic-7 file-count extrapolation: **+863 pages → ~1,060 files** on this small repo; thousands–tens of thousands on a large repo. **Crux:** a JSON layer shipping pre-rendered SVG does not shrink bytes (payload ≈ 170 KB dashboard); only porting chart-gen to TS reaches the ~13.5 KB floor. The *file-count* win, however, needs only a JSON+SPA output form — deliverable as a C# adapter, no port.
- **Axis D (distribution):** npx proven end-to-end via npm-wrapper-around-native-binary (1.5 KB wrapper + 73 MiB/34 MB-gz per-RID binary), no .NET SDK. `dnx`/`dotnet tool` compared on paper (needs .NET / per-RID); full-TS CLI compared (tiny install, but needs the port).
- **Axis C (analysis language):** Port surface enumerated (~14,200 LOC / 87 files + ~667 tests) with Markdig-extensions and deep-git parsing as the risk clusters. Coupling-breakers: WASM blocked by WASI-can't-spawn-`git`; pre-generated-JSON is zero-port but keeps the binary for regen.
- **Axis B (rendering language):** ruled by the accessibility posture — keeping C# rendering preserves the NFR6 JS-optional baseline for free.

**Decision (ADR 0006, Proposed):** Re-affirm ADR 0005's C#-render + self-contained-binary core; **amend** with (1) npx-via-npm-wrapper as the primary CLI channel and (2) an optional JSON+SPA delivery adapter (a C# `IRenderAdapter`) for file-count-sensitive contexts. **Defer** the full pure-TS-SPA + core-port pivot — the evidence shows none of the three owner concerns requires it, and it discards ~14,200 LOC + 667 tests at real Markdig/deep-git risk. **Accessibility ruling:** JSON+SPA is additive over the static-HTML baseline; a static/`noscript` fallback is required and is free because C# already emits the pre-rendered HTML.

**⚠ Reverses the owner's stated lean** (toward pure-TS + npx). ADR 0006 is therefore marked **Proposed**, awaiting owner ratification — accept → Accepted + unfreeze per the re-plan; override → record the counter-decision and seat the port epic (gated on a WASM-git-bridge spike). Either way, 6.4/6.5 + 16.1 unblock.

**Re-plan seated (not executed):** unfreeze 6.4/6.5/16.1; seat two additive stories (JSON+SPA delivery adapter; npx wrapper channel); do **not** seat a core-port epic unless the ADR is overridden.

### File List

**Durable (land on the decision):**
- `docs/adrs/0006-delivery-architecture-and-distribution.md` (new — the deliverable)
- `docs/adrs/README.md` (modified — index entry for ADR 0006)
- `docs/adrs/0005-vs-code-webview-runtime-and-packaging.md` (modified — `Amended by: ADR 0006` header note)
- `_bmad-output/implementation-artifacts/6-6-delivery-architecture-and-distribution-spike.md` (this story record; frontmatter `baseline_commit`, tasks, Dev Agent Record, Change Log, Status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — 6.6 → in-progress → review)

**Throwaway (quarantined under `spike/delivery/`, no `.sln`/pack/site-build reference; deletable with the branch):**
- `spike/delivery/README.md`
- `spike/delivery/exporter/SpecScribe.DeliverySpike.csproj`, `spike/delivery/exporter/Program.cs`
- `spike/delivery/spa/index.html`, `spike/delivery/spa/app.js`
- `spike/delivery/npm-wrapper/package.json`, `spike/delivery/npm-wrapper/bin/specscribe.js` (launcher — force-added past the global `bin/` gitignore)

**No changes to `src/SpecScribe/**` or `tests/**`.**

## Change Log

- 2026-07-10 — Spike executed (dev-story). Measured all four axes against this repo; built throwaway evidence under `spike/delivery/` (C# JSON-layer exporter + bloat meter, vanilla-JS SPA, npm-wrapper npx proof). Produced **ADR 0006** (Proposed) re-affirming ADR 0005's C#-render + self-contained-binary core with two additive amendments (npx channel + optional JSON+SPA delivery adapter) and deferring the full pure-TS pivot; ruled the NFR6 accessibility posture (static fallback required). ADR reverses the owner's stated lean → left **Proposed** for ratification. `src/SpecScribe` untouched; 676 tests pass incl. the golden byte-parity gate. Status → review.
- 2026-07-10 — Story 6.6 drafted (create-story → correct-course). Seated by the owner's decision to de-risk a possible delivery pivot (JSON + SPA + npx) via a spike rather than commit on a lean, immediately after ADR 0005 was Accepted on the "rendering stays in C#, bundle a 73 MB binary" premise. The spike measures four separable axes (output form, rendering language, analysis language, distribution), enumerates the C#-core-port cost and the coupling-breakers (WASM / pre-generated JSON), and produces **ADR 0006** superseding-or-reaffirming ADR 0005 — plus a ruling on the NFR6/progressive-enhancement accessibility posture for JS-rendered surfaces. Stories 6.4 + 6.5 and Epic 16 packaging are frozen pending ADR 0006. No production pivot lands on `main`; the code is throwaway evidence, the ADR is the durable output.
