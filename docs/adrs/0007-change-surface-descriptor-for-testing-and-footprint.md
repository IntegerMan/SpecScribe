# ADR 0007: A "Change Surface" Descriptor for Testing a Change and Understanding Its Footprint

**Status:** Proposed
**Date:** 2026-07-16
**Deciders:** Matt Eland

## Context

When a story or spec lands, the single most expensive question to answer after the fact is **"what
does this change actually make visible, and how do I confirm it works?"** Today that answer is
scattered. To reconstruct the footprint of a feature you must read the whole implementation
artifact end-to-end — the ACs, the "Where this renders" prose, the Tasks, the Scope in/out list,
the Reuse map, and (if the author included one) the trailing "Verify before marking review"
paragraph. There is no single, predictable place that says *here is the observable surface this
change adds, here is where to point your eyes, here is how to trigger each state.*

The information already exists — it is just unstructured and inconsistently present:

- Story 9.4 buries its testing walkthrough in a prose "Verify before marking review" paragraph and
  its render location in a "Where this renders (read before touching)" section — rich, but you have
  to read ~180 lines to extract "open `epics/story-6-9.html`, look for three pills under the status
  badge, click to jump to the dev record, confirm parity in the webview + SPA."
  [Source: `_bmad-output/implementation-artifacts/9-4-verification-evidence-strip-on-story-pages.md`]
- Story 9.1 states its verification as one sentence naming concrete URLs (`requirements/fr2.html`
  plus a deferred and an unmapped FR) and the exact things to confirm.
  [Source: `_bmad-output/implementation-artifacts/9-1-requirement-pages-link-to-their-covering-stories.md`]
- Story 6.9 spreads its surface across a VS Code tree view, a status-bar item, manifest
  contributions, and an `extension/README.md` F5 checklist — a fundamentally different surface than
  the HTML site, with an error/stale variant that "must be visible on both surfaces."
  [Source: `_bmad-output/implementation-artifacts/6-9-native-project-outline-tree-view-and-status-bar.md`]
- Story 8.3, by contrast, declares "**no new visible surface** … The ledger is plumbing" — an
  equally important footprint fact stated only in passing inside the Scope section.
  [Source: `_bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md`]

Across the corpus the *same facets* recur every time a reviewer or tester needs to understand a
change, but each artifact expresses them in a different shape and location. The recurring facets
are:

| Facet | Example evidence in the artifacts |
|---|---|
| **Which delivery surfaces** it paints on (static HTML, VS Code webview, JSON+SPA, VS Code chrome, CLI) | 9.4: "propagates byte-identically to HTML + webview + SPA"; 6.9: tree view + status bar |
| **Concrete entry points** — URLs, pages, commands, CLI flags | 9.1: `requirements/fr2.html`; 9.4: `epics/story-6-9.html`; 6.9: *SpecScribe: Open Status* |
| **New vs. modified vs. retired** output pages | 7.6: `code-map.html` added, `structure.html` deleted; 8.3: none |
| **Visible elements / selectors** — CSS classes, icons, badges, charts | 9.4: `.evidence-strip`, `.evidence-pill.empty`; new `Tests`/`Verified` icons |
| **States & variants** — populated, honest-absence, error/stale, reduced-motion, no-JS, theme | 9.4 empty-state pills; 6.9 stale/error indicator; treemap reduced-motion |
| **Trigger preconditions** — the data shape needed to make it appear | 9.4: "a done story whose Change Log has a dated entry"; 9.1 fixtures |
| **Interactions** — click, hover, keyboard, context action, drill-down | 6.9 reveal-in-panel, context menu; 9.4 click-to-dev-record |
| **Cross-cutting invariants** — render parity, determinism/golden fingerprint, a11y text-equivalent | 9.4 golden fingerprint regen; 7.8 sr-only `.ref-list` equivalent |

Because these facets are unstructured, three concrete costs recur: (1) reviewers re-derive the test
plan by reading the whole story; (2) regressions on *secondary* surfaces (webview, SPA, sr-only
fallback) slip because the surface list was never enumerated in one place; and (3) "this change is
invisible plumbing" is indistinguishable at a glance from "this change forgot to describe its
surface."

## Decision

Adopt a single, predictable **Change Surface** descriptor — a short structured section that every
implementation artifact carries near its top (right after the ACs, before the Tasks), answering
exactly one question: **"what does this change make observable, and how do I see each part of it?"**

It is a *descriptor of the observable footprint*, not a second copy of the design. It contains only
what a tester or a reviewer standing in front of the running product needs. Authoring it is cheap
because the author already knows every field at implementation time; the value is consolidating
them into one greppable, predictable block instead of scattering them across prose.

### The eight fields of the Change Surface descriptor

1. **Surfaces touched** — a fixed checklist of SpecScribe's delivery surfaces, each marked
   touched/untouched, so a `no new visible surface` change is *explicit* rather than inferred:
   - [ ] Static HTML site
   - [ ] VS Code webview
   - [ ] JSON + SPA adapter
   - [ ] VS Code chrome (tree view · status bar · commands · menus · settings)
   - [ ] CLI (`generate` / `watch` flags, interactive settings)
   - [ ] None — plumbing only (state why the output is byte-identical)

2. **Entry points (where to look)** — the concrete, copy-pasteable places to point your eyes:
   generated page paths (`epics/story-6-9.html`, `requirements/fr2.html`, `code-map.html`), VS Code
   command titles (*SpecScribe: Open Status*), CLI invocations (`specscribe generate --source …`).
   Prefer real pages from *this* repo's own `_bmad-output` so the tester can reproduce immediately.

3. **Page inventory delta** — pages **added**, **modified**, and **retired/removed** by this change.
   This is the "full footprint" line: it names the file-count and URL-shape impact (e.g. Story 7.6
   added `code-map.html` and *deleted* `structure.html`; Story 8.3 added nothing).

4. **Visible elements & selectors** — the actual paintable artifacts, named by their stable
   selectors so a tester can grep the generated HTML/CSS: new CSS classes (`.evidence-strip`,
   `.evidence-pill.empty`), icons/glyphs, badges, charts, text strings, DOM anchors
   (`#sec-dev-agent-record`). Include the *stylesheet* additions guarded by `StylesheetTests`.

5. **States & variants** — every distinct visual state and how to force each one:
   - populated / happy path,
   - **honest-absence / empty state** (the designed empty treatment, e.g. "no test evidence recorded"),
   - error / stale (e.g. the failed-refresh warning that "must be visible on both surfaces"),
   - accessibility & motion fallbacks — `noscript`/no-JS view, reduced-motion, sr-only text
     equivalent, theme (light/dark, VS Code high-contrast).

6. **Trigger preconditions** — the minimal data shape that makes each state appear (e.g. "a `done`
   story whose `## Change Log` has a dated verify/review entry" ⇒ the *verified* pill; a story with
   no dev record ⇒ the empty-state pill). Name the fixture or the real artifact that exhibits it.

7. **Interactions** — every user-triggerable behavior on the surface: clicks, hovers/tooltips,
   keyboard navigation, drill-down/breadcrumb, context-menu actions, watch-mode live refresh — each
   with its expected read-only result.

8. **Invariants to re-check** — the cross-cutting properties this surface must preserve, so testing
   is not just "it renders" but "it renders *correctly everywhere*": render **parity** across HTML /
   webview / SPA (or an explicit, justified exception), **determinism** / golden-fingerprint
   regeneration, **accessibility** text-equivalent (NFR6/UX-DR16), never-color-only (UX-DR17), and
   single-source-of-truth for any count.

### Worked example — Story 9.4's descriptor, extracted from its existing prose

> **Surfaces touched:** ☑ Static HTML ☑ VS Code webview ☑ JSON+SPA · ☐ VS Code chrome ☐ CLI
> (shared `RenderStoryBody`, so all three paint identically — no parity exception).
> **Entry points:** open `epics/story-6-9.html` (a `done` story); open this story's own page for the
> empty state.
> **Page inventory delta:** none added/retired; every *drafted story page* is modified (⇒ golden
> fingerprint regen expected).
> **Visible elements:** `.evidence-strip`, three `.evidence-pill`s (Tasks/Tests/Verified),
> `.evidence-pill.empty`, `.evidence-link`, new `Tests` & `Verified` icons; anchor target
> `#sec-dev-agent-record`.
> **States & variants:** all-three-present · tests-absent · no-changelog (⇒ "updated" not "verified")
> · no-dev-record (no link) · no-status (strip omitted).
> **Trigger preconditions:** Tasks from `ProgressCalculator`; Tests from `## Dev Agent Record` free
> text; Verified date from the top `## Change Log` entry.
> **Interactions:** click the strip → jumps to the Dev Agent Record.
> **Invariants:** HTML/webview/SPA parity (no exception); deterministic first-match extraction;
> icon+word so no state is color-only; golden fingerprint regenerated deliberately.

Everything above is already stated in the artifact — the descriptor only relocates it into one
predictable, testable block.

## Options Considered

### A. Structured "Change Surface" descriptor section — *chosen*

A fixed eight-field block in each implementation artifact. Cheap to author (the facts are known at
implementation time), greppable, and it makes "no visible surface" an explicit, first-class answer.
Low risk: it is a documentation convention, adds no code, and degrades gracefully (a partially
filled descriptor is still more useful than scattered prose).

### B. Keep the status quo — free-form "Verify before marking review" prose

Rejected. It is inconsistently present (roughly the Epic 9 stories have it; many earlier ones do
not), lives at the *bottom* of long artifacts, and omits the surface *inventory* (added/retired
pages, per-surface matrix) that the footprint question needs. It answers "how do I smoke-test the
happy path" but not "what is the full observable footprint."

### C. Make SpecScribe *render* a per-story "Change Surface" panel from a machine-readable block

Attractive long-term — SpecScribe is exactly the tool that could surface this as a portal panel on
each story page, alongside the Story 9.4 evidence strip. But it collides with a **non-negotiable
project value: no new required authoring schema** (SpecScribe must support many spec-driven
frameworks without dictating a house authoring style; see Story 9.4's guardrail). A machine-parsed
`change-surface:` block would be exactly the mandated schema that value forbids. Deferred: adopt the
prose convention now (Option A); only if a rendered panel is later wanted does its authoring-burden
trade-off get weighed in a follow-up ADR — mirroring how Story 9.4 explicitly punted a tagged
verification record to "an ADR weighing the authoring-burden trade-off."

### D. A repo-level testing checklist decoupled from the artifact

Rejected. A single shared checklist cannot name the concrete URLs, selectors, and preconditions that
make a *specific* change testable; those are inherently per-change and belong with the change.

## Consequences

**Positive**

- A reviewer or tester gets the full observable footprint of any change from one predictable block —
  no more reading the whole artifact to reconstruct a test plan.
- Secondary surfaces (webview, SPA, `noscript` fallback, sr-only equivalent, VS Code chrome) are
  *enumerated*, so regressions on the non-primary surface are less likely to slip.
- "Invisible plumbing" changes are explicitly labeled (Surfaces touched → *None*), distinguishing a
  deliberate no-op footprint from an under-described one.
- The descriptor doubles as the acceptance smoke-test script and as onboarding context ("what does
  this feature look like and where") without duplicating the design rationale.

**Negative / costs**

- A small, recurring authoring cost per artifact, and a mild redundancy with the existing "Verify
  before marking review" and "Where this renders" prose (those can be folded into the descriptor
  over time rather than duplicated).
- It is a convention, not an enforced schema, so its quality depends on author discipline; a stale
  or thin descriptor is possible. This is an accepted trade-off in exchange for *not* mandating a
  parsed authoring schema (Option C's rejected cost).

**Neutral**

- No code change and no impact on the generated site or the six `--status-*` tokens; this ADR
  governs artifact authoring only.
- Consistent with ADR 0002's shared-render-core view: because most surface changes flow through one
  shared renderer, the "Surfaces touched" matrix usually collapses to "all three, byte-identical,"
  which the descriptor records once rather than the author re-deriving parity each time.
