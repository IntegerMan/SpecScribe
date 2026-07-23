# `spike/ir-incremental` — Story 22.1 Incremental-Recompute + IR-Delta Spike

Everything here is **throwaway** and quarantined (see [`spike/README.md`](../README.md)): no `.sln` references it, it is
not part of `src/SpecScribe`'s build or `dotnet pack`, and it contributes **no** code path to the shipped `specscribe`
tool. The generated site is byte-identical with or without this folder. **The durable output is the spike report
[`_bmad-output/implementation-artifacts/22-1-spike-report.md`](../../_bmad-output/implementation-artifacts/22-1-spike-report.md)** —
the code here is the evidence behind it and can be deleted with the `spike/ir-incremental-22-1` branch.

This spike does **not** author a new ADR: [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md)
already decided the direction; Story 22.1 de-risks the *build* of that decision (mirroring how Story 6.6 → ADR 0006).

## What it measures

A single console probe (`specscribe-ir-incremental-spike`) that drives the **real shipped `SiteGenerator`** against a
mutable copy of this repo's own artifacts. It never re-parses `.md` itself and never scrapes `.html`; it diffs what the
core already produced (AD-1/AD-2).

- **Axis 1 — latency.** Full `GenerateAll` wall-clock, deep-git **ON vs OFF**, to isolate the git-subprocess share of
  gen-time (ADR 0008 says it dominates). Plus per-change-class incremental-route latency.
- **Axis 2 — changed-scope recompute correctness (the primary risk).** For each change class, it runs the shipped
  watch-mode incremental route (`RegenerateEpics` / `GenerateOne` / `RemoveFor` / `RegenerateAdrs` /
  `RegenerateFromDataSource`, dispatched in the exact predicate order [`FileWatcherService`](../../src/SpecScribe/FileWatcherService.cs)
  uses) on a **live** generator, then a **full** `GenerateAll` of the identical post-change source tree (the oracle),
  and diffs the two output trees byte-for-byte. It folds only the per-run/per-build/per-machine noise the
  `GoldenContentFingerprint` gate folds ([`NormalizeVolatile`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)).
  Byte-identical ⇒ the narrow route is correct for that class; any diff ⇒ staleness, enumerated as **stale / orphaned /
  missing** pages.
- **Axis 3 — IR-delta transport.** Using the shipped `SpaDelivery` manifest + content-chunk JSON as the IR, it measures
  the chunk set a single **content edit** vs a **topology delete** actually re-ships, and reports the chunker's
  byte/page caps (`MaxChunkBytes`, `MaxPagesPerChunk`).

The change classes: `content-story`, `content-doc`, `add-doc`, `delete-story`, `rename-doc`, `delete-adr`.

## Reproduce

```bash
dotnet run --project spike/ir-incremental/SpecScribe.IrIncrementalSpike.csproj -c Release -- \
  --repo <repo-root> --out <scratch-dir>
```

Writes `report.json` (machine-readable) to `<scratch-dir>` and prints it to stdout; a human-readable per-case summary
goes to stderr. The correctness sandbox runs **without** `.git` (deep-git off) so the incremental run and the oracle
read the identical inputs; the latency axis runs against the real repo with deep-git on and off.

## Findings

See the spike report for the full analysis and the 22.2–22.6 gate. Headline numbers land in that report's
_Measured Evidence_ section.
