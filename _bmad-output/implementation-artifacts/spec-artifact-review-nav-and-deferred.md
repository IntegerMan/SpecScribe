---
title: 'Artifact code-review navigation and deferred listing'
type: 'feature'
created: '2026-07-17'
status: 'done'
baseline_commit: 'dabcdbad20a9295293c46a48ee5f35cced6bfc59'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Portal pages for epic stories and quick-dev specs (`spec-*.md`) do not surface the deferred-work items that came out of reviewing them, and quick-dev pages lack navigable parent context (epic when inferable, otherwise root-level Unplanned / quick work). Deferred → source already works; the reverse path — and quick-dev parent chrome — do not, so pages like `spec-sprint-board-card-tooltip-html-corruption` feel stranded from their review fallout and work geometry.

**Approach:** On both story and quick-dev portal pages, add parent/context navigation plus a reverse-indexed list of deferred items associated via existing `source_spec` / `## Deferred from: code review of …` provenance. Attribute quick-dev to an epic using observable signals already in the project (text heuristics first; then best-effort sprint/timing cues from existing dates); when attribution fails, treat the item as root-level Unplanned quick work. No new authoring schema.

## Boundaries & Constraints

**Always:**
- Cover epic story artifact pages and quick-dev (`route: one-shot`) pages.
- Associate deferred items only from existing deferred-work provenance (`source_spec`, group `SourceKey` / heading keys) — reverse index, do not invent links.
- Prefer epic parent when attribution resolves; otherwise Unplanned / root-level quick-work parent chrome.
- Degrade to absent (NFR8): omit deferred panel when none match; omit epic crumb when unattributed.
- Keep deferred → source links and Story 9.6/9.11/9.12 geometry coherent (same membership / ledger ideas; no parallel recount).
- Story pages keep existing Home → Epics → Epic → Story breadcrumbs; extend rather than replace.

**Ask First:**
- Any change that would require authors to add new frontmatter or rewrite deferred-work headings.
- Attribution rules that would force a single epic when multiple epics tie on timing evidence (prefer leave Unplanned over guessing).

**Never:**
- New authoring conventions or required `source_spec` / epic fields.
- Separate “code review document” artifact type.
- Mislabeling quick-dev as stories in pipeline/counts.
- Breaking existing deferred detail/list provenance or sunburst/sprint Unplanned membership contracts from 9.12/9.13.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Story with deferred | Story `9-8-…` and deferred group `SourceKey` / `source_spec` naming it | Story page lists those deferred items (open + resolved distinguishable) with links to detail pages | Missing detail href → list page / omit link, never throw |
| Quick-dev with deferred | `spec-sprint-board-card-tooltip-html-corruption` + deferred group for that stem | Spec page lists the deferred item(s); parent chrome shows Unplanned or epic | NFR8 omit panel if zero matches |
| Quick-dev epic-attributed | Title/filename/deferred cue or unique timing cue resolves one epic | Breadcrumb/parent links to that epic (and Unplanned only if also residual-unplanned UX needs it — prefer epic) | Multi-epic tie → Unplanned, no forced pick |
| Quick-dev unattributed | No epic cue | Parent = root-level Unplanned / quick work (link to Unplanned group or sprint Unplanned when generated) | Absent Unplanned surface → Home / docs parent only |
| No deferred | Story/spec with no matching deferred | No deferred panel | N/A |
| Unstructured deferred note | Parser not structured | No reverse list (absent), generation continues | NFR2 never throw |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/FollowUpGeometry.cs` -- `FollowUpDeferredSlot.SourceKey` / `SourceHref`; add reverse lookup by source key / story id
- `src/SpecScribe/DeferredWorkParser.cs` / `FollowUpRefs.cs` -- existing provenance extractors (`SourceKey`, `source_spec`); reuse, do not fork
- `src/SpecScribe/UnplannedWorkGeometry.cs` -- `ResolveQuickDevEpic`; extend with optional timing using observable dates only
- `src/SpecScribe/EpicsTemplater.cs` / `EpicsViewBuilder.cs` / `EpicsView.cs` / `HtmlRenderAdapter.Epics.cs` -- story page chrome + deferred panel
- `src/SpecScribe/HtmlTemplater.cs` / `SiteGenerator.cs` -- quick-dev doc page breadcrumbs + deferred panel at write time
- `src/SpecScribe/FollowUpDetailTemplater.cs` / `DeferredWorkTemplater.cs` -- precedents for deferred row/chip linking (forward path already correct)
- `src/SpecScribe/Frontmatter.cs` -- optional: observe already-authored `created`/`date` if timing needs it (parse existing fields only)
- `tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs` / `FollowUpSurfacesTests.cs` / `HtmlRenderAdapterTests` / `HtmlTemplaterTests` -- extend

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/FollowUpGeometry.cs` -- add reverse lookup (source key / story id → deferred slots with detail hrefs) -- powers both page types from one ledger-backed set
- [x] `src/SpecScribe/UnplannedWorkGeometry.cs` (+ `Frontmatter.cs` only if needed) -- strengthen epic attribution: keep text heuristics first; add best-effort unique timing from observable dates; ties → null -- matches owner rule for sprint-period inference without new schema
- [x] `src/SpecScribe/EpicsView.cs` / `EpicsViewBuilder.cs` / `EpicsTemplater.cs` / `HtmlRenderAdapter.Epics.cs` / `SiteGenerator.cs` -- pass follow-ups into story pages; render deferred-from-this-artifact panel -- closes reverse path for stories
- [x] `src/SpecScribe/HtmlTemplater.cs` / `SiteGenerator.cs` -- for quick-dev docs, richer parent breadcrumb (epic or Unplanned) + same deferred panel -- closes the live example gap
- [x] `tests/SpecScribe.Tests/*` -- cover I/O matrix: reverse list match/omit, attribution hit/miss/tie, NFR8 absent panels -- pins edge cases

**Acceptance Criteria:**
- Given a story or quick-dev page whose deferred-work groups name it via existing provenance, when the portal generates, then that page lists those deferred items with working links to their detail (or list) URLs, without requiring authoring changes.
- Given a quick-dev page, when an epic is uniquely inferable from existing text or timing signals, then parent navigation targets that epic; when not, then parent navigation targets root-level Unplanned / quick work (or degrades cleanly if that surface is absent).
- Given no matching deferred items, when the page renders, then no empty deferred panel appears (NFR8).

## Design Notes

Reverse index should normalize keys the same way as `UnplannedWorkGeometry` / deferred `SourceKey` (strip `.md`/`.html`, ordinal-ignore-case stem match) so `source_spec: \`spec-….md\`` and heading `code review of spec-…` both hit.

Parent chrome for quick-dev is additive on the generic doc template — do not invent a second page pipeline. Story breadcrumbs stay; only add the deferred panel (and any small “origin” cue if already implied by provenance).

Timing attribution is best-effort and last. Prefer signals already present: deferred group dates next to reviews of stories under one epic, and already-authored frontmatter dates on the quick-dev file if parsed. If two epics look equally plausible, leave Unplanned.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~UnplannedWorkGeometry|FullyQualifiedName~FollowUp|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~HtmlRenderAdapter"` -- expected: new/updated cases green
- `dotnet test` -- expected: full suite green (regen golden fingerprints only if CSS/HTML chrome intentionally changes)

**Manual checks:**
- Regenerate and open `implementation-artifacts/spec-sprint-board-card-tooltip-html-corruption.html`: parent crumb present (Unplanned or epic); deferred item for RequirementLinkifier exposure listed and clickable.
- Open a done story that has a `## Deferred from: code review of …` group: deferred panel present; epic breadcrumb unchanged.

## Suggested Review Order

**Reverse index**

- Entry point: source-key match for story id and spec stem
  [`FollowUpGeometry.cs:164`](../../src/SpecScribe/FollowUpGeometry.cs#L164)

- Second pass so quick-dev deferred inherit epic with timing/cues available
  [`FollowUpGeometry.cs:273`](../../src/SpecScribe/FollowUpGeometry.cs#L273)

**Epic attribution**

- Text first; unique deferred cue; timing only with named residual
  [`UnplannedWorkGeometry.cs:141`](../../src/SpecScribe/UnplannedWorkGeometry.cs#L141)

- Observe existing `created` / ISO datetime for timing
  [`Frontmatter.cs:44`](../../src/SpecScribe/Frontmatter.cs#L44)

**Story + quick-dev chrome**

- Story page reverse panel + list href fallback
  [`HtmlRenderAdapter.Epics.cs:543`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L543)

- Shared panel: omit empty disclosure; list fallback when detail missing
  [`FollowUpRow.cs:100`](../../src/SpecScribe/FollowUpRow.cs#L100)

- Rewrite one-shots with epic/Unplanned crumbs after geometry exists
  [`SiteGenerator.cs:2838`](../../src/SpecScribe/SiteGenerator.cs#L2838)

- Additive quick-dev crumbs on the generic doc template
  [`HtmlTemplater.cs:91`](../../src/SpecScribe/HtmlTemplater.cs#L91)

**Tests**

- Timing requires named residual; ISO `created`; reverse lookup
  [`UnplannedWorkGeometryTests.cs:318`](../../tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs#L318)

- Quick-dev chrome / NFR8 omit empty panel
  [`HtmlTemplaterTests.cs:995`](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L995)
