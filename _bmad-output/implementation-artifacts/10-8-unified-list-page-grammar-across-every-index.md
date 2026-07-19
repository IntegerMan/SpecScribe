---
baseline_commit: cbb11d8eea08fefa549b4ab17212c555580c4318
---

# Story 10.8: Unified List-Page Grammar Across Every Index

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder scanning any index page,
I want every list page — requirements, stories, epics, follow-ups, code files, ADRs, commits — to share one scannable row grammar,
so that I learn the pattern once and read every list the same way.

## Acceptance Criteria

1.
**Given** the Story 9.10 follow-up row grammar as the seed
**When** it is generalized into a shared list primitive
**Then** each index renders through it with consistent row anatomy (primary label, status badge via the canonical `--status-*` tokens, key metadata, deep link) and a designed empty state (Story 8.6)
**And** it does not re-count items against the single-source counts (Story 8.3).

2.
**Given** HTML, webview, and SPA surfaces
**When** a list renders
**Then** all three stay coherent
**And** no index invents a one-off row layout outside the shared primitive.

## Context & Why This Story Exists

Epic 10 makes every surface legible for a first-time visitor. Six index-shaped pages exist today, and **five of them are one-off row grammars** — only the Story 9.10 follow-up family (`action-items.html`, `deferred-work.html`, `follow-ups/group-*.html`) already shares code:

| Index | Renderer (today) | Row markup | Shared? |
|-------|-------------------|------------|---------|
| Action Items / Deferred Work / Follow-up groups | `FollowUpRow.Render` (`FollowUpRow.cs:61`) | `<li class="followup-row">` | **Yes** — the seed (Story 9.10) |
| Requirements index | `RequirementsTemplater.AppendRequirementCard` (`RequirementsTemplater.cs:515`) + `AppendRequirementNfrUxdrRow` (`:459`) | `<div class="req-card {statusClass}">` | No |
| Epics index / story cards | `HtmlRenderAdapter.Epics.AppendEpicCard` (`HtmlRenderAdapter.Epics.cs:120`) / `AppendStoryCard` (`:240`) | `<div class="epic-card">` / `<div class="story-card">` | No |
| Code Map file listing | `CodeMapTemplater.cs` (~198–212) | raw `<table><tr>` | No |
| Code file history table | `CodeFileTemplater.cs` (~294, ~317) | raw `<tr>` | No |
| ADR index | Inline in `SiteGenerator.cs` (~831–868, no dedicated templater) | `<ul class="adr-landing-list"><li>` | No — most primitive |
| Activity timeline / commit days | `TimelineTemplater.cs:62` | `<ol class="timeline-list">` | No |

This story does **not** invent a sixth grammar — it **extracts** the row anatomy `FollowUpRow` already proved out (scan-first summary + status badge + metadata chip + one primary link + designed empty state) into a primitive the other five can call, and rewires them onto it. `FollowUpRow` itself is preserved and becomes a thin caller of the shared primitive (or is left as-is if it already fully expresses the shared shape) — it must not regress the 9.10/9.11 copy-payload and disclosure behavior that ~40 existing tests pin.

Serves the onboarding/legibility mission (FR27–29, UX-DR25/27–30). Load-bearing: **NFR8** (no empty-but-present rows/pages), **Story 8.2** (`StatusStyles` is the only color/word source), **Story 8.3** (`ProjectCounts` is the only count source), **Story 8.6** (designed empty state).

### What the code does today (read before designing)

**The seed — `FollowUpRow.cs`** (static class, not a data record):

```61:105:src/SpecScribe/FollowUpRow.cs
public static void Render(StringBuilder sb, string summaryHtml, string statusToken, string statusLabel,
    string sourceChipHtml, string detailBodyHtml, bool resolved = false, string? detailHref = null)
// Emits <li class="followup-row[ resolved]"> → .followup-row-scan → .followup-row-summary +
// .followup-row-meta (StatusStyles.Badge + .followup-row-source pill + one primary <a>/<details>).
```

CSS family: `.followup-row(-scan|-summary|-meta|-source|-primary|-detail|-detail-body)`, list wrapper `.followup-rows-list`. Consumers: `ActionItemsTemplater.cs`, `DeferredWorkTemplater.cs`, `FollowUpGroupTemplater.cs`, `FollowUpDetailTemplater.cs`.

**Status badges — `StatusStyles.Badge`** (`StatusStyles.cs:281,288`): `Badge(cssClass, label)` and `Badge(cssClass, label, iconClass)` — the ONLY place color/icon/tooltip come from; never hand-roll a badge span.

**Counts — `ProjectCounts`** (`ProjectCounts.cs:14`): "THE single generator-side authority ... never re-counted at a render site." `FollowUpGeometry.From` enforces this with a `Debug.Assert` comparing any locally-derived count against `counts.OpenActionItems` (`FollowUpGeometry.cs:121`) — follow this pattern for any new row-count display.

**Empty states — a CSS convention, not a helper class today** (Story 8.6): compose `.empty-state` (`specscribe.css:4031`) onto a card class + a `.pending-note` div, e.g. `AppendEmptyEpicsGuidance` (`HtmlRenderAdapter.Epics.cs:146-153`) → `<div class="epic-card empty-state"><div class="pending-note">{note}</div></div>`. Sibling dashed/muted convention: `.status-badge.evidence-pill.empty` (`specscribe.css:511`). **This story is the first natural place to promote it into a real shared helper** since every generalized list needs one.

**Render surfaces — `IRenderAdapter` / `HtmlRenderAdapter` / `RenderParity`:** Epics/Dashboard pages go through the one shared `HtmlRenderAdapter` body-render path consumed identically by HTML, the VS Code webview adapter, and the SPA adapter (`IRenderAdapter.cs`, `RenderParity.cs` — a semantic, not byte, differ over `SemanticFacts`/`SectionFacts`). Follow-ups/ADR/Code Map/Code File/Timeline are **standalone `StringBuilder` templaters** that hit disk via `SiteGenerator.WriteOutput` and get sliced into SPA/webview via the `main#main-content` capture seam (no `PageView`/`RenderParity` involvement) — same pattern Story 9.10 used. **Practical consequence: dropping the shared primitive into one place propagates to all three surfaces by construction for BOTH families** — you do not need adapter-specific wiring, only (for the Epics/Dashboard family) confirm `RenderParity`'s extractors still see the row facts they expect if row markup shape changes.

## Design Direction (default reuse pattern — read before designing)

**Do NOT rename `.followup-row*` CSS classes or `FollowUpRow`'s public signature** — ~40 existing tests (`FollowUpSurfacesTests`, `StylesheetTests`) pin that exact grammar and the 9.10/9.11 copy-payload + disclosure behavior is load-bearing. Instead:

1. **Extract, don't replace.** Add a new shared primitive (e.g. `ListRow.cs`, sibling of `FollowUpRow.cs`) expressing the common anatomy: primary label/summary, `StatusStyles.Badge` call, ≤2 metadata chips, one primary link, and a designed empty-state helper. Give it a neutral base CSS class family (e.g. `.list-row`, `.list-row-summary`, `.list-row-meta`, `.list-row-primary`, `.list-rows-list`, `.list-row.empty-state`).
2. **Rewire `FollowUpRow.Render` to call the shared primitive internally** (or confirm it already expresses the same shape and leave its own class names untouched, additively co-emitting the new base class alongside `.followup-row` so `.followup-row` keeps matching every existing test/CSS rule while also participating in any new shared CSS). Either approach is acceptable — the constraint is: **zero behavior change to the follow-up family**, confirmed by the existing suite staying green without edits to `FollowUpSurfacesTests`/`StylesheetTests` assertions (beyond additive ones).
3. **Rewire the other five one-off renderers onto the shared primitive:** Requirements cards, Epics/Story cards, Code Map table rows, Code File history rows, ADR list items, Timeline/commit-day entries. Each keeps its own status vocabulary (`StatusStyles.For*`) and metadata fields but stops hand-rolling `<div class="...-card">`/`<tr>`/`<li>` markup outside the shared call.
4. **Promote the empty-state convention into a real helper** (e.g. `ListRow.EmptyState(string message)` wrapping the existing `.empty-state` + `.pending-note` CSS pattern) and route every list's NFR8 degrade path through it instead of each surface hand-rolling its own empty markup.
5. **Tables (Code Map, Code File history) are still "lists"** — either restyle their rows to emit the shared row markup inside `<tr>`/wrap them as a `<ul>` (owner call: whichever preserves the existing sortable/scannable table semantics with the least churn), or, if a genuine `<table>` header row is load-bearing for that page (verify against any accessibility text-equivalent requirement), keep `<table>` semantics but route the **status badge + primary link** through the exact same `StatusStyles`/primitive calls other rows use, so the *visual* grammar (badge shape, link style) still reads as one system even if the DOM tag differs. Do not silently drop the accessible table structure to force `<li>` shape.

## Tasks / Subtasks

- [ ] **Task 1 — Extract the shared list-row primitive** (AC: 1)
  - [ ] Add `ListRow.cs` (or extend `FollowUpRow.cs` in place if a genuine single-file extraction reads cleaner — do not create two divergent implementations) expressing: summary/label, `StatusStyles.Badge` call, metadata chip slot(s), one primary link, empty-state helper.
  - [ ] Confirm `FollowUpRow`'s existing behavior is unchanged (byte-identical output) — run `FollowUpSurfacesTests` before and after with no assertion edits beyond additive ones.
  - [ ] Add the promoted empty-state helper; reuse `.empty-state` + `.pending-note` CSS, do not invent new empty-state chrome.

- [ ] **Task 2 — Requirements index onto the shared primitive** (AC: 1, 2)
  - [ ] Rework `RequirementsTemplater.AppendRequirementCard` (`:515`) and `AppendRequirementNfrUxdrRow` (`:459`) to call the shared primitive for badge + primary link + metadata; keep `req-card`-specific layout (coverage bars, etc.) that isn't part of the shared row anatomy.
  - [ ] Confirm Story 9.9's satisfaction band / `ProjectCounts.RequirementSatisfaction` counts are untouched — no local recount.

- [ ] **Task 3 — Epics index + story cards onto the shared primitive** (AC: 1, 2)
  - [ ] Rework `HtmlRenderAdapter.Epics.AppendEpicCard` (`:120`) and `AppendStoryCard` (`:240`) to route status badge + primary link + metadata through the shared primitive; keep epic/story-specific extras (progress donuts, evidence strip) that sit outside the row anatomy.
  - [ ] Rework `AppendEmptyEpicsGuidance` (`:146`) onto the promoted empty-state helper.
  - [ ] Confirm `RenderParity` stays green — this family rides the shared `HtmlRenderAdapter`/`PageView` path; if row-shape changes trip a `SectionFacts` extractor, update the extractor, don't special-case markup.

- [ ] **Task 4 — Code Map + Code File listings onto the shared primitive** (AC: 1, 2)
  - [ ] Rework `CodeMapTemplater.cs` (~198–212) file-listing rows and `CodeFileTemplater.cs` (~294, ~317) history rows per the table-vs-list guidance above (Design Direction #5) — badge/primary-link parity at minimum.
  - [ ] Preserve existing sortability/columns and any accessible text-equivalent table already present (Story 7.6/7.8 built these — don't regress their a11y work).

- [ ] **Task 5 — ADR index onto the shared primitive** (AC: 1, 2)
  - [ ] Extract the inline `SiteGenerator.cs` ADR list (~831–868) into a call on the shared primitive (this is the most primitive of the six — pure win, no competing layout concerns).
  - [ ] Preserve `AdrEntry.Date`/`Summary` (Story 10.4) and superseded/deprecated status pills.

- [ ] **Task 6 — Timeline / commit-day listing onto the shared primitive** (AC: 1, 2)
  - [ ] Rework `TimelineTemplater.cs:62` day entries onto the shared primitive (status badge may not apply here — commits don't carry lifecycle status; use the primitive's non-status-badge path, i.e. summary + metadata + link only, or confirm a badge-less variant exists).
  - [ ] Leave `CommitDayTemplater.cs` per-commit detail rendering alone unless it also duplicates row anatomy the shared primitive already covers.

- [ ] **Task 7 — Guardrails** (AC: 1, 2)
  - [ ] No index re-counts against `ProjectCounts` — every count shown reads the existing ledger field.
  - [ ] No new `--status-*` token; every badge routes through `StatusStyles`.
  - [ ] NFR8 unchanged: absent data → absent page/section, never an empty-but-present list.
  - [ ] No adapter-specific special-casing — the shared primitive lives in one place and both the `HtmlRenderAdapter` family and the standalone-`WriteOutput` family call it identically.

- [ ] **Task 8 — Tests + golden** (AC: 1, 2)
  - [ ] Extend/add tests per surface confirming shared-primitive markup (badge via `StatusStyles`, one primary link, empty state via the promoted helper) without regressing existing per-page assertions (grouping/order, copy-payload, degrade-to-absent).
  - [ ] `FollowUpSurfacesTests` / `StylesheetTests` stay green with no non-additive edits (proves the follow-up family didn't regress).
  - [ ] Golden fingerprint moves (every reworked page body changes) → regen `SiteGeneratorAdapterTests.cs`'s expected hash, confirming stability across ≥2 repeated runs before locking it in (known stale-first-hash trap). `RenderParity` + SPA/webview suites green.
  - [ ] `dotnet test` from repo root.

- [ ] **Task 9 — Verify end-to-end** (AC: 1, 2)
  - [ ] Generate against this repo's own history (`dotnet run --project src/SpecScribe -- generate --deep-git`); open requirements.html, epics.html, an epic page, code-map.html, a code file page, the ADR index, and timeline.html — confirm each reads with the same row grammar (badge shape, link style, metadata chip placement) at a glance.
  - [ ] Confirm empty-state pages (if any surface in this repo currently has zero items in a list) degrade via the promoted helper, not a hand-rolled div.

## Dev Notes

### Architecture patterns & constraints (must follow)

- **Extend, don't duplicate** (repo-wide principle reaffirmed in 10.2/10.6/10.7) — the shared primitive must be the ONE place row anatomy is defined; no per-page copy of badge/link/empty-state markup.
- **`StatusStyles` is the only color/word source** (Story 8.2) — never hand-roll a status span.
- **`ProjectCounts` is the only count source** (Story 8.3) — mirror `FollowUpGeometry.From`'s `Debug.Assert` pattern if a new row surfaces a count.
- **NFR8** — absent, not empty-but-present; the promoted empty-state helper is additive, not a new failure mode.
- **Never color-only** (UX-DR17) — badges already carry icon + word via `StatusStyles.Badge`; preserve that when routing other pages through it.
- **Three-surface coherence (AC #2):** confirm both render families (shared `HtmlRenderAdapter`/`PageView` path vs standalone `WriteOutput`/capture path) after the change — `RenderParity` for the former, existing SPA/webview capture assertions for the latter.
- **Copy-payload trap (inherited from 9.10):** `ActionItemsTemplater`'s `data-copy` Resolve-with-AI payload must stay raw/un-linkified — do not let the shared primitive's summary rendering introduce a linkify pass on that page.

### Source tree — files to touch

| File | Change |
|------|--------|
| `src/SpecScribe/FollowUpRow.cs` | Extract shared primitive from, or wire onto a new sibling (**UPDATE**, behavior-preserving) |
| `src/SpecScribe/ListRow.cs` (new, name illustrative) | New shared list-row primitive + empty-state helper (**NEW**) |
| `src/SpecScribe/RequirementsTemplater.cs` | Route `AppendRequirementCard`/`AppendRequirementNfrUxdrRow` through primitive (**UPDATE**) |
| `src/SpecScribe/HtmlRenderAdapter.Epics.cs` | Route `AppendEpicCard`/`AppendStoryCard`/`AppendEmptyEpicsGuidance` through primitive (**UPDATE**) |
| `src/SpecScribe/CodeMapTemplater.cs` | Route file-listing rows through primitive (**UPDATE**) |
| `src/SpecScribe/CodeFileTemplater.cs` | Route history rows through primitive (**UPDATE**) |
| `src/SpecScribe/SiteGenerator.cs` | Extract ADR list (~831–868) onto primitive (**UPDATE**) |
| `src/SpecScribe/TimelineTemplater.cs` | Route day entries through primitive (**UPDATE**) |
| `src/SpecScribe/assets/specscribe.css` | New `.list-row*` base classes; keep `.followup-row*` intact (**UPDATE**) |
| `tests/SpecScribe.Tests/*` | Per-surface assertions + golden regen (**UPDATE**) |

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Follow-up row seed | `FollowUpRow.Render` | `FollowUpRow.cs:61` |
| Status badge | `StatusStyles.Badge(cssClass, label[, iconClass])` | `StatusStyles.cs:281,288` |
| Ledger counts | `ProjectCounts` | `ProjectCounts.cs:14` |
| Count-agreement guard pattern | `Debug.Assert` in `FollowUpGeometry.From` | `FollowUpGeometry.cs:121` |
| Empty-state CSS convention | `.empty-state` + `.pending-note` | `specscribe.css:4031`; `HtmlRenderAdapter.Epics.cs:146-153` |
| Dates | `PortalDates` | (Story 10.4) |
| Write + SPA/webview capture | `SiteGenerator.WriteOutput` | `SiteGenerator.cs` |
| Shared body-render path (Epics/Dashboard family) | `IRenderAdapter` / `HtmlRenderAdapter` / `RenderParity` | `IRenderAdapter.cs`, `RenderParity.cs` |

### Guardrails & invariants

- **Behavior-preservation on the follow-up family is the #1 review checkpoint** — the 9.10/9.11 copy-payload boundary, disclosure-vs-detail-link seam, and grouping/order must not move.
- **Split, don't absorb.** This story does not change grouping/ordering rules any individual page owns (e.g. requirements coverage grouping, epic story ordering, ADR chronological order, timeline day-set derivation) — only row *anatomy* changes.
- **No new authoring schema.** Every field the shared primitive displays already exists on today's view models.
- **No new `--status-*` token; never color-only.**

### Previous story intelligence

- **From 9.10 (done):** `FollowUpRow` is a static-method renderer, not a data record — mirror that pragmatic style rather than over-engineering a generic view-model type. The copy-payload trap (`data-copy` must stay raw) is the concrete failure mode to avoid regressing.
- **From 10.7 (review):** Golden fingerprint regen has a known stale-first-hash trap — confirm a hash is stable across ≥2 repeated runs before locking it in as the expected constant ([golden-diff-normalization-gotchas]).
- **From 8.2/8.3 (done):** `StatusStyles`/`ProjectCounts` are the two seams every new surface must route through — both already have enforcement precedent (`Debug.Assert` in `FollowUpGeometry`) worth mirroring for any new count display.

### Testing standards

- xUnit; `Assert.Contains`/`DoesNotContain` on emitted HTML, matching the `FollowUpSurfacesTests` pattern.
- Full suite green including golden + `RenderParity` + SPA/webview parity suites.

### Project Structure Notes

- No new authoring schema, no `sprint-status.yaml`/epics.md shape changes.
- Follow existing per-file `Templater`/`HtmlRenderAdapter.*` naming and static-class style; do not introduce a competing MVC-style view-model layer for this.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.8]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10] — FR27–29, UX-DR25/27–30, NFR8
- [Source: _bmad-output/implementation-artifacts/9-10-scannable-follow-up-list-pages.md] — the seed grammar + copy-payload trap
- [Source: _bmad-output/implementation-artifacts/8-2-canonical-status-model-with-portal-wide-legend.md] — `StatusStyles`
- [Source: _bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md] — `ProjectCounts`
- [Source: _bmad-output/implementation-artifacts/8-6-designed-empty-states.md] — empty-state convention
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md] — SCP seating this story
- [Source: src/SpecScribe/FollowUpRow.cs]
- [Source: src/SpecScribe/StatusStyles.cs]
- [Source: src/SpecScribe/ProjectCounts.cs]
- [Source: src/SpecScribe/RequirementsTemplater.cs]
- [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs]
- [Source: src/SpecScribe/CodeMapTemplater.cs] / [src/SpecScribe/CodeFileTemplater.cs]
- [Source: src/SpecScribe/SiteGenerator.cs#ADR landing list]
- [Source: src/SpecScribe/TimelineTemplater.cs]
- [Source: src/SpecScribe/IRenderAdapter.cs] / [src/SpecScribe/RenderParity.cs]
- [Source: tests/SpecScribe.Tests/FollowUpSurfacesTests.cs] / [tests/SpecScribe.Tests/StylesheetTests.cs]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. This story generalizes Story 9.10's `FollowUpRow` grammar into one shared list-row primitive reused by Requirements, Epics/Stories, Code Map, Code File, ADR, and Timeline indexes, routing status through `StatusStyles` (8.2), counts through `ProjectCounts` (8.3), and empty states through the promoted 8.6 convention — without regressing the follow-up family's existing copy-payload and disclosure behavior.

### File List
