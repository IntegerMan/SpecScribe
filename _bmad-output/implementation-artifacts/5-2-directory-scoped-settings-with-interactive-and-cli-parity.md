---
baseline_commit: 6e12d0d79bbd891e20603759218699b0b4f1aeef
---

# Story 5.2: Directory-Scoped Settings with Interactive and CLI Parity

Status: done

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

- [x] **Task 1 — One load-aware resolution seam that captures provenance (AC: #1, #2, #3)**
  - [x] Introduce a single resolution entry point that every command routes through. Recommended shape: a `SettingsResolver.Resolve(SiteSettings settings, string? startDirectory = null)` returning a `ResolvedConfig` record: `{ ForgeOptions Options; IReadOnlyList<ConfigProvenance> Provenance; string? SavedSettingsPath }`. Keep it in a new `SettingsResolver.cs` (flat under `src/SpecScribe/`, matching convention).
  - [x] Inside `Resolve`: (a) snapshot which fields the **CLI** set (non-null on the incoming `SiteSettings` / `NoReadme == true`) *before* mutating anything; (b) `SettingsStore.TryLoad(...)` → if present, `SettingsStore.ApplyTo(saved, settings)`; (c) call `settings.Resolve()` (the existing pure `ForgeOptions.Resolve`) exactly **once**; (d) compute per-field provenance from the pre-mutation snapshot + what `saved` supplied.
  - [x] Add a small `ConfigSource` enum `{ CommandLine, SavedSettings, Default }` and a `ConfigProvenance` record `{ string Field; string EffectiveValue; ConfigSource Source }`. Provenance is computed for each configurable field: `Source` (source root), `ADRs`, `Output`, `Project name`, `README included`. Rule per field: CLI-set → `CommandLine`; else supplied by `saved` → `SavedSettings`; else → `Default`.
  - [x] **Do NOT** duplicate resolution logic — `SiteSettings.Resolve()` / `ForgeOptions.Resolve` stay the single pure path-resolution primitive and remain headless-testable. The resolver only layers load + precedence-snapshot + provenance on top.

- [x] **Task 2 — Route every command through the seam so CLI == interactive (AC: #1, #3)**
  - [x] `GenerateCommand.Execute` and `WatchCommand.Execute` currently call `settings.Resolve()` directly and **never load `.specscribe`** — this is the primary parity gap. Replace those calls with `SettingsResolver.Resolve(settings)`; use the returned `ForgeOptions`. When saved settings were loaded, print the same "Loaded saved settings" surface the interactive menu already shows (`ConsoleUi.PrintSettingsLoaded`) so the CLI run is transparent about what it inherited.
  - [x] `InteractiveCommand.RunMenu` currently does its own `TryLoad`/`ApplyTo`/`PrintSettingsLoaded` at menu entry, and `TryResolve` re-runs `settings.Resolve()` per action. Refactor so the menu's generate/watch actions resolve through the **same** `SettingsResolver` (resolve once per action), eliminating the duplicate load path. Keep the "load once at menu entry and show what was restored" UX, and keep `TryResolve`'s `DirectoryNotFoundException` → hint-not-crash behavior.
  - [x] Preserve `Program.cs` menu-fallback-on-bad-args and the exception→exit-code mapping (`DirectoryNotFoundException` → `1`, parse error → menu-if-interactive-else-`1`). The resolver must let a genuine discovery failure surface as `DirectoryNotFoundException` for `generate`/`watch` (fatal) while the menu catches it as a soft hint — mirror how `settings.Resolve()` throws today.

- [x] **Task 3 — Provenance diagnostic surface (AC: #2)**
  - [x] Annotate the always-printed paths block: extend `ConsoleUi.PrintPaths` (or add an overload that takes the provenance list) so each row carries a dim provenance tag — e.g. `Sources  <path>  [grey](.specscribe)[/]` / `[grey](--source)[/]` / `[grey](auto)[/]`. This makes "which source won" visible on every run without a new flag. Reuse the existing grid; keep colors/labels consistent with the current `PrintPaths` styling.
  - [x] Add an on-demand, machine-friendly diagnostic: a `--show-config` boolean option on `SiteSettings` that, when set, prints the effective config + per-field provenance + the resolved `.specscribe` path and **exits `0` without generating** (aligns with Story 5.1's machine-parseable, CI-friendly ethos). Emit a stable, greppable line per field (single line, no markup), e.g. `SpecScribe config: source=<path> (savedsettings) output=<path> (commandline) ...`. Wire it in `GenerateCommand.Execute` (and `watch`) as an early return after resolution. See Open Question #2 for flag-vs-subcommand.
  - [x] Keep the human-readable annotated `PrintPaths` and the machine `--show-config` line as separate surfaces (one for humans, one for `grep`/CI) — do not conflate, same discipline as 5.1's summary line.

- [x] **Task 4 — Directory-scoped `.specscribe` discovery + README-inclusion parity (AC: #1, #4; NFR7)**
  - [x] `SettingsStore` currently anchors `.specscribe` at raw `Directory.GetCurrentDirectory()`, so a run from a subdirectory misses a `.specscribe` that sits at the repo root even though `ForgeOptions` walks up to find `_bmad-output`. Add a `startDirectory` seam and a **git-style walk-up read**: `TryLoad` walks up from the start directory to the first `.specscribe` found (independent of `_bmad-output`, avoiding a circular dependency with source discovery). Writes should target the resolved root (the directory the loaded `.specscribe` lives in, or the repo root / cwd on first save) so read and write are symmetric and predictable. Preserve the best-effort semantics (missing/malformed/unreadable → "no saved settings", never an error).
  - [x] Close the README parity gap: `SavedSettings` persists `Source`/`Adrs`/`Output`/`ProjectName` but **not** README inclusion, so `--no-readme` cannot be saved and the interactive flow can't configure it. Add a nullable `bool? IncludeReadme` (nullable so "unset" is distinct from "explicitly include/exclude") to `SavedSettings`; thread it through `SettingsStore.ApplyTo` (fill `settings.NoReadme` only when the CLI didn't already opt out) and `TrySave`. Update `IsEmpty` to account for it.
  - [x] Add the matching interactive control in `InteractiveCommand.ConfigurePaths`: a confirm prompt ("Include the repository README?") whose result maps to `NoReadme`, persisted alongside the paths. This is the interactive half of AC #4.
  - [x] Confirm backward compatibility: a `.specscribe` written before this field exists deserializes fine (the property is simply absent → `null`). `.specscribe` remains **gitignored / personal** (per the root `.gitignore` comment "SpecScribe per-user saved interactive settings") — do not commit it or move it to a global location; "directory-scoped, no hidden global side effects" is the story's whole point.

- [x] **Task 5 — Tests (AC: #1–#4)**
  - [x] New `SettingsResolverTests.cs` (headless, temp-dir fixtures mirroring `ForgeOptionsTests`/`SettingsStoreTests`): (a) precedence — CLI beats `.specscribe` beats default, per field; (b) provenance — each field reports the correct `ConfigSource` given combinations of CLI-set / saved-only / neither; (c) `resolves once` — the pure `ForgeOptions.Resolve` is invoked a single time per `Resolve` call (assert via observable effect, e.g. one returned `Options`, not by counting internals if awkward).
  - [x] Extend `SettingsStoreTests.cs`: walk-up discovery finds a `.specscribe` in a parent directory; write-then-read round-trip including the new `IncludeReadme`; a `.specscribe` JSON *without* `IncludeReadme` still loads (backward compat); malformed JSON → `TryLoad` returns null (best-effort).
  - [x] Keep Spectre/`AnsiConsole` out of the units — extract the provenance-line string building into a small pure helper (mirroring how 5.1 extracted the summary/exit-code helpers) so `--show-config`'s output is asserted without a live console.
  - [x] Run the full suite: `dotnet test` from repo root; all existing tests (incl. the four `SettingsStoreTests`) stay green.

### Review Findings

_Code review 2026-07-25 (bmad-code-review), scoped to `git diff 6e12d0d..HEAD` restricted to this story's own File List (sibling stories 5.1/5.3/5.5/5.6 and epic-20/25/26 changes bundled in the same commit range excluded). Three parallel layers: Blind Hunter (adversarial), Edge Case Hunter, Acceptance Auditor (against this story's 4 ACs)._

- [x] [Review][Decision] `.specscribe` walk-up read is anchored to the run's own start directory, independent of `ForgeOptions`' discovered `_bmad-output` root — settings saved from a subdirectory before any `.specscribe` exists can silently fail to apply when a later run starts from the true repo root. This restates the story's own unresolved Open Question #1 (walk-up-read vs repo-root anchoring). — **RESOLVED by owner 2026-07-25: keep walk-up-read as-is** (the anchoring behavior is unchanged). Owner additionally directed that `.specscribe` become a FOLDER containing `config.json` rather than a flat file, to leave room for future per-directory state (incremental-build caching, run-history tracking) — implemented in this review pass, see below and **ADR 0014**. [src/SpecScribe/SettingsStore.cs]
- [x] [Review][Patch] `ConsoleUi.PrintConfigDiagnostics` writes `--show-config` lines straight to `Console.Out` with no `IOException` guard, unlike `PrintMachineSummary` (patched in Story 5.1 for the identical "downstream pipe closed early" failure) — `specscribe generate --show-config | head` can flip an otherwise-successful run to a fatal exit. [src/SpecScribe/ConsoleUi.cs:117-123] — **Applied**: wrapped in `try/catch (IOException)`, same guard/reason as `PrintMachineSummary`.
- [x] [Review][Patch] `CliOverrides.Capture` treats an empty-string CLI value (e.g. `--source ""`) as "not CLI-set" (`{Length: > 0}`), but `SettingsStore.ApplyTo` treats it as already-set (`??=` does not fill over an empty string) — the two predicates disagree, so an explicit empty override silently wins while its `--show-config` provenance is misreported as `SavedSettings`/`Default`. [src/SpecScribe/SettingsResolver.cs:35-46, src/SpecScribe/SettingsStore.cs:172-195] — **Applied**: `CliOverrides.Capture`'s path/name predicates changed to `is not null`, matching `ApplyTo`'s `??=`. `TodayPolicy` deliberately left as `{ Length: > 0 }` (already internally consistent with `ResolveDatePolicy`/`Validate`).
- [x] [Review][Patch] When `SettingsStore.TrySave` fails inside `ConfigurePaths` (write-protected path, disk full), the method returns the pre-edit `load` unchanged even though `settings` was already mutated with the newly typed values — subsequent provenance reports mislabel those live values as `Default` rather than "entered but not persisted". [src/SpecScribe/Commands.cs:622-626] — **Applied**: `ConfigurePaths` now distinguishes "nothing worth saving" (silent, expected) from "the write failed" (`Capture(settings).IsEmpty` check) and warns the user in the latter case that the choices apply to this session only.
- [x] [Review][Patch] `FormatConfigLines` has no guard against an embedded newline in a field's `EffectiveValue` (reachable via a hand-edited `.specscribe` or a quoted CLI value) — a multi-line value would split across extra unprefixed lines, breaking the documented one-line-per-field `--show-config` contract CI scripts rely on. [src/SpecScribe/SettingsResolver.cs:169-184] — **Applied**: new `EscapeForLine` helper collapses `\r\n`/`\r`/`\n` to a literal `\n` escape before formatting; test added.
- [x] [Review][Patch] `SettingsResolver.Load` performs two independent `.specscribe` walk-ups per call (once inside `SettingsStore.TryLoad`, again via `SettingsStore.ResolvePath` for `SettingsLoad.Path`) — minor redundant work and a theoretical TOCTOU window where the reported settings-file path could name a different file than the one actually parsed. [src/SpecScribe/SettingsResolver.cs:89-100] — **Applied**: new `SettingsStore.TryLoad(startDirectory, out loadedFrom)` overload walks up exactly once and reports the location that actually supplied the data; `SettingsResolver.Load` now uses it instead of a separate `ResolvePath` call. Bundled with the next finding (same walk-up rewrite).
- [x] [Review][Patch] `SettingsStore.FindExisting` picks the nearest `.specscribe` purely by `File.Exists`, and `TryLoad` returns null for the whole lookup on a parse failure instead of continuing up-tree — a malformed nearest file silently shadows a perfectly valid ancestor file with no fallback or warning. [src/SpecScribe/SettingsStore.cs:68-111] — **Applied**: the new `TryLoad` walk-up skips a candidate that fails to parse and continues to the next ancestor instead of stopping; `FindExisting`/`ResolvePath` (existence-only, used by the write path) are unchanged. 2 new tests.
- [x] [Review][Patch] No test locks the `saved.IncludeReadme == true` + no-CLI-override provenance branch — only the `false`-saved and saved-with-CLI-override cases are covered; logic verified correct by inspection but the leaf is uncovered. [tests/SpecScribe.Tests/SettingsResolverTests.cs:150-184] — **Applied**: `Resolve_AttributesAnExplicitPersistedReadmeInclusionToSavedSettings` added.

All 7 patches applied 2026-07-25. Verification: `dotnet test --filter "SettingsStoreTests|SettingsResolverTests"` — 65 passed / 0 failed (includes 8 new/changed tests across the patches). Full suite re-run: golden content-fingerprint and golden output-inventory both pass (regenerated by the concurrent Story 20.5 session on top of this work, see `SiteGeneratorAdapterTests.cs`'s provenance comment); the remaining 5–8 failures across two full-suite runs are the pre-existing git-fixture/concurrency flake this repo's other stories already document (varying failing set run-to-run, "git CLI unavailable on this host" errors under concurrent load) — none touch `SettingsStore`/`SettingsResolver`/`ConsoleUi`/`Commands`/`HowToReadTemplater`.

10 findings dismissed as noise/false-positive/by-design (see review record): CodeUrl's `Default` provenance conflating "hardcoded default" with "auto-detected" (consistent with how every other auto-discovered field already reports `Default`, not a 5.2-specific inconsistency); `PrintPaths`'s `ToDictionary` on a hardcoded 8-entry array that cannot contain duplicate keys; `SettingsResolver.Resolve` trusting the caller to keep `load`/`settings` in sync (a documented convention, not a reachable bug); a golden-fingerprint comment nitpick; `--show-config` printing the human paths block before the machine lines (deliberate — both surfaces are documented as intentionally coexisting); the README-inclusion prompt's boolean mapping (verified correct by trace; TTY-only, already flagged in Completion Note #9 as awaiting owner verification); no invariant-checking constructor on `SettingsLoad`/`ResolvedConfig` (design nitpick); `--show-config` propagating `DirectoryNotFoundException` on discovery failure (confirmed intentional — `Program.cs:59-63` catches it and exits 1 with a friendly message, exactly as documented at `Commands.cs:472`); `SettingsStore.ResolvePath`'s default-parameter widening (no caller outside this story's File List uses the zero-arg overload); Completion Note #7's "constant unchanged" wording read against the full `baseline..HEAD` diff (verified accurate for 5.2's own isolated contribution — the visible constant change is ~5 sibling stories' bundled regenerations on shared `main`, not 5.2's).

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

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

**Golden-fingerprint investigation (see Completion Note #7).** Isolated with four controlled renders of the same
synthetic fixture: `git archive HEAD` → scratch (no `.git`), the same scratch with only this story's source files
overlaid (identical hash — proves this story is rendering-neutral), then two full clones checked out at `2be7f6d`
and `6e12d0d` with the normalized fingerprint input dumped to disk and diffed. The diff was exactly **one line**:
`<dt>Build</dt><dd><date-iso> · 2be7f6d</dd>` vs `… · 6e12d0d`.

### Completion Notes List

1. **The parity gap is closed at the seam, not per-command.** `SettingsResolver` (new) is the single entry point;
   `generate`, `watch`, the bare default command, and the interactive menu all route through it. Before this story
   `GenerateCommand`/`WatchCommand` called `settings.Resolve()` directly and **never** read `.specscribe`, so
   configuring interactively and then running the CLI silently discarded every saved value. Verified live: a bare
   `specscribe generate` started three directories deep inherited the repo-root `Output`, `ProjectName`, and README
   exclusion, and wrote 11 pages into the saved output dir with `readme.html` correctly absent.

2. **Ordering is load-bearing — the CLI snapshot must precede the merge.** `SettingsStore.ApplyTo` fills nulls
   *in place*, so once it has run there is no way to distinguish a CLI-supplied value from a restored one.
   `CliOverrides.Capture` therefore runs first, inside `SettingsResolver.Load`. This is also why the menu loads
   **once at entry** and resolves per action from that one `SettingsLoad`: a per-action load would re-snapshot the
   already-merged settings and misreport every restored value as a command-line override. Pinned by
   `Resolve_KeepsProvenanceStableAcrossRepeatedResolvesFromOneLoad`.

3. **"Resolve once" is tested as *cannot disagree*, not as *call count*.** The requirement's real risk is two
   resolutions drifting, so `Resolve_ReportsProvenanceValuesTakenFromTheSameResolvedOptions` asserts every reported
   `EffectiveValue` is the value carried by the returned `ForgeOptions` — provably one resolution feeding both —
   rather than counting invocations through an awkward internal seam.

4. **`--show-config` emits one line per field, not the single packed line the task sketched.** Path values routinely
   contain spaces, which a packed `key=value key=value` line cannot be split back out of without a quoting scheme.
   Shape is `SpecScribe config: field=<key> origin=<commandline|savedsettings|default> value=<value>` with `value=`
   **last**, so everything after it is the value; plus a `settings_file=<path>|(none)` line. Origin tokens are
   spelled out in `OriginToken` (not `enum.ToString()`) so renaming a member cannot silently change a published CI
   contract. Built by the pure, Spectre-free `SettingsResolver.FormatConfigLines` and written straight to
   `Console.Out` — the same bypass, for the same reason, as Story 5.1's summary line (Spectre wraps at 80 cols once
   stdout is redirected, and absolute paths are exactly the lines long enough to wrap).

5. **README persistence uses the established tri-state discipline, and only the non-default is written.**
   `SavedSettings.IncludeReadme` is `bool?`; `Capture` writes `false` for an explicit exclusion and leaves `null`
   otherwise — writing `true` on every save would make `IsEmpty` always false and produce a `.specscribe` for a user
   who configured nothing. The read side still honors an explicit `true` (a hand-edited file), so the tri-state is
   preserved where it matters. Backward compatibility confirmed: a `.specscribe` written without the property
   deserializes to `null` and loads unchanged.

6. **Scope held.** No `FileWatcherService`/`Regenerate*` changes (5.3), no new discovery heuristics (Epic 4). Story
   5.1 landed first, so its work was threaded through rather than reverted: `generate` still returns
   `run.ExitCode`, `watch` still ignores it deliberately, the machine summary line is untouched, and the new
   `--show-config` provenance line is kept a **separate** surface from 5.1's summary line.

7. **⚠️ Fixed a pre-existing golden-fingerprint defect that is NOT a rendering change — please review this
   separately.** `GenerateAll_GoldenContentFingerprint` was failing at baseline `6e12d0d`, before any of my edits.
   Root cause: in `SiteGeneratorAdapterTests.NormalizeVolatile`, `FoldToday` runs *before* `BuildRow` and rewrites
   the build date to the `<date-iso>` **placeholder** — whose leading `<` the `[^<]*` negated class cannot cross, so
   `BuildRow` silently stopped matching its own row and let the **short commit SHA** through into the hash. The
   constant therefore drifted on every commit: it is captured pre-commit (when the About row still shows the
   *previous* SHA), then fails the moment the work lands — which reads as a rendering regression and invites a
   needless regeneration. (This is very likely why Story 5.1's record reports the fingerprint "UNCHANGED" while it
   is red on main.) Fix is one character class: `[^<]*` → `.*?`. **The constant was NOT regenerated — it did not
   need to be.** With the fix the *existing, unchanged* `aaef12dd…` constant passes at **both** `2be7f6d` and
   `6e12d0d`, which is the proof the normalization is now genuinely commit-independent. This edit is outside the
   story's own File List (it is `SiteGeneratorAdapterTests.cs`, a shared golden harness) and is flagged here
   deliberately rather than folded in silently.

8. **Two small in-scope transparency additions beyond the literal task text**, both noted for review:
   (a) provenance also covers `deep_git` and `code_url` — they are persisted fields, and omitting them from the
   diagnostic would leave a hole in "every configurable field"; (b) `PrintSettingsLoaded` now lists the restored
   README / deep-git / code-URL values, not just the four paths, so a CLI user who never opened the menu can see
   why the README vanished. An auto-detected `code_url` correctly reports `default` (it is discovery, not CLI or
   saved).

9. **VERIFICATION LIMIT, recorded honestly.** The **non-interactive** paths are proven live end-to-end (saved
   settings applied from a subdirectory; single-field override reporting only that field as `commandline` while the
   others stayed `savedsettings`/`default`; real 11-page generation into the saved output with README excluded;
   `watch --show-config` exiting without entering the loop; bare `specscribe --show-config`; a malformed
   `.specscribe` degrading to `settings_file=(none)` and running clean; discovery failure still exiting `1` with the
   message on stderr). The **interactive TTY** path — the menu loop and the new "Include the repository README?"
   confirm prompt — could **not** be exercised: both tool harnesses capture stdout, so Spectre always reports
   `Interactive == false`. Its logic is the same code the non-interactive path proves, but confirming the live
   prompt, its default, and the save round-trip is the owner-verification step. Same limit Story 5.1 recorded.

### File List

- `src/SpecScribe/SettingsResolver.cs` — **NEW.** `SettingsResolver` (`Load`/`Resolve`/`FormatConfigLines`/
  `DisplayTag`/`OriginToken`/`Fields`/`EscapeForLine` **[Review]**), `ConfigSource`, `ConfigProvenance`,
  `CliOverrides` (path/name predicates changed to `is not null` **[Review]**), `SettingsLoad`, `ResolvedConfig`.
- `src/SpecScribe/SettingsStore.cs` — `SavedSettings.IncludeReadme`; `IsEmpty` updated; new `FindExisting`
  (git-style walk-up) + `startDirectory` on `ResolvePath`/`TryLoad`/`TrySave`; `Capture` extracted from `TrySave`;
  `ApplyTo` honors the persisted README preference. **[Review, ADR 0014]** `.specscribe` is now a FOLDER containing
  `config.json` (`ConfigFileName`), not a flat file — `FindExisting`/`ReadConfigJson` support the folder form and a
  not-yet-migrated legacy flat file; `TrySave` migrates a legacy file to the folder form on write. New
  `TryLoad(startDirectory, out loadedFrom)` overload walks up exactly once, skips a malformed candidate and
  continues to the next ancestor instead of stopping, and reports the location that actually supplied the data.
- `src/SpecScribe/SiteSettings.cs` — new `--show-config` option; `Resolve(string? startDirectory = null)` seam.
- `src/SpecScribe/Commands.cs` — `generate`/`watch`/bare-default routed through `SettingsResolver` with the
  `--show-config` early return; menu loads once at entry and resolves per action from that load; `TryResolve` now
  returns `ResolvedConfig`; `ConfigurePaths` gained the README confirm prompt and re-bases the load after a save.
  **[Review]** `ConfigurePaths` now warns when `TrySave` fails for a real reason (not just "nothing to save").
- `src/SpecScribe/ConsoleUi.cs` — `PrintPaths` provenance overload + `Tag` helper; new `PrintResolvedConfig` and
  `PrintConfigDiagnostics`; `PrintSettingsLoaded` lists README/deep-git/code-URL. **[Review]**
  `PrintConfigDiagnostics` now guards `IOException` (closed downstream pipe), mirroring `PrintMachineSummary`.
- `src/SpecScribe/HowToReadTemplater.cs` — **[Review, ADR 0014]** generated copy updated: `.specscribe` **folder**,
  not *file*.
- `docs/adrs/0014-specscribe-settings-folder-format.md` — **NEW [Review].** Documents `.specscribe` becoming a
  folder containing `config.json`, extending ADR 0003; cross-linked from `docs/adrs/README.md`.
- `tests/SpecScribe.Tests/SettingsResolverTests.cs` — **NEW.** 18 tests: precedence, per-field provenance,
  README/deep-git restore, resolve-once, discovery propagation, `--show-config` shape (incl. a path with spaces).
  **[Review]** +4 tests: folder-form resolve smoke test, README-inclusion-true-no-override provenance leaf,
  newline-escaping in `FormatConfigLines`.
- `tests/SpecScribe.Tests/SettingsStoreTests.cs` — 12 new tests (walk-up discovery, write-back symmetry,
  `IncludeReadme` round-trip, backward compat, malformed JSON) + `IDisposable` temp-root fixture. **[Review, ADR
  0014]** +6 tests: folder-form read/write, legacy-file migration, malformed folder-form JSON, malformed-nearest-
  falls-back-to-valid-ancestor (×2, incl. reported path).
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — **out-of-scope fix, see Completion Note #7:** `BuildRow`
  normalizer `[^<]*` → `.*?`. Golden constant unchanged at the time; **regenerated since** by the concurrent
  Story 20.5 session on top of this story's rendering-visible change (the `.specscribe` "folder" copy edit) — see
  that file's provenance comment, not touched directly by this story.

## Change Log

- **2026-07-24 — Story 5.2 implemented (dev-story).** Added the `SettingsResolver` seam and routed `generate`,
  `watch`, the bare default command, and the interactive menu through it, closing the CLI-never-reads-`.specscribe`
  parity gap. Added three-way provenance (`CommandLine` > `SavedSettings` > `Default`) surfaced two ways: dim tags on
  the always-printed paths block, and a machine-parseable `--show-config` report that exits `0` without generating.
  Made `.specscribe` discovery a git-style walk-up with symmetric write-back, and closed the README-inclusion parity
  gap Story 5.1 deferred (`SavedSettings.IncludeReadme` + an interactive confirm prompt). 30 new tests; full suite
  **2215 passed / 0 failed / 3 skipped** (the 3 skips are pre-existing symlink-permission cases).
- **2026-07-24 — Out-of-scope fix, flagged for review:** repaired the `BuildRow` volatile-token normalizer in
  `SiteGeneratorAdapterTests` (`[^<]*` → `.*?`), which had been leaking the short commit SHA into the golden
  fingerprint and failing the gate on every commit. Golden constant **unchanged** — it now passes at two different
  commits, which is the proof. See Completion Note #7.
- **2026-07-25 — Code review (bmad-code-review).** 3-layer adversarial review (Blind Hunter, Edge Case Hunter,
  Acceptance Auditor) found all 4 ACs implemented correctly; 1 decision-needed, 7 patch, 10 dismissed. Owner resolved
  the decision-needed item by directing a bigger-than-patch change: **`.specscribe` becomes a folder containing
  `config.json`**, not a flat file, to leave room for future per-directory state (incremental-build caching, run
  history) — see new **ADR 0014**, which extends ADR 0003. Read is backward compatible with the pre-ADR-0014 flat
  file; write migrates it to the folder form in place. All 7 patch findings applied (IOException guard on
  `--show-config`'s output; `CliOverrides.Capture` predicate aligned with `ApplyTo`'s null-check semantics for the
  empty-string edge case; a real `TrySave` failure now warns the user instead of silently reporting stale
  provenance; `FormatConfigLines` escapes embedded newlines; the settings walk-up now happens exactly once per
  `SettingsResolver.Load` call and skips a malformed nearer candidate instead of shadowing a valid ancestor; added
  the missing README-inclusion-true provenance test). 10 new/changed tests. `SettingsStoreTests`/`SettingsResolverTests`
  **65 passed / 0 failed**. Full suite re-verified: golden content-fingerprint and golden output-inventory both pass
  (regenerated by the concurrent Story 20.5 session on top of this work); remaining full-suite failures (5–8,
  varying run to run) are the pre-existing git-fixture/concurrency flake this repo's other stories already document,
  none touching this story's files.

## Open Questions (for the maintainer — non-blocking; sensible defaults chosen)

1. **`.specscribe` location — cwd vs walk-up vs repo-root.** Default chosen: **git-style walk-up read** (find the nearest `.specscribe` at or above the start directory), writes anchored at the resolved root, so settings apply consistently from any subdirectory. The alternative (keep raw-cwd anchoring, current behavior) is simpler but silently misses settings when you run from a subfolder. I avoided anchoring `.specscribe` strictly at the discovered `_bmad-output` root because a saved `--source` could itself relocate that root (circular). Confirm walk-up is what you want, or say "keep cwd-only" to shrink the change.
2. **Provenance diagnostic: `--show-config` flag vs `config` subcommand vs always-on annotations only.** Default chosen: always-on dim provenance tags in `PrintPaths` **plus** a `--show-config` flag that prints a machine-parseable provenance line and exits `0`. If you'd rather this be a `specscribe config` subcommand (discoverable in `--help` alongside generate/watch), or want *only* the always-on annotations and no separate flag, say so — the resolver captures provenance either way, so the surface is a thin, swappable layer.
3. **Persist README preference as `bool? IncludeReadme`.** Default chosen: nullable so "never configured" stays distinct from "explicitly include." This means a user who never touches the README toggle keeps today's include-by-default behavior, and only an explicit interactive/CLI choice is persisted. Confirm you want the interactive "Configure paths" flow to prompt for it (adds one confirm step), or prefer README stay CLI-only (which would leave a known parity gap open).
