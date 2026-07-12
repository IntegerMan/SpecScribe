---
baseline_commit: 39f79da
---

# Story 6.12: Native Diagnostics — Problems Panel Integration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a VS Code user,
I want SpecScribe's per-artifact generation warnings to appear in the Problems panel on the offending files,
so that broken or unsupported artifacts surface where every other tool's errors live.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 6.12 (lines 1203–1215)._

1. **Given** the core emits per-artifact generation notices in a structured, core-owned format (JSON lines: path, message, severity — the same channel Story 4.8's diagnostics page consumes)
   **When** the extension receives them
   **Then** it maps each to a VS Code `Diagnostic` anchored to the offending artifact file, clearing them when a later run resolves the notice
   **And** this remains pure read-only data transport (no artifact is modified).

2. **Given** Story 4.8 owns the diagnostics format and page
   **When** this story is scheduled
   **Then** it consumes that format rather than defining a parallel one, and degrades cleanly (no diagnostics surfaced) when the core emits none
   **And** it stays coherent with the diagnostics page so the two never disagree.

## Scope Decision — READ FIRST (confirm or veto before dev)

This story seats **R8.3** from [docs/VSCodeIntegrationRecommendations.md:102](../../docs/VSCodeIntegrationRecommendations.md) (FR35): map the per-artifact generation notices the `specscribe webview` command already surfaces on stderr into the **VS Code Problems panel**, anchored to the offending artifact files. It is the "with their owning stories" wave item that rides **after Story 4.8** (which owns the notice derivation) — 4.8 is **done**, so the format it produced is ready to consume.

**It is two small changes, one on each side of the existing spawn seam:**
- **Core (C#):** replace the `specscribe webview` command's *human-readable* stderr line with a **structured JSON-lines** emission, derived from the **same** `DiagnosticNotice.FromEvents(...)` projection Story 4.8's diagnostics page renders (this is the coherence guarantee — AC #2).
- **Extension (TS):** capture those stderr lines during the spawn, parse them, and publish them into a `vscode.DiagnosticCollection` anchored to each offending file's `Uri`, clearing/rebuilding on every (re)load.

### What "the format 4.8 owns" actually is today — READ THIS
The epic text and R8.3 both say "the same channel Story 4.8's diagnostics page consumes." Concretely:
- Story 4.8 built the **page**, not a stderr wire format. Its canonical notice derivation is [`DiagnosticNotice.FromEvents(events)`](../../src/SpecScribe/DiagnosticsTemplater.cs) — filters the run's accumulated `List<GenerationEvent>` to the two non-fatal outcomes (`Error`/`Skipped`), recovers the fine ingest category, zero double-counting. **This is the "core-owned format" this story shares** — reuse it verbatim; do **not** re-filter `events` yourself or re-read `bundle.Diagnostics` (either would risk diverging from the page, breaking AC #2).
- The `webview` command's *current* stderr ([Commands.cs:60–64](../../src/SpecScribe/Commands.cs)) is a **Story 6.4 stopgap**: `Console.Error.WriteLine($"[specscribe webview] {error.RelativePath}: {error.Message}")`, and it only loops `Outcome == Error` — it **misses `Skipped`** (Unsupported/Skipped ingest notices) and is not machine-parseable. **This story replaces that stopgap** with the JSON-lines emission over the full `FromEvents` set. Nothing else consumes the old line (only the shim's failure-path `errText`), so replacing it is safe.

### The one real design decision — path anchorability (recommended default below)
A VS Code `Diagnostic` must be attached to a `Uri`. The notices split into two shapes:
- **Source-anchored (ingest) notices** — adapter diagnostics + unrecognized-folder notices, produced via [`SiteGenerator.MapDiagnostics`](../../src/SpecScribe/SiteGenerator.cs) (`GenerationEvent.FromAdapterDiagnostic == true`). Their `RelativePath` is **source-relative** (relative to `SourceRoot`) — a **real workspace file** (e.g. `sprint-status.yaml`, a malformed `.md`). **These are R8.3's target** ("the offending artifact files"), and they anchor cleanly.
- **Render-time notices** — `Error`/`Skipped` raised while rendering a page (e.g. `deep-analytics.html`). Their `RelativePath` is an **output-relative `.html`** — **not a source artifact**, so there is no artifact file to anchor to.

**Recommended default (build this unless the owner vetoes):** the core emits **every** non-fatal notice on the JSON-lines channel (so the wire stays a faithful mirror of `FromEvents` — coherence), each carrying a `fileAnchored` boolean. The shim publishes **only the `fileAnchored` notices** into the Problems panel (that IS "on the offending artifact files"); render-time non-anchored notices are **not** surfaced in Problems — they remain visible on the diagnostics page, which is their home. This is a *documented scoping*, not a disagreement: both surfaces derive from the identical `FromEvents` set and neither shows contradictory data (AC #2 "never disagree" is about contradiction, not about Problems being a strict subset). If the owner instead wants zero notices dropped from Problems, the fallback is to publish non-anchored notices on a single workspace-level `Uri` — noted in Task 4, not the default.

**IN scope**
- **Core:** a pure, spawn-free projection `WebviewCommand.SerializeDiagnostics(...)` that turns the run's `DiagnosticNotice` list + resolved options into the **JSON-lines** stderr payload (one JSON object per line: `path`, `severity`, `message`, `fileAnchored`), and the `Execute` wiring that replaces the current human stderr loop with it.
- **Core:** surface the source-anchorability signal on the shared notice type — additive `SourceAnchored` on `DiagnosticNotice` (set from the event's `FromAdapterDiagnostic`), so both the page and this channel read one type. (4.8's page ignores the new field; its rendering is byte-unchanged.)
- **Extension:** a module-scoped `vscode.DiagnosticCollection` (source `"SpecScribe"`); stderr capture + per-line parse in `runRenderer`; publish/clear on each store load settle, grouped by `Uri`, anchored `fileAnchored` notices only (recommended default).
- **Read-only, byte-parity, degrade-clean:** no artifact writes; the generated HTML surface is untouched (golden fingerprint unaffected — the change is stderr + TS only); an empty notice set clears the collection (no Problems noise).

**OUT of scope (do NOT start here)**
- **Changing what notices exist or how they're collected.** This story renders `FromEvents` as-is. It does not add diagnostic categories, add emission sites, or touch `MapDiagnostics`/`AdapterDiagnostic`/the `--deep-git` path. The one allowed core touch beyond the command is the additive `SourceAnchored` field.
- **The diagnostics *page* (4.8) or the About page.** Untouched.
- **Line/column precision.** Notices carry no source position; anchor to the top of the file (`Range(0,0,0,0)`). Do not parse markdown to find a line (that would reopen AD-2). This matches R4.4's "don't build content-aware position maps" ruling.
- **A new command, menu, view, or setting.** Diagnostics render in the native Problems panel with no manifest contribution. `extension/package.json` is **not** modified by this story.
- **Watch/reactivity, multi-root, non-`.md` watch gap.** Those are Stories 6.10/6.11. Diagnostics refresh whenever the store reloads (which today happens via the existing watchers + manual Refresh) — that is sufficient; do not add new watch triggers here.
- **Structured code-citation links / reveal-source.** Stories 6.10/7.x/8.4. Unrelated seam.

**One-line test for "is this in scope?":** if it moves the run's already-collected non-fatal notices onto stderr as JSON and into the Problems panel, it's in. If it *produces* new notices, changes *how* they're collected, adds UI chrome, or invents position info, it's out.

## Tasks / Subtasks

- [ ] **Task 1 — Surface source-anchorability on the shared notice type** (AC: #1, #2)
  - [ ] Add an additive `bool SourceAnchored` to the [`DiagnosticNotice`](../../src/SpecScribe/DiagnosticsTemplater.cs) record (constructor param + XML-doc, tagged `[Story 6.12]`). In `DiagnosticNotice.FromEvents`, set it from `e.FromAdapterDiagnostic` — the exact provenance bit that already distinguishes source-relative ingest notices (`MapDiagnostics`) from output-relative render-time notices. Keep it a **single shared type** so the page and the stderr channel can never derive different notice sets (AC #2 coherence).
  - [ ] **Do not** change `SplitCategory`, the filter, or the ordering. The page (4.8) does not read `SourceAnchored`; confirm `DiagnosticsTemplaterTests` and the golden inventory still pass unchanged (this field is invisible to rendering).

- [ ] **Task 2 — Pure JSON-lines projection for the machine channel** (AC: #1, #2)
  - [ ] Add `public static string SerializeDiagnostics(IReadOnlyList<DiagnosticNotice> notices, ForgeOptions resolved)` to [`WebviewCommand`](../../src/SpecScribe/Commands.cs) — pure and spawn-free, mirroring how `SerializePayload`/`ResolveConfiguredOutputRoot` are extracted for unit-testability. Return the full stderr text: **one JSON object per line** (newline-terminated), or `""` (empty) when there are no notices. Use the same `CamelCase` `JsonSerializerOptions` the payload uses.
  - [ ] Each line object carries exactly: `path` (string, **repo-relative, forward-slashed** — see below), `severity` (`"error"` | `"warning"`, from `DiagnosticNotice.Severity`), `message` (string; when `Message` is null, fall back to the `Category` word so a bare skip still reads), and `fileAnchored` (bool, from `SourceAnchored`).
  - [ ] **Path resolution (the crux):**
    - For a **source-anchored** notice (`SourceAnchored == true`): `path = Path.GetRelativePath(resolved.RepoRoot, Path.Combine(resolved.SourceRoot, notice.SourcePath)).Replace('\\','/')` — a repo-relative path the shim joins to the workspace folder to build a real file `Uri` (same repo-relative, forward-slashed convention as `ResolveConfiguredOutputRoot` and 4.8's `DiagnosticsConfig`).
    - For a **non-anchored** render-time notice: `path = notice.SourcePath.Replace('\\','/')` verbatim (the output-relative `.html`) with `fileAnchored = false`. It rides the wire (page coherence) but the shim won't anchor it (Task 4 default).
  - [ ] **Use the PRE-redirect `resolved` options** for `RepoRoot`/`SourceRoot`, not the scratch-redirected `options` — the anchored path must point at the project's real source, exactly as `configuredOutputRoot` already sources from `resolved` ([Commands.cs:68](../../src/SpecScribe/Commands.cs)). (`RedirectOutputToScratch` only moves `OutputRoot`, so the two roots happen to match today, but read from `resolved` for correctness and to survive a future change.)
  - [ ] Message content: prefer the notice's full `Message` (which for ingest notices already carries the `[Category]`-stripped detail from `FromEvents`); prefix it with the category for a self-describing Problems entry, e.g. `notice.Message is null ? notice.Category : $"{notice.Category}: {notice.Message}"`. Keep it one line (Problems shows a single line); do not linkify (raw text — same trap 4.8 documents).

- [ ] **Task 3 — Wire the projection into `WebviewCommand.Execute`** (AC: #1, #2)
  - [ ] Replace the current human stderr loop ([Commands.cs:60–64](../../src/SpecScribe/Commands.cs)) with: `var notices = DiagnosticNotice.FromEvents(events); Console.Error.Write(SerializeDiagnostics(notices, resolved));`. This covers `Error` **and** `Skipped` (the stopgap missed `Skipped`) and emits nothing when the run is clean (degrade-clean, AC #2).
  - [ ] `DiagnosticNotice`/`DiagnosticSeverity` live in `SpecScribe` namespace already (`DiagnosticsTemplater.cs`) — no new using. stdout still carries **only** the JSON payload (unchanged); diagnostics stay on stderr (never intermix — stdout parse must not break).
  - [ ] Update the command's XML-doc note about stderr (currently "diagnostics go to stderr") to state the structured JSON-lines contract and tag the change `[Story 6.12]`.

- [ ] **Task 4 — Extension: capture stderr, publish Diagnostics** (AC: #1, #2)
  - [ ] Create ONE module-scoped `vscode.DiagnosticCollection` via `vscode.languages.createDiagnosticCollection('SpecScribe')` in `activate` (push to `context.subscriptions` so it disposes; also null it in `deactivate`). Name the source `"SpecScribe"` so Problems entries read as ours.
  - [ ] In [`runRenderer`](../../extension/src/extension.ts): the spawn already accumulates `errText` (UTF-8 stream). On success (`close`, `code === 0`), parse `errText`: split on newlines, `JSON.parse` each non-empty line inside a try/catch, **skip lines that don't parse** (backward/forward-compat: an older core's human line, or a future field, must never throw). Collect the valid records and return them alongside the payload — change `runRenderer`'s resolve to `{ payload, diagnostics }` (define a small `interface RawDiagnostic { path: string; severity: string; message: string; fileAnchored?: boolean }`), and thread the diagnostics through `SpecScribeStore.load()`.
  - [ ] On each **successful** store settle, rebuild the collection: `collection.clear()`, then group `fileAnchored` records by `Uri.file(path.join(folder.uri.fsPath, record.path))`, build a `vscode.Diagnostic(new vscode.Range(0,0,0,0), message, sev)` per record (`sev`: `'error' → vscode.DiagnosticSeverity.Error`, else `Warning`; set `.source = 'SpecScribe'`), and `collection.set(uri, diags)` per file. This clears resolved notices (AC #1 "clearing them when a later run resolves"). On a **failed** load, leave the collection as-is (last-good), mirroring the tree/status-bar stale behavior — do not clear stale diagnostics on a transient spawn failure.
  - [ ] Non-anchored records: **skip** (recommended default — they live on the diagnostics page). Leave a one-line comment marking the deliberate scoping and the fallback (publish on a workspace-folder `Uri`) if the owner later wants them surfaced.
  - [ ] **Failure-path hygiene:** on `code !== 0` the error toast uses `errText`; now that stderr may contain JSON lines, keep the existing behavior but it's fine — a non-zero exit is a real crash whose stderr is a .NET stack trace, not our notice lines (notices are non-fatal, exit 0). No change needed beyond a comment noting this.
  - [ ] Keep the shim's rules intact: this is **pure data transport** — no artifact write, no markdown parse, no project knowledge. The core decides *what* the notice says and *which* file; the shim only decides *that VS Code shows it in Problems* (constraint #1). Read-only end to end (AD-6).

- [ ] **Task 5 — Tests** (AC: #1, #2)
  - [ ] **C# (`tests/SpecScribe.Tests/`, xUnit, `net10.0`, file-per-unit):** extend [`WebviewCommandTests`](../../tests/SpecScribe.Tests/WebviewCommandTests.cs) (or add `WebviewDiagnosticsTests`) covering `SerializeDiagnostics` purely (no spawn):
    - a source-anchored notice → one JSON line with `fileAnchored: true`, a **repo-relative forward-slashed** `path` (e.g. `_bmad-output/…`), correct `severity`, and a message carrying the category;
    - a render-time (non-anchored) notice → `fileAnchored: false` with the output-relative `.html` path verbatim;
    - `Error` → `"error"`, `Skipped`/`Unsupported` → `"warning"` severity mapping;
    - empty notice list → empty string (degrade-clean);
    - a null-`Message` notice → message falls back to the category word (no crash, no `null` in JSON).
  - [ ] **Coherence test (AC #2):** a `SiteGenerator`-level test reusing 4.1's malformed-artifact fixture (see [4-8 Task 7](4-8-generation-diagnostics-and-configuration-log-page.md) / `SiteGeneratorAdapterTests`) — assert the notice set feeding `SerializeDiagnostics` is exactly `DiagnosticNotice.FromEvents(events)` (same count, same paths, no double-count), i.e. the wire mirrors the page's source. This is the "two never disagree" guardrail.
  - [ ] **TS:** no unit harness exists in `extension/` (build/typecheck/package scripts only, per Story 6.9). Verify `npm run typecheck` and `npm run build` (esbuild) are clean. The shim's Diagnostic mapping is covered by the manual F5 smoke (Task 6).
  - [ ] Full `dotnet test` green. The **only** assertions that may change are the new ones — no existing 4.8 page test or golden fixture should move (the `SourceAnchored` field is render-invisible and the HTML surface is untouched). If a golden/inventory assertion changes, you altered rendering — stop and reconsider.

- [ ] **Task 6 — Verify end-to-end** (AC: #1, #2)
  - [ ] `dotnet test` — whole suite green.
  - [ ] Run `dotnet run --project src/SpecScribe -- webview 2> stderr.txt` in a repo that emits a known ingest notice (introduce a temporary malformed artifact, or use a fixture repo — this repo currently renders **all-clear**, per 4.8's completion note that `sprint-status.yaml` now parses cleanly). Confirm `stderr.txt` contains one JSON object per notice with `path`/`severity`/`message`/`fileAnchored`, and stdout is still valid JSON (the payload) — the two streams never intermix.
  - [ ] `npm run typecheck && npm run build` in `extension/` — clean.
  - [ ] **Manual F5 smoke** (the standing Epic 6 shim step — record it on the extension README checklist alongside the 6.8/6.9 smokes): open a repo containing a malformed/unsupported artifact in the Extension Development Host; confirm a Problems-panel entry appears **on that file** with SpecScribe as the source and the right severity; fix the artifact (or Refresh after resolving) and confirm the entry **clears**; open a clean repo and confirm **no** SpecScribe Problems noise.
  - [ ] Record in Completion Notes: the exact stderr line shape emitted, the anchored-vs-skipped decision as built, the severity mapping, and confirmation that the generated HTML surface (golden fingerprint) is unchanged.

## Dev Notes

### The notice set is already derived — this is a *transport* story
Everything AC #1 needs already exists at the end of `GenerateAll`:
- **The notices**: `DiagnosticNotice.FromEvents(events)` — 4.8's canonical, deduped, category-recovered projection off the single accumulated `List<GenerationEvent>`. **Reuse it**; do not re-filter `events` or re-read `bundle.Diagnostics` (double-count / divergence risk).
- **The paths**: `DiagnosticNotice.SourcePath` (source-relative for ingest, output-relative for render-time) + `resolved.RepoRoot`/`resolved.SourceRoot` for the repo-relative resolve. All pure string math (`Path.Combine`/`Path.GetRelativePath`) — no I/O, no remote calls (NFR3, local-first).
- **The provenance**: `GenerationEvent.FromAdapterDiagnostic`, already set by `MapDiagnostics`, is the exact "is this a real source artifact?" bit — surface it on `DiagnosticNotice` (Task 1) rather than re-deriving.

### Why the stderr channel (not stdout, not the payload)
stdout carries the machine JSON **payload** the shim parses as the webview bundle ([Commands.cs:68](../../src/SpecScribe/Commands.cs)); any extra bytes there corrupt `JSON.parse(out)`. Diagnostics belong on **stderr** — R8.3's explicit choice, and the stream the shim already captures (`errText`). Keeping notices on stderr means the payload contract (Story 6.4/6.8/6.9) is untouched; this story adds a *second, independent* read of a stream the shim already buffers.

### Severity mapping here is CORRECT — not a constraint-#5 violation (pre-empt the reviewer)
Constraint #5 (the six `--status-*` stages, [docs/VSCodeIntegrationRecommendations.md:32](../../docs/VSCodeIntegrationRecommendations.md)) forbids collapsing **status** surfaces (tree icons, status bar — Story 6.9) onto VS Code's 3-severity palette. **The Problems panel is a different domain**: it is *literally* VS Code's error/warning surface, and a generation *notice* is an error/warning, not a lifecycle stage. Mapping `DiagnosticSeverity.Error/Warning → vscode.DiagnosticSeverity.Error/Warning` is the native-correct thing and does **not** touch the `--status-*` vocabulary. Story 4.8's own `DiagnosticSeverity` doc says the same: "deliberately distinct from the lifecycle `--status-*` stage vocabulary." Call this out in the code comment so a review doesn't misfire.

### Anchoring precision — top of file, honestly
Notices have no line/column. Anchor to `new vscode.Range(0,0,0,0)` (file top). Do **not** synthesize a line by parsing the artifact — that reopens AD-2 (the shim parses no markdown) and is exactly the content-aware-position work R4.4 recommends *against*. A file-level "this artifact has a problem" is the honest and sufficient signal.

### Read-only / byte-parity (inherit the Epic 6 invariants)
- **Read-only (AD-6, ADR 0003):** publishing Diagnostics is a host-UI effect; nothing writes a project artifact or mutates settings. Constraint #2 holds trivially.
- **Byte parity (constraint #4):** the generated site is untouched — the only core change is the `webview` command's stderr text (not part of any golden fixture) plus a render-invisible `DiagnosticNotice` field. The golden fingerprint (`GoldenContentFingerprint`, memory: golden-diff-normalization-gotchas) must **not** move. If it does, something rendered differently — investigate before proceeding.

### Backward / forward compatibility
- **Old core + new shim:** an older `specscribe` emits the human `[specscribe webview] …` stderr line. The shim's per-line `JSON.parse` in a try/catch **skips** it — no diagnostics, no crash. Graceful.
- **New core + old shim:** the pre-6.12 shim reads stderr only on failure; the JSON lines on a success exit are ignored. Harmless.
- Keep the shim tolerant: unknown fields on a record are ignored; a record missing `path`/`severity` is skipped (defensive parse), so a future core field never breaks an older shim.

### Where the pieces live (mirror the established patterns)
- **Core projection** → `WebviewCommand.SerializeDiagnostics` next to `SerializePayload`/`ResolveConfiguredOutputRoot` ([Commands.cs](../../src/SpecScribe/Commands.cs)) — same "pure static, unit-testable without a spawn" shape. The `RawDiagnostic` wire record can be an anonymous object per line (as `SerializePayload` does) — no new public type needed core-side beyond the `SourceAnchored` field.
- **Shim** → all in [extension/src/extension.ts](../../extension/src/extension.ts) (the whole TS surface, per ADR 0005): the `DiagnosticCollection` at module scope beside `statusBar`/`treeProvider`; the parse in `runRenderer`; the publish in `SpecScribeStore.load()`'s success branch (the natural "on every settle" seam that already drives the tree + status bar via `dataChanged`).
- **Tests** → `tests/SpecScribe.Tests/` (extend `WebviewCommandTests` or add `WebviewDiagnosticsTests`; add the coherence assertion near `SiteGeneratorAdapterTests`).

### Do NOT
- Re-read `bundle.Diagnostics` or re-filter `events` for the stderr set — use `DiagnosticNotice.FromEvents` (single source, no double-count; 4.1/4.8's explicit design).
- Add a command, menu, view container, color, or setting — `extension/package.json` is untouched. Problems is native.
- Parse markdown for a line number, or add any project knowledge to the shim (AD-1/AD-2/R4.4).
- Touch the diagnostics *page*, the About page, `MapDiagnostics`, `AdapterDiagnostic`, or the `--deep-git` path.
- Put diagnostics on stdout, or let any non-payload byte reach stdout.
- Add new file watchers or reactivity (Stories 6.10/6.11). Diagnostics rebuild on the existing store reloads.

### Project Structure Notes
- Single project: `src/SpecScribe/SpecScribe.csproj` (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). Changed files: `Commands.cs`, `DiagnosticsTemplater.cs` (additive field). **No new project, no namespace split** — `namespace SpecScribe;`.
- Extension: `extension/src/extension.ts` only (thin shim, ADR 0005). `extension/package.json` **unchanged**.
- Match the heavy XML-doc house style — every public/changed member carries a `<summary>` explaining the *why*, tagged `[Story 6.12]`.
- Runs on `main` (not a worktree); edits target `C:\Dev\SpecScribe` directly. A background auto-committer on main commits/pushes edits (memory: worktree-edits-must-target-worktree-path) — keep commits coherent.
- **Not gated** as a hard dependency: Story 4.8 (format owner) is **done**; Stories 6.8/6.9 (the shim's command/store scaffolding this rides on) are done/review. This is the last of the Stories 6.8–6.12 native-integration wave; it lands before the Epic 17 hardening pass.

### References
- [epics.md:1192–1215](../planning-artifacts/epics.md) — Story 6.12 ACs + the R8.3 provenance comment (source of truth).
- [epics.md:1065–1073](../planning-artifacts/epics.md) — the Stories 6.8–6.12 wave constraints (rendering in C#, read-only shim, byte parity, six `--status-*` stages).
- [docs/VSCodeIntegrationRecommendations.md:102](../../docs/VSCodeIntegrationRecommendations.md) — **R8.3** (the recommendation this story seats); [:26–32](../../docs/VSCodeIntegrationRecommendations.md) §2 constraints (esp. #1 "core decides what, TS decides where" and #5 status-vocabulary — clarified above as not applying to Problems severity).
- [4-8-generation-diagnostics-and-configuration-log-page.md](4-8-generation-diagnostics-and-configuration-log-page.md) — the format owner (**done**): the `DiagnosticNotice`/`FromEvents` projection, the `[Category]` message enrichment, "one typed channel, never double-counted," the malformed-artifact test fixture.
- [src/SpecScribe/DiagnosticsTemplater.cs:10–73](../../src/SpecScribe/DiagnosticsTemplater.cs) — `DiagnosticSeverity`, `DiagnosticNotice` (+ `FromEvents`, `SplitCategory`) — the type to add `SourceAnchored` to and reuse.
- [src/SpecScribe/Commands.cs:35–128](../../src/SpecScribe/Commands.cs) — `WebviewCommand` (the stderr loop to replace at :60–64; `SerializePayload`/`ResolveConfiguredOutputRoot` as the pure-method pattern; `resolved` vs scratch `options` at :53–56, :68).
- [src/SpecScribe/SiteGenerator.cs:8–14](../../src/SpecScribe/SiteGenerator.cs) — `GenerationOutcome`, `GenerationEvent` (+ `FromAdapterDiagnostic`); [:1118–1123](../../src/SpecScribe/SiteGenerator.cs) — `MapDiagnostics` (source-relative path, `FromAdapterDiagnostic: true`).
- [src/SpecScribe/AdapterDiagnostic.cs:33–36](../../src/SpecScribe/AdapterDiagnostic.cs) — `RelativePath` is "relative to the source root" (why ingest notices anchor to a real file).
- [extension/src/extension.ts:756–791](../../extension/src/extension.ts) — `runRenderer` (stderr capture + close handler to extend); [:369–427](../../extension/src/extension.ts) — `SpecScribeStore.load()` (the on-settle seam to publish from); [:133–174, :806–810](../../extension/src/extension.ts) — `activate`/`deactivate` (where to create/dispose the collection).
- [tests/SpecScribe.Tests/WebviewCommandTests.cs](../../tests/SpecScribe.Tests/WebviewCommandTests.cs) — the pure-projection test shape to extend.
- Memory: golden-diff-normalization-gotchas (fingerprint stays fixed), specscribe-status-token-system (status tokens are NOT the Problems severity domain), story-6-4-webview-runtime-live / story-6-9-native-outline-tree-live (the shim spawn/store patterns this rides on).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
