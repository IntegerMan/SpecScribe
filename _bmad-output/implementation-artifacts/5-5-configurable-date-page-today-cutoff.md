# Story 5.5: Configurable Date-Page "Today" Cutoff (Timezone Policy)

Status: ready-for-dev

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

- [ ] **Task 1 — Introduce the policy type and a single `today` resolver** (AC: #1, #2)
  - [ ] Add a `DatePolicy` enum (values `MachineLocal`, `Utc`, `LastCommit`) — recommend a new small file `src/SpecScribe/DatePolicy.cs`, or co-locate on `ForgeOptions` if you prefer fewer files. `MachineLocal` MUST be the zero/first value so `default(DatePolicy)` == the status quo.
  - [ ] Add one **pure static resolver** — recommend `Charts.ResolveToday(DatePolicy policy, IReadOnlyList<(DateOnly Day, int Count)>? series)` (co-locate next to `LinkedCommitDays` since that is the consumer the constraint centers on) — returning the `DateOnly` that means "today" for the run:
    - `MachineLocal` → `DateOnly.FromDateTime(DateTime.Now)` (exactly today's expression — do not "improve" it).
    - `Utc` → `DateOnly.FromDateTime(DateTime.UtcNow)`.
    - `LastCommit` → `series` is non-null and non-empty ⇒ `series.Max(s => s.Day)`; **degrade to `MachineLocal` when `series` is null/empty** (no git, or an empty repo — there are no commit days to derive from). Document this fallback in the summary; record it in Completion Notes.
  - [ ] Unit-test the resolver directly (see Testing Requirements): all three policies + the `LastCommit`-without-git fallback + the `LastCommit` value equals the series max.

- [ ] **Task 2 — Compute the run's `today` once and thread it to all five call sites** (AC: #2 — this is the #1 review checkpoint)
  - [ ] In `SiteGenerator`, compute the run's resolved `today` **once** (call `Charts.ResolveToday(_options.DatePolicy, _progress?.Git?.DailySeries)`) as soon as git metrics are known, and store it in a private field (e.g. `_today`). Git is already available at story-render time — `ChangeLogDayHref` reads `_progress?.Git` today — so the field is populated before any consumer runs.
  - [ ] Replace **every** in-place `DateOnly.FromDateTime(DateTime.Now)` "today" computation on the date-cutoff path with the shared field / a threaded parameter. The five sites (verified present in the tree):
    1. [SiteGenerator.cs:951](../../src/SpecScribe/SiteGenerator.cs) — `GenerateDatePagesInternal` → `Charts.LinkedCommitDays(..., DateOnly.FromDateTime(DateTime.Now))`.
    2. [SiteGenerator.cs:1056](../../src/SpecScribe/SiteGenerator.cs) — artifact-by-day gather: `var today = DateOnly.FromDateTime(DateTime.Now);` (future-skew skip guard at :1065).
    3. [SiteGenerator.cs:1364](../../src/SpecScribe/SiteGenerator.cs) — `ChangeLogDayHref` → `Charts.LinkedCommitDays(..., DateOnly.FromDateTime(DateTime.Now)).Contains(date)`.
    4. [Charts.cs:1126](../../src/SpecScribe/Charts.cs) — `CommitHeatmap` computes `today` internally for **both** the grid extent and `LinkedCommitDays` (:1171). Add a `DateOnly today` parameter (default it to `DateOnly.FromDateTime(DateTime.Now)` so no other caller breaks) and pass the resolved value from the SiteGenerator call site.
    5. [Charts.cs:1320](../../src/SpecScribe/Charts.cs) — the Git Pulse last-commit link computes `today` internally for its `LinkedCommitDays` guard. Thread the resolved value in the same way as (4).
  - [ ] **Do not** change the grid-extent / future-skew *semantics* — a future-dated commit is still skipped, and the heatmap still never runs past "today". Only the *value* of "today" now flows from one policy-resolved source. Note in Dev Notes: under `Utc`/`LastCommit` the grid extent and the future-skew cutoff move together with the link/page set (this is the intended consistency, not a regression).
  - [ ] Grep-verify zero remaining `DateOnly.FromDateTime(DateTime.Now)` on the date-cutoff path after the change (the footer/console *generation clock* `DateTime.Now` in `PathUtil`/`ConsoleUi` is a **different** concern — leave it alone; it is the machine build clock, not the date-page cutoff).

- [ ] **Task 3 — Thread the policy through the settings/options stack** (AC: #2 — follow the existing `--deep-git` / `--code-url` precedent exactly)
  - [ ] `ForgeOptions`: add `public DatePolicy DatePolicy { get; init; }` (NOT `required` — default `MachineLocal` so every existing construction stays status quo, mirroring `EmitSpa`/`CodeSourceBaseUrl`). Add a `DatePolicy datePolicy = DatePolicy.MachineLocal` parameter to `Resolve(...)` and set it on the returned object.
  - [ ] `SiteSettings`: add `[CommandOption("--today-policy <POLICY>")]` `public string? TodayPolicy { get; set; }` with a clear `[Description(...)]` listing the three accepted values and stating machine-local is the default and that it governs only the date-page day-cutoff (not commit-time display). Parse/validate the string → `DatePolicy` in `Resolve()` / `ResolveTolerant()` and pass it to `ForgeOptions.Resolve`. Reject an unrecognized value with an actionable message that lists the valid values (mirror `TryValidateCodeUrl`'s reject-don't-silently-accept discipline — a typo must not silently fall back to default). Accept a small set of forgiving spellings if trivial (`utc`/`UTC`, `machine-local`/`machine`, `last-commit`/`last`), but keep the canonical set documented.
  - [ ] `SavedSettings` (`SettingsStore.cs`): add `public DatePolicy? TodayPolicy { get; set; }` (nullable for the tri-state "never configured", matching `DeepGit`'s pattern). Update `IsEmpty` to include it. In `TrySave`, persist only a **non-default** value (write `null` for `MachineLocal`, same "nothing to persist for the default" logic as `DeepGit`). In `ApplyTo`, fill `settings.TodayPolicy` from `saved` only when the CLI didn't pass one (CLI wins — same `??=` precedence as the other options).
  - [ ] `ConfigurePaths` (`Commands.cs:378`): add an interactive prompt for the policy (a `SelectionPrompt<DatePolicy>` or a three-choice text prompt), defaulted to the current value so re-running Configure paths doesn't silently flip it — this is the NFR7 menu/CLI-parity requirement the `--deep-git` and `--code-url` prompts already satisfy. Persist via the existing `SettingsStore.TrySave(settings)` call.

- [ ] **Task 4 — Surface effective policy + provenance on the diagnostics page** (AC: #2)
  - [ ] `DiagnosticsConfig` ([DiagnosticsTemplater.cs:109](../../src/SpecScribe/DiagnosticsTemplater.cs)): add a field for the policy display (e.g. `public required DatePolicy DatePolicy { get; init; }` or a pre-formatted `DatePolicyDisplay` string). Set it in `FromRun` from `options.DatePolicy` — pure field read, no I/O (preserve the local-first-by-construction invariant AC #2/NFR3 already guarantees).
  - [ ] `RenderConfig` ([DiagnosticsTemplater.cs:256](../../src/SpecScribe/DiagnosticsTemplater.cs)): add one `AppendRow(sb, "Date-page \"today\" policy", ...)` row after "Deep-git analytics", rendering a human label — e.g. `machine-local (default)`, `UTC calendar day`, `latest authored commit day`. Provenance treatment: the display must make clear when the value is the default vs. an explicit override (mirror the existing `"on (--deep-git)"` vs `"off"` and ADR `"explicit (--adrs)"` vs `"default"` conventions). **The word/label must never be color-only** (it is text in the `<dl>`, so this is satisfied by construction — same rule as 4.8 AC #2d).
  - [ ] This row is the **only** intentional byte delta on `diagnostics.html` at the default policy — confirm the golden diff shows exactly that.

- [ ] **Task 5 — Tests, golden regen, and verification** (AC: #1, #2)
  - [ ] Resolver unit tests (Task 1) + settings round-trip tests (Task 3: `TrySave` omits `MachineLocal`, persists non-default; `ApplyTo` CLI-precedence; unrecognized `--today-policy` string rejected).
  - [ ] A generation-level test proving **AC #1**: with default policy the full site is byte-identical to the pre-story baseline **except** the one new diagnostics config row. Use the golden fingerprint gate (see the golden-diff gotcha below) to prove the delta is exactly that.
  - [ ] A `DiagnosticsTemplaterTests` assertion that the config `<details>` contains the policy row (extend the existing test at the pattern noted in [4-8-…md:108](4-8-generation-diagnostics-and-configuration-log-page.md)).
  - [ ] A focused test proving **AC #2 consistency**: for a non-default policy (`Utc` or `LastCommit`) the linked-day set (`LinkedCommitDays` via the resolved today) equals the generated date-page set — i.e. the resolver value drives both. A construct-a-series unit test on `ResolveToday` + `LinkedCommitDays` is sufficient and cheaper than a full generation; add it at the `Charts`/`SiteGenerator` seam.
  - [ ] Regenerate the golden content fingerprint (one row on `diagnostics.html`) and record the new hash in Completion Notes. **Confirm with a repeated clean run before locking any constant** (stale-build first-captured-hash trap — see gotcha).

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

### Debug Log References

### Completion Notes List

### File List
