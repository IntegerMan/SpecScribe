# `spike/delta-transport` — Story 22.6 Task 1 delta-measurement gate

Everything here is **throwaway** and quarantined (see [`spike/README.md`](../README.md)): no `.slnx` references it,
it is not part of `src/SpecScribe`'s build or `dotnet pack`, and it contributes **no** code path to the shipped
`specscribe` tool. The generated site is byte-identical with or without this folder. **The durable output is
[`22-6-delta-measurement-report.md`](../../_bmad-output/implementation-artifacts/22-6-delta-measurement-report.md)** —
the code here is the evidence behind it.

This is **not a feature spike**. It is Story 22.6 AC #1's **hard abort gate**: if it fails, 22.6 ships no production
code and returns to `backlog`.

## What it measures

Story 22.1 measured IR-delta transport at **chunk** granularity, only ever through `RegenerateEpics`, and flagged
its own blind spot: *"the byte-perfect `GenerateOne` route was never delta-measured."* Story 22.2 then delivered
**page**-level addressing (`contentHash` + `bytes` per page in `spa/manifest.json`), which is what makes a
page-granular measurement possible. This harness discharges that gate.

For each of the four watch routes `FileWatcherService.RunDebouncedPass` dispatches a content-only edit to —
`GenerateOne`, `RegenerateEpics`, `RegenerateAdrs`, `RegenerateFromDataSource` — it:

1. snapshots the IR manifest plus the full webview NDJSON payload,
2. runs a **no-op control**: the same route, same file, no edit, to separate per-regen churn from real change,
3. applies one appended line to one source file,
4. invokes the **shipped** route, mirroring the dispatcher's predicate order verbatim (including Story 22.5's
   `ClassifyRebuildScope` escalation) rather than guessing it,
5. diffs by `contentHash` and reports delta bytes against **both** totals.

Delta bytes are the **JSON-encoded** chunk-member tokens, not the manifest's raw `bytes` — same units as the
on-disk denominator. It drives the real `SiteGenerator` / `SpaDelivery` / `WebviewCommand`; no `.md` is re-parsed
and no `.html` is scraped (AD-1/AD-2).

## The gate

A single-file content edit via `GenerateOne` must produce a delta under **5 %** of *both* the full IR and the full
webview payload — **and** must actually have run (dispatched where expected, not `Skipped`, >0 pages changed).
That liveness half is part of the gate because the first version of this harness passed at 0.000 % by editing an
*ignored* file: a delta of nothing measured against everything.

## Reproduce

```bash
dotnet run --project spike/delta-transport/SpecScribe.DeltaTransportSpike.csproj -c Release -- --repo . --out ./scratch
```

Writes `report.json` to `<scratch>` and prints it to stdout; progress goes to stderr. Exit code **0** = gate passed,
**1** = gate failed. The sandbox is built without `.git`, so deep-git is off and every run reads identical inputs.

## Findings

See the report. Headline: **gate passes at 2.72 % / 4.09 %**, stable across two runs. Three findings changed the
implementation — most importantly that `RegenerateFromDataSource` returns `Skipped` *after* a full rebuild, which
falsifies the story's Trap 2 premise for that route and forces the delta basis to be captured at the **emit** seam
rather than gated on the reported outcome.
