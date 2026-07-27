---
baseline_commit: 32fd282
---

# Story 23.4: Migrate Remaining Surfaces + Retire the C# HtmlRenderAdapter for Content

Status: blocked

<!-- BLOCKED ON STORY 23.5 (packaging reconciliation, `ready-for-dev`). Owner decision D1, 2026-07-27.
     epics.md:3940/:3942–3950 — "23.4 retires the C# renderer irreversibly and must not start before that is
     settled." Do NOT flip this to `ready-for-dev` because the story file looks complete. The gate is 23.5's
     verdict, and specifically its answer to Q2 (what the standalone binary does when Node is absent), because
     AC #6 here deletes the fallback that question is currently answered by. -->

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

7. **Given** owner decision **D4** — *Story 22.3 is retired; 23.4 is the answer*
   **When** this story lands
   **Then** `epics.md` **and** `sprint-status.yaml` record 22.3's retirement **in the same change**
   (CLAUDE.md — a structural change recorded in only one artifact is a drift bug), naming 23.4 as its
   replacement, and 22.4's "SPA + webview as IR consumers" scope is restated against AC #3's surviving region
   path so the two stories do not contradict each other.

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

> **Do not start.** This story is `blocked` on Story 23.5 (owner decision D1). Task 0 is the gate.

- [ ] **Task 0 — Confirm the gate is open** (AC: #3, #6)
  - [ ] 23.5 is `done`/`review` with its packaging strategy **recorded**, and its **Q2** answered: what the
        standalone self-contained binary does when Node is absent. This story deletes the C# HTML writer,
        which is the only current answer to that question — if Q2 resolved to "degrade to the C# renderer",
        **stop and escalate**; that answer and this story are mutually exclusive.
  - [ ] Re-read `23-5-…md`'s Dev Agent Record for what its **two-IR experiment** (its AC #4) actually found.
        If one prebuilt `.output/` could **not** render two different projects' IRs, the delivery model for a
        Nuxt-rendered site is unsettled and AC #3 must not proceed.
  - [ ] Re-read this file's Dev Notes end-to-end. Every `web/` and `src/` fact here was measured at
        `32fd282` + uncommitted work and **will have moved** — 23.5 alone changes `nuxt.config.ts`,
        `package.json` and possibly the Nuxt major (Nuxt 3 EOL **2026-07-31**).

- [ ] **Task 1 — Build the true surface inventory** (AC: #1, #8)
  - [ ] `dotnet run --project src/SpecScribe -- generate --spa --deep-git` into `SpecScribeOutput/` (the
        default — **never** `--output docs/live`). Generate the **static** site in the same run: it is the
        parity oracle, and after this story it is the last one you can produce.
  - [ ] **Verify the deep-git surfaces are actually present** before trusting the count — `git-insights.html`,
        `deep-analytics.html`, `impact-map.html`, `commit/*.html`. If absent, the 3,000 ms `GitMetrics` budget
        ate them at `errors=0`; raise the budget for the run (or fix it and say so) rather than proceeding on
        a partial inventory.
  - [ ] Produce the family table: path shape → count → owning C# templater → migration verdict. The
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

  - [ ] ⚠️ `code-map.html` is the manifest's **one declared `oversizedPages` entry at 6,758,631 B**. It is a
        single page bigger than the entire rest of its chunk. Plan for it explicitly — do not discover it
        when a harness or a prerender hangs.

- [ ] **Task 2 — Stand up the C# region-composition path BEFORE removing anything** (AC: #3)
  - [ ] Add a region-render seam that composes `navMarkup + wayfinding + <main …>…</main>` **directly from
        `PageView`** — the same concatenation `Render` does at
        [HtmlRenderAdapter.cs:31–51](src/SpecScribe/HtmlRenderAdapter.cs:31) minus `RenderHeadOpen`,
        `RenderFooter`, the script tags and `</body></html>`. `WebviewRenderAdapter.RenderContent` is already
        almost exactly this shape — **read it and reuse it**; do not write a third region composer.
  - [ ] Prove the new path emits **byte-identical regions** to today's `ExtractContentRegion` slice for all
        1,046+ pages before deleting the slice. This is a strictly mechanical equality check and it is the
        only thing standing between you and a silently-degraded IR.
  - [ ] ⚠️ The captured path and the family path are **not** equivalent today and 23.3 was bitten by exactly
        this: `ExtractContentRegion` slices from **inside** the `page-wayfinding` wrapper for the 853 captured
        pages but the 187 family pages carry the whole band (23-3-…md Debug Log #6). Whatever you build must
        emit **one** shape, and `web/ir/adapter.ts`'s two-shape split logic should then **shrink**, not grow.
        If it grows, the region path is not actually unified.
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
        ratification is the owner's. Update `docs/adrs/README.md` in the same change. Confirm the next free
        number by listing `docs/adrs/` (0018 is the highest at seeding; **0017 and 0018 are both Proposed** —
        expect contention on `README.md`).
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
9. **Nuxt 3 EOL is 2026-07-31.** 23.5 owns that decision (its Task 1 / Q1). Inherit its answer; do not make a
   second one here.

### Concurrency — this is a live tree (CLAUDE.md § Concurrent work on shared main)

At seeding (`32fd282` + working tree): `src/SpecScribe/Charts.cs` and `HierarchyExplorer.cs` modified,
`sprint-status.yaml` and `18-3-…md` modified. **Epic 20 is mid-flight around exactly the assets this story
touches** — 20.6 `review`, **20.7 / 20.8 / 20.9 `ready-for-dev`**; 20.7 deletes the three legacy arc renderers
and 20.9 finishes the rollout. Epic 18 is in flight in `src/`. So `specscribe.js`, `specscribe.css` and
`HierarchyExplorer.cs` **will move under you** — which is why the runtime assets are **copied through a gated
script**, and why you re-run the copy before verifying.

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
- [Story 23.5 — packaging](23-5-packaging-reconciliation-node-build-step.md) — **the gate.** Its two-IR
  experiment (AC #4), the build-time↔runtime adjudication (AC #5), and Q2 (the standalone binary without
  Node), which this story's AC #3 depends on.
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

Saved from analysis. None blocks the story being written; each changes what "done" looks like.

- **Q1 — What does `specscribe generate` do on a machine without Node, after this story?** 23.5's Q2 in the
  form this story cares about. Today the C# writer is the answer; AC #3 deletes it. If 23.5 answers "degrade
  to the C# renderer," that answer and this story cannot both hold — and that is a sequencing decision with a
  shelf life, not a technical one.
- **Q2 — Does the webview eventually consume the Nuxt output, or stay on the C# region path forever?** AC #3
  keeps it on the region path and Story 22.4 nominally owns the move. If it never moves, the "one renderer"
  claim is true for the *site* and not for the *product* — worth saying out loud in the ADR rather than
  leaving implied.
- **Q3 — Is a prose-styling stylesheet authored in `web/` acceptable as the D3 end state?** Dev Notes → **The
  D2/D3 tension** argues yes, on provenance grounds. If you want the harder line (nothing injected at all,
  prose decomposed into components), that requires structured per-family data in the IR and this story grows
  by an Epic 22 dependency.
- **Q4 — Story 22.4's scope after 22.3 retires.** 22.4 says "retire the duplicate, non-IR data paths for SPA
  and webview." AC #3 deliberately keeps one region composer feeding all three. Restating 22.4 is in Task 8;
  confirm the restatement is what you want rather than a quiet reinterpretation.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-27 | Story 23.4 created (baseline `32fd282`), seeded **`blocked`** on Story 23.5. Four owner decisions locked: (D1) seed now but blocked; (D2) "retire the `HtmlRenderAdapter`" means C# stops **writing** `.html` while keeping a **region-composition** path that feeds the IR and the webview/SPA; (D3) **full componentization** of the remaining 857 pages with `ir-content.css` retired to empty per ADR 0018; (D4) **Story 22.3 is retired** and 23.4 is the answer to "who renders static HTML from the IR." ACs 3–8 extend the epic's two. Three structural findings drove the shape: the IR for 82 % of the site is **produced by the code this story retires** (`ExtractContentRegion` slices the C# renderer's own full-page output), so the region path must be stood up and proven byte-equal **before** any deletion; D2 and D3 are in apparent tension and are reconciled on **provenance** (after C# stops writing pages, `specscribe.css` serves only the webview/SPA, so `web/` owning authored styles completes rather than reverses 23.2's decision); and the owed **ADR 0005 CSP amendment is probably now documentation-only**, because 23.3's `noScripts: true` removed the hydration that 23.1's `'strict-dynamic'` finding was about. `GoldenContentFingerprint` **inverts** here — 23.3 asserted it stationary; this story must move or retire it, by design. |
