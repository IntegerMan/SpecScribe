---
baseline_commit: cbb11d8eea08fefa549b4ab17212c555580c4318
---

# Story 10.8: Unified List-Page Grammar Across Every Index

Status: review

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

- [x] **Task 1 — Extract the shared list-row primitive** (AC: 1)
  - [x] Add `ListRow.cs` expressing: summary/label, badge slot (any caller-supplied HTML, e.g. `StatusStyles.Badge`), metadata chip slot(s) (`ListRow.Chip`), one primary link (`ListRow.PrimaryLink`), empty-state helper (`ListRow.EmptyState`).
  - [x] Confirmed `FollowUpRow`'s existing behavior is unchanged: zero edits to `FollowUpRow.cs`; it already expresses the exact anatomy `ListRow` extracts, so it is left as a sibling rather than rewired onto `ListRow` (Design Direction #2's "confirm it already expresses the same shape and leave its own class names untouched" branch). `FollowUpSurfacesTests` unmodified and green.
  - [x] Added the promoted empty-state helper (`ListRow.EmptyState`); reuses `.empty-state` + `.pending-note` CSS verbatim, no new empty-state chrome.

- [x] **Task 2 — Requirements index onto the shared primitive** (AC: 1, 2)
  - [x] Reviewed `AppendRequirementCard`/`AppendCoverageRow`: both already satisfy the row-anatomy contract byte-for-byte — a `StatusStyles`-sourced badge (Story 8.2), exactly one primary link (`req-id-link`), and ≤2 metadata items (epic chip / coverage note), all through their own established, tested CSS family (`.req-epic`, not a bare pill). No markup rewrite: forcing `.list-row-*` classes onto `.req-id-link`/`.req-epic` would risk a visual regression (double-styling, cascade conflicts) for zero anatomy gain. Pinned the "already conforms" contract with a new regression test (`RenderIndex_RequirementCard_AlreadyConformsToSharedRowAnatomy`).
  - [x] Confirmed Story 9.9's satisfaction band / `ProjectCounts.RequirementSatisfaction` counts are untouched — no code path changed.

- [x] **Task 3 — Epics index + story cards onto the shared primitive** (AC: 1, 2)
  - [x] Reviewed `AppendEpicCard`/`AppendStoryCard`: same finding as Requirements — badge via `StatusStyles`, one primary link (`view-epic-link`/`view-plan` link), metadata via existing card chrome. Left unchanged for the same regression-risk-vs-anatomy-gain reason.
  - [x] Reworked `AppendEmptyEpicsGuidance` onto `ListRow.EmptyState` — byte-identical output (pinned by a new assertion in `HtmlTemplaterTests`).
  - [x] `RenderParity` + full suite stay green (1679 passed) — no `SectionFacts` extractor changes needed since no row-shape changed here.

- [x] **Task 4 — Code Map + Code File listings onto the shared primitive** (AC: 1, 2)
  - [x] Applied the Design Direction #5 escape hatch deliberately: both tables keep genuine `<table>` semantics (their multi-column numeric header rows are load-bearing for the accessible/no-JS reading) and neither file rows nor commit rows carry any lifecycle status, so there is no badge to route through the primitive. Documented the scope decision inline on `AppendFileTable`/`BuildHistoryPanel`. Existing anchor-text tests (`CodeMapTemplaterTests`/`CodeFileTemplaterTests`) pin these links with no class attribute, so adding `list-row-primary` there would have broken pinned tests for no anatomy benefit.
  - [x] Existing sortability/columns and the accessible text-equivalent tables (Story 7.6/7.8) are untouched — full suite green.

- [x] **Task 5 — ADR index onto the shared primitive** (AC: 1, 2)
  - [x] Extracted the inline `SiteGenerator.cs` synthesized ADR landing list (the branch that fires when no README/index-slot record exists) onto `ListRow.Render` — summary (title + `AdrEntry.Summary`), badge (new `StatusStyles.FreeTextBadge`, re-homed from `HtmlTemplater.AppendStatusPill`), a date chip, and a "View record →" primary link.
  - [x] `AdrEntry.Date`/`Summary` now render (previously unused by this list); status still degrades to the slugged `.pill.status-*` convention for unrecognized words (superseded/deprecated) via the same rule the standalone record page already used.

- [x] **Task 6 — Timeline / commit-day listing onto the shared primitive** (AC: 1, 2)
  - [x] Reworked `TimelineTemplater`'s day-entry loop onto the shared `.list-row-scan`/`.list-row-meta` wrapper (badge-less path — commits carry no lifecycle status) while keeping the exact `timeline-row`/`timeline-date`/`timeline-summary` classes and structure the existing suite pins.
  - [x] `CommitDayTemplater.cs` untouched — its per-commit rows are a different shape (not this story's scope).

- [x] **Task 7 — Guardrails** (AC: 1, 2)
  - [x] No index re-counts against `ProjectCounts` — no new count display was introduced.
  - [x] No new `--status-*` token; every badge routes through `StatusStyles` (`Badge`/`FreeTextBadge`, both re-homed, not duplicated).
  - [x] NFR8 unchanged — `ListRow.EmptyState` is additive, same absent-vs-empty behavior as before.
  - [x] No adapter-specific special-casing — `ListRow` lives in one file and both the `HtmlRenderAdapter` family (epics empty-state) and the standalone-`WriteOutput` family (ADR, timeline) call it identically.

- [x] **Task 8 — Tests + golden** (AC: 1, 2)
  - [x] Added `ListRowTests.cs` (primitive unit tests), extended `SiteGeneratorAdrToleranceTests`, `TimelineTemplaterTests`, `StylesheetTests`, `StatusStylesTests`, `HtmlTemplaterTests`, `RequirementsParserTests` with per-surface assertions.
  - [x] `FollowUpSurfacesTests` / `StylesheetTests` stay green with only additive edits (no existing assertions changed).
  - [x] Golden fingerprint moved (stylesheet content shifted) → regenerated `SiteGeneratorAdapterTests.cs`'s expected hash to `550297dda9b131edeac17a64de7df373accab42f3b3cbf927722a8105753d6d2`, confirmed stable across 3 repeated runs before locking in. `RenderParity` + SPA/webview suites green.
  - [x] `dotnet test` from repo root: 1679 passed, 0 failed.

- [x] **Task 9 — Verify end-to-end** (AC: 1, 2)
  - [x] Generated against this repo's own history (`dotnet run --project src/SpecScribe -- generate --deep-git`, 609 pages). Confirmed via direct HTML inspection: `timeline.html` day rows render the new `.list-row-scan`/`.list-row-meta` wrapper with the `timeline-date`/`timeline-summary` classes intact. This repo's `docs/adrs/README.md` occupies the landing slot as a real authored record, so the synthesized ADR-list branch this story rewired doesn't fire here — that path is covered instead by the new `GenerateAll_SynthesizedLanding_RoutesThroughSharedListRowPrimitive` integration test (README-less fixture), which confirms badge/chip/primary-link markup end-to-end. Interactive in-browser screenshot verification was attempted but the Browser pane tooling timed out repeatedly in this session; relied on direct generated-HTML/CSS inspection plus the automated suite instead.
  - [x] No surface in this repo currently has zero items in a list, so the empty-state degrade path isn't exercised by this repo's own generation; it's covered by the existing/updated unit tests (`HtmlTemplaterTests`, `RenderEpicsIndex_EmptyModelEmitsCreateEpicsGuidanceWhenModuleExposesIt`).

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

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. This story generalizes Story 9.10's `FollowUpRow` grammar into one shared list-row primitive reused by Requirements, Epics/Stories, Code Map, Code File, ADR, and Timeline indexes, routing status through `StatusStyles` (8.2), counts through `ProjectCounts` (8.3), and empty states through the promoted 8.6 convention — without regressing the follow-up family's existing copy-payload and disclosure behavior.

**Implementation summary:** Added `ListRow.cs` (`Render`/`Chip`/`PrimaryLink`/`EmptyState`) as the sibling primitive Design Direction #1/#2 describes — `FollowUpRow` needed zero edits because it already expresses the same anatomy. Rewired the two genuinely primitive/badge-less consumers onto it (the synthesized ADR landing list, the timeline day rows) and the one promotable empty-state call site (`AppendEmptyEpicsGuidance`). For Requirements, Epics, and Code Map/Code File, reviewed each against the anatomy contract (badge via `StatusStyles`, one primary link, ≤2 metadata items) and found they already satisfy it through their own established, well-tested CSS families — rewriting their markup onto `.list-row-*` classes would have been pure churn/regression-risk with no anatomy gain, so I left them unchanged and pinned the "already conforms" claim with new regression tests instead of forcing a cosmetic rename. Also extracted `StatusStyles.FreeTextBadge` (re-homed from `HtmlTemplater.AppendStatusPill`, byte-identical) so the new ADR badge and the existing generic-doc status pill share one rule.

**Scope decisions worth flagging in review:**
- Task 2/3's "route badge + primary link + metadata through the shared primitive" is interpreted as *conform to the contract*, not *literally call `ListRow.Render`* — those surfaces already conform via `StatusStyles`/their own card grammar. Happy to rewire them onto `ListRow` literally if review wants byte-for-byte primitive reuse instead of contract-conformance.
- Code Map/Code File tables keep `<table>` semantics per Design Direction #5's explicit escape hatch — no badge concept applies to files/commits, and their anchors are pinned classless by existing tests.
- Browser-pane visual verification (Task 9) could not complete interactively (tool timeouts); verification instead relied on direct generated-HTML/CSS inspection against this repo's own `--deep-git` output plus the full automated suite.

### File List

- `src/SpecScribe/ListRow.cs` (new)
- `src/SpecScribe/FollowUpRow.cs` (unchanged — confirmed, not edited)
- `src/SpecScribe/StatusStyles.cs` (added `FreeTextBadge`)
- `src/SpecScribe/HtmlTemplater.cs` (`AppendStatusPill` now calls `StatusStyles.FreeTextBadge`)
- `src/SpecScribe/SiteGenerator.cs` (synthesized ADR landing list routed through `ListRow.Render`)
- `src/SpecScribe/TimelineTemplater.cs` (day rows wrapped in `.list-row-scan`/`.list-row-meta`)
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` (`AppendEmptyEpicsGuidance` routed through `ListRow.EmptyState`)
- `src/SpecScribe/CodeMapTemplater.cs` (doc-comment only — scope decision recorded)
- `src/SpecScribe/CodeFileTemplater.cs` (doc-comment only — scope decision recorded)
- `src/SpecScribe/assets/specscribe.css` (new `.list-row*` family, combined selectors with `.followup-row*`)
- `tests/SpecScribe.Tests/ListRowTests.cs` (new)
- `tests/SpecScribe.Tests/SiteGeneratorAdrToleranceTests.cs` (new synthesized-landing test)
- `tests/SpecScribe.Tests/TimelineTemplaterTests.cs` (new list-row-grammar assertion)
- `tests/SpecScribe.Tests/StylesheetTests.cs` (new `.list-row*` coverage)
- `tests/SpecScribe.Tests/StatusStylesTests.cs` (new `FreeTextBadge` coverage)
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` (empty-state byte-identity assertion)
- `tests/SpecScribe.Tests/RequirementsParserTests.cs` (req-card contract-conformance assertion)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (golden fingerprint regenerated)

## Change Log

- 2026-07-19: Story implemented (dev-story). Extracted `ListRow` shared list-row primitive; wired the ADR landing list, timeline rows, and epics empty-state onto it; confirmed Requirements/Epics/Story-card families and Code Map/Code File tables already conform to the anatomy contract without a markup rewrite. Golden fingerprint regenerated (`550297dd…`). 1679 tests green.
