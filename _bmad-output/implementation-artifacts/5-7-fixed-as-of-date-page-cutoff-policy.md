---
baseline_commit: d1722f17a6f9fefdb50d3aab91a9b8bca805f4e7
---

# Story 5.7: Fixed `--as-of <date>` Date-Page Cutoff Policy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer producing a portal for a review, a demo, or a historical snapshot,
I want to pin the date-page "today" cutoff to an explicit calendar date,
so that a regenerated portal reproduces the same date-page set regardless of when or where it is generated.

## Context & Origin (read first)

**Seeded 2026-07-27 at the Epic 5 retrospective** as the owner's answer to Story 5.5's Open Question #3
("is a fixed explicit `--as-of <date>` override desirable now, or defer?") → **ADD IT**. Epic 5's
`sprint-status.yaml` key deliberately stays `in-progress`: the epic reopened for this one story at its own
retrospective.

**This story EXTENDS Story 5.5's vocabulary. It does not re-open it.** Two of 5.5's three open questions were
**confirmed as implemented** at the same retrospective and are **locked**:

- The option name `--today-policy` and its tokens `machine-local` / `utc` / `last-commit` **stand as shipped**.
- `LastCommit` remains `series.Max(day)` = the latest **authored** commit day, chosen for symmetry with
  `LinkedCommitDays`, which filters the same series.

Do not rename, redefine, or "improve" any of the three existing policies. This story adds a **fourth,
argument-bearing** policy and the plumbing an argument-bearing policy needs.

**Why it matters:** all three shipped policies read a live clock or the live series. A portal regenerated a week
later therefore has a *different* date-page set — fine for a working portal, wrong for a snapshot you want to
hand someone and have reproduce. `--as-of` makes the cutoff an input.

## Owner Decisions (locked at create-story, 2026-07-27)

**D1 — CLI shape: `--as-of <DATE>` implies the policy, and collapses onto the existing single field.**
A dedicated `--as-of <DATE>` option. When present it resolves to the new fixed-date policy — the user does
**not** also pass `--today-policy`. Internally there is still **one** configured field: provenance, persistence
and diagnostics all carry a single composite token `as-of:2026-07-27` on the existing `today_policy` field.
So: one `SettingsResolver.Fields` constant, one `SavedSettings` field, one `Validate()` branch shape, and
`Token` → `TryParse` round-trip preserved by construction. The only genuinely new validation is rejecting the
conflict `--today-policy utc --as-of 2026-07-27`.

**D2 — An `--as-of` date before the repo's first commit is ACCEPTED verbatim; the heatmap's text is fixed.**
An empty commit-date-page set is the *correct* answer for a historical snapshot, so there is no rejection and no
warning. But `CommitHeatmap`'s accessible name and visible headline currently count the **whole** series and so
would name commits the grid does not show. Bound them to the rendered window. This also fixes the pre-existing
future-skew case, where the same text already overclaims today.

**D3 — Parse with `DateOnly.TryParse` (forgiving), and ECHO the parsed date in the logs.**
Forgiving input is acceptable *because* the resolved date is echoed back, so a misparse is visible immediately
rather than silently shifting the portal. Two consequences, both required:
- Parse with **`CultureInfo.InvariantCulture`**, not the ambient culture. This is not a style preference: the
  story's whole purpose is "the same date-page set regardless of *where* it is generated," and a
  culture-sensitive parse makes one string mean different days on different hosts. `Charts.D` already documents
  the th-TH / fa-IR non-Gregorian hazard for the *formatting* half of this.
- The resolved date must appear **in the ordinary run log**, canonicalized to ISO — not only under
  `--show-config`. See Task 5.

## Acceptance Criteria

_Verbatim from [epics.md § Story 5.7](../planning-artifacts/epics.md), with the owner decisions above folded in
as sub-points. AC numbering matches epics.md._

1. **The explicit date becomes the run's single resolved `today`.**
   **Given** I supply an explicit date to the date-page today policy
   **When** generation runs
   **Then** that date is used as the single resolved `today` by every one of Story 5.5's **five** cutoff consumers
   (`Charts.LinkedCommitDays`, date-page generation, artifact-skew, the heatmap grid, and the Git Pulse guard)
   **with no second resolution anywhere**
   **And** git commit times still render in each commit's authored offset (Story 10.4 honesty, unchanged)
   **And** the dashboard's artifact-staleness `today` stays a **separate** value from the date cutoff, per Story
   5.5's `dateCutoff` parameter split.
   - 1a. **(D2)** A date before the repo's first commit is accepted and yields an empty commit-date-page set —
     no crash, no rejection, no warning — **and** the heatmap's `aria-label` and visible headline describe only
     the rendered window, never commits outside it.
   - 1b. **The golden content fingerprint MUST NOT MOVE.** At the default policy this story changes no rendered
     byte. See "Scope guard" below — this is inverted versus Story 5.5, which legitimately moved it.

2. **The explicit date participates in the existing config stack unchanged in shape.**
   **Given** the explicit-date policy is set via CLI or persisted in `.specscribe/config.json`
   **When** generation runs
   **Then** it participates in the existing three-way provenance (`CommandLine` > `SavedSettings` > `Default`)
   and appears on `--show-config` and the Diagnostics config log (Story 4.8) like every other field, with
   interactive/CLI parity (NFR7 / Story 5.2)
   **And** an unparseable or absent date is rejected at the same `SiteSettings.Validate()` gate the other policy
   tokens use, with the same forgiving-vocabulary persistence treatment `DatePolicyJsonConverter` already applies
   — **a bad token must never fail whole-document deserialization and discard sibling settings** (the defect
   Story 5.5's code review fixed).
   - 2a. **(D3)** The resolved date is echoed in ISO form on an ordinary run, so a forgiving parse is auditable.
   - 2b. **(D1)** `--as-of` combined with a conflicting `--today-policy` is rejected at the same gate, rather
     than one silently winning.

## The load-bearing invariant (inherited whole from Story 5.5 — read before writing code)

`SiteGenerator._today` is **one** policy-resolved value computed **once** per pass by `RefreshToday()` and
threaded to all five consumers. `LinkedCommitDays`'s guarantee — *a linked cell always has a generated page, and
vice versa* — holds only while every consumer filters on the same value.

**A second resolution anywhere is the defect this design exists to prevent.** `--as-of` is the one policy where
re-resolution looks harmless (the date is fixed, so two reads agree). Do not let that reasoning creep in: the
shared field is the structure that makes the *other three* policies safe, and a new call site that resolves
independently will be correct under `as-of` and wrong under `utc`/`last-commit`.

Existing anchors — do not add a sixth resolution point:

| Consumer | Site |
|---|---|
| Date-page generation | [SiteGenerator.cs:1370](../../src/SpecScribe/SiteGenerator.cs) — `LinkedCommitDays(…, _today)` |
| Artifact-by-day future-skew guard | [SiteGenerator.cs:1478](../../src/SpecScribe/SiteGenerator.cs) — `var today = _today;` |
| `ChangeLogDayHref` link guard | [SiteGenerator.cs:1790](../../src/SpecScribe/SiteGenerator.cs) |
| Heatmap grid extent | `Charts.CommitHeatmap(… today:)` via [TimelineTemplater](../../src/SpecScribe/TimelineTemplater.cs) / [GitInsightsTemplater](../../src/SpecScribe/GitInsightsTemplater.cs) ([SiteGenerator.cs:1569](../../src/SpecScribe/SiteGenerator.cs), [:2589](../../src/SpecScribe/SiteGenerator.cs)) |
| Git Pulse last-commit guard | `Charts.GitPulsePanel(… today:)` via `dateCutoff` ([SiteGenerator.cs:2874](../../src/SpecScribe/SiteGenerator.cs), [:3151](../../src/SpecScribe/SiteGenerator.cs), [:3347](../../src/SpecScribe/SiteGenerator.cs)) |

Two further 5.5 constraints carry, both already correct in the tree — **do not undo them**:

- **Cutoff ≠ staleness.** `RenderDashboardBody`/`BuildIndexPage` take `today` (artifact staleness) *and*
  `dateCutoff` (the date cutoff) as **separate** parameters
  ([HtmlRenderAdapter.Dashboard.cs:18-23](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)). Conflating them
  would let `--as-of` on a long-idle repo report every planning artifact as freshly updated.
- **Commit timestamps are never re-zoned.** This story governs the day CUTOFF only. `PortalDates` keeps rendering
  each commit in its authored offset.

## Tasks / Subtasks

- [x] **Task 1 — Give the policy a shape that can carry a date** (AC: #1, #2)
  - [x] `DatePolicy` (enum) gains a fourth member `AsOf`. Keep `MachineLocal` as the zero value.
  - [x] Add `public readonly record struct DateCutoff(DatePolicy Policy, DateOnly? AsOf)` to
    [DatePolicy.cs](../../src/SpecScribe/DatePolicy.cs). **`default(DateCutoff)` is `(MachineLocal, null)`** — a
    record *struct*, deliberately, so Story 5.5's "the default is the status quo by construction" guarantee
    survives the shape change verbatim. Document that in the type's XML doc.
  - [x] `DatePolicies.TryParse(string?, out DateCutoff)`: keep all existing canonical + forgiving spellings
    unchanged, and add the composite form `as-of:<date>` (prefix match, case-insensitive, `_`→`-` normalized like
    the existing path). The date half parses via `DateOnly.TryParse(value, CultureInfo.InvariantCulture,
    DateTimeStyles.None, out …)` **(D3)**. A bare `as-of` with no date, or an unparseable date, returns `false`.
  - [x] `DatePolicies.Token(DateCutoff)` → `"as-of:2026-07-27"` for `AsOf` (ISO via `PortalDates.IsoDay`, so the
    token matches the `commits/{date}.html` filename vocabulary), existing tokens otherwise. This is what makes
    the persisted value and the `--show-config` value round-trip through `TryParse` — a hard requirement, since
    `Token`→`TryParse` is already an asserted invariant.
  - [x] `DatePolicies.Label(DateCutoff)` → e.g. `"fixed date 2026-07-27"`. Still a WORD-and-digits string, never
    a color or icon (the diagnostics `<dl>` and the interactive prompt are plain text).
  - [x] `RejectionMessage`: **leave `CanonicalTokens` as the three parseable tokens.** Do not add an
    `as-of:<date>` placeholder to that list — it is consumed as a list of things that *would have worked*, and an
    unparseable placeholder there is a trap. Instead append one sentence naming the flag:
    `"For a fixed date, use --as-of <yyyy-MM-dd>."`
  - [x] `Charts.ResolveToday(DateCutoff cutoff, IReadOnlyList<(DateOnly Day, int Count)>? series)`: new `AsOf` arm
    returns `cutoff.AsOf`. **Degradation:** `AsOf` with a null date falls back to `MachineLocal`, mirroring
    `LastCommit`-without-history — unreachable through the validated CLI path, but this resolver is also the
    library entry point (NFR8, and the same reasoning that made `LastCommit` degrade rather than throw).
  - [x] Update the two in-`Charts` library-caller defaults that currently read
    `ResolveToday(DatePolicy.MachineLocal, series: null)` ([Charts.cs:840](../../src/SpecScribe/Charts.cs),
    [Charts.cs:1377](../../src/SpecScribe/Charts.cs)) to the new type. Their **doc comments must keep** saying
    this is a deliberate degrade for library callers, not the run policy — that note exists because a future call
    site forgetting to thread `today` would silently regress to the default policy.

- [x] **Task 2 — Thread the shape through the options/settings stack** (AC: #2)
  - [x] `ForgeOptions`: rename `DatePolicy` → `DateCutoff`, retype to `DateCutoff`, and retype the
    `datePolicy` → `dateCutoff` parameter on `Resolve(...)` (still non-`required`, still defaulted). **Only three
    read sites**: [SiteGenerator.cs:73](../../src/SpecScribe/SiteGenerator.cs),
    [SettingsResolver.cs:153](../../src/SpecScribe/SettingsResolver.cs),
    [DiagnosticsTemplater.cs:158](../../src/SpecScribe/DiagnosticsTemplater.cs).
  - [x] `SiteSettings`: add `[CommandOption("--as-of <DATE>")] public string? AsOf { get; set; }` with a
    `[Description]`. Extend `ResolveDatePolicy()` (rename to `ResolveDateCutoff()`) so `AsOf` present ⇒ the fixed
    policy, with no `--today-policy` required. Keep its throw as the **defence-in-depth backstop** for
    interactive/library callers that bypass Spectre — 5.5's review explicitly confirmed that path is intentional,
    not dead code; do not delete it.
  - [x] `SiteSettings.Validate()` — the single gate, and it can return only **one** error, so order the checks
    deliberately and document the order:
    1. `TodayPolicy` non-empty and unparseable → existing message (unchanged).
    2. `AsOf` non-empty and unparseable → new message naming the value and the expected `yyyy-MM-dd` shape.
    3. **(2b)** `AsOf` non-empty **and** `TodayPolicy` resolves to something other than the fixed policy →
       conflict error naming both flags. Passing `--today-policy as-of:2026-07-27 --as-of 2026-07-27` in
       agreement is **not** a conflict.
  - [x] `--today-policy as-of:2026-07-27` is **accepted** (it falls out of the single parse path, and the
    persisted value must round-trip through exactly that path) but stays **unadvertised**, mirroring how the
    forgiving spellings are already "accepted by `TryParse` but deliberately not advertised"
    ([DatePolicy.cs:34](../../src/SpecScribe/DatePolicy.cs)). `--as-of` is the documented surface.
  - [x] `SavedSettings.TodayPolicy`: retype `DatePolicy?` → `DateCutoff?`, keep it in `IsEmpty`, keep the
    persist-only-when-non-default rule in `Capture`/`ResolvePolicyOrNull`, keep CLI-wins in `ApplyTo`.
  - [x] **`DatePolicyJsonConverter` must be EXTENDED, not replaced** (AC #2's explicit requirement):
    - `Read` keeps degrading an unrecognized token to `null` ("not configured") instead of throwing. **Do not**
      retype the field to `string?` and drop the converter: an unvalidated string would flow through `ApplyTo`
      into `ResolveDateCutoff()`'s **throw**, converting today's silent-and-safe degrade into a new hard failure
      — the exact blast-radius direction AC #2 forbids.
    - `Write` currently emits `policy.ToString()`, i.e. the enum member name `"Utc"`
      ([SettingsStore.cs:87-91](../../src/SpecScribe/SettingsStore.cs)). A record's `ToString()` would emit
      `DateCutoff { Policy = AsOf, AsOf = … }`. **Switch `Write` to `DatePolicies.Token(...)`**, making read and
      write symmetric on one vocabulary for the first time. Verify an existing `.specscribe` holding `"Utc"` /
      `"LastCommit"` still loads (both parse case-insensitively today — keep it that way).
  - [x] `SettingsResolver`: **no new `Fields` constant.** `Fields.TodayPolicy` (`today_policy`) is a published
    `--show-config` CI contract and keeps carrying the composite token. `CliOverrides.TodayPolicy` must now be
    true when **either** `--today-policy` or `--as-of` was supplied — keep the existing `{ Length: > 0 }`
    predicate shape and its in-code justification comment.

- [x] **Task 3 — Interactive parity, including the date (AC: #2 / NFR7) — the highest-risk task**
  - [x] `ConfigurePaths`'s `SelectionPrompt<DatePolicy>` builds its choices from
    `Enum.GetValues<DatePolicy>()` ([Commands.cs:624-631](../../src/SpecScribe/Commands.cs)). Adding `AsOf`
    therefore makes the menu **offer a policy it cannot satisfy** — selecting it would produce a dateless token.
    Add a follow-up `TextPrompt<string>` for the date, gated on the fixed policy being chosen, validating with
    the **same** `TryParse`/`DateOnly.TryParse` path (`.Validate(...)` re-prompts rather than accepting garbage).
  - [x] Preserve the stated "re-running Configure paths never silently flips it" discipline: when the current
    policy is already `AsOf`, pre-select it **and** seed the `TextPrompt` default with the existing date.
  - [x] Keep the existing yellow "saved today-policy is not recognized" warning line
    ([Commands.cs:616-622](../../src/SpecScribe/Commands.cs)) working for the new composite token.
  - [x] ⚠️ **This surface is unverifiable by any agent harness.** Every tool harness captures stdout, so Spectre
    reports `Interactive == false` and `AnsiConsole.Prompt` cannot be exercised — a permanent blind spot recorded
    independently by Stories 5.1, 5.2, 5.5 and confirmed at the Epic 5 retro, with the missing
    `Spectre.Console.Testing`/`CommandContext` harness logged in
    [deferred-work.md:1020](deferred-work.md). **Do not claim this task verified.** Record it under "Honest
    limits on verification" and leave it for the owner, exactly as 5.5's equivalent review finding did.

- [x] **Task 4 — Heatmap text honesty (AC: #1a / D2) — DO NOT INVENT THIS; PORT THE SIBLING**
  - [x] ⚠️ **The fix already exists 200 lines below, in the same file.**
    `Charts.DeliveryCadenceHeatmap` ([Charts.cs:1036](../../src/SpecScribe/Charts.cs)) is the same grid and
    already does exactly what this task needs, for the same stated reason — its comment reads *"Bound EVERY
    summary derived below … so a story carrying a future-dated Change-Log date can't make the aria-label / window
    overstate what the cells actually render (the project's truthfulness invariant)"*
    ([Charts.cs:1045-1055](../../src/SpecScribe/Charts.cs)). `CommitHeatmap` is simply the **un-migrated twin**.
    Port that shape verbatim; do not design a new one.
  - [x] The three lines to copy, in `CommitHeatmap` after `todayValue` is resolved:
    1. `var visible = series.Where(s => s.Day <= todayValue).ToList();`
    2. `if (visible.Count == 0) return "<div class=\"chart-empty\">…</div>";` — **this is the crash guard.**
       `firstCommit`/`lastCommit` are `series.Min/Max(...)` ([Charts.cs:838-839](../../src/SpecScribe/Charts.cs))
       and `firstCommit` also drives `start`/`isYoungRepo`, so an `--as-of` date before every commit would make a
       naive `series.Where(…).Min()` throw `InvalidOperationException`. The sibling answers this with an
       early-return designed empty state (UX-DR22), not with special-cased geometry. Do the same, with a message
       naming the cutoff so the state is self-explaining rather than just blank.
    3. Derive `firstCommit`, `lastCommit`, `totalCommits`, `activeDays` and `maxCount` from `visible`
       ([Charts.cs:838-839](../../src/SpecScribe/Charts.cs), [:884-886](../../src/SpecScribe/Charts.cs),
       [:867](../../src/SpecScribe/Charts.cs)).
  - [x] ⚠️ **Bound the visible `heatmap-headline` and the `aria-label` together.** They restate the same figures
    (`heatAria` at [:886](../../src/SpecScribe/Charts.cs), the headline at
    [:908-910](../../src/SpecScribe/Charts.cs)); fixing one alone makes the text twin disagree with the visual,
    which ADR 0013 forbids.
  - [x] `HeatLevel` needs no change: `count <= 0` returns 0 and `maxCount <= 1` returns 1, so `maxCount == 0`
    cannot divide by zero. Verified — do not "fix" it.
  - [x] `maxCount` already filters on `s.Day <= todayValue` ([:867](../../src/SpecScribe/Charts.cs)) — folding it
    onto `visible` is a simplification, not a behavior change. Keep its "a future commit must not inflate
    maxCount and depress every visible cell" comment.
  - [x] **Verify, don't assume,** that these three existing assertions still hold (they will, if and only if their
    fixtures carry no days after the resolved cutoff): `ChartsTests.cs:1437` (`across 2 active days, … to …`),
    `GitInsightsTemplaterTests.cs:250` (`1 commit across 1 active day`), `HtmlTemplaterTests.cs:298`.

- [x] **Task 5 — Echo the resolved date, and the diagnostics row (AC: #2, #2a)**
  - [x] `DiagnosticsConfig.DatePolicy` → `DateCutoff`; the config `<dl>` row
    ([DiagnosticsTemplater.cs:294-296](../../src/SpecScribe/DiagnosticsTemplater.cs)) keeps the established
    `"<label> (default)"` / `"<label> (--flag <token>)"` provenance convention, naming `--as-of` for the fixed
    policy. Plain text in a `<dl>`, never color-alone.
  - [x] **(D3) New:** `ConsoleUi.PrintPaths` prints only Project / Sources / ADRs / Output today
    ([ConsoleUi.cs:67-88](../../src/SpecScribe/ConsoleUi.cs)), so a pinned cutoff is currently invisible on an
    ordinary run. Add a **conditional** line — shown only when the resolved policy is the fixed date — echoing
    the ISO-canonicalized value and the flag, e.g.
    `i Date-page cutoff pinned to 2026-07-27 (--as-of)`. Match the style and placement of the existing
    conditional "ADR directory not found" line rather than adding a grid row (a permanent row would change every
    ordinary run's output for a setting almost nobody sets).
  - [x] Confirm `--show-config` emits `field=today_policy origin=commandline value=as-of:2026-07-27`. Note
    `EscapeForLine` needs no change — an ISO date contains no newline.

- [x] **Task 6 — Tests, and the inverted golden guard** (AC: #1, #1b, #2)
  - [x] `DatePolicyTests`: extend `TryParse` theories (`as-of:2026-07-27`, `AS-OF:2026-07-27`,
    `as_of:2026-07-27`); reject `as-of`, `as-of:`, `as-of:notadate`, `as-of:2026-13-45`. Extend
    `Token_RoundTripsThroughTryParse` with the composite token. Add `ResolveToday_AsOf_IsTheSuppliedDate` and
    `ResolveToday_AsOf_WithoutADate_FallsBackToMachineLocal`.
  - [x] ⚠️ **Two existing tests enumerate the enum and will need updating, not deleting:**
    `Label_IsDistinctNonEmptyTextForEveryPolicy` (`DatePolicyTests.cs:169`) iterates
    `Enum.GetValues<DatePolicy>()` and must construct a `DateCutoff` per member — including a dated one for
    `AsOf` — while keeping the distinct-and-non-empty assertion. `OneResolvedToday_MakesEveryConsumerAgree`
    (`:102`) iterates an explicit three-policy array; add the fixed policy to it.
  - [x] AC #1's real invariant needs the **production** wiring, not the pure resolver. Follow the pattern 5.5's
    review installed: extend
    `SiteGeneratorCommitDetailsTests.GenerateAll_LastCommitPolicy_LinkedHeatmapDaySetMatchesGeneratedDayPageSet`
    with an `--as-of` sibling running a real `SiteGenerator` against the real git fixture, asserting the linked
    heatmap day set **equals** the actual `commits/*.html` file set. **Pair it with the D2 counter-test:** an
    as-of date before the fixture's first commit ⇒ zero `commits/*.html`, no exception, and a heatmap whose
    accessible name names no commit outside the window. A guard fix without a counter-test is how five vacuous
    tests shipped in this epic.
  - [x] `SettingsStoreTests`: the composite token round-trips through `.specscribe/config.json`; a legacy
    `"TodayPolicy": "Utc"` still loads; **and the red-green case AC #2 names — an unrecognized token (e.g.
    `"as-of:nope"`) must leave `Source`/`Adrs`/`Output`/`ProjectName`/`DeepGit`/`CodeUrl`/`IncludeReadme` intact.**
    Write that one as red-first: it is the defect this requirement exists for.
  - [x] `SettingsResolverTests`: `--as-of` provenance on `today_policy` (CLI > saved > default); `Validate()`
    rejects a bad date; `Validate()` rejects the `--today-policy utc --as-of …` conflict.
  - [x] `DiagnosticsTemplaterTests`: the config row renders the fixed-date label + `--as-of` provenance.
  - [x] **Scope guard (INVERTED vs Story 5.5): `GoldenContentFingerprint` MUST NOT MOVE.** It is
    `2bd1c18e30c16cddb4ae62909979730161bff1f9486ec9acce0f9b4636b2beae`
    ([SiteGeneratorAdapterTests.cs:1211](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)). At the
    default policy this story renders nothing differently: the diagnostics row's default text is unchanged, the
    new console line is conditional, and the golden fixture **is not a git repo** — so no heatmap, no
    `timeline.html`, and no `commits/` page is emitted there at all, meaning even Task 4 cannot touch it.
    **If the hash moves, do not regenerate — find out why.** See "If the golden hash moves" below.

### Review Findings

- [x] [Review][Patch] Interactive "Configure paths" menu ignores `settings.AsOf` when deciding the pre-selected current cutoff — `ConfigurePaths` derives `currentCutoff` only from `DatePolicies.TryParse(settings.TodayPolicy, ...)` (Commands.cs:615), never consulting `settings.AsOf`. Launching `specscribe --as-of 2026-07-27` (no `--today-policy`) and then choosing "Configure paths" pre-selects MachineLocal instead of the fixed date actually in effect for the run — directly contradicting Task 3's "when the current policy is already AsOf, pre-select it and seed the TextPrompt default with the existing date" and the surrounding comment's own "never silently flips it" guarantee. Fixed: `currentCutoff` now checks `settings.AsOf` first, with the same AsOf-first precedence `SiteSettings.ResolveDateCutoff()`/`SettingsStore.ResolveCutoffOrNull` use. [src/SpecScribe/Commands.cs:615]
- [x] [Review][Patch] `DatePolicies.Label`/`Token` disagree for a dateless `DateCutoff(AsOf, null)` — `Label` returns `"fixed date"` for that state while `Token` degrades to `"machine-local"`. `ForgeOptions.Resolve(...)` is a public NFR8 library entry point not gated by `SiteSettings.Validate()`, so a library caller constructing `new DateCutoff(DatePolicy.AsOf, null)` directly renders the self-contradicting `DiagnosticsTemplater` row `"fixed date (--today-policy machine-local)"`. Unreachable via the validated CLI path. Fixed: `DiagnosticsTemplater`'s switch gained an explicit dateless-`AsOf` arm rendering the MachineLocal row instead of falling into the generic arm (left `Label`'s menu-only "fixed date" phrasing untouched, since the interactive menu depends on it). [src/SpecScribe/DiagnosticsTemplater.cs:301]
- [x] [Review][Patch] `Commands.cs`'s interactive date prompt discards `DatePolicies.TryParseAsOfDate`'s success bool after `AnsiConsole.Prompt(datePrompt)` returns, relying entirely on the prompt's own `.Validate()` delegate to guarantee a parseable value. Safe today only because the two call sites (the delegate and the post-prompt call) independently re-implement the same check — a future drift would silently produce `as-of:0001-01-01` instead of erroring, the opposite of this story's own reject-don't-silently-degrade discipline (NFR8). Fixed: the bool is now checked, throwing `InvalidOperationException` if it ever disagrees with the prompt's own validator. [src/SpecScribe/Commands.cs:652]
- [x] [Review][Patch] New `CommitHeatmap` comment overclaims parity with its cited "already-migrated twin" — `DeliveryCadenceHeatmap`'s empty state still collapses "no data at all" and "cutoff excludes everything" into one generic `"No completed stories to chart yet."`, while `CommitHeatmap`'s new empty state specifically names the cutoff (`"No commits on or before {date}."`). Not a functional defect in this story's own code (`CommitHeatmap`'s message is arguably better) — just an inaccurate comment; fixing `DeliveryCadenceHeatmap` itself is out of this story's scope. Fixed: comment corrected to name the gap rather than claim identical shape. [src/SpecScribe/Charts.cs:837]
- [x] [Review][Patch] AC #2a's new `ConsoleUi.PrintPaths` echo line has no automated test — there is no `ConsoleUiTests.cs` anywhere in the test project, so this requirement is verified only by the manual CLI transcript in the Dev Agent Record, not by CI. Fixed: the line's content logic was split into a new pure, Spectre-free `ConsoleUi.FormatPinnedCutoffLine` (matching the `CliFeedbackTests`/`GenerationSummary.FormatLine` "don't test Spectre, test us" convention), covered by a new `ConsoleUiTests.cs` (5 tests: pinned date, MachineLocal/Utc/LastCommit all null, and the dateless-`AsOf` degrade). [src/SpecScribe/ConsoleUi.cs:90, tests/SpecScribe.Tests/ConsoleUiTests.cs]
- [x] [Review][Defer] `DateCutoffJsonConverter.Read` calls `reader.GetString()` unconditionally on any non-null JSON token; a hand-edited `.specscribe/config.json` with `"TodayPolicy": 123` (or `true`/an object) throws `InvalidOperationException`, which `TryReadCandidate`'s `catch (IOException or JsonException)` does not catch — crashing settings load instead of the intended per-field graceful degrade this converter's own doc comment promises. Pre-existing and unchanged from the Story 5.5 `DatePolicyJsonConverter` this story retyped; not introduced by 5.7. [src/SpecScribe/SettingsStore.cs:92] — deferred, pre-existing
- [x] [Review][Defer] `DatePolicyTests.OneResolvedToday_MakesEveryConsumerAgree` asserts `linked == generated` where both sides call `Charts.LinkedCommitDays` with identical arguments — the assertion cannot fail regardless of whether `_today` threading is correct, so it proves nothing about AC #1's single-resolution guarantee. This story extended the test's policy array to include the new `AsOf` case without fixing the underlying tautology, despite the story's own Dev Notes warning explicitly against exactly this anti-pattern ("If a test would still pass with the guard deleted, it is not a test"). Pre-existing from Story 5.5; not a regression introduced here — real AC #1 coverage comes from the separate `SiteGeneratorCommitDetailsTests` production-wiring test. [tests/SpecScribe.Tests/DatePolicyTests.cs] — deferred, pre-existing

**Dismissed as noise (3):** `DateCutoff`'s Policy/AsOf pairing isn't self-validating at construction — consistent with this codebase's boundary-validation convention, and `ResolveToday` ignores the stray field safely rather than crashing. `SettingsStore.Capture`'s "a disagreeing pair never reaches here" doc claim only holds for the gated CLI/interactive path; a direct library call bypassing `Validate()` would resolve toward AsOf silently — matching `Capture`'s own documented never-throw design, not a functional break. AC #2b's conflict check compares the whole `DateCutoff` (catching disagreeing dates under the same policy) rather than just `Policy`, wider than Task 2's literal wording — explicitly disclosed in the Dev Agent Record as a deliberate, tested choice, not a silent deviation.

**Review scope note:** `tests/SpecScribe.Tests/DiagnosticsTemplaterTests.cs` carries two tests (`FromRun_UnmodeledModule_ShowsItsRealLabel_NotNotDetected`, `FromRun_ModuleWithNoLabel_FallsBackToNotDetected_RatherThanABlank`) bundled into the same commit range that belong to Story 18.2, not this story — excluded from the findings above as out of scope, alongside the previously-noted Story 18.6/20.9/accessibility-sweep churn in `Charts.cs`/`ChartsTests.cs`/`SiteGenerator.cs`.

**Verification completed (2026-07-28):** `dotnet build` succeeds. The 5 patched surfaces plus their existing coverage (`ConsoleUiTests`, `DatePolicyTests`, `DiagnosticsTemplaterTests`, `ChartsTests`, `SettingsResolverTests`, `SettingsStoreTests`, `SiteGeneratorCommitDetailsTests` — 317 tests) all pass. Full suite: 2662 passed / 1 failed / 3 skipped — the one failure, `IdeasTests.Discover_MalformedMemlog_StillListsTheIdeaAndReportsMalformed`, is in `IdeaDiscovery.cs`/`IdeasModel.cs`, both mid-edit by a concurrent session's uncommitted Story 18.4 work at review time, and touches none of this story's File List; not a regression from these patches.

## Dev Notes

### Follow the settings-stack precedent verbatim

`--deep-git` (3.2), `--code-url` (7.7) and `--today-policy` (5.5) are the same end-to-end template, and this
story is the fourth pass through it: CLI option on `SiteSettings` → `ForgeOptions` property + `Resolve`
parameter → `SavedSettings` tri-state with `IsEmpty` participation, persist-only-non-default, CLI-wins `ApplyTo`
→ `SettingsResolver` provenance entry → interactive prompt in `ConfigurePaths` → diagnostics `<dl>` row. Match
the shape and the mechanical half is low-risk. The genuinely new thinking is only three things: the record shape
(Task 1), the interactive date prompt (Task 3), and the heatmap text bound (Task 4).

### Why the shape change is unavoidable

`DatePolicy` is a bare enum and both string surfaces are pure `enum → string`:
`Token(DatePolicy)` and `Label(DatePolicy)`. A date cannot ride an enum member, and `Token`→`TryParse` round-trip
is already an asserted invariant that persistence and `--show-config` both depend on. Either the token carries
the date or the function signature does. D1 chose "the token carries the date" — which is also why the record,
not a second parallel `DateOnly?` field on `ForgeOptions`, is the right container: one value means one thing to
persist, one thing to attribute, one thing to log.

### Degradation (NFR8) — four cases, three already precedented

| Case | Behavior |
|---|---|
| `--as-of` unparseable | Reject at `Validate()` with an actionable message. A typo that silently no-ops is a worse failure than an error. |
| `--as-of` + conflicting `--today-policy` | Reject at `Validate()` naming both flags. Never let one silently win. |
| `as-of:` token unrecognized in `.specscribe` | Degrade to "not configured" in the converter. Never throw, never lose sibling fields. |
| `--as-of` before the first commit | **(D2)** Accept. Empty commit-date-page set, honest heatmap text, no warning. |

### Explicitly out of scope — declare, do not widen

- **`GitPulsePanel`'s signal strip also overclaims under a past `--as-of`.** `git.Last30DayCommitCount`,
  `git.ActiveDays` and `git.LastCommitTimestamp` are pre-computed on the `GitPulse` model by `GitMetrics` and are
  **not** bounded by `todayValue` ([Charts.cs:1387-1394](../../src/SpecScribe/Charts.cs)). The last-commit *link*
  is correctly suppressed (it goes through `LinkedCommitDays`), but the figures beside it still count past the
  cutoff. Bounding them means reaching into `GitMetrics`, and `Last30DayCommitCount`'s own 30-day window is one
  of the `DateTime.Now` reads Story 5.5 deliberately left **off** the cutoff path. D2 scoped this story to
  `CommitHeatmap`, whose figures are computed in-method and therefore cheap to bound. Raise it at close
  (Question 1), do not fix it here.
- Do not touch the three shipped policies, their tokens, or `LastCommit`'s `series.Max(day)` semantics.
- No new page, no nav change, no new asset, no CSS, no JS. Charts stay pure server-rendered SVG.

### If the golden hash moves

The golden-fingerprint **harness itself was defective** through most of Epic 5: `NormalizeVolatile` ran
`FoldToday` before `BuildRow`, whose `[^<]*` class could not cross the injected `<date-iso>` placeholder, so the
short commit SHA leaked into the hash and the constant drifted on **every commit**. Story 5.2 found it by
**declining to regenerate**. The rule that came out of the retro:

> **When the hash moves and you did not touch rendering, audit the normalizer BEFORE regenerating.
> Regeneration is the move that hides it.**

For this story the bar is higher still — AC #1b says the hash must not move at all. Also note `.gitattributes`
now pins `eol=lf` and `FoldLineEndings` is deliberately kept anyway (see the uncommitted comment change in
`SiteGeneratorAdapterTests.cs`), so a CRLF checkout is not a candidate explanation.

### Shared-`main` hazards (this repo, right now)

- Another session may be editing the same files. **Grep-verify every new symbol** (`DateCutoff`, `AsOf`,
  `ResolveDateCutoff`, the `--as-of` option) actually landed before trusting a green build — a `Charts.cs` edit
  has silently vanished this way before. A zero-grep can also be a transient mid-write read; confirm with
  `git diff HEAD`.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** Uncommitted work in the tree at story start
  includes `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (a doc-comment change about `.gitattributes`)
  and `web/scripts/ir-content-build.mjs`, plus an untracked `.gitattributes`. None of it is this story's.
- Expect the commit to bundle sibling stories — code review runs at epic end, scoped by this story's own
  File List and declared symbols, never by a commit range.

### Testing standards summary

- xUnit under `tests/SpecScribe.Tests/`. Prefer pure unit tests on `TryParse`/`Token`/`ResolveToday` and the
  settings round-trip; reserve full-generation tests for the AC #1 production-wiring invariant and the D2 empty
  window.
- **Every guard fix gets a counter-test.** Epic 5 shipped five tests that passed while never reaching the branch
  their name claimed — including one that "proved" a pure function was referentially transparent instead of
  touching `_today` at all. If a test would still pass with the guard deleted, it is not a test.
- Never assert on a `DateTime.Now`-derived value without pinning it; `--as-of` is the first policy where the
  expected value is an *input*, which makes these the easiest deterministic tests in the epic — use that.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 5.7] — the two ACs and the seeding rationale (lines 1035–1070).
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml] — the `5-7-…` key's owner-decision record.
- [Source: _bmad-output/implementation-artifacts/5-5-configurable-date-page-today-cutoff.md] — the inherited invariant, the five consumers, the `dateCutoff` split, and the four review patches this story must not regress.
- [Source: _bmad-output/implementation-artifacts/epic-5-retro-2026-07-27.md] — the retro that seated this story; the golden-normalizer defect and the vacuous-test pattern.
- [Source: src/SpecScribe/DatePolicy.cs] — `DatePolicy` enum + `DatePolicies` `TryParse`/`Token`/`Label`/`RejectionMessage`.
- [Source: src/SpecScribe/Charts.cs] — `ResolveToday` and `LinkedCommitDays` (co-located, ~:2357–2391); `CommitHeatmap` (:829); `GitPulsePanel` (:1364); `HeatLevel` (:4385).
- [Source: src/SpecScribe/Charts.cs:1036-1055] — `DeliveryCadenceHeatmap`, the already-shipped `visible`-bounding + empty-state pattern Task 4 ports into `CommitHeatmap`.
- [Source: src/SpecScribe/SiteGenerator.cs:58-73] — `_today` + `RefreshToday()`, the single-resolution seam.
- [Source: src/SpecScribe/SiteSettings.cs:42-92] — `--today-policy`, `Validate()`, `ResolveDatePolicy()`.
- [Source: src/SpecScribe/SettingsStore.cs:32-92,209-294] — `SavedSettings.TodayPolicy`, `DatePolicyJsonConverter`, `Capture`/`ApplyTo`.
- [Source: src/SpecScribe/SettingsResolver.cs:33-53,74-84,133-155,179-194] — `CliOverrides`, `Fields`, `BuildProvenance`, `FormatConfigLines`.
- [Source: src/SpecScribe/Commands.cs:610-632] — `ConfigurePaths`'s policy `SelectionPrompt`.
- [Source: src/SpecScribe/ConsoleUi.cs:67-88] — `PrintPaths` and its conditional-line precedent.
- [Source: src/SpecScribe/DiagnosticsTemplater.cs:135,158,294-296] — `DiagnosticsConfig.DatePolicy` and the config `<dl>` row.
- [Source: docs/adrs/0013-text-twin-is-the-no-js-contract.md] — why the heatmap headline and `aria-label` must be bounded together.
- [Source: docs/adrs/0014-specscribe-settings-folder-format.md] — `.specscribe/config.json` as the persistence target.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:1020] — the missing Spectre TTY harness that makes Task 3 unverifiable.
- [Source: CLAUDE.md] — shared-`main` conventions, `SpecScribeOutput/` as the only generate target, and epic-end review scoping.

## Questions for the Owner (raised at story close, not blocking)

1. **Git Pulse signal strip.** Under a past `--as-of`, the strip still shows unbounded `Last30DayCommitCount` /
   `ActiveDays` / last-commit timestamp beside a correctly-suppressed link (see "Explicitly out of scope").
   Bound them in a follow-up, or accept that the pinned cutoff governs *pages and links* while the Git Pulse
   figures describe the repo as it actually is today?
   **Live numbers, measured at close** (this repo, `--as-of 2026-07-20 --deep-git`, real "today" 2026-07-28):
   the strip reports **376** commits in the last 30 days and **25** active days, immediately above a heatmap
   correctly bounded to **301** commits across **17** active days. The last-commit timestamp (Jul 28) renders as
   **plain text, not a link** — the `LinkedCommitDays` guard held, so there is no dead link, only a numeric
   disagreement between two panels on the same page. That makes this a truthfulness question (ADR 0013's text
   twin sits between them), not a broken-link one.
2. **`watch` with a pinned cutoff.** `--as-of` means a long-running `specscribe watch` never advances its date
   cutoff, so commits authored during the session get no date page until the flag is dropped. Correct by design
   for a snapshot — worth a one-line note on the How-to-use page (Story 5.6), or leave it undocumented?

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, `bmad-dev-story`), 2026-07-28.

### Debug Log References

- Full suite after implementation: **2657 passed / 0 failed / 3 skipped** (2 m 3 s).
  One earlier full-suite run also reported `FileWatcherServiceTests.WatchedSourceFileStaysWritableAndDeletableDuringRegeneration`
  failing; it passes in isolation (8/8) and touches nothing in this story's File List — the known
  pre-existing concurrency flake already recorded on the 5.1 review key, not a regression from here.
- Live CLI verification (this repo, `SpecScribeOutput/`, the default target per CLAUDE.md):
  - `generate --as-of 2026-07-20 --deep-git` → `errors=0`; console echoed
    `i Date-page cutoff pinned to 2026-07-20 (--as-of)`.
  - `generate --as-of "07/20/2026" --show-config` → `field=today_policy origin=commandline value=as-of:2026-07-20`
    — the forgiving parse canonicalized to ISO in the echo, which is the whole justification for D3's forgiveness.
  - `generate --as-of 2000-01-01` and `--as-of 2000-01-01 --deep-git` → `errors=0`, **0** `commits/*.html`,
    **0** `aria-label="Commit activity:`, **0** `href="commits/`, and the designed empty state
    `No commits on or before Sat, Jan 1, 2000.` on both the dashboard and `git-insights.html`.
  - Rejections: `--as-of lastweek`, `--today-policy utc --as-of 2026-07-20` (conflict), and
    `--today-policy yesterday` (whose message now also names `--as-of`) all fail with the intended actionable text;
    the agreeing pair `--today-policy as-of:2026-07-20 --as-of 2026-07-20` is accepted.

### Completion Notes List

**All 6 tasks and both ACs are implemented and verified, with one honest exception recorded below (Task 3).**

- **AC #1 / #1a — the explicit date is the run's single resolved `today`.** No sixth resolution point was added:
  `SiteGenerator._today` still comes from one `RefreshToday()` call, now over `_options.DateCutoff`. Proven
  through the REAL production wiring by a new `--as-of` sibling of the 5.5 integration test (linked heatmap day
  set **equals** the actual `commits/*.html` file set) and its D2 counter-test (a pre-history pin ⇒ zero day
  pages, no exception, and a heatmap naming no commit outside the window). Live confirmation above.
- **AC #1b — the golden fingerprint did not move. ⚠️ But the story's quoted literal was already stale.**
  The story cites `2bd1c18e30c16…`; that IS the value at this story's `baseline_commit` (d1722f17), but HEAD
  carries `f4a7cbac5bee0fe56aa4ef9950a114a23acc8b2d59eb2e255e4b47e27873f0cd` — moved by a **sibling session's
  committed work** between create-story and dev-story (the constant's own comment block attributes the last
  regeneration to the Story 18.2 code review, 2026-07-27). Per the story's own "if the hash moves, do not
  regenerate — find out why" rule I did not touch it: the current constant was left untouched and the test is
  green, which is exactly the guarantee AC #1b asks for (this story renders nothing differently at the default
  policy). Verified by `git show d1722f17:… | grep expected` vs `git show HEAD:…`.
- **AC #2 / #2a / #2b — one field, one provenance entry, two ways to set it.** No new `SettingsResolver.Fields`
  constant; `today_policy` keeps carrying the composite `as-of:{iso}` token through `--show-config`, the
  diagnostics `<dl>`, and `.specscribe/config.json`. `Token` → `TryParse` round-trips by construction and is
  pinned by tests. The converter was EXTENDED, not replaced, and the red-green sibling-field test is written the
  way the requirement asks: an unrecognized `"as-of:nope"` degrades that one field to null while
  `Source`/`Adrs`/`Output`/`ProjectName`/`DeepGit`/`CodeUrl`/`IncludeReadme` all survive intact.
- **A guard the story did not name, found while wiring Task 2.** `SettingsStore.ApplyTo` had to learn about
  `--as-of`: without it, a saved `"utc"` would be restored on top of an explicit `--as-of` and manufacture
  exactly the disagreement `Validate()` rejects — turning a valid command line into an error because of a file
  the user never mentioned. Guarded and pinned by `ApplyTo_DoesNotOverrideAnExplicitAsOfWithASavedPolicy`.
- **A conflict case the story's rule left ambiguous, resolved toward its stated spirit.** Task 2's ordering says
  the conflict is "`TodayPolicy` resolves to something other than the fixed policy". Read literally, that makes
  `--today-policy as-of:2026-01-01 --as-of 2026-07-27` (same policy, DIFFERENT dates) *not* a conflict, with no
  rule for which date wins. Both surfaces therefore compare the whole `DateCutoff`, so disagreeing dates are
  rejected too. The story's explicit non-conflict case (identical values) is preserved and tested.
- **Task 4 ported the sibling, it did not invent a fix.** `CommitHeatmap` now derives `byDay`, `firstCommit`,
  `lastCommit`, `totalCommits`, `activeDays` and `maxCount` from `visible`, with `DeliveryCadenceHeatmap`'s
  early-return empty state as the crash guard. `HeatLevel` was left alone (verified: `maxCount == 0` cannot
  divide by zero). The three assertions the story asked me to verify rather than assume
  (`ChartsTests.cs:1437`, `GitInsightsTemplaterTests.cs:250`, `HtmlTemplaterTests.cs:298`) all still pass
  untouched — their fixtures carry no post-cutoff days, exactly as predicted.
- **⚠️ One existing test DID have to change, and it is a real behavior change, not a test fix.**
  `ChartsTests.CommitHeatmap_FutureDatedFirstCommit_KeepsValidGridWithoutOrphanCaption` fed an all-future
  series and asserted a positive-width grid. That series now has no visible day, so it takes the new empty
  state — which is the pre-existing future-skew overclaim D2 explicitly says this story also fixes (the old
  render drew a zero-cell grid whose aria-label still announced "2 commits"). Split into two tests: one pinning
  the empty state, one keeping the original mark/caption + positive-week coverage on a series that has a
  visible day, plus a third with an explicit deterministic `today:` cutoff. A consequence worth flagging at
  review: `if (start > end) start = end;` in `CommitHeatmap` is now **unreachable** (`firstCommit` is drawn from
  `visible`, so it can never exceed the cutoff). It was kept because the already-migrated
  `DeliveryCadenceHeatmap` keeps the identical line for the identical reason, and Task 4 says port that shape
  verbatim — but "delete both, or keep both" is a legitimate review question.
- **Honest limits on verification — Task 3 (interactive parity) is NOT verified, by design.** The
  `ConfigurePaths` selection prompt and the new gated `TextPrompt` date follow-up are implemented (including
  the pre-selected current policy, the seeded existing date, and the preserved yellow unrecognized-token
  warning), but **no agent harness can exercise them**: every tool harness captures stdout, so Spectre reports
  `Interactive == false` and `AnsiConsole.Prompt` cannot run. This is the permanent blind spot recorded
  independently by Stories 5.1, 5.2 and 5.5 and confirmed at the Epic 5 retro; the missing
  `Spectre.Console.Testing`/`CommandContext` harness is logged at `deferred-work.md:1020`. Left for the owner
  to drive by hand, exactly as 5.5's equivalent finding did. Everything reachable from that path *without* a
  TTY is covered: the token the menu writes, its round trip, and the `settings.AsOf = null` reset.
- **Out of scope, confirmed live rather than assumed.** Under `--as-of 2026-07-20` the Git Pulse signal strip
  still reports **376** commits in the last 30 days and **25** active days beside a heatmap correctly bounded to
  **301** across **17** — the documented `GitMetrics`-side overclaim. Its last-commit link IS correctly
  suppressed (Jul 28 rendered as plain text, no dead link), exactly as the story predicted. Real numbers now
  attached to Question 1 below.

### File List

**Production**

- `src/SpecScribe/DatePolicy.cs` — `DatePolicy.AsOf`; new `DateCutoff` record struct; `TryParse`/`Token`/`Label`
  retyped; new `AsOfTokenPrefix`, `TryParseAsOfDate`, `AsOfRejectionMessage`, `ConflictMessage`;
  `RejectionMessage` now names `--as-of`.
- `src/SpecScribe/Charts.cs` — `ResolveToday(DateCutoff, …)` + the `AsOf` arm and its degrade; `CommitHeatmap`
  bounded to `visible` with the ported empty-state crash guard; the two library-caller defaults retyped.
- `src/SpecScribe/ForgeOptions.cs` — `DatePolicy` property → `DateCutoff`; `datePolicy` parameter → `dateCutoff`.
- `src/SpecScribe/SiteSettings.cs` — `--as-of <DATE>` option; ordered three-check `Validate()`;
  `ResolveDatePolicy()` → `ResolveDateCutoff()` with the conflict backstop.
- `src/SpecScribe/SettingsStore.cs` — `SavedSettings.TodayPolicy` retyped; `DatePolicyJsonConverter` →
  `DateCutoffJsonConverter` (Write now emits the token); `ResolvePolicyOrNull` → `ResolveCutoffOrNull`;
  `ApplyTo` gained the `--as-of` guard.
- `src/SpecScribe/SettingsResolver.cs` — `CliOverrides.TodayPolicy` true for either flag; provenance reads
  `options.DateCutoff`.
- `src/SpecScribe/Commands.cs` — `ConfigurePaths` gained the gated date `TextPrompt`, the date-carrying label
  converter, and the `settings.AsOf` reset.
- `src/SpecScribe/ConsoleUi.cs` — the conditional pinned-cutoff echo line in `PrintPaths`.
- `src/SpecScribe/DiagnosticsTemplater.cs` — `DiagnosticsConfig.DatePolicy` → `DateCutoff`; the config `<dl>`
  row names `--as-of` for the fixed policy.
- `src/SpecScribe/SiteGenerator.cs` — `RefreshToday()` reads `_options.DateCutoff` (+ its doc `<see cref>`).

**Tests**

- `tests/SpecScribe.Tests/DatePolicyTests.cs` — retyped throughout; composite-token parse/reject/round-trip,
  invariant-culture pinning, the two `AsOf` resolver cases, the dateless-token degrade, the three message
  helpers, and the enum-iterating `Label` test now constructing a dated cutoff.
- `tests/SpecScribe.Tests/SettingsStoreTests.cs` — retyped; fixed-date round trip through
  `.specscribe/config.json`, ISO canonicalization, legacy `"Utc"`/`"LastCommit"` still loading, the red-green
  sibling-field survival test, and the `ApplyTo` `--as-of` guard.
- `tests/SpecScribe.Tests/SettingsResolverTests.cs` — retyped; `--as-of` provenance (CLI > saved > default),
  the `--show-config` wire line, both new `Validate()` rejections, the agreeing pair, the `ResolveDateCutoff`
  backstop, and the unadvertised composite token on `--today-policy` alone.
- `tests/SpecScribe.Tests/DiagnosticsTemplaterTests.cs` — helper retyped; new pinned-date row test.
- `tests/SpecScribe.Tests/ChartsTests.cs` — the future-skew test split in two (empty state / still-valid grid)
  plus a deterministic explicit-cutoff test bounding aria-label and headline together.
- `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs` — retyped call; the `--as-of` production-wiring
  sibling, the D2 counter-test, and two shared helpers.

**Process artifacts**

- `_bmad-output/implementation-artifacts/5-7-fixed-as-of-date-page-cutoff-policy.md` (this file)
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-28 — Code review (scoped to this story's File List, excluding bundled Story 18.2/18.6/20.9/accessibility-sweep churn in the same commits). 5 patch findings applied: the interactive "Configure paths" menu now recognizes an `--as-of`-only launch when pre-selecting the current cutoff (previously silently reverted to machine-local); `DiagnosticsTemplater`'s config row no longer disagrees with itself for a dateless `AsOf` cutoff; the interactive date prompt's parse-success bool is now checked instead of discarded; a comment overclaiming parity with `DeliveryCadenceHeatmap` was corrected; and AC #2a's console echo line gained a pure-function unit test (new `ConsoleUiTests.cs`, plus a `ConsoleUi.FormatPinnedCutoffLine` extraction following the codebase's "don't test Spectre, test us" convention). 2 pre-existing findings deferred (a JSON converter crash risk on malformed `.specscribe` data; a tautological existing test), 3 dismissed as noise. Full suite 2662 passed / 1 failed (unrelated, concurrent-session Ideas work) / 3 skipped. Status → done.
- 2026-07-28 — Implemented. `--as-of <DATE>` ships end to end: a fourth `DatePolicy.AsOf` member carried on a new
  `DateCutoff` record struct (`default` = the machine-local status quo, by construction), a composite
  `as-of:{iso}` token that rides the existing single `today_policy` field through the CLI, `.specscribe`
  persistence, `--show-config` and the diagnostics `<dl>`, an ordered three-check validation gate (bad policy →
  bad date → conflict) mirrored as a `ResolveDateCutoff()` backstop, a gated interactive date prompt, an ISO
  echo on ordinary runs, and `CommitHeatmap`'s summary text bounded to the rendered window (porting
  `DeliveryCadenceHeatmap`'s already-shipped shape, which also fixes the pre-existing future-skew overclaim).
  Suite 2657 passed / 0 failed / 3 skipped; golden fingerprint NOT moved. Two deviations recorded in the
  Completion Notes: the story's quoted golden literal was already stale (a sibling session moved it after this
  story's baseline — the CURRENT constant was left untouched and green), and one existing ChartsTests
  future-skew test was split because the all-future case now correctly renders the designed empty state. Task 3
  (interactive parity) is implemented but NOT verified — the permanent Spectre-TTY blind spot,
  `deferred-work.md:1020`. Status → review.
- 2026-07-27 — Story 5.7 seeded from the Epic 5 retrospective and contexted. Three owner decisions locked at create-story: D1 `--as-of <DATE>` implies the policy and collapses onto the single `today_policy` field as a composite `as-of:<iso>` token; D2 an out-of-range date is accepted verbatim with the heatmap's headline + `aria-label` bounded to the rendered window; D3 forgiving `DateOnly.TryParse` (invariant culture) with the resolved ISO date echoed on an ordinary run. Status → ready-for-dev.
