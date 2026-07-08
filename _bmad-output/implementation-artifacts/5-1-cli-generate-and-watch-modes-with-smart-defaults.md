# Story 5.1: CLI Generate and Watch Modes with Smart Defaults

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want one-shot `generate` and continuous `watch` commands with sensible defaults,
so that I can produce and refresh docs quickly in real projects — including from CI and piped/non-interactive shells.

## Acceptance Criteria

1. **Given** a supported repository layout
   **When** I run `generate` or `watch` with no required flags
   **Then** source and output roots are auto-discovered
   **And** generation succeeds with clear terminal feedback.

2. **Given** a non-standard repository layout
   **When** I supply explicit source, ADR, and output options
   **Then** those overrides are honored for the run
   **And** help output documents available command options clearly.

### Derived / cross-cutting acceptance (from UX-DR15, NFR2, DESIGN "CLI Output")

3. **Given** a non-interactive terminal (CI, piped/redirected stdout — `Capabilities.Interactive == false`)
   **When** I run `generate`
   **Then** feedback degrades to plain text (no live spinner/animated bar, no reliance on ANSI cursor control)
   **And** a single machine-parseable summary line is emitted to stdout describing the outcome (counts + elapsed).

4. **Given** one or more pages fail to generate (an `Error` outcome is reported)
   **When** the `generate` command finishes
   **Then** the process exits with a non-zero exit code
   **And** each failing path + message is surfaced (not swallowed), so CI can detect the failure.
   **Given** all pages generate successfully **Then** the exit code is `0`.

## Tasks / Subtasks

- [ ] **Task 1 — Non-interactive feedback + machine-parseable summary (AC: #1, #3)**
  - [ ] In `ConsoleUi.RunWithProgress`, branch on `AnsiConsole.Profile.Capabilities.Interactive`: keep the live `AnsiConsole.Progress()` display for interactive terminals; for non-interactive, run `generator.GenerateAll(...)` with a no-op/plain reporter (no live progress task tree) so nothing depends on cursor control. (Spectre already degrades `Progress()` when non-interactive, but do not rely on that implicitly — make the branch explicit and covered by intent.)
  - [ ] Add a machine-parseable one-line summary emitted on every `generate`/`watch` initial build. Recommended stable, greppable shape (single line, no markup): `SpecScribe: generated=<n> updated=<n> skipped=<n> errors=<n> elapsed_ms=<n>`. Emit it in addition to the existing human `PrintInitialSummary` table so interactive users still get the pretty table and CI gets a parseable line. In non-interactive mode the pretty table may be suppressed, but the summary line MUST always print.
  - [ ] Keep the existing warning/error line conventions (`⚠`/`✗`-style, currently `!`/`x`) intact; ensure they still render as plain text without color when non-interactive.
  - [ ] Route the summary through a single helper so `watch` per-rebuild feedback (Story 5.3's area) can reuse it later without duplication — but do NOT implement 5.3's per-rebuild summary here.

- [ ] **Task 2 — Exit codes reflect generation outcome (AC: #4)**
  - [ ] `GenerateCommand.Execute` and `WatchCommand.Execute` currently `return 0` unconditionally. Change `GenerateCommand.RunGeneration` (or its callers) to surface whether any `Error` outcome occurred, and return a non-zero exit code (`1`) from `GenerateCommand.Execute` when the initial build had errors.
  - [ ] For `watch`, the initial build's error state should still be reported, but the command's return code is governed by the watch loop lifecycle (Ctrl+C → `0`); do not fail-fast the watch loop on an initial-build error — surface it and keep watching (watch is a live-edit loop). Document this asymmetry in a code comment.
  - [ ] Preserve the existing top-level `Program.cs` exception → exit-code behavior (parse errors fall to interactive menu when a human is present, `1` otherwise; `DirectoryNotFoundException` → `1`). Do not regress the "drop into menu on bad args when interactive" flow.
  - [ ] Confirm `RunGeneration` returning the `SiteGenerator` still works for `watch` (it consumes the generator for the watch loop) — thread the error signal without breaking that return.

- [ ] **Task 3 — Help output clarity + examples (AC: #2)**
  - [ ] Verify every `SiteSettings` option (`--source`, `--adrs`, `--output`, `--project-name`, `--no-readme`) has an accurate, current `[Description]` (they do today — confirm wording still matches actual defaults, e.g. output default `SpecScribeOutput`, source default walk-up to `_bmad-output`).
  - [ ] Add top-level command descriptions/examples in `Program.cs` `app.Configure` (e.g. `config.AddExample(["generate"])`, `config.AddExample(["generate", "--source", "./_bmad-output", "--output", "./site"])`, `config.AddExample(["watch"])`) so `specscribe --help` shows real, working invocations.
  - [ ] Confirm `specscribe generate --help` and `specscribe watch --help` render the option table cleanly (Spectre auto-generates from `[CommandOption]`/`[Description]`).

- [ ] **Task 4 — Pin auto-discovery of BOTH source and output roots (AC: #1)**
  - [ ] `ForgeOptions.Resolve` already walks up from cwd to find `_bmad-output` (source) and derives `OutputRoot = <repoRoot>/SpecScribeOutput`. Confirm both are treated as "auto-discovered" per AC1 and that `ConsoleUi.PrintPaths` clearly shows the resolved Source and Output before the run (it does). No behavior change expected — this task is verification + tests, not new discovery logic.
  - [ ] Do NOT add new discovery heuristics (e.g. scanning for alternate source dir names) — that is Epic 4 (framework generalization) territory, out of scope here.

- [ ] **Task 5 — Tests (AC: #1–#4)**
  - [ ] Extend `tests/SpecScribe.Tests/ForgeOptionsTests.cs` (or a new `CliFeedbackTests.cs`) to cover: (a) the machine-parseable summary line format is produced from a known set of `GenerationEvent`s; (b) exit-code logic returns non-zero when events contain an `Error` and `0` otherwise. Prefer testing the pure summary/exit-code helpers directly rather than driving Spectre's console.
  - [ ] Keep console/Spectre out of the unit under test where possible — extract summary-string building and error→exit-code decision into small pure functions/helpers that are unit-testable without a live `AnsiConsole` (mirrors how `ForgeOptions.Resolve` is tested headlessly).
  - [ ] Run the full suite: `dotnet test` from repo root; all existing tests must stay green.

## Dev Notes

### ⚠️ Critical framing: this is a HARDENING story, not a greenfield build

The `generate`/`watch`/interactive CLI **already exists** and predates Epic 1 (built in commit `627907d "Add Spectre.Console.Cli command surface with interactive menu"`). **Do not rebuild the command surface.** Your job is to close the specific gaps between the current implementation and Story 5.1's acceptance criteria + `UX-DR15` + the DESIGN "CLI Output" spec. Reinventing the CommandApp, argument parsing, or discovery is the primary failure mode to avoid here.

### Current state of the files you will touch (read before editing)

- **`src/SpecScribe/Program.cs`** — Composition root. Builds `CommandApp<InteractiveCommand>`, registers `generate` (`GenerateCommand`) and `watch` (`WatchCommand`), uses `UseStrictParsing()` + `PropagateExceptions()`. Top-level try/catch maps `CommandParseException` → interactive menu (if interactive) else `1`; `DirectoryNotFoundException`/`Exception` → `1`. **Must preserve** this exception→exit-code + menu-fallback behavior. Add examples here (Task 3).
- **`src/SpecScribe/Commands.cs`** — `GenerateCommand`, `WatchCommand`, `InteractiveCommand`.
  - `GenerateCommand.Execute` → `settings.Resolve()` → `PrintLogo`/`PrintPaths` → `RunGeneration(options)` → **`return 0` (always — this is the exit-code gap, Task 2)**.
  - `GenerateCommand.RunGeneration(options)` runs `ConsoleUi.RunWithProgress`, prints summary + output link, returns the `SiteGenerator` (watch reuses it). This is the single choke point to thread the error signal through.
  - `WatchCommand` runs an initial `RunGeneration` then `RunWatchLoop` (blocks on `ManualResetEventSlim` until Ctrl+C/ProcessExit → `0`).
  - `InteractiveCommand` already handles the non-interactive case by printing usage and returning `0` — this is the *menu* command, not `generate`/`watch`. Note that `generate`/`watch` themselves have NO non-interactive branch today; that's Task 1/Task 3.
- **`src/SpecScribe/ConsoleUi.cs`** — All Spectre presentation. Key methods: `RunWithProgress` (wraps `AnsiConsole.Progress()`), `PrintInitialSummary` (Rounded table: Generated/Skipped/Errors counts + per-error lines + `Initial build: N page(s) in Xms`), `LogEvent` (per-file watch line), `PrintPaths`, `PrintUsage`, `PrintFatalError`. Add the machine-parseable summary + non-interactive branch here (Task 1). **Must preserve** the existing interactive table + colored output for humans.
- **`src/SpecScribe/SiteSettings.cs`** — `CommandSettings` subclass with the five `[CommandOption]`s. `Resolve()` → `ForgeOptions.Resolve(...)`. Help text comes from `[Description]` here. **Do not change option names/shortcuts** (breaking for existing users); only refine descriptions if inaccurate.
- **`src/SpecScribe/ForgeOptions.cs`** — Pure resolution logic (headless-testable). Walk-up discovery of `_bmad-output`, derives repoRoot/output/adr defaults, reads `project_name` from `_bmad/config.toml`. **No behavior change expected** — Task 4 is verification only. Note `startDirectory` param exists specifically so tests can drive discovery without changing cwd — reuse that seam.
- **`src/SpecScribe/GenerationReporter.cs`** — `IGenerationReporter` (BeginPhase/Tick/EndPhase) + `SpectreGenerationReporter`. `GenerationEvent`/`GenerationOutcome` (Generated/Updated/Removed/Skipped/Error) live in the generation layer. For the non-interactive path you likely want a plain/no-op `IGenerationReporter` (no live tasks) — check whether a null-object reporter already exists; if not, a trivial one is fine.

### What must be preserved (regression guard — the system must work end-to-end)

1. **Interactive UX unchanged for humans:** logo, `PrintPaths`, live progress bars, rounded summary table, clickable `file://` output link, watch footer, and the `Ctrl+C` stop path all remain for TTY sessions.
2. **Menu fallback on bad args:** `Program.cs` must still drop into `InteractiveCommand.RunMenu` on a parse error when interactive, and return `1` when not.
3. **`--no-readme` and `--project-name`** continue to flow through `SiteSettings.Resolve()` unchanged.
4. **Watch does not fail-fast:** an initial-build error surfaces but watch keeps running (live-edit loop). Only `generate` maps errors → non-zero exit.
5. **Shared-read / no write-lock invariant (NFR5):** don't introduce any file access on the watched tree that takes a write lock. (You're not touching `FileWatcherService` here — that's Story 5.3 — but don't add incidental writes either.)

### Scope boundaries — do NOT drift into sibling stories

- **Story 5.2 (Directory-Scoped Settings, CLI parity, provenance):** `.specscribe` persistence (`SettingsStore`) and the interactive "Configure paths" flow already exist but are **5.2's formalization surface**. Do not add settings-precedence provenance, per-directory config resolution, or CLI/interactive parity work here. If you notice a parity gap, note it — don't fix it in 5.1.
- **Story 5.3 (Watch Safety & Scope-Aware Rebuilds):** debounce, rename/delete topology handling, rebuild-scope escalation, and per-rebuild coherence all belong to 5.3. Leave `FileWatcherService` and `SiteGenerator.Regenerate*` alone. Task 1's "route summary through a reusable helper" is a seam for 5.3, not an implementation of it.
- **Epic 4 (framework generalization):** do not add alternate source-dir discovery or non-BMad layout heuristics.

### UX / feedback spec (authoritative for AC #1 & #3)

From `DESIGN.md` "CLI Output (Terminal Surface)" (lines 380–391):
- Header line, progress indicator (spinner/bar) — **interactive only**.
- Success `✓` green + count + elapsed; Warning `⚠` gold; Error `✗` rust/red + path + message.
- **"Non-interactive mode (CI / piped output): plain text, no spinner, no color (auto-detect TTY)."** ← This is the core of AC #3. `AnsiConsole.Profile.Capabilities.Interactive` is the TTY signal already used in the codebase (`Program.cs:35`, `Commands.cs:70`).

From `EXPERIENCE.md` Voice & Tone (lines 61–71): active voice, numbers over adjectives, error messages state *what happened + what to do*, watch feedback present-tense/brief ("Rebuilt in 240 ms.", "Watching 3 source paths."). Keep the machine-parseable line separate from prose — one is for humans, one is for `grep`/CI.

### Requirements traceability

- **FR12** (CLI-first generate + watch, auto-discovery + explicit overrides) — primary FR for this story.
- **FR8** (reliable watch regeneration) — partially seated here (watch command exists + initial build); rapid-edit safety is **FR8's deeper half in Story 5.3**.
- **UX-DR15** (CLI feedback states for interactive AND non-interactive terminals, incl. progress, warnings, errors, machine-parseable summary) — AC #3/#4 exist to satisfy this.
- **NFR2** (resilient to malformed/missing artifacts; degrade with non-fatal notices) — error surfacing in Task 2 must not turn a single bad page into a hard crash; it should be an `Error` outcome that yields a non-zero exit, not an exception.

### Project Structure Notes

- All CLI/console code lives flat under `src/SpecScribe/`; there is no `Cli/` subfolder — keep new helpers in the same folder, matching the existing convention (`Commands.cs`, `ConsoleUi.cs`, `Program.cs`, `SiteSettings.cs`, `ForgeOptions.cs`).
- Tests live in `tests/SpecScribe.Tests/` as xUnit (`[Fact]`), one file per subject, temp-dir fixtures via `Directory.CreateTempSubdirectory` with `IDisposable` cleanup (see `ForgeOptionsTests.cs`). Prefer headless helpers over driving `AnsiConsole`.
- **Output directory is `SpecScribeOutput`** (repo-root sibling), never `docs/live` — that flag is vestigial/gitignored. `ForgeOptions.OutputDirName` is the single source of this default.

### Technology / library specifics (verified against `SpecScribe.csproj`)

- **.NET `net10.0`**, `Nullable` enabled, `ImplicitUsings` enabled. Packaged as a dotnet global tool (`PackAsTool`, `ToolCommandName=specscribe`, `PackageId=SpecScribe`, `Version=0.1.0`).
- **Spectre.Console `0.57.2`** + **Spectre.Console.Cli `0.55.0`** — the command framework. `config.AddExample(string[])` is the API for `--help` examples (Cli 0.55). `AnsiConsole.Profile.Capabilities.Interactive` is the TTY-detection signal (already in use). Spectre's `Progress()` no-ops its live display when non-interactive, but make the branch explicit per Task 1 rather than depending on that.
- Markdig 1.3.2, YamlDotNet 18.1.0 — not relevant to this story.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 5.1] — story statement + ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#NonFunctional Requirements] — NFR2, NFR5, NFR7.
- [Source: _bmad-output/planning-artifacts/epics.md#UX Design Requirements] — UX-DR15.
- [Source: _bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md#FR-12] — CLI-first UX acceptance ("zero required flags in an auto-discoverable repo; explicit source/output flags in non-default layouts").
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md:380] — CLI Output terminal surface (interactive vs non-interactive).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md:59] — Voice & Tone, watch feedback copy.
- [Source: src/SpecScribe/Commands.cs] — current `generate`/`watch`/interactive commands (edit target).
- [Source: src/SpecScribe/ConsoleUi.cs] — presentation layer (edit target for summary + non-interactive branch).
- [Source: src/SpecScribe/ForgeOptions.cs] — discovery/resolution (verify only).
- [Source: tests/SpecScribe.Tests/ForgeOptionsTests.cs] — headless-test pattern to mirror.

### Git Intelligence (recent work patterns)

- CLI command surface: commit `627907d` (Spectre.Console.Cli + interactive menu); persisted settings added in `c5dea36`; module-command detection in `909724c`; split buttons/output change in `9486ac8`.
- Convention: presentation logic is deliberately isolated in `ConsoleUi` so generation code never references Spectre — **honor this seam.** Any new summary/exit-code helper should keep Spectre out of the pure/testable core.
- Recent commits (`9003bf3`, `fafd5a7`, `ae549d5`) are Epic 2/3 rendering + planning work; nothing conflicts with the CLI surface.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Open Questions (for the maintainer — non-blocking; sensible defaults chosen)

1. **Machine-parseable summary format.** I've specified a stable, greppable single line: `SpecScribe: generated=<n> updated=<n> skipped=<n> errors=<n> elapsed_ms=<n>`. If you'd prefer a `--json` opt-in emitting a structured object (instead of / in addition to the key=value line), say so — I kept it to a plain line to avoid scope creep into a serialization contract. The key=value line is forward-compatible with adding `--json` later.
2. **Non-interactive: suppress the pretty table entirely, or keep both?** Default chosen: keep human-readable output minimal (no live progress) but still print the counts + always print the machine line. If you want *only* the machine line in CI (no logo/paths noise), that's a small toggle — flag it.
3. **Watch exit code on initial-build error.** Default chosen: watch does NOT fail-fast (keeps watching, surfaces the error), only `generate` maps errors → exit `1`. Confirm this matches how you'd use `watch` in practice.
