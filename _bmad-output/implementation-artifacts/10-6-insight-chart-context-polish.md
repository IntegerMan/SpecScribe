# Story 10.6: Insight-Chart Context Polish

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tech lead interpreting analytics,
I want misleading chart contexts corrected — process-coupling distinguished from code-coupling, heatmap dead zones trimmed and marked, and single-contributor phrasing reworded,
so that I do not draw wrong conclusions from artifacts of the data.

## Context & Why This Story Exists

This is the code side of **UX-DR30** — the insight-chart *context* polish that Story 10.2 explicitly left out. 10.2 owns legends / time windows / "why this matters" framing; **this story owns three concrete misreadings** that survive even after charts are well-captioned:

| AC | Feedback item | Verbatim complaint | Type |
|---|---|---|---|
| **AC1** | [C3](docs/MissingFeatures.md:57-58) / [Deep Analytics](docs/Epic3UXFeedback.md:118) | _"`sprint-status.yaml ↔ specscribe.css` as the top coupled pair (29×) is an artifact of committing generated/status files with code; a note distinguishing process-coupling from code-coupling would preempt wrong conclusions."_ | NET-NEW |
| **AC2a** | [C4](docs/MissingFeatures.md:60-61) / [Git Insights](docs/Epic3UXFeedback.md:112) | _"The March–June dead zone renders as months of empty cells; annotate project start ('First commit Jul 4') or trim the window."_ | NET-NEW |
| **AC2b** | [F7](docs/MissingFeatures.md:108-109) / [T8](docs/Epic3UXFeedback.md:58-60) | _"'People to talk to about this file: Matt Eland' on every expanded file row is comic in a solo repo… Suppress the section when there's one contributor, or reword ('Sole contributor: …')."_ | NET-NEW (small) |

Serves Journey 6 (health & hotspots) — the adoption-deciding tech-lead path. Epic 10's spine: **framework-neutral, adapter-agnostic copy (NFR8)** — never name SpecScribe-specific files in the classifier or the note.

### What reads wrong today (read these before designing)

**AC1 — coupling has zero process/code awareness.** [`Charts.CouplingGraph`](src/SpecScribe/Charts.cs) / [`CouplingTable`](src/SpecScribe/Charts.cs) render every pair `GitMetrics.ParseNumstatLog` hands them. There is **no** path-kind classification anywhere in `src/` (confirmed). The only Deep Analytics call site is [`DeepAnalyticsTemplater.RenderPage`](src/SpecScribe/DeepAnalyticsTemplater.cs:44-77) — graph + `coupling-legend` + ranked-pairs table. The live example (`sprint-status.yaml ↔ specscribe.css`) is a *symptom*; the fix must generalize.

**AC2a — the dead zone is the young-repo branch of the 15-week floor.** In [`Charts.CommitHeatmap`](src/SpecScribe/Charts.cs:916-922):

```csharp
var minStart = end.AddDays(-7 * 15);
var start = firstCommit < minStart ? firstCommit : minStart; // young repo → pad with empty pre-project cells
```

When the project is **younger** than 15 weeks, `start = minStart` paints weeks/months of blank cells before the first commit. Old-repo behavior (`firstCommit < minStart` → start at `firstCommit`) is **out of scope** — preserve it.

**AC2b — plural phrasing is unconditional.** [`GitInsightsTemplater.AppendContributorPanel`](src/SpecScribe/GitInsightsTemplater.cs:176-202) has a zero-contributor branch, then always emits `"People to talk to about this file:"` for any non-empty list. The fixture already has a single-contributor file (`HtmlTemplater.cs` / Bob) that still gets the plural lead. Guard on **`file.TotalContributors <= 1`** (not the capped `Contributors.Count` alone — a truncated list can show 1 of N).

## Acceptance Criteria

**AC1 (Process-coupling distinguished from code-coupling, with an explanatory note; NFR8-generic)**
Given change-coupling analysis includes generated, status, config, or other non-source files,
When coupling views render (Deep Analytics graph + ranked-pairs table),
Then process-coupling is **distinguished** from code-coupling with an **explanatory note**,
And the classification **generalizes across repositories** — never hard-codes SpecScribe-specific paths like `sprint-status.yaml` / `specscribe.css` (NFR8).

**AC2 (Heatmap dead zone handled; single-contributor phrasing honest)**
Given an activity heatmap whose padded window predates the first commit,
When it renders,
Then the young-repo window is **trimmed** to roughly first-commit minus one week (not months of empty cells) **and** the first-commit moment is marked with a **text caption plus an accent line/box** (never color-only),
And single-contributor files **reword** multi-contributor phrasing ("People to talk to" → "Sole contributor: …").

## Design Direction — OWNER-LOCKED (2026-07-18)

**Polish bar:** Journey 6 showcase — annotations should feel designed (clear silhouette, intentional accent, scannable copy), not bolted-on captions. Every audience should leave smarter, not confused.

### AC1 — process vs code coupling — OWNER-LOCKED: annotate-in-place

Keep all pairs in the ranking; mark process pairs in the table + graph; add one explanatory note when any process pair is present. Honest data, no silent drop.

**What "process" means (coupling only — not "CSS isn't code in the portal"):**

Coupling answers "these two files tend to change in the same commits." Sometimes that is a real code dependency. Sometimes it is commit habit (status YAML + stylesheet co-committed every sprint). Marking a path as process for **coupling annotation** does **not** demote that file in the code map, code pages, or language treatment elsewhere.

| Kind | Meaning for coupling | Typical path patterns (never this-repo names) |
|---|---|---|
| **Code** | Application / library source | `.cs`, `.ts`, `.tsx`, `.js`, `.py`, `.go`, `.rs`, `.java`, … |
| **Process** | Config, status, lockfiles, build output, co-committed project assets | `.yml`/`.yaml`, `.json`, `.toml`, `.lock`, lockfile basenames, `bin/`/`obj/`/`dist/`/`node_modules/`, plus **stylesheet extensions** (`.css`/`.scss`/`.less`) — the live symptom class |

A **pair** is process-coupling when **either** path is process. Two code files stay unmarked. Ambiguous → **code** (false negative cheaper than hiding a real dependency).

- **Classifier:** shared pure `GitMetrics.IsProcessPath` + `ClassifyCoupling` — one definition for renderers + tests.
- **NFR8:** pattern/extension only — never hard-code SpecScribe filenames.
- **Rendering (shine):** Kind badge with visible **"Process"** text; dashed process edges + `<title>`; one metric-generic note when any process pair is present. Never color-only.
- **Related (NOT this story):** Code Map **file-type** colorize with a **discrete** (categorical) palette → **Story 7.9**. Large `specscribe.css` maintainability investigation → **Story 17.5**.

### AC2a — heatmap dead zone — OWNER-LOCKED: trim (~1 week lead) + accent annotation

| Piece | Spec |
|---|---|
| **Trim** | Young-repo: start ≈ `firstCommit − ~1 week` (then week-snap), **not** the full 15-week pad. Old-repo branch unchanged. |
| **Caption** | `"First commit {DReadable(firstCommit)}"` inside `CommitHeatmap` (Git Pulse + Git Insights). |
| **Visual accent** | SVG **vertical line or thin box** at the first-commit week column, distinct accent token (`--gold` / `--rust` — **not** a new `--status-*`), **plus** the caption text (never color-only). |

### AC2b — sole-contributor — OWNER-LOCKED: reword + light hub soften

- File panel: `TotalContributors <= 1` → **"Sole contributor:"**. Zero unchanged; multi stays "People to talk to…".
- Hub-wide when `ContributorCount == 1`: soften section lead + unselected prompt.
- Code-page contributor lists — **out of scope**.

### Shine / audience bar (owner mandate)

1. Teach without jargon — readable by a non-BMAD stakeholder.
2. Look finished — accent + caption + badges compose with existing chart chrome.
3. Stay honest — never drop pairs, never invent dates, never color-only.
4. Reach every surface that shows the chart (dashboard / Git Insights / Deep Analytics; webview + SPA).

### Coordination with Story 10.2

If `ChartMeta`/`Framed` exist at implement time, route panel-level notes through that frame. Otherwise place the process note beside `coupling-legend`. Heatmap caption + accent live inside `CommitHeatmap` either way.

## Tasks / Subtasks

- [ ] **Task 1 — Process-path classifier (shared, NFR8)** (AC: 1)
  - [ ] Add `IsProcessPath` / `ClassifyCoupling` in `GitMetrics.cs`. Stylesheets count as process **for coupling only**. No SpecScribe path literals.
  - [ ] Unit-test matrix: source↔source → code; yaml↔css → process; `src/A.cs`↔`package-lock.json` → process; ambiguous → code.

- [ ] **Task 2 — Annotate coupling views (designed)** (AC: 1)
  - [ ] `CouplingTable` + `CouplingGraph`: Kind badge + dashed process edges + `<title>`; never color-only.
  - [ ] `DeepAnalyticsTemplater`: one explanatory note when any process pair present; preserve lightbox + a11y twin.
  - [ ] CSS: intentional chrome; no new `--status-*` token.

- [ ] **Task 3 — Heatmap trim (~1 week lead) + accent first-commit mark** (AC: 2a)
  - [ ] Young-repo start ≈ `firstCommit − 7 days` then week-snap; old-repo unchanged.
  - [ ] Caption + SVG accent line/box at first-commit week; works with `showHeadline` true/false.
  - [ ] Preserve future-day muting, `LinkedCommitDays`, `HeatLevel`, `:target`, aria-label, `--col` stagger. Frozen heatmap contrast spec — extend only.

- [ ] **Task 4 — Sole-contributor phrasing** (AC: 2b)
  - [ ] `TotalContributors <= 1` → "Sole contributor:"; soften hub lead/prompt when `ContributorCount == 1`.
  - [ ] Update `GitInsightsTemplaterTests` accordingly.

- [ ] **Task 5 — Tests + golden** (AC: 1, 2)
  - [ ] `ChartsTests`: young-repo window + caption + accent; old-repo still passes.
  - [ ] `DeepAnalyticsTemplaterTests` + classifier matrix + `StylesheetTests`.
  - [ ] Golden regen on dashboard / git-insights / deep-analytics; baseline green first; RenderParity green.

- [ ] **Task 6 — Verify end-to-end (shine check)** (AC: 1, 2)
  - [ ] `--deep-git` and without; webview + SPA; eyeball finished for a non-expert reader.

## Dev Notes

### Architecture patterns & constraints (must follow)

- Pure SVG/CSS charts, no info-bearing JS.
- Never color/size-only; NFR8; invariant dates via `DReadable` / `PortalDates`.
- Do not absorb 10.2, 10.7, **7.9** (file-type discrete code-map), or **17.5** (large-file / CSS investigation).
- Touching `specscribe.css` for this story's chrome is expected and small — do **not** refactor the stylesheet here (that's 17.5).

### Source tree — files to touch

| File | Change |
|---|---|
| `src/SpecScribe/GitMetrics.cs` | classifier (UPDATE) |
| `src/SpecScribe/Charts.cs` | heatmap trim+accent; coupling marks (UPDATE) |
| `src/SpecScribe/DeepAnalyticsTemplater.cs` | process note (UPDATE) |
| `src/SpecScribe/GitInsightsTemplater.cs` | sole-contributor leads (UPDATE) |
| `src/SpecScribe/assets/specscribe.css` | process + first-commit accent styles only (UPDATE) |
| Tests | Charts / DeepAnalytics / GitInsights / GitMetrics / Stylesheet; golden regen |

### UPDATE files — preserve

- **`CommitHeatmap`:** only young-repo window + caption + accent; preserve drill-down / levels / `showHeadline`.
- **`CouplingTable` / `CouplingGraph`:** additive kind marking; preserve empty degrade + a11y twin.
- **`DeepAnalyticsTemplater`:** additive note; preserve lightbox / layout.
- **`AppendContributorPanel`:** preserve zero branch, truncation, file link.

### Out of scope (do not build)

- Chart metadata frame / real-value legend → 10.2
- Glossary → 10.3; date-token sweep → 10.4; doc chips/TOC → 10.5; sunburst density → 10.7
- **Code Map file-type discrete colorize → Story 7.9**
- **Large-file / `specscribe.css` size investigation → Story 17.5**
- Reclassify 7.8 related-file nodes; filter pairs out of `ParseNumstatLog`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.6]
- [Source: docs/MissingFeatures.md#C3/#C4/#F7]; [docs/Epic3UXFeedback.md#T8]
- [Source: src/SpecScribe/Charts.cs#CommitHeatmap]; CouplingTable/Graph; DeepAnalyticsTemplater; GitInsightsTemplater
- [Source: Story 7.9] (seated) — discrete file-type code-map dimension
- [Source: Story 17.5] (seated) — large-file investigation incl. `specscribe.css`

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed. Owner locked 2026-07-18: annotate-in-place; trim≈firstCommit−1w + accent mark; sole-contributor reword; shine bar. Related backlog seated: 7.9 (code-map file-type discrete), 17.5 (large-file / CSS investigation).

### File List
