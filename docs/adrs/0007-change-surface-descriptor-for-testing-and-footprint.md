# ADR 0007: Deriving a Change's Visible Surface by Reading Standard BMAD Story Sections

**Status:** Accepted
**Date:** 2026-07-16
**Deciders:** Matt Eland

## Context

We want a tool-supported way to answer, for any completed story, **"what does this change actually
make visible, and how do I confirm it works?"** — so a reviewer or tester can see a feature's full
footprint without reading the entire implementation artifact end-to-end.

Two hard constraints shape this decision:

1. **Do not modify the artifacts and do not invent a new authoring schema.** SpecScribe's ability to
   render *many* spec-driven frameworks without dictating a house authoring style is a load-bearing
   project value (the same guardrail Story 9.4 invoked when it refused to add a `verified:`
   frontmatter field). So the "visible surface" must be *derived by reading sections that already
   exist* in a normal story — never by adding a `change-surface:` block or asking authors to fill in
   anything new.
2. **Work against default BMAD, not just SpecScribe's own richly-authored stories.** This repo's
   artifacts happen to carry generous custom prose ("Where this renders", "Verify before marking
   review", "Reuse map", "Scope in/out"). A default BMAD story has **none of those guaranteed** — it
   has only the canonical template sections. The derivation must lean on what the BMAD template
   *always* produces, and treat the custom prose as a bonus when present.

The canonical BMAD story template (`bmad-create-story/template.md`) guarantees these sections:
**Status**, **Story** (As a / I want / so that), **Acceptance Criteria**, **Tasks / Subtasks** (with
`(AC: #)` references), **Dev Notes** (incl. **Project Structure Notes** and **References** with
`[Source: …]` citations), and **Dev Agent Record** (**Agent Model Used**, **Debug Log References**,
**Completion Notes List**, **File List**). A **Change Log** section is not in the base template but is
present on nearly every story in practice. [Source: `.agents/skills/bmad-create-story/template.md`]

The good news is that the visible-surface signal is *already there* — it is just spread across these
standard sections and expressed in prose. The recurring facets a reviewer needs, and the standard
section each is most reliably recoverable from:

| Facet the tester needs | Recoverable from (standard BMAD section) | Example evidence |
|---|---|---|
| **What files/footprint changed** | **Dev Agent Record → File List** | 9.4 File List: `EpicsParser.cs`, `HtmlRenderAdapter.Epics.cs`, `specscribe.css` … |
| **Observable behaviors to confirm** | **Acceptance Criteria** (Given/When/Then) | 9.4 AC#1 "a compact evidence strip appears near the status badge" |
| **Work breakdown → what was actually built** | **Tasks / Subtasks** (with `(AC: #)`) | 9.4 Task 4 "Render the pill strip in the story-page header" |
| **User-facing intent / who it's for** | **Story** (role · action · benefit) | 9.4 "As a reviewer … judge a done claim in one glance" |
| **Concrete code / requirement anchors** | **Dev Notes → References** `[Source: …]` | 9.4 `[Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs:256-357]` |
| **What shipped, and whether it's done** | **Status** + **Change Log** + **Completion Notes** | 9.4 "1202 passed … Status → review"; empty-state pills implemented |
| **Structure/paths & conflicts** | **Dev Notes → Project Structure Notes** | 9.4 "Primary code: `EpicsParser.cs` … `assets/specscribe.css`" |

Because this signal is unstructured prose, the recurring cost is that a reviewer must read the whole
story to reconstruct a test plan, and regressions on *secondary* surfaces slip because nothing
enumerates the footprint in one place. The opportunity: a tool can *read these standard sections and
project the visible surface for the reviewer* — no artifact change, portable to any BMAD story.

## Decision

Build the "visible surface" view by **reading the standard BMAD story sections in a fixed priority
order** and projecting them into a change-footprint the reviewer can act on. Nothing is added to the
artifacts; the tool consumes only sections a default BMAD story already contains, degrading
gracefully when a section is thin or absent.

### The reading order — backbone sections first

Read from most-reliable to least, so the footprint holds up even on a sparse story:

1. **File List (Dev Agent Record) — the footprint backbone.** This is the single most reliable,
   machine-friendly signal: the concrete set of files the change touched. It is a literal list of
   paths, so it needs no NLP. From the paths alone you can classify the surface with generic,
   framework-agnostic heuristics:
   - style/asset paths (`*.css`, `*.scss`, `assets/…`) ⇒ a *visual* change,
   - view/template/component/markup paths ⇒ a *rendered UI* change,
   - test paths (`*Tests*`, `test/…`, `spec/…`) ⇒ where the executable proof lives,
   - config/build/manifest paths ⇒ packaging/toolchain surface,
   - everything else ⇒ logic/plumbing.
   A File List that is **all tests + logic and no view/asset paths** is the portable signal for
   "plumbing, no new visible surface" — recovering Story 8.3's fact *without* relying on its custom
   prose. [Source: `.agents/skills/bmad-create-story/template.md` (File List)]

2. **Acceptance Criteria — the observable behaviors to confirm.** The Given/When/Then ACs are, by
   construction, statements of externally observable outcomes ("*a compact evidence strip appears
   near the status badge*"). They are the ready-made **test checklist**: each AC is one thing to
   verify. This is guaranteed on every BMAD story and is the most direct "how do I confirm it works"
   source.

3. **Tasks / Subtasks — what was actually built, and the AC mapping.** Tasks name the concrete work
   ("*Render the pill strip in the story-page header*") and carry `(AC: #)` back-references, giving a
   task→AC coverage map. Completed (`[x]`) vs. open (`[ ]`) checkboxes also reveal how much of the
   described surface actually shipped.

4. **Story statement — user-facing intent.** Role · action · benefit frames *who* the surface is for
   and *why*, so the tester knows the persona and the value to sanity-check against.

5. **References (`[Source: …]`) — anchors into code and requirements.** The citations point at the
   exact files/sections the change rests on, letting the tool cross-link the footprint to source
   (and, in SpecScribe, to its own generated code pages) — again with zero authoring change.

6. **Status + Change Log + Completion Notes — did it ship, and what does "done" mean here.** Status
   gates whether the surface is real yet; the Change Log's newest dated entry and the Completion
   Notes summarize what landed (test counts, "Status → review", etc.) — the same free text Story 9.4
   already mines for its evidence strip, reused here for footprint provenance.

7. **Project Structure Notes — paths, modules, and flagged variances** — a secondary confirmation of
   where the change lives when the File List is terse.

### What the tool projects

From those sections the tool assembles a compact **per-story surface view**:

- a **footprint classification** (visual / rendered-UI / plumbing / packaging) derived from File List
  path heuristics;
- the **AC checklist** as the ready-to-run verification list;
- the **changed-files list**, linked to source where References/citations resolve;
- a **"did it ship"** line from Status + the latest Change Log entry.

### Portability first; custom prose is a bonus, never a dependency

The backbone above uses only guaranteed BMAD sections, so it works on any default BMAD story. Where a
project *does* author richer prose, the tool may **opportunistically enrich** the view — e.g. if a
"Verify before marking review" paragraph or a "Where this renders" section is detected by heading,
surface it verbatim as an extra hint. But detection is best-effort and its absence never degrades the
backbone. The tool must never *require* SpecScribe-shaped prose to produce a useful surface view.

### Worked example — reconstructing Story 9.4's surface from standard sections only

Reading *only* the guaranteed sections of `9-4-verification-evidence-strip-on-story-pages.md`:

- **File List** → `EpicsParser.cs`, `EpicsView.cs`, `EpicsViewBuilder.cs`, `EpicsTemplater.cs`,
  `SiteGenerator.cs`, `HtmlRenderAdapter.Epics.cs`, `assets/specscribe.css`, plus test files. The
  presence of a `*.css` asset **and** an `HtmlRenderAdapter`/templater view path classifies this as a
  **visual + rendered-UI** change (not plumbing) — inferred purely from paths.
- **Acceptance Criteria** → the verification checklist writes itself: (1) "a compact evidence strip
  appears near the status badge and links to the dev record", (2) "missing evidence is visibly absent
  … using the empty-state treatment."
- **Tasks / Subtasks** → confirm what shipped and where: "Render the pill strip in the story-page
  header", "Style the strip and its empty states" (`(AC: #1, #2)` mapping intact).
- **Status + Change Log** → `review`; latest dated entry confirms it landed with tests green.

That reproduces "a visual change to story pages; confirm the strip renders near the status badge and
its empty state; the changed files center on the epics renderer + stylesheet" — **without reading a
single line of the story's custom prose.** The bespoke "Where this renders" / "Verify before marking
review" sections, when present, only sharpen the picture; they are never load-bearing.

## Options Considered

### A. Read the standard BMAD backbone sections and project a surface view — *chosen*

Derive the footprint from File List + Acceptance Criteria + Tasks + Story + References + Status/Change
Log — all guaranteed (or near-universal) in a default BMAD story. Zero artifact change, no new schema,
portable to any BMAD-based repo. Degrades gracefully: even a story with only a File List and ACs
yields a useful footprint. Best-effort enrichment from custom prose is layered on top but never
required.

### B. Add a structured "Change Surface" block to each artifact

Rejected. It would give the richest, most machine-readable surface — but it is exactly the
**required authoring schema** the project value forbids, and it would only work on repos that adopt
the block, breaking the "works on default BMAD" constraint. This is the same trade-off Story 9.4
declined when it refused a `verified:` field; if a tagged surface record is ever wanted, that is its
own future ADR weighing the authoring burden.

### C. Diff-based surface extraction (parse the git diff instead of the artifact)

Rejected as the *primary* mechanism. A raw diff shows changed lines but not *intent* — it cannot tell
you which change is user-visible vs. incidental, nor map to acceptance criteria. The **File List**
already gives the portable footprint the diff would provide, tied to the story's own ACs. (A diff can
still be a secondary cross-check to catch files the author omitted from the File List.)

### D. Rely on SpecScribe's own rich prose ("Where this renders", "Verify before marking review")

Rejected as a dependency. These sections are excellent when present and *are* consumed
opportunistically (Option A's enrichment layer), but they are SpecScribe-authored conventions absent
from default BMAD stories. Building the core view on them would make the tool useless on the very
repos it is meant to serve.

## Consequences

**Positive**

- A reviewer/tester gets a change's observable footprint and a ready-made AC checklist derived
  automatically from sections every BMAD story already has — no reading the whole artifact.
- **Portable by design:** works on default BMAD implementations, not just SpecScribe's own
  richly-authored stories, satisfying the "support many frameworks" project value.
- **No artifact change and no new authoring schema** — the same guardrail Story 9.4 honored.
- "Invisible plumbing" is *detected* (File List = tests + logic, no view/asset paths) rather than
  depending on the author having said so in prose — recovering facts like Story 8.3's without its
  custom wording.

**Negative / costs**

- The derivation is heuristic: File-List path classification and prose enrichment are best-effort, so
  the surface view is a strong hint, not a proof. A File List that omits a touched file, or unusual
  path conventions, will under- or mis-classify. Acceptable because the ACs remain the authoritative
  behavior checklist and the view is advisory.
- Accuracy tracks artifact hygiene: a story with a thin File List or vague ACs yields a thin surface
  view. The tool improves the *reading* of what exists; it cannot manufacture signal that was never
  written.

**Neutral**

- Governs a *reading/projection* capability, not artifact authoring; no change to the generated site
  contract or the six `--status-*` tokens.
- Consistent with ADR 0002's shared-render-core: once this surface view exists it can itself be
  rendered host-neutrally across HTML / webview / SPA, like every other SpecScribe view.

