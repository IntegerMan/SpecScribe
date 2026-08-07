---
baseline_commit: 755bd7a8d1679594dc48bb04fe5ac11473484618
---

# Story 24.1: Directional Coupling Metric Foundation (Confidence, Support, Lift, Cross-Boundary) + Upgraded List

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer inspecting a file's relationships,
I want the "changes with" data expressed as directional coupling strength rather than a raw shared-commit count,
so that I can read "when I touch this file, I usually touch X" instead of an unnormalized, symmetric tally that makes always-churning files look coupled to everything.

## Acceptance Criteria

1. **Given** the existing deep-git parse (`DeepGitPulse.CoChangePairs` + per-file `ChangeCount`)
   **When** coupling is computed
   **Then** each directed pair carries **confidence(A→B) = coChange(A,B) / ChangeCount[A]** (asymmetric — A→B and B→A may differ), **support** = shared-commit count with a configurable minimum-support floor that filters coincidental couples, and **lift** = confidence(A→B) ÷ (ChangeCount[B] ÷ analyzed-commits) so a file that changes every commit self-demotes
   **And** all three are derived from the SAME single `--deep-git` numstat parse with no additional git invocation, and the existing Code-vs-Process (`ClassifyCoupling`) noise classification is preserved.

2. **Given** a coupled pair whose two files live in different top-level directories/modules
   **When** coupling is surfaced
   **Then** the pair is flagged as **cross-boundary ("surprising") coupling** (higher architectural signal), distinct from same-directory coupling, using only the file paths already in hand
   **And** this classification is available to every downstream surface (list and graphs) as a shared property, not recomputed per view.

3. **Given** the per-file "Coupled files" list (Story 7.4 `FileInsight.CoupledFiles`) and the Git Insights hub coupling view (Story 3.8)
   **When** they render with the new metric
   **Then** each entry shows the directional confidence (e.g. "changes with **X** — 80%") and a one-sentence framing per Story 10.2, sorted by confidence (or lift) with the support floor applied, and cross-boundary couples visibly marked
   **And** the list remains fully readable and navigable without JavaScript — it is the canonical accessible text-twin the graph stories (24.2–24.5) reuse rather than replace.

## Tasks / Subtasks

- [x] **Task 1 — Cross-boundary classifier (pure, shared)** (AC: #2)
  - [x] Add `public static bool IsCrossBoundary(string pathA, string pathB)` to `GitMetrics` (near `ClassifyCoupling`, [GitMetrics.cs:271](src/SpecScribe/GitMetrics.cs)). Compare the **first path segment** (top-level directory) after normalizing `\`→`/` and splitting on `/`. Two files under the same top-level dir → same-boundary; different top-level dirs → cross-boundary. Decide root-level handling per **Q2** (recommended: a root-level file — no directory — shares a boundary with other root-level files, and is cross-boundary vs any nested file).
  - [x] Pure and repo-free (no SpecScribe path literals, NFR8), never throws, deterministic.
  - [x] Unit tests in `GitMetricsTests.cs` (or a new `GitMetricsCouplingTests.cs`): same-dir, cross-dir, root-vs-nested, root-vs-root, empty-path guards.

- [x] **Task 2 — Directional metric model (the shared spine)** (AC: #1, #2)
  - [x] Introduce a record `public sealed record CoupledFile(string Path, int Support, double Confidence, double? Lift, bool CrossBoundary, GitMetrics.CouplingKind Kind)` in `GitMetrics.cs` (beside `FileInsight`, [GitMetrics.cs:169](src/SpecScribe/GitMetrics.cs)). `Support` = the shared-commit count (today's `CoChanges`). `Lift` is nullable — undefined when `ChangeCount[B]` or `AnalyzedCommits` is 0 (guard divide-by-zero; render as "—"/omit, never `NaN`/`Infinity`).
  - [x] Change `FileInsight.CoupledFiles` from `IReadOnlyList<(string Path, int CoChanges)>` to `IReadOnlyList<CoupledFile>` ([GitMetrics.cs:172](src/SpecScribe/GitMetrics.cs)). This is the load-bearing shape all downstream surfaces read; every consumer below must be updated in this story (regression guardrail — see Task 6).
  - [x] Compute the directional fields inside `BuildFileInsights` where the pairs are already fanned out to both members ([GitMetrics.cs:858-888](src/SpecScribe/GitMetrics.cs)). In that loop **both** `a`, `b` and their `FileInsightAccum.ChangeCount` are in hand, so directional confidence is computed with the CORRECT numerator per direction: file A's list entry for B carries `confidence = count / ChangeCount[A]`; file B's entry for A carries `count / ChangeCount[B]`. `commits.Count` (== `AnalyzedCommits`) is available in `BuildFileInsights` for lift's denominator.
  - [x] Keep the sort **within** the cap as confidence-desc, then Support-desc, then ordinal path (was `CoChanges`-desc). Apply the min-support floor (Task 4) BEFORE the `coupledCap` take so low-support noise never crowds out real couples. Preserve `FileInsightCoupledCap` ([GitMetrics.cs:764](src/SpecScribe/GitMetrics.cs)).
  - [x] Preserve `ClassifyCoupling` — set `CoupledFile.Kind` from it; do not alter the Code-vs-Process rules.

- [x] **Task 3 — Whole-repo directional view for the hub** (AC: #1, #3)
  - [x] The hub's top-N `Coupling` list (`DeepGitPulse.Coupling`, built in `ParseNumstatLog` [GitMetrics.cs:541-548](src/SpecScribe/GitMetrics.cs)) is today symmetric `(FileA, FileB, int CoChanges)` sorted by shared-commit count. Add a directional projection the hub table/graph consume. Recommended: a new `public static IReadOnlyList<CoupledFile-with-source>` shaped list keyed by a source file, OR a sibling record `DirectedCouple(string FromPath, string ToPath, int Support, double Confidence, double? Lift, bool CrossBoundary, CouplingKind Kind)` computed from `CoChangePairs` + per-file `ChangeCount` (available via the `changeCounts` dict in `ParseNumstatLog`, [GitMetrics.cs:497](src/SpecScribe/GitMetrics.cs)) + `AnalyzedCommits`. See **Q1** for the directed-vs-symmetric decision.
  - [x] Reuse the SAME min-support floor const (Task 4) and confidence sort. Surface it on `DeepGitPulse` (a new `init` property, mirroring how `CoChangePairs` was added at [GitMetrics.cs:82](src/SpecScribe/GitMetrics.cs)) so it is computed once and reused, not recomputed per view (AC #2).
  - [x] Do NOT add a second git call or second commit scan — derive entirely from already-parsed records/maps (one fetch, one parse, several views).

- [x] **Task 4 — Configurable minimum-support floor** (AC: #1)
  - [x] Introduce a named const (e.g. `CouplingMinSupport = 2`) replacing the hard-coded `kv.Value >= 2` at [GitMetrics.cs:542](src/SpecScribe/GitMetrics.cs). Thread it through both the hub directional list (Task 3) and the per-file `BuildFileInsights` filter (Task 2) so the two surfaces agree, exactly as `CouplingFileSetCap` is shared today.
  - [x] Keep the default at **2** so the baseline output is unchanged except for the new metric columns/sort (see golden-fingerprint note). "Configurable" = a parameter/const with a sensible default, not necessarily a new CLI flag — confirm scope in **Q3**.

- [x] **Task 5 — Upgrade the two render surfaces (the "upgraded list")** (AC: #3)
  - [x] **Per-file text-twin** (`CodeFileTemplater.BuildRelatedNodes` + sr-only related list, [CodeFileTemplater.cs:261-272](src/SpecScribe/CodeFileTemplater.cs) and [CodeFileTemplater.cs:491-505](src/SpecScribe/CodeFileTemplater.cs)): each entry shows directional confidence ("changed together N× · confidence M%") and a **text** cross-boundary marker (never color-only — UX-DR19/NFR8), e.g. append " · cross-boundary". Lift belongs in the `<title>`/tooltip. This sr-only list is the accessible text-twin the graph stories reuse — keep it complete and readable with JS off. Do NOT redesign the visible code-page surface (the reference graph is 24.2's job); 24.1 upgrades the metric + text list only.
  - [x] **Hub coupling table** (`Charts.CouplingTable`, [Charts.cs:2090](src/SpecScribe/Charts.cs)): add a **Confidence** column (directional %), sort rows by confidence with the support floor applied, and add a cross-boundary text marker/badge alongside the existing "Process" Kind badge ([Charts.cs:2100-2114](src/SpecScribe/Charts.cs)). Keep the process-vs-code badge behavior intact.
  - [x] **Hub coupling graph legend** (`DeepAnalyticsTemplater`, [DeepAnalyticsTemplater.cs:60-77](src/SpecScribe/DeepAnalyticsTemplater.cs)): update the legend copy to explain the new edge/weight semantics if edges now encode confidence; the `CouplingGraph` SVG itself ([Charts.cs:2128](src/SpecScribe/Charts.cs)) may stay weight-by-shared-commits in 24.1 (interactive/confidence-weighted graph is 24.2+) — confirm in **Q1**. The `role="img"` aria label and `<title>` tooltips must stay truthful to whatever they encode.
  - [x] **Framing (Story 10.2)**: the `ChartMetric.ChangeCoupling` `WhyText` sentence already exists ([Charts.cs:58](src/SpecScribe/Charts.cs)); reuse it — do NOT hand-roll new "why" copy at call sites. If confidence changes what the ranking caption should say, update the `ChartMeta.Ranking` string in `DeepAnalyticsTemplater` ([DeepAnalyticsTemplater.cs:89-91](src/SpecScribe/DeepAnalyticsTemplater.cs)), not the shared `WhyText`.

- [x] **Task 6 — Update every `CoupledFiles` consumer (no regressions)** (AC: #3)
  - [x] `CodeFileTemplater.cs`: `BuildRelatedNodes` destructure `foreach (var (path, coChanges) in insight.CoupledFiles)` ([CodeFileTemplater.cs:269](src/SpecScribe/CodeFileTemplater.cs)) → read `CoupledFile.Path`/`.Support`/`.Confidence`/`.CrossBoundary`; the `related` node tuple + its consumer at [CodeFileTemplater.cs:495](src/SpecScribe/CodeFileTemplater.cs).
  - [x] `SiteGenerator.cs`: `BuildStoryRelatedEdges`/`BuildRelatedRelatedEdges` read `insight.CoupledFiles[j].Path` and `.CoChanges` ([SiteGenerator.cs:1966-2004](src/SpecScribe/SiteGenerator.cs)) → `.Path`/`.Support`. These index-align with the reference-graph related nodes ([SiteGenerator.cs:1960](src/SpecScribe/SiteGenerator.cs) comment) — keep the ordering contract intact after the sort change.
  - [x] `Charts.ReferenceGraph` related-node title ("changed together N times", [Charts.cs:2509](src/SpecScribe/Charts.cs)) stays valid (it reads the passed related tuple, not `CoupledFiles` directly) — verify no signature drift.
  - [x] Grep the whole `src/` + `tests/` for `CoupledFiles` / `.CoChanges` before finishing; every read site must compile against the new record.

- [x] **Task 7 — Tests + golden fingerprint** (AC: #1, #2, #3)
  - [x] `GitMetricsFileInsightsTests.cs`: assert confidence = count/ChangeCount[focal] with the correct direction (build a fixture where A→B ≠ B→A), lift math + divide-by-zero → null, min-support floor filters a support-1 couple, cross-boundary flag, `Kind` preserved.
  - [x] `ChartsTests.cs` (+ `SiteGeneratorCodeInsightsTests.cs`): `CouplingTable` renders the confidence column + cross-boundary marker + confidence sort; the per-file sr-only list carries confidence + cross-boundary text; empty/degenerate inputs still render the friendly empty state ([Charts.cs:2092](src/SpecScribe/Charts.cs)).
  - [x] Run the full suite. The golden fingerprint **WILL move** (coupling list text + sort change); regenerate it deliberately and confirm the move is only the intended coupling copy/order — see [[golden-diff-normalization-gotchas]]. RenderParity/SPA/webview: the coupled list lives inside code pages + the hub, both already captured by existing coherence tests — extend them, don't add a new page.

### Review Findings

Code review 2026-08-07 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Verified against
HEAD `6de2890`; landing commit `82880ba`. Scoped by this story's File List with **attribution by hunk**
per CLAUDE.md § Scoping a code review — the 48-commit range from `baseline_commit` bundles many sibling
stories. Excluded and handed off: Story 23.4's `PageView`/`BeginShell` conversion, Story 8.9's
retired-status CSS, Story 20.9's `revealPanelsNamedByHash` JS + `OwnershipTopAuthorsLegend` doc patch,
and Story 24.2's post-landing `coupledCap`/`RelationshipGraphCoupledCap`/`RelatedDetail` work in the
same four files. 39 raw findings deduped to 25; 5 dismissed.

- [ ] [Review][Decision] Hub "Ranked Pairs" ranking is defeated by confidence-only ordering over a support-2 floor — Any pair introduced together and never touched apart scores confidence 1.0 in **both** directions. With more such pairs than `topCoupling` (10), `OrderByDescending(Confidence).ThenByDescending(Support).ThenBy(FromPath, Ordinal)` lets the **ordinal path tie-break decide the visible top-10** — the panel becomes an alphabetical list of support-2 trivia, and genuinely high-support couples can never surface. Lift, computed specifically to demote low-information pairings, is not used in the ranking at all (and would rank a support-2 pair *higher*: base rate ~0.007 → lift ~150). Compounded by two related choices: both directions are always emitted, so a symmetric pair (`cA == cB`) renders as two rows identical in every displayed value — up to half the table is echo, and `Charts.cs:1761-1763` pre-emptively excuses this as "asymmetry is the finding, not a duplicate", which does not hold for exactly the population confidence-ranking floats to the top; and `Take(topCoupling)` was left at 10 while the population it caps doubled, with no per-`FromPath` diversity cap. The story's own Q4 note observed the constant-100% symptom on this repo and explicitly left the policy call for this round. Options: rank by lift; rank by confidence × log(1+support); require support > floor for the top-N; collapse mirror rows when confidence ties; raise the floor; or accept as-is. [src/SpecScribe/GitMetrics.cs:722-728]
- [ ] [Review][Decision] Confidence's numerator and denominator are drawn from different commit populations — `support` accumulates only for commits where `2 <= fileSet.Count <= CouplingFileSetCap`, but `ChangeCount` increments for **every** commit touching the file (`GitMetrics.cs:1019-1030` — the comment there concedes it: "it still counts toward change frequency above"). So `Confidence = support / ChangeCount` is systematically deflated for any file dragged through bulk/vendored/import commits: a lockfile in fifty 200-file vendor commits reads ~17% confident about its real partner instead of 100%. `CoupledFile`'s doc claim — "when I touch this file, I touch Path this often" — is false for exactly those files. The code matches AC #1's literal formula, so closing this means amending the metric definition, not just the code: either divide by a coupling-eligible change count (track it alongside `ChangeCount`), or accept the skew and correct the doc. The story's Dev Notes assert the metric "inherits that filtering for free" — that claim is not correct. [src/SpecScribe/GitMetrics.cs:1030] [src/SpecScribe/GitMetrics.cs:1088] [src/SpecScribe/GitMetrics.cs:714]
- [ ] [Review][Decision] AC #2's "available to every downstream surface (list and graphs)" is unmet for the graph — `Charts.CouplingGraph` still takes `IReadOnlyList<(string FileA, string FileB, int CoChanges)>` and is fed `deep.Coupling` (also in the lightbox copy). That tuple carries no `CrossBoundary`, so the flag is not merely unrendered on the graph — it is **structurally unavailable** there without re-deriving it, which is precisely what AC #2 forbids. Q1 legitimately deferred confidence-*weighting* of the graph to 24.2+, but AC #2 is about the cross-boundary property, not edge weight, and nothing in the Completion Notes discloses the gap. Decide: close it in 24.1, or amend AC #2 and record the deferral explicitly. [src/SpecScribe/Charts.cs:1824] [src/SpecScribe/DeepAnalyticsTemplater.cs:59]
- [ ] [Review][Patch] Lift's denominator counts commits that contributed nothing to its numerator [src/SpecScribe/GitMetrics.cs:1063] — both accumulation loops skip file-less records (`if (fileSet.Count == 0) continue;` at `:1006` and `:627`), yet `analyzedCommits = commits.Count` is the **unfiltered** record count, and the fetch at `:585` has no `--no-merges`. Measured on this repository: 25 of 299 commits in the window are merges (~8.4%), so every base rate is understated and every lift overstated by roughly `N / N_with_files` ≈ 9%. Lift's whole interpretive value is its anchor at 1.0. The comment at `:1061-1062` asserts the opposite of what the code does — "Records with no files were skipped above, so commits.Count is the honest analyzed-window size" — when the skip is precisely why it is the wrong number. Also passed to `BuildDirectedCoupling` at `:676`.
- [ ] [Review][Patch] Per-file lift caption attributes the base rate to the wrong file [src/SpecScribe/CodeFileTemplater.cs:679] — renders `title="Lift {n}× this file's usual rate"`, but `CoupledFile.Lift` is computed as `Lift(confidence, OtherChangeCount(p.Path), analyzedCommits)` (`GitMetrics.cs:1088`) — the denominator is the **coupled** file's base rate. `Charts.cs:1802` words the identical number correctly ("{ToPath} changes … — {lift}× its usual rate"). Two surfaces describe one number as belonging to two different files; the code page's version is simply wrong.
- [ ] [Review][Patch] `CouplingMinSupport` is bypassed by a surviving literal on the one path that most needs it [src/SpecScribe/SiteGenerator.cs:3134] — `if (count >= 2)` in `BuildRelatedRelatedEdges`, under a doc comment (`:3113`) claiming it "mirrors the SAME >= 2 threshold `GitMetrics.ParseNumstatLog`'s own top-level coupling view uses" — which is now a `minSupport` parameter, not a literal. Pass `minSupport: 3` and the relationship graph draws cross edges for couples the same page's list refuses to admit. This is verbatim the failure the const's own doc warns about (`GitMetrics.cs:271-273`: "two literals in two methods is how they silently stop agreeing"). Compounding it: the edges are computed over `DeepGitPulse.CoChangePairs`, which is assigned the **unfiltered** `pairCounts` (`GitMetrics.cs:1101`), so the floor never reaches that path regardless of the literal.
- [ ] [Review][Patch] Task 3 (`DirectedCoupling`/`BuildDirectedCoupling`) has zero test coverage, and no test asserts any ordering [tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs] — every `DirectedCoupling` reference in `tests/` is a hand-built fixture whose own helper admits "Confidence here is synthetic". Nothing asserts that `ParseNumstatLog` populates `DirectedCoupling` at all, that both directions are emitted, that the confidence ranking is applied, that the min-support floor reaches the hub view, or that `Take(topCoupling)` bounds it. Task 7 explicitly asked for "`CouplingTable` renders the confidence column + cross-boundary marker + **confidence sort**" — column and marker are tested, the sort is not. Related: the `FileInsight.CoupledFiles` ordering contract changed from support-desc to confidence-desc and every index-aligned consumer (`SiteGenerator.cs:3098`, `:3124`) depends on it implicitly, with nothing testing the invariant and the doc at `:3080` never naming which order — so that sentence stayed "true" across a semantic inversion. Also: `CouplingMinSupport_DefaultsToTwoSoOneOffCouplesAreCoincidenceNotSignal` is `Assert.Equal(2, GitMetrics.CouplingMinSupport)` — a literal asserted against itself, verifying no floor *behavior*, while the File List claims `GitMetricsCouplingTests.cs` covers the "floor contract".
- [ ] [Review][Patch] Two different lift formatters ship on the two surfaces [src/SpecScribe/Charts.cs:1802] — the hub renders lift via `F()` (`"0.##"`, `Charts.cs:3662`); the code page uses `lift.ToString("0.0")` (`CodeFileTemplater.cs:679`, and `:625`). Lift 1.25 → "1.25×" on the hub, "1.3×" on the code page; 2.0 → "2×" vs "2.0×". `Percent`'s own doc (`Charts.cs:3668-3672`) justifies itself as "the ONE formatter … so the per-file text twin and the hub's table can never disagree about rounding" — this story did that for confidence and shipped the exact disagreement for lift in the same commit. Fix: one shared `LiftLabel`.
- [ ] [Review][Patch] `Percent` asserts false extremes at both ends [src/SpecScribe/Charts.cs:3677] — `Math.Round(clamped * 100, MidpointRounding.AwayFromZero)` renders 0.996 as "100%", claiming a totality the data does not support, and a 500-change file with a support-2 partner as `2× / 0%` — a row asserting a couple exists while printing zero. Neither end has a floor or a "<1%" form.
- [ ] [Review][Patch] Coupling-table tooltip reads nonsensically when both files share a basename [src/SpecScribe/Charts.cs:1802] — the sentence is built from `Basename(FromPath)` and `Basename(ToPath)`, so a pair of `index.ts` / `README.md` / `__init__.py` files yields "When index.ts changes, index.ts changes 80% of the time". Disambiguate with the full path when basenames collide.
- [ ] [Review][Patch] `Basename` does not normalize separators although `BoundaryOf` in the same feature explicitly does [src/SpecScribe/Charts.cs:2071] — splits on `/` only, while `GitMetrics.BoundaryOf` does `path.Replace('\\','/')` first and has a test asserting why that matters. A backslash-bearing path reaching `CouplingTable` yields a tooltip naming the entire path where a filename was intended.
- [ ] [Review][Patch] Stale doc left by this story: `ParseNumstatLog`'s summary still names the literal this story replaced [src/SpecScribe/GitMetrics.cs:601] — reads "kept only at `CoChanges >= 2`" rather than naming `minSupport`/`CouplingMinSupport`.
- [x] [Review][Defer] `BoundaryOf` treats a leading `./` or an embedded `..` as a real boundary segment [src/SpecScribe/GitMetrics.cs:353] — deferred, not reachable from git's repo-relative numstat paths today. `StringSplitOptions.RemoveEmptyEntries` strips `""` but not `"."`, so `./src/A.cs` gets boundary `"."` and reads cross-boundary against `src/B.cs` while same-boundary as `./tests/B.cs`. A leading `/` is handled correctly.
- [x] [Review][Defer] `IsCrossBoundary` compares boundaries `Ordinal`, so `Src/A.cs` vs `src/B.cs` reads as crossing a module boundary [src/SpecScribe/GitMetrics.cs:381] — deferred, consistent with the rest of the pipeline's ordinal keying, but it makes the architectural signal sensitive to path casing git may report differently across platforms.
- [x] [Review][Defer] Lift is carried only in a `title` attribute, which on the per-file surface is unreachable by every user [src/SpecScribe/CodeFileTemplater.cs:678] — deferred, accessibility. The attribute sits on a non-focusable `<li>` inside `<ul class="ref-list sr-only">`: hidden from pointer users, not focusable for keyboard users, and `title` on a non-interactive element is not reliably announced by AT. The comment at `:660-662` frames it as progressive disclosure; on a hidden element there is no disclosure path. `Charts.cs:1809` has the milder pointer-only version on a `<td>`, as does the full path at `:1806-1807` while the class doc at `:1759-1760` claims full paths are "shown as real text".
- [x] [Review][Defer] The boundary badge's claimed non-colour differentiator is a 1.66:1 border, and sub-10px type was introduced to fit the fifth column [src/SpecScribe/assets/specscribe.css:4811] — deferred, owner design call. `--parchment-deep` (#d4b896) on `--cream` (#f5f0e8) is ≈1.66:1, below WCAG 1.4.11's 3:1 for non-text UI components, as a 1px border around ~10px text; the comment takes credit for a mechanism that does not meet the bar. The *words* carry the meaning independently, so AC #3/UX-DR19 survives. Separately `th.coupling-num`/`th.coupling-kind` at `0.6rem` (≈9.6px) and badges at `0.62rem`; with `table-layout: fixed` the paths land at ~117px — better than the 60px the live-browser pass fixed, but still truncating real repository paths.
- [x] [Review][Defer] `DirectedCoupling` is an optional init-only property beside a required positional `Coupling` [src/SpecScribe/GitMetrics.cs:97] — deferred, pre-existing shape. A `DeepGitPulse` built or `with`-copied without setting it renders, on one page, a populated coupling graph directly above "No significant change coupling detected." — the graph gates on `deep.Coupling` while the table gates on `deep.DirectedCoupling`. That three separate test fixtures had to remember to set both is the invariant leaking into every caller.
- [x] [Review][Defer] The graph and the table now rank different populations under one heading and one `WhyText` [src/SpecScribe/DeepAnalyticsTemplater.cs:88] — deferred, disclosure/UX. `Coupling` is the top 10 pairs by shared commits; `DirectedCoupling` is the top 10 directed edges by confidence, so "Ranked Pairs" can name ten files appearing nowhere in the "Change Coupling" graph above it. The new caption names this panel's ranking but never tells the reader the two panels stopped being about the same rows.
- [x] [Review][Defer] Task 5's Story 10.2 framing subtask is ticked but shipped nothing [src/SpecScribe/CodeFileTemplater.cs] — deferred, record correction. At the landing commit the file contained no `WhyText`/`Framed`/`ChartMeta` at all; the hub's framing predates this story unchanged. The per-file framing that exists now is Story 24.2's. AC #3's "one-sentence framing per Story 10.2" was satisfied only by the surface that already had it.
- [x] [Review][Defer] A default floor of 2 can silently empty every coupling surface on a short window with no user-visible explanation [src/SpecScribe/GitMetrics.cs:270] — deferred, product decision. The test churn is the tell: an existing assertion became `Assert.Empty`, `..._RespectsCaps` had to pass `minSupport: 1` to observe caps at all, and `SiteGeneratorCodeInsightsTests` needed an extra commit to keep its scenario alive. Users see "No significant change coupling detected." with nothing indicating a threshold exists or what it is. The floor is documented thoroughly in code and nowhere in the rendered site.

**Dismissed (5)** — recorded so a later review does not re-raise them:
`Support` interpolated without `CultureInfo.InvariantCulture` at `Charts.cs:1808` (false positive: `int`
interpolation uses the "G" specifier, which applies no group separators; no culture changes a non-negative
int here). • `.coupling-boundary-badge` missing from `web/assets/ir-content.css` (real at the landing
commit — the CSS reached the scoped IR layer only on 2026-07-30 in `a8c97f3`, two days late, so the
live-browser column rebalance was absent from IR-rendered surfaces until then — but present at HEAD;
`web/assets/ir-content.css` is still missing from the File List). • AC #3's "visibly marked" unmet on the
per-file surface (true at landing, where the visible reference graph's `<title>` still read only "changed
together N times"; closed at HEAD by Story 24.2's `RelatedDetail`, which renders confidence, cross-boundary
and lift as visible text). • `Percent` returning `"0%"` on NaN rather than degrading to an absence the way
`Lift` returns `null` (unreachable: both denominators are `ChangeCount`/`fromChanges`, ≥1 by construction
for any file present in the map). • "Baseline output byte-identical WITHOUT `--deep-git`" reported as
violated by this story's CSS (the invariant's intent — no coupling *panels* without deep-git — holds, and
the `GoldenContentFingerprint` gate cited as evidence was since retired by ADR 0034).

## Dev Notes

### What this story is (and is NOT)

- **IS**: the non-visual metric spine of Epic 24 + the upgraded accessible **list** (per-file text-twin + hub table). It gates 24.2–24.5 and is deliverable on its own.
- **IS NOT**: any interactive/force-directed/chord/matrix graph (those are 24.2–24.5), any new page, any new nav entry, any client JS, and NOT the ownership/bus-factor "who changes this file" half (already shipped in Story 7.11 — do not touch it). Charts stay pure-SVG here ([[charting-is-pure-svg-no-js]]); the JS interactivity budget (Epic 20 / ADR 0010) is a later-story concern.

### The metric (all from data already in hand — NO new git call)

Every input already exists on `DeepGitPulse` after the single `--deep-git` numstat parse ([[deep-git-single-numstat-path]] — extend that ONE fetch, never add a second):

- `CoChangePairs` — canonical unordered `(A,B)→count`, `A ≤ B` ordinal ([GitMetrics.cs:82](src/SpecScribe/GitMetrics.cs)); look up via `CoChangeCount` ([GitMetrics.cs:900](src/SpecScribe/GitMetrics.cs)).
- Per-file `ChangeCount` — commits touching the file (once per commit). Lives on `FileInsight.ChangeCount` ([GitMetrics.cs:170](src/SpecScribe/GitMetrics.cs)) and in `ParseNumstatLog`'s local `changeCounts` dict ([GitMetrics.cs:497](src/SpecScribe/GitMetrics.cs)).
- `AnalyzedCommits` — honest window size ([GitMetrics.cs:551](src/SpecScribe/GitMetrics.cs)).

Formulas:
- **confidence(A→B) = coChange(A,B) / ChangeCount[A]** — asymmetric. Always in `[0,1]` (a pair's shared-commit count can never exceed either file's own change count). "When I touch A, I touch B `confidence`% of the time."
- **support(A,B) = coChange(A,B)** — the shared-commit count. Floor via `CouplingMinSupport` (default 2) to kill coincidental one-off couples.
- **lift(A→B) = confidence(A→B) / (ChangeCount[B] / AnalyzedCommits)** — > 1 means B accompanies A more than B's base rate would predict; a file that changes every commit has base-rate ≈ 1 and self-demotes to lift ≈ confidence's ceiling. **Guard divide-by-zero**: null when `ChangeCount[B] == 0` or `AnalyzedCommits == 0`.

The `CouplingFileSetCap` (50) bulk-commit skip ([GitMetrics.cs:203](src/SpecScribe/GitMetrics.cs)) already excludes merge/vendored sweeps from pair counts in BOTH `ParseNumstatLog` and `BuildFileInsights` — the directional metric inherits that filtering for free; do not re-implement it.

### Cross-boundary ("surprising coupling")

Pure function of the two paths — top-level directory segments differ ⇒ architectural smell (a file coupled across a module boundary). This must be a **shared property computed once** (AC #2): store it on `CoupledFile.CrossBoundary` (and the hub's directed record), do not have each view re-derive it divergently. Emphasize it as **text** (a marker word/badge), never color-only (UX-DR19, NFR8). This is orthogonal to and layered ON TOP of the existing `ClassifyCoupling` Code-vs-Process lens ([GitMetrics.cs:271](src/SpecScribe/GitMetrics.cs)) — a pair can be both cross-boundary AND process; keep both signals.

### Existing surfaces to reuse (do not reinvent)

- Framing/metadata: `Charts.ChartMeta` + `Charts.Framed` + `Charts.WhyText(ChartMetric.ChangeCoupling)` ([Charts.cs:42-168](src/SpecScribe/Charts.cs)) — the ONE Story 10.2 framing source. Ranking caption goes in `ChartMeta.Ranking`, data caveat in `ChartMeta.Note` (as `ProcessCouplingNote` already does, [Charts.cs:128](src/SpecScribe/Charts.cs)).
- Pluralization: `Charts.Plural` ([Charts.cs:4742](src/SpecScribe/Charts.cs)). Percent/number formatting: use `CultureInfo.InvariantCulture` (matches every other numeric render here).
- Code-page link resolution: the `fileHref`/`coupledFileHref` `Func<string,string?>` dual-mode resolver already threaded into both templaters ([CodeFileTemplater.cs:48](src/SpecScribe/CodeFileTemplater.cs), [DeepAnalyticsTemplater.cs](src/SpecScribe/DeepAnalyticsTemplater.cs); wired at [SiteGenerator.cs:352](src/SpecScribe/SiteGenerator.cs)) — a null return means "no in-portal page" → plain text, never a dead link. Reuse it; do not build a new resolver.

### Files being modified (read current state before editing)

- `src/SpecScribe/GitMetrics.cs` — model + math + floor. `FileInsight`/`CoupledFile` record, `BuildFileInsights` fan-out, `ParseNumstatLog` coupling list, new `IsCrossBoundary`, new `CouplingMinSupport` const, new `DeepGitPulse` directed-couples property.
- `src/SpecScribe/Charts.cs` — `CouplingTable` (confidence column + cross-boundary marker + sort). Possibly `CouplingGraph` legend semantics (Q1). Do NOT touch `WhyText`.
- `src/SpecScribe/CodeFileTemplater.cs` — per-file coupled text-twin (`BuildRelatedNodes` + sr-only list) reads the new record + shows confidence/cross-boundary.
- `src/SpecScribe/DeepAnalyticsTemplater.cs` — hub coupling panel: table wiring, ranking caption, legend copy.
- `src/SpecScribe/SiteGenerator.cs` — `CoupledFiles` consumers in `BuildStoryRelatedEdges`/`BuildRelatedRelatedEdges` compile against the new record; ordering contract preserved.

### Preservation invariants (leave the system working end-to-end)

- Baseline output byte-identical WITHOUT `--deep-git` (coupling data is null → panels omitted entirely, [GitMetrics.cs:32](src/SpecScribe/GitMetrics.cs)) — the new metric only appears when deep-git is opted in.
- The per-file coupled list ↔ hub coupling must keep agreeing (same floor, same bulk-commit skip) — that consistency is why the floor is a shared const, not two literals.
- `CoChangeCount` and the reference-graph related-node ordering ([SiteGenerator.cs:1960](src/SpecScribe/SiteGenerator.cs) index-alignment) must stay correct after the sort/shape change.
- NFR8: everything in 24.1 is server-rendered HTML — no JS is introduced; the list is readable with JS off by construction.

### Project Structure Notes

- No new files required beyond optional new test files; all changes land in the five existing `src/SpecScribe/*.cs` files above and their existing test siblings. No new page, no nav change, no new CLI surface (pending Q3). Output dir remains `SpecScribeOutput` ([[generate-output-dir-is-specscribeoutput]]).
- If working in a worktree, target the worktree path — `main` has a background auto-committer ([[worktree-edits-must-target-worktree-path]]); grep-verify new symbols exist after edits ([[shared-main-concurrent-edit-loss-verify-after-edit]]).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 24] — epic charter, FR40, Stories 24.1–24.5, UX-DR19/20/21, NFR8.
- [Source: src/SpecScribe/GitMetrics.cs] — `DeepGitPulse` (35), `FileInsight` (169), `CouplingKind`/`ClassifyCoupling` (234/271), `BuildFileInsights` (802), `ParseNumstatLog` coupling (541), `CoChangeCount` (900), `CouplingFileSetCap` (203).
- [Source: src/SpecScribe/Charts.cs] — `ChartMetric`/`ChartMeta`/`WhyText`/`Framed` (13–168), `ProcessCouplingNote` (128), `CouplingTable` (2090), `CouplingGraph` (2128).
- [Source: src/SpecScribe/DeepAnalyticsTemplater.cs] — hub coupling panel (30–120).
- [Source: src/SpecScribe/CodeFileTemplater.cs] — per-file related/coupled rendering (261–272, 491–505).
- [Source: docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md] — zero-dep JS posture (relevant to 24.2–24.5, not 24.1).
- Prior art: Story 3.8 (git-insights hub), Story 7.4 (per-file coupled list), Story 7.8 (coupling moved onto the reference graph), Story 10.2 (chart framing), Story 10.6 (Code-vs-Process coupling classifier), Story 7.11 (ownership half — out of scope).

### Open questions for the owner (do not block dev-start — recommended defaults noted)

- **Q1 — Hub coupling direction & graph:** the hub table/graph today render *symmetric* pairs. Recommended: make the hub **table** directed (rows of "File → Coupled with, confidence%", top-N by confidence), and in 24.1 leave the hub **graph** SVG weighted by shared-commits (defer confidence-weighted/directed edges to 24.2+). Alternative: keep the table symmetric and show the max-confidence direction inline. Which?
- **Q2 — Cross-boundary at the repo root:** recommended — root-level files share a boundary with each other and are cross-boundary vs any nested file. Confirm, or treat every distinct top-level dir/file as its own boundary?
- **Q3 — "Configurable" min-support scope:** recommended — a shared named const/parameter with default 2 (no new user-facing flag this story). Or should it be a real `--deep-git` sub-option / setting now?
- **Q4 — Confidence vs lift as the primary sort:** AC allows either. Recommended — sort by **confidence** (most intuitive), tie-break support-desc then ordinal; surface lift in the tooltip. Confirm.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Amelia / dev-story), 2026-07-28.

### Debug Log References

- `dotnet test` full suite — 2740+ tests. Final green run: 0 failures attributable to this story.
- Golden fingerprint regenerated `f12b1ff2…` → `ee00f947…`, stable across two runs after
  `dotnet build --no-incremental`. Full provenance split recorded in `SiteGeneratorAdapterTests.cs`.
- Live-browser verification of the hub table at `http://localhost:8110/deep-analytics.html`
  (real `--deep-git` render of this repo, 300 commits, 692 files).

### Completion Notes List

**Owner questions — all four taken at the story's recommended default. Flagging Q4 for the verify round.**

- **Q1** — hub *table* is directed and confidence-ranked; hub *graph* SVG left weighted by shared commits
  (confidence-weighted/directed edges stay 24.2+). Because the two panels now rank different populations, the
  Ranked Pairs caption was rewritten to name *its own* ranking rather than inherit the graph's.
- **Q2** — root-level files share the repository-root boundary with each other, and are cross-boundary against
  anything nested.
- **Q3** — shared named const `GitMetrics.CouplingMinSupport = 2`, threaded as an optional parameter through
  `BuildFileInsights` and `ParseNumstatLog`. No new CLI flag.
- **Q4** — sorted by confidence, tie-broken support-desc then ordinal path; lift rides the tooltip.
  ⚠️ **Worth an owner look in the verify round:** on this repo the entire top-10 comes back at **100%
  confidence**, so the Confidence column reads as a constant and does no ranking work in the visible window.
  Lift *does* separate those rows (15.0× vs 2.16× for two otherwise identical-looking 100% rows) but is only in
  the tooltip. Sorting by lift, or showing lift as a column, would make the panel discriminate. Left as
  specified rather than changed unilaterally — this is a ranking-policy call, not a defect.

**What shipped**

- `IsCrossBoundary` + a private `BoundaryOf` helper: pure, symmetric, repo-free, never throws; an unreadable
  path degrades to "not cross-boundary" rather than asserting an architectural smell it cannot see.
- `CoupledFile` (per-file, directional) and `DirectedCouple` (whole-repo, carries its own `FromPath`) share one
  metric definition. `FileInsight.CoupledFiles` changed shape from `(string, int)` to `CoupledFile`.
- `GitMetrics.Lift(confidence, targetChangeCount, analyzedCommits)` is the ONE place the division happens, so
  no surface can forget the guard. Returns `null` — never `NaN`/`Infinity`, which would reach markup as literal
  text.
- Confidence is computed in the per-file loop where the focal file's own `ChangeCount` is in hand, which is what
  makes A→B and B→A genuinely differ. Verified live: `GitMetricsTests.cs → GitMetrics.cs` is 100% confident at
  15.0× lift and cross-boundary — a real, correct finding a symmetric count cannot express.
- `DeepGitPulse.DirectedCoupling` is computed once in `ParseNumstatLog` from maps the single numstat parse
  already built. **No second git call and no second commit scan** (AC #1).
- Both directions of a qualifying pair are emitted and then ranked together, so a strongly one-way relationship
  surfaces on its strong side instead of being averaged away.

**Consequences worth knowing (deliberate, per AC #1)**

- The support floor now applies to the **per-file** list too, which it did not before. One-off couples disappear
  from code pages site-wide. This is the AC's "filters coincidental couples", but it is a visible behaviour
  change and it moved three existing tests, each updated with a comment saying why rather than silently
  re-baselined.
- `Charts.ReferenceGraph`'s signature was deliberately NOT widened (Task 6 asked to verify no drift). The richer
  metric rides a private `RelatedNode` record in `CodeFileTemplater`, projected down to the graph's existing
  4-tuple via `ToGraphNodes`. This kept ~15 `ChartsTests` call sites untouched.
- `SiteGenerator.cs` needed **no change**: its two `CoupledFiles` consumers read only `.Path`. The story's Task 6
  predicted a `.CoChanges` read there; that read no longer existed. Verified by grep, not assumed.

**Live-browser pass caught two defects the test suite structurally could not** (CLAUDE.md § Verification)

Both were pure rendered geometry in the ~455px Ranked Pairs panel, invisible to string-assertion tests:

1. The new Confidence column starved the two path columns from ~124px to **60px each**, truncating them to
   "tests…"/"src/S…" and defeating the table's purpose.
2. The first fix then exposed single-word headers overrunning their columns under `table-layout: fixed`,
   rendering as one run-on string **"TOGETHERCONFIDENCEKIND"**.

Fixed by sizing the numeric columns to their *values* (with smaller unspaced headers), narrowing `.coupling-kind`
to one badge width, and setting the badges in sentence case — uppercase + letter-spacing was what made
"CROSS-BOUNDARY" 120px wide and single-handedly set the column width. Re-measured after: paths 94px, **zero**
header overflow, **zero** clipped cells, no horizontal body overflow, and the 3 rows carrying both Process and
Cross-boundary still stack correctly.

**Shared-main notes**

- Story 23.4's `PageView` inversion rewrote `CodeFileTemplater.cs` underneath this work mid-story. All Story 24.1
  symbols were grep-verified present afterwards ([[shared-main-concurrent-edit-loss-verify-after-edit]]).
- The golden fingerprint moved three times during this story from *other* sessions' work. The regeneration note
  names which portion is 24.1's and which is not, rather than absorbing theirs.
- Full-suite runs showed 5–15 failures in git-shelling tests under concurrent CPU load; all 54 pass in isolation.
  This is the known deep-git 3s-timeout flake ([[gitmetrics-3s-timeout-silent-deep-git-loss]]), not a regression.
  It also cost two `--deep-git` generation attempts, which silently produced no deep surfaces at all.

### File List

- `src/SpecScribe/GitMetrics.cs` — `IsCrossBoundary`/`BoundaryOf`, `CouplingMinSupport`, `CoupledFile`,
  `DirectedCouple`, `Lift`, `DeepGitPulse.DirectedCoupling`, `BuildDirectedCoupling`, directional math + floor +
  confidence sort in `BuildFileInsights`, `minSupport` parameter on `BuildFileInsights`/`ParseNumstatLog`.
- `src/SpecScribe/Charts.cs` — `CouplingTable` now takes `IReadOnlyList<DirectedCouple>`, adds the Confidence
  column and the cross-boundary badge; new shared `Percent` formatter.
- `src/SpecScribe/CodeFileTemplater.cs` — private `RelatedNode` record + `ToGraphNodes` projection;
  `BuildRelatedNodes` and the sr-only text twin carry confidence, cross-boundary words, and lift-on-title.
- `src/SpecScribe/DeepAnalyticsTemplater.cs` — Ranked Pairs panel reads `DirectedCoupling`; ranking caption
  rewritten to name the confidence ranking.
- `src/SpecScribe/assets/specscribe.css` — `.coupling-boundary-badge`, badge sizing, and the coupling-table
  column rebalance from the live-browser pass.
- `tests/SpecScribe.Tests/GitMetricsCouplingTests.cs` — **new**: `IsCrossBoundary` + floor contract.
- `tests/SpecScribe.Tests/GitMetricsFileInsightsTests.cs` — asymmetric confidence, lift + divide-by-zero,
  cross-boundary + preserved Kind, floor-before-cap, configurable floor.
- `tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs` — directed-table fixtures + `DirectedFrom` helper;
  Confidence column, cross-boundary badge, both-badge independence, lift omission, empty state, caption.
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` — directional fixture; sr-only confidence, cross-boundary
  words, lift-on-title.
- `tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs` — chip fixture given real support (2 co-change
  commits) so the floor does not filter the case under test.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden fingerprint regenerated + provenance split.
- `.claude/launch.json` — added the `coupling-24-1` verification server entry.

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-28 | Story 24.1 implemented: directional coupling spine (confidence/support/lift/cross-boundary) over the existing single deep-git parse, plus the upgraded per-file text twin and confidence-ranked hub table. All four owner questions taken at their recommended defaults (Q4 flagged for the verify round — the visible top-10 is all 100% confidence). Two rendered-geometry defects found and fixed in a live browser. Golden fingerprint regenerated `f12b1ff2…` → `ee00f947…` with an explicit split of which sessions moved it. Status → review. |
