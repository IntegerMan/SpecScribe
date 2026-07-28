---
baseline_commit: 811ba17
gated_by: 22-2-canonical-ir-schema-and-versioning # the per-page `contentHash` + `bytes` this story diffs; Story 22.1 named 22.2 (NOT 22.5) as 22.6's blocker
spike_gate: 22-1-spike-incremental-recompute-and-ir-delta-transport # §"22.6 must measure the GenerateOne delta before treating chunk-granularity as blocking" — Task 1 is a HARD ABORT gate
runs_before: 22-5-incremental-event-driven-regeneration-engine # owner decision D1 — recompute (22.5) and transport (22.6) are orthogonal
conflicts_with: 22-4-spa-and-webview-as-ir-consumers # 22.4 is ready-for-dev and MOVES the two hook sites this story attaches to; read § "If 22.4 landed first"
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # §Decision 3 "emits IR deltas suitable for a future watch / client-server transport (conformant with AD-8)"
owner_decisions: 2026-07-28 # D1 run now on 22.2's hashes; D2 delta-ify `--serve` + disk sidecar (no HTTP listener); D3 "Quiet Stamp" freshness direction; D4 hard abort gate
---

# Story 22.6: Client-Server Delta Channel

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer running a live SpecScribe consumer against an actively-changing repository,
I want the watch/serve channel to push **only what changed** — addressed by the per-page content hashes Story 22.2 already emits — plus a durable delta sidecar any polling consumer can read,
So that AD-8's "update transport is adapter-specific" clause is operationalized on both of its named halves (extension-host push and sidecar polling) without committing SpecScribe to a long-lived network server.

## Why this story looks different from epics.md — READ FIRST

epics.md's three ACs were written 2026-07-21, before Stories 22.1, 22.2, 23.1, 23.2, 23.3 and 23.5 ran. **This story's 8 ACs supersede them**; Task 9 records that drift in `epics.md` and `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.

### epics.md's "watch server" is NOT what this story builds

epics.md AC #1 says *"a watch server pushes deltas conformant with AD-8's transport-is-adapter-specific principle."* Read literally that implies a new long-lived **network listener**. **Owner decision D2 (2026-07-28) rejects that scope.** ADR 0008 §Consequences already flagged it as deferred — *"a future client/server mode adds a long-lived-process deployment shape (explicitly later; not decided here)"* — and nothing since has decided it.

What this story builds instead is **exactly AD-8's own two clauses**, verbatim from [ARCHITECTURE-SPINE.md § AD-8](../specs/spec-specscribe/ARCHITECTURE-SPINE.md):

> *"static HTML may hydrate via **URL hash plus sidecar polling**, while webview uses **extension host push**."*

| AD-8 clause | What 22.6 ships | New network surface |
|---|---|---|
| extension host push | `specscribe webview --serve --serve-delta` streams **delta frames** on the existing NDJSON stdout channel | **none** — stdout, already shipping |
| sidecar polling | `spa/delta.json` written beside the IR on each watch-mode regen | **none** — a file on disk |

No port is opened, no listener is bound, no new runtime is introduced, and ADR 0022's *"Node is a build toolchain and a generate-time runtime, never a shipped toolchain"* is untouched. **A `specscribe serve` HTTP/SSE server is explicitly out of scope** and, if ever wanted, needs its own ADR.

### The push channel already exists — and it pushes the whole site every time

This is the single most important fact for the dev agent: **do not build a new channel.** [`WebviewCommand.RunServeLoop`](../../src/SpecScribe/Commands.cs:142) has shipped since Story 6.4's deferred item. It:

1. constructs a `FileWatcherService` over the same debounce routes `specscribe watch` uses,
2. on every non-Error/non-Skipped event, calls `generator.RenderWebviewSurfaces()`,
3. re-serializes **the entire payload** via [`SerializePayload`](../../src/SpecScribe/Commands.cs:192) — `siteTitle`, `entry`, `document`, all four roots, **every surface's full `content`**, and `outline`,
4. writes it as one NDJSON line to stdout and flushes.

The extension consumes it in [`PersistentRenderer`](../../extension/src/extension.ts:1395), whose own guard comment measures the cost: *"a generous ceiling well above this repo's observed **~8 MB whole-site webview payload**"* ([extension.ts:1296](../../extension/src/extension.ts:1296)). **A one-character edit to one story file re-ships ~8 MB.** That is the defect this story closes, and it is live today.

The same shape holds on the SPA side: [`EmitSpaSite`](../../src/SpecScribe/SiteGenerator.cs:3267) is called from **six** sites in `SiteGenerator` (`:554`, `:573`, `:599`, `:715`, `:788`, `:1039`) and each one rewrites the manifest, **every** chunk, the client script and the entry shell.

### What Story 22.2 already gave us — the whole reason 22.6 is unblocked

`spa/manifest.json` carries, per page ([`SpaDelivery.ManifestEntry`](../../src/SpecScribe/SpaDelivery.cs:547)):

```
ContentHash   // truncated SHA-256 of the content region — 16 hex chars / 64 bits
Bytes         // the raw content-region size
Chunk         // which pages-*.json holds it
```

[`SpaDelivery.ContentHash`](../../src/SpecScribe/SpaDelivery.cs:298) is **already `public static`** and its doc comment names this story as the consumer:

> *"64 bits: at SpecScribe's page counts an accidental collision is far below the noise floor of anything **22.5/22.6** would do with it… the hash is a change detector **for delta addressing**."*

**Diff manifest N against manifest N−1 and you have the delta.** No incremental engine is required underneath — which is why owner decision D1 runs this story before 22.5. 22.5 makes *recompute* cheap; 22.6 makes *transport* cheap. They are orthogonal.

### Story 22.1's gate is a HARD ABORT (owner decision D4)

[22-1-spike-report.md](22-1-spike-report.md) §"22.6" reads:

> *"**Before treating chunk-granularity as blocking, 22.6 must measure the `GenerateOne` delta**; then proceed only after 22.2 delivers page-level delta addressing and re-measure."*

and the caveat it hangs on:

> *"Both delta events were driven through `RegenerateEpics`, and its own no-op over-count inflates the number… the byte-perfect `GenerateOne` route was **never delta-measured**."*

22.2 has delivered the page-level addressing. **Task 1 performs the re-measurement and is a gate, not a formality.** If it fails, the story writes its report, reverts to `backlog`, and implements nothing — which is epics.md AC #3 (*"remains deferred/unscheduled rather than implemented on assumption"*) honored literally rather than rhetorically.

## Acceptance Criteria

### 1. The spike gate is re-measured, and it is binding

**Given** Story 22.1 measured IR-delta size only through `RegenerateEpics` (25.3 %–39.9 % of a 48 MB IR) and explicitly never measured the byte-perfect `GenerateOne` route
**When** the page-level delta is measured against this repo for **all four** watch routes — `GenerateOne` (content-only generic doc), `RegenerateEpics`, `RegenerateAdrs`, `RegenerateFromDataSource`
**Then** the report states, per route: pages changed, delta bytes, full-IR bytes, full-webview-payload bytes, and the ratio of each
**And** the gate passes only if a **single-file content edit via `GenerateOne`** produces a delta under **5 %** of both the full IR and the full webview payload
**And if the gate fails**, the story halts: the measurement lands as `22-6-delta-measurement-report.md`, `sprint-status.yaml` returns `22-6-…` to `backlog` with the numbers, **no production code ships**, and ACs #2–#8 are not attempted.

### 2. A deterministic, watch-only delta sidecar

**Given** watch mode (or `--serve`) has emitted the IR at least twice
**When** a debounced regeneration completes
**Then** `spa/delta.json` is written beside `spa/manifest.json`, naming `changed` / `added` / `removed` page paths and the `chunks` that carry them, keyed off the previous emit's manifest
**And** it is written **atomically** (temp file + `File.Move(overwrite: true)`) so a polling consumer never reads a torn file — NFR5's "never take a write lock on the watched tree" is not weakened
**And** a one-shot `specscribe generate` emits **no** `spa/delta.json` at all, so a cold build stays byte-reproducible (NFR9) and no wall-clock timestamp enters a CI artifact.

The document's shape is a **contract**, not an implementation detail — Story 22.5 and any future consumer bind to these names. Emit exactly this:

```jsonc
{
  "deltaSchemaVersion": 1,
  "schemaVersion": 1,          // the IR SchemaVersion this delta is against; a mismatch ⇒ consumer refetches
  "sequence": 7,               // monotonic within one watch session; resets at session start
  "generatedAt": "2026-07-28T14:32:07.0000000Z",  // round-trip "O"; watch-only, never in a `generate` artifact
  "trigger": "_bmad-output/planning-artifacts/epics.md",  // the changed path, or "<directory change>" for a topology pass
  "full": false,               // true ⇒ every list below is empty and the consumer must refetch the manifest
  "changed": ["epics.html", "epics/epic-22.html"],
  "added": [],
  "removed": [],
  "chunks": ["spa/pages-epics.json"]   // the chunk files carrying `changed` + `added`
}
```

`trigger` reuses [`FileWatcherService.TopologyEventLabel`](../../src/SpecScribe/FileWatcherService.cs:27) (`"<directory change>"`) verbatim for the escalated pass — that constant is already shared with `SiteGenerator.RegenerateTopology` *"so the two can never drift"*; do not introduce a third spelling.

### 3. Delta frames on the existing NDJSON channel, behind an opt-in flag

**Given** `specscribe webview --serve --serve-delta`
**When** the first payload is produced
**Then** it is a **full** payload, byte-identical in shape to today's `SerializePayload` output — a cold consumer needs no special case
**And** every subsequent push is a delta frame carrying only changed/removed surfaces plus the (small) `outline`, tagged with a discriminator field and a monotonically increasing `sequence`
**And** `--serve` **without** `--serve-delta` streams full payloads exactly as it does today, so an older VSIX against a newer core is unaffected.

### 4. Disabled is byte-identical to today

**Given** neither `--serve-delta` nor watch mode is active
**When** `specscribe generate` (with or without `--spa`) runs
**Then** every output byte is unchanged, and `GoldenContentFingerprint` does not move
**And** `SpaDelivery.SchemaVersion` is **not** bumped — a new sidecar file is additive, and its own version lives in a separate `DeltaSchemaVersion` constant.

### 5. Freshness is signalled as text — the "Quiet Stamp" direction

**Given** a live consumer (SPA entry shell or webview) has a delta channel available
**When** the surface first renders and again after each applied delta
**Then** a small stamp in the existing page chrome reads its state as **words** — e.g. `Live updates: connected · updated 14:32` / `Live updates: unavailable` — never by color alone, never by motion, and with no layout shift on update
**And** the stamp is present in the initial server-rendered markup (so it is not a JS-only artifact) and is updated in place by the client
**And** it is emitted **only** on the SPA entry shell and the webview chrome — never on a static page, which has no live channel and whose bytes AC #4 pins.

### 6. A delta applied equals a full refetch — proved by oracle

**Given** a consumer holding state at manifest N−1
**When** the delta for N is applied to it
**Then** the resulting page set is **byte-identical** to what a cold consumer fetching manifest N would hold
**And** this is pinned by a test that drives real regenerations and diffs applied-delta state against full-emit state, in the spirit of the 22.1 spike's oracle-diff harness — not by asserting the delta document's shape alone.

### 7. Degrade to full, loudly, rather than emit a wrong delta

**Given** any condition under which the previous manifest is absent or untrustworthy — first emit of a session, a `RegenerateTopology` escalation, a `SpaDelivery.SchemaVersion` change between emits, or a caller-visible failure to read the prior state
**When** the delta is computed
**Then** it is emitted as a **full** marker (`"full": true`, no page lists) and the NDJSON channel sends a full payload frame
**And** a false "unchanged" is never emitted for a page that did change: the delta's trust boundary — that it is only as accurate as `_spaCapture`, which has a documented watch-mode drift class — is stated in code and in the story record.

### 8. The contract is recorded where contracts live

**Given** this story introduces a new on-disk artifact, a new CLI flag and a new wire frame — all cross-cutting consumer contracts
**When** the work completes
**Then** an ADR records the delta-channel contract and its deliberate exclusion of a network listener, cross-referenced from `docs/adrs/README.md` and from ADR 0008 §Decision 3
**And** `epics.md` § Story 22.6 and `sprint-status.yaml` record the AC supersession **in the same change**, per CLAUDE.md § Decision records.

## Tasks / Subtasks

- [ ] **Task 1 — THE GATE. Measure before building. (AC: #1)**
  - [ ] Build the measurement harness under `spike/` (quarantined, as 22.1 did) — do **not** touch `src/` in this task.
  - [ ] Drive real regenerations against a mutable copy of this repo's artifacts, one per watch route: `GenerateOne`, `RegenerateEpics`, `RegenerateAdrs`, `RegenerateFromDataSource`. Route selection logic lives at [`FileWatcherService.RunDebouncedPass`](../../src/SpecScribe/FileWatcherService.cs:381) — mirror it, don't guess.
  - [ ] For each: diff manifest before/after by `contentHash`; sum the `bytes` of changed pages; compare against full-IR bytes and against the full `SerializePayload` webview payload bytes.
  - [ ] Write `22-6-delta-measurement-report.md` with a per-route table. **Report the `RegenerateEpics` number even though it is inflated by the known no-op over-count** ([22.1 headline](22-1-spike-report.md)) — and say so, so the number is not read as this story's fault.
  - [ ] **STOP HERE if the `GenerateOne` gate fails.** Set `sprint-status.yaml` back to `backlog` with the numbers inline, and report to the owner. Do not proceed to Task 2.

- [ ] **Task 2 — The delta document, as a pure function (AC: #2, #7)**
  - [ ] Add `SpaDelivery.BuildDelta(previousManifestJson, currentManifestJson, …)` returning a delta record. **Pure and side-effect-free**, matching every other method in that file (its class doc comment: *"Every method here is side-effect-free string work"*).
  - [ ] Add `DeltaSchemaVersion` as its **own** constant with the same monotonically-increasing-integer compatibility rule `SchemaVersion`'s doc comment states. Do **not** bump `SchemaVersion` (AC #4).
  - [ ] Encode the degrade-to-full path (AC #7) inside `BuildDelta` so no caller can forget it.
  - [ ] Unit-test `BuildDelta` directly in `SpaDeliveryTests.cs`: unchanged site → empty delta; one page edited → exactly that page; page added; page removed; absent prior → `full`; schema-version mismatch → `full`.

- [ ] **Task 3 — Emit the sidecar in watch mode only (AC: #2, #4)**
  - [ ] Hold the previous emit's manifest on `SiteGenerator` and update it inside `EmitSpaSite`, where `SpaDelivery.BuildDataFiles(bundle)` already produces the new one — one place, six callers.
  - [ ] Gate emission on watch/serve, **not** on `--spa` alone, so a one-shot `generate --spa` writes no `delta.json` (AC #2, NFR9).
  - [ ] Write atomically: temp file under the output root, then `File.Move(temp, target, overwrite: true)`. `WriteSpaFile` uses a plain `File.WriteAllText` ([SiteGenerator.cs:3306](../../src/SpecScribe/SiteGenerator.cs:3306)) and is **not** safe for a concurrently-polling reader — add a sibling writer rather than changing that one.
  - [ ] Assert in `SiteGeneratorSpaTests.cs` that a one-shot `generate --spa` produces **no** `spa/delta.json`, and that two consecutive watch regens produce one whose contents match `BuildDelta`'s.

- [ ] **Task 4 — Delta frames on the NDJSON channel (AC: #3)**
  - [ ] Add a `--serve-delta` switch to `SiteSettings` alongside `--serve`.
  - [ ] Add `WebviewCommand.SerializeDeltaPayload(previousBundle, currentBundle, sequence, …)` as a **`public static` pure method**, exactly like the existing `SerializePayload` / `SerializeDiagnostics` / `ResolveConfiguredOutputRoot` pair-testable seams. **This is not optional style**: `RunServeLoop` is `private static`, blocks on a `ManualResetEventSlim`, spawns a real `FileWatcherService` and writes to `Console` — it has **zero** test coverage today (`WebviewCommandTests.cs` covers only the pure helpers), and any delta logic written *inside* it is untestable by construction.
  - [ ] Reuse `SpaDelivery.ContentHash` for the webview surfaces' change detection — do not introduce a second hash function.
  - [ ] Wire `RunServeLoop` to call the pure method; keep the first frame full.
  - [ ] Extend `WebviewCommandTests.cs` with the frame-shape contract: first frame full; second frame delta with only changed surfaces; `--serve-delta` off ⇒ byte-identical to `SerializePayload`.

- [ ] **Task 5 — Consume the delta in the extension (AC: #3, #6)**
  - [ ] Extend `PersistentRenderer` ([extension.ts:1395](../../extension/src/extension.ts:1395)) to merge a delta frame into its cached payload instead of replacing it.
  - [ ] Pass `--serve-delta` on spawn. The existing `persistentUnavailable` fallback ([extension.ts:580](../../extension/src/extension.ts:580)) already handles an older core that rejects an unknown flag — verify that path still degrades to `loadViaSpawn` rather than hanging.
  - [ ] Preserve the documented invariant that *"a live-pushed `--serve` payload and a one-shot spawn payload are indistinguishable"* ([extension.ts:612](../../extension/src/extension.ts:612)) — the merge must produce the same object shape the one-shot path yields.

- [ ] **Task 6 — The Quiet Stamp (AC: #5)**
  - [ ] Emit the stamp in [`SpaDelivery.BuildEntryShell`](../../src/SpecScribe/SpaDelivery.cs) and in the webview chrome. **Not** in `PathUtil.RenderHeadOpen` — that is shared with every static page and would move the golden fingerprint (AC #4).
  - [ ] Style it off the existing `--ink-light` / small-type conventions the `.coverage-freshness` rule already uses (`specscribe.css:4243`) rather than inventing a token. No `--motion-*` usage — the direction is deliberately motionless.
  - [ ] Client updates `textContent` in place. No element insertion/removal, no height change.
  - [ ] Test: the stamp's text conveys state without color; it is absent from every static page; a reduced-motion media query is unnecessary and none is added.

- [ ] **Task 7 — The oracle test (AC: #6)**
  - [ ] Drive a real generator: emit N−1, apply a source edit, emit N, apply the delta to an N−1 page set, assert byte-identity against the N page set.
  - [ ] Cover at minimum: content-only edit (`GenerateOne`), epics edit (`RegenerateEpics`), a file delete (`RemoveFor`), and a directory rename (`RegenerateTopology` → must degrade to `full`).

- [ ] **Task 8 — Live browser verification (CLAUDE.md § Verification)**
  - [ ] Generate with `--spa` to `SpecScribeOutput/` (**never** `--output docs/live`), serve it, run a watch session, edit a source file, and **look at the page**: confirm the region swaps, the stamp updates, no console error, and no layout shift.
  - [ ] The test suite structurally cannot see layout collapse or DOM corruption from markup splicing — all three of those shipped before and were caught only by looking.

- [ ] **Task 9 — Record the contracts (AC: #8)**
  - [ ] Write the ADR. **Read `docs/adrs/` for the next free number at dev time** — `0019` is claimed-but-unwritten (Story 18.3), `0023` is pre-claimed (Story 22.4), so `0024` is the first uncontested slot *as of this writing*; verify, don't assume.
  - [ ] Cross-reference it from `docs/adrs/README.md` and from ADR 0008 §Decision 3.
  - [ ] Update `epics.md` § Story 22.6 **and** `sprint-status.yaml` in the **same** change.

## Dev Notes

### Read these files before writing anything

| File | Why |
|---|---|
| [`Commands.cs:121-186`](../../src/SpecScribe/Commands.cs:121) | `RunServeLoop` — the channel you are extending, and the `pushLock` you must not break |
| [`SpaDelivery.cs:290-310, 440-565`](../../src/SpecScribe/SpaDelivery.cs) | `ContentHash`, `BuildDataFiles`, the `Manifest`/`ManifestEntry` records |
| [`SiteGenerator.cs:3267-3312`](../../src/SpecScribe/SiteGenerator.cs:3267) | `EmitSpaSite` + `WriteSpaFile` — the one place the manifest is produced |
| [`FileWatcherService.cs:381-408`](../../src/SpecScribe/FileWatcherService.cs:381) | `RunDebouncedPass` — the four routes Task 1 must measure |
| [`extension.ts:576-680, 1395-1460`](../../extension/src/extension.ts:576) | `persistentUnavailable` fallback + `PersistentRenderer` |

### ⚠️ Traps — each of these fails silently

1. **The previous-manifest field is shared mutable state across threads.** `FileWatcherService` fires **one debounce `Timer` per distinct changed path, each on its own thread-pool thread** — `RunServeLoop`'s own comment says so, which is why `pushLock` exists ([Commands.cs:144-148](../../src/SpecScribe/Commands.cs:144)). Two files saved in the same window can invoke the delta computation concurrently. If the "previous manifest" is read and replaced outside a lock, a delta is emitted against the wrong basis: a page that changed is reported unchanged, or vice versa, **with no test failing**. Own that state under the same serialization the stdout write already has.

2. **Do not advance the basis on Error or Skipped.** `RunServeLoop` returns early for `GenerationOutcome.Error or GenerationOutcome.Skipped` ([Commands.cs:155](../../src/SpecScribe/Commands.cs:155)) because *"the generator's in-memory state is unchanged."* If the delta basis advances on those events anyway, the next real push diffs against a manifest that was never emitted.

3. **`_spaCapture` has a documented watch-mode drift class, and the delta inherits it.** The capture is evicted and repopulated at `SiteGenerator.cs` `:883`, `:897`, `:933` and `:1311`, each carrying a `[deferred-work: story-6-7 watch-mode _spaCapture drift]` marker. **A stale capture yields a stale `contentHash`, which yields a false "unchanged" — the one failure mode worse than a false "changed."** AC #7 requires this trust boundary be stated in code, not discovered later.

4. **`diagnostics.html` will appear in nearly every delta on some machines.** Story 22.2 found it is *"THE one page whose `contentHash` is output-path dependent"* — it echoes the configured output root inside its own region. Not a bug; do not "fix" it by normalizing the hash (22.2 deliberately normalized nothing so the hash describes the shipped bytes). Just don't be surprised by it in Task 1's numbers.

5. **`RegenerateTopology` is a whole-site rebuild.** A literal diff there produces a thousand-entry `changed` list — larger and slower than the full payload it was meant to replace. AC #7's degrade-to-full covers it; make sure the topology route actually reaches that branch.

6. **The golden fingerprint is `f4a7cbac5bee0fe56aa4ef9950a114a23acc8b2d59eb2e255e4b47e27873f0cd`** at [`SiteGeneratorAdapterTests.cs:1242`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:1242) — **not** 22.4's `3171cf5c…`, not `06788c0f…`, not `2bd1c18e…`. It has moved four times in the last week. **Read it from the file.** The golden fixture generates **without** `--spa`, so nothing in Tasks 2–4 can move it; only Task 6's stamp could, and only if it is misplaced into `RenderHeadOpen`. Per CLAUDE.md, confirm any regenerated hash is stable across **two** repeated runs before locking it in, and name whose concurrent changes it sits on top of.

7. **Wall-clock in an artifact is an NFR9 hazard.** `spa/delta.json` carries a timestamp by nature. AC #2's watch-only rule is what keeps it out of a reproducible `generate`. Do not "helpfully" emit a `{"full": true}` delta on cold generate — that is how the timestamp gets into CI.

### If 22.4 landed first

[Story 22.4](22-4-spa-and-webview-as-ir-consumers.md) is `ready-for-dev` and unifies `BuildSpaBundle` (`:3101`) and `RenderWebviewSurfaces` (`:2810`) onto **one** region seam. Both are hook sites for this story. If 22.4 has landed when you start:

- there is **one** builder to hook, not two — strictly simpler;
- `SpaDelivery.ContentHash` becomes usable for the webview surfaces without a second projection, because both sides carry the same region shape;
- 22.4 also fixes the two-region-shapes defect (family pages carry the `page-wayfinding` wrapper, ~853 captured pages slice from inside it), which today means **two different content shapes hash differently for reasons unrelated to change**. Task 1's numbers should note which side of 22.4 they were taken on.

If 22.4 has **not** landed, hook both builders and say so in the File List — do not refactor them into one as a side effect. That is 22.4's story, and taking it here would make its review unscopeable.

### The Nuxt app is NOT a consumer of this story

`web/` reads the IR manifest through [`web/ir/adapter.ts`](../../web/ir/adapter.ts), but it does so **at prerender time** — `nuxt.config.ts` builds `nitro.prerender.routes` from `site.paths` at config load, and ADR 0022 establishes Node as *"a build toolchain and a generate-time runtime, never a shipped toolchain."* There is no long-lived Nuxt process to push to. **Do not add a delta consumer to `web/`**, and do not touch `web/ir/`. If Epic 23 ever wants live updates it re-runs generation, which re-runs prerender.

### Anti-patterns this story exists to avoid

- **Do not write a new HTTP server.** Owner decision D2. ADR 0008 defers the deployment shape; nothing has un-deferred it.
- **Do not write a second content-hash function.** `SpaDelivery.ContentHash` is `public static` and its doc comment names 22.5/22.6 as its consumers.
- **Do not put logic inside `RunServeLoop`.** It is untestable by construction — no coverage exists for it today. Follow the `SerializePayload` / `SerializeDiagnostics` precedent: pure `public static` method, thin loop.
- **Do not bump `SpaDelivery.SchemaVersion`.** Its own doc comment: *"Do NOT bump for a purely ADDITIVE field."* A new sidecar file is more additive than a field.
- **Do not touch `git reset --hard`, `git checkout --`, or `git clean`.** CLAUDE.md § Concurrent work — another session's uncommitted work is likely in the tree right now (`git status` at story-creation time showed 10+ modified files including `Commands.cs`, which this story edits).
- **Grep-verify every symbol you add before relying on it.** A `Charts.cs` edit has silently vanished on shared main before.

### Testing standards

xUnit under `tests/SpecScribe.Tests/`. Extend, don't create: `SpaDeliveryTests.cs` (Task 2), `SiteGeneratorSpaTests.cs` (Task 3), `WebviewCommandTests.cs` (Task 4), `FileWatcherServiceTests.cs` (concurrency, Trap 1). The oracle test (Task 7) is new and belongs alongside `CanonicalIrSerializationTests.cs`, which is Story 22.2's round-trip boundary and the closest precedent.

Baseline at story creation: the suite was last recorded green at 2427 passed / 0 failed / 3 skipped (the 3 skips are the pre-existing symlink-privilege tests). Two known flakes rotate through file-write contention (`BurstOfSaves`, the GitInsights hub test) — if either fails, re-run in isolation before treating it as yours.

### Project Structure Notes

New files: `spike/` measurement harness (Task 1, quarantined), `_bmad-output/implementation-artifacts/22-6-delta-measurement-report.md`, `docs/adrs/00XX-*.md`. Modified: `SpaDelivery.cs`, `SiteGenerator.cs`, `Commands.cs`, `SiteSettings.cs`, `assets/specscribe-spa.js`, `assets/specscribe.css`, `extension/src/extension.ts`, plus tests. No new package references — the delta is `System.Text.Json` over records SpecScribe already models.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 22.6: (Spike-gated) Client-Server Delta Channel] — the three superseded ACs
- [Source: _bmad-output/implementation-artifacts/22-1-spike-report.md#Axis 3 — IR-delta transport] — the 25.3 %/39.9 % figures, the `GenerateOne` caveat, and the gate this story discharges
- [Source: _bmad-output/implementation-artifacts/22-2-canonical-ir-schema-and-versioning.md] — `contentHash`, `bytes`, `oversizedPages`, and the `diagnostics.html` output-path caveat
- [Source: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md#Decision 3] — *"emits IR deltas suitable for a future watch / client-server transport (conformant with AD-8)"*; §Consequences defers the long-lived-process deployment shape
- [Source: docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md] — why no new shipped runtime
- [Source: _bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md#AD-8] — the two transport clauses this story implements; AD-5 for the changed-scope rule
- [Source: CLAUDE.md#Verification] — live-browser verification, `SpecScribeOutput/`, text equivalents, no color-only state

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
