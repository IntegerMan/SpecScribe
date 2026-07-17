---
baseline_commit: dd11922936aedd504b5d3b2066fa28c5499187c5
---

# Story 9.3: Deferred-on-Purpose vs Unmapped Coverage States

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder reading coverage,
I want deliberate deferrals distinguished from unmapped gaps,
so that I do not misread intentional scope decisions as oversights.

## Acceptance Criteria

1.
**Given** a requirement without active coverage
**When** coverage reporting renders
**Then** "deferred on purpose" and "unmapped" render as distinct states with distinct visual treatment
**And** the distinction is never color-only.

2.
**Given** a deliberately deferred item
**When** its coverage state renders
**Then** it links to the deferral source (retro, change proposal, or deferred-work entry) when one exists
**And** the requirements-flow diagram and its accessibility text twin both carry the split.

## Context & Scope

Epic 9 completes the requirement → epic → story chain. Stories 9.1 and 9.2 (both `ready-for-dev`, not yet built) both explicitly deferred this exact problem to this story and left structural seams for it:

- **9.1's `RenderRequirement` empty state** already branches `req.Deferred` vs "no covering epics" into separate copy, but both currently read through the *same* `RequirementStatus.Planned` header badge — 9.1's own dev notes say "9.3 will give deferred vs unmapped **distinct visual treatment** and link deferred items to their deferral source. Keep the two branches structurally separate and comment the seam; do not attempt 9.3's visual/linking work here." [Source: `9-1-requirement-pages-link-to-their-covering-stories.md` Task 3]
- **9.2's new "Non-functional & design coverage" section** built a *section-local* "Not yet mapped" badge (grey, reusing the `deferred` css class, word-only distinction) specifically because "9.3 will give them fully distinct visual treatment; leave a clean seam and comment it," and explicitly did **not** add a `RequirementStatus` enum value, scoping the fix to its own new section only. [Source: `9-2-nfr-and-ux-dr-coverage-maps.md` Task 3]

**This story is the one that actually fixes the underlying data model.** Today, `RequirementStatus` has exactly one bucket (`Planned`) for two genuinely different situations:

- A requirement **covered by an epic that simply hasn't started** (real coverage, real plan, just not begun) — the legitimate reading of "Planned."
- A requirement with **`CoverageEpicNumbers.Count == 0` and `Deferred == false`** — no plan exists at all. Today this silently reads as "Planned" too, which is the exact false-oversight-vs-intentional-scope confusion this story exists to kill. [Source: src/SpecScribe/RequirementsParser.cs:190-206 `DeriveStatus`, current fallback `return RequirementStatus.Planned;` on line 196 covers BOTH cases]

So this story adds a genuine fourth-and-fifth-tier split to `RequirementStatus` itself (not a section-local hack), threads it through every existing consumer (the FR/NFR status grid, the requirements-flow Sankey + its aria text twin, the status donuts, the requirement cards, and the detail-page header/coverage body 9.1 built), and adds best-effort linking from a deferred item to wherever its deferral was actually decided.

### Owner-selected design directions (locked at create-story)

Two decisions were made with the owner up front (project rule: elicit design intent for any new visual surface — see the Epic 3 retro action, memory `create-story-elicit-visual-intent`):

1. **Unmapped vs Deferred — different token family, not just different words.** Unmapped routes through the existing `--status-pending` (tan) token family — the closest existing meaning ("listed, no plan yet") — with its own icon and the word "Not yet mapped." Deferred keeps its existing dedicated grey `--status-deferred` token with its own icon and the word "Deferred." This reuses two of the **six existing canonical `--status-*` tokens** (memory `specscribe-status-token-system`: six tokens are the single stage→color source) — **do not add a seventh token.** The two states differ by color family AND icon AND word, so the distinction survives even without color (icon + word alone still disambiguates, satisfying "never color-only" independently of the color difference).
2. **Deferral-source linking — best-effort heuristic, no new authoring.** Reuse the exact pattern Story 2.3 already shipped in `ActionItemsTemplater` (deferred-work-page linking via a debt-keyword regex over free text, and an `epicRetroMap`-style epic→retro lookup): scan the requirement's `CoverageNote` for recognizable epic mentions and deferred/debt language, and link to the matching retro page and/or the deferred-work page when a match resolves. Render plain, unlinked text when nothing matches — this is explicitly optional per AC #2 ("when one exists"). **Do not introduce a new machine-readable tag or any new field in the `### FR Coverage Map` line format.**

### Non-negotiable project principle: no new authoring schema

The owner flagged this explicitly during create-story: **SpecScribe's ability to support many spec-driven frameworks without dictating a house authoring style is a load-bearing project value, not just this story's convenience choice.** Every requirement/coverage feature to date (Story 3.7's flow, Story 9.1's story listing, Story 9.2's NFR/UX-DR maps) has deliberately derived new signal from data that already exists in the standard `epics.md` shape (`### FR Coverage Map` lines, `## Epic List` headers) rather than asking authors to add new tags, fields, or conventions. This story continues that pattern: the deferral-source link is **best-effort inference over the existing free-text `CoverageNote`**, never a new required format. If a future story wants a more precise, explicitly-tagged deferral-source link, that should go through an ADR weighing the authoring-burden tradeoff explicitly — **do not add one silently inside this story.** (Flagged to the owner as a candidate follow-up ADR; not in this story's scope to write it.)

## Tasks / Subtasks

- [x] **Task 1 — Add the `Unmapped` status tier to the core model (AC: #1)**
  - [x] Add `RequirementStatus.Unmapped` to the enum, positioned between `Deferred` and `Planned` in the doc-comment's least→most-complete ordering (it is a lesser signal than "Planned" — nothing is even queued). Update the enum's XML doc to describe the new tier. [Source: src/SpecScribe/RequirementsModel.cs:17]
  - [x] In `RequirementsParser.DeriveStatus` (src/SpecScribe/RequirementsParser.cs:185-206), change the `classes.Count == 0` fallback (line 196) to distinguish: `!deferred && epicNumbers.Count == 0` → `RequirementStatus.Unmapped`; keep `Planned` for the case where `epicNumbers` is non-empty but every covering epic classifies as `pending`/`drafted` (i.e., real coverage exists, nothing has started). Concretely: the current early `if (deferred) return Deferred;` stays; then `if (epicNumbers.Count == 0) return RequirementStatus.Unmapped;`. **Keep** the subsequent `classes.Count == 0 → Planned` arm for the phantom/typo'd epic-number shape (named in the map but absent from the model) — without it, `classes.All(c => c == "done")` is vacuously true on an empty list and would over-claim Done. (Original create-story text called this arm unreachable after the empty-epicNumbers check; that was wrong for the phantom-epic input. Pinned by `DeriveStatus_NamedEpicThatDoesNotExist_StaysPlannedNotUnmappedNorDone`.)
  - [x] **This is a real behavior change, not additive-only.** `RequirementsParserTests.DeriveStatus_PartiallyImplemented_WhenACoveringEpicHasAnInProgressStory` currently asserts `Assert.Equal(RequirementStatus.Planned, reqs.ById["FR4"].Status)` for the unmapped FR4 fixture — this assertion must become `RequirementStatus.Unmapped`. Grep the whole test suite for `RequirementStatus.Planned` assertions on requirements known to be unmapped (FR4 in `MultiEpicEpicsMd`) before touching anything, so none are missed. [Source: tests/SpecScribe.Tests/RequirementsAndProgressTests.cs:215]

- [x] **Task 2 — Route the new tier through `StatusStyles` (AC: #1)**
  - [x] `StatusStyles.ForRequirement` (src/SpecScribe/StatusStyles.cs:98-105): add `RequirementStatus.Unmapped => "pending"` — **reuses the existing `pending` css class/token** (owner decision #1), so Unmapped visually sits in the tan family while Deferred keeps its own dedicated `"deferred"` (grey) class. Do not invent a new css class or a new `--status-*` custom property.
  - [x] `StatusStyles.RequirementLabel` (src/SpecScribe/StatusStyles.cs:107-114): add `RequirementStatus.Unmapped => "Not yet mapped"`. Verify the existing `_ => "Deferred"` fallback still only catches `Deferred` now that `Unmapped` has its own explicit arm (switch exhaustiveness — add the arm explicitly, don't rely on the wildcard).
  - [x] **Icon distinctness (AC #1 "never color-only"):** `StatusStyles.Badge`/`Icon` delegates to `Icons.ForStatus(cssClass)` keyed by css class (src/SpecScribe/StatusStyles.cs:177-186). Since Unmapped shares the `"pending"` css class with the pre-existing pending/tan family, its icon would currently be identical to genuine "Planned." Check `Icons.ForStatus` (src/SpecScribe/Icons.cs) for how it's keyed — if icon selection is purely by css class, `Unmapped` and `Planned` would share both color AND icon, which is **not** distinct enough for AC #1. Two acceptable fixes, pick whichever fits `Icons`' existing shape with the least new surface: (a) key `Icons.ForStatus` by an optional richer discriminator so `Unmapped` gets its own glyph while keeping the `pending` css class for color, or (b) give `Badge`/callers here a way to pass an icon override for this one case. Do NOT solve this by giving Unmapped a different css class than `pending` — that was the explicit owner call in decision #1. The WORD ("Not yet mapped" vs "Planned") already differs regardless, so if a distinct icon proves awkward to thread through `Icons.ForStatus`'s existing keying, icon-parity-but-word-difference is an acceptable fallback — but attempt the distinct icon first.

- [x] **Task 3 — Requirements-flow Sankey + its text twin (AC: #1, #2)**
  - [x] `Charts.FlowStates` (src/SpecScribe/Charts.cs:1174-1178) currently has 5 tuples ending in `("deferred", "deferred")`. Add a 6th: `("pending", "not yet mapped")` — **but `"pending"` is already the tuple key for "planned"** (`("pending", "planned")` on line 1177). `RequirementFlowConservation`'s `byState` dictionary is keyed by **css class**, and `ForRequirement` now returns `"pending"` for BOTH `Planned` and `Unmapped` — so the flow's state bucketing needs a real decision here, not just a tuple append:
    - **Do not silently merge Planned and Unmapped into one Sankey terminal state** — that would defeat AC #2's explicit requirement that "the requirements-flow diagram and its accessibility text twin both carry the split." `FlowStates`/`RequirementFlowConservation`/`RequirementFlow` all key by `StatusStyles.ForRequirement`'s css class, so if Unmapped and Planned share that css class, the flow diagram cannot distinguish them without a second discriminator.
    - Resolve this by having the flow layer (not `StatusStyles.ForRequirement`, which is correctly reused everywhere else) branch on `RequirementStatus` directly for its own state-bucket key where `ForRequirement` alone is ambiguous — i.e., derive the flow's bucket key as `req.Status == RequirementStatus.Unmapped ? "unmapped" : StatusStyles.ForRequirement(req)`, add `("unmapped", "not yet mapped")` to `FlowStates` alongside (not replacing) the existing `("pending", "planned")` tuple, and thread the same distinguishing logic through `RequirementFlowConservation` (byState keys) and `RequirementFlow` (`PairWeight`, node/ribbon rendering, the "No coverage" node's honest sub-label). Keep the **badge/card/grid/donut** color still routed through `StatusStyles.ForRequirement`'s `"pending"` class (owner decision #1 — same tan family) — only the Sankey's bucket KEY needs the extra split, not its color.
    - Update the flow's `<rect class="req-flow-node req-flow-state {css}">` CSS selector needs a rule for the new `unmapped` class too (reusing `--status-pending` for fill, per owner decision #1) — add `.req-flow-state.unmapped` alongside the existing per-state selectors near `.req-status-block.done`/`.active` etc. (src/SpecScribe/assets/specscribe.css:~1631+).
    - Update the aria summary string (`RequirementFlow`, src/SpecScribe/Charts.cs:1287-1289) to report unmapped as its own count, not folded into "planned." This aria string doubles as part of the accessibility text-twin AC #2 requires.
    - The "No coverage" L1 node's tooltip text (`titleExtra = key == Sentinel ? "deferred, unmapped, or non-functional" : ...`, Charts.cs:1349-1351) already says "unmapped" in prose — leave it, it's still accurate; the new split is about the STATE column, not the coverage column.
  - [x] `Charts.RequirementFlowConservation` is unit-tested directly (per its own doc comment, "exposed for testing") — expect and update a fixture-driven test asserting bucket counts once the split lands.

- [x] **Task 4 — Status donuts, status grid, and requirement cards (AC: #1)**
  - [x] `RequirementsTemplater.StatusCounts`/`StatusSegments` (src/SpecScribe/RequirementsTemplater.cs:254-272) currently produce 5 named segments (Done/Active/Ready/Planned/Deferred). Add a 6th "Not yet mapped" segment keyed off `RequirementStatus.Unmapped`, with its own count. `AppendStatusDonut`'s aria-label string-building (line 235) needs the new count folded in too.
  - [x] `Charts.RequirementStatusGrid` (src/SpecScribe/Charts.cs:1146-1168) already iterates generically via `StatusStyles.ForRequirement`/`RequirementLabel` — once Tasks 1–2 land, the grid tiles for unmapped requirements automatically pick up the right class/label/tooltip. Verify with a test rather than assuming; this is exactly the kind of "did the new tier propagate everywhere" gap the project's retro items warn about.
  - [x] `RequirementsTemplater.AppendRequirementCard` (src/SpecScribe/RequirementsTemplater.cs:204-226): currently `if (req.Deferred) → "Deferred" chip; else if (req.CoverageEpicNumber) → epic-link chip; else → (nothing)`. That silent `else` branch IS today's unmapped case rendering no chip at all — add an explicit `else` arm rendering a "Not yet mapped" chip (mirroring the deferred chip's markup/css shape, using the `pending`-family class per decision #1) so unmapped requirements are visibly flagged in the card list, not just silently missing a chip.

- [x] **Task 5 — Detail-page header badge + Coverage body (AC: #1, #2)**
  - [x] `RequirementsTemplater.RenderRequirement`'s header badge (`StatusStyles.Badge(statusClass, StatusStyles.RequirementLabel(req.Status))`, line 114) already reads through `req.Status` — once Tasks 1–2 land, an unmapped requirement's header badge automatically reads "Not yet mapped" in the tan family instead of a misleading "Planned." Confirm with a test.
  - [x] The Coverage body (lines 123-149, and — if Story 9.1 has landed by the time this story is worked — 9.1's replacement per-epic-group body) has an explicit `else` branch for "no covering epic, not deferred" (today: `"No covering epic recorded."` at line 146, or 9.1's seamed "Not yet mapped to any epic or story." if 9.1 landed first). Give this branch the same "Not yet mapped" badge treatment as the header for visual consistency, rather than leaving it as bare prose.
  - [x] **Deferred item → deferral-source link (AC #2, owner decision #2):** in the `req.Deferred` branch (line 124-130, or 9.1's equivalent), after the existing `CoverageNote` paragraph, attempt to resolve a link using the SAME heuristic `ActionItemsTemplater` already uses:
    - Reuse (do not fork) the debt/deferred keyword regex pattern from `ActionItemsTemplater.DebtWords`/`IsDebtRelated` (src/SpecScribe/ActionItemsTemplater.cs:90-92) — if it needs to move to a shared location so both callers use it, promote it to a small shared helper (e.g. a static method on `RetroActionStyler` or a new tiny static class) rather than duplicating the regex.
    - Scan `req.CoverageNote` for an "Epic N" mention (a simple `\bEpic\s+(\d+)\b` match is enough — do not build a generalized reference parser) and, if a numeric epic is found AND that epic has a retro (reuse the same `_epicRetroMap`-shaped lookup `SiteGenerator` already builds for `ActionItemsTemplater` at src/SpecScribe/SiteGenerator.cs:1161,1196-1197), render a link to that retro page.
    - If the note matches deferred/debt language and a deferred-work page exists (`WorkInventory.Deferred?.OutputPath`, already resolved once in `SiteGenerator` for `WriteActionItems` — reuse that resolution rather than rebuilding `WorkInventory` a second time; thread it into `WriteRequirements`/`RenderRequirement`'s call), render a link to the deferred-work page.
    - When neither resolves, render nothing extra beyond the existing plain-text note — this is explicitly optional per AC #2's "when one exists." Never fabricate a link.
    - Wire whatever new parameters this needs (the epic-retro map, the deferred-work href) through `SiteGenerator.WriteRequirements` → `RequirementsTemplater.RenderRequirement`'s signature, mirroring how `WriteActionItems` already threads `EpicRetroMap` + `deferredHref` into `ActionItemsTemplater.RenderPage`. [Source: src/SpecScribe/SiteGenerator.cs:1314-1326,1387-1404]

- [x] **Task 6 — Coordinate the 9.2 section-local seam (AC: #1)**
  - [x] If Story 9.2 has landed by the time this story is worked, its "Non-functional & design coverage" section built a **section-local** "Not yet mapped" presentation (explicitly not using a `RequirementStatus` enum value — see 9.2's Task 3). Once this story adds the real `RequirementStatus.Unmapped` tier, 9.2's NFR/UX-DR items should now derive `Unmapped` from `DeriveStatus` for free (their `CoverageEpicNumbers` empty + not deferred → `Unmapped` automatically) — **retire 9.2's section-local special-casing in favor of the now-real enum tier**, so there is exactly one "Not yet mapped" implementation, not two. If 9.2 has NOT landed yet, leave a comment at this story's Task 1/2 sites noting that 9.2's future section should consume `req.Status == RequirementStatus.Unmapped` directly rather than reinventing its own local check.

- [x] **Task 7 — Tests (AC: #1, #2)**
  - [x] Extend `RequirementsAndProgressTests`' `MultiEpicEpicsMd` fixture (FR4 is already the unmapped case) with a **deferred item that has a resolvable deferral source** — e.g. a `CoverageNote` mentioning "Epic 1" paired with a fixture epic that has a retro, and/or deferred/debt language paired with a `WorkInventory.Deferred` entry — so both the "link resolves" and "link absent" paths for AC #2 are covered.
  - [x] Parser tests: `DeriveStatus` returns `Unmapped` (not `Planned`) for a requirement with `Deferred == false` and empty `CoverageEpicNumbers`; existing `Deferred`/`Planned`(real-coverage-not-started) cases unaffected. Update the pre-existing `RequirementStatus.Planned` assertion on FR4 found in Task 1.
  - [x] `StatusStyles` tests: `ForRequirement(Unmapped)` → `"pending"`; `RequirementLabel(Unmapped)` → `"Not yet mapped"`; confirm the css class stays distinct from `Deferred`'s `"deferred"` class (two different classes) while `Planned` and `Unmapped` intentionally share `"pending"`.
  - [x] `Charts` tests: `RequirementFlowConservation` (or `RequirementFlow`'s rendered SVG/aria string) shows unmapped requirements as their own bucket, separate from planned, with the total still conserving (nothing dropped/double-counted). A fixture with both a deferred and an unmapped requirement shows two distinct state nodes/ribbons in the flow, not one merged "pending" node.
  - [x] Rendering tests on `RequirementsTemplater.RenderIndex`/`RenderRequirement`: the status grid, donut segments, requirement cards, and detail-page header/coverage body all show "Not yet mapped" (not "Planned") for an unmapped requirement, with a distinct icon/class from "Deferred." A deferred requirement whose `CoverageNote` matches the heuristic renders a working link to its retro or deferred-work page; a deferred requirement with no matching text renders no link (graceful, not broken).
  - [x] Run the full suite from repo root (`dotnet test`). This story touches shared classifiers (`RequirementStatus`, `StatusStyles.ForRequirement`) that Stories 9.1/9.2 also depend on — watch `RequirementsAndProgressTests`, `SiteGeneratorTraceabilityTests`, `ChartsTests`, `StatusStylesTests`, `StylesheetTests`, `LinkifierTests`, and (if 9.1/9.2 have landed) their own new tests for regressions from the `RequirementStatus.Planned`→`Unmapped` reclassification.

## Dev Notes

### What exists today (read before touching)

- **`RequirementStatus`** (src/SpecScribe/RequirementsModel.cs:17) has 5 values today: `Deferred, Planned, Ready, Active, Done`. `Planned` is currently overloaded to mean both "covered, not started" and "no coverage at all" — the exact bug this story fixes.
- **`RequirementsParser.DeriveStatus`** (src/SpecScribe/RequirementsParser.cs:185-206) is the single place status is rolled up; its `classes.Count == 0` fallback (driven by `epicNumbers.Count == 0`, i.e. no covering epics resolved) is where the split belongs.
- **`StatusStyles.ForRequirement`/`RequirementLabel`** (src/SpecScribe/StatusStyles.cs:98-114) are the single css-class/label mapping every requirement-status consumer routes through — grid, donuts, cards, detail page.
- **`Charts.FlowStates`/`RequirementFlowConservation`/`RequirementFlow`** (src/SpecScribe/Charts.cs:1170-1381) key the Sankey's terminal-state column by `StatusStyles.ForRequirement`'s css class — this is the one place the new tier needs a bucket key distinct from `ForRequirement`'s reused `"pending"` class (see Task 3), because the flow diagram must show the split even though the badge color does not have to.
- **`ActionItemsTemplater`** (src/SpecScribe/ActionItemsTemplater.cs) already solved "best-effort link this free text to a deferred-work page / a retro page" for retrospective action items — this is the pattern to reuse, not reinvent, for Task 5's deferral-source link.
- **`SiteGenerator`** already builds `_epicRetroMap` (epic number → retro output path) and resolves `WorkInventory.Deferred?.OutputPath` once for `WriteActionItems` (src/SpecScribe/SiteGenerator.cs:1161,1196-1197,1314-1326) — thread these same values into `WriteRequirements`/`RenderRequirement` rather than re-deriving them.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Requirement status → css class | `StatusStyles.ForRequirement(req)` | src/SpecScribe/StatusStyles.cs:98 |
| Requirement status → label word | `StatusStyles.RequirementLabel(status)` | src/SpecScribe/StatusStyles.cs:107 |
| Color+icon+word badge | `StatusStyles.Badge(cssClass, label)` | src/SpecScribe/StatusStyles.cs:185 |
| Epic number → retro page href | `SiteGenerator._epicRetroMap` / `EpicRetroMap` | src/SpecScribe/SiteGenerator.cs:1161,1196-1197 |
| Deferred-work page href | `WorkInventory.Build(docs).Deferred?.OutputPath` (already resolved once for `WriteActionItems`) | src/SpecScribe/SiteGenerator.cs:1320 |
| Debt/deferred keyword heuristic | `ActionItemsTemplater.DebtWords`/`IsDebtRelated` (promote to shared if reused) | src/SpecScribe/ActionItemsTemplater.cs:90-92 |
| Sankey flow terminal-state buckets | `Charts.FlowStates` + `RequirementFlowConservation` | src/SpecScribe/Charts.cs:1174,1186 |
| Requirement card chip shape (mirror for "Not yet mapped") | `AppendRequirementCard`'s `Deferred` chip branch | src/SpecScribe/RequirementsTemplater.cs:214-217 |

### Guardrails & invariants

- **No new authoring schema.** The deferral-source link is inferred from existing free text (`CoverageNote`) and existing artifacts (retros, deferred-work.md) — never a new tag/field in `epics.md`'s coverage-map line format. This is a stated project value (framework-agnostic, no house authoring conventions dictated to the community), not just this story's convenience call — see the "Non-negotiable project principle" note above. If a future story wants a precise tag-based link, that decision belongs in an ADR, not a silent addition here.
- **Six `--status-*` tokens remain the single color source.** Do not add a 7th token. Unmapped reuses `--status-pending`; Deferred keeps its existing `--status-deferred`. [Memory `specscribe-status-token-system`]
- **Never color-only (UX-DR17).** Icon + word must distinguish Unmapped from Planned even though they share a css class/color; word alone must distinguish Unmapped from Deferred even though those differ in color too.
- **`StatusStyles.ForRequirement` stays the single badge/card/grid/donut color source; the Sankey's bucket key is the one deliberate, documented exception** (Task 3) — because the flow diagram's AC explicitly requires the split visible there even though the badge color does not need a 7th token. Do not let this exception leak into any other consumer.
- **Deterministic output (NFR8 / CI reproducibility).** The deferral-source-link heuristic must not depend on iteration order or introduce timestamps/per-visitor state. A from-scratch regeneration must be byte-identical.
- **Degrade gracefully, don't break (NFR2).** No resolvable deferral source → plain text, never a broken link or an exception. A deferred requirement whose epic number in `CoverageNote` doesn't exist, or whose retro doesn't exist, silently skips that link.
- **Conservation still holds.** Every requirement must still terminate in exactly one Sankey state bucket after the split — the existing `RequirementFlowConservation` contract (entering count == sum of state counts) must continue to hold with 6 buckets instead of 5.
- **Coordinate, don't duplicate, with 9.1 and 9.2** (both `ready-for-dev`, may land before or after this story — see Task 5/6). If 9.1 has changed `RenderRequirement`'s signature/Coverage body by the time this lands, apply this story's changes on top of 9.1's shape rather than reverting it. If 9.2 has landed, retire its section-local "Not yet mapped" special-case in favor of this story's real `RequirementStatus.Unmapped` tier (Task 6).

### Project Structure Notes

- Primary code: `src/SpecScribe/RequirementsModel.cs` (enum), `RequirementsParser.cs` (`DeriveStatus`), `StatusStyles.cs` (class/label/icon mapping), `Charts.cs` (`FlowStates`/`RequirementFlowConservation`/`RequirementFlow`), `RequirementsTemplater.cs` (grid/donut/card/detail-page rendering + new deferral-source link), `SiteGenerator.cs` (threading the retro-map/deferred-work-href into `WriteRequirements`), possibly `Icons.cs` (distinct Unmapped icon) and `ActionItemsTemplater.cs` (promoting the shared debt-keyword regex). Tests in `tests/SpecScribe.Tests/`. No new source files expected; no epics.md schema changes.
- If new CSS is needed (the `.req-flow-state.unmapped` selector, a card-chip variant), add it to `src/SpecScribe/assets/specscribe.css` routed through the existing `--status-*` tokens; `StylesheetTests` guards the stylesheet.
- Output goes to `SpecScribeOutput/` by default when you generate to verify — **not** `docs/live`. [Memory `generate-output-dir-is-specscribeoutput`]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` on generated HTML strings — the established pattern in `RequirementsAndProgressTests` and `SiteGeneratorTraceabilityTests`. Run `dotnet test` from repo root.
- Reuse and extend `MultiEpicEpicsMd` (FR4 is already the unmapped fixture case) rather than authoring a wholly new epics doc.
- Grep the test suite for every existing `RequirementStatus.Planned` assertion before changing `DeriveStatus`, so no unmapped-case regression sneaks past silently (this is the exact "parser silently drops/over-claims a real input shape" class of bug the Epic 3 retro flagged).

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` (`SpecScribeOutput/`), open `requirements.html`, and confirm: the status grid, donuts, and requirement cards show a visually distinct "Not yet mapped" tile/chip (tan family, distinct icon) separate from "Deferred" (grey) and from genuine "Planned" (same tan family, different icon/word) items. Open the requirements-flow Sankey and confirm unmapped and deferred requirements terminate in two separate, separately-labeled state nodes, and that the aria-label/hint text states both counts. Open an unmapped FR/NFR detail page and confirm its header badge reads "Not yet mapped," not "Planned." If any requirement in this repo's own epics.md happens to carry a deferred coverage note mentioning a real epic/retro, confirm the link renders; otherwise confirm the plain-text fallback renders cleanly with no broken link.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 9.3`] (epics.md:1576-1594) — user story + ACs.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 9`] (epics.md:1530-1534) — epic intent; FR24 (epics.md:186), UX-DR26 (epics.md:153), NFR8 (epics.md:99).
- [Source: `_bmad-output/implementation-artifacts/9-1-requirement-pages-link-to-their-covering-stories.md`] — sibling story that leaves the deferred-vs-unmapped copy seam and the `RenderRequirement` signature this story builds on.
- [Source: `_bmad-output/implementation-artifacts/9-2-nfr-and-ux-dr-coverage-maps.md`] — sibling story whose section-local "Not yet mapped" hack this story's real enum tier should retire (Task 6).
- [Source: src/SpecScribe/RequirementsModel.cs:17] — `RequirementStatus` enum to extend.
- [Source: src/SpecScribe/RequirementsParser.cs:185-206] — `DeriveStatus`, the roll-up to fix.
- [Source: src/SpecScribe/StatusStyles.cs:98-114,177-186] — canonical requirement class/label/icon/badge.
- [Source: src/SpecScribe/Charts.cs:1170-1381] — `FlowStates`, `RequirementFlowConservation`, `RequirementFlow` (Sankey + text-twin aria).
- [Source: src/SpecScribe/RequirementsTemplater.cs:204-272] — status grid/donut/card rendering to extend.
- [Source: src/SpecScribe/ActionItemsTemplater.cs:85-93] — the debt-keyword/link-resolution pattern to reuse for AC #2.
- [Source: src/SpecScribe/SiteGenerator.cs:1161,1196-1197,1314-1326,1387-1404] — `_epicRetroMap`, `WorkInventory.Deferred` resolution, `WriteRequirements` (signature threading point).
- [Source: tests/SpecScribe.Tests/RequirementsAndProgressTests.cs:95-218] — `MultiEpicEpicsMd` fixture (FR3 deferred, FR4 unmapped) to extend.
- [Source: memory `specscribe-status-token-system`] — six-token constraint honored by owner decision #1.
- [Source: memory `create-story-elicit-visual-intent`] — why this story's design directions were elicited up front.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (Cursor agent)

### Debug Log References

- Stories 9.1 and 9.2 had already landed (`review`) when this story started; Task 5/6 built on 9.1's `RenderRequirement` Coverage body and retired 9.2's section-local "Not yet mapped" special-case.
- Kept `classes.Count == 0 → Planned` (after the new `epicNumbers.Count == 0 → Unmapped` check) rather than deleting it: a coverage line that names a phantom epic number still has author intent to map, and without the empty-classes guard `classes.All(c => c == "done")` would vacuously claim Done. Pinned with a dedicated parser test.

### Completion Notes List

- Added `RequirementStatus.Unmapped` between Deferred and Planned; `DeriveStatus` now returns Unmapped when `!deferred && epicNumbers.Count == 0`.
- `StatusStyles.ForRequirement(Unmapped) → "pending"` (tan, no 7th token); `RequirementLabel → "Not yet mapped"`; new `RequirementBadge` + `Badge` icon override + dashed-slot `Icons.ForStatus("unmapped")` so Unmapped differs from Planned by icon + word (never color-only).
- Sankey uses a flow-local `FlowStateKey` that splits Unmapped into its own `"unmapped"` bucket (documented exception to `ForRequirement`); aria twin reports the unmapped count separately; CSS `.req-flow-state.unmapped` reuses `--status-pending`.
- Donuts gain a 6th "Not yet mapped" segment; cards gain an explicit `req-epic unmapped` chip; detail-page header + Coverage body both show the Unmapped badge.
- Promoted debt/epic-mention heuristics to shared `DeferralHeuristics`; deferred requirement detail pages best-effort link to retro and/or deferred-work when `CoverageNote` matches (no new authoring schema). Threaded `EpicRetroMap` + deferred-work href through `WriteRequirements` → `RenderRequirement`.
- Retired 9.2's section-local Deferred/"Not yet mapped" special-casing in `AppendCoverageRow` in favor of `RequirementBadge`.
- Full suite green: **1184 passed, 0 failed**. Portal regenerated to `SpecScribeOutput/`; NFR3 detail page reads "Not yet mapped" (not Planned); Sankey aria reports "1 not yet mapped" as its own bucket. This repo's epics.md has no Deferred coverage notes — deferral-source linking covered by unit tests.

### File List

- src/SpecScribe/RequirementsModel.cs
- src/SpecScribe/RequirementsParser.cs
- src/SpecScribe/StatusStyles.cs
- src/SpecScribe/Icons.cs
- src/SpecScribe/Charts.cs
- src/SpecScribe/RequirementsTemplater.cs
- src/SpecScribe/ActionItemsTemplater.cs
- src/SpecScribe/DeferralHeuristics.cs (new)
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/HtmlRenderAdapter.Dashboard.cs (same commit: home tile-journey → journey-lead companion tweak)
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/RequirementsAndProgressTests.cs
- tests/SpecScribe.Tests/StatusStylesTests.cs
- tests/SpecScribe.Tests/ChartsTests.cs
- tests/SpecScribe.Tests/StylesheetTests.cs
- tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs (companion to Dashboard tweak)
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs (golden fingerprint regen)
- _bmad-output/implementation-artifacts/sprint-status.yaml

### Change Log

- 2026-07-16: Implemented Story 9.3 — Unmapped vs Deferred vs Planned as distinct coverage states (enum + StatusStyles + Sankey/aria twin + donuts/cards/detail page + best-effort deferral-source links; retired 9.2 section-local seam). 1184 tests green.
- 2026-07-16: Code review — patches applied (exact DebtWords restore, EpicMention IgnoreCase + comment fix, thread WorkInventory into WriteRequirements, ribbon CSS assert, Task 1 / File List / sprint-status hygiene).

### Review Findings

- [x] [Review][Patch] Restore DebtWords to Story 2.3's exact pattern (drop bare `defer`) — promotion to `DeferralHeuristics` silently broadened `\b(deferred|tech(nical)?\s+debt)\b` to also match standalone `defer`, changing ActionItemsTemplater deferred-work link behavior under a "reuse, do not fork" move [`DeferralHeuristics.cs:19`]
- [x] [Review][Patch] Make `EpicMention` case-insensitive — `DebtWords` uses `IgnoreCase` but `EpicMentionRe` does not, so "deferred from epic 1" resolves the backlog link and silently skips the retro link [`DeferralHeuristics.cs:24`]
- [x] [Review][Patch] Fix misleading `EpicMention` comment — "Epics 1 & 2" fails because of the trailing `s` on `Epics`, not "no space after Epic" [`DeferralHeuristics.cs:22`]
- [x] [Review][Patch] Stop re-deriving WorkInventory in `WriteRequirements` (or fix the lying comment) — comment claims the SAME href WriteActionItems resolves is "threaded in, not re-derived", but the body calls `WorkInventory.Build` again; on the epics regen path `workForFollowUps` is already built a few lines above [`SiteGenerator.cs:2809`]
- [x] [Review][Patch] Assert `.req-flow-ribbon.unmapped` in StylesheetTests — node fill is pinned; the sibling ribbon rule added beside it is not [`StylesheetTests.cs:275`]
- [x] [Review][Patch] Trim stale sprint-status create-story tail — implementation note still ends with "Coordinates with 9.1 … and 9.2 … both still ready-for-dev" while both are `review` [`sprint-status.yaml`]
- [x] [Review][Patch] Reconcile Task 1 checklist with shipped code — checked task says remove `classes.Count == 0` as unreachable; Debug Log correctly kept it for phantom-epic Planned (and a parser test pins it) [`9-3-…md` Task 1]
- [x] [Review][Patch] Complete story File List — implementing commit also touched `HtmlRenderAdapter.Dashboard.cs`, `HtmlRenderAdapterTests.cs`, and regenerated `SiteGeneratorAdapterTests.cs` golden fingerprint; omit or list them explicitly [`File List`]
