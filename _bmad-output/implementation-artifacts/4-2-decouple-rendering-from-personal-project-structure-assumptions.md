---
baseline_commit: 04aba3246b2906b80e06182263944f50cef42e53
---

# Story 4.2: Decouple Rendering from Personal Project-Structure Assumptions

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer of a BMad project that is organized differently from the tool author's own repositories,
I want generation to avoid hardcoded personal-structure assumptions,
so that my ADRs, folders, and groupings render correctly without matching one specific layout.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 4.2 (lines 646–664)._

1. **Given** a BMad project whose ADRs, folder names, or artifact groupings differ from this repository's personal conventions
   **When** the site is generated
   **Then** rendering adapts to the detected structure rather than depending on fixed personal assumptions (ADR location/format, hardcoded group-prefix names, specific filenames)
   **And** unrecognized structure degrades gracefully rather than mis-grouping or dropping content.

2. **Given** ADRs authored in non-standard formats or locations
   **When** they are parsed
   **Then** recognized decision records still render with title, status, and links where derivable
   **And** format and organization variance is handled tolerantly (non-fatal), without assuming a single numbering or directory scheme.

## Scope Decision — READ FIRST (confirm or veto before dev)

This story is the "first generalize the renderer away from any single project's personal structure" clause of the Epic 4 goal ([epics.md:622](../planning-artifacts/epics.md)). Story **4.1 builds the ingestion seam and wraps today's hardcoded discovery verbatim; 4.2 generalizes what's *inside* that wrapper** (memory: epic-4-adapter-contract-scope).

**Why this story exists (product origin).** SpecScribe began as a tool tailored to one specific BMad **GDS** project, then became a viable OSS product meant to serve the broader spec-driven-development community across many frameworks. Framework-agnosticism is the throughline of **all** of Epic 4 — 4.2's specific job is to **strip the personal/GDS-specific assumptions out of *shared* rendering** so that shared code is framework-neutral, and per-framework specifics live in adapters. It is not "add a second framework" (that's 4.3–4.7, each a concrete adapter) and it is not "make BMad-only code merely tolerant of BMad layout variance." It is: **de-personalize shared rendering so it privileges no single project's — or single framework's — conventions.** Getting this posture right matters because 4.3–4.7 build concrete adapters on top of whatever framework-neutral substrate this story establishes. **If you disagree with the scope below, raise it before writing code, not at review** (Epic 3 retro action item: don't defer a defining decision to the dev and correct it later).

### Sequencing dependency on 4.1 (READ)
Story **4.1 is in `review`** (effectively landed), so the `BmadArtifactAdapter` boundary this story builds on should exist — **read 4.1's final `File List` and Completion Notes first** and confirm where the ingestion seam and the `AdapterDiagnostic` channel actually landed before you start. 4.2's generalizations attach to that boundary:
- ADR + home-index grouping are **rendering/projection** concerns that 4.1 leaves untouched — 4.2 owns them regardless.
- The `epics.md` / `implementation-artifacts/` filename literals live in the discovery 4.1 moved behind `BmadArtifactAdapter`. Generalize them **inside that adapter** (one home for BMad's conventions) — do not fork a parallel discovery path. If 4.1's review sends it back and the seam shifts, reconcile before finalizing and note it in Completion Notes.

### The governing principle: detect-and-degrade, not configure-everything
The recommended posture (matches the codebase's existing `ModuleContext.Detect` pattern — data-driven detection with a graceful `None` fallback, [ModuleContext.cs:120–175](../../src/SpecScribe/ModuleContext.cs)): **probe a small ordered set of conventional variants, take the first that matches, and when nothing matches degrade to absent/flat rather than mis-grouped or dropped.** Keep the existing explicit overrides (`--adrs`, `--source`) as the escape hatch. Do **NOT** build a new user-facing structure-config schema (path-mapping settings, custom group definitions) — that is over-engineering for this story and risks the "structural growth mid-story → should have been its own story" trap (Epic 2 retro). Tolerance via detection, not configuration.

**IN scope**
- **ADR location tolerance (AC #2)** — beyond the single `docs/adrs/` default: probe an ordered fallback list of conventional ADR homes and recurse one level so nested schemes are found; `--adrs` still overrides; absent everywhere ⇒ no ADR section (as today).
- **ADR format tolerance (AC #2)** — derive the number from multiple filename schemes and the status from multiple markers; where a field isn't derivable, still render the record (title + link always; number/status optional).
- **Home-index grouping tolerance (AC #1)** — replace the three hardcoded folder-prefix group literals ([HtmlTemplater.cs:95–105](../../src/SpecScribe/HtmlTemplater.cs)) with structure-derived grouping: well-known BMad folders keep their friendly titles + PRD-prominent planning treatment; an unrecognized top-level folder degrades to a coherently-titled band, never a silent dump into "Other" and never mis-grouping.
- **Consolidate the scattered filename literals (AC #1)** — the `"epics.md"` and `"implementation-artifacts"` string literals independently hard-coded across ≥5 files become one shared source of truth (inside `BmadArtifactAdapter`), and discovery becomes location-tolerant (filename/family-anywhere-in-tree, consistent with how `SiteNav` already finds `epics.md`) so a project that nests these folders differently doesn't lose content.

**OUT of scope (later story — do NOT start it here)**
- **New-framework adapters / alternate epics filenames** (Spec Kit's `spec.md`, GSD's files, etc.) — 4.3–4.7. 4.2 does not invent non-BMad artifact names; it removes *scattered* BMad literals and makes discovery layout-tolerant. Keep `epics.md` the BMad primary.
- **The ingestion adapter contract itself** — Story 4.1. 4.2 consumes it.
- **Status-vocabulary mapping** — native→canonical `Status` mapping is downstream in `StatusStyles` and is Story 8.1's domain (memory: epic-4-adapter-contract-scope). 4.2 does not touch status classification; an ADR whose status string is non-standard renders that string as-is.
- **Renderer→HTML/webview view-model contract** — Story 6.1 / AD-2.
- **Package/namespace split** — seed, not invariant ([ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). Stay in the single `SpecScribe` project.
- **A user-facing structure-config surface** — deliberately excluded (see governing principle).
- **A persisted diagnostics / configuration-log page** — spun out to **Story 4.8** ([epics.md](../planning-artifacts/epics.md)). 4.2 must **emit** its degradation notices through the diagnostic channel (Task 5) so 4.8 can render them, but 4.2 does **not** build the page. Do not add a new output page here (Epic 2 retro: don't absorb a new page type mid-story).

**One-line test for "is this in scope?":** if it removes a personal / single-framework assumption from *shared* rendering so any repo renders coherently regardless of its layout, it's in. If it adds a *specific new framework's* artifact parsing (4.3–4.7), or a *new configuration surface*, it's out.

### The non-negotiable guardrail: this repo's output must not change
This repository already uses the canonical layout (`docs/adrs/`, `epics.md`, `planning-artifacts/`, `implementation-artifacts/`, `specs/`). **Every generalization must keep the default/first-probed branch identical to today, so generating THIS repo's own site produces byte-for-byte identical output.** Reuse 4.1's golden-output regression as the primary guardrail (Task 6). New tolerance is proven by *new fixtures with non-standard layouts*, never by changing an existing assertion.

## Tasks / Subtasks

- [ ] **Task 1 — Generalize ADR location detection** (AC: #1, #2)
  - [ ] Today ADRs come only from `ForgeOptions.AdrSourceRoot`, defaulted to `repoRoot/docs/adrs` ([ForgeOptions.cs:100](../../src/SpecScribe/ForgeOptions.cs)) and enumerated top-level-only ([SiteGenerator.cs:914–916](../../src/SpecScribe/SiteGenerator.cs)). When `--adrs` is **not** explicit and the default dir is absent, probe an ordered fallback list of conventional homes and take the first non-empty match: e.g. `docs/adr`, `docs/adrs`, `docs/decisions`, `docs/architecture/decisions`, `docs/architecture/adr`, `adr`, `adrs`. Explicit `--adrs` always wins and never triggers the probe; absent everywhere ⇒ no ADR section (unchanged behavior).
  - [ ] Recurse one level under the resolved ADR root when enumerating (`SearchOption.AllDirectories` bounded, or a shallow walk) so a `decisions/2024/0007-x.md` nesting is still found — but keep README-as-landing and the wipe-and-rebuild of `SpecScribeOutput/adrs` semantics intact ([SiteGenerator.cs:329–387](../../src/SpecScribe/SiteGenerator.cs)).
  - [ ] Preserve `AdrSourceExplicit` warning semantics ([ForgeOptions.cs:15–17](../../src/SpecScribe/ForgeOptions.cs)): an explicit-but-missing `--adrs` still warns; a probe that finds nothing is silent (ADRs are optional).
  - [ ] **This repo has `docs/adrs/`, which is the first/default branch — output must not change.**

- [ ] **Task 2 — Generalize ADR number + status parsing** (AC: #2)
  - [ ] **Number:** widen `AdrNumberPattern` ([SiteGenerator.cs:17](../../src/SpecScribe/SiteGenerator.cs), `ParseAdrNumber` at [:1085–1089](../../src/SpecScribe/SiteGenerator.cs)) to derive a leading integer across schemes: `0001-title.md`, `ADR-0001-title.md`, `adr-1-title.md`, `adr_001_title.md`, `1-title.md`. Any optional `adr`/`ADR` token + separators, then the first integer. **Not derivable ⇒ the record still renders** as a page and a card; unnumbered ADRs sort after numbered ones, then by title (update the `_adrs.OrderBy(e => e.Number)` at [:385](../../src/SpecScribe/SiteGenerator.cs) to a null-safe composite sort). Today an unnumbered file is *dropped from the card list* ([:372](../../src/SpecScribe/SiteGenerator.cs) `number is not null` gate) — AC #2 requires it render with what's derivable.
  - [ ] **Status:** `ExtractAdrStatus` ([SiteGenerator.cs:1093–1099](../../src/SpecScribe/SiteGenerator.cs)) currently matches only a `**Status:** …` bold line. Add tolerant derivation, first match wins: (a) existing `**Status:**` line, (b) a `## Status` / `### Status` heading whose next non-blank line is the value (MADR convention), (c) a `status:` YAML frontmatter key (reuse `Frontmatter` if it already exposes it — check [Frontmatter.cs](../../src/SpecScribe/Frontmatter.cs) before adding). Flatten any markdown link to plain text as today. **Not derivable ⇒ render with no status badge** (title + link still present).
  - [ ] `AdrsExist()` ([SiteGenerator.cs:1083](../../src/SpecScribe/SiteGenerator.cs)) currently requires a numbered file — decide and document: an ADR dir with only non-standard-named records should still surface the ADR nav/section (AC #2 "recognized decision records still render"). Change the gate to "any renderable record present," not "any numbered file."
  - [ ] Update the `AdrModel`/`AdrEntry` doc comment ([AdrModel.cs:3–5](../../src/SpecScribe/AdrModel.cs)) — it currently *asserts* `docs/adrs/` and the `**Status:**`/filename-prefix conventions as fixed. Rewrite to describe the tolerant behavior (title always; number/status where derivable).

- [ ] **Task 3 — Structure-derived home-index grouping** (AC: #1)
  - [ ] **Approach (DECIDED — Option B):** keep the ordered **well-known** group list with its friendly titles, order (Overview → Planning → Spec Kernel → Implementation), and the PRD-prominent planning treatment (`AppendPlanningSection`) exactly as today — then **append** any *unrecognized* top-level source folders as their own bands after the known groups (in place of the current catch-all "Other" dump). Do NOT re-derive the whole group set from scratch from the folders present; extend the known list. This keeps the known-folder output byte-identical and only adds behavior for unknown folders. `HtmlTemplater.RenderIndex` currently hard-codes the three prefixes at [HtmlTemplater.cs:95–105](../../src/SpecScribe/HtmlTemplater.cs) + the special `planning-artifacts` → `AppendPlanningSection` branch at [:152](../../src/SpecScribe/HtmlTemplater.cs).
  - [ ] **Degradation contract:** a doc under an *unrecognized* top-level folder lands in its own band titled by the **humanized folder name** (e.g. `design-notes/` → "Design Notes"), NOT silently dumped into "Other" and NEVER mis-grouped under a BMad title it doesn't belong to. Replace the trailing generic "Other" grid ([:167–176](../../src/SpecScribe/HtmlTemplater.cs)) with one-band-per-unknown-folder; a doc at the repo root (no folder) keeps flowing through the existing `groupPrefix == ""` Overview band ([:140–142](../../src/SpecScribe/HtmlTemplater.cs)). Since this repo has no unknown top-level folders, the rendered index is unchanged.
  - [ ] The `WellKnownDocs` filename constants ([ModuleContext.cs:76–99](../../src/SpecScribe/ModuleContext.cs)) already match "anywhere in the tree" — keep the PRD/UX/brief classification filename-based (not folder-based), so it survives a project that puts the PRD outside `planning-artifacts/`. Note the comment at [ModuleContext.cs:71–75](../../src/SpecScribe/ModuleContext.cs) explicitly says "folder layout varies; Epic 4 will generalize" — this task is that generalization for grouping.
  - [ ] **This repo's folders are the well-known set — the rendered index must not change.**

- [ ] **Task 4 — Consolidate scattered filename literals into one BMad source of truth** (AC: #1)
  - [ ] The strings `"epics.md"` and `"implementation-artifacts"` are independently hard-coded in: [SiteNav.cs:104](../../src/SpecScribe/SiteNav.cs), [SiteGenerator.cs:247,253,749,1102,1110](../../src/SpecScribe/SiteGenerator.cs), [WorkInventory.cs:37](../../src/SpecScribe/WorkInventory.cs). Promote them to shared constants owned by `BmadArtifactAdapter` (Story 4.1) — or, if 4.1 hasn't landed, a single `BmadConventions` static holding them — so BMad's naming lives in exactly one place. No behavior change; this is the "remove the scattered personal literals" half of AC #1.
  - [ ] Make family discovery **location-tolerant, not folder-depth-fixed:** `SiteNav` already finds `epics.md` by filename anywhere ([SiteNav.cs:104](../../src/SpecScribe/SiteNav.cs)); align the stragglers that assume a fixed parent-dir name — `IsBmadArtifact`'s `parentDir == "implementation-artifacts"` ([SiteGenerator.cs:242–253](../../src/SpecScribe/SiteGenerator.cs)), `FindEpicsFile` / the story parent check ([:1102,1110](../../src/SpecScribe/SiteGenerator.cs)), and `WorkInventory`'s `implementation-artifacts/` prefix ([WorkInventory.cs:37](../../src/SpecScribe/WorkInventory.cs)) — so a project that nests or renames the implementation folder still classifies its stories/quick-dev/deferred-work. Keep the canonical names as the primary; tolerate variance, don't require it.
  - [ ] Do **NOT** introduce non-BMad epics filenames here (4.3+). Scope is: one home for the literals + location tolerance for the existing names.

- [ ] **Task 5 — Graceful-degradation + non-fatal wiring** (AC: #1, #2)
  - [ ] Every new detection path must degrade to *absent, not broken* (NFR2/NFR8): unresolved ADR home ⇒ no ADR section; unparseable ADR ⇒ existing per-file `catch` → non-fatal event ([SiteGenerator.cs:379–382](../../src/SpecScribe/SiteGenerator.cs)); unrecognized folder ⇒ its own band; missing epics/stories ⇒ omitted surfaces (as today). No new fatal paths.
  - [ ] Route the new "unsupported ADR shape / unrecognized structure" notices through 4.1's `AdapterDiagnostic` channel (category `Unsupported`/`Skipped`) rather than inventing a second reporting surface (memory note: 4.1↔8.1 fold the two notice paths into one — reuse 4.1's). **These emitted notices are the input Story 4.8's diagnostics page will render** — getting them onto the channel is 4.2's obligation; rendering the page is not. Verify against 4.1's landed diagnostic API (it's in `review`).
  - [ ] Honor `IsIgnored` ([SiteGenerator.cs:1236–1243](../../src/SpecScribe/SiteGenerator.cs)) everywhere new enumeration is added — ignored files are neither rendered nor reported.

- [ ] **Task 6 — Tests** (AC: #1, #2)
  - [ ] **Golden-output regression (most important):** generating THIS repo's canonical layout is byte-for-byte unchanged. Reuse the 4.1 golden fixture if present; otherwise add one. A changed existing assertion = you altered canonical output — stop and reconsider.
  - [ ] **ADR location:** fixtures with ADRs under `docs/decisions/` and a nested `decisions/2024/…` render into `adrs/` correctly; explicit `--adrs` still overrides; no ADR dir ⇒ no ADR section, no error. (Extend `ForgeOptionsTests` for the probe + `SiteGenerator`/ADR tests for rendering.)
  - [ ] **ADR format:** fixtures for `ADR-0001-x.md`, `0007-x.md`, `adr_3_x.md`, and an *unnumbered* `decision-login.md` — all render; numbers parse where present; the unnumbered one renders as a card (sorted last) rather than dropping. Status derived from `**Status:**`, a `## Status` section, and `status:` frontmatter; a status-less ADR renders without a badge.
  - [ ] **Grouping:** a fixture whose docs sit under an unrecognized top-level folder (e.g. `design-notes/`) produces a coherently-titled band, not a silent "Other" dump or a mis-grouped BMad title; a flat-at-root fixture still lists via Overview. (Extend `HtmlTemplaterTests` / `PlanningArtifactsGenerationTests`.)
  - [ ] **Filename tolerance:** a fixture nesting `implementation-artifacts/` one level deeper still classifies its stories/quick-dev/deferred-work (extend `WorkInventoryTests` / `SiteNavTests`).
  - [ ] Add to the existing test project (`tests/SpecScribe.Tests/`, `net10.0`, xUnit 2.9); file-per-unit naming.

- [ ] **Task 7 — Verify baseline unchanged end-to-end** (AC: #1, #2)
  - [ ] `dotnet test` — whole suite green; no existing assertion changed (a forced change signals altered canonical output).
  - [ ] Generate this repo's own site and diff against a pre-change build: **zero diffs**. Output dir is `SpecScribeOutput` (memory: generate-output-dir-is-specscribeoutput — NOT `docs/live`).
  - [ ] Record in Completion Notes: the exact ADR fallback-probe order shipped, the number/status schemes covered, and how grouping degradation behaves for an unknown folder — the review will check these against AC #1/#2.

## Dev Notes

### Exactly what "personal-structure assumptions" means here (the target list)
This story exists because the current renderer bakes in the tool author's own repo layout. The concrete, evidence-located assumptions to decouple:

| Assumption | Where | AC | Generalization |
|---|---|---|---|
| ADRs live only in `docs/adrs/` | [ForgeOptions.cs:100](../../src/SpecScribe/ForgeOptions.cs), [SiteGenerator.cs:914–916](../../src/SpecScribe/SiteGenerator.cs) | #2 | ordered fallback probe + 1-level recurse (Task 1) |
| ADR number = leading digits of filename | [SiteGenerator.cs:17,1085–1089](../../src/SpecScribe/SiteGenerator.cs) | #2 | multi-scheme regex; render even if absent (Task 2) |
| ADR status = a `**Status:**` bold line | [SiteGenerator.cs:18,1093–1099](../../src/SpecScribe/SiteGenerator.cs) | #2 | also `## Status` + `status:` frontmatter (Task 2) |
| Unnumbered ADR is dropped from cards | [SiteGenerator.cs:372](../../src/SpecScribe/SiteGenerator.cs) | #2 | render it, sort last (Task 2) |
| Home index groups by 3 fixed folder prefixes | [HtmlTemplater.cs:95–105,152](../../src/SpecScribe/HtmlTemplater.cs) | #1 | structure-derived groups + graceful bands (Task 3) |
| `"epics.md"` / `"implementation-artifacts"` literals scattered in ≥5 files | [SiteNav.cs:104](../../src/SpecScribe/SiteNav.cs), [SiteGenerator.cs:247,253,749,1102,1110](../../src/SpecScribe/SiteGenerator.cs), [WorkInventory.cs:37](../../src/SpecScribe/WorkInventory.cs) | #1 | one shared source of truth + location tolerance (Task 4) |

### Prior art to match — the codebase already practices this
- **`ModuleContext.Detect`** ([ModuleContext.cs:120–175](../../src/SpecScribe/ModuleContext.cs)) — data-driven detection, try-primary-then-fallback, never-throws → `None`. This is the exact posture for the ADR probe and grouping detection: probe, first-match, degrade to absent. Comment at [:71–75](../../src/SpecScribe/ModuleContext.cs) literally says folder layout varies and "Epic 4 will generalize."
- **`SiteNav` well-known-filename-anywhere discovery** ([SiteNav.cs:79–95,104,142–148](../../src/SpecScribe/SiteNav.cs)) — the pattern for "find a family by filename regardless of folder depth"; extend it to the stragglers that still assume a fixed parent dir.
- **`WorkInventory.Build`** ([WorkInventory.cs:29–54](../../src/SpecScribe/WorkInventory.cs)) — "a missing/partial/empty file simply yields fewer entries — never an exception (NFR2)." That degradation discipline is the model for every new detection path.
- **`ArtifactCoverage.Specs`** ([ArtifactCoverage.cs:80–117](../../src/SpecScribe/ArtifactCoverage.cs)) — the family-set "seam Epic 4 generalizes"; not moved here, but its comment is the mental model for "the shape a framework varies."

### Non-negotiable invariants
- **NFR2** ([epics.md:76](../planning-artifacts/epics.md)): resilient to partial/malformed/unsupported/missing → non-fatal notices. Every new branch degrades gracefully.
- **NFR4** ([epics.md:78](../planning-artifacts/epics.md)): extensible without core rewrites — 4.2 removes personal literals so 4.3+ adapters plug in without fighting hardcoded layout.
- **NFR8** ([epics.md:82](../planning-artifacts/epics.md)): shared rendering is framework-agnostic; surfaces degrade to *absent, not broken or misleadingly empty*. The grouping-degradation and ADR-absent behaviors are direct NFR8 obligations.
- **Golden-output byte-identity for this repo** — the single most important guardrail. The canonical layout is the first-probed branch everywhere; new tolerance is proven by NEW fixtures, never by editing an existing assertion.
- **Do not touch git/progress/coverage or the `--deep-git` gate** (memory: deep-git-single-numstat-path) — out of this story entirely.
- **Do not touch status classification** — native→canonical mapping is downstream (`StatusStyles`) and is Story 8.1's domain (memory: epic-4-adapter-contract-scope). An ADR's raw status string renders as-is.

### Watch-mode parity (don't regress)
ADR changes route through `IsAdr` ([SiteGenerator.cs:296–301](../../src/SpecScribe/SiteGenerator.cs)) → `RegenerateAdrs` ([:305–323](../../src/SpecScribe/SiteGenerator.cs)), which compares against `_options.AdrSourceRoot`. If the resolved ADR root now comes from a probe rather than the default, `IsAdr` must compare against the **resolved** root (store the resolved path once at option-resolution/first-detect time), or watch will miss edits under a probed dir. `IsBmadArtifact` ([:242–253](../../src/SpecScribe/SiteGenerator.cs)) similarly gates watch routing on the `epics.md`/`implementation-artifacts` literals — keep it consistent with the consolidated constants. Call out your approach in Completion Notes (Epic 3 risk-center: watch parity).

### Risk centers (where review will focus)
1. **Output drift** — any rendered-byte change to this repo's canonical output fails AC #1's implicit "don't break the author's own repo." Run the golden test early and often.
2. **ADR probe over-reach** — probing too many dirs, or recursing too deep, risks pulling unintended `.md` files into the ADR section (e.g. a `docs/` full of prose). Keep the probe list tight and conventional; recurse one level, not the whole tree; only files that yield a renderable record count.
3. **Grouping "Other" regressions** — the refactor from fixed-prefix to structure-derived groups is the highest-churn change; assert the well-known set renders identically AND unknown folders degrade coherently.
4. **4.1 collision** — if 4.1 lands mid-flight, the filename literals move under you. Coordinate: prefer landing 4.1 first (see sequencing note).

### Project Structure Notes
- Single project: `src/SpecScribe/SpecScribe.csproj` (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). All changes here; **no new project, no namespace split** (seed-level, deferred — [ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- Tests: `tests/SpecScribe.Tests/` (xUnit 2.9, `net10.0`), file-per-unit naming. Relevant existing suites to extend: `ForgeOptionsTests`, `HtmlTemplaterTests`, `PlanningArtifactsGenerationTests`, `SiteNavTests`, `WorkInventoryTests`, `ModuleContextTests`.
- **Output dir is `SpecScribeOutput`** (memory: generate-output-dir-is-specscribeoutput). Never `--output docs/live`.
- Runs on `main` (not a worktree); edits target `C:\Dev\SpecScribe` directly. A background auto-committer on main commits/pushes edits (memory: worktree-edits-must-target-worktree-path) — keep commits coherent.
- Match the heavy XML-doc-comment house style — every public type/member carries a `<summary>` explaining the *why*, tagged `[Story N.M]`. Tag new/changed members `[Story 4.2]`.
- No new visual surface is introduced (this is structural tolerance, not a new card/chart/layout), so the "elicit visual intent" create-story step (memory: create-story-elicit-visual-intent) does not apply — but the scope decision above *is* the "defining decision surfaced up front" that retro asks for.

### References
- [epics.md:620–664](../planning-artifacts/epics.md) — Epic 4 goal ("first generalizing the renderer away from any single project's personal structure") + Story 4.2 ACs (source of truth).
- [epics.md:36,76,78,82](../planning-artifacts/epics.md) — FR1 (shared model without rewriting rendering), NFR2 (graceful degradation), NFR4 (extensible, no core rewrites), NFR8 (framework-agnostic shared rendering; absent-not-broken).
- [4-1-shared-framework-adapter-contract-and-projection-path.md](4-1-shared-framework-adapter-contract-and-projection-path.md) — the ingestion seam 4.2 generalizes inside; its scope block and the `AdapterDiagnostic` channel.
- [rendering-architecture.md:12–35,111–124](../specs/spec-specscribe/rendering-architecture.md) — layer boundaries; `IArtifactAdapter` responsibility (parse one framework's artifacts, tolerate variance); Evolution Sequence.
- [ARCHITECTURE-SPINE.md:34–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1 (shared core), AD-4 (insight providers non-blocking — keep git/progress out), "Seed, Not Invariant" (no package split).
- [ForgeOptions.cs:59–107](../../src/SpecScribe/ForgeOptions.cs), [SiteGenerator.cs:296–387,914–1099](../../src/SpecScribe/SiteGenerator.cs), [HtmlTemplater.cs:91–176,688–780](../../src/SpecScribe/HtmlTemplater.cs), [ModuleContext.cs:64–175](../../src/SpecScribe/ModuleContext.cs), [SiteNav.cs:57–157](../../src/SpecScribe/SiteNav.cs), [WorkInventory.cs:29–55](../../src/SpecScribe/WorkInventory.cs), [AdrModel.cs](../../src/SpecScribe/AdrModel.cs), [AdrLinkRewriter.cs](../../src/SpecScribe/AdrLinkRewriter.cs) — the exact code to generalize.

### Owner-decided (locked 2026-07-10)
- **Grouping (Q re: detection source + unknown-folder naming) → Option B:** keep the ordered well-known group list and its treatment; append each unrecognized top-level folder as its own band titled by the humanized folder name. Reflected in Task 3.

### Open question for the owner (does not block dev — resolve at review or before)
1. **ADR fallback-probe list** — is the proposed order (`docs/adr`, `docs/adrs`, `docs/decisions`, `docs/architecture/decisions`, `docs/architecture/adr`, `adr`, `adrs`) the right conventional set, or should it be tighter/wider? First-match-wins; explicit `--adrs` always overrides.

## Dev Agent Record

### Agent Model Used



### Debug Log References

### Completion Notes List

### File List
