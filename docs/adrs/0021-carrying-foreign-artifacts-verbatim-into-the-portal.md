# ADR 0021: Foreign Artifacts May Be Carried Verbatim Into the Portal, as Gated Dead-End Leaves

**Status:** Proposed (authored 2026-07-27 by Story 18.4; ratification is the owner's)
**Date:** 2026-07-27
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0002 — Shared Rendering Core and Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md) (AD-1/AD-2, which this scopes rather than breaks); [ADR 0013 — The Text Twin Is the No-JS Contract](0013-text-twin-is-the-no-js-contract.md) (the JS-off posture the safety gate protects); [ADR 0016 — The Canonical IR Carries Rendered Prose HTML](0016-ir-carries-rendered-prose-html.md) (the IR's transport shape, which a carried leaf deliberately stays out of); [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (the vendored-asset precedent); Epic 18 (Story 18.4)

**Numbering note.** `0019` is claimed-but-unwritten by **two** stories (18.3's "LLM-generated artifacts are enrichment-only inputs" and 22.3's IR-projection reconciliation), and `0020` is pre-claimed by Story 18.5. `0021` is the first uncontested number. If those land differently, renumber this one — the content, not the digit, is the decision.

## Context

Until Story 18.4, **every page in the generated portal was composed by SpecScribe's own C# rendering core.** That was never written down as a rule, because nothing had ever tested it. The site's only non-composed outputs were *assets* — `specscribe.css`, `specscribe.js`, the vendored `prism.*` and `plotly-hierarchy.min.js` — files a page loads, not pages a reader navigates to.

`bmad-forge-idea` breaks that. On **every** exit it renders `{workspace}/forge-report.html`: a self-contained HTML file with inline CSS and an inline-SVG outcome seal, crediting the personas that pressure-tested the idea and naming what was rejected and why. That report is the "persona-objections / rationale content" Story 18.4's AC #1 asks the Ideas list to link to — and it is the one file the product could not previously see at all (`EnumerateSourceFiles` globs `*.md` only).

Three options existed, and the owner locked the third (decision D1, 2026-07-27):

1. **Link to `forged-idea.md`.** Rejected: that file exists only on a *hardened* exit, so every killed, clarified and in-progress idea would have had no destination at all.
2. **Re-render the report from the memlog.** Rejected: the report's substance — persona voices, the seal, what survived scrutiny — is LLM-authored prose that cannot be reconstructed from the memlog's one-line entries. Re-rendering would quietly *replace* the record with a lesser one.
3. **Synthesize a detail page AND carry the original report alongside it.** Chosen. The synthesized page is composed by the core as usual; the report is carried through **verbatim**.

Option 3 introduces a second class of output. That is a cross-cutting precedent — the next story that wants to surface a foreign HTML artifact (a coverage report, a TEA `nfr-report`, a vendor export) will cite it — so CLAUDE.md requires it be proposed as an ADR rather than buried in a story file.

**It is narrower than it first looks.** `SiteGenerator.WriteOutput` already takes a `string`, so "carry the report" is a read-then-write, not a new copy mechanism or asset pipeline. The architectural question is not *how*; it is *whether the portal may contain a page it did not compose, and under what conditions.*

## Decision

**1. A foreign artifact MAY be carried into the portal output verbatim — never rewritten, restyled, sanitized-by-transformation, or wrapped.** It is carried whole or not at all. Wrapping it in `HtmlTemplater.RenderPage` would nest one complete `<html>` document inside another — the exact defect class Story 23.3 hit, where every harness passed while 187 pages were structurally corrupt.

**2. A carried artifact is a DEAD-END LEAF, not a portal page.** It carries no site nav, no breadcrumb, no footer, and no portal stylesheet. The page that links to it must say so in words, so a reader is never stranded wondering where the chrome went. It is reached from exactly one composed page and links nowhere back.

**3. Carrying is gated on the artifact being self-contained and inert.** Before anything is written:

- **No script.** Not a `<script>` tag, not an inline event handler, not a `javascript:` URL, and not an embedding element (`<iframe>`/`<object>`/`<embed>` — an `iframe srcdoc` executes).
- **No external origin.** No `src=`/`href=` pointing at `http://`, `https://`, or a protocol-relative `//host`. Deliberately strict: it also rejects an ordinary outbound anchor. A carried artifact must be openable offline from `file://` and inside the webview's CSP, so "no external origins at all" is the honest reading — and the cost of a false reject is one absent link plus a diagnostic that says why.
- **A size cap.** `_spaCapture` feeds the SPA bundle whose chunker is byte-blind, so an unbounded foreign page would inflate every chunk. Story 18.4 uses 512 KB.

A failure at any gate means the artifact is **not written**, the linking page renders **without the link**, and exactly one `Skipped` diagnostic names which gate failed. Never a partial write, never a silent omission.

**4. A carried artifact does NOT enter the SPA/webview capture.** `SpaDelivery.ExtractContentRegion` slices the universal `<main id="main-content">` landmark; a foreign document has none, and the extractor degrades a landmark-less page to nav-markup-only. Capturing one would therefore ship a **content-empty route** in the bundle while the real, readable file sat on disk beside it. Written with capture off, the link resolves to that static file — which is exactly right for a dead-end leaf.

**5. AD-1 and AD-2 are scoped, not broken.** The *information* SpecScribe presents about a foreign artifact — its title, verdict, date, summary, chronology, forward links — is derived once in the core and travels as a host-neutral view model, exactly as AD-1/AD-2 require. What is carried is a **byte-identical copy of somebody else's document**, which the core never parses, projects, or re-renders. It is closer in kind to a vendored asset (ADR 0012's Plotly bundle) than to a page. The line: **SpecScribe never composes markup it did not author, and never claims authorship of markup it merely copied.**

**6. Not the IR's problem.** A carried artifact stays out of the canonical IR (ADR 0016). The IR transports rendered *prose* the core produced; a foreign document is neither. Epic 22/23's projection layer treats it as a static file at its own path.

## Consequences

**Accepted:**

- The portal now contains pages SpecScribe cannot vouch for the styling, accessibility, or link-hygiene of. Gate 3 bounds the *safety* of that, not the *quality*. A carried report with poor contrast ships as-is; the alternative is not surfacing the rationale content at all.
- Gate 3's strictness will occasionally reject a legitimate report — one that cites a URL in an ordinary anchor, say. That is a deliberate false-positive bias, made visible by the diagnostic rather than hidden.
- A reader who follows the link leaves the portal's navigation behind and returns via the browser's back button. Decision 2 makes that explicit in the linking page's copy instead of pretending otherwise.
- ADR 0013's text-twin contract does not apply (the artifact is prose, not a chart), but its *spirit* is what gate 3 protects: a carried page that only worked with JS would be a portal surface with no text twin, so it is refused.

**Rejected alternatives:**

- **Sanitizing a failing artifact** (stripping the script, inlining the remote stylesheet) — that produces a document the author did not write while still presenting it as "the original report". Verbatim-or-nothing is the only honest carry.
- **A general "copy any file into the output" seam.** This decision authorizes carrying a *named, contracted* artifact a known producer always emits, discovered by an explicit rule. It is not a static-asset mirror.

## Open question for the owner

Should the gate's diagnostics be `Skipped` (today's choice — the artifact was deliberately not ingested) or `Unsupported`? `Skipped` reads correctly for the size cap and for a deliberate refusal; a scripted report is arguably an artifact "whose shape isn't one the adapter can interpret". Story 18.4 chose `Skipped` for all four conditions so one gate reports one way. No behaviour depends on the answer.
