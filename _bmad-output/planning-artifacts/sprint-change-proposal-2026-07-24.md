# Sprint Change Proposal — 2026-07-24

**Workflow:** `bmad-correct-course` · **Mode:** Incremental · **Requested by:** Matthew-Hope Eland
**Scope classification:** **Major** (reverses ratified ADRs, amends a PRD non-functional requirement, introduces the project's first third-party runtime dependency)
**Status:** Approved and applied — see § 6.

---

## 1. Issue Summary

The owner raised a concern about the Explorer epic (Epic 20) and directed four changes:

1. **Use Plotly.js for the sunburst control.**
2. **Build one standardized component** bundling a sunburst and a treemap over the same datasource, with a standardized selector letting the user choose which to view.
3. **Use that combination everywhere** — it replaces all treemaps and sunbursts in the solution.
4. **Make the click behavior mode-driven**: on some screens clicking a node navigates; on others it selects, populating other context on the page. The home screen uses the contextual mode to populate a details pane to the right of the control containing high-level details, the recommended-prompt button, and a link to view more.

Stated intent: *"a more powerful, dynamic, and standardized user experience with these controls… easier to make site-wide changes in the future and add additional features."*

### Evidence — verified in code, 2026-07-24

The request is well-founded, and the evidence is stronger than the request implies. This is not drift being *prevented*; it is drift that **already shipped three times**.

| Finding | Evidence |
|---|---|
| Three independently-built `Treemap \| Sunburst` toggles | `CodeMapTemplater.cs:182`, `GitInsightsTemplater.cs:160`, `ImpactMapTemplater.cs:126` |
| …which already **disagree with each other** | Code Map and Impact Map order *Treemap, Sunburst*; Git Insights orders *Sunburst, Treemap*. Different ID schemes, different mount logic. |
| Three independent client-side arc renderers in one file | `assets/specscribe.js` (1,961 lines): `initOwnershipSunburst` (7.11), `renderSunburst`/`arcPath` (21.3), `initSunburstExplorer` (20.2) |
| Seven server-side hierarchy entry points | `Charts.cs` (4,777 lines): `Sunburst`, `EpicSunburst`, `TaskSunburst`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeTreemap`, `CodeOwnershipTreemap` |

**Root cause:** ADR 0010 §6 *already required* "ONE shared engine/module… not independently reinvented per story." **That rule did not hold.** The Epics 19+21 joint retrospective verified the violation and seated Story 20.4 to fix it by *extracting shared arc math*. The deeper lesson this proposal acts on: **a shared *convention* is easy to defeat — three concurrent sessions defeated this one. A shared *component* is much harder to accidentally reinvent.**

**Supporting context:** the owner first named Plotly on 2026-07-22 (recorded verbatim in the Epic 20 epic body — *"You can do this via Plotly and it's amazing"*), flagged there as needing "its own dependency-budget decision at spike time, not an assumed yes." This proposal is that decision.

---

## 2. Impact Analysis

### 2.1 Epic Impact

| Epic | Impact |
|---|---|
| **20** | **Rewritten in place.** 20.1 annotated (superseded, not redone); 20.2/20.3 unchanged and land as-is; **20.4 replaced**; **20.5–20.8 added**. |
| **7** | Code Map, ownership, and freshness surfaces become consumers; their bespoke JS is deleted. **No AC changes.** |
| **21** | Impact Map (21.3) becomes a consumer. **No AC changes.** |
| **22** | Contradiction resolved: ADR 0008 and the epic body asserted "charts stay pre-rendered SVG *in* the IR." Under ADR 0013 there is no such SVG — the IR carries chart **data + component config**. **A simplification:** the IR becomes a data document rather than data-plus-markup. |
| **23** | Same contradiction, plus **Story 23.4 AC#2 rewritten**. **A scope reduction:** no C# chart-SVG generator remains to preserve when the `HtmlRenderAdapter` is retired. The ADR 0005 CSP amendment 23.4 already owed is now **shared with ADR 0012's and must be landed once, not twice.** |
| **24** | 24.1 unaffected (non-visual). **24.2's gate rewritten** (blocked on 20.7, not 20.4). **Engine is now an explicit open question** — Plotly has no force-directed layout and no chord trace, so it cannot serve 24.2/24.3/24.4. 24.5 may ride Plotly's `heatmap` trace. |
| **6** | ADR 0005's CSP (`script-src 'nonce-…'`, "the body carries no scripts of its own") blocks Plotly. Amended. If unreachable, the webview renders the text twin. |
| **16 / 17** | Plotly must be vendored and packaged across binary + npx + VSIX. **First third-party runtime dependency → NFR10 supply-chain audit scope.** |

### 2.2 Artifact Conflicts Resolved

- **PRD § 8 NFR-5** — amended (full prior wording and rationale preserved inline).
- **ADR 0010** — §1 and §6 superseded by ADR 0012; §2 superseded by ADR 0013.
- **ADR 0005** — CSP clause amended by ADR 0012.
- **ADR 0008 / ADR 0009** — SVG-in-IR non-goal amended by ADR 0013.
- **epics.md** — six separate SVG-in-IR assertions corrected.

### 2.3 Technical Impact

- **Deletions:** 7 `Charts.cs` hierarchy entry points; 3 `specscribe.js` arc renderers; 3 divergent toggles.
- **Additions:** vendored Plotly custom build (`sunburst` + `treemap` + `heatmap` only); one Hierarchy Explorer component.
- **Golden fingerprint:** stops covering charts — measured at **69.3% of the dashboard body** by the Story 23.1 spike. Replacement assertions must land in Story 20.6, *before* the first SVG retirement. Upside: the chronically-unstable fingerprint should become materially calmer once chart geometry leaves the hashed surface.
- **Output size:** replacing megabytes of inline SVG with JSON + one shared asset is likely a substantial reduction — directly relevant to the 82.5 MB `code-map.html` problem. **Expected, but must be measured (Story 20.4), not assumed.**

### 2.4 Risks Accepted

| Risk | Disposition |
|---|---|
| **Plotly accessibility is its weakest dimension**, and ADR 0013 removes the SVG fallback | Story 20.4 reports UX-DR7/16/17/18 as **explicit pass/fail**. This is the one finding that reopens ADR 0012. |
| **A JS-off visitor loses the charts, including on home** | Accepted, owner-directed. Recorded as a genuine product concession, not framed as a reinterpretation. |
| **The text twin becomes a single point of failure** for the entire no-JS story | Mitigated by Story 20.6's blocking per-surface audit, verified **live with JS disabled**. |
| Twin coverage is currently **partial** (Code Map, ownership, freshness, `EpicSunburst`, `TaskSunburst`, Impact Map unaudited) | Story 20.6 gates rollout per surface. An incomplete twin keeps its SVG. |
| **Two engine families is a weaker invariant than one** | Accepted deliberately — the alternative was letting Plotly's trace list decide whether Epic 24 ships force-directed and chord views. |
| First third-party runtime dependency | Vendored locally, never CDN, `file://`-safe; NFR10 audit scope; packaged across three channels. |

---

## 3. Recommended Approach

**Hybrid — Direct Adjustment plus coordinated ADR/PRD amendments.**

Direct Adjustment alone was **not viable**: the change reverses ratified architectural decisions, which by this project's own convention (CLAUDE.md § Decision records) must be recorded as ADRs rather than buried as story notes. Rollback was **not viable** — there is nothing to usefully revert; Story 20.2's shipped work is retained and its payload contract is reused. MVP reduction was **not warranted** — this improves the product rather than trimming it.

**Effort:** High · **Risk:** Medium-High, concentrated in the webview CSP and Plotly a11y unknowns · **Timeline:** four new stories plus one re-tasked spike.

### Owner decisions taken during this workflow

| Decision | Chosen | Alternatives declined |
|---|---|---|
| No-JS floor | **Text-twin only — retire the SVG** | Keep SVG + layer Plotly; relax NFR-5 for charts |
| Epic 24 engine | **Plotly for hierarchy, graphs decided later** | Plotly-everywhere-and-cut; re-open library choice now |
| Epic structure | **Rewrite Epic 20 in place** | New Epic 25; split platform/rollout epics |
| 20.2 / 20.3 | **Land both as-is, replace later** | Freeze; revert 20.2's zoom |
| ADR shape | **Two ADRs** (engine, no-JS contract) | One combined ADR |
| Ratification | **Accepted now**, spike validates via addendum | Proposed / spike-gated |
| Story split | **8-story Epic 20** | Per-surface rollout; fold audit into rollout; pane-first |

---

## 4. Detailed Change Proposals — all applied

### Decision records (new)
- **[ADR 0012 — Plotly.js as the Hierarchy-Chart Engine, and One Standardized Hierarchy Explorer Component](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)** — Accepted 2026-07-24.
- **[ADR 0013 — The Text Twin Is the No-JS Contract](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)** — Accepted 2026-07-24.
- `docs/adrs/README.md` — both indexed; amendment annotations added to 0005, 0008, 0009, 0010.

### PRD
- **§ 8 NFR-5 amended.** Before: *"…disabling JavaScript must not remove information or break navigation."* After: information and navigation are non-negotiable; **visualization may require JavaScript** provided a server-rendered text equivalent carries the information. Prior wording, rationale, and downstream effects preserved inline.

### epics.md
- Epic 20 retitled, body rewritten, **rollout inventory table added** (7 call sites with file:line).
- Epic List entry and **FR38** rewritten.
- Story 20.1 annotated; **Story 20.4 replaced**; **Stories 20.5–20.8 added**.
- Six SVG-in-IR assertions corrected across Epics 22/23; **Story 23.4 AC#2 rewritten**.
- Epic 24 foundations revised; **Story 24.2 AC#2 amended**.
- **NFR numbering collision recorded** (see § 5).

### sprint-status.yaml
- `epic-20` annotated; `20-4` key **renamed** `20-4-shared-client-side-geometry-engine` → `20-4-plotly-engine-adoption-spike`; `20-5`–`20-8` seated `backlog`; `24-2` and `24-5` notes revised; `last_updated` refreshed with prior value preserved.

---

## 5. Open Item Raised — Deliberately Not Bundled

**epics.md and the PRD number their NFR lists independently, and they disagree.**

- **PRD NFR-5** = progressive enhancement (the requirement ADR 0013 amends)
- **epics.md NFR5** = shared-access file reads / watch-mode write locks — *a different requirement*
- **epics.md NFR6** = cross-surface accessibility semantics

Stories and ADRs across Epics 20/22/23/24 routinely cite **"NFR6 JS-optional baseline"** or **"NFR6 no-JS baselines"** when they mean the **PRD's NFR-5**. Those citations point at the wrong entry, and epics.md has **no progressive-enhancement NFR at all**.

The collision **predates this change** and was found while amending NFR-5. It is recorded in epics.md but **not resolved here** — fixing it (renumber, add the missing entry, or adopt one canonical list) would touch many stories' citations and deserves its own pass.

---

## 6. Implementation Handoff

**Scope classification: Major.** Architectural reversal with PRD impact — but the strategic decisions are made and recorded, so execution routes normally.

**Sequencing (load-bearing):**

```
20.4 (Plotly spike — validates, does not gate)
  └─> 20.5 (component)
        ├─> 20.6 (text-twin audit + fingerprint replacement)  ◄── BLOCKING GATE
        │     └─> 20.7 (site-wide rollout + deletions)  ◄── must land before 24.2
        └─> 20.8 (home details pane)
```

**Two hard constraints:**
1. **Story 20.6 gates Story 20.7 per surface.** No surface's SVG retires until its twin is audited complete in a live browser with JavaScript disabled.
2. **Story 20.7 must land before Story 24.2 begins**, or Epic 24 adds renderers to a file whose existing renderers are about to be deleted.

**Next action:** `create-story` for **Story 20.4**. Per CLAUDE.md, elicit named design directions for any visual surface at create-story time — that matters most for **20.5** (selector idiom, shape-switch behavior, framing block) and **20.8** (details-pane layout, empty state).

**Verification standard for every story in this epic:** live-browser verification is mandatory, not optional. The test suite structurally cannot see CSS containment leaks, JS-off degradation, or Plotly's rendered accessibility tree — and under ADR 0013 the JS-off path has no SVG fallback to hide a defect.

**Follow-ups recorded, not scheduled:** the NFR numbering collision (§ 5); the Epic 24 graph-engine choice (ADR 0012 §4, Epic 24's own spike); the shared ADR 0005 CSP amendment (land once, with Story 23.4).
