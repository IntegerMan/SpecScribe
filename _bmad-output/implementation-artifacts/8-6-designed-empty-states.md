# Story 8.6: Designed Empty States

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder viewing a shared portal,
I want empty sections to read as intentional guidance,
so that zero-counts and repeated CLI hints do not read as errors or clutter.

## Acceptance Criteria

1.
**Given** multiple stories in an epic lack task plans
**When** the epics page renders
**Then** per-story CLI hints consolidate into one banner per epic with a single copy-able command affordance
**And** hint text is adapter-supplied, not hard-coded (NFR8). [Source: epics.md#Story 8.6; UX-DR9]

2.
**Given** a sprint board column is empty
**When** the board renders
**Then** the column shows intentional guidance copy (for example "Nothing in progress — pick from Ready")
**And** empty states are visually styled as designed states, not bare zero-counts. [Source: epics.md#Story 8.6; UX-DR9]

---

## Developer Context

**This is a presentation-only story.** No new page, no data-model or count change, no parser touch. It cleans up two existing "there's nothing here" surfaces so they read as *designed* rather than as errors or repeated clutter:

1. **AC #1 — the epic page's per-story "no plan yet" hints.** Today every undrafted story card on an epic page renders its own inline `No detailed story plan yet — draft it with /…-create-story X.Y` note ([`EpicsViewBuilder.BuildStoryCard`](../../src/SpecScribe/EpicsViewBuilder.cs:72) → [`AppendStoryCard`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211)'s `.not-detailed-note`). A backlog-heavy epic (e.g. Epics 11–16 in this very repo) repeats that command down the whole page — visual clutter that reads as N problems. This story **consolidates those repeated hints into one banner at the top of the epic's story list**, carrying a single copy-able `create-story` command; the individual cards keep a plain, command-free "No detailed story plan yet." label.
2. **AC #2 — the sprint board's empty columns.** Today an empty Kanban lane in [`SprintTemplater.RenderBoard`](../../src/SpecScribe/SprintTemplater.cs:103) shows just its label + a bare `0` count over blank space, which reads as a dead/broken column. This story gives an empty lane a **dashed ghost-card placeholder with column-specific guidance copy** (e.g. "Nothing in progress — pick from Ready") so it reads as an intentional designed state.

Both are direct fixes for live UX-review findings graded against the shared-portal / daily-pulse journeys:

> Repeated per-story CLI hints and bare zero-count columns read as errors or clutter to a stakeholder. Consolidate the hints into one guided banner per epic, and give empty board columns intentional, designed empty-state copy. [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md); UX-DR9]

It sits alongside its Epic 8 siblings but touches a **different seam** than any of them: **8.2** owns status words/colors (`StatusStyles`); **8.3** owns counts (`ProjectCounts`); **8.4** owns progress+state pairing + sprint-board *tooltips*; **8.5** owns the Next Steps *command surface* (`BmadCommands.ForStory/ForEpic/ForProject`). **8.6 owns two empty-state surfaces**: the epic-page undrafted-story banner (`EpicsViewBuilder` + `HtmlRenderAdapter.Epics`) and the sprint-board empty lane (`SprintTemplater.RenderBoard`). No file overlap with 8.2/8.3/8.4; a light, non-contended adjacency with 8.5 (both read `commands.Command("create-story", …)` through the same catalog seam, but 8.5 edits `BmadCommands.cs` and 8.6 does not).

### Owner-selected design decisions (visual intent elicited at create-story — do not re-litigate)

**1. AC #1 silhouette → "Top-of-list consolidated banner" (owner pick).** When an epic has **two or more** stories without a task plan, render **one** designed banner at the top of that epic's story-card list: a short count sentence (`N stories in this epic need task plans`) plus **one** copy-able command affordance = `create-story {id}` for the **next** undrafted story (the first story in file order with no plan — the same "next to detail" target `AppendUpNextCard`/`ForEpic` already pick). Each undrafted story card below then shows a **plain, command-free** `No detailed story plan yet.` note. **Do NOT** leave the per-card commands in place (that's the clutter being removed), and **do NOT** print one banner command per undrafted story (the single-affordance is the whole point). [Owner decision, this story; memory: [[create-story-elicit-visual-intent]]]

**2. AC #1 threshold → banner only when 2+ undrafted; a lone undrafted story is unchanged.** The AC is scoped "**Given multiple** stories in an epic lack task plans." A single undrafted story is not clutter, so it keeps today's inline per-card `create-story` command and **no** banner renders. This matches the AC literally and keeps single-undrafted epic pages byte-stable (smaller, cleaner golden diff). [Owner decision, this story]

**3. AC #2 silhouette → "Dashed ghost-card placeholder" (owner pick).** An empty lane renders a faint **dashed, card-sized placeholder** holding column-specific guidance copy — reusing the project's established "not-here-yet" dashed-placeholder visual language (the sunburst no-plan arc `.sb-noplan`, the funnel zero-count dashed band), so the empty column reads as an intentional designed state, not a bug. **Do NOT** use a plain muted text line with no placeholder, and **do NOT** add a per-column icon. [Owner decision, this story; memory: [[funnel-is-sideways-conventional-silhouettes]]]

### The rendering model (read carefully — this is where the change lives)

**AC #1 — build the banner as a named opaque fragment (Story 6.2 discipline).** The banner is command-catalog-driven guidance HTML, exactly like the epic page's existing `NextActionsPanelHtml` / `NextStepsHtml` / `RetroAffordanceHtml` — so it is **pre-rendered in `EpicsViewBuilder.BuildEpic` and carried on `EpicPageView` as a new opaque string fragment**, then emitted verbatim by the adapter. This keeps the adapter contract, the view-model shape philosophy, and the section-fact parity harness untouched (a fragment adds no *fact*). [memory: [[story-6-2-section-view-models-live]]]

Concretely, in [`BuildEpic`](../../src/SpecScribe/EpicsViewBuilder.cs:37):

> `var undrafted = epic.Stories.Where(s => s.ArtifactOutputPath is null).ToList();`

- If `undrafted.Count >= 2` → build `UndraftedBannerHtml` via `BmadCommands.InlineGuidance(commands.Command("create-story", undrafted[0].Id), lead, fallback)`, and build every story card **consolidated** (plain note).
- Else → `UndraftedBannerHtml = ""` and build story cards **un-consolidated** (today's inline-command note for the lone undrafted card, if any).

The predicate `ArtifactOutputPath is null` is deliberately the **same** one `BuildStoryCard` already uses for `!hasArtifact`, so the banner consolidates exactly the hints that would otherwise render per-card — no drift between "who gets a banner" and "who loses their inline command."

**Why the command carries the *next* undrafted id (not a bare `create-story` and not one-per-story):** `create-story` processes **one** story at a time, so the truthful single affordance is "draft the next one" — copy it, run it, and when that story gains a plan the banner recomputes to the following undrafted story. This mirrors `ForEpic`'s existing `create-story {nextUndetailed.Id}` behavior, so the two surfaces stay consistent.

**NFR8 (AC #1's "adapter-supplied, not hard-coded"):** the command string comes from `commands.Command("create-story", id)`, which returns the module-correct slash command (`/bmad-create-story 8.7` for BMad, `/gds-create-story …` for a GDS project) or **null** when the module exposes no such step. `InlineGuidance` returns its plain `fallback` text when the command is null, so a module without a create-story workflow shows the count sentence with **no** command affordance rather than a hard-coded or broken command. The surrounding English lead is a fixed template exactly as every other `InlineGuidance` caller does it — the *command* is the adapter-supplied part, and it already routes through the catalog. [Source: [BmadCommands.cs:191](../../src/SpecScribe/BmadCommands.cs:191); [ModuleContext.cs:43](../../src/SpecScribe/ModuleContext.cs:43)]

**AC #2 — the empty lane placeholder in the shared board renderer.** [`RenderBoard`](../../src/SpecScribe/SprintTemplater.cs:103) already loops the five `BoardColumns` and renders every column even when empty ("an empty Done column is meaningful on a board"). Today when `col.Count == 0` the `.sprint-cards` container is simply empty. Change: when `col.Count == 0`, append **one** `.sprint-lane-empty` dashed placeholder carrying that column's guidance copy (see the copy table below). `RenderBoard` is **shared** by the sprint page **and** the home dashboard's Now & Next board [memory: [[now-and-next-is-the-sprint-board]]], so the designed empty state appears consistently on both — keep the placeholder compact (a single line of copy in a dashed card) so it doesn't bloat the compact home panel.

### AC #2 — per-column guidance copy (canonical lifecycle columns; column-specific, truthful, guiding)

These are the board's own canonical lifecycle columns (not framework-native status words), so fixed English copy is correct here — no adapter data needed (contrast AC #1). Each line names the empty state and points to the adjacent actionable column:

| Column (`cssClass`) | Label | Empty-state copy |
|---|---|---|
| `pending` | Backlog | `Backlog is clear — every story is scheduled.` |
| `ready` | Ready for dev | `Nothing ready to pick up — draft or refine the next story.` |
| `active` | In progress | `Nothing in progress — pick from Ready.` *(the AC's own example)* |
| `review` | In review | `Nothing awaiting review.` |
| `done` | Done | `Nothing finished yet.` |

Wire the copy off the same `cssClass` the loop already has (a small `switch`/lookup keyed on `cssClass`, defaulting to a generic `Nothing here yet.` for safety). Keep the strings in `SprintTemplater` next to `BoardColumns` so they read as one table.

### Scope boundaries (read carefully)

- **Do NOT change any count, status word, or status color.** AC #1's banner restates a count that already exists (the number of undrafted stories) — derive it from `epic.Stories`, do not introduce a parallel counter or reclassify a status. That's 8.2/8.3's seam. [memory: [[specscribe-status-token-system]]]
- **Do NOT touch `BmadCommands.cs`** (that's 8.5's file this sprint) beyond *calling* the existing public `InlineGuidance` / `Command` — no new method there. If a genuinely shared helper is unavoidable, prefer adding it in `EpicsViewBuilder`/`SprintTemplater`, not `BmadCommands`, to stay uncontended with 8.5.
- **Do NOT touch the `RenderBoardByEpic` (By-epic) view.** AC #2 is about the *status-column* board (`RenderBoard`). The by-epic view's "empty" case is an epic with no stories — a different surface, explicitly out of scope here.
- **Do NOT change the `Up Next` / `Next Steps` panels.** The banner is a *new* top-of-list element, separate from the existing `NextActionsPanelHtml`; do not fold undrafted-story consolidation into those panels (owner picked the top-of-list banner, decision 1).
- **Do NOT add a client-side script or NuGet package.** Pure C# string-building + CSS; reuse the existing `cmd-badge` copy/send-menu JS unchanged for the banner's command. [memory: [[charting-is-pure-svg-no-js]]]
- **Do NOT change the section-fact contract.** The banner is an opaque fragment (no new fact); the per-card note change stays inside the already-opaque `StoryCardView.NoteHtml`. Section FACTS (id/status/task tally) must not move. [memory: [[story-6-2-section-view-models-live]]]
- **Do NOT write back to any source.** Local-first, read-only invariant.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Add the undrafted-story banner as an opaque fragment on `EpicPageView`.** In [`EpicsView.cs`](../../src/SpecScribe/EpicsView.cs:135), add `public required string UndraftedBannerHtml { get; init; }` on `EpicPageView`, parallel to the existing `NextActionsPanelHtml`/`NextStepsHtml`/`RetroAffordanceHtml` opaque fragments (same `required string` shape so JSON round-trip is symmetric). Empty string when no banner shows.
- **Build the banner + decide consolidation in `BuildEpic`.** In [`EpicsViewBuilder.BuildEpic`](../../src/SpecScribe/EpicsViewBuilder.cs:37): compute `undrafted = epic.Stories.Where(s => s.ArtifactOutputPath is null).ToList()`. When `undrafted.Count >= 2`, set `UndraftedBannerHtml = RenderUndraftedBanner(epic, undrafted, commands)` and pass `consolidated: true` into `BuildStoryCard`; otherwise `UndraftedBannerHtml = string.Empty` and `consolidated: false`.
- **Add `RenderUndraftedBanner` (new private helper in `EpicsViewBuilder`).** It builds the designed banner: a count sentence + the single command via `BmadCommands.InlineGuidance(commands.Command("create-story", undrafted[0].Id), $"{n} stories in this epic need task plans — draft the next with", $"{n} stories in this epic need task plans.")`. Wrap it in a `<div class="epic-undrafted-banner">…</div>`. Escape any interpolated text via `PathUtil.Html` (the count is an int; the `InlineGuidance` output is already escaped). Keep it a *named opaque fragment* built here, not in the adapter.
- **Thread a `consolidated` flag into `BuildStoryCard`.** [`BuildStoryCard`](../../src/SpecScribe/EpicsViewBuilder.cs:72) gains a `bool consolidated` parameter. When `!hasArtifact`: if `consolidated`, set `noteHtml = "No detailed story plan yet."` (plain, **no** command — the banner carries it); else keep today's `InlineGuidance(commands.Command("create-story", story.Id), "No detailed story plan yet — draft it with", "No detailed story plan yet.")`. The `.not-detailed-note` wrapper the adapter renders is unchanged either way.
- **Emit the banner before the story cards in the adapter.** In [`HtmlRenderAdapter.RenderEpicBody`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:196), append `view.UndraftedBannerHtml` immediately after `view.RetroAffordanceHtml` and **before** the `foreach (var card in view.StoryCards)` loop — verbatim, opaque (`main.Append(view.UndraftedBannerHtml);`). It is a top-of-list element, so it should not be a TOC entry.
- **Render the empty-lane placeholder in `RenderBoard`.** In [`SprintTemplater.RenderBoard`](../../src/SpecScribe/SprintTemplater.cs:103), when `col.Count == 0`, append one `<div class="sprint-lane-empty">{copy}</div>` inside the `.sprint-cards` container, where `{copy}` is the column-specific string from the copy table (keyed on `cssClass`, HTML-escaped via `PathUtil.Html`). Non-empty columns are unchanged. Applies to both the sprint page and the home board (shared renderer).
- **Add the two CSS classes, routed through tokens.** In [`specscribe.css`](../../src/SpecScribe/assets/specscribe.css): add `.epic-undrafted-banner` (a designed banner — muted/parchment surface with a `--status-drafted` accent, e.g. a `border-left: 3px solid var(--status-drafted)`, since these stories are drafted-but-unplanned; the command badge sits inline) near the `.pending-note`/`.empty-state` block ([:2209](../../src/SpecScribe/assets/specscribe.css:2209), [:2219](../../src/SpecScribe/assets/specscribe.css:2219)); add `.sprint-lane-empty` (dashed, muted, card-shaped: `border: 1px dashed var(--border)`, `background: transparent`, `color: var(--ink-light)`, italic, centered, same padding footprint as `.sprint-card`) near the `.sprint-cards`/`.sprint-card` block ([:2977](../../src/SpecScribe/assets/specscribe.css:2977), [:3052](../../src/SpecScribe/assets/specscribe.css:3052)). **No literal hex** — reuse existing tokens/neutrals only. [memory: [[specscribe-status-token-system]]]
- **Assert the new classes ship.** `StylesheetTests` asserts on embedded-stylesheet content — add companion assertions for `.epic-undrafted-banner` and `.sprint-lane-empty`.

### DON'T

- **DON'T print one banner command per undrafted story** — exactly one copy-able command (the next undrafted id) per epic banner (AC #1's "single copy-able command affordance").
- **DON'T render the banner for a single undrafted story** — threshold is 2+ (owner decision 2); a lone undrafted card keeps its inline command.
- **DON'T hard-code the create-story slash command** — always through `commands.Command("create-story", id)` so it degrades to plain fallback text under NFR8.
- **DON'T edit `BmadCommands.cs`, `StatusStyles`, or any count source** — call the existing public helpers only; this story owns no logic in those files (8.5/8.2/8.3 do).
- **DON'T touch `RenderBoardByEpic`** or the Up Next / Next Steps panels.
- **DON'T add JS or a NuGet package**, and don't move any section fact.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)]:

- **Framework-agnostic guidance (NFR8)** — AC #1's banner command routes through the same `CommandCatalog` seam every other next-action uses; a module without `create-story` degrades the banner to count-only text (no command), never a hard-coded string. AC #2's copy names the board's *canonical* lifecycle columns (not framework-native vocabulary), so fixed copy is correct there. [Source: [ModuleContext.cs:43-51](../../src/SpecScribe/ModuleContext.cs:43); [BmadCommands.cs:191-196](../../src/SpecScribe/BmadCommands.cs:191)]
- **Single source of truth** — the banner restates the count of undrafted stories derived directly from `epic.Stories` (the same list the cards render from); no parallel counter, no reclassification. The empty-lane placeholder is driven by the same `col.Count == 0` the header count already reflects, so the "0" badge and the placeholder can never disagree. [memory: [[specscribe-status-token-system]]]
- **Truthfulness over convenience** — consolidating the hints removes *clutter*, not *information*: every undrafted card still says "No detailed story plan yet.", and the one banner command is the honest single next move (`create-story` is one-at-a-time). An empty column still shows its real `0` count *and* explains what the emptiness means — it never hides the zero.
- **Accessibility is part of the rendering contract (NFR6, UX-DR17)** — the empty-state meaning is carried in **text** (the copy line, the banner sentence), not by dashed styling alone; the dashed border is reinforcement. The banner's command stays a real, focusable `cmd-badge`. Do not make either state color-/border-only. The lane's existing `aria-label` ("{label}: {count} stories") already announces emptiness; the placeholder copy is supplementary visible guidance, so no ARIA change is required (confirm the empty placeholder is not announced as an interactive control — it's inert text).
- **Deterministic, generation-time-only output** — banner text and empty-state copy derive solely from the projected model + catalog; a from-scratch regen of identical inputs is byte-identical. No per-visitor state.
- **Seed, not invariant** — no Core/Adapters package split; changes stay in `EpicsViewBuilder.cs` + `EpicsView.cs` + `HtmlRenderAdapter.Epics.cs` + `SprintTemplater.cs` + `specscribe.css`. [Source: [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: [SpecScribe.Tests.csproj](../../tests/SpecScribe.Tests/SpecScribe.Tests.csproj)]
- **Reuse, don't reinvent (all already in-repo):**
  - [`BmadCommands.InlineGuidance(command, lead, fallback)`](../../src/SpecScribe/BmadCommands.cs:191) — the shared "lead-in + command badge, or plain fallback when the command is null" renderer already used by the per-card note, the pending-epic note, and the empty-epics guidance. Use it verbatim for the banner's single command so NFR8 degradation and the `cmd-badge` copy/send-menu come for free.
  - [`CommandCatalog.Command("create-story", id)`](../../src/SpecScribe/ModuleContext.cs:43) — the module-correct slash command (or null). Same seam `BuildStoryCard`/`ForEpic` already use.
  - [`PathUtil.Html`](../../src/SpecScribe/PathUtil.cs) — escape any interpolated text.
  - `StatusStyles` / the `--status-drafted` token and the sprint `BoardColumns` — the banner accent and the lane classes ride existing tokens; no new palette. [memory: [[specscribe-status-token-system]]]
  - The established **dashed-placeholder visual language** — `.sb-noplan` (sunburst no-plan arc) and the refinement-funnel zero-count dashed band ([specscribe.css:1589](../../src/SpecScribe/assets/specscribe.css:1589)) — mirror their dashed/muted treatment for `.sprint-lane-empty` so the empty state reads as the same "not here yet" idiom. [memory: [[funnel-is-sideways-conventional-silhouettes]]]

---

## File Structure Requirements

**No new production classes.** One new opaque-fragment field, one new private helper, one threaded flag, one new lane branch, two CSS classes.

**Modified files (read fully before editing):**

- [`src/SpecScribe/EpicsView.cs`](../../src/SpecScribe/EpicsView.cs:110) — add `UndraftedBannerHtml` (`required string`) to `EpicPageView`, parallel to the existing opaque fragments.
- [`src/SpecScribe/EpicsViewBuilder.cs`](../../src/SpecScribe/EpicsViewBuilder.cs:37) — `BuildEpic`: compute `undrafted`, set `UndraftedBannerHtml`, pass `consolidated` into `BuildStoryCard`; add `RenderUndraftedBanner`; extend `BuildStoryCard` with the `consolidated` flag + plain-note branch.
- [`src/SpecScribe/HtmlRenderAdapter.Epics.cs`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:196) — `RenderEpicBody`: emit `view.UndraftedBannerHtml` after the retro affordance and before the story-card loop.
- [`src/SpecScribe/SprintTemplater.cs`](../../src/SpecScribe/SprintTemplater.cs:103) — `RenderBoard`: append the `.sprint-lane-empty` placeholder + per-column copy table (keyed on `cssClass`).
- [`src/SpecScribe/assets/specscribe.css`](../../src/SpecScribe/assets/specscribe.css:2209) — add `.epic-undrafted-banner` (near `.pending-note`/`.empty-state`) and `.sprint-lane-empty` (near `.sprint-card`/`.sprint-cards`). Route colors through `--status-*`/`--ink-light`/`--border`. **`StylesheetTests` asserts stylesheet content — add companion assertions.**

**Tests to update / add:**

- [`tests/SpecScribe.Tests/SprintTemplaterTests.cs`](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs) — the core AC #2 coverage. Add: an empty column renders one `.sprint-lane-empty` with its column-specific copy (e.g. an empty `active` lane contains "Nothing in progress — pick from Ready"); a populated column renders **no** `.sprint-lane-empty`; the existing `RenderBoard_CapsEachColumnAndLinksToMore` and the per-lane `sprint-lane {cls}` assertion ([:72](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs:72)) still pass. The existing helpers make these one-liners.
- [`tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs) — AC #1 coverage. The single-undrafted fixture (`GenerateAll_PlaceholderDoesNotChangeDetailedStoryAccounting`, [:150](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs:150)) asserts `.not-detailed-note` is present — **still passes** (a lone undrafted story is un-consolidated, keeps its inline note, no banner). **Add** a 2+-undrafted-stories fixture that asserts: exactly one `.epic-undrafted-banner` with the count sentence + a single `create-story` command; the undrafted cards carry a **plain** `No detailed story plan yet.` note with **no** `create-story` command in the card body.
- [`tests/SpecScribe.Tests/StylesheetTests.cs`](../../tests/SpecScribe.Tests/StylesheetTests.cs) — assert `.epic-undrafted-banner` and `.sprint-lane-empty` ship in the embedded stylesheet.
- **Section parity / serialization:** [`RenderSectionParityTests.cs`](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) — the banner is an opaque fragment and the note change stays inside `StoryCardView.NoteHtml`, so section FACTS are unchanged; parity should hold as-is. [`SectionViewModelSerializationTests.cs`](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs) round-trips the view models to JSON — confirm the new `required string UndraftedBannerHtml` field round-trips (it's a plain string, symmetric with the sibling opaque fragments; update any hand-built `EpicPageView` fixture in the tests to set it). [memory: [[story-6-2-section-view-models-live]]]
- **Golden fingerprint:** [`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:160) **WILL change** — this repo has several all-backlog epics (11–16) whose epic pages gain the banner + flip their cards to plain notes, and the board may gain empty-lane placeholders. Regenerate the constant per the drill below and confirm every diff is an undrafted-banner / plain-note / empty-lane change, nothing else. [memory: [[golden-diff-normalization-gotchas]]]

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Both changes are pure string-building over `EpicInfo`/`EpicsModel` + `SprintStatus` + `CommandCatalog` — unit-test directly against `EpicsViewBuilder.BuildEpic`/`HtmlRenderAdapter.RenderEpicBody` and `SprintTemplater.RenderBoard`, like the existing suites.

Cover explicitly:

- **AC #1 consolidation (2+ undrafted):** an epic with ≥2 undrafted stories → exactly one `.epic-undrafted-banner`, containing the count sentence and exactly one `create-story` command badge (the **first** undrafted story's id); each undrafted story card renders a plain `No detailed story plan yet.` note with **no** command; drafted cards are unchanged.
- **AC #1 threshold (single undrafted):** an epic with exactly one undrafted story → **no** `.epic-undrafted-banner`; that card keeps today's inline `create-story {id}` note (byte-stable vs. before this story).
- **AC #1 NFR8 degradation:** with a `CommandCatalog` that exposes no `create-story`, the banner (2+ case) renders the count sentence with **no** command affordance (plain fallback), and undrafted cards show the plain note — never a hard-coded or empty command.
- **AC #2 empty column:** `RenderBoard` over a sprint where a column is empty → that lane contains one `.sprint-lane-empty` with the column-specific copy (assert the `active`-empty case shows "Nothing in progress — pick from Ready"); a populated column contains **no** `.sprint-lane-empty`; the lane's `0` count still renders.
- **AC #2 shared surface:** the placeholder renders identically whether `RenderBoard` is called for the sprint page or the capped home board (call both ways; capping is orthogonal to emptiness).
- **Determinism:** two generations over identical input produce identical output for both surfaces.

**Run:** `dotnet test` from repo root. Then a full generation against this repo: `dotnet run --project src/SpecScribe` (output → `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; vestigial/gitignored). Eyeball: open an all-backlog epic page (e.g. Epic 11 or 16) — one banner at the top with a single `create-story` command, and the undrafted cards below showing plain "No detailed story plan yet." notes; open the sprint page — any empty lane shows a dashed placeholder with its guidance copy, not blank space. [memory: [[generate-output-dir-is-specscribeoutput]]]

**Golden-diff drill (rendered bytes change here — expect a fingerprint update):** freeze a fixture copy of `_bmad-output` + `docs/adrs` + `README.md` + `_bmad` in scratchpad, `git init` with fixed-date commits (+`--deep-git`), generate before/after, apply the 5 volatile-token normalizations (footer clock → invariant date, `?v=` token, subtitle+Version rows, About Build row, git-worktree CRLF), and confirm the ONLY diffs are undrafted-banner blocks, per-card note text (command → plain on multi-undrafted epics), and empty-lane placeholders. Then regenerate the `GoldenContentFingerprint` constant (the test prints the new hash). Run twice for portability. [memory: [[golden-diff-normalization-gotchas]]]

---

## Previous Story Intelligence

**Story 8.5 (State-Aware Next-Step Command Surface — `ready-for-dev`, sibling)** edits `BmadCommands.cs` (the Next Steps command surface) + `.next-steps*` CSS. 8.6 **calls** `BmadCommands.InlineGuidance` but does **not** edit `BmadCommands.cs` — the two are uncontended. If both land in the same sprint, expect independent golden-fingerprint moves (8.5 = Next Steps panels; 8.6 = undrafted banners + empty lanes); regenerate the constant against whichever lands second. [Source: [8-5-state-aware-next-step-command-surface.md](8-5-state-aware-next-step-command-surface.md)]

**Story 8.4 (Paired Progress & Readiness — `ready-for-dev`, sibling)** adds sprint-board column *tooltips* + badge pairing in `HtmlRenderAdapter.Epics`/`SprintTemplater`/`Charts`. 8.6 also edits `SprintTemplater.RenderBoard` (the empty-lane branch) and `HtmlRenderAdapter.Epics` (emitting the banner) — a **light adjacency**: 8.4 touches card/lane *headers* and tooltips, 8.6 touches the *empty* lane body and adds a top-of-list banner. Coordinate at merge (small, non-overlapping hunks in the same methods); both regenerate the golden fingerprint. [Source: [8-4-paired-progress-and-readiness-semantics.md](8-4-paired-progress-and-readiness-semantics.md)]

**Story 6.2 (Section View Models — `review`)** established that command-catalog-driven guidance HTML on the epic page is carried as **named opaque fragments** (`NextActionsPanelHtml`, `NextStepsHtml`, `RetroAffordanceHtml`) built in `EpicsViewBuilder` and emitted verbatim by the adapter — the exact pattern the new `UndraftedBannerHtml` follows, so no adapter/contract change and no section-fact movement. [memory: [[story-6-2-section-view-models-live]]]

**Story 2.1 (Inline authoring guidance)** introduced `BmadCommands.InlineGuidance` + the `.pending-note`/`.empty-state`/`.not-detailed-note` vocabulary this story consolidates and extends — build *on* it, don't re-invent a second guidance renderer. [Source: [BmadCommands.cs:186-196](../../src/SpecScribe/BmadCommands.cs:186)]

**Recurring lessons that apply here:**

- **Elicit visual intent up front** (Epic 3 retro, open action) — both new visual surfaces (the per-epic banner silhouette + the empty-lane treatment) were offered as named directions and the owner picked *top-of-list consolidated banner* and *dashed ghost-card placeholder*. Build those, not a re-invented silhouette. [memory: [[create-story-elicit-visual-intent]]]
- **Split, don't absorb** — if this tempts you into restyling badges (8.2/8.4), re-pairing counts (8.3), or reshaping the Next Steps command surface (8.5), stop; 8.6 is the two empty-state surfaces only. [Source: Epic 2/3 retros]

---

## Git Intelligence Summary

Recent history is planning/spike/merge churn on `main` (`Decision-making and ADRs`, `Merge branch 'spike/delivery-arch-6-6'`, `Work on technical spike`) — no in-flight code touches `EpicsViewBuilder.cs`, `SprintTemplater.RenderBoard`, or the `.sprint-lane`/empty-state CSS, so this change is additive and uncontended against siblings 8.2/8.3/8.5 (they touch `StatusStyles`/`ProjectCounts`/`BmadCommands`), with only the light 8.4 adjacency noted above (same methods, non-overlapping hunks). **Heed the worktree rule:** if this runs in a worktree, edit files at the **worktree path** — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [memory: [[worktree-edits-must-target-worktree-path]]]

---

## Latest Technical Information

No external libraries or APIs are introduced — pure in-repo C# string-building over existing models + CSS — so there is no version/security research to fold in. Discipline note: reuse `epic.Stories.Where(s => s.ArtifactOutputPath is null)` as the single undrafted predicate (it is exactly what `BuildStoryCard`'s `!hasArtifact` and `ForEpic`'s `nextUndetailed` already use), so the banner, the plain-note flip, and the "next id" command can never disagree about which stories count as undrafted.

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR coverage: [Source: [epics.md:1165-1169](../planning-artifacts/epics.md:1165)]
- Story 8.6 user story + both ACs: [Source: [epics.md:1262-1280](../planning-artifacts/epics.md:1262)]
- UX-DR9 (Now & Next cards as full-surface links with explicit empty states), NFR8 (framework-agnostic, adapter-supplied guidance): [Source: [epics.md:121](../planning-artifacts/epics.md:121); [epics.md](../planning-artifacts/epics.md)]
- The UX review that seeded Epics 8–10: [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)]
- Architecture invariants (framework-agnostic/NFR8, single-source, truthfulness, accessibility, deterministic, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]
- Section-view-model / status-token / pure-render / now-and-next-board / dashed-placeholder / golden-fingerprint / output-dir / worktree / visual-intent discipline: project memory ([[story-6-2-section-view-models-live]]; [[specscribe-status-token-system]]; [[charting-is-pure-svg-no-js]]; [[now-and-next-is-the-sprint-board]]; [[funnel-is-sideways-conventional-silhouettes]]; [[golden-diff-normalization-gotchas]]; [[generate-output-dir-is-specscribeoutput]]; [[worktree-edits-must-target-worktree-path]]; [[create-story-elicit-visual-intent]]).

---

## Tasks / Subtasks

- [ ] **Task 1 — Undrafted-story banner as an opaque fragment (AC: #1)**
  - [ ] Add `UndraftedBannerHtml` (`required string`) to `EpicPageView` in `EpicsView.cs`, parallel to the existing opaque fragments.
  - [ ] In `EpicsViewBuilder.BuildEpic`, compute `undrafted = epic.Stories.Where(s => s.ArtifactOutputPath is null).ToList()`; when `Count >= 2`, set `UndraftedBannerHtml = RenderUndraftedBanner(epic, undrafted, commands)` and pass `consolidated: true` into `BuildStoryCard`; else empty string + `consolidated: false`.
  - [ ] Add `RenderUndraftedBanner`: count sentence + one `create-story {undrafted[0].Id}` command via `BmadCommands.InlineGuidance`, wrapped in `<div class="epic-undrafted-banner">`.
  - [ ] Extend `BuildStoryCard` with a `bool consolidated` param: undrafted + consolidated → plain `No detailed story plan yet.` note; undrafted + not consolidated → today's inline-command note.
  - [ ] Emit `view.UndraftedBannerHtml` in `HtmlRenderAdapter.RenderEpicBody` after the retro affordance, before the story-card loop (not a TOC entry).
- [ ] **Task 2 — Empty sprint-board lane placeholder (AC: #2)**
  - [ ] In `SprintTemplater.RenderBoard`, add the per-column copy table (keyed on `cssClass`, next to `BoardColumns`); when `col.Count == 0`, append one `<div class="sprint-lane-empty">{copy}</div>` inside `.sprint-cards`.
  - [ ] Leave `RenderBoardByEpic` and non-empty columns untouched; confirm the change flows to both the sprint page and the home board (shared renderer).
- [ ] **Task 3 — CSS + stylesheet assertions (AC: #1, #2)**
  - [ ] Add `.epic-undrafted-banner` (drafted-token accent, designed banner) near `.pending-note`/`.empty-state`; add `.sprint-lane-empty` (dashed, muted, card-shaped) near `.sprint-card`/`.sprint-cards`. Tokens only, no hex.
  - [ ] Add `StylesheetTests` assertions for both classes.
- [ ] **Task 4 — Tests (AC: #1, #2)**
  - [ ] `SiteGeneratorStoryEpicPagesTests`: add a 2+-undrafted fixture (one banner + single command + plain card notes); confirm the existing single-undrafted test still passes (no banner, inline note kept).
  - [ ] `SprintTemplaterTests`: empty column → one `.sprint-lane-empty` with column copy; populated column → none; caps/lane assertions still pass; shared-surface (page vs. home) parity.
  - [ ] NFR8 degradation: catalog without `create-story` → banner count-only, plain card notes.
  - [ ] Confirm `RenderSectionParity` facts unchanged; update `SectionViewModelSerialization` fixture for the new field; regenerate `GoldenContentFingerprint` after confirming the byte diff is banners + plain notes + empty lanes only.
- [ ] **Task 5 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green; real generation to `SpecScribeOutput/`; eyeball an all-backlog epic page (one banner + plain notes) and the sprint page (dashed empty-lane placeholders with guidance copy).

## Dev Notes

- **The sharp edge is scope + fidelity, not difficulty.** Every change is a small string/CSS edit, but three disciplines constrain it: the banner is an *opaque fragment* built in the builder (not the adapter), the undrafted predicate must be the *same* `ArtifactOutputPath is null` the cards use, and the threshold is *2+* (a lone undrafted story is untouched). Get those three right and the golden diff is exactly banners + plain notes + empty lanes.
- **Consolidation removes clutter, not information.** Every undrafted card still says it has no plan; the one banner command is the honest single next move (`create-story` is one-at-a-time). Don't strip the per-card note entirely — keep the plain text, move only the command up to the banner.
- **The empty state is text-first.** The dashed border is reinforcement; the guidance copy carries the meaning, and the lane's `0` count still shows. Don't make emptiness border-only, and don't hide the zero.
- **`RenderBoard` is shared** by the sprint page and the home Now & Next board — the empty-lane placeholder appears on both by design; keep it compact so it doesn't bloat the home panel. [memory: [[now-and-next-is-the-sprint-board]]]
- **Byte parity moves on purpose** across every multi-undrafted epic page (this repo has several: Epics 11–16) and any empty board lane — verify the diff is *only* those before regenerating the constant. [memory: [[golden-diff-normalization-gotchas]]]
- **Opaque fragment stays opaque.** `UndraftedBannerHtml` mirrors the existing `NextActionsPanelHtml`/`RetroAffordanceHtml` fields exactly — a `required string` set in the builder, emitted verbatim by the adapter, round-tripped as a plain string in the serialization test. Keep all logic in `EpicsViewBuilder`. [memory: [[story-6-2-section-view-models-live]]]
- **Scope guard for later 8.x:** one-view-per-dataset (8.7) and recency signals (8.8) sit near the dashboard but are NOT this story. 8.6 is the two empty-state surfaces — the per-epic undrafted banner and the empty board lane.

### Project Structure Notes

- All change concentrates in `EpicsViewBuilder.cs` + `EpicsView.cs` + `HtmlRenderAdapter.Epics.cs` + `SprintTemplater.cs` + `specscribe.css` plus tests. No new page, no new adapter contract, no package restructure. One new opaque-fragment field on `EpicPageView` (shape-parallel to its siblings) and one new parameter on the internal `BuildStoryCard`.
- The section view-model split (Story 6.2) stays intact: the banner is built by the builder and emitted opaquely by the adapter; the per-card note change is inside the already-opaque `StoryCardView.NoteHtml`; section FACTS don't move.

### References

- [Source: [epics.md:1262-1280](../planning-artifacts/epics.md:1262)] — Story 8.6 user story + both ACs.
- [Source: [epics.md:1165-1169](../planning-artifacts/epics.md:1165)] — Epic 8 goal; FRs; UX-DR21–24; NFR8.
- [Source: [epics.md:121](../planning-artifacts/epics.md:121)] — UX-DR9 (Now & Next full-surface links with explicit empty states).
- [Source: [EpicsViewBuilder.cs:37-101](../../src/SpecScribe/EpicsViewBuilder.cs:37)] — `BuildEpic` + `BuildStoryCard` (banner build + consolidation flag).
- [Source: [EpicsViewBuilder.cs:167-217](../../src/SpecScribe/EpicsViewBuilder.cs:167)] — the existing named-opaque-fragment pattern the banner follows.
- [Source: [EpicsView.cs:110-142](../../src/SpecScribe/EpicsView.cs:110)] — `EpicPageView` opaque fragments (add `UndraftedBannerHtml` here).
- [Source: [HtmlRenderAdapter.Epics.cs:196-202](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:196)] — `RenderEpicBody` (emit the banner before the story cards).
- [Source: [HtmlRenderAdapter.Epics.cs:211-250](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211)] — `AppendStoryCard` / `.not-detailed-note` (the per-card note wrapper).
- [Source: [SprintTemplater.cs:103-135](../../src/SpecScribe/SprintTemplater.cs:103)] — `RenderBoard` (the empty-lane branch to add).
- [Source: [SprintTemplater.cs:27-34](../../src/SpecScribe/SprintTemplater.cs:27)] — `BoardColumns` (keep the empty-copy table beside it).
- [Source: [BmadCommands.cs:186-196](../../src/SpecScribe/BmadCommands.cs:186)] — `InlineGuidance` (reuse for the banner command; NFR8 degradation).
- [Source: [ModuleContext.cs:43-51](../../src/SpecScribe/ModuleContext.cs:43)] — `CommandCatalog.Command` (module-correct slash command or null).
- [Source: [assets/specscribe.css:2209-2219](../../src/SpecScribe/assets/specscribe.css:2209), [2977-3052](../../src/SpecScribe/assets/specscribe.css:2977), [1589](../../src/SpecScribe/assets/specscribe.css:1589)] — empty-state / sprint-lane / dashed-placeholder CSS seams.
- [Source: [SprintTemplaterTests.cs:141-167](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs:141)] — `RenderBoard` test patterns to extend.
- [Source: [SiteGeneratorStoryEpicPagesTests.cs:150-158](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs:150)] — the single-undrafted `.not-detailed-note` assertion (must still pass).
- [Source: [SiteGeneratorAdapterTests.cs:160](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:160)] — `GoldenContentFingerprint` (regenerate after the byte diff is confirmed).
- [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)] — framework-agnostic/NFR8, single-source, truthfulness, accessibility, deterministic, seed-not-invariant.
- [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)] — the UX review that seeded Epics 8–10.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
