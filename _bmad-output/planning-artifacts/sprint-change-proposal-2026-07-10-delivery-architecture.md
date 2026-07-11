# Sprint Change Proposal — Delivery Architecture Reopened (JSON + SPA + npx vs. C# Static-Site + Bundled Binary)

- **Date:** 2026-07-10
- **Author:** Matthew-Hope Eland (with Dev agent)
- **Workflow:** correct-course
- **Scope classification:** Significant (reopens an Accepted ADR + foundational delivery premise; freezes in-flight stories; a spike de-risks before any rewrite — no rollback, no code pivot yet)
- **Change mode:** Batch

---

## Section 1 — Issue Summary

**Problem statement.** [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) was **Accepted 2026-07-10** on the premise *"rendering stays in C#, as little TypeScript as possible,"* with packaging = *"bundle a ~73 MB/RID self-contained .NET binary in the VSIX."* Immediately after it landed, the owner reopened that premise: he is **leaning toward a JSON data layer + a small client-side renderer (SPA), distributed via npx** — i.e. **pure TypeScript for the application** — and wants that direction **measured, not argued**, before Story 6.4 (the webview runtime) is built on a foundation that may not hold.

**How discovered.** Owner-initiated, during the create-story run for Story 6.4 — the owner stated a preference for "few pages that use JS to parse and act on JSON data, populated by analyzing the codebase and the SDD byproducts, updatable as things change," then, weighing ADR 0005's tradeoffs, leaned toward pure TypeScript / a SPA framework distributed via npx.

**Supporting evidence (from the repo + ADR 0005's own measurements):**

- **Output-file bloat is real.** The static-site generator emits one HTML file per artifact: **113 files** for this repo today. Epic 7 (per-source-file browsing, per-commit detail pages, per-date activity pages) multiplies this into the thousands on a large repository.
- **ADR 0005's "just works" answer is a fat binary.** It bundles a **~73 MB/RID** self-contained .NET publish in the VSIX (vs. ~3.5 MB framework-dependent). The owner wants a **thin** rendering surface driven by a JSON layer, not a per-RID binary matrix.
- **Distribution friction.** A .NET tool needs the runtime or a self-contained publish; **npx** is far lower-friction for SpecScribe's SDD/JS audience. .NET 10's `dnx`/`dotnet tool exec` narrows but does not close the gap.
- **The premise, not the execution, is what's in question.** ADR 0005 is internally sound and evidence-backed; the owner is re-weighting its *inputs* (is C#-render + bundled-binary the right foundation at all), which is a decision above the ADR's own scope.

---

## Section 2 — Impact Analysis

### Epic Impact

- **No epic is invalidated; no work is discarded** — this seats a **spike** to decide, and **freezes** downstream work built on the questioned premise.
- **New story:** **Story 6.6 — Delivery Architecture & Distribution Spike** (append-only in Epic 6, no renumber — same convention as Stories 4.8, 6.3/6.4, Epics 11–15). Seated in Epic 6 because it revisits ADR 0005 (an Epic 6 artifact) and directly gates 6.4/6.5. Its **scope is application-wide** (not webview-only) and its outcome (ADR 0006) may trigger a larger Epic 6/16 re-plan.
- **Frozen pending ADR 0006:**
  - **Story 6.4** (webview runtime) and **Story 6.5** (host theming) — both are built on ADR 0005's C#-render + bundled-binary path.
  - **Epic 16 packaging** — 16.1 (packaging spike), 16.3 (CLI packaging), 16.4 (release pipeline), 16.5 (extension VSIX). The delivery-architecture decision is *upstream* of packaging mechanics; deciding "NuGet global tool" now risks a pivot mooting it.
- **Not frozen:** Epic 16.2 (CI build/test gate) — release-readiness hygiene, independent of the delivery form; and all non-Epic-6/16 work.

### Artifact Conflicts

| Artifact | Impact |
|---|---|
| **ADR 0005** | Reopened. Stays the current decision until **ADR 0006** (the spike's deliverable) supersedes or re-affirms it; on supersede, 0005 gets a `Superseded by ADR 0006` header note. |
| **Epics** | Append **Story 6.6** to Epic 6. No new FR (the territory is covered by FR13/FR32/NFR4/NFR6/NFR9). Epic 6's existing epics.md numbering drift (host-theming still numbered 6.3 there, no spike entries) remains the previously-noted deferred reconciliation — not widened here. |
| **Architecture** | Potentially significant *if the pivot lands* — the spine's "shared-core contract" is preserved as invariant, but the seed (language/package layout) and the progressive-enhancement / NFR6 policy would change. ADR 0006 rules on this; no spine edit until then. |
| **UX Design** | The progressive-enhancement policy ("JS never carries information") + NFR6 accessibility are on the line for JS-rendered surfaces; ADR 0006 must state the accessibility posture (static fallback / `noscript` / accept JS-required). No UX edit until then. |
| **sprint-status.yaml** | Add `6-6-…` (`ready-for-dev`); demote `6-4-…` and `6-5-…` to `backlog` with FROZEN comments; annotate Epic 16 packaging stories as gated on ADR 0006. |

### Technical Impact

- The spike is throwaway/quarantined (`spike/`), mirroring Story 6.3. **No production pivot lands on `main`**; the generated site stays byte-identical; read-only (AD-6) honored.
- New measurements the spike adds on top of ADR 0005's: static-site **file count at scale**, **JSON payload** size + client render perf, **npx package** size/latency.

---

## Section 3 — Recommended Approach

**Selected path: de-risk with a spike before any rewrite; freeze downstream in the meantime.**

| Option | Verdict | Rationale |
|---|---|---|
| **Spike + freeze** | **Selected** | Foundational, expensive, uncertain call → the project's established de-risking pattern (6.3, Epics 11–15). Measures the four axes and the C#-port coupling before committing; keeps 6.4/6.5/16 from building on a premise that may flip. |
| Commit to the pivot now | Rejected | Would greenlight porting a mature 667-test C# core (Epics 1–4 + 3) on a lean, with no measured evidence of the bloat/perf/npx wins. |
| Re-affirm ADR 0005, proceed | Rejected (prematurely) | Possibly the right end-state — but it should be *decided by the spike's numbers*, not by inertia, given the owner's stated concerns are concrete and unmeasured. |

- **Effort:** Medium (spike). **Risk:** Low (throwaway/quarantined; product code untouched).
- **Timeline impact:** Freezes 6.4/6.5 and Epic 16 packaging until ADR 0006. Epic 16.2 (CI) and all other epics proceed unaffected.

---

## Section 4 — Detailed Change Proposals

### 4A. New Story (Epic 6, append-only)

> ### Story 6.6: Delivery Architecture & Distribution Spike
>
> As the SpecScribe maintainer, I want a hands-on spike that **measures** whether SpecScribe should pivot to a JSON data layer + client-side SPA distributed via npx — versus the current C# static-site + bundled-binary path — decided by real numbers and recorded as **ADR 0006**, so that we commit to (or reject) the pivot with evidence before any code is rewritten and before Story 6.4 is built on a premise that may not hold.
>
> Full ACs + tasks: [`6-6-delivery-architecture-and-distribution-spike.md`](../implementation-artifacts/6-6-delivery-architecture-and-distribution-spike.md). Decides four separable axes (output form / rendering language / analysis language / distribution), enumerates the C#-core-port cost + coupling-breakers (WASM, pre-generated JSON), rules on the NFR6/progressive-enhancement accessibility posture, and produces ADR 0006 (supersedes-or-reaffirms ADR 0005). Spike-throwaway; nothing pivots on `main`.

### 4B. Freeze (sprint-status.yaml)

- `6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics`: `ready-for-dev` → **`backlog`** + `# FROZEN pending Story 6.6 / ADR 0006`.
- `6-5-host-aware-theming-and-explicit-helper-actions`: `ready-for-dev` → **`backlog`** + `# FROZEN pending Story 6.6 / ADR 0006`.
- Epic 16 `16-1` / `16-3` / `16-4` / `16-5`: stay `backlog`, annotated `# gated on ADR 0006 (delivery-architecture decision upstream of packaging)`. `16-2` (CI gate) explicitly **not** frozen.
- Add `6-6-delivery-architecture-and-distribution-spike: ready-for-dev`.

### 4C. ADR linkage

- ADR 0006 is authored **by the spike** (Story 6.6), not by this proposal. On supersede, add `**Superseded by:** ADR 0006` to ADR 0005's header and update `docs/adrs/README.md`.

### 4D. Deferred (not done here, named for traceability)

- **epics.md Epic 6 numbering reconciliation** (host-theming still `### Story 6.3` there; no spike entries for 6.3/6.6) — the pre-existing deferred follow-up; this proposal adds Story 6.6 to sprint-status (operational truth) and the story-file set, and leaves the epics.md reconciliation to that follow-up plus whatever ADR 0006's re-plan triggers.
- **The Epics 6/16 re-plan itself** — a *second* correct-course, seated *by* ADR 0006 (pivot → re-plan 6.4/6.5 + packaging + a possible C#-port epic; re-affirm → unfreeze).

---

## Section 5 — Implementation Handoff

- **Scope:** Significant → **Owner + Product Owner + Developer** coordination; the spike itself is Developer-led (throwaway).
- **Sequencing:**
  1. **Story 6.6 (this spike)** — measures the four axes, writes **ADR 0006**.
  2. **ADR 0006 decision** → a follow-on correct-course re-plans Epics 6/16 (pivot) or unfreezes them (re-affirm).
  3. **6.4 / 6.5 / Epic 16 packaging** resume only under ADR 0006's ruling.
  4. **Epic 16.2 (CI)** and unrelated epics proceed in parallel now.
- **Success criteria:** ADR 0006 records a numbers-backed decision across all four axes, explicitly supersedes-or-reaffirms ADR 0005, rules on the JS-surface accessibility posture, and names the concrete re-plan — with **no production pivot on `main`** and the generated site byte-identical.
- **Deliverables on approval:** create `6-6-…` story file (done); add Story 6.6 to `epics.md` Epic 6; freeze/annotate `sprint-status.yaml` per 4B; add FROZEN banners to the 6.4/6.5 story files.

---

## Approval

- [x] **Approved for implementation** — Matthew-Hope Eland, 2026-07-10 (selected "Spike it + freeze downstream").
- [ ] Revise (feedback: ______)

**Applied on approval (2026-07-10):**
- `_bmad-output/implementation-artifacts/6-6-delivery-architecture-and-distribution-spike.md` — spike story created (`ready-for-dev`).
- `sprint-status.yaml` — `6-6` seated `ready-for-dev`; `6-4`/`6-5` demoted to `backlog` + FROZEN; Epic 16 packaging annotated gated on ADR 0006.
- `epics.md` — Story 6.6 appended to Epic 6.
- `6-4-…` / `6-5-…` story files — FROZEN banner added atop each.

---

## Ratification & Re-plan (applied 2026-07-10, follow-up)

Story 6.6's spike ran and produced **[ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md)** — which, on measured evidence (charts are ~69 % of body bytes, so a JSON layer cuts file-count not bytes; npx-via-npm-wrapper proven port-free; WASM blocked by WASI's inability to spawn `git`; the C#→TS port surface is ~14,200 LOC + 667 tests), **re-affirmed ADR 0005** (C# render + self-contained binary), **added two additive port-free changes** (an npx wrapper channel; an optional JSON+SPA delivery adapter for file-count-heavy contexts), and **deferred the full pure-TS pivot**. This **reverses the owner's original lean**, so the ADR issued as **Proposed**.

**The owner ratified ADR 0006 on 2026-07-10** (and requested the architectures-considered documentation now embedded in the ADR: five flow diagrams + a comparison table + metrics). ADR 0006 → **Accepted**. Applied:

- **ADR 0006** — Status `Proposed` → `Accepted`; added an "Architectures Considered" section (mermaid flow diagrams A–E + comparison table + measured metrics); Decider's note records ratification. `docs/adrs/README.md` updated (0006 → Accepted).
- **Unfroze** Stories 6.4 and 6.5 (`backlog` → `ready-for-dev`; FROZEN banners replaced with UNFROZEN notes — their *original* prerequisite gates still stand: 6.4 on 6.2 `done`; 6.5 on 6.4). Epic 16 packaging (16.1/16.3/16.4/16.5) un-gated.
- **Seated two additive stories** (`backlog`, detail via `create-story` when scheduled): **Story 6.7** — *JSON + client-renderer (SPA) delivery adapter* (a second C# `IRenderAdapter` with the static/`noscript` fallback; no core port); **Story 16.8** — *npx distribution via npm-wrapped native binary* (promotes the spike's proven wrapper; aligns with 16.3). Appended to `epics.md` + `sprint-status.yaml`.
- **Did NOT** seat a C#→TS core-port epic (the pivot is deferred, not adopted).
- **Still open:** Story 6.6 is `review` and needs its close-out code-review to reach `done`.
