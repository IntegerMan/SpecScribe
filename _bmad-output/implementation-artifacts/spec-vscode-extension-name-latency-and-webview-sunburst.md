---
title: 'VS Code extension: display name, first-paint latency, and webview hierarchy charts'
type: 'bugfix'
created: '2026-07-31'
status: 'done'
baseline_commit: '13f0aad2a22946f5c1d6ff7974da7c2c4f13812d'
review_loop_iteration: 0
context:
  - '{project-root}/CLAUDE.md'
  - '{project-root}/docs/adrs/0005-vs-code-webview-runtime-and-packaging.md'
  - '{project-root}/docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md'
  - '{project-root}/docs/adrs/0013-text-twin-is-the-no-js-contract.md'
  - '{project-root}/docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md'
  - '{project-root}/docs/adrs/0030-epic-24-graph-engine.md'
  - '{project-root}/docs/adrs/0031-text-twin-standardization-moves-to-its-own-epic.md'
  - '{project-root}/docs/adrs/0032-csp-posture-after-the-projection-layer.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three defects on the VS Code extension surface. (1) The extension presents itself as "SpecScribe Status" rather than "SpecScribe". (2) The panel's first paint waits on a **full site generation** — `WebviewCommand` calls `GenerateAll()` before writing a byte, and `--serve` does not short-circuit it, so the user waits out ~1,400 IR JSON file writes, every long-tail/code/ADR page, and one `git log --follow` per done story, none of which the first surface needs. **Measured on this repository (2026-07-31, no `--deep-git`): 74.4 s to first stdout byte, 53.1 MB payload** — against in-code claims of "~3.5 s cold" and "~8 MB", both now badly stale. (3) The dashboard's hierarchy sunburst renders as a heading and a legend describing a chart that is not there: the JSON island is stripped, `specscribe.js` is absent, and the engine `<script>` is emitted only by `HtmlRenderAdapter`, which this surface never calls.

**Approach:** Rename the display name only (identity keys stay). Split the webview render into a **prelude payload** (dashboard + epics family) emitted immediately, with the long tail completing afterwards and streaming over the existing `--serve --serve-delta` channel; skip work the webview provably cannot consume. Restore the hierarchy charts by shipping the vendored engine and the mount code as **nonce'd inline scripts in the webview shell** — no CSP relaxation — and preserving the data islands, with an honest visible text twin wherever a chart still cannot be drawn.

## Boundaries & Constraints

**Always:**
- Keep the CSP exactly as strict as today: `default-src 'none'`, `script-src 'nonce-…'`, no `'unsafe-inline'` for scripts, no external origins. ADR 0030 § Good records that this suffices; ADR 0005 § Decision 4 calls it "the security-critical lock we keep strict."
- Generated static-site bytes must not change. This is webview/CLI-path work; `npm run check:parity` (the frozen 24-route corpus that replaced `GoldenContentFingerprint`) must stay green.
- Every chart keeps a text equivalent, and no state is signalled by colour alone. A surface that cannot draw its chart must say so rather than show a legend for nothing (ADR 0013 § Decision 2; NFR8).
- Any host divergence that is added, narrowed, or retired is reflected in `HostRenderExceptions.Registry` in the same change. An unregistered divergence is a bug.
- Shared `main`: grep-verify every new symbol after writing it; never `git reset --hard`, `git checkout --`, or `git clean`.

**Ask First:**
- Changing the extension `name`, `publisher`, or the `specscribeStatus` panel viewType. These are identity keys; this spec changes the *displayed* name only.
- The final byte budget for island preservation, and whether `code-map.html` (a 1.24 MB island) is included or excluded.
- Landing the ADR that resolves ADR 0012 § Decision 5 / ADR 0013 § Decision 7 / ADR 0030's open question. ADR 0030 requires this be decided **once, for both** the hierarchy charts and the Epic 24 graphs — propose it, do not decide it silently.

**Never:**
- Do not relax the CSP, add `'unsafe-inline'`/`'unsafe-eval'` for scripts, or set `localResourceRoots`.
- Do not add `workspaceState`/`globalState` stale-payload caching (R8.2's "instant stale first paint"). Real, but a separate deliverable — record it in `deferred-work.md`.
- Do not remove long-tail webview surfaces. `spec-webview-doc-page-surfaces` made docs/ADRs navigable in-panel; they must still arrive, just later.
- Do not touch `spike/`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Cold panel open, `--serve` available | No cache; workspace with BMad artifacts | Prelude payload (dashboard + epics) paints first; long-tail surfaces arrive as a later delta frame | Prelude failure falls back to today's whole-payload path |
| Cold panel open, `--serve` unavailable | `persistentUnavailable` set | One-shot path still returns a complete payload — no surface is lost by the split | Existing 60 s timeout applies |
| Navigate to a long-tail surface before its delta arrives | Prelude only | Link resolves once the delta lands; before that the panel reports the surface is still loading, never a blank region | Never a silent no-op on click |
| Dashboard surface, engine present | Island preserved, engine inlined | Sunburst draws; `[data-hierarchy-ready]` set; twin remains in the a11y tree | Mount failure sets `[data-hierarchy-failed]` and reveals the twin visibly |
| Surface whose island exceeds the byte budget | e.g. `code-map.html` | Island stripped as today, **and** the text twin is revealed visibly with a stated reason | No legend without a chart |
| In-panel navigation after first paint | Content swapped via `innerHTML` | Charts on the swapped-in surface mount; no duplicate mount on an already-ready host | Re-entrant mount is a no-op |

</frozen-after-approval>

## Code Map

- `extension/package.json` -- `displayName` (:3), plus two `openLocation` enum descriptions and one `markdownDescription` (:269-274) naming "SpecScribe Status".
- `extension/src/extension.ts` -- panel title (:1182), file header (:2); the payload store / spawn lifecycle (:638-830) and the `--serve` connection (:1493-1640) that must accept a prelude-then-delta sequence.
- `extension/README.md` (:1), `extension/esbuild.js` (:1) -- prose naming.
- `src/SpecScribe/Commands.cs` -- `WebviewCommand`: `CapturePages` + `GenerateAll()` (:104-109), payload serialization (:121-141), `SerializeDelta` (:283-365), `RunServeLoop` (:144+).
- `src/SpecScribe/SiteGenerator.cs` -- `GenerateAll()` phase order (:353-778); `EmitSpaSite` now unconditional (:728); `DeliveryCadence.Build` (:695); `RenderWebviewSurfaces` (:3720-3780).
- `src/SpecScribe/WebviewRenderAdapter.cs` -- `RenderContent`/`StripDataIslands` (:63-107), `WrapDocument` + `DocumentTemplate` incl. the CSP and bridge script (:114-323).
- `src/SpecScribe/HierarchyExplorer.cs` -- host div (:660), boot placeholder (:653), `IslandHtml` (:810), `TextTwinHtml` (:976), `BootScript` (:1091), `ContainsHost` (:1060).
- `src/SpecScribe/HtmlRenderAdapter.cs` -- the boot script (:37-40) and engine `<script src>` (:67-71) the webview never reaches.
- `src/SpecScribe/assets/specscribe.js` -- `initHierarchyExplorers` (:1016), `initHierarchyExplorer` (:1151), island read (:1155-1158).
- `src/SpecScribe/HostRenderException.cs` -- `asset.js` (:29), `data-island` (:49), `hierarchy-chart` (:60) entries to narrow/retire.
- `tests/SpecScribe.Tests/WebviewRenderAdapterTests.cs`, `WebviewCommandTests.cs`, `SiteGeneratorWebviewTests.cs`, `HierarchyExplorerTests.cs` -- existing coverage to extend.

## Tasks & Acceptance

**Execution:**

*Goal 1 — display name*
- [x] `extension/package.json` -- set `displayName` to `SpecScribe`; reword the three settings strings to "SpecScribe panel" -- the ask; leave `name`/`publisher` untouched (identity keys).
- [x] `extension/src/extension.ts` -- panel title (:1182) and header comment (:2) to "SpecScribe" -- the tab label is the most visible instance.
- [x] `extension/README.md`, `extension/esbuild.js` -- prose rename -- keep docs consistent with the manifest.

*Goal 2 — first-paint latency*
- [x] `src/SpecScribe/SiteGenerator.cs` -- add an explicit opt-out for work the webview cannot consume, starting with `EmitSpaSite` (~1,400 JSON writes to a scratch dir the payload never reads) -- pure waste on this path; keep it ON for `generate`/`watch`.
- [x] `src/SpecScribe/SiteGenerator.cs` -- expose a seam that yields the dashboard + epics family surfaces before the long-tail phases run -- the prelude is the first-paint payload.
- [x] `src/SpecScribe/Commands.cs` -- `WebviewCommand`: write the prelude payload to stdout, then finish generation and emit the remainder as a delta frame on the existing `--serve --serve-delta` channel; the one-shot path still emits one complete payload -- reuses the shipped frame shape rather than inventing one.
- [x] `extension/src/extension.ts` -- accept a prelude frame followed by an additive delta; a click on a not-yet-arrived surface reports loading rather than failing silently -- the store currently assumes the first frame is whole.
- [x] `tests/SpecScribe.Tests/WebviewCommandTests.cs` -- cover the matrix rows: prelude then delta, one-shot completeness, no surface lost across the split.

*Goal 3 — webview hierarchy charts*
- [x] `docs/adrs/0036-the-webview-shell-supplies-chrome-scripts.md` (+ `docs/adrs/README.md` index entry) -- propose an ADR **amending ADR 0032 § Decision 2's enforcement clause** (which names the webview island strip as one of three enforcement points) and retiring the `asset.js` / `data-island` / `hierarchy-chart` host exceptions -- **not** a new CSP decision: ADR 0032 already discharged ADR 0012 § Decision 5's "land it once", and duplicating it is what that clause forbids.
- [x] `src/SpecScribe/WebviewRenderAdapter.cs` -- inline the vendored engine and the hierarchy mount code as nonce'd `<script>` blocks in `DocumentTemplate` (once per document, not per surface); have the bridge mount charts on first paint and after every content swap, idempotently.
- [x] `src/SpecScribe/WebviewRenderAdapter.cs` -- **retire** the island strip on the webview content path and preserve every island (owner decision 2026-08-01, see Design Notes) -- an island the engine can now read is live data, not dead weight; this also lights up the Epic 24 relationship graphs, which ADR 0030 requires be decided together with the hierarchy charts.
- [x] `src/SpecScribe/HierarchyExplorer.cs` -- **narrowed scope:** handle only the *mount-failure* path (`[data-hierarchy-failed]`), revealing the twin rather than leaving a legend over nothing. Broader text-twin standardization is **Epic 28's** by ADR 0031, which retired ADR 0013's per-story gate — do not expand into it; record any twin gap as debt owed to Epic 28.
- [x] `src/SpecScribe/HostRenderException.cs` -- narrow `data-island`, narrow/retire `asset.js` and `hierarchy-chart` to match what now ships -- an unregistered divergence is a bug.
- [x] `tests/SpecScribe.Tests/WebviewRenderAdapterTests.cs` -- assert the CSP is unchanged, the engine appears exactly once under the document nonce, islands are preserved, and a surface whose mount fails exposes a visible twin.

**Acceptance Criteria:**
- Given the extension is installed, when the user views it in the Extensions list and opens the panel, then both read "SpecScribe" and no user-visible string says "SpecScribe Status".
- Given a cold panel open on this repository, when the panel first paints, then it does so in **under 10 s** against the measured 74.4 s baseline (a >85% reduction), and the new timing is recorded in Design Notes alongside it. If the target proves unreachable without cutting a surface, HALT and report the achieved figure rather than trimming scope silently.
- Given the panel has painted its prelude, when generation completes, then long-tail surfaces (docs, ADRs, requirements) become navigable without a panel reset or a second spawn.
- Given the webview dashboard, when it paints, then the hierarchy sunburst is drawn with data, and the document CSP is byte-identical to today's.
- Given a surface whose chart cannot be drawn, when it renders, then a visible text equivalent states so — never a legend with no chart.
- Given the full suite and web gates, when they run, then `dotnet test` and `npm run check` are green, with static-site output unchanged.

## Spec Change Log

### Review round 1 — 2026-08-01 (Blind Hunter + Edge Case Hunter, run in parallel without shared context)

26 raw findings, deduplicated to 21 distinct. **No `intent_gap` and no `bad_spec`, so no loopback**: every finding
was either fixable without renegotiating intent (`patch`) or a real issue better handled on its own
(`defer`). Triage below; severity assigned by consequence to the extension's user, not by the reviewers.

**Patched in this round (8):**

1. **`location.href` broke the feature this spec exists to deliver — HIGH.** Both chart components activate
   *programmatically* (`specscribe.js`), and the webview bridge only intercepts `a[href]` clicks, so a sunburst
   click escaped it and attempted a top-level panel navigation to a non-resource path — at best inert, at worst
   replacing the document and losing the bridge, the inlined CSS and the engine. **The rendering was verified live
   and its primary interaction was not** — the gap the review closed. Fixed with ONE shared seam
   (`navigateTo` → optional `window.__specscribeNavigate`), which the webview bridge installs and which defaults
   to the original assignment everywhere else, so the static site and SPA are byte-unchanged and ADR 0036 §2's
   no-fork rule holds.
2. **`OnFirstPaintReady` was never cleared — HIGH.** The generator outlives `Execute`, and the watcher's topology
   and data-source routes both call `GenerateAll()` again; a mid-session save would have re-fired the checkpoint,
   re-emitted a one-surface `partial` payload, collapsed the panel's cache and desynchronised the delta sequence
   into a channel teardown. Nulled after the first pass.
3. **The failure-reveal CSS was dead code twice over.** Scoped on `.chart-panel`, which the client need not
   resolve to (the panel hook is opt-in), and covering only the hierarchy marker while ADR 0036 §5 decides
   hierarchy *and* graphs together. Rewritten attribute-scoped and covering both.
4. **A timed-out git call was cached as a real answer**, making cadence output silently load-dependent. Only
   non-null results are cached now; a miss falls through to the ordinary serial resolve.
5. **"Open Epics" silently opened the dashboard during the prelude** and never resolved — the one path that
   bypassed `push`'s partial-aware guard.
6. **`ExecutableScriptCount` was a substring subtraction** that could net a genuine executable `<script>` to zero.
   Now walks real tag positions — it is the only enforcement of ADR 0032 §2 left on this surface since the strip
   was deleted, so it had to be honest.
7. **The placeholder-token guard covered 2 of 9 tokens**; `__CONTENT__` is the dangerous one, substituted *after*
   ~1.4 MB of vendored JS is already in the string. All nine now asserted absent, plus a `</script>` count.
8. **A stale lead comment contradicted the test it headed** — every clause of it falsified by ADR 0036.

**Deferred (13), all recorded in `deferred-work.md` with evidence.** Highest-value: the Problems panel is wiped
for the prelude window; a post-checkpoint exception leaves the panel stuck in "still loading" with no timeout; the
git pre-warm's parallelism still raises timeout probability on the `generate`/`watch` path; an older installed VSIX
degrades badly against a newer core (and a code comment claims otherwise); and the extension's TypeScript half has
no tests at all — which is where two of these findings landed.

**KEEP on any re-derivation:** the single shared navigation seam (never a webview-only fork); the attribute-only
CSS scoping; caching only non-null git results; and the entry-surface-only prelude.

## Design Notes

**Why the sunburst is blank.** Three independent legs are severed, all in our control: `StripDataIslands` deletes the component's only data source ([WebviewRenderAdapter.cs:105](src/SpecScribe/WebviewRenderAdapter.cs:105)); `specscribe.js` is not shipped (`asset.js` exception); and the engine `<script src>` is emitted only in `HtmlRenderAdapter.Render` (:67-71), which this adapter never calls. With no script and no `:root[data-ss-hierarchy-boot]`, the host is `display:none`, the boot placeholder is `display:none`, and the dashboard's twin is `sr-only` — leaving heading + legend only.

**Two ratified ADRs found after approval, both narrowing this work (2026-08-01).** The spec as approved planned a new ADR resolving the webview-chart question. That was wrong on both counts:

- **ADR 0032 (Proposed, 2026-07-29) already discharged it.** Its § Decision 3 — *"A nonce'd or shell-supplied `<script src>` in the head is exactly what `script-src 'nonce-…'` is for"* — and § Decision 2 — every chrome script is *"replaced by whichever shell consumes the region"* — authorize this approach outright. Writing a second CSP ADR is precisely the duplication ADR 0012 § Decision 5's "landed once, not twice" forbids. What Goal 3 actually needs is a narrow amendment: ADR 0032 § Decision 2 lists the webview island strip as one of three enforcement points for the no-executable-script-in-region invariant, and that clause goes stale when the strip is retired. (The strip never enforced that invariant anyway — inert data islands are explicitly *permitted* by the same sentence.)
- **ADR 0031 (Accepted, owner-ratified 2026-07-29) retired ADR 0013's per-story twin gate** and moved text-twin standardization to **Epic 28**. The "honest visible twin" task is therefore narrowed to the mount-failure path only; anything broader is Epic 28's and must be recorded as tracked debt rather than absorbed here.

**⚠ ADR 0032 is still `Proposed`, not Accepted** — "ratification is the owner's". Goal 3 leans on its reasoning, so it wants ratifying alongside the new amendment.

**Why no CSP change is needed.** ADR 0030 § Good: *"The webview CSP needs no relaxation. `script-src 'nonce-…'` alone suffices."* The `hierarchy-chart` exception itself calls the absence *"a SEQUENCING choice rather than a technical limit"*, noting the Story 20.4 spike already proved Plotly renders under the shipped policy. Inline nonce'd scripts satisfy the existing policy; `localResourceRoots` stays empty.

**Two swap hazards.** A `<script>` inserted via `innerHTML` does **not** execute, so the engine must live in the shell (created once by `WrapDocument`) and the bridge must invoke the mount after each swap. JSON islands are unaffected — they are data, read by `getElementById`, not executed. Mounting must skip `[data-hierarchy-ready]` hosts, as `specscribe.js:1031` already does.

**Owner decision on the island byte budget (2026-08-01).** The Ask-First budget question was put to the owner with measured data: 167 pages carry hierarchy islands totalling **4,528,007 B**; `code-map.html` alone is **1,243,124 B**, 15× the next largest (`story-23-2.html`, 81,884 B), with every other page under 82 KB. A 128 KB threshold sitting in that gap was recommended. **The owner chose no budget — preserve every island.** So the strip is retired rather than narrowed, the payload grows by ~4.53 MB, and `code-map.html`'s chart and the Epic 24 relationship graph islands become live in the webview too. The concern raised and overridden: this re-adds the 1.24 MB Story 20.9 removed as dead weight — it is no longer dead, because the engine can now read it.

**RESULTS AS MEASURED — SECOND LATENCY PASS (2026-08-01, warm, no `--deep-git`).** The figures below supersede
the first-pass table that follows them; that table is kept because it is what motivated this pass.

| | baseline | first pass | **this pass** |
|---|---|---|---|
| first frame, spawning `specscribe.exe` (what the extension does — [extension.ts:1416](extension/src/extension.ts:1416)) | — | — | **10,218 ms** |
| first frame, via `dotnet run --no-build` (the harness the baseline used) | 74,428 ms | 29,589 ms | **12,116 ms** |
| first frame bytes | 53.1 MB | 28.5 MB | **4.37 MB** |
| `TOTAL GenerateAll` | — | 22,177 ms | **15,382 ms** |
| surfaces in the completed bundle | 892 | 892 | **895** (no surface lost across the split — the merged
serve key set and the one-shot key set are identical in both directions) |

**FINAL, after the review round's fixes: 9,985 ms and 10,180 ms across two warm runs of the real binary** (4,405,002 B,
1 surface). The 10 s criterion is **straddled, not cleanly met** — say so plainly rather than quoting the run that
happens to start with a 9. Call it **~10.1 s against a 74.4 s baseline: an 86% cut.** Anything further needs the two
cuts named at the end of this section, both of which cross existing invariants and want their own change.

**Independently re-measured after the fact, on a fresh non-incremental build, not taken on trust:** direct
`SpecScribe.exe --serve --serve-delta` first frame **10,116 / 10,120 / 10,146 ms** across three runs (4,373,398 B,
1 surface, `partial: true`); via the `dotnet run` harness **11,951 ms** warm / 12,210 ms cold. Both agree with the
table. The ~1.9 s gap between the two rows is `dotnet run`'s own launch overhead, which the extension does not pay —
it spawns the binary ([extension.ts:1416](extension/src/extension.ts:1416)), so **10.1 s is the number a user
experiences**. Full suite re-run independently: **2,901 passed, 0 failed**. Goal 3 re-verified live against the new
entry document after the restructure: `Plotly` loaded, `data-hierarchy-mounted`, **222 slices**, no console errors —
the surface-composition changes did not regress the charts.

What changed, and what each was worth:

1. **`RenderEpicsPages` now honours `WriteStaticPages`** ([SiteGenerator.cs:3443](src/SpecScribe/SiteGenerator.cs:3443)).
   It was the flag's last holdout: it wrote `epics.html` + ~230 epic/story documents with raw `File.WriteAllText`,
   never through `WritePage`. The webview re-renders that family from cached models in `BuildFamilySurfaces`, so
   the work was pure waste there. **9,252 ms → 1,386 ms.**
2. **The prelude is the ENTRY SURFACE ALONE** (`RenderWebviewSurfaces(includeEpicsFamily: false)`), with the epics
   family riding the same delta as the long tail. **28.5 MB → 4.37 MB** on the first frame.
3. **Dashboard-critical data hoisted above the long-tail page phases, unconditionally**, and a null-by-default
   `SiteGenerator.OnFirstPaintReady` checkpoint fires there. This is what moves `cadence-build` (3,340 ms),
   `code-pages`, the code map, risk quadrant, traceability, impact map, work-graph page, ideas/test artifacts,
   the index write and diagnostics — **6,846 ms** — behind first paint.
4. **The source-code walk overlaps the ingest git call.** **1,129 ms → 23 ms.** ⚠️ This forced the output-root
   wipe below the join: when git is absent the walk degrades to `FallbackCodeWalk`, a raw filesystem walk that
   does **not** exclude the output root, so the walk and the wipe/scaffold raced and returned different file sets
   run to run. `SiteGeneratorCodeMapTests.GenerateAll_DeterministicAcrossTwoRuns` caught it. That
   `FallbackCodeWalk` includes generated output at all is a **latent defect worth its own change** — it is why
   the wipe had to move rather than the walk being made independent of it.

**What still dominates the remaining 10.2 s**, all of it genuinely before the dashboard can be composed:

| phase | ms | why it is pre-checkpoint |
|---|---|---|
| `doc-pages` | 2,443 | fills `_docs`, which the dashboard's work inventory and index page read |
| `ingest+progress(git)` | 1,795 | the models |
| `epics+stories-pages` | 1,386 | ≈900 ms of it is `WriteRequirements` (long-tail pages), ≈405 ms `ResolveFollowUpWork` |
| `work-graph-model` | 939 | dashboard related-work pane + a nav gate |
| `first-paint-checkpoint` | 935 | composing + serializing the 4.37 MB frame itself |
| `adr-pages` | 771 | fills `_adrs` |
| `discover-code-references` | 586 | `CodeItemHref` |
| process start | ~450 | |

**The next two cuts, both measured, both deliberately not taken here.** Together ≈1.7 s, which would land ≈8.5 s:
`WriteRequirements` (~900 ms) past the checkpoint — blocked on `RenderEpicsPages` also being the watch-incremental
path's requirements writer, so the call would have to be re-homed in two places; and deferring the composed-region
half of `WritePage` for docs/ADRs (~800 ms) until `AppendLongTailSurfaces` reads it — which crosses the explicit
"composed HERE, in the same breath as the document's own linkify pass" invariant at
[SiteGenerator.cs:4215](src/SpecScribe/SiteGenerator.cs:4215) and wants its own change with its own proof.

**RESULTS AS MEASURED — FIRST LATENCY PASS (2026-08-01, warm build, no `--deep-git`).**

| | before | after |
|---|---|---|
| first stdout frame (`--serve --serve-delta`) | 74,428 ms | **29,589 ms** (−60%) |
| one-shot `webview` | 74,428 ms | **30,956 ms** |
| payload | 53.1 MB | 59.6 MB (+6.5 MB: islands, as decided) |

**The <10 s acceptance criterion is NOT met — 29.6 s.** Reported rather than quietly rescoped, per the AC's own instruction. Two findings explain it:

1. **The win came from removing unused work, not from the split.** `EmitIr=false` + `WriteStaticPages=false` account for essentially all of it.
2. **The prelude split buys ~1.4 s, because it was made at the wrong boundary.** The prelude is the dashboard **+ the whole epics family** — 194 surfaces, 28.5 MB — so first paint still composes and serializes nearly half the payload for a panel that displays exactly ONE surface. Phase timing (`SPECSCRIBE_PHASE_TIMING=1`) puts `GenerateAll` at **22,177 ms** of the 29,589 ms, dominated by `epics+stories-pages` **9,252 ms**, `cadence-build(git-per-story)` **3,254 ms**, `doc-pages` 1,991 ms, `ingest+progress(git)` 1,594 ms. The remaining ~7 s is composition + JSON serialization.

Getting under 10 s needs a prelude of the **entry surface alone**, with the epics family streaming in the delta — a narrower boundary than this spec drew. Left undone deliberately; it is the obvious next step and is recorded as deferred work.

**Live verification (Browser pane, `file://`, real nonce substituted).** The webview entry document was rendered and opened under its exact `<meta>` CSP: `Plotly` global present, host carries `data-hierarchy-ready`, panel `data-hierarchy-mounted`, **223 sunburst slices** drawn, 454 px tall, island 64,344 B intact, **zero console errors and no CSP violations**. This is the empirical answer to ADR 0032 §4, which explicitly declined to extend Story 23.1's CSP verdict to `<meta>` delivery — it holds.

**The measured baseline.** `dotnet run --project src/SpecScribe --no-build -- webview` on this repo, warm build, no `--deep-git`: **74,428 ms** to first stdout byte, **53,116,311 B** payload. The 53 MB matters independently of latency — `Commands.cs:286` still describes a "~8 MB whole-site webview payload", and the extension holds this in memory and JSON-parses it before painting. Re-measure after each latency task rather than assuming.

**The drift gate to watch.** `GoldenContentFingerprint` is retired (only comments survive at [SiteGeneratorAdapterTests.cs:258](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:258)). The live gate is `npm run check:parity` over a frozen 24-route corpus whose whole-page digests explicitly cover the Hierarchy/Graph anti-flash handshakes. Static output should not move here; if it does, establish causality before touching any pinned value.

## Verification

**Commands:**
- `dotnet build SpecScribe.slnx --no-incremental --disable-build-servers` -- expected: 0 errors. Non-incremental is required whenever an embedded asset changes, or the stale assembly is measured instead.
- `dotnet test SpecScribe.slnx --no-build` -- expected: all green.
- `cd web && npm run check` -- expected: `check:tokens`, `check:ir-content`, `check:assets`, `check:parity` all green (static output unchanged).
- `dotnet run --project src/SpecScribe --no-build -- webview > payload.json` -- expected: first stdout frame arrives markedly faster than the pre-change baseline; record both numbers.

**Manual checks (if no CLI):**
- Launch the Extension Development Host, open the SpecScribe panel on this repository: the tab reads "SpecScribe"; the dashboard paints before generation completes; the sunburst draws with data and is interactive; the long tail becomes navigable without a reset.
- In the panel's developer tools, confirm no CSP violations are logged and the engine script appears exactly once.
- Navigate between surfaces and confirm charts mount on each swapped-in surface with no duplicate mounts.

## Suggested Review Order

**Webview charts — the headline fix (start here)**

- Entry point: the shell now supplies chrome scripts, so the region can carry live data.
  [`WebviewRenderAdapter.cs:378`](../../src/SpecScribe/WebviewRenderAdapter.cs#L378)

- The island strip is gone; the body rides verbatim. One line, the whole defect.
  [`WebviewRenderAdapter.cs:99`](../../src/SpecScribe/WebviewRenderAdapter.cs#L99)

- ⚠️ Highest-risk stop: charts navigate programmatically, so a host hook is required.
  [`specscribe.js:22`](../../src/SpecScribe/assets/specscribe.js#L22)

- The bridge claims that seam, reusing the anchor path so the two cannot drift.
  [`WebviewRenderAdapter.cs:351`](../../src/SpecScribe/WebviewRenderAdapter.cs#L351)

- Honest fallback: attribute-scoped, covering hierarchy and graph markers both.
  [`specscribe-webview-theme.css:431`](../../src/SpecScribe/assets/specscribe-webview-theme.css#L431)

- Registry truth: `asset.js` narrowed to a carrier fact, two entries retired.
  [`HostRenderException.cs:33`](../../src/SpecScribe/HostRenderException.cs#L33)

**First-paint latency — 74.4 s to ~10.1 s**

- The two flags that bought most of the win: work the webview cannot consume.
  [`Commands.cs:115`](../../src/SpecScribe/Commands.cs#L115)

- The checkpoint hook — null by default, so every other path is unchanged.
  [`SiteGenerator.cs:340`](../../src/SpecScribe/SiteGenerator.cs#L340)

- Where it fires: after the dashboard's data exists, before the long-tail pages.
  [`SiteGenerator.cs:769`](../../src/SpecScribe/SiteGenerator.cs#L769)

- Installed only under `--serve --serve-delta`; the prelude is one surface.
  [`Commands.cs:148`](../../src/SpecScribe/Commands.cs#L148)

- ⚠️ Disarmed after the first pass, or a mid-session rebuild tears the channel down.
  [`Commands.cs:168`](../../src/SpecScribe/Commands.cs#L168)

- ⚠️ Parallel git pre-warm: only non-null results cached, so a timeout never sticks.
  [`SiteGenerator.cs:5497`](../../src/SpecScribe/SiteGenerator.cs#L5497)

**Extension — prelude honesty**

- A click during the prelude says "still loading" and replays when the delta lands.
  [`extension.ts:526`](../../extension/src/extension.ts#L526)

- The one path that bypassed that guard: "Open Epics" folded to the dashboard.
  [`extension.ts:443`](../../extension/src/extension.ts#L443)

**Peripherals**

- The rename itself — displayed name only; identity keys untouched.
  [`package.json:3`](../../extension/package.json#L3)

- The decision record: amends ADR 0032 §2, deliberately not a second CSP ADR.
  [`0036-the-webview-shell-supplies-chrome-scripts.md`](../../docs/adrs/0036-the-webview-shell-supplies-chrome-scripts.md)
