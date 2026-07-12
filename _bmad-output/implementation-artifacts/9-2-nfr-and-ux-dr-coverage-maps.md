# Story 9.2: NFR and UX-DR Coverage Maps

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want NFR and UX design requirements traced with the same rigor as FRs,
so that non-functional obligations are not second-class.

## Acceptance Criteria

1.
**Given** the requirements page shows an FR coverage map
**When** the page renders
**Then** parallel coverage maps exist for NFRs and UX-DRs
**And** they use the same canonical status vocabulary as Story 8.1 (until 8.1 ships, `StatusStyles` is that canonical source).

2.
**Given** an NFR (or UX-DR) with no per-story implementation state
**When** its coverage renders
**Then** it shows a stated verification approach instead of an undifferentiated "Planned"
**And** per-item granularity replaces whole-section uniform status.

## Context & Scope

Epic 9 completes the requirement → epic → story chain. Story 9.1 closed the FR side (requirement detail pages list covering stories). This story extends the **same traceability rigor to NFRs and UX-DRs**, which are currently second-class:

- **NFRs** are parsed and rendered, but only `NFR10` has an entry in the FR Coverage Map, so `NFR1–9` all roll up to an undifferentiated **"Planned"** — the exact "second-class / whole-section uniform status" problem this story fixes. [Source: `_bmad-output/planning-artifacts/epics.md:161-199` — the FR Coverage Map]
- **UX-DRs are not parsed at all.** `RequirementKind` has only `Functional` and `NonFunctional`; the parser reads `### Functional Requirements` and `### NonFunctional Requirements` but never `### UX Design Requirements`. [Source: src/SpecScribe/RequirementsModel.cs:3, src/SpecScribe/RequirementsParser.cs:37-39]

So this is **more than a rendering story** — it adds a requirement kind, a second coverage source, and honest coverage states, then renders them.

### Owner-selected design directions (locked at create-story)

Three decisions were made with the owner up front (project rule: elicit design intent for any new visual surface — see the Epic 3 retro action, memory `create-story-elicit-visual-intent`):

1. **Layout — dedicated section.** Keep the existing FR-centric surfaces (the Functional/Non-functional donuts, the "Requirements at a glance" grid, the "Requirements flow" Sankey) **byte-identical**. Add a **new "Non-functional & design coverage" section** below them that renders NFRs and UX-DRs with a **per-item verification treatment**. Do **not** force NFR/UX-DR through FR "delivered-by-story" semantics — that would reproduce the second-class problem.
2. **Data source — derive from epic coverage + honest gap.** Resolve each NFR/UX-DR's covering epics from the epic-header reverse index (`**NFRs:** …` / `**UX-DRs:** …` / `**NFRs covered:** …` lines under `## Epic List`), **unioned** with the FR Coverage Map. Roll up state from those epics exactly as FRs do. An item with no covering epic shows an **honest "Not yet mapped"** state (not "Planned"). No new per-requirement authoring schema is introduced. A **conservative back-fill** of epic-header coverage is in scope to reduce false gaps (Task 6) — tag only where the delivering epic is unambiguous.
3. **UX-DR depth — full first-class requirements.** UX-DRs are parsed as a third kind, get a detail page each (`requirements/ux-dr{n}.html`), are linkified in prose, and are counted — the same footprint FRs/NFRs already have.

### Truthfulness caveat you MUST honor

The coverage is **epic-level** (same caveat as FRs — see `RequirementInfo`/`RequirementsParser` XML docs). A covering epic's rolled-up state is an epic-level approximation, never a per-requirement claim. [Source: src/SpecScribe/RequirementsModel.cs:5-16, src/SpecScribe/RequirementsParser.cs:173-206] Frame the new section's copy accordingly ("delivered by Epic N", not "this requirement is N% done"). This directly serves the recurring project value that visuals must never over-claim precision (Story 1.5 truthfulness; Epic 3's repeated "parser silently drops / over-claims a real input shape" retro item). **When the delivering epic is uncertain, leave the item honestly unmapped — never guess a mapping.**

## Tasks / Subtasks

- [ ] **Task 1 — Add the `Design` requirement kind + UX-DR parsing (AC: #1)**
  - [ ] Extend `RequirementKind` with `Design`. Update `RequirementInfo.Id` to yield `"UX-DR{Number}"` for `Design` (it currently branches Functional→"FR" / else→"NFR"). `Slug` stays the generic `Id.ToLowerInvariant()` → `"ux-dr{n}"`; keep slug == output filename == link href (see Task 5/7 — the hyphen is filesystem-safe and the linkifier reconstructs the same slug). [Source: src/SpecScribe/RequirementsModel.cs:3,21-33]
  - [ ] In `RequirementsParser.Parse`, slice `### UX Design Requirements` from the inventory (same `SliceSection(inventory, "### UX Design Requirements", "### ")` pattern already used for the FR/NFR sections) and parse it. UX-DR lines look like `UX-DR12: Implement a generated timestamp…` — the existing `DefLine` regex (`^(FR|NFR)(\d+):`) will NOT match, so add a `UxDrLine` regex `^UX-DR(\d+):\s*(.+)$` and a parse path (no categories, like NFRs). [Source: src/SpecScribe/RequirementsParser.cs:18,37-45,102-157; `_bmad-output/planning-artifacts/epics.md:126-157`]
  - [ ] Add `IReadOnlyList<RequirementInfo> Design` to `RequirementsModel`. **Critical:** keep `All` = `Functional.Concat(NonFunctional)` **unchanged** (the FR flow/grid depend on this scope — see Guardrails), but make `ById` include `Design` so UX-DR links resolve. Add a separate `Everything => Functional.Concat(NonFunctional).Concat(Design)` (or similar) for the new-section + detail-page consumers. Update `RequirementsModel.Empty`. [Source: src/SpecScribe/RequirementsModel.cs:65-83]

- [ ] **Task 2 — Second coverage source: epic-header reverse index for NFR/UX-DR (AC: #1, #2)**
  - [ ] Build a `requirement-id → covering epic numbers` map from the `## Epic List` header lines. The lines are `**FRs covered:** … · **UX-DRs:** UX-DR21, UX-DR22 · **NFRs:** NFR8` (note the inconsistent labels: `FRs covered:`, but `NFRs:` and `UX-DRs:`, and `**NFRs covered:**` in Epics 16/17). Parse every `FR\d+`, `NFR\d+`, and `UX-DR\d+` token found in each epic's header block and attribute it to that epic's number. [Source: `_bmad-output/planning-artifacts/epics.md:201-273`; EpicsParser already isolates these header/meta lines — see src/SpecScribe/EpicsParser.cs:21,328,389,523-529]
  - [ ] **Scope the source per kind so FR output stays byte-identical:**
    - **FR** coverage source = FR Coverage Map **only** (unchanged — do not union epic-header FR tokens in; the map is authoritative for FRs and any divergence would silently change existing FR status + break tests).
    - **NFR** coverage source = FR Coverage Map **∪** epic-header NFR tokens (so `NFR8`→Epics 8/9/10, `NFR9`→Epic 16, `NFR10`→Epic 17 **and** the FR Coverage Map's `NFR10` line agree/merge; de-dup + order by appearance).
    - **UX-DR** coverage source = epic-header UX-DR tokens (the FR Coverage Map has none today; union is harmless if one ever appears).
  - [ ] Reuse the existing coverage plumbing: populate each NFR/UX-DR `RequirementInfo.CoverageEpicNumbers` (+ primary `CoverageEpicNumber`, `CoverageEpicTitleHtml`) from the unioned set, and derive `Status` via the **existing** `DeriveStatus` (do not write a second roll-up). A UX-DR/NFR with covering epics then rolls up Done/Active/Ready exactly like an FR, and `RequirementsParser.StoriesFor` works on it for free (Task 5 reuses it). [Source: src/SpecScribe/RequirementsParser.cs:133-152,164-206]
  - [ ] Decide the module boundary cleanly: the reverse-index parse can live in `RequirementsParser` (it already receives `EpicsModel`) or `EpicsParser`. Prefer `RequirementsParser` so all coverage resolution stays in one place; do not add a new public parser class.

- [ ] **Task 3 — Honest "Not yet mapped" state distinct from "Planned" (AC: #2)**
  - [ ] The dedicated section must never stamp an uncovered NFR/UX-DR as "Planned". For each item compute a **section-local** presentation (do **not** add an enum value to `RequirementStatus` — the FR flow/grid iterate that enum and must not change):
    - Covering epics present → the rolled-up `RequirementLabel(req.Status)` badge (Done / Partially implemented / Ready for dev / Planned) **plus** the linked covering-epic chip(s). Here "Planned" is meaningful (covered but epics not started), not the undifferentiated whole-section default.
    - `req.Deferred` → "Deferred" + `CoverageNote`.
    - No covering epics and not deferred → a distinct **"Not yet mapped"** treatment: a grey badge routed through `StatusStyles.Badge` so it carries **icon + word**, never color-only (UX-DR17), + the honest sentence "Not yet mapped to a delivering epic." Reuse the existing `deferred`/grey css class for color, but the **word** must read "Not yet mapped", not "Deferred" (they are different meanings — 9.3 will give them fully distinct visual treatment; leave a clean seam and comment it). [Source: src/SpecScribe/StatusStyles.cs:98-114,182-186; memory `specscribe-status-token-system`]
  - [ ] Section-head framing sentence states the verification approach for the whole class: cross-cutting obligations verified across the codebase by tests + architectural invariants, tracked here by the epics that deliver them — so a bare per-item badge is never the only signal. This is the "stated verification approach" of AC #2 at the section level; the per-item cells provide the granularity.

- [ ] **Task 4 — Render the "Non-functional & design coverage" section on requirements.html (AC: #1, #2)**
  - [ ] In `RequirementsTemplater.RenderIndex`, after the existing "Requirements flow" panel (src/SpecScribe/RequirementsTemplater.cs:56-60) and before "Jump to a group", add the new section under a `<div class="section-divider">Non-functional & design coverage</div>`. Keep it inside the single `<main id="main-content">` landmark (do not add a second `<main>`). [Source: src/SpecScribe/RequirementsTemplater.cs:36-85; memory `story-1-4-a11y-seams-for-1-5`]
  - [ ] Two sub-groups — **Non-functional requirements** (`model.NonFunctional`) and **UX design requirements** (`model.Design`) — each a labelled block. For each requirement render a compact coverage row/card: id linked to its detail page (`prefix + requirements/{slug}.html`), the requirement text, the covering-epic chip(s) linked to `epics/epic-{n}.html`, and the state badge from Task 3. Reuse the existing `req-card` / `req-epic` / `status-badge` classes and the `AppendRequirementCard` shape where possible — do not invent a new card style or new CSS tokens. If a small amount of new CSS is genuinely needed, route every color through the `--status-*` tokens only (never a raw hex). [Source: src/SpecScribe/RequirementsTemplater.cs:204-226; src/SpecScribe/assets/specscribe.css; memory `specscribe-status-token-system`]
  - [ ] Add a third status donut for **Design** to the donut row alongside Functional / Non-functional (`AppendStatusDonut(sb, "Design", model.Design)`), so the at-a-glance counts include UX-DRs. Update the header subtitle count line (`{Functional.Count} functional · {NonFunctional.Count} non-functional`) to add `· {Design.Count} design`. [Source: src/SpecScribe/RequirementsTemplater.cs:21,38-41,228-238]
  - [ ] **NFR8 graceful-degradation invariant:** if a project's epics.md has no `### UX Design Requirements` section (or no NFRs), `model.Design`/`model.NonFunctional` is empty — the sub-group and its donut must be **absent, not broken or misleadingly empty** (mirror the existing `if (model.NonFunctional.Count > 0)` guard at line 31). [Source: `_bmad-output/planning-artifacts/epics.md:99` NFR8; memory `epic-4-adapter-contract-scope`]

- [ ] **Task 5 — First-class UX-DR detail pages + linkification (AC: #1)**
  - [ ] `SiteGenerator.WriteRequirements` iterates `requirements.All` to write detail pages — that scope is FR+NFR, so switch this loop to the Design-inclusive `Everything` (from Task 1) so `requirements/ux-dr{n}.html` pages are generated. [Source: src/SpecScribe/SiteGenerator.cs:1177-1185]
  - [ ] `RenderRequirement`'s `kindLabel` currently branches Functional→"Functional Requirement" / else→"Non-Functional Requirement". Add the `Design`→"UX Design Requirement" case so a UX-DR page's kicker is correct. The rest of `RenderRequirement` already works for any `RequirementInfo` (Coverage card, status badge, "← All requirements"). [Source: src/SpecScribe/RequirementsTemplater.cs:92-155]
  - [ ] **Coordinate with Story 9.1 (see Dependency note):** 9.1 rewrites `RenderRequirement`'s Coverage body to list covering **stories** grouped by epic and changes its signature to take `EpicsModel`. If 9.1 has landed, UX-DR pages inherit that story listing for free (their `CoverageEpicNumbers` are now populated). If 9.1 has **not** landed, do not duplicate its work — render the covering epic only (current behavior) and leave the story-listing to 9.1; add a comment noting the two stories share this method.
  - [ ] Extend `RequirementLinkifier.RefPattern` from `\b(FR|NFR)(\d+)\b` to `\b(FR|NFR|UX-DR)(\d+)\b` so "UX-DR25" in prose links to `requirements/ux-dr25.html`. Verify the reconstructed id (`groups[1] + groups[2]` = `"UX-DR25"`) matches `ById` (Task 1 put Design in `ById`) and that `req.Slug` = `"ux-dr25"` yields the right href. Confirm the anchor-skip still holds (a UX-DR already inside an `<a>` is left alone). [Source: src/SpecScribe/RequirementLinkifier.cs:17,42-56]

- [ ] **Task 6 — Conservative back-fill of epic-header coverage (AC: #2) — bounded, evidence-based, no guessing**
  - [ ] To reduce false "Not yet mapped" gaps for work that is clearly delivered (e.g. the UX-DR1–20 design-system / sunburst / a11y set delivered in Epics 1, 3, 6, 7; NFR1–7 touched by their obvious epics), add the requirement id to the delivering epic's header line under `## Epic List` (`**UX-DRs:** …` / `**NFRs:** …`, creating the label if absent). The parser (Task 2) then reflects it automatically — **no code special-casing**, the edit is pure data. [Source: `_bmad-output/planning-artifacts/epics.md:203-273`]
  - [ ] **Guardrail:** tag a requirement to an epic **only** when the epic's own goal/stories make the mapping unambiguous. When uncertain, leave it unmapped — the honest "Not yet mapped" state (Task 3) is the correct, truthful outcome, and a wrong mapping is a truthfulness violation (the project's cardinal sin — Story 1.5, Epic 3 retro). Prefer under-claiming to over-claiming. It is acceptable to surface the proposed mapping list in the completion notes for owner confirmation rather than asserting every one silently.
  - [ ] This edits a planning artifact (epics.md) that is regenerated into the portal — keep edits minimal and within the existing `## Epic List` header format so `EpicsParser`'s meta-line handling is undisturbed. [Source: src/SpecScribe/EpicsParser.cs:21,328-330,389,523-529]

- [ ] **Task 7 — Tests (AC: #1, #2)**
  - [ ] Extend the `MultiEpicEpicsMd` fixture (or add a focused sibling fixture) in `RequirementsAndProgressTests` to include a `### UX Design Requirements` section (e.g. `UX-DR1`, `UX-DR2`) and `## Epic List` header coverage lines (`**UX-DRs:** UX-DR1 · **NFRs:** NFR1` on one epic; leave `UX-DR2`/an NFR untagged for the unmapped case). The current fixture has neither. [Source: tests/SpecScribe.Tests/RequirementsAndProgressTests.cs:95-143]
  - [ ] Parser tests: UX-DRs parse into `model.Design` with ids `UX-DR1`/`UX-DR2` and slugs `ux-dr1`/`ux-dr2`; `ById` resolves a UX-DR id; `All` still equals FR+NFR only. NFR/UX-DR `CoverageEpicNumbers` resolve from the epic-header reverse index (and union with the FR Coverage Map for the NFR that appears in both); `StoriesFor` returns the covering epic's stories for a covered UX-DR and empty for an unmapped one.
  - [ ] Rendering tests on `RenderIndex` HTML: the "Non-functional & design coverage" section exists; a covered NFR/UX-DR shows its rolled-up status badge + a linked epic chip; an **unmapped** NFR/UX-DR shows the "Not yet mapped" badge (icon + word) and **does not** show a bare "Planned" badge; the Design donut and the updated subtitle count appear; with an NFR/UX-DR-free fixture the sub-groups are absent (NFR8 degrade-gracefully).
  - [ ] `RenderRequirement` for a UX-DR: kicker reads "UX Design Requirement"; page links back to `requirements.html`.
  - [ ] `RequirementLinkifier`: "UX-DR25" (present in `ById`) becomes a link to `requirements/ux-dr25.html`; an unknown "UX-DR99" is left as plain text; a UX-DR already inside an `<a>` is untouched. [Source: tests/SpecScribe.Tests/LinkifierTests.cs]
  - [ ] Run the full suite from repo root (`dotnet test`). Confirm no existing FR requirement/traceability test drifts — FR flow/grid/donut output must be byte-identical (that is the whole point of scoping `All` and the FR coverage source narrowly). Watch `RequirementsAndProgressTests`, `SiteGeneratorTraceabilityTests`, `LinkifierTests`, `StatusStylesTests`, `ChartsTests`, `StylesheetTests`.

## Dev Notes

### What exists today (read before touching)

- **`RequirementsTemplater.RenderIndex`** (src/SpecScribe/RequirementsTemplater.cs:10-90) builds `requirements.html`: a Functional/Non-functional donut row, a "Requirements at a glance" status grid (`Charts.RequirementStatusGrid`), a "Requirements flow" Sankey (`Charts.RequirementFlow`), a "Jump to a group" navigator, and grouped requirement-card lists. NFRs are already woven through all of these as one of the groups; UX-DRs appear nowhere. Your new section is **additive** below the flow — do not remove NFRs from the existing surfaces, and do not push UX-DRs into the existing FR-scoped flow/grid.
- **`RequirementsParser`** (src/SpecScribe/RequirementsParser.cs) parses FR/NFR definitions + the FR Coverage Map and rolls status up via `DeriveStatus`/`ForEpic`. `StoriesFor` resolves `CoverageEpicNumbers` → stories. All of this becomes reusable for NFR/UX-DR once their `CoverageEpicNumbers` are populated from the second source.
- **`RequirementInfo`/`RequirementsModel`** (src/SpecScribe/RequirementsModel.cs) — `Id`/`Slug`/`Kind`, `CoverageEpicNumber(s)`, `Deferred`, `Status`; `All`, `ById`, `Empty`.
- **`RenderRequirement`** (src/SpecScribe/RequirementsTemplater.cs:92-155) is shared by every requirement kind; only `kindLabel` is kind-specific. **Story 9.1 is actively changing this method's Coverage body + signature** — coordinate (see Dependency note).
- **`SiteGenerator.WriteRequirements`** (src/SpecScribe/SiteGenerator.cs:1168-1186) writes the index + one detail page per `requirements.All` item and applies `ApplyReferenceLinks` (which runs `RequirementLinkifier`).

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Requirement → covering stories | `RequirementsParser.StoriesFor(req, epics)` | src/SpecScribe/RequirementsParser.cs:164 |
| Epic roll-up → requirement status | `DeriveStatus` (private; reuse, don't fork) | src/SpecScribe/RequirementsParser.cs:185 |
| Requirement lifecycle css-class | `StatusStyles.ForRequirement(req)` | src/SpecScribe/StatusStyles.cs:98 |
| Canonical requirement label | `StatusStyles.RequirementLabel(status)` | src/SpecScribe/StatusStyles.cs:107 |
| Color+icon+word badge (never color-only) | `StatusStyles.Badge(cssClass, label)` | src/SpecScribe/StatusStyles.cs:185 |
| Status donut for a requirement set | `AppendStatusDonut(sb, label, reqs)` | src/SpecScribe/RequirementsTemplater.cs:228 |
| Requirement card shape | `AppendRequirementCard(sb, req, prefix)` | src/SpecScribe/RequirementsTemplater.cs:204 |
| Epic-header meta lines (FRs/NFRs/UX-DRs covered) | `## Epic List` header blocks | `_bmad-output/planning-artifacts/epics.md:201-273` |
| Requirement-id linkification | `RequirementLinkifier` (extend RefPattern) | src/SpecScribe/RequirementLinkifier.cs:17 |

### Guardrails & invariants

- **FR output must stay byte-identical.** The two levers that protect this: keep `RequirementsModel.All` = FR+NFR (so `RequirementFlow`/grid scope is unchanged), and source FR coverage from the FR Coverage Map only (no epic-header union for FRs). Anything else risks silently changing an FR's status. Prove it with the existing FR tests.
- **Status is a single-source system.** Every stage→color decision routes through `StatusStyles` and the six `--status-*` tokens. Never hard-code a status color or re-map status words locally. [Memory `specscribe-status-token-system`]
- **Never color-only (UX-DR17).** The "Not yet mapped" state must pair color with an icon + word via `StatusStyles.Badge`.
- **Deterministic output (NFR8 / CI reproducibility).** No dictionary-iteration-order dependence, timestamps, or per-visitor state in the new markup; the second coverage source must order covering epics deterministically (by appearance). A from-scratch regeneration must be byte-identical. [Source: src/SpecScribe/RequirementsParser.cs:99]
- **Degrade gracefully, don't break (NFR8).** Missing UX-DR section, missing NFRs, an epic header naming a requirement id that has no definition, or a covering-epic number with no epic — all must skip/empty, never throw (mirror `StoriesFor`'s best-effort resolution and the existing `Count > 0` guards).
- **Never dead-end / never over-claim.** Always link to a real detail page; frame coverage as epic-level; leave uncertain mappings honestly unmapped.
- **Accessibility.** New donut gets an `ariaLabel` (Donut convention, Story 1.4). Keep everything within `<main id="main-content">`. [Memory `story-1-4-a11y-seams-for-1-5`]

### Project Structure Notes

- Primary code: `src/SpecScribe/RequirementsModel.cs`, `RequirementsParser.cs`, `RequirementsTemplater.cs`, `RequirementLinkifier.cs`, plus a one-line loop-scope change in `SiteGenerator.cs`. Tests in `tests/SpecScribe.Tests/`. Data-only edit in `_bmad-output/planning-artifacts/epics.md` (Task 6). No new source files expected.
- If new CSS is unavoidable for the coverage section, add it to `src/SpecScribe/assets/specscribe.css` using `--status-*` tokens; `StylesheetTests` guards the stylesheet.
- Output goes to `SpecScribeOutput/` by default when you generate to verify — **not** `docs/live`. [Memory `generate-output-dir-is-specscribeoutput`]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains` / `Assert.DoesNotContain` on generated HTML strings — the established pattern in `RequirementsAndProgressTests` and `SiteGeneratorTraceabilityTests`. Run `dotnet test` from repo root.
- Extend the `MultiEpicEpicsMd` fixture rather than authoring a wholly new epics doc where practical; it already exercises multi-epic (FR2), deferred (FR3), and unmapped (FR4) cases you can mirror for NFR/UX-DR.

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` (`SpecScribeOutput/`), open `requirements.html`, and confirm: the "Non-functional & design coverage" section renders NFRs and UX-DRs with per-item covering-epic chips + canonical badges; an unmapped NFR/UX-DR reads "Not yet mapped" (not "Planned"); the Design donut + updated counts appear; the FR flow/grid look unchanged. Open a `requirements/ux-dr{n}.html` page (correct kicker, working coverage, back link) and confirm a "UX-DR{n}" mention elsewhere in the portal is now a link.

### Dependency note (coordinate, not a blocker)

**Story 9.1 (`9-1-requirement-pages-link-to-their-covering-stories`) is `ready-for-dev`, not yet done, and edits the same `RenderRequirement` method** (Coverage body → covering-stories-grouped-by-epic, plus a signature change to take `EpicsModel`). Both stories touch `RequirementsTemplater`. If 9.1 lands first, UX-DR detail pages inherit its story listing for free once their `CoverageEpicNumbers` are populated here. If this story lands first, keep `RenderRequirement`'s coverage body as-is and let 9.1 layer the story listing on top — do not pre-empt or duplicate 9.1's work. Epic 8's canonical status vocabulary (FR20) is also not yet built; until it is, `StatusStyles` is the canonical status source (same stance as 9.1). Neither is a blocker.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 9.2`] (epics.md:1556-1574) — user story + ACs.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 9`] (epics.md:1530-1534) — epic intent; FR23 (epics.md:185), NFR8 (epics.md:99).
- [Source: `_bmad-output/planning-artifacts/epics.md:126-157`] — the `### UX Design Requirements` section (UX-DR1–30) to parse.
- [Source: `_bmad-output/planning-artifacts/epics.md:161-199`] — the FR Coverage Map (FR + NFR10 only).
- [Source: `_bmad-output/planning-artifacts/epics.md:201-273`] — `## Epic List` epic-header reverse coverage (`**NFRs:**` / `**UX-DRs:**`).
- [Source: src/SpecScribe/RequirementsModel.cs] — `RequirementKind`, `RequirementInfo`, `RequirementsModel` (Id/Slug/All/ById to extend).
- [Source: src/SpecScribe/RequirementsParser.cs] — definition + coverage parsing, `DeriveStatus`, `StoriesFor` (reuse; add UX-DR + second source).
- [Source: src/SpecScribe/RequirementsTemplater.cs] — `RenderIndex` (new section) + `RenderRequirement` (UX-DR kicker).
- [Source: src/SpecScribe/RequirementLinkifier.cs:17] — `RefPattern` to extend for UX-DR.
- [Source: src/SpecScribe/SiteGenerator.cs:1168-1186] — `WriteRequirements` detail-page loop scope.
- [Source: src/SpecScribe/StatusStyles.cs:96-114,182-186] — canonical requirement class/label/badge.
- [Source: tests/SpecScribe.Tests/RequirementsAndProgressTests.cs:95-143] — reusable fixture (extend for UX-DR + epic-header coverage).
- [Source: `_bmad-output/implementation-artifacts/9-1-requirement-pages-link-to-their-covering-stories.md`] — sibling story sharing `RenderRequirement`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
