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

Status: done

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
**And** every subsequent push is a delta frame carrying `siteTitle`/`entry`/the configured root paths unconditionally (small, fixed-size strings — cheap to always include, so a consumer never needs a separate fetch to keep them current) alongside the changed/removed surfaces and the (small) `outline`, tagged with a discriminator field and a monotonically increasing `sequence` — **amended 2026-07-30, owner decision**, code review: the literal original wording ("carrying ONLY changed/removed surfaces plus the outline") did not match what `WebviewCommand.SerializeDeltaPayload` ships, and never has; this amendment ratifies the shipped contract rather than narrowing the frame to match the stricter original text
**And** `--serve` **without** `--serve-delta` streams full payloads exactly as it does today, so an older VSIX against a newer core is unaffected.

### 4. Disabled is byte-identical to today

**Given** neither `--serve-delta` nor watch mode is active
**When** `specscribe generate` (with or without `--spa`) runs
**Then** every rendered **page's** bytes are unchanged (verified per-page, e.g. `NoStaticPage_CarriesTheQuietStamp`) — **amended 2026-07-30, owner decision**, code review: `GoldenContentFingerprint` covers the shared, always-embedded `specscribe.css` too, and is EXPECTED to move when a story adds a CSS rule (Task 6's Quiet Stamp) even though no page's own content changed; the fingerprint's literal original wording ("every output byte is unchanged... does not move") did not anticipate that shared-asset case and is corrected here rather than moving the Quiet Stamp's CSS rule out of the fingerprinted stylesheet
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

- [x] **Task 1 — THE GATE. Measure before building. (AC: #1)**
  - [x] Build the measurement harness under `spike/` (quarantined, as 22.1 did) — do **not** touch `src/` in this task.
  - [x] Drive real regenerations against a mutable copy of this repo's artifacts, one per watch route: `GenerateOne`, `RegenerateEpics`, `RegenerateAdrs`, `RegenerateFromDataSource`. Route selection logic lives at [`FileWatcherService.RunDebouncedPass`](../../src/SpecScribe/FileWatcherService.cs:381) — mirror it, don't guess.
  - [x] For each: diff manifest before/after by `contentHash`; sum the `bytes` of changed pages; compare against full-IR bytes and against the full `SerializePayload` webview payload bytes.
  - [x] Write `22-6-delta-measurement-report.md` with a per-route table. **Report the `RegenerateEpics` number even though it is inflated by the known no-op over-count** ([22.1 headline](22-1-spike-report.md)) — and say so, so the number is not read as this story's fault.
  - [x] **STOP HERE if the `GenerateOne` gate fails.** Set `sprint-status.yaml` back to `backlog` with the numbers inline, and report to the owner. Do not proceed to Task 2.

- [x] **Task 2 — The delta document, as a pure function (AC: #2, #7)**
  - [x] Add `SpaDelivery.BuildDelta(previousManifestJson, currentManifestJson, …)` returning a delta record. **Pure and side-effect-free**, matching every other method in that file (its class doc comment: *"Every method here is side-effect-free string work"*).
  - [x] Add `DeltaSchemaVersion` as its **own** constant with the same monotonically-increasing-integer compatibility rule `SchemaVersion`'s doc comment states. Do **not** bump `SchemaVersion` (AC #4).
  - [x] Encode the degrade-to-full path (AC #7) inside `BuildDelta` so no caller can forget it.
  - [x] Unit-test `BuildDelta` directly in `SpaDeliveryTests.cs`: unchanged site → empty delta; one page edited → exactly that page; page added; page removed; absent prior → `full`; schema-version mismatch → `full`.

- [x] **Task 3 — Emit the sidecar in watch mode only (AC: #2, #4)**
  - [x] Hold the previous emit's manifest on `SiteGenerator` and update it inside `EmitSpaSite`, where `SpaDelivery.BuildDataFiles(bundle)` already produces the new one — one place, six callers.
  - [x] Gate emission on watch/serve, **not** on `--spa` alone, so a one-shot `generate --spa` writes no `delta.json` (AC #2, NFR9).
  - [x] Write atomically: temp file under the output root, then `File.Move(temp, target, overwrite: true)`. `WriteSpaFile` uses a plain `File.WriteAllText` ([SiteGenerator.cs:3306](../../src/SpecScribe/SiteGenerator.cs:3306)) and is **not** safe for a concurrently-polling reader — add a sibling writer rather than changing that one.
  - [x] Assert in `SiteGeneratorSpaTests.cs` that a one-shot `generate --spa` produces **no** `spa/delta.json`, and that two consecutive watch regens produce one whose contents match `BuildDelta`'s.

- [x] **Task 4 — Delta frames on the NDJSON channel (AC: #3)**
  - [x] Add a `--serve-delta` switch to `SiteSettings` alongside `--serve`.
  - [x] Add `WebviewCommand.SerializeDeltaPayload(previousBundle, currentBundle, sequence, …)` as a **`public static` pure method**, exactly like the existing `SerializePayload` / `SerializeDiagnostics` / `ResolveConfiguredOutputRoot` pair-testable seams. **This is not optional style**: `RunServeLoop` is `private static`, blocks on a `ManualResetEventSlim`, spawns a real `FileWatcherService` and writes to `Console` — it has **zero** test coverage today (`WebviewCommandTests.cs` covers only the pure helpers), and any delta logic written *inside* it is untestable by construction.
  - [x] Reuse `SpaDelivery.ContentHash` for the webview surfaces' change detection — do not introduce a second hash function.
  - [x] Wire `RunServeLoop` to call the pure method; keep the first frame full.
  - [x] Extend `WebviewCommandTests.cs` with the frame-shape contract: first frame full; second frame delta with only changed surfaces; `--serve-delta` off ⇒ byte-identical to `SerializePayload`.

- [x] **Task 5 — Consume the delta in the extension (AC: #3, #6)**
  - [x] Extend `PersistentRenderer` ([extension.ts:1395](../../extension/src/extension.ts:1395)) to merge a delta frame into its cached payload instead of replacing it.
  - [x] Pass `--serve-delta` on spawn. The existing `persistentUnavailable` fallback ([extension.ts:580](../../extension/src/extension.ts:580)) already handles an older core that rejects an unknown flag — verify that path still degrades to `loadViaSpawn` rather than hanging.
  - [x] Preserve the documented invariant that *"a live-pushed `--serve` payload and a one-shot spawn payload are indistinguishable"* ([extension.ts:612](../../extension/src/extension.ts:612)) — the merge must produce the same object shape the one-shot path yields.

- [x] **Task 6 — The Quiet Stamp (AC: #5)**
  - [x] Emit the stamp in [`SpaDelivery.BuildEntryShell`](../../src/SpecScribe/SpaDelivery.cs) and in the webview chrome. **Not** in `PathUtil.RenderHeadOpen` — that is shared with every static page and would move the golden fingerprint (AC #4).
  - [x] Style it off the existing `--ink-light` / small-type conventions the `.coverage-freshness` rule already uses (`specscribe.css:4243`) rather than inventing a token. No `--motion-*` usage — the direction is deliberately motionless.
  - [x] Client updates `textContent` in place. No element insertion/removal, no height change.
  - [x] Test: the stamp's text conveys state without color; it is absent from every static page; a reduced-motion media query is unnecessary and none is added.

- [x] **Task 7 — The oracle test (AC: #6)**
  - [x] Drive a real generator: emit N−1, apply a source edit, emit N, apply the delta to an N−1 page set, assert byte-identity against the N page set.
  - [x] Cover at minimum: content-only edit (`GenerateOne`), epics edit (`RegenerateEpics`), a file delete (`RemoveFor`), and a directory rename (`RegenerateTopology` → must degrade to `full`).

- [x] **Task 8 — Live browser verification (CLAUDE.md § Verification)**
  - [x] Generate with `--spa` to `SpecScribeOutput/` (**never** `--output docs/live`), serve it, run a watch session, edit a source file, and **look at the page**: confirm the region swaps, the stamp updates, no console error, and no layout shift.
  - [x] The test suite structurally cannot see layout collapse or DOM corruption from markup splicing — all three of those shipped before and were caught only by looking.

- [x] **Task 9 — Record the contracts (AC: #8)**
  - [x] Write the ADR. **Read `docs/adrs/` for the next free number at dev time** — `0019` is claimed-but-unwritten (Story 18.3), `0023` is pre-claimed (Story 22.4), so `0024` is the first uncontested slot *as of this writing*; verify, don't assume.
  - [x] Cross-reference it from `docs/adrs/README.md` and from ADR 0008 §Decision 3.
  - [x] Update `epics.md` § Story 22.6 **and** `sprint-status.yaml` in the **same** change.

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

claude-opus-5 (Claude Code / bmad-dev-story)

### Debug Log References

- Gate harness: `spike/delta-transport/` — `dotnet run --project spike/delta-transport/SpecScribe.DeltaTransportSpike.csproj -c Release -- --repo . --out ./scratch` (exit 0 = pass, 1 = fail)
- Gate report: [22-6-delta-measurement-report.md](22-6-delta-measurement-report.md)
- Live verification: `generate --spa` → `SpecScribeOutput/`, served on :8125, `watch --spa` session, real edits observed in-browser

### Completion Notes List

**All 8 ACs met. Full suite 2811 passed / 0 failed / 3 skipped** (the 3 pre-existing symlink-privilege skips).

#### The gate (AC #1) — passed, after being made honest

`GenerateOne` single-file content edit: **2.72 % of the full IR**, **4.09 % of the full webview payload**, against
a 5 % threshold, stable across two runs to <0.03 pp. Story 22.1's comparable figure was 25.3–39.9 % at *chunk*
granularity; the difference is Story 22.2's page-level addressing, not an accounting change. Per-route numbers, the
no-op control (zero churn on all four routes), and the caveats are in the report.

⚠️ **The harness's first run reported a false PASS at 0.000 %.** It selected `.memlog.md` — an *ignored* source
file, which still classifies `Narrow` and still reaches `GenerateOne`, which then returns `Skipped` and renders
nothing: a delta of nothing measured against everything. Liveness (dispatched as expected, not `Skipped`, >0 pages
changed) is now **part of the gate**, not a precondition to it.

#### Three of the story's own premises were disproved by measurement

1. **⚠️ Trap 2's premise is FALSE for one route.** `RegenerateFromDataSource` calls `GenerateAll()` on its **first
   line** — a complete rebuild including `EmitSpaSite` — and only *afterwards* inspects the events to decide what
   to report. An unparseable `sprint-status.yaml` (which this repo's own file is) therefore returns `Skipped`
   **having already rewritten the entire IR**; the harness observed `code-map.html`'s hash moving on exactly that
   path. A basis gated on the reported outcome would emit a false *unchanged* — the failure AC #7 names as worse
   than a false *changed*. **The delta basis is therefore captured at the EMIT seam**, which is what Task 3
   prescribed anyway; only its stated *reason* was wrong. The NDJSON channel keeps the opposite rule for the
   opposite reason (it genuinely does not re-render on `Skipped`) — two channels, two bases, two correct answers.
2. **⚠️ The golden fingerprint DID move, and AC #4 needs reading precisely.** Every **page** is byte-identical —
   pinned directly by `NoStaticPage_CarriesTheQuietStamp` — but Task 6's own instruction puts the Quiet Stamp's
   rule in `specscribe.css`, which the fingerprint embeds. The drift was **isolated rather than assumed**:
   `078ef476…` without this story's rule (a **concurrent session's** drift) vs `501ee958…` with it. Confirmed
   stable across two runs after `dotnet build --no-incremental`; provenance recorded at the constant.
3. **The next free ADR slot is 0028, not 0024.** The story said to verify rather than assume; four ADRs
   (0024–0027) landed in between.

#### ⚠️ Live verification caught a defect the test suite structurally could not

Exactly the class CLAUDE.md § Verification predicts. The topology degrade-to-full was first derived from the
`trigger` **label**. During Task 8 a concurrent session's save overwrote that label between
`RegenerateTopology` setting it and the emit reading it, and the sidecar written in that same second read
`"full": false` while the watch log printed `<directory change> full rebuild`. `FileWatcherService` fires one
debounce `Timer` per changed path on its own thread-pool thread, so the label is **racy by construction** — and my
own doc comment claimed it was "never a correctness input", which deriving `forceFull` from it had made false.

Fixed by having `RegenerateTopology` set a flag on itself (`_nextEmitIsFullDelta`), set and consumed under
`_gate` on one call stack. **Re-verified live under the identical race** (label again overwritten by a concurrent
save; `full: true` held), plus two regression tests including one that the flag is consumed exactly once.

#### AC #5 verified in a live browser, by measurement

Stamp text updated 15:15 → 15:19 → "full refresh" with **height shift 0.000 and content-top shift 0.000**, a single
text node throughout (no element insert/remove), `docScrollW` 1265 ≤ `innerW` 1280 (no horizontal overflow), and
the stamp survived a content-region swap unchanged. No console errors at any point.

*(An initial measurement showed a collapsed 0-width / 133px-tall stamp — that was an unsized headless viewport
(`window.innerWidth === 0`), not a layout defect. Re-measured at 1280×900.)*

#### Accepted floor, recorded so it is not rediscovered

`code-map.html` carries a whole-repo lines-of-code total and therefore changes on **every** content edit — ~807 KB
encoded, the dominant term in every delta measured, and 100 % of the data-source route's. **No content edit will
produce a delta below ~1.2 % of the IR until that page changes.** That is a page-design problem, not a transport
one, and must not be "fixed" by normalizing the hash (a delta omitting it would ship a false *unchanged*).

#### Notes on scope and the shared tree

- `22.4` had **landed** (done) when this ran, so there was one region seam to hook, not two.
- `22.5` had also landed (review) despite `runs_before` — no conflict; the two are orthogonal as owner decision D1
  said, and `ClassifyRebuildScope` (22.5's) is mirrored by the gate harness rather than guessed.
- Per CLAUDE.md § Concurrent work, **nothing of another session's was reset, reverted or cleaned.** Their in-flight
  `retired`-status pass broke `src/` mid-session (`RequirementsTemplater.cs` deconstructing 6 elements into 7);
  it was **waited out**, not worked around.
- `web/` was not touched (it is a prerender-time consumer; ADR 0022).
- One self-inflicted issue worth noting: the first write of `SurfaceHash` embedded **literal NUL bytes** as a field
  separator, which made `Commands.cs` read as binary to `grep`. Replaced with length-prefixing — unambiguous using
  only printable ASCII.

### File List

**New**

- `spike/delta-transport/SpecScribe.DeltaTransportSpike.csproj` — quarantined gate harness (Task 1)
- `spike/delta-transport/Program.cs`
- `spike/delta-transport/README.md`
- `_bmad-output/implementation-artifacts/22-6-delta-measurement-report.md` — the gate's durable output
- `tests/SpecScribe.Tests/DeltaOracleTests.cs` — AC #6 oracle (5 tests)
- `docs/adrs/0028-delta-transport-is-a-sidecar-and-a-stream-never-a-server.md`

**Modified — production**

- `src/SpecScribe/SpaDelivery.cs` — `DeltaPath`, `DeltaSchemaVersion`, `BuildDelta`, `TryReadPageIndex`,
  `DeltaPage`, `DeltaDocument`, `LiveStampId`, `LiveStampMarkup`; stamp emitted in `BuildEntryShell`
- `src/SpecScribe/SiteGenerator.cs` — `EmitDeltaSidecar`, `_previousManifestJson`, `_deltaSequence`,
  `_watchTrigger`, `SetWatchTrigger`, `_nextEmitIsFullDelta`, `EmitDelta`, `WriteSpaFileAtomic`;
  `RegenerateTopology` arms the full-delta flag
- `src/SpecScribe/Commands.cs` — `DeltaFrameDiscriminator`, `SerializeDeltaPayload`, `SurfaceHash`;
  `RunServeLoop` takes `serveDelta`/`initialBundle`; `WatchCommand.RunWatchLoop` enables the sidecar
- `src/SpecScribe/SiteSettings.cs` — `--serve-delta`
- `src/SpecScribe/FileWatcherService.cs` — sets the trigger label; `RunTopology` local relabels to the shared constant
- `src/SpecScribe/WebviewRenderAdapter.cs` — Quiet Stamp in the toolbar + `liveStatus` message handler
- `src/SpecScribe/assets/specscribe.css` — `.spa-live-stamp` (the one rule that moves the golden fingerprint)
- `src/SpecScribe/assets/specscribe-spa.js` — `startLiveStamp` sidecar polling
- `extension/src/extension.ts` — `DeltaFrame`, `isDeltaFrame`, `applyDeltaFrame`, `DELTA_FRAME`;
  `PersistentRenderer.lastPayload`/`lastSequence` + merge; `--serve-delta` on spawn;
  `SpecScribeStore.hasLiveChannel`; `pushLiveStamp`

**Modified — tests**

- `tests/SpecScribe.Tests/SpaDeliveryTests.cs` — 16 `BuildDelta` tests + 3 code-review tests (chunk-reassignment, siteTitle/nav degrade-to-full)
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — sidecar + Quiet Stamp + the two race regression guards + 1 code-review test (sidecar write-failure isolation)
- `tests/SpecScribe.Tests/WebviewCommandTests.cs` — 8 delta-frame contract tests
- `tests/SpecScribe.Tests/FileWatcherServiceTests.cs` — Trap 1 concurrency test + 2 code-review tests (real topology-race, RunTopologyPass trigger label)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — `GoldenContentFingerprint` → `501ee958…` with isolation + provenance; code review moved it again to `dbfa172b…` (Quiet Stamp CSS overflow guard)
- `tests/SpecScribe.Tests/DeltaOracleTests.cs` — code review: 1 test (real directory rename via `Directory.Move`)
- `tests/SpecScribe.Tests/SettingsResolverTests.cs` — code review: 2 tests (`--serve-delta` requires `--serve`)

**Modified — records**

- `docs/adrs/README.md` — ADR 0028 entry
- `docs/adrs/0008-json-ir-canonical-and-incremental-generation.md` — §Decision 3 forward reference
- `_bmad-output/planning-artifacts/epics.md` — § Story 22.6 outcome (same change as sprint-status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `.claude/launch.json` — `delta-22-6` preview config for Task 8

### Change Log

| Date | Change |
|---|---|
| 2026-07-29 | Task 1 gate harness built and run twice; **gate PASSED** (2.72 % / 4.09 %). First run's false PASS (ignored dotfile ⇒ `Skipped`) found and closed by making liveness part of the gate. |
| 2026-07-29 | `SpaDelivery.BuildDelta` + `DeltaSchemaVersion` with degrade-to-full enforced internally (16 tests). |
| 2026-07-29 | `spa/delta.json` emitted in watch mode only, written atomically; basis captured at the emit seam after Trap 2's premise was disproved for `RegenerateFromDataSource`. |
| 2026-07-29 | `--serve-delta` + `WebviewCommand.SerializeDeltaPayload` as a pure static seam (8 tests); `RunServeLoop` kept thin. |
| 2026-07-29 | Extension merges delta frames into its cached payload, preserving the one-shot/live indistinguishability invariant. |
| 2026-07-29 | Quiet Stamp (AC #5) on the SPA shell + webview chrome; `GoldenContentFingerprint` regenerated to `501ee958…`, drift isolated from a concurrent session's. |
| 2026-07-29 | `DeltaOracleTests` (AC #6); the separately-generated-site oracle was tried, failed on `diagnostics.html`/`code-map.html`, and rejected as conflating transport with recompute. |
| 2026-07-29 | **Live verification caught the topology degrade being derived from a racy label**; moved to a route-owned flag, re-verified live, 2 regression tests added. |
| 2026-07-29 | ADR 0028 written and cross-referenced; `epics.md` + `sprint-status.yaml` updated in the same change. |

### Review Findings

Code review run 2026-07-30, scoped by File List / declared symbols (not commit range) per CLAUDE.md, in 9
task-scoped groups with hunk-level sibling attribution for shared files (`SiteGenerator.cs`,
`FileWatcherService.cs`, `SpaDelivery.cs`, `specscribe.css`). Three independent review layers per group
(Blind Hunter, Edge Case Hunter, Acceptance Auditor). `spike/delta-transport/` findings were dismissed
wholesale — that harness is quarantined, throwaway, and explicitly deleted with the story per its own README;
patching code that will never ship provides no value. Full raw findings archived at
`C:\Users\MattE\AppData\Local\Temp\claude\C--Dev-SpecScribe\184dd7f2-c34b-44da-81dd-d206f9fe65f0\scratchpad\22-6-findings.md`.

**Two defects were independently reproduced/confirmed by 4+ separate reviewer runs each** (Group 2+7+9 for the
first; Group 3+8, 3 reviewers apiece, for the second) — treat both as CONFIRMED, not merely plausible.

#### Decisions needed — RESOLVED by owner 2026-07-30

- [x] [Review][Decision] NDJSON `DeltaFrame` field-set vs. AC #3's literal wording — **owner decision: amend AC
  #3's wording to match the shipped contract** (frame carries `siteTitle`/`entry`/roots unconditionally; no code
  change). See Patch list below.
- [x] [Review][Decision] AC #4 fingerprint-move reframing — **owner decision: ratify the reframing.** AC #4 means
  page bytes are unchanged, not the whole output tree; amend AC #4's wording accordingly (no code change). See
  Patch list below.
- [x] [Review][Decision] Site-level-metadata-only changes producing an empty delta — **owner decision:
  conservative degrade-to-full.** If site-level metadata (`siteTitle`/nav) differs from the previous manifest,
  `BuildDelta` should emit `full: true` rather than an empty delta. No `DeltaDocument` contract change. See
  Patch list below.

#### Patches

- [x] [Review][Patch] `BuildDelta` diffs pages by `ContentHash` only, never `Chunk` — a page whose chunk
  membership shifts (batch-boundary resequencing when a sibling page is added/removed in the same top-level
  group, crossing `MaxPagesPerChunk`=75/`MaxChunkBytes`=2MB) without its content changing is silently omitted
  from `changed` and from `chunks`; a polling consumer keeps a stale chunk pointer for that page. **CONFIRMED
  independently by 4 reviewer runs** (2 with live reproduction), and the oracle test (Task 7) and all 16
  `SpaDeliveryTests.cs` fixtures are too small to ever reach the batch-boundary scale where this fires — fix
  must include a regression test at >75-page/2MB scale, not just the code fix. [src/SpecScribe/SpaDelivery.cs:438]
- [x] [Review][Patch] `RegenerateTopology` sets `_nextEmitIsFullDelta = true` *before* acquiring `_gate` (the
  lock is only taken inside the subsequent `GenerateAll()` call) — a concurrent ordinary-file pass on another
  debounce thread can win the lock first and consume/clear the flag meant for the topology thread, so the
  topology rebuild's own delta is wrongly marked `full: false` (a false "unchanged" for a whole-site rebuild —
  AC #7's named worst failure). **CONFIRMED independently by 6 reviewer runs across two review groups** — this
  reproduces the exact race class the story's own Dev Agent Record claims Task 8's live verification already
  found and fixed for the trigger *label*; the bug moved, it wasn't fixed. Also add `try`/`finally` around the
  flag write (a mid-rebuild exception currently leaves it stuck `true`), and add a genuine two-thread
  concurrency regression test that actually drives `RegenerateTopology` racing an ordinary pass — the existing
  "Trap 1" test (`ConcurrentDebouncedPasses_LeaveTheDeltaSidecarCoherent`) never triggers a topology pass at
  all, so it cannot and did not catch this. [src/SpecScribe/SiteGenerator.cs:1497]
- [x] [Review][Patch] `PersistentRenderer.onStdoutChunk`'s two new delta-handling teardown paths ("delta before
  any full payload", "sequence gap") call `teardown()` without first calling `killWithEscalation(this.proc)` —
  unlike the pre-existing byte-ceiling teardown path in the same method. The old process is never signalled to
  exit, keeps its file watchers running, and (since `onStdoutChunk` has no `torndown` guard and the stdout
  listener is never detached) keeps calling `applyPayload`/`dataChanged.fire()` on the shared
  `SpecScribeStore`, racing the freshly-spawned recovery renderer and corrupting `hasLiveChannel` (the exact
  datum the Quiet Stamp surfaces as truth). **CONFIRMED independently by all 3 reviewers in this group** —
  directly violates Task 5's stated invariant that a live-pushed payload and a one-shot payload are
  indistinguishable. [extension/src/extension.ts:1570]
- [x] [Review][Patch] `--serve-delta` passed without `--serve` silently no-ops (nothing in `Validate()` rejects
  it) — user believes delta streaming is active when it never starts. [src/SpecScribe/SiteSettings.cs]
- [x] [Review][Patch] `.spa-live-stamp` has no `white-space`/`overflow`/`text-overflow` guard — `min-height`
  only sets a floor, not a ceiling, so the two possible stamp strings (differing substantially in length) can
  wrap inside `.ss-webview-toolbar` (`flex-wrap:wrap`) and shift layout, contradicting AC #5's "no layout
  shift." The Dev Agent Record's live-verification only measured a wide 1280px viewport, not the narrow panel
  width this same file's own comment calls "the norm" — re-verify live at narrow width after fixing.
  [src/SpecScribe/assets/specscribe.css]
- [x] [Review][Patch] `specscribe-spa.js`'s delta poller sets text to "Live updates: unavailable" on a
  transient fetch failure but never resets `lastSequence` — if no new build lands before the next successful
  poll, the unchanged sequence number short-circuits the update and the stamp stays wedged on "unavailable"
  indefinitely, the exact dishonesty its own code comment warns against. Compounded by no in-flight/ordering
  guard (no AbortController, overlapping polls can resolve out of order and reset `lastSequence` backwards).
  [src/SpecScribe/assets/specscribe-spa.js]
- [x] [Review][Patch] `isDeltaFrame`/`applyDeltaFrame` throw uncaught inside the `stdout.on('data', ...)`
  handler on malformed input: `isDeltaFrame` dereferences `.frame` with no null/object guard (a literal `null`
  JSON line throws), and `applyDeltaFrame` has no shape validation (a non-array `removedSurfaces` throws from
  `for...of`) — most likely to occur right after a sequence-gap recovery, when data quality is already
  suspect. Also add `?? base.X` fallback to the `siteTitle`/`entry` merge, which is unguarded unlike every
  other field. [extension/src/extension.ts:152]
- [x] [Review][Patch] `RunTopologyPass` (the directory-level topology entry point — folder create/rename/
  delete, watcher buffer-overflow fallback) never calls `SetWatchTrigger` before escalating, unlike the
  file-level path — a genuine directory-level rebuild can report a stale, unrelated file path as the delta
  sidecar's `trigger` field instead of the `<directory change>` sentinel. [src/SpecScribe/FileWatcherService.cs:352]
- [x] [Review][Patch] `EmitSpaSite` has no error isolation around `EmitDelta`/`WriteSpaFileAtomic` — a
  delta-sidecar write failure (e.g. a lost `File.Move` race) unwinds past the calling route's already-computed
  successful `GenerationEvent`, so a fully successful rebuild gets reported as a generic Error because the
  optional, watch-only sidecar failed to write. [src/SpecScribe/SiteGenerator.cs]
- [x] [Review][Patch] `DeltaOracleTests`'s "directory rename" scenario (Task 7's own checklist item) is
  implemented as a directory *creation* (`Directory.CreateDirectory` + a new file), not a rename — undisclosed
  substitution, checkbox marked complete regardless. Mitigating: both `Created` and `Renamed` directory watcher
  events funnel through the same escalation sentinel, so the code path is very likely identical to a real
  rename — but add a genuine rename-scenario test (old path removed + new path added) for literal coverage.
  [tests/SpecScribe.Tests/DeltaOracleTests.cs]
- [x] [Review][Patch] (from resolved decision) Amend AC #3's wording in this story file to state that a delta
  frame carries `siteTitle`/`entry`/`configuredOutputRoot`/`sourceRoot`/`adrRoot`/`repoRoot` unconditionally
  alongside changed/removed surfaces and the outline — matching what `SerializeDeltaPayload`/`DeltaFrame`
  actually ship — rather than the current literal "ONLY changed/removed surfaces plus the outline." Doc-only,
  no production code change. [22-6-client-server-delta-channel.md AC #3]
- [x] [Review][Patch] (from resolved decision) Amend AC #4's wording to read "every rendered *page's* bytes are
  unchanged" rather than "every output byte is unchanged," reflecting that `GoldenContentFingerprint` covers the
  shared, always-embedded `specscribe.css` and is expected to move when a story adds a CSS rule even if no page
  content changes. Doc-only, no production code change. [22-6-client-server-delta-channel.md AC #4]
- [x] [Review][Patch] (from resolved decision) `BuildDelta` should degrade to `full: true` when the current
  manifest's site-level metadata (`siteTitle`, nav) differs from the previous manifest's, even if no individual
  page's `ContentHash` changed — currently this case silently emits an empty, non-full delta. Add a test
  (retitle/nav-rename with zero page-content edits → `full: true`). [src/SpecScribe/SpaDelivery.cs BuildDelta]

#### Deferred

- [x] [Review][Defer] `EmitDelta`'s manifest lookup (`dataFiles.First(f => ...ManifestPath)`) has no fallback —
  throws `InvalidOperationException` if ever absent; guarded by the watch loop's catch-all today, but the
  public `GenerateOne`/`RegenerateEpics`/etc. routes are reachable directly with no equivalent guard.
  [src/SpecScribe/SiteGenerator.cs] — deferred, low likelihood, pre-existing pattern elsewhere in this file
- [x] [Review][Defer] `_watchTrigger` is never cleared after `EmitDelta` consumes it — a pass reached without an
  immediately preceding `SetWatchTrigger` call reports the previous pass's label; diagnostic-field honesty
  only, doesn't affect changed/added/removed correctness. [src/SpecScribe/SiteGenerator.cs] — deferred,
  cosmetic/diagnostic impact only
- [x] [Review][Defer] ADR 0028 documentation gaps: no link to its own evidence
  (`22-6-delta-measurement-report.md`), the NDJSON delta-frame shape only partially documented
  (`removedSurfaces`/`document`/`outline` unmentioned), no cross-reference to ADR 0024 (the surface model the
  mechanism is built on), `docs/adrs/README.md`'s ADR 0008 summary bullet left stale, no AD-8 back-link in
  ARCHITECTURE-SPINE.md (sibling ADR 0027 got one for AD-5), no numbering-guess note like sibling ADR 0027's.
  [docs/adrs/0028-delta-transport-is-a-sidecar-and-a-stream-never-a-server.md] — deferred, documentation-only,
  no correctness impact
- [x] [Review][Defer] `TryReadPageIndex` discards the entire page index over one malformed page entry rather
  than just that entry, silently forcing permanent full-refresh mode until the one bad entry is fixed with no
  visible signal why. [src/SpecScribe/SpaDelivery.cs] — deferred, defensive-programming hardening, not a
  currently-reachable production path
- [x] [Review][Defer] Six distinct "degrade to full" causes (no previous manifest, forceFull, schema mismatch,
  unparseable current/previous, malformed page entry) collapse into one wire boolean (`full`) with no reason
  code — undercuts the doc comment's own "degrade LOUDLY" framing for an actual consumer, not just the source
  reader. [src/SpecScribe/SpaDelivery.cs] — deferred, observability nicety

#### Patches applied — verification

All 13 patches (10 original + 3 folded in from resolved decisions) applied 2026-07-30. Verification:

- Full suite: **2834 passed, 0 failed, 3 skipped** (pre-existing symlink-privilege skips), across two full runs.
  Two rotating flakes hit once each (`SiteGeneratorCodeCitationTests.Output_IsDeterministicAcrossRuns` — a
  minute-boundary timestamp comparison, and `BurstOfSaves_CoalescesAndLeavesCoherentOutput` — the second of this
  story's own two documented pre-existing flakes); both confirmed green in isolation immediately after, per this
  story's own Testing Standards note.
- New/extended tests added alongside the fixes: 3 in `SpaDeliveryTests.cs` (chunk-reassignment,
  siteTitle-change, nav-change degrade-to-full), 2 in `FileWatcherServiceTests.cs` (genuine 2-thread topology
  race, RunTopologyPass trigger-label), 1 in `SiteGeneratorSpaTests.cs` (delta-sidecar write failure isolation),
  1 in `DeltaOracleTests.cs` (real directory rename via `Directory.Move`), 2 in `SettingsResolverTests.cs`
  (`--serve-delta` validation) — 9 new tests, all passing.
- `GoldenContentFingerprint` moved `ad661dca…` → `dbfa172b…` (the Quiet Stamp CSS overflow-guard patch touches
  the shared, always-embedded `specscribe.css`) — causality isolated, confirmed stable across two
  `dotnet build --no-incremental` runs, provenance recorded at the constant per CLAUDE.md.
- Live-browser verification (file:// snapshot of `SpecScribeOutput/app.html`, 340px viewport): the Quiet Stamp's
  `getBoundingClientRect()` height was measured pixel-identical (22.171875px) before and after swapping in the
  longer of its two possible strings, with `white-space: nowrap; overflow: hidden; text-overflow: ellipsis`
  confirmed applied and `scrollWidth === clientWidth` (text clipped, not wrapped) — the "no layout shift" claim
  now holds at the narrow width the codebase itself calls typical, not just the 1280px width the original
  implementation checked.
- TypeScript (`extension/`): `npm run typecheck` clean, 0 errors.
- AC #3 and AC #4 wording amended in this file per the owner's 2026-07-30 decisions (see the resolved Decisions
  section above).
- [x] [Review][Defer] `sequence` has no cross-process guard — two SpecScribe processes (e.g. `watch` +
  `webview --serve --serve-delta`) targeting the same output root would each run an independent, unsynchronized
  `_deltaSequence` while atomically overwriting the same `spa/delta.json`. [src/SpecScribe/SiteGenerator.cs] —
  deferred, unlikely operating mode, not exercised by any current caller

**Dismissed as noise (19):** all `spike/delta-transport/` findings (the harness is quarantined and explicitly
deleted with the story per its own README — patching throwaway code has no future value), plus minor
test-harness-only style nits (a helper throwing `XunitException` directly instead of `Assert.Fail`, a `Dispose()`
that swallows cleanup exceptions silently, a concurrency test not marking threads background, a `WaitForQuiet`
helper's zero-events edge case) that don't affect the shipped artifact.
