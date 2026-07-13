---
baseline_commit: 2f30ef9b696157c96d6c931304264f7bc138313d
---

# Story 7.8: Related Files in the Reference Graph

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reviewer exploring a code file,
I want the file's reference graph to also show the files it most frequently changes alongside,
so that I can see a file's real neighbourhood — the artifacts that cite it and the code that co-evolves with it — in one view.

## Acceptance Criteria

1.
**Given** deep-git analysis is available (the change-coupling / co-change data SpecScribe already computes)
**When** a code page's reference graph renders
**Then** the graph also includes nodes for the files most frequently changed together with this file, visually distinguished from the citing-artifact nodes and linking to those files' code pages
**And** each related-file node carries a rich tooltip (the file and its co-change strength), and the graph degrades to citations-only when deep-git data is unavailable. [Source: epics.md#Story 7.8 (lines 1407-1411); FR19]

2.
**Given** the graph now carries both citing-artifact and related-file nodes with tooltips and clickthroughs
**When** the page renders
**Then** the graph is the single relationship surface — no redundant visible list duplicating what the nodes already convey
**And** an accessible text equivalent of every node/link is still present for assistive tech (NFR6/UX-DR16), and node/edge counts stay bounded so a hub file's graph remains legible. [Source: epics.md#Story 7.8 (lines 1413-1417); NFR6, UX-DR16]

---

## Developer Context

**Read this first — this is a graph-rendering + wiring story, not a new page class or a new git computation.** Story 7.8 extends the code-page "Referenced by" reference graph — the pure-SVG hero introduced by Story 7.1's rework and rendered by `Charts.ReferenceGraph` — with a **second population of nodes: the files this file most often changes alongside**. That co-change data **already exists**, already computed, already bounded, already `--deep-git`-gated, and already flowing into the exact templater you'll edit: it is `FileInsight.CoupledFiles` (a list of `(string Path, int CoChanges)`), built by Story 7.4's `GitMetrics.BuildFileInsights` from the single shared numstat parse. Your net-new work is (a) rendering a second, visually-distinct node type on the existing graph, (b) threading the coupled-file list + the existing code-page href resolver into the graph builder, and (c) collapsing the now-redundant *visible* coupled-file list (Story 7.4's "Often changed with") into the graph so the graph is the single relationship surface (AC #2) — while keeping the sr-only text equivalent.

The three highest risks are: **(1)** re-deriving or re-fetching coupling data instead of consuming the `FileInsight` the generator already passes into `RenderPage` (there is **no** new git call and **no** new parse in this story); **(2)** letting a hub file's graph explode — citing artifacts are unbounded today, so "node/edge counts stay bounded" (AC #2) means you must cap **both** populations; **(3)** breaking the a11y contract — the visible surface is just the graph, but a text equivalent of *every* node (artifacts **and** related files) must remain in the sr-only list (NFR6/UX-DR16).

### Owner-selected visual design (locked — this is the #1 review checkpoint for the visual)

The owner chose **shape + color contrast** to distinguish the two node populations (elicited at create-story, per the standing Epic 3 "elicit visual intent" directive — [[create-story-elicit-visual-intent]]):

- **Citing-artifact nodes stay exactly as they are today:** gold-filled circles (`.ref-dot`), solid edges (`.ref-edge`), the compact "Story 7.1"/"ADR 0005" label. **Do not restyle the existing artifact nodes** — the artifact half of the graph must look unchanged.
- **Related-file nodes are a distinct shape in a cooler neutral tone with a dashed edge:** render each as a **rounded square or diamond** (not a circle), filled/stroked with the neutral `--ink-light`/`--border` family (NOT gold, NOT `--status-*`), connected to the center by a **dashed** spoke. The basename (e.g. `Charts.cs`) is the on-graph label; the full path + co-change strength ride the tooltip.
- The two shapes + the solid-vs-dashed edges must read as two populations at a glance, in both light and dark themes, using only the existing neutral/gold/border tokens.

Everything else (center node = the file itself, ring layout, no-JS, pure SVG) stays as `Charts.ReferenceGraph` already does it.

### What already exists (reuse / consume — do NOT rebuild)

- **The co-change data: `FileInsight.CoupledFiles`.** Story 7.4 already produces, per file, `IReadOnlyList<(string Path, int CoChanges)>` — the files most frequently changed alongside this one, **sorted descending by co-change count, capped at `FileInsightCoupledCap = 8`**, derived from the same canonical unordered-pair coupling data the deep-analytics hub uses (respecting the `CouplingFileSetCap` bulk-commit skip). This is precisely AC #1's "files most frequently changed together with this file" + "co-change strength." **Consume it; do not recompute pairs a second way.** [Source: [GitMetrics.cs:135-140](../../src/SpecScribe/GitMetrics.cs) (`FileInsight` record), [GitMetrics.cs:638+](../../src/SpecScribe/GitMetrics.cs) (`BuildFileInsights`); [7-4 story:98-109](7-4-advanced-code-and-git-coverage.md); [[deep-git-single-numstat-path]]]
- **The insight is already passed into the templater.** `GenerateCodePagesInternal` already computes `var insight = _progress?.DeepGit?.FileInsights.GetValueOrDefault(...)` and passes it to `CodeFileTemplater.RenderPage(..., insight, CodePageHref, CommitHref)`. When `--deep-git` is off (or no data), `insight` is null. **Nothing new to compute or thread through the generator** — the coupled-file list rides the `insight` you already receive; you just need to route it (and `coupledFileHref`) into `BuildAside`/the graph rather than only into `BuildCoverageSection`. [Source: [SiteGenerator.cs:1160-1176](../../src/SpecScribe/SiteGenerator.cs)]
- **The guarded code-page resolver: `CodePageHref` (passed as `coupledFileHref`).** A coupled file's repo-relative path → its `code/<path>.html` output-relative path **only when that file has an in-portal page** (i.e. it too is cited by an artifact; `_codePages`). Returns null otherwise. This is exactly the guard AC #1's "linking to those files' code pages" needs — link when the page exists, non-link node otherwise, never a dead link. [Source: [SiteGenerator.cs:1010-1011](../../src/SpecScribe/SiteGenerator.cs)]
- **The graph renderer: `Charts.ReferenceGraph(centerLabel, refs, size)`.** Pure-SVG hub-and-spoke: center chip = the file, one ring node per element of `refs` (`(Href, Title, Short)`), edges first, nodes on top, `role="img"` + summary `aria-label`, canvas grows with node count. This is the method you extend to render a second, distinctly-styled population. [Source: [Charts.cs:1058-1114](../../src/SpecScribe/Charts.cs)]
- **The aside/graph assembly: `CodeFileTemplater.BuildAside`.** Resolves each citing artifact to `(href, title, short)`, calls `Charts.ReferenceGraph(BaseName(path), nodes)`, then emits the sr-only `.ref-list`. This is where the related-file nodes and their sr-only entries get woven in. [Source: [CodeFileTemplater.cs:213-254](../../src/SpecScribe/CodeFileTemplater.cs)]
- **The now-redundant visible list: `BuildCoverageSection`'s "Often changed with" sub-block (`.code-insight-coupled`).** Story 7.4 renders `insight.CoupledFiles` as a *visible* `<ul>` inside the full-width "Advanced coverage" section. Once the graph carries related-file nodes, this visible list is the "redundant visible list" AC #2 forbids. **Remove that sub-block** (the graph + its sr-only equivalent now carry it); keep the section's other sub-parts (change frequency, contributors, change history) intact. [Source: [CodeFileTemplater.cs:135-152](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Existing graph CSS + a11y idiom:** `.ref-graph`/`.ref-edge`/`.ref-dot`/`.ref-center-box`/`.ref-label`/`.ref-node` and the `.sr-only` `.ref-list`. Add sibling classes for the related-file shape/edge; reuse the `<a>`+`<title>`+`aria-label` node pattern (and a non-link `<g>` chip variant for coupled files without a code page). [Source: [specscribe.css:590-684](../../src/SpecScribe/assets/specscribe.css)]

### The core design in one paragraph

`CodeFileTemplater.BuildAside` already builds the citing-artifact node list and calls `Charts.ReferenceGraph`. Give `BuildAside` access to the file's `FileInsight?` and the `coupledFileHref` resolver (both already available in `RenderPage`), and when the insight is present, map `insight.CoupledFiles` to a **second node list** — each entry `(path, coChanges)` → `(href-or-null, fullPath, basename, coChanges)`, where `href` = `coupledFileHref(path)` guarded to a code page. Extend `Charts.ReferenceGraph` to accept this second population and render it as **neutral diamonds with dashed edges** (owner design) around the same ring/center, each an `<a>` to its code page when a href exists or a non-link `<g>` chip otherwise, with a rich `<title>`/`aria-label` = "`<full path>` — changed together N times." Cap **both** node populations so a hub file stays legible (artifacts to a sensible top-N in insertion/deterministic order; coupled files are already capped at 8). Extend the sr-only `.ref-list` with a labelled related-files sub-list (text equivalent of every related node — path + co-change count, linked when a page exists) so nothing is graph-only. Finally, delete Story 7.4's now-redundant visible "Often changed with" list from `BuildCoverageSection`. Everything stays behind the existing `--deep-git` gate: null insight → no related-file nodes → the graph is citations-only, byte-identical to today's aside (AC #1 degradation). No new git call, no new parse, no JS, neutral tokens, everything escaped.

### AC #2's "single relationship surface" — scope this precisely (critical, easy to get wrong)

AC #2 says the graph is "the single relationship surface — no redundant visible list duplicating what the nodes already convey." Concretely:

- **The graph (aside) is the one *visible* place the file's relationships live** — citing artifacts (as today) **and** related files (new). Both node kinds are visible there.
- **Remove Story 7.4's visible "Often changed with" `<ul>`** (`.code-insight-coupled` in `BuildCoverageSection`) — it now duplicates the related-file graph nodes. This is the specific "redundant visible list" the AC targets. The owner's sprint note is explicit: *"Graph becomes the single relationship surface (sr-only text equivalent kept for a11y)."*
- **Keep** the rest of the "Advanced coverage" section: **change frequency**, **contributors to this file**, and **change history** are *not* relationship-node data and are not duplicated by the graph — leave them exactly as Story 7.4 renders them.
- **Keep and extend the sr-only text equivalent.** The visible-list removal must not remove the *accessible* equivalent. The sr-only `.ref-list` (which the graph's `svg role="img"` a11y contract depends on — the SVG exposes only its summary label) must now enumerate **both** artifact links (as today) and related-file entries (path + co-change count, linked to the code page when present). This is the "accessible text equivalent of every node/link is still present" half of AC #2 (NFR6/UX-DR16).

If you delete the visible coupled list *and* fail to add related files to the sr-only list, you've regressed accessibility. The two changes are a pair.

### Node/edge bounding — a real requirement, not a nicety (AC #2)

"node/edge counts stay bounded so a hub file's graph remains legible" is an explicit AC. Today `ReferenceGraph` renders **every** citing artifact with no cap — a heavily-cited hub file (e.g. `SiteGenerator.cs`) already risks an overcrowded ring, and adding up to 8 more nodes makes it worse. Cap **both** populations:

- **Related files** are already capped upstream at `FileInsightCoupledCap = 8` — no extra work, but do not exceed it.
- **Citing artifacts** are currently uncapped. Introduce a deterministic cap (a documented constant, e.g. a top-N such as 12–16) on the artifact ring nodes. When the set exceeds the cap, render the top-N and signal the remainder honestly — the sr-only list should still enumerate **all** citers (accessibility gets the complete set even if the visual is capped), and consider a small "+N more" affordance or a count in the graph's `aria-label`/a summary line. Do **not** silently drop citers from the accessible equivalent. Pick and document the caps; they are seed values, not contracts.

Keep the layout deterministic (same input → same SVG) so the golden/parity fixtures stay stable.

---

## Dependencies / Sequencing

- **Hard prerequisites are already merged/at-review — Stories 7.1 and 7.4.** 7.1 created `CodeFileTemplater`, `Charts.ReferenceGraph`, the aside, and the `_codePages`/`CodePageHref` seam. 7.4 created `FileInsight.CoupledFiles` and threads the per-file insight into `RenderPage`. Both are `review` on `main` — the seams exist on disk today. Read both stories before starting. [Source: [7-1 story](7-1-in-portal-code-file-browsing.md), [7-4 story](7-4-advanced-code-and-git-coverage.md)]
- **Consume the coupling data; do not extend the fetch or the parse.** Unlike 7.4/7.5, 7.8 needs **no** change to `TryComputeDeep`, the pretty-format, or `ParseNumstatLog`/`BuildFileInsights`. The data is already the right shape (`(Path, CoChanges)`, capped, sorted). Touching `GitMetrics` at all in this story is a smell — flag it if you think you need to. [[deep-git-single-numstat-path]]
- **`--code-url` / external mode (Story 7.7) is additive now — code pages always generate.** 7.7 made `CodeSourceBaseUrl` additive: in-portal code pages are always produced and each gains a "view source online" link. So the graph (and related-file nodes) render normally in external mode too; a related-file node still links to the coupled file's *in-portal* code page via `coupledFileHref` (not the external base). No special external-mode handling needed here. [Source: [7-7 story](7-7-external-source-linking-and-auto-detection.md); [[epic-7-code-link-strategy]]]
- **Coupled-file → code-page links stay guarded.** A coupled file only has a code page if it too is cited by an artifact (`_codePages`). `coupledFileHref` returns null otherwise → render the related-file node as a **non-link chip** (still shown, still tooltipped — the coupling is real and worth surfacing), never a dead link. Mirror how 7.4's coupled list already does `<a>`-when-present / plain-otherwise. [Source: [CodeFileTemplater.cs:140-149](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Concurrent in-flight work on `main` — coordinate, don't collide.** Story 7.6 (source-code treemap, `in-progress`) has **uncommitted** working-tree changes right now: a new untracked `src/SpecScribe/CodeMap.cs` and a modified `src/SpecScribe/GitMetrics.cs` (adds `CodeFileMetrics` + `CodeMapMetrics` to `DeepGitPulse`). Those changes are additive and orthogonal to 7.8 (7.6 adds a *whole-codebase* metric map; 7.8 consumes the *per-file* `CoupledFiles` already in `FileInsight`) — but be aware the tree is not clean, and **`main` has a background auto-committer**. Work on your own worktree branch, edit files at the worktree path, and don't re-root paths back at `C:\Dev\SpecScribe`. If you see `GitMetrics.cs`/`CodeMap.cs` churn, it's 7.6, not you. [[worktree-edits-must-target-worktree-path]]

### Scope boundary (read carefully)

- **IN scope:**
  - Rendering a **second, visually-distinct node population** (related files = co-changed code) on the code-page reference graph, sourced from `FileInsight.CoupledFiles`, gated on `--deep-git` + insight present.
  - Guarded clickthrough (related-file node → its `code/<path>.html` page via `coupledFileHref`; non-link chip otherwise) + rich tooltips (full path + co-change strength).
  - **Bounding both node populations** (cap the artifact ring; coupled files already capped) so a hub file's graph stays legible, keeping the layout deterministic.
  - **Removing Story 7.4's redundant visible "Often changed with" list** and **extending the sr-only `.ref-list`** to enumerate related files (path + co-change count, linked when a page exists) — the graph as single visible surface + a complete accessible text equivalent (AC #2).
  - CSS for the related-file shape/edge (neutral tokens) in both themes.
  - Graceful degradation: no deep-git / no insight / no coupling → graph is citations-only, aside byte-identical to today.
- **OUT of scope:**
  - **Any new git call, fetch-format change, or parse.** Consume `FileInsight.CoupledFiles`. [[deep-git-single-numstat-path]]
  - **New coupling computation or a different coupling metric.** The `(Path, CoChanges)` pairs from `BuildFileInsights` are the source of truth.
  - **Changing the artifact-node styling** — the artifact half of the graph looks unchanged (owner design).
  - **File→file *dependency*/call edges.** The related-file edges are **co-change** edges (temporal coupling), not code dependencies — the graph must keep reading honestly (its note already says "not code dependencies"). Do not imply import/call relationships.
  - **Any JavaScript.** Pure HTML/CSS + inline SVG. [[charting-is-pure-svg-no-js]]
  - **`--status-*` lifecycle tokens** on any code surface. Neutral `--ink`/`--gold`/`--border`/`--parchment` only. [[specscribe-status-token-system]]
  - **A new nav entry / new page / new output path.** The change is inside existing code pages.
  - Process-vs-code coupling distinction, dead-zone annotations (that's UX-DR30 / Story 10.6). Don't absorb it.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Consume `insight.CoupledFiles` from the `FileInsight` already passed into `RenderPage`.** Thread the insight (or just its coupled list) plus the existing `coupledFileHref` resolver into `BuildAside` so the graph can render related-file nodes. When `insight` is null (flag off / no data), render **zero** related-file nodes — the aside is byte-identical to today's citations-only graph. [Source: [CodeFileTemplater.cs:38-77](../../src/SpecScribe/CodeFileTemplater.cs), [SiteGenerator.cs:1160-1176](../../src/SpecScribe/SiteGenerator.cs)]
- **Extend `Charts.ReferenceGraph` to render two node populations** — the existing citing-artifact ring (unchanged) plus a related-file set. Prefer an additive overload/optional parameter (e.g. a second `IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)>` for related files) so the existing single-population call sites and tests keep compiling. Related-file nodes: **neutral diamonds/rounded-squares, dashed edges** (owner design), `<a>` to the code page when `Href` is non-null/non-empty, a non-link `<g>` chip otherwise, each with `<title>` + `aria-label` = the full path + "changed together N times." [Source: [Charts.cs:1058-1114](../../src/SpecScribe/Charts.cs)]
- **Lay the two populations out legibly and deterministically.** Keep the center chip (the file) and reuse the ring math; place related-file nodes so they read as a distinct set from the artifact nodes (e.g. interleaved but shape/edge-distinguished, or a sensible arc split — your call, but it must be deterministic and not crowd). Grow the canvas with the *total* node count as today. Same input → identical SVG (golden/parity stability). [Source: [Charts.cs:1066-1076](../../src/SpecScribe/Charts.cs)]
- **Bound both populations (AC #2).** Coupled files are pre-capped at `FileInsightCoupledCap = 8`. Add a documented deterministic cap on the **artifact** ring nodes (e.g. `RefGraphArtifactNodeCap`, ~12–16). Over the cap → render top-N visually, keep the **full** set in the sr-only list, and surface the overflow honestly (count in the `aria-label` and/or a "+N more" affordance). Never drop citers from the accessible equivalent. [Source: [Charts.cs:1079](../../src/SpecScribe/Charts.cs) (aria summary to extend)]
- **Rich tooltips carry the co-change strength (AC #1).** Related-file node `<title>`/`aria-label`: the file (full repo-relative path) and its co-change count with this file — e.g. `"src/SpecScribe/Charts.cs — changed together 7 times"`. Use `Charts.Plural` for the count word. Basename only on the on-graph label; full path in the tooltip. [Source: [CodeFileTemplater.cs:298-314](../../src/SpecScribe/CodeFileTemplater.cs) (`ShortLabel`/`BaseName`), [Charts.cs:1031](../../src/SpecScribe/Charts.cs) (`Plural`)]
- **Guard the clickthrough (AC #1).** Related-file node is a link only when `coupledFileHref(path)` returns a page; prefix it with the page prefix exactly as the coupled-list already does (`prefix + PathUtil.NormalizeSlashes(target)`). Otherwise a non-link chip. Never a dead link. [Source: [CodeFileTemplater.cs:142-146](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Remove Story 7.4's visible "Often changed with" list** (`.code-insight-coupled` block in `BuildCoverageSection`) — the graph now owns that relationship. Leave change-frequency, contributors, and change-history intact. Update `CodeFileTemplaterTests` cases that assert on the visible coupled list to assert on the graph/sr-only equivalent instead. [Source: [CodeFileTemplater.cs:99,135-152](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Extend the sr-only `.ref-list` with a related-files text equivalent.** After the citing-artifact `<li>`s, add the related files (a labelled sub-list or clearly-distinguished items): each shows the path + co-change count, an `<a>` when a code page exists, plain text otherwise. This is the accessible equivalent AC #2 requires. Keep it inside the same `.sr-only` region. [Source: [CodeFileTemplater.cs:238-243](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Neutral tokens only, both themes.** Related-file shape fill/stroke + dashed edge use `--ink-light`/`--border`/`--parchment` family — **never** `--gold` (that reads as an artifact node) and **never** `--status-*`. Verify the diamond/dashed distinction holds in dark mode. [[specscribe-status-token-system]]
- **Escape everything.** Paths, titles, tooltips are free-text/injection surfaces → `PathUtil.Html` on every derived string (as `ReferenceGraph` already does with `Html(...)`). [Source: [Charts.cs:1105-1108](../../src/SpecScribe/Charts.cs)]
- **Keep it pure SVG + CSS, no JS.** The one existing tooltip/copy script is not extended; the node `<title>` is the tooltip (native SVG). [[charting-is-pure-svg-no-js]]
- **Degrade non-fatally (AC #1, NFR2).** No deep-git → `insight` null → no related nodes. Deep-git on but this file has no coupling (empty `CoupledFiles`) → no related nodes, artifact graph unchanged. A coupled file with no code page → non-link chip. Never throw, never a dead link, never an empty related sub-list heading.

### DON'T

- **DON'T add a git call, change the fetch format, or re-parse.** Consume `FileInsight.CoupledFiles`. Editing `GitMetrics` is out of scope here. [[deep-git-single-numstat-path]]
- **DON'T recompute coupling** or invent a second coupling metric. Use the `(Path, CoChanges)` pairs 7.4 already produced.
- **DON'T restyle the citing-artifact nodes.** The artifact half of the graph is unchanged (owner design). Only *add* the related-file population.
- **DON'T leave the redundant visible coupled list in place** (AC #2) — and **DON'T** remove it without adding the related files to the sr-only equivalent (that would regress a11y). The two edits are a pair.
- **DON'T let the graph imply code dependencies.** Edges are co-change (temporal), not calls/imports. Keep the honest framing; don't add dependency language.
- **DON'T exceed the caps** or make the layout non-deterministic (breaks golden/parity + legibility).
- **DON'T use `--status-*` tokens or gold for related-file nodes.** [[specscribe-status-token-system]]
- **DON'T add JavaScript.** [[charting-is-pure-svg-no-js]]
- **DON'T force the Core/Adapters package split** — extend `Charts`/`CodeFileTemplater`/`specscribe.css` in place. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **DON'T use `--output docs/live`** in any manual-verify step — it's vestigial/gitignored; default is `SpecScribeOutput/`. [[generate-output-dir-is-specscribeoutput]]

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **AD-4 — optional insight providers are additive, non-blocking, never own baseline success.** Related-file nodes are opt-in (`--deep-git`) and additive: absence yields a citations-only graph and a fully-generating site. [#AD-4; AC #1 degradation]
- **FR-19 / NFR-1 — baseline stays responsive; opt-in depth is gated.** No new computation at all here — the data rides the already-gated `DeepGitPulse`. Baseline (flag off) timing and output are unchanged. [Source: [SiteGenerator.cs:1160-1162](../../src/SpecScribe/SiteGenerator.cs)]
- **Graceful degradation is contractual (NFR-2, AC #1).** Absent/partial coupling → omitted nodes and plain (never dead) links, never a throw; the artifact graph and baseline page always render.
- **Accessibility is part of the rendering contract (NFR-6, UX-DR16).** The graph `<svg role="img">` exposes only its summary label, so the sr-only `.ref-list` is the real accessible equivalent — it must enumerate **both** node kinds. Node links carry meaningful text (paths, not "click here"); the two populations are distinguished by shape **and** edge style, never color alone. [Source: [CodeFileTemplater.cs:207-243](../../src/SpecScribe/CodeFileTemplater.cs), [specscribe.css:621-633](../../src/SpecScribe/assets/specscribe.css)]
- **Local-only, read-only.** No new reads or writes; output stays under `OutputRoot`. [Inherited Invariants]
- **Seed, not invariant.** Extend `Charts`/`CodeFileTemplater` in place; no `IInsightProvider`/package split. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **Pure-SVG / no-JS.** Reuse the `ReferenceGraph` idiom; native `<title>` tooltips. [[charting-is-pure-svg-no-js]]

### Delivery-adapter note (does this touch webview/SPA parity?)

Code pages are synthesized pages built directly by `CodeFileTemplater` (like `CommitDayTemplater`), **not** routed through `HtmlRenderAdapter.RenderPage`/the `IRenderAdapter` view-model path — so this change does **not** add a `HostRenderException` or a RenderParity registry exception, and doesn't touch the webview/SPA body seams. It *does* change `specscribe.css` bytes and the code-page HTML, so the **golden content fingerprint** will shift (CSS + any code page that has coupling in the golden fixture). The golden fixture cites no real repo files today, so historically only the CSS bytes move (as in 7.1/7.4) — confirm and regenerate the fingerprint with the standard normalizations. [Source: [7-4 story:306](7-4-advanced-code-and-git-coverage.md); [[golden-diff-normalization-gotchas]]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **No new git library, no git call.** The one git seam is untouched. [[deep-git-single-numstat-path]]
- **Existing infra to reuse (do not reinvent):**
  - [Charts.cs:1058-1114](../../src/SpecScribe/Charts.cs) — `ReferenceGraph` (extend for the second population), `Shorten`/`Basename`/`Plural`/`Html`.
  - [CodeFileTemplater.cs](../../src/SpecScribe/CodeFileTemplater.cs) — `RenderPage`, `BuildAside` (weave in related nodes + sr-only entries), `BuildCoverageSection` (remove the coupled `<ul>`), `BaseName`/`ShortLabel`.
  - [GitMetrics.cs:135-140](../../src/SpecScribe/GitMetrics.cs) — `FileInsight.CoupledFiles` (the data; **read-only** here) + `FileInsightCoupledCap`.
  - [SiteGenerator.cs:1010-1011,1160-1176](../../src/SpecScribe/SiteGenerator.cs) — `CodePageHref` (the `coupledFileHref` resolver) + the `RenderPage` call site (thread the insight/resolver into the aside path).
  - [specscribe.css:590-684](../../src/SpecScribe/assets/specscribe.css) — graph/aside CSS to extend with the related-file shape/edge classes.
  - [PathUtil.cs](../../src/SpecScribe/PathUtil.cs) — `Html`/`NormalizeSlashes`/`RelativePrefix`.

---

## File Structure Requirements

**New files:**

- *(None required.)* This is an extension of existing renderers. Add tests to the existing suites (see Testing). If the two-population graph logic grows large, a small private helper on `Charts`/`CodeFileTemplater` is fine, but no new production file or output path is expected.

**Modified files (read fully before editing):**

- `src/SpecScribe/Charts.cs` — extend `ReferenceGraph` to render a second, distinctly-styled related-file population (neutral diamond/rounded-square + dashed edge; `<a>` or non-link `<g>`; rich `<title>`/`aria-label` with co-change strength). Add the artifact-node cap constant + overflow handling in the summary `aria-label`. **Preserve** the existing single-population signature/behavior (additive overload or optional param). [Source: [Charts.cs:1058-1114](../../src/SpecScribe/Charts.cs)]
- `src/SpecScribe/CodeFileTemplater.cs` — (1) thread the file's `FileInsight?`/coupled list + `coupledFileHref` into `BuildAside`; (2) map `CoupledFiles` → related-file nodes and pass them to `ReferenceGraph`; (3) extend the sr-only `.ref-list` with the related-files text equivalent; (4) **remove** the visible "Often changed with" `<ul>` from `BuildCoverageSection`. **Preserve** the a11y shell, the artifact-node path, the placeholder path, and (for null insight) byte-identical aside output. [Source: [CodeFileTemplater.cs:83,135-152,213-254](../../src/SpecScribe/CodeFileTemplater.cs)]
- `src/SpecScribe/SiteGenerator.cs` — only if the aside needs the insight/resolver passed differently: `RenderPage` already receives `insight`, `CodePageHref`, `CommitHref`, `referencedBy`. Most likely **no change** — the templater already has everything; you're re-routing it internally. Confirm and keep any change additive. [Source: [SiteGenerator.cs:1175-1176](../../src/SpecScribe/SiteGenerator.cs)]
- `src/SpecScribe/assets/specscribe.css` — add related-file node classes (e.g. `.ref-file`/`.ref-file-box`/`.ref-file-label` + a dashed `.ref-edge-file`) using neutral tokens; verify light + dark. Add companion `StylesheetTests` assertions if it asserts class presence. **Edit only** `src/SpecScribe/assets/specscribe.css` (any `docs/live` copy is vestigial). [Source: [specscribe.css:635-684](../../src/SpecScribe/assets/specscribe.css); [[generate-output-dir-is-specscribeoutput]]]

**Output layout:** No new paths. Everything renders inside existing `SpecScribeOutput/code/<repo-relative-path>.html` pages; related-file links resolve to sibling `code/…html` pages via `coupledFileHref`.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Chart/templater tests call the render methods directly with synthetic inputs; generation-level tests use the temp-`_bmad-output`-tree (+ a temp git repo for `--deep-git`) with `AssertNoErrors(gen.GenerateAll())`. [Source: [SiteGeneratorCodeInsightsTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs), [CodeFileTemplaterTests.cs](../../tests/SpecScribe.Tests/CodeFileTemplaterTests.cs), [SiteGeneratorTraceabilityTests.cs:106-154](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs)]

**`Charts.ReferenceGraph` tests (unit, no IO):**
- **Two populations render distinctly:** given citing-artifact nodes **and** related-file nodes, the SVG contains the existing `.ref-dot`/`.ref-node` artifact nodes **and** the new related-file shape class + dashed related edges, with the related nodes visually distinct (assert the distinct class names + `stroke-dasharray`/shape marker appear).
- **Related node is a link when a href is present, a non-link chip when null:** a coupled file with a href → `<a href=…>`; without → no `<a>` (a `<g>`/shape only). No dead links.
- **Rich tooltip carries co-change strength:** the related node's `<title>`/`aria-label` includes the full path and the co-change count (e.g. "changed together 7 times").
- **Citations-only (empty related list) is byte-identical to the pre-7.8 single-population output** for the same artifact inputs (proves null-insight degradation and the additive overload).
- **Bounding:** given more artifact nodes than the cap, the SVG renders at most the cap's worth of artifact ring nodes and the `aria-label`/summary reflects the true total (overflow surfaced, not silently dropped).
- **Escaping:** a coupled path / title with `<`/`&`/`"` is escaped.

**`CodeFileTemplaterTests` (unit, no IO) — extend:**
- **Null insight → aside byte-identical to today** (no related nodes, no related sr-only entries) — the citations-only graph is unchanged.
- **Populated insight → related nodes + sr-only entries:** the aside contains related-file graph nodes **and** the sr-only `.ref-list` now enumerates the related files (path + co-change count), linked when a code page exists.
- **AC #2 — no redundant visible list:** `BuildCoverageSection` output no longer contains the visible "Often changed with" `.code-insight-coupled` list; change-frequency/contributors/history are still present.
- **Guarded related-file link:** a coupled file whose `coupledFileHref` returns a page → `<a>` node + sr-only link; one returning null → non-link chip + plain sr-only text.
- **Escaping** of related paths/tooltips in the templater output.

**Generation-level tests (extend `SiteGeneratorCodeInsightsTests`):**
- **Opt-in on:** temp git repo + `DeepGitAnalytics = true` → a referenced code file's `code/…html` page's graph aside carries related-file nodes for its coupled files (positive control: a known coupled path appears as a node + sr-only entry, linked to its own code page when that file is also referenced), and the visible "Often changed with" list is gone.
- **Opt-out off (AC #1 degradation / baseline):** `DeepGitAnalytics = false` → the aside is the citations-only graph, no related nodes, no related sr-only entries; page matches the pre-7.8 baseline aside.
- **Graceful degradation (AC #1, NFR-2):** non-git dir with the flag on → `DeepGit` null → no related nodes, no `Error` outcome, page renders.
- **Coupled file without a code page → non-link node, no dead link.**
- **Determinism:** two runs over the same repo produce identical `code/…html` output (graph included).

**Golden fingerprint:** the CSS + code-page HTML change → regenerate the golden content fingerprint (`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint…`) with the standard normalizations; confirm the golden fixture's page inventory is unaffected (it cites no real repo files, so only CSS bytes should move — verify, don't assume). [[golden-diff-normalization-gotchas]]

**Run:** `dotnet test` from repo root. Then two real passes against this repo (default `SpecScribeOutput/`; **do not** pass `--output docs/live`):
1. **Baseline:** `dotnet run --project src/SpecScribe` (no `--deep-git`) → open a `code/…html` page → confirm the graph is citations-only (no related-file nodes) and no visible coupled list.
2. **Deep:** `dotnet run --project src/SpecScribe --deep-git` → open a well-coupled file's page (e.g. `code/src/SpecScribe/GitMetrics.cs.html` or `SiteGenerator.cs.html`) → confirm: neutral dashed-edge diamond related-file nodes distinct from the gold artifact circles; rich tooltips (path + co-change count); related nodes link to sibling code pages where they exist (plain chip otherwise); the sr-only list enumerates both artifacts and related files; no visible "Often changed with" list; a heavily-cited hub file's graph stays legible (caps hold); no JS; escaped content; and a non-git run degrades cleanly.

---

## Previous Story Intelligence

- **Story 7.1 (In-Portal Code Browsing / graph rework) — the graph you extend.** It made the code page relationship-first: `Charts.ReferenceGraph` (the hub-and-spoke SVG), `CodeFileTemplater.BuildAside` (two-column sticky aside + sr-only `.ref-list`), the `role="img"`/sr-only a11y contract, and the neutral-token rule on code surfaces. The owner's original reopening reason was that an un-highlighted code dump + a giant referenced-by list "inverted the tool's intent" — 7.8 continues that thrust (relationships as the hero, the graph as the single surface). [Source: [7-1 story:365](7-1-in-portal-code-file-browsing.md)]
- **Story 7.4 (Advanced Code & Git Coverage) — the data + the list you fold in.** It built `FileInsight.CoupledFiles` (the exact co-change data), the `--deep-git` gate, the guarded `coupledFileHref`/`CodePageHref` resolver, and the *visible* "Often changed with" list that 7.8 now supersedes with graph nodes. Its "attribution not ranking" discipline and neutral-token rule carry over. Read its `BuildCoverageSection` before removing the coupled sub-block. [Source: [7-4 story:311-314](7-4-advanced-code-and-git-coverage.md); [CodeFileTemplater.cs:95-192](../../src/SpecScribe/CodeFileTemplater.cs)]
- **Story 7.7 (External Source Linking) — additive `--code-url`.** Made in-portal code pages always generate (external is additive), so the graph renders in all modes and related nodes link to in-portal pages. No external-mode special-casing. [Source: [7-7 story](7-7-external-source-linking-and-auto-detection.md)]
- **Recurring lessons to apply:** escaping and stale-output are this renderer's two most common regressions (covered above); keep layout deterministic for golden/parity; grep in-flight/recent story files for stale repeated commands (`--output docs/live`) before closing; extend the monolith in place (no package split). [Source: [7-4 story:218](7-4-advanced-code-and-git-coverage.md); project memory]

## Git Intelligence Summary

Recent commits are Epic-7 code/git-exploration work (`7.4`, `Source Highlighting`, `enhance code browsing with relationship-first view`, `whole-site webview surfaces`). The reference-graph/code-page surface (7.1) and the `FileInsight`/coupling data (7.4) are both on `main` at `review`. **Working tree is not clean:** Story 7.6 (treemap, `in-progress`) has an untracked `src/SpecScribe/CodeMap.cs` and a modified `src/SpecScribe/GitMetrics.cs` (adds `CodeFileMetrics`/`CodeMapMetrics`) — additive and orthogonal to 7.8, but present. **`main` has a background auto-committer** — work on your own worktree branch, edit at the worktree path, don't re-root paths. [[worktree-edits-must-target-worktree-path]]

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Platform notes:
- **Native SVG `<title>` is the tooltip** — no JS, no library; it's what `ReferenceGraph` already uses for artifact nodes. Reuse it for related-file nodes (full path + co-change strength).
- **Two node populations distinguished by shape + edge style, not color alone** (NFR6 / UX-DR16): the diamond/rounded-square shape and the dashed edge are the primary distinguishers; the cooler neutral fill is reinforcing, not sole. This keeps the distinction legible for color-vision-deficient users and in both themes.
- **`stroke-dasharray`** on the related-file spoke is the simplest, dependency-free dashed-edge; keep it in CSS (`.ref-edge-file`) so the SVG stays token-driven and theme-aware.

## Project Context Reference

- FR19 (Epic 7 advanced code-and-git coverage on code pages): [Source: [epics.md:181](../../_bmad-output/planning-artifacts/epics.md)]
- Epic 7 goal + Story 7.8 ACs: [Source: [epics.md:1219,1399-1417](../../_bmad-output/planning-artifacts/epics.md)]
- The graph renderer to extend: [Source: [Charts.cs:1058-1114](../../src/SpecScribe/Charts.cs)]
- The aside/templater to weave into + the coupled list to remove: [Source: [CodeFileTemplater.cs:135-152,213-254](../../src/SpecScribe/CodeFileTemplater.cs)]
- The co-change data (read-only) + cap: [Source: [GitMetrics.cs:135-140](../../src/SpecScribe/GitMetrics.cs); [[deep-git-single-numstat-path]]]
- The guarded resolver + render call site: [Source: [SiteGenerator.cs:1010-1011,1160-1176](../../src/SpecScribe/SiteGenerator.cs)]
- Graph/aside CSS to extend: [Source: [specscribe.css:590-684](../../src/SpecScribe/assets/specscribe.css)]
- Architecture invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]
- Project memory: [[deep-git-single-numstat-path]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[epic-7-code-link-strategy]], [[golden-diff-normalization-gotchas]], [[generate-output-dir-is-specscribeoutput]], [[create-story-elicit-visual-intent]], [[worktree-edits-must-target-worktree-path]].

---

## Tasks / Subtasks

- [ ] **Task 1 — Extend `Charts.ReferenceGraph` for a second node population (AC: #1, #2)**
  - [ ] Add an additive overload/optional parameter carrying the related files: `IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)>` (or equivalent). Existing single-population call sites/tests keep compiling.
  - [ ] Render related-file nodes as **neutral diamonds/rounded-squares with dashed edges** (owner design), `<a>` to the code page when `Href` is present, non-link `<g>` chip otherwise, each with a rich `<title>`/`aria-label` = full path + co-change strength (`Charts.Plural`).
  - [ ] Lay the two populations out deterministically around the same center/ring, growing the canvas with the total node count; keep the artifact ring visually unchanged.
  - [ ] Add a documented artifact-node cap (e.g. `RefGraphArtifactNodeCap`); over the cap → render top-N, reflect the true total in the summary `aria-label`, don't drop citers from the accessible path. Coupled files already capped at 8.
- [ ] **Task 2 — Weave related nodes + sr-only equivalent into `CodeFileTemplater.BuildAside` (AC: #1, #2)**
  - [ ] Thread the file's `FileInsight?` (coupled list) + `coupledFileHref` into `BuildAside`; map `CoupledFiles` → related-file nodes (guarded href via `coupledFileHref`), pass both populations to `ReferenceGraph`.
  - [ ] Extend the sr-only `.ref-list` with a labelled related-files text equivalent (path + co-change count, linked when a page exists) — after the artifact `<li>`s, in the same `.sr-only` region.
  - [ ] Null insight / empty coupling → zero related nodes, zero related sr-only entries → aside byte-identical to today (citations-only).
- [ ] **Task 3 — Remove the redundant visible coupled list (AC: #2)**
  - [ ] Delete the visible "Often changed with" `.code-insight-coupled` `<ul>` from `BuildCoverageSection`; keep change-frequency, contributors, and change-history intact.
- [ ] **Task 4 — Styling (AC: #1)**
  - [ ] Add related-file node classes (shape box/label + dashed `.ref-edge-file`) to `specscribe.css` using neutral tokens (not gold, not `--status-*`); verify light + dark, distinct-by-shape-and-edge (not color-only). Update `StylesheetTests` if it asserts class presence. **No JS.**
- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] `Charts.ReferenceGraph`: two-population distinctness, linked-vs-chip, rich tooltip w/ co-change strength, citations-only byte-identity, artifact-node cap + honest overflow, escaping.
  - [ ] `CodeFileTemplaterTests`: null→baseline aside; populated→related nodes + sr-only entries; no visible coupled list; guarded related link; escaping.
  - [ ] Generation-level (`SiteGeneratorCodeInsightsTests`): opt-in on → related nodes present + coupled list gone; opt-out off → citations-only baseline; non-git → degrades; coupled-without-page → non-link; determinism.
  - [ ] Regenerate the golden content fingerprint (CSS-driven) with standard normalizations; confirm inventory unaffected.
- [ ] **Task 6 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green. Baseline generate (no `--deep-git`) → citations-only graph, no visible coupled list. Deep generate (`--deep-git`) → distinct dashed-diamond related nodes with tooltips + guarded links, complete sr-only equivalent, legible hub graph, no JS, escaped content; non-git run degrades cleanly.

## Dev Notes

- **This is a rendering + wiring story — no git work.** The coupling data (`FileInsight.CoupledFiles`) is already computed, bounded, sorted, gated, and passed into `RenderPage`. If you find yourself editing `GitMetrics` or adding a git call, stop — that's a smell. [[deep-git-single-numstat-path]]
- **AC #2 is a *pair* of edits.** Remove the visible coupled list **and** add related files to the sr-only equivalent in the same change. Doing only the first regresses accessibility.
- **Two populations, distinguished by shape + edge (owner-locked).** Gold circles + solid edges = citing artifacts (unchanged). Neutral diamonds + dashed edges = related files. Never color alone; verify both themes. [[create-story-elicit-visual-intent]]
- **Bound the graph.** Coupled files are pre-capped at 8; cap the artifact ring too (documented constant) and surface overflow honestly in the accessible summary — a hub file must stay legible without hiding citers from assistive tech. [AC #2]
- **Guard, don't probe.** Related-file link only when `coupledFileHref` returns a page (cached `_codePages`), never a filesystem probe, never a dead link.
- **No JS. Neutral tokens. Escape everything. Deterministic layout.** [[charting-is-pure-svg-no-js]] [[specscribe-status-token-system]]
- **Concurrent 7.6 in the tree.** Untracked `CodeMap.cs` + modified `GitMetrics.cs` are 7.6's treemap work, orthogonal to yours. Worktree branch; edit at the worktree path; `main` auto-commits. [[worktree-edits-must-target-worktree-path]]

### Project Structure Notes

- All changes extend existing renderers: `Charts.ReferenceGraph`, `CodeFileTemplater` (`BuildAside` + `BuildCoverageSection`), `specscribe.css`. No new production file, no new output path, no package restructure (deferred seed, Epics 4/6). Most likely `SiteGenerator.cs` needs no change (the templater already receives the insight + resolver).
- Code pages are synthesized (not `IRenderAdapter`-routed) → no new `HostRenderException`/RenderParity registry exception; only the golden content fingerprint moves.

### References

- [Source: [epics.md:1399-1417](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.8 user story + both ACs.
- [Source: [epics.md:1217-1221,181](../../_bmad-output/planning-artifacts/epics.md)] — Epic 7 goal + FR19 coverage.
- [Source: [src/SpecScribe/Charts.cs:1058-1114](../../src/SpecScribe/Charts.cs)] — `ReferenceGraph` to extend for the second population.
- [Source: [src/SpecScribe/CodeFileTemplater.cs:135-152,213-254](../../src/SpecScribe/CodeFileTemplater.cs)] — `BuildAside` (weave in) + `BuildCoverageSection` coupled list (remove).
- [Source: [src/SpecScribe/GitMetrics.cs:135-140](../../src/SpecScribe/GitMetrics.cs)] — `FileInsight.CoupledFiles` (read-only) + `FileInsightCoupledCap`.
- [Source: [src/SpecScribe/SiteGenerator.cs:1010-1011,1160-1176](../../src/SpecScribe/SiteGenerator.cs)] — `CodePageHref` resolver + `RenderPage` call site.
- [Source: [src/SpecScribe/assets/specscribe.css:590-684](../../src/SpecScribe/assets/specscribe.css)] — graph/aside CSS to extend.
- [Source: [7-1-in-portal-code-file-browsing.md](7-1-in-portal-code-file-browsing.md)] — graph rework, aside, a11y contract, neutral-token rule.
- [Source: [7-4-advanced-code-and-git-coverage.md](7-4-advanced-code-and-git-coverage.md)] — `FileInsight.CoupledFiles`, `--deep-git` gate, guarded resolver, the coupled list superseded here.
- [Source: [7-7-external-source-linking-and-auto-detection.md](7-7-external-source-linking-and-auto-detection.md)] — additive `--code-url`.
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant).
- [Source: [tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs), [tests/SpecScribe.Tests/CodeFileTemplaterTests.cs](../../tests/SpecScribe.Tests/CodeFileTemplaterTests.cs)] — test shapes to extend.
- [[deep-git-single-numstat-path]] / [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[epic-7-code-link-strategy]] / [[golden-diff-normalization-gotchas]] / [[generate-output-dir-is-specscribeoutput]] / [[create-story-elicit-visual-intent]] / [[worktree-edits-must-target-worktree-path]] — project memory.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
