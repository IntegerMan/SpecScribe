# Story 23.6: Retire the C# HTML Writer

Status: ready-for-dev

<!-- create-story 2026-07-30, baseline commit 5a78ee7. Four owner decisions elicited up front (D1-D4 below).
     Story 23.4's verify-and-iterate pass is CONFIRMED FINISHED by the owner, so AC #5's ordering gate is
     satisfied and this story is ready-for-dev rather than blocked. -->

## Story

As a maintainer finishing owner decision D2,
I want `HtmlRenderAdapter.Render`'s full-page composition and every C# content-`.html` write deleted, the IR
emitted unconditionally, the prerender driven from `generate` per ADR 0022 §Decision 3, and every gate that
depended on the written document re-pointed or retired with a stated reason,
so that Nuxt is the single writer of SpecScribe's HTML and no C# code path emits a content page.

## Owner decisions locked at create-story (2026-07-30)

| # | Decision |
| --- | --- |
| **D1** | **The IR becomes unconditional AND `generate` shells out to Node to emit the `.html`.** Not "IR-only output". A user's `specscribe generate` must still leave a browsable static portal in the output root — produced by the Nitro artefact, not by C#. This is ADR 0022 §Decision 3 executed for the first time. |
| **D2** | **The replacement content-drift gate is `check:parity` over the committed `web/measurements/parity.json`.** A new npm script that *reads back* the committed per-page `goldenSha` and asserts the freshly rendered page still hashes to it, naming the page on failure, and hard-failing when the row set is empty. Wired into CI beside `npm run check`. No C#-side digest gate. |
| **D3** | **`RegionCompositionParityTests` and `RegionCompositionCorpusProof` are RETIRED**, with the reason recorded in-file, together with the `SpaDelivery.Extract*` scrapers they are the last consumer of. Their job — proving the composed producer equals the slice — is finished and the evidence is committed (1,469 pages, 0 deltas). |
| **D4** | **Story 23.4's verify-and-iterate pass over the 1,276 migrated pages is FINISHED.** AC #5's ordering gate is satisfied. Nothing further needs re-measuring against the golden side, which is what makes the deletion safe to start. |

## Acceptance Criteria

1.
**Given** owner decision D2 — C# stops WRITING `.html` while still composing regions for the IR
**When** the retirement lands
**Then** `HtmlRenderAdapter.Render`'s full-page composition is gone, all **five** content-`.html` write paths are
gone (see § The five write paths — the epic text names only two), and no C# code path writes a content `.html`,
**while** `RenderNavMarkup`, `RenderBreadcrumb`, `RenderWayfinding`, `RenderDashboardBody` and `RenderEpicsBody`
survive and continue to feed the region — and the webview and SPA keep working through that same region path per
[ADR 0024](../../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md).

2.
**Given** the written document is the oracle for **six** dependents, not the four the epic enumerates
**When** the writer is deleted
**Then** each is **re-pointed or retired with a stated reason** — never left asserting against a vanished oracle. A
gate that silently passes because its basis is empty is a failure of this AC, not a pass; § The six dependents is
the checklist, and each row must be closed explicitly in the Dev Agent Record.

3.
**Given** [ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md) and owner decision D2
**When** the replacement content-drift gate lands
**Then** `npm run check:parity` compares the freshly rendered site against the **committed** per-page hashes in
`web/measurements/parity.json` and is **targeted** (a failure names the page), **regenerable by command**
(`npm run measure:parity`, producing a reviewable per-page diff — never a constant bump), **proven deterministic on
both CI operating systems** and not merely across two local runs, and **fails loudly when its oracle is absent** —
an empty or short row set is a hard failure, not a pass. It lands **before** the deletion in the task order below,
so the drift gate never lapses.

4.
**Given** [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) makes Node a
generate-time runtime, and D1 makes it load-bearing for every run
**When** C# can no longer produce HTML at all
**Then** the documented Node prerequisite is **verified to actually fire** — a user with no Node, or Node outside
`^22.19.0 || ^24.11.0 || >=26.0.0`, gets the actionable startup error naming the supported range, **not** a silent
empty output root — and the consequence 23.5 stated plainly (**a user without Node cannot generate at all**) is
re-confirmed as the accepted trade-off. Node *detection* remains Story 16.3's; this AC verifies the failure path
exists and is reached, and adds one if it is not.

5.
**Given** the owner's verify-and-iterate pass is the design gate (CLAUDE.md § Story lifecycle)
**When** this story starts
**Then** Story 23.4's 1,276 migrated pages are confirmed verified first, because after the deletion there is **no
golden side left to generate** and re-measuring stops being possible.
**✅ SATISFIED at create-story (owner decision D4, 2026-07-30).** Record it and move on; do not re-ask.

6.
**Given** owner decision D1 and [ADR 0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md)
**When** the retirement lands
**Then** the IR is emitted **unconditionally** — `_spaCapture`/`_spaPageViews` are no longer gated on
`EmitSpa || CapturePages`, `spa/` is written on every `generate`, and `--spa` is retired as a no-op alias with a
deprecation notice rather than silently changing meaning. A run that emits no IR is impossible, because the IR is
now the only thing standing between the user and an empty output root.

7.
**Given** [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) §Decision 3
**When** `specscribe generate` completes its IR emit
**Then** it boots the prebuilt Nitro artefact with `SPECSCRIBE_IR_DIR` set to the output root, issues **one request
per route from the manifest it just emitted** (never `nuxt generate`, never a crawler), writes each response to its
output-relative `.html` path, copies the artefact's static assets alongside, and shuts the server down — leaving an
output root a browser can open exactly as today. A non-200, an empty `<main>`, or a route the manifest names but the
server does not answer is a **reported error**, not a silently missing page.

8.
**Given** ~550 C# test call sites assert on either a written `.html` file or a templater's `RenderX` full-page
projection, both of which this story deletes
**When** the deletion lands
**Then** the suite is re-pointed at the **region** (`JsonSpaRenderAdapter.RenderContent` over the page's own
`PageView`) with **no net loss of assertion coverage**: every assertion is either preserved against the region,
moved to `web/test/` when it is genuinely a chrome assertion, or deleted with a one-line reason. Deleting an
assertion because it no longer compiles is a failure of this AC.

9.
**Given** CLAUDE.md § Decision records — propose an ADR for any change to a shared architectural contract
**When** the CLI's output contract changes from "a static site, with an optional IR" to "an IR, plus a site
rendered from it by Node"
**Then** the story **proposes** an ADR recording that inversion and the retirement of `--spa`, and proposes moving
**ADR 0022** and **ADR 0033** from `Proposed` to `Accepted` — this story being the first execution of the one and
the first implementation of the other. Ratification is the owner's; the story's obligation is the proposal.

## Tasks / Subtasks

**Task order is load-bearing.** AC #3 requires the drift gate to exist *before* the deletion, and AC #7's render
path must be working *before* the writer is removed, or there is a commit range with no portal at all.

- [ ] **Task 1 — Stand up the replacement drift gate FIRST (AC: #3)**
  - [ ] Add `web/scripts/check-parity.mjs` + `"check:parity"` to `web/package.json`. It loads the committed
        `web/measurements/parity.json`, and for each row re-reads the rendered page from `.output/public`,
        normalizes with `harness-lib.mjs`'s `normalizeVolatile`, hashes with the same 16-char sha256 slice, and
        compares against the row's committed `goldenSha`.
  - [ ] **Fail loudly on a vanished oracle (ADR 0033 §5).** Assert `rows.length > 0` *and* that the measured row
        count matches the committed row count before trusting any result. Model the assertion on
        `RegionCompositionCorpusProof`'s deep-git surface check, which ADR 0033 §5 names as the reference.
  - [ ] Prove determinism on **both** CI operating systems (`build-test-analyze` Windows + `portability-probe`
        Ubuntu) before pinning — two local runs is a floor, not a proof (ADR 0033 §4; this is exactly what sank
        `GoldenIrFingerprint`).
  - [ ] Add `npm run check:parity` to `build-test-analyze.yml` beside the existing `npm run check` step, and to
        the `"check"` aggregate script.
  - [ ] Record in the story: `measure:parity` **writes** the oracle, `check:parity` **reads** it. Today nothing
        reads it back, which is why the committed `parity.json` is evidence and not yet a gate.

- [ ] **Task 2 — Make the IR unconditional (AC: #6)**
  - [ ] `SiteGenerator.cs:355` — allocate `_spaCapture`/`_spaPageViews` unconditionally. (`_spaCapture` survives
        Task 2 only as the Task 5 comparison basis; Task 6 removes it.)
  - [ ] `EmitSpaSite` runs on every `GenerateAll`, not behind `_options.EmitSpa`.
  - [ ] `SiteSettings.Spa` (`--spa`) becomes a deprecated no-op that prints a one-line notice; keep the option
        registered so an existing script does not fail with "unknown option". Update its `[Description]`.
  - [ ] `ForgeOptions.EmitSpa` — keep the property, default it `true`, and document that the false path is gone.
  - [ ] **`EmitDeltaSidecar` stays gated on watch/serve.** A one-shot `generate` must stay byte-reproducible
        (NFR9); do not let the IR-always change drag `spa/delta.json` onto the cold path
        ([SiteGenerator.cs:4225](../../src/SpecScribe/SiteGenerator.cs:4225)).

- [ ] **Task 3 — Drive the prerender from `generate` (AC: #7, #4)**
  - [ ] Resolve the artefact directory, in order: `SPECSCRIBE_RENDERER_DIR` env override → a `renderer/`
        directory beside the executing assembly (the Epic 16 packaging shape) → `web/.output/` relative to the
        repo root (the developer path). A miss is an actionable error naming all three, never a silent skip.
  - [ ] Boot `node <artefact>/server/index.mjs` with `SPECSCRIBE_IR_DIR` = the output root and a port the OS
        assigns; wait for readiness by polling one known route rather than sleeping; on exit, kill the child in a
        `finally` so a failed generate cannot leak a server. **Do not** reuse `web/scripts/experiment-two-ir.mjs`
        as the shipping implementation — it is a measurement harness — but **do** read it: it already solved
        readiness, port handling, per-route driving and process isolation, and its comments record the traps.
  - [ ] Request one route per manifest entry, write the body to `<OutputRoot>/<path>`. Report a non-200, a missing
        `<main id="main-content">`, or an unanswered manifest route as a `GenerationOutcome.Error` — the
        diagnostics page is the surface for it.
  - [ ] Copy the artefact's static assets into the output root. **Decide and record** whether `specscribe.css`,
        `specscribe.js`, `prism.js` and `plotly-hierarchy.min.js` continue to come from C#'s `CopyEmbeddedAsset`
        or from the artefact's own synced copies — they exist in both places today
        (`web/scripts/sync-runtime-assets.mjs` copies C# → `web/public/`). Two writers of the same file is the
        drift this epic exists to end; pick one and say which.
  - [ ] **Node prerequisite (AC #4).** Verify the ADR 0022 §Decision 5 startup check actually fires. Test it three
        ways: Node absent from `PATH`, Node present but below the supported range, and the artefact directory
        missing. Each must produce an actionable error naming the supported range
        (`^22.19.0 || ^24.11.0 || >=26.0.0`) — never an empty output root with `errors=0`.
  - [ ] **Watch mode.** A full 1,469-route crawl per debounced save is not viable. Use the routes Story 22.5's
        classifier and Story 22.6's delta already name, and re-render only those. Record the measured
        per-save cost; if the scope classifier escalates to topology, a full crawl is correct there.
  - [ ] Record the measured cost of a cold full generate (23.5 measured the crawl at ~4 ms/route, 6.3 s for 1,056
        routes) against today's C#-only baseline, so the regression is stated rather than discovered.

- [ ] **Task 4 — Re-point the test suite off the written document (AC: #8)**
  - [ ] Add one shared test helper that returns a page's **region** given a completed generator and an
        output-relative path, reading the emitted `spa/` chunks (or exposing `CapturedRegions` to the test
        assembly). This is the substitute for the ~261 `File.ReadAllText(Path.Combine(Site, "…​.html"))` reads.
  - [ ] Mechanically substitute the ~300 templater call sites:
        `XTemplater.RenderPage(args)` → `JsonSpaRenderAdapter.Shared.RenderContent(XTemplater.BuildPage(args))`.
        Story 23.4 already added every `BuildX` returning a `PageView`, so this is a substitution, not a rewrite.
  - [ ] Triage the residue — the assertions that genuinely live in chrome, not in the region: `<title>`,
        `<meta name="description">`, the favicon data-URI, the footer, `<script src>` tags, the nav toggle script,
        the Mermaid init, the Hierarchy/Graph boot markers. Each moves to a `PageView`-level assertion
        (`page.Title`, `page.MetaDescription`, `page.Assets.*`) or to `web/test/`, or is deleted with a reason.
  - [ ] Do **not** keep a test-only full-page composer as a shortcut. It recreates the deleted writer, and a
        chrome regression would then pass a green suite while the shipped page was wrong. AC #1 forecloses it.

- [ ] **Task 5 — Close the six dependents (AC: #2)**
  - [ ] Work § The six dependents row by row. Each row gets an explicit disposition — re-pointed (say to what) or
        retired (say why) — recorded in the Dev Agent Record. A row left implicit fails this AC.
  - [ ] `GoldenContentFingerprint`: **retire.** Its subject is gone. Do not re-point it at the IR as another
        whole-tree hash — ADR 0033 forbids exactly that, by name, for this story.
  - [ ] `GoldenOutputInventory`: the file set changes wholesale (it gains `spa/**` and loses nothing only if
        Task 3's crawl writes the same `.html` names). Decide whether it survives as a re-pinned inventory or is
        superseded by `check:parity`'s row-count assertion, and say which.
  - [ ] `EnsureHierarchyEngine`'s host-marker scan: re-derive from the view model. `page.Assets.HierarchyEngineNeeded`
        / `GraphEngineNeeded` already carry the answer structurally — but read
        [SiteGenerator.cs:4370](../../src/SpecScribe/SiteGenerator.cs:4370) first: the existing comment explains
        why the flag alone was **not** sufficient (a watch-mode topology rebuild wipes the output root and deletes
        an asset this instance believes it copied). Preserve that disk-is-the-truth behaviour.

- [ ] **Task 6 — The deletion (AC: #1)**
  - [ ] Delete `HtmlRenderAdapter.Render` and the ~25 templater `RenderX` full-page wrappers it backs. Keep every
        `BuildX`.
  - [ ] Delete all five content-`.html` write paths (§ The five write paths).
  - [ ] Delete `_spaCapture` and everything that reads it — including the two call sites that would otherwise go
        **silently vacuous** rather than red (§ The six dependents, rows 5 and 6).
  - [ ] Delete `SpaDelivery.ExtractContentRegion`, `ExtractTitle`, `ExtractBreadcrumb`, `ExtractMetaDescription`,
        `ExtractNavMarkup` and `CapturedNavMarkup`. **Keep** `ExtractScriptIslands`, `ContentHash`,
        `BuildDataFiles`, `BuildDelta`, `BuildEntryShell`, `MainLandmark` — those operate on the region, not on a
        rendered document, and the webview CSP filter and delta channel depend on them.
  - [ ] Retire `RegionCompositionParityTests` and `RegionCompositionCorpusProof` (owner decision D3), leaving a
        block comment in each file recording what they proved, the numbers (1,469 pages, 0 unexpected deltas), and
        where the evidence lives — the same discipline used when `GoldenIrFingerprint` was removed.
  - [ ] Grep-verify after each deletion that the symbol is actually gone and nothing references it. CLAUDE.md
        § Concurrent work: a write that returned success is not a write that landed.

- [ ] **Task 7 — Decision records (AC: #9)**
  - [ ] Author the new ADR (next free number is **0034**): the CLI's output contract inverts — the IR is the
        unconditional product and the static site is rendered from it by Node; `--spa` is retired. Status
        `Proposed`.
  - [ ] Propose **ADR 0022 → Accepted** (this story is its §Decision 3 executed) and **ADR 0033 → Accepted** (this
        story is its first implementation). Update `docs/adrs/README.md`.
  - [ ] Clear `deferred-work.md:22`'s standing action — it asks for exactly Task 1's gate — and say in the entry
        which of its two offered options was taken.

- [ ] **Task 8 — Live-browser verification (CLAUDE.md § Verification)**
  - [ ] Generate to `SpecScribeOutput/` and open the result in a real browser at depth 0 and depth 3. The suite
        structurally cannot see CSS containment leaks, sub-pixel collapse, or DOM corruption from markup splicing;
        all three have shipped in this epic and were caught only by looking.
  - [ ] Confirm asset URLs resolve from `file://` — ADR 0022 §Decision 6's page-relative rewrite is the mechanism,
        and this is the first time C# is not the one emitting those paths.
  - [ ] Confirm the Hierarchy Explorer and the Story 24.2 relationship graph still boot: their anti-flash
        handshakes were emitted by `HtmlRenderAdapter.Render` at chrome level
        ([HtmlRenderAdapter.cs:37-46](../../src/SpecScribe/HtmlRenderAdapter.cs:37)) and must now come from the
        Nuxt head projection.

## Dev Notes

### What Story 23.4 already did — do not redo any of it

All 25 templaters are on `PageView`. The IR is built from a region **composed** from each page's own view model at
the `WritePage` seam, proven byte-equal to the old `ExtractContentRegion` slice across **1,469 pages with 0
unexpected deltas**. All 1,276 remaining pages are migrated to 10 Vue family components with **0 pass-through**. The
parity oracle is captured and committed as per-page sha256 in `web/measurements/parity.json`. **The deletion is
what remains** — plus the three things D1/D2 added to it.

### The good news, and why the circularity is already broken

`WritePage` ([SiteGenerator.cs:3970](../../src/SpecScribe/SiteGenerator.cs:3970)) renders the document via
`HtmlRenderAdapter.Shared.Render(page)` and composes the region **separately** from the same `PageView`.
`CapturedRegions` ([:3625](../../src/SpecScribe/SiteGenerator.cs:3625)) iterates `_spaPageViews` — the *composed*
producer — and reads nothing from the rendered document. Verified in the tree at baseline 5a78ee7. The 23.4-era
fear ("delete the writer and the IR goes dark for 82 % of the site") is already resolved.

### ⚠️ The five write paths — the epic text names two

`WriteOutput` is not the only content writer. Grep before you trust a count:

| write path | what it emits |
| --- | --- |
| `WriteOutput` ([:3939](../../src/SpecScribe/SiteGenerator.cs:3939)) | every page routed through `WritePage` (38 call sites), plus Story 18.4's verbatim `forge-report.html` at [:4798](../../src/SpecScribe/SiteGenerator.cs:4798) with `capture: false` |
| `File.WriteAllText` [:3234](../../src/SpecScribe/SiteGenerator.cs:3234) | `epics.html` |
| `File.WriteAllText` [:3249](../../src/SpecScribe/SiteGenerator.cs:3249) | `epics/epic-{N}.html` |
| `File.WriteAllText` [:3261](../../src/SpecScribe/SiteGenerator.cs:3261) | undrafted-story placeholder pages |
| `File.WriteAllText` [:3268](../../src/SpecScribe/SiteGenerator.cs:3268) | `epics/story-{id}.html` |
| `WriteTextWithRetry` [:4341](../../src/SpecScribe/SiteGenerator.cs:4341) | `index.html` |

The last five are the dashboard/epics families, which `BuildSpaBundle` deliberately re-renders from their view
models rather than capturing — so their IR path is *already* independent of these writes. They are pure deletions.

**Not content pages, and not in scope:** `WriteSpaFile` / `WriteSpaFileAtomic` write the IR itself (`spa/*.json`,
`spa/delta.json`), `specscribe-spa.js`, and `app.html`. `app.html` inlines the dashboard region for first paint and
is SPA *delivery*, not a generated content page. Whether the C# SPA form still earns its keep now that Nuxt renders
the site is a real question and an explicit **non-goal** here — if you conclude it does not, raise it as a
deferred-work entry rather than deleting it inside this story. ADR 0024 currently keeps it.

### ⚠️ The six dependents — two of them fail SILENTLY, and two are not in the epic's table

| # | dies with the writer | failure mode | what this story owes it |
| --- | --- | --- | --- |
| 1 | `_spaCapture` → `RegionCompositionDeltas()` ([:4039](../../src/SpecScribe/SiteGenerator.cs:4039)) | loses its comparison basis ⇒ `RegionCompositionParityTests` **and** `RegionCompositionCorpusProof` go **vacuous, not red** | **Retire both** (owner D3), reason recorded in-file |
| 2 | `GoldenContentFingerprint` ([SiteGeneratorAdapterTests.cs:236](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:236)) | subject gone | **Retire.** Do **not** re-point at the IR as another whole-tree hash — ADR 0033 names this story and forbids exactly that |
| 3 | `GoldenOutputInventory` ([:161](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:161)) | pins the output file set; changes wholesale | Re-pin or supersede — say which |
| 4 | `EnsureHierarchyEngine`'s host-marker scan ([:4367](../../src/SpecScribe/SiteGenerator.cs:4367)) | reads `WritePage`'s returned document | Re-derive from `page.Assets.*`, preserving the disk-is-the-truth guard |
| 5 | **`CapturedRegions`' silent-gap guard** ([:3634](../../src/SpecScribe/SiteGenerator.cs:3634)) | `if (_spaCapture is { } capture && capture.Count != views.Count)` — the check that a page is not still on the un-migrated write path. With `_spaCapture` gone the condition is never entered: **vacuous, not red** | Retire it explicitly (the un-migrated path it guards against is being deleted) or re-express it against the page inventory |
| 6 | **`RenderWebviewSurfaces`' long-tail gate** ([:3720](../../src/SpecScribe/SiteGenerator.cs:3720)) | `if (_spaCapture is not null)` gates the *entire* doc/ADR/requirement/sprint/retro surface set — even though the body inside consumes the **composed** producer. Delete `_spaCapture` naively and the webview silently loses every long-tail surface with no test failing | Re-point the condition at `_spaPageViews`. Also fix the sibling throw at [:3715](../../src/SpecScribe/SiteGenerator.cs:3715) |

Rows 5 and 6 are not in the epic's table. Row 6 is the one most likely to ship broken: it is a one-word condition
whose failure mode is a silently smaller webview, and Story 23.4's own finding 4 was the same shape — *the same
content dropped by three independent layers, caught only by a browser*.

### ⚠️ `measure:parity` goes vacuous too — this is why AC #3 exists

`web/scripts/measure-parity.mjs` sets `goldenRoot = ir.IR_DIR`, which resolves to `SpecScribeOutput/` — the
directory C# writes the `.html` into. After the deletion, `readOrNull(join(goldenRoot, path))` returns `null` for
every page, every row takes the `NO GOLDEN` branch, `measured` is empty, `migrationDeltas` is empty, and the script
exits **0**. The harness that produced this story's oracle reports success while measuring nothing.

`check:parity` (Task 1) is the fix, and it is a different script on purpose: `measure:parity` **writes** the oracle
from a live golden-vs-IR-vs-Nuxt comparison and stops being runnable after this story; `check:parity` **reads** the
committed oracle and keeps working forever. `web/measurements/parity.json` today holds 1,469 rows, all `status: ok`,
`goldenVsNuxt` true on every one — that is the baseline `check:parity` must reproduce.

`check:links` has the same one-sidedness: it walks `ir.IR_DIR` for the golden side
([check-links.mjs:44](../../web/scripts/check-links.mjs:44)). After the deletion it measures the Nuxt side alone.
That is acceptable — a one-sided link check is still a link check — but say so in the run rather than letting the
"golden" column read as a passing comparison.

### The prerender transport is already specified — don't design it fresh

ADR 0022 §Decision 3 is explicit: *"SpecScribe drives the prerender. At generate time the CLI boots the artefact,
sets `SPECSCRIBE_IR_DIR`, and issues one request per route from the manifest it just emitted. It does not invoke
`nuxt generate`, and the artefact does not crawl."* Story 23.5 proved it end to end — 1,056/1,056 routes of this
repo and 32/33 of a *different* project, from one 3.78 MB artefact, ~4 ms/route.

Read [`web/scripts/experiment-two-ir.mjs`](../../web/scripts/experiment-two-ir.mjs) before writing the C# driver.
It records the traps in its own header comments:

- **The artefact must be a `build:package` build.** A `npm run build` artefact carries project A's prerendered
  pages in `public/`, and **Nitro serves `public/` ahead of the SSR route** — pointed at project B it returned A's
  dashboard with HTTP 200. A wrong answer with a success status. `SPECSCRIBE_PACKAGE_BUILD=1` empties the route
  table structurally.
- **`IR_DIR` resolves at module scope**, so one process sees exactly one IR. Fine here (one generate, one project),
  but it means the server cannot be kept warm across projects.
- **Never substring-probe rendered HTML.** This portal renders its own source and its own docs, so
  `_payload.json`, `window.__NUXT__`, `data-hierarchy` and `<main>` all appear as *prose* on real pages. Match
  structure. Three separate stories have been bitten by this.
- Routing is `web/pages/[...path].vue`, a catch-all — `/epics/epic-1.html` resolves at server runtime with no
  route-table entry needed.

### ⚠️ The test blast radius is the largest single piece of this story

Measured at baseline 5a78ee7:

- **~261 call sites** across **35 test files** read a generated page via `Path.Combine(Site, …)`. Heaviest:
  `SiteGeneratorHowToReadTests` (36), `SiteGeneratorSpaTests` (27), `FollowUpSurfacesTests` (27),
  `SiteGeneratorAdrToleranceTests` (24), `SiteGeneratorAdapterTests` (18).
- **~300 call sites** across **22 test files** call a templater's full-page `RenderX` — all of which are
  `HtmlRenderAdapter.Shared.Render(BuildX(...)).Content` after 23.4. Heaviest: `CodeFileTemplater.RenderPage` (56),
  `HtmlTemplater.RenderIndex` (35), `SprintTemplater.RenderBoard` (25), `CodeMapTemplater.RenderPage` (20).

Both classes are **mechanically substitutable** because 23.4 already split every templater into
`BuildX → PageView` + a thin HTML projection. Most assertions are `Assert.Contains` over body content, which lives
in the region unchanged. Budget the time for the residue, not the substitution.

**Do not make the C# unit suite depend on Node or on a built artefact.** A ~65 s deep-git generate plus a Nitro boot
in the unit suite is not a trade this project should make; the region is the right subject for a C# assertion, and
chrome belongs to `web/test/`.

**This is the natural carve-out point if the story proves too large.** Tasks 1–3 (gate, unconditional IR, prerender
driver) are additive and independently valuable; Tasks 4–6 (test re-point, dependent closure, deletion) are the
irreversible half. If you reach that judgment mid-story, raise it rather than silently descoping — this story was
itself carved out of 23.4 for exactly this reason.

### Regression hazards specific to this deletion

- **`GenerateAll` wipes the output root** ([:343](../../src/SpecScribe/SiteGenerator.cs:343)) before writing. The
  prerender crawl runs *after* the IR emit, so the wipe is not a hazard for the rendered pages — but a crawl that
  fails partway leaves a half-written portal with `errors > 0`. That is the honest state; do not "repair" it by
  suppressing the error.
- **`EnsureHierarchyEngine` is reached from `WriteIndex`**, which every incremental watch path calls. Its dedupe by
  path exists because a persistently failing copy appended one diagnostics record per debounced save. Preserve it.
- **Chrome-level scripts.** `HtmlRenderAdapter.Render` is where `HierarchyExplorer.BootScript`,
  `RelationshipGraph.BootScript`, `Toc.ActiveSectionScript`, the Mermaid init and the
  `plotly-hierarchy.min.js` `<script src>` were appended — deliberately *outside* `page.BodyHtml` so the webview and
  SPA never carry them. Deleting `Render` deletes the only C# emitter of all five. Confirm the Nuxt head projection
  emits each, in the same order, gated on the same `page.Assets.*` flags.
- **`RenderParity.cs`** distills a rendered page into semantic facts and `RenderParityTests` calls
  `RenderParity.Extract(HtmlRenderAdapter.Shared.Render(page).Content, page)`. It loses its subject with `Render`.
  Re-point at the region or retire — it is not in the epic's table but it will not compile.

### Analysis observations

`.specscribe/analysis/files/src/SpecScribe/SiteGenerator.cs.json` carries **91 open observations** (19 error, 42
warning, 30 note). ⚠️ **The digest is STALE** — `provenance.evaluatedAtRevision` is `bc7a379`, HEAD is `5a78ee7`,
`workingTreeDirty: true`. Per CLAUDE.md, treat every cited line as approximate and confirm by symbol; re-run
`node tools/analysis-digest/index.mjs` before relying on it. A large fraction of this file is being deleted by this
story, so expect the count to drop rather than treating each observation as work.

### Testing standards

- xUnit, temp-dir fixtures in the `SiteGeneratorAdapterTests` style (`Directory.CreateTempSubdirectory`, `IDisposable`).
- `web/` is Vitest; `npm run test:coverage` emits lcov consumed by Sonar. Component tests are the known coverage gap
  (`web/**/*.vue` is excluded from the coverage denominator) — a chrome assertion moved out of C# and into a Vue
  component test is a *net win* against that gap, not new debt.
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.
- The fixture in `SiteGeneratorAdapterTests` cites no real repo files, so it emits no `code/` or `commit/` page —
  ~40 % of the real site. Fixture-green has never been sufficient in this epic and is not sufficient here.

### Project Structure Notes

- New file: `web/scripts/check-parity.mjs`. Zero npm dependencies, Node built-ins only — ADR 0010's posture, which
  `harness-lib.mjs`, `tokens-lib.mjs` and `measure-payload.mjs` all hold.
- New file: `docs/adrs/0034-*.md` (next free number confirmed — 0033 is the highest today).
- The C# prerender driver belongs beside the other generation-pipeline concerns. `SiteGenerator.cs` is already ~5,900
  lines and carries 91 analysis observations; prefer a new `NuxtPrerender.cs` (or similar) with `SiteGenerator`
  calling it, over growing that file further.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Story 23.6](../planning-artifacts/epics.md) — the five epic ACs, the four-gate table, the blocker note
- [Source: `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md`](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md) — §Decision 1–5 are AC #3's checklist; §Consequences names this story as inheriting the hole
- [Source: `docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md`](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) — §Decision 2 (`build:package`), §Decision 3 (the prerender transport), §Decision 5 (the Node prerequisite), §Decision 6 (page-relative assets)
- [Source: `docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md`](../../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) — the contract AC #1 must not break
- [Source: `docs/adrs/0016-ir-carries-rendered-prose-html.md`](../../docs/adrs/0016-ir-carries-rendered-prose-html.md) — `spa/` IS the IR
- [Source: `_bmad-output/implementation-artifacts/23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md`](23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md) — what is already done; its File List is the `BuildX` inventory Task 4 depends on
- [Source: `_bmad-output/implementation-artifacts/23-5-packaging-strategy-report.md`](23-5-packaging-strategy-report.md) — the measured basis for Task 3
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:22`](deferred-work.md) — the standing action Task 7 clears
- [Source: `CLAUDE.md`](../../CLAUDE.md) — § Concurrent work (verify after every edit; never `git reset --hard`), § Verification (live browser), § Decision records
- Code: [`SiteGenerator.cs`](../../src/SpecScribe/SiteGenerator.cs), [`HtmlRenderAdapter.cs`](../../src/SpecScribe/HtmlRenderAdapter.cs), [`SpaDelivery.cs`](../../src/SpecScribe/SpaDelivery.cs), [`RenderParity.cs`](../../src/SpecScribe/RenderParity.cs), [`SiteSettings.cs`](../../src/SpecScribe/SiteSettings.cs), [`ForgeOptions.cs`](../../src/SpecScribe/ForgeOptions.cs)
- Harnesses: [`web/scripts/measure-parity.mjs`](../../web/scripts/measure-parity.mjs), [`web/scripts/experiment-two-ir.mjs`](../../web/scripts/experiment-two-ir.mjs), [`web/scripts/harness-lib.mjs`](../../web/scripts/harness-lib.mjs), [`web/scripts/build-package.mjs`](../../web/scripts/build-package.mjs), [`.github/workflows/build-test-analyze.yml`](../../.github/workflows/build-test-analyze.yml)

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
