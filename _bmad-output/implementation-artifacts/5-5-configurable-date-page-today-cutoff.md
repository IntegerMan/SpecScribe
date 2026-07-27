---
baseline_commit: f9b52bd8557920e4f387d4017c924f63a5d38e19
---

# Story 5.5: Configurable Date-Page "Today" Cutoff (Timezone Policy)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer generating the portal across machines or timezones,
I want to choose how SpecScribe decides which calendar day is "today" when linking and generating date pages,
so that date-page membership stays predictable for my team's timezone policy without changing the author-offset honesty of commit times.

## Context & Origin (read first)

This story was **seeded 2026-07-20 from the Story 10.4 code review** ([10-4-consistent-dates-and-event-sequencing.md:198](10-4-consistent-dates-and-event-sequencing.md)):

> `LinkedCommitDays` "today" uses machine-local `DateTime.Now` while commit days are author-offset — **resolved (owner 2026-07-20): keep machine-local as default**; future directory-scoped + CLI policy seated as Epic 5 Story 5.5.

**The concrete defect this addresses (rare, real):** the set of days that get a generated `commits/{date}.html` page — and the guarded links pointing at them — is filtered by `s.Day <= today` where `today = DateOnly.FromDateTime(DateTime.Now)` (the **generating machine's** local wall-clock day). But git commit days are derived from each commit's **authored offset**. Generate the same repo on a laptop in New York just before midnight vs. a UTC CI runner just after, and a commit dated "today" in one timezone is "tomorrow" (future-skewed, and so excluded) in the other. The two clocks can name different calendar days at the boundary, so date-page membership is machine-dependent.

**Story 10.4's owner ruling stands:** machine-local is the honest, deterministic *default* and must stay the default (AC #1). This story does **not** change the default behavior — it **exposes the "today" decision as a policy** (directory-scoped setting + CLI override, per Story 5.2's parity contract) so a team can opt into UTC or an author-derived cutoff when their build topology needs it. Commit *times* stay rendered in each commit's authored offset regardless of policy — this only governs which calendar day counts as "today" for the date-page cutoff.

## Acceptance Criteria

1. **Default is byte-identical to Story 10.4's status quo.**
   **Given** the default configuration (no `--today-policy` flag and no saved setting)
   **When** the portal generates date pages and date links
   **Then** "today" remains the generating machine's local calendar day (`DateOnly.FromDateTime(DateTime.Now)` — the Story 10.4 status quo)
   **And** git commit times continue to render in each commit's authored offset (never a `format-local:` / UTC *time* conversion — this story touches only the day-cutoff, never how a timestamp is displayed)
   **And** the only generated-output delta versus today is the single new "Effective configuration" row on `diagnostics.html` (AC #2's provenance requirement); every other page is byte-for-byte unchanged.

2. **The policy is configurable, consistent across all three consumers, and surfaced as provenance.**
   **Given** I set a directory-scoped setting and/or CLI override for the date-page today policy
   **When** generation runs
   **Then** the chosen policy is applied **consistently** to `LinkedCommitDays` (heatmap link set), date-page generation (`commits/{date}.html` set), and every guarded date link (`ChangeLogDayHref`, the Git Pulse last-commit link) — computed once per run and shared, so the linked set and the generated set can never disagree (no dead links, no orphaned pages)
   **And** at least these policies are supported: **machine-local** (default), **UTC calendar day**, and an **author-local-derived cutoff** (the latest authored commit day, i.e. `max` of the daily series)
   **And** the effective policy + its provenance (default vs. saved vs. CLI-override) appear on the diagnostics / config-log surface (Story 4.8) with interactive/CLI parity (NFR7 / Story 5.2).

## Tasks / Subtasks

- [x] **Task 1 — Introduce the policy type and a single `today` resolver** (AC: #1, #2)
  - [x] Add a `DatePolicy` enum (values `MachineLocal`, `Utc`, `LastCommit`) — new file `src/SpecScribe/DatePolicy.cs` (with a `DatePolicies` parse/label/token helper). `MachineLocal` is the zero/first value so `default(DatePolicy)` == the status quo.
  - [x] Added pure static resolver `Charts.ResolveToday(DatePolicy policy, IReadOnlyList<(DateOnly Day, int Count)>? series)`, co-located next to `LinkedCommitDays`:
    - `MachineLocal` → `DateOnly.FromDateTime(DateTime.Now)` (verbatim — the `_ =>` arm).
    - `Utc` → `DateOnly.FromDateTime(DateTime.UtcNow)`.
    - `LastCommit` → `series` non-null/non-empty ⇒ `series.Max(s => s.Day)`; **degrades to `MachineLocal` when `series` is null/empty** (documented on the method + in Completion Notes).
  - [x] Unit-tested the resolver directly in `DatePolicyTests`: all three policies + the `LastCommit`-without-git fallback (both null and empty) + `LastCommit` == series max + the future-dated-max case.

- [x] **Task 2 — Compute the run's `today` once and thread it to all five call sites** (AC: #2 — this is the #1 review checkpoint)
  - [x] `SiteGenerator` computes the run's resolved `today` **once** into a private `_today` field via `RefreshToday()`, called wherever `_progress` is (re)assigned (both `GenerateAll` and the incremental watch path) and seeded in the constructor.
  - [x] Replaced every in-place `DateOnly.FromDateTime(DateTime.Now)` on the date-cutoff path with the shared field / a threaded parameter. The five sites:
    1. `GenerateDatePagesInternal` → `Charts.LinkedCommitDays(..., _today)`.
    2. artifact-by-day gather: `var today = _today;` (future-skew skip guard).
    3. `ChangeLogDayHref` → `Charts.LinkedCommitDays(..., _today).Contains(date)`.
    4. `Charts.CommitHeatmap` — added `DateOnly? today = null` param (defaults to machine-local), threaded from the SiteGenerator call sites via `TimelineTemplater.RenderPage` and `GitInsightsTemplater.RenderPage` and the dashboard's Git Pulse (`GitPulsePanel`).
    5. `Charts.GitPulsePanel` — added `DateOnly? today = null` param; threads the resolved value into its `LinkedCommitDays` guard AND its embedded `CommitHeatmap`.
  - [x] Grid-extent / future-skew *semantics* unchanged — only the *value* of "today" now flows from one policy-resolved source (noted in Dev Notes below).
  - [x] Grep-verified: the remaining `DateOnly.FromDateTime(DateTime.Now)` occurrences are all OFF the date-cutoff path (artifact-staleness `today` in `BuildArtifactCoverage`, the cadence page's own bound, `GitMetrics.CountCommitsInLastDays`'s 30-day window, the two `Charts` `?? today` defaults for library callers, and the resolver's own machine-local arm). **Note:** the dashboard's existing `today` param drives artifact *staleness*, so I threaded the cutoff as a SEPARATE `dateCutoff` parameter through `RenderDashboardBody`/`BuildIndexPage` rather than conflating the two — folding them would let `last-commit` on an idle repo report every artifact as fresh.

- [x] **Task 3 — Thread the policy through the settings/options stack** (AC: #2)
  - [x] `ForgeOptions`: added `public DatePolicy DatePolicy { get; init; }` (not `required`, defaults `MachineLocal`) + a `DatePolicy datePolicy = DatePolicy.MachineLocal` parameter on `Resolve(...)`.
  - [x] `SiteSettings`: added `[CommandOption("--today-policy <POLICY>")] public string? TodayPolicy`, wired into `Resolve`/`ResolveTolerant` via `ResolveDatePolicy()`. Rejection is done at Spectre's parse-time `Validate()` gate (cleanest surface) with `ResolveDatePolicy()` throwing as a backstop for the menu/library paths. Forgiving spellings accepted (`machine`/`local`, `utc`, `last`/`commit`); canonical set documented.
  - [x] `SavedSettings`: added `public DatePolicy? TodayPolicy` (tri-state nullable), included in `IsEmpty`; `Capture`/`TrySave` persist only a non-default value; `ApplyTo` fills from saved only when the CLI didn't pass one (CLI wins). Enum persists as its NAME (added a scoped `JsonStringEnumConverter`) so `.specscribe` stays human-editable and reorder-proof.
  - [x] `SettingsResolver`: added `Fields.TodayPolicy`, the `CliOverrides.TodayPolicy` snapshot, and the provenance entry (reported as the canonical token for the `--show-config` grep surface). `ConfigurePaths` (`Commands.cs`) gained a `SelectionPrompt<DatePolicy>` seeded with the current value first.

- [x] **Task 4 — Surface effective policy + provenance on the diagnostics page** (AC: #2)
  - [x] `DiagnosticsConfig` gained `public required DatePolicy DatePolicy`, set in `FromRun` from `options.DatePolicy` (pure field read).
  - [x] `RenderConfig` adds one `AppendRow(sb, "Date-page \"today\" policy", ...)` after "Deep-git analytics": `machine-local calendar day (default)` at the default, `… (--today-policy <token>)` for an override — mirroring the `on (--deep-git)` / ADR `explicit (--adrs)` provenance convention. Text in the `<dl>`, never color-only.
  - [x] Confirmed via the golden fingerprint gate that this row is the ONLY byte delta on the site at the default policy.

- [x] **Task 5 — Tests, golden regen, and verification** (AC: #1, #2)
  - [x] Resolver unit tests (`DatePolicyTests`) + settings round-trip tests (`SettingsStoreTests`: `TrySave` omits `MachineLocal`, persists/normalizes non-default, backward-compat load; `ApplyTo` CLI-precedence) + CLI rejection tests (`SettingsResolverTests`: `Validate` rejects a typo with the valid-value list, `ResolveDatePolicy` throws).
  - [x] AC #1 proven by the golden fingerprint gate: default-policy site byte-identical except the one new diagnostics row. New hash `336e807c…`, stable across two clean runs.
  - [x] `DiagnosticsTemplaterTests` asserts the config `<dl>` carries the policy row (default + both non-default provenance forms).
  - [x] AC #2 consistency proven in `DatePolicyTests` (`ResolvedToday_DrivesTheLinkedDaySet_UnderLastCommitPolicy`, `OneResolvedToday_MakesEveryConsumerAgree`) AND live: a real `--deep-git --today-policy last-commit` generation produced 21 linked days == 21 generated date pages, zero dead links.
  - [x] Golden content fingerprint regenerated + recorded; confirmed stable across two repeated clean runs (stale-build trap avoided).

### Review Findings

Code review 2026-07-26 (bmad-code-review, `git diff f9b52bd8..HEAD` restricted to this story's own File List). **Scope note:** file-level scoping was not sufficient — several of these 18 files (`SiteGenerator.cs`, `Charts.cs`, `SettingsStore.cs`, `Commands.cs`, `HtmlRenderAdapter.Dashboard.cs`) are also shared by sibling stories on the same commit range. Content belonging to Story 5.2 (ADR-0014 `.specscribe` folder migration, `CliOverrides.Capture` predicate fixes, the settings-save warning branch — already reviewed 2026-07-25), Story 5.3 (`CopyEmbeddedAsset` retry), and Epic 20/23/25 (Hierarchy Explorer, Design System page, sunburst weighting, vendored-asset test infra) was excluded from triage below as out-of-scope for this story; it is not itemized as deferred work here. 3 adversarial layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor) found 4 patch, 0 decision-needed, 3 dismissed (all in Story 5.5's own scope — no AC violations).

- [x] [Review][Patch] Malformed/unrecognized `TodayPolicy` token in `.specscribe/config.json` fails whole-document deserialization, silently discarding ALL saved settings (Source/Adrs/Output/ProjectName/DeepGit/CodeUrl/IncludeReadme), not just the date policy — confirmed empirically that the CLI's own accepted spelling `"last-commit"` (vs. the enum's `"LastCommit"`) throws a JsonException that `TryReadCandidate` treats as "no saved settings" at all. **Fixed:** replaced the generic `JsonStringEnumConverter` with a `DatePolicyJsonConverter` that reuses `DatePolicies.TryParse`'s forgiving vocabulary — this is strictly MORE permissive (the CLI's own spellings, incl. `"last-commit"`, now round-trip correctly) and degrades a genuinely unrecognized token to `null` ("not configured") instead of throwing. 2 new tests (`SettingsStoreTests`: unrecognized token doesn't lose sibling fields; CLI spelling round-trips). [src/SpecScribe/SettingsStore.cs:62-88]
- [x] [Review][Patch] Interactive `ConfigurePaths` menu silently falls back to `MachineLocal` when a restored `.specscribe` TodayPolicy value doesn't parse, with no warning, then overwrites the saved value with the (silently-defaulted) selection — inconsistent with the CLI path, which loudly rejects the same class of bad input via `SiteSettings.Validate()`. **Fixed:** added a yellow warning line (matching the existing `TrySave`-failure warning's style) when a non-empty saved policy fails to parse, before the prompt defaults to machine-local. Not test-covered — the interactive TTY menu is a pre-existing owner-verification-only surface (Story 5.2 completion notes: harnesses report `Interactive==false`, so `AnsiConsole.Prompt` can't be exercised headlessly). [src/SpecScribe/Commands.cs:606-619]
- [x] [Review][Patch] AC #2's "one resolved `today`, every consumer must agree" guarantee has no automated integration-level test. `OneResolvedToday_MakesEveryConsumerAgree` only proves `Charts.LinkedCommitDays` is referentially transparent (calls it twice with identical arguments) — it never exercises `SiteGenerator._today`, `CommitHeatmap`, or `GitPulsePanel`, the real call sites the AC is about. Only evidence for the real invariant is a one-off manual CLI run in the Dev Agent Record. **Fixed:** new `SiteGeneratorCommitDetailsTests.GenerateAll_LastCommitPolicy_LinkedHeatmapDaySetMatchesGeneratedDayPageSet` runs a real `SiteGenerator` with `DatePolicy.LastCommit` against a real git fixture and asserts the git-insights heatmap's linked day set equals the actual `commits/*.html` file set — exercising the production wiring, not the pure resolver alone. [tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs]
- [x] [Review][Patch] `Charts.CommitHeatmap`/`Charts.GitPulsePanel` silently default an omitted `today` parameter to `ResolveToday(DatePolicy.MachineLocal, ...)` with no comment marking this as a deliberate degrade rather than the resolved run policy — currently unreachable (all 4 production call sites verified to thread `_today`/`dateCutoff` correctly) but a future call site that forgets to pass it would silently regress to the default policy instead of erroring. Low severity, cosmetic. **Fixed:** `CommitHeatmap` already documented this; added the equivalent doc-comment note to `GitPulsePanel`. [src/SpecScribe/Charts.cs:1846-1858]

Dismissed (3): `CliOverrides.TodayPolicy`'s `{ Length: > 0 }` predicate looking inconsistent with its siblings' `is not null` — verified correct and already documented in-code as intentional [Story 5.5] reasoning, not a bug. `ResolveDatePolicy()`'s throw path looking "dead" since `SiteSettings.Validate()` already rejects bad CLI input first — verified intentional defense-in-depth backstop for library/interactive callers that bypass Spectre validation, documented in-code. The golden fingerprint being captured on top of other sessions' uncommitted work — an accepted, already-documented working condition of shared `main` (see CLAUDE.md), not a new defect.

## Dev Notes

### The load-bearing invariant (the #1 review checkpoint)

`LinkedCommitDays` is documented in-code as *"The single source of truth for which days get a heatmap link AND a generated per-day page … so a linked cell can never point at a page that wasn't generated, and vice versa"* ([Charts.cs:2143](../../src/SpecScribe/Charts.cs)). Its `today` parameter is the **filter that makes that guarantee hold**. Today, four *other* sites independently recompute `DateTime.Now` and one passes it in — they agree only because they all call the same expression. The moment "today" becomes a *policy*, those independent recomputations become a **drift hazard**: if the guard (`ChangeLogDayHref`), the page generator (`GenerateDatePagesInternal`), and the heatmap each resolve the policy separately, a `LastCommit` policy evaluated at slightly different moments — or a `Utc` boundary crossed mid-run — could produce different "today" values and thus a linked cell with no page (dead link) or a page nothing links to (orphan).

**The fix is structural, not a bigger `if`:** resolve "today" **once** per run into a shared field and thread it. This is the same lesson the `ChangeLogDayHref` XML doc already records from Story 10.4's "review loop 1/2/3" — narrower per-condition checks introduced real dead-link gaps; a single shared computation structurally avoids them. Do not reintroduce independent `DateTime.Now` reads on the date-cutoff path.

### Timezone honesty is preserved (do not overreach)

This story governs **which calendar day is the cutoff**, nothing else. Commit *timestamps* must keep rendering in their authored offset via `PortalDates` — the Story 10.4 timezone policy ([10-4-…md:117](10-4-consistent-dates-and-event-sequencing.md)) is untouched. There is no `format-local:` conversion, no UTC time display, no re-zoning of any commit clock. `Utc` policy means "use the UTC calendar day as the cutoff," not "render times in UTC." Keep these separate in code and in the diagnostics label so a reader isn't misled into thinking commit times moved.

### Settings stack — follow the established precedent verbatim

The `--deep-git` (Story 3.2) and `--code-url` (Story 7.7) options are the exact template for a new configurable setting, end to end:
- **CLI option** on `SiteSettings` with a `[Description]` ([SiteSettings.cs:29,37](../../src/SpecScribe/SiteSettings.cs)).
- **`ForgeOptions` property** — non-`required`, defaulted, plus a `Resolve(...)` parameter ([ForgeOptions.cs:40,49,110](../../src/SpecScribe/ForgeOptions.cs)).
- **`SavedSettings` tri-state nullable** with `IsEmpty` participation, **persist-only-when-non-default** in `TrySave`, **CLI-wins `??=`** in `ApplyTo` ([SettingsStore.cs:18,26,75,103](../../src/SpecScribe/SettingsStore.cs)).
- **Interactive prompt** in `ConfigurePaths`, defaulted to current value for NFR7 parity ([Commands.cs:394](../../src/SpecScribe/Commands.cs)).
- **Diagnostics row** in `DiagnosticsConfig.FromRun` + `RenderConfig` ([DiagnosticsTemplater.cs:132,256](../../src/SpecScribe/DiagnosticsTemplater.cs)).

Match this shape and the story is mechanically low-risk; the only genuinely new thinking is the shared-`today` threading in Task 2.

### Story 5.2 relationship (soft dependency, not a blocker)

AC #2 references "interactive/CLI parity (NFR7 / Story 5.2)." Story 5.2 (`5-2-directory-scoped-settings-…`) is **ready-for-dev, not yet done** — but the `SavedSettings`/`SettingsStore`/`ConfigurePaths` machinery this story extends **already exists and works today** (verified in the tree), so 5.5 is **not gated** on 5.2. Two coordination notes:
- 5.2's headline fix is routing `Resolve()` through the settings store for **all** commands including `webview` (which today ignores saved settings). If 5.2 lands first, the today-policy setting rides that routing for free. If 5.5 lands first, the today-policy persists/restores for `generate`/`watch`/interactive exactly like `--deep-git` does today; the `webview` gap is 5.2's to close and is not widened by this story.
- Do not build 5.2's routing here. Stay in the `--deep-git` lane: this story only adds one more option to the existing stack.

### Charting stays pure SVG + links, no JS

No new script, no motion. The heatmap and date pages are pure server-rendered SVG/HTML ([memory: charting-is-pure-svg-no-js]). The diagnostics disclosure is native `<details>` — reduced-motion trivially satisfied. Nothing in this story needs the one existing tooltip/copy script.

### Degradation (NFR8)

- `LastCommit` policy with no git / empty repo → resolver falls back to `MachineLocal` (documented). No crash, no empty page — date pages that exist are still the artifact-change days (which don't depend on the commit cutoff).
- Unrecognized `--today-policy` value → reject at `Resolve()` with an actionable message (do not silently default — a typo that silently no-ops is a worse failure than an error).
- Malformed/absent `.specscribe` → already "no saved settings" per `SettingsStore.TryLoad`; the new nullable field participates automatically.

### Project Structure Notes

- New type: `DatePolicy` enum — recommend `src/SpecScribe/DatePolicy.cs` (or on `ForgeOptions`). Zero value = `MachineLocal`.
- Resolver: `Charts.ResolveToday(...)` next to `LinkedCommitDays` (same file, same consumer domain).
- Touched files (all UPDATE): `SiteGenerator.cs` (compute-once field + 3 call sites), `Charts.cs` (`CommitHeatmap` + git-insights signatures + resolver), `SiteSettings.cs`, `ForgeOptions.cs`, `SettingsStore.cs`, `Commands.cs`, `DiagnosticsTemplater.cs`.
- No new page, no nav change, no new asset. Output delta at default = one diagnostics `<dl>` row.
- Naming: match the codebase's existing option vocabulary. `DatePolicy` / `--today-policy` chosen to read naturally in help and diagnostics; adjust if a nearer-existing term surfaces (confirm at review — see Question 1).

### Testing standards summary

- xUnit tests under `tests/SpecScribe.Tests/`. Prefer pure unit tests on `ResolveToday` and the settings round-trip over full-generation tests where a unit test proves the same invariant.
- The **golden content fingerprint** is the AC #1 guardrail here (unlike Story 4.8, where the footer change made it *not* the guardrail): at default policy the delta must be exactly the one diagnostics row. Regenerate deliberately and confirm the diff is only that row. See the golden-diff gotcha.
- Add resolver, settings-persistence, CLI-rejection, and diagnostics-row assertions per Task 5.

### Golden-diff / build gotchas (from memory)

- **Stale-build first-captured-hash trap:** the first fingerprint you capture may reflect a stale build. Re-run a clean generation and confirm the hash is stable across two runs **before** locking it into a test constant. [memory: golden-diff-normalization-gotchas]
- **Shared-main concurrent-edit loss:** this repo's `main` has a background auto-committer and other sessions may edit the same files. After your Charts.cs / SiteGenerator.cs edits, **grep-verify your new symbols (`ResolveToday`, `DatePolicy`, the `today` params) actually landed** before trusting a build pass. [memory: shared-main-concurrent-edit-loss-verify-after-edit]
- The `*/` CSS-comment truncation and `--status-*` conventions are not in play here (no CSS surface), but the "don't put a bare `*/` in a comment" habit still applies to any XML-doc you write.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 5.5] — the ACs and the seeding rationale (lines 861–885).
- [Source: _bmad-output/implementation-artifacts/10-4-consistent-dates-and-event-sequencing.md#Review-Decision] — origin (line 198): owner ruling to keep machine-local default, seat policy as 5.5.
- [Source: src/SpecScribe/Charts.cs:2143] — `LinkedCommitDays`, the single-source-of-truth day filter and its `today` param.
- [Source: src/SpecScribe/Charts.cs:1116] — `CommitHeatmap` (computes `today` at :1126, links at :1171).
- [Source: src/SpecScribe/Charts.cs:1315] — Git Pulse last-commit link guard (computes `today` at :1320).
- [Source: src/SpecScribe/SiteGenerator.cs:921] — `GenerateDatePagesInternal` (`today` at :951), artifact-by-day gather (`today` at :1056), `ChangeLogDayHref` (:1362).
- [Source: src/SpecScribe/SiteSettings.cs:7] — CLI option surface + `Resolve`/`ResolveTolerant`.
- [Source: src/SpecScribe/ForgeOptions.cs:110] — `Resolve` and the property pattern for non-required defaulted options.
- [Source: src/SpecScribe/SettingsStore.cs:8] — `SavedSettings` tri-state + `TrySave`/`ApplyTo` persistence pattern.
- [Source: src/SpecScribe/Commands.cs:378] — `ConfigurePaths` interactive parity (`--deep-git`/`--code-url` precedents at :394/:407).
- [Source: src/SpecScribe/DiagnosticsTemplater.cs:109] — `DiagnosticsConfig`/`FromRun`/`RenderConfig` config-log surface (Story 4.8).

## Questions for the Owner (raised at story close, not blocking)

1. **Naming.** `--today-policy` with values `machine-local` / `utc` / `last-commit`, surfaced on diagnostics as `Date-page "today" policy`. Acceptable, or do you prefer e.g. `--date-cutoff` / `--calendar-day-policy`? (Low stakes — pick at review; it's a one-word rename.)
2. **`LastCommit` naming precision.** The AC says "author-local-derived cutoff (e.g. max series / last-commit day)." The resolver uses `series.Max(day)` = the latest *authored* commit day. Confirm "latest authored commit day" is the intended semantics (vs. e.g. the `LastCommitTimestamp`'s day, which can differ from the series max if the series is capped). Recommend series-max for symmetry with `LinkedCommitDays`, which filters the same series.
3. **Fourth policy?** The AC requires *at least* the three. Is a fixed explicit `--as-of <date>` override desirable now, or defer? (Out of scope as written; flag only.)

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code)

### Debug Log References

- Full suite: 2336 passed / 3 skipped (pre-existing) / 0 failed.
- Golden fingerprint regenerated `89c8cf0c…` → `336e807c…`; verified stable across two clean runs.
- Live: `generate --deep-git --today-policy last-commit` → 679 pages, 0 errors; 21 linked days == 21 generated `commits/*.html`, 0 dead links; diagnostics row renders `latest authored commit day (--today-policy last-commit)`; `--show-config` emits `field=today_policy origin=commandline value=utc`; `--today-policy nope` rejected with the valid-values list.

### Completion Notes List

- **Story landed after Story 5.2 merged**, so the settings stack this extends is now the `SettingsResolver` seam (Load/Resolve/provenance), not the raw `SettingsStore` the story's Dev Notes described. Threaded the policy through that seam: new `Fields.TodayPolicy`, `CliOverrides.TodayPolicy` snapshot, and provenance entry — so `--show-config` and the interactive paths block attribute it correctly (CLI > `.specscribe` > default), exactly like `--deep-git`/`--code-url`.
- **`LastCommit` degradation:** with no git / empty repo the resolver falls back to `MachineLocal` (the `series is { Count: > 0 }` guard), so a git-less repo behaves exactly as today and never crashes or invents a sentinel date (NFR8).
- **Cutoff vs. staleness are kept distinct.** The dashboard already had a `today` parameter that drives artifact-coverage *staleness*; I did NOT reuse it for the date-page cutoff. Instead I added a separate `dateCutoff` parameter through `RenderDashboardBody`/`BuildIndexPage`/`AppendDashboardSection`. Conflating them would let `--today-policy last-commit` on a long-idle repo report every planning artifact as freshly updated — a real bug this separation prevents. `Utc` policy still never re-zones a commit *timestamp* (Story 10.4 honesty preserved).
- **Enum persistence format:** `DatePolicy` is written to `.specscribe` as its NAME (`"Utc"`) via a scoped `JsonStringEnumConverter`, not an ordinal — a hand-editable config shouldn't carry an opaque number, and a name is immune to enum reordering.
- **Rejection is at Spectre's `Validate()` gate** (parse-time, clean CLI error) with `ResolveDatePolicy()` throwing as a backstop for the interactive menu / library callers that never pass through Spectre validation.
- **Shared-main provenance:** regenerated the golden hash on a tree also carrying a concurrent session's untracked `23-2-component-library-and-design-token-bridge.md` (a doc, not code, not part of the temp fixture — does not affect the hash). All new symbols grep-verified present after the build.
- **Owner questions (from story close) still open** — none blocked implementation; recommend confirming at epic-end review: (1) the `--today-policy` name + `machine-local`/`utc`/`last-commit` tokens; (2) `LastCommit` == series-max semantics (implemented as recommended); (3) whether a future `--as-of <date>` policy is wanted (left out of scope).

### File List

- `src/SpecScribe/DatePolicy.cs` (NEW — `DatePolicy` enum + `DatePolicies` parse/label/token helper)
- `src/SpecScribe/Charts.cs` (`ResolveToday` resolver; `CommitHeatmap` + `GitPulsePanel` gain a `today` param)
- `src/SpecScribe/SiteGenerator.cs` (`_today` field + `RefreshToday()`; five call sites threaded; `dateCutoff` passed to the dashboard)
- `src/SpecScribe/HtmlTemplater.cs` (`RenderIndex`/`BuildIndexPage` gain `dateCutoff`)
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` (`RenderDashboardBody`/`AppendDashboardSection` gain `dateCutoff`; Git Pulse threaded)
- `src/SpecScribe/TimelineTemplater.cs` (`RenderPage` gains `today`, threaded to the heatmap)
- `src/SpecScribe/GitInsightsTemplater.cs` (`RenderPage`/`AppendActivitySection` gain `today`, threaded to the heatmap)
- `src/SpecScribe/ForgeOptions.cs` (`DatePolicy` property + `Resolve` parameter)
- `src/SpecScribe/SiteSettings.cs` (`--today-policy` option; `Validate()`; `ResolveDatePolicy()`; both `Resolve` paths)
- `src/SpecScribe/SettingsStore.cs` (`SavedSettings.TodayPolicy` tri-state; `IsEmpty`/`Capture`/`ApplyTo`; string-enum converter)
- `src/SpecScribe/SettingsResolver.cs` (`Fields.TodayPolicy`; `CliOverrides.TodayPolicy`; provenance entry)
- `src/SpecScribe/Commands.cs` (`ConfigurePaths` interactive `SelectionPrompt<DatePolicy>`)
- `src/SpecScribe/DiagnosticsTemplater.cs` (`DiagnosticsConfig.DatePolicy`; `FromRun`; the config `<dl>` row)
- `tests/SpecScribe.Tests/DatePolicyTests.cs` (NEW — resolver, consistency, parse/label/token)
- `tests/SpecScribe.Tests/SettingsStoreTests.cs` (policy persistence round-trip + precedence)
- `tests/SpecScribe.Tests/SettingsResolverTests.cs` (policy provenance + CLI rejection)
- `tests/SpecScribe.Tests/DiagnosticsTemplaterTests.cs` (config-row assertions + `Config(datePolicy:)`)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (golden fingerprint regenerated)

## Change Log

- 2026-07-24 — Story 5.5 implemented: configurable date-page "today" cutoff (`DatePolicy` MachineLocal/Utc/LastCommit; `--today-policy` + `.specscribe` persistence + interactive parity; one policy-resolved `_today` shared across all five date-cutoff consumers; diagnostics provenance row). Default byte-identical except the one diagnostics row (golden `336e807c…`). Status → review.
