# Story 17.1: Structural and Consistency Remediation Sweep

Status: ready-for-dev

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

- [ ] **Task 0 — Establish the baseline before changing anything (AC: #1, #2)**
  - [ ] Regenerate the analysis digest: `node tools/analysis-digest/index.mjs`. **`.specscribe/analysis/` does not exist at `07bdb79`** — per CLAUDE.md that is UNKNOWN, never clean. Every Sonar count in this story is quoted from the 2026-07-27 baseline triage and is ~2 weeks stale; the digest is how you get current numbers.
  - [ ] Record the digest's `provenance.evaluatedAtRevision` and compare to `git rev-parse HEAD`. If they differ, the digest is stale regardless of `isStale`.
  - [ ] Run the full C# suite and record the pass count as the pre-sweep baseline: `dotnet test SpecScribe.slnx`.
  - [ ] **Measure `check:ir-content` through the complete load-bearing order** (17.4 AC #4 records it as *believed RED* but unmeasured — and a partial run gives a confidently wrong answer either way):
    ```sh
    dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental
    dotnet run --project src/SpecScribe -- generate
    cd web && npm run extract:ir-content && npm run check:ir-content
    ```
  - [ ] Run `cd web && npm run check` (tokens + ir-content + assets + parity) and `npm run test` and record red/green **per gate**. If a gate is already red at HEAD, say so in the Dev Agent Record before you touch a line — otherwise you cannot distinguish your breakage from the inherited state, and CLAUDE.md forbids regenerating a baseline without first establishing causality.
  - [ ] If `check:ir-content` is red at HEAD, **stop and report** rather than remediating CSS on top of it (Task 3 depends on this gate being a trustworthy signal).

- [ ] **Task 1 — Adjudicate the 7 first-party C# reliability findings (AC: #1)**
  These are the highest-value items: an always-False condition guarding a render branch is a defect class this
  project has shipped and caught only in a live browser. **Do not assume they are all real** — see the
  false-positive warning in Dev Notes.
  - [ ] `csharpsquid:S2583` (condition always evaluates to a constant, code path therefore unreachable) at 5 sites — resolve each **by symbol**, not by the drifted line number:
    - [ ] `SiteGenerator.MutateSourceInventory` region (cited `SiteGenerator.cs:1392`, "always True")
    - [ ] `SiteGenerator` commit-detail page loop, `pages.TryAdd` / duplicate-hash region (cited `:2441` and `:2448`, "always False")
    - [ ] `WorkGraph` — the `if (edges.Count == 0) return null;` guard at the end of the epic-graph builder (cited `WorkGraph.cs:403`, "always True")
    - [ ] `CapabilityStyler` — the `if (matched == 0 || leftover.Contains("<li", …))` bail-out after `CapItem.Replace` (cited `CapabilityStyler.cs:57`, "always True")
  - [ ] `csharpsquid:S4158` (collection known to be empty where used) at 2 sites: `SiteGenerator.cs:1939`, `HtmlRenderAdapter.Dashboard.cs:235`.
  - [ ] For each: either **fix + pin with a regression test**, or record an explicit **won't-fix rationale** in `deferred-work.md` naming why the analyser is wrong. AC #1's "a test or an explicit rationale for deferral" is satisfied by either — but not by silence.
  - [ ] Reconcile the record conflict this exposes (see § *A record conflict to resolve*): `docs/SonarCloudSetup.md` calls the 12-bug reliability sweep **"unowned"**, while `deferred-work.md` schedules 7 of those bugs to **this story**. Resolve against the code and correct whichever record is wrong.

- [ ] **Task 2 — Kill the repository's only BLOCKER: a test that cannot fail (AC: #1)**
  - [ ] `tests/SpecScribe.Tests/ChartsTests.cs` → `Sunburst_CenterReportsEpicCountNotStoryCount` (line 338 at `07bdb79`). **Verified assertion-free at HEAD:** it builds `multiSvg` and `singleSvg` and then ends. Its own comment states the intent — *"the center headlines the epic count with an 'epic(s)' label (pluralized), never the story total"* — so write the assertions that comment already specifies: `multiSvg` asserts "2 epics", `singleSvg` asserts the singular form, and neither asserts the story total.
  - [ ] Confirm the restored test actually **fails** if the pluralization/center-label behavior is broken (a test added without checking it can fail reproduces the defect in a new shape).
  - [ ] A repo-wide scan at `07bdb79` found 14 other `[Fact]`/`[Theory]` bodies with no literal `Assert.` — **all 14 were checked and delegate to helper methods that assert internally** (e.g. `AssertPlotlyDataContract`, `HierarchyRolloutTests.cs`). They are not defects; do not "fix" them. This matches Sonar finding exactly 1 `S2699`.

- [ ] **Task 3 — Stylesheet duplicate declarations (AC: #1, #2)** — blocked on Task 0's `check:ir-content` result.
  - [ ] All four cited duplicates **re-verified live at `07bdb79`, and every cited line number had moved** — this is the concrete proof of the drift rule:

    | Selector | `deferred-work.md` cited | Actual at `07bdb79` |
    |---|---|---|
    | `:root` re-opened | 6 / 5511 | **6 / 5739** |
    | `.coverage-card` | 4191 / 5918 | **4356 / 6146** |
    | `.now-next-card.active` | 3714 / 3723 | **3875 / 3884** |
    | `.impact-shape-tabs` | 5549 / 5580 | **5777 / 5808** |

  - [ ] `:root` re-opened at `specscribe.css:5739` having been declared at line 6 — **this is the priority one.** It is the exact class of defect this project has already shipped invisibly once and caught only by reading computed styles in a live browser. Verify the fix in a live browser, not in review.
  - [ ] `.coverage-card` duplicated (4356 / 6146) — note this touches the vocabulary collision Epic 27 is already tracking; check the two blocks are not two *different* concepts sharing a name before merging them.
  - [ ] Remaining: duplicate `border` at ~1596/1964, duplicate `padding` at ~1598, and 3 `css:S1874` deprecated `word-break: break-word` keywords.
  - [ ] `web/assets/ir-content.css` carries **mirrored copies** — re-verified at `07bdb79` **after** commit `0b1f561` regenerated this file: `.now-next-card.active` at **3111 / 3123** and `.coverage-card` at **3573 / ~5151** (cited at 2272/3364/3930 — moved twice now). The IR stylesheet **inherited** them from `specscribe.css`.
    - `ir-content.css` is a **GENERATED FILE — DO NOT EDIT**. Fix `specscribe.css` and re-extract; hand-editing it is reverted by the next `extract:ir-content`.
    - Every rule there is re-nested under `.ir-content`, so grep unanchored (`.ir-content .coverage-card`), not for `^.coverage-card`.
    - Its header comment claims specscribe.css is "7,041-line" — it is **7,877** lines at `07bdb79`. That stale generated figure is itself AC #1 "naming/token drift"; fix it in whatever emits the header, not in the generated output.
  - [ ] **Follow CLAUDE.md's regeneration order exactly** — two `generate` calls, deliberately. Skipping either leaves you inspecting a page whose CSS predates your edit, and the failure looks exactly like "my selector is wrong":
    ```sh
    dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental
    dotnet run --project src/SpecScribe -- generate
    cd web && npm run extract:ir-content && npm run check:ir-content
    cd web && npm run build:package
    dotnet run --project src/SpecScribe -- generate
    ```

- [ ] **Task 4 — Extension TypeScript shim (AC: #2)**
  `extension/src/extension.ts` — 2,398 lines, 923 ncloc, 12 unresolved issues, **0.0% coverage**. The only
  first-party source file in the project with both a bug and zero test coverage.
  - [ ] The bug: `typescript:S5850` — regex alternation whose operator precedence is not explicit. **Cited at `:1268`; that line is now unrelated code.** Re-resolved by symbol at `07bdb79`: the terminal-profile matcher, `/git bash|bash|wsl|sh$/i` (~line 2024). The `$` anchor binds only to the final `sh` alternative, so `git bash`/`bash`/`wsl` match anywhere in the string while `sh` must be terminal — almost certainly not the intent. Make the grouping explicit and decide deliberately which alternatives anchor.
  - [ ] The remaining 11: `typescript:S6571`, `S6551`, `S6671`, `S7778`, `S7780`, `S7781`.
  - [ ] **Read § *Touching the extension has a quality-gate cost* before starting** — there is no TS test harness at all (no `test` script, no test runner in `devDependencies`, no test files), so you cannot pin an extension fix with a test today without first standing one up. That harness is explicitly **not** this story's (see § *Scope*). Record which route you took.
  - [ ] Re-run `npm run typecheck` in `extension/` after each change — it is the only automated signal this file has.

- [ ] **Task 5 — Single-source-of-truth and duplication clusters in the C# core (AC: #1, #2)**
  Verified present at `07bdb79`:
  - [ ] **Unguarded `ToDictionary` on epic/AC numbers — 11 sites, and the codebase already disagrees with itself.** `RequirementsTemplater.cs:682` guards with `GroupBy(e => e.Number).ToDictionary(g => g.Key, g => g.First())`; the other 10 call `ToDictionary(e => e.Number)` bare and throw on a duplicate epic number in a user's `epics.md`. Sites: `Charts.cs:3137`, `Charts.cs:3357`, `EpicsParser.cs:60`, `EpicsViewBuilder.cs:65`, `RelatedWorkCards.cs:98`, `RequirementsParser.cs:55`, `RequirementsParser.cs:307`, `SiteGenerator.cs:3432`, `SiteGenerator.cs:3601`, `SiteGenerator.cs:3769`. **Pick one policy and apply it everywhere** — that is the single-source-of-truth violation, not the crash. Pin with a test that feeds a duplicate epic number through the parser.
  - [ ] **Duplicated footer-strip regex.** `FooterClock` is declared independently in `tests/SpecScribe.Tests/GoldenNormalization.cs:26` and `tests/SpecScribe.Tests/TestArtifactDiscoveryTests.cs:612`, and `SiteGeneratorStatusStylesTests.cs:114` hand-rolls a third `StripFooterClock` local. Consolidate onto `GoldenNormalization`.
  - [ ] **`BmadCommands` next-step classifiers route on raw status strings.** `BmadCommands.cs:505` and `:515` use `status.Contains("review")` / `status.Contains("done") || status.Contains("complete")` while the same file elsewhere routes correctly through `StatusStyles.ForStory(story)` (lines 42, 69, 105, 627). Substring matching on a free-text status is the bug shape — `"review"` matches `"code-review-blocked"`. Route through `StatusStyles`. **Check ADR 0025** (`retired` is a terminal stage in *both* classifiers) before changing classifier behavior.
  - [ ] The `~300`-issue maintainability band (94 `S1192` duplicated string literals, 86 `S3776` cognitive complexity, 48 `S3358` nested ternaries, 29 `S3267`, 28 `S107`, 9 `S125` commented-out code, plus an `S2589`/`S1121`/`S127`/`S1066`/`S1172` tail). **This is not a to-do list — it is 2,999 minutes (~50 h) of Sonar-estimated effort and it will not fit in one story.** Take the `S125` (dead/commented-out code, 9 instances — directly AC #1's "dead or unreachable code") and the `S1192` instances that are genuine single-source-of-truth violations; explicitly defer the `S3776`/`S107` complexity band with a recorded rationale, and say so rather than leaving it looking swept. See § *Bounding this story*.

- [ ] **Task 6 — Record every decision (AC: #2)**
  - [ ] For each item touched: fix it **or** carry it forward with a recorded decision in `deferred-work.md`. AC #2 admits no third state.
  - [ ] Strike through closed items in place with the resolution — never delete (the file's own "How to read this file" preamble makes the audit trail load-bearing, and `DeferredWorkParser` renders it into the portal).
  - [ ] Update the stale claims this story disproves: the three closed AC #1 examples, the `GoldenContentFingerprint` blocker language, and the `SonarCloudSetup.md` "unowned" 12-bug claim (Task 1).
  - [ ] Note in the story record whose concurrent changes your regeneration sat on top of (CLAUDE.md § *Concurrent work*).

- [ ] **Task 7 — Prove nothing regressed (AC: #1, #2)**
  - [ ] `dotnet build SpecScribe.slnx --no-incremental` then `dotnet test SpecScribe.slnx` — compare to Task 0's baseline count.
  - [ ] `cd web && npm run check && npm run test`.
  - [ ] **Live-browser verification for every CSS change** — the suite structurally cannot see containment leaks, sub-pixel collapse, or DOM corruption. Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.
  - [ ] Confirm any regenerated gate baseline is **stable across two repeated runs** before locking it in.

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

### Debug Log References

### Completion Notes List

### File List
