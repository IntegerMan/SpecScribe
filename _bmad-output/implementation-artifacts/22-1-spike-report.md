# Story 22.1 Spike Report — Incremental Recompute + IR-Delta Transport

**Status:** Complete · **Date:** 2026-07-23 · **Branch:** `spike/ir-incremental-22-1` · **Probe:** [`spike/ir-incremental/`](../../spike/ir-incremental/README.md)

This is the durable deliverable of Story 22.1 (the throwaway probe code is deletable with the branch). It mirrors
Story 6.6's report → ADR structure. **It does NOT author a new ADR:**
[ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) already decided the direction; this
spike de-risks the *build* of that decision with real numbers, and **gates** whether Stories 22.2–22.6 proceed as
scoped or are re-scoped.

---

## Context

[ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) locked the direction (JSON IR
canonical; static/SPA/webview co-equal projections; incremental, event-driven generation) and mandated that Epic 22
**"open with a measurement spike (mirroring Story 6.6) that de-risks incremental-recompute correctness and IR-delta
transport before any implementation story"** — naming **incremental-recompute correctness (stale/topology-change
invalidation) as the primary technical risk.** This spike answers that with measurement, not argument.

### Method (how the numbers were obtained)

The probe drives the **real, shipped `SiteGenerator`** — no toy, no `.md` re-model, no `.html` scrape. For each
change class it:

1. runs a full `GenerateAll` of the **pre-change** tree and keeps the generator instance alive (exactly what
   `specscribe watch` does);
2. mutates the source and dispatches the change through the **shipped watch-mode route**, replicating
   [`FileWatcherService`](../../src/SpecScribe/FileWatcherService.cs)'s fire-time predicate order
   (`IsDataSource → IsAdr → IsEpicsRelated → GenerateOne/RemoveFor`);
3. runs a **second, cold `GenerateAll` of the identical post-change tree** — this is the **correctness oracle** (a
   full regenerate is, by definition, coherent output);
4. diffs the two output trees byte-for-byte, folding **only** the per-run/per-build/per-machine noise the shipped
   `GoldenContentFingerprint` gate folds (ported `NormalizeVolatile` — footer clock, cache-bust, version/build rows).

The correctness matrix runs against a **mutable copy of this repo's own artifacts** (`_bmad-output` + `docs`, 702
output files) with deep-git off, so the incremental run and the oracle read the identical inputs. The latency axis
runs against the **real repo** (1,211 output pages) with deep-git on and off. **Determinism was verified**: two
independent full generates agree byte-for-byte on all 701 shared pages (the `content-doc` case below is the proof —
0 diffs), so every staleness number reported here is real, not normalization noise.

---

## Measured Evidence

### Axis 1 — Latency

**Full `GenerateAll`, real repo, 1,211 output pages (this machine):**

| | cold | warm (best-of-2) |
|---|---|---|
| deep-git **ON** | 52.6 s | **31.5 s** |
| deep-git **OFF** | 27.6 s | 27.0 s |

- The **deep-git increment** (the `--deep-git` numstat log, over and above the always-on baseline git pulse) is
  **~4.5 s warm = 14.3 %** of gen-time at this scale. **This updates ADR 0008's premise.** ADR 0006/0008 measured
  "gen-time ~3.2 s dominated by the git subprocess" at **198 pages**; at **1,211 pages** the site has grown ~6×, and
  **page rendering now dominates — the deep-git increment is a minority share.** (Absolute times are inflated by this
  environment; the *ratios* are what matter.)

**Incremental route latency (in the 702-file artifact sandbox; a full regen there = ~16 s):**

| change class | route | incremental latency | speed-up vs full |
|---|---|---|---|
| delete-adr | `RegenerateAdrs` | **0.19 s** | ~84× |
| add-doc | `GenerateOne` (new) | 1.1 s | ~15× |
| rename-doc | `RemoveFor`+`GenerateOne` | 1.2 s | ~13× |
| content-doc | `GenerateOne` | 2.0 s | ~8× |
| delete-story | `RegenerateEpics` | 4.4 s | ~3.6× |
| content-story | `RegenerateEpics` | 4.7 s | ~3.4× |

**The latency case for incremental is strong and real: 3×–84× faster than a full rebuild.** The lever is genuine —
but, as Axis 2 shows, the fastest routes are not all faithful.

### Axis 2 — Changed-scope recompute correctness (the primary risk)

Incremental route output vs. full-regen oracle, byte-diff (golden normalization). **stale** = present in both, content
differs; **orphaned** = in incremental only (route failed to delete); **missing** = in oracle only (route failed to
create).

| change class | route | verdict | stale | orphaned | missing |
|---|---|---|---|---|---|
| **content-doc** | `GenerateOne` | ✅ **byte-identical** | 0 | 0 | 0 |
| add-doc | `GenerateOne` | ❌ | 1 | 0 | 0 |
| rename-doc | `RemoveFor`+`GenerateOne` | ❌ | 1 | 0 | 0 |
| **delete-adr** | `RegenerateAdrs` | ❌ | 6 | 1 | 0 |
| **content-story** | `RegenerateEpics` | ❌ | 57 | 0 | 0 |
| **delete-story** | `RegenerateEpics` | ❌ | 61 | 0 | 0 |

**No-op controls (route run with NO source change vs. a cold full regen — isolates route-vs-oracle divergence from any
change ripple):**

| control | verdict | stale |
|---|---|---|
| `RegenerateAdrs` (no-op) | ✅ CORRECT | 0 |
| `RegenerateEpics` (no-op) | ❌ **DIVERGES** | **56** |

**The load-bearing finding:** `RegenerateEpics` diverges from the full-regen oracle **even when nothing changed** — on
**every epic page**, its **work-graph over-counts provenance**:

| | `RegenerateEpics` (incremental) | `GenerateAll` (oracle) |
|---|---|---|
| Epic 1 | 16 items / 20 links | 13 items / 12 links |
| Epic 2 | 20 / 29 | 18 / 17 |
| Epic 6 | 57 / 76 | 51 / 54 |
| Epic 9 | 51 / 72 | 44 / 48 |

Because this divergence is **change-independent** (it is present with a no-op), the 57/61 stale pages in
content-story/delete-story are dominated by it, not by the edit. This is a **pre-existing watch-mode fidelity gap in
the shipped tool** (the Story 19.2 work-graph on the incremental path), and it is a concrete instance of exactly the
correctness risk ADR 0008 named. **Probable root cause** (lead for 22.5, not fixed here — no production code ships from
this spike): `GenerateAll` builds `_workGraph` reading follow-up/deferred work **from source** before `_docs` is
populated ([`SiteGenerator.cs:204–206`](../../src/SpecScribe/SiteGenerator.cs)), whereas `RegenerateEpics` rebuilds it
**after** `_docs` + `SyncDeferredDocFromDisk` are populated ([`SiteGenerator.cs:599–612`](../../src/SpecScribe/SiteGenerator.cs)),
so the narrow path resolves provenance from a different (doubled) inventory.

**The other routes, interpreted:**

- **`GenerateOne` on a content-only generic-doc edit is byte-perfect (0 diffs).** Nothing else depends on that doc's
  body, so the narrowest route is *correct* for this class. This is the safe base case.
- **`GenerateOne` topology (add/rename) leaves exactly one surface stale: `code-map.html`.** No incremental route ever
  re-renders the Code Map, so any change to the file set it indexes leaves it stale. (Notably, nav and the home index
  did **not** go stale — an earlier assumption that the cached `_nav` would strand new docs was **disproven** by
  measurement.)
- **`RegenerateAdrs` is faithful for the ADR portal pages** (no-op = 0) but a **delete** leaves the cross-artifact
  seams stale: the ADRs rendered as browsable **code-view pages** (`code/docs/adrs/*.md.html`), `code-map.html`, the
  **citing story page** `epics/story-9-4.html` (a dead cross-reference — the reference-graph seam), and one
  **orphaned** `code/docs/adrs/README.md.html`.
- **`delete-story` (`RegenerateEpics`)** additionally strands `cadence.html` (delivery-cadence depends on the story
  set) and `code-map.html`, on top of the work-graph divergence.

**Answer to AC #2's question — does incremental correctness hold without a full-rebuild fallback?**
**Partially. It holds for content-only edits of generic docs. It does NOT hold for (a) the epics/story family at all —
`RegenerateEpics` is not oracle-faithful even with no change — nor for (b) any topology change, which strands the
cross-artifact surfaces no narrow route refreshes (Code Map, delivery cadence, reference-graph citations, ADR
code-view pages).** A full-rebuild fallback (or targeted invalidation + a work-graph parity fix) is required for those
classes.

### Axis 3 — IR-delta transport

IR = the shipped `SpaDelivery` manifest + content chunks. Measured on the artifact sandbox with `--spa`:

- IR size: **23 chunks, 48.3 MB total**; largest chunk **3.08 MB**.
- Chunker guards in current code: `MaxChunkBytes = 2,000,000` **and** `MaxPagesPerChunk = 75`
  ([`SpaDelivery.cs:37,56,194`](../../src/SpecScribe/SpaDelivery.cs)).

**This updates the story's premise.** Story 22.2's stated "known constraint" — the **byte-blind chunker** that let
`pages-root.json` reach 112.9 MB (Story 6.6 at-scale) — **is already fixed**: a 2 MB byte cap ships today, so
multi-page chunks are byte-bounded. *Remaining gap:* a single page larger than the cap still gets a dedicated batch
that can exceed it (measured: a 3.08 MB chunk > the 2 MB guard).

**Delta size for a single change (whole-changed-chunk transport):**

| change | chunks changed | bytes re-shipped | % of IR |
|---|---|---|---|
| one-paragraph content edit to a story | 9 | 19.3 MB | **39.9 %** |
| delete one story | 6 | 12.2 MB | 25.3 % |

**A chunk-level delta is NOT small.** A one-line edit re-ships ~40 % of a 48 MB IR, because `RegenerateEpics`
re-emits the entire `epics` + `root` chunk family. A useful IR-delta needs **page-level (or finer) addressing**, not
chunk-level — the chunk is the wrong delta unit.

---

## Findings

1. **Latency: incremental is a large, real win (3×–84×).** The lever ADR 0008 chose is sound. At today's scale the
   deep-git increment is only ~14 % of gen-time (page rendering dominates), which *reduces* the urgency of
   git-avoidance and *raises* the value of not re-rendering unchanged pages.
2. **Correctness is the gating risk, exactly as ADR 0008 predicted — and it is already violated in the shipped
   watch mode.** `RegenerateEpics` is not oracle-faithful even with a no-op (56-page work-graph divergence).
   `GenerateOne` (content-only generic docs) is byte-perfect. Every topology change strands cross-artifact surfaces
   that **no** narrow route refreshes: **Code Map, delivery cadence, reference-graph citations, ADR code-view pages**,
   plus orphan/prune gaps on delete.
3. **The IR-delta is coarse at chunk granularity (25–40 % of the IR per single edit).** The byte-blind-chunker
   premise is stale (byte cap shipped); the real remaining work is **finer-than-chunk delta addressing** + capping
   single oversized pages.

---

## Gate for Stories 22.2–22.6

| Story | Verdict | Basis |
|---|---|---|
| **22.2 — Canonical IR schema + versioning** | **Proceed, RE-SCOPED** | The "byte-blind chunker" known-constraint is **already fixed** (2 MB cap shipped) — drop it. Re-aim 22.2's chunking work at **page-level (sub-chunk) delta addressing** + **capping single oversized pages** (a 3.08 MB chunk was measured over the 2 MB guard). Evidence: a 1-line edit re-ships 39.9 % of the IR at chunk granularity. |
| **22.3 — Static HTML from the IR** | **Proceed as scoped** | Pure projection; not gated by incremental correctness. The golden byte-parity gate this spike leaned on is the same one 22.3 must hold. No blocker found. |
| **22.4 — SPA + webview as IR consumers** | **Proceed as scoped** | Consumer-side of the IR; no incremental-correctness dependency surfaced. Note only that the SPA IR is large (48 MB / 1,211 pages) — 22.4 should consume chunks lazily, which the manifest already supports. |
| **22.5 — Incremental event-driven regeneration engine** | **RE-SCOPE (required)** | The measured facts forbid building the engine on the current narrow routes as-is. 22.5 MUST: **(a)** fix `_workGraph` parity so `RegenerateEpics` matches `GenerateAll` (the 56-page no-op divergence); **(b)** add **topology-change invalidation** for the cross-artifact seams no route refreshes today — Code Map, delivery cadence, the reference/citation graph (`_referenceMap`/`_codeReverseMap`), ADR code-view pages — and prune orphaned output on delete; **(c)** until (a)+(b) are proven against the byte-parity oracle, **fall back to a full rebuild for topology changes and the epics/story family.** Content-only generic-doc edits (`GenerateOne`) are proven safe and may stay narrow now. The oracle-diff harness this spike built is the acceptance test 22.5 should adopt. |
| **22.6 — (Spike-gated) client-server delta channel** | **Viable, but gated on 22.2** | A delta channel is only worthwhile atop a *small* delta. At chunk granularity a single edit is 25–40 % of a 48 MB IR — too coarse to justify a push channel. **Proceed only after 22.2 delivers page-level delta addressing;** then re-measure. Transport itself is a legitimate AD-8 concern and unblocked in principle. |

**No ADR amendment required.** These findings *scope* the implementation stories; they do not contradict ADR 0008's
decision (they confirm its risk call). Per the ADR-creation-trigger discipline, if 22.5's parity work reveals the
narrow-route model must change *architecturally*, propose an ADR at that point rather than in this report.

### Recommended follow-up outside Epic 22

The `RegenerateEpics` work-graph over-count is a **live watch-mode defect in the shipped tool today** (independent of
Epic 22): `specscribe watch` shows a different, inflated work-graph than `specscribe generate` until a full restart.
Worth a standalone bug fix (or folding into 22.5's parity task) rather than waiting on the whole IR pivot.

---

## AC coverage

- **AC #1** — changed-scope recompute correctness + latency measured, incl. AD-5 topology cases (add/rename/delete),
  captured in this report. ✅
- **AC #2** — IR-delta payload size + latency measured for ≥1 topology change and ≥1 content-only change; report states
  incremental correctness does **not** hold without a full-rebuild fallback for the epics family + topology changes. ✅
- **AC #3** — no production code ships (`src/SpecScribe/**` and `tests/**` untouched; the throwaway probe is quarantined
  under `spike/ir-incremental/` and excluded from `SpecScribe.slnx`/build/pack; generated site byte-identical — the
  full suite incl. `GoldenContentFingerprint` is green); findings gate 22.2–22.6 above. ✅
