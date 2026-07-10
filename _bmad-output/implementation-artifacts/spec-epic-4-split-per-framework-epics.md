---
title: 'Split framework adapters (4.3–4.7) into per-framework spike-led Epics 11–15'
type: 'chore'
created: '2026-07-10'
status: 'done'
baseline_commit: 'a97fca6dd2bca9c1191bf79df3ca9b2b3cc660a7'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/_bmad-output/implementation-artifacts/sprint-status.yaml'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 4 bundles the framework-agnostic *foundation* (4.1 adapter contract, 4.2 de-personalization, 4.8 diagnostics) together with five per-framework *coverage* stories (4.3 Spec Kit, 4.4 GSD/GSD-Pi, 4.5 SpecFlow, 4.6 Squad, 4.7 Superpowers). The coverage stories share one epic yet each requires deep, framework-specific investigation that a shared epic can't focus on, and none of them front-loads the discovery needed to scope the integration.

**Approach:** Extract 4.3–4.7 into five appended epics (11–15), one per framework, each led by a **Framework Integration Spike** that investigates the epic's scope and integration — mapping the framework's artifacts to the shared adapter contract and explicitly identifying (a) conventions SpecScribe will NOT support and (b) framework data richer than the shared projection — followed by the migrated baseline-coverage story. Rename Epic 4 to its foundation-only role. Additive planning-doc restructure only (`epics.md` + `sprint-status.yaml` at backlog/BDD level + a memory sync); no existing number, story file, or code changes.

## Boundaries & Constraints

**Always:**
- Append the new epics as **Epics 11–15**, purely additive — do NOT renumber any existing epic, story key, or story file.
- Fixed framework→epic mapping: 11 = Spec Kit (FR3), 12 = GSD/GSD-Pi (FR4), 13 = SpecFlow (FR17), 14 = Squad (FR17), 15 = Superpowers (FR17).
- Each new epic has exactly two stories: `X.1` a Framework Integration Spike, then `X.2` the coverage story migrated from the matching 4.3–4.7 block (renumbered, BDD ACs preserved verbatim).
- Preserve the heading shapes the parsers key on: `### Epic N:` / `## Epic N:` / `### Story N.M:` / `**FRs covered:**`; sprint-status keeps `epic-N:` and `N-M-slug:` lines inside the block-isolated `development_status` map.

**Ask First:** Any change that would require renumbering existing epics/stories, creating contexted spike story files, or editing the PRD/code.

**Never:** Renumber Epics 5–10 or their stories. Create story spec files (backlog stays epics.md-only until create-story). Touch `src/`/`tests/`. Alter Epic 4's retained stories 4.1/4.2/4.8 beyond the epic title/intro/FR line. Introduce a Spec Kit/GSD "framework-extra" projection extension here (spikes only *record candidates*).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Two-digit epics parse | `## Epic 11:`…`## Epic 15:` added | EpicsParser `(\d+)` matches; epics render in list + detail | N/A (regex already multi-digit) |
| Sprint-status integrity | `4-3`…`4-7` removed, `epic-11`…`epic-15` + their stories added to `development_status` | Block-isolated map still parses; `SprintStatusParser` emits Epic/Story entries for 11–15 | Malformed map must not degrade whole doc to null |
| Epic 4 no longer lists 4.3–4.7 | Detail section keeps only 4.1/4.2/4.8 | Epic 4 renders as foundation epic; no orphaned Story headings | N/A |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/epics.md` — **Epic List** (lines ~185–187 Epic 4 summary; append point after ~211 before closing comment) and **detail sections** (Epic 4 header/intro/FRs ~620–624; Story 4.3–4.7 blocks ~666–764 to remove; append Epics 11–15 detail after ~1496 end-of-file).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `development_status` map: remove `4-3`…`4-7` (lines ~82–86); append `epic-11`…`epic-15` blocks after the `epic-9` block (~133) before `action_items`.
- `src/SpecScribe/EpicsParser.cs` — reference only; confirms `(\d+)` epic/story regex handles 11–15 (no edit).
- `src/SpecScribe/SprintStatusParser.cs` — reference only; `epic-(\d+)` / `(\d+)-(\d+)-` key regex handles two-digit epics (no edit).
- `C:\Users\MattE\.claude\projects\C--Dev-SpecScribe\memory\` — `epic-4-adapter-contract-scope.md`, `story-1-4-split-into-1-4-1-5-1-6.md`, `MEMORY.md` to sync with the new structure.

## Tasks & Acceptance

**Execution:**
- [x] `epics.md` (Epic List) — Rewrote the Epic 4 summary as the foundation epic (title "Framework-Agnostic Adapter Foundation", **FRs → FR1 only** — FR3/FR4 now owned by Epics 11/12 to keep requirement→epic traceability single-owner; dropped the per-framework enumeration). Appended five `### Epic 11:`…`### Epic 15:` list entries (framework, one-line spike-first goal, `**FRs covered:**` per mapping) after Epic 10, before the trailing comment.
- [x] `epics.md` (detail) — Retitled `## Epic 4` to "Framework-Agnostic Adapter Foundation", rewrote its intro + `**FRs covered:**` line (FR1); deleted the Story 4.3–4.7 detail blocks (4.1, 4.2, and the 4.8 comment+story intact).
- [x] `epics.md` (detail) — Appended `## Epic 11`…`## Epic 15` sections at end of file. Each: intro, `**FRs covered:**`, `### Story X.1: <Framework> Integration Spike` (spike ACs below), `### Story X.2: <Framework> Baseline Adapter Coverage` (migrated the matching 4.3–4.7 user story + both BDD ACs verbatim, renumbered).
- [x] `sprint-status.yaml` — Removed `4-3`…`4-7` keys from `development_status`; appended `epic-11: backlog` … `epic-15: backlog`, each with `X-1-<framework>-integration-spike: backlog`, `X-2-<framework>-baseline-adapter-coverage: backlog`, and `epic-N-retrospective: optional`. Bumped `last_updated`.
- [x] `memory/epic-4-adapter-contract-scope.md`, `memory/story-1-4-split-into-1-4-1-5-1-6.md`, `memory/MEMORY.md` — Record: Epic 4 is now foundation-only; 4.3–4.7 relocated to spike-led Epics 11–15 (append, no renumber) on 2026-07-10.

**Spike story AC pattern** (applied per framework F in each Epic X.1):
- Given representative current-version F repositories, when the F artifact set is surveyed against Story 4.1's `ArtifactBundle`/projection model, then a written coverage map classifies each F artifact type as mappable / partially-mappable / unsupported and names the target shared-model projection for each mappable type.
- Given F conventions that exceed the shared model (framework-specific fields, states, relationships) or that SpecScribe will deliberately not support, when the spike documents findings, then extra-data cases are recorded as candidate projection extensions or explicit non-goals, and deliberately-unsupported conventions are listed with rationale and the non-fatal notice they emit — giving Story X.2 an agreed scope boundary.

**Acceptance Criteria:**
- Given the restructure is applied, when `epics.md` is scanned, then Epics 11–15 each exist with an Integration Spike as `X.1` and the migrated coverage story as `X.2`, and Epic 4 lists only 4.1/4.2/4.8 under a foundation title.
- Given no existing epic was renumbered, when Epics 1–10 and their sprint-status keys/story files are inspected, then all are byte-unchanged except Epic 4's title/intro/FR line, the removed 4.3–4.7 blocks, and the FR Coverage Map rows for FR3/FR4/FR17 (repointed from Epic 4 to Epics 11/12/13–15).
- Given `sprint-status.yaml` is parsed, when `SprintStatusParser.Parse` runs, then it returns non-null with Epic/Story entries for 11–15 and no `4-3`…`4-7` entries.

## Spec Change Log

- **2026-07-10 (self-review, patch):** Moving FR3/FR4/FR17 off Epic 4 left the **FR Coverage Map** (epics.md `### FR Coverage Map`) still pointing FR3/FR4/FR17 at Epic 4 — a broken requirement→epic link (fr3/fr4/fr17 pages resolved to Epic 4, which no longer covers them). Fixed the three rows to Epics 11/12/13–15 and widened AC#2 to include them. KEEP: append-only, no-renumber invariant held; verified via clean-source regeneration that fr3→Epic 11, fr4→Epic 12, fr17→Epic 13–15, fr1→Epic 4.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` — expected: all pass (epics/sprint parsers green with two-digit epics).
- `dotnet build src/SpecScribe/SpecScribe.csproj` + run generation against this repo — expected: no new fatal errors; Epics 11–15 appear in the roadmap surface and sprint board; Epic 4 shows as foundation.

**Manual checks:**
- Grep confirms zero `### Story 4.3`…`4.7` headings remain in `epics.md` and no `4-3`…`4-7` keys remain in `sprint-status.yaml`; Epic List and detail agree on Epic 4's new title and on Epics 11–15.

## Suggested Review Order

**Foundation reframe (Epic 4 → foundation-only)**

- Start here: Epic 4 detail retitled + rescoped to the adapter foundation (4.1/4.2/4.8 only).
  [`epics.md:641`](../planning-artifacts/epics.md#L641)
- Epic List summary mirrors the rename; FR line narrowed to FR1.
  [`epics.md:185`](../planning-artifacts/epics.md#L185)
- Traceability fix — FR3/FR4/FR17 repointed off Epic 4 to Epics 11/12/13–15 (the self-review catch).
  [`epics.md:141`](../planning-artifacts/epics.md#L141)

**New per-framework spike-led epics (11–15)**

- Epic List entries for the five appended epics (framework + FRs).
  [`epics.md:213`](../planning-artifacts/epics.md#L213)
- Detail sections; each epic's Story X.1 is the Integration Spike, X.2 the migrated coverage.
  [`epics.md:1429`](../planning-artifacts/epics.md#L1429)
- Spike AC shape — maps artifacts to the contract, flags unsupported conventions + framework-extra data.
  [`epics.md:1435`](../planning-artifacts/epics.md#L1435)

**Sprint tracking**

- Epic 4 block: 4.3–4.7 keys removed, foundation stories retained.
  [`sprint-status.yaml:79`](sprint-status.yaml#L79)
- Appended epic-11…15 blocks (spike + coverage stories, all backlog).
  [`sprint-status.yaml:134`](sprint-status.yaml#L134)
