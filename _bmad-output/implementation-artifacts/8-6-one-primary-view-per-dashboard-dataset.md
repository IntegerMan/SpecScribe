# Story 8.6: One Primary View per Dashboard Dataset

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer doing a 30-second scan,
I want each dataset shown one primary way with alternates demoted behind a toggle,
so that I never reconcile multiple renderings of the same data.

## Acceptance Criteria

1.
**Given** the home dashboard currently renders requirements multiple ways (today: the status-block grid **and** the requirements-flow Sankey, stacked in one panel)
**When** the page is generated
**Then** the coverage-flow Sankey is the single primary (default-visible) representation, with the status-block grid demoted behind a toggle
**And** the sprint page's By Status / By Epic radio-toggle is the reused pattern. [Source: epics.md#Story 8.6; Epic3UXFeedback.md#T2]

2.
**Given** the status-block grid is the requirements-flow Sankey's accessibility text-twin (Story 3.7's contract)
**When** the views are consolidated behind the toggle
**Then** the text-twin grid is never removed — it stays in the DOM (both views live in the DOM, the way the sprint board's two views do)
**And** duplicated work-count displays on the epics-index page (the header subtitle restating the epic/story counts the stat-grid tiles show immediately below) are consolidated to one authoritative display. [Source: epics.md#Story 8.6; Epic3UXFeedback.md#T2; 3-7-requirements-flow-and-status-blocks.md]

---

## Developer Context

**This is a presentation-only consolidation story.** No new page, no data-model or count change, no parser touch, no view-model field. It removes the "same data rendered several ways on one screen, forcing the reader to verify the views agree" failure mode (the T2 finding, graded 🔴 against the daily-pulse journey) on **two surfaces**:

1. **AC #1 — the home dashboard's Requirements panel.** Today [`AppendRequirementsPanel`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:199) stacks **two** renderings of the same requirements dataset in one `.req-panel`: the [`RequirementStatusGrid`](../../src/SpecScribe/Charts.cs:1146) (the status-block "badge list") **then** the [`RequirementFlow`](../../src/SpecScribe/Charts.cs:1210) Sankey (definition → epic-coverage → implementation-state — the "coverage matrix" / "per-epic breakdown" the UX review counted as the 2nd and 3rd renderings). This story wraps them in a **panel-scoped clone of the sprint page's pure-CSS radio-toggle**, so the **flow is the default-visible primary** and the **grid is the demoted alternate** — one view at a time, **both kept in the DOM**.
2. **AC #2 — the epics-index page's duplicated counts.** Today [`RenderEpicsIndexBody`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:20) prints a header subtitle (`{N} epics · {M} with stories drafted`) that restates the **exact same** epic/drafted figures the stat-grid tiles ([`AppendEpicsProgressPanel`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:72): "Epics drafted M/N", "Stories defined X") show inches below. This story **consolidates that literal duplication to one authoritative display** — keep the stat-grid tiles (the richer, tooltip'd, Story-8.2 count home) and trim the redundant count restatement out of the subtitle.

Both are direct fixes for the live UX-review finding graded against the daily-pulse / shared-portal journeys:

> Home shows requirements three ways (inline badge list, coverage matrix, per-epic breakdown) … Story counts appear in the epics header and again per epic card … Each view is individually defensible, but stacked on one page they force the reader to verify the views agree. **Recommendation:** per page, pick one primary representation and demote alternates behind a toggle — the sprint page's existing By Status / By Epic radio toggle is the right pattern to copy — never cut a chart's text-twin table. [Source: [Epic3UXFeedback.md#T2](../../docs/Epic3UXFeedback.md); [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)]

It sits alongside its Epic 8 siblings but touches a **different seam** than any of them: **8.1** owns status words/colors (`StatusStyles`); **8.2** owns count *correctness* / single-source (`ProjectCounts`); **8.3** owns progress+state pairing; **8.4** owns the Next Steps command surface (`BmadCommands`); **8.5** owns the two empty-state surfaces (`EpicsViewBuilder` banner + `SprintTemplater` empty lane). **8.6 owns view *consolidation*** on two rendered surfaces: the home Requirements panel (`HtmlRenderAdapter.Dashboard.cs`) and the epics-index header subtitle (`HtmlRenderAdapter.Epics.cs`) — plus the toggle CSS. **No file overlap with 8.1/8.2/8.4/8.5.** A light adjacency with 8.3 (both may touch `HtmlRenderAdapter.Epics.cs`) — non-overlapping hunks (8.3 = card/lane headers + tooltips; 8.6 = the epics-index header subtitle only).

### Owner-selected design decisions (visual intent elicited at create-story — do not re-litigate)

**1. Mechanism → "Reuse the sprint radio-toggle" (owner pick).** Demote the alternate with a **panel-scoped clone of the sprint page's pure-CSS `board-tabs` / `board-view` `:has()` toggle** — one view visible at a time, **both in the DOM**. Chosen over "keep one, link out for the rest": the toggle keeps the flow's text-twin grid **on the home page** (satisfying AC #2 + Story 3.7), whereas link-out would move the twin off-page; and AC #1 literally names this pattern. [Owner decision, this story; memory: [[create-story-elicit-visual-intent]]]

**2. Primary view → "Requirements-flow Sankey is the default; status-block grid is the toggled alternate" (owner pick).** The definition→epic-coverage→state flow is the "coverage matrix" AC #1 calls the single primary, so its tab is `checked` and it renders first/visible; the compact status-block grid becomes the toggled-in alternate (and stays in the DOM as the flow's text-twin). [Owner decision, this story]

**3. AC #2 count dedup → "Epics-index header subtitle vs. stat-grid" (owner pick).** The concrete duplication to fix is the epics-index page: the header subtitle restates the epic/drafted counts the stat-grid tiles show directly below. Keep the stat tiles; trim the subtitle's count restatement. Count *correctness* across pages (the historical 38-vs-39 clash) stays **Story 8.2's** job — this story only removes a redundant *display*, it does not touch any count source. [Owner decision, this story]

### The rendering model (read carefully — this is where the change lives)

**AC #1 — the toggle is a straight structural clone of `SprintTemplater`'s.** The sprint page's proven pattern ([`RenderBoardTabs`](../../src/SpecScribe/SprintTemplater.cs:183) + [`AppendBoardViews`](../../src/SpecScribe/SprintTemplater.cs:196) + the CSS at [specscribe.css:3093-3120](../../src/SpecScribe/assets/specscribe.css:3093)) is: hidden radios + visible `<label>` tabs drive which `.board-view` shows, via `:has()` on the panel container. It is **pure CSS, no JS** (memory: [[charting-is-pure-svg-no-js]]). Clone it into the Requirements panel with **panel-unique ids/name** so it can't collide with the sprint radios:

- Radios: `<input type="radio" id="rv-flow" name="req-view" class="board-tab-radio" checked>` and `<input ... id="rv-grid" name="req-view" class="board-tab-radio">`.
- Tabbar: reuse the generic `.board-tabbar` / `.board-tab` chrome — `<label for="rv-flow" class="board-tab">Flow</label>` and `<label for="rv-grid" class="board-tab">Status grid</label>`.
- Views: wrap the **flow** in `<div class="req-view req-view-flow">…</div>` (default visible) and the **grid** in `<div class="req-view req-view-grid">…</div>`.
- The `:has()` switch is scoped to **`.req-panel`** (the existing panel container), not `.sprint-page`.

**The toggle only appears when BOTH views exist.** The flow requires an epics model (`RequirementFlow(requirements, epicsModel)` is gated on `epicsModel is not null` today). So:

- `epicsModel is not null` → render the header row with the toggle tabs, then the two `.req-view` wrappers (flow default-visible, grid alternate).
- `epicsModel is null` → there is no flow, so render the **grid alone, with no toggle** (a single view needs no toggle) — keep this branch byte-stable.

**Why the flow renders first now (order flips):** today the panel emits grid **then** flow; after this story the primary (flow) renders first inside `.req-view-flow`, the alternate (grid) second inside `.req-view-grid`. That reordering is expected and is part of the golden-fingerprint diff — confirm the diff is *only* the toggle wrappers + the reorder, nothing else.

**AC #2 — the text-twin guardrail is satisfied by the toggle, not by a separate change.** The status-block grid **is** `RequirementFlow`'s declared accessibility text equivalent (Story 3.7 AC #3: "the status-tile grid + requirement cards are the text equivalent … never diagram-only" — see the `RequirementFlow` docstring at [Charts.cs:1208-1209](../../src/SpecScribe/Charts.cs:1208)). Because the toggle keeps the grid **in the DOM** (only `display:none` when the flow tab is active, exactly like the sprint board's hidden `.board-view-epic`), the twin is **never removed** — AC #2's first clause is met by construction. Two reinforcements to preserve:

- The flow keeps its own whole-diagram `role="img"` name + per-node/ribbon `<title>`s (its self-sufficient a11y for the default view — do not strip them).
- The grid stays reachable: its tab is a real keyboard-focusable radio, so an AT user can switch to the text-twin; and the panel's `View Requirements →` link still points at `requirements.html`, where the full always-visible twin (grid + requirement cards) lives.

**AC #2 — the epics-index count dedup.** In [`RenderEpicsIndexBody`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:23) the subtitle is:

> `{view.EpicCount} epics · {view.DraftedCount} with stories drafted`

`EpicCount == progress.EpicsTotal` and `DraftedCount == progress.EpicsDrafted` — the **same two integers** the first stat tile ("Epics drafted M/N") shows in `AppendEpicsProgressPanel` directly below, with "Stories defined X" beside it. Consolidate by **trimming the count restatement out of the subtitle**, keeping the stat-grid tiles as the single count display. Recommended reduced subtitle: `{view.SiteTitle}` alone (the `<h1>` already reads "Epics & Stories"; the stat grid two lines down carries the numbers). No information is lost — the counts remain in the stat grid inches away; only the duplicate *display* is removed. The per-epic "Progress by Epic" mosaic tallies ("X / Y stories detailed") are a legitimate **breakdown**, not a duplicate — leave them untouched.

### Scope boundaries (read carefully)

- **Do NOT change any count, count source, status word, or status color.** AC #2 removes a redundant *display* of counts that already agree by construction (both read the same `progress`/view fields); it does not reclassify, recompute, or reconcile any number. Count correctness/single-source is 8.2's seam. [memory: [[specscribe-status-token-system]]]
- **Do NOT delete, gut, or bypass `RequirementStatusGrid` or the grid's markup.** It is the flow's text-twin (Story 3.7) — it moves into the `.req-view-grid` wrapper, still rendered, still in the DOM. Removing it (or replacing it with a link-out) is an accessibility regression and violates AC #2.
- **Do NOT add a client-side script or NuGet package.** The toggle is pure CSS `:has()` + hidden radios, exactly like the sprint board. [memory: [[charting-is-pure-svg-no-js]]]
- **Do NOT reuse the sprint radios' ids/name** (`sprint-view` / `sv-status` / `sv-epic`) or the `.board-view-epic { display:none }` rule — those are board-specific. Use `req-view` / `rv-flow` / `rv-grid` and new `.req-view-*` classes so the two toggles never interfere (a home page has no sprint page markup, but keeping them distinct is the correct, collision-proof design).
- **Do NOT touch other pages' multi-render surfaces.** The Deep Analytics coupling triple-render (graph + ranked-pairs table + prose) named in T2 is a *different page* and is **out of scope** here — AC #1 is scoped to the home dashboard requirements; AC #2's text-twin clause is a **guardrail** on *this* consolidation, not a mandate to consolidate Deep Analytics.
- **Do NOT change the section-fact contract or any view model.** The requirements panel renders directly from `view.Requirements` + `view.Epics` (already on `DashboardView`); the toggle is presentational wrapper markup and adds **no** tracked section fact. No new `DashboardView`/`EpicsIndexView` field. [memory: [[story-6-2-section-view-models-live]]]
- **Do NOT touch `RenderBoard` / the sprint page.** The sprint toggle is the *reference*, not a target; leave `SprintTemplater` untouched.
- **Do NOT write back to any source.** Local-first, read-only invariant.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Wrap the home Requirements panel in a panel-scoped radio-toggle.** In [`AppendRequirementsPanel`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:199): keep `<div class="chart-panel req-panel">` and the `chart-panel-header-row` with `<h3>Requirements</h3>` + the `View Requirements →` link. When `epicsModel is not null`, add the `board-tabs` clone (radios `rv-flow` checked / `rv-grid`, name `req-view`, labels "Flow" / "Status grid") inside the header row, then emit `<div class="req-view req-view-flow">` wrapping `Charts.RequirementFlow(requirements, epicsModel)` and `<div class="req-view req-view-grid">` wrapping `Charts.RequirementStatusGrid(requirements.All.ToList(), prefix: string.Empty)`. When `epicsModel is null`, emit the grid alone with **no** toggle and **no** `.req-view` wrappers (byte-stable single-view branch).
- **Mirror `RenderBoardTabs`' markup shape exactly** so the reused CSS chrome applies: `<div class="board-tabs"><input id="rv-flow" …><input id="rv-grid" …><div class="board-tabbar"><label for="rv-flow" class="board-tab">Flow</label><label for="rv-grid" class="board-tab">Status grid</label></div></div>`. (Radios precede `.board-tabbar` so the `~` sibling active-tab selectors resolve.)
- **Add the panel-scoped toggle CSS**, next to the sprint board-tabs block so they read as one system. In [`specscribe.css`](../../src/SpecScribe/assets/specscribe.css:3093): (a) active-tab styling `#rv-flow:checked ~ .board-tabbar label[for="rv-flow"], #rv-grid:checked ~ .board-tabbar label[for="rv-grid"] { … }` + the `:focus-visible` ring, mirroring the `#sv-*` rules; (b) the view switch — `.req-view-grid { display: none; }` (flow is default), `.req-panel:has(#rv-grid:checked) .req-view-flow { display: none; }`, `.req-panel:has(#rv-grid:checked) .req-view-grid { display: block; }`. Reuse the existing generic `.board-tabs/.board-tabbar/.board-tab/.board-tab-radio` chrome classes as-is (presentation-only). No literal hex — reuse existing tokens. [memory: [[specscribe-status-token-system]]]
- **Trim the epics-index subtitle's count restatement.** In [`RenderEpicsIndexBody`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:23), change the subtitle from `{EpicCount} epics · {DraftedCount} with stories drafted` to `{SiteTitle}` alone (the stat grid below is the single count home). Leave `AppendEpicsProgressPanel`'s stat tiles and the "Progress by Epic" mosaic untouched.
- **Assert the new CSS ships.** `StylesheetTests` asserts on embedded-stylesheet content — add a companion assertion for the `.req-view` toggle rules (e.g. `.req-panel:has(#rv-grid:checked)` and `.req-view-grid`).

### DON'T

- **DON'T remove the status-block grid, replace it with a link, or hide it outside the DOM** — it is the flow's Story-3.7 text-twin; it must remain rendered in the `.req-view-grid` wrapper.
- **DON'T introduce JS or a NuGet package** — the toggle is pure CSS `:has()`, like the sprint board.
- **DON'T reuse the sprint toggle's ids/name/classes** for the requirements toggle — use `req-view`/`rv-flow`/`rv-grid`/`.req-view-*`.
- **DON'T change any count, count source, status token, or view model** — AC #2 removes a duplicate display only; correctness is 8.2's.
- **DON'T touch other multi-render surfaces** (Deep Analytics coupling, the sprint page) — out of scope.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]:

- **Truthfulness over convenience** — consolidation removes *redundant renderings*, not *information*: the flow's text-twin grid stays in the DOM and one tab-click away; the epics-index counts stay in the stat grid. Nothing the reader could learn before is now unavailable — they just no longer have to cross-check three copies of it.
- **Accessibility is part of the rendering contract (NFR6, UX-DR17; Story 3.7 AC #3)** — the flow keeps its `role="img"` name + `<title>`s; the text-twin grid is never removed (in the DOM, reachable via a focusable radio); the toggle tabs are keyboard-operable `<label for>`/radio pairs with a focus ring, cloned from the already-accessible sprint pattern. Meaning is carried in text and structure, never color/visibility alone.
- **Single source of truth** — the requirements panel renders from the one `view.Requirements` + `view.Epics` input (no parallel model); the epics-index counts stay sourced from the one `progress`/view fields (this story removes a second *display*, not a second *source*). The 38-vs-39 correctness class of clash is 8.2's; this story can neither cause nor fix it.
- **Deterministic, generation-time-only output** — the toggle is static markup + CSS; a from-scratch regen of identical inputs is byte-identical. No per-visitor/interaction state persists.
- **Seed, not invariant** — no Core/Adapters package split, no view-model change; changes stay in `HtmlRenderAdapter.Dashboard.cs` + `HtmlRenderAdapter.Epics.cs` + `specscribe.css` (+ tests). The Story 6.1/6.2 delivery seam is untouched: presentational wrapper markup adds no `SemanticFacts`/`SectionFacts`, so `RenderParity` stays green. [memory: [[story-6-1-delivery-seam-live]]; [[story-6-2-section-view-models-live]]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: [SpecScribe.Tests.csproj](../../tests/SpecScribe.Tests/SpecScribe.Tests.csproj)]
- **Reuse, don't reinvent (all already in-repo):**
  - The **sprint pure-CSS view-toggle pattern** — [`SprintTemplater.RenderBoardTabs`](../../src/SpecScribe/SprintTemplater.cs:183) + [`AppendBoardViews`](../../src/SpecScribe/SprintTemplater.cs:196) + [specscribe.css:3093-3120](../../src/SpecScribe/assets/specscribe.css:3093). Clone its shape with `req-view` ids; reuse the `.board-tabs/.board-tabbar/.board-tab/.board-tab-radio` chrome classes verbatim so the tabs look identical to the sprint page (the "reused pattern" AC #1 asks for). [memory: [[now-and-next-is-the-sprint-board]]]
  - [`Charts.RequirementFlow`](../../src/SpecScribe/Charts.cs:1210) + [`Charts.RequirementStatusGrid`](../../src/SpecScribe/Charts.cs:1146) — call them unchanged; this story only re-homes their call sites into the toggle wrappers.
  - [`PathUtil.Html`](../../src/SpecScribe/PathUtil.cs) — escape any interpolated text (the labels here are literals; the counts you're removing were the only interpolation on the subtitle).
- **No external libraries or APIs** — pure in-repo C# string-building + CSS — so there is no version/security research to fold in.

---

## File Structure Requirements

**No new production classes, no new view-model fields.** Two adapter edits (a toggle wrapper + a subtitle trim), one CSS block.

**Modified files (read fully before editing):**

- [`src/SpecScribe/HtmlRenderAdapter.Dashboard.cs`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:199) — `AppendRequirementsPanel`: wrap the flow + grid in the `board-tabs` toggle (flow default, grid alternate) when `epicsModel is not null`; grid-only, no toggle, when null.
- [`src/SpecScribe/HtmlRenderAdapter.Epics.cs`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:23) — `RenderEpicsIndexBody`: trim the count restatement out of the header subtitle.
- [`src/SpecScribe/assets/specscribe.css`](../../src/SpecScribe/assets/specscribe.css:3093) — add the `#rv-flow`/`#rv-grid` active-tab + `:focus-visible` rules and the `.req-panel:has(#rv-grid:checked)` view-switch rules, next to the sprint `.board-tabs` block. Tokens only, no hex. **`StylesheetTests` asserts stylesheet content — add a companion assertion.**

**Tests to update / add:**

- [`tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs) — the core AC coverage against `RenderDashboardBody` / `RenderEpicsIndexBody` (same style as `RenderSectionParityTests`' `Dashboard(...)` helper). Add: (AC #1) a `DashboardView` with requirements **and** epics renders `<div class="req-view req-view-flow">` containing the flow SVG (`role="img"`) and `<div class="req-view req-view-grid">` containing `req-status-grid`, plus the `board-tabs` with `id="rv-flow" … checked` and `id="rv-grid"`; (AC #2 twin guardrail) the `req-status-grid` is still present (not removed); (AC #1 single-view) a `DashboardView` with requirements but **no** epics renders the grid with **no** `board-tabs`/`req-view` wrappers; (AC #2 dedup) `RenderEpicsIndexBody` subtitle no longer contains "epics ·"/"with stories drafted" while the stat grid still shows "Epics drafted" and "Stories defined".
- [`tests/SpecScribe.Tests/StylesheetTests.cs`](../../tests/SpecScribe.Tests/StylesheetTests.cs) — assert the `.req-view` / `.req-panel:has(#rv-grid:checked)` toggle rules ship in the embedded stylesheet.
- **Section parity (must stay green, no change expected):** [`RenderSectionParityTests.cs`](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) — the toggle adds no tracked `SectionFacts` (statTiles/nowNextCards/progressBars/quickLinks/indexCards/workCards are untouched), so dashboard section parity holds as-is; run it to confirm. No `SectionViewModelSerialization` change (no new field). [memory: [[story-6-2-section-view-models-live]]]
- **Golden fingerprint:** [`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:160) **WILL change** — the home page gains the requirements toggle wrapper (and the flow-before-grid reorder), and the epics-index subtitle loses its counts. Regenerate the constant per the drill below and confirm every diff is one of exactly those two changes, nothing else. [memory: [[golden-diff-normalization-gotchas]]]
- **No test asserts the old epics-index subtitle string** (verified) and the "Stories defined" stat-tile assertion in `HtmlTemplaterTests`/`RenderSectionParityTests` targets the **stat tile** (untouched), so those stay green.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Both changes are pure string-building over `DashboardView` / `EpicsIndexView` — unit-test directly against `HtmlRenderAdapter.RenderDashboardBody` / `RenderEpicsIndexBody`, like the existing adapter suites.

Cover explicitly:

- **AC #1 toggle present (requirements + epics):** the rendered dashboard body contains the `board-tabs` clone (`id="rv-flow"` `checked`, `id="rv-grid"`, `name="req-view"`), the flow SVG inside `.req-view-flow`, and the status grid inside `.req-view-grid`; `.req-view-flow` is the default (grid hidden via CSS, verified structurally by ordering/wrapper, not by computed style).
- **AC #1 single view (requirements, no epics):** no flow → the grid renders alone with **no** `board-tabs` and **no** `.req-view` wrappers (the panel is byte-stable for this branch except the unchanged grid).
- **AC #2 text-twin guardrail:** the `req-status-grid` markup is present in the dashboard body whenever requirements exist — the consolidation never drops it.
- **AC #2 epics-index dedup:** `RenderEpicsIndexBody` subtitle contains neither the epic count restatement nor "with stories drafted"; the stat grid still renders "Epics drafted" and "Stories defined" tiles.
- **Determinism:** two generations over identical input produce identical output for both surfaces.

**Run:** `dotnet test` from repo root. Then a full generation against this repo: `dotnet run --project src/SpecScribe` (output → `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; vestigial/gitignored). Eyeball: open the home page — the Requirements panel shows the flow by default with a "Flow / Status grid" tab pair; clicking "Status grid" swaps to the status-block grid, one view at a time; open the epics page — the subtitle no longer repeats the counts the stat tiles show below. [memory: [[generate-output-dir-is-specscribeoutput]]]

**Golden-diff drill (rendered bytes change here — expect a fingerprint update):** freeze a fixture copy of `_bmad-output` + `docs/adrs` + `README.md` + `_bmad` in scratchpad, `git init` with fixed-date commits (+`--deep-git`), generate before/after, apply the 5 volatile-token normalizations (footer clock → invariant date, `?v=` token, subtitle+Version rows, About Build row, git-worktree CRLF), and confirm the ONLY diffs are (1) the home Requirements panel's new toggle wrappers + flow-before-grid reorder and (2) the epics-index subtitle losing its counts. Then regenerate the `GoldenContentFingerprint` constant (the test prints the new hash). Run twice for portability. [memory: [[golden-diff-normalization-gotchas]]]

---

## Previous Story Intelligence

**Story 8.5 (Designed Empty States — `ready-for-dev`, sibling)** established the create-story discipline this story follows: elicit visual intent up front and record owner picks as non-re-litigable decisions; keep presentation-only changes off the count/status/view-model seams; expect a golden-fingerprint move and confirm the byte diff is *only* the intended change before regenerating. No file overlap (8.5 = `EpicsViewBuilder`/`SprintTemplater` empty states; 8.6 = the requirements panel + epics-index subtitle). [Source: [8-5-designed-empty-states.md](8-5-designed-empty-states.md)]

**Story 6.2 (Section View Models — `review`)** re-homed the dashboard + epics bodies into `HtmlRenderAdapter.*.cs` driven by section view models, with a **semantic** parity harness (`RenderParity`) that tracks *facts*, not bytes. This story adds only presentational wrapper markup (no fact), so parity holds — the golden test is the byte gate, `RenderParity` is unaffected. Build the toggle inside the adapter where the panel already lives; do not push it back into a view model. [memory: [[story-6-2-section-view-models-live]]]

**Story 3.7 (Requirements Flow & Status Blocks — done)** is the load-bearing dependency for AC #2's guardrail: it created `RequirementFlow` **and** declared the status-block grid (+ requirement cards) as the flow's accessibility text equivalent ("never diagram-only"). That contract is *why* this story demotes the grid behind a DOM-preserving toggle rather than removing it or linking out. [Source: [3-7-requirements-flow-and-status-blocks.md](3-7-requirements-flow-and-status-blocks.md); [Charts.cs:1208-1209](../../src/SpecScribe/Charts.cs:1208)]

**Story 2.3 (Sprint page redesign — done)** built the pure-CSS `board-tabs` / `board-view` `:has()` toggle this story clones. Copy its shape and CSS discipline (hidden radios, `<label>` tabs, `:has()` on the panel container, focus ring), just re-scoped to `.req-panel`. [Source: [SprintTemplater.cs:183-204](../../src/SpecScribe/SprintTemplater.cs:183)]

**Recurring lessons that apply here:**

- **Elicit visual intent up front** (Epic 3 retro, open action) — the mechanism (toggle vs. link-out), the default-primary view, and the count-dedup target were offered as named directions and the owner picked *radio-toggle*, *flow-as-primary*, and *epics-index subtitle*. Build those. [memory: [[create-story-elicit-visual-intent]]]
- **Split, don't absorb** — if this tempts you into re-pairing counts (8.2), restyling badges (8.1/8.3), or consolidating Deep Analytics' coupling views, stop; 8.6 is the home requirements toggle + the epics-index subtitle dedup only. [Source: Epic 2/3 retros]

---

## Git Intelligence Summary

Recent history is planning/spike/merge churn on `main` (`Code review`, `Decision-making and ADRs`, `Merge branch 'spike/delivery-arch-6-6'`) — no in-flight code touches `AppendRequirementsPanel`, the epics-index subtitle, or the `.board-tabs` CSS, so this change is additive and uncontended against siblings 8.1/8.2/8.4/8.5. Light adjacency with 8.3 if both edit `HtmlRenderAdapter.Epics.cs` (8.3 = card/lane headers + tooltips; 8.6 = the header subtitle only — non-overlapping hunks); both regenerate the golden fingerprint, so re-run the drill against whichever lands second. **Heed the worktree rule:** if this runs in a worktree, edit files at the **worktree path** — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [memory: [[worktree-edits-must-target-worktree-path]]]

---

## Latest Technical Information

No external libraries or APIs are introduced — pure in-repo C# string-building over existing chart calls + CSS — so there is no version/security research to fold in. Discipline note: the `:has()` view-switch selector must be scoped to the **`.req-panel`** container (`.req-panel:has(#rv-grid:checked) …`), mirroring how the sprint toggle scopes to `.sprint-page`, because the tabs sit inside the panel header rather than adjacent to the views — a plain `~` sibling selector would not reach across the header row.

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR coverage: [Source: [epics.md:1165-1169](../planning-artifacts/epics.md:1165)]
- Story 8.6 user story + both ACs: [Source: [epics.md:1282-1300](../planning-artifacts/epics.md:1282)]
- The T2 finding (same data multiple ways; toggle recommendation; never cut a text-twin) + the home-page "coverage matrix is the keeper" note: [Source: [Epic3UXFeedback.md:25-32](../../docs/Epic3UXFeedback.md), [Epic3UXFeedback.md:71](../../docs/Epic3UXFeedback.md)]
- The UX review that seeded Epics 8–10: [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)]
- Story 3.7 text-twin contract: [Source: [3-7-requirements-flow-and-status-blocks.md](3-7-requirements-flow-and-status-blocks.md); [Charts.cs:1208-1209](../../src/SpecScribe/Charts.cs:1208)]
- The reused sprint toggle: [Source: [SprintTemplater.cs:183-204](../../src/SpecScribe/SprintTemplater.cs:183); [assets/specscribe.css:3093-3120](../../src/SpecScribe/assets/specscribe.css:3093)]
- Architecture invariants (truthfulness, accessibility, single-source, deterministic, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]
- Delivery-seam / section-view-model / pure-render / now-and-next-board / golden-fingerprint / output-dir / worktree / visual-intent discipline: project memory ([[story-6-1-delivery-seam-live]]; [[story-6-2-section-view-models-live]]; [[charting-is-pure-svg-no-js]]; [[now-and-next-is-the-sprint-board]]; [[golden-diff-normalization-gotchas]]; [[generate-output-dir-is-specscribeoutput]]; [[worktree-edits-must-target-worktree-path]]; [[create-story-elicit-visual-intent]]; [[specscribe-status-token-system]]).

---

## Tasks / Subtasks

- [ ] **Task 1 — Home Requirements panel radio-toggle (AC: #1, #2 twin guardrail)**
  - [ ] In `AppendRequirementsPanel`, when `epicsModel is not null`: emit the `board-tabs` clone (radios `rv-flow` checked / `rv-grid`, name `req-view`, labels "Flow" / "Status grid") in the header row, then `<div class="req-view req-view-flow">`(flow)`</div>` and `<div class="req-view req-view-grid">`(grid)`</div>`.
  - [ ] When `epicsModel is null`: render the grid alone, no toggle, no `.req-view` wrappers (byte-stable branch).
  - [ ] Keep both `Charts.RequirementFlow` / `Charts.RequirementStatusGrid` calls unchanged; the grid stays in the DOM (text-twin never removed).
- [ ] **Task 2 — Toggle CSS (AC: #1)**
  - [ ] In `specscribe.css` next to the sprint `.board-tabs` block: add `#rv-flow`/`#rv-grid` active-tab + `:focus-visible` rules; add `.req-view-grid { display: none }` and the `.req-panel:has(#rv-grid:checked)` view-switch rules. Reuse the generic `.board-tabs/.board-tabbar/.board-tab/.board-tab-radio` chrome. Tokens only, no hex.
  - [ ] Add `StylesheetTests` assertion for the `.req-view` toggle rules.
- [ ] **Task 3 — Epics-index count dedup (AC: #2)**
  - [ ] In `RenderEpicsIndexBody`, trim the header subtitle to `{SiteTitle}` (drop the epic/drafted count restatement); leave the stat grid + "Progress by Epic" mosaic untouched.
- [ ] **Task 4 — Tests (AC: #1, #2)**
  - [ ] `HtmlRenderAdapterTests`: toggle-present (requirements + epics) with flow default + grid alternate + `board-tabs`; single-view (requirements, no epics) grid-only no toggle; text-twin grid present; epics-index subtitle no longer restates counts while stat tiles remain.
  - [ ] Confirm `RenderSectionParity` dashboard facts unchanged; regenerate `GoldenContentFingerprint` after confirming the byte diff is only the toggle wrappers/reorder + the subtitle trim.
- [ ] **Task 5 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green; real generation to `SpecScribeOutput/`; eyeball the home Requirements panel (flow default, "Flow / Status grid" tabs, one view at a time) and the epics page (subtitle no longer repeats the stat-grid counts).

## Dev Notes

- **The sharp edge is fidelity + scope, not difficulty.** Every change is a small markup/CSS edit, but three disciplines constrain it: the toggle is a **structural clone** of the sprint pattern (don't reinvent it, don't reuse its ids), the grid **stays in the DOM** as the flow's text-twin (never remove/replace/link-out), and AC #2 removes a **display** only (never a count/source). Get those three right and the golden diff is exactly the toggle wrappers + the subtitle trim.
- **The toggle IS the accessibility answer.** Keeping both views in the DOM (only `display:none` when unselected, like the sprint board) is precisely what satisfies "the text-twin is never removed" — a link-out would have failed AC #2. The flow's own `role="img"` + `<title>`s carry the default view; the focusable radio reaches the grid.
- **Order flips on purpose.** Flow renders before grid now (primary first). That reorder is part of the expected golden diff — verify it's the *only* home-page change besides the wrappers.
- **No view model, no fact.** The requirements panel renders straight from `view.Requirements` + `view.Epics`; the toggle is presentational chrome the adapter owns. `RenderParity` (semantic) stays green; only the byte-level golden test moves. [memory: [[story-6-2-section-view-models-live]]]
- **Count dedup loses no information.** The epics-index counts remain in the stat grid two lines below the subtitle; only the duplicate restatement goes. Correctness across pages is 8.2's, not this story's.
- **Scope guard for later 8.x:** recency signals (8.7) sit near the dashboard but are NOT this story. 8.6 is the home requirements toggle + the epics-index subtitle dedup.

### Project Structure Notes

- All change concentrates in `HtmlRenderAdapter.Dashboard.cs` + `HtmlRenderAdapter.Epics.cs` + `specscribe.css` plus tests. No new page, no new adapter contract, no view-model field, no package restructure. The Story 6.1/6.2 delivery seam and the `RenderParity`/`SectionFacts` contract are untouched (presentational wrapper markup carries no fact).
- The toggle reuses the sprint page's proven pure-CSS `:has()` pattern with panel-unique ids (`rv-flow`/`rv-grid`, name `req-view`) scoped to `.req-panel`, so the two toggles are collision-proof by construction.

### References

- [Source: [epics.md:1282-1300](../planning-artifacts/epics.md:1282)] — Story 8.6 user story + both ACs.
- [Source: [epics.md:1165-1169](../planning-artifacts/epics.md:1165)] — Epic 8 goal; FRs; UX-DRs; NFR8.
- [Source: [Epic3UXFeedback.md:25-32](../../docs/Epic3UXFeedback.md)] — T2 (same data multiple ways; toggle recommendation; never cut a text-twin).
- [Source: [Epic3UXFeedback.md:71](../../docs/Epic3UXFeedback.md)] — home-page "coverage matrix is the keeper".
- [Source: [HtmlRenderAdapter.Dashboard.cs:199-212](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:199)] — `AppendRequirementsPanel` (add the toggle wrappers).
- [Source: [HtmlRenderAdapter.Epics.cs:20-24](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:20), [72-80](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:72)] — epics-index subtitle (trim) + stat grid (keep).
- [Source: [SprintTemplater.cs:183-204](../../src/SpecScribe/SprintTemplater.cs:183)] — `RenderBoardTabs` / `AppendBoardViews` (the pattern to clone).
- [Source: [assets/specscribe.css:3093-3120](../../src/SpecScribe/assets/specscribe.css:3093)] — the sprint `.board-tabs` toggle CSS (mirror for `.req-view`).
- [Source: [Charts.cs:1146](../../src/SpecScribe/Charts.cs:1146), [1210](../../src/SpecScribe/Charts.cs:1210), [1208-1209](../../src/SpecScribe/Charts.cs:1208)] — `RequirementStatusGrid` / `RequirementFlow` + the text-twin contract note.
- [Source: [RenderParity.cs:353-365](../../src/SpecScribe/RenderParity.cs:353)] — dashboard `SectionFacts` (the requirements panel is not a tracked fact; parity holds).
- [Source: [3-7-requirements-flow-and-status-blocks.md](3-7-requirements-flow-and-status-blocks.md)] — the Story 3.7 accessibility text-twin contract.
- [Source: [SiteGeneratorAdapterTests.cs:160](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:160)] — `GoldenContentFingerprint` (regenerate after confirming the byte diff).
- [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)] — truthfulness, accessibility, single-source, deterministic, seed-not-invariant.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
