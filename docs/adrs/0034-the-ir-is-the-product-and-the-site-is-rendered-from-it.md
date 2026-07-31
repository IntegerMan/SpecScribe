# ADR 0034: The IR is the product; the static site is rendered from it by Node

- **Status:** Proposed
- **Date:** 2026-07-31
- **Deciders:** Owner (Matt Eland)
- **Context story:** [Story 23.6](../../_bmad-output/implementation-artifacts/23-6-retire-the-c-sharp-html-writer.md)
  (owner decisions D1 and D5), which executes it.

## Context

Since Story 6.7 the CLI's output contract has been **"a static site, with an optional IR."** `specscribe generate`
wrote one `.html` per page from C#, and `--spa` — **off by default** — additionally emitted the JSON
intermediate representation under `spa/`.

Three things have since inverted the weight of those two halves, none of them by this ADR:

- **[ADR 0008](0008-json-ir-and-ssr-projection.md) / [ADR 0016](0016-ir-carries-rendered-prose-html.md)** seated
  `spa/manifest.json` + `spa/pages-*.json` as SpecScribe's **canonical** intermediate representation. `spa/` *is*
  the IR; it stopped being a delivery variant some time ago and the flag name never caught up.
- **Story 23.4** put all 25 templaters on `PageView` and proved the IR's content region byte-equal to C#'s own
  rendered page across **1,469 pages with 0 unexpected deltas**, then migrated all 1,276 remaining pages onto Vue
  components with **0 pass-through**. Nuxt could already render the whole site from the IR.
- **[ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)** established that Node is a
  generate-time runtime and that **SpecScribe drives the prerender** by booting a prebuilt, project-independent
  Nitro artefact and issuing one request per manifest route.

That left two renderers producing the same pages, which is the duplication Epic 23 exists to end. Retiring the C#
writer, however, is not a subtraction: it changes what `specscribe generate` *is*.

⚠️ **The specific hazard that forced this to be a decision rather than a refactor.** `--spa` defaulted to
**false**. Deleting the C# writer without touching that default would have left a plain `specscribe generate`
emitting `specscribe.css`, `specscribe.js` — and no pages and no IR. An empty output root, at `errors=0`.

## Decision

**The IR is SpecScribe's product. The static site is a rendering of it, produced at generate time by Node.**

1. **The IR is emitted unconditionally.** Every `generate`, every `watch` pass, every incremental route. There is
   no configuration under which a run produces no IR, because the IR is now the only thing standing between the
   user and an empty output root.

2. **`--spa` is retired as a deprecated no-op.** It stays *registered* so an existing script or CI step does not
   fail with "unknown option", and prints a one-line deprecation notice. It is deliberately **not re-purposed**:
   silently changing a flag's meaning is worse than retiring it.

3. **No C# code path emits a content `.html`.** `HtmlRenderAdapter.Render`'s full-page composition and all five
   content-write paths are deleted. C# still **composes the region** — `RenderNavMarkup`, `RenderBreadcrumb`,
   `RenderWayfinding`, `RenderDashboardBody`, `RenderEpicsBody` survive — because
   [ADR 0024](0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) keeps the webview and the SPA
   on that seam. **"One renderer" is true of the SITE, not of the PRODUCT.**

4. **Node becomes a hard prerequisite for producing HTML**, and the consequence is accepted rather than softened:
   **a user without Node cannot generate a browsable site.** They can still obtain the IR. The failure is loud —
   an actionable error naming the supported range (`^22.19.0 || ^24.11.0 || >=26.0.0`) — never a silent empty
   output root. ADR 0022 §Decision 5 assigned Node *detection* to Story 16.3, which does not exist yet; Story 23.6
   implements the check because it is the story that makes the dependency load-bearing.

5. **C# remains the single writer of the four shared runtime assets** — `specscribe.css`, `specscribe.js`,
   `prism.js`, `plotly-hierarchy.min.js`. They are embedded resources in the C# assembly,
   `web/scripts/sync-runtime-assets.mjs` already treats C# as authoritative, and the webview and SPA paths still
   need C# to place them. The prerender copies the artefact's own assets but **skips any file that already
   exists**, so there is exactly one writer per file. Two writers of the same file is the drift this epic exists
   to end.

## Consequences

- **`specscribe generate` now requires Node.** This is the single biggest change to the product's runtime
  contract since ADR 0005. It is stated in ADR 0022 and is re-confirmed here as accepted.
- **A cold full generate costs more.** Measured on this repository at 1,492 routes: **13.5 s of prerender,
  9.1 ms/route**. ⚠️ That is **2.3× Story 23.5's measured ~4 ms/route** and the difference is stated rather than
  discovered. Watch mode re-renders only routes whose region digest moved, so a debounced save does not pay it.
- **The IR is now on the cold path**, so its emission cost is unconditional. `EmitDeltaSidecar` remains gated on
  watch/serve so a one-shot `generate` stays byte-reproducible (NFR9) — the delta carries a wall clock.
- **`GoldenContentFingerprint` is retired**, its subject having been deleted. Its successor is `npm run
  check:parity` over a **pinned** corpus. Per [ADR 0033](0033-content-drift-gates-are-targeted-and-regenerable.md)
  it was deliberately **not** re-pointed at the IR as another whole-tree hash.
- **~268 test call sites moved from the full page to the region**, and ~206 more move from reading a written
  `.html` to reading the IR. The C# unit suite deliberately does **not** boot Node: the region is the right
  subject for a C# assertion, and chrome belongs to `web/test/` and the web gates.
- **A blind spot is closed, not opened.** The previous per-page oracle hashed `<main>` only, so `<title>`, meta,
  the favicon, the footer, `<script src>` tags, the nav toggle, the Mermaid init and the Hierarchy/Graph
  anti-flash handshakes were in **no** committed digest — precisely the chrome this ADR's decision 3 deletes the
  C# emitter for. `check:parity` now hashes the whole page for a frozen corpus.
- **The C# SPA delivery form (`app.html`, `specscribe-spa.js`) still ships.** Whether it still earns its keep now
  that Nuxt renders the site is a real question and an explicit **non-goal** here. ADR 0024 currently keeps it.

## Alternatives considered

- **Delete the writer and ship IR-only output.** Rejected by the owner (decision D1): `specscribe generate` must
  still leave a browsable static portal in the output root. A tool whose output requires a second tool to be
  readable is a different product.
- **Keep the C# writer alongside Nuxt indefinitely.** Rejected: two renderers producing the same pages is the
  duplication Epic 23 exists to end, and Story 23.4's finding 4 — the same content silently dropped by three
  independent layers — is what that duplication costs in practice.
- **Make `--spa` default to true and leave everything else alone.** Rejected: it preserves a flag whose name
  describes a delivery variant that no longer exists, and leaves a supported configuration (`--spa false`) that
  produces nothing.
- **Bundle a JavaScript runtime with the standalone binary.** Rejected in ADR 0022 (+50–100 MB per RID) and kept
  only as an escape hatch if the Node prerequisite proves unacceptable.

## Ratified decisions

None yet — this ADR is **Proposed**. Ratification is the owner's.
