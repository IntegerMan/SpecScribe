# ADR 0009: Front-End Framework for the Projection Layer — Component-Oriented Presentation over the Canonical IR

**Status:** Accepted (owner-ratified 2026-07-20 — topology = **universal/SSR via Vue + Nuxt**; C#-rendering north star relaxed for the presentation layer; see Decision)
**Date:** 2026-07-20
**Deciders:** Matthew-Hope Eland
**Depends on:** [ADR 0008 — JSON IR as Canonical Representation](0008-json-ir-canonical-and-incremental-generation.md) (the IR this presentation layer consumes)
**Relates to:** [ADR 0006 — Delivery Architecture](0006-delivery-architecture-and-distribution.md) (partially un-defers its **axis B / rendering-language**; leaves **axis C / analysis port** deferred)

## Context

The owner has real, evidence-backed concerns about the **presentation layer's maintainability**, independent of the byte/npx concerns ADR 0006 weighed:

- **CSS fragility.** The site CSS is a large, largely-monolithic authored stylesheet. A documented incident: a single comment containing `*/` (in `--status-*/chart-local`) **silently closed the comment early and broke ~1,000 rules with zero errors** — the failure mode of a non-modular, unscoped stylesheet.
- **Low modularity / large files.** Rendering lives in C# templaters (~4,691 LOC) + chart SVG generators (~1,425 LOC); several presentation files are large and interleave structure, styling, and logic.
- The **IR decision (ADR 0008)** makes a component-oriented front end *natural*: once surfaces are projections of a JSON IR, a component framework (Vue or similar) with **scoped, component-local CSS** can consume that IR and give the modularity the current templater+monolithic-CSS approach lacks.

**Why this needs its own ADR — and its own honest accounting.** Moving presentation to a JS framework is precisely ADR 0006's **axis B (rendering language)** — an estimated **~6,100 LOC** of C# rendering (templaters + charts) whose reimplementation ADR 0006 *deferred*. This ADR re-opens **only axis B**, and only for a **new reason ADR 0006 did not weigh: presentation-layer maintainability** (not bytes, not npx). It does **not** re-open **axis C** — the C# core still produces the IR (ADR 0008); analysis stays in C#.

### The load-bearing tension: NFR6 stops being free

ADR 0005/0006 leaned hard on one property: **because C# renders finished HTML, the JS-optional accessibility baseline (NFR6) is free.** If a JS framework renders the content **in the client**, that property breaks — a client-rendered SPA violates NFR6 on its face. So the real decision here is **not "which framework"** but **the rendering topology**, which governs whether NFR6 survives and whether we run one renderer or two.

This also moves against ADR 0005's stated north star ("**as little TypeScript as possible**; rendering stays in C#"). That north star was chosen for the *webview seam*; adopting a front-end framework consciously revises it for the *presentation layer*. That revision is exactly what must be ratified, which is why this ADR is **Proposed**.

## Decision (Accepted — two axes)

> **Ratified by the owner (Matthew-Hope Eland) 2026-07-20:** Axis 1 = **Option B, universal/SSR**. Axis 2 = **Vue** with its current modern supporting layers (**Nuxt 3** — Vue 3 / Vite / Nitro — TypeScript, scoped-SFC / CSS-module styling). ADR 0005's "rendering stays in C#" north star is **consciously relaxed for the presentation layer** (analysis stays in C# per ADR 0008; only rendering moves). Still **spike-gated** and sequenced **after** Epic 22's IR — see Disposition.

### Axis 1 — Rendering topology (the NFR6-governing choice) — **RATIFIED: Option B (universal/SSR)**

| Option | How NFR6 baseline is met | Renderers | Build/toolchain | Verdict |
|---|---|---|---|---|
| **A. Client-render (CSR) + C# keeps emitting static HTML** | C# static HTML baseline, framework hydrates on top | **Two** (C# + framework) — **drift risk** (the exact AD-1/AD-2 hazard) | No new build step | Not recommended — dual-renderer drift |
| **B. Universal / SSR via Node build** | Framework renders the static baseline at build time **and** hydrates client-side | **One** (framework) — no drift | **Adds Node to the generation pipeline**; retires C# `HtmlRenderAdapter` for content | **Recommended target** — single renderer, modular components, scoped CSS, **NFR6 stays free** |
| **C. Islands / static-first hybrid** (e.g. Astro-style) | Static HTML is the default output; interactivity is opt-in "islands" | **One** (framework, static-first) | Adds Node build; most NFR6-aligned by construction | **Strong alternative** — closest to today's static-first posture |

**Ratified: Option B (universal/SSR).** Nuxt prerenders every route to static HTML at build (`nuxt generate` / Nitro prerender) — so the **NFR6 baseline is fully rendered HTML, preserved by construction** — then hydrates for interactivity. Option A (client-only) is rejected: its dual-renderer drift re-introduces the precise failure AD-1/AD-2 exist to prevent. Option C's *spirit* is folded in as a tuning constraint: keep hydration/JS minimal (Nuxt lazy/`<NuxtIsland>` partial hydration) so interactivity is additive, not load-bearing — the surface must remain fully usable pre-hydration.

### Axis 2 — Framework choice (sub-decision; open)

**RATIFIED: Vue + Nuxt 3** (Vue 3, Vite, Nitro), TypeScript, scoped SFC `<style scoped>` / CSS modules for component-local styling. Nuxt provides the universal/SSR + build-time prerender (SSG) that Option B requires. The spike (below) still owns the *integration* proof, not the framework choice: chart-SVG injection, Markdig-prose fidelity, scoped-CSS migration of a representative surface, and **webview-CSP compatibility** (nonce-locked `script-src`, per ADR 0005 §4 — Nuxt's hydration script must run under a nonce, no `unsafe-inline`).

*(Alternatives considered and set aside at ratification: Astro/Lit/Svelte — all viable, but the owner chose Vue for familiarity and ecosystem; the topology decision, not the framework, is what protects NFR6.)*

### Charts

Charts stay **pre-rendered SVG carried in the IR (ADR 0008)** — the framework renders *chrome, layout, and interactivity*, injecting chart SVG as data. This preserves the pure-SVG accessibility story and keeps the ~1,425-LOC chart generators in C# out of scope for this port. (A later, separate decision could re-render charts client-side — that is the ADR 0006 byte-win path and is **not** proposed here.)

### Disposition — direction locked, build spike-gated, sequenced after the IR

The **direction is now decided** (Vue + Nuxt, universal/SSR). The **build** is still gated and later. It:
1. **Depends on ADR 0008's IR existing** (Epic 22) — the Nuxt app consumes the IR; no point before it exists.
2. **Opens with a spike** proving: Nuxt SSG/prerender NFR6 baseline, scoped-CSS migration of a representative surface, chart-SVG injection, webview-CSP survival under a hydration nonce, Markdig-prose fidelity, and pipeline cost of adding Node to generation.
3. Seated as **Epic 23** (backlog, unscheduled), sequencing **after** Epic 22, and **must not** disturb Epic 7 or the current roadmap.

## Consequences

**Positive**
- Component modularity + **scoped, component-local CSS** — directly addresses the fragility (the `*/` incident class) and large-file concerns.
- Under Option B/C, **one renderer** — removes the C# templater↔framework drift hazard and *shrinks* the C# surface (HtmlRenderAdapter retires for content).
- Aligns with the IR direction (ADR 0008) and the already-shipped SPA adapter (Story 6.7) and Epic 20's first client-JS surface.

**Negative / trade-offs**
- Re-opens ADR 0006 **axis B** — reimplementing ~6,100 LOC of rendering; the **Markdig custom-renderer fidelity** risk (Mermaid, comment annotations, link rewriters, gherkin/capability stylers ≈ 889 LOC) resurfaces for any prose that moves out of C#.
- **Adds Node to the generation pipeline** (Option B/C) — against ADR 0005's "as little TypeScript as possible" north star; a conscious revision.
- Distribution/packaging (Epic 16) gains a Node build; the self-contained-binary story (ADR 0005/0006) must be reconciled with a Node build step.
- If pursued as CSR-only (Option A), **NFR6 breaks** and dual-renderer drift returns — the reason A is not recommended.

## Ratified decisions (2026-07-20)
1. **Axis 1 topology:** **Option B — universal/SSR** (Nuxt prerender for the NFR6 baseline + hydration for interactivity; keep hydration minimal). Option A (client-only) rejected.
2. **North star:** ADR 0005's "rendering stays in C#" is **relaxed for the presentation layer** — accepted. Analysis stays in C# (ADR 0008); only rendering moves to Vue/Nuxt.
3. **Framework:** **Vue + Nuxt 3** with current modern supporting layers (Vite, Nitro, TypeScript, scoped-SFC CSS).

**Remaining spike-owned unknowns** (feasibility, not direction): Markdig-prose fidelity in the Vue layer, chart-SVG injection ergonomics, webview-CSP under a hydration nonce, and the Node-in-pipeline packaging reconciliation with ADR 0005/0006's self-contained binary.

## References
- **The IR this consumes:** [ADR 0008](0008-json-ir-canonical-and-incremental-generation.md).
- **The deferral this partially un-defers (axis B only):** [ADR 0006](0006-delivery-architecture-and-distribution.md); **the north star it revises:** [ADR 0005](0005-vs-code-webview-runtime-and-packaging.md) §1.
- **Evidence of CSS fragility:** memory `css-comment-star-slash-silent-truncation`; **token systems to preserve:** `specscribe-status-token-system`, `motion-token-system`.
- **Architecture:** [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1/AD-2 (single renderer; drift prevention), AD-7 (presentation tokens shared, host chrome host-owned), NFR6 (accessibility).
