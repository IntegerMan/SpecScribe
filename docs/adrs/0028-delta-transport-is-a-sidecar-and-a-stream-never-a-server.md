# ADR 0028: Delta Transport Is a Sidecar and a Stream, Never a Server

**Status:** Accepted
**Date:** 2026-07-29
**Deciders:** Matthew-Hope Eland (owner), implementing agent (Story 22.6)
**Amends:** [ADR 0008](0008-json-ir-canonical-and-incremental-generation.md) §Decision 3 (operationalizes its "emits
IR deltas suitable for a future watch / client-server transport" clause) and §Consequences (resolves the deferred
long-lived-process question — as a **no**)
**Related:** [ADR 0016](0016-ir-carries-rendered-prose-html.md) (the IR this diffs), [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)
(no new shipped runtime), [ADR 0027](0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md) (the
escalation this degrades on)

## Context

[ADR 0008](0008-json-ir-canonical-and-incremental-generation.md) §Decision 3 said the core "emits **IR deltas**
suitable for a future watch / client-server transport (conformant with AD-8)". It deliberately did not say what
that transport is, and §Consequences deferred the question: *"a future client/server mode adds a long-lived-process
deployment shape (explicitly later; not decided here)"*. Nothing since decided it.

Meanwhile the cost of not deciding was live and measurable. `WebviewCommand.RunServeLoop` has streamed a **full**
webview payload on every debounced regen since Story 6.4, and the extension's own guard comment records the size:
a **~8 MB whole-site payload**. A one-character edit to one story file re-shipped all of it. On the SPA side,
`EmitSpaSite` rewrote the manifest, every chunk, the client script and the entry shell from six call sites.

[Story 22.2](../../_bmad-output/implementation-artifacts/22-2-canonical-ir-schema-and-versioning.md) removed the
blocker by putting a per-page `contentHash` and `bytes` in the manifest — page-level addressing. Diffing manifest N
against manifest N−1 is then sufficient to compute a delta, with no incremental engine underneath.

`epics.md`'s original Story 22.6 AC #1 said *"a watch server pushes deltas"*, which read literally implies a new
long-lived **network listener**. That is the scope this ADR rejects.

## Decision

### 1. The delta channel is exactly AD-8's two clauses, and nothing more

[ARCHITECTURE-SPINE.md § AD-8](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) already names both
halves: *"static HTML may hydrate via **URL hash plus sidecar polling**, while webview uses **extension host
push**."* Story 22.6 implements those two and adds no third.

| AD-8 clause | What ships | New network surface |
|---|---|---|
| extension host push | `specscribe webview --serve --serve-delta` streams **delta frames** on the existing NDJSON stdout channel | **none** — stdout, already shipping |
| sidecar polling | `spa/delta.json`, written beside `spa/manifest.json` on each watch-mode regen | **none** — a file on disk |

### 2. No HTTP listener, no long-lived server, and that is a decision rather than an omission

No port is opened, no listener bound, no new runtime introduced. ADR 0022's *"Node is a build toolchain and a
generate-time runtime, never a shipped toolchain"* is untouched.

**A `specscribe serve` HTTP/SSE server is out of scope and needs its own ADR.** ADR 0008 §Consequences deferred the
deployment shape; this records that Story 22.6 deliberately did not un-defer it. The two transports above cover
both consumers that exist, at zero deployment cost.

### 3. `spa/delta.json` is a versioned contract, versioned separately from the IR

The document's field names are a contract other consumers bind to. It carries **two** versions:

- `deltaSchemaVersion` (`SpaDelivery.DeltaSchemaVersion`, currently **1**) — the delta document's own shape.
- `schemaVersion` — the IR `SpaDelivery.SchemaVersion` the delta was computed **against**. A consumer holding
  state from a different IR schema must refetch, not apply.

`SpaDelivery.SchemaVersion` is **not** bumped for this feature. A new sidecar file is strictly additive: every
existing IR consumer is bit-for-bit unaffected by a file it never opens, which is precisely the case that
constant's own doc comment says not to bump for.

```jsonc
{
  "deltaSchemaVersion": 1,
  "schemaVersion": 2,
  "sequence": 7,                  // monotonic within one watch session; resets at session start
  "generatedAt": "2026-07-29T19:25:48.2160034Z",
  "trigger": "_bmad-output/planning-artifacts/epics.md",   // DIAGNOSTIC LABEL ONLY — see §5
  "full": false,                  // true ⇒ every list below is empty and the consumer must refetch
  "changed": ["epics.html"],
  "added": [],
  "removed": [],
  "chunks": ["spa/pages-epics.json"]   // the chunks carrying `changed` + `added`; a removal needs none
}
```

### 4. Watch-mode only, and written atomically

The sidecar is gated on watch/serve (`SiteGenerator.EmitDeltaSidecar`), **never on `--spa` alone**. It carries a
wall clock by nature, and a one-shot `generate` must stay byte-reproducible (NFR9) — letting `--spa` turn it on is
exactly how a timestamp reaches a CI artifact.

It is written temp-file-then-`File.Move(overwrite: true)`, into the same directory as its target so the move stays
atomic, because it is the one IR file explicitly designed to be **polled** while being written. `WriteSpaFile`'s
plain `File.WriteAllText` is unchanged for the manifest and chunks — no consumer polls those, and putting a rename
in the hot path of ~1,400 pages would cost real time for no benefit.

### 5. Degrade to full, loudly — and the degrade signal must not be a racy label

`SpaDelivery.BuildDelta` emits `"full": true` with empty lists whenever the basis is absent or untrustworthy: no
previous manifest (a session's first emit), an unparseable or structurally alien manifest on either side, a
`SchemaVersion` change between emits, or a caller-declared full rebuild. Enforced **inside** `BuildDelta` so no
caller can forget one.

**The topology escalation signal is a flag the route sets on itself, never the `trigger` string.** The first
implementation derived it from the label, and Story 22.6's live verification caught the failure: a concurrent
session's save overwrote the label between `RegenerateTopology` setting it and the emit reading it, and the sidecar
written in that same second read `"full": false` while the watch log printed `<directory change> full rebuild`.
`FileWatcherService` fires **one debounce Timer per changed path, each on its own thread-pool thread**, so the
label is racy by construction. It is a diagnostic; correctness never reads it.

### 6. The delta basis advances at the EMIT seam, not on the reported outcome

The basis is captured inside `EmitSpaSite`, so it advances if and only if the IR was actually re-emitted.

This contradicts the obvious rule ("don't advance on `Error`/`Skipped`, the generator's state is unchanged"), and
the contradiction is load-bearing. `SiteGenerator.RegenerateFromDataSource` calls `GenerateAll()` on its **first
line** — a complete rebuild including the emit — and only afterwards inspects the events to decide what to report.
An unparseable `sprint-status.yaml` therefore returns `Skipped` **having already rewritten the entire IR**
(measured: `code-map.html`'s hash moved on exactly that path). A basis gated on the outcome would refuse to advance
and the next genuine push would diff against a stale manifest — a false *unchanged*, which is strictly worse than a
false *changed*: a spurious entry costs bytes, a missing one costs correctness and is undetectable by the consumer.

The NDJSON channel keeps the opposite rule for the opposite reason: it does not re-render on `Skipped`, so its
basis genuinely still describes what the consumer holds. **Two channels, two bases, two different correct answers.**

### 7. The first NDJSON frame is full; delta frames announce themselves

`--serve` without `--serve-delta` behaves exactly as before, so an older VSIX against a newer core is unaffected.
With it, the first payload is byte-identical to `SerializePayload`'s output — a cold consumer needs no special
case — and subsequent pushes carry `"frame": "delta"`. The discriminator is on the **new** shape, never the old
one, so every payload every already-shipped extension has received still reads as full.

The partial map is named `changedSurfaces`, **not** `surfaces`. A consumer that missed the discriminator and merged
a partial `surfaces` map as the whole site would silently drop every unchanged page; with a different name the same
mistake degrades to a missing key rather than data loss.

### 8. Freshness is signalled as text — the "Quiet Stamp"

The SPA entry shell and the webview chrome carry a small line of page chrome reading its state as **words**
(`Live updates: connected · updated 15:25` / `Live updates: unavailable`), never by color and never by motion. It
is present in the initial server-rendered markup and updated in place via `textContent` only, so an update inserts
no element and shifts no layout (verified live: height and content-top shift of exactly 0 across updates).

It is emitted **only** on those two surfaces — never on a static page, which has no live channel and whose bytes
are pinned by `GoldenContentFingerprint`. It deliberately does **not** live in `PathUtil.RenderHeadOpen`, which
every static page shares.

## Consequences

**Positive**

- Operationalizes ADR 0008 §Decision 3 on both of AD-8's named halves, with **zero** new deployment surface.
- Measured benefit (Story 22.6 Task 1, this repo): a single-file content edit through `GenerateOne` produces a
  delta of **2.72 % of the full IR** and **4.09 % of the full webview payload**, stable across two runs. Story 22.1
  measured **25.3 %–39.9 %** at chunk granularity; page addressing is the difference.
- `spa/delta.json` gives any polling consumer — including ones this repo does not ship — a stable contract, without
  committing SpecScribe to serving anything.
- Cheap to reverse: deleting the sidecar and the flag returns the system to full-payload pushes. No data migration,
  no schema bump to unwind.

**Negative / accepted**

- **A floor exists and the transport cannot lower it.** `code-map.html` carries a whole-repo lines-of-code total
  and therefore changes on *every* content edit — ~807 KB encoded, the dominant term in every delta measured. No
  content edit will produce a delta below ~1.2 % of the IR until that page changes, and that is a page-design
  problem, not a transport one.
- **The delta inherits `_spaCapture`'s documented watch-mode drift class.** A stale capture yields a stale
  `contentHash`, which yields a false *unchanged*. `BuildDelta` cannot close that gap — it never sees content, only
  hashes — so it only refuses to widen it. Stated in code, per Story 22.6 AC #7.
- **The `trigger` label can name the wrong file** when two files are saved in the same debounce window. Accepted:
  it is a diagnostic, and §5 removes every correctness dependency on it. Making it exact would mean threading a
  trigger argument through five public route signatures.
- **One additive stylesheet rule moved `GoldenContentFingerprint`.** Every page's markup is unchanged; the
  `.spa-live-stamp` rule is in `specscribe.css`, which the fingerprint embeds. Isolated and recorded at the
  constant.

**Neutral**

- `web/` (Nuxt) is **not** a consumer. It reads the IR at prerender time and there is no long-lived process to push
  to; per ADR 0022 that stays true. If Epic 23 ever wants live updates it re-runs generation.

## Alternatives considered

**A long-lived `specscribe serve` HTTP/SSE server.** The literal reading of `epics.md`'s original AC #1. Rejected
by owner decision (2026-07-28): it introduces a deployment shape ADR 0008 explicitly deferred, for no consumer that
exists — the webview already has a stdout channel and static HTML already has a filesystem.

**Chunk-granular deltas.** What Story 22.1 measured. Rejected on its own numbers: a one-line edit re-shipped
25.3–39.9 % of a 48 MB IR, because a chunk carries up to 75 pages.

**A second hash function for webview surfaces.** Rejected — `SpaDelivery.ContentHash` is `public static` and its
doc comment already names 22.5/22.6 as its consumers. The webview reuses it over title + source path + content.

**Bumping `SpaDelivery.SchemaVersion`.** Rejected — a new file is additive, and that constant's doc comment says
not to bump for additive changes. `DeltaSchemaVersion` versions the delta independently, letting the young contract
move without dragging every IR consumer through a compatibility check.
