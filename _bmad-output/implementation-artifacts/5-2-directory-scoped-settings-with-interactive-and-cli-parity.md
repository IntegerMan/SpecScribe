# Story 5.2: Directory-Scoped Settings with Interactive and CLI Parity

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a repeat user,
I want settings persisted per repository and overridable per run,
so that I can keep my preferred behavior without hidden global side effects — and get the **same** result whether I configure via the interactive menu or pass equivalent CLI flags.

## Acceptance Criteria

1. **Given** I configure settings interactively (via "Configure paths")
   **When** I run generation later in the same repository — **including a non-interactive `specscribe generate` / `specscribe watch`**
   **Then** the configured defaults are reused from the directory-scoped `.specscribe` file
   **And** behavior matches passing the equivalent CLI arguments.

2. **Given** I pass CLI overrides for a run
   **When** generation starts
   **Then** the effective config resolves **once** with overrides taking precedence over saved settings, and saved settings taking precedence over auto-discovered defaults
   **And** provenance (which source supplied each effective value) is available for diagnostics.

### Derived / cross-cutting acceptance (from NFR7, the "resolve once + preserve provenance" additional requirement, and the 5.1 hand-off)

3. **Given** a `.specscribe` exists but a run passes an explicit override for one field
   **When** the config is resolved
   **Then** only the overridden field reports `CommandLine` provenance; the untouched fields still resolve from `SavedSettings`; and fields absent from both resolve from `Default`
   **And** the precedence order is exactly **CLI > `.specscribe` > auto-discovery/default** for every configurable field (source, ADRs, output, project name, README inclusion).

4. **Given** the README-inclusion preference (`--no-readme`) — today configurable only on the CLI
   **When** I set it in the interactive "Configure paths" flow
   **Then** it persists to `.specscribe` and is honored on subsequent runs, closing the interactive/CLI parity gap that Story 5.1 explicitly deferred to 5.2
   **And** a `.specscribe` written by a prior version (without the field) still loads cleanly (additive, backward-compatible schema).

## Tasks / Subtasks

- [ ] **Task 1 — One load-aware resolution seam that captures provenance (AC: #1, #2, #3)**
  - [ ] Introduce a single resolution entry point that every command routes through. Recommended shape: a `SettingsResolver.Resolve(SiteSettings settings, string? startDirectory = null)` returning a `ResolvedConfig` record: `{ ForgeOptions Options; IReadOnlyList<ConfigProvenance> Provenance; string? SavedSettingsPath }`. Keep it in a new `SettingsResolver.cs` (flat under `src/SpecScribe/`, matching convention).
  - [ ] Inside `Resolve`: (a) snapshot which fields the **CLI** set (non-null on the incoming `SiteSettings` / `NoReadme == true`) *before* mutating anything; (b) `SettingsStore.TryLoad(...)` → if present, `SettingsStore.ApplyTo(saved, settings)`; (c) call `settings.Resolve()` (the existing pure `ForgeOptions.Resolve`) exactly **once**; (d) compute per-field provenance from the pre-mutation snapshot + what `saved` supplied.
  - [ ] Add a small `ConfigSource` enum `{ CommandLine, SavedSettings, Default }` and a `ConfigProvenance` record `{ string Field; string EffectiveValue; ConfigSource Source }`. Provenance is computed for each configurable field: `Source` (source root), `ADRs`, `Output`, `Project name`, `README included`. Rule per field: CLI-set → `CommandLine`; else supplied by `saved` → `SavedSettings`; else → `Default`.
  - [ ] **Do NOT** duplicate resolution logic — `SiteSettings.Resolve()` / `ForgeOptions.Resolve` stay the single pure path-resolution primitive and remain headless-testable. The resolver only layers load + precedence-snapshot + provenance on top.

- [ ] **Task 2 — Route every command through the seam so CLI == interactive (AC: #1, #3)**
  - [ ] `GenerateCommand.Execute` and `WatchCommand.Execute` currently call `settings.Resolve()` directly and **never load `.specscribe`** — this is the primary parity gap. Replace those calls with `SettingsResolver.Resolve(settings)`; use the returned `ForgeOptions`. When saved settings were loaded, print the same "Loaded saved settings" surface the interactive menu already shows (`ConsoleUi.PrintSettingsLoaded`) so the CLI run is transparent about what it inherited.
  - [ ] `InteractiveCommand.RunMenu` currently does its own `TryLoad`/`ApplyTo`/`PrintSettingsLoaded` at menu entry, and `TryResolve` re-runs `settings.Resolve()` per action. Refactor so the menu's generate/watch actions resolve through the **same** `SettingsResolver` (resolve once per action), eliminating the duplicate load path. Keep the "load once at menu entry and show what was restored" UX, and keep `TryResolve`'s `DirectoryNotFoundException` → hint-not-crash behavior.
  - [ ] Preserve `Program.cs` menu-fallback-on-bad-args and the exception→exit-code mapping (`DirectoryNotFoundException` → `1`, parse error → menu-if-interactive-else-`1`). The resolver must let a genuine discovery failure surface as `DirectoryNotFoundException` for `generate`/`watch` (fatal) while the menu catches it as a soft hint — mirror how `settings.Resolve()` throws today.

- [ ] **Task 3 — Provenance diagnostic surface (AC: #2)**
  - [ ] Annotate the always-printed paths block: extend `ConsoleUi.PrintPaths` (or add an overload that takes the provenance list) so each row carries a dim provenance tag — e.g. `Sources  <path>  [grey](.specscribe)[/]` / `[grey](--source)[/]` / `[grey](auto)[/]`. This makes "which source won" visible on every run without a new flag. Reuse the existing grid; keep colors/labels consistent with the current `PrintPaths` styling.
  - [ ] Add an on-demand, machine-friendly diagnostic: a `--show-config` boolean option on `SiteSettings` that, when set, prints the effective config + per-field provenance + the resolved `.specscribe` path and **exits `0` without generating** (aligns with Story 5.1's machine-parseable, CI-friendly ethos). Emit a stable, greppable line per field (single line, no markup), e.g. `SpecScribe config: source=<path> (savedsettings) output=<path> (commandline) ...`. Wire it in `GenerateCommand.Execute` (and `watch`) as an early return after resolution. See Open Question #2 for flag-vs-subcommand.
  - [ ] Keep the human-readable annotated `PrintPaths` and the machine `--show-config` line as separate surfaces (one for humans, one for `grep`/CI) — do not conflate, same discipline as 5.1's summary line.

- [ ] **Task 4 — Directory-scoped `.specscribe` discovery + README-inclusion parity (AC: #1, #4; NFR7)**
  - [ ] `SettingsStore` currently anchors `.specscribe` at raw `Directory.GetCurrentDirectory()`, so a run from a subdirectory misses a `.specscribe` that sits at the repo root even though `ForgeOptions` walks up to find `_bmad-output`. Add a `startDirectory` seam and a **git-style walk-up read**: `TryLoad` walks up from the start directory to the first `.specscribe` found (independent of `_bmad-output`, avoiding a circular dependency with source discovery). Writes should target the resolved root (the directory the loaded `.specscribe` lives in, or the repo root / cwd on first save) so read and write are symmetric and predictable. Preserve the best-effort semantics (missing/malformed/unreadable → "no saved settings", never an error).
  - [ ] Close the README parity gap: `SavedSettings` persists `Source`/`Adrs`/`Output`/`ProjectName` but **not** README inclusion, so `--no-readme` cannot be saved and the interactive flow can't configure it. Add a nullable `bool? IncludeReadme` (nullable so "unset" is distinct from "explicitly include/exclude") to `SavedSettings`; thread it through `SettingsStore.ApplyTo` (fill `settings.NoReadme` only when the CLI didn't already opt out) and `TrySave`. Update `IsEmpty` to account for it.
  - [ ] Add the matching interactive control in `InteractiveCommand.ConfigurePaths`: a confirm prompt ("Include the repository README?") whose result maps to `NoReadme`, persisted alongside the paths. This is the interactive half of AC #4.
  - [ ] Confirm backward compatibility: a `.specscribe` written before this field exists deserializes fine (the property is simply absent → `null`). `.specscribe` remains **gitignored / personal** (per the root `.gitignore` comment "SpecScribe per-user saved interactive settings") — do not commit it or move it to a global location; "directory-scoped, no hidden global side effects" is the story's whole point.

- [ ] **Task 5 — Tests (AC: #1–#4)**
  - [ ] New `SettingsResolverTests.cs` (headless, temp-dir fixtures mirroring `ForgeOptionsTests`/`SettingsStoreTests`): (a) precedence — CLI beats `.specscribe` beats default, per field; (b) provenance — each field reports the correct `ConfigSource` given combinations of CLI-set / saved-only / neither; (c) `resolves once` — the pure `ForgeOptions.Resolve` is invoked a single time per `Resolve` call (assert via observable effect, e.g. one returned `Options`, not by counting internals if awkward).
  - [ ] Extend `SettingsStoreTests.cs`: walk-up discovery finds a `.specscribe` in a parent directory; write-then-read round-trip including the new `IncludeReadme`; a `.specscribe` JSON *without* `IncludeReadme` still loads (backward compat); malformed JSON → `TryLoad` returns null (best-effort).
  - [ ] Keep Spectre/`AnsiConsole` out of the units — extract the provenance-line string building into a small pure helper (mirroring how 5.1 extracted the summary/exit-code helpers) so `--show-config`'s output is asserted without a live console.
  - [ ] Run the full suite: `dotnet test` from repo root; all existing tests (incl. the four `SettingsStoreTests`) stay green.

## Dev Notes

### ⚠️ Critical framing: this is a HARDENING + PARITY story, not a greenfield build

The persistence primitives **already exist**: `SettingsStore` (`.specscribe` read/write/apply), `SavedSettings`, the interactive "Configure paths" flow, and CLI-precedence-over-saved (`SettingsStore.ApplyTo`). **Do not rebuild them.** The actual gap is narrow and specific:

1. **The CLI path never loads `.specscribe`.** `SettingsStore.TryLoad`/`ApplyTo`/`PrintSettingsLoaded` are called **only** in `InteractiveCommand.RunMenu`. `GenerateCommand.Execute` and `WatchCommand.Execute` call `settings.Resolve()` directly, so a user who configures interactively and then runs `specscribe generate` gets **none** of their saved settings. That breaks AC #1's "behavior matches equivalent CLI arguments" and "reused when I run generation later." **This is the single most important fix in the story.**
2. **No provenance exists.** Nothing records whether an effective value came from the CLI, `.specscribe`, or a default. AC #2 requires it "for diagnostics."
3. **`--no-readme` isn't persistable** and has no interactive control — a real parity gap (NFR7) that Story 5.1 explicitly punted here (5.1 Dev Notes: "If you notice a parity gap, note it — don't fix it in 5.1").

Reinventing `SettingsStore` or a fresh config system is the primary failure mode to avoid.

### Current state of the files you will touch (read before editing)

- **`src/SpecScribe/SettingsStore.cs`** — `SavedSettings` (Source/Adrs/Output/ProjectName + `IsEmpty`), `SettingsStore` (`FileName = ".specscribe"`, `ResolvePath()` = cwd-anchored, `TryLoad()` best-effort with `IOException`/`JsonException` swallow, `TrySave(SiteSettings)`, `ApplyTo(saved, settings)` = fill-nulls-so-CLI-wins). Uses `MarkdownConverter.ReadAllTextShared` for shared-read (honors NFR5). **Edit targets:** add `startDirectory`/walk-up to load path, add `bool? IncludeReadme`. **Must preserve:** best-effort tolerance, `System.Text.Json` `WhenWritingNull` + `WriteIndented` serialization, and the CLI-wins semantics of `ApplyTo`.
- **`src/SpecScribe/SiteSettings.cs`** — `CommandSettings` subclass with the five `[CommandOption]`s (`-s|--source`, `-a|--adrs`, `-o|--output`, `-p|--project-name`, `--no-readme`) and `Resolve()` → `ForgeOptions.Resolve(...)`. **Do not rename/re-shortcut existing options** (breaking for users). Add `--show-config` here (new, additive). `Resolve()` stays the pure primitive.
- **`src/SpecScribe/ForgeOptions.cs`** — pure resolution (headless-testable via `startDirectory`). Walk-up discovery of `_bmad-output`, derives repoRoot/output/ADR/title, reads `project_name` from `_bmad/config.toml`. `AdrSourceExplicit` already tracks "explicitly set vs defaulted" (but conflates CLI+saved — provenance needs the finer three-way distinction, which lives in the resolver, not here). **No behavior change expected** — reuse it as-is.
- **`src/SpecScribe/Commands.cs`** — `GenerateCommand.Execute` (→ `settings.Resolve()` → `PrintLogo`/`PrintPaths` → `RunGeneration` → `return 0`), `WatchCommand.Execute` (→ resolve → generate → `RunWatchLoop`), `InteractiveCommand.RunMenu`/`TryResolve`/`ConfigurePaths`. **Edit targets:** swap direct `settings.Resolve()` for `SettingsResolver.Resolve`; unify the menu onto the same seam; add the README prompt in `ConfigurePaths`. **Must preserve:** the menu's `DirectoryNotFoundException`→hint behavior, the Ctrl+C/`ProcessExit` watch-stop path, and `RunGeneration` returning the `SiteGenerator` for watch reuse.
- **`src/SpecScribe/ConsoleUi.cs`** — presentation only (no generation refs — honor that seam). `PrintPaths(ForgeOptions)` (Project/Sources/ADRs/Output grid), `PrintSettingsLoaded(path, saved)`, `PrintSettingsSaved(path)`, `PrintUsage`. **Edit targets:** provenance annotations on `PrintPaths`; a helper to render the `--show-config` line. Keep Spectre out of the pure string-building helper so it's unit-testable.
- **`src/SpecScribe/Program.cs`** — composition root; registers `generate`/`watch`, `UseStrictParsing()` + `PropagateExceptions()`, maps parse errors → menu (interactive) or `1`. **Must preserve** this exception→exit-code + menu-fallback behavior. If `--show-config` becomes a subcommand instead of a flag (Open Q #2), it registers here.

### What must be preserved (regression guard — the system must work end-to-end)

1. **Interactive UX unchanged for humans:** logo, `PrintPaths`, "Loaded saved settings" grid on restore, "Saved settings to …" confirmation, live progress, summary table, watch footer, Ctrl+C stop — all remain for TTY sessions.
2. **Menu fallback on bad args:** `Program.cs` still drops into `InteractiveCommand.RunMenu` on a parse error when interactive, returns `1` when not.
3. **CLI-wins precedence is not weakened:** an explicit `--source` (etc.) must still beat a saved `.specscribe` value (the four `SettingsStoreTests` pin this — keep them green).
4. **`.specscribe` stays personal + gitignored + directory-scoped.** No global (`~/.config`) fallback, no committing it, no telemetry (NFR3, local-first). "No hidden global side effects" is the story's core promise.
5. **Shared-read / no write-lock invariant (NFR5):** keep reading `.specscribe` via `MarkdownConverter.ReadAllTextShared`; don't introduce write locks on the watched tree. `.specscribe` writes happen only from the interactive "Configure paths" action, not during watch.
6. **Best-effort persistence:** a missing/malformed/unreadable `.specscribe` degrades to "no saved settings," never a crash (NFR2).

### Scope boundaries — do NOT drift into sibling stories

- **Story 5.1 (CLI generate/watch, non-interactive feedback, exit codes) — status `ready-for-dev`, NOT done.** 5.1 also edits `Commands.cs` (`GenerateCommand.Execute` exit code) and `ConsoleUi.cs` (machine-parseable summary + non-interactive branch). **Coordination:** if 5.1 lands first, thread your resolver change through its non-zero-exit return rather than reverting to `return 0`; if 5.2 lands first, keep `Execute` returning `0` and leave a clear seam. Either way, do not undo 5.1's non-interactive/exit-code work, and keep the machine-parseable *summary* line (5.1) distinct from the `--show-config` *provenance* line (5.2). Flag any merge friction rather than guessing.
- **Story 5.3 (watch safety, scope-aware rebuilds):** debounce, rename/delete topology, rebuild scope — out of scope. Don't touch `FileWatcherService` or `SiteGenerator.Regenerate*`.
- **Epic 4 (framework generalization):** no alternate source-dir names / non-BMad layout heuristics. `.specscribe` walk-up here is for the *settings file*, not for generalizing artifact discovery.

### Requirements traceability

- **NFR7** (configurability parity across interactive menu and equivalent CLI parameters, **with directory-scoped settings persistence**) — the story's spine; AC #1/#3/#4 exist to satisfy it.
- **Additional Requirement** (epics.md:68): "Resolve effective settings **once** per run from directory-scoped settings plus run overrides, **preserving provenance**." — AC #2 is a direct restatement.
- **FR12 / FR-12** (CLI-first generate + watch; auto-discovery defaults **plus explicit overrides**; help documents options) — parity means the CLI overrides and the persisted defaults compose predictably.
- **NFR3** (local-first, privacy-preserving, no remote telemetry) and **NFR2** (resilient to malformed/missing artifacts) — `.specscribe` stays local and best-effort.
- **NFR5** (shared-read, no write locks) — preserved via `ReadAllTextShared`.

### Project Structure Notes

- All CLI/console code is flat under `src/SpecScribe/` (no `Cli/` subfolder) — put `SettingsResolver.cs` there next to `SettingsStore.cs`, matching convention.
- Tests: `tests/SpecScribe.Tests/`, xUnit `[Fact]`, one file per subject, temp-dir fixtures via `Directory.CreateTempSubdirectory` with `IDisposable` cleanup (see `ForgeOptionsTests.cs`, `SettingsStoreTests.cs`). Prefer headless helpers over driving `AnsiConsole`.
- `.specscribe` default filename lives on `SettingsStore.FileName`; the output dir default (`SpecScribeOutput`) lives on `ForgeOptions.OutputDirName` — single sources of truth, don't hardcode either.

### Technology / library specifics (verified against `SpecScribe.csproj`)

- **.NET `net10.0`**, `Nullable` + `ImplicitUsings` enabled. Packaged as a dotnet global tool (`ToolCommandName=specscribe`).
- **`System.Text.Json`** (BCL) is already the `.specscribe` serializer — `JsonIgnoreCondition.WhenWritingNull` + `WriteIndented`. Adding `bool? IncludeReadme` is additive and backward-compatible (absent property → `null` on deserialize). `[JsonIgnore]` is already used for `IsEmpty` — reuse the pattern if you add computed members.
- **Spectre.Console `0.57.2` / Spectre.Console.Cli `0.55.0`** — `[CommandOption]`/`[Description]` drive `--help`; `AnsiConsole.Profile.Capabilities.Interactive` is the TTY signal (used for the menu's non-interactive usage branch). A boolean flag like `--show-config` is a plain `[CommandOption("--show-config")] public bool ShowConfig { get; set; }`.
- Markdig / YamlDotNet — not relevant here.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 5.2] — story statement + ACs.
- [Source: _bmad-output/planning-artifacts/epics.md:62] — NFR7 (parity + directory-scoped persistence).
- [Source: _bmad-output/planning-artifacts/epics.md:68] — "Resolve effective settings once per run … preserving provenance."
- [Source: _bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md#FR-12] — CLI-first UX (auto-discovery defaults + explicit overrides).
- [Source: src/SpecScribe/SettingsStore.cs] — `.specscribe` persistence + `ApplyTo` precedence (edit target).
- [Source: src/SpecScribe/Commands.cs] — generate/watch/interactive commands (edit target; the CLI-bypasses-load gap is here).
- [Source: src/SpecScribe/SiteSettings.cs] — CLI options + `Resolve()` primitive (add `--show-config`).
- [Source: src/SpecScribe/ForgeOptions.cs] — pure path resolution + `AdrSourceExplicit` (reuse as-is).
- [Source: src/SpecScribe/ConsoleUi.cs] — `PrintPaths`/`PrintSettingsLoaded` (provenance annotation target).
- [Source: tests/SpecScribe.Tests/SettingsStoreTests.cs] — existing precedence/`IsEmpty` pins to keep green.
- [Source: tests/SpecScribe.Tests/ForgeOptionsTests.cs] — headless temp-dir test pattern to mirror.
- [Source: _bmad-output/implementation-artifacts/5-1-cli-generate-and-watch-modes-with-smart-defaults.md] — sibling story; the deferred `--no-readme`/parity note and the machine-parseable-line convention originate here.
- [Source: .gitignore:487] — `.specscribe` is "SpecScribe per-user saved interactive settings" (gitignored/personal by design).

### Git Intelligence (recent work patterns)

- Persisted settings were added in commit `c5dea36`; the CLI command surface + interactive menu in `627907d`. `SettingsStore`/`SavedSettings` and the "Configure paths" flow are the artifacts of that work — this story formalizes and unifies them, it does not reintroduce them.
- Convention: presentation is isolated in `ConsoleUi`; generation never references Spectre — keep any new provenance string-building in a pure helper so it's testable without a live console (same discipline `ForgeOptions`/`SettingsStore` already follow).
- Recent commits (`9029daa`, `7ccba21`, `9003bf3`) are Epic 2/3 rendering + planning; nothing conflicts with the settings/CLI surface. The live coordination risk is Story 5.1 (also `ready-for-dev`), which edits the same two files — see Scope boundaries.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Open Questions (for the maintainer — non-blocking; sensible defaults chosen)

1. **`.specscribe` location — cwd vs walk-up vs repo-root.** Default chosen: **git-style walk-up read** (find the nearest `.specscribe` at or above the start directory), writes anchored at the resolved root, so settings apply consistently from any subdirectory. The alternative (keep raw-cwd anchoring, current behavior) is simpler but silently misses settings when you run from a subfolder. I avoided anchoring `.specscribe` strictly at the discovered `_bmad-output` root because a saved `--source` could itself relocate that root (circular). Confirm walk-up is what you want, or say "keep cwd-only" to shrink the change.
2. **Provenance diagnostic: `--show-config` flag vs `config` subcommand vs always-on annotations only.** Default chosen: always-on dim provenance tags in `PrintPaths` **plus** a `--show-config` flag that prints a machine-parseable provenance line and exits `0`. If you'd rather this be a `specscribe config` subcommand (discoverable in `--help` alongside generate/watch), or want *only* the always-on annotations and no separate flag, say so — the resolver captures provenance either way, so the surface is a thin, swappable layer.
3. **Persist README preference as `bool? IncludeReadme`.** Default chosen: nullable so "never configured" stays distinct from "explicitly include." This means a user who never touches the README toggle keeps today's include-by-default behavior, and only an explicit interactive/CLI choice is persisted. Confirm you want the interactive "Configure paths" flow to prompt for it (adds one confirm step), or prefer README stay CLI-only (which would leave a known parity gap open).
