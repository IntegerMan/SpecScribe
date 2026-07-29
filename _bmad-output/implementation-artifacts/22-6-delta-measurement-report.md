# Story 22.6 Task 1 — Delta Measurement Report (THE GATE)

**Verdict: the gate PASSES.** A single-file content edit through `GenerateOne` produces a page-level delta of
**2.72 % of the full IR** and **4.09 % of the full webview payload** — both under AC #1's 5 % threshold, on both of
two repeated runs.

Story 22.6 therefore proceeds to Tasks 2–9.

- Harness: [`spike/delta-transport/`](../../spike/delta-transport/) (quarantined; no `src/` code, no `.slnx`
  reference, generated site byte-identical with or without it)
- Measured: 2026-07-29, against this repo, IR `schemaVersion` **2** (post-Story-22.4 region shape)
- Machine-readable output: `report.json` in the harness's `--out` directory

## Reproduce

```bash
dotnet run --project spike/delta-transport/SpecScribe.DeltaTransportSpike.csproj -c Release -- --repo . --out ./scratch
```

Exit code 0 = gate passed, 1 = gate failed.

## What was measured, and why in these units

For each of the four watch routes `FileWatcherService.RunDebouncedPass` can dispatch a **content-only** edit to,
the harness snapshots the IR manifest, applies one appended line to one source file, invokes the **shipped** route
(mirroring the dispatcher's predicate order verbatim, including Story 22.5's `ClassifyRebuildScope` escalation),
re-snapshots, and diffs by `contentHash`.

**Delta bytes are the JSON-ENCODED member tokens**, not the manifest's raw `bytes` field. A page's wire cost is the
`key : value ,` member its chunk actually carries, and default HTML-safe escaping turns every `<`, `>` and `&` into
a 6-byte `\uXXXX`. The full-IR denominator is measured on disk in that same encoded form. Dividing raw bytes by
encoded bytes would have flattered every ratio by roughly a third; both sides are held in the same units instead.
Raw content bytes are reported alongside for reference.

## Results

Two full runs, ~40 minutes apart, on a tree with concurrent uncommitted work (see § Caveats). Run 2 is the
headline; both are shown because CLAUDE.md requires confirming a measured number is stable across repeated runs
rather than locking in a single observation.

| Route | Outcome | Pages changed | Delta (raw) | Delta (encoded) | Full IR | Full webview payload | **% of IR** | **% of webview** |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| **`GenerateOne`** ← **THE GATE** | Updated | 3 | 1,199,282 | **1,830,263** | 67,289,674 | 44,782,403 | **2.72 %** | **4.09 %** |
| `RegenerateEpics` | Updated | 2 | 559,756 | 894,543 | 67,289,674 | 44,782,405 | 1.33 % | 2.00 % |
| `RegenerateAdrs` | Updated | 2 | 528,647 | 845,418 | 67,289,677 | 44,782,403 | 1.26 % | 1.89 % |
| `RegenerateFromDataSource` | **Skipped** | 1 | 502,773 | 808,541 | 67,289,521 | 44,782,314 | 1.20 % | 1.81 % |

Run 1 for comparison — 865 IR pages vs run 2's 875, the tree having moved underneath:

| Route | % of IR | % of webview |
|---|---:|---:|
| `GenerateOne` | 2.733 % | 4.109 % |
| `RegenerateEpics` | 1.336 % | 2.009 % |
| `RegenerateAdrs` | 1.263 % | 1.899 % |
| `RegenerateFromDataSource` | 1.208 % | 1.816 % |

**Drift between runs is under 0.03 pp on every route** despite ten IR pages appearing between them. The gate is not
sitting near its threshold on noise.

### The `RegenerateEpics` number is NOT comparable to Story 22.1's

Story 22.1 reported **25.3 %–39.9 %** for this route and the story file instructed this report to reproduce it
anyway and explain the gap. The gap has two causes, and neither is a regression:

1. **Granularity.** 22.1 measured at **chunk** granularity — a one-page edit re-shipped the whole multi-MB chunk
   containing it. This measures at **page** granularity, which is exactly the addressing Story 22.2 delivered and
   the whole reason 22.6 was unblocked. That is the intended ~20× improvement, not an accounting trick.
2. **22.1's own admitted no-op over-count.** Its figure was inflated by `RegenerateEpics` not being oracle-faithful
   even at no-op. This harness measures that inflation directly with a **no-op control** (below) and finds it is
   now **zero pages** on every route — so the 1.33 % figure carries none of it.

The 22.1 caveat is discharged, not inherited.

### The no-op control: zero churn on all four routes

Before each real edit, the harness runs **the same route against the same file with no edit at all**. Anything that
moves there moved for reasons unrelated to any change, and would be charged to every delta forever.

**Result: 0 pages changed, 0 bytes, on all four routes, on both runs.** The IR is regen-stable. Every byte in the
table above is attributable to the edit — the `attributable` and `total` columns in `report.json` are identical
for that reason.

This also means `spa/delta.json` will not thrash: a debounced regen that changes nothing emits an empty delta.

## Findings that change the implementation

### 1. `code-map.html` is in EVERY delta, and it is the dominant term

It appears in all four routes and is **~807 KB encoded — 44 % of the gated route's delta and 100 % of the
data-source route's.** The cause is not volatility; it is a genuine total:

```
BEFORE: "…5 lines of code</p>\n\n<h3>Source Code Map</h3>…"
AFTER : "…8 lines of code</p>\n\n<h3>Source Code Map</h3>…"
```

A whole-repo lines-of-code counter that includes the edited artifact. Appending 3 lines to any ingested file moves
it, which re-hashes the entire ~807 KB region.

**This is correct behavior and must not be "fixed" here.** The page genuinely changed; a delta that omitted it
would ship a false "unchanged", which AC #7 names as the failure mode worse than a false "changed". It is recorded
because it sets the floor: **no content edit will ever produce a delta below ~1.2 % of the IR**, and any future
attempt to drive the delta lower has to address this page specifically, not the transport.

### 2. `index.html` carries an artifact-freshness date keyed to file mtime

The gated route's third changed page diverges at:

```
BEFORE: "Sun, Jul 5, 2026\nDecision journal (.memlog) updated…"
AFTER : "Wed, Jul 29, 2026\nDecision journal (.memlog) updated…"
```

The dashboard's coverage card shows a per-family "updated" date derived from the edited file. Real and
change-driven — it is why `GenerateOne` (2.72 %) costs more than `RegenerateEpics` (1.33 %) despite touching a
smaller page.

### 3. ⚠️ `RegenerateFromDataSource` returns `Skipped` **after** fully rebuilding the site — Trap 2's stated premise is false for this route

This is the finding with teeth, and it contradicts the story file's own Dev Notes.

Story 22.6 Trap 2 says: *"Do not advance the basis on Error or Skipped… `RunServeLoop` returns early for
`GenerationOutcome.Error or Skipped` because 'the generator's in-memory state is unchanged.'"*

**For `RegenerateFromDataSource` that premise does not hold.** Reading
[`SiteGenerator.cs:1238`](../../src/SpecScribe/SiteGenerator.cs:1238), the route calls `GenerateAll()` on its
**first line** — a complete rebuild, including `EmitSpaSite` — and only *afterwards* inspects the resulting event
list to decide what to report. An unparseable `sprint-status.yaml` (which this repo's own file is — it is not
valid YAML) makes it return `Skipped` **having already rewritten the entire static site and the entire IR**.

The measurement confirms it empirically: outcome `Skipped`, and `code-map.html`'s `contentHash` moved anyway.

**Consequence for Task 3, and it is load-bearing:** a delta basis gated on the reported *outcome* would refuse to
advance here, and the next genuine push would diff against a manifest two emits stale — silently emitting a false
"unchanged" for every page this rebuild touched.

**The resolution is the seam, not a special case.** Task 3 already prescribes capturing the basis *inside*
`EmitSpaSite`, which runs if and only if the IR was actually re-emitted. That makes the basis track **what was
emitted** rather than **what was reported**, and this route stops being a special case at all. Trap 2's
*instruction* stands for the NDJSON channel (whose basis is the webview bundle, and which genuinely does not
re-render on `Skipped`); its *stated reason* is wrong for one of the four routes, and the implementation must not
be built on it.

A secondary observation, pre-existing and **out of scope for this story**: because `RunServeLoop` returns early on
`Skipped`, a webview panel goes stale-but-silent after an unparseable data-source save even though the site on disk
was rebuilt. Not introduced here and not fixed here; noted so it is not later mistaken for delta-channel damage.

## Caveats — read these before quoting a number

1. **The tree moved underneath both runs.** Concurrent sessions held uncommitted work throughout (CLAUDE.md
   § Concurrent work); the IR grew 865 → 875 pages between run 1 and run 2, and a mid-session `src/` build break
   from another session's in-flight `retired`-status work (`RequirementsTemplater.cs`) had to be waited out before
   the harness could build at all. The ratios are stable to <0.03 pp regardless, which is the point of running
   twice.
2. **The sandbox has no `.git`,** so deep-git analytics are off and commit/commit-day pages do not exist. The IR
   here is 875 pages against the ~1,408-page full inventory. This makes the reported percentages **conservative in
   the right direction**: a larger denominator with the same numerator would only lower them.
3. **The first version of this harness reported a false PASS** and is worth recording as a method lesson. It
   selected `.memlog.md` for the gated route — an *ignored* source file, which still classifies `Narrow` and still
   reaches `GenerateOne`, which then returns `Skipped` and renders nothing. The result was a delta of nothing
   measured against everything: **0.000 %, reported as passing.** The harness now (a) excludes
   `PathUtil.IsIgnoredSourceFile`, and (b) treats *liveness* as part of the gate — a route that did not dispatch
   where expected, returned `Skipped`, or changed zero pages **fails** rather than passes. A gate that passes
   because nothing happened is worse than one that fails.
4. **`diagnostics.html` did not appear in any delta**, contrary to Story 22.2's warning that it is output-path
   dependent. The harness uses one stable output directory per route, so the path never changes. On a machine that
   moves the output root it will appear; that remains expected and is not a bug.

## Bottom line

| Gate condition | Required | Measured | |
|---|---|---|---|
| `GenerateOne` delta vs full IR | < 5 % | **2.72 %** | ✅ |
| `GenerateOne` delta vs full webview payload | < 5 % | **4.09 %** | ✅ |
| Route dispatched as expected | `GenerateOne` | `GenerateOne` | ✅ |
| Route actually did work | not `Skipped`, >0 pages | `Updated`, 3 pages | ✅ |
| Stable across repeated runs | — | Δ < 0.03 pp | ✅ |

**Proceed with Tasks 2–9.**

