---
baseline_commit: 15336f4
---

# Story 17.1: Structural and Consistency Remediation Sweep

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!-- created 2026-08-07 (create-story 17.1) at baseline_commit 07bdb79. Every line number in this file was
     resolved at that revision and WILL drift — see § "Citations drift" before trusting any `:NNN`. -->

**baseline_commit:** `07bdb79` (`Merge branch 'worktree-ci-gate-regen'`)

## Story

As the SpecScribe maintainer preparing for public release,
I want a deliberate sweep for structural weaknesses, inconsistencies, and duplication across the C# core, the extension shim, and the stylesheet,
So that the codebase is coherent and maintainable before outside contributors and users depend on it.

## Acceptance Criteria

Reproduced verbatim from `epics.md` § Epic 17 → Story 17.1. Read the ⚠ note under AC #1 before acting on its examples.

1.
**Given** the code accumulated across the feature epics
**When** the structural review runs
**Then** it identifies and remediates structural weaknesses and inconsistencies — duplicated single-source-of-truth violations (for example the twin sunburst legend tuples, the divergent `scroll-margin-top` clearance values, the icon key/label dual-representation), dead or unreachable code, and naming/token drift — with each fix pinned by a test or an explicit rationale for deferral
**And** the golden byte-parity gate and full test suite stay green (remediation must not change rendered output unless a change is intentional and re-baselined).

> ⚠ **All three examples AC #1 names are already closed, and the gate it names no longer exists.**
> Verified at `07bdb79` during story creation:
>
> | AC #1 says | Actual state at HEAD |
> |---|---|
> | "the twin sunburst legend tuples" | **Closed.** `deferred-work.md:757` — *"RESOLVED 2026-07-19 as misdiagnosed"* (`spec-epic3-deferred-debt-cleanup`); both call sites verified against `Charts.cs`. |
> | "the divergent `scroll-margin-top` clearance values" | **Closed.** `deferred-work.md:666` — resolved 2026-07-18. Re-verified: all 7 live occurrences across `specscribe.css` + `ir-content.css` are `var(--nav-offset)`; zero divergence. |
> | "the icon key/label dual-representation" | **Closed.** `deferred-work.md:710`, struck through (`Icons.ForConcept` ampersand labels). |
> | "the golden byte-parity gate" | **Retired.** `GoldenContentFingerprint` was removed by ADR 0034 / Story 23.6. See § *The gate AC #1 names is gone — and its replacement is blind to this story* — this is the single largest trap in this story. |
>
> The AC's *intent* (find and fix single-source-of-truth violations, dead code, drift) is live and well-supplied
> with real work. Its *illustrations* are a snapshot of 2026-07 and must not be chased. The verified live
> inventory is in § *The actual work-list* and is what the Tasks below are built from.
> Raised as Q1 in § *Questions for the owner* — the epic text may want amending.

2.
**Given** items already recorded in `deferred-work.md` as maintainability/consistency debt
**When** this sweep triages them
**Then** each is either fixed here or carried forward with a recorded decision, and no fix silently regresses another surface
**And** the review covers the extension TypeScript shim and the CSS, not only the C# core.

> ⚠ **AC #2's scope statement predates Epic 23.** "the C# core, the extension shim, and the stylesheet" was
> written when C# wrote the HTML. It no longer names the `web/` Nuxt renderer (~45 unresolved issues) or the
> second stylesheet (`web/assets/ir-content.css`, 6,363 lines) that now exists. See § *Scope* for the boundary
> this story runs with, and Q2.

## Tasks / Subtasks

**Sequencing is load-bearing.** Task 0 must complete before any other task, and Task 3 (CSS) must not start
until Task 0 has established whether `check:ir-content` is green at HEAD.

- [x] **Task 0 — Establish the baseline before changing anything (AC: #1, #2)**
  - [x] Regenerate the analysis digest: `node tools/analysis-digest/index.mjs`. **`.specscribe/analysis/` does not exist at `07bdb79`** — per CLAUDE.md that is UNKNOWN, never clean. Every Sonar count in this story is quoted from the 2026-07-27 baseline triage and is ~2 weeks stale; the digest is how you get current numbers.
  - [x] Record the digest's `provenance.evaluatedAtRevision` and compare to `git rev-parse HEAD`. If they differ, the digest is stale regardless of `isStale`.
  - [x] Run the full C# suite and record the pass count as the pre-sweep baseline: `dotnet test SpecScribe.slnx`.
  - [x] **Measure `check:ir-content` through the complete load-bearing order** (17.4 AC #4 records it as *believed RED* but unmeasured — and a partial run gives a confidently wrong answer either way):
    ```sh
    dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental
    dotnet run --project src/SpecScribe -- generate
    cd web && npm run extract:ir-content && npm run check:ir-content
    ```
  - [x] Run `cd web && npm run check` (tokens + ir-content + assets + parity) and `npm run test` and record red/green **per gate**. If a gate is already red at HEAD, say so in the Dev Agent Record before you touch a line — otherwise you cannot distinguish your breakage from the inherited state, and CLAUDE.md forbids regenerating a baseline without first establishing causality.
  - [x] If `check:ir-content` is red at HEAD, **stop and report** rather than remediating CSS on top of it (Task 3 depends on this gate being a trustworthy signal).

- [x] **Task 1 — Adjudicate the 7 first-party C# reliability findings (AC: #1)**
  These are the highest-value items: an always-False condition guarding a render branch is a defect class this
  project has shipped and caught only in a live browser. **Do not assume they are all real** — see the
  false-positive warning in Dev Notes.
  - [x] `csharpsquid:S2583` (condition always evaluates to a constant, code path therefore unreachable) at 5 sites — resolve each **by symbol**, not by the drifted line number:
    - [x] `SiteGenerator.MutateSourceInventory` region (cited `SiteGenerator.cs:1392`, "always True")
    - [x] `SiteGenerator` commit-detail page loop, `pages.TryAdd` / duplicate-hash region (cited `:2441` and `:2448`, "always False")
    - [x] `WorkGraph` — the `if (edges.Count == 0) return null;` guard at the end of the epic-graph builder (cited `WorkGraph.cs:403`, "always True")
    - [x] `CapabilityStyler` — the `if (matched == 0 || leftover.Contains("<li", …))` bail-out after `CapItem.Replace` (cited `CapabilityStyler.cs:57`, "always True")
  - [x] `csharpsquid:S4158` (collection known to be empty where used) at 2 sites: `SiteGenerator.cs:1939`, `HtmlRenderAdapter.Dashboard.cs:235`.
  - [x] For each: either **fix + pin with a regression test**, or record an explicit **won't-fix rationale** in `deferred-work.md` naming why the analyser is wrong. AC #1's "a test or an explicit rationale for deferral" is satisfied by either — but not by silence.
  - [x] Reconcile the record conflict this exposes (see § *A record conflict to resolve*): `docs/SonarCloudSetup.md` calls the 12-bug reliability sweep **"unowned"**, while `deferred-work.md` schedules 7 of those bugs to **this story**. Resolve against the code and correct whichever record is wrong.

- [x] **Task 2 — Kill the repository's only BLOCKER: a test that cannot fail (AC: #1)**
  - [x] `tests/SpecScribe.Tests/ChartsTests.cs` → `Sunburst_CenterReportsEpicCountNotStoryCount` (line 338 at `07bdb79`). **Verified assertion-free at HEAD:** it builds `multiSvg` and `singleSvg` and then ends. Its own comment states the intent — *"the center headlines the epic count with an 'epic(s)' label (pluralized), never the story total"* — so write the assertions that comment already specifies: `multiSvg` asserts "2 epics", `singleSvg` asserts the singular form, and neither asserts the story total.
  - [x] Confirm the restored test actually **fails** if the pluralization/center-label behavior is broken (a test added without checking it can fail reproduces the defect in a new shape).
  - [x] A repo-wide scan at `07bdb79` found 14 other `[Fact]`/`[Theory]` bodies with no literal `Assert.` — **all 14 were checked and delegate to helper methods that assert internally** (e.g. `AssertPlotlyDataContract`, `HierarchyRolloutTests.cs`). They are not defects; do not "fix" them. This matches Sonar finding exactly 1 `S2699`.

- [x] **Task 3 — Stylesheet duplicate declarations (AC: #1, #2)** — blocked on Task 0's `check:ir-content` result.
  - [x] All four cited duplicates **re-verified live at `07bdb79`, and every cited line number had moved** — this is the concrete proof of the drift rule:

    | Selector | `deferred-work.md` cited | Actual at `07bdb79` |
    |---|---|---|
    | `:root` re-opened | 6 / 5511 | **6 / 5739** |
    | `.coverage-card` | 4191 / 5918 | **4356 / 6146** |
    | `.now-next-card.active` | 3714 / 3723 | **3875 / 3884** |
    | `.impact-shape-tabs` | 5549 / 5580 | **5777 / 5808** |

  - [x] `:root` re-opened at `specscribe.css:5739` having been declared at line 6 — **this is the priority one.** It is the exact class of defect this project has already shipped invisibly once and caught only by reading computed styles in a live browser. Verify the fix in a live browser, not in review.
  - [x] `.coverage-card` duplicated (4356 / 6146) — note this touches the vocabulary collision Epic 27 is already tracking; check the two blocks are not two *different* concepts sharing a name before merging them.
  - [x] Remaining: duplicate `border` at ~1596/1964, duplicate `padding` at ~1598, and 3 `css:S1874` deprecated `word-break: break-word` keywords.
  - [x] `web/assets/ir-content.css` carries **mirrored copies** — re-verified at `07bdb79` **after** commit `0b1f561` regenerated this file: `.now-next-card.active` at **3111 / 3123** and `.coverage-card` at **3573 / ~5151** (cited at 2272/3364/3930 — moved twice now). The IR stylesheet **inherited** them from `specscribe.css`.
    - `ir-content.css` is a **GENERATED FILE — DO NOT EDIT**. Fix `specscribe.css` and re-extract; hand-editing it is reverted by the next `extract:ir-content`.
    - Every rule there is re-nested under `.ir-content`, so grep unanchored (`.ir-content .coverage-card`), not for `^.coverage-card`.
    - Its header comment claims specscribe.css is "7,041-line" — it is **7,877** lines at `07bdb79`. That stale generated figure is itself AC #1 "naming/token drift"; fix it in whatever emits the header, not in the generated output.
  - [x] **Follow CLAUDE.md's regeneration order exactly** — two `generate` calls, deliberately. Skipping either leaves you inspecting a page whose CSS predates your edit, and the failure looks exactly like "my selector is wrong":
    ```sh
    dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental
    dotnet run --project src/SpecScribe -- generate
    cd web && npm run extract:ir-content && npm run check:ir-content
    cd web && npm run build:package
    dotnet run --project src/SpecScribe -- generate
    ```

- [x] **Task 4 — Extension TypeScript shim (AC: #2)**
  `extension/src/extension.ts` — 2,398 lines, 923 ncloc, 12 unresolved issues, **0.0% coverage**. The only
  first-party source file in the project with both a bug and zero test coverage.
  - [x] The bug: `typescript:S5850` — regex alternation whose operator precedence is not explicit. **Cited at `:1268`; that line is now unrelated code.** Re-resolved by symbol at `07bdb79`: the terminal-profile matcher, `/git bash|bash|wsl|sh$/i` (~line 2024). The `$` anchor binds only to the final `sh` alternative, so `git bash`/`bash`/`wsl` match anywhere in the string while `sh` must be terminal — almost certainly not the intent. Make the grouping explicit and decide deliberately which alternatives anchor.
  - [x] The remaining 11: `typescript:S6571`, `S6551`, `S6671`, `S7778`, `S7780`, `S7781`.
  - [x] **Read § *Touching the extension has a quality-gate cost* before starting** — there is no TS test harness at all (no `test` script, no test runner in `devDependencies`, no test files), so you cannot pin an extension fix with a test today without first standing one up. That harness is explicitly **not** this story's (see § *Scope*). Record which route you took.
  - [x] Re-run `npm run typecheck` in `extension/` after each change — it is the only automated signal this file has.

- [x] **Task 5 — Single-source-of-truth and duplication clusters in the C# core (AC: #1, #2)**
  Verified present at `07bdb79`:
  - [x] **Unguarded `ToDictionary` on epic/AC numbers — 11 sites, and the codebase already disagrees with itself.** `RequirementsTemplater.cs:682` guards with `GroupBy(e => e.Number).ToDictionary(g => g.Key, g => g.First())`; the other 10 call `ToDictionary(e => e.Number)` bare and throw on a duplicate epic number in a user's `epics.md`. Sites: `Charts.cs:3137`, `Charts.cs:3357`, `EpicsParser.cs:60`, `EpicsViewBuilder.cs:65`, `RelatedWorkCards.cs:98`, `RequirementsParser.cs:55`, `RequirementsParser.cs:307`, `SiteGenerator.cs:3432`, `SiteGenerator.cs:3601`, `SiteGenerator.cs:3769`. **Pick one policy and apply it everywhere** — that is the single-source-of-truth violation, not the crash. Pin with a test that feeds a duplicate epic number through the parser.
  - [x] **Duplicated footer-strip regex.** `FooterClock` is declared independently in `tests/SpecScribe.Tests/GoldenNormalization.cs:26` and `tests/SpecScribe.Tests/TestArtifactDiscoveryTests.cs:612`, and `SiteGeneratorStatusStylesTests.cs:114` hand-rolls a third `StripFooterClock` local. Consolidate onto `GoldenNormalization`.
  - [x] **`BmadCommands` next-step classifiers route on raw status strings.** `BmadCommands.cs:505` and `:515` use `status.Contains("review")` / `status.Contains("done") || status.Contains("complete")` while the same file elsewhere routes correctly through `StatusStyles.ForStory(story)` (lines 42, 69, 105, 627). Substring matching on a free-text status is the bug shape — `"review"` matches `"code-review-blocked"`. Route through `StatusStyles`. **Check ADR 0025** (`retired` is a terminal stage in *both* classifiers) before changing classifier behavior.
  - [x] The `~300`-issue maintainability band (94 `S1192` duplicated string literals, 86 `S3776` cognitive complexity, 48 `S3358` nested ternaries, 29 `S3267`, 28 `S107`, 9 `S125` commented-out code, plus an `S2589`/`S1121`/`S127`/`S1066`/`S1172` tail). **This is not a to-do list — it is 2,999 minutes (~50 h) of Sonar-estimated effort and it will not fit in one story.** Take the `S125` (dead/commented-out code, 9 instances — directly AC #1's "dead or unreachable code") and the `S1192` instances that are genuine single-source-of-truth violations; explicitly defer the `S3776`/`S107` complexity band with a recorded rationale, and say so rather than leaving it looking swept. See § *Bounding this story*.

- [x] **Task 6 — Record every decision (AC: #2)**
  - [x] For each item touched: fix it **or** carry it forward with a recorded decision in `deferred-work.md`. AC #2 admits no third state.
  - [x] Strike through closed items in place with the resolution — never delete (the file's own "How to read this file" preamble makes the audit trail load-bearing, and `DeferredWorkParser` renders it into the portal).
  - [x] Update the stale claims this story disproves: the three closed AC #1 examples, the `GoldenContentFingerprint` blocker language, and the `SonarCloudSetup.md` "unowned" 12-bug claim (Task 1).
  - [x] Note in the story record whose concurrent changes your regeneration sat on top of (CLAUDE.md § *Concurrent work*).

- [x] **Task 7 — Prove nothing regressed (AC: #1, #2)**
  - [x] `dotnet build SpecScribe.slnx --no-incremental` then `dotnet test SpecScribe.slnx` — compare to Task 0's baseline count.
  - [x] `cd web && npm run check && npm run test`.
  - [x] **Live-browser verification for every CSS change** — the suite structurally cannot see containment leaks, sub-pixel collapse, or DOM corruption. Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.
  - [x] Confirm any regenerated gate baseline is **stable across two repeated runs** before locking it in.

### Review Findings

<!-- Code review 2026-08-09 (bmad-code-review, 3 parallel layers: Blind Hunter, Edge Case Hunter, Acceptance
     Auditor). Scoped to commit d6ba8f2 vs baseline 15336f4 — the story landed as ONE isolated commit whose
     diff matches this File List exactly, so no sibling-story hunk exclusion was needed. 18 findings after
     dedup; 3 dismissed as noise. Every finding below was re-verified as STILL LIVE on origin/main at
     cc54708, 60+ commits later. NOTE: `BmadCommands.cs` was renamed to `WorkflowCommands.cs` by cd687e4 —
     line references use the CURRENT filename. AC #1 assessed MET; AC #2 assessed NOT MET (record-keeping,
     not code). -->

- [ ] [Review][Decision] **A `retired` story renders no Next Steps panel at all** — `ForStory` gained `if (stage is "done" or "retired") return suggestions;` but `RenderNextSteps` (`WorkflowCommands.cs:42`), `StoryCommands` (`:69`) and `PrimaryStoryCommand` (`:105`) all still gate on `== "done"` only, so a retired story falls to `RenderPanel(empty)` → `string.Empty`. The new arm's own comment ("Done stories are handled by `RenderNextSteps`") is true for `done` and false for `retired`. Three losses: no terminal-state affordance at all (the opposite of ADR 0025's premise that `retired` is a *visible* first-class terminal stage), no muted `correct-course` escape hatch (which `done` gets), and the arm returns before `AppendDeferredAlternate` so **open deferred work attached to a retired story becomes unreachable from its page**. The new test `RenderNextSteps_RetiredStory_OffersNoWork` asserts only `DoesNotContain`, which `""` satisfies trivially. **Decision needed:** should `retired` render its own terminal panel (and what should it say), reuse the done panel, or keep suggestions? Extending the three gates to `is "done" or "retired"` would hand retired stories the *celebratory* all-done panel, which is wrong — hence a decision, not a mechanical patch. [src/SpecScribe/WorkflowCommands.cs:42, :69, :105, :563]
- [ ] [Review][Decision] **A duplicate epic number now silently corrupts the generated site instead of failing loudly** — first-wins was applied to the eleven *lookups*, but nothing de-duplicates the model and no diagnostic is emitted. `EpicsParser.Parse` builds one `EpicInfo` per list entry, so a duplicated number yields two epics with the same `Number`, both handed the *same* `section.Stories` instance. Consequences, all silent with `errors=0`: two epic pages written to one path (`epics/epic-N.html`) so one is destroyed; both epics render the **first** epic's progress numbers, completion bar and status donut (`SiteGenerator.cs:3432`+`:3500`, `:3769`+`:3778`); a duplicate `## Epic N` H2 drops its whole body and **double-counts every story** in dashboard tallies; duplicate `(AC: #N)` deep links all resolve to the first criterion (`SiteGenerator.cs:3601`); duplicate DOM ids on the index page. The tolerant policy itself is deliberate and documented in `NumberIndex`'s docblock — the gap is that downstream was never taught what a collision means. `AdapterDiagnostic` exists for exactly this. **Decision needed:** emit a diagnostic and keep first-wins, de-duplicate in the parser, or accept as-is. [src/SpecScribe/NumberIndex.cs, src/SpecScribe/EpicsParser.cs:60-88, src/SpecScribe/EpicsTemplater.cs:64]
- [ ] [Review][Decision] **Free-text "done"-ish statuses regressed from *no panel* to *"create this story"*** — the removed `status.Contains("done") || status.Contains("complete")` was a substring test; `StatusStyles.ForStatusFromTokens` deliberately keeps done/complete **exact-only** (so `not-complete` stays unrecognized). So `Status: Done (2026-08-01)`, `Done - with caveats` and `done ✅` now fall through to the default arm and the page recommends **`create-story`** on a story that demonstrably already has an artifact. `almost-done` / `nearly complete` do the same. Separately, `ready-for-review` silently moved from the `dev-story` arm to the `code-review` arm. `RenderNextSteps_CanonicalStages_StillRouteAsBefore` — the test whose stated job is to prove the rewrite behaviour-preserving — covers only `ready-for-dev`, `in-progress`, `review`, `drafted` and none of these. **Decision needed:** widen `StatusStyles` done-matching (trades against its deliberate exact-only design), or suppress `create-story` for an unrecognized status on a story that already has an artifact. [src/SpecScribe/WorkflowCommands.cs:563→:572, src/SpecScribe/StatusStyles.cs:100-124]
- [ ] [Review][Patch] The replacement `tokens-lib` assertion **cannot fail**, and it deleted the only coverage of the Story 23.2 fail-open bug [web/test/tokens-lib.test.mjs:85]
- [ ] [Review][Patch] `word-break: break-word` → `overflow-wrap: break-word` is not the equivalent de-deprecation; the 1:1 form is `overflow-wrap: anywhere`, and the difference lets the auto-layout diagnostics table overflow horizontally [src/SpecScribe/assets/specscribe.css:7456]
- [ ] [Review][Patch] `pwsh` matches `sh$`, so PowerShell 7 gets POSIX backslash quoting — and the new comment reasons about "PowerShell" but never `pwsh`; the grouping change itself is behaviourally inert (`git bash` ⊂ `bash`, `$` still binds only to `sh`) [extension/src/extension.ts:2029]
- [ ] [Review][Patch] The `deferred-work.md` strikethrough is applied **backwards** on all 4 amended entries — the file's own preamble says "a struck-through claim is closed; the text under it is the audit trail", but these strike the *resolution* and leave the stale claim standing, so the portal renders the resolution as retracted and two only-partially-resolved items as fully closed [_bmad-output/implementation-artifacts/deferred-work.md:1260, :1278, :1292, :1297]
- [ ] [Review][Patch] Two CANONICAL cluster entries covering work this story *fixed* were never amended and still read "still open" — the `ToDictionary` entry says "Still open; **5 live sites**" (11 were converted), and the `BmadCommands` entry says "Re-verified STILL OPEN: the token `retired` does not appear anywhere in `BmadCommands.cs`", which this commit makes false [_bmad-output/implementation-artifacts/deferred-work.md:1209, :106, :858-860]
- [ ] [Review][Patch] The `.coverage-card` → Epic 27 deferral is recorded **only** as a continuation line inside an item now marked resolved, is absent from the new `## Deferred from: 17-1-…` section, and `epics.md` was never told — so Story 17.4 and Epic 27 will both miss it [_bmad-output/implementation-artifacts/deferred-work.md:1279]
- [ ] [Review][Patch] Neither new `EpicsParser` test exercises the change it names: the `DuplicateEpicNumberMd` fixture has exactly one `## Epic 1:` H2, so the old `ToDictionary(s => s.Number)` could not have thrown and the test would pass unchanged at `15336f4`; and the `EpicsViewBuilder` site sits behind `if (model.Milestones.Count == 0) return …`, which BMad never populates. **Zero of the 11 converted sites are covered end-to-end.** [tests/SpecScribe.Tests/EpicsParserTests.cs:1313-1351, src/SpecScribe/EpicsViewBuilder.cs:63-65]
- [ ] [Review][Patch] `NumberIndex.ByFirst` regressed the null contract from `ArgumentNullException` to `NullReferenceException`, and has no comparer overload — so the first string-keyed adopter silently gets the default comparer, diverging from the explicit `StringComparer.Ordinal` used one line away in `RelatedWorkCards.cs:99`. It also evaluates `value(item)` for keys `TryAdd` then discards [src/SpecScribe/NumberIndex.cs:22-40]
- [ ] [Review][Patch] The second `word-break` "fix" is inert: `.next-step-command .cmd-text` (0,2,0) is outranked by `.cmd-badge .cmd-text { white-space: nowrap }` (0,2,0) declared 2,666 lines later, and both always co-apply — so no wrapping property can take effect. Counted as one of the two `S1874` fixes; it is a no-op in a rule AC #1's dead-code clause should have flagged [src/SpecScribe/assets/specscribe.css:3689 vs :6355]
- [ ] [Review][Patch] `tokens-lib.test.mjs` title overclaims — `it('carries every token the real stylesheet declares, wherever it is declared')` while `findRootBlocks` deliberately skips `@media`-nested `:root`, so `--nav-offset` (`specscribe.css:6185`) is not carried; the body also weakened `blocks.length` from `> 1` to a near-vacuous `> 0` [web/test/tokens-lib.test.mjs:61, :70]
- [ ] [Review][Patch] Story-record corrections: Task 5's `S1192` subtask is checked `[x]` but no duplicated string literal was consolidated (its rationale conflates the three named SSOT clusters with `S1192` findings); the Scope table was not amended for 8 changed files outside its declared in-scope set (all task-mandated or regeneration outputs — a documentation gap, not unauthorized expansion); and Task 0's record labels the digest revision `evaluatedAtRevision` when `01acf5b` is `provenance.analysisRevision` — CLAUDE.md's read-time staleness rule keys on the former [this file, Task 5 / § Scope / Dev Agent Record]
- [x] [Review][Defer] `Sunburst_CenterReportsEpicCountNotStoryCount` asserts over the entire rendered payload rather than the centre node [tests/SpecScribe.Tests/ChartsTests.cs] — deferred, currently sound (only `HierarchyExplorer.cs:443`'s `ProjectRootKind` arm emits `N epic(s)`) but would silently weaken if another surface gained an "N epics" string

**Dismissed as noise (3):** the ungated `generatedBytes` recurrence (the story already filed it as a new deferred-work item); `Assert.DoesNotContain` being unreachable after `Assert.Equal(html, styled)` in the new CapabilityStyler test (cosmetic); the "3 `css:S1874`" vs actual 2 miscount (already recorded at `deferred-work.md:1278`).

## Dev Notes

### The gate AC #1 names is gone — and its replacement is blind to this story

This is the highest-risk misunderstanding available in this story, so it comes first.

AC #1 promises safety via "the golden byte-parity gate … remediation must not change rendered output". That
gate — `GoldenContentFingerprint` — **was retired by ADR 0034 (Story 23.6)**. `SiteGeneratorAdapterTests.cs`
carries only its tombstone comment.

Its replacement, `npm run check:parity`, **cannot see a C#-side change.** Per CLAUDE.md § *Which gate is which*,
its corpus IR is frozen at `web/fixtures/parity-corpus/`, so anything the C# region composer emits differently
renders from the *pinned* input and the gate stays green. This was verified on 2026-08-01: a change that removed
an element from the shared nav on **every page** left all 24 routes byte-identical.

**Consequence for this story specifically:** Tasks 1, 5 and much of the `S1192` work are C#-side changes. A green
`check:parity` is **not** evidence that your remediation preserved rendered output. It means "the renderer still
behaves the same on a frozen fixture". The real coverage is:

| Change surface | What actually catches a regression |
|---|---|
| C# region composer / templaters (Tasks 1, 5) | unit tests over the region + **live-browser inspection** |
| `specscribe.css` (Task 3) | `check:ir-content` **plus** live computed styles |
| `web/` renderer | `check:parity` (this is what it is for) |
| `extension/src` (Task 4) | `npm run typecheck` only — no tests exist |

Do not report AC #1's second clause as satisfied on the strength of `npm run check` alone.

### `check:ir-content` cannot catch a bug in its own derivation

`check:ir-content` re-derives through the same `harvest`/`selectorIsUsed` code the extractor uses, so a rule
wrongly dropped is dropped identically on both sides and the diff is empty. A dangling `else` in `harvest` once
meant **no id was ever collected**, so every id-bearing selector was pruned and the Code Map's spec/test filter
was absent from the shipped site — with every gate green, found only by reading computed styles in a live
browser. `web/test/ir-content-harvest.test.mjs` now pins the derivation itself; **extend that test rather than
trusting the round-trip gate** if Task 3 touches selector shapes.

### The S2583 findings are probably not all real — adjudicate, do not bulk-fix

Two of the five were read at `07bdb79` during story creation and both look like analyser dataflow blind spots:

- **`CapabilityStyler.cs:57`** — `if (matched == 0 || leftover.Contains("<li", …))`. `matched` is incremented
  **inside the `CapItem.Replace` lambda** on the preceding line. Sonar's dataflow does not model the side effect,
  so it concludes `matched == 0` is invariant. The code is correct and the guard is load-bearing: its comment
  explains that a stray top-level `<li>` means the list is not the pure CAP convention and rewrapping it would
  emit invalid HTML.
- **`WorkGraph.cs:403`** — `if (edges.Count == 0) return null;` where `edges` is populated by `Link(...)` calls
  through the preceding loop. Same shape.

**"Fixing" either by deleting the condition would remove a real guard and ship invalid markup.** AC #1
anticipates this: "a test or an explicit rationale for deferral". For a false positive the correct output is a
recorded rationale plus, if the guard is genuinely untested, a test that pins the behavior the guard protects.
Per ADR 0035 § Decision 5, a rule-level suppression is **not** the route here — that mechanism is deliberately
applied to zero rules and its one home is `docs/SonarCloudSetup.md` § *Rule-level decisions*, not an inline
pragma. A per-issue "won't fix" in `deferred-work.md` is the right granularity.

The other three (`SiteGenerator.cs:1392`, `:2441`, `:2448`, `HtmlRenderAdapter.Dashboard.cs:235`,
`SiteGenerator.cs:1939`) were **not** individually adjudicated during story creation — read each before deciding.

### Touching the extension has a quality-gate cost (ADR 0035)

ADR 0035 § Decision 2 records that `extension/src/**` is **deliberately not** coverage-excluded: "its 0% is real
information the project wants visible, **at the accepted cost that its next change can turn the gate red on
it**." Task 4 is that next change.

What this does and does not mean:

- **It does not block CI.** `sonar.qualitygate.wait` is unset (ADR 0035 § Decision 4), and the workflow file says
  so explicitly. A red gate fails nothing today.
- **It does move the three preconditions further away.** Making the gate blocking requires passing
  `new_coverage` + `A` `new_reliability_rating` + `A` `new_security_rating`. Adding uncovered TS lines works
  against the first.
- **The honest options are:** (a) make the smallest correct fix and record the coverage cost; (b) stand up a
  minimal TS test harness first — but that is the "absent TypeScript test harness" cluster, which belongs to
  **17.4**, not here; (c) defer Task 4 with a recorded rationale. Pick one deliberately and say which.

Also relevant: the new-code period is a sliding `days: 30` window that ADR 0035 records "has started behaving as
a whole-project gate" — new code went 3,198 → 22,640 lines in one day as unrelated epics landed on shared `main`.
**Expect this story's Sonar deltas to look disproportionate to its diff.** Do not treat that as a signal about
your own change.

### Citations drift — re-resolve every one by symbol

`deferred-work.md`'s own preamble says cited line numbers "age on the next commit". This story creation proved it
at scale: **every one of the four CSS citations and the extension regex citation had moved by the time they were
checked** (see the tables in Tasks 3 and 4 — `:root` 5511→5739, `.coverage-card` 4191/5918→4356/6146,
extension regex 1268→~2024). The Sonar line numbers quoted here are from the **2026-07-27** baseline and are
worse still. Task 0's digest refresh is how you get current ones; the named symbol is what you trust.

### Recent history that changes how you read Task 0

`git log` at the baseline, most recent first:

```
07bdb79  Merge branch 'worktree-ci-gate-regen'        <- baseline_commit
db00238  WT
48c050c  Portability: poll through a torn JSON read, the Linux-only transient state
eb5e320  Untrack the accidentally committed agent worktree gitlinks
0b1f561  CI fix: repair the lockfile and regenerate the two stale drift gates
```

**`0b1f561` regenerated both drift-gate baselines** — `web/assets/ir-content.css` (−47/+ lines),
`web/assets/ir-content.manifest.json`, `web/measurements/parity-pinned.json`,
`web/measurements/ir-content-drops.json`, `web/assets/shared-primitives.css`, plus a `pin-parity.mjs` rework and
a `package-lock.json` repair. It landed **1–2 commits before this story's baseline**.

Two consequences:

1. **`check:ir-content` may no longer be red.** 17.4 AC #4 records it as *believed shipping RED under a required
   check* — that belief predates `0b1f561`. Task 0 measures rather than inherits the claim, and now you know why
   the answer may have changed. Do not carry the "believed RED" assertion forward into your record without
   re-measuring it.
2. **Every `ir-content.css` line number in any older record is wrong twice over** — once from ordinary drift,
   once from this regeneration. The Task 3 figures above were re-resolved *after* it.

Also note `eb5e320` ("Untrack the accidentally committed agent worktree gitlinks") — worktree gitlinks have been
committed by accident on this repo before. Check `git status` before committing a sweep that touches many files.

### Concurrent work on shared `main`

CLAUDE.md § *Concurrent work* applies with unusual force to a sweep that touches 15+ files across three languages:

- **Verify after every edit.** Grep for the symbol you just changed before relying on it. A `Charts.cs` edit has
  silently vanished this way before — and `Charts.cs` is in this story's file list.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** Another session's uncommitted work may be in
  the tree. This has already destroyed real work mid-story.
- **Never regenerate a gate baseline reflexively.** If a gate moves and you did not touch rendering, audit the
  harness first — Epic 5 found the harness itself leaking a commit SHA. Bisect into a throwaway tree
  (`git archive HEAD` into the scratchpad, overwrite only your own files), never by resetting the shared tree.
- **Rebuild non-incrementally before trusting anything asset-related.** `specscribe.css`/`.js` are embedded
  resources; an incremental build reuses the cached assembly and never re-embeds a changed asset.
- Expect commits to bundle sibling stories — code review runs at epic end. Record your File List precisely so the
  eventual review can scope by hunk.

### The actual work-list

Sourced from `deferred-work.md` § *Deferred from: 25-2-quality-gate-and-findings-triage (2026-07-27)*, which
`docs/SonarCloudSetup.md:488` names as the routing home for everything scheduled to 17.1. Counts are from the
2026-07-27 analysis and are stale by ~2 weeks.

| # | Item | Volume | Task |
|---|---|---|---|
| 1 | Reliability findings in first-party C# (`S2583` ×5, `S4158` ×2) | 7 | 1 |
| 2 | The repo's only BLOCKER — `S2699` assertion-free test | 1 | 2 |
| 3 | Stylesheet duplicates (`css:S4666` ×7, `css:S4656` ×4, `css:S1874` ×3) | 14 | 3 |
| 4 | `extension/src/extension.ts` (1 bug + 11 smells, 0% coverage) | 12 | 4 |
| 5 | Structural maintainability band in first-party C# | ~300 | 5 |
| 6 | Cross-file SSOT clusters (ToDictionary, footer regex, status routing) | 3 clusters | 5 |

### A record conflict to resolve

`docs/SonarCloudSetup.md` (Story 25.6's 2026-07-29 correction) states: *"The 12-bug reliability sweep is
**unowned**. 10 of the 12 are in `src/SpecScribe/` … Naming it unowned is deliberate — it needs a home before the
gate can be made blocking."*

`deferred-work.md` simultaneously schedules **7 first-party C# reliability bugs to Story 17.1**. Those 7 are
`src/SpecScribe/` bugs and are almost certainly a subset of the "unowned" 12.

Both records cannot be right. Per 17.4 AC #2, *"a cluster whose members disagree is resolved against the code,
not against the older record."* Task 1 resolves it: adjudicate the 7 against the code, then correct whichever
record is stale — and state explicitly whether the remaining 3–5 bugs (the `web/scripts/` `.sort()` bugs at
`check-links.mjs:204` and `ir-content-build.mjs:224`) are inside or outside this story. **Recommendation:** the
`web/scripts/` bugs are *not* AC #2's named scope ("the extension TypeScript shim and the CSS"); leave them and
say so, rather than expanding silently.

### Scope

**In scope:** `src/SpecScribe/**` (C#), `extension/src/extension.ts`, `src/SpecScribe/assets/specscribe.css`,
`web/assets/ir-content.css` (as the mirrored half of a `specscribe.css` fix only), `tests/SpecScribe.Tests/**`.

**Explicitly out of scope — do not absorb these:**

| Not this story | Owner | Why |
|---|---|---|
| The `csharpsquid:S6444` / `S4036` regex-timeout band (157 issues) | **17.2** | `docs/SonarCloudSetup.md:484-485` routes it there by name. It is the whole security-rating story; pulling it here would hide it from the dashboard meant to prove it done. |
| Performance/efficiency work; the `external_roslyn` INFO band (`CA1861`/`CA1859`/`CA1822`) | **17.3** | `SonarCloudSetup.md:487`. |
| The 13 consolidation clusters as a *disposition exercise*; seating unowned candidates; the absent TS test harness | **17.4** | `epics.md` § 17.4 AC #2/#3 own dispositioning clusters and seating candidates. 17.1 **fixes code**; 17.4 **decides what happens to the backlog**. Where a cluster's fix is trivially in front of you (Task 5), fix it and let 17.4 record the closure. |
| `specscribe.css` file-scale / modularization | **17.5** | `deferred-work.md` flags the file-scale question to 17.5 explicitly. Fix the duplicate *declarations*; do not start a layer split. `specscribe.css` is 7,877 lines and `SiteGenerator.cs` is 7,143 — both are 17.5's subject, and 17.5 has not run yet (Q3). |
| `web/**` Nuxt renderer issues (~45) | unassigned | Outside AC #2's named surfaces. Record, do not fix. |
| `FileWatcherServiceTests.BurstOfSaves` flake | **17.4 AC #3** | Named there as time-critical, must resolve before 16.2 lands. Flagged here only because you will likely hit it in Task 0/7 under load. If it fails, re-run in isolation before believing it. |

### Bounding this story

The full unresolved set is 2,999 minutes (~50 h) of Sonar-estimated remediation, and Task 5's band is the bulk of
it. **This story cannot close all ~300 maintainability issues and should not pretend to.** AC #1's real
requirement is "each fix pinned by a test **or an explicit rationale for deferral**" — a bounded sweep with
recorded deferrals satisfies it; an unbounded one that runs out of budget and goes quiet does not.

Suggested bound, in value order: Tasks 1–4 complete (30 concrete items, including the only BLOCKER and the only
bug-plus-zero-coverage file), plus Task 5's three named SSOT clusters and the 9 `S125` dead-code instances. Then
record the `S3776`/`S107`/`S3358` complexity band as deferred with a rationale, and hand its disposition to 17.4.

### Sequencing reality check

`epics.md` states Epic 17 sequences "after Epics 1–15 and 18 (features)". **At `07bdb79`, Epics 1–8 and 10–16 are
still `in-progress`**, as are 22, 23, 24, 25 and 26. Only 9, 18, 19, 20, 21 are `done`. This sweep therefore runs
on a moving codebase, which is exactly why the verify-after-every-edit and establish-causality rules above are
non-negotiable here rather than advisory. It is also why Task 6 asks you to name whose changes your regeneration
sat on top of. Raised as Q4.

### Project Structure Notes

- **Three code surfaces, three toolchains.** C# (`src/SpecScribe`, 155 files / 53k lines; `tests/SpecScribe.Tests`,
  145 files / 58k lines) built with `dotnet build SpecScribe.slnx`; the Nuxt renderer in `web/` (`npm run check`,
  `npm run test` → vitest); the VS Code shim in `extension/` (`npm run typecheck` → `tsc --noEmit`; esbuild bundle;
  **no test runner**).
- **The two stylesheets are derived, not independent.** `web/assets/ir-content.css` (6,363 lines) is extracted
  from `src/SpecScribe/assets/specscribe.css` (7,877 lines) by `npm run extract:ir-content`, which **prunes** any
  rule whose selector names a class or id it cannot find in the IR. Edit the source, then re-extract — never
  hand-edit `ir-content.css` to match.
- **CSS/JS are embedded resources.** Any `--no-incremental` omission means you are measuring a stale asset.
- **CI** (`.github/workflows/build-test-analyze.yml`): `build-test-analyze` on `windows-latest` runs
  `dotnet build --no-incremental` → `dotnet test` (opencover) → `npm ci` → `sync:assets` → `build:package` →
  `generate --deep-git` → `npm run check` → `npm run test:coverage`, wrapped in SonarScanner begin/end.
  `portability-probe` on `ubuntu-latest` is **non-gating** and runs `check:parity` as ADR 0033 §4's cross-OS
  proof — 17.4 AC #4 records that Ubuntu half as *wired but never observed*.
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live` — vestigial and gitignored.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 17: Code Hardening & Release-Readiness Review`] — story text, ACs, epic sequencing; 17.2/17.3/17.4/17.5 boundaries.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#Deferred from: 25-2-quality-gate-and-findings-triage (2026-07-27)`] — the routed work-list (lines ~1240–1300); the "How to read this file" preamble at lines 5–20.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:666, :710, :757`] — the three closed AC #1 examples.
- [Source: `docs/SonarCloudSetup.md#Rule-level decisions`] — rule routing to Epic 17 stories (lines 477–488); the "unowned 12-bug sweep" correction (lines 300–335).
- [Source: `docs/adrs/0035-sonarcloud-quality-gate-and-rule-decision-policy.md`] — gate identity; `extension/src/**` coverage stance (Decision 2); `days: 30` new-code window (Decision 3); `qualitygate.wait` unset (Decision 4); the single home for rule-level decisions (Decision 5).
- [Source: `docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md`] — retirement of `GoldenContentFingerprint`.
- [Source: `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md`] — gate design rules; §4 cross-OS proof.
- [Source: `docs/adrs/0025-retired-is-a-terminal-story-stage-in-both-classifiers.md`] — constrains Task 5's `BmadCommands` classifier change.
- [Source: `CLAUDE.md#Which gate is which`, `#Changing specscribe.css? The regeneration order is load-bearing`, `#Concurrent work on shared main`, `#Verification`] — gate visibility, regeneration order, concurrency rules, live-browser requirement.
- [Source: `.github/workflows/build-test-analyze.yml`] — CI job shape and the `qualitygate.wait` comment.

## Questions for the owner

Saved for after the story, per workflow. None blocks `dev-story`; each has a stated default.

1. **AC #1's three examples are all closed and the gate it names is retired.** Amend `epics.md` to point at the
   live inventory instead? *Default if unanswered:* leave `epics.md` alone; this story's ⚠ note carries the
   correction, and 17.4 can fold it into the burndown record.
2. **AC #2's scope predates Epic 23** and does not name `web/` or `ir-content.css`. This story treats `web/`'s ~45
   issues as out of scope and touches `ir-content.css` only as the mirrored half of a `specscribe.css` fix.
   Confirm, or widen? *Default:* as written above.
3. **17.5 (large-file investigation) has not run**, but `epics.md` § 17.5 AC #2 says its recommendations should
   feed 17.1's Dev Notes so "the hardening epic does not rediscover the same debt". Running 17.1 first means the
   `specscribe.css` (7,877 lines) and `SiteGenerator.cs` (7,143 lines) structural questions get answered twice.
   Run 17.5 first? *Default:* proceed with 17.1 bounded to duplicate *declarations*, leaving file-scale to 17.5.
4. **Epic 17 was sequenced after Epics 1–15/18, but 15 epics are still `in-progress`.** Accept running the sweep
   on a moving codebase? *Default:* yes, with the concurrency discipline above — the work is remediation of
   already-landed code and deferring it further just grows the band.

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (1M context) — `claude-opus-5[1m]`.

### Debug Log References

Implemented in worktree `.claude/worktrees/story-17-1-dev` (branch `worktree-story-17-1-dev`), **starting from
`15336f4`**, not the story's create-time `07bdb79` — 36 commits of sibling work had landed in between, which is
why every citation was re-resolved by symbol. The frontmatter `baseline_commit` records the dev-start commit; the
prose line near the top of this file records the create-story baseline, and the two are deliberately different.

**Concurrent-work attribution (CLAUDE.md § *Concurrent work*).** The regenerations in this story sit on top of
`15336f4` (`Merge branch 'worktree-code-review-23-2-fourth-pass'`) and, materially, on `0b1f561`'s earlier
regeneration of both drift-gate baselines. Nothing in this story required bisecting a moved gate: every gate was
measured green *before* the first edit, so each later movement had an established cause.

**Task 0 baseline, measured at `15336f4` before any edit:**

| signal | value at HEAD |
|---|---|
| analysis digest | 1,755 observations; `evaluatedAtRevision` **`01acf5b`**, tree `15336f4`, **36 commits behind**, `isStale: true` |
| `dotnet test SpecScribe.slnx` | **2,991 passed / 0 failed / 3 skipped** (2,994 total) |
| `generate --deep-git` | 809 pages, **errors=0** |
| `check:ir-content` | **GREEN** |
| `check:tokens` | GREEN — "45 tokens across **2** `:root` block(s)" |
| `check:assets` | GREEN |
| `check:parity` | GREEN — 24 routes / 14 families byte-identical |
| `npm run test` (vitest) | GREEN — 183/183 |

**`check:ir-content` is GREEN at HEAD — 17.4 AC #4's "believed RED" is stale and should not be carried forward.**
Measured through the full load-bearing order (with `--deep-git`, per `web/CONVENTIONS.md` §10 — the story's own
snippet omits that flag, and without it extraction prunes deep-analytics rules). The belief predates `0b1f561`,
which regenerated the baseline; that is why the answer changed.

**Final regression, all green:** `dotnet test` **3,000 passed / 0 failed / 3 skipped** (+9 vs baseline);
`npm run check` all four gates OK; vitest 183/183; `generate --deep-git` 809 pages, errors=0. Regenerated
baselines confirmed **stable across two consecutive extractions** (identical md5 for `tokens.css`,
`ir-content.css`, `ir-content.manifest.json`).

### Completion Notes List

**Headline: the story's own premises did not survive contact with the code, and the corrections are the
deliverable as much as the fixes are.** Four of them:

1. **All 7 reliability findings (Task 1) are FALSE POSITIVES of one class** — a collection or counter mutated
   *only inside a lambda or local function*, which Sonar's dataflow does not model. Not two of five as the story
   predicted: all seven. Two are not even in the region the record names (the `SiteGenerator` `S2583` pair is in
   `TryCountCodeLines`, not the commit-detail loop). Deleting any of these conditions removes a live guard —
   **demonstrated**: temporarily dropping `matched == 0` from `CapabilityStyler` makes the new test fail and the
   styler replaces a real section with an empty `.capabilities` div. Disposition: won't-fix per issue (ADR 0035
   § Decision 5), with the one previously-untested guard now pinned.
2. **The BLOCKER test was assertion-free because Story 20.7 deleted its subject.** `ChartsTests`' own header
   records that the centre `<text>` assertions went with `Charts.Sunburst`. The *fact* survived the engine
   change — the centre count is now the root node's detail sentence in `HierarchyExplorer.WithDetails` — so it
   was restored against that, and **confirmed it can fail** by breaking pluralization on purpose.
3. **`.coverage-card` is not a duplicate — it is two components colliding on one class name, and the shipped
   layout depends on the collision.** Verified in a live browser: a block-2 card computes `flex-direction:
   column` sourced from block 1 alone, and block-1 cards compute `max-width: 460px` / `flex: 1 1 320px` /
   `align-items: flex-start` sourced from block 2. Merging or scoping it changes 120 elements across 100+ pages.
   Deferred to Epic 27 with that diagnosis rather than merged blind — which is exactly what this story's own
   Task 3 bullet told me to check for.
4. **17 of the 18 `S125` "commented-out code" findings are prose comments the rule misreads**, a systematic
   consequence of this codebase's comment-dense house style; the 18th is a deliberately quoted removed-guard kept
   as documentation. So AC #1's "dead or unreachable code" clause is **closed for C# by adjudication, not by
   deletion**. The real dead code this sweep found was in the stylesheet.

**What was actually fixed.** CSS: `:root` merged to a single top-level block (45 tokens unchanged, proven by
`check:tokens` reporting the same 45 across 1 block); `.now-next-card.active` merged; the pre-rename
`.impact-shape-*` toggle block **deleted as dead code** (confirmed three independent ways — no emitter, no
occurrence in any generated page but prose, and already pruned by `extract:ir-content`); dead `border: 0` /
`padding: 0` resets removed from `.code-tablist` / `.ss-tablist`; both deprecated `word-break: break-word` →
`overflow-wrap`. C# single-source-of-truth: **11 epic/AC-number lookups converged onto one first-wins policy**
(new `NumberIndex.ByFirst`) — the codebase disagreed with itself, ten throwing on a duplicate epic number and one
already working around it; the triplicated footer-clock regex consolidated onto `GoldenNormalization`;
`BmadCommands.ForStory` routed through `StatusStyles` instead of raw substring matching, which also fixed an
**ADR 0025 violation surviving in a third classifier** (the six retirement words fell through to "create-story"
on the id that had been retired). Extension: the `S5850` regex bug fixed with its anchoring asymmetry documented
(`sh` must stay end-anchored or it matches "Power**sh**ell"), plus `S2681` ×2, `S7780`, `S7781` ×2. Drift: the
stale "7,041-line" figure removed at its three sources rather than re-pinned to a number that would go stale
again.

**Deliberate non-fixes, each recorded.** `.coverage-card` → Epic 27 (above). `S3776`/`S107`/`S3358`/`S3267` and
the `S1192` band → deferred to 17.4 with a rationale: ~50 h of the estimate, and complexity reduction is a
per-method redesign with its own correctness risk, not the mechanical consistency work this story is bounded to.
`S7778` ×2 in the extension → won't-fix; collapsing three commented `context.subscriptions.push` registrations
into one call would strand the comments. `web/scripts/` `.sort()` bugs → left outside scope and **said so**,
per the story's own recommendation.

**Two gate findings worth the reader's attention.** (a) `web/assets/ir-content.manifest.json` carried a
`generatedBytes` **already 12 bytes stale on `main`** before this story touched anything (committed 186,492;
actual file 186,504) — inherited from `0b1f561`, invisible to `check:ir-content` because that gate compares rules,
not byte counts. Corrected here and filed as a gate blind spot. (b) `web/test/tokens-lib.test.mjs` asserted the
*real* stylesheet has `> 1` `:root` block, so a legitimate source edit turned two tests red while the extractor
was entirely correct; re-pointed at the invariant (every declared token is carried; emitted block count **equals**
source block count), which is strictly stronger and survives future edits.

**Honest reporting of AC #1's second clause.** `check:parity` is structurally blind to the C#-side changes in
Tasks 1/5 (frozen corpus IR), so it is **not** cited as evidence they preserved rendered output. The evidence
used instead is unit tests over the changed regions plus **live-browser computed-style inspection** via CDP —
which is what confirmed the `.coverage-card` diagnosis, the `:root` token resolution, the `.now-next-card.active`
merge (accent, 4px width and gradient all intact) and the tablist chrome (`1px solid`, `4px`, radius `10px`).

**Owner questions Q1–Q4 were left at their stated defaults**; none blocked the work. Q1 is now better supported:
`epics.md`'s AC #1 examples are stale *and* its safety clause names a retired gate, so amending it would help the
next reader.

### File List

- `src/SpecScribe/NumberIndex.cs` *(new)* — first-wins epic/AC-number indexing, the one policy the 11 call sites share
- `src/SpecScribe/BmadCommands.cs` — `ForStory` routed through `StatusStyles`; `retired` made terminal
- `src/SpecScribe/Charts.cs` — 2 `ToDictionary` → `ByFirst`
- `src/SpecScribe/EpicsParser.cs` — 1 `ToDictionary` → `ByFirst`
- `src/SpecScribe/EpicsViewBuilder.cs` — 1 `ToDictionary` → `ByFirst`
- `src/SpecScribe/RelatedWorkCards.cs` — 1 `ToDictionary` → `ByFirst`
- `src/SpecScribe/RequirementsParser.cs` — 2 `ToDictionary` → `ByFirst`
- `src/SpecScribe/RequirementsTemplater.cs` — `EpicsByNumberFirstWins` converged onto the shared helper
- `src/SpecScribe/SiteGenerator.cs` — 3 `ToDictionary` → `ByFirst` (2 epic-progress, 1 AC-number)
- `src/SpecScribe/assets/specscribe.css` — `:root` merge, `.now-next-card.active` merge, dead `.impact-shape-*` block deleted, dead `border`/`padding` resets removed, 2 × `word-break` → `overflow-wrap`
- `extension/src/extension.ts` — `S5850` regex grouping + anchoring documented, `S2681` ×2, `S7780`, `S7781` ×2
- `tests/SpecScribe.Tests/ChartsTests.cs` — restored the `S2699` BLOCKER's assertions against the live root-detail sentence
- `tests/SpecScribe.Tests/CapabilityStylerTests.cs` — new test pinning the `matched == 0` guard
- `tests/SpecScribe.Tests/EpicsParserTests.cs` — new duplicate-epic-number tests (parser + downstream builder + policy)
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — new `BmadCommands`/`StatusStyles` agreement + retirement-terminal tests
- `tests/SpecScribe.Tests/GoldenNormalization.cs` — shared `StripFooterClock`; stale `GoldenContentFingerprint` reference corrected
- `tests/SpecScribe.Tests/TestArtifactDiscoveryTests.cs` — second `FooterClock` transcription removed
- `tests/SpecScribe.Tests/SiteGeneratorStatusStylesTests.cs` — third `StripFooterClock` transcription removed
- `web/scripts/ir-content-build.mjs` — stale "7,041-line" figure removed from the emitted header
- `web/scripts/ir-content-lib.mjs` — same figure removed from prose
- `web/scripts/sync-runtime-assets.mjs` — same figure removed from prose
- `web/test/tokens-lib.test.mjs` — two tests re-pointed from the sheet's block COUNT to the carried-token invariant
- `web/assets/tokens.css` *(generated)* — regenerated after the `:root` merge; 45 tokens across 1 block
- `web/assets/ir-content.css` *(generated)* — re-extracted
- `web/assets/ir-content.manifest.json` *(generated)* — re-extracted; corrects an inherited stale `generatedBytes`
- `web/measurements/ir-content-drops.json` *(generated)* — re-extracted
- `_bmad-output/implementation-artifacts/deferred-work.md` — 5 entries amended with resolutions; new 17.1 section with 3 new findings
- `docs/SonarCloudSetup.md` — "unowned 12-bug sweep" conflict resolved; `S125` and `S2583`/`S4158` rule-level decisions added
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status → review

## Change Log

| Date | Change |
|---|---|
| 2026-08-07 | Story 17.1 implemented at baseline `15336f4`. Adjudicated all 7 C# reliability findings as false positives of one class (lambda/local-function side effects) with won't-fix rationales and a new guard test; restored the repository's only BLOCKER test against the live successor of the deleted centre `<text>`; fixed 4 of 5 stylesheet duplicate/deprecated findings and deleted a dead pre-rename CSS block, deferring `.coverage-card` to Epic 27 with a live-browser diagnosis that it is two components colliding rather than a duplicate; converged 11 epic/AC-number lookups, a triplicated footer-clock regex and the story-status classifier onto single sources (the last also closing an ADR 0025 violation); fixed the extension's `S5850` regex bug plus 5 smells; removed a stale generated line-count figure at its three sources. Adjudicated all 18 `S125` findings as false positives and deferred the ~300-issue complexity band to 17.4 with a rationale. Reconciled the `SonarCloudSetup.md` / `deferred-work.md` ownership conflict. Tests 2,991 → 3,000 passing, 0 failing; all four web gates and vitest green; regenerated baselines stable across two runs. |
