---
baseline_commit: 14232d6f82bb2971b0b54045952485b58ec77175
---

# Story 10.4: Consistent Dates and Event Sequencing

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader correlating events across the portal (a tech lead reading Journey 6, or a first-time visitor on Journey 5),
I want **one date format everywhere, an unambiguous time-of-day-and-timezone convention, sequencing for same-day events, and dates + summaries on the ADR index**,
so that recency and order are never ambiguous — whichever page I am on and whichever timezone the project was built in.

## Context & Why This Story Exists

This is the code side of **feedback T7 / MissingFeature F2**: _"Dates and recency are inconsistently formatted and used."_ ([docs/Epic3UXFeedback.md:54-56](docs/Epic3UXFeedback.md), [docs/MissingFeatures.md:93-94](docs/MissingFeatures.md)). T7 verbatim: _"Formats vary ('Thu, Jul 9, 2026' vs heatmap abbreviations vs bare 2026-07-09). The ADR index shows no dates (the ADR bodies have them — surface them in the listing). Change log entries within a story can share a date with nothing but prose ('review revision') to order them. Recommendation: one date format token used everywhere; add dates to ADR listings; add sequence markers where multiple events share a day."_ The ADR-index review adds: _"No dates, no one-line summaries — add both; the ADR body already contains them"_ and _"All four ADRs show 'Accepted'; fine now, but confirm superseded/deprecated states render distinctly when they arrive."_ ([docs/Epic3UXFeedback.md:125-128](docs/Epic3UXFeedback.md)).

**The owner added an explicit fifth concern for this story: _"please keep local timezones in mind as well."_** That is not a throwaway — it is the load-bearing subtlety here, because SpecScribe today renders **two different, unlabeled "local" clocks** side by side and never says which is which (see the timezone audit below).

### The scattered-format inventory (what "one token" has to replace)

There is **no** central date/time formatter today — every surface hand-rolls its own `ToString(...)` format string, and several render hand-authored date strings verbatim. A grep for date format strings turns up at least these distinct renderings:

| Surface | Format string | Example | Source |
|---|---|---|---|
| Footer generation clock | `MMMM d, yyyy 'at' h:mm tt` (**12-hour**) | "July 10, 2026 at 5:14 PM" | [PathUtil.cs:111](src/SpecScribe/PathUtil.cs) |
| Git Pulse last-commit | `ddd, MMM d, yyyy 'at' HH:mm` (**24-hour**) | "Fri, Jul 4, 2026 at 23:30" | [Charts.cs:627](src/SpecScribe/Charts.cs) |
| Heatmap tooltip / aria (`DReadable`) | `ddd, MMM d, yyyy` | "Fri, Jul 4, 2026" | [Charts.cs:1067](src/SpecScribe/Charts.cs) |
| Heatmap cell URL / machine (`D`) | `yyyy-MM-dd` | "2026-07-04" | [Charts.cs:1063](src/SpecScribe/Charts.cs) |
| Heatmap month gutter | `MMM` | "Jul" | [Charts.cs:561](src/SpecScribe/Charts.cs) |
| Per-commit time (in day pages / lists) | `HH:mm` (**24-hour**) | "23:30" | [GitMetrics.cs:194](src/SpecScribe/GitMetrics.cs) |
| Doc card meta (`CardMeta`) | **raw authored string** | whatever the frontmatter `Date:` literally says | [DashboardViewBuilder.cs:383](src/SpecScribe/DashboardViewBuilder.cs) |
| Retro card + header (`DateText`) | **raw authored string** | "2026-07-12" as typed | [RetroParser.cs:55](src/SpecScribe/RetroParser.cs) |
| ADR index card | **(nothing — no date at all)** | — | [DashboardViewBuilder.cs:360](src/SpecScribe/DashboardViewBuilder.cs) |
| Change Log entries | **raw markdown list**, dates as authored, no order cue | "- 2026-07-06: …" (×2 same day) | [EpicsView.cs:207](src/SpecScribe/EpicsView.cs) `ChangeLogHtml` |

So the same portal shows "July 10, 2026 at 5:14 **PM**" in the footer and "…at **23:30**" one panel away, "Fri, Jul 4, 2026" on a heatmap and a bare "2026-07-12" on a retro card, and **no** date on ADRs. That is exactly T7's complaint.

### The timezone audit (the owner's explicit ask — read this before designing)

SpecScribe renders **two independent notions of "local," and labels neither**:

1. **The generation clock** — the footer and the console both call **`DateTime.Now`** ([PathUtil.cs:111](src/SpecScribe/PathUtil.cs), [ConsoleUi.cs:162](src/SpecScribe/ConsoleUi.cs)). That is the **wall clock of the machine that ran `specscribe`**, `DateTimeKind.Unspecified`, printed with **no zone**. Generate the same repo on a laptop in New York and a CI runner in UTC and the footer shows two different times for the "same" build.

2. **The git commit clock** — every commit time comes from `git log --pretty=format:%h%x09%ad%x09%an%x09%s --date=format:%Y-%m-%dT%H:%M` ([GitMetrics.cs:133](src/SpecScribe/GitMetrics.cs)). `%ad` is the **author** date and `--date=format:` (NOT `format-local:`) renders it in **each commit's own recorded timezone offset** — the author's local time when they committed. Also printed with **no zone**. A commit made at `2026-07-04T23:30-05:00` renders "23:30" and buckets to the **Jul-4** heatmap dot, regardless of where the site is later generated.

These two clocks answer different questions ("when was this page built" vs "when did the author commit"), they can disagree by hours, and today a reader has no way to tell them apart or know either one's zone. **That ambiguity is the point of the owner's note.** This story must make the timezone semantics **coherent and legible**, and it must do so **without breaking determinism** — the codebase deliberately uses `InvariantCulture` everywhere so a repo renders byte-identically across machines/locales ([GitMetrics.cs:175-178](src/SpecScribe/GitMetrics.cs) documents exactly this for non-Gregorian calendars), and there is a committed golden **content fingerprint** ([SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)) that pins rendered bytes.

The determinism constraint rules out the naive "just convert everything to the viewer's local zone" fix:
- Switching git to `--date=format-local:` would make the **same commit** render a different time on every generating machine — non-deterministic output, and a **lie** about when the author actually worked. **Do not do this.**
- `DateOnly` values (heatmap days, coverage `LastModified`, series buckets) are **zone-free calendar dates** already — they are fine as *dates*; the ambiguity is only at *clock times* and at the *day-bucketing decision* that produced them.

The honest, deterministic policy this story should adopt (see Design Direction for the exact shape, and confirm at review — this is a **#1 review checkpoint**):
- **Keep git commit times in the commit's authored offset** (stay on `--date=format:`), and **keep the generation clock in the generating machine's local zone** — but **label each with its zone** through the single formatter, so "commit-authored 23:30 −05:00" and "generated 21:00 local" are self-describing rather than two bare unlabeled numbers.
- **Pick one time-of-day format** (recommend 24-hour `HH:mm`) so the footer and the Git Pulse stop disagreeing.
- **Document** that heatmap/series day-bucketing uses the commit's authored **calendar day** (deliberate, stable, not `format-local`).

### Sequencing (AC2) and the ADR states (AC1/AC2)

- **Same-day change-log ordering.** SpecScribe's own stories already exhibit T7's exact failure — e.g. [1-2-…md](_bmad-output/implementation-artifacts/1-2-traceability-links-across-requirements-stories-and-adrs.md) has **two** `2026-07-06:` change-log lines with only prose to order them. The change log renders today as an **opaque markdown fragment** (`ChangeLogHtml`, produced by `MarkdownConverter.RenderBlock` in [EpicsParser.cs:136-138](src/SpecScribe/EpicsParser.cs)) — there is no structured per-event model to hang a sequence marker on. This story adds a tolerant structured parse of the change-log list so same-date runs get an ordinal cue, degrading to today's as-is rendering when the shape isn't recognized (NFR8).
- **ADR dates + one-line summaries.** `AdrEntry` today carries only `Title/Path/Status/Number` ([AdrModel.cs](src/SpecScribe/AdrModel.cs)); the ADR bodies **do** carry a `**Date:**` line and a Context/Decision paragraph (see [ADR 0006](docs/adrs/0006-delivery-architecture-and-distribution.md): `**Status:** Accepted` / `**Date:** 2026-07-10`). Extract both, exactly the way `ExtractAdrStatus` already tolerates three status shapes ([SiteGenerator.cs:2108-2131](src/SpecScribe/SiteGenerator.cs)), and surface them on the ADR card.
- **Superseded/deprecated distinctness.** Good news: the pill CSS **already** styles these distinctly — `.pill.status-superseded, .pill.status-deprecated { … text-decoration: line-through; color: var(--ink-light); }` ([specscribe.css:271-272](src/SpecScribe/assets/specscribe.css)), and the ADR card already emits `status-{first-word}` ([HtmlRenderAdapter.Dashboard.cs:385](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)). So AC2's ADR-state clause is largely a **confirm-and-cover-with-a-test** ("when they arrive"), not new visual design — verify the card path produces the distinct class for a "Superseded by ADR 0007" status and add a regression test; add a `Proposed`/`proposed` mapping only if missing.

## Acceptance Criteria

**AC1 (One date-and-time token portal-wide, timezone-legible; ADR listings gain dates + one-line summaries)**
Given dates appear across pages (cards, heatmaps, change logs, ADRs, footer, Git Pulse),
When the portal generates,
Then **one date-format token is used portal-wide** — a single formatter is the sole source of every rendered human date and clock time (no surface hand-rolls its own `ToString` format),
And **clock times use one time-of-day format and carry an explicit, coherent timezone treatment** so the generation clock and the git-commit clock are each self-describing rather than two bare unlabeled "local" times (the owner's timezone requirement),
And **ADR listings gain dates and one-line summaries sourced from the ADR bodies** (tolerant extraction, degrade to absent when a record has neither).

**AC2 (Same-day sequencing; superseded/deprecated ADR states render distinctly)**
Given multiple change-log events share a date,
When they render,
Then **sequence markers order them** (an ordinal cue within the shared day; degrade to today's rendering when the change-log shape isn't recognized — NFR8),
And **superseded/deprecated ADR states render distinctly from Accepted** on both the ADR page and the ADR index card (confirmed and test-covered "when they arrive").

## Design Direction — the single date/time seam + the timezone policy (the #1 review checkpoints)

**Confirm the seam shape AND the timezone policy at review, before wiring the call sites.** These two decisions are the "silhouette" of the story. Recommended design (latitude noted):

### The seam: a new `PortalDates` static helper (single source of every date/time string)

Mirror the "single source of truth" discipline the codebase already applies to status (`StatusStyles`), vocabulary (`ModuleContext.GlossaryFor`, Story 10.3), and motion (`--motion-*` tokens): one small pure static class that **every** human-facing date/time rendering routes through. Nothing else in `src/` may hold a `ToString("…date…")` format after this story (the NFR8/T7 teeth — a reviewer will grep for stray date format strings).

```csharp
/// The single source of every human-facing date and clock string in the portal (T7 "one date token").
/// Pure + InvariantCulture so output is deterministic across machines/locales (same discipline as GitMetrics).
public static class PortalDates
{
    // ONE calendar-date token. Recommended: "MMM d, yyyy" → "Jul 9, 2026".
    public static string Day(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);
    public static string Day(DateTime dt)  => Day(DateOnly.FromDateTime(dt));

    // The heatmap keeps a weekday-prefixed variant for at-a-glance scanning, but built from the SAME token
    // so it can never drift from Day(): "ddd, " + Day(day). (Latitude: fold into Day() if the owner prefers
    // the weekday everywhere.)
    public static string DayWithWeekday(DateOnly day) => day.ToString("ddd, ", CultureInfo.InvariantCulture) + Day(day);

    // Machine/URL token (heatmap cell hrefs, per-day page filenames) — unchanged ISO, kept separate on purpose.
    public static string IsoDay(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ONE clock-time token. Recommended: 24-hour "HH:mm" (matches git; no AM/PM ambiguity).
    // zoneLabel is appended when known so the reader always knows WHICH clock this is (see timezone policy).
    public static string Timestamp(DateOnly day, string hhmm, string? zoneLabel = null) =>
        $"{Day(day)} at {hhmm}{(zoneLabel is { Length: > 0 } z ? " " + z : "")}";
}
```

- **`DayFormat`** is the one constant to pick. T7's own three examples ("Thu, Jul 9, 2026" / heatmap abbrev / "2026-07-09") are the *disease*; the cure is one token. Recommend `"MMM d, yyyy"` ("Jul 9, 2026") — compact enough for cards, unambiguous, month-name (so no US/EU `MM/dd` vs `dd/MM` confusion). **Confirm with owner at review.**
- **Keep `IsoDay` distinct.** The `yyyy-MM-dd` in heatmap **hrefs/filenames** (`commits/2026-07-04.html`) is a machine identifier, not a human date — it must stay ISO. Only *human-visible* dates unify. Do not accidentally reformat URLs.

### The timezone policy (owner's explicit ask — decide these three, they are review checkpoints)

1. **Time-of-day format:** one token. Recommend **24-hour `HH:mm`**. Consequence: the footer flips from "5:14 PM" to "17:14" (or "17:14 EST" — see #3). This changes the golden footer regex (see gotcha below).
2. **Git commit times stay in the commit's authored offset** (keep `--date=format:`, not `format-local:`) — deterministic and honest. Where a commit *time* is shown to a human (Git Pulse last-commit, per-commit lines, day pages), route it through `PortalDates.Timestamp(day, hhmm, zoneLabel)`.
3. **Label the clock's zone so the two clocks are distinguishable:**
   - **Generation clock (footer/console):** keep `DateTime.Now` (generating machine local), but append the machine's zone via `TimeZoneInfo.Local` (e.g. an abbreviation or `zzz` offset) **through `PortalDates`**, so it reads e.g. "Generated … on Jul 12, 2026 at 21:00 (−04:00)". This is inherently per-run/per-machine, and the golden already normalizes the footer clock — **widen that normalization to swallow the new zone token** (critical gotcha below).
   - **Git commit clock:** append the commit's own offset if you surface a precise time. **Simplest robust option if per-commit offset isn't already parsed:** don't invent an offset — instead **caption the clock's meaning once** near where commit times appear (e.g. Git Pulse: "times shown in each commit's local zone") rather than decorating every row. Either is acceptable; the requirement is that a reader can tell the generation clock from the commit clock and knows each one's zone semantics. **Confirm which approach at review.**
   - **Latitude:** the owner may prefer minimal chrome (a single caption per clock) over per-timestamp zone suffixes. Present both; recommend captions for git times + a labeled footer.

> **Do NOT** convert git times to UTC or to the generating machine's zone, and **do NOT** switch to `--date=format-local:`. That breaks cross-machine determinism (and the golden fingerprint) and misrepresents author-local commit times. The honest model is "each clock in its own zone, clearly labeled."

### AC1 — ADR dates + one-line summaries

Extend `AdrEntry` and the extractor, mirroring the existing status tolerance exactly:

```csharp
public sealed record AdrEntry(
    string Title, string OutputRelativePath, string SourceRelativePath,
    string? Status, int? Number,
    DateOnly? Date, string? Summary);   // NEW — null when the body carries neither
```

- **Date extraction** (`ExtractAdrDate`, beside `ExtractAdrStatus` at [SiteGenerator.cs:2108](src/SpecScribe/SiteGenerator.cs)): a `**Date:**`-bold-line regex (mirror `AdrStatusPattern`), then a `## Date` MADR-heading fallback, then `parsed.Frontmatter.Date`; parse tolerantly with `DateOnly.TryParseExact("yyyy-MM-dd", InvariantCulture)` **and** a couple of common shapes, `null` when unparseable. Render on the card via `PortalDates.Day`.
- **Summary extraction** (`ExtractAdrSummary`): recommend the **first non-empty prose paragraph under `## Context`** (or `## Decision`), collapsed to one sentence/line and tag-stripped; `null` when absent. **Latitude — confirm at review:** alternatives are the post-em-dash tail of the H1 title ("ADR 0006: … — JSON + SPA + npx vs …") or a dedicated `> summary` line. Pick the most reliable across the four real ADRs; degrade to `null` (card shows title + date only, never an empty line).
- **Card:** add the date (muted meta) and summary (one muted line) to `BuildAdrCard` ([DashboardViewBuilder.cs:360](src/SpecScribe/DashboardViewBuilder.cs)) and the Adr branch of `AppendIndexCard` ([HtmlRenderAdapter.Dashboard.cs:380](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)). Reuse the existing `.index-card-path`/retro-meta `<p>` grammar — no new component. Sorting stays number-then-title.

### AC2 — same-day change-log sequencing (the trickiest piece)

The change log is currently opaque HTML. Add a **tolerant structured parse** in `EpicsParser` before it renders:

- Recognize the shipped shape: markdown list items beginning with an ISO date — `- YYYY-MM-DD: <text>` (see the real example above). Group **consecutive same-date** items.
- For a run of N>1 items sharing a date, add an **ordinal sequence marker** (recommend a compact "(1 of 3)" cue, or a small numbered chip reusing existing badge vocabulary — confirm styling at review; no new `--status-*` token, no color-only signal). Single-occurrence dates get **no** marker (don't clutter unique days).
- Route the dates themselves through `PortalDates.Day` so the change log matches the rest of the portal (bare "2026-07-06" → "Jul 6, 2026").
- **Degrade to as-is (NFR8):** if the change-log section isn't a recognizable dated list (a table, free prose, an unusual shape), render it exactly as today. Never drop content, never reorder — markers annotate the existing order; they do not re-sort.
- Keep the change log's opaque-fragment contract to the view (`ChangeLogHtml`) — the structuring happens **inside** `EpicsParser` when it builds that fragment ([EpicsParser.cs:116-138](src/SpecScribe/EpicsParser.cs)); the view model shape (`EpicsView.ChangeLogHtml`) is unchanged, so no ripple into RenderParity's section contract.

### AC2 — ADR states distinct (confirm + cover)

- Verify (test) that a record whose status is "Superseded by ADR 0007" / "Deprecated" produces `pill status-superseded` / `status-deprecated` on **both** the ADR page ([HtmlTemplater.AppendStatusPill:155](src/SpecScribe/HtmlTemplater.cs), splits nothing) **and** the index card ([HtmlRenderAdapter.Dashboard.cs:385](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs), splits on first word) — note the two paths derive the class **differently** (page uses full status lowercased+hyphenated, card uses first word only); confirm both land on `status-superseded` for a multi-word "Superseded by …". Add `status-proposed` coverage if the CSS lacks it (it has `.pill.status-proposed` at [specscribe.css:268](src/SpecScribe/assets/specscribe.css) — good). No new visual work expected; if a gap surfaces, it's a small CSS add reusing existing tokens.

## Tasks / Subtasks

- [x] **Task 1 — Add the `PortalDates` single-source formatter** (AC: 1)
  - [x] Add `src/SpecScribe/PortalDates.cs`: `Day(DateOnly)`, `Day(DateTime)`, `DayWithWeekday(DateOnly)`, `IsoDay(DateOnly)`, `Timestamp(DateOnly, hhmm, zoneLabel?)`, and the one `DayFormat` constant (recommend `"MMM d, yyyy"`). Pure, `InvariantCulture`, no I/O.
  - [x] Decide + document the **time-of-day** token (recommend 24-hour `HH:mm`) and the **timezone policy** (git times stay author-offset; generation clock labeled with `TimeZoneInfo.Local`; commit clock captioned or offset-suffixed). Record the decision in the Dev Agent Record.

- [x] **Task 2 — Route every human date/clock through `PortalDates`** (AC: 1)
  - [x] Footer generation clock: replace the inline `MMMM d, yyyy 'at' h:mm tt` in [PathUtil.cs:111](src/SpecScribe/PathUtil.cs) with `PortalDates` (24-hour + local-zone label). **Then widen the golden `FooterClock` normalization regex** at [SiteGeneratorAdapterTests.cs:221](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) to match the new footer shape incl. the zone token (else the fingerprint becomes machine-dependent — see gotcha).
  - [x] Git Pulse last-commit ([Charts.cs:627](src/SpecScribe/Charts.cs)) and heatmap `DReadable`/month-label ([Charts.cs:561,1067](src/SpecScribe/Charts.cs)): route through `PortalDates.DayWithWeekday`/`Timestamp`. Leave `D` (URL) → `PortalDates.IsoDay` (byte-identical ISO).
  - [x] Per-commit `HH:mm` ([GitMetrics.cs:194](src/SpecScribe/GitMetrics.cs)) and its consumers (day pages, commit lists): route the *display* through `PortalDates`; leave the parse contract (`--date=format:%Y-%m-%dT%H:%M`) untouched.
  - [x] Doc card meta ([DashboardViewBuilder.cs:383](src/SpecScribe/DashboardViewBuilder.cs) `CardMeta`) and retro `DateText` ([RetroParser.cs:55](src/SpecScribe/RetroParser.cs) / [RetroTemplater.cs:38,77](src/SpecScribe/RetroTemplater.cs)): where the authored value is a parseable date, normalize it through `PortalDates.Day`; where it's free text, leave it (degrade — NFR8). Confirm `ConsoleUi.cs:162` (console-only, not portal output) is out of scope or aligned for consistency, your call.
  - [x] **Grep-guard:** after wiring, no `ToString("…[yMd]…")` human-date format string remains in `src/` outside `PortalDates` (the T7 teeth). Machine tokens (`IsoDay`, git parse format) are the only allowed exceptions.

- [x] **Task 3 — ADR dates + one-line summaries** (AC: 1)
  - [x] Add `DateOnly? Date` + `string? Summary` to `AdrEntry` ([AdrModel.cs](src/SpecScribe/AdrModel.cs)).
  - [x] Add `ExtractAdrDate` + `ExtractAdrSummary` beside `ExtractAdrStatus` ([SiteGenerator.cs:2108](src/SpecScribe/SiteGenerator.cs)); wire them into the `new AdrEntry(...)` construction at [SiteGenerator.cs:608](src/SpecScribe/SiteGenerator.cs). Tolerant, `null` when absent.
  - [x] Surface both on the ADR card: `BuildAdrCard` ([DashboardViewBuilder.cs:360](src/SpecScribe/DashboardViewBuilder.cs)) sets the meta/summary; the Adr branch of `AppendIndexCard` ([HtmlRenderAdapter.Dashboard.cs:380](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)) renders them (date via `PortalDates.Day`), reusing existing muted `<p>`/`.index-card-path` grammar. Sorting unchanged.

- [x] **Task 4 — Same-day change-log sequence markers** (AC: 2)
  - [x] In `EpicsParser` where the Change Log fragment is built ([EpicsParser.cs:116-138](src/SpecScribe/EpicsParser.cs)), add a tolerant pre-render pass: parse `- YYYY-MM-DD: text` list items, group consecutive same-date runs, add an ordinal marker to runs of N>1 (recommend "(k of N)"), reformat the visible date via `PortalDates.Day`. Degrade to as-is on any unrecognized shape; never reorder or drop content. Keep the `ChangeLogHtml` opaque-fragment output contract.
  - [x] Style the marker with existing badge/muted vocabulary (no new `--status-*` token, not color-only).

- [x] **Task 5 — Confirm superseded/deprecated ADR states render distinctly** (AC: 2)
  - [x] Add regression coverage: an ADR record with status "Superseded by ADR 0007" (multi-word) yields `status-superseded` on BOTH the page pill and the index card (note the two class-derivation paths differ). Same for "Deprecated". Confirm the CSS strikethrough/muted rule ([specscribe.css:271-272](src/SpecScribe/assets/specscribe.css)) applies. Add a small CSS/mapping fix only if a real gap surfaces.

- [x] **Task 6 — Tests** (AC: 1, 2)
  - [x] **`PortalDates`** unit tests: each method's exact output for a fixed date/time; invariant across a non-Gregorian `CultureInfo.CurrentCulture` (mirror the `GitMetrics` non-Gregorian rationale — set `th-TH` and assert stable output); `IsoDay` stays `yyyy-MM-dd`.
  - [x] **ADR extraction** unit tests: `**Date:**` line, `## Date` heading, frontmatter fallback, unparseable → `null`; summary from `## Context` first paragraph, absent → `null`. A generation-level test: `index.html` ADR cards show the date + summary for the four real-shaped ADRs.
  - [x] **Change-log sequencing**: a story with two same-date entries renders ordinal markers on both and none on a unique date; an unrecognized change-log shape renders unchanged (degrade); order preserved.
  - [x] **Timezone/format**: footer renders the chosen time token + zone label; Git Pulse and footer now share the time-of-day format; a commit-time display carries its zone treatment.
  - [x] **Parity + golden**: date changes touch **many** pages (footer on every page; Git Pulse/heatmap on index; ADR cards on index; change log on story pages). Keep `RenderParity`/SPA/webview green (the changes are in shared page HTML + view models, so no new `HostRenderException` expected — confirm). **Regenerate the golden fingerprint constant** at [SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) **after** widening the `FooterClock` normalization regex, and eyeball the diff to confirm it is only the intended date/format/ADR/change-log changes ([see golden-diff-normalization-gotchas]). **Confirm the baseline is green before starting** — a pre-existing unrelated golden drift (spec-comment-block work) has ridden recent commits; don't inherit or mask it.

- [x] **Task 7 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [x] `dotnet run` a full generate (with `--deep-git` so Git Pulse/commit surfaces populate): confirm footer, Git Pulse last-commit, heatmap tooltips, ADR cards, retro cards, and change logs all read in the **one** date format and one time-of-day format; confirm the footer clock and any git times are zone-legible and distinguishable.
  - [x] Open `index.html`: ADR cards show date + one-line summary; a story page with two same-date change-log entries shows sequence markers.
  - [x] Confirm `specscribe webview` and `--spa` render the same (they ride shared page HTML + view models).

## Dev Notes

### Architecture patterns & constraints (must follow)

- **One formatter, single source (the AC1/T7 contract).** After this story there is exactly **one** place a human date or clock string is formatted (`PortalDates`); every surface calls it. Same discipline as `StatusStyles` (single stage→class source), `ModuleContext.GlossaryFor` (Story 10.3), and the `--motion-*` tokens. A reviewer will grep `src/` for stray `ToString("…date…")` — there must be none outside `PortalDates` (machine tokens excepted).
- **Determinism is non-negotiable.** All formatting stays `InvariantCulture` and pure, exactly like the existing code ([GitMetrics.cs:175-178](src/SpecScribe/GitMetrics.cs) documents why: culture-sensitive parsing under th-TH/fa-IR corrupts every date). The committed golden fingerprint ([SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)) pins rendered bytes — any *intended* change regenerates the constant deliberately; an *unexpected* flip is a regression.
- **Timezone honesty over convenience.** Git times stay in the commit's authored offset (`--date=format:`, never `format-local:`); the generation clock stays machine-local; **both are labeled** so the reader knows which clock and which zone. Converting to UTC/viewer-local would be non-deterministic AND misrepresent author-local commit times. `DateOnly` values are zone-free calendar dates and don't need conversion — only clock times and the (already-made, now-documented) day-bucketing decision carry zone meaning.
- **The footer-clock golden gotcha (do this or the suite goes flaky).** The golden normalizes the footer wall-clock via a regex tuned to the *current* 12-hour shape ([SiteGeneratorAdapterTests.cs:221](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs): `on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} [AP]M`). If you change the footer to 24-hour and/or add a zone label, **update that regex to swallow the new shape**, or the volatile timestamp leaks into the hash and the fingerprint becomes machine/time-dependent.
- **Degrade to absent/as-is, never empty-but-present (NFR8).** No ADR date/summary → card shows title (+ status) only, no empty line. Unrecognized change-log shape → renders exactly as today, no markers, no reordering. Free-text authored dates that don't parse → left verbatim.
- **No information-bearing JavaScript, no color-only signals.** Sequence markers are static text/badges; ADR-state distinctness is the existing strikethrough + muted color **plus** the status word (not color alone) — matches the portal's pure-HTML/CSS ethos and the webview CSP.
- **Opaque-fragment contract preserved.** Change-log structuring happens inside `EpicsParser` when it builds `ChangeLogHtml`; the `EpicsView.ChangeLogHtml` shape is unchanged, so RenderParity's section-fact contract doesn't move.

### Source tree — files to touch

- `src/SpecScribe/PortalDates.cs` — the single date/time formatter (NEW — the AC1 heart).
- `src/SpecScribe/PathUtil.cs` — footer clock → `PortalDates` (24h + zone label) (UPDATE).
- `src/SpecScribe/Charts.cs` — `D`/`DReadable`/month-label + Git Pulse last-commit → `PortalDates` (UPDATE).
- `src/SpecScribe/GitMetrics.cs` — per-commit time *display* → `PortalDates`; parse contract unchanged (UPDATE).
- `src/SpecScribe/DashboardViewBuilder.cs` — `CardMeta` date normalization + `BuildAdrCard` date/summary (UPDATE).
- `src/SpecScribe/RetroParser.cs` / `RetroTemplater.cs` — retro `DateText` normalized when parseable (UPDATE).
- `src/SpecScribe/AdrModel.cs` — add `Date`/`Summary` to `AdrEntry` (UPDATE).
- `src/SpecScribe/SiteGenerator.cs` — `ExtractAdrDate`/`ExtractAdrSummary` + wire into `new AdrEntry(...)` (UPDATE).
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — Adr card branch renders date/summary (UPDATE).
- `src/SpecScribe/EpicsParser.cs` — same-day change-log sequence markers + date reformat (UPDATE).
- `src/SpecScribe/assets/specscribe.css` — sequence-marker + ADR meta styling (reuse existing badge/muted vocab) (UPDATE).
- Tests: new `PortalDatesTests.cs`; ADR extraction + change-log sequencing + ADR-state tests; **widen `FooterClock` regex and regenerate the golden fingerprint** in `SiteGeneratorAdapterTests.cs`; keep parity/SPA/webview suites green (UPDATE/NEW).

### UPDATE files — current state & what must be preserved

- **`PathUtil.RenderFooter`** ([PathUtil.cs:107-113](src/SpecScribe/PathUtil.cs)): the single footer, on every page; formats the clock **here** (single source) with a doc-comment saying so. **Preserve** the "formatted once here, not by each caller" property and the `relativePrefix` href math; just move the format into `PortalDates`. Remember the paired golden-regex update.
- **`GitMetrics.ParseLog`** ([GitMetrics.cs:161-203](src/SpecScribe/GitMetrics.cs)): the pure, unit-testable parse of `--date=format:%Y-%m-%dT%H:%M`, invariant on purpose. **Preserve** the parse format and invariance and the malformed-line-skip; only the *display* of the derived time changes. Do **not** touch the git invocation's `--date=format:` (that's the timezone decision — author-offset, deterministic).
- **`Charts.D`/`DReadable`** ([Charts.cs:1063-1067](src/SpecScribe/Charts.cs)): `D` is a **machine token** in hrefs/filenames (`commits/2026-07-04.html`) — must stay ISO (→ `PortalDates.IsoDay`, byte-identical). `DReadable` is the human token (→ `PortalDates.DayWithWeekday`). **Preserve** the href/filename bytes; only human tooltips/labels change.
- **`AdrEntry` + `GenerateAdrsInternal`** ([AdrModel.cs](src/SpecScribe/AdrModel.cs), [SiteGenerator.cs:541-638](src/SpecScribe/SiteGenerator.cs)): status is extracted via three tolerated shapes (bold line / MADR heading / frontmatter) computed once so page and card agree; records still render when a field is null (unnumbered sort last, status-less cards carry no badge). **Preserve** that tolerance and the "page and card derive from the same extraction" property — add Date/Summary the **same** way (extract once, both surfaces read it), null-safe.
- **`AppendIndexCard` (Adr branch)** ([HtmlRenderAdapter.Dashboard.cs:380-390](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)): the card links to the ADR, renders a `status-{firstWord}` pill, shows the source path. **Preserve** the whole-card anchor and pill-class derivation; append date/summary as muted lines. Note the pill-class derivation differs from the page's `AppendStatusPill` (first-word vs full) — keep both landing on the same class for multi-word statuses.
- **`EpicsParser` change-log carve** ([EpicsParser.cs:116-138](src/SpecScribe/EpicsParser.cs)): the Change Log is carved out of the remainder and rendered as its own opaque fragment via `MarkdownConverter.RenderBlock`. **Preserve** the carve + opaque-fragment contract and the section-order independence; the sequencing pass slots in where the fragment is built, degrading to the current `RenderBlock` output on any unrecognized shape.

### Testing standards

- xUnit. `PortalDates` and the ADR extractors are pure functions — unit-test them directly against strings (the `RequirementLinkifier`/`Charts`/`StatusStyles` test style). Generation-level page tests use the temp-root fixture pattern (`Directory.CreateTempSubdirectory`, `IDisposable`) as in the `SiteGenerator*` suites.
- Assert both **presence** (one format on every surface; ADR date+summary; same-day markers; distinct superseded pill) and **absence/degrade** (no marker on unique dates; null ADR date → no empty line; unrecognized change log unchanged; unparseable authored date left verbatim).
- **Non-Gregorian invariance**: set `CultureInfo.CurrentCulture`/`CurrentUICulture` to `th-TH` (or `fa-IR`) in a `PortalDates` test and assert output is unchanged — same guard `GitMetrics` documents. Determinism is a first-class requirement here.
- Regenerate `GoldenContentFingerprint` **deliberately** and diff to prove the change is only date-format/ADR/change-log/footer — and **only after** widening `FooterClock`. Confirm the baseline is green first (mind the known unrelated pre-existing drift) so you don't inherit or mask red.

### Out of scope (do not build)

- No nav grouping / Insights nav (Story 10.1). No chart metadata standard — legends, time-window numbers, "why this matters" (Story 10.2). No glossary/how-to-read/abbr work (Story 10.3, `ready-for-dev`).
- No recency / "changed since last visit" markers on cards/widgets (that's MissingFeature E2 / Story 7.3's adjacent scope) — this story is *format + sequencing*, not new recency signals.
- No document-rendering legibility — wiki-links, references appendix, collapsible TOC, retired-item collapse (Story 10.5).
- No insight-chart context polish — coupling process-vs-code, heatmap dead-zone annotation (Story 10.6).
- **Do not** switch git to `--date=format-local:` or convert any time to UTC/viewer-local (breaks determinism + author-local honesty). **Do not** change heatmap href/filename ISO tokens. **Do not** re-sort or drop any change-log content (markers annotate existing order only).
- No new authoring schema — dates/summaries/sequences are derived from what artifacts already contain (the load-bearing Epic 9/10 principle); degrade to absent when not present.

### Project Structure Notes

- Output dir is `SpecScribeOutput` (never `docs/live`) — [see generate-output-dir-is-specscribeoutput].
- `src/SpecScribe/assets/specscribe.css` is the styling source of truth; the generated `docs/live/specscribe.css` is untracked output — never edit the generated copy.
- If working in a git worktree, target the worktree path (main has a background auto-committer) — [see worktree-edits-must-target-worktree-path].
- The golden fingerprint constant lives at [SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs); its volatile-token normalizers (incl. the `FooterClock` regex you must widen) are at [SiteGeneratorAdapterTests.cs:220-256](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) — [see golden-diff-normalization-gotchas].

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.4: Consistent Dates and Event Sequencing] — the two ACs; Epic 10 FR27/28/29, UX-DR25/27/28/29/30, NFR8.
- [Source: docs/Epic3UXFeedback.md#T7] — "Dates and recency are inconsistently formatted and used"; one date token; ADR-listing dates; same-day sequence markers.
- [Source: docs/Epic3UXFeedback.md#ADR index] (lines 125-128) — ADR index has no dates/summaries (bodies have them — surface them); confirm superseded/deprecated render distinctly "when they arrive".
- [Source: docs/MissingFeatures.md#F2] — "Date-format token and event sequencing — NET-NEW": one token, dates+summaries on ADR listings, sequence markers for same-day events.
- [Source: src/SpecScribe/PathUtil.cs#RenderFooter] — the single footer clock (`DateTime.Now`, machine-local, 12-hour) — the generation clock to unify + label.
- [Source: src/SpecScribe/GitMetrics.cs] — `git log … %ad --date=format:%Y-%m-%dT%H:%M` (author date, commit-local offset, deterministic); invariant-parse rationale — the git clock to label, not convert.
- [Source: src/SpecScribe/Charts.cs] — `D`/`DReadable`/month-label + Git Pulse last-commit (`HH:mm`) — the second time-of-day format to reconcile.
- [Source: src/SpecScribe/AdrModel.cs] + [src/SpecScribe/SiteGenerator.cs#ExtractAdrStatus] — the tolerant status extraction to mirror for Date/Summary; `AdrEntry` to extend.
- [Source: src/SpecScribe/EpicsParser.cs] — the change-log carve + opaque `ChangeLogHtml` fragment where same-day sequencing slots in.
- [Source: src/SpecScribe/assets/specscribe.css] (lines 254-272) — the `.pill.status-*` rules that already render superseded/deprecated distinctly (strikethrough/muted).
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs] — the golden `GoldenContentFingerprint` (line 213) + `FooterClock` normalization (line 221) that must be widened + regenerated.
- [Source: _bmad-output/implementation-artifacts/1-2-traceability-links-across-requirements-stories-and-adrs.md#Change Log] — a real same-date change-log run (two `2026-07-06:` entries) — the exact T7 failure to fix.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no blocking failures. Full `dotnet test` suite green: 1588/1588 passing at current `main` (`c7ba041`).

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- **Bookkeeping backfill, not new implementation.** This story was implemented earlier via a bundled quick-dev session (`_bmad-output/implementation-artifacts/spec-7-3-10-4-honest-navigable-portal-dates.md`, commit `14232d6` "Dates and misc. work", 2026-07-13) that intentionally kept three related concerns together: the 7.3 artifact-day git-derivation bug fix, this story's date/timezone unification, and date-linkification follow-up. sprint-status.yaml already reflected `10-4-consistent-dates-and-event-sequencing: review` with a detailed note, but this standalone story file's own Tasks/Subtasks, Dev Agent Record, and File List were never updated to match — this dev-story run verified every AC/task against the actual diff and closed that gap rather than re-implementing already-shipped work.
- **AC1 verified in code:** `PortalDates` ([PortalDates.cs](src/SpecScribe/PortalDates.cs)) is the single date/clock formatter — `Day`/`DayWithWeekday`/`IsoDay`/`MonthShort`/`TimeOfDay`/`Timestamp`/`TryParseDay`/`ReformatAuthored`/`LocalZoneLabel`, one `DayFormat` ("MMM d, yyyy") and one `TimeFormat` ("HH:mm"), pure + `InvariantCulture`. All call sites route through it: footer clock (`PathUtil.cs`, now machine-local + `UTC±hh:mm` zone label via `LocalZoneLabel`), Git Pulse last-commit + heatmap `D`/`DReadable`/month-gutter (`Charts.cs`; `D` stays ISO for hrefs, only `DReadable` humanizes), per-commit time display (`GitMetrics.cs`; parse contract `--date=format:%Y-%m-%dT%H:%M` untouched), doc card meta + retro `DateText` (normalized via `ReformatAuthored`, degrading free text verbatim). Timezone policy matches the story's owner-confirmed design exactly: git times stay in the commit's authored offset (never `format-local:`/UTC), generation clock stays machine-local but is now labeled.
- **AC1 ADR dates/summaries verified:** `AdrEntry` gained `Date`/`Summary` ([AdrModel.cs](src/SpecScribe/AdrModel.cs)); `ExtractAdrDate`/`ExtractAdrSummary` in `SiteGenerator.cs` mirror `ExtractAdrStatus`'s tolerance (`**Date:**` line / `## Date` heading / frontmatter; first `## Context` paragraph / H1 em-dash-tail fallback), null-safe degrade. Surfaced on both `BuildAdrCard` and the index-card Adr branch.
- **AC2 verified in code:** `EpicsParser.SequenceChangeLog` recognizes `- YYYY-MM-DD: text` change-log items, reformats dates through `PortalDates.Day`, and adds a "(k of N)" ordinal marker to source-adjacency-guarded same-date runs (an intervening differently-dated bullet correctly breaks the run) — unique dates get no marker, unrecognized shapes degrade to as-is untouched, never reordered. Superseded/deprecated pill distinctness ( `.pill.status-superseded`/`.status-deprecated` strikethrough+muted) was already present in CSS and is now test-covered (`SiteGeneratorAdrToleranceTests.cs`).
- **Golden gotcha confirmed handled:** the `FooterClock` normalization regex in `SiteGeneratorAdapterTests.cs` was widened to match the new 24-hour + `UTC±hh:mm`-label footer shape before the fingerprint constant was regenerated, exactly per this story's flagged risk.
- **Tests confirmed present:** `PortalDatesTests.cs` (incl. non-Gregorian `th-TH` invariance), `ChangeLogSequencingTests.cs`, `SiteGeneratorAdrToleranceTests.cs`, plus updated `ChartsTests`/`GitMetricsFileInsightsTests`/`PathUtilTests`/`RetroTests`/`EpicsParserTests`/`SiteGeneratorTimelineTests`/`SiteGeneratorWebviewTests`/`StylesheetTests`/`SiteGeneratorCommitDetailsTests`/`HtmlTemplaterTests` cover the new behavior and updated golden expectations. Ran the full suite at current `main` to confirm no drift: 1588/1588 green.

### File List

Files touched for this story's scope, extracted from commit `14232d6` (which also carried unrelated bundled work — code-page tabs, VS Code workspace/indicators, code-map declutter follow-ups — omitted here):

- `src/SpecScribe/PortalDates.cs` — the single date/time formatter (NEW)
- `src/SpecScribe/PathUtil.cs` — footer clock routed through `PortalDates` (24h + `LocalZoneLabel`) (UPDATE)
- `src/SpecScribe/Charts.cs` — `D`/`DReadable`/month-label + Git Pulse last-commit routed through `PortalDates` (UPDATE)
- `src/SpecScribe/GitMetrics.cs` — per-commit time display routed through `PortalDates`; parse contract unchanged (UPDATE)
- `src/SpecScribe/DashboardViewBuilder.cs` — `CardMeta` date normalization + `BuildAdrCard` date/summary (UPDATE)
- `src/SpecScribe/DashboardView.cs` — ADR card view-model fields for date/summary (UPDATE)
- `src/SpecScribe/RetroTemplater.cs` — retro `DateText` normalized when parseable (UPDATE)
- `src/SpecScribe/AdrModel.cs` — `AdrEntry` gains `Date`/`Summary` (UPDATE)
- `src/SpecScribe/SiteGenerator.cs` — `ExtractAdrDate`/`ExtractAdrSummary` wired into `AdrEntry` construction (UPDATE)
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — Adr index-card branch renders date/summary (UPDATE)
- `src/SpecScribe/EpicsParser.cs` — `SequenceChangeLog` same-day change-log ordinal markers + date reformat; balance-aware `SourceCitationBrackets` regex fix (UPDATE)
- `tests/SpecScribe.Tests/PortalDatesTests.cs` — unit tests incl. non-Gregorian invariance (NEW)
- `tests/SpecScribe.Tests/ChangeLogSequencingTests.cs` — same-day sequencing + degrade tests (NEW)
- `tests/SpecScribe.Tests/SiteGeneratorAdrToleranceTests.cs` — ADR date/summary extraction + status-pill distinctness tests (NEW)
- `tests/SpecScribe.Tests/ChartsTests.cs` — updated for `PortalDates`-routed chart date/time output (UPDATE)
- `tests/SpecScribe.Tests/GitMetricsFileInsightsTests.cs` — updated for display-time routing (UPDATE)
- `tests/SpecScribe.Tests/PathUtilTests.cs` — updated footer clock expectations (UPDATE)
- `tests/SpecScribe.Tests/RetroTests.cs` — updated retro date normalization expectations (UPDATE)
- `tests/SpecScribe.Tests/EpicsParserTests.cs` — updated for the balance-aware citation regex + change-log carve (UPDATE)
- `tests/SpecScribe.Tests/SiteGeneratorTimelineTests.cs` — updated timeline date expectations (UPDATE)
- `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs` — updated commit-detail date expectations (UPDATE)
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — updated status-pill expectations (UPDATE)
- `tests/SpecScribe.Tests/StylesheetTests.cs` — updated for reused badge/muted vocabulary (UPDATE)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — widened `FooterClock` normalization regex + regenerated golden content-fingerprint constant (UPDATE)

## Change Log

- 2026-07-13: Implemented via bundled quick-dev session `spec-7-3-10-4-honest-navigable-portal-dates.md` (commit `14232d6`) — `PortalDates` single-source formatter (one date token, 24h time token, labeled machine-local footer zone, author-offset git times), ADR `Date`/`Summary` extraction + card surfacing, and same-day change-log "(k of N)" sequencing with source-adjacency-guarded degrade. 997 tests green at that time; sprint-status.yaml updated to `review`.
- 2026-07-18: dev-story — verified the shipped implementation against every AC/task in this story, found no gaps; backfilled this file's Tasks/Subtasks, Dev Agent Record, and File List (previously stale from the standalone-story path being superseded by the bundled session). No code changes this session. Full suite re-confirmed green: 1588/1588. Status → review (already matched sprint-status.yaml).
