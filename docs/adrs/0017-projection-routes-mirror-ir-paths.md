# ADR 0017: The Projection Layer's Route Space IS the IR's Path Space

**Status:** Proposed (authored 2026-07-27 by Story 23.3; ratification is the owner's)
**Date:** 2026-07-27
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0009 — Frontend Framework for the Projection Layer](0009-frontend-framework-for-projection-layer.md) (this constrains *how* the Nuxt app addresses pages, which 0009 left open); [ADR 0016 — The Canonical IR Carries Rendered Prose HTML](0016-ir-carries-rendered-prose-html.md) (the reason this is load-bearing: the IR ships whole rendered markup, links included); [ADR 0008 — JSON IR Canonical and Incremental Generation](0008-json-ir-canonical-and-incremental-generation.md) (the IR whose paths this adopts); [ADR 0006](0006-delivery-architecture-and-distribution.md) (packaging, Story 23.5); Epic 22, Epic 23 (Stories 23.3, 23.4, 23.5)

## Context

ADR 0009 chose Vue + Nuxt 3 with universal rendering and full prerender, but said nothing about what a page's **address** is in the projected site. That looked like a detail. It is not.

ADR 0016 settled that the IR carries **Markdig-rendered prose HTML as strings** — whole rendered content regions, travelling verbatim. Those strings contain the site's entire link graph, written as **relative** hrefs against each page's own depth: `../specscribe.js`, `../epics.html`, `code/src/SpecScribe/Charts.cs.html`, `../../docs/adrs/0013-…`. Story 23.3 counted **88,695 internal `<a href>`s across 1,049 pages**.

That leaves exactly two options, and they are not close:

1. **A clean, extension-less route space** (`/epics/epic-23`), which is what Nuxt's file-based routing is built for. Every one of those 88,695 hrefs then has to be **rewritten** at injection time, by a rewriter that understands relative depth, fragments, queries, and the difference between a page link and an asset link — and that rewriting immediately destroys the one property Story 23.1 measured and Story 23.3 depends on, namely that the IR's bytes arrive **unmodified**. Byte parity against the golden page stops being checkable, because the thing being compared has been edited in flight.

2. **Routes that ARE the IR's output-relative paths, verbatim** (`/epics/epic-23.html`). Every href resolves unchanged, because the projected site occupies the same path space the emitter wrote them for. Nothing is rewritten, so nothing can be rewritten wrongly.

Story 23.3 took option 2 and measured the result: `<main>` **byte-identical on 189 of 189 migrated surfaces**, the IR's body present **verbatim** in the emitted HTML on all 189, and **zero link regressions** against the golden site across all 88,695 internal links.

This is a cross-cutting contract rather than a story-local choice. It constrains the IR's path scheme (Epic 22 may not reshape paths without reshaping the projected site's URLs), Story 23.4's remaining surfaces, and Story 23.5's packaging. CLAUDE.md requires that such a decision be proposed as an ADR rather than buried in a story file.

## Decision

**1. A projected page's route is the IR page's `outputRelativePath`, verbatim, with a leading slash.** `index.html` → `/index.html`; `epics/epic-23.html` → `/epics/epic-23.html`. The `.html` extension is part of the route, not an artefact to be cleaned up.

**2. No href inside IR content is ever rewritten.** Not for depth, not for extension, not for a base path. If a link does not resolve, the fix belongs upstream in the emitter that wrote it, never in the projection layer. (Story 23.3's link harness found 487 links that dangle **in the golden site too** — inherited defects, faithfully reproduced, and correctly not patched over here.)

**3. The route table is generated from the IR manifest, and every page in it prerenders.** Not only migrated surfaces: a link graph can only be proven end to end if every destination exists. Pages without a migrated treatment render through a pass-through and are marked as such in the emitted HTML (`data-ir-family="pass-through"`), so a resolving destination is never mistaken for finished work.

**4. Nitro's link crawler stays off** (`crawlLinks: false`). It walks hrefs inside injected content and aborts the build on the first 404, which — given decision 2 and the inherited dangling links above — means it can never be enabled while the emitter's own link graph is imperfect. The manifest-driven table is the correct source regardless.

**5. Two consequences are accepted explicitly**, because they are the price of decisions 1–2 and should not be rediscovered as surprises:

- **Nuxt's file-based routing cannot express these routes.** There is no valid `pages/epics.html.vue`. All IR routing goes through a single `pages/[...path].vue` catch-all that resolves the path against the manifest and branches to a surface component. This is a structural consequence, not a workaround to be tidied away.
- **Nitro will not write a route whose path contains the substring `..`** — its `canWriteToDisk` guard is a substring test, not a path-segment test. SpecScribe emits a code page per repository file, so any source file with two consecutive dots in its name (this repo has one) is silently skipped: rendered, logged, not written. The projection layer writes those pages itself, enforcing the guard's actual intent (containment within the public directory) properly.

## Consequences

**Good.**

- Byte parity is a checkable claim rather than an aspiration, on every surface, forever — `npm run measure:parity` compares golden → IR → emitted and attributes each delta to a stage.
- The link graph is provable: `npm run check:links` walks the emitted site, resolves every href relative to its own page, and compares link-for-link against the golden site, so a *migration* regression is distinguishable from an *inherited* defect.
- Migrating a surface never risks its outbound links, because migration does not touch them.
- The projected site is drop-in URL-compatible with the generated portal. An existing bookmark, an external inbound link, and the VS Code webview's captured hrefs all keep working.

**Bad, and accepted.**

- The URLs carry a `.html` extension, which is unfashionable and reads as "static site" rather than "app". That is what it is.
- One catch-all route means Nuxt's routing conventions — nested layouts, per-route middleware by file position — are unavailable to IR surfaces. Branching happens in code instead.
- If Epic 22 ever renames the IR's path scheme, the projected site's URLs move with it. That coupling is deliberate and is the point of this ADR, but it makes a path rename a **public** change rather than an internal one.

**Neutral.**

- Nothing here constrains how the output is *served* or packaged; that stays Story 23.5's. It does mean any packaging must preserve relative-path resolution from an arbitrary depth.

## Alternatives considered

**Rewrite hrefs into a clean route space.** Rejected: it forfeits byte parity (the property that made ADR 0016's rendered-prose decision measurable in the first place), and it puts a bespoke relative-link rewriter — the exact component that already produced this repo's nested-anchor defect — on the critical path of all 88,695 links.

**Serve IR paths but redirect to clean URLs.** Rejected: it needs a server, and ADR 0009 committed to full prerender with a static output.

**Migrate only the four families and leave everything else unrouted.** Rejected as the default for Story 23.3 by owner decision: a link check that stops at the migrated set measures its own route table rather than the site.
