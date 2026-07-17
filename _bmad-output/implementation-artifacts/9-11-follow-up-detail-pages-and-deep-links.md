---
baseline_commit: 8d9aac44fe721e35315cef0881cb04ba64b2ded9
---

# Story 9.11: Follow-Up Detail Pages and Deep Links

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer following a link from the sunburst or a list row,
I want each action item and deferred-work item to have its own stable page,
so that I can deep-link to a single follow-up and read its full provenance and resolution context in one place.

## Acceptance Criteria

1.
**Given** an action item or a deferred-work item
**When** the portal generates
**Then** that item has its own detail page (or a stable per-item anchor) carrying its full provenance, resolution criteria, resolving-story/spec links, and cross-links — the detail that Story 9.10 moved off the list index
**And** action-item and deferred-item detail pages share one template, differing only in grouping / where-it-came-from framing.

2.
**Given** an item's detail page URL
**When** the same project regenerates (with the item unchanged)
**Then** the URL is a stable, human-readable slug derived from the item's existing text/source — not a positional index — so bookmarks and deep links survive reordering and regeneration
**And** no new authoring schema is introduced (slugs are derived by best-effort heuristic over text already authored, per the load-bearing Epic 9 principle).

3.
**Given** the Story 9.7 sunburst follow-up geometry and the Story 9.10 list rows
**When** an item is clicked
**Then** they link to that item's detail URL (completing 9.7's "navigable into their detail/provenance surfaces" AC), and the counts and set of items shown remain the Story 8.3 ledger's
**And** these surfaces degrade to absent when the underlying artifacts do not exist (NFR8).

## Context & Scope

This is the second half of the follow-up density fix. Story **9.6** put provenance/resolution content on the two list pages; Story **9.7** put open follow-ups in the sunburst (wedges currently deep-link only to the whole `action-items.html` / `deferred-work.html` page); Story **9.10** compresses those list pages into scan-first rows and relocates heavy detail. **This story gives each item its own stable page** so the sunburst wedge and the list row can link to *the single item*, not the whole page — closing 9.7's AC #1 ("navigable into their detail/provenance surfaces") for real.

**Owner decisions (locked at create-story):**

1. **Slugs, not positional indices.** URLs must be human-readable/scannable *and* stable between regenerations, surviving reordering. Derive the slug from the item's existing text — no new authoring schema.
2. **One shared detail template** for both action items and deferred items. They "look very, very similar"; the only difference is grouping / where the item came from (retro-epic vs deferral-source framing).

### Owner-selected design direction (locked)

- **Closest precedent = `WriteRequirements`** (`requirements/{slug}.html`): a set of per-item pages generated from a collection via `WriteOutput` (so SPA/webview pick them up through `_spaCapture` automatically — **do not** use the epic/story `File.WriteAllText` path, which requires teaching the view-model re-render loop). Mirror this exactly.
- **Output layout:** `follow-ups/{slug}.html` for both kinds (one folder, one template), or two subfolders if collisions across kinds are a concern — prefer one folder with kind-scoped slug prefixes (see slug rules) so cross-links and the shared template stay uniform.
- **Shared template** (`FollowUpDetailTemplater`, new): head/nav/breadcrumb + `<main class="followup-detail">` carrying: title/summary, status badge, **provenance block** (retro-epic framing for action items; `## Deferred from:` source framing for deferred), resolution criteria, resolving-story/spec links, cross-links, and — for action items — the Resolve-with-AI command (same `data-copy` discipline). The *only* per-kind branch is the provenance framing + status vocabulary.
- **Slug rules (stable, human-readable, no schema):**
  - Derive from existing visible text: action items from `SprintActionItem.Action`; deferred items from the item lead / provenance label. Kebab-case, lowercase, `[a-z0-9-]` only, collapse repeats, trim, cap to ~6–8 words.
  - **Kind prefix** to keep the two collections in one namespace: e.g. `action-{slug}` / `deferred-{slug}`.
  - **Deterministic disambiguation that survives reordering:** when two items produce the same base slug, append a short stable suffix derived from **content** (e.g. first 6 hex of SHA-256 of the full source text `+` epic/source key), **never** a positional `-2`/`-3` counter (positional counters shift under reordering and break AC #2). Prefer always-appending the short content hash only on collision; single-occurrence slugs stay clean/readable.
  - Slug is a **pure function of the item's authored text** → same text regenerates the same URL.
- **Link wiring:**
  - Sunburst: pass a **per-item href** into `Charts.AppendActionItemSlot` (currently every action wedge shares `geometry.ActionItemsHref`). Deferred aggregate wedge may either stay page-level or, if the parsed model is available, link per-item — page-level deferred is acceptable if per-item deferred attribution is awkward; do not break the ledger.
  - List rows (9.10): the row's single primary link becomes the detail URL.
  - `FollowUpGeometry` needs a way to resolve an item → its detail href (add a slug/href helper or a resolver map built once at generation time; keep it ledger-consistent).
- **NFR8:** generate detail pages only for items that exist; when no follow-ups exist, no pages, no wedge links, no list links.

## Tasks / Subtasks

- [ ] **Task 1 — Stable follow-up slug helper (AC: #2)**
  - [ ] Add a pure slug function (e.g. `FollowUpSlug.For(SprintActionItem)` / `.For(deferred item)`) producing `action-{kebab}` / `deferred-{kebab}` from existing text, with content-hash disambiguation on collision only. Mirror `RequirementInfo.Slug` / `CodeFileTemplater.Slugify` conventions already in the repo.
  - [ ] Unit-test determinism: same text → same slug; reordering the collection does not change any item's slug; two near-identical texts get distinct stable slugs; slug is filesystem/URL-safe.

- [ ] **Task 2 — Shared follow-up detail template (AC: #1)**
  - [ ] New `FollowUpDetailTemplater.RenderPage(...)` producing the shared `<main class="followup-detail">` page for both kinds; the only per-kind branch is provenance framing (retro-epic vs deferral-source) + status vocabulary. Reuse `RequirementsTemplater.RenderRequirement` structure (head/nav/breadcrumb/main) as the shape precedent.
  - [ ] Carry the heavy detail 9.10 relocated: full body/summary, status badge, provenance, resolution criteria, resolving-story/spec links, cross-links; action items also carry the Resolve-with-AI command via `BmadCommands.RenderLabeledCommand` with **raw** action text in `data-copy`.

- [ ] **Task 3 — Generate the per-item page set (AC: #1, #3)**
  - [ ] Add `WriteFollowUpDetails` in `SiteGenerator.cs` mirroring `WriteRequirements` (`~2799–2820`): `Directory.CreateDirectory` the `follow-ups/` folder, loop items, `WriteOutput($"follow-ups/{slug}.html", ...)`. Call it from `GenerateAll` beside `WriteActionItems`/`WriteDeferredWork` (`~364–365`).
  - [ ] **Copy-payload discipline:** if a detail page embeds the Resolve command, do not run *that page* through site-level `ApplyReferenceLinks` in a way that corrupts the `data-copy`; linkify visible text inside the templater (reuse `FollowUpRefs.LinkifyVisibleText`), matching the action-items page precedent. Deferred-only detail pages can be linkified safely.
  - [ ] Gate on existence (NFR8): no open/known items → no folder, no pages.

- [ ] **Task 4 — Deep-link the sunburst wedges (AC: #3)**
  - [ ] Thread a per-item detail href into `Charts.AppendActionItemSlot` (replace the shared `geometry.ActionItemsHref` for attributed + unattributed action wedges). Add a resolver on `FollowUpGeometry` (item → detail href via the slug helper) so `Charts` stays a pure renderer and counts stay ledger-agreed.
  - [ ] Deferred aggregate wedge: link per-item when the parsed deferred model is available; otherwise keep the page-level `DeferredHref` (acceptable). Never re-count.
  - [ ] `EpicSunburst` follow-up peers get the same per-item hrefs.

- [ ] **Task 5 — Deep-link the list rows (AC: #1, #3) — coordinate with 9.10**
  - [ ] In `ActionItemsTemplater` / `DeferredWorkTemplater`, make each 9.10 row's single primary link the item's detail URL; the row disclosure can shrink to a teaser since full detail now lives on the page. Keep additive so it works whether 9.10 landed first or this story does.

- [ ] **Task 6 — CSS for detail page (AC: #1)**
  - [ ] Add `.followup-detail` rules in `assets/specscribe.css` reusing requirement-detail / story-detail vocabulary. No new `--status-*` token; reuse `StatusStyles`. Extend `StylesheetTests`.

- [ ] **Task 7 — Tests + golden (AC: #1, #2, #3)**
  - [ ] Slug determinism/stability tests (Task 1) — the AC #2 teeth.
  - [ ] `FollowUpSurfacesTests`: a detail page exists per item; shared template shape across both kinds; provenance framing differs by kind; resolving links/cross-links present on detail (not the compressed row); action-item detail `data-copy` carries raw action text.
  - [ ] `ChartsTests`: sunburst action wedges now carry **per-item** detail hrefs (not the shared `action-items.html`); update `Sunburst_FollowUps_*` and `EpicSunburst_FollowUps_*` (`ChartsTests.cs` ~331–439) and `FollowUpSurfacesTests.HomeAndEpicSunburst_*` (~270–296, which currently asserts `href="action-items.html"`).
  - [ ] NFR8 degrade tests: no items → no `follow-ups/` pages, no per-item wedge/list links.
  - [ ] Golden fingerprint moves (new pages + changed wedge/list hrefs) → regen `SiteGeneratorAdapterTests.cs` expected hash (~line 398) per `golden-diff-normalization-gotchas`. Pages ride `WriteOutput` → SPA/webview capture; confirm they emit `main#main-content` and are sliced; no new `HostRenderException`.
  - [ ] Run `dotnet test` from repo root.

## Dev Notes

### Current code reality (read before editing)

```2799:2820:src/SpecScribe/SiteGenerator.cs
// WriteRequirements — canonical "set of per-item pages" seam:
//   foreach req: WriteOutput($"requirements/{req.Slug}.html", ApplyReferenceLinks(RenderRequirement(...), skipRequirementId: req.Id))
//   → SPA/webview pick up via _spaCapture. MIRROR THIS (not the epic/story File.WriteAllText path).
```

```41:42:src/SpecScribe/RequirementsModel.cs
// RequirementInfo.Slug => Id.ToLowerInvariant();  →  requirements/{slug}.html  (slug precedent)
```

```366:383:src/SpecScribe/Charts.cs
// AppendActionItemSlot(..., href, ...) / AppendFollowUpSlot(..., href, ...) already wrap <a href aria-label><title>
// Every action wedge currently passes geometry.ActionItemsHref (whole page). Swap to per-item href here.
```

```7:66:src/SpecScribe/FollowUpGeometry.cs
// FollowUpGeometry(ActionItems, DeferredOpenCount, DeferredHref, ActionItemsHref)
// hrefs are whole-page today; add a per-item detail-href resolver (slug-based).
```

```16:16:src/SpecScribe/SprintStatus.cs
// SprintActionItem(Action, Status, EpicNumber, Owner) — NO id/slug field today. Slug derives from Action text.
```

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Per-item page set precedent | `WriteRequirements` | `SiteGenerator.cs:2799` |
| Slug precedent (readable) | `RequirementInfo.Slug`, `CodeFileTemplater.Slugify` | `RequirementsModel.cs:41`, `CodeFileTemplater.cs:569` |
| Detail-page template shape | `RequirementsTemplater.RenderRequirement` | `RequirementsTemplater.cs:120` |
| Write + SPA/webview capture | `SiteGenerator.WriteOutput` | `SiteGenerator.cs:2285` |
| Wedge `<a href>` attach point | `Charts.AppendActionItemSlot` / `AppendFollowUpSlot` | `Charts.cs:366` |
| Per-story wedge href precedent | `Charts.AppendStorySlot` (`story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath`) | `Charts.cs:330` |
| Geometry input | `FollowUpGeometry` | `FollowUpGeometry.cs` |
| Action item fields | `SprintActionItem` | `SprintStatus.cs:16` |
| Deferred model (provenance/resolving) | `DeferredWorkParser` / `DeferredWorkModel` | `DeferredWorkParser.cs:8` |
| Visible-text linkify (safe) | `FollowUpRefs.LinkifyVisibleText` | `FollowUpRefs.cs:71` |
| Resolve-with-AI command | `BmadCommands.RenderLabeledCommand` | `BmadCommands.cs:195` |
| Ledger counts (authority) | `ProjectCounts.OpenActionItems` / `DeferredOpenItems` | `ProjectCounts.cs:135` |
| Status badges | `StatusStyles` | `StatusStyles.cs` |
| Golden fingerprint | expected hash + normalization | `SiteGeneratorAdapterTests.cs:398` |

### Guardrails & invariants

- **Slug stability is the AC #2 teeth.** Pure function of authored text; content-hash disambiguation on collision; never positional. Test that reordering the collection changes no slug.
- **No new authoring schema** (load-bearing Epic 9 principle, same as 9.3/9.4/9.6). Slugs and detail content derive from text already authored in `sprint-status.yaml` action items and the deferred-work note.
- **One shared template**, per-kind branch limited to provenance framing + status vocabulary (owner decision).
- **Copy-payload trap.** Action-item detail pages that embed the Resolve command must keep `data-copy` raw and linkify visible text inside the templater — do not corrupt the attribute via a whole-page linkify pass.
- **Single count ledger (Story 8.3).** Detail-page set and wedge/list links must reflect the ledger's items; no parallel recount.
- **Mirror `WriteRequirements`, not `File.WriteAllText`** — `WriteOutput` gets SPA/webview capture for free; no `PageView`/`RenderParity`/`HostRenderException` work needed (confirm `main#main-content` landmark so slicing works).
- **NFR8 degrade:** no items → no `follow-ups/` folder, no per-item wedge/list links; wedges/rows fall back cleanly.
- **No JS for navigation** (webview CSP; NFR5) — plain `<a href>`.
- **Coordinate with 9.10** (list-row primary link) and **9.7** (wedge href) — both were whole-page before; keep changes additive and re-point them to the detail URL.
- **Golden moves on purpose** — regen.

### Previous story intelligence

- 9.7 `FollowUpGeometry` currently sets `ActionItemsHref`/`DeferredHref` to whole pages and `Charts` already wraps every wedge in a real `<a>` — so deep-linking is a matter of *which href* is passed, plus a slug resolver. Minimal `Charts` shape change.
- 9.6/9.10 own the list-page chrome; this story adds the destination those rows/wedges point at. Deferred item resolving links are first-class (`ResolvingRef`/`ResolvingHref`); action-item resolving links are inline mentions — the detail page can surface both without a new schema field.
- `SprintActionItem` has no id — the slug helper is the identity. Owner is present on the model (never rendered); optional to show on detail, not required.

### Project Structure Notes

- New files: `FollowUpDetailTemplater.cs`, a slug helper (`FollowUpSlug.cs` or a static on an existing follow-up type), CSS block in `assets/specscribe.css`, tests.
- Edits: `SiteGenerator.cs` (`WriteFollowUpDetails` + `GenerateAll` call), `Charts.cs` (per-item wedge href), `FollowUpGeometry.cs` (detail-href resolver), `ActionItemsTemplater.cs` / `DeferredWorkTemplater.cs` (row primary link — coordinate with 9.10).
- Do **not** change `sprint-status.yaml` / `deferred-work.md` schemas or `SprintStatus`/`DeferredWorkParser` models.

### Testing standards

- xUnit; `Assert.Contains` / `DoesNotContain` on HTML; dedicated slug-determinism tests.
- Pin: per-item page exists, shared template + per-kind framing, slug stability under reorder, per-item wedge/list hrefs, raw `data-copy`, ledger agreement, degrade-to-absent.
- Full suite green including golden + parity suites.

### Verify before marking review

Generate to `SpecScribeOutput/`. Confirm `follow-ups/action-{...}.html` and `follow-ups/deferred-{...}.html` exist with readable slugs; open one — full provenance/resolution/cross-links present, shared layout, kind-appropriate framing. From home sunburst, click an action wedge → lands on that item's detail page (not the whole list). From `action-items.html`, a row's primary link → same detail page. Regenerate twice → identical URLs (diff the `follow-ups/` file names). A repo with no follow-ups → no `follow-ups/` folder, wedges/rows degrade. `dotnet test` green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.11] — user story + ACs
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] — FR30; NFR8; no-new-authoring-schema principle
- [Source: _bmad-output/implementation-artifacts/9-7-open-follow-ups-in-the-remaining-work-geometry.md] — sunburst wedges (whole-page href today); "navigable into detail" AC this story closes
- [Source: _bmad-output/implementation-artifacts/9-10-scannable-follow-up-list-pages.md] — list-row primary link hand-off
- [Source: _bmad-output/implementation-artifacts/9-6-follow-up-item-provenance-and-resolution-paths.md] — provenance/resolution content + copy-payload trap
- [Source: _bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md] — `ProjectCounts` ledger
- [Source: src/SpecScribe/SiteGenerator.cs] — `WriteRequirements` precedent + `WriteOutput`
- [Source: src/SpecScribe/RequirementsModel.cs] / [src/SpecScribe/RequirementsTemplater.cs] — slug + detail-template precedent
- [Source: src/SpecScribe/Charts.cs] — wedge `<a href>` attach points
- [Source: src/SpecScribe/FollowUpGeometry.cs] — geometry input to extend with per-item hrefs
- [Source: src/SpecScribe/SprintStatus.cs] — `SprintActionItem` (no id → slug is identity)
- [Source: src/SpecScribe/DeferredWorkParser.cs] — deferred model (provenance/resolving)
- [Source: src/SpecScribe/FollowUpRefs.cs] / [src/SpecScribe/BmadCommands.cs] — visible-text linkify + Resolve command
- [Source: tests/SpecScribe.Tests/ChartsTests.cs] / [tests/SpecScribe.Tests/FollowUpSurfacesTests.cs] — wedge + surface tests to update
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs] — golden fingerprint (~line 398)

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
