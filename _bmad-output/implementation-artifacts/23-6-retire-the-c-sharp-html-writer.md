---
baseline_commit: e864133
---

# Story 23.6: Retire the C# HTML Writer

Status: in-progress

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
| **D5** | **(dev-story, 2026-07-31) The drift gate's corpus is PINNED, superseding D2's "read back `web/measurements/parity.json` over the live site".** D2's shape was measured before building on it and found **vacuous**: `goldenSha === irSha === nuxtSha` on **all 1,469** committed rows, so reading the committed value back asks the same question the live run already answers. That is structural, not an oversight — the IR *is* the renderer's input and the region passes through verbatim by contract, so input-digest and output-digest are one quantity, and over a corpus that changes "the content moved" cannot be told from "the renderer moved". The gate now renders a **frozen 24-route IR corpus** committed at `web/fixtures/parity-corpus/` (all 14 families), so **any** digest move is a rendering change by construction and a sibling story editing a doc can never turn it red (ADR 0033 §Decision 2). |

### ⚠️ A second finding that drove D5: the old oracle was blind to the chrome this story deletes

`measure:parity` hashes `mainRegion(html)` — the `<main id="main-content">` landmark only. So `<title>`,
`<meta name="description">`, the favicon data-URI, the footer, every `<script src>`, the nav toggle script, the
Mermaid init and the Hierarchy/Graph anti-flash handshakes have **never been inside any committed digest**.
Those are exactly what `HtmlRenderAdapter.Render` emits and what Task 6 deletes — the highest-risk surface in
the story had no gate at all, and the story's own § Regression hazards names all five.

The pinned oracle therefore carries **two** digests per route:

| digest | subject | provenance |
| --- | --- | --- |
| `mainSha` | the normalized `<main>` region | the **C# lineage** — Story 23.4 proved the composed region byte-equal to C#'s own render across 1,469 pages. `pin:parity` **re-verified it live, 24/24, against the still-existing writer** before freezing it. |
| `pageSha` | the normalized **whole page** | a **renderer snapshot**. Deliberately *not* taken from C#: the two sides were only ever claimed to agree on `<main>`, so pinning a C# whole-page digest would record a difference that has always existed and call it drift. |

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

- [x] **Task 1 — Stand up the replacement drift gate FIRST (AC: #3)**
  - [x] Add `web/scripts/check-parity.mjs` + `"check:parity"` to `web/package.json`. ⚠️ **The oracle's SHAPE
        changed on an owner decision taken mid-task (2026-07-31)** — see § Owner decision D5. It reads back a
        committed oracle over a **pinned corpus**, not the live `parity.json` over the dogfood site.
  - [x] **Fail loudly on a vanished oracle (ADR 0033 §5).** Three gates, all negative-tested: (A) the oracle
        must exist, parse, and carry routes with **both** digests; (B) every pinned route must render; (C)
        every family the oracle claims must still be covered. Modelled on `RegionCompositionCorpusProof`.
  - [x] Prove determinism — **3 consecutive local runs identical**, and the Ubuntu half is wired into
        `portability-probe` (ADR 0033 §4). ⚠️ The Linux proof lands on the next CI run; it is not yet observed.
  - [x] Add `npm run check:parity` to `build-test-analyze.yml` and to the `"check"` aggregate script; add a
        `build:package` step before it in both jobs.
  - [x] Recorded: `pin:parity` **writes** the oracle, `check:parity` **reads** it. `measure:parity` is marked
        SUPERSEDED in-file with the vacuity it acquires at Task 6 spelled out.

- [x] **Task 2 — Make the IR unconditional (AC: #6)**
  - [x] `_spaCapture`/`_spaPageViews` allocated unconditionally.
  - [x] `EmitSpaSite` runs on every `GenerateAll` — **and on all five incremental watch paths**, which were the
        same `if (_options.EmitSpa)` gate and would otherwise have left watch mode emitting no IR at all.
  - [x] `SiteSettings.Spa` is a deprecated no-op printing a one-line notice (new
        `ConsoleUi.PrintSpaDeprecationNotice`, wired into **both** `generate` and `watch`); option stays
        registered; `[Description]` rewritten.
  - [x] `ForgeOptions.EmitSpa` kept, defaulted `true`, `Resolve`'s `emitSpa` parameter default flipped to `true`;
        both document that the false path is gone. No longer consulted by `SiteGenerator`.
  - [x] **`EmitDeltaSidecar` verified already correct** — it was never gated on `--spa`, only on watch/serve
        (its own doc comment says so). No change needed; NFR9 byte-reproducibility of a one-shot `generate` holds.

- [x] **Task 3 — Drive the prerender from `generate` (AC: #7, #4)** — new [`NuxtPrerender.cs`](../../src/SpecScribe/NuxtPrerender.cs).
  - [x] Artefact resolution in the specified order, with **two defects the tests caught and fixed**: (a) outside a
        git checkout the error named only **two of three** locations, because the developer candidate was added
        only when a repo root was found; (b) an **explicitly set but invalid `SPECSCRIBE_RENDERER_DIR` fell
        through** to the repo's own artefact — rendering with a renderer the operator did not name, and reporting
        success. It is now a hard failure.
  - [x] Boots `node <artefact>/server/index.mjs` with `SPECSCRIBE_IR_DIR`, an OS-assigned port, readiness by
        polling (any HTTP response, including a 500), and `Kill(entireProcessTree: true)` in a `finally`.
        `experiment-two-ir.mjs` was read, not reused; its four recorded traps are carried into the type doc.
  - [x] One request per manifest route; body written to `<OutputRoot>/<path>`; non-200 / missing
        `<main id="main-content">` / failed request each raise `GenerationOutcome.Error`.
  - [x] **Asset ownership DECIDED: C# stays the single writer** of `specscribe.css`, `specscribe.js`, `prism.js`
        and `plotly-hierarchy.min.js`. They are embedded resources here, `sync-runtime-assets.mjs` already treats
        C# as authoritative, and the webview/SPA paths still need C# to place them. Mechanically the artefact
        copy **skips any file that already exists**, so there is exactly one writer per file.
  - [x] **Node prerequisite (AC #4) — all three ways.** Node absent from `PATH` verified **live** (actionable
        error naming the range, `errors=1`, not a silent empty root). Below-range verified by unit test over
        `ValidateNodeVersion` — ⚠️ a `PATH` shim **cannot** test this: `Process` with `UseShellExecute=false`
        resolves a real executable, not a `.cmd`, so a shim-based test silently exercises the *absent* path and
        passes for the wrong reason. Artefact missing verified live and by unit test.
        ⚠️ **Check order corrected**: the artefact was resolved *before* Node, so a user with no Node was told to
        build an artefact — the wrong next step. Node is checked first.
  - [x] **Watch mode** re-renders only routes whose region digest moved, derived from `SpaDelivery.ContentHash`
        rather than from the watcher's file event (one source edit moves several routes — its page, the
        dashboard, the epics index, a date page — and a file-keyed list would leave those stale).
  - [x] **Measured cold full generate:** 1,492 routes prerendered in **13,512 ms = 9.1 ms/route**; whole run
        77.0 s vs the pre-Task-3 `--deep-git --spa` baseline of 78.9 s. ⚠️ 9.1 ms/route is **2.3× Story 23.5's
        ~4 ms/route** — stated rather than discovered. C# is still writing every page in parallel at this point;
        re-measure after Task 6 removes that work.

- [x] **Task 4 — Re-point the test suite off the written document (AC: #8)**
  - [x] Add one shared test helper that returns a page's **region** given a completed generator and an
        output-relative path, reading the emitted `spa/` chunks (or exposing `CapturedRegions` to the test
        assembly). This is the substitute for the ~261 `File.ReadAllText(Path.Combine(Site, "…​.html"))` reads.
  - [x] Mechanically substitute the ~300 templater call sites:
        `XTemplater.RenderPage(args)` → `JsonSpaRenderAdapter.Shared.RenderContent(XTemplater.BuildPage(args))`.
        Story 23.4 already added every `BuildX` returning a `PageView`, so this is a substitution, not a rewrite.
  - [x] Triage the residue — the assertions that genuinely live in chrome, not in the region. Each moved to the
        view model / the IR's head or chrome projection, or to `web/test/`, or was removed with a reason.
  - [x] Do **not** keep a test-only full-page composer as a shortcut. None was added.
  - [x] ⚠️ **Two classes the story's inventory did not name, both of which fail SILENTLY** — see Dev Agent Record
        § Task 4 (second session): the `Directory.*("*.html")` walks (which go *vacuous*, not red) and the
        `SitePath("…")` family in the two watch-mode fixtures.

- [x] **Task 5 — Close the six dependents (AC: #2)**
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

- [x] **Task 6 — The deletion (AC: #1)** — done, and the suite is green: **2,932 passed / 0 failed**.
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

- [x] **Task 7 — Decision records (AC: #9)**
  - [ ] Author the new ADR (next free number is **0034**): the CLI's output contract inverts — the IR is the
        unconditional product and the static site is rendered from it by Node; `--spa` is retired. Status
        `Proposed`.
  - [ ] Propose **ADR 0022 → Accepted** (this story is its §Decision 3 executed) and **ADR 0033 → Accepted** (this
        story is its first implementation). Update `docs/adrs/README.md`.
  - [ ] Clear `deferred-work.md:22`'s standing action — it asks for exactly Task 1's gate — and say in the entry
        which of its two offered options was taken.

- [~] **Task 8 — Live-browser verification (CLAUDE.md § Verification)** — ⚠️ **PARTIAL: the mechanically
      checkable half is verified; the VISUAL half still needs a real browser.** See Dev Agent Record § Task 8.
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

claude-opus-5 (dev-story), 2026-07-30 → 2026-07-31. Baseline `e864133`, clean tree at start.

### ⚠️ STATUS: PARTIAL. The deletion has NOT happened.

**Tasks 1, 2, 3, 5 and 7 are complete. Task 4 is ~60% complete. Tasks 6 and 8 are NOT STARTED.**

`HtmlRenderAdapter.Render`, the five content-`.html` write paths, `_spaCapture` and `SpaDelivery.Extract*` are
all **intact and still shipping**. Everything landed so far is additive — the story's own named carve-out point
("Tasks 1–3 are additive and independently valuable; Tasks 4–6 are the irreversible half"). The tree builds, both
suites are green, and `specscribe generate` produces a working portal that is now rendered by Nuxt.

The remaining work is stated precisely under § What remains rather than left to be rediscovered.

### Completion Notes List

**Task 1 — the replacement drift gate (AC #3). DONE, with the oracle's shape changed on a new owner decision.**

- ⚠️ **Owner decision D2 was measured before being built on, and found VACUOUS.** `goldenSha`, `irSha` and
  `nuxtSha` are **identical on all 1,469 rows** of the committed `parity.json`, so "does the rendered page still
  hash to the committed golden value?" is the same question as "does the rendered page match the IR it was
  rendered from?" — the committed value adds nothing a live run does not already answer. That is structural: the
  IR *is* the renderer's input and the region passes through verbatim by contract, so input-digest and
  output-digest are one quantity. Over a corpus that changes, "the content moved" cannot be distinguished from
  "the renderer moved". Escalated to the owner rather than built as specified → **owner decision D5: pin the
  corpus.**
- ⚠️ **Second finding: the old oracle hashed `<main>` ONLY**, so `<title>`, meta, the favicon, the footer, every
  `<script src>`, the nav toggle, the Mermaid init and the Hierarchy/Graph anti-flash handshakes were in **no
  committed digest at all** — precisely the chrome Task 6 deletes. The new oracle carries a whole-page digest
  too, which is the first gate this project has ever had over that surface.
- Landed: `web/scripts/parity-lib.mjs` (pure, unit-tested, 19 tests), `check-parity.mjs`, `pin-parity.mjs`,
  `render-lib.mjs`, the frozen corpus at `web/fixtures/parity-corpus/` (24 routes, all 14 families, 3.65 MB) and
  the oracle at `web/measurements/parity-pinned.json`.
- **The C# lineage was re-verified LIVE before freezing: 24/24 routes, golden = IR = rendered**, against the
  still-existing writer. After Task 6 that check stops being possible, which is why it ran now.
- **All four loudness/drift paths negative-tested** by perturbing the oracle: region drift, chrome drift, empty
  oracle, unrenderable route + vanished family. Each fails, and each names the page.
- Determinism: **3 consecutive local runs identical.** ⚠️ **The Ubuntu half is WIRED but NOT YET OBSERVED** — it
  runs in `portability-probe`. ADR 0033 §Decision 4 is not discharged until that CI run is read.
- `measure:parity` marked SUPERSEDED in-file with the vacuity it acquires at Task 6 spelled out.

**Task 2 — the IR is unconditional (AC #6). DONE.** Also caught: the same `if (_options.EmitSpa)` gate sat on
**five incremental watch paths**, which would otherwise have left watch mode emitting no IR at all.
`EmitDeltaSidecar` verified already correct (gated on watch/serve, never on `--spa`) — no change needed.

**Task 3 — the prerender driver (AC #7, #4). DONE.** New `src/SpecScribe/NuxtPrerender.cs`. **Three defects found
by writing the tests first**, all fixed:

1. Outside a git checkout the artefact-miss error named only **two of three** locations.
2. An **explicitly set but invalid `SPECSCRIBE_RENDERER_DIR` silently fell through** to the repo's own artefact —
   rendering with a renderer the operator did not name, and reporting success. Now a hard failure.
3. **The artefact was resolved BEFORE Node**, so a user with no Node was told to build an artefact — the wrong
   next step. Found by actually running a generate on a PATH with no Node and reading what the user got.

⚠️ **A `PATH` shim cannot test the below-range Node path**: `Process` with `UseShellExecute=false` resolves a real
executable, not a `.cmd`, so a shim-based test silently exercises the *absent* path and passes for the wrong
reason. The version gate was extracted to `ValidateNodeVersion` and unit-tested directly.

**Measured cold generate: 1,492 routes in 13,512 ms = 9.1 ms/route** (whole run 77.0 s vs a pre-Task-3 baseline
of 78.9 s). ⚠️ **9.1 ms/route is 2.3× Story 23.5's ~4 ms/route** — stated rather than discovered. C# is still
writing every page in parallel at this point; re-measure after Task 6.

**Asset ownership DECIDED: C# stays the single writer** of `specscribe.css`, `specscribe.js`, `prism.js` and
`plotly-hierarchy.min.js`. The prerender copies the artefact's `public/` but **skips any file that already
exists**, so there is exactly one writer per file.

**Task 5 — the six dependents (AC #2). DONE. Every row closed explicitly:**

| # | dependent | disposition |
| --- | --- | --- |
| 1 | `_spaCapture` → `RegionCompositionDeltas` | ⏳ **deferred to Task 6** with the two proofs it feeds (owner D3). Still live and still passing. |
| 2 | `GoldenContentFingerprint` | **RETIRED.** 1,459 lines removed, replaced by an in-file block recording what it proved, why its subject is gone, and why it was deliberately NOT re-pointed at the IR (ADR 0033 names this story and forbids exactly that). |
| 3 | `GoldenOutputInventory` | **RE-PINNED, not superseded** — reason recorded in-file: `check:parity` renders a deliberately FROZEN corpus, so it is structurally blind to a whole page family ceasing to be emitted by a real generate. The two are complementary. Gained `app.html`, `specscribe-spa.js` and four `spa/pages-*.json`. |
| 4 | `EnsureHierarchyEngine`'s host-marker scan | **RE-DERIVED** from `page.Assets.HierarchyEngineNeeded / GraphEngineNeeded`, with the disk-is-the-truth guard preserved verbatim. All three call sites now pass a `PageView`. |
| 5 | `CapturedRegions`' silent-gap guard | **RETIRED**, with the reason in-file — it would have gone **vacuous, not red**, which is the failure AC #2 exists to prevent. What still covers the underlying invariant is named. |
| 6 | `RenderWebviewSurfaces`' long-tail gate | **RE-POINTED — and NOT verbatim, which is the finding.** Re-pointing `_spaCapture` → `_spaPageViews` was WRONG and a test caught it: the webview surface set silently grew to the whole long tail. The old condition only ever worked by coincidence (`_spaCapture` stood in for `CapturePages` *and* fired on any `--spa` run). Now gated on `CapturePages`, the flag that actually means "this caller wants the long tail". The sibling throw moved with it. |

**Task 4 — re-point the suite (AC #8). PARTIAL.**

- ✅ **268 full-page templater call sites re-pointed** to `JsonSpaRenderAdapter.Shared.RenderContent(BuildX(...))`
  across 19 files, by a **paren-balanced C#-aware scanner** — not a regex, because most calls span lines and carry
  string literals full of parentheses, and a regex would truncate them silently. Compiled on the first pass.
  **247 of 268 assertions needed no further change** — they assert body content, which is in the region unchanged.
- ✅ The 38 full-page wrappers were discovered by **reading the source** (wrapper bodies calling
  `HtmlRenderAdapter.Shared.Render`), so fragment renderers — `RenderPane`, `RenderBoard`, `RenderProgressWheel`,
  `RenderEmbedded`, `RenderBoardByEpic`, `RenderModuleCoveragePanelBody` — were correctly left alone.
- ✅ **21 chrome assertions triaged**, each to the view model (`page.Title`, `page.MetaDescription`,
  `page.Assets.ExtraHead`, `page.Assets.GraphBootInline`, `page.Assets.GraphEngineNeeded`) or removed **with the
  gate that now owns it named in place** (`check:a11y` for the skip link and its ordering; `check:parity`'s
  `pageSha` for the favicon). Nothing was deleted merely for failing to compile.
- ✅ Two shared helpers landed: `tests/SpecScribe.Tests/RegionAssert.cs` (region + view-model assertions, carrying
  the full "where each chrome assertion went" table) and `tests/SpecScribe.Tests/SiteRegion.cs` (reads a page's
  region back out of the emitted IR — the substitute for the disk reads).
- ❌ **NOT DONE: the ~206 `File.ReadAllText(Path.Combine(Site, "….html"))` reads across 32 files.** They still
  pass, because C# still writes the pages. They break the moment Task 6 lands. `SiteRegion.Read/Exists/Routes` is
  written and building, ready for the substitution.
- ✅ Two tests obsoleted by AC #6 were **re-pointed rather than deleted**: `WithoutSpa_EmitsNoSpaFilesAtAll` is now
  `TheIrIsEmittedUnconditionally_EvenWhenTheRetiredSpaFlagIsOff` (inverted, and the inversion is the point), and
  `DeltaSidecar_WritesNothing_WhenSpaIsOff` now pins the switch as the only gate **in both directions** — its old
  reasoning ("no IR, so nothing to diff") had it agreeing with the right behaviour for the wrong reason.

**Task 7 — decision records (AC #9). DONE.** New **ADR 0034** (Proposed) records the output-contract inversion and
the `--spa` retirement. **ADR 0022 → Accepted** and **ADR 0033 → Accepted** are PROPOSED in-file (ratification is
the owner's, so their `Status` lines are annotated, not flipped). ⚠️ The ADR 0033 proposal carries **a requested
amendment**: its "reference implementation" names `web/measurements/parity.json`, which this story measured to be
vacuous. `docs/adrs/README.md` updated. `deferred-work.md`'s standing action **cleared, naming which of its two
offered options was taken** — the first (rebuild a gate), with the second explicitly rejected on measurement.

---

## Second dev-story session (2026-08-02)

### ⚠️ THE HEADLINE: a SEVENTH dependent, three LIVE regressions, and the renderer was reading the page being deleted

Task 4's residue was mechanical. Auditing what Task 6 would remove was not. Four findings, all of which had to be
fixed **before** the deletion rather than after it:

**1. `web/ir/adapter.ts` § `readGoldenChrome` READ THE GENERATED `index.html`.** It scraped three values out of
it — the `?v=` asset cache-bust, the favicon `data:` URI, and the Hierarchy Explorer's anti-flash boot script —
and it did so inside a `try { … } catch { return { …: '' } }`. Deleting the writer removes that file, so the
catch fires and **every rendered page silently loses its favicon, its cache-bust and its anti-flash marker**,
with every gate green. This is a dependent of the deletion, and it is **not one of the six** the story
enumerates. It is also the one whose failure mode is worst, because the swallowed exception guarantees silence.

**2. Mermaid was ALREADY BROKEN on the shipped portal.** `HtmlRenderAdapter.Render` is the only emitter of
`Mermaid.InitScript()`, and the Nuxt renderer had **no mermaid handling of any kind** — no probe, no head entry,
nothing. Since Task 3 made the prerender the writer of every `.html`, every diagram on the generated portal has
been shipping as an inert `<pre class="mermaid">` block. Verified on `SpecScribeOutput/about-sdd-bmad.html`:
`data-ir-family` present (so Nuxt rendered it), the mermaid block present, `mermaid.esm.min.mjs` count **0**.

**3. The Story 24.2 relationship graph was ALREADY BROKEN, the same way and worse** — it lost *both* halves.
A `data-relgraph` page carried **0** occurrences of `data-ss-relgraph-boot` and **0** of
`plotly-hierarchy.min.js`: no anti-flash marker and no charting engine, so the graph could not mount at all.
The renderer gated the engine on `needsHierarchyEngine` alone, where C# gated it on
`HierarchyEngineNeeded || GraphEngineNeeded` (ADR 0030 — one bundle serves both families).

**4. `Toc.ActiveSectionScript` was absent too**, so the TOC sidebar never tracked the active section.

Findings 2–4 are regressions Task 3 introduced and no gate could see: `check:parity`'s `pageSha` is a *renderer
snapshot*, so it pinned the broken output as correct on the day it was pinned. This is exactly the blind spot
CLAUDE.md § *Which gate is which* warns about, arriving from the other direction.

**The fix — the IR carries its own chrome now.** New `SpaDelivery.ManifestChrome`, a site-level `chrome` block on
the manifest carrying `assetVersion`, `faviconDataUri`, `hierarchyBootScript`, `graphBootScript`,
`mermaidInitScript` and `tocActiveSectionScript`. The scrape is gone. Purely **additive**, so
`SpaDelivery.SchemaVersion` is deliberately **not** bumped — that field's own compatibility rule reserves a bump
for a removal, rename, type change or meaning change. This also closes the *"ONE named gap handed to Epic 22"*
that `readGoldenChrome`'s comment described.

Per-page gating stays **derived, never read from a flag** — new exported `chromeNeeds()` in `web/ir/adapter.ts`
probes the region for each marker, mirroring how every C# `AssetManifest` flag is computed from the finished
body so a flag cannot disagree with the page carrying it. Reading a flag out of the IR instead would reintroduce
Story 20.7's bug shape exactly. Pinned by the new `web/test/chrome-needs.test.ts` (7 cases), including the
escaped-prose case that has bitten three separate stories — this portal renders its own source, so every one of
these markers appears as prose on `code/**` pages.

**Verified fixed on the regenerated portal**, following CLAUDE.md's load-bearing order (non-incremental build →
generate → `build:package` → generate): mermaid page now carries `mermaid.esm.min.mjs` in a `type="module"` tag;
the `data-relgraph` page carries both `data-ss-relgraph-boot` and `plotly-hierarchy.min.js`; the ADR page carries
the `IntersectionObserver` tracker; `index.html` keeps favicon, hierarchy boot, engine and `?v=1a1550ca`.

### Task 4 (second session) — the residue, and two classes the story's inventory missed

The story budgeted "~206 `File.ReadAllText(Path.Combine(Site, …))` reads across 32 files". That count was right
for the shape it named, but two further classes had the same subject and **fail silently rather than loudly**:

| class | count | why it matters |
| --- | --- | --- |
| `File.ReadAllText`/`File.Exists` over `Path.Combine(Site, "…html")` | 97 + 64 | named by the story; fails LOUDLY (`FileNotFoundException`) |
| path-holding members (`private string IndexPage => Path.Combine(Site, "index.html")`) | 48 | **renamed** `…Page` → `…Route` as well as re-valued, so a consumer the tool did not understand became a COMPILE error instead of a relative path read from the CWD |
| **`Directory.GetFiles/EnumerateFiles(…, "*.html")` walks** | 21 sites | ⚠️ **VACUOUS, not red.** A walk of a directory nothing writes returns empty, so every `foreach`/`DoesNotContain` over it passes while asserting nothing |
| **`SitePath("…")`** in the two watch-mode fixtures | 44 | same subject, different helper — invisible to a `Path.Combine(Site` grep |

Done with a **paren-balanced, comment- and string-aware C# scanner**, not a regex. The first version of that
tool had a bug worth recording: it rewrote `<c>File.ReadAllText(Path.Combine(Site, "….html"))</c>` **inside the
doc comments** of `SiteRegion.cs` and `RegionAssert.cs`. Caught on the dry run; the tool now builds an inert-byte
mask over comments and string literals before matching.

Six near-identical private `AssertNoBrokenLocalLinks` copies were consolidated into one
`SiteRegion.AssertNoBrokenLocalLinks`, which resolves a **page** link against the IR route set and an **asset**
link against the disk — the split is the substance, since resolving a page link on disk would assert the writer
still exists.

New `SiteRegion` members: `Head`, `Chrome`, `RoutesUnder`, `HasRoutesUnder`, `PoisonRoute`,
`AssertNoBrokenLocalLinks`.

**Two vacuous assertions were found by the conversion itself**, which is the argument for doing it this way:

- `SiteGeneratorEpicsRemovalTests`' `requirements/` subtree pair asserted
  `Directory.Exists(SitePath("requirements"))` — and that directory is **empty** in this fixture (FR1 yields
  `requirements.html` but no detail pages). The positive half was passing on an empty directory and the negative
  half only ever proved that an empty directory gets tidied up. Removed, with the reason recorded in-file; the
  `epics/` pair beside it is real and kept.
- `SiteGeneratorCodePagesTests`' path-traversal guard looked for a leaked secret by listing `code/` — which will
  not exist at all after Task 6. Re-pointed at the route set.

`SiteGeneratorSpaTests.HierarchyEngineBundle_*`' second half was re-pointed rather than preserved: pairing the
`<script src>` **tag** against the chart host is a tautology under Nuxt (the renderer derives one from the other),
so it now asserts the invariant that is still a C# decision — the engine ASSET is copied iff some region hosts a
chart, which is Story 20.7's actual bug.

`WebviewFirstPaintTests` was deliberately reverted to disk assertions and left for Task 6: its subject IS the
write switch, and three of its assertions are about write ORDERING, not about the IR.

**Suite: 2,948 passed / 0 failed** (was 2,944). `web/`: **161 passed** (was 155; +6 `chrome-needs`).

### Task 6 — the deletion. DONE in the source; the suite is not yet caught up.

**Everything the task lists as a deletion is deleted, and both projects compile.** Grep-verified after each step
per CLAUDE.md § Concurrent work:

| deleted | replaced by |
| --- | --- |
| `HtmlRenderAdapter.Render` | Nuxt, from the IR. `HtmlRenderAdapter` **no longer implements `IRenderAdapter`** — it composes nav/wayfinding markup *inside* the region now, so keeping the delivery interface would have claimed a capability it does not have. |
| **38 templater full-page `RenderX` wrappers** across 28 files | their `BuildX` view models, which all survive (28 files still export one) |
| all **five** content-`.html` write paths | `WritePage` composing a region into `_spaPageViews`; the four epics-family raw `File.WriteAllText` calls are now ordinary `WritePage` calls |
| `WriteOutput` + `_spaCapture` | `WriteVerbatimPage`, kept for Story 18.4's `forge-report.html` alone — a foreign document with no landmark, never an IR route |
| `SpaDelivery.ExtractContentRegion` / `ExtractTitle` / `ExtractBreadcrumb` / `ExtractMetaDescription` / `ExtractNavMarkup` | composition at the `WritePage` seam. `ExtractScriptIslands`, `ContentHash`, `BuildDataFiles`, `BuildDelta`, `BuildEntryShell`, `MainLandmark` kept as specified. |
| `SiteGenerator.CapturedNavMarkup` | the composer renders the nav from the view model, so the page-local context band that motivated the slice is present by construction |
| `RegionCompositionDeltas` + `RegionParityDelta` | `check:parity` (owner D3) |
| `RegionCompositionParityTests`, `RegionCompositionCorpusProof` | tombstoned in-file with what they proved, the numbers (1,469 pages / 0 deltas), and where the evidence lives |
| 7 `SpaDeliveryTests` slicer tests | tombstoned — see below |
| `SemanticFacts.Stylesheet` / `.Script` | `check:parity`'s `pageSha`. ⚠️ `MermaidPresent` was removed too and **that was wrong** — the webview declares a *sanctioned divergence* on it, so deleting the fact deleted the meaning of that declaration. Restored, re-derived from the region (`Mermaid.BlockMarker`). Caught by its own test. |

**Two findings worth keeping.**

1. **`ReconcileSpaCapturePrefix` had to be deleted, not re-pointed — it had become actively dangerous.** It
   evicted any captured route whose file was missing from disk, to converge after a partial directory delete.
   After this story EVERY route's file is missing, so on any watch pass that touched a subtree it would have
   emptied the IR of that whole subtree. Replaced by `RemoveRoutesUnder`, which works in route space.
   `DeleteOutputFile` → `RemoveRoute` for the same reason: its `File.Exists` check is false for every page now,
   so it took its "already gone" branch every time and returned `false` — reporting zero removals for removals
   that did happen.
2. **The `epics/` directory wipe was removed** along with the writes it protected. Staleness is a question about
   the route set now, and `RegenerateEpics` already evicts removed routes.

**The slicer tests' adversarial case is now UNREACHABLE, not merely untested** — worth recording because it is
what the story bought. The headline one pinned a page whose body renders `</main>` as prose (Markdig raw-HTML
passthrough, reachable from any user doc): a naive slice resolved `mainClose` before `mainOpen` and threw,
taking the whole emit down. Composition never searches a rendered string for its own boundaries.

### ~~47 TESTS ARE RED~~ — RESOLVED. Suite green at 2,932 passed / 0 failed

**2,888 passed / 47 failed / 2,935 total.** Both projects compile; `src/` has zero warnings-as-errors. The
failures are **not** a defect in the deletion — every one is a test still asking the DISK about a page:

| class | n | shape |
| --- | --- | --- |
| `IncrementalOracleParityTests` | 16 | compares an incremental run's **output tree** against a full rebuild's, file by file. Needs re-expressing as a comparison of IR **route sets + region digests**. The largest single piece. |
| `SiteGeneratorDesignSystemTests` | 12 | `FileNotFoundException` on `design-system.html` — a straight `SiteRegion.Read` substitution |
| `IdeasTests`, `FollowUpSurfacesTests`, `SiteGeneratorSpaTests`, `SiteGeneratorReadmeTests` | 14 | same shape, via site-root expressions the mechanical pass did not match |
| `WebviewRenderAdapterTests`, `WebviewFirstPaintTests`, `SiteGeneratorStoryEpicPagesTests`, `SiteGeneratorAdapterTests`, `RenderSpaParityTests` | 5 | individual; `WebviewFirstPaintTests` is the deliberate one — its subject IS the `WriteStaticPages` switch, which Task 6 removed, so it must be retired rather than re-pointed |

**Do not "fix" these by reintroducing a writer.** Each is a `SiteRegion.Read/Exists/Routes/RoutesUnder`
substitution or, for the output-tree comparisons, a route-set comparison. `SiteRegion` already carries every
accessor needed (`Read`, `Exists`, `Routes`, `RoutesUnder`, `HasRoutesUnder`, `Head`, `Chrome`, `PoisonRoute`,
`AssertNoBrokenLocalLinks`).

### ⚠️ CI regression: the SECOND workflow was never given the artefact (2026-08-02)

`publish-docs-live-pages.yml` — the GitHub Pages job — failed after the deletion with
`errors=1` and *"The SpecScribe renderer artefact could not be found, so no HTML can be produced."*

**Why it was missed.** The identical ordering fix was applied to `build-test-analyze.yml` when Task 3 landed
and is documented there at length — but this workflow had **no Node steps at all**, so nothing in it *looked*
like it depended on the artefact. And before Task 6 a missing artefact was survivable here: C# still wrote every
page, so the generate reported an error while the output root was fully populated and Pages published anyway.
Task 6 turned a degraded run into an empty one.

That is the SAME lesson the first CI failure produced, arriving from the other side. Then it was "when a step
acquires a new dependency, re-read the steps *before* it." Now it is **"…and audit every workflow that runs it,
not only the one whose failure you happened to see."**

**Fixed** by mirroring `build-test-analyze.yml`'s proven sequence: `Setup Node` (`web/.nvmrc`) →
`npm ci` with `SPECSCRIBE_PACKAGE_BUILD=1` → `sync:assets` → `build:package` → generate. Verified locally from a
wiped output root: **1,513 routes prerendered in 5,194 ms (3.4 ms/route), errors=0, 1,514 `.html` on disk**, and
`index.html` carries `data-ir-family` (so it is the Nuxt render, not a leftover).

**Two hardening changes made alongside it, both because the failure mode here is silence rather than noise:**

1. **The validate step now counts rendered pages** (`> 100`), not just `test -d SpecScribeOutput`. A run that
   emits the IR but renders nothing still leaves the output root populated with `spa/` and the shared assets, so
   the directory check alone passes on an unbrowsable site.
2. **`web/**` added to the workflow's `paths` trigger.** Nuxt renders every published page now, so a Vue
   component or `ir-content.css` edit changes what Pages serves exactly as directly as a `src/SpecScribe/` edit.
   The omission would never have failed loudly — `web/`-only pushes would simply not republish, and the live
   site would drift behind `main` until an unrelated commit happened to trigger a rebuild.

### Closing out the 47 — three were REAL BUGS, not stale assertions

The class held: every failure was a test still asking the disk about a page. But working through them surfaced
three defects in the deletion itself, which is the argument for re-pointing the assertions rather than deleting
them.

**1. The `epics/` directory wipe was load-bearing, and I removed it on a false premise.** Task 6's first pass
dropped the wipe with the note "`RegenerateEpics` already evicts removed routes from `_spaPageViews`". It does —
but only when the DOCUMENT behind a route disappears. An undrafted story's placeholder has no document; it exists
because epics.md lists the story. Drop the story and nothing told the IR the route was gone, so the placeholder
survived. `RegenerateEpics_PrunesPlaceholderWhenStoryLeavesThePlan` caught it, and so did a watch-mode sprint
test. Fixed with `RemoveRoutesUnder("epics")` immediately before the re-compose loop — the route-space form of
the wipe, same rebuild-the-whole-subtree shape.

**2. A test-side lock that broke the GENERATOR.** `BurstOfSaves_CoalescesAndLeavesCoherentOutput` began failing
with a generator error: *"the process cannot access the file `spa\pages-notes.json` because it is being used by
another process."* `File.ReadAllText` opens with `FileShare.Read`, which denies a concurrent WRITER. Harmless
when each test read its own settled page; once pages became IR routes, every watch-mode read targets the same
few chunk files and the assertions were locking the chunk the watcher was writing. `SiteRegion` now reads with
`FileShare.ReadWrite` — a read-only assertion must not perturb what it observes.

**3. `WriteStaticPages` became a switch that silently did nothing.** With the full-document render gone, the
property had no readers, yet `Commands.cs` still set it to `false` for the webview path. Deleted rather than
left: a flag named "write static pages" that does nothing is worse than no flag, and the ~12 s it used to buy is
unconditional now.

**The sanctioned-divergence registry is now EMPTY, and that is the honest state.** Retiring `SemanticFacts`'
chrome axis took the subject of every registered exception with it — the webview's `asset.css`, `asset.js` and
`mermaid`, and the SPA's `mermaid`. Each registered a difference against the C#-rendered PAGE, and no C# code
path renders one. The registry's own contract ("a registered divergence that no longer exists is a bug") forced
the cleanup. ⚠️ **This does not mean the webview gained Mermaid** — it still cannot run it under its CSP. What
changed is that the difference stopped being expressible as a C#-side parity fact: ADR 0024 makes every surface
a projection of one composed region, so they agree by construction and every remaining difference lives in
chrome, which is the renderer's. The capability gap is recorded in ADR 0005/0032, where it belongs. The two
tests that proved those exceptions were load-bearing are inverted rather than deleted, so re-adding an exception
without a divergence to justify it now fails.

`MermaidPresent` survives, re-derived from the region (`Mermaid.BlockMarker`) rather than from the deleted
head's `mermaid.initialize`. ⚠️ I removed it first and a test caught that: it still catches a page whose
`AssetManifest` claims a diagram the body does not carry.

**`GoldenOutputInventory` re-pinned as TWO lists** (AC #2, dependent #3): the FILE set on disk (11 — assets, the
IR, the SPA shell, and no `.html`) and the ROUTE set in the IR (29 — the page inventory). A single merged list
would let a page family vanish from the IR while its assets stayed put. Plus a direct assertion that no C# path
writes a `.html`, so the failure names the cause instead of diffing forty paths.

### ⚠️ CI fix #3 — `check:ir-content` was validating a NARROWER corpus than the one that ships

CI went red at `check:ir-content` with **+4 / −182 rules**. That is a mass prune, not incidental drift, so per
CLAUDE.md I established causality before touching the baseline — and it was not a CSS change at all.

`extract:ir-content` PRUNES any rule whose selector names a class it cannot find in the IR, so the generated
layer is only correct for the corpus it was extracted from. Two workflows generate two different corpora:

| workflow | generate | corpus |
| --- | --- | --- |
| `publish-docs-live-pages.yml` — **what actually ships** | `--deep-git` | code-insights history/relationships tabs, relgraph swatches, deep-analytics panels |
| `build-test-analyze.yml` — the gate | *(no flag)* | none of those surfaces |

Commit `b397084` (a concurrent session) regenerated the layer from a **deep-git** IR — `+912 / −104` lines — so
the committed layer is correct for the published site. The gate then pruned 182 of those rules because its own
generate is shallow. **Reproduced both directions locally: `+4 / −182` without the flag, `+0 / −2` with it.**

**Fixed by aligning the GATE to the published corpus** (`--deep-git` added to the CI generate), not by
regenerating the layer to match the shallow run. That direction matters: re-extracting from the narrower corpus
would have silently stripped styling from surfaces the deployed portal actually renders — the exact
prune-with-a-green-gate failure CLAUDE.md § *Changing `specscribe.css`* warns about.

The residual `−2` (`.mini-donut`) was the documented data-dependence: my own story-file edits change the
epics/story pages, which changes the IR. Regenerated, and **confirmed stable across two full generate+extract
cycles** (identical diff both times) before committing.

⚠️ **A structural limitation this exposed, worth a follow-up rather than a fix here.** `extract:ir-content`
prunes against ONE corpus, so whichever you pick loses the other's rules — the shallow run's four
graceful-degradation rules (`codemap-notice`, `risk-quadrant-empty`) are pruned by the deep-git extraction just
as the 182 were pruned by the shallow one. A project with no git history would lose that notice styling. The
layer needs the UNION of corpora, not one of them.

### ⚠️ TWO GATES WERE VACUOUS AND I REPORTED THEM GREEN — corrected

I ran `check:a11y` and `check:links`, saw them exit 0, and told the owner "all gates green". That was wrong,
and only reading the committed **measurement diff** before staging caught it:

```
-  pages checked        1474            -  pages walked   1476   (nuxt)
+  pages checked        0               +  pages walked   0      (nuxt)
```

Both walk `web/.output/public` — the pages a `nuxt build` PRERENDERS into the artefact. Every workflow now
builds with **`build:package`**, which sets `SPECSCRIBE_PACKAGE_BUILD=1` and **deliberately empties the route
table**: a package artefact must carry no prerendered HTML, because Nitro serves `public/` ahead of the SSR
route and would otherwise return the BUILDING project's pages when pointed at another project's IR (the
HTTP-200-wrong-answer trap from Story 23.5). So that directory is empty by design, both gates walked nothing,
and both reported success — `failures 0` over `pages checked 0`, and "no regressions" from comparing an empty
set against everything.

This is the exact failure AC #2 names, in the harness rather than the code, and **it was already live in CI**
before this session — `build:package` has been the CI path since Task 1. It is not a regression I introduced;
it is one the deletion made visible, because `.output/public` used to be populated back when the documented
local flow ran a plain `npm run build`.

**Fixed:** both now walk `ir.IR_DIR` — the site a real generate produces, which since this story IS the
Nuxt-rendered output, and which no build-mode flag can empty — plus a hard `< 50 pages` failure so a vanished
basis can never read as a pass again. `check:a11y`: **1,515 pages, 3,800 status chips, 0 failures.**
`check:links` is now honestly ONE-SIDED (the story's Dev Notes anticipated this half): there is no second tree
to compare against, so it asks "does every internal link resolve?" and reports 1,229 pre-existing dangling
references instead of printing two identical columns under a "nuxt vs golden" heading.

### ⚠️ One intermittent watcher test — flagged, not dismissed

`FileWatcherServiceTests.SprintStatusYaml_AddedThenEditedThenRemoved_RefreshesTheSprintSurfaceEachTime` failed
once in **four** full-suite runs and passes **3/3 in isolation**. The other three full runs were clean at
2,932/0.

I am flagging rather than filing it under the documented spawn-starvation flake, because I do not have the
evidence to claim that: this is a watch-mode timing test in exactly the family this story re-pointed, and the
honest statement is "intermittent, cause unconfirmed". Two things make a genuine regression less likely — the
`FileShare.ReadWrite` fix above removed a real test/generator contention in this same family, and the failure
mode is a `WaitFor` timeout rather than a wrong assertion — but neither is proof. Worth a second look if it
recurs on CI.

### Task 8 — the mechanically checkable half is VERIFIED; the visual half is not

Ran against a clean `--deep-git` generate (784 pages, `errors=0`, 1,513 routes prerendered).

| check | result |
| --- | --- |
| **ADR 0022 §Decision 6 page-relative rewrite** — the first generate where C# does NOT emit these paths | ✅ **1,240 references across depths 0–5, every one page-relative and resolving on disk.** No root-relative or protocol-relative reference anywhere (either would 404 from `file://`). |
| Hierarchy Explorer boots | ✅ anti-flash marker + engine, correct depth prefix |
| Story 24.2 relationship graph boots | ✅ both halves at depth 4 with `../../../../` — the regression this session fixed |
| Mermaid init | ✅ present as `type="module"` |
| **The escaped-prose guard, on a real page** | ✅ ADR 0034's own page mentions `data-relgraph` and `plotly-hierarchy.min.js` in prose and correctly ships **zero** real script tags. This is the trap the Dev Notes say has bitten three separate stories. |
| `check:parity` / `check:ir-content` / `check:a11y` / `check:links` | ✅ all green |

❌ **NOT done, and it is the half CLAUDE.md § Verification actually insists on**: opening the rendered site in a
real browser at depth 0 and depth 3 to look for CSS containment leaks, sub-pixel layout collapse, and DOM
corruption. All three have shipped in this epic and were caught only by looking; grepping emitted HTML cannot
see any of them. Chrome automation was not connected in this session. **This is the one outstanding item, and
it should gate the story's move to `review`.**

### What remains (updated 2026-08-02)

1. ~~The 47 red tests.~~ **DONE** — suite green at 2,932 / 0. Three real bugs found in the process; see above.
2. **Task 8's VISUAL half** — a real browser at depth 0 and depth 3. Everything else in Task 8 is verified
   (see the table above). This is the only thing standing between the story and `review`.
3. ~~Re-measure the prerender.~~ **DONE.** 9.1 ms/route with C# still writing → **3.4–3.7 ms/route** after
   the deletion, confirming the gap was the duplicated write rather than the transport. Recorded in ADR 0034.
4. **Read the `portability-probe` CI run** to discharge ADR 0033 §Decision 4 for `check:parity` on Ubuntu.
5. ~~ADR 0034 needs amending~~ — **DONE 2026-08-02.** Amended with a new **Decision 6** (the IR carries the
   site-level `chrome` block; per-page need stays DERIVED from the region, never read from a flag; purely
   additive so `SchemaVersion` is not bumped), a second § Context hazard recording the `readGoldenChrome`
   scrape, a corrected prerender figure (**3.7 ms/route** post-deletion vs the 9.1 ms/route measured while C#
   was still writing), a consequence recording that the renderer no longer reads the generated site, and an
   § Amendment history explaining why this completes the ADR rather than superseding it. `docs/adrs/README.md`
   updated to match. Status stays **Proposed** — ratification is the owner's.

### Superseded (kept for the record)

1. **Task 4 residue** — substitute the ~206 `File.ReadAllText(Path.Combine(Site, "….html"))` calls for
   `SiteRegion.Read`, then triage whatever chrome assertions surface. Do this BEFORE Task 6, or the suite goes
   red wholesale.
2. **Task 6 — the deletion.** Unchanged from the story text. Useful finding for whoever picks it up:
   `EpicsTemplater.Render*`, `HtmlTemplater.RenderIndex/RenderPage`, `RequirementsTemplater.*`,
   `RetroTemplater.RenderIndex`, `SprintTemplater.RenderIndex` and `CodeFileTemplater.RenderPlaceholder` are the
   **only** full-page wrappers still called from production — and they are exactly the five raw write paths, which
   confirms § The five write paths independently.
3. **Task 8 — live-browser verification.** A portal IS currently generated and Nuxt-rendered
   (`SpecScribeOutput/`, 1,188 pages, `data-ir-family` present, `_nuxt/` assets copied), so this is doable now.
4. **Read the `portability-probe` CI run** to discharge ADR 0033 §Decision 4 for `check:parity` on Ubuntu.

### ⚠️ CI failed on the first push, and BOTH causes are recorded here

**Cause 1 — an ordering inversion I introduced (the gating job's hard failure).**

`build-test-analyze` ran `Generate the IR` **before** `npm ci` and before the artefact build. Task 3 made
`specscribe generate` *drive the prerender*, so the generate now needs the artefact — which needs `npm ci`,
which (per the step's own long-standing comment) needed the IR the generate produces. **A cycle.** With no
artefact present every route errored, `errors=1`, non-zero exit, and the step failed hard.

I had actually written *"could equally run before the generate above"* in that step's comment — I noticed the
ordering was flexible and did not notice it had just become **required**. The lesson is narrow and worth
keeping: when a step acquires a new dependency, re-read the steps *before* it, not only the ones after.

**Fix:** `npm ci` → `sync:assets` → `build:package` → `generate`, with `SPECSCRIBE_PACKAGE_BUILD=1` on the
install. That flag stubs the manifest empty (its whole purpose), which breaks the cycle at the point it was
always breakable. The same fix was applied to `portability-probe`, whose `npm ci` would have failed too — that
job runs no generate at all, so it never has an IR. Verified locally by running the corrected sequence end to
end: `generate` → 1,197 routes prerendered, `errors=0`.

**Cause 2 — `check:ir-content` was red, and it is the DATA-dependence drift, not a CSS change.**

Two `.mini-donut` rules were missing from the generated layer. `specscribe.css` has carried that class since
`af1b5436` and I never touched it — but the extractor derives the layer from the C# stylesheet **and the
emitted IR**, and the IR's epics/story pages are rendered from `sprint-status.yaml` and the story files. **My
own story bookkeeping** (moving 23.6 to `in-progress`) changed those pages and pulled the class into the IR.
Regenerated with `npm run extract:ir-content`; the fresh derivation was `+2 / -0 / ~0`, so nothing was narrowed.

**Cause 3 — one I found while investigating, before CI could.** `pageSha` hashed Nuxt's content-hashed chunk
names (`_nuxt/PageShell.Ys9LGDmo.css`). Those are a property of the BUILD, so an artefact rebuilt on a
different machine would have reported CHROME DRIFT on every route — a failure unrelated to the change under
test, which is exactly what ADR 0033 §Decision 2 forbids, and it would have surfaced as a mystery on
CI-Ubuntu. New `foldBuildAssets` folds the digest only: the asset's directory, stem and extension all survive,
so a chunk being renamed, added, dropped or moved between `<link>` and `<script>` is still caught. Oracle
re-pinned (lineage re-verified 24/24), 5 new unit tests.

### Debug Log References

- Suite at hand-off: **2,883 passed / 3 skipped**, plus rotating git-dependent failures that are the **documented
  spawn-starvation flake** (memory: `suite-flake-cause-is-a-running-preview-server`), not regressions — the four
  implicated classes pass **25/25 in isolation**. `web/`: **144 passed** (was 125; +19 new `parity-lib` tests).
- ⚠️ Also observed: `GenerateAll_DeterministicAcrossTwoRuns` and `GenerateAll_TwoRunsProduceIdenticalHubMarkup`
  failed once on a **minute boundary** (`10:07` vs `10:08` in the wall-clock footer). Pre-existing fragility, but
  making the IR unconditional lengthens every run and therefore makes straddling more likely. Worth a normalizer.

### File List

**New**
- `src/SpecScribe/NuxtPrerender.cs`
- `tests/SpecScribe.Tests/NuxtPrerenderTests.cs`
- `tests/SpecScribe.Tests/RegionAssert.cs`
- `tests/SpecScribe.Tests/SiteRegion.cs`
- `web/scripts/parity-lib.mjs`, `web/scripts/check-parity.mjs`, `web/scripts/pin-parity.mjs`, `web/scripts/render-lib.mjs`
- `web/test/parity-lib.test.mjs`
- `web/fixtures/parity-corpus.routes.json`, `web/fixtures/parity-corpus/spa/manifest.json`, `web/fixtures/parity-corpus/spa/pages-pinned.json`
- `web/measurements/parity-pinned.json`
- `docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md`

**New (second session, 2026-08-02)**
- `web/test/chrome-needs.test.ts`

**Deleted (second session — Task 6)**
- `HtmlRenderAdapter.Render` and 38 templater full-page `RenderX` wrappers across 28 `src/SpecScribe/*Templater.cs`
- `SiteGenerator`: `WriteOutput`, `_spaCapture`, `ReconcileSpaCapturePrefix`, `DeleteOutputFile`,
  `DeleteOutputDirectory`, `CapturedNavMarkup`, `RegionCompositionDeltas`, `RegionParityDelta`
- `SpaDelivery`: `ExtractContentRegion`, `ExtractTitle`, `ExtractBreadcrumb`, `ExtractMetaDescription`,
  `ExtractNavMarkup`
- `SemanticFacts.Stylesheet`, `SemanticFacts.Script`
- test bodies of `RegionCompositionParityTests` / `RegionCompositionCorpusProof` (tombstones retained) and 7
  slicer tests in `SpaDeliveryTests.cs`

**Modified (second session)**
- `src/SpecScribe/SpaDelivery.cs` (the `chrome` block), `src/SpecScribe/PathUtil.cs`
  (`CurrentFaviconDataUri`), `src/SpecScribe/RenderParity.cs`, `src/SpecScribe/HtmlRenderAdapter.cs`
- `web/ir/adapter.ts` (`chromeNeeds`, the scrape removed), `web/ir/types.ts`,
  `web/components/surfaces/IrSurface.vue`
- `web/fixtures/parity-corpus/**`, `web/measurements/parity-pinned.json` (re-pinned, lineage verified 24/24)
- `tests/SpecScribe.Tests/SiteRegion.cs` (`Head`, `Chrome`, `RoutesUnder`, `HasRoutesUnder`, `PoisonRoute`,
  `AssertNoBrokenLocalLinks`), plus ~40 test files re-pointed at the IR

**Modified**
- `src/SpecScribe/SiteGenerator.cs`, `src/SpecScribe/Commands.cs`, `src/SpecScribe/ConsoleUi.cs`,
  `src/SpecScribe/SiteSettings.cs`, `src/SpecScribe/ForgeOptions.cs`
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`, `SiteGeneratorSpaTests.cs`, `SiteGeneratorCodeInsightsTests.cs`
- 19 test files re-pointed by the templater substitution: `AboutTemplaterTests.cs`, `CodeFileTemplaterTests.cs`,
  `CodeMapTemplaterTests.cs`, `CommitDayTemplaterTests.cs`, `CommitDetailTemplaterTests.cs`,
  `DeepAnalyticsTemplaterTests.cs`, `DeferredWorkParserTests.cs`, `DiagnosticsTemplaterTests.cs`,
  `FollowUpGroupPagesTests.cs`, `FollowUpSurfacesTests.cs`, `GitInsightsTemplaterTests.cs`,
  `HtmlTemplaterTests.cs`, `IdeasTests.cs`, `RequirementsAndProgressTests.cs`, `RetroTests.cs`,
  `RiskQuadrantTemplaterTests.cs`, `SprintTemplaterTests.cs`, `TimelineTemplaterTests.cs`, `WorkGraphTests.cs`
- `web/package.json`, `web/vitest.config.ts`, `web/scripts/measure-parity.mjs`
- `.github/workflows/build-test-analyze.yml`
- `docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md`,
  `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md`, `docs/adrs/README.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`, this story file

### Change Log

| Date | Change |
| --- | --- |
| 2026-07-30 | Task 1: pinned-corpus drift gate; owner decision **D5** supersedes D2 after D2's shape was measured vacuous. |
| 2026-07-31 | Task 2: IR unconditional; `--spa` retired as a deprecated no-op. |
| 2026-07-31 | Task 3: `NuxtPrerender.cs` drives the prerender (ADR 0022 §Decision 3, first execution); Node prerequisite implemented and verified three ways. |
| 2026-07-31 | Task 5: all six dependents closed; row 6's re-point corrected after a test caught a silent surface-set expansion. |
| 2026-07-31 | Task 4 (partial): 268 templater call sites + 21 chrome assertions re-pointed; ~206 disk reads outstanding. |
| 2026-07-31 | Task 7: ADR 0034 authored; ADR 0022/0033 ratification proposed; `deferred-work.md` action cleared. |
| 2026-07-31 | **CI fix**: workflow step order inverted (install+artefact BEFORE generate) with `SPECSCRIBE_PACKAGE_BUILD=1` breaking the install/IR cycle; `ir-content.css` regenerated; `foldBuildAssets` added so `pageSha` survives a rebuild on another machine. |
| 2026-08-02 | Task 4 COMPLETE: 209 disk reads + 48 path holders + 21 directory walks + 44 `SitePath` calls re-pointed at the IR; six link-checker copies consolidated; two vacuous assertions found and removed. |
| 2026-08-02 | ⚠️ **The IR now carries site-level `chrome`** (`SpaDelivery.ManifestChrome`). The renderer was SCRAPING the generated `index.html` for the favicon, cache-bust and boot script — a seventh dependent of the deletion, failing silently via a swallowed exception. |
| 2026-08-02 | ⚠️ **Three live regressions fixed**: the Nuxt renderer had no mermaid, no relationship-graph boot/engine and no TOC tracker, so all three had been broken on the shipped portal since Task 3. New `chromeNeeds()` + `web/test/chrome-needs.test.ts`. |
| 2026-08-02 | `check:parity` re-pinned with the C# lineage re-verified live 24/24 — done deliberately BEFORE the deletion, the last moment it was possible. |
| 2026-08-02 | **CI fix #2**: `publish-docs-live-pages.yml` had no Node/artefact steps at all — survivable before Task 6, an empty publish after it. Mirrored the proven sequence; added a rendered-page-count gate and `web/**` to the paths trigger. |
| 2026-08-02 | **ADR 0034 amended**: Decision 6 (the IR carries site-level chrome) + the `readGoldenChrome` hazard + the corrected 3.7 ms/route figure; `docs/adrs/README.md` updated. Still Proposed. |
| 2026-08-03 | Task 6 CLOSED: all 47 red tests resolved. **Three real bugs found doing it** — the `epics/` wipe was load-bearing (placeholder routes survived a story leaving the plan), test reads were locking the generator out of its own chunk files, and `WriteStaticPages` had become a switch that silently did nothing. The sanctioned-divergence registry is now empty, its whole chrome axis having lost its subject. `GoldenOutputInventory` re-pinned as files + routes. |
| 2026-08-03 | Task 8 PARTIAL: page-relative rewrite verified across depths 0–5 (1,240 refs), all four chrome fixes verified at depth, escaped-prose guard confirmed on a real page, all web gates green. The VISUAL browser pass remains. |
| 2026-08-02 | **Task 6: the writer is DELETED.** `HtmlRenderAdapter.Render`, 38 wrappers, all five write paths, `_spaCapture`, the five `SpaDelivery` slicers, `CapturedNavMarkup`, `RegionCompositionDeltas` and the two proofs. Both projects compile; 47 tests still read the disk and are red. |
