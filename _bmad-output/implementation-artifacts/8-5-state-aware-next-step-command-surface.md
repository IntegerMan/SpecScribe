# Story 8.5: State-Aware Next-Step Command Surface

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer selecting work,
I want the portal to recommend one primary command per lifecycle state plus applicable unhappy-path actions,
so that I copy the right command without hunting.

## Acceptance Criteria

1.
**Given** a story in any lifecycle state
**When** its next-step commands render
**Then** exactly one primary recommended command shows, plus applicable alternate/unhappy-path actions (for example correct-course mid-sprint, retro on done)
**And** commands inapplicable to the state never render — a done story no longer surfaces code-review as the next step. [Source: epics.md#Story 8.5; UX-DR22]

2.
**Given** the command surface is adapter-supplied data (NFR8)
**When** a framework lacks a command workflow
**Then** the next-step section degrades to absent rather than showing wrong or empty commands
**And** each surfaced command carries a one-line caption explaining what it does. [Source: epics.md#Story 8.5; NFR8]

3.
**Given** existing next-steps specs (spec-hide-code-review-button-ready-for-dev, spec-story-next-steps-review-command, spec-home-next-steps-label-and-code-review)
**When** this story is implemented
**Then** it audits and extends that shipped behavior rather than duplicating it. [Source: epics.md#Story 8.5]

---

## Developer Context

**This is a presentation + command-routing story, not a new page or a data-model change.** The "Next Steps" panels already exist, are already state-aware, and already degrade when a command isn't installed. Story 8.5 does three things on top of that shipped foundation:

1. **Adds a visual hierarchy** — today every suggestion in a panel renders as an equal-weight `<li>`. This story makes exactly **one** command the emphasized *primary* and demotes the rest into a labeled *Other actions* group (AC #1).
2. **Adds the missing unhappy-path actions** — chiefly `correct-course` mid-sprint, and a muted "whoops, this is broken" escape hatch on the celebratory done panel (AC #1's "for example correct-course mid-sprint, retro on done").
3. **Audits + extends the three shipped next-steps specs** rather than re-implementing them (AC #3) — the ready-branch code-review suppression, the story-scoped code-review id, and the bare `Next Steps` heading all stay exactly as shipped.

**Almost the entire change lives in one file: [`src/SpecScribe/BmadCommands.cs`](../../src/SpecScribe/BmadCommands.cs)** — the state→command logic (`ForStory`/`ForEpic`/`ForProject`) plus the shared panel renderer (`RenderInner` / `RenderAllDonePanel`). Plus [`specscribe.css`](../../src/SpecScribe/assets/specscribe.css) for the primary/alternate styling and tests. No view-model field is added: the Next Steps panels are carried as a **pre-rendered opaque HTML fragment** (`NextStepsHtml`) built by [`EpicsViewBuilder`](../../src/SpecScribe/EpicsViewBuilder.cs:63,131) — a "named opaque fragment" under Story 6.2's section split — so nothing changes in the adapter, the view-model contract, or the section-fact parity harness. [memory: [[story-6-2-section-view-models-live]]]

It is a direct fix for a live UX-review finding graded against journeys 1–2 (the daily "pick the next unit of work" pulse):

> The Next Steps panel lists several commands at equal weight, so a reader must reason about which one actually applies to the current state — and mid-sprint recovery moves (correct-course) aren't offered at all. Surface **one** recommended command per state, demote the rest, and add the unhappy-path actions. [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md); UX-DR22]

It sits alongside its Epic 8 siblings but touches a **different seam** than any of them: **8.2** owns status *words/colors* + the status legend (`StatusStyles`); **8.3** owns *counts* (`ProjectCounts`); **8.4** owns progress+state *pairing* (badges) + sprint-board tooltips. 8.5 owns the **command surface** (`BmadCommands`). No file overlap with 8.2/8.3/8.4 — this change is uncontended against them.

### Owner-selected design decisions (visual intent elicited at create-story — do not re-litigate)

**1. Silhouette → "Hero primary + quiet alternates" (AC #1).** The one recommended command renders as the standard `cmd-badge` with an **accent/emphasis treatment** (the primary). Alternate and unhappy-path actions render **below it in a demoted, labeled group** (`Other actions`) with lighter/muted styling — visible on the page at rest, but clearly secondary. This is the lowest-risk hierarchy that keeps the existing `cmd-badge` + copy/send-menu intact for every command; the only new CSS is the primary emphasis + the alternates group. **Do NOT** use the `<details>` disclosure pattern ([`RenderCommandMenu`](../../src/SpecScribe/BmadCommands.cs:87)) to hide the alternates — the owner picked the always-visible demoted group, not a click-to-expand popout. **Do NOT** merge the primary and alternates into one flat list. [Owner decision, this story]

**2. Done state → stays celebratory + one MUTED escape hatch (AC #1's "retro on done").** A `done` story keeps its celebratory "All done" panel ([`RenderAllDonePanel`](../../src/SpecScribe/BmadCommands.cs:50)) — retrospective is a **sprint/epic-level** move in BMad, not a story-level one, so a finished story is **never** given a primary command and **never** re-nagged to code-review (preserves [spec-hide-code-review-button-ready-for-dev] and spec-sunburst-retro). But per the owner: *"unless a Quick Fix or Correct Course type action makes sense for 'whoops, this is broken' — but that shouldn't be a primary type of thing, more of a secondary muted option."* So the all-done panel gains a **single muted secondary action** (`correct-course`) under an `Other actions` label for the rare re-open case — rendered in the same demoted styling as decision 1's alternates, never emphasized. If the module doesn't expose that command, the panel degrades to **purely celebratory** (no `Other actions` group at all). [Owner decision, this story]

### The rendering model (this is the crux — read carefully)

The shared panel renderer [`RenderInner`](../../src/SpecScribe/BmadCommands.cs:62) receives a `List<Suggestion>` that each `For*` method already builds **in priority order (most-recommended first)**, and [`Add`](../../src/SpecScribe/BmadCommands.cs:200) already drops any command the module doesn't expose. So the robust "exactly one primary" rule is simply:

> **`suggestions[0]` (the first *surviving* suggestion) is the primary; `suggestions[1..]` are the alternates.**

Because `Add` null-drops *before* the list is rendered, this automatically handles NFR8's degradation edge case: **if the intended primary command isn't installed, the next surviving suggestion becomes the primary** — you never render an empty `Other actions` group with no hero, and you never print a command that doesn't exist. An empty list still yields an empty panel (unchanged — [`RenderInner` returns `""`](../../src/SpecScribe/BmadCommands.cs:64)). **Keep each `For*` method's existing ordering** (they already lead with the most immediate move); do not reorder to change which command is primary unless the state matrix below says so.

Do **not** bake an `IsPrimary` bool at `Add`-time — the intended primary might be null-dropped, so primacy must be decided at render time from the surviving list, not at build time. `RenderInner` is the single place that knows "index 0 = primary".

### State → command matrix (STORY panel — `ForStory`, the AC's primary subject)

| Status | Primary (emphasized) | Alternates (`Other actions`, demoted) | Notes |
|---|---|---|---|
| `ready-for-dev` | `dev-story {id}` | *(none)* | **Unchanged.** No code-review (nothing built yet — [spec-hide-code-review-button-ready-for-dev]). Single command → renders as primary only, no `Other actions` group. |
| `in-progress` | `dev-story {id}` | `code-review {id}`, **`correct-course` (NEW)** | dev-story stays primary (resume implementation); code-review keeps its existing "review the work so far" caption; add `correct-course` for "re-plan mid-sprint if scope shifted / something's blocking." |
| `review` | `code-review {id}` | **`correct-course` (NEW, optional)** | Primary stays code-review (final adversarial pass). Add one muted `correct-course` alternate for "if review surfaces a scope problem, re-plan before re-review." Keep it lean — no create-story, no retrospective ([spec-story-next-steps-review-command] withholds those). |
| `done` | *(celebratory panel — no primary command)* | **`correct-course` (NEW, muted escape hatch)** | Decision 2. Celebratory `RenderAllDonePanel` + one muted `correct-course` "re-open if this needs rework." Never code-review, never a primary. |
| *unrecognized / no plan* | `create-story {id}` | `check-implementation-readiness` (`.1` stories only) | **Re-order to primary=create-story** (the action that advances *this* story) with the readiness check demoted as a preparatory alternate for an epic's first story. Today the order is readiness-then-create-story; flip so the story-advancing command is the hero. Keep the story's *own* id on create-story ([spec-story-next-steps-review-command] fall-through). |

Every command above is fetched through `commands.Command(step, id?)` and appended via `Add`, so each drops cleanly when the module lacks it (NFR8). Each carries a one-line description (AC #2) — the captions already exist for the shipped commands; write concise ones for the new `correct-course` uses.

### Same treatment on the EPIC and PROJECT panels (shared renderer — apply consistently)

`RenderInner` is shared across the story, epic, and home/project panels ([`ForEpic`](../../src/SpecScribe/BmadCommands.cs:266), [`ForProject`](../../src/SpecScribe/BmadCommands.cs:331)). Since the owner picked a **panel-level** silhouette, the primary/alternate visual language applies to all three automatically — and it should, so the portal's command surfaces read as one system. The semantics already line up:

- **`ForProject`** already stacks its suggestions "in the order a developer would reason through them" (awaiting-review → front-line dev-story → next create-story → next epic breakdown). The first surviving item becomes the primary; the rest become `Other actions`. **Keep the existing order** — do not reorder. The awaiting-review / front-line item is the intended hero.
- **`ForEpic`** already emits one primary move per epic state (retrospective, sprint-status+create-story, etc.). First = primary; any second (e.g. the "draft the next story" nudge in the `active` branch) demotes to an alternate.

You are **not** changing which commands `ForEpic`/`ForProject` emit or their order — only that `RenderInner` now renders index 0 as the hero and the rest as the demoted group. Confirm with a regression assertion that both still render their existing commands.

### AC #3 — audit of the three shipped specs (extend, do NOT duplicate)

Read these three `done` specs first; your change must leave their behavior intact and build *on* it:

- **[spec-hide-code-review-button-ready-for-dev.md](spec-hide-code-review-button-ready-for-dev.md)** — the `ready` branch of `ForStory` emits **no** code-review. **Preserve.** The `ready` primary stays `dev-story` only; do not re-introduce code-review there.
- **[spec-story-next-steps-review-command.md](spec-story-next-steps-review-command.md)** — a story page only acts on *this* story: code-review carries `story.Id`; the review/done branches dropped `create-story` (next story) and `retrospective`. **Preserve.** The alternates you add are `correct-course` (a this-story recovery move), **not** `create-story`/`retrospective` (those stay on `ForEpic`/`ForProject`). Keep `story.Id` on every code-review.
- **[spec-home-next-steps-label-and-code-review.md](spec-home-next-steps-label-and-code-review.md)** — the shared heading is exactly `Next Steps` (no `(BMad Method)` suffix); `ForProject` surfaces awaiting-review stories via `StatusStyles.ForStory(s) == "review"`. **Preserve both.** The heading text does not change; the awaiting-review item simply becomes `ForProject`'s primary in the new hierarchy.

Record in Completion Notes, per spec, that each behavior was audited and preserved.

### Scope boundaries (read carefully)

- **Do NOT change any status word, color, or the six lifecycle colors.** That's 8.2. You route commands and add emphasis styling through existing `--status-*` tokens/neutrals; you never reclassify a status. [memory: [[specscribe-status-token-system]]]
- **Do NOT recompute or restate any count.** That's 8.3/8.4. This story emits commands + captions only.
- **Do NOT re-introduce `create-story`/`retrospective` to the story panel** ([spec-story-next-steps-review-command] removed them on purpose) and **do NOT add code-review to the `ready` branch** ([spec-hide-code-review-button-ready-for-dev]).
- **Do NOT change the `Next Steps` heading** ([spec-home-next-steps-label-and-code-review]).
- **Do NOT use the `<details>` disclosure (`RenderCommandMenu`) for the alternates** — the owner picked the always-visible demoted group (decision 1).
- **Do NOT add a client-side script or NuGet package.** Pure C# string-building + CSS; the existing `cmd-badge` copy/send-menu JS is reused unchanged. [memory: [[charting-is-pure-svg-no-js]]]
- **Do NOT add a new page, view-model field, or adapter contract.** `NextStepsHtml` stays an opaque fragment built in `EpicsViewBuilder`. [memory: [[story-6-2-section-view-models-live]]]
- **Do NOT write back to any source.** Local-first, read-only invariant.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Split each panel into one primary + demoted alternates in `RenderInner`.** In [`RenderInner`](../../src/SpecScribe/BmadCommands.cs:62), render `suggestions[0]` as the emphasized primary `<li class="next-steps-primary">` (standard `RenderCommandBadge` + `.next-steps-desc`, plus the primary accent class). If `suggestions.Count > 1`, render the remaining suggestions under a demoted subgroup: a small label (`Other actions`) + the same command badges with muted styling (e.g. `<li class="next-steps-alt">…</li>` inside a `.next-steps-alternates` container). One primary always; the `Other actions` group appears only when at least one alternate survives. Keep the `<h3>Next Steps</h3>` heading and the `.next-steps-list` scaffold; you're adding structure/classes within it. Escape all description text via `PathUtil.Html` (already done).
- **Add `correct-course` as the mid-sprint / review unhappy-path alternate** in [`ForStory`](../../src/SpecScribe/BmadCommands.cs:214): in the `in-progress` branch, after code-review, `Add(suggestions, commands.Command("correct-course"), "Re-plan this story mid-sprint if scope shifted or something's blocking.")`; in the `review` branch add one `correct-course` alternate with a review-scoped caption. `correct-course` takes no story id (it's an interactive workflow) — pass no argument. It drops automatically if the module lacks it.
- **Re-order the unrecognized/no-plan fall-through** so `create-story {id}` is primary and `check-implementation-readiness` is the demoted alternate (`.1` stories only). Today ([`ForStory`:254-261](../../src/SpecScribe/BmadCommands.cs:254)) readiness is added first — move the `create-story` `Add` ahead of the readiness `Add` so index 0 is the story-advancing command. Keep the `.1`-only gate on readiness.
- **Give the done panel a muted escape hatch** in [`RenderAllDonePanel`](../../src/SpecScribe/BmadCommands.cs:50). Thread the `CommandCatalog` into it (from [`RenderNextSteps`](../../src/SpecScribe/BmadCommands.cs:30), which already holds `commands`). Keep the celebratory checkmark + "All done" message exactly as-is; below it, if `commands.Command("correct-course")` resolves, render **one** muted `Other actions` action (`correct-course`, caption e.g. "Re-open this story if it needs rework.") in the same demoted styling as the alternates. If it doesn't resolve, render the panel purely celebratory (today's exact output). Reuse `RenderCommandBadge` — do not hand-roll a second badge.
- **Route every new color/accent through `--status-*` tokens or existing neutrals** — the primary accent and the muted-alternate treatment must use existing tokens (e.g. an accent border like the `.next-steps.all-done` `border-left: 3px solid var(--status-done)` precedent, and `var(--ink-light)` for muted text), never literal hex. [memory: [[specscribe-status-token-system]]]
- **Keep every command routed through `commands.Command(step, id?)` + `Add`** so NFR8 degradation is automatic. Never hard-code a slash command. New commands (`correct-course`) obey the same `Add` null-drop discipline.
- **Keep the opaque-fragment seam.** All changes are inside `BmadCommands`; `EpicsViewBuilder` still calls `RenderNextSteps`/`RenderEpicNextSteps`/`RenderProjectNextSteps` and stores the result as `NextStepsHtml`. Do not push command logic into the builder or the adapter. [memory: [[story-6-2-section-view-models-live]]]

### DON'T

- **DON'T hide the alternates behind a `<details>` popout** — always-visible demoted group (owner decision 1). `RenderCommandMenu` is not the vehicle here.
- **DON'T give a `done` story a primary command or a code-review nudge** — celebratory + muted escape hatch only (owner decision 2; [spec-hide-code-review-button-ready-for-dev]; spec-sunburst-retro).
- **DON'T re-add `code-review` to `ready`, or `create-story`/`retrospective` to the story panel** — the three shipped specs removed those deliberately (AC #3).
- **DON'T change the `Next Steps` heading text** ([spec-home-next-steps-label-and-code-review]).
- **DON'T decide primacy at `Add`-time with a bool** — decide it at render time (`suggestions[0]`), so a null-dropped primary correctly promotes the next survivor (NFR8 edge case).
- **DON'T reorder `ForProject`/`ForEpic` suggestions** — their existing order already puts the intended primary first; only the *rendering* gains the hierarchy.
- **DON'T add JS or a NuGet package**, and don't recompute counts or reclassify statuses.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)]:

- **Framework-agnostic command surface (NFR8)** — the whole command layer is already adapter/catalog-driven: `CommandCatalog` is parsed from the detected module's `module-help.csv`, and `Command(step)` returns null for steps a module doesn't expose so the suggestion is dropped. This story adds commands *through the same seam* and relies on it for AC #2's "degrade to absent." No BMad-specific string is hard-coded; a GDS project shows `/gds-*` and a module without `correct-course` simply omits that alternate. [Source: [ModuleContext.cs:43-51](../../src/SpecScribe/ModuleContext.cs:43); [BmadCommands.cs:200-206](../../src/SpecScribe/BmadCommands.cs:200)]
- **Single source of truth** — "awaiting review" routes through `StatusStyles.ForStory(s) == "review"` (as `ForProject` already does); the done→celebratory decision routes through `StatusStyles.ForStory(story) == "done"` (as `RenderNextSteps` already does). Add no parallel status check. [memory: [[specscribe-status-token-system]]]
- **Truthfulness over convenience** — the hierarchy makes the *one* applicable command obvious without hiding the legitimate alternates; you never suppress a valid recovery move to look tidy, and you never surface a command that can't apply to the state (a done story's code-review). [Source: [StatusStyles.cs:3-5](../../src/SpecScribe/StatusStyles.cs)]
- **Accessibility is part of the rendering contract (NFR6, UX-DR17)** — the primary/alternate distinction is not color-only: the `Other actions` **label** carries the demotion in text, and every command stays a real, focusable `cmd-badge` button/anchor with its caption. The muted styling is a reinforcement, not the sole signal. No command becomes keyboard-unreachable.
- **Deterministic, generation-time-only output** — every command string derives solely from the parsed catalog + story status; a from-scratch regen of identical inputs is byte-identical. No per-visitor state.
- **Seed, not invariant** — no Core/Adapters package split; changes stay in `BmadCommands.cs` + `specscribe.css`. [Source: [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: [SpecScribe.Tests.csproj](../../tests/SpecScribe.Tests/SpecScribe.Tests.csproj)]
- **Reuse, don't reinvent (all already in-repo):**
  - [`BmadCommands.RenderCommandBadge`](../../src/SpecScribe/BmadCommands.cs:152) — the unified command badge (command text + Copy + Cursor send-menu). Reuse for BOTH primary and alternates and the done escape hatch; do not build a second badge shape.
  - [`BmadCommands.Add`](../../src/SpecScribe/BmadCommands.cs:200) — the null-dropping appender that enforces "never print an uninstalled command." Every new command goes through it.
  - [`CommandCatalog.Command(step, argument?)`](../../src/SpecScribe/ModuleContext.cs:43) — the module-correct slash command (or null). `correct-course` is a known step in `module-help.csv` (`bmad-correct-course`); `quick-dev` (`bmad-quick-dev`) is also present if a lighter "quick fix" verb reads better for the done panel — the owner named "Quick Fix or Correct Course," so either is acceptable there; prefer `correct-course` for the mid-sprint alternates.
  - [`StatusStyles.ForStory`](../../src/SpecScribe/StatusStyles.cs:16) — the single source of truth for a story's lifecycle stage (drives the `done` branch and `ForProject`'s awaiting-review).
  - [`Icons.ForStatus`](../../src/SpecScribe/BmadCommands.cs:52) — the shared done glyph already used by the all-done panel; leave it in place.
  - [`PathUtil.Html`](../../src/SpecScribe/PathUtil.cs) — escape all description/label text.
  - [specscribe.css](../../src/SpecScribe/assets/specscribe.css) `.next-steps` / `.next-steps-list` / `.next-steps-desc` / `.next-steps.all-done` ([:1292-1304](../../src/SpecScribe/assets/specscribe.css:1292), [:2525-2527](../../src/SpecScribe/assets/specscribe.css:2525)) — the panel styles to extend for the primary accent + `.next-steps-alternates` muted group.

---

## File Structure Requirements

**No new production classes.** One likely change to the `Suggestion` flow (primacy decided in `RenderInner`) and thread `CommandCatalog` into `RenderAllDonePanel`. No new files.

**Modified files (read fully before editing):**

- [`src/SpecScribe/BmadCommands.cs`](../../src/SpecScribe/BmadCommands.cs) — the whole change:
  - `RenderInner` ([:62](../../src/SpecScribe/BmadCommands.cs:62)) — render `suggestions[0]` as `.next-steps-primary`, the rest under a labeled `.next-steps-alternates` `Other actions` group.
  - `RenderAllDonePanel` ([:50](../../src/SpecScribe/BmadCommands.cs:50)) — accept `CommandCatalog`, append one muted `correct-course` action when exposed (keep the celebration).
  - `RenderNextSteps` ([:30](../../src/SpecScribe/BmadCommands.cs:30)) — pass `commands` into `RenderAllDonePanel`.
  - `ForStory` ([:214](../../src/SpecScribe/BmadCommands.cs:214)) — add `correct-course` alternates to `in-progress` and `review`; re-order the no-plan fall-through so `create-story` is primary and `check-implementation-readiness` is the alternate.
  - Update the `ForStory` doc comment to describe the primary/alternate model.
- [`src/SpecScribe/assets/specscribe.css`](../../src/SpecScribe/assets/specscribe.css) — add `.next-steps-primary` (accent emphasis, mirroring the `.next-steps.all-done` `border-left` token precedent), `.next-steps-alternates` (the demoted group + its `Other actions` label), and `.next-steps-alt` (muted rows). Route colors through `--status-*`/`--ink-light`. Place near the existing `.next-steps*` rules ([:1292-1304](../../src/SpecScribe/assets/specscribe.css:1292), [:2525-2527](../../src/SpecScribe/assets/specscribe.css:2525)). **`StylesheetTests` asserts on stylesheet content — add companion assertions for any new class.**

**Tests to update / add:**

- [`tests/SpecScribe.Tests/ModuleContextTests.cs`](../../tests/SpecScribe.Tests/ModuleContextTests.cs) (holds `BmadCommandsTests`, [:112](../../tests/SpecScribe.Tests/ModuleContextTests.cs:112)) — the core coverage. The existing `Story(id, status)` and `BmmCatalog` helpers make these one-liners; add a catalog variant that includes `correct-course` (and one that omits it) for the degradation cases.
- [`tests/SpecScribe.Tests/StylesheetTests.cs`](../../tests/SpecScribe.Tests/StylesheetTests.cs) — assert `.next-steps-primary` and `.next-steps-alternates` (+ `.next-steps-alt`) ship in the embedded stylesheet.
- **Golden fingerprint:** [`SiteGeneratorAdapterTests.GoldenContentFingerprint_...`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) **WILL change** (every Next Steps panel — home, epic, story — gains the primary/alternate structure). Regenerate the constant per the drill below and confirm every diff is a Next Steps panel, nothing else. [memory: [[golden-diff-normalization-gotchas]]]
- [`tests/SpecScribe.Tests/RenderSectionParityTests.cs`](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) / [`RenderParityTests.cs`](../../tests/SpecScribe.Tests/RenderParityTests.cs) — the `NextStepsHtml` fragment is opaque; section FACTS (id/status/task tally) are unchanged, so parity should hold. Update only if a fact genuinely shifted (it won't).

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). The command logic is pure string-building over `StoryInfo`/`EpicInfo`/`EpicsModel` + `CommandCatalog` — unit-test directly against `BmadCommands.Render*`, exactly like the existing `BmadCommandsTests`.

Cover explicitly:

- **Exactly one primary (AC #1):** every non-empty panel (story `ready`/`in-progress`/`review`, epic, project) renders **exactly one** `.next-steps-primary`. A single-suggestion panel (`ready` story) renders the primary with **no** `Other actions` group.
- **Primary identity per state (AC #1):** `in-progress` → primary is `/bmad-dev-story {id}`, alternates contain `/bmad-code-review {id}` and `/bmad-correct-course`; `review` → primary is `/bmad-code-review {id}`, alternate contains `/bmad-correct-course`; no-plan story → primary is `/bmad-create-story {id}`, `.1` alternate contains `/bmad-check-implementation-readiness`.
- **Done state (AC #1, owner decision 2):** `done` story → celebratory `next-steps all-done` panel + "All done" + the done glyph (unchanged), PLUS a muted `Other actions` `/bmad-correct-course` when the catalog exposes it; **never** `/bmad-code-review`; **no** primary command. With a catalog lacking `correct-course` → the panel is byte-identical to today's celebratory output (no `Other actions` group).
- **NFR8 degradation (AC #2):** `CommandCatalog.Empty` → empty string (unchanged). A catalog missing the intended primary but exposing an alternate → the alternate is promoted to `.next-steps-primary` (no empty `Other actions`, no missing hero). New `correct-course` alternates drop silently when absent.
- **Captions (AC #2):** every rendered command (primary and each alternate, incl. the done escape hatch) carries a `.next-steps-desc` one-liner.
- **Shipped-spec regression (AC #3):** `ready` story → still no `/bmad-code-review`; `review` story → still no `/bmad-create-story` / `/bmad-retrospective`; heading is still exactly `Next Steps` with no `(BMad Method)`. `ForProject` awaiting-review + `ForEpic` retrospective/sprint-status commands still render (now as the primary).
- **Determinism:** two generations over identical input produce identical output.

**Run:** `dotnet test` from repo root. Then a full generation against this repo: `dotnet run --project src/SpecScribe` (output → `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; vestigial/gitignored). Eyeball: on a story page in `in-progress`, the panel shows `dev-story` emphasized with `code-review` + `correct-course` demoted under `Other actions`; a `done` story shows the celebration plus a muted `correct-course`; the home page shows one hero command + demoted rest. [memory: [[generate-output-dir-is-specscribeoutput]]]

**Golden-diff drill (rendered bytes change here — expect a fingerprint update):** freeze a fixture copy of `_bmad-output` + `docs/adrs` + `README.md` + `_bmad` in scratchpad, `git init` with fixed-date commits (+`--deep-git`), generate before/after, apply the 5 volatile-token normalizations, and confirm the ONLY diffs are Next Steps panels (primary/alternate structure + the new `correct-course` rows). Then regenerate the `GoldenContentFingerprint` constant (the test prints the new hash). Run twice for portability. [memory: [[golden-diff-normalization-gotchas]]]

---

## Previous Story Intelligence

**Story 8.4 (Paired Progress & Readiness — `ready-for-dev`, sibling)** pairs badges + adds sprint-board column tooltips in `HtmlRenderAdapter.Epics`/`SprintTemplater`/`Charts` — a **different seam** than 8.5's `BmadCommands`. No overlap. It reaffirms the same disciplines: truthfulness over convenience, route colors through `--status-*` tokens, byte parity moves on purpose (regenerate the golden fingerprint), section FACTS stay green. [Source: [8-4-paired-progress-and-readiness-semantics.md](8-4-paired-progress-and-readiness-semantics.md)]

**Story 8.2 (Canonical Status Model — `ready-for-dev`, sibling)** owns status words/colors + the status legend + a possible `"unrecognized"` stage. 8.5 reads `StatusStyles.ForStory` but never changes a status word; if 8.2 adds an `"unrecognized"` stage, `ForStory`'s status branches already fall through to the no-plan/create-story path, which is safe. [Source: [8-2-canonical-status-model-with-portal-wide-legend.md](8-2-canonical-status-model-with-portal-wide-legend.md)]

**Story 6.2 (Section View Models — `review`)** established that `NextStepsHtml` is a **named opaque fragment** built in `EpicsViewBuilder` and carried through the view model — so a change to the command renderer needs no view-model or adapter change and doesn't move section FACTS. [Source: [[story-6-2-section-view-models-live]]]

**Three shipped one-shot specs (`done`) are the direct ancestors** — audit + extend them (AC #3), do not re-implement:
- [spec-hide-code-review-button-ready-for-dev.md](spec-hide-code-review-button-ready-for-dev.md) — `ready` emits no code-review.
- [spec-story-next-steps-review-command.md](spec-story-next-steps-review-command.md) — story panel acts only on *this* story; code-review carries `story.Id`; review/done dropped create-story/retro.
- [spec-home-next-steps-label-and-code-review.md](spec-home-next-steps-label-and-code-review.md) — bare `Next Steps` heading; `ForProject` surfaces awaiting-review.
- [spec-next-steps-send-command-split-button.md](spec-next-steps-send-command-split-button.md) — the `cmd-badge` Copy + Cursor send-menu you reuse verbatim for every command.

**Recurring lessons that apply here:**

- **Elicit visual intent up front** (Epic 3 retro, open action) — the new visual surface (primary/alternate hierarchy + the done escape hatch) was offered as named directions and the owner picked *hero-primary + quiet always-visible alternates* and *celebratory-done + muted secondary escape hatch*. Build those, not a re-invented silhouette (e.g. a `<details>` popout). [memory: [[create-story-elicit-visual-intent]]]
- **Split, don't absorb** — if this tempts you into 8.6's empty-state banners or restyling badges (8.2/8.4), stop; 8.5 is the command surface only. [Source: Epic 2/3 retros]

---

## Git Intelligence Summary

Recent history is planning/spike/merge churn on `main` (`Merge branch 'spike/vscode-6-3'`, `Planning and spikes`, `Review`) — no in-flight code touches `BmadCommands.cs` or the `.next-steps*` CSS, so this change is additive and uncontended against siblings 8.2/8.3/8.4 (they touch `StatusStyles`/`ProjectCounts`/`HtmlRenderAdapter.Epics`+`SprintTemplater`, not `BmadCommands`). **Heed the worktree rule:** if this runs in a worktree, edit files at the **worktree path** — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [memory: [[worktree-edits-must-target-worktree-path]]]

---

## Latest Technical Information

No external libraries or APIs are introduced — pure in-repo C# string-building over existing models + CSS — so there is no version/security research to fold in. Discipline note: keep the status comparison lowercase-invariant exactly as the current `ForStory` does (`story.Status?.Trim().ToLowerInvariant()`), and reuse `StatusStyles.ForStory` for the canonical stage rather than re-deriving from the raw string where a canonical stage is what you need.

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR coverage: [Source: [epics.md:1134-1138](../planning-artifacts/epics.md:1134)]
- Story 8.5 user story + all three ACs: [Source: [epics.md:1206-1229](../planning-artifacts/epics.md:1206)]
- UX-DR22 (state-aware next-step commands: one primary per state + unhappy-path actions), NFR8 (framework-agnostic, adapter-supplied command surface): [Source: [epics.md](../planning-artifacts/epics.md)]
- The three shipped next-steps specs this story audits + extends: [Source: [spec-hide-code-review-button-ready-for-dev.md](spec-hide-code-review-button-ready-for-dev.md); [spec-story-next-steps-review-command.md](spec-story-next-steps-review-command.md); [spec-home-next-steps-label-and-code-review.md](spec-home-next-steps-label-and-code-review.md)]
- The UX review that seeded Epics 8–10: [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)]
- Architecture invariants (framework-agnostic/NFR8, single-source, truthfulness, accessibility, deterministic, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]
- Status-token / pure-render / section-view-model / golden-fingerprint / output-dir / worktree / visual-intent discipline: project memory ([[specscribe-status-token-system]]; [[charting-is-pure-svg-no-js]]; [[story-6-2-section-view-models-live]]; [[golden-diff-normalization-gotchas]]; [[generate-output-dir-is-specscribeoutput]]; [[worktree-edits-must-target-worktree-path]]; [[create-story-elicit-visual-intent]]).

---

## Tasks / Subtasks

- [ ] **Task 1 — Primary/alternate hierarchy in the shared renderer (AC: #1)**
  - [ ] In `RenderInner`, render `suggestions[0]` as `.next-steps-primary` (emphasized) and, when `Count > 1`, the rest under a `.next-steps-alternates` group with an `Other actions` label + `.next-steps-alt` muted rows. Reuse `RenderCommandBadge` + `.next-steps-desc` for every command. Keep the `Next Steps` heading and the `.next-steps-list` scaffold.
  - [ ] Add `.next-steps-primary` / `.next-steps-alternates` / `.next-steps-alt` CSS via `--status-*`/`--ink-light` tokens (accent for primary, muted for alternates); no literal hex. Add `StylesheetTests` assertions.
- [ ] **Task 2 — Unhappy-path commands in `ForStory` (AC: #1, #2)**
  - [ ] `in-progress`: add `correct-course` alternate (after code-review) with a mid-sprint caption. `review`: add one `correct-course` alternate with a review-scoped caption. Both via `Add(commands.Command("correct-course"), …)` (no id).
  - [ ] Re-order the no-plan fall-through so `create-story {id}` is primary and `check-implementation-readiness` (`.1` only) is the demoted alternate.
  - [ ] Update the `ForStory` doc comment to describe the primary/alternate model.
- [ ] **Task 3 — Done-state muted escape hatch (AC: #1)**
  - [ ] Thread `CommandCatalog` into `RenderAllDonePanel` (via `RenderNextSteps`). Keep the celebratory checkmark + "All done"; append one muted `Other actions` `correct-course` action when exposed; render purely celebratory (today's bytes) when not. Never code-review, never primary.
- [ ] **Task 4 — Tests (AC: #1, #2, #3)**
  - [ ] `BmadCommandsTests`: exactly-one-primary per panel; primary identity per state; done celebratory + muted escape hatch (and pure-celebration when `correct-course` absent); NFR8 degradation incl. primary-promotion; every command has a caption; shipped-spec regressions (no code-review on `ready`, no create-story/retro on review, bare `Next Steps` heading, `ForProject`/`ForEpic` still render).
  - [ ] `StylesheetTests`: new classes present.
  - [ ] Confirm `RenderSectionParity` facts unchanged; regenerate `GoldenContentFingerprint` after confirming the byte diff is Next Steps panels only.
- [ ] **Task 5 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green; real generation to `SpecScribeOutput/`; eyeball an `in-progress` story (dev-story hero + code-review/correct-course demoted), a `done` story (celebration + muted correct-course), and the home panel (one hero + demoted rest).

## Dev Notes

### Cross-surface note from Story 8.1 (2026-07-14)

`NextStepsHtml` is an opaque fragment in `BodyHtml` → **shared-path** for the rendered primary/alternates panel on HTML, webview, and SPA. Two surface-specific gaps to plan for (not architecture blockers):

1. **Copy / send menu** uses `specscribe.js` (`data-copy`). Webview does not load that script — command text remains visible; click-to-copy will not work there unless you route through the existing host clipboard helper or the documented `stageCommand` seam.
2. **`stageCommand` extension point** is documented in `WebviewRenderAdapter` (comment currently mis-labels it “Story 8.4 / R4.3”; the owning story is **8.5** / native R4.3). Handler is intentionally unbuilt. Decide in this story whether to implement webview “stage in terminal” now or defer; either way, do not assume HTML copy UX covers VS Code.

CLI does not render Next Steps panels.

- **The sharp edge is scope + fidelity, not difficulty.** Every change is a small string/CSS edit in one file, but three shipped specs constrain it: don't re-add code-review to `ready`, don't re-add create-story/retro to the story panel, don't touch the `Next Steps` heading. Audit them (AC #3) and build *on* them.
- **Primacy is a render-time decision, not a build-time flag.** Because `Add` null-drops unavailable commands, `suggestions[0]` after building is always the correct, installed primary — which is exactly what makes NFR8's "primary not exposed → next survivor is primary" fall out for free. Do not pre-compute an `IsPrimary` bool.
- **Byte parity moves on purpose** across *every* Next Steps panel (home, epic, story) — a bigger golden-fingerprint diff than 8.4. Verify the diff is *only* next-steps structure + the new `correct-course` rows before regenerating the constant. [memory: [[golden-diff-normalization-gotchas]]]
- **Opaque fragment stays opaque.** `NextStepsHtml` is built in `EpicsViewBuilder` and rendered opaquely by the adapter — keep all logic in `BmadCommands`; section FACTS don't move. [memory: [[story-6-2-section-view-models-live]]]
- **Accessibility is text-first.** The `Other actions` label carries the demotion in words; muted color is reinforcement only. Keep every command a real focusable badge with a caption.
- **Scope guard for later 8.x/9.x:** empty states (8.6), one-view-per-dataset (8.7), recency (8.8), verification evidence strip (9.4) all sit near the dashboard but are NOT this story. 8.5 makes the command surface recommend one clear next move per state.

### Project Structure Notes

- All change concentrates in `BmadCommands.cs` + `specscribe.css` plus tests. No new page, no new view-model field (`NextStepsHtml` is unchanged in shape — an opaque fragment), no adapter contract, no package restructure. One method signature grows (`RenderAllDonePanel` takes a `CommandCatalog`).
- The section view-model split (Story 6.2) stays intact: the Next Steps fragment is built by the builder and rendered opaquely by the adapter; this story changes only the fragment's internal HTML.

### References

- [Source: [epics.md:1206-1229](../planning-artifacts/epics.md:1206)] — Story 8.5 user story + all three ACs.
- [Source: [epics.md:1134-1138](../planning-artifacts/epics.md:1134)] — Epic 8 goal; FRs FR20/FR21/FR25/FR31; UX-DR21–24; NFR8.
- [Source: [BmadCommands.cs:30-52](../../src/SpecScribe/BmadCommands.cs:30)] — `RenderNextSteps`/`RenderAllDonePanel` entry points (thread `CommandCatalog` into the done panel).
- [Source: [BmadCommands.cs:62-78](../../src/SpecScribe/BmadCommands.cs:62)] — `RenderInner` (the shared renderer to split into primary + alternates).
- [Source: [BmadCommands.cs:152-184](../../src/SpecScribe/BmadCommands.cs:152)] — `RenderCommandBadge` (reuse for every command).
- [Source: [BmadCommands.cs:200-264](../../src/SpecScribe/BmadCommands.cs:200)] — `Add` (null-drop) + `ForStory` (state matrix to extend).
- [Source: [BmadCommands.cs:266-387](../../src/SpecScribe/BmadCommands.cs:266)] — `ForEpic`/`ForProject` (shared renderer applies; keep order).
- [Source: [ModuleContext.cs:19-52](../../src/SpecScribe/ModuleContext.cs:19)] — `CommandCatalog.Command` (module-correct slash command or null; NFR8 seam).
- [Source: [StatusStyles.cs:16](../../src/SpecScribe/StatusStyles.cs:16)] — `ForStory` (single source of truth for lifecycle stage).
- [Source: [EpicsViewBuilder.cs:63,131](../../src/SpecScribe/EpicsViewBuilder.cs:63)] — where `NextStepsHtml` is built (opaque fragment).
- [Source: [assets/specscribe.css:1292-1304,2525-2527](../../src/SpecScribe/assets/specscribe.css:1292)] — `.next-steps*` styling seams.
- [Source: [ModuleContextTests.cs:112-200](../../tests/SpecScribe.Tests/ModuleContextTests.cs:112)] — `BmadCommandsTests` (existing helpers + assertions to extend).
- [Source: [spec-hide-code-review-button-ready-for-dev.md](spec-hide-code-review-button-ready-for-dev.md), [spec-story-next-steps-review-command.md](spec-story-next-steps-review-command.md), [spec-home-next-steps-label-and-code-review.md](spec-home-next-steps-label-and-code-review.md), [spec-next-steps-send-command-split-button.md](spec-next-steps-send-command-split-button.md)] — the shipped specs to audit + extend (AC #3).
- [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)] — framework-agnostic/NFR8, single-source, truthfulness, accessibility, deterministic, seed-not-invariant.
- [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)] — the UX review that seeded Epics 8–10.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
