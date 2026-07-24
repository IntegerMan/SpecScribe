# ADR 0012: Plotly.js as the Hierarchy-Chart Engine, and One Standardized Hierarchy Explorer Component

**Status:** Accepted (owner-ratified 2026-07-24, via correct-course; the engine-adoption spike is a validation step whose measurements are recorded back as an addendum — see "Spike validation" below)
**Date:** 2026-07-24
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0010 — Client-Side Charting JS for Opt-In Deep-Analytics Surfaces](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) (**supersedes §1 and §6**); [ADR 0005 — VS Code Webview Runtime](0005-vs-code-webview-runtime-and-packaging.md) (**amends** the CSP / "no scripts in the body" clause); [ADR 0013 — The Text Twin Is the No-JS Contract](0013-text-twin-is-the-no-js-contract.md) (its necessary companion); [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md) (AD-2 view models); Epic 20 (Interactive Project Explorer), Epic 24 (Change-Coupling Graphs, FR40), Epic 7 (Code Map / ownership / freshness), Epic 21 (Impact Map); memory `adr-consultation-gap-three-arc-renderers`

## Context

SpecScribe renders hierarchical data as sunbursts and treemaps on most of its analytical surfaces. That family has grown by accretion, and by 2026-07-23 it had produced three *independent* implementations of the same idea at three different layers:

**1. Seven server-side entry points** in `Charts.cs` (4,777 lines): `Sunburst`, `EpicSunburst`, `TaskSunburst`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeTreemap`, `CodeOwnershipTreemap`.

**2. Three independent client-side arc renderers** in `assets/specscribe.js` (1,961 lines) — `initOwnershipSunburst` (Story 7.11), `renderSunburst`/`arcPath` (Story 21.3), `initSunburstExplorer`/`annular`/`fullRing` (Story 20.2) — written by three concurrent sessions.

**3. Three independently built "Treemap | Sunburst" toggles** that already disagree with one another:

| Surface | Call site | Order | ID scheme |
|---|---|---|---|
| Code Map | `CodeMapTemplater.cs:182` | Treemap, Sunburst | per-variant generated |
| Git Insights (ownership) | `GitInsightsTemplater.cs:160` | **Sunburst, Treemap** | `ownership-view-*` |
| Impact Map | `ImpactMapTemplater.cs:126` | Treemap, Sunburst | `impact-view-*` |

ADR 0010 §6 already required "**ONE** shared engine/module across 7.11, 7.12, and any future opt-in analytics surface, not independently reinvented per story." **That rule did not hold.** The Epics 19+21 joint retrospective verified the violation and seated Story 20.4 to fix it by *extracting* shared arc math from the three hand-rolled renderers.

Two things make extraction the wrong remedy now:

- Extraction consolidates the *math* but leaves SpecScribe owning a bespoke charting engine — hover, drill-in, breadcrumb, transitions, hit-testing, and legend behavior all remain hand-written per surface, which is where the drift actually happened.
- The owner has already named the target twice. The Epic 20 epic note records the 2026-07-22 request verbatim — *"click and drill into a directory and filter down to that level… You can do this via Plotly and it's amazing"* — flagged there as needing "its own dependency-budget decision at spike time, not an assumed yes." This ADR is that decision.

The owner's 2026-07-23 direction is broader than the engine: **one standardized component** bundling a sunburst and a treemap over a single datasource behind a standard selector, used **everywhere**, with a **mode** governing what clicking a node does — so that site-wide changes and new features land in one place instead of seven.

There is a further constraint the codebase makes unavoidable: **UX-DR5, UX-DR6, and UX-DR7 originally specified exactly this** — an interactive multi-ring sunburst with hover tooltips, drill-down by epic and story, breadcrumb drill-up, URL-hash deep-linking, and Enter/Space/Escape keyboard drill. SpecScribe deliberately diverged to pure CSS (memory `charting-is-pure-svg-no-js`). Adopting a real charting engine does not invent new UX requirements; it **restores the originally-specified ones**.

## Decision

**1. Plotly.js is SpecScribe's hierarchy-chart engine.** It covers the `sunburst`, `treemap`, `icicle`, and `heatmap` trace families. It is **vendored locally and never loaded from a CDN** — the generated portal must keep working offline and from `file://` (NFR-3 local-first, and the portal is routinely opened as loose files).

**2. One component is the only path to a hierarchy chart.** Working name: **Hierarchy Explorer**. After the rollout, no page constructs a sunburst or treemap by any other route. Its contract:

- **One datasource per instance** — the hierarchical node shape Story 20.2 already committed (`id`, `parentId`, `label`, `value`/weight, `statusClass`, `href`, `kind`), embedded once at generation time. Both shapes read the *same* payload; switching shapes never re-derives or re-counts anything.
- **One selector** — a single ordering and control idiom site-wide, replacing the three divergent board-tab toggles above. This is UX-DR21 ("one primary representation per dataset, alternates demoted behind a toggle") made concrete rather than re-improvised per surface.
- **One framing block** — Story 10.2 legend, analysis window, and framing sentence, supplied by the component, not hand-written per call site.
- **One text twin** — mandatory, per [ADR 0013](0013-text-twin-is-the-no-js-contract.md).

**3. Node activation is governed by an explicit per-instance `mode`.** Exactly two are defined:

- **`navigate`** — activating a node follows its `href`, honoring the Story 9.13 destination contract (leaf → detail page, group → generated filtered list page). This is the behavior every current surface has.
- **`select`** — activating a node raises a selection event and **does not navigate**; other regions of the page bind to it. The dashboard uses this to drive a details pane.

Two rules make the modes safe rather than surprising:

- **Drill-in is a distinct affordance from activation.** Plotly's sunburst and treemap **drill in on click by default**; the component must intercept `plotly_sunburstclick` / `plotly_treemapclick` and suppress the default where the mode requires it. A node must never silently do two things at once.
- **`select` mode must not strand keyboard or assistive-technology users.** A selection-driven pane is a live region, and the selected node's own destination must remain reachable — the details pane carries the "view more" link precisely so `select` never removes navigation.

**4. Engine-family boundary — this supersedes ADR 0010 §6's single-engine rule.** Plotly owns **hierarchical** charts. It has **no force-directed layout and no chord/ribbon trace**, so it cannot serve Epic 24's Stories 24.2, 24.3, and 24.4. Rather than pretend one engine covers everything or let the tool dictate the product:

- Plotly is the engine for hierarchy (and, where it fits, the 24.5 adjacency matrix as a `heatmap` trace).
- **Epic 24's graph engine is a named open question**, deferred to Epic 24's own spike. It may be Plotly `scatter` with a hand-rolled layout, a second library, or bespoke — decided on evidence, not assumed here.
- **Two engine families are permitted. A third requires an ADR.** Every family must route through a component honoring the same mode / legend / text-twin contract, so the *discipline* is the invariant even when the renderer is not.

**5. The VS Code webview CSP clause of ADR 0005 is amended.** ADR 0005's "the body carries no scripts of its own" cannot survive a client-render engine. Amended narrowly: the webview may load the vendored engine and the component bootstrap under the existing nonce, and `style-src` must accommodate the runtime `<style>` Plotly injects. **This is the same ADR 0005 amendment Story 23.4 already owes — it must be landed once, not twice.** If the spike cannot achieve webview rendering under a CSP the owner accepts, webview surfaces render the text twin instead; that is a documented, accepted degradation, not a blocker on the rest of this change.

**6. Presentation tokens are SpecScribe's, not Plotly's.** The component drives every color from the existing `--status-*` and brand token families (AD-7). Plotly's default colorways are not permitted, and status must never be signalled by color alone (UX-DR17).

**7. Generation-time determinism and the no-ranking rule are unchanged.** ADR 0010 §3 stands: data is computed once at generation time and embedded — never re-derived client-side from live git state or wall-clock "now." ADR 0010 §4 stands: FR-10's no-productivity-ranking constraint is unaffected by rendering technology.

## Spike validation (direction is ratified; these are measured and recorded back as an addendum)

The direction in this ADR is decided. The Epic 20 engine-adoption spike is a **validation** step, not a ratification gate — but two of its findings can still force follow-up decisions, and the ADR names them explicitly so a bad measurement is surfaced, not swallowed:

1. **Bundle size** of a custom Plotly build limited to `sunburst` + `treemap` + `heatmap` (the full distribution is not acceptable).
2. **Net output-size delta** versus today's inline SVG across a real portal — including `code-map.html`, which has previously reached 82.5 MB. This is expected to be a *reduction*; it must be verified, not assumed.
3. **Webview CSP survival** (Decision 5), including whether the runtime `<style>` injection is tolerable under a nonce policy. **Escalation trigger:** if acceptable webview rendering is unreachable, the webview falls back to the text twin (Decision 5) — this does not reopen the engine choice.
4. **Keyboard and assistive-technology conformance** against UX-DR7 (Tab order, Enter/Space drill, Escape up), UX-DR16, and UX-DR17 — under ADR 0013 there is no server-rendered SVG to fall back to. **Escalation trigger:** a hard a11y failure Plotly cannot be configured around is the one finding that could force this ADR back open (e.g. toward the deferred ECharts option); the spike must report a11y conformance as an explicit pass/fail, not a polish note.
5. **Packaging** across all three channels: self-contained binary, npx (Story 16.8), and the VSIX (Story 16.5).
6. **Reduced-motion** conformance (UX-DR18) for Plotly's built-in transitions.

## Consequences

**Positive**
- Collapses three arc renderers, three divergent toggles, and seven server-side entry points into one component — the owner's stated goal of making site-wide changes and new features land in one place.
- Restores UX-DR5 / UX-DR6 / UX-DR7 as originally specified, instead of the pure-CSS approximation SpecScribe settled for.
- Hover, drill-in, breadcrumb, transitions, and hit-testing stop being per-surface hand-written code.
- New hierarchy surfaces (and icicle, a shape SpecScribe has never had) become nearly free.
- Replacing megabytes of inline SVG with a compact JSON payload plus one shared vendored asset is very likely a substantial output-size win.
- Ends the failure mode ADR 0010 §6 could not prevent: a shared *component* is far harder to accidentally reinvent than a shared *convention*.

**Negative / trade-offs**
- **SpecScribe's first third-party runtime dependency.** It must be vendored, audited under NFR10 (Epic 17), and packaged across three distribution channels — a permanent supply-chain and packaging obligation the zero-dependency posture did not carry.
- **Plotly's accessibility is its weakest dimension**, and under ADR 0013 there is no server-rendered SVG behind it. If the spike's a11y gate fails, this ADR does not ratify.
- The golden-HTML fingerprint — this project's primary chart regression net — stops covering chart output (see ADR 0013 §6).
- Reverses ADR 0005's "no scripts in the body" clause for the webview.
- **Two permitted engine families is a genuinely weaker invariant than one.** It is accepted deliberately: the alternative was letting Plotly's trace list decide whether Epic 24 ships force-directed and chord views.
- Story 20.4 as seated (extract shared arc math) is **invalidated** and must be replaced, not merely re-scoped.

## Options considered

| Option | Verdict |
|---|---|
| **Hand-rolled shared engine** (Story 20.4 as seated) | **Rejected.** It is the same "shared convention" remedy ADR 0010 §6 already tried and that three concurrent sessions defeated. It also buys none of the interaction behavior for free. |
| **Plotly for hierarchy; graph engine decided later** | **Chosen.** Owner-directed 2026-07-23. Best fit for the hierarchical family, honest about the Epic 24 gap. |
| **D3** | Rejected for this pass. More capable and more composable, but it is a toolkit rather than a chart library — every surface would still hand-write its chart, which is the problem being solved. |
| **ECharts** (hierarchy *and* force-directed *and* chord in one dependency) | **Considered and deferred**, not dismissed. It would preserve a true single-engine invariant. The owner chose Plotly-for-hierarchy on 2026-07-23 with the graph engine left open; **Epic 24's graph spike may legitimately reopen this**, and if it selects ECharts, superseding this ADR is the expected outcome rather than a failure. |

## Ratified decisions (2026-07-24)
1. **Plotly.js is SpecScribe's hierarchy-chart engine** — vendored locally, never CDN, `file://`-safe; custom build limited to `sunburst` + `treemap` + `heatmap`.
2. **One "Hierarchy Explorer" component is the only route to a sunburst or treemap** — one datasource, one selector, one framing block, one mandatory text twin ([ADR 0013](0013-text-twin-is-the-no-js-contract.md)); it replaces the three divergent board-tab toggles and the seven `Charts.cs` entry points.
3. **Node activation is governed by an explicit `navigate` | `select` mode**; drill-in is a distinct affordance from activation (the component intercepts Plotly's default click-to-drill); `select` never strands keyboard/AT users or removes navigation.
4. **ADR 0010 §6's single-engine rule is superseded** — two engine families are permitted (hierarchy = Plotly; Epic 24's graph engine = a named open question for its own spike), a third requires an ADR; every family routes through the same mode / legend / text-twin contract.
5. **ADR 0005's "no scripts in the body" CSP clause is amended** for the webview, landed once jointly with Story 23.4's owed amendment; if acceptable webview rendering is unreachable, the webview renders the text twin.
6. **Presentation is SpecScribe's tokens, never Plotly's colorways**; status is never color-only (UX-DR17). Generation-time determinism (ADR 0010 §3) and FR-10 no-ranking (ADR 0010 §4) are unchanged.
7. **Story 20.4 (extract shared arc math) is invalidated** and replaced by the component work; the engine-adoption spike validates bundle size, output-size delta, webview CSP, a11y (pass/fail), packaging, and reduced-motion, recorded back as an addendum.

## References
- **The rule this supersedes:** ADR 0010 §1 (baseline pages stay zero-JS) and §6 (one shared engine), and the verified three-renderer violation recorded in the Epics 19+21 joint retrospective.
- **The owner request this implements:** Epic 20 epic-body note dated 2026-07-22 (`_bmad-output/planning-artifacts/epics.md`), and the 2026-07-23 correct-course session.
- **Its necessary companion:** [ADR 0013](0013-text-twin-is-the-no-js-contract.md) — Plotly cannot server-render, so the no-JS contract must change with it.
- **The story it invalidates:** Story 20.4 (Shared Client-Side Geometry Engine), seated `backlog` 2026-07-23.
- **The CSP amendment it shares:** Story 23.4's owed ADR 0005 amendment (`_bmad-output/implementation-artifacts/23-1-spike-report.md`).
