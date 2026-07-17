---
baseline_commit: 8d9aac44fe721e35315cef0881cb04ba64b2ded9
---

# Story 9.9: Requirement Satisfaction Status at a Glance

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder or reviewer,
I want a holistic reading of requirement satisfaction status across the portal,
so that I can judge whether requirements are satisfied without assembling the picture from disconnected pages.

## Acceptance Criteria

1.
**Given** FR/NFR/UX-DR coverage data and covering-story links (Stories 9.1–9.3)
**When** the portal presents satisfaction status (dashboard and/or requirements hub surfaces)
**Then** a maintainer can answer "what is satisfied, deferred on purpose, unmapped, or in flight?" in one coherent scan
**And** status vocabulary routes through Story 8.2's canonical `StatusStyles` / `--status-*` system — no parallel words or colors.

2.
**Given** a requirement with covering stories
**When** satisfaction status renders
**Then** it reflects delivering-story lifecycle honestly (including in-progress / review, not only done-vs-not)
**And** missing coverage uses Story 9.3's deferred-vs-unmapped distinction when that story has landed (coordinate; do not re-implement the tier).

3.
**Given** this is a holistic pass over surfaces that Stories 9.1–9.3 also touch
**When** scope is planned at create-story
**Then** it does not absorb 9.1–9.3's page-level deliverables; it composes and closes journey-level gaps those stories leave
**And** empty/absent framework coverage degrades per NFR8.

## Context & Scope

Epic 9 completes the requirement → epic → story chain and the review follow-through. Stories 9.1–9.3 have **all landed** (9.1/9.2 `done`, 9.3 `review`) and built the per-requirement plumbing:

- **9.1** — FR/NFR detail pages list covering stories grouped by epic, status-badged.
- **9.2** — a "Non-functional & design coverage" section on `requirements.html`; UX-DRs became a first-class `RequirementKind.Design`; `RequirementsModel.Everything` = FR+NFR+UX-DR; second coverage source from epic-header reverse index.
- **9.3** — a real `RequirementStatus.Unmapped` tier distinct from `Planned`, threaded through `StatusStyles`, the Sankey (`FlowStateKey`), donuts, cards, and detail pages, plus best-effort deferral-source links.

**9.9 is the holistic pass that composes those outputs into one coherent reading** — it answers the four-word question a stakeholder/reviewer actually asks ("what is **satisfied**, **deferred on purpose**, **unmapped**, or **in flight**?") in a single scan, and closes the journey-level coherence gaps 9.1–9.3 left. It is **not** a rebuild of any of their page deliverables (AC #3).

### The journey-level gaps this story closes (why it exists)

Today the requirements picture is spread across fragments that each answer a *different* slice, and none answers the whole:

1. **No single satisfaction reading.** `requirements.html` has per-kind status donuts, a FR+NFR "at a glance" grid, a FR+NFR flow Sankey, and a separate NFR/UX-DR coverage section — a reader must mentally union them. There is no top-level "here is where every requirement stands" summary.
2. **The holistic scan is not actually holistic.** The "Requirements at a glance" grid and flow are scoped to `model.All` (**FR+NFR only**) — **UX-DR/Design is excluded**. `RequirementsTemplater.cs:70` (`allReqs = Functional.Concat(NonFunctional)`), grid at `:75`, flow at `:82`. So the one "at a glance" surface silently drops a whole requirement kind.
3. **The satisfaction vocabulary is not fully self-explaining.** `StatusStyles.LegendKey()` (the portal-wide status key) lists `pending, drafted, ready, active, review, done, deferred, retired, unrecognized` but **omits `Unmapped`/"Not yet mapped"** — the exact state 9.3 introduced and this scan foregrounds. `LegendStages` at `StatusStyles.cs:246–247`; meaning already exists in `StageMeaning` (`:237`) and icon in `Icons.ForStatus("unmapped")` (`Icons.cs:28`), but the legend never shows the row.
4. **Counts are recomputed per site, not sourced from the 8.3 ledger.** Requirement-status tallies are computed ad hoc in `RequirementsTemplater.StatusCounts` (`:520–526`) and `DashboardViewBuilder.RequirementStatSubLine` (`:155–169`). `ProjectCounts` (the single count ledger, Story 8.3) has **no requirement-satisfaction fields** — so Home and the hub can drift.

### Owner-selected design directions (locked at create-story)

Elicited 2026-07-16 (project rule: elicit visual intent up front for any new visual surface — memory `create-story-elicit-visual-intent`; Epic 8 retro action item #3; carried into every Epic 9 sibling). Four decisions:

1. **Placement — requirements hub owns the full scan; Home gets a compact rollup.** Add a new **"Satisfaction at a glance"** summary band at the **top of `requirements.html`** (immediately under the header, above the existing per-kind donut row) spanning **FR + NFR + UX-DR** (`model.Everything`). Add a **compact rollup** on Home (in/near the existing Requirements panel or tile band) that shows the same four-reading summary and **links into the hub band** (anchor). Do **not** put the full scan on Home (keeps golden/parity churn bounded).

2. **Form — four-reading summary: one proportional stacked bar + labeled count chips.** The scan presents **four readings**: **Satisfied · In flight · Deferred on purpose · Unmapped**. Render a **single horizontal proportional stacked bar** plus a row of **labeled count chips** (one per reading). Each chip carries color + icon + word (never color-only) and a count. The **"In flight" chip's tooltip/sub-line expands to the honest lifecycle** — Partially implemented (Active/Review) · Ready for dev · Planned — so AC #2's "not only done-vs-not" is satisfied. Chips link to the relevant on-page detail (the existing "at a glance" grid / coverage sections); a dedicated per-state filtered page is **out of scope** (would be a new surface — see boundaries).

3. **Counts source — extend the `ProjectCounts` ledger (8.3), retire the local recount.** Add requirement-satisfaction buckets to `ProjectCounts` so Home and the hub read **one source**. Retire the ad-hoc `RequirementStatSubLine` recount and (where practical) `RequirementsTemplater.StatusCounts` in favor of the ledger. Do **not** add a third parallel counter.

4. **Close two adjacent coherence gaps as part of the holistic pass:**
   - **Add the missing "Not yet mapped" (Unmapped) row to `StatusStyles.LegendKey`** so the satisfaction vocabulary is fully self-explaining.
   - **Include UX-DR/Design in the holistic reading** (the band spans `Everything`, not just `All`).

### The four-reading → six-tier mapping (locked — this is the semantic core)

The four readings are a **semantic grouping over the six canonical `RequirementStatus` tiers** — **not** a new vocabulary and **not** new colors. Every color still comes from the six `--status-*` tokens via `StatusStyles.ForRequirement`; every word still comes from `StatusStyles.RequirementLabel`. The four readings are *labels on brackets*, the bar segments are the *canonical tiers*.

| Reading (chip) | Composed of `RequirementStatus` tiers | Canonical color(s) / words |
|----------------|----------------------------------------|-----------------------------|
| **Satisfied** | `Done` | `--status-done` · "Done" |
| **In flight** | `Active` + `Ready` + `Planned` | `--status-active` / `--status-ready` / `--status-pending` · "Partially implemented" · "Ready for dev" · "Planned" |
| **Deferred on purpose** | `Deferred` | `--status-deferred` · "Deferred" |
| **Unmapped** | `Unmapped` | `--status-pending` (tan) + **unmapped icon** · "Not yet mapped" |

- **Do NOT invent a single "in-flight" color.** The stacked bar renders the underlying **six tiers** as its segments (each its own `--status-*` token) and the four readings are visual **brackets/labels** over them + the chip counts. This is the only honest way to obey "no parallel words or colors" while still presenting four readings. The "In flight" chip's count is the sum of its tiers; its tooltip breaks it back down.
- **Unmapped vs Planned stay distinguishable without color** (they share the tan `--status-pending` token per 9.3's owner decision): the Unmapped chip/segment uses the **`"unmapped"` icon + "Not yet mapped" word** (`StatusStyles.RequirementBadge` / `Icons.ForStatus("unmapped")` already do this — reuse, don't re-key).

## Tasks / Subtasks

- [ ] **Task 1 — Extend the `ProjectCounts` ledger with requirement-satisfaction buckets (AC: #1, #2, #3, counts-source decision)**
  - [ ] Add a satisfaction shape to `ProjectCounts` (e.g. a `RequirementSatisfaction` nested record or an `IReadOnlyList<StageCount>`), computed over `RequirementsModel.Everything` (FR+NFR+UX-DR). Carry **both** the six canonical tier counts (Done/Active/Ready/Planned/Unmapped/Deferred — sums to the requirement total) **and** the four-reading rollups (Satisfied/InFlight/Deferred/Unmapped) so both the bar (tiers) and the chips (readings) read from one source. Route every tier→css-class through `StatusStyles.ForRequirement`, not a local map. [Source: src/SpecScribe/ProjectCounts.cs:12–48]
  - [ ] Thread `RequirementsModel` into `ProjectCounts.Build` as an **optional** param (`RequirementsModel? requirements = null`), mirroring the existing optional `EpicsModel? epics = null`. When null/empty → empty satisfaction buckets, no throw (NFR8 graceful degradation). Keep `Build` pure and deterministic. [Source: src/SpecScribe/ProjectCounts.cs:86–141]
  - [ ] Update `ProjectCounts.Empty` and the `SiteGenerator` build site (`_counts`, ~src/SpecScribe/SiteGenerator.cs:346) to pass the parsed requirements model. **Confirm ordering:** requirements must be parsed before `ProjectCounts.Build` is called; if the current phase order builds counts before requirements, either reorder (safe — `Build` is pure) or compute the satisfaction buckets in the same pass. Do not double-parse requirements.
  - [ ] **Retire the local recount:** change `DashboardViewBuilder.RequirementStatSubLine` (and its callers `RequirementStatTile`) to read the per-kind/overall satisfaction buckets from `ProjectCounts` instead of re-`Count`ing `RequirementStatus` values. If per-kind (Functional/NonFunctional/Design) tallies are needed for the existing tiles, add them to the ledger too (keep the six-tier shape per kind). Where `RequirementsTemplater.StatusCounts`/`StatusSegments` can read the ledger without contorting the donut rendering, do so; if the per-kind donut path is cleaner keeping its local count, leave it but document why (avoid a half-migration that leaves two authorities for the same number). [Source: src/SpecScribe/DashboardViewBuilder.cs:143–169; src/SpecScribe/RequirementsTemplater.cs:520–542]

- [ ] **Task 2 — "Satisfaction at a glance" band on `requirements.html` (AC: #1, #2, #3)**
  - [ ] In `RequirementsTemplater.RenderIndex`, insert the new band **after the `<header>` (line 37) and before the donut-row `<section class="dashboard">` (line 54)**, under a `<div class="section-divider">Satisfaction at a glance</div>` inside the existing `<main id="main-content">` (do not add a second `<main>`). Give it an `id` anchor (e.g. `id="satisfaction"`) so Home's rollup can deep-link to it. [Source: src/SpecScribe/RequirementsTemplater.cs:32–65; memory `story-1-4-a11y-seams-for-1-5`]
  - [ ] Render the **proportional stacked bar** over `model.Everything` using the six canonical tiers as segments — each segment's color via `StatusStyles.ForRequirement` / `--status-*`, width proportional to count, with an `aria-label` describing the full breakdown (accessibility text-twin, never diagram-only — Story 1.4/9.3 pattern). Reuse an existing bar/segment primitive if one fits (`Charts` bar/donut helpers, `.status-*` classes); if a small new bar helper is needed, keep colors token-routed and add it beside the existing `Charts` requirement helpers.
  - [ ] Render the **four count chips** (Satisfied · In flight · Deferred on purpose · Unmapped) reading counts from the `ProjectCounts` satisfaction buckets (Task 1). Each chip pairs color + icon + word (reuse `StatusStyles.Badge`/`RequirementBadge` shape — the Unmapped chip must use the `"unmapped"` icon + "Not yet mapped", the In-flight chip a neutral/active treatment with a tooltip enumerating Active/Ready/Planned). **Never color-only.**
  - [ ] Chips/segments link to the relevant existing on-page detail (the "Requirements at a glance" grid section anchor and/or the coverage section) so the scan is a jumping-off point, never a dead end. Do **not** fabricate a per-state filtered page.
  - [ ] **NFR8:** when `model.Everything` is empty (no requirements at all), omit the band entirely (absent, not an empty bar). When a kind is empty, it simply contributes zero — the band still renders for the kinds present.
  - [ ] **Keep the existing FR+NFR donut row, "Requirements at a glance" grid, and flow Sankey byte-identical** (9.2's byte-identical guardrail). The new band is an **additive orienting summary above them** — it must not re-render or replace the grid/flow (Story 8.7 "one primary view per dataset": the band answers the *four-reading holistic* question incl. Design; the grid/flow remain the FR+NFR per-requirement detail).

- [ ] **Task 3 — Compact satisfaction rollup on Home (AC: #1, #3)**
  - [ ] Add a compact four-reading rollup to Home that reads the same `ProjectCounts` satisfaction buckets and **links to `requirements.html#satisfaction`**. Prefer extending the existing Requirements panel (`HtmlRenderAdapter.Dashboard.cs` `AppendRequirementsPanel`, ~:306–345) or the requirements tile band (`DashboardViewBuilder`, :96–110) rather than inventing a new Home panel — keep Home churn minimal (recent commits deliberately shaped Home layout; treat it as intentional). [Source: src/SpecScribe/HtmlRenderAdapter.Dashboard.cs; src/SpecScribe/DashboardViewBuilder.cs:92–110]
  - [ ] The rollup must stay on the **shared `RenderDashboardBody` path** (HTML + webview + SPA byte-aligned — no host fork, no new `HostRenderException`).
  - [ ] **NFR8:** omit the rollup when there are no requirements or no epics model (mirror the existing `requirements is not null` / `requirements.All`-count guards).

- [ ] **Task 4 — Close the legend gap: add "Not yet mapped" to the portal-wide legend (AC: #1, #2, decision #4)**
  - [ ] Add the `Unmapped`/"Not yet mapped" row to `StatusStyles.LegendStages` / `LegendKey()` (`StatusStyles.cs:246–247,272–304`). The row's **swatch reuses `--status-pending`** (tan, per 9.3's owner decision — no 7th token) but the **icon is `Icons.ForStatus("unmapped")` and the word is "Not yet mapped"**, so it is distinct from "Planned" by icon + word (never color-only). Its meaning already exists in `StageMeaning("unmapped")` (`:237`) — source the row's text from there, do not duplicate a second meaning string.
  - [ ] This closes the gap where the vocabulary this scan foregrounds was absent from the key. Verify the legend renders the row on every page that already shows `LegendKey()` (requirements index/detail, dashboard, epics, sprint, follow-ups) with no layout regression.
  - [ ] Heed the 8.2 review-deferred drift note ("LegendKey stage words are a second table beside `*Label` helpers"): route the new row through the existing `StageMeaning`/`Icons` seams; do not open a third vocabulary table.

- [ ] **Task 5 — Guardrails / do-not (AC: #3)**
  - [ ] Do **not** re-implement 9.1's covering-story lists, 9.2's NFR/UX-DR coverage section, or 9.3's `Unmapped` tier / Sankey `FlowStateKey` split — **compose** them. The band reads `RequirementStatus` (already rolled up by `DeriveStatus`) and `StatusStyles`; it introduces no new classifier or roll-up.
  - [ ] Do **not** add a 7th `--status-*` token, a new status word, or a section-local color map. Do **not** add any new authoring schema in `epics.md`.
  - [ ] Do **not** build a per-state filtered requirements page (new surface — out of scope; chips anchor to existing detail).
  - [ ] Do **not** push UX-DR/Design into the existing FR+NFR flow/grid (keeps them byte-identical) — Design enters only via the new band and the existing 9.2 coverage section.
  - [ ] Do **not** fork the shared body path or add JS (charts are pure SVG + links; webview has no `specscribe.js` — any tooltip is progressive-enhancement, the always-visible chip word/icon + legend row is the accessible channel).

- [ ] **Task 6 — Tests + golden (AC: #1, #2, #3)**
  - [ ] `ProjectCountsTests`: satisfaction buckets sum correctly over a fixture with Done/Active/Ready/Planned/Unmapped/Deferred requirements across FR+NFR+UX-DR; the four-reading rollups equal the expected tier sums (In flight = Active+Ready+Planned); empty `RequirementsModel` → empty buckets, no throw (NFR8); determinism (two builds identical).
  - [ ] `RequirementsAndProgressTests` / `HtmlRenderAdapterTests`: `RenderIndex` HTML contains the "Satisfaction at a glance" band with the four chips and a stacked bar whose segment classes route through `--status-*`; the band spans Design (a UX-DR-bearing fixture shows UX-DRs counted); the Unmapped chip reads "Not yet mapped" with the unmapped icon (not "Planned"); the In-flight chip's tooltip/sub-line enumerates the lifecycle; band absent when `Everything` is empty (NFR8). Assert the existing FR+NFR grid/flow/donut markup is unchanged (byte-identical guard where practical).
  - [ ] Dashboard tests: Home rollup renders the four readings from the ledger and links to `requirements.html#satisfaction`; absent when no requirements/epics; present on all three adapters (parity).
  - [ ] `StatusStylesTests` / `StylesheetTests`: `LegendKey()` now includes a "Not yet mapped" row (pending swatch + unmapped icon + word); assert any new band/bar CSS classes exist and route color through tokens.
  - [ ] Golden fingerprint **will move** (requirements.html + Home body) → regenerate the constant in `SiteGeneratorAdapterTests` (test `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`, ~:207; constant ~:398) per `golden-diff-normalization-gotchas`, from a clean full build (a `--no-build`/partial build produces a stale hash — see 9.1's debug log). Confirm the three parity suites green: `RenderParityTests`, `RenderSectionParityTests`, `RenderSpaParityTests`.
  - [ ] Run the full suite from repo root: `dotnet test`.

## Dev Notes

### What exists today (read before touching)

- **`RequirementsTemplater.RenderIndex`** (src/SpecScribe/RequirementsTemplater.cs:10–118) builds `requirements.html`: header + `LegendKey`, a per-kind donut row (Functional always; NFR/Design gated on count>0), a FR+NFR "Requirements at a glance" grid, a FR+NFR "Requirements flow" Sankey, the 9.2 "Non-functional & design coverage" section, a jump navigator, and grouped requirement cards. Your band goes **above the donut row, under the header**. Everything below it stays as-is.
- **`RequirementsModel`** — `Functional`, `NonFunctional`, `Design`; `All` = FR+NFR (drives the FR flow/grid — **do not widen**); `Everything` = FR+NFR+UX-DR (your band's scope). [Source: 9.2 story; src/SpecScribe/RequirementsModel.cs]
- **`RequirementStatus`** (src/SpecScribe/RequirementsModel.cs:22) = `Deferred, Unmapped, Planned, Ready, Active, Done` (least→most complete). Rolled up once in `RequirementsParser.DeriveStatus` (`:364–396`). Your band consumes this — never re-derives it.
- **`StatusStyles`** — `ForRequirement` (`:134–145`, Unmapped→`pending`), `RequirementLabel` (`:147–155`), `RequirementBadge` (`:164–169`, Unmapped uses iconClass `"unmapped"` while css stays `pending`), `StageMeaning` (`:224–241`, has `"unmapped"`), `LegendStages`/`LegendKey` (`:246–247,272–304`, **missing unmapped row** — Task 4). Icons: `Icons.ForStatus("unmapped")` at `Icons.cs:28`.
- **`ProjectCounts`** (src/SpecScribe/ProjectCounts.cs) — the single count ledger (Story 8.3). `Build(progress, sprint, work, epics?)` at `:86`; `StageCount` shape at `:16`; `Empty` at `:50`; built once in `SiteGenerator` (`_counts`, ~:346). **No requirement fields today** — Task 1 adds them.
- **`DashboardViewBuilder`** — `RequirementStatTile` (`:143`) + `RequirementStatSubLine` (`:155–169`) currently recount `RequirementStatus` locally; the requirement tiles lead the Home tile band (`:96–110`). `AppendRequirementsPanel` in `HtmlRenderAdapter.Dashboard.cs` (~:306–345) shows the flow/grid on Home.
- **`Charts`** — `RequirementStatusGrid` (`:1796`), `RequirementFlow` + `FlowStateKey`/`FlowStates`/`RequirementFlowConservation` (Unmapped split lives here, the one documented exception to `ForRequirement`), donut/legend helpers. Reuse a bar/segment helper if one fits your stacked bar.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Requirement rolled-up status | `req.Status` (`RequirementStatus`), from `DeriveStatus` | src/SpecScribe/RequirementsParser.cs:364 |
| Status → css class / color | `StatusStyles.ForRequirement(req)` (Unmapped→`pending`) | src/SpecScribe/StatusStyles.cs:134 |
| Status → label word | `StatusStyles.RequirementLabel(status)` | src/SpecScribe/StatusStyles.cs:147 |
| Color+icon+word badge/chip (never color-only) | `StatusStyles.Badge` / `RequirementBadge` | src/SpecScribe/StatusStyles.cs:164,254 |
| Unmapped glyph (distinct from Planned) | `Icons.ForStatus("unmapped")` | src/SpecScribe/Icons.cs:28 |
| Stage meaning text (single source) | `StatusStyles.StageMeaning(cssClass)` | src/SpecScribe/StatusStyles.cs:224 |
| Portal-wide legend | `StatusStyles.LegendKey()` / `LegendStages` | src/SpecScribe/StatusStyles.cs:246,272 |
| All requirement kinds incl. Design | `RequirementsModel.Everything` | src/SpecScribe/RequirementsModel.cs (9.2) |
| Single count ledger | `ProjectCounts` (`Build`, `StageCount`, `Empty`) | src/SpecScribe/ProjectCounts.cs:12,86 |
| Home requirement tiles/panel | `DashboardViewBuilder.RequirementStatTile`; `HtmlRenderAdapter.Dashboard.AppendRequirementsPanel` | DashboardViewBuilder.cs:143; HtmlRenderAdapter.Dashboard.cs:306 |
| Status grid / bar / donut | `Charts.RequirementStatusGrid` + donut/legend helpers | src/SpecScribe/Charts.cs:1796,299 |

### Guardrails & invariants

- **Status is a single-source system.** Every stage→color routes through `StatusStyles` + the six `--status-*` tokens. No 7th token, no local color map, no re-mapped status words. [Memory `specscribe-status-token-system`]
- **Never color-only (UX-DR17).** Every chip/segment pairs color + icon + word. Unmapped is distinguished from Planned by icon + word (shared tan color is intentional, 9.3).
- **Compose, don't absorb (AC #3).** No new classifier/roll-up; consume `RequirementStatus` + `StatusStyles`. Do not touch 9.1/9.2/9.3 page deliverables except the additive band and the legend row.
- **Single count ledger (8.3).** Satisfaction counts live in `ProjectCounts`; retire the ad-hoc recounts rather than adding a third. Do not leave two authorities for the same number.
- **Byte-identical protection.** Keep FR+NFR donuts/grid/flow output unchanged; prove with the existing FR tests. The band is additive above them.
- **One primary view per dataset (8.7).** The band is the *holistic four-reading* summary (incl. Design); the grid/flow remain the FR+NFR per-requirement detail. Don't render a fourth copy of the same FR+NFR chart.
- **Deterministic output (NFR8 / CI reproducibility).** No dictionary-iteration-order dependence, no timestamps, no per-visitor state. A from-scratch regen is byte-identical. Golden moves on purpose — regen the fingerprint from a clean full build.
- **Degrade to absent (NFR8).** Empty `Everything` → no band, no Home rollup; a missing kind contributes zero, never a broken/empty widget.
- **Shared body path.** HTML + webview + SPA stay byte-aligned; no host fork, no new JS (webview has no `specscribe.js`; always-visible chip word/icon + legend row is the accessible channel).
- **Accessibility.** The stacked bar carries an `aria-label` text-twin; everything stays inside `<main id="main-content">`. [Memory `story-1-4-a11y-seams-for-1-5`]

### Previous story intelligence

- **9.1** wired `StoriesFor` covering-story data into requirement detail pages; established the "epic-level, don't over-claim" honesty framing and the `--status-*`-only accent discipline. Golden-fingerprint gotcha: a partial/`--no-build` run yields a stale hash — regen from a clean full build.
- **9.2** added `RequirementKind.Design`, `Everything`, per-kind donut gating, and the dashboard requirement tiles (`RequirementStatTile`/`RequirementStatSubLine` — the recount you're retiring). Review patch already fixed the Home tile sub-line to count Unmapped honestly — carry that honesty into the ledger. Also widened requirements layout to 1100px with responsive breakpoints.
- **9.3** made `Unmapped` a real tier and proved the "one Sankey bucket-key exception, badge color still `ForRequirement`" pattern — mirror it: your band's color stays on `ForRequirement`; the four-reading grouping is a labeling layer, not a new color source. It also promoted shared `DeferralHeuristics`.
- **8.2** built `StatusStyles`/`LegendKey` and explicitly noted the legend-vs-`*Label` drift risk (deferred) — Task 4 must not open a third vocabulary table; route through `StageMeaning`/`Icons`.
- **8.3** built `ProjectCounts` with the Defined≠Tracked discipline and `StageCount` shape — extend it in the same spirit (named, deterministic, pure).
- **8.7** (one primary view per dataset) and **8.8** (deterministic recency) — keep the band from duplicating existing charts; keep it timestamp-free.

### Git intelligence

Recent commits (`8d9aac4` 9.5 adjustments, `b0168e5`/`9f985b4` review+UX tweaks, `5253281` undrafted create-story panel, `7589495` 9.x progress) show active churn on the requirements/home surfaces and 9.4/9.5 story pages. 9.1–9.3 are already merged, so the `RequirementStatus.Unmapped` tier, `Everything`, and the dashboard requirement tiles are all present on the baseline — this story builds on them directly, no rebase risk on those primitives. Treat Home's current layout as intentional; insert the rollup without reshuffling the stack.

### Project Structure Notes

- Primary edits: `src/SpecScribe/ProjectCounts.cs` (satisfaction buckets + `Build` param), `SiteGenerator.cs` (thread requirements into `Build`), `RequirementsTemplater.cs` (the band in `RenderIndex`), `DashboardViewBuilder.cs` + `HtmlRenderAdapter.Dashboard.cs` (Home rollup + retire local recount), `StatusStyles.cs` (legend row), `assets/specscribe.css` (band/bar/chip classes, token-routed), possibly `Charts.cs` (a small stacked-bar helper if none fits). Tests under `tests/SpecScribe.Tests/`.
- No new source files expected (a tiny `Charts` bar helper is fine if reuse doesn't fit). No new `epics.md` schema. No new NuGet packages (.NET 10 / C#).
- Output goes to `SpecScribeOutput/` by default when you generate to verify — **not** `docs/live`. [Memory `generate-output-dir-is-specscribeoutput`]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` on generated HTML strings, plus direct pure-unit tests on `ProjectCounts`/`StatusStyles`. Run `dotnet test` from repo root.
- Extend the `MultiEpicEpicsMd` fixture (already has Done/Active/Ready/Planned/Unmapped/Deferred + UX-DR cases from 9.2/9.3) rather than authoring a new epics doc.
- Pin: ledger bucket sums + four-reading rollups; band markup (chips, bar, Design inclusion, Unmapped icon+word, In-flight lifecycle tooltip, NFR8 absence); Home rollup + anchor link + parity; legend "Not yet mapped" row; FR+NFR grid/flow/donut byte-identical; golden regen + three parity suites.

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` (`SpecScribeOutput/`). Open **`requirements.html`**: the "Satisfaction at a glance" band sits under the header showing a proportional stacked bar + four chips (Satisfied · In flight · Deferred on purpose · Unmapped) that **include UX-DRs** in the totals; the Unmapped chip reads "Not yet mapped" with its own icon; hovering the In-flight chip breaks it into Partially implemented / Ready / Planned; the existing donuts/grid/flow below look unchanged. Open **Home**: the compact satisfaction rollup shows the same four readings and links to `requirements.html#satisfaction`. Open any page's status legend and confirm the new "Not yet mapped" row is present. `dotnet test` green (golden regenerated; three parity suites pass).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.9] (epics.md:1798–1822) — user story + ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] (epics.md:1612–1621) — epic intent; FR22–FR24/FR26/FR30, UX-DR26, NFR8.
- [Source: _bmad-output/implementation-artifacts/9-1-requirement-pages-link-to-their-covering-stories.md] — covering-story listing; golden-build gotcha.
- [Source: _bmad-output/implementation-artifacts/9-2-nfr-and-ux-dr-coverage-maps.md] — `Design` kind, `Everything`, dashboard requirement tiles (recount to retire).
- [Source: _bmad-output/implementation-artifacts/9-3-deferred-on-purpose-vs-unmapped-coverage-states.md] — `Unmapped` tier, `RequirementBadge` icon override, Sankey `FlowStateKey` exception, six-token discipline.
- [Source: _bmad-output/implementation-artifacts/8-2-canonical-status-model-with-portal-wide-legend.md] — `StatusStyles`/`LegendKey`; legend-vs-`*Label` drift note.
- [Source: _bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md] — `ProjectCounts` ledger discipline.
- [Source: src/SpecScribe/ProjectCounts.cs:12,50,86] — ledger record, `Empty`, `Build` (add satisfaction buckets + requirements param).
- [Source: src/SpecScribe/RequirementsTemplater.cs:10,32,54,70,520] — `RenderIndex` (band insertion point), `StatusCounts` (recount to retire).
- [Source: src/SpecScribe/StatusStyles.cs:134,147,164,224,246,272] — `ForRequirement`/`RequirementLabel`/`RequirementBadge`/`StageMeaning`/`LegendStages`/`LegendKey`.
- [Source: src/SpecScribe/Icons.cs:28] — `Icons.ForStatus("unmapped")`.
- [Source: src/SpecScribe/DashboardViewBuilder.cs:92,143,155] — Home requirement tiles + recount.
- [Source: src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:306] — `AppendRequirementsPanel` (Home rollup site).
- [Source: src/SpecScribe/Charts.cs:1796,299] — `RequirementStatusGrid`, donut/legend helpers.
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:207,398] — golden fingerprint test + constant to regen.
- [Source: tests/SpecScribe.Tests/RenderParityTests.cs · RenderSectionParityTests.cs · RenderSpaParityTests.cs] — three parity suites.
- [Source: memory `specscribe-status-token-system`, `create-story-elicit-visual-intent`, `story-1-4-a11y-seams-for-1-5`, `generate-output-dir-is-specscribeoutput`, `golden-diff-normalization-gotchas`].

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
|------|--------|
| 2026-07-16 | create-story — ready-for-dev. Owner locked: (1) requirements-hub-owned "Satisfaction at a glance" band spanning FR+NFR+UX-DR + compact Home rollup; (2) four-reading summary (Satisfied · In flight · Deferred on purpose · Unmapped) as one proportional stacked bar + count chips over the six canonical tiers (no parallel colors/words); (3) extend `ProjectCounts` ledger + retire local recount; (4) close the legend gap (add "Not yet mapped" row) and include UX-DR/Design in the holistic reading. Composes 9.1–9.3 + 8.2/8.3; absorbs none. |
