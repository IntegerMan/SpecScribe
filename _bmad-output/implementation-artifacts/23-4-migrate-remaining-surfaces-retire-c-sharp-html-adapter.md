---
baseline_commit: 32fd282
---

# Story 23.4: Migrate Remaining Surfaces + Retire the C# HtmlRenderAdapter for Content

Status: in-progress

<!-- UNBLOCKED 2026-07-27 (Story 23.5 dev-story). The packaging gate this story was seeded `blocked` on is
     SETTLED by ADR 0022 (Proposed): Node is a build/CI-time toolchain AND a generate-time runtime; the shipped
     artefact is a project-independent 3.78 MB prebuilt `.output/` proven to render a DIFFERENT project's IR;
     the standalone binary takes a DOCUMENTED NODE PREREQUISITE (owner decision) rather than degrading to the
     C# renderer. That is the answer this story needed — see `23-5-packaging-strategy-report.md`.
     ⚠️ ONE GATE REMAINS AND IT IS NEW: Story 22.4 (`ready-for-dev`) runs BEFORE this story by owner decision
     (2026-07-27, epics.md § Story 22.4 D2). See Task 0. -->

<!-- Status history: seeded `blocked` 2026-07-27 (owner decision D1) → `ready-for-dev` same day once 23.5
     landed. Revisited 2026-07-28: the file was still saying `blocked` while `sprint-status.yaml` said
     `ready-for-dev` — reconciled here, and the stale premises below were re-measured rather than assumed. -->

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer completing the presentation-layer migration,
I want every remaining surface rendered by real Vue components over the IR, the transitional monolith-derived
style layer retired, and the C# renderer's page-writing role ended — with C# keeping exactly the region
composition the IR itself is built from,
so that SpecScribe has **one** renderer for its HTML and no drift hazard between two templating systems.

## Acceptance Criteria

_ACs 1–2 are the epic's stated ACs (epics.md §Story 23.4, :4037–4047). ACs 3–8 are the concrete scope this
story is seeded with: the four owner decisions locked at create-story (Dev Notes → **Owner decisions**), the
23.1 spike gate's assignment of the ADR 0005 CSP amendment (23-1-spike-report.md:403), and ADR 0018's stated
retirement condition._

1. **Given** the 857 pages currently served by `PassThroughSurface` — explicitly *not* a migration claim
   ([PassThroughSurface.vue:3](web/components/surfaces/PassThroughSurface.vue:3))
   **When** each remaining surface family is migrated to a real Vue component
   **Then** every one achieves **parity — byte-identical or documented-equivalent** with its pre-migration
   golden baseline, proven by extending 23.3's committed `npm run measure:parity` from **189 pages to all
   1,046**, with a per-family table and **every** non-zero delta enumerated with its cause and attributed
   (migration defect vs. inherited capture defect). No sampling; if runtime forces a bound, `log()` what was
   dropped — silent truncation reads as "covered everything."

2. **Given** migration is complete
   **When** the C# `HtmlRenderAdapter` is retired for content rendering
   **Then** charts render through the Epic 20 Hierarchy Explorer component from IR chart data and the
   server-rendered text twin continues to be emitted per
   [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — verified on the surfaces that
   actually carry an explorer, with the failure path (bundle blocked ⇒ fallback SVG + text twin, unchanged)
   re-proven the way 23.3 proved it.

3. **Given** owner decision **D2** — *C# stops WRITING `.html`; it still composes regions for the IR*
   **When** the retirement lands
   **Then** `HtmlRenderAdapter.Render`'s **full-page composition** (head + nav + wayfinding + body + footer +
   `</body></html>`, [HtmlRenderAdapter.cs:27–69](src/SpecScribe/HtmlRenderAdapter.cs:27)) is gone and no C#
   code path writes a content `.html` file, **while** a region-composition path (nav + wayfinding +
   `<main>…</main>`) survives and is what the IR is built from — replacing today's
   `ExtractContentRegion(fullPageHtml, navMarkup)` slice-out-of-a-full-page
   ([SiteGenerator.cs:2912](src/SpecScribe/SiteGenerator.cs:2912),
   [:3110](src/SpecScribe/SiteGenerator.cs:3110)). The webview and SPA surfaces
   (`WebviewRenderAdapter`, `JsonSpaRenderAdapter`) keep working through that same region path — they are
   **not** Nuxt consumers and this story does not make them one (that is Story 22.4).
   ⚠️ **Read Dev Notes → The circularity before writing any code.** Retiring the page render without first
   standing up the region path removes the IR's own producer for 857 pages.

4. **Given** owner decision **D3** — *full componentization; retire `ir-content.css` to empty* — and
   [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) §Decision 4 ("that list **is** the
   surface Story 23.4 has to retire … when it is empty, the layer and its gate are deleted")
   **When** this story completes
   **Then** `web/assets/ir-content.manifest.json` is **empty and the layer plus `npm run check:ir-content`
   are deleted** — *or* the residue is **enumerated rule-by-rule with a named blocker per rule** and an
   owner-visible count, and the layer's doc comment plus ADR 0018 are amended to say what the residue is
   waiting on. A shrunken-but-unexplained manifest fails this AC.
   ⚠️ **This AC and AC #3 are in tension. Read Dev Notes → The D2/D3 tension first** — it names the only
   shape that satisfies both, and the escalation if it does not hold.

5. **Given** Nuxt now writes every page and no C# `.html` is emitted
   **When** the suite runs
   **Then** `GoldenContentFingerprint` **has moved or been retired by design** — this story is the **inverse**
   of 23.3's scope guard, where a stationary fingerprint was the assertion. State in the record which it is,
   why, and what replaces it as the content-drift gate for the C#-side region output (a fingerprint over the
   IR is the obvious candidate and does not exist yet). A fingerprint quietly re-blessed with no stated reason
   fails this AC. Confirm any new hash is **stable across two repeated runs** and say whose concurrent changes
   it sat on top of.

6. **Given** ADR 0012 §Decision 5 — the ADR 0005 CSP amendment "must be landed **once**, not twice" — and the
   23.1 spike gate assigning it to this story (23-1-spike-report.md:403, :486)
   **When** the CSP posture is settled
   **Then** **one** ADR 0005 amendment is authored and proposed (not two, not buried in a story note), and it
   records the **measured** position rather than the seeded one. ⚠️ The two inputs now disagree and the
   disagreement is probably resolved in your favour — see Dev Notes → **The CSP amendment may be
   documentation-only**. Re-measure before writing. If a policy-string change *is* required, it is a
   **two-knob atomic edit** (`'strict-dynamic'` **+** payload extraction off) with a regression test asserting
   **content survival**, because the half-applied fix is catastrophically worse than none (148 SVGs → **0**,
   23-1-spike-report.md:195–216).

7. **Given** owner decision **D4** — *Story 22.3 is retired; 23.4 is the answer* — ✅ **already discharged**:
   22.3's retirement and 22.4's restatement were both recorded in `epics.md` **and** `sprint-status.yaml` at
   create-story, and Story 22.4's own owner decision D2 (*22.4 runs before 23.4*) closes the restatement
   question from the other side
   **When** this story lands
   **Then** what remains is **this story's own** structural bookkeeping in both artifacts in the same change:
   its AC drift (ACs 3–8 extend the epic's two), the fate of the `ir-content.css` layer and ADR 0018, and the
   fate of `GoldenContentFingerprint` — plus a statement of what, if anything, is left of Epic 22's
   `22-5`/`22-6` premises once the C# writer is gone.

8. **Given** the story's own surface inventory must be complete before "all remaining surfaces" can mean
   anything
   **When** the inventory is built
   **Then** it is built from a **`--deep-git` generation**, not a default one, and the count is stated.
   ⚠️ A default generate emits **1,046** IR pages and is **missing `git-insights.html`,
   `deep-analytics.html`, `impact-map.html` and the `commit/{hash}.html` family entirely** — and the memory
   `gitmetrics-3s-timeout-silent-deep-git-loss` records that even `--deep-git` **silently drops** them at
   `errors=0` when `git log --numstat` exceeds the hard-coded 3,000 ms budget (measured 6,496 ms cold).
   A surface set derived from a quiet partial run is the single easiest way to ship this story "complete"
   while missing three whole surfaces and ~300 pages.

## Tasks / Subtasks

> **Packaging gate: CLEARED.** One gate remains — **Story 22.4 runs first.** Task 0 records both.

- [x] **Task 0 — Confirm the gate is open, and read what 23.5 and 22.4 hand you** (AC: #3, #6)
  - [x] ✅ **Packaging is settled (2026-07-27, ADR 0022 Proposed).** Node is a build/CI-time toolchain **and**
        a generate-time runtime; the shipped artefact is a **project-independent 3.78 MB prebuilt `.output/`**,
        proven against a second project's IR (**1056/1056** this repo, **32/33** CORA) at **~4 ms/route**; the
        standalone binary takes a **documented Node prerequisite** — it detects Node at startup and fails with
        an actionable error, it does **not** bundle a runtime and does **not** degrade to the C# renderer.
        The 23.1 gate's "client-rendered SPA *or* Node at run time" binary was **false**: build needs native
        `.node` bindings, the shipped artefact contains **zero**. Read
        [`23-5-packaging-strategy-report.md`](23-5-packaging-strategy-report.md) before starting.
  - [x] ✅ **Confirmed 2026-07-28: Story 22.4 is at `review`** — gate satisfied. ⚠️ **Story 22.4 (`ready-for-dev`) runs BEFORE this story** — owner decision D2, 2026-07-27
        (epics.md § Story 22.4). It collapses `BuildSpaBundle` and `RenderWebviewSurfaces` into **one region
        seam** (the slicers **survive**; 22.4 retires the *duplicate*, not the *slice*), so **23.4 inherits one
        region producer instead of two** and the circularity below is answered in advance. Confirm 22.4 is at
        least `review` before starting Task 2 — starting first means doing the unification twice.
  - [x] ✅ **Both re-checked, both landed.** `wayfindingRepaired` + `stillUnbalanced` are **gone** from
        `web/ir/adapter.ts`; the 46-delta convergence is recorded in 22.4's own AC #5 (root-caused to
        `ResolveDeferredModel` passing an empty `_docs.Values` to `FollowUpRefs.BuildHrefMap`, and it reached
        further than 23.3 reported — 9 epic pages + `work-graph.html` too). 22.4 also lands two things Task 2 would otherwise inherit as defects: the **46-delta** convergence
        (root-caused to `RenderEpicsPages` building follow-up geometry from `ResolveFollowUpWork(files)` while
        `_docs` is still empty) and the **two region shapes** — a one-marker fix in
        `HtmlRenderAdapter.RenderWayfinding`, which then **deletes** `web/ir/adapter.ts`'s `wayfindingRepaired`
        + `stillUnbalanced` throw. Re-check both are done rather than assuming.
  - [ ] **Read the retired Story 22.3 file — it is a 50 KB spec for Task 2, deliberately kept.**
        [`22-3-static-html-rendered-from-the-ir.md`](22-3-static-html-rendered-from-the-ir.md) characterizes
        exactly the region path this story stands up: the **25-templater migration inventory**, the
        **`NavLocalContext` blocker**, **eight traps** (each resolved at its own create-story so they are not
        re-derived under time pressure), the ADR constraint table and a **ranked test-gate map**. It is
        retired as a *story*, not as *analysis*.
  - [ ] Re-read this file's Dev Notes end-to-end and re-measure. Facts already known to have moved since
        seeding are flagged inline with **↻**.

- [x] **Task 1 — Build the true surface inventory** (AC: #1, #8) — **DONE.** 1,408 IR pages / 1,409 `.html`;
      family table + both `oversizedPages` entries in the Dev Agent Record.
  - [x] `dotnet run --project src/SpecScribe -- generate --spa --deep-git` into `SpecScribeOutput/` (the
        default — **never** `--output docs/live`). Generate the **static** site in the same run: it is the
        parity oracle, and after this story it is the last one you can produce.
  - [x] ✅ **All present** — `git-insights.html`, `deep-analytics.html`, `impact-map.html`, 300 `commit/*.html`.
        Budget measured at 2.42 s warm vs the 3,000 ms cap, so it cleared by ~580 ms; the hazard is real but did
        not fire. **Verify the deep-git surfaces are actually present** before trusting the count — `git-insights.html`,
        `deep-analytics.html`, `impact-map.html`, `commit/*.html`. If absent, the 3,000 ms `GitMetrics` budget
        ate them at `errors=0`; raise the budget for the run (or fix it and say so) rather than proceeding on
        a partial inventory.
  - [x] ✅ **Produced — see Dev Agent Record → Task 1.** ⚠️ The seeded table below is a **default**-generate
        baseline and is superseded on every row (the real total is 1,408, not 1,046). Produce the family table:
        path shape → count → owning C# templater → migration verdict. The
        default-generate baseline for comparison (measured 2026-07-27, 1,046 pages):

        | family | pages | today | owning C# code |
        | --- | --- | --- | --- |
        | `index.html`, `epics.html`, `epics/**` | 189 | **migrated (23.3)** | `HtmlRenderAdapter.Dashboard/.Epics`, `EpicsTemplater` |
        | root insight/landing pages | 23 | pass-through | ~15 distinct `*Templater.cs` |
        | `code/**` | 239 | pass-through | `CodeFileTemplater` |
        | `follow-ups/**` | 344 | pass-through | `FollowUpDetailTemplater`, `FollowUpGroupTemplater` |
        | `implementation-artifacts/**`, `planning-artifacts/**`, `specs/**` | 129 | pass-through | `HtmlTemplater.RenderPage` (generic doc) |
        | `requirements/**` | 80 | pass-through | requirement templater |
        | `adrs/**` | 19 | pass-through | ADR templater |
        | `commits/**` (commit-day) | 23 | pass-through | `CommitDayTemplater` |
        | **deep-git only** — `git-insights.html`, `deep-analytics.html`, `impact-map.html`, `commit/{hash}.html` | **absent from the default IR** | — | `GitInsightsTemplater`, `DeepAnalyticsTemplater`, `ImpactMapTemplater`, `CommitDetailTemplater` |

  - [x] ⚠️ **RE-MEASURED: there are now TWO oversized entries, and the known one has grown.** `code-map.html` is
        **8,012,656 B** (not 6,758,631) and **`git-insights.html` is also oversized at 2,508,588 B** — a page this
        story's text does not mention. Seeded text follows: ⚠️ `code-map.html` is the manifest's **one declared `oversizedPages` entry at 6,758,631 B**. It is a
        single page bigger than the entire rest of its chunk. Plan for it explicitly — do not discover it
        when a harness or a prerender hangs.

- [ ] **Task 2 — Stand up the C# region-composition path BEFORE removing anything** (AC: #3)
      **Templater migration COMPLETE (25/25); the capture switch is NOT — see the `ApplyReferenceLinks` finding.**
  - [x] ↻ **Work from the retired [Story 22.3 file](22-3-static-html-rendered-from-the-ir.md), not from
        scratch.** It is the spec for this task and it is already elicited: the 25-templater inventory with
        the six axes on which they actually differ (`extraHead` used **exactly once**; **12 distinct `<main>`
        class values**; 6 templaters render a pager and the rest call `RenderBreadcrumb`, which is
        byte-identical to `RenderWayfinding` with a null pager; exactly one inline body `<script>`; exactly
        one piece of content after `</main>`; four sites deliberately **not** reference-linkified), plus its
        batching advice — **migrate in reverse order of volume**, singletons first, `CodeFileTemplater`
        (~hundreds of pages) last.
  - [x] ✅ **NOT REAL — closed.** `SiteNav.ToNavigationView` already takes a `NavLocalContext?` and every templater
        already builds one, so composing from the page's own `PageView` keeps the local-context band by
        construction. All 25 templaters now thread their existing context straight through; no resolver, no
        ~8-call-site plumbing. Seeded text follows:
        ⚠️ **Do its Task 1 first: there is no `path → NavLocalContext` resolver, and this blocks everything
        else.** `SpaDelivery.ExtractNavMarkup` slices the page-local nav band out of the rendered page because
        every producer builds a `NavLocalContext` inline at render time and **discards it**. The moment the
        region comes from `RenderNavMarkup(page.Nav)` instead of a slice, `nav.ToNavigationView(path)` takes no
        local-context argument and **every migrated page silently loses its band** — regressing the 23.1
        difference #2 that Story 22.2 just fixed. Two tests fail immediately and byte-for-byte:
        `SiteGeneratorSpaTests.cs:387` and `SiteGeneratorWebviewTests.cs:547`. ~8 call sites of plumbing is
        the real cost of this story's correctness; pay it up front, not 20 templaters in.
  - [x] ✅ **Already existed — nothing new written.** `JsonSpaRenderAdapter.RenderContent(PageView)` *is* AC #3's
        composer (`RenderNavMarkup(page.Nav)` + `RenderWayfinding(...)` + `page.BodyHtml`). The whole of this
        bullet was therefore "put the remaining pages on `PageView`", now done for all 25. Seeded text follows:
        Add a region-render seam that composes `navMarkup + wayfinding + <main …>…</main>` **directly from
        `PageView`** — the same concatenation `Render` does at
        [HtmlRenderAdapter.cs:31–51](src/SpecScribe/HtmlRenderAdapter.cs:31) minus `RenderHeadOpen`,
        `RenderFooter`, the script tags and `</body></html>`. ↻ After Story 22.4 there is **one** region
        builder to extend, not two — read it and reuse it; do not write a third region composer.
  - [ ] ⛔ **BLOCKED ON A NEWLY-FOUND CONSTRAINT THE STORY DOES NOT MENTION — see Completion Notes → finding 3
        (`ApplyReferenceLinks`).** The captured page is linkified as a WHOLE DOCUMENT before the region is sliced
        out of it, so a region composed from raw `PageView.BodyHtml` would ship 1,217 pages with no FR/story/code
        links, no reference chips and no `<abbr>` expansions. Byte-equality against the slice is *unreachable*
        until that is resolved. Seeded text follows:
        Prove the new path emits **byte-identical regions** to today's `ExtractContentRegion` slice for all
        1,046+ pages before deleting the slice. This is a strictly mechanical equality check and it is the
        only thing standing between you and a silently-degraded IR.
  - [x] ✅ **Confirmed landed** — `wayfindingRepaired` + `stillUnbalanced` are gone from `web/ir/adapter.ts`.
        Seeded text follows: ↻ The two-region-shape hazard that bit 23.3 (Debug Log #6) is **Story 22.4's to fix** — a one-marker
        change in `HtmlRenderAdapter.RenderWayfinding`, which emits the `page-wayfinding` wrapper only when a
        pager renders while `ExtractContentRegion` slices from the *inner* breadcrumb. Confirm it landed:
        `web/ir/adapter.ts`'s `wayfindingRepaired` + `stillUnbalanced` throw should be **gone**. If they are
        still there, 22.4 did not finish and Task 2 will re-inherit the trap.
  - [ ] Only then: delete `HtmlRenderAdapter.Render`'s page composition and the `WriteOutput` HTML writes.
        Keep `RenderNavMarkup`, `RenderBreadcrumb`, `RenderWayfinding`, `RenderDashboardBody`,
        `RenderEpicsBody` — they feed the region.

- [ ] **Task 3 — Migrate the remaining families to real components** (AC: #1, #4)
  - [ ] One component per family under `web/components/surfaces/`, branched from
        [`pages/[...path].vue`](web/pages/%5B...path%5D.vue:49)'s existing regex ladder. Extend the ladder;
        do not add a second router.
  - [ ] Reuse 23.2's primitives — `PageShell`, `ChartPanel`, `ListRow`, `StatusBadge` — with their **real**
        props (Dev Notes → **Components available**). Inventing a prop is how the 23.3 story warned this
        goes wrong.
  - [ ] `IrSurface.vue` already owns head projection + region injection + chart boot for every family. Family
        components **wrap** it. Writing near-identical siblings to make the migration look bigger is the wrong
        kind of honesty (its own doc comment says so).
  - [ ] Order by risk, not by page count: the ~23 root insight pages first (most distinct markup, most
        chart/JS behaviour, smallest blast radius per page), the high-count prose families last (most pages,
        least variation).

- [ ] **Task 4 — Retire `ir-content.css`** (AC: #4)
  - [ ] Start from `web/assets/ir-content.manifest.json` — **906 rule entries; 898 carried rules + 4
        keyframes; 115,657 generated bytes; 265 classes used by pass-through pages that the layer does not
        cover.** That file is the worklist and it is already written.
  - [ ] For each family migrated in Task 3, move the styling it needs into the component's own
        `<style scoped>` (or a `:deep()` block for whatever markup is still injected — CONVENTIONS.md §3; a
        plain scoped rule matches nothing and fails **silently**), then re-run
        `npm run extract:ir-content` and watch the manifest shrink. The number moving is the progress signal.
  - [ ] ⚠️ **Do not hand-copy monolith rules into components.** That is ADR 0018's explicitly rejected
        alternative ("a second definition free to drift … it is not a migration, it is a rewrite"). What is
        legitimate: styling **you author** for markup **you now emit**. What is not: re-typing
        `specscribe.css` under a new selector.
  - [ ] When the manifest reaches zero: delete `assets/ir-content.css`, `assets/ir-content.manifest.json`,
        `scripts/extract-ir-content.mjs`, `scripts/check-ir-content.mjs`, `scripts/ir-content-lib.mjs`,
        `scripts/ir-content-build.mjs`, the `npm run` entries, the `nuxt.config.ts` css entry, and
        CONVENTIONS.md §10 — and mark ADR 0018 **Superseded/Retired** with the story that did it.
  - [ ] If it does not reach zero, AC #4's second branch applies: enumerate the residue **rule by rule with a
        named blocker each**, and amend ADR 0018's Consequences to state it. A number without causes is not
        an enumeration.

- [ ] **Task 5 — Extend the harnesses to the whole site** (AC: #1, #2)
  - [ ] `measure-parity.mjs` from 189 → all pages. It already compares three ways (golden / IR / Nuxt) on
        purpose — keep that, because a single golden-vs-Nuxt number cannot tell a migration defect from an
        inherited capture defect. ⚠️ After this story **there is no golden side to compare against** on the
        next run: capture the oracle from Task 1 and **commit it** (or commit its per-page hashes) before
        deleting the C# writer.
  - [ ] `check-links.mjs` and `check-a11y.mjs` already walk the whole emitted site (1,053 / 1,051 pages).
        Re-run; the bar is 23.3's numbers — **zero link regressions vs. the golden site**, zero a11y failures.
        The link harness gates on the **difference**, not the absolute count, because 499 links dangle on the
        golden site too. Keep it that way.
  - [ ] Re-run `measure:payload`. ⚠️ 23.5's Dev Notes flag this harness as fragile
        (`measure-payload.mjs:39` charges the whole shared `__nuxt_island/` dir to variant B; every size
        lookup ends `?? 0`, so a missing route prints `0.00x` and reads as "free"). Re-check the harness
        before re-citing its numbers.
  - [ ] Preserve the structural win: IR routes ship `noScripts: true`, so there are **zero `_payload.json`
        files and zero Nuxt `<script>` tags** across the IR route space. Do not undo it — and note AC #6
        depends on it.

- [ ] **Task 6 — Settle the CSP posture and land ONE ADR 0005 amendment** (AC: #6)
  - [ ] **Re-measure before writing.** The two inputs disagree: ADR 0012's addendum records "**no relaxation
        of the policy string is required**" (:204–205) for the portal's Plotly boot, while 23.1 measured that
        Nuxt **hydration** needs `'strict-dynamic'` + payload extraction off (:219–228). 23.3 then shipped
        `noScripts: true` — **there is no hydration on IR routes at all.** The likely truth is that the
        amendment is now documentation-only. Prove it, don't assume it.
  - [ ] Note the boundary the spike itself declared: its CSP verdict is for the **policy string** under
        **header** delivery over an **HTTP-served** asset graph — not `<meta>` delivery, not
        `vscode-resource:`, not an Electron paint (23-1-spike-report.md:239–245, :482). "Two lines wide" is a
        **lower bound**, and the webview is not a Nuxt consumer in this story anyway (AC #3).
  - [ ] Author **one** ADR 0005 amendment covering both owed changes (ADR 0012 §Decision 5 + this story's).
        House form: Status/Context/Decision/Consequences/Ratified-decisions. Leave it **Proposed** —
        ratification is the owner's. Update `docs/adrs/README.md` in the same change. ↻ **The next
        uncontested number is 0023**: 0017/0018/0020/0021/0022 all exist, **0019 is claimed-but-unwritten by
        Story 18.3**, and several are still `Proposed`. Re-list `docs/adrs/` before claiming a number and
        expect contention on `README.md`.
  - [ ] ↻ **ADR 0022 is a DIFFERENT ADR and deliberately does not touch CSP.** Do not fold the CSP amendment
        into it or treat it as having discharged this obligation — 23.5 was explicit about the separation.
  - [ ] If a policy-string change **is** required: land both knobs in one edit and add a regression test
        asserting **content survives** (SVG/element count), not merely that the page loads. The half-applied
        fix blanked the page.

- [ ] **Task 7 — Test-suite and fingerprint reconciliation** (AC: #5)
  - [ ] **11 test files reference `HtmlRenderAdapter`** and 13 touch it or the parity harnesses:
        `HtmlRenderAdapterTests`, `RenderParityTests`, `RenderSectionParityTests`, `RenderSpaParityTests`,
        `RenderViewModelTests`, `SiteGeneratorAdapterTests`, `SiteNavTests`, `WebviewRenderAdapterTests`,
        `PathUtilTests`, `ChangeSurfaceTests`, `RequirementLocalContextTests`. Triage each: **re-aimed at the
        region path** (most of them), or **deleted with a stated reason**. A deleted assertion with no reason
        is lost coverage disguised as cleanup.
  - [ ] `GoldenContentFingerprint`
        ([SiteGeneratorAdapterTests.cs:237](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:237)) fingerprints
        **every output file**. With no `.html` output it is either retired or re-aimed at the IR. Decide,
        state it in the test's own comment block (that comment is a running log of every deliberate
        regeneration — continue it), and confirm across **two repeated runs**.
  - [ ] ↻ **Do not cite a hash from memory — it moved four times in two days.** The constant is at
        `SiteGeneratorAdapterTests.cs:1242` and the log above it records the chain
        `126eed3a… → 3171cf5c… → 06788c0f… → 2bd1c18e… → f4a7cbac…` across Stories 20.6/20.7/20.8, a code
        review and 18.5. Read the current value; do not reuse one quoted in a sibling story.
  - [ ] ↻ **The golden fixture generates WITHOUT `--spa`**, so an IR-region change alone cannot move the
        fingerprint. That cuts both ways: it means Task 2's region work is *not* covered by this gate, so the
        byte-equality proof in Task 2 is the only thing checking it — and it means a hash that moves during
        Task 2 is telling you the page render changed, which it must not until Task 2 is finished.
  - [ ] `GoldenOutputInventory` pins the output **file set**. It will change wholesale. Same treatment.
  - [ ] Expect **one rotating file-write-contention flake per full run** (23.3 recorded six in one run, all
        green in isolation). Report it honestly rather than as a clean pass.

- [ ] **Task 8 — Record the structural changes** (AC: #7)
  - [ ] Retire **Story 22.3** in `epics.md` **and** `sprint-status.yaml` in the **same change**, naming 23.4
        as its replacement (owner decision D4, 2026-07-27).
  - [ ] Restate **Story 22.4**'s scope against AC #3's surviving region path, so "retire the duplicate,
        non-IR data paths for SPA and webview" does not read as contradicting a region composer this story
        deliberately keeps.
  - [ ] Record this story's own AC drift (ACs 3–8 extend the epic's two) in both artifacts, exactly as 23.3
        did.
  - [ ] Update `web/CONVENTIONS.md`: §10 (the `ir-content.css` layer) is deleted or rewritten as residue;
        add the family-component pattern and the C#-region contract.

- [ ] **Task 9 — Live browser verification** (AC: #1, #2, #4 — CLAUDE.md § Verification)
  - [ ] Serve the prerendered output via `.claude/launch.json` entries (23.3 added `web-prerender-23-3`,
        `golden-23-3`). **Never run servers via Bash.**
  - [ ] Inspect **computed** styles and real DOM/scroll geometry, not source. The suite structurally cannot
        see containment leaks, sub-pixel collapse, or DOM corruption from markup splicing. 23.3's worst defect
        — a double-opened wrapper nesting `<main>` and `<footer>` on **187 pages** — passed *every* harness
        and was visible only as a `.page-wayfinding` measuring 5,512 px on a 22 px breadcrumb.
  - [ ] Verify the whole `styleSheets` story live after Task 4: `document.styleSheets[i].cssRules.length`,
        not by reading the source. ⚠️ Never write the `*` + `/` sequence inside a CSS comment in any generated
        or hand-authored sheet — that exact mistake silently closed a comment and killed ~1,000 rules.
  - [ ] With JS **disabled**: every family readable and navigable, charts showing fallback + text twin.
        With JS **enabled**: the Hierarchy Explorer mounts, drills and toggles shape.
  - [ ] Mobile pass at 375 px — the page body must never scroll sideways; wide content scrolls in its own
        container.

- [ ] **Task 10 — Story record**
  - [ ] Record: the full-site parity table with every delta and its cause; the link/a11y/payload numbers; the
        `ir-content` manifest count at start and end (and the residue with blockers, if any); which tests were
        re-aimed vs. deleted and why; the fingerprint decision; and the CSP measurement that the ADR cites.
  - [ ] Say plainly which C# symbols were deleted and which survived. "Retired the HtmlRenderAdapter" is not
        a finding; a list is.

## Dev Notes

### Owner decisions locked at create-story 2026-07-27 (do not re-litigate)

1. **D1 — Seed now, status `blocked`.** The story file exists so the context is captured while it is fresh.
   23.5 is the gate; see Task 0.
2. **D2 — "Retire the HtmlRenderAdapter" means C# stops WRITING `.html`.** It still composes **regions** for
   the IR. The full-page assembly dies; the region assembly lives. The webview and SPA keep consuming it.
3. **D3 — Full componentization; retire `ir-content.css` to empty.** ADR 0018's stated end state, not a
   shrink-and-declare.
4. **D4 — Story 22.3 is retired; 23.4 is the answer** to "who renders static HTML from the IR."

### ⚠️ The circularity — read this before writing any code

**For 857 of 1,046 pages the IR is produced by the very code this story retires.**

`SiteGenerator.WriteOutput` ([:3017](src/SpecScribe/SiteGenerator.cs:3017)) captures each page's **finished
HTML string** as it is written, and `SpaDelivery.ExtractContentRegion(fullPageHtml, navMarkup)`
([SpaDelivery.cs:109](src/SpecScribe/SpaDelivery.cs:109)) slices `nav + [breadcrumb] + <main>…</main>` back
out of it ([SiteGenerator.cs:2912](src/SpecScribe/SiteGenerator.cs:2912),
[:3110](src/SpecScribe/SiteGenerator.cs:3110)). Only the five dashboard/epics families are rendered from view
models directly.

So: **delete the page render first and the IR goes dark for 82 % of the site.** Task 2 exists to invert the
order — build the region path, prove byte-equality against the slice, *then* delete. There is no version of
this story where the deletion comes first.

Note also that this is *why* the epic's AC #2 reads oddly small: `HtmlRenderAdapter` is **1,730 LOC of chrome**
(plus the dashboard/epics bodies), not the ~7,000 LOC of `*Templater.cs` that produce every other page body.
The templaters are **not** retired by this story — they feed the region. D2 is the decision that makes that
explicit.

### ⚠️ The D2/D3 tension, and the only shape that satisfies both

D2 keeps C# composing **rendered region HTML** into the IR. D3 requires **no injected monolith-derived CSS**
left in `web/`. Read naively these contradict: if Nuxt injects rendered HTML, it needs rules for that markup,
and today those rules are extracted from `specscribe.css`.

They are reconcilable because **the retirement condition is about *provenance*, not about injection**. ADR 0018
retires the layer because it is *monolith-derived*, and its blast-radius argument is about a generated extract
of a 7,041-line stylesheet the project is walking away from. After AC #3, **`specscribe.css` no longer serves
any page the Nuxt site writes** — it survives only for the webview/SPA. At that point `web/` owning **authored,
scoped styles** for the markup it emits is not a reversal of 23.2's decision; it is its completion.

So the target shape is:

- family components own their chrome/structural markup and its `<style scoped>`;
- whatever remains injected (Markdig prose above all — [ADR 0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md)
  settled that the IR carries **rendered prose HTML**, and a component cannot decompose arbitrary markdown) is
  styled by an **authored, owned** prose stylesheet in `web/`, not by a generated extract;
- `ir-content.css`, its manifest and its gate are deleted because nothing is monolith-derived any more.

**Escalation, not improvisation:** if a family genuinely cannot be de-injected or re-styled without structured
per-family data in the IR, that is a **named Epic 22 ask** — write it down as AC #4's residue with that
blocker attached and raise it. Do **not** silently keep the generated layer alive, and do **not** unilaterally
pull Epic 22 scope into this story.

### ⚠️ The CSP amendment is probably documentation-only now

Three facts, in order:

1. ADR 0012's spike addendum: "**no relaxation of the policy string is required**" (:204–205) — measured, for
   the portal's Plotly/hierarchy boot under the webview CSP.
2. Story 23.1: Nuxt **hydration** does not survive the webview CSP; the gap is `'strict-dynamic'` **plus**
   `experimental.payloadExtraction: false` (:219–228). Half-applying it **blanked the page** — 148 SVGs → 0
   (:195–216).
3. Story 23.3 shipped `routeRules: { '/**': { noScripts: true } }`. **IR routes carry no Nuxt runtime and no
   hydration payload at all** — zero `_payload.json`, zero Nuxt `<script>` across 1,046 routes.

(3) removes (2)'s premise. And the webview does not consume Nuxt output in this story anyway (AC #3 keeps it
on the region path). Re-measure, then write the amendment to say what is actually true. If the answer is "no
policy-string change is required," that is a **stronger** ADR, not a missing one — the amendment still has to
land, because ADR 0005's "the body carries no scripts of its own" clause is contradicted by the vendored
Plotly bundle regardless of Nuxt.

### ↻ What moved between seeding and 2026-07-28 — re-measured, not assumed

| was seeded as | is now |
| --- | --- |
| 23.5 `ready-for-dev`, the blocking gate | **`review`.** ADR 0022 settles packaging; the standalone binary takes a **documented Node prerequisite** (it detects Node and fails with an actionable error — it does not degrade to the C# renderer). **Q1 and Q2 in this file are answered.** |
| Nuxt 3, EOL 2026-07-31, undecided | ↻ **Nuxt `^4.5.1`**, and `engines.node` is now pinned (`^22.19.0 \|\| ^24.11.0 \|\| >=26.0.0`). 23.5 absorbed the major. The "Nuxt 3 EOL" trap is **closed**. |
| "zero tests under `web/`" | ↻ **False now.** `web/test/` + `vitest.config.ts` + `coverage/` exist (`harness-lib`, `ir-content-lib`, `region-split`, `relative-prefix`, `tokens-lib`). New `web/` code is under a coverage gate — write tests with it, do not discover this at the Sonar gate. |
| no packaging script | ↻ `npm run build:package` (`scripts/build-package.mjs`) exists. |
| 22.4 `backlog` | ↻ **`ready-for-dev`, and it runs BEFORE this story** (owner D2). It unifies the two region builders, fixes the 46-delta and the two-region-shape trap. |
| 22.3 `backlog`, a competing story | ↻ **`retired`** — and its 50 KB file is **kept as the spec for Task 2**. |
| ADR 0018 highest; 0017/0018 Proposed | ↻ **0020/0021/0022 also exist; 0019 is claimed-unwritten by 18.3; next uncontested is 0023.** |
| Epic 20: 20.6 `review`, 20.7–20.9 `ready-for-dev` | ↻ **20.6 `done`; 20.7/20.8/20.9 all `review`.** The explorer rollout is essentially complete, so `specscribe.js`/`.css` are steadier than at seeding — but three stories in `review` still move under you. |

**↻ Two things this story now inherits from 23.5, both named by 23.5 rather than patched by it:**

1. **`web/components/surfaces/DashboardSurface.vue` hard-throws on any project whose dashboard carries no
   Hierarchy Explorer.** That is a genuine **project-independence defect** — it is the one thing that broke
   in the two-IR experiment (CORA rendered 32/33). 23.5 attributed it to Story 23.3 and left it open. **This
   story touches that surface in Task 3**, so it is cheapest to fix here; if you do, say so, because 23.5's
   open-items table still points at 23.3.
2. **The ADR 0005 CSP amendment is still this story's, and ADR 0022 deliberately does not touch CSP.** It
   must land **once** (ADR 0012 §Decision 5). See below — it is probably documentation-only.

### The fingerprint flips meaning (AC #5)

Story 23.3's AC #8 made a **stationary** `GoldenContentFingerprint` the assertion — a moved hash meant the
story had leaked into the C# renderer. **This story is the opposite.** Deleting the HTML writer changes the
output file set wholesale; the hash must move or the test must go. Both `GoldenContentFingerprint` and
`GoldenOutputInventory` live in
[`SiteGeneratorAdapterTests.cs`](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:237); the fingerprint
test's comment block is a running log of every deliberate regeneration — **continue it, do not replace it**.

⚠️ Memory `golden-diff-normalization-gotchas` and Story 25.1: the hash has been **checkout- and
date/TZ-dependent**. And per CLAUDE.md it moves under concurrent sessions routinely. Confirm across two
repeated runs and name whose uncommitted work it sat on.

### What already exists — reuse it, do not rebuild it

**From Story 23.3 (all committed, all working):**

| thing | where | note |
| --- | --- | --- |
| the one IR adapter | `web/ir/adapter.ts` | only file that knows emitter-side names; `#ir` alias; `SPECSCRIBE_IR_DIR` |
| neutral types | `web/ir/types.ts` | `region.{navHtml,wayfindingHtml,mainAttrs,mainInnerHtml}`, `head`, `needsPrism`, `needsHierarchyEngine`, `hasExecutableIsland` |
| the single catch-all | `web/pages/[...path].vue` | regex ladder → surface component. **Extend it.** |
| shared surface | `web/components/surfaces/IrSurface.vue` | head projection + region injection + chart boot, once |
| injection primitives | `web/components/IrHtml.ts`, `IrMain.ts` | no-wrapper injection; the `<main>` landmark |
| harnesses | `measure-parity`, `check-links`, `check-a11y`, `check-tokens`, `check-ir-content`, `sync:assets`, `measure-payload` | all `npm run`; output committed under `web/measurements/` |
| Nitro workarounds | `server/plugins/no-payload-for-ir-routes.ts`, `report-render-errors.ts` | keep both; see 23.3 Debug Log 1 and 5 |

**Components from 23.2 — real APIs, do not invent props:**

- `PageShell` — `title`, `subtitle?`, `brand?`, `chrome: 'full' | 'nav-only'`; slots default, `nav`, `footer`.
  Under `chrome="nav-only"` it **yields** `<main>` to the injected region.
- `StatusBadge` — `stage` (`pending|drafted|ready|active|review|done|deferred|retired|unrecognized`), `label`
  (**required** — UX-DR17 enforced by shape), `meaning?`. Carries no stage→word map by design.
- `ChartPanel` — `title`, `window?`, `ranking?`, `note?`, `why?`; slots default, `legend`. Order
  head → ranking → note → body → why, matching `Charts.Framed`.
- `ListRow` — `summary`, `accent?`, `chips?`, `primaryHref?`, `primaryLabel?`, `resolved?`; slot `badge`.

**Non-negotiable conventions** (`web/CONVENTIONS.md`): §1 tokens are generated — never hand-edit;
§3 injected content needs `:deep()` (a plain scoped rule matches nothing and fails **silently**);
§4 build-time module-scope data — **not** `useAsyncData` (1.36×), **not** `<NuxtIsland>` (1.99×);
§8 routes are the IR's paths verbatim and **no href is ever rewritten** ([ADR 0017](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md));
§11 runtime assets are **copied, never forked** (ADR 0012 §Decision 2 — one Hierarchy Explorer implementation);
§12 IR-backed routes ship no Nuxt runtime.

### Traps, in roughly the order you will hit them

1. **`code-map.html` is 6.76 MB** — the manifest's one declared `oversizedPages` entry. Componentizing it,
   parity-diffing it, and prerendering it are each a different kind of expensive.
2. **`v-html` never executes `<script>`.** Nothing executable reaches a page through IR content. Inert
   `type="application/json"` data islands survive as DOM data, which is what the explorer reads. `IrSurface`
   already throws on an executable island rather than shipping a page that quietly does nothing — keep that.
3. **The anti-flash boot marker is chrome-level**, deliberately excluded from the IR region
   ([HierarchyExplorer.cs:514](src/SpecScribe/HierarchyExplorer.cs:514)). `IrSurface` re-emits it from the
   head. If you touch head projection, re-verify it or the fallback SVG paints and is then swapped.
4. **`crawlLinks: false` is load-bearing, not a preference.** Nitro's crawler follows links inside injected
   IR content and aborts the build on the first 404. The route table comes from the manifest.
5. **Routes carry `.html` verbatim**, which is why payload extraction collides (`EEXIST … mkdir
   '…/about-sdd-bmad.html'`) and why `no-payload-for-ir-routes.ts` exists. Do not "fix" it by disabling
   `payloadExtraction` globally — that would make 23.2's AC #4 measurement unreproducible.
6. **`web/` cannot move away from `src/`.** `web/scripts/tokens-lib.mjs:15-17` resolves
   `../../src/SpecScribe/assets/specscribe.css` by relative path.
7. **No new npm dependencies.** `web/` runs on `nuxt` + `vue` + `vue-router` + the vendored
   `plotly-hierarchy.min.js`. A CSS parser or link checker from npm would break a deliberate zero-dep posture
   (ADR 0010). Write harnesses against Node built-ins and plain string work, as the existing ones do.
8. **NFR citation hazard.** Epic 23 cites "NFR6" throughout meaning the **PRD's NFR-5** (progressive
   enhancement). `epics.md`'s own NFR6 is a different requirement. The collision is recorded and
   **unresolved** (epics.md:123–134). Cite it as "the PRD's NFR-5, cited as NFR6 throughout Epic 23 per the
   recorded collision."
9. ~~**Nuxt 3 EOL is 2026-07-31.**~~ ↻ **CLOSED** — 23.5 upgraded to Nuxt `^4.5.1` and pinned `engines.node`.
   Do not re-open it; do verify the version you are building against matches `package.json`.

### Concurrency — this is a live tree (CLAUDE.md § Concurrent work on shared main)

↻ **Re-checked 2026-07-28 at `811ba17`.** The tree is quieter than at seeding (only `sprint-status.yaml`
modified), but four sibling stories sit in `review` and can still be patched under you: **20.7 / 20.8 / 20.9**
(the explorer rollout — `specscribe.js`, `specscribe.css`, `HierarchyExplorer.cs`) and **22.2 / 23.2 / 23.3 /
23.5 / 25.2**. Commit `c1a6ee5` ("Land concurrent story work: 18.4, 18.5, 20.8, 23.5, 25.3 + ADRs 0021/0022")
is the shape to expect: **one commit carrying five stories** — so scope any review by this story's File List
and declared symbols, never a commit range.

Runtime assets are still **copied through a gated script** rather than forked, for exactly this reason;
re-run `npm run sync:assets` before verifying.

- **Verify after every edit.** Grep for the symbol you just added before relying on it. A `Charts.cs` edit has
  silently vanished this way. ⚠️ A zero-grep can also be a **transient mid-write read** — confirm with
  `git diff HEAD` before re-applying.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** Another session's uncommitted work is in
  the tree. This has already destroyed real work mid-story.
- Expect commits to bundle sibling stories; scope any review by this story's **File List and declared
  symbols**, never a commit range.

### Verification

- Generate to `SpecScribeOutput/` (the default). **Never** `--output docs/live` — vestigial and gitignored.
- Verify in a **live browser**, JS-on and JS-off, inspecting computed styles and real geometry.
- Every chart needs an accessible text equivalent; no state may be signalled by color alone (UX-DR17).
- ⚠️ 23.3 could take **no screenshots** (the Browser pane was not compositing). If that recurs, say so — a
  measured DOM/CSSOM value is not an image, and the owner's verify-and-iterate pass is the gate for
  appearance.

### Project Structure Notes

- **New — `web/`:** one surface component per remaining family under `components/surfaces/`; an authored prose
  stylesheet if the D3 shape needs one.
- **Update — `web/`:** `pages/[...path].vue` (the branch ladder), `scripts/measure-parity.mjs` (whole site),
  `nuxt.config.ts` (css entries), `package.json`, `CONVENTIONS.md`, `README.md`.
- **Delete — `web/`:** `assets/ir-content.css`, `assets/ir-content.manifest.json`,
  `scripts/{extract-ir-content,check-ir-content,ir-content-lib,ir-content-build}.mjs`, `check:ir-content`,
  CONVENTIONS.md §10 — **conditional on AC #4 reaching empty**.
- **Update — `src/SpecScribe/`:** `HtmlRenderAdapter.cs` (page composition removed, region composition added),
  `SiteGenerator.cs` (`WriteOutput` no longer writes content HTML; capture replaced by direct region build),
  `SpaDelivery.cs` (`ExtractContentRegion` retired once the region path proves equal), `HtmlTemplater.cs`.
- **Update — `tests/`:** the 11 files naming `HtmlRenderAdapter`, plus `SiteGeneratorAdapterTests`'
  fingerprint/inventory pair.
- **Update — repo:** `docs/adrs/` (the ADR 0005 amendment; ADR 0018 retired/superseded; `README.md`),
  `epics.md` + `sprint-status.yaml` (22.3 retirement, 22.4 restatement, this story's AC drift).
- **Unchanged:** `extension/**`, `spike/**`, `tools/**`.

### References

- [Epic 23 + Story 23.4 ACs](../planning-artifacts/epics.md) — §Story 23.4 at :4031–4053; the execution-order
  and blocking note at :3940–3950; **Story 22.3 at :3832–3853 (retired by owner decision D4)**; Story 22.4 at
  :3855–3876; the NFR collision at :123–134.
- [Story 23.3 — baseline surfaces](23-3-migrate-baseline-surfaces-dashboard-epics.md) — **the pattern this
  story extends.** Its Debug Log's six defects (especially #6, the double-wrapped band no harness saw), the
  parity/link/a11y numbers that are this story's bar, and the named head-projection gaps handed to Epic 22.
- [Story 23.5 — packaging](23-5-packaging-reconciliation-node-build-step.md) and its
  [**packaging strategy report**](23-5-packaging-strategy-report.md) — ↻ **the gate, now cleared.** The
  two-IR result (1056/1056 + 32/33 at ~4 ms/route), the channel table, the documented Node prerequisite for
  the standalone binary, the closed door on embedding a JS engine, and the **open-items table** whose row 1
  this story inherits. [**ADR 0022**](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)
  is its decision record — and it deliberately does **not** touch CSP.
- ↻ [**Story 22.3 (RETIRED, kept as reference)**](22-3-static-html-rendered-from-the-ir.md) — **the spec for
  Task 2.** The `NavLocalContext` blocker, the 25-templater migration inventory and its six axes of
  variation, eight pre-resolved traps, the ADR constraint table, and the ranked test-gate map.
- ↻ [Story 22.4 — SPA + webview as IR consumers](22-4-spa-and-webview-as-ir-consumers.md) — **runs before
  this story.** One region seam, the 46-delta convergence, and the one-marker `RenderWayfinding` fix that
  deletes `web/ir/adapter.ts`'s `wayfindingRepaired` + `stillUnbalanced` throw.
- [Story 23.2](23-2-component-library-and-design-token-bridge.md) — the primitives, the token bridge and its
  both-directions drift proof, the payload measurement (and its fragile harness).
- [Story 23.1 spike report](23-1-spike-report.md) — Axis 3 (the CSP matrix, :173–228), findings 5/8/9, and the
  §Gate row for 23.4 at :403.
- [web/CONVENTIONS.md](../../web/CONVENTIONS.md) — §§1, 3, 4, 8, 9, 10, 11, 12.
- ADRs: [0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) §4 (the CSP clause this story
  amends); [0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) (Vue + Nuxt 3,
  universal/SSR — ⚠️ its §Charts clause is **stale**, amended by ADR 0013 §5 with no marker in 0009's body);
  [0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
  §Decision 2 (one Hierarchy Explorer), §Decision 5 (**the shared CSP amendment, landed once**), addendum
  :204–205 (no policy relaxation measured);
  [0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) (the text twin is the no-JS contract);
  [0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md) (**the IR carries rendered prose HTML** — the
  reason full componentization of prose is not free);
  [0017](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md) (routes ARE the IR's paths);
  [0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) (**the layer this story retires**).
- C# seams: [`HtmlRenderAdapter.Render`](src/SpecScribe/HtmlRenderAdapter.cs:27) (page composition — the
  target); [`SpaDelivery.ExtractContentRegion`](src/SpecScribe/SpaDelivery.cs:109) (the slice being replaced);
  [`SiteGenerator.WriteOutput`](src/SpecScribe/SiteGenerator.cs:3017) (the capture seam);
  `WebviewRenderAdapter.RenderContent` (the region composer to reuse);
  [`PageView`](src/SpecScribe/PageView.cs:38) (the host-neutral page model);
  [`SiteGeneratorAdapterTests.cs:237`](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:237)
  (`GoldenContentFingerprint`).
- [CLAUDE.md](../../CLAUDE.md) — concurrent-work rules, the ADR-proposal trigger, live-browser verification,
  and "a structural change recorded in only one artifact is a drift bug."
- Memory: `story-23-3-baseline-surfaces-done`, `story-23-5-packaging-reconciliation-seeded`,
  `story-23-2-component-library-token-bridge-done`, `story-22-2-canonical-ir-done`,
  **`gitmetrics-3s-timeout-silent-deep-git-loss`** (AC #8's hazard),
  `css-comment-star-slash-silent-truncation`, `golden-diff-normalization-gotchas`,
  `shared-main-concurrent-edit-loss-verify-after-edit`, `generate-output-dir-is-specscribeoutput`,
  `cite-adrs-by-symbol-not-line-number`.

### Questions for the owner

Saved from analysis. ↻ Two of the four were answered by Story 23.5 and Story 22.4 between seeding and
2026-07-28.

- ~~**Q1 — What does `specscribe generate` do on a machine without Node?**~~ ✅ **ANSWERED (ADR 0022, owner
  decision 2026-07-27):** it requires Node as a **documented prerequisite** — detect at startup, fail with an
  actionable error naming the supported range. It does not bundle a runtime and does not degrade to the C#
  renderer. Cost stated plainly by 23.5: **a user without Node cannot generate at all** once this story
  lands. Node detection itself is **Story 16.3's** open item, not this story's.
- ~~**Q4 — Story 22.4's scope after 22.3 retires.**~~ ✅ **ANSWERED:** 22.4 runs **before** 23.4 and retires
  the *duplicate builder*, not the *slice*. Recorded in `epics.md` § Story 22.4 and on its sprint-status key.
- **Q2 — Does the webview eventually consume the Nuxt output, or stay on the C# region path forever?** Still
  open, and now sharper: after 22.4 there is exactly **one** region producer, and AC #3 keeps the webview on
  it. If it never moves to Nuxt, the "one renderer" claim is true for the *site* and not for the *product* —
  worth saying out loud in the ADR 0005 amendment rather than leaving implied.
- **Q3 — Is a prose-styling stylesheet authored in `web/` acceptable as the D3 end state?** Still open. Dev
  Notes → **The D2/D3 tension** argues yes, on provenance grounds. The harder line (nothing injected at all,
  prose decomposed into components) requires structured per-family data in the IR and grows this story by an
  Epic 22 dependency.
- **Q5 — new.** `DashboardSurface.vue`'s hard-throw is a **project-independence** defect 23.5 attributed to
  Story 23.3 and left open (its open-items table, row 1). This story touches that surface anyway. Fix it here
  and re-home the open item, or leave it for a 23.3 patch round?

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, dev-story workflow), 2026-07-28. Story baseline `32fd282` (preserved); run against
working tree at `b696485`.

### Debug Log References

- **Both gates confirmed open before any code.** Story 22.4 is at `review`; its region seam
  (`BuildSurfacePrelude` / `BuildFamilySurfaces` / `CapturedRegions` / `BuildOutline`) is in `SiteGenerator.cs`,
  and `wayfindingRepaired` + `stillUnbalanced` are **gone** from `web/ir/adapter.ts` — so the two-region-shape
  trap is fixed and Task 2 inherits ONE region producer. ADR 0022 settles packaging.
- **A zero-grep really can be a transient mid-write read.** Immediately after editing four templaters, a grep
  reported two of them reverted to their pre-edit shape (`BuildPage=0`, `RenderHeadOpen=1`) while `git diff
  --stat` simultaneously showed all four modified at the expected size. Re-reading two seconds later showed all
  four edits intact. Nothing was re-applied and nothing was reset — per
  `shared-main-concurrent-edit-loss-verify-after-edit`, the correct response to a zero-grep is to re-confirm,
  not to re-write. Re-writing here would have duplicated the edit.
- **Session 2 (2026-07-28, run at `755bd7a`): the "verify in isolation" technique that made byte-proof possible
  under a live concurrent session.** A sibling agent was mid-flight on Story 24.1 (`CoupledFile`,
  `DirectedCouple`, `GitMetrics`, `Charts`) and Story 8.9 (`StatusStyles`), and it edited **inside**
  `DeepAnalyticsTemplater.BuildPage` and `CodeFileTemplater` while this story was migrating them. Two consequences:
  (1) the main tree's build broke twice in files this story never touched — the correct response was to keep to
  non-colliding files and re-check, never to "fix" or revert theirs; (2) a golden-fingerprint result in the main
  tree could no longer attribute a moved hash. **The fix: two throwaway clones in the scratchpad** — `base`
  (pristine `755bd7a`) and `iso` (`755bd7a` + ONLY this story's files, with the sibling's `DirectedCoupling` edit
  hand-excluded from the copied `DeepAnalyticsTemplater`). The golden gate then answers about *this* story alone.
  Nothing in `C:\Dev\SpecScribe` was reset, checked out or cleaned.
- **⚠️ `--no-build` on a tree whose TEST project does not compile silently runs the STALE dll.** Mid-session the
  main tree's `dotnet test --no-build` reported `11 passed / 1 failed` while `dotnet build` was failing with 5
  `CS1503`s in the sibling's `CodeFileTemplaterTests`/`DeepAnalyticsTemplaterTests`. That "failure" was a stale
  binary, not a result. This is the `golden-diff-normalization-gotchas` stale-build trap wearing a different hat:
  **always read the build's error count before trusting a `--no-build` test run.**
- **The full-suite deep-git failures are ENVIRONMENTAL, and the baseline is far worse than this story's tree.**
  Measured back-to-back on the same box: pristine `755bd7a` fails **18** tests; the same tree plus this story's
  migrations fails **3** — a strict subset, and the failing set *changes between runs* (one run's 3 all passed on
  re-run of the identical filter, 17/17). Every failure is a missing `commit/` directory, i.e. deep-git produced
  nothing: `GitMetrics`' hard-coded **3,000 ms** budget losing to `git log --numstat` under parallel test load.
  This is `gitmetrics-3s-timeout-silent-deep-git-loss` and AC #8's hazard firing in the TEST harness rather than
  in a generate. Report it as a flake honestly; it is not this story's regression and this story does not fix it.
- **The golden fingerprint is the right gate for Task 2 and must stay STATIONARY until it finishes.** It is the
  inverse of AC #5's end state: while templaters are being moved onto `PageView`, a moved hash means the page
  render changed, which it must not. It has not moved. ⚠️ Its current constant is **`e384cbde…`**
  (`SiteGeneratorAdapterTests.cs`, Story 20.7 code-review regeneration, 2026-07-28) — the story file's
  `f4a7cbac…` was **already stale**, the fourth consecutive story to record a stale value. Read it, never quote it.

### Completion Notes List

**Status after session 2 (2026-07-28, run at `755bd7a`): Tasks 0 and 1 complete; Task 2's TEMPLATER MIGRATION is
COMPLETE (25/25) and byte-proven; Task 2's CAPTURE SWITCH is blocked on a newly-found constraint (finding 3).
Tasks 3–10 not started.** Reported honestly rather than as a finished story — see the scope note at the end.

**Task 0 — gates.** Both open (see Debug Log). Read 23.5's packaging report, 22.4's delivery record, and the
retired 22.3 file. ⚠️ **22.3's headline blocker is no longer true as stated.** It says "there is no `path →
NavLocalContext` resolver … ~8 call sites of plumbing is the real cost of this story's correctness." That is only
true for the path that *re-derives* nav from `nav.ToNavigationView(path)`. `SiteNav.ToNavigationView` **already
takes a `NavLocalContext?`**, and every templater already builds one and passes it to `RenderNavBar`. Composing
the region from the page's **own `PageView`** therefore keeps the local-context band by construction — the
plumbing is not needed, because the value is not discarded, it is simply not currently *retained past the render*.
This removes Task 2's declared long pole.

**Task 1 — the true surface inventory, from a `--deep-git --spa` generation.**

`dotnet run --project src/SpecScribe -- generate --spa --deep-git` into `SpecScribeOutput/` (the default).
Reported `generated=741 … errors=0 elapsed_ms=65108`; that counter is not the page count — **1,409 `.html` files
on disk, 1,408 IR pages** in `spa/manifest.json` (the 1,409th is `app.html`, the SPA shell, which is not an IR page).

⚠️ **AC #8's hazard did not fire, but it is real.** All three deep-git-only surfaces are PRESENT
(`git-insights.html`, `deep-analytics.html`, `impact-map.html`) plus **300** `commit/{hash}.html` pages. Measured
`git log --numstat -n 2000` at **2.42 s warm** against the hard-coded **3,000 ms** `GitMetrics.Timeout`
(`GitMetrics.cs:197`, used at `:1171`) — i.e. this run cleared the budget by ~580 ms, and the 6,496 ms cold
measurement in `gitmetrics-3s-timeout-silent-deep-git-loss` remains entirely plausible. **The inventory below is
from a verified-complete run**, but the budget is still a silent-loss hazard for any future run and is not fixed
by this story.

*The real family table (measured, 1,408 IR pages):*

| family | pages | today | owning C# code |
| --- | --- | --- | --- |
| `index.html` + `epics.html` + `epics/**` | **191** | migrated (23.3) | `HtmlRenderAdapter.Dashboard/.Epics`, `EpicsTemplater` |
| `follow-ups/**` | **376** | pass-through | `FollowUpDetailTemplater`, `FollowUpGroupTemplater` |
| `commit/**` (per-commit, deep-git only) | **300** | pass-through | `CommitDetailTemplater` |
| `code/**` | **254** | pass-through | `CodeFileTemplater` |
| `implementation-artifacts/**` | **112** | pass-through | `HtmlTemplater.RenderPage` (generic doc) |
| `requirements/**` | **80** | pass-through | `RequirementsTemplater` |
| root single-page | **26** | pass-through | ~18 distinct `*Templater.cs` |
| `commits/**` (commit-day) | **25** | pass-through | `CommitDayTemplater` |
| `adrs/**` | **24** | pass-through | `HtmlTemplater.RenderPage` + ADR local context |
| `planning-artifacts/**` | **15** | pass-through | `HtmlTemplater.RenderPage` |
| `specs/**` | **5** | pass-through | `HtmlTemplater.RenderPage` |
| **total** | **1,408** | **191 migrated / 1,217 pass-through** | |

The story's seeded table was a **default**-generate baseline (1,046) and is superseded on every row.

⚠️ **`oversizedPages` now has TWO entries, not one.** `code-map.html` at **8,012,656 B** (the story's figure was
6,758,631 B — it has grown ~19 %) **and `git-insights.html` at 2,508,588 B**, which the story does not mention at
all. Both must be planned for in Tasks 3/5/9, not discovered when a harness hangs.

**Task 2 — the region path. The declared design is wrong in a way that makes the task much smaller.**

AC #3 asks for "a region-composition path (nav + wayfinding + `<main>…</main>`)". **That path already exists and
is already the IR's composer for the 191 family pages:** `JsonSpaRenderAdapter.RenderContent(PageView)` is
literally `RenderNavMarkup(page.Nav) + RenderWayfinding(path, breadcrumb, pager) + page.BodyHtml`. Nothing new
needs to be written. The whole of Task 2 is therefore: **put the remaining 1,217 pages on `PageView`**, then point
the capture at `RenderContent` instead of `SpaDelivery.ExtractContentRegion`.

Only **5** call sites currently go through `HtmlRenderAdapter.Shared.Render(PageView)` (4 in `EpicsTemplater`, 1
in `HtmlTemplater`). The other ~25 templaters hand-compose `RenderHeadOpen → nav → breadcrumb → <main> → footer →
</body></html>` inline. The migration per templater is mechanical and byte-provable:

- `RenderPage(...)` becomes `HtmlRenderAdapter.Shared.Render(BuildPage(...)).Content` — one line;
- `BuildPage(...)` returns a `PageView` whose `BodyHtml` is **everything the templater emitted between the
  wayfinding band and the footer**;
- head/nav/breadcrumb/footer/`</body>` are deleted, because `Render` already emits exactly those bytes.

**14 of ~25 migrated, every one byte-identical** (golden fingerprint + golden inventory green after each batch,
rebuilt first to avoid the stale-build hash trap; **full suite 2,674 passed / 0 failed**, matching the pre-story
baseline exactly — no regressions and, unusually, no contention flake this run): `RiskQuadrantTemplater`,
`TraceabilityTemplater`, `WorkGraphTemplater`, `CadenceTemplater`, `HowToReadTemplater`, `AboutTemplater`,
`DesignSystemTemplater`, `DiagnosticsTemplater`, `ActionItemsTemplater`, `TimelineTemplater`,
`SprintTemplater`, `DeferredWorkTemplater`, `ImpactMapTemplater`, `CodeMapTemplater`.

**`AssetManifest` had to be decomposed, and the reason is a third finding.** `HierarchyEngineNeeded` was doing
two jobs, and **three** hierarchy shapes ship today:

| page | boot marker | engine `<script src>` |
| --- | --- | --- |
| dashboard / epics families | **inline**, between wayfinding and body | yes |
| `impact-map.html` (Story 21.3's newer convention) | **in `<head>`**, via `extraHead` | yes |
| `code-map.html` | **none at all** | yes |

One flag cannot express three shapes, and collapsing them would have moved bytes on pages this story must leave
untouched. `AssetManifest` now carries `HierarchyBootInline` (pre-body placement) and `ExtraHead` (verbatim head
additions) alongside `HierarchyEngineNeeded` (the engine script only). `ExtraHead` is also the field
`CodeFileTemplater`'s Prism stylesheet needs — the story flagged `extraHead` as "used exactly once", and it is now
used twice by construction. Both placements stay OUTSIDE the IR region either way, which is why `IrSurface.vue`
re-emits the marker from the head (Trap 3).

**↻ SESSION 2 — the remaining 11 are DONE. All 25 templaters are now on `PageView`.** Migrated this session, each
as `RenderX(...) => HtmlRenderAdapter.Shared.Render(BuildX(...)).Content` with the head/nav/wayfinding/footer/
`</body>` string-building DELETED because `Render` already emits exactly those bytes:

| templater | entry points added | pages | note |
| --- | --- | --- | --- |
| `DeepAnalyticsTemplater` | `BuildPage` | 1 | body extends PAST `</main>` (the lightbox — finding 2) |
| `GitInsightsTemplater` | `BuildPage` | 1 | engine via `HierarchyEngineNeeded`, computed from the rendered body; no boot marker |
| `RetroTemplater` | `BuildIndexPage`, `BuildPage` | 1 + n | index body starts at the doc-header (finding 1); detail carries a `Pager` |
| `AboutSddTemplater` | `BuildHubPage`, `BuildFrameworkPage` | 7 | `Begin`/`End` now pass a private `SddPage` record; bodies start at the doc-header |
| `IdeasTemplater` | `BuildListPage`, `BuildDetailPage` | n | — |
| `TestArtifactsTemplater` | `BuildListPage` | 1 | — |
| `CommitDayTemplater` | `BuildPage` | 25 | `Pager` + caller's `NavLocalContext` |
| `CommitDetailTemplater` | `BuildPage` | 300 | `Pager` + caller's `NavLocalContext` |
| `FollowUpDetailTemplater` | `BuildActionPage`, `BuildDeferredPage` | 376 | `AppendShellOpen`/`AppendShellClose` replaced by one private `ComposePage` |
| `FollowUpGroupTemplater` | `BuildPage` | — | body starts at the doc-header (finding 1) |
| `RequirementsTemplater` | `BuildIndexPage`, `BuildRequirementPage` | 80 | index passes NO description ⇒ `MetaDescription` stays **null** so `RenderHeadOpen`'s title fallback emits the same byte |
| `HtmlTemplater` | `BuildDocPage` | 156 | the generic doc path — `adrs/`, `implementation-artifacts/`, `planning-artifacts/`, `specs/`, `readme.html` |
| `CodeFileTemplater` | `BuildPage`, `BuildPlaceholderPage` | 254 | `Begin`/`End` now pass a private `CodeShell` record; Prism rides `AssetManifest.ExtraHead` |

**How byte-identity was proven, and its ONE stated gap.** In the isolated `iso` clone (see Debug Log) the golden
fingerprint + golden inventory pass with 12 of the 13 applied — that is a clean attribution to this story alone.
In the main tree, with all 13 applied on top of the sibling session's work, **85/85** targeted tests pass:
`SiteGeneratorAdapterTests` (both golden gates), `CodeFileTemplaterTests`, `RenderParityTests`,
`RenderSpaParityTests`, `WebviewRenderAdapterTests`.
⚠️ **The gap, stated rather than glossed:** the golden fixture **cites no real repo files, so it emits no code
page at all** (its own comment block says so) — therefore the golden hash does **not** cover
`CodeFileTemplater`'s 254 pages. `CodeFileTemplaterTests` passing is a markup gate, not a byte gate. A real
`--deep-git --spa` generate diffed against a captured oracle is what would close it, and that is Task 5's oracle
capture; it has NOT been run for this batch.

**⚠️ FINDING 3 — the capture switch cannot be a straight swap: `ApplyReferenceLinks` runs on the WHOLE DOCUMENT.**
This is the session's most important result and the story does not mention it anywhere. Every `WriteOutput` call
site passes `ApplyReferenceLinks(SomeTemplater.RenderPage(...), path)` — head + nav + wayfinding + body + footer —
and `SpaDelivery.ExtractContentRegion` then slices the region out of that **already-linkified** page. So today's
IR regions carry FR/story/code links, `[[wiki-link]]`/assumption chips and first-use `<abbr>` expansions.
Composing the region from raw `PageView.BodyHtml` would ship **1,217 pages with all of that silently gone** — the
same invisible-to-every-harness class as 23.3's double-wrapped band, and strictly worse in blast radius.
It is not merely "linkify the body instead", because two of the five passes are **document-scoped, not
body-scoped**: `AbbreviationExpander.Expand` is explicitly **first-use** (`Story 10.3`), so whether a term is
expanded depends on whether it already appeared in the nav or breadcrumb; and `RequirementLinkifier` /
`StoryEpicLinkifier` take skip-ids keyed to the page's own identity. Task 2's byte-equality proof is therefore
**unreachable** until this is designed, and the honest options are (a) compose the region and then run
`ApplyReferenceLinks` over the composed region — cheap, but changes first-use scope from "document" to "region",
which is a real byte delta that must be measured and attributed, or (b) keep capturing the linkified page purely
as the equality ORACLE while the composed path is proven against it, then retire the capture. **This is a
decision the owner should see, not one to take silently** — it is the last structural unknown between here and
AC #3.

**⚠️ Two structural findings that the remaining 17 migrations depend on:**

1. **`BodyHtml` must start at `<header class="doc-header">`, not at `<main>`, on every page that emits one.**
   `about.html`, `how-to-read.html`, `design-system.html` and `diagnostics.html` all emit a doc-header *before*
   the landmark. Today's `ExtractContentRegion` slices from the **breadcrumb**, so that header is inside the
   region. A `BodyHtml` that began at `<main>` would still render the static page correctly (the golden gate
   would stay green) while **silently dropping the page's own title block from the IR** — a defect no C#-side
   test can see. This is the same class of failure as 23.3's double-wrapped band.
2. **`deep-analytics.html`'s `:target` lightbox is ALREADY MISSING from the IR.** `DeepAnalyticsTemplater` emits
   a `<div id="coupling-zoom" class="coupling-lightbox">` **after** `</main>`, and `ExtractContentRegion`
   truncates at `</main>` — so the page's "Expand" link resolves to nothing in the SPA/webview/Nuxt today. This
   is an **inherited capture defect, not a migration defect**; the composed region includes it, so Task 2 *fixes*
   it. Under AC #1 this is a documented, attributed non-zero delta — the first entry in that table.

**Two further owner decisions locked during dev-story, 2026-07-28** (the story's own open Q3 and Q5 — asked
rather than decided unilaterally, because each changes what Task 3/Task 4 actually build):

5. **D5 — AC #4 takes its FIRST branch, via an authored prose stylesheet in `web/`.** Family components own
   their chrome in `<style scoped>`; the injected Markdig prose that ADR 0016 puts in the IR is styled by a
   **hand-authored, owned** sheet in `web/` — never a generated extract of `specscribe.css`. `ir-content.css`,
   `ir-content.manifest.json`, `check:ir-content` and CONVENTIONS.md §10 are all **deleted**, and ADR 0018 is
   marked Superseded/Retired by this story. This is the shape Dev Notes → **The D2/D3 tension** argues for, on
   provenance grounds; the alternative "nothing injected at all" was rejected as pulling an Epic 22 dependency
   into this story.
6. **D6 — `DashboardSurface.vue`'s hard-throw is fixed HERE, and 23.5's open item is re-homed.** The missing
   Hierarchy Explorer degrades to a documented empty state instead of throwing, so a project whose dashboard
   carries no explorer renders (the CORA 32/33 failure). 23.5's open-items table row 1 moves from Story 23.3 to
   Story 23.4, because the record currently points at the wrong story.

**↻ Scope after session 2, stated plainly.** Task 1 is done. Task 2's **templater migration is finished — 25/25,
byte-proven** except for the stated code-page gap. What remains is: the **capture switch**, now gated on
finding 3's `ApplyReferenceLinks` decision; then ~11 Vue family components covering 1,217 pages (Task 3), the
906-rule `ir-content.css` retirement (Task 4), whole-site harnesses (Task 5), the ADR 0005 amendment (Task 6),
13 test files' triage plus the fingerprint decision (Task 7), the `epics.md`/`sprint-status.yaml` bookkeeping
(Task 8) and live-browser verification (Task 9). D5 and D6 (below) already answered the story's own Q3 and Q5,
so Task 4 is unblocked; **finding 3 is the one new question that is not**.

**Nothing has been deleted yet, and that is deliberate.** `HtmlRenderAdapter.Render`'s page composition,
`WriteOutput`'s HTML writes and `SpaDelivery.ExtractContentRegion` are all still in place and still the IR's
producer. Per the story's own circularity note there is no version of this work where the deletion comes before
the byte-equality proof — and that proof is exactly what finding 3 blocks. The site and the IR are unchanged and
fully working at this checkpoint.

### File List

- `src/SpecScribe/RiskQuadrantTemplater.cs` — `BuildPage` added; `RenderPage` reduced to the HTML projection
- `src/SpecScribe/TraceabilityTemplater.cs` — same
- `src/SpecScribe/WorkGraphTemplater.cs` — same
- `src/SpecScribe/CadenceTemplater.cs` — same
- `src/SpecScribe/HowToReadTemplater.cs` — same; body starts at the doc-header (finding 1)
- `src/SpecScribe/AboutTemplater.cs` — same; body starts at the doc-header
- `src/SpecScribe/DesignSystemTemplater.cs` — same; body starts at the doc-header
- `src/SpecScribe/DiagnosticsTemplater.cs` — same; body starts at the doc-header
- `src/SpecScribe/ActionItemsTemplater.cs` — same; body starts at the doc-header
- `src/SpecScribe/TimelineTemplater.cs` — `BuildPage` added; `RenderPage` reduced to the HTML projection
- `src/SpecScribe/SprintTemplater.cs` — `BuildIndexPage` added; `RenderIndex` reduced to the HTML projection
- `src/SpecScribe/DeferredWorkTemplater.cs` — `BuildPage` added; body starts at the doc-header
- `src/SpecScribe/ImpactMapTemplater.cs` — `BuildPage` added; boot marker moved onto `AssetManifest.ExtraHead`
- `src/SpecScribe/CodeMapTemplater.cs` — `BuildPage` added; engine-only hierarchy shape (no boot marker)
- `src/SpecScribe/AssetManifest.cs` — **`HierarchyBootInline` + `ExtraHead` added** (the three-shape split)
- `src/SpecScribe/HtmlRenderAdapter.cs` — `Render` now threads `ExtraHead` into the head and gates the inline
  boot marker on `HierarchyBootInline` rather than `HierarchyEngineNeeded`
- `src/SpecScribe/EpicsTemplater.cs` — 4 sites set `HierarchyBootInline` alongside `HierarchyEngineNeeded`
- `src/SpecScribe/HtmlTemplater.cs` — 1 site, same
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status `ready-for-dev` → `in-progress` + findings

**Session 2 (2026-07-28) — the remaining 13 templater migrations.** Each: `BuildX` returning `PageView` added;
`RenderX` reduced to `HtmlRenderAdapter.Shared.Render(BuildX(...)).Content`; head/nav/wayfinding/footer/`</body>`
string-building deleted.

- `src/SpecScribe/DeepAnalyticsTemplater.cs` — `BuildPage`; body deliberately extends past `</main>`
- `src/SpecScribe/GitInsightsTemplater.cs` — `BuildPage`; hierarchy engine moved onto `AssetManifest.HierarchyEngineNeeded`
- `src/SpecScribe/RetroTemplater.cs` — `BuildIndexPage` + `BuildPage`; index body starts at the doc-header; detail carries `Pager`
- `src/SpecScribe/AboutSddTemplater.cs` — `BuildHubPage` + `BuildFrameworkPage`; new private `SddPage` record replaces the `Begin`/`End` string pair
- `src/SpecScribe/IdeasTemplater.cs` — `BuildListPage` + `BuildDetailPage`
- `src/SpecScribe/TestArtifactsTemplater.cs` — `BuildListPage`
- `src/SpecScribe/CommitDayTemplater.cs` — `BuildPage`; `Pager` + caller's `NavLocalContext` threaded to `ToNavigationView`
- `src/SpecScribe/CommitDetailTemplater.cs` — `BuildPage`; same
- `src/SpecScribe/FollowUpDetailTemplater.cs` — `BuildActionPage` + `BuildDeferredPage`; `AppendShellOpen`/`AppendShellClose` **deleted**, replaced by one private `ComposePage`
- `src/SpecScribe/FollowUpGroupTemplater.cs` — `BuildPage`; body starts at the doc-header
- `src/SpecScribe/RequirementsTemplater.cs` — `BuildIndexPage` + `BuildRequirementPage`; index keeps a **null** `MetaDescription` on purpose
- `src/SpecScribe/HtmlTemplater.cs` — `BuildDocPage` (the generic doc path, 156 pages)
- `src/SpecScribe/CodeFileTemplater.cs` — `BuildPage` + `BuildPlaceholderPage`; new private `CodeShell` record replaces the `BeginShell`/`EndShell` string pair; Prism head moved onto `AssetManifest.ExtraHead`

**Not this story's, present in the same working tree** (sibling session, Stories 24.1 + 8.9 — listed so a review
scopes correctly per CLAUDE.md): `GitMetrics.cs`, `StatusStyles.cs`, `EpicsParser.cs`, `Charts.cs`,
`HierarchyExplorer.cs`, `HtmlRenderAdapter.Epics.cs`, `RenderParity.cs`, `assets/specscribe.css`,
`tests/SpecScribe.Tests/GitMetricsCouplingTests.cs`, `tools/analysis-digest/`. ⚠️ Two files carry **both**
sessions' work: `DeepAnalyticsTemplater.cs` (their `DirectedCoupling` ranked-pairs change landed *inside* this
story's `BuildPage`) and `CodeFileTemplater.cs`.

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-28 | **dev-story session 2 (run at `755bd7a`; baseline `32fd282` preserved). Task 2's templater migration COMPLETE — 25/25 on `PageView`** (13 migrated this session: DeepAnalytics, GitInsights, Retro×2, AboutSdd×2, Ideas×2, TestArtifacts, CommitDay, CommitDetail, FollowUpDetail×2, FollowUpGroup, Requirements×2, HtmlTemplater's generic doc path, CodeFile×2). **Byte-identity proven** — golden fingerprint + golden inventory green in an ISOLATED clone carrying only this story's files, and **85/85** targeted tests green in the main tree (both golden gates, `CodeFileTemplaterTests`, `RenderParityTests`, `RenderSpaParityTests`, `WebviewRenderAdapterTests`). ⚠️ **Stated gap:** the golden fixture emits **no code page**, so the hash does not cover `CodeFileTemplater`'s 254 pages; that needs Task 5's generate-and-diff oracle. **⚠️ FINDING 3, new and structural: the capture switch is NOT a straight swap.** `ApplyReferenceLinks` runs over the **whole document** at every `WriteOutput` call site and the region is sliced out of the *already-linkified* page — so composing from raw `PageView.BodyHtml` would ship **1,217 pages with every FR/story/code link, reference chip and `<abbr>` expansion silently gone**. And it is not simply "linkify the body": `AbbreviationExpander` is **first-use scoped across the document**, so region-scoping it is itself a measurable byte delta. Task 2's byte-equality proof is unreachable until this is decided; **raised for the owner rather than taken silently.** Two blockers from session 1 closed instead: 22.3's `NavLocalContext` plumbing is **not needed** (`ToNavigationView` already takes one, and all 25 templaters now thread their existing context through), and AC #3's region composer **already existed** (`JsonSpaRenderAdapter.RenderContent`). **Nothing deleted yet** — `Render`'s page composition, `WriteOutput`'s HTML writes and `ExtractContentRegion` are all still in place, per the story's own circularity rule. Method note: with a sibling session live in the same tree (Stories 24.1 + 8.9, editing *inside* two of this story's files), attribution was recovered with two throwaway scratch clones — pristine HEAD vs HEAD-plus-only-this-story — and **nothing in the working tree was reset, checked out or cleaned.** Also measured: the full suite's deep-git failures are **environmental, and worse on pristine HEAD (18) than with this story applied (3)** — the 3,000 ms `GitMetrics` budget losing under parallel test load, an unstable set run-to-run. And a trap worth keeping: **`--no-build` on a tree whose test project does not compile silently runs the STALE dll** — read the build's error count before trusting it. |
| 2026-07-28 | **dev-story started (baseline `b696485`); status `ready-for-dev` → `in-progress`. Tasks 0 and 1 COMPLETE, Task 2 substantially under way (8 of ~25 templaters), Tasks 3–10 not started.** Both gates verified open: 22.4 is at `review`, its region seam is in `SiteGenerator.cs`, and `wayfindingRepaired`/`stillUnbalanced` are gone from `web/ir/adapter.ts`. **Task 1's inventory replaces the seeded one wholesale: 1,408 IR pages / 1,409 `.html`, not 1,046** — all three deep-git surfaces and 300 `commit/` pages present (budget measured 2.42 s warm vs the 3,000 ms cap, so AC #8's hazard is real but did not fire), and **`oversizedPages` has TWO entries, not one**: `code-map.html` at 8,012,656 B (grown ~19 % from the story's figure) and **`git-insights.html` at 2,508,588 B**, which the story never mentions. **Task 2's declared long pole is not real.** 22.3's `NavLocalContext` blocker ("~8 call sites of plumbing is the real cost of this story's correctness") applies only to the path that re-derives nav from `nav.ToNavigationView(path)`; `ToNavigationView` **already takes a `NavLocalContext?`** and every templater already builds one, so composing the region from the page's own `PageView` keeps the local-context band by construction. And the AC #3 region composer **already exists** — `JsonSpaRenderAdapter.RenderContent(PageView)` is exactly `navMarkup + wayfinding + BodyHtml` — so Task 2 is not "write a region path", it is "put the remaining 1,217 pages on `PageView`". 8 templaters migrated byte-identically with the golden fingerprint stationary throughout (correct: it must NOT move until Task 2 ends). **Two findings the remaining migrations depend on:** (1) four pages emit `<header class="doc-header">` BEFORE `<main>`, so `BodyHtml` must start at that header — starting at `<main>` keeps the golden gate green while silently dropping the page's own title block from the IR, the same invisible-to-every-harness class as 23.3's double-wrapped band; (2) **`deep-analytics.html`'s `:target` lightbox sits after `</main>` and is therefore already missing from today's IR** — an inherited capture defect the composed region fixes, and AC #1's first documented delta. ⚠️ The golden constant is **`e384cbde…`**, not the `f4a7cbac…` this file quotes — stale for the fourth story running. |
| 2026-07-28 | **Revisited and re-measured at `811ba17`; status reconciled `blocked` → `ready-for-dev`** (the file said `blocked` while `sprint-status.yaml` had said `ready-for-dev` since 23.5 landed — a one-artifact drift, now closed). **The packaging gate is CLEARED**: ADR 0022 makes Node a build/CI-time toolchain *and* a generate-time runtime, the shipped artefact is a project-independent 3.78 MB prebuilt `.output/` proven against a second project's IR (1056/1056 + 32/33 at ~4 ms/route), and the standalone binary takes a **documented Node prerequisite** rather than degrading to the C# renderer — so **Q1 and Q2 are answered**. **One new gate replaces it: Story 22.4 runs BEFORE this story** (owner D2), unifying the two region builders, converging the 46-delta and fixing the two-region-shape trap — so Task 2 inherits one region producer, not two. **Story 22.3's retired 50 KB file is now Task 2's spec** (the `NavLocalContext` blocker — there is no `path → NavLocalContext` resolver and ~8 call sites of plumbing is the real cost of correctness — the 25-templater inventory, eight pre-resolved traps, the ranked test-gate map). Stale premises corrected: **Nuxt 3 → `^4.5.1`** with `engines.node` pinned (the EOL trap is closed), **`web/` now has a vitest suite and a coverage gate** (the "zero tests" fact is false), the **next uncontested ADR number is 0023** (0019 claimed-unwritten by 18.3; 0020–0022 exist), and the golden fingerprint has moved four times in two days to `f4a7cbac…` at `SiteGeneratorAdapterTests.cs:1242` — read it, never quote it. Two items inherited from 23.5 and named rather than patched by it: **`DashboardSurface.vue` hard-throws on any project with no Hierarchy Explorer** (the one thing that broke in the two-IR run) and **the ADR 0005 CSP amendment is still this story's** — ADR 0022 deliberately does not touch CSP. |
| 2026-07-27 | Story 23.4 created (baseline `32fd282`), seeded **`blocked`** on Story 23.5. Four owner decisions locked: (D1) seed now but blocked; (D2) "retire the `HtmlRenderAdapter`" means C# stops **writing** `.html` while keeping a **region-composition** path that feeds the IR and the webview/SPA; (D3) **full componentization** of the remaining 857 pages with `ir-content.css` retired to empty per ADR 0018; (D4) **Story 22.3 is retired** and 23.4 is the answer to "who renders static HTML from the IR." ACs 3–8 extend the epic's two. Three structural findings drove the shape: the IR for 82 % of the site is **produced by the code this story retires** (`ExtractContentRegion` slices the C# renderer's own full-page output), so the region path must be stood up and proven byte-equal **before** any deletion; D2 and D3 are in apparent tension and are reconciled on **provenance** (after C# stops writing pages, `specscribe.css` serves only the webview/SPA, so `web/` owning authored styles completes rather than reverses 23.2's decision); and the owed **ADR 0005 CSP amendment is probably now documentation-only**, because 23.3's `noScripts: true` removed the hydration that 23.1's `'strict-dynamic'` finding was about. `GoldenContentFingerprint` **inverts** here — 23.3 asserted it stationary; this story must move or retire it, by design. |
