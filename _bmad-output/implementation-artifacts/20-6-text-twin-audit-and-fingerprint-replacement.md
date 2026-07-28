---
baseline_commit: 86b35c267241c15b05c64e3aaa3e13cce58198b2
---

# Story 20.6: Text-Twin Audit and Golden-Fingerprint Replacement — the ADR 0013 Gate

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Epic:** [Epic 20 — Interactive Project Explorer, Standardized Hierarchy Explorer on Plotly](../planning-artifacts/epics.md#epic-20-interactive-project-explorer--standardized-hierarchy-explorer-on-plotly)
**Design-locked by:** [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) (§2 the twin contract, §3 the hard per-surface gate, §6 the fingerprint replacement) and [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
**Blocked by:** **Story 20.5 must land first.** AC#2 asserts on `HierarchyExplorer`'s island + config + twin, which 20.5 creates. It is **in flight right now** — see § Concurrent work below.
**Gates:** Story 20.7. No SVG retires anywhere until this story's audit record says that surface's twin passes.
**Baseline commit:** `92fa581`

## Story

As a maintainer retiring server-rendered chart SVG,
I want every affected surface's text twin audited complete and the chart regression net rebuilt before any SVG is deleted,
so that ADR 0013's no-JS contract is proven rather than assumed, and chart regressions stay caught through the transition.

---

## ⛔ Read first — what this story IS and IS NOT

**IS:** produce the **durable per-surface audit record** for all seven rollout surfaces, verified in a live browser with JavaScript disabled; **fix the dashboard/epics twin** (the surface 20.7 retires first); and **land the golden-fingerprint replacement assertions** before any SVG is deleted.

**IS NOT:**

| Not this story | Whose it is | Why |
|---|---|---|
| Retiring **any** server-rendered chart SVG | 20.7 | This story is the *gate*, not the retirement. It produces the permission; it does not exercise it. |
| Fixing the other **five** surfaces' twins | 20.7, per-surface (owner decision D2) | AC#1's own escape valve: *"or keeps its server-rendered SVG until it is."* A recorded FAIL + a retained SVG is a **passing** outcome for this story. |
| Converting call sites to the component | 20.7 | Inventory rollout. |
| Deleting `Charts.cs` entry points / `specscribe.js` arc renderers | 20.7 AC#2 | Still the live fallback for six surfaces. |
| Replacing `Charts.SunburstCompanionList` | Nobody — it **stays** (owner decision D4) | It is a designed visible navigation panel, not an accessibility artifact. |
| Building the component, the island, or the base twin | 20.5 | Already specified and in flight. This story *audits and configures* it. |
| The VS Code webview twin fallback (ADR 0013 §7) | 20.7 (its D4-deferred decision) | 20.7 owns `RenderParity` and the ADR 0005 CSP amendment. |

---

## Owner decisions locked at create-story (2026-07-25)

**D1 — Twin shape: hybrid.** 20.5's `HierarchyExplorer.TextTwinHtml` is **the** standard twin and is adopted per-surface as 20.7 converts. Surfaces that already ship a genuinely **richer** natural twin — Code Map's per-variant file table, Impact Map's `<details open>` grouped list — are **audited and kept**, never replaced by the generic nested list. Only surfaces with no real twin get new markup. Rationale: the generic twin is weaker than either of those two (the file table carries per-file git metrics; the impact list groups by epic), and replacing them would re-litigate two shipped, reviewed designs.

**D2 — Fix scope: audit all seven, fix one.** This story fixes **dashboard + epics** only. The other five are **recorded as failing and keep their SVG** — the per-surface gate stays honest, 20.6 stays landable, and 20.7 fixes each twin as it converts that surface. A recorded FAIL is a deliverable here, not a defect.

**D3 — Twin visibility: collapsed `<details>`, closed.** Matches what 20.5 already emits (`<details class="ss-hierarchy-twin">`, no `open`) and ADR 0013 §2. `<details>` works without script, so a JS-off visitor reaches it in one click.

**D4 — Dashboard: keep both.** `Charts.SunburstCompanionList` ("Remaining Work by Epic") **stays visible and unchanged**; the component twin goes **`sr-only`** on the dashboard and epics index. The tile grid keeps its product value as a navigation aid; the component twin discharges the completeness contract. **This requires a new twin-presentation setting on `HierarchyExplorerConfig`, which does not exist yet — see Task 3.**

> **D3 and D4 are not in conflict.** D3 sets the component's *default* presentation (closed `<details>`). D4 *overrides* it on the two surfaces that already carry a visible companion panel. The knob added in Task 3 is what makes both true at once.

---

## Acceptance Criteria

*Verbatim from [`epics.md` § Story 20.6](../planning-artifacts/epics.md). D1–D4 constrain how they are met; they do not amend them.*

1.
**Given** ADR 0013's hard per-surface gate and the Epic 20 rollout inventory
**When** each surface's text twin is audited
**Then** every twin is server-rendered, complete (no fact exists only inside the chart), navigable (every link a chart node offers resolves), and non-color (UX-DR17/UX-DR19) — verified **in a live browser with JavaScript disabled**, not by test assertion alone (CLAUDE.md § Verification)
**And** any surface whose twin is incomplete is fixed here, or keeps its server-rendered SVG until it is — the gate is per-surface and blocking.

2.
**Given** `GoldenContentFingerprint` currently derives most of its signal from chart SVG (measured at 69.3% of the dashboard body by the Story 23.1 spike)
**When** charts become client-rendered
**Then** the replacement assertions cover what is now server-rendered — the embedded payload, the component configuration, and the text twin — and land in this story, before the first SVG retirement
**And** the regenerated fingerprint is confirmed stable across two repeated runs before being locked in, naming whose concurrent changes it sits on top of (CLAUDE.md § Concurrent work).

---

## 🔍 The audit inventory — pre-read at create-story, 2026-07-25

**These are code-verified starting positions, not verdicts.** AC#1 requires you to confirm each one in a live browser with JS off. Recorded here so the audit is a check against a stated hypothesis rather than a blank-page exercise — and so a surface that quietly *passes* is not mistaken for one nobody looked at.

| # | Surface | Chart entry point(s) | Twin today | Hypothesis |
|---|---|---|---|---|
| 1 | **Dashboard** `index.html` | `Charts.Sunburst` (+ 20.5's component) | `SunburstCompanionList` tile grid + 20.5's `ss-hierarchy-twin` | **PARTIAL → FIXED HERE** |
| 2 | **Epics index** `epics.html` | `Charts.Sunburst` | `SunburstCompanionList` (same call) | **PARTIAL → FIXED HERE** |
| 3 | **Epic detail** `epics/epic-N.html` | `Charts.EpicSunburst` | `role="img" aria-label="Epic story breakdown"` + the page's story cards | **FAIL** |
| 4 | **Story detail** `epics/story-N-M.html` | `Charts.TaskSunburst` | summary `aria-label` + the story doc's own task list | **PROBABLE FAIL** |
| 5 | **Code Map** `code-map.html` | `CodeTreemap` + `CodeMapSunburst`, **×4 variants = 8 charts** | `AppendFileTable` per variant — every file as a plain `<tr>` | **PROBABLE PASS** |
| 6 | **Git Insights** `git-insights.html` | `CodeOwnershipSunburst` + `CodeOwnershipTreemap` | **none** | **FAIL — the hardest gap** |
| 7 | **Impact Map** `impact-map.html` | client-rendered treemap/sunburst (Story 21.3) | `<details open>` + `Charts.ImpactMapBody` | **PROBABLE PASS — the exemplar** |

### The four findings that carry the most weight

**F1 — `SunburstCompanionList` is not a twin, and cannot become one (`Charts.cs:648`).** It is **epic-level only**: the sunburst draws three rings (epics → **stories** → **open/done follow-up aggregates**) and the tile grid emits one tile per epic plus Follow-ups and Unplanned roots. The story ring and the follow-up ring exist **only inside the chart**. Worse, `Charts.cs:668` *deliberately omits* any epic that is `done` with zero open follow-ups — a fact the chart draws and the grid does not state. That omission is correct for a "what's left" panel and disqualifying for a twin, which is exactly why **D4 keeps both**.

**F2 — Git Insights has no twin at all, and its stated contract is stale.** `GitInsightsTemplater.cs:10` records that Story 7.11 *"replaces the earlier files-and-contributors master-detail table AND the earlier plain ranked ownership table."* Both tables were **deleted**. What remains is `CodeOwnershipSunburst`'s aggregate `aria-label` — a sentence of counts (`Charts.cs:3941`), not an enumeration. `GitInsightsTemplater.cs:14-17` then states the page's no-JS contract as *"NFR-5, reinterpreted by ADR 0010 for this opt-in surface: a real, useful default-mode chart … renders and works with JS off."* **That is precisely the ADR 0010 §2 clause ADR 0013 §4 supersedes.** This surface's entire no-JS story rests on the SVG that 20.7 wants to delete, with nothing underneath. Record it as FAIL, fix it in 20.7, **and correct that doc comment** (Task 2.4) so the next reader is not misled by a superseded contract.

**F3 — Code Map's table is genuinely server-complete.** `specscribe.js:875` states it plainly: *"the server ships every file as a plain `<tr>` inside `.codemap-table`"* — JS only paginates. A JS-off visitor gets **every row**. Confirm the rows carry the file links and enough directory context to stand in for the hierarchy, but do not assume a pager implies truncation.

**F4 — Impact Map is the pattern the others should copy.** `ImpactMapTemplater.cs:56-62` server-renders `<details open>` with the full epic-grouped file list and lets the script collapse it once the chart is live. It was built (Story 21.3) under the assumption the chart is client-only — which is the world ADR 0013 creates. Cite it in the audit record as the reference implementation.

**Not an eighth surface:** ADR 0013's Context names *"the ownership and freshness views."* Verified 2026-07-25 — **freshness is a colorize *dimension* of the Code Map** ("Recently changed" / "First changed", `CodeMapTemplater.cs:204-205`), not a separate chart. The verified inventory is seven surfaces. Say so in the audit record so the discrepancy is closed rather than rediscovered.

---

## Tasks / Subtasks

### Task 1 — Set up the audit method (AC: #1)

- [x] 1.1 Generate to `SpecScribeOutput/` **with `--deep-git`** — without it `code-map.html`, `git-insights.html` and `impact-map.html` do not render and four of the seven surfaces cannot be audited at all. Never `--output docs/live`.
- [x] 1.2 Serve it (`.claude/launch.json` → `specscribe-output`, port 8099) and **disable JavaScript in the browser** for the whole audit. Not "assume JS off" — actually turn it off. CLAUDE.md § Verification: the suite structurally cannot see what a JS-off visitor receives, and this story exists because of that gap.
- [x] 1.3 Fix the four audit predicates before looking at anything, and apply them identically to all seven surfaces:
      **(a) Server-rendered** — present in `view-source:`, not injected. Check the source, not the DOM.
      **(b) Complete** — every fact the chart conveys is stated: every node's label, its **prose** status, and its magnitude. Enumerate the chart's node set from the payload/SVG with JS *on*, then diff it against the twin's entries with JS *off*. A count match is not a set match.
      **(c) Navigable** — every link a chart node offers exists in the twin **and resolves**. Follow them; a 404 in a twin is information loss, not a broken link.
      **(d) Non-color** — every status readable as a word, every metric as a number (UX-DR17/UX-DR19). Confirm nothing is signalled by hue alone.

### Task 2 — Audit all seven surfaces and write the durable record (AC: #1)

- [x] 2.1 Audit each surface in the inventory table against 1.3(a)–(d). Record a **per-predicate** verdict, not one verdict per surface — "FAIL" without naming which predicate failed gives 20.7 nothing to act on.
- [x] 2.2 Write the record to **`_bmad-output/implementation-artifacts/20-6-text-twin-audit.md`** — a durable artifact, like `20-4-spike-report.md`, **not** a Dev Agent Record bullet. Story 20.7 reads this file as its per-surface permission slip and its work list. One section per surface: entry point, twin as-shipped, four predicate verdicts with evidence, and — for each failure — **the specific missing facts**, so 20.7 implements against a list rather than re-auditing.
- [x] 2.3 For every surface that fails, state explicitly: **this surface keeps its server-rendered SVG.** That sentence is the gate. 20.7 must not be able to read the record as ambiguous.
- [x] 2.4 **Correct `GitInsightsTemplater.cs`'s stale progressive-enhancement doc comment** (`:14-17`) — it cites ADR 0010 §2, which ADR 0013 §4 supersedes. Replace it with the ADR 0013 contract and a pointer to this story's audit record. Doc-only; no rendering change. (F2)
- [x] 2.5 Record the freshness-view discrepancy resolution from the inventory note above, so ADR 0013's prose and the verified seven-surface inventory stop disagreeing.

### Task 3 — Fix the dashboard + epics twin (AC: #1) — the only surface fixed here (D2)

- [x] 3.1 **Add the twin-presentation knob to `HierarchyExplorerConfig`** (`HierarchyExplorer.cs:51`). It does not exist today: the record is `(DomId, Shape, Mode, HashKey, Size, Labels, Meta)` and `TextTwinHtml` hard-codes `<details class="ss-hierarchy-twin">`. Add an enum-typed field (e.g. `HierarchyTwinDisplay { Details, ScreenReaderOnly }`) defaulting to `Details` (D3), and have `TextTwinHtml` emit `<div class="ss-hierarchy-twin sr-only">` instead of the `<details>` when the config says `ScreenReaderOnly`. **Config-driven, never a call-site literal** — this is the same discipline that keeps `Size` out of the JS.
- [x] 3.2 Set the dashboard and epics-index instances to `ScreenReaderOnly` (D4). `SunburstCompanionList` and the 20.3 rail stay exactly as they are — **do not touch them**.
- [x] 3.3 Verify the twin is **complete against the chart's own node set**, using 1.3(b)'s diff. The twin is built from `model.Nodes`, and `HierarchyExplorer.ProjectDashboard` adapts `Charts.SunburstExplorerNodes` — so completeness reduces to whether the payload node set equals what the SVG draws. `SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew` already protects that invariant; **confirm it still holds after 20.5's `Reparent` pass** rather than assuming it, since 20.5 synthesizes a root the SVG never drew.
- [x] 3.4 Confirm the twin's `sr-only` variant is still reachable: an `sr-only` node must remain in the accessibility tree and its links must stay focusable. `sr-only` is not `display:none` — verify in the browser that the links are tab-reachable and announced, because this is the exact class of defect (`display:none` vs. focusability) the 20.2 review caught live and the suite missed.
- [x] 3.5 **Epics index:** verify the component is actually mounted there. 20.5's D1 mounts the component on **the dashboard only** — if `epics.html` has no component instance yet, its twin fix belongs to 20.7's conversion of that call site, and this story records it as such. **Check before fixing; do not mount a second instance here** — that is 20.7's rollout.

### Task 4 — The golden-fingerprint replacement assertions (AC: #2)

- [x] 4.1 **State the honest scope limit first, in the test's own comment.** The golden fixture is **not a git repo and cites no real repo files**, so `code-map.html`, `git-insights.html` and `impact-map.html` **never render in it** (confirmed against `GoldenOutputInventory`, `SiteGeneratorAdapterTests.cs:190-224`). The fingerprint is therefore the net for the *planning* hierarchy surfaces only; the code/git surfaces are netted by their own templater tests. Do not let the replacement imply portfolio-wide chart coverage it has never had.
- [x] 4.2 Add targeted assertions in `HierarchyExplorerTests.cs` (20.5's file — **extend, do not fork**) over the three now-server-rendered things ADR 0013 §6 names. The exact shapes, read from the live emitter at `HierarchyExplorer.cs:308-341`:
      - **The embedded payload** — `<script type="application/json" class="ss-hierarchy-data" id="{DomId}-data">`, nodes carrying `id, parentId, label, value, statusClass, statusLabel, href, kind`.
      - **The component configuration** — `config { domId, shape, mode, hashKey, size, labels, branchvalues }`. Assert `branchvalues` **equals `HierarchyExplorer.BranchValues`** and that the payload is genuinely parent-inclusive; a payload/`branchvalues` mismatch renders blank or wrong with only a console warning, so it must be asserted, never assumed.
      - **The text twin** — every payload node appears in the twin with a **prose** status word (not a CSS class) and a resolving `href`. This is the assertion that makes the audit *repeatable* instead of a one-time inspection, and it is the durable half of AC#1.
- [x] 4.3 **Fold vendored assets out of `FingerprintTree`.** `SiteGeneratorAdapterTests.cs:1011-1022` `File.ReadAllText`s **every** file in the output tree, so 20.5's 1.2 MB `plotly-hierarchy.min.js` now flows through `NormalizeVolatile`'s regexes and into the hash — slow, and it makes the golden constant hostage to a vendor rebuild that changes no SpecScribe rendering. Replace vendored asset **content** with a `<vendored asset: {name}, {N} bytes>` token so a size change still trips the hash but a re-minification of identical upstream does not. Apply it to `prism.js`/`prism.css` on the same rule — **one predicate, not a plotly special case.** (Flagged by 20.5 Task 8.7, which explicitly hands the broader replacement here.)
- [x] 4.4 Do **not** delete or weaken the existing fingerprint test. Chart SVG is still server-rendered on every surface at this point — the replacement runs *alongside* the existing net through the transition, exactly as ADR 0013's "period where chart regressions are less well netted" consequence anticipates. The SVG-derived coverage evaporates in 20.7, not here.

### Task 5 — Regenerate and stabilize the fingerprint (AC: #2)

- [x] 5.1 The constant at `SiteGeneratorAdapterTests.cs:984` is `9288bf5595ffa883edb21eef021e86caaf5c6a2a06a30f23ca1a99ddb7343836`. Task 3's twin change and Task 4.3's vendored-asset folding both move it. **Expect it to have moved under you already** — 20.5 is landing in the same tree.
- [x] 5.2 **Confirm the regenerated hash is stable across two repeated full runs** before locking it in. A first-captured hash off a stale build is the known trap (memory: `golden-diff-normalization-gotchas`).
- [x] 5.3 **Name whose concurrent changes it sits on top of** in the regeneration comment — CLAUDE.md § Concurrent work, and AC#2 requires it in words. At draft time that is Story 20.5's in-flight work; verify what it actually is when you regenerate rather than copying this sentence.
- [x] 5.4 Add the regeneration comment to the existing stacked block in the house style: what changed, why it is a deliberate reviewed rendering change, and what did **not** change.

### Task 6 — Verify and report (AC: #1, #2)

- [x] 6.1 Re-run the whole audit **after** Tasks 3–5, JS off, in the browser. A fix verified only by test assertion has not been verified — that is the specific failure this story exists to prevent.
- [x] 6.2 **Screenshot the dashboard with JS disabled.** It is the single most useful artifact this story can hand the owner: it shows exactly what a no-JS visitor gets once 20.7 lands. The 20.4 spike owed a pixel view and could not composite one; if the pane again refuses, say so and fall back to computed-geometry evidence — but try.
- [x] 6.3 Run the full suite and report real numbers. Two git-fixture tests are known to flake under parallel load (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything you caused.
- [x] 6.4 In the Completion Notes, state plainly **which surfaces passed and which keep their SVG**, and link the audit record. That list is 20.7's entry condition.

---

### Review Findings

*(populated by code-review at epic end — Epic 20's review runs once every story is complete and the owner is satisfied)*

**Scope:** reviewed only this story's own File List, diffed `baseline_commit` (`86b35c2`) → the landing commit (`32fd282`,
"Overnight work", which bundles 18.1/18.2/18.3/20.6/23.3/23.5). All sibling-story files in that commit were excluded.
Three layers ran (Blind Hunter, Edge Case Hunter, Acceptance Auditor). Acceptance Auditor found **no AC violations**
— AC#1 and AC#2 are met by the diff and are internally consistent with the durable audit record and ADR 0013/0012.

- [x] [Review][Patch] `.ss-hierarchy-twin.sr-only:focus-within` uses an undefined CSS custom property (`--surface`), so the revealed panel always renders a plain white box instead of a themed panel [src/SpecScribe/assets/specscribe.css:446] — fixed: swapped to `--warm-white` (the token every other `.chart-panel` background already uses); verified live, revealed panel computes to `rgb(250, 247, 242)`
- [x] [Review][Patch] Sr-only twin's `<h3>{Title} — full text listing</h3>` duplicates the same-level `<h3>{Title}</h3>` `Charts.Framed` already emits for the panel — two same-level headings for one landmark [src/SpecScribe/HierarchyExplorer.cs:611, src/SpecScribe/Charts.cs:170] — fixed: demoted the twin's heading to `<h4>`; verified live, the panel now nests one `<h3>` + one `<h4>`
- [x] [Review][Patch] `TwinDisplay`'s XML doc claims "the same discipline that keeps Size out of the JS", but unlike `Size`, `TwinDisplay` never reaches `IslandHtml`'s emitted client config at all — the analogy is inaccurate [src/SpecScribe/HierarchyExplorer.cs:94] — fixed: reworded to state `TwinDisplay` is server-only and never serialized to the client
- [x] [Review][Patch] New comment cites `Charts.cs:668` by line number, which this project's own convention flags as fragile (memory: cite-adrs-by-symbol-not-line-number) [src/SpecScribe/DashboardViewBuilder.cs:169] — fixed: cited by symbol (`Charts.SunburstCompanionList`) instead
- [x] [Review][Patch] Test comment "The real shape a converted surface ships" hardcodes `<details>`, but the dashboard (the surface it's modeled on) now ships `<section sr-only>` post-diff — comment is stale, assertions remain valid [tests/SpecScribe.Tests/SunburstExplorerTests.cs:208] — fixed: reworded to note the fixture is a generic stand-in, not either surface's exact wrapper
- [x] [Review][Patch] `srOnly` is derived via a direct `==` comparison rather than an exhaustive switch; an out-of-range `HierarchyTwinDisplay` value would silently take the `Details` branch [src/SpecScribe/HierarchyExplorer.cs:602] — fixed: switch expression with a `default` arm throwing `ArgumentOutOfRangeException`
- [x] [Review][Defer] No automated test guards that `git-insights.html` keeps its SVG until a twin exists — only a doc comment states the constraint today; enforcement is Story 20.7's gate-read of the audit record [src/SpecScribe/GitInsightsTemplater.cs] — deferred, out of this story's declared scope (Task 2.4 was doc-only)

**Patches applied (2026-07-27):** all 6 fixed as noted above. Targeted suite re-run green (73/73: `HierarchyExplorerTests`,
`SunburstExplorerTests`, `SiteGeneratorSpaTests`, `GoldenContentFingerprint`). The h3→h4 and CSS-token changes moved
the golden fingerprint; regenerated `3171cf5c…` (Story 20.8's value) → `06788c0fa85928924226617c767a808c85ae5a55fd9a8d9b3fb1757769a95e61`,
confirmed stable across two repeated runs, stacked provenance comment added at `SiteGeneratorAdapterTests.cs`. One
transient build break from a concurrent session's in-flight work (`TestArtifactDiscovery.cs`/`SiteGenerator.cs`) was
encountered between runs and resolved on its own without any action taken on those files, per CLAUDE.md § Concurrent work.

**Dismissed as noise or already addressed (8):** `EpicsViewBuilder.cs` not given the D4 `ScreenReaderOnly` treatment (confirmed via `git blame` to postdate this diff — it's Story 20.7's own conversion); the sr-only reveal CSS reflow "not verified live" (Completion Notes already record live verification: 212 links, 0 untabbable, 1×1→669×5680 on focus); new tests validating the component "in isolation" (the golden-fingerprint test covers the real production wiring end-to-end; unit-level isolation is appropriate for the rest); the fingerprint stability claim being unverifiable in the diff (Completion Notes record the confirmation); the removed "not yet load-bearing" framing sentence (superseded by the new, more detailed rationale); the 40+ line regeneration comment's length (matches this codebase's established house style for prior golden-fingerprint changes); Task 3.1's illustrative `<div>` vs. the shipped `<section>` (a deliberate, disclosed improvement); the sr-only twin still requiring 212 tab-stops before reveal (already flagged in the story's own Completion Notes as an owner-verify-round item).

---

## Dev Notes

### Why this story exists, in one paragraph

ADR 0013 traded SpecScribe's signature visuals for one chart implementation instead of two. That trade is only honest if the text twin actually carries the information the picture used to — and ADR 0013 §Context admits coverage is *"partial and uneven"* without knowing how uneven. This story measures it. The measurement is the deliverable: 20.7 is not permitted to delete a single `<path>` on a surface this story has not cleared. The second half is the safety net — the golden fingerprint currently gets **69.3% of the dashboard body's signal from chart SVG**, and that coverage evaporates the moment charts go client-rendered. Replacing it *before* the first retirement is what keeps the transition netted.

### The three things a twin must carry that a chart conveys implicitly

The completeness predicate is easy to under-apply, because a chart communicates things nobody wrote down. When auditing, look for all three:

1. **Membership** — which nodes exist. Easiest to check, and the only one most twins get right.
2. **Magnitude** — how big each node is. A sunburst says this with sweep angle. A twin must say it as a **number**. 20.5's twin does (`weight {n.Value}`).
3. **Structure** — what contains what. A sunburst says this with rings. A twin must say it with **nesting**. A flat list of every node is complete on membership and silent on structure, and that is the most likely way a surface fails predicate (b) while looking fine.

### Architecture compliance

- **ADR 0013** — §2 the four-part twin contract; §3 the hard per-surface gate verified live with JS off; §6 the fingerprint replacement lands before the first retirement. This story is the direct implementation of §3 and §6.
- **ADR 0013 §4 supersedes ADR 0010 §2.** Any code or comment still asserting "a real, useful default-mode chart must render with JS off" is stale. `GitInsightsTemplater.cs:14-17` is one such site (Task 2.4); grep for others while you are in there.
- **ADR 0012 §2** — one component is the only route to a hierarchy chart. This story adds a *setting* to it (Task 3.1), never a second twin implementation.
- **NFR-5 as amended by ADR 0013** — JS-off may lose the *visualization*; it must never lose **information** or **navigation**. Those two words are the audit's whole standard.
- **UX-DR17 / UX-DR19 / UX-DR21** — never color-only; every metric has a non-color text equivalent; chart text twins are accessibility contract and are never removed.
- **AD-2 / ADR 0002** — the twin-display setting is host-neutral view-model config. It belongs on `HierarchyExplorerConfig`, not in `HtmlRenderAdapter`.
- **FR31** — generation-time determinism; the fingerprint work must not introduce a nondeterministic token.

### Anti-patterns to prevent

1. **Auditing with JavaScript on.** The entire premise of this story is that the JS-on view lies about what a JS-off visitor gets. Turn it off, in the browser, and leave it off.
2. **Grepping the HTML instead of loading the page.** Server-rendered is necessary, not sufficient — a twin can be present in source and unreachable, unfocusable, or pointing at 404s.
3. **Treating a count match as a set match.** "The twin has 26 entries and the chart has 26 wedges" proves nothing about *which* 26. Diff the sets.
4. **Recording a bare "FAIL".** 20.7 needs the missing facts enumerated, not a verdict.
5. **Fixing the other six surfaces because the gaps are visible and annoying.** D2 scopes this story to one. A recorded FAIL plus a retained SVG is the *designed* outcome.
6. **Replacing `SunburstCompanionList`, the 20.3 rail, Code Map's file table, or Impact Map's `<details>` list.** D1/D4. Three of those four are richer than what would replace them.
7. **Deleting or weakening the existing golden fingerprint test.** The replacement runs alongside it; SVG is still server-rendered everywhere at this point (Task 4.4).
8. **Locking a fingerprint captured off one run, or off a stale build.** Two repeated runs, and name the provenance.
9. **A plotly-only special case in `FingerprintTree`.** One vendored-asset predicate covering prism too, or the next vendored asset re-creates the problem.
10. **`git reset --hard` / `git checkout --` / `git clean`.** Another session's uncommitted work is in the tree *right now*. This has already destroyed real work mid-story.

### Seams you must adopt, not re-mint

| Seam | Where | Contract |
|---|---|---|
| `HierarchyExplorer.TextTwinHtml` | `HierarchyExplorer.cs:354` | **the** twin builder; add a display mode, never a second builder |
| `HierarchyExplorer.IslandHtml` | `HierarchyExplorer.cs:308` | payload + config island; the AC#2 assertion target |
| `HierarchyExplorer.BranchValues` | `HierarchyExplorer.cs:103` | assert against this constant, never a literal `"total"` |
| `HierarchyExplorer.StatusLabelFor` | `HierarchyExplorer.cs:409` | prose status; the twin's non-color reading |
| `Projector_NodeSet_EqualsTheWedgesTheSvgDrew` | `SunburstExplorerTests` | the payload↔SVG anti-drift invariant — extend, don't replace |
| `FingerprintTree` / `NormalizeVolatile` | `SiteGeneratorAdapterTests.cs:1011,1033` | the one normalization path; add the vendored-asset fold here |
| `Charts.ImpactMapBody` + `<details open>` | `ImpactMapTemplater.cs:56-62` | the reference twin implementation |
| `StatusStyles` | the six `--status-*` tokens' single source | prose status labels; chart, twin and tile grid cannot disagree |

### Files being modified — current state

- **`src/SpecScribe/HierarchyExplorer.cs` (414 lines, NEW and UNTRACKED at draft time) — UPDATE.** 20.5's in-flight component. `HierarchyExplorerConfig` at `:51` has **no** twin-display field; `TextTwinHtml` at `:354` hard-codes `<details class="ss-hierarchy-twin">` with a `<summary>` and delegates nesting to `AppendTwinLevel` (`:375`, cycle-guarded, depth-capped at 12). `IslandHtml` at `:308` emits the exact `config`/`nodes` shape Task 4.2 asserts on. **Verify every one of these line numbers before relying on it** — this file is being written as you read this.
- **`src/SpecScribe/GitInsightsTemplater.cs` — UPDATE (doc comment only, Task 2.4).** `:14-17` states a superseded ADR 0010 §2 contract. `:10` records that Story 7.11 deleted both prior ownership tables. `AppendOwnershipSection` (`:70-193`) is sunburst + treemap + four legends and **no table**.
- **`tests/SpecScribe.Tests/HierarchyExplorerTests.cs` — UPDATE (20.5's file).** Extend with the AC#2 assertions.
- **`tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — UPDATE.** `FingerprintTree` `:1011`, `NormalizeVolatile` `:1033`, the golden constant `:984`, `GoldenOutputInventory` `:190-224`.
- **`_bmad-output/implementation-artifacts/20-6-text-twin-audit.md` — NEW.** The durable audit record; 20.7's work list and permission slip.
- **`src/SpecScribe/Charts.cs` — READ ONLY.** `SunburstCompanionList` `:648` (note the done-epic omission at `:668`), `EpicSunburst` `:974`, `TaskSunburst` `:1099`, `CodeTreemap` `:2910`, `CodeMapSunburst` `:3467`, `CodeOwnershipSunburst` `:3928`, `CodeOwnershipTreemap` `:3987`. **Change none of them** — they are 20.7's.
- **`src/SpecScribe/CodeMapTemplater.cs` / `ImpactMapTemplater.cs` — READ ONLY.** Audit targets 5 and 7.

*Verify each line reference before relying on it — another session is editing this tree.*

### Project Structure Notes

One new artifact (`20-6-text-twin-audit.md`). One new enum + one record field on an existing type. No new page, no new nav entry, no new dependency, no new chart. The rendering delta is confined to how the dashboard/epics twin presents.

### Testing standards summary

xUnit, `tests/SpecScribe.Tests`. SSR-first: C# emitters and rendered markup are unit-tested; the JS-off audit is a **live-browser** activity with no test-suite equivalent — say so plainly rather than implying coverage that does not exist. Golden fingerprint = `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`; regenerate deliberately, confirm across two runs, record provenance.

### Previous story intelligence

**Story 20.5 (`ready-for-dev`, IN FLIGHT)** — creates everything AC#2 asserts on. Two of its notes land directly here: Task 8.7 hands this story the broader `FingerprintTree` replacement and flags that the 1.2 MB vendored bundle now flows into the hash; **Open Question #4** — *"Does the twin supersede `SunburstCompanionList` on the dashboard?"* — was explicitly deferred to this story and is now answered by owner decision **D4** (no; keep both, twin goes `sr-only`). Its D1 mount decision is also why only the dashboard can be fixed here: the component is not on the other six surfaces yet.

**Story 20.4 (`done`, code-reviewed)** — the durable-report pattern this story's audit record follows (`20-4-spike-report.md`), and the reason to distrust console-based verification: **CSP violations do not appear in browser console captures** — a test that greps the console passes while the chart is blank. Ask the DOM.

**Story 20.3 (`done`)** — the rail is **101,435 B of a 472,222 B dashboard (21.5%)**, and 20.5's island + twin add to it. Its deferred payload-ceiling question was pointed at this story: with the fingerprint replacement making the server-rendered surface explicit, this is the natural place to *report* the real number. The lever is `RelatedWork.MaxEntriesPerGroup = 12` and pulling it is the owner's call, not this story's.

**Story 20.2 (`done`, 22 review patches)** — an SVG `<a>` at `display:none` **stays focusable**. Directly relevant to Task 3.4: `sr-only` and `display:none` behave differently in the accessibility tree, only the live browser shows which you got, and the suite cannot see either.

**Story 21.3 (`done`)** — built the Impact Map's server-rendered `<details open>` fallback under exactly the assumption ADR 0013 now makes universal. It is the exemplar (F4).

### Git intelligence summary

`92fa581` ("20.3,20.4,20.5,5.1, new epics for sonar") bundles four stories, which is structural — code review runs at epic end. **Scope any later review of this story by its own File List and declared symbols, never by a commit range** (CLAUDE.md § Scoping a code review).

### ⚠️ Concurrent work — read this before you start

`git status` at draft time shows **Story 20.5 mid-implementation in this tree**:

```
?? src/SpecScribe/HierarchyExplorer.cs          (414 lines, untracked)
?? src/SpecScribe/assets/plotly-hierarchy.min.js
?? tools/plotly-vendor/
 M src/SpecScribe/{Charts,DashboardView,DashboardViewBuilder,ForgeOptions,HtmlRenderAdapter.Dashboard}.cs
 M src/SpecScribe/SpecScribe.csproj
```

Consequences, per CLAUDE.md § Concurrent work:

- **Do not start until 20.5 is complete.** Every symbol in Task 3 and Task 4.2 is 20.5's, and half of them are unwritten as this story is drafted. Every line reference in this file is from an **in-flight working copy** — grep-verify each before relying on it, and expect names to have shifted.
- **Expect the golden fingerprint to have already moved.** Task 5.3 is not paperwork; it is how the next reader knows whether an unexpected hash is a regression or someone else's landing.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** This has already destroyed real work mid-story in this repo.

### References

- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — §2 twin contract, §3 the per-surface live JS-off gate, §4 supersedes ADR 0010 §2, §6 fingerprint replacement, §Context the partial-coverage admission this story quantifies
- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`] — §2 one component, and its Story 20.4 addendum
- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 20 → Story 20.6] — the two ACs verbatim; the seven-surface rollout inventory table
- [Source: `_bmad-output/implementation-artifacts/20-5-hierarchy-explorer-component.md`] — Task 3.5 the twin spec, Task 8.7 the `FingerprintTree` hand-off, Open Question #4 answered here by D4
- [Source: `_bmad-output/implementation-artifacts/20-4-spike-report.md`] — the durable-report pattern; the console-capture warning
- [Source: `CLAUDE.md`] — § Verification (live browser), § Concurrent work on shared `main`, § Scoping a code review
- Code: `HierarchyExplorer.cs:51,103,308,354,375,409`, `Charts.cs:648,668,974,1099,2910,3467,3928,3987,3941`, `GitInsightsTemplater.cs:10,14-17,70-193`, `CodeMapTemplater.cs:139-164,204-205,277`, `ImpactMapTemplater.cs:56-62,130-131`, `specscribe.js:875`, `SiteGeneratorAdapterTests.cs:190-224,984,1011,1033`

### Open questions (non-blocking — recommended answers stated; raise at the owner's verify round)

1. **Does the `sr-only` dashboard twin need a visible affordance?** Recommended: **no.** `SunburstCompanionList` and the 20.3 rail are both visible and both navigable, so a sighted JS-off visitor is not stranded. Revisit if the audit shows the tile grid's done-epic omission (F1) leaves a visible hole.
2. **Should Task 4.3's vendored-asset fold be a separate change?** Recommended: **no** — it is a prerequisite for a stable fingerprint once a 1.2 MB minified artifact is in the tree, and splitting it means regenerating the constant twice.
3. **Report the payload-ceiling number, or act on it?** Recommended: **report only.** Pulling `RelatedWork.MaxEntriesPerGroup` changes what visitors see and is the owner's call.

---

## Dev Agent Record

### Agent Model Used

claude-opus-5 (dev-story workflow)

### Debug Log References

**Line references in this story were all stale — grep-verified and corrected before use** (the story itself warned
they came from an in-flight working copy). `HierarchyExplorer.cs` is **616 lines, not 414**:
`HierarchyExplorerConfig` :51 → **:65** (confirmed: no twin-display field), `BranchValues` :103 → **:117**,
`IslandHtml` :308 → **:432**, `TextTwinHtml` :354 → **:484**, `AppendTwinLevel` :375 → **:505**, `StatusLabelFor`
:409 → **:611**. The record also gained `LegendHtml` and the node record gained `ShortLabel`/`Detail`, none of
which existed when this story was drafted — so the island's config carries `title` and its nodes carry
`shortLabel`/`detail` beyond the field list Task 4.2 named.

**Three of this story's own task instructions were wrong and were corrected rather than followed:**

1. **Task 4.1's scope limit was factually wrong about Code Map.** It states `code-map.html`, `git-insights.html`
   and `impact-map.html` "never render" in the golden fixture. Verified against `GoldenOutputInventory`:
   `code-map.html` **and** `risk-quadrant.html` DO render there — they ride the fixture's repo-root markdown walk,
   not deep-git. Only `git-insights.html`, `impact-map.html` and `timeline.html`/`commits/` are absent. The
   accurate limit is stated in the test comment.
2. **Task 4.3 was already done.** The vendored-asset fold landed with Story 20.5 and was hardened by Story 25.1's
   review into the shared `KnownStaticAssets` map — which already covers plotly **and** both prism assets, i.e.
   already the "one predicate, not a plotly special case" Task 4.3 asked for. Not re-implemented; recorded.
3. **Task 3.2's "dashboard and epics-index instances" is a single instance.** Verified: `epics.html` contains 0
   `ss-hierarchy-data` islands, 0 `data-hierarchy` hosts, 0 twins. Task 3.5's own escape condition applies, so no
   second instance was mounted — that is 20.7's rollout.

**Two false FAILs were caught mid-audit and retracted** (both would have sent 20.7 to fix something unbroken):
SVG `<title>` concatenates a parent group name and uses `—` inconsistently, which falsely reported the dashboard's
`Unplanned` node missing; and chart `<title>` carries raw markdown backticks while the page renders `<code>`,
which falsely reported 67 of 83 story-detail facts missing (true number after normalizing both sides: **1**).
Story detail is therefore a **PASS**, not the hypothesized "PROBABLE FAIL".

**Environmental blocker, twice.** `GitMetrics.Timeout` is a hard-coded 3 000 ms per git subprocess with no knob
(`GitMetrics.cs:197`). Measured here: `git log --numstat` takes **6 496 ms cold / ~2 450 ms warm** — so deep-git
fails outright cold and clears the budget by only ~18% warm. On failure `TryComputeDeep` returns null and
`git-insights.html`, `impact-map.html` and the ~300 per-commit pages are **silently not written** while the run
still reports `errors=0` and `diagnostics.html` still says deep-git is on. Two generations produced 413 pages
instead of 715 before the cause was found. Flagged in the audit record as worth its own story; not fixed here.

**Concurrent-session interference.** `SpecScribeOutput/` was regenerated by another session **without**
`--deep-git` mid-audit, deleting `git-insights.html` and `impact-map.html` after they had been verified present.
Per CLAUDE.md the shared tree was not contested; the audit moved to an isolated output directory instead.
`--output docs/live` was never used.

### Completion Notes List

**AC#1 — the audit is complete, and its verdict is the deliverable.** All seven surfaces were audited in a live
browser with JavaScript disabled at the browser level (CSP `script-src 'none'`), proven off rather than assumed:
`HierarchyExplorer.BootScript`'s synchronous head marker was never set, `window.Plotly` was `undefined`, the chart
host had zero children, and the retained server SVG was the page. The durable record is
**[`20-6-text-twin-audit.md`](20-6-text-twin-audit.md)** — Story 20.7's permission slip and work list.

**⛔ WHICH SURFACES 20.7 MAY RETIRE — the entry condition for that story:**

| ✅ Cleared for retirement | ⛔ Keeps its server-rendered SVG |
|---|---|
| **Dashboard** `index.html` — 212/212 set match, strict superset of the 138 real SVG nodes, 0 unresolved links | **Epics index** `epics.html` — the 87-node story ring and 39-node follow-up ring are stated nowhere |
| **Story detail** `epics/story-N-M.html` — 38/38 on an all-open story; the story doc's own task list is a complete natural twin | **Epic detail** `epics/epic-N.html` — only 4 of 9 stories state their task count; the deferred-item grouping is unstated |
| **Code Map** `code-map.html` — exact set match on all 4 variants × both shapes, 0 missing | **Git Insights** `git-insights.html` — **no twin at all**; only 6 of 1 115 files linked outside an `<svg>`; zero tables |
| **Impact Map** `impact-map.html` — already client-only; 993/993, the reference implementation | |

**Two hypotheses in the story's own pre-read were wrong and are corrected in the record:** Story detail was
predicted "PROBABLE FAIL" and **passes**; Git Insights is **worse** than predicted (the page has zero `<table>`
elements and its treemap emits no `<title>` at all).

**New cross-cutting finding:** `role="img"` sits on the chart `<svg>` on epic detail, story detail, Code Map (8
charts) and Git Insights (2 charts), and **contains all their in-chart links** — ARIA prunes descendants, so those
links reach no screen reader. 20.7 cannot discharge navigability by adding links inside a `role="img"` chart; this
is a structural argument for the component's separate text twin.

**AC#1 fix (owner D2 — audit seven, fix one).** `HierarchyTwinDisplay { Details, ScreenReaderOnly }` added to
`HierarchyExplorerConfig` as a trailing defaulted field; `TextTwinHtml` branches on it; the dashboard call site
sets `ScreenReaderOnly` (D4). `SunburstCompanionList` and the 20.3 rail were not touched — verified still visible
(940 px and 8 589 px tall respectively). The epics index was **not** fixed: it has no component instance at all
(0 islands, 0 hosts), so Task 3.5's escape condition applies and that conversion is 20.7's.

**An a11y hazard this created, resolved, and verified live — worth the owner's attention at the verify round.**
`.sr-only` is the clip-rect technique, so the twin's **212 links stay focusable while invisible** — an invisible
tab tunnel for a *sighted* keyboard user. Dropping them from the tab order was rejected because it would break the
navigation half of NFR-5, which ADR 0013 says may never be lost. Instead
`.ss-hierarchy-twin.sr-only:focus-within` un-clips the container, the pattern skip links use. Measured in the
browser: 212 links, **0 untabbable**; the container goes **1×1 clipped → 669×5680 revealed** on focus and restores
on blur. This is the same defect class Story 20.2's review caught live and the suite could not see.

**AC#2 — the replacement assertions landed before any retirement.** Four new tests in `HierarchyExplorerTests.cs`
(20.5's file, extended not forked) over the three things ADR 0013 §6 names: the embedded payload (every field,
`value` a number never null, `statusLabel` prose ≠ `statusClass`), the component configuration (asserted against
the `HierarchyExplorer.BranchValues` **constant**, plus a real check that every parent's value equals Σ its
children so the payload genuinely *is* parent-inclusive), and the text twin (every node, prose status, resolving
link). Three further tests pin the new knob — including
`TwinDisplay_ChangesPresentationOnly_TheListingIsByteIdenticalInBothModes`, which guarantees a surface can never
get a *less complete* twin by choosing a different presentation. The existing fingerprint test was **not** deleted
or weakened (Task 4.4): SVG is still server-rendered everywhere, so the replacement runs alongside it.

**Golden fingerprint regenerated: `dde7d077…` → `7adbdb016cf9bb7d6be3193ee27cad8f7888066d2d0f407eee98ea95f74b0c42`.**
Confirmed **stable across two repeated runs** before locking in. Provenance recorded in the stacked comment as
AC#2 requires: it sits on top of another session's uncommitted **Story 18.x / ADR 0015 module-identity work**
(`DiagnosticsTemplater`, `HowToReadTemplater`, `SiteGenerator`, `ModuleContext`, `BmadArtifactAdapter` — and both
`diagnostics.html` and `how-to-read.html` are in `GoldenOutputInventory`, so that work is inside this hash), plus
Story 23.3's `web/` tree, which is outside the fixture and cannot reach it. Note the prior value was `dde7d077…`,
not the `9288bf55…` this story cited at draft time — it had already moved before the story started.

**One real regression found and fixed.** `SiteGeneratorSpaTests.SunburstExplorerIsland_SurvivesSpaContentRegionCapture`
asserted the literal `<details class="ss-hierarchy-twin"`, which the dashboard no longer emits. It failed **in
isolation**, so it was a genuine break, not a flake. Re-keyed on the twin's presentation-independent identity
(`id="dashboard-hierarchy-twin"`) plus its listing (`<ul class="ss-hierarchy-twin-list">`), which is what the test
was actually about. Swept for other sites keyed on the old wrapper: none in `specscribe.js` (it never touches the
twin), and `StylesheetTests`/`HierarchyExplorerTests` remain valid.

**Test results — real numbers, honestly separated.** Targeted verification: **120/120 pass**
(`HierarchyExplorerTests` 34, plus `SiteGeneratorSpaTests`, `StylesheetTests`, `GoldenContentFingerprint`).
`SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew` still holds (Task 3.3) — note it pins the
**projector** vs the SVG, not 20.5's `Reparent` layer, which the browser set-diff covered instead.
The full suite shows **git-fixture flakes, none of them mine**, and the failure count is a clean monotonic
function of load — which is the proof, not an assertion:

| Run condition | Failures |
|---|---|
| Full suite, with a `--deep-git` generation running concurrently | **19** |
| Full suite, quieter machine | **5** |
| Full suite, quiet machine | **18** |
| Full suite, `xUnit.MaxParallelThreads=3` | **2** |
| The affected families run in isolation | **0** (`SiteGeneratorTimelineTests` 11/11, `SiteGeneratorGitInsightsTests` + `GitMetricsFirstCommitDateTests` 8/8) |

Membership was **disjoint** across the three full runs and every single failure is a test that shells out to
`git`. Root cause: `GitMetrics.Timeout` is a hard-coded 3 000 ms while `git log --numstat` on this repo measures
6 496 ms cold / ~2 450 ms warm, so parallel load starves it — the tell being
`GitMetricsFirstCommitDateTests.TryGetFirstCommitDate_ReturnsNull_ForNonexistentPath`, a trivial null check, taking
**34 s** before failing. My `GitInsightsTemplater.cs` change is **comment-only** (`git diff` shows zero
non-comment changed lines), and nothing I touched goes near git, watchers, or commit pages. See the audit record's
environmental section — this matters for 20.7, which will run this suite a lot.

**Caveat stated plainly:** I have not seen this suite go fully green in one run, at any parallelism, on this
machine. The evidence that the residue is pre-existing is the load gradient plus green-in-isolation, not a green
full run.

**Nothing was retired.** This story is the gate, not the retirement: every surface still ships its server-rendered
chart, and no `Charts.cs` symbol was modified.

### File List

- `src/SpecScribe/HierarchyExplorer.cs` — **UPDATE.** New `HierarchyTwinDisplay` enum; `TwinDisplay` field added to
  `HierarchyExplorerConfig` (trailing, defaulted, source-compatible); `TextTwinHtml` branches on it.
- `src/SpecScribe/DashboardViewBuilder.cs` — **UPDATE.** Dashboard instance set to `ScreenReaderOnly` (owner D4).
- `src/SpecScribe/GitInsightsTemplater.cs` — **UPDATE (doc comments only, Task 2.4).** Superseded ADR 0010 §2
  contract replaced with ADR 0013 §4 + the measured audit result, at two sites.
- `src/SpecScribe/assets/specscribe.css` — **UPDATE.** `.ss-hierarchy-twin.sr-only:focus-within` reveal and
  `.ss-hierarchy-twin-title`.
- `tests/SpecScribe.Tests/HierarchyExplorerTests.cs` — **UPDATE.** 7 new tests (3 AC#2 replacement + 3 twin-display
  knob + the byte-identical-listing guarantee) and the honest scope-limit comment.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — **UPDATE.** Golden constant regenerated with the stacked
  provenance comment.
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — **UPDATE.** Regression fix: twin assertion re-keyed off the
  `<details>` literal onto the twin's stable id + listing.
- `_bmad-output/implementation-artifacts/20-6-text-twin-audit.md` — **NEW.** The durable audit record.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — **UPDATE.** Status tracking.
- `.claude/launch.json` — **UPDATE.** Added `twin-audit-20-6` and `twin-audit-20-6-jsoff` (the JS-off audit
  harness) preview configs.

**Not modified, deliberately:** `Charts.cs` (read-only for this story — `SunburstCompanionList`, `EpicSunburst`,
`TaskSunburst`, `CodeTreemap`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeOwnershipTreemap` are all 20.7's),
`CodeMapTemplater.cs`, `ImpactMapTemplater.cs`, `RelatedWorkCards.cs`, `GitMetrics.cs`.

---

## Change Log

- 2026-07-27 — **code review: 6 patches applied, status → done.** 3-layer review (Blind Hunter, Edge Case Hunter,
  Acceptance Auditor) scoped to this story's own File List, diffed `baseline_commit` (`86b35c2`) → the landing
  commit (`32fd282`, "Overnight work" — bundles sibling stories 18.1/18.2/18.3/23.3/23.5, all excluded). Acceptance
  Auditor found **no AC violations** — both ACs are met and internally consistent with the durable audit record and
  ADR 0013/0012. 6 patches applied: an undefined CSS custom property (`--surface`) that always fell back to a stark
  white reveal panel (now `--warm-white`, matching every other `.chart-panel`); the sr-only twin's own `<h3>`
  duplicating the chart panel's existing same-level `<h3>` (demoted to `<h4>`); a misleading doc-comment analogy
  claiming `TwinDisplay` reaches the client like `Size` does (it doesn't — reworded); a fragile line-number citation
  (`Charts.cs:668` → cited by symbol); a stale test comment describing `<details>` as "the real shape a converted
  surface ships" post-diff (the dashboard now ships `<section sr-only>` — reworded); and a non-exhaustive `==`
  branch on `HierarchyTwinDisplay` (now an exhaustive switch with a throwing default arm). 1 finding deferred to
  Story 20.7 (no automated tripwire test guarding that `git-insights.html` keeps its SVG); 8 dismissed as noise or
  already addressed elsewhere (including one, `EpicsViewBuilder.cs` not getting the D4 treatment, confirmed via
  `git blame` to be Story 20.7's own later work, not this diff's). The `<h3>`→`<h4>` and CSS-token fixes moved the
  golden fingerprint (it had already moved twice more since this story's own dev-story pass, through Stories 20.7
  and 20.8's landed regenerations to `3171cf5c…`); regenerated to
  `06788c0fa85928924226617c767a808c85ae5a55fd9a8d9b3fb1757769a95e61`, confirmed stable across two repeated runs, with
  a stacked provenance comment. Targeted suite re-run green (73/73). Verified live in the browser: the sr-only
  reveal now computes to the themed `rgb(250, 247, 242)` background (not white), and the chart panel nests exactly
  one `<h3>` + one `<h4>`. One transient build break from a concurrent session's in-flight, unrelated work
  (`TestArtifactDiscovery.cs`/`SiteGenerator.cs`) was encountered between the two stability-confirmation runs and
  resolved on its own without touching those files, per CLAUDE.md § Concurrent work.
- 2026-07-26 — **dev-story: implemented, status → review.** All seven surfaces audited live with JavaScript
  disabled (CSP `script-src 'none'`, with the boot-marker/`window.Plotly` probes proving execution was blocked);
  durable record written to `20-6-text-twin-audit.md`. **Four surfaces cleared for 20.7 (dashboard, story detail,
  Code Map, Impact Map); three keep their SVG (epics index, epic detail, Git Insights)** — a recorded FAIL plus a
  retained SVG being the designed D2 outcome. Two of the story's own hypotheses corrected: story detail PASSES
  (38/38 on an all-open story; the earlier "67 of 83 missing" reading was a markdown-backtick normalization
  artifact) and Git Insights is worse than predicted (zero `<table>` elements; treemap emits no `<title>`; only 6
  of 1 115 files linked outside an `<svg>`). New cross-cutting finding: `role="img"` on the chart `<svg>` prunes
  every in-chart link from the accessibility tree on four surfaces. Owner D4 implemented as a config-driven
  `HierarchyTwinDisplay` knob rather than a second twin builder, with a guarantee test that the listing is
  byte-identical across presentations; the `sr-only` variant's 212-focusable-link hazard resolved with a
  `:focus-within` reveal and verified live (1×1 → 669×5680 on focus, 0 untabbable links). AC#2 replacement
  assertions landed over payload/config/twin with the existing fingerprint test kept intact; golden constant
  regenerated to `7adbdb01…`, stable across two runs, provenance naming another session's Story 18.x/ADR 0015
  work. One real regression found and fixed (`SunburstExplorerIsland_SurvivesSpaContentRegionCapture` was keyed on
  the `<details>` literal). Task 4.3 found already complete from 20.5 + 25.1; Task 4.1's scope limit corrected
  (`code-map.html` DOES render in the golden fixture). Two environmental findings recorded for the owner: the
  hard-coded 3 000 ms `GitMetrics.Timeout` silently drops three surfaces and ~300 pages while reporting
  `errors=0`, and it is the root cause of the git-fixture suite flakiness (three runs: 19/5/18 failures, disjoint,
  all git-shelling, green in isolation). Nothing was retired — this story is the gate.
- 2026-07-25 — Story 20.6 drafted (create-story). Context assembled from ADR 0013 and ADR 0012, the Epic 20 rollout inventory, Story 20.5's in-flight implementation (read from the working tree, not from its plan), and a **code-level pre-audit of all seven surfaces** so the dev starts from stated hypotheses rather than a blank page. Four owner decisions elicited and locked: **D1** hybrid twin shape — 20.5's component twin is the standard and is adopted as 20.7 converts, but Code Map's per-variant file table and Impact Map's `<details open>` grouped list are **audited and kept** because both are richer than the generic nested list and replacing them would re-litigate two shipped reviewed designs; **D2** audit all seven, fix only dashboard/epics — the other five are recorded as failing and **keep their SVG**, which is AC#1's own escape valve and keeps the per-surface gate honest; **D3** twin presents as a closed `<details>` by default, matching what 20.5 already emits and ADR 0013 §2; **D4** `SunburstCompanionList` stays visible and unchanged on the dashboard while the component twin goes `sr-only` there — which surfaced a concrete gap, since `HierarchyExplorerConfig` has no twin-presentation field and `TextTwinHtml` hard-codes the `<details>`, so Task 3.1 adds it. Four findings carry the story: **F1** `SunburstCompanionList` is epic-level only and deliberately omits done epics with no open follow-ups (`Charts.cs:668`), so it can never be the dashboard's twin — which is what makes D4 the right call rather than a compromise; **F2** Git Insights has **no** twin at all (Story 7.11 deleted both prior ownership tables) and its own doc comment still states the superseded ADR 0010 §2 contract, so its entire no-JS story rests on the SVG 20.7 wants to delete — the hardest gap, plus a stale-contract correction; **F3** Code Map's table is genuinely server-complete (`specscribe.js:875` — every file ships as a plain `<tr>`, JS only paginates), so its pager must not be mistaken for truncation; **F4** Impact Map is the reference implementation and should be cited as such. Also closed a discrepancy: ADR 0013's Context names "the ownership and **freshness** views" as separate surfaces, but freshness is verified to be a *colorize dimension* of the Code Map, not an eighth chart. Two honesty constraints recorded so the dev cannot over-claim: the golden fixture is not a git repo and cites no real files, so `code-map.html`/`git-insights.html`/`impact-map.html` **never render in it** and the fingerprint replacement cannot imply portfolio-wide chart coverage; and the JS-off audit has no test-suite equivalent at all. Sequencing recorded as blocking: **Story 20.5 is mid-implementation in this working tree** (`HierarchyExplorer.cs` untracked at 414 lines, the vendored bundle and `tools/plotly-vendor/` untracked, six source files modified), so every symbol and line reference in this story is from an in-flight working copy and must be grep-verified before use. baseline_commit `92fa581`.
