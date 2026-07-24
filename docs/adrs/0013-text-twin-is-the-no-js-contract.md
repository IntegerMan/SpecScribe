# ADR 0013: The Text Twin Is the No-JS Contract — Retiring Server-Rendered Chart SVG

**Status:** Accepted (owner-ratified 2026-07-24, via correct-course, jointly with ADR 0012)
**Date:** 2026-07-24
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0012 — Plotly.js as the Hierarchy-Chart Engine](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (the change that forces this one); [ADR 0010](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) (**supersedes §2**); [ADR 0008](0008-json-ir-canonical-and-incremental-generation.md) and [ADR 0009](0009-frontend-framework-for-projection-layer.md) (**amends** the "charts stay pre-rendered SVG in the IR" non-goal); PRD **NFR-5** (amendment required); UX-DR17, UX-DR19, UX-DR21; Epic 22, Epic 23 (Story 23.4)

## Context

PRD **NFR-5** states: *"The generated portal's core content and navigation must function without JavaScript. Insight surfaces may use JavaScript as a progressive enhancement only… disabling JavaScript must not remove information or break navigation."*

ADR 0010 §2 read that as a per-surface requirement that **a real, useful default-mode chart must render with JS off**, plus the accessible text equivalent. That reading was affordable because SpecScribe's charts were server-rendered SVG, and the JS layer only recolored or re-laid-out what was already on the page.

[ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) breaks that assumption: **Plotly renders client-side only.** It cannot emit server-side SVG. That leaves exactly three ways forward:

1. **Keep the C# SVG renderers as the no-JS baseline and layer Plotly on top.** NFR-5 survives untouched — but SpecScribe then maintains *two complete chart implementations forever*, in two languages, that must never disagree. That is precisely the drift class ADR 0012 exists to end, re-created at a larger scale.
2. **Retire the server-rendered SVG; the accessible text twin becomes the sole no-JS representation.** Information and navigation survive without JS; the *picture* does not.
3. **Declare charts JS-required outright** and drop the no-JS obligation for them.

The owner chose **option 2** on 2026-07-23.

The choice is more defensible than it first sounds, because **every chart in this codebase already ships a text equivalent** — it has been house convention since Epic 1, and UX-DR21 already names chart text-twin tables as "accessibility contract… never removed." What changes is not whether they exist, but that they stop being a companion and become **the contract itself**.

What that promotion exposes is that coverage is currently **partial and uneven**. `Charts.SunburstCompanionList` covers the dashboard and epics sunbursts; the traceability matrix is self-describing via `caption` + `th scope`; the work graph has a full sr-only node/edge enumeration. But the Code Map treemap and sunburst, the ownership and freshness views, `EpicSunburst`, `TaskSunburst`, and the Impact Map have not been audited against a standard that says *this is the only thing a JS-off visitor gets*.

## Decision

**1. NFR-5 is amended.** New wording (to be applied to the PRD):

> **NFR-5 (Progressive enhancement):** The generated portal's core content and navigation must function without JavaScript. Disabling JavaScript must never remove **information** or break **navigation**. Data **visualizations** may require JavaScript, provided the information they present remains fully available without it through a server-rendered text equivalent. The established accessibility conventions (text and non-color cues, reduced-motion support) still apply.

The load-bearing distinction: **information and navigation are non-negotiable; visualization is not.**

**2. The text twin is promoted from convention to contract.** Every chart's text twin must be:

- **Server-rendered** — present in the HTML source, never injected by script.
- **Complete** — no fact may exist only inside the chart. If the chart shows it, the twin states it.
- **Navigable** — every link a chart node would offer is present and resolves.
- **Non-color** — every metric readable as text, per UX-DR17 and UX-DR19.
- **Not visually redundant** — twins may remain visually collapsed or `sr-only` where a chart is present; the requirement is availability, not duplication on screen.

**3. Hard gate — no surface retires its SVG before its twin is audited complete.** This is per-surface, not per-epic, and it is verified **in a live browser with JavaScript disabled** — not by test assertion alone. CLAUDE.md's verification rule applies with full force here: the test suite structurally cannot see what a JS-off visitor actually gets. A surface whose twin is incomplete keeps its server-rendered SVG until the twin is fixed.

**4. ADR 0010 §2 is superseded** by Decisions 1–3. The "a real, useful default-mode chart must render with JS off" requirement no longer holds; the text twin discharges NFR-5 instead.

**5. ADR 0008 and ADR 0009 are amended: the IR carries chart *data and component configuration*, not pre-rendered SVG.** Both ADRs, the Epic 22 and Epic 23 epic bodies, and **Story 23.4 AC#2** currently state that "charts stay pre-rendered SVG *in* the IR." Under this ADR there is no pre-rendered SVG to carry. This is a **simplification** of both epics — the IR becomes a data document rather than a data-plus-markup document, and Story 23.4's scope shrinks because there is no C# chart-SVG generation left to preserve when the `HtmlRenderAdapter` is retired.

**6. The golden fingerprint stops covering charts, and needs a replacement.** `GoldenContentFingerprint` is this project's primary chart regression net, and the Story 23.1 spike measured chart SVG at **69.3% of the dashboard body**. Once charts are client-rendered, that coverage evaporates. The story that retires the first SVG **must** land the replacement in the same change: assert on the **embedded payload, the component configuration, and the text twin** — the things that are now server-rendered — rather than on SVG path geometry.

A real upside rides along: the fingerprint has been chronically unstable precisely *because* chart SVG dominates it and shifts under concurrent sessions (memory `golden-diff-normalization-gotchas`). Removing chart geometry from the hashed surface should make it materially calmer.

**7. Webview fallback.** If the ADR 0012 CSP amendment cannot deliver acceptable webview chart rendering, webview surfaces present the text twin. That is a documented, accepted degradation.

## Consequences

**Positive**
- One chart implementation instead of two. The dual-renderer drift hazard is never created.
- `Charts.cs` (4,777 lines) sheds its seven hierarchy entry points and their SVG geometry; `specscribe.js` (1,961 lines) sheds three arc renderers.
- Forces a genuine, audited accessibility floor across every chart surface — work that has been convention-driven and uneven becomes contract-driven and verified.
- The golden fingerprint gets materially more stable.
- The IR (Epic 22) becomes a cleaner data document, and Story 23.4 shrinks.

**Negative / trade-offs**
- **A JS-off visitor loses SpecScribe's signature visuals**, including on the home page. This is a real product concession, and it is the reason NFR-5 needs amending rather than reinterpreting.
- The text twin becomes a **single point of failure** for the entire no-JS story. If a twin is wrong or incomplete, information is genuinely lost rather than merely presented differently.
- The audit in Decision 3 is real, unglamorous work across roughly seven surfaces, and it gates the rollout.
- Losing chart coverage from the golden fingerprint means a period where chart regressions are less well netted than they were, until the replacement assertions mature.
- Amends two already-ratified ADRs (0008, 0009) and one story's acceptance criteria (23.4) that were settled on the opposite assumption.

## Options considered

| Option | Verdict |
|---|---|
| **Keep server-rendered SVG + layer Plotly over it** | Rejected by the owner 2026-07-23. Fully preserves NFR-5, but institutionalizes two complete chart implementations that must never disagree — recreating, at larger scale, the exact drift ADR 0012 exists to end. |
| **Text twin only (retire the SVG)** | **Chosen.** One renderer; information preserved; visualization JS-gated. |
| **Declare charts JS-required, drop the no-JS obligation** | Rejected. Would abandon information parity, not just visual parity — a materially larger concession than this ADR makes, and it would weaken the local-first robustness story for no additional engineering benefit. |

## Ratified decisions (2026-07-24)
1. **NFR-5 is amended** to the wording in Decision 1: information and navigation must survive JS-off; **visualization need not**, provided the information remains available through a server-rendered text equivalent.
2. **The text twin is contract, not convention** — server-rendered, complete, navigable, non-color; visually collapsed or `sr-only` is acceptable (availability, not on-screen duplication).
3. **Hard per-surface gate:** no surface retires its server-rendered SVG until its text twin is audited complete **in a live browser with JavaScript disabled**, per CLAUDE.md § Verification. An incomplete twin keeps its SVG.
4. **ADR 0010 §2 is superseded** — the "useful default-mode chart must render with JS off" requirement no longer holds; the text twin discharges NFR-5.
5. **ADR 0008 / ADR 0009 are amended** — the IR carries chart **data and component configuration**, not pre-rendered SVG; Epic 22, Epic 23, and Story 23.4 AC#2 update accordingly (a scope *reduction* for both).
6. **The golden-fingerprint replacement lands in the same story as the first SVG retirement** — assertions move to the embedded payload, component configuration, and text twin.
7. **Webview fallback:** if the ADR 0012 CSP amendment cannot deliver acceptable webview chart rendering, webview surfaces present the text twin — a documented, accepted degradation.

## References
- **The change that forces this:** [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md).
- **The clause it supersedes:** ADR 0010 §2 / Ratified decision 2.
- **The non-goal it amends:** ADR 0008 and ADR 0009 ("charts stay pre-rendered SVG *in* the IR"), the Epic 22 / Epic 23 epic bodies, and Story 23.4 AC#2.
- **The requirement it amends:** PRD NFR-5 (`_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md` § 8).
- **The conventions it promotes to contract:** UX-DR17 (never color-only), UX-DR19 (non-color text equivalent of every metric), UX-DR21 (chart text-twins are accessibility contract, never removed).
- **The verification rule it depends on:** `CLAUDE.md` § Verification — live-browser verification, because the test suite structurally cannot see what a JS-off visitor receives.
