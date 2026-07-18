---
baseline_commit: 8d9aac44fe721e35315cef0881cb04ba64b2ded9
---

# Story 9.10: Scannable Follow-Up List Pages

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer scanning what's left,
I want the Action Items and Deferred Work list pages to read as a fast, uniform overview instead of a dense wall of detail,
so that I can see everything outstanding at a glance and drill into the one item I care about.

## Acceptance Criteria

1.
**Given** the Action Items and Deferred Work pages carry provenance, resolution links, cross-links, and (for action items) a Resolve-with-AI command per item today (Story 9.6)
**When** the list page renders
**Then** each entry is compressed to a scan-first row — a short title/summary, its status, its source (epic/retro or deferral source), and one primary link — with the heavy per-item detail moved off the index (to the Story 9.11 detail page, or behind a per-row disclosure when 9.11 has not landed)
**And** the two pages share one list grammar so they read as siblings.

2.
**Given** many items exist across several sources
**When** the page renders
**Then** items stay grouped and ordered as Story 9.6 established (by source retro / deferral source, age within) and the grouping is legible at a glance without expanding anything
**And** counts continue to agree with the Story 8.3 `ProjectCounts` ledger — no parallel recount.

3.
**Given** a framework with no retros or no deferred-work note
**When** the portal generates
**Then** the pages degrade to absent rather than empty-but-present (NFR8), exactly as today.

## Context & Scope

Epic 9 closes review follow-through. Story **9.6** (`review`) made the two standalone follow-up pages carry FR30 provenance, resolution links, cross-links, and (action items) a Resolve-with-AI command. Story **9.7** (`review`) made open follow-ups visible in the sunburst. **Neither made the list pages scannable, and neither gave items stable URLs** — so `action-items.html` / `deferred-work.html` are now dense walls of per-item detail. This story is the **list-page legibility** half of the follow-up density fix; Story **9.11** is the paired detail-page + deep-link half.

**This story compresses the list pages into scan-first rows.** It does **not** invent a new authoring schema, does **not** change the sunburst (9.7), and does **not** own the per-item detail page or slug URLs (9.11).

### The two surfaces (both standalone `StringBuilder` templaters — NOT `IRenderAdapter`/`PageView`)

| Surface | Templater | Output path | Current per-item chrome |
|---------|-----------|-------------|--------------------------|
| **Action Items** | `ActionItemsTemplater.RenderPage` | `SiteNav.ActionItemsOutputPath` = `action-items.html` (fixed) | `.action-item-card` `<li>`: linkified action text, Epic pill, status badge, optional cross-retro link, optional deferred-work link, **Resolve-with-AI `data-copy` command**. Grouped by epic asc, "Unattributed" trails, file-order within group. Owner parsed but **never rendered**. |
| **Deferred Work** | `DeferredWorkTemplater.RenderPage` | `WorkInventory.Deferred.OutputPath` (overwrites the generic doc page) | `.deferred-item-card` `<li>` (+`resolved`): Open/Resolved badge, optional Resolving link, full `BodyHtml`. Grouped by `## Deferred from:` provenance, open-first then resolved within group. |

### Owner-selected design direction (locked at create-story)

Owner decisions carried from the 9.10/9.11 planning exchange:

- **Scan-first rows now; full detail moves to the 9.11 detail page.** Until 9.11 lands, heavy detail (full body, Resolve-with-AI command, cross-links, resolving criteria) goes **behind a per-row native `<details>` disclosure** — never deleted. When 9.11 lands, the primary link becomes the detail-page URL and the disclosure body can shrink to a teaser (9.11 owns that hand-off; keep the seam clean).
- **One shared list grammar across both pages** so Action Items and Deferred Work read as siblings. Introduce a single shared row renderer / CSS class family (e.g. `.followup-row`) that both templaters call, differing only in the source-label text and status vocabulary. This is the list-page mirror of 9.11's shared detail template.
- **Grouping/order unchanged (9.6 owns it).** Keep group-by-epic-retro (action items) / group-by-`## Deferred from:` (deferred), age/file-order within group. Only the *density of each row* changes.

Concrete silhouette rules:

- **Row anatomy (both pages):** one line per item — short title/summary (first sentence or truncated action text / deferred item lead, plain-linkified as today), a status badge (`StatusStyles`), a source chip (Epic N / retro label, or deferral source label), and **one** primary link (detail page when 9.11 exists, else the row-disclosure toggle). No more than that above the fold.
- **Group headers stay** (`<h2>` "From the Epic N retrospective" / `## Deferred from: {label}`) and become the at-a-glance grouping; do not require expanding a group to read its membership.
- **Heavy detail relocation:** action items — the Resolve-with-AI `data-copy` command and cross-retro links move into the per-row `<details>` (or 9.11 page). Deferred — the full `BodyHtml` and resolving criteria move into the per-row `<details>` (or 9.11 page). The short summary in the row must be derived from the same source text (no new authoring).
- **Copy-payload discipline (the #1 trap):** `SiteGenerator.WriteActionItems` intentionally does **not** run `ApplyReferenceLinks` because the Resolve-with-AI `data-copy` attribute embeds raw action text; whole-page linkify would wrap "Epic N"/"Story N.M" inside the attribute and corrupt the copyable command. **Preserve that:** any short-summary linkify must stay inside the templater on visible text only (reuse `FollowUpRefs.LinkifyVisibleText`), never touch the `data-copy` payload, and the page must remain excluded from the site-level `ApplyReferenceLinks` pass. Deferred-work page keeps its post-write `ApplyReferenceLinks` (no copy payload there).
- **Disclosure = pure CSS, zero JS** (webview CSP; NFR5). Native `<details class="followup-row-detail">` collapsed by default, reusing the `.dev-agent-details` / `.collapsible-section` caret vocabulary already in the codebase.
- **No new `--status-*` token; never color-only.** Reuse `StatusStyles` badges (open/resolved/done) with word + icon.

## Tasks / Subtasks

- [x] **Task 1 — Shared scan-first row grammar (AC: #1)**
  - [x] Add a single shared row/summary renderer both templaters call (e.g. a small `FollowUpRow` helper or a shared method) producing: short-summary + status badge + source chip + one primary link + collapsed `<details>` for heavy detail. Keep it a pure string builder (match existing `StringBuilder` style; no new render-adapter path).
  - [x] Derive the short summary from existing text only (first sentence / truncated action text or deferred lead) — **no new authoring field**. Reuse `PathUtil.StripHtmlTags` + truncation helpers already in the codebase.
  - [x] Both pages emit the same `.followup-row` markup so they read as siblings; only source-label text and status vocabulary differ.

- [x] **Task 2 — Action Items page compression (AC: #1, #2, #3)**
  - [x] Rework `ActionItemsTemplater.RenderCard` (`ActionItemsTemplater.cs` ~132–183) to the scan-first row. Move Resolve-with-AI `data-copy` command + cross-retro link into the collapsed `<details>` (or, when 9.11 URL is available, link to it as the primary link — coordinate with 9.11, keep additive).
  - [x] **Keep grouping/order exactly** (`GroupByEpic`, epic-asc, Unattributed trailing, file-order within). Keep `FindNearDuplicates` cross-link but relocate it into the disclosure.
  - [x] Preserve the copy-payload boundary: visible summary linkify inside templater only; page stays out of site-level `ApplyReferenceLinks`; `data-copy` still carries **raw** action text.

- [x] **Task 3 — Deferred Work page compression (AC: #1, #2, #3)**
  - [x] Rework `DeferredWorkTemplater.RenderItem` (`DeferredWorkTemplater.cs` ~85–117) to the same scan-first row: short summary + Open/Resolved badge + `## Deferred from:` source chip + one primary link; full `BodyHtml` + resolving criteria into the collapsed `<details>` (or 9.11 link).
  - [x] Keep group-by-provenance + open-first ordering (`DeferredWorkParser` model unchanged). Keep the unstructured-fallback path intact.
  - [x] Deferred page keeps its post-write `ApplyReferenceLinks` (`SiteGenerator.WriteDeferredWork` ~2737) — safe, no copy payload.

- [x] **Task 4 — Shared CSS for the row grammar (AC: #1)**
  - [x] Add `.followup-row` + `.followup-row-detail` rules in `src/SpecScribe/assets/specscribe.css` near the existing `.action-item-card` / `.deferred-item-card` block. Reuse `.dev-agent-details` caret for the disclosure. Retire/alias the old card-specific rules only if fully replaced; keep resolved-state visual.
  - [x] Extend `StylesheetTests` for the new shared classes; assert non-color-only status signalling survives.

- [x] **Task 5 — Guardrails: ledger counts + no schema/scope creep (AC: #2, #3)**
  - [x] Counts shown (header/summary counts if any) must read `ProjectCounts` (`OpenActionItems`, `DeferredOpenItems`) — no re-tally at a second parse site. Confirm `AppendWorkSummaryCards` home tiles still agree.
  - [x] Do **not** change `sprint-status.yaml` / `deferred-work.md` schemas, `SprintActionItem`, `DeferredWorkParser` model, or the sunburst (9.7). Do **not** build the detail page or slugs (9.11).
  - [x] NFR8 degrade unchanged: `WriteActionItems` early-returns with no open items; `WriteDeferredWork` early-returns with no deferred doc. Empty groups never render.

- [x] **Task 6 — Tests + golden (AC: #1, #2, #3)**
  - [x] `FollowUpSurfacesTests`: assert scan-first row shape on both pages (short summary present, heavy detail inside `<details>` / not above the fold), shared `.followup-row` class on both, grouping/order preserved, Resolve-with-AI `data-copy` still carries raw (un-linkified) action text.
  - [x] Keep the existing degrade tests green (`Degrade_NoActionItems_*`, `Degrade_NoDeferredWork_*`).
  - [x] Golden fingerprint moves (both page bodies change) → regen `SiteGeneratorAdapterTests.cs` expected hash (currently line ~398) per `golden-diff-normalization-gotchas`. These pages ride `WriteOutput` → SPA/webview capture, so no new `HostRenderException`; confirm SPA/webview capture still slices them (they emit `main#main-content`).
  - [x] Run `dotnet test` from repo root.

### Review Findings

- [x] [Review][Patch] Omit list “More” disclosure when `detailHref` is set (owner: scan + View detail only) [`FollowUpRow.cs:64`] [`ActionItemsTemplater.cs:154`] [`DeferredWorkTemplater.cs:125`]
- [x] [Review][Patch] Abbreviation false sentence ends truncate scan leads [`FollowUpRow.cs:153`]
- [x] [Review][Patch] Metadata-only / same-line `source_spec`/`evidence` handling pollutes or drops scan leads [`FollowUpRow.cs:14`]
- [x] [Review][Patch] Restore heavy list content when `detailHref` is null (additive 9.10 seam) [`ActionItemsTemplater.cs:154`] [`DeferredWorkTemplater.cs:125`]
- [x] [Review][Patch] Span teasers still styled as links (hover / dotted underline) [`specscribe.css:5608`] [`specscribe.css:5623`]
- [x] [Review][Patch] Stale FollowUpRow type comment still says heavy detail stays in `<details>` until 9.11 [`FollowUpRow.cs:6`]
- [x] [Review][Decision] Post-9.11 list “More” disclosure role — **resolved → omit More when `detailHref` is set** (option 2); subsumes ✓-only More patch.
- [x] [Review][Defer] Nested / unclosed `<li>` in unstructured deferred extraction [`FollowUpGeometry.cs:367`] — deferred, pre-existing
- [x] [Review][Defer] Unstructured deferred notes with list items no longer use plain-body fallback [`DeferredWorkTemplater.cs:38`] — deferred, pre-existing (9.11 overlay)
- [x] [Review][Defer] `FollowUpRow.Render` branches lack direct unit coverage [`FollowUpRowTests.cs`] — deferred, pre-existing (partially addressed: Render href/disclosure tests added)

## Dev Notes

### Current code reality (read before editing)

```11:19:src/SpecScribe/ActionItemsTemplater.cs
// RenderPage(openItems, epicRetroMap, commands, nav, deferredWorkHref, counts, epicsModel, hrefMap)
// GroupByEpic → epic asc, null-epic "Unattributed" trailing, file-order within group.
```

```145:182:src/SpecScribe/ActionItemsTemplater.cs
// .action-item-card <li>: linkedText + Epic pill + status badge + cross-retro + deferred link + Resolve-with-AI
// linkedText = FollowUpRefs.LinkifyVisibleText(item.Action, ...) — visible text ONLY.
```

```2694:2702:src/SpecScribe/SiteGenerator.cs
// WriteActionItems: NOT reference-linkified — data-copy payload embeds raw action text.
// Gated on OpenActionItems.Count > 0. WriteOutput(SiteNav.ActionItemsOutputPath, html).
```

```85:117:src/SpecScribe/DeferredWorkTemplater.cs
// .deferred-item-card <li>(+resolved): Open/Resolved badge + optional Resolving link + full BodyHtml.
// Open-first then resolved within group; groups by ## Deferred from: provenance.
```

```2708:2740:src/SpecScribe/SiteGenerator.cs
// WriteDeferredWork: runs ApplyReferenceLinks (safe), overwrites WorkInventory.Deferred.OutputPath.
```

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Action items list + open filter | `SprintStatus.OpenActionItems` | `SprintStatus.cs:34` |
| Action item fields | `SprintActionItem(Action, Status, EpicNumber, Owner)` | `SprintStatus.cs:16` |
| Visible-text linkify (safe) | `FollowUpRefs.LinkifyVisibleText` | `FollowUpRefs.cs:71` |
| Status badges | `StatusStyles.ForSprint` / `SprintLabel` | `StatusStyles.cs` |
| Resolve-with-AI command | `BmadCommands.RenderLabeledCommand` | `BmadCommands.cs:195` |
| Deferred model + groups | `DeferredWorkParser` / `DeferredWorkModel` | `DeferredWorkParser.cs:8` |
| Ledger counts (authority) | `ProjectCounts.OpenActionItems` / `DeferredOpenItems` | `ProjectCounts.cs:135` |
| Collapsible caret vocab | `.dev-agent-details` / `.collapsible-section` | `specscribe.css` |
| HTML strip / truncate | `PathUtil.StripHtmlTags`, existing truncation | `PathUtil.cs` |
| Write + SPA capture | `SiteGenerator.WriteOutput` | `SiteGenerator.cs:2285` |

### Guardrails & invariants

- **Copy-payload trap (#1 review checkpoint).** Never run the action-items page through site-level `ApplyReferenceLinks`; keep summary linkify inside the templater on visible text; `data-copy` stays raw. Corrupting the command is the classic failure here (`SiteGenerator.cs:1402` lineage).
- **Split, don't absorb.** 9.6 owns provenance/grouping/resolution content; 9.7 owns the sunburst; 9.11 owns the detail page + slug URLs. This story only compresses row density and adds the shared disclosure. Keep the 9.11 hand-off additive (primary-link becomes the detail URL when available).
- **Single count ledger (Story 8.3).** No parallel recount.
- **Never color-only (UX-DR17); no new `--status-*` token.**
- **NFR8 degrade unchanged** — no empty-but-present pages/groups.
- **No JS for disclosure** (webview CSP; NFR5) — native `<details>`.
- **Capture-based delivery:** both pages hit disk via `WriteOutput` and are sliced into SPA/webview via `main#main-content`; no `PageView`/`RenderParity`/`HostRenderException` work. Golden moves on purpose — regen.

### Previous story intelligence (9.6 / 9.7)

- Owner is on the `SprintActionItem` model but **never rendered** — optional to surface it as a source-chip detail, but not required by ACs; if shown, keep it inside the disclosure and never in the `data-copy`.
- Deferred item resolving link is a first-class field (`ResolvingRef`/`ResolvingHref`); action-item resolving links are only inline mentions — do not try to promote them to a schema field (9.11 may formalize; this story just relocates what exists).
- 9.7 changed the sunburst only; the list pages were untouched by it — this is the first re-render of these page bodies since 9.6, so the golden diff is expected and isolated to the two pages.

### Project Structure Notes

- Primary edits: `ActionItemsTemplater.cs`, `DeferredWorkTemplater.cs`, a small shared row helper (new tiny file next to them, or a shared static — keep it minimal), `assets/specscribe.css`, tests under `tests/SpecScribe.Tests/`.
- Likely **no** change to `SiteGenerator.cs` write seams beyond leaving them as-is (the golden regen is the only forced edit in tests). Confirm `WriteActionItems`/`WriteDeferredWork` still compile against any changed templater signatures (keep changes additive/optional).
- Do **not** touch schemas, `SprintStatus`/`SprintStatusParser`, `DeferredWorkParser` model, sunburst, or `package.json`.

### Testing standards

- xUnit; `Assert.Contains` / `DoesNotContain` on emitted HTML (`FollowUpSurfacesTests` pattern).
- Pin: shared `.followup-row` on both pages, short summary above the fold, heavy detail inside `<details>`, grouping/order preserved, raw `data-copy` intact, degrade-to-absent, ledger-agreed counts.
- Full suite green including golden + parity suites.

### Verify before marking review

Generate to `SpecScribeOutput/`. Open `action-items.html`: each item is a one-line scannable row (summary + status + source + one link); Resolve-with-AI is inside a disclosure and, when copied, the command still contains raw un-linkified action text (no stray `<a>` in the copied string). Open `deferred-work.html`: same row grammar, groups still read at a glance, full body behind the disclosure. Both pages share `.followup-row`. A repo with no retros / no deferred note produces neither page. `dotnet test` green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.10] — user story + ACs
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] — FR30; NFR8; split-don't-absorb principle
- [Source: _bmad-output/implementation-artifacts/9-6-follow-up-item-provenance-and-resolution-paths.md] — provenance/grouping owner + copy-payload trap
- [Source: _bmad-output/implementation-artifacts/9-7-open-follow-ups-in-the-remaining-work-geometry.md] — sunburst geometry (out of scope here)
- [Source: _bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md] — `ProjectCounts` ledger
- [Source: src/SpecScribe/ActionItemsTemplater.cs] — action-items page templater
- [Source: src/SpecScribe/DeferredWorkTemplater.cs] / [src/SpecScribe/DeferredWorkParser.cs] — deferred page templater + model
- [Source: src/SpecScribe/SiteGenerator.cs] — `WriteActionItems` / `WriteDeferredWork` / `WriteOutput`
- [Source: src/SpecScribe/FollowUpRefs.cs] — visible-text linkify
- [Source: src/SpecScribe/BmadCommands.cs] — Resolve-with-AI command / data-copy
- [Source: src/SpecScribe/assets/specscribe.css] — `.action-item-card` / `.deferred-item-card` / `.dev-agent-details`
- [Source: tests/SpecScribe.Tests/FollowUpSurfacesTests.cs] — page-shape + degrade tests
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs] — golden fingerprint (~line 398)

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

### Implementation Plan

- Introduced `FollowUpRow.cs` as the shared scan-first row renderer: `SummarizePlainText`/`SummarizeFromHtml` derive short leads from existing text; `Render` emits `.followup-row` with scan line (summary + badge + source chip) and a native `<details>` disclosure for heavy detail (or optional `detailHref` seam for Story 9.11).
- Reworked `ActionItemsTemplater.RenderCard` to call `FollowUpRow.Render`; cross-retro links, deferred-work link, full linkified text, and Resolve-with-AI `data-copy` moved into disclosure; copy-payload boundary preserved.
- Reworked `DeferredWorkTemplater.RenderItem` to call `FollowUpRow.Render` with provenance source chip; full `BodyHtml`, resolving link, and resolved mark moved into disclosure.
- Replaced `.action-item-card`/`.deferred-item-card` item CSS with shared `.followup-row*` rules reusing dev-agent caret grammar; retained group headings and resolved-state treatment.
- Updated `FollowUpSurfacesTests` and `StylesheetTests`; regenerated golden fingerprint in `SiteGeneratorAdapterTests.cs`.

### Completion Notes List

- ✅ Story 9.10 complete: both follow-up list pages now use shared `.followup-row` scan-first grammar with heavy detail behind per-row `<details>` disclosures.
- ✅ Grouping/order unchanged (epic-retro asc + deferral provenance); `ProjectCounts` ledger untouched; NFR8 degrade paths green.
- ✅ Copy-payload trap preserved: `data-copy` attributes carry raw un-linkified action text.
- ✅ 1279 tests pass (full suite); golden fingerprint regenerated for shared CSS delta.

### File List

- src/SpecScribe/FollowUpRow.cs (new)
- src/SpecScribe/ActionItemsTemplater.cs
- src/SpecScribe/DeferredWorkTemplater.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/FollowUpSurfacesTests.cs
- tests/SpecScribe.Tests/StylesheetTests.cs
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs

### Change Log

- 2026-07-16: Story 9.10 — compress action-items and deferred-work list pages to shared scan-first `.followup-row` grammar; heavy detail in per-row `<details>`; golden fingerprint regen.
- 2026-07-17: Code review — omit list More when detailHref set; abbreviation/metadata summarize fixes; null-href disclosure seam restored; span-as-link CSS; golden regen; status → done.
