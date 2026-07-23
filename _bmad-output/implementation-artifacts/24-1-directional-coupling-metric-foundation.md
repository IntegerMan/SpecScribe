# Story 24.1: Directional Coupling Metric Foundation (Confidence, Support, Lift, Cross-Boundary) + Upgraded List

Status: ready-for-dev

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

- [ ] **Task 1 — Cross-boundary classifier (pure, shared)** (AC: #2)
  - [ ] Add `public static bool IsCrossBoundary(string pathA, string pathB)` to `GitMetrics` (near `ClassifyCoupling`, [GitMetrics.cs:271](src/SpecScribe/GitMetrics.cs)). Compare the **first path segment** (top-level directory) after normalizing `\`→`/` and splitting on `/`. Two files under the same top-level dir → same-boundary; different top-level dirs → cross-boundary. Decide root-level handling per **Q2** (recommended: a root-level file — no directory — shares a boundary with other root-level files, and is cross-boundary vs any nested file).
  - [ ] Pure and repo-free (no SpecScribe path literals, NFR8), never throws, deterministic.
  - [ ] Unit tests in `GitMetricsTests.cs` (or a new `GitMetricsCouplingTests.cs`): same-dir, cross-dir, root-vs-nested, root-vs-root, empty-path guards.

- [ ] **Task 2 — Directional metric model (the shared spine)** (AC: #1, #2)
  - [ ] Introduce a record `public sealed record CoupledFile(string Path, int Support, double Confidence, double? Lift, bool CrossBoundary, GitMetrics.CouplingKind Kind)` in `GitMetrics.cs` (beside `FileInsight`, [GitMetrics.cs:169](src/SpecScribe/GitMetrics.cs)). `Support` = the shared-commit count (today's `CoChanges`). `Lift` is nullable — undefined when `ChangeCount[B]` or `AnalyzedCommits` is 0 (guard divide-by-zero; render as "—"/omit, never `NaN`/`Infinity`).
  - [ ] Change `FileInsight.CoupledFiles` from `IReadOnlyList<(string Path, int CoChanges)>` to `IReadOnlyList<CoupledFile>` ([GitMetrics.cs:172](src/SpecScribe/GitMetrics.cs)). This is the load-bearing shape all downstream surfaces read; every consumer below must be updated in this story (regression guardrail — see Task 6).
  - [ ] Compute the directional fields inside `BuildFileInsights` where the pairs are already fanned out to both members ([GitMetrics.cs:858-888](src/SpecScribe/GitMetrics.cs)). In that loop **both** `a`, `b` and their `FileInsightAccum.ChangeCount` are in hand, so directional confidence is computed with the CORRECT numerator per direction: file A's list entry for B carries `confidence = count / ChangeCount[A]`; file B's entry for A carries `count / ChangeCount[B]`. `commits.Count` (== `AnalyzedCommits`) is available in `BuildFileInsights` for lift's denominator.
  - [ ] Keep the sort **within** the cap as confidence-desc, then Support-desc, then ordinal path (was `CoChanges`-desc). Apply the min-support floor (Task 4) BEFORE the `coupledCap` take so low-support noise never crowds out real couples. Preserve `FileInsightCoupledCap` ([GitMetrics.cs:764](src/SpecScribe/GitMetrics.cs)).
  - [ ] Preserve `ClassifyCoupling` — set `CoupledFile.Kind` from it; do not alter the Code-vs-Process rules.

- [ ] **Task 3 — Whole-repo directional view for the hub** (AC: #1, #3)
  - [ ] The hub's top-N `Coupling` list (`DeepGitPulse.Coupling`, built in `ParseNumstatLog` [GitMetrics.cs:541-548](src/SpecScribe/GitMetrics.cs)) is today symmetric `(FileA, FileB, int CoChanges)` sorted by shared-commit count. Add a directional projection the hub table/graph consume. Recommended: a new `public static IReadOnlyList<CoupledFile-with-source>` shaped list keyed by a source file, OR a sibling record `DirectedCouple(string FromPath, string ToPath, int Support, double Confidence, double? Lift, bool CrossBoundary, CouplingKind Kind)` computed from `CoChangePairs` + per-file `ChangeCount` (available via the `changeCounts` dict in `ParseNumstatLog`, [GitMetrics.cs:497](src/SpecScribe/GitMetrics.cs)) + `AnalyzedCommits`. See **Q1** for the directed-vs-symmetric decision.
  - [ ] Reuse the SAME min-support floor const (Task 4) and confidence sort. Surface it on `DeepGitPulse` (a new `init` property, mirroring how `CoChangePairs` was added at [GitMetrics.cs:82](src/SpecScribe/GitMetrics.cs)) so it is computed once and reused, not recomputed per view (AC #2).
  - [ ] Do NOT add a second git call or second commit scan — derive entirely from already-parsed records/maps (one fetch, one parse, several views).

- [ ] **Task 4 — Configurable minimum-support floor** (AC: #1)
  - [ ] Introduce a named const (e.g. `CouplingMinSupport = 2`) replacing the hard-coded `kv.Value >= 2` at [GitMetrics.cs:542](src/SpecScribe/GitMetrics.cs). Thread it through both the hub directional list (Task 3) and the per-file `BuildFileInsights` filter (Task 2) so the two surfaces agree, exactly as `CouplingFileSetCap` is shared today.
  - [ ] Keep the default at **2** so the baseline output is unchanged except for the new metric columns/sort (see golden-fingerprint note). "Configurable" = a parameter/const with a sensible default, not necessarily a new CLI flag — confirm scope in **Q3**.

- [ ] **Task 5 — Upgrade the two render surfaces (the "upgraded list")** (AC: #3)
  - [ ] **Per-file text-twin** (`CodeFileTemplater.BuildRelatedNodes` + sr-only related list, [CodeFileTemplater.cs:261-272](src/SpecScribe/CodeFileTemplater.cs) and [CodeFileTemplater.cs:491-505](src/SpecScribe/CodeFileTemplater.cs)): each entry shows directional confidence ("changed together N× · confidence M%") and a **text** cross-boundary marker (never color-only — UX-DR19/NFR8), e.g. append " · cross-boundary". Lift belongs in the `<title>`/tooltip. This sr-only list is the accessible text-twin the graph stories reuse — keep it complete and readable with JS off. Do NOT redesign the visible code-page surface (the reference graph is 24.2's job); 24.1 upgrades the metric + text list only.
  - [ ] **Hub coupling table** (`Charts.CouplingTable`, [Charts.cs:2090](src/SpecScribe/Charts.cs)): add a **Confidence** column (directional %), sort rows by confidence with the support floor applied, and add a cross-boundary text marker/badge alongside the existing "Process" Kind badge ([Charts.cs:2100-2114](src/SpecScribe/Charts.cs)). Keep the process-vs-code badge behavior intact.
  - [ ] **Hub coupling graph legend** (`DeepAnalyticsTemplater`, [DeepAnalyticsTemplater.cs:60-77](src/SpecScribe/DeepAnalyticsTemplater.cs)): update the legend copy to explain the new edge/weight semantics if edges now encode confidence; the `CouplingGraph` SVG itself ([Charts.cs:2128](src/SpecScribe/Charts.cs)) may stay weight-by-shared-commits in 24.1 (interactive/confidence-weighted graph is 24.2+) — confirm in **Q1**. The `role="img"` aria label and `<title>` tooltips must stay truthful to whatever they encode.
  - [ ] **Framing (Story 10.2)**: the `ChartMetric.ChangeCoupling` `WhyText` sentence already exists ([Charts.cs:58](src/SpecScribe/Charts.cs)); reuse it — do NOT hand-roll new "why" copy at call sites. If confidence changes what the ranking caption should say, update the `ChartMeta.Ranking` string in `DeepAnalyticsTemplater` ([DeepAnalyticsTemplater.cs:89-91](src/SpecScribe/DeepAnalyticsTemplater.cs)), not the shared `WhyText`.

- [ ] **Task 6 — Update every `CoupledFiles` consumer (no regressions)** (AC: #3)
  - [ ] `CodeFileTemplater.cs`: `BuildRelatedNodes` destructure `foreach (var (path, coChanges) in insight.CoupledFiles)` ([CodeFileTemplater.cs:269](src/SpecScribe/CodeFileTemplater.cs)) → read `CoupledFile.Path`/`.Support`/`.Confidence`/`.CrossBoundary`; the `related` node tuple + its consumer at [CodeFileTemplater.cs:495](src/SpecScribe/CodeFileTemplater.cs).
  - [ ] `SiteGenerator.cs`: `BuildStoryRelatedEdges`/`BuildRelatedRelatedEdges` read `insight.CoupledFiles[j].Path` and `.CoChanges` ([SiteGenerator.cs:1966-2004](src/SpecScribe/SiteGenerator.cs)) → `.Path`/`.Support`. These index-align with the reference-graph related nodes ([SiteGenerator.cs:1960](src/SpecScribe/SiteGenerator.cs) comment) — keep the ordering contract intact after the sort change.
  - [ ] `Charts.ReferenceGraph` related-node title ("changed together N times", [Charts.cs:2509](src/SpecScribe/Charts.cs)) stays valid (it reads the passed related tuple, not `CoupledFiles` directly) — verify no signature drift.
  - [ ] Grep the whole `src/` + `tests/` for `CoupledFiles` / `.CoChanges` before finishing; every read site must compile against the new record.

- [ ] **Task 7 — Tests + golden fingerprint** (AC: #1, #2, #3)
  - [ ] `GitMetricsFileInsightsTests.cs`: assert confidence = count/ChangeCount[focal] with the correct direction (build a fixture where A→B ≠ B→A), lift math + divide-by-zero → null, min-support floor filters a support-1 couple, cross-boundary flag, `Kind` preserved.
  - [ ] `ChartsTests.cs` (+ `SiteGeneratorCodeInsightsTests.cs`): `CouplingTable` renders the confidence column + cross-boundary marker + confidence sort; the per-file sr-only list carries confidence + cross-boundary text; empty/degenerate inputs still render the friendly empty state ([Charts.cs:2092](src/SpecScribe/Charts.cs)).
  - [ ] Run the full suite. The golden fingerprint **WILL move** (coupling list text + sort change); regenerate it deliberately and confirm the move is only the intended coupling copy/order — see [[golden-diff-normalization-gotchas]]. RenderParity/SPA/webview: the coupled list lives inside code pages + the hub, both already captured by existing coherence tests — extend them, don't add a new page.

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

### Debug Log References

### Completion Notes List

### File List
