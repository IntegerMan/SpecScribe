---
baseline_commit: 27d37b69456526e2d89ca8111a3bf9cbc645f4dc
---

# Story 10.10: Context-Aware Navigation Bar

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader on any page,
I want the top navigation bar to carry navigation relevant to where I am,
so that the white bar earns its space on every page instead of only working on the home dashboard.

## Acceptance Criteria

1.
**Given** every page type (home, epic, story, requirement, code file, follow-up, ADR, commit, insight)
**When** the nav is defined
**Then** a page-type → nav-content mapping specifies what the bar surfaces on each — home keeps the global journey groups; an epic page surfaces its stories; a code page surfaces sibling files / sections; a requirement page surfaces its family; a follow-up page surfaces its group — all built from data already in the view models via the Story 10.1 `RenderNavMarkup` seam (no new authoring schema).

2.
**Given** that mapping
**When** an interior page renders
**Then** the bar shows its page-type-appropriate contents with the active item marked, a page with no meaningful local context (NFR8) falls back cleanly to the global nav rather than an empty bar
**And** HTML, webview, and SPA stay coherent through the shared seam.

## Context & Why This Story Exists

Epic 10 (Portal Legibility) has already delivered a two-tier nav bar: a dark **identity bar** (Story 10.1's journey-organized groups — Home / Delivery / Insights / Follow-ups / Project) and a white **sub-header band** beneath it. Today that white band (`HtmlRenderAdapter.AppendKeyViewsBand`, [HtmlRenderAdapter.cs:148](src/SpecScribe/HtmlRenderAdapter.cs)) only has two states:

- **On Home** → the Story 9.8 work-mode strip (Overview · Requirements · Plan · Develop · Review · Track).
- **On every other page, regardless of page type** → the same generic "Explore Key Views" quick-link pills (Readme/PRD/Architecture/Epics/Requirements/Sprint/ADRs/...).

So an epic page, a story page, a code file page, and an ADR page today all show the *identical* white band — it never reflects where the reader actually is. The owner's framing (SCP 2026-07-19, folded into Epic 10): *"the white bar used effectively throughout, with different context on each page or page type"* — same bar, page-type-specific contents, not a second sidebar rail.

This is genuinely reachable without a redesign because **every page kind this story targets already computes the exact data it would need to show** — this story is a wiring story, not a data-modeling story:

| Page kind | What already exists on hand | Where |
|-----------|------------------------------|-------|
| Epic (`epics/epic-N.html`) | `epic.Stories` (the full story list) + `InteractionState.ChildTargets` (each story's href) | [EpicsTemplater.cs:BuildEpicPage](src/SpecScribe/EpicsTemplater.cs) |
| Story (`epics/epic-N/story-N.M.html` or placeholder) | Same `epic.Stories` sibling list (the `epic` parameter is already passed to `BuildStoryPage`/`BuildStoryPlaceholderPage`) | [EpicsTemplater.cs:BuildStoryPage](src/SpecScribe/EpicsTemplater.cs) |
| Code file (`code/*.html`) | An `EntityPager` is already built over the alphabetical sibling-file family in the same directory ("Prev/next across sibling files in the same directory" — [CodeFileTemplater.cs:703](src/SpecScribe/CodeFileTemplater.cs)) | [CodeFileTemplater.cs:BeginShell](src/SpecScribe/CodeFileTemplater.cs) |
| ADR (`adrs/*.html`) | `EntityPager.FromSequence(_adrs, i, ...)` is already built over the full ADR list in chronological order | [SiteGenerator.cs:804](src/SpecScribe/SiteGenerator.cs) |
| Commit (`commit/*.html`, `commits/{date}.html`) | An `EntityPager` is already built over the commit/day family (calendar order) | `CommitDayTemplater`/commit-page call sites |
| Requirement (`requirements/{slug}.html`) | `coveringEpics` (resolved covering epic list) is already computed in `RenderRequirement`; the requirements-index page already groups requirements by category (`groups` in `RenderIndex`) | [RequirementsTemplater.cs:RenderRequirement](src/SpecScribe/RequirementsTemplater.cs) |
| Follow-up (`action-items.html`, `deferred-work.html`, `follow-ups/group-*.html`, `follow-ups/{slug}.html`) | Story 9.13's generated group pages already enumerate their member rows; Story 9.11's per-item detail pages already carry `crossLinks`/epic attribution | `FollowUpGroupTemplater`, `FollowUpDetailTemplater` |
| Insight (`git-insights.html`, `deep-analytics.html`, `code-map.html`) | These three pages are exactly the Story 10.1 **Insights** nav group's children — `nav.Groups` already has that membership | `SiteNav.Build` / `NavigationView.Groups` |

**The single missing piece is a place to put this per-page list in the shared nav-render seam** so it reaches HTML, webview, and SPA identically — that is the whole of this story's engineering.

Serves FR27–29 / UX-DR25,27–30 (Epic 10's onboarding + legibility mission) and directly extends Story 10.1's `RenderNavMarkup` seam. Load-bearing: **NFR8** (no meaningful context → clean fallback, never an empty band), the existing **three-surface parity** contract (HTML/webview/SPA share one render seam), and **no new authoring schema** (every field this story surfaces already exists on today's view models).

## Design Direction — One New Seam, Reused Everywhere

**This is the #1 review checkpoint: the shape of the new local-context seam and which page kinds get rich context vs. clean fallback.**

### The seam

1. **New `NavLocalContext` record** (sibling of `NavItem`/`NavGroup` in [NavigationView.cs](src/SpecScribe/NavigationView.cs)): a small, host-neutral view model —
   ```csharp
   public sealed record NavLocalItem(string Label, string Href, bool IsActive);
   public sealed record NavLocalContext(string Title, IReadOnlyList<NavLocalItem> Items);
   ```
   `Href` is already-relative-to-the-current-page (the same convention `PagerLink.Href` uses — see [EntityPager.cs](src/SpecScribe/EntityPager.cs)), so the renderer never recomputes a prefix per item. `Title` is the small label atop the band (e.g. "Stories in this epic", "Files in this directory", "ADRs", "This requirement's family"). An empty or null `NavLocalContext` means "no rich context for this page" — the renderer's fallback rule (below) takes over.

2. **`NavigationView` gains `NavLocalContext? LocalContext { get; init; }`** (nullable, defaults null — every existing call site keeps compiling and rendering exactly as today).

3. **`SiteNav.ToNavigationView` gains an optional `NavLocalContext? localContext = null` parameter**, threaded straight onto the new field. This is the single place every page-building call site (both the `PageView`-based family — Epics/Story/Home via `EpicsTemplater`/`HtmlTemplater` — and the standalone-`StringBuilder`-via-`RenderNavBar` family — code files, requirements, follow-ups, ADRs, commits, insight pages) already calls (`nav.ToNavigationView(outputPath)` or `nav.RenderNavBar(outputPath)` → `ToNavigationView` under the hood). Add an overload of `RenderNavBar` that also takes the optional local context so standalone templaters don't need to hand-build a `NavigationView` themselves.

4. **`HtmlRenderAdapter.AppendKeyViewsBand`** ([HtmlRenderAdapter.cs:148](src/SpecScribe/HtmlRenderAdapter.cs)) gets a third branch, ordered:
   - `onHome` → work-mode strip (**unchanged**, Story 9.8).
   - else `nav.LocalContext is { Items.Count: > 0 }` → render the **new** local-context band: the `Title` as a small label + each `NavLocalItem` as a pill (active one marked `class="... active" aria-current="page"`, reusing the existing `.quick-link-pill`-style visual language so the white band still reads as one system, but under a distinct CSS family, e.g. `.site-nav-local-context`/`.local-context-pill`, so it can be told apart from the generic quick-links band in tests/CSS).
   - else → **today's exact generic quick-links band** (unchanged fallback — this is the literal AC2 "falls back cleanly to the global nav" behavior, and it is not a new code path, just the existing one falling through).

   Because this lives inside the ONE `RenderNavMarkup` → `AppendKeyViewsBand` seam every surface already calls, HTML/webview/SPA pick it up identically with no new adapter-specific wiring (same argument Story 10.1 already proved for the dark bar).

### Page-kind → content mapping (build the `NavLocalContext` at each call site from data already on hand — do not invent new computation)

| Page kind | `Title` (suggested; confirm at review) | `Items` source | Active item |
|-----------|------|-----------------|--------------|
| **Home** | — (unchanged work-mode strip) | n/a | n/a |
| **Epic** | "Stories in this epic" | `epic.Stories` → each story's resolved href (`story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id)` — the exact expression `BuildEpicPage` already uses for `ChildTargets`) | none (epic page has no single "current" story) |
| **Story** (drafted + placeholder) | "Stories in Epic {N}" | Same `epic.Stories` sibling list (the `epic` parameter both `BuildStoryPage` and `BuildStoryPlaceholderPage` already receive) | the current story marked active |
| **Code file** | "Files in {directory}" (or reuse whatever label the existing sibling-file family computation already carries) | The same alphabetical sibling-file family already built for the code-file `EntityPager` — reuse the underlying list, don't recompute a second one | the current file marked active |
| **ADR** | "ADRs" | The same `_adrs` list already passed to `EntityPager.FromSequence(_adrs, i, ...)` at [SiteGenerator.cs:804](src/SpecScribe/SiteGenerator.cs) | the current ADR marked active |
| **Commit** (`commit/*.html`, `commits/{date}.html`) | "Recent commits" / "Commits on {date}" | The same commit/day family already built for that page's `EntityPager` | the current commit/day marked active |
| **Requirement** | "{Category} requirements" (or the requirement's kind grouping — same grouping the requirements index already uses) | Other requirements in the same group as `req` (mirror the `groups` construction in `RequirementsTemplater.RenderIndex`) — **owner latitude**: same-category siblings vs. the covering epic's other requirements; pick whichever reads better and confirm at review | the current requirement marked active |
| **Follow-up group page** (`follow-ups/group-*.html`) | Likely **no rich local context beyond itself** — the page body already IS the full group listing (`FollowUpGroupTemplater`), so duplicating it in the nav band would be redundant. Confirm at review whether to degrade to the generic fallback here (recommended) or show a compact version. | n/a if degrading | n/a |
| **Follow-up detail page** (`follow-ups/{slug}.html`, Story 9.11) | "This group" / the item's attributed epic label | The item's group membership already resolvable via the same attribution `FollowUpDetailTemplater` uses (epic-retro provenance / `crossLinks`) — link back to the owning `follow-ups/group-*.html` page and, where resolvable, list sibling items in that group | the current item marked active (or omit if the group page doesn't enumerate items in a directly reusable list — confirm at review) |
| **Action Items / Deferred Work root pages** (`action-items.html`, `deferred-work.html`) | No richer "family" exists above these — they ARE the top of their own hierarchy. **This is the concrete NFR8 fallback case**: degrade cleanly to the generic global quick-links band. | n/a (no `NavLocalContext` built) | n/a |
| **Insight** (`git-insights.html`, `deep-analytics.html`, `code-map.html`) | "Insights" | The Story 10.1 Insights nav-group's own children (`nav.Groups.First(g => g.ConceptKey == "Insights"...)` or equivalent) — the exact same membership already computed for the dark bar's Insights group, just re-surfaced in the white band | the current insight page marked active |

**Latitude, not a rigid spec:** several rows above say "owner latitude" or "confirm at review" — the AC's own wording ("a page-type → nav-content mapping specifies...") asks for a *defined* mapping, not necessarily the exact wording above. Where two reasonable data sources exist (e.g. requirement family = same-category vs. same-epic), pick the simpler one to wire correctly and flag the choice for the review checkpoint, mirroring how Stories 10.1/10.5/10.7 handled their own "owner-selected" visual/taxonomy decisions.

### Guardrails (do not violate)

- **No new authoring schema.** Every `NavLocalItem` comes from a field that already exists on today's domain/view models (epics.md-derived, git-derived, or the existing `_adrs`/sibling-file lists) — nothing new is parsed.
- **No information-bearing JavaScript** (same constraint Story 10.1 stated for group disclosure) — the local-context band is plain `<a>` links, no client script, so it needs no exception for the webview's strict CSP or the SPA's innerHTML swap.
- **Never color-only; reuse existing badge/pill vocabulary where a status is shown** (e.g. an epic's story pills MAY reuse `StatusStyles.Badge` for each story's stage — optional enrichment, not required by the AC, but don't hand-roll a new color scheme if you do add status).
- **`RenderParity` must stay green with no new `HostRenderException`.** The local-context band is plain anchors inside `site-nav-key-views` (or a sibling container) — keep it out of `site-nav-links`'s anchor scope that `RenderParity.ExtractNav` recovers nav *facts* from (Story 10.1's own constraint), so it doesn't get mistaken for a global nav item and doesn't change `NavigationView.Items`.
- **Golden fingerprint changes on every interior page** (the white band differs by page kind now) — regenerate deliberately, confirm stability across ≥2 runs before locking the constant ([golden-diff-normalization-gotchas]).
- **Follow existing empty-state convention** when a page kind's data source resolves to zero items (e.g. a lone ADR with no other ADRs, a story with no epic siblings because the epic has exactly one story) — that's `IsEmpty`-equivalent for `NavLocalContext`, and per the AC2 fallback rule, an empty local context should behave exactly like a null one (fall back to the generic band), not render a one-item band that looks broken.

## Tasks / Subtasks

- [ ] **Task 1 — Introduce the `NavLocalContext` seam** (AC: 1, 2)
  - [ ] Add `NavLocalItem`/`NavLocalContext` records to [NavigationView.cs](src/SpecScribe/NavigationView.cs).
  - [ ] Add `NavigationView.LocalContext` (nullable, default null).
  - [ ] Add the optional `localContext` parameter to `SiteNav.ToNavigationView` and thread it onto the new field; add a matching optional-parameter overload of `SiteNav.RenderNavBar` for the standalone-templater call sites.

- [ ] **Task 2 — Branch `AppendKeyViewsBand` on local context** (AC: 1, 2)
  - [ ] Insert the local-context branch between the `onHome` check and the generic quick-links fallback in [HtmlRenderAdapter.cs:148](src/SpecScribe/HtmlRenderAdapter.cs), per the Design Direction ordering.
  - [ ] New CSS family (e.g. `.site-nav-local-context`, `.local-context-pill`, `.local-context-pill.active`) in [specscribe.css](src/SpecScribe/assets/specscribe.css) — reuse the `.quick-link-pill` visual language (color, spacing, focus ring) rather than inventing a new look; verify the webview `.vscode-*` theme bridge covers it (likely no change if existing pill variables are reused).
  - [ ] Confirm the local-context markup stays inside the white sub-header band, outside `site-nav-links`'s anchor scope (`RenderParity.ExtractNav` guard).

- [ ] **Task 3 — Wire Epic and Story pages** (AC: 1, 2)
  - [ ] `EpicsTemplater.BuildEpicPage`: build a `NavLocalContext("Stories in this epic", ...)` from `epic.Stories`, pass it through to `nav.ToNavigationView(outputPath, localContext)`.
  - [ ] `EpicsTemplater.BuildStoryPage` / `BuildStoryPlaceholderPage`: same `epic.Stories` sibling list, current story marked active.
  - [ ] `EpicsTemplater.BuildIndexPage` (the epics INDEX, not an individual epic): confirm whether it gets rich context (e.g. jump-to-epic list) or degrades to the generic band — the AC only names "epic" and "story" pages, not the index; default to leaving the index on the generic fallback unless it's trivially cheap to add, and note the decision.

- [ ] **Task 4 — Wire Code File pages** (AC: 1, 2)
  - [ ] `CodeFileTemplater`: reuse the sibling-file family already assembled for the code-file `EntityPager` (do not recompute a second directory listing) to build `NavLocalContext("Files in {directory}", ...)`.

- [ ] **Task 5 — Wire ADR pages** (AC: 1, 2)
  - [ ] At the ADR page call site ([SiteGenerator.cs:804](src/SpecScribe/SiteGenerator.cs) and wherever `AdrTemplater`/equivalent renders each ADR page), reuse the same `_adrs` list already passed to `EntityPager.FromSequence` to build `NavLocalContext("ADRs", ...)`.

- [ ] **Task 6 — Wire Commit pages** (AC: 1, 2)
  - [ ] `CommitDayTemplater` and the per-commit page renderer: reuse the same commit/day family already backing their `EntityPager`.

- [ ] **Task 7 — Wire Requirement pages** (AC: 1, 2)
  - [ ] `RequirementsTemplater.RenderRequirement`: build `NavLocalContext` from the requirement's family (owner-latitude choice per Design Direction — same-category siblings recommended as the simpler, always-available option), current requirement marked active.
  - [ ] Confirm the requirements INDEX page's own behavior (likely stays on the generic fallback — it already has its own in-page "Jump to a group" navigator; don't duplicate that into the white band).

- [ ] **Task 8 — Wire Follow-up pages** (AC: 1, 2)
  - [ ] `FollowUpDetailTemplater` (9.11 per-item pages): build local context from the item's already-resolved group/epic attribution, linking back to its `follow-ups/group-*.html` page.
  - [ ] `FollowUpGroupTemplater`, `action-items.html`, `deferred-work.html`: default to the generic fallback (no `NavLocalContext` built) per the Design Direction's explicit NFR8 example — confirm at review if richer context is wanted instead.

- [ ] **Task 9 — Wire Insight pages** (AC: 1, 2)
  - [ ] `GitInsightsTemplater`, `DeepAnalyticsTemplater`, `CodeMapTemplater`: build `NavLocalContext("Insights", ...)` from the Insights nav group's own children (reuse `nav.Groups`, do not recompute a parallel list).

- [ ] **Task 10 — Guardrails** (AC: 1, 2)
  - [ ] No new authoring schema; every `NavLocalItem` traces to an existing field.
  - [ ] `RenderParity` stays green, no new `HostRenderException`.
  - [ ] Empty local context (`Items.Count == 0`) behaves identically to a null one (falls back to the generic band) — never a degenerate one-item-looks-broken band.
  - [ ] webview CSP / SPA innerHTML swap need no exception (plain anchors, no JS).

- [ ] **Task 11 — Tests + golden** (AC: 1, 2)
  - [ ] Per page-kind unit tests: rich context renders with the right items + active marking; absent/empty data degrades to the generic band (both halves of NFR8, matching the existing Story 10.1 present/absent test pattern).
  - [ ] `RenderParityTests` — confirm the local-context band doesn't register as a nav fact and doesn't trip a new divergence on webview/SPA.
  - [ ] Golden fingerprint regen (every interior page's white band changes) — confirm stability across ≥2 runs before locking the constant ([golden-diff-normalization-gotchas]).
  - [ ] `dotnet test` from repo root, full suite green.

- [ ] **Task 12 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [ ] `dotnet run --project src/SpecScribe -- generate --deep-git` against this repo; open an epic page, a story page, a code file page, an ADR page, a commit page, a requirement page, a follow-up detail page, and an insight page (git-insights/deep-analytics/code-map) — confirm each white band shows the expected page-type context with the active item marked.
  - [ ] Confirm Home is unchanged (still the work-mode strip) and confirm a page kind you deliberately left on the fallback (e.g. `action-items.html`) still shows the generic quick-links band, not an empty one.
  - [ ] Confirm `--spa` and the webview both render the same contextual band (open the SPA in the preview browser; confirm the webview theme bridge covers the new pill class).

## Dev Notes

### Architecture patterns & constraints (must follow)

- **One render seam, three surfaces** (Story 10.1's own framing, extended here): `HtmlRenderAdapter.RenderNavMarkup` → `AppendKeyViewsBand` is the ONE place the white band is drawn; every surface (HTML `RenderNav`, webview `RenderNavMarkup` directly, SPA per-swap) calls through it, so wiring the new branch there propagates to all three by construction — no adapter-specific code.
- **Reuse, don't recompute.** Every page kind in the mapping table already has its sibling/family/child data assembled for some other purpose (an `EntityPager`, `epic.Stories`, `_adrs`, `nav.Groups`). Pull the `NavLocalContext` from that existing computation; do not stand up a second, potentially-diverging query for the same family.
- **NFR8 is the fallback contract, not a new failure mode.** "No meaningful local context" degrades to the band that already exists today (the generic quick-links pills) — this is not a new empty state to design, it's the code path that's already there falling through unchanged when `LocalContext` is null/empty.
- **Golden byte-identity is a gate, expected to move.** Unlike some Epic 10 stories where a change was scoped to CSS-only, this one changes the white-band HTML on nearly every interior page kind — regenerate deliberately and diff to confirm the change is exactly the local-context band (plus any incidental CSS), nothing else.

### Source tree — files to touch

| File | Change |
|------|--------|
| `src/SpecScribe/NavigationView.cs` | Add `NavLocalItem`/`NavLocalContext`; add `NavigationView.LocalContext` (**UPDATE**) |
| `src/SpecScribe/SiteNav.cs` | `ToNavigationView`/`RenderNavBar` optional `localContext` parameter (**UPDATE**) |
| `src/SpecScribe/HtmlRenderAdapter.cs` | `AppendKeyViewsBand` new local-context branch (**UPDATE**) |
| `src/SpecScribe/EpicsTemplater.cs` | Build `NavLocalContext` for epic/story/placeholder pages (**UPDATE**) |
| `src/SpecScribe/CodeFileTemplater.cs` | Build `NavLocalContext` from the existing sibling-file family (**UPDATE**) |
| `src/SpecScribe/SiteGenerator.cs` | Build `NavLocalContext` for ADR + commit page call sites; wire the insight pages' Insights-group reuse (**UPDATE**) |
| `src/SpecScribe/RequirementsTemplater.cs` | Build `NavLocalContext` for `RenderRequirement` (**UPDATE**) |
| `src/SpecScribe/FollowUpDetailTemplater.cs` | Build `NavLocalContext` from item group/epic attribution (**UPDATE**) |
| `src/SpecScribe/GitInsightsTemplater.cs` / `DeepAnalyticsTemplater.cs` / `CodeMapTemplater.cs` | Build `NavLocalContext("Insights", ...)` from `nav.Groups` (**UPDATE**) |
| `src/SpecScribe/assets/specscribe.css` | New `.site-nav-local-context`/`.local-context-pill` classes reusing `.quick-link-pill` language (**UPDATE**) |
| `tests/SpecScribe.Tests/*` | Per-page-kind assertions + golden regen (**UPDATE**) |

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Epic's story list | `epic.Stories` | `EpicsTemplater.BuildEpicPage`/`BuildStoryPage` params |
| Story href resolution | `story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id)` | `EpicsTemplater.BuildEpicPage` (`ChildTargets`) |
| Code file sibling family | The existing directory listing behind the code-file `EntityPager` | `CodeFileTemplater.cs:703` area |
| ADR family | `_adrs` (the list already passed to `EntityPager.FromSequence`) | `SiteGenerator.cs:804` |
| Insights group membership | `nav.Groups` (`NavGroup` with `ConceptKey`/label matching "Insights") | `NavigationView.Groups` / `SiteNav.Build` |
| Requirement grouping | The category `groups` construction | `RequirementsTemplater.RenderIndex` |
| Follow-up item attribution | Existing epic-retro/`crossLinks` resolution | `FollowUpDetailTemplater.RenderActionPage`/`RenderDeferredPage` |
| Prev/next sibling convention (mirror, don't fork) | `EntityPager`/`PagerLink` | `EntityPager.cs` |
| Nav render seam | `HtmlRenderAdapter.RenderNavMarkup` → `AppendKeyViewsBand` | `HtmlRenderAdapter.cs:64`/`:148` |

### Guardrails & invariants

- **NFR8**: absent/empty local context → the existing generic band, never a broken one-item band.
- **No new authoring schema.**
- **No information-bearing JS**; native anchors only.
- **`RenderParity`**: local-context anchors must not be recovered as `NavigationView.Items` nav facts — keep them out of `site-nav-links`.
- **Never color-only** if status is optionally added to any local-context item.

### Previous story intelligence

- **From 10.1 (review):** The exact "one render seam, three surfaces" argument this story reuses; `RenderNavMarkup`'s split from `RenderNav` (webview calls the markup-only method) is why a new branch inside it needs no webview-specific code. Also: keep any new interactive-looking element non-anchor if it must not register as a `RenderParity` nav fact (10.1 used `<summary>` for exactly this reason — this story's local-context band is plain `<a>`s in a different container, so the same discipline applies via placement, not element choice).
- **From 10.8 (ready-for-dev, not yet built):** A closely related "one shared row/primitive, many call sites" story — if 10.8 lands first and its `ListRow`/empty-state helper exists, prefer reusing its promoted empty-state convention for any local-context degrade-messaging (though the primary fallback here is simply "show the generic band," not an empty-state message).
- **From golden-diff-normalization-gotchas:** confirm a regenerated fingerprint is stable across ≥2 runs before locking it in as the expected constant (known stale-first-hash trap).

### Testing standards

- xUnit; `Assert.Contains`/`DoesNotContain` on emitted HTML, matching existing `HtmlRenderAdapterTests`/`SiteNavTests` patterns.
- Cover both the presence case (rich context renders, correct active marking) and the absence case (missing/empty data degrades to the generic band) per page kind — the NFR8 pair, same discipline Story 10.1 used for its group-gating tests.
- Full suite green including golden + `RenderParity` + SPA/webview parity suites.

### Project Structure Notes

- No new authoring schema, no `sprint-status.yaml`/epics.md shape changes.
- Output dir is `SpecScribeOutput` (never `docs/live`) — see [generate-output-dir-is-specscribeoutput].
- If working in a git worktree, target the worktree path (main has a background auto-committer) — see [worktree-edits-must-target-worktree-path].

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.10] — this story's ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Portal Legibility for Every Audience] — FR27–29, UX-DR25/27–30, NFR8.
- [Source: _bmad-output/implementation-artifacts/10-1-insights-navigation-and-structure-page-retirement.md] — the `RenderNavMarkup` seam, group disclosure discipline, `RenderParity` nav-fact constraint this story extends.
- [Source: _bmad-output/implementation-artifacts/10-8-unified-list-page-grammar-across-every-index.md] — sibling "one shared primitive, many call sites" story; reuse its empty-state convention if timing allows.
- [Source: src/SpecScribe/NavigationView.cs]
- [Source: src/SpecScribe/SiteNav.cs]
- [Source: src/SpecScribe/HtmlRenderAdapter.cs#AppendKeyViewsBand]
- [Source: src/SpecScribe/EpicsTemplater.cs]
- [Source: src/SpecScribe/CodeFileTemplater.cs]
- [Source: src/SpecScribe/EntityPager.cs]
- [Source: src/SpecScribe/RequirementsTemplater.cs]
- [Source: src/SpecScribe/FollowUpDetailTemplater.cs] / [src/SpecScribe/FollowUpGroupTemplater.cs]
- [Source: src/SpecScribe/SiteGenerator.cs] (ADR/commit page call sites)
- [Source: docs/UserJourneys.md] — journeys this story continues to serve (J1/J2 pulse, J5 onboarding, J6 health, J7 follow-ups).

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. This story extends Story 10.1's `RenderNavMarkup` seam with a new `NavLocalContext` carried on `NavigationView`, rendered by a new branch in `AppendKeyViewsBand` between the Home work-mode strip and the existing generic quick-links fallback, so the white sub-header band shows page-type-appropriate context (epic's stories, code file siblings, ADR family, requirement family, follow-up group, insight-group siblings) built entirely from data each page kind already computes elsewhere — with a clean fallback to today's generic band when no rich context applies.

### File List
