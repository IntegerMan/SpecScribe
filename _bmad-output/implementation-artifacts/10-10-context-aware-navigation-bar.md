---
baseline_commit: 27d37b69456526e2d89ca8111a3bf9cbc645f4dc
---

# Story 10.10: Context-Aware Navigation Bar

Status: review

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

- [x] **Task 1 — Introduce the `NavLocalContext` seam** (AC: 1, 2)
  - [x] Add `NavLocalItem`/`NavLocalContext` records to [NavigationView.cs](src/SpecScribe/NavigationView.cs).
  - [x] Add `NavigationView.LocalContext` (nullable, default null).
  - [x] Add the optional `localContext` parameter to `SiteNav.ToNavigationView` and thread it onto the new field; add a matching optional-parameter overload of `SiteNav.RenderNavBar` for the standalone-templater call sites.

- [x] **Task 2 — Branch `AppendKeyViewsBand` on local context** (AC: 1, 2)
  - [x] Insert the local-context branch between the `onHome` check and the generic quick-links fallback in [HtmlRenderAdapter.cs:148](src/SpecScribe/HtmlRenderAdapter.cs), per the Design Direction ordering.
  - [x] New CSS family (e.g. `.site-nav-local-context`, `.local-context-pill`, `.local-context-pill.active`) in [specscribe.css](src/SpecScribe/assets/specscribe.css) — reuse the `.quick-link-pill` visual language (color, spacing, focus ring) rather than inventing a new look; verify the webview `.vscode-*` theme bridge covers it (likely no change if existing pill variables are reused).
  - [x] Confirm the local-context markup stays inside the white sub-header band, outside `site-nav-links`'s anchor scope (`RenderParity.ExtractNav` guard).

- [x] **Task 3 — Wire Epic and Story pages** (AC: 1, 2)
  - [x] `EpicsTemplater.BuildEpicPage`: build a `NavLocalContext("Stories in this epic", ...)` from `epic.Stories`, pass it through to `nav.ToNavigationView(outputPath, localContext)`.
  - [x] `EpicsTemplater.BuildStoryPage` / `BuildStoryPlaceholderPage`: same `epic.Stories` sibling list, current story marked active.
  - [x] `EpicsTemplater.BuildIndexPage` (the epics INDEX, not an individual epic): confirm whether it gets rich context (e.g. jump-to-epic list) or degrades to the generic band — the AC only names "epic" and "story" pages, not the index; default to leaving the index on the generic fallback unless it's trivially cheap to add, and note the decision.

- [x] **Task 4 — Wire Code File pages** (AC: 1, 2)
  - [x] `CodeFileTemplater`: reuse the sibling-file family already assembled for the code-file `EntityPager` (do not recompute a second directory listing) to build `NavLocalContext("Files in {directory}", ...)`.

- [x] **Task 5 — Wire ADR pages** (AC: 1, 2)
  - [x] At the ADR page call site ([SiteGenerator.cs:804](src/SpecScribe/SiteGenerator.cs) and wherever `AdrTemplater`/equivalent renders each ADR page), reuse the same `_adrs` list already passed to `EntityPager.FromSequence` to build `NavLocalContext("ADRs", ...)`.

- [x] **Task 6 — Wire Commit pages** (AC: 1, 2)
  - [x] `CommitDayTemplater` and the per-commit page renderer: reuse the same commit/day family already backing their `EntityPager`.

- [x] **Task 7 — Wire Requirement pages** (AC: 1, 2)
  - [x] `RequirementsTemplater.RenderRequirement`: build `NavLocalContext` from the requirement's family (owner-latitude choice per Design Direction — same-category siblings recommended as the simpler, always-available option), current requirement marked active.
  - [x] Confirm the requirements INDEX page's own behavior (likely stays on the generic fallback — it already has its own in-page "Jump to a group" navigator; don't duplicate that into the white band).

- [x] **Task 8 — Wire Follow-up pages** (AC: 1, 2)
  - [x] `FollowUpDetailTemplater` (9.11 per-item pages): build local context from the item's already-resolved group/epic attribution, linking back to its `follow-ups/group-*.html` page.
  - [x] `FollowUpGroupTemplater`, `action-items.html`, `deferred-work.html`: default to the generic fallback (no `NavLocalContext` built) per the Design Direction's explicit NFR8 example — confirm at review if richer context is wanted instead.

- [x] **Task 9 — Wire Insight pages** (AC: 1, 2)
  - [x] `GitInsightsTemplater`, `DeepAnalyticsTemplater`, `CodeMapTemplater`: build `NavLocalContext("Insights", ...)` from the Insights nav group's own children (reuse `nav.Groups`, do not recompute a parallel list).

- [x] **Task 10 — Guardrails** (AC: 1, 2)
  - [x] No new authoring schema; every `NavLocalItem` traces to an existing field.
  - [x] `RenderParity` stays green, no new `HostRenderException`.
  - [x] Empty local context (`Items.Count == 0`) behaves identically to a null one (falls back to the generic band) — never a degenerate one-item-looks-broken band.
  - [x] webview CSP / SPA innerHTML swap need no exception (plain anchors, no JS).

- [x] **Task 11 — Tests + golden** (AC: 1, 2)
  - [x] Per page-kind unit tests: rich context renders with the right items + active marking; absent/empty data degrades to the generic band (both halves of NFR8, matching the existing Story 10.1 present/absent test pattern).
  - [x] `RenderParityTests` — confirm the local-context band doesn't register as a nav fact and doesn't trip a new divergence on webview/SPA.
  - [x] Golden fingerprint regen (every interior page's white band changes) — confirm stability across ≥2 runs before locking the constant ([golden-diff-normalization-gotchas]).
  - [x] `dotnet test` from repo root, full suite green.

- [x] **Task 12 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [x] `dotnet run --project src/SpecScribe -- generate --deep-git` against this repo; open an epic page, a story page, a code file page, an ADR page, a commit page, a requirement page, a follow-up detail page, and an insight page (git-insights/deep-analytics/code-map) — confirm each white band shows the expected page-type context with the active item marked.
  - [x] Confirm Home is unchanged (still the work-mode strip) and confirm a page kind you deliberately left on the fallback (e.g. `action-items.html`) still shows the generic quick-links band, not an empty one.
  - [x] Confirm `--spa` and the webview both render the same contextual band (open the SPA in the preview browser; confirm the webview theme bridge covers the new pill class).

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

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet build src/SpecScribe` — clean throughout (0 warnings, 0 errors) after each task.
- `dotnet test tests/SpecScribe.Tests` — full suite green (1689 tests) after fixes.
- `dotnet run --project src/SpecScribe -- generate --deep-git` (and `--spa`) against this repo (613 pages) — verified live via direct HTML inspection of every page kind.

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. This story extends Story 10.1's `RenderNavMarkup` seam with a new `NavLocalContext` carried on `NavigationView`, rendered by a new branch in `AppendKeyViewsBand` between the Home work-mode strip and the existing generic quick-links fallback, so the white sub-header band shows page-type-appropriate context (epic's stories, code file siblings, ADR family, requirement family, follow-up group, insight-group siblings) built entirely from data each page kind already computes elsewhere — with a clean fallback to today's generic band when no rich context applies.

Implemented per the Design Direction, with one review-flagged latitude call actually taken:

- **Task 1–2 (seam):** `NavLocalItem`/`NavLocalContext` added to `NavigationView.cs`; `NavigationView.LocalContext` (nullable); `SiteNav.ToNavigationView`/`RenderNavBar` gained an optional `localContext` parameter. `HtmlRenderAdapter.AppendKeyViewsBand` gained the local-context branch between the Home strip and the generic fallback; new `.site-nav-local-context`/`.local-context-*` CSS reusing the `.quick-link-pill` visual language (no webview-bridge change needed — reuses existing semantic tokens). The band lives outside `site-nav-links`, so `RenderParity.ExtractNav` never recovers its anchors as nav facts (added a regression test proving it).
- **Task 3 (Epic/Story):** `EpicsTemplater.BuildEpicPage`/`BuildStoryPage`/`BuildStoryPlaceholderPage` share a new `BuildStoriesLocalContext` helper over `epic.Stories`. Epics INDEX page left on the generic fallback per the story's own default guidance.
- **Task 4 (Code files):** `SiteGenerator`'s code-page loop builds the local context from the exact `siblings` list already assembled for the `EntityPager` (no second directory read).
- **Task 5 (ADRs):** the ADR pass-2 loop builds it from the same `_adrs` list already passed to `EntityPager.FromSequence`; `HtmlTemplater.RenderPage` gained an optional `localContext` param (used only by the ADR call site — every other doc page stays on the generic fallback, matching the mapping table).
- **Task 6 (Commits):** both `CommitDayTemplater.RenderPage` and `CommitDetailTemplater.RenderPage` gained the optional param, fed from the same `days`/`slots` families already backing their pagers.
- **Task 7 (Requirements):** `RequirementsTemplater.RenderRequirement` takes the same-category sibling group (owner-latitude call taken: same-category siblings, the simpler always-available option per the Design Direction) via a new `RequirementGroupLabel`/`BuildRequirementLocalContext` pair. Requirements INDEX stays on the generic fallback (already has its own in-page navigator).
- **Task 8 (Follow-ups) — revised at review:** the first pass took a same-epic-attribution shortcut (flagged as a review checkpoint); the owner asked for the full 9.13 group-page membership instead, so it now reuses that directly. `WriteFollowUpDetails` computes `FollowUpGroupPages.Enumerate(geometry, unplanned, _epicsModel)` once (the SAME call `WriteFollowUpGroupPages` already makes) and indexes every member by its detail href (`groupByHref`, normalized via a new `NormalizeFollowUpHref` that strips any baked-in depth prefix — mirrors `FollowUpGroupTemplater.ToListBatchEntry`'s own defensive stripping). Both the action-item and deferred-item loops resolve their item's containing spec through a shared `BuildFollowUpGroupLocalContext` helper and reuse `spec.Title`/`spec.Members` verbatim — so a detail page's "This group" band now shows the EXACT same title and membership as the `follow-ups/group-*.html` page it links back to (Follow-ups orphan / Unplanned / Epic N follow-ups), including quick-dev entries alongside actions and deferred items. `FollowUpGroupTemplater`/`action-items.html`/`deferred-work.html` still stay on the generic fallback (unchanged, per the Design Direction). Verified live: `follow-ups/action-add-a-review-checklist-item-for-the-parser.html`'s band title ("Epic 3 follow-ups") and membership now match `follow-ups/group-epic-3.html`'s own `<h1>` and row list exactly.
- **Task 9 (Insights):** new `SiteNav.BuildInsightsLocalContext` reads `Groups` for the "Insights" label; returns null when the group doesn't exist OR collapsed to a single flat link (see NFR8 note below). Wired into `GitInsightsTemplater`/`DeepAnalyticsTemplater`/`CodeMapTemplater`.
- **Task 10 (Guardrails) — one review-worthy self-caught bug:** the initial `AppendKeyViewsBand` guard was `Items.Count > 0`, which let a code file with no sibling files (or a requirement/story/ADR whose group has no other members) render a **one-item band containing only itself** — a real self-link at first (`href` pointing at its own page), then, after fixing the active item to render as plain text (see below), a one-pill band that added nothing (labeled "Files in X" with only the current file, non-actionable). Fixed twice: (1) the active `NavLocalItem` now renders as `<span>` never `<a href>` — the same "current page never self-links" rule `RenderBreadcrumb`'s last crumb already follows — caught by `SiteGeneratorTraceabilityTests.GenerateAll_SkipsSelfLinkOnRequirementDetailPage` failing on FR6's own detail page; (2) the guard was tightened to `Items.Any(i => !i.IsActive)` so a context with only the current item (no real navigation target) falls back to the generic band — caught by a second real-repo verification pass over this repo's own single-file code directories. Golden fingerprint regenerated twice accordingly.
- **Task 11 (Tests + golden):** added 10 new tests — 4 in `HtmlRenderAdapterTests` (rich context, null fallback, empty fallback, Home-ignores-local-context), 5 in `SiteNavTests` (`BuildInsightsLocalContext` present/single-collapses-to-null/absent, `ToNavigationView` threading), 1 in `RenderParityTests` (local-context anchors never recovered as nav facts). Golden fingerprint regenerated and confirmed stable across 3 repeated runs (first regen) + 2 repeated runs (second regen after the NFR8 tightening). Full suite: 1689 tests green.
- **Task 12 (real-repo verification):** ran `generate --deep-git` (613 pages) and `generate --deep-git --spa` against this repo; verified via direct HTML inspection (the in-session browser tool couldn't reliably navigate `file://` URLs in this sandbox — one screenshot did succeed and confirmed correct visual rendering of the epic page's "Stories in this epic" band) that every page kind (epic, story, code file with/without siblings, ADR, commit day, commit detail, requirement, follow-up detail, git-insights) shows the correct contextual band with active-item marking and no self-links; confirmed Home is unchanged and `action-items.html` correctly falls back to the generic band; confirmed `--spa` output carries the same band (same render seam, no adapter-specific code needed).

### File List

- `src/SpecScribe/NavigationView.cs` (UPDATE)
- `src/SpecScribe/SiteNav.cs` (UPDATE)
- `src/SpecScribe/HtmlRenderAdapter.cs` (UPDATE)
- `src/SpecScribe/EpicsTemplater.cs` (UPDATE)
- `src/SpecScribe/CodeFileTemplater.cs` (UPDATE)
- `src/SpecScribe/HtmlTemplater.cs` (UPDATE)
- `src/SpecScribe/CommitDayTemplater.cs` (UPDATE)
- `src/SpecScribe/CommitDetailTemplater.cs` (UPDATE)
- `src/SpecScribe/RequirementsTemplater.cs` (UPDATE)
- `src/SpecScribe/FollowUpDetailTemplater.cs` (UPDATE)
- `src/SpecScribe/GitInsightsTemplater.cs` (UPDATE)
- `src/SpecScribe/DeepAnalyticsTemplater.cs` (UPDATE)
- `src/SpecScribe/CodeMapTemplater.cs` (UPDATE)
- `src/SpecScribe/SiteGenerator.cs` (UPDATE)
- `src/SpecScribe/assets/specscribe.css` (UPDATE)
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` (UPDATE)
- `tests/SpecScribe.Tests/SiteNavTests.cs` (UPDATE)
- `tests/SpecScribe.Tests/RenderParityTests.cs` (UPDATE)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (UPDATE — golden fingerprint constant)

## Change Log

- 2026-07-19: Story implemented (dev-story). New `NavigationView.LocalContext`/`NavLocalContext`/`NavLocalItem` seam threaded through `SiteNav.ToNavigationView`/`RenderNavBar`, rendered by a new `HtmlRenderAdapter.AppendKeyViewsBand` branch; wired epic/story, code file, ADR, commit day/detail, requirement, and follow-up-detail pages onto it, with Insights pages reusing the dark bar's own `nav.Groups` membership. Self-caught + fixed a real defect during implementation: the active item must never self-link (mirrors `RenderBreadcrumb`'s rule) and a local context containing only the current item must fall back to the generic band (NFR8 — no degenerate one-item band), both caught via test failures and a real-repo verification pass. Golden fingerprint regenerated twice, stable across repeated runs. 1689 tests green (10 new).
- 2026-07-19: Follow-up detail pages' local context revised at review — swapped the same-epic-attribution shortcut for the actual Story 9.13 filtered group-page membership (`FollowUpGroupPages.Enumerate`, reused not re-derived), so a detail page's band now matches its `follow-ups/group-*.html` page exactly. No golden fingerprint change (the fixture has no follow-up items). Full suite green (1692 tests — the 3-test delta is unrelated concurrent work already on `main`, not this change).
