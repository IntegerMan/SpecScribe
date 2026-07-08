---
baseline_commit: 02c06c8c9bd9d9abfcae4763d6984667a2665943
---

# Story 2.4: Planning Artifacts Grouping, Status Badges, and PRD Prominence

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader arriving at the portal,
I want the planning artifacts organized meaningfully with the PRD front and center,
so that the most important planning documents are easy to find and their status is obvious at a glance.

## Acceptance Criteria

1. **Given** planning artifacts of different kinds (product brief, PRD, PRD quality review, UX design, UX experience)
   **When** I view the home planning section
   **Then** artifacts are grouped meaningfully (for example the PRD as a prominent primary document, UX design and experience together, the brief distinct)
   **And** each artifact's status is shown as a badge consistent with the site's status semantics, not plain text.

2. **Given** the PRD has an associated quality-review / rubric document
   **When** I view the planning section
   **Then** the quality review does not appear as a standalone top-level card
   **And** it is reachable as a branching/linked reference from the PRD (from the PRD card or its page).

> **Origin & scope:** Fourth story of Epic 2 (Complete and Faithful BMad Artifact Representation).
> This story reshapes the **home index card grid's "Planning Artifacts" section** — the folder-prefix bucket
> in `HtmlTemplater.RenderIndex` — into a meaningfully grouped, PRD-prominent, status-badged layout, and
> stops the PRD quality review (`review-rubric.md`) from reading as a peer document. It advances **FR2**
> (first-class BMad support), **FR5** (coherent navigation + complete artifact-class representation), **FR7**
> (truthful status surfacing), and **NFR2** (graceful degradation). It is **purely a home-index presentation
> change** over documents that are already discovered, parsed, and page-generated today — no new parser, no
> new page, no new nav item. Reuse the two seams that already carry this data: `ModuleContext`'s well-known
> planning-doc classification (filename → kind) and `StatusStyles`' single status-color vocabulary.

## Tasks / Subtasks

- [x] Task 1: Add a document-status → badge mapping to the single status-semantics seam (AC: #1)
  - [x] **Add `StatusStyles.ForDoc(string? status)` + `StatusStyles.DocLabel(string? status)`** next to `ForStory`/`ForEpic`/`ForRequirement`. Map the free-text frontmatter `status:` planning docs actually use onto the **existing** done/active/review/ready/pending classes — do NOT invent a parallel palette. Concretely, matching case-insensitively on the trimmed value: `final`/`approved`/`done`/`complete`/`published` → `done` (green); `review`/`in review` → `review`; `in-progress`/`in progress`/`wip`/`active` → `active` (teal); `ready`/`ready-for-dev` → `ready` (gold); `draft`/`drafting`/`proposed` → `drafted` (gold); empty/null/unknown → `pending` (parchment). Keep the label human-cased (e.g. `final` → "Final", `draft` → "Draft") — prefer showing the source word title-cased rather than remapping it to a lifecycle noun, so the badge stays truthful to what the doc declares (Story 1.5). [Source: `src/SpecScribe/StatusStyles.cs:1-67`]
  - [x] **No new CSS is required.** The base `.status-badge` rule is already the parchment/ink-light/border "pending" look, so a `pending`-classed badge renders correctly with zero additions; `done`/`active`/`review`/`ready`/`drafted`/`deferred` badge rules already exist. If Story 2.3 has already landed it will have added an explicit `.status-badge.pending` rule — that is harmless overlap; **do not duplicate it**, and do not depend on it (the base rule covers you either way). [Source: `src/SpecScribe/assets/specscribe.css:820-837`, `:1479`]

- [x] Task 2: Render the card status as a badge, not plain text (AC: #1)
  - [x] **`AppendIndexCard` today renders status as plain text** — it joins `Frontmatter.Status · Date · Author` into one `<p>` (a middot-separated string). Replace the *status* portion with a real badge: emit `<span class="status-badge {StatusStyles.ForDoc(status)}">{Html(StatusStyles.DocLabel(status))}</span>` when a status is present, and keep date/author as the de-emphasized `<p>` text. When there is no status, emit no badge (graceful). [Source: `src/SpecScribe/HtmlTemplater.cs:495-512`]
  - [x] **Scope decision (recommended: apply to every index card).** `AppendIndexCard` is the shared renderer for Overview / Planning Artifacts / Implementation Artifacts / Other. The AC only *mandates* the planning section, but having planning cards show badges while sibling sections still show plain-text status would read as inconsistent. Default: route all index cards through the badge (one code path, one behavior). This is low-risk — **no existing test asserts the old `Status · Date` plain-text string** (verified). If you instead choose planning-only, parameterize rather than fork the renderer. Either way, keep the badge text-carrying (UX-DR17: never color-only). [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs`]

- [x] Task 3: Group the Planning Artifacts section meaningfully with the PRD prominent (AC: #1)
  - [x] **Reuse the well-known-filename classification that already exists — do NOT invent a second classifier.** `ModuleContext.BmadMethodDocs` already maps the exact filenames in play: `prd.md` → PRD, `brief.md` → Product Brief, `DESIGN.md` → UX Design, `EXPERIENCE.md` → UX Experience (and `ARCHITECTURE-SPINE.md` → Architecture). Classify each doc in the planning-artifacts bucket by matching `Path.GetFileName(SourceRelativePath)` against these, exactly the filename-anywhere discipline `SiteNav.Build` uses. Anything unrecognized degrades to a normal card. Match by **filename**, never by hard-coded nested paths (`prds/`, `ux-designs/`) — folder layout varies and Epic 4 will generalize it. [Source: `src/SpecScribe/ModuleContext.cs:74-81`, `src/SpecScribe/SiteNav.cs:53-69`]
  - [x] **Render the section with intent:** the **PRD as a prominent primary card** (first, visually distinct — e.g. a wider/lead card or a labeled "Primary" treatment reusing existing card CSS, not a new component); **UX Design + UX Experience grouped together** (adjacent, under a shared "UX" sub-label or a paired sub-row); the **Product Brief distinct** (its own card, clearly not the PRD); any other planning docs as ordinary cards after these. Keep it on-brand by composing existing `index-grid`/`index-card`/`index-section-title` classes plus at most a small `index-card--primary` / sub-label style; do not build a foreign layout. [Source: `src/SpecScribe/HtmlTemplater.cs:61-118`, `:495-512`, `src/SpecScribe/assets/specscribe.css:464-534`]
  - [x] **Drive the grouping off the module context you already have.** `RenderIndex`'s caller (`SiteGenerator.WriteIndex` → `HtmlTemplater.RenderIndex`) has access to the detected module; the planning-doc kinds should come from `ModuleContext.DocsFor(...)` / the passed `SiteNav`, not a literal list re-hard-coded in the templater. If threading the module in is heavier than warranted, a small private kind map keyed by the same filenames is acceptable **as long as it references the same filename constants** and degrades unknowns to plain cards. [Source: `src/SpecScribe/SiteGenerator.cs:435-436`, `src/SpecScribe/ModuleContext.cs:90-96`]
  - [x] **Graceful degradation (NFR2, Story 2.1 "no plan yet ≠ no data").** Missing PRD → no primary card, section still renders whatever exists. Only a brief, or only UX docs → they render in their distinct/paired slots with no empty "PRD" placeholder. No planning artifacts at all → the section is omitted entirely (existing `if (inGroup.Count == 0) continue;` behavior — preserve it). Never a broken link, never an empty labeled sub-group. [Source: `src/SpecScribe/HtmlTemplater.cs:97`, `_bmad-output/planning-artifacts/epics.md:287-291`]

- [x] Task 4: Fold the PRD quality review into the PRD instead of a standalone card (AC: #2)
  - [x] **Detect `review-rubric.md` as the PRD's companion by well-known filename** (ideally a sibling of `prd.md` — both live under the same `prds/<...>/` folder here). This is the same "companion of a primary doc" shape Story 2.2 used for spec companions and the ADR pipeline uses for `README.md`. [Source: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/review-rubric.md`, `_bmad-output/implementation-artifacts/2-2-first-class-rendering-of-spec-artifacts.md`]
  - [x] **Suppress the rubric as a standalone top-level card** in the home index grid — exclude it from the planning-artifacts card list (mark it consumed, the same way `SiteGenerator.WriteIndex` keeps the ADR landing page out of `_docs` so it "never doubles up as a document-grid card"). [Source: `src/SpecScribe/SiteGenerator.cs:435-448`, `src/SpecScribe/HtmlTemplater.cs:86-118`]
  - [x] **Still generate the rubric's page** so the link resolves — do NOT stop rendering it, only stop *carding* it. `review-rubric.md` is already produced as `.../review-rubric.html` by the normal `*.md` pipeline; leave that intact. [Source: `src/SpecScribe/SiteGenerator.cs:517-523`]
  - [x] **Link the rubric from the PRD.** Add a "Quality review →" (or similar) link on the **PRD card** pointing at the rubric's output page (AC#2 allows "from the PRD card or its page"; the card is the higher-value surface for a reader scanning the section). Resolve the href from the rubric doc's `OutputRelativePath` with the correct relative depth — never emit a link to a page that wasn't generated. [Source: `src/SpecScribe/HtmlTemplater.cs:495-512`, `src/SpecScribe/PathUtil.cs`]
  - [x] **Graceful degradation both ways.** PRD present but no rubric → PRD card shows no quality-review link (no dangling affordance). Rubric present but no PRD (unexpected) → fall back to rendering the rubric as an ordinary card rather than orphan-linking it or dropping it silently. [Source: `_bmad-output/planning-artifacts/epics.md` NFR2]

- [x] Task 5: Test coverage (AC: #1, #2)
  - [x] **`StatusStylesTests`**: `ForDoc`/`DocLabel` map `final`→`done`, `draft`→`drafted`, `ready`→`ready`, `in-progress`→`active`, `review`→`review`, and null/empty/unknown→`pending`; labels are the human-cased source word. Mirror the existing `[InlineData]` table style. [Source: `tests/SpecScribe.Tests/StatusStylesTests.cs:1-70`]
  - [x] **`HtmlTemplaterTests` (render-level string assertions — the house pattern)**: given planning-artifacts DocModels (a PRD with `status: final`, a brief with `status: draft`, DESIGN + EXPERIENCE, and a `review-rubric.md`), `RenderIndex` emits (a) a status **badge** with the mapped class on each planning card (not a middot text run), (b) the PRD as a prominent/primary card ahead of the others, (c) UX Design and UX Experience grouped together, (d) **no** standalone card for the rubric, (e) a link from the PRD card to the rubric's page. Also assert the home index still has exactly one `<main id="main-content">` and the skip link (Story 1.4), and that the badge does not introduce a second contradictory status number (Story 1.5). [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs:52-71`]
  - [x] **Graceful-degradation render cases**: no PRD → no primary card, no broken rubric link; PRD but no rubric → PRD card has no quality-review link; no planning docs → the Planning Artifacts section is absent. [Source: `src/SpecScribe/HtmlTemplater.cs:97`]
  - [x] **`SiteGenerator`-level test** (temp-dir fixture, following `SiteGeneratorFidelityTests`): with a `planning-artifacts/prds/.../prd.md` + sibling `review-rubric.md` present, the home index cards the PRD (prominent) and links the rubric, the rubric page **still exists on disk** (`review-rubric.html` written), and the rubric is **not** a top-level card — and there are no broken links. Confirm `.memlog.md` dotfiles remain excluded (already ignored by `IsIgnored`). [Source: `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs`, `src/SpecScribe/SiteGenerator.cs:620-627`]

- [x] Task 6: End-to-end validation with a real generation pass (AC: #1, #2)
  - [x] Run the focused test filter, then a real generation pass against this repo (it ships the full planning-artifacts fixture: brief `draft`, prd `final`, review-rubric no-frontmatter, DESIGN/EXPERIENCE `final`).
  - [x] Manually verify on `docs/live/index.html`: the Planning Artifacts section shows the **PRD as a prominent primary card** carrying a "Final" badge and a link to its quality review; **UX Design and UX Experience together**; the **Product Brief** as a distinct card with a "Draft" badge; **no** standalone "PRD Quality Review" card. Confirm `docs/live/planning-artifacts/prds/prd-SpecScribe-2026-07-05/review-rubric.html` still exists and the PRD-card link reaches it (no 404). Temporarily rename `review-rubric.md` and re-generate to confirm the PRD card drops the quality-review link cleanly with no broken link.

### Review Findings

- [x] [Review][Patch] `AppendCardStatusBadge` doesn't trim before its presence-check — a whitespace-only `status:` value renders a "Pending" badge instead of no badge, violating the graceful "no status emits no badge" contract [src/SpecScribe/HtmlTemplater.cs:581]
- [x] [Review][Patch] Dead `&& d != brief` clause in `AppendPlanningSection`'s "others" filter — always true since `brief` is already excluded via `claimed`; remove for clarity [src/SpecScribe/HtmlTemplater.cs:651]
- [x] [Review][Patch] `AppendPrimaryPrdCard` breaks the "whole card is a link" convention every other index card follows — only the title and optional branch link are clickable, leaving the badge/meta/path as dead space on the most prominent card on the page; fix with a CSS-only full-card overlay link (consistent with the project's no-JS approach) rather than nesting anchors [src/SpecScribe/HtmlTemplater.cs:669-684]
- [x] [Review][Defer] `FindByFileName` silently keeps only the first doc matching a well-known filename (e.g. a second `prd.md`) with no diagnostic; untested [src/SpecScribe/HtmlTemplater.cs:689-691] — deferred, pre-existing edge case, graceful (not broken), same scope note as Epic 4 generalization
- [x] [Review][Defer] `SprintSourcePath` picks the alphabetically-first `sprint-status.yaml` under `SourceRoot` with no diagnostic and no try/catch [src/SpecScribe/SiteGenerator.cs:566] — deferred, belongs to Story 2.3's scope, not 2.4's

## Developer Context Section

### Epic Context and Business Value

Epic 2 — "Complete and Faithful BMad Artifact Representation" — makes the portal reflect the **whole**
project truthfully. Story 2.1 surfaced quick-dev/deferred work; 2.2 surfaced the spec kernel; 2.3 surfaced
the sprint tracking file; **Story 2.4 makes the planning documents legible at a glance** — the PRD is the
single most important planning artifact a reader wants, yet today it sits as one alphabetical card among five
peers, its status buried in plain text, with a quality-review rubric masquerading as a co-equal document.
This story fixes the *presentation* of already-represented artifacts: PRD prominence, meaningful grouping,
status-as-badge, and folding the rubric under the PRD. It advances **FR2/FR5/FR7** and the graceful-
degradation guarantee **NFR2**. Later Epic 2 stories continue the arc: iconography (2.5) and comment
annotations (2.6).

### Story Foundation Extract

- **Primary concern:** a reader lands on Home, scrolls to the Planning Artifacts section, and immediately
  sees the PRD as *the* primary document (prominent, with its status as an on-brand badge), UX design +
  experience read as one pair, the brief reads as its own thing, and there is no confusing "PRD Quality
  Review" card competing with the PRD.
- **User outcome:** find the PRD fast → see it's "Final" at a glance → click through to its quality review
  from the PRD itself, not from a stray sibling card.
- **Success boundary:** a home-index presentation change built on existing seams — `HtmlTemplater.RenderIndex`
  card grid, `ModuleContext` filename classification, `StatusStyles` color vocabulary, existing card CSS. No
  new parser, page, nav item, dependency, or JS.
- **Regression boundary:** epics/stories/requirements/ADR/quick-dev/spec/sprint surfaces and tallies
  unchanged; the dashboard (stat row, Now & Next, sunburst, Overall Progress, quick-links) untouched; Story
  1.4 accessibility (skip link, single `main`, focus, aria) and Story 1.5 truthfulness preserved; antiquarian
  identity kept.

### Current Implementation Reality (READ THIS FIRST)

- **The home index card grid groups by folder prefix only.** `HtmlTemplater.RenderIndex` buckets docs into
  `Overview` (root, no `/`), `Planning Artifacts` (`planning-artifacts/`), `Implementation Artifacts`
  (`implementation-artifacts/`), then `Other`. Within a bucket it sorts by `Title` and renders each via
  `AppendIndexCard`. There is **no sub-grouping by kind and no PRD prominence** today. [Source: `src/SpecScribe/HtmlTemplater.cs:61-118`]
- **Card status is plain text, not a badge.** `AppendIndexCard` joins `Frontmatter.Status`, `Date`, `Author`
  with " · " into a single `<p>`. AC#1's "badge consistent with the site's status semantics, not plain text"
  is precisely this line. [Source: `src/SpecScribe/HtmlTemplater.cs:495-512`]
- **The planning docs and their statuses are already parsed.** The live fixture: `brief.md` (`status: draft`),
  `prd.md` (`status: final`), `review-rubric.md` (**no frontmatter**; title derived from its H1 "PRD Quality
  Review — …"; no status), `DESIGN.md` (`status: final`), `EXPERIENCE.md` (`status: final`). `.memlog.md`
  files are dotfiles and already excluded by `IsIgnored`. [Source: planning-artifacts tree; `src/SpecScribe/SiteGenerator.cs:620-627`]
- **A filename→kind classifier already exists — reuse it.** `ModuleContext.BmadMethodDocs` maps `prd.md`→PRD,
  `brief.md`→Product Brief, `DESIGN.md`→UX Design, `EXPERIENCE.md`→UX Experience (and ARCHITECTURE-SPINE→
  Architecture). `SiteNav.Build` matches these by filename anywhere in the tree. `review-rubric.md` is
  deliberately *not* in this list, which is exactly why it currently falls through to a generic card. [Source: `src/SpecScribe/ModuleContext.cs:74-81`, `src/SpecScribe/SiteNav.cs:53-69`]
- **Keeping a doc out of the card grid is a known move.** `SiteGenerator.WriteIndex` already excludes the ADR
  landing page from `_docs` "so it never doubles up as a document-grid card" — the same discipline suppresses
  the rubric card while its page still renders. [Source: `src/SpecScribe/SiteGenerator.cs:435-448`]
- **Status color semantics live in exactly one place.** `StatusStyles` maps story/epic/requirement status to
  done/active/review/ready/pending/deferred; the CSS defines those on `.status-badge`. Add the doc mapping
  here (`ForDoc`), not inline in the templater. The base `.status-badge` already renders the pending look, so
  no new CSS is required. [Source: `src/SpecScribe/StatusStyles.cs:1-67`, `src/SpecScribe/assets/specscribe.css:820-837`]

### Two Presentation Facts (avoid confusion)

- **Quick-links vs. the index card grid are two different affordances.** The dashboard's quick-link pills
  (`AppendDashboard` → `nav.QuickLinks`, PRD/UX/brief already there with a `family-planning` accent) are
  **not** what this story touches. Story 2.4 reshapes the **bottom-of-page index card grid** "Planning
  Artifacts" section. Do not restyle or duplicate the quick-links. [Source: `src/SpecScribe/HtmlTemplater.cs:128-196`, `:257-308`, `src/SpecScribe/SiteNav.cs:18-20`]
- **The doc badge is the doc's own declared status (Story 1.5 truthfulness).** It reflects each planning
  document's frontmatter `status:` — name it honestly and don't remap it into a lifecycle vocabulary it
  didn't claim, and don't reconcile it with the sprint/derived status signals. It is a third, independent,
  clearly-scoped signal (the document's self-reported state). [Source: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`]

### Scope Boundaries

- **IN (this story):** `StatusStyles.ForDoc`/`DocLabel`; render index-card status as a `status-badge`; regroup
  the home "Planning Artifacts" section (PRD prominent primary, UX Design+Experience together, Brief distinct,
  others as plain cards) driven off the existing `ModuleContext` filename classification; suppress
  `review-rubric.md` as a standalone card while keeping its page and linking it from the PRD card; full
  graceful degradation; render- and generation-level tests.
- **OUT — the dashboard** (stat row, Now & Next, sunburst, Overall Progress, quick-link pills): untouched.
- **OUT — new artifact discovery/parsing, a new page, a new nav item, a new dependency, or any JS.** Every
  doc here is already discovered, parsed, and page-generated.
- **OUT — iconography (2.5), comment annotations (2.6), sprint/derived-status changes (2.3), multi-framework
  path generalization (Epic 4), dark mode.** Classify by filename so Epic 4 can generalize later, but do not
  build the general adapter here.

### Previous Story Intelligence

- **2.3 (`ready-for-dev`) is the immediate predecessor.** It adds `StatusStyles.ForSprint`/`SprintLabel` and
  (optionally) an explicit `.status-badge.pending` CSS rule. `ForDoc` here is the exact same shape — add it
  as a sibling method in the same file. If 2.3's `.status-badge.pending` rule has landed, don't re-add it;
  if it hasn't, the base `.status-badge` already covers `pending`, so you are unblocked either way. [Source: `_bmad-output/implementation-artifacts/2-3-sprint-status-page-and-dashboard-widget.md:49-50`, `:205-208`]
- **2.2 established the "companion of a primary doc" and "labeled section, not generic Other" patterns** —
  both reused here (rubric as PRD companion; planning docs grouped by kind rather than dumped alphabetically).
  [Source: `_bmad-output/implementation-artifacts/2-2-first-class-rendering-of-spec-artifacts.md`]
- **2.1 established "distinguish *no plan yet* from *no data* so gaps read as next actions"** — apply it to the
  empty/partial planning section (missing PRD/rubric/UX degrade cleanly, never an empty labeled sub-group).
  [Source: `_bmad-output/planning-artifacts/epics.md:287-291`]
- **Story 1.1** established graceful omission of missing artifact classes; **1.4** the a11y floor (skip link,
  single `main`, focus, aria); **1.5** truthfulness (name what a signal means; don't let signals silently
  contradict). All are contracts to preserve — inherited by reusing the existing `RenderIndex`/`StatusStyles`/
  card seams.
- **Environment:** use `py -3` for BMAD helper scripts on this Windows host (`python`/`python3` are not on
  PATH — the create-story `resolve_customization.py` step failed for exactly this reason).

### Architecture Compliance

- **One status-semantics seam, one classifier, one card renderer.** Doc status → color = a new method in
  `StatusStyles`; planning-doc kinds = the existing `ModuleContext` filename map; cards = the existing
  `AppendIndexCard`/`index-*` CSS. No parallel palette, no second classifier, no forked card component. [Source: `src/SpecScribe/StatusStyles.cs`, `src/SpecScribe/ModuleContext.cs:74-81`, `src/SpecScribe/HtmlTemplater.cs:495-512`]
- **Graceful degradation is contractual (NFR2).** Missing/partial planning artifacts → omitted section or
  omitted card/link, never an exception, never an empty labeled group, never a broken link. Preserve the
  existing `if (inGroup.Count == 0) continue;` guard and the ADR-style "generate the page but don't card it"
  pattern. [Source: `src/SpecScribe/HtmlTemplater.cs:97`, `src/SpecScribe/SiteGenerator.cs:435-448`]
- **Host-neutral, static-host-safe output (NFR6, GitHub Pages).** The regrouped section, badges, and the
  PRD→rubric link are static HTML/CSS + relative hrefs at the correct depth via `PathUtil` — no host-specific
  behavior, so the Epic 6 webview inherits them. [Source: `src/SpecScribe/PathUtil.cs`]
- **Self-contained packaging.** Any styling reuses the embedded `specscribe.css` (`index-card`/`index-grid`/
  `status-badge`) with at most a small `index-card--primary`/sub-label rule; no loose assets, no new deps. [Source: `src/SpecScribe/SiteGenerator.cs:500-509`, `src/SpecScribe/assets/specscribe.css:464-534`]

## Technical Requirements

- Add `StatusStyles.ForDoc(string?)` + `DocLabel(string?)` mapping planning-doc frontmatter `status:`
  (`final/draft/ready/in-progress/review/…`, plus empty→pending) onto the existing done/active/review/ready/
  drafted/pending classes — no new palette; label = the human-cased source word.
- Render the index-card status as `<span class="status-badge {ForDoc}">{DocLabel}</span>` (recommended: for
  all index cards; at minimum the planning section). No existing test asserts the old plain-text status.
- Regroup the home "Planning Artifacts" section by the existing `ModuleContext` filename classification: PRD
  as a prominent primary card first, UX Design + UX Experience grouped together, Product Brief distinct,
  unrecognized planning docs as ordinary cards. Match by filename, not hard-coded nested paths.
- Detect `review-rubric.md` as the PRD's quality-review companion; suppress its standalone card, keep
  generating its page, and add a link to it from the PRD card. Resolve-or-omit the link (never broken).
- Preserve Story 1.4 accessibility (single `main`, skip link, focusable `<a>`s) and Story 1.5 truthfulness
  (badge is the doc's own status, not a reconciled/derived number). No new JS; static-host-safe.
- Full graceful degradation: missing PRD / missing rubric / missing UX / no planning docs each degrade
  cleanly with no empty groups and no broken links.

## File Structure Requirements

Primary UPDATE candidates:

- `src/SpecScribe/StatusStyles.cs` — add `ForDoc`/`DocLabel`.
- `src/SpecScribe/HtmlTemplater.cs` — badge in `AppendIndexCard`; regroup the Planning Artifacts section
  (PRD-prominent, UX-paired, Brief-distinct) in `RenderIndex`; suppress the rubric card + add the PRD→rubric
  link. Thread the module/classification in from the caller as needed.
- `src/SpecScribe/SiteGenerator.cs` — only if the rubric card-suppression is best done at the `_docs`/
  `WriteIndex` layer (mirroring the ADR-landing exclusion) rather than inside the templater.
- `src/SpecScribe/assets/specscribe.css` — at most a small `index-card--primary` / UX sub-label rule; prefer
  reusing existing `index-*`/`status-badge` classes. Do NOT add `.status-badge.pending` (base rule covers it;
  2.3 owns any explicit rule).

Primary TEST updates:

- `tests/SpecScribe.Tests/StatusStylesTests.cs` — `ForDoc`/`DocLabel` table.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — planning section: badges with mapped classes, PRD
  prominent, UX paired, rubric not carded, PRD→rubric link; single `main` + skip link intact; graceful cases.
- A `SiteGenerator`-level test (extend `SiteGeneratorFidelityTests` or a sibling) — rubric page still written,
  rubric not carded, PRD card links to it, no broken links; `.memlog.md` stays excluded.

## Library and Framework Requirements

- Stay on the existing .NET / Markdig / YamlDotNet / inline-SVG / CSS stack. **No new runtime dependencies, no
  new JS.** Everything reuses `StatusStyles`, `ModuleContext`, `HtmlTemplater`'s card renderer, `PathUtil`
  relative-href helpers, and the embedded CSS. The planning docs are already parsed with frontmatter statuses
  available on `DocModel.Frontmatter`.

## Testing Requirements

- Preserve existing coverage and **Story 1.4's accessibility assertions** and **Story 1.5's truthfulness
  assertions** — none may regress. The home index must keep exactly one `<main id="main-content">` and the
  skip link, and must not gain a contradictory status number.
- Add coverage (see Task 5): `ForDoc`/`DocLabel` mapping; planning-section render (badges with mapped classes,
  PRD prominence, UX pairing, rubric suppression, PRD→rubric link); graceful degradation (no PRD / no rubric /
  no planning docs); generation-level (rubric page written but not carded, no broken links, dotfiles excluded).
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~StatusStyles|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification on `docs/live/index.html` (PRD prominent + Final badge + quality-review link; UX pair;
  Brief distinct + Draft badge; no standalone rubric card) and that `review-rubric.html` still exists and the
  link resolves. Rename `review-rubric.md` and re-generate to confirm the PRD card drops the link cleanly.

## UX and Accessibility Requirements

- The regrouped section reads on-brand: reuse the antiquarian `index-card`/`index-grid`/`index-section-title`
  treatment (parchment/rust/teal/gold); the PRD's prominence is a compositional emphasis (lead position,
  slightly larger/labeled), not a foreign component. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]
- Every status is a **text badge**, never color-only (UX-DR17) — the badge carries the status word and color
  is redundant reinforcement. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR17]
- Preserve Story 1.4 accessibility: the home page keeps its one `<main id="main-content">` and skip link; the
  PRD→rubric affordance is a real focusable `<a>`; the primary-card emphasis must not rely on color alone
  (position/label carry it too). [Source: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`, `src/SpecScribe/HtmlTemplater.cs:77-79`]
- The section is static (no motion), so `prefers-reduced-motion` is trivially satisfied. Keep the doc-status
  badge clearly the *document's own* status so it doesn't read as contradicting the sprint or derived signals
  (Story 1.5). [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR18]

## Reinvention and Regression Guardrails

- Do NOT invent a new status color vocabulary — map doc status onto the existing done/active/review/ready/
  pending classes in `StatusStyles.ForDoc`. Do NOT add `.status-badge.pending` (base rule covers it; 2.3
  owns any explicit rule).
- Do NOT write a second planning-doc classifier — reuse `ModuleContext`'s filename map. Match by filename,
  never by hard-coded nested folder paths.
- Do NOT stop generating `review-rubric.html` — only stop *carding* it. Never emit a PRD→rubric link to a
  page that wasn't generated.
- Do NOT touch the dashboard (stat row, Now & Next, sunburst, Overall Progress, quick-link pills) or the nav.
- Do NOT render an empty labeled sub-group (e.g. a "UX" heading with nothing under it) or a "PRD" slot when
  no PRD exists.
- Do NOT regress Story 1.4 accessibility (skip link, single `main`, focus, aria), Story 1.5 truthfulness, or
  Story 1.1's missing-section omission. Keep all links/anchors static-host-safe (GitHub Pages), relative, at
  correct depth via `PathUtil`.

## Git Intelligence Summary

- Baseline `02c06c8` (main, "Addressed starburst concerns"). Recent commits (1.5 dev + code review) reworked
  `HtmlTemplater.AppendDashboard`, `Charts`, and `StatusStyles`/CSS — read `HtmlTemplater.RenderIndex`/
  `AppendIndexCard` and `StatusStyles` before editing so the badge and regroup compose with, not against, the
  established card and status-color patterns. [Source: `src/SpecScribe/HtmlTemplater.cs:61-126`, `:495-512`]
- Shared seams (`HtmlTemplater`/`StatusStyles`/`ModuleContext`/`SiteGenerator`) are the single-source points —
  change them additively and centrally, the same pattern 1.2–2.3 followed.
- Generated output publishes to GitHub Pages — keep every href/anchor static-host-safe (relative, correct
  depth via `PathUtil.RelativePrefix`).

## Latest Technical Information

- No framework/library version decisions are introduced by this story; it stays entirely within the existing
  .NET + Markdig + YamlDotNet + inline-SVG + CSS stack. Frontmatter statuses are already deserialized onto
  `DocModel.Frontmatter.Status`, so the badge needs no new parsing. Relative `<a href>` navigation and static
  badge/card markup are universally supported — no polyfills, no new capability.

## Project Context Reference

- Epic + story source: `_bmad-output/planning-artifacts/epics.md` (Epic 2, Story 2.4; FR2/FR5/FR7; NFR2; UX-DR17/DR18)
- Planning artifacts (live fixture): `_bmad-output/planning-artifacts/briefs/.../brief.md` (`draft`),
  `prds/.../prd.md` (`final`), `prds/.../review-rubric.md` (no frontmatter), `ux-designs/.../DESIGN.md`
  (`final`), `ux-designs/.../EXPERIENCE.md` (`final`)
- Closest structural precedents: `src/SpecScribe/HtmlTemplater.cs` (`RenderIndex`/`AppendIndexCard` card grid),
  `src/SpecScribe/ModuleContext.cs` (filename→kind classification), `src/SpecScribe/StatusStyles.cs`
  (`ForStory`/`ForEpic`/`ForRequirement` — add `ForDoc`), `src/SpecScribe/SiteGenerator.cs:435-448`
  (ADR-landing card exclusion)
- Status semantics (single source): `src/SpecScribe/StatusStyles.cs`; CSS badges: `src/SpecScribe/assets/specscribe.css:820-837`
- Predecessors: `_bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md`, `2-2-first-class-rendering-of-spec-artifacts.md`, `2-3-sprint-status-page-and-dashboard-widget.md`
- Accessibility baseline: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Truthfulness baseline: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`
- Successors (do NOT do here): 2.5 iconography, 2.6 comment annotations
- Key source seams: `src/SpecScribe/HtmlTemplater.cs`, `StatusStyles.cs`, `ModuleContext.cs`, `SiteGenerator.cs`, `SiteNav.cs`, `PathUtil.cs`, `DocModel.cs`, `Frontmatter.cs`, `assets/specscribe.css`
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Memory: [[specscribe-status-token-system]], [[story-1-4-a11y-seams-for-1-5]], [[charting-is-pure-svg-no-js]]

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed — comprehensive developer guide created for
  Epic 2's planning-grouping story: a home-index presentation change that adds `StatusStyles.ForDoc`/
  `DocLabel` (mapping planning-doc frontmatter status onto the existing status colors), renders index-card
  status as an on-brand `status-badge`, regroups the home "Planning Artifacts" section (PRD prominent primary,
  UX Design+Experience together, Product Brief distinct) driven off the existing `ModuleContext` filename
  classification, and folds the `review-rubric.md` quality review under the PRD (suppressed as a standalone
  card, still page-generated, linked from the PRD card) — all with full graceful degradation, Story 1.4
  accessibility and Story 1.5 truthfulness preserved, no new parser/page/nav/dependency/JS.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- Confirmed the home index card grid groups by folder prefix only (`Overview`/`Planning Artifacts`/
  `Implementation Artifacts`/`Other`) and renders status as plain " · "-joined text in `AppendIndexCard` — the
  exact line AC#1 ("badge … not plain text") targets. [`src/SpecScribe/HtmlTemplater.cs:61-118`, `:495-512`]
- Confirmed `ModuleContext.BmadMethodDocs` already classifies the exact planning filenames (prd/brief/DESIGN/
  EXPERIENCE) and that `review-rubric.md` is intentionally absent from it (hence its generic card today).
  [`src/SpecScribe/ModuleContext.cs:74-81`]
- Confirmed `StatusStyles` has `ForStory`/`ForEpic`/`ForRequirement` but no `ForDoc`; the base `.status-badge`
  CSS is already the pending look, so no new CSS is required and `.status-badge.pending` (2.3's) must not be
  duplicated. [`src/SpecScribe/StatusStyles.cs:1-67`, `src/SpecScribe/assets/specscribe.css:820-837`]
- Confirmed the "generate the page but keep it out of the card grid" precedent (ADR landing page excluded from
  `_docs`) — the pattern for suppressing the rubric card while keeping `review-rubric.html`. [`src/SpecScribe/SiteGenerator.cs:435-448`]
- Confirmed `.memlog.md` files are dotfiles and already excluded by `IsIgnored`. [`src/SpecScribe/SiteGenerator.cs:620-627`]
- Confirmed no existing test asserts the old plain-text card status, so switching to a badge is low-regression.
- Environment: `python`/`python3` absent on PATH (create-story `resolve_customization.py` step failed); use
  `py -3` for BMAD helper scripts on this Windows host.
- Planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~StatusStyles|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Task 1 (`StatusStyles.ForDoc`/`DocLabel`) → Task 2 (badge in `AppendIndexCard`) → Task 3 (regroup Planning
  Artifacts: PRD-prominent, UX-paired, Brief-distinct via `ModuleContext` filename classification) → Task 4
  (suppress rubric card, keep its page, link it from the PRD card) → Task 5 (tests) → Task 6 (real generation
  pass).
- Keep every change in the shared seams; prefer render-/generation-level string assertions over new public
  API; keep everything host-neutral and static-host-safe; keep the doc status badge clearly the document's own
  self-reported status, distinct from sprint/derived signals.

### Completion Notes List

- Fourth story of Epic 2. A home-index presentation change over already-represented planning artifacts: PRD
  prominence, meaningful grouping (UX paired, brief distinct), status-as-badge, and folding the PRD quality
  review under the PRD (suppressed card, still-generated page, linked from the PRD card).
- Explicitly kept out: dashboard (stat row / Now & Next / sunburst / quick-links), nav, new discovery/parsing/
  page/dependency/JS, iconography (2.5), comment annotations (2.6), sprint/derived-status changes (2.3),
  multi-framework path generalization (Epic 4), dark mode.
- Coordination flags: reuse `StatusStyles` (add `ForDoc`, no new palette, no duplicate `.status-badge.pending`);
  reuse `ModuleContext` filename classification (no second classifier, match by filename); resolve-or-omit the
  PRD→rubric link (never broken); keep the doc badge truthful to the doc's own status (Story 1.5).

**Implementation outcome (2026-07-07):**

- **Task 1** — Added `StatusStyles.ForDoc`/`DocLabel` as siblings to `ForStory`/`ForSprint`, mapping the doc's
  own frontmatter word (`final`/`draft`/`ready`/`in-progress`/`review`/…) onto the existing done/review/active/
  ready/drafted/pending classes; empty/unknown → `pending`. Label is the source word title-cased (truthful, not
  remapped). No new CSS — Story 2.3's `.status-badge.pending` and the base rule already cover it.
- **Task 2** — `AppendIndexCard` now emits the status as a `<span class="status-badge {ForDoc}">{DocLabel}</span>`
  (all index cards, one code path) with date·author kept as the de-emphasized `<p>`; status removed from that
  middot run. No existing test asserted the old plain-text status, so zero regression.
- **Task 3** — New `AppendPlanningSection` regroups the "Planning Artifacts" band: PRD as a full-width
  `index-card--primary` (with a "Primary document" kicker), UX Design + UX Experience under a shared "UX"
  sub-label, Product Brief distinct, unrecognized docs as ordinary cards. Classification is by well-known
  filename via the new `ModuleContext.WellKnownDocs` constants (same source of truth as the nav — no second
  classifier), matched filename-anywhere. Unknowns and empty slots degrade cleanly (no empty labeled groups).
- **Task 4** — `review-rubric.md` is detected as the PRD companion; when a PRD exists it is suppressed as a
  standalone card and reached via a "Quality review →" `index-card-branch` link on the PRD card. Its page is
  still generated by the normal `*.md` pipeline (link resolves). No PRD → rubric degrades to an ordinary card;
  PRD but no rubric → no dangling link.
- **Tasks 5–6** — Added `ForDoc`/`DocLabel` tables, five `RenderIndex` planning-section render tests (badges,
  PRD prominence, UX pairing, rubric fold, and three graceful-degradation cases), and a new generation-level
  fixture (`PlanningArtifactsGenerationTests`) proving the rubric page is written but not carded, the PRD links
  it, and removing the rubric drops the link cleanly. Full suite green (367 tests). Real generation pass over
  the repo verified on `docs/live/index.html`: PRD prominent + "Final" badge + quality-review link; UX paired;
  Brief distinct + "Draft" badge; rubric page present, referenced exactly once (the branch link), never carded.
  Browser DOM inspection confirmed the on-brand rendering (teal primary accent, green "Final" badge).
- Story 1.4 a11y (single `<main>`, skip link, real focusable `<a>`s) and Story 1.5 truthfulness preserved; no
  new parser/page/nav/dependency/JS; dashboard untouched.

### File List

- _bmad-output/implementation-artifacts/2-4-planning-artifacts-grouping-status-badges-and-prd-prominence.md (story: tasks/records/status)
- _bmad-output/implementation-artifacts/sprint-status.yaml (status: ready-for-dev → in-progress → review)
- src/SpecScribe/StatusStyles.cs (add `ForDoc`/`DocLabel` — doc-status → shared badge vocabulary)
- src/SpecScribe/ModuleContext.cs (add `WellKnownDocs` filename constants; `BmadMethodDocs` references them)
- src/SpecScribe/HtmlTemplater.cs (badge in `AppendIndexCard`; `AppendPlanningSection`/`AppendPrimaryPrdCard`/`FindByFileName`; badge+meta helpers)
- src/SpecScribe/assets/specscribe.css (small `index-card--primary`/`index-card-kicker`/`index-card-branch`/`index-subgroup-label` + card badge spacing)
- tests/SpecScribe.Tests/StatusStylesTests.cs (`ForDoc`/`DocLabel` tables)
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs (planning-section render + graceful cases)
- tests/SpecScribe.Tests/PlanningArtifactsGenerationTests.cs (new — generation-level rubric fold + degradation)
- .claude/launch.json (dev-only static preview config for docs/live)

## Change Log

- 2026-07-07: Implemented Story 2.4. `StatusStyles.ForDoc`/`DocLabel` map a planning doc's own frontmatter
  status onto the shared badge vocabulary; index cards render status as an on-brand `status-badge` (not plain
  text); the home "Planning Artifacts" band is regrouped (PRD prominent full-width primary, UX Design + UX
  Experience paired under a "UX" sub-label, Product Brief distinct) via new `ModuleContext.WellKnownDocs`
  filename constants; and `review-rubric.md` is folded under the PRD (suppressed as a card, page still
  generated, linked as "Quality review →"). Full graceful degradation; Story 1.4 a11y and 1.5 truthfulness
  preserved; no new parser/page/nav/dependency/JS. 367 tests pass; verified on a real generation pass. Status →
  review.
- 2026-07-06: Created Story 2.4 as Epic 2's planning-grouping/PRD-prominence story. Scoped: a home-index card-
  grid presentation change that adds `StatusStyles.ForDoc`/`DocLabel` (planning-doc frontmatter status →
  existing status colors), renders index-card status as an on-brand `status-badge` instead of plain text,
  regroups the home "Planning Artifacts" section (PRD prominent primary, UX Design + UX Experience together,
  Product Brief distinct) driven off the existing `ModuleContext` filename classification, and folds
  `review-rubric.md` under the PRD (suppressed as a standalone card, page still generated, linked from the PRD
  card). Full graceful degradation for missing PRD/rubric/UX/planning docs; Story 1.4 accessibility and Story
  1.5 truthfulness preserved; no new parser, page, nav item, dependency, or JS. Baseline `02c06c8`.
